using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// محرّك تجميع التنفيذ (ERDS Phase 5) — قراءة فقط.
/// يقرأ القوالب التنفيذية الرقمية الستة (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع) من جدول TableGrid المخزَّن
/// كـ string[][] في ValueJson، ويطابق الأعمدة بالفهرس على أعمدة الـSchema، ثم يجمّعها حسب
/// (الفترة، الفريق/Pod، الموظّف، العميل، المشروع). لا يمسّ أيّ بيانات ولا يمسّ Phase 4 (B2C/B2B).
/// Pod = فريق المُسلِّم (Submitter.TeamId على التسليم). النطاق محكوم بـ IScopeResolver (تصفية على SubmitterId عند !SeesAll).
/// التقارير/الصفوف غير المطابقة تُتجاهَل بلا فشل، مع عدّها في البيانات التشخيصية.
/// </summary>
public class PodExecutionAggregationService : IPodExecutionAggregationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    public PodExecutionAggregationService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    // نسبة مئوية آمنة القسمة (المقام صفر ⇒ 0)، مقرّبة لمنزلة واحدة.
    private static decimal Pct(decimal num, decimal den) => den > 0 ? Math.Round(num / den * 100m, 1) : 0m;

    // نسبة مباشرة آمنة القسمة (لكل ساعة/تكلفة/عائد)، مقرّبة لمنزلتين.
    private static decimal Per(decimal num, decimal den) => den > 0 ? Math.Round(num / den, 2) : 0m;

    // قراءة خلية رقمية بأمان: خارج الحدود/فارغة/غير قابلة للتحويل ⇒ 0.
    // يمرّ عبر NumericNormalizer (RC-3 Task 2B) لتطبيع الخانات العربية-الهندية/الفارسية ⇒ حساب صحيح
    // بصرف النظر عن لغة لوحة المفاتيح، ودفاع للبيانات القديمة المخزَّنة بخانات عربية.
    private static decimal Num(string[] row, int idx)
    {
        if (idx < 0 || idx >= row.Length) return 0m;
        return NumericNormalizer.TryParseDecimal(row[idx], out var d) ? d : 0m;
    }

    // قراءة خلية نصّية بأمان (العميل/المشروع/الخطر).
    private static string Text(string[] row, int idx)
        => (idx >= 0 && idx < row.Length ? row[idx] : null)?.Trim() ?? string.Empty;

    // ترتيب مستوى الخطر لاختيار «الأسوأ» عبر صفوف المشاريع (نصّ حرّ عربي/إنجليزي).
    private static int RiskRank(string risk)
    {
        var r = risk.Trim().ToLowerInvariant();
        if (r.Length == 0) return 0;
        if (r.Contains("حرج") || r.Contains("critical") || r.Contains("جدا") || r.Contains("جدًا")) return 4;
        if (r.Contains("مرتفع") || r.Contains("عال") || r.Contains("high")) return 3;
        if (r.Contains("متوسط") || r.Contains("medium") || r.Contains("mid")) return 2;
        if (r.Contains("منخفض") || r.Contains("low") || r.Contains("بسيط")) return 1;
        return 0;
    }

    private string ViewLevel()
        => RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles)) switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary",
        };

    /// <summary>
    /// المسار المشترك: إيجاد قالب ERDS بالعنوان، جلب حقل الجدول، ثم التسليمات (غير المسودّات) ضمن النطاق/الفلاتر،
    /// وأخيرًا قراءة قيم الجدول (string[][]) لكل تسليم مع عدّ المتجاهَل بأمان. مطابق لنمط Phase 4.
    /// </summary>
    private async Task<GridScan?> ScanGridAsync(string templateTitle, string mainTableLabel,
        PodExecutionFilter filter, ScopeContext scope, CancellationToken ct)
    {
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == templateTitle, ct);
        if (template is null) return null;

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var gridFieldIds = template.Versions
            .SelectMany(v => v.Fields)
            .Where(f => f.FieldType == FieldType.TableGrid && f.Label == mainTableLabel)
            .Select(f => f.Id)
            .ToHashSet();
        if (gridFieldIds.Count == 0) return null;

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.EmployeeId is not null) subsQ = subsQ.Where(s => s.SubmitterId == filter.EmployeeId);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ
            .Select(s => new { s.Id, s.SubmitterId, s.TeamId, s.DepartmentId, s.PeriodType, s.PeriodKey })
            .ToListAsync(ct);

        var scan = new GridScan { SubmissionsConsidered = subs.Count };
        if (subs.Count == 0) return scan;

        var subIds = subs.Select(s => s.Id).ToList();
        var gridValues = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId) && gridFieldIds.Contains(v.TemplateFieldId))
            .Select(v => new { v.ReportSubmissionId, v.ValueJson })
            .ToListAsync(ct);

        var subMeta = subs.ToDictionary(s => s.Id, s => s);
        var valuesBySub = gridValues.GroupBy(v => v.ReportSubmissionId);
        var seenSubs = new HashSet<Guid>();

        foreach (var grp in valuesBySub)
        {
            var meta = subMeta[grp.Key];
            var anyRow = false;
            foreach (var val in grp)
            {
                if (string.IsNullOrWhiteSpace(val.ValueJson)) continue;
                string[][]? rows;
                try
                {
                    rows = JsonSerializer.Deserialize<string[][]>(val.ValueJson!);
                }
                catch (JsonException)
                {
                    // تسليم بجدول غير قابل للقراءة (توافق خلفي) ⇒ يُتجاهَل بلا فشل.
                    continue;
                }
                if (rows is null) continue;
                foreach (var row in rows)
                {
                    if (row is null) continue;
                    scan.RawRows.Add(new GridRow(meta.SubmitterId, meta.TeamId, meta.PeriodType, meta.PeriodKey ?? string.Empty, row));
                    anyRow = true;
                }
            }
            if (anyRow) seenSubs.Add(grp.Key);
        }

        scan.SubmissionsIgnored = subs.Count - seenSubs.Count;
        return scan;
    }

    /// <summary>
    /// يمسح القوالب الستة ويملأ مُجمِّعًا موحّدًا مفتاحه (PeriodType، الفترة، الموظّف، العميل، المشروع).
    /// القوالب الإنتاجية تحمل العميل بلا مشروع ⇒ Project فارغ؛ قالب المشاريع وحده يحمل مشروعًا.
    /// </summary>
    private async Task<(Dictionary<PodKey, PodAccum> Accum, Diag Diag)> ScanAllAsync(
        PodExecutionFilter filter, ScopeContext scope, CancellationToken ct)
    {
        var accum = new Dictionary<PodKey, PodAccum>();
        var diag = new Diag();

        var clientFilter = string.IsNullOrWhiteSpace(filter.Client) ? null : filter.Client.Trim();
        var projectFilter = string.IsNullOrWhiteSpace(filter.Project) ? null : filter.Project.Trim();

        bool MatchClient(string c) => clientFilter is null || string.Equals(c, clientFilter, StringComparison.OrdinalIgnoreCase);
        bool MatchProject(string p) => projectFilter is null || string.Equals(p, projectFilter, StringComparison.OrdinalIgnoreCase);

        PodAccum Bucket(GridRow r, string client, string project)
        {
            var key = new PodKey(r.PeriodType, r.PeriodKey, r.SubmitterId, client, project);
            if (!accum.TryGetValue(key, out var a))
            {
                a = new PodAccum { TeamId = r.TeamId };
                accum[key] = a;
            }
            return a;
        }

        // ✍️ المحتوى — إنتاج المحتوى لكل عميل.
        {
            var cols = ContentProductionReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(ContentProductionReportSchema.ColClient), iReq = I(ContentProductionReportSchema.ColPiecesRequired),
                iApproved = I(ContentProductionReportSchema.ColApproved), iLate = I(ContentProductionReportSchema.ColLate),
                iProject = I(ContentProductionReportSchema.ColProject), iHours = I(ContentProductionReportSchema.ColWorkHours);
            var scan = await ScanGridAsync(ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    if (client.Length == 0) { diag.RowsIgnored++; continue; }
                    var project = Text(r.Cells, iProject); // Work Unit: صفوف قديمة بلا عمود المشروع ⇒ "" (توافق خلفي).
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.ContentPieces += Num(r.Cells, iApproved);
                    a.DelayedItems += Num(r.Cells, iLate);
                    a.RequiredItems += Num(r.Cells, iReq);
                    a.WorkHours += Num(r.Cells, iHours); // صفوف قديمة بلا عمود ساعات ⇒ 0.
                }
            }
        }

        // 🎨 التصميم — إنتاج التصميم لكل عميل.
        {
            var cols = DesignProductionReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(DesignProductionReportSchema.ColClient), iReq = I(DesignProductionReportSchema.ColRequired),
                iDone = I(DesignProductionReportSchema.ColDone), iLate = I(DesignProductionReportSchema.ColLate),
                iProject = I(DesignProductionReportSchema.ColProject), iHours = I(DesignProductionReportSchema.ColWorkHours);
            var scan = await ScanGridAsync(DesignProductionReportSchema.TemplateTitle, DesignProductionReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    if (client.Length == 0) { diag.RowsIgnored++; continue; }
                    var project = Text(r.Cells, iProject); // Work Unit: صفوف قديمة بلا عمود المشروع ⇒ "" (توافق خلفي).
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.DesignsCompleted += Num(r.Cells, iDone);
                    a.DelayedItems += Num(r.Cells, iLate);
                    a.RequiredItems += Num(r.Cells, iReq);
                    a.WorkHours += Num(r.Cells, iHours); // صفوف قديمة بلا عمود ساعات ⇒ 0.
                }
            }
        }

        // 🎬 الفيديو — إنتاج الفيديو لكل عميل.
        {
            var cols = VideoProductionReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(VideoProductionReportSchema.ColClient), iReq = I(VideoProductionReportSchema.ColRequired),
                iEdited = I(VideoProductionReportSchema.ColEdited), iLate = I(VideoProductionReportSchema.ColLate),
                iProject = I(VideoProductionReportSchema.ColProject), iHours = I(VideoProductionReportSchema.ColWorkHours);
            var scan = await ScanGridAsync(VideoProductionReportSchema.TemplateTitle, VideoProductionReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    if (client.Length == 0) { diag.RowsIgnored++; continue; }
                    var project = Text(r.Cells, iProject); // Work Unit: صفوف قديمة بلا عمود المشروع ⇒ "" (توافق خلفي).
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.VideosCompleted += Num(r.Cells, iEdited);
                    a.DelayedItems += Num(r.Cells, iLate);
                    a.RequiredItems += Num(r.Cells, iReq);
                    a.WorkHours += Num(r.Cells, iHours); // صفوف قديمة بلا عمود ساعات ⇒ 0.
                }
            }
        }

        // 📣 النشر — النشر والتفاعل لكل عميل.
        {
            var cols = SocialPublishingReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(SocialPublishingReportSchema.ColClient), iScheduled = I(SocialPublishingReportSchema.ColPostsScheduled),
                iPublished = I(SocialPublishingReportSchema.ColPostsPublished), iMissed = I(SocialPublishingReportSchema.ColMissedPosts),
                iProject = I(SocialPublishingReportSchema.ColProject), iHours = I(SocialPublishingReportSchema.ColWorkHours);
            var scan = await ScanGridAsync(SocialPublishingReportSchema.TemplateTitle, SocialPublishingReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    if (client.Length == 0) { diag.RowsIgnored++; continue; }
                    var project = Text(r.Cells, iProject); // Work Unit: صفوف قديمة بلا عمود المشروع ⇒ "" (توافق خلفي).
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.PostsPublished += Num(r.Cells, iPublished);
                    a.MissedPosts += Num(r.Cells, iMissed);
                    a.PostsScheduled += Num(r.Cells, iScheduled);
                    a.WorkHours += Num(r.Cells, iHours); // صفوف قديمة بلا عمود ساعات ⇒ 0.
                }
            }
        }

        // 📊 Media Buyer — أداء الحملات لكل عميل (المنصّة مطويّة في الرؤية الموحّدة).
        {
            var cols = MediaBuyerByClientReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(MediaBuyerByClientReportSchema.ColClient), iSpend = I(MediaBuyerByClientReportSchema.ColSpend),
                iLeads = I(MediaBuyerByClientReportSchema.ColLeads), iPurch = I(MediaBuyerByClientReportSchema.ColPurchases),
                iRevenue = I(MediaBuyerByClientReportSchema.ColRevenue),
                iProject = I(MediaBuyerByClientReportSchema.ColProject), iHours = I(MediaBuyerByClientReportSchema.ColWorkHours);
            var scan = await ScanGridAsync(MediaBuyerByClientReportSchema.TemplateTitle, MediaBuyerByClientReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    if (client.Length == 0) { diag.RowsIgnored++; continue; }
                    var project = Text(r.Cells, iProject); // Work Unit: صفوف قديمة بلا عمود المشروع ⇒ "" (توافق خلفي).
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.Spend += Num(r.Cells, iSpend);
                    a.Leads += Num(r.Cells, iLeads);
                    a.Purchases += Num(r.Cells, iPurch);
                    a.Revenue += Num(r.Cells, iRevenue);
                    a.WorkHours += Num(r.Cells, iHours); // صفوف قديمة بلا عمود ساعات ⇒ 0.
                }
            }
        }

        // 🗂️ المشاريع — تقدّم المشاريع لكل عميل/مشروع (المصدر الوحيد لساعات العمل والمهام والخطر).
        {
            var cols = ProjectsByClientReportSchema.Columns;
            int I(string c) => Array.IndexOf(cols, c);
            int iClient = I(ProjectsByClientReportSchema.ColClient), iProject = I(ProjectsByClientReportSchema.ColProject),
                iHours = I(ProjectsByClientReportSchema.ColWorkHours), iBlocked = I(ProjectsByClientReportSchema.ColTasksBlocked),
                iRisk = I(ProjectsByClientReportSchema.ColRiskLevel);
            var scan = await ScanGridAsync(ProjectsByClientReportSchema.TemplateTitle, ProjectsByClientReportSchema.MainTableLabel, filter, scope, ct);
            if (scan is not null)
            {
                diag.Add(scan);
                foreach (var r in scan.RawRows)
                {
                    var client = Text(r.Cells, iClient);
                    var project = Text(r.Cells, iProject);
                    if (client.Length == 0 && project.Length == 0) { diag.RowsIgnored++; continue; }
                    if (!MatchClient(client) || !MatchProject(project)) continue;
                    var a = Bucket(r, client, project);
                    a.WorkHours += Num(r.Cells, iHours);
                    a.BlockedTasks += Num(r.Cells, iBlocked);
                    var risk = Text(r.Cells, iRisk);
                    var rank = RiskRank(risk);
                    if (rank >= a.RiskRank && risk.Length > 0) { a.RiskRank = rank; a.RiskText = risk; }
                }
            }
        }

        return (accum, diag);
    }

    public async Task<Result<PodExecutionReport>> AggregateByPodAsync(PodExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<PodExecutionReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (accum, diag) = await ScanAllAsync(filter, scope, ct);

        var empIds = accum.Keys.Select(k => k.Emp).Distinct().ToList();
        var teamIds = accum.Values.Where(a => a.TeamId is not null).Select(a => a.TeamId!.Value).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);
        var teams = await TeamNamesAsync(teamIds, ct);

        var rows = accum
            .Select(kv =>
            {
                var k = kv.Key;
                var a = kv.Value;
                var indicators = new PodProductivityIndicators(
                    Per(a.ContentPieces, a.WorkHours),
                    Per(a.DesignsCompleted, a.WorkHours),
                    Per(a.VideosCompleted, a.WorkHours),
                    Per(a.PostsPublished, a.WorkHours),
                    Pct(a.DelayedItems, a.RequiredItems),
                    Pct(a.MissedPosts, a.PostsScheduled),
                    Per(a.Spend, a.Leads),
                    Per(a.Spend, a.Purchases),
                    Per(a.Revenue, a.Spend));
                return new PodExecutionAggregationDto(
                    k.PeriodType, k.Period, a.TeamId,
                    a.TeamId is not null ? teams.GetValueOrDefault(a.TeamId.Value, string.Empty) : string.Empty,
                    k.Emp, names.GetValueOrDefault(k.Emp, string.Empty), k.Client, k.Project,
                    a.WorkHours, a.ContentPieces, a.DesignsCompleted, a.VideosCompleted, a.PostsPublished,
                    a.MissedPosts, a.DelayedItems, a.BlockedTasks, a.Spend, a.Leads, a.Purchases, a.Revenue,
                    a.RequiredItems, a.PostsScheduled, a.RiskText, indicators);
            })
            .OrderBy(r => r.PeriodKey).ThenBy(r => r.EmployeeName).ThenBy(r => r.Client).ThenBy(r => r.Project)
            .ToList();

        return Result<PodExecutionReport>.Success(new PodExecutionReport(
            filter.PeriodKey, rows.Count, diag.SubmissionsConsidered, diag.SubmissionsIgnored, diag.RowsIgnored, viewLevel, rows));
    }

    public async Task<Result<ClientExecutionReport>> AggregateByClientAsync(PodExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ClientExecutionReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (accum, diag) = await ScanAllAsync(filter, scope, ct);

        var byClient = new Dictionary<(string Client, string Project), ClientAccum>();
        foreach (var kv in accum)
        {
            var k = (kv.Key.Client, kv.Key.Project);
            if (!byClient.TryGetValue(k, out var c))
            {
                c = new ClientAccum();
                byClient[k] = c;
            }
            var a = kv.Value;
            c.WorkHours += a.WorkHours;
            c.ContentPieces += a.ContentPieces;
            c.Designs += a.DesignsCompleted;
            c.Videos += a.VideosCompleted;
            c.PublishedPosts += a.PostsPublished;
            c.MissedPosts += a.MissedPosts;
            c.DelayedItems += a.DelayedItems;
            c.BlockedTasks += a.BlockedTasks;
            c.Spend += a.Spend;
            c.Leads += a.Leads;
            c.Purchases += a.Purchases;
            c.Revenue += a.Revenue;
            if (a.RiskRank >= c.RiskRank && a.RiskText.Length > 0) { c.RiskRank = a.RiskRank; c.RiskText = a.RiskText; }
        }

        var rows = byClient
            .Select(kv => new ClientExecutionAggregationDto(
                kv.Key.Client, kv.Key.Project,
                kv.Value.WorkHours, kv.Value.ContentPieces, kv.Value.Designs, kv.Value.Videos,
                kv.Value.PublishedPosts, kv.Value.MissedPosts, kv.Value.DelayedItems, kv.Value.BlockedTasks,
                kv.Value.Spend, kv.Value.Leads, kv.Value.Purchases, kv.Value.Revenue,
                Per(kv.Value.Spend, kv.Value.Leads),        // CPL من المجاميع
                Per(kv.Value.Spend, kv.Value.Purchases),    // CPA من المجاميع
                Per(kv.Value.Revenue, kv.Value.Spend),      // ROAS من المجاميع
                kv.Value.RiskText))
            .OrderBy(r => r.Client).ThenBy(r => r.Project)
            .ToList();

        return Result<ClientExecutionReport>.Success(new ClientExecutionReport(
            filter.PeriodKey, rows.Count, diag.SubmissionsConsidered, diag.SubmissionsIgnored, diag.RowsIgnored, viewLevel, rows));
    }

    public async Task<Result<ProjectExecutionReport>> AggregateByProjectAsync(PodExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ProjectExecutionReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);

        var scan = await ScanGridAsync(ProjectsByClientReportSchema.TemplateTitle, ProjectsByClientReportSchema.MainTableLabel, filter, scope, ct);
        if (scan is null)
            return Result<ProjectExecutionReport>.Success(new ProjectExecutionReport(filter.PeriodKey, 0, 0, 0, 0, viewLevel, new List<ProjectExecutionAggregationDto>()));

        var cols = ProjectsByClientReportSchema.Columns;
        int I(string c) => Array.IndexOf(cols, c);
        int iClient = I(ProjectsByClientReportSchema.ColClient), iProject = I(ProjectsByClientReportSchema.ColProject),
            iHours = I(ProjectsByClientReportSchema.ColWorkHours), iPlanned = I(ProjectsByClientReportSchema.ColTasksPlanned),
            iDone = I(ProjectsByClientReportSchema.ColTasksDone), iLate = I(ProjectsByClientReportSchema.ColTasksLate),
            iBlocked = I(ProjectsByClientReportSchema.ColTasksBlocked), iProgress = I(ProjectsByClientReportSchema.ColProgressPct),
            iRisk = I(ProjectsByClientReportSchema.ColRiskLevel);

        var clientFilter = string.IsNullOrWhiteSpace(filter.Client) ? null : filter.Client.Trim();
        var projectFilter = string.IsNullOrWhiteSpace(filter.Project) ? null : filter.Project.Trim();
        var rowsIgnored = 0;
        var accum = new Dictionary<(string Period, Guid Emp, string Client, string Project), ProjectAccum>();

        foreach (var r in scan.RawRows)
        {
            var client = Text(r.Cells, iClient);
            var project = Text(r.Cells, iProject);
            if (client.Length == 0 && project.Length == 0) { rowsIgnored++; continue; }
            if (clientFilter is not null && !string.Equals(client, clientFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (projectFilter is not null && !string.Equals(project, projectFilter, StringComparison.OrdinalIgnoreCase)) continue;

            var key = (r.PeriodKey, r.SubmitterId, client, project);
            if (!accum.TryGetValue(key, out var a))
            {
                a = new ProjectAccum { TeamId = r.TeamId };
                accum[key] = a;
            }
            a.WorkHours += Num(r.Cells, iHours);
            a.Planned += Num(r.Cells, iPlanned);
            a.Done += Num(r.Cells, iDone);
            a.Late += Num(r.Cells, iLate);
            a.Blocked += Num(r.Cells, iBlocked);
            a.ProgressSum += Num(r.Cells, iProgress);
            a.RowCount++;
            var risk = Text(r.Cells, iRisk);
            var rank = RiskRank(risk);
            if (rank >= a.RiskRank && risk.Length > 0) { a.RiskRank = rank; a.RiskText = risk; }
        }

        var empIds = accum.Keys.Select(k => k.Emp).Distinct().ToList();
        var teamIds = accum.Values.Where(a => a.TeamId is not null).Select(a => a.TeamId!.Value).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);
        var teams = await TeamNamesAsync(teamIds, ct);

        var rows = accum
            .Select(kv =>
            {
                var (period, emp, client, project) = kv.Key;
                var a = kv.Value;
                var progressAvg = a.RowCount > 0 ? Math.Round(a.ProgressSum / a.RowCount, 1) : 0m;
                return new ProjectExecutionAggregationDto(
                    period, emp, names.GetValueOrDefault(emp, string.Empty), a.TeamId,
                    a.TeamId is not null ? teams.GetValueOrDefault(a.TeamId.Value, string.Empty) : string.Empty,
                    client, project, a.WorkHours, a.Planned, a.Done, a.Late, a.Blocked,
                    progressAvg, Pct(a.Done, a.Planned), a.RiskText);
            })
            .OrderBy(r => r.PeriodKey).ThenBy(r => r.EmployeeName).ThenBy(r => r.Client).ThenBy(r => r.Project)
            .ToList();

        return Result<ProjectExecutionReport>.Success(new ProjectExecutionReport(
            filter.PeriodKey, rows.Count, scan.SubmissionsConsidered, scan.SubmissionsIgnored, rowsIgnored, viewLevel, rows));
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Teams.AsNoTracking().Where(t => distinct.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
    }

    private sealed class GridScan
    {
        public int SubmissionsConsidered { get; set; }
        public int SubmissionsIgnored { get; set; }
        public List<GridRow> RawRows { get; } = new();
    }

    private readonly record struct GridRow(Guid SubmitterId, Guid? TeamId, PeriodType PeriodType, string PeriodKey, string[] Cells);

    // مفتاح التجميع الموحّد. القوالب الإنتاجية Project="".
    private readonly record struct PodKey(PeriodType PeriodType, string Period, Guid Emp, string Client, string Project);

    private sealed class Diag
    {
        public int SubmissionsConsidered { get; private set; }
        public int SubmissionsIgnored { get; private set; }
        public int RowsIgnored { get; set; }
        public void Add(GridScan scan)
        {
            SubmissionsConsidered += scan.SubmissionsConsidered;
            SubmissionsIgnored += scan.SubmissionsIgnored;
        }
    }

    private sealed class PodAccum
    {
        public Guid? TeamId { get; set; }
        public decimal WorkHours, ContentPieces, DesignsCompleted, VideosCompleted, PostsPublished, MissedPosts;
        public decimal DelayedItems, BlockedTasks, Spend, Leads, Purchases, Revenue, RequiredItems, PostsScheduled;
        public int RiskRank;
        public string RiskText = string.Empty;
    }

    private sealed class ClientAccum
    {
        public decimal WorkHours, ContentPieces, Designs, Videos, PublishedPosts, MissedPosts;
        public decimal DelayedItems, BlockedTasks, Spend, Leads, Purchases, Revenue;
        public int RiskRank;
        public string RiskText = string.Empty;
    }

    private sealed class ProjectAccum
    {
        public Guid? TeamId { get; set; }
        public decimal WorkHours, Planned, Done, Late, Blocked, ProgressSum;
        public int RowCount;
        public int RiskRank;
        public string RiskText = string.Empty;
    }
}

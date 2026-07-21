using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// محرّك التجميع Project-First (PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1) — قراءة فقط.
/// يقرأ قوالب التنفيذ الأربعة (ProjectFirstExecutionSchema.ExecutionTemplateTitles) من حقول ProjectRepeatableSection،
/// حيث كل الأرقام التشغيلية مخزَّنة داخل كل مشروع في Answers (لا top-level)، ويستخرج المقاييس <b>حسب القالب</b>
/// عبر خريطة المفاتيح الحقيقية (v5) في ProjectFirstExecutionSchema.MapFor، ثم يجمّع على معرّفات المشاريع الحقيقية
/// حسب (المشروع/الموظّف/Pod/العميل). لا يمسّ المسار القديم (Family B المسطّح) ولا مسار المبيعات (B2C/B2B).
/// Pod = فريق المُسلِّم (Submitter.TeamId). الرؤية = اتحاد محورين مفروضين خادميًّا:
/// (1) نطاق التسلسل الإداري IScopeResolver (المُسلِّم داخل النطاق ⇒ ترى كل مدخلاته)، و(2) حافظة المشاريع
/// IClientProjectAccess (مدير الحساب يرى مدخلات مشاريع عملائه ولو سلّمها موظّف خارج نطاقه الإداري).
/// عند SeesAll لا قيد. المدخلات بلا ProjectId صالح تُتجاهَل بلا فشل مع عدّها تشخيصيًّا.
/// </summary>
public class ProjectFirstExecutionAggregationService : IProjectFirstExecutionAggregationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IClientProjectAccess _access;

    public ProjectFirstExecutionAggregationService(
        AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IClientProjectAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _access = access;
    }

    // نسبة مئوية آمنة القسمة (المقام صفر ⇒ 0)، مقرّبة لمنزلة واحدة.
    private static decimal Pct(decimal num, decimal den) => den > 0 ? Math.Round(num / den * 100m, 1) : 0m;

    private string ViewLevel()
        => RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles)) switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary",
        };

    private static readonly JsonSerializerOptions PrsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class PrsEntryDto
    {
        public Guid? ProjectId { get; set; }
        public Dictionary<string, JsonElement>? Answers { get; set; }
    }

    // مدخل مشروع واحد من قسم PRS مع القيم الرقمية القياسية والحالة المطبَّعة المقروءة بأمان.
    private readonly record struct ProjEntry(
        Guid SubmitterId, Guid? TeamId, PeriodType PeriodType, string PeriodKey, Guid ProjectId,
        decimal Planned, decimal Completed, decimal Approved, decimal Revisions, decimal Published, decimal Delayed,
        decimal MessagesIn, decimal Responses, decimal IssueComments, decimal Escalations, string Status);

    // أسباب إسقاط مدخلات المشاريع (كلّها داخل النطاق — لا تكشف بيانات خارج النطاق تلبيةً لـ §9).
    private const string ReasonEmptyEntry = "empty_project_entry";
    private const string ReasonOutsideProjectFilter = "outside_project_filter";
    private const string ReasonOutsideClientFilter = "outside_client_filter";

    private sealed class Diag
    {
        public int SubmissionsConsidered { get; set; }
        public int SubmissionsIgnored { get; set; }
        public int EntriesIgnored { get; set; }        // = empty_project_entry (توافق خلفي).
        public int RowsConsidered { get; set; }        // مدخلات مشاريع مرئية فُحِصت.
        public int RowsIgnored { get; set; }           // مدخلات مرئية أُسقِطت (فارغ/خارج فلتر مشروع أو عميل).
        public Dictionary<string, int> IgnoredReasons { get; } = new();

        public void Ignore(string reason)
        {
            RowsIgnored++;
            IgnoredReasons[reason] = IgnoredReasons.GetValueOrDefault(reason) + 1;
        }
    }

    private sealed class MetricsAccum
    {
        public decimal Planned, Completed, Approved, Revisions, Published, Delayed;
        public decimal MessagesIn, Responses, IssueComments, Escalations;
        public int StHealthy, StStable, StNeeds, StUnspecified, StTotal;

        public void Add(in ProjEntry e)
        {
            Planned += e.Planned; Completed += e.Completed; Approved += e.Approved;
            Revisions += e.Revisions; Published += e.Published; Delayed += e.Delayed;
            MessagesIn += e.MessagesIn; Responses += e.Responses;
            IssueComments += e.IssueComments; Escalations += e.Escalations;
            switch (e.Status)
            {
                case ProjectFirstExecutionSchema.StatusHealthy: StHealthy++; break;
                case ProjectFirstExecutionSchema.StatusStable: StStable++; break;
                case ProjectFirstExecutionSchema.StatusNeedsIntervention: StNeeds++; break;
                default: StUnspecified++; break;
            }
            StTotal++;
        }

        // المقياس الرئيسي للمقارنة الدورية = مجموع المخرجات (المُنجَز + الردود) يغطّي الإنتاج والمديرشن معًا.
        public decimal Headline => Completed + Responses;

        public ProjectExecMetrics Build() => new(
            Planned, Completed, Approved, Revisions, Published, Delayed,
            MessagesIn, Responses, IssueComments, Escalations,
            Pct(Completed, Planned), Pct(Approved, Completed), Pct(Published, Approved), Pct(Responses, MessagesIn));

        public ProjectStatusTally BuildStatus() => new(StHealthy, StStable, StNeeds, StUnspecified, StTotal);
    }

    // نوع الفترة الفعّال لاشتقاق الفترة السابقة: من الفلتر إن حُدّد، وإلا يُستنتَج أسبوعيًّا من صيغة المفتاح.
    private static PeriodType? EffectivePeriodType(ProjectFirstExecutionFilter filter)
    {
        if (filter.PeriodType is not null) return filter.PeriodType;
        return ReportCalendarPolicy.IsWeekKey(filter.PeriodKey) ? PeriodType.Weekly : null;
    }

    // مفتاح الفترة السابقة (null ⇒ لا مقارنة): يتطلّب مفتاحًا حاليًّا محدَّدًا ونوع فترة قابلًا للاشتقاق.
    private static string? PreviousKey(ProjectFirstExecutionFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.PeriodKey)) return null;
        var pt = EffectivePeriodType(filter);
        return pt is null ? null : ReportCalendarPolicy.PreviousPeriodKey(pt.Value, filter.PeriodKey);
    }

    // يبني كائن المقارنة لمقياس رئيسي: previous=null (المفتاح غير موجود بالفترة السابقة) ⇒ «لا بيانات سابقة».
    private static PeriodComparison BuildComparison(decimal current, decimal? previous)
    {
        if (previous is null) return new PeriodComparison(current, 0m, 0m, null, "none", false);
        var prev = previous.Value;
        var change = current - prev;
        decimal? pct = prev != 0m ? Math.Round(change / prev * 100m, 1) : null;
        var trend = change > 0m ? "up" : change < 0m ? "down" : "stable";
        return new PeriodComparison(current, prev, change, pct, trend, true);
    }

    private static bool TryReadNumber(Dictionary<string, JsonElement> answers, string key, out decimal value)
    {
        value = 0m;
        if (!answers.TryGetValue(key, out var el)) return false;
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetDecimal(out value);
            case JsonValueKind.String:
                // يمرّ عبر NumericNormalizer لتطبيع الخانات العربية-الهندية/الفارسية قبل التحويل.
                return NumericNormalizer.TryParseDecimal(el.GetString(), out value);
            default:
                return false;
        }
    }

    private static decimal Read(Dictionary<string, JsonElement> answers, string key)
        => TryReadNumber(answers, key, out var v) ? v : 0m;

    // مجموع القيم الرقمية لمجموعة مفاتيح مصدر (Strategy A داخل المدخل الواحد؛ مصفوفة فارغة ⇒ 0).
    private static decimal Sum(Dictionary<string, JsonElement> answers, string[] keys)
    {
        decimal total = 0m;
        foreach (var k in keys) total += Read(answers, k);
        return total;
    }

    // يقرأ قيمة حقل Select (نصّيًّا) لتطبيع حالة المشروع.
    private static string? ReadRaw(Dictionary<string, JsonElement> answers, string key)
    {
        if (!answers.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    /// <summary>
    /// يمسح قوالب التنفيذ الأربعة ويُنتج قائمة مدخلات مشاريع مع القيم القياسية المستخرجة حسب القالب. يطبّق فلاتر
    /// التسليم/الفترة/الموظّف/الفريق، وفلتر ProjectId مباشرةً على مدخل المشروع، والرؤية على مستوى المدخل =
    /// (المُسلِّم داخل نطاق IScopeResolver) أو (المشروع ضمن حافظة IClientProjectAccess). المدخلات بلا ProjectId صالح
    /// تُعدّ EntriesIgnored وتُتجاهَل.
    /// </summary>
    private async Task<(List<ProjEntry> Entries, Diag Diag)> ScanEntriesAsync(
        ProjectFirstExecutionFilter filter, ScopeContext scope, CancellationToken ct)
    {
        var diag = new Diag();
        var entries = new List<ProjEntry>();

        // حافظة المشاريع (المحور الثاني) تُحسَب فقط حين لا يرى المستخدم الكل — لمدير الحساب/قائد الفريق المسؤول.
        var vis = scope.SeesAll ? null : await _access.ResolveAsync(ct);

        var templates = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => ProjectFirstExecutionSchema.ExecutionTemplateTitles.Contains(t.Title))
            .ToListAsync(ct);
        if (templates.Count == 0) return (entries, diag);

        // خريطة نسخة القالب ← عنوان القالب (لاستخراج المقاييس حسب القالب لكل تسليم).
        var versionToTitle = templates
            .SelectMany(t => t.Versions.Select(v => new { v.Id, t.Title }))
            .ToDictionary(x => x.Id, x => x.Title);

        var versionIds = versionToTitle.Keys.ToList();
        var prsFieldIds = templates
            .SelectMany(t => t.Versions)
            .SelectMany(v => v.Fields)
            .Where(f => f.FieldType == FieldType.ProjectRepeatableSection)
            .Select(f => f.Id)
            .ToHashSet();
        if (prsFieldIds.Count == 0) return (entries, diag);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.EmployeeId is not null) subsQ = subsQ.Where(s => s.SubmitterId == filter.EmployeeId);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        // ملاحظة: لا تصفية للمُسلِّم على مستوى SQL — الرؤية تُطبَّق على مستوى المدخل لأنّ محور الحافظة
        // (مدير الحساب) يشمل مشاريع سلّمها موظّفون خارج نطاق المستخدم الإداري.

        var subs = await subsQ
            .Select(s => new { s.Id, s.SubmitterId, s.TeamId, s.PeriodType, s.PeriodKey, s.ReportTemplateVersionId })
            .ToListAsync(ct);

        if (subs.Count == 0) return (entries, diag);

        var subMeta = subs.ToDictionary(s => s.Id, s => s);
        var subIds = subs.Select(s => s.Id).ToList();

        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId)
                && prsFieldIds.Contains(v.TemplateFieldId)
                && v.ValueJson != null)
            .Select(v => new { v.ReportSubmissionId, v.ValueJson })
            .ToListAsync(ct);

        var seenSubs = new HashSet<Guid>();
        var consideredSubs = new HashSet<Guid>();

        foreach (var v in values)
        {
            List<PrsEntryDto>? prs;
            try { prs = JsonSerializer.Deserialize<List<PrsEntryDto>>(v.ValueJson!, PrsJsonOptions); }
            catch { continue; } // ValueJson تالف يُتجاهَل بأمان (توافق خلفي).
            if (prs is null) continue;

            var meta = subMeta[v.ReportSubmissionId];
            // خريطة المفاتيح الحقيقية لهذا التسليم بحسب عنوان قالبه (نسخة القالب المثبَّتة على التسليم).
            var map = versionToTitle.TryGetValue(meta.ReportTemplateVersionId, out var title)
                ? ProjectFirstExecutionSchema.MapFor(title)
                : null;
            if (map is null) continue; // تسليم بنسخة قالب غير معروفة ضمن الأربعة ⇒ يُتخطّى بأمان.

            // المُسلِّم داخل النطاق الإداري ⇒ كل مدخلاته مرئية؛ وإلا يُقاس المشروع على الحافظة (مدير الحساب).
            var submitterInScope = vis is null || scope.UserIds.Contains(meta.SubmitterId);
            if (submitterInScope) consideredSubs.Add(v.ReportSubmissionId);

            foreach (var e in prs)
            {
                if (e.ProjectId is not Guid pid || pid == Guid.Empty || e.Answers is null)
                {
                    // Project-First: كل الأرقام يجب أن تكون داخل مشروع محدَّد. تُعدّ تشخيصيًّا فقط لتسليم داخل النطاق.
                    if (submitterInScope)
                    {
                        diag.RowsConsidered++;
                        diag.EntriesIgnored++;
                        diag.Ignore(ReasonEmptyEntry);
                    }
                    continue;
                }

                // الرؤية على مستوى المدخل أولًا: داخل النطاق الإداري أو المشروع ضمن حافظة المستخدم.
                // المدخلات غير المرئية تُتخطّى بصمت (لا تُعدّ) كي لا تكشف التشخيصات بيانات خارج النطاق (§9).
                var visible = submitterInScope || vis!.ProjectIds.Contains(pid);
                if (!visible) continue;

                diag.RowsConsidered++;
                if (filter.ProjectId is not null && pid != filter.ProjectId)
                {
                    diag.Ignore(ReasonOutsideProjectFilter);
                    continue;
                }
                if (!submitterInScope) consideredSubs.Add(v.ReportSubmissionId); // ظهر عبر محور الحافظة.

                var a = e.Answers;
                entries.Add(new ProjEntry(
                    meta.SubmitterId, meta.TeamId, meta.PeriodType, meta.PeriodKey ?? string.Empty, pid,
                    Sum(a, map.Planned),
                    Sum(a, map.Completed),
                    Sum(a, map.Approved),
                    Sum(a, map.Revisions),
                    0m, // Published: لا مصدر في v5 ⇒ دائمًا صفر.
                    Sum(a, map.Delayed),
                    Sum(a, map.MessagesIn),
                    Sum(a, map.Responses),
                    Sum(a, map.IssueComments),
                    Sum(a, map.Escalations),
                    ProjectFirstExecutionSchema.NormalizeStatus(ReadRaw(a, ProjectFirstExecutionSchema.StatusKey))));
                seenSubs.Add(v.ReportSubmissionId);
            }
        }

        diag.SubmissionsConsidered = consideredSubs.Count;
        diag.SubmissionsIgnored = consideredSubs.Count - seenSubs.Count;
        return (entries, diag);
    }

    // يحلّ المشاريع المُشار إليها → (الاسم، ClientId، الحالة) ثمّ العملاء → الاسم؛ ويطبّق فلتر ClientId (إن وُجد) بحذف
    // مدخلات مشاريع لا تنتمي للعميل المطلوب (تُعدّ outside_client_filter في diag إن مُرِّر). ActiveProjectIds = المشاريع Active.
    private async Task<(List<ProjEntry> Filtered, Dictionary<Guid, (string Name, Guid? ClientId)> Projects, Dictionary<Guid, string> Clients, HashSet<Guid> ActiveProjectIds)>
        ResolveAndFilterAsync(List<ProjEntry> entries, ProjectFirstExecutionFilter filter, Diag? diag, CancellationToken ct)
    {
        var projectIds = entries.Select(e => e.ProjectId).Distinct().ToList();
        var projectRows = projectIds.Count == 0
            ? new List<(Guid Id, string Name, Guid ClientId, ProjectStatus Status)>()
            : (await _db.Projects.AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.ClientId, p.Status })
                .ToListAsync(ct))
                .Select(p => (p.Id, p.Name, p.ClientId, p.Status)).ToList();

        var projects = projectRows.ToDictionary(p => p.Id, p => (p.Name, (Guid?)p.ClientId));
        var activeProjectIds = projectRows.Where(p => p.Status == ProjectStatus.Active).Select(p => p.Id).ToHashSet();

        var clientIds = projects.Values.Where(p => p.Item2 is not null).Select(p => p.Item2!.Value).Distinct().ToList();
        var clients = clientIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Clients.AsNoTracking()
                .Where(c => clientIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        List<ProjEntry> filtered;
        if (filter.ClientId is null)
        {
            filtered = entries;
        }
        else
        {
            filtered = new List<ProjEntry>(entries.Count);
            foreach (var e in entries)
            {
                if (projects.TryGetValue(e.ProjectId, out var p) && p.Item2 == filter.ClientId) filtered.Add(e);
                else diag?.Ignore(ReasonOutsideClientFilter);
            }
        }

        return (filtered, projects, clients, activeProjectIds);
    }

    // يحمّل مدخلات الفترة السابقة (مُصفّاة بنفس الفلاتر عدا المفتاح) لبناء المقارنة الدورية؛ null إن تعذّر اشتقاق فترة سابقة.
    private async Task<(string PrevKey, List<ProjEntry> Entries)?> LoadPreviousEntriesAsync(
        ProjectFirstExecutionFilter filter, ScopeContext scope, CancellationToken ct)
    {
        var prevKey = PreviousKey(filter);
        if (prevKey is null) return null;

        var prevFilter = filter with { PeriodKey = prevKey, PeriodType = EffectivePeriodType(filter) };
        var (raw, _) = await ScanEntriesAsync(prevFilter, scope, ct);
        var (entries, _, _, _) = await ResolveAndFilterAsync(raw, prevFilter, null, ct);
        return (prevKey, entries);
    }

    // يبني ظرف النتيجة العامّة من الصفوف + التشخيصات + مفتاح الفترة السابقة المشتقّ.
    private static ProjectFirstExecutionReport<TRow> BuildReport<TRow>(
        ProjectFirstExecutionFilter filter, string? prevKey, string viewLevel, Diag diag, List<TRow> rows)
        => new(filter.PeriodKey, prevKey, rows.Count,
            diag.SubmissionsConsidered, diag.SubmissionsIgnored, diag.EntriesIgnored,
            diag.RowsConsidered, diag.RowsIgnored, diag.IgnoredReasons, viewLevel, rows);

    public async Task<Result<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>> AggregateByProjectAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (raw, diag) = await ScanEntriesAsync(filter, scope, ct);
        var (entries, projects, clients, _) = await ResolveAndFilterAsync(raw, filter, diag, ct);
        var prev = await LoadPreviousEntriesAsync(filter, scope, ct);

        var accum = new Dictionary<Guid, MetricsAccum>();
        var contributors = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in entries)
        {
            if (!accum.TryGetValue(e.ProjectId, out var a)) accum[e.ProjectId] = a = new MetricsAccum();
            a.Add(e);
            if (!contributors.TryGetValue(e.ProjectId, out var set)) contributors[e.ProjectId] = set = new HashSet<Guid>();
            set.Add(e.SubmitterId);
        }

        Dictionary<Guid, decimal>? prevHeadline = null;
        if (prev is not null)
        {
            prevHeadline = new Dictionary<Guid, decimal>();
            foreach (var e in prev.Value.Entries)
                prevHeadline[e.ProjectId] = prevHeadline.GetValueOrDefault(e.ProjectId) + e.Completed + e.Responses;
        }

        var rows = accum
            .Select(kv =>
            {
                var meta = projects.GetValueOrDefault(kv.Key, (string.Empty, (Guid?)null));
                var clientName = meta.Item2 is Guid cid ? clients.GetValueOrDefault(cid, string.Empty) : string.Empty;
                var comparison = prevHeadline is null ? null
                    : BuildComparison(kv.Value.Headline, prevHeadline.TryGetValue(kv.Key, out var pv) ? pv : null);
                return new ProjectFirstByProjectRow(kv.Key, meta.Item1, meta.Item2, clientName,
                    contributors.GetValueOrDefault(kv.Key)?.Count ?? 0, kv.Value.Build(), comparison, kv.Value.BuildStatus());
            })
            .OrderBy(r => r.ClientName).ThenBy(r => r.ProjectName)
            .ToList();

        return Result<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>.Success(
            BuildReport(filter, prev?.PrevKey, viewLevel, diag, rows));
    }

    public async Task<Result<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>> AggregateByEmployeeAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (raw, diag) = await ScanEntriesAsync(filter, scope, ct);
        var (entries, projects, clients, _) = await ResolveAndFilterAsync(raw, filter, diag, ct);
        var prev = await LoadPreviousEntriesAsync(filter, scope, ct);

        var accum = new Dictionary<(Guid Emp, Guid Project), (Guid? TeamId, MetricsAccum M)>();
        foreach (var e in entries)
        {
            var key = (e.SubmitterId, e.ProjectId);
            if (!accum.TryGetValue(key, out var a)) accum[key] = a = (e.TeamId, new MetricsAccum());
            a.M.Add(e);
        }

        Dictionary<(Guid, Guid), decimal>? prevHeadline = null;
        if (prev is not null)
        {
            prevHeadline = new Dictionary<(Guid, Guid), decimal>();
            foreach (var e in prev.Value.Entries)
            {
                var k = (e.SubmitterId, e.ProjectId);
                prevHeadline[k] = prevHeadline.GetValueOrDefault(k) + e.Completed + e.Responses;
            }
        }

        var empIds = accum.Keys.Select(k => k.Emp).Distinct().ToList();
        var teamIds = accum.Values.Where(a => a.TeamId is not null).Select(a => a.TeamId!.Value).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);
        var teams = await TeamNamesAsync(teamIds, ct);

        var rows = accum
            .Select(kv =>
            {
                var (emp, project) = kv.Key;
                var (teamId, m) = kv.Value;
                var meta = projects.GetValueOrDefault(project, (string.Empty, (Guid?)null));
                var clientName = meta.Item2 is Guid cid ? clients.GetValueOrDefault(cid, string.Empty) : string.Empty;
                var comparison = prevHeadline is null ? null
                    : BuildComparison(m.Headline, prevHeadline.TryGetValue(kv.Key, out var pv) ? pv : null);
                return new ProjectFirstByEmployeeRow(
                    emp, names.GetValueOrDefault(emp, string.Empty), teamId,
                    teamId is not null ? teams.GetValueOrDefault(teamId.Value, string.Empty) : string.Empty,
                    project, meta.Item1, meta.Item2, clientName, m.Build(), comparison, m.BuildStatus());
            })
            .OrderBy(r => r.EmployeeName).ThenBy(r => r.ClientName).ThenBy(r => r.ProjectName)
            .ToList();

        return Result<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>.Success(
            BuildReport(filter, prev?.PrevKey, viewLevel, diag, rows));
    }

    public async Task<Result<ProjectFirstExecutionReport<ProjectFirstByPodRow>>> AggregateByPodAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ProjectFirstExecutionReport<ProjectFirstByPodRow>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (raw, diag) = await ScanEntriesAsync(filter, scope, ct);
        var (entries, _, _, _) = await ResolveAndFilterAsync(raw, filter, diag, ct);
        var prev = await LoadPreviousEntriesAsync(filter, scope, ct);

        var accum = new Dictionary<Guid, MetricsAccum>(); // مفتاح Guid.Empty = بلا فريق.
        var podProjects = new Dictionary<Guid, HashSet<Guid>>();
        var podEmployees = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in entries)
        {
            var key = e.TeamId ?? Guid.Empty;
            if (!accum.TryGetValue(key, out var a)) accum[key] = a = new MetricsAccum();
            a.Add(e);
            if (!podProjects.TryGetValue(key, out var ps)) podProjects[key] = ps = new HashSet<Guid>();
            ps.Add(e.ProjectId);
            if (!podEmployees.TryGetValue(key, out var es)) podEmployees[key] = es = new HashSet<Guid>();
            es.Add(e.SubmitterId);
        }

        Dictionary<Guid, decimal>? prevHeadline = null;
        if (prev is not null)
        {
            prevHeadline = new Dictionary<Guid, decimal>();
            foreach (var e in prev.Value.Entries)
            {
                var k = e.TeamId ?? Guid.Empty;
                prevHeadline[k] = prevHeadline.GetValueOrDefault(k) + e.Completed + e.Responses;
            }
        }

        var teamIds = accum.Keys.Where(k => k != Guid.Empty).ToList();
        var teams = await TeamNamesAsync(teamIds, ct);

        var rows = accum
            .Select(kv =>
            {
                Guid? teamId = kv.Key == Guid.Empty ? null : kv.Key;
                var teamName = teamId is not null ? teams.GetValueOrDefault(teamId.Value, string.Empty) : string.Empty;
                var comparison = prevHeadline is null ? null
                    : BuildComparison(kv.Value.Headline, prevHeadline.TryGetValue(kv.Key, out var pv) ? pv : null);
                return new ProjectFirstByPodRow(teamId, teamName,
                    podProjects.GetValueOrDefault(kv.Key)?.Count ?? 0,
                    podEmployees.GetValueOrDefault(kv.Key)?.Count ?? 0,
                    kv.Value.Build(), comparison, kv.Value.BuildStatus());
            })
            .OrderBy(r => r.TeamName)
            .ToList();

        return Result<ProjectFirstExecutionReport<ProjectFirstByPodRow>>.Success(
            BuildReport(filter, prev?.PrevKey, viewLevel, diag, rows));
    }

    public async Task<Result<ProjectFirstExecutionReport<ProjectFirstByClientRow>>> AggregateByClientAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ProjectFirstExecutionReport<ProjectFirstByClientRow>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scope = await _scope.ResolveAsync(ct);
        var (raw, diag) = await ScanEntriesAsync(filter, scope, ct);
        var (entries, projects, clients, activeProjectIds) = await ResolveAndFilterAsync(raw, filter, diag, ct);
        var prev = await LoadPreviousEntriesAsync(filter, scope, ct);

        var accum = new Dictionary<Guid, MetricsAccum>(); // مفتاح Guid.Empty = بلا عميل (مشروع بلا عميل نادر).
        var clientProjects = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in entries)
        {
            var clientId = projects.TryGetValue(e.ProjectId, out var p) && p.Item2 is Guid cid ? cid : Guid.Empty;
            if (!accum.TryGetValue(clientId, out var a)) accum[clientId] = a = new MetricsAccum();
            a.Add(e);
            if (!clientProjects.TryGetValue(clientId, out var ps)) clientProjects[clientId] = ps = new HashSet<Guid>();
            ps.Add(e.ProjectId);
        }

        Dictionary<Guid, decimal>? prevHeadline = null;
        if (prev is not null)
        {
            prevHeadline = new Dictionary<Guid, decimal>();
            foreach (var e in prev.Value.Entries)
            {
                var clientId = projects.TryGetValue(e.ProjectId, out var p) && p.Item2 is Guid cid ? cid : Guid.Empty;
                prevHeadline[clientId] = prevHeadline.GetValueOrDefault(clientId) + e.Completed + e.Responses;
            }
        }

        var rows = accum
            .Select(kv =>
            {
                Guid? clientId = kv.Key == Guid.Empty ? null : kv.Key;
                var clientName = clientId is not null ? clients.GetValueOrDefault(clientId.Value, string.Empty) : string.Empty;
                var projectSet = clientProjects.GetValueOrDefault(kv.Key) ?? new HashSet<Guid>();
                var activeCount = projectSet.Count(activeProjectIds.Contains);
                var comparison = prevHeadline is null ? null
                    : BuildComparison(kv.Value.Headline, prevHeadline.TryGetValue(kv.Key, out var pv) ? pv : null);
                return new ProjectFirstByClientRow(clientId, clientName, projectSet.Count, activeCount,
                    kv.Value.Build(), comparison, kv.Value.BuildStatus());
            })
            .OrderBy(r => r.ClientName)
            .ToList();

        return Result<ProjectFirstExecutionReport<ProjectFirstByClientRow>>.Success(
            BuildReport(filter, prev?.PrevKey, viewLevel, diag, rows));
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
}

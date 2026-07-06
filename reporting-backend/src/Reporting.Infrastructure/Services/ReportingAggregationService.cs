using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// محرّك التجميع الرقمي (ERDS Phase 4) — قراءة فقط.
/// يجمّع قوالب ERDS المُهيكَلة (B2C حسب الدورة، B2B حسب الخدمة) بقراءة جدول TableGrid المخزَّن
/// كـ string[][] في ValueJson، ومطابقة الأعمدة بالفهرس على أعمدة الـSchema. لا يمسّ أيّ بيانات.
/// النطاق محكوم بـ IScopeResolver (تصفية على SubmitterId عند !SeesAll). التقارير/الصفوف غير المطابقة تُتجاهَل بلا فشل.
/// </summary>
public class ReportingAggregationService : IReportingAggregationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    public ReportingAggregationService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    // نسبة مئوية آمنة القسمة (المقام صفر ⇒ 0)، مقرّبة لمنزلة واحدة.
    private static decimal Pct(decimal num, decimal den) => den > 0 ? Math.Round(num / den * 100m, 1) : 0m;

    // نسبة مباشرة آمنة القسمة (لكل ساعة)، مقرّبة لمنزلتين.
    private static decimal Per(decimal num, decimal den) => den > 0 ? Math.Round(num / den, 2) : 0m;

    // قراءة خلية رقمية بأمان من صفّ الجدول: خارج الحدود/فارغة/غير قابلة للتحويل ⇒ 0.
    private static decimal Num(string[] row, int idx)
    {
        if (idx < 0 || idx >= row.Length) return 0m;
        var cell = row[idx];
        if (string.IsNullOrWhiteSpace(cell)) return 0m;
        return decimal.TryParse(cell.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    // قراءة خلية نصّية بأمان (الدورة/الخدمة).
    private static string Text(string[] row, int idx)
        => (idx >= 0 && idx < row.Length ? row[idx] : null)?.Trim() ?? string.Empty;

    private string ViewLevel()
        => RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles)) switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary",
        };

    /// <summary>
    /// يترجم فلتر الفترة إلى نطاق تواريخ [from, to] حين تكون الفترة أسبوعية/شهرية/ربع سنوية ولها مفتاح صالح.
    /// الغرض: تقارير المبيعات (B2C/B2B) تُخزَّن دائمًا كـ«يومي» (ReportCadencePolicy)، فعند اختيار فترة أوسع
    /// نجمع كل التقارير اليومية الواقعة داخل حدود تلك الفترة بدل البحث عن تطابق نوع/مفتاح تامّ (الذي لا يُرجِع شيئًا).
    /// يعيد null للفترة اليومية/بلا مفتاح ⇒ يُبقى على المطابقة التامّة (توافق خلفي). Yearly/AdHoc ⇒ مطابقة تامّة أيضًا.
    /// </summary>
    private static (DateOnly From, DateOnly To)? PeriodDateRange(PeriodType? type, string? key)
    {
        if (type is null || string.IsNullOrWhiteSpace(key)) return null;
        key = key.Trim();
        switch (type)
        {
            case PeriodType.Weekly:
                if (!ReportCalendarPolicy.IsWeekKey(key)) return null;
                var wr = ReportCalendarPolicy.WeekRange(key); // (خميس البداية، أربعاء النهاية)
                return (wr.Start, wr.End);
            case PeriodType.Monthly:
                var mp = key.Split('-'); // YYYY-MM
                if (mp.Length == 2
                    && int.TryParse(mp[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var my)
                    && int.TryParse(mp[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm)
                    && mm is >= 1 and <= 12)
                    return ReportCalendarPolicy.MonthRange(my, mm);
                return null;
            case PeriodType.Quarterly:
                var qp = key.Split("-Q"); // YYYY-Qn
                if (qp.Length == 2
                    && int.TryParse(qp[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var qy)
                    && int.TryParse(qp[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var qq)
                    && qq is >= 1 and <= 4)
                    return ReportCalendarPolicy.QuarterRange(qy, qq);
                return null;
            default:
                return null; // Daily / Yearly / AdHoc ⇒ مطابقة تامّة
        }
    }

    /// <summary>
    /// المسار المشترك: إيجاد قالب ERDS بالعنوان، جلب حقل الجدول، ثم التسليمات (غير المسودّات) ضمن النطاق/الفلاتر،
    /// وأخيرًا قراءة قيم الجدول (string[][]) لكل تسليم مع عدّ المتجاهَل بأمان.
    /// </summary>
    private async Task<GridScan?> ScanGridAsync(string templateTitle, string mainTableLabel,
        AggregationFilter filter, CancellationToken ct)
    {
        var scope = await _scope.ResolveAsync(ct);

        // قوالب ERDS تُبذَر عبر TemplateSeeder دائمًا؛ إن غاب القالب (بيئة غير مبذورة) ⇒ لا نتائج.
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

        // ترجمة الفترة: أسبوعي/شهري/ربع سنوي بمفتاح صالح ⇒ اجمع التقارير اليومية داخل نطاق الفترة؛
        // وإلا (يومي/سنوي/AdHoc/بلا مفتاح) ⇒ مطابقة تامّة على النوع/المفتاح كما كان (توافق خلفي).
        var range = PeriodDateRange(filter.PeriodType, filter.PeriodKey);
        if (range is not null)
        {
            // الترتيب المعجمي لصيغة YYYY-MM-DD مطابق للترتيب الزمني ⇒ مقارنة نصّية قابلة للترجمة في EF.
            var fromKey = range.Value.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var toKey = range.Value.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            subsQ = subsQ.Where(s => s.PeriodType == PeriodType.Daily
                && s.PeriodKey != null
                && string.Compare(s.PeriodKey, fromKey) >= 0
                && string.Compare(s.PeriodKey, toKey) <= 0);
        }
        else
        {
            if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
            if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        }
        if (filter.EmployeeId is not null) subsQ = subsQ.Where(s => s.SubmitterId == filter.EmployeeId);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (filter.DepartmentId is not null) subsQ = subsQ.Where(s => s.DepartmentId == filter.DepartmentId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ
            .Select(s => new { s.Id, s.SubmitterId, s.TeamId, s.DepartmentId, s.PeriodKey })
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

        // في وضع النطاق تُدرَج كل الصفوف اليومية تحت مفتاح الفترة المطلوب (تجميع تحت أسبوع/شهر/ربع)؛
        // وإلا يبقى مفتاح كل تسليم الأصلي (السلوك اليومي/التامّ).
        var bucketOverride = range is not null ? filter.PeriodKey?.Trim() : null;

        foreach (var g in valuesBySub)
        {
            var meta = subMeta[g.Key];
            var periodForRow = bucketOverride ?? (meta.PeriodKey ?? string.Empty);
            var anyRow = false;
            foreach (var val in g)
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
                    scan.RawRows.Add(new GridRow(meta.SubmitterId, meta.TeamId, meta.DepartmentId, periodForRow, row));
                    anyRow = true;
                }
            }
            if (anyRow) seenSubs.Add(g.Key);
        }

        // تسليمات لها قيمة جدول لكن بلا أيّ صفّ صالح، أو بلا قيمة جدول أصلًا ⇒ تُعدّ متجاهَلة.
        scan.SubmissionsIgnored = subs.Count - seenSubs.Count;
        return scan;
    }

    public async Task<Result<B2cAggregationReport>> AggregateB2cByCourseAsync(AggregationFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<B2cAggregationReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scan = await ScanGridAsync(B2cByCourseReportSchema.TemplateTitle, B2cByCourseReportSchema.MainTableLabel, filter, ct);
        if (scan is null)
            return Result<B2cAggregationReport>.Success(new B2cAggregationReport(filter.PeriodKey, 0, 0, 0, 0, viewLevel, new List<B2cCourseAggregateRow>()));

        var cols = B2cByCourseReportSchema.Columns;
        int I(string c) => Array.IndexOf(cols, c);
        var iCourse = I(B2cByCourseReportSchema.ColCourse);
        var iWork = I(B2cByCourseReportSchema.ColWorkHours);
        var iLeads = I(B2cByCourseReportSchema.ColLeads);
        var iContacted = I(B2cByCourseReportSchema.ColContacted);
        var iQualified = I(B2cByCourseReportSchema.ColQualified);
        var iFollow = I(B2cByCourseReportSchema.ColFollowUps);
        var iSales = I(B2cByCourseReportSchema.ColSales);
        var iRevenue = I(B2cByCourseReportSchema.ColRevenue);
        var iLost = I(B2cByCourseReportSchema.ColLost);

        var itemFilter = string.IsNullOrWhiteSpace(filter.Item) ? null : filter.Item.Trim();
        var rowsIgnored = 0;
        var accum = new Dictionary<(string Period, Guid Emp, string Course), B2cAccum>();

        foreach (var r in scan.RawRows)
        {
            var course = Text(r.Cells, iCourse);
            if (course.Length == 0) { rowsIgnored++; continue; } // صفّ بلا دورة ⇒ لا يمكن تجميعه.
            if (itemFilter is not null && !string.Equals(course, itemFilter, StringComparison.OrdinalIgnoreCase)) continue;

            var key = (r.PeriodKey, r.SubmitterId, course);
            if (!accum.TryGetValue(key, out var a))
            {
                a = new B2cAccum { TeamId = r.TeamId, DepartmentId = r.DepartmentId };
                accum[key] = a;
            }
            a.WorkHours += Num(r.Cells, iWork);
            a.Leads += Num(r.Cells, iLeads);
            a.Contacted += Num(r.Cells, iContacted);
            a.Qualified += Num(r.Cells, iQualified);
            a.FollowUps += Num(r.Cells, iFollow);
            a.Sales += Num(r.Cells, iSales);
            a.Revenue += Num(r.Cells, iRevenue);
            a.Lost += Num(r.Cells, iLost);
        }

        var empIds = accum.Keys.Select(k => k.Emp).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);

        var rows = accum
            .Select(kv =>
            {
                var (period, emp, course) = kv.Key;
                var a = kv.Value;
                return new B2cCourseAggregateRow(
                    period, emp, names.GetValueOrDefault(emp, string.Empty), course, a.TeamId, a.DepartmentId,
                    a.WorkHours, a.Leads, a.Contacted, a.Qualified, a.FollowUps, a.Sales, a.Revenue, a.Lost,
                    Pct(a.Sales, a.Leads),        // Conversion Rate
                    Pct(a.Qualified, a.Leads),    // Qualification Rate
                    Pct(a.Contacted, a.Leads),    // Contact Rate
                    Per(a.Revenue, a.WorkHours),  // Revenue per Hour
                    Per(a.Sales, a.WorkHours),    // Sales per Hour
                    Pct(a.Lost, a.Leads));        // Lost Rate
            })
            .OrderBy(r => r.PeriodKey).ThenBy(r => r.EmployeeName).ThenBy(r => r.Course)
            .ToList();

        return Result<B2cAggregationReport>.Success(new B2cAggregationReport(
            filter.PeriodKey, rows.Count, scan.SubmissionsConsidered, scan.SubmissionsIgnored, rowsIgnored, viewLevel, rows));
    }

    public async Task<Result<B2cCourseGroupedReport>> AggregateB2cByCourseGroupedAsync(AggregationFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<B2cCourseGroupedReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var itemFilter = string.IsNullOrWhiteSpace(filter.Item) ? null : filter.Item.Trim();

        // Phase 7.1 — استعادة drill-down مع فصل الدلاء لكل (دورة، موظّف): نقرأ المصادر الثلاثة.
        // New = القالب القديم أحادي الجدول (Legacy) + جدول «New Leads» من القالب الجديد.
        // Old = جدول «Old CRM Data» من القالب الجديد. Total = New + Old (يُجمَع عند البناء).
        var newPerEmp = new Dictionary<(string Course, Guid Emp), B2cAccum>();
        var oldPerEmp = new Dictionary<(string Course, Guid Emp), B2cAccum>();
        var display = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // أوّل تهجئة ظهرت
        var considered = 0;
        var ignored = 0;
        var rowsIgnored = 0;

        // (1) القالب القديم أحادي الجدول (Legacy) ⇒ دلو New (Leads=Leads، Qualified=Qualified).
        var scanLegacy = await ScanGridAsync(B2cByCourseReportSchema.TemplateTitle, B2cByCourseReportSchema.MainTableLabel, filter, ct);
        if (scanLegacy is not null)
        {
            considered += scanLegacy.SubmissionsConsidered;
            ignored += scanLegacy.SubmissionsIgnored;
            AccumBucketPerEmp(scanLegacy, B2cByCourseReportSchema.Columns, B2cByCourseReportSchema.ColCourse,
                B2cByCourseReportSchema.ColLeads, B2cByCourseReportSchema.ColQualified, newPerEmp, display, itemFilter, ref rowsIgnored);
        }

        // (2) جدول «أداء البيانات الجديدة New Leads» من القالب الجديد ⇒ دلو New (Leads=New Leads، Qualified=Qualified).
        var scanNew = await ScanGridAsync(B2cNewOldReportSchema.TemplateTitle, B2cNewOldReportSchema.NewLeadsTableLabel, filter, ct);
        if (scanNew is not null)
        {
            considered += scanNew.SubmissionsConsidered;
            ignored += scanNew.SubmissionsIgnored;
            AccumBucketPerEmp(scanNew, B2cNewOldReportSchema.NewLeadsColumns, B2cNewOldReportSchema.ColCourse,
                B2cNewOldReportSchema.ColNewLeads, B2cNewOldReportSchema.ColQualified, newPerEmp, display, itemFilter, ref rowsIgnored);
        }

        // (3) جدول «أداء بيانات CRM القديمة Old CRM Data» ⇒ دلو Old (Leads=Old Leads Worked، Qualified=Requalified).
        var scanOld = await ScanGridAsync(B2cNewOldReportSchema.TemplateTitle, B2cNewOldReportSchema.OldCrmTableLabel, filter, ct);
        if (scanOld is not null)
        {
            considered += scanOld.SubmissionsConsidered;
            ignored += scanOld.SubmissionsIgnored;
            AccumBucketPerEmp(scanOld, B2cNewOldReportSchema.OldCrmColumns, B2cNewOldReportSchema.ColCourse,
                B2cNewOldReportSchema.ColOldLeadsWorked, B2cNewOldReportSchema.ColRequalified, oldPerEmp, display, itemFilter, ref rowsIgnored);
        }

        // مفاتيح (دورة، موظّف) عبر الدلوين (قد يظهر موظّف في أحدهما فقط).
        var allKeys = new HashSet<(string Course, Guid Emp)>(newPerEmp.Keys);
        allKeys.UnionWith(oldPerEmp.Keys);

        var empIds = allKeys.Select(k => k.Emp).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);

        // الدورة موحّدة التهجئة مسبقًا عبر display ⇒ التجميع بالتطابق التام آمن.
        var courses = allKeys
            .GroupBy(k => k.Course)
            .Select(cg =>
            {
                var courseDisplay = display.GetValueOrDefault(cg.Key, cg.Key);
                var employees = cg
                    .Select(k =>
                    {
                        var n = newPerEmp.GetValueOrDefault(k);
                        var o = oldPerEmp.GetValueOrDefault(k);
                        var teamId = n?.TeamId ?? o?.TeamId;
                        var deptId = n?.DepartmentId ?? o?.DepartmentId;
                        var newBucket = BucketOf(n);
                        var oldBucket = BucketOf(o);
                        // Total = New + Old (مجموع مساهمتَي الدلوين لهذا الموظّف في هذه الدورة).
                        var tWork = newBucket.WorkHours + oldBucket.WorkHours;
                        var tLeads = newBucket.Leads + oldBucket.Leads;
                        var tContacted = newBucket.Contacted + oldBucket.Contacted;
                        var tQualified = newBucket.Qualified + oldBucket.Qualified;
                        var tFollow = newBucket.FollowUps + oldBucket.FollowUps;
                        var tSales = newBucket.Sales + oldBucket.Sales;
                        var tRevenue = newBucket.Revenue + oldBucket.Revenue;
                        var tLost = newBucket.Lost + oldBucket.Lost;
                        return new B2cCourseEmployeeRow(
                            k.Emp, names.GetValueOrDefault(k.Emp, string.Empty), teamId, deptId,
                            tWork, tLeads, tContacted, tQualified, tFollow, tSales, tRevenue, tLost,
                            Pct(tSales, tLeads), newBucket, oldBucket);
                    })
                    .OrderByDescending(e => e.Revenue).ThenBy(e => e.EmployeeName)
                    .ToList();

                var tWorkC = employees.Sum(e => e.WorkHours);
                var tLeadsC = employees.Sum(e => e.Leads);
                var tContactedC = employees.Sum(e => e.Contacted);
                var tQualifiedC = employees.Sum(e => e.Qualified);
                var tFollowC = employees.Sum(e => e.FollowUps);
                var tSalesC = employees.Sum(e => e.Sales);
                var tRevenueC = employees.Sum(e => e.Revenue);
                var tLostC = employees.Sum(e => e.Lost);

                return new B2cCourseGroupRow(
                    courseDisplay, tWorkC, tLeadsC, tContactedC, tQualifiedC, tFollowC, tSalesC, tRevenueC, tLostC,
                    Pct(tSalesC, tLeadsC),         // Conversion Rate
                    Pct(tQualifiedC, tLeadsC),     // Qualification Rate
                    Pct(tContactedC, tLeadsC),     // Contact Rate
                    Per(tRevenueC, tWorkC),        // Revenue per Hour
                    Per(tSalesC, tWorkC),          // Sales per Hour
                    Pct(tLostC, tLeadsC),          // Lost Rate
                    employees.Count, employees);
            })
            .OrderByDescending(c => c.Revenue).ThenBy(c => c.Course)
            .ToList();

        return Result<B2cCourseGroupedReport>.Success(new B2cCourseGroupedReport(
            filter.PeriodKey, courses.Count, considered, ignored, rowsIgnored, viewLevel, courses));
    }

    // يصهر صفوف جدول واحد داخل قاموس دلو لكل (دورة، موظّف). leadsCol/qualifiedCol تختلف حسب الجدول.
    // توحيد تهجئة الدورة عبر display (case-insensitive) يضمن دمج الدلاء بمفتاح واحد عبر المصادر الثلاثة.
    private static void AccumBucketPerEmp(GridScan scan, string[] cols, string courseCol, string leadsCol, string qualifiedCol,
        Dictionary<(string Course, Guid Emp), B2cAccum> bucket, Dictionary<string, string> display, string? itemFilter, ref int rowsIgnored)
    {
        int I(string c) => Array.IndexOf(cols, c);
        var iCourse = I(courseCol);
        var iWork = I(B2cNewOldReportSchema.ColWorkHours);
        var iLeads = I(leadsCol);
        var iContacted = I(B2cNewOldReportSchema.ColContacted);
        var iQualified = I(qualifiedCol);
        var iFollow = I(B2cNewOldReportSchema.ColFollowUps);
        var iSales = I(B2cNewOldReportSchema.ColSales);
        var iRevenue = I(B2cNewOldReportSchema.ColRevenue);
        var iLost = I(B2cNewOldReportSchema.ColLost);

        foreach (var r in scan.RawRows)
        {
            var course = Text(r.Cells, iCourse);
            if (course.Length == 0) { rowsIgnored++; continue; } // صفّ بلا دورة ⇒ لا يمكن تجميعه.
            if (itemFilter is not null && !string.Equals(course, itemFilter, StringComparison.OrdinalIgnoreCase)) continue;

            if (!display.TryGetValue(course, out var canon)) { canon = course; display[course] = course; }
            var key = (canon, r.SubmitterId);
            if (!bucket.TryGetValue(key, out var a))
            {
                a = new B2cAccum { TeamId = r.TeamId, DepartmentId = r.DepartmentId };
                bucket[key] = a;
            }
            a.WorkHours += Num(r.Cells, iWork);
            a.Leads += Num(r.Cells, iLeads);
            a.Contacted += Num(r.Cells, iContacted);
            a.Qualified += Num(r.Cells, iQualified);
            a.FollowUps += Num(r.Cells, iFollow);
            a.Sales += Num(r.Cells, iSales);
            a.Revenue += Num(r.Cells, iRevenue);
            a.Lost += Num(r.Cells, iLost);
        }
    }

    public async Task<Result<B2bAggregationReport>> AggregateB2bByServiceAsync(AggregationFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<B2bAggregationReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var scan = await ScanGridAsync(B2bByServiceReportSchema.TemplateTitle, B2bByServiceReportSchema.MainTableLabel, filter, ct);
        if (scan is null)
            return Result<B2bAggregationReport>.Success(new B2bAggregationReport(filter.PeriodKey, 0, 0, 0, 0, viewLevel, new List<B2bServiceAggregateRow>()));

        var cols = B2bByServiceReportSchema.Columns;
        int I(string c) => Array.IndexOf(cols, c);
        var iService = I(B2bByServiceReportSchema.ColService);
        var iWork = I(B2bByServiceReportSchema.ColWorkHours);
        var iLeads = I(B2bByServiceReportSchema.ColLeads);
        var iMeetings = I(B2bByServiceReportSchema.ColMeetings);
        var iProposals = I(B2bByServiceReportSchema.ColProposals);
        var iNegotiation = I(B2bByServiceReportSchema.ColNegotiation);
        var iWon = I(B2bByServiceReportSchema.ColWon);
        var iLost = I(B2bByServiceReportSchema.ColLost);
        var iRevenue = I(B2bByServiceReportSchema.ColRevenue);

        var itemFilter = string.IsNullOrWhiteSpace(filter.Item) ? null : filter.Item.Trim();
        var rowsIgnored = 0;
        var accum = new Dictionary<(string Period, Guid Emp, string Service), B2bAccum>();

        foreach (var r in scan.RawRows)
        {
            var service = Text(r.Cells, iService);
            if (service.Length == 0) { rowsIgnored++; continue; }
            if (itemFilter is not null && !string.Equals(service, itemFilter, StringComparison.OrdinalIgnoreCase)) continue;

            var key = (r.PeriodKey, r.SubmitterId, service);
            if (!accum.TryGetValue(key, out var a))
            {
                a = new B2bAccum { TeamId = r.TeamId, DepartmentId = r.DepartmentId };
                accum[key] = a;
            }
            a.WorkHours += Num(r.Cells, iWork);
            a.Leads += Num(r.Cells, iLeads);
            a.Meetings += Num(r.Cells, iMeetings);
            a.Proposals += Num(r.Cells, iProposals);
            a.Negotiation += Num(r.Cells, iNegotiation);
            a.Won += Num(r.Cells, iWon);
            a.Lost += Num(r.Cells, iLost);
            a.Revenue += Num(r.Cells, iRevenue);
        }

        var empIds = accum.Keys.Select(k => k.Emp).Distinct().ToList();
        var names = await UserNamesAsync(empIds, ct);

        var rows = accum
            .Select(kv =>
            {
                var (period, emp, service) = kv.Key;
                var a = kv.Value;
                return new B2bServiceAggregateRow(
                    period, emp, names.GetValueOrDefault(emp, string.Empty), service, a.TeamId, a.DepartmentId,
                    a.WorkHours, a.Leads, a.Meetings, a.Proposals, a.Negotiation, a.Won, a.Lost, a.Revenue,
                    Pct(a.Meetings, a.Leads),     // Meeting Rate
                    Pct(a.Proposals, a.Meetings), // Proposal Rate
                    Pct(a.Won, a.Proposals),      // Win Rate
                    Per(a.Revenue, a.WorkHours),  // Revenue per Hour
                    Per(a.Won, a.WorkHours),      // Won per Hour
                    Pct(a.Lost, a.Leads));        // Lost Rate
            })
            .OrderBy(r => r.PeriodKey).ThenBy(r => r.EmployeeName).ThenBy(r => r.Service)
            .ToList();

        return Result<B2bAggregationReport>.Success(new B2bAggregationReport(
            filter.PeriodKey, rows.Count, scan.SubmissionsConsidered, scan.SubmissionsIgnored, rowsIgnored, viewLevel, rows));
    }

    public async Task<Result<B2cNewOldReport>> AggregateB2cNewOldAsync(AggregationFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<B2cNewOldReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var viewLevel = ViewLevel();
        var itemFilter = string.IsNullOrWhiteSpace(filter.Item) ? null : filter.Item.Trim();

        // دلوان لكل دورة (case-insensitive) + إجماليان. نصهر المصادر الثلاثة في نفس القاموسين.
        var newByCourse = new Dictionary<string, B2cAccum>(StringComparer.OrdinalIgnoreCase);
        var oldByCourse = new Dictionary<string, B2cAccum>(StringComparer.OrdinalIgnoreCase);
        var display = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // أوّل تهجئة ظهرت
        var considered = 0;
        var ignored = 0;
        var rowsIgnored = 0;

        // (1) القالب القديم أحادي الجدول (Legacy) ⇒ دلو New (Leads=Leads، Qualified=Qualified).
        var legacyCols = B2cByCourseReportSchema.Columns;
        var scanLegacy = await ScanGridAsync(B2cByCourseReportSchema.TemplateTitle, B2cByCourseReportSchema.MainTableLabel, filter, ct);
        if (scanLegacy is not null)
        {
            considered += scanLegacy.SubmissionsConsidered;
            ignored += scanLegacy.SubmissionsIgnored;
            AccumBucket(scanLegacy, legacyCols, B2cByCourseReportSchema.ColCourse, B2cByCourseReportSchema.ColLeads,
                B2cByCourseReportSchema.ColQualified, newByCourse, display, itemFilter, ref rowsIgnored);
        }

        // (2) جدول «أداء البيانات الجديدة New Leads» من القالب الجديد ⇒ دلو New (Leads=New Leads، Qualified=Qualified).
        var newCols = B2cNewOldReportSchema.NewLeadsColumns;
        var scanNew = await ScanGridAsync(B2cNewOldReportSchema.TemplateTitle, B2cNewOldReportSchema.NewLeadsTableLabel, filter, ct);
        if (scanNew is not null)
        {
            considered += scanNew.SubmissionsConsidered;
            ignored += scanNew.SubmissionsIgnored;
            AccumBucket(scanNew, newCols, B2cNewOldReportSchema.ColCourse, B2cNewOldReportSchema.ColNewLeads,
                B2cNewOldReportSchema.ColQualified, newByCourse, display, itemFilter, ref rowsIgnored);
        }

        // (3) جدول «أداء بيانات CRM القديمة Old CRM Data» ⇒ دلو Old (Leads=Old Leads Worked، Qualified=Requalified).
        var oldCols = B2cNewOldReportSchema.OldCrmColumns;
        var scanOld = await ScanGridAsync(B2cNewOldReportSchema.TemplateTitle, B2cNewOldReportSchema.OldCrmTableLabel, filter, ct);
        if (scanOld is not null)
        {
            considered += scanOld.SubmissionsConsidered;
            ignored += scanOld.SubmissionsIgnored;
            AccumBucket(scanOld, oldCols, B2cNewOldReportSchema.ColCourse, B2cNewOldReportSchema.ColOldLeadsWorked,
                B2cNewOldReportSchema.ColRequalified, oldByCourse, display, itemFilter, ref rowsIgnored);
        }

        // دمج مفاتيح الدورات من الدلوين لبناء صفوف مزدوجة (دورة قد تظهر في أحدهما فقط).
        var allCourses = new HashSet<string>(newByCourse.Keys, StringComparer.OrdinalIgnoreCase);
        allCourses.UnionWith(oldByCourse.Keys);

        var courses = allCourses
            .Select(c => new B2cNewOldCourseRow(
                display.GetValueOrDefault(c, c),
                BucketOf(newByCourse.GetValueOrDefault(c)),
                BucketOf(oldByCourse.GetValueOrDefault(c))))
            .OrderByDescending(c => c.New.Revenue + c.Old.Revenue).ThenBy(c => c.Course)
            .ToList();

        var newTotals = AggregateBuckets(newByCourse.Values);
        var oldTotals = AggregateBuckets(oldByCourse.Values);

        return Result<B2cNewOldReport>.Success(new B2cNewOldReport(
            filter.PeriodKey, courses.Count, considered, ignored, rowsIgnored, viewLevel, newTotals, oldTotals, courses));
    }

    // يصهر صفوف جدول واحد داخل قاموس دلو (بمفتاح الدورة). leadsCol/qualifiedCol تختلف حسب الجدول.
    private static void AccumBucket(GridScan scan, string[] cols, string courseCol, string leadsCol, string qualifiedCol,
        Dictionary<string, B2cAccum> bucket, Dictionary<string, string> display, string? itemFilter, ref int rowsIgnored)
    {
        int I(string c) => Array.IndexOf(cols, c);
        var iCourse = I(courseCol);
        var iWork = I(B2cNewOldReportSchema.ColWorkHours);
        var iLeads = I(leadsCol);
        var iContacted = I(B2cNewOldReportSchema.ColContacted);
        var iQualified = I(qualifiedCol);
        var iFollow = I(B2cNewOldReportSchema.ColFollowUps);
        var iSales = I(B2cNewOldReportSchema.ColSales);
        var iRevenue = I(B2cNewOldReportSchema.ColRevenue);
        var iLost = I(B2cNewOldReportSchema.ColLost);

        foreach (var r in scan.RawRows)
        {
            var course = Text(r.Cells, iCourse);
            if (course.Length == 0) { rowsIgnored++; continue; }
            if (itemFilter is not null && !string.Equals(course, itemFilter, StringComparison.OrdinalIgnoreCase)) continue;

            if (!display.ContainsKey(course)) display[course] = course;
            if (!bucket.TryGetValue(course, out var a))
            {
                a = new B2cAccum { TeamId = r.TeamId, DepartmentId = r.DepartmentId };
                bucket[course] = a;
            }
            a.WorkHours += Num(r.Cells, iWork);
            a.Leads += Num(r.Cells, iLeads);
            a.Contacted += Num(r.Cells, iContacted);
            a.Qualified += Num(r.Cells, iQualified);
            a.FollowUps += Num(r.Cells, iFollow);
            a.Sales += Num(r.Cells, iSales);
            a.Revenue += Num(r.Cells, iRevenue);
            a.Lost += Num(r.Cells, iLost);
        }
    }

    // يحوّل مُجمِّعًا (أو null) إلى حزمة مؤشرات دلو. null ⇒ حزمة أصفار.
    private static B2cNewOldBucket BucketOf(B2cAccum? a)
    {
        if (a is null) return new B2cNewOldBucket(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new B2cNewOldBucket(
            a.WorkHours, a.Leads, a.Contacted, a.Qualified, a.FollowUps, a.Sales, a.Revenue, a.Lost,
            Pct(a.Sales, a.Leads),        // Conversion Rate (New) / Recovery Rate (Old)
            Pct(a.Qualified, a.Leads),    // Qualification / Requalification Rate
            Pct(a.Contacted, a.Leads),    // Contact Rate
            Per(a.Revenue, a.WorkHours),  // Revenue per Hour
            Per(a.Sales, a.WorkHours),    // Sales per Hour
            Pct(a.Lost, a.Leads));        // Lost Rate
    }

    // يجمع كل مُجمِّعات الدلو إلى حزمة إجمالية واحدة.
    private static B2cNewOldBucket AggregateBuckets(IEnumerable<B2cAccum> accums)
    {
        var t = new B2cAccum();
        foreach (var a in accums)
        {
            t.WorkHours += a.WorkHours;
            t.Leads += a.Leads;
            t.Contacted += a.Contacted;
            t.Qualified += a.Qualified;
            t.FollowUps += a.FollowUps;
            t.Sales += a.Sales;
            t.Revenue += a.Revenue;
            t.Lost += a.Lost;
        }
        return BucketOf(t);
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private sealed class GridScan
    {
        public int SubmissionsConsidered { get; set; }
        public int SubmissionsIgnored { get; set; }
        public List<GridRow> RawRows { get; } = new();
    }

    private readonly record struct GridRow(Guid SubmitterId, Guid? TeamId, Guid? DepartmentId, string PeriodKey, string[] Cells);

    private sealed class B2cAccum
    {
        public Guid? TeamId { get; set; }
        public Guid? DepartmentId { get; set; }
        public decimal WorkHours, Leads, Contacted, Qualified, FollowUps, Sales, Revenue, Lost;
    }

    private sealed class B2bAccum
    {
        public Guid? TeamId { get; set; }
        public Guid? DepartmentId { get; set; }
        public decimal WorkHours, Leads, Meetings, Proposals, Negotiation, Won, Lost, Revenue;
    }
}

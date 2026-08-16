using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RPT-DUE-LATE-COMPLIANCE-R1 — تعريف «متأخر» الموحّد + نقاط النهاية الجديدة:
/// compliance-summary / compliance-trend / late-by-template / compliance-breakdown.
/// متأخر = (سلّم بعد موعد دوره) أو (لم يسلّم وانقضى موعد الدور). Compliance%=Submitted/Expected؛ OnTime%=OnTime/Expected.
/// Draft لا يُحتسب تسليمًا. الأسبوع التشغيلي (الخميس→الأربعاء) كما هو. التوقيت = الرياض (UTC+3).
/// Archived/غير الفعّال و Monthly مستبعَدة من المتوقَّع؛ <b>اليومي (مبيعات SALES_B2C/SALES_B2B) مشمول</b> بوحدة
/// لكلّ يوم عمل (تُستبعَد الجمعة/السبت). الاستبعاد (Exclude) لا يُغيّر «المتوقَّع» (الإسناد عام بالمسمّى).
/// </summary>
[Collection("Integration")]
public class ComplianceDueLateTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ComplianceDueLateTests(CustomWebApplicationFactory factory) => _factory = factory;

    // أسبوع تشغيلي منقضٍ بالكامل (قبل ~3 أسابيع) ⇒ موعد دور الموظّف (الأربعاء) مضى ⇒ غير المُسلِّم = MissingOverdue.
    // مثبَّت في/بعد أرضيّة الإطلاق الأسبوعيّ 2026-07-04 (نافذة انتقاليّة قصيرة قرب الإطلاق) كي يبقى منطبقًا.
    private static string PastWeekKey()
    {
        var start = ReportCalendarPolicy.WeekRange(ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday().AddDays(-21))).Start;
        var floorStart = ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor;
        if (start < floorStart) start = floorStart;
        return ReportCalendarPolicy.WeekKeyFor(start);
    }

    // (1) سلّم داخل الأسبوع (≤ الأربعاء) ⇒ OnTime (لا متأخر).
    [Fact]
    public async Task SubmittedOnTime_CountsOnTime_NotLate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await CreateWeeklyRoleAsync(admin, "DLT1");
        var dept = await CreateDeptAsync();
        var emp = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var week = PastWeekKey();
        var employeeDue = ReportCalendarPolicy.WeekRange(week).Start.AddDays(4); // الأربعاء = موعد الموظّف (السبت + 4)
        await AddSubmissionAsync(versionId, emp, week, SubmissionStatus.Submitted, AtRiyadh(employeeDue)); // في الموعد

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(0, summary.Late);
        Assert.Equal(1, summary.OnTime);
        var row = await GetRowAsync(admin, week, emp);
        Assert.False(row.Late);
        Assert.True(row.Submitted);
    }

    // (2) سلّم بعد موعد الدور ⇒ Late + LateSubmitted، لكنّه يُحتسب ضمن Submitted (Compliance%).
    [Fact]
    public async Task SubmittedLate_IsLate_ButCountsAsSubmitted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await CreateWeeklyRoleAsync(admin, "DLT2");
        var dept = await CreateDeptAsync();
        var emp = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var week = PastWeekKey();
        var end = ReportCalendarPolicy.WeekRange(week).End;
        await AddSubmissionAsync(versionId, emp, week, SubmissionStatus.Submitted, AtRiyadh(end.AddDays(2))); // بعد الموعد

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(1, summary.Late);
        Assert.Equal(1, summary.LateSubmitted);
        Assert.Equal(1, summary.Submitted);
        var row = await GetRowAsync(admin, week, emp);
        Assert.True(row.Late);
        Assert.True(row.Submitted);
        Assert.True(row.LateSubmitted);
    }

    // (3) لم يسلّم وانقضى الموعد ⇒ Late + MissingOverdue + ليس Submitted.
    [Fact]
    public async Task MissingOverdue_IsLate_AndMissing()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "DLT3");
        var dept = await CreateDeptAsync();
        var emp = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var week = PastWeekKey();

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(1, summary.MissingOverdue);
        Assert.Equal(1, summary.Late);
        Assert.Equal(0, summary.Submitted);
        var row = await GetRowAsync(admin, week, emp);
        Assert.True(row.Late);
        Assert.False(row.Submitted);
    }

    // (4) Draft لا يُحتسب تسليمًا (يظلّ MissingOverdue في أسبوع منقضٍ).
    [Fact]
    public async Task Draft_DoesNotCountAsSubmitted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await CreateWeeklyRoleAsync(admin, "DLT4");
        var emp = await CreateWeeklyEmployeeAsync(roleId);
        var week = PastWeekKey();
        var end = ReportCalendarPolicy.WeekRange(week).End;
        await AddSubmissionAsync(versionId, emp, week, SubmissionStatus.Draft, AtRiyadh(end)); // مسودّة

        var row = await GetRowAsync(admin, week, emp);
        Assert.False(row.Submitted);
        Assert.True(row.Late); // غير مُسلَّم + منقضٍ ⇒ متأخر
    }

    // (5) summary: Expected/Submitted/Missing متّسقة + النسب صحيحة.
    [Fact]
    public async Task Summary_Aggregates_AreConsistent()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await CreateWeeklyRoleAsync(admin, "DLT5");
        var dept = await CreateDeptAsync();
        var onTime = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var missing = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var week = PastWeekKey();
        var employeeDue = ReportCalendarPolicy.WeekRange(week).Start.AddDays(4); // الأربعاء = موعد الموظّف
        await AddSubmissionAsync(versionId, onTime, week, SubmissionStatus.Submitted, AtRiyadh(employeeDue));

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(2, summary.Expected);
        Assert.Equal(1, summary.Submitted);
        Assert.Equal(1, summary.Missing);
        Assert.Equal(1, summary.OnTime);
        Assert.Equal(summary.Expected - summary.Submitted, summary.Missing);
        Assert.Equal(summary.LateSubmitted + summary.MissingOverdue, summary.Late);
        Assert.Equal(50m, summary.CompliancePercent);
        Assert.Equal(50m, summary.OnTimePercent);
        // الموظّفان موجودان: واحد في الموعد وواحد متأخر-غائب.
        var rows = await GetReportAsync(admin, week);
        Assert.Contains(rows.Rows, r => r.UserId == onTime && !r.Late);
        Assert.Contains(rows.Rows, r => r.UserId == missing && r.Late);
    }

    // (6) trend: يعيد نقاطًا مرتّبة (الأقدم→الأحدث) وعددها ضمن المطلوب، وآخر نقطة = الأسبوع الحالي.
    [Fact]
    public async Task Trend_ReturnsOrderedWeeks()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        await CreateWeeklyRoleAsync(admin, "DLT6");
        await CreateWeeklyEmployeeAsync((await CreateWeeklyRoleAsync(admin, "DLT6b")).RoleId);

        var res = await admin.GetAsync("/api/reports/compliance-trend?weeks=4");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var trend = await res.ReadAsync<ComplianceTrendReport>();
        Assert.NotNull(trend);
        Assert.True(trend!.Points.Count <= 4 && trend.Points.Count >= 1);
        var currentWeek = ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());
        Assert.Equal(currentWeek, trend.Points[^1].PeriodKey);
    }

    // (7) late-by-template: المسمّى المتأخّر يظهر بصفّ يحمل عنوان قالبه ونسبة تأخّر > 0.
    [Fact]
    public async Task LateByTemplate_ShowsDelayedRole()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "DLT7");
        var emp = await CreateWeeklyEmployeeAsync(roleId);
        var week = PastWeekKey();

        var res = await admin.GetAsync($"/api/reports/late-by-template?weekKey={week}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<LateByTemplateReport>();
        Assert.NotNull(report);
        Assert.Contains(report!.Rows, r => r.JobRoleId == roleId && r.Late >= 1 && r.LatePercent > 0);
    }

    // (8) compliance-breakdown حسب الإدارة: تظهر إدارة الموظّف بصفّ متوقَّع ≥ 1.
    [Fact]
    public async Task Breakdown_ByDepartment_GroupsCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "DLT8");
        var dept = await CreateDeptAsync();
        var emp = await CreateWeeklyEmployeeAsync(roleId, deptId: dept);
        var week = PastWeekKey();

        var res = await admin.GetAsync($"/api/reports/compliance-breakdown?weekKey={week}&groupBy=department");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<ComplianceBreakdownReport>();
        Assert.NotNull(report);
        Assert.Equal("department", report!.GroupBy);
        Assert.Contains(report.Rows, r => r.GroupId == dept && r.Expected >= 1);
    }

    // (9) قالب مؤرشف (غير فعّال) لا يجعل حامله متوقَّعًا (يُستبعَد من المتوقَّع).
    [Fact]
    public async Task ArchivedTemplate_HolderNotExpected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync("DLT9");
        // قالب أساسي أسبوعي ثم نؤرشفه ⇒ IsActive=false ⇒ المسمّى لا يصبح «متوقَّعًا».
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, roleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        await admin.PostAsync($"/api/report-templates/{created.Id}/archive", null);

        var emp = await CreateWeeklyEmployeeAsync(roleId);
        var week = PastWeekKey();
        var report = await GetReportAsync(admin, week);
        Assert.DoesNotContain(report.Rows, r => r.UserId == emp);
    }

    // (10) قالب شهري لا يجعل حامله متوقَّعًا أسبوعيًّا (Known Limitation: الشهري مستبعَد الآن).
    [Fact]
    public async Task MonthlyTemplate_HolderNotWeeklyExpected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync("DLT10");
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, roleId, PeriodType.Monthly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);

        var emp = await CreateWeeklyEmployeeAsync(roleId);
        var week = PastWeekKey();
        var report = await GetReportAsync(admin, week);
        Assert.DoesNotContain(report.Rows, r => r.UserId == emp);
    }

    // (11) الأسبوع التشغيلي سبت→جمعة: حدود مفتاح أسبوع منقضٍ تبدأ سبتًا وتنتهي جمعة.
    [Fact]
    public void WeeklyPeriod_SaturdayToFriday_Unchanged()
    {
        var week = PastWeekKey();
        var (start, end) = ReportCalendarPolicy.WeekRange(week);
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, end.DayOfWeek);
        Assert.Equal(6, end.DayNumber - start.DayNumber);
    }

    // (12) RBAC: الموظّف=403، غير المصادَق=401 على نقاط النهاية الأربع الجديدة.
    [Fact]
    public async Task NewEndpoints_Employee_403_Anonymous_401()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var anon = _factory.CreateClient();
        foreach (var path in new[]
        {
            "/api/reports/compliance-summary",
            "/api/reports/compliance-trend?weeks=4",
            "/api/reports/late-by-template",
            "/api/reports/compliance-breakdown"
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
        }
    }

    // (13) HR على مستوى الشركة في summary (استثناء موثّق): يرى موظّفًا خارج نطاقه الذاتي ضمن المتوقَّع.
    [Fact]
    public async Task Hr_CompanyWide_Summary_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "DLT13");
        await CreateWeeklyEmployeeAsync(roleId);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var res = await hr.GetAsync("/api/reports/compliance-summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var summary = await res.ReadAsync<ComplianceSummaryReport>();
        Assert.NotNull(summary);
        Assert.True(summary!.Expected >= 1);
    }

    // (14) Manager في breakdown لا يرى خارج نطاقه (النطاق ∩ التجميع).
    [Fact]
    public async Task Manager_Breakdown_ScopeRespected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "DLT14");
        var (mgr, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var inScope = await CreateWeeklyEmployeeAsync(roleId, managerId: mId);
        var (_, otherMgr) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var outScope = await CreateWeeklyEmployeeAsync(roleId, managerId: otherMgr);

        var report = await GetReportAsync(mgr, PastWeekKey());
        Assert.Contains(report.Rows, r => r.UserId == inScope);
        Assert.DoesNotContain(report.Rows, r => r.UserId == outScope);
    }

    // (15) مندوب يومي (SALES_B2C) سلّم كلّ أيام العمل في الموعد ⇒ متوقَّع 5، مُسلَّم 5، OnTime 5، صفّ غير متأخر.
    [Fact]
    public async Task DailySales_AllWorkingDays_FullCompliance()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await EnsureDailyRoleAsync(admin, "SALES_B2C");
        var dept = await CreateDeptAsync();
        var emp = await CreateDailyEmployeeAsync(roleId, dept);
        var week = PastWeekKey();
        var days = WorkingDays(week);
        foreach (var d in days)
            await AddDailySubmissionAsync(versionId, emp, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), SubmissionStatus.Submitted, AtRiyadh(d));

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(5, summary.Expected);        // الخميس + الأحد→الأربعاء (الجمعة/السبت مستبعدتان)
        Assert.Equal(5, summary.Submitted);
        Assert.Equal(5, summary.OnTime);
        Assert.Equal(0, summary.Late);

        var row = await GetRowAsync(admin, week, emp);
        Assert.True(row.Submitted);
        Assert.False(row.Late);
        Assert.Equal("سلّم 5 من 5 يوم", row.StatusLabel);
    }

    // (16) مندوب يومي سلّم 3 من 5 أيام في أسبوع منقضٍ ⇒ مُسلَّم 3، MissingOverdue 2، Late 2، صفّ متأخر غير مكتمل.
    [Fact]
    public async Task DailySales_PartialDays_LateAndMissingOverdue()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await EnsureDailyRoleAsync(admin, "SALES_B2C");
        var dept = await CreateDeptAsync();
        var emp = await CreateDailyEmployeeAsync(roleId, dept);
        var week = PastWeekKey();
        var days = WorkingDays(week);
        foreach (var d in days.Take(3))
            await AddDailySubmissionAsync(versionId, emp, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), SubmissionStatus.Submitted, AtRiyadh(d));

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(5, summary.Expected);
        Assert.Equal(3, summary.Submitted);
        Assert.Equal(2, summary.MissingOverdue);
        Assert.Equal(2, summary.Late);

        var row = await GetRowAsync(admin, week, emp);
        Assert.False(row.Submitted);              // لم يكمل كلّ الأيام المتوقَّعة
        Assert.True(row.Late);
        Assert.Equal("سلّم 3 من 5 يوم", row.StatusLabel);
    }

    // (17) مندوب يومي سلّم كلّ الأيام لكنّ أحدها بعد يومه ⇒ LateSubmitted 1، Submitted 5، الصفّ مكتمل لكنّه متأخر.
    [Fact]
    public async Task DailySales_SubmittedAfterDay_IsLateSubmitted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await EnsureDailyRoleAsync(admin, "SALES_B2C");
        var dept = await CreateDeptAsync();
        var emp = await CreateDailyEmployeeAsync(roleId, dept);
        var week = PastWeekKey();
        var days = WorkingDays(week);
        for (var i = 0; i < days.Count; i++)
        {
            var d = days[i];
            // اليوم الأوّل يُسلَّم بعد يومه (متأخر)، البقية في الموعد.
            var at = i == 0 ? AtRiyadh(d.AddDays(1)) : AtRiyadh(d);
            await AddDailySubmissionAsync(versionId, emp, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), SubmissionStatus.Submitted, at);
        }

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(5, summary.Submitted);
        Assert.Equal(1, summary.LateSubmitted);
        Assert.Equal(4, summary.OnTime);
        Assert.Equal(1, summary.Late);

        var row = await GetRowAsync(admin, week, emp);
        Assert.True(row.Submitted);
        Assert.True(row.Late);
        Assert.True(row.LateSubmitted);
        Assert.Equal("سلّم 5 من 5 يوم (متأخر)", row.StatusLabel);
    }

    // (18) Draft يومي (SALES_B2B) لا يُحتسب تسليمًا: يوم مسودّة ⇒ مُسلَّم 4 من 5، MissingOverdue 1.
    [Fact]
    public async Task DailySales_Draft_DoesNotCountAsSubmitted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await EnsureDailyRoleAsync(admin, "SALES_B2B");
        var dept = await CreateDeptAsync();
        var emp = await CreateDailyEmployeeAsync(roleId, dept);
        var week = PastWeekKey();
        var days = WorkingDays(week);
        for (var i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var status = i == 0 ? SubmissionStatus.Draft : SubmissionStatus.Submitted;
            await AddDailySubmissionAsync(versionId, emp, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), status, AtRiyadh(d));
        }

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(5, summary.Expected);
        Assert.Equal(4, summary.Submitted);       // المسودّة لا تُحتسب
        Assert.Equal(1, summary.MissingOverdue);

        var row = await GetRowAsync(admin, week, emp);
        Assert.False(row.Submitted);
        Assert.Equal("سلّم 4 من 5 يوم", row.StatusLabel);
    }

    // ===== أدوات مساعدة =====

    // أيام العمل ضمن الأسبوع التشغيلي (الخميس→الأربعاء) باستثناء الجمعة/السبت = 5 أيام.
    private static List<DateOnly> WorkingDays(string weekKey)
    {
        var (start, end) = ReportCalendarPolicy.WeekRange(weekKey);
        var list = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday))
                list.Add(d);
        return list;
    }

    private static DateTime AtRiyadh(DateOnly date) =>
        // 12:00 ظهرًا بتوقيت الرياض ⇒ ناقص 3 ساعات = 09:00 UTC (يحافظ على نفس اليوم محليًّا).
        DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(12, 0)).Add(-ReportCalendarPolicy.RiyadhOffset), DateTimeKind.Utc);

    // departmentId يعزل العدّ عن تراكم القاعدة المشتركة (الأدمن company-wide).
    private async Task<ComplianceSummaryReport> GetSummaryAsync(HttpClient client, string weekKey, Guid? departmentId = null)
    {
        var url = $"/api/reports/compliance-summary?weekKey={weekKey}";
        if (departmentId is not null) url += $"&departmentId={departmentId}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var s = await res.ReadAsync<ComplianceSummaryReport>();
        Assert.NotNull(s);
        return s!;
    }

    private async Task<SubmissionComplianceReport> GetReportAsync(HttpClient client, string weekKey)
    {
        var res = await client.GetAsync($"/api/reports/submission-compliance?weekKey={weekKey}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var r = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.NotNull(r);
        return r!;
    }

    private async Task<SubmissionComplianceRow> GetRowAsync(HttpClient client, string weekKey, Guid userId)
    {
        var report = await GetReportAsync(client, weekKey);
        var row = report.Rows.FirstOrDefault(r => r.UserId == userId);
        Assert.NotNull(row);
        return row!;
    }

    private async Task<(Guid RoleId, Guid VersionId)> CreateWeeklyRoleAsync(HttpClient admin, string tag)
    {
        var roleId = await CreateJobRoleAsync(tag);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {tag} {Guid.NewGuid():N}", null, roleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (roleId, versionId);
    }

    private async Task<Guid> CreateWeeklyEmployeeAsync(Guid jobRoleId, Guid? managerId = null, Guid? deptId = null)
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: managerId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        if (deptId is not null) user.DepartmentId = deptId;
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task AddSubmissionAsync(Guid versionId, Guid submitterId, string weekKey, SubmissionStatus status, DateTime submittedAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = weekKey,
            Status = status,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : submittedAtUtc
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> CreateDeptAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}".Substring(0, 16), IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    // مسمّى مبيعات يومي (SALES_B2C/SALES_B2B): فهرس Code فريد + قاعدة مشتركة ⇒ ابحث-أو-أنشئ المسمّى وقالبه
    // الأساسي اليومي المنشور (الدورية اليومية تُشتقّ من الكود لا من القالب). يعيد إصدارًا منشورًا لإلحاق التسليمات.
    private async Task<(Guid RoleId, Guid VersionId)> EnsureDailyRoleAsync(HttpClient admin, string code)
    {
        Guid roleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = await db.JobRoles.FirstOrDefaultAsync(j => j.Code == code);
            if (role is null)
            {
                role = new JobRole { NameAr = $"مسمّى {code}", Code = code };
                db.JobRoles.Add(role);
                await db.SaveChangesAsync();
            }
            roleId = role.Id;

            var existing = await db.ReportTemplates
                .Where(t => t.JobRoleId == roleId && t.IsActive
                            && t.Classification == TemplateClassification.Primary
                            && t.DefaultPeriodType != PeriodType.Monthly)
                .Select(t => t.Id)
                .FirstOrDefaultAsync();
            if (existing != Guid.Empty)
            {
                var vId = await db.ReportTemplateVersions
                    .Where(v => v.ReportTemplateId == existing && v.IsPublished)
                    .Select(v => v.Id)
                    .FirstOrDefaultAsync();
                if (vId != Guid.Empty) return (roleId, vId);
            }
        }

        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب يومي {code} {Guid.NewGuid():N}", null, roleId, PeriodType.Daily, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (roleId, versionId);
    }

    private async Task<Guid> CreateDailyEmployeeAsync(Guid jobRoleId, Guid deptId)
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        user.DepartmentId = deptId;
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task AddDailySubmissionAsync(Guid versionId, Guid submitterId, string dayKey, SubmissionStatus status, DateTime submittedAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Daily,
            PeriodKey = dayKey,
            Status = status,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : submittedAtUtc
        });
        await db.SaveChangesAsync();
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RPT-DUE1 — محرّك مواعيد التقارير والتأخّر (محسوب عند الطلب، بلا جدول/هجرة).
/// يتحقّق من: الأسبوع التشغيلي (الخميس→الأربعاء)، مواعيد الأدوار (موظّف=أربعاء، قائد=خميس،
/// مدير=أحد، تنفيذي=اثنين)، توقيت الرياض، حالة الموظّف الذاتية (self-only متاح للموظّف)،
/// التمييز بين الأسبوعي واليومي (مبيعات)، النطاق (ScopeResolver) لكلٍّ من المدير/القائد/التنفيذي،
/// عدم تسرّب تأخّر الغير لموظّف عاديّ، ولا أثر جانبيّ (بلا بريد/إشعارات بريد/تعديل تسليم).
/// </summary>
[Collection("Integration")]
public class ReportDueTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportDueTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== مساعدات =====

    /// <summary>يجعل المستخدم «متوقَّعًا منه تقرير» عبر مسمّى وظيفي له قالب أساسي منشور (أسبوعي افتراضيًّا).</summary>
    private async Task<Guid> SetupReportingRoleAsync(Guid userId, string? cadenceCode = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // الكود يحمل فهرسًا فريدًا وقاعدة الاختبار مشتركة ⇒ ابحث ثم أنشئ عند وجود كود؛
        // وإلا أنشئ مسمّى فريدًا بلا كود (الفهرس الفريد مُصفّى على Code IS NOT NULL).
        JobRole? jobRole = cadenceCode is null ? null : await db.JobRoles.FirstOrDefaultAsync(j => j.Code == cadenceCode);
        if (jobRole is null)
        {
            jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = cadenceCode };
            db.JobRoles.Add(jobRole);
        }

        var template = new ReportTemplate
        {
            Title = $"قالب {Guid.NewGuid():N}",
            JobRoleId = jobRole.Id,
            Classification = TemplateClassification.Primary,
            DefaultPeriodType = PeriodType.Weekly,
            IsActive = true,
            Status = TemplateStatus.Published,
            OwnerId = userId
        };
        db.ReportTemplates.Add(template);

        // أرضيّة الانطباق = MAX(إنشاء المستخدم، أوّل نشر للقالب). لاختبار أسبوع ماضٍ كتأخّر فعليّ،
        // يجب أن يكون الموظّف موجودًا والقالب منشورًا قبل ذلك الأسبوع؛ وإلّا فالدورة قبل الأرضيّة = غير مطلوبة.
        var floorAnchor = DateTime.UtcNow.AddDays(-60);
        var version = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = floorAnchor
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        if (user.CreatedAtUtc > floorAnchor) user.CreatedAtUtc = floorAnchor;
        await db.SaveChangesAsync();
        return version.Id;
    }

    /// <summary>يُدرج تسليمًا مباشرةً في القاعدة (للتحكّم في الفترة/الحالة دون المرور بمنطق الإنشاء).</summary>
    private async Task InsertSubmissionAsync(Guid versionId, Guid submitterId, PeriodType periodType, string periodKey,
        SubmissionStatus status, Guid? currentApproverId = null, DateTime? submittedAtUtc = null,
        Guid? teamId = null, Guid? departmentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = status,
            CurrentApproverId = currentApproverId,
            SubmittedAtUtc = submittedAtUtc ?? (status == SubmissionStatus.Draft ? null : DateTime.UtcNow),
            TeamId = teamId,
            DepartmentId = departmentId
        });
        await db.SaveChangesAsync();
    }

    private static string CurrentWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());
    private static string PastWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday().AddDays(-21));

    // ===== 1) الأسبوع التشغيلي: السبت → الجمعة (وموعد الموظّف = الأربعاء = بداية الدورة +4) =====
    // (التقويم الرسميّ الموحّد Sat→Fri من ROLE-AWARE-REPORTING-CALENDAR؛ الموعد يُشتقّ من بداية الدورة لا نهايتها.)
    [Fact]
    public void WeekBoundaries_AreSaturdayToFriday_EmployeeDueIsWednesday()
    {
        var key = CurrentWeekKey();
        var (start, end) = ReportCalendarPolicy.WeekRange(key);
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, end.DayOfWeek);
        Assert.Equal(start.AddDays(6), end);
        // موظّف عاديّ: موعده الأربعاء (بداية الدورة +4).
        Assert.Equal(start.AddDays(4), ReportCalendarPolicy.DueDateForRole(key, Roles.Employee));
        Assert.Equal(DayOfWeek.Wednesday, ReportCalendarPolicy.DueDateForRole(key, Roles.Employee).DayOfWeek);
    }

    // ===== 2) مواعيد الأدوار: قائد=خميس(+5)، مدير=أحد(+8)، تنفيذي=اثنين(+9) — من بداية الدورة (السبت) =====
    [Fact]
    public void DueDateForRole_MapsTeamLeaderThursday_ManagerSunday_ExecutiveMonday()
    {
        var key = CurrentWeekKey();
        var start = ReportCalendarPolicy.WeekRange(key).Start; // السبت = بداية الدورة
        Assert.Equal(start.AddDays(5), ReportCalendarPolicy.DueDateForRole(key, Roles.TeamLeader));
        Assert.Equal(start.AddDays(8), ReportCalendarPolicy.DueDateForRole(key, Roles.Manager));
        Assert.Equal(start.AddDays(9), ReportCalendarPolicy.DueDateForRole(key, Roles.GeneralManager));
        Assert.Equal(start.AddDays(9), ReportCalendarPolicy.DueDateForRole(key, Roles.Ceo));
        Assert.Equal(DayOfWeek.Thursday, ReportCalendarPolicy.DueDateForRole(key, Roles.TeamLeader).DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, ReportCalendarPolicy.DueDateForRole(key, Roles.Manager).DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, ReportCalendarPolicy.DueDateForRole(key, Roles.Ceo).DayOfWeek);
    }

    // ===== 3) توقيت الرياض (UTC+3 ثابت بلا توقيت صيفي) =====
    [Fact]
    public void RiyadhDate_ShiftsUtcByPlusThreeHours()
    {
        // 2026-06-10 23:00 UTC ⇒ 2026-06-11 02:00 بتوقيت الرياض ⇒ التاريخ المحلّي هو اليوم التالي.
        var utc = new DateTime(2026, 6, 10, 23, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 6, 11), ReportCalendarPolicy.RiyadhDate(utc));
        Assert.Equal(TimeSpan.FromHours(3), ReportCalendarPolicy.RiyadhOffset);
    }

    // ===== 4) my-status يتطلّب مصادقة =====
    [Fact]
    public async Task MyStatus_Anonymous_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/reports/due/my-status");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== 5) الأسبوع الحالي: موظّف بلا تسليم ⇒ غير متأخّر (المهلة لم تنتهِ) =====
    [Fact]
    public async Task MyStatus_CurrentWeek_Employee_NotOverdue()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(employeeId);

        var status = await (await employee.GetAsync("/api/reports/due/my-status")).ReadAsync<ReportDueMyStatus>();

        Assert.NotNull(status);
        Assert.True(status!.Expected);
        Assert.False(status.Submitted);
        Assert.False(status.IsOverdue);
        Assert.Equal(DelayType.NoDelay, status.DelayType);
        // موعد الموظّف = الأربعاء (بداية الأسبوع السبت + 4).
        Assert.Equal(status.WeekStart.AddDays(4), status.EmployeeDueDate);
        Assert.Equal(DayOfWeek.Wednesday, status.EmployeeDueDate.DayOfWeek);
    }

    // ===== 6) أسبوع ماضٍ: موظّف بلا تسليم ⇒ متأخّر (تجاوز الموعد) =====
    [Fact]
    public async Task MyStatus_PastWeek_Employee_Overdue()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(employeeId);

        var status = await (await employee.GetAsync($"/api/reports/due/my-status?weekKey={PastWeekKey()}"))
            .ReadAsync<ReportDueMyStatus>();

        Assert.NotNull(status);
        Assert.True(status!.Expected);
        Assert.False(status.Submitted);
        Assert.True(status.IsOverdue);
        Assert.Equal(DelayType.EmployeeReportNotSubmitted, status.DelayType);
    }

    // ===== 7) تسليم في الموعد ⇒ مُسلَّم وغير متأخّر =====
    [Fact]
    public async Task MyStatus_SubmittedOnTime_NotOverdue()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(employeeId);
        var key = CurrentWeekKey();
        await InsertSubmissionAsync(versionId, employeeId, PeriodType.Weekly, key, SubmissionStatus.Submitted);

        var status = await (await employee.GetAsync($"/api/reports/due/my-status?weekKey={key}"))
            .ReadAsync<ReportDueMyStatus>();

        Assert.NotNull(status);
        Assert.True(status!.Submitted);
        Assert.False(status.IsOverdue);
        Assert.Equal(DelayType.NoDelay, status.DelayType);
        Assert.NotNull(status.SubmissionId);
    }

    // ===== 8) موظّف بلا مسمّى تقارير ⇒ لا يُتوقَّع منه تقرير =====
    [Fact]
    public async Task MyStatus_NoReportingRole_NotExpected()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var status = await (await employee.GetAsync("/api/reports/due/my-status")).ReadAsync<ReportDueMyStatus>();

        Assert.NotNull(status);
        Assert.False(status!.Expected);
        Assert.False(status.IsOverdue);
        Assert.Equal(DelayType.NoDelay, status.DelayType);
    }

    // ===== 9) مندوب مبيعات ⇒ مسار يوميّ (يُحتسب أيام العمل لا الأسبوع كوحدة) =====
    [Fact]
    public async Task MyStatus_SalesUser_UsesDailyWorkingDays()
    {
        var (sales, salesId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(salesId, "SALES_B2C");

        var status = await (await sales.GetAsync("/api/reports/due/my-status")).ReadAsync<ReportDueMyStatus>();

        Assert.NotNull(status);
        Assert.True(status!.Expected);
        Assert.Contains("يوم عمل", status.StatusLabel);
    }

    // ===== 10) نطاق المدير: يرى تأخّر مرؤوسه المباشر (أسبوع ماضٍ غير مُسلَّم) =====
    [Fact]
    public async Task Overview_Manager_SeesDirectReportOverdue()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await SetupReportingRoleAsync(reportId);
        var key = PastWeekKey();

        var overview = await (await manager.GetAsync($"/api/reports/due/overview?weekKey={key}"))
            .ReadAsync<ReportDueOverview>();

        Assert.NotNull(overview);
        Assert.Equal("department", overview!.ScopeType);
        Assert.True(overview.MissingReportsCount >= 1);
        Assert.True(overview.OverdueReportsCount >= 1);
        Assert.Contains(overview.Items, r => r.UserId == reportId && r.DelayType == DelayType.EmployeeReportNotSubmitted);
    }

    // ===== 11) نطاق قائد الفريق: يرى تأخّر عضو فريقه (مرؤوس مباشر) =====
    [Fact]
    public async Task Overview_TeamLeader_SeesTeamMemberOverdue()
    {
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", leaderId);
        await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();

        var overview = await (await leader.GetAsync($"/api/reports/due/overview?weekKey={key}"))
            .ReadAsync<ReportDueOverview>();

        Assert.NotNull(overview);
        Assert.Equal("team", overview!.ScopeType);
        Assert.Contains(overview.Items, r => r.UserId == memberId);
    }

    // ===== 12) قائمة التأخّر: تُدرج التقارير غير المُسلَّمة لأسبوع ماضٍ =====
    [Fact]
    public async Task Overdue_PastWeek_ListsMissingReport()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await SetupReportingRoleAsync(reportId);
        var key = PastWeekKey();

        var overdue = await (await manager.GetAsync($"/api/reports/due/overdue?weekKey={key}"))
            .ReadAsync<ReportDueOverdueReport>();

        Assert.NotNull(overdue);
        Assert.True(overdue!.OverdueReportsCount >= 1);
        Assert.Contains(overdue.Rows, r => r.UserId == reportId && r.DelayType == DelayType.EmployeeReportNotSubmitted);
    }

    // ===== 13) قائمة التأخّر: التسليم في الموعد (الأسبوع الحالي) لا يظهر كمتأخّر =====
    [Fact]
    public async Task Overdue_CurrentWeek_SubmittedReport_NotListed()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var versionId = await SetupReportingRoleAsync(reportId);
        var key = CurrentWeekKey();
        await InsertSubmissionAsync(versionId, reportId, PeriodType.Weekly, key, SubmissionStatus.Submitted);

        var overdue = await (await manager.GetAsync($"/api/reports/due/overdue?weekKey={key}"))
            .ReadAsync<ReportDueOverdueReport>();

        Assert.NotNull(overdue);
        Assert.DoesNotContain(overdue!.Rows, r => r.UserId == reportId);
    }

    // ===== 14) مراجعة متأخّرة لقائد فريق: تظهر بنوع تأخّر مراجعة القائد =====
    [Fact]
    public async Task Overview_TeamLeaderReviewOverdue_Flagged()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", managerId);
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", leaderId);
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        // تقرير عضوٍ عالقٌ عند قائد الفريق للاعتماد منذ أسبوع ماضٍ ⇒ مراجعة القائد متأخّرة.
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.Submitted, currentApproverId: leaderId);

        var overview = await (await manager.GetAsync($"/api/reports/due/overview?weekKey={key}"))
            .ReadAsync<ReportDueOverview>();

        Assert.NotNull(overview);
        Assert.True(overview!.OverdueReviewsCount >= 1);
        Assert.Contains(overview.Items, r => r.UserId == leaderId && r.DelayType == DelayType.TeamLeaderReviewOverdue);
    }

    // ===== 15) مراجعة متأخّرة لمدير: تظهر بنوع تأخّر مراجعة المدير (نطاق تنفيذيّ) =====
    [Fact]
    public async Task Overview_ManagerReviewOverdue_Flagged()
    {
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.ApprovedByDirectManager, currentApproverId: managerId);

        var overdue = await (await ceo.GetAsync($"/api/reports/due/overdue?weekKey={key}"))
            .ReadAsync<ReportDueOverdueReport>();

        Assert.NotNull(overdue);
        Assert.Contains(overdue!.Rows, r => r.UserId == managerId && r.DelayType == DelayType.ManagerReviewOverdue);
    }

    // ===== 16) الرئيس التنفيذي: نطاق الشركة كاملةً (SeesAll) =====
    [Fact]
    public async Task Overview_Ceo_ScopeIsCompany()
    {
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(reportId);
        var key = PastWeekKey();

        var overview = await (await ceo.GetAsync($"/api/reports/due/overview?weekKey={key}"))
            .ReadAsync<ReportDueOverview>();

        Assert.NotNull(overview);
        Assert.Equal("company", overview!.ScopeType);
        // ضمن نطاق الشركة، الموظّف المتأخّر يظهر للرئيس التنفيذي.
        Assert.Contains(overview.Items, r => r.UserId == reportId);
    }

    // ===== 17) موظّف عاديّ: نطاقه ذاتيّ فقط ⇒ لا يرى تأخّر غيره =====
    [Fact]
    public async Task Overview_PlainEmployee_SelfScope_DoesNotSeeOthers()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, otherId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(otherId);
        var key = PastWeekKey();

        var overview = await (await employee.GetAsync($"/api/reports/due/overview?weekKey={key}"))
            .ReadAsync<ReportDueOverview>();

        Assert.NotNull(overview);
        Assert.Equal("own", overview!.ScopeType);
        Assert.DoesNotContain(overview.Items, r => r.UserId == otherId);
    }

    // ===== 18) قراءة فقط: لا إشعار بريد، لا صندوق صادر، لا تعديل تسليم =====
    [Fact]
    public async Task DueEndpoints_AreReadOnly_NoEmail_NoOutbox_NoSubmissionRegression()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(employeeId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, employeeId, PeriodType.Weekly, key, SubmissionStatus.Submitted);

        int emailsBefore, outboxBefore;
        (Guid Id, SubmissionStatus Status) subBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            emailsBefore = await db.EmailNotifications.CountAsync();
            outboxBefore = await db.EmailOutbox.CountAsync();
            var s = await db.ReportSubmissions.AsNoTracking()
                .Where(x => x.SubmitterId == employeeId && x.PeriodKey == key).FirstAsync();
            subBefore = (s.Id, s.Status);
        }

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync($"/api/reports/due/my-status?weekKey={key}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync($"/api/reports/due/overview?weekKey={key}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync($"/api/reports/due/overdue?weekKey={key}")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(emailsBefore, await db.EmailNotifications.CountAsync());
            Assert.Equal(outboxBefore, await db.EmailOutbox.CountAsync());
            var after = await db.ReportSubmissions.AsNoTracking().FirstAsync(x => x.Id == subBefore.Id);
            Assert.Equal(subBefore.Status, after.Status);
        }
    }
}

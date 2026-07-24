using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// EMAIL-REPORT-REMINDERS-R1 — مولّد تذكيرات/تنبيهات التقارير (توليد يدويّ DryRun).
/// يتحقّق من: حصر الصلاحية (Admin/CEO/GM/CeoSupport فقط)، توليد الأنواع التسعة بمفاتيح ترابط دقيقة،
/// منع التكرار عبر CorrelationKey، تمييز اليوميّ عن الأسبوعيّ، تقييد التاريخ، أعلام التضمين،
/// كتم التذكير عند وجود تسليم، الوضع DryRun على الصفوف، وعدم مسّ email_outbox إطلاقًا.
/// كلّ التأكيدات على مفاتيح ترابط مستخدمين مُنشأين حديثًا (القاعدة مشتركة ⇒ لا تأكيد على أعداد مطلقة).
/// </summary>
[Collection("Integration")]
public class ReportRemindersTests
{
    private const string Endpoint = "/api/report-reminders/dry-run/generate";
    private readonly CustomWebApplicationFactory _factory;

    public ReportRemindersTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== مساعدات =====

    /// <summary>يجعل المستخدم «متوقَّعًا منه تقرير» عبر مسمّى وظيفي له قالب أساسي منشور (أسبوعي افتراضيًّا). يعيد versionId.</summary>
    private async Task<Guid> SetupReportingRoleAsync(Guid userId, string? cadenceCode = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

        var version = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task InsertSubmissionAsync(Guid versionId, Guid submitterId, PeriodType periodType, string periodKey,
        SubmissionStatus status, Guid? currentApproverId = null)
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
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>ينشئ إدارة بمدير فعليّ (ManagerId) ويُسنِد الأعضاء إليها — للنوع 5 (ملخّص المدير).</summary>
    private async Task CreateDepartmentWithManagerAsync(Guid managerId, params Guid[] memberIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", ManagerId = managerId, IsActive = true };
        db.Set<Department>().Add(dept);
        foreach (var mid in memberIds)
        {
            var u = await db.Users.FirstAsync(x => x.Id == mid);
            u.DepartmentId = dept.Id;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ReportReminderRunResult> GenerateAsync(HttpClient client, object body)
    {
        var res = await client.PostAsJsonAsync(Endpoint, body);
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<ReportReminderRunResult>())!;
    }

    private async Task<int> CountByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.CountAsync(n => n.CorrelationKey == correlationKey);
    }

    private async Task<EmailNotification?> FirstByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().FirstOrDefaultAsync(n => n.CorrelationKey == correlationKey);
    }

    private static string CurrentWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());
    private static string PastWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday().AddDays(-21));
    private static string FutureWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday().AddDays(21));
    private static string DateKey(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ===== 1-4) حصر الصلاحية: الأدوار غير المصرّح لها =====
    [Fact]
    public async Task Generate_Anonymous_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Generate_Employee_Returns403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Generate_TeamLeader_Returns403()
    {
        var (leader, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var res = await leader.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Generate_Manager_Returns403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 5-8) حصر الصلاحية: الأدوار المصرّح لها =====
    [Fact]
    public async Task Generate_Admin_Returns200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Generate_Ceo_Returns200()
    {
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var res = await ceo.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Generate_GeneralManager_Returns200()
    {
        var gm = await TestAuth.LoginAsRoleAsync(_factory, "GeneralManager");
        var res = await gm.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Generate_CeoSupport_Returns200()
    {
        var support = await TestAuth.LoginAsRoleAsync(_factory, "CeoSupport");
        var res = await support.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 9) النوع 1: تذكير أسبوعي مستحقّ (أسبوع مستقبليّ ⇒ غير متأخّر) =====
    [Fact]
    public async Task Generate_WeeklyDue_FutureWeek_CreatesWeeklyDueRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = FutureWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-weekly-due:{key}:{userId}");
        Assert.NotNull(row);
        Assert.Equal("report-weekly-due", row!.EventType);
        Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
    }

    // ===== 10) النوع 2: تذكير يوميّ مستحقّ اليوم (مبيعات، يوم عمل) =====
    [Fact]
    public async Task Generate_DailyDue_Today_CreatesDailyDueRowOnWorkingDay()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId, "SALES_B2C");
        var today = ReportCalendarPolicy.RiyadhToday();
        var isWorkingDay = today.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday);

        await GenerateAsync(admin, new { weekKey = CurrentWeekKey(), date = DateKey(today) });

        var key = $"report-daily-due:{DateKey(today)}:{userId}";
        if (isWorkingDay)
        {
            var row = await FirstByKeyAsync(key);
            Assert.NotNull(row);
            Assert.Equal("report-daily-due", row!.EventType);
            Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
        }
        else
        {
            Assert.Equal(0, await CountByKeyAsync(key));
        }
    }

    // ===== 11) النوع 3: تنبيه تأخّر أسبوعيّ (أسبوع ماضٍ) =====
    [Fact]
    public async Task Generate_WeeklyOverdue_PastWeek_CreatesOverdueRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = PastWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-overdue:{key}:{userId}:{DelayType.EmployeeReportNotSubmitted}");
        Assert.NotNull(row);
        Assert.Equal("report-overdue", row!.EventType);
    }

    // ===== 12) النوع 3 (يوميّ): صفّ لكل يوم عمل متأخّر في أسبوع ماضٍ كامل (5 أيام) =====
    // DAILY-BUSINESS-DAY-COMPLIANCE-R1: أسبوع W28 (السبت 2026-07-04 → الجمعة 2026-07-10) هو أوّل
    // أسبوع دورة بعد أرضيّة الإطلاق المؤسّسيّة (2026-07-04) وقد انقضى بالكامل قبل «اليوم» (2026-07-24).
    // أيّام العمل المتوقَّعة فيه = الأحد→الخميس بعد الأرضية = 05,06,07,08,09 = 5 (السبت 04 عطلة،
    // والجمعة 10 عطلة). لا يُستعمَل PastWeekKey (today−21 = 2026-07-03) لأنه أسبوع سابق للأرضية
    // بالكامل ⇒ 0 متوقَّع ⇒ 0 تذكيرات تأخّر (السلوك الصحيح الموحَّد: لا تأخّر قبل إطلاق النظام).
    [Fact]
    public async Task Generate_DailyOverdue_PastWeek_CreatesRowPerWorkingDay()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId, "SALES_B2C");
        const string key = "2026-W28"; // أوّل أسبوع دورة منقضٍ بعد أرضيّة الإطلاق

        await GenerateAsync(admin, new { weekKey = key });

        var suffix = $":{userId}:{DelayType.EmployeeReportNotSubmitted}";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.EmailNotifications.CountAsync(n =>
            n.CorrelationKey.StartsWith("report-overdue:") && n.CorrelationKey.EndsWith(suffix));
        // أيّام العمل بعد الأرضية في W28 = 05,06,07,08,09 = 5 (السبت 04 والجمعة 10 مستبعدان).
        Assert.Equal(5, count);
    }

    // ===== 13) النوع 4: ملخّص تأخّر الفريق لقائده الفعليّ =====
    [Fact]
    public async Task Generate_TeamOverdueSummary_CreatesRowForLeader()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(memberId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var key = PastWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-team-overdue-summary:{key}:{leaderId}");
        Assert.NotNull(row);
        Assert.Equal("report-team-overdue-summary", row!.EventType);
    }

    // ===== 14) النوع 5: ملخّص تأخّر الإدارة لمديرها الفعليّ =====
    [Fact]
    public async Task Generate_DepartmentOverdueSummary_CreatesRowForManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(memberId);
        await CreateDepartmentWithManagerAsync(managerId, memberId);
        var key = PastWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-department-overdue-summary:{key}:{managerId}");
        Assert.NotNull(row);
        Assert.Equal("report-department-overdue-summary", row!.EventType);
    }

    // ===== 15) النوع 6: ملخّص تنفيذيّ (CEO مُنشأ) عند وجود أيّ تأخّر =====
    [Fact]
    public async Task Generate_ExecutiveOverdueSummary_CreatesRowForCeo()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-executive-overdue-summary:{key}:{ceoId}");
        Assert.NotNull(row);
        Assert.Equal("report-executive-overdue-summary", row!.EventType);
    }

    // ===== 16) النوع 7: تأخّر مراجعة قائد الفريق (تقرير عالق عند قائد فريق منذ أسبوع ماضٍ) =====
    [Fact]
    public async Task Generate_TeamLeaderReviewOverdue_CreatesRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.Submitted, currentApproverId: leaderId);

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-review-overdue-teamleader:{key}:{leaderId}");
        Assert.NotNull(row);
        Assert.Equal("report-review-overdue-teamleader", row!.EventType);
    }

    // ===== 17) النوع 8: تأخّر مراجعة المدير =====
    [Fact]
    public async Task Generate_ManagerReviewOverdue_CreatesRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.ApprovedByDirectManager, currentApproverId: managerId);

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-review-overdue-manager:{key}:{managerId}");
        Assert.NotNull(row);
        Assert.Equal("report-review-overdue-manager", row!.EventType);
    }

    // ===== 18) النوع 9: مراجعات تنفيذية معلّقة (معتمِد تنفيذيّ) =====
    [Fact]
    public async Task Generate_ExecutiveReviewPending_CreatesRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, execId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.ApprovedByNextLevel, currentApproverId: execId);

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-review-pending-executive:{key}:{execId}");
        Assert.NotNull(row);
        Assert.Equal("report-review-pending-executive", row!.EventType);
    }

    // ===== 19) منع التكرار: تشغيلان ⇒ صفّ واحد فقط + عدّاد Duplicate موجب =====
    [Fact]
    public async Task Generate_RunTwice_DeduplicatesByCorrelationKey()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = PastWeekKey();
        var ck = $"report-overdue:{key}:{userId}:{DelayType.EmployeeReportNotSubmitted}";

        await GenerateAsync(admin, new { weekKey = key });
        var second = await GenerateAsync(admin, new { weekKey = key });

        Assert.Equal(1, await CountByKeyAsync(ck));
        Assert.True(second.SkippedDuplicate > 0);
    }

    // ===== 20) تسليم موجود ⇒ يُكتَم تذكير الاستحقاق =====
    [Fact]
    public async Task Generate_SubmittedReport_SuppressesDueReminder()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(userId);
        var key = FutureWeekKey();
        await InsertSubmissionAsync(versionId, userId, PeriodType.Weekly, key, SubmissionStatus.Submitted);

        await GenerateAsync(admin, new { weekKey = key });

        Assert.Equal(0, await CountByKeyAsync($"report-weekly-due:{key}:{userId}"));
    }

    // ===== 21) IncludeDue=false ⇒ لا تذكير استحقاق =====
    [Fact]
    public async Task Generate_IncludeDueFalse_SuppressesDueTypes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = FutureWeekKey();

        await GenerateAsync(admin, new { weekKey = key, includeDue = false });

        Assert.Equal(0, await CountByKeyAsync($"report-weekly-due:{key}:{userId}"));
    }

    // ===== 22) IncludeOverdue=false ⇒ لا تنبيه تأخّر =====
    [Fact]
    public async Task Generate_IncludeOverdueFalse_SuppressesOverdueTypes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = PastWeekKey();

        await GenerateAsync(admin, new { weekKey = key, includeOverdue = false });

        Assert.Equal(0, await CountByKeyAsync($"report-overdue:{key}:{userId}:{DelayType.EmployeeReportNotSubmitted}"));
    }

    // ===== 23) IncludeReviewOverdue=false ⇒ لا تنبيه مراجعة =====
    [Fact]
    public async Task Generate_IncludeReviewOverdueFalse_SuppressesReviewTypes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var versionId = await SetupReportingRoleAsync(memberId);
        var key = PastWeekKey();
        await InsertSubmissionAsync(versionId, memberId, PeriodType.Weekly, key,
            SubmissionStatus.Submitted, currentApproverId: leaderId);

        await GenerateAsync(admin, new { weekKey = key, includeReviewOverdue = false });

        Assert.Equal(0, await CountByKeyAsync($"report-review-overdue-teamleader:{key}:{leaderId}"));
    }

    // ===== 24) تقييد التاريخ: اليوميّ يقتصر على اليوم المحدّد فقط =====
    // DAILY-BUSINESS-DAY-COMPLIANCE-R1: يُستعمَل أسبوع W28 المنقضي بعد الأرضية (لا PastWeekKey
    // السابق للأرضية بالكامل الذي يُنتج 0 أيّام متوقَّعة). اليومان مختاران من أيّام العمل بعد
    // الأرضية (الاثنين 07-06 والأربعاء 07-08) لضمان دخولهما التوقّع وفق العقد الموحَّد.
    [Fact]
    public async Task Generate_DateRestriction_LimitsDailyToSingleDay()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId, "SALES_B2C");
        const string key = "2026-W28";
        var restrictedDay = new DateOnly(2026, 7, 6); // الاثنين — يوم عمل ضمن W28 بعد الأرضية
        var otherDay = new DateOnly(2026, 7, 8);       // الأربعاء — يوم عمل مختلف بعد الأرضية

        await GenerateAsync(admin, new { weekKey = key, date = DateKey(restrictedDay) });

        var suffix = $":{userId}:{DelayType.EmployeeReportNotSubmitted}";
        Assert.Equal(1, await CountByKeyAsync($"report-overdue:{DateKey(restrictedDay)}{suffix}"));
        Assert.Equal(0, await CountByKeyAsync($"report-overdue:{DateKey(otherDay)}{suffix}"));
    }

    // ===== 25) عدم مسّ email_outbox إطلاقًا =====
    [Fact]
    public async Task Generate_DoesNotTouchEmailOutbox()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = PastWeekKey();

        int outboxBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            outboxBefore = await db.EmailOutbox.CountAsync();
        }

        await GenerateAsync(admin, new { weekKey = key });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(outboxBefore, await db.EmailOutbox.CountAsync());
        }
    }

    // ===== 26) الصفوف المُنشأة بوضع DryRun (Status + Mode) =====
    [Fact]
    public async Task Generate_CreatedRows_AreDryRunStatusAndMode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);
        var key = FutureWeekKey();

        await GenerateAsync(admin, new { weekKey = key });

        var row = await FirstByKeyAsync($"report-weekly-due:{key}:{userId}");
        Assert.NotNull(row);
        Assert.Equal(EmailNotificationStatus.DryRun, row!.Status);
        Assert.Equal(EmailNotificationMode.DryRun, row.Mode);
        Assert.Equal("ReportReminder", row.EntityType);
    }

    // ===== 27) بيانات النتيجة: مفتاح الأسبوع + الوضع + التسمية =====
    [Fact]
    public async Task Generate_Result_ReturnsWeekKeyModeAndLabel()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var key = PastWeekKey();

        var result = await GenerateAsync(admin, new { weekKey = key });

        Assert.Equal(key, result.WeekKey);
        Assert.Equal("DryRun", result.Mode);
        Assert.False(string.IsNullOrWhiteSpace(result.WeekLabel));
    }
}

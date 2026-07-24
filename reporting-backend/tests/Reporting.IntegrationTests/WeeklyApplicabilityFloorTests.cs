using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// WEEKLY-REPORTING-APPLICABILITY-FLOOR-2026-07-04-R1 — اختبارات مُوجَّهة لأرضيّة إطلاق التقارير الأسبوعيّة.
/// قرار الأعمال: أوّل دورة أسبوعيّة منطبقة = الدورة المرتبطة بـ 4 يوليو 2026 (السبت = بداية 2026-W28).
/// أيّ دورة أسبوعيّة تبدأ قبلها: IsApplicable=false، لا Expected/Missing/MissingOverdue، لا تدخل مقام الالتزام
/// ولا تُخفِّض النسبة، ولا تظهر متأخّرة. التسليمات التاريخيّة الفعليّة تبقى مخزَّنة ومقروءة (لا حذف/إعادة كتابة).
/// النطاق أسبوعيّ حصرًا — اليوميّ/الشهريّ غير متأثّر. مصدر مركزيّ واحد =
/// <see cref="ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor"/>. قراءة فقط — لا هجرة/كتابة إنتاج.
/// </summary>
[Collection("Integration")]
public class WeeklyApplicabilityFloorTests
{
    private readonly CustomWebApplicationFactory _factory;
    public WeeklyApplicabilityFloorTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static DateOnly Floor => ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor; // 2026-07-04

    // ساعة ثابتة بعيدة في المستقبل (الإثنين 2026-11-23 09:00Z) لاختبارات المُحلِّل: تجعل دورة الأرضيّة (2026-W28)
    // ماضية بوضوح (تجاوزت موعدها) لإثبات OverdueNotSubmitted بعد الأرضيّة، بينما ما قبل الأرضيّة يبقى «لا ينطبق».
    private static readonly DateTimeOffset Fixed = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    // مفتاح دورة أسبوعيّة تبدأ قبل الأرضيّة بأسبوع (بداية السبت 2026-06-27) — غير منطبقة.
    private static string PreFloorCycleKey() =>
        ReportingCalendarPolicy.CycleKeyFor(Floor.AddDays(-7));
    // أوّل دورة منطبقة = دورة الأرضيّة نفسها (بداية السبت 2026-07-04 = 2026-W28).
    private static string FloorCycleKey() =>
        ApplicabilityFloorPolicy.FirstEligibleCycleKey(Floor);

    // ============================================================
    // المجموعة (أ) — سياسة نقيّة (Pure) بلا I/O
    // ============================================================

    // (1) دورة تبدأ قبل الأرضيّة ⇒ IsCycleApplicable=false.
    [Fact]
    public void Policy_PreFloorCycleStart_IsCycleApplicable_False()
    {
        var preStart = ReportingCalendarPolicy.CycleStart(Floor.AddDays(-7));
        Assert.True(preStart < Floor);
        Assert.False(ApplicabilityFloorPolicy.IsCycleApplicable(preStart, Floor));
    }

    // (2) دورة الأرضيّة (السبت 2026-07-04) ⇒ IsCycleApplicable=true (شامل عند الأرضيّة).
    [Fact]
    public void Policy_FloorCycleStart_IsCycleApplicable_True()
    {
        var floorStart = ReportingCalendarPolicy.CycleStart(Floor);
        Assert.Equal(Floor, floorStart);
        Assert.True(ApplicabilityFloorPolicy.IsCycleApplicable(floorStart, Floor));
    }

    // (3) أوّل دورة مؤهَّلة = 2026-W28 بالضبط (مطابقة لمفتاح دورة 4 يوليو).
    [Fact]
    public void Policy_FirstEligibleCycleKey_Is_2026_W28()
    {
        var first = ApplicabilityFloorPolicy.FirstEligibleCycleKey(Floor);
        Assert.Equal(ReportingCalendarPolicy.CycleKeyFor(Floor), first);
        Assert.Equal("2026-W28", first);
    }

    // (4) ثابت الأرضيّة = السبت 4 يوليو 2026 (بداية دورة، ISO week 28).
    [Fact]
    public void Policy_WeeklyLaunchFloor_Is_Saturday_2026_07_04()
    {
        Assert.Equal(new DateOnly(2026, 7, 4), Floor);
        Assert.Equal(DayOfWeek.Saturday, Floor.DayOfWeek);
        Assert.Equal(Floor, ReportingCalendarPolicy.CycleStart(Floor)); // بداية دورة فعلًا
    }

    // (5) حين تكون الأرضيّة المنظّميّة أحدث من الأرضيّة الفرديّة ⇒ تحكم، والمصدر OrganizationalLaunchFloor.
    [Fact]
    public void Policy_Resolve_OrgFloorGoverns_WhenLaterThanIndividual()
    {
        var input = new ApplicabilityFloorPolicy.FloorInput(
            UserCreatedAt: new DateOnly(2026, 1, 1),
            TemplateFirstPublishedAt: new DateOnly(2026, 2, 1),
            AuditedJobRoleAssignedAt: null,
            OrganizationalLaunchFloor: Floor);
        var r = ApplicabilityFloorPolicy.Resolve(input);
        Assert.Equal(Floor, r.Floor);
        Assert.Equal(ApplicabilitySource.OrganizationalLaunchFloor, r.Source);
        Assert.Equal(ApplicabilityConfidence.High, r.Confidence);
    }

    // (6) حين تكون الأرضيّة المنظّميّة أقدم من الأرضيّة الفرديّة ⇒ تُتجاهَل، وتحكم الفرديّة (توافق خلفيّ).
    [Fact]
    public void Policy_Resolve_OrgFloorIgnored_WhenEarlierThanIndividual()
    {
        var lateIndividual = new DateOnly(2026, 9, 1);
        var input = new ApplicabilityFloorPolicy.FloorInput(
            UserCreatedAt: lateIndividual,
            TemplateFirstPublishedAt: null,
            AuditedJobRoleAssignedAt: null,
            OrganizationalLaunchFloor: Floor); // 2026-07-04 < 2026-09-01
        var r = ApplicabilityFloorPolicy.Resolve(input);
        Assert.Equal(lateIndividual, r.Floor);
        Assert.NotEqual(ApplicabilitySource.OrganizationalLaunchFloor, r.Source);
    }

    // ============================================================
    // المجموعة (ب) — المُحلِّل (ExpectedSubmissionStatusResolver) بساعة ثابتة
    // ============================================================

    // (7) دورة ما قبل الأرضيّة ⇒ لا تُتوقَّع (IsExpected=false) وحالتها NotApplicable، رغم أرضيّة فرديّة مبكّرة.
    [Fact]
    public async Task Resolver_PreFloorCycle_NotExpected_NotApplicable()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupWeeklyAsync(uid, EarlyAnchor); // أرضيّة فرديّة مبكّرة ⇒ الحاكم = الأرضيّة المنظّميّة
        var r = await ResolveOneAsync(uid, PreFloorCycleKey());
        Assert.NotNull(r);
        Assert.False(r!.IsExpected);
        Assert.False(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.NotApplicable, r.Status);
    }

    // (8) دورة ما قبل الأرضيّة ⇒ سبب الاستبعاد = BeforeApplicabilityFloor (لا إعفاء/إجازة).
    [Fact]
    public async Task Resolver_PreFloorCycle_ExclusionCode_BeforeApplicabilityFloor()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupWeeklyAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, PreFloorCycleKey());
        Assert.NotNull(r);
        Assert.Equal(CycleExclusionReason.BeforeApplicabilityFloor, r!.ExclusionReasonCode);
        Assert.NotEqual(CycleExclusionReason.ExemptOrOnLeave, r.ExclusionReasonCode);
    }

    // (9) دورة ما قبل الأرضيّة بلا تسليم ⇒ ليست متأخّرة ولا بند إجراء (لا OverdueNotSubmitted).
    [Fact]
    public async Task Resolver_PreFloorCycle_NoSubmission_NotOverdue_NotActionable()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupWeeklyAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, PreFloorCycleKey());
        Assert.NotNull(r);
        Assert.False(r!.IsActionable);
        Assert.NotEqual(ExpectedSubmissionStatus.OverdueNotSubmitted, r.Status);
        Assert.NotEqual(ExpectedSubmissionStatus.OverdueDraft, r.Status);
    }

    // (10) أوّل دورة منطبقة (2026-W28) بلا تسليم وبعد موعدها ⇒ OverdueNotSubmitted (السلوك الطبيعيّ يعود).
    [Fact]
    public async Task Resolver_FirstApplicableCycle_MissingAfterDue_OverdueNotSubmitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupWeeklyAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, FloorCycleKey());
        Assert.NotNull(r);
        Assert.True(r!.IsExpected);
        Assert.True(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.OverdueNotSubmitted, r.Status);
    }

    // (11) الإسقاط الإداريّ لدورة ما قبل الأرضيّة ⇒ Expected=0 (لا يدخل الموظّف مقام الالتزام).
    [Fact]
    public async Task Resolver_PreFloorCycle_ManagementProjection_ExpectedZero()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupWeeklyAsync(uid, EarlyAnchor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new ExpectedSubmissionStatusResolver(db, new FixedClock(Fixed));
        var proj = await resolver.ResolveManagementAsync(PreFloorCycleKey(), new[] { uid });

        Assert.Equal(0, proj.Expected);
        Assert.Equal(0, proj.OverdueNotSubmitted);
        Assert.DoesNotContain(proj.ActionItems, i => i.UserId == uid);
    }

    // (12) أوّل دورة منطبقة (2026-W28) مُسلَّمة ⇒ Submitted، IsExpected=true (تباين إيجابيّ مع ما قبل الأرضيّة).
    [Fact]
    public async Task Resolver_FirstApplicableCycle_Submitted_ExpectedTrue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupWeeklyAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, FloorCycleKey(), SubmissionStatus.Submitted, Fixed.UtcDateTime);
        var r = await ResolveOneAsync(uid, FloorCycleKey());
        Assert.NotNull(r);
        Assert.True(r!.IsExpected);
        Assert.True(r.HasSubmission);
        Assert.False(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.Submitted, r.Status);
    }

    // ============================================================
    // المجموعة (ج) — الالتزام (Compliance API) بالساعة الحقيقيّة على مفاتيح صريحة
    // ============================================================

    // (13) أسبوع ما قبل الأرضيّة ⇒ Expected=0، لا Missing، لا عقوبة نسبة (رغم أنّه أسبوع منقضٍ).
    [Fact]
    public async Task Compliance_PreFloorWeek_ExpectedZero_NoMissingPenalty()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "WAF13");
        var dept = await CreateDeptAsync();
        await CreateWeeklyEmployeeAsync(roleId, dept);
        var week = PreFloorWeekKey();

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(0, summary.Expected);
        Assert.Equal(0, summary.Missing);
        Assert.Equal(0, summary.MissingOverdue);
        Assert.Equal(0, summary.Late);
    }

    // (14) أوّل أسبوع منطبق (2026-W28) بلا تسليم وقد انقضى ⇒ Expected=1، MissingOverdue=1 (السلوك الطبيعيّ).
    [Fact]
    public async Task Compliance_FirstApplicableWeek_Missing_ExpectedOne_MissingOverdue()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "WAF14");
        var dept = await CreateDeptAsync();
        await CreateWeeklyEmployeeAsync(roleId, dept);
        var week = FloorWeekKey();

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(1, summary.Expected);
        Assert.Equal(1, summary.MissingOverdue);
        Assert.Equal(1, summary.Late);
        Assert.Equal(0, summary.Submitted);
    }

    // (15) أسبوع ما قبل الأرضيّة ⇒ الموظّف لا يظهر ضمن صفوف متابعة الالتزام (لا يُعرَض متأخّرًا/غائبًا).
    [Fact]
    public async Task Compliance_PreFloorWeek_SubmissionCompliance_EmployeeNotListed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, _) = await CreateWeeklyRoleAsync(admin, "WAF15");
        var dept = await CreateDeptAsync();
        var emp = await CreateWeeklyEmployeeAsync(roleId, dept);
        var week = PreFloorWeekKey();

        var report = await GetReportAsync(admin, week);
        Assert.DoesNotContain(report.Rows, r => r.UserId == emp);
    }

    // (16) DAILY-REPORTING-APPLICABILITY-R1: المندوب اليوميّ (SALES_B2C) خاضع لأرضيّة الإطلاق المنظّميّة
    // (4 يوليو 2026) مثل الأسبوعيّ. أسبوع كامل قبل الأرضيّة ⇒ لا يوم يوميّ متوقَّع (Expected=0، Submitted=0،
    // لا عقوبة تأخّر/غياب). التسليمات اليوميّة التاريخيّة الخمسة تبقى مخزَّنة ومقروءة في القاعدة (لا حذف/إعادة كتابة).
    [Fact]
    public async Task Compliance_PreFloorWeek_DailySales_SubjectToOrganizationalFloor_NoExpected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await EnsureDailyRoleAsync(admin, "SALES_B2C");
        var dept = await CreateDeptAsync();
        var emp = await CreateDailyEmployeeAsync(roleId, dept);
        var week = PreFloorWeekKey();
        var days = WorkingDays(week);
        foreach (var d in days)
            await AddDailySubmissionAsync(versionId, emp, d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                SubmissionStatus.Submitted, AtRiyadh(d));

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(0, summary.Expected);   // كلّ أيّام الأسبوع قبل الأرضيّة المنظّميّة ⇒ لا توقّع يوميّ (لا عقوبة قبل 4 يوليو).
        Assert.Equal(0, summary.Submitted);  // لا وحدات التزام يوميّة قبل الأرضيّة (تُطابق سلوك ما قبل الأرضيّة الأسبوعيّ).
        Assert.Equal(0, summary.MissingOverdue);
        Assert.Equal(0, summary.Late);

        // مع ذلك التسليمات اليوميّة الخمسة التاريخيّة باقية في القاعدة دون مساس (المتطلّب 11: حفظ ما قبل الأرضيّة).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedCount = await db.ReportSubmissions.AsNoTracking()
            .CountAsync(s => s.SubmitterId == emp && s.PeriodType == PeriodType.Daily
                             && s.Status == SubmissionStatus.Submitted);
        Assert.Equal(5, storedCount);
    }

    // (17) معادلات الالتزام صحيحة عند حدّ الأرضيّة (2026-W28): Expected-Submitted=Missing، LateSubmitted+MissingOverdue=Late.
    [Fact]
    public async Task Compliance_FirstApplicableWeek_Equations_Valid()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, versionId) = await CreateWeeklyRoleAsync(admin, "WAF17");
        var dept = await CreateDeptAsync();
        var onTime = await CreateWeeklyEmployeeAsync(roleId, dept);
        await CreateWeeklyEmployeeAsync(roleId, dept); // غائب
        var week = FloorWeekKey();
        var employeeDue = ReportCalendarPolicy.WeekRange(week).Start.AddDays(4);
        await AddSubmissionAsync(versionId, onTime, week, SubmissionStatus.Submitted, AtRiyadh(employeeDue));

        var summary = await GetSummaryAsync(admin, week, dept);
        Assert.Equal(2, summary.Expected);
        Assert.Equal(1, summary.Submitted);
        Assert.Equal(summary.Expected - summary.Submitted, summary.Missing);
        Assert.Equal(summary.LateSubmitted + summary.MissingOverdue, summary.Late);
        Assert.True(summary.Expected >= 0 && summary.Submitted >= 0 && summary.Missing >= 0);
    }

    // ============================================================
    // المجموعة (د) — سلامة البيانات + النطاق + الصلاحيات
    // ============================================================

    // (18) تسليم تاريخيّ فعليّ في دورة ما قبل الأرضيّة يبقى مخزَّنًا ومقروءًا (لا حذف/إعادة كتابة رغم عدم الانطباق).
    [Fact]
    public async Task PreFloorCycle_HistoricalSubmission_StillStored_NoSyntheticDeletion()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupWeeklyAsync(uid, EarlyAnchor);
        var preKey = PreFloorCycleKey();
        await InsertAsync(vid, uid, preKey, SubmissionStatus.Submitted, Fixed.UtcDateTime);

        // الصفّ التاريخيّ باقٍ في القاعدة (قراءة مباشرة) رغم أنّ الدورة غير منطبقة في القراءة الإداريّة.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ReportSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SubmitterId == uid && s.PeriodKey == preKey && s.PeriodType == PeriodType.Weekly);
        Assert.NotNull(stored);
        Assert.Equal(SubmissionStatus.Submitted, stored!.Status);

        // ومع ذلك الدورة غير منطبقة (لا تُحذف البيانات، تُستبعَد من العدّ فقط).
        var r = await ResolveOneAsync(uid, preKey);
        Assert.False(r!.IsExpected);
        Assert.Equal(ExpectedSubmissionStatus.NotApplicable, r.Status);
    }

    // (19) الصلاحيات لم تُضعَّف بالأرضيّة: الموظّف=403 على compliance-summary لأسبوع ما قبل الأرضيّة، وغير المصادَق=401.
    [Fact]
    public async Task PreFloorWeek_Authorization_Unchanged_Employee403_Anonymous401()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var anon = _factory.CreateClient();
        var week = PreFloorWeekKey();
        var path = $"/api/reports/compliance-summary?weekKey={week}";
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
    }

    // ============================================================
    // أدوات مساعدة — المُحلِّل
    // ============================================================

    private static DateTime EarlyAnchor => Fixed.UtcDateTime.AddDays(-365); // قبل الأرضيّة بكثير

    private async Task<Guid> SetupWeeklyAsync(Guid userId, DateTime floorAnchor)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = null };
        db.JobRoles.Add(jobRole);

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
            PublishedAtUtc = floorAnchor
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        user.CreatedAtUtc = floorAnchor;
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task InsertAsync(Guid versionId, Guid submitterId, string key,
        SubmissionStatus status, DateTime? submittedAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = key,
            Status = status,
            SubmittedAtUtc = submittedAtUtc ?? (status == SubmissionStatus.Draft ? null : Fixed.UtcDateTime)
        });
        await db.SaveChangesAsync();
    }

    private async Task<ExpectedCycleResult?> ResolveOneAsync(Guid userId, string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new ExpectedSubmissionStatusResolver(db, new FixedClock(Fixed));
        var results = await resolver.ResolveAsync(new ExpectedStatusQuery(new[] { userId }, new[] { key }, null));
        return results.FirstOrDefault();
    }

    // ============================================================
    // أدوات مساعدة — الالتزام (Compliance API) بالساعة الحقيقيّة
    // ============================================================

    // أسبوع ما قبل الأرضيّة (بداية السبت 2026-06-27) — أسبوع منقضٍ + غير منطبق ⇒ لولا الأرضيّة لكان MissingOverdue.
    private static string PreFloorWeekKey() =>
        ReportCalendarPolicy.WeekKeyFor(Floor.AddDays(-7));
    // أوّل أسبوع منطبق = 2026-W28 (بداية السبت 2026-07-04) — أسبوع منقضٍ أيضًا ⇒ الغائب MissingOverdue.
    private static string FloorWeekKey() =>
        ReportCalendarPolicy.WeekKeyFor(Floor);

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
        DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(12, 0)).Add(-ReportCalendarPolicy.RiyadhOffset), DateTimeKind.Utc);

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

    private async Task<Guid> CreateWeeklyEmployeeAsync(Guid jobRoleId, Guid deptId)
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

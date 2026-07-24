using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// DAILY-REPORTING-APPLICABILITY-AND-UNIFIED-OVERDUE-R1 — مصفوفة الاختبارات الـ29 (القسم 12).
/// تُثبِت: أرضيّة الإطلاق المنظّميّة (4 يوليو 2026) تحكم اليوميّ أيضًا — لا توقّع/مفقود/متأخّر/عقوبة قبلها؛
/// بعدها تظهر كلّ التقارير اليوميّة المنطبقة (بما فيها المتأخّرة) في العرض الموحّد؛ التمييز بين الدورية
/// اليوميّة والأسبوعيّة بلا تحويل؛ Overdue يجمع النوعين؛ NeedsAction/MineApproval للتسليمات الفعليّة فقط؛
/// الصفوف المتوقّعة اليوميّة عرض-فقط؛ فلتر الدورية (All/Daily/Weekly)؛ عدم اتّساع النطاق؛ الحفاظ على
/// التسليمات اليوميّة التاريخيّة قبل الأرضيّة دون تغيير؛ إعادة استخدام السكيمة (لا هجرة). قراءة فقط.
///
/// الساعة الثابتة: الإثنين 2026-07-20 09:00Z (= 12:00 الرياض). الأرضيّة = السبت 2026-07-04.
/// دورات مؤهَّلة داخل النافذة: W28 (07-04→07-10)، W29 (07-11→07-17)، W30 (07-18→07-24، الجارية).
/// أيّام عمل W28 المنطبقة: الأحد 07-05 → الخميس 07-09 (السبت 07-04 والجمعة 07-10 خارج التقرير اليوميّ).
/// </summary>
[Collection("Integration")]
public class DailyApplicabilityUnifiedOverdueTests
{
    private readonly CustomWebApplicationFactory _factory;
    public DailyApplicabilityUnifiedOverdueTests(CustomWebApplicationFactory factory) => _factory = factory;

    // الإثنين 2026-07-20 12:00 الرياض (W30 الجارية) — بعد الأرضيّة بأسبوعين، فتصير W28/W29 ماضيتين متأخّرتين.
    private static readonly DateTimeOffset Fixed = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
    // مرساة مبكّرة قبل الأرضيّة لإنشاء المستخدم/نشر القالب (كي لا تحكم أرضيّةُ إنشاءِ المستخدم على الحساب).
    private static DateTime EarlyAnchor => new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    // مفاتيح الدورات (YYYY-Www) عبر سياسة التقويم المصدر.
    private static string W28Key => ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 7, 8));
    private static string W29Key => ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 7, 15));
    private static string W30Key => ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 7, 20));
    private static string PreFloorCycleKey => ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 7, 1)); // W27، كلّها قبل الأرضيّة.

    // مفاتيح الأيّام (yyyy-MM-dd).
    private const string Day05 = "2026-07-05"; // أحد W28، أوّل يوم عمل مؤهَّل بعد الأرضيّة.
    private const string Day07 = "2026-07-07"; // ثلاثاء W28، ماضٍ ⇒ متأخّر.
    private const string Day16 = "2026-07-16"; // خميس W29، ماضٍ يوم عمل ⇒ متأخّر.
    private const string Day19 = "2026-07-19"; // أحد W30، أمس ⇒ متأخّر.
    private const string Day20 = "2026-07-20"; // اليوم (الإثنين) ⇒ ليس متأخّرًا (لم ينتهِ يومه).
    private const string PreFloorDay = "2026-07-02"; // خميس قبل الأرضيّة ⇒ لا عقوبة/توقّع.

    private static readonly DateOnly Floor = ApplicabilityFloorPolicy.OrganizationalReportingLaunchFloor;

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        private readonly string[] _roles;
        public TestCurrentUser(Guid userId, params string[] roles) { UserId = userId; _roles = roles; }
        public Guid? UserId { get; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => _roles;
        public bool IsInRole(string role) => _roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => _roles.Intersect(roles).Any();
    }

    private sealed record Seeded(Guid TemplateId, Guid VersionId);

    /// <summary>يجعل المستخدم مطالَبًا بتقرير يوميّ عبر مسمّى مبيعات (SALES_B2C ⇒ ExpectedCadence=Daily)
    /// + قالب أساسي منشور مرتبط بالمسمّى (get-or-create لكليهما بسبب فهرس Code الفريد المشترك).</summary>
    private async Task<Seeded> SeedDailyExpectedAsync(Guid userId, Guid? teamId = null, Guid? departmentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = await db.JobRoles.FirstOrDefaultAsync(r => r.Code == "SALES_B2C");
        if (role is null)
        {
            role = new JobRole { NameAr = "مبيعات يوميّ", Code = "SALES_B2C", IsActive = true };
            db.JobRoles.Add(role);
            await db.SaveChangesAsync();
        }

        var template = await db.ReportTemplates.FirstOrDefaultAsync(
            t => t.JobRoleId == role.Id && t.Classification == TemplateClassification.Primary && t.IsActive);
        Guid versionId;
        if (template is null)
        {
            template = new ReportTemplate
            {
                Title = "قالب المبيعات اليومي",
                JobRoleId = role.Id,
                Classification = TemplateClassification.Primary,
                DefaultPeriodType = PeriodType.Weekly, // كما في الإنتاج: النوع الافتراضيّ أسبوعيّ لكنّ الدورية الفعليّة يوميّة بالرمز.
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
                PublishedAtUtc = EarlyAnchor
            };
            db.ReportTemplateVersions.Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }
        else
        {
            versionId = (await db.ReportTemplateVersions.FirstAsync(v => v.ReportTemplateId == template.Id && v.IsPublished)).Id;
        }

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = role.Id;
        user.CreatedAtUtc = EarlyAnchor;
        user.IsActive = true;
        if (teamId is Guid tid) user.TeamId = tid;
        if (departmentId is Guid did) user.DepartmentId = did;
        await db.SaveChangesAsync();
        return new Seeded(template.Id, versionId);
    }

    /// <summary>يجعل المستخدم مطالَبًا بتقرير أسبوعيّ (مسمّى بلا رمز ⇒ ExpectedCadence=Weekly، لا يُستبعَد).</summary>
    private async Task<Seeded> SeedWeeklyExpectedAsync(Guid userId, Guid? teamId = null, Guid? departmentId = null)
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
            PublishedAtUtc = EarlyAnchor
        };
        db.ReportTemplateVersions.Add(version);
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        user.CreatedAtUtc = EarlyAnchor;
        if (teamId is Guid tid) user.TeamId = tid;
        if (departmentId is Guid did) user.DepartmentId = did;
        await db.SaveChangesAsync();
        return new Seeded(template.Id, version.Id);
    }

    private async Task<Guid> CreateDepartmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        db.Set<Department>().Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<Guid> CreateTeamAsync(Guid departmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = departmentId, IsActive = true };
        db.Set<Team>().Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task InsertDailyAsync(Guid versionId, Guid submitterId, string dayKey, SubmissionStatus status,
        Guid? currentApproverId = null, Guid? teamId = null, Guid? departmentId = null)
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
            TeamId = teamId,
            DepartmentId = departmentId,
            CurrentApproverId = currentApproverId,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : Fixed.UtcDateTime
        });
        await db.SaveChangesAsync();
    }

    private async Task InsertWeeklyAsync(Guid versionId, Guid submitterId, string cycleKey, SubmissionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = cycleKey,
            Status = status,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : Fixed.UtcDateTime
        });
        await db.SaveChangesAsync();
    }

    private async Task<UnifiedSubmissionOverviewDto> OverviewAsync(Guid actorId, string[] roles, UnifiedSubmissionFilter filter)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = new FixedClock(Fixed);
        var currentUser = new TestCurrentUser(actorId, roles);
        var scopeResolver = new ScopeResolver(db, currentUser);
        var grants = new ReportViewGrantService(db, currentUser, scope.ServiceProvider.GetRequiredService<IAuditService>());
        var expected = new ExpectedSubmissionStatusResolver(db, clock);
        var svc = new SubmissionService(db, currentUser, null!, null!, scopeResolver, null!, null!, grants, expected, clock);
        var result = await svc.GetOverviewAsync(filter);
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);
        return result.Value!;
    }

    private static IEnumerable<UnifiedSubmissionRowDto> Daily(UnifiedSubmissionOverviewDto o, Guid uid) =>
        o.Items.Where(r => r.SubmitterId == uid && r.PeriodType == PeriodType.Daily);

    // ===== 1) يوم يوميّ قبل 2026-07-04 غير منطبق؛ يوم الأرضيّة فأكثر منطبق =====
    [Fact]
    public void D01_DailyDateBeforeFloor_NotApplicable()
    {
        Assert.False(ApplicabilityFloorPolicy.IsDailyDateApplicable(new DateOnly(2026, 7, 3), Floor));
        Assert.True(ApplicabilityFloorPolicy.IsDailyDateApplicable(new DateOnly(2026, 7, 4), Floor));
        Assert.True(ApplicabilityFloorPolicy.IsDailyDateApplicable(new DateOnly(2026, 7, 5), Floor));
    }

    // ===== 2) لا صفّ متوقّع يوميّ لدورة قبل الأرضيّة =====
    [Fact]
    public async Task D02_NoPreFloorDailyExpectedMissing()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PreFloorCycleKey));
        Assert.DoesNotContain(o.Items, r => r.PeriodType == PeriodType.Daily);
    }

    // ===== 3) لا «متأخّر مفقود» يوميّ قبل الأرضيّة =====
    [Fact]
    public async Task D03_NoPreFloorDailyMissingOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(PeriodKey: PreFloorCycleKey, QuickFilter: SubmissionQuickFilter.Overdue));
        Assert.Empty(o.Items);
        Assert.Equal(0, o.Summary.OverdueCount);
        Assert.Equal(0, o.Summary.MissingOverdueCount);
    }

    // ===== 4) لا عقوبة التزام يوميّة قبل الأرضيّة: تسليم يوميّ فعليّ قبلها يبقى مرئيًّا دون تأخّر =====
    [Fact]
    public async Task D04_NoPreFloorDailyCompliancePenalty()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, PreFloorDay, SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        var r = Assert.Single(o.Items, x => x.PeriodKey == PreFloorDay);
        Assert.False(r.IsOverdue); // قبل الأرضيّة ⇒ لا عقوبة تأخّر.
        var overdue = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.Overdue, Cadence: SubmissionCadenceFilter.Daily));
        Assert.DoesNotContain(overdue.Items, x => x.PeriodKey == PreFloorDay);
    }

    // ===== 5) 2026-07-04 يتبع سياسة يوم العمل: السبت (الأرضيّة) لا يُنتِج توقّعًا يوميًّا =====
    [Fact]
    public async Task D05_FloorSaturday_FollowsBusinessDayPolicy()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W28Key));
        Assert.DoesNotContain(Daily(o, uid), r => r.PeriodKey == "2026-07-04"); // السبت = الأرضيّة لكنّه عطلة.
        Assert.Equal(DayOfWeek.Saturday, new DateOnly(2026, 7, 4).DayOfWeek);
    }

    // ===== 6) أوّل يوم عمل مؤهَّل بعد الأرضيّة (الأحد 07-05) منطبق ويظهر =====
    [Fact]
    public async Task D06_FirstEligibleBusinessDayAfterFloor_Applicable()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W28Key));
        Assert.Contains(Daily(o, uid), r => r.PeriodKey == Day05);
    }

    // ===== 7) مفقود يوميّ منطبق قبل انتهاء يومه (اليوم) ⇒ ليس متأخّرًا =====
    [Fact]
    public async Task D07_ApplicableDailyMissing_BeforeDue_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W30Key));
        var today = Assert.Single(Daily(o, uid), r => r.PeriodKey == Day20);
        Assert.False(today.IsOverdue);
        Assert.Equal("لم يبدأ التقرير", today.StatusLabel);
        Assert.Equal(0, today.DelayDays);
    }

    // ===== 8) مفقود يوميّ منطبق بعد انتهاء يومه ⇒ متأخّر =====
    [Fact]
    public async Task D08_ApplicableDailyMissing_AfterDue_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W28Key));
        var past = Assert.Single(Daily(o, uid), r => r.PeriodKey == Day07);
        Assert.True(past.IsOverdue);
        Assert.Equal("متأخّر — لم يُقدَّم", past.StatusLabel);
        Assert.True(past.DelayDays > 0);
    }

    // ===== 9) مسودّة يوميّة فعليّة بعد انتهاء يومها ⇒ متأخّرة =====
    [Fact]
    public async Task D09_DailyDraft_AfterDue_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        var r = Assert.Single(o.Items, x => x.PeriodKey == Day16 && x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.True(r.IsOverdue);
        Assert.Equal("مسودّة متأخّرة", r.StatusLabel);
    }

    // ===== 10) مُعاد للتعديل يوميّ فعليّ بعد انتهاء يومه ⇒ متأخّر =====
    [Fact]
    public async Task D10_DailyReturned_AfterDue_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Returned);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        var r = Assert.Single(o.Items, x => x.PeriodKey == Day16 && x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.True(r.IsOverdue);
        Assert.Equal("مُعاد للتعديل — متأخّر", r.StatusLabel);
    }

    // ===== 11) تقرير يوميّ مُسلَّم يظهر في العرض الموحّد (وليس متأخّرًا) =====
    [Fact]
    public async Task D11_DailySubmitted_InUnifiedOverview()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Submitted);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        var r = Assert.Single(o.Items, x => x.PeriodKey == Day16 && x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.Equal(PeriodType.Daily, r.PeriodType);
        Assert.False(r.IsOverdue);
    }

    // ===== 12) تقرير يوميّ مُغلَق يظهر لكنّه خارج فلتر «المتأخّر» =====
    [Fact]
    public async Task D12_DailyClosed_Appears_ButNotInOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Closed);
        var all = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        Assert.Contains(all.Items, x => x.PeriodKey == Day16 && x.RowKind == SubmissionRowKind.ExistingSubmission);
        var overdue = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.Overdue, Cadence: SubmissionCadenceFilter.Daily));
        Assert.DoesNotContain(overdue.Items, x => x.PeriodKey == Day16 && x.RowKind == SubmissionRowKind.ExistingSubmission);
    }

    // ===== 13) الصفّ المتوقّع اليوميّ المفقود يظهر في العرض الموحّد =====
    [Fact]
    public async Task D13_DailyExpectedMissing_InUnifiedOverview()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        Assert.Contains(Daily(o, uid), r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
    }

    // ===== 14) الصفّ المتوقّع اليوميّ عرض-فقط (لا معرّف، ولا يدخل NeedsAction/MineApproval) =====
    [Fact]
    public async Task D14_DailyExpectedMissing_DisplayOnly()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W28Key));
        var expected = Daily(o, uid).First(r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
        Assert.Null(expected.SubmissionId);
        Assert.False(expected.HasSubmission);
        Assert.True(expected.IsExpectedSubmission);

        var needs = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.NeedsAction, Cadence: SubmissionCadenceFilter.Daily));
        Assert.DoesNotContain(needs.Items, r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
        var mine = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.MineApproval, Cadence: SubmissionCadenceFilter.Daily));
        Assert.DoesNotContain(mine.Items, r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
    }

    // ===== 15) QuickFilter=Overdue يجمع اليوميّ والأسبوعيّ معًا =====
    [Fact]
    public async Task D15_QuickFilterOverdue_CombinesDailyAndWeekly()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, dailyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, weeklyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedDailyExpectedAsync(dailyUser); // مفقود يوميّ متأخّر (أيّام W28/W29 الماضية).
        var sw = await SeedWeeklyExpectedAsync(weeklyUser);
        await InsertWeeklyAsync(sw.VersionId, weeklyUser, W28Key, SubmissionStatus.Draft); // مسودّة أسبوعيّة متأخّرة.
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.Overdue));
        Assert.Contains(o.Items, r => r.SubmitterId == dailyUser && r.PeriodType == PeriodType.Daily && r.IsOverdue);
        Assert.Contains(o.Items, r => r.SubmitterId == weeklyUser && r.PeriodType == PeriodType.Weekly && r.IsOverdue);
        Assert.All(o.Items, r => Assert.True(r.IsOverdue));
    }

    // ===== 16) فلتر الدورة يُرجِع كلّ أيّام العمل المنطبقة داخلها (W28 = 05..09) =====
    [Fact]
    public async Task D16_CycleFilter_ReturnsAllApplicableDailyDatesInCycle()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: W28Key));
        var days = Daily(o, uid).Select(r => r.PeriodKey).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "2026-07-05", "2026-07-06", "2026-07-07", "2026-07-08", "2026-07-09" }, days);
    }

    // ===== 17) Cadence=Daily يقصي الأسبوعيّ =====
    [Fact]
    public async Task D17_CadenceDaily_ExcludesWeekly()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, dailyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, weeklyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedDailyExpectedAsync(dailyUser);
        await SeedWeeklyExpectedAsync(weeklyUser);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.Equal(PeriodType.Daily, r.PeriodType));
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == weeklyUser);
    }

    // ===== 18) Cadence=Weekly يقصي اليوميّ =====
    [Fact]
    public async Task D18_CadenceWeekly_ExcludesDaily()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, dailyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, weeklyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedDailyExpectedAsync(dailyUser);
        await SeedWeeklyExpectedAsync(weeklyUser);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Weekly));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.Equal(PeriodType.Weekly, r.PeriodType));
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == dailyUser);
    }

    // ===== 19) Cadence=All يشمل النوعين بلا تكرار =====
    [Fact]
    public async Task D19_CadenceAll_IncludesBoth_NoDuplicates()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, dailyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, weeklyUser) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedDailyExpectedAsync(dailyUser);
        await SeedWeeklyExpectedAsync(weeklyUser);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.All, PageSize: 1000));
        Assert.Contains(o.Items, r => r.SubmitterId == dailyUser && r.PeriodType == PeriodType.Daily);
        Assert.Contains(o.Items, r => r.SubmitterId == weeklyUser && r.PeriodType == PeriodType.Weekly);
        var keys = o.Items.Select(r => (r.SubmitterId, r.PeriodType, r.PeriodKey)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ===== 20) Summary.Total == TotalCount (نفس المجموعة بعد QuickFilter، قبل الترقيم) =====
    [Fact]
    public async Task D20_SummaryTotal_EqualsTotalCount()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));
        Assert.Equal(o.Summary.Total, o.TotalCount);
    }

    // ===== 21) الترقيم لا يغيّر العدّادات =====
    [Fact]
    public async Task D21_Pagination_DoesNotChangeSummary()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        var big = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));
        var small = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, Page: 1, PageSize: 1));
        Assert.Equal(big.Summary.Total, small.Summary.Total);
        Assert.Equal(big.TotalCount, small.TotalCount);
        Assert.True(small.Items.Count <= 1);
    }

    // ===== 22) NeedsAction يشمل المسودّة/المُعاد اليوميّة الفعليّة لا المفقود اليوميّ =====
    [Fact]
    public async Task D22_NeedsAction_IncludesDailyDraft_NotDailyMissing()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.NeedsAction, Cadence: SubmissionCadenceFilter.Daily));
        Assert.Contains(o.Items, r => r.PeriodKey == Day16 && r.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.DoesNotContain(o.Items, r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
    }

    // ===== 23) MineApproval يشمل تسليمًا يوميًّا فعليًّا معتمِده الحاليّ = المستخدم =====
    [Fact]
    public async Task D23_MineApproval_IncludesDailyActualAssignedToUser()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, emp) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var s = await SeedDailyExpectedAsync(emp);
        await InsertDailyAsync(s.VersionId, emp, Day16, SubmissionStatus.Submitted, currentApproverId: tl);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(QuickFilter: SubmissionQuickFilter.MineApproval));
        Assert.Contains(o.Items, r => r.SubmitterId == emp && r.PeriodType == PeriodType.Daily
            && r.RowKind == SubmissionRowKind.ExistingSubmission && r.CurrentApproverId == tl);
    }

    // ===== 24) فلاتر الفريق/الإدارة/القالب/البحث تعمل مع اليوميّ =====
    [Fact]
    public async Task D24_TeamDeptTemplateSearchFilters_WorkForDaily()
    {
        var dept = await CreateDepartmentAsync();
        var teamX = await CreateTeamAsync(dept);
        var teamY = await CreateTeamAsync(dept);
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedDailyExpectedAsync(a, teamId: teamX, departmentId: dept);
        await SeedDailyExpectedAsync(b, teamId: teamY, departmentId: dept);

        var byTeam = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, TeamId: teamX));
        Assert.NotEmpty(byTeam.Items);
        Assert.All(byTeam.Items, r => Assert.Equal(a, r.SubmitterId));

        // القالب الفعّال الذي تحسمه الخدمة حتميًّا للمسمّى اليوميّ (OrderBy Title) — يُشتقّ من صفوف المستخدم
        // لتجنّب هشاشة قاعدة الاختبار المشتركة (قوالب Primary متعدّدة مرتبطة بنفس مسمّى SALES_B2C الفريد).
        var effectiveTemplateId = byTeam.Items.First(r => r.SubmitterId == a).ReportTemplateId;
        var byTemplate = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, ReportTemplateId: effectiveTemplateId, TeamId: teamX));
        Assert.NotEmpty(byTemplate.Items);
        Assert.All(byTemplate.Items, r => Assert.Equal(effectiveTemplateId, r.ReportTemplateId));

        var bySearch = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Search: "لا-يوجد-هذا-النصّ", Cadence: SubmissionCadenceFilter.Daily));
        Assert.Empty(bySearch.Items);
    }

    // ===== 25) النطاق لا يتّسع: قائد فريق يرى تابعيه فقط لا تابعي فريق آخر =====
    [Fact]
    public async Task D25_AuthorizationScope_DoesNotWiden()
    {
        var (_, tl1) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, tl2) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, mine) = await TestAuth.CreateUserAsync(_factory, "Employee", tl1);
        var (_, other) = await TestAuth.CreateUserAsync(_factory, "Employee", tl2);
        await SeedDailyExpectedAsync(mine);
        await SeedDailyExpectedAsync(other);
        var o = await OverviewAsync(tl1, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        Assert.Contains(o.Items, r => r.SubmitterId == mine);
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == other);
    }

    // ===== 26) تسليم يوميّ تاريخيّ قبل الأرضيّة يبقى قابلًا للقراءة دون تغيير =====
    [Fact]
    public async Task D26_HistoricalPreFloorDailySubmission_RemainsReadable()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, PreFloorDay, SubmissionStatus.Submitted);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));
        var r = Assert.Single(o.Items, x => x.PeriodKey == PreFloorDay);
        Assert.Equal(PeriodType.Daily, r.PeriodType);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.False(r.IsOverdue);
        Assert.Equal(nameof(SubmissionStatus.Submitted), r.Status);
    }

    // ===== 27) يوم عطلة/غير تقريريّ لا يُنتِج توقّعًا (لا جمعة ولا سبت) =====
    [Fact]
    public async Task D27_WeekendNonReportingDay_GeneratesNoExpectation()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedDailyExpectedAsync(uid);
        // نافذة تشمل W28+W29 ⇒ لا صفّ يوميّ يقع على جمعة/سبت إطلاقًا.
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));
        Assert.All(Daily(o, uid), r =>
        {
            var day = DateOnly.ParseExact(r.PeriodKey, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.True(day.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday));
        });
    }

    // ===== 28) سلوك الأسبوعيّ دون تغيير: مستخدم أسبوعيّ (بلا رمز) ما زال يُنتِج صفوفًا أسبوعيّة ولا يُستبعَد =====
    [Fact]
    public async Task D28_WeeklyBehavior_Unchanged()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedWeeklyExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PageSize: 1000));
        Assert.NotEmpty(o.Items);
        Assert.Contains(o.Items, r => r.PeriodType == PeriodType.Weekly && r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
        Assert.DoesNotContain(o.Items, r => r.PeriodType == PeriodType.Daily); // بلا رمز مبيعات ⇒ لا صفوف يوميّة.
    }

    // ===== 29) بلا تغيير سكيمة: تسليم يوميّ يُخزَّن ويُقرأ بالأعمدة القائمة (PeriodType/PeriodKey) دون هجرة =====
    [Fact]
    public async Task D29_NoSchemaChange_DailySubmissionRoundtrips()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Day16, SubmissionStatus.Submitted);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ReportSubmissions.AsNoTracking()
            .FirstAsync(x => x.SubmitterId == uid && x.PeriodKey == Day16);
        Assert.Equal(PeriodType.Daily, row.PeriodType);
        Assert.Equal(Day16, row.PeriodKey);
    }

    // ===== 30) إثبات الأداء وعدم N+1 للمسار اليوميّ — عدّ أوامر SQL تجريبيًّا عبر اعتراض، على مقياسين =====
    // يُثبِت: عدد أوامر SQL للعرض الموحّد على مسمّى يوميّ (SALES_B2C ⇒ BuildDailyExpectedMissingAsync) ثابت بنيويًّا
    // (لا يتضخّم مع عدد المستخدمين/الأيّام ⇒ لا N+1)، محدود بسقف صغير، وزمنه ضمن عتبة واقعيّة (< 5 ثوانٍ).
    private sealed class QueryCountingInterceptor : DbCommandInterceptor
    {
        private int _count;
        public int Count => System.Threading.Volatile.Read(ref _count);
        public void Reset() => System.Threading.Interlocked.Exchange(ref _count, 0);
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { System.Threading.Interlocked.Increment(ref _count); return base.ReaderExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { System.Threading.Interlocked.Increment(ref _count); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        { System.Threading.Interlocked.Increment(ref _count); return base.ScalarExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { System.Threading.Interlocked.Increment(ref _count); return base.ScalarExecutingAsync(command, eventData, result, cancellationToken); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        { System.Threading.Interlocked.Increment(ref _count); return base.NonQueryExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { System.Threading.Interlocked.Increment(ref _count); return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken); }
    }

    private sealed record DailyMeasurement(int QueryCount, long ElapsedMs, int PayloadBytes, int ItemCount, int TotalCount, int SummaryTotal);

    // يبني AppDbContext مُزوَّدًا باعتراض عدّ الأوامر ويشغّل GetOverviewAsync مقيسًا (بلا تعديل كود الإنتاج).
    private async Task<DailyMeasurement> MeasureDailyOverviewAsync(string connectionString, Guid actorId, string[] roles, UnifiedSubmissionFilter filter)
    {
        var counter = new QueryCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).AddInterceptors(counter).Options;
        await using var db = new AppDbContext(options);
        var clock = new FixedClock(Fixed);
        var currentUser = new TestCurrentUser(actorId, roles);
        var scopeResolver = new ScopeResolver(db, currentUser);
        var grants = new ReportViewGrantService(db, currentUser, null!); // قراءة فقط (لا تدقيق)
        var expected = new ExpectedSubmissionStatusResolver(db, clock);
        var svc = new SubmissionService(db, currentUser, null!, null!, scopeResolver, null!, null!, grants, expected, clock);

        counter.Reset();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.GetOverviewAsync(filter);
        sw.Stop();
        Assert.True(result.Succeeded, result.Error);
        var dto = result.Value!;
        var payload = JsonSerializer.SerializeToUtf8Bytes(dto);
        return new DailyMeasurement(counter.Count, sw.ElapsedMilliseconds, payload.Length, dto.Items.Count, dto.TotalCount, dto.Summary.Total);
    }

    [Fact]
    public async Task D30_Performance_DailyPath_NoNPlusOne_ConstantQueryCount_AcrossScales()
    {
        // سلسلة اتصال قاعدة الاختبار الدائمة (لبناء AppDbContext مُعترَض عليه خارج DI المصنع).
        string conn;
        using (var s = _factory.Services.CreateScope())
            conn = s.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;

        // قائدان منفصلان (نطاقان معزولان) لعزل مجموعتَي القياس داخل القاعدة المشتركة الدائمة.
        var (_, tlSmall) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, tlLarge) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");

        const int smallUsers = 10;
        const int largeUsers = 20;

        // كل مستخدم مسمّى يوميّ ⇒ صفوف متوقّعة يوميّة مفقودة عبر أيّام العمل المنطبقة داخل النافذة (بعد الأرضيّة).
        async Task SeedGroupAsync(Guid tl, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var (_, e) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
                var seed = await SeedDailyExpectedAsync(e);
                await InsertDailyAsync(seed.VersionId, e, Day16, SubmissionStatus.Submitted); // صفّ فعليّ + صفوف متوقّعة.
            }
        }
        await SeedGroupAsync(tlSmall, smallUsers);
        await SeedGroupAsync(tlLarge, largeUsers);

        var filter = new UnifiedSubmissionFilter(PeriodKey: null, Cadence: SubmissionCadenceFilter.Daily, PageSize: 2000);

        // إحماء (JIT/خطّة الاستعلام) حتى لا يلوّث القياسَ الزمنيَّ أوّلُ تشغيل.
        _ = await MeasureDailyOverviewAsync(conn, tlSmall, new[] { "TeamLeader" }, filter);

        var small = await MeasureDailyOverviewAsync(conn, tlSmall, new[] { "TeamLeader" }, filter);
        var large = await MeasureDailyOverviewAsync(conn, tlLarge, new[] { "TeamLeader" }, filter);

        // مجموعتان يوميّتان فعليّتان: صفوف > 0 وتتضاعف مع مضاعفة المستخدمين (تحقّق أنّ الحمل الفعليّ ازداد).
        Assert.True(small.TotalCount > 0);
        Assert.True(large.TotalCount > small.TotalCount);

        // ===== إثبات عدم N+1: عدد أوامر SQL ثابت رغم مضاعفة المستخدمين/الصفوف (10→20) =====
        Assert.Equal(small.QueryCount, large.QueryCount);
        // محدود بسقف صغير ثابت (نطاق + فعليّ + تسميات دفعيّة + مُحلِّل متوقّع + دفعات BuildDailyExpectedMissingAsync الخمس). ليس بدلالة الصفوف.
        Assert.True(large.QueryCount <= 25, $"عدد أوامر SQL للمسار اليوميّ تجاوز السقف الثابت: {large.QueryCount}");

        // ===== عتبة زمنيّة واقعيّة: ≥20 مستخدمًا يوميًّا في < 5 ثوانٍ =====
        Assert.True(large.ElapsedMs < 5000, $"العرض الموحّد اليوميّ للمجموعة الكبيرة استغرق {large.ElapsedMs}ms (> 5000ms).");

        // ===== تسجيل القياس (ملفّ يُقرأ لاحقًا للتقرير) — أرقام حتميّة + زمنيّة =====
        var metrics = new
        {
            path = "daily",
            usersSmall = smallUsers,
            usersLarge = largeUsers,
            unionRowsSmall = small.TotalCount,
            unionRowsLarge = large.TotalCount,
            sqlQueryCountSmall = small.QueryCount,
            sqlQueryCountLarge = large.QueryCount,
            sqlQueryCountConstant = small.QueryCount == large.QueryCount,
            elapsedMsSmall = small.ElapsedMs,
            elapsedMsLarge = large.ElapsedMs,
            payloadBytesSmall = small.PayloadBytes,
            payloadBytesLarge = large.PayloadBytes,
            rowsReturnedSmall = small.ItemCount,
            rowsReturnedLarge = large.ItemCount,
            summaryTotalSmall = small.SummaryTotal,
            summaryTotalLarge = large.SummaryTotal
        };
        try
        {
            System.IO.File.WriteAllText("/tmp/d30-daily-metrics.json",
                JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* التسجيل اختياريّ؛ لا يُفشِل الاختبار إن تعذّرت الكتابة */ }
    }

    // ===== 31) وحدة التطبيع: مفاتيح الإنتاج التاريخية تُطبَّع لليوم المنطقيّ الصحيح؛ غير القابل للتفسير ⇒ false =====
    // (القسم 8.1/8.2) — تحقّق مباشر لعقد ReportingCalendarPolicy.TryCanonicalDay على المفاتيح الفعليّة المكتشَفة.
    [Fact]
    public void D31_CanonicalDay_NormalizesLegacyProductionKeys()
    {
        // مفاتيح فعليّة من reporting_prod: 6-7-2026 (d-M-yyyy) و 2026-07-9 (yyyy-M-d).
        Assert.True(ReportingCalendarPolicy.TryCanonicalDay("6-7-2026", out var d1));
        Assert.Equal(new DateOnly(2026, 7, 6), d1); // الإثنين — يطابق تاريخ الإنشاء 2026-07-06.
        Assert.True(ReportingCalendarPolicy.TryCanonicalDay("2026-07-9", out var d2));
        Assert.Equal(new DateOnly(2026, 7, 9), d2); // الخميس — يطابق تاريخ الإنشاء 2026-07-09.
        Assert.True(ReportingCalendarPolicy.TryCanonicalDay("2026-07-06", out var d3));
        Assert.Equal(new DateOnly(2026, 7, 6), d3); // ISO القياسيّ يُطبَّع لنفسه.

        // مفاتيح غير قابلة للتفسير كيوم ⇒ false (لا تُفسَّر خطأً، ولا تُنتِج توقّعًا).
        Assert.False(ReportingCalendarPolicy.TryCanonicalDay("2026-W30", out _)); // مفتاح دورة أسبوعيّة.
        Assert.False(ReportingCalendarPolicy.TryCanonicalDay("2026-02-30", out _)); // تاريخ غير موجود.
        Assert.False(ReportingCalendarPolicy.TryCanonicalDay("بدون-تاريخ", out _)); // نصّ حرّ.
        Assert.False(ReportingCalendarPolicy.TryCanonicalDay(null, out _));
    }

    // ===== 32) مفتاح قديم فعليّ (6-7-2026) مُسلَّم ⇒ يظهر كتسليم قائم بمفتاحه الخام، غير متأخّر، =====
    // ولا يُولَّد له صفّ «متوقّع مفقود» مكرّر لليوم المنطقيّ 2026-07-06 (القسم 2/3، 8.3، 8.6).
    [Fact]
    public async Task D32_LegacyDailySubmitted_NoDuplicateExpected_RawKeyPreserved()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "6-7-2026", SubmissionStatus.Submitted); // مفتاح قديم، يوم منطقيّ 07-06.

        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));

        // الصفّ الفعليّ ظاهر بمفتاحه الخام (لا يُعاد كتابته).
        var actual = Assert.Single(Daily(o, uid), x => x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.Equal("6-7-2026", actual.PeriodKey);
        Assert.False(actual.IsOverdue); // مُسلَّم ⇒ خارج التأخّر.

        // لا صفّ «متوقّع مفقود» لليوم المنطقيّ 2026-07-06 لنفس المُرسِل (منع الازدواج على التاريخ المنطقيّ).
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == "2026-07-06");
    }

    // ===== 33) مفتاح قديم فعليّ (2026-07-9) مُعاد للتعديل ⇒ يظهر بمفتاحه الخام ومتأخّر (يوم عمل ماضٍ بعد الأرضيّة) =====
    // ولا يُولَّد له متوقّع مكرّر لليوم المنطقيّ 2026-07-09 (القسم 2، 8.7).
    [Fact]
    public async Task D33_LegacyDailyReturned_Overdue_ByCanonicalDay_NoDuplicateExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "2026-07-9", SubmissionStatus.Returned); // مفتاح قديم، يوم منطقيّ 07-09.

        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));

        var actual = Assert.Single(Daily(o, uid), x => x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.Equal("2026-07-9", actual.PeriodKey);
        Assert.True(actual.IsOverdue); // مُعاد + يوم عمل منطبق ماضٍ ⇒ متأخّر بحسب اليوم المنطقيّ.
        Assert.True(actual.DelayDays > 0);

        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == "2026-07-09");
    }

    // ===== 34) الازدواج على التاريخ المنطقيّ دقيق: مفتاح قديم يكبت متوقّعَ يومه فقط، وباقي أيّام العمل تبقى متوقّعة =====
    // (القسم 3.1/3.2) — legacy actual + ISO expected same day ⇒ Actual only، والأيّام الأخرى غير متأثّرة.
    [Fact]
    public async Task D34_CanonicalDedup_SuppressesOnlyItsOwnDay()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "6-7-2026", SubmissionStatus.Submitted); // يكبت متوقّع 2026-07-06 فقط.

        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));

        // يوم منطقيّ آخر (2026-07-07 ثلاثاء عمل ماضٍ) بلا تسليم ⇒ يبقى «متوقّع مفقود» (لم يُكبَت خطأً).
        Assert.Contains(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Day07);
        // ويومه هو وحده المكبوت.
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == "2026-07-06");
    }

    // ===== 35) مفتاح غير قابل للتفسير ⇒ لا يُخفى (يظهر تسليمًا قائمًا)، ولا يُنتِج «مفقودًا» آليًّا، ولا يعطب الطلب =====
    // (القسم 3.3، 8.5).
    [Fact]
    public async Task D35_UnparseableDailyKey_NotHidden_NoAutoMissing_NoCrash()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "بدون-تاريخ", SubmissionStatus.Submitted); // مفتاح لا يُفسَّر كيوم.

        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));

        // الصفّ الفعليّ يبقى مرئيًّا بمفتاحه الخام (لا يُخفى).
        var actual = Assert.Single(Daily(o, uid), x => x.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.Equal("بدون-تاريخ", actual.PeriodKey);
        Assert.False(actual.IsOverdue); // غير قابل للتفسير ⇒ لا تأخّر آليّ (محافِظ).
        // لا يُنتِج صفّ «متوقّع مفقود» له (لا يُطابِق أيّ يوم عمل قياسيّ).
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == "بدون-تاريخ");
    }

    // ===== 36) سجلّان فعليّان حقيقيّان لنفس اليوم المنطقيّ بصيغتين (2026-07-9 مُعاد + 2026-07-09 مسودّة) =====
    // كلاهما يظهر (لا دمج/حذف لبيانات تاريخية)، ولا يُولَّد صفّ «متوقّع» ثالث لذلك اليوم (القسم 3).
    [Fact]
    public async Task D36_TwoRealRecordsSameLogicalDay_BothShown_NoThirdExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "2026-07-9", SubmissionStatus.Returned); // سجلّ حقيقيّ 1، يوم منطقيّ 07-09.
        await InsertDailyAsync(s.VersionId, uid, "2026-07-09", SubmissionStatus.Draft);   // سجلّ حقيقيّ 2، نفس اليوم.

        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily));

        var existing = Daily(o, uid).Where(x => x.RowKind == SubmissionRowKind.ExistingSubmission).ToList();
        Assert.Equal(2, existing.Count); // كلا السجلّين الحقيقيّين ظاهر (لا دمج).
        Assert.Contains(existing, x => x.PeriodKey == "2026-07-9");
        Assert.Contains(existing, x => x.PeriodKey == "2026-07-09");
        // لا صفّ «متوقّع مفقود» ثالث لليوم المنطقيّ 07-09 (كبته أيٌّ من السجلّين الفعليّين).
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == "2026-07-09");
    }

    // ===== 37) سلامة العدّادات مع مفتاح قديم حاضر: Summary.Total == TotalCount، والترقيم لا يغيّر الملخّص (القسم 8.14/8.15) =====
    [Fact]
    public async Task D37_LegacyPresent_SummaryTotalEqualsTotalCount_PaginationInvariant()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "6-7-2026", SubmissionStatus.Submitted);
        await InsertDailyAsync(s.VersionId, uid, "2026-07-9", SubmissionStatus.Returned);

        var full = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, PageSize: 2000));
        Assert.Equal(full.TotalCount, full.Summary.Total); // الملخّص = عدد الصفوف بعد الفلترة.

        // صفحة أصغر: الملخّص والمجموع الكلّيّ ثابتان بغضّ النظر عن الترقيم.
        var paged = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(Cadence: SubmissionCadenceFilter.Daily, Page: 1, PageSize: 2));
        Assert.Equal(full.TotalCount, paged.TotalCount);
        Assert.Equal(full.Summary.Total, paged.Summary.Total);
        Assert.True(paged.Items.Count <= 2);
    }

    // ============================================================================
    // §6 (السطوح الخمسة الأخرى) — إثبات أنّ التطبيع الشرعيّ للمفتاح اليوميّ القديم مطبَّق أيضًا في:
    // ReportDueService / ReportingService / ReportCalendarService / ReportingCalendarCycleService /
    // ReportingAggregationService. الساعة الحقيقيّة 2026-07-24 ⇒ W28 كلّها ماضية (حتميّة). المفتاح
    // القديم 6-7-2026 (يوم منطقيّ 07-06) و 2026-07-9 (يوم منطقيّ 07-09) يجب أن يُطابَقا على اليوم
    // المنطقيّ لا على النصّ الخام (قبل الإصلاح كانا يُفسَّران خطأً أو يُسقَطان معجميًّا).
    // ============================================================================

    /// <summary>مراجع قالب B2C-حسب-الدورة المبذور (TemplateSeeder) + حقل الجدول — لاختبار مسار التجميع.</summary>
    private async Task<(Guid VersionId, Guid GridFieldId)> SeedB2cGridTemplateRefsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.ReportTemplates
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstAsync(t => t.Title == B2cByCourseReportSchema.TemplateTitle);
        var version = template.Versions.First(v =>
            v.Fields.Any(f => f.FieldType == FieldType.TableGrid && f.Label == B2cByCourseReportSchema.MainTableLabel));
        var gridField = version.Fields.First(f =>
            f.FieldType == FieldType.TableGrid && f.Label == B2cByCourseReportSchema.MainTableLabel);
        return (version.Id, gridField.Id);
    }

    /// <summary>يُدرِج تسليمًا يوميًّا فعليًّا (غير مسودّة) على قالب B2C مع قيمة جدول (string[][]) لصفٍّ واحد.</summary>
    private async Task InsertB2cGridDailyAsync(Guid versionId, Guid gridFieldId, Guid submitterId, string dayKey, string[] row)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sub = new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Daily,
            PeriodKey = dayKey,
            Status = SubmissionStatus.Submitted,
            SubmittedAtUtc = Fixed.UtcDateTime
        };
        db.ReportSubmissions.Add(sub);
        await db.SaveChangesAsync();
        db.SubmissionFieldValues.Add(new SubmissionFieldValue
        {
            ReportSubmissionId = sub.Id,
            TemplateFieldId = gridFieldId,
            ValueJson = JsonSerializer.Serialize(new[] { row })
        });
        await db.SaveChangesAsync();
    }

    // ===== 38) ReportDueService.MyStatusAsync (self) — المفتاح القديم 6-7-2026 يُحتسَب مُسلَّمًا =====
    // «سلّمت 1 من N يوم عمل» (لا صفر) بحسب اليوم المنطقيّ 07-06 داخل W28 (القسم 6).
    [Fact]
    public async Task D38_ReportDueService_MyStatus_LegacyDailyKey_CountedByCanonicalDay()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "6-7-2026", SubmissionStatus.Submitted); // يوم منطقيّ 07-06.

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = new TestCurrentUser(uid, "Employee");
        var clock = new FixedClock(Fixed);
        var svc = new ReportDueService(db, currentUser, new ScopeResolver(db, currentUser),
            new ExpectedSubmissionStatusResolver(db, clock));

        var r = await svc.MyStatusAsync(W28Key);
        Assert.True(r.Succeeded, r.Error);
        Assert.True(r.Value!.Expected);
        // لولا التطبيع لظهرت «سلّمت 0 من» (المفتاح القديم لا يساوي 2026-07-06 نصّيًّا).
        Assert.Contains("سلّمت 1 من", r.Value!.StatusLabel);
    }

    // ===== 39) ReportingService.SubmissionComplianceAsync — صفّ الشخص «سلّم 1 من N يوم» بحسب اليوم المنطقيّ =====
    [Fact]
    public async Task D39_ReportingService_Compliance_LegacyDailyKey_PersonRowByCanonicalDay()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, emp) = await TestAuth.CreateUserAsync(_factory, "Employee", tl); // مرؤوس مباشر ضمن نطاق القائد.
        var s = await SeedDailyExpectedAsync(emp);
        await InsertDailyAsync(s.VersionId, emp, "6-7-2026", SubmissionStatus.Submitted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = new TestCurrentUser(tl, "TeamLeader");
        var svc = new ReportingService(db, currentUser, new ScopeResolver(db, currentUser));

        var r = await svc.SubmissionComplianceAsync(W28Key, null, null);
        Assert.True(r.Succeeded, r.Error);
        var row = Assert.Single(r.Value!.Rows, x => x.UserId == emp);
        Assert.Contains("سلّم 1 من", row.StatusLabel); // لا «سلّم 0 من».
    }

    // ===== 40) ReportCalendarService.GetSalesDailyComplianceAsync — SubmittedDays==1 بحسب اليوم المنطقيّ =====
    [Fact]
    public async Task D40_ReportCalendarService_SalesDaily_LegacyDailyKey_SubmittedDayByCanonicalDay()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, emp) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var s = await SeedDailyExpectedAsync(emp);
        await InsertDailyAsync(s.VersionId, emp, "6-7-2026", SubmissionStatus.Submitted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = new TestCurrentUser(tl, "TeamLeader");
        var svc = new ReportCalendarService(db, currentUser, new ScopeResolver(db, currentUser));

        var r = await svc.GetSalesDailyComplianceAsync(W28Key);
        Assert.True(r.Succeeded, r.Error);
        var row = Assert.Single(r.Value!.Rows, x => x.UserId == emp);
        Assert.Equal(1, row.SubmittedDays); // لولا التطبيع لَكان 0 (سقوط معجميّ/تفسير خاطئ).
    }

    // ===== 41) ReportingCalendarCycleService.GetMyDaysAsync (self) — يوم 07-06 = «Submitted» بالمفتاح القديم =====
    [Fact]
    public async Task D41_ReportingCalendarCycle_MyDays_LegacyDailyKey_DayStatusSubmitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, "6-7-2026", SubmissionStatus.Submitted); // يوم منطقيّ 07-06.

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = new TestCurrentUser(uid, "Employee");
        var svc = new ReportingCalendarCycleService(currentUser, db, null!); // GetMyDaysAsync لا يستعمل unified.

        var r = await svc.GetMyDaysAsync("2026-07-06", 0, 0, null);
        Assert.True(r.Succeeded, r.Error);
        var day = Assert.Single(r.Value!.Days, d => d.DayKey == "2026-07-06");
        Assert.Equal("Submitted", day.Status); // لولا التطبيع لَظهر «متأخّر — لم يُرسَل».
    }

    // ===== 42) ReportingAggregationService.AggregateB2cByCourseAsync — المفتاح القديم 2026-07-9 داخل نطاق W28 =====
    // SubmissionsConsidered==1 (لا يُسقَط معجميًّا: 2026-07-9 يقع بعد 2026-07-31 لفظيًّا لكنّ يومه المنطقيّ 07-09).
    [Fact]
    public async Task D42_ReportingAggregation_B2cByCourse_LegacyDailyKey_ConsideredByCanonicalDay()
    {
        var (_, emp) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, ceo) = await TestAuth.CreateUserAsync(_factory, "CEO"); // SeesAll — لا حصر نطاق.
        var (vid, gfid) = await SeedB2cGridTemplateRefsAsync();
        await InsertB2cGridDailyAsync(vid, gfid, emp, "2026-07-9",
            new[] { "دورة اختبار", "8", "10", "5", "3", "2", "1", "1000", "1", "سبب" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = new TestCurrentUser(ceo, "CEO");
        var svc = new ReportingAggregationService(db, currentUser, new ScopeResolver(db, currentUser));

        // فلتر أسبوعيّ (W28) + عزل الموظّف (EmployeeId) لحتميّة العدّ داخل القاعدة المشتركة.
        var filter = new AggregationFilter(PeriodType.Weekly, W28Key, EmployeeId: emp);
        var r = await svc.AggregateB2cByCourseAsync(filter);
        Assert.True(r.Succeeded, r.Error);
        Assert.Equal(1, r.Value!.SubmissionsConsidered); // لولا التطبيع لَكان 0.
    }
}

using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Audit;
using Reporting.Application.Common;
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
/// SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1 — القسم 6: مصفوفة اختبارات العرض الموحّد
/// لـ<see cref="SubmissionService.GetOverviewAsync"/> بساعة ثابتة (ISystemClock مُثبَّت).
/// تُثبِت: ظهور «المتوقّع غير المُقدَّم» (non-starter، SubmissionId=null) بلا كتابة صناعيّة؛ إزالة التكرار
/// حين وجود تسليم فعليّ؛ حدّ التأخّر الصارم للقائم (Draft/Returned)؛ استبعاد Submitted/Closed/Escalated من التأخّر؛
/// QuickFilter=Overdue يُرجِع النوعين؛ OverdueCount=القائم+المتوقّع؛ الفلاتر تنطبق على الصفّ المتوقّع؛
/// النطاق؛ الترقيم على القائمة فقط دون العدّادات. قراءة فقط — لا تعديل/هجرة/كتابة إنتاج.
/// </summary>
[Collection("Integration")]
public class UnifiedSubmissionOverviewTests
{
    private readonly CustomWebApplicationFactory _factory;
    public UnifiedSubmissionOverviewTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ساعة ثابتة: الإثنين 2026-07-13 09:00Z (= 12:00 الرياض) — قبل موعد الموظّف (الأربعاء) للدورة الحاليّة.
    private static readonly DateTimeOffset Fixed = new(2026, 7, 13, 9, 0, 0, TimeSpan.Zero);
    private static DateTime EarlyAnchor => Fixed.UtcDateTime.AddDays(-180);

    private static string CurrentKey() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime));
    private static string PastKey() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime).AddDays(-21));
    // أسبوع تاريخيّ ثانٍ متمايز (‑6 أسابيع) لإثبات حصر النافذة بدورة واحدة عبر ثلاث فترات مختلفة.
    private static string PastKey2() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime).AddDays(-42));
    // أسبوع بعيد (‑20 أسبوعًا) خارج النافذة التاريخية المحدودة (12 أسبوعًا) لكنه بعد أرضية الانطباق ⇒ قابل للاختيار الصريح.
    private static string FarPastKey() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime).AddDays(-140));

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

    /// <summary>يجعل المستخدم مطالَبًا بتقرير أسبوعيّ عبر مسمّى + قالب أساسي منشور، ويضبط الفريق/الإدارة اختياريًّا.</summary>
    private async Task<Seeded> SeedExpectedAsync(Guid userId, Guid? teamId = null, Guid? departmentId = null)
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

    private async Task InsertAsync(Guid versionId, Guid submitterId, string key, SubmissionStatus status,
        Guid? teamId = null, Guid? departmentId = null, Guid? currentApproverId = null, DateTime? submittedAtUtc = null)
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
            TeamId = teamId,
            DepartmentId = departmentId,
            CurrentApproverId = currentApproverId,
            SubmittedAtUtc = submittedAtUtc ?? (status == SubmissionStatus.Draft ? null : Fixed.UtcDateTime)
        });
        await db.SaveChangesAsync();
    }

    /// <summary>يبني <see cref="SubmissionService"/> بساعة ثابتة ومستخدم حاليّ اختباريّ، ويشغّل العرض الموحّد.</summary>
    private async Task<UnifiedSubmissionOverviewDto> OverviewAsync(Guid actorId, string[] roles, UnifiedSubmissionFilter filter)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = new FixedClock(Fixed);
        var currentUser = new TestCurrentUser(actorId, roles);
        var scopeResolver = new ScopeResolver(db, currentUser);
        var grants = new ReportViewGrantService(db, currentUser, scope.ServiceProvider.GetRequiredService<IAuditService>());
        var expected = new ExpectedSubmissionStatusResolver(db, clock);
        // notifications/audit/access/templates غير مستخدمة في GetOverviewAsync (مسار قراءة فقط).
        var svc = new SubmissionService(db, currentUser, null!, null!, scopeResolver, null!, null!, grants, expected, clock);
        var result = await svc.GetOverviewAsync(filter);
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);
        return result.Value!;
    }

    private static UnifiedSubmissionRowDto Row(UnifiedSubmissionOverviewDto o, Guid submitterId) =>
        Assert.Single(o.Items, r => r.SubmitterId == submitterId);

    // ===== 1) المتوقّع غير المُقدَّم (دورة ماضية) ⇒ صفّ non-starter بلا معرّف تسليم، متأخّر =====
    [Fact]
    public async Task T01_MissingOverdue_NonStarter_Included_WithNullSubmissionId()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExpectedMissingSubmission, r.RowKind);
        Assert.Null(r.SubmissionId);
        Assert.False(r.HasSubmission);
        Assert.True(r.IsExpectedSubmission);
        Assert.True(r.IsOverdue);
        Assert.Equal("NotSubmitted", r.Status);
        Assert.Equal("متأخّر — لم يُقدَّم", r.StatusLabel);
    }

    // ===== 2) المتوقّع غير المُقدَّم (الدورة الحاليّة قبل الموعد) ⇒ non-starter غير متأخّر «لم يبدأ» =====
    [Fact]
    public async Task T02_MissingWithinDeadline_NonStarter_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExpectedMissingSubmission, r.RowKind);
        Assert.False(r.IsOverdue);
        Assert.Equal("لم يبدأ التقرير", r.StatusLabel);
        Assert.Equal(0, r.DelayDays);
    }

    // ===== 3) إزالة التكرار: وجود تسليم فعليّ لنفس (قالب،موظّف،فترة) ⇒ لا صفّ متوقّع =====
    [Fact]
    public async Task T03_Dedup_ActualExists_NoMissingRowForSameKey()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, CurrentKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.DoesNotContain(o.Items, x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
    }

    // ===== 4) مسودّة قائمة في دورة ماضية ⇒ متأخّرة (حدّ صارم) =====
    [Fact]
    public async Task T04_ExistingDraft_PastCycle_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.NotNull(r.SubmissionId);
        Assert.True(r.IsOverdue);
        Assert.Equal("مسودّة متأخّرة", r.StatusLabel);
    }

    // ===== 5) مسودّة قائمة في الدورة الحاليّة قبل الموعد ⇒ غير متأخّرة =====
    [Fact]
    public async Task T05_ExistingDraft_CurrentCycle_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, CurrentKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
        Assert.Equal("مسودّة", r.StatusLabel);
    }

    // ===== 6) مُعاد للتعديل في دورة ماضية ⇒ متأخّر =====
    [Fact]
    public async Task T06_ExistingReturned_PastCycle_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Returned);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.True(r.IsOverdue);
        Assert.Equal("مُعاد للتعديل — متأخّر", r.StatusLabel);
    }

    // ===== 7) مُسلَّم في دورة ماضية ⇒ ليس متأخّرًا (غير مؤهّل للتأخّر) =====
    [Fact]
    public async Task T07_Submitted_PastCycle_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Submitted);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
        Assert.Equal(0, o.Summary.OverdueCount);
    }

    // ===== 8) مُغلَق في دورة ماضية ⇒ ليس متأخّرًا =====
    [Fact]
    public async Task T08_Closed_PastCycle_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Closed);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
    }

    // ===== 9) مُصعَّد في دورة ماضية ⇒ ليس متأخّرًا (التأخّر للقائم = Draft/Returned فقط) =====
    [Fact]
    public async Task T09_Escalated_PastCycle_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Escalated);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
    }

    // ===== 10) QuickFilter=Overdue يُرجِع النوعين معًا (قائم متأخّر + متوقّع متأخّر) =====
    [Fact]
    public async Task T10_QuickFilterOverdue_ReturnsBothKinds()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft); // قائم متأخّر
        // b non-starter في دورة ماضية ⇒ متوقّع متأخّر
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Overdue));
        Assert.Contains(o.Items, r => r.SubmitterId == a && r.RowKind == SubmissionRowKind.ExistingSubmission && r.IsOverdue);
        Assert.Contains(o.Items, r => r.SubmitterId == b && r.RowKind == SubmissionRowKind.ExpectedMissingSubmission && r.IsOverdue);
        Assert.All(o.Items, r => Assert.True(r.IsOverdue));
    }

    // ===== 11) OverdueCount = القائم المتأخّر + المتوقّع المتأخّر =====
    [Fact]
    public async Task T11_OverdueCount_EqualsExistingPlusMissing()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.Equal(1, o.Summary.ExistingOverdueCount);
        Assert.Equal(1, o.Summary.MissingOverdueCount);
        Assert.Equal(o.Summary.ExistingOverdueCount + o.Summary.MissingOverdueCount, o.Summary.OverdueCount);
    }

    // ===== 12) الفلتر SubmitterId ينطبق على الصفّ المتوقّع =====
    [Fact]
    public async Task T12_Filter_SubmitterId_AppliesToMissingRows()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), SubmitterId: a));
        Assert.Single(o.Items);
        Assert.Equal(a, o.Items[0].SubmitterId);
    }

    // ===== 13) الفلتر TeamId ينطبق على الصفّ المتوقّع =====
    [Fact]
    public async Task T13_Filter_TeamId_AppliesToMissingRows()
    {
        var dept = await CreateDepartmentAsync();
        var teamX = await CreateTeamAsync(dept);
        var teamY = await CreateTeamAsync(dept);
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a, teamId: teamX);
        await SeedExpectedAsync(b, teamId: teamY);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), TeamId: teamX));
        Assert.Single(o.Items);
        Assert.Equal(a, o.Items[0].SubmitterId);
        Assert.Equal(teamX, o.Items[0].TeamId);
    }

    // ===== 14) الفلتر DepartmentId ينطبق على الصفّ المتوقّع =====
    [Fact]
    public async Task T14_Filter_DepartmentId_AppliesToMissingRows()
    {
        var deptX = await CreateDepartmentAsync();
        var deptY = await CreateDepartmentAsync();
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a, departmentId: deptX);
        await SeedExpectedAsync(b, departmentId: deptY);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), DepartmentId: deptX));
        Assert.Single(o.Items);
        Assert.Equal(a, o.Items[0].SubmitterId);
    }

    // ===== 15) الفلتر ReportTemplateId ينطبق على الصفّ المتوقّع =====
    [Fact]
    public async Task T15_Filter_ReportTemplateId_AppliesToMissingRows()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), ReportTemplateId: sa.TemplateId));
        Assert.Single(o.Items);
        Assert.Equal(a, o.Items[0].SubmitterId);
        Assert.Equal(sa.TemplateId, o.Items[0].ReportTemplateId);
    }

    // ===== 16) البحث بالاسم ينطبق على الصفّ المتوقّع =====
    [Fact]
    public async Task T16_Search_MatchesSubmitterName_OnMissingRows()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        // أسماء مستخدمي الاختبار كلّها «مستخدم Employee» ⇒ البحث بها يبقيهما، وبنصّ غير موجود يُقصيهما.
        var hit = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Search: "مستخدم"));
        Assert.Contains(hit.Items, r => r.SubmitterId == a);
        var miss = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Search: "لا-يوجد-هذا-النصّ"));
        Assert.DoesNotContain(miss.Items, r => r.SubmitterId == a || r.SubmitterId == b);
    }

    // ===== 17) النطاق: الموظّف يرى صفوفه فقط (own) =====
    [Fact]
    public async Task T17_Scope_Employee_SeesOnlyOwn()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, other) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        await SeedExpectedAsync(other);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.Equal(uid, r.SubmitterId));
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == other);
    }

    // ===== 18) النطاق: الأدمن (governance) يرى الجميع ويشمل المتوقّع =====
    [Fact]
    public async Task T18_Scope_Admin_SeesAll_IncludesExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Admin" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), SubmitterId: uid));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExpectedMissingSubmission, r.RowKind);
        Assert.True(r.IsOverdue);
    }

    // ===== 19) الترقيم ينطبق على القائمة فقط لا على العدّادات =====
    [Fact]
    public async Task T19_Pagination_AppliesToItemsOnly_NotCounts()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var (_, e) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
            await SeedExpectedAsync(e);
            ids.Add(e);
        }
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 1, PageSize: 2));
        Assert.Equal(2, o.Items.Count);
        Assert.Equal(5, o.TotalCount);
        Assert.Equal(5, o.Summary.Total);
        Assert.Equal(5, o.Summary.ExpectedMissingCount);
        Assert.Equal(5, o.Summary.MissingOverdueCount);
    }

    // ===== 20) وجود فلتر Status يُعطّل توليد الصفوف المتوقّعة =====
    [Fact]
    public async Task T20_StatusFilter_DisablesExpectedRows()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Status: SubmissionStatus.Draft));
        Assert.DoesNotContain(o.Items, r => r.RowKind == SubmissionRowKind.ExpectedMissingSubmission);
    }

    // ===== 21) فلتر PeriodKey يختار دورة التوقّع الفعّالة =====
    [Fact]
    public async Task T21_PeriodKeyFilter_SelectsThatCycleForExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Equal(PastKey(), r.PeriodKey);
        Assert.Equal(PastKey(), o.Summary.PeriodKey);
    }

    // ===== 22) QuickFilter=NeedsAction = التسليم الفعليّ القابل للإجراء فقط (لا المتوقّع مطلقًا) =====
    // العقد النهائيّ المعتمَد: ExpectedMissingSubmission لا يدخل NeedsAction (لا قبل الاستحقاق ولا بعده).
    [Fact]
    public async Task T22_NeedsAction_ActualActionableOnly_ExcludesMissing()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Returned); // قائم قابل للإجراء
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.NeedsAction));
        // القائم القابل للإجراء يظهر، المتوقّع غير المُقدَّم لا يظهر إطلاقًا، وكل صفّ له معرّف تسليم.
        Assert.Contains(o.Items, r => r.SubmitterId == a && r.RowKind == SubmissionRowKind.ExistingSubmission);
        Assert.DoesNotContain(o.Items, r => r.IsExpectedSubmission);
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == b);
        Assert.All(o.Items, r => Assert.NotNull(r.SubmissionId));
    }

    // ===== 23) QuickFilter=Returned يُرجِع القائم المُعاد فقط (لا المتوقّع) =====
    [Fact]
    public async Task T23_ReturnedQuickFilter_OnlyExistingReturned()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Returned);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Returned));
        var r = Assert.Single(o.Items);
        Assert.Equal(a, r.SubmitterId);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.Equal("Returned", r.Status);
    }

    // ===== 24) عقد الصفّ المتوقّع null-safe: بلا معرّف تسليم/معتمِد/وقت تسليم =====
    [Fact]
    public async Task T24_MissingRow_NullSafe_NoSubmissionIdOrApprover()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Null(r.SubmissionId);
        Assert.Null(r.CurrentApproverId);
        Assert.Null(r.SubmittedAtUtc);
        Assert.False(r.HasSubmission);
        Assert.True(r.IsExpectedSubmission);
    }

    // ============================================================================================
    // ADDENDUM — تكافؤ الملخّص وعدّادات الفترة (SUMMARY FILTER PARITY AND PERIOD COUNTERS)
    // يُثبِت أنّ العدّادات (Summary) تُحسَب من نفس مجموعة الصفوف المفلترة التي تُبنى منها القائمة (Items):
    // نطاق ⇒ فلاتر عاديّة (تشمل PeriodKey) ⇒ Search ⇒ QuickFilter ⇒ [عدّادات] ⇒ ترقيم. مصدر واحد، بلا
    // استعلام ملخّص منفصل، فيستحيل بنيويًّا أن «تثبت البطاقات» عند تغيّر الفترة أو QuickFilter. القرار الوظيفيّ
    // المعتمد: العدّادات والبطاقات تُحسب بعد QuickFilter (W30 + Overdue ⇒ أرقام وصفوف متأخّري W30 فقط).
    // قراءة فقط — لا كتابة/هجرة/إنتاج.
    // ============================================================================================

    // ===== 25) العدّادات تتغيّر عند تغيّر PeriodKey (نفس الفلاتر عدا الفترة) =====
    [Fact]
    public async Task T25_Summary_Changes_When_PeriodKey_Changes()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft); // مسودّة قائمة متأخّرة في الماضي

        var past = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var current = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));

        // الماضي: مسودّة قائمة متأخّرة ⇒ متأخّر=1؛ الحاليّة: لا مسودّة (فُلترت بالفترة) والمتوقّع ضمن المهلة ⇒ متأخّر=0.
        Assert.Equal(1, past.Summary.OverdueCount);
        Assert.Equal(0, current.Summary.OverdueCount);
        Assert.NotEqual(past.Summary.OverdueCount, current.Summary.OverdueCount);
        Assert.Equal(PastKey(), past.Summary.PeriodKey);
        Assert.Equal(CurrentKey(), current.Summary.PeriodKey);
    }

    // ===== 26) عدّادات دورة محدّدة تختلف عن النافذة التاريخية المحدودة عند اختلاف البيانات =====
    [Fact]
    public async Task T26_Summary_SpecificCycle_Differs_From_BoundedWindow()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);

        var past = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var all = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null));

        // الفترة المحدّدة (PastKey): القائم PastKey (المتوقّع مُزال بالتوحيد) ⇒ Total=1، PeriodKey=PastKey.
        Assert.Equal(1, past.Summary.Total);
        Assert.Equal(PastKey(), past.Summary.PeriodKey);

        // بلا فترة: النافذة = آخر 12 أسبوعًا. أسبوع PastKey يظهر بالقائم (توحيد) والأحد عشر الباقية متوقّعة مفقودة
        //   ⇒ Total=12، PeriodKey=null (النطاق ليس دورة واحدة).
        Assert.Equal(12, all.Summary.Total);
        Assert.Null(all.Summary.PeriodKey);
        Assert.NotEqual(past.Summary.Total, all.Summary.Total);
    }

    // ===== 27) الفترة تقود القائمة والعدّادات بشكل متطابق (بلا QuickFilter، بلا ترقيم) =====
    [Fact]
    public async Task T27_Period_Drives_List_And_Summary_Identically()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);

        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), PageSize: 500));

        // العدّادات مشتقّة من نفس مجموعة الصفوف المعروضة (لا صفحة جزئيّة ولا QuickFilter).
        Assert.Equal(o.Items.Count, o.Summary.Total);
        Assert.Equal(o.Items.Count(x => x.IsOverdue), o.Summary.OverdueCount);
        Assert.Equal(o.Items.Count(x => x.IsExpectedSubmission), o.Summary.ExpectedMissingCount);
        Assert.Equal(o.Items.Count(x => !x.IsExpectedSubmission && x.IsOverdue), o.Summary.ExistingOverdueCount);
        Assert.Equal(o.Items.Count(x => x.IsExpectedSubmission && x.IsOverdue), o.Summary.MissingOverdueCount);
    }

    // ===== 28) Team + Period يتركّبان بشكل متطابق في القائمة والعدّادات =====
    [Fact]
    public async Task T28_Team_And_Period_Compose_Identically()
    {
        var dept = await CreateDepartmentAsync();
        var teamX = await CreateTeamAsync(dept);
        var teamY = await CreateTeamAsync(dept);
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a, teamId: teamX);
        await SeedExpectedAsync(b, teamId: teamY);

        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), TeamId: teamX, PageSize: 500));

        Assert.All(o.Items, r => Assert.Equal(teamX, r.TeamId));
        Assert.Equal(1, o.Summary.Total);
        Assert.Equal(o.Items.Count, o.Summary.Total); // القائمة والعدّادات على نفس المجموعة المفلترة (teamX+الفترة)
    }

    // ===== 29) Template + Period يتركّبان بشكل متطابق =====
    [Fact]
    public async Task T29_Template_And_Period_Compose_Identically()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);

        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), ReportTemplateId: sa.TemplateId, PageSize: 500));

        Assert.All(o.Items, r => Assert.Equal(sa.TemplateId, r.ReportTemplateId));
        Assert.Equal(1, o.Summary.Total);
        Assert.Equal(o.Items.Count, o.Summary.Total);
    }

    // ===== 30) QuickFilter + Period يتركّبان بشكل متطابق: العدّادات والقائمة كلاهما بعد QuickFilter =====
    [Fact]
    public async Task T30_QuickFilter_And_Period_Compose_Identically()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, c) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        var sc = await SeedExpectedAsync(c);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);     // قائم متأخّر
        await InsertAsync(sc.VersionId, c, PastKey(), SubmissionStatus.Submitted); // قائم غير متأخّر
        // b متوقّع متأخّر (non-starter في الماضي).

        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Overdue, PageSize: 500));

        // القرار الوظيفيّ المعتمد: العدّادات والقائمة كلاهما يُشتقّان من نفس المجموعة بعد QuickFilter=Overdue.
        // W-past + Overdue ⇒ صفوف وأرقام متأخّري الفترة فقط (a قائم متأخّر + b متوقّع متأخّر). c غير متأخّر مستبعَد.
        Assert.Equal(2, o.Summary.Total);
        Assert.Equal(2, o.Summary.OverdueCount);
        Assert.Equal(2, o.Items.Count);
        Assert.All(o.Items, r => Assert.True(r.IsOverdue));
        // العدّادات == القائمة الكاملة بعد QuickFilter (مصدر واحد، لا استعلام ملخّص منفصل).
        Assert.Equal(o.Items.Count, o.Summary.Total);
        Assert.Equal(o.TotalCount, o.Summary.Total);
    }

    // ===== 31) الترقيم لا يغيّر العدّادات (نفس الفلاتر، صفحات مختلفة) =====
    [Fact]
    public async Task T31_Pagination_Does_Not_Change_Summary()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        for (var i = 0; i < 5; i++)
        {
            var (_, e) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
            await SeedExpectedAsync(e);
        }

        var p1 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 1, PageSize: 2));
        var p2 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 2, PageSize: 2));

        Assert.Equal(p1.Summary.Total, p2.Summary.Total);
        Assert.Equal(p1.Summary.OverdueCount, p2.Summary.OverdueCount);
        Assert.Equal(p1.Summary.ExpectedMissingCount, p2.Summary.ExpectedMissingCount);
        Assert.Equal(p1.Summary.MissingOverdueCount, p2.Summary.MissingOverdueCount);
        Assert.Equal(5, p1.Summary.Total);
    }

    // ===== 32) إزالة الفترة تستعيد عدّادات «كل الفترات» (تشمل صفوفًا عبر فترات مختلفة) =====
    [Fact]
    public async Task T32_ClearingPeriod_Restores_AllPeriods_Summary()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft); // قائم متأخّر في الماضي

        var current = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var all = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null));

        // بلا فلتر فترة: يظهر القائم PastKey (عبر الفترات) فيرتفع الإجمالي ويظهر المتأخّر القائم.
        Assert.True(all.Summary.Total > current.Summary.Total);
        Assert.Equal(1, all.Summary.ExistingOverdueCount);
        Assert.Equal(0, current.Summary.ExistingOverdueCount);
    }

    // ===== 33) فترة غير صالحة ⇒ نتيجة فارغة حسب العقد (لا أرقام النطاق العالميّ) =====
    [Fact]
    public async Task T33_InvalidPeriod_Returns_Empty_NotGlobalNumbers()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft); // بيانات عالميّة موجودة

        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: "INVALID-CYCLE-KEY", PageSize: 500));

        // القائم مُقيَّد بمطابقة المفتاح الحرفيّ (لا مطابقة) والمتوقّع مُعطَّل (مفتاح غير دورة) ⇒ فارغ.
        Assert.Empty(o.Items);
        Assert.Equal(0, o.TotalCount);
        Assert.Equal(0, o.Summary.Total);
        Assert.Equal(0, o.Summary.OverdueCount);
        Assert.Equal(0, o.Summary.ExpectedMissingCount);
    }

    // ===== 34) TotalCount للقائمة = Total للعدّادات لنفس الفلاتر (بلا QuickFilter) =====
    [Fact]
    public async Task T34_ListTotalCount_Equals_SummaryTotal_SameFilters()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);

        // بلا ترقيم جزئيّ.
        var full = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), PageSize: 500));
        Assert.Equal(full.Summary.Total, full.TotalCount);

        // مع ترقيم جزئيّ: TotalCount (قبل الترقيم) يبقى = Summary.Total.
        var paged = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 1, PageSize: 1));
        Assert.Equal(paged.Summary.Total, paged.TotalCount);
        Assert.Single(paged.Items);
    }

    // ===== 35) نافذة «المتوقّع المفقود» محصورة بدورة واحدة عبر: أسبوع حاليّ + أسبوعين تاريخيين متمايزين =====
    // يُثبت العقد Q1: كل مفتاح فترة صالح يحصر الصفوف المتوقّعة بدورته حصرًا (لا نزف عبر الدورات، لا ازدواج).
    // الحاليّ (قبل الموعد) ⇒ متوقّع غير متأخّر؛ التاريخيّان (‑3 و‑6 أسابيع) ⇒ متوقّع + متأخّر.
    [Fact]
    public async Task T35_ExpectedMissing_SingleCycleBounded_Current_And_TwoHistoricalWeeks()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a);

        var curKey = CurrentKey();
        var w3Key = PastKey();
        var w6Key = PastKey2();
        // الفترات الثلاث متمايزة فعليًّا (حاليّ ≠ ‑3 ≠ ‑6).
        Assert.Equal(3, new[] { curKey, w3Key, w6Key }.Distinct().Count());

        // (أ) الأسبوع الحاليّ: صفّ متوقّع واحد، غير متأخّر (الموعد لم يفت)، محصور بالدورة الحاليّة.
        var cur = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: curKey, PageSize: 500));
        var curRow = Assert.Single(cur.Items);
        Assert.True(curRow.IsExpectedSubmission);
        Assert.Null(curRow.SubmissionId);
        Assert.False(curRow.IsOverdue);
        Assert.Equal(curKey, curRow.PeriodKey);
        Assert.Equal(curKey, cur.Summary.PeriodKey);
        Assert.Equal(1, cur.Summary.ExpectedMissingCount);
        Assert.Equal(0, cur.Summary.MissingOverdueCount);

        // (ب) الأسبوع التاريخيّ الأول (‑3): صفّ متوقّع واحد متأخّر، محصور بدورته.
        var w3 = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: w3Key, PageSize: 500));
        var w3Row = Assert.Single(w3.Items);
        Assert.True(w3Row.IsExpectedSubmission);
        Assert.Null(w3Row.SubmissionId);
        Assert.True(w3Row.IsOverdue);
        Assert.Equal(w3Key, w3Row.PeriodKey);
        Assert.Equal(w3Key, w3.Summary.PeriodKey);
        Assert.Equal(1, w3.Summary.ExpectedMissingCount);
        Assert.Equal(1, w3.Summary.MissingOverdueCount);

        // (ج) الأسبوع التاريخيّ الثاني (‑6): صفّ متوقّع واحد متأخّر، محصور بدورته (لا يظهر صفّ الأسبوع ‑3).
        var w6 = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: w6Key, PageSize: 500));
        var w6Row = Assert.Single(w6.Items);
        Assert.True(w6Row.IsExpectedSubmission);
        Assert.Null(w6Row.SubmissionId);
        Assert.True(w6Row.IsOverdue);
        Assert.Equal(w6Key, w6Row.PeriodKey);
        Assert.Equal(w6Key, w6.Summary.PeriodKey);
        Assert.Equal(1, w6.Summary.ExpectedMissingCount);
        Assert.Equal(1, w6.Summary.MissingOverdueCount);

        // لا ازدواج/نزف عبر الدورات: كل استعلام أرجع صفًّا واحدًا فقط بمفتاح دورته لا صفوف الدورات الأخرى.
        Assert.Equal(w3Key, Assert.Single(w3.Items).PeriodKey);
        Assert.NotEqual(w3.Items[0].PeriodKey, w6.Items[0].PeriodKey);
    }

    // ===== 36) النافذة التاريخية المحدودة (بلا فترة) تُجمِّع المتوقّع المفقود عبر آخر 12 أسبوعًا =====
    // العقد B: عند عدم اختيار فترة، تُحسَب الصفوف المتوقّعة عبر آخر 12 دورة شاملةً الحاليّة (لا «كل الفترات» بلا حدّ).
    // الحاليّ غير متأخّر (الموعد لم يفت) والإحدى عشرة السابقة متأخّرة. PeriodKey في الملخّص = null (النطاق ليس دورة واحدة).
    [Fact]
    public async Task T36_BoundedWindow_NoFilter_Aggregates_Last12Weeks_ExpectedMissing()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a); // أرضية الانطباق = EarlyAnchor (‑180 يومًا) ⇒ تغطّي كل النافذة.

        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500));

        // 12 صفًّا متوقّعًا مفقودًا (بلا معرّف تسليم) بمفاتيح 12 دورة متمايزة.
        Assert.Equal(12, o.Summary.Total);
        Assert.Equal(12, o.Summary.ExpectedMissingCount);
        Assert.All(o.Items, r => Assert.True(r.IsExpectedSubmission));
        Assert.All(o.Items, r => Assert.Null(r.SubmissionId));
        Assert.Equal(12, o.Items.Select(r => r.PeriodKey).Distinct().Count());
        // الملخّص للنطاق: PeriodKey=null، والحاليّ يجب أن يكون ضمن النافذة.
        Assert.Null(o.Summary.PeriodKey);
        Assert.Contains(CurrentKey(), o.Items.Select(r => r.PeriodKey));
        // متأخّر = 11 (كل السابقة)، والحاليّ غير متأخّر.
        Assert.Equal(11, o.Summary.MissingOverdueCount);
        Assert.Single(o.Items, r => r.PeriodKey == CurrentKey() && !r.IsOverdue);
    }

    // ===== 37) النافذة تستبعد أسبوعًا أقدم من 12 أسبوعًا، لكنه يبقى قابلًا للاختيار الصريح =====
    [Fact]
    public async Task T37_BoundedWindow_ExcludesBeyond12Weeks_ButExplicitSelectionReachesIt()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a);

        var farKey = FarPastKey(); // ‑20 أسبوعًا: خارج النافذة (12) وبعد الأرضية (‑180 يومًا).

        // (أ) بلا فترة: النافذة 12 أسبوعًا فقط ⇒ الأسبوع البعيد لا يظهر إطلاقًا.
        var window = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500));
        Assert.DoesNotContain(farKey, window.Items.Select(r => r.PeriodKey));
        Assert.Equal(12, window.Summary.Total);

        // (ب) اختيار الأسبوع البعيد صراحةً ⇒ يظهر صفّ متوقّع مفقود واحد بمفتاحه (الأرضية تغطّيه).
        var explicitFar = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: farKey, PageSize: 500));
        var row = Assert.Single(explicitFar.Items);
        Assert.True(row.IsExpectedSubmission);
        Assert.Null(row.SubmissionId);
        Assert.Equal(farKey, row.PeriodKey);
        Assert.Equal(farKey, explicitFar.Summary.PeriodKey);
        Assert.Equal(1, explicitFar.Summary.ExpectedMissingCount);
    }

    // ===== 38) داخل النافذة: Actual + Missing بلا ازدواج (أسبوع فيه تسليم فعليّ يظهر فعليًّا فقط) =====
    [Fact]
    public async Task T38_BoundedWindow_ActualPlusMissing_NoDoubleCount()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Submitted); // تسليم فعليّ داخل النافذة (‑3 أسابيع)

        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500));

        // 12 أسبوعًا: أسبوع PastKey يظهر بالتسليم الفعليّ (توحيد يزيل المتوقّع)، و11 أسبوعًا متوقّعة مفقودة.
        Assert.Equal(12, o.Summary.Total);
        Assert.Equal(11, o.Summary.ExpectedMissingCount);
        // صفّ PastKey واحد فقط، فعليّ لا متوقّع (لا ازدواج على نفس الدورة).
        var pastRows = o.Items.Where(r => r.PeriodKey == PastKey()).ToList();
        var pastRow = Assert.Single(pastRows);
        Assert.False(pastRow.IsExpectedSubmission);
        Assert.NotNull(pastRow.SubmissionId);
        // إجماليّ الصفوف = 12 مفتاحًا متمايزًا (لا صفّان لنفس الدورة).
        Assert.Equal(12, o.Items.Select(r => r.PeriodKey).Distinct().Count());
    }

    // ===== 39) الأداء محدود: عدّة مستخدمين × نافذة ثابتة ⇒ ExpectedMissing = مستخدمون × أسابيع، بزمن محدود =====
    // عدد استعلامات المُحلِّل ثابت بنيويًّا (دفعيّ عبر keys.Contains) مهما اتّسعت النافذة أو عدد المستخدمين.
    [Fact]
    public async Task T39_BoundedWindow_Performance_ConstantQueryShape_ScalesLinearly()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        for (var i = 0; i < 4; i++)
        {
            var (_, e) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
            await SeedExpectedAsync(e);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500));
        sw.Stop();

        // 4 مستخدمين × 12 أسبوعًا = 48 صفًّا متوقّعًا مفقودًا، منها 44 متأخّرة (11 أسبوعًا سابقًا لكلٍّ) و4 حاليّة غير متأخّرة.
        Assert.Equal(48, o.Summary.Total);
        Assert.Equal(48, o.Summary.ExpectedMissingCount);
        Assert.Equal(44, o.Summary.MissingOverdueCount);
        Assert.Null(o.Summary.PeriodKey);
        // حارس أداء محدود (ليس قياسًا دقيقًا): شكل الاستعلام ثابت، فالزمن يجب أن يبقى ضمن سقف سخيّ.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"استغرق العرض الموحّد وقتًا مفرطًا: {sw.Elapsed}");
    }

    // ===== 40) عقد QuickFilter (القرار المعتمد) — البطاقات (Summary) تتبع كل أبعاد النطاق الستّة وأيضًا QuickFilter =====
    // يُثبِت رسميًّا: تغيير Period/Team/Department/Submitter/Template/Search يُغيّر العدّادات؛ وكذلك QuickFilter يُغيّر
    // العدّادات (تُحسَب بعد QuickFilter على نفس المجموعة التي تُبنى منها القائمة) ⇒ Summary == TotalCount دائمًا.
    [Fact]
    public async Task T40_QuickFilterContract_ScopeDimensionsAndQuickFilterAllChangeCards()
    {
        var deptA = await CreateDepartmentAsync();
        var deptB = await CreateDepartmentAsync();
        var teamA = await CreateTeamAsync(deptA);
        var teamB = await CreateTeamAsync(deptB);
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, c) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a, teamId: teamA, departmentId: deptA); // متوقّع متأخّر (ماضٍ)
        await SeedExpectedAsync(b, teamId: teamB, departmentId: deptB);           // متوقّع متأخّر (ماضٍ)
        var sc = await SeedExpectedAsync(c, teamId: teamB, departmentId: deptB);
        // c: تسليم فعليّ مُسلَّم (غير متأخّر) في نفس الدورة ⇒ يُزال المتوقّع بالتوحيد، ويبقى صفًّا غير متأخّر.
        await InsertAsync(sc.VersionId, c, PastKey(), SubmissionStatus.Submitted, teamId: teamB, departmentId: deptB);

        var pk = PastKey();

        // خطّ الأساس: الفترة الماضية بلا فلتر إضافيّ ⇒ 3 صفوف (a متوقّع، b متوقّع، c فعليّ)، منها 2 متأخّران.
        var baseline = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, PageSize: 500));
        Assert.Equal(3, baseline.Summary.Total);
        Assert.Equal(2, baseline.Summary.OverdueCount);
        Assert.Equal(pk, baseline.Summary.PeriodKey);

        // (1) Period يُغيّر البطاقات: الدورة الحاليّة ⇒ a,b,c متوقّعون غير متأخّرين (c الفعليّ كان في PastKey فيُتوقَّع الآن)
        //     ⇒ العدّاد المتأخّر ينتقل 2→0 (البطاقات تغيّرت بتغيّر الفترة رغم بقاء الإجمالي 3).
        var byPeriod = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), PageSize: 500));
        Assert.Equal(0, byPeriod.Summary.OverdueCount);
        Assert.NotEqual(baseline.Summary.OverdueCount, byPeriod.Summary.OverdueCount);
        Assert.Equal(CurrentKey(), byPeriod.Summary.PeriodKey);
        Assert.NotEqual(baseline.Summary.PeriodKey, byPeriod.Summary.PeriodKey);

        // (2) Team يُغيّر البطاقات: teamA ⇒ a فقط.
        var byTeam = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, TeamId: teamA, PageSize: 500));
        Assert.Equal(1, byTeam.Summary.Total);
        Assert.NotEqual(baseline.Summary.Total, byTeam.Summary.Total);

        // (3) Department يُغيّر البطاقات: deptA ⇒ a فقط.
        var byDept = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, DepartmentId: deptA, PageSize: 500));
        Assert.Equal(1, byDept.Summary.Total);
        Assert.NotEqual(baseline.Summary.Total, byDept.Summary.Total);

        // (4) Submitter يُغيّر البطاقات: a فقط.
        var bySubmitter = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, SubmitterId: a, PageSize: 500));
        Assert.Equal(1, bySubmitter.Summary.Total);
        Assert.NotEqual(baseline.Summary.Total, bySubmitter.Summary.Total);

        // (5) Template يُغيّر البطاقات: قالب a فقط.
        var byTemplate = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, ReportTemplateId: sa.TemplateId, PageSize: 500));
        Assert.Equal(1, byTemplate.Summary.Total);
        Assert.NotEqual(baseline.Summary.Total, byTemplate.Summary.Total);

        // (6) Search يُغيّر البطاقات: البحث بعنوان قالب a (فريد) ⇒ صفّ a فقط.
        var aTitle = await TemplateTitleAsync(sa.TemplateId);
        var bySearch = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: pk, Search: aTitle, PageSize: 500));
        Assert.Equal(1, bySearch.Summary.Total);
        Assert.NotEqual(baseline.Summary.Total, bySearch.Summary.Total);

        // (7) QuickFilter أيضًا يُغيّر البطاقات: Overdue ⇒ Summary والقائمة كلاهما ينكمش إلى المتأخّرَين (2/2).
        var byQuick = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: pk, QuickFilter: SubmissionQuickFilter.Overdue, PageSize: 500));
        Assert.Equal(2, byQuick.Summary.Total);                                // البطاقات انكمشت (3→2)
        Assert.Equal(2, byQuick.Summary.OverdueCount);                         // المتأخّرون فقط
        Assert.NotEqual(baseline.Summary.Total, byQuick.Summary.Total);        // QuickFilter غيّر البطاقات (3→2)
        Assert.Equal(2, byQuick.Items.Count);                                  // القائمة انكمشت
        Assert.Equal(2, byQuick.TotalCount);                                   // TotalCount بعد QuickFilter
        Assert.All(byQuick.Items, r => Assert.True(r.IsOverdue));
        // القرار المعتمد: البطاقات == القائمة == TotalCount بعد QuickFilter (مصدر واحد بعد الفلترة).
        Assert.Equal(byQuick.Summary.Total, byQuick.TotalCount);
        Assert.Equal(byQuick.Items.Count, byQuick.Summary.Total);
    }

    private async Task<string> TemplateTitleAsync(Guid templateId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ReportTemplates.Where(t => t.Id == templateId).Select(t => t.Title).FirstAsync();
    }

    // ===== 40أ) دورة محدّدة + Overdue ⇒ صفوف وأرقام متأخّري تلك الدورة فقط (مثال W30 مقابل W29) =====
    // القرار المعتمد: PastKey + Overdue يعرض متأخّري PastKey فقط، وPastKey2 + Overdue يعرض متأخّري PastKey2 فقط،
    // في الصفوف والعدّادات معًا (لا تسرّب من دورة إلى أخرى، ولا عدّ سابق للـ QuickFilter).
    [Fact]
    public async Task T40a_SpecificCycle_Plus_Overdue_ShowsOnlyThatCycleOverdue_RowsAndCounts()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, c) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        var sc = await SeedExpectedAsync(c);
        // PastKey: a مسودّة متأخّرة، b متوقّع متأخّر، c مُسلَّم غير متأخّر.
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);
        await InsertAsync(sc.VersionId, c, PastKey(), SubmissionStatus.Submitted);

        // PastKey + Overdue ⇒ متأخّرا PastKey فقط (a قائم + b متوقّع). c غير متأخّر مستبعَد.
        var p1 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Overdue, PageSize: 500));
        Assert.Equal(2, p1.Summary.Total);
        Assert.Equal(2, p1.Summary.OverdueCount);
        Assert.Equal(2, p1.Items.Count);
        Assert.Equal(2, p1.TotalCount);
        Assert.All(p1.Items, r => Assert.Equal(PastKey(), r.PeriodKey));
        Assert.All(p1.Items, r => Assert.True(r.IsOverdue));

        // PastKey2 (دورة أخرى) + Overdue ⇒ متأخّرو PastKey2 فقط (a,b,c متوقّعون متأخّرون؛ لا تسرّب من PastKey).
        var p2 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey2(), QuickFilter: SubmissionQuickFilter.Overdue, PageSize: 500));
        Assert.Equal(3, p2.Summary.Total);
        Assert.Equal(3, p2.Summary.OverdueCount);
        Assert.Equal(3, p2.Items.Count);
        Assert.Equal(3, p2.TotalCount);
        Assert.All(p2.Items, r => Assert.Equal(PastKey2(), r.PeriodKey));
        Assert.All(p2.Items, r => Assert.True(r.IsOverdue));
        Assert.All(p2.Items, r => Assert.True(r.IsExpectedSubmission)); // لا تسليم فعليّ في PastKey2
    }

    // ===== 40ب) النافذة التاريخيّة المحدودة (بلا فترة) + Overdue ⇒ كل المتأخّرين داخل النافذة فقط، صفوفًا وأرقامًا =====
    [Fact]
    public async Task T40b_BoundedWindow_Plus_Overdue_ShowsAllWindowOverdue_RowsAndCounts()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var seed = await SeedExpectedAsync(a);
        // تسليم فعليّ غير متأخّر في الدورة الحاليّة ⇒ لا يُحسب متأخّرًا.
        await InsertAsync(seed.VersionId, a, CurrentKey(), SubmissionStatus.Submitted);

        // بلا فترة ⇒ النافذة = آخر 12 أسبوعًا: الدورة الحاليّة (فعليّ غير متأخّر) + 11 متوقّعة مفقودة متأخّرة.
        var all = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500));
        Assert.Equal(12, all.Summary.Total);
        Assert.Equal(11, all.Summary.OverdueCount); // 11 أسبوعًا سابقًا متأخّرة، والدورة الحاليّة غير متأخّرة
        Assert.Null(all.Summary.PeriodKey);

        // + Overdue ⇒ الأحد عشر المتأخّرة فقط، في الصفوف والعدّادات معًا (لا الدورة الحاليّة غير المتأخّرة).
        var overdue = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: null, QuickFilter: SubmissionQuickFilter.Overdue, PageSize: 500));
        Assert.Equal(11, overdue.Summary.Total);
        Assert.Equal(11, overdue.Summary.OverdueCount);
        Assert.Equal(11, overdue.Items.Count);
        Assert.Equal(11, overdue.TotalCount);
        Assert.All(overdue.Items, r => Assert.True(r.IsOverdue));
        Assert.DoesNotContain(overdue.Items, r => r.PeriodKey == CurrentKey()); // الحاليّة غير المتأخّرة مستبعَدة
        Assert.Null(overdue.Summary.PeriodKey);
    }

    // ===== 41) الأداء وعدم N+1 — عدّ أوامر SQL تجريبيًّا عبر اعتراض، على مقياسين (15 و30 مستخدمًا) =====
    // يُثبِت: عدد أوامر SQL لطلب العرض الموحّد ثابت بنيويًّا (لا يتضخّم مع عدد المستخدمين/الصفوف) ⇒ لا N+1؛
    // ومحدود (سقف صغير)؛ والزمن ضمن عتبة واقعيّة (< 5 ثوانٍ) لمجموعة ≥30 مستخدمًا × 12 أسبوعًا مع صفوف فعليّة + متوقّعة.
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

    private sealed record OverviewMeasurement(int QueryCount, long ElapsedMs, int PayloadBytes, int ItemCount, int TotalCount, int SummaryTotal);

    // يبني AppDbContext مُزوَّدًا باعتراض عدّ الأوامر ويشغّل GetOverviewAsync مقيسًا (بلا تعديل كود الإنتاج).
    private async Task<OverviewMeasurement> MeasureOverviewAsync(string connectionString, Guid actorId, string[] roles, UnifiedSubmissionFilter filter)
    {
        var counter = new QueryCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).AddInterceptors(counter).Options;
        await using var db = new AppDbContext(options);
        var clock = new FixedClock(Fixed);
        var currentUser = new TestCurrentUser(actorId, roles);
        var scopeResolver = new ScopeResolver(db, currentUser);
        var grants = new ReportViewGrantService(db, currentUser, null!); // ResolveGrantedSubmitterIdsAsync قراءة فقط (لا تدقيق)
        var expected = new ExpectedSubmissionStatusResolver(db, clock);
        var svc = new SubmissionService(db, currentUser, null!, null!, scopeResolver, null!, null!, grants, expected, clock);

        counter.Reset();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.GetOverviewAsync(filter);
        sw.Stop();
        Assert.True(result.Succeeded, result.Error);
        var dto = result.Value!;
        var payload = JsonSerializer.SerializeToUtf8Bytes(dto);
        return new OverviewMeasurement(counter.Count, sw.ElapsedMilliseconds, payload.Length, dto.Items.Count, dto.TotalCount, dto.Summary.Total);
    }

    [Fact]
    public async Task T41_Performance_NoNPlusOne_ConstantQueryCount_AcrossScales_RealisticThreshold()
    {
        // سلسلة الاتصال نفسها لقاعدة الاختبار الدائمة (لبناء AppDbContext مُعترَض عليه خارج DI المصنع).
        string conn;
        using (var s = _factory.Services.CreateScope())
            conn = s.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;

        // قائدان منفصلان (نطاقان منفصلان) لعزل مجموعتَي القياس داخل القاعدة المشتركة الدائمة.
        var (_, tlSmall) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, tlLarge) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");

        const int smallUsers = 15;
        const int largeUsers = 30;
        const int weeks = 12; // HistoricalWindowWeeks (النافذة الافتراضية بلا فترة).

        // كل مستخدم: مطالبة أسبوعيّة (12 أسبوعًا متوقّعة) + تسليم فعليّ واحد في PastKey ⇒ صفوف فعليّة + متوقّعة معًا.
        async Task SeedGroupAsync(Guid tl, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var (_, e) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
                var seed = await SeedExpectedAsync(e);
                await InsertAsync(seed.VersionId, e, PastKey(), SubmissionStatus.Submitted); // فعليّ (غير متأخّر) داخل النافذة
            }
        }
        await SeedGroupAsync(tlSmall, smallUsers);
        await SeedGroupAsync(tlLarge, largeUsers);

        var filter = new UnifiedSubmissionFilter(PeriodKey: null, PageSize: 500); // النافذة التاريخية المحدودة (12 أسبوعًا)

        // إحماء (JIT/خطّة الاستعلام) حتى لا يلوّث القياسَ الزمنيَّ أوّلُ تشغيل.
        _ = await MeasureOverviewAsync(conn, tlSmall, new[] { "TeamLeader" }, filter);

        var small = await MeasureOverviewAsync(conn, tlSmall, new[] { "TeamLeader" }, filter);
        var large = await MeasureOverviewAsync(conn, tlLarge, new[] { "TeamLeader" }, filter);

        // union rows/user = 12 (أسبوع PastKey فعليّ + 11 متوقّعة مفقودة، بلا ازدواج).
        var smallExpectedUnion = smallUsers * weeks; // 180
        var largeExpectedUnion = largeUsers * weeks; // 360
        Assert.Equal(smallExpectedUnion, small.TotalCount);
        Assert.Equal(smallExpectedUnion, small.SummaryTotal);
        Assert.Equal(largeExpectedUnion, large.TotalCount);
        Assert.Equal(largeExpectedUnion, large.SummaryTotal);

        // ===== إثبات عدم N+1: عدد أوامر SQL ثابت رغم مضاعفة المستخدمين/الصفوف (15→30، 180→360 صفًّا) =====
        Assert.Equal(small.QueryCount, large.QueryCount);
        // محدود بسقف صغير ثابت (شكل الاستعلام: نطاق + فعليّ + تسميات دفعيّة + مُحلِّل متوقّع دفعيّ). ليس بدلالة الصفوف.
        Assert.True(large.QueryCount <= 20, $"عدد أوامر SQL تجاوز السقف الثابت: {large.QueryCount}");

        // ===== عتبة زمنيّة واقعيّة (أضيق من 20 ثانية): ≥30 مستخدمًا × 12 أسبوعًا في < 5 ثوانٍ =====
        Assert.True(large.ElapsedMs < 5000, $"العرض الموحّد للمجموعة الكبيرة استغرق {large.ElapsedMs}ms (> 5000ms).");

        // ===== تسجيل القياس (ملفّ يُقرأ لاحقًا للتقرير) — أرقام حتميّة + زمنيّة =====
        var metrics = new
        {
            usersSmall = smallUsers,
            usersLarge = largeUsers,
            templatesSmall = smallUsers,   // قالب أساسيّ واحد لكل مستخدم
            templatesLarge = largeUsers,
            periodCount = weeks,
            expectedRowsPerUser = weeks - 1, // 11 متوقّعة مفقودة (PastKey أصبح فعليًّا)
            actualRowsPerUser = 1,
            unionRowsPerUser = weeks,        // 12
            unionRowsSmall = small.TotalCount,
            unionRowsLarge = large.TotalCount,
            sqlQueryCountSmall = small.QueryCount,
            sqlQueryCountLarge = large.QueryCount,
            sqlQueryCountConstant = small.QueryCount == large.QueryCount,
            elapsedMsSmall = small.ElapsedMs,
            elapsedMsLarge = large.ElapsedMs,
            payloadBytesSmall = small.PayloadBytes,
            payloadBytesLarge = large.PayloadBytes,
            pageSize = 500,
            rowsReturnedSmall = small.ItemCount,
            rowsReturnedLarge = large.ItemCount,
            summaryTotalSmall = small.SummaryTotal,
            summaryTotalLarge = large.SummaryTotal
        };
        try
        {
            System.IO.File.WriteAllText("/tmp/t41-metrics.json",
                JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* التسجيل اختياريّ؛ لا يُفشِل الاختبار إن تعذّرت الكتابة */ }
    }

    // ============================================================================
    // القسم النهائيّ — تصحيح عقد العدّادات قبل RC (C01–C18):
    // NeedsAction = التسليم الفعليّ القابل للإجراء فقط (لا المتوقّع)؛ المتوقّع المتأخّر في Overdue فقط؛
    // Closed = Status==Closed حصرًا (لا Visible)؛ WaitingMyApproval = المعتمِد الحاليّ == المستخدم المصادَق.
    // ============================================================================

    // ===== C01) المتوقّع غير المُقدَّم قبل الاستحقاق ⇒ يظهر في القائمة، خارج Overdue وخارج NeedsAction =====
    [Fact]
    public async Task C01_MissingNotDue_InList_NotOverdue_NotNeedsAction()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.True(r.IsExpectedSubmission);
        Assert.False(r.IsOverdue);
        Assert.Equal(1, o.Summary.Total);
        Assert.Equal(0, o.Summary.OverdueCount);
        Assert.Equal(0, o.Summary.NeedsActionCount);
    }

    // ===== C02) المتوقّع غير المُقدَّم بعد الاستحقاق ⇒ في Overdue، وخارج NeedsAction =====
    [Fact]
    public async Task C02_MissingOverdue_InOverdue_NotNeedsAction()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.True(r.IsExpectedSubmission);
        Assert.True(r.IsOverdue);
        Assert.Equal(1, o.Summary.OverdueCount);
        Assert.Equal(1, o.Summary.MissingOverdueCount);
        Assert.Equal(0, o.Summary.NeedsActionCount);
    }

    // ===== C03) مسودّة قائمة قبل الاستحقاق ⇒ في NeedsAction، خارج Overdue =====
    [Fact]
    public async Task C03_DraftNotDue_InNeedsAction_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, CurrentKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.False(r.IsOverdue);
        Assert.Equal(1, o.Summary.NeedsActionCount);
        Assert.Equal(0, o.Summary.OverdueCount);
    }

    // ===== C04) مسودّة قائمة بعد الاستحقاق ⇒ في NeedsAction وفي Overdue معًا =====
    [Fact]
    public async Task C04_DraftOverdue_InNeedsAction_AndOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Draft);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.True(r.IsOverdue);
        Assert.Equal(1, o.Summary.NeedsActionCount);
        Assert.Equal(1, o.Summary.OverdueCount);
        Assert.Equal(1, o.Summary.ExistingOverdueCount);
    }

    // ===== C05) مُعاد قائم قبل الاستحقاق ⇒ في NeedsAction وReturned، خارج Overdue =====
    [Fact]
    public async Task C05_ReturnedNotDue_InNeedsAction_InReturned_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, CurrentKey(), SubmissionStatus.Returned);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
        Assert.Equal(1, o.Summary.NeedsActionCount);
        Assert.Equal(1, o.Summary.ReturnedCount);
        Assert.Equal(0, o.Summary.OverdueCount);
    }

    // ===== C06) مُعاد قائم بعد الاستحقاق ⇒ في NeedsAction وReturned وOverdue =====
    [Fact]
    public async Task C06_ReturnedOverdue_InNeedsAction_Returned_Overdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Returned);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.True(r.IsOverdue);
        Assert.Equal(1, o.Summary.NeedsActionCount);
        Assert.Equal(1, o.Summary.ReturnedCount);
        Assert.Equal(1, o.Summary.OverdueCount);
    }

    // ===== C07) مُصعَّد قائم (حتى بعد الاستحقاق) ⇒ في NeedsAction، خارج Overdue (غير مؤهّل للتأخّر) =====
    [Fact]
    public async Task C07_Escalated_InNeedsAction_NotOverdue()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Escalated);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.False(r.IsOverdue);
        Assert.Equal(1, o.Summary.NeedsActionCount);
        Assert.Equal(0, o.Summary.OverdueCount);
    }

    // ===== C08) Visible قائم ⇒ خارج Closed وOverdue وNeedsAction وReturned =====
    [Fact]
    public async Task C08_Visible_NotInClosed_Overdue_NeedsAction_Returned()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Visible);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind);
        Assert.Equal(1, o.Summary.Total);
        Assert.Equal(0, o.Summary.ClosedCount);
        Assert.Equal(0, o.Summary.OverdueCount);
        Assert.Equal(0, o.Summary.NeedsActionCount);
        Assert.Equal(0, o.Summary.ReturnedCount);
    }

    // ===== C09) Closed قائم ⇒ في Closed فقط (خارج Overdue وNeedsAction وReturned) =====
    [Fact]
    public async Task C09_Closed_InClosedOnly()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await SeedExpectedAsync(uid);
        await InsertAsync(s.VersionId, uid, PastKey(), SubmissionStatus.Closed);
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        var r = Row(o, uid);
        Assert.Equal("Closed", r.Status);
        Assert.Equal(1, o.Summary.ClosedCount);
        Assert.Equal(0, o.Summary.OverdueCount);
        Assert.Equal(0, o.Summary.NeedsActionCount);
        Assert.Equal(0, o.Summary.ReturnedCount);
    }

    // ===== C10) QuickFilter=NeedsAction لا يُدخِل المتوقّع غير المُقدَّم مطلقًا =====
    [Fact]
    public async Task C10_ExpectedMissing_NotInNeedsActionQuickFilter()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await SeedExpectedAsync(b); // متوقّع متأخّر (لن يظهر في NeedsAction)
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft); // قائم قابل للإجراء
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.NeedsAction));
        Assert.DoesNotContain(o.Items, r => r.IsExpectedSubmission);
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == b);
        Assert.Contains(o.Items, r => r.SubmitterId == a);
    }

    // ===== C11) QuickFilter=Overdue يُدخِل المتوقّع المتأخّر (يبقى ضمن Overdue) =====
    [Fact]
    public async Task C11_MissingOverdue_InOverdueQuickFilter()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Overdue));
        var r = Assert.Single(o.Items);
        Assert.True(r.IsExpectedSubmission);
        Assert.True(r.IsOverdue);
        Assert.Equal(1, o.Summary.Total);
        Assert.Equal(o.TotalCount, o.Summary.Total);
    }

    // ===== C12) كل صفوف NeedsAction (QF) لها معرّف تسليم غير فارغ =====
    [Fact]
    public async Task C12_NeedsActionRows_AllHaveSubmissionId()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);
        await InsertAsync(sb.VersionId, b, PastKey(), SubmissionStatus.Returned);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.NeedsAction));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.NotNull(r.SubmissionId));
        Assert.All(o.Items, r => Assert.Equal(SubmissionRowKind.ExistingSubmission, r.RowKind));
    }

    // ===== C13) NeedsActionCount = عدد التسليمات الفعليّة القابلة للإجراء (Distinct SubmissionId) =====
    [Fact]
    public async Task C13_NeedsActionCount_DistinctSubmissionId()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft);
        await InsertAsync(sb.VersionId, b, PastKey(), SubmissionStatus.Escalated);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.Equal(2, o.Summary.NeedsActionCount);
    }

    // ===== C14) WaitingMyApproval يطابق التسليم الذي معتمِده الحاليّ = المستخدم المصادَق =====
    [Fact]
    public async Task C14_WaitingMyApproval_MatchesCurrentApprover()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        await InsertAsync(sb.VersionId, b, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: Guid.NewGuid());
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), QuickFilter: SubmissionQuickFilter.MineApproval));
        var r = Assert.Single(o.Items);
        Assert.Equal(a, r.SubmitterId);
        Assert.Equal(tl, r.CurrentApproverId);
        Assert.Equal(1, o.Summary.Total);
    }

    // ===== C15) WaitingMyApproval يستبعد التسليم الذي معتمِده مستخدم آخر =====
    [Fact]
    public async Task C15_WaitingMyApproval_ExcludesOtherApprover()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        await InsertAsync(sb.VersionId, b, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: Guid.NewGuid());
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), QuickFilter: SubmissionQuickFilter.MineApproval));
        Assert.DoesNotContain(o.Items, r => r.SubmitterId == b);
        Assert.All(o.Items, r => Assert.Equal(tl, r.CurrentApproverId));
    }

    // ===== C16) الترقيم لا يغيّر الملخّص (Summary.Total ثابت عبر أحجام صفحات مختلفة) =====
    [Fact]
    public async Task C16_Pagination_DoesNotChangeSummary()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, c) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        await SeedExpectedAsync(a);
        await SeedExpectedAsync(b);
        await SeedExpectedAsync(c);
        var page1 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 1, PageSize: 1));
        var pageAll = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), Page: 1, PageSize: 100));
        Assert.Equal(3, page1.Summary.Total);
        Assert.Equal(3, pageAll.Summary.Total);
        Assert.Equal(page1.Summary.Total, page1.TotalCount);
        Assert.Single(page1.Items);
        Assert.Equal(3, pageAll.Items.Count);
    }

    // ===== C17) W(ماضٍ‑21) + Overdue ⇒ صفوف تلك الفترة فقط =====
    [Fact]
    public async Task C17_PastKeyPlusOverdue_OnlyThatPeriod()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey(), QuickFilter: SubmissionQuickFilter.Overdue));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.Equal(PastKey(), r.PeriodKey));
        Assert.Equal(PastKey(), o.Summary.PeriodKey);
    }

    // ===== C18) W(ماضٍ‑42) + Overdue ⇒ صفوف تلك الفترة فقط (تمييز الفترة) =====
    [Fact]
    public async Task C18_PastKey2PlusOverdue_OnlyThatPeriod()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid);
        var o = await OverviewAsync(uid, new[] { "Employee" },
            new UnifiedSubmissionFilter(PeriodKey: PastKey2(), QuickFilter: SubmissionQuickFilter.Overdue));
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, r => Assert.Equal(PastKey2(), r.PeriodKey));
        Assert.Equal(PastKey2(), o.Summary.PeriodKey);
    }

    // ===== WaitingMyApprovalCount (عدّاد الملخّص) — العقد النهائيّ =====
    // WaitingMyApprovalCount = COUNT DISTINCT SubmissionId WHERE RowKind=ExistingSubmission
    //   AND SubmissionId IS NOT NULL AND CurrentApproverId == المستخدم المصادَق؛
    // يُحسَب خادميًّا بعد النطاق والفلاتر وQuickFilter وقبل الترقيم — لا من صفوف الصفحة، لا من الدور،
    // ولا من صفّ متوقّع غير مُقدَّم. الاختبارات W01–W08 تُثبِت هذا العقد على حقل الملخّص مباشرةً (بلا QuickFilter).

    // ===== W01) التسليم الذي معتمِده الحاليّ = المستخدم ⇒ يُحتَسب في العدّاد (بلا QuickFilter) =====
    [Fact]
    public async Task W01_WaitingMyApprovalCount_CurrentApprover_Counted()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        Assert.Equal(1, o.Summary.WaitingMyApprovalCount);
    }

    // ===== W02) التسليم الذي معتمِده مستخدم آخر ⇒ يُستبعَد من العدّاد =====
    [Fact]
    public async Task W02_WaitingMyApprovalCount_OtherApprover_Excluded()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: Guid.NewGuid());
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        Assert.Equal(0, o.Summary.WaitingMyApprovalCount);
    }

    // ===== W03) التسليم بلا معتمِد حاليّ (null) ⇒ يُستبعَد من العدّاد =====
    [Fact]
    public async Task W03_WaitingMyApprovalCount_NullApprover_Excluded()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, PastKey(), SubmissionStatus.Draft, currentApproverId: null);
        var o = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.Equal(0, o.Summary.WaitingMyApprovalCount);
    }

    // ===== W04) الصفّ المتوقّع غير المُقدَّم لا يدخل العدّاد إطلاقًا =====
    [Fact]
    public async Task W04_WaitingMyApprovalCount_ExcludesExpectedMissing()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedExpectedAsync(uid); // متوقّع متأخّر — بلا تسليم فعليّ (SubmissionId=null)
        var o = await OverviewAsync(uid, new[] { "Employee" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.Contains(o.Items, r => r.IsExpectedSubmission);
        Assert.Equal(0, o.Summary.WaitingMyApprovalCount);
    }

    // ===== W05) الترقيم لا يغيّر العدّاد (ثابت عبر أحجام صفحات مختلفة) =====
    [Fact]
    public async Task W05_WaitingMyApprovalCount_StableAcrossPagination()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        await InsertAsync(sb.VersionId, b, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        var page1 = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), Page: 1, PageSize: 1));
        var pageAll = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), Page: 1, PageSize: 100));
        Assert.Equal(2, page1.Summary.WaitingMyApprovalCount);
        Assert.Equal(2, pageAll.Summary.WaitingMyApprovalCount);
        Assert.Single(page1.Items); // الصفحة الأولى صفّ واحد، لكنّ العدّاد على المجموعة الكاملة المفلترة
    }

    // ===== W06) تغيير الأسبوع يغيّر العدّاد (نطاق الفترة يؤثّر) =====
    [Fact]
    public async Task W06_WaitingMyApprovalCount_ChangesWithWeek()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        var cur = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var past = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: PastKey()));
        Assert.Equal(1, cur.Summary.WaitingMyApprovalCount);
        Assert.Equal(0, past.Summary.WaitingMyApprovalCount);
        Assert.NotEqual(cur.Summary.WaitingMyApprovalCount, past.Summary.WaitingMyApprovalCount);
    }

    // ===== W07) تغيير الفريق يغيّر العدّاد (نطاق الفلتر يؤثّر) =====
    [Fact]
    public async Task W07_WaitingMyApprovalCount_ChangesWithTeamFilter()
    {
        var deptId = await CreateDepartmentAsync();
        var teamX = await CreateTeamAsync(deptId);
        var teamY = await CreateTeamAsync(deptId);
        // قائد الفريق يرى تابعيه (شجرة ManagerId)؛ معتمِد التسليمين = القائد نفسه.
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a, teamX);
        var sb = await SeedExpectedAsync(b, teamY);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, teamId: teamX, currentApproverId: tl);
        await InsertAsync(sb.VersionId, b, CurrentKey(), SubmissionStatus.Submitted, teamId: teamY, currentApproverId: tl);
        var both = await OverviewAsync(tl, new[] { "TeamLeader" }, new UnifiedSubmissionFilter(PeriodKey: CurrentKey()));
        var onlyX = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), TeamId: teamX));
        Assert.Equal(2, both.Summary.WaitingMyApprovalCount);
        Assert.Equal(1, onlyX.Summary.WaitingMyApprovalCount);
        Assert.NotEqual(both.Summary.WaitingMyApprovalCount, onlyX.Summary.WaitingMyApprovalCount);
    }

    // ===== W08) QuickFilter=MineApproval ⇒ Summary.Total == عدد الصفوف == WaitingMyApprovalCount =====
    [Fact]
    public async Task W08_MineApprovalQuickFilter_TotalMatchesWaitingCount()
    {
        var (_, tl) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, a) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var (_, b) = await TestAuth.CreateUserAsync(_factory, "Employee", tl);
        var sa = await SeedExpectedAsync(a);
        var sb = await SeedExpectedAsync(b);
        await InsertAsync(sa.VersionId, a, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: tl);
        await InsertAsync(sb.VersionId, b, CurrentKey(), SubmissionStatus.Submitted, currentApproverId: Guid.NewGuid());
        var o = await OverviewAsync(tl, new[] { "TeamLeader" },
            new UnifiedSubmissionFilter(PeriodKey: CurrentKey(), QuickFilter: SubmissionQuickFilter.MineApproval));
        Assert.Equal(1, o.Summary.WaitingMyApprovalCount);
        Assert.Equal(o.Summary.WaitingMyApprovalCount, o.Summary.Total);
        Assert.Equal(o.Items.Count, o.Summary.WaitingMyApprovalCount);
        Assert.Equal(a, Assert.Single(o.Items).SubmitterId);
    }
}

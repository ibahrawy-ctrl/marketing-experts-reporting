using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — PHASE 2. اختبارات الخدمة الخلفية الموحّدة
/// <see cref="UnifiedReportStatusService"/> (self-only، بلا endpoint بعد). تُثبِت: الاشتقاق الموحّد
/// للحالة عبر الدورات، الجلب الدفعيّ (بلا N+1)، استبعاد الحذف الناعم، أنّ الحالة الطرفيّة (Closed) لا
/// تُوصَف أبدًا كـ«متأخّر بلا تسليم» (جوهر الإصلاح)، وأنّ الخدمة قراءة فقط بلا أثر جانبيّ.
/// </summary>
[Collection("Integration")]
public class UnifiedReportStatusTests
{
    private readonly CustomWebApplicationFactory _factory;

    public UnifiedReportStatusTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== مساعدات =====

    /// <summary>ICurrentUser بديل للاختبار المباشر للخدمة (self-only) دون المرور بطبقة HTTP.</summary>
    private sealed class StubCurrentUser : ICurrentUser
    {
        private readonly Guid? _id;
        private readonly HashSet<string> _roles;
        public StubCurrentUser(Guid? id, params string[] roles)
        {
            _id = id;
            _roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        }
        public Guid? UserId => _id;
        public bool IsAuthenticated => _id is not null;
        public IReadOnlyCollection<string> Roles => _roles;
        public bool IsInRole(string role) => _roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(_roles.Contains);
    }

    /// <summary>يجعل المستخدم متوقَّعًا منه تقرير أسبوعيّ عبر مسمّى له قالب أساسي منشور.</summary>
    private async Task<Guid> SetupReportingRoleAsync(Guid userId)
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
            PublishedAtUtc = DateTime.UtcNow
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task InsertSubmissionAsync(Guid versionId, Guid submitterId, string cycleKey,
        SubmissionStatus status, DateTime? submittedAtUtc = null, Guid? currentApproverId = null,
        bool isDeleted = false)
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
            CurrentApproverId = currentApproverId,
            SubmittedAtUtc = submittedAtUtc ?? (status == SubmissionStatus.Draft ? null : DateTime.UtcNow),
            IsDeleted = isDeleted,
            DeletedAtUtc = isDeleted ? DateTime.UtcNow : null
        });
        await db.SaveChangesAsync();
    }

    private async Task<Result<UnifiedReportCycleStatusDto>> DeriveAsync(Guid userId, string cycleKey, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new UnifiedReportStatusService(db, new StubCurrentUser(userId, roles.Length == 0 ? new[] { "Employee" } : roles));
        return await svc.GetMyWeeklyCycleStatusAsync(cycleKey, null);
    }

    private static string CurrentCycleKey() => ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday());
    private static string PastCycleKey() => ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday().AddDays(-21));

    // ===== 1) غير مصادَق ⇒ auth.unauthenticated =====
    [Fact]
    public async Task Unauthenticated_Fails()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new UnifiedReportStatusService(db, new StubCurrentUser(null));
        var res = await svc.GetMyWeeklyCycleStatusesAsync(new[] { CurrentCycleKey() }, null);
        Assert.False(res.Succeeded);
        Assert.Equal("auth.unauthenticated", res.ErrorCode);
    }

    // ===== 2) مفتاح غير صالح للـ single ⇒ فشل تحقّق =====
    [Fact]
    public async Task InvalidCycleKey_Single_Fails()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await DeriveAsync(uid, "not-a-key");
        Assert.False(res.Succeeded);
        Assert.Equal("calendar.cycle_key_invalid", res.ErrorCode);
    }

    // ===== 3) بلا مسمّى تقارير ⇒ NotAssigned =====
    [Fact]
    public async Task NoReportingRole_NotAssigned()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await DeriveAsync(uid, CurrentCycleKey());
        Assert.True(res.Succeeded);
        Assert.Equal(UnifiedCycleStatus.NotAssigned, res.Value!.UnifiedStatus);
        Assert.False(res.Value.IsAssigned);
    }

    // ===== 4) الدورة الحالية، مُسنَد، بلا تسليم ⇒ DueNow (لا متأخّر) =====
    [Fact]
    public async Task CurrentCycle_NoSubmission_DueNow()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(uid);
        var res = await DeriveAsync(uid, CurrentCycleKey());
        Assert.True(res.Succeeded);
        Assert.Equal(UnifiedCycleStatus.DueNow, res.Value!.UnifiedStatus);
        Assert.False(res.Value.IsLate);
        Assert.Equal(0, res.Value.DelayDays);
    }

    // ===== 5) دورة ماضية، مُسنَد، بلا تسليم ⇒ OverdueNotSubmitted (متأخّر) =====
    [Fact]
    public async Task PastCycle_NoSubmission_OverdueNotSubmitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(uid);
        var res = await DeriveAsync(uid, PastCycleKey());
        Assert.True(res.Succeeded);
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, res.Value!.UnifiedStatus);
        Assert.True(res.Value.IsLate);
        Assert.True(res.Value.DelayDays > 0);
        Assert.Equal("alert", res.Value.Severity);
    }

    // ===== 6) تسليم في الموعد (الدورة الحالية) ⇒ SubmittedOnTime =====
    [Fact]
    public async Task CurrentCycle_Submitted_OnTime()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = CurrentCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.Submitted);
        var res = await DeriveAsync(uid, key);
        Assert.Equal(UnifiedCycleStatus.SubmittedOnTime, res.Value!.UnifiedStatus);
        Assert.False(res.Value.IsLate);
        Assert.NotNull(res.Value.SubmissionId);
    }

    // ===== 7) تسليم بعد الموعد (دورة ماضية، أُرسِل الآن) ⇒ SubmittedLate =====
    [Fact]
    public async Task PastCycle_SubmittedNow_Late()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.Submitted, submittedAtUtc: DateTime.UtcNow);
        var res = await DeriveAsync(uid, key);
        Assert.Equal(UnifiedCycleStatus.SubmittedLate, res.Value!.UnifiedStatus);
        Assert.True(res.Value.IsLate);
        Assert.True(res.Value.DelayDays > 0);
    }

    // ===== 8) مسودّة: حالية ⇒ Draft، ماضية ⇒ OverdueDraft =====
    [Fact]
    public async Task Draft_CurrentIsDraft_PastIsOverdueDraft()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var cur = CurrentCycleKey();
        var past = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, cur, SubmissionStatus.Draft);
        await InsertSubmissionAsync(vid, uid, past, SubmissionStatus.Draft);

        Assert.Equal(UnifiedCycleStatus.Draft, (await DeriveAsync(uid, cur)).Value!.UnifiedStatus);
        Assert.Equal(UnifiedCycleStatus.OverdueDraft, (await DeriveAsync(uid, past)).Value!.UnifiedStatus);
    }

    // ===== 9) معاد: حالية ⇒ ReturnedForChanges، ماضية ⇒ OverdueReturned =====
    [Fact]
    public async Task Returned_CurrentVsPast()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var cur = CurrentCycleKey();
        var past = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, cur, SubmissionStatus.Returned);
        await InsertSubmissionAsync(vid, uid, past, SubmissionStatus.Returned);

        Assert.Equal(UnifiedCycleStatus.ReturnedForChanges, (await DeriveAsync(uid, cur)).Value!.UnifiedStatus);
        Assert.Equal(UnifiedCycleStatus.OverdueReturned, (await DeriveAsync(uid, past)).Value!.UnifiedStatus);
    }

    // ===== 10) عالق عند معتمِد ⇒ PendingApproval (لا إجراء على الموظّف) =====
    [Fact]
    public async Task PendingApproval_WhenApprovedByDirectManager()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = CurrentCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.ApprovedByDirectManager, currentApproverId: Guid.NewGuid());
        var res = await DeriveAsync(uid, key);
        Assert.Equal(UnifiedCycleStatus.PendingApproval, res.Value!.UnifiedStatus);
    }

    // ===== 11) جوهر الإصلاح: تسليم مُغلَق لدورة ماضية ⇒ Closed (لا OverdueNotSubmitted أبدًا) =====
    [Fact]
    public async Task PastCycle_Closed_NeverOverdueNotSubmitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.Closed);
        var res = await DeriveAsync(uid, key);
        Assert.Equal(UnifiedCycleStatus.Closed, res.Value!.UnifiedStatus);
        Assert.NotEqual(UnifiedCycleStatus.OverdueNotSubmitted, res.Value.UnifiedStatus);
    }

    // ===== 12) الحذف الناعم يُلغي التسليم ⇒ دورة ماضية محذوفة ⇒ OverdueNotSubmitted =====
    [Fact]
    public async Task SoftDeletedSubmission_TreatedAsNoSubmission()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.Submitted, isDeleted: true);
        var res = await DeriveAsync(uid, key);
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, res.Value!.UnifiedStatus);
        Assert.False(res.Value.HasSubmission);
    }

    // ===== 13) الجلب الدفعيّ: عدّة مفاتيح في نداء واحد ⇒ صفّ لكلّ مفتاح، بلا N+1 =====
    [Fact]
    public async Task Batch_MultipleKeys_OneRowPerKey_NoNPlusOne()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var cur = CurrentCycleKey();
        var past = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, cur, SubmissionStatus.Submitted);
        // past بلا تسليم ⇒ متأخّر.

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new UnifiedReportStatusService(db, new StubCurrentUser(uid, "Employee"));
        var res = await svc.GetMyWeeklyCycleStatusesAsync(new[] { cur, past, cur }, null); // مع تكرار

        Assert.True(res.Succeeded);
        Assert.Equal(2, res.Value!.Count); // إزالة التكرار ⇒ مفتاحان فقط
        Assert.Equal(UnifiedCycleStatus.SubmittedOnTime, res.Value.First(r => r.PeriodKey == cur).UnifiedStatus);
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, res.Value.First(r => r.PeriodKey == past).UnifiedStatus);
    }

    // ===== 14) isCurrentPriority: الأولوية للدورة الأعلى إلحاحًا (المتأخّرة غير المُسلَّمة) =====
    [Fact]
    public async Task CurrentPriority_PicksHighestActionRequired()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var cur = CurrentCycleKey();       // DueNow (رتبة 5)
        var past = PastCycleKey();         // OverdueNotSubmitted (رتبة 1 — الأعلى)

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new UnifiedReportStatusService(db, new StubCurrentUser(uid, "Employee"));
        var res = await svc.GetMyWeeklyCycleStatusesAsync(new[] { cur, past }, null);

        Assert.True(res.Value!.First(r => r.PeriodKey == past).IsCurrentPriority);
        Assert.False(res.Value.First(r => r.PeriodKey == cur).IsCurrentPriority);
    }

    // ===== 15) قراءة فقط: لا بريد/صندوق صادر/تغيير تسليم =====
    [Fact]
    public async Task ReadOnly_NoSideEffects()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupReportingRoleAsync(uid);
        var key = PastCycleKey();
        await InsertSubmissionAsync(vid, uid, key, SubmissionStatus.Submitted);

        int emailsBefore, outboxBefore;
        (Guid Id, SubmissionStatus Status) subBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            emailsBefore = await db.EmailNotifications.CountAsync();
            outboxBefore = await db.EmailOutbox.CountAsync();
            var s = await db.ReportSubmissions.AsNoTracking().FirstAsync(x => x.SubmitterId == uid && x.PeriodKey == key);
            subBefore = (s.Id, s.Status);
        }

        var res = await DeriveAsync(uid, key);
        Assert.True(res.Succeeded);

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

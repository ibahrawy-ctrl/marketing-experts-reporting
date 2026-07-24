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
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — PHASE 7. مصفوفة الحالة المتوقّعة A–H عبر
/// <see cref="ExpectedSubmissionStatusResolver"/> بساعة ثابتة (ISystemClock مُثبَّت).
/// تُثبِت: Users-first LEFT JOIN، بوّابة أرضيّة الانطباق، فصل الحاليّ عن التاريخيّ، اتّساق العدّادات،
/// وظهور «من لم يبدأ» (non-starter، SubmissionId=null). قراءة فقط — لا تعديل/هجرة/كتابة إنتاج.
/// </summary>
[Collection("Integration")]
public class ExpectedSubmissionStatusResolverTests
{
    private readonly CustomWebApplicationFactory _factory;
    public ExpectedSubmissionStatusResolverTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ساعة ثابتة: الإثنين 2026-11-23 09:00Z (= 12:00 الرياض) — قبل موعد الموظّف (الأربعاء) للدورة الحاليّة.
    // مؤخَّرة إلى ما بعد أرضيّة الإطلاق الأسبوعيّ 2026-07-04 كي تكون كلّ الدورات المختبَرة (الحاليّة و−21)
    // في/بعد الأرضيّة، فيبقى سلوك الأرضيّة الفرديّة (المستخدم/القالب) هو الحاكم دون أن تتدخّل أرضيّة الإطلاق.
    private static readonly DateTimeOffset Fixed = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private static string CurrentKey() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime));
    private static string PastKey() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(Fixed.UtcDateTime).AddDays(-21));

    // أرضيّة مبكّرة (قبل كلّ الدورات المختبَرة) ما لم يُطلَب خلاف ذلك (اختبار NotApplicable).
    private static DateTime EarlyAnchor => Fixed.UtcDateTime.AddDays(-180);

    /// <summary>يجعل المستخدم مطالَبًا بتقرير أسبوعيّ عبر مسمّى + قالب أساسي منشور، بأرضيّة محقونة.</summary>
    private async Task<Guid> SetupAsync(Guid userId, DateTime floorAnchor)
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
        var templates = scope.ServiceProvider.GetRequiredService<IReportTemplateService>();
        var resolver = new ExpectedSubmissionStatusResolver(db, new FixedClock(Fixed), templates);
        var results = await resolver.ResolveAsync(new ExpectedStatusQuery(new[] { userId }, new[] { key }, null));
        return results.FirstOrDefault();
    }

    // ===== A) الدورة الحاليّة، بلا تسليم، قبل الموعد ⇒ NotStartedWithinDeadline =====
    [Fact]
    public async Task A_CurrentNoSubmission_NotStartedWithinDeadline()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.NotNull(r);
        Assert.True(r!.IsExpected);
        Assert.False(r.HasSubmission);
        Assert.Null(r.SubmissionId);
        Assert.True(r.IsWithinDeadline);
        Assert.False(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.NotStartedWithinDeadline, r.Status);
    }

    // ===== B) الدورة الحاليّة، مسودّة، قبل الموعد ⇒ DraftWithinDeadline =====
    [Fact]
    public async Task B_CurrentDraft_DraftWithinDeadline()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, CurrentKey(), SubmissionStatus.Draft);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.Equal(ExpectedSubmissionStatus.DraftWithinDeadline, r!.Status);
        Assert.True(r.HasSubmission);
        Assert.False(r.IsActionable);
    }

    // ===== C) دورة ماضية، بلا تسليم، بعد الموعد ⇒ OverdueNotSubmitted (non-starter مرئيّ) =====
    [Fact]
    public async Task C_PastNoSubmission_OverdueNotSubmitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, PastKey());
        Assert.True(r!.IsExpected);
        Assert.False(r.HasSubmission);
        Assert.Null(r.SubmissionId);
        Assert.True(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.OverdueNotSubmitted, r.Status);
    }

    // ===== D) دورة ماضية، مسودّة ⇒ OverdueDraft =====
    [Fact]
    public async Task D_PastDraft_OverdueDraft()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, PastKey(), SubmissionStatus.Draft);
        var r = await ResolveOneAsync(uid, PastKey());
        Assert.Equal(ExpectedSubmissionStatus.OverdueDraft, r!.Status);
        Assert.True(r.IsActionable);
    }

    // ===== E) مُعاد للتعديل ⇒ ReturnedActionRequired =====
    [Fact]
    public async Task E_Returned_ReturnedActionRequired()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, CurrentKey(), SubmissionStatus.Returned);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.Equal(ExpectedSubmissionStatus.ReturnedActionRequired, r!.Status);
        Assert.True(r.IsActionable);
    }

    // ===== F) مُصعَّد ⇒ EscalatedActionRequired =====
    [Fact]
    public async Task F_Escalated_EscalatedActionRequired()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, CurrentKey(), SubmissionStatus.Escalated);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.Equal(ExpectedSubmissionStatus.EscalatedActionRequired, r!.Status);
        Assert.True(r.IsActionable);
    }

    // ===== G) دورة قبل أرضيّة الانطباق ⇒ NotApplicable (لا تدخل العدّادات، IsExpected=false) =====
    [Fact]
    public async Task G_CycleBeforeFloor_NotApplicable()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        // أرضيّة متأخّرة (اليوم السابق للساعة الثابتة) ⇒ الدورة الماضية تبدأ قبلها.
        await SetupAsync(uid, Fixed.UtcDateTime.AddDays(-1));
        var r = await ResolveOneAsync(uid, PastKey());
        Assert.NotNull(r);
        Assert.False(r!.IsExpected);
        Assert.False(r.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.NotApplicable, r.Status);
    }

    // ===== G2) تمييز صريح: «ما قبل الأرضيّة» ⇒ BeforeApplicabilityFloor لا ExemptOrOnLeave =====
    // يُثبِت أن الدورة السابقة لأرضيّة الانطباق تُصنَّف «ما قبل الأرضيّة» صراحةً (نموذج القراءة)،
    // فلا تُعرَض للموظّف كإعفاء/إجازة. الحالة NotApplicable، IsExpected=false، التسمية «لا ينطبق».
    [Fact]
    public async Task G2_CycleBeforeFloor_ExclusionCode_IsBeforeApplicabilityFloor_NotExempt()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupAsync(uid, Fixed.UtcDateTime.AddDays(-1));
        var r = await ResolveOneAsync(uid, PastKey());
        Assert.NotNull(r);
        Assert.False(r!.IsExpected);
        Assert.Equal(ExpectedSubmissionStatus.NotApplicable, r.Status);
        // التمييز البرمجيّ القاطع: ما قبل الأرضيّة، وليس إعفاءً/إجازة.
        Assert.Equal(CycleExclusionReason.BeforeApplicabilityFloor, r.ExclusionReasonCode);
        Assert.NotEqual(CycleExclusionReason.ExemptOrOnLeave, r.ExclusionReasonCode);
        Assert.Equal("لا ينطبق", r.StatusLabel);
    }

    // ===== H) مُسلَّم في الموعد ⇒ Submitted (ليس بندَ إجراء) =====
    [Fact]
    public async Task H_SubmittedOnTime_Submitted()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        await InsertAsync(vid, uid, CurrentKey(), SubmissionStatus.Submitted, Fixed.UtcDateTime);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.Equal(ExpectedSubmissionStatus.Submitted, r!.Status);
        Assert.True(r.HasSubmission);
        Assert.False(r.IsActionable);
    }

    // ===== الإسقاط الإداريّ (Users-first): مُسلِّم واحد + non-starter واحد ⇒ عدّادات متّسقة =====
    [Fact]
    public async Task Management_UsersFirst_CountsConsistent_NonStarterVisible()
    {
        var (_, u1) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, u2) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var v1 = await SetupAsync(u1, EarlyAnchor);
        await SetupAsync(u2, EarlyAnchor);
        await InsertAsync(v1, u1, PastKey(), SubmissionStatus.Submitted, Fixed.UtcDateTime);
        // u2 لم يبدأ (لا تسليم) في دورة ماضية ⇒ OverdueNotSubmitted.

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var templates = scope.ServiceProvider.GetRequiredService<IReportTemplateService>();
        var resolver = new ExpectedSubmissionStatusResolver(db, new FixedClock(Fixed), templates);
        var proj = await resolver.ResolveManagementAsync(PastKey(), new[] { u1, u2 });

        Assert.Equal(2, proj.Expected);
        Assert.Equal(1, proj.Submitted);
        Assert.Equal(1, proj.OverdueNotSubmitted);
        Assert.Equal(proj.Expected - proj.Submitted, proj.OverdueNotSubmitted + proj.OverdueDraft
            + proj.NotStartedWithinDeadline + proj.ReturnedActionRequired + proj.EscalatedActionRequired);
        var nonStarter = Assert.Single(proj.ActionItems, i => i.UserId == u2);
        Assert.Null(nonStarter.SubmissionId);           // non-starter بلا تسليم
        Assert.False(nonStarter.HasSubmission);
        Assert.Equal(ExpectedSubmissionStatus.OverdueNotSubmitted, nonStarter.Status);
    }

    // ===== فصل الحاليّ عن التاريخيّ: الشارة = الحاليّة؛ التاريخيّ = الماضية القابلة للإجراء =====
    [Fact]
    public async Task Self_CurrentSeparateFromHistorical()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupAsync(uid, EarlyAnchor);
        // لا تسليم في أيّ دورة: الحاليّة ضمن المهلة (لا إجراء)، الماضية متأخّرة (بند تاريخيّ).

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var templates = scope.ServiceProvider.GetRequiredService<IReportTemplateService>();
        var resolver = new ExpectedSubmissionStatusResolver(db, new FixedClock(Fixed), templates);
        var self = await resolver.ResolveSelfAsync(uid, new[] { CurrentKey(), PastKey() });

        Assert.NotNull(self.CurrentCycle);
        Assert.Equal(CurrentKey(), self.CurrentCycle!.PeriodKey);
        Assert.Equal(ExpectedSubmissionStatus.NotStartedWithinDeadline, self.CurrentCycle.Status);

        var hist = Assert.Single(self.HistoricalActionItems);
        Assert.Equal(PastKey(), hist.PeriodKey);
        Assert.True(hist.IsHistorical);
        Assert.True(hist.IsActionable);
        Assert.Equal(ExpectedSubmissionStatus.OverdueNotSubmitted, hist.Status);
    }

    /// <summary>يضيف استثناءً صريحًا على مستوى الموظّف (Employee/Exclude) للقالب المرتبط بالإصدار.</summary>
    private async Task ExcludeUserFromTemplateAsync(Guid versionId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var templateId = await db.ReportTemplateVersions
            .Where(v => v.Id == versionId).Select(v => v.ReportTemplateId).FirstAsync();
        db.ReportTemplateAssignments.Add(new ReportTemplateAssignment
        {
            ReportTemplateId = templateId,
            ScopeType = TemplateAssignmentScope.Employee,
            ScopeId = userId,
            Kind = TemplateAssignmentKind.Exclude,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    // ===== عقد الاستحقاق المركزي (REPORT-EXPECTED-ENTITLEMENT-CONTRACT-R1) — PHASE 7 =====
    // يُثبِت أنّ التوقّع مُقيَّد بحارس الإسناد المركزي نفسه (IsTemplateAssignedToUserAsync) مصدر
    // CanSubmit الوحيد؛ فيُمنَع منطقيًّا Expected=true مع CanSubmit=false.

    // (سلبيّ) قالب أساسي أسبوعيّ مرتبط بالمسمّى، لكن يوجد استثناء صريح Employee/Exclude لهذا
    // (المستخدم، القالب) ⇒ لا يستطيع الإنشاء ⇒ يجب ألّا يُولَّد صفّ «متوقّع» إطلاقًا.
    [Fact]
    public async Task Entitlement_EmployeeExclude_ProducesNoExpectedRow()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var vid = await SetupAsync(uid, EarlyAnchor);
        // ضبط أوّليّ: بلا استثناء ⇒ التوقّع قائم (ضابط قبليّ داخل نفس الاختبار).
        var before = await ResolveOneAsync(uid, CurrentKey());
        Assert.NotNull(before);
        Assert.True(before!.IsExpected);

        await ExcludeUserFromTemplateAsync(vid, uid);

        var after = await ResolveOneAsync(uid, CurrentKey());
        // العقد: لا صفّ متوقّع (المستخدم مُستثنى ⇒ CanSubmit=false ⇒ Expected مستحيل).
        Assert.Null(after);
    }

    // (إيجابيّ — ضابط) مستخدم مرتبط بالمسمّى فقط بلا أيّ استثناء ⇒ يبقى متوقَّعًا (Expected=true).
    [Fact]
    public async Task Entitlement_JobRoleOnly_RemainsExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupAsync(uid, EarlyAnchor);
        var r = await ResolveOneAsync(uid, CurrentKey());
        Assert.NotNull(r);
        Assert.True(r!.IsExpected);
        Assert.Equal(ExpectedSubmissionStatus.NotStartedWithinDeadline, r.Status);
    }
}

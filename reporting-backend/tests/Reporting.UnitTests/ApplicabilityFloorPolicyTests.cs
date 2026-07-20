using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — اختبارات نقيّة لأرضيّة الانطباق (Applicability Floor).
/// تُثبِت: الأرضيّة = MAX(إنشاء المستخدم، أوّل نشر للقالب، الإسناد الموثَّق)؛ اشتقاق المصدر/الثقة؛
/// وأنّ الدورة قبل الأرضيّة غير مطلوبة (لا التزام رجعيّ).
/// </summary>
public class ApplicabilityFloorPolicyTests
{
    private static readonly DateOnly D2026_01_01 = new(2026, 1, 1);
    private static readonly DateOnly D2026_03_01 = new(2026, 3, 1);
    private static readonly DateOnly D2026_06_01 = new(2026, 6, 1);

    // (1) بلا إسناد/نشر ⇒ الأرضيّة = إنشاء المستخدم (رجوع محافِظ).
    [Fact]
    public void Floor_UserCreationOnly_IsUserCreated_ConservativeFallback()
    {
        var r = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(D2026_03_01, null, null));
        Assert.Equal(D2026_03_01, r.Floor);
        Assert.Equal(ApplicabilitySource.UserCreationFallback, r.Source);
        Assert.Equal(ApplicabilityConfidence.ConservativeFallback, r.Confidence);
    }

    // (2) نشر القالب أحدث من إنشاء المستخدم ⇒ الأرضيّة = النشر، ثقة متوسطة.
    [Fact]
    public void Floor_TemplatePublishedLater_Governs_MediumConfidence()
    {
        var r = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(D2026_01_01, D2026_06_01, null));
        Assert.Equal(D2026_06_01, r.Floor);
        Assert.Equal(ApplicabilitySource.TemplatePublicationFloor, r.Source);
        Assert.Equal(ApplicabilityConfidence.Medium, r.Confidence);
    }

    // (3) إسناد موثَّق أحدث ⇒ الأرضيّة = الإسناد، ثقة عالية.
    [Fact]
    public void Floor_AuditedAssignmentLatest_Governs_HighConfidence()
    {
        var r = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(D2026_01_01, D2026_03_01, D2026_06_01));
        Assert.Equal(D2026_06_01, r.Floor);
        Assert.Equal(ApplicabilitySource.AuditedJobRoleAssignment, r.Source);
        Assert.Equal(ApplicabilityConfidence.High, r.Confidence);
    }

    // (4) إسناد موثَّق موجود لكنّه ليس الحاكم ⇒ الثقة تبقى عالية، المصدر أرضيّة مركّبة/نشر.
    [Fact]
    public void Floor_AuditedPresentButNotGoverning_StillHighConfidence()
    {
        var r = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(D2026_01_01, D2026_06_01, D2026_03_01));
        Assert.Equal(D2026_06_01, r.Floor);
        Assert.Equal(ApplicabilityConfidence.High, r.Confidence);
        Assert.Equal(ApplicabilitySource.TemplatePublicationFloor, r.Source);
    }

    // (5) تساوي مكوّنين عند الأرضيّة بلا إسناد ⇒ أرضيّة مركّبة محافِظة، ثقة متوسطة.
    [Fact]
    public void Floor_TwoComponentsTie_CombinedConservative()
    {
        var r = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(D2026_06_01, D2026_06_01, null));
        Assert.Equal(D2026_06_01, r.Floor);
        Assert.Equal(ApplicabilitySource.CombinedConservativeFloor, r.Source);
        Assert.Equal(ApplicabilityConfidence.Medium, r.Confidence);
    }

    // (6) بوّابة الانطباق: دورة تبدأ قبل الأرضيّة ⇒ غير مطلوبة؛ في/بعدها ⇒ مطلوبة.
    [Fact]
    public void IsCycleApplicable_BeforeFloor_False_OnOrAfter_True()
    {
        var floor = D2026_03_01;
        Assert.False(ApplicabilityFloorPolicy.IsCycleApplicable(new DateOnly(2026, 2, 28), floor));
        Assert.True(ApplicabilityFloorPolicy.IsCycleApplicable(floor, floor));
        Assert.True(ApplicabilityFloorPolicy.IsCycleApplicable(new DateOnly(2026, 3, 7), floor));
    }

    // (7) أوّل دورة مؤهَّلة: إن كانت الدورة المحتوية للأرضيّة تبدأ قبلها ⇒ الدورة التالية.
    [Fact]
    public void FirstEligibleCycleKey_SkipsPartialCycle()
    {
        var floor = D2026_06_01;
        var key = ApplicabilityFloorPolicy.FirstEligibleCycleKey(floor);
        var start = ReportingCalendarPolicy.CycleRange(key).Start;
        Assert.True(start >= floor);                          // أوّل دورة تبدأ في/بعد الأرضيّة
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);    // بداية الدورة سبت
        Assert.True(start.AddDays(-7) < floor);               // الدورة السابقة تبدأ قبل الأرضيّة
    }
}

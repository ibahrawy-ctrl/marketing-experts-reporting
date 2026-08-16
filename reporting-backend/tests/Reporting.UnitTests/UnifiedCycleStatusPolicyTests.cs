using Reporting.Domain.Common;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — PHASE 1 — اختبارات وحدة للسياسة النقيّة
/// <see cref="UnifiedCycleStatusPolicy"/>. تُغطّي جدول الحالة الموحّد (§3-أ) وترتيب الاشتقاق (§4-أ):
/// غير مُسنَد/غير مطلوب، لا تسليم (NotDue/DueNow/OverdueNotSubmitted)، تسليم فعّال بكل حالاته
/// (Draft/OverdueDraft، Returned/OverdueReturned، SubmittedOnTime/SubmittedLate، PendingApproval، Closed)،
/// أولويّة التسليم الفعّال على الموعد، الحذف الناعم يُلغي التسليم، الحدّ عند الموعد بالضبط،
/// توقيت الرياض (UTC+3)، حدود نهاية الأسبوع/السنة، والطوابع الزمنيّة null. كلّها دوال خالصة.
/// </summary>
public class UnifiedCycleStatusPolicyTests
{
    private static readonly TimeSpan Riyadh = TimeSpan.FromHours(3);

    private static DateTimeOffset At(int y, int m, int d, int hh = 0, int mm = 0)
        => new(y, m, d, hh, mm, 0, Riyadh);

    // مدخل أساسيّ: دورة مُسنَدة/مطلوبة، بداية 2026-07-04، موعد 2026-07-10 نهاية اليوم (الرياض).
    private static CycleStatusInput Base(
        DateTimeOffset now,
        bool hasSubmission = false,
        SubmissionStatus? status = null,
        DateTimeOffset? submittedAt = null,
        bool isDeleted = false,
        bool isAssigned = true,
        bool isRequired = true,
        DateTimeOffset? cycleStartsAt = null,
        DateTimeOffset? dueAt = null)
        => new(
            IsAssigned: isAssigned,
            IsRequired: isRequired,
            CycleStartsAt: cycleStartsAt ?? At(2026, 7, 4),
            DueAt: dueAt ?? At(2026, 7, 10, 23, 59),
            CurrentTime: now,
            HasSubmission: hasSubmission,
            SubmissionStatus: status,
            SubmittedAt: submittedAt,
            ApprovedAt: null,
            ClosedAt: null,
            IsDeleted: isDeleted);

    // U1 — غير مُسنَد ⇒ NotAssigned.
    [Fact]
    public void NotAssigned_WhenNotAssigned()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 6), isAssigned: false));
        Assert.Equal(UnifiedCycleStatus.NotAssigned, r.Status);
        Assert.Equal(CycleStatusSeverity.None, r.Severity);
        Assert.False(r.IsLate);
        Assert.Equal(0, r.DelayDays);
    }

    // U2 — مُسنَد لكن غير مطلوب (إجازة/استثناء) ⇒ NotRequired.
    [Fact]
    public void NotRequired_WhenAssignedButNotRequired()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 6), isRequired: false));
        Assert.Equal(UnifiedCycleStatus.NotRequired, r.Status);
        Assert.Equal(CycleStatusSeverity.None, r.Severity);
    }

    // U3 — لا تسليم، الدورة مستقبليّة (قبل بداية النافذة) ⇒ NotDue.
    [Fact]
    public void NotDue_WhenBeforeWindowNoSubmission()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 1)));
        Assert.Equal(UnifiedCycleStatus.NotDue, r.Status);
        Assert.Equal(CycleStatusSeverity.None, r.Severity);
    }

    // U4 — لا تسليم، النافذة مفتوحة واليوم ≤ الموعد ⇒ DueNow.
    [Fact]
    public void DueNow_WhenWindowOpenNoSubmission()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 6)));
        Assert.Equal(UnifiedCycleStatus.DueNow, r.Status);
        Assert.Equal(CycleStatusSeverity.Info, r.Severity);
    }

    // U5 — لا تسليم، اليوم > الموعد (بعد استبعاد soft-deleted) ⇒ OverdueNotSubmitted (جوهر الإصلاح).
    [Fact]
    public void OverdueNotSubmitted_WhenPastDueNoSubmission()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 13, 12, 0)));
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, r.Status);
        Assert.Equal(CycleStatusSeverity.Alert, r.Severity);
        Assert.True(r.IsLate);
        Assert.Equal(3, r.DelayDays); // 2026-07-10 23:59 → 2026-07-13 12:00 ⇒ 2.5 يوم ⇒ 3.
    }

    // U6 — مسودّة قبل الموعد ⇒ Draft (ليست تأخّرًا).
    [Fact]
    public void Draft_WhenDraftBeforeDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 6), hasSubmission: true, status: SubmissionStatus.Draft));
        Assert.Equal(UnifiedCycleStatus.Draft, r.Status);
        Assert.Equal(CycleStatusSeverity.Info, r.Severity);
        Assert.False(r.IsLate);
    }

    // U7 — مسودّة بعد الموعد ⇒ OverdueDraft (تأخّر، لا «مُسلَّم»).
    [Fact]
    public void OverdueDraft_WhenDraftPastDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 12), hasSubmission: true, status: SubmissionStatus.Draft));
        Assert.Equal(UnifiedCycleStatus.OverdueDraft, r.Status);
        Assert.Equal(CycleStatusSeverity.Warn, r.Severity);
        Assert.Equal(2, r.DelayDays); // 07-10 23:59 → 07-12 00:00 ⇒ يوم+ ⇒ 2.
    }

    // U8 — Submitted، SubmittedAt ≤ الموعد ⇒ SubmittedOnTime.
    [Fact]
    public void SubmittedOnTime_WhenSubmittedBeforeDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 11), hasSubmission: true, status: SubmissionStatus.Submitted, submittedAt: At(2026, 7, 9, 10, 0)));
        Assert.Equal(UnifiedCycleStatus.SubmittedOnTime, r.Status);
        Assert.Equal(CycleStatusSeverity.Success, r.Severity);
        Assert.False(r.IsLate);
        Assert.Equal(0, r.DelayDays);
    }

    // U9 — Submitted، SubmittedAt > الموعد ⇒ SubmittedLate (تأخّر لكنه مُسلَّم).
    [Fact]
    public void SubmittedLate_WhenSubmittedAfterDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 14), hasSubmission: true, status: SubmissionStatus.Submitted, submittedAt: At(2026, 7, 13, 8, 0)));
        Assert.Equal(UnifiedCycleStatus.SubmittedLate, r.Status);
        Assert.Equal(CycleStatusSeverity.Warn, r.Severity);
        Assert.True(r.IsLate);
        Assert.Equal(3, r.DelayDays); // 07-10 23:59 → 07-13 08:00 ⇒ 2.3 يوم ⇒ 3.
    }

    // U10 — عالق عند معتمِد (اعتماد مباشر/أعلى/تصعيد) ⇒ PendingApproval، لا إجراء على الموظّف.
    [Theory]
    [InlineData(SubmissionStatus.ApprovedByDirectManager)]
    [InlineData(SubmissionStatus.ApprovedByNextLevel)]
    [InlineData(SubmissionStatus.Escalated)]
    public void PendingApproval_WhenAtApprover(SubmissionStatus status)
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 14), hasSubmission: true, status: status, submittedAt: At(2026, 7, 9)));
        Assert.Equal(UnifiedCycleStatus.PendingApproval, r.Status);
        Assert.Equal(CycleStatusSeverity.Info, r.Severity);
    }

    // U11 — Returned قبل الموعد ⇒ ReturnedForChanges (يحتاج إجراء الموظّف ضمن المهلة).
    [Fact]
    public void ReturnedForChanges_WhenReturnedBeforeDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 8), hasSubmission: true, status: SubmissionStatus.Returned));
        Assert.Equal(UnifiedCycleStatus.ReturnedForChanges, r.Status);
        Assert.Equal(CycleStatusSeverity.Warn, r.Severity);
    }

    // U12 — Returned بعد الموعد ⇒ OverdueReturned (معاد وفات موعده).
    [Fact]
    public void OverdueReturned_WhenReturnedPastDue()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 15), hasSubmission: true, status: SubmissionStatus.Returned));
        Assert.Equal(UnifiedCycleStatus.OverdueReturned, r.Status);
        Assert.Equal(CycleStatusSeverity.Alert, r.Severity);
    }

    // U13 — Closed/Visible ⇒ Closed دائمًا، ولا يُوصَف أبدًا كمتجاوِز-بلا-تسليم ولو مرّ الموعد.
    [Theory]
    [InlineData(SubmissionStatus.Closed)]
    [InlineData(SubmissionStatus.Visible)]
    public void Closed_NeverOverdueNotSubmitted(SubmissionStatus status)
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 30), hasSubmission: true, status: status, submittedAt: At(2026, 7, 9)));
        Assert.Equal(UnifiedCycleStatus.Closed, r.Status);
        Assert.Equal(CycleStatusSeverity.Success, r.Severity);
        Assert.False(r.IsLate);
    }

    // U14 — الحذف الناعم يُلغي التسليم: مسودّة محذوفة بعد الموعد ⇒ OverdueNotSubmitted لا OverdueDraft.
    [Fact]
    public void SoftDeletedSubmission_TreatedAsNoSubmission()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 13), hasSubmission: true, status: SubmissionStatus.Draft, isDeleted: true));
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, r.Status);
        Assert.Equal(CycleStatusSeverity.Alert, r.Severity);
    }

    // U15 — الحدّ عند الموعد بالضبط: الوقت == الموعد ليس تجاوزًا (DueNow لا Overdue).
    [Fact]
    public void Boundary_AtExactDue_IsNotOverdue()
    {
        var due = At(2026, 7, 10, 23, 59);
        var r = UnifiedCycleStatusPolicy.Derive(Base(due, dueAt: due));
        Assert.Equal(UnifiedCycleStatus.DueNow, r.Status);
    }

    // U16 — الحدّ: التسليم == الموعد بالضبط ليس تأخّرًا (SubmittedOnTime).
    [Fact]
    public void Boundary_SubmittedExactlyAtDue_IsOnTime()
    {
        var due = At(2026, 7, 10, 23, 59);
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 11), hasSubmission: true, status: SubmissionStatus.Submitted, submittedAt: due, dueAt: due));
        Assert.Equal(UnifiedCycleStatus.SubmittedOnTime, r.Status);
        Assert.False(r.IsLate);
    }

    // U17 — توقيت الرياض: تسليم 07-11 00:30 (+3) بعد موعد 07-10 23:59 (+3) ⇒ متأخّر (فرق المنطقة لا يقلبه).
    [Fact]
    public void RiyadhTimezone_LateAcrossMidnight()
    {
        var due = At(2026, 7, 10, 23, 59);
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 12), hasSubmission: true, status: SubmissionStatus.Submitted, submittedAt: At(2026, 7, 11, 0, 30), dueAt: due));
        Assert.Equal(UnifiedCycleStatus.SubmittedLate, r.Status);
        Assert.True(r.IsLate);
    }

    // U18 — نهاية السنة: دورة W53→W01، لا تسليم بعد الموعد 2026-12-31 ⇒ OverdueNotSubmitted (لا كسر عبر السنة).
    [Fact]
    public void EndOfYear_OverdueNotSubmitted()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2027, 1, 3, 12, 0), cycleStartsAt: At(2026, 12, 26), dueAt: At(2026, 12, 31, 23, 59)));
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, r.Status);
        Assert.Equal(3, r.DelayDays); // 12-31 23:59 → 01-03 12:00 ⇒ 2.5 ⇒ 3.
    }

    // U19 — الطوابع الزمنيّة null: Submitted بلا SubmittedAt يُعامَل كمُسلَّم في الموعد (لا تأخّر يُفترَض).
    [Fact]
    public void NullSubmittedAt_SubmittedTreatedOnTime()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(
            At(2026, 7, 30), hasSubmission: true, status: SubmissionStatus.Submitted, submittedAt: null));
        Assert.Equal(UnifiedCycleStatus.SubmittedOnTime, r.Status);
        Assert.False(r.IsLate);
        Assert.Equal(0, r.DelayDays);
    }

    // U20 — HasSubmission=true لكن Status=null (بيانات ناقصة) ⇒ يُعامَل كأنّه بلا تسليم (منطق الموعد).
    [Fact]
    public void HasSubmissionButNullStatus_TreatedAsNoSubmission()
    {
        var r = UnifiedCycleStatusPolicy.Derive(Base(At(2026, 7, 6), hasSubmission: true, status: null));
        Assert.Equal(UnifiedCycleStatus.DueNow, r.Status);
    }
}

using Reporting.Application.Attendance;
using Reporting.Domain.Enums;

namespace Reporting.UnitTests;

/// <summary>
/// P2-ATT-004/006 — إثبات وحدويّ لآلة حالات الحضور ومُخوِّل فاعليها.
/// الهدف ليس تغطية الجدول بل إثبات **القواعد الحاكمة** التي لا يجوز كسرها.
/// </summary>
public class AttendanceTransitionTests
{
    private readonly AttendanceTransitions _t = new();

    // ── القاعدة الأولى: البلاغ ليس واقعة ──────────────────────────────────────

    [Theory]
    [InlineData(AttendanceIncidentStatus.Draft)]
    [InlineData(AttendanceIncidentStatus.Reported)]
    [InlineData(AttendanceIncidentStatus.AwaitingEmployee)]
    [InlineData(AttendanceIncidentStatus.Acknowledged)]
    [InlineData(AttendanceIncidentStatus.Disputed)]
    [InlineData(AttendanceIncidentStatus.EmployeeResponseTimedOut)]
    [InlineData(AttendanceIncidentStatus.Corrected)]
    public void HrConfirm_IsRejected_FromAnyStateOtherThanAwaitingHr(AttendanceIncidentStatus from)
    {
        var result = _t.Validate(from, AttendanceTrigger.HrConfirm);

        Assert.False(result.Allowed);
        Assert.Equal("attendance.conflict", result.ErrorCode);
    }

    [Fact]
    public void AwaitingHr_IsOnlyReachable_AfterEmployeeRespondedOrWindowElapsed()
    {
        // المصادر الثلاثة الوحيدة لـAwaitingHr: إقرار، اعتراض، انقضاء نافذة.
        var sources = Enum.GetValues<AttendanceIncidentStatus>()
            .Where(s => Enum.GetValues<AttendanceTrigger>()
                .Any(tr => _t.Validate(s, tr).Allowed
                           && _t.Target(s, tr) == AttendanceIncidentStatus.AwaitingHr))
            .ToHashSet();

        Assert.Equal(
            new HashSet<AttendanceIncidentStatus>
            {
                AttendanceIncidentStatus.Acknowledged,
                AttendanceIncidentStatus.Disputed,
                AttendanceIncidentStatus.EmployeeResponseTimedOut
            },
            sources);
    }

    [Fact]
    public void Confirmed_IsNeverReachable_WithoutPassingThroughAwaitingHr()
    {
        var sources = Enum.GetValues<AttendanceIncidentStatus>()
            .Where(s => Enum.GetValues<AttendanceTrigger>()
                .Any(tr => _t.Validate(s, tr).Allowed
                           && _t.Target(s, tr) == AttendanceIncidentStatus.Confirmed))
            .ToArray();

        Assert.Equal(new[] { AttendanceIncidentStatus.AwaitingHr }, sources);
    }

    // ── القاعدة الثانية: المصحَّح يعود إلى الموظّف ─────────────────────────────

    [Fact]
    public void Corrected_ReturnsToEmployee_AndCannotBeConfirmedDirectly()
    {
        Assert.True(_t.Validate(AttendanceIncidentStatus.Corrected, AttendanceTrigger.ReturnToEmployee).Allowed);
        Assert.Equal(
            AttendanceIncidentStatus.AwaitingEmployee,
            _t.Target(AttendanceIncidentStatus.Corrected, AttendanceTrigger.ReturnToEmployee));

        Assert.False(_t.Validate(AttendanceIncidentStatus.Corrected, AttendanceTrigger.HrConfirm).Allowed);
        Assert.False(_t.Validate(AttendanceIncidentStatus.Corrected, AttendanceTrigger.Close).Allowed);
    }

    // ── القاعدة الثالثة: لا حذف صامت بعد الإرسال ──────────────────────────────

    [Fact]
    public void Cancel_IsAllowedOnlyWhileDraft()
    {
        Assert.True(_t.Validate(AttendanceIncidentStatus.Draft, AttendanceTrigger.Cancel).Allowed);

        foreach (var s in Enum.GetValues<AttendanceIncidentStatus>().Where(x => x != AttendanceIncidentStatus.Draft))
            Assert.False(_t.Validate(s, AttendanceTrigger.Cancel).Allowed);
    }

    [Fact]
    public void ConfirmedAndClosed_AreCorrectedOnlyByVoid_NeverDeleted()
    {
        Assert.True(_t.Validate(AttendanceIncidentStatus.Confirmed, AttendanceTrigger.Void).Allowed);
        Assert.True(_t.Validate(AttendanceIncidentStatus.Closed, AttendanceTrigger.Void).Allowed);

        // لا مسار يعيد حادثة مؤكَّدة إلى مسودّة أو يمحوها.
        foreach (var tr in _t.AllowedTriggers(AttendanceIncidentStatus.Confirmed))
            Assert.NotEqual(AttendanceIncidentStatus.Draft, _t.Target(AttendanceIncidentStatus.Confirmed, tr));
    }

    [Fact]
    public void TerminalStates_HaveNoOutgoingTransitions()
    {
        foreach (var s in AttendanceTransitions.Terminal)
            Assert.Empty(_t.AllowedTriggers(s));
    }

    // ── القاعدة الرابعة: الدورة السعيدة كاملة ─────────────────────────────────

    [Fact]
    public void HappyPath_DraftToClosed_IsWalkable()
    {
        var s = AttendanceIncidentStatus.Draft;

        foreach (var tr in new[]
                 {
                     AttendanceTrigger.Submit,
                     AttendanceTrigger.NotifyEmployee,
                     AttendanceTrigger.Acknowledge,
                     AttendanceTrigger.SendToHr,
                     AttendanceTrigger.HrConfirm,
                     AttendanceTrigger.Close
                 })
        {
            Assert.True(_t.Validate(s, tr).Allowed, $"تعثّر عند {s} + {tr}");
            s = _t.Target(s, tr);
        }

        Assert.Equal(AttendanceIncidentStatus.Closed, s);
    }

    [Fact]
    public void DisputePath_ReachesHrReview_AndCanBeReconciled()
    {
        var s = AttendanceIncidentStatus.Draft;

        foreach (var tr in new[]
                 {
                     AttendanceTrigger.Submit,
                     AttendanceTrigger.NotifyEmployee,
                     AttendanceTrigger.Dispute,
                     AttendanceTrigger.SendToHr,
                     AttendanceTrigger.HrReconcile,
                     AttendanceTrigger.Close
                 })
        {
            Assert.True(_t.Validate(s, tr).Allowed, $"تعثّر عند {s} + {tr}");
            s = _t.Target(s, tr);
        }

        Assert.Equal(AttendanceIncidentStatus.Closed, s);
    }

    [Fact]
    public void TimeoutPath_ReachesHrReview_WithoutEmployeeResponse()
    {
        var s = AttendanceIncidentStatus.Draft;
        s = _t.Target(s, AttendanceTrigger.Submit);
        s = _t.Target(s, AttendanceTrigger.NotifyEmployee);
        s = _t.Target(s, AttendanceTrigger.TimeOutEmployeeResponse);

        Assert.Equal(AttendanceIncidentStatus.EmployeeResponseTimedOut, s);
        Assert.True(_t.Validate(s, AttendanceTrigger.SendToHr).Allowed);
    }

    [Fact]
    public void Target_Throws_WhenTransitionIsNotAllowed() =>
        Assert.Throws<InvalidOperationException>(
            () => _t.Target(AttendanceIncidentStatus.Draft, AttendanceTrigger.HrConfirm));
}

/// <summary>P2-ATT-004 — من يملك تشغيل الانتقال (منفصل عن جوازه شكليًّا).</summary>
public class AttendanceActorRulesTests
{
    private readonly AttendanceActorRules _a = new();

    private static AttendanceActorContext Ctx(
        bool isSubject = false, bool isReporter = false, bool canReport = false,
        bool canReview = false, bool canEscalate = false, bool responded = false, bool isSystem = false) =>
        new(Guid.NewGuid(), isSubject, isReporter, canReport, canReview, canEscalate, responded, isSystem);

    [Fact]
    public void Reporter_CannotConfirmOwnReport()
    {
        var result = _a.Authorize(AttendanceTrigger.HrConfirm, Ctx(isReporter: true, canReport: true));

        Assert.False(result.Allowed);
        Assert.Equal("auth.forbidden", result.ErrorCode);
    }

    [Fact]
    public void Subject_CannotPerformHrActions()
    {
        foreach (var tr in new[]
                 {
                     AttendanceTrigger.HrConfirm, AttendanceTrigger.HrReject,
                     AttendanceTrigger.HrCorrect, AttendanceTrigger.HrReconcile,
                     AttendanceTrigger.Void, AttendanceTrigger.Close
                 })
            Assert.False(_a.Authorize(tr, Ctx(isSubject: true)).Allowed);
    }

    [Fact]
    public void Subject_MayAcknowledgeOrDispute_ButNonSubjectMayNot()
    {
        Assert.True(_a.Authorize(AttendanceTrigger.Acknowledge, Ctx(isSubject: true)).Allowed);
        Assert.True(_a.Authorize(AttendanceTrigger.Dispute, Ctx(isSubject: true)).Allowed);

        Assert.False(_a.Authorize(AttendanceTrigger.Acknowledge, Ctx(canReview: true)).Allowed);
        Assert.False(_a.Authorize(AttendanceTrigger.Dispute, Ctx(isReporter: true, canReport: true)).Allowed);
    }

    [Fact]
    public void Withdraw_IsReporterOnly_AndBlockedAfterEmployeeResponded()
    {
        Assert.True(_a.Authorize(AttendanceTrigger.Withdraw, Ctx(isReporter: true)).Allowed);

        Assert.False(_a.Authorize(AttendanceTrigger.Withdraw, Ctx(canReview: true)).Allowed);

        var afterResponse = _a.Authorize(AttendanceTrigger.Withdraw, Ctx(isReporter: true, responded: true));
        Assert.False(afterResponse.Allowed);
        Assert.Equal("attendance.conflict", afterResponse.ErrorCode);
    }

    [Fact]
    public void SystemTransitions_AreNotAvailableToHumans()
    {
        foreach (var tr in new[]
                 {
                     AttendanceTrigger.NotifyEmployee,
                     AttendanceTrigger.TimeOutEmployeeResponse,
                     AttendanceTrigger.SendToHr
                 })
        {
            Assert.False(_a.Authorize(tr, Ctx(canReview: true, canEscalate: true, canReport: true)).Allowed);
            Assert.True(_a.Authorize(tr, Ctx(isSystem: true)).Allowed);
        }
    }

    [Fact]
    public void HrActions_RequireReviewPermission_NotMerelyReportPermission()
    {
        foreach (var tr in new[]
                 {
                     AttendanceTrigger.HrConfirm, AttendanceTrigger.HrReject, AttendanceTrigger.HrCorrect,
                     AttendanceTrigger.HrReconcile, AttendanceTrigger.ReturnToEmployee, AttendanceTrigger.Void
                 })
        {
            Assert.False(_a.Authorize(tr, Ctx(canReport: true, isReporter: true)).Allowed);
            Assert.True(_a.Authorize(tr, Ctx(canReview: true)).Allowed);
        }
    }

    [Fact]
    public void Escalate_RequiresExplicitEscalatePermission()
    {
        Assert.False(_a.Authorize(AttendanceTrigger.Escalate, Ctx(canReview: true)).Allowed);
        Assert.True(_a.Authorize(AttendanceTrigger.Escalate, Ctx(canEscalate: true)).Allowed);
    }
}

/// <summary>P2-ATT-004 — تاريخ الرياض، المدّة المشتقّة، ومواعيد SLA على الأحد→الخميس.</summary>
public class AttendancePolicyTests
{
    [Fact]
    public void RiyadhDate_RollsToNextDay_ForLateUtcEvening()
    {
        // 21:30 UTC = 00:30 بتوقيت الرياض (+3) ⟹ اليوم التالي محلّيًّا.
        var utc = new DateTimeOffset(2026, 8, 25, 21, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 26), AttendancePolicy.RiyadhDate(utc));
    }

    [Fact]
    public void RiyadhDate_KeepsSameDay_ForMiddayUtc()
    {
        var utc = new DateTimeOffset(2026, 8, 25, 09, 00, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 25), AttendancePolicy.RiyadhDate(utc));
    }

    [Theory]
    [InlineData(2026, 8, 23, true)]  // الأحد
    [InlineData(2026, 8, 24, true)]  // الإثنين
    [InlineData(2026, 8, 27, true)]  // الخميس
    [InlineData(2026, 8, 28, false)] // الجمعة
    [InlineData(2026, 8, 29, false)] // السبت
    public void IsWorkingDay_IsSundayThroughThursday(int y, int m, int d, bool expected) =>
        Assert.Equal(expected, AttendancePolicy.IsWorkingDay(new DateOnly(y, m, d)));

    [Fact]
    public void AttendanceWeek_DiffersFromKpiWeek_SaturdayIsNotAWorkingDay() =>
        // أسبوع KPI يبدأ السبت؛ أسبوع الحضور لا يعدّه يوم عمل — استقلال مقصود.
        Assert.False(AttendancePolicy.IsWorkingDay(new DateOnly(2026, 8, 29)));

    [Fact]
    public void ComputeDuration_IsDerivedFromTimes()
    {
        Assert.Equal(45, AttendancePolicy.ComputeDurationMinutes(new TimeOnly(8, 15), new TimeOnly(9, 0)));
        Assert.Equal(0, AttendancePolicy.ComputeDurationMinutes(new TimeOnly(9, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void ComputeDuration_IsNull_WhenIncompleteOrNegative()
    {
        Assert.Null(AttendancePolicy.ComputeDurationMinutes(null, new TimeOnly(9, 0)));
        Assert.Null(AttendancePolicy.ComputeDurationMinutes(new TimeOnly(9, 0), null));
        Assert.Null(AttendancePolicy.ComputeDurationMinutes(null, null));
        Assert.Null(AttendancePolicy.ComputeDurationMinutes(new TimeOnly(10, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void EmployeeResponseDeadline_IsCalendarHours_AndDefaultsTo48()
    {
        var from = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(from.AddHours(48), AttendancePolicy.EmployeeResponseDeadlineUtc(from, 48));
        Assert.Equal(from.AddHours(48), AttendancePolicy.EmployeeResponseDeadlineUtc(from, 0));
        Assert.Equal(from.AddHours(24), AttendancePolicy.EmployeeResponseDeadlineUtc(from, 24));
    }

    [Fact]
    public void HrReviewDeadline_SkipsFridayAndSaturday()
    {
        // الأربعاء 26 أغسطس 2026، 07:00 UTC = 10:00 بالرياض.
        var from = new DateTime(2026, 8, 26, 7, 0, 0, DateTimeKind.Utc);

        var deadline = AttendancePolicy.HrReviewDeadlineUtc(from, 3);
        var localDate = AttendancePolicy.RiyadhDate(new DateTimeOffset(deadline, TimeSpan.Zero));

        // أربعاء +1 = خميس(1)، جمعة وسبت مُتخطَّيان، أحد(2)، إثنين(3).
        Assert.Equal(new DateOnly(2026, 8, 31), localDate);
        Assert.True(AttendancePolicy.IsWorkingDay(localDate));
    }

    [Fact]
    public void HrReviewDeadline_DefaultsToFiveWorkingDays()
    {
        var from = new DateTime(2026, 8, 23, 7, 0, 0, DateTimeKind.Utc); // الأحد

        Assert.Equal(
            AttendancePolicy.HrReviewDeadlineUtc(from, 5),
            AttendancePolicy.HrReviewDeadlineUtc(from, 0));
    }

    [Fact]
    public void WorkingDaysBetween_CountsInclusively_AndExcludesWeekend()
    {
        // الأحد 23 → الأحد 30 أغسطس 2026: أحد..خميس (5) + أحد (1) = 6.
        Assert.Equal(6, AttendancePolicy.WorkingDaysBetween(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 30)));
        Assert.Equal(1, AttendancePolicy.WorkingDaysBetween(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 23)));
        Assert.Equal(0, AttendancePolicy.WorkingDaysBetween(new DateOnly(2026, 8, 28), new DateOnly(2026, 8, 29)));
        Assert.Equal(0, AttendancePolicy.WorkingDaysBetween(new DateOnly(2026, 8, 30), new DateOnly(2026, 8, 23)));
    }
}

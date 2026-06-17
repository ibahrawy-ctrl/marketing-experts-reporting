using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// قواعد تقويم التقارير (Phase 5 §1/§2/§3/§8) كدوال خالصة:
/// الأسبوع التشغيلي الخميس→الأربعاء، تواريخ التسليم بحسب الدور، وانتماء الأسبوع لشهر/ربع/سنة.
/// كلّها قواعد دور/تاريخ — لا تعتمد على اسم مستخدم بعينه.
/// </summary>
public class ReportCalendarPolicyTests
{
    // §14.1 — الأسبوع التشغيلي يبدأ الخميس وينتهي الأربعاء.
    [Fact]
    public void OperationalWeek_StartsThursday_EndsWednesday()
    {
        // 2026-06-20 يوم سبت ⇒ خميس أسبوعه 2026-06-18، أربعاؤه 2026-06-24.
        var anyDay = new DateOnly(2026, 6, 20);
        var start = ReportCalendarPolicy.WeekStart(anyDay);
        var end = ReportCalendarPolicy.WeekEnd(start);

        Assert.Equal(DayOfWeek.Thursday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Wednesday, end.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 6, 18), start);
        Assert.Equal(new DateOnly(2026, 6, 24), end);
        Assert.Equal(6, (end.DayNumber - start.DayNumber)); // 7 أيام
    }

    [Fact]
    public void WeekKeyRoundTrip_IsStable()
    {
        var start = ReportCalendarPolicy.WeekRange("2026-W25").Start;
        Assert.Equal(DayOfWeek.Thursday, start.DayOfWeek);
        Assert.Equal("2026-W25", ReportCalendarPolicy.WeekKeyFor(start));
    }

    // §14.2 — الموظّف يُسلّم بنهاية الأربعاء (نهاية الأسبوع).
    [Fact]
    public void EmployeeDueDate_IsWednesday()
    {
        var (_, end) = ReportCalendarPolicy.WeekRange("2026-W25");
        var due = ReportCalendarPolicy.DueDateForRole("2026-W25", Roles.Employee);
        Assert.Equal(end, due);
        Assert.Equal(DayOfWeek.Wednesday, due.DayOfWeek);
    }

    // §14.3 — قائد الفريق يُسلّم الخميس (نهاية الأسبوع + 1).
    [Fact]
    public void TeamLeaderDueDate_IsThursday()
    {
        var due = ReportCalendarPolicy.DueDateForRole("2026-W25", Roles.TeamLeader);
        Assert.Equal(DayOfWeek.Thursday, due.DayOfWeek);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W25").End.AddDays(1), due);
    }

    // §14.4 — المدير يُسلّم الأحد (نهاية الأسبوع + 4).
    [Fact]
    public void ManagerDueDate_IsSunday()
    {
        var due = ReportCalendarPolicy.DueDateForRole("2026-W25", Roles.Manager);
        Assert.Equal(DayOfWeek.Sunday, due.DayOfWeek);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W25").End.AddDays(4), due);
    }

    // §14.5 — المدير العام/الرئيس التنفيذي يراجعون الاثنين (نهاية الأسبوع + 5).
    [Fact]
    public void GmAndCeoReviewDate_IsMonday()
    {
        var gm = ReportCalendarPolicy.DueDateForRole("2026-W25", Roles.GeneralManager);
        var ceo = ReportCalendarPolicy.DueDateForRole("2026-W25", Roles.Ceo);
        Assert.Equal(DayOfWeek.Monday, gm.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, ceo.DayOfWeek);
        Assert.Equal(gm, ceo);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W25").End.AddDays(5), gm);
    }

    // §14.15 — متوسط الشهر يُحتسب من أسابيع تقع داخل حدود الشهر (انتماء بخميس البداية).
    [Fact]
    public void MonthRange_ContainsWeeksAnchoredInThatMonth()
    {
        var (from, to) = ReportCalendarPolicy.MonthRange(2026, 6);
        Assert.Equal(new DateOnly(2026, 6, 1), from);
        Assert.Equal(new DateOnly(2026, 6, 30), to);
        // أسبوع خميسه 2026-06-18 ينتمي ليونيو، وأسبوع خميسه 2026-07-02 لا.
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W25", from, to));
        var julyKey = ReportCalendarPolicy.WeekKeyFor(new DateOnly(2026, 7, 2));
        Assert.False(ReportCalendarPolicy.WeekInRange(julyKey, from, to));
    }

    // §14.16 — حدود الربع تغطّي ثلاثة أشهر.
    [Fact]
    public void QuarterRange_CoversThreeMonths()
    {
        var (from, to) = ReportCalendarPolicy.QuarterRange(2026, 2);
        Assert.Equal(new DateOnly(2026, 4, 1), from);
        Assert.Equal(new DateOnly(2026, 6, 30), to);
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W25", from, to));
    }

    // §14.17 — حدود السنة تغطّي العام كاملًا.
    [Fact]
    public void YearRange_CoversWholeYear()
    {
        var (from, to) = ReportCalendarPolicy.YearRange(2026);
        Assert.Equal(new DateOnly(2026, 1, 1), from);
        Assert.Equal(new DateOnly(2026, 12, 31), to);
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W25", from, to));
        Assert.False(ReportCalendarPolicy.WeekInRange("2025-W25", from, to));
    }

    [Theory]
    [InlineData("2026-W25", true)]
    [InlineData("2026-06", false)]
    [InlineData("الأسبوع الأول", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWeekKey_ValidatesFormat(string? key, bool expected)
        => Assert.Equal(expected, ReportCalendarPolicy.IsWeekKey(key));
}

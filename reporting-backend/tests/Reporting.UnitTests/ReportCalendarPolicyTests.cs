using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// ROLE-AWARE-REPORTING-CALENDAR — واجهة التوافق <see cref="ReportCalendarPolicy"/> تفوّض بالكامل
/// إلى السياسة المُدرِكة للأدوار (مرساة **السبت**). هذه الاختبارات تتحقّق أنّ التفويض يُعيد إنتاج
/// جدول الإدارة المعتمد (الأسبوع يبدأ السبت وينتهي الجمعة) وأنّ تواريخ الاستحقاق بحسب الدور مطابقة،
/// وأنّه **لا ارتداد** لمرساة الخميس القديمة. كلّها قواعد دور/تاريخ — لا تعتمد على اسم مستخدم بعينه.
/// </summary>
public class ReportCalendarPolicyTests
{
    // الأسبوع التشغيلي عبر الواجهة يبدأ السبت وينتهي الجمعة (لا ارتداد للخميس).
    [Fact]
    public void OperationalWeek_StartsSaturday_EndsFriday()
    {
        // 2026-06-30 يوم ثلاثاء ⇒ سبت أسبوعه 2026-06-27، جمعته 2026-07-03 (الأسبوع 27).
        var anyDay = new DateOnly(2026, 6, 30);
        var start = ReportCalendarPolicy.WeekStart(anyDay);
        var end = ReportCalendarPolicy.WeekEnd(start);

        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, end.DayOfWeek);
        Assert.NotEqual(DayOfWeek.Thursday, start.DayOfWeek); // لا ارتداد للخميس
        Assert.Equal(new DateOnly(2026, 6, 27), start);
        Assert.Equal(new DateOnly(2026, 7, 3), end);
        Assert.Equal(6, (end.DayNumber - start.DayNumber)); // 7 أيام
    }

    [Fact]
    public void WeekKeyRoundTrip_IsStable()
    {
        var start = ReportCalendarPolicy.WeekRange("2026-W27").Start;
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 6, 27), start);
        Assert.Equal("2026-W27", ReportCalendarPolicy.WeekKeyFor(start));
    }

    // الموظّف يُسلّم الأربعاء (بداية السبت + 4).
    [Fact]
    public void EmployeeDueDate_IsWednesday()
    {
        var due = ReportCalendarPolicy.DueDateForRole("2026-W27", Roles.Employee);
        Assert.Equal(DayOfWeek.Wednesday, due.DayOfWeek);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W27").Start.AddDays(4), due);
    }

    // قائد الفريق يُسلّم الخميس (بداية السبت + 5).
    [Fact]
    public void TeamLeaderDueDate_IsThursday()
    {
        var due = ReportCalendarPolicy.DueDateForRole("2026-W27", Roles.TeamLeader);
        Assert.Equal(DayOfWeek.Thursday, due.DayOfWeek);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W27").Start.AddDays(5), due);
    }

    // المدير يُسلّم الأحد (بداية السبت + 8).
    [Fact]
    public void ManagerDueDate_IsSunday()
    {
        var due = ReportCalendarPolicy.DueDateForRole("2026-W27", Roles.Manager);
        Assert.Equal(DayOfWeek.Sunday, due.DayOfWeek);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W27").Start.AddDays(8), due);
    }

    // المدير العام/الرئيس التنفيذي يراجعون الاثنين (بداية السبت + 9).
    [Fact]
    public void GmAndCeoReviewDate_IsMonday()
    {
        var gm = ReportCalendarPolicy.DueDateForRole("2026-W27", Roles.GeneralManager);
        var ceo = ReportCalendarPolicy.DueDateForRole("2026-W27", Roles.Ceo);
        Assert.Equal(DayOfWeek.Monday, gm.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, ceo.DayOfWeek);
        Assert.Equal(gm, ceo);
        Assert.Equal(ReportCalendarPolicy.WeekRange("2026-W27").Start.AddDays(9), gm);
    }

    // متوسط الشهر يُحتسب من أسابيع تقع داخل حدود الشهر (انتماء بمرجع الثلاثاء).
    [Fact]
    public void MonthRange_ContainsWeeksAnchoredInThatMonth()
    {
        var (from, to) = ReportCalendarPolicy.MonthRange(2026, 6);
        Assert.Equal(new DateOnly(2026, 6, 1), from);
        Assert.Equal(new DateOnly(2026, 6, 30), to);
        // W27 مرجع ثلاثائه 2026-06-30 ينتمي ليونيو، وأسبوع 2026-07-04 (ثلاثاؤه في يوليو) لا.
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W27", from, to));
        var julyKey = ReportCalendarPolicy.WeekKeyFor(new DateOnly(2026, 7, 4));
        Assert.False(ReportCalendarPolicy.WeekInRange(julyKey, from, to));
    }

    // حدود الربع تغطّي ثلاثة أشهر.
    [Fact]
    public void QuarterRange_CoversThreeMonths()
    {
        var (from, to) = ReportCalendarPolicy.QuarterRange(2026, 2);
        Assert.Equal(new DateOnly(2026, 4, 1), from);
        Assert.Equal(new DateOnly(2026, 6, 30), to);
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W27", from, to));
    }

    // حدود السنة تغطّي العام كاملًا.
    [Fact]
    public void YearRange_CoversWholeYear()
    {
        var (from, to) = ReportCalendarPolicy.YearRange(2026);
        Assert.Equal(new DateOnly(2026, 1, 1), from);
        Assert.Equal(new DateOnly(2026, 12, 31), to);
        Assert.True(ReportCalendarPolicy.WeekInRange("2026-W27", from, to));
        Assert.False(ReportCalendarPolicy.WeekInRange("2025-W27", from, to));
    }

    [Theory]
    [InlineData("2026-W27", true)]
    [InlineData("2026-06", false)]
    [InlineData("الأسبوع الأول", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWeekKey_ValidatesFormat(string? key, bool expected)
        => Assert.Equal(expected, ReportCalendarPolicy.IsWeekKey(key));
}

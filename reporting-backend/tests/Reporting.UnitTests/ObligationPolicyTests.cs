using Reporting.Application.Common;
using Reporting.Application.Obligations;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// P2-HR-008 — اختبارات المنطق النقيّ لمحرّك الالتزامات: الحدود، والتأخّر، وعدم الإسناد، والإعفاء.
/// كلّها حتميّة لأنّ «اليوم» يُحقَن ولا يُقرَأ من الساعة.
/// </summary>
public class ObligationPolicyTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // ===== ① عدم الإسناد — القاعدة غير القابلة للتفاوض =====

    [Fact]
    public void No_Assignment_Is_Never_Missing_Nor_Late_Even_Long_After_Due()
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: false, isUserActive: true, isWithinApplicability: true,
            isExemptByLeave: false, isFulfilled: false,
            dueAt: D(2026, 1, 1), fulfilledOn: null, today: D(2026, 12, 31));

        Assert.False(outcome.Expected);
        Assert.False(outcome.Missing);
        Assert.False(outcome.Late);
        Assert.Equal(0, outcome.LateByDays);
        Assert.Equal(ObligationState.NotApplicable, outcome.State);
        Assert.Equal(ObligationExemptionReason.NotAssigned, outcome.ExemptionReason);
    }

    [Fact]
    public void No_Assignment_Outranks_Every_Other_Condition()
    {
        // غير نشط + خارج الانطباق + إجازة: يبقى «لا إسناد» لأنّه يُفحَص أوّلًا.
        var outcome = ObligationPolicy.Derive(
            isAssigned: false, isUserActive: false, isWithinApplicability: false,
            isExemptByLeave: true, isFulfilled: false,
            dueAt: D(2026, 5, 5), fulfilledOn: null, today: D(2026, 6, 6));

        Assert.Equal(ObligationExemptionReason.NotAssigned, outcome.ExemptionReason);
    }

    // ===== ② حدود التأخّر — يوم الاستحقاق نفسه ليس تأخّرًا =====

    [Fact]
    public void Due_Day_Itself_Is_Still_Within_Deadline()
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: true, isUserActive: true, isWithinApplicability: true,
            isExemptByLeave: false, isFulfilled: false,
            dueAt: D(2026, 8, 20), fulfilledOn: null, today: D(2026, 8, 20));

        Assert.True(outcome.Expected);
        Assert.False(outcome.Missing);
        Assert.False(outcome.Late);
        Assert.Equal(ObligationState.Pending, outcome.State);
    }

    [Fact]
    public void One_Day_After_Due_Is_Missing_And_Late_By_Exactly_One()
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: true, isUserActive: true, isWithinApplicability: true,
            isExemptByLeave: false, isFulfilled: false,
            dueAt: D(2026, 8, 20), fulfilledOn: null, today: D(2026, 8, 21));

        Assert.True(outcome.Expected);
        Assert.True(outcome.Missing);
        Assert.True(outcome.Late);
        Assert.Equal(1, outcome.LateByDays);
        Assert.Equal(ObligationState.Missing, outcome.State);
    }

    [Fact]
    public void Fulfilled_On_The_Due_Day_Is_Not_Late()
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: true, isUserActive: true, isWithinApplicability: true,
            isExemptByLeave: false, isFulfilled: true,
            dueAt: D(2026, 8, 20), fulfilledOn: D(2026, 8, 20), today: D(2026, 8, 25));

        Assert.True(outcome.Fulfilled);
        Assert.False(outcome.Late);
        Assert.False(outcome.Missing);
        Assert.Equal(ObligationState.Fulfilled, outcome.State);
    }

    [Fact]
    public void Fulfilled_After_Due_Is_Late_But_Never_Missing()
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: true, isUserActive: true, isWithinApplicability: true,
            isExemptByLeave: false, isFulfilled: true,
            dueAt: D(2026, 8, 20), fulfilledOn: D(2026, 8, 23), today: D(2026, 8, 25));

        Assert.True(outcome.Fulfilled);
        Assert.True(outcome.Late);
        Assert.Equal(3, outcome.LateByDays);
        // «ناقص» و«مُنجَز متأخّرًا» حالتان متمايزتان: المتأخّر أُنجِز فلا يُعَدّ نقصًا.
        Assert.False(outcome.Missing);
        Assert.Equal(ObligationState.Fulfilled, outcome.State);
    }

    // ===== ③ الإعفاءات — تُفحَص قبل حساب التأخّر فلا يتولّد نقصٌ ثمّ يُعفى =====

    [Theory]
    [InlineData(false, true, false, ObligationExemptionReason.InactiveUser)]
    [InlineData(true, false, false, ObligationExemptionReason.BeforeApplicabilityFloor)]
    [InlineData(true, true, true, ObligationExemptionReason.ApprovedLeave)]
    public void Exemptions_Suppress_Missing_And_Late_Entirely(
        bool active, bool withinApplicability, bool exemptByLeave, ObligationExemptionReason expected)
    {
        var outcome = ObligationPolicy.Derive(
            isAssigned: true, isUserActive: active, isWithinApplicability: withinApplicability,
            isExemptByLeave: exemptByLeave, isFulfilled: false,
            dueAt: D(2026, 8, 20), fulfilledOn: null, today: D(2026, 9, 30));

        Assert.Equal(ObligationState.Exempt, outcome.State);
        Assert.Equal(expected, outcome.ExemptionReason);
        Assert.False(outcome.Expected);
        Assert.False(outcome.Missing);
        Assert.False(outcome.Late);
        Assert.Equal(0, outcome.LateByDays);
    }

    // ===== ④ تغطية الإجازة المعتمَدة =====

    [Fact]
    public void Full_Coverage_Of_Period_Through_Due_Date_Exempts()
    {
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[] { new ObligationPolicy.DateSpan(D(2026, 8, 10), D(2026, 8, 25)) });

        Assert.True(covered);
    }

    [Fact]
    public void Coverage_Ending_On_The_Due_Day_Is_Enough()
    {
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[] { new ObligationPolicy.DateSpan(D(2026, 8, 16), D(2026, 8, 20)) });

        Assert.True(covered);
    }

    [Fact]
    public void Partial_Coverage_Does_Not_Exempt()
    {
        // يوم عمل واحد متاح داخل المهلة يكفي لبقاء الالتزام قائمًا.
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[] { new ObligationPolicy.DateSpan(D(2026, 8, 16), D(2026, 8, 19)) });

        Assert.False(covered);
    }

    [Fact]
    public void A_Gap_Between_Two_Leaves_Breaks_Coverage()
    {
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[]
            {
                new ObligationPolicy.DateSpan(D(2026, 8, 16), D(2026, 8, 17)),
                new ObligationPolicy.DateSpan(D(2026, 8, 19), D(2026, 8, 22))
            });

        Assert.False(covered); // 18 أغسطس غير مغطّى.
    }

    [Fact]
    public void Two_Adjacent_Leaves_Merge_Into_Full_Coverage()
    {
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[]
            {
                new ObligationPolicy.DateSpan(D(2026, 8, 16), D(2026, 8, 18)),
                new ObligationPolicy.DateSpan(D(2026, 8, 19), D(2026, 8, 21))
            });

        Assert.True(covered);
    }

    [Fact]
    public void Leave_Entirely_Outside_The_Window_Never_Exempts()
    {
        var covered = ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20),
            new[] { new ObligationPolicy.DateSpan(D(2026, 9, 1), D(2026, 9, 30)) });

        Assert.False(covered);
    }

    [Fact]
    public void No_Leaves_At_All_Is_Never_Covered()
    {
        Assert.False(ObligationPolicy.IsCoveredByApprovedLeave(
            D(2026, 8, 16), D(2026, 8, 20), Array.Empty<ObligationPolicy.DateSpan>()));
    }

    // ===== ⑤ التقويم الربعيّ — تفويض لا تكرار =====

    [Fact]
    public void Quarterly_Due_Date_Is_Quarter_End_Plus_Fixed_Grace()
    {
        var range = ObligationPolicy.QuarterRange("2026-Q3");
        var due = ObligationPolicy.QuarterlyDueDate("2026-Q3");

        Assert.Equal(range.End.AddDays(ObligationPolicy.QuarterlyEvaluationGraceDays), due);
    }

    [Fact]
    public void Quarter_Range_Matches_The_Shared_Calendar_Policy()
    {
        // إثبات التفويض: لا حساب مستقلّ لحدود الربع في محرّك الالتزامات.
        Assert.Equal(ReportingCalendarPolicy.QuarterRange(2026, 2), ObligationPolicy.QuarterRange("2026-Q2"));
    }

    [Fact]
    public void A_Cycle_Belongs_To_Exactly_One_Quarter_Via_Its_Tuesday_Reference()
    {
        var cycleKey = ReportingCalendarPolicy.CycleKeyFor(D(2026, 8, 18));
        var quarterKey = ObligationPolicy.QuarterKeyForCycle(cycleKey);

        Assert.Equal("2026-Q3", quarterKey);
    }

    [Fact]
    public void Cycle_Straddling_A_Quarter_Boundary_Is_Not_Counted_Twice()
    {
        // دورة تعبر حدّ الربع تُنسَب لربع واحد فقط (مرجع الثلاثاء يحسم).
        var cycleKey = ReportingCalendarPolicy.CycleKeyFor(D(2026, 3, 31));
        var quarterKey = ObligationPolicy.QuarterKeyForCycle(cycleKey);

        var start = ReportingCalendarPolicy.CycleRange(cycleKey).Start;
        var tuesday = ReportingCalendarPolicy.TuesdayReference(start);
        var expectedQuarter = ((tuesday.Month - 1) / 3) + 1;

        Assert.Equal($"{tuesday.Year:D4}-Q{expectedQuarter}", quarterKey);
    }

    [Fact]
    public void Cycle_Cap_Is_A_Structural_Ceiling_Not_A_Setting()
    {
        Assert.True(ObligationPolicy.DefaultRecentCycles <= ObligationPolicy.MaxCycles);
        Assert.True(ObligationPolicy.MaxCycles > 0);
    }
}

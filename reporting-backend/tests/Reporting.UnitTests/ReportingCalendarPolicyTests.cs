using System.Globalization;
using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.7 — اختبارات انحدار للسياسة المُدرِكة للأدوار
/// (<see cref="ReportingCalendarPolicy"/>، مرساة **السبت**). تتحقّق من:
///  (1) جدول الإدارة المعتمد W27→W33 (كل دورة تبدأ السبت الصحيح)؛
///  (2) كل المستويات لنفس الفترة تحمل **نفس مفتاح الدورة**؛
///  (3) تواريخ الاستحقاق بحسب الدور (الموظّف=الأربعاء، قائد الفريق=الخميس، المدير=الأحد، GM/CEO/Admin=الاثنين)؛
///  (4) **لا ارتداد** لمرساة الخميس القديمة؛ (5) الترقيم عبر 2026/2027/2028؛
///  (6) حدود السنة W52/W53→W01؛ (7) السنة الكبيسة 2028؛ (8) توقيت الرياض UTC+3؛
///  (9) نافذة الدورة ≠ تاريخ الاستحقاق؛ (10) التأخّر لا يغيّر المفتاح (المفتاح ثابت لكل أيام الدورة).
/// كلّها دوال خالصة — لا تعتمد على اسم مستخدم بعينه ولا على حالة قاعدة بيانات.
/// </summary>
public class ReportingCalendarPolicyTests
{
    // (1) جدول الإدارة المعتمد: W27→السبت 27 يونيو 2026 … W33→السبت 8 أغسطس 2026.
    [Theory]
    [InlineData("2026-W27", 2026, 6, 27, 2026, 7, 3)]
    [InlineData("2026-W28", 2026, 7, 4, 2026, 7, 10)]
    [InlineData("2026-W29", 2026, 7, 11, 2026, 7, 17)]
    [InlineData("2026-W30", 2026, 7, 18, 2026, 7, 24)]
    [InlineData("2026-W31", 2026, 7, 25, 2026, 7, 31)]
    [InlineData("2026-W32", 2026, 8, 1, 2026, 8, 7)]
    [InlineData("2026-W33", 2026, 8, 8, 2026, 8, 14)]
    public void ApprovedManagementCalendar_W27ToW33_SaturdayAnchors(
        string cycleKey, int sy, int sm, int sd, int ey, int em, int ed)
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange(cycleKey);
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, end.DayOfWeek);
        Assert.Equal(new DateOnly(sy, sm, sd), start);
        Assert.Equal(new DateOnly(ey, em, ed), end);
        // المفتاح ثابت ذهابًا وإيابًا.
        Assert.Equal(cycleKey, ReportingCalendarPolicy.CycleKeyFor(start));
        Assert.Equal(cycleKey, ReportingCalendarPolicy.CycleKeyFor(end));
    }

    // (2) كل المستويات لنفس التاريخ تحمل نفس مفتاح الدورة (المفتاح مستقلّ عن الدور).
    [Fact]
    public void AllLevels_SamePeriod_ShareSameCycleKey()
    {
        var anyDayInCycle = new DateOnly(2026, 6, 30); // ثلاثاء داخل W27
        var key = ReportingCalendarPolicy.CycleKeyFor(anyDayInCycle);
        Assert.Equal("2026-W27", key);

        // تواريخ استحقاق الأدوار كلّها تُشتقّ من نفس بداية الدورة (نفس السبت).
        var start = ReportingCalendarPolicy.CycleRange(key).Start;
        foreach (var role in new[] { Roles.Employee, Roles.TeamLeader, Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin })
        {
            var due = ReportingCalendarPolicy.RoleDueDate(key, role);
            Assert.Equal(start.AddDays(ReportingCalendarPolicy.RoleDueOffset(role)), due);
        }
    }

    // (3) تواريخ الاستحقاق بحسب الدور من بداية السبت.
    [Theory]
    [InlineData(Roles.Employee, 4, DayOfWeek.Wednesday)]
    [InlineData(Roles.TeamLeader, 5, DayOfWeek.Thursday)]
    [InlineData(Roles.Manager, 8, DayOfWeek.Sunday)]
    [InlineData(Roles.GeneralManager, 9, DayOfWeek.Monday)]
    [InlineData(Roles.Ceo, 9, DayOfWeek.Monday)]
    [InlineData(Roles.Admin, 9, DayOfWeek.Monday)]
    public void RoleDueDate_MatchesOffsetAndWeekday(string role, int offset, DayOfWeek weekday)
    {
        Assert.Equal(offset, ReportingCalendarPolicy.RoleDueOffset(role));
        var due = ReportingCalendarPolicy.RoleDueDate("2026-W27", role);
        Assert.Equal(weekday, due.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 6, 27).AddDays(offset), due);
    }

    // (4) لا ارتداد لمرساة الخميس: بداية الدورة لأيّ تاريخ يجب أن تكون السبت.
    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 6, 18)] // كان خميسًا في التقويم القديم
    [InlineData(2027, 3, 15)]
    [InlineData(2028, 2, 29)] // يوم كبيس
    [InlineData(2028, 12, 31)]
    public void CycleStart_IsAlwaysSaturday_NoThursdayRegression(int y, int m, int d)
    {
        var start = ReportingCalendarPolicy.CycleStart(new DateOnly(y, m, d));
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.NotEqual(DayOfWeek.Thursday, start.DayOfWeek);
    }

    // (5)+(7) الترقيم مستقرّ ذهابًا وإيابًا عبر 2026/2027/2028 (شاملًا يوم كبيس).
    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 6, 27)]
    [InlineData(2026, 12, 31)]
    [InlineData(2027, 1, 1)]
    [InlineData(2027, 7, 4)]
    [InlineData(2028, 2, 29)]
    [InlineData(2028, 8, 8)]
    [InlineData(2028, 12, 31)]
    public void CycleKey_RoundTrips_AcrossYears(int y, int m, int d)
    {
        var date = new DateOnly(y, m, d);
        var key = ReportingCalendarPolicy.CycleKeyFor(date);
        Assert.True(ReportingCalendarPolicy.IsValidCycleKey(key));
        var (start, end) = ReportingCalendarPolicy.CycleRange(key);
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, end.DayOfWeek);
        Assert.True(date >= start && date <= end); // التاريخ داخل نافذة دورته
        Assert.Equal(key, ReportingCalendarPolicy.CycleKeyFor(start));
    }

    // (6) حدود السنة: 2026 لها 53 أسبوع ISO؛ الدورة التالية لـ W53 هي 2027-W01.
    [Fact]
    public void YearBoundary_W53_RollsOverTo_NextYearW01()
    {
        Assert.Equal(53, ISOWeek.GetWeeksInYear(2026));

        // دورة أواخر ديسمبر 2026 = W53 (مرجع ثلاثائها 29 ديسمبر 2026).
        var lateDec = new DateOnly(2026, 12, 28);
        Assert.Equal("2026-W53", ReportingCalendarPolicy.CycleKeyFor(lateDec));

        // الدورة التالية (سبت 2 يناير 2027) = 2027-W01.
        var (start, _) = ReportingCalendarPolicy.CycleRange("2026-W53");
        var nextStart = start.AddDays(7);
        Assert.Equal(new DateOnly(2027, 1, 2), nextStart);
        Assert.Equal("2027-W01", ReportingCalendarPolicy.CycleKeyFor(nextStart));

        // W53 صالح لـ2026، وغير صالح لسنة ذات 52 أسبوعًا (2025).
        Assert.True(ReportingCalendarPolicy.IsValidCycleKey("2026-W53"));
        Assert.False(ReportingCalendarPolicy.IsValidCycleKey("2025-W53"));
    }

    // (8) توقيت الرياض ثابت UTC+3 بلا توقيت صيفي.
    [Fact]
    public void Riyadh_IsFixedUtcPlus3()
    {
        Assert.Equal(TimeSpan.FromHours(3), ReportingCalendarPolicy.RiyadhOffset);
        // لحظة UTC 2026-06-26T22:00Z ⇒ بتوقيت الرياض 2026-06-27T01:00 ⇒ التاريخ 27 يونيو.
        var utc = new DateTime(2026, 6, 26, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 6, 27), ReportingCalendarPolicy.RiyadhDate(utc));
    }

    // (9) نافذة الدورة (السبت→الجمعة) تختلف عن تاريخ استحقاق الدور:
    //     المدير/المدير العام يستحقّان بعد نهاية النافذة (الأحد/الاثنين)، والموظّف داخلها (الأربعاء).
    [Fact]
    public void CycleWindow_DiffersFrom_RoleDueDate()
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange("2026-W27");
        var empDue = ReportingCalendarPolicy.RoleDueDate("2026-W27", Roles.Employee);
        var mgrDue = ReportingCalendarPolicy.RoleDueDate("2026-W27", Roles.Manager);
        var gmDue = ReportingCalendarPolicy.RoleDueDate("2026-W27", Roles.GeneralManager);

        Assert.True(empDue >= start && empDue <= end);      // الموظّف داخل النافذة
        Assert.True(mgrDue > end);                            // المدير بعد نهاية النافذة
        Assert.True(gmDue > end);                             // المدير العام بعد نهاية النافذة
        Assert.Equal(new DateOnly(2026, 7, 3), end);          // الجمعة
        Assert.Equal(new DateOnly(2026, 7, 5), mgrDue);       // الأحد
        Assert.Equal(new DateOnly(2026, 7, 6), gmDue);        // الاثنين

        // نافذة تغطية البيانات الافتراضية = نافذة الدورة نفسها.
        Assert.Equal((start, end), ReportingCalendarPolicy.DataCoverageWindow("2026-W27"));
    }

    // (10) التأخّر لا يغيّر المفتاح: كل يوم داخل الدورة يُنتج نفس المفتاح بصرف النظر عن تاريخ الاستحقاق.
    [Fact]
    public void Lateness_DoesNotChangeCycleKey()
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange("2026-W27");
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            Assert.Equal("2026-W27", ReportingCalendarPolicy.CycleKeyFor(day));
        }
    }
}

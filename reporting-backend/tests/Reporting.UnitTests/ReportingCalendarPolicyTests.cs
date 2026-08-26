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

    // ===== الوضع اليوميّ (Daily) — DAILY REPORTING CALENDAR — PHASE 2 EXTENSION =====

    // (D1) توليد مفتاح اليوم YYYY-MM-DD (Invariant، لا يتأثّر بالثقافة).
    [Theory]
    [InlineData(2026, 7, 14, "2026-07-14")]
    [InlineData(2026, 1, 5, "2026-01-05")]
    [InlineData(2028, 2, 29, "2028-02-29")] // يوم كبيس
    [InlineData(2026, 12, 31, "2026-12-31")]
    public void DayKey_Formats_YyyyMmDd(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.DayKey(new DateOnly(y, m, d)));
    }

    // (D2) صلاحية مفتاح اليوم بنيويًّا + رفض القيم غير الصالحة (round-trip يرفض 30 فبراير/الشهر 13).
    [Theory]
    [InlineData("2026-07-14", true)]
    [InlineData("2028-02-29", true)]  // كبيسة صالحة
    [InlineData("2026-02-30", false)] // لا يوجد
    [InlineData("2026-13-01", false)] // شهر غير صالح
    [InlineData("2027-02-29", false)] // غير كبيسة
    [InlineData("2026-7-14", false)]  // بلا أصفار بادئة
    [InlineData("14-07-2026", false)] // ترتيب خاطئ
    [InlineData("2026/07/14", false)] // فواصل خاطئة
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidDayKey_AcceptsValid_RejectsInvalid(string? key, bool expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.IsValidDayKey(key));
    }

    // (D3) تحليل مفتاح اليوم ذهابًا وإيابًا.
    [Fact]
    public void ParseDayKey_RoundTrips()
    {
        var date = new DateOnly(2026, 7, 14);
        var key = ReportingCalendarPolicy.DayKey(date);
        Assert.Equal(date, ReportingCalendarPolicy.ParseDayKey(key));
        Assert.Equal(key, ReportingCalendarPolicy.DayKey(ReportingCalendarPolicy.ParseDayKey(key)));
    }

    // (D4) DAILY-BUSINESS-DAY-COMPLIANCE-R1: العطلة الأسبوعية اليومية = **الجمعة والسبت**
    // (أيّام العمل اليومية = الأحد→الخميس فقط تدخل التوقّع والالتزام).
    [Theory]
    [InlineData(2026, 7, 10, true)]  // جمعة  → عطلة
    [InlineData(2026, 7, 11, true)]  // سبت   → عطلة
    [InlineData(2026, 7, 12, false)] // أحد
    [InlineData(2026, 7, 13, false)] // اثنين
    [InlineData(2026, 7, 14, false)] // ثلاثاء
    [InlineData(2026, 7, 15, false)] // أربعاء
    [InlineData(2026, 7, 16, false)] // خميس
    public void IsDailyHoliday_FridayAndSaturday(int y, int m, int d, bool expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.IsDailyHoliday(new DateOnly(y, m, d)));
    }

    // (D4-b) عقد أيّام العمل: الجمعة والسبت عطلة، ومعكوسها IsDailyExpectedBusinessDay للأحد→الخميس.
    [Fact]
    public void IsDailyHoliday_FridaySaturday_True_BusinessDays_ExpectedTrue()
    {
        var friday = new DateOnly(2026, 7, 10);
        var saturday = new DateOnly(2026, 7, 11);
        var sunday = new DateOnly(2026, 7, 12);
        var thursday = new DateOnly(2026, 7, 16);
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(friday));
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(saturday));
        Assert.False(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(friday));
        Assert.False(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(saturday));
        Assert.True(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(sunday));
        Assert.True(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(thursday));
        // مفتاح السبت لا يزال يُولَّد ويُقرأ صحيحًا (التقارير الفعليّة القديمة تبقى مقروءة — §5).
        Assert.Equal("2026-07-11", ReportingCalendarPolicy.DayKey(saturday));
        Assert.True(ReportingCalendarPolicy.IsValidDayKey("2026-07-11"));
    }

    // (D4-d) DAILY-BUSINESS-DAY-COMPLIANCE-R1 (قرار إنشاء يوم السبت + اختبار #23):
    // بوابة إنشاء التقرير **مستقلّة** عن عقد التوقّع/الالتزام — الجمعة وحدها محظورة للإنشاء،
    // والسبت **غير محظور** (يبقى مسموحًا كتقرير فعليّ طوعيّ) رغم أنه عطلة توقّع/التزام.
    [Theory]
    [InlineData(2026, 7, 10, true)]  // جمعة → محظورة للإنشاء
    [InlineData(2026, 7, 11, false)] // سبت  → مسموح للإنشاء (السلوك السابق محفوظ)
    [InlineData(2026, 7, 12, false)] // أحد
    [InlineData(2026, 7, 13, false)] // اثنين
    [InlineData(2026, 7, 16, false)] // خميس
    public void IsDailySubmissionBlockedDay_FridayOnly(int y, int m, int d, bool expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.IsDailySubmissionBlockedDay(new DateOnly(y, m, d)));
    }

    // (D4-e) الفصل الصريح بين السياستين: السبت عطلة توقّع (IsDailyHoliday) لكنه ليس يوم إنشاء محظور.
    [Fact]
    public void Saturday_IsHolidayForExpected_ButNotBlockedForSubmission()
    {
        var saturday = new DateOnly(2026, 7, 11);
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(saturday));            // خارج التوقّع/الالتزام
        Assert.False(ReportingCalendarPolicy.IsDailySubmissionBlockedDay(saturday)); // لكن الإنشاء مسموح
        var friday = new DateOnly(2026, 7, 10);
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(friday));
        Assert.True(ReportingCalendarPolicy.IsDailySubmissionBlockedDay(friday));  // الجمعة محظورة للإنشاء
    }

    // (D4-c) DailyExpectedDates لدورة W28 = 5 أيّام عمل فقط (الأحد 05 → الخميس 09)،
    // مستبعِدةً السبت 04 (أرضية+عطلة) والجمعة 10 (عطلة).
    [Fact]
    public void DailyExpectedDates_W28_FiveBusinessDays_ExcludesFridaySaturday()
    {
        var today = new DateOnly(2026, 7, 24);
        var dates = ReportingCalendarPolicy.DailyExpectedDates("2026-W28", today);
        Assert.Equal(5, dates.Count);
        Assert.Equal(new DateOnly(2026, 7, 5), dates[0]);   // الأحد
        Assert.Equal(new DateOnly(2026, 7, 9), dates[^1]);  // الخميس
        Assert.DoesNotContain(new DateOnly(2026, 7, 4), dates);   // السبت (أرضية الإطلاق + عطلة)
        Assert.DoesNotContain(new DateOnly(2026, 7, 10), dates);  // الجمعة
        Assert.All(dates, d => Assert.True(d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday)));
    }

    // (D5) أسماء الأيام العربية + التسمية الكاملة «الثلاثاء 14 يوليو 2026».
    [Theory]
    [InlineData(2026, 7, 14, "الثلاثاء", "الثلاثاء 14 يوليو 2026")]
    [InlineData(2026, 7, 10, "الجمعة", "الجمعة 10 يوليو 2026")]
    [InlineData(2026, 7, 11, "السبت", "السبت 11 يوليو 2026")]
    [InlineData(2026, 1, 4, "الأحد", "الأحد 4 يناير 2026")]
    public void ArFullDateLabel_MatchesArabicDayAndMonth(int y, int m, int d, string dayName, string full)
    {
        var date = new DateOnly(y, m, d);
        Assert.Equal(dayName, ReportingCalendarPolicy.ArDayName(date));
        Assert.Equal(full, ReportingCalendarPolicy.ArFullDateLabel(date));
    }

    // (D6) مفاتيح اليوم السابق/التالي (تقويميًّا، عبر حدود الشهر/السنة/الكبيسة).
    [Theory]
    [InlineData(2026, 7, 14, "2026-07-13", "2026-07-15")]
    [InlineData(2026, 7, 1, "2026-06-30", "2026-07-02")]   // حدّ شهر
    [InlineData(2026, 12, 31, "2026-12-30", "2027-01-01")] // حدّ سنة
    [InlineData(2028, 2, 28, "2028-02-27", "2028-02-29")]  // قبل الكبيس
    [InlineData(2028, 2, 29, "2028-02-28", "2028-03-01")]  // الكبيس
    public void PreviousNextDayKey_AreAdjacentDays(int y, int m, int d, string prev, string next)
    {
        var date = new DateOnly(y, m, d);
        Assert.Equal(prev, ReportingCalendarPolicy.PreviousDayKey(date));
        Assert.Equal(next, ReportingCalendarPolicy.NextDayKey(date));
    }

    // ===== سريان سبت المبيعات (SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1) — القسم D7 =====

    // (D7-1) الأرضية الثابتة = 2026-07-25 وهي **سبت** (لا التزام سبتٍ قبلها لأيّ دور).
    [Fact]
    public void SalesSaturdayApplicabilityFloor_Is_2026_07_25_Saturday()
    {
        Assert.Equal(new DateOnly(2026, 7, 25), ReportingCalendarPolicy.SalesSaturdayApplicabilityFloor);
        Assert.Equal(DayOfWeek.Saturday, ReportingCalendarPolicy.SalesSaturdayApplicabilityFloor.DayOfWeek);
        // الأرضية داخل دورة W31 (السبت الافتتاحيّ لها).
        Assert.Equal("2026-W31", ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.SalesSaturdayApplicabilityFloor));
    }

    // (D7-2) تفعيل السبت مقصور على المسمّيات اليومية القانونيّة (SALES_B2B/SALES_B2C) — غيرهم/فارغ/null = false.
    [Theory]
    [InlineData("SALES_B2B", true)]
    [InlineData("SALES_B2C", true)]
    [InlineData("CONTENT_WRITER", false)]
    [InlineData("SEO_SPECIALIST", false)]
    [InlineData("MEDIA_BUYER", false)]
    [InlineData("sales_b2b", false)] // حسّاس لحالة الأحرف (لا تطبيع)
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SaturdayEnabledForJobRole_OnlyForSalesDailyRoles(string? code, bool expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.SaturdayEnabledForJobRole(code));
    }

    // (D7-3) عقد أيّام العمل مُدرِكًا للسبت: الجمعة محجوبة دائمًا؛ الأحد→الخميس متوقَّعة دائمًا؛
    // السبت متوقَّع ⟺ (مبيعات) و(≥ الأرضية) — لا أثر رجعيّ.
    [Theory]
    // الجمعة: محجوبة للجميع بصرف النظر عن التفعيل.
    [InlineData(2026, 7, 31, true, false)]
    [InlineData(2026, 7, 31, false, false)]
    // السبت قبل الأرضية (2026-07-18): غير متوقَّع حتى لمبيعات (لا رجعيّة).
    [InlineData(2026, 7, 18, true, false)]
    [InlineData(2026, 7, 18, false, false)]
    // السبت على الأرضية (2026-07-25): متوقَّع لمبيعات فقط.
    [InlineData(2026, 7, 25, true, true)]
    [InlineData(2026, 7, 25, false, false)]
    // السبت بعد الأرضية (2026-08-01): متوقَّع لمبيعات فقط.
    [InlineData(2026, 8, 1, true, true)]
    [InlineData(2026, 8, 1, false, false)]
    // الأحد→الخميس: متوقَّعة دائمًا بصرف النظر عن التفعيل.
    [InlineData(2026, 7, 26, true, true)]   // أحد
    [InlineData(2026, 7, 26, false, true)]
    [InlineData(2026, 7, 30, true, true)]   // خميس
    [InlineData(2026, 7, 30, false, true)]
    public void IsDailyExpectedBusinessDay_SaturdayAware(int y, int m, int d, bool saturdayEnabled, bool expected)
    {
        Assert.Equal(expected, ReportingCalendarPolicy.IsDailyExpectedBusinessDay(new DateOnly(y, m, d), saturdayEnabled));
    }

    // (D7-3-b) الصيغة صفريّة-الوسائط تُطابق حرفيًّا سلوك غير-المبيعات (السبت عطلة دائمًا).
    [Fact]
    public void IsDailyExpectedBusinessDay_ZeroArg_Equals_NonSales()
    {
        var salesFloorSaturday = new DateOnly(2026, 7, 25);
        Assert.False(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(salesFloorSaturday));
        Assert.False(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(salesFloorSaturday, saturdayEnabled: false));
        Assert.True(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(salesFloorSaturday, saturdayEnabled: true));
    }

    // (D7-4) العطلة مُدرِكة للسبت: لمبيعات السبت ≥ الأرضية **ليس** عطلة؛ لغير المبيعات عطلة؛ الجمعة عطلة دائمًا.
    [Fact]
    public void IsDailyHoliday_SaturdayAware()
    {
        var saturdayOnFloor = new DateOnly(2026, 7, 25);
        Assert.False(ReportingCalendarPolicy.IsDailyHoliday(saturdayOnFloor, saturdayEnabled: true));  // يوم عمل لمبيعات
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(saturdayOnFloor, saturdayEnabled: false));  // عطلة لغير مبيعات
        var saturdayBeforeFloor = new DateOnly(2026, 7, 18);
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(saturdayBeforeFloor, saturdayEnabled: true)); // عطلة (رجعيّة ممنوعة)
        var friday = new DateOnly(2026, 7, 31);
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(friday, saturdayEnabled: true));  // الجمعة عطلة دائمًا
        Assert.True(ReportingCalendarPolicy.IsDailyHoliday(friday, saturdayEnabled: false));
    }

    // (D7-5) DailyExpectedDates لدورة W31 (السبت 25 → الجمعة 31) لمبيعات = **6 أيّام**:
    // السبت 25 + الأحد 26 → الخميس 30، مستبعِدةً الجمعة 31.
    [Fact]
    public void DailyExpectedDates_W31_Sales_SixDays_IncludesSaturday_ExcludesFriday()
    {
        var today = new DateOnly(2026, 8, 10); // بعد نهاية الدورة ⇒ كامل النافذة
        var dates = ReportingCalendarPolicy.DailyExpectedDates("2026-W31", today, saturdayEnabled: true);
        Assert.Equal(6, dates.Count);
        Assert.Equal(new DateOnly(2026, 7, 25), dates[0]);  // السبت (الأرضية)
        Assert.Equal(new DateOnly(2026, 7, 30), dates[^1]); // الخميس
        Assert.Contains(new DateOnly(2026, 7, 25), dates);
        Assert.DoesNotContain(new DateOnly(2026, 7, 31), dates); // الجمعة
        Assert.All(dates, d => Assert.NotEqual(DayOfWeek.Friday, d.DayOfWeek));
    }

    // (D7-6) لا أثر رجعيّ: W30 (السبت 18 → الجمعة 24) لمبيعات = **5 أيّام** فقط (يبقى السبت 18 مستبعَدًا).
    [Fact]
    public void DailyExpectedDates_W30_Sales_FiveDays_NoRetroactiveSaturday()
    {
        var today = new DateOnly(2026, 8, 10);
        var dates = ReportingCalendarPolicy.DailyExpectedDates("2026-W30", today, saturdayEnabled: true);
        Assert.Equal(5, dates.Count);
        Assert.DoesNotContain(new DateOnly(2026, 7, 18), dates); // السبت قبل الأرضية
        Assert.DoesNotContain(new DateOnly(2026, 7, 24), dates); // الجمعة
        Assert.Equal(new DateOnly(2026, 7, 19), dates[0]);  // الأحد
        Assert.Equal(new DateOnly(2026, 7, 23), dates[^1]); // الخميس
    }

    // (D7-7) الصيغة صفريّة-السبت (غير المبيعات) على W31 = **5 أيّام** (يُستبعَد السبت 25).
    [Fact]
    public void DailyExpectedDates_W31_NonSales_FiveDays_ExcludesSaturday()
    {
        var today = new DateOnly(2026, 8, 10);
        var dates = ReportingCalendarPolicy.DailyExpectedDates("2026-W31", today);
        Assert.Equal(5, dates.Count);
        Assert.DoesNotContain(new DateOnly(2026, 7, 25), dates); // السبت غير متوقَّع لغير المبيعات
        Assert.DoesNotContain(new DateOnly(2026, 7, 31), dates); // الجمعة
        Assert.Equal(new DateOnly(2026, 7, 26), dates[0]);  // الأحد
        Assert.Equal(new DateOnly(2026, 7, 30), dates[^1]); // الخميس
    }

    // (D7-8) صيغة المسمّى الوظيفيّ تشتقّ التفعيل: مبيعات ⇒ 6، غير-مبيعات/null ⇒ 5.
    [Theory]
    [InlineData("SALES_B2B", 6)]
    [InlineData("SALES_B2C", 6)]
    [InlineData("CONTENT_WRITER", 5)]
    [InlineData(null, 5)]
    public void DailyExpectedDates_W31_ByJobRole_DerivesSaturdayEnablement(string? code, int expectedCount)
    {
        var today = new DateOnly(2026, 8, 10);
        var dates = ReportingCalendarPolicy.DailyExpectedDates("2026-W31", today, code);
        Assert.Equal(expectedCount, dates.Count);
    }

    // ===== B-1 — لا اشتقاق مستقلّ لمفتاح الأسبوع خارج السياسة المعياريّة =====

    /// <summary>
    /// كانت `DashboardService.CurrentWeekKey()` تشتقّ المفتاح بـ`ISOWeek.GetWeekOfYear(DateTime.UtcNow)`
    /// (أسبوع يبدأ الاثنين، وبتوقيت UTC) بينما `PeriodKey` مخزَّن بدورة الرياض (السبت → الجمعة).
    /// هذا الاختبار يثبّت الفارق: أسبوع ISO يتخلّف دورةً كاملة في كلّ سبت وأحد — وهما أوّل يومَي
    /// أسبوع العمل السعوديّ — فيقرأ المُرشِّح بيانات الأسبوع الماضي.
    /// </summary>
    [Theory]
    [InlineData(2026, 1, 3)]   // سبت
    [InlineData(2026, 1, 4)]   // أحد
    [InlineData(2026, 8, 22)]  // سبت
    [InlineData(2026, 8, 23)]  // أحد
    public void CycleKeyFor_OnSaturdayAndSunday_DiffersFromIsoWeek_AndFollowsRiyadhCycle(int y, int m, int d)
    {
        var date = new DateOnly(y, m, d);
        var isoKey = $"{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue))}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):00}";

        var cycleKey = ReportingCalendarPolicy.CycleKeyFor(date);

        Assert.NotEqual(isoKey, cycleKey);
        // الدورة التي ينتمي إليها التاريخ تبدأ السبت السابق له أو نفسه وتحتويه فعلًا.
        var (start, end) = ReportingCalendarPolicy.CycleRange(cycleKey);
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);
        Assert.InRange(date, start, end);
    }

    /// <summary>
    /// وفي بقيّة الأيّام يتطابق المفتاحان — فالفارق ليس عشوائيًّا بل محصور في حدّ بداية الدورة.
    /// </summary>
    [Theory]
    [InlineData(2026, 8, 24)]  // اثنين
    [InlineData(2026, 8, 26)]  // أربعاء
    [InlineData(2026, 8, 28)]  // جمعة
    public void CycleKeyFor_OnMondayToFriday_MatchesIsoWeek(int y, int m, int d)
    {
        var date = new DateOnly(y, m, d);
        var isoKey = $"{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue))}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):00}";

        Assert.Equal(isoKey, ReportingCalendarPolicy.CycleKeyFor(date));
    }
}

using System.Globalization;
using Reporting.Domain.Enums;

namespace Reporting.Application.Common;

/// <summary>
/// سياسة تقويم التقارير المُدرِكة للأدوار (ROLE-AWARE-REPORTING-CALENDAR).
/// دورة التقرير تبدأ يوم **السبت** وتنتهي يوم **الجمعة** (نافذة ثابتة Sat→Fri)، ورقم الدورة
/// يُشتقّ من **مرجع الثلاثاء** (بداية الدورة + 3 أيام) عبر ترقيم ISO — وهو ما يُعيد إنتاج
/// جدول الإدارة المعتمد (W27→السبت 27 يونيو 2026، W28→السبت 4 يوليو …) دون قاعدة مخترَعة.
///
/// المبدأ الجوهريّ: كل المستويات (موظّف/قائد فريق/مدير/مدير عام/رئيس تنفيذي/KPI) لنفس الفترة
/// تحمل **نفس مفتاح الدورة** (CycleKey)؛ ما يختلف هو **تاريخ الاستحقاق بحسب الدور** فقط:
///   الموظّف/فِرَق التنفيذ = الأربعاء (بداية+4)، قائد الفريق = الخميس (بداية+5)،
///   المدير = الأحد (بداية+8)، المدير العام/الرئيس التنفيذي = الاثنين (بداية+9).
/// اختلاف تاريخ الاستحقاق ≠ اختلاف الأسبوع.
///
/// دوال خالصة (Pure) بلا حالة؛ **الدور يُستخرَج خادميًّا** من دور المستخدم الأساسيّ ولا يُرسَل من الواجهة.
/// هذه السياسة هي **مصدر الحقيقة الوحيد** لحساب الدورة/الاستحقاق؛ يفوّض إليها التقويم التشغيليّ القديم.
/// </summary>
public static class ReportingCalendarPolicy
{
    // ===== المنطقة الزمنية المرجعية (آسيا/الرياض، UTC+3 ثابت بلا توقيت صيفي) =====

    /// <summary>إزاحة توقيت الرياض الثابتة عن UTC (+3 ساعات، بلا توقيت صيفي).</summary>
    public static readonly TimeSpan RiyadhOffset = TimeSpan.FromHours(3);

    /// <summary>التاريخ الحالي بتوقيت الرياض.</summary>
    public static DateOnly RiyadhToday() =>
        DateOnly.FromDateTime(DateTime.UtcNow.Add(RiyadhOffset));

    /// <summary>تاريخ لحظة UTC معيّنة بتوقيت الرياض (لتقييم تأخّر التسليم بيومه المحلّي).</summary>
    public static DateOnly RiyadhDate(DateTime utc) =>
        DateOnly.FromDateTime((utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc)).Add(RiyadhOffset));

    // ===== نافذة الدورة (السبت → الجمعة) =====

    /// <summary>يوم السبت الذي تبدأ به الدورة المحتوية على تاريخ معيّن.</summary>
    public static DateOnly CycleStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>يوم الجمعة الذي تنتهي به الدورة (بداية + 6 أيام).</summary>
    public static DateOnly CycleEnd(DateOnly cycleStart) => cycleStart.AddDays(6);

    /// <summary>مرجع الثلاثاء للدورة (بداية + 3 أيام) — أساس الترقيم.</summary>
    public static DateOnly TuesdayReference(DateOnly cycleStart) => cycleStart.AddDays(3);

    /// <summary>مفتاح الدورة YYYY-Www للتاريخ (مرتكزًا على مرجع الثلاثاء).</summary>
    public static string CycleKeyFor(DateOnly date)
    {
        var start = CycleStart(date);
        var tuesday = TuesdayReference(start).ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(tuesday);
        var year = ISOWeek.GetYear(tuesday);
        return $"{year}-W{week:00}";
    }

    /// <summary>رقم الدورة (أسبوع ISO لمرجع الثلاثاء).</summary>
    public static int CycleNumber(DateOnly date) =>
        ISOWeek.GetWeekOfYear(TuesdayReference(CycleStart(date)).ToDateTime(TimeOnly.MinValue));

    /// <summary>سنة الدورة (سنة ISO لمرجع الثلاثاء).</summary>
    public static int CycleYear(DateOnly date) =>
        ISOWeek.GetYear(TuesdayReference(CycleStart(date)).ToDateTime(TimeOnly.MinValue));

    /// <summary>عكس العملية: (سبت البداية، جمعة النهاية) لمفتاح دورة.</summary>
    public static (DateOnly Start, DateOnly End) CycleRange(string cycleKey)
    {
        var (year, week) = ParseCycleKey(cycleKey);
        // مرجع الثلاثاء = ثلاثاء أسبوع ISO؛ بداية الدورة = مرجع الثلاثاء − 3 أيام (السبت السابق).
        var tuesday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Tuesday));
        var start = tuesday.AddDays(-3);
        return (start, start.AddDays(6));
    }

    /// <summary>تحليل مفتاح الدورة YYYY-Www إلى (سنة، رقم الأسبوع).</summary>
    public static (int Year, int Week) ParseCycleKey(string cycleKey)
    {
        var parts = cycleKey.Trim().Split("-W");
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>هل المفتاح بصيغة الدورة YYYY-Www (مثل 2026-W27)؟</summary>
    public static bool IsCycleKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"^\d{4}-W\d{2}$");

    /// <summary>
    /// هل المفتاح صالح بنيويًّا **وقابل للعكس بلا خسارة**؟ يرفض أرقامًا خارج مدى ISO للسنة
    /// (مثل W53 لسنة ذات 52 أسبوعًا) إذ إنّ إعادة تشكيل المفتاح من نطاقه يجب أن تطابق الأصل.
    /// </summary>
    public static bool IsValidCycleKey(string? key)
    {
        if (!IsCycleKey(key)) return false;
        var (year, week) = ParseCycleKey(key!);
        if (week < 1 || week > ISOWeek.GetWeeksInYear(year)) return false;
        // تحقّق ذهاب-وإياب: مفتاح بداية الدورة يجب أن يساوي المفتاح المُدخَل.
        var (start, _) = CycleRange(key!);
        return CycleKeyFor(start) == key!.Trim();
    }

    // ===== تواريخ الاستحقاق بحسب الدور (نفس الدورة، إزاحات ثابتة من السبت) =====
    // الموظّف/التنفيذ: الأربعاء (بداية+4). قائد الفريق: الخميس (بداية+5).
    // المدير: الأحد (بداية+8). المدير العام/الرئيس التنفيذي: الاثنين (بداية+9).

    /// <summary>إزاحة يوم الاستحقاق (أيام من سبت البداية) لدور تقريريّ.</summary>
    public static int RoleDueOffset(string role) => role switch
    {
        Roles.TeamLeader => 5,                                     // الخميس
        Roles.Manager => 8,                                        // الأحد
        Roles.GeneralManager or Roles.Ceo or Roles.Admin => 9,    // الاثنين (مراجعة أعلى)
        _ => 4                                                     // الموظّف وغيره ⇒ الأربعاء
    };

    /// <summary>تاريخ الاستحقاق لدور معيّن عن دورة معيّنة (سبت البداية + إزاحة الدور).</summary>
    public static DateOnly RoleDueDate(string cycleKey, string role)
    {
        var start = CycleRange(cycleKey).Start;
        return start.AddDays(RoleDueOffset(role));
    }

    // ملاحظة: استخراج الدور الأساسيّ من أدوار المستخدم يتمّ عبر المصدر القانونيّ الوحيد
    // <see cref="RoleAccess.PrimaryRole"/> (لا تعريف ثانٍ هنا)، ثمّ يُمرَّر إلى <see cref="RoleDueOffset"/>.
    // الدور لا يُرسَل من الواجهة إطلاقًا — يُستخرَج خادميًّا من أدوار المستخدم الحاليّ.

    // ===== نطاق تغطية البيانات (Data Coverage) — نقطة توسّع موثّقة =====
    // في هذه الحزمة: تغطية البيانات الافتراضية = **نافذة الدورة نفسها** (السبت → الجمعة).
    // هذا مصمَّم كنقطة توسّع (Extension Point): مستقبلًا قد يُسنَد لقالب إعداد «مدى تغطية»
    // يمتدّ لعدّة دورات، فيُحسَب النطاق من هنا دون تعديل قاعدة البيانات أو الترقيم.
    // لا يوجد إعداد جديد في هذه الحزمة، ولا هجرة — القيمة الافتراضية فقط.

    /// <summary>
    /// نطاق تغطية البيانات الافتراضيّ لدورة (= نافذة الدورة السبت→الجمعة).
    /// نقطة توسّع: لا إعداد قالب جديد ولا هجرة في هذه الحزمة.
    /// </summary>
    public static (DateOnly Start, DateOnly End) DataCoverageWindow(string cycleKey) =>
        CycleRange(cycleKey);

    // ===== انتماء الدورة لشهر/ربع/سنة (تجميع KPI) =====
    // تنتمي الدورة للشهر/الربع/السنة التي يقع فيها **مرجع الثلاثاء** — فلا تُحتسب دورة لفترتين.

    /// <summary>حدود فترة شهرية «YYYY-MM».</summary>
    public static (DateOnly Start, DateOnly End) MonthRange(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    /// <summary>حدود فترة ربع سنوية (الربع 1..4).</summary>
    public static (DateOnly Start, DateOnly End) QuarterRange(int year, int quarter)
    {
        var firstMonth = (quarter - 1) * 3 + 1;
        var start = new DateOnly(year, firstMonth, 1);
        return (start, start.AddMonths(3).AddDays(-1));
    }

    /// <summary>حدود فترة سنوية.</summary>
    public static (DateOnly Start, DateOnly End) YearRange(int year) =>
        (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    /// <summary>هل تنتمي الدورة (بحسب مرجع الثلاثاء) إلى المدى [from, to]؟</summary>
    public static bool CycleInRange(string cycleKey, DateOnly from, DateOnly to)
    {
        if (!IsCycleKey(cycleKey)) return false;
        var anchor = TuesdayReference(CycleRange(cycleKey).Start); // مرجع الثلاثاء
        return anchor >= from && anchor <= to;
    }

    // ===== تسميات عربية مفهومة =====

    private static readonly string[] ArMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private static readonly string[] ArDays =
    {
        "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"
    };

    /// <summary>اسم اليوم العربيّ لتاريخ.</summary>
    public static string ArDayName(DateOnly date) => ArDays[(int)date.DayOfWeek];

    /// <summary>«8 يوليو».</summary>
    public static string ArDayMonth(DateOnly date) => $"{date.Day} {ArMonths[date.Month - 1]}";

    /// <summary>«الأسبوع 27 — 2026 (السبت 27 يونيو — الجمعة 3 يوليو)».</summary>
    public static string CycleLabel(string cycleKey)
    {
        if (!IsCycleKey(cycleKey)) return cycleKey;
        var (year, week) = ParseCycleKey(cycleKey);
        var (start, end) = CycleRange(cycleKey);
        return $"الأسبوع {week} — {year} (السبت {ArDayMonth(start)} — الجمعة {ArDayMonth(end)})";
    }

    /// <summary>تسمية مختصرة للدورة «الأسبوع 27 — 2026».</summary>
    public static string ShortCycleLabel(string cycleKey)
    {
        if (!IsCycleKey(cycleKey)) return cycleKey;
        var (year, week) = ParseCycleKey(cycleKey);
        return $"الأسبوع {week} — {year}";
    }

    // ===== الوضع اليوميّ (Daily) — تقارير المبيعات =====
    // مفتاح اليوم YYYY-MM-DD يُولَّد **خادميًّا** بتوقيت الرياض (لا حساب محليّ في الواجهة، ولا إدخال يدويّ).
    // أيام العمل اليومية: السبت→الخميس. الجمعة **وحدها** عطلة أسبوعية (غير متاحة للتقارير اليومية). السبت يوم عمل يوميّ كامل.
    // هذه الدوال خالصة (Pure) وتشترك مع الأسبوعيّ في مصدر الحقيقة نفسه (RiyadhToday/التسميات العربية).

    /// <summary>مفتاح اليوم بصيغة YYYY-MM-DD (خادميّ، بلا انزياح منطقة زمنية).</summary>
    public static string DayKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>هل المفتاح بصيغة يوم YYYY-MM-DD صالحة بنيويًّا **وقابلة للعكس بلا خسارة**؟</summary>
    public static bool IsValidDayKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var s = key.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$")) return false;
        return DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            && DayKey(d) == s; // تحقّق ذهاب-وإياب (يرفض مثل 2026-02-30 أو 2026-13-01).
    }

    /// <summary>تحليل مفتاح اليوم YYYY-MM-DD إلى تاريخ (يفترض صحّته البنيوية).</summary>
    public static DateOnly ParseDayKey(string key) =>
        DateOnly.ParseExact(key.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>هل اليوم عطلة أسبوعية (الجمعة وحدها) بحسب سياسة التقارير اليومية؟ السبت يوم عمل.</summary>
    public static bool IsDailyHoliday(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Friday;

    /// <summary>«الثلاثاء 14 يوليو 2026».</summary>
    public static string ArFullDateLabel(DateOnly date) =>
        $"{ArDayName(date)} {date.Day} {ArMonths[date.Month - 1]} {date.Year}";

    /// <summary>مفتاح اليوم السابق (تقويميًّا).</summary>
    public static string PreviousDayKey(DateOnly date) => DayKey(date.AddDays(-1));

    /// <summary>مفتاح اليوم التالي (تقويميًّا).</summary>
    public static string NextDayKey(DateOnly date) => DayKey(date.AddDays(1));
}

using System.Globalization;
using Reporting.Domain.Enums;

namespace Reporting.Application.Common;

/// <summary>
/// تقويم التقارير التشغيلي — **واجهة توافق (Compatibility Facade)** تفوّض بالكامل إلى
/// <see cref="ReportingCalendarPolicy"/> (مصدر الحقيقة الوحيد المُدرِك للأدوار).
///
/// تاريخيًّا بُني هذا التقويم على أسبوع «الخميس → الأربعاء». اعتمدت الإدارة تقويمًا موحّدًا
/// يبدأ يوم **السبت** وينتهي **الجمعة** (W27→السبت 27 يونيو 2026 …) مع الإبقاء على أرقام ISO.
/// لتجنّب مصدرَي حقيقة متعارضين، تُعاد كتابة كل الدوال هنا كـ**تفويض رقيق** للسياسة الجديدة:
///   WeekStart→CycleStart (السبت)، WeekEnd→CycleEnd (الجمعة)، WeekKeyFor→CycleKeyFor،
///   WeekRange→CycleRange، DueDateForRole→RoleDueDate، Week*Label→Cycle*Label.
///
/// تواريخ الاستحقاق بحسب الدور **تبقى مطابقة تمامًا** (الموظّف=الأربعاء، قائد الفريق=الخميس،
/// المدير=الأحد، المدير العام/الرئيس التنفيذي=الاثنين) — يتغيّر فقط حدّ بداية الأسبوع إلى السبت
/// ليطابق جدول الإدارة المعتمد. لا اسم مستخدم بعينه — كلّها قواعد دور/تاريخ.
/// </summary>
public static class ReportCalendarPolicy
{
    // ===== المنطقة الزمنية المرجعية (آسيا/الرياض، UTC+3 ثابت بلا توقيت صيفي) =====

    /// <summary>إزاحة توقيت الرياض الثابتة عن UTC (+3 ساعات، بلا توقيت صيفي).</summary>
    public static readonly TimeSpan RiyadhOffset = ReportingCalendarPolicy.RiyadhOffset;

    /// <summary>التاريخ الحالي بتوقيت الرياض.</summary>
    public static DateOnly RiyadhToday() => ReportingCalendarPolicy.RiyadhToday();

    /// <summary>تاريخ لحظة UTC معيّنة بتوقيت الرياض.</summary>
    public static DateOnly RiyadhDate(DateTime utc) => ReportingCalendarPolicy.RiyadhDate(utc);

    // ===== الأسبوع التشغيلي (السبت → الجمعة) — يفوّض للسياسة الجديدة =====

    /// <summary>السبت الذي يبدأ به الأسبوع التشغيلي المحتوي على تاريخ معيّن.</summary>
    public static DateOnly WeekStart(DateOnly date) => ReportingCalendarPolicy.CycleStart(date);

    /// <summary>الجمعة الذي ينتهي به الأسبوع التشغيلي (بداية + 6 أيام).</summary>
    public static DateOnly WeekEnd(DateOnly weekStart) => ReportingCalendarPolicy.CycleEnd(weekStart);

    /// <summary>مفتاح الأسبوع التشغيلي YYYY-Www للتاريخ (مرتكزًا على مرجع الثلاثاء).</summary>
    public static string WeekKeyFor(DateOnly date) => ReportingCalendarPolicy.CycleKeyFor(date);

    /// <summary>عكس العملية: (سبت البداية، جمعة النهاية) لمفتاح أسبوع.</summary>
    public static (DateOnly Start, DateOnly End) WeekRange(string weekKey) =>
        ReportingCalendarPolicy.CycleRange(weekKey);

    /// <summary>تحليل مفتاح الأسبوع YYYY-Www إلى (سنة، رقم الأسبوع).</summary>
    public static (int Year, int Week) ParseWeekKey(string weekKey) =>
        ReportingCalendarPolicy.ParseCycleKey(weekKey);

    /// <summary>هل المفتاح بصيغة الأسبوع YYYY-Www (مثل 2026-W27)؟</summary>
    public static bool IsWeekKey(string? key) => ReportingCalendarPolicy.IsCycleKey(key);

    // ===== تواريخ التسليم بحسب الدور — يفوّض للسياسة الجديدة (نفس التواريخ) =====

    /// <summary>تاريخ التسليم/المراجعة المتوقَّع لدور معيّن عن أسبوع تشغيلي.</summary>
    public static DateOnly DueDateForRole(string weekKey, string role) =>
        ReportingCalendarPolicy.RoleDueDate(weekKey, role);

    // ===== انتماء الأسبوع لشهر/ربع/سنة (تجميع KPI) — يفوّض للسياسة الجديدة =====

    /// <summary>حدود فترة شهرية «YYYY-MM».</summary>
    public static (DateOnly Start, DateOnly End) MonthRange(int year, int month) =>
        ReportingCalendarPolicy.MonthRange(year, month);

    /// <summary>حدود فترة ربع سنوية (الربع 1..4).</summary>
    public static (DateOnly Start, DateOnly End) QuarterRange(int year, int quarter) =>
        ReportingCalendarPolicy.QuarterRange(year, quarter);

    /// <summary>حدود فترة سنوية.</summary>
    public static (DateOnly Start, DateOnly End) YearRange(int year) =>
        ReportingCalendarPolicy.YearRange(year);

    /// <summary>هل ينتمي الأسبوع (بحسب مرجع الثلاثاء) إلى المدى [from, to]؟</summary>
    public static bool WeekInRange(string weekKey, DateOnly from, DateOnly to) =>
        ReportingCalendarPolicy.CycleInRange(weekKey, from, to);

    // ===== تسميات عربية مفهومة — يفوّض للسياسة الجديدة =====

    /// <summary>«الأسبوع 27 — 2026 (السبت 27 يونيو — الجمعة 3 يوليو)».</summary>
    public static string WeekLabel(string weekKey) => ReportingCalendarPolicy.CycleLabel(weekKey);

    /// <summary>تسمية مختصرة للأسبوع «الأسبوع 27 — 2026».</summary>
    public static string ShortWeekLabel(string weekKey) => ReportingCalendarPolicy.ShortCycleLabel(weekKey);

    // ===== مفتاح الفترة السابقة (للمقارنة الدورية في محرّك التجميع Project-First) =====

    /// <summary>
    /// يشتقّ مفتاح الفترة **السابقة** لنوع فترة ومفتاح معطى (قراءة فقط، بلا تأثير جانبي):
    /// Weekly YYYY-Www ⇒ الأسبوع التشغيلي السابق (إزاحة −7 يومًا عبر CycleKeyFor/CycleRange)،
    /// Monthly YYYY-MM ⇒ الشهر السابق، Quarterly YYYY-Qn ⇒ الربع السابق (Q1⇒Q4 والسنة −1)،
    /// Daily YYYY-MM-DD ⇒ اليوم السابق. أي مفتاح غير صالح/تعذّر اشتقاقه ⇒ null (لا مقارنة).
    /// </summary>
    public static string? PreviousPeriodKey(PeriodType periodType, string? periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey)) return null;
        try
        {
            switch (periodType)
            {
                case PeriodType.Weekly:
                    if (!IsWeekKey(periodKey)) return null;
                    var (start, _) = WeekRange(periodKey);
                    return WeekKeyFor(start.AddDays(-7));

                case PeriodType.Monthly:
                {
                    var parts = periodKey.Split('-');
                    if (parts.Length != 2) return null;
                    var y = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    var m = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    if (m < 1 || m > 12) return null;
                    m--; if (m == 0) { m = 12; y--; }
                    return $"{y:D4}-{m:D2}";
                }

                case PeriodType.Quarterly:
                {
                    var parts = periodKey.Split('-');
                    if (parts.Length != 2 || !parts[1].StartsWith("Q", StringComparison.OrdinalIgnoreCase)) return null;
                    var y = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    var q = int.Parse(parts[1].Substring(1), CultureInfo.InvariantCulture);
                    if (q < 1 || q > 4) return null;
                    q--; if (q == 0) { q = 4; y--; }
                    return $"{y:D4}-Q{q}";
                }

                case PeriodType.Daily:
                    if (!DateOnly.TryParseExact(periodKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return null;
                    return d.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
}

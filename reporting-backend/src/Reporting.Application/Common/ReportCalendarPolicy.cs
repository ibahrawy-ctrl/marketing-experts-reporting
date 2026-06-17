using System.Globalization;
using Reporting.Domain.Enums;

namespace Reporting.Application.Common;

/// <summary>
/// تقويم التقارير التشغيلي (Phase 5): الأسبوع التشغيلي للشركة = «الخميس → الأربعاء».
/// دوال خالصة (Pure) تحسب: حدود الأسبوع، مفتاح الأسبوع YYYY-Www، تواريخ التسليم بحسب الدور،
/// وانتماء الأسبوع لشهر/ربع/سنة. لا تعتمد على اسم مستخدم بعينه — كلّها قواعد دور/تاريخ.
/// التقويم مبنيّ على ثلوثاء/خميس ISO: الخميس الذي يبدأ به الأسبوع التشغيلي هو نفسه خميس أسبوع ISO
/// (لأنّ خميس أي أسبوع ISO يقع داخله) ⇒ ترقيم الأسابيع يبقى متوافقًا مع 2026-W25 المستخدَم سابقًا.
/// </summary>
public static class ReportCalendarPolicy
{
    // ===== الأسبوع التشغيلي (الخميس → الأربعاء) =====

    /// <summary>الخميس الذي يبدأ به الأسبوع التشغيلي المحتوي على تاريخ معيّن.</summary>
    public static DateOnly WeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Thursday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>الأربعاء الذي ينتهي به الأسبوع التشغيلي (بداية + 6 أيام).</summary>
    public static DateOnly WeekEnd(DateOnly weekStart) => weekStart.AddDays(6);

    /// <summary>مفتاح الأسبوع التشغيلي YYYY-Www للتاريخ (مرتكزًا على خميس البداية).</summary>
    public static string WeekKeyFor(DateOnly date)
    {
        var thursday = WeekStart(date);
        var dt = thursday.ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(dt);
        var year = ISOWeek.GetYear(dt);
        return $"{year}-W{week:00}";
    }

    /// <summary>عكس العملية: (خميس البداية، أربعاء النهاية) لمفتاح أسبوع.</summary>
    public static (DateOnly Start, DateOnly End) WeekRange(string weekKey)
    {
        var (year, week) = ParseWeekKey(weekKey);
        var thursday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Thursday));
        return (thursday, thursday.AddDays(6));
    }

    /// <summary>تحليل مفتاح الأسبوع YYYY-Www إلى (سنة، رقم الأسبوع).</summary>
    public static (int Year, int Week) ParseWeekKey(string weekKey)
    {
        var parts = weekKey.Trim().Split("-W");
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>هل المفتاح بصيغة الأسبوع YYYY-Www (مثل 2026-W25)؟</summary>
    public static bool IsWeekKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"^\d{4}-W\d{2}$");

    // ===== تواريخ التسليم بحسب الدور (Phase 5 §2/§3) =====
    // الموظّف: ينتهي أسبوعه الأربعاء ⇒ يُسلّم بنهاية الأربعاء.
    // قائد الفريق: يراجع بعد إغلاق أسبوع الموظّفين ⇒ يُسلّم الخميس (نهاية الأسبوع + 1).
    // المدير: يراجع تقارير القادة ⇒ يُسلّم الأحد (نهاية الأسبوع + 4).
    // المدير العام/الرئيس التنفيذي: يراجع ملخّص الأسبوع السابق ⇒ الاثنين (نهاية الأسبوع + 5).

    /// <summary>تاريخ التسليم/المراجعة المتوقَّع لدور معيّن عن أسبوع تشغيلي.</summary>
    public static DateOnly DueDateForRole(string weekKey, string role)
    {
        var end = WeekRange(weekKey).End; // الأربعاء
        return role switch
        {
            Roles.TeamLeader => end.AddDays(1),                       // الخميس
            Roles.Manager => end.AddDays(4),                          // الأحد
            Roles.GeneralManager or Roles.Ceo => end.AddDays(5),      // الاثنين (مراجعة)
            _ => end                                                  // الموظّف وغيره ⇒ الأربعاء
        };
    }

    // ===== انتماء الأسبوع لشهر/ربع/سنة (Phase 5 §8 — تجميع KPI) =====
    // ينتمي الأسبوع للشهر/الربع/السنة التي يقع فيها «خميس بدايته» — فلا يُحتسب أسبوع لفترتين.

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

    /// <summary>هل ينتمي الأسبوع (بحسب خميس بدايته) إلى المدى [from, to]؟</summary>
    public static bool WeekInRange(string weekKey, DateOnly from, DateOnly to)
    {
        if (!IsWeekKey(weekKey)) return false;
        var anchor = WeekRange(weekKey).Start; // خميس البداية
        return anchor >= from && anchor <= to;
    }

    // ===== تسميات عربية مفهومة (Phase 5 §7) =====

    private static readonly string[] ArMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    /// <summary>«الأسبوع 25 — 2026 (الخميس 18 يونيو — الأربعاء 24 يونيو)».</summary>
    public static string WeekLabel(string weekKey)
    {
        if (!IsWeekKey(weekKey)) return weekKey;
        var (year, week) = ParseWeekKey(weekKey);
        var (start, end) = WeekRange(weekKey);
        return $"الأسبوع {week} — {year} (الخميس {start.Day} {ArMonths[start.Month - 1]} — الأربعاء {end.Day} {ArMonths[end.Month - 1]})";
    }

    /// <summary>تسمية مختصرة للأسبوع «الأسبوع 25 — 2026».</summary>
    public static string ShortWeekLabel(string weekKey)
    {
        if (!IsWeekKey(weekKey)) return weekKey;
        var (year, week) = ParseWeekKey(weekKey);
        return $"الأسبوع {week} — {year}";
    }
}

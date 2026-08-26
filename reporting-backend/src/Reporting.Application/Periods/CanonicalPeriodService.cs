using System.Globalization;
using Reporting.Application.Common;

namespace Reporting.Application.Periods;

/// <summary>
/// P1-KPI-002 — التنفيذ الوحيد لـ<see cref="IPeriodService"/>.
/// يفوّض حساب نافذة الأسبوع وترقيمه إلى <see cref="ReportingCalendarPolicy"/> (السبت→الجمعة، مرجع الثلاثاء)
/// حفاظًا حرفيًّا على <c>PeriodKey</c> التاريخيّة المنشورة — **لا مفتاح يُعاد كتابته ولا Backfill** (B-1/B-4).
/// دوال خالصة عدا قراءة «الآن» من <see cref="ISystemClock"/> لتمكين الاختبار الحتميّ.
/// </summary>
public sealed class CanonicalPeriodService : IPeriodService
{
    /// <summary>المنطقة الزمنيّة المرجعيّة المُعلَنة في كلّ عقد فترة.</summary>
    public const string TimeZoneId = "Asia/Riyadh";

    private static readonly TimeSpan Offset = ReportingCalendarPolicy.RiyadhOffset;

    private static readonly string[] ArMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private readonly ISystemClock _clock;

    public CanonicalPeriodService(ISystemClock clock) => _clock = clock;

    /// <summary>«اليوم» بتوقيت الرياض مشتقًّا من الساعة المحقونة (لا <c>DateTime.UtcNow</c> مباشر).</summary>
    private DateOnly RiyadhToday() => DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime.Add(Offset));

    public Result<ResolvedPeriod> Resolve(PeriodRequest request)
    {
        var type = (request.Type ?? string.Empty).Trim();
        var key = request.Key?.Trim();

        switch (type)
        {
            case PeriodKinds.LastCompletedWeek:
                return Result<ResolvedPeriod>.Success(LastCompletedWeek());

            case PeriodKinds.Week:
                if (!ReportingCalendarPolicy.IsValidCycleKey(key))
                    return Fail("صيغة الأسبوع غير صحيحة؛ استخدم YYYY-Www مثل 2026-W27.", "period.week_key_invalid");
                var (ws, we) = ReportingCalendarPolicy.CycleRange(key!);
                return Ok(PeriodKinds.Week, key!, ws, we, ReportingCalendarPolicy.CycleLabel(key!));

            case PeriodKinds.Month:
                if (!TryParseMonth(key, out var year, out var month))
                    return Fail("صيغة الشهر غير صحيحة؛ استخدم YYYY-MM مثل 2026-06.", "period.month_key_invalid");
                var (ms, me) = ReportingCalendarPolicy.MonthRange(year, month);
                return Ok(PeriodKinds.Month, MonthKey(year, month), ms, me, $"{ArMonths[month - 1]} {year}");

            case PeriodKinds.Quarter:
                if (!TryParseQuarter(key, out var qYear, out var quarter))
                    return Fail("صيغة الربع غير صحيحة؛ استخدم YYYY-Qn مثل 2026-Q2.", "period.quarter_key_invalid");
                var (qs, qe) = ReportingCalendarPolicy.QuarterRange(qYear, quarter);
                return Ok(PeriodKinds.Quarter, QuarterKey(qYear, quarter), qs, qe, $"الربع {quarter} — {qYear}");

            case PeriodKinds.Year:
                if (!TryParseYear(key, out var yYear))
                    return Fail("صيغة السنة غير صحيحة؛ استخدم YYYY مثل 2026.", "period.year_key_invalid");
                var (ys, ye) = ReportingCalendarPolicy.YearRange(yYear);
                return Ok(PeriodKinds.Year, YearKey(yYear), ys, ye, $"سنة {yYear}");

            case PeriodKinds.Custom:
                if (request.From is not DateOnly cf || request.To is not DateOnly ct)
                    return Fail("المدى المخصّص يتطلّب تاريخ بداية وتاريخ نهاية.", "period.range_required");
                if (cf > ct)
                    return Fail("تاريخ البداية يجب أن يسبق تاريخ النهاية.", "period.range_invalid");
                return Ok(PeriodKinds.Custom, CustomKey(cf, ct), cf, ct, CustomLabel(cf, ct));

            default:
                return Fail(
                    "نوع الفترة غير مدعوم؛ استخدم Week/Month/Quarter/Year/Custom/LastCompletedWeek.",
                    "period.type_invalid");
        }
    }

    public ResolvedPeriod LastCompletedWeek()
    {
        // الأسبوع الجاري يحوي «اليوم»؛ آخر أسبوع مكتمل هو السبت السابق له بسبعة أيام (انتهت جمعته).
        var previousStart = ReportingCalendarPolicy.CycleStart(RiyadhToday()).AddDays(-7);
        var key = ReportingCalendarPolicy.CycleKeyFor(previousStart);
        return Ok(PeriodKinds.Week, key, previousStart, previousStart.AddDays(6),
            ReportingCalendarPolicy.CycleLabel(key)).Value!;
    }

    public ResolvedPeriod PreviousComparable(ResolvedPeriod current)
    {
        switch (current.Type)
        {
            case PeriodKinds.Week:
            {
                var start = current.Start.AddDays(-7);
                var key = ReportingCalendarPolicy.CycleKeyFor(start);
                return Ok(PeriodKinds.Week, key, start, start.AddDays(6),
                    ReportingCalendarPolicy.CycleLabel(key)).Value!;
            }
            case PeriodKinds.Month:
            {
                var anchor = current.Start.AddMonths(-1);
                var (s, e) = ReportingCalendarPolicy.MonthRange(anchor.Year, anchor.Month);
                return Ok(PeriodKinds.Month, MonthKey(anchor.Year, anchor.Month), s, e,
                    $"{ArMonths[anchor.Month - 1]} {anchor.Year}").Value!;
            }
            case PeriodKinds.Quarter:
            {
                var anchor = current.Start.AddMonths(-3);
                var q = (anchor.Month - 1) / 3 + 1;
                var (s, e) = ReportingCalendarPolicy.QuarterRange(anchor.Year, q);
                return Ok(PeriodKinds.Quarter, QuarterKey(anchor.Year, q), s, e, $"الربع {q} — {anchor.Year}").Value!;
            }
            case PeriodKinds.Year:
            {
                var y = current.Start.Year - 1;
                var (s, e) = ReportingCalendarPolicy.YearRange(y);
                return Ok(PeriodKinds.Year, YearKey(y), s, e, $"سنة {y}").Value!;
            }
            default:
            {
                // المخصّص: يُزاح بطول المدى نفسه (شاملًا الطرفين) فيبقى القياس متكافئًا.
                var lengthDays = current.End.DayNumber - current.Start.DayNumber + 1;
                var s = current.Start.AddDays(-lengthDays);
                var e = current.End.AddDays(-lengthDays);
                return Ok(PeriodKinds.Custom, CustomKey(s, e), s, e, CustomLabel(s, e)).Value!;
            }
        }
    }

    public IReadOnlyList<string> WeekKeysWithin(ResolvedPeriod period)
    {
        if (period.Type == PeriodKinds.Week) return new[] { period.Key };

        // انتماء الدورة للفترة يُحسم بمرجع الثلاثاء (بداية+3) — فلا تُحتسب دورة لفترتين.
        var keys = new List<string>();
        var cursor = ReportingCalendarPolicy.CycleStart(period.Start);
        while (ReportingCalendarPolicy.TuesdayReference(cursor) <= period.End)
        {
            if (ReportingCalendarPolicy.TuesdayReference(cursor) >= period.Start)
                keys.Add(ReportingCalendarPolicy.CycleKeyFor(cursor));
            cursor = cursor.AddDays(7);
        }
        return keys;
    }

    // ===== أدوات داخليّة =====

    // مفاتيح الفترات تُبنى بـ InvariantCulture **إلزامًا**: ثقافة الخادم العربيّة (ar-SA) تستعمل التقويم
    // الأمّ القرى، فـ$"{date:yyyy-MM-dd}" كان يُنتج «1447-11-14» بدل «2026-05-01». المفتاح معرّف تقنيّ لا نصّ معروض.
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string CustomKey(DateOnly from, DateOnly to) => $"{Iso(from)}..{Iso(to)}";

    private static string CustomLabel(DateOnly from, DateOnly to) =>
        $"من {ReportingCalendarPolicy.ArDayMonth(from)} {from.Year} إلى {ReportingCalendarPolicy.ArDayMonth(to)} {to.Year}";

    private static string MonthKey(int year, int month) =>
        $"{year.ToString("0000", CultureInfo.InvariantCulture)}-{month.ToString("00", CultureInfo.InvariantCulture)}";

    private static string QuarterKey(int year, int quarter) =>
        $"{year.ToString("0000", CultureInfo.InvariantCulture)}-Q{quarter.ToString(CultureInfo.InvariantCulture)}";

    private static string YearKey(int year) => year.ToString("0000", CultureInfo.InvariantCulture);

    private Result<ResolvedPeriod> Ok(string type, string key, DateOnly start, DateOnly end, string label)
    {
        // حدود الرياض: البداية 00:00 والنهاية 23:59:59.9999999 محليًّا، محوَّلتان إلى UTC بطرح الإزاحة.
        var startUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue).Subtract(Offset), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue).Subtract(Offset), DateTimeKind.Utc);
        var isOpen = _clock.UtcNow.UtcDateTime < endUtc;
        return Result<ResolvedPeriod>.Success(
            new ResolvedPeriod(type, key, start, end, startUtc, endUtc, TimeZoneId, isOpen, label));
    }

    private static Result<ResolvedPeriod> Fail(string message, string code) =>
        Result<ResolvedPeriod>.Failure(message, code);

    private static bool TryParseMonth(string? key, out int year, out int month)
    {
        year = 0; month = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Trim().Split('-');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out month)) return false;
        return year is >= 1 and <= 9999 && month is >= 1 and <= 12;
    }

    private static bool TryParseQuarter(string? key, out int year, out int quarter)
    {
        year = 0; quarter = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Trim().ToUpperInvariant().Split('Q');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0].TrimEnd('-'), NumberStyles.None, CultureInfo.InvariantCulture, out year)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out quarter)) return false;
        return year is >= 1 and <= 9999 && quarter is >= 1 and <= 4;
    }

    private static bool TryParseYear(string? key, out int year) =>
        int.TryParse((key ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out year)
        && year is >= 1 and <= 9999;
}

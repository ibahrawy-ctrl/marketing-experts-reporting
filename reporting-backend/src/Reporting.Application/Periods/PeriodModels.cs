namespace Reporting.Application.Periods;

/// <summary>
/// P1-KPI-002 — أنواع الفترات القانونيّة الوحيدة لتحليلات KPI.
/// الأسماء تُرسَل نصًّا من الواجهة وتُطبَّع خادميًّا؛ الواجهة لا تحسب أيّ حدّ زمنيّ بنفسها (B-1).
/// </summary>
public static class PeriodKinds
{
    public const string Week = "Week";
    public const string Month = "Month";
    public const string Quarter = "Quarter";
    public const string Year = "Year";
    public const string Custom = "Custom";

    /// <summary>اسم رمزيّ يُحلّ خادميًّا إلى آخر أسبوع **مكتمل** (الافتراضيّ للبطاقات التنظيميّة).</summary>
    public const string LastCompletedWeek = "LastCompletedWeek";

    /// <summary>
    /// DEC-01/1 — اسم رمزيّ يُحلّ خادميًّا إلى الربع الميلاديّ **الجاري** بتوقيت <c>Asia/Riyadh</c>
    /// (في أغسطس 2026 ⇒ <c>2026-Q3</c>). الافتراضيّ لصفحة KPI: لا يختار المستخدم الفترة الجارية.
    /// </summary>
    public const string CurrentQuarter = "CurrentQuarter";
}

/// <summary>
/// طلب حلّ فترة. <paramref name="Key"/> مطلوب لـWeek/Month/Quarter/Year،
/// و<paramref name="From"/>/<paramref name="To"/> مطلوبان لـCustom، ولا شيء مطلوب لـLastCompletedWeek.
/// </summary>
public sealed record PeriodRequest(
    string Type,
    string? Key = null,
    DateOnly? From = null,
    DateOnly? To = null);

/// <summary>
/// فترة محلولة خادميًّا بحدود **Asia/Riyadh** (B-1). <c>StartUtc</c>/<c>EndUtc</c> هما الحدّان الفعليّان
/// للاستعلام (شامل الطرفين بدقّة الميلي ثانية)، و<c>IsOpen</c> يميّز الفترة الجارية عن المكتملة —
/// ولا يُحسَب Trend رسميّ من فترة مفتوحة (5.4/5.6).
/// </summary>
public sealed record ResolvedPeriod(
    string Type,
    string Key,
    DateOnly Start,
    DateOnly End,
    DateTime StartUtc,
    DateTime EndUtc,
    string TimeZone,
    bool IsOpen,
    string Label);

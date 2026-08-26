using Reporting.Domain.Enums;

namespace Reporting.Application.Kpi;

/// <summary>
/// P1 §5.1/§5.5/§5.6 — السياسات الحسابيّة الخالصة لتحليلات KPI، معزولة عن الوصول للبيانات
/// كي تُختبَر وحدويًّا بلا قاعدة بيانات. **مصدر واحد** لكلّ من: التقريب، التغطية، جودة البيانات، والاتجاه.
/// </summary>
public static class KpiScorePolicy
{
    /// <summary>
    /// **سياسة التقريب الوحيدة**: خانتان عشريّتان، تقريب الأنصاف بعيدًا عن الصفر، ويُطبَّق
    /// **مرّة واحدة فقط عند حافة الـDTO**. كلّ الحساب الوسيط (متوسّط الموظّف ثمّ متوسّط المجموعة)
    /// يجري على <c>decimal</c> غير مقرَّب لمنع التقريب المزدوج (§9.1 بند 10).
    /// </summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <inheritdoc cref="Round(decimal)"/>
    public static decimal? Round(decimal? value) => value is null ? null : Round(value.Value);

    /// <summary>
    /// المرحلة الأولى من التوسيط الثنائي: متوسّط درجات الموظّف المؤهّلة داخل الفترة.
    /// يُعيد <c>null</c> إذا لا تقييم مؤهّلًا — ولا يتحوّل ذلك إلى صفر أبدًا (§5.2).
    /// </summary>
    public static decimal? EmployeePeriodScore(decimal sumOfApprovedScores, int approvedCount) =>
        approvedCount > 0 ? sumOfApprovedScores / approvedCount : null;

    /// <summary>
    /// المرحلة الثانية: متوسّط **متوسّطات** الأعضاء — لكلّ موظّف وزن واحد مهما اختلف عدد تقييماته (B-2).
    /// المُدخَل يجب أن يكون درجات موظّفين غير مقرَّبة؛ الأعضاء بلا درجة لا يدخلون.
    /// </summary>
    public static decimal? GroupScore(IEnumerable<decimal> employeePeriodScores)
    {
        decimal sum = 0m;
        var n = 0;
        foreach (var s in employeePeriodScores) { sum += s; n++; }
        return n > 0 ? sum / n : null;
    }

    /// <summary>التغطية = المؤهَّل / المتوقَّع المعدَّل؛ <c>null</c> إذا المقام صفر (لا تُلفَّق قيمة).</summary>
    public static decimal? Coverage(int eligibleCount, int adjustedExpectedCount) =>
        adjustedExpectedCount > 0 ? (decimal)eligibleCount / adjustedExpectedCount : null;

    /// <summary>الالتزامات المتوقَّعة بلا درجة معتمَدة. لا تدخل المتوسّط كأصفار.</summary>
    public static int MissingCount(int eligibleCount, int adjustedExpectedCount) =>
        Math.Max(0, adjustedExpectedCount - eligibleCount);

    /// <summary>
    /// جودة البيانات (§5.5): لا تقييم ⇒ <c>NoData</c>؛ تغطية دون الحدّ الأدنى ⇒ <c>InsufficientCoverage</c>؛
    /// تغطية كاملة ⇒ <c>Complete</c>؛ وإلّا <c>Partial</c>. مقام صفر مع وجود درجة يُعدّ اكتمالًا (لا التزام متبقٍّ).
    /// </summary>
    public static KpiDataQuality DataQuality(int eligibleCount, int adjustedExpectedCount, decimal minimumCoverage)
    {
        if (eligibleCount <= 0) return KpiDataQuality.NoData;
        var coverage = Coverage(eligibleCount, adjustedExpectedCount);
        if (coverage is null) return KpiDataQuality.Complete;
        if (coverage.Value < minimumCoverage) return KpiDataQuality.InsufficientCoverage;
        return coverage.Value >= 1m ? KpiDataQuality.Complete : KpiDataQuality.Partial;
    }

    /// <summary>
    /// أهليّة دخول الترتيب والمقارنة الرسميّة (B-5): تقييم معتمَد واحد على الأقلّ **و** تغطية ≥ الحدّ الأدنى.
    /// الأسبوع ذو الالتزام الواحد يعطي 1/1 = 100% فيكون مؤهّلًا.
    /// </summary>
    public static bool EligibleForRanking(int eligibleCount, int adjustedExpectedCount, decimal minimumCoverage)
    {
        if (eligibleCount <= 0) return false;
        var coverage = Coverage(eligibleCount, adjustedExpectedCount);
        return coverage is null || coverage.Value >= minimumCoverage;
    }

    /// <summary>
    /// الاتجاه (§5.6): <c>Unknown</c> إذا إحدى الفترتين بلا بيانات **أو** الفترة الحاليّة مفتوحة
    /// (لا Trend رسميّ من فترة جارية)؛ وإلّا Up/Down عند تجاوز الحدّ ±، و<c>Flat</c> خلاف ذلك.
    /// يُحسَب على القيم **غير المقرَّبة** ثمّ يُقرَّب <c>delta</c> عند العرض فقط.
    /// </summary>
    public static (decimal? Delta, KpiTrend Trend) Trend(
        decimal? current, decimal? previous, bool currentIsOpen, decimal deltaThreshold)
    {
        if (current is null || previous is null || currentIsOpen) return (null, KpiTrend.Unknown);
        var delta = current.Value - previous.Value;
        if (delta >= deltaThreshold) return (delta, KpiTrend.Up);
        if (delta <= -deltaThreshold) return (delta, KpiTrend.Down);
        return (delta, KpiTrend.Flat);
    }
}

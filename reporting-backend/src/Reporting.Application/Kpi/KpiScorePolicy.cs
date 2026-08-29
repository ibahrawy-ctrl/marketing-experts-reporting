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

    /// <summary>
    /// DEC-01/9 — الحالات التي تُحتسَب «مكتملة» وتدخل الدرجة والتغطية: <c>Approved</c> (اعتماد المراجِع)
    /// و<c>Closed</c> (إقفال نهائيّ بعد الاعتماد). المسودة وما دون الاعتماد **لا تُحتسَب مكتملة**.
    /// مصدر واحد يُستعمل في الاستعلام وفي الاختبارات معًا — لا قائمة ثانية.
    /// </summary>
    public static readonly KpiEvaluationStatus[] CompletedStatuses =
    {
        KpiEvaluationStatus.Approved,
        KpiEvaluationStatus.Closed
    };

    /// <inheritdoc cref="CompletedStatuses"/>
    public static bool IsCompleted(KpiEvaluationStatus status) =>
        status is KpiEvaluationStatus.Approved or KpiEvaluationStatus.Closed;

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
    /// DEC-01/12 — التغطية **كنسبة مئويّة معروضة**: <c>Completed ÷ AdjustedExpected × 100</c>
    /// مقرَّبة إلى منزلتين. مثال العقد الحاكم: 1 من 9 ⇒ <c>11.11</c>.
    /// </summary>
    public static decimal? CoveragePercent(int completedCount, int adjustedExpectedCount)
    {
        var coverage = Coverage(completedCount, adjustedExpectedCount);
        return coverage is null ? null : Round(coverage.Value * 100m);
    }

    /// <summary>
    /// DEC-01/14 — الدرجة «مؤقّتة» متى وُجدت درجة وكانت التغطية دون الحدّ الأدنى المعتمَد.
    /// المؤقّتة تُعرَض للمستخدم لكنّها لا تدخل المتوسّط الرسميّ ولا التصدير المالي النهائي.
    /// </summary>
    public static bool IsProvisional(decimal? score, int completedCount, int adjustedExpectedCount, decimal minimumCoverage)
        => score is not null && !EligibleForRanking(completedCount, adjustedExpectedCount, minimumCoverage);

    /// <summary>
    /// DEC-01/18 — حالة رحلة KPI الصريحة. الترتيب مقصود ولا يجوز قلبه:
    /// «لا تواتر» يسبق «مُعفى»، و«مُعفى» يسبق «لم يبدأ»، وإلّا اختفى سبب انعدام المقام خلف حالة عامّة.
    /// «قيد الاستكمال» مشروط بأن تكون الفترة ما زالت **مفتوحة** — فلا يُوصَم ربع جارٍ بأنّه ناقص التغطية.
    /// </summary>
    public static KpiJourneyState JourneyState(
        bool cadenceConfigured,
        int expectedCount,
        int adjustedExpectedCount,
        int completedCount,
        bool periodIsOpen,
        decimal minimumCoverage)
    {
        if (!cadenceConfigured) return KpiJourneyState.CadenceNotConfigured;
        if (adjustedExpectedCount <= 0 && expectedCount > 0 && completedCount <= 0) return KpiJourneyState.Exempt;
        if (completedCount <= 0) return KpiJourneyState.NotStarted;

        var coverage = Coverage(completedCount, adjustedExpectedCount);
        if (coverage is null || coverage.Value >= 1m) return KpiJourneyState.CompleteEligible;
        if (coverage.Value >= minimumCoverage) return KpiJourneyState.CompleteEligible;
        return periodIsOpen ? KpiJourneyState.InProgress : KpiJourneyState.InsufficientCoverage;
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

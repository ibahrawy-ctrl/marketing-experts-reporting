using Reporting.Domain.Enums;

namespace Reporting.Domain.Projects360;

/// <summary>
/// نتيجة احتساب مؤشّر واحد (CPW-R3 · §9-8-أ/ب) — ما تعرضه لوحة القيادة عن المؤشّر.
///
/// <para>
/// **لماذا نموذج مستقلّ لا حقول على <c>ProjectKpi</c>؟** لأنّ هذه قيم **مشتقّة** من
/// (<c>CurrentValue</c>, <c>TargetValue</c>, <c>Direction</c>) وآخر قراءتين. تخزينها يخلق
/// مصدرَي حقيقة يفترقان عند أوّل تعديل هدف، ويضيف أعمدة تخالف أثر §18-2.
/// </para>
///
/// <para>
/// **<c>null</c> ليست صفرًا**: هدف ≤ 0 أو قيمة حاليّة غائبة ⟹ <see cref="Achievement"/> =
/// <c>null</c> بمعنى «غير محتسَب»، ويُستبعَد المؤشّر من النتيجة الموزونة بدل أن يسحبها إلى الصفر.
/// </para>
/// </summary>
/// <param name="Achievement">نسبة التحقيق مقصوصة على 0..200 — <c>null</c> ⟹ غير محتسَبة.</param>
/// <param name="Variance">الانحراف عن الهدف = <c>Achievement − 100</c>.</param>
/// <param name="Trend">الاتّجاه من آخر قراءتين بعتبة ±2% — <see cref="ProjectKpiTrend.Unknown"/> دون قراءتين.</param>
public sealed record ProjectKpiAchievement(
    decimal? Achievement,
    decimal? Variance,
    ProjectKpiTrend Trend)
{
    /// <summary>«غير محتسَب» — هدف غير صالح أو لا قراءة بعد.</summary>
    public static ProjectKpiAchievement NotComputed { get; } =
        new(Achievement: null, Variance: null, Trend: ProjectKpiTrend.Unknown);

    /// <summary>هل يدخل هذا المؤشّر في النتيجة الموزونة للمشروع؟</summary>
    public bool IsComputed => Achievement is not null;
}

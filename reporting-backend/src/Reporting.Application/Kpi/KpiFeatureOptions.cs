namespace Reporting.Application.Kpi;

/// <summary>
/// P1 §8 — أعلام محرّك KPI الجديد + العتبات المركزيّة **الاحتياطيّة** (B-6).
/// كلّ الأعلام <c>false</c> افتراضيًّا؛ لا يُقرأ أيّ علم في الواجهة بوصفه طبقة حماية (الحماية بالسياسات والنطاق خادميًّا).
/// ترتيب مصدر العتبة: عتبة إصدار القالب (<c>KpiTemplateVersion</c>) أوّلًا، ثمّ هذه القيم المركزيّة — ولا ثوابت متناثرة.
/// </summary>
public sealed class KpiFeatureOptions
{
    public const string SectionName = "Kpi";

    /// <summary>تفعيل محرّك الحساب الموحّد خلف عقود v2 والـLegacy adapter. افتراضيًّا معطّل حتى Reconciliation على TEST.</summary>
    public bool NewCalculationEngine { get; set; }

    /// <summary>تفعيل فلتر الفترة الموحّد المحلول خادميًّا في الواجهة.</summary>
    public bool UnifiedPeriodFilter { get; set; }

    /// <summary>تفعيل حساب الظلّ (القديم مقابل الجديد) للمقارنة. لا يُشغَّل على أيّ بيئة مشتركة بلا تصريح.</summary>
    public bool ShadowCompare { get; set; }

    /// <summary>عتبة «دون المستهدف» المركزيّة الاحتياطيّة (كانت ثابتًا 60 موزّعًا في الخدمات والواجهة).</summary>
    public decimal DefaultBelowTargetThreshold { get; set; } = 60m;

    /// <summary>عتبة «يحتاج دعمًا» المركزيّة الاحتياطيّة (كانت ثابتًا 70 موزّعًا في الواجهة).</summary>
    public decimal DefaultSupportThreshold { get; set; } = 70m;

    /// <summary>أدنى تغطية للدخول في الترتيب والمقارنة الرسميّة (B-5 = 0.75).</summary>
    public decimal MinimumCoverageForRanking { get; set; } = 0.75m;

    /// <summary>حدّ <c>delta</c> المطلق لاعتبار الاتجاه صاعدًا/هابطًا بدل مستقرّ (5.6 = 2.00).</summary>
    public decimal TrendDeltaThreshold { get; set; } = 2.00m;
}

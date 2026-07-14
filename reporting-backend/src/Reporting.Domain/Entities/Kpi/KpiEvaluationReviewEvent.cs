using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>
/// سجل حوكمة مُهيكَل لكل حدث مراجعة على تقييم KPI (ADMIN-GOVERNANCE-R1):
/// الإرسال/بدء المراجعة/الاعتماد/طلب التعديل/الرفض/الإشارة/التعليق/طلب إعادة الفتح/إعادة الفتح/الحذف الإداريّ.
/// يحفظ لقطة منظَّمة للقيم السابقة (PreviousValuesJson) قبل أيّ تغيير — لا overwrite صامت.
/// FK إلى kpi_evaluations بسلوك RESTRICT كي لا يُمحى سجلّ الحوكمة والتاريخ عند أيّ حذف.
/// </summary>
public class KpiEvaluationReviewEvent : BaseEntity
{
    public Guid KpiEvaluationId { get; set; }
    public KpiEvaluation? KpiEvaluation { get; set; }

    /// <summary>submitted/started_review/approved/needs_revision/rejected/flagged/commented/reopen_requested/reopened/admin_deleted.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>الفاعل (المُدخِل/المُراجِع/HR/الأدمن). لا Cascade — يبقى السجلّ حتى لو تعطّل/تغيّر الحساب.</summary>
    public Guid ActorId { get; set; }

    /// <summary>حالة التقييم قبل الحدث (نصّ مطابق لتخزين الحالة)، إن وُجدت.</summary>
    public string? FromStatus { get; set; }
    /// <summary>حالة التقييم بعد الحدث، إن تغيّرت.</summary>
    public string? ToStatus { get; set; }

    /// <summary>
    /// لقطة منظَّمة للقيم قبل التغيير (jsonb): {status, totalScore, results:[{metricId,rawValue,score,weight}], reviewerId, reviewedAtUtc}.
    /// لا يقتصر على الدرجة الإجمالية.
    /// </summary>
    public string? PreviousValuesJson { get; set; }

    /// <summary>سبب/تعليق الحدث (إلزاميّ لطلب التعديل/الرفض/الإشارة/الحذف/إعادة الفتح).</summary>
    public string? Reason { get; set; }
}

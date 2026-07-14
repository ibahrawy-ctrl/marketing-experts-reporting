using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>تقييم KPI لموظف عن فترة محددة؛ يجمّع نتائج المؤشرات في درجة مرجّحة.</summary>
public class KpiEvaluation : BaseEntity
{
    public Guid KpiTemplateVersionId { get; set; }
    public Guid SubjectUserId { get; set; }
    public Guid? EvaluatorId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? DepartmentId { get; set; }

    public PeriodType PeriodType { get; set; } = PeriodType.Weekly;
    public string PeriodKey { get; set; } = string.Empty;

    public KpiEvaluationStatus Status { get; set; } = KpiEvaluationStatus.Draft;
    /// <summary>الدرجة الإجمالية المرجّحة (0–100).</summary>
    public decimal? TotalScore { get; set; }
    public KpiTrend Trend { get; set; } = KpiTrend.Unknown;
    public DateTime? SubmittedAtUtc { get; set; }

    // المراجعة الحوكميّة (ADMIN-GOVERNANCE-R1) — المُراجِع المعيَّن عند الإرسال وقراره.
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }

    // الحذف الإداريّ الناعم — لا حذف صفوف؛ يُستبعَد عبر Global Query Filter، والأثر التدقيقيّ يبقى كاملًا.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? DeletionReason { get; set; }

    public ICollection<KpiResult> Results { get; set; } = new List<KpiResult>();
    public ICollection<KpiEvaluationReviewEvent> ReviewEvents { get; set; } = new List<KpiEvaluationReviewEvent>();
}

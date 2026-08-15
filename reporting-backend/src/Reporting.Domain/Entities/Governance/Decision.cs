using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>قرار إداري موثّق، قد يرتبط بتسليم أو مخاطرة أو تصعيد.</summary>
public class Decision : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid MadeById { get; set; }
    public DecisionStatus Status { get; set; } = DecisionStatus.Proposed;
    public Guid? RelatedSubmissionId { get; set; }
    public Guid? RelatedRiskId { get; set; }
    public Guid? RelatedEscalationId { get; set; }
    public Guid? RelatedKpiEvaluationId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>الإجراء التالي لمتابعة تنفيذ القرار.</summary>
    public string? NextAction { get; set; }

    /// <summary>
    /// CPW-R3 · D-05 · §5-8 — ربط **اختياريّ** بمشروع. عمود واحد <c>Guid?</c> بفهرس عاديّ،
    /// والقرارات القائمة كلّها تبقى <c>NULL</c> ⟹ **صفر Backfill وصفر صفّ يتغيّر**.
    ///
    /// <para>
    /// **لماذا عمود صريح لا نمط <c>EntityType + EntityId</c>؟** لأنّ النمط متعدّد الأشكال يمنع
    /// التكامل المرجعيّ على مستوى القاعدة ويجبر كلّ استعلام على فلترة نصّيّة إضافيّة.
    /// العمود الصريح يبقى إضافيًّا بحتًا ويُفهرَس مباشرةً للوحة القيادة.
    /// </para>
    /// </summary>
    public Guid? ProjectId { get; set; }
}

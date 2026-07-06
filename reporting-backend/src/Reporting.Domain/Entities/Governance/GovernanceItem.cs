using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// بند حوكمة عام (ورشة الحوكمة — GOV-GOVERNANCE-UX1): ملاحظة/مخاطرة/قرار/توصية/متابعة/التزام/أداء/مشكلة تشغيلية
/// تُسجَّل وتُتابَع بشكل منظَّم وقابل للتتبّع. كيان موحَّد مستقلّ — لا يوسّع Risk ولا يكرّر Escalation ولا يمسّ
/// منطق التصعيد الفردي (محجوز لمرحلة GOV-INDIVIDUAL-ESCALATION1). الربط بالتقارير اختياري وإضافي (Option A).
/// </summary>
public class GovernanceItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GovernanceCategory Category { get; set; } = GovernanceCategory.Observation;
    public GovernanceSeverity Severity { get; set; } = GovernanceSeverity.Medium;
    public GovernanceItemStatus Status { get; set; } = GovernanceItemStatus.Open;

    /// <summary>
    /// نطاق التطبيق (GOV-APPLICATION-SCOPE-R1): «يُطبَّق على من؟» — مستقلّ عن AssignedToUserId (مسؤول المتابعة).
    /// يحدّد أيّ مرجع سياقي ملزِم: Company (لا مرجع)؛ Department→DepartmentId؛ Team→TeamId؛ User→RelatedUserId؛ RelatedReport→RelatedSubmissionId.
    /// </summary>
    public GovernanceApplicationScope ApplicationScope { get; set; } = GovernanceApplicationScope.User;

    /// <summary>منشئ البند (الفاعل وقت الإنشاء).</summary>
    public Guid CreatedById { get; set; }

    /// <summary>الموظّف المُسنَد إليه متابعة/معالجة البند (اختياري).</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>السياق التنظيمي الاختياري — إدارة/فريق يخصّهما البند.</summary>
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }

    /// <summary>ربط سياقي اختياري بتقرير مُرسَل (Option A — اختيار من قائمة).</summary>
    public Guid? RelatedSubmissionId { get; set; }

    /// <summary>موظّف يخصّه البند (سياق فردي بحت للعرض — ليس تصعيدًا فرديًّا).</summary>
    public Guid? RelatedUserId { get; set; }

    /// <summary>تاريخ الاستحقاق/المتابعة الاختياري.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>ملخّص المعالجة/الإغلاق (يُملأ عند Resolved/Closed).</summary>
    public string? ResolutionSummary { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedById { get; set; }

    public ICollection<GovernanceItemUpdate> Updates { get; set; } = new List<GovernanceItemUpdate>();
}

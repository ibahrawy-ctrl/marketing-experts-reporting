using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// إجراء حوكمة/متابعة (GOV-ACTION-ITEMS-R1): يحوّل أي تصعيد فردي/بند حوكمة عام/ملاحظة يدوية إلى إجراء قابل للتتبّع
/// بمُسنَد إليه وتاريخ استحقاق وأولوية وحالة وخطّ زمنيّ للمتابعة. كيان مستقلّ تمامًا — لا يوسّع GovernanceItem/Escalation
/// ولا يمسّ سير اعتماد التقارير (Workflow/ApprovalStep/CurrentApproverId). «متأخر» تُحسَب ولا تُخزَّن. لا إشعارات/بريد في هذه المرحلة.
/// </summary>
public class GovernanceActionItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>مصدر الإجراء (يدوي/تصعيد فردي/بند حوكمة عام).</summary>
    public ActionItemSourceType SourceType { get; set; } = ActionItemSourceType.Manual;

    /// <summary>معرّف الكيان المصدر (تصعيد أو بند حوكمة)؛ null عند المصدر اليدوي.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>الموظّف المُسنَد إليه تنفيذ الإجراء.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>مَن أسنَد الإجراء (الفاعل وقت الإسناد/الإنشاء).</summary>
    public Guid? AssignedByUserId { get; set; }

    public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;

    /// <summary>تاريخ الاستحقاق (يُستخدم لاحتساب «متأخر»).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>ملاحظة الإكمال (تُملأ عند Completed).</summary>
    public string? CompletionNote { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    /// <summary>منشئ الإجراء (الفاعل وقت الإنشاء).</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>إجراء حسّاس (موروث من مصدر حسّاس) — لا تُسرَّب تفاصيل المصدر لمن لا يملك صلاحية رؤيته.</summary>
    public bool IsSensitive { get; set; }

    public ICollection<GovernanceActionItemUpdate> Updates { get; set; } = new List<GovernanceActionItemUpdate>();
}

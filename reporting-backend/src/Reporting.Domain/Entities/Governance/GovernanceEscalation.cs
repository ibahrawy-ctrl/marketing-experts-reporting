using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// تصعيد فردي (GOV-INDIVIDUAL-ESCALATION1): رفع موقف يخصّ موظّفًا/إدارة/فريقًا/تقريرًا/سير عمل/بند حوكمة للمراجعة
/// والإسناد والمتابعة بخطّ زمنيّ قابل للتتبّع. كيان مستقلّ تمامًا — لا يوسّع GovernanceItem ولا يمسّ سير اعتماد
/// التقارير (Workflow/ApprovalStep/CurrentApproverId). الرؤية محكومة بالأدوار والنطاق (ScopeResolver، قراءة فقط).
/// </summary>
public class GovernanceEscalation : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EscalationType EscalationType { get; set; } = EscalationType.Other;
    public EscalationSeverity Severity { get; set; } = EscalationSeverity.Medium;
    public GovernanceEscalationStatus Status { get; set; } = GovernanceEscalationStatus.Open;

    /// <summary>رافع التصعيد (الفاعل وقت الإنشاء).</summary>
    public Guid RaisedByUserId { get; set; }

    /// <summary>نوع الهدف الذي يخصّه التصعيد (يحدّد أيّ حقل هدف يُملأ).</summary>
    public EscalationTargetType TargetType { get; set; } = EscalationTargetType.Other;

    /// <summary>الموظّف الهدف (عند TargetType=User).</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>الإدارة الهدف (عند TargetType=Department).</summary>
    public Guid? TargetDepartmentId { get; set; }

    /// <summary>الفريق الهدف (عند TargetType=Team).</summary>
    public Guid? TargetTeamId { get; set; }

    /// <summary>تقرير مُرسَل مرتبط (عند TargetType=Report أو سياق إضافيّ).</summary>
    public Guid? RelatedSubmissionId { get; set; }

    /// <summary>بند حوكمة عام مرتبط (عند TargetType=GovernanceItem أو سياق إضافيّ).</summary>
    public Guid? RelatedGovernanceItemId { get; set; }

    /// <summary>الموظّف المُسنَد إليه معالجة التصعيد (اختياري).</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>ملخّص الحلّ/المعالجة (يُملأ عند Resolved/Closed).</summary>
    public string? Resolution { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public ICollection<GovernanceEscalationUpdate> Updates { get; set; } = new List<GovernanceEscalationUpdate>();
}

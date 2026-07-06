namespace Reporting.Application.Governance;

/// <summary>
/// غرض الدليل الموحّد — يحدّد قواعد النطاق والحسّاسية:
/// <list type="bullet">
/// <item><b>Workspace</b>: اختيار مُسنَد إليه/متعلَّق ضمن نطاق الملكية (ورشة الحوكمة).</item>
/// <item><b>ActionItemAssignee</b>: اختيار مُسنَد إليه لإجراء حوكمة ضمن نطاق المنشئ.</item>
/// <item><b>EscalationTarget</b>: اختيار هدف تصعيد متقاطع (أوسع — يسمح بالرفع خارج النطاق دون توسيع الرؤية).</item>
/// </list>
/// أصحاب الرؤية الواسعة (Admin/CEO/GM/CeoSupport) يرون الكلّ شاملًا الحسابات الحسّاسة في كل الأغراض.
/// </summary>
public enum GovernanceDirectoryPurpose
{
    Workspace = 0,
    ActionItemAssignee = 1,
    EscalationTarget = 2
}

public record GovernanceDirectoryUserDto(Guid Id, string FullName, Guid? DepartmentId, Guid? TeamId);
public record GovernanceDirectoryDepartmentDto(Guid Id, string Name);
public record GovernanceDirectoryTeamDto(Guid Id, string Name, Guid? DepartmentId);

/// <summary>قوائم الدليل الموحّد للحوكمة: المستخدمون + الإدارات + الفِرق ضمن نطاق صلاحية المستخدم الحالي والغرض.</summary>
public record GovernanceDirectoryDto(
    IReadOnlyList<GovernanceDirectoryUserDto> Users,
    IReadOnlyList<GovernanceDirectoryDepartmentDto> Departments,
    IReadOnlyList<GovernanceDirectoryTeamDto> Teams);

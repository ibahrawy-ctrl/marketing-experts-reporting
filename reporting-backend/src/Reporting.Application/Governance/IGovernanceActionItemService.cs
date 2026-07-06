using Reporting.Application.Common;

namespace Reporting.Application.Governance;

/// <summary>
/// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1): تحويل تصعيد فردي/بند حوكمة عام/ملاحظة يدوية إلى إجراء قابل للتتبّع
/// بمُسنَد إليه واستحقاق وأولوية وحالة وخطّ زمنيّ. كيان مستقلّ تمامًا ولا يمسّ سير اعتماد التقارير. الرؤية والصلاحيات
/// تُفرَض داخل الخدمة (واسع/نطاق/HR/منشئ/مُسنَد إليه؛ القراءة غير المصرّح بها تُقنَّع كـ«غير موجود» 404 لا 403).
/// لا إشعارات/بريد في هذه المرحلة.
/// </summary>
public interface IGovernanceActionItemService
{
    Task<Result<IReadOnlyList<GovernanceActionItemListItemDto>>> ListAsync(GovernanceActionItemFilter filter, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> CreateAsync(CreateGovernanceActionItemRequest request, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceActionItemStatusRequest request, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> AssignAsync(Guid id, AssignGovernanceActionItemRequest request, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> ChangeDueDateAsync(Guid id, ChangeGovernanceActionItemDueDateRequest request, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> AddCommentAsync(Guid id, AddGovernanceActionItemCommentRequest request, CancellationToken ct = default);
    Task<Result<GovernanceActionItemDetailDto>> CancelAsync(Guid id, CancelGovernanceActionItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// دليل المُسنَد إليهم: قائمة آمنة على مستوى الشركة لاختيار المُسنَد إليه دون فتح الدليل العام.
    /// الموظّفون = النشطون غير الحسّاسين فقط (تُستبعَد حسابات Admin/CEO/GM/CeoSupport). قراءة فقط ولا يكشف أيّ إجراء.
    /// </summary>
    Task<Result<ActionItemAssigneeDirectoryDto>> GetAssigneeDirectoryAsync(CancellationToken ct = default);
}

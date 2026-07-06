using Reporting.Application.Common;

namespace Reporting.Application.Governance;

/// <summary>ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1): تسجيل ومتابعة ملاحظات الحوكمة العامة بخط زمني.</summary>
public interface IGovernanceItemService
{
    Task<Result<IReadOnlyList<GovernanceItemListItemDto>>> ListAsync(GovernanceItemFilter filter, CancellationToken ct = default);
    Task<Result<GovernanceItemDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<GovernanceItemDetailDto>> CreateAsync(CreateGovernanceItemRequest request, CancellationToken ct = default);
    Task<Result<GovernanceItemDetailDto>> UpdateAsync(Guid id, UpdateGovernanceItemRequest request, CancellationToken ct = default);
    Task<Result<GovernanceItemDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceItemStatusRequest request, CancellationToken ct = default);
    Task<Result<GovernanceItemDetailDto>> AddCommentAsync(Guid id, AddGovernanceItemCommentRequest request, CancellationToken ct = default);

    /// <summary>دليل ورشة الحوكمة الموحّد: قوائم اختيار المُسنَد إليه/المتعلَّق ضمن نطاق الملكية (GOV-DIRECTORY-SCOPE-FIX-R1).</summary>
    Task<Result<GovernanceDirectoryDto>> GetDirectoryAsync(CancellationToken ct = default);
}

using Reporting.Application.Common;

namespace Reporting.Application.Submissions;

/// <summary>خدمة تسليمات التقارير ودورة حياة الاعتماد (8 حالات) مع تفويض قائم على المورد.</summary>
public interface ISubmissionService
{
    Task<Result<SubmissionDto>> CreateOrGetDraftAsync(CreateSubmissionRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDto>> GetAsync(Guid submissionId, CancellationToken ct = default);
    Task<Result<SubmissionDto>> SaveFieldValuesAsync(Guid submissionId, SaveFieldValuesRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDto>> SubmitAsync(Guid submissionId, CancellationToken ct = default);

    Task<Result<SubmissionDto>> ApproveAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDto>> ReturnAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDto>> EscalateAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListAsync(SubmissionFilter filter, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListMineAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListPendingApprovalsAsync(CancellationToken ct = default);
    Task<Result<SubmissionSummaryDto>> SummaryAsync(SubmissionFilter filter, CancellationToken ct = default);
}

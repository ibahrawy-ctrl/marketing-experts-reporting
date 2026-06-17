using Reporting.Application.Common;

namespace Reporting.Application.Development;

/// <summary>وحدة التطوير: الاحتياجات التدريبية وخطط التحسين.</summary>
public interface IDevelopmentService
{
    Task<Result<TrainingNeedDto>> CreateTrainingNeedAsync(CreateTrainingNeedRequest request, CancellationToken ct = default);
    Task<Result<TrainingNeedDto>> UpdateTrainingNeedAsync(Guid id, UpdateTrainingNeedRequest request, CancellationToken ct = default);
    Task<Result<TrainingNeedDto>> GetTrainingNeedAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TrainingNeedDto>>> ListTrainingNeedsAsync(TrainingNeedFilter filter, CancellationToken ct = default);

    Task<Result<ImprovementPlanDto>> CreateImprovementPlanAsync(CreateImprovementPlanRequest request, CancellationToken ct = default);
    Task<Result<ImprovementPlanDto>> UpdateImprovementPlanAsync(Guid id, UpdateImprovementPlanRequest request, CancellationToken ct = default);
    Task<Result<ImprovementPlanDto>> GetImprovementPlanAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ImprovementPlanDto>>> ListImprovementPlansAsync(ImprovementPlanFilter filter, CancellationToken ct = default);
}

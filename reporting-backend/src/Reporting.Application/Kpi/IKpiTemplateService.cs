using Reporting.Application.Common;

namespace Reporting.Application.Kpi;

/// <summary>إدارة قوالب مؤشرات الأداء (مقاييس بأوزان، إصدارات، نشر).</summary>
public interface IKpiTemplateService
{
    Task<Result<KpiTemplateDetailDto>> CreateAsync(CreateKpiTemplateRequest request, Guid ownerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<KpiTemplateDto>>> ListAsync(KpiTemplateFilter filter, CancellationToken ct = default);
    Task<Result<KpiTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<KpiTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateKpiTemplateRequest request, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);

    Task<Result<KpiMetricDto>> AddMetricAsync(Guid versionId, UpsertKpiMetricRequest request, CancellationToken ct = default);
    Task<Result<KpiMetricDto>> UpdateMetricAsync(Guid metricId, UpsertKpiMetricRequest request, CancellationToken ct = default);
    Task<Result> DeleteMetricAsync(Guid metricId, CancellationToken ct = default);

    Task<Result<KpiTemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default);
    Task<Result<KpiTemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default);
}

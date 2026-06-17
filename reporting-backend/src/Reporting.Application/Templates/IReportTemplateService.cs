using Reporting.Application.Common;

namespace Reporting.Application.Templates;

public interface IReportTemplateService
{
    Task<Result<ReportTemplateDetailDto>> CreateAsync(CreateTemplateRequest request, Guid ownerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ReportTemplateDto>>> ListAsync(TemplateFilter filter, CancellationToken ct = default);
    Task<Result<ReportTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ReportTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);

    // بانِي الحقول — على الإصدار المسودة فقط
    Task<Result<TemplateFieldDto>> AddFieldAsync(Guid versionId, UpsertFieldRequest request, CancellationToken ct = default);
    Task<Result<TemplateFieldDto>> UpdateFieldAsync(Guid fieldId, UpsertFieldRequest request, CancellationToken ct = default);
    Task<Result> DeleteFieldAsync(Guid fieldId, CancellationToken ct = default);
    Task<Result> ReorderFieldsAsync(Guid versionId, IReadOnlyList<Guid> orderedFieldIds, CancellationToken ct = default);

    // الإصدارات
    Task<Result<TemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default);
    Task<Result<TemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default);
}

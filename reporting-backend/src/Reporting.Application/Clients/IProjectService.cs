using Reporting.Application.Common;

namespace Reporting.Application.Clients;

public interface IProjectService
{
    Task<Result<IReadOnlyList<ProjectDto>>> ListAsync(ProjectFilter filter, CancellationToken ct = default);
    Task<Result<ProjectDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectDto>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result<ProjectDto>> ArchiveAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectDto>> ReactivateAsync(Guid id, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectSummaryDto>> GetSummaryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// شريحة مشروع واحد من تسليم تقرير واحد. الرفض موحّد بـ<c>project.not_found</c> في
    /// كلّ حالات «خارج النطاق» و«غير مرتبط» و«غير موجود» منعًا للتعداد.
    /// </summary>
    Task<Result<ProjectReportSliceDto>> GetReportSliceAsync(Guid id, Guid submissionId, CancellationToken ct = default);
}

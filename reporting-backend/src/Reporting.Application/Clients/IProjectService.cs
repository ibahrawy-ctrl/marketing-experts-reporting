using Reporting.Application.Common;

namespace Reporting.Application.Clients;

public interface IProjectService
{
    Task<Result<IReadOnlyList<ProjectDto>>> ListAsync(ProjectFilter filter, CancellationToken ct = default);
    Task<Result<ProjectDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectDto>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result<ProjectDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectSummaryDto>> GetSummaryAsync(Guid id, CancellationToken ct = default);
}

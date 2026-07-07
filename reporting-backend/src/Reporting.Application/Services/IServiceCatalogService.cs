using Reporting.Application.Common;

namespace Reporting.Application.Services;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<Result<ServiceDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ServiceDto>> CreateAsync(CreateServiceRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<ServiceDto>> UpdateAsync(Guid id, UpdateServiceRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<ServiceDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default);
    Task<Result<ServiceDeleteResult>> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}

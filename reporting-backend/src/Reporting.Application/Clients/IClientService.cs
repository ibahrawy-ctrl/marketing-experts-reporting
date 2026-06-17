using Reporting.Application.Common;

namespace Reporting.Application.Clients;

public interface IClientService
{
    Task<Result<IReadOnlyList<ClientDto>>> ListAsync(ClientFilter filter, CancellationToken ct = default);
    Task<Result<ClientDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ClientDto>> CreateAsync(CreateClientRequest request, CancellationToken ct = default);
    Task<Result<ClientDto>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);
    Task<Result<ClientDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default);
    Task<Result<ClientHealthReport>> GetHealthAsync(CancellationToken ct = default);
}

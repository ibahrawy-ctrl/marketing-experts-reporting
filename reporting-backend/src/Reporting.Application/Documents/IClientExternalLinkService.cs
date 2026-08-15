using Reporting.Application.Common;

namespace Reporting.Application.Documents;

/// <summary>
/// إدارة «الروابط المهمّة» للعميل (CPW-R1B2). نفس نموذج الصلاحيّة المستخدم في مستندات العميل.
/// لا حذف نهائيّ من الواجهة — التعطيل (<c>IsActive=false</c>) هو المسار الوحيد.
/// </summary>
public interface IClientExternalLinkService
{
    Task<Result<IReadOnlyList<ClientExternalLinkDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ClientExternalLinkDto>> CreateAsync(Guid clientId, CreateClientExternalLinkRequest request, CancellationToken ct = default);
    Task<Result<ClientExternalLinkDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientExternalLinkRequest request, CancellationToken ct = default);
    Task<Result<ClientExternalLinkDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default);
}

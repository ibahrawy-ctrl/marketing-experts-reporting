using Reporting.Application.Common;

namespace Reporting.Application.Clients;

/// <summary>
/// إدارة القنوات الرقمية للعميل (Client 360 — CPW-R1B). القراءة محكومة بنطاق رؤية العميل.
/// الكتابة مسموحة إن كان المستخدم مديرًا أساسيًّا للعميل ضمن نطاقه، أو كان هو مدير الحساب للعميل.
/// التعطيل بدل الحذف افتراضيًّا. **لا تُخزَّن أيّ أسرار/رموز وصول** — معرّفات مرجعية فقط.
/// </summary>
public interface IClientDigitalChannelService
{
    Task<Result<IReadOnlyList<ClientDigitalChannelDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ClientDigitalChannelDto>> CreateAsync(Guid clientId, CreateClientDigitalChannelRequest request, CancellationToken ct = default);
    Task<Result<ClientDigitalChannelDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientDigitalChannelRequest request, CancellationToken ct = default);
    Task<Result<ClientDigitalChannelDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default);
}

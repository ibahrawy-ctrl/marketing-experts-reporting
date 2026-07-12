using Reporting.Application.Common;

namespace Reporting.Application.Clients;

/// <summary>
/// إدارة جهات اتصال العميل (Client 360 — CPW-R1B). القراءة محكومة بنطاق رؤية العميل.
/// الكتابة مسموحة إن كان المستخدم مديرًا أساسيًّا للعميل ضمن نطاقه، أو كان هو مدير الحساب للعميل.
/// التعطيل بدل الحذف افتراضيًّا. جهة اتصال أساسية واحدة نشطة كحدّ أقصى لكلّ عميل (معاملة + فهرس فريد جزئيّ).
/// </summary>
public interface IClientContactService
{
    Task<Result<IReadOnlyList<ClientContactDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ClientContactDto>> CreateAsync(Guid clientId, CreateClientContactRequest request, CancellationToken ct = default);
    Task<Result<ClientContactDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientContactRequest request, CancellationToken ct = default);
    Task<Result<ClientContactDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default);
}

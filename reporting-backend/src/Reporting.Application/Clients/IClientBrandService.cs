using Reporting.Application.Common;

namespace Reporting.Application.Clients;

/// <summary>
/// إدارة ملفّ البراند للعميل (Client 360 — CPW-R1B) — علاقة 1:0..1. القراءة محكومة بنطاق رؤية العميل.
/// الكتابة (Upsert) مسموحة إن كان المستخدم مديرًا أساسيًّا للعميل ضمن نطاقه، أو كان هو مدير الحساب للعميل.
/// يُرجِع GetAsync قيمة فارغة (null داخل Result ناجح) عند عدم وجود ملفّ بعد.
/// </summary>
public interface IClientBrandService
{
    Task<Result<ClientBrandProfileDto?>> GetAsync(Guid clientId, CancellationToken ct = default);
    Task<Result<ClientBrandProfileDto>> UpsertAsync(Guid clientId, UpsertClientBrandProfileRequest request, CancellationToken ct = default);
}

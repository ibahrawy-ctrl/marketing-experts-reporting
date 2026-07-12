using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة جهات اتصال العميل (Client 360 — CPW-R1B). القراءة محكومة بنطاق رؤية العميل (IClientProjectAccess).
/// الكتابة محكومة داخل الخدمة (Resource-Based): إمّا دور إداريّ أساسيّ (ClientCoreManagers) مع رؤية العميل،
/// أو كون المستخدم هو مدير الحساب للعميل نفسه (Client.AccountManagerId == المستخدم الحالي).
/// التعطيل بدل الحذف افتراضيًّا. جهة اتصال أساسية واحدة نشطة كحدّ أقصى لكلّ عميل — تُفرَض عبر معاملة صريحة
/// (تنزيل الأساسيّ السابق ثم ترقية الجديد) بجانب الفهرس الفريد الجزئيّ في القاعدة.
/// </summary>
public class ClientContactService : IClientContactService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ClientContactService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ClientContactDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ClientContactDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(clientId)) return Result<IReadOnlyList<ClientContactDto>>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return Result<IReadOnlyList<ClientContactDto>>.Failure("العميل غير موجود.", "client.not_found");

        var q = _db.ClientContacts.AsNoTracking().Where(x => x.ClientId == clientId);
        if (!includeInactive) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
        return Result<IReadOnlyList<ClientContactDto>>.Success(rows.Select(Map).ToList());
    }

    public async Task<Result<ClientContactDto>> CreateAsync(Guid clientId, CreateClientContactRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientContactDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ClientContactDto>.Failure("اسم جهة الاتصال مطلوب.", "client_contact.name_required");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientContactDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.PreferredContactMethodCode, request.Notes, request.JobTitle, request.Department);
        if (validation is not null) return Result<ClientContactDto>.Failure(validation.Value.message, validation.Value.code);

        var entity = new ClientContact
        {
            ClientId = clientId,
            Name = request.Name.Trim(),
            JobTitle = Trim(request.JobTitle),
            Department = Trim(request.Department),
            Email = Trim(request.Email),
            Phone = Trim(request.Phone),
            WhatsApp = Trim(request.WhatsApp),
            PreferredContactMethodCode = Trim(request.PreferredContactMethodCode),
            IsPrimary = request.IsPrimary,
            IsFinancialContact = request.IsFinancialContact,
            Notes = Trim(request.Notes),
            IsActive = true,
            SortOrder = request.SortOrder
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (entity.IsPrimary) await DemoteActivePrimariesAsync(clientId, null, ct);
        _db.ClientContacts.Add(entity);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(uid, "client_contact.created", nameof(ClientContact), entity.Id, ct: ct);

        return Result<ClientContactDto>.Success(Map(entity));
    }

    public async Task<Result<ClientContactDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientContactRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientContactDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ClientContactDto>.Failure("اسم جهة الاتصال مطلوب.", "client_contact.name_required");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientContactDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.PreferredContactMethodCode, request.Notes, request.JobTitle, request.Department);
        if (validation is not null) return Result<ClientContactDto>.Failure(validation.Value.message, validation.Value.code);

        var entity = await _db.ClientContacts.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientContactDto>.Failure("جهة الاتصال غير موجودة.", "client_contact.not_found");

        // الأساسيّ يُفرَض على النشط فقط — لا تُرقَّى جهة معطّلة.
        var wantsPrimary = request.IsPrimary && entity.IsActive;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (wantsPrimary && !entity.IsPrimary) await DemoteActivePrimariesAsync(clientId, entity.Id, ct);
        entity.Name = request.Name.Trim();
        entity.JobTitle = Trim(request.JobTitle);
        entity.Department = Trim(request.Department);
        entity.Email = Trim(request.Email);
        entity.Phone = Trim(request.Phone);
        entity.WhatsApp = Trim(request.WhatsApp);
        entity.PreferredContactMethodCode = Trim(request.PreferredContactMethodCode);
        entity.IsPrimary = wantsPrimary;
        entity.IsFinancialContact = request.IsFinancialContact;
        entity.Notes = Trim(request.Notes);
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(uid, "client_contact.updated", nameof(ClientContact), entity.Id, ct: ct);

        return Result<ClientContactDto>.Success(Map(entity));
    }

    public async Task<Result<ClientContactDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientContactDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientContactDto>.Failure(auth.Value.message, auth.Value.code);

        var entity = await _db.ClientContacts.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientContactDto>.Failure("جهة الاتصال غير موجودة.", "client_contact.not_found");
        if (entity.IsActive == isActive)
            return Result<ClientContactDto>.Failure(isActive ? "جهة الاتصال مُفعّلة بالفعل." : "جهة الاتصال معطّلة بالفعل.", "client_contact.state_unchanged.conflict");

        entity.IsActive = isActive;
        // تعطيل جهة أساسية يُلغي كونها أساسية (لتحرير الفهرس الفريد الجزئيّ).
        if (!isActive && entity.IsPrimary) entity.IsPrimary = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isActive ? "client_contact.activated" : "client_contact.deactivated", nameof(ClientContact), entity.Id, ct: ct);

        return Result<ClientContactDto>.Success(Map(entity));
    }

    // ===== helpers =====
    private async Task DemoteActivePrimariesAsync(Guid clientId, Guid? exceptId, CancellationToken ct)
    {
        var current = await _db.ClientContacts
            .Where(x => x.ClientId == clientId && x.IsPrimary && x.IsActive && (exceptId == null || x.Id != exceptId))
            .ToListAsync(ct);
        foreach (var c in current)
        {
            c.IsPrimary = false;
            c.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (current.Count > 0) await _db.SaveChangesAsync(ct);
    }

    // حارس الكتابة (Resource-Based): مدير أساسيّ ضمن نطاق العميل، أو مدير الحساب للعميل نفسه.
    private async Task<(string message, string code)?> AuthorizeWriteAsync(Guid clientId, Guid uid, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.AccountManagerId })
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return ("العميل غير موجود.", "client.not_found");

        if (client.AccountManagerId == uid) return null;

        if (_currentUser.IsInAnyRole(Roles.ClientCoreManagers))
        {
            var vis = await _access.ResolveAsync(ct);
            if (vis.CanViewClient(clientId)) return null;
        }
        return ("لا تملك صلاحية إدارة بيانات هذا العميل.", "auth.forbidden");
    }

    private static (string message, string code)? Validate(string? contactMethodCode, params string?[] freeTextFields)
    {
        if (!ClientCodeConstants.IsValidContactMethod(contactMethodCode))
            return ("طريقة التواصل المفضَّلة غير معتمَدة.", "client_contact.method_invalid");
        if (ClientFieldGuards.AnyContainsSecret(freeTextFields))
            return ("لا يجوز تخزين كلمات مرور أو رموز وصول في حقول جهة الاتصال.", "client_contact.secret_forbidden");
        return null;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientContactDto Map(ClientContact x) => new(
        x.Id, x.ClientId, x.Name, x.JobTitle, x.Department, x.Email, x.Phone, x.WhatsApp,
        x.PreferredContactMethodCode, x.IsPrimary, x.IsFinancialContact, x.Notes, x.IsActive,
        x.SortOrder, x.CreatedAtUtc, x.UpdatedAtUtc);
}

using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Documents;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة «الروابط المهمّة» للعميل (CPW-R1B2). نفس نموذج صلاحيّة مستندات العميل:
/// القراءة بنطاق رؤية العميل، والكتابة لمدير الحساب أو لمدير أساسيّ يرى العميل.
/// <para>
/// ثابت أمنيّ: الرابط مرجع عنوان فقط — <see cref="ExternalLinkPolicy"/> يرفض أيّ رابط
/// يحمل ما يشبه سرًّا (رمز وصول/كلمة مرور/مفتاح API) أو اعتمادات مضمَّنة أو مخطَّطًا غير http(s).
/// لا حذف نهائيّ: التعطيل هو المسار الوحيد.
/// </para>
/// </summary>
public class ClientExternalLinkService : IClientExternalLinkService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ClientExternalLinkService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ClientExternalLinkDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ClientExternalLinkDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeReadAsync(clientId, ct);
        if (auth is not null) return Result<IReadOnlyList<ClientExternalLinkDto>>.Failure(auth.Value.message, auth.Value.code);

        var q = _db.ClientExternalLinks.AsNoTracking().Where(x => x.ClientId == clientId);
        if (!includeInactive) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync(ct);

        var names = await ResolveNamesAsync(rows.Select(x => x.CreatedByUserId), ct);
        return Result<IReadOnlyList<ClientExternalLinkDto>>.Success(rows.Select(x => Map(x, names)).ToList());
    }

    public async Task<Result<ClientExternalLinkDto>> CreateAsync(Guid clientId, CreateClientExternalLinkRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientExternalLinkDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientExternalLinkDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.Title, request.Url, request.CategoryCode, request.Description);
        if (validation.error is not null) return Result<ClientExternalLinkDto>.Failure(validation.error.Value.message, validation.error.Value.code);

        var entity = new ClientExternalLink
        {
            ClientId = clientId,
            Title = request.Title.Trim(),
            Url = validation.normalizedUrl,
            CategoryCode = request.CategoryCode.Trim(),
            Description = Trim(request.Description),
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedByUserId = uid
        };

        _db.ClientExternalLinks.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_external_link.created", nameof(ClientExternalLink), entity.Id, ct: ct);

        var names = await ResolveNamesAsync(new[] { entity.CreatedByUserId }, ct);
        return Result<ClientExternalLinkDto>.Success(Map(entity, names));
    }

    public async Task<Result<ClientExternalLinkDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientExternalLinkRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientExternalLinkDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientExternalLinkDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.Title, request.Url, request.CategoryCode, request.Description);
        if (validation.error is not null) return Result<ClientExternalLinkDto>.Failure(validation.error.Value.message, validation.error.Value.code);

        var entity = await _db.ClientExternalLinks.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientExternalLinkDto>.Failure("الرابط غير موجود.", "client_external_link.not_found");

        entity.Title = request.Title.Trim();
        entity.Url = validation.normalizedUrl;
        entity.CategoryCode = request.CategoryCode.Trim();
        entity.Description = Trim(request.Description);
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_external_link.updated", nameof(ClientExternalLink), entity.Id, ct: ct);

        var names = await ResolveNamesAsync(new[] { entity.CreatedByUserId }, ct);
        return Result<ClientExternalLinkDto>.Success(Map(entity, names));
    }

    public async Task<Result<ClientExternalLinkDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientExternalLinkDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientExternalLinkDto>.Failure(auth.Value.message, auth.Value.code);

        var entity = await _db.ClientExternalLinks.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientExternalLinkDto>.Failure("الرابط غير موجود.", "client_external_link.not_found");
        if (entity.IsActive == isActive)
            return Result<ClientExternalLinkDto>.Failure(isActive ? "الرابط مُفعّل بالفعل." : "الرابط معطّل بالفعل.", "client_external_link.state_unchanged.conflict");

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isActive ? "client_external_link.activated" : "client_external_link.deactivated",
            nameof(ClientExternalLink), entity.Id, ct: ct);

        var names = await ResolveNamesAsync(new[] { entity.CreatedByUserId }, ct);
        return Result<ClientExternalLinkDto>.Success(Map(entity, names));
    }

    // ===== المساعدات =====

    /// <summary>عميل غير موجود أو خارج النطاق ⇒ 404 موحَّد (منع استكشاف المعرّفات).</summary>
    private async Task<(string message, string code)?> AuthorizeReadAsync(Guid clientId, CancellationToken ct)
    {
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(clientId)) return ("العميل غير موجود.", "client.not_found");
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return ("العميل غير موجود.", "client.not_found");
        return null;
    }

    private async Task<(string message, string code)?> AuthorizeWriteAsync(Guid clientId, Guid uid, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.AccountManagerId })
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return ("العميل غير موجود.", "client.not_found");

        if (client.AccountManagerId == uid) return null;

        var vis = await _access.ResolveAsync(ct);
        // خارج النطاق ⇒ 404 لا 403 حتّى لا يُستدلّ على وجود العميل.
        if (!vis.CanViewClient(clientId)) return ("العميل غير موجود.", "client.not_found");
        if (_currentUser.IsInAnyRole(Roles.ClientCoreManagers)) return null;

        return ("لا تملك صلاحية إدارة روابط هذا العميل.", "auth.forbidden");
    }

    private static ((string message, string code)? error, string normalizedUrl) Validate(
        string? title, string? url, string? categoryCode, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (("عنوان الرابط مطلوب.", "client_external_link.title_required"), string.Empty);
        if (!DocumentCodeConstants.IsValidLinkCategory(categoryCode))
            return (("تصنيف الرابط غير معتمَد.", "client_external_link.category_invalid"), string.Empty);
        if (ClientFieldGuards.AnyContainsSecret(title, description))
            return (("لا يجوز تخزين كلمات مرور أو رموز وصول في حقول الرابط.", "client_external_link.secret_forbidden"), string.Empty);

        var link = ExternalLinkPolicy.Validate(url);
        if (!link.IsValid) return ((link.Error!, link.ErrorCode!), string.Empty);

        return (null, link.NormalizedUrl);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientExternalLinkDto Map(ClientExternalLink x, IReadOnlyDictionary<Guid, string> names) => new(
        x.Id, x.ClientId, x.Title, x.Url, x.CategoryCode, x.Description, x.IsActive, x.SortOrder,
        x.CreatedByUserId, names.TryGetValue(x.CreatedByUserId, out var name) ? name : null,
        x.CreatedAtUtc, x.UpdatedAtUtc);
}

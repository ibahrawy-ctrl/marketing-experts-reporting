using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة القنوات الرقمية للعميل (Client 360 — CPW-R1B). القراءة محكومة بنطاق رؤية العميل.
/// الكتابة (Resource-Based) مسموحة لمدير أساسيّ ضمن النطاق أو لمدير الحساب للعميل نفسه.
/// التعطيل بدل الحذف افتراضيًّا. **لا تُخزَّن أيّ أسرار/رموز وصول** — معرّفات مرجعية فقط.
/// </summary>
public class ClientDigitalChannelService : IClientDigitalChannelService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ClientDigitalChannelService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ClientDigitalChannelDto>>> ListAsync(Guid clientId, bool includeInactive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ClientDigitalChannelDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(clientId)) return Result<IReadOnlyList<ClientDigitalChannelDto>>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return Result<IReadOnlyList<ClientDigitalChannelDto>>.Failure("العميل غير موجود.", "client.not_found");

        var q = _db.ClientDigitalChannels.AsNoTracking().Where(x => x.ClientId == clientId);
        if (!includeInactive) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.PlatformCode).ToListAsync(ct);
        return Result<IReadOnlyList<ClientDigitalChannelDto>>.Success(rows.Select(Map).ToList());
    }

    public async Task<Result<ClientDigitalChannelDto>> CreateAsync(Guid clientId, CreateClientDigitalChannelRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDigitalChannelDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.PlatformCode)) return Result<ClientDigitalChannelDto>.Failure("رمز المنصّة مطلوب.", "client_channel.platform_required");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDigitalChannelDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.PlatformCode, request.AccessStatusCode, request.ProfileUrl,
            request.DisplayName, request.UsernameOrHandle, request.BusinessManagerId, request.AdAccountId,
            request.PixelId, request.Ga4PropertyId, request.GtmContainerId, request.Notes);
        if (validation is not null) return Result<ClientDigitalChannelDto>.Failure(validation.Value.message, validation.Value.code);

        var entity = new ClientDigitalChannel
        {
            ClientId = clientId,
            PlatformCode = request.PlatformCode.Trim(),
            DisplayName = Trim(request.DisplayName),
            UsernameOrHandle = Trim(request.UsernameOrHandle),
            ProfileUrl = Trim(request.ProfileUrl),
            IsActive = true,
            AccessStatusCode = Trim(request.AccessStatusCode),
            BusinessManagerId = Trim(request.BusinessManagerId),
            AdAccountId = Trim(request.AdAccountId),
            PixelId = Trim(request.PixelId),
            Ga4PropertyId = Trim(request.Ga4PropertyId),
            GtmContainerId = Trim(request.GtmContainerId),
            Notes = Trim(request.Notes),
            SortOrder = request.SortOrder
        };
        _db.ClientDigitalChannels.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_channel.created", nameof(ClientDigitalChannel), entity.Id, ct: ct);

        return Result<ClientDigitalChannelDto>.Success(Map(entity));
    }

    public async Task<Result<ClientDigitalChannelDto>> UpdateAsync(Guid clientId, Guid id, UpdateClientDigitalChannelRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDigitalChannelDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.PlatformCode)) return Result<ClientDigitalChannelDto>.Failure("رمز المنصّة مطلوب.", "client_channel.platform_required");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDigitalChannelDto>.Failure(auth.Value.message, auth.Value.code);

        var validation = Validate(request.PlatformCode, request.AccessStatusCode, request.ProfileUrl,
            request.DisplayName, request.UsernameOrHandle, request.BusinessManagerId, request.AdAccountId,
            request.PixelId, request.Ga4PropertyId, request.GtmContainerId, request.Notes);
        if (validation is not null) return Result<ClientDigitalChannelDto>.Failure(validation.Value.message, validation.Value.code);

        var entity = await _db.ClientDigitalChannels.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientDigitalChannelDto>.Failure("القناة الرقمية غير موجودة.", "client_channel.not_found");

        entity.PlatformCode = request.PlatformCode.Trim();
        entity.DisplayName = Trim(request.DisplayName);
        entity.UsernameOrHandle = Trim(request.UsernameOrHandle);
        entity.ProfileUrl = Trim(request.ProfileUrl);
        entity.AccessStatusCode = Trim(request.AccessStatusCode);
        entity.BusinessManagerId = Trim(request.BusinessManagerId);
        entity.AdAccountId = Trim(request.AdAccountId);
        entity.PixelId = Trim(request.PixelId);
        entity.Ga4PropertyId = Trim(request.Ga4PropertyId);
        entity.GtmContainerId = Trim(request.GtmContainerId);
        entity.Notes = Trim(request.Notes);
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_channel.updated", nameof(ClientDigitalChannel), entity.Id, ct: ct);

        return Result<ClientDigitalChannelDto>.Success(Map(entity));
    }

    public async Task<Result<ClientDigitalChannelDto>> SetActiveAsync(Guid clientId, Guid id, bool isActive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDigitalChannelDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDigitalChannelDto>.Failure(auth.Value.message, auth.Value.code);

        var entity = await _db.ClientDigitalChannels.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (entity is null) return Result<ClientDigitalChannelDto>.Failure("القناة الرقمية غير موجودة.", "client_channel.not_found");
        if (entity.IsActive == isActive)
            return Result<ClientDigitalChannelDto>.Failure(isActive ? "القناة مُفعّلة بالفعل." : "القناة معطّلة بالفعل.", "client_channel.state_unchanged.conflict");

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isActive ? "client_channel.activated" : "client_channel.deactivated", nameof(ClientDigitalChannel), entity.Id, ct: ct);

        return Result<ClientDigitalChannelDto>.Success(Map(entity));
    }

    // ===== helpers =====
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

    private static (string message, string code)? Validate(string? platformCode, string? accessStatusCode, string? profileUrl, params string?[] freeTextFields)
    {
        if (!ClientCodeConstants.IsValidPlatform(platformCode))
            return ("رمز المنصّة غير معتمَد.", "client_channel.platform_invalid");
        if (!ClientCodeConstants.IsValidAccessStatus(accessStatusCode))
            return ("رمز حالة الوصول غير معتمَد.", "client_channel.access_status_invalid");
        if (!ClientFieldGuards.IsValidUrl(profileUrl))
            return ("رابط الملفّ الشخصيّ غير صالح (يجب أن يكون http/https).", "client_channel.profile_url_invalid");
        if (ClientFieldGuards.AnyContainsSecret(freeTextFields))
            return ("لا يجوز تخزين كلمات مرور أو رموز وصول في القناة الرقمية — معرّفات مرجعية فقط.", "client_channel.secret_forbidden");
        return null;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientDigitalChannelDto Map(ClientDigitalChannel x) => new(
        x.Id, x.ClientId, x.PlatformCode, x.DisplayName, x.UsernameOrHandle, x.ProfileUrl, x.IsActive,
        x.AccessStatusCode, x.BusinessManagerId, x.AdAccountId, x.PixelId, x.Ga4PropertyId, x.GtmContainerId,
        x.Notes, x.SortOrder, x.CreatedAtUtc, x.UpdatedAtUtc);
}

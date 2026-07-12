using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة ملفّ البراند للعميل (Client 360 — CPW-R1B) — علاقة 1:0..1. القراءة محكومة بنطاق رؤية العميل.
/// الكتابة (Upsert، Resource-Based) مسموحة لمدير أساسيّ ضمن النطاق أو لمدير الحساب للعميل نفسه.
/// GetAsync يُرجِع null داخل Result ناجح عند عدم وجود ملفّ بعد. حقول وصفيّة فقط — لا أسرار.
/// </summary>
public class ClientBrandService : IClientBrandService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ClientBrandService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<ClientBrandProfileDto?>> GetAsync(Guid clientId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ClientBrandProfileDto?>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(clientId)) return Result<ClientBrandProfileDto?>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return Result<ClientBrandProfileDto?>.Failure("العميل غير موجود.", "client.not_found");

        var entity = await _db.ClientBrandProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        return Result<ClientBrandProfileDto?>.Success(entity is null ? null : Map(entity));
    }

    public async Task<Result<ClientBrandProfileDto>> UpsertAsync(Guid clientId, UpsertClientBrandProfileRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientBrandProfileDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientBrandProfileDto>.Failure(auth.Value.message, auth.Value.code);

        if (!ClientFieldGuards.IsValidUrl(request.BrandGuidelinesUrl))
            return Result<ClientBrandProfileDto>.Failure("رابط دليل البراند غير صالح (يجب أن يكون http/https).", "client_brand.guidelines_url_invalid");
        if (ClientFieldGuards.AnyContainsSecret(
                request.BusinessOverview, request.ProductsAndServices, request.TargetAudience, request.TargetLocations,
                request.UniqueSellingProposition, request.Strengths, request.Competitors, request.ToneOfVoice,
                request.PreferredMessages, request.ProhibitedMessages, request.Notes))
            return Result<ClientBrandProfileDto>.Failure("لا يجوز تخزين كلمات مرور أو رموز وصول في ملفّ البراند.", "client_brand.secret_forbidden");

        var entity = await _db.ClientBrandProfiles.FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        var created = entity is null;
        if (entity is null)
        {
            entity = new ClientBrandProfile { ClientId = clientId };
            _db.ClientBrandProfiles.Add(entity);
        }

        entity.BusinessOverview = Trim(request.BusinessOverview);
        entity.ProductsAndServices = Trim(request.ProductsAndServices);
        entity.TargetAudience = Trim(request.TargetAudience);
        entity.TargetLocations = Trim(request.TargetLocations);
        entity.UniqueSellingProposition = Trim(request.UniqueSellingProposition);
        entity.Strengths = Trim(request.Strengths);
        entity.Competitors = Trim(request.Competitors);
        entity.ToneOfVoice = Trim(request.ToneOfVoice);
        entity.PreferredMessages = Trim(request.PreferredMessages);
        entity.ProhibitedMessages = Trim(request.ProhibitedMessages);
        entity.BrandGuidelinesUrl = Trim(request.BrandGuidelinesUrl);
        entity.Notes = Trim(request.Notes);
        if (!created) entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, created ? "client_brand.created" : "client_brand.updated", nameof(ClientBrandProfile), clientId, ct: ct);

        return Result<ClientBrandProfileDto>.Success(Map(entity));
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

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientBrandProfileDto Map(ClientBrandProfile x) => new(
        x.ClientId, x.BusinessOverview, x.ProductsAndServices, x.TargetAudience, x.TargetLocations,
        x.UniqueSellingProposition, x.Strengths, x.Competitors, x.ToneOfVoice, x.PreferredMessages,
        x.ProhibitedMessages, x.BrandGuidelinesUrl, x.Notes, x.CreatedAtUtc, x.UpdatedAtUtc);
}

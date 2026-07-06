using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1). إدارة المنح (Admin فقط عبر السياسة عند نقطة النهاية)
/// + اشتقاق معرّفات المُرسِلين المُصرَّح برؤية تقاريرهم. معزولة تمامًا: لا تستدعي ScopeResolver ولا تمسّ KPI/Dashboard/
/// المشاريع/العملاء/عضوية الفِرق؛ المعرّفات مرجعية فقط (بلا FK صلب). الإلغاء soft (لا حذف صلب).
/// </summary>
public class ReportViewGrantService : IReportViewGrantService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public ReportViewGrantService(AppDbContext db, ICurrentUser currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ReportViewGrantDto>>> ListAsync(bool includeRevoked = false, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<IReadOnlyList<ReportViewGrantDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var q = _db.ReportViewGrants.AsNoTracking().AsQueryable();
        if (!includeRevoked) q = q.Where(g => g.IsActive);
        var grants = await q.OrderByDescending(g => g.CreatedAtUtc).ToListAsync(ct);
        return Result<IReadOnlyList<ReportViewGrantDto>>.Success(await MapManyAsync(grants, ct));
    }

    public async Task<Result<ReportViewGrantDto>> CreateAsync(CreateReportViewGrantRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid actorId)
            return Result<ReportViewGrantDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // تحقّق اتساق النطاق/الهدف.
        if (request.ScopeKind == ReportViewGrantScopeKind.User)
        {
            if (request.TargetUserId is not Guid || request.TargetTeamId is not null)
                return Result<ReportViewGrantDto>.Failure("نطاق المستخدم يتطلّب مستخدمًا مستهدَفًا واحدًا دون فريق.", "report_view_grant.scope_invalid");
        }
        else // Team
        {
            if (request.TargetTeamId is not Guid || request.TargetUserId is not null)
                return Result<ReportViewGrantDto>.Failure("نطاق الفريق يتطلّب فريقًا مستهدَفًا واحدًا دون مستخدم.", "report_view_grant.scope_invalid");
        }

        // وجود المستفيد.
        if (!await _db.Users.AnyAsync(u => u.Id == request.GranteeUserId, ct))
            return Result<ReportViewGrantDto>.Failure("المستفيد غير موجود.", "report_view_grant.grantee_not_found");

        if (request.ScopeKind == ReportViewGrantScopeKind.User)
        {
            if (request.TargetUserId == request.GranteeUserId)
                return Result<ReportViewGrantDto>.Failure("لا حاجة لمنح المستخدم رؤية تقاريره.", "report_view_grant.self_target");
            if (!await _db.Users.AnyAsync(u => u.Id == request.TargetUserId, ct))
                return Result<ReportViewGrantDto>.Failure("المستخدم المستهدَف غير موجود.", "report_view_grant.target_not_found");
        }
        else
        {
            if (!await _db.Teams.AnyAsync(t => t.Id == request.TargetTeamId, ct))
                return Result<ReportViewGrantDto>.Failure("الفريق المستهدَف غير موجود.", "report_view_grant.target_not_found");
        }

        // البحث عن منح قائم لنفس الثنائية (نشط ⇒ تعارض؛ مُلغًى ⇒ إعادة تفعيل).
        var existing = await _db.ReportViewGrants.FirstOrDefaultAsync(g =>
            g.GranteeUserId == request.GranteeUserId
            && g.ScopeKind == request.ScopeKind
            && g.TargetUserId == request.TargetUserId
            && g.TargetTeamId == request.TargetTeamId, ct);

        ReportViewGrant grant;
        string action;
        if (existing is not null)
        {
            if (existing.IsActive)
                return Result<ReportViewGrantDto>.Failure("يوجد منح نشط مطابق بالفعل.", "report_view_grant.duplicate.conflict");
            // إعادة تفعيل المُلغى بدل إنشاء صفّ جديد.
            existing.IsActive = true;
            existing.RevokedAtUtc = null;
            existing.RevokedByUserId = null;
            existing.ExpiresAtUtc = request.ExpiresAtUtc;
            existing.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            existing.CreatedByUserId = actorId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            grant = existing;
            action = "report_view_grant.reactivated";
        }
        else
        {
            grant = new ReportViewGrant
            {
                GranteeUserId = request.GranteeUserId,
                ScopeKind = request.ScopeKind,
                TargetUserId = request.TargetUserId,
                TargetTeamId = request.TargetTeamId,
                IsActive = true,
                CreatedByUserId = actorId,
                ExpiresAtUtc = request.ExpiresAtUtc,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            _db.ReportViewGrants.Add(grant);
            action = "report_view_grant.created";
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, action, nameof(ReportViewGrant), grant.Id,
            JsonSerializer.Serialize(new
            {
                grant.GranteeUserId,
                scopeKind = grant.ScopeKind.ToString(),
                grant.TargetUserId,
                grant.TargetTeamId
            }), ct: ct);

        return Result<ReportViewGrantDto>.Success((await MapManyAsync(new[] { grant }, ct))[0]);
    }

    public async Task<Result> RevokeAsync(Guid grantId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid actorId)
            return Result.Failure("غير مصرّح.", "auth.unauthenticated");

        var grant = await _db.ReportViewGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct);
        if (grant is null) return Result.Failure("المنح غير موجود.", "report_view_grant.not_found");
        if (!grant.IsActive) return Result.Success();

        grant.IsActive = false;
        grant.RevokedAtUtc = DateTime.UtcNow;
        grant.RevokedByUserId = actorId;
        grant.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "report_view_grant.revoked", nameof(ReportViewGrant), grant.Id,
            JsonSerializer.Serialize(new { grant.GranteeUserId, scopeKind = grant.ScopeKind.ToString() }), ct: ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ReportViewGrantDto>>> EffectiveForMeAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<ReportViewGrantDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var now = DateTime.UtcNow;
        var grants = await _db.ReportViewGrants.AsNoTracking()
            .Where(g => g.GranteeUserId == userId && g.IsActive
                        && (g.ExpiresAtUtc == null || g.ExpiresAtUtc > now))
            .OrderByDescending(g => g.CreatedAtUtc)
            .ToListAsync(ct);
        return Result<IReadOnlyList<ReportViewGrantDto>>.Success(await MapManyAsync(grants, ct));
    }

    public async Task<IReadOnlySet<Guid>> ResolveGrantedSubmitterIdsAsync(Guid granteeUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var grants = await _db.ReportViewGrants.AsNoTracking()
            .Where(g => g.GranteeUserId == granteeUserId && g.IsActive
                        && (g.ExpiresAtUtc == null || g.ExpiresAtUtc > now))
            .Select(g => new { g.ScopeKind, g.TargetUserId, g.TargetTeamId })
            .ToListAsync(ct);

        if (grants.Count == 0) return new HashSet<Guid>();

        var result = new HashSet<Guid>();
        foreach (var g in grants.Where(g => g.ScopeKind == ReportViewGrantScopeKind.User && g.TargetUserId is Guid))
            result.Add(g.TargetUserId!.Value);

        var teamIds = grants.Where(g => g.ScopeKind == ReportViewGrantScopeKind.Team && g.TargetTeamId is Guid)
            .Select(g => g.TargetTeamId!.Value).Distinct().ToList();
        if (teamIds.Count > 0)
        {
            // أعضاء الفريق الأساسيّون (ApplicationUser.TeamId) فقط — لا عضويات إضافية، لا عبر ScopeResolver.
            var members = await _db.Users.AsNoTracking()
                .Where(u => u.TeamId != null && teamIds.Contains(u.TeamId.Value))
                .Select(u => u.Id)
                .ToListAsync(ct);
            foreach (var id in members) result.Add(id);
        }

        return result;
    }

    private async Task<IReadOnlyList<ReportViewGrantDto>> MapManyAsync(IReadOnlyList<ReportViewGrant> grants, CancellationToken ct)
    {
        if (grants.Count == 0) return Array.Empty<ReportViewGrantDto>();

        var userIds = grants.Select(g => g.GranteeUserId)
            .Concat(grants.Where(g => g.TargetUserId is Guid).Select(g => g.TargetUserId!.Value))
            .Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var teamIds = grants.Where(g => g.TargetTeamId is Guid).Select(g => g.TargetTeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Teams.AsNoTracking()
                .Where(t => teamIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);

        return grants.Select(g => new ReportViewGrantDto(
            g.Id,
            g.GranteeUserId,
            userNames.GetValueOrDefault(g.GranteeUserId, string.Empty),
            g.ScopeKind,
            g.TargetUserId,
            g.TargetUserId is Guid tu ? userNames.GetValueOrDefault(tu) : null,
            g.TargetTeamId,
            g.TargetTeamId is Guid tt ? teamNames.GetValueOrDefault(tt) : null,
            g.IsActive,
            g.CreatedAtUtc,
            g.CreatedByUserId,
            g.RevokedAtUtc,
            g.ExpiresAtUtc,
            g.Notes)).ToList();
    }
}

using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ManagementNoteService : IManagementNoteService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;

    public ManagementNoteService(AppDbContext db, ICurrentUser currentUser,
        IScopeResolver scope, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ManagementNoteDto>>> ListAsync(
        ManagementNoteEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid)
            return Result<IReadOnlyList<ManagementNoteDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!await CanAccessEntityAsync(entityType, entityId, ct))
            return Result<IReadOnlyList<ManagementNoteDto>>.Failure("لا تملك صلاحية الوصول لهذا السياق.", "auth.forbidden");

        var rows = await _db.ManagementNotes.AsNoTracking()
            .Where(n => n.EntityType == entityType && n.EntityId == entityId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        var names = await UserNamesAsync(
            rows.SelectMany(n => new[] { n.AuthorId, n.ResolvedById ?? Guid.Empty }), ct);
        return Result<IReadOnlyList<ManagementNoteDto>>.Success(rows.Select(n => Map(n, names)).ToList());
    }

    public async Task<Result<ManagementNoteDto>> CreateAsync(CreateManagementNoteRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ManagementNoteDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<ManagementNoteDto>.Failure("نص الملاحظة مطلوب.", "note.body_required");
        if (request.EntityId == Guid.Empty)
            return Result<ManagementNoteDto>.Failure("المرجع المرتبط مطلوب.", "note.entity_required");
        if (!await EntityExistsAsync(request.EntityType, request.EntityId, ct))
            return Result<ManagementNoteDto>.Failure("الكيان المرتبط غير موجود.", "note.entity.not_found");
        if (!await CanAccessEntityAsync(request.EntityType, request.EntityId, ct))
            return Result<ManagementNoteDto>.Failure("لا تملك صلاحية إضافة ملاحظة على هذا الكيان.", "auth.forbidden");

        var note = new ManagementNote
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            AuthorId = uid,
            NoteType = request.NoteType,
            Body = request.Body.Trim(),
            RequiresAction = request.RequiresAction,
            Status = ManagementNoteStatus.Open
        };
        _db.ManagementNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "management_note.created", nameof(ManagementNote), note.Id, ct: ct);

        return Result<ManagementNoteDto>.Success(await BuildAsync(note.Id, ct));
    }

    public async Task<Result<ManagementNoteDto>> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ManagementNoteDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var note = await _db.ManagementNotes.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return Result<ManagementNoteDto>.Failure("الملاحظة غير موجودة.", "note.not_found");
        if (!await CanAccessEntityAsync(note.EntityType, note.EntityId, ct))
            return Result<ManagementNoteDto>.Failure("لا تملك صلاحية معالجة هذه الملاحظة.", "auth.forbidden");

        note.Status = ManagementNoteStatus.Resolved;
        note.ResolvedById = uid;
        note.ResolvedAtUtc = DateTime.UtcNow;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "management_note.resolved", nameof(ManagementNote), note.Id, ct: ct);

        return Result<ManagementNoteDto>.Success(await BuildAsync(note.Id, ct));
    }

    // ===== helpers =====

    private async Task<bool> EntityExistsAsync(ManagementNoteEntityType type, Guid id, CancellationToken ct) => type switch
    {
        ManagementNoteEntityType.ReportSubmission => await _db.ReportSubmissions.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.User => await _db.Users.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.KpiEvaluation => await _db.KpiEvaluations.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.Team => await _db.Teams.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.Escalation => await _db.Escalations.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.Decision => await _db.Decisions.AnyAsync(x => x.Id == id, ct),
        ManagementNoteEntityType.Risk => await _db.Risks.AnyAsync(x => x.Id == id, ct),
        _ => false
    };

    /// <summary>
    /// الوصول للملاحظة مرتبط بالسياق: من يحق له رؤية الكيان المرتبط يحق له رؤية ملاحظاته.
    /// أصحاب صلاحية الحوكمة يرون الكل؛ غيرهم يُقيَّد بنطاق الرؤية (own/team/department).
    /// </summary>
    private async Task<bool> CanAccessEntityAsync(ManagementNoteEntityType type, Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid uid) return false;
        if (RoleAccess.CanViewGovernance(_currentUser.Roles)) return true;

        var scope = await _scope.ResolveAsync(ct);

        switch (type)
        {
            case ManagementNoteEntityType.ReportSubmission:
            {
                var subjectId = await _db.ReportSubmissions.Where(x => x.Id == id)
                    .Select(x => (Guid?)x.SubmitterId).FirstOrDefaultAsync(ct);
                return subjectId is Guid s && scope.Contains(s);
            }
            case ManagementNoteEntityType.User:
                return scope.Contains(id);
            case ManagementNoteEntityType.KpiEvaluation:
            {
                var subjectId = await _db.KpiEvaluations.Where(x => x.Id == id)
                    .Select(x => (Guid?)x.SubjectUserId).FirstOrDefaultAsync(ct);
                return subjectId is Guid s && scope.Contains(s);
            }
            case ManagementNoteEntityType.Team:
            {
                var leaderId = await _db.Teams.Where(x => x.Id == id)
                    .Select(x => x.TeamLeaderId).FirstOrDefaultAsync(ct);
                if (leaderId is Guid l && scope.Contains(l)) return true;
                var memberIds = await _db.Users.Where(u => u.TeamId == id).Select(u => u.Id).ToListAsync(ct);
                return memberIds.Any(scope.Contains);
            }
            case ManagementNoteEntityType.Escalation:
            {
                var e = await _db.Escalations.Where(x => x.Id == id)
                    .Select(x => new { x.RaisedById, x.TargetUserId }).FirstOrDefaultAsync(ct);
                return e is not null && (e.RaisedById == uid || e.TargetUserId == uid);
            }
            // المخاطر والقرارات بيانات حوكمة مؤسسية — مقصورة على أصحاب صلاحية الحوكمة (فُحصت أعلاه).
            case ManagementNoteEntityType.Decision:
            case ManagementNoteEntityType.Risk:
            default:
                return false;
        }
    }

    private async Task<ManagementNoteDto> BuildAsync(Guid id, CancellationToken ct)
    {
        var n = await _db.ManagementNotes.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { n.AuthorId, n.ResolvedById ?? Guid.Empty }, ct);
        return Map(n, names);
    }

    private static ManagementNoteDto Map(ManagementNote n, Dictionary<Guid, string> names) =>
        new(n.Id, n.EntityType, n.EntityId, n.AuthorId, names.GetValueOrDefault(n.AuthorId),
            n.NoteType, n.Body, n.RequiresAction, n.Status, n.ResolvedById,
            n.ResolvedById is Guid r ? names.GetValueOrDefault(r) : null, n.ResolvedAtUtc, n.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}

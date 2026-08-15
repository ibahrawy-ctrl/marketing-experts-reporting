using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// قراءة مخاطر المشروع وقراراته وملاحظاته الإداريّة (CPW-R3 · W5 · §17-19).
///
/// <para>
/// **صفر تغيير على الحوكمة**: لا كيان جديد، ولا عمود، ولا حالة، ولا سير عمل، ولا صلاحيّة —
/// هذه الطبقة **قراءة فقط** فوق <c>Risk</c> و<c>Decision</c> و<c>ManagementNote</c> كما هي،
/// وكلّ الكتابة تبقى في مساراتها الأصليّة بلا مساس.
/// </para>
///
/// <para>
/// **الرؤية من بوّابة واحدة**: كلّ مسار يبدأ بـ<c>LoadVisibleProjectAsync</c>، فمن لا يرى المشروع
/// لا يقرأ حوكمته — ولا تُفتح بهذه المسارات نافذة رؤية جانبيّة على عناصر حوكمة خارج نطاقه.
/// </para>
///
/// <para>**عدد استعلامات ثابت لكلّ مسار**: استعلام للعناصر + استعلام واحد لأسماء الأشخاص (§15).</para>
/// </summary>
public class ProjectGovernanceReadService : IProjectGovernanceReadService
{
    private readonly AppDbContext _db;
    private readonly IProject360Authorization _auth;

    public ProjectGovernanceReadService(AppDbContext db, IProject360Authorization auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<Result<IReadOnlyList<ProjectRiskListItemDto>>> ListRisksAsync(
        Guid projectId, bool openOnly, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded)
            return Result<IReadOnlyList<ProjectRiskListItemDto>>.Failure(scope.Error!, scope.ErrorCode);

        var query = _db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId);
        if (openOnly) query = query.Where(r => r.Status != RiskStatus.Closed);

        var rows = await query
            .OrderByDescending(r => r.Severity).ThenByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.Id, r.Title, r.Description, r.Severity, r.Status, r.OwnerId,
                r.MitigationPlan, r.NextAction, r.ClosedAtUtc, r.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(rows.Select(r => r.OwnerId), ct);

        return Result<IReadOnlyList<ProjectRiskListItemDto>>.Success(rows
            .Select(r => new ProjectRiskListItemDto(
                r.Id, r.Title, r.Description, r.Severity, r.Status,
                r.OwnerId, names.GetValueOrDefault(r.OwnerId),
                r.MitigationPlan, r.NextAction, r.ClosedAtUtc, r.CreatedAtUtc))
            .ToList());
    }

    public async Task<Result<IReadOnlyList<ProjectDecisionListItemDto>>> ListDecisionsAsync(
        Guid projectId, bool pendingOnly, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded)
            return Result<IReadOnlyList<ProjectDecisionListItemDto>>.Failure(scope.Error!, scope.ErrorCode);

        var query = _db.Decisions.AsNoTracking().Where(d => d.ProjectId == projectId);
        if (pendingOnly) query = query.Where(d => d.Status == DecisionStatus.Proposed);

        var rows = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new
            {
                d.Id, d.Title, d.Description, d.Status, d.MadeById,
                d.NextAction, d.DecidedAtUtc, d.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(rows.Select(d => d.MadeById), ct);

        return Result<IReadOnlyList<ProjectDecisionListItemDto>>.Success(rows
            .Select(d => new ProjectDecisionListItemDto(
                d.Id, d.Title, d.Description, d.Status,
                d.MadeById, names.GetValueOrDefault(d.MadeById),
                d.NextAction, d.DecidedAtUtc, d.CreatedAtUtc))
            .ToList());
    }

    public async Task<Result<IReadOnlyList<ProjectNoteListItemDto>>> ListNotesAsync(
        Guid projectId, bool openOnly, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded)
            return Result<IReadOnlyList<ProjectNoteListItemDto>>.Failure(scope.Error!, scope.ErrorCode);

        // الملاحظة الإداريّة عامّة الارتباط (EntityType + EntityId) ⟹ الترشيح بالنوع **و**المعرّف
        // معًا إلزاميّ، وإلّا سُرِّبت ملاحظات كيان آخر يصادف حمله المعرّف نفسه.
        var query = _db.ManagementNotes.AsNoTracking()
            .Where(n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == projectId);
        if (openOnly) query = query.Where(n => n.Status == ManagementNoteStatus.Open);

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new
            {
                n.Id, n.NoteType, n.Body, n.RequiresAction, n.Status,
                n.AuthorId, n.ResolvedAtUtc, n.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(rows.Select(n => n.AuthorId), ct);

        return Result<IReadOnlyList<ProjectNoteListItemDto>>.Success(rows
            .Select(n => new ProjectNoteListItemDto(
                n.Id, n.NoteType, n.Body, n.RequiresAction, n.Status,
                n.AuthorId, names.GetValueOrDefault(n.AuthorId),
                n.ResolvedAtUtc, n.CreatedAtUtc))
            .ToList());
    }

    /// <summary>استعلام واحد لكلّ الأسماء — لا استعلام داخل حلقة مهما بلغ عدد الصفوف (§15).</summary>
    private async Task<Dictionary<Guid, string>> ResolveNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}

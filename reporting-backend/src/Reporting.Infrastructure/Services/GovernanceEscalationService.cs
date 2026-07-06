using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1): كيان مستقلّ لرفع/متابعة/إسناد/إغلاق تصعيدات فردية بخطّ زمنيّ.
/// لا يوسّع GovernanceItem ولا يمسّ سير اعتماد التقارير (Workflow/ApprovalStep/CurrentApproverId). الرؤية:
/// واسعة (Admin/CEO/GM/CeoSupport) = الكلّ + إسناد/إغلاق/إعادة فتح؛ Manager = نطاقه (ScopeResolver) معالجة/إسناد؛
/// TeamLeader = نطاق فريقه متابعة/تعليق (إغلاق فقط إن أُسنِد إليه)؛ HR = نطاق إدارته/ما يخصّه؛ Employee = إنشاء +
/// رؤية ما رفعه/استُهدف به/أُسنِد إليه فقط (بلا إسناد/إغلاق). القراءة غير المصرّح بها تُقنَّع 404 لا 403.
/// يستخدم ScopeResolver للقراءة فقط دون تغيير سلوكه.
/// </summary>
public class GovernanceEscalationService : IGovernanceEscalationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;
    private readonly IGovernanceDirectoryService _directory;
    private readonly IEmailNotificationService _emailNotifications;

    public GovernanceEscalationService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IAuditService audit, IGovernanceDirectoryService directory, IEmailNotificationService emailNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
        _directory = directory;
        _emailNotifications = emailNotifications;
    }

    private bool IsWideViewer => _currentUser.IsInAnyRole(Roles.GovernanceEscalationWideViewers);
    private bool IsHr => _currentUser.IsInRole(Roles.Hr);
    private bool IsManager => _currentUser.IsInRole(Roles.Manager);
    private bool IsTeamLeader => _currentUser.IsInRole(Roles.TeamLeader);

    private const string NotFoundMsg = "التصعيد غير موجود.";
    private const string NotFoundCode = "governance_escalation.not_found";

    // الحسابات الحسّاسة لا تُعرَض في دليل أهداف التصعيد ولا تُستهدَف من غير أصحاب الرؤية الواسعة.
    private static readonly string[] SensitiveAccountRoles = { Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport };

    private async Task<bool> IsSensitiveUserAsync(Guid userId, CancellationToken ct) =>
        await (from ur in _db.UserRoles
               join r in _db.Roles on ur.RoleId equals r.Id
               where ur.UserId == userId && SensitiveAccountRoles.Contains(r.Name!)
               select ur.UserId).AnyAsync(ct);

    public async Task<Result<IReadOnlyList<GovernanceEscalationListItemDto>>> ListAsync(GovernanceEscalationFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<GovernanceEscalationListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await BuildVisibilityAsync(uid, ct);

        var query = _db.GovernanceEscalations.AsNoTracking().AsQueryable();
        query = ApplyVisibility(query, uid, vis);

        if (filter.Status is GovernanceEscalationStatus st) query = query.Where(g => g.Status == st);
        if (filter.Type is EscalationType ty) query = query.Where(g => g.EscalationType == ty);
        if (filter.Severity is EscalationSeverity sev) query = query.Where(g => g.Severity == sev);
        if (filter.TargetType is EscalationTargetType tt) query = query.Where(g => g.TargetType == tt);
        if (filter.AssignedToUserId is Guid au) query = query.Where(g => g.AssignedToUserId == au);
        if (filter.RaisedByUserId is Guid rb) query = query.Where(g => g.RaisedByUserId == rb);
        if (filter.TargetUserId is Guid tu) query = query.Where(g => g.TargetUserId == tu);
        if (filter.TargetDepartmentId is Guid d) query = query.Where(g => g.TargetDepartmentId == d);
        if (filter.TargetTeamId is Guid t) query = query.Where(g => g.TargetTeamId == t);
        if (filter.Mine) query = query.Where(g => g.RaisedByUserId == uid);
        if (filter.AssignedToMe) query = query.Where(g => g.AssignedToUserId == uid);
        if (filter.OpenOnly)
            query = query.Where(g => g.Status != GovernanceEscalationStatus.Closed
                                     && g.Status != GovernanceEscalationStatus.Cancelled
                                     && g.Status != GovernanceEscalationStatus.Resolved);

        var rows = await query.OrderByDescending(g => g.CreatedAtUtc).ToListAsync(ct);
        var names = await ResolveNamesAsync(rows, ct);
        var items = rows.Select(g => MapListItem(g, names)).ToList();
        return Result<IReadOnlyList<GovernanceEscalationListItemDto>>.Success(items);
    }

    public async Task<Result<GovernanceEscalationDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceEscalations.AsNoTracking()
            .Include(g => g.Updates)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> CreateAsync(CreateGovernanceEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GovernanceEscalationDetailDto>.Failure("العنوان مطلوب.", "governance_escalation.title_required");

        var vis = await BuildVisibilityAsync(uid, ct);
        var validation = await ValidateAsync(request.TargetType, request.TargetUserId, request.TargetDepartmentId,
            request.TargetTeamId, request.RelatedSubmissionId, request.RelatedGovernanceItemId, uid, vis, ct);
        if (validation is not null)
            return Result<GovernanceEscalationDetailDto>.Failure(validation.Value.Message, validation.Value.Code);

        var item = new GovernanceEscalation
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            EscalationType = request.EscalationType,
            Severity = request.Severity,
            Status = GovernanceEscalationStatus.Open,
            RaisedByUserId = uid,
            TargetType = request.TargetType,
            TargetUserId = request.TargetUserId,
            TargetDepartmentId = request.TargetDepartmentId,
            TargetTeamId = request.TargetTeamId,
            RelatedSubmissionId = request.RelatedSubmissionId,
            RelatedGovernanceItemId = request.RelatedGovernanceItemId
        };
        item.Updates.Add(new GovernanceEscalationUpdate
        {
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Created,
            NewStatus = GovernanceEscalationStatus.Open
        });
        _db.GovernanceEscalations.Add(item);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.created", nameof(GovernanceEscalation), item.Id,
            JsonSerializer.Serialize(new { type = item.EscalationType.ToString(), severity = item.Severity.ToString(), targetType = item.TargetType.ToString() }), ct: ct);

        // إشعار بريد عند التصعيد الموجَّه لموظّف بعينه (TargetType=User) — لا يكسر العملية الأساسية.
        if (item.TargetType == EscalationTargetType.User && item.TargetUserId is not null)
        {
            try { await _emailNotifications.NotifyGovernanceEscalationCreatedAsync(item, ct); }
            catch { /* تُسجَّل داخل الخدمة. */ }
        }

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> UpdateAsync(Guid id, UpdateGovernanceEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GovernanceEscalationDetailDto>.Failure("العنوان مطلوب.", "governance_escalation.title_required");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanEdit(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure("لا تملك صلاحية تعديل هذا التصعيد.", "auth.forbidden");

        var validation = await ValidateAsync(request.TargetType, request.TargetUserId, request.TargetDepartmentId,
            request.TargetTeamId, request.RelatedSubmissionId, request.RelatedGovernanceItemId, uid, vis, ct);
        if (validation is not null)
            return Result<GovernanceEscalationDetailDto>.Failure(validation.Value.Message, validation.Value.Code);

        item.Title = request.Title.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.EscalationType = request.EscalationType;
        item.Severity = request.Severity;
        item.TargetType = request.TargetType;
        item.TargetUserId = request.TargetUserId;
        item.TargetDepartmentId = request.TargetDepartmentId;
        item.TargetTeamId = request.TargetTeamId;
        item.RelatedSubmissionId = request.RelatedSubmissionId;
        item.RelatedGovernanceItemId = request.RelatedGovernanceItemId;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Edited
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.updated", nameof(GovernanceEscalation), item.Id, ct: ct);

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceEscalationStatusRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanChangeStatus(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure("لا تملك صلاحية تغيير حالة هذا التصعيد.", "auth.forbidden");

        var oldStatus = item.Status;
        if (oldStatus == request.Status)
            return Result<GovernanceEscalationDetailDto>.Failure("الحالة غير متغيّرة.", "governance_escalation.status_unchanged.conflict");

        ApplyStatus(item, request.Status, uid, request.Resolution);
        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.StatusChanged,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = request.Status
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.status_changed", nameof(GovernanceEscalation), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString(), to = request.Status.ToString() }), ct: ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): إغلاق/حلّ عبر تغيير الحالة ⇒ الرافع + المُسنَد إليه (دون مَن أغلق).
        // الدِدوب عبر مفتاح الترابط (بلا حالة في المفتاح) يمنع تكرار الإشعار عند التداخل مع CloseAsync.
        if (request.Status is GovernanceEscalationStatus.Closed or GovernanceEscalationStatus.Resolved)
        {
            try { await _emailNotifications.NotifyGovernanceEscalationClosedAsync(item, ct); }
            catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }
        }

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> AssignAsync(Guid id, AssignGovernanceEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanAssign(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure("لا تملك صلاحية إسناد هذا التصعيد.", "auth.forbidden");
        if (!await _db.Users.AnyAsync(u => u.Id == request.AssignedToUserId, ct))
            return Result<GovernanceEscalationDetailDto>.Failure("المستخدم المُسنَد إليه غير موجود.", "governance_escalation.assignee_not_found");

        item.AssignedToUserId = request.AssignedToUserId;
        if (item.Status is GovernanceEscalationStatus.Open or GovernanceEscalationStatus.UnderReview)
            item.Status = GovernanceEscalationStatus.Assigned;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Assigned,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.assigned", nameof(GovernanceEscalation), item.Id, ct: ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): إسناد التصعيد للمُسنَد إليه للمراجعة.
        try { await _emailNotifications.NotifyGovernanceEscalationAssignedAsync(item, ct); }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> AddCommentAsync(Guid id, AddGovernanceEscalationCommentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<GovernanceEscalationDetailDto>.Failure("نص التعليق مطلوب.", "governance_escalation.comment_required");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Comment,
            Body = request.Body.Trim()
        });
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.comment_added", nameof(GovernanceEscalation), item.Id, ct: ct);

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> ReopenAsync(Guid id, ReopenGovernanceEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanReopen(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure("لا تملك صلاحية إعادة فتح هذا التصعيد.", "auth.forbidden");
        if (item.Status is not (GovernanceEscalationStatus.Resolved or GovernanceEscalationStatus.Closed or GovernanceEscalationStatus.Cancelled))
            return Result<GovernanceEscalationDetailDto>.Failure("لا يمكن إعادة فتح تصعيد غير مغلق.", "governance_escalation.not_closed.conflict");

        var oldStatus = item.Status;
        item.Status = GovernanceEscalationStatus.Reopened;
        item.ClosedAtUtc = null;
        item.ClosedByUserId = null;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Reopened,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = GovernanceEscalationStatus.Reopened
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.reopened", nameof(GovernanceEscalation), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString() }), ct: ct);

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceEscalationDetailDto>> CloseAsync(Guid id, CloseGovernanceEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceEscalationDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceEscalations.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanClose(item, uid, vis))
            return Result<GovernanceEscalationDetailDto>.Failure("لا تملك صلاحية إغلاق هذا التصعيد.", "auth.forbidden");
        if (item.Status is GovernanceEscalationStatus.Closed or GovernanceEscalationStatus.Cancelled)
            return Result<GovernanceEscalationDetailDto>.Failure("التصعيد مغلق بالفعل.", "governance_escalation.already_closed.conflict");

        var oldStatus = item.Status;
        item.Status = GovernanceEscalationStatus.Closed;
        item.ClosedAtUtc = DateTime.UtcNow;
        item.ClosedByUserId = uid;
        if (!string.IsNullOrWhiteSpace(request.Resolution))
            item.Resolution = request.Resolution.Trim();
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceEscalationUpdates.Add(new GovernanceEscalationUpdate
        {
            EscalationId = item.Id,
            AuthorId = uid,
            UpdateType = EscalationUpdateType.Closed,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = GovernanceEscalationStatus.Closed
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_escalation.closed", nameof(GovernanceEscalation), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString() }), ct: ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): إغلاق التصعيد ⇒ الرافع + المُسنَد إليه (دون مَن أغلق).
        try { await _emailNotifications.NotifyGovernanceEscalationClosedAsync(item, ct); }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

        return Result<GovernanceEscalationDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<EscalationTargetDirectoryDto>> GetTargetDirectoryAsync(CancellationToken ct = default)
    {
        // المصدر الموحّد لدليل الحوكمة (GOV-DIRECTORY-SCOPE-FIX-R1): غرض هدف التصعيد المتقاطع.
        var dir = await _directory.GetDirectoryAsync(GovernanceDirectoryPurpose.EscalationTarget, ct);
        if (!dir.Succeeded)
            return Result<EscalationTargetDirectoryDto>.Failure(dir.Error!, dir.ErrorCode!);

        var userDtos = dir.Value!.Users
            .Select(u => new EscalationTargetUserDto(u.Id, u.FullName, u.DepartmentId, u.TeamId))
            .ToList();
        var deptDtos = dir.Value!.Departments
            .Select(d => new EscalationTargetDepartmentDto(d.Id, d.Name))
            .ToList();
        var teamDtos = dir.Value!.Teams
            .Select(t => new EscalationTargetTeamDto(t.Id, t.Name, t.DepartmentId))
            .ToList();

        return Result<EscalationTargetDirectoryDto>.Success(new EscalationTargetDirectoryDto(userDtos, deptDtos, teamDtos));
    }

    private static void ApplyStatus(GovernanceEscalation item, GovernanceEscalationStatus status, Guid uid, string? resolution)
    {
        item.Status = status;
        item.UpdatedAtUtc = DateTime.UtcNow;
        if (status is GovernanceEscalationStatus.Resolved or GovernanceEscalationStatus.Closed or GovernanceEscalationStatus.Cancelled)
        {
            item.ClosedAtUtc = DateTime.UtcNow;
            item.ClosedByUserId = uid;
            if (!string.IsNullOrWhiteSpace(resolution))
                item.Resolution = resolution.Trim();
        }
        else
        {
            item.ClosedAtUtc = null;
            item.ClosedByUserId = null;
        }
    }

    // ===== الرؤية والصلاحيات =====

    private readonly record struct Visibility(
        bool Wide, bool Hr, Guid? HrDepartmentId, HashSet<Guid> ScopeIds, bool ScopeSeesAll,
        HashSet<Guid> HrDeptUserIds, bool Manager, bool TeamLeader, Guid? OwnDepartmentId, Guid? OwnTeamId);

    private async Task<Visibility> BuildVisibilityAsync(Guid uid, CancellationToken ct)
    {
        var (ownDept, ownTeam) = await _db.Users.Where(u => u.Id == uid)
            .Select(u => new ValueTuple<Guid?, Guid?>(u.DepartmentId, u.TeamId))
            .FirstOrDefaultAsync(ct);

        if (IsWideViewer)
            return new Visibility(true, false, null, new HashSet<Guid>(), true, new HashSet<Guid>(), false, false, ownDept, ownTeam);

        if (IsHr)
        {
            var hrDeptUsers = ownDept is Guid hd
                ? (await _db.Users.Where(u => u.DepartmentId == hd).Select(u => u.Id).ToListAsync(ct)).ToHashSet()
                : new HashSet<Guid>();
            return new Visibility(false, true, ownDept, new HashSet<Guid>(), false, hrDeptUsers, false, false, ownDept, ownTeam);
        }

        // Manager / TeamLeader / Employee: نطاق الرؤية من ScopeResolver (قراءة فقط).
        var scope = await _scope.ResolveAsync(ct);
        return new Visibility(false, false, null, scope.UserIds.ToHashSet(), scope.SeesAll, new HashSet<Guid>(), IsManager, IsTeamLeader, ownDept, ownTeam);
    }

    private static IQueryable<GovernanceEscalation> ApplyVisibility(IQueryable<GovernanceEscalation> query, Guid uid, Visibility vis)
    {
        if (vis.Wide) return query;

        if (vis.Hr)
        {
            var hrDept = vis.HrDepartmentId;
            return query.Where(g =>
                g.RaisedByUserId == uid
                || g.TargetUserId == uid
                || g.AssignedToUserId == uid
                || (hrDept != null && g.TargetDepartmentId == hrDept)
                || (g.TargetUserId != null && vis.HrDeptUserIds.Contains(g.TargetUserId.Value)));
        }

        if (vis.ScopeSeesAll) return query;
        var ids = vis.ScopeIds;
        return query.Where(g =>
            g.RaisedByUserId == uid
            || g.TargetUserId == uid
            || g.AssignedToUserId == uid
            || ids.Contains(g.RaisedByUserId)
            || (g.TargetUserId != null && ids.Contains(g.TargetUserId.Value))
            || (g.AssignedToUserId != null && ids.Contains(g.AssignedToUserId.Value)));
    }

    private bool CanView(GovernanceEscalation g, Guid uid, Visibility vis)
    {
        if (vis.Wide) return true;
        if (vis.Hr)
            return g.RaisedByUserId == uid
                || g.TargetUserId == uid
                || g.AssignedToUserId == uid
                || (vis.HrDepartmentId is Guid hd && g.TargetDepartmentId == hd)
                || (g.TargetUserId is Guid tu && vis.HrDeptUserIds.Contains(tu));
        if (vis.ScopeSeesAll) return true;
        return g.RaisedByUserId == uid
            || g.TargetUserId == uid
            || g.AssignedToUserId == uid
            || vis.ScopeIds.Contains(g.RaisedByUserId)
            || (g.TargetUserId is Guid t && vis.ScopeIds.Contains(t))
            || (g.AssignedToUserId is Guid a && vis.ScopeIds.Contains(a));
    }

    /// <summary>هل المدير ضمن نطاقه على هذا التصعيد (يشمل الرافع/الهدف/المُسنَد إليه أو SeesAll)؟</summary>
    private static bool ManagerInScope(GovernanceEscalation g, Visibility vis) =>
        vis.Manager && (vis.ScopeSeesAll
            || vis.ScopeIds.Contains(g.RaisedByUserId)
            || (g.TargetUserId is Guid t && vis.ScopeIds.Contains(t))
            || (g.AssignedToUserId is Guid a && vis.ScopeIds.Contains(a)));

    private static bool IsOpenForEdit(GovernanceEscalationStatus s) =>
        s is not (GovernanceEscalationStatus.Closed or GovernanceEscalationStatus.Cancelled);

    /// <summary>تعديل المحتوى: رؤية واسعة، أو مدير ضمن نطاقه، أو الرافع (ما دام غير مغلق).</summary>
    private static bool CanEdit(GovernanceEscalation g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis) || (g.RaisedByUserId == uid && IsOpenForEdit(g.Status));

    /// <summary>تغيير الحالة: رؤية واسعة، أو مدير ضمن نطاقه، أو المُسنَد إليه.</summary>
    private static bool CanChangeStatus(GovernanceEscalation g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis) || g.AssignedToUserId == uid;

    /// <summary>الإسناد: رؤية واسعة أو مدير ضمن نطاقه (لا الرافع ولا قائد الفريق ولا الموظف).</summary>
    private static bool CanAssign(GovernanceEscalation g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis);

    /// <summary>الإغلاق: رؤية واسعة، أو مدير ضمن نطاقه، أو قائد فريق مُسنَد إليه (إغلاق مسموح صراحةً فقط).</summary>
    private static bool CanClose(GovernanceEscalation g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis) || (vis.TeamLeader && g.AssignedToUserId == uid);

    /// <summary>إعادة الفتح: رؤية واسعة أو مدير ضمن نطاقه.</summary>
    private static bool CanReopen(GovernanceEscalation g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis);

    // ===== التحقق من المراجع والنطاق (منع IDOR + قصر الهدف على النطاق) =====

    private async Task<(string Message, string Code)?> ValidateAsync(
        EscalationTargetType targetType, Guid? targetUserId, Guid? targetDepartmentId, Guid? targetTeamId,
        Guid? relatedSubmissionId, Guid? relatedGovernanceItemId, Guid uid, Visibility vis, CancellationToken ct)
    {
        // الهدف إلزاميّ حسب نوعه.
        if (targetType == EscalationTargetType.User && targetUserId is null)
            return ("الموظّف الهدف مطلوب.", "governance_escalation.target_user_required");
        if (targetType == EscalationTargetType.Department && targetDepartmentId is null)
            return ("الإدارة الهدف مطلوبة.", "governance_escalation.target_department_required");
        if (targetType == EscalationTargetType.Team && targetTeamId is null)
            return ("الفريق الهدف مطلوب.", "governance_escalation.target_team_required");

        // وجود المراجع.
        if (targetUserId is Guid tu && !await _db.Users.AnyAsync(u => u.Id == tu, ct))
            return ("الموظّف الهدف غير موجود.", "governance_escalation.target_user_not_found");
        if (targetDepartmentId is Guid td && !await _db.Departments.AnyAsync(x => x.Id == td, ct))
            return ("الإدارة الهدف غير موجودة.", "governance_escalation.target_department_not_found");
        if (targetTeamId is Guid tt && !await _db.Teams.AnyAsync(x => x.Id == tt, ct))
            return ("الفريق الهدف غير موجود.", "governance_escalation.target_team_not_found");
        if (relatedGovernanceItemId is Guid gi && !await _db.GovernanceItems.AnyAsync(x => x.Id == gi, ct))
            return ("بند الحوكمة المرتبط غير موجود.", "governance_escalation.governance_item_not_found");

        // (UAT FIX) أُتيح للجميع توجيه التصعيد لموظّف/إدارة/فريق خارج نطاقهم (الرفع المتقاطع) — دون أن يوسّع ذلك
        // أيّ صلاحية قراءة (الرؤية تبقى محصورة بـ ApplyVisibility/CanView، فلا يرى المُصعِّد بقية تصعيدات تلك الجهة).
        // القيد الوحيد الباقي لغير أصحاب الرؤية الواسعة: لا يجوز توجيه التصعيد لحساب حسّاس (Admin/CEO/GM/CeoSupport)
        // كي لا يُكشَف هؤلاء عبر دليل الأهداف ولا يُستهدَفوا بمعرّف مُلفَّق (دفاع في العمق).
        if (!vis.Wide && targetUserId is Guid su && su != uid && await IsSensitiveUserAsync(su, ct))
            return ("لا يمكن توجيه التصعيد لهذا الحساب.", "governance_escalation.target_not_allowed");

        // التقرير المرتبط: موجود + قابل للرؤية (واسع/الرافع نفسه مُرسِله/ضمن النطاق).
        if (relatedSubmissionId is Guid rs)
        {
            var submitterId = await _db.ReportSubmissions.Where(x => x.Id == rs).Select(x => (Guid?)x.SubmitterId).FirstOrDefaultAsync(ct);
            if (submitterId is null)
                return ("التقرير المرتبط غير موجود.", "governance_escalation.submission_not_found");
            if (!vis.Wide && submitterId != uid
                && !vis.ScopeIds.Contains(submitterId.Value)
                && !(vis.Hr && vis.HrDeptUserIds.Contains(submitterId.Value)))
                return ("لا يمكنك ربط تقرير خارج نطاقك.", "governance_escalation.submission_out_of_scope");
        }

        return null;
    }

    // ===== البناء والأسماء =====

    private async Task<GovernanceEscalationDetailDto> BuildDetailAsync(GovernanceEscalation item, Guid uid, CancellationToken ct)
    {
        var fresh = await _db.GovernanceEscalations.AsNoTracking()
            .Include(g => g.Updates)
            .FirstAsync(g => g.Id == item.Id, ct);

        var names = await ResolveNamesAsync(new[] { fresh }, ct);
        var authorNames = await UserNamesAsync(fresh.Updates.Select(u => (Guid?)u.AuthorId), ct);
        var closedByName = fresh.ClosedByUserId is Guid c
            ? (await UserNamesAsync(new[] { (Guid?)c }, ct)).GetValueOrDefault(c)
            : null;

        var listItem = MapListItem(fresh, names);
        var timeline = fresh.Updates
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new GovernanceEscalationUpdateDto(
                u.Id, u.AuthorId, authorNames.GetValueOrDefault(u.AuthorId),
                u.UpdateType, u.Body, u.OldStatus, u.NewStatus, u.CreatedAtUtc))
            .ToList();

        var vis = await BuildVisibilityAsync(uid, ct);
        return new GovernanceEscalationDetailDto(
            listItem, fresh.Description, fresh.Resolution, fresh.ClosedAtUtc, fresh.ClosedByUserId, closedByName,
            CanEdit(fresh, uid, vis), CanChangeStatus(fresh, uid, vis), CanAssign(fresh, uid, vis),
            CanClose(fresh, uid, vis), CanReopen(fresh, uid, vis), CanView(fresh, uid, vis), timeline);
    }

    private static GovernanceEscalationListItemDto MapListItem(GovernanceEscalation g, ResolvedNames names) =>
        new(
            g.Id,
            g.Title,
            g.EscalationType,
            g.Severity,
            g.Status,
            g.RaisedByUserId,
            names.Users.GetValueOrDefault(g.RaisedByUserId),
            g.TargetType,
            g.TargetUserId,
            g.TargetUserId is Guid tu ? names.Users.GetValueOrDefault(tu) : null,
            g.TargetDepartmentId,
            g.TargetDepartmentId is Guid d ? names.Departments.GetValueOrDefault(d) : null,
            g.TargetTeamId,
            g.TargetTeamId is Guid t ? names.Teams.GetValueOrDefault(t) : null,
            g.RelatedSubmissionId,
            g.RelatedGovernanceItemId,
            g.AssignedToUserId,
            g.AssignedToUserId is Guid a ? names.Users.GetValueOrDefault(a) : null,
            g.CreatedAtUtc,
            g.UpdatedAtUtc);

    private readonly record struct ResolvedNames(Dictionary<Guid, string> Users, Dictionary<Guid, string> Departments, Dictionary<Guid, string> Teams);

    private async Task<ResolvedNames> ResolveNamesAsync(IEnumerable<GovernanceEscalation> items, CancellationToken ct)
    {
        var list = items as ICollection<GovernanceEscalation> ?? items.ToList();
        var userIds = list.SelectMany(g => new[] { (Guid?)g.RaisedByUserId, g.TargetUserId, g.AssignedToUserId, g.ClosedByUserId });
        var deptIds = list.Select(g => g.TargetDepartmentId);
        var teamIds = list.Select(g => g.TargetTeamId);
        return new ResolvedNames(
            await UserNamesAsync(userIds, ct),
            await DepartmentNamesAsync(deptIds, ct),
            await TeamNamesAsync(teamIds, ct));
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is Guid g && g != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<Dictionary<Guid, string>> DepartmentNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Departments.Where(d => distinct.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
    }

    private async Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Teams.Where(t => distinct.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
    }
}

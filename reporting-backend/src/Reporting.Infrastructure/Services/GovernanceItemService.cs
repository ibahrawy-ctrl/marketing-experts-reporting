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
/// ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1): تسجيل ومتابعة بنود الحوكمة (ملاحظة/خطر/قرار/توصية/متابعة/التزام/أداء/مشكلة تشغيلية)
/// مع خط زمني (تعليقات + تغييرات حالة + متابعات). الرؤية محكومة بالأدوار:
/// رؤية واسعة (Admin/CEO/GeneralManager/CeoSupport) = كل البنود؛ Manager/TeamLeader = ضمن نطاقهم (ScopeResolver) أو ما أنشؤوه/أُسنِد إليهم؛
/// HR = ما أنشأه/أُسنِد إليه/يتعلّق به أو بإدارة HR فقط. الموظف لا وصول (محجوب بالسياسة). يستخدم ScopeResolver للقراءة فقط دون تغيير سلوكه.
/// </summary>
public class GovernanceItemService : IGovernanceItemService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;
    private readonly IGovernanceDirectoryService _directory;
    private readonly IEmailNotificationService _emailNotifications;

    public GovernanceItemService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IAuditService audit, IGovernanceDirectoryService directory, IEmailNotificationService emailNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
        _directory = directory;
        _emailNotifications = emailNotifications;
    }

    private bool IsWideViewer => _currentUser.IsInAnyRole(Roles.GovernanceWorkspaceWideViewers);
    private bool IsHr => _currentUser.IsInRole(Roles.Hr);

    /// <summary>
    /// دليل ورشة الحوكمة الموحّد (GOV-DIRECTORY-SCOPE-FIX-R1): مصدر قوائم المستخدمين/الإدارات/الفِرق لاختيار
    /// المُسنَد إليه/المتعلَّق ضمن نطاق الملكية. يفوّض للمصدر الموحّد بغرض Workspace (لا HR Directory مباشرة).
    /// </summary>
    public Task<Result<GovernanceDirectoryDto>> GetDirectoryAsync(CancellationToken ct = default) =>
        _directory.GetDirectoryAsync(GovernanceDirectoryPurpose.Workspace, ct);

    public async Task<Result<IReadOnlyList<GovernanceItemListItemDto>>> ListAsync(GovernanceItemFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<GovernanceItemListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await BuildVisibilityAsync(uid, ct);

        var query = _db.GovernanceItems.AsNoTracking().AsQueryable();
        query = ApplyVisibility(query, uid, vis);

        if (filter.Status is GovernanceItemStatus st) query = query.Where(g => g.Status == st);
        if (filter.Category is GovernanceCategory cat) query = query.Where(g => g.Category == cat);
        if (filter.Severity is GovernanceSeverity sev) query = query.Where(g => g.Severity == sev);
        if (filter.AssignedToUserId is Guid au) query = query.Where(g => g.AssignedToUserId == au);
        if (filter.DepartmentId is Guid d) query = query.Where(g => g.DepartmentId == d);
        if (filter.TeamId is Guid t) query = query.Where(g => g.TeamId == t);
        if (filter.OpenOnly)
            query = query.Where(g => g.Status != GovernanceItemStatus.Closed
                                     && g.Status != GovernanceItemStatus.Cancelled
                                     && g.Status != GovernanceItemStatus.Resolved);

        var rows = await query
            .OrderByDescending(g => g.CreatedAtUtc)
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(rows, ct);
        var items = rows.Select(g => MapListItem(g, names)).ToList();
        return Result<IReadOnlyList<GovernanceItemListItemDto>>.Success(items);
    }

    public async Task<Result<GovernanceItemDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceItems.AsNoTracking()
            .Include(g => g.Updates)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        return Result<GovernanceItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceItemDetailDto>> CreateAsync(CreateGovernanceItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GovernanceItemDetailDto>.Failure("العنوان مطلوب.", "governance_item.title_required");

        var validation = await ValidateReferencesAsync(
            request.AssignedToUserId, request.DepartmentId, request.TeamId,
            request.RelatedSubmissionId, request.RelatedUserId, ct);
        if (validation is not null)
            return Result<GovernanceItemDetailDto>.Failure(validation.Value.Message, validation.Value.Code);

        var scopeValidation = await ValidateScopeAsync(
            request.ApplicationScope, request.DepartmentId, request.TeamId,
            request.RelatedUserId, request.RelatedSubmissionId, ct);
        if (scopeValidation is not null)
            return Result<GovernanceItemDetailDto>.Failure(scopeValidation.Value.Message, scopeValidation.Value.Code);

        var item = new GovernanceItem
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = request.Category,
            Severity = request.Severity,
            ApplicationScope = request.ApplicationScope,
            Status = GovernanceItemStatus.Open,
            CreatedById = uid,
            AssignedToUserId = request.AssignedToUserId,
            DepartmentId = request.DepartmentId,
            TeamId = request.TeamId,
            RelatedSubmissionId = request.RelatedSubmissionId,
            RelatedUserId = request.RelatedUserId,
            DueDate = request.DueDate
        };
        item.Updates.Add(new GovernanceItemUpdate
        {
            AuthorId = uid,
            UpdateType = GovernanceItemUpdateType.Created,
            NewStatus = GovernanceItemStatus.Open
        });
        _db.GovernanceItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_item.created", nameof(GovernanceItem), item.Id,
            JsonSerializer.Serialize(new { item.Title, category = item.Category.ToString(), severity = item.Severity.ToString() }), ct: ct);

        // إشعار بريد (EMAIL-NOTIFICATIONS-R1) بعد نجاح الإنشاء — لا يكسر العملية الأساسية عند الفشل.
        if (item.AssignedToUserId is not null)
        {
            try { await _emailNotifications.NotifyGovernanceItemAssignedAsync(item, ct); }
            catch { /* تُسجَّل داخل الخدمة؛ لا تكسر إنشاء البند. */ }
        }

        return Result<GovernanceItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceItemDetailDto>> UpdateAsync(Guid id, UpdateGovernanceItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GovernanceItemDetailDto>.Failure("العنوان مطلوب.", "governance_item.title_required");

        var item = await _db.GovernanceItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");
        if (!CanEdit(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("لا تملك صلاحية تعديل هذا البند.", "auth.forbidden");

        var validation = await ValidateReferencesAsync(
            request.AssignedToUserId, request.DepartmentId, request.TeamId,
            request.RelatedSubmissionId, request.RelatedUserId, ct);
        if (validation is not null)
            return Result<GovernanceItemDetailDto>.Failure(validation.Value.Message, validation.Value.Code);

        var scopeValidation = await ValidateScopeAsync(
            request.ApplicationScope, request.DepartmentId, request.TeamId,
            request.RelatedUserId, request.RelatedSubmissionId, ct);
        if (scopeValidation is not null)
            return Result<GovernanceItemDetailDto>.Failure(scopeValidation.Value.Message, scopeValidation.Value.Code);

        item.Title = request.Title.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.Category = request.Category;
        item.Severity = request.Severity;
        item.ApplicationScope = request.ApplicationScope;
        item.AssignedToUserId = request.AssignedToUserId;
        item.DepartmentId = request.DepartmentId;
        item.TeamId = request.TeamId;
        item.RelatedSubmissionId = request.RelatedSubmissionId;
        item.RelatedUserId = request.RelatedUserId;
        item.DueDate = request.DueDate;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceItemUpdates.Add(new GovernanceItemUpdate
        {
            GovernanceItemId = item.Id,
            AuthorId = uid,
            UpdateType = GovernanceItemUpdateType.Edited
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_item.updated", nameof(GovernanceItem), item.Id,
            JsonSerializer.Serialize(new { item.Title }), ct: ct);

        // إشعار بريد بتحديث البند للمُسنَد إليه/المنشئ — لا يكسر العملية الأساسية.
        try { await _emailNotifications.NotifyGovernanceItemUpdatedAsync(item, $"edited:{item.UpdatedAtUtc:O}", ct); }
        catch { /* تُسجَّل داخل الخدمة. */ }

        return Result<GovernanceItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceItemDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceItemStatusRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");
        if (!CanEdit(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("لا تملك صلاحية تغيير حالة هذا البند.", "auth.forbidden");

        var oldStatus = item.Status;
        if (oldStatus == request.Status)
            return Result<GovernanceItemDetailDto>.Failure("الحالة غير متغيّرة.", "governance_item.status_unchanged.conflict");

        item.Status = request.Status;
        item.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Status is GovernanceItemStatus.Resolved or GovernanceItemStatus.Closed)
        {
            item.ClosedAtUtc = DateTime.UtcNow;
            item.ClosedById = uid;
            if (!string.IsNullOrWhiteSpace(request.ResolutionSummary))
                item.ResolutionSummary = request.ResolutionSummary.Trim();
        }
        else
        {
            item.ClosedAtUtc = null;
            item.ClosedById = null;
        }
        _db.GovernanceItemUpdates.Add(new GovernanceItemUpdate
        {
            GovernanceItemId = item.Id,
            AuthorId = uid,
            UpdateType = GovernanceItemUpdateType.StatusChanged,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = request.Status
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_item.status_changed", nameof(GovernanceItem), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString(), to = request.Status.ToString() }), ct: ct);

        // إشعار بريد بتغيير حالة البند للمُسنَد إليه/المنشئ — لا يكسر العملية الأساسية.
        try { await _emailNotifications.NotifyGovernanceItemUpdatedAsync(item, $"status:{oldStatus}->{request.Status}", ct); }
        catch { /* تُسجَّل داخل الخدمة. */ }

        return Result<GovernanceItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceItemDetailDto>> AddCommentAsync(Guid id, AddGovernanceItemCommentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<GovernanceItemDetailDto>.Failure("نص التعليق مطلوب.", "governance_item.comment_required");

        var item = await _db.GovernanceItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceItemDetailDto>.Failure("بند الحوكمة غير موجود.", "governance_item.not_found");

        _db.GovernanceItemUpdates.Add(new GovernanceItemUpdate
        {
            GovernanceItemId = item.Id,
            AuthorId = uid,
            UpdateType = request.IsFollowUp ? GovernanceItemUpdateType.FollowUp : GovernanceItemUpdateType.Comment,
            Body = request.Body.Trim()
        });
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_item.comment_added", nameof(GovernanceItem), item.Id,
            JsonSerializer.Serialize(new { isFollowUp = request.IsFollowUp }), ct: ct);

        return Result<GovernanceItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    // ===== الرؤية والصلاحيات =====

    private readonly record struct Visibility(bool Wide, bool Hr, Guid? HrDepartmentId, HashSet<Guid> ScopeIds, bool ScopeSeesAll, HashSet<Guid> HrDeptUserIds);

    private async Task<Visibility> BuildVisibilityAsync(Guid uid, CancellationToken ct)
    {
        if (IsWideViewer)
            return new Visibility(true, false, null, new HashSet<Guid>(), true, new HashSet<Guid>());

        if (IsHr)
        {
            var hrDeptId = await _db.Users.Where(u => u.Id == uid).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
            var hrDeptUsers = hrDeptId is Guid hd
                ? (await _db.Users.Where(u => u.DepartmentId == hd).Select(u => u.Id).ToListAsync(ct)).ToHashSet()
                : new HashSet<Guid>();
            return new Visibility(false, true, hrDeptId, new HashSet<Guid>(), false, hrDeptUsers);
        }

        // Manager / TeamLeader: نطاق الرؤية من ScopeResolver (قراءة فقط).
        var scope = await _scope.ResolveAsync(ct);
        return new Visibility(false, false, null, scope.UserIds.ToHashSet(), scope.SeesAll, new HashSet<Guid>());
    }

    private static IQueryable<GovernanceItem> ApplyVisibility(IQueryable<GovernanceItem> query, Guid uid, Visibility vis)
    {
        if (vis.Wide) return query;

        if (vis.Hr)
        {
            var hrDept = vis.HrDepartmentId;
            return query.Where(g =>
                g.CreatedById == uid
                || g.AssignedToUserId == uid
                || g.RelatedUserId == uid
                || (hrDept != null && g.DepartmentId == hrDept)
                || (g.RelatedUserId != null && vis.HrDeptUserIds.Contains(g.RelatedUserId.Value)));
        }

        if (vis.ScopeSeesAll) return query;
        var ids = vis.ScopeIds;
        return query.Where(g =>
            g.CreatedById == uid
            || g.AssignedToUserId == uid
            || ids.Contains(g.CreatedById)
            || (g.AssignedToUserId != null && ids.Contains(g.AssignedToUserId.Value))
            || (g.RelatedUserId != null && ids.Contains(g.RelatedUserId.Value)));
    }

    private bool CanView(GovernanceItem g, Guid uid, Visibility vis)
    {
        if (vis.Wide) return true;
        if (vis.Hr)
            return g.CreatedById == uid
                || g.AssignedToUserId == uid
                || g.RelatedUserId == uid
                || (vis.HrDepartmentId is Guid hd && g.DepartmentId == hd)
                || (g.RelatedUserId is Guid ru && vis.HrDeptUserIds.Contains(ru));
        if (vis.ScopeSeesAll) return true;
        return g.CreatedById == uid
            || g.AssignedToUserId == uid
            || vis.ScopeIds.Contains(g.CreatedById)
            || (g.AssignedToUserId is Guid a && vis.ScopeIds.Contains(a))
            || (g.RelatedUserId is Guid r && vis.ScopeIds.Contains(r));
    }

    /// <summary>صلاحية التعديل/تغيير الحالة: رؤية واسعة، أو منشئ البند، أو المُسنَد إليه.</summary>
    private bool CanEdit(GovernanceItem g, Guid uid, Visibility vis) =>
        vis.Wide || g.CreatedById == uid || g.AssignedToUserId == uid;

    // ===== التحقق من المراجع (منع IDOR) =====

    private async Task<(string Message, string Code)?> ValidateReferencesAsync(
        Guid? assignedToUserId, Guid? departmentId, Guid? teamId,
        Guid? relatedSubmissionId, Guid? relatedUserId, CancellationToken ct)
    {
        if (assignedToUserId is Guid au && !await _db.Users.AnyAsync(u => u.Id == au, ct))
            return ("المستخدم المُسنَد إليه غير موجود.", "governance_item.assignee_not_found");
        if (relatedUserId is Guid ru && !await _db.Users.AnyAsync(u => u.Id == ru, ct))
            return ("المستخدم المرتبط غير موجود.", "governance_item.related_user_not_found");
        if (departmentId is Guid d && !await _db.Departments.AnyAsync(x => x.Id == d, ct))
            return ("الإدارة غير موجودة.", "governance_item.department_not_found");
        if (teamId is Guid t && !await _db.Teams.AnyAsync(x => x.Id == t, ct))
            return ("الفريق غير موجود.", "governance_item.team_not_found");
        if (relatedSubmissionId is Guid rs && !await _db.ReportSubmissions.AnyAsync(x => x.Id == rs, ct))
            return ("التقرير المرتبط غير موجود.", "governance_item.submission_not_found");
        return null;
    }

    // ===== التحقق من نطاق التطبيق (GOV-APPLICATION-SCOPE-R1) =====

    /// <summary>
    /// يفرض قواعد «نطاق التطبيق»: Company لأصحاب الرؤية الواسعة فقط؛ كل نطاق يلزمه مرجعه الواحد ويرفض المراجع الأخرى؛
    /// ولغير أصحاب الرؤية الواسعة يجب أن يقع الهدف (إدارة/فريق/موظّف) ضمن الدليل الموحّد (نطاق الملكية).
    /// </summary>
    private async Task<(string Message, string Code)?> ValidateScopeAsync(
        GovernanceApplicationScope scope, Guid? departmentId, Guid? teamId,
        Guid? relatedUserId, Guid? relatedSubmissionId, CancellationToken ct)
    {
        if (scope == GovernanceApplicationScope.Company && !IsWideViewer)
            return ("نطاق «كل الشركة» متاح لأصحاب الرؤية الواسعة فقط.", "auth.forbidden");

        switch (scope)
        {
            case GovernanceApplicationScope.Company:
                if (departmentId is not null || teamId is not null || relatedUserId is not null || relatedSubmissionId is not null)
                    return ("نطاق «كل الشركة» لا يقبل تحديد إدارة أو فريق أو موظّف أو تقرير.", "governance_item.scope_mismatch");
                break;
            case GovernanceApplicationScope.Department:
                if (departmentId is null)
                    return ("يجب تحديد الإدارة عند اختيار نطاق «إدارة محددة».", "governance_item.scope_department_required");
                if (teamId is not null || relatedUserId is not null || relatedSubmissionId is not null)
                    return ("نطاق «إدارة محددة» يقبل تحديد الإدارة فقط.", "governance_item.scope_mismatch");
                break;
            case GovernanceApplicationScope.Team:
                if (teamId is null)
                    return ("يجب تحديد الفريق عند اختيار نطاق «فريق محدد».", "governance_item.scope_team_required");
                if (departmentId is not null || relatedUserId is not null || relatedSubmissionId is not null)
                    return ("نطاق «فريق محدد» يقبل تحديد الفريق فقط.", "governance_item.scope_mismatch");
                break;
            case GovernanceApplicationScope.User:
                if (relatedUserId is null)
                    return ("يجب تحديد الموظّف عند اختيار نطاق «موظّف محدد».", "governance_item.scope_user_required");
                if (departmentId is not null || teamId is not null || relatedSubmissionId is not null)
                    return ("نطاق «موظّف محدد» يقبل تحديد الموظّف فقط.", "governance_item.scope_mismatch");
                break;
            case GovernanceApplicationScope.RelatedReport:
                if (relatedSubmissionId is null)
                    return ("يجب تحديد التقرير عند اختيار نطاق «تقرير مرتبط».", "governance_item.scope_report_required");
                if (departmentId is not null || teamId is not null || relatedUserId is not null)
                    return ("نطاق «تقرير مرتبط» يقبل تحديد التقرير فقط.", "governance_item.scope_mismatch");
                break;
        }

        // فرض الملكية لغير أصحاب الرؤية الواسعة: الهدف ضمن الدليل الموحّد (نطاق الملكية).
        if (!IsWideViewer && scope != GovernanceApplicationScope.Company)
        {
            var dir = await _directory.GetDirectoryAsync(GovernanceDirectoryPurpose.Workspace, ct);
            if (dir.Succeeded && dir.Value is GovernanceDirectoryDto d)
            {
                if (scope == GovernanceApplicationScope.Department && departmentId is Guid dep && d.Departments.All(x => x.Id != dep))
                    return ("لا تملك صلاحية استهداف هذه الإدارة خارج نطاقك.", "auth.forbidden");
                if (scope == GovernanceApplicationScope.Team && teamId is Guid tm && d.Teams.All(x => x.Id != tm))
                    return ("لا تملك صلاحية استهداف هذا الفريق خارج نطاقك.", "auth.forbidden");
                if (scope == GovernanceApplicationScope.User && relatedUserId is Guid ru && d.Users.All(x => x.Id != ru))
                    return ("لا تملك صلاحية استهداف هذا الموظّف خارج نطاقك.", "auth.forbidden");
            }
        }

        return null;
    }

    // ===== البناء والأسماء =====

    private async Task<GovernanceItemDetailDto> BuildDetailAsync(GovernanceItem item, Guid uid, CancellationToken ct)
    {
        // إعادة التحميل لضمان شمول كل الحركات بعد الحفظ.
        var fresh = await _db.GovernanceItems.AsNoTracking()
            .Include(g => g.Updates)
            .FirstAsync(g => g.Id == item.Id, ct);

        var names = await ResolveNamesAsync(new[] { fresh }, ct);
        var authorNames = await UserNamesAsync(fresh.Updates.Select(u => (Guid?)u.AuthorId), ct);
        if (fresh.ClosedById is Guid cb) names.Users.TryGetValue(cb, out _);
        var closedByName = fresh.ClosedById is Guid c
            ? (await UserNamesAsync(new[] { (Guid?)c }, ct)).GetValueOrDefault(c)
            : null;

        var listItem = MapListItem(fresh, names);
        var timeline = fresh.Updates
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new GovernanceItemUpdateDto(
                u.Id, u.AuthorId, authorNames.GetValueOrDefault(u.AuthorId),
                u.UpdateType, u.Body, u.OldStatus, u.NewStatus, u.CreatedAtUtc))
            .ToList();

        var vis = await BuildVisibilityAsync(uid, ct);
        var canEdit = CanEdit(fresh, uid, vis);
        return new GovernanceItemDetailDto(
            listItem, fresh.Description, fresh.ResolutionSummary, fresh.ClosedAtUtc, fresh.ClosedById, closedByName,
            canEdit, canEdit, timeline);
    }

    private static GovernanceItemListItemDto MapListItem(GovernanceItem g, ResolvedNames names) =>
        new(
            g.Id,
            g.Title,
            g.Category,
            g.Severity,
            g.Status,
            g.ApplicationScope,
            g.CreatedById,
            names.Users.GetValueOrDefault(g.CreatedById),
            g.AssignedToUserId,
            g.AssignedToUserId is Guid a ? names.Users.GetValueOrDefault(a) : null,
            g.DepartmentId,
            g.DepartmentId is Guid d ? names.Departments.GetValueOrDefault(d) : null,
            g.TeamId,
            g.TeamId is Guid t ? names.Teams.GetValueOrDefault(t) : null,
            g.RelatedSubmissionId,
            g.RelatedUserId,
            g.RelatedUserId is Guid r ? names.Users.GetValueOrDefault(r) : null,
            g.DueDate,
            g.CreatedAtUtc,
            g.UpdatedAtUtc);

    private readonly record struct ResolvedNames(Dictionary<Guid, string> Users, Dictionary<Guid, string> Departments, Dictionary<Guid, string> Teams);

    private async Task<ResolvedNames> ResolveNamesAsync(IEnumerable<GovernanceItem> items, CancellationToken ct)
    {
        var list = items as ICollection<GovernanceItem> ?? items.ToList();
        var userIds = list.SelectMany(g => new[] { (Guid?)g.CreatedById, g.AssignedToUserId, g.RelatedUserId, g.ClosedById });
        var deptIds = list.Select(g => g.DepartmentId);
        var teamIds = list.Select(g => g.TeamId);
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

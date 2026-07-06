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
/// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1): كيان مستقلّ يحوّل تصعيدًا فرديًّا/بند حوكمة عام/ملاحظة يدوية إلى إجراء
/// قابل للتتبّع بمُسنَد إليه واستحقاق وأولوية وحالة وخطّ زمنيّ. لا يوسّع أيّ كيان آخر ولا يمسّ سير اعتماد التقارير. الرؤية:
/// واسعة (Admin/CEO/GM/CeoSupport) = الكلّ + إدارة كاملة؛ Manager = نطاقه (ScopeResolver) إنشاء/إسناد/إدارة؛ TeamLeader/HR
/// إنشاء + إدارة ما أنشأه + تحديث ما أُسنِد إليه؛ Employee = رؤية ما أُسنِد إليه/أنشأه فقط. القراءة غير المصرّح بها تُقنَّع 404.
/// «متأخر» تُحسَب ولا تُخزَّن. الإجراء المنبثق من تصعيد يُعَدّ حسّاسًا فلا تُكشَف تفاصيل مصدره للمُسنَد إليه فقط (يرى نصّ الإجراء فقط).
/// إشعار بريد (EMAIL-NOTIFICATIONS-R1) عند الإنشاء بمُسنَد إليه وعند إعادة الإسناد فقط — لا يكسر العملية الأساسية.
/// </summary>
public class GovernanceActionItemService : IGovernanceActionItemService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;
    private readonly IGovernanceDirectoryService _directory;
    private readonly IEmailNotificationService _emailNotifications;

    public GovernanceActionItemService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IAuditService audit, IGovernanceDirectoryService directory, IEmailNotificationService emailNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
        _directory = directory;
        _emailNotifications = emailNotifications;
    }

    private bool IsWideViewer => _currentUser.IsInAnyRole(Roles.GovernanceActionItemWideViewers);
    private bool IsHr => _currentUser.IsInRole(Roles.Hr);
    private bool IsManager => _currentUser.IsInRole(Roles.Manager);
    private bool IsTeamLeader => _currentUser.IsInRole(Roles.TeamLeader);

    /// <summary>الإنشاء على مستوى الدور: رؤية واسعة (Admin/CEO/GM/CeoSupport) أو Manager/TeamLeader/HR. الموظف لا يُنشئ.</summary>
    private bool CanCreateActionItems => IsWideViewer || IsManager || IsTeamLeader || IsHr;

    private const string NotFoundMsg = "إجراء الحوكمة غير موجود.";
    private const string NotFoundCode = "governance_action_item.not_found";

    private static readonly string[] SensitiveAccountRoles = { Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport };

    public async Task<Result<IReadOnlyList<GovernanceActionItemListItemDto>>> ListAsync(GovernanceActionItemFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<GovernanceActionItemListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await BuildVisibilityAsync(uid, ct);

        var query = _db.GovernanceActionItems.AsNoTracking().AsQueryable();
        query = ApplyVisibility(query, uid, vis);

        if (filter.Status is ActionItemStatus st) query = query.Where(g => g.Status == st);
        if (filter.Priority is ActionItemPriority pr) query = query.Where(g => g.Priority == pr);
        if (filter.SourceType is ActionItemSourceType src) query = query.Where(g => g.SourceType == src);
        if (filter.SourceId is Guid sid) query = query.Where(g => g.SourceId == sid);
        if (filter.AssignedToUserId is Guid au) query = query.Where(g => g.AssignedToUserId == au);
        if (filter.DueFrom is DateOnly df) query = query.Where(g => g.DueDate != null && g.DueDate >= df);
        if (filter.DueTo is DateOnly dt) query = query.Where(g => g.DueDate != null && g.DueDate <= dt);
        if (filter.MineOnly) query = query.Where(g => g.CreatedByUserId == uid);
        if (filter.AssignedToMe) query = query.Where(g => g.AssignedToUserId == uid);
        if (filter.OverdueOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(g => g.DueDate != null && g.DueDate < today
                                     && g.Status != ActionItemStatus.Completed
                                     && g.Status != ActionItemStatus.Cancelled);
        }

        var rows = await query.OrderByDescending(g => g.CreatedAtUtc).ToListAsync(ct);
        var userNames = await UserNamesAsync(rows.SelectMany(g => new[] { (Guid?)g.CreatedByUserId, g.AssignedToUserId }), ct);
        var sourceTitles = await SourceTitlesAsync(rows, uid, vis, ct);
        var items = rows.Select(g => MapListItem(g, uid, vis, userNames, sourceTitles)).ToList();
        return Result<IReadOnlyList<GovernanceActionItemListItemDto>>.Success(items);
    }

    public async Task<Result<GovernanceActionItemDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceActionItems.AsNoTracking()
            .Include(g => g.Updates)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> CreateAsync(CreateGovernanceActionItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanCreateActionItems)
            return Result<GovernanceActionItemDetailDto>.Failure("لا تملك صلاحية إنشاء إجراء حوكمة.", "auth.forbidden");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GovernanceActionItemDetailDto>.Failure("العنوان مطلوب.", "governance_action_item.title_required");

        var vis = await BuildVisibilityAsync(uid, ct);

        // التحقّق من المصدر ووجوب رؤيته (منع IDOR لمصدر خارج النطاق) وحساب الحسّاسية الموروثة.
        var sourceCheck = await ValidateSourceAsync(request.SourceType, request.SourceId, uid, vis, ct);
        if (sourceCheck.Error is { } srcErr)
            return Result<GovernanceActionItemDetailDto>.Failure(srcErr.Message, srcErr.Code);

        if (request.AssignedToUserId is Guid assignee && !await _db.Users.AnyAsync(u => u.Id == assignee, ct))
            return Result<GovernanceActionItemDetailDto>.Failure("المستخدم المُسنَد إليه غير موجود.", "governance_action_item.assignee_not_found");

        var item = new GovernanceActionItem
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SourceType = request.SourceType,
            SourceId = request.SourceType == ActionItemSourceType.Manual ? null : request.SourceId,
            Priority = request.Priority,
            Status = ActionItemStatus.Open,
            DueDate = request.DueDate,
            AssignedToUserId = request.AssignedToUserId,
            AssignedByUserId = request.AssignedToUserId is null ? null : uid,
            CreatedByUserId = uid,
            IsSensitive = sourceCheck.IsSensitive
        };
        item.Updates.Add(new GovernanceActionItemUpdate
        {
            AuthorId = uid,
            UpdateType = ActionItemUpdateType.Created,
            NewStatus = ActionItemStatus.Open
        });
        if (request.AssignedToUserId is not null)
            item.Updates.Add(new GovernanceActionItemUpdate
            {
                AuthorId = uid,
                UpdateType = ActionItemUpdateType.AssigneeChanged
            });
        _db.GovernanceActionItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.created", nameof(GovernanceActionItem), item.Id,
            JsonSerializer.Serialize(new { sourceType = item.SourceType.ToString(), priority = item.Priority.ToString(), assigned = item.AssignedToUserId != null }), ct: ct);

        // إشعار بريد عند الإنشاء بمُسنَد إليه (حدث assigned) — لا يكسر العملية الأساسية.
        if (item.AssignedToUserId is not null)
        {
            try { await _emailNotifications.NotifyGovernanceActionItemAssignedAsync(item, isReassign: false, ct); }
            catch { /* تُسجَّل داخل الخدمة. */ }
        }

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceActionItemStatusRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceActionItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanChangeStatus(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure("لا تملك صلاحية تغيير حالة هذا الإجراء.", "auth.forbidden");

        if (request.Status == ActionItemStatus.Cancelled)
            return Result<GovernanceActionItemDetailDto>.Failure("استخدم إلغاء الإجراء بدل تغيير الحالة إلى ملغى.", "governance_action_item.use_cancel_endpoint");

        var oldStatus = item.Status;
        if (oldStatus == request.Status)
            return Result<GovernanceActionItemDetailDto>.Failure("الحالة غير متغيّرة.", "governance_action_item.status_unchanged.conflict");

        var manage = CanManage(item, uid, vis);
        var isReopen = oldStatus is ActionItemStatus.Completed or ActionItemStatus.Cancelled;

        // إعادة الفتح (من مكتمل/ملغى إلى حالة نشطة) مقصورة على المنشئ/الإدارة، لا المُسنَد إليه.
        if (isReopen && !manage)
            return Result<GovernanceActionItemDetailDto>.Failure("إعادة فتح الإجراء من صلاحية المنشئ/الإدارة فقط.", "auth.forbidden");

        // المُسنَد إليه (بلا صلاحية إدارة) محصور بانتقالات التنفيذ المسموحة.
        if (!manage && !IsAllowedAssigneeTransition(oldStatus, request.Status))
            return Result<GovernanceActionItemDetailDto>.Failure("انتقال الحالة غير مسموح.", "governance_action_item.transition_invalid.conflict");

        if (request.Status == ActionItemStatus.Completed)
        {
            item.CompletedAtUtc = DateTime.UtcNow;
            item.CompletedByUserId = uid;
            if (!string.IsNullOrWhiteSpace(request.CompletionNote))
                item.CompletionNote = request.CompletionNote.Trim();
        }
        else if (isReopen)
        {
            // مغادرة حالة مكتمل/ملغى تُفرغ بيانات الإكمال.
            item.CompletedAtUtc = null;
            item.CompletedByUserId = null;
            item.CompletionNote = null;
        }

        item.Status = request.Status;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceActionItemUpdates.Add(new GovernanceActionItemUpdate
        {
            ActionItemId = item.Id,
            AuthorId = uid,
            UpdateType = isReopen ? ActionItemUpdateType.Reopened
                : request.Status == ActionItemStatus.Completed ? ActionItemUpdateType.CompletionSubmitted
                : ActionItemUpdateType.StatusChanged,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = request.Status
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.status_changed", nameof(GovernanceActionItem), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString(), to = request.Status.ToString() }), ct: ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): إغلاق/إكمال إجراء ⇒ المنشئ + المُسنِد (دون مَن أغلق).
        if (request.Status == ActionItemStatus.Completed)
        {
            try { await _emailNotifications.NotifyGovernanceActionItemCompletedAsync(item, ct); }
            catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }
        }

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> AssignAsync(Guid id, AssignGovernanceActionItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceActionItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanManage(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure("لا تملك صلاحية إسناد هذا الإجراء.", "auth.forbidden");
        if (!await _db.Users.AnyAsync(u => u.Id == request.AssignedToUserId, ct))
            return Result<GovernanceActionItemDetailDto>.Failure("المستخدم المُسنَد إليه غير موجود.", "governance_action_item.assignee_not_found");

        item.AssignedToUserId = request.AssignedToUserId;
        item.AssignedByUserId = uid;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceActionItemUpdates.Add(new GovernanceActionItemUpdate
        {
            ActionItemId = item.Id,
            AuthorId = uid,
            UpdateType = ActionItemUpdateType.AssigneeChanged,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.assigned", nameof(GovernanceActionItem), item.Id, ct: ct);

        // إشعار بريد عند إعادة الإسناد (حدث reassigned) — لا يكسر العملية الأساسية.
        if (item.AssignedToUserId is not null)
        {
            try { await _emailNotifications.NotifyGovernanceActionItemAssignedAsync(item, isReassign: true, ct); }
            catch { /* تُسجَّل داخل الخدمة. */ }
        }

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> ChangeDueDateAsync(Guid id, ChangeGovernanceActionItemDueDateRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceActionItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanManage(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure("لا تملك صلاحية تغيير تاريخ استحقاق هذا الإجراء.", "auth.forbidden");

        item.DueDate = request.DueDate;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceActionItemUpdates.Add(new GovernanceActionItemUpdate
        {
            ActionItemId = item.Id,
            AuthorId = uid,
            UpdateType = ActionItemUpdateType.DueDateChanged,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.due_date_changed", nameof(GovernanceActionItem), item.Id, ct: ct);

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> AddCommentAsync(Guid id, AddGovernanceActionItemCommentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<GovernanceActionItemDetailDto>.Failure("نص التعليق مطلوب.", "governance_action_item.comment_required");

        var item = await _db.GovernanceActionItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        _db.GovernanceActionItemUpdates.Add(new GovernanceActionItemUpdate
        {
            ActionItemId = item.Id,
            AuthorId = uid,
            UpdateType = ActionItemUpdateType.Comment,
            Body = request.Body.Trim()
        });
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.comment_added", nameof(GovernanceActionItem), item.Id, ct: ct);

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    public async Task<Result<GovernanceActionItemDetailDto>> CancelAsync(Guid id, CancelGovernanceActionItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceActionItemDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var item = await _db.GovernanceActionItems.Include(g => g.Updates).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (item is null)
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);

        var vis = await BuildVisibilityAsync(uid, ct);
        if (!CanView(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure(NotFoundMsg, NotFoundCode);
        if (!CanManage(item, uid, vis))
            return Result<GovernanceActionItemDetailDto>.Failure("لا تملك صلاحية إلغاء هذا الإجراء.", "auth.forbidden");
        if (item.Status is ActionItemStatus.Completed or ActionItemStatus.Cancelled)
            return Result<GovernanceActionItemDetailDto>.Failure("لا يمكن إلغاء إجراء مكتمل أو ملغى.", "governance_action_item.not_cancellable.conflict");

        var oldStatus = item.Status;
        item.Status = ActionItemStatus.Cancelled;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _db.GovernanceActionItemUpdates.Add(new GovernanceActionItemUpdate
        {
            ActionItemId = item.Id,
            AuthorId = uid,
            UpdateType = ActionItemUpdateType.Cancelled,
            Body = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            OldStatus = oldStatus,
            NewStatus = ActionItemStatus.Cancelled
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "governance_action_item.cancelled", nameof(GovernanceActionItem), item.Id,
            JsonSerializer.Serialize(new { from = oldStatus.ToString() }), ct: ct);

        return Result<GovernanceActionItemDetailDto>.Success(await BuildDetailAsync(item, uid, ct));
    }

    // دليل المُسنَد إليهم يُفوَّض إلى مصدر الحوكمة الموحّد (GOV-DIRECTORY-SCOPE-FIX-R1) بغرض ActionItemAssignee:
    // الرؤية الواسعة ترى الكلّ شاملًا الحسّاسين؛ وغيرها ضمن نطاق ScopeResolver/إدارة HR عدا الحسّاسين + النفس.
    public async Task<Result<ActionItemAssigneeDirectoryDto>> GetAssigneeDirectoryAsync(CancellationToken ct = default)
    {
        var dir = await _directory.GetDirectoryAsync(GovernanceDirectoryPurpose.ActionItemAssignee, ct);
        if (!dir.Succeeded)
            return Result<ActionItemAssigneeDirectoryDto>.Failure(dir.Error!, dir.ErrorCode!);

        var userDtos = dir.Value!.Users
            .Select(u => new ActionItemAssigneeDto(u.Id, u.FullName, u.DepartmentId, u.TeamId))
            .ToList();
        return Result<ActionItemAssigneeDirectoryDto>.Success(new ActionItemAssigneeDirectoryDto(userDtos));
    }

    // ===== انتقالات حالة المُسنَد إليه (بلا صلاحية إدارة) =====

    private static bool IsAllowedAssigneeTransition(ActionItemStatus from, ActionItemStatus to) =>
        (from, to) switch
        {
            (ActionItemStatus.Open, ActionItemStatus.InProgress) => true,
            (ActionItemStatus.InProgress, ActionItemStatus.Blocked) => true,
            (ActionItemStatus.InProgress, ActionItemStatus.Completed) => true,
            (ActionItemStatus.Blocked, ActionItemStatus.InProgress) => true,
            _ => false
        };

    // ===== الرؤية والصلاحيات =====

    private readonly record struct Visibility(
        bool Wide, bool Hr, Guid? HrDepartmentId, HashSet<Guid> ScopeIds, bool ScopeSeesAll,
        HashSet<Guid> HrDeptUserIds, bool Manager, bool TeamLeader);

    private async Task<Visibility> BuildVisibilityAsync(Guid uid, CancellationToken ct)
    {
        var ownDept = await _db.Users.Where(u => u.Id == uid).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);

        if (IsWideViewer)
            return new Visibility(true, false, null, new HashSet<Guid>(), true, new HashSet<Guid>(), false, false);

        if (IsHr)
        {
            var hrDeptUsers = ownDept is Guid hd
                ? (await _db.Users.Where(u => u.DepartmentId == hd).Select(u => u.Id).ToListAsync(ct)).ToHashSet()
                : new HashSet<Guid>();
            return new Visibility(false, true, ownDept, new HashSet<Guid>(), false, hrDeptUsers, false, false);
        }

        var scope = await _scope.ResolveAsync(ct);
        return new Visibility(false, false, null, scope.UserIds.ToHashSet(), scope.SeesAll, new HashSet<Guid>(), IsManager, IsTeamLeader);
    }

    private static IQueryable<GovernanceActionItem> ApplyVisibility(IQueryable<GovernanceActionItem> query, Guid uid, Visibility vis)
    {
        if (vis.Wide) return query;

        if (vis.Hr)
        {
            return query.Where(g =>
                g.CreatedByUserId == uid
                || g.AssignedToUserId == uid
                || vis.HrDeptUserIds.Contains(g.CreatedByUserId)
                || (g.AssignedToUserId != null && vis.HrDeptUserIds.Contains(g.AssignedToUserId.Value)));
        }

        if (vis.ScopeSeesAll) return query;
        var ids = vis.ScopeIds;
        return query.Where(g =>
            g.CreatedByUserId == uid
            || g.AssignedToUserId == uid
            || ids.Contains(g.CreatedByUserId)
            || (g.AssignedToUserId != null && ids.Contains(g.AssignedToUserId.Value)));
    }

    private static bool CanView(GovernanceActionItem g, Guid uid, Visibility vis)
    {
        if (vis.Wide) return true;
        if (vis.Hr)
            return g.CreatedByUserId == uid
                || g.AssignedToUserId == uid
                || vis.HrDeptUserIds.Contains(g.CreatedByUserId)
                || (g.AssignedToUserId is Guid ha && vis.HrDeptUserIds.Contains(ha));
        if (vis.ScopeSeesAll) return true;
        return g.CreatedByUserId == uid
            || g.AssignedToUserId == uid
            || vis.ScopeIds.Contains(g.CreatedByUserId)
            || (g.AssignedToUserId is Guid a && vis.ScopeIds.Contains(a));
    }

    /// <summary>هل المدير ضمن نطاقه على هذا الإجراء (يشمل المنشئ/المُسنَد إليه أو SeesAll)؟</summary>
    private static bool ManagerInScope(GovernanceActionItem g, Visibility vis) =>
        vis.Manager && (vis.ScopeSeesAll
            || vis.ScopeIds.Contains(g.CreatedByUserId)
            || (g.AssignedToUserId is Guid a && vis.ScopeIds.Contains(a)));

    /// <summary>الإدارة الكاملة (إسناد/تغيير استحقاق/إلغاء/إعادة فتح): رؤية واسعة، أو مدير ضمن نطاقه، أو المنشئ.</summary>
    private static bool CanManage(GovernanceActionItem g, Guid uid, Visibility vis) =>
        vis.Wide || ManagerInScope(g, vis) || g.CreatedByUserId == uid;

    /// <summary>تغيير الحالة: إدارة كاملة أو المُسنَد إليه (محصورًا بانتقالات التنفيذ).</summary>
    private static bool CanChangeStatus(GovernanceActionItem g, Guid uid, Visibility vis) =>
        CanManage(g, uid, vis) || g.AssignedToUserId == uid;

    /// <summary>هل يُكشَف المصدر للمشاهِد؟ الإجراء الحسّاس (المنبثق من تصعيد) لا يكشف مصدره للمُسنَد إليه فقط.</summary>
    private static bool SourceVisible(GovernanceActionItem g, Guid uid, Visibility vis) =>
        !g.IsSensitive || vis.Wide || ManagerInScope(g, vis) || g.CreatedByUserId == uid;

    // ===== التحقّق من المصدر (وجوده + رؤيته + الحسّاسية الموروثة) =====

    private async Task<(( string Message, string Code)? Error, bool IsSensitive)> ValidateSourceAsync(
        ActionItemSourceType sourceType, Guid? sourceId, Guid uid, Visibility vis, CancellationToken ct)
    {
        if (sourceType == ActionItemSourceType.Manual)
            return (null, false);

        if (sourceId is not Guid sid)
            return ((("المصدر مطلوب لهذا النوع.", "governance_action_item.source_required")), false);

        if (sourceType == ActionItemSourceType.Escalation)
        {
            var esc = await _db.GovernanceEscalations.AsNoTracking()
                .Where(e => e.Id == sid)
                .Select(e => new { e.Id, e.RaisedByUserId, e.TargetUserId, e.AssignedToUserId })
                .FirstOrDefaultAsync(ct);
            if (esc is null)
                return ((("التصعيد المصدر غير موجود.", "governance_action_item.source_not_found")), false);
            // منع IDOR: لغير الرؤية الواسعة يجب أن يكون التصعيد ضمن النطاق (رافع/هدف/مُسنَد إليه).
            if (!vis.Wide)
            {
                var inScope = esc.RaisedByUserId == uid
                    || esc.TargetUserId == uid
                    || esc.AssignedToUserId == uid
                    || vis.ScopeIds.Contains(esc.RaisedByUserId)
                    || (esc.TargetUserId is Guid tu && vis.ScopeIds.Contains(tu))
                    || (esc.AssignedToUserId is Guid au && vis.ScopeIds.Contains(au))
                    || (vis.Hr && (vis.HrDeptUserIds.Contains(esc.RaisedByUserId)
                                   || (esc.TargetUserId is Guid htu && vis.HrDeptUserIds.Contains(htu))));
                if (!inScope)
                    return ((("لا يمكنك ربط تصعيد خارج نطاقك.", "governance_action_item.source_out_of_scope")), false);
            }
            // الإجراء المنبثق من تصعيد يُعَدّ حسّاسًا: تفاصيل المصدر لا تُكشَف للمُسنَد إليه فقط.
            return (null, true);
        }

        // GovernanceItem.
        if (!await _db.GovernanceItems.AnyAsync(x => x.Id == sid, ct))
            return ((("بند الحوكمة المصدر غير موجود.", "governance_action_item.source_not_found")), false);
        return (null, false);
    }

    // ===== البناء والأسماء =====

    private async Task<GovernanceActionItemDetailDto> BuildDetailAsync(GovernanceActionItem item, Guid uid, CancellationToken ct)
    {
        var fresh = await _db.GovernanceActionItems.AsNoTracking()
            .Include(g => g.Updates)
            .FirstAsync(g => g.Id == item.Id, ct);

        var vis = await BuildVisibilityAsync(uid, ct);

        var userIds = fresh.Updates.Select(u => (Guid?)u.AuthorId)
            .Concat(new[] { (Guid?)fresh.CreatedByUserId, fresh.AssignedToUserId, fresh.AssignedByUserId, fresh.CompletedByUserId });
        var userNames = await UserNamesAsync(userIds, ct);
        var sourceTitles = await SourceTitlesAsync(new[] { fresh }, uid, vis, ct);

        var listItem = MapListItem(fresh, uid, vis, userNames, sourceTitles);
        var timeline = fresh.Updates
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new GovernanceActionItemUpdateDto(
                u.Id, u.AuthorId, userNames.GetValueOrDefault(u.AuthorId),
                u.UpdateType, u.Body, u.OldStatus, u.NewStatus, u.CreatedAtUtc))
            .ToList();

        var notCancelled = fresh.Status != ActionItemStatus.Cancelled;
        return new GovernanceActionItemDetailDto(
            listItem,
            fresh.Description,
            fresh.CompletionNote,
            fresh.CompletedAtUtc,
            fresh.CompletedByUserId,
            fresh.CompletedByUserId is Guid c ? userNames.GetValueOrDefault(c) : null,
            fresh.AssignedByUserId,
            fresh.AssignedByUserId is Guid ab ? userNames.GetValueOrDefault(ab) : null,
            SourceVisible(fresh, uid, vis),
            CanChangeStatus(fresh, uid, vis) && notCancelled,
            CanManage(fresh, uid, vis) && notCancelled,
            CanManage(fresh, uid, vis) && notCancelled,
            CanManage(fresh, uid, vis) && fresh.Status is not (ActionItemStatus.Completed or ActionItemStatus.Cancelled),
            CanManage(fresh, uid, vis) && fresh.Status is (ActionItemStatus.Completed or ActionItemStatus.Cancelled),
            true,
            timeline);
    }

    private static bool ComputeOverdue(GovernanceActionItem g) =>
        g.DueDate is DateOnly d
        && g.Status != ActionItemStatus.Completed
        && g.Status != ActionItemStatus.Cancelled
        && d < DateOnly.FromDateTime(DateTime.UtcNow);

    private static GovernanceActionItemListItemDto MapListItem(
        GovernanceActionItem g, Guid uid, Visibility vis,
        Dictionary<Guid, string> userNames, Dictionary<Guid, string> sourceTitles)
    {
        var sourceVisible = SourceVisible(g, uid, vis);
        return new GovernanceActionItemListItemDto(
            g.Id,
            g.Title,
            g.SourceType,
            sourceVisible ? g.SourceId : null,
            sourceVisible && g.SourceId is Guid s ? sourceTitles.GetValueOrDefault(s) : null,
            g.Priority,
            g.Status,
            ComputeOverdue(g),
            g.DueDate,
            g.AssignedToUserId,
            g.AssignedToUserId is Guid a ? userNames.GetValueOrDefault(a) : null,
            g.CreatedByUserId,
            userNames.GetValueOrDefault(g.CreatedByUserId),
            g.IsSensitive,
            g.CreatedAtUtc,
            g.UpdatedAtUtc);
    }

    private async Task<Dictionary<Guid, string>> SourceTitlesAsync(
        ICollection<GovernanceActionItem> items, Guid uid, Visibility vis, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();

        var escIds = items
            .Where(g => g.SourceType == ActionItemSourceType.Escalation && g.SourceId is Guid && SourceVisible(g, uid, vis))
            .Select(g => g.SourceId!.Value).Distinct().ToList();
        if (escIds.Count > 0)
            foreach (var kv in await _db.GovernanceEscalations.Where(e => escIds.Contains(e.Id))
                         .Select(e => new { e.Id, e.Title }).ToListAsync(ct))
                result[kv.Id] = kv.Title;

        var govIds = items
            .Where(g => g.SourceType == ActionItemSourceType.GovernanceItem && g.SourceId is Guid && SourceVisible(g, uid, vis))
            .Select(g => g.SourceId!.Value).Distinct().ToList();
        if (govIds.Count > 0)
            foreach (var kv in await _db.GovernanceItems.Where(e => govIds.Contains(e.Id))
                         .Select(e => new { e.Id, e.Title }).ToListAsync(ct))
                result[kv.Id] = kv.Title;

        return result;
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is Guid g && g != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}

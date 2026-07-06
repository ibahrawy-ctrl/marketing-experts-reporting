using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// طلبات الموارد البشرية العامة (V1.1). يفرض الصلاحية خادميًّا: الموظّف ينشئ/يرى/يلغي طلباته فقط؛
/// الإدارة/HR (HrRequestManagers) ترى الكل وتعالج. كل إجراء حسّاس يُسجَّل في Audit + خطّ زمني خفيف.
/// لا بيانات حساسة في السجلّات (معرّفات وحالات فقط، لا محتوى مرفقات).
/// </summary>
public class EmployeeServiceRequestService : IEmployeeServiceRequestService
{
    private const long MaxFinalDocumentBytes = 10 * 1024 * 1024; // 10MB

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IEmailNotificationService _emailNotifications;
    private readonly IHostEnvironment _env;
    private readonly FileStorageOptions _storage;

    public EmployeeServiceRequestService(AppDbContext db, ICurrentUser currentUser,
        IAuditService audit, INotificationService notifications, IEmailNotificationService emailNotifications,
        IHostEnvironment env, IOptions<FileStorageOptions> storage)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notifications = notifications;
        _emailNotifications = emailNotifications;
        _env = env;
        _storage = storage.Value;
    }

    private bool CanManage => _currentUser.IsInAnyRole(Roles.HrRequestManagers);

    /// <summary>مُعرّفات مستخدمي الموارد البشرية النشطين (Role="HR") — لإشعارات وصول طلب جديد للمراجعة.</summary>
    private async Task<List<Guid>> HrUserIdsAsync(CancellationToken ct)
    {
        var hrRoleIds = await _db.Roles.Where(r => r.Name == Roles.Hr).Select(r => r.Id).ToListAsync(ct);
        if (hrRoleIds.Count == 0) return new();
        return await _db.UserRoles.Where(ur => hrRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);
    }

    /// <summary>مجلّد التخزين خارج جذر الويب — المُعرَّف بالإعدادات أو افتراضي داخل ContentRoot/App_Data.</summary>
    private string FinalDocumentsDirectory =>
        string.IsNullOrWhiteSpace(_storage.EmployeeServiceFinalDocumentsPath)
            ? Path.Combine(_env.ContentRootPath, "App_Data", "employee-service-requests", "final-documents")
            : _storage.EmployeeServiceFinalDocumentsPath;

    public async Task<Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>> GetMyAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var rows = await _db.EmployeeServiceRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == uid)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
        var names = await UserNamesAsync(rows.Select(r => r.RequesterUserId), ct);
        return Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>.Success(rows.Select(r => MapList(r, names)).ToList());
    }

    public async Task<Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>> ListAsync(
        EmployeeServiceRequestType? type, EmployeeServiceRequestStatus? status, string? q, Guid? userId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>.Failure("لا تملك صلاحية عرض طلبات الموارد البشرية.", "auth.forbidden");

        var query = _db.EmployeeServiceRequests.AsNoTracking().AsQueryable();
        if (type is EmployeeServiceRequestType t) query = query.Where(r => r.RequestType == t);
        if (status is EmployeeServiceRequestStatus s) query = query.Where(r => r.Status == s);
        if (userId is Guid u) query = query.Where(r => r.RequesterUserId == u);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r => r.Title.Contains(term) || (r.Description != null && r.Description.Contains(term)));
        }

        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.Select(r => r.RequesterUserId), ct);
        return Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>.Success(rows.Select(r => MapList(r, names)).ToList());
    }

    public async Task<Result<EmployeeServiceRequestDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await _db.EmployeeServiceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");

        if (entity.RequesterUserId != uid && !CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية عرض هذا الطلب.", "auth.forbidden");

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<EmployeeServiceRequestDto>> CreateAsync(CreateEmployeeServiceRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<EmployeeServiceRequestDto>.Failure("عنوان الطلب مطلوب.", "employee_service_request.title_required");

        var entity = new EmployeeServiceRequest
        {
            RequesterUserId = uid,
            RequestType = request.RequestType,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PreferredLanguage = request.PreferredLanguage,
            DestinationEntity = string.IsNullOrWhiteSpace(request.DestinationEntity) ? null : request.DestinationEntity.Trim(),
            AttachmentPath = string.IsNullOrWhiteSpace(request.AttachmentPath) ? null : request.AttachmentPath.Trim(),
            Status = EmployeeServiceRequestStatus.Submitted
        };
        _db.EmployeeServiceRequests.Add(entity);
        AddEvent(entity.Id, uid, "created", EmployeeServiceRequestStatus.Submitted, EmployeeServiceRequestStatus.Submitted, null);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.created", nameof(EmployeeServiceRequest), entity.Id,
            JsonSerializer.Serialize(new { entity.RequestType }), ct: ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): طلب موارد بشرية جديد ⇒ مستخدمو HR للمراجعة.
        try { await _emailNotifications.NotifyHrRequestCreatedAsync(entity, await HrUserIdsAsync(ct), ct); }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<EmployeeServiceRequestDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        if (entity.RequesterUserId != uid)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكنك إلغاء طلب لا تملكه.", "auth.forbidden");
        if (entity.Status is EmployeeServiceRequestStatus.Completed or EmployeeServiceRequestStatus.Rejected or EmployeeServiceRequestStatus.Cancelled)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكن إلغاء الطلب في حالته الحالية.", "employee_service_request.cannot_cancel");

        var from = entity.Status;
        entity.Status = EmployeeServiceRequestStatus.Cancelled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "cancelled", from, EmployeeServiceRequestStatus.Cancelled, null);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.cancelled", nameof(EmployeeServiceRequest), entity.Id, ct: ct);

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public Task<Result<EmployeeServiceRequestDto>> StartReviewAsync(Guid id, CancellationToken ct = default)
        => TransitionAsync(id, EmployeeServiceRequestStatus.InReview, "in_review",
            "employee_service_request.status_changed", null, null, false, ct);

    public async Task<Result<EmployeeServiceRequestDto>> CommentAsync(Guid id, EmployeeServiceRequestCommentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية معالجة هذا الطلب.", "auth.forbidden");
        if (string.IsNullOrWhiteSpace(request.Comment))
            return Result<EmployeeServiceRequestDto>.Failure("نص التعليق مطلوب.", "employee_service_request.comment_required");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");

        entity.HrComment = request.Comment.Trim();
        if (entity.AssignedToHrUserId is null) entity.AssignedToHrUserId = uid;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "commented", entity.Status, entity.Status, request.Comment.Trim());
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.commented", nameof(EmployeeServiceRequest), entity.Id, ct: ct);
        await _notifications.NotifyAsync(entity.RequesterUserId, "employee_service_request.comment",
            "تعليق جديد على طلب الموارد البشرية", request.Comment.Trim(), "/app/hr-requests", ct);

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<EmployeeServiceRequestDto>> CompleteAsync(Guid id, EmployeeServiceRequestCompleteRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية معالجة هذا الطلب.", "auth.forbidden");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        if (entity.Status is EmployeeServiceRequestStatus.Completed or EmployeeServiceRequestStatus.Rejected or EmployeeServiceRequestStatus.Cancelled)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكن إكمال الطلب في حالته الحالية.", "employee_service_request.invalid_state");

        var from = entity.Status;
        entity.Status = EmployeeServiceRequestStatus.Completed;
        if (!string.IsNullOrWhiteSpace(request.HrComment)) entity.HrComment = request.HrComment.Trim();
        if (entity.AssignedToHrUserId is null) entity.AssignedToHrUserId = uid;
        entity.CompletedAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "completed", from, EmployeeServiceRequestStatus.Completed, request.HrComment?.Trim());
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.completed", nameof(EmployeeServiceRequest), entity.Id, ct: ct);
        await _notifications.NotifyAsync(entity.RequesterUserId, "employee_service_request.completed",
            "اكتمل طلب الموارد البشرية", entity.Title, "/app/hr-requests", ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): إكمال طلب الموارد البشرية ⇒ صاحب الطلب.
        try { await _emailNotifications.NotifyHrRequestCompletedAsync(entity, ct); }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<EmployeeServiceRequestDto>> RejectAsync(Guid id, EmployeeServiceRequestRejectRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية معالجة هذا الطلب.", "auth.forbidden");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<EmployeeServiceRequestDto>.Failure("سبب الرفض مطلوب.", "employee_service_request.rejection_reason_required");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        if (entity.Status is EmployeeServiceRequestStatus.Completed or EmployeeServiceRequestStatus.Rejected or EmployeeServiceRequestStatus.Cancelled)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكن رفض الطلب في حالته الحالية.", "employee_service_request.invalid_state");

        var from = entity.Status;
        entity.Status = EmployeeServiceRequestStatus.Rejected;
        entity.RejectionReason = request.Reason.Trim();
        if (entity.AssignedToHrUserId is null) entity.AssignedToHrUserId = uid;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "rejected", from, EmployeeServiceRequestStatus.Rejected, request.Reason.Trim());
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.rejected", nameof(EmployeeServiceRequest), entity.Id, ct: ct);
        await _notifications.NotifyAsync(entity.RequesterUserId, "employee_service_request.rejected",
            "رُفض طلب الموارد البشرية", request.Reason.Trim(), "/app/hr-requests", ct);

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<EmployeeServiceRequestDto>> UploadFinalDocumentAsync(Guid id, FinalDocumentUpload upload, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية رفع الملف النهائي.", "auth.forbidden");

        // فحوص الملف قبل لمس القاعدة أو القرص.
        if (upload is null || upload.Length <= 0)
            return Result<EmployeeServiceRequestDto>.Failure("الملف مطلوب ولا يجوز أن يكون فارغًا.", "employee_service_request.file_required");
        if (upload.Length > MaxFinalDocumentBytes)
            return Result<EmployeeServiceRequestDto>.Failure("حجم الملف يتجاوز الحدّ المسموح (10MB).", "employee_service_request.file_too_large");
        if (!IsPdf(upload.FileName, upload.ContentType))
            return Result<EmployeeServiceRequestDto>.Failure("يُسمح بملفات PDF فقط.", "employee_service_request.file_must_be_pdf");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        // الاستبدال مسموح قبل الإكمال فقط — لا رفع/استبدال بعد إغلاق الطلب (Completed/Rejected/Cancelled).
        if (entity.Status is EmployeeServiceRequestStatus.Completed or EmployeeServiceRequestStatus.Rejected or EmployeeServiceRequestStatus.Cancelled)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكن رفع أو استبدال الملف النهائي بعد إغلاق الطلب.", "employee_service_request.final_document_locked.conflict");

        var dir = FinalDocumentsDirectory;
        Directory.CreateDirectory(dir);
        var internalName = $"{Guid.NewGuid():N}.pdf";
        var fullPath = Path.Combine(dir, internalName);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await upload.Content.CopyToAsync(fs, ct);

        // حذف الملف السابق (استبدال) — أفضل جهد، لا يفشل العملية.
        var previousPath = entity.HrAttachmentPath;

        entity.HrAttachmentPath = fullPath;
        entity.HrAttachmentOriginalFileName = SafeFileName(upload.FileName);
        entity.HrAttachmentContentType = "application/pdf";
        entity.HrAttachmentSizeBytes = upload.Length;
        entity.HrAttachmentUploadedAtUtc = DateTime.UtcNow;
        entity.HrAttachmentUploadedByUserId = uid;
        if (entity.AssignedToHrUserId is null) entity.AssignedToHrUserId = uid;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "final_document_uploaded", entity.Status, entity.Status, null);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "employee_service_request.final_document_uploaded", nameof(EmployeeServiceRequest), entity.Id,
            JsonSerializer.Serialize(new { entity.HrAttachmentSizeBytes }), ct: ct);

        if (!string.IsNullOrWhiteSpace(previousPath) && previousPath != fullPath)
            TryDeleteFile(previousPath);

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<FinalDocumentDownload>> DownloadFinalDocumentAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<FinalDocumentDownload>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await _db.EmployeeServiceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<FinalDocumentDownload>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        if (entity.RequesterUserId != uid && !CanManage)
            return Result<FinalDocumentDownload>.Failure("لا تملك صلاحية تنزيل هذا الملف.", "auth.forbidden");

        if (string.IsNullOrWhiteSpace(entity.HrAttachmentPath) || !File.Exists(entity.HrAttachmentPath))
            return Result<FinalDocumentDownload>.Failure("لا يوجد ملف نهائي لهذا الطلب.", "employee_service_request.final_document.not_found");

        var downloadName = string.IsNullOrWhiteSpace(entity.HrAttachmentOriginalFileName)
            ? $"final-document-{entity.Id:N}.pdf"
            : entity.HrAttachmentOriginalFileName;
        return Result<FinalDocumentDownload>.Success(
            new FinalDocumentDownload(entity.HrAttachmentPath, entity.HrAttachmentContentType ?? "application/pdf", downloadName));
    }

    // ===== مساعدات =====

    private static bool IsPdf(string? fileName, string? contentType)
    {
        var byExt = !string.IsNullOrWhiteSpace(fileName) &&
            fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        var byType = !string.IsNullOrWhiteSpace(contentType) &&
            contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        return byExt || byType;
    }

    private static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return "document.pdf";
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
        return name.Length > 260 ? name[^260..] : name;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* أفضل جهد */ }
    }

    private async Task<Result<EmployeeServiceRequestDto>> TransitionAsync(
        Guid id, EmployeeServiceRequestStatus toStatus, string action, string auditAction,
        string? comment, string? notifyTitle, bool notify, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeServiceRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<EmployeeServiceRequestDto>.Failure("لا تملك صلاحية معالجة هذا الطلب.", "auth.forbidden");

        var entity = await _db.EmployeeServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<EmployeeServiceRequestDto>.Failure("الطلب غير موجود.", "employee_service_request.not_found");
        if (entity.Status is EmployeeServiceRequestStatus.Completed or EmployeeServiceRequestStatus.Rejected or EmployeeServiceRequestStatus.Cancelled)
            return Result<EmployeeServiceRequestDto>.Failure("لا يمكن تغيير حالة الطلب في حالته الحالية.", "employee_service_request.invalid_state");
        if (entity.Status == toStatus)
            return Result<EmployeeServiceRequestDto>.Failure("الطلب في هذه الحالة مسبقًا.", "employee_service_request.invalid_state");

        var from = entity.Status;
        entity.Status = toStatus;
        if (entity.AssignedToHrUserId is null) entity.AssignedToHrUserId = uid;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, action, from, toStatus, comment);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, auditAction, nameof(EmployeeServiceRequest), entity.Id, ct: ct);
        if (notify && notifyTitle is not null)
            await _notifications.NotifyAsync(entity.RequesterUserId, "employee_service_request.status",
                notifyTitle, entity.Title, "/app/hr-requests", ct);

        return Result<EmployeeServiceRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    private void AddEvent(Guid requestId, Guid actorId, string action,
        EmployeeServiceRequestStatus from, EmployeeServiceRequestStatus to, string? comment)
    {
        _db.EmployeeServiceRequestEvents.Add(new EmployeeServiceRequestEvent
        {
            EmployeeServiceRequestId = requestId,
            ActorUserId = actorId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });
    }

    private async Task<EmployeeServiceRequestDto> BuildAsync(EmployeeServiceRequest entity, Guid uid, CancellationToken ct)
    {
        var events = await _db.EmployeeServiceRequestEvents.AsNoTracking()
            .Where(e => e.EmployeeServiceRequestId == entity.Id)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        var ids = new List<Guid> { entity.RequesterUserId };
        if (entity.AssignedToHrUserId is Guid a) ids.Add(a);
        ids.AddRange(events.Select(e => e.ActorUserId));
        var names = await UserNamesAsync(ids, ct);

        var canCancel = entity.RequesterUserId == uid && entity.Status is EmployeeServiceRequestStatus.Submitted or EmployeeServiceRequestStatus.InReview;

        return new EmployeeServiceRequestDto(
            entity.Id, entity.RequesterUserId, names.GetValueOrDefault(entity.RequesterUserId, string.Empty),
            entity.RequestType, entity.Title, entity.Description, entity.PreferredLanguage, entity.DestinationEntity,
            entity.AttachmentPath, entity.Status, entity.HrComment,
            !string.IsNullOrWhiteSpace(entity.HrAttachmentPath),
            entity.HrAttachmentOriginalFileName, entity.HrAttachmentUploadedAtUtc,
            entity.AssignedToHrUserId, entity.AssignedToHrUserId is Guid h ? names.GetValueOrDefault(h) : null,
            entity.RejectionReason, entity.CompletedAtUtc, canCancel, entity.CreatedAtUtc, entity.UpdatedAtUtc,
            events.Select(e => new EmployeeServiceRequestEventDto(
                e.Id, e.ActorUserId, names.GetValueOrDefault(e.ActorUserId), e.Action,
                e.FromStatus, e.ToStatus, e.Comment, e.CreatedAtUtc)).ToList());
    }

    private static EmployeeServiceRequestListItemDto MapList(EmployeeServiceRequest r, Dictionary<Guid, string> names) =>
        new(r.Id, r.RequesterUserId, names.GetValueOrDefault(r.RequesterUserId, string.Empty),
            r.RequestType, r.Title, r.PreferredLanguage, r.Status, r.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}

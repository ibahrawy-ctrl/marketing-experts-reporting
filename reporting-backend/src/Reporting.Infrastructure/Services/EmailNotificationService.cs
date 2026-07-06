using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة إشعارات البريد المستقلّة (EMAIL-NOTIFICATIONS-R1) — لا تمسّ صندوق الصادر القديم (email_outbox).
/// تُنشئ صفًّا في email_notifications لكل حدث/مستلم، تمنع التكرار عبر CorrelationKey، وتحترم الوضع EmailNotifications:Mode.
/// الافتراضي DryRun ⇒ لا إرسال SMTP. الإرسال الفعليّ (Enabled) محروس ولا يجري إلا إذا كان المُرسِل مُهيّأً.
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _sender;
    private readonly EmailNotificationOptions _options;
    private readonly AppOptions _app;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        AppDbContext db,
        IEmailSender sender,
        IOptions<EmailNotificationOptions> options,
        IOptions<AppOptions> app,
        ILogger<EmailNotificationService> logger)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
        _app = app.Value;
        _logger = logger;
    }

    public async Task NotifyGovernanceItemAssignedAsync(GovernanceItem item, CancellationToken ct = default)
    {
        if (item.AssignedToUserId is not { } recipientId) return;

        var link = BuildLink("/app/governance-workspace");
        var body = BuildItemBody(item, link);
        await EnqueueAsync(
            eventType: "governance-item-created",
            entityType: nameof(GovernanceItem),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"governance-item-created:{item.Id}:{recipientId}",
            subject: "تم إسناد بند حوكمة جديد إليك",
            title: "تم إسناد بند حوكمة جديد إليك",
            body: body,
            link: link,
            createdByUserId: item.CreatedById,
            ct: ct);
    }

    public async Task NotifyGovernanceActionItemAssignedAsync(GovernanceActionItem item, bool isReassign, CancellationToken ct = default)
    {
        if (item.AssignedToUserId is not { } recipientId) return;

        var link = BuildLink("/app/governance/action-items");
        var eventType = isReassign ? "governance-action-item-reassigned" : "governance-action-item-assigned";
        var subject = isReassign ? "تم إعادة إسناد إجراء حوكمة إليك" : "تم إسناد إجراء حوكمة جديد إليك";
        var body = BuildActionItemBody(item, link);
        await EnqueueAsync(
            eventType: eventType,
            entityType: nameof(GovernanceActionItem),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"{eventType}:{item.Id}:{recipientId}",
            subject: subject,
            title: subject,
            body: body,
            link: link,
            createdByUserId: item.AssignedByUserId ?? item.CreatedByUserId,
            ct: ct);
    }

    public async Task NotifyGovernanceEscalationCreatedAsync(GovernanceEscalation item, CancellationToken ct = default)
    {
        // لا مستلم فرديّ واضح إلا عند التصعيد الموجَّه لموظّف بعينه.
        if (item.TargetType != EscalationTargetType.User) return;
        if (item.TargetUserId is not { } recipientId) return;

        var link = BuildLink("/app/governance/escalations");
        var body = BuildEscalationBody(item, link);
        await EnqueueAsync(
            eventType: "governance-escalation-created",
            entityType: nameof(GovernanceEscalation),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"governance-escalation-created:{item.Id}:{recipientId}",
            subject: "تم إنشاء تصعيد جديد يحتاج للمراجعة",
            title: "تم إنشاء تصعيد جديد يحتاج للمراجعة",
            body: body,
            link: link,
            createdByUserId: item.RaisedByUserId,
            ct: ct);
    }

    // ===== EMAIL-OPERATIONAL-NOTIFICATIONS-R1 =====

    public async Task NotifyGovernanceItemUpdatedAsync(GovernanceItem item, string changeKey, CancellationToken ct = default)
    {
        var link = BuildLink("/app/governance-workspace");
        var body = BuildItemUpdatedBody(item, changeKey, link);

        // المستلمون: المُسنَد إليه + المنشئ (إن اختلف). لا يُرسَل لنطاق التطبيق كاملًا.
        var recipients = new List<Guid>();
        if (item.AssignedToUserId is { } assignee) recipients.Add(assignee);
        if (item.CreatedById != Guid.Empty && item.CreatedById != item.AssignedToUserId) recipients.Add(item.CreatedById);

        foreach (var recipientId in recipients.Distinct())
        {
            await EnqueueAsync(
                eventType: "governance-item-updated",
                entityType: nameof(GovernanceItem),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"governance-item-updated:{item.Id}:{recipientId}:{changeKey}",
                subject: "تم تحديث بند حوكمة مرتبط بك",
                title: "تم تحديث بند حوكمة مرتبط بك",
                body: body,
                link: link,
                createdByUserId: item.CreatedById,
                ct: ct);
        }
    }

    public async Task NotifyGovernanceActionItemCompletedAsync(GovernanceActionItem item, CancellationToken ct = default)
    {
        var link = BuildLink("/app/governance/action-items");
        var body = BuildActionItemCompletedBody(item, link);

        // المستلمون: المنشئ + المُسنِد (مالك الإجراء)، مع استبعاد مَن أغلقه لتفادي إشعار الذات.
        var recipients = new List<Guid>();
        if (item.CreatedByUserId != Guid.Empty) recipients.Add(item.CreatedByUserId);
        if (item.AssignedByUserId is { } owner) recipients.Add(owner);

        foreach (var recipientId in recipients.Distinct().Where(id => id != item.CompletedByUserId))
        {
            await EnqueueAsync(
                eventType: "governance-action-item-completed",
                entityType: nameof(GovernanceActionItem),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"governance-action-item-completed:{item.Id}:{recipientId}",
                subject: "تم إغلاق إجراء حوكمة",
                title: "تم إغلاق إجراء حوكمة",
                body: body,
                link: link,
                createdByUserId: item.CompletedByUserId,
                ct: ct);
        }
    }

    public async Task NotifyGovernanceEscalationAssignedAsync(GovernanceEscalation item, CancellationToken ct = default)
    {
        if (item.AssignedToUserId is not { } recipientId) return;

        var link = BuildLink("/app/governance/escalations");
        var body = BuildEscalationBody(item, link);
        await EnqueueAsync(
            eventType: "governance-escalation-assigned",
            entityType: nameof(GovernanceEscalation),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"governance-escalation-assigned:{item.Id}:{recipientId}",
            subject: "تم إسناد تصعيد إليك للمراجعة",
            title: "تم إسناد تصعيد إليك للمراجعة",
            body: body,
            link: link,
            createdByUserId: item.RaisedByUserId,
            ct: ct);
    }

    public async Task NotifyGovernanceEscalationClosedAsync(GovernanceEscalation item, CancellationToken ct = default)
    {
        var link = BuildLink("/app/governance/escalations");
        var body = BuildEscalationClosedBody(item, link);

        // المستلمون: الرافع + المُسنَد إليه (إن اختلف)، مع استبعاد مَن أغلقه.
        var recipients = new List<Guid>();
        if (item.RaisedByUserId != Guid.Empty) recipients.Add(item.RaisedByUserId);
        if (item.AssignedToUserId is { } assignee) recipients.Add(assignee);

        foreach (var recipientId in recipients.Distinct().Where(id => id != item.ClosedByUserId))
        {
            await EnqueueAsync(
                eventType: "governance-escalation-closed",
                entityType: nameof(GovernanceEscalation),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"governance-escalation-closed:{item.Id}:{recipientId}",
                subject: "تم إغلاق التصعيد",
                title: "تم إغلاق التصعيد",
                body: body,
                link: link,
                createdByUserId: item.ClosedByUserId,
                ct: ct);
        }
    }

    public async Task NotifyLeaveRequestCreatedAsync(LeaveRequest item, IReadOnlyCollection<Guid> reviewerUserIds, CancellationToken ct = default)
    {
        var link = BuildLink("/app/leave-requests?tab=pending");
        var body = BuildLeaveBody(item, link, "الإجراء المطلوب: مراجعة الطلب واتخاذ القرار من خلال النظام.");
        foreach (var recipientId in reviewerUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            await EnqueueAsync(
                eventType: "leave-request-created",
                entityType: nameof(LeaveRequest),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"leave-request-created:{item.Id}:{recipientId}",
                subject: "طلب إجازة جديد يحتاج للمراجعة",
                title: "طلب إجازة جديد يحتاج للمراجعة",
                body: body,
                link: link,
                createdByUserId: item.RequesterUserId,
                ct: ct);
        }
    }

    public async Task NotifyLeaveRequestNeedsHrActionAsync(LeaveRequest item, IReadOnlyCollection<Guid> hrUserIds, CancellationToken ct = default)
    {
        var link = BuildLink("/app/leave-requests?tab=pending");
        var body = BuildLeaveBody(item, link, "الإجراء المطلوب: إجراء الموارد البشرية على الطلب من خلال النظام.");
        foreach (var recipientId in hrUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            await EnqueueAsync(
                eventType: "leave-request-needs-hr-action",
                entityType: nameof(LeaveRequest),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"leave-request-needs-hr-action:{item.Id}:{recipientId}",
                subject: "طلب إجازة يحتاج لإجراء من الموارد البشرية",
                title: "طلب إجازة يحتاج لإجراء من الموارد البشرية",
                body: body,
                link: link,
                createdByUserId: item.RequesterUserId,
                ct: ct);
        }
    }

    public async Task NotifyLeaveRequestApprovedAsync(LeaveRequest item, CancellationToken ct = default)
    {
        var recipientId = item.RequesterUserId;
        if (recipientId == Guid.Empty) return;

        var link = BuildLink("/app/leave-requests");
        var body = BuildLeaveBody(item, link, "تمت الموافقة على طلبك. يمكنك متابعة التفاصيل من خلال النظام.");
        await EnqueueAsync(
            eventType: "leave-request-approved",
            entityType: nameof(LeaveRequest),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"leave-request-approved:{item.Id}:{recipientId}",
            subject: "تمت الموافقة على طلب الإجازة",
            title: "تمت الموافقة على طلب الإجازة",
            body: body,
            link: link,
            createdByUserId: item.HrReviewerId,
            ct: ct);
    }

    public async Task NotifyLeaveRequestRejectedAsync(LeaveRequest item, CancellationToken ct = default)
    {
        var recipientId = item.RequesterUserId;
        if (recipientId == Guid.Empty) return;

        var link = BuildLink("/app/leave-requests");
        var extra = string.IsNullOrWhiteSpace(item.RejectionReason)
            ? "نعتذر، لم تتم الموافقة على طلبك هذه المرة. يمكنك التواصل مع مسؤولك المباشر لمزيد من التفاصيل."
            : $"نعتذر، لم تتم الموافقة على طلبك هذه المرة. السبب: {item.RejectionReason}";
        var body = BuildLeaveBody(item, link, extra);
        await EnqueueAsync(
            eventType: "leave-request-rejected",
            entityType: nameof(LeaveRequest),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"leave-request-rejected:{item.Id}:{recipientId}",
            subject: "تم رفض طلب الإجازة",
            title: "تحديث بشأن طلب الإجازة",
            body: body,
            link: link,
            createdByUserId: null,
            ct: ct);
    }

    public async Task NotifyHrRequestCreatedAsync(EmployeeServiceRequest item, IReadOnlyCollection<Guid> hrUserIds, CancellationToken ct = default)
    {
        var link = BuildLink("/app/hr-requests");
        var body = BuildHrRequestBody(item, link, "الإجراء المطلوب: مراجعة الطلب ومعالجته من خلال النظام.");
        foreach (var recipientId in hrUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            await EnqueueAsync(
                eventType: "hr-request-created",
                entityType: nameof(EmployeeServiceRequest),
                entityId: item.Id,
                recipientUserId: recipientId,
                correlationKey: $"hr-request-created:{item.Id}:{recipientId}",
                subject: "طلب موارد بشرية جديد يحتاج للمراجعة",
                title: "طلب موارد بشرية جديد يحتاج للمراجعة",
                body: body,
                link: link,
                createdByUserId: item.RequesterUserId,
                ct: ct);
        }
    }

    public async Task NotifyHrRequestCompletedAsync(EmployeeServiceRequest item, CancellationToken ct = default)
    {
        var recipientId = item.RequesterUserId;
        if (recipientId == Guid.Empty) return;

        var link = BuildLink("/app/hr-requests");
        var body = BuildHrRequestBody(item, link, "تم إنجاز طلبك. يمكنك متابعة التفاصيل من خلال النظام.");
        await EnqueueAsync(
            eventType: "hr-request-completed",
            entityType: nameof(EmployeeServiceRequest),
            entityId: item.Id,
            recipientUserId: recipientId,
            correlationKey: $"hr-request-completed:{item.Id}:{recipientId}",
            subject: "تم إغلاق طلب الموارد البشرية",
            title: "تم إغلاق طلب الموارد البشرية",
            body: body,
            link: link,
            createdByUserId: item.AssignedToHrUserId,
            ct: ct);
    }

    public async Task<IReadOnlyList<EmailNotificationLogDto>> ListAsync(int take = 200, CancellationToken ct = default)
    {
        if (take <= 0) take = 200;
        if (take > 1000) take = 1000;

        return await _db.EmailNotifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new EmailNotificationLogDto(
                n.Id,
                n.CreatedAtUtc,
                n.EventType,
                n.EntityType,
                n.EntityId,
                n.RecipientName,
                n.RecipientEmail,
                n.Subject,
                n.Status.ToString(),
                n.Mode.ToString(),
                n.FailureReason,
                n.CorrelationKey))
            .ToListAsync(ct);
    }

    // ===== EMAIL-NOTIFICATIONS-UI-R1 (سطح مراجعة مصفّح، قراءة فقط — لا إرسال/تعديل/حذف) =====

    public async Task<EmailNotificationLogPageDto> ListLogAsync(EmailNotificationLogFilter filter, CancellationToken ct = default)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 25 : (filter.PageSize > 200 ? 200 : filter.PageSize);

        var baseQuery = _db.EmailNotifications.AsNoTracking();

        // الملخّص ثابت على كامل الجدول (مستقلّ عن الفلاتر) — لبطاقات الواجهة.
        var summary = await baseQuery
            .GroupBy(_ => 1)
            .Select(g => new EmailNotificationLogSummaryDto(
                g.Count(),
                g.Count(n => n.Status == EmailNotificationStatus.DryRun),
                g.Count(n => n.Status == EmailNotificationStatus.Skipped),
                g.Count(n => n.Status == EmailNotificationStatus.Failed),
                g.Count(n => n.Status == EmailNotificationStatus.Sent),
                g.Count(n => n.Status == EmailNotificationStatus.Pending),
                g.Count(n => n.Status == EmailNotificationStatus.Cancelled),
                g.Max(n => (DateTime?)n.CreatedAtUtc)))
            .FirstOrDefaultAsync(ct)
            ?? new EmailNotificationLogSummaryDto(0, 0, 0, 0, 0, 0, 0, null);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<EmailNotificationStatus>(filter.Status, ignoreCase: true, out var status))
        {
            query = query.Where(n => n.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            var evt = filter.EventType.Trim();
            query = query.Where(n => n.EventType == evt);
        }

        if (filter.RecipientUserId is { } recipientId)
        {
            query = query.Where(n => n.RecipientUserId == recipientId);
        }

        if (filter.DateFrom is { } from)
        {
            var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAtUtc >= fromUtc);
        }

        if (filter.DateTo is { } to)
        {
            var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var like = $"%{term}%";
            query = query.Where(n =>
                EF.Functions.ILike(n.Subject, like)
                || (n.RecipientName != null && EF.Functions.ILike(n.RecipientName, like))
                || (n.RecipientEmail != null && EF.Functions.ILike(n.RecipientEmail, like))
                || EF.Functions.ILike(n.CorrelationKey, like));
        }

        var totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.CreatedAtUtc,
                n.EventType,
                Status = n.Status.ToString(),
                Mode = n.Mode.ToString(),
                n.RecipientUserId,
                n.RecipientName,
                n.RecipientEmail,
                n.Subject,
                n.BodyText,
                n.BodyHtml,
                n.CorrelationKey,
                n.EntityType,
                n.EntityId,
                n.FailureReason,
            })
            .ToListAsync(ct);

        var items = rows.Select(n => new EmailNotificationRowDto(
            n.Id,
            n.CreatedAtUtc,
            n.EventType,
            n.Status,
            n.Mode,
            n.RecipientUserId,
            n.RecipientName,
            n.RecipientEmail,
            n.Subject,
            BuildBodyPreview(n.BodyText, n.BodyHtml),
            n.CorrelationKey,
            n.EntityType,
            n.EntityId,
            n.FailureReason)).ToList();

        return new EmailNotificationLogPageDto(items, page, pageSize, totalCount, summary);
    }

    public async Task<EmailNotificationLogDetailDto?> GetLogAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.EmailNotifications.AsNoTracking()
            .Where(n => n.Id == id)
            .Select(n => new EmailNotificationLogDetailDto(
                n.Id,
                n.CreatedAtUtc,
                n.EventType,
                n.Status.ToString(),
                n.Mode.ToString(),
                n.RecipientUserId,
                n.RecipientName,
                n.RecipientEmail,
                n.Subject,
                n.BodyHtml,
                n.BodyText,
                n.CorrelationKey,
                n.EntityType,
                n.EntityId,
                n.AttemptCount,
                n.LastAttemptAt,
                n.SentAt,
                n.FailedAt,
                n.FailureReason,
                n.CreatedByUserId))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>معاينة مختصرة للمتن: نصّ صِرف إن وُجد، وإلا HTML مُجرَّد من الوسوم، مقصوصًا.</summary>
    private static string BuildBodyPreview(string? bodyText, string bodyHtml)
    {
        var source = !string.IsNullOrWhiteSpace(bodyText)
            ? bodyText!
            : System.Text.RegularExpressions.Regex.Replace(bodyHtml ?? string.Empty, "<[^>]+>", " ");
        source = System.Text.RegularExpressions.Regex.Replace(source, "\\s+", " ").Trim();
        return source.Length <= 160 ? source : source[..160] + "…";
    }

    // EMAIL-REPORT-REMINDERS-R1 — إدراج تذكير تقرير عبر القلب الآمن نفسه (بلا كتابة مباشرة على الجدول).
    public Task<ReportReminderOutcome> EnqueueReportReminderAsync(ReportReminderMessage message, CancellationToken ct = default)
        => EnqueueAsync(
            eventType: message.EventType,
            entityType: message.EntityType,
            entityId: message.EntityId,
            recipientUserId: message.RecipientUserId,
            correlationKey: message.CorrelationKey,
            subject: message.Subject,
            title: message.Subject,
            body: message.Body,
            link: message.Link,
            createdByUserId: null,
            ct: ct);

    /// <summary>
    /// قلب الخدمة: يحترم الوضع، يمنع التكرار، يحلّ المستلم، يبني الرسالة، ينشئ الصفّ،
    /// ويُرسِل فعليًّا فقط في وضع Enabled مع مُرسِل مُهيّأ. لا يرمي للمتصل. يُرجِع نتيجة الإدراج.
    /// </summary>
    private async Task<ReportReminderOutcome> EnqueueAsync(
        string eventType,
        string entityType,
        Guid entityId,
        Guid recipientUserId,
        string correlationKey,
        string subject,
        string title,
        string body,
        string link,
        Guid? createdByUserId,
        CancellationToken ct)
    {
        try
        {
            // وضع التعطيل: لا صفّ ولا إرسال.
            if (_options.Mode == EmailNotificationMode.Disabled)
            {
                _logger.LogDebug("EmailNotifications disabled; skipping {EventType} for {EntityType}/{EntityId}",
                    eventType, entityType, entityId);
                return ReportReminderOutcome.Disabled;
            }

            // منع التكرار عبر مفتاح الترابط.
            var exists = await _db.EmailNotifications.AsNoTracking()
                .AnyAsync(n => n.CorrelationKey == correlationKey, ct);
            if (exists)
            {
                _logger.LogDebug("EmailNotification duplicate skipped for {CorrelationKey}", correlationKey);
                return ReportReminderOutcome.Duplicate;
            }

            // حلّ المستلم (بريد + اسم).
            var recipient = await _db.Users.AsNoTracking()
                .Where(u => u.Id == recipientUserId)
                .Select(u => new { u.FullName, u.Email })
                .FirstOrDefaultAsync(ct);

            var html = EmailHtml.Build(title, body, link);

            var entity = new EmailNotification
            {
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                RecipientUserId = recipientUserId,
                RecipientEmail = recipient?.Email,
                RecipientName = recipient?.FullName,
                Subject = subject,
                BodyHtml = html,
                BodyText = body,
                Mode = _options.Mode,
                CorrelationKey = correlationKey,
                CreatedByUserId = createdByUserId
            };

            // لا بريد صالح ⇒ تخطّي مرئيّ بلا إرسال.
            if (string.IsNullOrWhiteSpace(recipient?.Email))
            {
                entity.Status = EmailNotificationStatus.Skipped;
                entity.FailureReason = "recipient_email_missing";
                _db.EmailNotifications.Add(entity);
                await _db.SaveChangesAsync(ct);
                return ReportReminderOutcome.SkippedNoEmail;
            }

            if (_options.Mode == EmailNotificationMode.DryRun)
            {
                entity.Status = EmailNotificationStatus.DryRun;
                _db.EmailNotifications.Add(entity);
                await _db.SaveChangesAsync(ct);
                return ReportReminderOutcome.Created;
            }

            // وضع Enabled: إرسال فعليّ محروس بتهيئة المُرسِل.
            entity.Status = EmailNotificationStatus.Pending;
            _db.EmailNotifications.Add(entity);
            await _db.SaveChangesAsync(ct);

            if (!_sender.IsConfigured)
            {
                entity.Status = EmailNotificationStatus.Failed;
                entity.FailedAt = DateTime.UtcNow;
                entity.FailureReason = "sender_not_configured";
                await _db.SaveChangesAsync(ct);
                return ReportReminderOutcome.Created;
            }

            entity.AttemptCount++;
            entity.LastAttemptAt = DateTime.UtcNow;
            var result = await _sender.SendAsync(entity.RecipientEmail!, entity.RecipientName, entity.Subject, entity.BodyHtml, ct);
            if (result.Success)
            {
                entity.Status = EmailNotificationStatus.Sent;
                entity.SentAt = DateTime.UtcNow;
                entity.FailureReason = null;
            }
            else
            {
                entity.Status = EmailNotificationStatus.Failed;
                entity.FailedAt = DateTime.UtcNow;
                entity.FailureReason = result.Error;
            }
            await _db.SaveChangesAsync(ct);
            return ReportReminderOutcome.Created;
        }
        catch (Exception ex)
        {
            // لا نكسر العملية الأساسية للحوكمة بسبب فشل الإشعار.
            _logger.LogError(ex, "EmailNotification enqueue failed for {EventType} {EntityType}/{EntityId}",
                eventType, entityType, entityId);
            return ReportReminderOutcome.Error;
        }
    }

    private string BuildLink(string path)
    {
        var baseUrl = (_app.BaseUrl ?? string.Empty).TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl + path;
    }

    private static string BuildItemBody(GovernanceItem item, string link)
    {
        var lines = new List<string>
        {
            $"عنوان البند: {item.Title}",
            $"نطاق التطبيق: {ApplicationScopeLabel(item.ApplicationScope)}",
            $"درجة الأهمية: {SeverityLabel(item.Severity)}"
        };
        if (item.DueDate is { } due)
            lines.Add($"تاريخ الاستحقاق: {due:yyyy-MM-dd}");
        lines.Add("الإجراء المطلوب: مراجعة البند ومتابعته من خلال النظام.");
        return string.Join("\n", lines);
    }

    private static string BuildActionItemBody(GovernanceActionItem item, string link)
    {
        var lines = new List<string>
        {
            $"عنوان الإجراء: {item.Title}",
            $"الأولوية: {ActionItemPriorityLabel(item.Priority)}"
        };
        if (item.DueDate is { } due)
            lines.Add($"تاريخ الاستحقاق: {due:yyyy-MM-dd}");
        lines.Add("الإجراء المطلوب: تنفيذ الإجراء ومتابعته من خلال النظام.");
        return string.Join("\n", lines);
    }

    private static string BuildEscalationBody(GovernanceEscalation item, string link)
    {
        var lines = new List<string>
        {
            $"عنوان التصعيد: {item.Title}",
            $"درجة الأهمية: {EscalationSeverityLabel(item.Severity)}",
            "الإجراء المطلوب: مراجعة التصعيد من خلال النظام."
        };
        return string.Join("\n", lines);
    }

    private static string BuildItemUpdatedBody(GovernanceItem item, string changeKey, string link)
    {
        var lines = new List<string>
        {
            $"عنوان البند: {item.Title}",
            $"الحالة الحالية: {ItemStatusLabel(item.Status)}",
            $"درجة الأهمية: {SeverityLabel(item.Severity)}",
            "الإجراء المطلوب: مراجعة آخر تحديث على البند من خلال النظام."
        };
        return string.Join("\n", lines);
    }

    private static string BuildActionItemCompletedBody(GovernanceActionItem item, string link)
    {
        var lines = new List<string>
        {
            $"عنوان الإجراء: {item.Title}",
            $"الحالة: {ActionItemStatusLabel(item.Status)}"
        };
        if (!string.IsNullOrWhiteSpace(item.CompletionNote))
            lines.Add($"ملاحظة الإكمال: {item.CompletionNote}");
        lines.Add("الإجراء المطلوب: الاطّلاع على إغلاق الإجراء من خلال النظام.");
        return string.Join("\n", lines);
    }

    private static string BuildEscalationClosedBody(GovernanceEscalation item, string link)
    {
        var lines = new List<string>
        {
            $"عنوان التصعيد: {item.Title}",
            "الحالة: مغلق"
        };
        if (!string.IsNullOrWhiteSpace(item.Resolution))
            lines.Add($"ملخّص المعالجة: {item.Resolution}");
        lines.Add("الإجراء المطلوب: الاطّلاع على إغلاق التصعيد من خلال النظام.");
        return string.Join("\n", lines);
    }

    private static string BuildLeaveBody(LeaveRequest item, string link, string actionLine)
    {
        var lines = new List<string>
        {
            $"نوع الطلب: {LeaveTypeLabel(item.Type)}",
            $"من: {item.StartDate:yyyy-MM-dd} إلى: {item.EndDate:yyyy-MM-dd}"
        };
        if (!string.IsNullOrWhiteSpace(item.Reason))
            lines.Add($"السبب: {item.Reason}");
        lines.Add(actionLine);
        return string.Join("\n", lines);
    }

    private static string BuildHrRequestBody(EmployeeServiceRequest item, string link, string actionLine)
    {
        var lines = new List<string>
        {
            $"عنوان الطلب: {item.Title}",
            $"نوع الطلب: {HrRequestTypeLabel(item.RequestType)}",
            actionLine
        };
        return string.Join("\n", lines);
    }

    private static string SeverityLabel(GovernanceSeverity s) => s switch
    {
        GovernanceSeverity.Low => "منخفضة",
        GovernanceSeverity.Medium => "متوسطة",
        GovernanceSeverity.High => "عالية",
        GovernanceSeverity.Critical => "حرجة",
        _ => s.ToString()
    };

    private static string EscalationSeverityLabel(EscalationSeverity s) => s switch
    {
        EscalationSeverity.Low => "منخفضة",
        EscalationSeverity.Medium => "متوسطة",
        EscalationSeverity.High => "عالية",
        EscalationSeverity.Critical => "حرجة",
        _ => s.ToString()
    };

    private static string ActionItemPriorityLabel(ActionItemPriority p) => p switch
    {
        ActionItemPriority.Low => "منخفضة",
        ActionItemPriority.Medium => "متوسطة",
        ActionItemPriority.High => "عالية",
        ActionItemPriority.Critical => "حرجة",
        _ => p.ToString()
    };

    private static string ApplicationScopeLabel(GovernanceApplicationScope s) => s switch
    {
        GovernanceApplicationScope.Company => "كل الشركة",
        GovernanceApplicationScope.Department => "إدارة محددة",
        GovernanceApplicationScope.Team => "فريق محدد",
        GovernanceApplicationScope.User => "موظّف محدد",
        GovernanceApplicationScope.RelatedReport => "تقرير مرتبط",
        _ => s.ToString()
    };

    private static string ItemStatusLabel(GovernanceItemStatus s) => s switch
    {
        GovernanceItemStatus.Open => "مفتوح",
        GovernanceItemStatus.InReview => "قيد المراجعة",
        GovernanceItemStatus.Waiting => "بانتظار",
        GovernanceItemStatus.Resolved => "معالَج",
        GovernanceItemStatus.Closed => "مغلق",
        GovernanceItemStatus.Cancelled => "ملغى",
        _ => s.ToString()
    };

    private static string ActionItemStatusLabel(ActionItemStatus s) => s switch
    {
        ActionItemStatus.Open => "مفتوح",
        ActionItemStatus.InProgress => "قيد التنفيذ",
        ActionItemStatus.Blocked => "متوقّف",
        ActionItemStatus.Completed => "مكتمل",
        ActionItemStatus.Cancelled => "ملغى",
        _ => s.ToString()
    };

    private static string LeaveTypeLabel(LeaveRequestType t) => t switch
    {
        LeaveRequestType.Leave => "إجازة",
        LeaveRequestType.Permission => "استئذان",
        _ => t.ToString()
    };

    private static string HrRequestTypeLabel(EmployeeServiceRequestType t) => t switch
    {
        EmployeeServiceRequestType.HrLetter => "خطاب تعريف",
        EmployeeServiceRequestType.SalaryCertificate => "شهادة راتب",
        EmployeeServiceRequestType.ExperienceCertificate => "شهادة خبرة",
        EmployeeServiceRequestType.BankLetter => "خطاب بنك",
        EmployeeServiceRequestType.EmbassyLetter => "خطاب سفارة",
        EmployeeServiceRequestType.PersonalDataUpdate => "تحديث بيانات",
        EmployeeServiceRequestType.Other => "أخرى",
        _ => t.ToString()
    };
}

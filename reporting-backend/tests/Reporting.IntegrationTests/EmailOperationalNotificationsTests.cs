using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// إشعارات البريد التشغيلية (EMAIL-OPERATIONAL-NOTIFICATIONS-R1) — الأحداث الأحد عشر الآمنة (DryRun).
/// تتحقّق مباشرةً من محرّك IEmailNotificationService: المستلمون الصريحون، عناوين عربية صحيحة،
/// مفاتيح الترابط ومنع التكرار، استبعاد الفاعل (مَن أغلق/أكمل)، احترام الوضع Disabled/DryRun،
/// وعدم مساس صندوق الصادر القديم (email_outbox). لا SMTP، لا Enabled، لا Scheduler، لا كتابة إنتاج.
/// تُستبدَل IEmailSender بمُزيّف لا يتّصل خارجيًّا.
/// </summary>
[Collection("Integration")]
public class EmailOperationalNotificationsTests
{
    private const string TestBaseUrl = "https://reports.test.example";

    private readonly CustomWebApplicationFactory _factory;

    public EmailOperationalNotificationsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string? Name, string Subject, string Html)> Sent { get; } = new();
        public bool IsConfigured => true;
        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((toEmail, toName, subject, htmlBody));
            return Task.FromResult(EmailSendResult.Ok());
        }
    }

    private (WebApplicationFactory<Program> Factory, FakeEmailSender Sender) Build(string mode = "DryRun")
    {
        var sender = new FakeEmailSender();
        var f = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("EmailNotifications:Mode", mode);
            b.UseSetting("App:BaseUrl", TestBaseUrl);
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });
        return (f, sender);
    }

    private static async Task<Guid> UserAsync(WebApplicationFactory<Program> f)
    {
        var login = $"op-{Guid.NewGuid():N}@test.local";
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = login, Email = login, EmailConfirmed = true,
            FullName = "مستخدم تشغيليّ", IsActive = true
        };
        await users.CreateAsync(user, "Passw0rd#1");
        return user.Id;
    }

    private static async Task<T> Run<T>(WebApplicationFactory<Program> f, Func<IEmailNotificationService, Task<T>> action)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        return await action(svc);
    }

    private static async Task Run(WebApplicationFactory<Program> f, Func<IEmailNotificationService, Task> action)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        await action(svc);
    }

    private static async Task<List<EmailNotification>> RowsAsync(WebApplicationFactory<Program> f, Guid entityId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().Where(n => n.EntityId == entityId).ToListAsync();
    }

    private static async Task<int> OutboxCountAsync(WebApplicationFactory<Program> f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailOutbox.AsNoTracking().CountAsync();
    }

    private static GovernanceItem Item(Guid id, Guid createdBy, Guid? assignedTo) => new()
    {
        Id = id, Title = "بند حوكمة اختباري", Severity = GovernanceSeverity.High,
        ApplicationScope = GovernanceApplicationScope.Company, CreatedById = createdBy,
        AssignedToUserId = assignedTo, Status = GovernanceItemStatus.InReview, DueDate = new DateOnly(2026, 7, 15)
    };

    private static GovernanceActionItem ActionItem(Guid id, Guid createdBy, Guid? assignedBy, Guid? completedBy) => new()
    {
        Id = id, Title = "إجراء حوكمة اختباري", Priority = ActionItemPriority.High,
        CreatedByUserId = createdBy, AssignedByUserId = assignedBy, CompletedByUserId = completedBy,
        Status = ActionItemStatus.Completed, DueDate = new DateOnly(2026, 7, 20)
    };

    private static GovernanceEscalation Escalation(Guid id, Guid raisedBy, Guid? assignedTo, Guid? closedBy) => new()
    {
        Id = id, Title = "تصعيد اختباري", Severity = EscalationSeverity.High,
        RaisedByUserId = raisedBy, TargetType = EscalationTargetType.User,
        AssignedToUserId = assignedTo, ClosedByUserId = closedBy, Status = GovernanceEscalationStatus.Closed
    };

    private static LeaveRequest Leave(Guid id, Guid requester, Guid? hrReviewer = null, string? rejection = null) => new()
    {
        Id = id, RequesterUserId = requester, Type = LeaveRequestType.Leave,
        StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 3),
        Reason = "سبب اختباري", HrReviewerId = hrReviewer, RejectionReason = rejection
    };

    private static EmployeeServiceRequest HrReq(Guid id, Guid requester, Guid? assignedHr = null) => new()
    {
        Id = id, RequesterUserId = requester, RequestType = EmployeeServiceRequestType.HrLetter,
        Title = "خطاب تعريف", Status = EmployeeServiceRequestStatus.Completed, AssignedToHrUserId = assignedHr
    };

    // ===== Event 1 — تحديث بند حوكمة =====

    [Fact]
    public async Task ItemUpdated_AssigneeAndCreator_TwoRows()
    {
        var (f, _) = Build();
        var creator = await UserAsync(f);
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, creator, assignee), "status:Open->InReview"));

        var rows = await RowsAsync(f, id);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("governance-item-updated", r.EventType));
        Assert.All(rows, r => Assert.Equal("تم تحديث بند حوكمة مرتبط بك", r.Subject));
    }

    [Fact]
    public async Task ItemUpdated_CreatorIsAssignee_OneRow()
    {
        var (f, _) = Build();
        var same = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, same, same), "edited:x"));

        Assert.Single(await RowsAsync(f, id));
    }

    [Fact]
    public async Task ItemUpdated_DifferentChangeKey_NotDeduped()
    {
        var (f, _) = Build();
        var creator = await UserAsync(f);
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, creator, assignee), "status:Open->InReview"));
        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, creator, assignee), "status:InReview->Resolved"));

        // مفتاحان مختلفان لكلّ مستلم ⇒ 4 صفوف (تحديثان مختلفان لا يُحجَبان).
        Assert.Equal(4, (await RowsAsync(f, id)).Count);
    }

    [Fact]
    public async Task ItemUpdated_SameChangeKey_Deduped()
    {
        var (f, _) = Build();
        var creator = await UserAsync(f);
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, creator, assignee), "edited:same"));
        await Run(f, s => s.NotifyGovernanceItemUpdatedAsync(Item(id, creator, assignee), "edited:same"));

        Assert.Equal(2, (await RowsAsync(f, id)).Count);
    }

    // ===== Event 2 — إكمال إجراء حوكمة =====

    [Fact]
    public async Task ActionItemCompleted_CreatorAndOwner_ExcludeExecutor()
    {
        var (f, _) = Build();
        var creator = await UserAsync(f);
        var owner = await UserAsync(f);
        var executor = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceActionItemCompletedAsync(ActionItem(id, creator, owner, executor)));

        var rows = await RowsAsync(f, id);
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.RecipientUserId == executor);
        Assert.All(rows, r => Assert.Equal("تم إغلاق إجراء حوكمة", r.Subject));
    }

    [Fact]
    public async Task ActionItemCompleted_ExecutorIsCreator_NoSelfNotification()
    {
        var (f, _) = Build();
        var creator = await UserAsync(f);
        var id = Guid.NewGuid();

        // المنشئ نفسه هو مَن أغلق، ولا مُسنِد ⇒ لا مستلم.
        await Run(f, s => s.NotifyGovernanceActionItemCompletedAsync(ActionItem(id, creator, null, creator)));

        Assert.Empty(await RowsAsync(f, id));
    }

    // ===== Event 3 — إسناد تصعيد =====

    [Fact]
    public async Task EscalationAssigned_Assignee_CreatesRow()
    {
        var (f, _) = Build();
        var raiser = await UserAsync(f);
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceEscalationAssignedAsync(
            new GovernanceEscalation { Id = id, Title = "تصعيد", Severity = EscalationSeverity.High,
                RaisedByUserId = raiser, TargetType = EscalationTargetType.User, AssignedToUserId = assignee,
                Status = GovernanceEscalationStatus.Assigned }));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("governance-escalation-assigned", row.EventType);
        Assert.Equal("تم إسناد تصعيد إليك للمراجعة", row.Subject);
        Assert.Equal(assignee, row.RecipientUserId);
    }

    [Fact]
    public async Task EscalationAssigned_NoAssignee_NoRow()
    {
        var (f, _) = Build();
        var raiser = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceEscalationAssignedAsync(
            new GovernanceEscalation { Id = id, Title = "تصعيد", Severity = EscalationSeverity.High,
                RaisedByUserId = raiser, TargetType = EscalationTargetType.User, AssignedToUserId = null,
                Status = GovernanceEscalationStatus.Open }));

        Assert.Empty(await RowsAsync(f, id));
    }

    // ===== Event 4 — إغلاق تصعيد =====

    [Fact]
    public async Task EscalationClosed_RaiserAndAssignee_ExcludeCloser()
    {
        var (f, _) = Build();
        var raiser = await UserAsync(f);
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        // المُغلِق هو المُسنَد إليه ⇒ يبقى الرافع فقط.
        await Run(f, s => s.NotifyGovernanceEscalationClosedAsync(Escalation(id, raiser, assignee, assignee)));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal(raiser, row.RecipientUserId);
        Assert.Equal("تم إغلاق التصعيد", row.Subject);
    }

    [Fact]
    public async Task EscalationClosed_DoubleCall_Deduped()
    {
        var (f, _) = Build();
        var raiser = await UserAsync(f);
        var assignee = await UserAsync(f);
        var closer = await UserAsync(f);
        var id = Guid.NewGuid();

        // محاكاة التداخل بين CloseAsync و ChangeStatusAsync — نفس المفتاح يمنع التكرار.
        await Run(f, s => s.NotifyGovernanceEscalationClosedAsync(Escalation(id, raiser, assignee, closer)));
        await Run(f, s => s.NotifyGovernanceEscalationClosedAsync(Escalation(id, raiser, assignee, closer)));

        Assert.Equal(2, (await RowsAsync(f, id)).Count);
    }

    // ===== Event 5 — طلب إجازة جديد =====

    [Fact]
    public async Task LeaveCreated_Reviewers_CreatesRows()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var reviewer = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestCreatedAsync(Leave(id, requester), new[] { reviewer }));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("leave-request-created", row.EventType);
        Assert.Equal("طلب إجازة جديد يحتاج للمراجعة", row.Subject);
        Assert.Equal(reviewer, row.RecipientUserId);
    }

    [Fact]
    public async Task LeaveCreated_NoReviewers_NoRows()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestCreatedAsync(Leave(id, requester), Array.Empty<Guid>()));

        Assert.Empty(await RowsAsync(f, id));
    }

    // ===== Event 6 — طلب إجازة يحتاج إجراء HR =====

    [Fact]
    public async Task LeaveNeedsHr_HrUsers_CreatesRows()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var hr1 = await UserAsync(f);
        var hr2 = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestNeedsHrActionAsync(Leave(id, requester), new[] { hr1, hr2 }));

        var rows = await RowsAsync(f, id);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("طلب إجازة يحتاج لإجراء من الموارد البشرية", r.Subject));
    }

    // ===== Event 7 — اعتماد إجازة =====

    [Fact]
    public async Task LeaveApproved_Requester_CreatesRow()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var hr = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestApprovedAsync(Leave(id, requester, hrReviewer: hr)));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("leave-request-approved", row.EventType);
        Assert.Equal("تمت الموافقة على طلب الإجازة", row.Subject);
        Assert.Equal(requester, row.RecipientUserId);
    }

    // ===== Event 8 — رفض إجازة =====

    [Fact]
    public async Task LeaveRejected_Requester_ShowsReason()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestRejectedAsync(Leave(id, requester, rejection: "تعارض مع جدول العمل")));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("leave-request-rejected", row.EventType);
        Assert.Equal("تم رفض طلب الإجازة", row.Subject);
        Assert.Contains("تعارض مع جدول العمل", row.BodyText);
    }

    // ===== Event 9 — طلب موارد بشرية جديد =====

    [Fact]
    public async Task HrRequestCreated_HrUsers_CreatesRows()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var hr = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyHrRequestCreatedAsync(HrReq(id, requester), new[] { hr }));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("hr-request-created", row.EventType);
        Assert.Equal("طلب موارد بشرية جديد يحتاج للمراجعة", row.Subject);
    }

    // ===== Event 11 — إكمال طلب موارد بشرية =====

    [Fact]
    public async Task HrRequestCompleted_Requester_CreatesRow()
    {
        var (f, _) = Build();
        var requester = await UserAsync(f);
        var hr = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyHrRequestCompletedAsync(HrReq(id, requester, assignedHr: hr)));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("hr-request-completed", row.EventType);
        Assert.Equal("تم إغلاق طلب الموارد البشرية", row.Subject);
        Assert.Equal(requester, row.RecipientUserId);
    }

    // ===== إصلاح عنوان إعادة الإسناد =====

    [Fact]
    public async Task ActionItemReassign_UsesReassignSubject()
    {
        var (f, _) = Build();
        var assignee = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyGovernanceActionItemAssignedAsync(
            new GovernanceActionItem { Id = id, Title = "إجراء", Priority = ActionItemPriority.High,
                AssignedToUserId = assignee, AssignedByUserId = Guid.NewGuid(), CreatedByUserId = Guid.NewGuid(),
                DueDate = new DateOnly(2026, 7, 20) }, isReassign: true));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal("governance-action-item-reassigned", row.EventType);
        Assert.Equal("تم إعادة إسناد إجراء حوكمة إليك", row.Subject);
    }

    // ===== قواعد عامّة =====

    [Fact]
    public async Task Disabled_OperationalEvent_NoRows()
    {
        var (f, sender) = Build("Disabled");
        var requester = await UserAsync(f);
        var hr = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestNeedsHrActionAsync(Leave(id, requester), new[] { hr }));

        Assert.Empty(await RowsAsync(f, id));
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task DryRun_OperationalEvent_RowDryRun_NoSend()
    {
        var (f, sender) = Build("DryRun");
        var requester = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestApprovedAsync(Leave(id, requester)));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task OperationalEvent_DoesNotTouchOutbox()
    {
        var (f, _) = Build("DryRun");
        var requester = await UserAsync(f);
        var hr = await UserAsync(f);

        var before = await OutboxCountAsync(f);
        await Run(f, s => s.NotifyHrRequestCreatedAsync(HrReq(Guid.NewGuid(), requester), new[] { hr }));
        var after = await OutboxCountAsync(f);

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task LeaveApproved_DoubleCall_Deduped()
    {
        var (f, _) = Build("DryRun");
        var requester = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestApprovedAsync(Leave(id, requester)));
        await Run(f, s => s.NotifyLeaveRequestApprovedAsync(Leave(id, requester)));

        Assert.Single(await RowsAsync(f, id));
    }

    [Fact]
    public async Task BaseUrl_AppearsInOperationalBody()
    {
        var (f, _) = Build("DryRun");
        var requester = await UserAsync(f);
        var id = Guid.NewGuid();

        await Run(f, s => s.NotifyLeaveRequestApprovedAsync(Leave(id, requester)));

        var row = Assert.Single(await RowsAsync(f, id));
        Assert.Contains(TestBaseUrl + "/app/leave-requests", row.BodyHtml);
    }
}

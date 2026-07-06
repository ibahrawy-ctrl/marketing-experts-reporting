using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// إشعارات البريد المستقلّة (EMAIL-NOTIFICATIONS-R1) — جدول email_notifications منفصل تمامًا عن صندوق الصادر القديم.
/// تتحقّق من: الأوضاع الثلاثة (Disabled/DryRun/Enabled)، منع التكرار عبر CorrelationKey، أحداث الحوكمة الثلاثة،
/// تخطّي المستلم بلا بريد، ظهور رابط App:BaseUrl، عدم مساس email_outbox، وحصر سجلّ القراءة بالأدوار العليا.
/// تستبدل IEmailSender بمُزيّف يلتقط/يفشل/يرمي بلا أي اتصال خارجي.
/// </summary>
[Collection("Integration")]
public class EmailNotificationsTests
{
    private const string TestBaseUrl = "https://reports.test.example";

    private readonly CustomWebApplicationFactory _factory;

    public EmailNotificationsTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>مُرسِل بريد مزيّف — يلتقط/يفشل/يرمي حسب الإعداد بلا أي اتصال خارجي.</summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        public bool Configured { get; init; } = true;
        public bool Succeeds { get; init; } = true;
        public bool Throws { get; init; }
        public List<(string To, string? Name, string Subject, string Html)> Sent { get; } = new();

        public bool IsConfigured => Configured;

        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (Throws) throw new InvalidOperationException("فشل إرسال مزيّف للاختبار");
            Sent.Add((toEmail, toName, subject, htmlBody));
            return Task.FromResult(Succeeds ? EmailSendResult.Ok() : EmailSendResult.Fail("فشل مزيّف للاختبار"));
        }
    }

    private (WebApplicationFactory<Program> Factory, FakeEmailSender Sender) Build(
        string mode, bool senderConfigured = true, bool senderSucceeds = true, bool senderThrows = false)
    {
        var sender = new FakeEmailSender { Configured = senderConfigured, Succeeds = senderSucceeds, Throws = senderThrows };

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

    private static async Task<Guid> CreateRecipientAsync(WebApplicationFactory<Program> f, bool withEmail = true)
    {
        var login = $"recip-{Guid.NewGuid():N}@test.local";
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = login,
            Email = withEmail ? login : null,
            EmailConfirmed = true,
            FullName = "مستلِم اختبار",
            IsActive = true
        };
        await users.CreateAsync(user, "Passw0rd#1");
        return user.Id;
    }

    private static async Task NotifyItemAsync(WebApplicationFactory<Program> f, Guid itemId, Guid? assignedTo)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        var item = new GovernanceItem
        {
            Id = itemId,
            Title = "بند حوكمة اختباري",
            Severity = GovernanceSeverity.High,
            ApplicationScope = GovernanceApplicationScope.Company,
            CreatedById = Guid.NewGuid(),
            AssignedToUserId = assignedTo,
            DueDate = new DateOnly(2026, 7, 15)
        };
        await svc.NotifyGovernanceItemAssignedAsync(item);
    }

    private static async Task NotifyActionItemAsync(WebApplicationFactory<Program> f, Guid actionItemId, Guid? assignedTo, bool isReassign)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        var item = new GovernanceActionItem
        {
            Id = actionItemId,
            Title = "إجراء حوكمة اختباري",
            Priority = ActionItemPriority.Critical,
            AssignedToUserId = assignedTo,
            AssignedByUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            DueDate = new DateOnly(2026, 7, 20)
        };
        await svc.NotifyGovernanceActionItemAssignedAsync(item, isReassign);
    }

    private static async Task NotifyEscalationAsync(WebApplicationFactory<Program> f, Guid escalationId, EscalationTargetType targetType, Guid? targetUser)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        var item = new GovernanceEscalation
        {
            Id = escalationId,
            Title = "تصعيد اختباري",
            Severity = EscalationSeverity.High,
            RaisedByUserId = Guid.NewGuid(),
            TargetType = targetType,
            TargetUserId = targetUser
        };
        await svc.NotifyGovernanceEscalationCreatedAsync(item);
    }

    private static async Task<List<EmailNotification>> RowsForEntityAsync(WebApplicationFactory<Program> f, Guid entityId)
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

    private static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> f)
    {
        var c = f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    private static async Task<HttpClient> RoleClientAsync(WebApplicationFactory<Program> f, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FullName = $"مستخدم {role}", IsActive = true
            };
            await users.CreateAsync(u, password);
            await users.AddToRoleAsync(u, role);
        }
        var c = f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    // ===== 1. الأوضاع الثلاثة =====

    [Fact]
    public async Task DryRun_CreatesRow_StatusDryRun()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
        Assert.Equal(EmailNotificationMode.DryRun, row.Mode);
        Assert.Equal("governance-item-created", row.EventType);
    }

    [Fact]
    public async Task DryRun_DoesNotCallSender()
    {
        var (f, sender) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);

        await NotifyItemAsync(f, Guid.NewGuid(), recip);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Disabled_CreatesNothing()
    {
        var (f, sender) = Build("Disabled");
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        Assert.Empty(await RowsForEntityAsync(f, itemId));
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Enabled_ConfiguredSender_SendsAndMarksSent()
    {
        var (f, sender) = Build("Enabled", senderConfigured: true, senderSucceeds: true);
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Equal(EmailNotificationStatus.Sent, row.Status);
        Assert.Equal(EmailNotificationMode.Enabled, row.Mode);
        Assert.NotNull(row.SentAt);
        Assert.Equal(1, row.AttemptCount);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task Enabled_SenderNotConfigured_MarksFailed()
    {
        var (f, sender) = Build("Enabled", senderConfigured: false);
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Equal(EmailNotificationStatus.Failed, row.Status);
        Assert.Equal("sender_not_configured", row.FailureReason);
        Assert.Empty(sender.Sent);
    }

    // ===== 2. منع التكرار =====

    [Fact]
    public async Task CorrelationKey_PreventsDuplicates()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);
        await NotifyItemAsync(f, itemId, recip);

        Assert.Single(await RowsForEntityAsync(f, itemId));
    }

    // ===== 3. أحداث الحوكمة الثلاثة =====

    [Fact]
    public async Task Item_WithoutAssignee_NoNotification()
    {
        var (f, _) = Build("DryRun");
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, assignedTo: null);

        Assert.Empty(await RowsForEntityAsync(f, itemId));
    }

    [Fact]
    public async Task Item_WithAssignee_CreatesNotification()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Equal(recip, row.RecipientUserId);
        Assert.Equal("تم إسناد بند حوكمة جديد إليك", row.Subject);
        Assert.Equal($"governance-item-created:{itemId}:{recip}", row.CorrelationKey);
    }

    [Fact]
    public async Task ActionItem_Create_WithAssignee_CreatesAssignedNotification()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var actionItemId = Guid.NewGuid();

        await NotifyActionItemAsync(f, actionItemId, recip, isReassign: false);

        var row = Assert.Single(await RowsForEntityAsync(f, actionItemId));
        Assert.Equal("governance-action-item-assigned", row.EventType);
        Assert.Equal($"governance-action-item-assigned:{actionItemId}:{recip}", row.CorrelationKey);
    }

    [Fact]
    public async Task ActionItem_Reassign_UsesReassignedEvent_AndDedups()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var actionItemId = Guid.NewGuid();

        await NotifyActionItemAsync(f, actionItemId, recip, isReassign: true);
        await NotifyActionItemAsync(f, actionItemId, recip, isReassign: true);

        var row = Assert.Single(await RowsForEntityAsync(f, actionItemId));
        Assert.Equal("governance-action-item-reassigned", row.EventType);
    }

    [Fact]
    public async Task Escalation_UserTarget_CreatesNotification()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var escId = Guid.NewGuid();

        await NotifyEscalationAsync(f, escId, EscalationTargetType.User, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, escId));
        Assert.Equal("governance-escalation-created", row.EventType);
        Assert.Equal("تم إنشاء تصعيد جديد يحتاج للمراجعة", row.Subject);
    }

    [Fact]
    public async Task Escalation_NonUserTarget_NoNotification()
    {
        var (f, _) = Build("DryRun");
        var escId = Guid.NewGuid();

        await NotifyEscalationAsync(f, escId, EscalationTargetType.Department, targetUser: null);

        Assert.Empty(await RowsForEntityAsync(f, escId));
    }

    // ===== 4. المستلم والرابط =====

    [Fact]
    public async Task RecipientWithoutEmail_MarkedSkipped_NoSend()
    {
        var (f, sender) = Build("Enabled");
        var recip = await CreateRecipientAsync(f, withEmail: false);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Equal(EmailNotificationStatus.Skipped, row.Status);
        Assert.Equal("recipient_email_missing", row.FailureReason);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task BaseUrl_AppearsInBodyLink()
    {
        var (f, _) = Build("DryRun");
        var recip = await CreateRecipientAsync(f);
        var itemId = Guid.NewGuid();

        await NotifyItemAsync(f, itemId, recip);

        var row = Assert.Single(await RowsForEntityAsync(f, itemId));
        Assert.Contains(TestBaseUrl + "/app/governance-workspace", row.BodyHtml);
    }

    // ===== 5. العزل عن صندوق الصادر =====

    [Fact]
    public async Task DoesNotTouchEmailOutbox()
    {
        var (f, _) = Build("Enabled");
        var recip = await CreateRecipientAsync(f);

        var before = await OutboxCountAsync(f);
        await NotifyItemAsync(f, Guid.NewGuid(), recip);
        var after = await OutboxCountAsync(f);

        Assert.Equal(before, after);
    }

    // ===== 6. عدم كسر العملية الأساسية عند فشل الإشعار =====

    [Fact]
    public async Task NotificationFailure_DoesNotBreakActionItemCreation()
    {
        // المُرسِل يرمي استثناءً في وضع Enabled — يجب ألّا يكسر إنشاء إجراء الحوكمة.
        var (f, _) = Build("Enabled", senderConfigured: true, senderThrows: true);
        var admin = await AdminClientAsync(f);
        var recip = await CreateRecipientAsync(f);

        var req = new CreateGovernanceActionItemRequest(
            "إجراء غير قابل للكسر", "وصف", ActionItemPriority.High,
            ActionItemSourceType.Manual, null, recip, new DateOnly(2026, 7, 25));
        var res = await admin.PostAsJsonAsync("/api/governance/action-items", req, TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 7. سجلّ القراءة محصور بالأدوار العليا =====

    [Fact]
    public async Task ReadLog_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/email-notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ReadLog_Employee_403()
    {
        var (f, _) = Build("DryRun");
        var employee = await RoleClientAsync(f, Roles.Employee);
        var res = await employee.GetAsync("/api/email-notifications");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Ceo)]
    [InlineData(Roles.GeneralManager)]
    [InlineData(Roles.CeoSupport)]
    public async Task ReadLog_TopRoles_200(string role)
    {
        var (f, _) = Build("DryRun");
        var client = await RoleClientAsync(f, role);
        var res = await client.GetAsync("/api/email-notifications");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}

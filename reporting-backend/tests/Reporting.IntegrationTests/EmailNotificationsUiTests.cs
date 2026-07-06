using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// سطح مراجعة سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-UI-R1) — قراءة فقط تمامًا.
/// يتحقّق من: حصر الأدوار (Admin/CEO/GM/CeoSupport ⇒ 200؛ Employee/TeamLeader/Manager ⇒ 403؛ Anonymous ⇒ 401)،
/// التصفيح، الفلاتر (status/eventType/recipientUserId/search/dateFrom/dateTo)، endpoint التفاصيل بالمتن الكامل،
/// وعدم أي أثر جانبي (لا كتابة على email_notifications/email_outbox، لا استدعاء SMTP).
/// </summary>
[Collection("Integration")]
public class EmailNotificationsUiTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailNotificationsUiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> RoleClientAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";
        using (var scope = _factory.Services.CreateScope())
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
        var c = _factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    private async Task<Guid> SeedAsync(
        string eventType,
        EmailNotificationStatus status = EmailNotificationStatus.DryRun,
        Guid? recipientUserId = null,
        string? recipientEmail = null,
        string? recipientName = null,
        string subject = "عنوان اختباري",
        string bodyHtml = "<p>متن اختباري</p>",
        string? bodyText = null,
        string? correlationKey = null,
        DateTime? createdAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new EmailNotification
        {
            EventType = eventType,
            EntityType = "TestEntity",
            EntityId = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            RecipientName = recipientName,
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyText = bodyText,
            Status = status,
            Mode = EmailNotificationMode.DryRun,
            CorrelationKey = correlationKey ?? $"{eventType}:{Guid.NewGuid():N}",
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
        db.EmailNotifications.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<int> OutboxCountAsync(WebApplicationFactory<Program> f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailOutbox.AsNoTracking().CountAsync();
    }

    private static async Task<int> NotificationCountAsync(WebApplicationFactory<Program> f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync();
    }

    // ===== 1. حصر الأدوار على /log =====

    [Fact]
    public async Task Log_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/email-notifications/log");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Employee)]
    [InlineData(Roles.TeamLeader)]
    [InlineData(Roles.Manager)]
    public async Task Log_LowRoles_403(string role)
    {
        var client = await RoleClientAsync(role);
        var res = await client.GetAsync("/api/email-notifications/log");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Ceo)]
    [InlineData(Roles.GeneralManager)]
    [InlineData(Roles.CeoSupport)]
    public async Task Log_TopRoles_200(string role)
    {
        var client = await RoleClientAsync(role);
        var res = await client.GetAsync("/api/email-notifications/log");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 2. حصر الأدوار على التفاصيل =====

    [Fact]
    public async Task Details_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync($"/api/email-notifications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Details_Employee_403()
    {
        var client = await RoleClientAsync(Roles.Employee);
        var res = await client.GetAsync($"/api/email-notifications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 3. التصفيح =====

    [Fact]
    public async Task Log_Pagination_RespectsPageSize()
    {
        var evt = $"ui-page-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++) await SeedAsync(evt);

        var admin = await AdminClientAsync();
        var page1 = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&page=1&pageSize=2"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);
        var page3 = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&page=3&pageSize=2"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page3!.Items); // 5 صفوف ⇒ الصفحة 3 بحجم 2 فيها صفّ واحد
    }

    // ===== 4. الفلاتر =====

    [Fact]
    public async Task Log_FilterByStatus()
    {
        var evt = $"ui-status-{Guid.NewGuid():N}";
        await SeedAsync(evt, EmailNotificationStatus.DryRun);
        await SeedAsync(evt, EmailNotificationStatus.DryRun);
        await SeedAsync(evt, EmailNotificationStatus.Failed);

        var admin = await AdminClientAsync();
        var failed = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&status=Failed"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(1, failed!.TotalCount);
        Assert.All(failed.Items, r => Assert.Equal("Failed", r.Status));
    }

    [Fact]
    public async Task Log_FilterByEventType_IsolatesOthers()
    {
        var evtA = $"ui-evtA-{Guid.NewGuid():N}";
        var evtB = $"ui-evtB-{Guid.NewGuid():N}";
        await SeedAsync(evtA);
        await SeedAsync(evtA);
        await SeedAsync(evtB);

        var admin = await AdminClientAsync();
        var onlyA = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evtA}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(2, onlyA!.TotalCount);
        Assert.All(onlyA.Items, r => Assert.Equal(evtA, r.EventType));
    }

    [Fact]
    public async Task Log_FilterByRecipientUserId()
    {
        var evt = $"ui-recip-{Guid.NewGuid():N}";
        var target = Guid.NewGuid();
        await SeedAsync(evt, recipientUserId: target);
        await SeedAsync(evt, recipientUserId: Guid.NewGuid());

        var admin = await AdminClientAsync();
        var page = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&recipientUserId={target}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(target, page.Items[0].RecipientUserId);
    }

    [Fact]
    public async Task Log_Search_MatchesSubject()
    {
        var token = $"UISUBJ{Guid.NewGuid():N}";
        var evt = $"ui-search-{Guid.NewGuid():N}";
        await SeedAsync(evt, subject: $"موضوع يحوي {token}");
        await SeedAsync(evt, subject: "موضوع آخر بلا علامة");

        var admin = await AdminClientAsync();
        var page = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&search={token}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(1, page!.TotalCount);
        Assert.Contains(token, page.Items[0].Subject);
    }

    [Fact]
    public async Task Log_Search_MatchesRecipientAndCorrelationKey()
    {
        var evt = $"ui-search2-{Guid.NewGuid():N}";
        var emailToken = $"uifind{Guid.NewGuid():N}@test.local";
        var corrToken = $"UICORR{Guid.NewGuid():N}";
        await SeedAsync(evt, recipientEmail: emailToken);
        await SeedAsync(evt, correlationKey: corrToken);
        await SeedAsync(evt);

        var admin = await AdminClientAsync();
        var byEmail = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&search={emailToken}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);
        var byCorr = await (await admin.GetAsync($"/api/email-notifications/log?eventType={evt}&search={corrToken}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(1, byEmail!.TotalCount);
        Assert.Equal(1, byCorr!.TotalCount);
    }

    [Fact]
    public async Task Log_FilterByDateRange()
    {
        var evt = $"ui-date-{Guid.NewGuid():N}";
        await SeedAsync(evt, createdAtUtc: new DateTime(2000, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        await SeedAsync(evt, createdAtUtc: new DateTime(2000, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        await SeedAsync(evt, createdAtUtc: new DateTime(2000, 12, 10, 0, 0, 0, DateTimeKind.Utc));

        var admin = await AdminClientAsync();
        var page = await (await admin.GetAsync(
                $"/api/email-notifications/log?eventType={evt}&dateFrom=2000-03-01&dateTo=2000-09-01"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        Assert.Equal(1, page!.TotalCount);
    }

    [Fact]
    public async Task Log_Summary_CountsReflectSeededStatuses()
    {
        var evt = $"ui-sum-{Guid.NewGuid():N}";
        await SeedAsync(evt, EmailNotificationStatus.DryRun);
        await SeedAsync(evt, EmailNotificationStatus.Skipped);
        await SeedAsync(evt, EmailNotificationStatus.Failed);

        var admin = await AdminClientAsync();
        var page = await (await admin.GetAsync("/api/email-notifications/log?pageSize=1"))
            .Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);

        // الملخّص على كامل الجدول (مستقلّ عن الفلاتر) — لا بدّ أن يعكس الصفوف المزروعة على الأقل.
        Assert.True(page!.Summary.Total >= 3);
        Assert.True(page.Summary.DryRun >= 1);
        Assert.True(page.Summary.Skipped >= 1);
        Assert.True(page.Summary.Failed >= 1);
        Assert.NotNull(page.Summary.LastCreatedAtUtc);
    }

    // ===== 5. التفاصيل =====

    [Fact]
    public async Task Details_ReturnsFullBody()
    {
        var evt = $"ui-detail-{Guid.NewGuid():N}";
        var id = await SeedAsync(evt, bodyHtml: "<p>متن كامل للتفاصيل</p>", bodyText: "متن كامل للتفاصيل");

        var admin = await AdminClientAsync();
        var detail = await (await admin.GetAsync($"/api/email-notifications/{id}"))
            .Content.ReadFromJsonAsync<EmailNotificationLogDetailDto>(TestJson.Options);

        Assert.NotNull(detail);
        Assert.Equal(id, detail!.Id);
        Assert.Equal(evt, detail.EventType);
        Assert.Contains("متن كامل للتفاصيل", detail.BodyHtml);
        Assert.Equal("متن كامل للتفاصيل", detail.BodyText);
    }

    [Fact]
    public async Task Details_NotFound_404()
    {
        var admin = await AdminClientAsync();
        var res = await admin.GetAsync($"/api/email-notifications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 6. لا أثر جانبي =====

    [Fact]
    public async Task Log_DoesNotTouchOutbox_NorCreateRows()
    {
        await SeedAsync($"ui-noeffect-{Guid.NewGuid():N}");

        var admin = await AdminClientAsync();
        var outboxBefore = await OutboxCountAsync(_factory);
        var notifBefore = await NotificationCountAsync(_factory);

        var res = await admin.GetAsync("/api/email-notifications/log?pageSize=5");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        // نداء التفاصيل أيضًا قراءة فقط
        var page = await res.Content.ReadFromJsonAsync<EmailNotificationLogPageDto>(TestJson.Options);
        if (page!.Items.Count > 0)
        {
            var d = await admin.GetAsync($"/api/email-notifications/{page.Items[0].Id}");
            Assert.Equal(HttpStatusCode.OK, d.StatusCode);
        }

        Assert.Equal(outboxBefore, await OutboxCountAsync(_factory));
        Assert.Equal(notifBefore, await NotificationCountAsync(_factory)); // القراءة لا تُنشئ صفوفًا
    }
}

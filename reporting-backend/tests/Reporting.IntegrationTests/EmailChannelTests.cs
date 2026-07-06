using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reporting.Application.Auth;
using Reporting.Application.Notifications;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات قناة البريد (V1.0.3) — تتحقّق من السلوك الآمن محليًا بلا SMTP حقيقي:
/// البوابة العامة Email:Enabled، صندوق الصادر، إعادة المحاولة، الاستثناءات، ونقطة اختبار البريد.
/// تستبدل IEmailSender بمُزيّف يلتقط الرسائل، وتزيل الخدمة الخلفية لتشغيل دورة واحدة حتميًا.
/// </summary>
[Collection("Integration")]
public class EmailChannelTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailChannelTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>مُرسِل بريد مزيّف — يلتقط كل محاولة إرسال بلا أي اتصال خارجي.</summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        public bool Configured { get; init; } = true;
        public bool Succeeds { get; init; } = true;
        public List<(string To, string? Name, string Subject, string Html)> Sent { get; } = new();

        public bool IsConfigured => Configured;

        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((toEmail, toName, subject, htmlBody));
            return Task.FromResult(Succeeds ? EmailSendResult.Ok() : EmailSendResult.Fail("فشل مزيّف للاختبار"));
        }
    }

    private (WebApplicationFactory<Program> Factory, FakeEmailSender Sender) Build(
        bool enabled, bool senderSucceeds = true, bool senderConfigured = true,
        int maxAttempts = 5, string[]? excludedTypes = null, string[]? includedTypes = null)
    {
        var sender = new FakeEmailSender { Configured = senderConfigured, Succeeds = senderSucceeds };

        var f = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Email:Enabled", enabled ? "true" : "false");
            b.UseSetting("Email:Host", "smtp.test.local");
            b.UseSetting("Email:FromAddress", "noreply@test.local");
            b.UseSetting("Email:MaxAttempts", maxAttempts.ToString());
            b.UseSetting("Email:BackoffBaseSeconds", "60");
            if (excludedTypes != null)
                for (var i = 0; i < excludedTypes.Length; i++)
                    b.UseSetting($"Email:ExcludedTypes:{i}", excludedTypes[i]);
            if (includedTypes != null)
                for (var i = 0; i < includedTypes.Length; i++)
                    b.UseSetting($"Email:IncludedTypes:{i}", includedTypes[i]);

            b.ConfigureServices(services =>
            {
                // إزالة الخدمة الخلفية كي لا تعالج صندوق الصادر تلقائيًا أثناء الاختبار.
                var hosted = services.FirstOrDefault(s => s.ImplementationType == typeof(EmailOutboxDispatcher));
                if (hosted != null) services.Remove(hosted);

                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });

        return (f, sender);
    }

    private static async Task<(Guid Id, string Email)> CreateRecipientAsync(WebApplicationFactory<Program> f)
    {
        var email = $"recip-{Guid.NewGuid():N}@test.local";
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FullName = "مستلِم اختبار", IsActive = true
        };
        await users.CreateAsync(user, "Passw0rd#1");
        return (user.Id, email);
    }

    private static async Task NotifyAsync(WebApplicationFactory<Program> f, Guid recipientId, string type)
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await svc.NotifyAsync(recipientId, type, "عنوان الإشعار", "نص الإشعار", "/reports");
    }

    private static async Task<List<Reporting.Domain.Entities.System.EmailOutbox>> OutboxForAsync(WebApplicationFactory<Program> f, Guid recipientId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailOutbox.AsNoTracking().Where(m => m.RecipientId == recipientId).ToListAsync();
    }

    private static async Task RunDispatcherOnceAsync(WebApplicationFactory<Program> f)
    {
        var scopeFactory = f.Services.GetRequiredService<IServiceScopeFactory>();
        var opts = f.Services.GetRequiredService<IOptions<EmailOptions>>();
        var dispatcher = new EmailOutboxDispatcher(scopeFactory, opts, NullLogger<EmailOutboxDispatcher>.Instance);
        await dispatcher.RunOnceAsync();
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

    [Fact]
    public async Task Disabled_DoesNotEnqueueEmail()
    {
        var (f, _) = Build(enabled: false);
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "kpi.approved");

        var rows = await OutboxForAsync(f, recipId);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Enabled_EnqueuesPendingEmail()
    {
        var (f, _) = Build(enabled: true);
        var (recipId, email) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "kpi.approved");

        var rows = await OutboxForAsync(f, recipId);
        var row = Assert.Single(rows);
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal(email, row.ToEmail);
        Assert.Equal("kpi.approved", row.Type);
        Assert.Equal(0, row.Attempts);
    }

    [Fact]
    public async Task ExcludedType_IsNotEnqueued()
    {
        var (f, _) = Build(enabled: true, excludedTypes: new[] { "kpi.approved" });
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "kpi.approved");

        var rows = await OutboxForAsync(f, recipId);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task IncludedTypes_AllowsOnlyListedType()
    {
        // التفعيل المحدود: نوع مُدرَج في قائمة السماح يُرسَل بريدًا.
        var (f, _) = Build(enabled: true,
            includedTypes: new[] { "submission.returned", "submission.submitted" });
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "submission.returned");

        var rows = await OutboxForAsync(f, recipId);
        var row = Assert.Single(rows);
        Assert.Equal("submission.returned", row.Type);
    }

    [Fact]
    public async Task IncludedTypes_BlocksUnlistedType()
    {
        // نوع خارج قائمة السماح يظل إشعارًا داخل التطبيق فقط بلا بريد.
        var (f, _) = Build(enabled: true,
            includedTypes: new[] { "submission.returned", "submission.submitted" });
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "kpi.approved");

        var rows = await OutboxForAsync(f, recipId);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Dispatcher_OnSuccess_SendsAndMarksSent()
    {
        var (f, sender) = Build(enabled: true, senderSucceeds: true);
        var (recipId, email) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "submission.submitted");
        await RunDispatcherOnceAsync(f);

        Assert.Contains(sender.Sent, m => m.To == email);

        var rows = await OutboxForAsync(f, recipId);
        var row = Assert.Single(rows);
        Assert.Equal(EmailOutboxStatus.Sent, row.Status);
        Assert.NotNull(row.SentAtUtc);
        Assert.Equal(1, row.Attempts);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task Dispatcher_OnFailure_StaysPending_WithBackoff()
    {
        var (f, _) = Build(enabled: true, senderSucceeds: false, maxAttempts: 5);
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "submission.returned");
        await RunDispatcherOnceAsync(f);

        var rows = await OutboxForAsync(f, recipId);
        var row = Assert.Single(rows);
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.NotNull(row.LastError);
        Assert.True(row.NextAttemptUtc > DateTime.UtcNow.AddSeconds(30));
    }

    [Fact]
    public async Task Dispatcher_OnFailure_AtMaxAttempts_MarksFailed()
    {
        var (f, _) = Build(enabled: true, senderSucceeds: false, maxAttempts: 1);
        var (recipId, _) = await CreateRecipientAsync(f);

        await NotifyAsync(f, recipId, "submission.returned");
        await RunDispatcherOnceAsync(f);

        var rows = await OutboxForAsync(f, recipId);
        var row = Assert.Single(rows);
        Assert.Equal(EmailOutboxStatus.Failed, row.Status);
        Assert.Equal(1, row.Attempts);
    }

    [Fact]
    public async Task TestEmail_Admin_SendsOneMessage_EvenWhenDisabled()
    {
        // البوابة العامة معطّلة، لكن نقطة الاختبار ترسل رسالة واحدة مباشرةً — تمامًا كما يتطلّب سيناريو النشر.
        var (f, sender) = Build(enabled: false, senderSucceeds: true, senderConfigured: true);
        var admin = await AdminClientAsync(f);

        var res = await admin.PostAsJsonAsync("/api/notifications/test-email",
            new { toEmail = "internal@test.local" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Single(sender.Sent);
        Assert.Equal("internal@test.local", sender.Sent[0].To);
    }

    [Fact]
    public async Task TestEmail_NonAdmin_IsForbidden()
    {
        var (f, sender) = Build(enabled: false);
        var employee = await RoleClientAsync(f, "Employee");

        var res = await employee.PostAsJsonAsync("/api/notifications/test-email",
            new { toEmail = "internal@test.local" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task TestEmail_WhenChannelNotConfigured_Returns503()
    {
        var (f, sender) = Build(enabled: false, senderConfigured: false);
        var admin = await AdminClientAsync(f);

        var res = await admin.PostAsJsonAsync("/api/notifications/test-email",
            new { toEmail = "internal@test.local" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task TestEmail_MissingAddress_Returns400()
    {
        var (f, _) = Build(enabled: false);
        var admin = await AdminClientAsync(f);

        var res = await admin.PostAsJsonAsync("/api/notifications/test-email",
            new { toEmail = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

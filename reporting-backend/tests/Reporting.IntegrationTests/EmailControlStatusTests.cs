using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — اختبارات الحالة التشغيليّة الحيّة لقناة البريد.
///
/// تُثبت: الأوضاع الثلاثة، أنّ العلم القديم Email__Enabled لا يقلب الوضع، أنّ سجلّات DryRun
/// التاريخيّة لا تُغيّر الوضع الحاليّ، حالتَي المجدول، حالتَي SMTP، أنّ CredentialConfigured قيمة
/// منطقيّة لا قيمة سرّ، صحّة العدّادات وصفريّتها، الطوابع الزمنيّة القابلة للإفراغ، توليد التنبيهات،
/// الصلاحيّات (Admin 200 / غير Admin 403 / Anonymous 401)، خلوّ الاستجابة من أيّ سرّ،
/// وأنّ المسار قراءة فقط بالكامل (بلا كتابة، بلا إرسال SMTP، بلا استدعاء مهمّة).
///
/// العزل: كلّ اختبار عدّادات يعمل داخل معاملة تُفرَّغ فيها جداول البريد ثمّ **تُلغى** (Rollback)،
/// فلا يتغيّر شيء في قاعدة الاختبار المشتركة.
/// </summary>
[Collection("Integration")]
public class EmailControlStatusTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailControlStatusTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    /// <summary>مُرسِل وهميّ: يكشف الجاهزيّة فقط ويعدّ محاولات الإرسال (يجب أن تبقى صفرًا).</summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly bool _configured;
        public FakeEmailSender(bool configured) => _configured = configured;
        public int SendCalls { get; private set; }
        public bool IsConfigured => _configured;
        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            SendCalls++;
            return Task.FromResult(EmailSendResult.Ok());
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Reporting.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeClock : ISystemClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public DateTimeOffset UtcNow { get; }
    }

    /// <summary>إعدادات يفشل ربطها — تحاكي قيمة غير صالحة في EmailNotifications__Mode.</summary>
    private sealed class ThrowingOptions<T> : IOptions<T> where T : class
    {
        public T Value => throw new InvalidOperationException("mode binding failed");
    }

    private static EmailControlStatusService BuildService(
        AppDbContext db,
        EmailNotificationMode? mode = EmailNotificationMode.DryRun,
        EmailOptions? email = null,
        ReportReminderSchedulerOptions? scheduler = null,
        FakeEmailSender? sender = null,
        string environmentName = "Testing",
        DateTime? now = null)
    {
        IOptions<EmailNotificationOptions> modeOptions = mode is null
            ? new ThrowingOptions<EmailNotificationOptions>()
            : Options.Create(new EmailNotificationOptions { Mode = mode.Value });

        return new EmailControlStatusService(
            db,
            modeOptions,
            Options.Create(email ?? ConfiguredEmail()),
            Options.Create(scheduler ?? new ReportReminderSchedulerOptions { Enabled = true }),
            sender ?? new FakeEmailSender(true),
            new FakeHostEnvironment { EnvironmentName = environmentName },
            new FakeClock(now ?? DateTime.UtcNow));
    }

    /// <summary>إعدادات بريد مُهيّأة بالكامل — قيم اختبار لا علاقة لها بأيّ بيئة حقيقيّة.</summary>
    private static EmailOptions ConfiguredEmail(bool legacyEnabled = false, bool withPassword = true) => new()
    {
        Enabled = legacyEnabled,
        SmtpHost = "smtp.test.local",
        SmtpPort = 587,
        UseStartTls = true,
        FromEmail = "no-reply@test.local",
        Password = withPassword ? "test-only-not-a-real-secret" : string.Empty
    };

    /// <summary>
    /// يُفرغ جدولَي البريد داخل معاملة، ينفّذ الاختبار، ثمّ يُلغي المعاملة بالكامل.
    ///
    /// يُستعمَل <c>TRUNCATE</c> لا <c>DELETE</c>: قاعدة الاختبار المشتركة تحوي مئات آلاف الصفوف
    /// (بضع مئات من الميغابايت) فيتجاوز <c>DELETE</c> مهلة الأمر الافتراضيّة (30 ثانية).
    /// <c>TRUNCATE</c> في PostgreSQL **معامَلاتيّ** ⇒ يُعاد كلّ شيء عند <c>RollbackAsync</c>،
    /// ولا يمسّ أيّ جدول آخر (لا تابع FK لهذين الجدولين).
    /// اختبارات HTTP الثلاثة لا تستعمل هذا المُساعِد فلا تتصادم مع قفل TRUNCATE.
    /// </summary>
    private async Task IsolatedAsync(Func<AppDbContext, Task> body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE email_notifications, email_outbox");
            await body(db);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    private static EmailNotification Notification(
        EmailNotificationMode mode,
        EmailNotificationStatus status,
        DateTime createdAt,
        int attemptCount = 0,
        string eventType = "status.test.event",
        DateTime? sentAt = null,
        DateTime? failedAt = null) => new()
        {
            EventType = eventType,
            EntityType = "StatusTest",
            EntityId = Guid.NewGuid(),
            RecipientEmail = "recipient@test.local",
            Subject = "اختبار الحالة",
            BodyHtml = "<p>اختبار</p>",
            Status = status,
            Mode = mode,
            AttemptCount = attemptCount,
            SentAt = sentAt,
            FailedAt = failedAt,
            CreatedAtUtc = createdAt,
            CorrelationKey = $"status-test:{Guid.NewGuid():N}"
        };

    private static EmailOutbox Outbox(EmailOutboxStatus status) => new()
    {
        RecipientId = Guid.NewGuid(),
        ToEmail = "recipient@test.local",
        ToName = "مستلم",
        Subject = "اختبار الصادر",
        HtmlBody = "<p>اختبار</p>",
        Type = "status.test",
        Status = status
    };

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

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

    private static bool HasWarning(EmailControlCenterStatusDto dto, string code)
        => dto.Warnings.Any(w => w.Code == code);

    // ===== 1-3: الأوضاع الثلاثة =====

    [Fact]
    public async Task Mode_Enabled_MarksLiveSending()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled).GetStatusAsync();
            Assert.Equal("Enabled", dto.Mode);
            Assert.True(dto.IsLiveSendingEnabled);
        });
    }

    [Fact]
    public async Task Mode_DryRun_MarksSimulation()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.DryRun).GetStatusAsync();
            Assert.Equal("DryRun", dto.Mode);
            Assert.False(dto.IsLiveSendingEnabled);
        });
    }

    [Fact]
    public async Task Mode_Disabled_MarksDisabled()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Disabled).GetStatusAsync();
            Assert.Equal("Disabled", dto.Mode);
            Assert.False(dto.IsLiveSendingEnabled);
        });
    }

    // ===== 4: Enabled مع العلم القديم معطَّل =====

    [Fact]
    public async Task Mode_Enabled_WithLegacyFlagDisabled_StaysLive_AndLegacyIsNotAuthoritative()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                email: ConfiguredEmail(legacyEnabled: false)).GetStatusAsync();

            Assert.True(dto.IsLiveSendingEnabled);
            Assert.False(dto.LegacyEmailEnabled);
            Assert.False(dto.LegacyFlagIsAuthoritative);
            // العلم القديم المعطَّل لا يُنتج أيّ تنبيه حرِج
            Assert.DoesNotContain(dto.Warnings, w => w.Severity == EmailControlStatusSeverity.Critical);
            Assert.True(HasWarning(dto, "legacy_flag_not_authoritative"));
        });
    }

    // ===== 5: السجلّات التاريخيّة لا تغيّر الوضع =====

    [Fact]
    public async Task HistoricalDryRunRows_DoNotAffectCurrentMode()
    {
        await IsolatedAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.EmailNotifications.AddRange(
                Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddDays(-3)),
                Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddDays(-2)));
            await db.SaveChangesAsync();

            var dto = await BuildService(db, EmailNotificationMode.Enabled, now: now).GetStatusAsync();

            Assert.Equal("Enabled", dto.Mode);
            Assert.True(dto.IsLiveSendingEnabled);
            Assert.Equal(2, dto.HistoricalDryRunCount);
            var info = dto.Warnings.Single(w => w.Code == "historical_dryrun_records");
            Assert.Equal(EmailControlStatusSeverity.Info, info.Severity);
        });
    }

    // ===== 6-7: المجدول =====

    [Fact]
    public async Task Scheduler_Enabled_EmitsNoSchedulerWarning()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                scheduler: new ReportReminderSchedulerOptions
                {
                    Enabled = true, PollMinutes = 15,
                    DailyDueHour = 16, WeeklyDueHour = 9, OverdueHour = 9, SummaryHour = 9, ReviewHour = 9
                }).GetStatusAsync();

            Assert.True(dto.SchedulerEnabled);
            Assert.Equal(15, dto.PollMinutes);
            Assert.Equal(16, dto.DailyDueHour);
            Assert.Equal(9, dto.WeeklyDueHour);
            Assert.Equal("Asia/Riyadh", dto.TimeZoneId);
            Assert.False(HasWarning(dto, "scheduler_disabled"));
        });
    }

    [Fact]
    public async Task Scheduler_Disabled_EmitsSchedulerWarning()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.DryRun,
                scheduler: new ReportReminderSchedulerOptions { Enabled = false }).GetStatusAsync();

            Assert.False(dto.SchedulerEnabled);
            var w = dto.Warnings.Single(x => x.Code == "scheduler_disabled");
            Assert.Equal(EmailControlStatusSeverity.Warning, w.Severity);
        });
    }

    // ===== 8-9: جاهزيّة SMTP =====

    [Fact]
    public async Task Smtp_Configured_EmitsNoSmtpCritical()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                sender: new FakeEmailSender(true)).GetStatusAsync();

            Assert.True(dto.SmtpConfigured);
            Assert.Equal("smtp.test.local", dto.SmtpHost);
            Assert.Equal(587, dto.SmtpPort);
            Assert.True(dto.UsesTls);
            Assert.Equal("no-reply@test.local", dto.SenderAddress);
            Assert.False(HasWarning(dto, "live_without_smtp"));
        });
    }

    [Fact]
    public async Task Smtp_NotConfigured_WhileLive_EmitsCritical()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                email: new EmailOptions { Password = "x" },
                sender: new FakeEmailSender(false)).GetStatusAsync();

            Assert.False(dto.SmtpConfigured);
            Assert.Null(dto.SmtpHost);
            Assert.Null(dto.SmtpPort);
            Assert.Null(dto.SenderAddress);
            var w = dto.Warnings.Single(x => x.Code == "live_without_smtp");
            Assert.Equal(EmailControlStatusSeverity.Critical, w.Severity);
        });
    }

    // ===== 10-11: بيانات الاعتماد =====

    [Fact]
    public async Task Credential_Present_IsBooleanOnly_AndNeverExposesValue()
    {
        await IsolatedAsync(async db =>
        {
            var email = ConfiguredEmail(withPassword: true);
            var dto = await BuildService(db, EmailNotificationMode.Enabled, email: email).GetStatusAsync();

            Assert.True(dto.CredentialConfigured);
            // القيمة المعروضة قيمة منطقيّة فقط: لا السرّ ولا أيّ جزء منه في الحمولة
            var json = System.Text.Json.JsonSerializer.Serialize(dto);
            Assert.DoesNotContain(email.Password, json, StringComparison.Ordinal);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Credential_Missing_WhileLive_EmitsCritical()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                email: ConfiguredEmail(withPassword: false)).GetStatusAsync();

            Assert.False(dto.CredentialConfigured);
            var w = dto.Warnings.Single(x => x.Code == "live_without_credential");
            Assert.Equal(EmailControlStatusSeverity.Critical, w.Severity);
        });
    }

    // ===== 12-13: العدّادات =====

    [Fact]
    public async Task Counters_MatchSeededRows_WithoutDoubleCounting()
    {
        await IsolatedAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.EmailNotifications.AddRange(
                Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddMinutes(-30)),
                Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddMinutes(-29)),
                Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddMinutes(-28)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Sent, now.AddMinutes(-20), sentAt: now.AddMinutes(-20)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Sent, now.AddMinutes(-19), sentAt: now.AddMinutes(-19)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Failed, now.AddMinutes(-18), failedAt: now.AddMinutes(-18)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Pending, now.AddMinutes(-5)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Pending, now.AddMinutes(-4), attemptCount: 2));
            db.EmailOutbox.AddRange(
                Outbox(EmailOutboxStatus.Pending),
                Outbox(EmailOutboxStatus.Pending),
                Outbox(EmailOutboxStatus.Sent));
            await db.SaveChangesAsync();

            var dto = await BuildService(db, EmailNotificationMode.Enabled, now: now).GetStatusAsync();

            Assert.Equal(8, dto.TotalNotifications);
            Assert.Equal(3, dto.HistoricalDryRunCount);   // عمود Mode
            Assert.Equal(5, dto.EnabledCount);            // عمود Mode
            Assert.Equal(2, dto.SentCount);               // عمود Status
            Assert.Equal(1, dto.PendingCount);            // Pending بلا محاولات
            Assert.Equal(1, dto.ProcessingCount);         // Pending مع محاولات
            Assert.Equal(1, dto.FailedCount);
            Assert.Equal(2, dto.OutboxCount);             // المعلّق فقط
            // عمودا Mode و Status مستقلّان: مجموع كلّ منهما = الإجمالي بلا ازدواج
            Assert.Equal(dto.TotalNotifications, dto.HistoricalDryRunCount + dto.EnabledCount);
        });
    }

    [Fact]
    public async Task Counters_AreZero_OnEmptyTables()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.DryRun).GetStatusAsync();

            Assert.Equal(0, dto.TotalNotifications);
            Assert.Equal(0, dto.HistoricalDryRunCount);
            Assert.Equal(0, dto.EnabledCount);
            Assert.Equal(0, dto.SentCount);
            Assert.Equal(0, dto.PendingCount);
            Assert.Equal(0, dto.ProcessingCount);
            Assert.Equal(0, dto.FailedCount);
            Assert.Equal(0, dto.OutboxCount);
            Assert.False(HasWarning(dto, "historical_dryrun_records"));
            Assert.False(HasWarning(dto, "failed_notifications"));
            Assert.False(HasWarning(dto, "outbox_backlog"));
        });
    }

    // ===== 14-15: الطوابع الزمنيّة =====

    [Fact]
    public async Task Timestamps_AreNull_OnEmptyTables()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, EmailNotificationMode.DryRun).GetStatusAsync();

            Assert.Null(dto.LastNotificationCreatedAtUtc);
            Assert.Null(dto.LastSentAtUtc);
            Assert.Null(dto.LastFailureAtUtc);
            Assert.Null(dto.LastScheduledNotificationCreatedAtUtc);
            Assert.NotEqual(default, dto.CheckedAtUtc);
        });
    }

    [Fact]
    public async Task Timestamps_ReflectLatestRows()
    {
        await IsolatedAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var created = now.AddMinutes(-3);
            var sent = now.AddMinutes(-10);
            var failed = now.AddMinutes(-7);
            var scheduled = now.AddMinutes(-6);

            db.EmailNotifications.AddRange(
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Sent, now.AddMinutes(-10), sentAt: sent),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Failed, failed, failedAt: failed),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Sent, scheduled,
                    eventType: ScheduledReminderEventTypes.DailyDue, sentAt: scheduled),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Sent, created, sentAt: created));
            await db.SaveChangesAsync();

            var dto = await BuildService(db, EmailNotificationMode.Enabled, now: now).GetStatusAsync();

            Assert.Equal(created, dto.LastNotificationCreatedAtUtc!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal(created, dto.LastSentAtUtc!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal(failed, dto.LastFailureAtUtc!.Value, TimeSpan.FromSeconds(1));
            // آخر إشعار من فئة مجدوَلة — وليس «آخر تشغيل للمجدول»
            Assert.Equal(scheduled, dto.LastScheduledNotificationCreatedAtUtc!.Value, TimeSpan.FromSeconds(1));
        });
    }

    // ===== 16: توليد التنبيهات =====

    [Fact]
    public async Task FailedRows_OutboxBacklog_AndStuckPending_EmitWarnings()
    {
        await IsolatedAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.EmailNotifications.AddRange(
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Failed, now.AddMinutes(-10), failedAt: now.AddMinutes(-10)),
                Notification(EmailNotificationMode.Enabled, EmailNotificationStatus.Pending, now.AddHours(-3)));
            db.EmailOutbox.Add(Outbox(EmailOutboxStatus.Pending));
            await db.SaveChangesAsync();

            var dto = await BuildService(db, EmailNotificationMode.Enabled,
                email: ConfiguredEmail(legacyEnabled: false), now: now).GetStatusAsync();

            Assert.Equal(EmailControlStatusSeverity.Critical, dto.Warnings.Single(w => w.Code == "failed_notifications").Severity);
            Assert.Equal(EmailControlStatusSeverity.Critical, dto.Warnings.Single(w => w.Code == "stuck_pending").Severity);
            Assert.Equal(EmailControlStatusSeverity.Warning, dto.Warnings.Single(w => w.Code == "outbox_backlog").Severity);
            Assert.Equal(EmailControlStatusSeverity.Warning, dto.Warnings.Single(w => w.Code == "legacy_disabled_with_backlog").Severity);
        });
    }

    // ===== 17: ربط وضع غير صالح =====

    [Fact]
    public async Task InvalidModeBinding_DoesNotThrow_AndEmitsCritical()
    {
        await IsolatedAsync(async db =>
        {
            var dto = await BuildService(db, mode: null).GetStatusAsync();

            Assert.Equal("Invalid", dto.Mode);
            Assert.False(dto.IsLiveSendingEnabled);
            Assert.Equal(EmailControlStatusSeverity.Critical, dto.Warnings.Single(w => w.Code == "mode_invalid").Severity);
        });
    }

    // ===== 18-19: قراءة فقط + أداء =====

    [Fact]
    public async Task GetStatus_WritesNothing_AndSendsNoEmail()
    {
        await IsolatedAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.EmailNotifications.Add(Notification(EmailNotificationMode.DryRun, EmailNotificationStatus.DryRun, now.AddMinutes(-1)));
            db.EmailOutbox.Add(Outbox(EmailOutboxStatus.Pending));
            await db.SaveChangesAsync();

            var notificationsBefore = await db.EmailNotifications.CountAsync();
            var outboxBefore = await db.EmailOutbox.CountAsync();
            var sender = new FakeEmailSender(true);

            await BuildService(db, EmailNotificationMode.Enabled, sender: sender, now: now).GetStatusAsync();

            Assert.Equal(notificationsBefore, await db.EmailNotifications.CountAsync());
            Assert.Equal(outboxBefore, await db.EmailOutbox.CountAsync());
            // بلا كيانات متعقَّبة ⇒ لا شيء يمكن أن يُحفَظ لاحقًا (AsNoTracking في كلّ الاستعلامات)
            Assert.Empty(db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
            // بلا اتّصال SMTP وبلا إرسال، وبلا استدعاء أيّ مهمّة مجدوَلة (لا صفوف جديدة)
            Assert.Equal(0, sender.SendCalls);
        });
    }

    [Fact]
    public async Task GetStatus_CompletesWithinReasonableTime()
    {
        await IsolatedAsync(async db =>
        {
            var service = BuildService(db, EmailNotificationMode.Enabled);
            await service.GetStatusAsync(); // إحماء الاتّصال/الخطّة

            var sw = Stopwatch.StartNew();
            await service.GetStatusAsync();
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 3000, $"استغرق الاستعلام {sw.ElapsedMilliseconds}ms");
        });
    }

    // ===== 20-22: الصلاحيّات وخلوّ الاستجابة من الأسرار =====

    [Fact]
    public async Task Http_Admin_Returns200_AndJsonHasNoSecrets()
    {
        var c = await AdminClientAsync();
        var res = await c.GetAsync("/api/email-control/status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"mode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"credentialConfigured\"", json, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "password", "secret", "connectionstring", "apikey",
                     "accesstoken", "refreshtoken", "apppassword", "smtppassword",
                     "jwt", "Host=", "Username=", "Pwd="
                 })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Http_NonAdmin_Returns403()
    {
        foreach (var role in new[] { Roles.Employee, Roles.Manager })
        {
            var c = await RoleClientAsync(role);
            var res = await c.GetAsync("/api/email-control/status");
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task Http_Anonymous_Returns401()
    {
        var c = _factory.CreateClient();
        var res = await c.GetAsync("/api/email-control/status");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

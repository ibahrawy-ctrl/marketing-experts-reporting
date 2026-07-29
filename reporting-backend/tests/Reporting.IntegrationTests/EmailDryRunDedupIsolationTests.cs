using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// EMAIL-DRYRUN-DEDUPLICATION-ISOLATION-R1 — عزل مفاتيح المحاكاة عن مفاتيح التسليم.
///
/// العيب المُثبَت: كان فحص التكرار في <c>EmailNotificationService.EnqueueAsync</c> يقع **قبل** التفرّع على
/// الوضع، ويقارن المفتاح وحده بلا نظر إلى الوضع أو الحالة. فصفّ <c>DryRun</c> — وهو محاكاة لا تسليم —
/// يحجز مفتاح الترابط حجزًا دائمًا (والفهرس الفريد في القاعدة على العمود وحده يُثبِّت الحجز)، فيُصنَّف أوّل
/// إرسال فعليّ لاحق «مكرّرًا» ولا يُرسَل أبدًا.
///
/// الإصلاح المعتمَد (الخيار C — عزل غير متماثل): صفوف <c>DryRun</c> وحدها تُخزَّن وتُفحَص في فضاء أسماء
/// خاصّ (<see cref="EmailNotificationService.DryRunCorrelationKeyPrefix"/>)، بينما تبقى مفاتيح <c>Enabled</c>
/// قانونيّة كما هي — فتظلّ الرسائل المُرسَلة فعليًّا حاجزةً لأيّ إرسال مكرّر. بلا Migration وبلا مساس بأيّ صفّ تاريخيّ.
///
/// عزل الاختبارات على قاعدة <c>reporting_test</c> المشتركة الدائمة (نفس نهج SplitDeliveryWindowsTests):
/// دورة محاكاة بسنة مرساة مستقلّة لكلّ اختبار، وكلّ عدّ محصور بمستخدمي الاختبار نفسه، وتنظيف مضمون في
/// <see cref="DisposeAsync"/>. الساعة محقونة دائمًا ⇒ حتميّة تامّة. لا SMTP حقيقيّ إطلاقًا: كلّ مُرسِل هنا وهميّ.
/// </summary>
[Collection("Integration")]
public class EmailDryRunDedupIsolationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailDryRunDedupIsolationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    /// <summary>مُرسِل وهميّ ناجح يَعُدّ الاستدعاءات ويسجّل المستلمين.</summary>
    private sealed class CountingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }
        public List<string> Recipients { get; } = new();
        public bool IsConfigured => true;
        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            SendCount++;
            Recipients.Add(toEmail);
            return Task.FromResult(EmailSendResult.Ok());
        }
    }

    /// <summary>مُرسِل وهميّ يفشل دائمًا — لإثبات أنّ الفشل لا يفتح بابًا لتكرار غير منضبط.</summary>
    private sealed class FailingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }
        public List<string> Recipients { get; } = new();
        public bool IsConfigured => true;
        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            SendCount++;
            Recipients.Add(toEmail);
            return Task.FromResult(EmailSendResult.Fail("smtp_unavailable"));
        }
    }

    // ===== دورة محاكاة معزولة لكلّ اختبار =====

    private sealed class SimCycle
    {
        public SimCycle(int anchorYear) => Start = ReportCalendarPolicy.WeekStart(new DateOnly(anchorYear, 5, 17));
        public DateOnly Start { get; }
        public string Key => ReportCalendarPolicy.WeekKeyFor(Start);
        public DateOnly Day(int offsetFromStart) => Start.AddDays(offsetFromStart);
        public string YearSegment => $"{Start.Year}-";
    }

    private SimCycle _cycle = new(2061);

    private SimCycle UseCycle(int anchorYear) => _cycle = new SimCycle(anchorYear);

    private static string DayKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTimeOffset RiyadhMoment(DateOnly day, int hour) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero) - ReportCalendarPolicy.RiyadhOffset;

    /// <summary>الإعداد الإنتاجيّ المعتمَد للنوافذ (Daily=16، والبقيّة=9، Poll=15د).</summary>
    private static ReportReminderSchedulerOptions ProductionWindows() => new()
    {
        Enabled = true,
        PollMinutes = 15,
        DailyDueHour = 16,
        WeeklyDueHour = 9,
        OverdueHour = 9,
        SummaryHour = 9,
        ReviewHour = 9
    };

    private static ReportReminderRunOptions RunOptions(string cycleKey, ReminderCategorySet categories) =>
        new(WeekKey: cycleKey,
            Date: null,
            IncludeWeeklyDue: categories.WeeklyDue,
            IncludeDailyDue: categories.DailyDue,
            IncludeOverdue: categories.Overdue,
            IncludeOverdueSummaries: categories.Summaries,
            IncludeReviewOverdue: categories.ReviewOverdue);

    /// <summary>
    /// يُشغِّل نافذة (يوم، ساعة رياض) بوضع بريد مُعطًى ومُرسِل مُعطًى.
    /// كلّ استدعاء يبني نطاقًا جديدًا وخدمات جديدة ⇒ الحالة في الذاكرة تُصفَّر تمامًا،
    /// وهو ما يجعل الاستدعاء المتكرّر نموذجًا أمينًا لإعادة التشغيل (Restart) ولدورة الاستطلاع (Poll) معًا.
    /// </summary>
    private async Task<(int Created, int Duplicate)> RunAsync(
        DateOnly riyadhDay, int riyadhHour, EmailNotificationMode mode, IEmailSender sender)
    {
        var categories = ProductionWindows().CategoriesForHour(riyadhHour);
        if (categories.IsEmpty) return (0, 0);

        var currentKey = ReportCalendarPolicy.WeekKeyFor(riyadhDay);
        var previousKey = ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.WeekStart(riyadhDay).AddDays(-7));

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var opts = Options.Create(new EmailNotificationOptions { Mode = mode });
        var app = sp.GetRequiredService<IOptions<AppOptions>>();

        var notifications = new EmailNotificationService(
            db, sender, opts, app, NullLogger<EmailNotificationService>.Instance);

        var service = new ReportReminderService(
            db, notifications, opts, app,
            new FixedClock(RiyadhMoment(riyadhDay, riyadhHour)),
            NullLogger<ReportReminderService>.Instance);

        var previous = await service.GenerateAsync(RunOptions(previousKey, categories));
        var current = await service.GenerateAsync(RunOptions(currentKey, categories));

        return (previous.Created + current.Created, previous.SkippedDuplicate + current.SkippedDuplicate);
    }

    // ===== سجلّ ما أُنشئ (للتنظيف المضمون) =====

    private readonly List<Guid> _userIds = new();
    private readonly List<Guid> _jobRoleIds = new();
    private readonly List<Guid> _templateIds = new();
    private readonly List<Guid> _departmentIds = new();
    private readonly List<Guid> _submissionIds = new();

    private Guid? _sharedCadenceTemplateId;
    private readonly Dictionary<Guid, Guid> _versionByUser = new();

    // ===== مساعدات إنشاء =====

    private async Task<Guid> CreateReportingUserAsync(string identityRole, string? cadenceCode = null)
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, identityRole);
        _userIds.Add(userId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Guid jobRoleId;
        var needsTemplate = true;

        if (cadenceCode is null)
        {
            var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}" };
            db.JobRoles.Add(jobRole);
            await db.SaveChangesAsync();
            jobRoleId = jobRole.Id;
            _jobRoleIds.Add(jobRoleId);
        }
        else
        {
            var jobRole = await db.JobRoles.FirstOrDefaultAsync(j => j.Code == cadenceCode);
            if (jobRole is null)
            {
                jobRole = new JobRole { NameAr = cadenceCode, Code = cadenceCode, IsActive = true };
                db.JobRoles.Add(jobRole);
                await db.SaveChangesAsync();
            }
            jobRoleId = jobRole.Id;
            needsTemplate = _sharedCadenceTemplateId is null;
        }

        Guid versionId;
        if (needsTemplate)
        {
            var template = new ReportTemplate
            {
                Title = $"قالب {Guid.NewGuid():N}",
                JobRoleId = jobRoleId,
                Classification = TemplateClassification.Primary,
                DefaultPeriodType = PeriodType.Weekly,
                IsActive = true,
                Status = TemplateStatus.Published,
                OwnerId = userId
            };
            db.ReportTemplates.Add(template);
            var version = new ReportTemplateVersion
            {
                ReportTemplateId = template.Id,
                VersionNumber = 1,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow
            };
            db.ReportTemplateVersions.Add(version);
            _templateIds.Add(template.Id);
            versionId = version.Id;
            if (cadenceCode is not null) _sharedCadenceTemplateId = template.Id;
            _sharedVersionId ??= cadenceCode is not null ? version.Id : _sharedVersionId;
        }
        else
        {
            versionId = _sharedVersionId!.Value;
        }

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();

        _versionByUser[userId] = versionId;
        return userId;
    }

    private Guid? _sharedVersionId;

    private Task<Guid> CreateSalesUserAsync() => CreateReportingUserAsync("Employee", "SALES_B2C");

    private async Task CreateDepartmentWithManagerAsync(Guid managerId, params Guid[] memberIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", ManagerId = managerId, IsActive = true };
        db.Set<Department>().Add(dept);
        foreach (var mid in memberIds)
        {
            var u = await db.Users.FirstAsync(x => x.Id == mid);
            u.DepartmentId = dept.Id;
        }
        await db.SaveChangesAsync();
        _departmentIds.Add(dept.Id);
    }

    /// <summary>يُسجِّل تسليمًا فعليًّا للمستخدم ⇒ ينتهي استحقاقه لتلك الفترة.</summary>
    private async Task InsertSubmissionAsync(Guid userId, PeriodType periodType, string periodKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = new ReportSubmission
        {
            ReportTemplateVersionId = _versionByUser[userId],
            SubmitterId = userId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = SubmissionStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow
        };
        db.ReportSubmissions.Add(submission);
        await db.SaveChangesAsync();
        _submissionIds.Add(submission.Id);
    }

    /// <summary>
    /// يزرع صفًّا **تاريخيًّا** بصيغة ما قبل الإصلاح: وضع DryRun لكن بمفتاح قانونيّ غير مُنمَّط.
    /// هذه هي صيغة الـ19 صفًّا القائمة على الإنتاج.
    /// </summary>
    private async Task InsertLegacyDryRunRowAsync(Guid userId, string eventType, string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EmailNotifications.Add(new EmailNotification
        {
            EventType = eventType,
            EntityType = "User",
            EntityId = userId,
            RecipientUserId = userId,
            RecipientEmail = (await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync()),
            Subject = "صفّ محاكاة تاريخيّ",
            BodyHtml = "<p>محاكاة</p>",
            BodyText = "محاكاة",
            Status = EmailNotificationStatus.DryRun,
            Mode = EmailNotificationMode.DryRun,
            CorrelationKey = correlationKey
        });
        await db.SaveChangesAsync();
    }

    // ===== مساعدات قراءة =====

    private static string DryKey(string canonicalKey) =>
        EmailNotificationService.DryRunCorrelationKeyPrefix + canonicalKey;

    private async Task<int> CountByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync(n => n.CorrelationKey == correlationKey);
    }

    private async Task<EmailNotification?> RowByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().FirstOrDefaultAsync(n => n.CorrelationKey == correlationKey);
    }

    private async Task<int> CountAllForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId);
    }

    /// <summary>كلّ صفوف الإشعار لهذا المستخدم (المفتاح + الوضع + الحالة) — للتوكيد التشخيصيّ.</summary>
    private async Task<List<(string Key, EmailNotificationMode Mode, EmailNotificationStatus Status)>> RowsForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.EmailNotifications.AsNoTracking()
                .Where(n => n.RecipientUserId == userId)
                .Select(n => new { n.CorrelationKey, n.Mode, n.Status })
                .ToListAsync())
            .Select(n => (n.CorrelationKey, n.Mode, n.Status)).ToList();
    }

    /// <summary>عدد صفوف **التسليم** (وضع Enabled) لهذا المستخدم — ما عداها محاكاة لا تُرسَل.</summary>
    private async Task<int> CountDeliveryRowsForUserAsync(Guid userId) =>
        (await RowsForUserAsync(userId)).Count(r => r.Mode == EmailNotificationMode.Enabled);

    private static string Describe(IEnumerable<(string Key, EmailNotificationMode Mode, EmailNotificationStatus Status)> rows) =>
        string.Join(" | ", rows.Select(r => $"[{r.Mode}/{r.Status}] {r.Key}"));

    /// <summary>
    /// عدد الرسائل الفعليّة التي خرجت لهذا المستخدم وحده.
    /// قاعدة <c>reporting_test</c> مشتركة ودائمة وفيها مئات المستخدمين المتراكمين، فأيّ تشغيل
    /// يولّد رسائل لهم أيضًا؛ لذلك كلّ عدّ إرسال هنا **محصور بمستلم الاختبار** لا بعدّاد المُرسِل العامّ.
    /// </summary>
    private async Task<int> SendsToAsync(List<string> recipients, Guid userId)
    {
        var email = await UserEmailAsync(userId);
        return recipients.Count(r => string.Equals(r, email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int> CountByEventAsync(string eventType, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var canonical = $"{eventType}:{_cycle.YearSegment}";
        var simulated = DryKey(canonical);
        return await db.EmailNotifications.AsNoTracking().CountAsync(n =>
            n.RecipientUserId == userId
            && (n.CorrelationKey.StartsWith(canonical) || n.CorrelationKey.StartsWith(simulated)));
    }

    private static string OverdueKey(string cycleKey, Guid userId) =>
        $"report-overdue:{cycleKey}:{userId}:{DelayType.EmployeeReportNotSubmitted}";

    private static string DailyDueKey(DateOnly day, Guid userId) =>
        $"report-daily-due:{DayKey(day)}:{userId}";

    // ═══════════════════════════════════════════════════════════════════════════
    // 1) DryRun أوّلًا ثمّ Enabled ⇒ المحاكاة تُسجَّل، والإرسال الفعليّ يقع مرّة واحدة بلا Duplicate كاذب
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S01_DryRunThenEnabled_RealSendHappensOnce_NotBlockedAsDuplicate()
    {
        var cycle = UseCycle(2061);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        // (أ) محاكاة: صفّ في فضاء المحاكاة، بلا أيّ استدعاء SMTP، ولا حجز لمفتاح التسليم.
        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);

        Assert.Equal(0, drySpy.SendCount);
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));
        Assert.Equal(0, await CountByKeyAsync(key));               // مفتاح التسليم لم يُحجَز
        Assert.Equal(EmailNotificationStatus.DryRun, (await RowByKeyAsync(DryKey(key)))!.Status);

        // (ب) تفعيل: الإرسال الفعليّ يقع — لم يُحجَب كمكرّر.
        var liveSpy = new CountingEmailSender();
        var live = await RunAsync(sunday, 16, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(1, await SendsToAsync(liveSpy.Recipients, sales));
        Assert.True(live.Created >= 1);
        var sent = await RowByKeyAsync(key);
        Assert.NotNull(sent);
        Assert.Equal(EmailNotificationStatus.Sent, sent!.Status);
        Assert.Equal(EmailNotificationMode.Enabled, sent.Mode);
        Assert.NotNull(sent.SentAt);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2) Enabled + Sent ثمّ استطلاع جديد ⇒ لا إرسال ثانٍ
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S02_EnabledSent_ThenNextPoll_NoSecondSend()
    {
        var cycle = UseCycle(2062);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);
        Assert.Equal(1, await SendsToAsync(spy.Recipients, sales));
        Assert.Equal(1, await CountByKeyAsync(key));

        // استطلاع لاحق داخل النافذة نفسها (نفس المُرسِل ⇒ العدّاد تراكميّ).
        var second = await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);

        Assert.Equal(1, await SendsToAsync(spy.Recipients, sales));  // لا إرسال ثانٍ
        Assert.Equal(1, await CountByKeyAsync(key));               // لا صفّ ثانٍ
        Assert.True(second.Duplicate >= 1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3) Enabled + Sent ثمّ إعادة تشغيل ⇒ لا إرسال ثانٍ (الحارس الحقيقيّ = المفتاح المستمرّ)
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S03_EnabledSent_ThenRestart_NoSecondSend()
    {
        var cycle = UseCycle(2063);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var beforeRestart = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, beforeRestart);
        Assert.Equal(1, await SendsToAsync(beforeRestart.Recipients, sales));

        // إعادة التشغيل = خدمات جديدة ومُرسِل جديد وعدّاد صفريّ؛ لا يبقى إلا ما في القاعدة.
        var afterRestart = new CountingEmailSender();
        var rerun = await RunAsync(sunday, 16, EmailNotificationMode.Enabled, afterRestart);

        Assert.Equal(0, await SendsToAsync(afterRestart.Recipients, sales));
        Assert.Equal(1, await CountByKeyAsync(key));
        Assert.True(rerun.Duplicate >= 1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4) محاكاة متكرّرة عبر استطلاعات ⇒ لا إرسال، ولا حجب للإرسال الفعليّ لاحقًا
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S04_RepeatedDryRunPolls_NoSend_AndDoNotBlockLaterEnabled()
    {
        var cycle = UseCycle(2064);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);
        var secondDry = await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);
        var thirdDry = await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);

        Assert.Equal(0, drySpy.SendCount);
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));       // المحاكاة نفسها ما زالت لا تتكرّر
        Assert.True(secondDry.Duplicate >= 1);
        Assert.True(thirdDry.Duplicate >= 1);
        Assert.Equal(0, await CountByKeyAsync(key));

        var liveSpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(1, await SendsToAsync(liveSpy.Recipients, sales));
        Assert.Equal(1, await CountByKeyAsync(key));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5) محاكاة سابقة لنفس الدورة ⇒ الإرسال الفعليّ الحاليّ مسموح ما دام الاستحقاق قائمًا
    //    (هذا هو سيناريو الإنتاج بعينه: مفاتيح مرتبطة بالدورة لا باليوم)
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S05_PriorDryRunForSameCycle_StillDue_EnabledAllowed()
    {
        var cycle = UseCycle(2065);
        var emp = await CreateReportingUserAsync("Employee");
        var sunday = cycle.Day(8);
        var key = OverdueKey(cycle.Key, emp);

        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.DryRun, drySpy);
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));
        Assert.Equal(0, drySpy.SendCount);

        var liveSpy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(1, await CountByKeyAsync(key));
        Assert.Equal(EmailNotificationStatus.Sent, (await RowByKeyAsync(key))!.Status);
        Assert.Equal(1, await SendsToAsync(liveSpy.Recipients, emp));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6) محاكاة سابقة لكنّ الاستحقاق انتهى ⇒ لا إرسال، والسبب «غير مستحقّ» لا «مكرّر»
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S06_PriorDryRun_ButEntitlementEnded_NoSend_ReasonIsNotDue()
    {
        var cycle = UseCycle(2066);
        var emp = await CreateReportingUserAsync("Employee");
        var sunday = cycle.Day(8);
        var key = OverdueKey(cycle.Key, emp);

        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.DryRun, drySpy);
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));

        // انتهاء الاستحقاق: سُلّم التقرير فعلًا لتلك الدورة.
        await InsertSubmissionAsync(emp, PeriodType.Weekly, cycle.Key);

        var liveSpy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(0, await CountByKeyAsync(key));               // لا صفّ تسليم إطلاقًا
        Assert.Equal(0, await SendsToAsync(liveSpy.Recipients, emp)); // ولا إرسال
        // لم يُصنَّف «مكرّرًا» — بل لم يُرشَّح أصلًا: لا صفّ تسليم واحد لهذا المستخدم.
        var rows = await RowsForUserAsync(emp);
        Assert.True(rows.All(r => r.Mode == EmailNotificationMode.DryRun), Describe(rows));
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));        // صفّ المحاكاة لم يُمَسّ
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7) مستخدم سلّم تقريره ⇒ لا رسالة إطلاقًا حتى مع وجود محاكاة سابقة
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S07_UserWhoSubmitted_GetsNoMessage_EvenWithPriorDryRun()
    {
        var cycle = UseCycle(2067);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);
        Assert.Equal(1, await CountByKeyAsync(DryKey(key)));

        await InsertSubmissionAsync(sales, PeriodType.Daily, DayKey(sunday));

        var liveSpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(0, await CountByKeyAsync(key));
        Assert.Equal(0, await SendsToAsync(liveSpy.Recipients, sales));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8) مستخدم لم يُسلِّم ⇒ رسالة Enabled واحدة بالضبط
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S08_UserWhoDidNotSubmit_GetsExactlyOneEnabledMessage()
    {
        var cycle = UseCycle(2068);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);

        Assert.Equal(1, await SendsToAsync(spy.Recipients, sales));
        Assert.Equal(1, await CountByKeyAsync(key));
        Assert.Equal(1, await CountAllForUserAsync(sales));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9) مستخدمون مختلفون ⇒ لا تصادم في مفاتيح الترابط
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S09_DifferentUsers_NoKeyCollision()
    {
        var cycle = UseCycle(2069);
        var a = await CreateSalesUserAsync();
        var b = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);

        Assert.Equal(1, await SendsToAsync(spy.Recipients, a));
        Assert.Equal(1, await SendsToAsync(spy.Recipients, b));
        Assert.NotEqual(await UserEmailAsync(a), await UserEmailAsync(b));
        Assert.Equal(1, await CountByKeyAsync(DailyDueKey(sunday, a)));
        Assert.Equal(1, await CountByKeyAsync(DailyDueKey(sunday, b)));
        Assert.NotEqual(DailyDueKey(sunday, a), DailyDueKey(sunday, b));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10) فئات مختلفة لنفس المستخدم ⇒ لا تصادم كاذب
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S10_DifferentCategoriesForSameUser_NoFalseCollision()
    {
        var cycle = UseCycle(2070);
        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var sunday = cycle.Day(8);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.Enabled, spy);

        // المدير يستلم في النافذة نفسها فئتين مختلفتين: «أسبوعيّ مستحقّ» (يوم استحقاق المدير = الأحد)
        // و«ملخّص تأخّر الإدارة» عن موظّفه المتأخّر. التوكيد بنيويّ لا بإعادة تركيب المفاتيح يدويًّا:
        // المطلوب إثبات أنّ الفئتين لا تتصادمان على مفتاح واحد.
        var mgrRows = await RowsForUserAsync(mgr);
        Assert.Contains(mgrRows, r => r.Key.StartsWith("report-weekly-due:"));
        Assert.Contains(mgrRows, r => r.Key.StartsWith("report-department-overdue-summary:"));
        Assert.Equal(1, await CountByKeyAsync(OverdueKey(cycle.Key, emp)));

        // لا تصادم كاذب: كلّ فئة بمفتاح مستقلّ، وعدد المفاتيح المتمايزة = عدد الصفوف = عدد الرسائل الفعليّة.
        Assert.Equal(mgrRows.Count, mgrRows.Select(r => r.Key).Distinct().Count());
        Assert.True(mgrRows.Count >= 2, Describe(mgrRows));
        Assert.Equal(mgrRows.Count, await SendsToAsync(spy.Recipients, mgr));
        Assert.Equal(1, await SendsToAsync(spy.Recipients, emp));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11) فشل الإرسال ⇒ لا تكرار غير منضبط (القناة تحاول مرّة واحدة والمفتاح يبقى محجوزًا)
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S11_FailedSend_DoesNotProduceUncontrolledDuplicates()
    {
        var cycle = UseCycle(2071);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var key = DailyDueKey(sunday, sales);

        var failing = new FailingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, failing);

        var failed = await RowByKeyAsync(key);
        Assert.NotNull(failed);
        Assert.Equal(EmailNotificationStatus.Failed, failed!.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Null(failed.SentAt);
        Assert.Equal(1, await SendsToAsync(failing.Recipients, sales));

        // استطلاع/إعادة تشغيل لاحق: لا صفّ جديد ولا محاولة موازية.
        var again = await RunAsync(sunday, 16, EmailNotificationMode.Enabled, failing);
        Assert.Equal(1, await SendsToAsync(failing.Recipients, sales));
        Assert.Equal(1, await CountByKeyAsync(key));
        Assert.Equal(1, await CountAllForUserAsync(sales));
        Assert.True(again.Duplicate >= 1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 12) نافذة 09:00 ⇒ التذكير اليوميّ = صفر (الفصل بين النوافذ لم ينكسر بالإصلاح)
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S12_Window09_ProducesZeroDailyDue()
    {
        var cycle = UseCycle(2072);
        var emp = await CreateReportingUserAsync("Employee");
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 9, EmailNotificationMode.Enabled, spy);

        Assert.Equal(0, await CountByEventAsync("report-daily-due", sales));
        Assert.Equal(0, await CountByKeyAsync(DailyDueKey(sunday, sales)));
        Assert.Equal(1, await CountByKeyAsync(OverdueKey(cycle.Key, emp)));   // نافذة 09 تعمل فعلًا
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 13) نافذة 16:00 ⇒ التذكير اليوميّ وحده
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S13_Window16_ProducesDailyDueOnly()
    {
        var cycle = UseCycle(2073);
        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);

        var spy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, spy);

        Assert.Equal(1, await CountByKeyAsync(DailyDueKey(sunday, sales)));
        Assert.Equal(0, await CountByKeyAsync(OverdueKey(cycle.Key, emp)));
        Assert.Equal(0, await CountByKeyAsync($"report-department-overdue-summary:{cycle.Key}:{mgr}"));
        Assert.Equal(0, await CountByEventAsync("report-weekly-due", mgr));
        Assert.Equal(1, await SendsToAsync(spy.Recipients, sales));
        Assert.Equal(0, await SendsToAsync(spy.Recipients, mgr));
        Assert.Equal(0, await SendsToAsync(spy.Recipients, emp));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 14) سلوك الصفوف التاريخية (صيغة الـ19 على الإنتاج)
    //     الصفّ التاريخيّ بمفتاح قانونيّ غير مُنمَّط يبقى حاجزًا **عمدًا**:
    //     الإصلاح لا يُطلق أيّ رسالة تعويضيّة من تلقاء نفسه، ولا يُعدّل أيّ مفتاح تاريخيّ.
    //     الإفراج المضبوط عن تلك الرسائل يقع في تذكرة الاسترداد المنفصلة بتصريح مستقلّ.
    //     وفي المقابل: صفّ محاكاة بالصيغة الجديدة لا يحجب الإرسال الفعليّ إطلاقًا.
    // ═══════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task S14_LegacyUnprefixedDryRunRows_RemainSuppressed_WhileNewFormatDoesNot()
    {
        var cycle = UseCycle(2074);
        var legacyUser = await CreateSalesUserAsync();
        var freshUser = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);

        var legacyKey = DailyDueKey(sunday, legacyUser);
        var freshKey = DailyDueKey(sunday, freshUser);

        // (أ) الصيغة التاريخية: محاكاة بمفتاح قانونيّ (كما هي الـ19 على الإنتاج).
        await InsertLegacyDryRunRowAsync(legacyUser, "report-daily-due", legacyKey);

        // (ب) الصيغة الجديدة بعد الإصلاح: محاكاة في فضاء أسمائها.
        var drySpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.DryRun, drySpy);
        Assert.Equal(1, await CountByKeyAsync(DryKey(freshKey)));
        Assert.Equal(0, drySpy.SendCount);

        // (ج) تشغيل فعليّ: صاحب الصفّ الجديد يُرسَل له، وصاحب الصفّ التاريخيّ يبقى محجوبًا.
        var liveSpy = new CountingEmailSender();
        await RunAsync(sunday, 16, EmailNotificationMode.Enabled, liveSpy);

        Assert.Equal(1, await CountByKeyAsync(freshKey));
        Assert.Equal(EmailNotificationStatus.Sent, (await RowByKeyAsync(freshKey))!.Status);

        // لا صفّ تسليم واحد له: الصفّ التاريخيّ ما زال حاجزًا، وصفّ المحاكاة الجديد محاكاة لا تسليم.
        var legacyRows = await RowsForUserAsync(legacyUser);
        Assert.Equal(0, await CountDeliveryRowsForUserAsync(legacyUser));
        Assert.True(legacyRows.All(r => r.Status == EmailNotificationStatus.DryRun), Describe(legacyRows));
        Assert.Equal(EmailNotificationStatus.DryRun, (await RowByKeyAsync(legacyKey))!.Status);
        Assert.Equal(1, await SendsToAsync(liveSpy.Recipients, freshUser));     // إرسال واحد فقط: للجديد
        Assert.Equal(0, await SendsToAsync(liveSpy.Recipients, legacyUser));
        Assert.DoesNotContain(
            await UserEmailAsync(legacyUser), liveSpy.Recipients);
    }

    private async Task<string> UserEmailAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync())!;
    }

    // ===== التنظيف المضمون =====

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var users = _userIds.ToList();
            if (users.Count > 0)
            {
                await db.EmailNotifications
                    .Where(n => n.RecipientUserId != null && users.Contains(n.RecipientUserId.Value))
                    .ExecuteDeleteAsync();
            }

            var submissions = _submissionIds.ToList();
            if (submissions.Count > 0)
                await db.ReportSubmissions.Where(s => submissions.Contains(s.Id)).ExecuteDeleteAsync();

            if (users.Count > 0)
            {
                await db.Users.Where(u => users.Contains(u.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.JobRoleId, u => (Guid?)null)
                        .SetProperty(u => u.DepartmentId, u => (Guid?)null)
                        .SetProperty(u => u.TeamId, u => (Guid?)null));
            }

            var templates = _templateIds.ToList();
            if (templates.Count > 0)
            {
                await db.ReportTemplateVersions.Where(v => templates.Contains(v.ReportTemplateId)).ExecuteDeleteAsync();
                await db.ReportTemplates.Where(t => templates.Contains(t.Id)).ExecuteDeleteAsync();
            }

            var departments = _departmentIds.ToList();
            if (departments.Count > 0)
                await db.Set<Department>().Where(d => departments.Contains(d.Id)).ExecuteDeleteAsync();

            var jobRoles = _jobRoleIds.ToList();
            if (jobRoles.Count > 0)
                await db.JobRoles.Where(j => jobRoles.Contains(j.Id)).ExecuteDeleteAsync();
        }
        catch
        {
            // التنظيف أفضل-جهد بحكم مشاركة القاعدة؛ لا يُسقِط نتيجة الاختبار.
        }
    }
}

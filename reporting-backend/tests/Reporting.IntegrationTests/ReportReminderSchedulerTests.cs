using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// EMAIL-NOTIFICATIONS-FULL-INTERNAL-ACTIVATION-R1 — مجدول تذكيرات التقارير (R2).
///
/// يُثبِت: (1) التشغيل التلقائيّ عند ساعة الرياض المضبوطة بلا استدعاء endpoint يدويّ،
/// (2) عدم التشغيل خارج الساعة أو حين تعطيل البوابة أو حين خلوّ قائمة الساعات،
/// (3) عدم التكرار داخل نفس الفتحة الزمنية،
/// (4) الأهمّ — عدم التكرار بعد إعادة التشغيل: نسخة جديدة (تُحاكي Restart، فتفقد القفل الذاكريّ)
///     تُعيد الاستدعاء فعلًا لكنّها لا تُنشئ أيّ صفّ جديد بفضل حارس CorrelationKey في القاعدة.
///
/// كلّ الاختبارات تمرّر <c>utcNow</c> صراحةً ⇒ حتميّة بلا اعتماد على ساعة النظام.
/// </summary>
[Collection("Integration")]
public class ReportReminderSchedulerTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportReminderSchedulerTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== مساعدات =====

    private ReportReminderSchedulerService NewScheduler(ReportReminderSchedulerOptions options) =>
        new(_factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<ReportReminderSchedulerService>.Instance);

    private static ReportReminderSchedulerOptions EnabledAt(string hours) => new()
    {
        Enabled = true,
        RunAtRiyadhHours = hours,
        PollMinutes = 15,
        IncludeDue = true,
        IncludeOverdue = true,
        IncludeReviewOverdue = true
    };

    /// <summary>لحظة UTC يقابلها في الرياض (UTC+3) اليومُ نفسه والساعةُ المطلوبة.</summary>
    private static DateTime UtcForRiyadhHour(int riyadhHour) =>
        DateTime.UtcNow.Date.AddHours(riyadhHour).Add(-ReportCalendarPolicy.RiyadhOffset);

    /// <summary>يجعل المستخدم «متوقَّعًا منه تقرير» عبر مسمّى وظيفي له قالب أساسي أسبوعي منشور.</summary>
    private async Task SetupReportingRoleAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}" };
        db.JobRoles.Add(jobRole);

        var template = new ReportTemplate
        {
            Title = $"قالب {Guid.NewGuid():N}",
            JobRoleId = jobRole.Id,
            Classification = TemplateClassification.Primary,
            DefaultPeriodType = PeriodType.Weekly,
            IsActive = true,
            Status = TemplateStatus.Published,
            OwnerId = userId
        };
        db.ReportTemplates.Add(template);
        db.ReportTemplateVersions.Add(new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow
        });

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        await db.SaveChangesAsync();
    }

    /// <summary>عدد صفوف الإشعارات التي تخصّ هذا المستخدم (مفتاح الترابط يحوي معرّفه).</summary>
    private async Task<int> CountNotificationsForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var needle = userId.ToString();
        return await db.EmailNotifications.AsNoTracking()
            .CountAsync(n => n.CorrelationKey.Contains(needle));
    }

    // ===== 1) البوابة معطّلة ⇒ لا تشغيل =====
    [Fact]
    public async Task Tick_WhenDisabled_DoesNotRun()
    {
        // مستخدم متوقَّع منه تقرير ⇒ لو عمل المجدول لأنتج صفوفًا. البوابة معطّلة ⇒ يجب أن يبقى 0.
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);

        var options = EnabledAt("8");
        options.Enabled = false;
        var scheduler = NewScheduler(options);

        var result = await scheduler.TickAsync(UtcForRiyadhHour(8));

        Assert.Null(result);
        Assert.Equal(0, await CountNotificationsForUserAsync(userId));
    }

    // ===== 2) خارج الساعة المضبوطة ⇒ لا تشغيل =====
    [Fact]
    public async Task Tick_OutsideConfiguredHour_DoesNotRun()
    {
        var scheduler = NewScheduler(EnabledAt("8"));

        var result = await scheduler.TickAsync(UtcForRiyadhHour(9));

        Assert.Null(result);
    }

    // ===== 3) قائمة ساعات فارغة/غير صالحة ⇒ لا تشغيل =====
    [Fact]
    public async Task Tick_WithEmptyOrInvalidHours_DoesNotRun()
    {
        var scheduler = NewScheduler(EnabledAt("   ,99,-3"));

        Assert.Empty(EnabledAt("   ,99,-3").ParsedRunAtRiyadhHours);
        Assert.Null(await scheduler.TickAsync(UtcForRiyadhHour(8)));
        Assert.Null(await scheduler.TickAsync(UtcForRiyadhHour(13)));
    }

    // ===== 4) عند ساعة الرياض المضبوطة ⇒ يعمل تلقائيًّا وللدورة الحالية =====
    [Fact]
    public async Task Tick_AtConfiguredRiyadhHour_RunsForCurrentRiyadhCycle()
    {
        var scheduler = NewScheduler(EnabledAt("8"));

        var result = await scheduler.TickAsync(UtcForRiyadhHour(8));

        Assert.NotNull(result);
        Assert.Equal(ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday()), result!.WeekKey);
        Assert.False(string.IsNullOrWhiteSpace(result.WeekLabel));
    }

    // ===== 5) نفس الفتحة الزمنية مرّتين ⇒ تشغيل واحد فقط =====
    [Fact]
    public async Task Tick_SameSlotTwice_RunsOnlyOnce()
    {
        var scheduler = NewScheduler(EnabledAt("8"));
        var slot = UtcForRiyadhHour(8);

        Assert.NotNull(await scheduler.TickAsync(slot));
        Assert.Null(await scheduler.TickAsync(slot));
        Assert.Null(await scheduler.TickAsync(slot.AddMinutes(45)));
    }

    // ===== 6) ساعة ثانية مضبوطة في اليوم نفسه ⇒ فتحة مستقلّة تعمل =====
    [Fact]
    public async Task Tick_SecondConfiguredHourSameDay_RunsAgain()
    {
        var scheduler = NewScheduler(EnabledAt("8,13"));

        Assert.NotNull(await scheduler.TickAsync(UtcForRiyadhHour(8)));
        Assert.Null(await scheduler.TickAsync(UtcForRiyadhHour(8)));
        Assert.NotNull(await scheduler.TickAsync(UtcForRiyadhHour(13)));
    }

    // ===== 7) إثبات عدم التكرار بعد Restart =====
    [Fact]
    public async Task Tick_AfterRestartSameSlot_RunsAgainButCreatesNoNewRows()
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetupReportingRoleAsync(userId);

        var slot = UtcForRiyadhHour(8);

        // النسخة الأولى: تُولّد فعلًا.
        var first = await NewScheduler(EnabledAt("8")).TickAsync(slot);
        Assert.NotNull(first);
        Assert.True(first!.Created >= 1, "يجب أن يُنشئ التشغيل الأول صفًّا واحدًا على الأقلّ للمستخدم الجديد.");

        var afterFirst = await CountNotificationsForUserAsync(userId);
        Assert.True(afterFirst >= 1);

        // نسخة جديدة تمامًا = محاكاة إعادة التشغيل (القفل الذاكريّ مفقود ⇒ يُعاد الاستدعاء فعلًا).
        var second = await NewScheduler(EnabledAt("8")).TickAsync(slot);
        Assert.NotNull(second);

        // الضمان الحقيقيّ في القاعدة: لا صفّ جديد ولا بريد.
        Assert.Equal(0, second!.Created);
        Assert.True(second.SkippedDuplicate >= afterFirst,
            "يجب أن تُصنَّف كلّ الرسائل المعاد استدعاؤها كمُكرّرة.");
        Assert.Equal(afterFirst, await CountNotificationsForUserAsync(userId));
    }

    // ===== 8) التشغيل المباشر يشتقّ الدورة من تقويم الرياض لا من مفتاح ثابت =====
    [Fact]
    public async Task RunOnce_DerivesCycleFromRiyadhCalendar()
    {
        var result = await NewScheduler(EnabledAt("8")).RunOnceAsync();

        Assert.Equal(ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday()), result.WeekKey);
    }
}

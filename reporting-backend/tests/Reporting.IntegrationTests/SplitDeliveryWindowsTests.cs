using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// EMAIL-NOTIFICATIONS-SPLIT-DELIVERY-WINDOWS-R1 — نوافذ إرسال مفصولة لكلّ فئة تذكير.
///
/// القرار التشغيليّ المعتمَد (توقيت الرياض):
///   09:00 — الاستحقاق الأسبوعيّ (حسب يوم استحقاق الدور) + التأخّر الفرديّ + ملخّصات التأخّر + تنبيهات المراجعة.
///           تذكير التقرير اليوميّ (report-daily-due) **ممنوع** في هذه النافذة.
///   16:00 — تذكير التقرير اليوميّ **وحده** لموظّفي المبيعات (SALES_B2B/SALES_B2C).
///           الاستحقاق الأسبوعيّ والتأخّر والملخّصات والمراجعة **ممنوعة** في هذه النافذة.
///
/// تُثبِت هذه الاختبارات أنّ الفصل يجري في المولّد نفسه لا في المجدول فحسب: النافذة تُشتقّ من
/// <see cref="ReportReminderSchedulerOptions.CategoriesForHour"/> الحقيقيّة ثمّ تُمرَّر كما هي إلى
/// <see cref="ReportReminderService"/> — فما يُمنَع في نافذة لا يُنشئ أيّ صفّ إطلاقًا.
///
/// كلّ الاختبارات تحقن <see cref="ISystemClock"/> ثابتة ⇒ محاكاة اليوم والساعة حتميّة بلا اعتماد على
/// ساعة النظام. القاعدة مشتركة ⇒ العدّ دائمًا بمفاتيح ترابط مستخدمين مُنشأين حديثًا داخل سنة محاكاة معزولة.
/// </summary>
[Collection("Integration")]
public class SplitDeliveryWindowsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SplitDeliveryWindowsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    /// <summary>مُرسِل SMTP وهميّ يَعُدّ الاستدعاءات — لإثبات أنّ DryRun لا يلمس القناة إطلاقًا.</summary>
    private sealed class CountingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }
        public bool IsConfigured => true;
        public Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
        {
            SendCount++;
            return Task.FromResult(EmailSendResult.Ok());
        }
    }

    // ===== دورة محاكاة معزولة (سنة مستقلّة عن بقيّة ملفّات الاختبار) =====
    private static readonly DateOnly CycleStart = ReportCalendarPolicy.WeekStart(new DateOnly(2028, 5, 17));
    private static string CycleKey => ReportCalendarPolicy.WeekKeyFor(CycleStart);

    /// <summary>يوم من دورة المحاكاة (0 = السبت … 6 = الجمعة)، ويجوز تجاوز 6 للدورة التالية.</summary>
    private static DateOnly Day(int offsetFromCycleStart) => CycleStart.AddDays(offsetFromCycleStart);

    private static string DayKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>لحظة UTC يقابلها في الرياض اليومُ والساعةُ المطلوبان.</summary>
    private static DateTimeOffset RiyadhMoment(DateOnly day, int hour) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero) - ReportCalendarPolicy.RiyadhOffset;

    /// <summary>الإعداد الإنتاجيّ المعتمَد للنوافذ — هو نفسه مصدر الحقيقة في هذه الاختبارات.</summary>
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

    /// <summary>
    /// يُشغِّل المولّد تمامًا كما يفعل المجدول في نافذة (يوم، ساعة رياض): الفئات تُشتقّ من الإعداد الحقيقيّ،
    /// ثمّ تُنفَّذ الدورة السابقة ثمّ الحالية. الساعة التي لا تُطابِق أيّ فئة لا تُشغِّل شيئًا إطلاقًا.
    /// </summary>
    private async Task<(int Created, int Duplicate)> RunWindowAsync(DateOnly riyadhDay, int riyadhHour)
    {
        var categories = ProductionWindows().CategoriesForHour(riyadhHour);
        if (categories.IsEmpty) return (0, 0);

        var currentKey = ReportCalendarPolicy.WeekKeyFor(riyadhDay);
        var previousKey = ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.WeekStart(riyadhDay).AddDays(-7));

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = new ReportReminderService(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IEmailNotificationService>(),
            sp.GetRequiredService<IOptions<EmailNotificationOptions>>(),
            sp.GetRequiredService<IOptions<AppOptions>>(),
            new FixedClock(RiyadhMoment(riyadhDay, riyadhHour)),
            NullLogger<ReportReminderService>.Instance);

        var previous = await service.GenerateAsync(RunOptions(previousKey, categories));
        var current = await service.GenerateAsync(RunOptions(currentKey, categories));

        return (previous.Created + current.Created, previous.SkippedDuplicate + current.SkippedDuplicate);
    }

    private static ReportReminderRunOptions RunOptions(string cycleKey, ReminderCategorySet categories) =>
        new(WeekKey: cycleKey,
            Date: null,
            IncludeWeeklyDue: categories.WeeklyDue,
            IncludeDailyDue: categories.DailyDue,
            IncludeOverdue: categories.Overdue,
            IncludeOverdueSummaries: categories.Summaries,
            IncludeReviewOverdue: categories.ReviewOverdue);

    // ===== مساعدات إنشاء =====

    private async Task<Guid> CreateReportingUserAsync(string identityRole, string? cadenceCode = null)
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, identityRole);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        JobRole? jobRole = cadenceCode is null ? null : await db.JobRoles.FirstOrDefaultAsync(j => j.Code == cadenceCode);
        if (jobRole is null)
        {
            jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = cadenceCode };
            db.JobRoles.Add(jobRole);
        }

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
        return userId;
    }

    private Task<Guid> CreateSalesUserAsync() => CreateReportingUserAsync("Employee", "SALES_B2C");

    private async Task<List<Guid>> CreateSalesUsersAsync(int count)
    {
        var ids = new List<Guid>(count);
        for (var i = 0; i < count; i++) ids.Add(await CreateSalesUserAsync());
        return ids;
    }

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
    }

    // ===== مساعدات عدّ =====

    private async Task<int> CountByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync(n => n.CorrelationKey == correlationKey);
    }

    /// <summary>بادئة فضاء المحاكاة: مفاتيح الدورة واليوم كلّها تبدأ بسنة المحاكاة ⇒ عزل عن الدورة الجارية.</summary>
    private static readonly string SimulationSegment = $"{CycleStart.Year}-";

    private async Task<int> CountByEventAsync(string eventType, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prefix = $"{eventType}:{SimulationSegment}";
        var suffix = $":{userId}";
        return await db.EmailNotifications.AsNoTracking().CountAsync(n =>
            n.CorrelationKey != null && n.CorrelationKey.StartsWith(prefix) && n.CorrelationKey.EndsWith(suffix));
    }

    private Task<int> CountWeeklyDueAsync(Guid userId) => CountByEventAsync("report-weekly-due", userId);

    private Task<int> CountDailyDueAsync(Guid userId) => CountByEventAsync("report-daily-due", userId);

    /// <summary>مفتاح تأخّر الموظّف لدورة المحاكاة (تطابق تامّ — لا يلتقط دورة أخرى).</summary>
    private static string OverdueKey(string cycleKey, Guid userId) =>
        $"report-overdue:{cycleKey}:{userId}:{DelayType.EmployeeReportNotSubmitted}";

    // ===== 1) الأحد 09:00 — أسبوعيّ للمديرين + تأخّر وملخّصات، و0 تذكير يوميّ =====
    [Fact]
    public async Task Sunday_At09_WeeklyManagersAndOverdueAndSummaries_NoDailyDue()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var sales = await CreateSalesUserAsync();
        await CreateDepartmentWithManagerAsync(mgr, emp);

        var sunday = Day(8); // الأحد التالي = يوم استحقاق المدير للدورة السابقة

        await RunWindowAsync(sunday, 9);

        // الأسبوعيّ: المدير فقط.
        Assert.Equal(1, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));

        // التأخّر الفرديّ والملخّصات مسموحان في نافذة 09.
        Assert.Equal(1, await CountByKeyAsync(OverdueKey(CycleKey, emp)));
        Assert.Equal(1, await CountByKeyAsync($"report-department-overdue-summary:{CycleKey}:{mgr}"));

        // اليوميّ ممنوع في نافذة 09 — لا صفّ إطلاقًا.
        Assert.Equal(0, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountByKeyAsync($"report-daily-due:{DayKey(sunday)}:{sales}"));
    }

    // ===== 2) الأحد 16:00 — تذكير يوميّ للمبيعات فقط (خمسة مستلمين)، و0 أسبوعيّ/تأخّر/ملخّصات =====
    [Fact]
    public async Task Sunday_At16_DailySalesOnly_NoWeeklyDue()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var salesTeam = await CreateSalesUsersAsync(5);

        var sunday = Day(8);

        await RunWindowAsync(sunday, 16);

        // خمسة موظّفي مبيعات ⇒ خمس رسائل يوميّة، واحدة لكلٍّ بمفتاح يوم الأحد نفسه.
        foreach (var sales in salesTeam)
        {
            Assert.Equal(1, await CountDailyDueAsync(sales));
            Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(sunday)}:{sales}"));
        }

        // الأسبوعيّ والتأخّر والملخّصات ممنوعة في نافذة 16.
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountByKeyAsync(OverdueKey(CycleKey, emp)));
        Assert.Equal(0, await CountByKeyAsync($"report-department-overdue-summary:{CycleKey}:{mgr}"));
    }

    // ===== 3) الاثنين 09:00 — أسبوعيّ للتنفيذيين فقط، و0 تذكير يوميّ =====
    [Fact]
    public async Task Monday_At09_WeeklyExecutivesOnly_NoDailyDue()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var ceo = await CreateReportingUserAsync("CEO");
        var admin = await CreateReportingUserAsync("Admin");
        var sales = await CreateSalesUserAsync();

        var monday = Day(9); // الاثنين التالي = يوم استحقاق التنفيذيين للدورة السابقة

        await RunWindowAsync(monday, 9);

        Assert.Equal(1, await CountWeeklyDueAsync(gm));
        Assert.Equal(1, await CountWeeklyDueAsync(ceo));
        Assert.Equal(1, await CountWeeklyDueAsync(admin));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));

        Assert.Equal(0, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountByKeyAsync($"report-daily-due:{DayKey(monday)}:{sales}"));
    }

    // ===== 4) الاثنين 16:00 — تذكير يوميّ فقط =====
    [Fact]
    public async Task Monday_At16_DailySalesOnly()
    {
        var gm = await CreateReportingUserAsync("GeneralManager");
        var ceo = await CreateReportingUserAsync("CEO");
        var sales = await CreateSalesUserAsync();

        var monday = Day(9);

        await RunWindowAsync(monday, 16);

        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(monday)}:{sales}"));

        // يوم استحقاق التنفيذيين نفسه، لكنّ نافذة 16 لا تُنتِج أسبوعيًّا إطلاقًا.
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
        Assert.Equal(0, await CountWeeklyDueAsync(ceo));
    }

    // ===== 5) الجمعة — لا يوميّ ولا أسبوعيّ في أيٍّ من النافذتين =====
    [Fact]
    public async Task Friday_BothWindows_NoDailyDue_AndNoWeeklyDue()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var sales = await CreateSalesUserAsync();

        var friday = Day(6);

        await RunWindowAsync(friday, 9);
        await RunWindowAsync(friday, 16);

        Assert.Equal(0, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountByKeyAsync($"report-daily-due:{DayKey(friday)}:{sales}"));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
    }

    // ===== 6) السبت 16:00 — تذكير يوميّ للمبيعات فقط =====
    [Fact]
    public async Task Saturday_At16_DailySalesOnly()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var sales = await CreateSalesUserAsync();

        var saturday = Day(0);

        await RunWindowAsync(saturday, 16);

        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(saturday)}:{sales}"));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(sales));
    }

    // ===== 7) إعادة التشغيل داخل النافذة نفسها ⇒ created=0 وكلّ الرسائل مُكرّرة =====
    [Fact]
    public async Task SameWindowRerun_CreatesNothing_AndCountsDuplicates()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var sales = await CreateSalesUserAsync();

        var sunday = Day(8);

        // نافذة 09: التشغيل الأول يُنشئ فعلًا، والثاني لا يُنشئ شيئًا.
        var first09 = await RunWindowAsync(sunday, 9);
        Assert.True(first09.Created >= 1);
        var weeklyAfterFirst = await CountWeeklyDueAsync(mgr);

        var second09 = await RunWindowAsync(sunday, 9);
        Assert.Equal(0, second09.Created);
        Assert.True(second09.Duplicate >= first09.Created);
        Assert.Equal(weeklyAfterFirst, await CountWeeklyDueAsync(mgr));

        // نافذة 16: نفس السلوك تمامًا، مستقلّة عن نافذة 09.
        var first16 = await RunWindowAsync(sunday, 16);
        Assert.True(first16.Created >= 1);
        var dailyAfterFirst = await CountDailyDueAsync(sales);
        Assert.Equal(1, dailyAfterFirst);

        var second16 = await RunWindowAsync(sunday, 16);
        Assert.Equal(0, second16.Created);
        Assert.True(second16.Duplicate >= first16.Created);
        Assert.Equal(dailyAfterFirst, await CountDailyDueAsync(sales));
    }

    // ===== 8) تشغيل 09:00 لا يحجز مفتاح رسالة اليوميّ الخاصّة بـ16:00 =====
    [Fact]
    public async Task Run_At09_DoesNotSquat_DailyDueKeyOf16()
    {
        var sales = await CreateSalesUserAsync();
        var sunday = Day(8);
        var dailyKey = $"report-daily-due:{DayKey(sunday)}:{sales}";

        await RunWindowAsync(sunday, 9);
        Assert.Equal(0, await CountByKeyAsync(dailyKey));   // لم يُحجَز المفتاح

        var at16 = await RunWindowAsync(sunday, 16);
        Assert.True(at16.Created >= 1);                      // ولا Duplicate كاذب
        Assert.Equal(1, await CountByKeyAsync(dailyKey));
    }

    // ===== 9) وضع DryRun لا يستدعي قناة SMTP إطلاقًا (نافذة 16) =====
    [Fact]
    public async Task DryRun_InDailyWindow_DoesNotCallSmtpSender()
    {
        var sales = await CreateSalesUserAsync();
        var sunday = Day(8);
        var categories = ProductionWindows().CategoriesForHour(16);
        var spy = new CountingEmailSender();

        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<AppDbContext>();
            var dryRun = Options.Create(new EmailNotificationOptions { Mode = EmailNotificationMode.DryRun });
            var app = sp.GetRequiredService<IOptions<AppOptions>>();

            var notifications = new EmailNotificationService(
                db, spy, dryRun, app, NullLogger<EmailNotificationService>.Instance);

            var service = new ReportReminderService(
                db, notifications, dryRun, app,
                new FixedClock(RiyadhMoment(sunday, 16)),
                NullLogger<ReportReminderService>.Instance);

            await service.GenerateAsync(RunOptions(ReportCalendarPolicy.WeekKeyFor(sunday), categories));
        }

        // أُنشئ الصفّ فعلًا (المسار عمل) لكن بلا أيّ استدعاء إرسال.
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(sunday)}:{sales}"));
        Assert.Equal(0, spy.SendCount);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = $"report-daily-due:{DayKey(sunday)}:{sales}";
        var row = await verifyDb.EmailNotifications.AsNoTracking().FirstAsync(n => n.CorrelationKey == key);
        Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
        Assert.Null(row.SentAt);
    }

    // ===== 10) المجدول معطّل ⇒ لا تشغيل ولا صفّ في أيّ نافذة =====
    [Fact]
    public async Task SchedulerDisabled_GeneratesNoRowsInAnyWindow()
    {
        var sales = await CreateSalesUserAsync();
        var emp = await CreateReportingUserAsync("Employee");

        var options = ProductionWindows();
        options.Enabled = false;

        var scheduler = new ReportReminderSchedulerService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<ReportReminderSchedulerService>.Instance);

        // فتحتا 09 و16 بتوقيت الرياض لليوم الحقيقيّ — البوابة معطّلة ⇒ null في كلتيهما.
        Assert.Null(await scheduler.TickAsync(UtcForRiyadhHourToday(9)));
        Assert.Null(await scheduler.TickAsync(UtcForRiyadhHourToday(16)));

        Assert.Equal(0, await CountAllForUserAsync(sales));
        Assert.Equal(0, await CountAllForUserAsync(emp));
    }

    /// <summary>لحظة UTC يقابلها في الرياض اليومُ الحقيقيّ نفسه والساعةُ المطلوبة.</summary>
    private static DateTime UtcForRiyadhHourToday(int riyadhHour) =>
        DateTime.UtcNow.Date.AddHours(riyadhHour).Add(-ReportCalendarPolicy.RiyadhOffset);

    private async Task<int> CountAllForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var needle = userId.ToString();
        return await db.EmailNotifications.AsNoTracking()
            .CountAsync(n => n.CorrelationKey != null && n.CorrelationKey.Contains(needle));
    }
}

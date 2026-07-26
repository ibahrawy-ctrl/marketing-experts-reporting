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
/// EMAIL-NOTIFICATIONS-ROLE-AWARE-SCHEDULE-FIX-R1 — أهليّة التذكير بحسب يوم الرياض ودور المستلِم،
/// موحَّدة مع SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1.
///
/// العقد الموحَّد النهائي (مشتقّ من <see cref="ReportingCalendarPolicy"/>، لا جدول جديد):
///   السبت   : report-daily-due لموظّفي المبيعات فقط (SALES_B2B/SALES_B2C، من أرضيّة 2026-07-25)؛ report-weekly-due = 0 لكلّ الأدوار.
///   الأحد   : daily للمبيعات؛ weekly للمدير فقط (بداية الدورة + 8).
///   الاثنين : daily للمبيعات؛ weekly للمدير العام/الرئيس التنفيذي/مدير النظام فقط (+ 9).
///   الثلاثاء: daily للمبيعات فقط.
///   الأربعاء: daily للمبيعات؛ weekly للموظّفين الأسبوعيين (+ 4).
///   الخميس  : daily للمبيعات؛ weekly لقادة الفرق (+ 5).
///   الجمعة  : لا daily ولا weekly إطلاقًا.
/// لا إزاحة دور تقع على السبت (4/5/8/9) ⇒ استحالة بنيويّة لظهور weekly-due يوم السبت.
/// التأخّر = بعد انقضاء يوم الاستحقاق فقط. الملخّصات = بعد وجود متأخّر فعليّ.
///
/// كلّ الاختبارات تحقن <see cref="ISystemClock"/> ثابتة ⇒ محاكاة أيّام الأسبوع حتميّة بلا اعتماد على
/// ساعة النظام. القاعدة مشتركة ⇒ لا تأكيد على أعداد مطلقة، بل على مفاتيح ترابط مستخدمين مُنشأين حديثًا.
/// </summary>
[Collection("Integration")]
public class RoleAwareReminderScheduleTests
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAwareReminderScheduleTests(CustomWebApplicationFactory factory) => _factory = factory;

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

    // ===== دورة محاكاة ثابتة بعد أرضيّة الإطلاق، بعيدة عن الدورة الجارية كي لا تتداخل بيانات =====
    /// <summary>سبت بداية دورة المحاكاة (مشتقّ من التقويم نفسه لا من ثابت مكتوب يدويًّا).</summary>
    private static readonly DateOnly CycleStart = ReportCalendarPolicy.WeekStart(new DateOnly(2027, 3, 17));
    private static string CycleKey => ReportCalendarPolicy.WeekKeyFor(CycleStart);

    /// <summary>لحظة UTC يقابلها في الرياض اليومُ المطلوب الساعة 08:00.</summary>
    private static DateTimeOffset RiyadhMorning(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero) - ReportCalendarPolicy.RiyadhOffset;

    // ===== مساعدات =====

    /// <summary>ينشئ مستخدمًا بدور Identity محدّد ويجعله «متوقَّعًا منه تقرير» (أسبوعيّ افتراضًا).</summary>
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

    /// <summary>موظّف مبيعات (دوريّة يوميّة مشتقّة من كود المسمّى SALES_B2C).</summary>
    private Task<Guid> CreateSalesUserAsync() => CreateReportingUserAsync("Employee", "SALES_B2C");

    /// <summary>
    /// يُشغِّل المولّد كما يفعل المجدول تمامًا (الدورة السابقة ثمّ الحالية) بساعة رياض محقونة ليوم معيّن.
    /// </summary>
    private async Task RunAsSchedulerAsync(DateOnly riyadhDay)
    {
        var currentKey = ReportCalendarPolicy.WeekKeyFor(riyadhDay);
        var previousKey = ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.WeekStart(riyadhDay).AddDays(-7));

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = new ReportReminderService(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IEmailNotificationService>(),
            sp.GetRequiredService<IOptions<EmailNotificationOptions>>(),
            sp.GetRequiredService<IOptions<AppOptions>>(),
            new FixedClock(RiyadhMorning(riyadhDay)),
            NullLogger<ReportReminderService>.Instance);

        await service.GenerateAsync(new ReportReminderRunOptions(WeekKey: previousKey));
        await service.GenerateAsync(new ReportReminderRunOptions(WeekKey: currentKey));
    }

    /// <summary>
    /// بادئة فضاء المحاكاة: مفاتيح الدورة (2027-Www) واليوم (2027-MM-dd) تبدأ كلّها بسنة المحاكاة.
    /// ضروريّة لعزل العدّ عن أيّ صفوف تخصّ الدورة الحقيقيّة الجارية قد يولّدها مضيف الاختبار بساعته الحقيقيّة.
    /// </summary>
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

    private async Task<int> CountWeeklyDueAsync(Guid userId) => await CountByEventAsync("report-weekly-due", userId);

    private async Task<int> CountDailyDueAsync(Guid userId) => await CountByEventAsync("report-daily-due", userId);

    /// <summary>الأربعة: موظّف/قائد فريق/مدير/مدير عام — كلّهم أسبوعيّون ومتوقَّع منهم تقرير.</summary>
    private async Task<(Guid Employee, Guid TeamLeader, Guid Manager, Guid GeneralManager)> CreateFourRolesAsync() =>
    (
        await CreateReportingUserAsync("Employee"),
        await CreateReportingUserAsync("TeamLeader"),
        await CreateReportingUserAsync("Manager"),
        await CreateReportingUserAsync("GeneralManager")
    );

    /// <summary>يوم من دورة المحاكاة (0 = السبت … 6 = الجمعة)، مع إزاحة اختيارية بالدورات.</summary>
    private static DateOnly Day(int offsetFromCycleStart) => CycleStart.AddDays(offsetFromCycleStart);

    private static string DayKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ===== 1) السبت: تذكير يوميّ للمبيعات فقط، و0 تذكير أسبوعي لأيّ دور =====
    [Fact]
    public async Task Saturday_DailySalesOnly_AndNoWeeklyDueForAnyRole()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var saturday = Day(0);

        await RunAsSchedulerAsync(saturday); // السبت — اليوم صفر للدورة

        // اليوميّ: صفّ واحد لموظّف المبيعات بمفتاح يوم السبت نفسه.
        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(saturday)}:{sales}"));

        // الأسبوعيّ: صفر لكلّ الأدوار (لا إزاحة دور تقع على السبت).
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
        Assert.Equal(0, await CountWeeklyDueAsync(sales));
    }

    // ===== 2) الجمعة: لا يوميّ ولا أسبوعيّ إطلاقًا =====
    [Fact]
    public async Task Friday_NoDailyDue_AndNoWeeklyDue()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();

        await RunAsSchedulerAsync(Day(6)); // الجمعة

        Assert.Equal(0, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
    }

    // ===== 3) الأربعاء: يوميّ للمبيعات + أسبوعيّ للموظّفين فقط =====
    [Fact]
    public async Task Wednesday_DailySales_AndWeeklyEmployeesOnly()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var wednesday = Day(4);

        await RunAsSchedulerAsync(wednesday);

        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(wednesday)}:{sales}"));
        Assert.Equal(1, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
    }

    // ===== 4) الخميس: يوميّ للمبيعات + أسبوعيّ لقادة الفرق فقط =====
    [Fact]
    public async Task Thursday_DailySales_AndWeeklyTeamLeadersOnly()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var thursday = Day(5);

        await RunAsSchedulerAsync(thursday);

        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(thursday)}:{sales}"));
        Assert.Equal(1, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
    }

    // ===== 5) الأحد: يوميّ للمبيعات + أسبوعيّ للمديرين فقط (استحقاق الدورة السابقة = بدايتها + 8) =====
    [Fact]
    public async Task Sunday_DailySales_AndWeeklyManagersOnly()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var sunday = Day(8); // الأحد التالي = يوم استحقاق المدير للدورة السابقة

        await RunAsSchedulerAsync(sunday);

        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(sunday)}:{sales}"));
        Assert.Equal(1, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
    }

    // ===== 6) الاثنين: يوميّ للمبيعات + أسبوعيّ للمدير العام/الرئيس التنفيذي/مدير النظام فقط =====
    [Fact]
    public async Task Monday_DailySales_AndWeeklyExecutivesOnly()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var ceo = await CreateReportingUserAsync("CEO");
        var admin = await CreateReportingUserAsync("Admin");
        var sales = await CreateSalesUserAsync();
        var monday = Day(9); // الاثنين التالي = يوم استحقاق التنفيذيين للدورة السابقة

        await RunAsSchedulerAsync(monday);

        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(monday)}:{sales}"));
        Assert.Equal(1, await CountWeeklyDueAsync(gm));
        Assert.Equal(1, await CountWeeklyDueAsync(ceo));
        Assert.Equal(1, await CountWeeklyDueAsync(admin));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
    }

    // ===== 7) الثلاثاء: يوميّ للمبيعات فقط (لا استحقاق أسبوعيّ لأيّ دور) =====
    [Fact]
    public async Task Tuesday_DailySalesOnly()
    {
        var (emp, tl, mgr, gm) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var tuesday = Day(3);

        await RunAsSchedulerAsync(tuesday);

        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(tuesday)}:{sales}"));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
    }

    // ===== 8) يوم عمل: تذكير يوميّ واحد لليوم نفسه فقط =====
    [Fact]
    public async Task WorkingDay_DailyDue_OnlyForThatDay()
    {
        var sales = await CreateSalesUserAsync();
        var monday = Day(2);

        await RunAsSchedulerAsync(monday); // الاثنين — يوم عمل

        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(monday)}:{sales}"));
    }

    // ===== 9) إعادة التشغيل في نفس اليوم: لا صفّ جديد (Idempotency) — أسبوعيًّا ويوميًّا =====
    [Fact]
    public async Task SameDayRerun_CreatesNoNewRows()
    {
        var (emp, _, _, _) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var wednesday = Day(4);

        await RunAsSchedulerAsync(wednesday);
        var weeklyAfterFirst = await CountWeeklyDueAsync(emp);
        var dailyAfterFirst = await CountDailyDueAsync(sales);
        Assert.Equal(1, weeklyAfterFirst);
        Assert.Equal(1, dailyAfterFirst);

        await RunAsSchedulerAsync(wednesday);
        await RunAsSchedulerAsync(wednesday);

        Assert.Equal(weeklyAfterFirst, await CountWeeklyDueAsync(emp));
        Assert.Equal(dailyAfterFirst, await CountDailyDueAsync(sales));
    }

    // ===== 10) رسالة السبت اليوميّة لا تمنع أيّ رسالة أسبوعية لاحقة =====
    [Fact]
    public async Task SaturdayDailyMessage_DoesNotBlockLaterWeeklyMessages()
    {
        var (emp, tl, _, _) = await CreateFourRolesAsync();
        var sales = await CreateSalesUserAsync();
        var saturday = Day(0);
        var wednesday = Day(4);
        var thursday = Day(5);

        await RunAsSchedulerAsync(saturday);
        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));

        await RunAsSchedulerAsync(wednesday);
        Assert.Equal(1, await CountWeeklyDueAsync(emp));            // الأسبوعيّ لم يُحجَب برسالة السبت
        Assert.Equal(2, await CountDailyDueAsync(sales));           // السبت + الأربعاء

        await RunAsSchedulerAsync(thursday);
        Assert.Equal(1, await CountWeeklyDueAsync(tl));
        Assert.Equal(3, await CountDailyDueAsync(sales));           // + الخميس
    }

    // ===== 11) مفتاحا الترابط اليوميّ والأسبوعيّ فضاءان منفصلان تمامًا =====
    [Fact]
    public async Task DailyAndWeeklyCorrelationKeys_AreSeparateNamespaces()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var sales = await CreateSalesUserAsync();
        var wednesday = Day(4);

        await RunAsSchedulerAsync(wednesday);

        var weeklyDueKey = $"report-weekly-due:{CycleKey}:" +
            $"{DayKey(ReportCalendarPolicy.DueDateForRole(CycleKey, "Employee"))}:{emp}";
        var dailyDueKey = $"report-daily-due:{DayKey(wednesday)}:{sales}";

        Assert.Equal(1, await CountByKeyAsync(weeklyDueKey));
        Assert.Equal(1, await CountByKeyAsync(dailyDueKey));

        // لا تقاطع: الأسبوعيّ لا يُنتج مفتاحًا يوميًّا والعكس صحيح.
        Assert.Equal(0, await CountDailyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(sales));
        Assert.StartsWith("report-weekly-due:", weeklyDueKey);
        Assert.StartsWith("report-daily-due:", dailyDueKey);
    }

    // ===== 12) رسالة مبكّرة لا تحجز مفتاح الموعد الصحيح =====
    // تشغيل يوم السبت لا يُنشئ تذكيرًا أسبوعيًّا، وتشغيل الأربعاء بعده يُنشئه فعلًا (لا Duplicate كاذب).
    [Fact]
    public async Task EarlyRun_DoesNotSquatCorrectDayKey()
    {
        var (emp, _, _, _) = await CreateFourRolesAsync();

        await RunAsSchedulerAsync(Day(0)); // السبت — لا شيء
        Assert.Equal(0, await CountWeeklyDueAsync(emp));

        await RunAsSchedulerAsync(Day(4)); // الأربعاء — يوم الدور

        Assert.Equal(1, await CountWeeklyDueAsync(emp));
        var dueKey = DayKey(ReportCalendarPolicy.DueDateForRole(CycleKey, "Employee"));
        Assert.Equal(1, await CountByKeyAsync($"report-weekly-due:{CycleKey}:{dueKey}:{emp}"));
    }

    // ===== 13) التأخّر لا يظهر قبل انقضاء يوم الاستحقاق =====
    [Fact]
    public async Task Overdue_NotEmittedBeforeDueDayElapses()
    {
        var emp = await CreateReportingUserAsync("Employee");

        // مفتاح ترابط التأخّر ينتهي بنوع التأخير لا بمعرّف المستخدم ⇒ تطابق تامّ لا لاحقة،
        // وهو أيضًا يعزل دورة المحاكاة عن صفّ الدورة السابقة الذي يُنشئه تشغيل المجدول للدورتين.
        var overdueKey = $"report-overdue:{CycleKey}:{emp}:{DelayType.EmployeeReportNotSubmitted}";

        await RunAsSchedulerAsync(Day(4)); // الأربعاء = يوم الاستحقاق نفسه
        Assert.Equal(0, await CountByKeyAsync(overdueKey));

        await RunAsSchedulerAsync(Day(5)); // الخميس = بعد انقضائه
        Assert.Equal(1, await CountByKeyAsync(overdueKey));
    }

    // ===== 14) الملخّص لا يظهر قبل وجود متأخّر فعليّ =====
    [Fact]
    public async Task DepartmentSummary_NotEmittedBeforeAnyoneIsOverdue()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var manager = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(manager, emp);

        // تطابق تامّ على مفتاح دورة المحاكاة: تشغيل المجدول يشمل الدورة السابقة أيضًا، فملخّصها
        // يحمل مفتاحًا آخر ولا يجوز أن يُحتسب هنا.
        var summaryKey = $"report-department-overdue-summary:{CycleKey}:{manager}";

        await RunAsSchedulerAsync(Day(4)); // الأربعاء — لا متأخّر بعد
        Assert.Equal(0, await CountByKeyAsync(summaryKey));

        await RunAsSchedulerAsync(Day(5)); // الخميس — الموظّف صار متأخّرًا
        Assert.Equal(1, await CountByKeyAsync(summaryKey));
    }

    // ===== 15) وضع DryRun لا يستدعي قناة SMTP إطلاقًا =====
    [Fact]
    public async Task DryRun_DoesNotCallSmtpSender()
    {
        var emp = await CreateReportingUserAsync("Employee");
        var sales = await CreateSalesUserAsync();
        var wednesday = Day(4);

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
                new FixedClock(RiyadhMorning(wednesday)),
                NullLogger<ReportReminderService>.Instance);

            await service.GenerateAsync(new ReportReminderRunOptions(WeekKey: CycleKey));
        }

        // أُنشئت صفوف فعلًا (فالمسار عمل)، لكن بلا أيّ استدعاء إرسال.
        Assert.Equal(1, await CountWeeklyDueAsync(emp));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(wednesday)}:{sales}"));
        Assert.Equal(0, spy.SendCount);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prefix = "report-weekly-due:";
        var suffix = $":{emp}";
        var rows = await verifyDb.EmailNotifications.AsNoTracking()
            .Where(n => n.CorrelationKey != null && n.CorrelationKey.StartsWith(prefix) && n.CorrelationKey.EndsWith(suffix))
            .ToListAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.Equal(EmailNotificationStatus.DryRun, r.Status);
            Assert.Null(r.SentAt);
        });
    }

    // ===== مساعدات إضافية =====

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

    private async Task<int> CountByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync(n => n.CorrelationKey == correlationKey);
    }
}

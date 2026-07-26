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
/// عزل الاختبارات (تحصين ضدّ الترتيب على قاعدة <c>reporting_test</c> المشتركة الدائمة):
///   (1) لكلّ اختبار **دورة محاكاة خاصّة به** (سنة مرساة مستقلّة) ⇒ مفاتيح الترابط لا تتقاطع بين الاختبارات إطلاقًا.
///   (2) كلّ عدّ وكلّ توكيد محصور بـ**مستخدمي الاختبار نفسه** وبمفاتيح ترابطه هو — لا عدّادات على مستوى الشركة
///       (العدّادات العامّة تلتقط مستخدمين متراكمين من فئات اختبار أخرى فتصير غير حتميّة).
///   (3) **تنظيف مضمون** في <see cref="DisposeAsync"/>: تُحذف صفوف الإشعارات والقوالب وإصداراتها والإدارات
///       والمسمّيات المُنشأة هنا، ويُفَكّ ارتباط مستخدمي الاختبار بالمسمّى/الإدارة ⇒ يصيرون خاملين تمامًا
///       فلا يُنتِجون صفوفًا في أيّ تشغيل لاحق.
///
/// كلّ الاختبارات تحقن <see cref="ISystemClock"/> ثابتة ⇒ محاكاة اليوم والساعة حتميّة بلا اعتماد على ساعة النظام.
/// </summary>
[Collection("Integration")]
public class SplitDeliveryWindowsTests : IAsyncLifetime
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

    // ===== دورة محاكاة معزولة لكلّ اختبار =====

    /// <summary>
    /// دورة محاكاة (السبت→الجمعة) مشتقّة من سنة مرساة مستقلّة لكلّ اختبار. المرساة في مايو دائمًا
    /// ⇒ الدورة وسابقتها ولاحقتها كلّها داخل السنة نفسها، فبادئة السنة تكفي لعزل فضاء المفاتيح.
    /// </summary>
    private sealed class SimCycle
    {
        public SimCycle(int anchorYear) => Start = ReportCalendarPolicy.WeekStart(new DateOnly(anchorYear, 5, 17));

        /// <summary>سبت بداية الدورة المرساة.</summary>
        public DateOnly Start { get; }

        /// <summary>مفتاح الدورة المرساة (YYYY-Www).</summary>
        public string Key => ReportCalendarPolicy.WeekKeyFor(Start);

        /// <summary>يوم من دورة المحاكاة (0 = السبت … 6 = الجمعة)، ويجوز تجاوز 6 للدورة التالية.</summary>
        public DateOnly Day(int offsetFromStart) => Start.AddDays(offsetFromStart);

        /// <summary>بادئة فضاء المحاكاة — كلّ مفاتيح هذه الدورة (دورةً ويومًا) تحملها.</summary>
        public string YearSegment => $"{Start.Year}-";
    }

    private SimCycle _cycle = new(2041);

    /// <summary>يحجز دورة المحاكاة الخاصّة بهذا الاختبار. يُستدعى في أوّل سطر من كلّ اختبار.</summary>
    private SimCycle UseCycle(int anchorYear) => _cycle = new SimCycle(anchorYear);

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

    // ===== سجلّ ما أُنشئ (للتنظيف المضمون) =====

    private readonly List<Guid> _userIds = new();
    private readonly List<Guid> _jobRoleIds = new();
    private readonly List<Guid> _templateIds = new();
    private readonly List<Guid> _departmentIds = new();

    /// <summary>قالب المبيعات المشترك داخل هذا الاختبار (المسمّى SALES_* مشترك عالميًّا، أمّا القالب فمِلك الاختبار).</summary>
    private Guid? _sharedCadenceTemplateId;

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
            // مسمّى خاصّ بهذا المستخدم وحده ⇒ يُحذف في التنظيف.
            var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}" };
            db.JobRoles.Add(jobRole);
            await db.SaveChangesAsync();
            jobRoleId = jobRole.Id;
            _jobRoleIds.Add(jobRoleId);
        }
        else
        {
            // رمز الوتيرة (SALES_*) مقيَّد بفهرس فريد ⇒ get-or-create ولا يُحذف، ويكفيه قالب واحد لكلّ الاختبار.
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
            db.ReportTemplateVersions.Add(new ReportTemplateVersion
            {
                ReportTemplateId = template.Id,
                VersionNumber = 1,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow
            });
            _templateIds.Add(template.Id);
            if (cadenceCode is not null) _sharedCadenceTemplateId = template.Id;
        }

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
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
        _departmentIds.Add(dept.Id);
    }

    // ===== مساعدات عدّ (كلّها محصورة بمستخدمي هذا الاختبار وبسنة محاكاته) =====

    private async Task<int> CountByKeyAsync(string correlationKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking().CountAsync(n => n.CorrelationKey == correlationKey);
    }

    private async Task<int> CountByEventAsync(string eventType, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prefix = $"{eventType}:{_cycle.YearSegment}";
        // الفلترة تبدأ بـ RecipientUserId (مفهرس) ثمّ تُضيَّق بسنة المحاكاة —
        // الفلترة النصّية وحدها تمسح الجدول المشترك بالكامل (ملايين الصفوف المتراكمة).
        return await db.EmailNotifications.AsNoTracking().CountAsync(n =>
            n.RecipientUserId == userId && n.CorrelationKey.StartsWith(prefix));
    }

    private Task<int> CountWeeklyDueAsync(Guid userId) => CountByEventAsync("report-weekly-due", userId);

    private Task<int> CountDailyDueAsync(Guid userId) => CountByEventAsync("report-daily-due", userId);

    /// <summary>مفتاح تأخّر الموظّف لدورة المحاكاة (تطابق تامّ — لا يلتقط دورة أخرى).</summary>
    private static string OverdueKey(string cycleKey, Guid userId) =>
        $"report-overdue:{cycleKey}:{userId}:{DelayType.EmployeeReportNotSubmitted}";

    /// <summary>
    /// إجماليّ صفوف الإشعارات **الخاصّة بهذا الاختبار وحده**: مفاتيح تحمل سنة محاكاته ومعرّف أحد مستخدميه.
    /// هذا بديل العدّادات على مستوى الشركة — تلك تلتقط مستخدمين متراكمين من فئات أخرى فتصير غير حتميّة.
    /// </summary>
    private async Task<int> CountOwnSimulationRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var segment = $":{_cycle.YearSegment}";
        var ids = _userIds.ToList();
        if (ids.Count == 0) return 0;
        return await db.EmailNotifications.AsNoTracking().CountAsync(n =>
            n.RecipientUserId != null && ids.Contains(n.RecipientUserId.Value)
            && n.CorrelationKey.Contains(segment));
    }

    /// <summary>كلّ صفوف المستخدم أيًّا كان فضاء المفاتيح (يُستعمل لإثبات «لا شيء إطلاقًا»).</summary>
    private async Task<int> CountAllForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailNotifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId);
    }

    // ===== 1) الأحد 09:00 — أسبوعيّ للمديرين + تأخّر وملخّصات، و0 تذكير يوميّ =====
    [Fact]
    public async Task Sunday_At09_WeeklyManagersAndOverdueAndSummaries_NoDailyDue()
    {
        var cycle = UseCycle(2041);

        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var sales = await CreateSalesUserAsync();
        await CreateDepartmentWithManagerAsync(mgr, emp);

        var sunday = cycle.Day(8); // الأحد التالي = يوم استحقاق المدير للدورة السابقة

        await RunWindowAsync(sunday, 9);

        // الأسبوعيّ: المدير فقط.
        Assert.Equal(1, await CountWeeklyDueAsync(mgr));
        Assert.Equal(0, await CountWeeklyDueAsync(gm));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));

        // التأخّر الفرديّ والملخّصات مسموحان في نافذة 09.
        Assert.Equal(1, await CountByKeyAsync(OverdueKey(cycle.Key, emp)));
        Assert.Equal(1, await CountByKeyAsync($"report-department-overdue-summary:{cycle.Key}:{mgr}"));

        // اليوميّ ممنوع في نافذة 09 — لا صفّ إطلاقًا.
        Assert.Equal(0, await CountDailyDueAsync(sales));
        Assert.Equal(0, await CountByKeyAsync($"report-daily-due:{DayKey(sunday)}:{sales}"));
    }

    // ===== 2) الأحد 16:00 — تذكير يوميّ للمبيعات فقط (خمسة مستلمين)، و0 أسبوعيّ/تأخّر/ملخّصات =====
    [Fact]
    public async Task Sunday_At16_DailySalesOnly_NoWeeklyDue()
    {
        var cycle = UseCycle(2042);

        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var salesTeam = await CreateSalesUsersAsync(5);

        var sunday = cycle.Day(8);

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
        Assert.Equal(0, await CountByKeyAsync(OverdueKey(cycle.Key, emp)));
        Assert.Equal(0, await CountByKeyAsync($"report-department-overdue-summary:{cycle.Key}:{mgr}"));
    }

    // ===== 3) الاثنين 09:00 — أسبوعيّ للتنفيذيين فقط، و0 تذكير يوميّ =====
    [Fact]
    public async Task Monday_At09_WeeklyExecutivesOnly_NoDailyDue()
    {
        var cycle = UseCycle(2043);

        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var ceo = await CreateReportingUserAsync("CEO");
        var admin = await CreateReportingUserAsync("Admin");
        var sales = await CreateSalesUserAsync();

        var monday = cycle.Day(9); // الاثنين التالي = يوم استحقاق التنفيذيين للدورة السابقة

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
        var cycle = UseCycle(2044);

        var gm = await CreateReportingUserAsync("GeneralManager");
        var ceo = await CreateReportingUserAsync("CEO");
        var sales = await CreateSalesUserAsync();

        var monday = cycle.Day(9);

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
        var cycle = UseCycle(2045);

        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var mgr = await CreateReportingUserAsync("Manager");
        var gm = await CreateReportingUserAsync("GeneralManager");
        var sales = await CreateSalesUserAsync();

        var friday = cycle.Day(6);

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
        var cycle = UseCycle(2046);

        var emp = await CreateReportingUserAsync("Employee");
        var tl = await CreateReportingUserAsync("TeamLeader");
        var sales = await CreateSalesUserAsync();

        var saturday = cycle.Day(0);

        await RunWindowAsync(saturday, 16);

        Assert.Equal(1, await CountDailyDueAsync(sales));
        Assert.Equal(1, await CountByKeyAsync($"report-daily-due:{DayKey(saturday)}:{sales}"));
        Assert.Equal(0, await CountWeeklyDueAsync(emp));
        Assert.Equal(0, await CountWeeklyDueAsync(tl));
        Assert.Equal(0, await CountWeeklyDueAsync(sales));
    }

    // ===== 7) إعادة التشغيل داخل النافذة نفسها ⇒ لا صفّ جديد لمستخدمي الاختبار وكلّ رسائلهم مُكرّرة =====
    [Fact]
    public async Task SameWindowRerun_CreatesNothing_AndCountsDuplicates()
    {
        var cycle = UseCycle(2047);

        var emp = await CreateReportingUserAsync("Employee");
        var mgr = await CreateReportingUserAsync("Manager");
        await CreateDepartmentWithManagerAsync(mgr, emp);
        var sales = await CreateSalesUserAsync();

        var sunday = cycle.Day(8);

        // نافذة 09: التشغيل الأول يُنشئ فعلًا لمستخدمي هذا الاختبار.
        await RunWindowAsync(sunday, 9);
        var ownAfterFirst09 = await CountOwnSimulationRowsAsync();
        Assert.True(ownAfterFirst09 >= 1, "يجب أن تُنشئ نافذة 09 صفًّا واحدًا على الأقلّ لمستخدمي هذا الاختبار.");
        var weeklyAfterFirst = await CountWeeklyDueAsync(mgr);
        Assert.Equal(1, weeklyAfterFirst);

        // إعادة التشغيل في النافذة نفسها: لا صفّ جديد، وكلّ رسائل الاختبار تُصنَّف مُكرّرة.
        var second09 = await RunWindowAsync(sunday, 9);
        Assert.Equal(ownAfterFirst09, await CountOwnSimulationRowsAsync());
        Assert.True(second09.Duplicate >= ownAfterFirst09,
            "يجب أن تُحتسَب كلّ رسائل مستخدمي هذا الاختبار كمُكرّرة عند إعادة التشغيل.");
        Assert.Equal(weeklyAfterFirst, await CountWeeklyDueAsync(mgr));

        // نافذة 16: نفس السلوك تمامًا، مستقلّة عن نافذة 09.
        await RunWindowAsync(sunday, 16);
        var ownAfterFirst16 = await CountOwnSimulationRowsAsync();
        Assert.True(ownAfterFirst16 > ownAfterFirst09, "يجب أن تُضيف نافذة 16 رسالة المبيعات اليوميّة.");
        Assert.Equal(1, await CountDailyDueAsync(sales));

        var second16 = await RunWindowAsync(sunday, 16);
        Assert.Equal(ownAfterFirst16, await CountOwnSimulationRowsAsync());
        Assert.True(second16.Duplicate >= 1);
        Assert.Equal(1, await CountDailyDueAsync(sales));
    }

    // ===== 8) تشغيل 09:00 لا يحجز مفتاح رسالة اليوميّ الخاصّة بـ16:00 =====
    [Fact]
    public async Task Run_At09_DoesNotSquat_DailyDueKeyOf16()
    {
        var cycle = UseCycle(2048);

        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
        var dailyKey = $"report-daily-due:{DayKey(sunday)}:{sales}";

        await RunWindowAsync(sunday, 9);
        Assert.Equal(0, await CountByKeyAsync(dailyKey));   // لم يُحجَز المفتاح
        Assert.Equal(0, await CountDailyDueAsync(sales));

        await RunWindowAsync(sunday, 16);
        Assert.Equal(1, await CountByKeyAsync(dailyKey));   // أُنشئ فعلًا — ولا Duplicate كاذب
        Assert.Equal(1, await CountDailyDueAsync(sales));
    }

    // ===== 9) وضع DryRun لا يستدعي قناة SMTP إطلاقًا (نافذة 16) =====
    [Fact]
    public async Task DryRun_InDailyWindow_DoesNotCallSmtpSender()
    {
        var cycle = UseCycle(2049);

        var sales = await CreateSalesUserAsync();
        var sunday = cycle.Day(8);
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
        var key = $"report-daily-due:{DayKey(sunday)}:{sales}";
        Assert.Equal(1, await CountByKeyAsync(key));
        Assert.Equal(0, spy.SendCount);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await verifyDb.EmailNotifications.AsNoTracking().FirstAsync(n => n.CorrelationKey == key);
        Assert.Equal(EmailNotificationStatus.DryRun, row.Status);
        Assert.Null(row.SentAt);
    }

    // ===== 10) المجدول معطّل ⇒ لا تشغيل ولا صفّ في أيّ نافذة =====
    [Fact]
    public async Task SchedulerDisabled_GeneratesNoRowsInAnyWindow()
    {
        UseCycle(2050);

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

    // ===== التنظيف المضمون =====

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// يمحو كلّ أثر لهذا الاختبار من القاعدة المشتركة الدائمة، بالترتيب الذي يحترم المفاتيح الأجنبية:
    /// صفوف الإشعارات ⇐ فكّ ارتباط المستخدمين بالمسمّى/الإدارة ⇐ إصدارات القوالب ⇐ القوالب ⇐ الإدارات ⇐ المسمّيات.
    /// حسابات Identity تبقى (كبقيّة الحزمة) لكنّها تصير **خاملة تمامًا**: بلا مسمّى ⇒ لا تقرير متوقَّع منها أبدًا.
    /// أفضل-جهد: فشل التنظيف لا يجوز أن يُفشِل اختبارًا ناجحًا.
    /// </summary>
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

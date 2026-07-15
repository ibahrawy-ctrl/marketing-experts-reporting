using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Calendar;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ROLE-AWARE-REPORTING-CALENDAR — الوضع اليوميّ (Daily). اختبارات تكامل على قاعدة معزولة مؤقّتة
/// (reporting_calendar_iso) — لا تمسّ قاعدة الاختبارات المشتركة إطلاقًا. تُثبِت أن الخادم هو مصدر الحقيقة
/// الوحيد للأيام: GET /api/reporting-calendar/my-days يُرجِع نافذةً محسوبةً خادميًّا مع حالة كل يوم من قاعدة
/// البيانات، وأن إنشاء تقرير يوميّ يُرفَض لمفتاح غير صالح/مستقبليّ/عطلة، ويُقبَل بمفتاح خادميّ صالح.
/// </summary>
[Collection("CalendarIsolated")]
public class DailyCalendarTests
{
    private readonly CalendarIsolatedFactory _factory;

    public DailyCalendarTests(CalendarIsolatedFactory factory) => _factory = factory;

    // الحالات الثمانى المسموحة ليوم واحد (حالة واحدة بالضبط لكل يوم).
    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "Available", "Draft", "Submitted", "Overdue", "Holiday", "FutureLocked", "Returned", "Reopened",
    };

    // أقرب يوم عمل (غير عطلة) قبل التاريخ المعطى.
    private static DateOnly PrevWeekday(DateOnly d)
    {
        do { d = d.AddDays(-1); } while (ReportingCalendarPolicy.IsDailyHoliday(d));
        return d;
    }

    private static async Task<(Guid TemplateId, Guid GridId)> GetSeededB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var grid = Assert.Single(version.Fields.Where(f => f.FieldType == FieldType.TableGrid));
        return (detail.Id, grid.Id);
    }

    private static async Task AssignTemplateToEmployeeAsync(HttpClient admin, Guid templateId, Guid employeeId)
    {
        var res = await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, employeeId, TemplateAssignmentKind.Include, null));
        res.EnsureSuccessStatusCode();
    }

    private static string[] B2cRow(string course, int work, int leads, int contacted, int qualified,
        int follow, int sales, int revenue, int lost)
        => new[] { course, work.ToString(), leads.ToString(), contacted.ToString(), qualified.ToString(),
                   follow.ToString(), sales.ToString(), revenue.ToString(), lost.ToString(), "" };

    // ---------- my-days: بنية النتيجة ----------

    [Fact]
    public async Task MyDays_Returns200_WithWindowStructure_AndSingleTodaySelected()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await employee.GetAsync("/api/reporting-calendar/my-days");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = await res.ReadAsync<MyDaysDto>();
        Assert.NotNull(data);
        Assert.NotEmpty(data!.Days);
        Assert.False(string.IsNullOrWhiteSpace(data.CurrentDayKey));
        Assert.False(string.IsNullOrWhiteSpace(data.RoleLabel));

        // اليوم الحاليّ محدَّد بالضبط مرّة واحدة، ومفتاحه = CurrentDayKey، وليس مستقبلًا.
        var today = Assert.Single(data.Days.Where(d => d.IsToday));
        Assert.Equal(data.CurrentDayKey, today.DayKey);
        Assert.False(today.IsFuture);

        // كل يوم يحمل حالةً واحدة ضمن المجموعة المسموحة، ومفاتيح الجوار خادميّة صحيحة.
        foreach (var d in data.Days)
        {
            Assert.Contains(d.Status, AllowedStatuses);
            Assert.Equal(ReportingCalendarPolicy.DayKey(ReportingCalendarPolicy.ParseDayKey(d.DayKey)), d.DayKey);
            Assert.Equal(ReportingCalendarPolicy.PreviousDayKey(d.Date), d.PreviousDayKey);
            Assert.Equal(ReportingCalendarPolicy.NextDayKey(d.Date), d.NextDayKey);
        }
    }

    [Fact]
    public async Task MyDays_HolidayDay_IsNotSelectable_AndFutureDay_IsLocked()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        // نافذة أوسع لضمان احتواء عطلة (الجمعة وحدها) ويوم مستقبليّ.
        var data = await (await employee.GetAsync(
            "/api/reporting-calendar/my-days?previousCount=10&nextCount=5")).ReadAsync<MyDaysDto>();

        var holiday = data!.Days.FirstOrDefault(d => d.IsHoliday);
        Assert.NotNull(holiday);
        Assert.False(holiday!.IsSelectable);
        Assert.Equal("Holiday", holiday.Status);
        Assert.NotNull(holiday.LockReason);

        var future = data.Days.FirstOrDefault(d => d.IsFuture);
        Assert.NotNull(future);
        Assert.False(future!.IsSelectable);
        Assert.Equal("FutureLocked", future.Status);
        Assert.NotNull(future.LockReason);
    }

    [Fact]
    public async Task MyDays_InvalidAnchor_Returns400()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync("/api/reporting-calendar/my-days?anchorDate=2026-13-99");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task MyDays_Reflects_Submitted_Draft_Overdue_FromDatabase()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        var today = ReportingCalendarPolicy.RiyadhToday();
        var submittedDay = PrevWeekday(today);          // يوم عمل ماضٍ → سنُرسِله
        var overdueDay = PrevWeekday(submittedDay);     // يوم عمل ماضٍ آخر بلا تسليم → متأخّر

        // (1) يوم ماضٍ مُرسَل: أنشئ مسودّة، املأ الشبكة، ثم أرسِل (بلا اعتماد) ⇒ الحالة Submitted.
        var subDraft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, ReportingCalendarPolicy.DayKey(submittedDay))))
            .ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[] { B2cRow("دورة الاختبار", 8, 30, 20, 12, 6, 4, 12000, 2) });
        (await employee.PutAsJsonAsync($"/api/submissions/{subDraft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(gridId, null, null, null, null, gridJson) })))
            .EnsureSuccessStatusCode();
        (await employee.PostAsync($"/api/submissions/{subDraft.Id}/submit", null)).EnsureSuccessStatusCode();

        // (2) اليوم الحاليّ: مسودّة غير مُرسَلة (إن لم يكن اليوم عطلة) ⇒ الحالة Draft.
        var todayIsWorkday = !ReportingCalendarPolicy.IsDailyHoliday(today);
        if (todayIsWorkday)
        {
            (await employee.PostAsJsonAsync("/api/submissions",
                new CreateSubmissionRequest(templateId, PeriodType.Daily, ReportingCalendarPolicy.DayKey(today))))
                .EnsureSuccessStatusCode();
        }

        // اقرأ النافذة مرساةً على اليوم الحاليّ بنافذة ماضية كافية لاحتواء اليومين الماضيين.
        var data = await (await employee.GetAsync(
            "/api/reporting-calendar/my-days?previousCount=15&nextCount=2")).ReadAsync<MyDaysDto>();

        var sub = Assert.Single(data!.Days.Where(d => d.DayKey == ReportingCalendarPolicy.DayKey(submittedDay)));
        Assert.True(sub.IsSubmitted);
        Assert.Equal("Submitted", sub.Status);

        var over = Assert.Single(data.Days.Where(d => d.DayKey == ReportingCalendarPolicy.DayKey(overdueDay)));
        Assert.True(over.IsOverdue);
        Assert.False(over.IsSubmitted);
        Assert.Equal("Overdue", over.Status);

        if (todayIsWorkday)
        {
            var td = Assert.Single(data.Days.Where(d => d.IsToday));
            Assert.True(td.HasDraft);
            Assert.Equal("Draft", td.Status);
        }
    }

    // ---------- إنشاء التقرير اليوميّ: التحقّق الخادميّ ----------

    [Fact]
    public async Task CreateDaily_WithServerIssuedKey_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await GetSeededB2cTemplateAsync(admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // نأخذ المفتاح من الخادم حصرًا (يوم قابل للإنشاء عليه) لا من حساب محليّ.
        var days = await (await employee.GetAsync("/api/reporting-calendar/my-days")).ReadAsync<MyDaysDto>();
        var selectable = days!.Days.First(d => d.IsOpenForDraft);

        var res = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, selectable.DayKey));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, dto!.Status);
        Assert.Equal(selectable.DayKey, dto.PeriodKey);
    }

    [Fact]
    public async Task CreateDaily_InvalidManualKey_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await GetSeededB2cTemplateAsync(admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        var res = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, "2026-02-30"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("report.daily_key_invalid", body);
    }

    [Fact]
    public async Task CreateDaily_FutureDay_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await GetSeededB2cTemplateAsync(admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // يوم مستقبليّ يوم عمل (لا عطلة) كي يُطلَق حارسُ المستقبل لا حارسُ العطلة.
        var future = ReportingCalendarPolicy.RiyadhToday().AddDays(1);
        while (ReportingCalendarPolicy.IsDailyHoliday(future)) future = future.AddDays(1);
        var res = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, ReportingCalendarPolicy.DayKey(future)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("calendar.future_day_locked", body);
    }

    [Fact]
    public async Task CreateDaily_Holiday_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await GetSeededB2cTemplateAsync(admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // أقرب عطلة ماضية (الجمعة وحدها) قبل اليوم — ماضية كي لا يسبق حارسُ المستقبل حارسَ العطلة.
        var day = ReportingCalendarPolicy.RiyadhToday();
        do { day = day.AddDays(-1); } while (!ReportingCalendarPolicy.IsDailyHoliday(day));
        // تصحيح السبت: العطلة الوحيدة الآن هي الجمعة.
        Assert.Equal(DayOfWeek.Friday, day.DayOfWeek);

        var res = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, ReportingCalendarPolicy.DayKey(day)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("calendar.day_is_holiday", body);
    }

    // ---------- تصحيح السبت الإلزاميّ: السبت يوم عمل يوميّ كامل، الجمعة وحدها عطلة ----------

    // أقرب سبت ماضٍ (يوم عمل يوميّ) قبل اليوم.
    private static DateOnly PrevSaturday(DateOnly today)
    {
        var d = today;
        do { d = d.AddDays(-1); } while (d.DayOfWeek != DayOfWeek.Saturday);
        return d;
    }

    // أقرب جمعة ماضية (عطلة) قبل اليوم.
    private static DateOnly PrevFriday(DateOnly today)
    {
        var d = today;
        do { d = d.AddDays(-1); } while (d.DayOfWeek != DayOfWeek.Friday);
        return d;
    }

    [Fact]
    public async Task CreateDaily_Saturday_WithServerKey_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await GetSeededB2cTemplateAsync(admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // السبت يوم عمل يوميّ كامل ⇒ إنشاء تقرير ليوم سبت ماضٍ يجب أن ينجح (لا يُرفَض كعطلة).
        var saturday = PrevSaturday(ReportingCalendarPolicy.RiyadhToday());
        Assert.Equal(DayOfWeek.Saturday, saturday.DayOfWeek);
        Assert.False(ReportingCalendarPolicy.IsDailyHoliday(saturday));

        var res = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, ReportingCalendarPolicy.DayKey(saturday)));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, dto!.Status);
        Assert.Equal(ReportingCalendarPolicy.DayKey(saturday), dto.PeriodKey);
    }

    [Fact]
    public async Task MyDays_Saturday_IsWorking_AndFriday_IsHoliday()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        // نافذة ماضية واسعة تضمن احتواء سبت وجمعة.
        var data = await (await employee.GetAsync(
            "/api/reporting-calendar/my-days?previousCount=14&nextCount=2")).ReadAsync<MyDaysDto>();

        var saturday = ReportingCalendarPolicy.DayKey(PrevSaturday(ReportingCalendarPolicy.RiyadhToday()));
        var friday = ReportingCalendarPolicy.DayKey(PrevFriday(ReportingCalendarPolicy.RiyadhToday()));

        // السبت يوم عمل: ليس Holiday ولا معلَّم عطلة.
        var sat = Assert.Single(data!.Days.Where(d => d.DayKey == saturday));
        Assert.False(sat.IsHoliday);
        Assert.NotEqual("Holiday", sat.Status);

        // الجمعة عطلة: Holiday، غير قابلة للاختيار.
        var fri = Assert.Single(data.Days.Where(d => d.DayKey == friday));
        Assert.True(fri.IsHoliday);
        Assert.Equal("Holiday", fri.Status);
        Assert.False(fri.IsSelectable);
    }
}

using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Obligations;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-HR-008 — سطح محرّك الالتزامات: التخويل الصريح، وتمييز 404 عن 403، والقواعد غير القابلة
/// للتفاوض (لا إسناد ⇒ لا تأخّر أبدًا · الإجازة المعتمَدة تُعفي بدل أن تُراكم نقصًا).
/// كلّ ما هنا قراءة واشتقاق؛ البذور تُكتَب في قاعدة المرحلة الثانية المعزولة فقط.
/// </summary>
[Collection("Phase2")]
public class ObligationsApiTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public ObligationsApiTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private const int Cycles = 4;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static IEnumerable<JsonElement> Items(JsonElement root) =>
        root.GetProperty("items").EnumerateArray();

    private static IEnumerable<JsonElement> For(JsonElement root, Guid sourceId) =>
        Items(root).Where(i => i.GetProperty("sourceId").GetGuid() == sourceId);

    /// <summary>مفاتيح الدورات التي يُرجِعها النداء بالترتيب نفسه الذي تحسبه الخدمة.</summary>
    private static IReadOnlyList<string> RecentKeys() =>
        ReportingCalendarPolicy.RecentCycleKeys(ReportingCalendarPolicy.RiyadhToday(), Cycles)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

    // ===================== ① التخويل: مفتاح صريح لا دور ضمنيّ =====================

    [Fact]
    public async Task Scope_Endpoint_Needs_An_Explicit_Permission_That_No_Role_Grants_Implicitly()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (admin, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);

        // غياب المفتاح العامّ قبل تحديد أيّ مورد ⇒ 403 (لا كشف عن مورد بعينه).
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/obligations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/obligations")).StatusCode);
        // ولا حتّى Admin يملكها ضمنًا — المفتاح مطالبة مستقلّة.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/obligations")).StatusCode);
    }

    [Fact]
    public async Task Holder_Of_The_Explicit_Permission_Gets_A_Well_Formed_Result()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        var res = await viewer.GetAsync($"/api/obligations?recentCycles={Cycles}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.Equal(RecentKeys(), body.GetProperty("periodKeys").EnumerateArray()
            .Select(k => k.GetString()!).ToList());
        Assert.True(body.TryGetProperty("summary", out _));
    }

    // ===================== ② خارج النطاق ⇒ 404 لا 403 =====================

    [Fact]
    public async Task Out_Of_Scope_Employee_Is_Indistinguishable_From_A_Nonexistent_One()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);

        // موظّف حقيقيّ تابع لقائد آخر تمامًا.
        var (_, strangerLeaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, strangerId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: strangerLeaderId);

        var outOfScope = await leader.GetAsync($"/api/obligations?userId={strangerId}");
        var nonexistent = await leader.GetAsync($"/api/obligations?userId={Guid.NewGuid()}");

        // 404 في الحالتين — ولا فرق في الرمز ولا في النصّ، فلا يُستدلّ على وجود الموظّف.
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);

        // نقارن الحقول الحاملة للمعنى فقط: traceId يتغيّر لكلّ طلب بطبيعته ولا يصف الموظّف.
        static async Task<string> ShapeAsync(HttpResponseMessage res)
        {
            var body = await JsonAsync(res);
            var fields = body.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={p.Value}");
            return string.Join("|", fields);
        }

        Assert.Equal(await ShapeAsync(nonexistent), await ShapeAsync(outOfScope));
    }

    [Fact]
    public async Task In_Scope_Employee_Is_Readable_By_The_Permission_Holder()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var res = await leader.GetAsync($"/api/obligations?userId={employeeId}&recentCycles={Cycles}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // لا صفّ عن غير المطلوب — النطاق يُضيَّق إلى الموظّف المحدَّد.
        Assert.All(Items(await JsonAsync(res)),
            i => Assert.Equal(employeeId, i.GetProperty("subjectUserId").GetGuid()));
    }

    // ===================== ③ المسار الذاتيّ: حقّ أصيل بلا مفتاح =====================

    [Fact]
    public async Task Self_Endpoint_Works_Without_The_Hr_Key_And_Ignores_A_Client_Supplied_UserId()
    {
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, otherId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // محاولة انتحال صريحة عبر الاستعلام — تُتجاهَل خادميًّا لا تُرفَض بصمت.
        var res = await employee.GetAsync($"/api/obligations/me?userId={otherId}&recentCycles={Cycles}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.All(Items(await JsonAsync(res)),
            i => Assert.Equal(employeeId, i.GetProperty("subjectUserId").GetGuid()));
    }

    // ===================== ④ لا إسناد ⇒ لا التزام ولا تأخّر أبدًا =====================

    [Fact]
    public async Task An_Employee_With_No_Assignment_Is_Never_Counted_Missing_Or_Late()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, includedId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);
        var (_, excludedId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, _) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: includedId, excludeUserId: excludedId);

        var included = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={includedId}&recentCycles={Cycles}"));
        var excluded = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={excludedId}&recentCycles={Cycles}"));

        // المُسنَد إليه: التزام قائم لكلّ دورة.
        Assert.Equal(Cycles, For(included, templateId).Count());

        // المستثنى صراحةً: لا صفّ إطلاقًا — لا Missing ولا Late ولا حتّى Exempt.
        Assert.Empty(For(excluded, templateId));

        // وعدد المتأخّرين لا يتضخّم بعدد أعضاء الفريق: من لا إسناد له ليس رقمًا في المقام.
        Assert.DoesNotContain(For(excluded, templateId), i => i.GetProperty("missing").GetBoolean());
    }

    // ===================== ⑤ التأخّر والإنجاز على تقييمات KPI =====================

    [Fact]
    public async Task An_Overdue_Cycle_Is_Missing_And_A_Submitted_One_Is_Fulfilled()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, versionId) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var keys = RecentKeys();
        var oldest = keys[0];                 // متجاوِزة المهلة قطعًا (٣ دورات مضت).
        var fulfilledKey = keys[1];

        await SeedEvaluationAsync(versionId, employeeId, fulfilledKey,
            KpiEvaluationStatus.Submitted, DateTime.UtcNow.AddDays(-7));

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var rows = For(body, templateId).ToDictionary(i => i.GetProperty("periodKey").GetString()!);

        var missing = rows[oldest];
        Assert.True(missing.GetProperty("expected").GetBoolean());
        Assert.True(missing.GetProperty("missing").GetBoolean());
        Assert.True(missing.GetProperty("late").GetBoolean());
        Assert.True(missing.GetProperty("lateByDays").GetInt32() > 0);
        Assert.Equal("Missing", missing.GetProperty("state").GetString());

        var done = rows[fulfilledKey];
        Assert.True(done.GetProperty("fulfilled").GetBoolean());
        Assert.False(done.GetProperty("missing").GetBoolean());
        Assert.Equal("Fulfilled", done.GetProperty("state").GetString());

        // المالك = المُقيِّم (المدير المباشر)، لا الموظّف موضوع التقييم.
        Assert.Equal(leaderId, missing.GetProperty("ownerUserId").GetGuid());
        Assert.Equal(nameof(KpiTemplateAssignment), missing.GetProperty("sourceKind").GetString());
    }

    [Fact]
    public async Task A_Draft_Evaluation_Is_Not_An_Achievement()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, versionId) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var oldest = RecentKeys()[0];
        await SeedEvaluationAsync(versionId, employeeId, oldest, KpiEvaluationStatus.Draft, null);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var row = For(body, templateId).Single(i => i.GetProperty("periodKey").GetString() == oldest);

        Assert.False(row.GetProperty("fulfilled").GetBoolean());
        Assert.True(row.GetProperty("missing").GetBoolean());
    }

    // ===================== ⑥ الإعفاء بالإجازة المعتمَدة =====================

    [Fact]
    public async Task An_Approved_Leave_Covering_The_Whole_Window_Exempts_Instead_Of_Accruing_A_Deficit()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, _) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var oldest = RecentKeys()[0];
        var start = ReportingCalendarPolicy.CycleRange(oldest).Start;
        var due = ReportingCalendarPolicy.RoleDueDate(oldest, Roles.TeamLeader);

        await SeedLeaveAsync(employeeId, start, due, LeaveRequestStatus.HrApproved, LeaveRequestType.Leave);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var row = For(body, templateId).Single(i => i.GetProperty("periodKey").GetString() == oldest);

        Assert.Equal("Exempt", row.GetProperty("state").GetString());
        Assert.Equal("ApprovedLeave", row.GetProperty("exemptionReason").GetString());
        Assert.False(row.GetProperty("expected").GetBoolean());
        Assert.False(row.GetProperty("missing").GetBoolean());
        Assert.False(row.GetProperty("late").GetBoolean());
    }

    [Fact]
    public async Task A_Partially_Covering_Leave_Does_Not_Exempt()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, _) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var oldest = RecentKeys()[0];
        var start = ReportingCalendarPolicy.CycleRange(oldest).Start;
        var due = ReportingCalendarPolicy.RoleDueDate(oldest, Roles.TeamLeader);

        // يوم عمل واحد متاح داخل المهلة ⇒ الالتزام يبقى قائمًا.
        await SeedLeaveAsync(employeeId, start, due.AddDays(-1),
            LeaveRequestStatus.HrApproved, LeaveRequestType.Leave);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var row = For(body, templateId).Single(i => i.GetProperty("periodKey").GetString() == oldest);

        Assert.Equal("Missing", row.GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_Leave_That_Is_Not_Hr_Approved_Never_Exempts()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, _) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var oldest = RecentKeys()[0];
        var start = ReportingCalendarPolicy.CycleRange(oldest).Start;
        var due = ReportingCalendarPolicy.RoleDueDate(oldest, Roles.TeamLeader);

        // طلب مقدَّم بانتظار الاعتماد — نيّة لا قرارًا.
        await SeedLeaveAsync(employeeId, start, due, LeaveRequestStatus.Submitted, LeaveRequestType.Leave);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var row = For(body, templateId).Single(i => i.GetProperty("periodKey").GetString() == oldest);

        Assert.Equal("Missing", row.GetProperty("state").GetString());
    }

    [Fact]
    public async Task An_Hourly_Permission_Never_Exempts_From_A_Periodic_Obligation()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (templateId, _) = await SeedKpiTemplateAsync(
            KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var oldest = RecentKeys()[0];
        var start = ReportingCalendarPolicy.CycleRange(oldest).Start;
        var due = ReportingCalendarPolicy.RoleDueDate(oldest, Roles.TeamLeader);

        await SeedLeaveAsync(employeeId, start, due,
            LeaveRequestStatus.HrApproved, LeaveRequestType.Permission);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}"));
        var row = For(body, templateId).Single(i => i.GetProperty("periodKey").GetString() == oldest);

        Assert.Equal("Missing", row.GetProperty("state").GetString());
    }

    // ===================== ⑦ التقارير: تفويض للمُشتقّ القائم لا نسخة ثانية =====================

    [Fact]
    public async Task Report_Obligations_Come_From_The_Existing_Expected_Status_Resolver()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var templateId = await SeedReportTemplateForUserAsync(employeeId);

        var body = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}&kind=Report"));

        var rows = For(body, templateId).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.Equal("Report", r.GetProperty("kind").GetString());
            Assert.Equal(nameof(ReportTemplateAssignment), r.GetProperty("sourceKind").GetString());
            // التقرير التزام على الموظّف نفسه — لا مالك خارجيّ.
            Assert.Equal(employeeId, r.GetProperty("ownerUserId").GetGuid());
        });
    }

    [Fact]
    public async Task Kind_Filter_Isolates_One_Engine_Without_Mixing_The_Other()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        await SeedKpiTemplateAsync(KpiCadence.WeeklyPulse, includeUserId: employeeId);
        await SeedReportTemplateForUserAsync(employeeId);

        var kpiOnly = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}&kind=KpiEvaluation"));
        Assert.NotEmpty(Items(kpiOnly));
        Assert.All(Items(kpiOnly), i => Assert.Equal("KpiEvaluation", i.GetProperty("kind").GetString()));

        var reportOnly = await JsonAsync(await leader.GetAsync(
            $"/api/obligations?userId={employeeId}&recentCycles={Cycles}&kind=Report"));
        Assert.NotEmpty(Items(reportOnly));
        Assert.All(Items(reportOnly), i => Assert.Equal("Report", i.GetProperty("kind").GetString()));
    }

    // ===================== ⑧ اتّساق العدّادات مع الصفوف =====================

    [Fact]
    public async Task Summary_Counters_Are_Computed_Before_Display_Filtering()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        await SeedKpiTemplateAsync(KpiCadence.WeeklyPulse, includeUserId: employeeId);

        var url = $"/api/obligations?userId={employeeId}&recentCycles={Cycles}";
        var full = await JsonAsync(await leader.GetAsync(url));
        var actionable = await JsonAsync(await leader.GetAsync(url + "&onlyActionable=true"));

        // نفس العدّادات رغم اختلاف الصفوف المعروضة — الرقم لا يتغيّر بتغيّر العدسة.
        Assert.Equal(full.GetProperty("summary").GetProperty("missing").GetInt32(),
                     actionable.GetProperty("summary").GetProperty("missing").GetInt32());

        Assert.All(Items(actionable), i =>
            Assert.Contains(i.GetProperty("state").GetString(), new[] { "Pending", "Missing" }));

        // والعدّاد مطابق فعلًا لعدد الصفوف الناقصة في القائمة الكاملة.
        Assert.Equal(Items(full).Count(i => i.GetProperty("missing").GetBoolean()),
                     full.GetProperty("summary").GetProperty("missing").GetInt32());
    }

    [Fact]
    public async Task The_Cycle_Window_Is_Capped_Structurally()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        var body = await JsonAsync(await viewer.GetAsync("/api/obligations?recentCycles=500"));
        Assert.Equal(ObligationPolicy.MaxCycles,
            body.GetProperty("periodKeys").EnumerateArray().Count());
    }

    // ===================== بذور معزولة =====================

    /// <summary>قالب KPI منشور مع نسخة، وإسناد/استثناء صريح على مستوى الموظّف.</summary>
    private async Task<(Guid TemplateId, Guid VersionId)> SeedKpiTemplateAsync(
        KpiCadence cadence, Guid? includeUserId = null, Guid? excludeUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = new KpiTemplate
        {
            Title = $"قالب مؤشّرات {Guid.NewGuid():N}",
            // مسمّى وظيفيّ وهميّ: يمنع المطابقة العامّة فيبقى الإسناد الصريح هو المصدر الوحيد.
            JobRoleId = Guid.NewGuid(),
            Cadence = cadence,
            Status = TemplateStatus.Published,
            IsActive = true,
            OwnerId = includeUserId ?? Guid.NewGuid()
        };
        db.KpiTemplates.Add(template);

        var version = new KpiTemplateVersion
        {
            KpiTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow.AddYears(-1)
        };
        db.KpiTemplateVersions.Add(version);

        if (includeUserId is Guid inc)
            db.KpiTemplateAssignments.Add(new KpiTemplateAssignment
            {
                KpiTemplateId = template.Id,
                ScopeType = TemplateAssignmentScope.Employee,
                ScopeId = inc,
                Kind = TemplateAssignmentKind.Include,
                IsActive = true
            });

        if (excludeUserId is Guid exc)
            db.KpiTemplateAssignments.Add(new KpiTemplateAssignment
            {
                KpiTemplateId = template.Id,
                ScopeType = TemplateAssignmentScope.Employee,
                ScopeId = exc,
                Kind = TemplateAssignmentKind.Exclude,
                IsActive = true
            });

        await db.SaveChangesAsync();
        return (template.Id, version.Id);
    }

    private async Task SeedEvaluationAsync(
        Guid versionId, Guid subjectId, string periodKey, KpiEvaluationStatus status, DateTime? submittedAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.KpiEvaluations.Add(new KpiEvaluation
        {
            KpiTemplateVersionId = versionId,
            SubjectUserId = subjectId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = periodKey,
            Status = status,
            SubmittedAtUtc = submittedAtUtc
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedLeaveAsync(
        Guid userId, DateOnly start, DateOnly end, LeaveRequestStatus status, LeaveRequestType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequesterUserId = userId,
            Type = type,
            StartDate = start,
            EndDate = end,
            Status = status
        });
        await db.SaveChangesAsync();
    }

    /// <summary>قالب تقرير أسبوعيّ منشور مربوط بمسمّى الموظّف — المسار الذي يفهمه المُشتقّ القائم.</summary>
    private async Task<Guid> SeedReportTemplateForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobRole = new Reporting.Domain.Entities.Org.JobRole
        {
            NameAr = $"مسمّى {Guid.NewGuid():N}",
            Code = null
        };
        db.JobRoles.Add(jobRole);

        var template = new ReportTemplate
        {
            Title = $"قالب تقرير {Guid.NewGuid():N}",
            JobRoleId = jobRole.Id,
            Classification = TemplateClassification.Primary,
            DefaultPeriodType = PeriodType.Weekly,
            IsActive = true,
            Status = TemplateStatus.Published,
            OwnerId = userId
        };
        db.ReportTemplates.Add(template);

        var anchor = DateTime.UtcNow.AddYears(-1);
        db.ReportTemplateVersions.Add(new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = anchor
        });

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        user.CreatedAtUtc = anchor;

        await db.SaveChangesAsync();
        return template.Id;
    }
}

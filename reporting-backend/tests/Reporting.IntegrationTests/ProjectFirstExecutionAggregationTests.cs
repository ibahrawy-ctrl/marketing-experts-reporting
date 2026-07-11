using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// محرّك التجميع Project-First (RC-4 Task 4، Path A) — يثبت أنّ كل الأرقام التشغيلية تُقرأ
/// من داخل كل مشروع في قسم المشاريع المتكرّر (ProjectRepeatableSection) بدل القوالب المسطّحة القديمة.
/// يغطّي: التجميع حسب (المشروع/الموظّف/Pod/العميل)، معدّلات الاشتقاق الآمنة، معدّل الاستجابة للمديرشن،
/// تطبيع الأرقام العربية داخل الإجابات، عزل فلتر المشروع/العميل، والنطاق (الغريب لا يرى شيئًا + المجهول 401).
/// النطاق محكوم خادميًّا عبر IScopeResolver. لا يمسّ أيّ تسليم/قالب/مسار اعتماد.
/// </summary>
[Collection("Integration")]
public class ProjectFirstExecutionAggregationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectFirstExecutionAggregationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    /// <summary>حقل قسم المشاريع المتكرّر في النسخة المنشورة الوحيدة (v2) لقالب التنفيذ.</summary>
    private static Guid PrsFieldId(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished)
            .Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection).Id;

    private static async Task<Guid> CreateClientAsync(HttpClient admin)
        => (await (await admin.PostAsJsonAsync("/api/clients",
                new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!.Id;

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, Guid clientId, string name, Guid? ownerTeamId = null)
        => (await (await admin.PostAsJsonAsync("/api/projects",
                new CreateProjectRequest(clientId, name, ServiceType.Social, OwnerTeamId: ownerTeamId)))
            .ReadAsync<ProjectDto>())!;

    /// <summary>
    /// موظّف فريد داخل فريق يقوده قائد فريد. نُسلِّم بهذا الموظّف (لا بالأدمن المبذور المشترك) لتفادي
    /// تصادم حارس ازدواج التقرير الأساسي (موظّف، فترة) مع بقية ملفات الاختبار على القاعدة المشتركة.
    /// المشاريع تُنشأ بـ OwnerTeamId = فريقه ليكون داخل نطاقه (منع IDOR). الاستعلام يبقى كأدمن (SeesAll).
    /// </summary>
    private async Task<(HttpClient Client, Guid EmpId, Guid TeamId)> NewEmployeeInTeamAsync()
    {
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);
        return (emp, empId, teamId);
    }

    /// <summary>يبني قيمة قسم المشاريع لمشروع واحد من قاموس الإجابات (المفاتيح والقيم نصوص خام كما يرسلها العميل).</summary>
    private static string OneProjectJson(Guid projectId, IReadOnlyDictionary<string, string> answers)
    {
        var pairs = string.Join(",", answers.Select(a => $"\"{a.Key}\":\"{a.Value}\""));
        return $"[{{\"projectId\":\"{projectId}\",\"answers\":{{{pairs}}}}}]";
    }

    /// <summary>
    /// حقول التصنيف (SingleSelect) الإلزامية التي أضافتها قوالب التنفيذ v3 (RC-4 Task 4D1) عبر كل التخصّصات
    /// (محتوى/تصميم/فيديو/مديرشن) + حقل العدد. التحقّق الخادميّ يفرض وجود الحقول الفرعية المطلوبة فقط ولا يمسّ
    /// المفاتيح الرقمية القديمة (planned/completed/…) التي ما زال محرّك التجميع يقرأها (تحديث التجميع مؤجَّل لمرحلة لاحقة).
    /// تُحقَن كمجموعة فائقة داخل كل مدخل ذي مشروع؛ كل قالب يستهلك المطلوب منه ويتجاهل الباقي، فتبقى تغطية التجميع v2 سليمة.
    /// </summary>
    private static readonly (string Key, string Value)[] TaxonomyV3RequiredDefaults =
    {
        ("content_type", "Carousel"), ("content_goal", "Sales"), ("work_status", "Draft"),
        ("design_type", "Static"), ("design_status", "New"), ("design_tool", "Figma"),
        ("video_type", "Reel"), ("edit_type", "Full Editing"), ("video_duration", "1_3min"), ("video_status", "Draft"),
        ("activity_type", "Comments"), ("interaction_result", "Inquiry"), ("response_time", "under_1h"),
        ("count", "1"),
    };

    /// <summary>
    /// يحقن حقول التصنيف v3 المطلوبة في كل مدخل مشروع ضمن قيمة قسم المشاريع (JSON) إن لم تكن موجودة أصلًا،
    /// كي يمرّ التحقّق الخادميّ لقوالب التنفيذ v3، مع الإبقاء على المفاتيح الرقمية القديمة كما هي. المدخلات
    /// بلا مشروع أو بلا answers تُترَك دون تعديل (تُختبَر كتشخيص «مدخل مُتجاهَل»).
    /// </summary>
    private static string InjectTaxonomyV3(string valueJson)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(valueJson);
        if (node is not System.Text.Json.Nodes.JsonArray arr) return valueJson;
        foreach (var item in arr)
        {
            if (item is not System.Text.Json.Nodes.JsonObject obj) continue;
            if (!obj.TryGetPropertyValue("projectId", out var pid) || pid is null) continue;
            if (obj["answers"] is not System.Text.Json.Nodes.JsonObject answers) continue;
            foreach (var (key, value) in TaxonomyV3RequiredDefaults)
                if (!answers.ContainsKey(key)) answers[key] = value;
        }
        return arr.ToJsonString();
    }

    /// <summary>ينشئ مسودّة على قالب التنفيذ، يعبّئ قسم المشاريع بالقيمة الجاهزة، ثم يُرسِلها.</summary>
    private static async Task SubmitPrsAsync(HttpClient c, Guid templateId, Guid fieldId, string periodKey, string valueJson)
    {
        var draftId = (await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>())!.Id;
        valueJson = InjectTaxonomyV3(valueJson);
        await c.PutAsJsonAsync($"/api/submissions/{draftId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, null, null, null, valueJson) }));
        var res = await c.PostAsync($"/api/submissions/{draftId}/submit", null);
        res.EnsureSuccessStatusCode();
    }

    private static Dictionary<string, string> ProductionAnswers(
        string planned, string completed, string approved, string revisions, string published, string delayed)
        => new()
        {
            [ProjectFirstExecutionSchema.KeyPlanned] = planned,
            [ProjectFirstExecutionSchema.KeyCompleted] = completed,
            [ProjectFirstExecutionSchema.KeyApproved] = approved,
            [ProjectFirstExecutionSchema.KeyRevisions] = revisions,
            [ProjectFirstExecutionSchema.KeyPublished] = published,
            [ProjectFirstExecutionSchema.KeyDelayed] = delayed,
        };

    private async Task<ProjectFirstExecutionReport<ProjectFirstByProjectRow>> ByProjectAsync(HttpClient c, string query)
        => (await (await c.GetAsync($"/api/reporting/project-execution/projects?{query}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>())!;

    private async Task<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>> ByEmployeeAsync(HttpClient c, string query)
        => (await (await c.GetAsync($"/api/reporting/project-execution/employees?{query}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>())!;

    private async Task<ProjectFirstExecutionReport<ProjectFirstByPodRow>> ByPodAsync(HttpClient c, string query)
        => (await (await c.GetAsync($"/api/reporting/project-execution/pods?{query}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByPodRow>>())!;

    private async Task<ProjectFirstExecutionReport<ProjectFirstByClientRow>> ByClientAsync(HttpClient c, string query)
        => (await (await c.GetAsync($"/api/reporting/project-execution/clients?{query}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByClientRow>>())!;

    // ===== 1: التجميع حسب المشروع يقرأ الأرقام من داخل المشروع ويشتقّ المعدّلات =====
    [Fact]
    public async Task ByProject_AggregatesMetricsInsideProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع المحتوى A", ownerTeamId: teamId);
        const string period = "2026-W60";

        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("25", "20", "16", "4", "10", "2")));

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(project.Id, row.ProjectId);
        Assert.Equal(clientId, row.ClientId);
        Assert.Equal(25m, row.Metrics.Planned);
        Assert.Equal(20m, row.Metrics.Completed);
        Assert.Equal(16m, row.Metrics.Approved);
        Assert.Equal(80m, row.Metrics.CompletionRate);  // 20/25
        Assert.Equal(80m, row.Metrics.ApprovalRate);     // 16/20
        Assert.Equal(62.5m, row.Metrics.PublishRate);    // 10/16
    }

    // ===== 2: التجميع حسب الموظّف يُرجِع صفًّا (موظّف، مشروع) بالمقاييس =====
    [Fact]
    public async Task ByEmployee_ReturnsEmployeeProjectRowWithMetrics()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع الموظّف", ownerTeamId: teamId);
        const string period = "2026-W61";

        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("40", "30", "24", "6", "12", "3")));

        var report = await ByEmployeeAsync(admin, $"periodKey={period}&projectId={project.Id}&employeeId={empId}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(empId, row.EmployeeId);
        Assert.Equal(project.Id, row.ProjectId);
        Assert.Equal(teamId, row.TeamId);
        Assert.Equal(40m, row.Metrics.Planned);
        Assert.Equal(75m, row.Metrics.CompletionRate); // 30/40
    }

    // ===== 3: التجميع حسب Pod يستخدم فريق المُسلِّم (Submitter.TeamId) =====
    [Fact]
    public async Task ByPod_UsesSubmitterTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.DesignTitle);
        var fieldId = PrsFieldId(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع التصميم Pod", ownerTeamId: teamId);
        const string period = "2026-W62";

        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("10", "8", "6", "2", "4", "1")));

        var report = await ByPodAsync(admin, $"periodKey={period}&teamId={teamId}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(teamId, row.TeamId);
        Assert.Equal(10m, row.Metrics.Planned);
        Assert.Equal(80m, row.Metrics.CompletionRate); // 8/10
    }

    // ===== 4: التجميع حسب العميل يجمع كل مشاريع العميل ضمن الفلاتر =====
    [Fact]
    public async Task ByClient_AggregatesAcrossProjectsOfSameClient()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var p1 = await CreateProjectAsync(admin, clientId, "مشروع عميل 1", ownerTeamId: teamId);
        var p2 = await CreateProjectAsync(admin, clientId, "مشروع عميل 2", ownerTeamId: teamId);
        const string period = "2026-W63";

        // مشروعان لنفس العميل في تسليمين لنفس الموظّف بفترتين مختلفتين: المجاميع planned=30، completed=24.
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(p1.Id, ProductionAnswers("20", "16", "12", "3", "8", "1")));
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W64",
            OneProjectJson(p2.Id, ProductionAnswers("10", "8", "6", "1", "4", "0")));

        var report = await ByClientAsync(admin, $"clientId={clientId}");

        var row = Assert.Single(report.Rows.Where(r => r.ClientId == clientId));
        Assert.Equal(30m, row.Metrics.Planned);
        Assert.Equal(24m, row.Metrics.Completed);
        Assert.Equal(80m, row.Metrics.CompletionRate); // 24/30
    }

    // ===== 5: المديرشن — معدّل الاستجابة يُشتقّ من داخل المشروع =====
    [Fact]
    public async Task ByProject_Moderation_ComputesResponseRate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ModerationTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع المديرشن", ownerTeamId: teamId);
        const string period = "2026-W65";

        var answers = new Dictionary<string, string>
        {
            [ProjectFirstExecutionSchema.KeyMessagesIn] = "100",
            [ProjectFirstExecutionSchema.KeyResponses] = "80",
            [ProjectFirstExecutionSchema.KeyIssueComments] = "12",
            [ProjectFirstExecutionSchema.KeyEscalations] = "3",
            [ProjectFirstExecutionSchema.KeyPublished] = "40",
            [ProjectFirstExecutionSchema.KeyDelayed] = "5",
        };
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period, OneProjectJson(project.Id, answers));

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(100m, row.Metrics.MessagesIn);
        Assert.Equal(80m, row.Metrics.Responses);
        Assert.Equal(80m, row.Metrics.ResponseRate); // 80/100
    }

    // ===== 6: تطبيع الأرقام العربية داخل إجابات المشروع =====
    [Fact]
    public async Task ByProject_NormalizesArabicIndicDigits()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع الأرقام العربية", ownerTeamId: teamId);
        const string period = "2026-W66";

        // أرقام هندية-عربية: ٢٥ / ٢٠ / ١٦ …
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("٢٥", "٢٠", "١٦", "٤", "١٠", "٢")));

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(25m, row.Metrics.Planned);
        Assert.Equal(20m, row.Metrics.Completed);
        Assert.Equal(80m, row.Metrics.CompletionRate);
    }

    // ===== 7: فلتر المشروع يعزل المدخلات (لا يخلط مشروعًا بآخر) =====
    [Fact]
    public async Task ProjectIdFilter_IsolatesEntries()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var pA = await CreateProjectAsync(admin, clientId, "مشروع A", ownerTeamId: teamId);
        var pB = await CreateProjectAsync(admin, clientId, "مشروع B", ownerTeamId: teamId);
        const string period = "2026-W67";

        // تسليم واحد يحوي مشروعين في نفس القسم.
        var valueJson =
            $"[{{\"projectId\":\"{pA.Id}\",\"answers\":{{\"planned\":\"25\",\"completed\":\"20\",\"approved\":\"16\",\"revisions\":\"4\",\"published\":\"10\",\"delayed\":\"2\"}}}}," +
            $"{{\"projectId\":\"{pB.Id}\",\"answers\":{{\"planned\":\"8\",\"completed\":\"4\",\"approved\":\"2\",\"revisions\":\"1\",\"published\":\"1\",\"delayed\":\"0\"}}}}]";
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period, valueJson);

        var onlyA = await ByProjectAsync(admin, $"periodKey={period}&projectId={pA.Id}");
        var rowA = Assert.Single(onlyA.Rows);
        Assert.Equal(pA.Id, rowA.ProjectId);
        Assert.Equal(25m, rowA.Metrics.Planned);

        var onlyB = await ByProjectAsync(admin, $"periodKey={period}&projectId={pB.Id}");
        var rowB = Assert.Single(onlyB.Rows);
        Assert.Equal(pB.Id, rowB.ProjectId);
        Assert.Equal(8m, rowB.Metrics.Planned);
    }

    // ===== 8: الغريب (نطاق own بلا تسليم) لا يرى شيئًا وViewLevel = self =====
    [Fact]
    public async Task Outsider_SeesNothing_WithSelfViewLevel()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع فريق مغلق", ownerTeamId: teamId);
        const string period = "2026-W68";

        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("25", "20", "16", "4", "10", "2")));

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var report = await ByProjectAsync(stranger, $"periodKey={period}");

        Assert.Empty(report.Rows);
        Assert.Equal("self", report.ViewLevel);
    }

    // ===== 9: المجهول غير مصرّح له (401) على كل النقاط =====
    [Fact]
    public async Task Anonymous_Returns401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/reporting/project-execution/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/reporting/project-execution/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/reporting/project-execution/pods")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/reporting/project-execution/clients")).StatusCode);
    }

    // ===== أدوات مساعدة إضافية (تلاعب DbContext = كود اختبار، لا تعديل قاعدة إنتاج) =====

    /// <summary>
    /// مفتاح أسبوع فريد لكل تشغيل (صيغة YYYY-Www صحيحة) لعزل اختبارات التشخيص «العالمية للفترة»
    /// (outside_project_filter/outside_client_filter/empty_project_entry) عن تراكم قاعدة الاختبار المشتركة الدائمة.
    /// سنة بعيدة (2100..2899) وأسبوع 10..49 ⇒ احتمال التصادم بين التشغيلات مهمَل، ولا يمسّ بيانات 2026 القائمة.
    /// </summary>
    private static string UniqueWeek()
    {
        var year = 2100 + Random.Shared.Next(0, 800);
        var week = 10 + Random.Shared.Next(0, 40);
        return $"{year}-W{week:00}";
    }

    /// <summary>يضبط حالة مشروع مباشرةً (لاختبار عدّ المشاريع النشطة ActiveProjectCount).</summary>
    private async Task SetProjectStatusAsync(Guid projectId, ProjectStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = await db.Projects.FirstAsync(x => x.Id == projectId);
        p.Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>يستبدل ValueJson لقيمة قسم مشاريع (تُلتقَط عبر وجود المعرّف marker) — لحقن مدخل فارغ/تالف.</summary>
    private async Task OverwritePrsValueJsonAsync(Guid fieldId, Guid marker, string newJson)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var markerText = marker.ToString();
        // الترشيح على العمود jsonb لا يترجم .Contains إلى SQL (~~ يفشل على jsonb) ⇒ نُحضِر صفوف الحقل ثم نطابق العميلَ نصّيًّا.
        var candidates = await db.SubmissionFieldValues
            .Where(v => v.TemplateFieldId == fieldId && v.ValueJson != null)
            .ToListAsync();
        var val = candidates.First(v => v.ValueJson!.Contains(markerText));
        val.ValueJson = newJson;
        await db.SaveChangesAsync();
    }

    // ===== 10: المقارنة الأسبوعية — صعود حين الحالي > السابق =====
    [Fact]
    public async Task ByProject_WeeklyComparison_Up_WhenCurrentExceedsPrevious()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مقارنة صعود", ownerTeamId: teamId);

        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W09",
            OneProjectJson(project.Id, ProductionAnswers("20", "10", "8", "1", "5", "0")));
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W10",
            OneProjectJson(project.Id, ProductionAnswers("30", "20", "16", "2", "10", "0")));

        var report = await ByProjectAsync(admin, $"periodType=Weekly&periodKey=2026-W10&projectId={project.Id}");

        Assert.Equal("2026-W09", report.PreviousPeriodKey);
        var row = Assert.Single(report.Rows);
        Assert.NotNull(row.Comparison);
        var cmp = row.Comparison!;
        Assert.True(cmp.HasPrevious);
        Assert.Equal(20m, cmp.Current);   // Completed(20) + Responses(0)
        Assert.Equal(10m, cmp.Previous);  // Completed(10) + Responses(0)
        Assert.Equal(10m, cmp.Change);
        Assert.Equal(100m, cmp.ChangePercent);
        Assert.Equal("up", cmp.Trend);
    }

    // ===== 11: المقارنة الأسبوعية — هبوط حين الحالي < السابق =====
    [Fact]
    public async Task ByProject_WeeklyComparison_Down_WhenCurrentBelowPrevious()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مقارنة هبوط", ownerTeamId: teamId);

        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W09",
            OneProjectJson(project.Id, ProductionAnswers("30", "20", "16", "2", "10", "0")));
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W10",
            OneProjectJson(project.Id, ProductionAnswers("20", "10", "8", "1", "5", "0")));

        var report = await ByProjectAsync(admin, $"periodType=Weekly&periodKey=2026-W10&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.NotNull(row.Comparison);
        var cmp = row.Comparison!;
        Assert.True(cmp.HasPrevious);
        Assert.Equal(-10m, cmp.Change);
        Assert.Equal(-50m, cmp.ChangePercent);
        Assert.Equal("down", cmp.Trend);
    }

    // ===== 12: المقارنة الأسبوعية — ثبات حين التساوي =====
    [Fact]
    public async Task ByProject_WeeklyComparison_Stable_WhenEqual()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مقارنة ثبات", ownerTeamId: teamId);

        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W09",
            OneProjectJson(project.Id, ProductionAnswers("20", "10", "8", "1", "5", "0")));
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W10",
            OneProjectJson(project.Id, ProductionAnswers("25", "10", "9", "1", "6", "0")));

        var report = await ByProjectAsync(admin, $"periodType=Weekly&periodKey=2026-W10&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.NotNull(row.Comparison);
        var cmp = row.Comparison!;
        Assert.True(cmp.HasPrevious);
        Assert.Equal(0m, cmp.Change);
        Assert.Equal(0m, cmp.ChangePercent);
        Assert.Equal("stable", cmp.Trend);
    }

    // ===== 13: لا بيانات سابقة — Trend=none وHasPrevious=false (لا صفر مضلِّل) =====
    [Fact]
    public async Task ByProject_NoPreviousData_ReportsNoneTrend()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع بلا سابق", ownerTeamId: teamId);

        // تسليم واحد فقط في W12 — الأسبوع السابق W11 لا يحوي أيّ مدخل لهذا المشروع.
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W12",
            OneProjectJson(project.Id, ProductionAnswers("20", "10", "8", "1", "5", "0")));

        var report = await ByProjectAsync(admin, $"periodType=Weekly&periodKey=2026-W12&projectId={project.Id}");

        Assert.Equal("2026-W11", report.PreviousPeriodKey);
        var row = Assert.Single(report.Rows);
        Assert.NotNull(row.Comparison);
        var cmp = row.Comparison!;
        Assert.False(cmp.HasPrevious);
        Assert.Equal("none", cmp.Trend);
        Assert.Null(cmp.ChangePercent);
        Assert.Equal(10m, cmp.Current);
        Assert.Equal(0m, cmp.Previous);
    }

    // ===== 14: عدّ المساهمين — موظّفان يُسلّمان لنفس المشروع ⇒ Contributors=2 =====
    [Fact]
    public async Task ByProject_CountsDistinctContributors()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp1, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (emp2, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, emp1Id, emp2Id);
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مساهمين", ownerTeamId: teamId);
        const string period = "2026-W71";

        await SubmitPrsAsync(emp1, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("10", "8", "6", "1", "4", "0")));
        await SubmitPrsAsync(emp2, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("12", "9", "7", "2", "5", "1")));

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(2, row.Contributors);
        Assert.Equal(17m, row.Metrics.Completed); // 8 + 9
    }

    // ===== 15: عدّ Pod — مشروعان وموظّفان في نفس الفريق ⇒ ProjectCount=2, EmployeeCount=2 =====
    [Fact]
    public async Task ByPod_CountsProjectsAndEmployees()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp1, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (emp2, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, emp1Id, emp2Id);
        var clientId = await CreateClientAsync(admin);
        var p1 = await CreateProjectAsync(admin, clientId, "مشروع Pod 1", ownerTeamId: teamId);
        var p2 = await CreateProjectAsync(admin, clientId, "مشروع Pod 2", ownerTeamId: teamId);
        const string period = "2026-W72";

        await SubmitPrsAsync(emp1, tpl.Id, fieldId, period,
            OneProjectJson(p1.Id, ProductionAnswers("10", "8", "6", "1", "4", "0")));
        await SubmitPrsAsync(emp2, tpl.Id, fieldId, period,
            OneProjectJson(p2.Id, ProductionAnswers("12", "9", "7", "2", "5", "1")));

        var report = await ByPodAsync(admin, $"periodKey={period}&teamId={teamId}");

        var row = Assert.Single(report.Rows.Where(r => r.TeamId == teamId));
        Assert.Equal(2, row.ProjectCount);
        Assert.Equal(2, row.EmployeeCount);
    }

    // ===== 16: عدّ العميل — مشروعان أحدهما مؤرشف ⇒ ProjectCount=2, ActiveProjectCount=1 =====
    [Fact]
    public async Task ByClient_CountsActiveProjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var p1 = await CreateProjectAsync(admin, clientId, "مشروع عميل نشط", ownerTeamId: teamId);
        var p2 = await CreateProjectAsync(admin, clientId, "مشروع عميل مؤرشف", ownerTeamId: teamId);
        const string period = "2026-W73";

        var valueJson =
            $"[{{\"projectId\":\"{p1.Id}\",\"answers\":{{\"planned\":\"20\",\"completed\":\"16\",\"approved\":\"12\",\"revisions\":\"3\",\"published\":\"8\",\"delayed\":\"1\"}}}}," +
            $"{{\"projectId\":\"{p2.Id}\",\"answers\":{{\"planned\":\"10\",\"completed\":\"8\",\"approved\":\"6\",\"revisions\":\"1\",\"published\":\"4\",\"delayed\":\"0\"}}}}]";
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period, valueJson);

        // p2 يُغلَق (مؤرشف) بعد التسليم — يبقى مُجمَّعًا لكنه لا يُحتسَب ضمن ActiveProjectCount.
        await SetProjectStatusAsync(p2.Id, ProjectStatus.Closed);

        var report = await ByClientAsync(admin, $"periodKey={period}&clientId={clientId}");

        var row = Assert.Single(report.Rows.Where(r => r.ClientId == clientId));
        Assert.Equal(2, row.ProjectCount);
        Assert.Equal(1, row.ActiveProjectCount);
    }

    // ===== 17: تشخيص outside_project_filter — مدخل خارج فلتر المشروع يُعدّ ويُسقَط =====
    [Fact]
    public async Task ProjectIdFilter_ReportsOutsideProjectFilterDiagnostic()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var pA = await CreateProjectAsync(admin, clientId, "مشروع تشخيص A", ownerTeamId: teamId);
        var pB = await CreateProjectAsync(admin, clientId, "مشروع تشخيص B", ownerTeamId: teamId);
        var period = UniqueWeek();

        var valueJson =
            $"[{{\"projectId\":\"{pA.Id}\",\"answers\":{{\"planned\":\"25\",\"completed\":\"20\",\"approved\":\"16\",\"revisions\":\"4\",\"published\":\"10\",\"delayed\":\"2\"}}}}," +
            $"{{\"projectId\":\"{pB.Id}\",\"answers\":{{\"planned\":\"8\",\"completed\":\"4\",\"approved\":\"2\",\"revisions\":\"1\",\"published\":\"1\",\"delayed\":\"0\"}}}}]";
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period, valueJson);

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={pA.Id}");

        Assert.Single(report.Rows);
        Assert.Equal(2, report.RowsConsidered);
        Assert.True(report.IgnoredReasons.TryGetValue("outside_project_filter", out var count));
        Assert.Equal(1, count);
    }

    // ===== 18: تشخيص outside_client_filter — مدخل مشروع خارج فلتر العميل يُسقَط =====
    [Fact]
    public async Task ClientIdFilter_ReportsOutsideClientFilterDiagnostic()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var c1 = await CreateClientAsync(admin);
        var c2 = await CreateClientAsync(admin);
        var p1 = await CreateProjectAsync(admin, c1, "مشروع عميل أول", ownerTeamId: teamId);
        var p2 = await CreateProjectAsync(admin, c2, "مشروع عميل ثانٍ", ownerTeamId: teamId);
        var period = UniqueWeek();

        var valueJson =
            $"[{{\"projectId\":\"{p1.Id}\",\"answers\":{{\"planned\":\"20\",\"completed\":\"16\",\"approved\":\"12\",\"revisions\":\"3\",\"published\":\"8\",\"delayed\":\"1\"}}}}," +
            $"{{\"projectId\":\"{p2.Id}\",\"answers\":{{\"planned\":\"10\",\"completed\":\"8\",\"approved\":\"6\",\"revisions\":\"1\",\"published\":\"4\",\"delayed\":\"0\"}}}}]";
        await SubmitPrsAsync(emp, tpl.Id, fieldId, period, valueJson);

        var report = await ByProjectAsync(admin, $"periodKey={period}&clientId={c1}");

        Assert.True(report.IgnoredReasons.TryGetValue("outside_client_filter", out var count));
        Assert.Equal(1, count);
        var row = Assert.Single(report.Rows);
        Assert.Equal(p1.Id, row.ProjectId);
    }

    // ===== 19: مدخل مشروع فارغ — يُعدّ empty_project_entry ويُتجاهَل بينما يبقى الصفّ الصحيح =====
    [Fact]
    public async Task EmptyProjectEntry_IsCountedAndIgnored_ValidRowSurvives()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مدخل فارغ", ownerTeamId: teamId);
        var period = UniqueWeek();

        await SubmitPrsAsync(emp, tpl.Id, fieldId, period,
            OneProjectJson(project.Id, ProductionAnswers("20", "16", "12", "3", "8", "1")));

        // حقن مدخل فارغ (بلا projectId) بجانب المدخل الصحيح — يحاكي بيانات v1 قديمة أو صفًّا فارغًا.
        var validEntry =
            $"{{\"projectId\":\"{project.Id}\",\"answers\":{{\"planned\":\"20\",\"completed\":\"16\",\"approved\":\"12\",\"revisions\":\"3\",\"published\":\"8\",\"delayed\":\"1\"}}}}";
        await OverwritePrsValueJsonAsync(fieldId, project.Id, $"[{validEntry},{{\"answers\":{{}}}}]");

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        var row = Assert.Single(report.Rows);
        Assert.Equal(16m, row.Metrics.Completed);
        Assert.True(report.EntriesIgnored >= 1);
        Assert.True(report.IgnoredReasons.TryGetValue("empty_project_entry", out var count));
        Assert.Equal(1, count);
    }

    // ===== 20: ValueJson تالف لا يُسقِط الطلب — الصفّ الصحيح في تسليم آخر يبقى =====
    [Fact]
    public async Task CorruptValueJson_IsSkipped_ValidSubmissionSurvives()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var pGood = await CreateProjectAsync(admin, clientId, "مشروع سليم", ownerTeamId: teamId);
        var pBad = await CreateProjectAsync(admin, clientId, "مشروع تالف", ownerTeamId: teamId);

        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W77",
            OneProjectJson(pGood.Id, ProductionAnswers("20", "16", "12", "3", "8", "1")));
        await SubmitPrsAsync(emp, tpl.Id, fieldId, "2026-W78",
            OneProjectJson(pBad.Id, ProductionAnswers("10", "8", "6", "1", "4", "0")));

        // إتلاف بنية ValueJson لتسليم المشروع الثاني: JSON صالح للعمود jsonb لكنه بشكلٍ غير متوقَّع
        // (كائن بدل مصفوفة المدخلات) — يجب أن يُتجاهَل بأمان دون إسقاط الطلب. (العمود jsonb يرفض JSON غير صالح نحويًّا.)
        await OverwritePrsValueJsonAsync(fieldId, pBad.Id, "{\"corrupt\":\"unexpected-shape\"}");

        var report = await ByClientAsync(admin, $"clientId={clientId}");

        var row = Assert.Single(report.Rows.Where(r => r.ClientId == clientId));
        Assert.Equal(1, row.ProjectCount);          // المشروع التالف أُسقِط، السليم بقي.
        Assert.Equal(16m, row.Metrics.Completed);
    }

    // ===== 21: المسودّة (Draft) لا تُحتسَب في التجميع =====
    [Fact]
    public async Task DraftSubmission_IsIgnored()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectFirstExecutionSchema.ContentTitle);
        var fieldId = PrsFieldId(tpl);
        var (emp, _, teamId) = await NewEmployeeInTeamAsync();
        var clientId = await CreateClientAsync(admin);
        var project = await CreateProjectAsync(admin, clientId, "مشروع مسودّة", ownerTeamId: teamId);
        const string period = "2026-W79";

        // مسودّة تُملأ لكن لا تُرسَل.
        var draftId = (await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>())!.Id;
        await emp.PutAsJsonAsync($"/api/submissions/{draftId}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(fieldId, null, null, null, null,
                    OneProjectJson(project.Id, ProductionAnswers("20", "16", "12", "3", "8", "1"))),
            }));

        var report = await ByProjectAsync(admin, $"periodKey={period}&projectId={project.Id}");

        Assert.Empty(report.Rows);
    }
}

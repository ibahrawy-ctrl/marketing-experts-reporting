using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1 (Phase 13) — اختبارات تكامل حتميّة على قاعدة معزولة
/// (reporting_pfe_iso). تبذر التسليمات مباشرةً عبر AppDbContext بمفاتيح v5 الحقيقية داخل قسم المشاريع المتكرّر
/// (لأن قالب كاتب المحتوى المبذور يستعمل مفاتيح فرعية مختلفة pieces/project_notes، بينما المحرّك يقرأ مفاتيح v5
/// الحقيقية required_pieces/delivered_pieces/… — والمحرّك لا يعيد التحقّق من المفاتيح وقت القراءة).
/// تغطّي: التجميع لكل مشروع/موظّف/Pod/عميل، الصيغ والنِسب، حالة المشروع المطبَّعة (RAG)، المقارنة الدوريّة،
/// معالجة تكرار المشروع (استراتيجية A = التراكم/الجمع)، وأمان النطاق/الصلاحيات (IScopeResolver ∪ IClientProjectAccess).
/// </summary>
[Collection("ProjectFirstIsolated")]
public class ProjectFirstExecutionAggregationTests
{
    private readonly ProjectFirstIsolatedFactory _factory;

    public ProjectFirstExecutionAggregationTests(ProjectFirstIsolatedFactory factory) => _factory = factory;

    // ===== منافذ التجميع الأربعة (قراءة فقط) =====

    private static async Task<ProjectFirstExecutionReport<ProjectFirstByProjectRow>> ByProjectsAsync(HttpClient c, string q)
        => (await (await c.GetAsync($"/api/reporting/project-execution/projects?{q}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>())!;

    private static async Task<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>> ByEmployeesAsync(HttpClient c, string q)
        => (await (await c.GetAsync($"/api/reporting/project-execution/employees?{q}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>())!;

    private static async Task<ProjectFirstExecutionReport<ProjectFirstByPodRow>> ByPodsAsync(HttpClient c, string q)
        => (await (await c.GetAsync($"/api/reporting/project-execution/pods?{q}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByPodRow>>())!;

    private static async Task<ProjectFirstExecutionReport<ProjectFirstByClientRow>> ByClientsAsync(HttpClient c, string q)
        => (await (await c.GetAsync($"/api/reporting/project-execution/clients?{q}"))
            .ReadAsync<ProjectFirstExecutionReport<ProjectFirstByClientRow>>())!;

    /// <summary>قيم قسم المشاريع بمفاتيح v5 الحقيقية لقالب المحتوى (Content).</summary>
    private sealed record ContentValues(
        int RequiredPieces, int DeliveredPieces, int ApprovedFirstTime,
        int ReturnedOnce, int ReturnedMore, int LatePieces, string ProjectStatus);

    /// <summary>ينشئ عميلًا (Active) ومشروعًا (Active) عبر AppDbContext مباشرةً ويُعيد معرّفاتهما.</summary>
    private async Task<(Guid ClientId, Guid ProjectId)> SeedClientProjectAsync(
        string clientName, string projectName, ProjectStatus projectStatus = ProjectStatus.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Client { Name = clientName, Status = ClientStatus.Active };
        db.Clients.Add(client);
        var project = new Project
        {
            ClientId = client.Id,
            Name = projectName,
            ServiceType = ServiceType.Other,
            Status = projectStatus
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (client.Id, project.Id);
    }

    /// <summary>
    /// يبذر تسليمًا واحدًا (Status=Submitted، غير Draft) لقالب المحتوى المبذور بمفتاح فترة معطى،
    /// مع حقل قسم المشاريع المتكرّر يحمل مدخلًا لكل (projectId، قيم v5). يقرأ نسخة القالب وحقل القسم
    /// من القاعدة عبر العنوان القياسي ونوع الحقل ProjectRepeatableSection.
    /// </summary>
    private async Task SeedSubmissionAsync(
        Guid submitterId, Guid? teamId, string periodKey,
        IReadOnlyList<(Guid ProjectId, ContentValues Values)> entries)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = await db.ReportTemplates
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .SingleAsync(t => t.Title == ProjectFirstExecutionSchema.ContentTitle);
        var version = template.Versions.First(v => v.IsPublished);
        var prsField = version.Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection);

        var payload = entries.Select(e => new
        {
            projectId = e.ProjectId,
            answers = new Dictionary<string, object>
            {
                ["required_pieces"] = e.Values.RequiredPieces,
                ["delivered_pieces"] = e.Values.DeliveredPieces,
                ["approved_first_time"] = e.Values.ApprovedFirstTime,
                ["returned_once"] = e.Values.ReturnedOnce,
                ["returned_more"] = e.Values.ReturnedMore,
                ["late_pieces"] = e.Values.LatePieces,
                ["project_status"] = e.Values.ProjectStatus
            }
        }).ToList();

        var submission = new ReportSubmission
        {
            ReportTemplateVersionId = version.Id,
            SubmitterId = submitterId,
            TeamId = teamId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = periodKey,
            Status = SubmissionStatus.Submitted
        };
        db.ReportSubmissions.Add(submission);
        db.SubmissionFieldValues.Add(new SubmissionFieldValue
        {
            ReportSubmissionId = submission.Id,
            TemplateFieldId = prsField.Id,
            ValueJson = JsonSerializer.Serialize(payload)
        });
        await db.SaveChangesAsync();
    }

    private static ContentValues Sample(string status = "🟢 ممتاز")
        => new(RequiredPieces: 25, DeliveredPieces: 20, ApprovedFirstTime: 15,
               ReturnedOnce: 2, ReturnedMore: 1, LatePieces: 5, ProjectStatus: status);

    // ===== (1) التجميع لكل مشروع + الصيغ والنِسب + حالة RAG =====
    [Fact]
    public async Task ByProject_AggregatesMetrics_Rates_AndNormalizedStatus()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (clientId, projectId) = await SeedClientProjectAsync("عميل ألفا P1", "مشروع ألفا P1");
        await SeedSubmissionAsync(empId, null, "2098-W11", new[] { (projectId, Sample()) });

        var report = await ByProjectsAsync(admin, $"periodKey=2098-W11&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(projectId, row.ProjectId);
        Assert.Equal("مشروع ألفا P1", row.ProjectName);
        Assert.Equal(clientId, row.ClientId);
        Assert.Equal(1, row.Contributors);

        Assert.Equal(25m, row.Metrics.Planned);
        Assert.Equal(20m, row.Metrics.Completed);
        Assert.Equal(15m, row.Metrics.Approved);
        Assert.Equal(3m, row.Metrics.Revisions);      // 2 + 1
        Assert.Equal(5m, row.Metrics.Delayed);
        Assert.Equal(80.0m, row.Metrics.CompletionRate); // 20/25
        Assert.Equal(75.0m, row.Metrics.ApprovalRate);   // 15/20

        Assert.Equal(1, row.Status.Healthy);
        Assert.Equal(1, row.Status.Total);
        Assert.Equal("summary", report.ViewLevel); // admin governance ⇒ summary
        Assert.Equal(1, report.SubmissionsConsidered);
    }

    // ===== (2) التجميع لكل (موظّف، مشروع) =====
    [Fact]
    public async Task ByEmployee_AggregatesPerEmployeeProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);
        var (_, projectId) = await SeedClientProjectAsync("عميل بيتا P2", "مشروع بيتا P2");
        await SeedSubmissionAsync(empId, teamId, "2098-W12", new[] { (projectId, Sample()) });

        var report = await ByEmployeesAsync(admin, $"periodKey=2098-W12&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(empId, row.EmployeeId);
        Assert.Equal(projectId, row.ProjectId);
        Assert.Equal(teamId, row.TeamId);
        Assert.Equal(20m, row.Metrics.Completed);
        Assert.Equal(80.0m, row.Metrics.CompletionRate);
    }

    // ===== (3) التجميع لكل Pod (فريق المُسلِّم) =====
    [Fact]
    public async Task ByPod_AggregatesByTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);
        var (_, projectId) = await SeedClientProjectAsync("عميل جاما P3", "مشروع جاما P3");
        await SeedSubmissionAsync(empId, teamId, "2098-W13", new[] { (projectId, Sample()) });

        var report = await ByPodsAsync(admin, $"periodKey=2098-W13&teamId={teamId}");
        var row = Assert.Single(report.Rows.Where(r => r.TeamId == teamId));
        Assert.Equal(1, row.EmployeeCount);
        Assert.Equal(1, row.ProjectCount);
        Assert.Equal(20m, row.Metrics.Completed);
    }

    // ===== (4) التجميع لكل عميل + عدّ المشاريع النشطة =====
    [Fact]
    public async Task ByClient_AggregatesAndCountsActiveProjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (clientId, projectId) = await SeedClientProjectAsync("عميل دلتا P4", "مشروع دلتا P4");
        await SeedSubmissionAsync(empId, null, "2098-W14", new[] { (projectId, Sample()) });

        var report = await ByClientsAsync(admin, $"periodKey=2098-W14&employeeId={empId}&clientId={clientId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(clientId, row.ClientId);
        Assert.Equal(1, row.ProjectCount);
        Assert.Equal(1, row.ActiveProjectCount);
        Assert.Equal(20m, row.Metrics.Completed);
    }

    // ===== (5) المقارنة الدوريّة (الفترة السابقة) =====
    [Fact]
    public async Task PeriodComparison_UsesPreviousOperationalWeek()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, projectId) = await SeedClientProjectAsync("عميل إبسيلون P5", "مشروع إبسيلون P5");

        const string currentKey = "2098-W16";
        var prevKey = ReportCalendarPolicy.PreviousPeriodKey(PeriodType.Weekly, currentKey)!;
        Assert.False(string.IsNullOrEmpty(prevKey));

        // السابق: Completed=10 ؛ الحالي: Completed=20 ⇒ تغيّر +10 صعودًا.
        await SeedSubmissionAsync(empId, null, prevKey, new[]
        {
            (projectId, new ContentValues(15, 10, 8, 1, 0, 2, "🟡 مستقر"))
        });
        await SeedSubmissionAsync(empId, null, currentKey, new[] { (projectId, Sample()) });

        var report = await ByProjectsAsync(admin, $"periodType={(int)PeriodType.Weekly}&periodKey={currentKey}&employeeId={empId}");
        Assert.Equal(prevKey, report.PreviousPeriodKey);
        var row = Assert.Single(report.Rows);
        Assert.NotNull(row.Comparison);
        Assert.True(row.Comparison!.HasPrevious);
        Assert.Equal(20m, row.Comparison.Current);   // Completed + Responses الحاليّ
        Assert.Equal(10m, row.Comparison.Previous);
        Assert.Equal(10m, row.Comparison.Change);
        Assert.Equal("up", row.Comparison.Trend);
    }

    // ===== (6) معالجة تكرار المشروع — استراتيجية A (التراكم/الجمع) =====
    [Fact]
    public async Task DuplicateProject_AccumulatesAcrossEntriesAndSubmissions_StrategyA()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, emp1) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, emp2) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, projectId) = await SeedClientProjectAsync("عميل زيتا P6", "مشروع زيتا P6");

        // نفس المشروع مرّتين في تسليم واحد (موظّف 1) + مرّة في تسليم آخر (موظّف 2) = 3 مدخلات تتراكم.
        await SeedSubmissionAsync(emp1, null, "2098-W17", new[]
        {
            (projectId, new ContentValues(10, 8, 6, 0, 0, 1, "🟢 ممتاز")),
            (projectId, new ContentValues(10, 7, 5, 1, 0, 2, "🟢 ممتاز")),
        });
        await SeedSubmissionAsync(emp2, null, "2098-W17", new[]
        {
            (projectId, new ContentValues(10, 5, 4, 0, 1, 1, "🟡 مستقر")),
        });

        var report = await ByProjectsAsync(admin, $"periodKey=2098-W17");
        var row = Assert.Single(report.Rows.Where(r => r.ProjectId == projectId));
        Assert.Equal(30m, row.Metrics.Planned);    // 10+10+10
        Assert.Equal(20m, row.Metrics.Completed);  // 8+7+5
        Assert.Equal(15m, row.Metrics.Approved);   // 6+5+4
        Assert.Equal(2, row.Contributors);          // موظّفان
        Assert.Equal(3, row.Status.Total);          // ثلاثة مدخلات في حصيلة الحالة
        Assert.Equal(2, row.Status.Healthy);
        Assert.Equal(1, row.Status.Stable);
    }

    // ===== (7) أمان النطاق/الصلاحيات (IDOR) =====
    [Fact]
    public async Task Scope_UnrelatedEmployeeSeesNothing_AndAnonymous401()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, projectId) = await SeedClientProjectAsync("عميل نطاق P7", "مشروع نطاق P7");
        await SeedSubmissionAsync(empId, null, "2098-W18", new[] { (projectId, Sample()) });

        // الأدمن (governance ⇒ SeesAll) يرى الصفّ.
        var asAdmin = await ByProjectsAsync(admin, $"periodKey=2098-W18&employeeId={empId}");
        Assert.Single(asAdmin.Rows);

        // موظّف غير مرتبط (نطاق own) لا يرى بيانات غيره ولا مشروعه.
        var (outsider, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var asOutsider = await ByProjectsAsync(outsider, $"periodKey=2098-W18&employeeId={empId}");
        Assert.Empty(asOutsider.Rows);
        Assert.Equal("self", asOutsider.ViewLevel);

        // غير مصادَق ⇒ 401 على المنافذ الأربعة.
        var anon = _factory.CreateClient();
        foreach (var path in new[] { "projects", "employees", "pods", "clients" })
        {
            var resp = await anon.GetAsync($"/api/reporting/project-execution/{path}?periodKey=2098-W18");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
    }

    // ===== (8) استبعاد المسودّات (Draft) + المدخلات الفارغة في التشخيص =====
    [Fact]
    public async Task DraftExcluded_AndEmptyEntryDiagnosed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, projectId) = await SeedClientProjectAsync("عميل إيتا P8", "مشروع إيتا P8");

        // تسليم مسودّة (Draft) لنفس الفترة — يجب أن يُستبعَد كليًّا.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var template = await db.ReportTemplates
                .Include(t => t.Versions).ThenInclude(v => v.Fields)
                .SingleAsync(t => t.Title == ProjectFirstExecutionSchema.ContentTitle);
            var version = template.Versions.First(v => v.IsPublished);
            var prsField = version.Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection);
            var draft = new ReportSubmission
            {
                ReportTemplateVersionId = version.Id,
                SubmitterId = empId,
                PeriodType = PeriodType.Weekly,
                PeriodKey = "2098-W19",
                Status = SubmissionStatus.Draft
            };
            db.ReportSubmissions.Add(draft);
            db.SubmissionFieldValues.Add(new SubmissionFieldValue
            {
                ReportSubmissionId = draft.Id,
                TemplateFieldId = prsField.Id,
                ValueJson = JsonSerializer.Serialize(new[]
                {
                    new { projectId, answers = new Dictionary<string, object> { ["delivered_pieces"] = 99 } }
                })
            });
            await db.SaveChangesAsync();
        }

        var report = await ByProjectsAsync(admin, $"periodKey=2098-W19&employeeId={empId}");
        Assert.Empty(report.Rows);
        Assert.Equal(0, report.SubmissionsConsidered); // المسودّة لا تُحتسب
    }
}

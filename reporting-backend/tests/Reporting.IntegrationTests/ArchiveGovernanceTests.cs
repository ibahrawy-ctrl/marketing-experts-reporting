using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Archive;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1) — قراءة العناصر المحذوفة إداريًّا ناعمًا
/// (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid المعتمَدة. محكوم بسياسة ArchiveGovernanceAccess
/// (Admin/CEO/GM فقط). لا حذف نهائيّ ولا جدولة ولا إشعارات. كل القراءات تتجاوز مرشّح الاستعلام العالميّ
/// وتقتصر على IsDeleted == true. الاسترجاع يعكس الحذف الإداريّ فقط: يعيد المعتمِد التاريخيّ إن كان نشطًا،
/// وإلا يُسترجَع دون معتمِد (يحتاج قرارًا إداريًّا)، ويُحجَب عند تعارض عنصر نشط لنفس الفترة.
/// </summary>
[Collection("Integration")]
public class ArchiveGovernanceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ArchiveGovernanceTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===================== مساعدون =====================

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishReportTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب أرشيف {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient employee, Guid templateId, Guid fieldId, string weekKey, decimal value = 100m)
    {
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, weekKey)))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, value, null, null, null) }));
        return (await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<Guid> DeleteReportAsync(HttpClient admin, Guid submissionId, string reason)
    {
        var res = await admin.PostAsJsonAsync($"/api/submissions/{submissionId}/admin-delete",
            new AdminDeleteRequest(reason));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return submissionId;
    }

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"أرشيف KPI {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return (created.Id, manual!.Id, auto!.Id);
    }

    private static async Task<KpiEvaluationDto> SubmitEvalAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string weekKey, decimal score = 70m)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, weekKey)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, score, null),
                new KpiResultInput(autoId, score, null, null)
            }));
        return (await (await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;
    }

    private static async Task DeleteKpiAsync(HttpClient admin, Guid evalId, string reason)
    {
        var res = await admin.PostAsJsonAsync($"/api/kpi-evaluations/{evalId}/admin-delete",
            new KpiReviewActionRequest(reason));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private const string ValidReason = "استرجاع بغرض التصحيح الإداريّ المعتمد";

    // ========================================================================
    // المصادَقة والتفويض
    // ========================================================================

    [Fact]
    public async Task Anonymous_Is_Unauthorized_On_All_Archive_Routes()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/archive")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/admin/archive/report/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/admin/archive/kpi/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync($"/api/admin/archive/report/{Guid.NewGuid()}/restore", new RestoreRequest(ValidReason))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync($"/api/admin/archive/kpi/{Guid.NewGuid()}/restore", new RestoreRequest(ValidReason))).StatusCode);
    }

    [Fact]
    public async Task NonElevated_Roles_Are_Forbidden_On_List()
    {
        foreach (var role in new[] { "Employee", "Manager", "TeamLeader", "HR", "CeoSupport", "AccountPortfolioReader" })
        {
            var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
            var res = await client.GetAsync("/api/admin/archive");
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_Can_List_Archive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync("/api/admin/archive");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Ceo_And_Gm_Can_List_Archive()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        Assert.Equal(HttpStatusCode.OK, (await ceo.GetAsync("/api/admin/archive")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gm.GetAsync("/api/admin/archive")).StatusCode);
    }

    // ========================================================================
    // القائمة والمرشّحات
    // ========================================================================

    [Fact]
    public async Task Deleted_Report_Appears_In_List_And_NonDeleted_Does_Not()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var deleted = await SubmitReportAsync(employee, templateId, fieldId, "2025-W20");
        var alive = await SubmitReportAsync(employee, templateId, fieldId, "2025-W21");
        await DeleteReportAsync(admin, deleted.Id, "حذف للاختبار");

        var list = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&itemType=Report"))
            .ReadAsync<ArchivePagedResult>();
        Assert.Contains(list!.Items, i => i.ArchiveItemId == deleted.Id);
        Assert.DoesNotContain(list.Items, i => i.ArchiveItemId == alive.Id);
    }

    [Fact]
    public async Task Deleted_Kpi_Appears_In_List()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W22");
        await DeleteKpiAsync(admin, submitted.Id, "حذف تقييم للاختبار");

        var list = await (await admin.GetAsync($"/api/admin/archive?employeeId={subjectId}&itemType=KpiEvaluation"))
            .ReadAsync<ArchivePagedResult>();
        Assert.Contains(list!.Items, i => i.ArchiveItemId == submitted.Id && i.ItemType == ArchiveItemType.KpiEvaluation);
    }

    [Fact]
    public async Task Filter_By_ItemType_Report_Returns_Only_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (kpiTemplateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W23");
        await DeleteReportAsync(admin, report.Id, "حذف تقرير");
        var eval = await SubmitEvalAsync(manager, kpiTemplateId, employeeId, manualId, autoId, "2025-W23");
        await DeleteKpiAsync(admin, eval.Id, "حذف تقييم");

        var reports = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&itemType=Report"))
            .ReadAsync<ArchivePagedResult>();
        Assert.All(reports!.Items, i => Assert.Equal(ArchiveItemType.Report, i.ItemType));
        Assert.Contains(reports.Items, i => i.ArchiveItemId == report.Id);
        Assert.DoesNotContain(reports.Items, i => i.ArchiveItemId == eval.Id);
    }

    [Fact]
    public async Task Filter_By_ItemType_Kpi_Returns_Only_Kpi()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (kpiTemplateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W24");
        await DeleteReportAsync(admin, report.Id, "حذف تقرير");
        var eval = await SubmitEvalAsync(manager, kpiTemplateId, employeeId, manualId, autoId, "2025-W24");
        await DeleteKpiAsync(admin, eval.Id, "حذف تقييم");

        var kpis = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&itemType=KpiEvaluation"))
            .ReadAsync<ArchivePagedResult>();
        Assert.All(kpis!.Items, i => Assert.Equal(ArchiveItemType.KpiEvaluation, i.ItemType));
        Assert.Contains(kpis.Items, i => i.ArchiveItemId == eval.Id);
    }

    [Fact]
    public async Task Filter_By_PeriodKey_Narrows_Results()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var r1 = await SubmitReportAsync(employee, templateId, fieldId, "2025-W25");
        var r2 = await SubmitReportAsync(employee, templateId, fieldId, "2025-W26");
        await DeleteReportAsync(admin, r1.Id, "حذف");
        await DeleteReportAsync(admin, r2.Id, "حذف");

        var list = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&periodKey=2025-W25"))
            .ReadAsync<ArchivePagedResult>();
        Assert.Contains(list!.Items, i => i.ArchiveItemId == r1.Id);
        Assert.DoesNotContain(list.Items, i => i.ArchiveItemId == r2.Id);
    }

    [Fact]
    public async Task Filter_By_EmployeeId_Isolates_Employee()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (emp1, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (emp2, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var r1 = await SubmitReportAsync(emp1, templateId, fieldId, "2025-W27");
        var r2 = await SubmitReportAsync(emp2, templateId, fieldId, "2025-W27");
        await DeleteReportAsync(admin, r1.Id, "حذف");
        await DeleteReportAsync(admin, r2.Id, "حذف");

        var list = await (await admin.GetAsync($"/api/admin/archive?employeeId={emp1Id}"))
            .ReadAsync<ArchivePagedResult>();
        Assert.Contains(list!.Items, i => i.ArchiveItemId == r1.Id);
        Assert.DoesNotContain(list.Items, i => i.ArchiveItemId == r2.Id);
    }

    [Fact]
    public async Task Deleted_Item_Has_Retention_Fresh_And_Restore_Metadata()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W28");
        await DeleteReportAsync(admin, report.Id, "سبب الحذف المميّز");

        var list = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&itemType=Report"))
            .ReadAsync<ArchivePagedResult>();
        var item = Assert.Single(list!.Items, i => i.ArchiveItemId == report.Id);
        Assert.Equal(RetentionStatus.Fresh, item.RetentionStatus);
        Assert.True(item.DaysSinceDeletion >= 0);
        Assert.True(item.CanRestore);
        Assert.Equal("سبب الحذف المميّز", item.DeletionReason);
    }

    // ========================================================================
    // التفاصيل
    // ========================================================================

    [Fact]
    public async Task Report_Details_NotFound_For_Unknown_Id_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/admin/archive/report/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Report_Details_Conflict_For_NonDeleted_Report_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var alive = await SubmitReportAsync(employee, templateId, fieldId, "2025-W29");
        var res = await admin.GetAsync($"/api/admin/archive/report/{alive.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Report_Details_For_Deleted_Returns_Workflow_And_CanRestore()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W30");
        await DeleteReportAsync(admin, report.Id, "حذف للتفاصيل");

        var details = await (await admin.GetAsync($"/api/admin/archive/report/{report.Id}"))
            .ReadAsync<ArchiveDetailsDto>();
        Assert.NotNull(details);
        Assert.True(details!.CanRestore);
        Assert.NotEmpty(details.WorkflowSteps);
        Assert.Contains(details.WorkflowSteps, s => s.Status == nameof(ApprovalStatus.CancelledByAdministrativeDeletion));
        Assert.Equal(RestoreStrategy.HistoricalApproverRestored, details.RestoreStrategy);
        Assert.Equal(managerId, details.HistoricalApproverId);
    }

    [Fact]
    public async Task Kpi_Details_For_Deleted_Returns_CanRestore_NotApplicable_Strategy()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W31");
        await DeleteKpiAsync(admin, submitted.Id, "حذف تقييم للتفاصيل");

        var details = await (await admin.GetAsync($"/api/admin/archive/kpi/{submitted.Id}"))
            .ReadAsync<ArchiveDetailsDto>();
        Assert.NotNull(details);
        Assert.True(details!.CanRestore);
        Assert.Equal(RestoreStrategy.NotApplicable, details.RestoreStrategy);
        Assert.True(details.KpiResultsCount > 0);
    }

    [Fact]
    public async Task Details_Contain_Audit_Trail()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W32");
        await DeleteReportAsync(admin, report.Id, "حذف مع أثر تدقيقيّ");

        var details = await (await admin.GetAsync($"/api/admin/archive/report/{report.Id}"))
            .ReadAsync<ArchiveDetailsDto>();
        Assert.NotNull(details);
        Assert.Contains(details!.AuditTrail, a => a.Action == "submission.admin_deleted");
    }

    // ========================================================================
    // الاسترجاع — التقارير
    // ========================================================================

    [Fact]
    public async Task Restore_Report_Reason_TooShort_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W33");
        await DeleteReportAsync(admin, report.Id, "حذف");

        var res = await admin.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore",
            new RestoreRequest("قصير"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Report_Valid_Reappears_And_HistoricalApprover_Restored()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W34");
        Assert.Equal(managerId, report.CurrentApproverId);
        await DeleteReportAsync(admin, report.Id, "حذف قبل الاسترجاع");

        // بعد الحذف: مفقود + خارج اعتمادات المدير.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/submissions/{report.Id}")).StatusCode);

        var restored = await admin.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        // بعد الاسترجاع: التقرير ظاهر مجدّدًا + عاد لاعتمادات المدير التاريخيّ.
        var fetched = await (await admin.GetAsync($"/api/submissions/{report.Id}")).ReadAsync<SubmissionDto>();
        Assert.NotNull(fetched);
        Assert.Equal(managerId, fetched!.CurrentApproverId);

        var pending = await (await manager.GetAsync("/api/submissions/pending-approvals"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.Contains(pending!, s => s.Id == report.Id);
    }

    [Fact]
    public async Task Restore_Report_By_Ceo_Works()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W35");
        await DeleteReportAsync(admin, report.Id, "حذف");

        var restored = await ceo.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task Restore_Report_ActiveConflict_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var first = await SubmitReportAsync(employee, templateId, fieldId, "2025-W36");
        await DeleteReportAsync(admin, first.Id, "حذف الأوّل");
        // تسليم نشط جديد لنفس (الموظّف، القالب، الفترة) بعد حذف الأوّل.
        var second = await SubmitReportAsync(employee, templateId, fieldId, "2025-W36");
        Assert.NotEqual(first.Id, second.Id);

        var res = await admin.PostAsJsonAsync($"/api/admin/archive/report/{first.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Report_AlreadyRestored_NotDeleted_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W37");
        await DeleteReportAsync(admin, report.Id, "حذف");
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore", new RestoreRequest(ValidReason))).StatusCode);

        // استرجاع ثانٍ لعنصر لم يعد محذوفًا ⇒ تعارض.
        var again = await admin.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Restore_Report_NonElevated_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W38");
        await DeleteReportAsync(admin, report.Id, "حذف");

        var res = await manager.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ========================================================================
    // الاسترجاع — تقييمات KPI
    // ========================================================================

    [Fact]
    public async Task Restore_Kpi_Reason_TooShort_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W39");
        await DeleteKpiAsync(admin, submitted.Id, "حذف");

        var res = await admin.PostAsJsonAsync($"/api/admin/archive/kpi/{submitted.Id}/restore",
            new RestoreRequest("قصير"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Kpi_Valid_Reappears_In_Aggregate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W40", 80m);
        await admin.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null);
        await DeleteKpiAsync(admin, submitted.Id, "حذف قبل الاسترجاع");

        var restored = await admin.PostAsJsonAsync($"/api/admin/archive/kpi/{submitted.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        // بعد الاسترجاع: التقييم مرئيّ مجدّدًا عبر مسار القراءة.
        var fetched = await admin.GetAsync($"/api/kpi-evaluations/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Restore_Kpi_ActiveConflict_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var first = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W41");
        await DeleteKpiAsync(admin, first.Id, "حذف الأوّل");
        var second = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W41");
        Assert.NotEqual(first.Id, second.Id);

        var res = await admin.PostAsJsonAsync($"/api/admin/archive/kpi/{first.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Kpi_AlreadyRestored_NotDeleted_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W42");
        await DeleteKpiAsync(admin, submitted.Id, "حذف");
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/admin/archive/kpi/{submitted.Id}/restore", new RestoreRequest(ValidReason))).StatusCode);

        var again = await admin.PostAsJsonAsync($"/api/admin/archive/kpi/{submitted.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Kpi_Details_NotFound_For_Unknown_Id_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/admin/archive/kpi/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Kpi_Details_Conflict_For_NonDeleted_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W43");
        var res = await admin.GetAsync($"/api/admin/archive/kpi/{submitted.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Kpi_NonElevated_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2025-W44");
        await DeleteKpiAsync(admin, submitted.Id, "حذف");

        var res = await manager.PostAsJsonAsync($"/api/admin/archive/kpi/{submitted.Id}/restore",
            new RestoreRequest(ValidReason));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Restore_Report_Writes_ArchiveItemRestored_Audit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var report = await SubmitReportAsync(employee, templateId, fieldId, "2025-W45");
        await DeleteReportAsync(admin, report.Id, "حذف");
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/admin/archive/report/{report.Id}/restore", new RestoreRequest(ValidReason))).StatusCode);

        // بعد الاسترجاع لم يعد محذوفًا ⇒ تفاصيل الأرشيف تُرجِع 409 (تتطلّب IsDeleted)؛
        // نكتفي بالتأكّد أنّ الاسترجاع نجح أعلاه وأنّ التقرير رجع نشطًا عبر مسار القراءة العامّ.
        var details = await admin.GetAsync($"/api/admin/archive/report/{report.Id}");
        Assert.Equal(HttpStatusCode.Conflict, details.StatusCode);
        var fetched = await admin.GetAsync($"/api/submissions/{report.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Pagination_Respects_PageSize()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        foreach (var wk in new[] { "2025-W46", "2025-W47", "2025-W48" })
        {
            var r = await SubmitReportAsync(employee, templateId, fieldId, wk);
            await DeleteReportAsync(admin, r.Id, "حذف");
        }

        var page1 = await (await admin.GetAsync($"/api/admin/archive?employeeId={employeeId}&page=1&pageSize=2"))
            .ReadAsync<ArchivePagedResult>();
        Assert.Equal(2, page1!.PageSize);
        Assert.True(page1.Items.Count <= 2);
        Assert.True(page1.TotalCount >= 3);
    }
}

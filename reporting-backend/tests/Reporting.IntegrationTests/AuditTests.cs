using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Audit;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class AuditTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuditTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب تدقيق {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    [Fact]
    public async Task SubmitAndApprove_WritesAuditTrail_VisibleToExecutive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W14")))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        await manager.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve", new ApprovalActionRequest(null));

        var logs = await (await admin.GetAsync($"/api/audit-logs?entityId={draft.Id}")).ReadAsync<List<AuditLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Action == "submission.submitted");
        Assert.Contains(logs!, l => l.Action == "submission.approved");
    }

    [Fact]
    public async Task AuditLogs_ForbiddenToEmployee_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_ForbiddenToManager_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

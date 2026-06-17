using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class ManagementNotesTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ManagementNotesTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_CanCreateNote_OnUser_AndList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var note = await (await admin.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, subjectId,
                ManagementNoteType.Guidance, "يحتاج إلى متابعة جودة التقارير.", true)))
            .ReadAsync<ManagementNoteDto>();
        Assert.NotNull(note);
        Assert.Equal(ManagementNoteEntityType.User, note!.EntityType);
        Assert.True(note.RequiresAction);
        Assert.Equal(ManagementNoteStatus.Open, note.Status);

        var list = await (await admin.GetAsync(
            $"/api/management-notes?entityType=User&entityId={subjectId}")).ReadAsync<List<ManagementNoteDto>>();
        Assert.Contains(list!, n => n.Id == note.Id);
    }

    [Fact]
    public async Task Manager_CanNote_DirectReport_ButNot_OutOfScope_403()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, directReportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, foreignId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var ok = await manager.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, directReportId,
                ManagementNoteType.Documentation, "أداء جيد هذا الأسبوع.", false));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var forbidden = await manager.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, foreignId,
                ManagementNoteType.Documentation, "خارج النطاق.", false));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateNote_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await employee.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, subjectId,
                ManagementNoteType.Documentation, "محاولة.", false));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotList_OutOfScope_Notes_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, foreignId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await manager.GetAsync($"/api/management-notes?entityType=User&entityId={foreignId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ResolveNote_FlipsStatusToResolved()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var note = await (await admin.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, subjectId,
                ManagementNoteType.FollowUp, "يتطلّب إجراء.", true)))
            .ReadAsync<ManagementNoteDto>();

        var resolved = await (await admin.PostAsync($"/api/management-notes/{note!.Id}/resolve", null))
            .ReadAsync<ManagementNoteDto>();
        Assert.Equal(ManagementNoteStatus.Resolved, resolved!.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
        Assert.NotNull(resolved.ResolvedById);
    }

    [Fact]
    public async Task CreateNote_OnMissingEntity_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, Guid.NewGuid(),
                ManagementNoteType.Documentation, "كيان غير موجود.", false));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Notes_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/management-notes?entityType=User&entityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Risk_WithNextAction_AndKpiLink_Persisted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var kpiEvalId = Guid.NewGuid();

        var risk = await (await admin.PostAsJsonAsync("/api/risks",
            new CreateRiskRequest("انخفاض KPI", "مؤشر منخفض", RiskSeverity.High, null, null, null,
                RelatedKpiEvaluationId: kpiEvalId, NextAction: "تصعيد للمدير العام")))
            .ReadAsync<RiskDto>();
        Assert.Equal(kpiEvalId, risk!.RelatedKpiEvaluationId);
        Assert.Equal("تصعيد للمدير العام", risk.NextAction);
    }
}

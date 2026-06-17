using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Governance;
using Reporting.Application.Notifications;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class GovernanceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateRisk_ThenList_AndUpdateToClosed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var risk = await (await admin.PostAsJsonAsync("/api/risks",
            new CreateRiskRequest("تأخر تسليمات الفريق", "وصف", RiskSeverity.High, null, null, null)))
            .ReadAsync<RiskDto>();
        Assert.Equal(RiskStatus.Open, risk!.Status);
        Assert.Equal(RiskSeverity.High, risk.Severity);

        var list = await (await admin.GetAsync("/api/risks?status=Open")).ReadAsync<List<RiskDto>>();
        Assert.Contains(list!, r => r.Id == risk.Id);

        var updated = await (await admin.PutAsJsonAsync($"/api/risks/{risk.Id}",
            new UpdateRiskRequest(risk.Title, risk.Description, RiskSeverity.Medium, RiskStatus.Closed, "خُفّفت")))
            .ReadAsync<RiskDto>();
        Assert.Equal(RiskStatus.Closed, updated!.Status);
        Assert.NotNull(updated.ClosedAtUtc);
    }

    [Fact]
    public async Task Employee_CannotCreateRisk_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.PostAsJsonAsync("/api/risks",
            new CreateRiskRequest("خطر", null, RiskSeverity.Low, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RaiseEscalation_NotifiesTarget_ThenResolve()
    {
        var (raiser, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Manager");

        var esc = await (await raiser.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(targetId, "تجاوز مهلة التسليم", null, null)))
            .ReadAsync<EscalationDto>();
        Assert.Equal(EscalationStatus.Open, esc!.Status);

        // الهدف يتلقّى إشعارًا
        var notifs = await (await target.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.Contains(notifs!, n => n.Type == "escalation.raised");

        // الهدف يحلّ التصعيد
        var resolved = await (await target.PostAsJsonAsync($"/api/escalations/{esc.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Resolved, "عولج"))).ReadAsync<EscalationDto>();
        Assert.Equal(EscalationStatus.Resolved, resolved!.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
    }

    [Fact]
    public async Task Escalation_OtherUser_CannotResolve_403()
    {
        var (raiser, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (intruder, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var esc = await (await raiser.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(targetId, "سبب", null, null)))
            .ReadAsync<EscalationDto>();

        var res = await intruder.PostAsJsonAsync($"/api/escalations/{esc!.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Dismissed, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateDecision_ThenUpdateToImplemented()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var decision = await (await admin.PostAsJsonAsync("/api/decisions",
            new CreateDecisionRequest("إعادة توزيع المهام", "وصف", null, null, null)))
            .ReadAsync<DecisionDto>();
        Assert.Equal(DecisionStatus.Proposed, decision!.Status);

        var updated = await (await admin.PutAsJsonAsync($"/api/decisions/{decision.Id}",
            new UpdateDecisionRequest(decision.Title, decision.Description, DecisionStatus.Implemented)))
            .ReadAsync<DecisionDto>();
        Assert.Equal(DecisionStatus.Implemented, updated!.Status);
        Assert.NotNull(updated.DecidedAtUtc);
    }

    [Fact]
    public async Task Risks_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/risks");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

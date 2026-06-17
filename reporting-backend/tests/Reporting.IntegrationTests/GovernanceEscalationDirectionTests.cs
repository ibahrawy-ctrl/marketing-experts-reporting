using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات اتجاه التصعيد (UAT Phase 3 — البند 5):
/// • التصعيد «النازل» (من مستوى إداري أعلى إلى أدنى) لا يمكن للمستهدَف رفضه.
/// • التصعيد «الصاعد» (من أدنى إلى أعلى) يمكن للسلطة الأعلى رفضه بسبب إلزامي.
/// • الرفض يتطلب سببًا، والإغلاق يتطلب تعليقًا.
/// </summary>
[Collection("Integration")]
public class GovernanceEscalationDirectionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceEscalationDirectionTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DownwardEscalation_TargetCannotDismiss()
    {
        // المدير (raiser) أعلى من الموظف (target) عبر سلسلة المديرين.
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var esc = await (await manager.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(employeeId, "متابعة أداء", null, null)))
            .ReadAsync<EscalationDto>();

        // الموظف المستهدَف يحاول الرفض → مرفوض (تصعيد نازل من سلطة أعلى).
        var res = await employee.PostAsJsonAsync($"/api/escalations/{esc!.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Dismissed, "لا أوافق"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // لكنه يستطيع الاستلام (Acknowledged).
        var ack = await (await employee.PostAsJsonAsync($"/api/escalations/{esc.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Acknowledged, null))).ReadAsync<EscalationDto>();
        Assert.Equal(EscalationStatus.Acknowledged, ack!.Status);
    }

    [Fact]
    public async Task UpwardEscalation_HigherAuthorityCanDismissWithReason()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // الموظف يرفع تصعيدًا إلى مديره (صاعد).
        var esc = await (await employee.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(managerId, "أحتاج قرارًا", null, null)))
            .ReadAsync<EscalationDto>();

        // المدير (سلطة أعلى) يرفضه بسبب → مسموح.
        var dismissed = await (await manager.PostAsJsonAsync($"/api/escalations/{esc!.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Dismissed, "خارج النطاق الحالي"))).ReadAsync<EscalationDto>();
        Assert.Equal(EscalationStatus.Dismissed, dismissed!.Status);
        Assert.NotNull(dismissed.ResolvedAtUtc);
    }

    [Fact]
    public async Task UpwardDismiss_WithoutReason_400()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var esc = await (await employee.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(managerId, "طلب", null, null)))
            .ReadAsync<EscalationDto>();

        var res = await manager.PostAsJsonAsync($"/api/escalations/{esc!.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Dismissed, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Resolve_WithoutComment_400()
    {
        var (raiser, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Manager");

        var esc = await (await raiser.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(targetId, "سبب", null, null)))
            .ReadAsync<EscalationDto>();

        var res = await target.PostAsJsonAsync($"/api/escalations/{esc!.Id}/resolve",
            new ResolveEscalationRequest(EscalationStatus.Resolved, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

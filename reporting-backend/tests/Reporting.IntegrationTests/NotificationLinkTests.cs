using System.Net.Http.Json;
using Reporting.Application.Development;
using Reporting.Application.Governance;
using Reporting.Application.Kpi;
using Reporting.Application.Notifications;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// OFFICIAL-LAUNCH-FIX-PACK-R1A (Fix 2) — روابط الإشعارات الداخلية الجديدة تُولَّد بمسارات SPA صحيحة (/app/...).
/// كل رابط يشير إلى صفحة قائمة موجودة فعلًا ⇒ لا صفحة بيضاء عند التنقّل. لا يتغيّر مخطّط NotificationDto.
/// </summary>
[Collection("Integration")]
public class NotificationLinkTests
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationLinkTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب رابط {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    // ===== 7: رابط إشعار التسليم = /app/submissions?open={id} =====
    [Fact]
    public async Task SubmissionNotification_Link_IsAppSubmissionsOpen()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W40"))).ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        var list = await (await manager.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        var notif = list!.First(n => n.Type == "submission.submitted");
        Assert.Equal($"/app/submissions?open={draft.Id}", notif.Link);
        Assert.StartsWith("/app/", notif.Link!);
    }

    // ===== 8: رابط إشعار KPI = /app/my-kpi (لا يوجد مسار بمعرّف ⇒ صفحة قائمة آمنة) =====
    [Fact]
    public async Task KpiNotification_Link_IsAppMyKpi()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 100m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(created.Id, subjectId, PeriodType.Weekly, "2026-W41")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(manual!.Id, null, 80m, null) }));
        await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);

        var list = await (await subject.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        var notif = list!.First(n => n.Type == "kpi.submitted");
        Assert.Equal("/app/my-kpi", notif.Link);
        Assert.StartsWith("/app/", notif.Link!);
    }

    // ===== 9: رابط إشعار التطوير = /app/development (صفحة قائمة آمنة) =====
    [Fact]
    public async Task DevelopmentNotification_Link_IsAppDevelopment()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await manager.PostAsJsonAsync("/api/training-needs",
            new CreateTrainingNeedRequest(subjectId, "مهارات العرض", "وصف", "تحليل KPI", null));

        var list = await (await subject.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        var notif = list!.First(n => n.Type == "training_need.raised");
        Assert.Equal("/app/development", notif.Link);
        Assert.StartsWith("/app/", notif.Link!);
    }

    // ===== 10: رابط إشعار تصعيد الحوكمة = /app/governance/escalations (صفحة قائمة آمنة) =====
    [Fact]
    public async Task GovernanceEscalationNotification_Link_IsAppGovernanceEscalations()
    {
        var (raiser, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Manager");

        await raiser.PostAsJsonAsync("/api/escalations",
            new CreateEscalationRequest(targetId, "تجاوز مهلة التسليم", null, null));

        var list = await (await target.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        var notif = list!.First(n => n.Type == "escalation.raised");
        Assert.Equal("/app/governance/escalations", notif.Link);
        Assert.StartsWith("/app/", notif.Link!);
    }

    // ===== 11: مخطّط NotificationDto لم يتغيّر (الحقول السبعة نفسها) =====
    [Fact]
    public async Task NotificationDto_SchemaUnchanged()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W42"))).ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        var raw = await (await manager.GetAsync("/api/notifications")).Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(raw);
        var el = doc.RootElement.EnumerateArray().First();
        var props = el.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(7, props.Count);
        foreach (var name in new[] { "id", "type", "title", "body", "link", "isRead", "createdAtUtc" })
            Assert.Contains(name, props);
    }
}

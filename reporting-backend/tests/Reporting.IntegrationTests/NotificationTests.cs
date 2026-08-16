using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Notifications;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class NotificationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب إشعار {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task SubmitToManagerAsync(HttpClient employee, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
    }

    [Fact]
    public async Task Submit_NotifiesApprover_AndUnreadCountIncrements()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await SubmitToManagerAsync(employee, templateId, fieldId, TestCalendar.Cycle(1));

        var list = await (await manager.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, n => n.Type == "submission.submitted" && !n.IsRead);

        var unread = await (await manager.GetAsync("/api/notifications/unread-count")).ReadAsync<UnreadCountDto>();
        Assert.True(unread!.Count >= 1);
    }

    [Fact]
    public async Task MarkRead_ClearsSingleNotification()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await SubmitToManagerAsync(employee, templateId, fieldId, TestCalendar.Cycle(2));

        var list = await (await manager.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        var target = list!.First(n => !n.IsRead);

        var readRes = await manager.PostAsync($"/api/notifications/{target.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        var after = await (await manager.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.True(after!.Single(n => n.Id == target.Id).IsRead);
    }

    [Fact]
    public async Task MarkRead_UnknownId_404()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MarkAllRead_ClearsUnreadCount()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await SubmitToManagerAsync(employee, templateId, fieldId, TestCalendar.Cycle(3));
        await SubmitToManagerAsync(employee, templateId, fieldId, TestCalendar.Cycle(4));

        await manager.PostAsync("/api/notifications/read-all", null);

        var unread = await (await manager.GetAsync("/api/notifications/unread-count")).ReadAsync<UnreadCountDto>();
        Assert.Equal(0, unread!.Count);
    }

    [Fact]
    public async Task Notifications_DoNotLeak_AcrossUsers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (otherManager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");

        await SubmitToManagerAsync(employee, templateId, fieldId, "2026-W18");

        // المدير الآخر لا يرى إشعار اعتماد ليس له
        var otherList = await (await otherManager.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.DoesNotContain(otherList!, n => n.Type == "submission.submitted");
    }

    [Fact]
    public async Task Anonymous_CannotListNotifications_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record UnreadCountDto(int Count);
}

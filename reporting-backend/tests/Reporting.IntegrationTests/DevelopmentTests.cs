using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Development;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class DevelopmentTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DevelopmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateTrainingNeed_SubjectCanView_OtherEmployeeCannot()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var need = await (await manager.PostAsJsonAsync("/api/training-needs",
            new CreateTrainingNeedRequest(subjectId, "مهارات العرض", "وصف", "تحليل KPI", null)))
            .ReadAsync<TrainingNeedDto>();
        Assert.Equal(TrainingNeedStatus.Open, need!.Status);

        // الموظف صاحب الاحتياج يراه
        var mine = await subject.GetAsync($"/api/training-needs/{need.Id}");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        // موظف آخر لا يراه (IDOR)
        var forbidden = await other.GetAsync($"/api/training-needs/{need.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateTrainingNeed_403()
    {
        var (employee, otherId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.PostAsJsonAsync("/api/training-needs",
            new CreateTrainingNeedRequest(otherId, "عنوان", null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingNeed_TransitionsStatus()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var need = await (await manager.PostAsJsonAsync("/api/training-needs",
            new CreateTrainingNeedRequest(subjectId, "احتياج", null, null, null)))
            .ReadAsync<TrainingNeedDto>();

        var updated = await (await manager.PutAsJsonAsync($"/api/training-needs/{need!.Id}",
            new UpdateTrainingNeedRequest(need.Title, "محدّث", TrainingNeedStatus.Completed)))
            .ReadAsync<TrainingNeedDto>();
        Assert.Equal(TrainingNeedStatus.Completed, updated!.Status);
    }

    [Fact]
    public async Task CreateImprovementPlan_SubjectCanView()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var plan = await (await manager.PostAsJsonAsync("/api/improvement-plans",
            new CreateImprovementPlanRequest(subjectId, "خطة تحسين الأداء", "وصف", DateTime.UtcNow.AddDays(30), null)))
            .ReadAsync<ImprovementPlanDto>();
        Assert.Equal(ImprovementPlanStatus.Open, plan!.Status);

        var mine = await (await subject.GetAsync("/api/improvement-plans")).ReadAsync<List<ImprovementPlanDto>>();
        Assert.Contains(mine!, p => p.Id == plan.Id);
    }

    [Fact]
    public async Task ImprovementPlans_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/improvement-plans");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

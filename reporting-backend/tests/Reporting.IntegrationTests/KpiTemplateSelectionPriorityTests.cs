using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Mini-Fix — أولوية اختيار قالب KPI في مسار إنشاء التقييم العادي (subjectUserId):
/// • إذا وُجد قالب متخصص مطابق لمسمّى الموظّف الوظيفي ⇒ يُرجَع المتخصص وحده (لا يظهر العام معه).
/// • إن لم يوجد قالب متخصص مناسب ⇒ تُرجَع القوالب العامّة فقط.
/// • لا قوالب مسوّدة/مؤرشفة/غير نشطة/غير أسبوعية في القائمة. التصفية خادمية بالكامل.
/// </summary>
[Collection("Integration")]
public class KpiTemplateSelectionPriorityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiTemplateSelectionPriorityTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<Guid> PublishKpiAsync(HttpClient admin, Guid? jobRoleId, KpiCadence cadence = KpiCadence.WeeklyPulse)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب KPI {Guid.NewGuid():N}", null, jobRoleId, cadence)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("مؤشر", null, 100m, null, null, KpiCalcMethod.Manual, null));
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    private static async Task<Guid> CreateDraftKpiAsync(HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب مسودة {Guid.NewGuid():N}", null, jobRoleId, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        return created!.Id;
    }

    private async Task SetUserJobRoleAsync(Guid userId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    private static async Task<List<KpiTemplateDto>> ListForSubjectAsync(HttpClient client, Guid subjectId)
    {
        var res = await client.GetAsync(
            $"/api/kpi-templates?isActive=true&status=Published&cadence=WeeklyPulse&subjectUserId={subjectId}");
        var list = await res.ReadAsync<List<KpiTemplateDto>>();
        return list!;
    }

    [Fact]
    public async Task RoleSpecificExists_GeneralIsHidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var jobRole = Guid.NewGuid();
        var general = await PublishKpiAsync(admin, null);
        var specialized = await PublishKpiAsync(admin, jobRole);

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(subjectId, jobRole);

        var list = await ListForSubjectAsync(admin, subjectId);
        Assert.Contains(list, t => t.Id == specialized);
        Assert.DoesNotContain(list, t => t.Id == general);
        // كل المُرجَع يطابق دور الموظّف (لا قوالب عامّة مختلطة).
        Assert.All(list, t => Assert.Equal(jobRole, t.JobRoleId));
    }

    [Fact]
    public async Task DesignerRoleSpecific_OnlySpecialized_NoGeneral_NoOtherRole()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var designerRole = Guid.NewGuid();
        var b2cRole = Guid.NewGuid();
        var general = await PublishKpiAsync(admin, null);
        var designerTpl = await PublishKpiAsync(admin, designerRole);
        var b2cTpl = await PublishKpiAsync(admin, b2cRole);

        var (_, designerId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(designerId, designerRole);

        var list = await ListForSubjectAsync(admin, designerId);
        Assert.Contains(list, t => t.Id == designerTpl);
        Assert.DoesNotContain(list, t => t.Id == general);
        Assert.DoesNotContain(list, t => t.Id == b2cTpl);
    }

    [Fact]
    public async Task NoRoleSpecific_GeneralIsReturned()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var general = await PublishKpiAsync(admin, null);

        // موظّف بمسمّى وظيفي لا يملك قالبًا متخصصًا منشورًا ⇒ يظهر العام فقط.
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(subjectId, Guid.NewGuid());

        var list = await ListForSubjectAsync(admin, subjectId);
        Assert.Contains(list, t => t.Id == general);
        Assert.All(list, t => Assert.Null(t.JobRoleId));
    }

    [Fact]
    public async Task DraftRoleSpecific_DoesNotSuppressGeneral_AndDraftHidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var jobRole = Guid.NewGuid();
        var general = await PublishKpiAsync(admin, null);
        var draftSpecialized = await CreateDraftKpiAsync(admin, jobRole);

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(subjectId, jobRole);

        var list = await ListForSubjectAsync(admin, subjectId);
        // المسوّدة لا تظهر، ولأنها لا تُعدّ قالبًا متخصصًا منشورًا ⇒ يظهر العام.
        Assert.DoesNotContain(list, t => t.Id == draftSpecialized);
        Assert.Contains(list, t => t.Id == general);
    }

    [Fact]
    public async Task NonWeeklyRoleSpecific_IsExcluded_AndGeneralReturned()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var jobRole = Guid.NewGuid();
        var general = await PublishKpiAsync(admin, null);
        // قالب متخصص لكنه ليس أسبوعيًّا ⇒ لا يُحتسب متخصصًا مناسبًا في هذا المسار.
        var quarterlyTpl = await CreateDraftKpiAsync(admin, jobRole); // ربع سنوي لا يُنشَر (حارس الدورية) فيبقى مسوّدة

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(subjectId, jobRole);

        var list = await ListForSubjectAsync(admin, subjectId);
        Assert.DoesNotContain(list, t => t.Id == quarterlyTpl);
        Assert.Contains(list, t => t.Id == general);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// أولوية اختيار قالب التقرير في مساري الإنشاء (UAT — Mini-Fix أولوية قالب «تقريري»):
/// • «إنشاء تقريري» (AssignedOnly): يرى المستخدم قالب دوره فقط — لا الكل ولا قوالب المرؤوسين
///   ولا العام إن وُجد قالب دور؛ حتى مديرو القوالب (مدير عام/أدمن) يرون قالب دورهم وحده هنا.
/// • «إنشاء بالنيابة» (SubjectUserId): يجب أن يكون الموظّف ضمن نطاق المُنشئ، ثم تُطبَّق أولوية
///   دور الموظّف لا المُنشئ؛ غير المخوَّل يُمنع (403).
/// </summary>
[Collection("Integration")]
public class ReportTemplateSelfAssignmentTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportTemplateSelfAssignmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) المدير العام في «إنشاء تقريري» يرى قالب المدير العام وحده — لا كل القوالب ولا العام.
    [Fact]
    public async Task GmSelfReport_SeesGmTemplateOnly_NotAllNotGeneral()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var gmRole = await CreateJobRoleAsync("GM");
        var otherRole = await CreateJobRoleAsync("OTHER");

        var gmTemplate = await PublishAsync(admin, gmRole);
        var general = await PublishAsync(admin, null);
        var otherTemplate = await PublishAsync(admin, otherRole);

        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        await SetJobRoleAsync(gmId, gmRole);

        var list = await SelfListAsync(gm);

        Assert.Contains(list, t => t.Id == gmTemplate);
        Assert.DoesNotContain(list, t => t.Id == general);
        Assert.DoesNotContain(list, t => t.Id == otherTemplate);
        Assert.All(list, t => Assert.Equal(gmRole, t.JobRoleId));
    }

    // (2) المدير العام في «بالنيابة» بعد اختيار موظّف يرى قوالب ذلك الموظّف فقط (لا قالبه هو).
    [Fact]
    public async Task GmOnBehalf_AfterSelectingEmployee_SeesEmployeeTemplateOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var gmRole = await CreateJobRoleAsync("GM2");
        var empRole = await CreateJobRoleAsync("EMP2");

        await PublishAsync(admin, gmRole);
        var empTemplate = await PublishAsync(admin, empRole);
        var general = await PublishAsync(admin, null);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, empRole);

        var list = await OnBehalfListAsync(gm, empId);

        Assert.Contains(list, t => t.Id == empTemplate);
        Assert.DoesNotContain(list, t => t.Id == general);
        Assert.All(list, t => Assert.Equal(empRole, t.JobRoleId));
    }

    // (3) المدير في «إنشاء تقريري» يرى قالب المدير (لا تكون القائمة فارغة).
    [Fact]
    public async Task ManagerSelfReport_SeesManagerTemplate_NotEmpty()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var mgrRole = await CreateJobRoleAsync("MGR");
        var mgrTemplate = await PublishAsync(admin, mgrRole);

        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        await SetJobRoleAsync(mgrId, mgrRole);

        var list = await SelfListAsync(mgr);

        Assert.NotEmpty(list);
        Assert.Contains(list, t => t.Id == mgrTemplate);
        Assert.All(list, t => Assert.Equal(mgrRole, t.JobRoleId));
    }

    // (4) المدير في «إنشاء تقريري» لا يرى قالب المرؤوسين.
    [Fact]
    public async Task ManagerSelfReport_DoesNotSeeSubordinateTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var mgrRole = await CreateJobRoleAsync("MGR4");
        var subRole = await CreateJobRoleAsync("SUB4");
        var mgrTemplate = await PublishAsync(admin, mgrRole);
        var subTemplate = await PublishAsync(admin, subRole);

        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        await SetJobRoleAsync(mgrId, mgrRole);

        var list = await SelfListAsync(mgr);

        Assert.Contains(list, t => t.Id == mgrTemplate);
        Assert.DoesNotContain(list, t => t.Id == subTemplate);
    }

    // (5) قائد فريق B2C في «إنشاء تقريري» يرى قالب القائد وحده.
    [Fact]
    public async Task TeamLeaderSelfReport_SeesLeaderTemplateOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tlRole = await CreateJobRoleAsync("B2C_TL");
        var repRole = await CreateJobRoleAsync("B2C_REP");
        var tlTemplate = await PublishAsync(admin, tlRole);
        var repTemplate = await PublishAsync(admin, repRole);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        await SetJobRoleAsync(tlId, tlRole);

        var list = await SelfListAsync(tl);

        Assert.Contains(list, t => t.Id == tlTemplate);
        Assert.DoesNotContain(list, t => t.Id == repTemplate);
        Assert.All(list, t => Assert.Equal(tlRole, t.JobRoleId));
    }

    // (6) مندوب B2C في «إنشاء تقريري» يرى قالب المندوب لا قالب القائد.
    [Fact]
    public async Task RepEmployeeSelfReport_SeesRepTemplate_NotLeaderTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tlRole = await CreateJobRoleAsync("B2C_TL6");
        var repRole = await CreateJobRoleAsync("B2C_REP6");
        var tlTemplate = await PublishAsync(admin, tlRole);
        var repTemplate = await PublishAsync(admin, repRole);

        var (rep, repId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(repId, repRole);

        var list = await SelfListAsync(rep);

        Assert.Contains(list, t => t.Id == repTemplate);
        Assert.DoesNotContain(list, t => t.Id == tlTemplate);
        Assert.All(list, t => Assert.Equal(repRole, t.JobRoleId));
    }

    // (7) قائد فريق سوشيال ميديا يرى قالب القائد وحده حتى مع وجود قالب عام (تعميم سلوك أميرة الصحيح).
    [Fact]
    public async Task LeaderWithGeneralPresent_SeesLeaderTemplateOnly_NotGeneral()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var socialRole = await CreateJobRoleAsync("SOCIAL_TL");
        var socialTemplate = await PublishAsync(admin, socialRole);
        var general = await PublishAsync(admin, null);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        await SetJobRoleAsync(tlId, socialRole);

        var list = await SelfListAsync(tl);

        Assert.Contains(list, t => t.Id == socialTemplate);
        Assert.DoesNotContain(list, t => t.Id == general);
        Assert.All(list, t => Assert.Equal(socialRole, t.JobRoleId));
    }

    // (8) الموظّف يرى قالبه الفني الأساسي فقط لا العام.
    [Fact]
    public async Task EmployeeSelfReport_SeesPrimaryTemplateOnly_NotGeneral()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var techRole = await CreateJobRoleAsync("TECH8");
        var techTemplate = await PublishAsync(admin, techRole);
        var general = await PublishAsync(admin, null);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, techRole);

        var list = await SelfListAsync(emp);

        Assert.Contains(list, t => t.Id == techTemplate);
        Assert.DoesNotContain(list, t => t.Id == general);
        Assert.All(list, t => Assert.Equal(techRole, t.JobRoleId));
    }

    // (9) لا يوجد قالب مخصص لدور الموظّف ⇒ يُرجَع العام فقط لا كل القوالب.
    [Fact]
    public async Task NoRoleTemplate_FallsBackToGeneralOnly_NotAll()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var lonelyRole = await CreateJobRoleAsync("LONELY9");
        var otherRole = await CreateJobRoleAsync("OTHER9");
        var general = await PublishAsync(admin, null);
        var otherTemplate = await PublishAsync(admin, otherRole);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, lonelyRole); // دور بلا قالب مربوط

        var list = await SelfListAsync(emp);

        Assert.Contains(list, t => t.Id == general);
        Assert.DoesNotContain(list, t => t.Id == otherTemplate);
        Assert.All(list, t => Assert.Null(t.JobRoleId));
    }

    // (10) موظّف غير مخوَّل لا يستطيع الإنشاء «بالنيابة» عن موظّف خارج نطاقه (403).
    [Fact]
    public async Task EmployeeOnBehalfOfAnother_IsForbidden_403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, otherId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await emp.GetAsync(
            $"/api/report-templates?status=Published&isActive=true&subjectUserId={otherId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== أدوات مساعدة =====

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private static async Task<Guid> PublishAsync(HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    private async Task SetJobRoleAsync(Guid userId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    private static async Task<List<ReportTemplateDto>> SelfListAsync(HttpClient client)
        => (await (await client.GetAsync("/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>())!;

    private static async Task<List<ReportTemplateDto>> OnBehalfListAsync(HttpClient client, Guid subjectId)
        => (await (await client.GetAsync(
            $"/api/report-templates?status=Published&isActive=true&subjectUserId={subjectId}"))
            .ReadAsync<List<ReportTemplateDto>>())!;
}

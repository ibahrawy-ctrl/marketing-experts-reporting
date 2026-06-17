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

[Collection("Integration")]
public class ReportTemplateTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportTemplateTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullTemplateLifecycle_Create_AddField_Publish_LockedThenNewVersion()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // إنشاء
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("تقرير مشتري الإعلانات", "أسبوعي", null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(TemplateStatus.Draft, created!.Status);
        var versionId = created.Versions.Single().Id;

        // إضافة حقل
        var addRes = await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق الإعلاني", "spend", FieldType.Currency, true, null, null));
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);

        // نشر
        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
        var published = await publishRes.ReadAsync<TemplateVersionDto>();
        Assert.True(published!.IsPublished);

        // لا يمكن التعديل بعد النشر
        var lockedRes = await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("حقل جديد", null, FieldType.Number, false, null, null));
        Assert.Equal(HttpStatusCode.Conflict, lockedRes.StatusCode);

        // إنشاء إصدار مسودة جديد يستنسخ الحقول
        var draftRes = await admin.PostAsync($"/api/report-templates/{created.Id}/versions", null);
        Assert.Equal(HttpStatusCode.OK, draftRes.StatusCode);
        var draft = await draftRes.ReadAsync<TemplateVersionDto>();
        Assert.Equal(2, draft!.VersionNumber);
        Assert.Single(draft.Fields);
        Assert.False(draft.IsPublished);
    }

    [Fact]
    public async Task PublishEmptyVersion_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("قالب فارغ", null, null, PeriodType.Monthly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, publishRes.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateTemplate_403()
    {
        var employee = await TestAuth.LoginAsRoleAsync(_factory, Roles.Employee);
        var res = await employee.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("غير مصرّح", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotListTemplates_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/report-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>القالب الجديد يكون «أساسيًّا» (إلزاميًّا) افتراضيًّا — UAT Phase 3 البند 9.</summary>
    [Fact]
    public async Task NewTemplate_DefaultsToPrimaryClassification()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب تصنيف {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateClassification.Primary, created!.Classification);
    }

    /// <summary>
    /// حارس منع ازدواج التقارير الأسبوعية (UAT Phase 3 — البند 9):
    /// موظّف مرتبط بمسمّى وظيفي له قالبان أسبوعيّان يرى الأساسي «إلزاميًّا» والتكميلي «اختياريًّا»،
    /// فلا يُفرض عليه تقريران إلزاميّان لنفس الأسبوع.
    /// </summary>
    [Fact]
    public async Task RoleWithTwoWeeklyTemplates_OnePrimary_OneSupplementary()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var primary = await PublishWeeklyTemplateAsync(admin, "أساسي");
        var supplementary = await PublishWeeklyTemplateAsync(admin, "تكميلي");

        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var code = $"DUAL_{Guid.NewGuid():N}".Substring(0, 12);
        await BindTemplatesToJobRoleAsync(employeeId, code, primary, supplementary,
            supplementaryId: supplementary);

        var list = await (await employee.GetAsync("/api/report-templates?assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>();

        var p = list!.Single(t => t.Id == primary);
        var s = list.Single(t => t.Id == supplementary);
        Assert.Equal(TemplateClassification.Primary, p.Classification);
        Assert.Equal(TemplateClassification.Supplementary, s.Classification);
    }

    private static async Task<Guid> PublishWeeklyTemplateAsync(HttpClient admin, string tag)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {tag} {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    /// <summary>يربط القالبَين بمسمّى وظيفي للموظّف، ويصنّف القالب التكميلي «اختياريًّا» عبر قاعدة البيانات.</summary>
    private async Task BindTemplatesToJobRoleAsync(Guid userId, string code, Guid t1, Guid t2, Guid supplementaryId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobRole = await db.JobRoles.FirstOrDefaultAsync(j => j.Code == code);
        if (jobRole is null)
        {
            jobRole = new JobRole { NameAr = $"مسمّى {code}", Code = code };
            db.JobRoles.Add(jobRole);
            await db.SaveChangesAsync();
        }
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        foreach (var id in new[] { t1, t2 })
        {
            var tpl = await db.ReportTemplates.FirstAsync(t => t.Id == id);
            tpl.JobRoleId = jobRole.Id;
            tpl.Classification = id == supplementaryId
                ? TemplateClassification.Supplementary
                : TemplateClassification.Primary;
        }
        await db.SaveChangesAsync();
    }
}

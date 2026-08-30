using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// TASK 3 Part 2 — إدارة إصدارات القالب: عرض عدد التقارير المرتبطة بكل نسخة
/// + حذف النسخ غير المستخدَمة فقط (لا نسخة مستخدَمة/وحيدة/أحدث/منشورة حالية، ولا حذف القالب نفسه).
/// </summary>
[Collection("Integration")]
public class TemplateVersionManagementTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TemplateVersionManagementTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ينشئ قالبًا، يضيف حقلًا، وينشر الإصدار الأول (v1).
    private static async Task<(Guid templateId, Guid v1Id)> CreatePublishedTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب نسخ {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var v1 = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{v1}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{v1}/publish", null);
        return (created.Id, v1);
    }

    // ينشئ إصدارًا جديدًا (مسودة) وينشره؛ يُرجِع معرّفه.
    private static async Task<Guid> CreateAndPublishNextVersionAsync(HttpClient admin, Guid templateId)
    {
        var draft = await (await admin.PostAsync($"/api/report-templates/{templateId}/versions", null))
            .ReadAsync<TemplateVersionDto>();
        await admin.PostAsync($"/api/report-templates/versions/{draft!.Id}/publish", null);
        return draft.Id;
    }

    // يربط تسليمًا مباشرةً بنسخة معيّنة عبر قاعدة البيانات (لمحاكاة نسخة مستخدَمة تاريخيًّا).
    private async Task SeedSubmissionForVersionAsync(Guid versionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = Guid.NewGuid(),
            PeriodType = PeriodType.Weekly,
            PeriodKey = $"2099-W{Random.Shared.Next(1, 52):D2}",
            Status = SubmissionStatus.Submitted,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ReportTemplateDetailDto> GetDetailAsync(HttpClient admin, Guid templateId)
        => (await (await admin.GetAsync($"/api/report-templates/{templateId}"))
            .ReadAsync<ReportTemplateDetailDto>())!;

    [Fact]
    public async Task Detail_ExposesPerVersionUsageAndDeletability()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        var v2 = await CreateAndPublishNextVersionAsync(admin, templateId);

        var detail = await GetDetailAsync(admin, templateId);
        var ver1 = detail.Versions.Single(v => v.Id == v1);
        var ver2 = detail.Versions.Single(v => v.Id == v2);

        Assert.Equal(0, ver1.SubmissionCount);
        Assert.Equal(0, ver2.SubmissionCount);
        Assert.True(ver2.IsCurrentPublished);
        Assert.False(ver1.IsCurrentPublished);
        // v1 غير مستخدَمة وليست الوحيدة ولا الأحدث ولا المنشورة الحالية ⇒ قابلة للحذف.
        Assert.True(ver1.CanDelete);
        Assert.Null(ver1.DeleteBlockReason);
        // v2 هي المنشورة الحالية والأحدث ⇒ غير قابلة للحذف.
        Assert.False(ver2.CanDelete);
        Assert.NotNull(ver2.DeleteBlockReason);
    }

    [Fact]
    public async Task DeleteUnusedOldVersion_Succeeds_204_WithoutDeletingTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        await CreateAndPublishNextVersionAsync(admin, templateId);

        var del = await admin.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // القالب ما زال موجودًا وله نسخة واحدة فقط الآن.
        var detail = await GetDetailAsync(admin, templateId);
        Assert.Single(detail.Versions);
        Assert.DoesNotContain(detail.Versions, v => v.Id == v1);
    }

    [Fact]
    public async Task DeleteUsedVersion_IsForbidden_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        await SeedSubmissionForVersionAsync(v1);
        await CreateAndPublishNextVersionAsync(admin, templateId);

        var detail = await GetDetailAsync(admin, templateId);
        var ver1 = detail.Versions.Single(v => v.Id == v1);
        Assert.Equal(1, ver1.SubmissionCount);
        Assert.False(ver1.CanDelete);
        Assert.Contains("مستخدمة", ver1.DeleteBlockReason);

        var del = await admin.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task DeleteCurrentPublishedVersion_IsForbidden_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await CreatePublishedTemplateAsync(admin);
        var v2 = await CreateAndPublishNextVersionAsync(admin, templateId);

        var del = await admin.DeleteAsync($"/api/report-templates/versions/{v2}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task DeleteOnlyVersion_IsForbidden_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, v1) = await CreatePublishedTemplateAsync(admin);

        var del = await admin.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task DeleteNonExistentVersion_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var del = await admin.DeleteAsync($"/api/report-templates/versions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotDeleteVersion_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        await CreateAndPublishNextVersionAsync(admin, templateId);

        var employee = await TestAuth.LoginAsRoleAsync(_factory, Roles.Employee);
        var del = await employee.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotDeleteVersion_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        await CreateAndPublishNextVersionAsync(admin, templateId);

        var manager = await TestAuth.LoginAsRoleAsync(_factory, Roles.Manager);
        var del = await manager.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotDeleteVersion_401()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, v1) = await CreatePublishedTemplateAsync(admin);

        var anon = _factory.CreateClient();
        var del = await anon.DeleteAsync($"/api/report-templates/versions/{v1}");
        Assert.Equal(HttpStatusCode.Unauthorized, del.StatusCode);
    }

    [Fact]
    public async Task TemplatesList_StillWorks_AfterVersionDelete()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        await CreateAndPublishNextVersionAsync(admin, templateId);
        await admin.DeleteAsync($"/api/report-templates/versions/{v1}");

        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, t => t.Id == templateId);
    }

    // ===== R22A — حارس إنشاء الإصدارات: مسودات بذر راكدة أدنى من المنشور الحاليّ =====
    // كانت القاعدة «أيّ إصدار غير منشور يحجب» تُجمِّد القالب إلى الأبد حين تُخلِّف البذرة مسودات
    // أرقامها أدنى من الإصدار المنشور (غير قابلة للبلوغ: الفعّال هو أعلى رقم منشور).

    /// <summary>يزرع في القاعدة إصدارات إضافيّة تحاكي شذوذ البذر: مسودتان راكدتان ثمّ منشور أعلى رقمًا بحقل واحد.</summary>
    private async Task<Guid> SeedStaleDraftsBelowPublishedHeadAsync(Guid templateId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var maxNumber = await db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId).MaxAsync(v => v.VersionNumber);

        db.ReportTemplateVersions.AddRange(
            new Domain.Entities.Templates.ReportTemplateVersion
            { ReportTemplateId = templateId, VersionNumber = maxNumber + 1, IsPublished = false },
            new Domain.Entities.Templates.ReportTemplateVersion
            { ReportTemplateId = templateId, VersionNumber = maxNumber + 2, IsPublished = false });

        var head = new Domain.Entities.Templates.ReportTemplateVersion
        {
            ReportTemplateId = templateId,
            VersionNumber = maxNumber + 3,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        head.Fields.Add(new Domain.Entities.Templates.TemplateField
        {
            Label = "قيمة الرأس",
            Key = "head_value",
            FieldType = FieldType.Number,
            IsRequired = true,
            Order = 1,
        });
        db.ReportTemplateVersions.Add(head);
        await db.SaveChangesAsync();
        return head.Id;
    }

    [Fact]
    public async Task CreateDraftVersion_Allowed_WhenStaleDraftsAreBelowPublishedHead()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await CreatePublishedTemplateAsync(admin);
        var headId = await SeedStaleDraftsBelowPublishedHeadAsync(templateId);

        var res = await admin.PostAsync($"/api/report-templates/{templateId}/versions", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var draft = await res.ReadAsync<TemplateVersionDto>();
        Assert.NotNull(draft);
        Assert.False(draft!.IsPublished);

        var detail = await GetDetailAsync(admin, templateId);
        // المسودات الراكدة لم تُمَسّ: تبقى موجودة وغير منشورة، والإصدار الجديد أعلى رقمًا من رأس المنشور.
        var head = detail.Versions.Single(v => v.Id == headId);
        Assert.True(draft.VersionNumber > head.VersionNumber);
        Assert.Equal(3, detail.Versions.Count(v => !v.IsPublished));  // المسودتان الراكدتان + المسودة الجديدة
        Assert.Contains(detail.Versions, v => v.Id == v1);
        // الحقول تُنسَخ من رأس المنشور لا من المسودات الراكدة.
        var fields = detail.Versions.Single(v => v.Id == draft.Id).Fields;
        Assert.Equal("head_value", Assert.Single(fields).Key);
    }

    [Fact]
    public async Task CreateDraftVersion_Blocked_WhenLatestVersionIsAnOpenDraft()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await CreatePublishedTemplateAsync(admin);
        await SeedStaleDraftsBelowPublishedHeadAsync(templateId);

        var first = await admin.PostAsync($"/api/report-templates/{templateId}/versions", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // مسودة مفتوحة واحدة فقط: المحاولة الثانية تُرفَض ولا تُنشِئ صفًّا جديدًا.
        var before = (await GetDetailAsync(admin, templateId)).Versions.Count;
        var second = await admin.PostAsync($"/api/report-templates/{templateId}/versions", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(before, (await GetDetailAsync(admin, templateId)).Versions.Count);
    }
}

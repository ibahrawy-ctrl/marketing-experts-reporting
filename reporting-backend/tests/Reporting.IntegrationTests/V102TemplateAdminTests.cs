using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// V1.0.2 — تحسينات إدارة قوالب التقارير: الحذف/الأرشفة الآمنة + التدقيق،
/// المعاينة كموظّف (بلا إنشاء تسليم)، وتغطية القالب (المرتبطون/المستثنون) بنفس أولوية الاختيار.
/// </summary>
[Collection("Integration")]
public class V102TemplateAdminTests
{
    private readonly CustomWebApplicationFactory _factory;

    public V102TemplateAdminTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== الحذف/الأرشفة الآمنة + التدقيق =====

    [Fact]
    public async Task NewDraftTemplate_CanHardDelete_True()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateDraftAsync(admin, "مسودة قابلة للحذف");
        Assert.Equal(TemplateStatus.Draft, created.Status);
        Assert.True(created.CanHardDelete);
        Assert.Equal(0, created.SubmissionCount);
    }

    [Fact]
    public async Task PublishedTemplate_CanHardDelete_False()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "منشور غير قابل للحذف");
        var detail = await (await admin.GetAsync($"/api/report-templates/{id}")).ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, detail!.Status);
        Assert.False(detail.CanHardDelete);
    }

    [Fact]
    public async Task HardDelete_UnusedDraft_Succeeds_AndIsGone_AndAudited()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateDraftAsync(admin, "حذف نهائي");

        var del = await admin.DeleteAsync($"/api/report-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var after = await admin.GetAsync($"/api/report-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);

        await AssertAuditExistsAsync("template.deleted", created.Id);
    }

    [Fact]
    public async Task HardDelete_PublishedTemplate_IsRejected_409_AndPreserved()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "محاولة حذف منشور");

        var del = await admin.DeleteAsync($"/api/report-templates/{id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);

        // ما زال موجودًا (لم يُحذف).
        var after = await admin.GetAsync($"/api/report-templates/{id}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task HardDelete_DraftWithSubmissions_IsRejected_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateDraftAsync(admin, "مسودة مستخدَمة");
        var versionId = created.Versions.Single().Id;

        // إدراج تسليم مباشر يشير إلى إصدار القالب (محاكاة قالب مستخدَم).
        await SeedSubmissionAsync(versionId);

        var del = await admin.DeleteAsync($"/api/report-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);

        var detail = await (await admin.GetAsync($"/api/report-templates/{created.Id}")).ReadAsync<ReportTemplateDetailDto>();
        Assert.True(detail!.SubmissionCount >= 1);
        Assert.False(detail.CanHardDelete);
    }

    [Fact]
    public async Task Archive_SetsStatusArchived_AndInactive_AndAudited()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "للأرشفة");

        var arch = await admin.PostAsync($"/api/report-templates/{id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, arch.StatusCode);

        var detail = await (await admin.GetAsync($"/api/report-templates/{id}")).ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Archived, detail!.Status);
        Assert.False(detail.IsActive);

        await AssertAuditExistsAsync("template.archived", id);
    }

    // ===== المعاينة كموظّف (بلا إنشاء تسليم) =====

    [Fact]
    public async Task Preview_ReturnsFieldsInOrder_AndCreatesNoSubmission()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "معاينة");

        var before = await CountSubmissionsAsync();
        var preview = await (await admin.GetAsync($"/api/report-templates/{id}/preview")).ReadAsync<TemplatePreviewDto>();
        var after = await CountSubmissionsAsync();

        Assert.NotNull(preview);
        Assert.True(preview!.IsPublished);
        Assert.NotEmpty(preview.Fields);
        // الحقول مرتّبة تصاعديًّا.
        Assert.Equal(preview.Fields.OrderBy(f => f.Order).Select(f => f.Id), preview.Fields.Select(f => f.Id));
        // المعاينة لا تُنشئ أي تسليم.
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Preview_DraftOnly_ShowsDraftFields_NotPublished()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateDraftAsync(admin, "معاينة مسودة");
        var versionId = created.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("ملاحظة", "note", FieldType.ShortText, false, null, null));

        var preview = await (await admin.GetAsync($"/api/report-templates/{created.Id}/preview")).ReadAsync<TemplatePreviewDto>();
        Assert.False(preview!.IsPublished);
        Assert.Single(preview.Fields);
    }

    // ===== تغطية القالب (المرتبطون/المستثنون) =====

    [Fact]
    public async Task Assignments_RoleSpecificTemplate_MatchesSameRole_ExcludesOthers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync($"دور {Guid.NewGuid():N}".Substring(0, 14));
        var (_, matchUserId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetUserJobRoleAsync(matchUserId, roleId);
        var (_, otherUserId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var id = await PublishTemplateAsync(admin, "متخصص", roleId);

        var asg = await (await admin.GetAsync($"/api/report-templates/{id}/assignments")).ReadAsync<TemplateAssignmentsDto>();
        Assert.NotNull(asg);
        Assert.True(asg!.IsRoleSpecific);
        Assert.True(asg.IsAssignable);
        // الموظّف صاحب نفس المسمّى يظهر ضمن المرتبطين.
        Assert.Contains(asg.MatchedUsers, u => u.UserId == matchUserId);
        // السلوك المعتمد: «بقيّة موظّفي الشركة» (مسمّى مختلف) لا يظهرون لا في المرتبطين ولا في المستثنين —
        // عدم المطابقة لمسمّى قالب متخصّص ليست استثناءً ذا معنى للعرض، فتُتجاهَل ولا تُحشى بها قائمة المستثنين.
        Assert.DoesNotContain(asg.MatchedUsers, u => u.UserId == otherUserId);
        Assert.DoesNotContain(asg.ExcludedUsers, u => u.UserId == otherUserId);
        // لا يُنتِج المحرّك سبب «excludedBecauseRoleMismatch» إطلاقًا.
        Assert.DoesNotContain(asg.ExcludedUsers, u => u.ExclusionReason == "excludedBecauseRoleMismatch");
        // كل استثناء معروض يجب أن يكون ذا معنى فقط: استثناء يدوي / يوجد قالب أخصّ / معطّل / غير قابل للإسناد.
        var meaningfulExclusions = new[]
        {
            "excludedManually",
            "excludedBecauseInactive",
            "excludedBecauseMoreSpecificTemplateExists",
            "excludedBecauseTemplateNotAssignable",
        };
        Assert.All(asg.ExcludedUsers, u => Assert.Contains(u.ExclusionReason!, meaningfulExclusions));
    }

    [Fact]
    public async Task Assignments_GeneralTemplate_ExcludesUserWithSpecializedRole()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync($"دور {Guid.NewGuid():N}".Substring(0, 14));
        var (_, specializedUserId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetUserJobRoleAsync(specializedUserId, roleId);

        // قالب متخصص منشور لهذا المسمّى → يجعل حامله مستثنى من القالب العام.
        await PublishTemplateAsync(admin, "متخصص للدور", roleId);
        var generalId = await PublishTemplateAsync(admin, "عام");

        var asg = await (await admin.GetAsync($"/api/report-templates/{generalId}/assignments")).ReadAsync<TemplateAssignmentsDto>();
        Assert.False(asg!.IsRoleSpecific);
        Assert.Contains(asg.ExcludedUsers,
            u => u.UserId == specializedUserId && u.ExclusionReason == "excludedBecauseMoreSpecificTemplateExists");
    }

    [Fact]
    public async Task Assignments_InactiveUser_IsExcludedWithReason()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, inactiveUserId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetUserActiveAsync(inactiveUserId, false);

        var generalId = await PublishTemplateAsync(admin, "عام لاختبار الموقوف");

        var asg = await (await admin.GetAsync($"/api/report-templates/{generalId}/assignments")).ReadAsync<TemplateAssignmentsDto>();
        Assert.Contains(asg!.ExcludedUsers,
            u => u.UserId == inactiveUserId && u.ExclusionReason == "excludedBecauseInactive");
        Assert.DoesNotContain(asg.MatchedUsers, u => u.UserId == inactiveUserId);
    }

    // ===== الصلاحيات (TemplateGovernance فقط) =====

    [Fact]
    public async Task Employee_CannotPreviewOrAssignmentsOrArchiveOrDelete_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "صلاحيات موظّف");
        var employee = await TestAuth.LoginAsRoleAsync(_factory, Roles.Employee);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/report-templates/{id}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/report-templates/{id}/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsync($"/api/report-templates/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.DeleteAsync($"/api/report-templates/{id}")).StatusCode);
    }

    [Fact]
    public async Task TeamLeader_CannotArchiveOrDelete_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var id = await PublishTemplateAsync(admin, "صلاحيات قائد فريق");
        var leader = await TestAuth.LoginAsRoleAsync(_factory, Roles.TeamLeader);

        Assert.Equal(HttpStatusCode.Forbidden, (await leader.PostAsync($"/api/report-templates/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leader.DeleteAsync($"/api/report-templates/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leader.GetAsync($"/api/report-templates/{id}/assignments")).StatusCode);
    }

    // ===== أدوات مساعدة =====

    private static async Task<ReportTemplateDetailDto> CreateDraftAsync(HttpClient admin, string title)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"{title} {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        return created!;
    }

    private static async Task<Guid> PublishTemplateAsync(HttpClient admin, string title, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"{title} {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الحقل الأول", "f1", FieldType.ShortText, true, null, null));
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الحقل الثاني", "f2", FieldType.Number, false, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    private async Task<Guid> CreateJobRoleAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobRole = new JobRole { NameAr = $"مسمّى {code}", Code = code };
        db.JobRoles.Add(jobRole);
        await db.SaveChangesAsync();
        return jobRole.Id;
    }

    private async Task SetUserJobRoleAsync(Guid userId, Guid jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    private async Task SetUserActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsActive = active;
        await db.SaveChangesAsync();
    }

    private async Task SeedSubmissionAsync(Guid versionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = Guid.NewGuid(),
            PeriodType = PeriodType.Weekly,
            PeriodKey = "2026-W25",
            Status = SubmissionStatus.Draft
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountSubmissionsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ReportSubmissions.CountAsync();
    }

    private async Task AssertAuditExistsAsync(string action, Guid entityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.AuditLogs.AnyAsync(a => a.Action == action && a.EntityId == entityId);
        Assert.True(exists, $"توقّعنا سجل تدقيق «{action}» للكيان {entityId}.");
    }
}

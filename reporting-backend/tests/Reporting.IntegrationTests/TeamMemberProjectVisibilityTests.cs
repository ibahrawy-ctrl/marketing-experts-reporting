using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// PROJ-TEAM-MEMBER-VIEW-R1 — رؤية عضو الفريق التنفيذي لمشاريع فريقه داخل تقارير PRS.
/// عضو الفريق (User.TeamId == Project.OwnerTeamId) يستطيع رؤية/اختيار مشروع فريقه داخل التقرير،
/// دون أي صلاحية إدارة/تعديل/أرشفة/اعتماد. لا يرى مشاريع فرق أخرى. سلوك Admin/AM/TeamLeader لا يتغيّر.
/// </summary>
[Collection("Integration")]
public class TeamMemberProjectVisibilityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamMemberProjectVisibilityTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string ConfigJson =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"seoStatus\",\"label\":\"حالة المشروع\",\"type\":\"ShortText\",\"required\":true}]}";

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, string name, Guid? ownerTeamId)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        return (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, name, ServiceType.Seo, OwnerTeamId: ownerTeamId)))
            .ReadAsync<ProjectDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishPrsTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب فريق {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("مشاريع SEO", "seo_projects", FieldType.ProjectRepeatableSection, true, null, ConfigJson)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    // ===== 1: عضو الفريق يرى مشروع فريقه (GET + قائمة الاختيار) =====
    [Fact]
    public async Task TeamMember_SeesOwnTeamProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var project = await CreateProjectAsync(admin, "مشروع فريقي", teamId);

        var get = await member.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.Contains(list!, p => p.Id == project.Id);
    }

    // ===== 2: عضو الفريق لا يرى مشروع فريق آخر =====
    [Fact]
    public async Task TeamMember_DoesNotSeeOtherTeamProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var myTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);

        var (otherLead, otherLeadId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var otherTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, otherLeadId);
        var otherProject = await CreateProjectAsync(admin, "مشروع فريق آخر", otherTeam);

        var get = await member.GetAsync($"/api/projects/{otherProject.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.DoesNotContain(list!, p => p.Id == otherProject.Id);
    }

    // ===== 3: عضو الفريق لا يستطيع تعديل المشروع (سياسة ManagementOnly) =====
    [Fact]
    public async Task TeamMember_CannotEditTeamProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var project = await CreateProjectAsync(admin, "مشروع للتعديل", teamId);

        var update = await member.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest("اسم معدّل", ServiceType.Seo, ProjectStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        var archive = await member.PostAsync($"/api/projects/{project.Id}/archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    // ===== 4: عضو الفريق لا يستطيع اعتماد تقرير غيره بسبب هذا التعديل =====
    [Fact]
    public async Task TeamMember_CannotApproveOthersSubmission()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);

        var (templateId, _) = await PublishPrsTemplateAsync(admin);
        var (other, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var otherDraft = (await (await other.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W30"))).ReadAsync<SubmissionDto>())!.Id;

        var approve = await member.PostAsync($"/api/submissions/{otherDraft}/approve", null);
        Assert.False(approve.IsSuccessStatusCode);
    }

    // ===== 5: سلوك Admin/AM/TeamLeader لا يتغيّر =====
    [Fact]
    public async Task Admin_And_TeamLeader_Visibility_Unchanged()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var project = await CreateProjectAsync(admin, "مشروع الفريق", teamId);

        // Admin (SeesAll) يرى المشروع.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        // قائد الفريق يرى مشروع فريقه (قاعدة القيادة القائمة، بلا تغيير).
        Assert.Equal(HttpStatusCode.OK, (await leader.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        // موظف غير منتمٍ لأي فريق لا يرى المشروع.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/projects/{project.Id}")).StatusCode);
    }

    // ===== 6: حفظ/إرسال PRS ينجح لمشروع يملكه فريق العضو =====
    [Fact]
    public async Task TeamMember_CanSaveAndSubmitPrs_ForOwnTeamProject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var project = await CreateProjectAsync(admin, "مشروع PRS", teamId);
        var (templateId, fieldId) = await PublishPrsTemplateAsync(admin);

        var draftId = (await (await member.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W31"))).ReadAsync<SubmissionDto>())!.Id;

        await member.PutAsJsonAsync($"/api/submissions/{draftId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, null, null, null,
                $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"seoStatus\":\"تحت التنفيذ\"}}}}]") }));

        var submit = await member.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.True(submit.IsSuccessStatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1 — P9: عقد الوصول الشخصيّ E2E عبر HTTP.
/// يُثبِت أنّ: القائد يرى «تقاريري» (mine) و«تقارير الفريق» (نطاق team) معًا؛ القائد يُنشئ/يُرسِل مسودّته
/// الشخصيّة؛ الموظّف العاديّ (نطاق own) لا يرى تقارير الفريق؛ من لا قالب له لا يظهر كـ«متوقّع»؛
/// ما هو Expected لكن غير قابل للإسناد (CanSubmit=false) يُرفَض إرساله بالحارس المركزيّ.
/// قراءة/تحقّق فقط — لا يمسّ ScopeResolver ولا التوجيه ولا الهيكل التنظيميّ.
/// </summary>
[Collection("Integration")]
public class RoleAwarePersonalAccessE2ETests
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAwarePersonalAccessE2ETests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string GuardCode = "report.template_not_assigned";

    // مفتاح الدورة الأسبوعيّة الحاليّة (بدأت فعلًا) لتفادي رفض calendar.cycle_not_open للدورات المستقبليّة.
    private static string CurrentWeek() =>
        ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(DateTime.UtcNow));

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Leader;   // TeamLeader — نطاق team
        public required (HttpClient C, Guid Id) Member;   // Employee — ManagerId=Leader
        public required (HttpClient C, Guid Id) Ordinary; // Employee — بلا مدير، نطاق own
        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var leader = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var member = await TestAuth.CreateUserAsync(_factory, Roles.Employee, leader.UserId);
        var ordinary = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        return new Org
        {
            Leader = (leader.Client, leader.UserId),
            Member = (member.Client, member.UserId),
            Ordinary = (ordinary.Client, ordinary.UserId),
            Admin = admin,
        };
    }

    // ===== 1) القائد يرى «تقاريري» و«تقارير الفريق» معًا. =====
    [Fact]
    public async Task Leader_SeesMineAndTeamReports_Together()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishGeneralTemplateAsync(org.Admin);

        var leaderOwn = await SubmitReportAsync(org.Leader.C, templateId, fieldId, CurrentWeek());
        var memberOwn = await SubmitReportAsync(org.Member.C, templateId, fieldId, CurrentWeek());

        // «تقاريري» = تسليماته الشخصيّة فقط.
        var mine = await (await org.Leader.C.GetAsync("/api/submissions/mine"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.Contains(leaderOwn.Id, mine!.Select(s => s.Id));
        Assert.DoesNotContain(memberOwn.Id, mine.Select(s => s.Id));

        // «تقارير الفريق» = نطاق team (نفسه + مرؤوسوه المباشرون).
        var team = await (await org.Leader.C.GetAsync("/api/submissions"))
            .ReadAsync<List<SubmissionListItemDto>>();
        var teamIds = team!.Select(s => s.SubmitterId).ToList();
        Assert.Contains(org.Leader.Id, teamIds);
        Assert.Contains(org.Member.Id, teamIds);
    }

    // ===== 2) الموظّف العاديّ (نطاق own) لا يرى تقارير الفريق — تسليمه فقط. =====
    [Fact]
    public async Task OrdinaryEmployee_NoTeamReports_OnlyOwn()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishGeneralTemplateAsync(org.Admin);

        var mine = await SubmitReportAsync(org.Ordinary.C, templateId, fieldId, CurrentWeek());
        var memberOwn = await SubmitReportAsync(org.Member.C, templateId, fieldId, CurrentWeek());

        var list = await (await org.Ordinary.C.GetAsync("/api/submissions"))
            .ReadAsync<List<SubmissionListItemDto>>();
        var submitters = list!.Select(s => s.SubmitterId).Distinct().ToList();

        Assert.Contains(org.Ordinary.Id, submitters);
        Assert.DoesNotContain(org.Member.Id, submitters);
        Assert.DoesNotContain(org.Leader.Id, submitters);
        Assert.Single(submitters); // النطاق own ⇒ نفسه حصرًا
        Assert.Contains(mine.Id, list.Select(s => s.Id));
        Assert.DoesNotContain(memberOwn.Id, list.Select(s => s.Id));
    }

    // ===== 3) القائد يُنشئ ويُرسِل مسودّته الشخصيّة. =====
    [Fact]
    public async Task Leader_CreatesAndSubmitsPersonalDraft()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishGeneralTemplateAsync(org.Admin);

        var draft = await (await org.Leader.C.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, CurrentWeek())))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, draft!.Status);

        var save = await org.Leader.C.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 99m, null, null, null) }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await org.Leader.C.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        Assert.Equal(org.Leader.Id, submitted.SubmitterId);
    }

    // ===== 4) من لا قالب له لا يظهر كـ«متوقّع غير مُقدَّم» في العرض الموحّد. =====
    [Fact]
    public async Task NoTemplateUser_NotExpected_InOverview()
    {
        var org = await BuildOrgAsync();

        var overview = await (await org.Ordinary.C.GetAsync("/api/submissions/overview"))
            .ReadAsync<UnifiedSubmissionOverviewDto>();

        Assert.DoesNotContain(overview!.Items,
            r => r.SubmitterId == org.Ordinary.Id && r.IsExpectedSubmission);
        Assert.Equal(0, overview.Summary.ExpectedMissingCount);
        Assert.Equal(overview.Summary.Total, overview.TotalCount); // ثبات العقد
    }

    // ===== 5) Expected=true لكن غير قابل للإسناد (CanSubmit=false) ⇒ إرساله مرفوض بالحارس. =====
    [Fact]
    public async Task ExpectedButNotAssignable_SubmissionForbidden()
    {
        var org = await BuildOrgAsync();

        // قالب مرتبط بمسمّى «س» (Expected لحاملي المسمّى)، والموظّف يحمل مسمّى «ص» مختلفًا.
        var templateRoleId = await CreateJobRoleAsync("EXPX");
        var otherRoleId = await CreateJobRoleAsync("EXPY");
        var templateId = await PublishRoleTemplateAsync(org.Admin, templateRoleId);
        await SetJobRoleAsync(org.Ordinary.Id, otherRoleId);

        var res = await org.Ordinary.C.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, CurrentWeek()));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains(GuardCode, await res.Content.ReadAsStringAsync());
    }

    // ===== أدوات =====

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishGeneralTemplateAsync(HttpClient admin)
        => await PublishTemplateWithFieldAsync(admin, null);

    private static async Task<Guid> PublishRoleTemplateAsync(HttpClient admin, Guid jobRoleId)
        => (await PublishTemplateWithFieldAsync(admin, jobRoleId)).TemplateId;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateWithFieldAsync(
        HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly,
                TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(HttpClient c, Guid templateId, Guid fieldId, string period)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 12m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>())!;
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task SetJobRoleAsync(Guid userId, Guid roleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = roleId;
        await db.SaveChangesAsync();
    }
}

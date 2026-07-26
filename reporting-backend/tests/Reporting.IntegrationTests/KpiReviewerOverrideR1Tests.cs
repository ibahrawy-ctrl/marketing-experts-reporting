using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// KPI-REVIEWER-OVERRIDE-R1 — ثلاثة تعديلات مترابطة على مسار تقييم KPI:
/// (A1) نطاق «الموظّفون القابلون للتقييم» يشمل من عُيِّن لهم المستخدم الحالي مُراجِعًا صريحًا
///      (KpiReviewerOverrideUserId) دون أيّ تغيير في ManagerId أو أدوار Identity أو ScopeResolver.
/// (A3) مسار قراءة صرف /api/kpi-evaluations/lookup لتحميل تقييم قائم بلا أيّ أثر جانبيّ (لا إنشاء).
/// (A4) حين يكون المُدخِل نفسه هو المُراجِع الصريح ⇒ اعتماد مباشر عند الإرسال، بلا سقوط إلى ManagerId
///      وبلا رفض، مع حدث مراجعة وتدقيق يوضّحان سبب الاعتماد المباشر. لا يعمل الاستثناء بلا تجاوز صريح.
/// </summary>
[Collection("Integration")]
public class KpiReviewerOverrideR1Tests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiReviewerOverrideR1Tests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== A1 — نطاق الموظّفين القابلين للتقييم يشمل حاملي التجاوز الصريح =====

    [Fact]
    public async Task EvaluatableSubjects_IncludesExplicitOverrideSubjects_WithoutManagerIdChange()
    {
        // إبراهيم (CEO) بلا مرؤوسين مباشرين؛ أربعة موظّفين مديرهم شخص آخر لكن مراجِع KPI لهم = إبراهيم.
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var otherManager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subjects = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var u = await TestAuth.CreateUserAsync(_factory, Roles.Employee, otherManager.UserId);
            await SetKpiReviewerOverrideAsync(u.UserId, ibrahim.UserId);
            subjects.Add(u.UserId);
        }

        // موظّف خامس بلا تجاوز ومديره شخص آخر ⇒ يجب ألّا يظهر لإبراهيم.
        var outsider = await TestAuth.CreateUserAsync(_factory, Roles.Employee, otherManager.UserId);

        var dto = await (await ibrahim.Client.GetAsync("/api/kpi-evaluations/evaluatable-subjects"))
            .ReadAsync<EvaluatableSubjectsDto>();

        Assert.NotNull(dto);
        Assert.False(dto!.IsAdminOverride); // لم يُمنَح دور Admin
        var ids = dto.Subjects.Select(s => s.Id).ToHashSet();
        Assert.All(subjects, id => Assert.Contains(id, ids));
        Assert.DoesNotContain(outsider.UserId, ids);
        Assert.DoesNotContain(ibrahim.UserId, ids); // لا تقييم ذاتيّ

        // ManagerId لم يتغيّر لأيّ منهم.
        foreach (var id in subjects)
            Assert.Equal(otherManager.UserId, await GetManagerIdAsync(id));
    }

    [Fact]
    public async Task EvaluatableSubjects_KeepsDirectReports_AlongsideOverrideSubjects()
    {
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var directReport = await TestAuth.CreateUserAsync(_factory, Roles.Manager, ibrahim.UserId);
        var otherManager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var overrideSubject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, otherManager.UserId);
        await SetKpiReviewerOverrideAsync(overrideSubject.UserId, ibrahim.UserId);

        var dto = await (await ibrahim.Client.GetAsync("/api/kpi-evaluations/evaluatable-subjects"))
            .ReadAsync<EvaluatableSubjectsDto>();

        var ids = dto!.Subjects.Select(s => s.Id).ToHashSet();
        Assert.Contains(directReport.UserId, ids);   // المرؤوس المباشر باقٍ (لا تراجع)
        Assert.Contains(overrideSubject.UserId, ids); // ومن عُيِّن له تجاوز صريح
    }

    // ===== A2 — تصفية القائمة خادميًّا بـsubjectUserId =====

    [Fact]
    public async Task List_FilteredBySubjectUserId_ReturnsOnlyThatSubject()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var a = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var b = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);

        await CreateWithResultsAsync(manager.Client, templateId, a.UserId, "2026-W20", manualId, autoId);
        await CreateWithResultsAsync(manager.Client, templateId, b.UserId, "2026-W20", manualId, autoId);

        var filtered = await (await manager.Client.GetAsync($"/api/kpi-evaluations?subjectUserId={a.UserId}"))
            .ReadAsync<List<KpiEvaluationListItemDto>>();

        Assert.NotNull(filtered);
        Assert.NotEmpty(filtered!);
        Assert.All(filtered!, x => Assert.Equal(a.UserId, x.SubjectUserId));
        Assert.DoesNotContain(filtered!, x => x.SubjectUserId == b.UserId);
    }

    // ===== A3 — مسار القراءة الصرف: يُحمّل التقييم القائم بلا إنشاء نسخة ثانية =====

    [Fact]
    public async Task Lookup_ReturnsExistingApprovedEvaluation_WithoutCreatingDuplicate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var otherManager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var fatma = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport, otherManager.UserId);
        await SetKpiReviewerOverrideAsync(fatma.UserId, ibrahim.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(ibrahim.Client, templateId, fatma.UserId, "2026-W28", manualId, autoId);
        var submitted = (await (await ibrahim.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;

        var before = await CountEvaluationsAsync(fatma.UserId, "2026-W28");

        var lookup = await (await ibrahim.Client.GetAsync(
            $"/api/kpi-evaluations/lookup?subjectUserId={fatma.UserId}&periodKey=2026-W28&kpiTemplateId={templateId}"))
            .ReadAsync<KpiEvaluationLookupDto>();

        Assert.NotNull(lookup);
        Assert.True(lookup!.Found);
        Assert.Equal(created.Id, lookup.Evaluation!.Id);
        Assert.Equal(submitted.TotalScore, lookup.Evaluation.TotalScore);
        Assert.Equal(submitted.Status, lookup.Evaluation.Status);

        // لا سجلّ جديد نتيجة القراءة.
        Assert.Equal(before, await CountEvaluationsAsync(fatma.UserId, "2026-W28"));
    }

    [Fact]
    public async Task Lookup_NoMatch_ReturnsNotFoundFlag_WithoutCreatingRecord()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var (templateId, _, _) = await PublishKpiAsync(admin);

        var lookup = await (await manager.Client.GetAsync(
            $"/api/kpi-evaluations/lookup?subjectUserId={subject.UserId}&periodKey=2026-W28&kpiTemplateId={templateId}"))
            .ReadAsync<KpiEvaluationLookupDto>();

        Assert.NotNull(lookup);
        Assert.False(lookup!.Found);
        Assert.Null(lookup.Evaluation);
        Assert.Equal(0, await CountEvaluationsAsync(subject.UserId, "2026-W28"));
    }

    [Fact]
    public async Task Lookup_Forbidden_ForUnrelatedManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var managerA = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var managerB = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerA.UserId);
        var (templateId, _, _) = await PublishKpiAsync(admin);

        var res = await managerB.Client.GetAsync(
            $"/api/kpi-evaluations/lookup?subjectUserId={subject.UserId}&periodKey=2026-W28&kpiTemplateId={templateId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== A4 — الاعتماد المباشر حين يكون المُدخِل هو المُراجِع الصريح =====

    [Fact]
    public async Task Submit_ByExplicitReviewerHimself_ApprovesDirectly_NoFallbackToManagerChain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var ahmed = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager, ibrahim.UserId);
        var fatma = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport, ahmed.UserId);
        await SetKpiReviewerOverrideAsync(fatma.UserId, ibrahim.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(ibrahim.Client, templateId, fatma.UserId, "2026-W29", manualId, autoId);
        var submitted = (await (await ibrahim.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;

        Assert.Equal(KpiEvaluationStatus.Approved, submitted.Status);
        Assert.Equal(ibrahim.UserId, submitted.ReviewerId);
        Assert.NotNull(submitted.TotalScore);
        Assert.NotEqual(ahmed.UserId, submitted.ReviewerId); // لم يُوجَّه إلى سلسلة المدير

        var (status, reviewerId, reviewedAtUtc) = await GetEvaluationStateAsync(created.Id);
        Assert.Equal(KpiEvaluationStatus.Approved, status);
        Assert.Equal(ibrahim.UserId, reviewerId);
        Assert.NotNull(reviewedAtUtc);
    }

    [Fact]
    public async Task Submit_ByExplicitReviewerHimself_WritesReviewEventAndAudit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var otherManager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var fatma = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport, otherManager.UserId);
        await SetKpiReviewerOverrideAsync(fatma.UserId, ibrahim.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(ibrahim.Client, templateId, fatma.UserId, "2026-W29", manualId, autoId);
        await ibrahim.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null);

        var events = await (await ibrahim.Client.GetAsync($"/api/kpi-evaluations/{created.Id}/review-events"))
            .ReadAsync<List<KpiEvaluationReviewEventDto>>();
        Assert.NotNull(events);
        var direct = Assert.Single(events!, e => e.Action == "ApprovedByExplicitReviewerOverride");
        Assert.Equal(ibrahim.UserId, direct.ActorId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == created.Id && a.Action == "kpi.approved_direct_by_reviewer_override")
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal(ibrahim.UserId, audit!.ActorId);
        Assert.Contains(ibrahim.UserId.ToString(), audit.DataJson ?? string.Empty);
        Assert.Contains("KpiReviewerOverrideUserId", audit.DataJson ?? string.Empty);
    }

    [Fact]
    public async Task Submit_WithoutExplicitOverride_DoesNotApproveDirectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, ibrahim.UserId);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(manager.Client, templateId, subject.UserId, "2026-W29", manualId, autoId);
        var submitted = (await (await manager.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;

        // بلا تجاوز صريح ⇒ لا اعتماد مباشر إطلاقًا؛ يبقى قيد المراجعة لدى مراجِع آخر.
        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.NotEqual(manager.UserId, submitted.ReviewerId);

        var events = await (await manager.Client.GetAsync($"/api/kpi-evaluations/{created.Id}/review-events"))
            .ReadAsync<List<KpiEvaluationReviewEventDto>>();
        Assert.DoesNotContain(events!, e => e.Action == "ApprovedByExplicitReviewerOverride");
    }

    [Fact]
    public async Task Submit_OverrideOnOtherReviewer_RoutesToThatReviewer_NotDirectApproval()
    {
        // التجاوز الصريح يشير إلى شخص ثالث غير المُدخِل ⇒ مراجعة عادية لدى ذلك الشخص (لا اعتماد مباشر).
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        await SetKpiReviewerOverrideAsync(subject.UserId, ibrahim.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(manager.Client, templateId, subject.UserId, "2026-W29", manualId, autoId);
        var submitted = (await (await manager.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;

        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.Equal(ibrahim.UserId, submitted.ReviewerId);
    }

    [Fact]
    public async Task DirectApproval_DoesNotChangeManagerIdOrIdentityRoles()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var ahmed = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager, ibrahim.UserId);
        var fatma = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport, ahmed.UserId);
        await SetKpiReviewerOverrideAsync(fatma.UserId, ibrahim.UserId);

        var rolesBefore = await GetRolesAsync(ibrahim.UserId);

        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var created = await CreateWithResultsAsync(ibrahim.Client, templateId, fatma.UserId, "2026-W29", manualId, autoId);
        await ibrahim.Client.PostAsync($"/api/kpi-evaluations/{created.Id}/submit", null);

        Assert.Equal(ahmed.UserId, await GetManagerIdAsync(fatma.UserId));
        Assert.Null(await GetManagerIdAsync(ibrahim.UserId));
        Assert.Equal(rolesBefore, await GetRolesAsync(ibrahim.UserId));
        Assert.DoesNotContain(Roles.Admin, await GetRolesAsync(ibrahim.UserId));
    }

    // ===== أدوات =====

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
        return (created.Id, manual!.Id, auto!.Id);
    }

    private static async Task<KpiEvaluationDto> CreateWithResultsAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, string period, Guid manualId, Guid autoId)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, period)))
            .ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(ev);
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));
        return ev;
    }

    private async Task SetKpiReviewerOverrideAsync(Guid userId, Guid? reviewerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.KpiReviewerOverrideUserId = reviewerId;
        await db.SaveChangesAsync();
    }

    private async Task<Guid?> GetManagerIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.ManagerId).FirstAsync();
    }

    private async Task<List<string>> GetRolesAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == userId
                           select r.Name!).ToListAsync();
        roles.Sort(StringComparer.Ordinal);
        return roles;
    }

    private async Task<int> CountEvaluationsAsync(Guid subjectUserId, string periodKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.KpiEvaluations.AsNoTracking()
            .CountAsync(e => e.SubjectUserId == subjectUserId && e.PeriodKey == periodKey);
    }

    private async Task<(KpiEvaluationStatus Status, Guid? ReviewerId, DateTime? ReviewedAtUtc)> GetEvaluationStateAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = await db.KpiEvaluations.AsNoTracking().FirstAsync(x => x.Id == id);
        return (e.Status, e.ReviewerId, e.ReviewedAtUtc);
    }
}

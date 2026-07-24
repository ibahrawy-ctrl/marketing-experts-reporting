using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1 — P6/P7.
/// تجاوز صريح لمعتمِد التقارير (ReportApproverOverrideUserId) ومراجِع KPI (KpiReviewerOverrideUserId)
/// على ApplicationUser: الأربعة (أحمد عبدالرؤوف/محسن مجدي/محمد عبدالله/فاطمة محمد) يتوجّه اعتماد
/// تقاريرهم ومراجعة KPI الخاصة بهم مباشرةً إلى إبراهيم البحراوي دون خطوة قائد فريق/مدير وسيط، ودون
/// تغيير ManagerId/TeamId/DepartmentId ولا سلسلة المدير القائمة. مستخدِم خامس بلا تجاوز يبقى على
/// مساره القديم. Override غير صالح (غير موجود/غير نشط/هو الموضوع/هو المُدخِل) ⇒ خطأ إعداد صريح
/// دون تجاهل صامت ودون سقوط للمسار القديم.
/// </summary>
[Collection("Integration")]
public class RoleAwarePersonalReportOverrideTests
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAwarePersonalReportOverrideTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Ibrahim;   // CEO — المعتمِد/المراجِع الصريح (بلا مدير)
        public required (HttpClient C, Guid Id) Ahmed;      // GeneralManager — ManagerId=إبراهيم
        public required (HttpClient C, Guid Id) Mohsen;     // HR — ManagerId=أحمد
        public required (HttpClient C, Guid Id) Mohamed;    // Manager — ManagerId=أحمد
        public required (HttpClient C, Guid Id) Fatma;      // CeoSupport — ManagerId=إبراهيم
        public required (HttpClient C, Guid Id) Fifth;      // موظّف عادي بلا تجاوز — ManagerId=أحمد
        public required HttpClient Admin;
    }

    /// <summary>
    /// يبني الهيكل: إبراهيم(CEO) → أحمد(GM) → {محسن,محمد,خامس}؛ وفاطمة مديرها إبراهيم. ثم يضبط التجاوز
    /// الصريح للتقارير وKPI = إبراهيم للأربعة فقط عبر AppDbContext (لا يمسّ ManagerId/TeamId/DepartmentId).
    /// </summary>
    private async Task<Org> BuildOrgAsync()
    {
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var ahmed = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager, ibrahim.UserId);
        var mohsen = await TestAuth.CreateUserAsync(_factory, Roles.Hr, ahmed.UserId);
        var mohamed = await TestAuth.CreateUserAsync(_factory, Roles.Manager, ahmed.UserId);
        var fatma = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport, ibrahim.UserId);
        var fifth = await TestAuth.CreateUserAsync(_factory, Roles.Employee, ahmed.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        foreach (var id in new[] { ahmed.UserId, mohsen.UserId, mohamed.UserId, fatma.UserId })
        {
            await SetReportApproverOverrideAsync(id, ibrahim.UserId);
            await SetKpiReviewerOverrideAsync(id, ibrahim.UserId);
        }

        return new Org
        {
            Ibrahim = (ibrahim.Client, ibrahim.UserId),
            Ahmed = (ahmed.Client, ahmed.UserId),
            Mohsen = (mohsen.Client, mohsen.UserId),
            Mohamed = (mohamed.Client, mohamed.UserId),
            Fatma = (fatma.Client, fatma.UserId),
            Fifth = (fifth.Client, fifth.UserId),
            Admin = admin,
        };
    }

    // ===== P6 — اعتماد التقارير للأربعة يتوجّه مباشرةً إلى إبراهيم (0 خطوات قبله، لا وسيط أحمد). =====

    [Fact]
    public async Task Report_Ahmed_RoutesDirectlyToIbrahim_NoIntermediate()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Ahmed.C, templateId, fieldId, "2026-W20");
        AssertRoutesDirectlyToIbrahim(submitted, org.Ibrahim.Id, org.Ahmed.Id);
    }

    [Fact]
    public async Task Report_Mohsen_RoutesDirectlyToIbrahim_ManagerIdUnchanged()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Mohsen.C, templateId, fieldId, "2026-W20");
        AssertRoutesDirectlyToIbrahim(submitted, org.Ibrahim.Id, org.Ahmed.Id);

        // ManagerId لم يتغيّر: يبقى أحمد كما هو (التجاوز لا يمسّ الهيكل).
        Assert.Equal(org.Ahmed.Id, await GetManagerIdAsync(org.Mohsen.Id));
    }

    [Fact]
    public async Task Report_Mohamed_RoutesDirectlyToIbrahim_ManagerIdUnchanged()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Mohamed.C, templateId, fieldId, "2026-W20");
        AssertRoutesDirectlyToIbrahim(submitted, org.Ibrahim.Id, org.Ahmed.Id);
        Assert.Equal(org.Ahmed.Id, await GetManagerIdAsync(org.Mohamed.Id));
    }

    [Fact]
    public async Task Report_Fatma_RoutesDirectlyToIbrahim_NoTeamLeaderStep()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Fatma.C, templateId, fieldId, "2026-W20");
        AssertRoutesDirectlyToIbrahim(submitted, org.Ibrahim.Id, org.Ahmed.Id);
    }

    // ===== P6 — Returned→Resubmit يعود لإبراهيم دون وسيط. =====
    [Fact]
    public async Task Report_Ahmed_ReturnedThenResubmit_ReturnsToIbrahim()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Ahmed.C, templateId, fieldId, "2026-W23");
        Assert.Equal(org.Ibrahim.Id, submitted.CurrentApproverId);

        // إبراهيم (المعتمِد الحاليّ) يُعيد التقرير.
        var returnRes = await org.Ibrahim.C.PostAsJsonAsync($"/api/submissions/{submitted.Id}/return",
            new ApprovalActionRequest("يرجى التعديل"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, returnRes.StatusCode);
        var returned = (await returnRes.ReadAsync<SubmissionDto>())!;
        Assert.Equal(SubmissionStatus.Returned, returned.Status);

        // إعادة الإرسال ⇒ يعود لإبراهيم مباشرةً (لا وسيط أحمد).
        var resubmitted = (await (await org.Ahmed.C.PostAsync($"/api/submissions/{submitted.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
        Assert.Equal(SubmissionStatus.Submitted, resubmitted.Status);
        Assert.Equal(org.Ibrahim.Id, resubmitted.CurrentApproverId);
        Assert.DoesNotContain(resubmitted.ApprovalSteps.Where(s => s.Status == ApprovalStatus.Pending),
            s => s.ApproverId == org.Ahmed.Id);
    }

    // ===== P6 — مستخدِم خامس بلا تجاوز يبقى على المسار القديم (المدير أحمد، لا إبراهيم). =====
    [Fact]
    public async Task Report_FifthUser_NoOverride_KeepsOldPath()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Fifth.C, templateId, fieldId, "2026-W20");
        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        // بلا تجاوز: أول معتمِد = مديره المباشر أحمد (المسار الاحتياطي القائم)، وليس إبراهيم.
        Assert.Equal(org.Ahmed.Id, submitted.CurrentApproverId);
        Assert.NotEqual(org.Ibrahim.Id, submitted.CurrentApproverId);
    }

    // ===== P7 — مراجِع KPI للأربعة = إبراهيم (لا قائد فريق، لا مدير وسيط، لا مراجعة ذاتية، لا fallback). =====

    [Fact]
    public async Task Kpi_Ahmed_ReviewerIsIbrahim()
    {
        var org = await BuildOrgAsync();
        var submitted = await SubmitKpiAsync(org.Admin, org.Ahmed.Id, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);
    }

    [Fact]
    public async Task Kpi_Mohsen_ReviewerIsIbrahim_ManagerIdUnchanged()
    {
        var org = await BuildOrgAsync();
        var submitted = await SubmitKpiAsync(org.Admin, org.Mohsen.Id, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);
        Assert.Equal(org.Ahmed.Id, await GetManagerIdAsync(org.Mohsen.Id));
    }

    [Fact]
    public async Task Kpi_Mohamed_ReviewerIsIbrahim()
    {
        var org = await BuildOrgAsync();
        var submitted = await SubmitKpiAsync(org.Admin, org.Mohamed.Id, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);
    }

    [Fact]
    public async Task Kpi_Fatma_ReviewerIsIbrahim()
    {
        var org = await BuildOrgAsync();
        var submitted = await SubmitKpiAsync(org.Admin, org.Fatma.Id, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);
    }

    // ===== P7 — مستخدِم خامس بلا تجاوز يستعمل سلسلة المدير القائمة (لا إبراهيم كتجاوز). =====
    [Fact]
    public async Task Kpi_FifthUser_NoOverride_UsesManagerChain()
    {
        var org = await BuildOrgAsync();
        // المُقيّم = أحمد (مدير الخامس المباشر) ⇒ المراجِع يُحسَب من سلسلة مدير المُقيّم (إبراهيم)،
        // وليس من تجاوز صريح؛ نُثبت أنّ الحلّ لم يمرّ عبر آلية التجاوز (الخامس بلا تجاوز).
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var ev = await (await org.Ahmed.C.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, org.Fifth.Id, PeriodType.Weekly, "2026-W20")))
            .ReadAsync<KpiEvaluationDto>();
        await org.Ahmed.C.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));
        var submitted = (await (await org.Ahmed.C.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;

        // المُراجِع = مدير المُقيّم (إبراهيم) عبر السلسلة القائمة، وليس عبر تجاوز صريح على الخامس.
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);
        Assert.Null(await GetKpiReviewerOverrideAsync(org.Fifth.Id));
    }

    // ===== P7 — Override غير صالح: غير نشط / هو الموضوع / هو المُدخِل ⇒ kpi.reviewer_override_invalid. =====

    [Fact]
    public async Task Kpi_Override_InactiveUser_ConfigError()
    {
        _ = await BuildOrgAsync();
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mgr.UserId);
        var ghost = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetUserActiveAsync(ghost.UserId, false);
        await SetKpiReviewerOverrideAsync(subject.UserId, ghost.UserId);

        var code = await SubmitKpiExpectingErrorAsync(mgr.Client, subject.UserId, "2026-W20");
        Assert.Equal("kpi.reviewer_override_invalid", code);
    }

    [Fact]
    public async Task Kpi_Override_EqualsSubject_ConfigError()
    {
        _ = await BuildOrgAsync();
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mgr.UserId);
        await SetKpiReviewerOverrideAsync(subject.UserId, subject.UserId);

        var code = await SubmitKpiExpectingErrorAsync(mgr.Client, subject.UserId, "2026-W20");
        Assert.Equal("kpi.reviewer_override_invalid", code);
    }

    [Fact]
    public async Task Kpi_Override_EqualsEvaluator_ConfigError()
    {
        _ = await BuildOrgAsync();
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var subject = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mgr.UserId);
        await SetKpiReviewerOverrideAsync(subject.UserId, mgr.UserId); // = المُقيّم

        var code = await SubmitKpiExpectingErrorAsync(mgr.Client, subject.UserId, "2026-W20");
        Assert.Equal("kpi.reviewer_override_invalid", code);
    }

    // ===== أدوات =====

    private static void AssertRoutesDirectlyToIbrahim(SubmissionDto s, Guid ibrahimId, Guid ahmedId)
    {
        Assert.Equal(SubmissionStatus.Submitted, s.Status);
        Assert.Equal(ibrahimId, s.CurrentApproverId);
        // 0 خطوات قبل إبراهيم: كل خطوات الاعتماد تستهدف إبراهيم، ولا وسيط لأحمد.
        Assert.DoesNotContain(s.ApprovalSteps, st => st.ApproverId == ahmedId);
        Assert.All(s.ApprovalSteps, st => Assert.Equal(ibrahimId, st.ApproverId));
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب أداء {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(HttpClient c, Guid templateId, Guid fieldId, string period)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1500m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>())!;
    }

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

    /// <summary>يُنشئ تقييم KPI للموضوع عبر المُقيّم المُعطى، يُدخِل النتائج، ثم يُرسِل ويُعيد الـDTO.</summary>
    private async Task<KpiEvaluationDto> SubmitKpiAsync(HttpClient evaluator, Guid subjectId, string period)
    {
        var (templateId, manualId, autoId) = await PublishKpiAsync(evaluator);
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, period)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));
        return (await (await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;
    }

    /// <summary>يُرسِل تقييم KPI متوقّعًا فشلًا، ويُعيد كود الخطأ (حقل type في ProblemDetails).</summary>
    private async Task<string?> SubmitKpiExpectingErrorAsync(HttpClient evaluator, Guid subjectId, string period)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, period)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));
        var res = await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private async Task SetReportApproverOverrideAsync(Guid userId, Guid? approverId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.ReportApproverOverrideUserId = approverId;
        await db.SaveChangesAsync();
    }

    private async Task SetKpiReviewerOverrideAsync(Guid userId, Guid? reviewerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.KpiReviewerOverrideUserId = reviewerId;
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

    private async Task<Guid?> GetManagerIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.ManagerId).FirstAsync();
    }

    private async Task<Guid?> GetKpiReviewerOverrideAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.KpiReviewerOverrideUserId).FirstAsync();
    }
}

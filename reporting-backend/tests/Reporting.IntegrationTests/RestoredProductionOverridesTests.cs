using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RECONCILE-PROD-DEVELOP-LINEAGE — اختبارات انحدار للميزات الإنتاجيّة المستعادة في المرشَّح الموحّد.
///
/// تغطّي الهجرتين الإنتاجيّتين اللتين لم تكونا في <c>origin/develop</c> ولم يكن لهما أيّ تغطية اختباريّة:
/// <list type="bullet">
/// <item><c>20260724224053_AddReportApproverAndKpiReviewerOverrides</c> —
///       <c>AspNetUsers.ReportApproverOverrideUserId</c> و<c>AspNetUsers.KpiReviewerOverrideUserId</c>
///       (عقد <c>ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1</c> و<c>KPI-REVIEWER-OVERRIDE-R1</c>).</item>
/// <item><c>20260716015239_KpiEvaluationPartialUniqueIndex</c> — فهرس فريد **جزئيّ**
///       على (إصدار القالب، الموظّف، الفترة) مقيَّد بـ<c>"IsDeleted" = false</c>.</item>
/// </list>
/// العَلَم <c>BypassTeamLeaderApproval</c> (هجرة <c>20260715162851</c>) مُغطًّى أصلًا في
/// <see cref="FatmaDirectReportingTests"/> فلا يُكرَّر هنا.
///
/// كلّ التأكيدات على **سلوك خادميّ منشور**، ولم يُعدَّل سطر منتج واحد من أجلها.
/// </summary>
[Collection("Integration")]
public class RestoredProductionOverridesTests
{
    private readonly CustomWebApplicationFactory _factory;

    public RestoredProductionOverridesTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Employee;
        public required HttpClient Admin;
    }

    /// <summary>GM → Manager → TeamLeader، وموظّف مديره المباشر Manager وعضو في فريق قائده TeamLeader.</summary>
    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var employee = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, employee.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Employee = (employee.Client, employee.UserId),
            Admin = admin,
        };
    }

    // ==========================================================================================
    // 1) ReportApproverOverrideUserId — تجاوز معتمِد التقارير الصريح
    // ==========================================================================================

    /// <summary>التجاوز الصريح له الأولوية القصوى: يصير المعتمِد الأوّل مباشرةً بلا خطوة قائد فريق ولا مدير.</summary>
    [Fact]
    public async Task ReportApproverOverride_TakesPriority_OverTeamLeaderAndManager()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        await SetReportApproverOverrideAsync(org.Employee.Id, org.Gm.Id);

        var submitted = await SubmitReportAsync(org.Employee.C, templateId, fieldId, TestCalendar.Cycle(1));

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(org.Gm.Id, submitted.CurrentApproverId);
        Assert.NotEqual(org.Tl.Id, submitted.CurrentApproverId);
        Assert.NotEqual(org.Manager.Id, submitted.CurrentApproverId);
    }

    /// <summary>تجاوز يشير إلى صاحب التقرير نفسه = خطأ إعداد صريح، لا سقوط صامت للمسار القديم.</summary>
    [Fact]
    public async Task ReportApproverOverride_PointingToSelf_IsExplicitConfigurationError()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        await SetReportApproverOverrideAsync(org.Employee.Id, org.Employee.Id);

        var (res, _) = await TrySubmitReportAsync(org.Employee.C, templateId, fieldId, TestCalendar.Cycle(1));

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("approval.override_invalid", await ErrorCodeAsync(res));
    }

    /// <summary>تجاوز يشير إلى مستخدم غير نشط = خطأ إعداد صريح أيضًا (لا يُتجاهَل بصمت).</summary>
    [Fact]
    public async Task ReportApproverOverride_PointingToInactiveUser_IsExplicitConfigurationError()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        await SetReportApproverOverrideAsync(org.Employee.Id, org.Gm.Id);
        await SetUserActiveAsync(org.Gm.Id, false);

        var (res, _) = await TrySubmitReportAsync(org.Employee.C, templateId, fieldId, TestCalendar.Cycle(1));

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("approval.override_invalid", await ErrorCodeAsync(res));
    }

    /// <summary>بلا تجاوز: السلسلة الاحتياطيّة القائمة تعمل كما هي — أوّل معتمِد هو قائد الفريق.</summary>
    [Fact]
    public async Task NoReportApproverOverride_FallbackChain_Unchanged()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Employee.C, templateId, fieldId, TestCalendar.Cycle(1));

        Assert.Equal(org.Tl.Id, submitted.CurrentApproverId);
    }

    // ==========================================================================================
    // 2) KpiReviewerOverrideUserId — تجاوز مُراجِع KPI الصريح
    // ==========================================================================================

    /// <summary>التجاوز الصريح يوجّه المراجعة إليه بدل سلسلة مدير المُقيّم.</summary>
    [Fact]
    public async Task KpiReviewerOverride_RoutesReview_ToExplicitReviewer()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);

        // بلا تجاوز يكون المُراجِع = قائد فريق الموضوع (سلسلة اعتماد الموضوع)؛
        // نضبط التجاوز على المدير العامّ ليتمايز الأثر تمايزًا قاطعًا.
        await SetKpiReviewerOverrideAsync(org.Employee.Id, org.Gm.Id);

        var submitted = await EvaluateAndSubmitAsync(
            org.Manager.C, templateId, org.Employee.Id, manualId, autoId, TestCalendar.Cycle(1));

        Assert.Equal(org.Gm.Id, submitted.ReviewerId);
        Assert.NotEqual(org.Tl.Id, submitted.ReviewerId);
        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
    }

    /// <summary>KPI-REVIEWER-OVERRIDE-R1: حين يكون التجاوز هو المُدخِل نفسه ⇒ اعتماد مباشر عند الإرسال.</summary>
    [Fact]
    public async Task KpiReviewerOverride_IsEvaluator_ApprovesDirectlyOnSubmit()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);

        await SetKpiReviewerOverrideAsync(org.Employee.Id, org.Manager.Id);

        var submitted = await EvaluateAndSubmitAsync(
            org.Manager.C, templateId, org.Employee.Id, manualId, autoId, TestCalendar.Cycle(1));

        Assert.Equal(KpiEvaluationStatus.Approved, submitted.Status);
        Assert.Equal(org.Manager.Id, submitted.ReviewerId);
    }

    /// <summary>تجاوز يشير إلى الموضوع نفسه ⇒ خطأ إعداد صريح، لا سقوط صامت إلى ManagerId.</summary>
    [Fact]
    public async Task KpiReviewerOverride_PointingToSubject_IsExplicitConfigurationError()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);

        await SetKpiReviewerOverrideAsync(org.Employee.Id, org.Employee.Id);

        var res = await TryEvaluateAndSubmitAsync(
            org.Manager.C, templateId, org.Employee.Id, manualId, autoId, TestCalendar.Cycle(1));

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("kpi.reviewer_override_invalid", await ErrorCodeAsync(res));
    }

    /// <summary>
    /// بلا تجاوز: سلسلة المُراجِع القائمة تعمل كما هي — الخطوة الأولى في <c>ResolveReviewerAsync</c>
    /// هي سلسلة اعتماد **الموضوع** (قائد فريقه ما لم يكن Bypass) ⇒ قائد الفريق، لا مدير المُقيّم.
    /// </summary>
    [Fact]
    public async Task NoKpiReviewerOverride_FallbackChain_Unchanged()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);

        var submitted = await EvaluateAndSubmitAsync(
            org.Manager.C, templateId, org.Employee.Id, manualId, autoId, TestCalendar.Cycle(1));

        Assert.Equal(org.Tl.Id, submitted.ReviewerId);
        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
    }

    /// <summary>التجاوز يوسّع نطاق الإدخال: من عُيِّن مُراجِعًا صريحًا يرى الموظّف ضمن «القابلين للتقييم».</summary>
    [Fact]
    public async Task KpiReviewerOverride_GrantsEvaluationScope_ToExplicitReviewer()
    {
        var org = await BuildOrgAsync();

        var beforeRes = await org.Tl.C.GetAsync("/api/kpi-evaluations/evaluatable-subjects");
        var before = await beforeRes.ReadAsync<EvaluatableSubjectsDto>();
        Assert.DoesNotContain(before!.Subjects, s => s.Id == org.Employee.Id);

        await SetKpiReviewerOverrideAsync(org.Employee.Id, org.Tl.Id);

        var afterRes = await org.Tl.C.GetAsync("/api/kpi-evaluations/evaluatable-subjects");
        var after = await afterRes.ReadAsync<EvaluatableSubjectsDto>();
        Assert.Contains(after!.Subjects, s => s.Id == org.Employee.Id);
    }

    // ==========================================================================================
    // 3) الفهرس الفريد الجزئيّ على kpi_evaluations
    // ==========================================================================================

    /// <summary>لا تقييمان نشطان لنفس (إصدار القالب، الموظّف، الفترة) — يمنعه الفهرس على مستوى القاعدة.</summary>
    [Fact]
    public async Task KpiEvaluation_DuplicateActiveRow_IsRejectedByPartialUniqueIndex()
    {
        var org = await BuildOrgAsync();
        var (templateId, _, _) = await PublishKpiAsync(org.Admin);
        var versionId = await PublishedVersionIdAsync(templateId);
        var period = TestCalendar.Cycle(1);

        await InsertEvaluationAsync(versionId, org.Employee.Id, period, isDeleted: false);

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => InsertEvaluationAsync(versionId, org.Employee.Id, period, isDeleted: false));
    }

    /// <summary>الفهرس **جزئيّ**: الصفّ المحذوف منطقيًّا لا يحجز المفتاح ولا يمنع إنشاء بديل نشط.</summary>
    [Fact]
    public async Task KpiEvaluation_SoftDeletedRow_DoesNotBlockNewActiveRow()
    {
        var org = await BuildOrgAsync();
        var (templateId, _, _) = await PublishKpiAsync(org.Admin);
        var versionId = await PublishedVersionIdAsync(templateId);
        var period = TestCalendar.Cycle(1);

        await InsertEvaluationAsync(versionId, org.Employee.Id, period, isDeleted: true);

        // لا استثناء: الصفّ المحذوف خارج نطاق الفهرس ("IsDeleted" = false).
        var id = await InsertEvaluationAsync(versionId, org.Employee.Id, period, isDeleted: false);
        Assert.NotEqual(Guid.Empty, id);
    }

    // ==========================================================================================
    // أدوات
    // ==========================================================================================

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

    private async Task<Guid> PublishedVersionIdAsync(Guid kpiTemplateId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.KpiTemplateVersions
            .Where(v => v.KpiTemplateId == kpiTemplateId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.Id)
            .FirstAsync();
    }

    private async Task<Guid> InsertEvaluationAsync(Guid versionId, Guid subjectId, string periodKey, bool isDeleted)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = new KpiEvaluation
        {
            KpiTemplateVersionId = versionId,
            SubjectUserId = subjectId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = periodKey,
            Status = KpiEvaluationStatus.Draft,
            Trend = KpiTrend.Unknown,
            IsDeleted = isDeleted,
        };
        db.KpiEvaluations.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب تجاوز {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null)).StatusCode);
        return (created.Id, field!.Id);
    }

    private static async Task<(HttpResponseMessage Res, Guid DraftId)> TrySubmitReportAsync(
        HttpClient c, Guid templateId, Guid fieldId, string period)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1500m, null, null, null) }));
        return (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null), draft.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient c, Guid templateId, Guid fieldId, string period)
    {
        var (res, _) = await TrySubmitReportAsync(c, templateId, fieldId, period);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات تجاوز {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null)).StatusCode);
        return (created.Id, manual!.Id, auto!.Id);
    }

    private static async Task<HttpResponseMessage> TryEvaluateAndSubmitAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string period)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, period)))
            .ReadAsync<KpiEvaluationDto>();

        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));

        return await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
    }

    private static async Task<KpiEvaluationDto> EvaluateAndSubmitAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string period)
    {
        var res = await TryEvaluateAndSubmitAsync(evaluator, templateId, subjectId, manualId, autoId, period);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<KpiEvaluationDto>())!;
    }
}

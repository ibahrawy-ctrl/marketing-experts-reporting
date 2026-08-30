using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// حوكمة التصحيح الإداريّ للتقارير وتقييمات KPI (ADMIN-GOVERNANCE-R1).
/// مساران: (1) الحذف الإداريّ الناعم للتقارير/التقييمات محصور بـ Admin/CEO/GM فقط (soft-delete + تدقيق)؛
/// (2) مسار مراجعة/اعتماد KPI (Draft→Submitted→UnderReview→Approved|NeedsRevision|Rejected) قبل أن تُصبح الأرقام نهائيّة.
/// كل الحالات قائمة على الدور والنطاق خادميًّا: المُقيّم/الموضوع لا يعتمدان، المراجع المُصعَّد يقرّر،
/// والمحذوف يُستبعَد من كل التجميعات والتصدير. HR يشير/يعلّق/يطلب إعادة الفتح لكنه لا يعتمد/يرفض/يحذف/يعيد الفتح.
/// </summary>
[Collection("Integration")]
public class AdminGovernanceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminGovernanceTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===================== مساعدو KPI =====================

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(
        HttpClient admin, KpiCadence cadence = KpiCadence.WeeklyPulse)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"حوكمة {Guid.NewGuid():N}", null, null, cadence)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return (created.Id, manual!.Id, auto!.Id);
    }

    /// <summary>
    /// ينشئ تقييمًا أسبوعيًّا، يحفظ نتائجه، ثم يُرسله (⇒ UnderReview) ويعيد الـDTO المُرسَل.
    /// كلّ استدعاء مؤكَّد فورًا برمز الحالة ونصّ خطأ الخادم (BASELINE-DEFECT-01): <c>ReadAsync</c>
    /// يفكّ ترميز الجسم أيًّا كان رمز الحالة، فإخفاق <c>/submit</c> كان يُنتِج DTO افتراضيًّا بـ
    /// <c>Guid.Empty</c> يظهر لاحقًا على هيئة 404 مضلِّل يُبلّغ العرَض لا العلّة.
    /// </summary>
    private static async Task<KpiEvaluationDto> SubmitEvalAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string weekKey, decimal score = 70m,
        PeriodType periodType = PeriodType.Weekly)
    {
        var createRes = await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, periodType, weekKey));
        await EnsureSuccessAsync(createRes, "إنشاء تقييم KPI");
        var ev = await createRes.ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(ev);
        Assert.NotEqual(Guid.Empty, ev!.Id);

        var resultsRes = await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, score, null),
                new KpiResultInput(autoId, score, null, null)
            }));
        await EnsureSuccessAsync(resultsRes, "حفظ نتائج تقييم KPI");

        var submitRes = await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        await EnsureSuccessAsync(submitRes, "إرسال تقييم KPI");
        var submitted = await submitRes.ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(submitted);
        Assert.NotEqual(Guid.Empty, submitted!.Id);
        Assert.NotNull(submitted.ReviewerId);
        return submitted;
    }

    /// <summary>يُفشِل الاختبار فورًا برمز الخادم وجسمه عند أوّل استدعاء غير ناجح — لا تمرير DTO افتراضيّ.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string step)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"فشل «{step}»: {(int)response.StatusCode} {response.StatusCode} — {body}");
    }

    // ===================== مساعدو التقارير =====================

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishReportTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب حوكمة {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient employee, Guid templateId, Guid fieldId, string weekKey, decimal value = 100m)
    {
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, weekKey)))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, value, null, null, null) }));
        return (await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    // ========================================================================
    // المسار (2): مراجعة/اعتماد KPI
    // ========================================================================

    // §الإرسال يقود مباشرةً إلى UnderReview مع إسناد مُراجِع آليًّا (تصحيح #5).
    [Fact]
    public async Task Submit_MovesTo_UnderReview_And_AssignsReviewer()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W20");

        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.NotNull(submitted.ReviewerId);
    }

    // §Phase 6 (ROLE-AWARE-PERSONAL-REPORT-R1): مراجعة KPI تُوجَّه إلى المدير المباشر للموضوع
    // (سلسلة اعتماد الموضوع مُوجَّهة بالبيانات) لا إلى سلسلة مدير المُدخِل. المُدخِل هنا Admin (نطاق
    // كامل) وليس مدير الموضوع؛ ومع ذلك يُشتقّ المراجع من ManagerId للموضوع مباشرةً.
    [Fact]
    public async Task KpiReviewer_RoutesTo_SubjectDirectManager_NotEvaluatorChain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (_, designatedManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", designatedManagerId);

        var submitted = await SubmitEvalAsync(admin, templateId, subjectId, manualId, autoId, "2026-W17");

        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.Equal(designatedManagerId, submitted.ReviewerId);
    }

    // §Phase 6: عند وجود فريق للموضوع بقائد نشط (بلا Bypass) تُوجَّه مراجعة KPI إلى قائد فريق الموضوع.
    [Fact]
    public async Task KpiReviewer_RoutesTo_SubjectTeamLeader_WhenNotBypassed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (_, teamLeaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await TestAuth.CreateTeamWithLeaderAsync(_factory, teamLeaderId, subjectId);

        var submitted = await SubmitEvalAsync(admin, templateId, subjectId, manualId, autoId, "2026-W18");

        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.Equal(teamLeaderId, submitted.ReviewerId);
    }

    // §Phase 6: موظّف Direct Reporting (BypassTeamLeaderApproval=true) يتخطّى قائد الفريق فتُوجَّه
    // مراجعة KPI إلى مديره المباشر مباشرةً — نفس آليّة توجيه اعتماد تقارير فاطمة إلى إبراهيم.
    [Fact]
    public async Task KpiReviewer_SubjectBypass_SkipsTeamLeader_UsesDirectManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (_, teamLeaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, designatedManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", designatedManagerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, teamLeaderId, subjectId);
        await TestAuth.SetBypassTeamLeaderApprovalAsync(_factory, subjectId, true);

        var submitted = await SubmitEvalAsync(admin, templateId, subjectId, manualId, autoId, "2026-W19");

        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.Equal(designatedManagerId, submitted.ReviewerId);
        Assert.NotEqual(teamLeaderId, submitted.ReviewerId);
    }

    // §المُقيّم لا يعتمد تقييمه (استبعاد المُقيّم يطغى على الترقّي).
    [Fact]
    public async Task Evaluator_Cannot_Approve_Own_Evaluation_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W21");

        // المُقيّم (المدير) هو من أنشأ التقييم ⇒ لا يعتمده.
        var denied = await manager.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // §الموضوع لا يعتمد تقييمه (وليس ضمن صلاحية الإدارة أصلًا).
    [Fact]
    public async Task Subject_Cannot_Approve_Own_Evaluation_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W22");

        var denied = await subject.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // §مُصعَّد مؤهّل (Admin) يعتمد ⇒ Approved.
    [Fact]
    public async Task Elevated_Reviewer_Approves_ToApproved()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W23");

        var approved = await (await admin.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
    }

    // §طلب تعديل ⇒ NeedsRevision (بسبب إلزاميّ).
    [Fact]
    public async Task RequestRevision_MovesTo_NeedsRevision()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W24");

        var revised = await (await admin.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/request-revision",
            new KpiReviewActionRequest("يرجى تصحيح مؤشر الالتزام"))).ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.NeedsRevision, revised!.Status);
    }

    // §رفض نهائيّ ⇒ Rejected؛ وبلا سبب ⇒ 400.
    [Fact]
    public async Task Reject_RequiresReason_Then_MovesTo_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W25");

        var noReason = await admin.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/reject",
            new KpiReviewActionRequest(null));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var rejected = await (await admin.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/reject",
            new KpiReviewActionRequest("بيانات غير مكتملة"))).ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Rejected, rejected!.Status);
    }

    // §إعادة الفتح من Admin (Approved ⇒ UnderReview) بصلاحية AdminKpiGovernance فقط.
    [Fact]
    public async Task Admin_Reopens_Approved_To_UnderReview()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W26");
        var approved = await (await admin.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);

        var reopened = await (await admin.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/reopen",
            new KpiReviewActionRequest("مراجعة إضافيّة مطلوبة"))).ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.UnderReview, reopened!.Status);
    }

    // §الحذف الإداريّ للتقييم محصور بـ Admin/CEO/GM؛ غيرهم ⇒ 403.
    [Fact]
    public async Task KpiAdminDelete_RestrictedToElevated_OthersForbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subject, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        // إغلاق فجوة التوثيق: CeoSupport (دعم الرئيس التنفيذي) ومدير الحساب (AccountPortfolioReader) ممنوعان صراحةً.
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var (accountManager, _) = await TestAuth.CreateUserAsync(_factory, "AccountPortfolioReader");

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W27");

        foreach (var forbidden in new[] { manager, subject, tl, hr, ceoSupport, accountManager })
        {
            var res = await forbidden.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/admin-delete",
                new KpiReviewActionRequest("محاولة"));
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        var deleted = await admin.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/admin-delete",
            new KpiReviewActionRequest("حذف إداريّ للاختبار"));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    // §التقييم المحذوف إداريًّا يُستبعَد من التجميع الدوريّ ومن تصدير المالية (تصحيح #7/#8).
    [Fact]
    public async Task Deleted_Kpi_ExcludedFrom_Aggregate_And_FinanceExport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W20", 80m);
        var approved = await (await admin.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);

        // التصدير المالي مصدره المسار الربعيّ الرسميّ وحده (DEC-01 §5) ⇒ نقيسه على تقييم ربعيّ لنفس الموظّف.
        var (qTemplate, qManual, qAuto) = await PublishKpiAsync(admin, KpiCadence.Quarterly);
        var qSubmitted = await SubmitEvalAsync(manager, qTemplate, subjectId, qManual, qAuto, "2026-Q2", 80m,
            PeriodType.Quarterly);
        var qApproved = await (await admin.PostAsync($"/api/kpi-evaluations/{qSubmitted.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, qApproved!.Status);

        // قبل الحذف: التجميع يعكس الدرجة، والتصدير يحوي التقييم الربعيّ.
        var beforeAgg = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-05&subjectUserId={subjectId}"))
            .ReadAsync<KpiAggregateDto>();
        Assert.Equal(1, beforeAgg!.WeeksCount);
        Assert.Equal(80m, beforeAgg.Average);

        var beforeExport = await (await admin.GetAsync("/api/kpi-evaluations/finance-export?year=2026&quarter=2"))
            .ReadAsync<KpiFinanceExportDto>();
        Assert.Contains(beforeExport!.Rows, r => r.EvaluationId == qSubmitted.Id);

        // حذف إداريّ للتقييمَين معًا.
        foreach (var id in new[] { submitted.Id, qSubmitted.Id })
        {
            var del = await admin.PostAsJsonAsync($"/api/kpi-evaluations/{id}/admin-delete",
                new KpiReviewActionRequest("استبعاد من التجميع"));
            Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        }

        // بعد الحذف: التجميع يسقط، والتصدير لا يحوي التقييم.
        var afterAgg = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-05&subjectUserId={subjectId}"))
            .ReadAsync<KpiAggregateDto>();
        Assert.Equal(0, afterAgg!.WeeksCount);
        Assert.Null(afterAgg.Average);

        var afterExport = await (await admin.GetAsync("/api/kpi-evaluations/finance-export?year=2026&quarter=2"))
            .ReadAsync<KpiFinanceExportDto>();
        Assert.DoesNotContain(afterExport!.Rows, r => r.EvaluationId == qSubmitted.Id);
    }

    // §سجلّ أحداث المراجعة (Timeline) يُملأ بأحداث الإرسال والاعتماد.
    [Fact]
    public async Task ReviewEvents_Timeline_Is_Populated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitEvalAsync(manager, templateId, subjectId, manualId, autoId, "2026-W28");
        await admin.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null);

        var events = await (await admin.GetAsync($"/api/kpi-evaluations/{submitted.Id}/review-events"))
            .ReadAsync<List<KpiEvaluationReviewEventDto>>();
        Assert.NotNull(events);
        Assert.Contains(events!, e => e.Action == "Submitted");
        Assert.Contains(events!, e => e.Action == "Approved");
    }

    // §HR يشير/يعلّق/يطلب إعادة الفتح، لكنه لا يعتمد/يرفض/يعيد الفتح/يحذف (rev #7).
    [Fact]
    public async Task Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        // نطاق HR = «own» فقط (لا يتوسّع عبر ManagerId)، لذا يمرّ التعليق (المشروط بإمكانيّة العرض)
        // حين يكون HR هو موضوع التقييم. أمّا الإشارة/طلب إعادة الفتح فبالدور فقط (KpiReviewFlaggers) بلا اشتراط عرض.
        // مُراجِع صريح للموضوع: على قاعدة فارغة لا يوجد مسؤول أعلى غير الأدمن نفسه، وهو المُقيّم
        // فيُستبعَد ⇒ ResolveReviewerAsync يعيد null ⇒ kpi_eval.no_reviewer.conflict ويسقط /submit.
        // إسناد ManagerId للموضوع يجعل الإسناد حتميًّا من الخطوة الأولى (مدير الموضوع المباشر)
        // على قاعدة فارغة أو مأهولة سواء، فلا يعتمد الاختبار على ترتيب تنفيذ ولا على بيانات سابقة.
        var (_, hrManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (hr, hrId) = await TestAuth.CreateUserAsync(_factory, "HR", hrManagerId);

        // الأدمن هو المُقيّم (مُصعَّد يستطيع تقييم أيّ موظّف) وموضوعه HR، والإرسال يُسنِد مُراجِعًا آليًّا.
        var submitted = await SubmitEvalAsync(admin, templateId, hrId, manualId, autoId, "2026-W29");
        Assert.Equal(KpiEvaluationStatus.UnderReview, submitted.Status);
        Assert.Equal(hrManagerId, submitted.ReviewerId);

        // مسموح لـ HR:
        var flag = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/flag",
            new KpiReviewActionRequest("إشارة HR"));
        Assert.Equal(HttpStatusCode.OK, flag.StatusCode);

        var comment = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/comment",
            new KpiReviewActionRequest("تعليق HR"));
        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);

        var requestReopen = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/request-reopen",
            new KpiReviewActionRequest("طلب HR لإعادة الفتح"));
        Assert.Equal(HttpStatusCode.OK, requestReopen.StatusCode);

        // ممنوع على HR:
        var approve = await hr.PostAsync($"/api/kpi-evaluations/{submitted.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        var reject = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/reject",
            new KpiReviewActionRequest("رفض"));
        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);

        var reopen = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/reopen",
            new KpiReviewActionRequest("إعادة فتح"));
        Assert.Equal(HttpStatusCode.Forbidden, reopen.StatusCode);

        var adminDelete = await hr.PostAsJsonAsync($"/api/kpi-evaluations/{submitted.Id}/admin-delete",
            new KpiReviewActionRequest("حذف"));
        Assert.Equal(HttpStatusCode.Forbidden, adminDelete.StatusCode);
    }

    // ========================================================================
    // المسار (1): الحذف الإداريّ الناعم للتقارير
    // ========================================================================

    // §حذف التقرير الإداريّ محصور بـ Admin/CEO/GM؛ غيرهم ⇒ 403، والأدمن ⇒ يختفي التقرير.
    [Fact]
    public async Task ReportAdminDelete_RestrictedToElevated_OthersForbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        // إغلاق فجوة التوثيق: CeoSupport (دعم الرئيس التنفيذي) ومدير الحساب (AccountPortfolioReader) ممنوعان صراحةً.
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var (accountManager, _) = await TestAuth.CreateUserAsync(_factory, "AccountPortfolioReader");

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W20");

        foreach (var forbidden in new[] { employee, manager, tl, hr, ceoSupport, accountManager })
        {
            var res = await forbidden.PostAsJsonAsync($"/api/submissions/{submitted.Id}/admin-delete",
                new AdminDeleteRequest("محاولة"));
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        var deleted = await admin.PostAsJsonAsync($"/api/submissions/{submitted.Id}/admin-delete",
            new AdminDeleteRequest("حذف إداريّ للاختبار"));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        // التقرير المحذوف ناعمًا لم يعد يظهر (Global Query Filter).
        var get = await admin.GetAsync($"/api/submissions/{submitted.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // §حذف التقرير الإداريّ يحوّل خطوات الاعتماد المعلّقة إلى CancelledByAdministrativeDeletion
    //   ويُصفّر المعتمِد الحاليّ ويستبعده من «الاعتمادات المعلّقة» (rev #8).
    [Fact]
    public async Task ReportAdminDelete_Converts_PendingSteps_And_ExcludesFromPendingApprovals()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager", gmId);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W21");
        Assert.Equal(managerId, submitted.CurrentApproverId);

        // قبل الحذف: التقرير ضمن اعتمادات المدير المعلّقة.
        var pendingBefore = await (await manager.GetAsync("/api/submissions/pending-approvals"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.Contains(pendingBefore!, s => s.Id == submitted.Id);

        var deleted = await admin.PostAsJsonAsync($"/api/submissions/{submitted.Id}/admin-delete",
            new AdminDeleteRequest("إلغاء إداريّ"));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        // بعد الحذف: لم يعد ضمن اعتمادات المدير المعلّقة.
        var pendingAfter = await (await manager.GetAsync("/api/submissions/pending-approvals"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.DoesNotContain(pendingAfter!, s => s.Id == submitted.Id);
    }

    // §الحذف الإداريّ للتقرير يتطلّب سببًا إلزاميًّا.
    [Fact]
    public async Task ReportAdminDelete_RequiresReason_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W22");

        var noReason = await admin.PostAsJsonAsync($"/api/submissions/{submitted.Id}/admin-delete",
            new AdminDeleteRequest(null));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    // §المستخدم غير المصادَق يُرفض على مسارات الحوكمة.
    [Fact]
    public async Task Anonymous_Is_Unauthorized_On_Governance_Routes()
    {
        var anon = _factory.CreateClient();
        var kpi = await anon.PostAsJsonAsync($"/api/kpi-evaluations/{Guid.NewGuid()}/admin-delete",
            new KpiReviewActionRequest("x"));
        Assert.Equal(HttpStatusCode.Unauthorized, kpi.StatusCode);

        var report = await anon.PostAsJsonAsync($"/api/submissions/{Guid.NewGuid()}/admin-delete",
            new AdminDeleteRequest("x"));
        Assert.Equal(HttpStatusCode.Unauthorized, report.StatusCode);
    }
}

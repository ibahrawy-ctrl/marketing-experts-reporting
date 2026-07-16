using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Kpi;
using Reporting.Application.Leave;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// FATMA-DIRECT-REPORTING-OVERRIDE-R1 — قاعدة عامة «التبعية المباشرة للمدير» (BypassTeamLeaderApproval).
/// موظّف مضبوط على العلم يبقى تشغيليًّا ضمن فريقٍ له قائد (TeamId وقائد الفريق دون تغيير) لكن مسار
/// الاعتماد (إجازة/استئذان/تقرير) يتخطّى خطوة قائد الفريق ويبدأ من المدير المباشر النشط ثم الاحتياطي.
/// موظّف العلم=false يسلك المسار الحالي دون تغيير. مسار KPI (سلسلة المدير) غير متأثّر إطلاقًا.
/// </summary>
[Collection("Integration")]
public class FatmaDirectReportingTests
{
    private readonly CustomWebApplicationFactory _factory;

    public FatmaDirectReportingTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;     // المدير المباشر (إبراهيم)
        public required (HttpClient C, Guid Id) Tl;          // قائد فريق الموظّف الفعليّ (أحمد)
        public required (HttpClient C, Guid Id) Normal;      // موظّف عادي (bypass=false)
        public required (HttpClient C, Guid Id) Direct;      // موظّف تبعية مباشرة (bypass=true) = فاطمة
        public required HttpClient Admin;
        public required Guid TeamId;
    }

    /// <summary>
    /// يبني هيكلًا: GM → Manager(إبراهيم) → TeamLeader(أحمد) وموظّفان (عادي + تبعية مباشرة) مديرهما
    /// المباشر إبراهيم وكلاهما عضوان في فريق أحمد. الموظّف «Direct» يُضبط bypass=true بيانيًّا (لا API).
    /// </summary>
    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        // كلا الموظّفين مديرهما المباشر هو Manager (إبراهيم) — كما بقاء ManagerId لفاطمة على إبراهيم.
        var normal = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var direct = await TestAuth.CreateUserAsync(_factory, Roles.Employee, manager.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // فريق فعليّ قائده أحمد يضمّ الموظّفَين (كلاهما TeamId=هذا الفريق) — لا يتغيّر انتماؤهما التشغيليّ.
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, normal.UserId, direct.UserId);

        // العلم العام: الموظّف «Direct» تبعية مباشرة (كما يُضبط لفاطمة على الإنتاج عبر SQL محكوم).
        await TestAuth.SetBypassTeamLeaderApprovalAsync(_factory, direct.UserId, true);

        foreach (var id in new[] { gm.UserId, manager.UserId, tl.UserId, normal.UserId, direct.UserId })
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Normal = (normal.Client, normal.UserId),
            Direct = (direct.Client, direct.UserId),
            Admin = admin,
            TeamId = teamId,
        };
    }

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, DateOnly start, DateOnly end)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, "سبب الإجازة", null),
            TestJson.Options);

    private static async Task<LeaveRequestDto> OkAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<LeaveRequestDto>())!;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> TeamLeaderApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> ManagerApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> HrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

    private const string DirectSkipText =
        "تم تخطي مراجعة قائد الفريق (تبعية مباشرة للمدير)، وتم توجيه الطلب إلى المدير المباشر.";

    // ===== 1) موظّف عادي (bypass=false) داخل فريق له قائد ⇒ يبدأ عند خطوة قائد الفريق (المسار الحالي). =====
    [Fact]
    public async Task Leave_NormalEmployee_StartsAtTeamLeaderStep()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Normal.C, new(2026, 1, 5), new(2026, 1, 7)));

        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
        Assert.DoesNotContain(created.Timeline, e => e.Action == "team_leader_step_skipped");

        // قائد الفريق الفعليّ (أحمد) يعتمد خطوته ⇒ ينتقل للمدير (سلوك T-WF2 المحفوظ).
        var approved = await OkAsync(await TeamLeaderApproveAsync(org.Tl.C, created.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, approved.Status);
        Assert.Equal(LeaveRequestStep.Manager, approved.CurrentStep);
        Assert.Equal(org.Tl.Id, approved.TeamLeaderReviewerId);
    }

    // ===== 2) موظّف تبعية مباشرة (bypass=true) ⇒ يُتخطّى قائد الفريق ويبدأ عند المدير؛ المدير يعتمد=200
    //          بلا 403؛ حدث تخطٍّ خاصّ بالتبعية المباشرة؛ لا اعتماد بشريّ لقائد الفريق. =====
    [Fact]
    public async Task Leave_DirectReporting_SkipsTeamLeader_ManagerApproves()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Direct.C, new(2026, 2, 5), new(2026, 2, 7)));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Null(created.TeamLeaderReviewerId); // تخطٍّ آليّ لا اعتماد بشريّ
        var skip = Assert.Single(created.Timeline, e => e.Action == "team_leader_step_skipped");
        Assert.Equal(DirectSkipText, skip.Comment);

        // المدير المباشر (إبراهيم) يعتمد خطوة المدير ⇒ 200 (لا 403).
        var mgr = await OkAsync(await ManagerApproveAsync(org.Manager.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, mgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, mgr.CurrentStep);

        // المسار يكمل: الاعتماد النهائي.
        Assert.Equal(LeaveRequestStatus.HrApproved, (await OkAsync(await HrApproveAsync(org.Gm.C, created.Id))).Status);
    }

    // ===== 3) Fallback: تبعية مباشرة والمدير المباشر غير نشط ⇒ الطلب عند خطوة المدير، والمدير العام
    //          (نطاقه يشمل الموظّف) يعتمدها بلا قائد فريق (احتياطي GM). =====
    [Fact]
    public async Task Leave_DirectReporting_InactiveManager_GmApprovesManagerStep()
    {
        var org = await BuildOrgAsync();
        // تعطيل المدير المباشر بعد الإنشاء.
        await SetUserActiveAsync(org.Manager.Id, false);

        var created = await OkAsync(await CreateLeaveAsync(org.Direct.C, new(2026, 3, 5), new(2026, 3, 7)));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);

        // المدير العام (نطاقه يشمل الموظّف عبر شجرة الإدارة) يعتمد خطوة المدير.
        var gm = await OkAsync(await ManagerApproveAsync(org.Gm.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, gm.Status);
        Assert.Equal(LeaveRequestStep.Hr, gm.CurrentStep);
    }

    // ===== 4) تقرير الموظّف العادي ⇒ أول معتمِد هو قائد الفريق الفعليّ (أحمد). =====
    [Fact]
    public async Task Report_NormalEmployee_FirstApprover_IsTeamLeader()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Normal.C, templateId, fieldId, "2026-W20");
        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(org.Tl.Id, submitted.CurrentApproverId); // قائد الفريق أحمد
    }

    // ===== 5) تقرير موظّف التبعية المباشرة ⇒ يُتجاهَل قائد الفريق ويكون أول معتمِد هو المدير (إبراهيم). =====
    [Fact]
    public async Task Report_DirectReporting_FirstApprover_IsManager_NotTeamLeader()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Direct.C, templateId, fieldId, "2026-W21");
        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(org.Manager.Id, submitted.CurrentApproverId); // المدير إبراهيم لا قائد الفريق
        Assert.NotEqual(org.Tl.Id, submitted.CurrentApproverId);
    }

    // ===== 6) KPI بلا انحدار: المُراجِع يُحسَب من سلسلة المدير (المُقيّم)، لا من قائد الفريق ولا من العلم. =====
    [Fact]
    public async Task Kpi_ReviewerStaysManagerChain_UnaffectedByBypass()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);

        // المدير المباشر (إبراهيم) يُقيّم موظّف التبعية المباشرة (مرؤوسه المباشر).
        var ev = await (await org.Manager.C.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, org.Direct.Id, PeriodType.Weekly, "2026-W22")))
            .ReadAsync<KpiEvaluationDto>();

        // إدخال النتائج ثم الإرسال — المُراجِع يُسنَد عند الإرسال.
        await org.Manager.C.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 80m, null, null),
            }));
        var submitted = await (await org.Manager.C.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();

        // المُراجِع = مدير المُقيّم في السلسلة (المدير العام) — ليس قائد الفريق، وغير متأثّر بعلم التبعية.
        Assert.Equal(org.Gm.Id, submitted!.ReviewerId);
        Assert.NotEqual(org.Tl.Id, submitted.ReviewerId);
    }

    // ===== 7) الافتراضي: مستخدم جديد علمه=false ⇒ يسلك المسار العادي (خطوة قائد الفريق موجودة). =====
    [Fact]
    public async Task NewUser_DefaultsToFalse_NormalRouting()
    {
        var org = await BuildOrgAsync();
        // الموظّف العادي أُنشئ حديثًا بلا ضبط للعلم ⇒ الافتراضي false ⇒ يبدأ عند قائد الفريق.
        var created = await OkAsync(await CreateLeaveAsync(org.Normal.C, new(2026, 4, 10), new(2026, 4, 12)));
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
    }

    // ===== 8) الاستئذان لموظّف تبعية مباشرة ⇒ يُتخطّى قائد الفريق أيضًا (العلم عامّ لكل مسار الاعتماد). =====
    [Fact]
    public async Task Permission_DirectReporting_SkipsTeamLeader()
    {
        var org = await BuildOrgAsync();
        // سنة بلا سياسة حدّ شهريّ متراكمة ⇒ الاستئذان غير محدود ولا يتدخّل حارس الحدّ.
        var res = await org.Direct.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, new(2091, 5, 4), null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان تبعية مباشرة", null), TestJson.Options);
        var created = await OkAsync(res);

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Null(created.TeamLeaderReviewerId);
        var skip = Assert.Single(created.Timeline, e => e.Action == "team_leader_step_skipped");
        Assert.Equal(DirectSkipText, skip.Comment);
    }

    // ===== 9) صون T-WF2: لموظّف عادي، المدير العام لا يعتمد خطوة قائد الفريق (403) — لم يُضعِف الإصلاحُ
    //          توجيهَ قائد الفريق الدقيق؛ القائد الفعليّ وحده يعتمدها. =====
    [Fact]
    public async Task Leave_NormalEmployee_GmForbiddenOnTeamLeaderStep_Twf2Preserved()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Normal.C, new(2026, 6, 5), new(2026, 6, 7)));
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);

        // المدير العام (نطاقه يشمل الموظّف) يحاول اعتماد خطوة قائد الفريق ⇒ 403 auth.forbidden.
        var forbidden = await TeamLeaderApproveAsync(org.Gm.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(forbidden));

        // القائد الفعليّ (أحمد) وحده يعتمدها ⇒ 200.
        var ok = await OkAsync(await TeamLeaderApproveAsync(org.Tl.C, created.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, ok.Status);
    }

    // ===== أدوات التقارير وKPI =====

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

    private async Task SetUserActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsActive = active;
        await db.SaveChangesAsync();
    }
}

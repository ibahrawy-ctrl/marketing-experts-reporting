using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Projects360;
using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// **جسر التنفيذ الهجين** (P360-WF-R2 §10 · OPTION B) — عبر HTTP حقيقيّ.
///
/// <para>
/// الجسر يحلّ مشكلة واحدة: من يعرف ما نُفِّذ لا يملك الكتابة على المخرَج التعاقديّ، ومن
/// يملكها لم يكن يرى ادّعاءه. لذا يفصل هذا الملفّ بين حارسَين مختلفين عمدًا — **الرفع**
/// يكفيه أن ترى المشروع، و**الحسم** يتطلّب الكتابة التشغيليّة. إسقاط أحدهما في الآخر
/// يُلغي معنى «مقترح»: لو ساوينا الحارسَين لصار الجسر بابًا خلفيًّا للكتابة، ولو منعنا
/// الرفع على من لا يكتب لبقي تنفيذه خارج المشروع أصلًا.
/// </para>
///
/// <para>
/// **قاعدة الاختبار مشتركة دائمة وتتراكم** ⟹ كلّ تجهيزة تبني عميلها ومشروعها بوسم فريد،
/// ولا يعتمد أيّ تأكيد على عدّ عالميّ لصفوف الجدول.
/// </para>
/// </summary>
[Collection("Integration")]
public class Project360ExecutionBridgeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Project360ExecutionBridgeTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ==================================================================
    // §10-1 — الرفع: حارس الرؤية لا حارس الكتابة
    // ==================================================================

    /// <summary>
    /// موظّف الفريق **يملك** رفع الادّعاء ولا يملك الكتابة المباشرة على المخرَج.
    /// هذان تأكيدان في اختبار واحد عمدًا: الفصل بينهما هو الادّعاء نفسه، وإثبات أحدهما
    /// بلا الآخر لا يثبت أنّ الجسر جسرٌ لا باب خلفيّ.
    /// </summary>
    [Fact]
    public async Task Create_TeamEmployeeMayPropose_ButStillMayNotWriteTheDeliverableDirectly()
    {
        var fx = await NewFixtureAsync();

        var proposed = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 70m);
        Assert.Equal(ExecutionProposalStatus.Pending, proposed.Status);

        var direct = await fx.TeamEmployee.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/contract-deliverables/{fx.DeliverableId}/progress",
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.InProgress, 70m));
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
    }

    /// <summary>
    /// المقترح المعلَّق **لا يحرّك رقمًا واحدًا**: لا نسبة المخرَج ولا تقدّم المشروع.
    /// لو حرّك شيئًا لصار الادّعاء تنفيذًا، ولانتفت الحاجة إلى المراجعة كلّها.
    /// </summary>
    [Fact]
    public async Task Create_PendingProposal_TouchesNeitherDeliverableNorProject()
    {
        var fx = await NewFixtureAsync();
        var before = await ReadDeliverableStateAsync(fx.DeliverableId);
        var projectBefore = await ReadProjectProgressAsync(fx.ProjectId);

        await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 90m);

        var after = await ReadDeliverableStateAsync(fx.DeliverableId);
        Assert.Equal(before.Percent, after.Percent);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(projectBefore, await ReadProjectProgressAsync(fx.ProjectId));
    }

    /// <summary>مشروع خارج النطاق يردّ 404 لا 403 — لا فرق بين «غير موجود» و«ممنوع» (FINDING-W6-04).</summary>
    [Fact]
    public async Task Create_ForeignProject_Returns404_NotForbidden()
    {
        var fx = await NewFixtureAsync();

        var res = await fx.ForeignTeamLeader.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals",
            new CreateProjectExecutionProposalRequest(
                fx.DeliverableId, 50m, ProjectDeliverableStatus.InProgress, "ادّعاء من خارج النطاق"));

        await AssertErrorAsync(res, HttpStatusCode.NotFound, Project360ErrorCodes.ProjectNotFound);
    }

    /// <summary>المخرَج المعطَّل لا يُدَّعى عليه: الادّعاء على التزام مُلغى لا معنى له.</summary>
    [Fact]
    public async Task Create_InactiveDeliverable_IsRejectedAsConflict()
    {
        var fx = await NewFixtureAsync();

        // التعطيل لا الحذف: المخرَج التعاقديّ لا يُحذف من السجلّ، يُخرَج من النشاط.
        var deactivated = await fx.Admin.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/contract-deliverables/{fx.DeliverableId}/deactivate",
            new { });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var res = await fx.TeamEmployee.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals",
            new CreateProjectExecutionProposalRequest(
                fx.DeliverableId, 50m, ProjectDeliverableStatus.InProgress, null));

        await AssertErrorAsync(res, HttpStatusCode.Conflict, Project360ErrorCodes.ProposalDeliverableInactive);
    }

    /// <summary>نسبة خارج [0,100] تُردّ قبل أن تُخزَّن — التحقّق في الخادم لا في النموذج.</summary>
    [Fact]
    public async Task Create_PercentOutOfRange_IsRejected()
    {
        var fx = await NewFixtureAsync();

        var res = await fx.TeamEmployee.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals",
            new CreateProjectExecutionProposalRequest(
                fx.DeliverableId, 140m, ProjectDeliverableStatus.InProgress, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ==================================================================
    // §10-2 — الحسم: حارس الكتابة التشغيليّة
    // ==================================================================

    /// <summary>
    /// `CanReview` **تُحسَب في الخادم لكلّ مقترح**: الرافع نفسه يقرأ مقترحه بـ`false`،
    /// والمسؤول يقرأه بـ`true`. الواجهة تعرض الأزرار بهذه القيمة وحدها.
    /// </summary>
    [Fact]
    public async Task List_CanReview_ReflectsTheReadersOwnOperationalPermission()
    {
        var fx = await NewFixtureAsync();
        await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 60m);

        var asProposer = await GetAsync<List<ProjectExecutionProposalDto>>(
            fx.TeamEmployee, $"/api/projects/{fx.ProjectId}/execution-proposals");
        var asReviewer = await GetAsync<List<ProjectExecutionProposalDto>>(
            fx.OwnerTeamLeader, $"/api/projects/{fx.ProjectId}/execution-proposals");

        Assert.False(Assert.Single(asProposer).CanReview);
        Assert.True(Assert.Single(asReviewer).CanReview);
    }

    /// <summary>من لا يملك الكتابة التشغيليّة لا يحسم — ولو كان هو من رفع الادّعاء.</summary>
    [Fact]
    public async Task Review_ByProposerWithoutOperatePermission_IsForbidden()
    {
        var fx = await NewFixtureAsync();
        var proposal = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 60m);

        var res = await fx.TeamEmployee.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals/{proposal.Id}/review",
            new ReviewProjectExecutionProposalRequest(true, null));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// القبول يكتب على المخرَج **ويحرّك تقدّم المشروع في نفس الوحدة**، ويحفظ لقطة
    /// «ما كانت النسبة قبل التطبيق». بلا اللقطة يصير الأثر غير قابل للمراجعة ولا للعكس.
    /// </summary>
    [Fact]
    public async Task Review_Accept_AppliesToDeliverable_RollsUpProject_AndSnapshotsThePreviousValue()
    {
        var fx = await NewFixtureAsync();
        var proposal = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 80m,
            ProjectDeliverableStatus.Delivered);

        var accepted = await ReviewAsync(fx.OwnerTeamLeader, fx.ProjectId, proposal.Id, accept: true);

        Assert.Equal(ExecutionProposalStatus.Accepted, accepted.Status);
        Assert.Equal(0m, accepted.PreviousProgressPercent);

        var deliverable = await ReadDeliverableStateAsync(fx.DeliverableId);
        Assert.Equal(80m, deliverable.Percent);
        Assert.Equal(ProjectDeliverableStatus.Delivered, deliverable.Status);
        Assert.NotNull(deliverable.DeliveredAtUtc);

        // تقدّم المشروع تحرّك عن الصفر الذي بدأ به ⟹ القبول يجرّ إعادة الاحتساب في نفس الوحدة.
        // الرقم **100 لا 80** عمدًا: `ProjectHealthPolicy.ComputeProjectProgress` تعدّ المخرَج
        // `Delivered` مكتملًا مهما كانت نسبته المخزَّنة، فالتسليم التزام أُوفِيَ لا نسبة تُقاس.
        // هذا سلوك قائم لم يمسّه الجسر، ويُثبَّت هنا كي لا يُغيَّر ضمنًا لاحقًا.
        Assert.Equal(100m, await ReadProjectProgressAsync(fx.ProjectId));
    }

    /// <summary>الرفض بلا سبب لا يترك ما يُصحَّح ⟹ يُردّ، ثمّ يمرّ مع السبب.</summary>
    [Fact]
    public async Task Review_Reject_RequiresAReason_AndLeavesTheDeliverableUntouched()
    {
        var fx = await NewFixtureAsync();
        var proposal = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 80m);

        var noReason = await fx.OwnerTeamLeader.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals/{proposal.Id}/review",
            new ReviewProjectExecutionProposalRequest(false, "   "));
        await AssertErrorAsync(noReason, HttpStatusCode.BadRequest,
            Project360ErrorCodes.ProposalRejectReasonRequired);

        var rejected = await ReviewAsync(fx.OwnerTeamLeader, fx.ProjectId, proposal.Id,
            accept: false, note: "الدليل لا يغطّي النسبة المُدَّعاة");

        Assert.Equal(ExecutionProposalStatus.Rejected, rejected.Status);
        Assert.Null(rejected.PreviousProgressPercent);
        Assert.Equal(0m, (await ReadDeliverableStateAsync(fx.DeliverableId)).Percent);
    }

    /// <summary>
    /// **الحسم مُمِرّ (idempotent) بالحالة لا بعلَم**: إعادة نفس القرار تنجح ولا تطبّق مرّتين،
    /// وقرار معاكس يُردّ بتعارض. إعادة التطبيق كانت لتُضاعف أثر القبول على المخرَج.
    /// </summary>
    [Fact]
    public async Task Review_Replay_SameDecisionSucceedsWithoutReapplying_OppositeDecisionConflicts()
    {
        var fx = await NewFixtureAsync();
        var proposal = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 55m);

        var first = await ReviewAsync(fx.OwnerTeamLeader, fx.ProjectId, proposal.Id, accept: true);
        var replay = await ReviewAsync(fx.OwnerTeamLeader, fx.ProjectId, proposal.Id, accept: true);

        Assert.Equal(first.ReviewedAtUtc, replay.ReviewedAtUtc);
        Assert.Equal(55m, (await ReadDeliverableStateAsync(fx.DeliverableId)).Percent);
        Assert.Equal(0m, replay.PreviousProgressPercent);

        var reversal = await fx.OwnerTeamLeader.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals/{proposal.Id}/review",
            new ReviewProjectExecutionProposalRequest(false, "عدول عن القبول"));
        await AssertErrorAsync(reversal, HttpStatusCode.Conflict, Project360ErrorCodes.ProposalAlreadyReviewed);
    }

    /// <summary>مقترح من مشروع آخر لا يُحسَم من مسار هذا المشروع (IDOR على المورد الابن).</summary>
    [Fact]
    public async Task Review_ProposalFromAnotherProject_Returns404()
    {
        var fx = await NewFixtureAsync();
        var other = await NewFixtureAsync();
        var foreign = await ProposeAsync(other.TeamEmployee, other.ProjectId, other.DeliverableId, 30m);

        var res = await fx.Admin.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals/{foreign.Id}/review",
            new ReviewProjectExecutionProposalRequest(true, null));

        await AssertErrorAsync(res, HttpStatusCode.NotFound, Project360ErrorCodes.ProposalNotFound);
    }

    // ==================================================================
    // §10-3 — الترشيح والاستشهاد بالتقرير
    // ==================================================================

    /// <summary>الترشيح بالحالة يقع في الخادم: طلب «المرفوضة» لا يعيد مقبولًا.</summary>
    [Fact]
    public async Task List_StatusFilter_IsAppliedByTheServer()
    {
        var fx = await NewFixtureAsync();
        var accepted = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 40m);
        await ReviewAsync(fx.OwnerTeamLeader, fx.ProjectId, accepted.Id, accept: true);
        var pending = await ProposeAsync(fx.TeamEmployee, fx.ProjectId, fx.DeliverableId, 45m);

        var onlyPending = await GetAsync<List<ProjectExecutionProposalDto>>(
            fx.OwnerTeamLeader, $"/api/projects/{fx.ProjectId}/execution-proposals?status=Pending");

        Assert.Equal(pending.Id, Assert.Single(onlyPending).Id);
    }

    /// <summary>
    /// الاستشهاد بتقرير من مشروع آخر يُردّ: الاستشهاد دليل، ودليلٌ من مشروع غير هذا
    /// المشروع ليس دليلًا عليه.
    /// </summary>
    [Fact]
    public async Task Create_CitingASubmissionFromAnotherProject_IsRejected()
    {
        var fx = await NewFixtureAsync();

        var res = await fx.TeamEmployee.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/execution-proposals",
            new CreateProjectExecutionProposalRequest(
                fx.DeliverableId, 50m, ProjectDeliverableStatus.InProgress, null, SubmissionId: Guid.NewGuid()));

        await AssertErrorAsync(res, HttpStatusCode.Conflict, Project360ErrorCodes.ProposalSubmissionMismatch);
    }

    // ==================================================================
    // المساعدات
    // ==================================================================

    private static async Task<ProjectExecutionProposalDto> ProposeAsync(
        HttpClient client,
        Guid projectId,
        Guid deliverableId,
        decimal percent,
        ProjectDeliverableStatus status = ProjectDeliverableStatus.InProgress)
    {
        var res = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/execution-proposals",
            new CreateProjectExecutionProposalRequest(deliverableId, percent, status, "ما نُفِّذ فعلًا"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ProjectExecutionProposalDto>())!;
    }

    private static async Task<ProjectExecutionProposalDto> ReviewAsync(
        HttpClient client, Guid projectId, Guid proposalId, bool accept, string? note = null)
    {
        var res = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/execution-proposals/{proposalId}/review",
            new ReviewProjectExecutionProposalRequest(accept, note));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ProjectExecutionProposalDto>())!;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode expected, string errorCode)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(expected, res.StatusCode);
        Assert.Contains(errorCode, body);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<T>())!;
    }

    private async Task<(decimal Percent, ProjectDeliverableStatus Status, DateTime? DeliveredAtUtc)>
        ReadDeliverableStateAsync(Guid deliverableId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Set<ProjectDeliverable>().AsNoTracking()
            .Where(d => d.Id == deliverableId)
            .Select(d => new { d.ProgressPercent, d.Status, d.DeliveredAtUtc })
            .FirstAsync();
        return (row.ProgressPercent, row.Status, row.DeliveredAtUtc);
    }

    private async Task<decimal> ReadProjectProgressAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.ProgressPercent)
            .FirstAsync();
    }

    private sealed record Fixture(
        Guid ProjectId,
        Guid DeliverableId,
        HttpClient Admin,
        HttpClient OwnerTeamLeader,
        HttpClient ForeignTeamLeader,
        HttpClient TeamEmployee);

    /// <summary>
    /// مشروع بمخرَج تعاقديّ واحد نشط عند الصفر، وثلاث هويّات هي بالضبط المداخل التي
    /// يفرّق بينها الجسر: من يملك كلّ شيء، من يملك الكتابة التشغيليّة، ومن يرى فقط.
    /// </summary>
    private async Task<Fixture> NewFixtureAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ownerTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var foreignTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var employee = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var ownerTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, ownerTl.UserId, employee.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, foreignTl.UserId);

        Guid projectId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tag = Guid.NewGuid().ToString("N")[..8];
            var client = new Client { Name = $"عميل جسر {tag}", Status = ClientStatus.Active };
            db.Clients.Add(client);
            await db.SaveChangesAsync();

            var project = new Project
            {
                ClientId = client.Id,
                Name = $"مشروع جسر {tag}",
                ServiceType = ServiceType.Other,
                Status = ProjectStatus.Active,
                OwnerTeamId = ownerTeam,
                TeamLeaderId = ownerTl.UserId,
                ProgressPercent = 0m,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            projectId = project.Id;
        }

        var deliverableRes = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/contract-deliverables",
            new CreateProjectContractDeliverableRequest("monthly_report", PlannedQuantity: 12));
        Assert.Equal(HttpStatusCode.OK, deliverableRes.StatusCode);
        var deliverableId = (await deliverableRes.ReadAsync<ProjectContractDeliverableDto>())!.Id;

        return new Fixture(projectId, deliverableId, admin, ownerTl.Client, foreignTl.Client, employee.Client);
    }
}

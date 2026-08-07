using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// LEAVE-WORKFLOW-DEADLOCK-HOTFIX — المسار P2 «طيّ خطوة المدير المضبوط تشغيليًّا».
/// الجذر البنيويّ: حين يكون قائد فريق الموظّف هو نفسه مديره المباشر
/// (Team.TeamLeaderId == RequesterUser.ManagerId)، يعتمد الشخص خطوة قائد الفريق، ثم يمنعه
/// حارس «عدم تصرّف الشخص نفسه في خطوتين» من اعتماد خطوة المدير ⇒ جمود عند CurrentStep=Manager.
/// إصلاح P2: بعد اعتماد خطوة قائد الفريق وكون المُعتمِد == المدير المباشر لمقدّم الطلب،
/// لا نطوي بمجرّد التطابق الاسميّ؛ بل نبحث أوّلًا عن «مدير تشغيليّ بديل» داخل شجرة الموظّف الإدارية
/// (مستخدم نشط يحمل دور Manager ويشمل نطاقه الموظّف عبر سلسلة ManagerId، مختلف عن قائد الفريق).
/// إن وُجد ⇒ لا طيّ (المسار الطبيعيّ: TeamLeaderApproved/Manager، يعتمده المدير الأعلى).
/// إن لم يوجد ⇒ يُطوى خطوة المدير آليًّا في المعاملة ذاتها: الحالة → ManagerApproved، الخطوة → Hr،
/// ManagerReviewerId = المُعتمِد، وحدث تدقيق مميّز manager_step_auto_folded_no_operational_manager.
/// أدوار الشركة/الحوكمة (Admin/CEO/GeneralManager/CeoSupport) لا تُعدّ بديلًا تشغيليًّا.
/// لا خصم مضاعف، ولا قراران من الشخص، والحارس ذاته دون تعديل، والطلبات القديمة/المسارات العاديّة دون تغيير.
/// </summary>
[Collection("Integration")]
public class LeaveWorkflowDeadlockHotfixTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveWorkflowDeadlockHotfixTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string FoldEvent = "manager_step_auto_folded_no_operational_manager";

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        // شخص واحد هو قائد فريق الموظّف ومديره المباشر معًا (سيناريو الجمود):
        public required (HttpClient C, Guid Id) TlMgr;
        public required (HttpClient C, Guid Id) Emp;       // موظّف قائد فريقه = مديره = TlMgr
        // مسار عاديّ (قائد الفريق ≠ المدير):
        public required (HttpClient C, Guid Id) Mgr2;
        public required (HttpClient C, Guid Id) Tl2;
        public required (HttpClient C, Guid Id) EmpNormal; // قائد فريقه Tl2 ومديره Mgr2 (مختلفان)
        public required (HttpClient C, Guid Id) Hr;
        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        // شخص الجمود: بدور قائد فريق، مديره GM، وهو نفسه المدير المباشر لـ emp وقائد فريقه.
        var tlmgr = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gm.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlmgr.UserId);

        // مسار عاديّ منفصل: قائد فريق ومدير مختلفان.
        var mgr2 = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl2 = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr2.UserId);
        var empNormal = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mgr2.UserId);

        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, gm.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // فريق الجمود: قائده TlMgr يضمّ emp (فيصير TlMgr قائد فريق emp ومديره معًا).
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlmgr.UserId, emp.UserId);
        // فريق عاديّ: قائده Tl2 يضمّ empNormal (قائد الفريق ≠ المدير Mgr2).
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl2.UserId, empNormal.UserId);

        foreach (var id in new[] { emp.UserId, empNormal.UserId, tlmgr.UserId, tl2.UserId })
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            TlMgr = (tlmgr.Client, tlmgr.UserId),
            Emp = (emp.Client, emp.UserId),
            Mgr2 = (mgr2.Client, mgr2.UserId),
            Tl2 = (tl2.Client, tl2.UserId),
            EmpNormal = (empNormal.Client, empNormal.UserId),
            Hr = (hr.Client, hr.UserId),
            Admin = admin,
        };
    }

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, DateOnly start, DateOnly end)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, "سبب الإجازة", null),
            TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionAsync(HttpClient c, DateOnly day, TimeOnly from, TimeOnly to)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null, from, to, "سبب الاستئذان", null),
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

    private static Task<HttpResponseMessage> TlApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> TlRejectAsync(HttpClient c, Guid id, string reason)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/reject", new LeaveRejectRequest(reason), TestJson.Options);

    private static Task<HttpResponseMessage> MgrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> MgrRejectAsync(HttpClient c, Guid id, string reason)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/reject", new LeaveRejectRequest(reason), TestJson.Options);

    private static Task<HttpResponseMessage> HrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

    private static Task<HttpResponseMessage> HrRejectAsync(HttpClient c, Guid id, string reason)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/reject", new LeaveRejectRequest(reason), TestJson.Options);

    private static async Task<LeaveRequestDto> GetAsync(HttpClient c, Guid id)
        => (await (await c.GetAsync($"/api/leave-requests/{id}")).ReadAsync<LeaveRequestDto>())!;

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year=2026"))
            .ReadAsync<EmployeeLedgerDto>())!;

    // ===== 1) المسار العاديّ (قائد الفريق ≠ المدير) لم يتغيّر: لا طيّ، ManagerReviewerId يُسجَّل بالاعتماد الفعليّ. =====
    [Fact]
    public async Task Normal_TlNotEqualManager_NoFold_PathUnchanged()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.EmpNormal.C, new(2026, 1, 5), new(2026, 1, 7)));
        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);

        // قائد الفريق الفعليّ (Tl2) يعتمد ⇒ ينتقل إلى المدير (لا طيّ لأن Tl2 ليس مدير الموظّف).
        var afterTl = await OkAsync(await TlApproveAsync(org.Tl2.C, created.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, afterTl.Status);
        Assert.Equal(LeaveRequestStep.Manager, afterTl.CurrentStep);
        Assert.Null(afterTl.ManagerReviewerId);
        Assert.DoesNotContain(afterTl.Timeline, e => e.Action == FoldEvent);

        // المدير المختلف (Mgr2) يعتمد خطوته فعليًّا.
        var afterMgr = await OkAsync(await MgrApproveAsync(org.Mgr2.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, afterMgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, afterMgr.CurrentStep);
        Assert.Equal(org.Mgr2.Id, afterMgr.ManagerReviewerId);
    }

    // ===== 2) الجمود: قائد الفريق == المدير ⇒ اعتماد خطوة القائد يطوي خطوة المدير آليًّا ⇒ ينتقل لـ HR بلا توقّف. =====
    [Fact]
    public async Task Deadlock_TlEqualsManager_FoldsManagerStep_GoesToHr()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 2, 5), new(2026, 2, 7)));
        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);

        var folded = await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        // الطيّ الآليّ: الحالة قفزت إلى ManagerApproved والخطوة إلى Hr دون توقّف.
        Assert.Equal(LeaveRequestStatus.ManagerApproved, folded.Status);
        Assert.Equal(LeaveRequestStep.Hr, folded.CurrentStep);
        // قرار واحد فعليّ: نفس الشخص مسجَّل قائدَ فريق ومديرًا (لأنه فعلًا كذلك) — لا اعتماد مدير بشريّ منفصل.
        Assert.Equal(org.TlMgr.Id, folded.TeamLeaderReviewerId);
        Assert.Equal(org.TlMgr.Id, folded.ManagerReviewerId);
        Assert.NotNull(folded.ManagerDecisionAtUtc);
    }

    // ===== 3) حدث التدقيق المميّز للطيّ الآليّ موجود بخطوة Manager وانتقال TeamLeaderApproved→ManagerApproved. =====
    [Fact]
    public async Task Deadlock_Fold_EmitsDistinctAuditEvent_NotForgedManualDecision()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 3, 5), new(2026, 3, 7)));
        var folded = await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        var foldEvt = Assert.Single(folded.Timeline, e => e.Action == FoldEvent);
        Assert.Equal(LeaveRequestStep.Manager, foldEvt.Step);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, foldEvt.FromStatus);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, foldEvt.ToStatus);
        Assert.Equal(org.TlMgr.Id, foldEvt.ActorUserId);

        // حدث اعتماد قائد الفريق الطبيعيّ ما زال موجودًا (لم يُستبدَل) — قرار حقيقيّ واحد + طيّ موثَّق.
        Assert.Contains(folded.Timeline, e => e.Step == LeaveRequestStep.TeamLeader
            && e.ToStatus == LeaveRequestStatus.TeamLeaderApproved && e.Action != FoldEvent);
    }

    // ===== 4) بعد الطيّ يعتمد HR نهائيًّا ⇒ HrApproved + صفّ خصم واحد (خصم لمرّة واحدة). =====
    [Fact]
    public async Task Deadlock_Fold_ThenHrApprove_DeductsOnce()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 4, 5), new(2026, 4, 7))); // 3 أيام
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        var final = await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));
        Assert.Equal(LeaveRequestStatus.HrApproved, final.Status);
        Assert.Equal(LeaveRequestStep.Completed, final.CurrentStep);
        Assert.Equal(org.Gm.Id, final.HrReviewerId);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debits = ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit).ToList();
        var single = Assert.Single(debits);
        Assert.Equal(3, single.Amount);
        Assert.Equal(362, ledger.AnnualLeave.Remaining); // 365 - 3
    }

    // ===== 5) نفس الشخص لا يتّخذ قرارين: بعد الطيّ، محاولة TlMgr اعتماد خطوة المدير ⇒ 403 (الحارس صامد). =====
    [Fact]
    public async Task Deadlock_Fold_SamePerson_CannotApproveManagerStep_Forbidden()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 5, 5), new(2026, 5, 7)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        var mgrTry = await MgrApproveAsync(org.TlMgr.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, mgrTry.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(mgrTry));

        // الحالة لم تتغيّر بالمحاولة المرفوضة.
        var still = await GetAsync(org.Emp.C, created.Id);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, still.Status);
        Assert.Equal(LeaveRequestStep.Hr, still.CurrentStep);
    }

    // ===== 6) نقر مزدوج على اعتماد خطوة قائد الفريق (نفس الشخص) بعد الطيّ ⇒ 403، وطيّ واحد فقط. =====
    [Fact]
    public async Task Deadlock_DoubleClickTlApprove_NoDuplicateFold()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 6, 5), new(2026, 6, 7)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        var second = await TlApproveAsync(org.TlMgr.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);

        var after = await GetAsync(org.Emp.C, created.Id);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, after.Status);
        Assert.Single(after.Timeline, e => e.Action == FoldEvent); // طيّ واحد لا غير
    }

    // ===== 7) idempotency للخصم: إعادة اعتماد HR بعد الاعتماد النهائيّ لا تُضاعف الخصم. =====
    [Fact]
    public async Task Deadlock_HrApproveTwice_LedgerNotDoubled()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 7, 5), new(2026, 7, 7)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));

        // إعادة الاعتماد النهائيّ: الحالة أصبحت HrApproved ⇒ لم تعُد تسمح ⇒ لا خصم إضافيّ.
        var again = await HrApproveAsync(org.Gm.C, created.Id);
        Assert.NotEqual(HttpStatusCode.OK, again.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // ===== 8) التزامن: اعتمادان متوازيان لخطوة قائد الفريق ⇒ الحالة النهائيّة مطويّة ومتّسقة وخصم واحد بعد HR. =====
    [Fact]
    public async Task Deadlock_ConcurrentTlApprove_SafeSingleDeduction()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 8, 5), new(2026, 8, 7)));

        var t1 = TlApproveAsync(org.TlMgr.C, created.Id);
        var t2 = TlApproveAsync(org.TlMgr.C, created.Id);
        var results = await Task.WhenAll(t1, t2);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);

        var after = await GetAsync(org.Emp.C, created.Id);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, after.Status);
        Assert.Equal(LeaveRequestStep.Hr, after.CurrentStep);

        await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // ===== 9) رفض قائد الفريق في سياق الجمود ⇒ لا طيّ ولا خصم. =====
    [Fact]
    public async Task Deadlock_TlReject_NoFold_NoDeduction()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 9, 5), new(2026, 9, 7)));

        var rejected = await OkAsync(await TlRejectAsync(org.TlMgr.C, created.Id, "غير مبرّر"));
        Assert.Equal(LeaveRequestStatus.TeamLeaderRejected, rejected.Status);
        Assert.DoesNotContain(rejected.Timeline, e => e.Action == FoldEvent);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.DoesNotContain(ledger.Entries, e => e.Source == BalanceSource.ApprovedLeave);
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // ===== 10) رفض المدير في المسار العاديّ (قائد الفريق ≠ المدير) لم يتغيّر. =====
    [Fact]
    public async Task Normal_ManagerReject_Unchanged()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.EmpNormal.C, new(2026, 10, 5), new(2026, 10, 7)));
        await OkAsync(await TlApproveAsync(org.Tl2.C, created.Id));

        var rejected = await OkAsync(await MgrRejectAsync(org.Mgr2.C, created.Id, "لا يمكن الآن"));
        Assert.Equal(LeaveRequestStatus.ManagerRejected, rejected.Status);
    }

    // ===== 11) رفض HR بعد الطيّ ⇒ HrRejected، والرصيد يعود كما كان بلا أثر صافٍ. =====
    // LEAVE-DEDUCTION-ON-TL-APPROVAL-R1: صار الخصم يقع عند اعتماد قائد الفريق (وهو ما يطوي خطوة المدير هنا)،
    // ثمّ يستوجب رفضُ HR اللاحق عكسًا واحدًا. النتيجة الصافية أمام الموظّف لم تتغيّر (365) لكنّ السجلّ
    // صار يوثّق الحركتين (خصم + عكس) بدل ألّا يوثّق شيئًا — وهذا هو المطلوب (السجلّ لا يُحذف منه).
    [Fact]
    public async Task Deadlock_HrReject_AfterFold_DebitReversed_NetZero()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 11, 5), new(2026, 11, 7)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        // الخصم وقع فورًا عند اعتماد قائد الفريق (قبل رفض HR).
        var mid = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(mid.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        Assert.Equal(362, mid.AnnualLeave.Remaining);

        var rejected = await OkAsync(await HrRejectAsync(org.Gm.C, created.Id, "مرفوض نهائيًّا"));
        Assert.Equal(LeaveRequestStatus.HrRejected, rejected.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debit = Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        var reversal = Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.Reversal && e.Direction == BalanceDirection.Credit
            && e.RelatedRequestId == created.Id));
        Assert.Equal(debit.Amount, reversal.Amount);
        Assert.Equal(365, ledger.AnnualLeave.Remaining); // الأثر الصافي = صفر
    }

    // ===== 12) حارس اعتماد الذات صامد: مقدّم الطلب لا يعتمد طلبه (حتى لو كان قائد فريق/مدير). =====
    [Fact]
    public async Task SelfApproval_Guard_Intact()
    {
        var org = await BuildOrgAsync();
        // TlMgr يقدّم طلبًا لنفسه: يُتخطّى قائد الفريق (T-WF1) ويبدأ عند المدير، ولا يعتمده هو.
        var created = await OkAsync(await CreateLeaveAsync(org.TlMgr.C, new(2026, 1, 12), new(2026, 1, 14)));
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);

        var selfTry = await MgrApproveAsync(org.TlMgr.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, selfTry.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(selfTry));
    }

    // ===== 13) فصل رصيد الإجازة عن الاستئذان: اعتماد إجازة يخصم AnnualLeave فقط ولا يمسّ رصيد الاستئذان. =====
    [Fact]
    public async Task Deadlock_Fold_LeaveDeduction_DoesNotTouchPermissionBalance()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 2, 16), new(2026, 2, 18)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        // لا حركة خصم على رصيد الاستئذان.
        Assert.DoesNotContain(ledger.Entries, e => e.Source == BalanceSource.ApprovedPermission);
    }

    // ===== 14) عزل الطلبات: معالجة طلب الجمود لا تغيّر طلبًا آخر معلَّقًا (لا تغيير تلقائيّ للطلبات القائمة). =====
    [Fact]
    public async Task ProcessingOneRequest_DoesNotAutoChangeAnother()
    {
        var org = await BuildOrgAsync();
        var other = await OkAsync(await CreateLeaveAsync(org.EmpNormal.C, new(2026, 3, 16), new(2026, 3, 18)));
        Assert.Equal(LeaveRequestStep.TeamLeader, other.Status == LeaveRequestStatus.Submitted ? other.CurrentStep : LeaveRequestStep.TeamLeader);

        var deadlock = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 3, 20), new(2026, 3, 22)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, deadlock.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, deadlock.Id));

        var otherAfter = await GetAsync(org.EmpNormal.C, other.Id);
        Assert.Equal(LeaveRequestStatus.Submitted, otherAfter.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, otherAfter.CurrentStep);
        Assert.DoesNotContain(otherAfter.Timeline, e => e.Action == FoldEvent);
    }

    // ===== 15) الاعتماد النهائيّ عبر HR/الأدمن لم يُكسَر (بديل GM): الأدمن يعتمد نهائيًّا طلبًا مطويًّا. =====
    [Fact]
    public async Task Deadlock_Fold_AdminFinalApproval_Works()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 4, 16), new(2026, 4, 18)));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));

        var final = await OkAsync(await HrApproveAsync(org.Admin, created.Id));
        Assert.Equal(LeaveRequestStatus.HrApproved, final.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
    }

    // ===== 16) لا تراجع للطيّ في مسار fallback: موظّف مديره ليس قائد فريقه؛ اعتماد القائد لا يطوي خطأً. =====
    [Fact]
    public async Task Fold_DoesNotTrigger_WhenTlApproverIsNotRequesterManager()
    {
        var org = await BuildOrgAsync();
        // EmpNormal: مديره Mgr2، قائد فريقه Tl2. اعتماد Tl2 لخطوة القائد يجب ألّا يطوي (Tl2 ≠ مدير Emp).
        var created = await OkAsync(await CreateLeaveAsync(org.EmpNormal.C, new(2026, 5, 16), new(2026, 5, 18)));
        var afterTl = await OkAsync(await TlApproveAsync(org.Tl2.C, created.Id));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, afterTl.Status);
        Assert.Equal(LeaveRequestStep.Manager, afterTl.CurrentStep); // توقّف عند المدير كالمعتاد، لا طيّ
        Assert.Null(afterTl.ManagerReviewerId);
        Assert.DoesNotContain(afterTl.Timeline, e => e.Action == FoldEvent);
    }

    // ===== 17) المسار العاديّ الكامل حتى الخصم: قائد الفريق ≠ المدير ⇒ صفّ خصم واحد. =====
    [Fact]
    public async Task Normal_FullFlow_DeductsOnce()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.EmpNormal.C, new(2026, 6, 16), new(2026, 6, 18)));
        await OkAsync(await TlApproveAsync(org.Tl2.C, created.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr2.C, created.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));

        var ledger = await LedgerAsync(org.Admin, org.EmpNormal.Id);
        var single = Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
        Assert.Equal(3, single.Amount);
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    private async Task SetUserActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsActive = active;
        await db.SaveChangesAsync();
    }

    private async Task SeedOpeningAsync(HttpClient admin, params Guid[] ids)
    {
        foreach (var id in ids)
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);
    }

    // ===== 18) P2: قائد الفريق == المدير المباشر ويوجد Manager تشغيليّ بديل أعلى ⇒ لا طيّ، يعتمده المدير الأعلى. =====
    [Fact]
    public async Task P2_OperationalManagerAlternativeAbove_NoFold_HigherManagerApproves()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgrAbove = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tlmgr = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgrAbove.UserId); // قائد فريق = مدير مباشر
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlmgr.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlmgr.UserId, emp.UserId);
        await SeedOpeningAsync(admin, emp.UserId);

        var created = await OkAsync(await CreateLeaveAsync(emp.Client, new(2026, 1, 20), new(2026, 1, 22)));
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);

        // اعتماد قائد الفريق (= المدير المباشر) مع وجود Manager تشغيليّ بديل أعلى ⇒ لا طيّ، المسار الطبيعيّ.
        var afterTl = await OkAsync(await TlApproveAsync(tlmgr.Client, created.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, afterTl.Status);
        Assert.Equal(LeaveRequestStep.Manager, afterTl.CurrentStep);
        Assert.Null(afterTl.ManagerReviewerId);
        Assert.DoesNotContain(afterTl.Timeline, e => e.Action == FoldEvent);

        // المدير الأعلى (البديل التشغيليّ) يعتمد خطوة المدير فعليًّا ⇒ HR.
        var afterMgr = await OkAsync(await MgrApproveAsync(mgrAbove.Client, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, afterMgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, afterMgr.CurrentStep);
        Assert.Equal(mgrAbove.UserId, afterMgr.ManagerReviewerId);

        var final = await OkAsync(await HrApproveAsync(gm.Client, created.Id));
        Assert.Equal(LeaveRequestStatus.HrApproved, final.Status);
        var ledger = await LedgerAsync(admin, emp.UserId);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit));
    }

    // ===== 19) P2: أكثر من مستوى — أوّل Manager نشط في السلسلة (فوق وسيط قائد فريق) = بديل تشغيليّ ⇒ لا طيّ. =====
    [Fact]
    public async Task P2_MultiLevel_FirstActiveManagerInChainIsAlternative_NoFold()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgrTop = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var midTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgrTop.UserId);   // وسيط بلا دور Manager
        var tlmgr = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, midTl.UserId);     // قائد فريق = مدير مباشر
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlmgr.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlmgr.UserId, emp.UserId);
        await SeedOpeningAsync(admin, emp.UserId);

        var created = await OkAsync(await CreateLeaveAsync(emp.Client, new(2026, 2, 20), new(2026, 2, 22)));
        var afterTl = await OkAsync(await TlApproveAsync(tlmgr.Client, created.Id));
        // الوسيط midTl (TeamLeader) لا يُعدّ بديلًا؛ أوّل Manager نشط = mgrTop ⇒ لا طيّ.
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, afterTl.Status);
        Assert.Equal(LeaveRequestStep.Manager, afterTl.CurrentStep);
        Assert.DoesNotContain(afterTl.Timeline, e => e.Action == FoldEvent);

        var afterMgr = await OkAsync(await MgrApproveAsync(mgrTop.Client, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, afterMgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, afterMgr.CurrentStep);
        Assert.Equal(mgrTop.UserId, afterMgr.ManagerReviewerId);
    }

    // ===== 20) P2: المدير الأعلى غير نشط ⇒ ليس بديلًا تشغيليًّا ⇒ يُطوى خطوة المدير آليًّا إلى HR. =====
    [Fact]
    public async Task P2_HigherManagerInactive_NotAlternative_Folds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgrAbove = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tlmgr = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgrAbove.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlmgr.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlmgr.UserId, emp.UserId);
        await SeedOpeningAsync(admin, emp.UserId);

        await SetUserActiveAsync(mgrAbove.UserId, false); // المدير الأعلى غير نشط ⇒ يُستبعَد كبديل

        var created = await OkAsync(await CreateLeaveAsync(emp.Client, new(2026, 3, 20), new(2026, 3, 22)));
        var folded = await OkAsync(await TlApproveAsync(tlmgr.Client, created.Id));
        // لا بديل تشغيليّ نشط في الشجرة (mgrAbove غير نشط، وفوقه GM فقط) ⇒ طيّ آليّ.
        Assert.Equal(LeaveRequestStatus.ManagerApproved, folded.Status);
        Assert.Equal(LeaveRequestStep.Hr, folded.CurrentStep);
        Assert.Equal(tlmgr.UserId, folded.ManagerReviewerId);
        Assert.Single(folded.Timeline, e => e.Action == FoldEvent);
    }

    // ===== 21) P2: فوق شخص الجمود GM فقط (GeneralManager، دور شركة) ⇒ ليس بديلًا تشغيليًّا ⇒ طيّ. =====
    [Fact]
    public async Task P2_OnlyGeneralManagerAbove_NotOperationalAlternative_Folds()
    {
        var org = await BuildOrgAsync();
        // فوق TlMgr لا يوجد سوى GM (GeneralManager) — لا يحمل دور Manager ⇒ لا بديل تشغيليّ.
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 7, 20), new(2026, 7, 22)));
        var folded = await OkAsync(await TlApproveAsync(org.TlMgr.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, folded.Status);
        Assert.Equal(LeaveRequestStep.Hr, folded.CurrentStep);
        var evt = Assert.Single(folded.Timeline, e => e.Action == FoldEvent);
        Assert.Equal(LeaveRequestStep.Manager, evt.Step);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, evt.FromStatus);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, evt.ToStatus);
    }
}

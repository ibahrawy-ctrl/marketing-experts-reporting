using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// LEAVE-DEDUCTION-ON-TL-APPROVAL-R1 — نقل لحظة خصم رصيد الإجازة/الاستئذان إلى «اعتماد قائد الفريق».
///
/// العقد السلوكيّ المُثبَت هنا (القواعد العشر):
/// 1) اعتماد قائد الفريق ⇒ حركة خصم واحدة (Debit) في المعاملة ذاتها، والرصيد ينقص فورًا، والطلب يواصل مساره.
/// 2) رفض قائد الفريق ⇒ لا خصم ولا حركة إطلاقًا.
/// 3) اعتماد المدير ⇒ لا خصم إضافيّ ولا تغيير للمقدار.
/// 4) الاعتماد النهائي (HR) ⇒ لا خصم إضافيّ ولا تغيير للمقدار.
/// 5) رفض المدير بعد اعتماد قائد الفريق ⇒ عكس (Reversal) واحد واستعادة الرصيد مرّة واحدة.
/// 6) رفض HR بعد اعتماد قائد الفريق ⇒ عكس واحد واستعادة الرصيد مرّة واحدة.
/// 7) الإلغاء بعد اعتماد قائد الفريق ⇒ عكس واحد وفق سياسة الإلغاء القائمة.
/// 8) الإعادة للتعديل بعد اعتماد قائد الفريق ⇒ إبطال دورة الاعتماد ⇒ عكس واحد (لا يوجد مسار «استئناف»
///    للطلب المُعاد: الحالة تعود إلى ReturnedForEdit عند خطوة الموظّف، ولا نقطة نهاية لإعادة الإرسال،
///    فالمواصلة تكون بطلب جديد ⇒ إبقاء الخصم كان سيحجز رصيدًا بلا دورة اعتماد حيّة).
/// 9) الطلب الجديد بعد العكس ⇒ دورة خصم مستقلّة تمامًا بمفتاح (RelatedRequestId, Source) جديد.
/// 10) النقر المزدوج/التزامن ⇒ خصم واحد بالضبط وعكس واحد بالضبط، ولا رصيد سالب من تكرار تقنيّ.
///
/// ثوابت مُلزِمة: لا اعتماد آليّ، لا رفض آليّ، لا خصم قبل اعتماد قائد الفريق، ولا حذف من السجلّ
/// (التصحيح دائمًا بحركة معاكسة). الرصيد مشتقّ من السجلّ: Remaining = ΣCredit − ΣDebit.
/// </summary>
[Collection("Integration")]
public class LeaveDeductionOnTeamLeaderApprovalTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveDeductionOnTeamLeaderApprovalTests(CustomWebApplicationFactory factory) => _factory = factory;

    // سنة معزولة تمامًا: لا سياسة أرصدة (balance_policies) لها في قاعدة الاختبار المشتركة الدائمة،
    // فلا حدّ شهريّ للأذونات ولا تلوّث من مجموعات اختبار أخرى (2026 / 2089–2099).
    private const int Y = 2064;

    private const string FoldEvent = "manager_step_auto_folded_no_operational_manager";

    private sealed class Org
    {
        // المسار الطبيعيّ ثلاثيّ الخطوات: قائد الفريق ≠ المدير المباشر.
        public required (HttpClient C, Guid Id) Gm;      // الاعتماد/الرفض النهائي + الإعادة للتعديل
        public required (HttpClient C, Guid Id) Mgr;     // مدير emp المباشر
        public required (HttpClient C, Guid Id) Tl;      // قائد فريق emp (ليس مديره)
        public required (HttpClient C, Guid Id) Emp;

        // مسار الطيّ (P2): قائد الفريق هو نفسه المدير المباشر ⇒ اعتماده يطوي خطوة المدير.
        public required (HttpClient C, Guid Id) TlMgr;
        public required (HttpClient C, Guid Id) EmpFold;

        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync(decimal opening = 365)
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mgr.UserId);

        var tlmgr = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gm.UserId);
        var empFold = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlmgr.UserId);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlmgr.UserId, empFold.UserId);

        foreach (var id in new[] { emp.UserId, empFold.UserId })
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, opening, Y, "رصيد اختبار"), TestJson.Options);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Mgr = (mgr.Client, mgr.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            TlMgr = (tlmgr.Client, tlmgr.UserId),
            EmpFold = (empFold.Client, empFold.UserId),
            Admin = admin,
        };
    }

    // ===== أدوات =====

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, int month, int day, int days)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave,
                new DateOnly(Y, month, day), new DateOnly(Y, month, day + days - 1),
                null, null, "سبب الإجازة", null), TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionAsync(HttpClient c, int month, int day)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission,
                new DateOnly(Y, month, day), null, new TimeOnly(9, 0), new TimeOnly(11, 0),
                "سبب الاستئذان", null), TestJson.Options);

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

    private static Task<HttpResponseMessage> TlRejectAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/reject", new LeaveRejectRequest("رفض قائد الفريق"), TestJson.Options);

    private static Task<HttpResponseMessage> MgrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> MgrRejectAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/reject", new LeaveRejectRequest("رفض المدير"), TestJson.Options);

    private static Task<HttpResponseMessage> HrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

    private static Task<HttpResponseMessage> HrRejectAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/reject", new LeaveRejectRequest("رفض نهائيّ"), TestJson.Options);

    private static Task<HttpResponseMessage> ReturnAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/return", new LeaveReturnRequest("أعد التعديل"), TestJson.Options);

    private static Task<HttpResponseMessage> CancelAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/cancel", new { }, TestJson.Options);

    private static Task<HttpResponseMessage> RevokeAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/revoke", new LeaveRevokeRequest("إبطال إداريّ"), TestJson.Options);

    private static async Task<LeaveRequestDto> GetAsync(HttpClient c, Guid id)
        => (await (await c.GetAsync($"/api/leave-requests/{id}")).ReadAsync<LeaveRequestDto>())!;

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year={Y}"))
            .ReadAsync<EmployeeLedgerDto>())!;

    private static IEnumerable<BalanceLedgerEntryDto> Debits(EmployeeLedgerDto l, Guid reqId, BalanceSource src)
        => l.Entries.Where(e => e.Direction == BalanceDirection.Debit && e.Source == src && e.RelatedRequestId == reqId);

    private static IEnumerable<BalanceLedgerEntryDto> Reversals(EmployeeLedgerDto l, Guid reqId)
        => l.Entries.Where(e => e.Direction == BalanceDirection.Credit
                                && e.Source == BalanceSource.Reversal && e.RelatedRequestId == reqId);

    // ================= القاعدة 1 — الخصم عند اعتماد قائد الفريق =================

    // 1/1 — اعتماد قائد الفريق يُنشئ خصمًا واحدًا فورًا وينقص الرصيد، والطلب يواصل مساره إلى المدير.
    [Fact]
    public async Task R1_TlApprove_CreatesSingleDebit_AndDeductsImmediately()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 1, 5, 3));

        var before = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(365, before.AnnualLeave.Remaining);

        var afterTl = await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, afterTl.Status);
        Assert.Equal(LeaveRequestStep.Manager, afterTl.CurrentStep);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debit = Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(3, debit.Amount);
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // 1/2 — حقول حركة الخصم صحيحة بالكامل (النوع/الاتجاه/المصدر/السنة/ربط الطلب).
    [Fact]
    public async Task R1_DebitEntry_FieldsAreCorrect()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 2, 10, 4));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debit = Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(BalanceType.AnnualLeave, debit.BalanceType);
        Assert.Equal(BalanceDirection.Debit, debit.Direction);
        Assert.Equal(BalanceSource.ApprovedLeave, debit.Source);
        Assert.Equal(Y, debit.Year);
        Assert.Equal(req.Id, debit.RelatedRequestId);
        Assert.Equal(4, debit.Amount); // شامل الطرفين: 10..13
        Assert.Equal(org.Tl.Id, debit.CreatedBy); // المُعتمِد الفعليّ = قائد الفريق
    }

    // 1/3 — الاستئذان يُخصَم بمقدار 1 من رصيد الأذونات بمصدر ApprovedPermission.
    [Fact]
    public async Task R1_TlApprove_Permission_DebitsOneFromPermissionBalance()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreatePermissionAsync(org.Emp.C, 3, 6));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debit = Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedPermission));
        Assert.Equal(1, debit.Amount);
        Assert.Equal(BalanceType.Permission, debit.BalanceType);
        Assert.Equal(365, ledger.AnnualLeave.Remaining); // رصيد الإجازات لم يُمَسّ
    }

    // 1/4 — لا خصم إطلاقًا قبل أيّ اعتماد (الإنشاء وحده لا يحرّك السجلّ).
    [Fact]
    public async Task R1_NoDebit_BeforeAnyApproval()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 4, 5, 3));
        Assert.Equal(LeaveRequestStatus.Submitted, req.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 1/5 — في مسار الطيّ (قائد الفريق == المدير) يقع خصم واحد أيضًا والطلب ينتقل إلى HR.
    [Fact]
    public async Task R1_Fold_TlApprove_SingleDebit_AndMovesToHr()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.EmpFold.C, 5, 5, 3));

        var folded = await OkAsync(await TlApproveAsync(org.TlMgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, folded.Status);
        Assert.Equal(LeaveRequestStep.Hr, folded.CurrentStep);
        Assert.Contains(folded.Timeline, e => e.Action == FoldEvent);

        var ledger = await LedgerAsync(org.Admin, org.EmpFold.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // ================= القاعدة 2 — رفض قائد الفريق: لا خصم =================

    [Fact]
    public async Task R2_TlReject_Leave_NoDebit_NoLedgerMovement()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 6, 5, 3));

        var rejected = await OkAsync(await TlRejectAsync(org.Tl.C, req.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderRejected, rejected.Status);
        Assert.Equal(LeaveRequestStep.Completed, rejected.CurrentStep);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R2_TlReject_Permission_NoDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreatePermissionAsync(org.Emp.C, 6, 20));

        await OkAsync(await TlRejectAsync(org.Tl.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(0, ledger.Permission.Debited);
    }

    // ================= القاعدة 3 — اعتماد المدير لا يضيف خصمًا =================

    [Fact]
    public async Task R3_ManagerApprove_AddsNoAdditionalDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 7, 5, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var afterTl = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(afterTl, req.Id, BalanceSource.ApprovedLeave));

        var afterMgr = await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, afterMgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, afterMgr.CurrentStep);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // 3/2 — اعتماد المدير لا يغيّر مقدار الخصم ولا معرّف حركته (نفس الصفّ حرفيًّا).
    [Fact]
    public async Task R3_ManagerApprove_DoesNotChangeDebitAmountOrIdentity()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 8, 5, 5));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        var d1 = Assert.Single(Debits(await LedgerAsync(org.Admin, org.Emp.Id), req.Id, BalanceSource.ApprovedLeave));

        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        var d2 = Assert.Single(Debits(await LedgerAsync(org.Admin, org.Emp.Id), req.Id, BalanceSource.ApprovedLeave));

        Assert.Equal(d1.Id, d2.Id);
        Assert.Equal(d1.Amount, d2.Amount);
        Assert.Equal(5, d2.Amount);
    }

    // ================= القاعدة 4 — الاعتماد النهائي لا يضيف خصمًا =================

    [Fact]
    public async Task R4_HrApprove_AddsNoAdditionalDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 9, 5, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));

        var final = await OkAsync(await HrApproveAsync(org.Gm.C, req.Id));
        Assert.Equal(LeaveRequestStatus.HrApproved, final.Status);
        Assert.Equal(LeaveRequestStep.Completed, final.CurrentStep);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // 4/2 — السلسلة الكاملة ثلاثيّة الخطوات ⇒ خصم واحد بالضبط عبر كامل الدورة.
    [Fact]
    public async Task R4_FullChain_ExactlyOneDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 10, 5, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(1, ledger.Entries.Count(e =>
            e.Direction == BalanceDirection.Debit && e.RelatedRequestId == req.Id));
        Assert.Empty(Reversals(ledger, req.Id));
    }

    // 4/3 — الاستئذان عبر السلسلة الكاملة ⇒ خصم واحد بمقدار 1.
    [Fact]
    public async Task R4_Permission_FullChain_ExactlyOneDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreatePermissionAsync(org.Emp.C, 10, 20));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var debit = Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedPermission));
        Assert.Equal(1, debit.Amount);
        Assert.Equal(1, ledger.Permission.Debited);
    }

    // ================= القاعدة 5 — رفض المدير بعد اعتماد قائد الفريق ⇒ عكس واحد =================

    [Fact]
    public async Task R5_ManagerReject_AfterTlApprove_SingleReversal_BalanceRestored()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 11, 5, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        Assert.Equal(362, (await LedgerAsync(org.Admin, org.Emp.Id)).AnnualLeave.Remaining);

        var rejected = await OkAsync(await MgrRejectAsync(org.Mgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.ManagerRejected, rejected.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 5/2 — العكس يطابق الخصم الأصليّ مقدارًا ونوعًا وسنةً (مرآة تامّة).
    [Fact]
    public async Task R5_Reversal_MirrorsOriginalDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 11, 20, 4));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        var debit = Assert.Single(Debits(await LedgerAsync(org.Admin, org.Emp.Id), req.Id, BalanceSource.ApprovedLeave));

        await OkAsync(await MgrRejectAsync(org.Mgr.C, req.Id));
        var reversal = Assert.Single(Reversals(await LedgerAsync(org.Admin, org.Emp.Id), req.Id));

        Assert.Equal(debit.Amount, reversal.Amount);
        Assert.Equal(debit.BalanceType, reversal.BalanceType);
        Assert.Equal(debit.Year, reversal.Year);
        Assert.Equal(BalanceDirection.Credit, reversal.Direction);
        Assert.Equal(BalanceSource.Reversal, reversal.Source);
    }

    // 5/3 — نفس السلوك للاستئذان.
    [Fact]
    public async Task R5_ManagerReject_Permission_SingleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreatePermissionAsync(org.Emp.C, 12, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        Assert.Equal(1, (await LedgerAsync(org.Admin, org.Emp.Id)).Permission.Debited);

        await OkAsync(await MgrRejectAsync(org.Mgr.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var reversal = Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(1, reversal.Amount);
        Assert.Equal(BalanceType.Permission, reversal.BalanceType);
        Assert.Equal(0, ledger.Permission.Remaining);
    }

    // ================= القاعدة 6 — رفض HR بعد اعتماد قائد الفريق ⇒ عكس واحد =================

    [Fact]
    public async Task R6_HrReject_AfterTlApprove_SingleReversal_BalanceRestored()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 12, 10, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));

        var rejected = await OkAsync(await HrRejectAsync(org.Gm.C, req.Id));
        Assert.Equal(LeaveRequestStatus.HrRejected, rejected.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 6/2 — الخصم الأصليّ لا يُحذف من السجلّ: تبقى الحركتان موثَّقتين معًا.
    [Fact]
    public async Task R6_HrReject_OriginalDebitRowIsNotDeleted()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 12, 20, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        await OkAsync(await HrRejectAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(2, ledger.Entries.Count(e => e.RelatedRequestId == req.Id));
    }

    // ================= القاعدة 7 — الإلغاء والإبطال =================

    [Fact]
    public async Task R7_Cancel_AfterTlApprove_SingleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 1, 20, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var cancelled = await OkAsync(await CancelAsync(org.Emp.C, req.Id));
        Assert.Equal(LeaveRequestStatus.Cancelled, cancelled.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R7_Cancel_BeforeAnyApproval_NoLedgerMovementAtAll()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 2, 20, 3));

        await OkAsync(await CancelAsync(org.Emp.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R7_Cancel_AfterManagerApprove_SingleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 3, 20, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));

        await OkAsync(await CancelAsync(org.Emp.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 7/4 — الإبطال الإداريّ بعد الاعتماد النهائي (المسار المحروس القائم) ⇒ عكس واحد.
    [Fact]
    public async Task R7_RevokeApproved_AfterHrApprove_SingleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 4, 20, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, req.Id));
        Assert.Equal(362, (await LedgerAsync(org.Admin, org.Emp.Id)).AnnualLeave.Remaining);

        await OkAsync(await RevokeAsync(org.Admin, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 7/5 — الإلغاء مرّتين لا يُنتج عكسًا مضاعفًا (الثاني مرفوض أصلًا بحالة الطلب).
    [Fact]
    public async Task R7_CancelTwice_NoDoubleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 5, 20, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await CancelAsync(org.Emp.C, req.Id));

        var second = await CancelAsync(org.Emp.C, req.Id);
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("leave_request.cannot_cancel", await ErrorCodeAsync(second));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // ================= القاعدة 8 — الإعادة للتعديل تُبطِل دورة الاعتماد =================

    [Fact]
    public async Task R8_Return_AfterTlApprove_SingleReversal_CycleVoided()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 6, 15, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        Assert.Equal(362, (await LedgerAsync(org.Admin, org.Emp.Id)).AnnualLeave.Remaining);

        var returned = await OkAsync(await ReturnAsync(org.Mgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.ReturnedForEdit, returned.Status);
        Assert.Equal(LeaveRequestStep.Employee, returned.CurrentStep);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R8_Return_BeforeAnyApproval_NoLedgerMovement()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 7, 15, 3));

        var returned = await OkAsync(await ReturnAsync(org.Mgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.ReturnedForEdit, returned.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R8_Return_AfterManagerApprove_SingleReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 8, 15, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await MgrApproveAsync(org.Mgr.C, req.Id));

        await OkAsync(await ReturnAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 8/4 — إعادة ثمّ إلغاء ⇒ العكس يبقى واحدًا (Idempotency عبر مسارين مختلفين).
    [Fact]
    public async Task R8_ReturnThenCancel_ReversalStaysSingle()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 9, 15, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await ReturnAsync(org.Mgr.C, req.Id));
        await OkAsync(await CancelAsync(org.Emp.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // ================= القاعدة 9 — الطلب الجديد بعد العكس دورة مستقلّة =================

    [Fact]
    public async Task R9_NewRequestAfterReversal_GetsIndependentDebit()
    {
        var org = await BuildOrgAsync();
        var first = await OkAsync(await CreateLeaveAsync(org.Emp.C, 10, 15, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, first.Id));
        await OkAsync(await CancelAsync(org.Emp.C, first.Id));
        Assert.Equal(365, (await LedgerAsync(org.Admin, org.Emp.Id)).AnnualLeave.Remaining);

        var second = await OkAsync(await CreateLeaveAsync(org.Emp.C, 11, 15, 3));
        Assert.NotEqual(first.Id, second.Id);
        await OkAsync(await TlApproveAsync(org.Tl.C, second.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, second.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // 9/2 — مفتاح (RelatedRequestId, Source) مختلف بين الدورتين ⇒ الفهرس الفريد لا يحجب الخصم الجديد.
    [Fact]
    public async Task R9_TwoCycles_HaveDistinctRelatedRequestIds()
    {
        var org = await BuildOrgAsync();
        var first = await OkAsync(await CreateLeaveAsync(org.Emp.C, 1, 10, 2));
        await OkAsync(await TlApproveAsync(org.Tl.C, first.Id));
        await OkAsync(await CancelAsync(org.Emp.C, first.Id));

        var second = await OkAsync(await CreateLeaveAsync(org.Emp.C, 2, 10, 2));
        await OkAsync(await TlApproveAsync(org.Tl.C, second.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        var d1 = Assert.Single(Debits(ledger, first.Id, BalanceSource.ApprovedLeave));
        var d2 = Assert.Single(Debits(ledger, second.Id, BalanceSource.ApprovedLeave));
        Assert.NotEqual(d1.Id, d2.Id);
        Assert.NotEqual(d1.RelatedRequestId, d2.RelatedRequestId);
    }

    // 9/3 — طلبان قائمان معًا ⇒ لكلٍّ خصمه المستقلّ والرصيد ينقص بمجموعهما.
    [Fact]
    public async Task R9_TwoParallelRequests_EachHasOwnDebit()
    {
        var org = await BuildOrgAsync();
        var a = await OkAsync(await CreateLeaveAsync(org.Emp.C, 3, 10, 2));
        var b = await OkAsync(await CreateLeaveAsync(org.Emp.C, 4, 10, 3));

        await OkAsync(await TlApproveAsync(org.Tl.C, a.Id));
        await OkAsync(await TlApproveAsync(org.Tl.C, b.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(2, Assert.Single(Debits(ledger, a.Id, BalanceSource.ApprovedLeave)).Amount);
        Assert.Equal(3, Assert.Single(Debits(ledger, b.Id, BalanceSource.ApprovedLeave)).Amount);
        Assert.Equal(360, ledger.AnnualLeave.Remaining); // 365 − 2 − 3
    }

    // ================= القاعدة 10 — النقر المزدوج والتزامن =================

    [Fact]
    public async Task R10_DoubleClickTlApprove_ExactlyOneDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 5, 10, 3));

        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        var second = await TlApproveAsync(org.Tl.C, req.Id);
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R10_ParallelTlApprove_ExactlyOneDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 6, 10, 3));

        var results = await Task.WhenAll(TlApproveAsync(org.Tl.C, req.Id), TlApproveAsync(org.Tl.C, req.Id));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R10_ParallelCancel_ExactlyOneReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 7, 10, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var results = await Task.WhenAll(CancelAsync(org.Emp.C, req.Id), CancelAsync(org.Emp.C, req.Id));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    [Fact]
    public async Task R10_DoubleManagerReject_ExactlyOneReversal()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 8, 10, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        await OkAsync(await MgrRejectAsync(org.Mgr.C, req.Id));
        var second = await MgrRejectAsync(org.Mgr.C, req.Id);
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // 10/5 — لا رصيد سالب ناتج عن تكرار تقنيّ: رصيد 3 وإجازة 3 أيام مع نقر مزدوج ⇒ المتبقّي 0 لا −3.
    [Fact]
    public async Task R10_DoubleClick_DoesNotProduceNegativeBalance()
    {
        var org = await BuildOrgAsync(opening: 3);
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 9, 10, 3));

        await Task.WhenAll(TlApproveAsync(org.Tl.C, req.Id), TlApproveAsync(org.Tl.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Equal(0, ledger.AnnualLeave.Remaining);
        Assert.False(ledger.AnnualLeave.IsNegative);
    }

    // ================= ثوابت مُلزِمة =================

    // ث1 — لا اعتماد آليّ: اعتماد قائد الفريق (المسار الطبيعيّ) لا يقفز بالطلب إلى HrApproved.
    [Fact]
    public async Task INV_NoAutoApproval_AfterTlApprove()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 10, 25, 2));

        var after = await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, after.Status);
        Assert.NotEqual(LeaveRequestStatus.HrApproved, after.Status);
        Assert.Null(after.HrReviewerId);
        Assert.Null(after.ManagerReviewerId);

        var reread = await GetAsync(org.Emp.C, req.Id);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, reread.Status);
    }

    // ث2 — لا رفض آليّ: وقوع الخصم لا يُحوّل الطلب إلى مرفوض ولا يُنشئ عكسًا من تلقاء نفسه.
    [Fact]
    public async Task INV_NoAutoRejection_AfterDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 11, 25, 2));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));

        var reread = await GetAsync(org.Emp.C, req.Id);
        Assert.NotEqual(LeaveRequestStatus.TeamLeaderRejected, reread.Status);
        Assert.NotEqual(LeaveRequestStatus.ManagerRejected, reread.Status);
        Assert.NotEqual(LeaveRequestStatus.HrRejected, reread.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Empty(Reversals(ledger, req.Id));
    }

    // ث3 — الطيّ لا يضاعف الخصم حتى بعد الاعتماد النهائي.
    [Fact]
    public async Task INV_Fold_ThenHrApprove_StillSingleDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.EmpFold.C, 12, 5, 3));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, req.Id));
        await OkAsync(await HrApproveAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.EmpFold.Id);
        Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        Assert.Empty(Reversals(ledger, req.Id));
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
    }

    // ث4 — الطيّ ثمّ رفض HR ⇒ عكس واحد وأثر صافٍ = صفر.
    [Fact]
    public async Task INV_Fold_ThenHrReject_SingleReversal_NetZero()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.EmpFold.C, 1, 5, 3));
        await OkAsync(await TlApproveAsync(org.TlMgr.C, req.Id));
        await OkAsync(await HrRejectAsync(org.Gm.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.EmpFold.Id);
        var debit = Assert.Single(Debits(ledger, req.Id, BalanceSource.ApprovedLeave));
        var reversal = Assert.Single(Reversals(ledger, req.Id));
        Assert.Equal(debit.Amount, reversal.Amount);
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // ث5 — عزل تامّ بين رصيدَي الإجازات والأذونات.
    [Fact]
    public async Task INV_LeaveAndPermissionBalances_AreIsolated()
    {
        var org = await BuildOrgAsync();
        var leave = await OkAsync(await CreateLeaveAsync(org.Emp.C, 2, 5, 3));
        var perm = await OkAsync(await CreatePermissionAsync(org.Emp.C, 3, 25));

        await OkAsync(await TlApproveAsync(org.Tl.C, leave.Id));
        await OkAsync(await TlApproveAsync(org.Tl.C, perm.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(3, ledger.AnnualLeave.Debited);
        Assert.Equal(362, ledger.AnnualLeave.Remaining);
        Assert.Equal(1, ledger.Permission.Debited);
        Assert.Equal(-1, ledger.Permission.Remaining);
    }

    // ث6 — السجلّ لا يُحذف منه: بعد دورة خصم+عكس تبقى الحركتان مسجَّلتين بجانب الرصيد الافتتاحيّ.
    [Fact]
    public async Task INV_LedgerRowsAreNeverDeleted()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.Emp.C, 4, 5, 3));
        await OkAsync(await TlApproveAsync(org.Tl.C, req.Id));
        await OkAsync(await CancelAsync(org.Emp.C, req.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.OpeningBalance);
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.ApprovedLeave && e.RelatedRequestId == req.Id);
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.Reversal && e.RelatedRequestId == req.Id);
        Assert.Equal(3, ledger.Entries.Count);
    }

    // ث7 — الرصيد مشتقّ لا مخزَّن: Remaining = Credited − Debited في كلّ الحالات.
    [Fact]
    public async Task INV_RemainingIsAlwaysDerivedFromLedger()
    {
        var org = await BuildOrgAsync();
        var a = await OkAsync(await CreateLeaveAsync(org.Emp.C, 5, 5, 3));
        var b = await OkAsync(await CreateLeaveAsync(org.Emp.C, 6, 5, 2));
        await OkAsync(await TlApproveAsync(org.Tl.C, a.Id));
        await OkAsync(await TlApproveAsync(org.Tl.C, b.Id));
        await OkAsync(await MgrRejectAsync(org.Mgr.C, a.Id));

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(ledger.AnnualLeave.Credited - ledger.AnnualLeave.Debited, ledger.AnnualLeave.Remaining);
        Assert.Equal(ledger.Permission.Credited - ledger.Permission.Debited, ledger.Permission.Remaining);
        Assert.Equal(363, ledger.AnnualLeave.Remaining); // 365 − 3 − 2 + 3
    }

    // ث8 — رفض قائد الفريق لطلب موظّف الطيّ لا يطوي ولا يخصم.
    [Fact]
    public async Task INV_Fold_TlReject_NoFold_NoDebit()
    {
        var org = await BuildOrgAsync();
        var req = await OkAsync(await CreateLeaveAsync(org.EmpFold.C, 2, 5, 3));

        var rejected = await OkAsync(await TlRejectAsync(org.TlMgr.C, req.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderRejected, rejected.Status);
        Assert.DoesNotContain(rejected.Timeline, e => e.Action == FoldEvent);

        var ledger = await LedgerAsync(org.Admin, org.EmpFold.Id);
        Assert.Empty(ledger.Entries.Where(e => e.RelatedRequestId == req.Id));
        Assert.Equal(365, ledger.AnnualLeave.Remaining);
    }

    // ث9 — معالجة طلب لا تُحرّك رصيد طلب آخر لموظّف آخر (لا آثار جانبيّة عابرة).
    [Fact]
    public async Task INV_ProcessingOneRequest_DoesNotTouchAnotherEmployeeBalance()
    {
        var org = await BuildOrgAsync();
        var mine = await OkAsync(await CreateLeaveAsync(org.Emp.C, 7, 5, 3));
        var other = await OkAsync(await CreateLeaveAsync(org.EmpFold.C, 7, 5, 3));

        await OkAsync(await TlApproveAsync(org.Tl.C, mine.Id));

        var otherLedger = await LedgerAsync(org.Admin, org.EmpFold.Id);
        Assert.Empty(otherLedger.Entries.Where(e => e.RelatedRequestId == other.Id));
        Assert.Equal(365, otherLedger.AnnualLeave.Remaining);

        var otherState = await GetAsync(org.EmpFold.C, other.Id);
        Assert.Equal(LeaveRequestStatus.Submitted, otherState.Status);
    }
}

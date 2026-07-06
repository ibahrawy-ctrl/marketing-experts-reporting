using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RPT-WORKFLOW-BOTTLENECKS-R1 — اختناقات مسار الاعتماد (قراءة فقط).
/// تعريف العالق: حالة انتظار اعتماد + CurrentApproverId محدَّد + خطوة Pending قائمة.
/// عمر المرحلة = الآن − أحدث (أعلى Level) خطوة Pending.CreatedAtUtc.
/// SLA: قائد فريق=24h، مدير=48h، الإدارة العليا (GM/CEO/Admin/CeoSupport)=72h.
/// النطاق عبر ScopeResolver وحده (لا سياسة جديدة): Admin/CEO/GM=الكل؛ Manager=شجرته؛ TeamLeader=فريقه؛ الموظف=نفسه.
/// </summary>
[Collection("Integration")]
public class WorkflowBottlenecksTests
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkflowBottlenecksTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) تقرير بانتظار اعتماد وله CurrentApproverId + خطوة Pending ⇒ يظهر في التفاصيل (Admin يرى الكل).
    [Fact]
    public async Task PendingWithApprover_AppearsInDetails()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 5);

        var report = await GetDetailsAsync(admin);
        Assert.Contains(report.Rows, r => r.SubmissionId == subId);
    }

    // (2) المسودّة (Draft) مستبعَدة — لم تُرسَل بعد.
    [Fact]
    public async Task Draft_Excluded()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        // Draft عادةً بلا معتمِد؛ نضعه معتمِدًا + خطوة لإثبات أن استبعاده بسبب الحالة لا غياب البيانات.
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Draft, stepAgeHours: 5);

        var report = await GetDetailsAsync(admin);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == subId);
    }

    // (3) المُعادة (Returned) مع CurrentApproverId=null لا تُحتسب اختناق اعتماد.
    [Fact]
    public async Task ReturnedWithoutApprover_NotCounted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId: null, SubmissionStatus.Returned, stepAgeHours: null);

        var report = await GetDetailsAsync(admin);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == subId);
    }

    // (4) المُغلق (Closed) مستبعَد — انتهى مساره.
    [Fact]
    public async Task Closed_Excluded()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Closed, stepAgeHours: 100);

        var report = await GetDetailsAsync(admin);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == subId);
    }

    // (5) عمر المرحلة يعتمد ApprovalStep.CreatedAtUtc لا SubmittedAtUtc.
    [Fact]
    public async Task StageAge_UsesApprovalStepCreatedAtUtc()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        // الإرسال حديث (ساعة) لكن خطوة الاعتماد قديمة (30 ساعة) ⇒ العمر يجب أن يقارب 30 لا 1.
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted,
            stepAgeHours: 30, submittedAgeHours: 1);

        var report = await GetDetailsAsync(admin);
        var row = report.Rows.Single(r => r.SubmissionId == subId);
        Assert.True(row.AgeHours >= 29 && row.AgeHours <= 31, $"AgeHours={row.AgeHours} يجب أن يقارب 30");
    }

    // (6أ) قائد فريق: خطوة عمرها 30h > SLA 24h ⇒ overdue، والمرحلة team_leader.
    [Fact]
    public async Task Sla_TeamLeader_24h_Overdue()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 30);

        var row = (await GetDetailsAsync(admin)).Rows.Single(r => r.SubmissionId == subId);
        Assert.Equal("team_leader", row.StageKey);
        Assert.Equal(24, row.SlaHours);
        Assert.True(row.IsOverdue);
    }

    // (6ب) مدير: خطوة عمرها 30h < SLA 48h ⇒ ليست overdue، والمرحلة manager.
    [Fact]
    public async Task Sla_Manager_48h_NotOverdueAt30h()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 30);

        var row = (await GetDetailsAsync(admin)).Rows.Single(r => r.SubmissionId == subId);
        Assert.Equal("manager", row.StageKey);
        Assert.Equal(48, row.SlaHours);
        Assert.False(row.IsOverdue);
    }

    // (6ج) الإدارة العليا (GM): خطوة عمرها 80h > SLA 72h ⇒ overdue، والمرحلة senior_management.
    [Fact]
    public async Task Sla_SeniorManagement_72h_OverdueAt80h()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 80);

        var row = (await GetDetailsAsync(admin)).Rows.Single(r => r.SubmissionId == subId);
        Assert.Equal("senior_management", row.StageKey);
        Assert.Equal(72, row.SlaHours);
        Assert.True(row.IsOverdue);
    }

    // (7) قائد فريق لا يرى تقريرًا لموظف خارج فريقه (نطاق team = ManagerId == TL).
    [Fact]
    public async Task TeamLeader_CannotSeeOutsideTeam()
    {
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, otherMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, inTeamId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: tlId);
        var (_, outTeamId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: otherMgrId);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);

        var inSub = await SeedSubmissionAsync(inTeamId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);
        var outSub = await SeedSubmissionAsync(outTeamId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(tl);
        Assert.Contains(report.Rows, r => r.SubmissionId == inSub);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == outSub);
    }

    // (8) مدير لا يرى تقريرًا خارج نطاقه (تابع لمدير آخر).
    [Fact]
    public async Task Manager_CannotSeeOutsideScope()
    {
        var (mgr, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, otherMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, inId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: mId);
        var (_, outId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: otherMgrId);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);

        var inSub = await SeedSubmissionAsync(inId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);
        var outSub = await SeedSubmissionAsync(outId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(mgr);
        Assert.Contains(report.Rows, r => r.SubmissionId == inSub);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == outSub);
    }

    // (9) Admin يرى الكل (موظف بلا مدير اختباري ضمن نطاقه).
    [Fact]
    public async Task Admin_SeesAll()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subId = await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(admin);
        Assert.Contains(report.Rows, r => r.SubmissionId == subId);
    }

    // (10أ) فلتر approverId يحصر التفاصيل بالمعتمِد المحدَّد.
    [Fact]
    public async Task Details_RespectsApproverFilter()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverA) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, approverB) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, s1) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, s2) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subA = await SeedSubmissionAsync(s1, approverA, SubmissionStatus.Submitted, stepAgeHours: 10);
        var subB = await SeedSubmissionAsync(s2, approverB, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(admin, approverId: approverA);
        Assert.Contains(report.Rows, r => r.SubmissionId == subA);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == subB);
    }

    // (10ب) فلتر stage يحصر التفاصيل بمرحلة محدَّدة.
    [Fact]
    public async Task Details_RespectsStageFilter()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, tlApprover) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, mgrApprover) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, s1) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, s2) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var tlSub = await SeedSubmissionAsync(s1, tlApprover, SubmissionStatus.Submitted, stepAgeHours: 10);
        var mgrSub = await SeedSubmissionAsync(s2, mgrApprover, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(admin, stage: "manager");
        Assert.Contains(report.Rows, r => r.SubmissionId == mgrSub);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == tlSub);
    }

    // (10ج) فلتر overdueOnly يستبعد غير المتأخّر.
    [Fact]
    public async Task Details_RespectsOverdueOnlyFilter()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, mgrApprover) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, s1) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, s2) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var overdueSub = await SeedSubmissionAsync(s1, mgrApprover, SubmissionStatus.Submitted, stepAgeHours: 60); // > 48
        var freshSub = await SeedSubmissionAsync(s2, mgrApprover, SubmissionStatus.Submitted, stepAgeHours: 5);   // < 48

        var report = await GetDetailsAsync(admin, overdueOnly: true);
        Assert.Contains(report.Rows, r => r.SubmissionId == overdueSub);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == freshSub);
    }

    // (11) الحارس: غير المصادَق = 401 على المسارات الأربعة.
    [Fact]
    public async Task Anonymous_401()
    {
        var anon = _factory.CreateClient();
        foreach (var path in new[]
        {
            "/api/reports/workflow-bottlenecks/summary",
            "/api/reports/workflow-bottlenecks/by-stage",
            "/api/reports/workflow-bottlenecks/by-approver",
            "/api/reports/workflow-bottlenecks/details"
        })
        {
            var res = await anon.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
    }

    // (12) الملخّص + by-stage + by-approver متّسقة مع التفاصيل لمعتمِد واحد عالق.
    [Fact]
    public async Task Summary_ByStage_ByApprover_Consistent()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, submitterId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SeedSubmissionAsync(submitterId, approverId, SubmissionStatus.Submitted, stepAgeHours: 30);

        var summaryRes = await admin.GetAsync("/api/reports/workflow-bottlenecks/summary");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);
        var summary = await summaryRes.ReadAsync<WorkflowBottlenecksSummaryReport>();
        Assert.NotNull(summary);
        Assert.True(summary!.TotalPending >= 1);
        Assert.True(summary.OverduePending >= 1);

        var byStageRes = await admin.GetAsync("/api/reports/workflow-bottlenecks/by-stage");
        Assert.Equal(HttpStatusCode.OK, byStageRes.StatusCode);
        var byStage = await byStageRes.ReadAsync<WorkflowBottlenecksByStageReport>();
        Assert.Contains(byStage!.Rows, r => r.StageKey == "team_leader");

        var byApproverRes = await admin.GetAsync("/api/reports/workflow-bottlenecks/by-approver");
        Assert.Equal(HttpStatusCode.OK, byApproverRes.StatusCode);
        var byApprover = await byApproverRes.ReadAsync<WorkflowBottlenecksByApproverReport>();
        Assert.Contains(byApprover!.Rows, r => r.ApproverId == approverId);
    }

    // (13) الموظف يرى تقريره العالق فقط (نطاق own) ولا يرى تقرير غيره.
    [Fact]
    public async Task Employee_SeesOwnStuckOnly()
    {
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, otherId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, approverId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var ownSub = await SeedSubmissionAsync(empId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);
        var otherSub = await SeedSubmissionAsync(otherId, approverId, SubmissionStatus.Submitted, stepAgeHours: 10);

        var report = await GetDetailsAsync(emp);
        Assert.Contains(report.Rows, r => r.SubmissionId == ownSub);
        Assert.DoesNotContain(report.Rows, r => r.SubmissionId == otherSub);
    }

    // ===== أدوات مساعدة =====

    private async Task<WorkflowBottlenecksDetailsReport> GetDetailsAsync(
        HttpClient client, string? stage = null, Guid? departmentId = null, Guid? teamId = null,
        Guid? approverId = null, bool overdueOnly = false)
    {
        var qs = new List<string>();
        if (stage is not null) qs.Add($"stage={stage}");
        if (departmentId is not null) qs.Add($"departmentId={departmentId}");
        if (teamId is not null) qs.Add($"teamId={teamId}");
        if (approverId is not null) qs.Add($"approverId={approverId}");
        if (overdueOnly) qs.Add("overdueOnly=true");
        var url = "/api/reports/workflow-bottlenecks/details" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<WorkflowBottlenecksDetailsReport>();
        Assert.NotNull(report);
        return report!;
    }

    /// <summary>
    /// يبذر قالبًا + إصدارًا + تسليمًا (+ خطوة Pending اختيارية) مباشرةً عبر AppDbContext للتحكّم الكامل
    /// في الحالة وعمر المرحلة. stepAgeHours=null ⇒ بلا خطوة. submittedAgeHours يتحكّم في SubmittedAtUtc.
    /// </summary>
    private async Task<Guid> SeedSubmissionAsync(
        Guid submitterId, Guid? approverId, SubmissionStatus status, double? stepAgeHours,
        double submittedAgeHours = 2, Guid? teamId = null, Guid? deptId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tmpl = new ReportTemplate { Title = $"قالب اختناق {Guid.NewGuid():N}", OwnerId = submitterId, IsActive = true };
        db.ReportTemplates.Add(tmpl);
        var ver = new ReportTemplateVersion { ReportTemplateId = tmpl.Id, VersionNumber = 1, IsPublished = true };
        db.ReportTemplateVersions.Add(ver);

        var sub = new ReportSubmission
        {
            ReportTemplateVersionId = ver.Id,
            SubmitterId = submitterId,
            TeamId = teamId,
            DepartmentId = deptId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = $"2026-W{Guid.NewGuid():N}".Substring(0, 12),
            Status = status,
            SubmittedAtUtc = DateTime.UtcNow.AddHours(-submittedAgeHours),
            CurrentApproverId = approverId
        };
        db.ReportSubmissions.Add(sub);

        if (stepAgeHours is not null && approverId is not null)
        {
            db.ApprovalSteps.Add(new ApprovalStep
            {
                ReportSubmissionId = sub.Id,
                Level = 1,
                ApproverId = approverId.Value,
                Status = ApprovalStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-stepAgeHours.Value)
            });
        }

        await db.SaveChangesAsync();
        return sub.Id;
    }
}

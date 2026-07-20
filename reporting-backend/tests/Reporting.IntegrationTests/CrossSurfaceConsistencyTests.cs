using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Calendar;
using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — اتّساق عبر السطوح (Cross-Surface Consistency).
/// موظّف واحد + تاريخ واحد يجب أن يُنتج «حقيقة أسبوعية واحدة» عبر كل السطوح:
/// my-cycles (شارة الموظّف)، due/my-status، pending-reports (مصدر عدّادات الداشبورد)،
/// due/overview لقائد الفريق (حالة الفريق). الأسبوع الحاليّ مُسلَّم ⇒ كلّها «مُسلَّم/لا إجراء»؛
/// الأسبوع الماضي بلا تسليم ⇒ كلّها «متأخّر/إجراء مطلوب». قراءة فقط — لا تعديل/هجرة/كتابة إنتاج.
/// </summary>
[Collection("Integration")]
public class CrossSurfaceConsistencyTests
{
    private readonly CustomWebApplicationFactory _factory;
    public CrossSurfaceConsistencyTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string CurrentWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());
    private static string PastWeekKey() => ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday().AddDays(-21));

    /// <summary>يجعل المستخدم مطالَبًا بتقرير أسبوعيّ عبر مسمّى + قالب أساسي منشور، بأرضيّة قبل الأسابيع المختبَرة.</summary>
    private async Task<Guid> SetupReportingRoleAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = null };
        db.JobRoles.Add(jobRole);

        var template = new ReportTemplate
        {
            Title = $"قالب {Guid.NewGuid():N}",
            JobRoleId = jobRole.Id,
            Classification = TemplateClassification.Primary,
            DefaultPeriodType = PeriodType.Weekly,
            IsActive = true,
            Status = TemplateStatus.Published,
            OwnerId = userId
        };
        db.ReportTemplates.Add(template);

        var floorAnchor = DateTime.UtcNow.AddDays(-60);
        var version = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = floorAnchor
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        if (user.CreatedAtUtc > floorAnchor) user.CreatedAtUtc = floorAnchor;
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task InsertSubmissionAsync(Guid versionId, Guid submitterId, string periodKey, SubmissionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = periodKey,
            Status = status,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ===== الأسبوع الحاليّ مُسلَّم: كل السطوح تتّفق على «مُسلَّم / لا إجراء» =====
    [Fact]
    public async Task CurrentWeekSubmitted_AllSurfaces_AgreeSubmittedNoAction()
    {
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", leaderId);
        var versionId = await SetupReportingRoleAsync(memberId);
        var curKey = CurrentWeekKey();
        await InsertSubmissionAsync(versionId, memberId, curKey, SubmissionStatus.Submitted);

        // (1) my-status (الموظّف): مُسلَّم، غير متأخّر، لا تأخّر.
        var myStatus = await (await member.GetAsync($"/api/reports/due/my-status?weekKey={curKey}"))
            .ReadAsync<ReportDueMyStatus>();
        Assert.NotNull(myStatus);
        Assert.True(myStatus!.Submitted);
        Assert.False(myStatus.IsOverdue);
        Assert.Equal(DelayType.NoDelay, myStatus.DelayType);

        // (2) my-cycles (شارة الموظّف): الدورة الحاليّة تحمل تسليمًا وليست حالة إجراء للموظّف.
        var cycles = await (await member.GetAsync("/api/reporting-calendar/my-cycles?past=6&future=1"))
            .ReadAsync<MyCyclesDto>();
        var current = cycles!.Cycles.Single(c => c.IsCurrent);
        Assert.NotNull(current.Unified);
        Assert.True(current.Unified!.HasSubmission);
        Assert.False(current.Unified.IsLate);
        Assert.DoesNotContain(current.Unified.UnifiedStatus, new[]
        {
            UnifiedCycleStatus.DueNow, UnifiedCycleStatus.Draft, UnifiedCycleStatus.OverdueDraft,
            UnifiedCycleStatus.OverdueNotSubmitted, UnifiedCycleStatus.ReturnedForChanges, UnifiedCycleStatus.OverdueReturned
        });

        // (3) pending-reports للموظّف نفسه (مصدر عدّادات الداشبورد): لا يظهر تقرير الأسبوع الحاليّ (لا إجراء).
        var myPending = await (await member.GetAsync($"/api/dashboard/pending-reports?periodKey={curKey}"))
            .ReadAsync<List<PendingReportDto>>();
        Assert.NotNull(myPending);
        Assert.DoesNotContain(myPending!, p => p.SubmitterId == memberId && p.PeriodKey == curKey);

        // (4) due/overview لقائد الفريق (حالة الفريق): العضو ليس ضمن التأخّر/النقص لهذا الأسبوع.
        var overview = await (await leader.GetAsync($"/api/reports/due/overview?weekKey={curKey}"))
            .ReadAsync<ReportDueOverview>();
        Assert.NotNull(overview);
        Assert.Equal("team", overview!.ScopeType);
        Assert.DoesNotContain(overview.Items, r => r.UserId == memberId
            && r.DelayType == DelayType.EmployeeReportNotSubmitted);

        // (5) pending-reports بنطاق القائد: العضو ليس بندًا معلّقًا هذا الأسبوع (اتّساق عدّادات الفريق).
        var teamPending = await (await leader.GetAsync($"/api/dashboard/pending-reports?periodKey={curKey}"))
            .ReadAsync<List<PendingReportDto>>();
        Assert.DoesNotContain(teamPending!, p => p.SubmitterId == memberId && p.PeriodKey == curKey);
    }

    // ===== الأسبوع الماضي بلا تسليم: كل السطوح تتّفق على «متأخّر / إجراء مطلوب» =====
    [Fact]
    public async Task PastWeekNoSubmission_AllSurfaces_AgreeOverdueActionRequired()
    {
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", leaderId);
        await SetupReportingRoleAsync(memberId);
        var pastKey = PastWeekKey();

        // (1) my-status (الموظّف): متأخّر، غير مُسلَّم، نوع تأخّر = تقرير موظّف غير مُسلَّم.
        var myStatus = await (await member.GetAsync($"/api/reports/due/my-status?weekKey={pastKey}"))
            .ReadAsync<ReportDueMyStatus>();
        Assert.NotNull(myStatus);
        Assert.False(myStatus!.Submitted);
        Assert.True(myStatus.IsOverdue);
        Assert.Equal(DelayType.EmployeeReportNotSubmitted, myStatus.DelayType);

        // (2) pending-reports للموظّف نفسه: يظهر بند non-starter (بلا تسليم) بحالة متأخّر غير مُسلَّم.
        var myPending = await (await member.GetAsync($"/api/dashboard/pending-reports?periodKey={pastKey}"))
            .ReadAsync<List<PendingReportDto>>();
        var mine = Assert.Single(myPending!, p => p.SubmitterId == memberId && p.PeriodKey == pastKey);
        Assert.Null(mine.SubmissionId);          // non-starter بلا تسليم
        Assert.False(mine.HasSubmission);
        Assert.Equal(nameof(ExpectedSubmissionStatus.OverdueNotSubmitted), mine.Status);

        // (3) due/overview لقائد الفريق: العضو مُدرَج كتقرير موظّف غير مُسلَّم، وعدّاد التأخّر ≥ 1.
        var overview = await (await leader.GetAsync($"/api/reports/due/overview?weekKey={pastKey}"))
            .ReadAsync<ReportDueOverview>();
        Assert.NotNull(overview);
        Assert.Equal("team", overview!.ScopeType);
        Assert.True(overview.OverdueReportsCount >= 1);
        Assert.Contains(overview.Items, r => r.UserId == memberId
            && r.DelayType == DelayType.EmployeeReportNotSubmitted);

        // (4) pending-reports بنطاق القائد: نفس العضو غير-البادئ يظهر (اتّساق عدّادات الفريق مع القائمة).
        var teamPending = await (await leader.GetAsync($"/api/dashboard/pending-reports?periodKey={pastKey}"))
            .ReadAsync<List<PendingReportDto>>();
        Assert.Contains(teamPending!, p => p.SubmitterId == memberId && p.PeriodKey == pastKey
            && !p.HasSubmission && p.Status == nameof(ExpectedSubmissionStatus.OverdueNotSubmitted));
    }
}

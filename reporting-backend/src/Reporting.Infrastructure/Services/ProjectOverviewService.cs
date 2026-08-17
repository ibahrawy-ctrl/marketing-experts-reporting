using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// لوحة النظرة العامّة للمشروع (CPW-R3 · W4 · §13 + §14 + §15).
///
/// <para>
/// **رحلة تطبيق واحدة بعدد استعلامات ثابت = 12** مهما بلغ عدد الأهداف أو المؤشّرات أو المخرَجات
/// أو المسنَدين (الاستعلامان الأخيران: أسماء المسنَدين الثلاثة دفعةً واحدة + اسم الفريق المالك):
/// <list type="number">
///   <item><description>نواة المشروع + اسم العميل (استعلام واحد بضمّ).</description></item>
///   <item><description>الأهداف — عبر <see cref="ProjectObjectiveService.BuildAsync"/> (3 استعلامات ثابتة داخله).</description></item>
///   <item><description>مؤشّرات المشروع النشطة (استعلام واحد يخدم تجميع المؤشّرات ومكوّن الصحّة معًا).</description></item>
///   <item><description>المخرَجات التعاقديّة النشطة (استعلام واحد).</description></item>
///   <item><description>المخاطر · القرارات · الملاحظات الإداريّة (استعلام لكلّ منها).</description></item>
///   <item><description>الاستراتيجيّة النشطة مع عدد سماتها (استعلام واحد بعدّ فرعيّ).</description></item>
/// </list>
/// لا استعلام داخل أيّ حلقة، ولا <c>Include</c> متشعّب، وكلّ التجميعات تقع في الذاكرة على صفوف مجلوبة مرّة (§15).
/// </para>
///
/// <para>
/// **قراءة خالصة — لا كتابة في GET**: الصحّة تُحتسَب لحظيًّا من <see cref="ProjectHealthPolicy"/>
/// و**لا تُكتَب** إلى <c>HealthStatus</c>/<c>HealthPercent</c>/<c>HealthComputedAtUtc</c> من هنا.
/// الكتابة تقع حصرًا في <see cref="ProjectHealthService"/> عند الطفرات المؤثِّرة وعند
/// <c>POST /health/recompute</c>. الختم **المخزَّن** يُعرَض في <c>PersistedAtUtc</c> بلا تعديله.
/// </para>
///
/// <para>
/// **مكوّن المؤشّرات موزون على مستويين** (DEC-W4-03) لا مسطَّحًا: نتيجة كلّ هدف تُحسَب من مؤشّراته
/// ثمّ تُرجَّح بأوزان الأهداف. مثال الحَكَم يعطي **65** لا 66.67.
/// </para>
///
/// <para>
/// **رقم تقدّم واحد وحالة صحّة واحدة** (P360-WF-R2 · GAP-21): أُزيلت النسبة الثانية التي كانت
/// تُحسَب هنا داخل بطاقة المخرَجات، ودخلت الإشارات التشغيليّة (معطِّل/متأخّر/خطر عالٍ) في احتساب
/// الصحّة عبر **نفس** بانِي المسار الكاتب — فلا تختلف اللوحة عن العمود المخزَّن ولا عن القائمة.
/// </para>
/// </summary>
public class ProjectOverviewService : IProjectOverviewService
{
    private readonly AppDbContext _db;
    private readonly IProject360Authorization _auth;
    private readonly ProjectObjectiveService _objectives;

    public ProjectOverviewService(AppDbContext db, IProject360Authorization auth, ProjectObjectiveService objectives)
    {
        _db = db;
        _auth = auth;
        _objectives = objectives;
    }

    public async Task<Result<ProjectOverviewDto>> GetProjectOverviewAsync(Guid projectId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectOverviewDto>.Failure(scope.Error!, scope.ErrorCode);

        // (1) نواة المشروع + اسم العميل + ختم الصحّة **المخزَّن** — استعلام واحد كما هو.
        var coreRow = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                Core = new ProjectOverviewCoreDto(
                    p.Id, p.Name, p.ClientId, p.Client!.Name, p.ServiceType, p.Status,
                    p.StartDate, p.EndDate, p.ProgressPercent, p.Summary,
                    p.ProjectOwnerId, p.TeamLeaderId, p.AccountManagerId, p.OwnerTeamId,
                    // الأسماء تُملأ بعد الاستعلام (11) — لا خاصّيّات تنقّل لهذه المراجع الثلاثة عمدًا.
                    null, null, null, null,
                    p.ProgressMode, p.ProgressCalculatedAtUtc, p.ProgressSourceDeliverableCount),
                p.HealthComputedAtUtc,
            })
            .FirstOrDefaultAsync(ct);
        if (coreRow is null)
            return Result<ProjectOverviewDto>.Failure("المشروع غير موجود.", Project360ErrorCodes.ProjectNotFound);
        var core = coreRow.Core;

        // (2-4) الأهداف بتقدّمها المشتقّ
        var objectives = await _objectives.BuildAsync(projectId, includeInactive: false, ct);

        // (5) المؤشّرات النشطة — صفوف خام تخدم التجميع ومكوّن الصحّة معًا
        var kpiRows = await _db.ProjectKpis.AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive)
            .Select(k => new { k.Weight, k.CurrentValue, k.TargetValue, k.Direction, k.LastReadingDate })
            .ToListAsync(ct);

        // (6) المخرَجات التعاقديّة النشطة — بالوزن وتاريخ الاستحقاق أيضًا، لأنّ نفس الصفوف
        // تخدم عدّادات البطاقة **وإشارات التأخّر** التي تحكم الحالة الظاهرة (§7).
        var deliverableRows = await _db.ProjectDeliverables.AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.IsActive)
            .Select(d => new { d.Status, d.ProgressPercent, d.WeightPercentage, d.DueDate })
            .ToListAsync(ct);

        // (7-9) الحوكمة — إعادة استخدام كامل للكيانات القائمة بلا أيّ تعديل عليها.
        // الشدّة تُجلَب مع الحالة: منها يُشتقّ «معطَّل» (Critical مفتوح) و«معرَّض للخطر» (High مفتوح).
        var riskRows = await _db.Risks.AsNoTracking()
            .Where(r => r.ProjectId == projectId).Select(r => new { r.Status, r.Severity }).ToListAsync(ct);
        var decisionStatuses = await _db.Decisions.AsNoTracking()
            .Where(d => d.ProjectId == projectId).Select(d => d.Status).ToListAsync(ct);
        var noteStatuses = await _db.ManagementNotes.AsNoTracking()
            .Where(n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == projectId)
            .Select(n => n.Status).ToListAsync(ct);

        // (10) الاستراتيجيّة النشطة + عدد سماتها
        var strategy = await _db.ProjectStrategies.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .Select(s => new
            {
                Filled = new[]
                {
                    s.Vision, s.StrategySummary, s.TargetAudience, s.CustomerPersona,
                    s.Positioning, s.ValueProposition, s.Competitors, s.ToneOfVoice,
                    s.Messaging, s.MarketingApproach, s.SuccessFactors,
                },
                AttributesCount = s.Attributes.Count,
            })
            .FirstOrDefaultAsync(ct);

        // ===== التجميع في الذاكرة =====

        var achievements = kpiRows
            .Select(k => new ProjectHealthPolicy.WeightedAchievement(
                k.Weight, ProjectHealthPolicy.ComputeAchievement(k.CurrentValue, k.TargetValue, k.Direction)))
            .ToList();

        // DEC-W4-03 — ترجيح على مستويين: نتيجة كلّ هدف حُسِبت أصلًا داخل BuildAsync
        // (ProjectObjectiveDto.KpiScore)، فلا يبقى إلّا ترجيحها بأوزان الأهداف الخام.
        // **ممنوع** إعادة تمرير قائمة المؤشّرات المسطَّحة هنا — والنوع نفسه يمنعها.
        var kpiScore = ProjectHealthPolicy.RollUpObjectiveScores(
            objectives.Select(o => new ProjectHealthPolicy.WeightedAchievement(o.Weight, o.ProgressPercent)).ToList());

        var computed = achievements.Where(a => a.Achievement is not null).Select(a => a.Achievement!.Value).ToList();

        var kpis = new ProjectOverviewKpisDto(
            Total: kpiRows.Count,
            OnTrack: computed.Count(a => a >= ProjectHealthPolicy.GreenThreshold),
            AtRisk: computed.Count(a => a >= ProjectHealthPolicy.YellowThreshold && a < ProjectHealthPolicy.GreenThreshold),
            OffTrack: computed.Count(a => a < ProjectHealthPolicy.YellowThreshold),
            WithoutReadings: kpiRows.Count(k => k.LastReadingDate is null),
            // نتيجة المستويين نفسها لا متوسّطًا مسطَّحًا — لئلّا تعرض اللوحة رقمين متناقضين
            // لنفس المفهوم (مثال الحَكَم: 65 هنا، والمسطَّح الممنوع كان 66.67).
            AverageAchievementPercent: kpiScore.Score,
            LastReadingDate: kpiRows.Where(k => k.LastReadingDate is not null).Max(k => k.LastReadingDate));

        var objectivesDto = new ProjectOverviewObjectivesDto(
            Total: objectives.Count,
            NotStarted: objectives.Count(o => o.Status == ProjectObjectiveStatus.NotStarted),
            InProgress: objectives.Count(o => o.Status == ProjectObjectiveStatus.InProgress),
            AtRisk: objectives.Count(o => o.Status == ProjectObjectiveStatus.AtRisk),
            Completed: objectives.Count(o => o.Status == ProjectObjectiveStatus.Completed),
            // موزون بأوزان الأهداف (DEC-W4-03) لا متوسّطًا حسابيًّا — رقم واحد للمفهوم الواحد
            // عبر اللوحة كلّها: objectives.average == kpis.average == health.kpiScore.
            AverageAchievementPercent: kpiScore.Score,
            Items: objectives);

        var deliverables = new ProjectOverviewDeliverablesDto(
            Total: deliverableRows.Count,
            Completed: deliverableRows.Count(d => d.Status == ProjectDeliverableStatus.Delivered),
            Pending: deliverableRows.Count(d => d.Status is ProjectDeliverableStatus.NotStarted or ProjectDeliverableStatus.InProgress),
            Delayed: deliverableRows.Count(d => d.Status == ProjectDeliverableStatus.Delayed));

        var governance = new ProjectOverviewGovernanceDto(
            RisksTotal: riskRows.Count,
            RisksOpen: riskRows.Count(r => r.Status != RiskStatus.Closed),
            DecisionsTotal: decisionStatuses.Count,
            DecisionsPending: decisionStatuses.Count(s => s == DecisionStatus.Proposed),
            NotesTotal: noteStatuses.Count,
            NotesOpen: noteStatuses.Count(s => s == ManagementNoteStatus.Open));

        var strategyDto = new ProjectOverviewStrategyDto(
            Exists: strategy is not null,
            FilledCoreFields: strategy is null ? 0 : strategy.Filled.Count(v => !string.IsNullOrWhiteSpace(v)),
            TotalCoreFields: ProjectStrategyCoreFields.Count,
            AttributesCount: strategy?.AttributesCount ?? 0);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var scheduleScore = ProjectHealthPolicy.ComputeScheduleScore(
            core.StartDate, core.EndDate, today, core.ProgressPercent, core.Status);

        // **نفس بانِي الإشارات الذي يستعمله مسار الكتابة** (ProjectHealthService) بحرفه: لولاه
        // لأمكن أن تعرض اللوحة «أخضر» بينما الحالة المخزَّنة «معطَّل»، فيقرأ المدير طمأنينةً
        // من الشاشة ويقرأ الفلتر تعطّلًا من العمود — والرقمان من نفس النظام.
        var signals = ProjectHealthPolicy.BuildOperationalSignals(
            deliverableRows.Select(d => new ProjectHealthPolicy.DeliverableSignalInput(d.Status, d.DueDate)).ToList(),
            riskRows.Where(r => r.Status != RiskStatus.Closed).Select(r => r.Severity).ToList(),
            today,
            core.ProgressMode);

        var snapshot = ProjectHealthPolicy.ComputeHealth(
            kpiScore.Score,
            ProjectHealthPolicy.ToHealthProgressComponent(core.ProgressPercent, core.ProgressMode),
            scheduleScore, now, kpiScore.AllWeightsZero, signals);

        // نفس مُحوِّل المسار الآخر حرفيًّا — عقد صحّة واحد لا شكلان.
        // **قراءة خالصة**: لا كتابة على أعمدة الصحّة من مسار GET؛ الختم المخزَّن يُعرَض كما هو
        // في `PersistedAtUtc` ليعرف المستهلك متى جرى آخر احتساب **محفوظ**.
        var health = ProjectHealthService.Map(snapshot, coreRow.HealthComputedAtUtc);

        // (11) أسماء المسنَدين — استعلامان ثابتان (مستخدمون + فريق) لا استعلام لكلّ دور.
        var named = await ResolveAssigneeNamesAsync(core, ct);

        var access = await _auth.BuildCapabilitiesAsync(scope.Value!, ct);

        return Result<ProjectOverviewDto>.Success(new ProjectOverviewDto(
            named, health, objectivesDto, kpis, deliverables, governance, strategyDto, access));
    }

    /// <summary>
    /// يملأ أسماء مالك المشروع وقائد الفريق ومدير الحساب والفريق المالك. الأسماء تُعرَض للبشر،
    /// والمعرّفات تبقى كما هي للربط — فلا يُستبدَل أحدهما بالآخر.
    /// </summary>
    private async Task<ProjectOverviewCoreDto> ResolveAssigneeNamesAsync(
        ProjectOverviewCoreDto core, CancellationToken ct)
    {
        var personIds = new[] { core.ProjectOwnerId, core.TeamLeaderId, core.AccountManagerId }
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();

        var personNames = personIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking().Where(u => personIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        string? teamName = null;
        if (core.OwnerTeamId is Guid teamId)
            teamName = await _db.Teams.AsNoTracking().Where(t => t.Id == teamId)
                .Select(t => t.NameAr).FirstOrDefaultAsync(ct);

        string? Name(Guid? id) => id is Guid g && personNames.TryGetValue(g, out var n) ? n : null;

        return core with
        {
            ProjectOwnerName = Name(core.ProjectOwnerId),
            TeamLeaderName = Name(core.TeamLeaderId),
            AccountManagerName = Name(core.AccountManagerId),
            OwnerTeamName = teamName,
        };
    }
}

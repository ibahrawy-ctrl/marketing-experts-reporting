using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Periods;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P1-KPI-003 — التنفيذ الوحيد لمحرّك حساب KPI (انظر <see cref="IKpiCalculationService"/> للضمانات).
///
/// أهمّ ما يصحّحه مقارنةً بالمسار القديم:
/// <list type="number">
/// <item>المسار القديم في <c>KpiSummaryAsync</c> كان يرشّح بـ<c>TotalScore != null</c> فقط **بلا شرط Approved**،
/// ويُعيد صفًّا لكلّ تقييم لا لكلّ موظّف، فينهار في الواجهة إلى «أعلى درجة تاريخيّة لكلّ عضو».</item>
/// <item>المتوسّط القديم كان خامًّا على مستوى التقييمات ⇒ الموظّف كثير التقييمات يطغى. الآن توسيط ثنائي المرحلة.</item>
/// <item>لم تكن هناك تغطية ولا تمييز بين Missing والصفر. الآن كلاهما صريح في العقد.</item>
/// </list>
///
/// الأداء: التجميع لكلّ موظّف يجري **داخل قاعدة البيانات** (GroupBy مترجَم إلى SQL) ضمن مفاتيح
/// الدورات المحدودة بالفترة فقط — لا تحميل لكامل التاريخ ولا استعلام لكلّ موظّف (لا N+1).
/// </summary>
public class KpiCalculationService : IKpiCalculationService
{
    private readonly AppDbContext _db;
    private readonly IScopeResolver _scope;
    private readonly ICurrentUser _currentUser;
    private readonly IPeriodService _periods;
    private readonly ISystemClock _clock;
    private readonly KpiFeatureOptions _options;

    public KpiCalculationService(
        AppDbContext db,
        IScopeResolver scope,
        ICurrentUser currentUser,
        IPeriodService periods,
        ISystemClock clock,
        IOptions<KpiFeatureOptions> options)
    {
        _db = db;
        _scope = scope;
        _currentUser = currentUser;
        _periods = periods;
        _clock = clock;
        _options = options.Value;
    }

    // ===================== العقد التنظيميّ =====================

    public async Task<Result<KpiPerformanceDto>> GetPerformanceAsync(
        KpiAnalyticsQuery query, CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(query, ct);
        if (prepared.Error is { } failure) return Result<KpiPerformanceDto>.Failure(failure.Message, failure.Code);
        var ctx = prepared.Context!;

        var current = await BuildEmployeeAggregatesAsync(ctx, ctx.Period, ct);
        var previous = await BuildEmployeeAggregatesAsync(ctx, ctx.PreviousPeriod, ct);

        var employees = ctx.Roster
            .Select(u => BuildEmployeeDto(ctx, u, current, previous))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

        var company = BuildGroup(ctx, "Company", null, null, ctx.Roster, current, previous);

        var departments = ctx.Roster
            .Where(u => u.DepartmentId is not null)
            .GroupBy(u => u.DepartmentId!.Value)
            .Select(g => BuildGroup(ctx, "Department", g.Key, ctx.DepartmentNames.GetValueOrDefault(g.Key),
                g.ToList(), current, previous))
            .OrderBy(g => g.GroupName, StringComparer.Ordinal)
            .ToList();

        // الإدارة = متوسّط متوسّطات **الموظّفين** المنتمين إليها مباشرةً، لا متوسّط متوسّطات الفرق (§5.3).
        var teams = ctx.Roster
            .Where(u => u.TeamId is not null)
            .GroupBy(u => u.TeamId!.Value)
            .Select(g => BuildGroup(ctx, "Team", g.Key, ctx.TeamNames.GetValueOrDefault(g.Key),
                g.ToList(), current, previous))
            .OrderBy(g => g.GroupName, StringComparer.Ordinal)
            .ToList();

        return Result<KpiPerformanceDto>.Success(new KpiPerformanceDto(
            KpiPeriodResolvedDto.From(ctx.Period),
            KpiPeriodResolvedDto.From(ctx.PreviousPeriod),
            ctx.Cadence,
            ctx.Scope.ScopeType,
            company,
            departments,
            teams,
            employees,
            _clock.UtcNow.UtcDateTime));
    }

    // ===================== الترتيب =====================

    public async Task<Result<KpiRankingsDto>> GetRankingsAsync(
        KpiAnalyticsQuery query, int take = 5, CancellationToken ct = default)
    {
        if (take is < 1 or > 100) take = 5;

        var prepared = await PrepareAsync(query, ct);
        if (prepared.Error is { } failure) return Result<KpiRankingsDto>.Failure(failure.Message, failure.Code);
        var ctx = prepared.Context!;

        var current = await BuildEmployeeAggregatesAsync(ctx, ctx.Period, ct);
        var previous = await BuildEmployeeAggregatesAsync(ctx, ctx.PreviousPeriod, ct);

        // صفّ واحد لكلّ موظّف (الحبيبيّة مضمونة بالبناء: الفهرس مفتاحه UserId).
        var scored = ctx.Roster
            .Select(u => BuildEmployeeDto(ctx, u, current, previous))
            .Where(e => e.Measure.Value is not null)
            .ToList();

        var eligible = scored.Where(e => e.EligibleForRanking).ToList();
        var excluded = scored.Count - eligible.Count;

        // كسر التعادل المستقرّ: الدرجة، ثمّ التغطية الأعلى، ثمّ الاسم، ثمّ المعرّف (§5.7).
        var top = eligible
            .OrderByDescending(e => e.Measure.Value!.Value)
            .ThenByDescending(e => e.Measure.Coverage ?? 0m)
            .ThenBy(e => e.FullName, StringComparer.Ordinal)
            .ThenBy(e => e.UserId)
            .Take(take)
            .ToList();

        var needs = eligible
            .OrderBy(e => e.Measure.Value!.Value)
            .ThenByDescending(e => e.Measure.Coverage ?? 0m)
            .ThenBy(e => e.FullName, StringComparer.Ordinal)
            .ThenBy(e => e.UserId)
            .Take(take)
            .ToList();

        return Result<KpiRankingsDto>.Success(new KpiRankingsDto(
            KpiPeriodResolvedDto.From(ctx.Period),
            ctx.Cadence,
            ctx.Scope.ScopeType,
            top,
            needs,
            excluded,
            _options.MinimumCoverageForRanking,
            _clock.UtcNow.UtcDateTime));
    }

    // ===================== التفصيل (Drill-down) =====================

    public async Task<Result<KpiDrilldownDto>> GetDrilldownAsync(
        KpiAnalyticsQuery query, CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(query, ct);
        if (prepared.Error is { } failure) return Result<KpiDrilldownDto>.Failure(failure.Message, failure.Code);
        var ctx = prepared.Context!;

        var rows = await EligibleEvaluationsQuery(ctx, ctx.Period)
            .Select(x => new
            {
                x.e.Id, x.e.SubjectUserId, x.t.Title, x.t.Cadence,
                x.e.PeriodType, x.e.PeriodKey, x.e.Status, x.e.TotalScore, x.e.SubmittedAtUtc
            })
            .ToListAsync(ct);

        var names = ctx.Roster.ToDictionary(u => u.UserId, u => u.FullName);
        var dtoRows = rows
            .Select(r =>
            {
                var (ws, we) = ReportingCalendarPolicy.IsValidCycleKey(r.PeriodKey)
                    ? ReportingCalendarPolicy.CycleRange(r.PeriodKey)
                    : (ctx.Period.Start, ctx.Period.End);
                return new KpiDrilldownRowDto(
                    r.Id, r.SubjectUserId, names.GetValueOrDefault(r.SubjectUserId, string.Empty),
                    r.Title, r.Cadence, r.PeriodType, r.PeriodKey, ws, we, r.Status, r.TotalScore, r.SubmittedAtUtc);
            })
            .OrderBy(r => r.SubjectName, StringComparer.Ordinal)
            .ThenBy(r => r.PeriodKey, StringComparer.Ordinal)
            .ToList();

        // إعادة إنتاج الرقم من الصفوف المُعادة نفسها بنفس التوسيط الثنائي — إثبات قابليّة التدقيق.
        var recomputed = KpiScorePolicy.GroupScore(
            dtoRows.Where(r => r.TotalScore is not null)
                .GroupBy(r => r.SubjectUserId)
                .Select(g => g.Average(x => x.TotalScore!.Value)));

        return Result<KpiDrilldownDto>.Success(new KpiDrilldownDto(
            KpiPeriodResolvedDto.From(ctx.Period),
            ctx.Cadence,
            query.SubjectUserId,
            KpiScorePolicy.Round(recomputed),
            dtoRows.Count,
            dtoRows,
            _clock.UtcNow.UtcDateTime));
    }

    // ===================== التحضير المشترك (نطاق + فترة + كادنس + قائمة الموظّفين) =====================

    private sealed record Failure(string Message, string Code);

    private sealed record Prepared(CalculationContext? Context, Failure? Error);

    private sealed record RosterUser(Guid UserId, string FullName, Guid? TeamId, Guid? DepartmentId);

    private sealed class CalculationContext
    {
        public required ScopeContext Scope { get; init; }
        public required ResolvedPeriod Period { get; init; }
        public required ResolvedPeriod PreviousPeriod { get; init; }
        public required KpiCadence Cadence { get; init; }
        public required IReadOnlyList<RosterUser> Roster { get; init; }
        public required IReadOnlyDictionary<Guid, string> TeamNames { get; init; }
        public required IReadOnlyDictionary<Guid, string> DepartmentNames { get; init; }
        public required decimal BelowTargetThreshold { get; init; }
        public required string ThresholdSource { get; init; }
    }

    private async Task<Prepared> PrepareAsync(KpiAnalyticsQuery query, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return new Prepared(null, new Failure("غير مصرّح.", "auth.unauthenticated"));

        // B-3 — الكادنس إلزاميّ صراحةً: لا افتراض ولا سقوط صامت بين النبض الأسبوعيّ والربع سنويّ.
        if (query.Cadence is not KpiCadence cadence)
            return new Prepared(null, new Failure(
                "يجب تحديد دوريّة التقييم صراحةً (WeeklyPulse أو Quarterly).", "kpi.cadence_required"));

        var resolved = _periods.Resolve(new PeriodRequest(query.PeriodType, query.PeriodKey, query.From, query.To));
        if (!resolved.Succeeded) return new Prepared(null, new Failure(resolved.Error!, resolved.ErrorCode!));
        var period = resolved.Value!;

        var scope = await _scope.ResolveAsync(ct);

        // فرض النطاق على الموظّف المطلوب: خارج النطاق ⇒ لا يُسرَّب وجوده (404 على مستوى المتحكّم).
        if (query.SubjectUserId is Guid subject && !scope.Contains(subject))
            return new Prepared(null, new Failure("غير موجود.", "kpi.not_found"));

        var roster = await BuildRosterAsync(scope, query, ct);

        var teamIds = roster.Where(u => u.TeamId is not null).Select(u => u.TeamId!.Value).Distinct().ToList();
        var deptIds = roster.Where(u => u.DepartmentId is not null).Select(u => u.DepartmentId!.Value).Distinct().ToList();

        var teamNames = teamIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var deptNames = deptIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);

        var (threshold, source) = await ResolveBelowTargetThresholdAsync(cadence, ct);

        return new Prepared(new CalculationContext
        {
            Scope = scope,
            Period = period,
            PreviousPeriod = _periods.PreviousComparable(period),
            Cadence = cadence,
            Roster = roster,
            TeamNames = teamNames,
            DepartmentNames = deptNames,
            BelowTargetThreshold = threshold,
            ThresholdSource = source
        }, null);
    }

    /// <summary>
    /// قائمة الموظّفين داخل نطاق الرؤية بعد تطبيق مرشّحات الإدارة/الفريق/الموظّف.
    /// وجودها ضروريّ لتمييز «لا بيانات» (موظّف بلا تقييم معتمَد) عن «صفر»، ولضمان صفّ واحد لكلّ موظّف.
    /// </summary>
    private async Task<IReadOnlyList<RosterUser>> BuildRosterAsync(
        ScopeContext scope, KpiAnalyticsQuery query, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(u => ids.Contains(u.Id));
        }
        if (query.SubjectUserId is Guid s) q = q.Where(u => u.Id == s);
        if (query.TeamId is Guid t) q = q.Where(u => u.TeamId == t);
        if (query.DepartmentId is Guid d) q = q.Where(u => u.DepartmentId == d);

        return await q
            .Select(u => new RosterUser(u.Id, u.FullName, u.TeamId, u.DepartmentId))
            .ToListAsync(ct);
    }

    // ===================== التجميع لكلّ موظّف (داخل قاعدة البيانات) =====================

    /// <summary>
    /// <paramref name="Expected"/> = الالتزامات قبل خصم الإعفاءات، و<paramref name="AdjustedExpected"/> بعده.
    /// الفصل بينهما مقصود (§5.5): الفرق نفسه هو دليل أنّ الإجازة المعتمَدة خفّضت المقام ولم تعاقب الموظّف.
    /// </summary>
    private sealed record EmployeeAggregate(
        decimal? Score, int EligibleCount, int ExcludedByStatusCount, int Expected, int AdjustedExpected);

    /// <summary>
    /// استعلام التقييمات **المؤهّلة** لفترة وكادنس ونطاق: Approved + درجة غير فارغة + الكادنس المطلوب
    /// + مفتاح الدورة داخل الفترة. المحذوف مستبعَد تلقائيًّا بالمرشّح العامّ في <see cref="AppDbContext"/>.
    /// </summary>
    private IQueryable<EvaluationJoin> EligibleEvaluationsQuery(CalculationContext ctx, ResolvedPeriod period)
        => BaseEvaluationsQuery(ctx, period)
            .Where(x => x.e.Status == KpiEvaluationStatus.Approved && x.e.TotalScore != null);

    private IQueryable<EvaluationJoin> BaseEvaluationsQuery(CalculationContext ctx, ResolvedPeriod period)
    {
        var weekKeys = _periods.WeekKeysWithin(period);
        var userIds = ctx.Roster.Select(u => u.UserId).ToList();
        var cadence = ctx.Cadence;

        return from e in _db.KpiEvaluations.AsNoTracking()
               join v in _db.KpiTemplateVersions.AsNoTracking() on e.KpiTemplateVersionId equals v.Id
               join t in _db.KpiTemplates.AsNoTracking() on v.KpiTemplateId equals t.Id
               where t.Cadence == cadence
                     && userIds.Contains(e.SubjectUserId)
                     && weekKeys.Contains(e.PeriodKey)
               select new EvaluationJoin { e = e, t = t };
    }

    private sealed class EvaluationJoin
    {
        public required Domain.Entities.Kpi.KpiEvaluation e { get; init; }
        public required Domain.Entities.Kpi.KpiTemplate t { get; init; }
    }

    private async Task<IReadOnlyDictionary<Guid, EmployeeAggregate>> BuildEmployeeAggregatesAsync(
        CalculationContext ctx, ResolvedPeriod period, CancellationToken ct)
    {
        // التجميع يُترجَم إلى GROUP BY في SQL: صفّ واحد لكلّ موظّف بدل جلب كل التقييمات إلى الذاكرة.
        var approved = await BaseEvaluationsQuery(ctx, period)
            .GroupBy(x => x.e.SubjectUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Sum = g.Where(x => x.e.Status == KpiEvaluationStatus.Approved && x.e.TotalScore != null)
                       .Sum(x => (decimal?)x.e.TotalScore) ?? 0m,
                EligibleCount = g.Count(x => x.e.Status == KpiEvaluationStatus.Approved && x.e.TotalScore != null),
                ExcludedByStatus = g.Count(x => x.e.Status != KpiEvaluationStatus.Approved)
            })
            .ToListAsync(ct);

        var (baseExpected, adjustedExpected) = await BuildAdjustedExpectedAsync(ctx, period, ct);

        var result = new Dictionary<Guid, EmployeeAggregate>(ctx.Roster.Count);
        var byUser = approved.ToDictionary(a => a.UserId);

        foreach (var u in ctx.Roster)
        {
            byUser.TryGetValue(u.UserId, out var a);
            var eligibleCount = a?.EligibleCount ?? 0;
            result[u.UserId] = new EmployeeAggregate(
                KpiScorePolicy.EmployeePeriodScore(a?.Sum ?? 0m, eligibleCount),
                eligibleCount,
                a?.ExcludedByStatus ?? 0,
                baseExpected,
                adjustedExpected.GetValueOrDefault(u.UserId, 0));
        }

        return result;
    }

    /// <summary>
    /// المتوقَّع المعدَّل لكلّ موظّف (B-5): عدد الالتزامات داخل الفترة ناقصًا الالتزامات التي **تغطّيها بالكامل**
    /// إجازة معتمَدة نهائيًّا (<c>HrApproved</c>). التغطية الجزئيّة لا تُسقِط الالتزام كي لا يُلغى أسبوع كامل
    /// بسبب يوم إجازة واحد. الإجازة المعتمَدة تخفض المقام ⇒ لا تعاقب الموظّف على غياب مأذون.
    /// </summary>
    private async Task<(int BaseExpected, IReadOnlyDictionary<Guid, int> Adjusted)> BuildAdjustedExpectedAsync(
        CalculationContext ctx, ResolvedPeriod period, CancellationToken ct)
    {
        // النبض الأسبوعيّ: التزام لكلّ دورة داخل الفترة. الربع سنويّ: التزام واحد لكلّ ربع تغطّيه الفترة.
        var commitments = ctx.Cadence == KpiCadence.WeeklyPulse
            ? _periods.WeekKeysWithin(period)
                .Select(k => ReportingCalendarPolicy.CycleRange(k))
                .ToList()
            : QuarterWindowsWithin(period);

        var baseExpected = commitments.Count;
        var userIds = ctx.Roster.Select(u => u.UserId).ToList();

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == LeaveRequestStatus.HrApproved
                        && userIds.Contains(l.RequesterUserId)
                        && l.StartDate <= period.End
                        && l.EndDate >= period.Start)
            .Select(l => new { l.RequesterUserId, l.StartDate, l.EndDate })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, int>(ctx.Roster.Count);
        foreach (var u in ctx.Roster) result[u.UserId] = baseExpected;

        foreach (var g in leaves.GroupBy(l => l.RequesterUserId))
        {
            var exempt = commitments.Count(w => g.Any(l => l.StartDate <= w.Start && l.EndDate >= w.End));
            result[g.Key] = Math.Max(0, baseExpected - exempt);
        }

        return (baseExpected, result);
    }

    private static List<(DateOnly Start, DateOnly End)> QuarterWindowsWithin(ResolvedPeriod period)
    {
        var windows = new List<(DateOnly, DateOnly)>();
        var cursor = new DateOnly(period.Start.Year, (period.Start.Month - 1) / 3 * 3 + 1, 1);
        while (cursor <= period.End)
        {
            var q = (cursor.Month - 1) / 3 + 1;
            var (s, e) = ReportingCalendarPolicy.QuarterRange(cursor.Year, q);
            if (s <= period.End && e >= period.Start) windows.Add((s, e));
            cursor = cursor.AddMonths(3);
        }
        return windows;
    }

    // ===================== بناء عناصر العقد =====================

    private KpiEmployeeScoreDto BuildEmployeeDto(
        CalculationContext ctx,
        RosterUser user,
        IReadOnlyDictionary<Guid, EmployeeAggregate> current,
        IReadOnlyDictionary<Guid, EmployeeAggregate> previous)
    {
        var now = current.GetValueOrDefault(user.UserId, EmptyAggregate);
        var before = previous.GetValueOrDefault(user.UserId, EmptyAggregate);

        var measure = BuildMeasure(ctx, now, before);
        var eligible = KpiScorePolicy.EligibleForRanking(
            now.EligibleCount, now.AdjustedExpected, _options.MinimumCoverageForRanking);

        // Missing لا يُفترَض دون العتبة ولا فوقها: القيمة null ⇒ الحكم null (لا تلوين ولا إنذار كاذب).
        bool? belowTarget = now.Score is null ? null : now.Score.Value < ctx.BelowTargetThreshold;

        return new KpiEmployeeScoreDto(
            user.UserId,
            user.FullName,
            user.TeamId,
            user.TeamId is Guid t ? ctx.TeamNames.GetValueOrDefault(t) : null,
            user.DepartmentId,
            user.DepartmentId is Guid d ? ctx.DepartmentNames.GetValueOrDefault(d) : null,
            measure,
            eligible,
            belowTarget,
            ctx.BelowTargetThreshold,
            ctx.ThresholdSource);
    }

    private KpiGroupScoreDto BuildGroup(
        CalculationContext ctx,
        string groupType,
        Guid? groupId,
        string? groupName,
        IReadOnlyList<RosterUser> members,
        IReadOnlyDictionary<Guid, EmployeeAggregate> current,
        IReadOnlyDictionary<Guid, EmployeeAggregate> previous)
    {
        var now = AggregateGroup(members, current);
        var before = AggregateGroup(members, previous);

        return new KpiGroupScoreDto(
            groupType,
            groupId,
            groupName,
            BuildMeasure(ctx, now, before),
            members.Count(m => current.GetValueOrDefault(m.UserId, EmptyAggregate).Score is not null),
            members.Count);
    }

    /// <summary>
    /// المرحلة الثانية من التوسيط: متوسّط **متوسّطات** الأعضاء ذوي الدرجة، على قيم **غير مقرَّبة**
    /// منعًا للتقريب المزدوج. العدّادات تُجمَع للشفافيّة لا لتوليد المتوسّط.
    /// </summary>
    private static EmployeeAggregate AggregateGroup(
        IReadOnlyList<RosterUser> members, IReadOnlyDictionary<Guid, EmployeeAggregate> byUser)
    {
        var rows = members.Select(m => byUser.GetValueOrDefault(m.UserId, EmptyAggregate)).ToList();
        return new EmployeeAggregate(
            KpiScorePolicy.GroupScore(rows.Where(r => r.Score is not null).Select(r => r.Score!.Value)),
            rows.Sum(r => r.EligibleCount),
            rows.Sum(r => r.ExcludedByStatusCount),
            rows.Sum(r => r.Expected),
            rows.Sum(r => r.AdjustedExpected));
    }

    private KpiMeasureDto BuildMeasure(CalculationContext ctx, EmployeeAggregate now, EmployeeAggregate before)
    {
        var (delta, trend) = KpiScorePolicy.Trend(
            now.Score, before.Score, ctx.Period.IsOpen, _options.TrendDeltaThreshold);

        return new KpiMeasureDto(
            KpiScorePolicy.Round(now.Score),
            now.EligibleCount,
            now.Expected,
            now.AdjustedExpected,
            KpiScorePolicy.Round(KpiScorePolicy.Coverage(now.EligibleCount, now.AdjustedExpected)),
            KpiScorePolicy.MissingCount(now.EligibleCount, now.AdjustedExpected),
            now.ExcludedByStatusCount,
            KpiScorePolicy.DataQuality(now.EligibleCount, now.AdjustedExpected, _options.MinimumCoverageForRanking),
            KpiScorePolicy.Round(before.Score),
            KpiScorePolicy.Round(delta),
            trend);
    }

    private static readonly EmployeeAggregate EmptyAggregate = new(null, 0, 0, 0, 0);

    /// <summary>
    /// B-6 — مصدر العتبة: عتبة إصدار القالب المنشور للكادنس أوّلًا (مالك تاريخيّ لا يتغيّر بأثر رجعيّ)،
    /// ثمّ الإعداد المركزيّ احتياطيًّا. لا ثابت مبعثر في الخدمات أو الواجهة.
    /// </summary>
    private async Task<(decimal Threshold, string Source)> ResolveBelowTargetThresholdAsync(
        KpiCadence cadence, CancellationToken ct)
    {
        var fromVersion = await (
            from v in _db.KpiTemplateVersions.AsNoTracking()
            join t in _db.KpiTemplates.AsNoTracking() on v.KpiTemplateId equals t.Id
            where t.Cadence == cadence && t.IsActive && v.IsPublished && v.BelowTargetThreshold != null
            orderby v.PublishedAtUtc descending
            select v.BelowTargetThreshold).FirstOrDefaultAsync(ct);

        return fromVersion is decimal v2
            ? (v2, "kpiTemplateVersion")
            : (_options.DefaultBelowTargetThreshold, "centralConfiguration");
    }
}

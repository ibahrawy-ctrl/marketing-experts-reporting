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
    private readonly IKpiTemplateService _templates;
    private readonly KpiFeatureOptions _options;

    public KpiCalculationService(
        AppDbContext db,
        IScopeResolver scope,
        ICurrentUser currentUser,
        IPeriodService periods,
        ISystemClock clock,
        IKpiTemplateService templates,
        IOptions<KpiFeatureOptions> options)
    {
        _db = db;
        _scope = scope;
        _currentUser = currentUser;
        _periods = periods;
        _clock = clock;
        _templates = templates;
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
            ctx.RequestedCadence,
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
        var all = ctx.Roster.Select(u => BuildEmployeeDto(ctx, u, current, previous)).ToList();
        var scored = all.Where(e => e.Measure.Value is not null).ToList();

        var eligible = scored.Where(e => e.EligibleForRanking).ToList();

        // DEC-01/17 — المستبعَدون لضعف التغطية بأسمائهم وحالتهم، لا بعددهم وحده.
        var excludedRows = scored
            .Where(e => !e.EligibleForRanking)
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

        // DEC-01/5+18 — «لا يوجد تواتر أو قالب فعّال» حالة مستقلّة: لا تُخلَط بضعف التغطية.
        var notConfigured = all
            .Where(e => e.Measure.JourneyState == KpiJourneyState.CadenceNotConfigured)
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

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
            ctx.RequestedCadence,
            ctx.Scope.ScopeType,
            top,
            needs,
            excludedRows.Count,
            ctx.MinimumCoverage,
            _clock.UtcNow.UtcDateTime,
            excludedRows,
            notConfigured));
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

        // DEC-01/18 — التفصيل يعرض المقاس الكامل، لا الصفوف وحدها: Expected · AdjustedExpected ·
        // Completed · Missing · Coverage + الفترات المصدريّة (بما فيها الناقصة والمُعفاة).
        var aggregates = await BuildEmployeeAggregatesAsync(ctx, ctx.Period, ct);
        var previous = await BuildEmployeeAggregatesAsync(ctx, ctx.PreviousPeriod, ct);
        var commitments = await BuildCommitmentsAsync(ctx, ctx.Period, ct);

        KpiMeasureDto? measure = null;
        List<KpiSourcePeriodDto>? sourcePeriods = null;
        KpiCadence? effectiveCadence = null;
        var cadenceSource = KpiCadenceSources.NotConfigured;

        if (query.SubjectUserId is Guid subject && ctx.Roster.Any(u => u.UserId == subject))
        {
            effectiveCadence = ctx.CadenceOf(subject);
            cadenceSource = ctx.CadenceSourceOf(subject);
            measure = BuildMeasure(
                ctx,
                aggregates.GetValueOrDefault(subject, EmptyAggregate),
                previous.GetValueOrDefault(subject, EmptyAggregate),
                effectiveCadence is not null);

            var completedKeys = dtoRows.Where(r => r.SubjectUserId == subject)
                .Select(r => r.PeriodKey).ToHashSet(StringComparer.Ordinal);
            var scoreByKey = dtoRows.Where(r => r.SubjectUserId == subject && r.TotalScore is not null)
                .GroupBy(r => r.PeriodKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (decimal?)g.Average(r => r.TotalScore!.Value), StringComparer.Ordinal);

            sourcePeriods = commitments.GetValueOrDefault(subject, new List<Commitment>())
                .Select(c => new KpiSourcePeriodDto(
                    c.Key, c.Start, c.End, c.Label,
                    completedKeys.Contains(c.Key), c.ExemptReason is not null, c.ExemptReason,
                    scoreByKey.GetValueOrDefault(c.Key)))
                .ToList();
        }
        else
        {
            var members = ctx.Roster;
            measure = BuildMeasure(
                ctx, AggregateGroup(ctx, members, aggregates), AggregateGroup(ctx, members, previous), true);
        }

        return Result<KpiDrilldownDto>.Success(new KpiDrilldownDto(
            KpiPeriodResolvedDto.From(ctx.Period),
            ctx.RequestedCadence,
            query.SubjectUserId,
            KpiScorePolicy.Round(recomputed),
            dtoRows.Count,
            dtoRows,
            _clock.UtcNow.UtcDateTime,
            measure,
            sourcePeriods,
            effectiveCadence,
            cadenceSource));
    }

    // ===================== التحضير المشترك (نطاق + فترة + كادنس + قائمة الموظّفين) =====================

    private sealed record Failure(string Message, string Code);

    private sealed record Prepared(CalculationContext? Context, Failure? Error);

    private sealed record RosterUser(
        Guid UserId, string FullName, Guid? TeamId, Guid? DepartmentId, DateOnly? HireDate, DateOnly? ExitDate);

    private sealed class CalculationContext
    {
        public required ScopeContext Scope { get; init; }
        public required ResolvedPeriod Period { get; init; }
        public required ResolvedPeriod PreviousPeriod { get; init; }

        /// <summary>الكادنس المطلوب صراحةً (مسار النبض الأسبوعيّ المنفصل)؛ <c>null</c> ⇒ تلقائيّ لكلّ موظّف.</summary>
        public required KpiCadence? RequestedCadence { get; init; }

        /// <summary>DEC-01/5 — التواتر الفعّال لكلّ موظّف في القائمة، مع مصدره.</summary>
        public required IReadOnlyDictionary<Guid, KpiEffectiveCadence> Cadences { get; init; }

        public required IReadOnlyList<RosterUser> Roster { get; init; }
        public required IReadOnlyDictionary<Guid, string> TeamNames { get; init; }
        public required IReadOnlyDictionary<Guid, string> DepartmentNames { get; init; }

        /// <summary>العتبة ومصدرها لكلّ كادنس (B-6) — تُنتقى بحسب تواتر الموظّف لا بقيمة عامّة واحدة.</summary>
        public required IReadOnlyDictionary<KpiCadence, (decimal Threshold, string Source)> Thresholds { get; init; }

        public decimal MinimumCoverage { get; init; }

        public KpiCadence? CadenceOf(Guid userId) =>
            RequestedCadence ?? Cadences.GetValueOrDefault(userId)?.Cadence;

        public string CadenceSourceOf(Guid userId) =>
            RequestedCadence is not null
                ? KpiCadenceSources.ExplicitRequest
                : Cadences.GetValueOrDefault(userId)?.Source ?? KpiCadenceSources.NotConfigured;

        public (decimal Threshold, string Source) ThresholdOf(Guid userId) =>
            CadenceOf(userId) is KpiCadence c && Thresholds.TryGetValue(c, out var t)
                ? t
                : Thresholds.Values.FirstOrDefault();
    }

    private async Task<Prepared> PrepareAsync(KpiAnalyticsQuery query, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return new Prepared(null, new Failure("غير مصرّح.", "auth.unauthenticated"));

        var resolved = _periods.Resolve(new PeriodRequest(query.PeriodType, query.PeriodKey, query.From, query.To));
        if (!resolved.Succeeded) return new Prepared(null, new Failure(resolved.Error!, resolved.ErrorCode!));
        var period = resolved.Value!;

        var scope = await _scope.ResolveAsync(ct);

        // فرض النطاق على الموظّف المطلوب: خارج النطاق ⇒ لا يُسرَّب وجوده (404 على مستوى المتحكّم).
        if (query.SubjectUserId is Guid subject && !scope.Contains(subject))
            return new Prepared(null, new Failure("غير موجود.", "kpi.not_found"));

        var roster = await BuildRosterAsync(scope, query, ct);

        // DEC-01/2 — لا يُطلَب من المستخدم اختيار «نوع التقييم». الكادنس يُحسَم خادميًّا لكلّ موظّف من
        // القالب الذي يُقيَّم عليه فعلًا (DEC-01/5). B-3 محفوظ: ما زال لا يوجد سقوط صامت — غياب الإعداد
        // يُنتج حالة «التواتر غير مُهيّأ» الصريحة لا افتراضًا للنبض الأسبوعيّ.
        // DEC-01/6 — مرساة السريان هي نهاية الفترة: الأرباع التاريخيّة تُقرأ بإعدادها الساري حينها.
        var cadences = query.Cadence is null
            ? await _templates.ResolveEffectiveCadencesAsync(
                roster.Select(u => u.UserId).ToList(), period.End, ct)
            : new Dictionary<Guid, KpiEffectiveCadence>();

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

        var thresholds = new Dictionary<KpiCadence, (decimal, string)>();
        foreach (var c in new[] { KpiCadence.WeeklyPulse, KpiCadence.Quarterly })
            thresholds[c] = await ResolveBelowTargetThresholdAsync(c, ct);

        return new Prepared(new CalculationContext
        {
            Scope = scope,
            Period = period,
            PreviousPeriod = _periods.PreviousComparable(period),
            RequestedCadence = query.Cadence,
            Cadences = cadences,
            Roster = roster,
            TeamNames = teamNames,
            DepartmentNames = deptNames,
            Thresholds = thresholds,
            MinimumCoverage = _options.MinimumCoverageForRanking
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
            .Select(u => new RosterUser(u.Id, u.FullName, u.TeamId, u.DepartmentId, u.HireDate, u.ExitDate))
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
    /// استعلام التقييمات **المكتمِلة** لفترة ونطاق: حالة اكتمال معتمَدة (DEC-01/9 — Approved أو Closed)
    /// + درجة غير فارغة + كادنس الموظّف الفعّال + مفتاح الدورة داخل الفترة.
    /// المحذوف مستبعَد تلقائيًّا بالمرشّح العامّ في <see cref="AppDbContext"/>.
    /// </summary>
    private IQueryable<EvaluationJoin> EligibleEvaluationsQuery(CalculationContext ctx, ResolvedPeriod period)
        => BaseEvaluationsQuery(ctx, period)
            .Where(x => KpiScorePolicy.CompletedStatuses.Contains(x.e.Status) && x.e.TotalScore != null);

    private IQueryable<EvaluationJoin> BaseEvaluationsQuery(CalculationContext ctx, ResolvedPeriod period)
    {
        // DEC-01/3+4 — مساران منفصلان داخل نافذة عرض واحدة (الربع): نبض الأسبوع مفاتيحه YYYY-Www،
        // والتقييم الربعيّ الرسميّ مفتاحه YYYY-Qn. قصر الاستعلام على مفاتيح الأسابيع وحدها كان يجعل
        // بسط المسار الربعيّ صفرًا أبدًا مقابل مقام غير صفريّ ⟹ «لم يبدأ» دائمة وتغطية 0% كاذبة.
        var weekKeys = _periods.WeekKeysWithin(period);
        var quarterKeys = QuarterWindowsWithin(period).Select(w => w.Key).ToList();
        var periodKeys = weekKeys.Concat(quarterKeys).ToList();

        var q = from e in _db.KpiEvaluations.AsNoTracking()
                join v in _db.KpiTemplateVersions.AsNoTracking() on e.KpiTemplateVersionId equals v.Id
                join t in _db.KpiTemplates.AsNoTracking() on v.KpiTemplateId equals t.Id
                where periodKeys.Contains(e.PeriodKey)
                select new EvaluationJoin { e = e, t = t };

        if (ctx.RequestedCadence is KpiCadence requested)
        {
            // المسار الصريح (DEC-01/3): مسار واحد لكلّ من في النطاق — سلوك ما قبل R5 حرفيًّا.
            var userIds = ctx.Roster.Select(u => u.UserId).ToList();
            return q.Where(x => x.t.Cadence == requested && userIds.Contains(x.e.SubjectUserId));
        }

        // المسار التلقائيّ: تواتر كلّ موظّف على حدة. قائمتان فقط ⇒ استعلام واحد بلا N+1.
        var weeklyIds = ctx.Roster.Where(u => ctx.CadenceOf(u.UserId) == KpiCadence.WeeklyPulse)
            .Select(u => u.UserId).ToList();
        var quarterlyIds = ctx.Roster.Where(u => ctx.CadenceOf(u.UserId) == KpiCadence.Quarterly)
            .Select(u => u.UserId).ToList();

        return q.Where(x =>
            (x.t.Cadence == KpiCadence.WeeklyPulse && weeklyIds.Contains(x.e.SubjectUserId))
            || (x.t.Cadence == KpiCadence.Quarterly && quarterlyIds.Contains(x.e.SubjectUserId)));
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
        var completed = KpiScorePolicy.CompletedStatuses;
        var approved = await BaseEvaluationsQuery(ctx, period)
            .GroupBy(x => x.e.SubjectUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Sum = g.Where(x => completed.Contains(x.e.Status) && x.e.TotalScore != null)
                       .Sum(x => (decimal?)x.e.TotalScore) ?? 0m,
                EligibleCount = g.Count(x => completed.Contains(x.e.Status) && x.e.TotalScore != null),
                ExcludedByStatus = g.Count(x => !completed.Contains(x.e.Status))
            })
            .ToListAsync(ct);

        var commitments = await BuildCommitmentsAsync(ctx, period, ct);

        var result = new Dictionary<Guid, EmployeeAggregate>(ctx.Roster.Count);
        var byUser = approved.ToDictionary(a => a.UserId);

        foreach (var u in ctx.Roster)
        {
            byUser.TryGetValue(u.UserId, out var a);
            var eligibleCount = a?.EligibleCount ?? 0;
            var c = commitments.GetValueOrDefault(u.UserId);
            result[u.UserId] = new EmployeeAggregate(
                KpiScorePolicy.EmployeePeriodScore(a?.Sum ?? 0m, eligibleCount),
                eligibleCount,
                a?.ExcludedByStatus ?? 0,
                c?.Count ?? 0,
                c?.Count(w => w.ExemptReason is null) ?? 0);
        }

        return result;
    }

    /// <summary>التزام متوقَّع واحد داخل نافذة التحليل، مع سبب إعفائه إن وُجد (<c>null</c> ⇒ يدخل المقام).</summary>
    private sealed record Commitment(string Key, DateOnly Start, DateOnly End, string Label, string? ExemptReason);

    private const string ExemptApprovedLeave = "approvedLeave";
    private const string ExemptAdministrative = "administrativeExemption";
    private const string ExemptBeforeHire = "beforeHireDate";
    private const string ExemptAfterExit = "afterExitDate";

    /// <summary>
    /// DEC-01/7+8 — الالتزامات المتوقَّعة لكلّ موظّف داخل الفترة، وأسباب إعفاء كلّ التزام.
    /// <list type="number">
    /// <item>العدد يُشتقّ من <b>تواتر الموظّف الفعّال</b> لا من قيمة عامّة واحدة (DEC-01/7).</item>
    /// <item>الإعفاء لا يُطبَّق إلّا إذا غطّى الالتزام <b>بالكامل</b>؛ التغطية الجزئيّة لا تُسقِط أسبوعًا كاملًا.</item>
    /// <item><c>HireDate</c>/<c>ExitDate</c> الفارغان ⇒ القيد غير مطبَّق إطلاقًا (لا بديل مُستنتَج).</item>
    /// <item>الإعفاء الإداريّ = استثناء موظّف صريح <b>محدَّد بتاريخَي سريان</b> (DEC-01/6+8)؛ الاستثناء
    /// غير المؤقَّت يبقى استثناء قالب كما كان ولا يمسّ المقام.</item>
    /// </list>
    /// الفرق بين <c>Expected</c> و<c>AdjustedExpected</c> هو نفسه الدليل على أنّ الإعفاء خفّض المقام ولم يعاقب.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, List<Commitment>>> BuildCommitmentsAsync(
        CalculationContext ctx, ResolvedPeriod period, CancellationToken ct)
    {
        var weekly = _periods.WeekKeysWithin(period)
            .Select(k =>
            {
                var (s, e) = ReportingCalendarPolicy.CycleRange(k);
                return new Commitment(k, s, e, ReportingCalendarPolicy.CycleLabel(k), null);
            })
            .ToList();
        var quarterly = QuarterWindowsWithin(period);

        var userIds = ctx.Roster.Select(u => u.UserId).ToList();

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == LeaveRequestStatus.HrApproved
                        && l.Type == LeaveRequestType.Leave
                        && userIds.Contains(l.RequesterUserId)
                        && l.StartDate <= period.End
                        && l.EndDate >= period.Start)
            .Select(l => new { l.RequesterUserId, l.StartDate, l.EndDate })
            .ToListAsync(ct);
        var leavesByUser = leaves.GroupBy(l => l.RequesterUserId)
            .ToDictionary(g => g.Key, g => g.Select(l => (l.StartDate, l.EndDate)).ToList());

        var exemptions = await _db.KpiTemplateAssignments.AsNoTracking()
            .Where(a => a.IsActive
                        && a.ScopeType == TemplateAssignmentScope.Employee
                        && a.Kind == TemplateAssignmentKind.Exclude
                        && a.EffectiveFrom != null && a.EffectiveTo != null
                        && userIds.Contains(a.ScopeId)
                        && a.EffectiveFrom <= period.End
                        && a.EffectiveTo >= period.Start)
            .Select(a => new { a.ScopeId, a.EffectiveFrom, a.EffectiveTo })
            .ToListAsync(ct);
        var exemptionsByUser = exemptions.GroupBy(a => a.ScopeId)
            .ToDictionary(g => g.Key, g => g.Select(a => (From: a.EffectiveFrom!.Value, To: a.EffectiveTo!.Value)).ToList());

        var result = new Dictionary<Guid, List<Commitment>>(ctx.Roster.Count);
        foreach (var u in ctx.Roster)
        {
            var cadence = ctx.CadenceOf(u.UserId);
            if (cadence is null)
            {
                // DEC-01/5 — لا تواتر مُهيّأ ⇒ لا التزام مفترَض. لا يُخترَع مقام ولا تُلفَّق تغطية.
                result[u.UserId] = new List<Commitment>();
                continue;
            }

            var windows = cadence == KpiCadence.WeeklyPulse ? weekly : quarterly;
            result[u.UserId] = windows.Select(w => w with { ExemptReason = ExemptReasonFor(w, u) }).ToList();
        }

        return result;

        string? ExemptReasonFor(Commitment w, RosterUser u)
        {
            if (u.HireDate is DateOnly hire && w.End < hire) return ExemptBeforeHire;
            if (u.ExitDate is DateOnly exit && w.Start > exit) return ExemptAfterExit;
            if (exemptionsByUser.TryGetValue(u.UserId, out var ex)
                && ex.Any(a => a.From <= w.Start && a.To >= w.End)) return ExemptAdministrative;
            if (leavesByUser.TryGetValue(u.UserId, out var lv)
                && lv.Any(l => l.StartDate <= w.Start && l.EndDate >= w.End)) return ExemptApprovedLeave;
            return null;
        }
    }

    private static List<Commitment> QuarterWindowsWithin(ResolvedPeriod period)
    {
        var windows = new List<Commitment>();
        var cursor = new DateOnly(period.Start.Year, (period.Start.Month - 1) / 3 * 3 + 1, 1);
        while (cursor <= period.End)
        {
            var q = (cursor.Month - 1) / 3 + 1;
            var (s, e) = ReportingCalendarPolicy.QuarterRange(cursor.Year, q);
            if (s <= period.End && e >= period.Start)
                windows.Add(new Commitment($"{cursor.Year}-Q{q}", s, e, $"الربع {q} — {cursor.Year}", null));
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

        var cadence = ctx.CadenceOf(user.UserId);
        var (threshold, thresholdSource) = ctx.ThresholdOf(user.UserId);
        var measure = BuildMeasure(ctx, now, before, cadence is not null);

        // DEC-01/13+14 — الأهليّة للمتوسّط الرسميّ: تواتر مُهيّأ **و** تغطية ≥ 80% من AdjustedExpected.
        var eligible = cadence is not null
            && KpiScorePolicy.EligibleForRanking(now.EligibleCount, now.AdjustedExpected, ctx.MinimumCoverage);

        // Missing لا يُفترَض دون العتبة ولا فوقها: القيمة null ⇒ الحكم null (لا تلوين ولا إنذار كاذب).
        bool? belowTarget = now.Score is null ? null : now.Score.Value < threshold;

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
            threshold,
            thresholdSource,
            cadence,
            ctx.CadenceSourceOf(user.UserId));
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
        var now = AggregateGroup(ctx, members, current);
        var before = AggregateGroup(ctx, members, previous);

        var rows = members.Select(m => BuildEmployeeDto(ctx, m, current, previous)).ToList();

        // DEC-01/17 — غير المؤهّلين لا يختفون: تُعرَض أسماؤهم وحالة نقصهم **منفصلةً** عن المتوسّط الرسميّ.
        var excluded = rows
            .Where(r => !r.EligibleForRanking && r.Measure.Value is not null)
            .OrderBy(r => r.FullName, StringComparer.Ordinal)
            .ToList();

        return new KpiGroupScoreDto(
            groupType,
            groupId,
            groupName,
            BuildMeasure(ctx, now, before, true),
            members.Count(m => current.GetValueOrDefault(m.UserId, EmptyAggregate).Score is not null),
            members.Count,
            rows.Count(r => r.EligibleForRanking),
            excluded);
    }

    /// <summary>
    /// المرحلة الثانية من التوسيط (DEC-01/16): متوسّط **متوسّطات** الأعضاء، على قيم **غير مقرَّبة**
    /// منعًا للتقريب المزدوج. لا يدخل المتوسّطَ إلّا العضو <b>المؤهّل</b> (تواتر مُهيّأ + تغطية ≥ 80%)
    /// — DEC-01/14: نتيجة دون العتبة «مؤقّتة» ولا تدخل المتوسّط الرسميّ ولا التصدير المالي النهائي.
    /// العدّادات تُجمَع للشفافيّة على **كلّ** الأعضاء لا على المؤهّلين وحدهم.
    /// </summary>
    private static EmployeeAggregate AggregateGroup(
        CalculationContext ctx, IReadOnlyList<RosterUser> members, IReadOnlyDictionary<Guid, EmployeeAggregate> byUser)
    {
        var rows = members.Select(m => (User: m, Agg: byUser.GetValueOrDefault(m.UserId, EmptyAggregate))).ToList();

        var qualified = rows
            .Where(r => r.Agg.Score is not null
                        && ctx.CadenceOf(r.User.UserId) is not null
                        && KpiScorePolicy.EligibleForRanking(
                            r.Agg.EligibleCount, r.Agg.AdjustedExpected, ctx.MinimumCoverage))
            .Select(r => r.Agg.Score!.Value);

        return new EmployeeAggregate(
            KpiScorePolicy.GroupScore(qualified),
            rows.Sum(r => r.Agg.EligibleCount),
            rows.Sum(r => r.Agg.ExcludedByStatusCount),
            rows.Sum(r => r.Agg.Expected),
            rows.Sum(r => r.Agg.AdjustedExpected));
    }

    private KpiMeasureDto BuildMeasure(
        CalculationContext ctx, EmployeeAggregate now, EmployeeAggregate before, bool cadenceConfigured)
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
            KpiScorePolicy.DataQuality(now.EligibleCount, now.AdjustedExpected, ctx.MinimumCoverage),
            KpiScorePolicy.Round(before.Score),
            KpiScorePolicy.Round(delta),
            trend,
            KpiScorePolicy.CoveragePercent(now.EligibleCount, now.AdjustedExpected),
            cadenceConfigured
                && KpiScorePolicy.IsProvisional(now.Score, now.EligibleCount, now.AdjustedExpected, ctx.MinimumCoverage),
            KpiScorePolicy.JourneyState(
                cadenceConfigured, now.Expected, now.AdjustedExpected, now.EligibleCount,
                ctx.Period.IsOpen, ctx.MinimumCoverage));
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

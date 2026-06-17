using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class KpiEvaluationService : IKpiEvaluationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IScopeResolver _scope;

    // صيغة مفتاح الفترة الأسبوعية المعتمدة: YYYY-Www (مثال 2026-W25) — تمنع إدخال قيَم حرّة غير مفهومة.
    private static readonly Regex WeeklyPeriodKeyPattern = new(@"^\d{4}-W\d{2}$", RegexOptions.Compiled);

    /// <summary>عتبة التنبيه: درجة إجمالية دونها تُعدّ تحت المستهدف.</summary>
    private const decimal AlertThreshold = 60m;

    public KpiEvaluationService(AppDbContext db, ICurrentUser currentUser,
        INotificationService notifications, IAuditService audit, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _scope = scope;
    }

    public async Task<Result<KpiEvaluationDto>> CreateOrGetAsync(CreateKpiEvaluationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid evaluatorId)
            return Result<KpiEvaluationDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.PeriodKey))
            return Result<KpiEvaluationDto>.Failure("مفتاح الفترة مطلوب.", "kpi_eval.period_required");
        if (request.SubjectUserId == Guid.Empty)
            return Result<KpiEvaluationDto>.Failure("الموظف المُقيَّم مطلوب.", "kpi_eval.subject_required");

        // حارس الدورية (المرحلة الحالية): تقييم KPI أسبوعي فقط. التجميع الشهري/الربع سنوي/السنوي يُدعم لاحقًا.
        if (request.PeriodType != PeriodType.Weekly)
            return Result<KpiEvaluationDto>.Failure(
                "تقييم KPI الحالي أسبوعي فقط. الدوريات الأخرى (شهري/ربع سنوي/سنوي) ستُدعم لاحقًا.",
                "kpi_eval.period_type_not_supported");

        // صيغة الفترة الأسبوعية يجب أن تكون YYYY-Www (مثال 2026-W25) — يمنع القيَم الحرّة غير المفهومة.
        if (!WeeklyPeriodKeyPattern.IsMatch(request.PeriodKey.Trim()))
            return Result<KpiEvaluationDto>.Failure(
                "صيغة الفترة غير صحيحة؛ استخدم صيغة الأسبوع YYYY-Www مثل 2026-W25.",
                "kpi_eval.period_format_invalid");

        // نطاق إنشاء التقييم أضيق من نطاق العرض: المرؤوسون المباشرون فقط (أو كل الموظّفين للأدمن).
        // لا يكفي أن يكون الموظّف ضمن نطاق رؤية المدير الواسع (القسم) — يجب أن يكون مرؤوسًا مباشرًا.
        var (isAdmin, evaluatableIds) = await EvaluatableSubjectScopeAsync(evaluatorId, ct);
        if (!isAdmin && !evaluatableIds.Contains(request.SubjectUserId))
            return Result<KpiEvaluationDto>.Failure(
                "لا يمكنك إنشاء تقييم لهذا الموظّف؛ التقييم متاح لمرؤوسيك المباشرين فقط.", "auth.forbidden");

        var version = await _db.KpiTemplateVersions
            .Where(v => v.KpiTemplateId == request.KpiTemplateId && v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null)
            return Result<KpiEvaluationDto>.Failure("لا يوجد إصدار منشور لهذا القالب.", "kpi_template.no_published_version.conflict");

        var periodKey = request.PeriodKey.Trim();
        var existing = await _db.KpiEvaluations.FirstOrDefaultAsync(
            e => e.KpiTemplateVersionId == version.Id && e.SubjectUserId == request.SubjectUserId && e.PeriodKey == periodKey, ct);
        if (existing is not null)
            return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(existing.Id, ct));

        var subject = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.SubjectUserId, ct);
        if (subject is null)
            return Result<KpiEvaluationDto>.Failure("الموظف المُقيَّم غير موجود.", "kpi_eval.subject_not_found");

        var evaluation = new KpiEvaluation
        {
            KpiTemplateVersionId = version.Id,
            SubjectUserId = request.SubjectUserId,
            EvaluatorId = evaluatorId,
            TeamId = subject.TeamId,
            DepartmentId = subject.DepartmentId,
            PeriodType = request.PeriodType,
            PeriodKey = periodKey,
            Status = KpiEvaluationStatus.Draft,
            Trend = KpiTrend.Unknown
        };
        _db.KpiEvaluations.Add(evaluation);
        await _db.SaveChangesAsync(ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluation.Id, ct));
    }

    public async Task<Result<EvaluatableSubjectsDto>> GetEvaluatableSubjectsAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EvaluatableSubjectsDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var (isAdmin, ids) = await EvaluatableSubjectScopeAsync(uid, ct);
        var subjects = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new EvaluatableSubjectDto(u.Id, u.FullName, u.Email ?? string.Empty))
            .ToListAsync(ct);

        return Result<EvaluatableSubjectsDto>.Success(new EvaluatableSubjectsDto(isAdmin, subjects));
    }

    /// <summary>
    /// نطاق إنشاء تقييم KPI: الأدمن يختار أي موظّف نشط (وضع إداري)، وبقيّة القيادات
    /// (TL/Manager/GM/CEO) مرؤوسوهم المباشرون فقط (ManagerId == المُقيّم) باستثناء النفس.
    /// متعمَّد أن يكون أضيق من نطاق العرض في ScopeResolver (الذي قد يشمل قسمًا كاملًا).
    /// </summary>
    private async Task<(bool IsAdmin, List<Guid> Ids)> EvaluatableSubjectScopeAsync(Guid uid, CancellationToken ct)
    {
        if (_currentUser.IsInRole(Roles.Admin))
            return (true, await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct));

        var ids = await _db.Users
            .Where(u => u.IsActive && u.ManagerId == uid && u.Id != uid)
            .Select(u => u.Id)
            .ToListAsync(ct);
        return (false, ids);
    }

    public async Task<Result<KpiEvaluationDto>> GetAsync(Guid evaluationId, CancellationToken ct = default)
    {
        var e = await _db.KpiEvaluations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!await CanViewAsync(e, ct)) return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية الوصول لهذا التقييم.", "auth.forbidden");
        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> SaveResultsAsync(Guid evaluationId, SaveKpiResultsRequest request, CancellationToken ct = default)
    {
        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        var ownerCheck = ResourceGuard.EnsureOwnerOrElevated(_currentUser, e.EvaluatorId ?? Guid.Empty);
        if (!ownerCheck.Succeeded) return Result<KpiEvaluationDto>.Failure(ownerCheck.Error!, ownerCheck.ErrorCode!);

        if (e.Status is not (KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress))
            return Result<KpiEvaluationDto>.Failure("لا يمكن تعديل تقييم بعد إرساله.", "kpi_eval.locked.conflict");

        var metricIds = await _db.KpiMetrics.Where(m => m.KpiTemplateVersionId == e.KpiTemplateVersionId)
            .Select(m => m.Id).ToListAsync(ct);

        foreach (var input in request.Results)
        {
            if (!metricIds.Contains(input.KpiMetricId)) continue;
            var result = e.Results.FirstOrDefault(r => r.KpiMetricId == input.KpiMetricId);
            if (result is null)
            {
                result = new KpiResult { KpiEvaluationId = e.Id, KpiMetricId = input.KpiMetricId };
                _db.KpiResults.Add(result);
            }
            result.RawValue = input.RawValue;
            result.Score = input.Score;
            result.Note = input.Note;
            result.UpdatedAtUtc = DateTime.UtcNow;
        }

        e.Status = KpiEvaluationStatus.InProgress;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> SubmitAsync(Guid evaluationId, CancellationToken ct = default)
    {
        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        var ownerCheck = ResourceGuard.EnsureOwnerOrElevated(_currentUser, e.EvaluatorId ?? Guid.Empty);
        if (!ownerCheck.Succeeded) return Result<KpiEvaluationDto>.Failure(ownerCheck.Error!, ownerCheck.ErrorCode!);

        if (e.Status is not (KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress))
            return Result<KpiEvaluationDto>.Failure("التقييم في حالة لا تسمح بالإرسال.", "kpi_eval.not_submittable.conflict");

        var metrics = await _db.KpiMetrics.Where(m => m.KpiTemplateVersionId == e.KpiTemplateVersionId)
            .ToListAsync(ct);
        if (metrics.Count == 0)
            return Result<KpiEvaluationDto>.Failure("لا توجد مؤشرات للاحتساب.", "kpi_eval.no_metrics.conflict");

        decimal weighted = 0m;
        foreach (var metric in metrics)
        {
            var result = e.Results.FirstOrDefault(r => r.KpiMetricId == metric.Id);
            if (result is null)
            {
                result = new KpiResult { KpiEvaluationId = e.Id, KpiMetricId = metric.Id };
                _db.KpiResults.Add(result);
                e.Results.Add(result);
            }
            var score = ComputeScore(metric, result);
            result.Score = score;
            result.Weight = metric.Weight; // لقطة تاريخية للوزن
            weighted += score * metric.Weight;
        }

        var totalScore = Math.Round(weighted / 100m, 2);
        e.TotalScore = totalScore;
        e.Trend = await ComputeTrendAsync(e, totalScore, ct);
        e.Status = KpiEvaluationStatus.Submitted;
        e.SubmittedAtUtc = DateTime.UtcNow;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(e.SubjectUserId, "kpi.submitted",
            "تم احتساب مؤشرات أدائك", null, $"/kpi-evaluations/{e.Id}", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.submitted", nameof(KpiEvaluation), e.Id, ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> ApproveAsync(Guid evaluationId, CancellationToken ct = default)
    {
        var e = await _db.KpiEvaluations.FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        if (!_currentUser.IsInAnyRole(Roles.Management))
            return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية اعتماد التقييم.", "auth.forbidden");
        // منع IDOR: المعتمِد لا يعتمد إلا تقييمات أنشأها أو ضمن نطاق رؤيته.
        var approveScope = await _scope.ResolveAsync(ct);
        if (_currentUser.UserId != e.EvaluatorId && !approveScope.Contains(e.SubjectUserId))
            return Result<KpiEvaluationDto>.Failure("هذا التقييم خارج نطاق إشرافك.", "auth.forbidden");
        if (e.Status != KpiEvaluationStatus.Submitted)
            return Result<KpiEvaluationDto>.Failure("لا يمكن اعتماد تقييم إلا بعد إرساله.", "kpi_eval.not_approvable.conflict");

        e.Status = KpiEvaluationStatus.Approved;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(e.SubjectUserId, "kpi.approved",
            "تم اعتماد تقييم أدائك", null, $"/kpi-evaluations/{e.Id}", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.approved", nameof(KpiEvaluation), e.Id, ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<IReadOnlyList<KpiEvaluationListItemDto>>> ListAsync(KpiEvaluationFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<KpiEvaluationListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.KpiEvaluations.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(e => ids.Contains(e.SubjectUserId) || e.EvaluatorId == userId);
        }

        if (filter.SubjectUserId is not null) q = q.Where(e => e.SubjectUserId == filter.SubjectUserId);
        if (filter.EvaluatorId is not null) q = q.Where(e => e.EvaluatorId == filter.EvaluatorId);
        if (filter.TeamId is not null) q = q.Where(e => e.TeamId == filter.TeamId);
        if (filter.DepartmentId is not null) q = q.Where(e => e.DepartmentId == filter.DepartmentId);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(e => e.PeriodKey == filter.PeriodKey);
        if (filter.Status is not null) q = q.Where(e => e.Status == filter.Status);

        return Result<IReadOnlyList<KpiEvaluationListItemDto>>.Success(await ProjectListAsync(q, ct));
    }

    public async Task<Result<IReadOnlyList<KpiEvaluationListItemDto>>> ListForSubjectAsync(Guid subjectUserId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<KpiEvaluationListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var subjectScope = await _scope.ResolveAsync(ct);
        if (userId != subjectUserId && !subjectScope.Contains(subjectUserId))
            return Result<IReadOnlyList<KpiEvaluationListItemDto>>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");

        var q = _db.KpiEvaluations.AsNoTracking().Where(e => e.SubjectUserId == subjectUserId);
        return Result<IReadOnlyList<KpiEvaluationListItemDto>>.Success(await ProjectListAsync(q, ct));
    }

    public async Task<Result<KpiAggregateDto>> GetAggregateAsync(KpiAggregateRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<KpiAggregateDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // 1) تحويل الدورية إلى مدى تواريخ [from, to] + تسمية الفترة. الأسبوع وحدة الأساس دائمًا.
        DateOnly from, to;
        string label;
        var granularity = (request.Granularity ?? string.Empty).Trim();
        switch (granularity)
        {
            case "Monthly":
                if (!TryParseYearMonth(request.PeriodKey, out var ym))
                    return Result<KpiAggregateDto>.Failure("صيغة الشهر غير صحيحة؛ استخدم YYYY-MM مثل 2026-06.", "kpi_aggregate.period_format_invalid");
                (from, to) = ReportCalendarPolicy.MonthRange(ym.Year, ym.Month);
                label = MonthLabel(ym.Year, ym.Month);
                break;
            case "Quarterly":
                if (!TryParseQuarter(request.PeriodKey, out var yq))
                    return Result<KpiAggregateDto>.Failure("صيغة الربع غير صحيحة؛ استخدم YYYY-Qn مثل 2026-Q2.", "kpi_aggregate.period_format_invalid");
                (from, to) = ReportCalendarPolicy.QuarterRange(yq.Year, yq.Quarter);
                label = $"الربع {yq.Quarter} — {yq.Year}";
                break;
            case "Yearly":
                if (!int.TryParse((request.PeriodKey ?? string.Empty).Trim(), out var year))
                    return Result<KpiAggregateDto>.Failure("صيغة السنة غير صحيحة؛ استخدم YYYY مثل 2026.", "kpi_aggregate.period_format_invalid");
                (from, to) = ReportCalendarPolicy.YearRange(year);
                label = $"سنة {year}";
                break;
            case "Custom":
                if (request.From is not DateOnly cf || request.To is not DateOnly cterm)
                    return Result<KpiAggregateDto>.Failure("المدى المخصّص يتطلّب تاريخ بداية ونهاية.", "kpi_aggregate.range_required");
                if (cf > cterm)
                    return Result<KpiAggregateDto>.Failure("تاريخ البداية يجب أن يسبق تاريخ النهاية.", "kpi_aggregate.range_invalid");
                (from, to) = (cf, cterm);
                label = $"من {cf:yyyy-MM-dd} إلى {cterm:yyyy-MM-dd}";
                break;
            default:
                return Result<KpiAggregateDto>.Failure("نوع التجميع غير مدعوم؛ استخدم Monthly/Quarterly/Yearly/Custom.", "kpi_aggregate.granularity_invalid");
        }

        // 2) فرض النطاق خادميًّا (لا تصفية من الواجهة فقط).
        var scope = await _scope.ResolveAsync(ct);
        if (request.SubjectUserId is Guid sid && userId != sid && !scope.Contains(sid))
            return Result<KpiAggregateDto>.Failure("هذا الموظّف خارج نطاق صلاحيتك.", "auth.forbidden");

        var q = _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.PeriodType == PeriodType.Weekly
                        && e.TotalScore != null
                        && (e.Status == KpiEvaluationStatus.Submitted
                            || e.Status == KpiEvaluationStatus.Approved
                            || e.Status == KpiEvaluationStatus.Closed));

        if (!scope.SeesAll)
        {
            var scopeIds = scope.UserIds;
            q = q.Where(e => scopeIds.Contains(e.SubjectUserId));
        }
        if (request.SubjectUserId is Guid s) q = q.Where(e => e.SubjectUserId == s);
        if (request.TeamId is Guid t) q = q.Where(e => e.TeamId == t);
        if (request.DepartmentId is Guid d) q = q.Where(e => e.DepartmentId == d);

        var raw = await q.Select(e => new { e.PeriodKey, e.TotalScore }).ToListAsync(ct);

        // 3) فلترة الأسابيع الواقعة داخل المدى (بحسب خميس بداية الأسبوع) ثم التجميع لكل أسبوع.
        var inRange = raw
            .Where(r => ReportCalendarPolicy.WeekInRange(r.PeriodKey, from, to))
            .ToList();

        var weeks = inRange
            .GroupBy(r => r.PeriodKey)
            .Select(g =>
            {
                var (ws, we) = ReportCalendarPolicy.WeekRange(g.Key);
                return new KpiWeeklyPointDto(
                    g.Key, ws, we,
                    Math.Round(g.Average(x => x.TotalScore!.Value), 2),
                    g.Count());
            })
            .OrderBy(w => w.WeekStart)
            .ToList();

        decimal? average = weeks.Count > 0 ? Math.Round(weeks.Average(w => w.Score), 2) : null;
        var evaluationsCount = inRange.Count;

        // المستخدم العادي يرى نتائجه فقط؛ تفاصيل الأسابيع تُعرض إذا لم يكن نطاقه شاملًا أو حدّد موظّفًا بعينه.
        var canViewRows = !scope.SeesAll || request.SubjectUserId is not null
                          || request.TeamId is not null || request.DepartmentId is not null;

        var dto = new KpiAggregateDto(
            granularity, label, from, to, average,
            weeks.Count, evaluationsCount, scope.ScopeType, canViewRows,
            canViewRows ? weeks : new List<KpiWeeklyPointDto>());

        return Result<KpiAggregateDto>.Success(dto);
    }

    private static bool TryParseYearMonth(string? key, out (int Year, int Month) value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var m = Regex.Match(key.Trim(), @"^(\d{4})-(\d{2})$");
        if (!m.Success) return false;
        var year = int.Parse(m.Groups[1].Value);
        var month = int.Parse(m.Groups[2].Value);
        if (month is < 1 or > 12) return false;
        value = (year, month);
        return true;
    }

    private static bool TryParseQuarter(string? key, out (int Year, int Quarter) value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var m = Regex.Match(key.Trim(), @"^(\d{4})-Q([1-4])$");
        if (!m.Success) return false;
        value = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        return true;
    }

    private static readonly string[] ArMonthNames =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private static string MonthLabel(int year, int month) => $"{ArMonthNames[month - 1]} {year}";

    private static decimal ComputeScore(KpiMetric metric, KpiResult result)
    {
        // الاحتساب اليدوي: الدرجة المُدخَلة مباشرة.
        if (metric.CalcMethod == KpiCalcMethod.Manual)
            return Clamp(result.Score ?? 0m);

        // الآلي/الهجين: درجة من القيمة الخام مقابل المستهدف، مع أولوية للدرجة اليدوية في الهجين.
        if (metric.CalcMethod == KpiCalcMethod.Hybrid && result.Score is decimal manual)
            return Clamp(manual);

        if (metric.TargetValue is decimal target && target != 0m && result.RawValue is decimal raw)
            return Clamp(raw / target * 100m);

        return Clamp(result.Score ?? 0m);
    }

    private static decimal Clamp(decimal v) => Math.Round(Math.Max(0m, Math.Min(100m, v)), 2);

    private async Task<KpiTrend> ComputeTrendAsync(KpiEvaluation e, decimal totalScore, CancellationToken ct)
    {
        var templateId = await _db.KpiTemplateVersions.Where(v => v.Id == e.KpiTemplateVersionId)
            .Select(v => v.KpiTemplateId).FirstAsync(ct);

        var priorScore = await _db.KpiEvaluations.AsNoTracking()
            .Where(x => x.SubjectUserId == e.SubjectUserId
                        && x.Id != e.Id
                        && string.Compare(x.PeriodKey, e.PeriodKey) < 0
                        && x.TotalScore != null
                        && (x.Status == KpiEvaluationStatus.Submitted
                            || x.Status == KpiEvaluationStatus.Approved
                            || x.Status == KpiEvaluationStatus.Closed))
            .Join(_db.KpiTemplateVersions, x => x.KpiTemplateVersionId, v => v.Id, (x, v) => new { x, v.KpiTemplateId })
            .Where(j => j.KpiTemplateId == templateId)
            .OrderByDescending(j => j.x.PeriodKey)
            .Select(j => j.x.TotalScore)
            .FirstOrDefaultAsync(ct);

        if (priorScore is not decimal prior) return KpiTrend.Unknown;
        if (totalScore > prior) return KpiTrend.Up;
        if (totalScore < prior) return KpiTrend.Down;
        return KpiTrend.Flat;
    }

    private async Task<IReadOnlyList<KpiEvaluationListItemDto>> ProjectListAsync(IQueryable<KpiEvaluation> q, CancellationToken ct)
    {
        var rows = await q.OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new
            {
                e.Id,
                Title = _db.KpiTemplateVersions.Where(v => v.Id == e.KpiTemplateVersionId)
                    .Select(v => v.KpiTemplate!.Title).FirstOrDefault(),
                e.SubjectUserId,
                e.EvaluatorId,
                e.PeriodType,
                e.PeriodKey,
                e.Status,
                e.TotalScore,
                e.Trend
            }).ToListAsync(ct);

        var names = await UserNamesAsync(rows.Select(r => r.SubjectUserId), ct);
        return rows.Select(r => new KpiEvaluationListItemDto(
            r.Id, r.Title ?? string.Empty, r.SubjectUserId, names.GetValueOrDefault(r.SubjectUserId, string.Empty),
            r.EvaluatorId, r.PeriodType, r.PeriodKey, r.Status, r.TotalScore, r.Trend)).ToList();
    }

    private async Task<KpiEvaluationDto> BuildDtoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.KpiEvaluations.AsNoTracking().Include(x => x.Results)
            .FirstAsync(x => x.Id == id, ct);

        var version = await _db.KpiTemplateVersions
            .Where(v => v.Id == e.KpiTemplateVersionId)
            .Select(v => new { v.KpiTemplate!.Title, v.KpiTemplate.Cadence })
            .FirstAsync(ct);

        var metrics = await _db.KpiMetrics.Where(m => m.KpiTemplateVersionId == e.KpiTemplateVersionId)
            .OrderBy(m => m.Order)
            .Select(m => new { m.Id, m.Name, m.Weight, m.TargetValue, m.Unit })
            .ToListAsync(ct);

        var resultDtos = metrics.Select(m =>
        {
            var r = e.Results.FirstOrDefault(x => x.KpiMetricId == m.Id);
            return new KpiResultDto(m.Id, m.Name, m.Weight, m.TargetValue, m.Unit, r?.RawValue, r?.Score, r?.Note);
        }).ToList();

        var ids = new List<Guid> { e.SubjectUserId };
        if (e.EvaluatorId is Guid ev) ids.Add(ev);
        var names = await UserNamesAsync(ids, ct);

        var canEdit = (e.Status is KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress)
                      && (_currentUser.UserId == e.EvaluatorId || _currentUser.IsInRole(Roles.Admin));
        var isBelowTarget = e.TotalScore is decimal s && s < AlertThreshold;

        return new KpiEvaluationDto(e.Id, e.KpiTemplateVersionId, version.Title, version.Cadence,
            e.SubjectUserId, names.GetValueOrDefault(e.SubjectUserId, string.Empty),
            e.EvaluatorId, e.EvaluatorId is Guid evx ? names.GetValueOrDefault(evx) : null,
            e.TeamId, e.DepartmentId, e.PeriodType, e.PeriodKey, e.Status, e.TotalScore, e.Trend,
            isBelowTarget, e.SubmittedAtUtc, canEdit, resultDtos);
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<bool> CanViewAsync(KpiEvaluation e, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId) return false;
        if (userId == e.SubjectUserId || userId == e.EvaluatorId) return true;
        var scope = await _scope.ResolveAsync(ct);
        return scope.Contains(e.SubjectUserId);
    }
}

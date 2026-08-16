using System.Globalization;
using System.Text;
using System.Text.Json;
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

        // ROLE-AWARE-REPORTING-CALENDAR — التحقّق الخادميّ من مفتاح الدورة (Phase 2.4):
        // فوق فحص الصيغة، يجب أن يكون المفتاح دورةً صالحة بنيويًّا وقابلة للعكس (Sat→Fri عبر
        // ReportingCalendarPolicy) وألّا يكون دورةً مستقبلية لم تبدأ بعد. لا تصحيح بيانات ولا تغيير مفتاح مخزَّن.
        if (!ReportingCalendarPolicy.IsValidCycleKey(request.PeriodKey.Trim()))
            return Result<KpiEvaluationDto>.Failure("مفتاح الدورة غير صالح.", "kpi.cycle_key_invalid");
        if (ReportingCalendarPolicy.CycleRange(request.PeriodKey.Trim()).Start > ReportingCalendarPolicy.RiyadhToday())
            return Result<KpiEvaluationDto>.Failure("لا يمكن إنشاء تقييم لدورة لم تبدأ بعد.", "calendar.cycle_not_open");

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
    /// (TL/Manager/GM/CEO) مرؤوسوهم المباشرون (ManagerId == المُقيّم) باستثناء النفس،
    /// إضافةً إلى من عُيِّن لهم المستخدم الحالي مُراجِع KPI صريحًا (KpiReviewerOverrideUserId).
    /// متعمَّد أن يكون أضيق من نطاق العرض في ScopeResolver (الذي قد يشمل قسمًا كاملًا).
    /// </summary>
    private async Task<(bool IsAdmin, List<Guid> Ids)> EvaluatableSubjectScopeAsync(Guid uid, CancellationToken ct)
    {
        if (_currentUser.IsInRole(Roles.Admin))
            return (true, await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct));

        var ids = await _db.Users
            .Where(u => u.IsActive && u.Id != uid
                && (u.ManagerId == uid || u.KpiReviewerOverrideUserId == uid))
            .Select(u => u.Id)
            .ToListAsync(ct);
        return (false, ids);
    }

    /// <summary>
    /// KPI-REVIEWER-OVERRIDE-R1 — بحث قرائيّ صرف: يبحث عن تقييم قائم لـ(الموظّف + الفترة) ضمن إصدار
    /// محدَّد أو ضمن كلّ إصدارات القالب (كي لا يُحجَب تقييم تاريخيّ أُنشئ على إصدار أقدم). لا يُنشئ
    /// سجلًّا ولا يُعدّل أيّ حقل؛ لا يستدعي CreateOrGetAsync ولا يكتب في القاعدة إطلاقًا.
    /// لا يُطبَّق عليه حارس «الدورة المستقبلية» ولا حارس القابلية الحالية للقالب — فالغرض قراءة التاريخ.
    /// </summary>
    public async Task<Result<KpiEvaluationLookupDto>> LookupAsync(KpiEvaluationLookupQuery query, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<KpiEvaluationLookupDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (query.SubjectUserId == Guid.Empty)
            return Result<KpiEvaluationLookupDto>.Failure("الموظف المُقيَّم مطلوب.", "kpi_eval.subject_required");

        var periodKey = (query.PeriodKey ?? string.Empty).Trim();
        if (periodKey.Length == 0)
            return Result<KpiEvaluationLookupDto>.Failure("مفتاح الفترة مطلوب.", "kpi_eval.period_required");
        if (query.KpiTemplateId is null && query.KpiTemplateVersionId is null)
            return Result<KpiEvaluationLookupDto>.Failure("القالب أو إصدار القالب مطلوب.", "kpi_eval.template_required");

        // صلاحية القراءة: الموظّف نفسه، أو من يحقّ له تقييمه (يشمل التجاوز الصريح)، أو من يشمله نطاق العرض.
        if (uid != query.SubjectUserId)
        {
            var (isAdmin, evaluatableIds) = await EvaluatableSubjectScopeAsync(uid, ct);
            var allowed = isAdmin || evaluatableIds.Contains(query.SubjectUserId);
            if (!allowed)
            {
                var scope = await _scope.ResolveAsync(ct);
                allowed = scope.Contains(query.SubjectUserId);
            }
            if (!allowed)
                return Result<KpiEvaluationLookupDto>.Failure("لا تملك صلاحية الاطّلاع على تقييمات هذا الموظّف.", "auth.forbidden");
        }

        var q = _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.SubjectUserId == query.SubjectUserId && e.PeriodKey == periodKey && !e.IsDeleted);

        if (query.KpiTemplateVersionId is Guid versionId)
        {
            q = q.Where(e => e.KpiTemplateVersionId == versionId);
        }
        else
        {
            var templateId = query.KpiTemplateId!.Value;
            var versionIds = await _db.KpiTemplateVersions.AsNoTracking()
                .Where(v => v.KpiTemplateId == templateId)
                .Select(v => v.Id)
                .ToListAsync(ct);
            if (versionIds.Count == 0)
                return Result<KpiEvaluationLookupDto>.Success(new KpiEvaluationLookupDto(false, null));
            q = q.Where(e => versionIds.Contains(e.KpiTemplateVersionId));
        }

        var match = await q.OrderByDescending(e => e.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (match is null)
            return Result<KpiEvaluationLookupDto>.Success(new KpiEvaluationLookupDto(false, null));

        return Result<KpiEvaluationLookupDto>.Success(
            new KpiEvaluationLookupDto(true, await BuildDtoAsync(match.Id, ct)));
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

        // يُسمح بالتعديل قبل الإرسال (Draft/InProgress) أو بعد طلب تعديل من المراجع (NeedsRevision).
        if (e.Status is not (KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress or KpiEvaluationStatus.NeedsRevision))
            return Result<KpiEvaluationDto>.Failure("لا يمكن تعديل تقييم في حالته الحاليّة.", "kpi_eval.locked.conflict");

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

        // يُسمح بالإرسال أوّل مرّة (Draft/InProgress) أو إعادة الإرسال بعد طلب تعديل (NeedsRevision).
        if (e.Status is not (KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress or KpiEvaluationStatus.NeedsRevision))
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

        // إسناد مُراجِع إلزاميّ (ADMIN-GOVERNANCE-R1، تصحيح #6): المدير الأعلى للمُدخِل ثم GM ثم CEO ثم Admin (break-glass).
        // لا يجوز أن يكون المُراجِع هو الموضوع أو المُدخِل. نضمن عدم بقاء تقييم بلا مُراجِع.
        // ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1: تجاوز مراجِع KPI الصريح له الأولوية القصوى؛
        // وإن كان غير صالح لا نتجاهله بصمت بل نُرجِع خطأ إعداد واضحًا.
        var (reviewerOutcome, reviewerId) = await ResolveReviewerWithOverrideAsync(e, ct);
        if (reviewerOutcome == ReviewerResolution.InvalidOverride)
        {
            await _audit.LogAsync(_currentUser.UserId, "kpi.reviewer_override_invalid",
                nameof(KpiEvaluation), e.Id, ct: ct);
            return Result<KpiEvaluationDto>.Failure(
                "إعداد مراجِع KPI غير صالح (المستخدم غير موجود أو غير نشط أو هو الموضوع أو المُدخِل).",
                "kpi.reviewer_override_invalid");
        }
        if (reviewerId is not Guid reviewer)
            return Result<KpiEvaluationDto>.Failure(
                "تعذّر إسناد مُراجِع لهذا التقييم؛ لا يوجد مسؤول أعلى متاح للمراجعة.", "kpi_eval.no_reviewer.conflict");

        // KPI-REVIEWER-OVERRIDE-R1: حين يكون المُدخِل نفسه هو المُراجِع الصريح المعيَّن للموظّف
        // (KpiReviewerOverrideUserId == EvaluatorId) ⇒ اعتماد مباشر بلا سقوط إلى ManagerId وبلا رفض.
        // هذا الاستثناء لا يعمل إطلاقًا بلا تجاوز صريح (SelfOverride لا تُنتَج إلا من Override مضبوط).
        var isDirectApproval = reviewerOutcome == ReviewerResolution.SelfOverride;
        var now = DateTime.UtcNow;
        var toStatus = isDirectApproval ? KpiEvaluationStatus.Approved : KpiEvaluationStatus.UnderReview;

        var fromStatus = e.Status;
        var totalScore = Math.Round(weighted / 100m, 2);
        e.TotalScore = totalScore;
        e.Trend = await ComputeTrendAsync(e, totalScore, ct);
        e.Status = toStatus;
        e.ReviewerId = reviewer;
        e.ReviewedAtUtc = isDirectApproval ? now : null;
        e.ReviewNote = null;
        e.SubmittedAtUtc = now;
        e.UpdatedAtUtc = now;
        AddReviewEvent(e, "Submitted", fromStatus, toStatus, null, BuildSnapshot(e));
        if (isDirectApproval)
            AddReviewEvent(e, "ApprovedByExplicitReviewerOverride", KpiEvaluationStatus.UnderReview,
                KpiEvaluationStatus.Approved,
                "اعتماد مباشر: المُدخِل هو المُراجِع الصريح المعيَّن للموظّف (KpiReviewerOverrideUserId).", null);
        await _db.SaveChangesAsync(ct);

        if (isDirectApproval)
        {
            await _notifications.NotifyAsync(e.SubjectUserId, "kpi.approved",
                "تم اعتماد تقييم أدائك", null, "/app/my-kpi", ct);
            await _audit.LogAsync(_currentUser.UserId, "kpi.submitted", nameof(KpiEvaluation), e.Id, ct: ct);
            await _audit.LogAsync(_currentUser.UserId, "kpi.approved_direct_by_reviewer_override",
                nameof(KpiEvaluation), e.Id,
                JsonSerializer.Serialize(new
                {
                    reason = "الاعتماد المباشر تمّ لأنّ المُدخِل هو المُراجِع الصريح المعيَّن للموظّف (KpiReviewerOverrideUserId).",
                    subjectUserId = e.SubjectUserId,
                    evaluatorId = e.EvaluatorId,
                    reviewerId = reviewer,
                    approvedByUserId = _currentUser.UserId,
                    reviewedAtUtc = now,
                    periodKey = e.PeriodKey
                }), ct: ct);
            return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
        }

        // إشعار المُراجِع المعيَّن + إعلام الموظّف بأنّ تقييمه قيد المراجعة.
        await _notifications.NotifyAsync(reviewer, "kpi.review_requested",
            "تقييم KPI بانتظار مراجعتك", null, "/app/kpi-review", ct);
        await _notifications.NotifyAsync(e.SubjectUserId, "kpi.submitted",
            "تم احتساب مؤشرات أدائك وهي قيد المراجعة", null, "/app/my-kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.submitted", nameof(KpiEvaluation), e.Id, ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> ApproveAsync(Guid evaluationId, CancellationToken ct = default)
    {
        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        var gate = EnsureCanReview(e);
        if (gate is Result<KpiEvaluationDto> denied) return denied;

        // يُعتمَد من UnderReview (المسار الجديد) أو Submitted (توافق خلفيّ للسجلّات القديمة).
        if (e.Status is not (KpiEvaluationStatus.UnderReview or KpiEvaluationStatus.Submitted))
            return Result<KpiEvaluationDto>.Failure("لا يمكن اعتماد تقييم إلا وهو قيد المراجعة.", "kpi_eval.not_approvable.conflict");

        var fromStatus = e.Status;
        var snapshot = BuildSnapshot(e);
        e.Status = KpiEvaluationStatus.Approved;
        e.ReviewedAtUtc = DateTime.UtcNow;
        e.UpdatedAtUtc = DateTime.UtcNow;
        AddReviewEvent(e, "Approved", fromStatus, KpiEvaluationStatus.Approved, null, snapshot);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(e.SubjectUserId, "kpi.approved",
            "تم اعتماد تقييم أدائك", null, "/app/my-kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.approved", nameof(KpiEvaluation), e.Id, ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    // ── ADMIN-GOVERNANCE-R1: معالجة مراجعة تقييمات KPI ──

    public async Task<Result<KpiEvaluationDto>> RequestRevisionAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب طلب التعديل إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        var gate = EnsureCanReview(e);
        if (gate is Result<KpiEvaluationDto> denied) return denied;

        if (e.Status is not (KpiEvaluationStatus.UnderReview or KpiEvaluationStatus.Submitted))
            return Result<KpiEvaluationDto>.Failure("طلب التعديل متاح للتقييم قيد المراجعة فقط.", "kpi_eval.not_reviewable.conflict");

        var fromStatus = e.Status;
        var snapshot = BuildSnapshot(e);
        e.Status = KpiEvaluationStatus.NeedsRevision;
        e.ReviewedAtUtc = DateTime.UtcNow;
        e.ReviewNote = reason;
        e.UpdatedAtUtc = DateTime.UtcNow;
        AddReviewEvent(e, "RequestRevision", fromStatus, KpiEvaluationStatus.NeedsRevision, reason, snapshot);
        await _db.SaveChangesAsync(ct);

        if (e.EvaluatorId is Guid ev)
            await _notifications.NotifyAsync(ev, "kpi.needs_revision",
                "طُلب تعديل تقييم KPI", reason, "/app/kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.needs_revision", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> RejectAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب الرفض إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        var gate = EnsureCanReview(e);
        if (gate is Result<KpiEvaluationDto> denied) return denied;

        if (e.Status is not (KpiEvaluationStatus.UnderReview or KpiEvaluationStatus.Submitted))
            return Result<KpiEvaluationDto>.Failure("الرفض متاح للتقييم قيد المراجعة فقط.", "kpi_eval.not_reviewable.conflict");

        var fromStatus = e.Status;
        var snapshot = BuildSnapshot(e);
        e.Status = KpiEvaluationStatus.Rejected;
        e.ReviewedAtUtc = DateTime.UtcNow;
        e.ReviewNote = reason;
        e.UpdatedAtUtc = DateTime.UtcNow;
        AddReviewEvent(e, "Reject", fromStatus, KpiEvaluationStatus.Rejected, reason, snapshot);
        await _db.SaveChangesAsync(ct);

        if (e.EvaluatorId is Guid ev)
            await _notifications.NotifyAsync(ev, "kpi.rejected", "رُفض تقييم KPI", reason, "/app/kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.rejected", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> CommentAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("نصّ التعليق إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");

        // التعليق متاح للمراجع المختصّ أو لِمن يملك صلاحية الإشارة (HR)، بشرط إمكانيّة العرض.
        if (!_currentUser.IsInAnyRole(Roles.KpiReviewers) && !_currentUser.IsInAnyRole(Roles.KpiReviewFlaggers))
            return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية التعليق على المراجعة.", "auth.forbidden");
        if (!await CanViewAsync(e, ct))
            return Result<KpiEvaluationDto>.Failure("هذا التقييم خارج نطاق صلاحيتك.", "auth.forbidden");

        AddReviewEvent(e, "Comment", e.Status, e.Status, reason, null);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.review_comment", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> FlagForReviewAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب الإشارة إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!_currentUser.IsInAnyRole(Roles.KpiReviewFlaggers))
            return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية الإشارة للمراجعة.", "auth.forbidden");

        // لا تغيير للحالة — إشارة توثيقيّة تُخطر Admin/GM/CEO فقط.
        AddReviewEvent(e, "Flag", e.Status, e.Status, reason, null);
        await _db.SaveChangesAsync(ct);

        var recipients = await UsersInRolesAsync(Roles.AdminReportKpiDeleters, ct);
        await _notifications.NotifyManyAsync(recipients, "kpi.flagged", "تمّت الإشارة لتقييم KPI للمراجعة", reason, "/app/kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.flagged", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> RequestReopenAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب طلب إعادة الفتح إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!_currentUser.IsInAnyRole(Roles.KpiReviewFlaggers))
            return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية طلب إعادة الفتح.", "auth.forbidden");

        // لا تغيير للحالة ولا منح إعادة فتح فعليّة — طلب يُخطر Admin/GM/CEO فقط.
        AddReviewEvent(e, "RequestReopen", e.Status, e.Status, reason, null);
        await _db.SaveChangesAsync(ct);

        var recipients = await UsersInRolesAsync(Roles.AdminReportKpiDeleters, ct);
        await _notifications.NotifyManyAsync(recipients, "kpi.reopen_requested", "طلب إعادة فتح تقييم KPI", reason, "/app/kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.reopen_requested", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> ReopenForRevisionAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب إعادة الفتح إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!_currentUser.IsInAnyRole(Roles.AdminReportKpiDeleters))
            return Result<KpiEvaluationDto>.Failure("إعادة الفتح من صلاحية Admin/CEO/GM فقط.", "auth.forbidden");

        if (e.Status is not (KpiEvaluationStatus.Approved or KpiEvaluationStatus.Rejected or KpiEvaluationStatus.NeedsRevision))
            return Result<KpiEvaluationDto>.Failure("إعادة الفتح متاحة للتقييم المعتمَد أو المرفوض أو المطلوب تعديله فقط.", "kpi_eval.not_reopenable.conflict");

        var fromStatus = e.Status;
        var snapshot = BuildSnapshot(e);

        // إعادة إسناد مُراجِع إن لم يكن معيَّنًا (توافق خلفيّ للسجلّات القديمة).
        // ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1: يُطبَّق تجاوز مراجِع KPI الصريح هنا أيضًا،
        // وإن كان غير صالح لا نتجاهله بصمت بل نُرجِع خطأ إعداد واضحًا.
        if (e.ReviewerId is null)
        {
            var (reopenOutcome, reopenReviewerId) = await ResolveReviewerWithOverrideAsync(e, ct);
            if (reopenOutcome == ReviewerResolution.InvalidOverride)
            {
                await _audit.LogAsync(_currentUser.UserId, "kpi.reviewer_override_invalid",
                    nameof(KpiEvaluation), e.Id, ct: ct);
                return Result<KpiEvaluationDto>.Failure(
                    "إعداد مراجِع KPI غير صالح (المستخدم غير موجود أو غير نشط أو هو الموضوع أو المُدخِل).",
                    "kpi.reviewer_override_invalid");
            }
            e.ReviewerId = reopenReviewerId;
        }

        e.Status = KpiEvaluationStatus.UnderReview;
        e.ReviewedAtUtc = null;
        e.ReviewNote = reason;
        e.UpdatedAtUtc = DateTime.UtcNow;
        AddReviewEvent(e, "Reopen", fromStatus, KpiEvaluationStatus.UnderReview, reason, snapshot);
        await _db.SaveChangesAsync(ct);

        if (e.EvaluatorId is Guid ev)
            await _notifications.NotifyAsync(ev, "kpi.reopened", "أُعيد فتح تقييم KPI للمراجعة", reason, "/app/kpi", ct);
        await _audit.LogAsync(_currentUser.UserId, "kpi.reopened", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<KpiEvaluationDto>> AdminDeleteAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return Result<KpiEvaluationDto>.Failure("سبب الحذف الإداريّ إلزاميّ.", "kpi_eval.reason_required");

        var e = await _db.KpiEvaluations.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<KpiEvaluationDto>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!_currentUser.IsInAnyRole(Roles.AdminReportKpiDeleters))
            return Result<KpiEvaluationDto>.Failure("الحذف الإداريّ من صلاحية Admin/CEO/GM فقط.", "auth.forbidden");
        if (e.IsDeleted)
            return Result<KpiEvaluationDto>.Failure("هذا التقييم محذوف مسبقًا.", "kpi_eval.already_deleted.conflict");

        var fromStatus = e.Status;
        var snapshot = BuildSnapshot(e);
        e.IsDeleted = true;
        e.DeletedAtUtc = DateTime.UtcNow;
        e.DeletedByUserId = _currentUser.UserId;
        e.DeletionReason = reason;
        e.UpdatedAtUtc = DateTime.UtcNow;
        // ملاحظة (تصحيح #10): كل تجميعات KPI تُحتسب حيًّا من الاستعلامات، ويستبعد المحذوف تلقائيًّا عبر Global Query Filter؛
        // لا توجد إجماليّات مخزّنة مؤقّتة تحتاج إعادة احتساب.
        AddReviewEvent(e, "AdminDeleted", fromStatus, null, reason, snapshot);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "kpi.admin_deleted", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new { reason, previousStatus = fromStatus.ToString() }), ct: ct);

        return Result<KpiEvaluationDto>.Success(await BuildDtoAsync(evaluationId, ct));
    }

    public async Task<Result<IReadOnlyList<KpiEvaluationReviewEventDto>>> ListReviewEventsAsync(Guid evaluationId, CancellationToken ct = default)
    {
        // شاشات الحوكمة تعرض حتى المحذوف إداريًّا — نتجاوز Global Query Filter لجلب التقييم.
        var e = await _db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<IReadOnlyList<KpiEvaluationReviewEventDto>>.Failure("التقييم غير موجود.", "kpi_eval.not_found");
        if (!await CanViewAsync(e, ct))
            return Result<IReadOnlyList<KpiEvaluationReviewEventDto>>.Failure("لا تملك صلاحية عرض سجلّ المراجعة.", "auth.forbidden");

        var events = await _db.KpiEvaluationReviewEvents.AsNoTracking()
            .Where(x => x.KpiEvaluationId == evaluationId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.Action, x.ActorId, x.FromStatus, x.ToStatus, x.Reason, x.CreatedAtUtc })
            .ToListAsync(ct);

        var names = await UserNamesAsync(events.Select(x => x.ActorId), ct);
        var dtos = events.Select(x => new KpiEvaluationReviewEventDto(
            x.Id, x.Action, x.ActorId, names.GetValueOrDefault(x.ActorId),
            x.FromStatus, x.ToStatus, x.Reason, x.CreatedAtUtc)).ToList();

        return Result<IReadOnlyList<KpiEvaluationReviewEventDto>>.Success(dtos);
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

        // قاعدة النتائج النهائيّة (تصحيح #7): يدخل التجميع فقط ما كان Approved وغير محذوف
        // (المحذوف مستبعَد تلقائيًّا عبر Global Query Filter). Submitted/UnderReview/NeedsRevision/Rejected/Closed مستبعَدة.
        var q = _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.PeriodType == PeriodType.Weekly
                        && e.TotalScore != null
                        && e.Status == KpiEvaluationStatus.Approved);

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

    // ===== تصدير KPI للمالية (KPI-FIN1) — قراءة/تصدير فقط، لا تغيير أيّ تقييم =====

    public async Task<Result<KpiFinanceExportDto>> GetFinanceExportAsync(KpiFinanceExportFilter filter, CancellationToken ct = default)
        => await BuildFinanceExportAsync(filter, ct);

    public async Task<Result<byte[]>> ExportFinanceCsvAsync(KpiFinanceExportFilter filter, CancellationToken ct = default)
    {
        var built = await BuildFinanceExportAsync(filter, ct);
        if (!built.Succeeded) return Result<byte[]>.Failure(built.Error!, built.ErrorCode);
        var data = built.Value!;

        var sb = new StringBuilder();
        sb.Append("اسم الموظف,الإدارة,الفريق,المسمى الوظيفي,نوع الفترة,مفتاح الفترة,السنة,الربع,القالب المستخدم,الدرجة النهائية,الحالة,تاريخ آخر تحديث / اعتماد\n");
        foreach (var r in data.Rows)
        {
            var score = r.TotalScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
            var updated = r.LastUpdatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            sb.Append(Csv(r.EmployeeName)).Append(',')
              .Append(Csv(r.DepartmentName ?? string.Empty)).Append(',')
              .Append(Csv(r.TeamName ?? string.Empty)).Append(',')
              .Append(Csv(r.JobRoleName ?? string.Empty)).Append(',')
              .Append(Csv(r.PeriodType.ToString())).Append(',')
              .Append(Csv(r.PeriodKey)).Append(',')
              .Append(Csv(r.Year.ToString(CultureInfo.InvariantCulture))).Append(',')
              .Append(Csv(r.Quarter.ToString(CultureInfo.InvariantCulture))).Append(',')
              .Append(Csv(r.TemplateTitle)).Append(',')
              .Append(Csv(score)).Append(',')
              .Append(Csv(r.Status.ToString())).Append(',')
              .Append(Csv(updated)).Append('\n');
        }

        // تدقيق على التصدير فقط — بلا أيّ أسماء أو درجات (وصف الفترة والمرشّحات وعدد الصفوف فقط).
        var auditData = JsonSerializer.Serialize(new
        {
            year = filter.Year,
            quarter = filter.Quarter,
            departmentId = filter.DepartmentId,
            teamId = filter.TeamId,
            status = data.Status.ToString(),
            rowCount = data.RowCount
        });
        await _audit.LogAsync(_currentUser.UserId, "kpi.finance_exported", nameof(KpiEvaluation), null, auditData, ct: ct);

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return Result<byte[]>.Success(bom.Concat(body).ToArray());
    }

    private async Task<Result<KpiFinanceExportDto>> BuildFinanceExportAsync(KpiFinanceExportFilter filter, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<KpiFinanceExportDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (filter.Quarter is < 1 or > 4)
            return Result<KpiFinanceExportDto>.Failure("الربع غير صحيح؛ استخدم قيمة بين 1 و4.", "kpi_finance.quarter_invalid");
        if (filter.Year is < 2000 or > 3000)
            return Result<KpiFinanceExportDto>.Failure("السنة غير صحيحة.", "kpi_finance.year_invalid");

        // الحالة المسموح تصديرها: Approved (افتراضي) أو Closed فقط — أيّ حالة أخرى تُرفَض.
        var status = filter.Status ?? KpiEvaluationStatus.Approved;
        if (status is not (KpiEvaluationStatus.Approved or KpiEvaluationStatus.Closed))
            return Result<KpiFinanceExportDto>.Failure(
                "حالة التصدير غير مسموحة؛ يُسمح بتصدير المعتمد (Approved) أو المغلق (Closed) فقط.",
                "kpi_finance.status_invalid");

        var (from, to) = ReportCalendarPolicy.QuarterRange(filter.Year, filter.Quarter);
        var label = $"الربع {filter.Quarter} — {filter.Year}";

        // عرض على مستوى الشركة (بلا ScopeResolver؛ النطاق مفروض بالسياسة). تقييمات أسبوعية بالحالة المختارة فقط.
        var q = _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.PeriodType == PeriodType.Weekly && e.Status == status);
        if (filter.DepartmentId is Guid d) q = q.Where(e => e.DepartmentId == d);
        if (filter.TeamId is Guid t) q = q.Where(e => e.TeamId == t);

        var raw = await q.Select(e => new
        {
            e.Id,
            e.SubjectUserId,
            e.TeamId,
            e.DepartmentId,
            e.KpiTemplateVersionId,
            e.PeriodType,
            e.PeriodKey,
            e.TotalScore,
            e.Status,
            e.UpdatedAtUtc,
            e.CreatedAtUtc
        }).ToListAsync(ct);

        // فلترة الأسابيع الواقعة داخل مدى الربع (بحسب خميس بداية الأسبوع).
        var inRange = raw.Where(r => ReportCalendarPolicy.WeekInRange(r.PeriodKey, from, to)).ToList();

        // حلّ الأسماء على دفعات: الموظّفون (الاسم/المسمّى/الإدارة/الفريق الحاليّ)، الإدارات، الفِرق، عناوين القوالب.
        var subjectIds = inRange.Select(r => r.SubjectUserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => subjectIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.JobRoleId }).ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.Id);

        var deptIds = inRange.Where(r => r.DepartmentId is not null).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.AsNoTracking().Where(x => deptIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);

        var teamIds = inRange.Where(r => r.TeamId is not null).Select(r => r.TeamId!.Value).Distinct().ToList();
        var teamNames = await _db.Teams.AsNoTracking().Where(x => teamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);

        var jobRoleIds = users.Where(u => u.JobRoleId is not null).Select(u => u.JobRoleId!.Value).Distinct().ToList();
        var jobRoleNames = await _db.JobRoles.AsNoTracking().Where(x => jobRoleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);

        var versionIds = inRange.Select(r => r.KpiTemplateVersionId).Distinct().ToList();
        var templateTitles = await _db.KpiTemplateVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id))
            .Select(v => new { v.Id, Title = v.KpiTemplate!.Title })
            .ToDictionaryAsync(v => v.Id, v => v.Title, ct);

        var rows = inRange
            .OrderBy(r => r.PeriodKey)
            .ThenBy(r => userMap.TryGetValue(r.SubjectUserId, out var u) ? u.FullName : string.Empty)
            .Select(r =>
            {
                userMap.TryGetValue(r.SubjectUserId, out var user);
                Guid? jobRoleId = user?.JobRoleId;
                return new KpiFinanceExportRowDto(
                    r.Id,
                    r.SubjectUserId,
                    user?.FullName ?? string.Empty,
                    r.DepartmentId is Guid dd ? deptNames.GetValueOrDefault(dd) : null,
                    r.TeamId is Guid tt ? teamNames.GetValueOrDefault(tt) : null,
                    jobRoleId is Guid jr ? jobRoleNames.GetValueOrDefault(jr) : null,
                    r.PeriodType,
                    r.PeriodKey,
                    filter.Year,
                    filter.Quarter,
                    templateTitles.GetValueOrDefault(r.KpiTemplateVersionId, string.Empty),
                    r.TotalScore,
                    r.Status,
                    r.UpdatedAtUtc ?? r.CreatedAtUtc);
            })
            .ToList();

        return Result<KpiFinanceExportDto>.Success(
            new KpiFinanceExportDto(filter.Year, filter.Quarter, label, from, to, status, rows.Count, rows));
    }

    // ── مساعِدات المراجعة الحوكميّة (ADMIN-GOVERNANCE-R1) ──

    /// <summary>
    /// حارس معالجة المراجعة: يجب أن يكون المستخدم مُراجِعًا مخوّلًا، وليس الموضوع ولا المُدخِل،
    /// وأن يكون المُراجِع المعيَّن (ReviewerId) أو تصعيدًا أعلى (Admin/CEO/GM). يُرجِع null عند السماح.
    /// </summary>
    private Result<KpiEvaluationDto>? EnsureCanReview(KpiEvaluation e)
    {
        if (!_currentUser.IsInAnyRole(Roles.KpiReviewers))
            return Result<KpiEvaluationDto>.Failure("لا تملك صلاحية معالجة مراجعة التقييم.", "auth.forbidden");
        var uid = _currentUser.UserId;
        if (uid == e.SubjectUserId)
            return Result<KpiEvaluationDto>.Failure("لا يمكنك مراجعة تقييمك الخاصّ.", "auth.forbidden");
        if (uid == e.EvaluatorId)
            return Result<KpiEvaluationDto>.Failure("لا يمكن لمُدخِل التقييم مراجعته.", "auth.forbidden");
        var isElevated = _currentUser.IsInAnyRole(Roles.AdminReportKpiDeleters); // Admin/CEO/GM
        if (!isElevated && e.ReviewerId != uid)
            return Result<KpiEvaluationDto>.Failure(
                "المراجعة من صلاحية المُراجِع المعيَّن أو تصعيد أعلى (Admin/CEO/GM) فقط.", "auth.forbidden");
        return null;
    }

    /// <summary>
    /// نتيجة محاولة إسناد المُراجِع: تمييز صريح بين النجاح، وحالة «المُراجِع الصريح هو المُدخِل نفسه»
    /// (KPI-REVIEWER-OVERRIDE-R1 ⇒ اعتماد مباشر)، وتجاوز صريح غير صالح (خطأ إعداد لا يُتجاهَل بصمت)،
    /// وعدم توفّر أيّ مُراجِع (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1).
    /// </summary>
    private enum ReviewerResolution { Resolved, SelfOverride, InvalidOverride, NoReviewer }

    /// <summary>
    /// إسناد مُراجِع KPI مع مراعاة التجاوز الصريح (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1):
    /// إن كان لموضوع التقييم KpiReviewerOverrideUserId مضبوطًا ⇒ له الأولوية القصوى، ويُقبَل فقط إن كان
    /// المستخدم موجودًا ونشطًا وليس الموضوع نفسه؛ خلاف ذلك يُرجَع InvalidOverride (خطأ إعداد صريح لا
    /// سقوط صامت). KPI-REVIEWER-OVERRIDE-R1: إن كان التجاوز الصريح هو المُدخِل نفسه (Evaluator) ⇒
    /// SelfOverride (اعتماد مباشر عند الإرسال) بدل اعتباره خطأ إعداد أو السقوط إلى ManagerId.
    /// إن كان التجاوز NULL ⇒ يُفوَّض الأمر إلى ResolveReviewerAsync الحاليّ دون أيّ تغيير في سلوكه.
    /// لا يمسّ ManagerId/TeamId ولا الهيكل التنظيمي.
    /// </summary>
    private async Task<(ReviewerResolution Outcome, Guid? ReviewerId)> ResolveReviewerWithOverrideAsync(
        KpiEvaluation e, CancellationToken ct)
    {
        var overrideId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == e.SubjectUserId)
            .Select(u => u.KpiReviewerOverrideUserId)
            .FirstOrDefaultAsync(ct);
        if (overrideId is Guid ovr)
        {
            var isSubject = ovr == e.SubjectUserId;
            var isEvaluator = e.EvaluatorId is Guid evId && ovr == evId;
            var isActive = !isSubject
                && await _db.Users.AsNoTracking().AnyAsync(u => u.Id == ovr && u.IsActive, ct);
            if (!isActive)
                return (ReviewerResolution.InvalidOverride, (Guid?)null);
            return isEvaluator
                ? (ReviewerResolution.SelfOverride, ovr)
                : (ReviewerResolution.Resolved, ovr);
        }

        var resolved = await ResolveReviewerAsync(e, ct);
        return resolved is Guid r
            ? (ReviewerResolution.Resolved, r)
            : (ReviewerResolution.NoReviewer, (Guid?)null);
    }

    /// <summary>
    /// إسناد مُراجِع KPI (ROLE-AWARE-PERSONAL-REPORT-R1 — Phase 6): يتبع سلسلة اعتماد الموضوع نفسها
    /// المستخدَمة في اعتماد التقارير (APPROVAL-FALLBACK-R1)، مُوجَّهة بالبيانات لا بالهويّات المضمّنة:
    /// (1) قائد فريق الموضوع (ما لم يكن الموضوع BypassTeamLeaderApproval=true والفريق/القائد نشط) ثم
    /// المدير المباشر للموضوع (ManagerId نشط). بذلك من ضُبط ManagerId له إلى مسؤول بعينه (+Bypass لقائد
    /// الفريق) تُوجَّه مراجعة KPI إلى ذلك المسؤول مباشرةً بلا مرحلة وسيطة. يستبعد الموضوع والمُدخِل دائمًا.
    /// ثمّ (2) صعود سلسلة مدير المُدخِل (السلوك السابق) و(3) تصعيد بالدور GM←CEO←Admin كاحتياطيّ يضمن
    /// قدر الإمكان عدم إرجاع null. يشترط أن يكون المُراجِع نشطًا في كل الحالات.
    /// </summary>
    private async Task<Guid?> ResolveReviewerAsync(KpiEvaluation e, CancellationToken ct)
    {
        var exclude = new HashSet<Guid> { e.SubjectUserId };
        if (e.EvaluatorId is Guid ev) exclude.Add(ev);

        // 1) سلسلة اعتماد الموضوع (مطابِقة لتوجيه اعتماد التقارير، مُوجَّهة بالبيانات):
        //    قائد فريق الموضوع (ما لم يكن Bypass) ← المدير المباشر للموضوع.
        var subject = await _db.Users.AsNoTracking()
            .Where(u => u.Id == e.SubjectUserId)
            .Select(u => new { u.TeamId, u.ManagerId, u.BypassTeamLeaderApproval })
            .FirstOrDefaultAsync(ct);
        if (subject is not null)
        {
            if (!subject.BypassTeamLeaderApproval && subject.TeamId is Guid stid)
            {
                var tlId = await _db.Teams.AsNoTracking()
                    .Where(t => t.Id == stid && t.IsActive)
                    .Select(t => t.TeamLeaderId).FirstOrDefaultAsync(ct);
                if (tlId is Guid tl && !exclude.Contains(tl)
                    && await _db.Users.AsNoTracking().AnyAsync(u => u.Id == tl && u.IsActive, ct))
                    return tl;
            }
            if (subject.ManagerId is Guid smgr && !exclude.Contains(smgr)
                && await _db.Users.AsNoTracking().AnyAsync(u => u.Id == smgr && u.IsActive, ct))
                return smgr;
        }

        // 2) صعود سلسلة المدير انطلاقًا من المُدخِل (المُقيّم) — احتياطيّ.
        var visited = new HashSet<Guid>();
        Guid? cursor = e.EvaluatorId;
        while (cursor is Guid cid && visited.Add(cid))
        {
            var managerId = await _db.Users.AsNoTracking()
                .Where(u => u.Id == cid).Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
            if (managerId is Guid m && !exclude.Contains(m))
            {
                var active = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == m && u.IsActive, ct);
                if (active) return m;
            }
            cursor = managerId;
        }

        // 2) تصعيد بالدور: GM ثم CEO ثم Admin (break-glass) — أوّل مستخدم نشط بالدور غير مستبعَد.
        foreach (var role in new[] { Roles.GeneralManager, Roles.Ceo, Roles.Admin })
        {
            var candidate = await FirstActiveUserInRoleAsync(role, exclude, ct);
            if (candidate is Guid c) return c;
        }
        return null;
    }

    private async Task<Guid?> FirstActiveUserInRoleAsync(string roleName, HashSet<Guid> exclude, CancellationToken ct)
    {
        var roleId = await _db.Roles.Where(r => r.Name == roleName).Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (roleId == Guid.Empty) return null;
        var excludeList = exclude.ToList();
        var id = await (from ur in _db.UserRoles
                        join u in _db.Users on ur.UserId equals u.Id
                        where ur.RoleId == roleId && u.IsActive && !excludeList.Contains(u.Id)
                        orderby u.FullName
                        select (Guid?)u.Id).FirstOrDefaultAsync(ct);
        return id;
    }

    private async Task<List<Guid>> UsersInRolesAsync(string[] roles, CancellationToken ct)
    {
        var roleIds = await _db.Roles.Where(r => r.Name != null && roles.Contains(r.Name))
            .Select(r => r.Id).ToListAsync(ct);
        if (roleIds.Count == 0) return new List<Guid>();
        return await (from ur in _db.UserRoles
                      join u in _db.Users on ur.UserId equals u.Id
                      where roleIds.Contains(ur.RoleId) && u.IsActive
                      select u.Id).Distinct().ToListAsync(ct);
    }

    private void AddReviewEvent(KpiEvaluation e, string action, KpiEvaluationStatus? from,
        KpiEvaluationStatus? to, string? reason, string? snapshotJson)
    {
        _db.KpiEvaluationReviewEvents.Add(new KpiEvaluationReviewEvent
        {
            KpiEvaluationId = e.Id,
            Action = action,
            ActorId = _currentUser.UserId ?? Guid.Empty,
            FromStatus = from?.ToString(),
            ToStatus = to?.ToString(),
            PreviousValuesJson = snapshotJson,
            Reason = reason
        });
    }

    /// <summary>لقطة قيَم التقييم قبل التغيير (تصحيح #4): الحالة/الدرجة/الاتجاه/المراجع + كل نتائج المؤشرات.</summary>
    private static string BuildSnapshot(KpiEvaluation e) => JsonSerializer.Serialize(new
    {
        status = e.Status.ToString(),
        totalScore = e.TotalScore,
        trend = e.Trend.ToString(),
        reviewerId = e.ReviewerId,
        reviewedAtUtc = e.ReviewedAtUtc,
        results = e.Results.Select(r => new { r.KpiMetricId, r.RawValue, r.Score, r.Weight }).ToList()
    });

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
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

        // الاتجاه يُقارَن بآخر تقييم معتمَد فقط (تصحيح #7) — غير المعتمَد لا يُعدّ نتيجة نهائيّة.
        var priorScore = await _db.KpiEvaluations.AsNoTracking()
            .Where(x => x.SubjectUserId == e.SubjectUserId
                        && x.Id != e.Id
                        && string.Compare(x.PeriodKey, e.PeriodKey) < 0
                        && x.TotalScore != null
                        && x.Status == KpiEvaluationStatus.Approved)
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
        // نتجاوز Global Query Filter كي يعمل بناء الـDTO حتى بعد الحذف الإداريّ الناعم (لشاشة الحوكمة/تأكيد الحذف).
        var e = await _db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking().Include(x => x.Results)
            .FirstAsync(x => x.Id == id, ct);

        var version = await _db.KpiTemplateVersions
            .Where(v => v.Id == e.KpiTemplateVersionId)
            .Select(v => new { v.KpiTemplate!.Title, v.KpiTemplate.Cadence })
            .FirstAsync(ct);

        var metrics = await _db.KpiMetrics.Where(m => m.KpiTemplateVersionId == e.KpiTemplateVersionId)
            .OrderBy(m => m.Order)
            .Select(m => new { m.Id, m.Name, m.Weight, m.TargetValue, m.Unit, m.CalcMethod })
            .ToListAsync(ct);

        var resultDtos = metrics.Select(m =>
        {
            var r = e.Results.FirstOrDefault(x => x.KpiMetricId == m.Id);
            return new KpiResultDto(m.Id, m.Name, m.Weight, m.TargetValue, m.Unit, m.CalcMethod, r?.RawValue, r?.Score, r?.Note);
        }).ToList();

        var ids = new List<Guid> { e.SubjectUserId };
        if (e.EvaluatorId is Guid ev) ids.Add(ev);
        if (e.ReviewerId is Guid rv) ids.Add(rv);
        var names = await UserNamesAsync(ids, ct);

        // التعديل متاح قبل الإرسال أو بعد طلب تعديل (NeedsRevision) لصاحب الإدخال أو الأدمن.
        var canEdit = (e.Status is KpiEvaluationStatus.Draft or KpiEvaluationStatus.InProgress or KpiEvaluationStatus.NeedsRevision)
                      && (_currentUser.UserId == e.EvaluatorId || _currentUser.IsInRole(Roles.Admin));
        var isBelowTarget = e.TotalScore is decimal s && s < AlertThreshold;

        // القدرات السياقيّة (لإظهار/إخفاء أزرار الواجهة؛ الفرض النهائيّ خادميّ في كل دالّة).
        var uid = _currentUser.UserId;
        var isElevated = _currentUser.IsInAnyRole(Roles.AdminReportKpiDeleters); // Admin/CEO/GM
        var reviewable = e.Status is KpiEvaluationStatus.UnderReview or KpiEvaluationStatus.Submitted;
        var canReview = reviewable && _currentUser.IsInAnyRole(Roles.KpiReviewers)
                        && uid != e.SubjectUserId && uid != e.EvaluatorId
                        && (isElevated || e.ReviewerId == uid);
        var canFlag = _currentUser.IsInAnyRole(Roles.KpiReviewFlaggers);
        var canAdminDelete = isElevated && !e.IsDeleted;
        var canReopen = isElevated && e.Status is KpiEvaluationStatus.Approved
                        or KpiEvaluationStatus.Rejected or KpiEvaluationStatus.NeedsRevision;

        return new KpiEvaluationDto(e.Id, e.KpiTemplateVersionId, version.Title, version.Cadence,
            e.SubjectUserId, names.GetValueOrDefault(e.SubjectUserId, string.Empty),
            e.EvaluatorId, e.EvaluatorId is Guid evx ? names.GetValueOrDefault(evx) : null,
            e.TeamId, e.DepartmentId, e.PeriodType, e.PeriodKey, e.Status, e.TotalScore, e.Trend,
            isBelowTarget, e.SubmittedAtUtc, canEdit, resultDtos,
            e.ReviewerId, e.ReviewerId is Guid rvx ? names.GetValueOrDefault(rvx) : null,
            e.ReviewedAtUtc, e.ReviewNote,
            canReview, canFlag, canAdminDelete, canReopen);
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

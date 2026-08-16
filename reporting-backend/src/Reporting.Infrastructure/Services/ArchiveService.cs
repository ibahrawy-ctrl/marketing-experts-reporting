using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Archive;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phases 6/8/9/10/11):
/// قراءة العناصر المحذوفة إداريًّا ناعمًا (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid المعتمَدة.
/// كل القراءات تتجاوز مرشّح الاستعلام العالميّ (IgnoreQueryFilters) وتقتصر على IsDeleted == true.
/// لا حذف نهائيّ، لا جدولة، لا إشعارات/بريد على الاسترجاع.
/// </summary>
public class ArchiveService : IArchiveService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public ArchiveService(AppDbContext db, ICurrentUser currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>نتيجة تقييم قابلية الاسترجاع (مشتركة بين القائمة والتفاصيل والاسترجاع) — بلا أيّ تعديل بيانات.</summary>
    private record RestoreEval(
        bool CanRestore,
        string? BlockedCode,
        string? BlockedReason,
        RestoreStrategy Strategy,
        Guid? HistoricalApproverId,
        string? HistoricalApproverName,
        bool? HistoricalApproverIsActive,
        string? RestoreWarning);

    private record RawRow(
        Guid Id,
        ArchiveItemType Type,
        Guid EmployeeId,
        Guid TemplateVersionId,
        string PeriodKey,
        string Status,
        DateTime DeletedAtUtc,
        Guid? DeletedByUserId,
        string? DeletionReason);

    // ===================== Phase 6 — القائمة =====================

    public async Task<ArchivePagedResult> ListAsync(ArchiveFilter filter, CancellationToken ct = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;

        var rows = new List<RawRow>();

        if (filter.ItemType is null or ArchiveItemType.Report)
        {
            var q = _db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking().Where(s => s.IsDeleted);
            if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(s => s.PeriodKey == filter.PeriodKey);
            if (filter.EmployeeId is Guid rEid) q = q.Where(s => s.SubmitterId == rEid);
            var reports = await q.Select(s => new
            {
                s.Id, s.SubmitterId, s.ReportTemplateVersionId, s.PeriodKey,
                Status = s.Status,
                s.DeletedAtUtc, s.DeletedByUserId, s.DeletionReason
            }).ToListAsync(ct);
            rows.AddRange(reports.Select(r => new RawRow(
                r.Id, ArchiveItemType.Report, r.SubmitterId, r.ReportTemplateVersionId, r.PeriodKey,
                r.Status.ToString(), r.DeletedAtUtc ?? DateTime.MinValue, r.DeletedByUserId, r.DeletionReason)));
        }

        if (filter.ItemType is null or ArchiveItemType.KpiEvaluation)
        {
            var q = _db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking().Where(e => e.IsDeleted);
            if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(e => e.PeriodKey == filter.PeriodKey);
            if (filter.EmployeeId is Guid kEid) q = q.Where(e => e.SubjectUserId == kEid);
            var evals = await q.Select(e => new
            {
                e.Id, e.SubjectUserId, e.KpiTemplateVersionId, e.PeriodKey,
                Status = e.Status,
                e.DeletedAtUtc, e.DeletedByUserId, e.DeletionReason
            }).ToListAsync(ct);
            rows.AddRange(evals.Select(e => new RawRow(
                e.Id, ArchiveItemType.KpiEvaluation, e.SubjectUserId, e.KpiTemplateVersionId, e.PeriodKey,
                e.Status.ToString(), e.DeletedAtUtc ?? DateTime.MinValue, e.DeletedByUserId, e.DeletionReason)));
        }

        var total = rows.Count;
        var pageRows = rows
            .OrderByDescending(r => r.DeletedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // حلّ الأسماء وعناوين القوالب دفعةً واحدة لعناصر الصفحة فقط.
        var userIds = pageRows.Select(r => r.EmployeeId)
            .Concat(pageRows.Where(r => r.DeletedByUserId is Guid).Select(r => r.DeletedByUserId!.Value))
            .Distinct().ToList();
        var names = await NamesAsync(userIds, ct);

        var reportVerIds = pageRows.Where(r => r.Type == ArchiveItemType.Report).Select(r => r.TemplateVersionId).Distinct().ToList();
        var kpiVerIds = pageRows.Where(r => r.Type == ArchiveItemType.KpiEvaluation).Select(r => r.TemplateVersionId).Distinct().ToList();
        var reportTitles = reportVerIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.ReportTemplateVersions.AsNoTracking().Where(v => reportVerIds.Contains(v.Id))
                .Select(v => new { v.Id, Title = v.ReportTemplate!.Title }).ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var kpiTitles = kpiVerIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.KpiTemplateVersions.AsNoTracking().Where(v => kpiVerIds.Contains(v.Id))
                .Select(v => new { v.Id, Title = v.KpiTemplate!.Title }).ToDictionaryAsync(x => x.Id, x => x.Title, ct);

        // تقييم قابلية الاسترجاع لعناصر الصفحة (تقارير تحتاج خطوات الاعتماد، KPI فحص تعارض فقط).
        var pageReportIds = pageRows.Where(r => r.Type == ArchiveItemType.Report).Select(r => r.Id).ToList();
        var reportEntities = pageReportIds.Count == 0 ? new List<ReportSubmission>()
            : await _db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
                .Include(s => s.ApprovalSteps)
                .Where(s => pageReportIds.Contains(s.Id)).ToListAsync(ct);
        var pageKpiIds = pageRows.Where(r => r.Type == ArchiveItemType.KpiEvaluation).Select(r => r.Id).ToList();
        var kpiEntities = pageKpiIds.Count == 0 ? new List<KpiEvaluation>()
            : await _db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking()
                .Where(e => pageKpiIds.Contains(e.Id)).ToListAsync(ct);

        var evalMap = new Dictionary<Guid, RestoreEval>();
        foreach (var s in reportEntities) evalMap[s.Id] = await EvaluateReportAsync(s, ct);
        foreach (var e in kpiEntities) evalMap[e.Id] = await EvaluateKpiAsync(e, ct);

        var items = new List<ArchiveItemDto>(pageRows.Count);
        foreach (var r in pageRows)
        {
            var (days, retention) = Retention(r.DeletedAtUtc);
            var eval = evalMap.GetValueOrDefault(r.Id)
                ?? new RestoreEval(false, "archive.restore_resolution_required.conflict", "تعذّر تقييم الاسترجاع.", RestoreStrategy.NotApplicable, null, null, null, null);
            var templateName = r.Type == ArchiveItemType.Report
                ? reportTitles.GetValueOrDefault(r.TemplateVersionId, "—")
                : kpiTitles.GetValueOrDefault(r.TemplateVersionId, "—");
            items.Add(new ArchiveItemDto(
                r.Id, r.Type, r.EmployeeId, names.GetValueOrDefault(r.EmployeeId, "—"),
                templateName, r.PeriodKey, r.Status, r.DeletedAtUtc, r.DeletedByUserId,
                r.DeletedByUserId is Guid dby ? names.GetValueOrDefault(dby) : null,
                r.DeletionReason, eval.CanRestore, eval.BlockedCode, eval.BlockedReason, days, retention));
        }

        return new ArchivePagedResult(items, total, page, pageSize);
    }

    // ===================== Phase 6 — التفاصيل =====================

    public async Task<Result<ArchiveDetailsDto>> GetDetailsAsync(ArchiveItemType itemType, Guid itemId, CancellationToken ct = default)
    {
        if (!HasAccess())
            return Result<ArchiveDetailsDto>.Failure("الوصول إلى الأرشيف الإداريّ من صلاحية Admin/CEO/GM فقط.", "auth.forbidden");

        return itemType == ArchiveItemType.Report
            ? await BuildReportDetailsAsync(itemId, ct)
            : await BuildKpiDetailsAsync(itemId, ct);
    }

    private async Task<Result<ArchiveDetailsDto>> BuildReportDetailsAsync(Guid submissionId, CancellationToken ct, bool allowNonDeleted = false)
    {
        var s = await _db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.ApprovalSteps)
            .FirstOrDefaultAsync(x => x.Id == submissionId, ct);
        if (s is null) return Result<ArchiveDetailsDto>.Failure("العنصر غير موجود.", "archive.report.not_found");
        // allowNonDeleted يُستخدم فقط من مسار الاسترجاع بعد عكس الحذف (العنصر لم يعد محذوفًا عمدًا).
        if (!s.IsDeleted && !allowNonDeleted) return Result<ArchiveDetailsDto>.Failure("العنصر ليس محذوفًا إداريًّا.", "archive.report_not_deleted.conflict");

        var eval = await EvaluateReportAsync(s, ct);
        var templateName = await ReportTemplateNameAsync(s.ReportTemplateVersionId, ct);
        var fieldValuesCount = await _db.SubmissionFieldValues.AsNoTracking().CountAsync(v => v.ReportSubmissionId == s.Id, ct);

        var stepUserIds = s.ApprovalSteps.Select(a => a.ApproverId).ToList();
        var userIds = stepUserIds
            .Append(s.SubmitterId)
            .Concat(s.DeletedByUserId is Guid d ? new[] { d } : Array.Empty<Guid>())
            .Concat(s.CurrentApproverId is Guid c ? new[] { c } : Array.Empty<Guid>())
            .Distinct().ToList();
        var names = await NamesAsync(userIds, ct);

        var steps = s.ApprovalSteps.OrderBy(a => a.Level).Select(a => new ArchiveWorkflowStepDto(
            a.Level, a.ApproverId, names.GetValueOrDefault(a.ApproverId), a.Status.ToString(), a.Comment, a.DecidedAtUtc)).ToList();

        var audit = await AuditTrailAsync(nameof(ReportSubmission), s.Id, ct);
        var (days, retention) = Retention(s.DeletedAtUtc ?? DateTime.MinValue);

        var dto = new ArchiveDetailsDto(
            s.Id, ArchiveItemType.Report, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId, "—"),
            templateName, s.PeriodKey, s.Status.ToString(), s.DeletedAtUtc ?? DateTime.MinValue, s.DeletedByUserId,
            s.DeletedByUserId is Guid dby ? names.GetValueOrDefault(dby) : null,
            s.DeletionReason, eval.CanRestore, eval.BlockedCode, eval.BlockedReason, days, retention,
            s.CurrentApproverId, s.CurrentApproverId is Guid ca ? names.GetValueOrDefault(ca) : null,
            steps, fieldValuesCount, 0, 0, audit,
            eval.HistoricalApproverId, eval.HistoricalApproverName, eval.HistoricalApproverIsActive,
            eval.Strategy, eval.RestoreWarning);

        return Result<ArchiveDetailsDto>.Success(dto);
    }

    private async Task<Result<ArchiveDetailsDto>> BuildKpiDetailsAsync(Guid evaluationId, CancellationToken ct, bool allowNonDeleted = false)
    {
        var e = await _db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<ArchiveDetailsDto>.Failure("العنصر غير موجود.", "archive.kpi.not_found");
        // allowNonDeleted يُستخدم فقط من مسار الاسترجاع بعد عكس الحذف (العنصر لم يعد محذوفًا عمدًا).
        if (!e.IsDeleted && !allowNonDeleted) return Result<ArchiveDetailsDto>.Failure("العنصر ليس محذوفًا إداريًّا.", "archive.kpi_not_deleted.conflict");

        var eval = await EvaluateKpiAsync(e, ct);
        var templateName = await KpiTemplateNameAsync(e.KpiTemplateVersionId, ct);
        var resultsCount = await _db.KpiResults.AsNoTracking().CountAsync(r => r.KpiEvaluationId == e.Id, ct);
        var reviewEventsCount = await _db.KpiEvaluationReviewEvents.AsNoTracking().CountAsync(x => x.KpiEvaluationId == e.Id, ct);

        var userIds = new[] { e.SubjectUserId }
            .Concat(e.DeletedByUserId is Guid d ? new[] { d } : Array.Empty<Guid>())
            .Distinct().ToList();
        var names = await NamesAsync(userIds, ct);

        var audit = await AuditTrailAsync(nameof(KpiEvaluation), e.Id, ct);
        var (days, retention) = Retention(e.DeletedAtUtc ?? DateTime.MinValue);

        var dto = new ArchiveDetailsDto(
            e.Id, ArchiveItemType.KpiEvaluation, e.SubjectUserId, names.GetValueOrDefault(e.SubjectUserId, "—"),
            templateName, e.PeriodKey, e.Status.ToString(), e.DeletedAtUtc ?? DateTime.MinValue, e.DeletedByUserId,
            e.DeletedByUserId is Guid dby ? names.GetValueOrDefault(dby) : null,
            e.DeletionReason, eval.CanRestore, eval.BlockedCode, eval.BlockedReason, days, retention,
            null, null,
            Array.Empty<ArchiveWorkflowStepDto>(), 0, resultsCount, reviewEventsCount, audit,
            null, null, null,
            eval.Strategy, eval.RestoreWarning);

        return Result<ArchiveDetailsDto>.Success(dto);
    }

    // ===================== Phase 8 — استرجاع التقرير (Hybrid) =====================

    public async Task<Result<ArchiveDetailsDto>> RestoreReportAsync(Guid submissionId, RestoreRequest request, CancellationToken ct = default)
    {
        if (!HasAccess())
            return Result<ArchiveDetailsDto>.Failure("الاسترجاع من صلاحية Admin/CEO/GM فقط.", "auth.forbidden");

        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length is < 10 or > 500)
            return Result<ArchiveDetailsDto>.Failure("سبب الاسترجاع إلزاميّ (10–500 محرفًا).", "archive.restore_reason_invalid");

        var s = await _db.ReportSubmissions.IgnoreQueryFilters()
            .Include(x => x.ApprovalSteps)
            .FirstOrDefaultAsync(x => x.Id == submissionId, ct);
        if (s is null) return Result<ArchiveDetailsDto>.Failure("العنصر غير موجود.", "archive.report.not_found");
        if (!s.IsDeleted) return Result<ArchiveDetailsDto>.Failure("العنصر ليس محذوفًا إداريًّا.", "archive.report_not_deleted.conflict");

        var eval = await EvaluateReportAsync(s, ct);
        if (!eval.CanRestore)
            return Result<ArchiveDetailsDto>.Failure(eval.BlockedReason ?? "تعذّر الاسترجاع.", eval.BlockedCode ?? "archive.restore_resolution_required.conflict");

        var now = DateTime.UtcNow;
        var before = new
        {
            isDeleted = s.IsDeleted,
            status = s.Status.ToString(),
            currentApproverId = s.CurrentApproverId,
            deletedAtUtc = s.DeletedAtUtc,
            deletedByUserId = s.DeletedByUserId
        };

        // عكس الحذف الإداريّ الناعم (لا تغيير في عمود Status).
        s.IsDeleted = false;
        s.DeletedAtUtc = null;
        s.DeletedByUserId = null;
        s.DeletionReason = null;
        s.UpdatedAtUtc = now;

        if (eval.Strategy == RestoreStrategy.HistoricalApproverRestored && eval.HistoricalApproverId is Guid approverId)
        {
            // إعادة الخطوات الملغاة بالحذف الإداريّ إلى Pending + إعادة تعيين المعتمِد التاريخيّ الصالح.
            foreach (var step in s.ApprovalSteps.Where(a => a.Status == ApprovalStatus.CancelledByAdministrativeDeletion))
            {
                step.Status = ApprovalStatus.Pending;
                step.DecidedAtUtc = null;
            }
            s.CurrentApproverId = approverId;
        }
        // NoActiveApprover: يبقى CurrentApproverId = null (لا توجيه آليّ لمعتمِد حاليّ).

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "archive_item_restored", nameof(ReportSubmission), s.Id,
            JsonSerializer.Serialize(new
            {
                reason,
                itemType = ArchiveItemType.Report.ToString(),
                strategy = eval.Strategy.ToString(),
                historicalApproverId = eval.HistoricalApproverId,
                before,
                after = new
                {
                    isDeleted = s.IsDeleted,
                    status = s.Status.ToString(),
                    currentApproverId = s.CurrentApproverId
                }
            }), ct: ct);

        return await BuildReportDetailsAsync(submissionId, ct, allowNonDeleted: true);
    }

    // ===================== Phase 9 — استرجاع KPI =====================

    public async Task<Result<ArchiveDetailsDto>> RestoreKpiAsync(Guid evaluationId, RestoreRequest request, CancellationToken ct = default)
    {
        if (!HasAccess())
            return Result<ArchiveDetailsDto>.Failure("الاسترجاع من صلاحية Admin/CEO/GM فقط.", "auth.forbidden");

        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length is < 10 or > 500)
            return Result<ArchiveDetailsDto>.Failure("سبب الاسترجاع إلزاميّ (10–500 محرفًا).", "archive.restore_reason_invalid");

        var e = await _db.KpiEvaluations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == evaluationId, ct);
        if (e is null) return Result<ArchiveDetailsDto>.Failure("العنصر غير موجود.", "archive.kpi.not_found");
        if (!e.IsDeleted) return Result<ArchiveDetailsDto>.Failure("العنصر ليس محذوفًا إداريًّا.", "archive.kpi_not_deleted.conflict");

        var eval = await EvaluateKpiAsync(e, ct);
        if (!eval.CanRestore)
            return Result<ArchiveDetailsDto>.Failure(eval.BlockedReason ?? "تعذّر الاسترجاع.", eval.BlockedCode ?? "archive.restore_active_conflict.conflict");

        var now = DateTime.UtcNow;
        var fromStatus = e.Status;
        var before = new
        {
            isDeleted = e.IsDeleted,
            status = e.Status.ToString(),
            deletedAtUtc = e.DeletedAtUtc,
            deletedByUserId = e.DeletedByUserId
        };

        e.IsDeleted = false;
        e.DeletedAtUtc = null;
        e.DeletedByUserId = null;
        e.DeletionReason = null;
        e.UpdatedAtUtc = now;

        // حدث مراجعة يوثّق الاسترجاع الإداريّ في سجلّ أحداث التقييم.
        _db.KpiEvaluationReviewEvents.Add(new KpiEvaluationReviewEvent
        {
            KpiEvaluationId = e.Id,
            Action = "AdminRestored",
            ActorId = _currentUser.UserId ?? Guid.Empty,
            FromStatus = fromStatus.ToString(),
            ToStatus = e.Status.ToString(),
            PreviousValuesJson = null,
            Reason = reason
        });

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "archive_item_restored", nameof(KpiEvaluation), e.Id,
            JsonSerializer.Serialize(new
            {
                reason,
                itemType = ArchiveItemType.KpiEvaluation.ToString(),
                strategy = RestoreStrategy.NotApplicable.ToString(),
                before,
                after = new { isDeleted = e.IsDeleted, status = e.Status.ToString() }
            }), ct: ct);

        return await BuildKpiDetailsAsync(evaluationId, ct, allowNonDeleted: true);
    }

    // ===================== تقييم الاسترجاع (بلا تعديل) =====================

    private async Task<RestoreEval> EvaluateReportAsync(ReportSubmission s, CancellationToken ct)
    {
        // تعارض: تسليم نشط آخر لنفس (القالب، الموظّف، الفترة) يمنع الاسترجاع دون قرار.
        var conflict = await _db.ReportSubmissions.IgnoreQueryFilters().AnyAsync(a =>
            !a.IsDeleted && a.Id != s.Id
            && a.ReportTemplateVersionId == s.ReportTemplateVersionId
            && a.SubmitterId == s.SubmitterId
            && a.PeriodKey == s.PeriodKey, ct);
        if (conflict)
            return Blocked("archive.restore_active_conflict.conflict",
                "يوجد تسليم نشط آخر لنفس القالب والموظّف والفترة؛ لا يمكن الاسترجاع دون حلّ التعارض.");

        var cancelledAtDelete = s.ApprovalSteps
            .Where(a => a.Status == ApprovalStatus.CancelledByAdministrativeDeletion).ToList();

        if (cancelledAtDelete.Count > 0)
        {
            var approverIds = cancelledAtDelete.Select(a => a.ApproverId).Distinct().ToList();
            if (approverIds.Count > 1)
                return Blocked("archive.restore_resolution_required.conflict",
                    "المسار التاريخيّ يحوي أكثر من معتمِد معلّق؛ يلزم قرار إداريّ لتحديد التوجيه.");

            var approverId = approverIds[0];
            var approver = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == approverId, ct);
            if (approver is null)
                return new RestoreEval(false, "archive.restore_approver_missing.conflict",
                    "المعتمِد التاريخيّ لم يعد موجودًا في النظام؛ يلزم قرار إداريّ لإعادة التوجيه.",
                    RestoreStrategy.HistoricalApproverRestored, approverId, null, null, null);
            if (!approver.IsActive)
                return new RestoreEval(false, "archive.restore_approver_inactive.conflict",
                    "المعتمِد التاريخيّ موقوف؛ يلزم إعادة تفعيله أو قرار توجيه إداريّ منفصل.",
                    RestoreStrategy.HistoricalApproverRestored, approverId, approver.FullName, false, null);

            return new RestoreEval(true, null, null, RestoreStrategy.HistoricalApproverRestored,
                approverId, approver.FullName, true, null);
        }

        // لا خطوات معلّقة محفوظة: إن كانت الحالة تشير إلى مسار نشط فالتقاط اللقطة ناقص.
        if (s.Status is SubmissionStatus.Submitted or SubmissionStatus.Escalated)
            return Blocked("archive.restore_snapshot_missing.conflict",
                "الحالة تشير إلى مسار اعتماد نشط لكن لا توجد خطوات معلّقة محفوظة؛ يلزم قرار إداريّ.");

        // حالة نهائية/مسودّة/معادة: يُسترجَع بلا معتمِد حاليّ (لا مسار نشط).
        return new RestoreEval(true, null, null, RestoreStrategy.NoActiveApprover, null, null, null,
            "سيُسترجَع التقرير بحالته السابقة دون معتمِد حاليّ (لا مسار اعتماد نشط).");
    }

    private async Task<RestoreEval> EvaluateKpiAsync(KpiEvaluation e, CancellationToken ct)
    {
        var conflict = await _db.KpiEvaluations.IgnoreQueryFilters().AnyAsync(a =>
            !a.IsDeleted && a.Id != e.Id
            && a.KpiTemplateVersionId == e.KpiTemplateVersionId
            && a.SubjectUserId == e.SubjectUserId
            && a.PeriodKey == e.PeriodKey, ct);
        if (conflict)
            return Blocked("archive.restore_active_conflict.conflict",
                "يوجد تقييم نشط آخر لنفس القالب والموظّف والفترة؛ لا يمكن الاسترجاع دون حلّ التعارض.");

        return new RestoreEval(true, null, null, RestoreStrategy.NotApplicable, null, null, null, null);
    }

    private static RestoreEval Blocked(string code, string reason) =>
        new(false, code, reason, RestoreStrategy.NotApplicable, null, null, null, null);

    // ===================== Phase 11 — الاحتفاظ الزمنيّ =====================

    private static (int Days, RetentionStatus Status) Retention(DateTime deletedAtUtc)
    {
        var days = (int)(DateTime.UtcNow - deletedAtUtc).TotalDays;
        if (days < 0) days = 0;
        var status = days < 30 ? RetentionStatus.Fresh
            : days <= 90 ? RetentionStatus.ReviewDue
            : RetentionStatus.LongTerm;
        return (days, status);
    }

    // ===================== أدوات مساعدة =====================

    private bool HasAccess() => _currentUser.IsInAnyRole(Roles.ArchiveGovernanceAccessors);

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking().Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<string> ReportTemplateNameAsync(Guid versionId, CancellationToken ct)
        => await _db.ReportTemplateVersions.AsNoTracking().Where(v => v.Id == versionId)
            .Select(v => v.ReportTemplate!.Title).FirstOrDefaultAsync(ct) ?? "—";

    private async Task<string> KpiTemplateNameAsync(Guid versionId, CancellationToken ct)
        => await _db.KpiTemplateVersions.AsNoTracking().Where(v => v.Id == versionId)
            .Select(v => v.KpiTemplate!.Title).FirstOrDefaultAsync(ct) ?? "—";

    private async Task<IReadOnlyList<ArchiveAuditEntryDto>> AuditTrailAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new { a.Id, a.Action, a.ActorId, a.CreatedAtUtc, a.DataJson })
            .ToListAsync(ct);
        var names = await NamesAsync(logs.Where(l => l.ActorId is Guid).Select(l => l.ActorId!.Value), ct);
        return logs.Select(l => new ArchiveAuditEntryDto(
            l.Id, l.Action, l.ActorId, l.ActorId is Guid a ? names.GetValueOrDefault(a) : null,
            l.CreatedAtUtc, l.DataJson)).ToList();
    }
}

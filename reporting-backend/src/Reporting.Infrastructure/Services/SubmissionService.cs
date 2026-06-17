using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Submissions;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class SubmissionService : ISubmissionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IScopeResolver _scope;
    private readonly IClientProjectAccess _access;

    public SubmissionService(AppDbContext db, ICurrentUser currentUser,
        INotificationService notifications, IAuditService audit, IScopeResolver scope, IClientProjectAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _scope = scope;
        _access = access;
    }

    public async Task<Result<SubmissionDto>> CreateOrGetDraftAsync(CreateSubmissionRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<SubmissionDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.PeriodKey))
            return Result<SubmissionDto>.Failure("مفتاح الفترة مطلوب.", "submission.period_required");

        var version = await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == request.ReportTemplateId && v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null)
            return Result<SubmissionDto>.Failure("لا يوجد إصدار منشور لهذا القالب.", "template.no_published_version.conflict");

        var periodKey = request.PeriodKey.Trim();
        var existing = await _db.ReportSubmissions.FirstOrDefaultAsync(
            s => s.ReportTemplateVersionId == version.Id && s.SubmitterId == userId && s.PeriodKey == periodKey, ct);
        if (existing is not null)
            return Result<SubmissionDto>.Success(await BuildDtoAsync(existing.Id, ct));

        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        // حارس الدورية: تُفرَض دورية التقرير بحسب المسمّى الوظيفي للمُرسِل (مبيعات B2C/B2B = يومي، غيرهم = أسبوعي).
        var jobRoleCode = me?.JobRoleId is Guid jrid
            ? await _db.JobRoles.Where(j => j.Id == jrid).Select(j => j.Code).FirstOrDefaultAsync(ct)
            : null;
        var expectedCadence = ReportCadencePolicy.ExpectedCadence(jobRoleCode);
        if (request.PeriodType != expectedCadence)
            return Result<SubmissionDto>.Failure(
                expectedCadence == PeriodType.Daily
                    ? "تقارير مندوب المبيعات يومية فقط في المرحلة الحالية."
                    : "دورية هذا التقرير أسبوعية في المرحلة الحالية.",
                "submission.cadence_invalid");

        // منع ازدواج التقرير الأساسي (Phase 4 §4): لكل موظف تقرير أساسي واحد مطلوب لكل فترة.
        // قسم النبض يكون مضمَّنًا داخل التقرير الأساسي، أو قالبًا تكميليًا (Supplementary) لا يُحتسب
        // تقريرًا أساسيًا ثانيًا. القوالب التكميلية لا يطبّق عليها هذا الحارس.
        var classification = await _db.ReportTemplates
            .Where(t => t.Id == request.ReportTemplateId)
            .Select(t => t.Classification)
            .FirstAsync(ct);
        if (classification == TemplateClassification.Primary)
        {
            var hasOtherPrimary = await _db.ReportSubmissions
                .Where(s => s.SubmitterId == userId && s.PeriodKey == periodKey && s.PeriodType == expectedCadence)
                .Join(_db.ReportTemplateVersions, s => s.ReportTemplateVersionId, v => v.Id, (s, v) => v.ReportTemplateId)
                .Join(_db.ReportTemplates, tid => tid, t => t.Id, (tid, t) => t)
                .AnyAsync(t => t.Classification == TemplateClassification.Primary && t.Id != request.ReportTemplateId, ct);
            if (hasOtherPrimary)
                return Result<SubmissionDto>.Failure(
                    "لديك تقرير أساسي مطلوب لهذه الفترة بالفعل؛ لا يُسمح بتقريرين أساسيين مطلوبين لنفس الفترة. استخدم قسم النبض المضمَّن داخل تقريرك الأساسي أو قالبًا تكميليًا/اختياريًا.",
                    "submission.primary_duplicate.conflict");
        }

        // ربط اختياري بمشروع (Phase 6 §3): إن حُدِّد مشروع، يجب أن يكون موجودًا وضمن نطاق رؤية المُرسِل،
        // ويُشتقّ العميل تلقائيًا من المشروع كي تتجمّع تقارير العميل.
        Guid? linkedProjectId = null;
        Guid? linkedClientId = null;
        if (request.ProjectId is Guid pid)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (project is null)
                return Result<SubmissionDto>.Failure("المشروع غير موجود.", "project.not_found");
            var vis = await _access.ResolveAsync(ct);
            if (!vis.CanViewProject(pid))
                return Result<SubmissionDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");
            linkedProjectId = project.Id;
            linkedClientId = project.ClientId;
        }

        var submission = new ReportSubmission
        {
            ReportTemplateVersionId = version.Id,
            SubmitterId = userId,
            TeamId = me?.TeamId,
            DepartmentId = me?.DepartmentId,
            PeriodType = expectedCadence,
            PeriodKey = periodKey,
            Status = SubmissionStatus.Draft,
            ProjectId = linkedProjectId,
            ClientId = linkedClientId
        };
        _db.ReportSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submission.Id, ct));
    }

    public async Task<Result<SubmissionDto>> GetAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await _db.ReportSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result<SubmissionDto>.Failure("التسليم غير موجود.", "submission.not_found");

        if (!await CanViewAsync(submission, ct))
            return Result<SubmissionDto>.Failure("لا تملك صلاحية الوصول لهذا التسليم.", "auth.forbidden");

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    public async Task<Result<SubmissionDto>> SaveFieldValuesAsync(Guid submissionId, SaveFieldValuesRequest request, CancellationToken ct = default)
    {
        var submission = await _db.ReportSubmissions.Include(s => s.FieldValues)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result<SubmissionDto>.Failure("التسليم غير موجود.", "submission.not_found");

        var ownerCheck = ResourceGuard.EnsureOwnerOrElevated(_currentUser, submission.SubmitterId);
        if (!ownerCheck.Succeeded) return Result<SubmissionDto>.Failure(ownerCheck.Error!, ownerCheck.ErrorCode!);

        if (submission.Status is not (SubmissionStatus.Draft or SubmissionStatus.Returned))
            return Result<SubmissionDto>.Failure("لا يمكن تعديل تسليم بعد إرساله.", "submission.locked.conflict");

        var fieldIds = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == submission.ReportTemplateVersionId)
            .Select(f => f.Id).ToListAsync(ct);

        foreach (var input in request.Values)
        {
            if (!fieldIds.Contains(input.TemplateFieldId)) continue;
            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == input.TemplateFieldId);
            if (value is null)
            {
                value = new SubmissionFieldValue
                {
                    ReportSubmissionId = submission.Id,
                    TemplateFieldId = input.TemplateFieldId
                };
                _db.SubmissionFieldValues.Add(value);
            }
            value.ValueText = input.ValueText;
            value.ValueNumber = input.ValueNumber;
            value.ValueDate = input.ValueDate;
            value.ValueBool = input.ValueBool;
            value.ValueJson = input.ValueJson;
            value.UpdatedAtUtc = DateTime.UtcNow;
        }

        submission.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    public async Task<Result<SubmissionDto>> SubmitAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await _db.ReportSubmissions.Include(s => s.FieldValues).Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result<SubmissionDto>.Failure("التسليم غير موجود.", "submission.not_found");

        var ownerCheck = ResourceGuard.EnsureOwnerOrElevated(_currentUser, submission.SubmitterId);
        if (!ownerCheck.Succeeded) return Result<SubmissionDto>.Failure(ownerCheck.Error!, ownerCheck.ErrorCode!);

        if (submission.Status is not (SubmissionStatus.Draft or SubmissionStatus.Returned))
            return Result<SubmissionDto>.Failure("التسليم في حالة لا تسمح بالإرسال.", "submission.not_submittable.conflict");

        var requiredFields = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == submission.ReportTemplateVersionId
                        && f.IsRequired && f.FieldType != FieldType.SectionHeader)
            .Select(f => new { f.Id, f.Label }).ToListAsync(ct);

        var missing = requiredFields
            .Where(f => !HasValue(submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == f.Id)))
            .Select(f => f.Label).ToList();
        if (missing.Count > 0)
            return Result<SubmissionDto>.Failure($"حقول مطلوبة غير مكتملة: {string.Join("، ", missing)}", "submission.required_fields_missing");

        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == submission.SubmitterId, ct);
        var managerId = me?.ManagerId;

        submission.Status = SubmissionStatus.Submitted;
        submission.SubmittedAtUtc = DateTime.UtcNow;
        submission.UpdatedAtUtc = DateTime.UtcNow;

        if (managerId is Guid approverId && approverId != Guid.Empty)
        {
            submission.CurrentApproverId = approverId;
            var nextLevel = (submission.ApprovalSteps.Count == 0 ? 0 : submission.ApprovalSteps.Max(a => a.Level)) + 1;
            _db.ApprovalSteps.Add(new ApprovalStep
            {
                ReportSubmissionId = submission.Id,
                Level = nextLevel,
                ApproverId = approverId,
                Status = ApprovalStatus.Pending
            });
        }
        else
        {
            // لا يوجد مدير مباشر — يُغلق التسليم مباشرة.
            submission.Status = SubmissionStatus.Closed;
            submission.ClosedAtUtc = DateTime.UtcNow;
            submission.CurrentApproverId = null;
        }

        await _db.SaveChangesAsync(ct);

        if (submission.CurrentApproverId is Guid approver)
            await _notifications.NotifyAsync(approver, "submission.submitted",
                "تقرير بانتظار اعتمادك", null, $"/submissions/{submission.Id}", ct);
        await _audit.LogAsync(_currentUser.UserId, "submission.submitted", nameof(ReportSubmission), submission.Id, ct: ct);

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    public Task<Result<SubmissionDto>> ApproveAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default)
        => DecideAsync(submissionId, ApprovalStatus.Approved, request.Comment, ct);

    public Task<Result<SubmissionDto>> ReturnAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default)
        => DecideAsync(submissionId, ApprovalStatus.Returned, request.Comment, ct);

    public Task<Result<SubmissionDto>> EscalateAsync(Guid submissionId, ApprovalActionRequest request, CancellationToken ct = default)
        => DecideAsync(submissionId, ApprovalStatus.Escalated, request.Comment, ct);

    private async Task<Result<SubmissionDto>> DecideAsync(Guid submissionId, ApprovalStatus decision, string? comment, CancellationToken ct)
    {
        var submission = await _db.ReportSubmissions.Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result<SubmissionDto>.Failure("التسليم غير موجود.", "submission.not_found");

        if (_currentUser.UserId is not Guid userId)
            return Result<SubmissionDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var isCurrentApprover = submission.CurrentApproverId == userId;
        if (!isCurrentApprover && !_currentUser.IsInRole(Roles.Admin))
            return Result<SubmissionDto>.Failure("لست الموافِق الحالي لهذا التسليم.", "auth.forbidden");

        if (submission.Status is not (SubmissionStatus.Submitted or SubmissionStatus.ApprovedByDirectManager
            or SubmissionStatus.ApprovedByNextLevel or SubmissionStatus.Escalated))
            return Result<SubmissionDto>.Failure("التسليم في حالة لا تسمح باتخاذ قرار.", "submission.not_actionable.conflict");

        var step = submission.ApprovalSteps
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.Level)
            .FirstOrDefault();
        if (step is null)
            return Result<SubmissionDto>.Failure("لا توجد خطوة اعتماد قيد الانتظار.", "submission.no_pending_step.conflict");

        step.Status = decision;
        step.Comment = comment;
        step.DecidedAtUtc = DateTime.UtcNow;
        submission.UpdatedAtUtc = DateTime.UtcNow;

        var approver = await _db.Users.FirstOrDefaultAsync(u => u.Id == step.ApproverId, ct);
        var nextApproverId = approver?.ManagerId;

        switch (decision)
        {
            case ApprovalStatus.Returned:
                submission.Status = SubmissionStatus.Returned;
                submission.CurrentApproverId = null;
                break;

            case ApprovalStatus.Escalated:
                if (nextApproverId is not Guid escalateTo || escalateTo == Guid.Empty)
                    return Result<SubmissionDto>.Failure("لا يوجد مستوى أعلى للتصعيد إليه.", "submission.no_escalation_target.conflict");
                submission.Status = SubmissionStatus.Escalated;
                submission.CurrentApproverId = escalateTo;
                _db.ApprovalSteps.Add(new ApprovalStep
                {
                    ReportSubmissionId = submission.Id,
                    Level = step.Level + 1,
                    ApproverId = escalateTo,
                    Status = ApprovalStatus.Pending
                });
                break;

            case ApprovalStatus.Approved:
                if (nextApproverId is Guid nextId && nextId != Guid.Empty)
                {
                    submission.Status = submission.Status switch
                    {
                        SubmissionStatus.Submitted => SubmissionStatus.ApprovedByDirectManager,
                        SubmissionStatus.ApprovedByDirectManager => SubmissionStatus.ApprovedByNextLevel,
                        _ => SubmissionStatus.ApprovedByNextLevel
                    };
                    submission.CurrentApproverId = nextId;
                    _db.ApprovalSteps.Add(new ApprovalStep
                    {
                        ReportSubmissionId = submission.Id,
                        Level = step.Level + 1,
                        ApproverId = nextId,
                        Status = ApprovalStatus.Pending
                    });
                }
                else
                {
                    submission.Status = SubmissionStatus.Closed;
                    submission.ClosedAtUtc = DateTime.UtcNow;
                    submission.CurrentApproverId = null;
                }
                break;
        }

        await _db.SaveChangesAsync(ct);

        switch (decision)
        {
            case ApprovalStatus.Returned:
                await _notifications.NotifyAsync(submission.SubmitterId, "submission.returned",
                    "أُعيد تقريرك للتعديل", comment, $"/submissions/{submission.Id}", ct);
                break;
            case ApprovalStatus.Escalated:
                if (submission.CurrentApproverId is Guid esc)
                    await _notifications.NotifyAsync(esc, "submission.escalated",
                        "تصعيد بانتظار اعتمادك", comment, $"/submissions/{submission.Id}", ct);
                break;
            case ApprovalStatus.Approved:
                if (submission.CurrentApproverId is Guid next)
                    await _notifications.NotifyAsync(next, "submission.submitted",
                        "تقرير بانتظار اعتمادك", null, $"/submissions/{submission.Id}", ct);
                else
                    await _notifications.NotifyAsync(submission.SubmitterId, "submission.approved",
                        "تم اعتماد تقريرك", comment, $"/submissions/{submission.Id}", ct);
                break;
        }
        await _audit.LogAsync(_currentUser.UserId, $"submission.{decision.ToString().ToLowerInvariant()}",
            nameof(ReportSubmission), submission.Id, ct: ct);

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    public async Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListAsync(SubmissionFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<SubmissionListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.ReportSubmissions.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }

        q = ApplyFilter(q, filter);
        return Result<IReadOnlyList<SubmissionListItemDto>>.Success(await ProjectListAsync(q, ct));
    }

    public async Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListMineAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<SubmissionListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var q = _db.ReportSubmissions.AsNoTracking().Where(s => s.SubmitterId == userId);
        return Result<IReadOnlyList<SubmissionListItemDto>>.Success(await ProjectListAsync(q, ct));
    }

    public async Task<Result<IReadOnlyList<SubmissionListItemDto>>> ListPendingApprovalsAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<IReadOnlyList<SubmissionListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var q = _db.ReportSubmissions.AsNoTracking().Where(s => s.CurrentApproverId == userId);
        return Result<IReadOnlyList<SubmissionListItemDto>>.Success(await ProjectListAsync(q, ct));
    }

    public async Task<Result<SubmissionSummaryDto>> SummaryAsync(SubmissionFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<SubmissionSummaryDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.ReportSubmissions.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }
        q = ApplyFilter(q, filter);

        var grouped = await q.GroupBy(s => s.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(ct);
        var total = grouped.Sum(g => g.Count);

        return Result<SubmissionSummaryDto>.Success(new SubmissionSummaryDto(filter.PeriodKey, total, grouped));
    }

    private static IQueryable<ReportSubmission> ApplyFilter(IQueryable<ReportSubmission> q, SubmissionFilter filter)
    {
        if (filter.Status is not null) q = q.Where(s => s.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.SubmitterId is not null) q = q.Where(s => s.SubmitterId == filter.SubmitterId);
        if (filter.TeamId is not null) q = q.Where(s => s.TeamId == filter.TeamId);
        if (filter.DepartmentId is not null) q = q.Where(s => s.DepartmentId == filter.DepartmentId);
        return q;
    }

    private async Task<IReadOnlyList<SubmissionListItemDto>> ProjectListAsync(IQueryable<ReportSubmission> q, CancellationToken ct)
    {
        var rows = await q.OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new
            {
                s.Id,
                Title = _db.ReportTemplateVersions
                    .Where(v => v.Id == s.ReportTemplateVersionId)
                    .Select(v => v.ReportTemplate!.Title).FirstOrDefault(),
                s.SubmitterId,
                s.TeamId,
                s.DepartmentId,
                s.PeriodType,
                s.PeriodKey,
                s.Status,
                s.SubmittedAtUtc,
                s.CurrentApproverId
            })
            .ToListAsync(ct);

        var names = await UserNamesAsync(rows.Select(r => r.SubmitterId), ct);
        return rows.Select(r => new SubmissionListItemDto(
            r.Id, r.Title ?? string.Empty, r.SubmitterId, names.GetValueOrDefault(r.SubmitterId, string.Empty),
            r.TeamId, r.DepartmentId, r.PeriodType, r.PeriodKey, r.Status, r.SubmittedAtUtc, r.CurrentApproverId)).ToList();
    }

    private async Task<SubmissionDto> BuildDtoAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.ReportSubmissions.AsNoTracking()
            .Include(x => x.FieldValues)
            .Include(x => x.ApprovalSteps)
            .FirstAsync(x => x.Id == id, ct);

        var title = await _db.ReportTemplateVersions
            .Where(v => v.Id == s.ReportTemplateVersionId)
            .Select(v => v.ReportTemplate!.Title).FirstOrDefaultAsync(ct) ?? string.Empty;

        var fields = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == s.ReportTemplateVersionId)
            .OrderBy(f => f.Order)
            .Select(f => new { f.Id, f.Label, f.FieldType, f.IsRequired, f.HelpText, f.ConfigJson })
            .ToListAsync(ct);

        var fieldDtos = fields.Select(f =>
        {
            var v = s.FieldValues.FirstOrDefault(x => x.TemplateFieldId == f.Id);
            return new SubmissionFieldValueDto(f.Id, f.Label, f.FieldType,
                v?.ValueText, v?.ValueNumber, v?.ValueDate, v?.ValueBool, v?.ValueJson,
                f.IsRequired, f.HelpText, f.ConfigJson);
        }).ToList();

        var userIds = new List<Guid> { s.SubmitterId };
        userIds.AddRange(s.ApprovalSteps.Select(a => a.ApproverId));
        var names = await UserNamesAsync(userIds, ct);

        var steps = s.ApprovalSteps.OrderBy(a => a.Level).Select(a => new ApprovalStepDto(
            a.Level, a.ApproverId, names.GetValueOrDefault(a.ApproverId), a.Status, a.Comment, a.DecidedAtUtc)).ToList();

        var canEdit = (s.Status is SubmissionStatus.Draft or SubmissionStatus.Returned)
                      && _currentUser.UserId == s.SubmitterId;

        string? clientName = s.ClientId is Guid cid
            ? await _db.Clients.Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        string? projectName = s.ProjectId is Guid prid
            ? await _db.Projects.Where(p => p.Id == prid).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;

        return new SubmissionDto(s.Id, s.ReportTemplateVersionId, title, s.SubmitterId,
            names.GetValueOrDefault(s.SubmitterId, string.Empty), s.TeamId, s.DepartmentId,
            s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClosedAtUtc, s.CurrentApproverId,
            canEdit, fieldDtos, steps, s.ClientId, clientName, s.ProjectId, projectName);
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<bool> CanViewAsync(ReportSubmission s, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId) return false;
        if (userId == s.SubmitterId) return true;
        if (s.CurrentApproverId == userId) return true;
        var scope = await _scope.ResolveAsync(ct);
        return scope.Contains(s.SubmitterId);
    }

    private static bool HasValue(SubmissionFieldValue? v)
    {
        if (v is null) return false;
        return !string.IsNullOrWhiteSpace(v.ValueText)
            || v.ValueNumber is not null
            || v.ValueDate is not null
            || v.ValueBool is not null
            || !string.IsNullOrWhiteSpace(v.ValueJson);
    }
}

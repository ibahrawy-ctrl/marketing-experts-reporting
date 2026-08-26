using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Attendance;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Documents;
using Reporting.Application.Notifications;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Attendance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-ATT-006 — تنفيذ خدمة وقائع الحضور.
///
/// <para><b>البنية:</b> كلّ كتابة تمرّ بـ<see cref="TransitionAsync"/> وحدها، وهي البوّابة التي
/// تجمع ثلاثة فحوص لا يُستغنى عن أيّها: جدول الانتقالات (هل الانتقال جائز؟)، ومُخوِّل الفاعل
/// (هل تملك تشغيله؟)، والتزامن المتفائل (هل تكتب فوق قرار غيرك؟) — ثمّ تُلحِق حدثًا غير قابل
/// للتعديل. لا يوجد مسار كتابة يلتفّ على هذه البوّابة.</para>
///
/// <para><b>لا أثر ماليّ:</b> لا تلمس هذه الخدمة أيّ جدول أرصدة أو رواتب أو خصومات في أيّ مسار،
/// ولا تستدعي أيّ خدمة تفعل ذلك. الواقعة المؤكَّدة توثيق لا عقوبة.</para>
/// </summary>
public class AttendanceService : IAttendanceService
{
    private const string NotFound = "attendance.not_found";
    private const string Conflict = "attendance.conflict";
    private const string Forbidden = "auth.forbidden";
    private const string Invalid = "attendance.invalid";

    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>فاعل النظام في الخطّ الزمنيّ — لا يطابق أيّ مستخدم، فلا يُنسَب إجراء آليّ إلى إنسان.</summary>
    private static readonly Guid SystemActorId = Guid.Empty;
    private const string SystemActorNameAr = "النظام";

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".docx", ".xlsx", ".txt" };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFieldVisibilityPolicy _visibility;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IFileStorage _storage;
    private readonly Phase2FeatureOptions _options;

    private static readonly AttendanceTransitions Transitions = new();
    private static readonly AttendanceActorRules ActorRules = new();

    public AttendanceService(
        AppDbContext db,
        ICurrentUser currentUser,
        IFieldVisibilityPolicy visibility,
        IScopeResolver scope,
        IAuditService audit,
        INotificationService notifications,
        IFileStorage storage,
        IOptions<Phase2FeatureOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _visibility = visibility;
        _scope = scope;
        _audit = audit;
        _notifications = notifications;
        _storage = storage;
        _options = options.Value;
    }

    private Guid ActorId => _currentUser.UserId ?? Guid.Empty;

    /// <summary>العلم المطفأ يُخفي السطح كاملًا بـ404 — لا 403 كي لا يُفصح عن وجود الميزة.</summary>
    private bool Enabled => _options.AttendanceEnabled && _currentUser.IsAuthenticated && ActorId != Guid.Empty;

    // ═══════════════════════════════ القراءة ═══════════════════════════════

    public async Task<Result<IReadOnlyList<AttendanceTypeDto>>> ListTypesAsync(CancellationToken ct = default)
    {
        if (!Enabled) return Result<IReadOnlyList<AttendanceTypeDto>>.Failure("غير متاح.", NotFound);

        var types = await _db.AttendanceIncidentTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Order)
            .Select(t => new AttendanceTypeDto(
                t.Id, t.Code, t.NameAr, t.RequiresTimes, t.RequiresPolicyReference, t.AllowsMultiplePerDay, t.Order))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AttendanceTypeDto>>.Success(types);
    }

    public async Task<Result<AttendancePagedDto>> ListAsync(AttendanceListFilter filter, CancellationToken ct = default)
    {
        if (!Enabled) return Result<AttendancePagedDto>.Failure("غير متاح.", NotFound);

        var query = await BuildScopedQueryAsync(ct);

        if (filter.SubjectUserId is { } subject) query = query.Where(i => i.SubjectUserId == subject);
        if (filter.TeamId is { } team) query = query.Where(i => i.TeamId == team);
        if (filter.DepartmentId is { } dept) query = query.Where(i => i.DepartmentId == dept);
        if (filter.IncidentTypeId is { } type) query = query.Where(i => i.IncidentTypeId == type);
        if (filter.Status is { } status) query = query.Where(i => i.Status == status);
        if (filter.FromDate is { } from) query = query.Where(i => i.IncidentDate >= from);
        if (filter.ToDate is { } to) query = query.Where(i => i.IncidentDate <= to);

        if (filter.NeedsMyAction) query = ApplyNeedsMyActionFilter(query);

        // استعلامان ثابتان مهما بلغ عدد الموظّفين: عدّ ثمّ صفحة — لا حلقة على الأفراد.
        var total = await query.CountAsync(ct);

        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(i => i.IncidentDate).ThenByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .Join(_db.AttendanceIncidentTypes.AsNoTracking(), i => i.IncidentTypeId, t => t.Id,
                (i, t) => new { Incident = i, Type = t })
            .Join(_db.Users.AsNoTracking(), x => x.Incident.SubjectUserId, u => u.Id,
                (x, u) => new { x.Incident, x.Type, SubjectName = u.FullName })
            .ToListAsync(ct);

        var items = rows
            .Select(r => ToListItem(r.Incident, r.Type.Code, r.Type.NameAr, r.SubjectName))
            .Where(i => !filter.OverdueOnly || i.IsOverdue)
            .ToList();

        // العدّاد يجب أن يساوي عدد الصفوف تحت نفس المرشِّح؛ ومرشِّح التأخّر محسوب بعد الجلب،
        // فيُعاد ضبط الإجمالي عليه بدل إظهار رقم لا تطابقه الصفوف.
        var effectiveTotal = filter.OverdueOnly ? items.Count : total;

        return Result<AttendancePagedDto>.Success(new AttendancePagedDto(items, effectiveTotal, page, size));
    }

    public async Task<Result<AttendanceIncidentDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadVisibleAsync(id, ct);
        if (!loaded.Succeeded) return Result<AttendanceIncidentDetailDto>.Failure(loaded.Error!, loaded.ErrorCode);

        return Result<AttendanceIncidentDetailDto>.Success(await BuildDetailAsync(loaded.Value!, ct));
    }

    public async Task<Result<IReadOnlyList<AttendanceEventDto>>> ListEventsAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadVisibleAsync(id, ct);
        if (!loaded.Succeeded) return Result<IReadOnlyList<AttendanceEventDto>>.Failure(loaded.Error!, loaded.ErrorCode);

        return Result<IReadOnlyList<AttendanceEventDto>>.Success(await LoadEventsAsync(id, ct));
    }

    // ═══════════════════════════════ الإنشاء ═══════════════════════════════

    public async Task<Result<AttendanceIncidentDetailDto>> CreateAsync(
        CreateAttendanceIncidentRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        if (!Enabled) return Fail("غير متاح.", NotFound);

        var ctx = await _visibility.BuildContextAsync(request.SubjectUserId, "attendance.create", ct);

        // البلاغ على النفس مرفوض صراحةً بـ403 لا بـ404: وجودُك ليس سرًّا تحميه منك،
        // والإخفاء هنا كان سيُوهم المُبلِّغ أنّ حسابه غير موجود.
        if (ctx.IsSelf)
            return Fail("لا يُبلِّغ الموظّف عن واقعة على نفسه.", Forbidden);

        // خارج النطاق أو الموظّف غير موجود ⇒ نفس الجواب تمامًا: 404 لا يفرّق بينهما.
        if (!AttendanceAccess.CanReport(ctx))
            return Fail("لا توجد واقعة مطابقة.", NotFound);

        // إعادة الإرسال الشبكيّة لا تُنشئ بلاغًا ثانيًا.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.AttendanceIncidents.AsNoTracking()
                .FirstOrDefaultAsync(i => i.ReportedByUserId == ActorId && i.IdempotencyKey == idempotencyKey, ct);
            if (existing is not null)
                return Result<AttendanceIncidentDetailDto>.Success(await BuildDetailAsync(existing, ct));
        }

        var type = await _db.AttendanceIncidentTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.IncidentTypeId && t.IsActive, ct);
        if (type is null) return Fail("نوع الحادثة غير معروف.", NotFound);

        var validation = ValidateAgainstType(type, request.IncidentDate, request.StartTime, request.ReturnTime,
            request.Description, request.PolicyRefId);
        if (validation is not null) return Fail(validation, Invalid);

        var duplicate = await FindOpenDuplicateAsync(
            request.SubjectUserId, request.IncidentTypeId, request.IncidentDate, ct);

        if (duplicate is not null && !type.AllowsMultiplePerDay)
            return Fail($"يوجد بلاغ مفتوح من النوع نفسه على الموظّف في {request.IncidentDate:yyyy-MM-dd}.", Conflict);

        var subject = await _db.Users.AsNoTracking()
            .Where(u => u.Id == request.SubjectUserId)
            .Select(u => new { u.TeamId, u.DepartmentId })
            .FirstAsync(ct);

        var incident = new AttendanceIncident
        {
            SubjectUserId = request.SubjectUserId,
            IncidentTypeId = type.Id,
            IncidentDate = request.IncidentDate,
            StartTime = request.StartTime,
            ReturnTime = request.ReturnTime,
            DurationMinutes = AttendancePolicy.ComputeDurationMinutes(request.StartTime, request.ReturnTime),
            Description = request.Description.Trim(),
            DetectionSource = AttendanceDetectionSource.Manual,
            ReportedByUserId = ActorId,
            TeamId = subject.TeamId,
            DepartmentId = subject.DepartmentId,
            PolicyRefId = request.PolicyRefId,
            Status = AttendanceIncidentStatus.Draft,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,

            // تكرار مسموح بحكم الكتالوج ⇒ تحذير موثَّق لا حجب.
            DuplicateOfId = duplicate?.Id
        };

        _db.AttendanceIncidents.Add(incident);
        await AppendEventAsync(incident.Id, "created", AttendanceIncidentStatus.Draft,
            AttendanceIncidentStatus.Draft, null, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(ActorId, "attendance.created", nameof(AttendanceIncident), incident.Id,
            JsonSerializer.Serialize(new { subjectUserId = incident.SubjectUserId, typeCode = type.Code }), ct: ct);

        if (request.SubmitImmediately)
        {
            var submitted = await SubmitAsync(incident.Id, incident.ConcurrencyStamp, ct);
            if (submitted.Succeeded) return submitted;
        }

        return Result<AttendanceIncidentDetailDto>.Success(await BuildDetailAsync(incident, ct));
    }

    public async Task<Result<AttendanceIncidentDetailDto>> UpdateDraftAsync(
        Guid id, UpdateAttendanceDraftRequest request, CancellationToken ct = default)
    {
        var loaded = await LoadForWriteAsync(id, ct);
        if (!loaded.Succeeded) return Fail(loaded.Error!, loaded.ErrorCode);

        var (incident, ctx) = loaded.Value!;

        if (incident.Status != AttendanceIncidentStatus.Draft)
            return Fail("لا يُعدَّل البلاغ بعد إرساله؛ التصحيح يمرّ بالموارد البشريّة ويُوثَّق.", Conflict);

        if (incident.ReportedByUserId != ActorId || !AttendanceAccess.CanReport(ctx))
            return Fail("تعديل المسودّة حقّ مُنشِئها وحده.", Forbidden);

        if (incident.ConcurrencyStamp != request.ConcurrencyStamp)
            return Fail("تغيّرت الواقعة منذ آخر تحميل. أعِد التحميل ثمّ حاول.", Conflict);

        var type = await _db.AttendanceIncidentTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.IncidentTypeId && t.IsActive, ct);
        if (type is null) return Fail("نوع الحادثة غير معروف.", NotFound);

        var validation = ValidateAgainstType(type, request.IncidentDate, request.StartTime, request.ReturnTime,
            request.Description, request.PolicyRefId);
        if (validation is not null) return Fail(validation, Invalid);

        incident.IncidentTypeId = type.Id;
        incident.IncidentDate = request.IncidentDate;
        incident.StartTime = request.StartTime;
        incident.ReturnTime = request.ReturnTime;
        incident.DurationMinutes = AttendancePolicy.ComputeDurationMinutes(request.StartTime, request.ReturnTime);
        incident.Description = request.Description.Trim();
        incident.PolicyRefId = request.PolicyRefId;
        incident.ConcurrencyStamp++;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await AppendEventAsync(incident.Id, "draft_updated", AttendanceIncidentStatus.Draft,
            AttendanceIncidentStatus.Draft, null, ct);
        await _db.SaveChangesAsync(ct);

        return Result<AttendanceIncidentDetailDto>.Success(await BuildDetailAsync(incident, ct));
    }

    public async Task<Result> CancelDraftAsync(Guid id, int concurrencyStamp, CancellationToken ct = default)
    {
        var result = await RunTransitionAsync(id, AttendanceTrigger.Cancel, concurrencyStamp, null, ct);
        return result.Succeeded ? Result.Success() : Result.Failure(result.Error!, result.ErrorCode);
    }

    public Task<Result<AttendanceIncidentDetailDto>> SubmitAsync(
        Guid id, int concurrencyStamp, CancellationToken ct = default) =>
        RunTransitionAsync(id, AttendanceTrigger.Submit, concurrencyStamp, null, ct);

    public Task<Result<AttendanceIncidentDetailDto>> WithdrawAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(request.Reason)
            ? Task.FromResult(Fail("سحب البلاغ يستلزم سببًا موثَّقًا.", Invalid))
            : RunTransitionAsync(id, AttendanceTrigger.Withdraw, request.ConcurrencyStamp, request.Reason, ct);

    // ═══════════════════════════════ حقّ الموظّف ═══════════════════════════════

    public Task<Result<AttendanceIncidentDetailDto>> AcknowledgeAsync(
        Guid id, EmployeeResponseRequest request, CancellationToken ct = default) =>
        RunTransitionAsync(id, AttendanceTrigger.Acknowledge, request.ConcurrencyStamp, request.Response, ct);

    public Task<Result<AttendanceIncidentDetailDto>> DisputeAsync(
        Guid id, EmployeeResponseRequest request, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(request.Response)
            ? Task.FromResult(Fail("الاعتراض يستلزم بيان رواية الموظّف.", Invalid))
            : RunTransitionAsync(id, AttendanceTrigger.Dispute, request.ConcurrencyStamp, request.Response, ct);

    // ═══════════════════════════ مراجعة الموارد البشريّة ═══════════════════════════

    public async Task<Result<AttendanceIncidentDetailDto>> HrReviewAsync(
        Guid id, HrReviewRequest request, CancellationToken ct = default)
    {
        var trigger = request.Decision switch
        {
            AttendanceHrDecision.Confirm => AttendanceTrigger.HrConfirm,
            AttendanceHrDecision.Reject => AttendanceTrigger.HrReject,
            AttendanceHrDecision.Correct => AttendanceTrigger.HrCorrect,
            AttendanceHrDecision.Reconcile => AttendanceTrigger.HrReconcile,
            AttendanceHrDecision.Void => AttendanceTrigger.Void,
            _ => (AttendanceTrigger?)null
        };

        if (trigger is null) return Fail("قرار غير معروف.", Invalid);

        if (request.Decision == AttendanceHrDecision.Reject && string.IsNullOrWhiteSpace(request.Note))
            return Fail("رفض البلاغ يستلزم تعليلًا موثَّقًا.", Invalid);

        if (request.Decision == AttendanceHrDecision.Void && string.IsNullOrWhiteSpace(request.Note))
            return Fail("إبطال واقعة مؤكَّدة يستلزم تعليلًا موثَّقًا.", Invalid);

        return await RunTransitionAsync(id, trigger.Value, request.ConcurrencyStamp, request.Note, ct, request);
    }

    public Task<Result<AttendanceIncidentDetailDto>> EscalateAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(request.Reason)
            ? Task.FromResult(Fail("التصعيد يستلزم سببًا موثَّقًا.", Invalid))
            : RunTransitionAsync(id, AttendanceTrigger.Escalate, request.ConcurrencyStamp, request.Reason, ct);

    public Task<Result<AttendanceIncidentDetailDto>> CloseAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default) =>
        RunTransitionAsync(id, AttendanceTrigger.Close, request.ConcurrencyStamp, request.Reason, ct);

    // ═══════════════════════════════ البوّابة الوحيدة للكتابة ═══════════════════════════════

    /// <summary>
    /// البوّابة الوحيدة لأيّ تغيير حالة. لا يوجد مسار كتابة آخر في هذه الخدمة، ولذلك يكفي
    /// إثبات صحّتها لإثبات أنّ كلّ الانتقالات محكومة.
    /// </summary>
    private async Task<Result<AttendanceIncidentDetailDto>> RunTransitionAsync(
        Guid id, AttendanceTrigger trigger, int concurrencyStamp, string? comment,
        CancellationToken ct, HrReviewRequest? hrRequest = null)
    {
        var loaded = await LoadForWriteAsync(id, ct);
        if (!loaded.Succeeded) return Fail(loaded.Error!, loaded.ErrorCode);

        var (incident, ctx) = loaded.Value!;

        // الترتيب مقصود: صلاحيّة ← جواز ← تزامن. لو سبق فحصُ التزامن، لأعاد ختمٌ بائت 409
        // لفاعل غير مخوَّل أصلًا، فأفشى له أنّ الواقعة تغيّرت — وهو تسريب حالة لا يستحقّه.
        // (1) هل يملك هذا الفاعل تشغيل هذا الانتقال أصلًا؟
        var actorCtx = BuildActorContext(incident, ctx, isSystem: false);
        var authorized = ActorRules.Authorize(trigger, actorCtx);
        if (!authorized.Allowed) return Fail(authorized.ReasonAr!, authorized.ErrorCode);

        // (2) هل الانتقال جائز شكليًّا من هذه الحالة؟
        var allowed = Transitions.Validate(incident.Status, trigger);
        if (!allowed.Allowed) return Fail(allowed.ReasonAr!, allowed.ErrorCode);

        // (3) التزامن المتفائل أخيرًا: يحمي من الكتابة فوق تغيير غاب عن المُرسِل.
        if (incident.ConcurrencyStamp != concurrencyStamp)
            return Fail("تغيّرت الواقعة منذ آخر تحميل. أعِد التحميل ثمّ حاول.", Conflict);

        var from = incident.Status;
        var to = Transitions.Target(from, trigger);
        var now = DateTime.UtcNow;
        string? changesJson = null;

        switch (trigger)
        {
            case AttendanceTrigger.Acknowledge or AttendanceTrigger.Dispute:
                incident.EmployeeResponse = string.IsNullOrWhiteSpace(comment) ? incident.EmployeeResponse : comment.Trim();
                incident.RespondedAtUtc = now;
                break;

            case AttendanceTrigger.HrConfirm or AttendanceTrigger.HrReject or AttendanceTrigger.HrCorrect
                or AttendanceTrigger.HrReconcile or AttendanceTrigger.Void:
                incident.HrDecision = hrRequest?.Decision ?? incident.HrDecision;
                incident.HrNote = string.IsNullOrWhiteSpace(comment) ? incident.HrNote : comment.Trim();
                incident.ReviewedByUserId = ActorId;
                incident.ReviewedAtUtc = now;

                if (trigger == AttendanceTrigger.HrCorrect)
                {
                    var applied = await ApplyCorrectionAsync(incident, hrRequest, ct);
                    if (applied.error is not null) return Fail(applied.error, Invalid);
                    changesJson = applied.changesJson;
                }

                if (trigger == AttendanceTrigger.HrReconcile)
                {
                    var linked = await ApplyReconciliationAsync(incident, hrRequest, ct);
                    if (linked is not null) return Fail(linked, Invalid);
                }
                break;

            case AttendanceTrigger.Close:
                incident.ClosedAtUtc = now;
                break;
        }

        incident.Status = to;
        incident.ConcurrencyStamp++;
        incident.UpdatedAtUtc = now;

        await AppendEventAsync(incident.Id, trigger.ToString(), from, to, comment, ct, changesJson);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(ActorId, $"attendance.{trigger.ToString().ToLowerInvariant()}",
            nameof(AttendanceIncident), incident.Id,
            JsonSerializer.Serialize(new { from = from.ToString(), to = to.ToString() }), ct: ct);

        // التصحيح يُعيد الواقعة إلى الموظّف في الحركة نفسها. تركُها عند «مصحَّحة» بانتظار نقرة
        // ثانية من المراجع يعني أنّ نسيانه يحرم الموظّف من الردّ على نصٍّ تغيّر تحت يده.
        if (trigger == AttendanceTrigger.HrCorrect)
            await ApplyReviewerTransitionAsync(incident, AttendanceTrigger.ReturnToEmployee, actorCtx, ct);

        // انتقالات النظام التابعة (إشعار الموظّف/الإحالة) تُشغَّل بسياق النظام لا بسياق المستخدم.
        await AdvanceSystemChainAsync(incident, ct);

        await NotifyTransitionAsync(incident, trigger, ct);

        return Result<AttendanceIncidentDetailDto>.Success(await BuildDetailAsync(incident, ct));
    }

    /// <summary>
    /// الانتقالات التي يملكها النظام وحده وتتبع فعل المستخدم مباشرةً:
    /// الإرسال يفتح نافذة ردّ الموظّف، والردّ يُحيل إلى الموارد البشريّة.
    /// تمرّ هي أيضًا بجدول الانتقالات ومُخوِّل الفاعل — بلا استثناء.
    /// </summary>
    private async Task AdvanceSystemChainAsync(AttendanceIncident incident, CancellationToken ct)
    {
        var chain = incident.Status switch
        {
            AttendanceIncidentStatus.Reported => AttendanceTrigger.NotifyEmployee,
            AttendanceIncidentStatus.Acknowledged or AttendanceIncidentStatus.Disputed
                or AttendanceIncidentStatus.EmployeeResponseTimedOut => AttendanceTrigger.SendToHr,
            _ => (AttendanceTrigger?)null
        };

        if (chain is null) return;

        await ApplySystemTransitionAsync(incident, chain.Value, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// انتقال تابع يُنسَب إلى المراجع نفسه لا إلى النظام — لأنّه نتيجة مباشرة لقراره.
    /// يمرّ بجدول الانتقالات وبمُخوِّل الفاعل كاملَين، فلا يفتح بابًا خلفيًّا للكتابة.
    /// </summary>
    private async Task ApplyReviewerTransitionAsync(
        AttendanceIncident incident, AttendanceTrigger trigger, AttendanceActorContext actorCtx,
        CancellationToken ct)
    {
        if (!Transitions.Validate(incident.Status, trigger).Allowed) return;
        if (!ActorRules.Authorize(trigger, actorCtx).Allowed) return;

        var from = incident.Status;
        incident.Status = Transitions.Target(from, trigger);
        incident.ConcurrencyStamp++;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await AppendEventAsync(incident.Id, trigger.ToString(), from, incident.Status, null, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplySystemTransitionAsync(
        AttendanceIncident incident, AttendanceTrigger trigger, CancellationToken ct)
    {
        if (!Transitions.Validate(incident.Status, trigger).Allowed) return;

        var systemCtx = new AttendanceActorContext(
            ActorUserId: incident.ReportedByUserId,
            IsSubject: false, IsReporter: false, CanReport: false, CanReview: false, CanEscalate: false,
            EmployeeHasResponded: incident.RespondedAtUtc is not null, IsSystem: true);

        if (!ActorRules.Authorize(trigger, systemCtx).Allowed) return;

        var from = incident.Status;
        incident.Status = Transitions.Target(from, trigger);
        incident.ConcurrencyStamp++;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await AppendEventAsync(incident.Id, trigger.ToString(), from, incident.Status, null, ct,
            actorUserId: SystemActorId);
    }

    // ═══════════════════════════════ التصحيح والمصالحة ═══════════════════════════════

    /// <summary>
    /// تصحيح بيانات الواقعة. التغيير الجوهريّ (تاريخ/نوع/أوقات) يُوثَّق بفروقه في الحدث،
    /// والحالة تصير <c>Corrected</c> فلا تُؤكَّد مباشرة بل تعود إلى الموظّف — يفرضه جدول الانتقالات.
    /// </summary>
    private async Task<(string? error, string? changesJson)> ApplyCorrectionAsync(
        AttendanceIncident incident, HrReviewRequest? request, CancellationToken ct)
    {
        if (request is null) return ("بيانات التصحيح مفقودة.", null);

        var changes = new Dictionary<string, object?>();

        if (request.CorrectedIncidentTypeId is { } typeId && typeId != incident.IncidentTypeId)
        {
            var type = await _db.AttendanceIncidentTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == typeId && t.IsActive, ct);
            if (type is null) return ("نوع الحادثة غير معروف.", null);

            changes["incidentTypeId"] = new { from = incident.IncidentTypeId, to = typeId };
            incident.IncidentTypeId = typeId;
        }

        if (request.CorrectedIncidentDate is { } date && date != incident.IncidentDate)
        {
            changes["incidentDate"] = new { from = incident.IncidentDate.ToString("yyyy-MM-dd"), to = date.ToString("yyyy-MM-dd") };
            incident.IncidentDate = date;
        }

        if (request.CorrectedStartTime != incident.StartTime && request.CorrectedStartTime is not null)
        {
            changes["startTime"] = new { from = incident.StartTime?.ToString(), to = request.CorrectedStartTime?.ToString() };
            incident.StartTime = request.CorrectedStartTime;
        }

        if (request.CorrectedReturnTime != incident.ReturnTime && request.CorrectedReturnTime is not null)
        {
            changes["returnTime"] = new { from = incident.ReturnTime?.ToString(), to = request.CorrectedReturnTime?.ToString() };
            incident.ReturnTime = request.CorrectedReturnTime;
        }

        if (!string.IsNullOrWhiteSpace(request.CorrectedDescription))
        {
            changes["description"] = "changed";
            incident.Description = request.CorrectedDescription.Trim();
        }

        if (changes.Count == 0) return ("لا يوجد تغيير فعليّ في التصحيح.", null);

        incident.DurationMinutes = AttendancePolicy.ComputeDurationMinutes(incident.StartTime, incident.ReturnTime);

        return (null, JsonSerializer.Serialize(changes));
    }

    /// <summary>
    /// ربط الواقعة بإجازة/استئذان **معتمد نهائيًّا** يغطّي تاريخها. الربط قرار موارد بشريّة صريح،
    /// ولا يقع تلقائيًّا لمجرّد وجود تطابق.
    /// </summary>
    private async Task<string?> ApplyReconciliationAsync(
        AttendanceIncident incident, HrReviewRequest? request, CancellationToken ct)
    {
        if (request?.ReconcileWithLeaveRequestId is not { } leaveId)
            return "المصالحة تستلزم تحديد الإجازة/الاستئذان المعتمد.";

        var leave = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.Id == leaveId
                        && l.RequesterUserId == incident.SubjectUserId
                        && l.Status == LeaveRequestStatus.HrApproved)
            .Select(l => new { l.Id, l.Type, l.StartDate, l.EndDate })
            .FirstOrDefaultAsync(ct);

        if (leave is null) return "لا يوجد طلب معتمد مطابق لهذا الموظّف.";

        if (incident.IncidentDate < leave.StartDate || incident.IncidentDate > leave.EndDate)
            return "الطلب المعتمد لا يغطّي تاريخ الواقعة.";

        if (leave.Type == LeaveRequestType.Permission) incident.ReconciledWithPermissionId = leave.Id;
        else incident.ReconciledWithLeaveId = leave.Id;

        return null;
    }

    public async Task<Result<IReadOnlyList<AttendanceReconciliationSuggestionDto>>> SuggestReconciliationAsync(
        Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadVisibleAsync(id, ct);
        if (!loaded.Succeeded)
            return Result<IReadOnlyList<AttendanceReconciliationSuggestionDto>>.Failure(loaded.Error!, loaded.ErrorCode);

        var incident = loaded.Value!;
        var ctx = await _visibility.BuildContextAsync(incident.SubjectUserId, "attendance.reconcile", ct);

        // الاقتراح يكشف وجود إجازة معتمدة ⇒ لا يُعرض إلّا لمن يملك المراجعة فعلًا.
        if (!AttendanceAccess.CanReview(ctx))
            return Result<IReadOnlyList<AttendanceReconciliationSuggestionDto>>.Success(
                Array.Empty<AttendanceReconciliationSuggestionDto>());

        return Result<IReadOnlyList<AttendanceReconciliationSuggestionDto>>.Success(
            await FindReconciliationCandidatesAsync(incident, ct));
    }

    private async Task<IReadOnlyList<AttendanceReconciliationSuggestionDto>> FindReconciliationCandidatesAsync(
        AttendanceIncident incident, CancellationToken ct)
    {
        var matches = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.RequesterUserId == incident.SubjectUserId
                        && l.Status == LeaveRequestStatus.HrApproved
                        && l.StartDate <= incident.IncidentDate
                        && l.EndDate >= incident.IncidentDate)
            .Select(l => new { l.Id, l.Type, l.StartDate, l.EndDate, l.StartTime, l.EndTime })
            .ToListAsync(ct);

        // سبب الإجازة (HrOnly) مُستبعَد من الإسقاط أصلًا — لا يُجلَب ثمّ يُرشَّح.
        return matches.Select(l => new AttendanceReconciliationSuggestionDto(
            l.Id,
            l.Type,
            l.Type == LeaveRequestType.Permission ? "استئذان" : "إجازة",
            l.StartDate,
            l.EndDate,
            l.StartTime,
            l.EndTime,
            $"/app/leave/{l.Id}")).ToList();
    }

    // ═══════════════════════════════ المرفقات ═══════════════════════════════

    public async Task<Result<AttendanceAttachmentDto>> UploadAttachmentAsync(
        Guid id, string fileName, string contentType, long sizeBytes, Stream content, CancellationToken ct = default)
    {
        var loaded = await LoadForWriteAsync(id, ct);
        if (!loaded.Succeeded) return Result<AttendanceAttachmentDto>.Failure(loaded.Error!, loaded.ErrorCode);

        var (incident, ctx) = loaded.Value!;

        // الدليل يرفعه صاحب الواقعة أو مُبلِّغها أو مراجع الموارد البشريّة — لا أيّ مُشاهِد.
        var mayAttach = ctx.IsSelf || incident.ReportedByUserId == ActorId || AttendanceAccess.CanReview(ctx);
        if (!mayAttach) return Result<AttendanceAttachmentDto>.Failure("لا تملك إرفاق دليل على هذه الواقعة.", Forbidden);

        if (incident.Status is AttendanceIncidentStatus.Closed or AttendanceIncidentStatus.Voided
            or AttendanceIncidentStatus.Cancelled)
            return Result<AttendanceAttachmentDto>.Failure("لا يُرفَق دليل على واقعة منتهية.", Conflict);

        if (sizeBytes <= 0 || sizeBytes > MaxAttachmentBytes)
            return Result<AttendanceAttachmentDto>.Failure("حجم الملفّ خارج الحدّ المسموح (10 ميغابايت).", Invalid);

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return Result<AttendanceAttachmentDto>.Failure("نوع الملفّ غير مسموح.", Invalid);

        // الاسم المعروض يُنظَّف من أيّ مسار؛ ومفتاح التخزين يُبنى من معرّفات فقط لا من نصّ المستخدم.
        var safeName = Path.GetFileName(fileName);
        if (safeName.Length > 260) safeName = safeName[^260..];

        var attachmentId = Guid.NewGuid();
        var storageKey = _storage.BuildStorageKey(
            "attendance", incident.Id, attachmentId, attachmentId, extension.ToLowerInvariant());

        var stored = await _storage.SaveAsync(storageKey, content, ct);

        var attachment = new AttendanceIncidentAttachment
        {
            Id = attachmentId,
            IncidentId = incident.Id,
            UploadedByUserId = ActorId,
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = stored.SizeBytes,
            StoredPath = storageKey,
            ContentHash = stored.Sha256
        };

        _db.AttendanceIncidentAttachments.Add(attachment);
        await AppendEventAsync(incident.Id, "attachment_added", incident.Status, incident.Status, safeName, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(ActorId, "attendance.attachment_added", nameof(AttendanceIncident), incident.Id,
            JsonSerializer.Serialize(new { attachmentId, sizeBytes = stored.SizeBytes }), ct: ct);

        return Result<AttendanceAttachmentDto>.Success(new AttendanceAttachmentDto(
            attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes,
            attachment.UploadedByUserId, attachment.CreatedAtUtc));
    }

    public async Task<Result<AttendanceFileDownload>> DownloadAttachmentAsync(
        Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var loaded = await LoadVisibleAsync(id, ct);
        if (!loaded.Succeeded) return Result<AttendanceFileDownload>.Failure(loaded.Error!, loaded.ErrorCode);

        var attachment = await _db.AttendanceIncidentAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.IncidentId == id, ct);

        // مرفق خارج الواقعة المصرَّح بها ⇒ 404 لا 403.
        if (attachment is null) return Result<AttendanceFileDownload>.Failure("لا يوجد مرفق مطابق.", NotFound);

        if (!await _storage.ExistsAsync(attachment.StoredPath, ct))
            return Result<AttendanceFileDownload>.Failure("لا يوجد مرفق مطابق.", NotFound);

        var stream = await _storage.OpenReadAsync(attachment.StoredPath, ct);

        await _audit.LogAsync(ActorId, "attendance.attachment_read", nameof(AttendanceIncident), id,
            JsonSerializer.Serialize(new { attachmentId }), ct: ct);

        return Result<AttendanceFileDownload>.Success(
            new AttendanceFileDownload(stream, attachment.ContentType, attachment.FileName));
    }

    // ═══════════════════════════════ كنس SLA ═══════════════════════════════

    public async Task<Result<AttendanceSlaSweepResult>> RunSlaSweepAsync(CancellationToken ct = default)
    {
        if (!_options.AttendanceEnabled)
            return Result<AttendanceSlaSweepResult>.Failure("غير متاح.", NotFound);

        var now = DateTime.UtcNow;
        var notified = 0;
        var timedOut = 0;
        var sentToHr = 0;

        var pendingNotify = await _db.AttendanceIncidents
            .Where(i => i.Status == AttendanceIncidentStatus.Reported).ToListAsync(ct);
        foreach (var incident in pendingNotify)
        {
            await ApplySystemTransitionAsync(incident, AttendanceTrigger.NotifyEmployee, ct);
            notified++;
        }

        var awaiting = await _db.AttendanceIncidents
            .Where(i => i.Status == AttendanceIncidentStatus.AwaitingEmployee).ToListAsync(ct);
        foreach (var incident in awaiting)
        {
            var deadline = AttendancePolicy.EmployeeResponseDeadlineUtc(
                incident.UpdatedAtUtc ?? incident.CreatedAtUtc, _options.AttendanceEmployeeResponseHours);

            if (deadline > now) continue;

            await ApplySystemTransitionAsync(incident, AttendanceTrigger.TimeOutEmployeeResponse, ct);
            timedOut++;
        }

        var readyForHr = await _db.AttendanceIncidents
            .Where(i => i.Status == AttendanceIncidentStatus.Acknowledged
                        || i.Status == AttendanceIncidentStatus.Disputed
                        || i.Status == AttendanceIncidentStatus.EmployeeResponseTimedOut)
            .ToListAsync(ct);
        foreach (var incident in readyForHr)
        {
            await ApplySystemTransitionAsync(incident, AttendanceTrigger.SendToHr, ct);
            sentToHr++;
        }

        await _db.SaveChangesAsync(ct);

        return Result<AttendanceSlaSweepResult>.Success(new AttendanceSlaSweepResult(notified, timedOut, sentToHr));
    }

    // ═══════════════════════════════ مساعدات داخليّة ═══════════════════════════════

    private static Result<AttendanceIncidentDetailDto> Fail(string message, string? code) =>
        Result<AttendanceIncidentDetailDto>.Failure(message, code);

    /// <summary>
    /// استعلام الوقائع محصورًا بنطاق المستخدم — يُطبَّق **قبل** أيّ مرشِّح من العميل،
    /// فلا يستطيع مرشِّح مُلفَّق توسيع النطاق.
    /// </summary>
    private async Task<IQueryable<AttendanceIncident>> BuildScopedQueryAsync(CancellationToken ct)
    {
        var query = _db.AttendanceIncidents.AsNoTracking();

        var canReview = _currentUser.HasPermission(AppPermissions.AttendanceReview);
        var canEscalate = _currentUser.HasPermission(AppPermissions.AttendanceEscalate);
        if (canReview || canEscalate) return query;

        var scope = await _scope.ResolveAsync(ct);
        if (scope.SeesAll) return query;

        var visibleUsers = scope.UserIds.ToList();
        var me = ActorId;

        // صاحب الواقعة، أو مُبلِّغها، أو من هو داخل نطاق المُشاهِد — وما عدا ذلك غير موجود بالنسبة له.
        return query.Where(i =>
            i.SubjectUserId == me || i.ReportedByUserId == me || visibleUsers.Contains(i.SubjectUserId));
    }

    private IQueryable<AttendanceIncident> ApplyNeedsMyActionFilter(IQueryable<AttendanceIncident> query)
    {
        var me = ActorId;
        var canReview = _currentUser.HasPermission(AppPermissions.AttendanceReview);

        if (canReview)
            return query.Where(i => i.Status == AttendanceIncidentStatus.AwaitingHr
                                    || i.Status == AttendanceIncidentStatus.Corrected);

        return query.Where(i =>
            (i.SubjectUserId == me && i.Status == AttendanceIncidentStatus.AwaitingEmployee)
            || (i.ReportedByUserId == me && i.Status == AttendanceIncidentStatus.Draft));
    }

    /// <summary>تحميل للقراءة مع فحص الرؤية. الغياب وانعدام التصريح يعطيان نفس الجواب.</summary>
    private async Task<Result<AttendanceIncident>> LoadVisibleAsync(Guid id, CancellationToken ct)
    {
        if (!Enabled) return Result<AttendanceIncident>.Failure("لا توجد واقعة مطابقة.", NotFound);

        var incident = await _db.AttendanceIncidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident is null) return Result<AttendanceIncident>.Failure("لا توجد واقعة مطابقة.", NotFound);

        var ctx = await _visibility.BuildContextAsync(incident.SubjectUserId, "attendance.read", ct);
        if (!AttendanceAccess.CanViewIncident(ctx, incident.ReportedByUserId, incident.Status))
            return Result<AttendanceIncident>.Failure("لا توجد واقعة مطابقة.", NotFound);

        return Result<AttendanceIncident>.Success(incident);
    }

    /// <summary>تحميل متتبَّع للكتابة مع سياق الرؤية.</summary>
    private async Task<Result<(AttendanceIncident, FieldVisibilityContext)>> LoadForWriteAsync(
        Guid id, CancellationToken ct)
    {
        if (!Enabled)
            return Result<(AttendanceIncident, FieldVisibilityContext)>.Failure("لا توجد واقعة مطابقة.", NotFound);

        var incident = await _db.AttendanceIncidents.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident is null)
            return Result<(AttendanceIncident, FieldVisibilityContext)>.Failure("لا توجد واقعة مطابقة.", NotFound);

        var ctx = await _visibility.BuildContextAsync(incident.SubjectUserId, "attendance.write", ct);
        if (!AttendanceAccess.CanViewIncident(ctx, incident.ReportedByUserId, incident.Status))
            return Result<(AttendanceIncident, FieldVisibilityContext)>.Failure("لا توجد واقعة مطابقة.", NotFound);

        return Result<(AttendanceIncident, FieldVisibilityContext)>.Success((incident, ctx));
    }

    private AttendanceActorContext BuildActorContext(
        AttendanceIncident incident, FieldVisibilityContext ctx, bool isSystem) =>
        new(
            ActorUserId: ActorId,
            IsSubject: incident.SubjectUserId == ActorId,
            IsReporter: incident.ReportedByUserId == ActorId,
            CanReport: AttendanceAccess.CanReport(ctx),
            CanReview: AttendanceAccess.CanReview(ctx),
            CanEscalate: AttendanceAccess.CanEscalate(ctx),
            EmployeeHasResponded: incident.RespondedAtUtc is not null,
            IsSystem: isSystem);

    /// <summary>
    /// إلحاق حدث. <paramref name="actorUserId"/> يساوي <see cref="SystemActorId"/> في إجراءات النظام،
    /// فلا يُنسَب أيّ انتقال آليّ إلى إنسان لم يفعله.
    /// </summary>
    private async Task AppendEventAsync(
        Guid incidentId, string action, AttendanceIncidentStatus from, AttendanceIncidentStatus to,
        string? comment, CancellationToken ct, string? changesJson = null, Guid? actorUserId = null)
    {
        _db.AttendanceIncidentEvents.Add(new AttendanceIncidentEvent
        {
            IncidentId = incidentId,
            ActorUserId = actorUserId ?? ActorId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            ChangesJson = changesJson
        });

        await Task.CompletedTask;
    }

    /// <summary>إشعارات داخل النظام فقط (<c>INotificationService</c>) — لا بريد خارجيّ في أيّ مسار.</summary>
    private async Task NotifyTransitionAsync(AttendanceIncident incident, AttendanceTrigger trigger, CancellationToken ct)
    {
        var link = $"/app/attendance/{incident.Id}";

        switch (trigger)
        {
            case AttendanceTrigger.Submit:
                await _notifications.NotifyAsync(incident.SubjectUserId, "attendance.reported",
                    "بلاغ حضور بانتظار ردّك",
                    "سُجِّل بلاغ حضور يخصّك. لك حقّ الإقرار أو الاعتراض وإرفاق دليل.", link, ct);
                break;

            case AttendanceTrigger.Acknowledge or AttendanceTrigger.Dispute:
                await _notifications.NotifyAsync(incident.ReportedByUserId, "attendance.employee_responded",
                    trigger == AttendanceTrigger.Dispute ? "اعتراض على بلاغ حضور" : "إقرار على بلاغ حضور",
                    null, link, ct);
                break;

            case AttendanceTrigger.HrConfirm or AttendanceTrigger.HrReject or AttendanceTrigger.HrReconcile
                or AttendanceTrigger.Void:
                await _notifications.NotifyManyAsync(
                    new[] { incident.SubjectUserId, incident.ReportedByUserId }.Distinct(),
                    "attendance.hr_decision", "صدر قرار الموارد البشريّة على واقعة حضور", null, link, ct);
                break;

            case AttendanceTrigger.HrCorrect:
                await _notifications.NotifyAsync(incident.SubjectUserId, "attendance.corrected",
                    "صُحِّحت بيانات واقعة تخصّك", "راجِع البيانات المصحَّحة ولك حقّ الردّ من جديد.", link, ct);
                break;

            case AttendanceTrigger.Withdraw:
                await _notifications.NotifyAsync(incident.SubjectUserId, "attendance.withdrawn",
                    "سُحِب بلاغ حضور كان يخصّك", null, link, ct);
                break;
        }
    }

    private string? ValidateAgainstType(
        AttendanceIncidentType type, DateOnly date, TimeOnly? start, TimeOnly? returnTime,
        string description, Guid? policyRefId)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "وصف الواقعة مطلوب.";

        if (date > AttendancePolicy.RiyadhDate(DateTimeOffset.UtcNow))
            return "لا يجوز تسجيل واقعة بتاريخ مستقبليّ.";

        if (type.RequiresTimes)
        {
            if (start is null || returnTime is null)
                return "هذا النوع يستلزم وقت البداية ووقت العودة.";

            if (AttendancePolicy.ComputeDurationMinutes(start, returnTime) is null)
                return "وقت العودة يجب أن يكون بعد وقت البداية.";
        }

        if (type.RequiresPolicyReference && policyRefId is null)
            return "هذا النوع يستلزم مرجع سياسة.";

        return null;
    }

    private Task<AttendanceIncident?> FindOpenDuplicateAsync(
        Guid subjectUserId, Guid typeId, DateOnly date, CancellationToken ct)
    {
        var openStates = new[]
        {
            AttendanceIncidentStatus.Draft, AttendanceIncidentStatus.Reported,
            AttendanceIncidentStatus.AwaitingEmployee, AttendanceIncidentStatus.Acknowledged,
            AttendanceIncidentStatus.Disputed, AttendanceIncidentStatus.EmployeeResponseTimedOut,
            AttendanceIncidentStatus.AwaitingHr, AttendanceIncidentStatus.Corrected,
            AttendanceIncidentStatus.Confirmed, AttendanceIncidentStatus.Escalated
        };

        return _db.AttendanceIncidents.AsNoTracking()
            .Where(i => i.SubjectUserId == subjectUserId
                        && i.IncidentTypeId == typeId
                        && i.IncidentDate == date
                        && openStates.Contains(i.Status))
            .FirstOrDefaultAsync(ct);
    }

    // ═══════════════════════════════ بناء العقود ═══════════════════════════════

    private static bool IsOfficial(AttendanceIncident i) =>
        AttendancePolicy.IsOfficialIncident(i.Status, i.HrDecision);

    private DateTime? ComputeSlaDueAtUtc(AttendanceIncident i) =>
        AttendancePolicy.CurrentSlaDueAtUtc(
            i.Status, i.UpdatedAtUtc ?? i.CreatedAtUtc,
            _options.AttendanceEmployeeResponseHours, _options.AttendanceHrReviewWorkingDays);

    private static string? NextActor(AttendanceIncidentStatus status) => status switch
    {
        AttendanceIncidentStatus.Draft => "مُقدِّم البلاغ",
        AttendanceIncidentStatus.Reported or AttendanceIncidentStatus.AwaitingEmployee => "الموظّف",
        AttendanceIncidentStatus.Acknowledged or AttendanceIncidentStatus.Disputed
            or AttendanceIncidentStatus.EmployeeResponseTimedOut or AttendanceIncidentStatus.AwaitingHr
            or AttendanceIncidentStatus.Corrected or AttendanceIncidentStatus.Confirmed => "الموارد البشريّة",
        AttendanceIncidentStatus.Escalated => "الحوكمة",
        _ => null
    };

    private AttendanceIncidentListItemDto ToListItem(
        AttendanceIncident i, string typeCode, string typeNameAr, string subjectName)
    {
        var due = ComputeSlaDueAtUtc(i);
        var today = AttendancePolicy.RiyadhDate(DateTimeOffset.UtcNow);

        return new AttendanceIncidentListItemDto(
            i.Id, i.SubjectUserId, subjectName, i.IncidentTypeId, typeCode, typeNameAr, i.IncidentDate,
            i.Status, AttendanceTransitions.StatusAr(i.Status), IsOfficial(i), i.DurationMinutes,
            AttendancePolicy.WorkingDaysBetween(i.IncidentDate, today),
            due, due is not null && due < DateTime.UtcNow,
            i.UpdatedAtUtc ?? i.CreatedAtUtc, NextActor(i.Status));
    }

    private async Task<IReadOnlyList<AttendanceEventDto>> LoadEventsAsync(Guid incidentId, CancellationToken ct)
    {
        // ربط خارجيّ لا داخليّ: أحداث النظام لا فاعل بشريًّا لها، والربط الداخليّ كان
        // سيُسقِطها من الخطّ الزمنيّ فتختفي انتقالات حدثت فعلًا.
        var rows = await _db.AttendanceIncidentEvents.AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.CreatedAtUtc)
            .GroupJoin(_db.Users.AsNoTracking(), e => e.ActorUserId, u => u.Id, (e, us) => new { Event = e, Users = us })
            .SelectMany(x => x.Users.DefaultIfEmpty(), (x, u) => new
            {
                x.Event.Id, x.Event.ActorUserId, ActorName = u == null ? null : u.FullName,
                x.Event.Action, x.Event.FromStatus, x.Event.ToStatus, x.Event.Comment, x.Event.CreatedAtUtc
            })
            .ToListAsync(ct);

        // الحالتان مخزَّنتان كتعداد؛ تحويلهما إلى نصّ يتمّ بعد التجسيد لا داخل الاستعلام.
        return rows.Select(x => new AttendanceEventDto(
                x.Id, x.ActorUserId, x.ActorName ?? SystemActorNameAr, x.Action,
                x.FromStatus.ToString(), x.ToStatus.ToString(), x.Comment, x.CreatedAtUtc))
            .ToList();
    }

    /// <summary>
    /// يبني عقد التفاصيل بعد ترشيح الحسّاسيّة. الحقل غير المصرّح يبقى <c>null</c> هنا، ويُحذف
    /// من الـJSON نهائيًّا بوسم <c>JsonIgnore(WhenWritingNull)</c> على الحقل نفسه في العقد —
    /// وسم على الحقل لا إعداد عامّ، كي لا يتغيّر شكل أيّ استجابة قائمة خارج الحضور.
    /// </summary>
    private async Task<AttendanceIncidentDetailDto> BuildDetailAsync(AttendanceIncident i, CancellationToken ct)
    {
        var ctx = await _visibility.BuildContextAsync(i.SubjectUserId, "attendance.detail", ct);

        var type = await _db.AttendanceIncidentTypes.AsNoTracking().FirstAsync(t => t.Id == i.IncidentTypeId, ct);

        var names = await _db.Users.AsNoTracking()
            .Where(u => u.Id == i.SubjectUserId || u.Id == i.ReportedByUserId)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);

        var attachments = await _db.AttendanceIncidentAttachments.AsNoTracking()
            .Where(a => a.IncidentId == i.Id)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new AttendanceAttachmentDto(
                a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, a.CreatedAtUtc))
            .ToListAsync(ct);

        var canReview = AttendanceAccess.CanReview(ctx);

        // ملاحظة الموارد البشريّة مصنّفة HrOnly ⇒ تُطلَب بالتصنيف لا بالدور، ويُكتَب أثر تدقيقيّ
        // عند الوصول الفعليّ إليها بلا تسجيل قيمتها.
        string? hrNote = null;
        if (i.HrNote is not null
            && await _visibility.CanSeeAsync(ctx, FieldSensitivity.HrOnly, "attendance.hrNote", ct))
            hrNote = i.HrNote;

        var employeeResponse = i.EmployeeResponse is not null
                               && _visibility.CanSee(ctx, FieldSensitivity.SharedWithEmployee)
            ? i.EmployeeResponse
            : null;

        var due = ComputeSlaDueAtUtc(i);
        var today = AttendancePolicy.RiyadhDate(DateTimeOffset.UtcNow);

        var actorCtx = BuildActorContext(i, ctx, isSystem: false);
        var allowedActions = Transitions.AllowedTriggers(i.Status)
            .Where(tr => ActorRules.Authorize(tr, actorCtx).Allowed)
            .Select(tr => tr.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return new AttendanceIncidentDetailDto
        {
            Id = i.Id,
            SubjectUserId = i.SubjectUserId,
            SubjectName = names.FirstOrDefault(n => n.Id == i.SubjectUserId)?.FullName ?? string.Empty,
            IncidentTypeId = i.IncidentTypeId,
            TypeCode = type.Code,
            TypeNameAr = type.NameAr,
            IncidentDate = i.IncidentDate,
            StartTime = i.StartTime,
            ReturnTime = i.ReturnTime,
            DurationMinutes = i.DurationMinutes,
            Description = i.Description,
            DetectionSource = i.DetectionSource.ToString(),
            ReportedByUserId = i.ReportedByUserId,
            ReportedByName = names.FirstOrDefault(n => n.Id == i.ReportedByUserId)?.FullName ?? string.Empty,
            Status = i.Status,
            StatusAr = AttendanceTransitions.StatusAr(i.Status),
            IsOfficialIncident = IsOfficial(i),
            ConcurrencyStamp = i.ConcurrencyStamp,
            SlaDueAtUtc = due,
            IsOverdue = due is not null && due < DateTime.UtcNow,
            AgeingDays = AttendancePolicy.WorkingDaysBetween(i.IncidentDate, today),
            NextActorAr = NextActor(i.Status),
            EmployeeResponse = employeeResponse,
            RespondedAtUtc = i.RespondedAtUtc,
            HrDecision = i.HrDecision == AttendanceHrDecision.None ? null : i.HrDecision.ToString(),
            HrNote = hrNote,
            ReviewedByUserId = i.ReviewedByUserId,
            ReviewedAtUtc = i.ReviewedAtUtc,
            ReconciledWithLeaveId = i.ReconciledWithLeaveId,
            ReconciledWithPermissionId = i.ReconciledWithPermissionId,
            DuplicateOfId = i.DuplicateOfId,
            ClosedAtUtc = i.ClosedAtUtc,
            CreatedAtUtc = i.CreatedAtUtc,
            Attachments = attachments,
            Events = await LoadEventsAsync(i.Id, ct),
            AllowedActions = allowedActions,
            ReconciliationSuggestions = canReview ? await FindReconciliationCandidatesAsync(i, ct) : null
        };
    }
}

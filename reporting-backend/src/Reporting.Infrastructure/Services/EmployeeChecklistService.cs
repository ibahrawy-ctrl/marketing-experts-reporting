using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Checklist;
using Reporting.Application.Common;
using Reporting.Application.Obligations;
using Reporting.Application.Security;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-HR-010 — التنفيذ الوحيد لقائمة خدمة الموظّف والالتزام.
///
/// <para><b>القاعدة الحاكمة:</b> البند المحسوب لا صفَّ له. كلّ عدّاد هنا استعلام على مصدر
/// الحقيقة المالك له في لحظة النداء، والبنود اليدويّة وحدها تُقرأ من
/// <c>employee_checklist_items</c>. خلطُ الاثنين كان سيُنتج قائمةً تقول شيئًا ومصدرَها يقول
/// غيره، فيُتّخَذ قرار على النسخة البائتة.</para>
///
/// <para><b>الحسّاسيّة قبل التركيب:</b> البند غير المصرَّح به لا يدخل القائمة أصلًا —
/// لا مقنَّعًا ولا بعدّاد صفر — كي لا يُستدلّ على وجوده من مجرّد ظهور مفتاحه.</para>
/// </summary>
public sealed class EmployeeChecklistService : IEmployeeChecklistService
{
    private const string Purpose = "employeeChecklist";
    private const string NotFound = "employeeChecklist.not_found";
    private const string NotFoundMessage = "الموظّف غير موجود أو خارج نطاقك.";

    private readonly AppDbContext _db;
    private readonly IFieldVisibilityPolicy _visibility;
    private readonly IObligationsService _obligations;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemClock _clock;
    private readonly Phase2FeatureOptions _flags;

    public EmployeeChecklistService(
        AppDbContext db,
        IFieldVisibilityPolicy visibility,
        IObligationsService obligations,
        ICurrentUser currentUser,
        ISystemClock clock,
        IOptions<Phase2FeatureOptions> flags)
    {
        _db = db;
        _visibility = visibility;
        _obligations = obligations;
        _currentUser = currentUser;
        _clock = clock;
        _flags = flags.Value;
    }

    // ═══════════════════════════════ القراءة ═══════════════════════════════

    public async Task<Result<EmployeeChecklistDto>> GetAsync(Guid subjectUserId, CancellationToken ct = default)
    {
        var ctx = await _visibility.BuildContextAsync(subjectUserId, Purpose, ct);

        // خارج النطاق وغير الموجود ⟵ استجابة واحدة، فلا يُستدلّ على وجود موظّف من فرق الرمز.
        if (!ctx.InScope)
            return Result<EmployeeChecklistDto>.Failure(NotFoundMessage, NotFound);

        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == subjectUserId, ct);
        if (!exists)
            return Result<EmployeeChecklistDto>.Failure(NotFoundMessage, NotFound);

        var items = await BuildItemsAsync(ctx, ct);
        var ordered = ChecklistPolicy.Order(items);

        return Result<EmployeeChecklistDto>.Success(new EmployeeChecklistDto(
            subjectUserId,
            ctx.IsSelf,
            ctx.Relation.ToString(),
            ChecklistPolicy.Summarize(ordered),
            ordered));
    }

    public Task<Result<EmployeeChecklistDto>> GetForSelfAsync(CancellationToken ct = default)
    {
        var me = _currentUser.UserId;
        return me is null
            ? Task.FromResult(Result<EmployeeChecklistDto>.Failure(NotFoundMessage, NotFound))
            : GetAsync(me.Value, ct);
    }

    // ═══════════════════════════════ الكتابة (اليدويّ وحده) ═══════════════════════════════

    public async Task<Result<ChecklistItemDto>> UpdateManualItemAsync(
        Guid subjectUserId, string itemKey, UpdateChecklistItemCommand command, CancellationToken ct = default)
    {
        var ctx = await _visibility.BuildContextAsync(subjectUserId, Purpose, ct);
        if (!ctx.InScope)
            return Result<ChecklistItemDto>.Failure(NotFoundMessage, NotFound);

        var definition = ChecklistCatalog.Find(itemKey);

        // مفتاح مجهول ⟵ 404: لا نُفرِّق بين «لا يوجد بند بهذا الاسم» و«موجود لكنّه محجوب عنك».
        if (definition is null)
            return Result<ChecklistItemDto>.Failure("البند غير موجود.", NotFound);

        // بند محسوب ⟵ 400 صريح: خطأ في الطلب لا نقص صلاحيّة. الكتابة هنا كانت ستُنشئ
        // نسخةً تناقض مصدرها؛ والتصحيح موضعه المصدر نفسه (التقرير/التقييم/الواقعة).
        if (definition.Source != ChecklistItemSource.Manual)
            return Result<ChecklistItemDto>.Failure(
                "هذا البند محسوب من مصدره ولا يُحرَّر يدويًّا. صحّحه في مصدره.",
                "employeeChecklist.computed_item_not_writable");

        if (!ChecklistPolicy.IsValidManualStatus(command.Status))
            return Result<ChecklistItemDto>.Failure("حالة غير معروفة.", "employeeChecklist.invalid_status");

        // بند محجوب عن هذا المُشاهِد ⟵ 404 لا 403: لو رددنا 403 لأثبتنا وجوده.
        if (!await _visibility.CanSeeAsync(ctx, definition.Sensitivity, $"checklist.{definition.Key}", ct))
            return Result<ChecklistItemDto>.Failure("البند غير موجود.", NotFound);

        var record = await _db.EmployeeChecklistRecords
            .FirstOrDefaultAsync(r => r.SubjectUserId == subjectUserId && r.ItemKey == definition.Key, ct);

        var now = _clock.UtcNow.UtcDateTime;
        var actor = _currentUser.UserId;

        if (record is null)
        {
            // أوّل تسجيل للبند. بصمة تزامن مُرسَلة على سجلّ غير قائم = طلب على نسخة لا وجود لها.
            if (!string.IsNullOrWhiteSpace(command.ConcurrencyStamp))
                return Result<ChecklistItemDto>.Failure(
                    "تغيّر البند منذ آخر قراءة. أعِد التحميل ثمّ حاول.", "employeeChecklist.conflict");

            record = new EmployeeChecklistRecord
            {
                SubjectUserId = subjectUserId,
                ItemKey = definition.Key,
                CreatedAtUtc = now
            };
            _db.EmployeeChecklistRecords.Add(record);
        }
        else if (!string.IsNullOrWhiteSpace(command.ConcurrencyStamp)
                 && !string.Equals(record.ConcurrencyStamp, command.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Result<ChecklistItemDto>.Failure(
                "تغيّر البند منذ آخر قراءة. أعِد التحميل ثمّ حاول.", "employeeChecklist.conflict");
        }

        record.Status = command.Status;
        record.DueDate = command.DueDate;
        record.OwnerUserId = command.OwnerUserId;
        record.EvidenceReference = Trim(command.EvidenceReference, 200);
        record.Note = Trim(command.Note, 1000);
        record.LastActionAtUtc = now;
        record.LastActionByUserId = actor;
        record.UpdatedAtUtc = now;
        record.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(ct);

        var names = await NamesAsync(new[] { record.OwnerUserId }, ct);
        return Result<ChecklistItemDto>.Success(ManualItem(definition, record, ctx, names));
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    // ═══════════════════════════════ التركيب ═══════════════════════════════

    private async Task<List<ChecklistItemDto>> BuildItemsAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;
        var viewer = ctx.ViewerUserId;

        // البنود المرئيّة تُحدَّد **قبل** أيّ استعلام: ما لا يُرى لا يُستعلَم عنه أصلًا.
        var visible = new List<ChecklistItemDefinition>();
        foreach (var d in ChecklistCatalog.All)
            if (await _visibility.CanSeeAsync(ctx, d.Sensitivity, $"checklist.{d.Key}", ct))
                visible.Add(d);

        var manualRecords = await _db.EmployeeChecklistRecords.AsNoTracking()
            .Where(r => r.SubjectUserId == subject)
            .ToListAsync(ct);

        var computed = await ComputeCountsAsync(ctx, visible, ct);

        // كلّ الأسماء المطلوبة في نداء واحد — لا استعلام داخل حلقة (منع N+1).
        var ownerIds = manualRecords.Select(r => r.OwnerUserId)
            .Concat(computed.Values.Select(c => c.OwnerUserId))
            .ToList();
        var names = await NamesAsync(ownerIds, ct);

        var items = new List<ChecklistItemDto>(visible.Count);

        foreach (var d in visible)
        {
            if (d.Source == ChecklistItemSource.Manual)
            {
                var record = manualRecords.FirstOrDefault(r => r.ItemKey == d.Key);
                items.Add(ManualItem(d, record, ctx, names));
                continue;
            }

            if (!computed.TryGetValue(d.Key, out var c)) continue;

            var status = ChecklistPolicy.ComputedStatus(c.OpenCount, c.Applicable);
            items.Add(new ChecklistItemDto(
                d.Key, d.TitleAr, d.GroupAr, d.Source.ToString(),
                status,
                ChecklistPolicy.ComputedStatusLabelAr(status, c.OpenCount),
                c.OpenCount,
                c.OwnerUserId,
                c.OwnerUserId is null ? null : names.GetValueOrDefault(c.OwnerUserId.Value),
                c.DueDate,
                c.LastActionAtUtc,
                c.EvidenceAr,
                d.SourceKind,
                c.SourceLink,
                c.OpenCount > 0 && c.ActionOwnerUserId == viewer));
        }

        return items;
    }

    private ChecklistItemDto ManualItem(
        ChecklistItemDefinition d, EmployeeChecklistRecord? record,
        FieldVisibilityContext ctx, IReadOnlyDictionary<Guid, string> names)
    {
        var status = record?.Status ?? EmployeeChecklistStatus.NotStarted;
        var owner = record?.OwnerUserId;
        var open = status is not (EmployeeChecklistStatus.Completed or EmployeeChecklistStatus.NotApplicable);

        return new ChecklistItemDto(
            d.Key, d.TitleAr, d.GroupAr, d.Source.ToString(),
            status,
            ChecklistPolicy.StatusLabelAr(status),
            open ? 1 : 0,
            owner,
            owner is null ? null : names.GetValueOrDefault(owner.Value),
            record?.DueDate,
            record?.LastActionAtUtc,
            record?.EvidenceReference,
            d.SourceKind,
            null,
            open && owner == ctx.ViewerUserId);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        return await _db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ═══════════════════════════════ الاشتقاق ═══════════════════════════════

    /// <summary>نتيجة اشتقاق بند محسوب واحد — قيمة عابرة لا تُخزَّن.</summary>
    private sealed record ComputedItem(
        int OpenCount,
        bool Applicable,
        Guid? OwnerUserId,
        Guid? ActionOwnerUserId,
        DateOnly? DueDate,
        DateTime? LastActionAtUtc,
        string? EvidenceAr,
        string? SourceLink);

    private async Task<Dictionary<string, ComputedItem>> ComputeCountsAsync(
        FieldVisibilityContext ctx, IReadOnlyList<ChecklistItemDefinition> visible, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;
        var wanted = visible.Where(d => d.Source == ChecklistItemSource.Computed)
            .Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, ComputedItem>(StringComparer.Ordinal);

        // ===== (1)(2) التقارير وتقييمات KPI — من محرّك الالتزامات وحده، لا إعادة حساب هنا =====
        if (wanted.Contains(ChecklistCatalog.ReportsObligations)
            || wanted.Contains(ChecklistCatalog.KpiObligations))
        {
            var cycles = ReportingCalendarPolicy.RecentCycleKeys(
                ReportingCalendarPolicy.RiyadhDate(_clock.UtcNow.UtcDateTime), 4);

            var obligations = await _obligations.ComputeAsync(
                new ObligationQuery(new[] { subject }, cycles), ct);

            AddObligation(result, wanted, ChecklistCatalog.ReportsObligations,
                obligations, ObligationKind.Report, "/app/reports");
            AddObligation(result, wanted, ChecklistCatalog.KpiObligations,
                obligations, ObligationKind.KpiEvaluation, "/app/kpi");
        }

        // ===== (3)(4) الحضور =====
        if (wanted.Contains(ChecklistCatalog.AttendanceAwaitingResponse)
            || wanted.Contains(ChecklistCatalog.AttendanceAwaitingHrReview))
        {
            // الوحدة مُطفأة ⟵ «غير منطبق» لا صفر: الفرق بين «لا وقائع» و«لا وحدة» جوهريّ.
            if (!_flags.AttendanceEnabled)
            {
                AddNotApplicable(result, wanted, ChecklistCatalog.AttendanceAwaitingResponse);
                AddNotApplicable(result, wanted, ChecklistCatalog.AttendanceAwaitingHrReview);
            }
            else
            {
                var rows = await _db.AttendanceIncidents.AsNoTracking()
                    .Where(i => i.SubjectUserId == subject
                                && (i.Status == AttendanceIncidentStatus.AwaitingEmployee
                                    || i.Status == AttendanceIncidentStatus.AwaitingHr))
                    .Select(i => new { i.Id, i.Status, i.UpdatedAtUtc, i.CreatedAtUtc })
                    .ToListAsync(ct);

                if (wanted.Contains(ChecklistCatalog.AttendanceAwaitingResponse))
                {
                    var mine = rows.Where(r => r.Status == AttendanceIncidentStatus.AwaitingEmployee).ToList();
                    result[ChecklistCatalog.AttendanceAwaitingResponse] = new ComputedItem(
                        mine.Count, true, subject, subject, null,
                        mine.Count == 0 ? null : mine.Max(r => r.UpdatedAtUtc ?? r.CreatedAtUtc),
                        null, "/app/attendance");
                }

                if (wanted.Contains(ChecklistCatalog.AttendanceAwaitingHrReview))
                {
                    var hr = rows.Where(r => r.Status == AttendanceIncidentStatus.AwaitingHr).ToList();
                    // المسؤول هنا الموارد البشريّة لا شخصًا بعينه ⟹ لا OwnerUserId ولا «فعلي».
                    result[ChecklistCatalog.AttendanceAwaitingHrReview] = new ComputedItem(
                        hr.Count, true, null, null, null,
                        hr.Count == 0 ? null : hr.Max(r => r.UpdatedAtUtc ?? r.CreatedAtUtc),
                        null, "/app/attendance");
                }
            }
        }

        // ===== (5) الإجازات والاستئذانات المفتوحة =====
        if (wanted.Contains(ChecklistCatalog.LeaveRequestsOpen))
        {
            var rows = await _db.LeaveRequests.AsNoTracking()
                .Where(l => l.RequesterUserId == subject
                            && l.Status != LeaveRequestStatus.HrApproved
                            && l.Status != LeaveRequestStatus.HrRejected
                            && l.Status != LeaveRequestStatus.Cancelled)
                .Select(l => new
                {
                    l.CreatedAtUtc, l.Status,
                    l.TeamLeaderReviewerId, l.ManagerReviewerId, l.HrReviewerId
                })
                .ToListAsync(ct);

            // «فعلي» إن كنتُ المُراجِع المنتظَر لأيّ طلب مفتوح — لا لمجرّد كوني في السلسلة.
            var mine = rows.Any(r => r.TeamLeaderReviewerId == ctx.ViewerUserId
                                     || r.ManagerReviewerId == ctx.ViewerUserId
                                     || r.HrReviewerId == ctx.ViewerUserId);

            result[ChecklistCatalog.LeaveRequestsOpen] = new ComputedItem(
                rows.Count, true, null, mine ? ctx.ViewerUserId : null, null,
                rows.Count == 0 ? null : rows.Max(r => r.CreatedAtUtc),
                null, "/app/leave");
        }

        // ===== (6) طلبات الخدمة المفتوحة =====
        if (wanted.Contains(ChecklistCatalog.ServiceRequestsOpen))
        {
            var rows = await _db.EmployeeServiceRequests.AsNoTracking()
                .Where(r => r.RequesterUserId == subject
                            && r.Status != EmployeeServiceRequestStatus.Completed
                            && r.Status != EmployeeServiceRequestStatus.Rejected
                            && r.Status != EmployeeServiceRequestStatus.Cancelled)
                .Select(r => new { r.CreatedAtUtc })
                .ToListAsync(ct);

            result[ChecklistCatalog.ServiceRequestsOpen] = new ComputedItem(
                rows.Count, true, null, null, null,
                rows.Count == 0 ? null : rows.Max(r => r.CreatedAtUtc),
                null, "/app/employee-services");
        }

        // ===== (7) الملاحظات الإداريّة المفتوحة التي تتطلّب إجراءً =====
        if (wanted.Contains(ChecklistCatalog.NotesRequiringAction))
        {
            var rows = await _db.ManagementNotes.AsNoTracking()
                .Where(n => n.EntityType == ManagementNoteEntityType.User
                            && n.EntityId == subject
                            && n.RequiresAction
                            && n.Status == ManagementNoteStatus.Open)
                .Select(n => new { n.Sensitivity, n.CreatedAtUtc })
                .ToListAsync(ct);

            // العدّ **بعد** ترشيح حسّاسيّة كلّ ملاحظة، وإلّا سرّب الرقمُ وجود ملاحظة محجوبة.
            var seen = rows
                .Where(n => _visibility.CanSee(ctx, NoteSensitivity.Effective(n.Sensitivity)))
                .ToList();

            result[ChecklistCatalog.NotesRequiringAction] = new ComputedItem(
                seen.Count, true, null, null, null,
                seen.Count == 0 ? null : seen.Max(n => n.CreatedAtUtc),
                null, $"/app/employee/{subject}");
        }

        // ===== (8) خطط التحسين المفتوحة =====
        if (wanted.Contains(ChecklistCatalog.ImprovementPlansOpen))
        {
            var rows = await _db.ImprovementPlans.AsNoTracking()
                .Where(p => p.SubjectUserId == subject
                            && p.Status != ImprovementPlanStatus.Completed
                            && p.Status != ImprovementPlanStatus.Cancelled)
                .Select(p => new { p.CreatedAtUtc, p.DueDateUtc, p.OwnerId })
                .ToListAsync(ct);

            var owner = rows.Select(r => (Guid?)r.OwnerId).FirstOrDefault(o => o != Guid.Empty);
            var due = rows.Where(r => r.DueDateUtc != null)
                .Select(r => DateOnly.FromDateTime(r.DueDateUtc!.Value))
                .DefaultIfEmpty()
                .Min();

            result[ChecklistCatalog.ImprovementPlansOpen] = new ComputedItem(
                rows.Count, true, owner, owner, due == default ? null : due,
                rows.Count == 0 ? null : rows.Max(r => r.CreatedAtUtc),
                null, $"/app/employee/{subject}");
        }

        // ===== (9) اكتمال بيانات التعيين =====
        if (wanted.Contains(ChecklistCatalog.ProfileCompleteness))
        {
            var u = await _db.Users.AsNoTracking()
                .Where(x => x.Id == subject)
                .Select(x => new { x.JobRoleId, x.TeamId, x.DepartmentId, x.ManagerId, x.IsActive })
                .FirstOrDefaultAsync(ct);

            var missing = new List<string>();
            if (u is not null)
            {
                if (u.JobRoleId is null) missing.Add("المسمّى الوظيفيّ");
                if (u.TeamId is null) missing.Add("الفريق");
                if (u.DepartmentId is null) missing.Add("الإدارة");
                if (u.ManagerId is null) missing.Add("المدير المباشر");
            }

            result[ChecklistCatalog.ProfileCompleteness] = new ComputedItem(
                missing.Count, true, null, null, null, null,
                missing.Count == 0 ? null : $"الناقص: {string.Join('،', missing)}",
                $"/app/employee/{subject}");
        }

        return result;
    }

    private static void AddNotApplicable(
        Dictionary<string, ComputedItem> result, IReadOnlySet<string> wanted, string key)
    {
        if (wanted.Contains(key))
            result[key] = new ComputedItem(0, false, null, null, null, null, null, null);
    }

    private static void AddObligation(
        Dictionary<string, ComputedItem> result, IReadOnlySet<string> wanted, string key,
        IReadOnlyList<ObligationDto> all, ObligationKind kind, string link)
    {
        if (!wanted.Contains(key)) return;

        var mine = all.Where(o => o.Kind == kind).ToList();

        // لا إسناد إطلاقًا ⟵ «غير منطبق»: عرضُ صفرٍ هنا يقول «مطلوب ومُنجَز» وهو كذب.
        if (mine.Count == 0 || mine.All(o => o.State == ObligationState.NotApplicable))
        {
            result[key] = new ComputedItem(0, false, null, null, null, null, null, link);
            return;
        }

        var open = mine.Where(o => o.State is ObligationState.Pending or ObligationState.Missing).ToList();
        var owner = open.Select(o => o.OwnerUserId).FirstOrDefault(o => o != null);

        result[key] = new ComputedItem(
            open.Count, true, owner, owner,
            open.Count == 0 ? null : open.Min(o => o.DueAt),
            mine.Where(o => o.FulfilledAtUtc != null).Select(o => o.FulfilledAtUtc).DefaultIfEmpty().Max(),
            null, link);
    }
}

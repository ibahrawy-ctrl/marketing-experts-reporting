using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
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
    private readonly IReportTemplateService _templates;
    private readonly IReportViewGrantService _grants;

    // الحالات التي يجوز لمستفيد منح الرؤية رؤيتها فقط (REPORT-VIEW-GRANTS-R1):
    // تقارير مُرسَلة رسميًّا بحالة معتمدة — تُستبعد المسودّة (Draft) والمُعادة للتعديل (Returned).
    private static readonly SubmissionStatus[] GrantViewableStatuses =
    {
        SubmissionStatus.Submitted,
        SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel,
        SubmissionStatus.Escalated,
        SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    public SubmissionService(AppDbContext db, ICurrentUser currentUser,
        INotificationService notifications, IAuditService audit, IScopeResolver scope, IClientProjectAccess access,
        IReportTemplateService templates, IReportViewGrantService grants)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _scope = scope;
        _access = access;
        _templates = templates;
        _grants = grants;
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

        // حارس الإسناد المركزي (TEMPLATE-ROLE-GUARD): يُمنع إنشاء/فتح مسودة لقالب غير مُسنَد للمستخدم،
        // بنفس منطق assignedOnly المصدر الوحيد للحقيقة. لا إعفاء ضمني لأي دور (لا انتحال بالنيابة هنا).
        if (!await _templates.IsTemplateAssignedToUserAsync(userId, request.ReportTemplateId, ct))
            return Result<SubmissionDto>.Failure(
                "هذا القالب غير مُسنَد إليك ولا يمكنك إنشاء تقرير به.", "report.template_not_assigned");

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

        // ROLE-AWARE-REPORTING-CALENDAR — التحقّق الخادميّ من مفتاح الدورة الأسبوعي (Phase 2.4):
        // لا نثق بمفتاح الفترة القادم من الواجهة. للتقارير الأسبوعية يجب أن يكون المفتاح دورةً صالحة بنيويًّا
        // (Sat→Fri عبر ReportingCalendarPolicy) وألّا يكون دورةً مستقبلية لم تبدأ بعد. لا يُطبَّق على اليومي
        // (مفتاح المبيعات تاريخ YYYY-MM-DD لا دورة). لا تصحيح بيانات ولا تغيير مفتاح مخزَّن — منعُ إنشاءٍ فقط.
        if (expectedCadence == PeriodType.Weekly)
        {
            if (!ReportingCalendarPolicy.IsValidCycleKey(periodKey))
                return Result<SubmissionDto>.Failure("مفتاح الدورة غير صالح.", "report.cycle_key_invalid");
            var cycleStart = ReportingCalendarPolicy.CycleRange(periodKey).Start;
            if (cycleStart > ReportingCalendarPolicy.RiyadhToday())
                return Result<SubmissionDto>.Failure("لا يمكن إنشاء تقرير لدورة لم تبدأ بعد.", "calendar.cycle_not_open");
        }
        else // Daily — تقارير المبيعات اليومية: لا نثق بمفتاح اليوم القادم من الواجهة.
        {
            // مفتاح اليوم يجب أن يكون YYYY-MM-DD صالحًا بنيويًّا (يرفض 2026-02-30/2026-13-01).
            if (!ReportingCalendarPolicy.IsValidDayKey(periodKey))
                return Result<SubmissionDto>.Failure("مفتاح اليوم غير صالح.", "report.daily_key_invalid");
            var day = ReportingCalendarPolicy.ParseDayKey(periodKey);
            // لا تقارير في العطلة الأسبوعية (الجمعة وحدها) بحسب سياسة اليوميّ. السبت يوم عمل.
            if (ReportingCalendarPolicy.IsDailyHoliday(day))
                return Result<SubmissionDto>.Failure(
                    "لا تقارير يومية في العطلة الأسبوعية (الجمعة).", "calendar.day_is_holiday");
            // لا يوم مستقبليّ لم يبدأ بعد.
            if (day > ReportingCalendarPolicy.RiyadhToday())
                return Result<SubmissionDto>.Failure(
                    "لا يمكن إنشاء تقرير ليوم لم يبدأ بعد.", "calendar.future_day_locked");
        }

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

        // حارس الإسناد المركزي (يمنع استغلال أي مسودة قائمة لقالب غير مُسنَد): يجب أن يكون القالب مُسنَدًا لصاحب التسليم.
        var saveTemplateId = await _db.ReportTemplateVersions
            .Where(v => v.Id == submission.ReportTemplateVersionId)
            .Select(v => v.ReportTemplateId).FirstAsync(ct);
        if (!await _templates.IsTemplateAssignedToUserAsync(submission.SubmitterId, saveTemplateId, ct))
            return Result<SubmissionDto>.Failure(
                "هذا القالب غير مُسنَد إليك ولا يمكنك تعديل تقرير به.", "report.template_not_assigned");

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
            // طبقة الحماية الثانية (RC-3 Task 2B): تطبيع الخانات العربية-الهندية/الفارسية إلى لاتينية قبل التخزين
            // مهما كان مصدر الطلب (واجهة/سكربت/استيراد) ⇒ لا تُخزَّن خانة عربية في القاعدة إطلاقًا. تطبيع الخانات فقط
            // آمن للنصوص الحرّة (لا يمسّ الحروف/العلامات) ويوحّد الأرقام داخل شبكات الجداول (ValueJson).
            value.ValueText = NumericNormalizer.NormalizeDigits(input.ValueText);
            value.ValueNumber = input.ValueNumber;
            value.ValueDate = input.ValueDate;
            value.ValueBool = input.ValueBool;
            value.ValueJson = NumericNormalizer.NormalizeJsonDigits(input.ValueJson);
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

        // حارس الإسناد المركزي (دفاع متعمّق قبل الإرسال النهائي): يجب أن يكون القالب مُسنَدًا لصاحب التسليم.
        var submitTemplateId = await _db.ReportTemplateVersions
            .Where(v => v.Id == submission.ReportTemplateVersionId)
            .Select(v => v.ReportTemplateId).FirstAsync(ct);
        if (!await _templates.IsTemplateAssignedToUserAsync(submission.SubmitterId, submitTemplateId, ct))
            return Result<SubmissionDto>.Failure(
                "هذا القالب غير مُسنَد إليك ولا يمكنك إرسال تقرير به.", "report.template_not_assigned");

        // الحقول العادية المطلوبة — يُستثنى منها أقسام المشاريع المتكررة لأن لها تحققًا خاصًّا أدناه.
        var requiredFields = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == submission.ReportTemplateVersionId
                        && f.IsRequired && f.FieldType != FieldType.SectionHeader
                        && f.FieldType != FieldType.ProjectRepeatableSection)
            .Select(f => new { f.Id, f.Label }).ToListAsync(ct);

        var missing = requiredFields
            .Where(f => !HasValue(submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == f.Id)))
            .Select(f => f.Label).ToList();
        if (missing.Count > 0)
            return Result<SubmissionDto>.Failure($"حقول مطلوبة غير مكتملة: {string.Join("، ", missing)}", "submission.required_fields_missing");

        // تحقق أقسام المشاريع المتكررة (ProjectRepeatableSection): الحد الأدنى/الأقصى، صلاحية المشروع ضمن النطاق، الحقول الفرعية المطلوبة.
        var sectionErrors = await ValidateRepeatableSectionsAsync(submission, ct);
        if (sectionErrors.Count > 0)
            return Result<SubmissionDto>.Failure(string.Join("، ", sectionErrors), "submission.repeatable_section_invalid");

        // ERDS Phase 2A: تحقّق رقمي/منطقي آمن لخلايا جدول «مبيعات B2C حسب الدورة» فقط (مطابقة الأعمدة)، لا يمسّ أي TableGrid آخر.
        var versionGrids = await GetVersionTableGridsAsync(submission.ReportTemplateVersionId, ct);
        var gridErrors = ValidateB2cByCourseGrids(submission, versionGrids);
        // Phase 7: تحقّق مماثل لجدولَي قالب «B2C — بيانات جديدة/قديمة» (New Leads + Old CRM)، مطابقة الأعمدة فقط.
        gridErrors.AddRange(ValidateB2cNewOldGrids(submission, versionGrids));
        // RC-3 Task 2: تحقّق جدول «مبيعات B2B حسب الخدمة» (مطابقة الأعمدة فقط) — لا يمسّ أيّ TableGrid آخر.
        gridErrors.AddRange(ValidateB2bByServiceGrids(submission, versionGrids));
        // RC-3: تحقّق جدولَي قالب «B2B — حسب مصدر البيانات» (New Leads + Data Scraping)، مطابقة الأعمدة فقط.
        gridErrors.AddRange(ValidateB2bBySourceGrids(submission, versionGrids));
        if (gridErrors.Count > 0)
            return Result<SubmissionDto>.Failure(string.Join("، ", gridErrors), "submission.grid_invalid");

        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == submission.SubmitterId, ct);
        // APPROVAL-FALLBACK-R1: تحديد أول معتمِد عبر سلسلة احتياطية بدل الاعتماد على المدير المباشر وحده.
        // الترتيب: قائد فريق المقدّم ← المدير المباشر (ManagerId) ← أول مدير عام نشط ← أول Admin/CEO نشط.
        // لا يُغلق التقرير لمجرد غياب قائد الفريق أو المدير طالما وُجد بديل أعلى، مع تفادي اعتماد المقدّم لنفسه.
        var firstApproverId = me is null
            ? (Guid?)null
            : await ResolveFirstApproverAsync(me.Id, me.TeamId, me.ManagerId, ct);

        submission.Status = SubmissionStatus.Submitted;
        submission.SubmittedAtUtc = DateTime.UtcNow;
        submission.UpdatedAtUtc = DateTime.UtcNow;

        if (firstApproverId is Guid approverId && approverId != Guid.Empty)
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
            // لا يوجد أي معتمِد بديل إطلاقًا (مثلاً مقدّم من الطبقة العليا بلا مدير) — يُغلق التسليم مباشرة.
            submission.Status = SubmissionStatus.Closed;
            submission.ClosedAtUtc = DateTime.UtcNow;
            submission.CurrentApproverId = null;
        }

        await _db.SaveChangesAsync(ct);

        if (submission.CurrentApproverId is Guid approver)
            await _notifications.NotifyAsync(approver, "submission.submitted",
                "تقرير بانتظار اعتمادك", null, $"/app/submissions?open={submission.Id}", ct);
        await _audit.LogAsync(_currentUser.UserId, "submission.submitted", nameof(ReportSubmission), submission.Id, ct: ct);

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    // ===== APPROVAL-FALLBACK-R1: سلسلة اعتماد احتياطية لتقارير التسليم فقط =====
    // لا تمسّ مسار الإجازات/الأذونات ولا طلبات الموارد البشرية (تلك لها Endpoints ومسارات منفصلة تمامًا).
    //
    // الطبقة العليا (Senior tier) = { GeneralManager, Admin, CEO }. عند وصول الاعتماد لهذه الطبقة
    // وغياب مدير مباشر صالح للمعتمِد ⇒ يُعدّ الاعتماد نهائيًّا فيُغلق التقرير (لا تصعيد عام أعلى منها).
    // أما المعتمِد الأدنى (موظّف/قائد فريق/مدير) بلا مدير ⇒ يُصعَّد لأول مدير عام نشط ثم أول Admin/CEO نشط.
    // هذا يحافظ على السلوك القائم (سلاسل ManagerId الصحيحة، ومقدّم الطبقة العليا يُغلق مباشرة)
    // ويمنع في الوقت ذاته إغلاق/تعليق تقرير موظّف لمجرد غياب قائد الفريق أو المدير.
    private static readonly string[] SeniorRoles = { Roles.GeneralManager, Roles.Admin, Roles.Ceo };
    private static readonly string[] GeneralManagerRoles = { Roles.GeneralManager };
    private static readonly string[] FinalFallbackRoles = { Roles.Admin, Roles.Ceo };

    /// <summary>هل المستخدم مفعَّل (IsActive)؟</summary>
    private Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct)
        => _db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct);

    /// <summary>هل يحمل المستخدم أيًّا من الأدوار المحدّدة؟ (عبر ربط UserRoles↔Roles)</summary>
    private Task<bool> UserHasAnyRoleAsync(Guid userId, string[] roleNames, CancellationToken ct)
        => (from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Name != null && roleNames.Contains(r.Name)
            select ur.UserId).AnyAsync(ct);

    /// <summary>
    /// أول مستخدم نشط يحمل أحد الأدوار المطلوبة مع استبعاد مجموعة معيّنة (المقدّم/المُزارين)،
    /// بترتيب حتميّ (OrderBy على المعرّف) لضمان ثبات الاختيار.
    /// </summary>
    private async Task<Guid?> FirstActiveUserInRoleAsync(string[] roleNames, HashSet<Guid> exclude, CancellationToken ct)
    {
        var candidates = await (from ur in _db.UserRoles
                                join r in _db.Roles on ur.RoleId equals r.Id
                                join u in _db.Users on ur.UserId equals u.Id
                                where u.IsActive && r.Name != null && roleNames.Contains(r.Name)
                                select u.Id).Distinct().ToListAsync(ct);
        foreach (var id in candidates.OrderBy(x => x))
            if (!exclude.Contains(id)) return id;
        return null;
    }

    /// <summary>
    /// تحديد أول معتمِد لتسليم جديد: قائد فريق المقدّم ← المدير المباشر ← أول مدير عام نشط ← أول Admin/CEO نشط.
    /// يُستبعَد المقدّم نفسه في كل خطوة (منع اعتماد الذات). يُرجِع null فقط عند انعدام أي بديل صالح
    /// (كمقدّم من الطبقة العليا بلا مدير) ⇒ عندها يُغلق التسليم مباشرة.
    /// </summary>
    private async Task<Guid?> ResolveFirstApproverAsync(Guid submitterId, Guid? submitterTeamId, Guid? submitterManagerId, CancellationToken ct)
    {
        // تجاوز خطوة قائد الفريق (Direct Reporting Override): قاعدة عامة — إن كان المقدّم مضبوطًا على
        // BypassTeamLeaderApproval=true فلا قائد فريق فعلي له في مسار اعتماد التقارير رغم بقائه ضمن فريق
        // له قائد، فيبدأ المسار مباشرةً من المدير المباشر ثم الاحتياطي (GM ← Admin/CEO). لا يمسّ TeamId.
        var submitterBypassesTeamLeader = await _db.Users.Where(u => u.Id == submitterId)
            .Select(u => u.BypassTeamLeaderApproval).FirstOrDefaultAsync(ct);

        // 1) قائد فريق المقدّم (الفريق نشط، القائد نشط، وليس المقدّم نفسه) — يُتخطّى لموظّف Direct Reporting.
        if (!submitterBypassesTeamLeader && submitterTeamId is Guid teamId)
        {
            var tlId = await _db.Teams
                .Where(t => t.Id == teamId && t.IsActive)
                .Select(t => t.TeamLeaderId)
                .FirstOrDefaultAsync(ct);
            if (tlId is Guid tl && tl != submitterId && await IsActiveUserAsync(tl, ct))
                return tl;
        }

        // 2) المدير المباشر (ManagerId نشط وليس المقدّم نفسه).
        if (submitterManagerId is Guid mgr && mgr != submitterId && await IsActiveUserAsync(mgr, ct))
            return mgr;

        // 3) إن لم يكن المقدّم ضمن الطبقة العليا: تصعيد عام لأول مدير عام ثم أول Admin/CEO.
        var submitterIsSenior = await UserHasAnyRoleAsync(submitterId, SeniorRoles, ct);
        if (!submitterIsSenior)
        {
            var exclude = new HashSet<Guid> { submitterId };
            var gm = await FirstActiveUserInRoleAsync(GeneralManagerRoles, exclude, ct);
            if (gm is Guid g) return g;
            var top = await FirstActiveUserInRoleAsync(FinalFallbackRoles, exclude, ct);
            if (top is Guid t) return t;
        }

        // 4) لا بديل إطلاقًا ⇒ إغلاق مباشر.
        return null;
    }

    /// <summary>
    /// قائد الفريق الفعليّ لمقدّم التقرير: TeamId للمقدّم ← Team.TeamLeaderId (الفريق نشط، القائد نشط، وليس المقدّم نفسه).
    /// يُرجِع null إن لم يوجد قائد فريق صالح — عندها يبقى المسار الاحتياطيّ (تصعيد للمدير ثم GM/CEO) بلا تغيير.
    /// </summary>
    private async Task<Guid?> ResolveSubmitterTeamLeaderIdAsync(Guid submitterId, CancellationToken ct)
    {
        // تجاوز خطوة قائد الفريق (Direct Reporting Override): الموظّف Direct Reporting لا قائد فريق فعلي
        // له في مسار التقارير ⇒ لا يبدأ مساره عند قائد الفريق ولا يُعاد إدخال قائد الفريق في التصعيد التالي.
        var bypassesTeamLeader = await _db.Users
            .Where(u => u.Id == submitterId)
            .Select(u => u.BypassTeamLeaderApproval)
            .FirstOrDefaultAsync(ct);
        if (bypassesTeamLeader) return null;

        var teamId = await _db.Users
            .Where(u => u.Id == submitterId)
            .Select(u => u.TeamId)
            .FirstOrDefaultAsync(ct);
        if (teamId is not Guid tid) return null;

        var tlId = await _db.Teams
            .Where(t => t.Id == tid && t.IsActive)
            .Select(t => t.TeamLeaderId)
            .FirstOrDefaultAsync(ct);
        if (tlId is Guid tl && tl != submitterId && await IsActiveUserAsync(tl, ct))
            return tl;
        return null;
    }

    /// <summary>
    /// تحديد المعتمِد التالي بعد قرار المعتمِد الحالي: المدير المباشر للمعتمِد الحالي ←
    /// (إن كان المعتمِد الحالي من الطبقة العليا بلا مدير ⇒ اعتماد نهائي/إغلاق) ← وإلا تصعيد عام
    /// لأول مدير عام ثم أول Admin/CEO. تُستبعَد كل المعرّفات المُزارة سابقًا (منع الحلقات) والمقدّم (منع اعتماد الذات).
    /// يُرجِع null عندما يجب أن يُغلق التسليم نهائيًّا.
    /// </summary>
    private async Task<Guid?> ResolveNextApproverAsync(Guid currentApproverId, Guid submitterId, HashSet<Guid> visited, CancellationToken ct)
    {
        // 0) B2C-UAT-FIXPACK — إيقاف صعود التقرير بعد قائد الفريق:
        // إذا كان المعتمِد الحالي هو قائد الفريق الفعليّ لمقدّم التقرير ⇒ اعتماده نهائيّ فيُغلق التقرير،
        // ولا تُنشأ خطوة اعتماد للمدير. الاحتياطيّ محفوظ: عند غياب قائد فريق فعليّ لن يبدأ المسار عنده
        // (ResolveFirstApproverAsync يعيد المدير/GM/CEO)، فلن يساوي المعتمِد الحالي قائد الفريق،
        // فيمرّ للمنطق الأصلي (تصعيد للمدير ثم المدير العام ثم Admin/CEO) بلا كسر لأي تقرير بلا قائد فريق.
        var teamLeaderId = await ResolveSubmitterTeamLeaderIdAsync(submitterId, ct);
        if (teamLeaderId is Guid tlId && tlId == currentApproverId)
            return null;

        // 1) المدير المباشر للمعتمِد الحالي (نشط، غير مُزار، وليس المقدّم).
        var currentManagerId = await _db.Users
            .Where(u => u.Id == currentApproverId)
            .Select(u => u.ManagerId)
            .FirstOrDefaultAsync(ct);
        if (currentManagerId is Guid mgr && mgr != submitterId && !visited.Contains(mgr) && await IsActiveUserAsync(mgr, ct))
            return mgr;

        // 2) المعتمِد الحالي من الطبقة العليا (GM/Admin/CEO) بلا مدير صالح ⇒ الاعتماد نهائي (إغلاق).
        var currentIsSenior = await UserHasAnyRoleAsync(currentApproverId, SeniorRoles, ct);
        if (currentIsSenior)
            return null;

        // 3) معتمِد أدنى بلا مدير ⇒ تصعيد عام لأول مدير عام ثم أول Admin/CEO (مع منع الحلقات واعتماد الذات).
        var exclude = new HashSet<Guid>(visited) { submitterId };
        var gm = await FirstActiveUserInRoleAsync(GeneralManagerRoles, exclude, ct);
        if (gm is Guid g) return g;
        var top = await FirstActiveUserInRoleAsync(FinalFallbackRoles, exclude, ct);
        if (top is Guid t) return t;

        // 4) لا بديل ⇒ إغلاق نهائي.
        return null;
    }

    /// <summary>
    /// حذف مسودة تقرير. صاحب المسودة فقط (لا يُسمح للأدمن/القائد/المدير بحذف مسودة موظّف آخر)،
    /// وحالة Draft حصرًا. الحذف يزيل القيم المرتبطة وخطوات الاعتماد عبر Cascade، ولا يمسّ القالب/النسخة/المشروع.
    /// </summary>
    public async Task<Result> DeleteDraftAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await _db.ReportSubmissions
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result.Failure("التسليم غير موجود.", "submission.not_found");

        // صاحب المسودة فقط — منع IDOR وعدم السماح بأي تصعيد (حتى الأدمن لا يحذف مسودة غيره).
        if (_currentUser.UserId != submission.SubmitterId)
            return Result.Failure("لا يمكنك حذف مسودة لا تخصّك.", "auth.forbidden");

        // الحذف مسموح فقط عندما تكون الحالة Draft — أي حالة أخرى (مُرسَل/مُراجَع/معتمد/مُغلق/مُعاد) ممنوعة.
        if (submission.Status != SubmissionStatus.Draft)
            return Result.Failure("لا يمكن حذف تقرير بعد إرساله؛ الحذف متاح للمسودات فقط.", "submission.delete_forbidden.conflict");

        // Cascade يحذف submission_field_values + approval_steps المرتبطة بالمسودة. لا يمسّ القالب/النسخة/المشروع.
        _db.ReportSubmissions.Remove(submission);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "submission.draft_deleted", nameof(ReportSubmission), submission.Id, ct: ct);

        return Result.Success();
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

        // APPROVAL-FALLBACK-R1: تحديد المعتمِد التالي عبر السلسلة الاحتياطية بدل ManagerId وحده،
        // مع منع الحلقات عبر مجموعة المعتمِدين السابقين (visited) وتفادي اعتماد المقدّم لنفسه.
        var visited = submission.ApprovalSteps.Select(a => a.ApproverId).ToHashSet();
        var nextApproverId = await ResolveNextApproverAsync(step.ApproverId, submission.SubmitterId, visited, ct);

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
                    "أُعيد تقريرك للتعديل", comment, $"/app/submissions?open={submission.Id}", ct);
                break;
            case ApprovalStatus.Escalated:
                if (submission.CurrentApproverId is Guid esc)
                    await _notifications.NotifyAsync(esc, "submission.escalated",
                        "تصعيد بانتظار اعتمادك", comment, $"/app/submissions?open={submission.Id}", ct);
                break;
            case ApprovalStatus.Approved:
                if (submission.CurrentApproverId is Guid next)
                    await _notifications.NotifyAsync(next, "submission.submitted",
                        "تقرير بانتظار اعتمادك", null, $"/app/submissions?open={submission.Id}", ct);
                else
                    await _notifications.NotifyAsync(submission.SubmitterId, "submission.approved",
                        "تم اعتماد تقريرك", comment, $"/app/submissions?open={submission.Id}", ct);
                break;
        }
        await _audit.LogAsync(_currentUser.UserId, $"submission.{decision.ToString().ToLowerInvariant()}",
            nameof(ReportSubmission), submission.Id, ct: ct);

        return Result<SubmissionDto>.Success(await BuildDtoAsync(submissionId, ct));
    }

    public async Task<Result<SubmissionDto>> AdminDeleteAsync(Guid submissionId, AdminDeleteRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result<SubmissionDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!_currentUser.IsInAnyRole(Roles.AdminReportKpiDeleters))
            return Result<SubmissionDto>.Failure("الحذف الإداريّ من صلاحية مدير النظام أو الرئيس التنفيذي أو المدير العام فقط.", "auth.forbidden");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Result<SubmissionDto>.Failure("سبب الحذف الإداريّ إلزاميّ.", "submission.delete_reason_required");

        var submission = await _db.ReportSubmissions.Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null) return Result<SubmissionDto>.Failure("التسليم غير موجود.", "submission.not_found");
        if (submission.IsDeleted)
            return Result<SubmissionDto>.Failure("التسليم محذوف إداريًّا بالفعل.", "submission.already_deleted.conflict");

        var now = DateTime.UtcNow;

        // RESTORE-ARCHIVE-GOVERNANCE-R1 (Phase 7) — لقطة كاملة لسير العمل قبل الحذف تُحفَظ في الأثر التدقيقيّ:
        // الحالة قبل الحذف + المعتمِد الحاليّ + كل خطوات الاعتماد (المستوى/المعتمِد/الحالة/القرار). تُلتقَط قبل أيّ تعديل.
        // تُثري الاسترجاع Hybrid (Phase 8) بمصدر أساسيّ لإعادة بناء المسار التاريخيّ، دون أيّ تغيير في سلوك الحذف.
        var statusBeforeDelete = submission.Status;
        var currentApproverBeforeDelete = submission.CurrentApproverId;
        var workflowBeforeDelete = submission.ApprovalSteps
            .OrderBy(a => a.Level)
            .Select(a => new
            {
                level = a.Level,
                approverId = a.ApproverId,
                status = a.Status.ToString(),
                comment = a.Comment,
                decidedAtUtc = a.DecidedAtUtc
            })
            .ToList();

        // حذف إداريّ ناعم: لا حذف صفوف — يُعلَّم IsDeleted فيختفي من كل القوائم/التجميعات (Global Query Filter) ومن «بانتظار اعتمادي».
        submission.IsDeleted = true;
        submission.DeletedAtUtc = now;
        submission.DeletedByUserId = userId;
        submission.DeletionReason = reason;
        submission.UpdatedAtUtc = now;

        // خطوات الاعتماد المعلّقة تُحوَّل إلى CancelledByAdministrativeDeletion (لا حذف)، وتصفير المعتمِد الحالي
        // كي لا يبقى التقرير معلّقًا في «بانتظار اعتماد» أيّ مستخدم.
        foreach (var step in submission.ApprovalSteps.Where(a => a.Status == ApprovalStatus.Pending))
        {
            step.Status = ApprovalStatus.CancelledByAdministrativeDeletion;
            step.DecidedAtUtc = now;
        }
        submission.CurrentApproverId = null;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(userId, "submission.admin_deleted", nameof(ReportSubmission), submission.Id,
            JsonSerializer.Serialize(new
            {
                reason,
                submitterId = submission.SubmitterId,
                periodKey = submission.PeriodKey,
                statusBeforeDelete = statusBeforeDelete.ToString(),
                currentApproverId = currentApproverBeforeDelete,
                workflowBeforeDelete
            }), ct: ct);

        var dto = await BuildDtoAsync(submissionId, ct);
        return Result<SubmissionDto>.Success(dto);
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
            // إضافة تقارير مُرسِلين أُتيحوا عبر منح الرؤية المخفيّ (عرض فقط، حالات معتمدة فقط) —
            // معزول تمامًا عن ScopeResolver؛ لا يوسّع نطاق الدور بل يضيف اتحادًا محدودًا للقراءة.
            var grantIds = await _grants.ResolveGrantedSubmitterIdsAsync(userId, ct);
            if (grantIds.Count == 0)
                q = q.Where(s => ids.Contains(s.SubmitterId));
            else
                q = q.Where(s => ids.Contains(s.SubmitterId)
                                 || (grantIds.Contains(s.SubmitterId) && GrantViewableStatuses.Contains(s.Status)));
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
            var grantIds = await _grants.ResolveGrantedSubmitterIdsAsync(userId, ct);
            if (grantIds.Count == 0)
                q = q.Where(s => ids.Contains(s.SubmitterId));
            else
                q = q.Where(s => ids.Contains(s.SubmitterId)
                                 || (grantIds.Contains(s.SubmitterId) && GrantViewableStatuses.Contains(s.Status)));
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
        // IgnoreQueryFilters كي تُبنى الحمولة حتى بعد الحذف الإداريّ الناعم (IsDeleted=true) في AdminDeleteAsync؛
        // الجلب بالمعرّف فلا يُدخِل صفوفًا محذوفة في المسارات العادية.
        var s = await _db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
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
        if (scope.Contains(s.SubmitterId)) return true;
        // منح الرؤية المخفيّ (عرض فقط): يُسمح بقراءة تقرير مُرسِل مُتاح فقط بحالة معتمدة (لا مسودّة/مُعادة).
        // canEdit يبقى false (المستفيد ليس صاحب التقرير ولا الموافِق) فلا يكتسب أيّ قدرة قرار/تعديل.
        if (GrantViewableStatuses.Contains(s.Status))
        {
            var grantIds = await _grants.ResolveGrantedSubmitterIdsAsync(userId, ct);
            if (grantIds.Contains(s.SubmitterId)) return true;
        }
        return false;
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

    // ===== أقسام المشاريع المتكررة (Multi-Project MVP) =====
    // الإعداد في ConfigJson للحقل، والقيمة في ValueJson للتسليم كقائمة {projectId, answers}.
    private static readonly JsonSerializerOptions RepeatableJson = new() { PropertyNameCaseInsensitive = true };

    private sealed class RepeatableConfig
    {
        public bool ProjectRequired { get; set; } = true;
        public int MinProjects { get; set; }
        public int MaxProjects { get; set; }
        public List<RepeatableField> Fields { get; set; } = new();
    }

    private sealed class RepeatableField
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    private sealed class RepeatableEntry
    {
        public Guid? ProjectId { get; set; }
        public Dictionary<string, JsonElement> Answers { get; set; } = new();
    }

    private static RepeatableConfig ParseSectionConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return new RepeatableConfig();
        try { return JsonSerializer.Deserialize<RepeatableConfig>(configJson, RepeatableJson) ?? new RepeatableConfig(); }
        catch { return new RepeatableConfig(); }
    }

    private static List<RepeatableEntry> ParseSectionEntries(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) return new List<RepeatableEntry>();
        try { return JsonSerializer.Deserialize<List<RepeatableEntry>>(valueJson, RepeatableJson) ?? new List<RepeatableEntry>(); }
        catch { return new List<RepeatableEntry>(); }
    }

    private static bool AnswerHasValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => !string.IsNullOrWhiteSpace(e.GetString()),
        JsonValueKind.Number => true,
        JsonValueKind.True or JsonValueKind.False => true,
        _ => false
    };

    private async Task<List<string>> ValidateRepeatableSectionsAsync(ReportSubmission submission, CancellationToken ct)
    {
        var errors = new List<string>();
        var sections = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == submission.ReportTemplateVersionId
                        && f.FieldType == FieldType.ProjectRepeatableSection)
            .Select(f => new { f.Id, f.Label, f.IsRequired, f.ConfigJson })
            .ToListAsync(ct);
        if (sections.Count == 0) return errors;

        var vis = await _access.ResolveAsync(ct);

        foreach (var sec in sections)
        {
            var config = ParseSectionConfig(sec.ConfigJson);
            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == sec.Id);
            var entries = ParseSectionEntries(value?.ValueJson);

            var min = sec.IsRequired ? Math.Max(config.MinProjects, 1) : Math.Max(config.MinProjects, 0);
            if (entries.Count < min)
            {
                errors.Add($"قسم «{sec.Label}» يتطلب {min} مشروعًا على الأقل.");
                continue;
            }
            if (config.MaxProjects > 0 && entries.Count > config.MaxProjects)
            {
                errors.Add($"قسم «{sec.Label}» يسمح بحد أقصى {config.MaxProjects} مشروعًا.");
                continue;
            }

            // منع تكرار المشروع داخل القسم الواحد: صفّ واحد لكل مشروع في التقرير (فترة واحدة) —
            // يمنع ازدواج بيانات نفس (العميل/المشروع) ضمن نفس التسليم. مقصور على القسم الحالي.
            var seenProjects = new HashSet<Guid>();

            foreach (var entry in entries)
            {
                if (config.ProjectRequired || entry.ProjectId is not null)
                {
                    if (entry.ProjectId is not Guid pid || pid == Guid.Empty)
                    {
                        errors.Add($"قسم «{sec.Label}»: يجب اختيار المشروع لكل عنصر.");
                        continue;
                    }
                    if (!vis.CanViewProject(pid))
                    {
                        errors.Add($"قسم «{sec.Label}»: مشروع خارج نطاق صلاحيتك.");
                        continue;
                    }
                    if (!seenProjects.Add(pid))
                    {
                        errors.Add($"قسم «{sec.Label}»: لا يمكن تكرار نفس المشروع أكثر من مرة في التقرير الواحد.");
                        continue;
                    }
                }

                foreach (var sf in config.Fields.Where(x => x.Required))
                {
                    var has = entry.Answers.TryGetValue(sf.Key, out var av) && AnswerHasValue(av);
                    if (!has)
                        errors.Add($"قسم «{sec.Label}»: الحقل «{sf.Label}» مطلوب لكل مشروع.");
                }
            }
        }
        return errors;
    }

    // ===== تحقّق خلايا جدول «مبيعات B2C حسب الدورة» (ERDS Phase 2A) =====
    // نطاق آمن مقصور: يُطبَّق حصرًا على الجداول (TableGrid) التي تطابق أعمدتها شيما B2cByCourseReportSchema بالترتيب،
    // فلا يمسّ أيّ TableGrid آخر في النظام (كلها ذات أعمدة مختلفة). القيم مخزَّنة string[][] في ValueJson.
    // يُفرَض خادميًّا عند الإرسال (SubmitAsync) — نفس بوابة تحقّق الحقول المطلوبة — فيمنع البيانات غير المنطقية حتى لو أُرسِلت من API مباشرة.
    private sealed record GridFieldInfo(Guid Id, string Label, string[] Columns);

    private async Task<List<GridFieldInfo>> GetVersionTableGridsAsync(Guid versionId, CancellationToken ct)
    {
        var raw = await _db.TemplateFields
            .Where(f => f.ReportTemplateVersionId == versionId && f.FieldType == FieldType.TableGrid)
            .Select(f => new { f.Id, f.Label, f.ConfigJson })
            .ToListAsync(ct);
        return raw.Select(f => new GridFieldInfo(f.Id, f.Label, ParseGridColumns(f.ConfigJson))).ToList();
    }

    private static List<string> ValidateB2cByCourseGrids(ReportSubmission submission, List<GridFieldInfo> grids)
    {
        var errors = new List<string>();
        foreach (var grid in grids)
        {
            // مطابقة الأعمدة الكاملة بالترتيب هي شرط التفعيل — يضمن عدم تأثّر أيّ جدول آخر.
            if (!grid.Columns.SequenceEqual(B2cByCourseReportSchema.Columns)) continue;

            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == grid.Id);
            var rows = ParseGridRows(value?.ValueJson);

            int Col(string name) => Array.IndexOf(B2cByCourseReportSchema.Columns, name);
            var cWorkHours = Col(B2cByCourseReportSchema.ColWorkHours);
            var cLeads = Col(B2cByCourseReportSchema.ColLeads);
            var cContacted = Col(B2cByCourseReportSchema.ColContacted);
            var cQualified = Col(B2cByCourseReportSchema.ColQualified);
            var cFollowUps = Col(B2cByCourseReportSchema.ColFollowUps);
            var cSales = Col(B2cByCourseReportSchema.ColSales);
            var cRevenue = Col(B2cByCourseReportSchema.ColRevenue);
            var cLost = Col(B2cByCourseReportSchema.ColLost);

            var numericColumns = new[]
            {
                (Index: cWorkHours, Name: B2cByCourseReportSchema.ColWorkHours),
                (Index: cLeads, Name: B2cByCourseReportSchema.ColLeads),
                (Index: cContacted, Name: B2cByCourseReportSchema.ColContacted),
                (Index: cQualified, Name: B2cByCourseReportSchema.ColQualified),
                (Index: cFollowUps, Name: B2cByCourseReportSchema.ColFollowUps),
                (Index: cSales, Name: B2cByCourseReportSchema.ColSales),
                (Index: cRevenue, Name: B2cByCourseReportSchema.ColRevenue),
                (Index: cLost, Name: B2cByCourseReportSchema.ColLost),
            };

            // قاعدة عامة: الجدول المطلوب لا يكون فارغًا، ولا تُقبَل صفوف فارغة بالكامل كبيانات (لا بدّ من صفّ واحد ببيانات فعلية).
            if (!rows.Any(RowHasAnyValue))
            {
                errors.Add($"جدول «{grid.Label}» يجب أن يحتوي على صفّ واحد على الأقل ببيانات فعلية.");
                continue;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!RowHasAnyValue(row)) continue; // صفّ فارغ بالكامل = غير مُدخَل، يُتجاهَل.
                var rowNum = i + 1;

                // (1) الأعمدة الرقمية: رقم صحيح/عشري صالح وغير سالب.
                var parsed = new Dictionary<int, decimal>();
                var rowValid = true;
                foreach (var (idx, name) in numericColumns)
                {
                    var raw = Cell(row, idx);
                    if (string.IsNullOrWhiteSpace(raw)) continue; // خليّة فارغة = غير مُدخَلة.
                    if (!TryParseNumber(raw, out var num))
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: قيمة «{name}» يجب أن تكون رقمًا.");
                        rowValid = false;
                        continue;
                    }
                    if (num < 0)
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{name}» لا يمكن أن يكون سالبًا.");
                        rowValid = false;
                        continue;
                    }
                    parsed[idx] = num;
                }
                if (!rowValid) continue; // لا تُطبَّق القواعد المنطقية على صفّ به خطأ رقمي.

                decimal? Val(int idx) => parsed.TryGetValue(idx, out var v) ? v : (decimal?)null;

                // (2) ساعات العمل: إن كان الصفّ يحوي نشاطًا (أيّ مقياس > 0) فيجب أن تكون ساعات العمل > 0.
                var hasActivity = new[] { cLeads, cContacted, cQualified, cFollowUps, cSales, cRevenue, cLost }
                    .Any(idx => Val(idx) is decimal dv && dv > 0);
                if (hasActivity && !(Val(cWorkHours) is decimal wh && wh > 0))
                    errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: يجب إدخال ساعات عمل أكبر من صفر لصفّ يحتوي على نشاط.");

                // (3) قواعد منطقية (تُطبَّق فقط حين تتوفّر القيمتان رقميًّا).
                void LeMax(int lo, string loName, int hi, string hiName)
                {
                    if (Val(lo) is decimal a && Val(hi) is decimal b && a > b)
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{loName}» لا يمكن أن يكون أكبر من «{hiName}».");
                }
                LeMax(cContacted, B2cByCourseReportSchema.ColContacted, cLeads, B2cByCourseReportSchema.ColLeads);
                LeMax(cQualified, B2cByCourseReportSchema.ColQualified, cContacted, B2cByCourseReportSchema.ColContacted);
                LeMax(cSales, B2cByCourseReportSchema.ColSales, cQualified, B2cByCourseReportSchema.ColQualified);
                LeMax(cLost, B2cByCourseReportSchema.ColLost, cLeads, B2cByCourseReportSchema.ColLeads);
            }
        }
        return errors;
    }

    // ===== تحقّق جدولَي قالب «مبيعات B2C — بيانات جديدة/قديمة» (Phase 7) =====
    // نفس منطق جدول B2C القديم لكن على جدولين: New Leads (عمود Leads = New Leads، التأهيل = Qualified)،
    // و Old CRM (عمود Leads = Old Leads Worked، التأهيل = Requalified). مطابقة الأعمدة بالترتيب شرط التفعيل
    // فلا يمسّ أيّ TableGrid آخر (ومن ضمنه قالب B2C القديم ذو الأعمدة المختلفة).
    private static List<string> ValidateB2cNewOldGrids(ReportSubmission submission, List<GridFieldInfo> grids)
    {
        var errors = new List<string>();
        foreach (var grid in grids)
        {
            string[] cols;
            string leadsName, qualifiedName;
            if (grid.Columns.SequenceEqual(B2cNewOldReportSchema.NewLeadsColumns))
            {
                cols = B2cNewOldReportSchema.NewLeadsColumns;
                leadsName = B2cNewOldReportSchema.ColNewLeads;
                qualifiedName = B2cNewOldReportSchema.ColQualified;
            }
            else if (grid.Columns.SequenceEqual(B2cNewOldReportSchema.OldCrmColumns))
            {
                cols = B2cNewOldReportSchema.OldCrmColumns;
                leadsName = B2cNewOldReportSchema.ColOldLeadsWorked;
                qualifiedName = B2cNewOldReportSchema.ColRequalified;
            }
            else continue;

            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == grid.Id);
            var rows = ParseGridRows(value?.ValueJson);

            int Col(string name) => Array.IndexOf(cols, name);
            var cWorkHours = Col(B2cNewOldReportSchema.ColWorkHours);
            var cLeads = Col(leadsName);
            var cContacted = Col(B2cNewOldReportSchema.ColContacted);
            var cQualified = Col(qualifiedName);
            var cFollowUps = Col(B2cNewOldReportSchema.ColFollowUps);
            var cSales = Col(B2cNewOldReportSchema.ColSales);
            var cRevenue = Col(B2cNewOldReportSchema.ColRevenue);
            var cLost = Col(B2cNewOldReportSchema.ColLost);

            var numericColumns = new[]
            {
                (Index: cWorkHours, Name: B2cNewOldReportSchema.ColWorkHours),
                (Index: cLeads, Name: leadsName),
                (Index: cContacted, Name: B2cNewOldReportSchema.ColContacted),
                (Index: cQualified, Name: qualifiedName),
                (Index: cFollowUps, Name: B2cNewOldReportSchema.ColFollowUps),
                (Index: cSales, Name: B2cNewOldReportSchema.ColSales),
                (Index: cRevenue, Name: B2cNewOldReportSchema.ColRevenue),
                (Index: cLost, Name: B2cNewOldReportSchema.ColLost),
            };

            if (!rows.Any(RowHasAnyValue))
            {
                errors.Add($"جدول «{grid.Label}» يجب أن يحتوي على صفّ واحد على الأقل ببيانات فعلية.");
                continue;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!RowHasAnyValue(row)) continue;
                var rowNum = i + 1;

                var parsed = new Dictionary<int, decimal>();
                var rowValid = true;
                foreach (var (idx, name) in numericColumns)
                {
                    var raw = Cell(row, idx);
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    if (!TryParseNumber(raw, out var num))
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: قيمة «{name}» يجب أن تكون رقمًا.");
                        rowValid = false;
                        continue;
                    }
                    if (num < 0)
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{name}» لا يمكن أن يكون سالبًا.");
                        rowValid = false;
                        continue;
                    }
                    parsed[idx] = num;
                }
                if (!rowValid) continue;

                decimal? Val(int idx) => parsed.TryGetValue(idx, out var v) ? v : (decimal?)null;

                var hasActivity = new[] { cLeads, cContacted, cQualified, cFollowUps, cSales, cRevenue, cLost }
                    .Any(idx => Val(idx) is decimal dv && dv > 0);
                if (hasActivity && !(Val(cWorkHours) is decimal wh && wh > 0))
                    errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: يجب إدخال ساعات عمل أكبر من صفر لصفّ يحتوي على نشاط.");

                void LeMax(int lo, string loName, int hi, string hiName)
                {
                    if (Val(lo) is decimal a && Val(hi) is decimal b && a > b)
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{loName}» لا يمكن أن يكون أكبر من «{hiName}».");
                }
                LeMax(cContacted, B2cNewOldReportSchema.ColContacted, cLeads, leadsName);
                LeMax(cQualified, qualifiedName, cContacted, B2cNewOldReportSchema.ColContacted);
                LeMax(cSales, B2cNewOldReportSchema.ColSales, cQualified, qualifiedName);
                LeMax(cLost, B2cNewOldReportSchema.ColLost, cLeads, leadsName);
            }
        }
        return errors;
    }

    // ===== تحقّق جدول قالب «مبيعات B2B حسب الخدمة» (RC-3 Task 2) =====
    // مطابقة الأعمدة بالترتيب شرط التفعيل، فلا يمسّ أيّ TableGrid آخر. عمود «الخدمة» و«Next Step» نصّيان (لا تحقّق رقمي).
    // القواعد الرقمية: كل مقياس غير سالب؛ Meetings ≤ Leads، Proposals ≤ Meetings، Won ≤ Proposals، Lost ≤ Leads.
    private static List<string> ValidateB2bByServiceGrids(ReportSubmission submission, List<GridFieldInfo> grids)
    {
        var errors = new List<string>();
        foreach (var grid in grids)
        {
            if (!grid.Columns.SequenceEqual(B2bByServiceReportSchema.Columns)) continue;

            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == grid.Id);
            var rows = ParseGridRows(value?.ValueJson);

            int Col(string name) => Array.IndexOf(B2bByServiceReportSchema.Columns, name);
            var cWorkHours = Col(B2bByServiceReportSchema.ColWorkHours);
            var cLeads = Col(B2bByServiceReportSchema.ColLeads);
            var cMeetings = Col(B2bByServiceReportSchema.ColMeetings);
            var cProposals = Col(B2bByServiceReportSchema.ColProposals);
            var cNegotiation = Col(B2bByServiceReportSchema.ColNegotiation);
            var cWon = Col(B2bByServiceReportSchema.ColWon);
            var cLost = Col(B2bByServiceReportSchema.ColLost);
            var cRevenue = Col(B2bByServiceReportSchema.ColRevenue);

            var numericColumns = new[]
            {
                (Index: cWorkHours, Name: B2bByServiceReportSchema.ColWorkHours),
                (Index: cLeads, Name: B2bByServiceReportSchema.ColLeads),
                (Index: cMeetings, Name: B2bByServiceReportSchema.ColMeetings),
                (Index: cProposals, Name: B2bByServiceReportSchema.ColProposals),
                (Index: cNegotiation, Name: B2bByServiceReportSchema.ColNegotiation),
                (Index: cWon, Name: B2bByServiceReportSchema.ColWon),
                (Index: cLost, Name: B2bByServiceReportSchema.ColLost),
                (Index: cRevenue, Name: B2bByServiceReportSchema.ColRevenue),
            };

            if (!rows.Any(RowHasAnyValue))
            {
                errors.Add($"جدول «{grid.Label}» يجب أن يحتوي على صفّ واحد على الأقل ببيانات فعلية.");
                continue;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!RowHasAnyValue(row)) continue;
                var rowNum = i + 1;

                var parsed = new Dictionary<int, decimal>();
                var rowValid = true;
                foreach (var (idx, name) in numericColumns)
                {
                    var raw = Cell(row, idx);
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    if (!TryParseNumber(raw, out var num))
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: قيمة «{name}» يجب أن تكون رقمًا.");
                        rowValid = false;
                        continue;
                    }
                    if (num < 0)
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{name}» لا يمكن أن يكون سالبًا.");
                        rowValid = false;
                        continue;
                    }
                    parsed[idx] = num;
                }
                if (!rowValid) continue;

                decimal? Val(int idx) => parsed.TryGetValue(idx, out var v) ? v : (decimal?)null;

                var hasActivity = new[] { cLeads, cMeetings, cProposals, cNegotiation, cWon, cLost, cRevenue }
                    .Any(idx => Val(idx) is decimal dv && dv > 0);
                if (hasActivity && !(Val(cWorkHours) is decimal wh && wh > 0))
                    errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: يجب إدخال ساعات عمل أكبر من صفر لصفّ يحتوي على نشاط.");

                void LeMax(int lo, string loName, int hi, string hiName)
                {
                    if (Val(lo) is decimal a && Val(hi) is decimal b && a > b)
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{loName}» لا يمكن أن يكون أكبر من «{hiName}».");
                }
                LeMax(cMeetings, B2bByServiceReportSchema.ColMeetings, cLeads, B2bByServiceReportSchema.ColLeads);
                LeMax(cProposals, B2bByServiceReportSchema.ColProposals, cMeetings, B2bByServiceReportSchema.ColMeetings);
                LeMax(cWon, B2bByServiceReportSchema.ColWon, cProposals, B2bByServiceReportSchema.ColProposals);
                LeMax(cLost, B2bByServiceReportSchema.ColLost, cLeads, B2bByServiceReportSchema.ColLeads);
            }
        }
        return errors;
    }

    // ===== تحقّق جدولَي قالب «مبيعات B2B — حسب مصدر البيانات» (RC-3 Task 2A — فصل المصدر) =====
    // جدولان: New Leads (عمود Leads = New Leads) و Data Scraping (Scraped Leads + Valid Leads).
    // مطابقة الأعمدة بالترتيب شرط التفعيل، فلا يمسّ أيّ TableGrid آخر (ومن ضمنه B2B حسب الخدمة وB2C). عمود «الخدمة» نصّي.
    // قواعد New Leads: كل مقياس غير سالب؛ Contacted ≤ New Leads، Meetings ≤ Contacted، Proposals ≤ Meetings، Won ≤ Proposals.
    // قواعد Data Scraping: كل مقياس غير سالب؛ Valid Leads ≤ Scraped Leads، Contacted ≤ Valid Leads، Meetings ≤ Contacted، Proposals ≤ Meetings، Won ≤ Proposals.
    private static List<string> ValidateB2bBySourceGrids(ReportSubmission submission, List<GridFieldInfo> grids)
    {
        var errors = new List<string>();
        foreach (var grid in grids)
        {
            bool isNewLeads;
            string[] cols;
            if (grid.Columns.SequenceEqual(B2bBySourceReportSchema.NewLeadsColumns))
            {
                isNewLeads = true;
                cols = B2bBySourceReportSchema.NewLeadsColumns;
            }
            else if (grid.Columns.SequenceEqual(B2bBySourceReportSchema.DataScrapingColumns))
            {
                isNewLeads = false;
                cols = B2bBySourceReportSchema.DataScrapingColumns;
            }
            else continue;

            var value = submission.FieldValues.FirstOrDefault(v => v.TemplateFieldId == grid.Id);
            var rows = ParseGridRows(value?.ValueJson);

            int Col(string name) => Array.IndexOf(cols, name);
            var cWorkHours = Col(B2bBySourceReportSchema.ColWorkHours);
            var cContacted = Col(B2bBySourceReportSchema.ColContacted);
            var cMeetings = Col(B2bBySourceReportSchema.ColMeetings);
            var cProposals = Col(B2bBySourceReportSchema.ColProposals);
            var cNegotiation = Col(B2bBySourceReportSchema.ColNegotiation);
            var cWon = Col(B2bBySourceReportSchema.ColWon);
            var cRevenue = Col(B2bBySourceReportSchema.ColRevenue);
            // خاصّان بكل جدول:
            var cNewLeads = isNewLeads ? Col(B2bBySourceReportSchema.ColNewLeads) : -1;
            var cScraped = isNewLeads ? -1 : Col(B2bBySourceReportSchema.ColScrapedLeads);
            var cValid = isNewLeads ? -1 : Col(B2bBySourceReportSchema.ColValidLeads);

            var numericColumns = new List<(int Index, string Name)>
            {
                (cWorkHours, B2bBySourceReportSchema.ColWorkHours),
                (cContacted, B2bBySourceReportSchema.ColContacted),
                (cMeetings, B2bBySourceReportSchema.ColMeetings),
                (cProposals, B2bBySourceReportSchema.ColProposals),
                (cNegotiation, B2bBySourceReportSchema.ColNegotiation),
                (cWon, B2bBySourceReportSchema.ColWon),
                (cRevenue, B2bBySourceReportSchema.ColRevenue),
            };
            if (isNewLeads)
                numericColumns.Add((cNewLeads, B2bBySourceReportSchema.ColNewLeads));
            else
            {
                numericColumns.Add((cScraped, B2bBySourceReportSchema.ColScrapedLeads));
                numericColumns.Add((cValid, B2bBySourceReportSchema.ColValidLeads));
            }

            // الجدولان مستقلّان اختياريان: جدول فارغ تمامًا (بلا صفّ ببيانات فعلية) يُتخطّى بلا خطأ
            // كي يستطيع المندوب إرسال مصدر واحد فقط (New Leads فقط أو Data Scraping فقط) أو كليهما.
            if (!rows.Any(RowHasAnyValue)) continue;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!RowHasAnyValue(row)) continue;
                var rowNum = i + 1;

                var parsed = new Dictionary<int, decimal>();
                var rowValid = true;
                foreach (var (idx, name) in numericColumns)
                {
                    var raw = Cell(row, idx);
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    if (!TryParseNumber(raw, out var num))
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: قيمة «{name}» يجب أن تكون رقمًا.");
                        rowValid = false;
                        continue;
                    }
                    if (num < 0)
                    {
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{name}» لا يمكن أن يكون سالبًا.");
                        rowValid = false;
                        continue;
                    }
                    parsed[idx] = num;
                }
                if (!rowValid) continue;

                decimal? Val(int idx) => parsed.TryGetValue(idx, out var v) ? v : (decimal?)null;

                var activityColumns = isNewLeads
                    ? new[] { cNewLeads, cContacted, cMeetings, cProposals, cNegotiation, cWon, cRevenue }
                    : new[] { cScraped, cValid, cContacted, cMeetings, cProposals, cNegotiation, cWon, cRevenue };
                var hasActivity = activityColumns.Any(idx => Val(idx) is decimal dv && dv > 0);
                if (hasActivity && !(Val(cWorkHours) is decimal wh && wh > 0))
                    errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: يجب إدخال ساعات عمل أكبر من صفر لصفّ يحتوي على نشاط.");

                void LeMax(int lo, string loName, int hi, string hiName)
                {
                    if (Val(lo) is decimal a && Val(hi) is decimal b && a > b)
                        errors.Add($"الصف {rowNum} في جدول «{grid.Label}»: «{loName}» لا يمكن أن يكون أكبر من «{hiName}».");
                }

                if (isNewLeads)
                {
                    LeMax(cContacted, B2bBySourceReportSchema.ColContacted, cNewLeads, B2bBySourceReportSchema.ColNewLeads);
                }
                else
                {
                    LeMax(cValid, B2bBySourceReportSchema.ColValidLeads, cScraped, B2bBySourceReportSchema.ColScrapedLeads);
                    LeMax(cContacted, B2bBySourceReportSchema.ColContacted, cValid, B2bBySourceReportSchema.ColValidLeads);
                }
                LeMax(cMeetings, B2bBySourceReportSchema.ColMeetings, cContacted, B2bBySourceReportSchema.ColContacted);
                LeMax(cProposals, B2bBySourceReportSchema.ColProposals, cMeetings, B2bBySourceReportSchema.ColMeetings);
                LeMax(cWon, B2bBySourceReportSchema.ColWon, cProposals, B2bBySourceReportSchema.ColProposals);
            }
        }
        return errors;
    }

    private static string Cell(IReadOnlyList<string> row, int idx)
        => idx >= 0 && idx < row.Count ? (row[idx] ?? string.Empty) : string.Empty;

    private static bool RowHasAnyValue(IReadOnlyList<string> row)
        => row.Any(c => !string.IsNullOrWhiteSpace(c));

    private static string[] ParseGridColumns(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
                return cols.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        }
        catch { /* ConfigJson غير صالح ⇒ لا أعمدة ⇒ لا تفعيل للتحقّق. */ }
        return Array.Empty<string>();
    }

    private static List<string[]> ParseGridRows(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) return new List<string[]>();
        try { return JsonSerializer.Deserialize<List<string[]>>(valueJson, RepeatableJson) ?? new List<string[]>(); }
        catch { return new List<string[]>(); }
    }

    // يمرّ عبر الأداة المركزية الموحّدة (RC-3 Task 2B): تطبيع الخانات العربية/الفارسية ثم التحويل.
    private static bool TryParseNumber(string raw, out decimal value)
        => NumericNormalizer.TryParseDecimal(raw, out value);
}

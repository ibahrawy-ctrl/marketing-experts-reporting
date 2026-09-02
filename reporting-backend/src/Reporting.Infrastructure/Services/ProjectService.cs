using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ProjectService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    // ======================================================================
    // عقد الرفض الموحّد (P360-WF-R2 · GAP-24 · مكافحة التعداد — FINDING-W6-04)
    // ======================================================================

    /// <summary>
    /// **الرسالة العامّة الوحيدة** لحالتَي «غير موجود» و«موجود خارج النطاق». ثابت واحد كي لا
    /// ينحرف نصّ إحداهما عن الأخرى بتعديل لاحق فيعود التمييز من حيث لا يُقصَد.
    /// </summary>
    internal const string ProjectNotFoundMessage = "المشروع غير موجود.";
    internal const string ProjectNotFoundCode = "project.not_found";

    /// <summary>
    /// **الرفض المميَّز الوحيد المسموح (403)**: من يرى المشروع فعلًا ولا يملك القدرة المطلوبة عليه.
    /// لا يكشف وجود شيء لا يعرفه المستخدم أصلًا، فلا يخدم التعداد.
    /// </summary>
    private const string StructuralForbiddenMessage = "لا تملك صلاحية التعديل البنيويّ على هذا المشروع.";

    /// <summary>
    /// حارس القدرة البنيويّة داخل الخدمة — **لا يُعتمد على سمة المتحكّم وحدها** (دفاع بالعمق).
    /// قائد الفريق ومدير الحساب لا يملكانها بالدور؛ صلاحيّتهما تشغيليّة بالمورد.
    /// </summary>
    private bool HasStructuralCapability() => _currentUser.IsInAnyRole(Roles.ProjectStructuralManagers);

    public async Task<Result<IReadOnlyList<ProjectDto>>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ProjectDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);

        var q = _db.Projects.AsNoTracking().AsQueryable();
        if (!vis.SeesAll) q = q.Where(p => vis.ProjectIds.Contains(p.Id));
        if (filter.ClientId is not null) q = q.Where(p => p.ClientId == filter.ClientId);
        if (filter.Status is not null) q = q.Where(p => p.Status == filter.Status);
        if (filter.ServiceType is not null) q = q.Where(p => p.ServiceType == filter.ServiceType);
        if (filter.OwnerTeamId is not null) q = q.Where(p => p.OwnerTeamId == filter.OwnerTeamId);
        if (filter.AccountManagerId is not null) q = q.Where(p => p.AccountManagerId == filter.AccountManagerId);
        if (!filter.IncludeClosed) q = q.Where(p => p.Status != ProjectStatus.Closed);
        // قائمة الاختيار (dropdown): مشاريع نشطة فقط تابعة لعميل غير مؤرشف، ضمن النطاق المُطبَّق أعلاه.
        if (filter.SelectableOnly)
        {
            q = q.Where(p => p.Status == ProjectStatus.Active);
            q = q.Where(p => _db.Clients.Any(c => c.Id == p.ClientId && c.Status != ClientStatus.Closed));
        }

        var rows = await q.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var dtos = await MapManyAsync(rows, ct);
        return Result<IReadOnlyList<ProjectDto>>.Success(dtos);
    }

    public async Task<Result<ProjectDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        // الترتيب مقصود: الرؤية أوّلًا ثمّ الوجود — فحالتا «غير موجود» و«خارج النطاق» تعودان
        // بعقد واحد لا يُميَّز (نفس الرمز والرسالة) منعًا لتعداد معرّفات المشاريع.
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var p = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { p }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ProjectDto>.Failure("اسم المشروع مطلوب.", "project.name_required");

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct);
        if (client is null) return Result<ProjectDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        // غير ذوي الرؤية الكاملة: لا يُنشئون مشروعًا إلا داخل نطاقهم (عميل مرئي
        // أو يضعون أنفسهم مديري حساب أو فريقهم هو المسؤول).
        if (!vis.SeesAll && !await CanOwnAsync(vis, request.AccountManagerId, request.OwnerTeamId, request.ClientId, uid, ct))
            return Result<ProjectDto>.Failure("لا يمكنك إنشاء مشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var project = new Project
        {
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            ServiceType = request.ServiceType,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            OwnerTeamId = request.OwnerTeamId,
            AccountManagerId = request.AccountManagerId,
            Notes = request.Notes,
            ProjectOwnerId = request.ProjectOwnerId,
            TeamLeaderId = request.TeamLeaderId
        };

        // GAP-01: أوّل مسار كتابة على الإطلاق لهذين الحقلين. كانا يُقرآن في التخويل والعرض
        // ولا يُكتبان في أيّ مكان ⟹ صفر مشروع في الإنتاج يحمل أيّهما، فبقيت الصلاحيّة التشغيليّة
        // بالمورد **نظريّة**، ولجأ المستخدمون إلى `AccountManagerId` كالتفاف يوسّع الصلاحيّات.
        var roleError = await ValidateAssignedRolesAsync(request.ProjectOwnerId, request.TeamLeaderId, ct);
        if (roleError is not null) return Result<ProjectDto>.Failure(roleError.Value.Message, roleError.Value.Code);

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.created", nameof(Project), project.Id, ct: ct);
        if (project.ProjectOwnerId is not null || project.TeamLeaderId is not null)
            await _audit.LogAsync(uid, "project.roles_assigned", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ProjectDto>.Failure("اسم المشروع مطلوب.", "project.name_required");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        // القدرة البنيويّة: الإدارة (Admin/CEO/GM/Manager) أو مالك المشروع المسنَد. قائد الفريق
        // يراه ويحدّثه تشغيليًّا ولا يعدّل بنيته — هذا هو إصلاح انقلاب الصلاحيّات (GAP-06).
        if (!HasStructuralCapability() && project.ProjectOwnerId != uid)
            return Result<ProjectDto>.Failure(StructuralForbiddenMessage, "auth.forbidden");

        project.Name = request.Name.Trim();
        project.ServiceType = request.ServiceType;
        project.Status = request.Status;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        var roleError = await ValidateAssignedRolesAsync(request.ProjectOwnerId, request.TeamLeaderId, ct);
        if (roleError is not null) return Result<ProjectDto>.Failure(roleError.Value.Message, roleError.Value.Code);

        // تغيير الإسناد يُرصَد **قبل** الكتابة: إسناد دور تشغيليّ يمنح صلاحيّة كتابة على المشروع،
        // فيجب أن يترك أثرًا مستقلًّا في السجلّ لا أن يذوب داخل «project.updated».
        var rolesChanged = project.ProjectOwnerId != request.ProjectOwnerId
                        || project.TeamLeaderId != request.TeamLeaderId;

        project.OwnerTeamId = request.OwnerTeamId;
        project.AccountManagerId = request.AccountManagerId;
        project.Notes = request.Notes;
        project.ProjectOwnerId = request.ProjectOwnerId;
        project.TeamLeaderId = request.TeamLeaderId;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.updated", nameof(Project), project.Id, ct: ct);
        if (rolesChanged)
            await _audit.LogAsync(uid, "project.roles_assigned", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        if (!HasStructuralCapability() && project.ProjectOwnerId != uid)
            return Result<ProjectDto>.Failure("لا تملك صلاحية أرشفة هذا المشروع.", "auth.forbidden");

        project.Status = ProjectStatus.Closed;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.archived", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        if (!HasStructuralCapability() && project.ProjectOwnerId != uid)
            return Result<ProjectDto>.Failure("لا تملك صلاحية إعادة تفعيل هذا المشروع.", "auth.forbidden");

        if (project.Status != ProjectStatus.Closed)
            return Result<ProjectDto>.Failure("المشروع غير مؤرشف.", "project.not_archived.conflict");

        // لا يُعاد تفعيل مشروع تابع لعميل مؤرشف.
        var clientClosed = await _db.Clients.AnyAsync(c => c.Id == project.ClientId && c.Status == ClientStatus.Closed, ct);
        if (clientClosed)
            return Result<ProjectDto>.Failure("لا يمكن إعادة تفعيل مشروع تابع لعميل مؤرشف. أعِد تفعيل العميل أولًا.", "project.client_archived.conflict");

        project.Status = ProjectStatus.Active;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.reactivated", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        // الحذف النهائيّ **للإدارة حصرًا** — بلا استثناء لمالك المشروع (أضيق الخيارات، صفر توسّع).
        if (!HasStructuralCapability())
            return Result.Failure("لا تملك صلاحية حذف هذا المشروع.", "auth.forbidden");

        var reason = await DeleteBlockReasonAsync(project.Id, ct);
        if (reason is not null)
            return Result.Failure(reason, "project.delete_forbidden.conflict");

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.deleted", nameof(Project), id, ct: ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<LinkedReportRow>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<IReadOnlyList<LinkedReportRow>>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var exists = await _db.Projects.AnyAsync(p => p.Id == id, ct);
        if (!exists) return Result<IReadOnlyList<LinkedReportRow>>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var rows = await LinkedReportsAsync(LinkedToProject(id), id, ct);
        return Result<IReadOnlyList<LinkedReportRow>>.Success(rows);
    }

    /// <summary>
    /// شرط ارتباط التسليم بالمشروع (PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2 · ADR-R2-002).
    /// <para><b>لماذا شرطان لا شرط واحد</b>: العمود العلويّ <c>ReportSubmissions.ProjectId</c> شبه مهجور —
    /// مقيسًا على الإنتاج: مملوء في تسليمَين فقط من 311، بينما 261 إشارة مشروع تعيش داخل
    /// <c>ValueJson</c> لأقسام <c>ProjectRepeatableSection</c>. الاقتصار على العمود العلويّ كان يُخفي
    /// 74 تقريرًا من 76 عن قوائم تقارير مشاريعها.</para>
    /// <para><b>لماذا الاحتواء <c>@&gt;</c> لا البحث النصّيّ</b>: <c>ValueJson</c> عمود <c>jsonb</c>،
    /// فالاحتواء البنيويّ يطابق المفتاح والقيمة معًا ويستفيد من فهرس GIN، بينما <c>LIKE</c> على نصّ
    /// قد يطابق معرّفًا داخل حقل آخر ⇒ تسريب تقرير خارج نطاق المشروع.</para>
    /// <para><b>لماذا التصفية على نوع الحقل</b>: نفس علّة الشريحة — لا يجوز اعتبار ورود المعرّف في
    /// أيّ حقل حرّ ارتباطًا بالمشروع.</para>
    /// <para>الشرط مبنيّ بـ<c>Any</c> فرعيّ ⇒ صفّ واحد لكلّ تسليم مهما تعدّدت مواضع الارتباط (لا تكرار،
    /// ولا حاجة إلى <c>Distinct</c>)، والتصفية كلّها في قاعدة البيانات لا في الذاكرة.</para>
    /// </summary>
    private System.Linq.Expressions.Expression<Func<Domain.Entities.Submissions.ReportSubmission, bool>> LinkedToProject(Guid id)
    {
        var containment = ProjectContainmentJson(id);
        return s => s.ProjectId == id
            || _db.SubmissionFieldValues.Any(v =>
                    v.ReportSubmissionId == s.Id
                    && v.ValueJson != null
                    && _db.TemplateFields.Any(f => f.Id == v.TemplateFieldId
                                                   && f.FieldType == FieldType.ProjectRepeatableSection)
                    && EF.Functions.JsonContains(v.ValueJson!, containment));
    }

    // قالب الاحتواء: عنصر مصفوفة واحد يحمل projectId المطلوب. التنسيق مطابق حرفيًّا لما تكتبه الواجهة
    // (مفتاح camelCase ومعرّف بأحرف صغيرة) — وهو ما تحقّقنا منه على بيانات الإنتاج قبل اعتماد الشرط.
    private static string ProjectContainmentJson(Guid id) =>
        "[{\"projectId\":\"" + id.ToString("D") + "\"}]";

    public async Task<Result<ProjectSummaryDto>> GetSummaryAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ProjectSummaryDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectSummaryDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectSummaryDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        // نفس شرط الارتباط المستعمَل في قائمة التقارير — وإلّا صار العدّاد يناقض القائمة التي تحته.
        var subs = await _db.ReportSubmissions.AsNoTracking().Where(LinkedToProject(id))
            .Select(s => new { s.Status, s.SubmittedAtUtc }).ToListAsync(ct);
        var total = subs.Count;
        var closed = subs.Count(s => s.Status == SubmissionStatus.Closed);
        var pending = subs.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.ApprovedByDirectManager
            or SubmissionStatus.ApprovedByNextLevel or SubmissionStatus.Escalated);
        var last = subs.Where(s => s.SubmittedAtUtc != null).Max(s => (DateTime?)s.SubmittedAtUtc);

        var openRisks = await _db.Risks.CountAsync(r => r.ProjectId == id && r.Status != RiskStatus.Closed, ct);
        var openNotes = await _db.ManagementNotes.CountAsync(
            n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == id && n.Status == ManagementNoteStatus.Open, ct);

        var dto = (await MapManyAsync(new[] { project }, ct))[0];
        return Result<ProjectSummaryDto>.Success(new ProjectSummaryDto(dto, total, closed, pending, last, openRisks, openNotes));
    }

    /// <summary>
    /// شريحة المشروع من تسليم واحد (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1).
    /// <para><b>لماذا الرفض موحّد بـ<c>project.not_found</c> في ثلاث حالات مختلفة</b> (خارج النطاق /
    /// التسليم غير موجود / التسليم غير مرتبط بالمشروع): أيّ تمييز بينها يحوّل الواجهة إلى عدّاد
    /// وجود — يجرّب المهاجم معرّفات حتّى يفرّق «غير موجود» عن «موجود لكن ليس لك».</para>
    /// <para><b>لماذا يُصفّى على <c>ProjectRepeatableSection</c> وحده</b>: هو نوع الحقل الوحيد
    /// الذي يحمل <c>projectId</c> بنيويًّا في <c>ValueJson</c>. بقيّة الحقول (نصّ حرّ، ملخّص عامّ…)
    /// لا رابط موثوقًا لها بمشروع، وإخراجها يعني تسريب عمل مشروعات أخرى — ولا يجوز تخمين
    /// انتماء الفقرة من نصّها.</para>
    /// </summary>
    public async Task<Result<ProjectReportSliceDto>> GetReportSliceAsync(Guid id, Guid submissionId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ProjectReportSliceDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectReportSliceDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name, p.ClientId, ClientName = p.Client!.Name })
            .FirstOrDefaultAsync(ct);
        if (project is null) return Result<ProjectReportSliceDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var sub = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new
            {
                s.Id,
                s.SubmitterId,
                s.PeriodType,
                s.PeriodKey,
                s.Status,
                s.SubmittedAtUtc,
                s.ProjectId,
                TemplateTitle = _db.ReportTemplateVersions
                    .Where(v => v.Id == s.ReportTemplateVersionId)
                    .Select(v => v.ReportTemplate!.Title).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        if (sub is null) return Result<ProjectReportSliceDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var raw = await (from v in _db.SubmissionFieldValues.AsNoTracking()
                         join f in _db.TemplateFields.AsNoTracking() on v.TemplateFieldId equals f.Id
                         where v.ReportSubmissionId == submissionId
                            && f.FieldType == FieldType.ProjectRepeatableSection
                            && v.ValueJson != null
                         orderby f.Order
                         select new { f.Id, f.Label, f.ConfigJson, f.Order, v.ValueJson })
                        .ToListAsync(ct);

        var fields = new List<ProjectReportSliceFieldDto>();
        foreach (var r in raw)
        {
            var entries = ExtractProjectEntries(r.ValueJson!, id, TemplateDeclaresWorkItems(r.ConfigJson));
            if (entries.Count == 0) continue; // حقل بلا عنصر لهذا المشروع لا يُذكَر أصلًا
            fields.Add(new ProjectReportSliceFieldDto(r.Id, r.Label, r.ConfigJson, r.Order, entries));
        }

        // لا شريحة **ولا** ربط مباشر على مستوى التسليم ⇒ «غير مرتبط» = نفس رفض «غير موجود».
        if (fields.Count == 0 && sub.ProjectId != id)
            return Result<ProjectReportSliceDto>.Failure(ProjectNotFoundMessage, ProjectNotFoundCode);

        var names = await UserNamesAsync(new[] { sub.SubmitterId }, ct);
        return Result<ProjectReportSliceDto>.Success(new ProjectReportSliceDto(
            sub.Id, project.Id, project.Name, project.ClientId, project.ClientName,
            sub.SubmitterId, names.GetValueOrDefault(sub.SubmitterId), sub.TemplateTitle,
            sub.PeriodType, sub.PeriodKey, sub.Status, sub.SubmittedAtUtc, fields));
    }

    // ===== helpers =====

    /// <summary>
    /// يُبقي من مصفوفة القسم المتكرّر عناصر <b>هذا المشروع فقط</b>، ويسطّح <c>answers</c> إلى
    /// قاموس نصّيّ. <c>JsonException</c> تُبتلع عمدًا وتُعيد قائمة فارغة: JSON تالف يعني
    /// «لا أعرف لمن هذا العنصر» ⇒ الفشل المغلق (لا يخرج شيء) لا المفتوح.
    /// </summary>
    private static List<ProjectReportSliceEntryDto> ExtractProjectEntries(string valueJson, Guid projectId, bool templateHasWorkItems)
    {
        var result = new List<ProjectReportSliceEntryDto>();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(valueJson); }
        catch (JsonException) { return result; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                if (!el.TryGetProperty("projectId", out var pid) || pid.ValueKind != JsonValueKind.String) continue;
                if (!Guid.TryParse(pid.GetString(), out var g) || g != projectId) continue;

                var answers = FlattenAnswers(el);

                var items = new List<IReadOnlyDictionary<string, string?>>();
                if (el.TryGetProperty("workItems", out var wi) && wi.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in wi.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.Object)
                            items.Add(FlattenAnswers(item));
                }
                // محوِّل القراءة (§7): بيانات v1 داخل قالب أعلن مجموعة بنود عمل ⇒ بندٌ ضمنيّ واحد،
                // عرضًا فقط. لا كتابة، ولا تحويل، ولا مساس بما هو مخزَّن.
                else if (templateHasWorkItems && answers.Count > 0)
                {
                    items.Add(answers);
                }

                result.Add(new ProjectReportSliceEntryDto(answers, items));
            }
        }
        return result;
    }

    private static Dictionary<string, string?> FlattenAnswers(JsonElement container)
    {
        var answers = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!container.TryGetProperty("answers", out var ans) || ans.ValueKind != JsonValueKind.Object) return answers;
        foreach (var p in ans.EnumerateObject())
            answers[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => p.Value.GetString(),
                _ => p.Value.GetRawText()
            };
        return answers;
    }

    // هل أعلن القالب مجموعة بنود عمل؟ فحص بنيويّ خفيف على ConfigJson بلا نموذج كامل —
    // الشريحة لا تحتاج تفاصيل المجموعة، بل وجودها فقط لتقرير تفعيل محوِّل القراءة.
    private static bool TemplateDeclaresWorkItems(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("workItems", out var w)
                   && w.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>
    /// المُسنَد إليه يجب أن يكون **مستخدمًا قائمًا ونشطًا**. مرجع ميت أو موظّف مغادر يعني
    /// مشروعًا بلا مسؤول فعليّ مع بقاء الواجهة تعرض اسمًا — وهو أسوأ من غياب الإسناد.
    /// المراجع تبقى <c>Guid?</c> بلا مفتاح أجنبيّ صلب (نفس نمط <c>AccountManagerId</c>)،
    /// فالحارس هنا هو الضمانة الوحيدة.
    /// </summary>
    private async Task<(string Message, string Code)?> ValidateAssignedRolesAsync(Guid? projectOwnerId, Guid? teamLeaderId, CancellationToken ct)
    {
        var ids = new List<Guid>(2);
        if (projectOwnerId is Guid po) ids.Add(po);
        if (teamLeaderId is Guid tl) ids.Add(tl);
        if (ids.Count == 0) return null;

        var activeIds = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (projectOwnerId is Guid p && !activeIds.Contains(p))
            return ("مالك المشروع المحدَّد غير موجود أو غير نشط.", "project.owner_invalid");
        if (teamLeaderId is Guid t && !activeIds.Contains(t))
            return ("قائد الفريق المحدَّد غير موجود أو غير نشط.", "project.team_leader_invalid");

        return null;
    }

    private async Task<bool> CanOwnAsync(ClientProjectVisibility vis, Guid? amId, Guid? ownerTeamId, Guid clientId, Guid uid, CancellationToken ct)
    {
        if (vis.CanViewClient(clientId)) return true;
        if (amId == uid) return true;
        if (ownerTeamId is Guid t)
            return await _db.Teams.AnyAsync(x => x.Id == t && x.TeamLeaderId == uid, ct);
        return false;
    }

    /// <summary>
    /// صفوف التقارير المرتبطة بمشروع، مُثراة بما يلزم لاتّخاذ قرار من داخل مساحة المشروع (VIS-02ب):
    /// اسم القالب · آخر تحديث · عدد بنود العمل المرصودة **لهذا المشروع وحده** · آخر قرار اعتماد · سبب الإرجاع.
    /// كلّ إثراء يُجلَب باستعلام مُجمَّع واحد (لا N+1).
    /// </summary>
    private async Task<IReadOnlyList<LinkedReportRow>> LinkedReportsAsync(
        System.Linq.Expressions.Expression<Func<Domain.Entities.Submissions.ReportSubmission, bool>> predicate,
        Guid projectId, CancellationToken ct)
    {
        var subs = await _db.ReportSubmissions.AsNoTracking().Where(predicate)
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        if (subs.Count == 0) return new List<LinkedReportRow>();

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);
        var subIds = subs.Select(s => s.Id).ToList();
        var versionIds = subs.Select(s => s.ReportTemplateVersionId).Distinct().ToList();

        // اسم القالب من نَسَب الإصدار: بدونه يستحيل تمييز تقرير التصميم من تقرير السيو في القائمة.
        var templateNames = await _db.ReportTemplateVersions.AsNoTracking()
            .Where(v => versionIds.Contains(v.Id))
            .Join(_db.ReportTemplates.AsNoTracking(), v => v.ReportTemplateId, t => t.Id,
                  (v, t) => new { v.Id, t.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);

        // آخر خطوة اعتماد مقضيّة لكلّ تسليم (القرار وتاريخه)، وآخر خطوة إرجاع (السبب).
        var decided = await _db.ApprovalSteps.AsNoTracking()
            .Where(a => subIds.Contains(a.ReportSubmissionId) && a.DecidedAtUtc != null)
            .Select(a => new { a.ReportSubmissionId, a.Status, a.DecidedAtUtc, a.Comment })
            .ToListAsync(ct);
        var lastDecision = decided
            .GroupBy(a => a.ReportSubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DecidedAtUtc).First());
        var lastReturn = decided
            .Where(a => a.Status == ApprovalStatus.Returned)
            .GroupBy(a => a.ReportSubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DecidedAtUtc).First().Comment);

        // عدد بنود العمل المرصودة لهذا المشروع داخل كلّ تسليم — يُحسب من أقسام
        // ProjectRepeatableSection وحدها كي لا يُحتسب ورود المعرّف في حقل حرّ.
        var sectionJson = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId) && v.ValueJson != null
                        && _db.TemplateFields.Any(f => f.Id == v.TemplateFieldId
                                                       && f.FieldType == FieldType.ProjectRepeatableSection))
            .Select(v => new { v.ReportSubmissionId, v.ValueJson })
            .ToListAsync(ct);
        var workItemCounts = new Dictionary<Guid, int>();
        foreach (var row in sectionJson)
            workItemCounts[row.ReportSubmissionId] =
                workItemCounts.GetValueOrDefault(row.ReportSubmissionId)
                + CountWorkItemsForProject(row.ValueJson!, projectId);

        return subs.Select(s => new LinkedReportRow(
            s.Id, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId),
            s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClientId, s.ProjectId,
            TemplateName: templateNames.GetValueOrDefault(s.ReportTemplateVersionId),
            LastUpdatedAtUtc: s.UpdatedAtUtc,
            WorkItemCount: workItemCounts.GetValueOrDefault(s.Id),
            LastDecision: lastDecision.TryGetValue(s.Id, out var d) ? d.Status : null,
            LastDecisionAtUtc: lastDecision.TryGetValue(s.Id, out var d2) ? d2.DecidedAtUtc : null,
            LastReturnReason: lastReturn.GetValueOrDefault(s.Id))).ToList();
    }

    /// <summary>
    /// عدد بنود العمل داخل بطاقات هذا المشروع وحده. بطاقة بلا مصفوفة بنود = بندٌ واحد ضمنيّ
    /// (القوالب التي لا تُفعّل بنود العمل تحمل مجموعة إجابات واحدة لكلّ مشروع).
    /// </summary>
    private static int CountWorkItemsForProject(string valueJson, Guid projectId)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<List<SectionEntryWithItems>>(valueJson, SectionJson);
            if (entries is null) return 0;
            var total = 0;
            foreach (var e in entries)
            {
                if (e.ProjectId != projectId) continue;
                total += e.WorkItems is { Count: > 0 } ? e.WorkItems.Count : 1;
            }
            return total;
        }
        catch { return 0; }
    }

    private sealed class SectionEntryWithItems
    {
        public Guid? ProjectId { get; set; }
        public List<JsonElement>? WorkItems { get; set; }
    }

    private async Task<List<ProjectDto>> MapManyAsync(IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        var ids = projects.Select(p => p.Id).ToList();
        var clientIds = projects.Select(p => p.ClientId).Distinct().ToList();
        var clientNames = await _db.Clients.Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var teamIds = projects.Where(p => p.OwnerTeamId != null).Select(p => p.OwnerTeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        // اسم واحد لكلّ الأدوار الثلاثة في استعلام واحد — لا استعلام لكلّ دور ولا N+1.
        var personIds = projects.SelectMany(p => new[] { p.AccountManagerId, p.ProjectOwnerId, p.TeamLeaderId })
            .Where(x => x != null).Select(x => x!.Value);
        var personNames = await UserNamesAsync(personIds, ct);

        // **خريطة القدرات** (§12 · GAP-16): الخادم يُعلن ما يسمح به فتُخفي الواجهة ما يرفضه،
        // بدل أن يكتشفه المستخدم بعد الحفظ. مشتقّة من **نفس** حارسَي الخدمة أعلاه حرفيًّا —
        // خريطة تُحسَب بقاعدة ثانية هي وعدٌ كاذب أخطر من غيابها.
        var structural = HasStructuralCapability();
        var actorId = _currentUser.UserId;

        // عدّادات الارتباط لاحتساب canHardDelete دفعةً واحدة.
        var directReports = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ReportSubmissions.Where(s => s.ProjectId != null && ids.Contains(s.ProjectId.Value))
                .GroupBy(s => s.ProjectId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var risksByProject = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.Risks.Where(r => r.ProjectId != null && ids.Contains(r.ProjectId.Value))
                .GroupBy(r => r.ProjectId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var notesByProject = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ManagementNotes.Where(n => n.EntityType == ManagementNoteEntityType.Project && ids.Contains(n.EntityId))
                .GroupBy(n => n.EntityId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        // الاستخدام داخل أقسام المشاريع المتكررة (Multi-Project ValueJson).
        var inSections = await ReferencedProjectIdsInSectionsAsync(ct);

        return projects.Select(p =>
        {
            var (canHardDelete, reason) = BuildDeleteGuard(
                directReports.GetValueOrDefault(p.Id), inSections.Contains(p.Id),
                risksByProject.GetValueOrDefault(p.Id), notesByProject.GetValueOrDefault(p.Id));
            var canManageStructure = structural || (actorId is Guid a1 && p.ProjectOwnerId == a1);
            var canOperate = canManageStructure
                || (actorId is Guid a2 && (p.TeamLeaderId == a2 || p.AccountManagerId == a2));

            return new ProjectDto(
                p.Id, p.ClientId, clientNames.GetValueOrDefault(p.ClientId), p.Name, p.ServiceType, p.Status,
                p.StartDate, p.EndDate, p.OwnerTeamId, p.OwnerTeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                p.AccountManagerId, p.AccountManagerId is Guid aid ? personNames.GetValueOrDefault(aid) : null,
                p.Notes, p.CreatedAtUtc, p.UpdatedAtUtc, canHardDelete, reason,
                p.ProjectOwnerId, p.ProjectOwnerId is Guid poid ? personNames.GetValueOrDefault(poid) : null,
                p.TeamLeaderId, p.TeamLeaderId is Guid tlid ? personNames.GetValueOrDefault(tlid) : null,
                p.ProgressPercent, p.ProgressMode, p.ProgressCalculatedAtUtc, p.ProgressSourceDeliverableCount,
                p.HealthStatus, p.HealthPercent, p.HealthComputedAtUtc,
                canManageStructure, canOperate);
        }).ToList();
    }

    // ===== فحص الاستخدام / منع الحذف النهائي =====
    private async Task<string?> DeleteBlockReasonAsync(Guid projectId, CancellationToken ct)
    {
        var reports = await _db.ReportSubmissions.CountAsync(s => s.ProjectId == projectId, ct);
        var risks = await _db.Risks.CountAsync(r => r.ProjectId == projectId, ct);
        var notes = await _db.ManagementNotes.CountAsync(
            n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == projectId, ct);
        var inSection = (await ReferencedProjectIdsInSectionsAsync(ct)).Contains(projectId);
        return BuildDeleteGuard(reports, inSection, risks, notes).reason;
    }

    private static (bool canHardDelete, string? reason) BuildDeleteGuard(int reports, bool inSection, int risks, int notes)
    {
        var parts = new List<string>();
        if (reports > 0) parts.Add($"{reports} تقريرًا مباشرًا");
        if (inSection) parts.Add("مستخدم داخل أقسام تقارير متعدّدة المشاريع");
        if (risks > 0) parts.Add($"{risks} مخاطرة");
        if (notes > 0) parts.Add($"{notes} ملاحظة إدارية");
        if (parts.Count == 0) return (true, null);
        return (false, "لا يمكن الحذف النهائي — " + string.Join("، ", parts) + ". استخدم الأرشفة بدلًا من ذلك.");
    }

    private static readonly JsonSerializerOptions SectionJson = new() { PropertyNameCaseInsensitive = true };

    private sealed class SectionEntry
    {
        public Guid? ProjectId { get; set; }
    }

    // يجمع كل معرّفات المشاريع المُشار إليها داخل ValueJson لأقسام ProjectRepeatableSection.
    private async Task<HashSet<Guid>> ReferencedProjectIdsInSectionsAsync(CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        var sectionFieldIds = await _db.TemplateFields
            .Where(f => f.FieldType == FieldType.ProjectRepeatableSection)
            .Select(f => f.Id).ToListAsync(ct);
        if (sectionFieldIds.Count == 0) return result;

        var jsons = await _db.SubmissionFieldValues
            .Where(v => sectionFieldIds.Contains(v.TemplateFieldId) && v.ValueJson != null)
            .Select(v => v.ValueJson!).ToListAsync(ct);
        foreach (var json in jsons)
            foreach (var pid in ExtractProjectIds(json))
                result.Add(pid);
        return result;
    }

    private static List<Guid> ExtractProjectIds(string valueJson)
    {
        var ids = new List<Guid>();
        try
        {
            var entries = JsonSerializer.Deserialize<List<SectionEntry>>(valueJson, SectionJson);
            if (entries is not null)
                foreach (var e in entries)
                    if (e.ProjectId is Guid pid && pid != Guid.Empty) ids.Add(pid);
        }
        catch { /* ValueJson تالف يُتجاهَل بأمان */ }
        return ids;
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}

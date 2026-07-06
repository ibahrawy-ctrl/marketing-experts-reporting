using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ReportTemplateService : IReportTemplateService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;

    public ReportTemplateService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
    }

    public async Task<Result<ReportTemplateDetailDto>> CreateAsync(CreateTemplateRequest request, Guid ownerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<ReportTemplateDetailDto>.Failure("عنوان القالب مطلوب.", "template.title_required");

        var template = new ReportTemplate
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            JobRoleId = request.JobRoleId,
            DefaultPeriodType = request.DefaultPeriodType,
            Classification = request.Classification,
            Status = TemplateStatus.Draft,
            OwnerId = ownerId
        };
        template.Versions.Add(new ReportTemplateVersion { VersionNumber = 1, IsPublished = false });

        _db.ReportTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return Result<ReportTemplateDetailDto>.Success(await BuildDetailAsync(template.Id, ct));
    }

    public async Task<Result<IReadOnlyList<ReportTemplateDto>>> ListAsync(TemplateFilter filter, CancellationToken ct = default)
    {
        var q = _db.ReportTemplates.AsNoTracking().AsQueryable();
        if (filter.JobRoleId is not null) q = q.Where(t => t.JobRoleId == filter.JobRoleId);
        if (filter.Status is not null) q = q.Where(t => t.Status == filter.Status);
        if (filter.IsActive is not null) q = q.Where(t => t.IsActive == filter.IsActive);

        if (filter.SubjectUserId is { } subjectId)
        {
            // إنشاء «بالنيابة»: صاحب التقرير هو الموظّف المختار. لا يكفي كون المُنشئ مديرًا/مديرًا
            // عامًّا ليرى الكل — يجب أن يكون الموظّف ضمن نطاق رؤيته، ثم تُطبَّق أولوية إسناد الموظّف.
            var scope = await _scope.ResolveAsync(ct);
            if (!scope.Contains(subjectId))
                return Result<IReadOnlyList<ReportTemplateDto>>.Failure(
                    "لا تملك صلاحية إنشاء تقرير بالنيابة عن هذا الموظّف.", "auth.forbidden");
            var allowed = await ResolveAssignedTemplateIdsAsync(q, subjectId, ct);
            q = q.Where(t => allowed.Contains(t.Id));
        }
        else if (filter.AssignedOnly)
        {
            // إنشاء «تقريري»: صاحب التقرير هو المستخدم الحالي. تُطبَّق أولوية الإسناد حتى لمن يملك
            // صلاحية إدارة القوالب (مدير عام/أدمن) — في هذا المسار يرى قالبه المُسنَد فقط لا الكل.
            if (_currentUser.UserId is { } selfId)
            {
                var allowed = await ResolveAssignedTemplateIdsAsync(q, selfId, ct);
                q = q.Where(t => allowed.Contains(t.Id));
            }
            else
            {
                q = q.Where(t => t.JobRoleId == null);
            }
        }
        else if (!RoleAccess.Has(_currentUser.Roles, "ManageTemplates"))
        {
            // وضع التصفّح العادي: من لا يملك صلاحية إدارة القوالب يرى القوالب العامة (بلا وظيفة)
            // أو المربوطة بوظيفته فقط. مديرو القوالب (Admin/CEO/GM) يرون الكل في هذا الوضع.
            var viewerJobRoleId = _currentUser.UserId is { } uid
                ? await _db.Users.AsNoTracking()
                    .Where(u => u.Id == uid)
                    .Select(u => u.JobRoleId)
                    .FirstOrDefaultAsync(ct)
                : null;
            q = q.Where(t => t.JobRoleId == null || t.JobRoleId == viewerJobRoleId);
        }

        var rows = await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new
            {
                Template = t,
                LatestVersion = t.Versions.Max(v => (int?)v.VersionNumber) ?? 0,
                FieldCount = t.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.Fields.Count)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var list = rows.Select(r => new ReportTemplateDto(
            r.Template.Id, r.Template.Title, r.Template.Description, r.Template.JobRoleId,
            r.Template.DefaultPeriodType, r.Template.Status, r.Template.OwnerId, r.Template.IsActive,
            r.LatestVersion, r.FieldCount, r.Template.Classification)).ToList();

        return Result<IReadOnlyList<ReportTemplateDto>>.Success(list);
    }

    // مستوى أخصّية الإسناد (تنازليًّا): الأصغر رقمًا = الأخصّ.
    private enum MatchTier { Employee = 1, JobRole = 2, Team = 3, Department = 4, General = 5 }

    private sealed record TemplateMeta(Guid Id, Guid? JobRoleId, TemplateClassification Classification, PeriodType PeriodType);
    private sealed record UserScopes(Guid UserId, Guid? JobRoleId, Guid? TeamId, Guid? DepartmentId);
    private sealed record ResolveResult(bool Included, MatchTier Tier, string Reason);

    /// <summary>
    /// حلّ قالب واحد لمستخدم واحد بترتيب الأولوية المعتمَد (الأخصّ أولًا، والاستثناء يتفوّق على الإسناد
    /// في مستواه وما دونه):
    /// ① استثناء موظّف ② إسناد موظّف ③ استثناء مسمّى ④ إسناد مسمّى (الصريح أو <see cref="ReportTemplate.JobRoleId"/>)
    /// ⑤ استثناء فريق ⑥ إسناد فريق ⑦ استثناء إدارة ⑧ إسناد إدارة ⑨ عام (قالب بلا مسمّى).
    /// تُرجِع null إذا كان القالب متخصصًا ولم يطابق المستخدم بأيّ مستوى.
    /// </summary>
    private static ResolveResult? ResolveOne(
        TemplateMeta t, UserScopes u,
        HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)> assignments)
    {
        bool Has(TemplateAssignmentScope scope, Guid? id, TemplateAssignmentKind kind)
            => id is Guid g && assignments.Contains((t.Id, scope, g, kind));

        if (Has(TemplateAssignmentScope.Employee, u.UserId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Employee, "excludedManually");
        if (Has(TemplateAssignmentScope.Employee, u.UserId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Employee, "matchedByUser");

        if (Has(TemplateAssignmentScope.JobRole, u.JobRoleId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.JobRole, "excludedManually");
        if ((u.JobRoleId is Guid jr && t.JobRoleId == jr)
            || Has(TemplateAssignmentScope.JobRole, u.JobRoleId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.JobRole, "matchedByJobRole");

        if (Has(TemplateAssignmentScope.Team, u.TeamId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Team, "excludedManually");
        if (Has(TemplateAssignmentScope.Team, u.TeamId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Team, "matchedByTeam");

        if (Has(TemplateAssignmentScope.Department, u.DepartmentId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Department, "excludedManually");
        if (Has(TemplateAssignmentScope.Department, u.DepartmentId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Department, "matchedByDepartment");

        if (t.JobRoleId is null)
            return new(true, MatchTier.General, "matchedByGeneral");

        return null;
    }

    private async Task<HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)>> LoadActiveAssignmentsAsync(
        IReadOnlyCollection<Guid> templateIds, CancellationToken ct)
    {
        if (templateIds.Count == 0)
            return new HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)>();
        var rows = await _db.ReportTemplateAssignments.AsNoTracking()
            .Where(a => a.IsActive && templateIds.Contains(a.ReportTemplateId))
            .Select(a => new { a.ReportTemplateId, a.ScopeType, a.ScopeId, a.Kind })
            .ToListAsync(ct);
        return rows.Select(r => (r.ReportTemplateId, r.ScopeType, r.ScopeId, r.Kind)).ToHashSet();
    }

    /// <summary>
    /// أولوية اختيار قوالب التقرير لصاحب التقرير ضمن المرشّحات الحالية (عادةً منشور/نشط):
    /// تُحلّ كل القوالب بترتيب الأخصّية، ثم — للقوالب الأساسية (Primary) — يُبقى لكل دورية أعلى مستوى
    /// أخصّية فقط (الأخصّ يسبق العام، فلا يظهر تقريران أساسيان لنفس الدورية). القوالب التكميلية
    /// (Supplementary) تظهر دائمًا متى أُسنِدَت ولم تُستثنَ. عند غياب أي إسناد صريح يؤول السلوك إلى
    /// «قالب المسمّى الوظيفي إن وُجد وإلّا العام» (توافق خلفي تام مع نظام JobRole القائم).
    /// </summary>
    private async Task<List<Guid>> ResolveAssignedTemplateIdsAsync(
        IQueryable<ReportTemplate> q, Guid subjectId, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking()
            .Where(x => x.Id == subjectId)
            .Select(x => new { x.JobRoleId, x.TeamId, x.DepartmentId })
            .FirstOrDefaultAsync(ct);
        var scopes = new UserScopes(subjectId, u?.JobRoleId, u?.TeamId, u?.DepartmentId);

        var metas = await q
            .Select(t => new TemplateMeta(t.Id, t.JobRoleId, t.Classification, t.DefaultPeriodType))
            .ToListAsync(ct);
        var ids = metas.Select(m => m.Id).ToList();
        var assignments = await LoadActiveAssignmentsAsync(ids, ct);

        var included = new List<(TemplateMeta Meta, MatchTier Tier)>();
        foreach (var m in metas)
        {
            var r = ResolveOne(m, scopes, assignments);
            if (r is { Included: true }) included.Add((m, r.Tier));
        }

        // القوالب العامة (General tier) احتياطية فقط: تُستخدم لمن لا يملك أيّ مطابقة أخصّ
        // (إسناد صريح أو مطابقة مسمّى/فريق/إدارة). هذا يحافظ على السلوك القديم «قالب الدور
        // إن وُجد وإلّا العام» لكلٍّ من الأساسي والتكميلي معًا، فلا تتسرّب القوالب العامة للجميع.
        var hasSpecific = included.Any(x => x.Tier != MatchTier.General);
        var effective = hasSpecific
            ? included.Where(x => x.Tier != MatchTier.General).ToList()
            : included;

        var allowed = new List<Guid>();
        // التكميلية: تظهر متى أُسنِدَت ولم تُستثنَ ضمن المطابقات الفعّالة.
        allowed.AddRange(effective
            .Where(x => x.Meta.Classification == TemplateClassification.Supplementary)
            .Select(x => x.Meta.Id));
        // الأساسية: لكل دورية، أبقِ أعلى مستوى أخصّية فقط (الأخصّ يسبق العام).
        foreach (var grp in effective
            .Where(x => x.Meta.Classification == TemplateClassification.Primary)
            .GroupBy(x => x.Meta.PeriodType))
        {
            var minTier = grp.Min(x => x.Tier);
            allowed.AddRange(grp.Where(x => x.Tier == minTier).Select(x => x.Meta.Id));
        }
        return allowed.Distinct().ToList();
    }

    /// <summary>
    /// حارس الإسناد المركزي (المصدر الوحيد للحقيقة): يعيد استخدام <see cref="ResolveAssignedTemplateIdsAsync"/>
    /// نفسها على القوالب المنشورة النشطة، فيطابق تمامًا منطق assignedOnly بكل مستوياته (Employee/JobRole/Team/
    /// Department/General) والإسناد/الاستثناء. يُرجِع true فقط إذا كان <paramref name="templateId"/> ضمن
    /// القوالب المُسنَدة فعليًّا للمستخدم. لا يمنح أي إعفاء للأدوار الإدارية (لا انتحال ضمني).
    /// </summary>
    public async Task<bool> IsTemplateAssignedToUserAsync(Guid userId, Guid templateId, CancellationToken ct = default)
    {
        var q = _db.ReportTemplates.AsNoTracking()
            .Where(t => t.Status == TemplateStatus.Published && t.IsActive);
        var allowed = await ResolveAssignedTemplateIdsAsync(q, userId, ct);
        return allowed.Contains(templateId);
    }

    public async Task<Result<ReportTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.ReportTemplates.AnyAsync(t => t.Id == id, ct);
        if (!exists) return Result<ReportTemplateDetailDto>.Failure("القالب غير موجود.", "template.not_found");
        return Result<ReportTemplateDetailDto>.Success(await BuildDetailAsync(id, ct));
    }

    public async Task<Result<ReportTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result<ReportTemplateDetailDto>.Failure("القالب غير موجود.", "template.not_found");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<ReportTemplateDetailDto>.Failure("عنوان القالب مطلوب.", "template.title_required");

        template.Title = request.Title.Trim();
        template.Description = request.Description;
        template.JobRoleId = request.JobRoleId;
        template.DefaultPeriodType = request.DefaultPeriodType;
        template.Classification = request.Classification;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<ReportTemplateDetailDto>.Success(await BuildDetailAsync(id, ct));
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result.Failure("القالب غير موجود.", "template.not_found");
        template.Status = TemplateStatus.Archived;
        template.IsActive = false;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // الأرشفة لا تمسّ التقارير القديمة (مرتبطة بالإصدار لا بالقالب)، فقط تُخفي القالب من إنشاء التقارير الجديدة.
        await _audit.LogAsync(_currentUser.UserId, "template.archived", nameof(ReportTemplate), id,
            $"{{\"title\":\"{template.Title}\"}}", ct: ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.ReportTemplates.Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result.Failure("القالب غير موجود.", "template.not_found");

        // الحذف النهائي مسموح فقط لقالب مسودة لم يُستخدَم في أي تقرير مُسلَّم.
        // القوالب المنشورة أو المستخدَمة تُؤرشَف فقط، حفاظًا على التقارير القديمة المرتبطة بإصداراتها.
        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var used = versionIds.Count > 0 &&
            await _db.ReportSubmissions.AnyAsync(s => versionIds.Contains(s.ReportTemplateVersionId), ct);
        if (template.Status != TemplateStatus.Draft || used)
            return Result.Failure(
                "لا يمكن حذف قالب منشور أو مستخدَم في تقارير سابقة؛ استخدم الأرشفة بدلًا من الحذف.",
                "template.delete_forbidden.conflict");

        foreach (var v in template.Versions)
            _db.TemplateFields.RemoveRange(v.Fields);
        _db.ReportTemplateVersions.RemoveRange(template.Versions);
        _db.ReportTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "template.deleted", nameof(ReportTemplate), id,
            $"{{\"title\":\"{template.Title}\"}}", ct: ct);
        return Result.Success();
    }

    public async Task<Result<TemplatePreviewDto>> PreviewAsync(Guid id, CancellationToken ct = default)
    {
        // قراءة فقط — لا إنشاء تسليم ولا أي كتابة لقاعدة البيانات.
        var t = await _db.ReportTemplates.AsNoTracking()
            .Include(x => x.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<TemplatePreviewDto>.Failure("القالب غير موجود.", "template.not_found");

        // الإصدار الفعّال كما يراه الموظّف: آخر إصدار منشور، وإلّا آخر إصدار (مسودة) للمعاينة الإدارية.
        var effective = t.Versions.Where(v => v.IsPublished).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
            ?? t.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        var fields = effective is null
            ? new List<TemplateFieldDto>()
            : effective.Fields.OrderBy(f => f.Order).Select(MapField).ToList();

        return Result<TemplatePreviewDto>.Success(new TemplatePreviewDto(
            t.Id, t.Title, t.Description, t.DefaultPeriodType, t.Classification, t.Status, t.IsActive,
            effective?.VersionNumber, effective?.IsPublished ?? false, fields));
    }

    public async Task<Result<TemplateAssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.ReportTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<TemplateAssignmentsDto>.Failure("القالب غير موجود.", "template.not_found");

        var isRoleSpecific = t.JobRoleId is not null;
        var isAssignable = t.Status == TemplateStatus.Published && t.IsActive;

        // أسماء الكيانات لعرضها (مسمّيات/فِرق/إدارات/موظّفون).
        var jobRoles = await _db.JobRoles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var teams = await _db.Teams.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var templateJobRoleName = t.JobRoleId is { } trid ? jobRoles.GetValueOrDefault(trid) : null;

        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive, u.JobRoleId, u.TeamId, u.DepartmentId })
            .ToListAsync(ct);
        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);

        // كل القوالب القابلة للإسناد (منشورة ونشطة) — لحساب «الأخصّية تسبق العام» والتعارضات عبر القوالب.
        var assignable = await _db.ReportTemplates.AsNoTracking()
            .Where(x => x.Status == TemplateStatus.Published && x.IsActive)
            .Select(x => new { x.Id, x.Title, x.JobRoleId, x.Classification, x.DefaultPeriodType })
            .ToListAsync(ct);
        var assignableMetas = assignable
            .Select(x => new TemplateMeta(x.Id, x.JobRoleId, x.Classification, x.DefaultPeriodType)).ToList();
        var titles = assignable.ToDictionary(x => x.Id, x => x.Title);

        var assignmentTemplateIds = assignableMetas.Select(m => m.Id).ToList();
        if (!assignmentTemplateIds.Contains(t.Id)) assignmentTemplateIds.Add(t.Id);
        var assignments = await LoadActiveAssignmentsAsync(assignmentTemplateIds, ct);

        var thisMeta = new TemplateMeta(t.Id, t.JobRoleId, t.Classification, t.DefaultPeriodType);

        var matched = new List<TemplateAssignmentUserDto>();
        var excluded = new List<TemplateAssignmentUserDto>();
        var conflicts = new List<TemplateAssignmentConflictDto>();

        foreach (var u in users)
        {
            var scopes = new UserScopes(u.Id, u.JobRoleId, u.TeamId, u.DepartmentId);
            var jobRoleName = u.JobRoleId is { } urid ? jobRoles.GetValueOrDefault(urid) : null;
            var teamName = u.TeamId is { } utid ? teams.GetValueOrDefault(utid) : null;
            var deptName = u.DepartmentId is { } udid ? depts.GetValueOrDefault(udid) : null;

            TemplateAssignmentUserDto Make(string? exclusion, string? match) =>
                new(u.Id, u.FullName, u.Email, u.JobRoleId, jobRoleName, u.IsActive, exclusion, match,
                    u.TeamId, teamName, u.DepartmentId, deptName);

            if (!u.IsActive) { excluded.Add(Make("excludedBecauseInactive", null)); continue; }

            var r = ResolveOne(thisMeta, scopes, assignments);
            // عدم المطابقة لمسمّى القالب المتخصّص = «بقيّة موظّفي الشركة»، وليست استثناءً ذا معنى للعرض،
            // لذا تُتجاهَل (لا تُدرَج في قائمة المستثنين). تبقى الاستثناءات الحقيقية فقط: يدوي/أخصّ/معطّل/غير قابل للإسناد.
            if (r is null) continue;
            if (!r.Included) { excluded.Add(Make(r.Reason, null)); continue; }

            if (t.Classification == TemplateClassification.Primary)
            {
                // أعلى مستوى أخصّية مطابق لهذا المستخدم بين القوالب الأساسية بنفس الدورية.
                var userPrimaries = new List<(Guid Id, MatchTier Tier)>();
                foreach (var m in assignableMetas.Where(m =>
                    m.Classification == TemplateClassification.Primary && m.PeriodType == t.DefaultPeriodType))
                {
                    var rr = ResolveOne(m, scopes, assignments);
                    if (rr is { Included: true }) userPrimaries.Add((m.Id, rr.Tier));
                }
                var minTier = userPrimaries.Count > 0 ? userPrimaries.Min(x => x.Tier) : r.Tier;

                if (r.Tier > minTier)
                {
                    // يوجد قالب أساسي أخصّ لهذا المستخدم لنفس الدورية ⇒ هذا القالب لا يظهر له.
                    excluded.Add(Make("excludedBecauseMoreSpecificTemplateExists", null));
                    continue;
                }

                // عند نفس أعلى مستوى أخصّية: أيّ قالب أساسي آخر ⇒ تعارض «أكثر من أساسي لنفس الدورية».
                // يُحتسب التعارض فقط على المستويات الأخصّ (موظّف/مسمّى/فريق/إدارة)؛ أمّا القوالب العامة
                // فهي مجمّع احتياطي مشترك لا يُعدّ تعدُّدها سوء إعداد يستوجب التنبيه.
                var tied = minTier == MatchTier.General
                    ? new List<(Guid Id, MatchTier Tier)>()
                    : userPrimaries.Where(x => x.Tier == minTier && x.Id != t.Id).ToList();
                if (isAssignable)
                {
                    foreach (var other in tied)
                        conflicts.Add(new TemplateAssignmentConflictDto(
                            u.Id, u.FullName, t.Id, t.Title, other.Id, titles.GetValueOrDefault(other.Id) ?? "—",
                            t.DefaultPeriodType,
                            "كلا القالبين أساسيّ (Primary) لنفس الموظّف ونفس الدورية وبنفس مستوى الأولوية، ولا يُسمح بأكثر من تقرير أساسي واحد لنفس الفترة.",
                            "اجعل أحد القالبين تكميليًّا (Supplementary)، أو استثنِ الموظّف من أحدهما، أو غيّر نطاق الإسناد ليصبح أحدهما أخصّ من الآخر."));
                }
                matched.Add(Make(null, r.Reason));
            }
            else
            {
                matched.Add(Make(null, r.Reason));
            }
        }

        // إن كان القالب غير قابل للإسناد (مسودة/مؤرشف/غير نشط) فلا أحد يستلمه فعليًّا الآن.
        if (!isAssignable)
        {
            excluded.AddRange(matched.Select(m => m with
            {
                ExclusionReason = "excludedBecauseTemplateNotAssignable",
                MatchReason = null
            }));
            matched = new List<TemplateAssignmentUserDto>();
            conflicts = new List<TemplateAssignmentConflictDto>();
        }

        // صفوف الإسناد/الاستثناء الصريحة لهذا القالب (النشطة وغير النشطة) للعرض والإدارة.
        var rawRows = await _db.ReportTemplateAssignments.AsNoTracking()
            .Where(x => x.ReportTemplateId == id)
            .OrderBy(x => x.ScopeType).ThenByDescending(x => x.Kind).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.ScopeType, x.ScopeId, x.Kind, x.Notes, x.IsActive, x.CreatedAtUtc })
            .ToListAsync(ct);

        string? ScopeName(TemplateAssignmentScope s, Guid sid) => s switch
        {
            TemplateAssignmentScope.Employee => userNames.GetValueOrDefault(sid),
            TemplateAssignmentScope.JobRole => jobRoles.GetValueOrDefault(sid),
            TemplateAssignmentScope.Team => teams.GetValueOrDefault(sid),
            TemplateAssignmentScope.Department => depts.GetValueOrDefault(sid),
            _ => null
        };

        var assignmentRows = rawRows.Select(x => new TemplateAssignmentRowDto(
            x.Id, x.ScopeType, x.ScopeId, ScopeName(x.ScopeType, x.ScopeId), x.Kind, x.Notes, x.IsActive, x.CreatedAtUtc))
            .ToList();

        return Result<TemplateAssignmentsDto>.Success(new TemplateAssignmentsDto(
            t.Id, t.Title, t.JobRoleId, templateJobRoleName, t.DefaultPeriodType, t.Classification,
            t.Status, t.IsActive, isAssignable, isRoleSpecific,
            matched.OrderBy(m => m.FullName).ToList(),
            excluded.OrderBy(m => m.FullName).ToList(),
            assignmentRows,
            conflicts.OrderBy(c => c.FullName).ToList()));
    }

    public async Task<Result<TemplateAssignmentRowDto>> AddAssignmentAsync(
        Guid templateId, CreateAssignmentRequest request, CancellationToken ct = default)
    {
        var template = await _db.ReportTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return Result<TemplateAssignmentRowDto>.Failure("القالب غير موجود.", "template.not_found");

        var (exists, name) = await ResolveScopeAsync(request.ScopeType, request.ScopeId, ct);
        if (!exists)
            return Result<TemplateAssignmentRowDto>.Failure("الكيان المُسنَد إليه غير موجود.", "assignment.scope_not_found");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // منع التكرار لنفس (القالب/المستوى/المعرّف/النوع)؛ إن وُجد صفّ معطّل أعِد تفعيله بدل إنشاء جديد.
        var dup = await _db.ReportTemplateAssignments.FirstOrDefaultAsync(a =>
            a.ReportTemplateId == templateId && a.ScopeType == request.ScopeType &&
            a.ScopeId == request.ScopeId && a.Kind == request.Kind, ct);
        if (dup is not null)
        {
            if (dup.IsActive)
                return Result<TemplateAssignmentRowDto>.Failure(
                    "هذا الإسناد/الاستثناء موجود بالفعل لنفس الكيان.", "assignment.duplicate.conflict");
            dup.IsActive = true;
            dup.Notes = notes;
            dup.UpdatedById = _currentUser.UserId;
            dup.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await LogAssignmentAsync("template.assignment.enabled", dup, templateId, ct);
            return Result<TemplateAssignmentRowDto>.Success(MapAssignment(dup, name));
        }

        var row = new ReportTemplateAssignment
        {
            ReportTemplateId = templateId,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            Kind = request.Kind,
            Notes = notes,
            IsActive = true,
            CreatedById = _currentUser.UserId
        };
        _db.ReportTemplateAssignments.Add(row);
        await _db.SaveChangesAsync(ct);
        await LogAssignmentAsync("template.assignment.added", row, templateId, ct);
        return Result<TemplateAssignmentRowDto>.Success(MapAssignment(row, name));
    }

    public async Task<Result<TemplateAssignmentRowDto>> UpdateAssignmentAsync(
        Guid templateId, Guid assignmentId, UpdateAssignmentRequest request, CancellationToken ct = default)
    {
        var row = await _db.ReportTemplateAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ReportTemplateId == templateId, ct);
        if (row is null) return Result<TemplateAssignmentRowDto>.Failure("الإسناد غير موجود.", "assignment.not_found");

        row.IsActive = request.IsActive;
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.UpdatedById = _currentUser.UserId;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogAssignmentAsync(row.IsActive ? "template.assignment.enabled" : "template.assignment.disabled", row, templateId, ct);
        var (_, name) = await ResolveScopeAsync(row.ScopeType, row.ScopeId, ct);
        return Result<TemplateAssignmentRowDto>.Success(MapAssignment(row, name));
    }

    public async Task<Result> RemoveAssignmentAsync(Guid templateId, Guid assignmentId, CancellationToken ct = default)
    {
        var row = await _db.ReportTemplateAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ReportTemplateId == templateId, ct);
        if (row is null) return Result.Failure("الإسناد غير موجود.", "assignment.not_found");

        _db.ReportTemplateAssignments.Remove(row);
        await _db.SaveChangesAsync(ct);
        await LogAssignmentAsync("template.assignment.removed", row, templateId, ct);
        return Result.Success();
    }

    // التحقق من وجود الكيان المُسنَد إليه وإرجاع اسمه للعرض.
    private async Task<(bool Exists, string? Name)> ResolveScopeAsync(
        TemplateAssignmentScope scope, Guid scopeId, CancellationToken ct) => scope switch
    {
        TemplateAssignmentScope.Employee => (
            await _db.Users.AnyAsync(x => x.Id == scopeId, ct),
            await _db.Users.AsNoTracking().Where(x => x.Id == scopeId).Select(x => x.FullName).FirstOrDefaultAsync(ct)),
        TemplateAssignmentScope.JobRole => (
            await _db.JobRoles.AnyAsync(x => x.Id == scopeId, ct),
            await _db.JobRoles.AsNoTracking().Where(x => x.Id == scopeId).Select(x => x.NameAr).FirstOrDefaultAsync(ct)),
        TemplateAssignmentScope.Team => (
            await _db.Teams.AnyAsync(x => x.Id == scopeId, ct),
            await _db.Teams.AsNoTracking().Where(x => x.Id == scopeId).Select(x => x.NameAr).FirstOrDefaultAsync(ct)),
        TemplateAssignmentScope.Department => (
            await _db.Departments.AnyAsync(x => x.Id == scopeId, ct),
            await _db.Departments.AsNoTracking().Where(x => x.Id == scopeId).Select(x => x.NameAr).FirstOrDefaultAsync(ct)),
        _ => (false, null)
    };

    private async Task LogAssignmentAsync(string action, ReportTemplateAssignment row, Guid templateId, CancellationToken ct)
        => await _audit.LogAsync(_currentUser.UserId, action, nameof(ReportTemplateAssignment), row.Id,
            $"{{\"templateId\":\"{templateId}\",\"scopeType\":\"{row.ScopeType}\",\"scopeId\":\"{row.ScopeId}\",\"kind\":\"{row.Kind}\",\"isActive\":{row.IsActive.ToString().ToLowerInvariant()}}}",
            ct: ct);

    private static TemplateAssignmentRowDto MapAssignment(ReportTemplateAssignment a, string? name)
        => new(a.Id, a.ScopeType, a.ScopeId, name, a.Kind, a.Notes, a.IsActive, a.CreatedAtUtc);

    public async Task<Result<TemplateFieldDto>> AddFieldAsync(Guid versionId, UpsertFieldRequest request, CancellationToken ct = default)
    {
        var version = await _db.ReportTemplateVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result<TemplateFieldDto>.Failure("الإصدار غير موجود.", "version.not_found");
        if (version.IsPublished) return Result<TemplateFieldDto>.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "version.published.conflict");
        if (string.IsNullOrWhiteSpace(request.Label))
            return Result<TemplateFieldDto>.Failure("عنوان الحقل مطلوب.", "field.label_required");

        var maxOrder = await _db.TemplateFields.Where(f => f.ReportTemplateVersionId == versionId)
            .Select(f => (int?)f.Order).MaxAsync(ct) ?? -1;

        var field = new TemplateField
        {
            ReportTemplateVersionId = versionId,
            Label = request.Label.Trim(),
            Key = request.Key,
            FieldType = request.FieldType,
            IsRequired = request.IsRequired,
            HelpText = request.HelpText,
            ConfigJson = request.ConfigJson,
            Order = maxOrder + 1
        };
        _db.TemplateFields.Add(field);
        await _db.SaveChangesAsync(ct);

        return Result<TemplateFieldDto>.Success(MapField(field));
    }

    public async Task<Result<TemplateFieldDto>> UpdateFieldAsync(Guid fieldId, UpsertFieldRequest request, CancellationToken ct = default)
    {
        var field = await _db.TemplateFields.Include(f => f.ReportTemplateVersion)
            .FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) return Result<TemplateFieldDto>.Failure("الحقل غير موجود.", "field.not_found");
        if (field.ReportTemplateVersion!.IsPublished)
            return Result<TemplateFieldDto>.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "version.published.conflict");
        if (string.IsNullOrWhiteSpace(request.Label))
            return Result<TemplateFieldDto>.Failure("عنوان الحقل مطلوب.", "field.label_required");

        field.Label = request.Label.Trim();
        field.Key = request.Key;
        field.FieldType = request.FieldType;
        field.IsRequired = request.IsRequired;
        field.HelpText = request.HelpText;
        field.ConfigJson = request.ConfigJson;
        field.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<TemplateFieldDto>.Success(MapField(field));
    }

    public async Task<Result> DeleteFieldAsync(Guid fieldId, CancellationToken ct = default)
    {
        var field = await _db.TemplateFields.Include(f => f.ReportTemplateVersion)
            .FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) return Result.Failure("الحقل غير موجود.", "field.not_found");
        if (field.ReportTemplateVersion!.IsPublished)
            return Result.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "version.published.conflict");

        _db.TemplateFields.Remove(field);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReorderFieldsAsync(Guid versionId, IReadOnlyList<Guid> orderedFieldIds, CancellationToken ct = default)
    {
        var version = await _db.ReportTemplateVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result.Failure("الإصدار غير موجود.", "version.not_found");
        if (version.IsPublished) return Result.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "version.published.conflict");

        var fields = await _db.TemplateFields.Where(f => f.ReportTemplateVersionId == versionId).ToListAsync(ct);
        for (var i = 0; i < orderedFieldIds.Count; i++)
        {
            var f = fields.FirstOrDefault(x => x.Id == orderedFieldIds[i]);
            if (f is not null) f.Order = i;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<TemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default)
    {
        var version = await _db.ReportTemplateVersions.Include(v => v.Fields)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result<TemplateVersionDto>.Failure("الإصدار غير موجود.", "version.not_found");
        if (version.IsPublished) return Result<TemplateVersionDto>.Failure("الإصدار منشور بالفعل.", "version.already_published.conflict");
        if (version.Fields.Count == 0)
            return Result<TemplateVersionDto>.Failure("لا يمكن نشر إصدار بلا حقول.", "version.empty.conflict");

        version.IsPublished = true;
        version.PublishedAtUtc = DateTime.UtcNow;
        version.PublishedById = publishedById;
        version.UpdatedAtUtc = DateTime.UtcNow;

        var template = await _db.ReportTemplates.FirstAsync(t => t.Id == version.ReportTemplateId, ct);
        template.Status = TemplateStatus.Published;
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<TemplateVersionDto>.Success(MapVersion(version));
    }

    public async Task<Result<TemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _db.ReportTemplates.Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return Result<TemplateVersionDto>.Failure("القالب غير موجود.", "template.not_found");

        if (template.Versions.Any(v => !v.IsPublished))
            return Result<TemplateVersionDto>.Failure("يوجد إصدار مسودة مفتوح بالفعل.", "version.draft_exists.conflict");

        var latest = template.Versions.OrderByDescending(v => v.VersionNumber).First();
        var draft = new ReportTemplateVersion
        {
            ReportTemplateId = templateId,
            VersionNumber = latest.VersionNumber + 1,
            IsPublished = false
        };
        foreach (var f in latest.Fields.OrderBy(f => f.Order))
        {
            draft.Fields.Add(new TemplateField
            {
                Label = f.Label,
                Key = f.Key,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                HelpText = f.HelpText,
                ConfigJson = f.ConfigJson,
                Order = f.Order
            });
        }
        _db.ReportTemplateVersions.Add(draft);
        await _db.SaveChangesAsync(ct);

        return Result<TemplateVersionDto>.Success(MapVersion(draft));
    }

    /// <summary>
    /// حذف نسخة قالب غير مستخدَمة فقط. يُمنع الحذف إن كانت النسخة مرتبطة بأي تقرير سابق،
    /// أو كانت النسخة الوحيدة، أو الأحدث، أو المنشورة الحالية المستخدَمة للتقارير الجديدة.
    /// لا يمسّ القالب نفسه ولا أي تقرير. يزيل حقول النسخة المرتبطة فقط.
    /// </summary>
    public async Task<Result> DeleteVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await _db.ReportTemplateVersions.Include(v => v.Fields)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result.Failure("الإصدار غير موجود.", "version.not_found");

        var template = await _db.ReportTemplates.Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == version.ReportTemplateId, ct);
        if (template is null) return Result.Failure("القالب غير موجود.", "template.not_found");

        // (1) نسخة مستخدَمة في أي تقرير سابق — تبقى محفوظة (لا يتغيّر أي تقرير قديم).
        var used = await _db.ReportSubmissions.AnyAsync(s => s.ReportTemplateVersionId == versionId, ct);
        if (used)
            return Result.Failure("لا يمكن حذف نسخة مستخدَمة في تقارير سابقة.", "version.delete_forbidden.conflict");

        // (2) لا تحذف النسخة الوحيدة للقالب.
        if (template.Versions.Count <= 1)
            return Result.Failure("لا يمكن حذف النسخة الوحيدة للقالب.", "version.delete_forbidden.conflict");

        // (3) لا تحذف النسخة المنشورة الحالية (المرتبطة بها التقارير الجديدة).
        var currentPublishedId = template.Versions.Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefault()?.Id;
        if (currentPublishedId == versionId)
            return Result.Failure("لا يمكن حذف النسخة المنشورة الحالية.", "version.delete_forbidden.conflict");

        // (4) لا تحذف أحدث نسخة.
        if (version.VersionNumber == template.Versions.Max(v => v.VersionNumber))
            return Result.Failure("لا يمكن حذف أحدث نسخة من القالب.", "version.delete_forbidden.conflict");

        _db.TemplateFields.RemoveRange(version.Fields);
        _db.ReportTemplateVersions.Remove(version);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(_currentUser.UserId, "template.version_deleted", nameof(ReportTemplateVersion), versionId, ct: ct);
        return Result.Success();
    }

    private async Task<ReportTemplateDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.ReportTemplates.AsNoTracking()
            .Include(x => x.Versions).ThenInclude(v => v.Fields)
            .FirstAsync(x => x.Id == id, ct);

        // عدد التقارير المرتبطة بكل إصدار على حدة — لتحديد قابلية الحذف الآمن للنسخة والقالب.
        var versionIds = t.Versions.Select(v => v.Id).ToList();
        var perVersionCounts = versionIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.ReportSubmissions.AsNoTracking()
                .Where(s => versionIds.Contains(s.ReportTemplateVersionId))
                .GroupBy(s => s.ReportTemplateVersionId)
                .Select(g => new { VersionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VersionId, x => x.Count, ct);

        var totalVersions = t.Versions.Count;
        var highestVersionNumber = totalVersions == 0 ? 0 : t.Versions.Max(v => v.VersionNumber);
        var currentPublishedId = t.Versions.Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefault()?.Id;

        var versions = t.Versions.OrderBy(v => v.VersionNumber)
            .Select(v => MapVersionWithUsage(v, perVersionCounts.GetValueOrDefault(v.Id),
                totalVersions, highestVersionNumber, currentPublishedId))
            .ToList();

        var submissionCount = perVersionCounts.Values.Sum();
        var canHardDelete = t.Status == TemplateStatus.Draft && submissionCount == 0;

        return new ReportTemplateDetailDto(t.Id, t.Title, t.Description, t.JobRoleId,
            t.DefaultPeriodType, t.Status, t.OwnerId, t.IsActive, t.Classification, versions,
            submissionCount, canHardDelete);
    }

    private static TemplateVersionDto MapVersion(ReportTemplateVersion v) => new(
        v.Id, v.VersionNumber, v.IsPublished, v.PublishedAtUtc,
        v.Fields.OrderBy(f => f.Order).Select(MapField).ToList());

    // نفس التعيين مع حساب الاستخدام وقابلية الحذف الآمن للنسخة (تُستخدم عند بناء تفاصيل القالب).
    // قاعدة الحذف: النسخة قابلة للحذف فقط إن لم تكن مستخدَمة في أي تقرير، وليست الوحيدة،
    // وليست الأحدث، وليست النسخة المنشورة الحالية. غير ذلك تبقى محفوظة مع سبب المنع.
    private static TemplateVersionDto MapVersionWithUsage(
        ReportTemplateVersion v, int submissionCount, int totalVersions,
        int highestVersionNumber, Guid? currentPublishedId)
    {
        var isCurrentPublished = currentPublishedId is { } cid && cid == v.Id;
        string? blockReason =
            submissionCount > 0 ? $"لا يمكن حذف هذه النسخة لأنها مستخدمة في {submissionCount} تقريرًا سابقًا."
            : totalVersions <= 1 ? "لا يمكن حذف النسخة الوحيدة للقالب."
            : isCurrentPublished ? "لا يمكن حذف النسخة المنشورة الحالية المستخدَمة للتقارير الجديدة."
            : v.VersionNumber == highestVersionNumber ? "لا يمكن حذف أحدث نسخة من القالب."
            : null;

        return new TemplateVersionDto(
            v.Id, v.VersionNumber, v.IsPublished, v.PublishedAtUtc,
            v.Fields.OrderBy(f => f.Order).Select(MapField).ToList(),
            submissionCount, isCurrentPublished, blockReason is null, blockReason);
    }

    private static TemplateFieldDto MapField(TemplateField f) => new(
        f.Id, f.Label, f.Key, f.FieldType, f.Order, f.IsRequired, f.HelpText, f.ConfigJson);
}

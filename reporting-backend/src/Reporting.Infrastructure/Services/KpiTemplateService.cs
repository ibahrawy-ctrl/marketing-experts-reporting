using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class KpiTemplateService : IKpiTemplateService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public KpiTemplateService(AppDbContext db, ICurrentUser currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result<KpiTemplateDetailDto>> CreateAsync(CreateKpiTemplateRequest request, Guid ownerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<KpiTemplateDetailDto>.Failure("عنوان القالب مطلوب.", "kpi_template.title_required");

        var template = new KpiTemplate
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            JobRoleId = request.JobRoleId,
            Cadence = request.Cadence,
            Status = TemplateStatus.Draft,
            OwnerId = ownerId
        };
        template.Versions.Add(new KpiTemplateVersion { VersionNumber = 1, IsPublished = false });
        _db.KpiTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return Result<KpiTemplateDetailDto>.Success(await BuildDetailAsync(template.Id, ct));
    }

    public async Task<Result<IReadOnlyList<KpiTemplateDto>>> ListAsync(KpiTemplateFilter filter, CancellationToken ct = default)
    {
        var q = _db.KpiTemplates.AsNoTracking().AsQueryable();
        if (filter.JobRoleId is not null) q = q.Where(t => t.JobRoleId == filter.JobRoleId);
        if (filter.Cadence is not null) q = q.Where(t => t.Cadence == filter.Cadence);
        if (filter.Status is not null) q = q.Where(t => t.Status == filter.Status);
        if (filter.IsActive is not null) q = q.Where(t => t.IsActive == filter.IsActive);

        // SubjectUserId: أولوية اختيار قالب KPI في مسار إنشاء تقييم الأداء العادي (Phase T1).
        // الأولوية الموحَّدة (الأخصّ يطغى، والاستثناء يتفوّق): استثناء موظّف > إسناد موظّف > مسمّى
        // (الصريح أو KpiTemplate.JobRoleId) > فريق > إدارة > عام (JobRoleId == null).
        // القوالب العامّة احتياطية فقط: تُستخدم لمن لا يملك أيّ مطابقة أخصّ — حفاظًا على السلوك القديم
        // «قالب الدور إن وُجد وإلّا العام» مع توسعته بإسنادات الموظّف/الفريق/الإدارة والاستثناءات الصريحة.
        if (filter.SubjectUserId is { } subjectId)
        {
            var allowed = await ResolveAssignedTemplateIdsAsync(q, subjectId, ct);
            q = q.Where(t => allowed.Contains(t.Id));
        }

        var rows = await q.OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new
            {
                Template = t,
                LatestVersion = t.Versions.Max(v => (int?)v.VersionNumber) ?? 0,
                MetricCount = t.Versions.OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.Metrics.Count).FirstOrDefault()
            })
            .ToListAsync(ct);

        var list = rows.Select(r => new KpiTemplateDto(
            r.Template.Id, r.Template.Title, r.Template.Description, r.Template.JobRoleId,
            r.Template.Cadence, r.Template.Status, r.Template.OwnerId, r.Template.IsActive,
            r.LatestVersion, r.MetricCount)).ToList();

        return Result<IReadOnlyList<KpiTemplateDto>>.Success(list);
    }

    public async Task<Result<KpiTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.KpiTemplates.AnyAsync(t => t.Id == id, ct);
        if (!exists) return Result<KpiTemplateDetailDto>.Failure("القالب غير موجود.", "kpi_template.not_found");
        return Result<KpiTemplateDetailDto>.Success(await BuildDetailAsync(id, ct));
    }

    public async Task<Result<KpiTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateKpiTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _db.KpiTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result<KpiTemplateDetailDto>.Failure("القالب غير موجود.", "kpi_template.not_found");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<KpiTemplateDetailDto>.Failure("عنوان القالب مطلوب.", "kpi_template.title_required");

        template.Title = request.Title.Trim();
        template.Description = request.Description;
        template.JobRoleId = request.JobRoleId;
        template.Cadence = request.Cadence;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<KpiTemplateDetailDto>.Success(await BuildDetailAsync(id, ct));
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.KpiTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result.Failure("القالب غير موجود.", "kpi_template.not_found");
        template.Status = TemplateStatus.Archived;
        template.IsActive = false;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.KpiTemplates.Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Result.Failure("القالب غير موجود.", "kpi_template.not_found");
        if (template.Status != TemplateStatus.Archived)
            return Result.Failure("القالب غير مؤرشف.", "kpi_template.not_archived.conflict");

        // إعادة التفعيل تُرجِع الحالة لما يناسب إصداراته: منشور إن وُجد إصدار منشور، وإلا مسودة.
        // لا تمسّ الإصدارات أو المؤشّرات أو التقييمات القائمة (التقييمات مرتبطة بنسخة مجمّدة).
        template.Status = template.Versions.Any(v => v.IsPublished)
            ? TemplateStatus.Published
            : TemplateStatus.Draft;
        template.IsActive = true;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<KpiMetricDto>> AddMetricAsync(Guid versionId, UpsertKpiMetricRequest request, CancellationToken ct = default)
    {
        var version = await _db.KpiTemplateVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result<KpiMetricDto>.Failure("الإصدار غير موجود.", "kpi_version.not_found");
        if (version.IsPublished) return Result<KpiMetricDto>.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "kpi_version.published.conflict");
        var validation = ValidateMetric(request);
        if (!validation.Succeeded) return Result<KpiMetricDto>.Failure(validation.Error!, validation.ErrorCode!);

        var maxOrder = await _db.KpiMetrics.Where(m => m.KpiTemplateVersionId == versionId)
            .Select(m => (int?)m.Order).MaxAsync(ct) ?? -1;

        var metric = new KpiMetric
        {
            KpiTemplateVersionId = versionId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Weight = request.Weight,
            TargetValue = request.TargetValue,
            Unit = request.Unit,
            CalcMethod = request.CalcMethod,
            CalcConfigJson = request.CalcConfigJson,
            Order = maxOrder + 1
        };
        _db.KpiMetrics.Add(metric);
        await _db.SaveChangesAsync(ct);

        return Result<KpiMetricDto>.Success(MapMetric(metric));
    }

    public async Task<Result<KpiMetricDto>> UpdateMetricAsync(Guid metricId, UpsertKpiMetricRequest request, CancellationToken ct = default)
    {
        var metric = await _db.KpiMetrics.Include(m => m.KpiTemplateVersion)
            .FirstOrDefaultAsync(m => m.Id == metricId, ct);
        if (metric is null) return Result<KpiMetricDto>.Failure("المؤشر غير موجود.", "kpi_metric.not_found");
        if (metric.KpiTemplateVersion!.IsPublished)
            return Result<KpiMetricDto>.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "kpi_version.published.conflict");
        var validation = ValidateMetric(request);
        if (!validation.Succeeded) return Result<KpiMetricDto>.Failure(validation.Error!, validation.ErrorCode!);

        metric.Name = request.Name.Trim();
        metric.Description = request.Description;
        metric.Weight = request.Weight;
        metric.TargetValue = request.TargetValue;
        metric.Unit = request.Unit;
        metric.CalcMethod = request.CalcMethod;
        metric.CalcConfigJson = request.CalcConfigJson;
        metric.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<KpiMetricDto>.Success(MapMetric(metric));
    }

    public async Task<Result> DeleteMetricAsync(Guid metricId, CancellationToken ct = default)
    {
        var metric = await _db.KpiMetrics.Include(m => m.KpiTemplateVersion)
            .FirstOrDefaultAsync(m => m.Id == metricId, ct);
        if (metric is null) return Result.Failure("المؤشر غير موجود.", "kpi_metric.not_found");
        if (metric.KpiTemplateVersion!.IsPublished)
            return Result.Failure("لا يمكن تعديل إصدار منشور؛ أنشئ إصدارًا جديدًا.", "kpi_version.published.conflict");

        _db.KpiMetrics.Remove(metric);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<KpiTemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default)
    {
        var version = await _db.KpiTemplateVersions.Include(v => v.Metrics)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return Result<KpiTemplateVersionDto>.Failure("الإصدار غير موجود.", "kpi_version.not_found");
        if (version.IsPublished) return Result<KpiTemplateVersionDto>.Failure("الإصدار منشور بالفعل.", "kpi_version.already_published.conflict");
        if (version.Metrics.Count == 0)
            return Result<KpiTemplateVersionDto>.Failure("لا يمكن نشر إصدار بلا مؤشرات.", "kpi_version.empty.conflict");

        var totalWeight = version.Metrics.Sum(m => m.Weight);
        if (totalWeight != 100m)
            return Result<KpiTemplateVersionDto>.Failure($"مجموع الأوزان يجب أن يساوي 100 (الحالي {totalWeight}).", "kpi_version.weights_invalid.conflict");

        var template = await _db.KpiTemplates.FirstAsync(t => t.Id == version.KpiTemplateId, ct);

        // R5/DEC-01/3+4 — رُفِع حارس «أسبوعيّ فقط» (Phase 4) لأنّه كان يناقض العقد المعتمد نفسه:
        // البند 3 يُقرّ «التقييم الربعيّ الرسميّ» مسارًا قائمًا بذاته، والبند 5 يوجب حسم تواتر الموظّف
        // من إعداده الفعّال — وكلاهما مستحيل ما دام لا يُنشَر إلّا قالب أسبوعيّ. الرفع ليس التفافًا على
        // الحارس بل إزالة تعارض: محرّك الحساب صار يبني نوافذ الالتزام الربعيّة ويقرأ مفاتيح YYYY-Qn،
        // وإنشاء التقييم صار يُلزم تطابق نوع الفترة مع تواتر القالب (KpiEvaluationService).
        version.IsPublished = true;
        version.PublishedAtUtc = DateTime.UtcNow;
        version.PublishedById = publishedById;
        version.UpdatedAtUtc = DateTime.UtcNow;

        template.Status = TemplateStatus.Published;
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<KpiTemplateVersionDto>.Success(MapVersion(version));
    }

    public async Task<Result<KpiTemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _db.KpiTemplates.Include(t => t.Versions).ThenInclude(v => v.Metrics)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return Result<KpiTemplateVersionDto>.Failure("القالب غير موجود.", "kpi_template.not_found");
        if (template.Versions.Any(v => !v.IsPublished))
            return Result<KpiTemplateVersionDto>.Failure("يوجد إصدار مسودة مفتوح بالفعل.", "kpi_version.draft_exists.conflict");

        var latest = template.Versions.OrderByDescending(v => v.VersionNumber).First();
        var draft = new KpiTemplateVersion
        {
            KpiTemplateId = templateId,
            VersionNumber = latest.VersionNumber + 1,
            IsPublished = false
        };
        foreach (var m in latest.Metrics.OrderBy(m => m.Order))
        {
            draft.Metrics.Add(new KpiMetric
            {
                Name = m.Name,
                Description = m.Description,
                Weight = m.Weight,
                TargetValue = m.TargetValue,
                Unit = m.Unit,
                CalcMethod = m.CalcMethod,
                CalcConfigJson = m.CalcConfigJson,
                Order = m.Order
            });
        }
        _db.KpiTemplateVersions.Add(draft);
        await _db.SaveChangesAsync(ct);

        return Result<KpiTemplateVersionDto>.Success(MapVersion(draft));
    }

    private static Result ValidateMetric(UpsertKpiMetricRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure("اسم المؤشر مطلوب.", "kpi_metric.name_required");
        if (request.Weight < 0 || request.Weight > 100)
            return Result.Failure("الوزن يجب أن يكون بين 0 و100.", "kpi_metric.weight_invalid");
        return Result.Success();
    }

    // ===== محرّك أولوية إسناد قوالب KPI (Phase T1) — يحاكي محرّك إسناد التقارير =====

    // مستوى أخصّية الإسناد (تنازليًّا): الأصغر رقمًا = الأخصّ.
    //
    // OBS-R5-01/2 — سلّم KPI المعتمَد نصًّا هو: موظّف ← **فريق** ← مسمّى ← إدارة ← عامّ. كان هذا
    // السلّم يخالف <see cref="CadencePriority"/> (الذي يطبّق العقد) في موضع الفريق والمسمّى، فينتج
    // تناقض مقيس: الخادم يُسند المسار إلى «إسناد الفريق» ويحسب تغطيته بقالب الفريق، بينما منتقي
    // القوالب يعرض للمُقيّم قالب المسمّى ⟹ يُنشأ التقييم بقالب غير الذي تُحسب به التغطية.
    // التوحيد هنا يخصّ KPI وحده؛ سلّم قوالب التقارير في `ReportTemplateService` خارج هذا العقد ولم يُمسّ.
    private enum MatchTier { Employee = 1, Team = 2, JobRole = 3, Department = 4, General = 5 }

    private sealed record KpiMeta(Guid Id, Guid? JobRoleId, KpiCadence Cadence);
    private sealed record UserScopes(Guid UserId, Guid? JobRoleId, Guid? TeamId, Guid? DepartmentId);
    private sealed record ResolveResult(bool Included, MatchTier Tier, string Reason);

    /// <summary>
    /// حلّ قالب KPI واحد لمستخدم واحد بترتيب الأولوية المعتمَد (الأخصّ أولًا، والاستثناء يتفوّق في
    /// مستواه وما دونه): ① استثناء موظّف ② إسناد موظّف ③ استثناء فريق ④ إسناد فريق ⑤ استثناء مسمّى
    /// ⑥ إسناد مسمّى (الصريح أو <see cref="KpiTemplate.JobRoleId"/>) ⑦ استثناء إدارة ⑧ إسناد إدارة
    /// ⑨ عام (قالب بلا مسمّى). تُرجِع null إذا كان القالب متخصصًا ولم يطابق المستخدم بأيّ مستوى.
    ///
    /// OBS-R5-01/2 — الفريق قبل المسمّى وفق نصّ العقد، وهو ترتيب الفحص نفسه المستعمَل في
    /// <see cref="CadencePriority"/> ⟹ منتقي القوالب وحاسم المسار يقرآن السلّم ذاته لا سلّمين.
    /// </summary>
    private static ResolveResult? ResolveOne(
        KpiMeta t, UserScopes u,
        HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)> assignments)
    {
        bool Has(TemplateAssignmentScope scope, Guid? id, TemplateAssignmentKind kind)
            => id is Guid g && assignments.Contains((t.Id, scope, g, kind));

        if (Has(TemplateAssignmentScope.Employee, u.UserId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Employee, "excludedManually");
        if (Has(TemplateAssignmentScope.Employee, u.UserId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Employee, "matchedByUser");

        if (Has(TemplateAssignmentScope.Team, u.TeamId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Team, "excludedManually");
        if (Has(TemplateAssignmentScope.Team, u.TeamId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Team, "matchedByTeam");

        if (Has(TemplateAssignmentScope.JobRole, u.JobRoleId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.JobRole, "excludedManually");
        if ((u.JobRoleId is Guid jr && t.JobRoleId == jr)
            || Has(TemplateAssignmentScope.JobRole, u.JobRoleId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.JobRole, "matchedByJobRole");

        if (Has(TemplateAssignmentScope.Department, u.DepartmentId, TemplateAssignmentKind.Exclude))
            return new(false, MatchTier.Department, "excludedManually");
        if (Has(TemplateAssignmentScope.Department, u.DepartmentId, TemplateAssignmentKind.Include))
            return new(true, MatchTier.Department, "matchedByDepartment");

        if (t.JobRoleId is null)
            return new(true, MatchTier.General, "matchedByGeneral");

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> ResolveAssignedTemplatesForUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return result;

        // ثلاثة استعلامات ثابتة مهما بلغ عدد المستخدمين: النطاقات + بيانات القوالب + الإسنادات.
        var users = await _db.Users.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.JobRoleId, x.TeamId, x.DepartmentId })
            .ToListAsync(ct);

        var metas = await _db.KpiTemplates.AsNoTracking()
            .Where(t => t.Status == TemplateStatus.Published && t.IsActive)
            .Select(t => new KpiMeta(t.Id, t.JobRoleId, t.Cadence))
            .ToListAsync(ct);

        var assignments = await LoadActiveAssignmentsAsync(metas.Select(m => m.Id).ToList(), ct);

        foreach (var u in users)
        {
            var scopes = new UserScopes(u.Id, u.JobRoleId, u.TeamId, u.DepartmentId);
            var included = new List<(KpiMeta Meta, MatchTier Tier)>();
            foreach (var m in metas)
            {
                // نفس دالّة الحلّ المستعملة للمستخدم الواحد — لا نسخة ثانية من المنطق.
                var r = ResolveOne(m, scopes, assignments);
                if (r is { Included: true }) included.Add((m, r.Tier));
            }

            if (included.Count == 0)
            {
                result[u.Id] = Array.Empty<Guid>();
                continue;
            }

            // «الأخصّ يطغى» غير تراكميّ — مطابق حرفيًّا لـResolveAssignedTemplateIdsAsync.
            var minTier = included.Min(x => x.Tier);
            result[u.Id] = included.Where(x => x.Tier == minTier)
                .Select(x => x.Meta.Id).Distinct().ToList();
        }
        return result;
    }

    private async Task<HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)>> LoadActiveAssignmentsAsync(
        IReadOnlyCollection<Guid> templateIds, CancellationToken ct, DateOnly? asOf = null)
    {
        if (templateIds.Count == 0)
            return new HashSet<(Guid, TemplateAssignmentScope, Guid, TemplateAssignmentKind)>();
        var q = _db.KpiTemplateAssignments.AsNoTracking()
            .Where(a => a.IsActive && templateIds.Contains(a.KpiTemplateId));

        // DEC-01/6 — سريان الإسناد: صفّ بحدود فارغة يبقى ساريًا دائمًا، فالسلوك بلا asOf مطابق حرفيًّا
        // للسابق، والأرباع التاريخيّة لا يُعاد تفسيرها بإعداد أُنشئ بعدها.
        if (asOf is DateOnly at)
            q = q.Where(a => (a.EffectiveFrom == null || a.EffectiveFrom <= at)
                             && (a.EffectiveTo == null || a.EffectiveTo >= at));

        var rows = await q.Select(a => new { a.KpiTemplateId, a.ScopeType, a.ScopeId, a.Kind }).ToListAsync(ct);
        return rows.Select(r => (r.KpiTemplateId, r.ScopeType, r.ScopeId, r.Kind)).ToHashSet();
    }

    /// <summary>
    /// ترتيب حسم مصدر المسار وفق DEC-01/5 — الأصغر يفوز، ويُقارَن <b>داخل المسار الواحد فقط</b>
    /// (OBS-R5-01). يقرأ سلّم <see cref="KpiCadenceSources.Specificity"/> نفسه الذي يقرؤه منتقي
    /// المسار الأوّليّ، فلا يوجد سلّم ثانٍ قابل للتباعد عنه.
    /// </summary>
    private static int CadencePriority(string reason) =>
        KpiCadenceSources.Specificity(CadenceSourceOf(reason));

    private static string CadenceSourceOf(string reason) => reason switch
    {
        "matchedByUser" => KpiCadenceSources.EmployeeAssignment,
        "matchedByTeam" => KpiCadenceSources.TeamAssignment,
        "matchedByJobRole" => KpiCadenceSources.JobRole,
        "matchedByDepartment" => KpiCadenceSources.DepartmentAssignment,
        _ => KpiCadenceSources.GeneralTemplate
    };

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, KpiEffectiveTracks>> ResolveEffectiveTracksAsync(
        IReadOnlyCollection<Guid> userIds, DateOnly asOf, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, KpiEffectiveTracks>();
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return result;

        var users = await _db.Users.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.JobRoleId, x.TeamId, x.DepartmentId })
            .ToListAsync(ct);

        var metas = await _db.KpiTemplates.AsNoTracking()
            .Where(t => t.Status == TemplateStatus.Published && t.IsActive)
            .Select(t => new KpiMeta(t.Id, t.JobRoleId, t.Cadence))
            .ToListAsync(ct);

        var assignments = await LoadActiveAssignmentsAsync(metas.Select(m => m.Id).ToList(), ct, asOf);

        foreach (var id in ids) result[id] = KpiEffectiveTracks.NotConfigured(id);

        foreach (var u in users)
        {
            var scopes = new UserScopes(u.Id, u.JobRoleId, u.TeamId, u.DepartmentId);

            // نفس ResolveOne المستعمَل في اختيار القالب — لا محرّك موازٍ ولا نسخة ثانية من المنطق.
            var matched = new List<(KpiMeta Meta, string Reason)>();
            foreach (var m in metas)
            {
                var r = ResolveOne(m, scopes, assignments);
                if (r is { Included: true }) matched.Add((m, r.Reason));
            }

            if (matched.Count == 0) continue;

            // OBS-R5-01 — التجميع حسب التواتر **أوّلًا**، ثمّ أدنى أولويّة **داخل كلّ مسار**.
            // لا Min عبر التواترات مجتمعةً ولا فاصل تعادل إلى الربعيّ: كلاهما كان يُخفي المسار الآخر كلّيًّا.
            result[u.Id] = new KpiEffectiveTracks(
                u.Id,
                TrackOf(u.Id, KpiCadence.WeeklyPulse, matched),
                TrackOf(u.Id, KpiCadence.Quarterly, matched));
        }

        return result;

        static KpiEffectiveCadence TrackOf(
            Guid userId, KpiCadence cadence, List<(KpiMeta Meta, string Reason)> matched)
        {
            var inTrack = matched.Where(x => x.Meta.Cadence == cadence).ToList();
            if (inTrack.Count == 0)
                return new KpiEffectiveCadence(userId, null, KpiCadenceSources.NotConfigured, Array.Empty<Guid>());

            var winningPriority = inTrack.Min(x => CadencePriority(x.Reason));
            var winners = inTrack.Where(x => CadencePriority(x.Reason) == winningPriority).ToList();

            return new KpiEffectiveCadence(
                userId,
                cadence,
                CadenceSourceOf(winners[0].Reason),
                winners.Select(w => w.Meta.Id).Distinct().ToList());
        }
    }

    /// <summary>
    /// أولوية اختيار قوالب KPI لموظّف ضمن المرشّحات الحالية (عادةً منشور/نشط/دورية محدّدة):
    /// تُحلّ كل القوالب بترتيب الأخصّية، ثم يُبقى فقط على القوالب عند أدنى مستوى أخصّية مطابق
    /// (الأخصّ يطغى، غير تراكمي): موظّف صريح > مسمّى > فريق > إدارة > عام. فلا يظهر قالب أعمّ
    /// (فريق/إدارة/عام) لمن طابق بمستوى أخصّ. عند غياب أي إسناد صريح يؤول السلوك إلى «قالب المسمّى
    /// إن وُجد وإلّا العام» (توافق خلفي تام). هذا يطابق تمامًا منطق Preview في GetAssignmentsAsync.
    /// موازنة الأخصّية تتمّ داخل المرشّحات نفسها (الدورية مُطبَّقة مسبقًا في q).
    /// </summary>
    private async Task<List<Guid>> ResolveAssignedTemplateIdsAsync(
        IQueryable<KpiTemplate> q, Guid subjectId, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking()
            .Where(x => x.Id == subjectId)
            .Select(x => new { x.JobRoleId, x.TeamId, x.DepartmentId })
            .FirstOrDefaultAsync(ct);
        var scopes = new UserScopes(subjectId, u?.JobRoleId, u?.TeamId, u?.DepartmentId);

        var metas = await q
            .Select(t => new KpiMeta(t.Id, t.JobRoleId, t.Cadence))
            .ToListAsync(ct);
        var ids = metas.Select(m => m.Id).ToList();
        var assignments = await LoadActiveAssignmentsAsync(ids, ct);

        var included = new List<(KpiMeta Meta, MatchTier Tier)>();
        foreach (var m in metas)
        {
            var r = ResolveOne(m, scopes, assignments);
            if (r is { Included: true }) included.Add((m, r.Tier));
        }

        // الأخصّ يطغى (غير تراكمي): أبقِ فقط القوالب عند أدنى مستوى أخصّية مطابق لهذا الموظّف،
        // فلا يظهر قالب فريق/إدارة لمن لديه قالب أخصّ (موظّف صريح أو مسمّى). يطابق تمامًا منطق
        // Preview في GetAssignmentsAsync (minTier) كي يتطابق المنتقي مع المعاينة.
        if (included.Count == 0) return new List<Guid>();
        var minTier = included.Min(x => x.Tier);
        var effective = included.Where(x => x.Tier == minTier).ToList();

        return effective.Select(x => x.Meta.Id).Distinct().ToList();
    }

    public async Task<Result<KpiTemplateAssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.KpiTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<KpiTemplateAssignmentsDto>.Failure("القالب غير موجود.", "kpi_template.not_found");

        var isRoleSpecific = t.JobRoleId is not null;
        var isAssignable = t.Status == TemplateStatus.Published && t.IsActive;

        var jobRoles = await _db.JobRoles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var teams = await _db.Teams.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.NameAr, ct);
        var templateJobRoleName = t.JobRoleId is { } trid ? jobRoles.GetValueOrDefault(trid) : null;

        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive, u.JobRoleId, u.TeamId, u.DepartmentId })
            .ToListAsync(ct);
        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);

        // كل قوالب KPI القابلة للاختيار (منشورة ونشطة) بنفس الدورية — لحساب «الأخصّية تسبق العام».
        var candidates = await _db.KpiTemplates.AsNoTracking()
            .Where(x => x.Status == TemplateStatus.Published && x.IsActive && x.Cadence == t.Cadence)
            .Select(x => new { x.Id, x.JobRoleId, x.Cadence })
            .ToListAsync(ct);
        var candidateMetas = candidates.Select(x => new KpiMeta(x.Id, x.JobRoleId, x.Cadence)).ToList();

        var assignmentTemplateIds = candidateMetas.Select(m => m.Id).ToList();
        if (!assignmentTemplateIds.Contains(t.Id)) assignmentTemplateIds.Add(t.Id);
        var assignments = await LoadActiveAssignmentsAsync(assignmentTemplateIds, ct);

        var thisMeta = new KpiMeta(t.Id, t.JobRoleId, t.Cadence);

        var matched = new List<KpiTemplateAssignmentUserDto>();
        var excluded = new List<KpiTemplateAssignmentUserDto>();

        foreach (var u in users)
        {
            var scopes = new UserScopes(u.Id, u.JobRoleId, u.TeamId, u.DepartmentId);
            var jobRoleName = u.JobRoleId is { } urid ? jobRoles.GetValueOrDefault(urid) : null;
            var teamName = u.TeamId is { } utid ? teams.GetValueOrDefault(utid) : null;
            var deptName = u.DepartmentId is { } udid ? depts.GetValueOrDefault(udid) : null;

            KpiTemplateAssignmentUserDto Make(string? exclusion, string? match) =>
                new(u.Id, u.FullName, u.Email, u.JobRoleId, jobRoleName, u.IsActive, exclusion, match,
                    u.TeamId, teamName, u.DepartmentId, deptName);

            if (!u.IsActive) { excluded.Add(Make("excludedBecauseInactive", null)); continue; }

            var r = ResolveOne(thisMeta, scopes, assignments);
            // عدم المطابقة لمسمّى القالب المتخصّص = «بقيّة الموظّفين»، وليست استثناءً ذا معنى للعرض.
            if (r is null) continue;
            if (!r.Included) { excluded.Add(Make(r.Reason, null)); continue; }

            // أعلى مستوى أخصّية مطابق لهذا المستخدم بين قوالب KPI بنفس الدورية.
            var userMatches = new List<MatchTier>();
            foreach (var m in candidateMetas)
            {
                var rr = ResolveOne(m, scopes, assignments);
                if (rr is { Included: true }) userMatches.Add(rr.Tier);
            }
            var minTier = userMatches.Count > 0 ? userMatches.Min() : r.Tier;

            if (r.Tier > minTier)
            {
                // يوجد قالب KPI أخصّ لهذا المستخدم بنفس الدورية ⇒ هذا القالب لا يظهر له في المنتقي.
                excluded.Add(Make("excludedBecauseMoreSpecificTemplateExists", null));
                continue;
            }

            matched.Add(Make(null, r.Reason));
        }

        // إن كان القالب غير قابل للاختيار (مسودة/مؤرشف/غير نشط) فلا أحد يستلمه فعليًّا الآن.
        if (!isAssignable)
        {
            excluded.AddRange(matched.Select(m => m with
            {
                ExclusionReason = "excludedBecauseTemplateNotAssignable",
                MatchReason = null
            }));
            matched = new List<KpiTemplateAssignmentUserDto>();
        }

        var rawRows = await _db.KpiTemplateAssignments.AsNoTracking()
            .Where(x => x.KpiTemplateId == id)
            .OrderBy(x => x.ScopeType).ThenByDescending(x => x.Kind).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.ScopeType, x.ScopeId, x.Kind, x.Notes, x.IsActive, x.CreatedAtUtc, x.EffectiveFrom, x.EffectiveTo })
            .ToListAsync(ct);

        string? ScopeName(TemplateAssignmentScope s, Guid sid) => s switch
        {
            TemplateAssignmentScope.Employee => userNames.GetValueOrDefault(sid),
            TemplateAssignmentScope.JobRole => jobRoles.GetValueOrDefault(sid),
            TemplateAssignmentScope.Team => teams.GetValueOrDefault(sid),
            TemplateAssignmentScope.Department => depts.GetValueOrDefault(sid),
            _ => null
        };

        var assignmentRows = rawRows.Select(x => new KpiTemplateAssignmentRowDto(
            x.Id, x.ScopeType, x.ScopeId, ScopeName(x.ScopeType, x.ScopeId), x.Kind, x.Notes, x.IsActive, x.CreatedAtUtc,
            x.EffectiveFrom, x.EffectiveTo))
            .ToList();

        return Result<KpiTemplateAssignmentsDto>.Success(new KpiTemplateAssignmentsDto(
            t.Id, t.Title, t.JobRoleId, templateJobRoleName, t.Cadence, t.Status, t.IsActive,
            isAssignable, isRoleSpecific,
            matched.OrderBy(m => m.FullName).ToList(),
            excluded.OrderBy(m => m.FullName).ToList(),
            assignmentRows));
    }

    public async Task<Result<KpiTemplateAssignmentRowDto>> AddAssignmentAsync(
        Guid templateId, CreateKpiAssignmentRequest request, CancellationToken ct = default)
    {
        var template = await _db.KpiTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return Result<KpiTemplateAssignmentRowDto>.Failure("القالب غير موجود.", "kpi_template.not_found");

        var (exists, name) = await ResolveScopeAsync(request.ScopeType, request.ScopeId, ct);
        if (!exists)
            return Result<KpiTemplateAssignmentRowDto>.Failure("الكيان المُسنَد إليه غير موجود.", "kpi_assignment.scope_not_found");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // DEC-01/6 — مدى سريان مقلوب لا معنى له تجاريًّا؛ يُرفَض صراحةً بدل أن يُخزَّن ويُنتج نافذة خالية صامتة.
        if (request.EffectiveFrom is DateOnly f && request.EffectiveTo is DateOnly t && f > t)
            return Result<KpiTemplateAssignmentRowDto>.Failure(
                "تاريخ بداية السريان يجب ألّا يتجاوز تاريخ نهايته.", "kpi_assignment.effective_range.invalid");

        // منع التكرار لنفس (القالب/المستوى/المعرّف/النوع)؛ إن وُجد صفّ معطّل أعِد تفعيله بدل إنشاء جديد.
        var dup = await _db.KpiTemplateAssignments.FirstOrDefaultAsync(a =>
            a.KpiTemplateId == templateId && a.ScopeType == request.ScopeType &&
            a.ScopeId == request.ScopeId && a.Kind == request.Kind, ct);
        if (dup is not null)
        {
            if (dup.IsActive)
                return Result<KpiTemplateAssignmentRowDto>.Failure(
                    "هذا الإسناد/الاستثناء موجود بالفعل لنفس الكيان.", "kpi_assignment.duplicate.conflict");
            dup.IsActive = true;
            dup.Notes = notes;
            dup.EffectiveFrom = request.EffectiveFrom;
            dup.EffectiveTo = request.EffectiveTo;
            dup.UpdatedById = _currentUser.UserId;
            dup.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await LogAssignmentAsync("kpi_template.assignment.enabled", dup, templateId, ct);
            return Result<KpiTemplateAssignmentRowDto>.Success(MapAssignment(dup, name));
        }

        var row = new KpiTemplateAssignment
        {
            KpiTemplateId = templateId,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            Kind = request.Kind,
            Notes = notes,
            IsActive = true,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedById = _currentUser.UserId
        };
        _db.KpiTemplateAssignments.Add(row);
        await _db.SaveChangesAsync(ct);
        await LogAssignmentAsync("kpi_template.assignment.added", row, templateId, ct);
        return Result<KpiTemplateAssignmentRowDto>.Success(MapAssignment(row, name));
    }

    public async Task<Result<KpiTemplateAssignmentRowDto>> UpdateAssignmentAsync(
        Guid templateId, Guid assignmentId, UpdateKpiAssignmentRequest request, CancellationToken ct = default)
    {
        var row = await _db.KpiTemplateAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.KpiTemplateId == templateId, ct);
        if (row is null) return Result<KpiTemplateAssignmentRowDto>.Failure("الإسناد غير موجود.", "kpi_assignment.not_found");

        row.IsActive = request.IsActive;
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.UpdatedById = _currentUser.UserId;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogAssignmentAsync(row.IsActive ? "kpi_template.assignment.enabled" : "kpi_template.assignment.disabled", row, templateId, ct);
        var (_, name) = await ResolveScopeAsync(row.ScopeType, row.ScopeId, ct);
        return Result<KpiTemplateAssignmentRowDto>.Success(MapAssignment(row, name));
    }

    public async Task<Result> RemoveAssignmentAsync(Guid templateId, Guid assignmentId, CancellationToken ct = default)
    {
        var row = await _db.KpiTemplateAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.KpiTemplateId == templateId, ct);
        if (row is null) return Result.Failure("الإسناد غير موجود.", "kpi_assignment.not_found");

        _db.KpiTemplateAssignments.Remove(row);
        await _db.SaveChangesAsync(ct);
        await LogAssignmentAsync("kpi_template.assignment.removed", row, templateId, ct);
        return Result.Success();
    }

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

    private async Task LogAssignmentAsync(string action, KpiTemplateAssignment row, Guid templateId, CancellationToken ct)
        => await _audit.LogAsync(_currentUser.UserId, action, nameof(KpiTemplateAssignment), row.Id,
            $"{{\"templateId\":\"{templateId}\",\"scopeType\":\"{row.ScopeType}\",\"scopeId\":\"{row.ScopeId}\",\"kind\":\"{row.Kind}\",\"isActive\":{row.IsActive.ToString().ToLowerInvariant()}}}",
            ct: ct);

    // DEC-01/6 — تاريخا السريان جزءٌ من العقد لا تفصيل تخزينيّ: بدونهما في الاستجابة
    // لا يستطيع المستخدم التحقّق من أنّ الربع التاريخيّ لم يُعَد تفسيره بإعداد جديد.
    private static KpiTemplateAssignmentRowDto MapAssignment(KpiTemplateAssignment a, string? name)
        => new(a.Id, a.ScopeType, a.ScopeId, name, a.Kind, a.Notes, a.IsActive, a.CreatedAtUtc,
            a.EffectiveFrom, a.EffectiveTo);

    private async Task<KpiTemplateDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.KpiTemplates.AsNoTracking()
            .Include(x => x.Versions).ThenInclude(v => v.Metrics)
            .FirstAsync(x => x.Id == id, ct);

        var versions = t.Versions.OrderBy(v => v.VersionNumber).Select(MapVersion).ToList();
        return new KpiTemplateDetailDto(t.Id, t.Title, t.Description, t.JobRoleId,
            t.Cadence, t.Status, t.OwnerId, t.IsActive, versions);
    }

    private static KpiTemplateVersionDto MapVersion(KpiTemplateVersion v) => new(
        v.Id, v.VersionNumber, v.IsPublished, v.PublishedAtUtc,
        v.Metrics.Sum(m => m.Weight),
        v.Metrics.OrderBy(m => m.Order).Select(MapMetric).ToList());

    private static KpiMetricDto MapMetric(KpiMetric m) => new(
        m.Id, m.Name, m.Description, m.Order, m.Weight, m.TargetValue, m.Unit, m.CalcMethod, m.CalcConfigJson);
}

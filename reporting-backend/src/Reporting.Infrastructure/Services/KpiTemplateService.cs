using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class KpiTemplateService : IKpiTemplateService
{
    private readonly AppDbContext _db;

    public KpiTemplateService(AppDbContext db) => _db = db;

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

        // SubjectUserId: أولوية اختيار قالب KPI في مسار إنشاء تقييم الأداء العادي.
        // القاعدة: إذا وُجد قالب متخصص مطابق لمسمّى الموظّف الوظيفي (JobRoleId == subject.JobRoleId)
        // نُرجع القالب المتخصص وحده ولا نُظهر القالب العام معه (منعًا للازدواج والتشويش).
        // وإلا — لا قالب متخصص مناسب — نُرجع القوالب العامّة فقط (JobRoleId == null).
        if (filter.SubjectUserId is { } subjectId)
        {
            var subjectJobRoleId = await _db.Users.AsNoTracking()
                .Where(u => u.Id == subjectId)
                .Select(u => u.JobRoleId)
                .FirstOrDefaultAsync(ct);

            var hasRoleSpecific = subjectJobRoleId is { } roleId
                && await q.AnyAsync(t => t.JobRoleId == roleId, ct);

            q = hasRoleSpecific
                ? q.Where(t => t.JobRoleId == subjectJobRoleId)
                : q.Where(t => t.JobRoleId == null);
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

        // حارس الدورية (Phase 4): تقييم KPI الحالي أسبوعي فقط. يُسمح بأي دورية في المسودّة،
        // لكن لا يُنشَر/يُفعَّل قالب KPI غير أسبوعي. التجميع الشهري/الربع سنوي/السنوي يأتي في Phase 5.
        if (template.Cadence != KpiCadence.WeeklyPulse)
            return Result<KpiTemplateVersionDto>.Failure(
                "تقييم KPI الحالي أسبوعي فقط. التجميع الشهري والربع سنوي والسنوي سيتم في Phase 5؛ لا يمكن نشر قالب KPI بدورية غير أسبوعية.",
                "kpi_version.cadence_not_weekly.conflict");

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

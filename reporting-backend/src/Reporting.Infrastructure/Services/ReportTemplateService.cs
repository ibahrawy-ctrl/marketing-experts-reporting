using Microsoft.EntityFrameworkCore;
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

    public ReportTemplateService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
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
            // عامًّا ليرى الكل — يجب أن يكون الموظّف ضمن نطاق رؤيته، ثم تُطبَّق أولوية دور الموظّف.
            var scope = await _scope.ResolveAsync(ct);
            if (!scope.Contains(subjectId))
                return Result<IReadOnlyList<ReportTemplateDto>>.Failure(
                    "لا تملك صلاحية إنشاء تقرير بالنيابة عن هذا الموظّف.", "auth.forbidden");
            q = await ApplyOwnerPriorityAsync(q, subjectId, ct);
        }
        else if (filter.AssignedOnly)
        {
            // إنشاء «تقريري»: صاحب التقرير هو المستخدم الحالي. تُطبَّق أولوية الدور حتى لمن يملك
            // صلاحية إدارة القوالب (مدير عام/أدمن) — في هذا المسار يرى قالب دوره فقط لا الكل.
            q = _currentUser.UserId is { } selfId
                ? await ApplyOwnerPriorityAsync(q, selfId, ct)
                : q.Where(t => t.JobRoleId == null);
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

    /// <summary>
    /// أولوية اختيار قالب التقرير لصاحب التقرير (الأخص أولًا): إن وُجد — ضمن المرشّحات الحالية
    /// (منشور/نشط) — قالبٌ مربوط بمسمّاه الوظيفي يُرجَع وحده؛ وإلّا تُرجَع القوالب العامّة فقط.
    /// الترتيب الكامل (فردي ⟶ قيادي ⟶ مسمّى وظيفي ⟶ إدارة ⟶ عام) مُختزَل هنا إلى «دور صاحب
    /// التقرير أو العام» لأنّ القيادة ممثَّلة بمسمّى وظيفي قيادي (قائد فريق B2C، مدير التخطيط…).
    /// لا يُخلَط العام مع المتخصص ولا تظهر قوالب أدوار أخرى.
    /// </summary>
    private async Task<IQueryable<ReportTemplate>> ApplyOwnerPriorityAsync(
        IQueryable<ReportTemplate> q, Guid subjectId, CancellationToken ct)
    {
        var subjectJobRoleId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == subjectId)
            .Select(u => u.JobRoleId)
            .FirstOrDefaultAsync(ct);
        var hasRoleSpecific = subjectJobRoleId is { } roleId && await q.AnyAsync(t => t.JobRoleId == roleId, ct);
        return hasRoleSpecific
            ? q.Where(t => t.JobRoleId == subjectJobRoleId)
            : q.Where(t => t.JobRoleId == null);
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
        return Result.Success();
    }

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

    private async Task<ReportTemplateDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.ReportTemplates.AsNoTracking()
            .Include(x => x.Versions).ThenInclude(v => v.Fields)
            .FirstAsync(x => x.Id == id, ct);

        var versions = t.Versions.OrderBy(v => v.VersionNumber).Select(MapVersion).ToList();
        return new ReportTemplateDetailDto(t.Id, t.Title, t.Description, t.JobRoleId,
            t.DefaultPeriodType, t.Status, t.OwnerId, t.IsActive, t.Classification, versions);
    }

    private static TemplateVersionDto MapVersion(ReportTemplateVersion v) => new(
        v.Id, v.VersionNumber, v.IsPublished, v.PublishedAtUtc,
        v.Fields.OrderBy(f => f.Order).Select(MapField).ToList());

    private static TemplateFieldDto MapField(TemplateField f) => new(
        f.Id, f.Label, f.Key, f.FieldType, f.Order, f.IsRequired, f.HelpText, f.ConfigJson);
}

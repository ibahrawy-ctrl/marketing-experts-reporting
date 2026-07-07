using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Services;
using Reporting.Domain.Entities.Services;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة كتالوج خدمات B2B. كل التحقّقات رسائل عربية. إضافة بحتة — لا تمسّ التقارير/القوالب القائمة.
/// توحيد الاسم: NameAr فريد (تجاهل حالة الأحرف/المسافات الطرفية) لمنع التكرار المسبِّب لازدواج التجميع.
/// </summary>
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ServiceCatalogService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ServiceDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Services.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        var list = await q.OrderBy(s => s.SortOrder).ThenBy(s => s.NameAr).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<Result<ServiceDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<ServiceDto>.Failure("الخدمة غير موجودة.", "service.not_found");
        return Result<ServiceDto>.Success(Map(s));
    }

    public async Task<Result<ServiceDto>> CreateAsync(CreateServiceRequest req, Guid actorId, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<ServiceDto>.Failure("اسم الخدمة بالعربية مطلوب.", "service.name_required");
        if (await _db.Services.AnyAsync(s => s.NameAr.ToLower() == nameAr.ToLower(), ct))
            return Result<ServiceDto>.Failure("اسم الخدمة مستخدم بالفعل.", "service.name_duplicate.conflict");

        var entity = new Service
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            SortOrder = req.SortOrder,
            IsActive = true,
        };
        _db.Services.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "service.created", "Service", entity.Id,
            JsonSerializer.Serialize(new { entity.NameAr, entity.NameEn, entity.SortOrder }), null, ct);
        return Result<ServiceDto>.Success(Map(entity));
    }

    public async Task<Result<ServiceDto>> UpdateAsync(Guid id, UpdateServiceRequest req, Guid actorId, CancellationToken ct = default)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<ServiceDto>.Failure("الخدمة غير موجودة.", "service.not_found");
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<ServiceDto>.Failure("اسم الخدمة بالعربية مطلوب.", "service.name_required");
        if (await _db.Services.AnyAsync(x => x.NameAr.ToLower() == nameAr.ToLower() && x.Id != id, ct))
            return Result<ServiceDto>.Failure("اسم الخدمة مستخدم بالفعل.", "service.name_duplicate.conflict");

        var old = new { s.NameAr, s.NameEn, s.SortOrder };
        s.NameAr = nameAr;
        s.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        s.SortOrder = req.SortOrder;
        s.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "service.updated", "Service", s.Id,
            JsonSerializer.Serialize(new { old, @new = new { s.NameAr, s.NameEn, s.SortOrder } }), null, ct);
        return Result<ServiceDto>.Success(Map(s));
    }

    public async Task<Result<ServiceDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<ServiceDto>.Failure("الخدمة غير موجودة.", "service.not_found");
        if (s.IsActive == isActive)
            return Result<ServiceDto>.Failure(isActive ? "الخدمة مُفعّلة بالفعل." : "الخدمة معطّلة بالفعل.", "service.state_unchanged.conflict");
        s.IsActive = isActive;
        s.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, isActive ? "service.activated" : "service.deactivated", "Service", s.Id,
            JsonSerializer.Serialize(new { s.NameAr, isActive }), null, ct);
        return Result<ServiceDto>.Success(Map(s));
    }

    public async Task<Result<ServiceDeleteResult>> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<ServiceDeleteResult>.Failure("الخدمة غير موجودة.", "service.not_found");

        var used = await IsServiceUsedInReportsAsync(s.NameAr, ct);
        if (used)
        {
            // مُستخدَمة في تقارير قائمة ⇒ أرشفة (تعطيل) لا حذف نهائيّ؛ التقارير القديمة تبقى صالحة (لقطة نصّية).
            var wasActive = s.IsActive;
            s.IsActive = false;
            s.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(actorId, "service.archived", "Service", s.Id,
                JsonSerializer.Serialize(new { s.NameAr, reason = "used_in_reports", wasActive }), null, ct);
            return Result<ServiceDeleteResult>.Success(new ServiceDeleteResult(
                false, Map(s),
                "الخدمة مستخدَمة في تقارير قائمة، فتمّت أرشفتها (تعطيلها) بدل الحذف النهائي. لن تظهر في التقارير الجديدة، وتبقى التقارير القديمة كما هي."));
        }

        // غير مستخدَمة ⇒ حذف نهائيّ آمن.
        var nameAr = s.NameAr;
        _db.Services.Remove(s);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "service.deleted", "Service", id,
            JsonSerializer.Serialize(new { nameAr }), null, ct);
        return Result<ServiceDeleteResult>.Success(new ServiceDeleteResult(
            true, null, "تمّ حذف الخدمة نهائيًّا (غير مستخدَمة في أي تقرير)."));
    }

    /// <summary>
    /// هل اسم الخدمة مستخدَم كنصّ في أي تقرير مبيعات B2B حسب الخدمة؟ (مقارنة الاسم في عمود «الخدمة» تجاهلًا لحالة الأحرف/المسافات).
    /// الخدمات تُخزَّن كلقطة نصّية في جدول التقرير (لا FK)، لذا الاستخدام يُكتشَف بمسح خلايا الجدول.
    /// </summary>
    private async Task<bool> IsServiceUsedInReportsAsync(string nameAr, CancellationToken ct)
    {
        var target = (nameAr ?? string.Empty).Trim();
        if (target.Length == 0) return false;

        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == B2bByServiceReportSchema.TemplateTitle, ct);
        if (template is null) return false;

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var gridFieldIds = template.Versions
            .SelectMany(v => v.Fields)
            .Where(f => f.FieldType == Domain.Enums.FieldType.TableGrid
                        && f.Label == B2bByServiceReportSchema.MainTableLabel)
            .Select(f => f.Id)
            .ToHashSet();
        if (gridFieldIds.Count == 0) return false;

        var iService = Array.IndexOf(B2bByServiceReportSchema.Columns, B2bByServiceReportSchema.ColService);
        if (iService < 0) return false;

        var subIds = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId)
                        && s.Status != Domain.Enums.SubmissionStatus.Draft)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (subIds.Count == 0) return false;

        var gridJsons = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId)
                        && gridFieldIds.Contains(v.TemplateFieldId)
                        && v.ValueJson != null)
            .Select(v => v.ValueJson!)
            .ToListAsync(ct);

        foreach (var json in gridJsons)
        {
            string[][]? rows;
            try { rows = JsonSerializer.Deserialize<string[][]>(json); }
            catch (JsonException) { continue; } // جدول غير قابل للقراءة (توافق خلفي) ⇒ يُتجاهَل.
            if (rows is null) continue;
            foreach (var row in rows)
            {
                if (row is null || iService >= row.Length) continue;
                var cell = row[iService]?.Trim();
                if (!string.IsNullOrEmpty(cell) && string.Equals(cell, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static ServiceDto Map(Service s) =>
        new(s.Id, s.NameAr, s.NameEn, s.IsActive, s.SortOrder, s.CreatedAtUtc, s.UpdatedAtUtc);
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Courses;
using Reporting.Domain.Entities.Courses;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة كتالوج الدورات. كل التحقّقات رسائل عربية. إضافة بحتة — لا تمسّ التقارير/القوالب القائمة.
/// توحيد الاسم: NameAr فريد (تجاهل حالة الأحرف/المسافات الطرفية) لمنع التكرار المسبِّب لازدواج التجميع.
/// </summary>
public class CourseService : ICourseService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CourseService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<CourseDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Courses.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        var list = await q.OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<Result<CourseDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CourseDto>.Failure("الدورة غير موجودة.", "course.not_found");
        return Result<CourseDto>.Success(Map(c));
    }

    public async Task<Result<CourseDto>> CreateAsync(CreateCourseRequest req, Guid actorId, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<CourseDto>.Failure("اسم الدورة بالعربية مطلوب.", "course.name_required");
        if (await _db.Courses.AnyAsync(c => c.NameAr.ToLower() == nameAr.ToLower(), ct))
            return Result<CourseDto>.Failure("اسم الدورة مستخدم بالفعل.", "course.name_duplicate.conflict");

        var entity = new Course
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            SortOrder = req.SortOrder,
            IsActive = true,
        };
        _db.Courses.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "course.created", "Course", entity.Id,
            JsonSerializer.Serialize(new { entity.NameAr, entity.NameEn, entity.SortOrder }), null, ct);
        return Result<CourseDto>.Success(Map(entity));
    }

    public async Task<Result<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest req, Guid actorId, CancellationToken ct = default)
    {
        var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CourseDto>.Failure("الدورة غير موجودة.", "course.not_found");
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<CourseDto>.Failure("اسم الدورة بالعربية مطلوب.", "course.name_required");
        if (await _db.Courses.AnyAsync(x => x.NameAr.ToLower() == nameAr.ToLower() && x.Id != id, ct))
            return Result<CourseDto>.Failure("اسم الدورة مستخدم بالفعل.", "course.name_duplicate.conflict");

        var old = new { c.NameAr, c.NameEn, c.SortOrder };
        c.NameAr = nameAr;
        c.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        c.SortOrder = req.SortOrder;
        c.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "course.updated", "Course", c.Id,
            JsonSerializer.Serialize(new { old, @new = new { c.NameAr, c.NameEn, c.SortOrder } }), null, ct);
        return Result<CourseDto>.Success(Map(c));
    }

    public async Task<Result<CourseDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CourseDto>.Failure("الدورة غير موجودة.", "course.not_found");
        if (c.IsActive == isActive)
            return Result<CourseDto>.Failure(isActive ? "الدورة مُفعّلة بالفعل." : "الدورة معطّلة بالفعل.", "course.state_unchanged.conflict");
        c.IsActive = isActive;
        c.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, isActive ? "course.activated" : "course.deactivated", "Course", c.Id,
            JsonSerializer.Serialize(new { c.NameAr, isActive }), null, ct);
        return Result<CourseDto>.Success(Map(c));
    }

    public async Task<Result<CourseDeleteResult>> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CourseDeleteResult>.Failure("الدورة غير موجودة.", "course.not_found");

        var used = await IsCourseUsedInReportsAsync(c.NameAr, ct);
        if (used)
        {
            // مُستخدَمة في تقارير قائمة ⇒ أرشفة (تعطيل) لا حذف نهائيّ؛ التقارير القديمة تبقى صالحة (لقطة نصّية).
            var wasActive = c.IsActive;
            c.IsActive = false;
            c.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(actorId, "course.archived", "Course", c.Id,
                JsonSerializer.Serialize(new { c.NameAr, reason = "used_in_reports", wasActive }), null, ct);
            return Result<CourseDeleteResult>.Success(new CourseDeleteResult(
                false, Map(c),
                "الدورة مستخدَمة في تقارير قائمة، فتمّت أرشفتها (تعطيلها) بدل الحذف النهائي. لن تظهر في التقارير الجديدة، وتبقى التقارير القديمة كما هي."));
        }

        // غير مستخدَمة ⇒ حذف نهائيّ آمن.
        var nameAr = c.NameAr;
        _db.Courses.Remove(c);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "course.deleted", "Course", id,
            JsonSerializer.Serialize(new { nameAr }), null, ct);
        return Result<CourseDeleteResult>.Success(new CourseDeleteResult(
            true, null, "تمّ حذف الدورة نهائيًّا (غير مستخدَمة في أي تقرير)."));
    }

    /// <summary>
    /// هل اسم الدورة مستخدَم كنصّ في أي تقرير مبيعات B2C حسب الدورة؟ (مقارنة الاسم في عمود «الدورة» تجاهلًا لحالة الأحرف/المسافات).
    /// الدورات تُخزَّن كلقطة نصّية في جدول التقرير (لا FK)، لذا الاستخدام يُكتشَف بمسح خلايا الجدول.
    /// </summary>
    private async Task<bool> IsCourseUsedInReportsAsync(string nameAr, CancellationToken ct)
    {
        var target = (nameAr ?? string.Empty).Trim();
        if (target.Length == 0) return false;

        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == B2cByCourseReportSchema.TemplateTitle, ct);
        if (template is null) return false;

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var gridFieldIds = template.Versions
            .SelectMany(v => v.Fields)
            .Where(f => f.FieldType == Domain.Enums.FieldType.TableGrid
                        && f.Label == B2cByCourseReportSchema.MainTableLabel)
            .Select(f => f.Id)
            .ToHashSet();
        if (gridFieldIds.Count == 0) return false;

        var iCourse = Array.IndexOf(B2cByCourseReportSchema.Columns, B2cByCourseReportSchema.ColCourse);
        if (iCourse < 0) return false;

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
                if (row is null || iCourse >= row.Length) continue;
                var cell = row[iCourse]?.Trim();
                if (!string.IsNullOrEmpty(cell) && string.Equals(cell, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static CourseDto Map(Course c) =>
        new(c.Id, c.NameAr, c.NameEn, c.IsActive, c.SortOrder, c.CreatedAtUtc, c.UpdatedAtUtc);
}

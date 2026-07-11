using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.ExecutionTaxonomy;
using Reporting.Domain.Entities.ExecutionTaxonomy;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة كتالوج تصنيفات التنفيذ (RC-4 Task 4D2). كل الرسائل عربية. إضافة بحتة — لا تمسّ القوالب/التقارير القائمة.
/// Domain و Code غير قابلين للتعديل (مفتاح التفرّد المنطقيّ + مرجع اللقطات). التعطيل بدل الحذف.
/// </summary>
public class ExecutionTaxonomyService : IExecutionTaxonomyService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ExecutionTaxonomyService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // المجالات الثابتة المعروفة (تُدار برمجيًّا — لا إنشاء مجالات جديدة من الواجهة).
    // الثلاثة عشر الأولى = قوالب التنفيذ v3/v4. الستة الأخيرة = P0 منصّة التنفيذ العامة.
    private static readonly HashSet<string> KnownDomains = new(StringComparer.Ordinal)
    {
        "content_type", "content_goal", "work_status",
        "design_type", "design_status", "design_tool",
        "video_type", "edit_type", "video_duration", "video_status",
        "activity_type", "interaction_result", "response_time",
        // P0 — منصّة التنفيذ العامة
        "workstream_type", "deliverable", "usage_context",
        "workflow_step", "delay_reason", "platform_channel",
    };

    public async Task<IReadOnlyList<ExecutionTaxonomyDto>> ListAsync(string? domain, bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.ExecutionTaxonomyValues.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(v => v.IsActive);
        var normalizedDomain = domain?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDomain)) q = q.Where(v => v.Domain == normalizedDomain);
        var list = await q
            .OrderBy(v => v.Domain).ThenBy(v => v.SortOrder).ThenBy(v => v.NameAr)
            .ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    // RC-4 Task 4D3 — خيارات نشطة لمجال (NameAr مرتّبة). فارغة لمجال غير معروف/بلا قيم نشطة (لا خطأ).
    public async Task<IReadOnlyList<string>> ListActiveOptionsAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = (domain ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedDomain)) return Array.Empty<string>();
        return await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == normalizedDomain)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.NameAr)
            .Select(v => v.NameAr)
            .ToListAsync(ct);
    }

    // P1 — خيارات نشطة مُفصَّلة (Code + NameAr) لمجال واحد. فارغة لمجال غير معروف/بلا قيم نشطة (لا خطأ).
    public async Task<IReadOnlyList<ExecutionTaxonomyOptionDto>> ListActiveOptionDetailsAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = (domain ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedDomain)) return Array.Empty<ExecutionTaxonomyOptionDto>();
        return await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == normalizedDomain)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.NameAr)
            .Select(v => new ExecutionTaxonomyOptionDto(v.Code, v.NameAr))
            .ToListAsync(ct);
    }

    public async Task<Result<ExecutionTaxonomyDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var v = await _db.ExecutionTaxonomyValues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result<ExecutionTaxonomyDto>.Failure("قيمة التصنيف غير موجودة.", "execution_taxonomy.not_found");
        return Result<ExecutionTaxonomyDto>.Success(Map(v));
    }

    public async Task<Result<ExecutionTaxonomyDto>> CreateAsync(CreateExecutionTaxonomyRequest req, Guid actorId, CancellationToken ct = default)
    {
        var domain = (req.Domain ?? string.Empty).Trim();
        var code = (req.Code ?? string.Empty).Trim();
        var nameAr = (req.NameAr ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(domain))
            return Result<ExecutionTaxonomyDto>.Failure("المجال (Domain) مطلوب.", "execution_taxonomy.domain_required");
        if (!KnownDomains.Contains(domain))
            return Result<ExecutionTaxonomyDto>.Failure("المجال (Domain) غير معروف — لا يُسمح بإنشاء مجالات جديدة من الواجهة.", "execution_taxonomy.domain_invalid");
        if (string.IsNullOrWhiteSpace(code))
            return Result<ExecutionTaxonomyDto>.Failure("الرمز (Code) مطلوب.", "execution_taxonomy.code_required");
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<ExecutionTaxonomyDto>.Failure("الاسم بالعربية مطلوب.", "execution_taxonomy.name_required");

        // تفرّد (Domain, Code) تجاهلًا لحالة الأحرف — قبل الوصول لاستثناء الفهرس الفريد بالقاعدة.
        if (await _db.ExecutionTaxonomyValues.AnyAsync(v => v.Domain == domain && v.Code.ToLower() == code.ToLower(), ct))
            return Result<ExecutionTaxonomyDto>.Failure("يوجد رمز (Code) مطابق في هذا المجال بالفعل.", "execution_taxonomy.duplicate.conflict");

        var entity = new ExecutionTaxonomyValue
        {
            Domain = domain,
            Code = code,
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            SortOrder = req.SortOrder,
            IsActive = true,
        };
        _db.ExecutionTaxonomyValues.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "execution_taxonomy.created", "ExecutionTaxonomyValue", entity.Id,
            JsonSerializer.Serialize(new { entity.Domain, entity.Code, entity.NameAr, entity.NameEn, entity.SortOrder }), null, ct);
        return Result<ExecutionTaxonomyDto>.Success(Map(entity));
    }

    public async Task<Result<ExecutionTaxonomyDto>> UpdateAsync(Guid id, UpdateExecutionTaxonomyRequest req, Guid actorId, CancellationToken ct = default)
    {
        var v = await _db.ExecutionTaxonomyValues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result<ExecutionTaxonomyDto>.Failure("قيمة التصنيف غير موجودة.", "execution_taxonomy.not_found");
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<ExecutionTaxonomyDto>.Failure("الاسم بالعربية مطلوب.", "execution_taxonomy.name_required");

        // Domain و Code لا يتغيّران — يُعدَّل الاسم والترتيب فقط.
        var old = new { v.NameAr, v.NameEn, v.SortOrder };
        v.NameAr = nameAr;
        v.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        v.SortOrder = req.SortOrder;
        v.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "execution_taxonomy.updated", "ExecutionTaxonomyValue", v.Id,
            JsonSerializer.Serialize(new { v.Domain, v.Code, old, @new = new { v.NameAr, v.NameEn, v.SortOrder } }), null, ct);
        return Result<ExecutionTaxonomyDto>.Success(Map(v));
    }

    public async Task<Result<ExecutionTaxonomyDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        var v = await _db.ExecutionTaxonomyValues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result<ExecutionTaxonomyDto>.Failure("قيمة التصنيف غير موجودة.", "execution_taxonomy.not_found");
        if (v.IsActive == isActive)
            return Result<ExecutionTaxonomyDto>.Failure(isActive ? "القيمة مُفعّلة بالفعل." : "القيمة معطّلة بالفعل.", "execution_taxonomy.state_unchanged.conflict");
        v.IsActive = isActive;
        v.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, isActive ? "execution_taxonomy.activated" : "execution_taxonomy.deactivated", "ExecutionTaxonomyValue", v.Id,
            JsonSerializer.Serialize(new { v.Domain, v.Code, isActive }), null, ct);
        return Result<ExecutionTaxonomyDto>.Success(Map(v));
    }

    private static ExecutionTaxonomyDto Map(ExecutionTaxonomyValue v) =>
        new(v.Id, v.Domain, v.Code, v.NameAr, v.NameEn, v.IsActive, v.SortOrder, v.CreatedAtUtc, v.UpdatedAtUtc);
}

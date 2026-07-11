using Reporting.Application.Common;

namespace Reporting.Application.ExecutionTaxonomy;

/// <summary>
/// إدارة كتالوج تصنيفات التنفيذ (RC-4 Task 4D2). قراءة/إنشاء/تعديل/تفعيل/تعطيل فقط — لا حذف نهائيّ.
/// الجدول والكيان والهجرة والبذر موجودة من Task 4D1 — لا هجرة جديدة.
/// </summary>
public interface IExecutionTaxonomyService
{
    // domain اختياريّ (فلترة بمجال واحد)، includeInactive يُظهر المعطّلة (للإدارة).
    Task<IReadOnlyList<ExecutionTaxonomyDto>> ListAsync(string? domain, bool includeInactive, CancellationToken ct = default);

    // RC-4 Task 4D3 — خيارات نشطة لمجال واحد (NameAr مرتّبة SortOrder ثم NameAr) لملء التقارير.
    // قراءة خفيفة آمنة للمستخدمين المصادَقين (لا تكشف إدارة) — نفس شكل options اللقطة (string[]).
    Task<IReadOnlyList<string>> ListActiveOptionsAsync(string domain, CancellationToken ct = default);

    // P1 — خيارات نشطة مُفصَّلة (Code + NameAr) لمجال واحد (مرتّبة SortOrder ثم NameAr).
    // تُستخدم في القوائم المنسدلة التي ترسل الرمز (نوع تيار العمل). قراءة خفيفة للمصادَقين، لا تكشف إدارة.
    Task<IReadOnlyList<ExecutionTaxonomyOptionDto>> ListActiveOptionDetailsAsync(string domain, CancellationToken ct = default);
    Task<Result<ExecutionTaxonomyDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExecutionTaxonomyDto>> CreateAsync(CreateExecutionTaxonomyRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<ExecutionTaxonomyDto>> UpdateAsync(Guid id, UpdateExecutionTaxonomyRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<ExecutionTaxonomyDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default);
}

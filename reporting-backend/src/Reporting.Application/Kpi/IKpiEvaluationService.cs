using Reporting.Application.Common;

namespace Reporting.Application.Kpi;

/// <summary>تقييمات KPI لفترة: إدخال نتائج المؤشرات، احتساب الدرجة المرجّحة، الاتجاه، ودورة الاعتماد.</summary>
public interface IKpiEvaluationService
{
    Task<Result<KpiEvaluationDto>> CreateOrGetAsync(CreateKpiEvaluationRequest request, CancellationToken ct = default);

    /// <summary>قائمة الموظّفين الذين يحقّ للمستخدم الحالي إنشاء تقييم KPI لهم (مرؤوسوه المباشرون، أو الكل للأدمن).</summary>
    Task<Result<EvaluatableSubjectsDto>> GetEvaluatableSubjectsAsync(CancellationToken ct = default);
    Task<Result<KpiEvaluationDto>> GetAsync(Guid evaluationId, CancellationToken ct = default);
    Task<Result<KpiEvaluationDto>> SaveResultsAsync(Guid evaluationId, SaveKpiResultsRequest request, CancellationToken ct = default);
    Task<Result<KpiEvaluationDto>> SubmitAsync(Guid evaluationId, CancellationToken ct = default);
    Task<Result<KpiEvaluationDto>> ApproveAsync(Guid evaluationId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<KpiEvaluationListItemDto>>> ListAsync(KpiEvaluationFilter filter, CancellationToken ct = default);
    Task<Result<IReadOnlyList<KpiEvaluationListItemDto>>> ListForSubjectAsync(Guid subjectUserId, CancellationToken ct = default);

    /// <summary>
    /// تجميع KPI الدوري (Phase 5 §8): الأسبوع وحدة الأساس، والمتوسط الشهري/الربع سنوي/السنوي/المخصّص
    /// يُحسب كمتوسط نتائج الأسابيع داخل المدى. مقيَّد خادميًّا بنطاق المستخدم الحالي (ScopeResolver).
    /// </summary>
    Task<Result<KpiAggregateDto>> GetAggregateAsync(KpiAggregateRequest request, CancellationToken ct = default);

    /// <summary>
    /// تصدير KPI للمالية (KPI-FIN1): معاينة صفوف التقييمات الأسبوعية المعتمدة الواقعة داخل الربع المختار،
    /// على مستوى الشركة (بلا ScopeResolver؛ النطاق مفروض بالسياسة). قراءة فقط، لا تغيّر أيّ تقييم.
    /// </summary>
    Task<Result<KpiFinanceExportDto>> GetFinanceExportAsync(KpiFinanceExportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تصدير KPI للمالية بصيغة CSV (UTF-8 مع BOM لدعم العربية في Excel). يسجّل حدث تدقيق
    /// kpi.finance_exported (بلا أسماء/درجات). قراءة فقط، لا تغيّر أيّ تقييم.
    /// </summary>
    Task<Result<byte[]>> ExportFinanceCsvAsync(KpiFinanceExportFilter filter, CancellationToken ct = default);
}

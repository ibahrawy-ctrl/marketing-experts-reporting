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

    // ── ADMIN-GOVERNANCE-R1: مسار مراجعة/اعتماد تقييمات KPI ──

    /// <summary>
    /// طلب مراجعة (NeedsRevision): يعيد التقييم من UnderReview إلى NeedsRevision مع سبب إلزاميّ،
    /// يُنشئ حدث مراجعة + لقطة، يُخطر المُقيّم لإعادة الإدخال. لا يُغيّر الدرجة. صلاحية المراجع المختصّ.
    /// </summary>
    Task<Result<KpiEvaluationDto>> RequestRevisionAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// رفض نهائيّ (Rejected): يعيد التقييم من UnderReview إلى Rejected مع سبب إلزاميّ،
    /// يُنشئ حدث مراجعة + لقطة، يُخطر المُقيّم. صلاحية المراجع المختصّ. لا يدخل النتائج النهائية.
    /// </summary>
    Task<Result<KpiEvaluationDto>> RejectAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// تعليق مراجعة (لا يُغيّر الحالة): يُنشئ حدث مراجعة Comment مع نصّ إلزاميّ + تدقيق. للمراجع أو HR (Flag).
    /// </summary>
    Task<Result<KpiEvaluationDto>> CommentAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// تمييز للمراجعة (Flag) من HR: لا يُغيّر الحالة، يُنشئ حدث مراجعة Flag + يُخطر Admin/GM/CEO. لا يمنح HR اعتمادًا.
    /// </summary>
    Task<Result<KpiEvaluationDto>> FlagForReviewAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// طلب إعادة فتح (Request Reopen) من HR: سبب إلزاميّ، لا يُغيّر الحالة، يُنشئ حدث مراجعة + تدقيق
    /// ويُخطر Admin/GM/CEO. لا يمنح HR صلاحية إعادة الفتح الفعليّة.
    /// </summary>
    Task<Result<KpiEvaluationDto>> RequestReopenAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// إعادة فتح للتعديل (Reopen): من Approved/Rejected/NeedsRevision إلى UnderReview بصلاحية Admin/CEO/GM،
    /// سبب إلزاميّ، يُنشئ حدث مراجعة + لقطة + تدقيق. يعيد إسناد المراجع إن لزم.
    /// </summary>
    Task<Result<KpiEvaluationDto>> ReopenForRevisionAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>
    /// حذف إداريّ ناعم لتقييم KPI (Admin/CEO/GM فقط): IsDeleted=true + سبب إلزاميّ + تدقيق + حدث مراجعة.
    /// يُخرج التقييم من كل التجميعات (Global Query Filter). لا حذف فيزيائيّ.
    /// </summary>
    Task<Result<KpiEvaluationDto>> AdminDeleteAsync(Guid evaluationId, KpiReviewActionRequest request, CancellationToken ct = default);

    /// <summary>سجلّ أحداث المراجعة لتقييم KPI (Timeline)، حسب صلاحية العرض.</summary>
    Task<Result<IReadOnlyList<KpiEvaluationReviewEventDto>>> ListReviewEventsAsync(Guid evaluationId, CancellationToken ct = default);
}

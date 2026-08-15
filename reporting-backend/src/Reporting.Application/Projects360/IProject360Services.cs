using Reporting.Application.Common;

namespace Reporting.Application.Projects360;

/// <summary>
/// استراتيجيّة المشروع (CPW-R3 · §3) — استراتيجيّة **نشطة واحدة** لكلّ مشروع (فهرس فريد جزئيّ).
/// لا إصدارات في W4، والتصميم لا يمنعها لاحقًا.
/// </summary>
public interface IProjectStrategyService
{
    /// <summary>الاستراتيجيّة النشطة — <c>null</c> داخل نتيجة ناجحة إن لم تُنشأ بعد.</summary>
    Task<Result<ProjectStrategyDto?>> GetAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>حفظ الاستراتيجيّة النشطة (إنشاء أو تحديث) مع مزامنة تفاضليّة للسمات.</summary>
    Task<Result<ProjectStrategyDto>> UpsertAsync(Guid projectId, UpsertProjectStrategyRequest request, CancellationToken ct = default);

    /// <summary>مخطَّط الاستراتيجيّة (النواة + كتالوج الحقول والأقسام) — تبني الواجهة نموذجها منه.</summary>
    Task<Result<ProjectStrategySchemaDto>> GetSchemaAsync(Guid projectId, CancellationToken ct = default);
}

/// <summary>أهداف المشروع (CPW-R3 · §4) — الأب الإلزاميّ لكلّ مؤشّر.</summary>
public interface IProjectObjectiveService
{
    Task<Result<IReadOnlyList<ProjectObjectiveDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ProjectObjectiveDto>> GetAsync(Guid projectId, Guid objectiveId, CancellationToken ct = default);
    Task<Result<ProjectObjectiveDto>> CreateAsync(Guid projectId, CreateProjectObjectiveRequest request, CancellationToken ct = default);
    Task<Result<ProjectObjectiveDto>> UpdateAsync(Guid projectId, Guid objectiveId, UpdateProjectObjectiveRequest request, CancellationToken ct = default);

    /// <summary>تحديث الحالة فقط — المستوى التشغيليّ (قائد الفريق/مدير الحساب مسموح).</summary>
    Task<Result<ProjectObjectiveDto>> UpdateStatusAsync(Guid projectId, Guid objectiveId, UpdateProjectObjectiveStatusRequest request, CancellationToken ct = default);

    Task<Result<ProjectObjectiveDto>> SetActiveAsync(Guid projectId, Guid objectiveId, bool isActive, CancellationToken ct = default);

    /// <summary>حذف نهائيّ — يُرفَض بـ<c>project_objective.has_kpis.conflict</c> إن كان للهدف مؤشّرات.</summary>
    Task<Result> DeleteAsync(Guid projectId, Guid objectiveId, CancellationToken ct = default);
}

/// <summary>
/// مؤشّرات المشروع وقراءاتها (CPW-R3 · §5 + §6).
/// **كلّ المسارات متداخلة تحت الهدف** — لا يوجد مسار إنشاء مؤشّر مباشرة تحت المشروع (D-02).
/// </summary>
public interface IProjectKpiService
{
    /// <summary>قائمة مؤشّرات المشروع كلّها (للوحة) — القراءة فقط، لا إنشاء بهذا المستوى.</summary>
    Task<Result<IReadOnlyList<ProjectKpiDto>>> ListByProjectAsync(Guid projectId, bool includeInactive, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ProjectKpiDto>>> ListByObjectiveAsync(Guid projectId, Guid objectiveId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ProjectKpiDto>> GetAsync(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct = default);
    Task<Result<ProjectKpiDto>> CreateAsync(Guid projectId, Guid objectiveId, CreateProjectKpiRequest request, CancellationToken ct = default);
    Task<Result<ProjectKpiDto>> UpdateAsync(Guid projectId, Guid objectiveId, Guid kpiId, UpdateProjectKpiRequest request, CancellationToken ct = default);
    Task<Result<ProjectKpiDto>> SetActiveAsync(Guid projectId, Guid objectiveId, Guid kpiId, bool isActive, CancellationToken ct = default);

    // ===== §6 — القراءات =====
    Task<Result<IReadOnlyList<ProjectKpiReadingDto>>> ListReadingsAsync(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct = default);

    /// <summary>تسجيل قراءة مؤرَّخة ثمّ إعادة كتابة اللقطة السريعة من **أحدث قراءة باقية**.</summary>
    Task<Result<ProjectKpiReadingDto>> AddReadingAsync(Guid projectId, Guid objectiveId, Guid kpiId, CreateProjectKpiReadingRequest request, CancellationToken ct = default);

    /// <summary>تصحيح قراءة قائمة (لا تُضاف ثانية لنفس التاريخ).</summary>
    Task<Result<ProjectKpiReadingDto>> UpdateReadingAsync(Guid projectId, Guid objectiveId, Guid kpiId, Guid readingId, UpdateProjectKpiReadingRequest request, CancellationToken ct = default);
}

/// <summary>المخرَجات التعاقديّة (CPW-R3 · §11) — **Contract Deliverable** لا «Deliverable» مجرَّدة.</summary>
public interface IProjectContractDeliverableService
{
    Task<Result<IReadOnlyList<ProjectContractDeliverableDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ProjectContractDeliverableDto>> GetAsync(Guid projectId, Guid deliverableId, CancellationToken ct = default);
    Task<Result<ProjectContractDeliverableDto>> CreateAsync(Guid projectId, CreateProjectContractDeliverableRequest request, CancellationToken ct = default);
    Task<Result<ProjectContractDeliverableDto>> UpdateAsync(Guid projectId, Guid deliverableId, UpdateProjectContractDeliverableRequest request, CancellationToken ct = default);

    /// <summary>تحديث التقدّم/الحالة — المستوى التشغيليّ (قائد الفريق/مدير الحساب مسموح).</summary>
    Task<Result<ProjectContractDeliverableDto>> UpdateProgressAsync(Guid projectId, Guid deliverableId, UpdateProjectContractDeliverableProgressRequest request, CancellationToken ct = default);

    Task<Result<ProjectContractDeliverableDto>> SetActiveAsync(Guid projectId, Guid deliverableId, bool isActive, CancellationToken ct = default);

    /// <summary>
    /// أنواع المخرَجات التعاقديّة النشطة من الكتالوج (<c>contract_deliverable</c>) — تبني منها الواجهة
    /// قائمتها بلا رموز مكتوبة يدويًّا. مرشَّحة بالرؤية كباقي المسارات فلا تُستعمَل لاستكشاف المشاريع.
    /// </summary>
    Task<Result<IReadOnlyList<ProjectCatalogOptionDto>>> ListTypesAsync(Guid projectId, CancellationToken ct = default);
}

/// <summary>
/// **قراءة الحوكمة مرشَّحة بالمشروع** (CPW-R3 · §17-19) — قراءة فقط، صفر تغيير على نموذج الحوكمة
/// أو سير عملها أو صلاحيّاتها. كلّ مسار يمرّ بـ<c>LoadVisibleProjectAsync</c> أوّلًا.
/// </summary>
public interface IProjectGovernanceReadService
{
    Task<Result<IReadOnlyList<ProjectRiskListItemDto>>> ListRisksAsync(Guid projectId, bool openOnly, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProjectDecisionListItemDto>>> ListDecisionsAsync(Guid projectId, bool pendingOnly, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProjectNoteListItemDto>>> ListNotesAsync(Guid projectId, bool openOnly, CancellationToken ct = default);
}

/// <summary>
/// لوحة النظرة العامّة (CPW-R3 · §14) — **رحلة تطبيق واحدة بعدد استعلامات ثابت** (§15).
/// تُستهلَك لاحقًا من <c>GET /projects/{id}/overview</c> في W5؛ **لا Controller في W4**.
/// </summary>
public interface IProjectOverviewService
{
    Task<Result<ProjectOverviewDto>> GetProjectOverviewAsync(Guid projectId, CancellationToken ct = default);
}

/// <summary>
/// **الصحّة المخزَّنة** (CPW-R3 · R2 §1074 + قرار المالك FINDING-W6-03).
///
/// <para>
/// **مصدر احتساب واحد لا غير**: <see cref="ProjectHealthPolicy"/>. هذه الخدمة **لا تحمل معادلة
/// واحدة** — تجمع المدخلات من قاعدة البيانات، تناديها، ثمّ تكتب المخرجات الثلاثة.
/// </para>
///
/// <para>
/// **يُخزَّن ثلاثة فقط**: <c>Project.HealthStatus</c> · <c>Project.HealthPercent</c> ·
/// <c>Project.HealthComputedAtUtc</c>. **الأسباب لا تُخزَّن أبدًا** (W1-A بند 1) — تُشتقّ لحظيًّا.
/// **صفر هجرة**: الأعمدة الثلاثة موجودة أصلًا منذ الهجرة #33.
/// </para>
/// </summary>
public interface IProjectHealthService
{
    /// <summary>
    /// **المسار العامّ** لـ<c>POST /projects/{projectId}/health/recompute</c>:
    /// بوّابة التخويل ⟵ احتساب ⟵ تخزين ⟵ عقد الصحّة الرسميّ.
    /// **Idempotent وظيفيًّا**: مدخلات لم تتغيّر ⟹ نفس <c>Score</c> ونفس <c>Status</c>.
    /// </summary>
    Task<Result<ProjectHealthDto>> RecomputeAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// **حفظ الطفرة والصحّة في معاملة واحدة** — يستدعيه كلّ Use Case يغيّر مدخلًا فعليًّا
    /// من مدخلات <see cref="ProjectHealthPolicy"/>، **بدل** <c>SaveChangesAsync</c> المباشرة.
    ///
    /// <para>
    /// **لماذا معاملة صريحة؟** الطفرة يجب أن تُحفَظ أوّلًا كي يراها استعلام تجميع الصحّة، ثمّ
    /// تُكتب الصحّة. المعاملة تضمن أنّ فشل أيّ خطوة **يُرجِع الاثنتين معًا** فلا تبقى طفرة
    /// بصحّة بائتة. لا تُفتَح معاملة ثانية إن كانت واحدة قائمة أصلًا.
    /// </para>
    ///
    /// <para>**لا تخويل هنا** — تمّ في الـUse Case المستدعي قبل الطفرة.</para>
    /// </summary>
    Task SaveWithHealthAsync(Guid projectId, CancellationToken ct = default);
}

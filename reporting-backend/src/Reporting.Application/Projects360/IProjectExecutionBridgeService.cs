using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.Projects360;

/// <summary>
/// **جسر التنفيذ الهجين** (P360-WF-R2 §10) — القرار المعتمد `OPTION B`.
///
/// <para>
/// **ما يفعله**: يصل ما نفّذه الفريق بالمخرَج التعاقديّ **بالمعرّف لا بالاسم**، عبر ادّعاءٍ
/// يرفعه المنفِّذ ويحسمه المراجِع؛ والقبول وحده يكتب على المخرَج، ومنه يصعد الأثر تلقائيًّا
/// إلى تقدّم المشروع وصحّته عبر <see cref="IProjectHealthService.SaveWithHealthAsync"/>.
/// </para>
///
/// <para>
/// **ما لا يفعله عمدًا**: لا مهامّ ولا مهامّ فرعيّة ولا تبعيّات ولا سجلّات عمل. البرومبت حسم
/// أنّ بناء محرّك مهامّ كامل يُنشئ نظامًا ثانيًا للتشغيل بجوار التقارير بدل أن يصلها به.
/// </para>
///
/// <para>
/// **حارسان مختلفان لا حارس واحد**: الرفع مسموح لكلّ من يرى المشروع (المنفِّذ لا يملك
/// <c>CanOperate</c> عادةً، وإلزامه بها كان سيُلغي معنى «مقترح»)؛ أمّا الحسم فمقصور على من
/// يملك تحديث تقدّم هذا المشروع بعينه — وهو نفس حارس الكتابة المباشرة على المخرَج، فلا يفتح
/// الجسر بابًا خلفيًّا يتجاوز الصلاحيّة التي يحميها.
/// </para>
/// </summary>
public interface IProjectExecutionBridgeService
{
    /// <summary>مقترحات المشروع، أحدثها أوّلًا، مع ترشيح اختياريّ بالحالة أو بالمخرَج.</summary>
    Task<Result<IReadOnlyList<ProjectExecutionProposalDto>>> ListAsync(
        Guid projectId,
        ExecutionProposalStatus? status = null,
        Guid? deliverableId = null,
        CancellationToken ct = default);

    /// <summary>رفع ادّعاء تنفيذ على مخرَج تعاقديّ نشط.</summary>
    Task<Result<ProjectExecutionProposalDto>> CreateAsync(
        Guid projectId, CreateProjectExecutionProposalRequest request, CancellationToken ct = default);

    /// <summary>
    /// حسم المقترح قبولًا أو رفضًا.
    ///
    /// <para>
    /// **التعادليّة بالحالة لا بعلَم**: التطبيق مشروط بكون المقترح <c>Pending</c>، والقبول ينقله
    /// إلى <c>Accepted</c> داخل نفس المعاملة ⟹ نداء ثانٍ لا يضاعف أثرًا. النداء الثاني بنفس
    /// القرار يعيد المقترح كما هو **نجاحًا**، وبقرار مخالف يُرفَض بـ<c>already_reviewed.conflict</c>
    /// — لأنّ «قبلتُه ثمّ رفضتُه» ليس تكرارًا بل قرار جديد يتطلّب مسارًا صريحًا.
    /// </para>
    /// </summary>
    Task<Result<ProjectExecutionProposalDto>> ReviewAsync(
        Guid projectId, Guid proposalId, ReviewProjectExecutionProposalRequest request, CancellationToken ct = default);
}

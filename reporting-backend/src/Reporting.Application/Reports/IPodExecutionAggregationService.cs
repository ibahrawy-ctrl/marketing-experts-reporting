using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// محرّك تجميع التنفيذ (ERDS Phase 5) — قراءة فقط.
/// يقرأ القوالب التنفيذية الرقمية الستة (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع) من جدول TableGrid،
/// ويُجمّعها حسب (الفريق/Pod، العميل، المشروع، الموظّف، الفترة). لا يغيّر أيّ تسليم/قالب/مسار اعتماد.
/// النطاق محكوم بـ IScopeResolver (لا يفتح بيانات خارج نطاق الدور). يتجاهل بأمان التقارير/الصفوف غير المطابقة.
/// مستقلّ تمامًا عن Phase 4 (B2C/B2B) — لا يمسّه.
/// </summary>
public interface IPodExecutionAggregationService
{
    /// <summary>تجميع التنفيذ الموحّد لكل (الفترة، الفريق، الموظّف، العميل، المشروع) ضمن نطاق المستخدم.</summary>
    Task<Result<PodExecutionReport>> AggregateByPodAsync(PodExecutionFilter filter, CancellationToken ct = default);

    /// <summary>تجميع التنفيذ لكل (عميل، مشروع) على مستوى النطاق مع مؤشّرات محسوبة من المجاميع.</summary>
    Task<Result<ClientExecutionReport>> AggregateByClientAsync(PodExecutionFilter filter, CancellationToken ct = default);

    /// <summary>تجميع «تقرير المشاريع حسب العميل/المشروع» فقط لكل (الفترة، الموظّف، العميل، المشروع).</summary>
    Task<Result<ProjectExecutionReport>> AggregateByProjectAsync(PodExecutionFilter filter, CancellationToken ct = default);
}

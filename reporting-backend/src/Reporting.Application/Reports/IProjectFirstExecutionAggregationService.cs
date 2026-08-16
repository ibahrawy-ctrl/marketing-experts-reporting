using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// محرّك التجميع Project-First (PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1) — قراءة فقط.
/// يقرأ قوالب التنفيذ الأربعة من قسم المشاريع المتكرّر (ProjectRepeatableSection) حيث كل الأرقام داخل المشروع،
/// ويُجمّعها حسب (المشروع/الموظّف/Pod/العميل) على المعرّفات الحقيقية. لا يغيّر أيّ تسليم/قالب/مسار اعتماد.
/// النطاق محكوم بـ IScopeResolver ∪ IClientProjectAccess. مستقلّ تمامًا عن مسار المبيعات (B2C/B2B)
/// وعن محرّك التجميع الموحّد للتنفيذ (Family B المسطّح على TableGrid).
/// </summary>
public interface IProjectFirstExecutionAggregationService
{
    /// <summary>تجميع لكل مشروع (عبر كل الموظّفين ضمن النطاق/الفلاتر).</summary>
    Task<Result<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>> AggregateByProjectAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default);

    /// <summary>تجميع لكل (موظّف، مشروع) — للتفصيل داخل عرض قائد الفريق/المدير.</summary>
    Task<Result<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>> AggregateByEmployeeAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default);

    /// <summary>تجميع لكل Pod (فريق المُسلِّم).</summary>
    Task<Result<ProjectFirstExecutionReport<ProjectFirstByPodRow>>> AggregateByPodAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default);

    /// <summary>تجميع لكل عميل.</summary>
    Task<Result<ProjectFirstExecutionReport<ProjectFirstByClientRow>>> AggregateByClientAsync(
        ProjectFirstExecutionFilter filter, CancellationToken ct = default);
}

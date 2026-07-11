using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// محرّك التجميع Project-First (RC-4 Task 4، Path A) — قراءة فقط.
/// يقرأ قوالب التنفيذ الأربعة من قسم المشاريع المتكرّر (ProjectRepeatableSection) حيث كل الأرقام داخل المشروع،
/// ويُجمّعها حسب (المشروع/الموظّف/Pod/العميل) على المعرّفات الحقيقية. لا يغيّر أيّ تسليم/قالب/مسار اعتماد.
/// النطاق محكوم بـ IScopeResolver. مستقلّ تمامًا عن مسار المبيعات (B2C/B2B) وعن المسار القديم (Family B المسطّح).
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

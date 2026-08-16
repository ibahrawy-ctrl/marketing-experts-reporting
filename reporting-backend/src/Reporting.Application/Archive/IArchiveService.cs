using Reporting.Application.Common;

namespace Reporting.Application.Archive;

/// <summary>
/// خدمة الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1): قراءة العناصر المحذوفة إداريًّا ناعمًا
/// (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid المعتمَدة. لا حذف نهائيّ ولا جدولة ولا إشعارات.
/// كل القراءات تتجاوز مرشّح الاستعلام العالميّ (IgnoreQueryFilters) وتقتصر على IsDeleted == true.
/// </summary>
public interface IArchiveService
{
    /// <summary>قائمة العناصر المؤرشفة (مصفّحة + مرشّحة) مع حساب قابلية الاسترجاع لكل عنصر.</summary>
    Task<ArchivePagedResult> ListAsync(ArchiveFilter filter, CancellationToken ct = default);

    /// <summary>تفاصيل عنصر مؤرشف واحد (تقرير أو KPI) مع لقطة سير العمل والأثر التدقيقيّ واستراتيجية الاسترجاع.</summary>
    Task<Result<ArchiveDetailsDto>> GetDetailsAsync(ArchiveItemType itemType, Guid itemId, CancellationToken ct = default);

    /// <summary>استرجاع تقرير مُسلَّم محذوف إداريًّا وفق دلالات Hybrid (سبب إلزاميّ 10–500 محرفًا).</summary>
    Task<Result<ArchiveDetailsDto>> RestoreReportAsync(Guid submissionId, RestoreRequest request, CancellationToken ct = default);

    /// <summary>استرجاع تقييم KPI محذوف إداريًّا (سبب إلزاميّ 10–500 محرفًا).</summary>
    Task<Result<ArchiveDetailsDto>> RestoreKpiAsync(Guid evaluationId, RestoreRequest request, CancellationToken ct = default);
}

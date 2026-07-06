using Reporting.Application.Common;

namespace Reporting.Application.Kpi;

/// <summary>إدارة قوالب مؤشرات الأداء (مقاييس بأوزان، إصدارات، نشر).</summary>
public interface IKpiTemplateService
{
    Task<Result<KpiTemplateDetailDto>> CreateAsync(CreateKpiTemplateRequest request, Guid ownerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<KpiTemplateDto>>> ListAsync(KpiTemplateFilter filter, CancellationToken ct = default);
    Task<Result<KpiTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<KpiTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateKpiTemplateRequest request, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);
    Task<Result> ReactivateAsync(Guid id, CancellationToken ct = default);

    Task<Result<KpiMetricDto>> AddMetricAsync(Guid versionId, UpsertKpiMetricRequest request, CancellationToken ct = default);
    Task<Result<KpiMetricDto>> UpdateMetricAsync(Guid metricId, UpsertKpiMetricRequest request, CancellationToken ct = default);
    Task<Result> DeleteMetricAsync(Guid metricId, CancellationToken ct = default);

    Task<Result<KpiTemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default);
    Task<Result<KpiTemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default);

    // ===== إسناد قوالب KPI (Phase T1) =====
    /// <summary>تغطية القالب: المرتبطون والمستثنون بأسبابهم + صفوف الإسناد الصريحة (معاينة).</summary>
    Task<Result<KpiTemplateAssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct = default);

    /// <summary>إسناد/استثناء صريح للقالب (Employee/JobRole/Team/Department).</summary>
    Task<Result<KpiTemplateAssignmentRowDto>> AddAssignmentAsync(Guid templateId, CreateKpiAssignmentRequest request, CancellationToken ct = default);

    /// <summary>تعطيل/تفعيل إسناد قائم + تعديل الملاحظة.</summary>
    Task<Result<KpiTemplateAssignmentRowDto>> UpdateAssignmentAsync(Guid templateId, Guid assignmentId, UpdateKpiAssignmentRequest request, CancellationToken ct = default);

    /// <summary>حذف إسناد صريح.</summary>
    Task<Result> RemoveAssignmentAsync(Guid templateId, Guid assignmentId, CancellationToken ct = default);
}

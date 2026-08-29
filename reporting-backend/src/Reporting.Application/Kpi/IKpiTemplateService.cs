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

    /// <summary>
    /// P2-HR-008 — النسخة الدفعيّة من منتقي الإسناد: لكلّ مستخدم، معرّفات قوالب KPI المنشورة النشطة
    /// المُسنَدة له فعليًّا. تستعمل <b>نفس</b> منطق الأخصّية/الاستثناء المستعمل للمستخدم الواحد
    /// (لا نسخة ثانية منه)، بعدد استعلامات ثابت مهما بلغ عدد المستخدمين (لا N+1).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> ResolveAssignedTemplatesForUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>
    /// DEC-01/5+6 — التواتر الفعّال لكلّ مستخدم كما كان ساريًا في <paramref name="asOf"/>.
    /// يُشتقّ من القالب الذي يُقيَّم عليه الموظّف فعلًا عبر <b>نفس</b> محرّك الأولوية أعلاه — لا آليّة موازية.
    /// ترتيب الحسم بين القوالب المطابِقة: إسناد موظّف ← إسناد فريق ← مسمّى وظيفيّ ← إسناد إدارة ← قالب عامّ.
    /// عند انعدام أيّ مطابقة تُعاد <see cref="KpiEffectiveCadence.Cadence"/> = <c>null</c>
    /// بمصدر <c>notConfigured</c> — لا اختيار ولا سقوط صامت.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, KpiEffectiveCadence>> ResolveEffectiveCadencesAsync(
        IReadOnlyCollection<Guid> userIds, DateOnly asOf, CancellationToken ct = default);
}

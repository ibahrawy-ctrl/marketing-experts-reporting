using Reporting.Application.Common;

namespace Reporting.Application.Templates;

public interface IReportTemplateService
{
    Task<Result<ReportTemplateDetailDto>> CreateAsync(CreateTemplateRequest request, Guid ownerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ReportTemplateDto>>> ListAsync(TemplateFilter filter, CancellationToken ct = default);
    Task<Result<ReportTemplateDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ReportTemplateDetailDto>> UpdateMetadataAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);
    // الحذف النهائي — مسموح فقط لقالب مسودة غير مستخدَم؛ غير ذلك يُرجَع تعارض يوجّه للأرشفة.
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    // معاينة القالب كما يراه الموظّف (قراءة فقط، بلا إنشاء تسليم).
    Task<Result<TemplatePreviewDto>> PreviewAsync(Guid id, CancellationToken ct = default);
    // تغطية القالب: المرتبطون والمستثنون بنفس أولوية الاختيار بالخادم + الإسنادات الصريحة + التعارضات.
    Task<Result<TemplateAssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct = default);

    // حارس الإسناد (المصدر الوحيد للحقيقة): هل القالب مُسنَد فعليًّا للمستخدم بنفس منطق assignedOnly
    // (Include/Exclude + مستويات Employee/JobRole/Team/Department/General)؟ يُستخدم لمنع إنشاء/تسليم
    // تقرير لقالب غير مُسنَد. يقتصر على القوالب المنشورة النشطة.
    Task<bool> IsTemplateAssignedToUserAsync(Guid userId, Guid templateId, CancellationToken ct = default);

    // نسخة مُجمَّعة من حارس الإسناد بعدد استعلامات ثابت (لا N+1): تُرجِع لكلّ مستخدم مجموعة القوالب
    // المنشورة النشطة المُسنَدة له فعليًّا، بنفس منطق <see cref="IsTemplateAssignedToUserAsync"/> ذاته
    // (Include/Exclude + مستويات Employee/JobRole/Team/Department/General والأخصّ يطغى). تُستخدَم في
    // الإسقاطات القرائية (الحالة المتوقّعة) لفرض عقد الاستحقاق دون استدعاء لكلّ مستخدم على حدة.
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> ResolveAssignedTemplatesForUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    // إدارة الإسناد/الاستثناء الصريح (Employee/JobRole/Team/Department) — حوكمة القوالب فقط.
    Task<Result<TemplateAssignmentRowDto>> AddAssignmentAsync(Guid templateId, CreateAssignmentRequest request, CancellationToken ct = default);
    Task<Result<TemplateAssignmentRowDto>> UpdateAssignmentAsync(Guid templateId, Guid assignmentId, UpdateAssignmentRequest request, CancellationToken ct = default);
    Task<Result> RemoveAssignmentAsync(Guid templateId, Guid assignmentId, CancellationToken ct = default);

    // بانِي الحقول — على الإصدار المسودة فقط
    Task<Result<TemplateFieldDto>> AddFieldAsync(Guid versionId, UpsertFieldRequest request, CancellationToken ct = default);
    Task<Result<TemplateFieldDto>> UpdateFieldAsync(Guid fieldId, UpsertFieldRequest request, CancellationToken ct = default);
    Task<Result> DeleteFieldAsync(Guid fieldId, CancellationToken ct = default);
    Task<Result> ReorderFieldsAsync(Guid versionId, IReadOnlyList<Guid> orderedFieldIds, CancellationToken ct = default);

    // الإصدارات
    Task<Result<TemplateVersionDto>> PublishVersionAsync(Guid versionId, Guid publishedById, CancellationToken ct = default);
    Task<Result<TemplateVersionDto>> CreateDraftVersionAsync(Guid templateId, CancellationToken ct = default);
    // حذف نسخة غير مستخدَمة فقط — يُمنع حذف نسخة مرتبطة بتقارير سابقة أو الوحيدة أو الأحدث أو المنشورة الحالية.
    // لا يحذف القالب نفسه إطلاقًا. حوكمة القوالب فقط.
    Task<Result> DeleteVersionAsync(Guid versionId, CancellationToken ct = default);
}

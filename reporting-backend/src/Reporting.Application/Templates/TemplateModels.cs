using Reporting.Domain.Enums;

namespace Reporting.Application.Templates;

public record TemplateFieldDto(
    Guid Id,
    string Label,
    string? Key,
    FieldType FieldType,
    int Order,
    bool IsRequired,
    string? HelpText,
    string? ConfigJson);

public record TemplateVersionDto(
    Guid Id,
    int VersionNumber,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    IReadOnlyList<TemplateFieldDto> Fields,
    // عدد التقارير المُسلَّمة المرتبطة بهذه النسخة تحديدًا (لتحديد قابلية الحذف الآمن للنسخة).
    int SubmissionCount = 0,
    // هل هذه هي النسخة المنشورة الحالية التي تُربَط بها التقارير الجديدة؟
    bool IsCurrentPublished = false,
    // الحذف مسموح فقط لنسخة غير مستخدَمة وليست الوحيدة ولا الأحدث ولا المنشورة الحالية.
    bool CanDelete = false,
    // سبب منع الحذف (يُعرض على الزر المعطّل) حين CanDelete=false.
    string? DeleteBlockReason = null);

public record ReportTemplateDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? JobRoleId,
    PeriodType DefaultPeriodType,
    TemplateStatus Status,
    Guid OwnerId,
    bool IsActive,
    int LatestVersionNumber,
    int FieldCount,
    TemplateClassification Classification);

public record ReportTemplateDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? JobRoleId,
    PeriodType DefaultPeriodType,
    TemplateStatus Status,
    Guid OwnerId,
    bool IsActive,
    TemplateClassification Classification,
    IReadOnlyList<TemplateVersionDto> Versions,
    // عدد التقارير المُسلَّمة المرتبطة بأي إصدار من هذا القالب (لتحديد قابلية الحذف الآمن).
    int SubmissionCount = 0,
    // الحذف النهائي مسموح فقط لقالب مسودة غير مستخدَم (لا تقارير مرتبطة)؛ غير ذلك أرشفة فقط.
    bool CanHardDelete = false);

// معاينة القالب «كما يراه الموظّف» — قراءة فقط بلا أي كتابة أو إنشاء تسليم.
// يُعرض الإصدار الفعّال (آخر إصدار منشور، وإن لم يوجد فآخر إصدار مسودة).
public record TemplatePreviewDto(
    Guid TemplateId,
    string Title,
    string? Description,
    PeriodType DefaultPeriodType,
    TemplateClassification Classification,
    TemplateStatus Status,
    bool IsActive,
    int? VersionNumber,
    bool IsPublished,
    IReadOnlyList<TemplateFieldDto> Fields);

// مستخدم ضمن تغطية القالب: مرتبط (Matched) أو مستثنى (Excluded) مع سبب الربط أو الاستثناء.
public record TemplateAssignmentUserDto(
    Guid UserId,
    string FullName,
    string? Email,
    Guid? JobRoleId,
    string? JobRoleName,
    bool IsActive,
    // أسباب الاستثناء: excludedBecauseInactive / excludedBecauseRoleMismatch /
    // excludedBecauseMoreSpecificTemplateExists / excludedBecauseTemplateNotAssignable / excludedManually
    string? ExclusionReason,
    // سبب الربط للمرتبطين: matchedByUser / matchedByJobRole / matchedByTeam / matchedByDepartment / matchedByGeneral
    string? MatchReason = null,
    // انتماء تنظيمي (قراءة فقط) لتمكين أزرار الاستثناء السريع على مستوى الفريق/الإدارة في الواجهة.
    Guid? TeamId = null,
    string? TeamName = null,
    Guid? DepartmentId = null,
    string? DepartmentName = null);

// صفّ إسناد/استثناء صريح للقالب (للعرض والإدارة في تبويب الإسناد).
public record TemplateAssignmentRowDto(
    Guid Id,
    TemplateAssignmentScope ScopeType,
    Guid ScopeId,
    // اسم الموظّف/المسمّى/الفريق/الإدارة المُسنَد إليه (للعرض).
    string? ScopeName,
    TemplateAssignmentKind Kind,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc);

// تعارض «أكثر من تقرير أساسي لنفس الدورية» لموظّف بعينه على هذا القالب.
public record TemplateAssignmentConflictDto(
    Guid UserId,
    string FullName,
    // هذا القالب (الذي تُعرَض إسناداته الآن) — أحد طرفي التعارض.
    Guid ThisTemplateId,
    string ThisTemplateTitle,
    // القالب الأساسي الآخر المتعارض لنفس الموظّف والدورية.
    Guid OtherTemplateId,
    string OtherTemplateTitle,
    PeriodType PeriodType,
    string Reason,
    string SuggestedResolution);

// تغطية القالب: المعلومات + قواعد الربط + المرتبطون + المستثنون بأسبابهم
// + الإسنادات/الاستثناءات الصريحة + التعارضات (أكثر من أساسي لنفس الدورية).
// يطبّق نفس أولوية الاختيار بالخادم (Employee→JobRole→Team→Department→General، Exclude يتفوّق).
public record TemplateAssignmentsDto(
    Guid TemplateId,
    string Title,
    Guid? JobRoleId,
    string? JobRoleName,
    PeriodType DefaultPeriodType,
    TemplateClassification Classification,
    TemplateStatus Status,
    bool IsActive,
    // قابل للإسناد فعليًّا في إنشاء التقارير الجديدة (منشور ونشط).
    bool IsAssignable,
    // قالب متخصص مربوط بمسمّى وظيفي؟ (عكسه: قالب عام بلا مسمّى).
    bool IsRoleSpecific,
    IReadOnlyList<TemplateAssignmentUserDto> MatchedUsers,
    IReadOnlyList<TemplateAssignmentUserDto> ExcludedUsers,
    IReadOnlyList<TemplateAssignmentRowDto> Assignments,
    IReadOnlyList<TemplateAssignmentConflictDto> Conflicts);

// إنشاء إسناد/استثناء صريح لقالب على مستوى (موظّف/مسمّى/فريق/إدارة).
public record CreateAssignmentRequest(
    TemplateAssignmentScope ScopeType,
    Guid ScopeId,
    TemplateAssignmentKind Kind,
    string? Notes = null);

// تعطيل/تفعيل إسناد قائم + تعديل الملاحظة.
public record UpdateAssignmentRequest(bool IsActive, string? Notes = null);

public record CreateTemplateRequest(
    string Title,
    string? Description,
    Guid? JobRoleId,
    PeriodType DefaultPeriodType,
    // التصنيف (Phase 4 §4): Primary = تقرير الدور الأساسي المطلوب (يضم النبض)،
    // Supplementary = قالب تكميلي/اختياري لا يُحتسب تقريرًا أساسيًا ثانيًا. الافتراضي Primary.
    TemplateClassification Classification = TemplateClassification.Primary);

public record UpdateTemplateRequest(
    string Title,
    string? Description,
    Guid? JobRoleId,
    PeriodType DefaultPeriodType,
    TemplateClassification Classification = TemplateClassification.Primary);

public record UpsertFieldRequest(
    string Label,
    string? Key,
    FieldType FieldType,
    bool IsRequired,
    string? HelpText,
    string? ConfigJson);

// مرشّحات قائمة القوالب. مساران منفصلان لإنشاء التقرير:
// • AssignedOnly (إنشاء «تقريري»): صاحب التقرير هو المستخدم الحالي — تُطبَّق أولوية الدور
//   (قالب الدور إن وُجد وإلّا العام) حتى لمن يملك صلاحية إدارة القوالب، فلا يرى الكل.
// • SubjectUserId (إنشاء «بالنيابة»): صاحب التقرير هو الموظّف المختار — يتطلّب أن يكون ضمن
//   نطاق رؤية المُنشئ، ثم تُطبَّق أولوية الدور على ذلك الموظّف لا على المُنشئ.
public record TemplateFilter(Guid? JobRoleId = null, TemplateStatus? Status = null, bool? IsActive = null, bool AssignedOnly = false, Guid? SubjectUserId = null);

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
    IReadOnlyList<TemplateFieldDto> Fields);

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
    IReadOnlyList<TemplateVersionDto> Versions);

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

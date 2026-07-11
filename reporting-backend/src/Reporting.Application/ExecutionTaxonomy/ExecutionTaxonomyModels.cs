namespace Reporting.Application.ExecutionTaxonomy;

public record ExecutionTaxonomyDto(
    Guid Id,
    string Domain,
    string Code,
    string NameAr,
    string? NameEn,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

// خيار نشط مُفصَّل (Code + NameAr) لملء القوائم المنسدلة التي تحتاج الرمز للإرسال (مثل نوع تيار العمل P1).
public record ExecutionTaxonomyOptionDto(string Code, string NameAr);

// الإنشاء يحدّد Domain + Code (كلاهما غير قابل للتعديل بعد الإنشاء لأنهما مفتاح التفرّد المنطقيّ).
public record CreateExecutionTaxonomyRequest(string Domain, string Code, string NameAr, string? NameEn, int SortOrder);

// التعديل يقتصر على الاسم والترتيب — Domain و Code غير قابلين للتعديل.
public record UpdateExecutionTaxonomyRequest(string NameAr, string? NameEn, int SortOrder);

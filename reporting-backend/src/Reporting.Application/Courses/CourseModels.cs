namespace Reporting.Application.Courses;

/// <summary>عنصر دورة في الكتالوج (قراءة).</summary>
public record CourseDto(
    Guid Id, string NameAr, string? NameEn, bool IsActive, int SortOrder,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>طلب إنشاء دورة جديدة.</summary>
public record CreateCourseRequest(string NameAr, string? NameEn, int SortOrder);

/// <summary>طلب تعديل دورة قائمة.</summary>
public record UpdateCourseRequest(string NameAr, string? NameEn, int SortOrder);

/// <summary>
/// نتيجة الحذف الآمن للدورة: إمّا حذف نهائي (لم تُستخدَم في أي تقرير) أو أرشفة (مُستخدَمة — تُعطَّل ولا تُحذف).
/// <b>HardDeleted=true</b> ⇒ أُزيلت من القاعدة. <b>false</b> ⇒ أُرشِفت (IsActive=false) والتقارير القديمة تبقى كما هي.
/// </summary>
public record CourseDeleteResult(bool HardDeleted, CourseDto? Course, string Message);

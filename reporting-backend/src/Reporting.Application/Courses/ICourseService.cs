using Reporting.Application.Common;

namespace Reporting.Application.Courses;

/// <summary>
/// إدارة كتالوج الدورات (المصدر الرسمي لأسماء دورات مبيعات B2C). إضافة بحتة، لا تمسّ التقارير القائمة.
/// الكتابة للأدمن/الحوكمة فقط؛ القراءة (النشطة) متاحة للمستخدمين المصرَّح لهم لتغذية منتقي الدورة.
/// </summary>
public interface ICourseService
{
    /// <summary>كل الدورات (نشطة ومعطّلة) لشاشة الإدارة، مرتّبة بـ SortOrder ثم الاسم.</summary>
    Task<IReadOnlyList<CourseDto>> ListAsync(bool includeInactive, CancellationToken ct = default);

    Task<Result<CourseDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<CourseDto>> CreateAsync(CreateCourseRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<CourseDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// حذف آمن: إن لم تكن الدورة مستخدَمة في أي تقرير مبيعات B2C ⇒ حذف نهائي؛ وإن كانت مستخدَمة ⇒ أرشفة (تعطيل)
    /// دون حذف كي تبقى التقارير القديمة صالحة. في الحالتين تختفي الدورة من منتقي التقارير الجديدة.
    /// </summary>
    Task<Result<CourseDeleteResult>> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}

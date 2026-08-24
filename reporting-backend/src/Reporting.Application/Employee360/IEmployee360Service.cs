using Reporting.Application.Common;

namespace Reporting.Application.Employee360;

/// <summary>
/// P2-EMP-002 — المصدر الوحيد لبناء عرض الموظّف 360.
/// الخدمة **لا تملك بيانات**: تقرأ من مالكي الحقيقة وتُسقِطها في أقسام مصرَّح بها فقط.
/// خارج النطاق ⟵ <c>employee360.not_found</c> (404) اتّساقًا مع نمط المشروع في منع IDOR.
/// </summary>
public interface IEmployee360Service
{
    /// <param name="subjectUserId">الموظّف موضوع العرض.</param>
    /// <param name="sections">قائمة أقسام مطلوبة مفصولة بفواصل؛ فارغة = كلّ المصرَّح به.</param>
    /// <param name="periodKey">مفتاح الفترة الموحّد (مرحلة 1)؛ فارغ = الفترة الجارية.</param>
    Task<Result<Employee360Dto>> GetProfileAsync(
        Guid subjectUserId,
        string? sections = null,
        string? periodKey = null,
        CancellationToken ct = default);
}

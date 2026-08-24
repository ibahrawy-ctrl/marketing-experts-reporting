namespace Reporting.Application.Security;

/// <summary>
/// الطبقة الخادميّة المركزيّة للرؤية على مستوى الحقل/القسم (P2-SEC-001).
/// كلّ سطح يعرض بيانات موظّف يبني سياقه من هنا — لا يُعاد حساب النطاق يدويًّا في أيّ خدمة.
/// إخفاء الواجهة ليس حماية؛ الحقل غير المصرّح لا يُرسَل أصلًا.
/// </summary>
public interface IFieldVisibilityPolicy
{
    /// <summary>
    /// يبني سياق رؤية المستخدم الحالي تجاه موظّف بعينه (يحلّ العلاقة من شجرة الإدارة والنطاق).
    /// يعيد <see cref="SubjectRelation.None"/> إن كان الموضوع خارج النطاق أو غير موجود
    /// ⇒ على المُستدعي إعادة <c>*.not_found</c> (404) لا 403.
    /// </summary>
    Task<FieldVisibilityContext> BuildContextAsync(Guid subjectUserId, string? purpose = null, CancellationToken ct = default);

    /// <summary>هل يرى المُشاهِد حقلًا بهذا التصنيف؟ يكتب أثرًا تدقيقيًّا للتصنيفات الحسّاسة (بلا قيمة الحقل).</summary>
    Task<bool> CanSeeAsync(FieldVisibilityContext ctx, FieldSensitivity sensitivity, string fieldKey, CancellationToken ct = default);

    /// <summary>فحص متزامن بلا تدقيق — للترشيح الكتليّ داخل الإسقاطات.</summary>
    bool CanSee(FieldVisibilityContext ctx, FieldSensitivity sensitivity);

    /// <summary>هل يظهر القسم في استجابة Employee 360؟</summary>
    bool CanSeeSection(FieldVisibilityContext ctx, Employee360Section section);
}

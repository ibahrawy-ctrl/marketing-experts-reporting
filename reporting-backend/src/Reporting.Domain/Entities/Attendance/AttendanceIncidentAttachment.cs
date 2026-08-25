using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Attendance;

/// <summary>
/// مرفق إثبات على حادثة حضور. **البيان الوصفيّ في قاعدة البيانات والملفّ على القرص خارج جذر الويب**
/// وفق نمط <c>FileStorageOptions</c> القائم — لا يُقدَّم عبر Static Files ولا يُبنى مساره من إدخال المستخدم.
/// الوصول خارج النطاق يُرجِع 404 لا 403 كي لا يُستدلّ على وجود المرفق.
/// </summary>
public class AttendanceIncidentAttachment : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Guid UploadedByUserId { get; set; }

    /// <summary>اسم الملفّ كما رفعه المستخدم — للعرض فقط، لا يُستعمل في بناء المسار.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>المسار النسبيّ داخل جذر التخزين المُهيّأ. يُولّده الخادم بمعرّف لا باسم المستخدم.</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>بصمة SHA-256 للمحتوى — لكشف التكرار وإثبات السلامة.</summary>
    public string ContentHash { get; set; } = string.Empty;
}

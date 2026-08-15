namespace Reporting.Application.Common;

/// <summary>
/// إعدادات تخزين الملفات المحلي (V1.1 — خدمات الموظف، ثمّ CPW-R1B2 — خدمة المستندات).
/// تُقرأ من قسم "FileStorage" (متغيرات البيئة FileStorage__*).
/// كل المسارات يجب أن تكون خارج جذر الويب (wwwroot) ولا تُقدَّم عبر Static Files.
/// عند غياب القيمة يُستخدم مسار افتراضي آمن داخل ContentRoot/App_Data.
/// </summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// مجلّد تخزين الملفات النهائية لطلبات الموارد البشرية (PDF). خارج wwwroot.
    /// مثال إنتاج: /var/lib/reporting/employee-service-requests/final-documents.
    /// </summary>
    public string? EmployeeServiceFinalDocumentsPath { get; set; }

    // ===== CPW-R1B2 — خدمة المستندات (قرارات المالك C-01..C-04) =====

    /// <summary>
    /// جذر تخزين خدمة المستندات (عامّ لكلّ ارتباطات الموارد، وأوّلها العميل).
    /// من الإعدادات حصرًا — لا مسار صلب في الكود. خارج wwwroot.
    /// مثال إنتاج: /var/lib/reporting/documents.
    /// </summary>
    public string? DocumentsRootPath { get; set; }

    /// <summary>الحدّ الأقصى لحجم الملفّ المرفوع بالبايت (افتراضيًّا 25MB) — C-04.</summary>
    public long MaxUploadSizeBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>حصّة التخزين القصوى لكلّ مورد (عميل) بالبايت (افتراضيًّا 2GB) — C-04.</summary>
    public long ResourceStorageQuotaBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>عدد عمليّات الرفع المسموح بها لكلّ مستخدم داخل النافذة الزمنيّة — C-04.</summary>
    public int UploadRateLimitPermitLimit { get; set; } = 20;

    /// <summary>طول نافذة حدّ معدّل الرفع بالثواني — C-04.</summary>
    public int UploadRateLimitWindowSeconds { get; set; } = 60;

    /// <summary>
    /// قائمة سماح الامتدادات (بنقطة بادئة، حروف صغيرة). فارغة ⇒ تُستخدَم القائمة الافتراضيّة
    /// في <c>DocumentContentPolicy</c>. التوسيع من الإعدادات لا من الكود.
    /// </summary>
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

    /// <summary>قائمة سماح أنواع المحتوى. فارغة ⇒ تُستخدَم القائمة الافتراضيّة.</summary>
    public string[] AllowedMimeTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// اسم محرّك فحص البرمجيّات الخبيثة المُهيّأ. القيمة <c>None</c> تعني «لا محرّك» (C-01)،
    /// وعندها تبقى حالة الفحص <c>NotScanned</c> ولا يُدَّعى أنّ الملفّ نظيف إطلاقًا.
    /// </summary>
    public string ScanEngine { get; set; } = "None";

    /// <summary>
    /// هل يُشترَط أن تكون النسخة مفحوصة ونظيفة قبل السماح بالتنزيل.
    /// يبقى <c>false</c> ما دام لا يوجد محرّك فعليّ، وإلّا تعطّلت الخدمة كلّها (C-01).
    /// </summary>
    public bool RequireCleanScanBeforeDownload { get; set; }
}

namespace Reporting.Application.Common;

/// <summary>
/// إعدادات تخزين الملفات المحلي (V1.1 — خدمات الموظف). تُقرأ من قسم "FileStorage"
/// (متغيرات البيئة FileStorage__*). كل المسارات يجب أن تكون خارج جذر الويب (wwwroot).
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
}

using Reporting.Application.Common;

namespace Reporting.Application.Documents;

/// <summary>نتيجة التحقّق من محتوى ملفّ مرفوع.</summary>
/// <param name="IsValid">هل اجتاز الملفّ كلّ الفحوص.</param>
/// <param name="ErrorCode">رمز الخطأ عند الرفض.</param>
/// <param name="Error">رسالة عربيّة واضحة عند الرفض.</param>
/// <param name="ResolvedExtension">الامتداد المعتمَد (بنقطة بادئة، حروف صغيرة).</param>
/// <param name="ResolvedContentType">نوع المحتوى المعتمَد خادميًّا — لا يُؤخَذ من العميل كما هو.</param>
/// <param name="ForceAttachment">هل يجب فرض <c>Content-Disposition: attachment</c> دائمًا (SVG مثلًا).</param>
public record DocumentContentValidation(
    bool IsValid,
    string? ErrorCode,
    string? Error,
    string ResolvedExtension,
    string ResolvedContentType,
    bool ForceAttachment)
{
    public static DocumentContentValidation Reject(string code, string message)
        => new(false, code, message, string.Empty, string.Empty, true);

    public static DocumentContentValidation Accept(string extension, string contentType, bool forceAttachment)
        => new(true, null, null, extension, contentType, forceAttachment);
}

/// <summary>
/// سياسة محتوى المستندات (CPW-R1B2 — قرار المالك C-04):
/// قائمة سماح للامتدادات، وقائمة سماح لأنواع المحتوى، وتحقّق Magic Number،
/// وتطهير اسم الملفّ، وفرض التنزيل كمرفق لأنواع قابلة للتنفيذ في المتصفّح.
/// المنطق كلّه خادميّ ولا يعتمد على ما يُرسله العميل.
/// </summary>
public static class DocumentContentPolicy
{
    /// <summary>الحدّ الأقصى لطول اسم الملفّ الأصليّ المعروض.</summary>
    public const int MaxFileNameLength = 200;

    /// <summary>قائمة السماح الافتراضيّة للامتدادات (تُستبدَل من الإعدادات عند الحاجة).</summary>
    public static readonly string[] DefaultAllowedExtensions =
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".zip"
    };

    /// <summary>قائمة السماح الافتراضيّة لأنواع المحتوى.</summary>
    public static readonly string[] DefaultAllowedMimeTypes =
    {
        "application/pdf",
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain", "text/csv",
        "application/zip"
    };

    /// <summary>أنواع تُقدَّم دائمًا كمرفق ولا تُعرَض داخل المتصفّح (خطر تنفيذ سكربت).</summary>
    private static readonly HashSet<string> AlwaysAttachmentExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".svg", ".zip", ".csv", ".txt", ".html", ".htm" };

    /// <summary>الامتدادات المبنيّة على حاوية ZIP (OOXML) — بصمتها السحريّة نفسها.</summary>
    private static readonly HashSet<string> ZipContainerExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".docx", ".xlsx", ".pptx", ".zip" };

    /// <summary>الامتدادات النصّيّة الصرفة التي لا بصمة سحريّة لها.</summary>
    private static readonly HashSet<string> PlainTextExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".csv", ".svg" };

    /// <summary>نوع المحتوى المعتمَد خادميًّا لكلّ امتداد مسموح.</summary>
    private static readonly Dictionary<string, string> CanonicalContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".zip"] = "application/zip"
        };

    /// <summary>
    /// يطهّر اسم الملفّ الأصليّ: يزيل أيّ مكوّن مسار ومحارف التحكّم ويقصّ الطول.
    /// الناتج للعرض والتنزيل فقط — لا يُستخدَم إطلاقًا كمسار على القرص.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "file";

        // إزالة أيّ مسار (بما فيه فواصل ويندوز) لمنع Path Traversal في الاسم المعروض.
        var name = fileName.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];

        var cleaned = new string(name
            .Where(c => !char.IsControl(c) && c != '"' && c != ':' && c != '*' && c != '?' && c != '<' && c != '>' && c != '|')
            .ToArray())
            .Trim()
            .Trim('.');

        if (cleaned.Length == 0) return "file";
        return cleaned.Length > MaxFileNameLength ? cleaned[^MaxFileNameLength..] : cleaned;
    }

    /// <summary>قائمة الامتدادات المفعَّلة (من الإعدادات إن وُجدت وإلّا الافتراضيّة).</summary>
    public static string[] AllowedExtensions(FileStorageOptions options)
        => options.AllowedExtensions.Length > 0 ? options.AllowedExtensions : DefaultAllowedExtensions;

    /// <summary>قائمة أنواع المحتوى المفعَّلة (من الإعدادات إن وُجدت وإلّا الافتراضيّة).</summary>
    public static string[] AllowedMimeTypes(FileStorageOptions options)
        => options.AllowedMimeTypes.Length > 0 ? options.AllowedMimeTypes : DefaultAllowedMimeTypes;

    /// <summary>
    /// هل يُقدَّم هذا الملفّ كمرفق إجباريًّا؟ يُشتقّ من الامتداد وحده بلا إعادة قراءة المحتوى،
    /// ويُستعمَل عند التنزيل وعند بناء الـDTO. الامتداد المجهول يُعامَل كمرفق (الوضع الآمن).
    /// </summary>
    public static bool ShouldForceAttachment(string? fileNameOrExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrExtension)) return true;
        var value = fileNameOrExtension.Trim();
        var extension = value.StartsWith('.') && !value.Contains('/') && value.LastIndexOf('.') == 0
            ? value
            : Path.GetExtension(value);
        if (string.IsNullOrWhiteSpace(extension)) return true;
        extension = extension.ToLowerInvariant();
        if (AlwaysAttachmentExtensions.Contains(extension)) return true;
        return !DefaultAllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// يتحقّق من الامتداد ونوع المحتوى والبصمة السحريّة معًا (بـ AND لا OR).
    /// </summary>
    /// <param name="fileName">اسم الملفّ كما ورد من العميل (يُطهَّر داخليًّا).</param>
    /// <param name="declaredContentType">نوع المحتوى المُعلَن من العميل — لا يُوثَق به وحده.</param>
    /// <param name="header">أوّل بايتات المحتوى (يُنصَح بـ 512 بايت على الأقلّ).</param>
    /// <param name="options">الإعدادات المفعَّلة.</param>
    public static DocumentContentValidation Validate(
        string? fileName, string? declaredContentType, ReadOnlySpan<byte> header, FileStorageOptions options)
    {
        var safeName = SanitizeFileName(fileName);
        var extension = Path.GetExtension(safeName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension))
            return DocumentContentValidation.Reject("document.extension_missing",
                "يجب أن يحمل الملفّ امتدادًا معروفًا.");

        if (!AllowedExtensions(options).Contains(extension, StringComparer.OrdinalIgnoreCase))
            return DocumentContentValidation.Reject("document.extension_not_allowed",
                $"نوع الملفّ «{extension}» غير مسموح به.");

        if (!CanonicalContentTypes.TryGetValue(extension, out var canonical))
            return DocumentContentValidation.Reject("document.extension_not_allowed",
                $"نوع الملفّ «{extension}» غير مسموح به.");

        if (!AllowedMimeTypes(options).Contains(canonical, StringComparer.OrdinalIgnoreCase))
            return DocumentContentValidation.Reject("document.mime_not_allowed",
                "نوع محتوى الملفّ غير مسموح به.");

        // نوع المحتوى المُعلَن — إن أُرسِل، يجب أن يطابق النوع المعتمَد للامتداد.
        var declared = NormalizeContentType(declaredContentType);
        if (!string.IsNullOrEmpty(declared)
            && !declared.Equals(canonical, StringComparison.OrdinalIgnoreCase)
            && !IsAcceptableAlias(extension, declared))
        {
            return DocumentContentValidation.Reject("document.mime_mismatch",
                "نوع المحتوى المُعلَن لا يطابق امتداد الملفّ.");
        }

        if (!MatchesMagicNumber(extension, header))
            return DocumentContentValidation.Reject("document.magic_number_mismatch",
                "محتوى الملفّ لا يطابق نوعه المعلن.");

        var forceAttachment = AlwaysAttachmentExtensions.Contains(extension);
        return DocumentContentValidation.Accept(extension, canonical, forceAttachment);
    }

    /// <summary>يجرّد معاملات نوع المحتوى (charset وغيره) ويُصغّر الحروف.</summary>
    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return string.Empty;
        var value = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return value == "application/octet-stream" ? string.Empty : value;
    }

    /// <summary>مرادفات شائعة يرسلها المتصفّح ولا تُعدّ تعارضًا.</summary>
    private static bool IsAcceptableAlias(string extension, string declared) => extension switch
    {
        ".zip" or ".docx" or ".xlsx" or ".pptx" => declared is "application/zip" or "application/x-zip-compressed",
        ".csv" => declared is "text/csv" or "text/plain" or "application/csv" or "application/vnd.ms-excel",
        ".txt" => declared is "text/plain",
        ".svg" => declared is "image/svg+xml" or "text/xml" or "application/xml",
        ".jpg" or ".jpeg" => declared is "image/jpeg" or "image/jpg",
        _ => false
    };

    /// <summary>
    /// تحقّق البصمة السحريّة. الملفّات النصّيّة (txt/csv/svg) لا بصمة لها، ويكتفى بفحص
    /// أنّها ليست حاوية ثنائيّة معروفة متنكّرة.
    /// </summary>
    public static bool MatchesMagicNumber(string extension, ReadOnlySpan<byte> header)
    {
        if (header.Length == 0) return false;

        if (PlainTextExtensions.Contains(extension))
            return !StartsWith(header, "PK\u0003\u0004"u8) && !StartsWith(header, "%PDF-"u8);

        if (ZipContainerExtensions.Contains(extension))
            return StartsWith(header, "PK\u0003\u0004"u8)
                || StartsWith(header, "PK\u0005\u0006"u8)
                || StartsWith(header, "PK\u0007\u0008"u8);

        return extension switch
        {
            ".pdf" => StartsWith(header, "%PDF-"u8),
            ".png" => StartsWith(header, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".gif" => StartsWith(header, "GIF87a"u8) || StartsWith(header, "GIF89a"u8),
            ".webp" => header.Length >= 12 && StartsWith(header, "RIFF"u8) && StartsWith(header[8..], "WEBP"u8),
            // صيغ Office القديمة تعتمد حاوية OLE2 المركّبة.
            ".doc" or ".xls" or ".ppt" => StartsWith(header,
                new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            _ => false
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix)
        => value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}

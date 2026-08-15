namespace Reporting.Application.Documents;

/// <summary>
/// وصف ملفّ بعد تخزينه بنجاح (CPW-R1B2 — خدمة المستندات).
/// </summary>
/// <param name="StorageKey">مفتاح التخزين الداخليّ — يُمنع تسريبه إلى أيّ DTO أو Log أو تدقيق.</param>
/// <param name="SizeBytes">الحجم الفعليّ المكتوب على القرص بالبايت.</param>
/// <param name="Sha256">بصمة SHA-256 محسوبة أثناء الكتابة (64 محرفًا hex بحروف صغيرة).</param>
public record StoredFile(string StorageKey, long SizeBytes, string Sha256);

/// <summary>
/// تجريد تخزين الملفّات لخدمة المستندات — عامّ ومستقلّ عن نوع المورد المرتبط.
/// <para>
/// ضوابث مُلزِمة على أيّ تنفيذ:
/// المسار من الإعدادات حصرًا ولا يُكتب صلبًا؛ اسم التخزين مُولَّد خادميًّا ولا يُشتقّ من اسم
/// الملفّ الأصليّ؛ منع الخروج من الجذر (Path Traversal)؛ الكتابة ذرّيّة (ملفّ مؤقّت ثمّ نقل)؛
/// حساب البصمة أثناء الكتابة لا بقراءة ثانية؛ لا تقديم عبر Static Files إطلاقًا.
/// </para>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// يبني مفتاح تخزين داخليًّا من معرّفات فقط (بلا أيّ نصّ من المستخدم).
    /// </summary>
    /// <param name="resourceKind">نوع المورد المرتبط، مثل <c>clients</c> (أوّل ارتباط).</param>
    /// <param name="resourceId">معرّف المورد.</param>
    /// <param name="documentId">معرّف المستند.</param>
    /// <param name="versionId">معرّف النسخة.</param>
    /// <param name="safeExtension">امتداد مُتحقَّق منه من قائمة السماح (بنقطة بادئة) أو فارغ.</param>
    string BuildStorageKey(string resourceKind, Guid resourceId, Guid documentId, Guid versionId, string safeExtension);

    /// <summary>يكتب المحتوى ذرّيًّا ويُرجِع الحجم والبصمة. يفشل إن كان المفتاح موجودًا مسبقًا.</summary>
    Task<StoredFile> SaveAsync(string storageKey, Stream content, CancellationToken ct = default);

    /// <summary>يفتح تدفّق قراءة للملفّ المخزَّن. يرمي <see cref="FileNotFoundException"/> إن غاب.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>هل الملفّ موجود فعليًّا على القرص (يُستخدَم لكشف الملفّات المفقودة).</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);

    /// <summary>يحذف الملفّ إن وُجد. يُستعمل للتنظيف بعد فشل المعاملة فقط — لا للحذف الوظيفيّ.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

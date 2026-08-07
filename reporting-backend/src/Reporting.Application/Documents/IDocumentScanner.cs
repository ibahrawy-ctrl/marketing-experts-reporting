using Reporting.Domain.Enums;

namespace Reporting.Application.Documents;

/// <summary>نتيجة فحص محتوى ملفّ (CPW-R1B2 — قرار المالك C-01).</summary>
/// <param name="Status">الحالة الصادقة للفحص — <c>Clean</c> لا تُستعمَل إلّا بنتيجة محرّك فعليّ.</param>
/// <param name="Engine">اسم المحرّك المُنتِج للنتيجة، أو <c>None</c> حين لا محرّك.</param>
/// <param name="Detail">تفصيل النتيجة (رسالة المحرّك) — بلا أسرار.</param>
/// <param name="ScannedAtUtc">وقت الفحص — يبقى <c>null</c> حين لا يجري فحص فعليّ.</param>
public record DocumentScanResult(DocumentScanStatus Status, string Engine, string? Detail, DateTime? ScannedAtUtc);

/// <summary>
/// تجريد فحص البرمجيّات الخبيثة. لا يوجد محرّك فعليّ في هذه النسخة
/// (<see cref="NullDocumentScanner"/>)، ويُمنع منعًا باتًّا ادّعاء النظافة بلا فحص.
/// </summary>
public interface IDocumentScanner
{
    /// <summary>هل يوجد محرّك فحص مُهيّأ فعليًّا.</summary>
    bool IsConfigured { get; }

    /// <summary>اسم المحرّك المُهيّأ، أو <c>None</c>.</summary>
    string EngineName { get; }

    /// <summary>يفحص المحتوى ويُرجِع نتيجة صادقة. التدفّق يجب أن يكون قابلًا لإعادة الوضع.</summary>
    Task<DocumentScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default);
}

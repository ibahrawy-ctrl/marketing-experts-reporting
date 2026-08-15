using Reporting.Application.Documents;
using Reporting.Domain.Enums;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// محرّك فحص «لا شيء» — التنفيذ الوحيد في هذه النسخة (قرار المالك C-01).
/// لا يفحص المحتوى ولا يدّعي نظافته: يُرجِع دائمًا <see cref="DocumentScanStatus.NotScanned"/>
/// بمحرّك <c>None</c> وبلا وقت فحص. استبداله بمحرّك فعليّ لاحقًا لا يتطلّب تغيير الخدمة.
/// </summary>
public class NullDocumentScanner : IDocumentScanner
{
    public bool IsConfigured => false;

    public string EngineName => "None";

    public Task<DocumentScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
        => Task.FromResult(new DocumentScanResult(
            DocumentScanStatus.NotScanned,
            EngineName,
            "لا يوجد محرّك فحص مُهيّأ في هذه النسخة — لم يُفحَص الملفّ.",
            null));
}

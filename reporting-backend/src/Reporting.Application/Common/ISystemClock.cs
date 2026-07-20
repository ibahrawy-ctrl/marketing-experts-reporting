namespace Reporting.Application.Common;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — مصدر الوقت الحاليّ القابل للحقن (injectable).
/// يسمح للاختبارات بتثبيت «الآن» (ساعة اختبار) لإثبات التأخّر بعد الموعد دون انتظار الزمن الحقيقيّ،
/// بينما يستخدم الإنتاج الوقت الحقيقيّ. مصدر واحد للوقت عبر كل الطبقات المعتمدة على الاستحقاق/التأخّر.
/// </summary>
public interface ISystemClock
{
    /// <summary>اللحظة الحاليّة بتوقيت UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

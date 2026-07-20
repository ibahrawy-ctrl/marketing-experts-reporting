using Reporting.Application.Common;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — التنفيذ الإنتاجيّ لمصدر الوقت: يعيد الوقت الحقيقيّ (UTC).
/// الاختبارات تستبدله بساعة ثابتة عبر الحقن.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — المصدر الخلفيّ الموحّد لحالة دورات التقارير.
/// يجلب التسليمات دفعةً واحدة (بلا N+1) ويطبّق <see cref="Reporting.Domain.Common.UnifiedCycleStatusPolicy"/>
/// النقيّة. قراءة فقط: لا تعديل تسليم/سير عمل، لا هجرة، لا إشعار/بريد. النطاق self-only في هذه المرحلة.
/// </summary>
public interface IUnifiedReportStatusService
{
    /// <summary>حالة الدورات الأسبوعية الموحّدة للمستخدم نفسه لمجموعة مفاتيح دورات (استعلام دفعيّ واحد).</summary>
    Task<Result<IReadOnlyList<UnifiedReportCycleStatusDto>>> GetMyWeeklyCycleStatusesAsync(
        IReadOnlyList<string> cycleKeys, Guid? templateId, CancellationToken ct = default);

    /// <summary>حالة دورة أسبوعية موحّدة واحدة للمستخدم نفسه.</summary>
    Task<Result<UnifiedReportCycleStatusDto>> GetMyWeeklyCycleStatusAsync(
        string cycleKey, Guid? templateId, CancellationToken ct = default);
}

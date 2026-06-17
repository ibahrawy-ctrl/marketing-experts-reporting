using Reporting.Application.Common;

namespace Reporting.Application.Dashboard;

/// <summary>يبني عقد الداشبورد للمستخدم الحالي بناءً على الدور والتسلسل الإداري — لا تختار الواجهة النوع يدويًا.</summary>
public interface IDashboardService
{
    Task<Result<DashboardDto>> GetMineAsync(string? periodKey, CancellationToken ct = default);

    /// <summary>اتجاه KPI لشخص داخل نطاق المستخدم (الافتراضي: نفسه) — يمنع رؤية من هم خارج النطاق.</summary>
    Task<Result<KpiTrendDto>> GetKpiTrendsAsync(Guid? subjectId, CancellationToken ct = default);

    /// <summary>أداء أعضاء النطاق (فريق/قسم/شركة) — متوسط KPI واكتمال التقارير لكل عضو.</summary>
    Task<Result<IReadOnlyList<MemberPerformanceDto>>> GetMembersPerformanceAsync(CancellationToken ct = default);

    /// <summary>أحدث التسليمات داخل النطاق — خلاصة أنشطة.</summary>
    Task<Result<IReadOnlyList<ActivityItemDto>>> GetRecentActivityAsync(CancellationToken ct = default);

    /// <summary>التقارير التي لم تُسلَّم بعد أو تحتاج إجراء داخل النطاق للفترة.</summary>
    Task<Result<IReadOnlyList<PendingReportDto>>> GetPendingReportsAsync(string? periodKey, CancellationToken ct = default);

    /// <summary>الملف الموحّد لأداء موظّف واحد — محصور بنطاق المستخدم (403 خارج النطاق، 404 لو غير موجود).</summary>
    Task<Result<EmployeeProfileDto>> GetEmployeeProfileAsync(Guid userId, CancellationToken ct = default);
}

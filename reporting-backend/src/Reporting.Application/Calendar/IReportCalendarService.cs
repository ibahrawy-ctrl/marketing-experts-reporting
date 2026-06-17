using Reporting.Application.Common;

namespace Reporting.Application.Calendar;

/// <summary>
/// تقويم التقارير التشغيلي (Phase 5): كشف التقارير الأسبوعية الناقصة (§5)، وتأخّر الاعتماد بعد المهلة (§6)،
/// والتزام مندوبي المبيعات بالتقارير اليومية وتجميعها أسبوعيًّا (§9). كلّها مقيَّدة خادميًّا بنطاق المستخدم.
/// </summary>
public interface IReportCalendarService
{
    /// <summary>§5 — مَن كان متوقَّعًا منه تقرير أسبوعي ولم يُسلّم (أو تأخّر) لأسبوع معيّن، ضمن نطاق المستخدم.</summary>
    Task<Result<MissingReportsReport>> GetMissingReportsAsync(string? weekKey, CancellationToken ct = default);

    /// <summary>§6 — تقارير مُرسَلة لم تُراجَع بعد انتهاء مهلة المعتمِد الحالي — تُعرض للمستوى الأعلى فقط.</summary>
    Task<Result<ApprovalDelaysReport>> GetApprovalDelaysAsync(CancellationToken ct = default);

    /// <summary>§9 — التزام مندوبي المبيعات بالتقارير اليومية ضمن الأسبوع، وكشف الأسابيع الناقصة.</summary>
    Task<Result<SalesDailyComplianceReport>> GetSalesDailyComplianceAsync(string? weekKey, CancellationToken ct = default);
}

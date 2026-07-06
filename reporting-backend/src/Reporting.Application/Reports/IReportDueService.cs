using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// RPT-DUE1 — محرّك مواعيد التقارير والتأخّر (محسوب عند الطلب، بلا جدول/هجرة). قراءة فقط، لا إرسال بريد ولا إشعارات.
/// كلّ التواريخ/الاستحقاق/التأخّر بتوقيت الرياض عبر ReportCalendarPolicy. يميّز الأسبوعي عن اليومي (مبيعات B2C/B2B)
/// عبر ReportCadencePolicy؛ القاعدة مبنيّة على (نوع التقرير/القالب + الدور + الفريق/الإدارة) لا الدور وحده.
/// </summary>
public interface IReportDueService
{
    /// <summary>حالة تقرير الأسبوع الحالي للمستخدم نفسه (self-only، متاح للموظّف). يعكس اليومي للمبيعات.</summary>
    Task<Result<ReportDueMyStatus>> MyStatusAsync(string? weekKey, CancellationToken ct = default);

    /// <summary>نظرة عامة على مواعيد التقارير لأسبوع ضمن نطاق المستخدم (ScopeResolver) + فلاتر إدارة/فريق.</summary>
    Task<Result<ReportDueOverview>> OverviewAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default);

    /// <summary>قائمة التأخّر (تقارير غير مُسلَّمة + مراجعات متأخّرة) ضمن نطاق المستخدم + فلاتر.</summary>
    Task<Result<ReportDueOverdueReport>> OverdueAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default);
}

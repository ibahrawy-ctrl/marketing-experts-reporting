using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

// ===== RPT-DUE1 — مواعيد التقارير الأسبوعية ومحرّك التأخّر (محسوب عند الطلب، بلا جدول/هجرة) =====
// كلّ التواريخ/الاستحقاق/التأخّر بتوقيت الرياض عبر ReportCalendarPolicy. لا إرسال بريد ولا إنشاء إشعارات.

/// <summary>حالة تقرير الأسبوع الحالي للمستخدم نفسه (self-only، متاح للموظّف).</summary>
public record ReportDueMyStatus(
    string WeekKey,
    string WeekLabel,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    DateOnly EmployeeDueDate,
    bool Expected,
    bool Submitted,
    bool IsOverdue,
    DelayType DelayType,
    string StatusLabel,
    Guid? SubmissionId);

/// <summary>صفّ تأخّر واحد (تقرير غير مُسلَّم أو مراجعة متأخّرة) ضمن نطاق المستخدم.</summary>
public record ReportDueOverdueRow(
    Guid UserId,
    string UserName,
    string Role,
    string RoleLabel,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    DelayType DelayType,
    string ExpectedAction,
    DateOnly DueDate,
    int OverdueDays,
    Guid? RelatedSubmissionId);

/// <summary>نظرة عامة على مواعيد التقارير للأسبوع الحالي حسب نطاق المستخدم وصلاحيته.</summary>
public record ReportDueOverview(
    string WeekKey,
    string WeekLabel,
    string ScopeType,
    int RequiredReportsCount,
    int SubmittedReportsCount,
    int MissingReportsCount,
    int OverdueReportsCount,
    int PendingReviewsCount,
    int OverdueReviewsCount,
    IReadOnlyList<ReportDueOverdueRow> Items);

/// <summary>قائمة التأخّر (تقارير غير مُسلَّمة + مراجعات متأخّرة) ضمن نطاق المستخدم.</summary>
public record ReportDueOverdueReport(
    string WeekKey,
    int TotalCount,
    int OverdueReportsCount,
    int OverdueReviewsCount,
    IReadOnlyList<ReportDueOverdueRow> Rows);

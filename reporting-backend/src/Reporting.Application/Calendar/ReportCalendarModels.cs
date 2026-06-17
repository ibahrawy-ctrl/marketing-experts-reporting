using Reporting.Domain.Enums;

namespace Reporting.Application.Calendar;

// ===== تقويم التقارير (Phase 5) — كشف التقارير الناقصة وتأخّر الاعتماد والتزام المبيعات اليومي =====
// كل النواتج مقيَّدة خادميًّا بنطاق المستخدم الحالي (ScopeResolver). لا اعتماد على اسم مستخدم بعينه —
// المتوقَّع منهم تقريرٌ يُحدَّد بالمسمّى الوظيفي المربوط بقالب تقرير منشور أساسي.

/// <summary>صفّ موظّف متوقَّع منه تقرير أسبوعي: حالته (مُسلَّم/متأخّر/ناقص) وتاريخ التسليم المتوقَّع.</summary>
public record ExpectedReporterRow(
    Guid UserId,
    string FullName,
    string RoleLabel,
    PeriodType ExpectedCadence,
    Guid? TeamId,
    string? TeamName,
    string Status,            // "submitted" | "late" | "missing" | "leave" (إجازة معتمدة)
    DateOnly DueDate,
    DateTime? SubmittedAtUtc);

/// <summary>قصور فريق: عدد المتوقَّع منهم مقابل الناقص والمتأخّر داخل الفريق.</summary>
public record TeamShortfallRow(Guid? TeamId, string TeamName, int Expected, int Missing, int Late);

/// <summary>تقرير التقارير الناقصة لأسبوع تشغيلي (الخميس → الأربعاء).</summary>
public record MissingReportsReport(
    string PeriodKey,
    string PeriodLabel,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int ExpectedCount,
    int SubmittedCount,
    int LateCount,
    int MissingCount,
    string ScopeType,
    bool CanViewRows,
    IReadOnlyList<ExpectedReporterRow> Rows,
    IReadOnlyList<TeamShortfallRow> TeamShortfalls,
    // عدد من استُثنوا بإجازة معتمدة تغطّي الأسبوع (V1.0.1) — لا يُحتسبون ناقصين.
    int LeaveCount = 0);

/// <summary>صفّ تأخّر اعتماد: تقرير مُرسَل لم يُراجَع بعد انتهاء مهلة المعتمِد الحالي.</summary>
public record ApprovalDelayRow(
    Guid SubmissionId,
    Guid SubmitterId,
    string SubmitterName,
    string TemplateTitle,
    string PeriodKey,
    SubmissionStatus Status,
    Guid ApproverId,
    string ApproverName,
    string ApproverRoleLabel,
    DateOnly DueDate,
    int DaysOverdue,
    DateTime? SubmittedAtUtc);

/// <summary>تقرير تأخّر الاعتماد — يُعرض للمستوى الأعلى من المعتمِد المتأخّر فقط، ضمن النطاق.</summary>
public record ApprovalDelaysReport(
    string ScopeType,
    int DelayCount,
    IReadOnlyList<ApprovalDelayRow> Rows);

/// <summary>التزام مندوب مبيعات بالتقارير اليومية ضمن الأسبوع التشغيلي.</summary>
public record SalesDailyComplianceRow(
    Guid UserId,
    string FullName,
    Guid? TeamId,
    string? TeamName,
    int ExpectedDays,
    int SubmittedDays,
    int MissingDays,
    bool IsComplete,
    bool NeedsReview,
    // أيام الإجازة المعتمدة ضمن النافذة المنقضية (V1.0.1) — مستثناة من الأيام المتوقَّعة.
    int LeaveDays = 0);

/// <summary>تجميع تقارير المبيعات اليومية إلى عرض أسبوعي + كشف الأسابيع الناقصة (تحتاج مراجعة).</summary>
public record SalesDailyComplianceReport(
    string PeriodKey,
    string PeriodLabel,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int ReportersCount,
    int CompleteCount,
    int IncompleteCount,
    string ScopeType,
    bool CanViewRows,
    IReadOnlyList<SalesDailyComplianceRow> Rows);

using Reporting.Domain.Enums;

namespace Reporting.Application.Payroll;

// ===== FIN-L1 — عرض التأثير على الرواتب =====
// عرض إعلامي بحت على مستوى الشركة (محكوم بسياسة PayrollImpactRead) لطلبات الإجازة/الاستئذان المؤثّرة على
// الراتب. لا يعدّل الطلب الأصلي ولا حالته ولا يُجري أيّ خصم آليّ. المراجعة المالية (PATCH) تغيّر حالة المراجعة
// والملاحظة فقط (Admin/HR). الطلب المؤثّر بلا صفّ مراجعة = Pending ضمنيًّا (يُنشأ الصفّ كسولًا عند أوّل مراجعة).

/// <summary>معايير فلترة قائمة التأثير على الراتب. الافتراضي: الطلبات المعتمَدة نهائيًّا (HrApproved) فقط.</summary>
public record PayrollImpactFilter(
    int? Year = null,
    int? Month = null,
    Guid? EmployeeUserId = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    LeaveRequestType? Type = null,
    LeaveRequestStatus? ApprovalStatus = null,
    // عند true تُعرَض كل الحالات (بما فيها المرفوضة/الملغاة)؛ الافتراضي HrApproved فقط.
    bool AllApprovalStatuses = false,
    PayrollImpactType? ImpactType = null,
    PayrollImpactReviewStatus? ReviewStatus = null);

/// <summary>بطاقات تلخيصية للمجموعة المفلترة.</summary>
public record PayrollImpactSummaryDto(
    int TotalImpacted,
    decimal TotalUncoveredLeaveDays,
    int TotalImpactedPermissions,
    int AfterHoursCompensationRequests,
    int NeedsFinanceReviewCount);

/// <summary>صفّ في قائمة التأثير على الراتب (لقطة الطلب + حالة المراجعة المالية).</summary>
public record PayrollImpactListItemDto(
    Guid LeaveRequestId,
    Guid RequesterUserId,
    string RequesterName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    LeaveRequestType Type,
    PayrollImpactType ImpactType,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    LeaveRequestStatus ApprovalStatus,
    // لقطة الرصيد وقت الطلب (FIN-L1 data من حارس الرصيد/ESS-L2).
    decimal? BalanceAtRequest,
    int? RequestedLeaveDays,
    decimal? UncoveredLeaveDays,
    bool IsPotentialUnpaidLeave,
    bool EmployeeAcknowledgedUnpaidDeduction,
    DateTime? EmployeeAcknowledgedAtUtc,
    PermissionShortfallResolution PermissionShortfallResolution,
    // حالة المراجعة المالية (Pending ضمنيًّا إن لم يوجد صفّ مراجعة).
    PayrollImpactReviewStatus ReviewStatus,
    string? FinanceNote,
    Guid? ReviewedByUserId,
    string? ReviewedByName,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>قائمة التأثير على الراتب: بطاقات تلخيصية + صفوف.</summary>
public record PayrollImpactListDto(
    PayrollImpactSummaryDto Summary,
    IReadOnlyList<PayrollImpactListItemDto> Items);

/// <summary>تفاصيل طلب مؤثّر على الراتب (للوحة الجانبية) = صفّ القائمة + سبب الطلب/الملاحظات + قدرة المراجعة.</summary>
public record PayrollImpactDetailDto(
    PayrollImpactListItemDto Item,
    string Reason,
    string? Notes,
    // هل يملك المستخدم الحالي صلاحية تحديث المراجعة المالية (Admin/HR).
    bool CanManage);

/// <summary>تحديث المراجعة المالية: الحالة + ملاحظة اختيارية. لا يمسّ الطلب الأصلي ولا حالته ولا الراتب.</summary>
public record PayrollImpactReviewRequest(
    PayrollImpactReviewStatus Status,
    string? FinanceNote);

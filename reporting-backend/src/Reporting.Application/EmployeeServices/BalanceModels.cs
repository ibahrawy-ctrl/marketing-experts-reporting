using Reporting.Domain.Enums;

namespace Reporting.Application.EmployeeServices;

// ===== V1.1 — أرصدة الإجازات والأذونات (خدمات الموظف) =====
// كل النواتج مقيَّدة خادميًّا: الموظّف يرى رصيده فقط؛ الإدارة/HR (BalanceManagement) ترى الجميع.
// الرصيد مشتقّ من الحركات (Ledger) لا قيمة مخزّنة. لا حذف لحركة؛ التصحيح بإضافة حركة معاكسة.

/// <summary>ملخّص رصيد نوع واحد (إجازة أو إذن) لسنة محددة.</summary>
public record BalanceSummaryDto(
    BalanceType BalanceType,
    decimal Credited,
    decimal Debited,
    decimal Remaining,
    bool IsNegative);

/// <summary>أرصدتي (للموظّف نفسه) لسنة محددة + الطلبات المعلّقة + حدود الأذونات إن وُجدت سياسة.</summary>
public record MyBalancesDto(
    int Year,
    BalanceSummaryDto AnnualLeave,
    BalanceSummaryDto Permission,
    int PendingLeaveRequests,
    PermissionUnit PermissionUnit,
    decimal? PermissionMonthlyLimit,
    decimal? PermissionAnnualLimit,
    bool AllowNegativeBalance,
    bool HasPolicy,
    // الأذونات المستخدَمة والمتبقية ضمن الشهر الحالي (UTC) — تُحسَب فقط حين يوجد حدّ شهري.
    int? PermissionUsedThisMonth,
    int? PermissionRemainingThisMonth);

/// <summary>صفّ موظّف في شاشة أرصدة الإدارة/HR.</summary>
public record EmployeeBalanceRowDto(
    Guid EmployeeId,
    string EmployeeName,
    string? JobTitle,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    int Year,
    decimal AnnualLeaveRemaining,
    decimal PermissionRemaining,
    bool AnnualLeaveNegative,
    bool PermissionNegative);

/// <summary>حركة واحدة في سجلّ رصيد موظّف.</summary>
public record BalanceLedgerEntryDto(
    Guid Id,
    BalanceType BalanceType,
    decimal Amount,
    BalanceDirection Direction,
    BalanceSource Source,
    Guid? RelatedRequestId,
    int Year,
    string? Notes,
    Guid CreatedBy,
    string? CreatedByName,
    DateTime CreatedAtUtc);

/// <summary>سجلّ رصيد موظّف لسنة + ملخّصاته.</summary>
public record EmployeeLedgerDto(
    Guid EmployeeId,
    string EmployeeName,
    int Year,
    BalanceSummaryDto AnnualLeave,
    BalanceSummaryDto Permission,
    IReadOnlyList<BalanceLedgerEntryDto> Entries);

/// <summary>رصيد افتتاحي لموظّف (Credit، Source=OpeningBalance).</summary>
public record OpeningBalanceRequest(
    BalanceType BalanceType,
    decimal Amount,
    int Year,
    string? Notes);

/// <summary>تعديل يدوي على رصيد موظّف (Credit أو Debit، Source=ManualAdjustment، سبب إلزامي).</summary>
public record BalanceAdjustmentRequest(
    BalanceType BalanceType,
    BalanceDirection Direction,
    decimal Amount,
    int Year,
    string Reason);

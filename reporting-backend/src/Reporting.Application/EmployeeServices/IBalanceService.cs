using Reporting.Application.Common;

namespace Reporting.Application.EmployeeServices;

/// <summary>
/// أرصدة الإجازات والأذونات (V1.1). الرصيد مشتقّ من حركات Ledger. كل العمليات تفرض الصلاحية خادميًّا:
/// الموظّف يرى رصيده فقط؛ الإدارة/HR (BalanceManagement) ترى/تُدير الجميع. لا حذف لحركة؛ التصحيح بإضافة معاكسة.
/// الخصم الآلي عند HrApproved والعكس عند إلغاء معتمد يقعان داخل خدمة الإجازات لا هنا.
/// </summary>
public interface IBalanceService
{
    /// <summary>أرصدة المستخدم الحالي (هو صاحبها) لسنة (افتراضي: السنة الحالية).</summary>
    Task<Result<MyBalancesDto>> GetMyBalancesAsync(int? year, CancellationToken ct = default);

    /// <summary>قائمة الموظّفين وأرصدتهم (بحث/تصفية) — BalanceManagement.</summary>
    Task<Result<IReadOnlyList<EmployeeBalanceRowDto>>> ListEmployeesAsync(
        string? q, Guid? departmentId, Guid? teamId, int? year, CancellationToken ct = default);

    /// <summary>سجلّ حركات رصيد موظّف لسنة — BalanceManagement.</summary>
    Task<Result<EmployeeLedgerDto>> GetEmployeeLedgerAsync(Guid userId, int? year, CancellationToken ct = default);

    /// <summary>رصيد افتتاحي لموظّف (Credit) — BalanceManagement + Audit.</summary>
    Task<Result<EmployeeLedgerDto>> SetOpeningBalanceAsync(Guid userId, OpeningBalanceRequest request, CancellationToken ct = default);

    /// <summary>تعديل يدوي على رصيد موظّف — BalanceManagement + سبب إلزامي + Audit.</summary>
    Task<Result<EmployeeLedgerDto>> AdjustAsync(Guid userId, BalanceAdjustmentRequest request, CancellationToken ct = default);
}

using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.EmployeeServices;

/// <summary>
/// حركة رصيد إجازات/أذونات لموظف (V1.1 — خدمات الموظف). الرصيد مشتقّ من الأحداث:
/// لا قيمة مخزّنة؛ المتبقّي = Σ(Credit.Amount) − Σ(Debit.Amount) على نفس (EmployeeId, BalanceType, Year).
/// لا تُحذف حركة أبدًا — التصحيح يكون بإضافة حركة معاكسة (Reversal/ManualAdjustment).
/// </summary>
public class EmployeeBalanceLedger : BaseEntity
{
    /// <summary>صاحب الرصيد.</summary>
    public Guid EmployeeId { get; set; }

    public BalanceType BalanceType { get; set; }

    /// <summary>المقدار موجب دائمًا؛ الاتجاه (Direction) يحدّد الإشارة.</summary>
    public decimal Amount { get; set; }

    public BalanceDirection Direction { get; set; }

    public BalanceSource Source { get; set; }

    /// <summary>
    /// يشير إلى leave_requests.Id عند المصدر الآلي (ApprovedLeave/ApprovedPermission/Reversal).
    /// بلا FK صلب — إضافي وآمن. يُستخدم مع Source لمنع الازدواج.
    /// </summary>
    public Guid? RelatedRequestId { get; set; }

    /// <summary>سنة الرصيد (تُشتقّ من StartDate للطلب الآلي).</summary>
    public int Year { get; set; }

    /// <summary>سبب التعديل اليدوي/العكس (إلزامي للتعديل اليدوي والعكس).</summary>
    public string? Notes { get; set; }

    /// <summary>منفّذ الحركة (للحركة الآلية = منفّذ الاعتماد النهائي).</summary>
    public Guid CreatedBy { get; set; }
}

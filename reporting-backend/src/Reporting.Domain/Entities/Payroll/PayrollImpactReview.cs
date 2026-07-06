using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Payroll;

/// <summary>
/// مراجعة مالية إعلامية لطلب إجازة/استئذان مؤثّر على الراتب (FIN-L1 — عرض التأثير على الرواتب).
/// كيان منفصل تمامًا عن LeaveRequest: لا يعدّل الطلب الأصلي ولا يغيّر حالته ولا يُجري أيّ خصم آليّ على الراتب.
/// لا يُنشأ صفّ تلقائيًّا لكل طلب — يُنشأ كسولًا (Lazy) عند أوّل مراجعة مالية (PATCH). الطلب بلا صفّ = Pending ضمنيًّا.
/// مرتبط بطلب واحد (LeaveRequestId فريد) بـ FK Restrict (لا Cascade) — لا يُحذف الطلب الأصلي عبر هذا الكيان.
/// </summary>
public class PayrollImpactReview : BaseEntity
{
    /// <summary>الطلب المؤثّر على الراتب الذي تخصّه هذه المراجعة (فريد — مراجعة واحدة لكل طلب).</summary>
    public Guid LeaveRequestId { get; set; }

    /// <summary>حالة المراجعة المالية. الافتراضي عند أوّل إنشاء = Pending.</summary>
    public PayrollImpactReviewStatus Status { get; set; } = PayrollImpactReviewStatus.Pending;

    /// <summary>ملاحظة المالية (اختيارية).</summary>
    public string? FinanceNote { get; set; }

    /// <summary>من أجرى آخر مراجعة مالية.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>وقت آخر مراجعة مالية (UTC).</summary>
    public DateTime? ReviewedAtUtc { get; set; }
}

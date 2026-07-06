using Reporting.Application.Common;

namespace Reporting.Application.Payroll;

/// <summary>
/// خدمة «عرض التأثير على الرواتب» (FIN-L1). عرض إعلامي بحت على مستوى الشركة لطلبات الإجازة/الاستئذان
/// المؤثّرة على الراتب + مراجعة مالية (حالة/ملاحظة). لا تعدّل الطلب الأصلي ولا حالته ولا تُجري خصمًا آليًّا،
/// ولا تستدعي ScopeResolver (الحماية عبر سياستَي PayrollImpactRead/PayrollImpactManage عند نقطة النهاية).
/// </summary>
public interface IPayrollImpactService
{
    /// <summary>قائمة الطلبات المؤثّرة على الراتب + بطاقات تلخيصية، حسب المعايير (الافتراضي HrApproved فقط).</summary>
    Task<Result<PayrollImpactListDto>> ListAsync(PayrollImpactFilter filter, CancellationToken ct = default);

    /// <summary>تفاصيل طلب مؤثّر واحد (للوحة الجانبية).</summary>
    Task<Result<PayrollImpactDetailDto>> GetByIdAsync(Guid leaveRequestId, CancellationToken ct = default);

    /// <summary>تحديث المراجعة المالية لطلب مؤثّر (Admin/HR) — يُنشئ صفّ المراجعة كسولًا أو يحدّثه.</summary>
    Task<Result<PayrollImpactDetailDto>> ReviewAsync(Guid leaveRequestId, PayrollImpactReviewRequest request, CancellationToken ct = default);
}

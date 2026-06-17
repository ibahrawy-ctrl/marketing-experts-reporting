using Reporting.Application.Common;

namespace Reporting.Application.Leave;

/// <summary>
/// إدارة طلبات الإجازة والاستئذان واعتمادها هرميًّا. كل العمليات تفرض النطاق والدور خادميًّا
/// (لا اكتفاء بإخفاء أزرار الواجهة): الموظّف يرى طلباته فقط ولا يعتمد طلبه؛ قائد الفريق يرى فريقه فقط؛
/// المدير يرى ما اعتمده القادة ضمن نطاقه؛ الموارد البشرية تعتمد نهائيًّا (سياسة LeaveFinalApproval).
/// </summary>
public interface ILeaveRequestService
{
    /// <summary>طلبات المستخدم الحالي (هو صاحبها).</summary>
    Task<Result<IReadOnlyList<LeaveRequestListItemDto>>> GetMyAsync(CancellationToken ct = default);

    /// <summary>الطلبات التي تنتظر قرار المستخدم الحالي حسب دوره ونطاقه.</summary>
    Task<Result<IReadOnlyList<LeaveRequestListItemDto>>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>تفاصيل طلب — للمالك أو لمن له صلاحية مراجعته ضمن نطاقه.</summary>
    Task<Result<LeaveRequestDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>إنشاء طلب جديد (الموظّف لنفسه).</summary>
    Task<Result<LeaveRequestDto>> CreateAsync(CreateLeaveRequestRequest request, CancellationToken ct = default);

    /// <summary>إلغاء الطلب من صاحبه قبل الاعتماد النهائي.</summary>
    Task<Result<LeaveRequestDto>> CancelAsync(Guid id, CancellationToken ct = default);

    Task<Result<LeaveRequestDto>> TeamLeaderApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> TeamLeaderRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> ManagerApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> ManagerRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> HrApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> HrRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default);

    /// <summary>إعادة الطلب للموظّف للتعديل (من أي مراجِع ضمن نطاقه).</summary>
    Task<Result<LeaveRequestDto>> ReturnAsync(Guid id, LeaveReturnRequest request, CancellationToken ct = default);
}

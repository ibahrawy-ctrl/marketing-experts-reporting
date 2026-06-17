using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Leave;

/// <summary>
/// حدث في الخطّ الزمني لطلب إجازة/استئذان: من فعل، ومتى، والإجراء، والمرحلة، والحالة قبل/بعد، وتعليق اختياري.
/// سجلّ غير قابل للتعديل يوثّق كل قرار في مسار الاعتماد.
/// </summary>
public class LeaveRequestEvent : BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public Guid ActorUserId { get; set; }

    /// <summary>الإجراء: submitted / team_leader_approved / manager_rejected / returned / cancelled ... إلخ.</summary>
    public string Action { get; set; } = string.Empty;

    public LeaveRequestStep Step { get; set; }
    public LeaveRequestStatus FromStatus { get; set; }
    public LeaveRequestStatus ToStatus { get; set; }

    public string? Comment { get; set; }
}

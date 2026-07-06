using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.EmployeeServices;

/// <summary>
/// حدث في الخطّ الزمني لطلب موارد بشرية عام (يحاكي LeaveRequestEvent): من فعل، الإجراء، الحالة قبل/بعد، تعليق.
/// سجلّ غير قابل للتعديل. لا بيانات حساسة (لا محتوى مرفقات).
/// </summary>
public class EmployeeServiceRequestEvent : BaseEntity
{
    public Guid EmployeeServiceRequestId { get; set; }
    public Guid ActorUserId { get; set; }

    /// <summary>الإجراء: created / in_review / completed / rejected / cancelled / commented.</summary>
    public string Action { get; set; } = string.Empty;

    public EmployeeServiceRequestStatus FromStatus { get; set; }
    public EmployeeServiceRequestStatus ToStatus { get; set; }

    public string? Comment { get; set; }
}

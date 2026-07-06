using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Positions;

/// <summary>
/// إسناد منصب مرن إلى مستخدم. مستقلّ تمامًا عن أدوار Identity وعن ManagerId.
/// إلغاء الإسناد (حذف السطر) أو تعطيل المنصب يُزيل أثر الرؤية فورًا.
/// </summary>
public class UserPosition : BaseEntity
{
    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    /// <summary>مرجع المستخدم المُسنَد إليه المنصب (AspNetUsers.Id).</summary>
    public Guid UserId { get; set; }

    /// <summary>المستخدم الذي نفّذ الإسناد (للتدقيق).</summary>
    public Guid AssignedBy { get; set; }
}

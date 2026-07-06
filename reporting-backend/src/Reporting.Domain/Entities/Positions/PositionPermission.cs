using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Positions;

/// <summary>
/// صلاحية رؤية واحدة مرتبطة بمنصب. PermissionKey نصّ يُتحقَّق منه مقابل قائمة ثابتة في الكود
/// (PositionPermissions) — لا جدول صلاحيات منفصل في Phase 1A.
/// </summary>
public class PositionPermission : BaseEntity
{
    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    /// <summary>مفتاح الصلاحية (مثل reports.view، dashboard.view).</summary>
    public string PermissionKey { get; set; } = string.Empty;
}

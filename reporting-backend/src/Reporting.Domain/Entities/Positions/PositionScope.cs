using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Positions;

/// <summary>
/// نطاق رؤية واحد مرتبط بمنصب. حسب Kind:
/// Department ⇒ DepartmentId مطلوب، Team ⇒ TeamId مطلوب،
/// SpecificUsers ⇒ TargetUserId مطلوب (سطر لكل مستخدم)، AllCompany ⇒ بلا مرجع.
/// </summary>
public class PositionScope : BaseEntity
{
    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    public PositionScopeKind Kind { get; set; }

    /// <summary>مطلوب حين Kind = Department.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>مطلوب حين Kind = Team.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>مطلوب حين Kind = SpecificUsers (مستخدم واحد لكل سطر).</summary>
    public Guid? TargetUserId { get; set; }
}

using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Org;

/// <summary>إدارة/قسم داخل الشركة.</summary>
public class Department : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Team> Teams { get; set; } = new List<Team>();
}

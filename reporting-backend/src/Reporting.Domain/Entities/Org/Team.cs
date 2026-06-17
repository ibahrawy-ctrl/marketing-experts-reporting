using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Org;

/// <summary>فريق عمل ضمن إدارة، يقوده قائد فريق.</summary>
public class Team : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? TeamLeaderId { get; set; }
    public bool IsActive { get; set; } = true;
}

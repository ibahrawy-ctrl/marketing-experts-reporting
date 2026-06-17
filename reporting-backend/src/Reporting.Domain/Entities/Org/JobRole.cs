using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Org;

/// <summary>المسمى الوظيفي (مشتري إعلانات، كاتب محتوى، …) تُربط به قوالب التقارير.</summary>
public class JobRole : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

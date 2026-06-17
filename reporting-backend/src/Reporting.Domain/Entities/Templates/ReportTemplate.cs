using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Templates;

/// <summary>قالب تقرير مرتبط بمسمى وظيفي؛ له إصدارات متعددة.</summary>
public class ReportTemplate : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? JobRoleId { get; set; }
    public PeriodType DefaultPeriodType { get; set; } = PeriodType.Weekly;
    // تصنيف الإلزام: أساسي (إلزامي) أو تكميلي (اختياري) — يمنع ازدواج التقارير الأسبوعية الإلزامية.
    public TemplateClassification Classification { get; set; } = TemplateClassification.Primary;
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
    public Guid OwnerId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ReportTemplateVersion> Versions { get; set; } = new List<ReportTemplateVersion>();
}

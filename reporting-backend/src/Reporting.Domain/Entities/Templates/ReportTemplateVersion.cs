using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Templates;

/// <summary>إصدار من قالب التقرير؛ التسليمات ترتبط بإصدار محدد لضمان ثبات البنية.</summary>
public class ReportTemplateVersion : BaseEntity
{
    public Guid ReportTemplateId { get; set; }
    public ReportTemplate? ReportTemplate { get; set; }
    public int VersionNumber { get; set; } = 1;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }

    public ICollection<TemplateField> Fields { get; set; } = new List<TemplateField>();
}

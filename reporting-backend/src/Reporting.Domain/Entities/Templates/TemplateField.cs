using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Templates;

/// <summary>حقل ضمن إصدار قالب؛ الإعدادات المرنة (خيارات/حدود) تُخزَّن JSONB.</summary>
public class TemplateField : BaseEntity
{
    public Guid ReportTemplateVersionId { get; set; }
    public ReportTemplateVersion? ReportTemplateVersion { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Key { get; set; }
    public FieldType FieldType { get; set; } = FieldType.ShortText;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? HelpText { get; set; }
    /// <summary>إعدادات الحقل المرنة (options[], min, max, decimals, gridColumns…) كـ JSONB.</summary>
    public string? ConfigJson { get; set; }
}

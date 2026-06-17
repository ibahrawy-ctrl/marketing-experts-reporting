using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Submissions;

/// <summary>قيمة حقل ضمن تسليم؛ تُخزَّن نصيًا/رقميًا/JSONB حسب نوع الحقل.</summary>
public class SubmissionFieldValue : BaseEntity
{
    public Guid ReportSubmissionId { get; set; }
    public ReportSubmission? ReportSubmission { get; set; }
    public Guid TemplateFieldId { get; set; }

    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTime? ValueDate { get; set; }
    public bool? ValueBool { get; set; }
    /// <summary>للقيم المركبة (multi-select / table-grid / مرفقات) كـ JSONB.</summary>
    public string? ValueJson { get; set; }
}

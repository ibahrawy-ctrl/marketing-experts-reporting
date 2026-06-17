using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Submissions;

/// <summary>خطوة اعتماد ضمن سلسلة الموافقة (مستوى → موافِق → قرار).</summary>
public class ApprovalStep : BaseEntity
{
    public Guid ReportSubmissionId { get; set; }
    public ReportSubmission? ReportSubmission { get; set; }
    public int Level { get; set; }
    public Guid ApproverId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Comment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}

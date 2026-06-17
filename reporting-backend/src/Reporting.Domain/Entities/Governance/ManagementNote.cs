using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// ملاحظة إدارية مرتبطة بالسياق: تقرير، موظف، KPI، فريق، تصعيد، قرار، أو مخاطرة.
/// لها كاتب وتاريخ ونوع، وقد تتطلّب إجراءً أو تكون مجرد توثيق.
/// </summary>
public class ManagementNote : BaseEntity
{
    public ManagementNoteEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid AuthorId { get; set; }
    public ManagementNoteType NoteType { get; set; } = ManagementNoteType.Documentation;
    public string Body { get; set; } = string.Empty;
    public bool RequiresAction { get; set; }
    public ManagementNoteStatus Status { get; set; } = ManagementNoteStatus.Open;
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

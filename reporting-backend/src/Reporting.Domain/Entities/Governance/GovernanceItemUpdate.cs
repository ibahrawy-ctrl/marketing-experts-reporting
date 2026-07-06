using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// حركة في الخط الزمني لبند الحوكمة: تعليق، تغيير حالة، إعادة إسناد، ملاحظة متابعة، تعديل. تُسجَّل «من غيّر ماذا
/// ومتى» (AuthorId + CreatedAtUtc) مع حالة قبل/بعد عند تغيير الحالة. ابن لـ GovernanceItem (حذف متتالٍ).
/// </summary>
public class GovernanceItemUpdate : BaseEntity
{
    public Guid GovernanceItemId { get; set; }
    public GovernanceItem? GovernanceItem { get; set; }

    public Guid AuthorId { get; set; }
    public GovernanceItemUpdateType UpdateType { get; set; } = GovernanceItemUpdateType.Comment;

    /// <summary>نصّ التعليق/الملاحظة (اختياري لحركات الحالة الآلية).</summary>
    public string? Body { get; set; }

    /// <summary>الحالة قبل/بعد عند حركة تغيير الحالة (للتتبّع).</summary>
    public GovernanceItemStatus? OldStatus { get; set; }
    public GovernanceItemStatus? NewStatus { get; set; }
}

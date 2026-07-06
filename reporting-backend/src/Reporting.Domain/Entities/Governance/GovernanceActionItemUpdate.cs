using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// حركة في الخط الزمني لإجراء الحوكمة: إنشاء/تعليق/تغيير حالة/تغيير تاريخ استحقاق/تغيير مُسنَد إليه/تقديم إكمال/إعادة فتح/إلغاء.
/// تُسجَّل «من غيّر ماذا ومتى» (AuthorId + CreatedAtUtc) مع حالة قبل/بعد عند تغيير الحالة. ابن لـ GovernanceActionItem (حذف متتالٍ).
/// </summary>
public class GovernanceActionItemUpdate : BaseEntity
{
    public Guid ActionItemId { get; set; }
    public GovernanceActionItem? ActionItem { get; set; }

    public Guid AuthorId { get; set; }
    public ActionItemUpdateType UpdateType { get; set; } = ActionItemUpdateType.Comment;

    /// <summary>نصّ التعليق/الملاحظة (اختياري لحركات الحالة/الإسناد الآلية).</summary>
    public string? Body { get; set; }

    /// <summary>الحالة قبل/بعد عند حركة تغيير الحالة (للتتبّع).</summary>
    public ActionItemStatus? OldStatus { get; set; }
    public ActionItemStatus? NewStatus { get; set; }
}

using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>
/// حركة في الخط الزمني للتصعيد الفردي: إنشاء/تعليق/تغيير حالة/إسناد/إعادة فتح/تعديل/إغلاق. تُسجَّل «من غيّر ماذا
/// ومتى» (AuthorId + CreatedAtUtc) مع حالة قبل/بعد عند تغيير الحالة. ابن لـ GovernanceEscalation (حذف متتالٍ).
/// </summary>
public class GovernanceEscalationUpdate : BaseEntity
{
    public Guid EscalationId { get; set; }
    public GovernanceEscalation? Escalation { get; set; }

    public Guid AuthorId { get; set; }
    public EscalationUpdateType UpdateType { get; set; } = EscalationUpdateType.Comment;

    /// <summary>نصّ التعليق/الملاحظة (اختياري لحركات الحالة/الإسناد الآلية).</summary>
    public string? Body { get; set; }

    /// <summary>الحالة قبل/بعد عند حركة تغيير الحالة (للتتبّع).</summary>
    public GovernanceEscalationStatus? OldStatus { get; set; }
    public GovernanceEscalationStatus? NewStatus { get; set; }
}

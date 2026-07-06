using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Positions;

/// <summary>
/// منصب مرن (Phase 1A — رؤية فقط) مستقلّ عن أدوار Identity وعن شجرة ManagerId.
/// يجمع مجموعة صلاحيات رؤية + نطاقات رؤية، ويُسنَد إلى مستخدمين عبر UserPosition.
/// لا يمنح أي قدرة اعتماد/كتابة — يوسّع نطاق الرؤية فقط عبر ScopeResolver (اتحاد).
/// </summary>
public class Position : BaseEntity
{
    /// <summary>رمز فريد للمنصب (مثل QUALITY_VIEWER).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم المعروض بالعربية.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>وصف اختياري.</summary>
    public string? Description { get; set; }

    /// <summary>تعطيل المنصب يُلغي أثره فورًا (دون حذف الإسناد).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<PositionPermission> Permissions { get; set; } = new List<PositionPermission>();
    public ICollection<PositionScope> Scopes { get; set; } = new List<PositionScope>();
    public ICollection<UserPosition> Assignments { get; set; } = new List<UserPosition>();
}

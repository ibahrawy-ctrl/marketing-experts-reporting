using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.System;

/// <summary>إشعار لمستخدم (تسليم جديد، اعتماد، تصعيد…)، يُدفع لحظيًا عبر SignalR.</summary>
public class Notification : BaseEntity
{
    public Guid RecipientId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
}

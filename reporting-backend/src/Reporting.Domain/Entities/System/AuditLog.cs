using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.System;

/// <summary>سجل تدقيق غير قابل للتعديل لكل إجراء حسّاس في النظام.</summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    /// <summary>لقطة البيانات/التغيير كـ JSONB.</summary>
    public string? DataJson { get; set; }
    public string? IpAddress { get; set; }
}

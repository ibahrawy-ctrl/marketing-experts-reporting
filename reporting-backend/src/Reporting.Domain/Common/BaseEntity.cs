namespace Reporting.Domain.Common;

/// <summary>
/// قاعدة كل كيانات النظام — مفتاح أساسي GUID دون استثناء (عدا جداول Microsoft Identity).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

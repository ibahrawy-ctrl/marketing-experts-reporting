using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// عميل/حساب تتعامل معه الوكالة (Phase 6 — بُعد العميل والمشروع).
/// يربط الأداء التشغيلي بأداء العميل عبر المشاريع المرتبطة به.
/// </summary>
public class Client : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ClientStatus Status { get; set; } = ClientStatus.Active;

    /// <summary>مدير الحساب المسؤول (مرجع مستخدم — ليس دورًا نظاميًا).</summary>
    public Guid? AccountManagerId { get; set; }

    public string? MainContactName { get; set; }
    public string? MainContactInfo { get; set; }
    public string? Notes { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

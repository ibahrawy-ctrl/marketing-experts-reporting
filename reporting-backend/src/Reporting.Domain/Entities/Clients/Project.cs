using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// مشروع/خدمة يقدَّم لعميل (Phase 6). يربط مخرجات الفِرق بالعميل ونوع الخدمة.
/// </summary>
public class Project : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;
    public ServiceType ServiceType { get; set; } = ServiceType.Other;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>الفريق المسؤول عن تنفيذ المشروع.</summary>
    public Guid? OwnerTeamId { get; set; }
    /// <summary>مدير الحساب المسؤول عن المشروع (مرجع مستخدم).</summary>
    public Guid? AccountManagerId { get; set; }

    public string? Notes { get; set; }
}

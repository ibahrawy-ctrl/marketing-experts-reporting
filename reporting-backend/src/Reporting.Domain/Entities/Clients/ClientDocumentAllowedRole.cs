using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// دور مسموح له برؤية مستند بعينه (CPW-R2 — يُستخدَم حصرًا مع <c>DocumentVisibilityType.CustomRoles</c>).
/// <c>RoleName</c> نصّ يطابق ثوابت <c>Roles</c>، ويُتحقَّق منه خادميًّا عند الكتابة (لا FK إلى AspNetRoles
/// اتّساقًا مع نمط النظام الذي يخزّن أسماء الأدوار نصًّا).
/// </summary>
public class ClientDocumentAllowedRole : BaseEntity
{
    public Guid ClientDocumentId { get; set; }
    public ClientDocument? Document { get; set; }

    public string RoleName { get; set; } = string.Empty;
}

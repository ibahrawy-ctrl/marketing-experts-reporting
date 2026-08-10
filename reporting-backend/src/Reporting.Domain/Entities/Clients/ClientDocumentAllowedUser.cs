using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// مستخدم مسموح له برؤية مستند بعينه (CPW-R2 — يُستخدَم حصرًا مع <c>DocumentVisibilityType.CustomUsers</c>).
/// بلا مفتاح أجنبيّ إلزاميّ إلى <c>AspNetUsers</c>، اتّساقًا مع نمط النظام القائم
/// (<c>UploadedByUserId</c>/<c>ArchivedByUserId</c> معرّفات مجرّدة بلا FK).
/// </summary>
public class ClientDocumentAllowedUser : BaseEntity
{
    public Guid ClientDocumentId { get; set; }
    public ClientDocument? Document { get; set; }

    public Guid UserId { get; set; }
}

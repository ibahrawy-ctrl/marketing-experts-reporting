namespace Reporting.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? Link,
    bool IsRead,
    DateTime CreatedAtUtc);

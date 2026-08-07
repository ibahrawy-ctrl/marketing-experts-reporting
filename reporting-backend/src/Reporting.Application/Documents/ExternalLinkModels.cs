namespace Reporting.Application.Documents;

// ===== Client External Links (CPW-R1B2 — «الروابط المهمّة») =====
// الرابط مرجع عنوان فقط. أيّ رابط يحمل ما يشبه سرًّا يُرفَض في ExternalLinkPolicy.

public record ClientExternalLinkDto(
    Guid Id,
    Guid ClientId,
    string Title,
    string Url,
    string CategoryCode,
    string? Description,
    bool IsActive,
    int SortOrder,
    Guid CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateClientExternalLinkRequest(
    string Title,
    string Url,
    string CategoryCode,
    string? Description = null,
    int SortOrder = 0);

public record UpdateClientExternalLinkRequest(
    string Title,
    string Url,
    string CategoryCode,
    string? Description = null,
    int SortOrder = 0);

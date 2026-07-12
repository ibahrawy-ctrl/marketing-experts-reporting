namespace Reporting.Application.Clients;

// ===== Client Digital Channel (Client 360 — CPW-R1B) =====
// معرّفات مرجعية فقط — لا أسرار/رموز وصول/كلمات مرور.
public record ClientDigitalChannelDto(
    Guid Id,
    Guid ClientId,
    string PlatformCode,
    string? DisplayName,
    string? UsernameOrHandle,
    string? ProfileUrl,
    bool IsActive,
    string? AccessStatusCode,
    string? BusinessManagerId,
    string? AdAccountId,
    string? PixelId,
    string? Ga4PropertyId,
    string? GtmContainerId,
    string? Notes,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateClientDigitalChannelRequest(
    string PlatformCode,
    string? DisplayName = null,
    string? UsernameOrHandle = null,
    string? ProfileUrl = null,
    string? AccessStatusCode = null,
    string? BusinessManagerId = null,
    string? AdAccountId = null,
    string? PixelId = null,
    string? Ga4PropertyId = null,
    string? GtmContainerId = null,
    string? Notes = null,
    int SortOrder = 0);

public record UpdateClientDigitalChannelRequest(
    string PlatformCode,
    string? DisplayName = null,
    string? UsernameOrHandle = null,
    string? ProfileUrl = null,
    string? AccessStatusCode = null,
    string? BusinessManagerId = null,
    string? AdAccountId = null,
    string? PixelId = null,
    string? Ga4PropertyId = null,
    string? GtmContainerId = null,
    string? Notes = null,
    int SortOrder = 0);

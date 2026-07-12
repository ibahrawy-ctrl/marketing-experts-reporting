namespace Reporting.Application.Clients;

// ===== Client Contact (Client 360 — CPW-R1B) =====
public record ClientContactDto(
    Guid Id,
    Guid ClientId,
    string Name,
    string? JobTitle,
    string? Department,
    string? Email,
    string? Phone,
    string? WhatsApp,
    string? PreferredContactMethodCode,
    bool IsPrimary,
    bool IsFinancialContact,
    string? Notes,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateClientContactRequest(
    string Name,
    string? JobTitle = null,
    string? Department = null,
    string? Email = null,
    string? Phone = null,
    string? WhatsApp = null,
    string? PreferredContactMethodCode = null,
    bool IsPrimary = false,
    bool IsFinancialContact = false,
    string? Notes = null,
    int SortOrder = 0);

public record UpdateClientContactRequest(
    string Name,
    string? JobTitle = null,
    string? Department = null,
    string? Email = null,
    string? Phone = null,
    string? WhatsApp = null,
    string? PreferredContactMethodCode = null,
    bool IsPrimary = false,
    bool IsFinancialContact = false,
    string? Notes = null,
    int SortOrder = 0);

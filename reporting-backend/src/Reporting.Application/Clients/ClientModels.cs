using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ===== Client =====
public record ClientDto(
    Guid Id,
    string Name,
    ClientStatus Status,
    Guid? AccountManagerId,
    string? AccountManagerName,
    string? MainContactName,
    string? MainContactInfo,
    string? Notes,
    int ProjectCount,
    int ActiveProjectCount,
    int AtRiskProjectCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateClientRequest(
    string Name,
    Guid? AccountManagerId = null,
    string? MainContactName = null,
    string? MainContactInfo = null,
    string? Notes = null,
    ClientStatus Status = ClientStatus.Active);

public record UpdateClientRequest(
    string Name,
    ClientStatus Status,
    Guid? AccountManagerId = null,
    string? MainContactName = null,
    string? MainContactInfo = null,
    string? Notes = null);

public record ClientFilter(
    ClientStatus? Status = null,
    Guid? AccountManagerId = null,
    bool IncludeClosed = false);

// ===== Linked report row (تقارير مرتبطة بعميل/مشروع) =====
public record LinkedReportRow(
    Guid SubmissionId,
    Guid SubmitterId,
    string? SubmitterName,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    Guid? ClientId,
    Guid? ProjectId);

// ===== Account Manager — Client Health =====
public record ClientHealthRow(
    Guid ClientId,
    string ClientName,
    ClientStatus Status,
    Guid? AccountManagerId,
    string? AccountManagerName,
    int ProjectCount,
    int AtRiskProjectCount,
    int OpenRiskCount,
    int OpenNoteCount,
    DateTime? LastReportAtUtc,
    string ChurnRisk,        // عالٍ/متوسط/منخفض — مشتقّ من الحالة والمؤشرات
    bool DecisionNeeded);    // يحتاج قرارًا (عميل/مشروع AtRisk أو مخاطر مفتوحة)

public record ClientHealthReport(
    string PeriodLabel,
    IReadOnlyList<ClientHealthRow> Rows,
    int TotalClients,
    int AtRiskClients,
    int DecisionNeededCount,
    int RenewalOpportunities,
    string ViewLevel,
    bool CanViewRows);

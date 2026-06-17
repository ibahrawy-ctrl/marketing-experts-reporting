using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ===== Project =====
public record ProjectDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Name,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? OwnerTeamId,
    string? OwnerTeamName,
    Guid? AccountManagerId,
    string? AccountManagerName,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateProjectRequest(
    Guid ClientId,
    string Name,
    ServiceType ServiceType,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    string? Notes = null,
    ProjectStatus Status = ProjectStatus.Active);

public record UpdateProjectRequest(
    string Name,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    string? Notes = null);

public record ProjectFilter(
    Guid? ClientId = null,
    ProjectStatus? Status = null,
    ServiceType? ServiceType = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    bool IncludeClosed = false);

// ملخّص المشروع (drill-down) — إحصاءات التقارير المرتبطة دون إعادة حساب KPI.
public record ProjectSummaryDto(
    ProjectDto Project,
    int TotalReports,
    int ClosedReports,
    int PendingReports,
    DateTime? LastReportAtUtc,
    int OpenRiskCount,
    int OpenNoteCount);

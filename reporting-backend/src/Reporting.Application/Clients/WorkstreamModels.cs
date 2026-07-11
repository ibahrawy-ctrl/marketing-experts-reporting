using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ===== Project Workstream (P1 — منصّة التنفيذ العامة) =====
public record ProjectWorkstreamDto(
    Guid Id,
    Guid ProjectId,
    string WorkstreamTypeCode,
    string? WorkstreamTypeNameAr,
    string Name,
    Guid ResponsibleTeamId,
    string? ResponsibleTeamName,
    Guid? ResponsibleManagerId,
    string? ResponsibleManagerName,
    WorkstreamStatus Status,
    int SortOrder,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateProjectWorkstreamRequest(
    string WorkstreamTypeCode,
    Guid ResponsibleTeamId,
    string? Name = null,
    Guid? ResponsibleManagerId = null,
    int SortOrder = 0,
    string? Notes = null);

public record UpdateProjectWorkstreamRequest(
    Guid ResponsibleTeamId,
    WorkstreamStatus Status,
    string? Name = null,
    Guid? ResponsibleManagerId = null,
    int SortOrder = 0,
    string? Notes = null);

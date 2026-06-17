using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

/// <summary>ملاحظة إدارية مرتبطة بالسياق (تقرير/موظف/KPI/فريق/تصعيد/قرار/مخاطرة).</summary>
public record ManagementNoteDto(
    Guid Id,
    ManagementNoteEntityType EntityType,
    Guid EntityId,
    Guid AuthorId,
    string? AuthorName,
    ManagementNoteType NoteType,
    string Body,
    bool RequiresAction,
    ManagementNoteStatus Status,
    Guid? ResolvedById,
    string? ResolvedByName,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc);

public record CreateManagementNoteRequest(
    ManagementNoteEntityType EntityType,
    Guid EntityId,
    ManagementNoteType NoteType,
    string Body,
    bool RequiresAction);

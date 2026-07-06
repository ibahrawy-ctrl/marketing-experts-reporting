using Reporting.Domain.Enums;

namespace Reporting.Application.Submissions;

/// <summary>منح رؤية تقارير (للعرض في لوحة الأدمن).</summary>
public record ReportViewGrantDto(
    Guid Id,
    Guid GranteeUserId,
    string GranteeName,
    ReportViewGrantScopeKind ScopeKind,
    Guid? TargetUserId,
    string? TargetUserName,
    Guid? TargetTeamId,
    string? TargetTeamName,
    bool IsActive,
    DateTime CreatedAtUtc,
    Guid? CreatedByUserId,
    DateTime? RevokedAtUtc,
    DateTime? ExpiresAtUtc,
    string? Notes);

/// <summary>طلب إنشاء منح رؤية. ScopeKind=User ⇒ TargetUserId مطلوب؛ ScopeKind=Team ⇒ TargetTeamId مطلوب.</summary>
public record CreateReportViewGrantRequest(
    Guid GranteeUserId,
    ReportViewGrantScopeKind ScopeKind,
    Guid? TargetUserId = null,
    Guid? TargetTeamId = null,
    DateTime? ExpiresAtUtc = null,
    string? Notes = null);

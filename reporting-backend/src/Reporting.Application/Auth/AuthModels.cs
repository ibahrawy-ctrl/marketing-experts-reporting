namespace Reporting.Application.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresUtc,
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles,
    // الدورية المتوقَّعة لتقارير هذا المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم).
    string ExpectedReportCadence);

public record MeResponse(
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    // الدورية المتوقَّعة لتقارير هذا المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم) — تُعرض كقيمة ثابتة بالواجهة.
    string ExpectedReportCadence);

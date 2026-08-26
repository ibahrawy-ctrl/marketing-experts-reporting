namespace Reporting.Application.Auth;

public record AccessToken(string Token, DateTime ExpiresUtc);

public interface ITokenService
{
    /// <param name="permissions">
    /// مفاتيح الصلاحيّات الدقيقة الممنوحة صراحةً للمستخدم (P2) — تُصدَر كمطالبات <c>perm</c>.
    /// الافتراضي فارغ ⇒ لا يتغيّر توكن أيّ مستخدم قائم.
    /// </param>
    AccessToken CreateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles,
        IEnumerable<string>? permissions = null);
    string CreateRefreshToken();
}

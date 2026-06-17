namespace Reporting.Application.Auth;

public record AccessToken(string Token, DateTime ExpiresUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
    string CreateRefreshToken();
}

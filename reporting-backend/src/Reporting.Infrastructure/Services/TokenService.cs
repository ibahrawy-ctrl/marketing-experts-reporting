using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Reporting.Application.Auth;
using Reporting.Application.Security;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _opt;

    public TokenService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public AccessToken CreateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles,
        IEnumerable<string>? permissions = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("fullName", fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        // P2 — الصلاحيّات الدقيقة الممنوحة صراحةً فقط. لا اشتقاق من الأدوار ⇒ لا مطالبة لأيّ مستخدم قائم.
        if (permissions is not null)
            claims.AddRange(permissions.Distinct().Select(p => new Claim(AppPermissions.ClaimType, p)));

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }
}

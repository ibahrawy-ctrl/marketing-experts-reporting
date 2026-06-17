using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;
    private readonly JwtOptions _opt;

    public AuthService(
        UserManager<ApplicationUser> users,
        ITokenService tokens,
        AppDbContext db,
        IOptions<JwtOptions> opt)
    {
        _users = users;
        _tokens = tokens;
        _db = db;
        _opt = opt.Value;
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        // رسالة موحّدة لتفادي تعداد الحسابات.
        if (user is null || !await _users.CheckPasswordAsync(user, request.Password))
            return Result<AuthResponse>.Failure("بيانات الدخول غير صحيحة.", "auth.invalid_credentials");

        if (!user.IsActive)
            return Result<AuthResponse>.Failure("الحساب موقوف.", "auth.account_disabled");

        return Result<AuthResponse>.Success(await IssueAsync(user, ct));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);
        if (token is null || !token.IsActive)
            return Result<AuthResponse>.Failure("رمز التجديد غير صالح.", "auth.invalid_refresh");

        var user = await _users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("الحساب غير متاح.", "auth.account_disabled");

        // تدوير الرمز: إبطال القديم وإصدار جديد.
        token.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(await IssueAsync(user, ct));
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
        if (token is not null && token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<Result<MeResponse>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<MeResponse>.Failure("المستخدم غير موجود.", "auth.not_found");

        var roles = await _users.GetRolesAsync(user);
        var jobRoleCode = user.JobRoleId is Guid jrid
            ? await _db.JobRoles.Where(j => j.Id == jrid).Select(j => j.Code).FirstOrDefaultAsync(ct)
            : null;
        var cadence = ReportCadencePolicy.ExpectedCadence(jobRoleCode).ToString();
        return Result<MeResponse>.Success(new MeResponse(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive, roles.ToArray(), cadence));
    }

    private async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = _tokens.CreateAccessToken(user.Id, user.Email ?? string.Empty, user.FullName, roles);

        var jobRoleCode = user.JobRoleId is Guid jrid
            ? await _db.JobRoles.Where(j => j.Id == jrid).Select(j => j.Code).FirstOrDefaultAsync(ct)
            : null;
        var cadence = ReportCadencePolicy.ExpectedCadence(jobRoleCode).ToString();

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _tokens.CreateRefreshToken(),
            ExpiresUtc = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays)
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            access.Token, refresh.Token, access.ExpiresUtc,
            user.Id, user.FullName, user.Email ?? string.Empty, roles.ToArray(), cadence);
    }
}

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
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive, roles.ToArray(), cadence, jobRoleCode));
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "auth.not_found");

        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            // كلمة المرور الحالية خاطئة.
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
                return Result.Failure("كلمة المرور الحالية غير صحيحة.", "auth.invalid_credentials");

            // كلمة المرور الجديدة لا تستوفي الشروط.
            return Result.Failure(
                "كلمة المرور الجديدة لا تستوفي الشروط (8 أحرف على الأقل، وتشمل حرفًا كبيرًا وصغيرًا ورقمًا).",
                "auth.password_invalid");
        }

        // إبطال كل جلسات التجديد النشطة بعد تغيير كلمة المرور (إنهاء أي جلسات أخرى).
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
            token.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ChangeEmailAsync(Guid userId, ChangeEmailRequest request, CancellationToken ct = default)
    {
        var newEmail = (request.NewEmail ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newEmail))
            return Result.Failure("البريد الإلكتروني مطلوب.", "auth.email_required");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "auth.not_found");

        // تأكيد الهوية بكلمة المرور الحالية قبل تغيير بريد الدخول.
        if (!await _users.CheckPasswordAsync(user, request.CurrentPassword))
            return Result.Failure("كلمة المرور الحالية غير صحيحة.", "auth.invalid_credentials");

        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            return Result.Success();

        var existing = await _users.FindByEmailAsync(newEmail);
        if (existing is not null && existing.Id != user.Id)
            return Result.Failure("هذا البريد الإلكتروني مستخدم بالفعل.", "auth.email_duplicate.conflict");

        // البريد = UserName أيضًا.
        var setEmail = await _users.SetEmailAsync(user, newEmail);
        if (!setEmail.Succeeded)
            return Result.Failure("البريد الإلكتروني غير صالح.", "auth.email_invalid");
        var setName = await _users.SetUserNameAsync(user, newEmail);
        if (!setName.Succeeded)
            return Result.Failure("البريد الإلكتروني غير صالح.", "auth.email_invalid");

        return Result.Success();
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
            user.Id, user.FullName, user.Email ?? string.Empty, roles.ToArray(), cadence, jobRoleCode);
    }
}

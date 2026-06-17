using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Reporting.Api.Realtime;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Infrastructure;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationPusher, SignalRNotificationPusher>();

// ===== JWT =====
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// حارس مفتاح الإنتاج: لا يُسمح بمفتاح ضعيف أو تطويري في غير بيئة التطوير.
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32 || jwt.Key.Contains("dev-only"))
        throw new InvalidOperationException("Jwt:Key غير آمن للإنتاج (يجب ≥ 32 محرفًا وبدون قيمة تطويرية).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(jwt.Key) ? new string('k', 32) : jwt.Key)),
            ClockSkew = TimeSpan.Zero
        };

        // تمرير الـJWT عبر سلسلة الاستعلام لمصادقة WebSocket على محاور SignalR.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
    options.AddPolicy(Policies.ExecutiveOnly, p => p.RequireRole(Roles.Admin, Roles.Ceo, Roles.GeneralManager));
    options.AddPolicy(Policies.ManagementOnly, p => p.RequireRole(Roles.Management));
    options.AddPolicy(Policies.TeamManagement, p => p.RequireRole(Roles.TeamManagement));
    options.AddPolicy(Policies.TemplateGovernance, p => p.RequireRole(Roles.TemplateGovernance));
    // الاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A) — قدرة الموارد البشرية HR (+ تدخّل Admin/CEO/GM).
    options.AddPolicy(Policies.LeaveFinalApproval, p => p.RequireRole(Roles.LeaveFinalApprovers));
    // رؤية طابور مراجعة الإجازات — الإدارة + الموارد البشرية (الفرض الدقيق للنطاق والخطوة في الخدمة).
    options.AddPolicy(Policies.LeaveReview, p => p.RequireRole(Roles.LeaveReviewers));
});

// ===== Rate limiting لمنع التخمين على المصادقة =====
var authLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 30;
var authWindow = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authLimit,
                Window = TimeSpan.FromSeconds(authWindow)
            }));
});

// ===== CORS =====
// الأصول المسموحة تُقرأ من الإعدادات (Cors:AllowedOrigins). في التطوير تُستخدم منافذ
// الواجهة المحلية افتراضيًا؛ في الإنتاج يجب تحديد الأصول صراحةً (لا wildcard، لا localhost).
var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

string[] corsOrigins;
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    // التطوير/الاختبار: منافذ الواجهة المحلية + أي أصول مُعرّفة في الإعدادات.
    corsOrigins = new[] { "http://localhost:5174", "http://localhost:4173" }
        .Concat(configuredOrigins)
        .Distinct()
        .ToArray();
}
else
{
    // الإنتاج: يجب توفّر أصل واحد على الأقل، ويُرفض الـ wildcard وأي أصل محلي.
    if (configuredOrigins.Length == 0)
        throw new InvalidOperationException(
            "Cors:AllowedOrigins مطلوب في الإنتاج. حدّد أصول الواجهة عبر متغيرات البيئة " +
            "مثل Cors__AllowedOrigins__0=https://reporting.<domain> (بدون wildcard وبدون localhost).");

    if (configuredOrigins.Any(o => o.Contains('*')))
        throw new InvalidOperationException("Cors:AllowedOrigins لا يقبل wildcard (*) في الإنتاج.");

    if (configuredOrigins.Any(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                                || o.Contains("127.0.0.1")))
        throw new InvalidOperationException("Cors:AllowedOrigins لا يقبل localhost كأصل إنتاجي.");

    corsOrigins = configuredOrigins;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// تطبيق الـ Migrations + تهيئة الأدوار والمدير الأولي عند الإقلاع
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    await TemplateSeeder.SeedAsync(scope.ServiceProvider);

    // هيكل تنظيمي تمثيلي لاختبار نطاق الرؤية — بيئة التطوير فقط (لا يُزرع في الإنتاج).
    if (app.Environment.IsDevelopment())
        await OrgSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ترويسات أمان أساسية
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHsts();

app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "reporting-api" }));

app.Run();

public partial class Program { }

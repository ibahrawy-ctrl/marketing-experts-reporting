using System.Security.Claims;
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
    // إدارة نواة العميل (Client 360 — CPW-R1B): Admin/CEO/GM/Manager فقط (يستثني TeamLeader).
    options.AddPolicy(Policies.ClientCoreManagement, p => p.RequireRole(Roles.ClientCoreManagers));
    options.AddPolicy(Policies.TeamManagement, p => p.RequireRole(Roles.TeamManagement));
    options.AddPolicy(Policies.TemplateGovernance, p => p.RequireRole(Roles.TemplateGovernance));
    // الاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A) — قدرة الموارد البشرية HR (+ تدخّل Admin/CEO/GM).
    options.AddPolicy(Policies.LeaveFinalApproval, p => p.RequireRole(Roles.LeaveFinalApprovers));
    // رؤية طابور مراجعة الإجازات — الإدارة + الموارد البشرية (الفرض الدقيق للنطاق والخطوة في الخدمة).
    options.AddPolicy(Policies.LeaveReview, p => p.RequireRole(Roles.LeaveReviewers));
    // رؤية طابور الحوكمة «معلّقة عند قادة الفرق» (LEAVE-TL-PENDING-GOVERNANCE-R1) — قراءة-فقط لأدوار الحوكمة
    // (Admin/CEO/GM/HR). بلا سلطة قرار؛ منفصل تمامًا عن LeaveReview. CeoSupport مُستبعَد اتّساقًا مع
    // CanViewAsync في وحدة الإجازات (لا يملك رؤية تفاصيل الطلبات اليوم).
    options.AddPolicy(Policies.LeaveGovernanceRead, p => p.RequireRole(Roles.LeaveGovernanceReaders));
    // إعادة تعيين كلمة مرور المستخدم — Admin + CEO + CeoSupport (GOV-R1). الحماية الإضافية لحسابات Admin في الخدمة (فاعل Admin فقط).
    options.AddPolicy(Policies.UserPasswordReset, p => p.RequireRole(Roles.Admin, Roles.Ceo, Roles.CeoSupport));
    // إدارة المستخدمين الكاملة (إنشاء/تعديل/تعطيل/حذف + الأدوار) — Admin + CEO فقط (GOV-R1). منفصلة عن AdminOnly العامة.
    options.AddPolicy(Policies.UserManagement, p => p.RequireRole(Roles.UserManagers));
    // تعديل المسمّى الوظيفي للموظف فقط (سطح مخصّص) — Admin/CeoSupport/HR/GM/CEO. مستقلة عن باقي إدارة المستخدم.
    options.AddPolicy(Policies.UserJobRoleManagement, p => p.RequireRole(Roles.UserJobRoleManagers));
    // رؤية/إدارة أرصدة الإجازات والأذونات (V1.1 — خدمات الموظف) — Admin/CEO/GM/CeoSupport/HR.
    options.AddPolicy(Policies.BalanceManagement, p => p.RequireRole(Roles.BalanceManagers));
    // معالجة طلبات الموارد البشرية العامة (V1.1 — خدمات الموظف) — HR/Admin/CEO/GM/CeoSupport.
    options.AddPolicy(Policies.HrRequestManagement, p => p.RequireRole(Roles.HrRequestManagers));
    // إدارة المناصب المرنة (Phase 1A — رؤية فقط) — Admin فقط (عمدًا لا CEO/GM).
    options.AddPolicy(Policies.PositionManagement, p => p.RequireRole(Roles.Admin));
    // تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم فقط) — Admin/CeoSupport/HR.
    options.AddPolicy(Policies.UserBasicManagement, p => p.RequireRole(Roles.UserBasicManagers));
    // تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) — Admin/CeoSupport/HR/GM/CEO. القيود في طبقة الخدمة.
    options.AddPolicy(Policies.UserOrgAssignment, p => p.RequireRole(Roles.UserOrgAssigners));
    // رؤية شاشة متابعة التزام التسليم (per-person، عرض فقط بلا محتوى تقرير) — Roles.CompletionMonitors (+HR).
    options.AddPolicy(Policies.ReportCompletionView, p => p.RequireRole(Roles.CompletionMonitors));
    // قراءة دليل الموارد البشرية المخصّص (قوائم الاختيار لحزمة HR) — Admin/CeoSupport/HR/GM/CEO. قراءة فقط، منفصلة عن الدليل العام.
    options.AddPolicy(Policies.HrDirectoryRead, p => p.RequireRole(Roles.HrDirectoryReaders));
    // قراءة عرض التأثير على الرواتب (FIN-L1) — Admin/CEO/GM/HR/CeoSupport. عرض على مستوى الشركة، إعلامي بحت بلا تعديل على الطلب.
    options.AddPolicy(Policies.PayrollImpactRead, p => p.RequireRole(Roles.PayrollImpactReaders));
    // تحديث حالة المراجعة المالية لطلب مؤثّر على الراتب (FIN-L1) — Admin/HR فقط. لا يمسّ الطلب الأصلي ولا الراتب.
    options.AddPolicy(Policies.PayrollImpactManage, p => p.RequireRole(Roles.PayrollImpactManagers));
    // تصدير تقييمات KPI المعتمدة للمالية (KPI-FIN1) — Admin/CEO/GM/HR/CeoSupport. قراءة فقط على مستوى الشركة، لا يحسب/يصرف مستحقات.
    options.AddPolicy(Policies.KpiFinanceExport, p => p.RequireRole(Roles.KpiFinanceExporters));
    // محفظة مدير الحساب (مشاريعي/عملائي — عرض فقط) — AccountPortfolioReader (+Admin تشغيليًّا). الرؤية مقصورة خادمًا على مشاريع المستخدم نفسه.
    options.AddPolicy(Policies.AccountPortfolioRead, p => p.RequireRole(Roles.AccountPortfolioReaders));
    // ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) — Admin/CEO/GM/CeoSupport/Manager/TeamLeader/HR. Employee/Viewer ممنوعون.
    // الفرز التفصيلي للرؤية (واسع/نطاق/HR محدود) وقيود تغيير الحالة/الإغلاق تُفرَض في طبقة الخدمة.
    options.AddPolicy(Policies.GovernanceWorkspaceAccess, p => p.RequireRole(Roles.GovernanceWorkspaceUsers));
    // التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1 — كيان مستقلّ) — Admin/CEO/GM/CeoSupport/Manager/TeamLeader/HR/Employee. Viewer ممنوع.
    // الفرز التفصيلي للرؤية (واسع/نطاق/HR/موظف) وقيود الإسناد/الإغلاق/إعادة الفتح تُفرَض في طبقة الخدمة.
    options.AddPolicy(Policies.GovernanceEscalationAccess, p => p.RequireRole(Roles.GovernanceEscalationUsers));
    // إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1 — كيان مستقلّ) — Admin/CEO/GM/CeoSupport/Manager/TeamLeader/HR/Employee. Viewer ممنوع.
    // الفرز التفصيلي للرؤية وقيود الإنشاء/الإسناد/تغيير الحالة تُفرَض في طبقة الخدمة.
    options.AddPolicy(Policies.GovernanceActionItemAccess, p => p.RequireRole(Roles.GovernanceActionItemUsers));
    // سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-R1) — عرض إداريّ قراءة-فقط: Admin/CEO/GM/CeoSupport.
    options.AddPolicy(Policies.EmailNotificationLog, p => p.RequireRole(Roles.EmailNotificationLogViewers));
    // مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — كتابة قوالب/قواعد + تذكير يدويّ DryRun: Admin حصرًا.
    options.AddPolicy(Policies.EmailControlManage, p => p.RequireRole(Roles.EmailControlManagers));
    // ===== ADMIN-GOVERNANCE-R1 — التصحيح الإداريّ ومراجعة التقارير/الـKPI =====
    // الحذف الإداريّ الناعم للتقرير المُسلَّم (POST /admin-delete بسبب إلزاميّ) — Admin/CEO/GM فقط.
    options.AddPolicy(Policies.AdminReportDelete, p => p.RequireRole(Roles.AdminReportKpiDeleters));
    // الحوكمة الإداريّة لتقييم KPI (حذف ناعم/إعادة فتح للتعديل) — Admin/CEO/GM فقط.
    options.AddPolicy(Policies.AdminKpiGovernance, p => p.RequireRole(Roles.AdminReportKpiDeleters));
    // معالجة مراجعة تقييم KPI (اعتماد/طلب تعديل/رفض/تعليق) — Admin/CEO/GM/Manager/TeamLeader (الفرض النهائي في الخدمة).
    options.AddPolicy(Policies.KpiReview, p => p.RequireRole(Roles.KpiReviewers));
    // الإشارة/التعليق/طلب إعادة الفتح على تقييم KPI (بلا اعتماد/رفض/حذف) — HR + Admin/CEO/GM.
    options.AddPolicy(Policies.KpiReviewFlag, p => p.RequireRole(Roles.KpiReviewFlaggers));
    // ===== RESTORE-ARCHIVE-GOVERNANCE-R1 — الأرشيف الإداريّ واسترجاع المحذوف =====
    // قراءة الأرشيف الإداريّ (تقارير + KPI محذوفة ناعمًا) واسترجاعها وفق Hybrid — Admin/CEO/GM فقط.
    options.AddPolicy(Policies.ArchiveGovernanceAccess, p => p.RequireRole(Roles.ArchiveGovernanceAccessors));
});

// ===== Rate limiting لمنع التخمين على المصادقة =====
var authLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 30;
var authWindow = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
// CPW-R1B2 (C-04): حدّ معدّل الرفع لكلّ مستخدم مصادَق (لا لكلّ IP) كي لا يشترك موظّفو مكتب واحد في حصّة واحدة.
var uploadLimit = builder.Configuration.GetValue<int?>("FileStorage:UploadRateLimitPermitLimit") ?? 20;
var uploadWindow = builder.Configuration.GetValue<int?>("FileStorage:UploadRateLimitWindowSeconds") ?? 60;
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
    options.AddPolicy("document-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = uploadLimit,
                Window = TimeSpan.FromSeconds(uploadWindow)
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
    corsOrigins = new[] { "http://localhost:5173", "http://localhost:5174", "http://localhost:4173" }
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
    // كتالوج تصنيفات التنفيذ — يجب أن يعمل قبل TemplateSeeder كي تتوفّر القيم عند بناء قوالب v3 (idempotent، إضافيّ بحت).
    await ExecutionTaxonomySeeder.SeedAsync(scope.ServiceProvider);
    await TemplateSeeder.SeedAsync(scope.ServiceProvider);
    // مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — بذر قوالب/قواعد أساسية (idempotent، DryRun، إضافيّ بحت).
    await EmailControlSeeder.SeedAsync(scope.ServiceProvider);
    // كتالوج الدورات (مصدر أسماء دورات مبيعات B2C) — بذر أولي قابل للتعديل (idempotent، إضافيّ بحت).
    await CourseSeeder.SeedAsync(scope.ServiceProvider);
    // كتالوج خدمات B2B (مصدر أسماء خدمات مبيعات B2B) — بذر أولي قابل للتعديل (idempotent، إضافيّ بحت).
    await ServiceSeeder.SeedAsync(scope.ServiceProvider);

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

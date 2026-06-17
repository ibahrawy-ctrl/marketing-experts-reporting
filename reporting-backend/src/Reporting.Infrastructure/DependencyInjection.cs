using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Audit;
using Reporting.Application.Calendar;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Development;
using Reporting.Application.Directory;
using Reporting.Application.Governance;
using Reporting.Application.Kpi;
using Reporting.Application.Leave;
using Reporting.Application.Notifications;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;

namespace Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Default")
                   ?? "Host=localhost;Database=reporting_dev;Username=ibrahimelbahrawi";

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(conn));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IScopeResolver, ScopeResolver>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReportTemplateService, ReportTemplateService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IKpiTemplateService, KpiTemplateService>();
        services.AddScoped<IKpiEvaluationService, KpiEvaluationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IGovernanceService, GovernanceService>();
        services.AddScoped<IManagementNoteService, ManagementNoteService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IDevelopmentService, DevelopmentService>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportCalendarService, ReportCalendarService>();
        services.AddScoped<IClientProjectAccess, ClientProjectAccess>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}

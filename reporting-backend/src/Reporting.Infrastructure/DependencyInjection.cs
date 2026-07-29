using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.AccountPortfolio;
using Reporting.Application.Archive;
using Reporting.Application.Auth;
using Reporting.Application.Audit;
using Reporting.Application.Calendar;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Courses;
using Reporting.Application.Dashboard;
using Reporting.Application.Development;
using Reporting.Application.Directory;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Governance;
using Reporting.Application.Kpi;
using Reporting.Application.Leave;
using Reporting.Application.Notifications;
using Reporting.Application.Payroll;
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
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ReminderOptions>(configuration.GetSection(ReminderOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<EmailNotificationOptions>(configuration.GetSection(EmailNotificationOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<ReportReminderSchedulerOptions>(configuration.GetSection(ReportReminderSchedulerOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IScopeResolver, ScopeResolver>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReportTemplateService, ReportTemplateService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IReportViewGrantService, ReportViewGrantService>();
        services.AddScoped<IKpiTemplateService, KpiTemplateService>();
        services.AddScoped<IKpiEvaluationService, KpiEvaluationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IEmailControlService, EmailControlService>();
        services.AddScoped<IEmailControlStatusService, EmailControlStatusService>();
        services.AddHostedService<EmailOutboxDispatcher>();
        services.AddHostedService<SubmissionReminderService>();
        services.AddHostedService<ReportReminderSchedulerService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IReportingAggregationService, ReportingAggregationService>();
        services.AddScoped<IPodExecutionAggregationService, PodExecutionAggregationService>();
        services.AddScoped<IProjectFirstExecutionAggregationService, ProjectFirstExecutionAggregationService>();
        services.AddScoped<IReportDueService, ReportDueService>();
        services.AddScoped<IReportReminderService, ReportReminderService>();
        services.AddScoped<IGovernanceService, GovernanceService>();
        services.AddScoped<IGovernanceDirectoryService, GovernanceDirectoryService>();
        services.AddScoped<IGovernanceItemService, GovernanceItemService>();
        services.AddScoped<IGovernanceEscalationService, GovernanceEscalationService>();
        services.AddScoped<IGovernanceActionItemService, GovernanceActionItemService>();
        services.AddScoped<IManagementNoteService, ManagementNoteService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IEmployeeServiceRequestService, EmployeeServiceRequestService>();
        services.AddScoped<IDevelopmentService, DevelopmentService>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IExecutiveDashboardService, ExecutiveDashboardService>();
        services.AddScoped<IReportCalendarService, ReportCalendarService>();
        services.AddScoped<IReportingCalendarCycleService, ReportingCalendarCycleService>();
        services.AddScoped<IUnifiedReportStatusService, UnifiedReportStatusService>();
        services.AddScoped<IExpectedSubmissionStatusResolver, ExpectedSubmissionStatusResolver>();
        services.AddScoped<IClientProjectAccess, ClientProjectAccess>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IPayrollImpactService, PayrollImpactService>();
        services.AddScoped<IArchiveService, ArchiveService>();
        services.AddScoped<IAccountPortfolioService, AccountPortfolioService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<Application.Services.IServiceCatalogService, ServiceCatalogService>();

        return services;
    }
}

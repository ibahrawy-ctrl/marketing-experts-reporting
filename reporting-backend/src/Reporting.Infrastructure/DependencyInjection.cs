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
using Reporting.Application.Documents;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Governance;
using Reporting.Application.Kpi;
using Reporting.Application.Leave;
using Reporting.Application.Notifications;
using Reporting.Application.Periods;
using Reporting.Application.Positions;
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
        // P1 — أعلام محرّك KPI الجديد وعتباته المركزيّة الاحتياطيّة. كلّ الأعلام false افتراضيًّا (§8).
        services.Configure<KpiFeatureOptions>(configuration.GetSection(KpiFeatureOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<ISystemClock, SystemClock>();
        // P1-KPI-002 — مصدر الحقيقة الوحيد لحدود الفترات (Asia/Riyadh).
        services.AddScoped<IPeriodService, CanonicalPeriodService>();
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
        services.AddScoped<ILeaveBalanceLifecycleService, LeaveBalanceLifecycleService>();
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
        services.AddScoped<IClientContactService, ClientContactService>();
        services.AddScoped<IClientDigitalChannelService, ClientDigitalChannelService>();
        services.AddScoped<IClientBrandService, ClientBrandService>();
        // خدمة المستندات (CPW-R1B2) — التخزين مفرد بلا حالة، ومحرّك الفحص «لا شيء» (C-01).
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IDocumentScanner, NullDocumentScanner>();
        // المقيّم المركزيّ لصلاحيّة المستندات (CPW-R2) — مصدر واحد لكلّ مسارات القائمة/العرض/التنزيل.
        services.AddScoped<IDocumentAccessEvaluator, DocumentAccessEvaluator>();
        services.AddScoped<IClientDocumentService, ClientDocumentService>();
        services.AddScoped<IClientExternalLinkService, ClientExternalLinkService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectWorkstreamService, ProjectWorkstreamService>();
        services.AddScoped<IWorkstreamDeliverableService, WorkstreamDeliverableService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IPayrollImpactService, PayrollImpactService>();
        services.AddScoped<IArchiveService, ArchiveService>();
        services.AddScoped<IAccountPortfolioService, AccountPortfolioService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<Application.Services.IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<Application.ExecutionTaxonomy.IExecutionTaxonomyService, ExecutionTaxonomyService>();

        // ===== Project 360 (CPW-R3 · W4 محرّك الأعمال + W5 سطح الـAPI) =====
        services.AddScoped<Application.Projects360.IProject360Authorization, Project360Authorization>();
        // الصحّة المخزَّنة (FINDING-W6-03): تُحقَن في خدمات الطفرة التي تمسّ مدخلات ProjectHealthPolicy،
        // فوجب تسجيلها قبلها كي لا يفشل بناء الرسم عند أوّل طلب.
        services.AddScoped<Application.Projects360.IProjectHealthService, ProjectHealthService>();
        services.AddScoped<Application.Projects360.IProjectStrategyService, ProjectStrategyService>();
        // النوع الملموس مسجَّل مرّة واحدة ثمّ يُعاد استعماله عبر الواجهة، كي لا تُنشأ نسختان
        // في الطلب الواحد حين تستدعي لوحة النظرة العامّة بانيَ الأهداف مباشرة (§15).
        services.AddScoped<ProjectObjectiveService>();
        services.AddScoped<Application.Projects360.IProjectObjectiveService>(sp => sp.GetRequiredService<ProjectObjectiveService>());
        services.AddScoped<Application.Projects360.IProjectKpiService, ProjectKpiService>();
        services.AddScoped<Application.Projects360.IProjectContractDeliverableService, ProjectContractDeliverableService>();
        services.AddScoped<Application.Projects360.IProjectOverviewService, ProjectOverviewService>();
        services.AddScoped<Application.Projects360.IProjectGovernanceReadService, ProjectGovernanceReadService>();
        services.AddScoped<Application.Projects360.IProjectExecutionBridgeService, ProjectExecutionBridgeService>();

        return services;
    }
}

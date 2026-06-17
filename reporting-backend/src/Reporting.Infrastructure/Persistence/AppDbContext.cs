using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Entities.Templates;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// سياق قاعدة البيانات الرئيسي. يرث IdentityDbContext بمفاتيح GUID.
/// كل جداول النظام (غير Identity) تستخدم GUID كمفتاح أساسي.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // التنظيم
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<JobRole> JobRoles => Set<JobRole>();

    // العملاء والمشاريع (Phase 6)
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();

    // قوالب التقارير
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportTemplateVersion> ReportTemplateVersions => Set<ReportTemplateVersion>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();

    // التسليمات والاعتماد
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SubmissionFieldValue> SubmissionFieldValues => Set<SubmissionFieldValue>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    // مؤشرات الأداء
    public DbSet<KpiTemplate> KpiTemplates => Set<KpiTemplate>();
    public DbSet<KpiTemplateVersion> KpiTemplateVersions => Set<KpiTemplateVersion>();
    public DbSet<KpiMetric> KpiMetrics => Set<KpiMetric>();
    public DbSet<KpiEvaluation> KpiEvaluations => Set<KpiEvaluation>();
    public DbSet<KpiResult> KpiResults => Set<KpiResult>();

    // التطوير
    public DbSet<TrainingNeed> TrainingNeeds => Set<TrainingNeed>();
    public DbSet<ImprovementPlan> ImprovementPlans => Set<ImprovementPlan>();

    // الحوكمة والمخاطر
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ManagementNote> ManagementNotes => Set<ManagementNote>();

    // الإجازات والاستئذانات (V1.0.1)
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestEvent> LeaveRequestEvents => Set<LeaveRequestEvent>();

    // النظام
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // رموز التجديد (Identity-adjacent)
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // تطبيق كل تكوينات الكيانات في هذا التجميع تلقائيًا (تُضاف في المراحل اللاحقة).
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}

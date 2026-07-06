using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Courses;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Payroll;
using Reporting.Domain.Entities.Positions;
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
    public DbSet<UserTeamMembership> UserTeamMemberships => Set<UserTeamMembership>();

    // العملاء والمشاريع (Phase 6)
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();

    // كتالوج الدورات (مصدر أسماء دورات مبيعات B2C)
    public DbSet<Course> Courses => Set<Course>();

    // قوالب التقارير
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportTemplateVersion> ReportTemplateVersions => Set<ReportTemplateVersion>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<ReportTemplateAssignment> ReportTemplateAssignments => Set<ReportTemplateAssignment>();

    // التسليمات والاعتماد
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SubmissionFieldValue> SubmissionFieldValues => Set<SubmissionFieldValue>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<ReportViewGrant> ReportViewGrants => Set<ReportViewGrant>();

    // مؤشرات الأداء
    public DbSet<KpiTemplate> KpiTemplates => Set<KpiTemplate>();
    public DbSet<KpiTemplateVersion> KpiTemplateVersions => Set<KpiTemplateVersion>();
    public DbSet<KpiMetric> KpiMetrics => Set<KpiMetric>();
    public DbSet<KpiEvaluation> KpiEvaluations => Set<KpiEvaluation>();
    public DbSet<KpiResult> KpiResults => Set<KpiResult>();
    public DbSet<KpiTemplateAssignment> KpiTemplateAssignments => Set<KpiTemplateAssignment>();

    // التطوير
    public DbSet<TrainingNeed> TrainingNeeds => Set<TrainingNeed>();
    public DbSet<ImprovementPlan> ImprovementPlans => Set<ImprovementPlan>();

    // الحوكمة والمخاطر
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ManagementNote> ManagementNotes => Set<ManagementNote>();
    public DbSet<GovernanceItem> GovernanceItems => Set<GovernanceItem>();
    public DbSet<GovernanceItemUpdate> GovernanceItemUpdates => Set<GovernanceItemUpdate>();

    // التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ
    public DbSet<GovernanceEscalation> GovernanceEscalations => Set<GovernanceEscalation>();
    public DbSet<GovernanceEscalationUpdate> GovernanceEscalationUpdates => Set<GovernanceEscalationUpdate>();

    // إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ
    public DbSet<GovernanceActionItem> GovernanceActionItems => Set<GovernanceActionItem>();
    public DbSet<GovernanceActionItemUpdate> GovernanceActionItemUpdates => Set<GovernanceActionItemUpdate>();

    // الإجازات والاستئذانات (V1.0.1)
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestEvent> LeaveRequestEvents => Set<LeaveRequestEvent>();

    // خدمات الموظف: الأرصدة + طلبات HR العامة (V1.1)
    public DbSet<EmployeeBalanceLedger> EmployeeBalanceLedger => Set<EmployeeBalanceLedger>();
    public DbSet<BalancePolicy> BalancePolicies => Set<BalancePolicy>();
    public DbSet<EmployeeServiceRequest> EmployeeServiceRequests => Set<EmployeeServiceRequest>();
    public DbSet<EmployeeServiceRequestEvent> EmployeeServiceRequestEvents => Set<EmployeeServiceRequestEvent>();

    // المراجعة المالية لطلبات الإجازة/الاستئذان المؤثّرة على الراتب (FIN-L1 — عرض فقط، إعلامي)
    public DbSet<PayrollImpactReview> PayrollImpactReviews => Set<PayrollImpactReview>();

    // المناصب المرنة (Phase 1A — رؤية فقط)
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionPermission> PositionPermissions => Set<PositionPermission>();
    public DbSet<PositionScope> PositionScopes => Set<PositionScope>();
    public DbSet<UserPosition> UserPositions => Set<UserPosition>();

    // النظام
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    // مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — قوالب + قواعد قابلة للتحرير. DryRun فقط.
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailRule> EmailRules => Set<EmailRule>();

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

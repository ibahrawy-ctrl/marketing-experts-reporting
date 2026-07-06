using Reporting.Domain.Enums;

namespace Reporting.Application.Dashboard;

/// <summary>
/// نماذج طبقة العرض للوحة التنفيذية (ERDS Phase 6 — Preview).
/// DTOs مستقلّة تمامًا عن DTOs محرّكات التجميع (Phase 4 / Phase 5 / Phase 5.5) — لا تُعاد أنواع التجميع مباشرة.
/// طبقة العرض تُركّب فوق نتائج المحرّكات ولا تحسب بيانات بنفسها ولا تكرّر أيّ استعلام/حساب.
/// </summary>

/// <summary>فلتر موحّد للوحة التنفيذية (كله اختياري — best-effort على كل النقاط).</summary>
public record ExecutiveDashboardFilter(
    PeriodType? PeriodType = null,
    string? PeriodKey = null,
    Guid? TeamId = null,
    Guid? EmployeeId = null,
    string? Client = null,
    string? Project = null);

// ─────────────────────────── 1) Overview ───────────────────────────

/// <summary>إجماليات عامّة على مستوى نطاق المستخدم (تركيب تنفيذ + مبيعات).</summary>
public record DashboardOverviewDto(
    decimal WorkHours,
    int Clients,
    int Projects,
    decimal Revenue,
    decimal Leads,
    decimal Sales,
    decimal Content,
    decimal Designs,
    decimal Videos,
    decimal PublishedPosts,
    string ViewLevel);

// ─────────────────────────── 2) Sales ───────────────────────────

/// <summary>صفّ مبيعات موحّد (يُستخدم لكلٍّ من B2C وB2B بعد التطبيع في طبقة العرض).</summary>
public record DashboardSalesRowDto(
    string PeriodKey,
    Guid EmployeeId,
    string EmployeeName,
    Guid? TeamId,
    string Item,
    decimal WorkHours,
    decimal Leads,
    decimal Sales,
    decimal Revenue,
    decimal ConversionRate,
    decimal RevenuePerHour);

/// <summary>مؤشرات المبيعات الحالية المجمّعة (قراءة من صفوف المحرّك بلا إعادة حساب معدّلات لكل ساعة).</summary>
public record DashboardSalesKpisDto(
    decimal TotalLeads,
    decimal TotalSales,
    decimal TotalRevenue,
    decimal TotalWorkHours,
    decimal B2cSales,
    decimal B2cRevenue,
    decimal B2bWon,
    decimal B2bRevenue);

public record DashboardSalesDto(
    DashboardSalesKpisDto Kpis,
    IReadOnlyList<DashboardSalesRowDto> B2c,
    IReadOnlyList<DashboardSalesRowDto> B2b,
    string ViewLevel);

// ─────────────────────────── 3) Pods ───────────────────────────

/// <summary>مؤشرات كل Pod (فريق) التنفيذية المجمّعة.</summary>
public record DashboardPodDto(
    Guid? TeamId,
    string TeamName,
    decimal WorkHours,
    decimal Content,
    decimal Designs,
    decimal Videos,
    decimal Published,
    decimal Delayed,
    decimal Revenue,
    decimal Productivity);

public record DashboardPodsDto(
    IReadOnlyList<DashboardPodDto> Pods,
    string ViewLevel);

// ─────────────────────────── 4) Clients ───────────────────────────

/// <summary>مؤشرات كل عميل المجمّعة عبر كل مشاريعه.</summary>
public record DashboardClientDto(
    string Client,
    decimal WorkHours,
    int Projects,
    decimal Revenue,
    decimal Spend,
    decimal Content,
    decimal Designs,
    decimal Videos,
    decimal Posts,
    string RiskLevel);

public record DashboardClientsDto(
    IReadOnlyList<DashboardClientDto> Clients,
    string ViewLevel);

// ─────────────────────────── 5) Projects ───────────────────────────

/// <summary>مؤشرات كل مشروع المجمّعة (الإيراد من تجميع العملاء عند وجوده).</summary>
public record DashboardProjectDto(
    string Client,
    string Project,
    decimal WorkHours,
    decimal CompletionRate,
    decimal DelayedTasks,
    decimal BlockedTasks,
    decimal ProgressPercent,
    decimal Revenue);

public record DashboardProjectsDto(
    IReadOnlyList<DashboardProjectDto> Projects,
    string ViewLevel);

// ─────────────────────────── 6) Workload ───────────────────────────

/// <summary>عبء العمل لكل فريق: إجمالي الساعات، عدد المشاريع/العملاء، وحدات العمل، الإنتاجية.</summary>
public record DashboardWorkloadTeamDto(
    Guid? TeamId,
    string TeamName,
    decimal TotalWorkHours,
    int ProjectsCount,
    int ClientsCount,
    int WorkUnits,
    decimal Productivity);

/// <summary>عبء العمل لكل موظّف: إجمالي الساعات، عدد المشاريع/العملاء، وحدات العمل، الإنتاجية.</summary>
public record DashboardWorkloadEmployeeDto(
    Guid EmployeeId,
    string EmployeeName,
    Guid? TeamId,
    string TeamName,
    decimal TotalWorkHours,
    int ProjectsCount,
    int ClientsCount,
    int WorkUnits,
    decimal Productivity);

public record DashboardWorkloadDto(
    IReadOnlyList<DashboardWorkloadTeamDto> Teams,
    IReadOnlyList<DashboardWorkloadEmployeeDto> Employees,
    string ViewLevel);

// ─────────────────────────── 7) Risks ───────────────────────────

public record DashboardRiskyProjectDto(
    string Client,
    string Project,
    string RiskLevel,
    decimal DelayedTasks,
    decimal BlockedTasks,
    decimal ProgressPercent);

public record DashboardDelayedClientDto(
    string Client,
    decimal DelayedItems,
    decimal MissedPosts);

public record DashboardPressuredPodDto(
    Guid? TeamId,
    string TeamName,
    decimal WorkHours,
    decimal DelayedItems,
    decimal BlockedTasks);

public record DashboardBlockedTasksDto(
    string Client,
    string Project,
    decimal BlockedTasks);

public record DashboardDelayRateDto(
    Guid? TeamId,
    string TeamName,
    Guid EmployeeId,
    string EmployeeName,
    string Client,
    string Project,
    decimal DelayRate);

public record DashboardRisksDto(
    IReadOnlyList<DashboardRiskyProjectDto> TopRiskyProjects,
    IReadOnlyList<DashboardDelayedClientDto> TopDelayedClients,
    IReadOnlyList<DashboardPressuredPodDto> TopPressuredPods,
    IReadOnlyList<DashboardBlockedTasksDto> TopBlockedTasks,
    IReadOnlyList<DashboardDelayRateDto> TopDelayRate,
    string ViewLevel);

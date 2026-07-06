using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>
/// مرشّح محرّك تجميع التنفيذ (ERDS Phase 5) — قراءة فقط.
/// يوسّع نمط Phase 4 ليقرأ القوالب التنفيذية الرقمية الستة (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع)
/// ويجمّعها حسب (الفترة، الفريق/Pod، الموظّف، العميل، المشروع).
/// <b>PeriodKey</b> = مفتاح الفترة (مثل 2026-W33) — تطابق تام. <b>Client/Project</b> = تصفية صفوف الجدول بالاسم (case-insensitive).
/// Pod = فريق المُسلِّم (Submitter.TeamId على التسليم)؛ إن غاب فلا يُنسَب لفريق.
/// </summary>
public record PodExecutionFilter(
    PeriodType? PeriodType = null,
    string? PeriodKey = null,
    Guid? TeamId = null,
    Guid? EmployeeId = null,
    string? Client = null,
    string? Project = null);

/// <summary>
/// مؤشّرات الإنتاجية المحسوبة بقسمة آمنة (المقام صفر ⇒ 0). لا تُجمَع مباشرةً بل تُشتَقّ من المجاميع.
/// المعدّلات لكل ساعة تعتمد ساعات العمل من قالب المشاريع فقط (القوالب الإنتاجية لا تحمل ساعات) — لذا قد تكون 0 حين لا مشاريع.
/// </summary>
public record PodProductivityIndicators(
    decimal ContentPerHour,
    decimal DesignsPerHour,
    decimal VideosPerHour,
    decimal PostsPerHour,
    decimal DelayRate,          // DelayedItems / RequiredItems (%)
    decimal MissedPostingRate,  // MissedPosts / PostsScheduled (%)
    decimal Cpl,                // Spend / Leads
    decimal Cpa,                // Spend / Purchases
    decimal Roas);              // Revenue / Spend

/// <summary>
/// صفّ تجميع التنفيذ الموحّد لكل (الفترة، الفريق، الموظّف، العميل، المشروع).
/// القوالب الإنتاجية (محتوى/تصميم/فيديو/نشر/ميديا باير) لا تحمل عمود مشروع ⇒ Project فارغ لصفوفها؛
/// قالب المشاريع وحده يحمل مشروعًا حقيقيًّا. القيم مجموع خلايا TableGrid المقروءة بأمان.
/// ProductionRequiredItems و PostsScheduled مكشوفان لتمكين التحقّق من DelayRate و MissedPostingRate.
/// </summary>
public record PodExecutionAggregationDto(
    PeriodType PeriodType,
    string PeriodKey,
    Guid? TeamId,
    string TeamName,
    Guid EmployeeId,
    string EmployeeName,
    string Client,
    string Project,
    decimal WorkHours,
    decimal ContentPieces,       // محتوى معتمد
    decimal DesignsCompleted,    // تصميمات منجزة
    decimal VideosCompleted,     // فيديوهات بعد المونتاج
    decimal PostsPublished,
    decimal MissedPosts,
    decimal DelayedItems,        // متأخرات المحتوى + التصميم + الفيديو
    decimal BlockedTasks,
    decimal Spend,
    decimal Leads,
    decimal Purchases,
    decimal Revenue,
    decimal ProductionRequiredItems,
    decimal PostsScheduled,
    string RiskLevel,
    PodProductivityIndicators ProductivityIndicators);

/// <summary>نتيجة تجميع التنفيذ حسب Pod + بيانات تشخيصية للتوافق الخلفي (المتجاهَل بلا فشل).</summary>
public record PodExecutionReport(
    string? PeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<PodExecutionAggregationDto> Rows);

/// <summary>
/// صفّ تجميع التنفيذ لكل (عميل، مشروع) على مستوى النطاق — يجمع كل الموظّفين والفترات ضمن الفلاتر.
/// CPL/CPA/ROAS محسوبة من المجاميع (لا تُجمَع). RiskLevel = أسوأ خطر ظاهر لمشاريع هذا العميل/المشروع.
/// </summary>
public record ClientExecutionAggregationDto(
    string Client,
    string Project,
    decimal TotalWorkHours,
    decimal TotalContentPieces,
    decimal TotalDesigns,
    decimal TotalVideos,
    decimal TotalPublishedPosts,
    decimal TotalMissedPosts,
    decimal TotalDelayedItems,
    decimal TotalBlockedTasks,
    decimal TotalSpend,
    decimal TotalLeads,
    decimal TotalPurchases,
    decimal TotalRevenue,
    decimal Cpl,
    decimal Cpa,
    decimal Roas,
    string RiskLevel);

/// <summary>نتيجة تجميع التنفيذ حسب العميل/المشروع + بيانات تشخيصية.</summary>
public record ClientExecutionReport(
    string? PeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<ClientExecutionAggregationDto> Rows);

/// <summary>
/// صفّ تجميع المشاريع من «تقرير المشاريع حسب العميل/المشروع» فقط، لكل (الفترة، الموظّف، العميل، المشروع).
/// ProgressPercentAvg = متوسّط آمن لنسب التقدّم (لا يُجمَع). CompletionRate = المنجز/المخطط (%).
/// RiskLevel = أسوأ خطر ظاهر لصفوف المفتاح.
/// </summary>
public record ProjectExecutionAggregationDto(
    string PeriodKey,
    Guid EmployeeId,
    string EmployeeName,
    Guid? TeamId,
    string TeamName,
    string Client,
    string Project,
    decimal WorkHours,
    decimal PlannedTasks,
    decimal CompletedTasks,
    decimal DelayedTasks,
    decimal BlockedTasks,
    decimal ProgressPercentAvg,
    decimal CompletionRate,
    string RiskLevel);

/// <summary>نتيجة تجميع المشاريع + بيانات تشخيصية.</summary>
public record ProjectExecutionReport(
    string? PeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<ProjectExecutionAggregationDto> Rows);

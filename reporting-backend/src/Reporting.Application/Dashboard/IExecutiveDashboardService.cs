using Reporting.Application.Common;

namespace Reporting.Application.Dashboard;

/// <summary>
/// اللوحة التنفيذية (ERDS Phase 6 — Preview) — قراءة فقط، طبقة عرض فقط.
/// لا تحسب البيانات بنفسها: تُركّب فوق IReportingAggregationService (Phase 4) و
/// IPodExecutionAggregationService (Phase 5 / 5.5) وتُعيد تشكيل نتائجها في DTOs مستقلّة.
/// النطاق محكوم داخل محرّكات التجميع (IScopeResolver) — لا تفتح بيانات خارج نطاق الدور.
/// لا تمسّ أيّ تسليم/قالب/مسار اعتماد/صلاحية. لا استعلام جديد ولا إعادة حساب مؤشرات.
/// </summary>
public interface IExecutiveDashboardService
{
    /// <summary>إجماليات عامّة (ساعات، عملاء، مشاريع، إيراد، Leads، مبيعات، محتوى، تصاميم، فيديو، منشورات).</summary>
    Task<Result<DashboardOverviewDto>> GetOverviewAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>مبيعات B2C + B2B لكل أسبوع/فريق/موظّف مع المؤشرات الحالية.</summary>
    Task<Result<DashboardSalesDto>> GetSalesAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>مؤشرات كل Pod التنفيذية (ساعات/محتوى/تصاميم/فيديو/منشور/متأخر/إيراد/إنتاجية).</summary>
    Task<Result<DashboardPodsDto>> GetPodsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>مؤشرات كل عميل المجمّعة عبر مشاريعه.</summary>
    Task<Result<DashboardClientsDto>> GetClientsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>مؤشرات كل مشروع المجمّعة (الإنجاز/التأخير/التوقف/التقدّم/الإيراد عند وجوده).</summary>
    Task<Result<DashboardProjectsDto>> GetProjectsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>عبء العمل لكل فريق ولكل موظّف (ساعات، مشاريع، عملاء، وحدات عمل، إنتاجية).</summary>
    Task<Result<DashboardWorkloadDto>> GetWorkloadAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);

    /// <summary>لوحة المخاطر: أخطر المشاريع، أكثر العملاء تأخّرًا، أكثر Pods ضغطًا، أعلى المهام المتوقّفة، أعلى معدّل تأخير.</summary>
    Task<Result<DashboardRisksDto>> GetRisksAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default);
}

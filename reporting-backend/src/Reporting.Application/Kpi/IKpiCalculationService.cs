using Reporting.Application.Common;

namespace Reporting.Application.Kpi;

/// <summary>
/// P1-KPI-003 — **مصدر الحساب الوحيد** لكلّ رقم KPI مؤسّسيّ.
/// أيّ مستهلك جديد يستدعي هذه الخدمة ولا يقرأ <c>KpiEvaluations</c> مباشرةً ليشتقّ مؤشّرًا.
///
/// الضمانات المفروضة داخل التنفيذ (لا يمكن للمستدعي تعطيلها):
/// <list type="bullet">
/// <item>Approved فقط + <c>TotalScore != null</c> + المحذوف مستبعَد بالمرشّح العامّ (§5.2).</item>
/// <item>توسيط ثنائي المرحلة: متوسّط الموظّف أوّلًا ثمّ متوسّط متوسّطات الأعضاء — لكلّ موظّف وزن واحد (B-2).</item>
/// <item>فصل الكادنس صراحةً؛ الطلب بلا كادنس يفشل ولا يسقط صامتًا (B-3).</item>
/// <item>التغطية وجودة البيانات محسوبتان دائمًا؛ Missing ≠ صفر (B-5/§5.2).</item>
/// <item>النطاق مفروض خادميًّا عبر <see cref="IScopeResolver"/>؛ الخارج عنه لا يُسرَّب.</item>
/// </list>
/// </summary>
public interface IKpiCalculationService
{
    /// <summary>العقد التنظيميّ: شركة + إدارات + فرق + موظّفون بنفس الفترة والكادنس والنطاق.</summary>
    Task<Result<KpiPerformanceDto>> GetPerformanceAsync(KpiAnalyticsQuery query, CancellationToken ct = default);

    /// <summary>ترتيب الأفضل/المحتاجين للدعم — صفّ واحد لكلّ موظّف بعد شرط التغطية.</summary>
    Task<Result<KpiRankingsDto>> GetRankingsAsync(KpiAnalyticsQuery query, int take = 5, CancellationToken ct = default);

    /// <summary>الصفوف الفعليّة التي بنت الرقم + المتوسّط المُعاد حسابه منها (لإثبات القابليّة للتدقيق).</summary>
    Task<Result<KpiDrilldownDto>> GetDrilldownAsync(KpiAnalyticsQuery query, CancellationToken ct = default);
}

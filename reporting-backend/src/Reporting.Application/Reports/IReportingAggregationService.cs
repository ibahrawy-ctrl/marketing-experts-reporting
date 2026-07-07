using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>
/// محرّك التجميع الرقمي (ERDS Phase 4) — قراءة فقط.
/// يقرأ التسليمات المُرسَلة لقوالب ERDS المُهيكَلة، ويستخرج القيم الرقمية من جدول TableGrid،
/// ويُجمّعها إلى مؤشرات جاهزة لتغذية لوحات لاحقة. لا يغيّر أيّ تسليم/قالب/مسار اعتماد.
/// النطاق محكوم بـ IScopeResolver (لا يفتح بيانات خارج نطاق الدور). يتجاهل بأمان التقارير/الصفوف غير المطابقة.
/// </summary>
public interface IReportingAggregationService
{
    /// <summary>تجميع «تقرير مبيعات B2C حسب الدورة» لكل (أسبوع، موظّف، دورة) ضمن نطاق المستخدم.</summary>
    Task<Result<B2cAggregationReport>> AggregateB2cByCourseAsync(AggregationFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع «تقرير مبيعات B2C» مجموعةً حسب الدورة (الدورة تظهر مرّة واحدة مهما تعدّد الموظّفون)،
    /// مع تفصيل لكل موظّف داخل الدورة (Drill-down). العرض الافتراضي للمدير.
    /// </summary>
    Task<Result<B2cCourseGroupedReport>> AggregateB2cByCourseGroupedAsync(AggregationFilter filter, CancellationToken ct = default);

    /// <summary>تجميع «تقرير مبيعات B2B حسب الخدمة» لكل (أسبوع، موظّف، خدمة) ضمن نطاق المستخدم.</summary>
    Task<Result<B2bAggregationReport>> AggregateB2bByServiceAsync(AggregationFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع B2C مفصولًا إلى بيانات جديدة New / بيانات CRM قديمة Old (Phase 7). يقرأ القالب القديم أحادي الجدول
    /// (⇒ New) وجدولَي القالب الجديد (New Leads ⇒ New، Old CRM Data ⇒ Old). قراءة فقط ضمن نطاق المستخدم.
    /// </summary>
    Task<Result<B2cNewOldReport>> AggregateB2cNewOldAsync(AggregationFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع B2B مفصولًا حسب مصدر البيانات (RC-3 Task 2A): New Leads / Data Scraping / Legacy. يقرأ القالب أحادي الجدول
    /// السابق «حسب الخدمة» (⇒ Legacy) وجدولَي القالب الجديد (New Leads ⇒ New، Data Scraping ⇒ Data). قراءة فقط ضمن نطاق المستخدم.
    /// </summary>
    Task<Result<B2bSourceReport>> AggregateB2bBySourceAsync(AggregationFilter filter, CancellationToken ct = default);

    /// <summary>
    /// سياق المبيعات الموثوق للمستخدم الحالي (RC-3 Task 1.1): يحدّد أيّ أقسام المبيعات تُعرَض (B2C/B2B)
    /// وهل هو مندوب فردي، بحساب خادميّ من المسمّى الوظيفي ومسمّيات أعضاء نطاقه. لا يقرأ أيّ تسليم.
    /// </summary>
    Task<Result<SalesContextDto>> GetSalesContextAsync(CancellationToken ct = default);
}

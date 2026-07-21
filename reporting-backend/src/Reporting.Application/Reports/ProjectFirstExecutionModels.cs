using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>
/// مرشّح محرّك التجميع Project-First (PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1) — قراءة فقط.
/// يقرأ قوالب التنفيذ الأربعة (محتوى/تصميم/فيديو/مديرشن) من قسم المشاريع المتكرّر (ProjectRepeatableSection)
/// حيث تُخزَّن كل الأرقام التشغيلية <b>داخل كل مشروع</b>، لا top-level.
/// <b>ClientId/ProjectId</b> = تصفية على المعرّفات الحقيقية (لا على الاسم النصّي). Pod = فريق المُسلِّم.
/// النطاق محكوم بـ IScopeResolver ∪ IClientProjectAccess.
/// </summary>
public record ProjectFirstExecutionFilter(
    PeriodType? PeriodType = null,
    string? PeriodKey = null,
    Guid? TeamId = null,
    Guid? EmployeeId = null,
    Guid? ClientId = null,
    Guid? ProjectId = null);

/// <summary>
/// مقاييس التنفيذ المجمَّعة المشتركة لكل مجموعة (مشروع/موظّف/Pod/عميل). المجاميع تُقرأ من المفاتيح الرقمية الحقيقية
/// داخل كل مشروع (عبر ProjectFirstExecutionSchema.MapFor). المعدّلات مشتقّة بقسمة آمنة (المقام صفر ⇒ 0) ولا تُجمَع.
/// عائلة الإنتاج (محتوى/تصميم/فيديو): Planned/Completed/Approved/Revisions/Delayed.
/// عائلة المديرشن: MessagesIn/Responses/IssueComments/Escalations.
/// Published: لا مصدر في v5 ⇒ دائمًا صفر (يبقى لثبات الشكل).
/// </summary>
public record ProjectExecMetrics(
    decimal Planned,
    decimal Completed,
    decimal Approved,
    decimal Revisions,
    decimal Published,
    decimal Delayed,
    decimal MessagesIn,
    decimal Responses,
    decimal IssueComments,
    decimal Escalations,
    decimal CompletionRate,   // Completed / Planned (%)
    decimal ApprovalRate,     // Approved / Completed (%)
    decimal PublishRate,      // Published / Approved (%) — دائمًا 0 (لا مصدر)
    decimal ResponseRate);    // Responses / MessagesIn (%)

/// <summary>
/// توزيع حالة المشروع المطبَّعة (Phase 4) لكل مجموعة — عدد مدخلات المشاريع في كل حالة آليّة.
/// Total = مجموع المدخلات المحتسبة (قد يتجاوز عدد المشاريع لأنّ Strategy A يجمع مدخلات متعدّدة لنفس المشروع).
/// </summary>
public record ProjectStatusTally(
    int Healthy,
    int Stable,
    int NeedsIntervention,
    int Unspecified,
    int Total);

/// <summary>
/// مقارنة دورية لمقياس المخرجات الرئيسي (TotalOutput = Completed + Responses) بين الفترة الحالية والسابقة.
/// Trend: "up"/"down"/"stable" حين توجد فترة سابقة، و"none" حين لا توجد بيانات سابقة (HasPrevious=false).
/// ChangePercent = null حين Previous == 0 (لا قسمة نسبة على صفر) بينما القيمة المطلقة Change تبقى محسوبة.
/// </summary>
public record PeriodComparison(
    decimal Current,
    decimal Previous,
    decimal Change,
    decimal? ChangePercent,
    string Trend,
    bool HasPrevious);

/// <summary>صفّ تجميع لكل مشروع (عبر كل الموظّفين/الفترات ضمن الفلاتر). مفتاح التجميع = المعرّف الحقيقي للمشروع.</summary>
public record ProjectFirstByProjectRow(
    Guid ProjectId,
    string ProjectName,
    Guid? ClientId,
    string ClientName,
    int Contributors,
    ProjectExecMetrics Metrics,
    PeriodComparison? Comparison,
    ProjectStatusTally Status);

/// <summary>صفّ تجميع لكل (موظّف، مشروع) — للتفصيل داخل عرض قائد الفريق/المدير.</summary>
public record ProjectFirstByEmployeeRow(
    Guid EmployeeId,
    string EmployeeName,
    Guid? TeamId,
    string TeamName,
    Guid ProjectId,
    string ProjectName,
    Guid? ClientId,
    string ClientName,
    ProjectExecMetrics Metrics,
    PeriodComparison? Comparison,
    ProjectStatusTally Status);

/// <summary>صفّ تجميع لكل Pod (فريق المُسلِّم) — يجمع كل مشاريع/موظّفي الفريق ضمن الفلاتر.</summary>
public record ProjectFirstByPodRow(
    Guid? TeamId,
    string TeamName,
    int ProjectCount,
    int EmployeeCount,
    ProjectExecMetrics Metrics,
    PeriodComparison? Comparison,
    ProjectStatusTally Status);

/// <summary>صفّ تجميع لكل عميل — يجمع كل مشاريع العميل ضمن الفلاتر. ActiveProjectCount = المشاريع ذات الحالة Active.</summary>
public record ProjectFirstByClientRow(
    Guid? ClientId,
    string ClientName,
    int ProjectCount,
    int ActiveProjectCount,
    ProjectExecMetrics Metrics,
    PeriodComparison? Comparison,
    ProjectStatusTally Status);

/// <summary>
/// نتيجة تجميع Project-First عامّة + بيانات تشخيصية آمنة (لا تكشف بيانات خارج النطاق).
/// PreviousPeriodKey = مفتاح الفترة السابقة المشتقّ (null إن تعذّر الاشتقاق ⇒ لا مقارنة).
/// EntriesIgnored = مدخلات مشاريع بلا ProjectId صالح (بيانات قديمة أو صفوف فارغة).
/// RowsConsidered = مدخلات المشاريع المرئية التي فُحِصت؛ RowsIgnored = ما أُسقِط منها (فارغ/خارج فلتر المشروع أو العميل).
/// IgnoredReasons = تجميع أسباب الإسقاط داخل النطاق فقط (empty_project_entry/outside_project_filter/outside_client_filter).
/// </summary>
public record ProjectFirstExecutionReport<TRow>(
    string? PeriodKey,
    string? PreviousPeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int EntriesIgnored,
    int RowsConsidered,
    int RowsIgnored,
    IReadOnlyDictionary<string, int> IgnoredReasons,
    string ViewLevel,
    IReadOnlyList<TRow> Rows);

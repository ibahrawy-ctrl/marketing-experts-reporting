using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// مؤشّر أداء لمشروع (CPW-R3 · D-02 + D-06 · §5-5) — **تابع إلزاميًّا لهدف**.
///
/// <para>
/// **منفصل تمامًا عن منظومة مؤشّرات الموظّفين** (<c>KpiTemplate</c> / <c>KpiEvaluation</c> / <c>KpiMetric</c>)
/// التي لم تُمَسّ بحرف. لا مرجع، لا وراثة، لا تعداد مشترك بين المنظومتين.
/// </para>
///
/// <para>
/// **D-06 — الضمانة الجوهريّة**: «المؤشّر لا يتغيّر — مصدر بياناته فقط». لا يُبنى أيّ منطق على كون
/// المصدر يدويًّا؛ يُبنى على <see cref="SourceType"/>. الانتقال من المرحلة 1 (Manual) إلى المرحلة 2
/// (TaskDerived) أو 3 (Integration) = تحديث هذا العمود لصفّ قائم: **لا صفّ جديد، لا هجرة، لا فقد تاريخ**.
/// الأعمدة الثلاثة <see cref="ExternalSourceKey"/> و<see cref="ExternalMetricCode"/> و<see cref="LastSyncedAtUtc"/>
/// مُنشأة اليوم وفارغة تمامًا — تهيئة مسبقة تمنع هجرة مستقبليّة.
/// </para>
///
/// <para>
/// **لماذا <see cref="ProjectId"/> مُكرَّر رغم اشتقاقه عبر <see cref="ObjectiveId"/>؟**
/// (1) الأداء: لوحة النظرة التنفيذيّة تجلب كلّ مؤشّرات المشروع بلا JOIN على الأهداف.
/// (2) الأمن: التحقّق من الرؤية يتمّ على المشروع مباشرة قبل أيّ استعلام آخر — أقلّ عرضة لـIDOR.
/// (3) التكامل المرجعيّ: حارس خادميّ يفرض <c>objective.ProjectId == kpi.ProjectId</c> وإلّا
/// <c>project_kpi.objective_mismatch.conflict</c> (409).
/// </para>
/// </summary>
public class ProjectKpi : BaseEntity
{
    /// <summary>المشروع — مُكرَّر عمدًا لأسباب الأداء والأمن والتكامل المرجعيّ (انظر ملخّص الصنف).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// الهدف الأب — **إلزاميّ (NOT NULL) بقرار D-02**. مؤشّر بلا هدف مرفوض **بنيويًّا** لا منطقيًّا.
    /// </summary>
    public Guid ObjectiveId { get; set; }
    public ProjectObjective? Objective { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ProjectKpiCategory Category { get; set; } = ProjectKpiCategory.Custom;
    public ProjectKpiUnit Unit { get; set; } = ProjectKpiUnit.Number;

    /// <summary>تسمية الوحدة المخصَّصة — تُستعمل حين <see cref="Unit"/> = <see cref="ProjectKpiUnit.Custom"/>.</summary>
    public string? CustomUnitLabel { get; set; }

    /// <summary>اتّجاه التحسّن — يحدّد معادلة نسبة الإنجاز.</summary>
    public ProjectKpiDirection Direction { get; set; } = ProjectKpiDirection.HigherIsBetter;

    /// <summary>دوريّة القياس المتوقَّعة.</summary>
    public ProjectKpiFrequency Frequency { get; set; } = ProjectKpiFrequency.Monthly;

    /// <summary>خطّ الأساس قبل بدء العمل.</summary>
    public decimal? BaselineValue { get; set; }

    /// <summary>القيمة المستهدَفة.</summary>
    public decimal TargetValue { get; set; }

    /// <summary>لقطة سريعة لآخر قيمة — تُعاد كتابتها من **أحدث قراءة باقية** لا من القراءة المُدخَلة.</summary>
    public decimal? CurrentValue { get; set; }

    /// <summary>تاريخ أحدث قراءة باقية.</summary>
    public DateOnly? LastReadingDate { get; set; }

    /// <summary>
    /// وزن المؤشّر داخل هدفه. لا يُفرَض مجموع محدَّد — يُطبَّع وقت الاحتساب.
    ///
    /// <para>
    /// **قرار المالك · ملحق W1-A بند 4**: مؤشّرات الهدف الواحد ليست متساوية الأهمّيّة بطبيعتها
    /// (تكلفة الاكتساب ليست كعدد المتابعين). الوزن مُنشأ من اليوم؛ صفر في الكلّ ⟹ تساوٍ،
    /// وأوّل قيمة حقيقيّة تُفعِّل الترجيح **بلا هجرة وبلا تغيير معادلة**.
    /// </para>
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// مصدر بيانات المؤشّر (D-06). المرحلة 1 تُنشئ كلّ المؤشّرات <see cref="ProjectKpiSourceType.Manual"/>،
    /// وإدخال القراءات اليدويّ مسموح **فقط** عند هذه القيمة — وإلّا
    /// <c>project_kpi.source_not_manual.conflict</c> (409). الحارس خامل عمليًّا اليوم، وهو ما يجعل
    /// المرحلتين 2 و3 **تبديل مزوّد** لا إعادة كتابة نظام.
    /// </summary>
    public ProjectKpiSourceType SourceType { get; set; } = ProjectKpiSourceType.Manual;

    /// <summary>D-06 مرحلة 3 — معرّف مصدر خارجيّ (GA4 · Meta · Ads · Search Console · CRM). مُهيَّأ الآن، غير مستعمل.</summary>
    public string? ExternalSourceKey { get; set; }

    /// <summary>D-06 مرحلة 3 — رمز المقياس داخل المصدر الخارجيّ. مُهيَّأ الآن، غير مستعمل.</summary>
    public string? ExternalMetricCode { get; set; }

    /// <summary>D-06 مرحلة 3 — ختم آخر مزامنة خارجيّة. مُهيَّأ الآن، غير مستعمل.</summary>
    public DateTime? LastSyncedAtUtc { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// ترتيب العرض المعلَن (تصاعديّ) داخل الهدف — **قرار المالك · ملحق W1-A بند 2**.
    /// المؤشّر الأهمّ يُعرَض أوّلًا بقرار المدير لا بترتيب الإدخال. لا واجهة سحب اليوم، والتصميم
    /// لا يمنعها لاحقًا لأنّها ستكتب في هذا العمود نفسه.
    /// الاسم التقنيّ موحَّد على اصطلاح <c>SortOrder</c> القائم؛ و«ترتيب العرض» تسمية الواجهة فقط.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>التعطيل بدل الحذف. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>سجلّ القراءات التاريخيّة لهذا المؤشّر.</summary>
    public ICollection<ProjectKpiReading> Readings { get; set; } = new List<ProjectKpiReading>();
}

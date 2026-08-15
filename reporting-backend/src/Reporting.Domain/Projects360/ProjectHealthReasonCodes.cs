namespace Reporting.Domain.Projects360;

/// <summary>
/// رموز أسباب الصحّة (CPW-R3 · §9-8) — **ثوابت نصّيّة لا تعداد**.
///
/// <para>
/// **لماذا نصّ لا <c>enum</c>؟** لأنّ §18-2 يقفل الأثر الكمّيّ على **تسعة تعدادات** بالضبط،
/// ولأنّ قائمة الأسباب هي البند **الأكثر توقّعًا للنموّ** في هذه المنظومة: سبب جديد يجب أن يكون
/// **سطر ثابت واحد** لا تعديل تعداد يُلزِم مراجعة كلّ <c>switch</c> ولا هجرة على عمود مُعدَّد.
/// </para>
///
/// <para>
/// الرمز **معرّف مستقرّ للآلة** (يُستعمل في الاختبارات والواجهة لاختيار النصّ المعروض)،
/// ولا يُعرَض للمستخدم كما هو.
/// </para>
/// </summary>
public static class ProjectHealthReasonCodes
{
    // ===== مكوّن المؤشّرات =====

    /// <summary>لا يوجد مؤشّر واحد قابل للاحتساب ⟹ استُبعِد مكوّن المؤشّرات وأُعيد توزيع وزنه.</summary>
    public const string KpiComponentExcluded = "health.kpi.excluded";

    /// <summary>نتيجة المؤشّرات الموزونة دون العتبة الخضراء.</summary>
    public const string KpiScoreBelowTarget = "health.kpi.below_target";

    /// <summary>كلّ الأوزان صفر ⟹ عوملت المؤشّرات بأوزان متساوية.</summary>
    public const string KpiWeightsAllZero = "health.kpi.weights_all_zero";

    // ===== مكوّن التقدّم =====

    /// <summary>تقدّم المشروع المعلَن دون العتبة الخضراء.</summary>
    public const string ProgressBelowTarget = "health.progress.below_target";

    // ===== مكوّن الجدول الزمنيّ =====

    /// <summary>تواريخ المشروع ناقصة ⟹ استُبعِد مكوّن الجدول وأُعيد توزيع وزنه.</summary>
    public const string ScheduleComponentExcluded = "health.schedule.excluded";

    /// <summary>التقدّم الفعليّ متأخّر عن التقدّم المتوقَّع زمنيًّا.</summary>
    public const string ScheduleBehindPlan = "health.schedule.behind_plan";

    // ===== أسباب عامّة =====

    /// <summary>لا مكوّن واحد متاح ⟹ الصحّة غير محتسَبة (لا تُعامَل صفرًا).</summary>
    public const string NoComponentAvailable = "health.no_component";

    /// <summary>كلّ المكوّنات المتاحة فوق العتبة الخضراء.</summary>
    public const string AllComponentsHealthy = "health.all_healthy";
}

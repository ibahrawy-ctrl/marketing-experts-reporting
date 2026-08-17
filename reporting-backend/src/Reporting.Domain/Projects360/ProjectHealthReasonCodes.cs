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

    // ===== الحجب والتأخّر والمخاطر (P360-WF-R2 §7) =====

    /// <summary>
    /// حاجب مفتوح مثبت على المشروع. **يتقدّم على كلّ حساب آخر** مهما بلغت المؤشّرات:
    /// مشروع محجوب لا يكون أخضر لأنّ متوسّطه الرقميّ مرتفع.
    /// <c>Detail</c> = عدد الحواجب المفتوحة.
    /// </summary>
    public const string OpenBlocker = "health.blocked.open_blocker";

    /// <summary>
    /// مخرَج تعاقديّ نشط تجاوز تاريخ استحقاقه ولم يُسلَّم. <c>Detail</c> = عدد المخرَجات المتأخّرة.
    /// </summary>
    public const string OverdueDeliverable = "health.delayed.overdue_deliverable";

    /// <summary>
    /// خطر مفتوح بخطورة مرتفعة أو حرجة. <c>Detail</c> = عدد المخاطر المرتفعة المفتوحة.
    /// </summary>
    public const string OpenHighRisk = "health.at_risk.open_high_risk";

    // ===== وضع احتساب التقدّم (P360-WF-R2 §6-1) =====

    /// <summary>
    /// أوزان المخرَجات غير مضبوطة أو ناقصة ⟹ حُسِب التقدّم بتوزيع متساوٍ. **تنبيه لا حكم**:
    /// الرقم صالح للقراءة ومصدره معلن، لكنّه ليس الترجيح الذي أراده مالك المشروع.
    /// </summary>
    public const string ProgressEqualWeightFallback = "health.progress.equal_weight_fallback";

    /// <summary>لا مخرَجات تعاقديّة نشطة ⟹ لا مصدر لتقدّم المشروع (النسبة 0 بتفسير لا حكمًا).</summary>
    public const string ProgressNoDeliverables = "health.progress.no_deliverables";

    // ===== أسباب عامّة =====

    /// <summary>لا مكوّن واحد متاح ⟹ الصحّة غير محتسَبة (لا تُعامَل صفرًا).</summary>
    public const string NoComponentAvailable = "health.no_component";

    /// <summary>كلّ المكوّنات المتاحة فوق العتبة الخضراء.</summary>
    public const string AllComponentsHealthy = "health.all_healthy";
}

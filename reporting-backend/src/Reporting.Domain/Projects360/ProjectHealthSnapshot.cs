using Reporting.Domain.Enums;

namespace Reporting.Domain.Projects360;

/// <summary>
/// **نموذج الصحّة** (CPW-R3 · §9-8) — الشكل الكامل لحكم الصحّة على مشروع.
///
/// <para>
/// **العناصر الأربعة المطلوبة**: <see cref="Score"/> · <see cref="Status"/> ·
/// <see cref="Reasons"/> · <see cref="LastEvaluatedAtUtc"/>. اللون وحده لا يكفي؛ الحكم بلا
/// نتيجة رقميّة وبلا سبب وبلا ختم تقييم لا يمكن مراجعته ولا اختباره.
/// </para>
///
/// <para>
/// **ثلاثة تُخزَّن وواحد يُشتقّ**: <c>projects</c> يحمل <c>HealthPercent</c> و<c>HealthStatus</c>
/// و<c>HealthComputedAtUtc</c> (§5-2) لأنّ الفرز والفلترة على قوائم المشاريع تحتاجها في SQL بلا
/// N+1. أمّا <see cref="Reasons"/> فيُشتقّ حتميًّا من نفس المكوّنات المخزَّنة هنا ⟹ **صفر عمود
/// إضافيّ وصفر جدول إضافيّ**، والأثر الكمّيّ في §18-2 يبقى كما هو.
/// </para>
///
/// <para>
/// **قرار المالك · ملحق W1-A بند 1 — <see cref="Reasons"/> مشتقّة نهائيًّا (Derived)**: هذا
/// **قرار مقفل** لا خيار تصميميّ مفتوح. لا تُخزَّن الأسباب في قاعدة البيانات: **لا عمود JSON،
/// ولا جدول أسباب، ولا أيّ عمود إضافيّ** — لا اليوم ولا عند التوسّع.
/// </para>
///
/// <para>
/// **المبدأ الحاكم بلسان المالك**: «الصحّة **لقطة (Snapshot) للحالة الحاليّة**؛ أمّا **التاريخ**
/// فيُبنى لاحقًا في <c>Project Timeline / Audit / Activity</c>. لا أريد تكرار بيانات يمكن اشتقاقها».
/// وعليه: من احتاج «لماذا كان المشروع أحمر الشهر الماضي؟» فجوابه في سجلّ النشاط لا في صفّ الصحّة.
/// وأيّ اقتراح مستقبليّ بتخزين الأسباب يُرفَض بهذا البند صراحةً ما لم يُنقَض بقرار مالك جديد.
/// </para>
///
/// <para>
/// **ما يجعل الاشتقاق آمنًا**: الأسباب دالّة حتميّة (Pure Function) في المكوّنات الثلاثة المخزَّنة
/// + بيانات المشروع القائمة؛ نفس المدخلات تعطي نفس الرموز دائمًا. ورموزها ثوابت نصّيّة مستقرّة
/// في <see cref="ProjectHealthReasonCodes"/> ⟹ الترجمة والعرض من الكتالوج لا من الكود.
/// </para>
///
/// <para>
/// **لا منطق هنا**: هذا النموذج **حاوٍ للنتيجة** لا محرّك لها. معادلات §9-8 والعتبات وأوزان
/// المكوّنات تعيش في <c>ProjectHealthPolicy</c> بطبقة التطبيق (W5). الفصل مقصود: الدومين يصف
/// **ماذا** تعني الصحّة، والتطبيق يقرّر **كيف** تُحسَب — والواجهة **عرض فقط** لا تحسب شيئًا.
/// </para>
///
/// <para>
/// **المكوّنات الثلاثة قابلة للغياب** (<c>null</c>) عمدًا: «غير محتسَب» ≠ «صفر». مشروع بلا
/// مؤشّرات بعدُ يُقاس بتقدّمه وجدوله، ويُعاد تسوية الأوزان على المتاح — لا يُعاقَب بصفر.
/// </para>
/// </summary>
/// <param name="Score">النتيجة النهائيّة 0..100 — <c>null</c> حين لا يتوفّر أيّ مكوّن.</param>
/// <param name="Status">اللون المشتقّ من <paramref name="Score"/> بعتبات §9-8.</param>
/// <param name="Reasons">تفسير الحكم برموز مستقرّة — قد تكون فارغة لا <c>null</c>.</param>
/// <param name="LastEvaluatedAtUtc">ختم آخر تقييم — <c>null</c> يعني «لم يُقيَّم بعد» لا «صفر».</param>
/// <param name="KpiScore">مكوّن المؤشّرات الموزون (وزن 0.50) — <c>null</c> ⟹ مُستبعَد.</param>
/// <param name="ProgressPercent">مكوّن التقدّم المعلَن (وزن 0.30) — <c>null</c> ⟹ مُستبعَد.</param>
/// <param name="ScheduleScore">مكوّن الجدول الزمنيّ (وزن 0.20) — <c>null</c> ⟹ مُستبعَد.</param>
public sealed record ProjectHealthSnapshot(
    decimal? Score,
    ProjectHealthStatus Status,
    IReadOnlyList<ProjectHealthReason> Reasons,
    DateTime? LastEvaluatedAtUtc,
    decimal? KpiScore,
    decimal? ProgressPercent,
    decimal? ScheduleScore)
{
    /// <summary>
    /// «لم يُقيَّم بعد» — حالة مشروع أُنشئ للتوّ بلا مؤشّرات ولا تواريخ.
    ///
    /// <para>
    /// **كانت هذه اللقطة تُعلن <c>Green</c>** (P360-WF-R2 · GAP-05): مشروع لم يُقَس قطّ كان يظهر
    /// «سليمًا» بلا ختم تقييم واحد — وهو أسوأ من الخطأ لأنّه يطمئن المدير على ما لم يُفحَص.
    /// الحالة الآن <see cref="ProjectHealthStatus.NotEvaluated"/> صراحةً: **الغياب يُعلَن ولا يُلوَّن**.
    /// </para>
    /// </summary>
    public static ProjectHealthSnapshot NotEvaluated { get; } = new(
        Score: null,
        Status: ProjectHealthStatus.NotEvaluated,
        Reasons: new[] { new ProjectHealthReason(ProjectHealthReasonCodes.NoComponentAvailable) },
        LastEvaluatedAtUtc: null,
        KpiScore: null,
        ProgressPercent: null,
        ScheduleScore: null);

    /// <summary>هل أنتج التقييم نتيجة رقميّة فعليّة؟ (تمييز «غير محتسَب» عن «صفر»).</summary>
    public bool IsEvaluated => Score is not null && LastEvaluatedAtUtc is not null;
}

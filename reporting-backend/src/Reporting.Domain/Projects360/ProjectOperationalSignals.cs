using Reporting.Domain.Enums;

namespace Reporting.Domain.Projects360;

/// <summary>
/// **الإشارات التشغيليّة** التي تحكم الحالة الظاهرة للصحّة (P360-WF-R2 §7).
///
/// <para>
/// **لماذا لا تدخل المعادلة الرقميّة؟** لأنّ الحجب والتأخّر ليسا «درجة» بل **حقيقة**: مشروع فيه
/// حاجب مفتوح لا يصير سليمًا لأنّ متوسّطه الموزون مرتفع. المعادلة تبقى كما هي (0.50/0.30/0.20)
/// وتنتج <c>Score</c>؛ وهذه الإشارات **تتقدّم عليها** في اشتقاق الحالة الظاهرة وحدها.
/// هذا يُغلِق العيب المرصود: «لا يصبح Green لمجرّد KPI مرتفع مع وجود Blocker».
/// </para>
///
/// <para>
/// كلّ حقل هنا **عدد مُحصى من الدومين لحظة الحساب** لا رأي ولا نصّ: صفر يعني «لا إشارة»
/// ولا يعني «لم يُفحَص». الغياب الكامل للإشارات يُمثَّل بـ<see cref="None"/>.
/// </para>
/// </summary>
/// <param name="OpenBlockerCount">عدد الحواجب المفتوحة المسجَّلة على المشروع.</param>
/// <param name="OverdueDeliverableCount">عدد المخرَجات التعاقديّة النشطة التي تجاوزت استحقاقها بلا تسليم.</param>
/// <param name="OpenHighRiskCount">عدد المخاطر المفتوحة بخطورة مرتفعة أو حرجة.</param>
/// <param name="ProgressMode">وضع احتساب التقدّم — يُصدِر سببًا مفسِّرًا لا حكمًا.</param>
public sealed record ProjectOperationalSignals(
    int OpenBlockerCount = 0,
    int OverdueDeliverableCount = 0,
    int OpenHighRiskCount = 0,
    ProjectProgressMode ProgressMode = ProjectProgressMode.NotCalculated)
{
    /// <summary>لا إشارة تشغيليّة واحدة — يُستعمَل حيث لا تُتاح الإشارات (اختبارات الوحدة النقيّة).</summary>
    public static ProjectOperationalSignals None { get; } = new();

    /// <summary>حاجب مفتوح واحد على الأقلّ ⟹ الحالة <c>Blocked</c> مهما بلغت النتيجة.</summary>
    public bool HasBlocker => OpenBlockerCount > 0;

    /// <summary>مخرَج متأخّر واحد على الأقلّ ⟹ الحالة <c>Delayed</c> على الأقلّ.</summary>
    public bool HasOverdueDeliverable => OverdueDeliverableCount > 0;

    /// <summary>خطر مرتفع مفتوح واحد على الأقلّ ⟹ الحالة <c>AtRisk</c> على الأقلّ.</summary>
    public bool HasHighRisk => OpenHighRiskCount > 0;
}

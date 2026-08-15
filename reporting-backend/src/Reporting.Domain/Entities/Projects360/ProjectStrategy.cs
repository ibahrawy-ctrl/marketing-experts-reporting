using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// النواة الثابتة لاستراتيجيّة المشروع (CPW-R3 · D-04 · §5-3-أ) — علاقة 1:1 مع المشروع.
///
/// <para>
/// **المبدأ المعماريّ الحاكم**: «لا يوجد نموذج ثابت لكلّ المشاريع». لذلك لا توجد — ولن توجد —
/// استراتيجيّة خاصّة بالـSEO ولا أخرى خاصّة بالإعلانات. هذا الكيان يحمل الحقول الإحدى عشرة
/// التي تصلح لأيّ مشروع مهما كان نوعه، بينما تُحمَل الحقول المشروطة بنوع الخدمة في
/// <see cref="ProjectStrategyAttribute"/> كأزواج (رمز حقل ⟵ قيمة) محكومة بكتالوج التصنيفات.
/// </para>
///
/// <para>
/// **أثر ذلك على التوسّع**: إضافة نوع مشروع جديد مستقبلًا (فيديو، علاقات عامّة، تجارة إلكترونيّة…)
/// = بذر رموز جديدة في مجال الكتالوج <c>strategy_field</c> + سطر واحد في خريطة القراءة بطبقة التطبيق.
/// **صفر تعديل على هذا الكيان، صفر هجرة، صفر تغيير في الدومين.**
/// </para>
///
/// <para>
/// **كلّ الحقول اختياريّة عمدًا**: الاستراتيجيّة تُبنى تدريجيًّا مع العميل، ولا يجوز أن يمنع
/// حقل ناقص حفظ ما اكتمل. الحقل الإلزاميّ الوحيد هو <see cref="ProjectId"/>.
/// </para>
///
/// <para>
/// **جاهزيّة الإصدارات (Versioning) — قرار المالك · ملحق W1-A بند 5**: لا إصدارات اليوم،
/// و**لا شيء في التصميم يمنعها غدًا**. الضمانة ليست حقلًا مضافًا بل **شكل قيد التفرّد**:
/// التفرّد على <c>(ProjectId)</c> **مشروطًا بـ<see cref="IsActive"/> = true</c>** (فهرس فريد جزئيّ)
/// لا على <c>(ProjectId)</c> مطلقًا. الفرق حاسم: القيد المطلق يجعل الصفّ الثاني **مستحيلًا**
/// فيُجبِر الترقية على إسقاط قيد قائم (تغيير كاسر)، بينما القيد المشروط يسمح ببقاء نسخ تاريخيّة
/// غير نشطة **من اليوم** مع بقاء «استراتيجيّة نشطة واحدة لكلّ مشروع» مفروضًا على القاعدة.
/// </para>
///
/// <para>
/// **مسار الترقية المستقبليّ إذن إضافيّ بحت**: إضافة <c>VersionNumber</c> + <c>SupersededAtUtc</c>
/// (عمودان) وتحويل التحرير إلى «تعطيل الحالية + إنشاء نسخة» — **بلا إسقاط قيد، وبلا تغيير مفتاح،
/// وبلا نقل بيانات، وبلا مساس بالسمات** لأنّها معلّقة بـ<c>ProjectStrategyId</c> لا بـ<c>ProjectId</c>.
/// وهذا سبب إضافيّ لكون السمة تابعةً للاستراتيجيّة لا للمشروع مباشرةً.
/// </para>
/// </summary>
public class ProjectStrategy : BaseEntity
{
    /// <summary>
    /// المشروع صاحب الاستراتيجيّة. «استراتيجيّة نشطة واحدة لكلّ مشروع» مفروضة بفهرس فريد
    /// **جزئيّ** على هذا العمود مشروطًا بـ<see cref="IsActive"/> — انظر فقرة جاهزيّة الإصدارات.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>رؤية المشروع.</summary>
    public string? Vision { get; set; }

    /// <summary>ملخّص الاستراتيجيّة.</summary>
    public string? StrategySummary { get; set; }

    /// <summary>الجمهور المستهدَف.</summary>
    public string? TargetAudience { get; set; }

    /// <summary>شخصيّة العميل النموذجيّة.</summary>
    public string? CustomerPersona { get; set; }

    /// <summary>التموضع في السوق.</summary>
    public string? Positioning { get; set; }

    /// <summary>القيمة المقدَّمة.</summary>
    public string? ValueProposition { get; set; }

    /// <summary>المنافسون.</summary>
    public string? Competitors { get; set; }

    /// <summary>نبرة الصوت.</summary>
    public string? ToneOfVoice { get; set; }

    /// <summary>الرسائل الأساسيّة.</summary>
    public string? Messaging { get; set; }

    /// <summary>التوجّه التسويقيّ العامّ.</summary>
    public string? MarketingApproach { get; set; }

    /// <summary>عوامل النجاح.</summary>
    public string? SuccessFactors { get; set; }

    /// <summary>التعطيل بدل الحذف. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>السمات المشروطة بنوع الخدمة (مفتاح/قيمة محكوم بكتالوج <c>strategy_field</c>).</summary>
    public ICollection<ProjectStrategyAttribute> Attributes { get; set; } = new List<ProjectStrategyAttribute>();
}

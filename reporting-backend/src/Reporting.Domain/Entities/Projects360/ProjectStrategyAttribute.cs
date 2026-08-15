using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// سمة استراتيجيّة مشروطة بنوع الخدمة (CPW-R3 · D-04 · §5-3-ب) — الجزء القابل للتوسّع من محرّك الاستراتيجيّة.
///
/// <para>
/// **لماذا مفتاح/قيمة بدل عمود لكلّ حقل؟** لأنّ عمودًا لكلّ حقل لكلّ نوع خدمة يعني تعديل سكيمة
/// عند كلّ نوع مشروع جديد، وهو انتهاك مباشر لقاعدة <c>Additive-Only</c> على المدى الطويل.
/// هنا تُضاف السمات الجديدة **بيانات** لا **بنية**.
/// </para>
///
/// <para>
/// **الحدّ الأمنيّ الحاكم**: <see cref="FieldCode"/> نصّ لا مفتاح أجنبيّ، لكنّه **ليس رمزًا حرًّا** —
/// يُتحقَّق منه على الخادم مقابل مجال الكتالوج <c>strategy_field</c> قبل الحفظ، وإلّا
/// <c>project_strategy.field_code_invalid</c> (400).
/// </para>
///
/// <para>
/// **الفهرس الفريد** <c>(ProjectStrategyId, FieldCode)</c>: سمة واحدة لكلّ رمز حقل لكلّ استراتيجيّة.
/// </para>
/// </summary>
public class ProjectStrategyAttribute : BaseEntity
{
    /// <summary>الاستراتيجيّة الأمّ (حذف تعاقبيّ Cascade).</summary>
    public Guid ProjectStrategyId { get; set; }
    public ProjectStrategy? ProjectStrategy { get; set; }

    /// <summary>رمز الحقل من كتالوج التصنيفات (Domain=strategy_field). نصّ فقط — لا مفتاح أجنبيّ.</summary>
    public string FieldCode { get; set; } = string.Empty;

    /// <summary>
    /// رمز **القسم** الذي تُعرَض تحته السمة (Domain=<c>strategy_section</c>) — **قرار المالك · ملحق W1-A بند 3**.
    ///
    /// <para>
    /// **لماذا رمز قسم لا تعداد ولا تجميع في الواجهة؟** لأنّ الهدف المُعلَن هو أن **تعرف الواجهة
    /// كيف تعرض بلا Hard Coding**. لو كان القسم تعدادًا لصار كلّ قسم جديد تعديلَ دومين + هجرة +
    /// مراجعة كلّ <c>switch</c>؛ ولو كان التجميع في الواجهة لعاد المنطق إلى العرض وهو ممنوع.
    /// كونه رمز كتالوج يجعل «قسم جديد» = **صفّ بيانات واحد** يحمل اسمه العربيّ وترتيبه،
    /// والواجهة تجمّع وترتّب بما يصلها من الخادم.
    /// </para>
    ///
    /// <para>
    /// الأقسام الأوّليّة المبذورة في W4: <c>business</c> · <c>audience</c> · <c>seo</c> ·
    /// <c>ads</c> · <c>brand</c> · <c>social</c>. يُتحقَّق من الرمز على الخادم مقابل مجال الكتالوج
    /// قبل الحفظ — قيمة خارجه مرفوضة بـ<c>project_strategy.section_code_invalid</c> (400).
    /// </para>
    ///
    /// <para>
    /// **اختياريّ عمدًا**: سمة بلا قسم تُعرَض في المجموعة الافتراضيّة بدل أن يُرفَض حفظها.
    /// </para>
    /// </summary>
    public string? SectionCode { get; set; }

    /// <summary>القيمة النصّيّة للسمة.</summary>
    public string? ValueText { get; set; }

    /// <summary>
    /// ترتيب العرض **داخل القسم** (تصاعديّ) — **قرار المالك · ملحق W1-A بند 2**.
    /// الترتيب الكامل للنموذج = ترتيب القسم (من الكتالوج) ثمّ هذا الحقل، فتُبنى شاشة الاستراتيجيّة
    /// كلّها من بيانات لا من كود.
    /// الاسم التقنيّ موحَّد على اصطلاح <c>SortOrder</c> القائم؛ و«ترتيب العرض» تسمية الواجهة فقط.
    /// </summary>
    public int SortOrder { get; set; }
}

using System.Text.RegularExpressions;

namespace Reporting.Application.Common;

/// <summary>
/// COURSE-DUPLICATE-MERGE-R1 — السياسة المركزية الوحيدة لتوحيد أسماء الدورات وقت القراءة/العرض/التجميع.
///
/// المشكلة: دورة واحدة ظهرت باسمين مختلفين في خلايا التقارير (شبكات B2C) بسبب إعادة تسمية الكتالوج
/// ثمّ إعادة إنشاء الاسم القديم بواسطة CourseSeeder ⇒ انقسمت مؤشراتها إلى مجموعتين في لوحات المبيعات.
///
/// الاستراتيجية B (المعتمَدة): لا تُعدَّل خلايا التقارير التاريخية إطلاقًا (دليل تاريخيّ ثابت)؛
/// التوحيد يحدث حصريًّا وقت القراءة عبر مفتاح تجميع موحّد + اسم عرض موحّد.
///
/// دمج صريح لأسماء بديلة معتمَدة فقط — لا مطابقة ضبابية. أيّ اسم دورة آخر يمرّ كما هو (بعد trim/طيّ المسافات فقط).
/// مصدر الحقيقة الوحيد لتوحيد اسم الدورة في كامل النظام (لا تكرار لأسماء مضمّنة متناثرة).
/// </summary>
public static class CourseNamePolicy
{
    /// <summary>الاسم الرسميّ النهائيّ الظاهر للدورة الموحّدة (بلا «ال» بادئة).</summary>
    public const string CanonicalDigitalDiploma = "دبلوم التسويق الرقمي والنمو";

    // يجب أن يُهيَّأ قبل DigitalDiplomaAliasKeys لأنّ NormalizeCore يستعمله (ترتيب تهيئة الحقول الساكنة).
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// الأسماء البديلة المعتمَدة (بعد التطبيع الأساسيّ) التي تُدمَج تحت الدورة الموحّدة.
    /// تشمل: الاسم القديم «الدبلوم الشامل»، والاسم الانتقاليّ ذو «ال» الزائدة، والاسم النهائيّ نفسه.
    /// </summary>
    private static readonly HashSet<string> DigitalDiplomaAliasKeys = new(StringComparer.Ordinal)
    {
        NormalizeCore("الدبلوم الشامل"),
        NormalizeCore("الدبلوم التسويق الرقمي والنمو"),
        NormalizeCore("دبلوم التسويق الرقمي والنمو"),
    };

    /// <summary>
    /// تطبيع أساسيّ: trim للطرفين + طيّ المسافات الداخلية المتكرّرة إلى مسافة واحدة + توحيد الحالة اللاتينية.
    /// لا يمسّ الحروف العربية (ToLowerInvariant لا يغيّرها) ⇒ آمن لأسماء الدورات العربية.
    /// </summary>
    private static string NormalizeCore(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return WhitespaceRun.Replace(raw.Trim(), " ").ToLowerInvariant();
    }

    /// <summary>هل الاسم اسمٌ بديلٌ معتمَدٌ صراحةً للدورة الموحّدة؟ (لا مطابقة ضبابية).</summary>
    public static bool IsAliasOfCanonicalCourse(string? name)
        => DigitalDiplomaAliasKeys.Contains(NormalizeCore(name));

    /// <summary>
    /// مفتاح التجميع الموحّد: كلّ الأسماء البديلة المعتمَدة تعود لمفتاح واحد (مفتاح الدورة الموحّدة)؛
    /// أيّ اسم آخر يُطبَّع فقط (trim/طيّ مسافات/حالة لاتينية) دون تغيير جوهر الاسم. يُستخدَم قبل أيّ تجميع.
    /// </summary>
    public static string NormalizeForGrouping(string? name)
    {
        var key = NormalizeCore(name);
        return DigitalDiplomaAliasKeys.Contains(key) ? NormalizeCore(CanonicalDigitalDiploma) : key;
    }

    /// <summary>
    /// اسم العرض الرسميّ: الأسماء البديلة المعتمَدة ⇒ الاسم الموحّد الرسميّ حرفيًّا؛
    /// أيّ اسم آخر ⇒ الأصل بعد trim وطيّ المسافات (يحافظ على التهجئة الأصلية للدورات غير المعنيّة).
    /// </summary>
    public static string GetCanonicalDisplayName(string? name)
    {
        if (IsAliasOfCanonicalCourse(name)) return CanonicalDigitalDiploma;
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return WhitespaceRun.Replace(name.Trim(), " ");
    }
}

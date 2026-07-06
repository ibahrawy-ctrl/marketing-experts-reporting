using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Courses;

/// <summary>
/// دورة تدريبية ضمن كتالوج الدورات (المصدر الرسمي لأسماء دورات مبيعات B2C).
/// إضافة بحتة: تُغذّي منتقي «الدورة» في قالب مبيعات B2C حسب الدورة، وتوحّد الأسماء المُدخَلة يدويًّا.
/// لا ترتبط بأي جدول قائم (لا FK إلى التقارير) — التقارير القديمة النصّية تبقى كما هي (توافق خلفي).
/// </summary>
public class Course : BaseEntity
{
    /// <summary>الاسم المعروض بالعربية (المخزَّن كلقطة اسم في تقارير B2C).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>الاسم بالإنجليزية (اختياري).</summary>
    public string? NameEn { get; set; }

    /// <summary>تعطيل الدورة يُخفيها من المنتقي دون حذفها (التقارير القديمة تبقى صالحة).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>ترتيب العرض في المنتقي (تصاعدي).</summary>
    public int SortOrder { get; set; }
}

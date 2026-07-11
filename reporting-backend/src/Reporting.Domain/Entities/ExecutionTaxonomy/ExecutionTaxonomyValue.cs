using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.ExecutionTaxonomy;

/// <summary>
/// قيمة ضمن كتالوج تصنيفات التنفيذ (Execution Taxonomy) — المصدر الرسمي للقوائم الثابتة
/// التي تُغذّي حقول SingleSelect داخل قسم المشاريع (ProjectRepeatableSection) في قوالب التنفيذ v3
/// (كاتب المحتوى/المصمّم/الفيديو/المديرشن).
/// إضافة بحتة على غرار كتالوج الدورات/الخدمات — لا ترتبط بأي جدول قائم (لا FK إلى التقارير).
/// التقارير القديمة النصّية تبقى صالحة (توافق خلفي): القيمة تُخزَّن كلقطة نصّية في التقرير.
/// تعطيل القيمة (IsActive=false) يُخفيها من التقارير الجديدة دون كسر التقارير القديمة.
/// </summary>
public class ExecutionTaxonomyValue : BaseEntity
{
    /// <summary>المفتاح البرمجي الثابت للقيمة (إنجليزي، فريد ضمن نفس الـ Domain).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم المعروض بالعربية (المخزَّن كلقطة اسم في التقرير).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>الاسم بالإنجليزية (اختياري).</summary>
    public string? NameEn { get; set; }

    /// <summary>
    /// البُعد/المجال الذي تنتمي إليه القيمة (مثل content_type / design_status / video_duration / activity_type).
    /// كل بُعد يُنتِج قائمة SingleSelect مستقلّة داخل القالب.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>تعطيل القيمة يُخفيها من التقارير الجديدة دون حذفها (التقارير القديمة تبقى صالحة).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>ترتيب العرض داخل قائمة الـ Domain (تصاعدي).</summary>
    public int SortOrder { get; set; }
}

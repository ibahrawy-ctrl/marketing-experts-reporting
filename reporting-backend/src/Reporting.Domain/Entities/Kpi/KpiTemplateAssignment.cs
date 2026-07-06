using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>
/// إسناد/استثناء صريح لقالب مؤشرات أداء (KPI) على مستوى (موظّف/مسمّى وظيفي/فريق/إدارة).
/// يحاكي تمامًا <see cref="Reporting.Domain.Entities.Templates.ReportTemplateAssignment"/>:
/// إضافيّ فوق نظام JobRole القائم (<see cref="KpiTemplate.JobRoleId"/> يبقى كما هو ويُعامَل
/// كإسناد ضمنيّ على مستوى المسمّى الوظيفي). يخدم هذا الجدول إسنادات Employee/Team/Department
/// والاستثناءات الصريحة عند أيّ مستوى، مع طبقة حلّ أولوية موحَّدة في خدمة قوالب KPI.
/// (Phase T1 — رؤية/اختيار قالب فقط؛ لا يمسّ التقييمات القائمة المرتبطة بنسخة مجمّدة.)
/// </summary>
public class KpiTemplateAssignment : BaseEntity
{
    /// <summary>قالب KPI المُسنَد.</summary>
    public Guid KpiTemplateId { get; set; }
    public KpiTemplate? KpiTemplate { get; set; }

    /// <summary>مستوى الإسناد (موظّف/مسمّى/فريق/إدارة).</summary>
    public TemplateAssignmentScope ScopeType { get; set; }

    /// <summary>معرّف الموظّف/المسمّى/الفريق/الإدارة بحسب <see cref="ScopeType"/>.</summary>
    public Guid ScopeId { get; set; }

    /// <summary>إسناد (Include) أو استثناء (Exclude).</summary>
    public TemplateAssignmentKind Kind { get; set; }

    /// <summary>ملاحظة اختيارية تشرح سبب الإسناد/الاستثناء.</summary>
    public string? Notes { get; set; }

    /// <summary>تعطيل دون حذف — صفوف غير النشطة تُتجاهَل في الحلّ.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>منشئ الإسناد (تدقيق).</summary>
    public Guid? CreatedById { get; set; }

    /// <summary>آخر مَن عدّل الإسناد (تدقيق).</summary>
    public Guid? UpdatedById { get; set; }
}

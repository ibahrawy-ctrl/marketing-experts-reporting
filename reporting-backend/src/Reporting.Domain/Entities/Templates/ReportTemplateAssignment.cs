using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Templates;

/// <summary>
/// إسناد/استثناء صريح لقالب تقرير على مستوى (موظّف/مسمّى وظيفي/فريق/إدارة).
/// إضافيّ تمامًا فوق نظام JobRole القائم (<see cref="ReportTemplate.JobRoleId"/> يبقى كما هو
/// ويُعامَل كإسناد على مستوى المسمّى الوظيفي). يخدم هذا الجدول إسنادات Employee/Team/Department
/// والاستثناءات الصريحة عند أيّ مستوى، مع طبقة حلّ أولوية موحَّدة في الخدمة.
/// </summary>
public class ReportTemplateAssignment : BaseEntity
{
    /// <summary>القالب المُسنَد.</summary>
    public Guid ReportTemplateId { get; set; }
    public ReportTemplate? ReportTemplate { get; set; }

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

using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Org;

/// <summary>
/// عضوية فريق إضافية (ثانوية) لمستخدم — MULTI-TEAM-MEMBERSHIP-MVP-R1.
/// منفصلة تمامًا عن <c>ApplicationUser.TeamId</c> (الفريق الأساسي): هذا الجدول يسمح بانتماء المستخدم
/// لفريق آخر بصفة «إضافية» دون نقله من فريقه الأساسي ودون تغيير أي حقل تنظيمي عليه
/// (TeamId/DepartmentId/ManagerId/JobRoleId). لا يدخل ScopeResolver ولا يؤثّر على التقارير/الـKPI/المشاريع.
/// </summary>
public class UserTeamMembership : BaseEntity
{
    /// <summary>المستخدم العضو إضافيًّا.</summary>
    public Guid UserId { get; set; }

    /// <summary>الفريق الذي يُضاف إليه المستخدم بصفة إضافية (ليس فريقه الأساسي).</summary>
    public Guid TeamId { get; set; }

    /// <summary>ملاحة قراءة للفريق (لقوائم العرض).</summary>
    public Team? Team { get; set; }

    /// <summary>هل العضوية الإضافية نشطة؟ (إلغاء التفعيل بدل الحذف الصلب يحفظ السجل التاريخي).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>نوع العضوية — في MVP «Secondary» فقط (يُترك قابلًا للتوسعة مستقبلًا).</summary>
    public string MembershipType { get; set; } = "Secondary";

    /// <summary>بداية العضوية الإضافية (اختياري).</summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>نهاية العضوية الإضافية (اختياري).</summary>
    public DateTime? EndDateUtc { get; set; }

    /// <summary>من أنشأ العضوية الإضافية (اختياري — للتدقيق).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>ملاحظة حرّة (اختياري).</summary>
    public string? Notes { get; set; }
}

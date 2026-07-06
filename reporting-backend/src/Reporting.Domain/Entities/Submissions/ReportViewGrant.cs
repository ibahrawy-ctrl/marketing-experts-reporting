using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Submissions;

/// <summary>
/// منح رؤية تقارير مخفيّ للقراءة فقط (REPORT-VIEW-GRANTS-R1).
/// يسمح للأدمن بمنح مستخدم (المستفيد) رؤيةً للقراءة فقط لتقارير مستخدم آخر أو لتقارير أعضاء فريق،
/// دون جعله عضوًا في الفريق ودون أيّ قدرة اعتماد/تعديل/إرجاع/تصعيد، ودون أن يظهر المستفيد داخل الفريق.
///
/// عزل صارم: هذا الكيان لا يدخل ScopeResolver ولا KPI ولا Dashboard ولا رؤية المشاريع/العملاء؛
/// يُستهلك حصرًا في مسار قراءة التقارير (SubmissionService) لإضافة تقارير مُصرَّح برؤيتها بحالات معتمدة فقط
/// (لا مسودّات ولا مُعادة للتعديل). الإلغاء soft (IsActive=false + RevokedAtUtc) لا حذف صلب.
/// </summary>
public class ReportViewGrant : BaseEntity
{
    /// <summary>المستخدم المستفيد الذي يُمنَح رؤية القراءة فقط.</summary>
    public Guid GranteeUserId { get; set; }

    /// <summary>نوع نطاق المنح: مستخدم بعينه أو فريق.</summary>
    public ReportViewGrantScopeKind ScopeKind { get; set; }

    /// <summary>المستخدم المستهدَف (عند ScopeKind=User) — تقاريره هي المرئيّة. null عند نطاق الفريق.</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>الفريق المستهدَف (عند ScopeKind=Team) — تقارير أعضائه هي المرئيّة. null عند نطاق المستخدم.</summary>
    public Guid? TargetTeamId { get; set; }

    /// <summary>هل المنح نشط؟ (الإلغاء soft بدل الحذف يحفظ السجل التاريخي؛ يُعاد تفعيله بدل التكرار).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>من أنشأ المنح (الأدمن) — للتدقيق.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>وقت الإلغاء (soft) إن أُلغي.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>من ألغى المنح.</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>وقت انتهاء صلاحية المنح اختياريًّا (بعده لا يُعتبر فعّالًا حتى لو IsActive=true).</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>ملاحظة حرّة (سبب المنح مثلًا).</summary>
    public string? Notes { get; set; }
}

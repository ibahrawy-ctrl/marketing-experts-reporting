using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.Projects360;

/// <summary>
/// لقطة المشروع المرئيّ — تُحمَّل **مرّة واحدة** لكلّ عمليّة وتُمرَّر بدل إعادة الاستعلام.
/// تحمل الحقول التي تحتاجها حرّاس التخويل ومحرّك الصحّة معًا (صفر استعلام إضافيّ).
/// </summary>
public sealed record Project360Context(
    Guid ProjectId,
    Guid ClientId,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal ProgressPercent,
    Guid? TeamLeaderId,
    Guid? AccountManagerId,
    Guid? ProjectOwnerId);

/// <summary>
/// **بوّابة الرؤية والتخويل الموحّدة لـProject 360** (CPW-R3 · W4 · §2).
///
/// <para>
/// **لا نطاق جديد**: الرؤية تُشتقّ حصرًا من <c>IClientProjectAccess</c> القائم — لم تُوسَّع رؤية
/// المشاريع بحرف، ولم يُنشأ دور ولا Policy جديدة. الجديد هنا **قيود كتابة أضيق** فقط.
/// </para>
///
/// <para>
/// **مستويا الكتابة (R2 §12)**:
/// <list type="number">
///   <item><description>
///     **بنيويّ** — إنشاء/تعديل/حذف الأهداف والمؤشّرات والمخرَجات التعاقديّة والاستراتيجيّة:
///     أدوار الإدارة (<c>Roles.ProjectPlanManagers</c>) حصرًا.
///   </description></item>
///   <item><description>
///     **تشغيليّ** — التقدّم والحالة والقراءات اليدويّة (D-07): الإدارة **أو** قائد الفريق المسؤول
///     (<c>Project.TeamLeaderId</c>) **أو** مدير الحساب المسؤول (<c>Project.AccountManagerId</c>).
///   </description></item>
/// </list>
/// كلا المستويين يُطبَّقان **بعد** التحقّق من الرؤية، فلا يمنح أيّ منهما وصولًا لمشروع خارج النطاق.
/// </para>
/// </summary>
public interface IProject360Authorization
{
    /// <summary>
    /// يحمّل المشروع بعد التحقّق من المصادقة والرؤية والوجود — **المدخل الوحيد لكلّ عمليّات القراءة**.
    /// يُرجِع <c>auth.unauthenticated</c> أو <c>auth.forbidden</c> أو <c>project.not_found</c>.
    /// </summary>
    Task<Result<Project360Context>> LoadVisibleProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>هل يملك المستخدم الحاليّ الكتابة **البنيويّة** على هذا المشروع؟</summary>
    Task<bool> CanManageProject360Async(Project360Context project, CancellationToken ct = default);

    /// <summary>هل يملك المستخدم الحاليّ تحديث **التقدّم/الحالة/القراءات** على هذا المشروع؟ (D-07)</summary>
    Task<bool> CanUpdateProject360ProgressAsync(Project360Context project, CancellationToken ct = default);
}

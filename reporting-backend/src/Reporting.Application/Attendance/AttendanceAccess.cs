using Reporting.Application.Common;
using Reporting.Application.Security;

namespace Reporting.Application.Attendance;

/// <summary>
/// P2-ATT-006 — قواعد الوصول إلى وقائع الحضور. **دالّة نقيّة** فوق سياق الرؤية المحسوب خادميًّا،
/// فلا يُشتقّ أيّ قرار هنا من إدخال العميل.
///
/// <para>
/// المبدأ الحاكم: خارج النطاق ⇒ <c>false</c> في كلّ شيء، ونقطة النهاية تُترجم ذلك إلى
/// <c>attendance.not_found</c> (404) لا 403 — كي لا يُسرَّب وجود الواقعة ولا وجود الموظّف.
/// </para>
/// </summary>
public static class AttendanceAccess
{
    /// <summary>
    /// حقّ تسجيل بلاغ على موظّف: إذن صريح، أو قيادة تشغيليّة (قائد فريق/مدير) **داخل نطاقها**.
    /// لا أحد يُبلِّغ عن نفسه، ولا أحد يُبلِّغ خارج نطاقه مهما كان دوره.
    /// </summary>
    public static bool CanReport(FieldVisibilityContext ctx)
    {
        if (!ctx.InScope || ctx.IsSelf) return false;

        if (ctx.HasPermission(AppPermissions.AttendanceReport)) return true;

        return ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager)
               && FieldVisibilityRules.IsSupervisoryRelation(ctx.Relation);
    }

    /// <summary>
    /// حقّ مراجعة الموارد البشريّة (تأكيد/رفض/تصحيح/مصالحة/إبطال/إغلاق).
    /// **مفتاح صريح حصرًا** — لا يمنحه دور <c>Hr</c> ولا <c>Admin</c> ضمنًا.
    /// </summary>
    public static bool CanReview(FieldVisibilityContext ctx) =>
        ctx.HasPermission(AppPermissions.AttendanceReview);

    /// <summary>حقّ التصعيد إلى الحوكمة — مفتاح صريح، ولا يمنح رؤية موارد بشريّة.</summary>
    public static bool CanEscalate(FieldVisibilityContext ctx) =>
        ctx.HasPermission(AppPermissions.AttendanceEscalate);

    /// <summary>حقّ التصدير — **مستقلّ تمامًا** عن حقّ الرؤية، وكلّ تصدير يُدقَّق.</summary>
    public static bool CanExport(FieldVisibilityContext ctx) =>
        ctx.HasPermission(AppPermissions.AttendanceExport);

    /// <summary>
    /// حقّ الاطّلاع على واقعة بعينها: صاحبها، أو مُبلِّغها، أو مشرف تشغيليّ داخل نطاقه،
    /// أو حامل مفتاح مراجعة/تصعيد صريح.
    /// </summary>
    public static bool CanViewIncident(FieldVisibilityContext ctx, Guid reportedByUserId)
    {
        if (ctx.IsSelf) return true;
        if (ctx.ViewerUserId == reportedByUserId) return true;
        if (CanReview(ctx) || CanEscalate(ctx)) return true;

        return ctx.InScope
               && ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager)
               && FieldVisibilityRules.IsSupervisoryRelation(ctx.Relation);
    }
}

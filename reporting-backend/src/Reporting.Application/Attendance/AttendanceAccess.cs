using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Enums;

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
    /// الحالات السابقة للإرسال: الواقعة فيها **ملكُ المُبلِّغ وحده** ولم تصر بلاغًا رسميًّا بعد.
    /// <c>Cancelled</c> لا تُبلَغ إلّا من <c>Draft</c> (انظر جدول <c>AttendanceTransitions</c>)
    /// فهي مسودّة عدَل عنها صاحبها ⇒ تبقى كأن لم تكن بالنسبة إلى الموظّف موضوعها.
    /// </summary>
    private static bool IsPreSubmission(AttendanceIncidentStatus status) =>
        status is AttendanceIncidentStatus.Draft or AttendanceIncidentStatus.Cancelled;

    /// <summary>
    /// حقّ الاطّلاع على واقعة بعينها: صاحبها **بعد إرسالها**، أو مُبلِّغها، أو مشرف تشغيليّ
    /// داخل نطاقه، أو حامل مفتاح مراجعة/تصعيد صريح.
    ///
    /// <para>
    /// DEF-P123-003: الموظّف موضوع الواقعة لا يراها ما دامت سابقةً للإرسال. المسودّة وُجِدت
    /// ليعدّلها المُبلِّغ أو يلغيها قبل أن تصير اتّهامًا رسميًّا؛ وكشفها مبكّرًا يُبطِل معنى
    /// <c>Draft</c> ويُبطِل ضمانة السحب. حقّه يبدأ من <c>Reported</c> (هدف <c>Submit</c>) فصاعدًا.
    /// </para>
    /// <para>الحجب يُترجَم في نقطة النهاية إلى <c>attendance.not_found</c> (404) لا 403.</para>
    /// </summary>
    public static bool CanViewIncident(
        FieldVisibilityContext ctx, Guid reportedByUserId, AttendanceIncidentStatus status)
    {
        // فصل الواجبات: الموضوع يُقيَّم بصفته موضوعًا فقط، ولا ترفعه مفاتيح المراجعة فوق واقعته.
        if (ctx.IsSelf) return !IsPreSubmission(status);

        if (ctx.ViewerUserId == reportedByUserId) return true;
        if (CanReview(ctx) || CanEscalate(ctx)) return true;

        return ctx.InScope
               && ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager)
               && FieldVisibilityRules.IsSupervisoryRelation(ctx.Relation);
    }
}

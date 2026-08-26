using System.Linq.Expressions;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Attendance;
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
    ///
    /// <para>
    /// هذه المصفوفة هي **مصدر الحقيقة الوحيد** لحدّ ما قبل الإرسال: تستهلكها قاعدة العنصر المفرد
    /// <see cref="CanViewIncident"/> وقاعدة الاستعلام <see cref="VisibleIncidentPredicate"/> معًا،
    /// فلا يمكن أن ينحرف السطحان عن بعضهما بتعديل أحدهما وحده.
    /// </para>
    /// </summary>
    public static readonly AttendanceIncidentStatus[] PreSubmissionStatuses =
    {
        AttendanceIncidentStatus.Draft,
        AttendanceIncidentStatus.Cancelled
    };

    /// <summary>هل الواقعة ما زالت سابقةً للإرسال؟ — مشتقّة من <see cref="PreSubmissionStatuses"/>.</summary>
    public static bool IsPreSubmission(AttendanceIncidentStatus status) =>
        Array.IndexOf(PreSubmissionStatuses, status) >= 0;

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

    /// <summary>
    /// DEF-P123-RC-001 — نظير <see cref="CanViewIncident"/> على مستوى **الاستعلام**، بترتيب الفروع نفسه
    /// وبالمصفوفة نفسها لحدّ ما قبل الإرسال.
    ///
    /// <para>
    /// كان سطح القائمة يبني نطاقه من الملكيّة/النطاق وحدها فيُدرِج مسودّةً لصاحبها بينما يردّ سطحُ
    /// التفاصيل عليها <c>404</c>. العلاج ليس إخفاءً بعد الجلب: الشرط يدخل الاستعلام نفسه فيسبق
    /// <c>Count</c> و<c>Skip/Take</c> والإسقاط والتسلسل معًا، فلا يتسرّب وجود المسودّة من عدّاد ولا صفحة.
    /// </para>
    ///
    /// <para>
    /// الثابت المحروس: <b>القائمة ⊆ التفاصيل</b> — كلّ صفّ يعود هنا يفتحه <see cref="CanViewIncident"/>
    /// للمُشاهِد نفسه. الفروع كلّها قابلة للترجمة إلى SQL (لا تقييم على العميل ولا تحميل كامل للجدول).
    /// </para>
    /// </summary>
    /// <param name="viewerUserId">المُشاهِد.</param>
    /// <param name="canReviewOrEscalate">يحمل <c>Attendance.Review</c> أو <c>Attendance.Escalate</c> صراحةً.</param>
    /// <param name="isOperationalSupervisor">قائد فريق أو مدير — الشرط الدوريّ لفرع الإشراف.</param>
    /// <param name="seesAllSubjects">نطاق شركة/حوكمة (يقابل <c>SubjectRelation.Company</c>).</param>
    /// <param name="scopedSubjectUserIds">مَن يقع داخل نطاق المُشاهِد من شجرة <c>ManagerId</c>.</param>
    public static Expression<Func<AttendanceIncident, bool>> VisibleIncidentPredicate(
        Guid viewerUserId,
        bool canReviewOrEscalate,
        bool isOperationalSupervisor,
        bool seesAllSubjects,
        IReadOnlyCollection<Guid> scopedSubjectUserIds)
    {
        // متغيّر محلّيّ لا حقل ساكن: يضمن أن يُقوَّم كمعامل استعلام (‎= ANY‎) لا كتعبير غير مترجَم.
        var preSubmission = PreSubmissionStatuses;
        var scoped = scopedSubjectUserIds as IList<Guid> ?? scopedSubjectUserIds.ToList();

        return i => i.SubjectUserId == viewerUserId
            // فصل الواجبات: الموضوع يُقيَّم بصفته موضوعًا فقط، ولا ترفعه مفاتيح المراجعة فوق واقعته.
            ? !preSubmission.Contains(i.Status)
            : i.ReportedByUserId == viewerUserId
              || canReviewOrEscalate
              || (isOperationalSupervisor && (seesAllSubjects || scoped.Contains(i.SubjectUserId)));
    }
}

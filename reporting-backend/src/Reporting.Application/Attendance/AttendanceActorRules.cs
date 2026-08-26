using Reporting.Application.Workflow;

namespace Reporting.Application.Attendance;

/// <summary>
/// سياق الفاعل أمام حادثة بعينها. كلّ الحقول **محلولة خادميًّا** قبل الوصول إلى هنا؛
/// لا يُشتقّ شيء منها من إدخال العميل.
/// </summary>
/// <param name="ActorUserId">الفاعل.</param>
/// <param name="IsSubject">الفاعل هو صاحب الحادثة.</param>
/// <param name="IsReporter">الفاعل هو مُقدِّم البلاغ.</param>
/// <param name="CanReport">إذن <c>Attendance.Report</c> (أو دور قيادة داخل النطاق).</param>
/// <param name="CanReview">إذن <c>Attendance.Review</c> — الموارد البشريّة.</param>
/// <param name="CanEscalate">إذن <c>Attendance.Escalate</c> — الحوكمة.</param>
/// <param name="EmployeeHasResponded">هل ردّ الموظّف فعلًا (يمنع السحب بعد الإقرار).</param>
/// <param name="IsSystem">الفاعل هو النظام (مهامّ SLA) لا مستخدم بشريّ.</param>
public sealed record AttendanceActorContext(
    Guid ActorUserId,
    bool IsSubject,
    bool IsReporter,
    bool CanReport,
    bool CanReview,
    bool CanEscalate,
    bool EmployeeHasResponded,
    bool IsSystem = false);

/// <summary>
/// P2-ATT-004 — مُخوِّل الفاعل لآلة حالات الحضور. **دالّة نقيّة** منفصلة عن جدول الانتقالات:
/// «الانتقال جائز» و«أنت من يملك تشغيله» سؤالان مختلفان.
///
/// مبادئ مُشفَّرة هنا:
/// - المُبلِّغ **لا يؤكّد بلاغه بنفسه**: <c>HrConfirm</c> تحتاج <c>Attendance.Review</c> حصرًا.
/// - الموظّف يردّ على حادثته وحدها؛ ولا يملك أيّ إجراء موارد بشريّة.
/// - السحب حقّ المُنشِئ وحده وقبل ردّ الموظّف.
/// - انقضاء نافذة الردّ إجراء نظام لا إجراء مستخدم.
/// </summary>
public sealed class AttendanceActorRules
    : IWorkflowActorAuthorizer<AttendanceTrigger, AttendanceActorContext>
{
    private const string Forbidden = "auth.forbidden";

    public WorkflowTransitionResult Authorize(AttendanceTrigger trigger, AttendanceActorContext ctx) => trigger switch
    {
        AttendanceTrigger.Submit or AttendanceTrigger.Cancel =>
            ctx.IsReporter && ctx.CanReport
                ? WorkflowTransitionResult.Ok()
                : Deny("هذا الإجراء لمُنشِئ البلاغ وحده."),

        AttendanceTrigger.Withdraw =>
            !ctx.IsReporter
                ? Deny("سحب البلاغ حقّ مُنشِئه وحده.")
                : ctx.EmployeeHasResponded
                    ? WorkflowTransitionResult.Deny(
                        "attendance.conflict", "لا يمكن سحب البلاغ بعد أن ردّ عليه الموظّف.")
                    : WorkflowTransitionResult.Ok(),

        AttendanceTrigger.Acknowledge or AttendanceTrigger.Dispute =>
            ctx.IsSubject
                ? WorkflowTransitionResult.Ok()
                : Deny("الردّ على الحادثة حقّ صاحبها وحده."),

        // إجراءات النظام (تذكير/انقضاء نافذة/إحالة) لا يملكها مستخدم بشريّ.
        AttendanceTrigger.NotifyEmployee or AttendanceTrigger.TimeOutEmployeeResponse or AttendanceTrigger.SendToHr =>
            ctx.IsSystem
                ? WorkflowTransitionResult.Ok()
                : Deny("هذا انتقال يُجريه النظام لا المستخدم."),

        // فصل الواجبات على **التأكيد وحده**: `Confirmed` هي المخرج الوحيد الذي يجعل البلاغ
        // «واقعة رسميّة» (AttendancePolicy.IsOfficialIncident)، ولا يُبلَغ إليها إلّا بهذا المُشغِّل.
        // قبل هذا الشرط كان إذن المراجعة كافيًا وحده، فلو حمل المُبلِّغ نفسه `Attendance.Review`
        // لأكّد بلاغه بنفسه — وكان الفصل يبدو قائمًا لأنّ قائد الفريق في الاختبارات لا يحمل المفتاح.
        // الرفض هنا **403 لا 404** عن قصد: المُبلِّغ يرى واقعته أصلًا، فإخفاؤها عنه كذب لا حماية.
        AttendanceTrigger.HrConfirm =>
            !ctx.CanReview
                ? Deny("هذا الإجراء يتطلّب إذن مراجعة الحضور.")
                : ctx.IsReporter
                    ? Deny("لا يؤكّد مُقدِّم البلاغ بلاغَه بنفسه — التأكيد لمراجِع آخر.")
                    : WorkflowTransitionResult.Ok(),

        AttendanceTrigger.HrReject or AttendanceTrigger.HrCorrect
            or AttendanceTrigger.HrReconcile or AttendanceTrigger.ReturnToEmployee or AttendanceTrigger.Void =>
            ctx.CanReview
                ? WorkflowTransitionResult.Ok()
                : Deny("هذا الإجراء يتطلّب إذن مراجعة الحضور."),

        AttendanceTrigger.Escalate =>
            ctx.CanEscalate
                ? WorkflowTransitionResult.Ok()
                : Deny("التصعيد يتطلّب إذنًا صريحًا."),

        AttendanceTrigger.Close =>
            ctx.CanReview
                ? WorkflowTransitionResult.Ok()
                : Deny("الإغلاق يتطلّب إذن مراجعة الحضور."),

        _ => Deny("إجراء غير معروف.")
    };

    private static WorkflowTransitionResult Deny(string reasonAr) =>
        WorkflowTransitionResult.Deny(Forbidden, reasonAr);
}

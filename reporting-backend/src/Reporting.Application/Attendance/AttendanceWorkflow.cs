using Reporting.Application.Workflow;
using Reporting.Domain.Enums;

namespace Reporting.Application.Attendance;

/// <summary>مُشغِّلات آلة حالات الحضور (P2-ATT-004/006).</summary>
public enum AttendanceTrigger
{
    /// <summary>إرسال المسودّة — تصير بلاغًا مبدئيًّا.</summary>
    Submit = 0,

    /// <summary>إلغاء مسودّة لم تُرسَل.</summary>
    Cancel = 1,

    /// <summary>إشعار الموظّف وفتح نافذة ردّه.</summary>
    NotifyEmployee = 2,

    /// <summary>سحب البلاغ من مُنشِئه قبل إقرار الموظّف، بسبب موثَّق.</summary>
    Withdraw = 3,

    /// <summary>إقرار الموظّف.</summary>
    Acknowledge = 4,

    /// <summary>اعتراض الموظّف.</summary>
    Dispute = 5,

    /// <summary>انقضاء نافذة ردّ الموظّف.</summary>
    TimeOutEmployeeResponse = 6,

    /// <summary>الإحالة إلى الموارد البشريّة.</summary>
    SendToHr = 7,

    /// <summary>تأكيد الموارد البشريّة — هنا فقط تصير الحادثة واقعة رسميّة.</summary>
    HrConfirm = 8,

    /// <summary>رفض الموارد البشريّة للبلاغ.</summary>
    HrReject = 9,

    /// <summary>تصحيح بيانات الحادثة.</summary>
    HrCorrect = 10,

    /// <summary>تسوية الحادثة بإجازة/استئذان معتمد.</summary>
    HrReconcile = 11,

    /// <summary>إعادة الحادثة المصحَّحة إلى الموظّف لإعادة الردّ.</summary>
    ReturnToEmployee = 12,

    /// <summary>تصعيد إلى الحوكمة بإذن صريح.</summary>
    Escalate = 13,

    /// <summary>الإغلاق.</summary>
    Close = 14,

    /// <summary>إبطال حادثة مؤكَّدة/مغلقة بقرار موارد بشريّة موثَّق — بديل الحذف.</summary>
    Void = 15
}

/// <summary>
/// P2-ATT-004 — جدول انتقالات الحضور. **دالّة نقيّة بلا قاعدة بيانات ولا مستخدم**، فكلّ قاعدة
/// هنا قابلة للإثبات وحدويًّا.
///
/// القواعد المُشفَّرة في الجدول نفسه لا في تعليق:
/// - لا مسار من <c>Draft</c> إلى <c>Confirmed</c> مباشرة.
/// - لا مسار من <c>Corrected</c> إلى <c>Confirmed</c> إطلاقًا: المصحَّح يعود إلى الموظّف أوّلًا.
/// - لا تأكيد قبل ردّ الموظّف أو انقضاء نافذته: <c>HrConfirm</c> لا تُقبل إلّا من <c>AwaitingHr</c>،
///   ولا يُبلَغ <c>AwaitingHr</c> إلّا من <c>Acknowledged</c>/<c>Disputed</c>/<c>EmployeeResponseTimedOut</c>.
/// - <c>Confirmed</c>/<c>Closed</c> لا تُحذف: المخرج الوحيد منهما تصعيدٌ أو إغلاقٌ أو <c>Void</c>.
/// </summary>
public sealed class AttendanceTransitions
    : IWorkflowTransitionValidator<AttendanceIncidentStatus, AttendanceTrigger>
{
    private static readonly Dictionary<(AttendanceIncidentStatus, AttendanceTrigger), AttendanceIncidentStatus> Map =
        new()
        {
            [(AttendanceIncidentStatus.Draft, AttendanceTrigger.Submit)] = AttendanceIncidentStatus.Reported,
            [(AttendanceIncidentStatus.Draft, AttendanceTrigger.Cancel)] = AttendanceIncidentStatus.Cancelled,

            [(AttendanceIncidentStatus.Reported, AttendanceTrigger.NotifyEmployee)] = AttendanceIncidentStatus.AwaitingEmployee,
            [(AttendanceIncidentStatus.Reported, AttendanceTrigger.Withdraw)] = AttendanceIncidentStatus.Withdrawn,

            // السحب متاح ما دام الموظّف لم يُقِرّ بعد — والشرط الزمنيّ يفرضه مُخوِّل الفاعل لا الجدول.
            [(AttendanceIncidentStatus.AwaitingEmployee, AttendanceTrigger.Withdraw)] = AttendanceIncidentStatus.Withdrawn,
            [(AttendanceIncidentStatus.AwaitingEmployee, AttendanceTrigger.Acknowledge)] = AttendanceIncidentStatus.Acknowledged,
            [(AttendanceIncidentStatus.AwaitingEmployee, AttendanceTrigger.Dispute)] = AttendanceIncidentStatus.Disputed,
            [(AttendanceIncidentStatus.AwaitingEmployee, AttendanceTrigger.TimeOutEmployeeResponse)] = AttendanceIncidentStatus.EmployeeResponseTimedOut,

            [(AttendanceIncidentStatus.Acknowledged, AttendanceTrigger.SendToHr)] = AttendanceIncidentStatus.AwaitingHr,
            [(AttendanceIncidentStatus.Disputed, AttendanceTrigger.SendToHr)] = AttendanceIncidentStatus.AwaitingHr,
            [(AttendanceIncidentStatus.EmployeeResponseTimedOut, AttendanceTrigger.SendToHr)] = AttendanceIncidentStatus.AwaitingHr,

            [(AttendanceIncidentStatus.AwaitingHr, AttendanceTrigger.HrConfirm)] = AttendanceIncidentStatus.Confirmed,
            [(AttendanceIncidentStatus.AwaitingHr, AttendanceTrigger.HrReject)] = AttendanceIncidentStatus.Rejected,
            [(AttendanceIncidentStatus.AwaitingHr, AttendanceTrigger.HrCorrect)] = AttendanceIncidentStatus.Corrected,
            [(AttendanceIncidentStatus.AwaitingHr, AttendanceTrigger.HrReconcile)] = AttendanceIncidentStatus.Reconciled,
            [(AttendanceIncidentStatus.AwaitingHr, AttendanceTrigger.Escalate)] = AttendanceIncidentStatus.Escalated,

            // المصحَّح لا يُؤكَّد مباشرة — يعود إلى الموظّف ليردّ من جديد.
            [(AttendanceIncidentStatus.Corrected, AttendanceTrigger.ReturnToEmployee)] = AttendanceIncidentStatus.AwaitingEmployee,

            [(AttendanceIncidentStatus.Confirmed, AttendanceTrigger.Escalate)] = AttendanceIncidentStatus.Escalated,
            [(AttendanceIncidentStatus.Confirmed, AttendanceTrigger.Close)] = AttendanceIncidentStatus.Closed,
            [(AttendanceIncidentStatus.Confirmed, AttendanceTrigger.Void)] = AttendanceIncidentStatus.Voided,

            [(AttendanceIncidentStatus.Rejected, AttendanceTrigger.Close)] = AttendanceIncidentStatus.Closed,
            [(AttendanceIncidentStatus.Reconciled, AttendanceTrigger.Close)] = AttendanceIncidentStatus.Closed,
            [(AttendanceIncidentStatus.Withdrawn, AttendanceTrigger.Close)] = AttendanceIncidentStatus.Closed,
            [(AttendanceIncidentStatus.Escalated, AttendanceTrigger.Close)] = AttendanceIncidentStatus.Closed,

            [(AttendanceIncidentStatus.Closed, AttendanceTrigger.Void)] = AttendanceIncidentStatus.Voided,
        };

    /// <summary>حالات نهائيّة لا يخرج منها شيء.</summary>
    public static readonly IReadOnlySet<AttendanceIncidentStatus> Terminal =
        new HashSet<AttendanceIncidentStatus>
        {
            AttendanceIncidentStatus.Cancelled,
            AttendanceIncidentStatus.Voided
        };

    public WorkflowTransitionResult Validate(AttendanceIncidentStatus from, AttendanceTrigger trigger) =>
        Map.ContainsKey((from, trigger))
            ? WorkflowTransitionResult.Ok()
            : WorkflowTransitionResult.Deny(
                "attendance.conflict",
                $"لا يمكن تنفيذ هذا الإجراء على حادثة في الحالة «{StatusAr(from)}».");

    public AttendanceIncidentStatus Target(AttendanceIncidentStatus from, AttendanceTrigger trigger) =>
        Map.TryGetValue((from, trigger), out var to)
            ? to
            : throw new InvalidOperationException($"انتقال غير مسموح: {from} + {trigger}.");

    public IReadOnlyCollection<AttendanceTrigger> AllowedTriggers(AttendanceIncidentStatus from) =>
        Map.Keys.Where(k => k.Item1 == from).Select(k => k.Item2).ToArray();

    /// <summary>اسم عربيّ للحالة — للرسائل فقط، والقيمة المخزَّنة تبقى الرمز الإنجليزيّ.</summary>
    public static string StatusAr(AttendanceIncidentStatus s) => s switch
    {
        AttendanceIncidentStatus.Draft => "مسودّة",
        AttendanceIncidentStatus.Reported => "بلاغ مبدئيّ",
        AttendanceIncidentStatus.AwaitingEmployee => "بانتظار ردّ الموظّف",
        AttendanceIncidentStatus.Acknowledged => "أقرّ بها الموظّف",
        AttendanceIncidentStatus.Disputed => "معترَض عليها",
        AttendanceIncidentStatus.EmployeeResponseTimedOut => "انقضت نافذة الردّ",
        AttendanceIncidentStatus.AwaitingHr => "بانتظار الموارد البشريّة",
        AttendanceIncidentStatus.Confirmed => "مؤكَّدة",
        AttendanceIncidentStatus.Rejected => "مرفوضة",
        AttendanceIncidentStatus.Corrected => "مصحَّحة",
        AttendanceIncidentStatus.Reconciled => "مسوّاة بإجازة/استئذان",
        AttendanceIncidentStatus.Escalated => "مُصعَّدة",
        AttendanceIncidentStatus.Closed => "مغلقة",
        AttendanceIncidentStatus.Cancelled => "ملغاة",
        AttendanceIncidentStatus.Withdrawn => "مسحوبة",
        AttendanceIncidentStatus.Voided => "مُبطَلة",
        _ => s.ToString()
    };
}

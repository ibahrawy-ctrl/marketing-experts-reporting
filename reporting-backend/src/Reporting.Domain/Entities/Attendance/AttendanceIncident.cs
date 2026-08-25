using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Attendance;

/// <summary>
/// حادثة حضور (P2-ATT-005). **بلاغ مبدئيّ حتّى تُؤكَّد**، وبلا أيّ أثر ماليّ في أيّ حالة:
/// لا خصم، ولا حركة رصيد، ولا ربط برواتب — لا في هذا الكيان ولا في أيّ انتقال عليه.
///
/// لقطات <see cref="TeamId"/>/<see cref="DepartmentId"/> تُثبَّت وقت الإنشاء كي لا تتغيّر
/// الحادثة التاريخيّة بتغيّر الهيكل التنظيميّ لاحقًا.
/// </summary>
public class AttendanceIncident : BaseEntity
{
    /// <summary>الموظّف موضوع الحادثة.</summary>
    public Guid SubjectUserId { get; set; }

    public Guid IncidentTypeId { get; set; }

    /// <summary>تاريخ الواقعة بتقويم الرياض (لا UTC) — الحضور واقعة يوم محلّيّ لا لحظة عالميّة.</summary>
    public DateOnly IncidentDate { get; set; }

    public TimeOnly? StartTime { get; set; }
    public TimeOnly? ReturnTime { get; set; }

    /// <summary>المدّة بالدقائق — مُحتسَبة من الوقتين لا مُدخَلة يدويًّا.</summary>
    public int? DurationMinutes { get; set; }

    public string Description { get; set; } = string.Empty;

    public AttendanceDetectionSource DetectionSource { get; set; } = AttendanceDetectionSource.Manual;

    /// <summary>مُقدِّم البلاغ. لا يملك تأكيد بلاغه بنفسه.</summary>
    public Guid ReportedByUserId { get; set; }

    /// <summary>لقطة الفريق وقت الإنشاء.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>لقطة الإدارة وقت الإنشاء.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>مرجع السياسة/اللائحة إن استلزمه النوع.</summary>
    public Guid? PolicyRefId { get; set; }

    public AttendanceIncidentStatus Status { get; set; } = AttendanceIncidentStatus.Draft;

    // ===== ردّ الموظّف =====
    public string? EmployeeResponse { get; set; }
    public DateTime? RespondedAtUtc { get; set; }

    // ===== قرار الموارد البشريّة =====
    public AttendanceHrDecision HrDecision { get; set; } = AttendanceHrDecision.None;
    public string? HrNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>الإجازة المعتمدة التي سُوِّيت بها الحادثة (اقتراح النظام، والقرار للموارد البشريّة).</summary>
    public Guid? ReconciledWithLeaveId { get; set; }

    /// <summary>الاستئذان المعتمد الذي سُوِّيت به الحادثة.</summary>
    public Guid? ReconciledWithPermissionId { get; set; }

    /// <summary>الحادثة الأصل حين يُكتشَف أنّ هذه تكرار لها.</summary>
    public Guid? DuplicateOfId { get; set; }

    /// <summary>
    /// مفتاح التكافؤ الذي أرسله العميل (<c>Idempotency-Key</c>) — يمنع ازدواج البلاغ عند
    /// إعادة الإرسال الشبكيّ. فريد لكلّ مُبلِّغ.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// رمز التزامن المتفائل: يُزاد مع كلّ انتقال حالة. تصادم القيمة ⟹ 409 لا كتابة فوق قرار غيرك.
    /// </summary>
    public int ConcurrencyStamp { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
}

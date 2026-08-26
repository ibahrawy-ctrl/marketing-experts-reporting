using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Attendance;

/// <summary>
/// حدث في الخطّ الزمنيّ لحادثة حضور — **سجلّ إلحاقيّ فقط** (append-only): لا تعديل ولا حذف.
/// كلّ انتقال حالة يكتب صفًّا هنا، فالتصحيح والإبطال والسحب تبقى مرئيّة بدل أن تُمحى.
/// </summary>
public class AttendanceIncidentEvent : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Guid ActorUserId { get; set; }

    /// <summary>الإجراء: <c>submitted</c>, <c>acknowledged</c>, <c>disputed</c>, <c>hr_confirmed</c>, …</summary>
    public string Action { get; set; } = string.Empty;

    public AttendanceIncidentStatus FromStatus { get; set; }
    public AttendanceIncidentStatus ToStatus { get; set; }

    public string? Comment { get; set; }

    /// <summary>
    /// لقطة الحقول التي تغيّرت في هذا الانتقال (JSON) — بلا أيّ قيمة حسّاسة.
    /// تُستعمل لإثبات التغيير الجوهريّ الذي يوجب إعادة إشعار الموظّف.
    /// </summary>
    public string? ChangesJson { get; set; }
}

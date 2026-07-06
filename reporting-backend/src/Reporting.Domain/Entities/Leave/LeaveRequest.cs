using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Leave;

/// <summary>
/// طلب إجازة أو استئذان (V1.0.1 — رقعة ما قبل النشر). وحدة خفيفة لإدارة الطلبات واعتمادها هرميًّا:
/// الموظّف → قائد الفريق → المدير → الموارد البشرية (الاعتماد النهائي). لا يؤثّر الطلب في التقارير
/// إلا عند بلوغه الحالة <see cref="LeaveRequestStatus.HrApproved"/>. ليست نظام موارد بشرية ولا أرصدة ولا خصومات.
/// </summary>
public class LeaveRequest : BaseEntity
{
    /// <summary>صاحب الطلب (الموظّف). لا يحق له اعتماد طلبه.</summary>
    public Guid RequesterUserId { get; set; }

    public LeaveRequestType Type { get; set; }

    /// <summary>تاريخ البداية (إجازة: يوم البداية؛ استئذان: اليوم الواحد).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>تاريخ النهاية (إجازة: قد يساوي البداية أو يمتدّ؛ استئذان: يساوي البداية).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>وقت بداية الاستئذان (للاستئذان فقط).</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>وقت نهاية الاستئذان (للاستئذان فقط).</summary>
    public TimeOnly? EndTime { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Submitted;
    public LeaveRequestStep CurrentStep { get; set; } = LeaveRequestStep.TeamLeader;

    /// <summary>
    /// طلب مُقدَّم من موظّف يحمل دور الموارد البشرية (V1.0.1-A). لا يراجع HR طلبه بنفسه؛ المسار الخاص:
    /// المدير العام يراجع ثم الإدارة العليا (CEO/Admin) تعتمد نهائيًّا. يُحدَّد عند الإنشاء ولا يتغيّر.
    /// </summary>
    public bool IsHrRequest { get; set; }

    // من اتّخذ كل قرار ومتى (سجلّ خفيف داخل الكيان + خطّ زمني تفصيلي في LeaveRequestEvent).
    public Guid? TeamLeaderReviewerId { get; set; }
    public Guid? ManagerReviewerId { get; set; }
    public Guid? HrReviewerId { get; set; }
    public DateTime? TeamLeaderDecisionAtUtc { get; set; }
    public DateTime? ManagerDecisionAtUtc { get; set; }
    public DateTime? HrDecisionAtUtc { get; set; }

    public string? RejectionReason { get; set; }
    public string? ReturnReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    // ===== لقطة كفاية رصيد الإجازات وقت الإنشاء (V1.1 — حارس الرصيد) =====
    // تُملأ فقط لطلبات الإجازة (Leave) عندما تتجاوز الأيام المطلوبة الرصيد المتاح. لا تخصم شيئًا ولا
    // تربط الرواتب آليًّا — لقطة إعلامية + إقرار الموظّف لتمكين الموارد البشرية/الرواتب لاحقًا.

    /// <summary>الرصيد المتبقّي للإجازات السنوية وقت إنشاء الطلب (لقطة). يُملأ فقط عند نقص الرصيد.</summary>
    public decimal? BalanceAtRequest { get; set; }

    /// <summary>عدد أيام الإجازة المطلوبة (شاملة الطرفين). يُملأ فقط عند نقص الرصيد.</summary>
    public int? RequestedLeaveDays { get; set; }

    /// <summary>عدد الأيام غير المغطّاة بالرصيد = max(0, المطلوب − المتبقّي). يُملأ فقط عند نقص الرصيد.</summary>
    public decimal? UncoveredLeaveDays { get; set; }

    /// <summary>علم إعلامي: الطلب قد يتضمّن أيامًا بدون راتب إن اعتُمد (لا خصم آلي — لمساعدة HR/Payroll لاحقًا).</summary>
    public bool IsPotentialUnpaidLeave { get; set; }

    /// <summary>أقرّ الموظّف بأن الأيام غير المغطّاة قد تُحتسب إجازةً بدون راتب وتُخصم من الراتب.</summary>
    public bool EmployeeAcknowledgedUnpaidDeduction { get; set; }

    /// <summary>وقت إقرار الموظّف (UTC).</summary>
    public DateTime? EmployeeAcknowledgedAtUtc { get; set; }

    /// <summary>
    /// قرار الموظّف عند تجاوز الاستئذان رصيدَ الأذونات الشهري (V1.1.1). None للإجازات وللأذونات ضمن الرصيد.
    /// CompensateAfterHours: تعهّد بتعويض الوقت بعد الدوام. AdminOrPayrollReview: تقديمٌ مع علمٍ باحتمال المعالجة الإدارية/المالية.
    /// لا خصم آلي — لقطة إعلامية لمساعدة HR/Payroll لاحقًا.
    /// </summary>
    public PermissionShortfallResolution PermissionShortfallResolution { get; set; } = PermissionShortfallResolution.None;
}

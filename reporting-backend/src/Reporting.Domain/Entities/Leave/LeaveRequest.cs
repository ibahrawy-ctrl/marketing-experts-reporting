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
}

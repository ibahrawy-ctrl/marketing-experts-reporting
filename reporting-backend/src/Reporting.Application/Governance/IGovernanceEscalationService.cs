using Reporting.Application.Common;

namespace Reporting.Application.Governance;

/// <summary>
/// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1): رفع/متابعة/إسناد/إغلاق تصعيدات فردية بخطّ زمنيّ قابل للتتبّع.
/// كيان مستقلّ تمامًا عن بنود الحوكمة العامة ولا يمسّ سير اعتماد التقارير. الرؤية والصلاحيات تُفرَض داخل الخدمة
/// (رؤية واسعة/نطاق/HR/موظف؛ القراءة غير المصرّح بها تُقنَّع كـ«غير موجود» 404 لا 403).
/// </summary>
public interface IGovernanceEscalationService
{
    Task<Result<IReadOnlyList<GovernanceEscalationListItemDto>>> ListAsync(GovernanceEscalationFilter filter, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> CreateAsync(CreateGovernanceEscalationRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> UpdateAsync(Guid id, UpdateGovernanceEscalationRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> ChangeStatusAsync(Guid id, ChangeGovernanceEscalationStatusRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> AssignAsync(Guid id, AssignGovernanceEscalationRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> AddCommentAsync(Guid id, AddGovernanceEscalationCommentRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> ReopenAsync(Guid id, ReopenGovernanceEscalationRequest request, CancellationToken ct = default);
    Task<Result<GovernanceEscalationDetailDto>> CloseAsync(Guid id, CloseGovernanceEscalationRequest request, CancellationToken ct = default);

    /// <summary>
    /// دليل أهداف التصعيد: قوائم آمنة على مستوى الشركة لاختيار الهدف (الرفع المتقاطع) دون فتح الدليل العام.
    /// الموظّفون = النشطون غير الحسّاسين فقط (تُستبعَد حسابات Admin/CEO/GM/CeoSupport)؛ الإدارات والفِرق كاملة.
    /// قراءة فقط ولا يكشف أيّ تصعيد — مجرّد مراجع لاختيار الهدف.
    /// </summary>
    Task<Result<EscalationTargetDirectoryDto>> GetTargetDirectoryAsync(CancellationToken ct = default);
}

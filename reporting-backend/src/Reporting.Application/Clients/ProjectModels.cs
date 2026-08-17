using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;

namespace Reporting.Application.Clients;

// ===== Project =====
/// <summary>
/// عقد قراءة المشروع. الحقول المضافة في P360-WF-R2 موضوعة **في الذيل بقيم افتراضيّة** عمدًا:
/// السجلّ موضعيّ (positional) ويُنشأ في مواضع كثيرة، فالإضافة في الوسط كانت ستكسرها جميعًا
/// بلا فائدة. الترتيب هنا عقد تسلسل لا ترتيب أهمّيّة.
/// </summary>
public record ProjectDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Name,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? OwnerTeamId,
    string? OwnerTeamName,
    Guid? AccountManagerId,
    string? AccountManagerName,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    bool CanHardDelete = false,
    string? DeleteBlockReason = null,

    // --- إسناد أدوار المشروع (P360-WF-R2 · GAP-01) ---
    Guid? ProjectOwnerId = null,
    string? ProjectOwnerName = null,
    Guid? TeamLeaderId = null,
    string? TeamLeaderName = null,

    // --- تقدّم المشروع المشتقّ من المخرَجات التعاقديّة (GAP-02/03/21) ---
    decimal ProgressPercent = 0m,
    ProjectProgressMode ProgressMode = ProjectProgressMode.NotCalculated,
    DateTime? ProgressCalculatedAtUtc = null,
    int ProgressSourceDeliverableCount = 0,

    // --- الصحّة الظاهرة (GAP-04/05) ---
    // **بلا أسباب هنا عمدًا**: الأسباب مشتقّة من مكوّنات الصحّة، واشتقاقها لكلّ صفّ في القائمة
    // يعني N+1 على استعلام قوائم يُستدعى في كلّ شاشة. مصدرها الوحيد يبقى
    // `GET /projects/{id}/overview` (`ProjectHealthDto.ReasonCodes`) — قناة واحدة لا قناتان.
    ProjectHealthStatus HealthStatus = ProjectHealthStatus.NotEvaluated,
    decimal HealthPercent = 0m,
    DateTime? HealthComputedAtUtc = null,

    // --- خريطة القدرات: الواجهة تخفي ما يرفضه الخادم بدل أن تكتشفه بعد الحفظ (§12) ---
    bool CanManageStructure = false,
    bool CanOperate = false);

public record CreateProjectRequest(
    Guid ClientId,
    string Name,
    ServiceType ServiceType,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    string? Notes = null,
    ProjectStatus Status = ProjectStatus.Active,
    Guid? ProjectOwnerId = null,
    Guid? TeamLeaderId = null);

public record UpdateProjectRequest(
    string Name,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    string? Notes = null,
    Guid? ProjectOwnerId = null,
    Guid? TeamLeaderId = null);

public record ProjectFilter(
    Guid? ClientId = null,
    ProjectStatus? Status = null,
    ServiceType? ServiceType = null,
    Guid? OwnerTeamId = null,
    Guid? AccountManagerId = null,
    bool IncludeClosed = false,
    // قائمة الاختيار (Multi-Project وغيره): مشاريع نشطة فقط تابعة لعميل غير مؤرشف ضمن النطاق.
    bool SelectableOnly = false);

// ملخّص المشروع (drill-down) — إحصاءات التقارير المرتبطة دون إعادة حساب KPI.
public record ProjectSummaryDto(
    ProjectDto Project,
    int TotalReports,
    int ClosedReports,
    int PendingReports,
    DateTime? LastReportAtUtc,
    int OpenRiskCount,
    int OpenNoteCount);

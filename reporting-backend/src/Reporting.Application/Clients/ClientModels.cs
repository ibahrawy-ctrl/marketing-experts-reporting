using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ===== Client =====
public record ClientDto(
    Guid Id,
    string Name,
    ClientStatus Status,
    Guid? AccountManagerId,
    string? AccountManagerName,
    string? MainContactName,
    string? MainContactInfo,
    string? Notes,
    int ProjectCount,
    int ActiveProjectCount,
    int AtRiskProjectCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    // Client 360 Foundation (CPW-R1B) — حقول موسَّعة اختيارية
    string? TradeNameEn = null,
    string? LegalName = null,
    string? ClientTypeCode = null,
    string? SectorCode = null,
    string? Country = null,
    string? City = null,
    string? Website = null,
    string? SourceCode = null,
    DateOnly? RelationshipStartDate = null,
    bool CanHardDelete = false,
    string? DeleteBlockReason = null);

public record CreateClientRequest(
    string Name,
    Guid? AccountManagerId = null,
    string? MainContactName = null,
    string? MainContactInfo = null,
    string? Notes = null,
    ClientStatus Status = ClientStatus.Active,
    // Client 360 Foundation (CPW-R1B)
    string? TradeNameEn = null,
    string? LegalName = null,
    string? ClientTypeCode = null,
    string? SectorCode = null,
    string? Country = null,
    string? City = null,
    string? Website = null,
    string? SourceCode = null,
    DateOnly? RelationshipStartDate = null);

public record UpdateClientRequest(
    string Name,
    ClientStatus Status,
    Guid? AccountManagerId = null,
    string? MainContactName = null,
    string? MainContactInfo = null,
    string? Notes = null,
    // Client 360 Foundation (CPW-R1B)
    string? TradeNameEn = null,
    string? LegalName = null,
    string? ClientTypeCode = null,
    string? SectorCode = null,
    string? Country = null,
    string? City = null,
    string? Website = null,
    string? SourceCode = null,
    DateOnly? RelationshipStartDate = null);

public record ClientFilter(
    ClientStatus? Status = null,
    Guid? AccountManagerId = null,
    bool IncludeClosed = false);

// ===== Linked report row (تقارير مرتبطة بعميل/مشروع) =====
// VIS-02ب: الصفّ كان يحمل تسعة حقول لا تكفي لاتّخاذ قرار من داخل مساحة المشروع —
// لا اسم قالب يميّز تقرير التصميم من تقرير السيو، ولا آخر تحديث، ولا عدد بنود العمل،
// ولا نتيجة آخر قرار اعتماد، ولا سبب الإرجاع. الحقول الستّة التالية اختياريّة الموضع
// حتّى تبقى المواضع التسعة الأولى متوافقة مع كلّ المُنشِئين القائمين.
public record LinkedReportRow(
    Guid SubmissionId,
    Guid SubmitterId,
    string? SubmitterName,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    Guid? ClientId,
    Guid? ProjectId,
    string? TemplateName = null,
    DateTime? LastUpdatedAtUtc = null,
    int WorkItemCount = 0,
    ApprovalStatus? LastDecision = null,
    DateTime? LastDecisionAtUtc = null,
    string? LastReturnReason = null);

// ===== Account Manager — Client Health =====
public record ClientHealthRow(
    Guid ClientId,
    string ClientName,
    ClientStatus Status,
    Guid? AccountManagerId,
    string? AccountManagerName,
    int ProjectCount,
    int AtRiskProjectCount,
    int OpenRiskCount,
    int OpenNoteCount,
    DateTime? LastReportAtUtc,
    string ChurnRisk,        // عالٍ/متوسط/منخفض — مشتقّ من الحالة والمؤشرات
    bool DecisionNeeded);    // يحتاج قرارًا (عميل/مشروع AtRisk أو مخاطر مفتوحة)

public record ClientHealthReport(
    string PeriodLabel,
    IReadOnlyList<ClientHealthRow> Rows,
    int TotalClients,
    int AtRiskClients,
    int DecisionNeededCount,
    int RenewalOpportunities,
    string ViewLevel,
    bool CanViewRows);

namespace Reporting.Application.Archive;

/// <summary>نوع العنصر المؤرشف (المحذوف إداريًّا ناعمًا) — تقرير مُسلَّم أو تقييم KPI.</summary>
public enum ArchiveItemType
{
    Report = 0,
    KpiEvaluation = 1
}

/// <summary>
/// حالة الاحتفاظ الزمنيّة للعنصر المؤرشف (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 11):
/// إعلاميّة بحتة لعرض قِدَم الحذف؛ لا حذف نهائيّ آليّ إطلاقًا.
/// </summary>
public enum RetentionStatus
{
    /// <summary>حُذف حديثًا (أقل من 30 يومًا).</summary>
    Fresh = 0,
    /// <summary>يستحقّ المراجعة (30–90 يومًا).</summary>
    ReviewDue = 1,
    /// <summary>محتفَظ به طويل الأمد (أكثر من 90 يومًا).</summary>
    LongTerm = 2
}

/// <summary>
/// استراتيجية الاسترجاع المحسوبة للتقرير (Hybrid — Phase 8):
/// إمّا استرجاع بمعتمِد تاريخيّ صالح، أو استرجاع بلا مسار نشط (يحتاج قرار إداريّ لاحق).
/// </summary>
public enum RestoreStrategy
{
    /// <summary>لا ينطبق (KPI أو غير قابل للاسترجاع).</summary>
    NotApplicable = 0,
    /// <summary>استرجاع المسار التاريخيّ مع تعيين المعتمِد التاريخيّ الصالح النشط.</summary>
    HistoricalApproverRestored = 1,
    /// <summary>استرجاع بلا معتمِد حاليّ (لا مسار نشط) — يظهر للإدارة لاتخاذ قرار توجيه منفصل.</summary>
    NoActiveApprover = 2
}

/// <summary>خطوة اعتماد ضمن لقطة سير العمل المعروضة في تفاصيل الأرشيف (قراءة فقط).</summary>
public record ArchiveWorkflowStepDto(
    int Level,
    Guid ApproverId,
    string? ApproverName,
    string Status,
    string? Comment,
    DateTime? DecidedAtUtc);

/// <summary>حدث تدقيقيّ ضمن الأثر المعروض في تفاصيل الأرشيف (قراءة فقط).</summary>
public record ArchiveAuditEntryDto(
    Guid Id,
    string Action,
    Guid? ActorId,
    string? ActorName,
    DateTime CreatedAtUtc,
    string? DataJson);

/// <summary>عنصر مؤرشف في قائمة الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 4).</summary>
public record ArchiveItemDto(
    Guid ArchiveItemId,
    ArchiveItemType ItemType,
    Guid EmployeeId,
    string EmployeeName,
    string TemplateName,
    string PeriodKey,
    string Status,
    DateTime DeletedAtUtc,
    Guid? DeletedByUserId,
    string? DeletedByName,
    string? DeletionReason,
    bool CanRestore,
    string? RestoreBlockedCode,
    string? RestoreBlockedReason,
    int DaysSinceDeletion,
    RetentionStatus RetentionStatus);

/// <summary>تفاصيل عنصر مؤرشف كاملة — تُقرأ صراحةً في شاشة الأرشيف (تتجاوز مرشّح الاستعلام العالميّ).</summary>
public record ArchiveDetailsDto(
    Guid ArchiveItemId,
    ArchiveItemType ItemType,
    Guid EmployeeId,
    string EmployeeName,
    string TemplateName,
    string PeriodKey,
    string Status,
    DateTime DeletedAtUtc,
    Guid? DeletedByUserId,
    string? DeletedByName,
    string? DeletionReason,
    bool CanRestore,
    string? RestoreBlockedCode,
    string? RestoreBlockedReason,
    int DaysSinceDeletion,
    RetentionStatus RetentionStatus,
    // تفاصيل إضافية
    Guid? CurrentApproverId,
    string? CurrentApproverName,
    IReadOnlyList<ArchiveWorkflowStepDto> WorkflowSteps,
    int FieldValuesCount,
    int KpiResultsCount,
    int ReviewEventsCount,
    IReadOnlyList<ArchiveAuditEntryDto> AuditTrail,
    Guid? HistoricalApproverId,
    string? HistoricalApproverName,
    bool? HistoricalApproverIsActive,
    RestoreStrategy RestoreStrategy,
    string? RestoreWarning);

/// <summary>مرشّح قائمة الأرشيف الإداريّ.</summary>
public record ArchiveFilter(
    ArchiveItemType? ItemType = null,
    string? PeriodKey = null,
    Guid? EmployeeId = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>نتيجة مصفّحة لقائمة الأرشيف.</summary>
public record ArchivePagedResult(
    IReadOnlyList<ArchiveItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>طلب استرجاع عنصر مؤرشف (السبب إلزاميّ 10–500 محرفًا، يُحفَظ في الأثر التدقيقيّ).</summary>
public record RestoreRequest(string? Reason);

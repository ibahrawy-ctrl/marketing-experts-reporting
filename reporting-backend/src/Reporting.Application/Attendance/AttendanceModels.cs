using System.Text.Json.Serialization;
using Reporting.Domain.Enums;

namespace Reporting.Application.Attendance;

// ═══════════════════════════════ عقود القراءة ═══════════════════════════════

/// <summary>نوع حادثة من الكتالوج المرجعيّ.</summary>
public sealed record AttendanceTypeDto(
    Guid Id,
    string Code,
    string NameAr,
    bool RequiresTimes,
    bool RequiresPolicyReference,
    bool AllowsMultiplePerDay,
    int Order);

/// <summary>
/// سطر واقعة في القائمة/الطابور.
/// <para><see cref="IsOfficialIncident"/> هو الفارق الدلاليّ بين **بلاغ** و**واقعة مؤكَّدة**،
/// وتعتمد عليه الواجهة في التمييز البصريّ بدل استنتاجه من اسم الحالة.</para>
/// </summary>
public sealed record AttendanceIncidentListItemDto(
    Guid Id,
    Guid SubjectUserId,
    string SubjectName,
    Guid IncidentTypeId,
    string TypeCode,
    string TypeNameAr,
    DateOnly IncidentDate,
    AttendanceIncidentStatus Status,
    string StatusAr,
    bool IsOfficialIncident,
    int? DurationMinutes,
    int AgeingDays,
    DateTime? SlaDueAtUtc,
    bool IsOverdue,
    DateTime? LastActionAtUtc,
    string? NextActorAr);

/// <summary>حدث واحد على الخطّ الزمنيّ — غير قابل للتعديل بعد كتابته.</summary>
public sealed record AttendanceEventDto(
    Guid Id,
    Guid ActorUserId,
    string ActorName,
    string Action,
    string FromStatus,
    string ToStatus,
    string? Comment,
    DateTime CreatedAtUtc);

/// <summary>وصف مرفق. <c>StoredPath</c> لا يُسرَّب إلى العميل إطلاقًا.</summary>
public sealed record AttendanceAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTime CreatedAtUtc);

/// <summary>
/// اقتراح مصالحة مع إجازة/استئذان معتمد يغطّي تاريخ الواقعة.
/// **اقتراح لا قرار**: لا يُغلِق الواقعة ولا يغيّر حالتها؛ القرار النهائيّ للموارد البشريّة.
/// سبب الإجازة مُستبعَد عمدًا (مصنَّف <c>HrOnly</c>) ولا يظهر هنا لأيّ متلقٍّ.
/// </summary>
public sealed record AttendanceReconciliationSuggestionDto(
    Guid LeaveRequestId,
    LeaveRequestType Type,
    string TypeAr,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string EvidenceLink);

/// <summary>
/// تفاصيل الواقعة بعد ترشيح الحسّاسيّة.
/// الحقول المرشَّحة (<see cref="HrNote"/> مثلًا) **تُحذف من التسلسل** عند انعدام التصريح
/// بدل إرسالها <c>null</c> — انعدام المفتاح هو الحماية لا القيمة الفارغة.
/// </summary>
public sealed record AttendanceIncidentDetailDto
{
    public required Guid Id { get; init; }
    public required Guid SubjectUserId { get; init; }
    public required string SubjectName { get; init; }
    public required Guid IncidentTypeId { get; init; }
    public required string TypeCode { get; init; }
    public required string TypeNameAr { get; init; }
    public required DateOnly IncidentDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? ReturnTime { get; init; }
    public int? DurationMinutes { get; init; }
    public required string Description { get; init; }
    public required string DetectionSource { get; init; }
    public required Guid ReportedByUserId { get; init; }
    public required string ReportedByName { get; init; }
    public required AttendanceIncidentStatus Status { get; init; }
    public required string StatusAr { get; init; }

    /// <summary>واقعة رسميّة مؤكَّدة — لا مجرّد بلاغ. يحسمه الخادم لا الواجهة.</summary>
    public required bool IsOfficialIncident { get; init; }

    public required int ConcurrencyStamp { get; init; }
    public DateTime? SlaDueAtUtc { get; init; }
    public required bool IsOverdue { get; init; }
    public required int AgeingDays { get; init; }
    public string? NextActorAr { get; init; }

    // ===== ردّ الموظّف — مشترَك معه بطبيعته =====

    /// <summary>نصّ ردّ الموظّف — يُحذف من الـJSON بلا تصريح <c>SharedWithEmployee</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmployeeResponse { get; init; }

    public DateTime? RespondedAtUtc { get; init; }

    // ===== قرار الموارد البشريّة =====
    public string? HrDecision { get; init; }

    /// <summary>ملاحظة الموارد البشريّة الداخليّة — تُحذف كلّيًّا بلا <c>Sensitivity.HrOnly.Read</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HrNote { get; init; }

    public Guid? ReviewedByUserId { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public Guid? ReconciledWithLeaveId { get; init; }
    public Guid? ReconciledWithPermissionId { get; init; }
    public Guid? DuplicateOfId { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public required DateTime CreatedAtUtc { get; init; }

    public required IReadOnlyList<AttendanceAttachmentDto> Attachments { get; init; }
    public required IReadOnlyList<AttendanceEventDto> Events { get; init; }

    /// <summary>
    /// عقد القدرات: ما يملك هذا المُشاهِد تشغيلَه **الآن** على هذه الواقعة.
    /// الواجهة تبني أزرارها منه ولا تستنتج الصلاحيّة محلّيًّا؛ والخادم يعيد التحقّق عند كلّ كتابة.
    /// </summary>
    public required IReadOnlyList<string> AllowedActions { get; init; }

    /// <summary>اقتراحات المصالحة — تظهر لمن يملك مراجعة فقط، وتغيب من الـJSON لغيره.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AttendanceReconciliationSuggestionDto>? ReconciliationSuggestions { get; init; }
}

/// <summary>صفحة نتائج مع عدّاد كلّيّ — العدّاد يساوي عدد الصفوف تحت نفس المرشِّح.</summary>
public sealed record AttendancePagedDto(
    IReadOnlyList<AttendanceIncidentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ═══════════════════════════════ عقود الكتابة ═══════════════════════════════

/// <summary>مرشِّح قائمة الوقائع. كلّ حقوله اختياريّة، والنطاق يُفرَض خادميًّا فوقها دائمًا.</summary>
public sealed class AttendanceListFilter
{
    public Guid? SubjectUserId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? IncidentTypeId { get; set; }
    public AttendanceIncidentStatus? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    /// <summary>الوقائع المتأخّرة عن مهلتها فقط.</summary>
    public bool OverdueOnly { get; set; }

    /// <summary>ما ينتظر إجراءً من المستخدم الحالي.</summary>
    public bool NeedsMyAction { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class CreateAttendanceIncidentRequest
{
    public Guid SubjectUserId { get; set; }
    public Guid IncidentTypeId { get; set; }
    public DateOnly IncidentDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? ReturnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? PolicyRefId { get; set; }

    /// <summary>إرسال فوريّ بدل حفظ مسودّة.</summary>
    public bool SubmitImmediately { get; set; }
}

public sealed class UpdateAttendanceDraftRequest
{
    public Guid IncidentTypeId { get; set; }
    public DateOnly IncidentDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? ReturnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? PolicyRefId { get; set; }
    public int ConcurrencyStamp { get; set; }
}

/// <summary>ردّ الموظّف: إقرار أو اعتراض. الاعتراض يستلزم رواية مكتوبة.</summary>
public sealed class EmployeeResponseRequest
{
    public string? Response { get; set; }
    public int ConcurrencyStamp { get; set; }
}

/// <summary>قرار الموارد البشريّة الموحّد.</summary>
public sealed class HrReviewRequest
{
    public AttendanceHrDecision Decision { get; set; }
    public string? Note { get; set; }

    /// <summary>مطلوب مع <see cref="AttendanceHrDecision.Reconcile"/>.</summary>
    public Guid? ReconcileWithLeaveRequestId { get; set; }

    // ===== حقول التصحيح (مع Decision = Correct فقط) =====
    public DateOnly? CorrectedIncidentDate { get; set; }
    public TimeOnly? CorrectedStartTime { get; set; }
    public TimeOnly? CorrectedReturnTime { get; set; }
    public Guid? CorrectedIncidentTypeId { get; set; }
    public string? CorrectedDescription { get; set; }

    public int ConcurrencyStamp { get; set; }
}

/// <summary>سحب/تصعيد/إغلاق — جميعها تستلزم سببًا موثَّقًا.</summary>
public sealed class AttendanceReasonRequest
{
    public string Reason { get; set; } = string.Empty;
    public int ConcurrencyStamp { get; set; }
}

/// <summary>تدفّق تنزيل مرفق — يُبنى في الخدمة ولا يُسلسَل إلى JSON.</summary>
public sealed record AttendanceFileDownload(Stream Content, string ContentType, string FileName);

/// <summary>حصيلة كنس SLA — إجراء نظام لا إجراء مستخدم.</summary>
public sealed record AttendanceSlaSweepResult(int NotifiedEmployees, int TimedOut, int SentToHr);

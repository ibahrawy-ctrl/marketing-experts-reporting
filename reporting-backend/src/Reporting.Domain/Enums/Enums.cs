namespace Reporting.Domain.Enums;

/// <summary>حالة قالب التقرير/الـKPI (مسودة/منشور/مؤرشف).</summary>
public enum TemplateStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>أنواع حقول بانِي النموذج (20 نوعًا).</summary>
public enum FieldType
{
    ShortText = 0,
    LongText = 1,
    RichText = 2,
    Number = 3,
    Decimal = 4,
    Currency = 5,
    Percentage = 6,
    Date = 7,
    DateTime = 8,
    Time = 9,
    Boolean = 10,
    SingleSelect = 11,
    MultiSelect = 12,
    Rating = 13,
    Scale = 14,
    FileUpload = 15,
    Image = 16,
    Url = 17,
    Email = 18,
    Phone = 19,
    TableGrid = 20,
    SectionHeader = 21
}

/// <summary>
/// تصنيف قالب التقرير من حيث الإلزام (UAT Phase 3 — البند 9):
/// • Primary = التقرير الأساسي المطلوب للدور (إلزامي).
/// • Supplementary = تقرير/استبيان تكميلي (اختياري، مثل متابعة أو نبض عام)
///   لمنع ظهور تقريرَين أسبوعيَّين إلزاميَّين لنفس الموظّف.
/// </summary>
public enum TemplateClassification
{
    Primary = 0,
    Supplementary = 1
}

/// <summary>نوع الفترة الزمنية للتقرير/التقييم.</summary>
public enum PeriodType
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4,
    AdHoc = 5
}

/// <summary>حالات دورة حياة التسليم الثماني (BR — Submission lifecycle).</summary>
public enum SubmissionStatus
{
    Draft = 0,
    Submitted = 1,
    Returned = 2,
    ApprovedByDirectManager = 3,
    ApprovedByNextLevel = 4,
    Escalated = 5,
    Closed = 6,
    Visible = 7
}

/// <summary>حالة خطوة الاعتماد ضمن سير العمل.</summary>
public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Returned = 2,
    Escalated = 3
}

/// <summary>دورية مؤشرات الأداء.</summary>
public enum KpiCadence
{
    WeeklyPulse = 0,
    Quarterly = 1
}

/// <summary>طريقة احتساب المؤشر.</summary>
public enum KpiCalcMethod
{
    Manual = 0,
    Auto = 1,
    Hybrid = 2
}

/// <summary>حالة تقييم الـKPI.</summary>
public enum KpiEvaluationStatus
{
    Draft = 0,
    InProgress = 1,
    Submitted = 2,
    Approved = 3,
    Closed = 4
}

/// <summary>اتجاه المؤشر مقارنة بالفترة السابقة.</summary>
public enum KpiTrend
{
    Unknown = 0,
    Up = 1,
    Flat = 2,
    Down = 3
}

/// <summary>حالة الاحتياج التدريبي.</summary>
public enum TrainingNeedStatus
{
    Open = 0,
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>حالة خطة التحسين.</summary>
public enum ImprovementPlanStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

/// <summary>شدة المخاطرة.</summary>
public enum RiskSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>حالة المخاطرة.</summary>
public enum RiskStatus
{
    Open = 0,
    Mitigating = 1,
    Monitoring = 2,
    Closed = 3
}

/// <summary>حالة التصعيد.</summary>
public enum EscalationStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
    Dismissed = 3
}

/// <summary>حالة القرار.</summary>
public enum DecisionStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
    Implemented = 3
}

/// <summary>الكيان الذي ترتبط به الملاحظة الإدارية (السياق الذي تظهر فيه).</summary>
public enum ManagementNoteEntityType
{
    ReportSubmission = 0,
    User = 1,
    KpiEvaluation = 2,
    Team = 3,
    Escalation = 4,
    Decision = 5,
    Risk = 6,
    Client = 7,
    Project = 8
}

/// <summary>حالة العميل/الحساب (Phase 6 — بُعد العميل والمشروع).</summary>
public enum ClientStatus
{
    Active = 0,
    Paused = 1,
    AtRisk = 2,
    Closed = 3
}

/// <summary>حالة المشروع (Phase 6 — بُعد العميل والمشروع).</summary>
public enum ProjectStatus
{
    Active = 0,
    Paused = 1,
    Completed = 2,
    AtRisk = 3,
    Closed = 4
}

/// <summary>نوع الخدمة المقدَّمة في المشروع (Phase 6).</summary>
public enum ServiceType
{
    Social = 0,
    Seo = 1,
    MediaBuying = 2,
    Website = 3,
    Video = 4,
    Branding = 5,
    Other = 6
}

/// <summary>نوع الملاحظة الإدارية.</summary>
public enum ManagementNoteType
{
    Documentation = 0,
    Guidance = 1,
    Warning = 2,
    FollowUp = 3
}

/// <summary>حالة الملاحظة الإدارية (تُستخدم عندما تتطلّب إجراءً).</summary>
public enum ManagementNoteStatus
{
    Open = 0,
    Resolved = 1
}

/// <summary>نوع طلب الإجازة/الاستئذان (V1.0.1 — رقعة الإجازات والاستئذانات).</summary>
public enum LeaveRequestType
{
    /// <summary>إجازة: يوم كامل أو عدّة أيام (StartDate → EndDate).</summary>
    Leave = 0,
    /// <summary>استئذان: يوم واحد، من وقت إلى وقت (StartTime → EndTime).</summary>
    Permission = 1
}

/// <summary>
/// حالات دورة حياة طلب الإجازة/الاستئذان. لا يُصبح الطلب رسميًّا ويؤثّر في التقارير إلا عند <see cref="HrApproved"/>.
/// </summary>
public enum LeaveRequestStatus
{
    Draft = 0,
    Submitted = 1,
    TeamLeaderApproved = 2,
    TeamLeaderRejected = 3,
    ManagerApproved = 4,
    ManagerRejected = 5,
    HrApproved = 6,
    HrRejected = 7,
    ReturnedForEdit = 8,
    Cancelled = 9
}

/// <summary>الخطوة الحالية في سلسلة الاعتماد الهرمية لطلب الإجازة/الاستئذان.</summary>
public enum LeaveRequestStep
{
    /// <summary>لدى الموظّف (مسودّة أو مُعاد للتعديل).</summary>
    Employee = 0,
    TeamLeader = 1,
    Manager = 2,
    Hr = 3,
    /// <summary>انتهى مسار الطلب (اعتماد نهائي أو رفض أو إلغاء).</summary>
    Completed = 4
}

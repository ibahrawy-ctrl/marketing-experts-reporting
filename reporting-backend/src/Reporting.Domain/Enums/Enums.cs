namespace Reporting.Domain.Enums;

/// <summary>حالة قالب التقرير/الـKPI (مسودة/منشور/مؤرشف).</summary>
public enum TemplateStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>حالة رسالة صندوق صادر البريد (قيد الانتظار/مُرسَلة/فاشلة نهائيًا).</summary>
public enum EmailOutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

/// <summary>
/// حالة إشعار البريد (EMAIL-NOTIFICATIONS-R1) — مستقلّ تمامًا عن صندوق الصادر القديم.
/// Pending=بانتظار الإرسال، DryRun=أُنشئ في وضع المعاينة بلا إرسال، Sent=أُرسل فعليًا،
/// Failed=فشل الإرسال، Skipped=تخطّي (لا مستلم صالح)، Cancelled=أُلغي.
/// </summary>
public enum EmailNotificationStatus
{
    Pending = 0,
    DryRun = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4,
    Cancelled = 5
}

/// <summary>
/// وضع نظام إشعارات البريد المستقلّ (EMAIL-NOTIFICATIONS-R1).
/// Disabled=لا صفّ ولا إرسال، DryRun=إنشاء صفّ معاينة بلا SMTP (الافتراضي)، Enabled=إرسال فعليّ (محروس، غير مُفعَّل في R1 بلا موافقة).
/// </summary>
public enum EmailNotificationMode
{
    Disabled = 0,
    DryRun = 1,
    Enabled = 2
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
    SectionHeader = 21,
    /// <summary>قسم مشاريع متكرر: يُعبّأ مرة لكل مشروع ضمن نطاق المستخدم. الإعداد في ConfigJson، والقيمة في ValueJson كقائمة {projectId, answers}.</summary>
    ProjectRepeatableSection = 22
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

/// <summary>
/// نطاق إسناد قالب التقرير (Template Assignment) — المستوى الذي يُربَط به القالب بالموظّفين.
/// الأخصّية تنازليًّا: Employee (موظّف بعينه) ⟶ JobRole (مسمّى وظيفي) ⟶ Team (فريق) ⟶ Department (إدارة).
/// </summary>
public enum TemplateAssignmentScope
{
    Employee = 0,
    JobRole = 1,
    Team = 2,
    Department = 3
}

/// <summary>
/// نوع إسناد قالب التقرير: Include = إسناد صريح (يُظهِر القالب)، Exclude = استثناء صريح (يُخفيه).
/// الاستثناء يتفوّق على الإسناد في نفس المستوى أو أيّ مستوى أدنى.
/// </summary>
public enum TemplateAssignmentKind
{
    Include = 0,
    Exclude = 1
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

/// <summary>تصنيف بند الحوكمة (ورشة الحوكمة العامة — GOV-GOVERNANCE-UX1).</summary>
public enum GovernanceCategory
{
    Observation = 0,
    Risk = 1,
    Decision = 2,
    Recommendation = 3,
    FollowUp = 4,
    Compliance = 5,
    Performance = 6,
    OperationalIssue = 7
}

/// <summary>شدة بند الحوكمة.</summary>
public enum GovernanceSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>حالة بند الحوكمة في ورشة الحوكمة.</summary>
public enum GovernanceItemStatus
{
    Open = 0,
    InReview = 1,
    Waiting = 2,
    Resolved = 3,
    Closed = 4,
    Cancelled = 5
}

/// <summary>نوع حركة الخط الزمني لبند الحوكمة (تعليق/تغيير حالة/إعادة إسناد/متابعة...).</summary>
public enum GovernanceItemUpdateType
{
    Created = 0,
    Comment = 1,
    StatusChanged = 2,
    Reassigned = 3,
    FollowUp = 4,
    Edited = 5
}

/// <summary>
/// نطاق تطبيق بند الحوكمة (GOV-APPLICATION-SCOPE-R1): «يُطبَّق على من؟» — مستقلّ عن «المُسنَد إليه» (مسؤول المتابعة).
/// Company = كل الشركة (لأصحاب الرؤية الواسعة فقط)؛ Department = إدارة محدّدة؛ Team = فريق محدّد؛
/// User = موظّف محدّد؛ RelatedReport = تقرير مرتبط.
/// </summary>
public enum GovernanceApplicationScope
{
    Company = 0,
    Department = 1,
    Team = 2,
    User = 3,
    RelatedReport = 4
}

// ===== التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ عن بنود الحوكمة العامة =====

/// <summary>نوع/سبب التصعيد الفردي (أداء/تأخير/جودة/التزام/تواصل/سير عمل/أثر على العميل/مخالفة سياسة/أخرى).</summary>
public enum EscalationType
{
    Performance = 0,
    Delay = 1,
    Quality = 2,
    Compliance = 3,
    Communication = 4,
    Workflow = 5,
    ClientImpact = 6,
    PolicyViolation = 7,
    Other = 8
}

/// <summary>شدّة التصعيد الفردي.</summary>
public enum EscalationSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>حالة التصعيد الفردي في دورة حياته.</summary>
public enum GovernanceEscalationStatus
{
    Open = 0,
    UnderReview = 1,
    Assigned = 2,
    WaitingForResponse = 3,
    Resolved = 4,
    Closed = 5,
    Reopened = 6,
    Cancelled = 7
}

/// <summary>نوع الهدف الذي يخصّه التصعيد (موظّف/إدارة/فريق/تقرير/سير عمل/بند حوكمة/تشغيلي/أخرى).</summary>
public enum EscalationTargetType
{
    User = 0,
    Department = 1,
    Team = 2,
    Report = 3,
    Workflow = 4,
    GovernanceItem = 5,
    Operational = 6,
    Other = 7
}

/// <summary>نوع حركة الخط الزمني للتصعيد الفردي (إنشاء/تعليق/تغيير حالة/إسناد/إعادة فتح/تعديل/إغلاق).</summary>
public enum EscalationUpdateType
{
    Created = 0,
    Comment = 1,
    StatusChanged = 2,
    Assigned = 3,
    Reopened = 4,
    Edited = 5,
    Closed = 6
}

// ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ يحوّل أي ملاحظة/تصعيد إلى إجراء متابَع =====

/// <summary>مصدر إجراء الحوكمة (تصعيد فردي/بند حوكمة عام/إجراء يدوي مباشر).</summary>
public enum ActionItemSourceType
{
    Manual = 0,
    Escalation = 1,
    GovernanceItem = 2
}

/// <summary>أولوية إجراء الحوكمة.</summary>
public enum ActionItemPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>حالة إجراء الحوكمة في دورة حياته. «متأخر» ليست حالة مخزَّنة بل تُحسَب (غير مكتمل/ملغى وتاريخ الاستحقاق مضى).</summary>
public enum ActionItemStatus
{
    Open = 0,
    InProgress = 1,
    Blocked = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>نوع حركة الخط الزمني لإجراء الحوكمة.</summary>
public enum ActionItemUpdateType
{
    Created = 0,
    Comment = 1,
    StatusChanged = 2,
    DueDateChanged = 3,
    AssigneeChanged = 4,
    CompletionSubmitted = 5,
    Reopened = 6,
    Cancelled = 7
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

// ===== خدمات الموظف: الأرصدة + طلبات HR العامة (V1.1 — Employee Self-Service) =====

/// <summary>نوع رصيد الموظف الذي تُحتسب عليه الحركات.</summary>
public enum BalanceType
{
    /// <summary>رصيد الإجازة السنوية (يُخصم بالأيام).</summary>
    AnnualLeave = 0,
    /// <summary>رصيد الأذونات (يُخصم بوحدة السياسة: عدد أو ساعات).</summary>
    Permission = 1
}

/// <summary>اتجاه حركة الرصيد. الرصيد = مجموع الإضافات − مجموع الخصومات.</summary>
public enum BalanceDirection
{
    /// <summary>إضافة للرصيد (رصيد افتتاحي/ترحيل/تعديل موجب/عكس خصم).</summary>
    Credit = 0,
    /// <summary>خصم من الرصيد (إجازة/إذن معتمد/تعديل سالب).</summary>
    Debit = 1
}

/// <summary>مصدر حركة الرصيد — يوثّق سبب الحركة ويُستخدم في منع الازدواج.</summary>
public enum BalanceSource
{
    /// <summary>رصيد افتتاحي يدوي لسنة محددة.</summary>
    OpeningBalance = 0,
    /// <summary>خصم آلي عند اعتماد إجازة نهائيًّا (HrApproved).</summary>
    ApprovedLeave = 1,
    /// <summary>خصم آلي عند اعتماد إذن نهائيًّا (HrApproved).</summary>
    ApprovedPermission = 2,
    /// <summary>تعديل يدوي من الإدارة/HR (بسبب إلزامي + Audit).</summary>
    ManualAdjustment = 3,
    /// <summary>ترحيل رصيد بين السنوات (يدوي في MVP).</summary>
    CarryOver = 4,
    /// <summary>عكس خصم سابق عند إلغاء/إبطال طلب معتمد (إضافة معاكسة، لا حذف).</summary>
    Reversal = 5
}

/// <summary>وحدة احتساب الأذونات في السياسة.</summary>
public enum PermissionUnit
{
    /// <summary>كل إذن = 1 (الافتراضي — أوضح في كروت الموظف).</summary>
    Count = 0,
    /// <summary>الإذن يُحتسب بعدد ساعاته (EndTime − StartTime).</summary>
    Hours = 1
}

/// <summary>
/// قرار الموظّف عند تجاوز طلب الاستئذان رصيدَ الأذونات الشهري المتاح (V1.1.1 — حارس رصيد الأذونات الشهري).
/// لا يُحتسب أي خصم آلي على الراتب؛ القرار لقطة إعلامية لتمكين الموارد البشرية/الرواتب لاحقًا (FIN-L1).
/// </summary>
public enum PermissionShortfallResolution
{
    /// <summary>لا تجاوز ولا حاجة لقرار (الرصيد الشهري كافٍ أو لا توجد سياسة حدّ شهري).</summary>
    None = 0,
    /// <summary>يتعهّد الموظّف بتعويض وقت الاستئذان بعد مواعيد العمل.</summary>
    CompensateAfterHours = 1,
    /// <summary>يتقدّم بالطلب مع علمه باحتمال المعالجة الإدارية/المالية حسب قرار الإدارة.</summary>
    AdminOrPayrollReview = 2
}

/// <summary>نوع طلب الموارد البشرية العام (غير الإجازة/الإذن).</summary>
public enum EmployeeServiceRequestType
{
    HrLetter = 0,
    SalaryCertificate = 1,
    ExperienceCertificate = 2,
    BankLetter = 3,
    EmbassyLetter = 4,
    PersonalDataUpdate = 5,
    Other = 6
}

/// <summary>اللغة المفضّلة للخطاب/الشهادة الناتجة.</summary>
public enum PreferredLanguage
{
    Arabic = 0,
    English = 1
}

/// <summary>دورة حياة طلب الموارد البشرية العام.</summary>
public enum EmployeeServiceRequestStatus
{
    Submitted = 0,
    InReview = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// نوع نطاق منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1 — رؤية للقراءة فقط):
/// • User = منح رؤية تقارير مستخدم بعينه (TargetUserId).
/// • Team = منح رؤية تقارير أعضاء فريق (TargetTeamId).
/// المنح لا يجعل المستفيد عضوًا في الفريق ولا يمنحه أيّ قدرة اعتماد/تعديل، ولا يدخل ScopeResolver/KPI/المشاريع.
/// </summary>
public enum ReportViewGrantScopeKind
{
    User = 0,
    Team = 1
}

/// <summary>
/// نوع نطاق المنصب المرن (Phase 1A — رؤية فقط). يحدّد كيف يُترجَم نطاق المنصب إلى مجموعة مستخدمين:
/// Department/Team عبر مرجع كيان، SpecificUsers عبر معرّف مستخدم واحد لكل سطر، AllCompany = كل الشركة.
/// </summary>
public enum PositionScopeKind
{
    Department = 0,
    Team = 1,
    SpecificUsers = 2,
    AllCompany = 3
}

/// <summary>
/// حالة المراجعة المالية لطلب إجازة/استئذان مؤثّر على الراتب (FIN-L1 — عرض التأثير على الرواتب).
/// مراجعة إعلامية بحتة: لا تغيّر حالة الطلب الأصلي ولا تُجري أيّ خصم آليّ. الطلب بلا صفّ مراجعة = Pending ضمنيًّا.
/// </summary>
public enum PayrollImpactReviewStatus
{
    /// <summary>لم تُراجَع ماليًّا بعد (الحالة الافتراضية الضمنية لأيّ طلب مؤثّر بلا صفّ مراجعة).</summary>
    Pending = 0,
    /// <summary>عولِجت ماليًّا (احتُسبت/طُبِّقت في الرواتب خارج النظام).</summary>
    Processed = 1,
    /// <summary>تم تجاهلها ماليًّا (لا أثر على الراتب بقرار المالية).</summary>
    Ignored = 2,
    /// <summary>تحتاج مراجعة إضافية قبل المعالجة المالية.</summary>
    NeedsReview = 3
}

/// <summary>
/// تصنيف نوع التأثير على الراتب لطلب مؤثّر (FIN-L1 — مشتقّ خادميًّا من لقطة الطلب، لا يُخزَّن).
/// يُستخدم للفلترة والعرض فقط. لا يُجري أيّ خصم آليّ.
/// </summary>
public enum PayrollImpactType
{
    /// <summary>إجازة قد تتضمّن أيامًا بدون راتب (أيام غير مغطّاة بالرصيد).</summary>
    UnpaidLeave = 0,
    /// <summary>استئذان مع تعهّد الموظّف بتعويض الوقت بعد مواعيد العمل.</summary>
    PermissionAfterHoursCompensation = 1,
    /// <summary>استئذان مع علم الموظّف باحتمال المعالجة الإدارية/المالية.</summary>
    PermissionAdminOrPayrollReview = 2
}

/// <summary>
/// نوع تأخّر موعد التقرير الأسبوعي (RPT-DUE1 — محسوب عند الطلب، غير مخزَّن).
/// يصف الإجراء المتأخّر المتوقَّع حسب الدور ضمن الأسبوع التشغيلي (خميس→أربعاء، توقيت الرياض).
/// </summary>
public enum DelayType
{
    /// <summary>لا تأخّر.</summary>
    NoDelay = 0,
    /// <summary>الموظّف لم يُسلّم تقريره الأسبوعي بعد الموعد (الأربعاء).</summary>
    EmployeeReportNotSubmitted = 1,
    /// <summary>قائد الفريق متأخّر في مراجعة تقارير فريقه (بعد الخميس).</summary>
    TeamLeaderReviewOverdue = 2,
    /// <summary>المدير متأخّر في مراجعة/تصعيد التقارير (بعد الأحد).</summary>
    ManagerReviewOverdue = 3,
    /// <summary>المراجعة التنفيذية (GM/CEO) قيد الانتظار (موعدها الاثنين).</summary>
    ExecutiveReviewPending = 4
}

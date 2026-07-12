// تسميات عربية للتعدادات + أدوات تنسيق.
import type {
  Role,
  SubmissionStatus,
  PeriodType,
  KpiTrend,
  RiskSeverity,
  RiskStatus,
  EscalationStatus,
  DecisionStatus,
  TrainingNeedStatus,
  ImprovementPlanStatus,
  KpiEvaluationStatus,
  ApprovalStatus,
  FieldType,
  KpiCalcMethod,
  TemplateStatus,
  KpiCadence,
  TemplateClassification,
  ManagementNoteType,
  ManagementNoteStatus,
  ManagementNoteEntityType,
  GovernanceCategory,
  GovernanceSeverity,
  GovernanceItemStatus,
  GovernanceItemUpdateType,
  GovernanceApplicationScope,
  EscalationType,
  EscalationSeverity,
  GovernanceEscalationStatus,
  EscalationTargetType,
  EscalationUpdateType,
  ClientStatus,
  ProjectStatus,
  ServiceType,
  WorkstreamStatus,
  DeliverablePriority,
  LeaveRequestType,
  LeaveRequestStatus,
  PermissionShortfallResolution,
  BalanceType,
  BalanceDirection,
  BalanceSource,
  PermissionUnit,
  EmployeeServiceRequestType,
  EmployeeServiceRequestStatus,
  PreferredLanguage,
  PayrollImpactReviewStatus,
  PayrollImpactType,
  ActionItemStatus,
  ActionItemPriority,
  ActionItemSourceType,
  ActionItemUpdateType,
} from '../types/api';

export const roleLabel: Record<Role, string> = {
  Admin: 'مدير النظام',
  CEO: 'الرئيس التنفيذي',
  GeneralManager: 'المدير العام',
  Manager: 'مدير',
  TeamLeader: 'قائد فريق',
  Employee: 'موظف',
  CeoSupport: 'دعم الرئيس التنفيذي',
  HR: 'الموارد البشرية',
  Viewer: 'مُطّلع',
  FinanceManager: 'المدير المالي',
  Accountant: 'الحسابات',
  AccountPortfolioReader: 'محفظة مدير الحساب',
};

export const submissionStatusLabel: Record<SubmissionStatus, string> = {
  Draft: 'مسودة',
  Submitted: 'مُرسَل',
  Returned: 'مُعاد للتعديل',
  ApprovedByDirectManager: 'معتمد من المدير المباشر',
  ApprovedByNextLevel: 'معتمد من المستوى الأعلى',
  Escalated: 'مُصعّد',
  Closed: 'مُغلق',
  Visible: 'ظاهر',
};

export const periodTypeLabel: Record<PeriodType, string> = {
  Daily: 'يومي',
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
  Yearly: 'سنوي',
  AdHoc: 'حسب الحاجة',
};

export const kpiTrendLabel: Record<KpiTrend, string> = {
  Unknown: 'لم يُحتسب بعد',
  Up: 'صاعد',
  Flat: 'ثابت',
  Down: 'هابط',
};

// تسمية اتجاه KPI واعية بالسياق — بدل «غير معروف» الغامضة نوضّح السبب للمستخدم الإداري.
// hasScore=true يعني يوجد تقييم لكن لا توجد فترة سابقة للمقارنة (أول تقييم).
export function kpiTrendDisplay(trend: KpiTrend, hasScore?: boolean): string {
  if (trend !== 'Unknown') return kpiTrendLabel[trend];
  return hasScore ? 'أول تقييم — لا توجد فترة سابقة للمقارنة' : 'لم يُحتسب الاتجاه بعد';
}

export const riskSeverityLabel: Record<RiskSeverity, string> = {
  Low: 'منخفض',
  Medium: 'متوسط',
  High: 'مرتفع',
  Critical: 'حرج',
};

export const riskStatusLabel: Record<RiskStatus, string> = {
  Open: 'مفتوح',
  Mitigating: 'قيد التخفيف',
  Monitoring: 'قيد المراقبة',
  Closed: 'مُغلق',
};

export const escalationStatusLabel: Record<EscalationStatus, string> = {
  Open: 'مفتوح',
  Acknowledged: 'تم الإقرار',
  Resolved: 'تمت المعالجة',
  Dismissed: 'مرفوض',
};

export const decisionStatusLabel: Record<DecisionStatus, string> = {
  Proposed: 'مقترح',
  Approved: 'معتمد',
  Rejected: 'مرفوض',
  Implemented: 'مُنفّذ',
};

export const managementNoteTypeLabel: Record<ManagementNoteType, string> = {
  Documentation: 'توثيق',
  Guidance: 'توجيه',
  Warning: 'تنبيه',
  FollowUp: 'متابعة',
};

export const managementNoteStatusLabel: Record<ManagementNoteStatus, string> = {
  Open: 'مفتوحة',
  Resolved: 'تمّت معالجتها',
};

export const managementNoteEntityLabel: Record<ManagementNoteEntityType, string> = {
  ReportSubmission: 'تقرير',
  User: 'موظف',
  KpiEvaluation: 'تقييم KPI',
  Team: 'فريق',
  Escalation: 'تصعيد',
  Decision: 'قرار',
  Risk: 'مخاطرة',
};

export const trainingNeedStatusLabel: Record<TrainingNeedStatus, string> = {
  Open: 'مفتوح',
  Planned: 'مُخطّط',
  InProgress: 'قيد التنفيذ',
  Completed: 'مكتمل',
  Cancelled: 'مُلغى',
};

export const improvementPlanStatusLabel: Record<ImprovementPlanStatus, string> = {
  Open: 'مفتوح',
  InProgress: 'قيد التنفيذ',
  Completed: 'مكتمل',
  Cancelled: 'مُلغى',
};

export const kpiEvaluationStatusLabel: Record<KpiEvaluationStatus, string> = {
  Draft: 'مسودة',
  InProgress: 'قيد التقييم',
  Submitted: 'مُرسَل',
  Approved: 'معتمد',
  Closed: 'مُغلق',
};

export const approvalStatusLabel: Record<ApprovalStatus, string> = {
  Pending: 'بانتظار',
  Approved: 'معتمد',
  Returned: 'مُعاد',
  Escalated: 'مُصعّد',
};

export const fieldTypeLabel: Record<FieldType, string> = {
  ShortText: 'نص قصير',
  LongText: 'نص طويل',
  RichText: 'نص منسّق',
  Number: 'رقم',
  Decimal: 'رقم عشري',
  Currency: 'مبلغ مالي',
  Percentage: 'نسبة مئوية',
  Date: 'تاريخ',
  DateTime: 'تاريخ ووقت',
  Time: 'وقت',
  Boolean: 'نعم/لا',
  SingleSelect: 'اختيار واحد',
  MultiSelect: 'اختيار متعدد',
  Rating: 'تقييم نجوم',
  Scale: 'مقياس',
  FileUpload: 'رفع ملف',
  Image: 'صورة',
  Url: 'رابط',
  Email: 'بريد إلكتروني',
  Phone: 'هاتف',
  TableGrid: 'جدول',
  SectionHeader: 'عنوان قسم',
  ProjectRepeatableSection: 'قسم مشاريع متكرر',
};

export const kpiCalcMethodLabel: Record<KpiCalcMethod, string> = {
  Manual: 'يدوي',
  Auto: 'تلقائي',
  Hybrid: 'مختلط',
};

export const templateStatusLabel: Record<TemplateStatus, string> = {
  Draft: 'مسودة',
  Published: 'منشور',
  Archived: 'مؤرشف',
};

export const kpiCadenceLabel: Record<KpiCadence, string> = {
  WeeklyPulse: 'نبض أسبوعي',
  Quarterly: 'ربع سنوي',
};

// تصنيف القالب (Phase 4 §4) — لمنع ازدواج التقرير الأساسي:
// «أساسي» = تقرير الدور المطلوب الوحيد لكل فترة (يضم قسم النبض مضمَّنًا).
// «تكميلي» = قالب اختياري/استبيان نبض لا يُحتسب تقريرًا أساسيًا ثانيًا.
export const templateClassificationLabel: Record<TemplateClassification, string> = {
  Primary: 'تقرير أساسي مطلوب',
  Supplementary: 'قالب تكميلي/اختياري',
};

// ===== Phase 6: بُعد العميل/المشروع =====
export const clientStatusLabel: Record<ClientStatus, string> = {
  Active: 'نشِط',
  Paused: 'متوقّف مؤقتًا',
  AtRisk: 'معرَّض للخطر',
  Closed: 'مؤرشف',
};

export const projectStatusLabel: Record<ProjectStatus, string> = {
  Active: 'نشِط',
  Paused: 'متوقّف مؤقتًا',
  Completed: 'مكتمل',
  AtRisk: 'معرَّض للخطر',
  Closed: 'مؤرشف',
};

export const serviceTypeLabel: Record<ServiceType, string> = {
  Social: 'سوشيال ميديا',
  Seo: 'تحسين محرّكات البحث',
  MediaBuying: 'شراء إعلانات',
  Website: 'موقع إلكتروني',
  Video: 'فيديو',
  Branding: 'هوية وعلامة',
  Other: 'أخرى',
};

// ===== Client 360 (CPW-R1B) — رموز الملف الموسَّع (تطابق ClientCodeConstants بالخادم) =====
export const clientTypeLabel: Record<string, string> = {
  Company: 'شركة',
  Individual: 'فرد',
  Government: 'جهة حكومية',
  NonProfit: 'منظمة غير ربحية',
  Startup: 'شركة ناشئة',
  Agency: 'وكالة',
  Other: 'أخرى',
};
export const CLIENT_TYPE_CODES = Object.keys(clientTypeLabel);

export const sectorLabel: Record<string, string> = {
  Retail: 'تجزئة',
  Ecommerce: 'تجارة إلكترونية',
  Realestate: 'عقارات',
  Healthcare: 'رعاية صحية',
  Education: 'تعليم',
  Food: 'أغذية ومطاعم',
  Technology: 'تقنية',
  Finance: 'مالية',
  Travel: 'سفر وسياحة',
  Automotive: 'سيارات',
  Beauty: 'تجميل',
  Fashion: 'أزياء',
  Sports: 'رياضة',
  Entertainment: 'ترفيه',
  Industrial: 'صناعي',
  Services: 'خدمات',
  Other: 'أخرى',
};
export const SECTOR_CODES = Object.keys(sectorLabel);

export const clientSourceLabel: Record<string, string> = {
  Referral: 'ترشيح',
  Website: 'الموقع الإلكتروني',
  SocialMedia: 'وسائل التواصل',
  ColdOutreach: 'تواصل مباشر',
  Event: 'فعالية',
  Partner: 'شريك',
  Advertisement: 'إعلان',
  Existing: 'عميل حالي',
  Other: 'أخرى',
};
export const CLIENT_SOURCE_CODES = Object.keys(clientSourceLabel);

export const contactMethodLabel: Record<string, string> = {
  Email: 'البريد الإلكتروني',
  Phone: 'الهاتف',
  WhatsApp: 'واتساب',
  InPerson: 'مقابلة شخصية',
  Other: 'أخرى',
};
export const CONTACT_METHOD_CODES = Object.keys(contactMethodLabel);

export const platformLabel: Record<string, string> = {
  Facebook: 'فيسبوك',
  Instagram: 'إنستغرام',
  X: 'إكس (تويتر)',
  LinkedIn: 'لينكدإن',
  TikTok: 'تيك توك',
  Snapchat: 'سناب شات',
  YouTube: 'يوتيوب',
  GoogleAds: 'إعلانات جوجل',
  GoogleAnalytics: 'تحليلات جوجل',
  GoogleTagManager: 'مدير وسوم جوجل',
  MetaBusiness: 'ميتا بزنس',
  Website: 'الموقع الإلكتروني',
  WhatsApp: 'واتساب',
  Pinterest: 'بنترست',
  Other: 'أخرى',
};
export const PLATFORM_CODES = Object.keys(platformLabel);

export const accessStatusLabel: Record<string, string> = {
  FullAccess: 'وصول كامل',
  PartialAccess: 'وصول جزئي',
  Requested: 'مطلوب',
  Pending: 'قيد الانتظار',
  NoAccess: 'لا يوجد وصول',
  Revoked: 'مسحوب',
};
export const ACCESS_STATUS_CODES = Object.keys(accessStatusLabel);

// عرض رمز اختياري بالاسم العربي إن عُرِف، وإلّا الرمز نفسه أو شرطة.
export function codeLabel(map: Record<string, string>, code: string | null | undefined): string {
  if (!code) return '—';
  return map[code] ?? code;
}

// لون شارة حالة العميل/المشروع (يتوافق مع نغمات الهوية).
export function clientStatusTone(s: ClientStatus): 'success' | 'gold' | 'alert' | 'muted' {
  return s === 'Active' ? 'success' : s === 'Paused' ? 'gold' : s === 'AtRisk' ? 'alert' : 'muted';
}
export function projectStatusTone(s: ProjectStatus): 'success' | 'gold' | 'alert' | 'muted' | 'navy' {
  return s === 'Active' ? 'success' : s === 'Paused' ? 'gold' : s === 'AtRisk' ? 'alert' : s === 'Completed' ? 'navy' : 'muted';
}

// ===== P1: تيّارات العمل داخل المشروع =====
export const workstreamStatusLabel: Record<WorkstreamStatus, string> = {
  Active: 'نشِط',
  Paused: 'متوقّف مؤقتًا',
  Completed: 'مكتمل',
  Cancelled: 'ملغى',
};

export function workstreamStatusTone(s: WorkstreamStatus): 'success' | 'gold' | 'navy' | 'muted' {
  return s === 'Active' ? 'success' : s === 'Paused' ? 'gold' : s === 'Completed' ? 'navy' : 'muted';
}

// ===== P2: مخرَجات خطّة الإنتاج داخل تيار العمل =====
export const deliverablePriorityLabel: Record<DeliverablePriority, string> = {
  Low: 'منخفضة',
  Medium: 'متوسّطة',
  High: 'عالية',
  Urgent: 'عاجلة',
};

export function deliverablePriorityTone(p: DeliverablePriority): 'muted' | 'navy' | 'gold' | 'alert' {
  return p === 'Low' ? 'muted' : p === 'Medium' ? 'navy' : p === 'High' ? 'gold' : 'alert';
}

// ===== V1.0.1: الإجازات والاستئذانات =====
export const leaveTypeLabel: Record<LeaveRequestType, string> = {
  Leave: 'إجازة',
  Permission: 'استئذان',
};

// قرار الموظّف عند تجاوز الاستئذان رصيدَ الأذونات الشهري (V1.1.1).
export const permissionShortfallResolutionLabel: Record<PermissionShortfallResolution, string> = {
  None: '—',
  CompensateAfterHours: 'تعهّد بتعويض وقت الاستئذان بعد مواعيد العمل',
  AdminOrPayrollReview: 'تقديم الطلب مع العلم باحتمال المعالجة الإدارية/المالية',
};

export const leaveRequestStatusLabel: Record<LeaveRequestStatus, string> = {
  Draft: 'مسودة',
  Submitted: 'بانتظار قائد الفريق',
  TeamLeaderApproved: 'بانتظار المدير',
  TeamLeaderRejected: 'مرفوض من قائد الفريق',
  ManagerApproved: 'بانتظار الموارد البشرية',
  ManagerRejected: 'مرفوض من المدير',
  HrApproved: 'معتمد نهائيًّا',
  HrRejected: 'مرفوض من الموارد البشرية',
  ReturnedForEdit: 'مُعاد للتعديل',
  Cancelled: 'مُلغى',
};

// تسمية الحالة حسب سياق الطلب: طلب الموارد البشرية يسلك مسارًا خاصًّا (المدير العام ثم الإدارة العليا)،
// لذا تُترجَم الحالات الوسيطة TeamLeaderApproved/ManagerApproved إلى عبارات المسار الخاص بدل المسار العادي.
export function leaveStatusLabelFor(s: LeaveRequestStatus, isHrRequest: boolean): string {
  if (isHrRequest) {
    if (s === 'TeamLeaderApproved') return 'بانتظار مراجعة المدير العام';
    if (s === 'ManagerApproved') return 'بانتظار الاعتماد النهائي من الإدارة العليا';
  }
  return leaveRequestStatusLabel[s];
}

// لون شارة حالة الطلب — مطابق لمنطق التدفّق: أخضر للاعتماد النهائي، أحمر للرفض، ذهبي للمُعاد/الملغى.
export function leaveStatusTone(s: LeaveRequestStatus): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'HrApproved') return 'success';
  if (s === 'TeamLeaderRejected' || s === 'ManagerRejected' || s === 'HrRejected') return 'alert';
  if (s === 'ReturnedForEdit') return 'gold';
  if (s === 'Cancelled') return 'muted';
  return 'navy';
}

// ===== V1.1 — أرصدة الإجازات والأذونات + طلبات الموارد البشرية (خدمات الموظف) =====

export const balanceTypeLabel: Record<BalanceType, string> = {
  AnnualLeave: 'رصيد الإجازات',
  Permission: 'رصيد الأذونات',
};

export const balanceDirectionLabel: Record<BalanceDirection, string> = {
  Credit: 'إضافة',
  Debit: 'خصم',
};

export const balanceSourceLabel: Record<BalanceSource, string> = {
  OpeningBalance: 'رصيد افتتاحي',
  ApprovedLeave: 'إجازة معتمَدة',
  ApprovedPermission: 'إذن معتمَد',
  ManualAdjustment: 'تعديل يدوي',
  CarryOver: 'مُرحَّل',
  Reversal: 'عكس حركة',
};

export const permissionUnitLabel: Record<PermissionUnit, string> = {
  Count: 'عدد (إذن)',
  Hours: 'ساعات',
};

export const employeeServiceRequestTypeLabel: Record<EmployeeServiceRequestType, string> = {
  HrLetter: 'خطاب موارد بشرية',
  SalaryCertificate: 'شهادة راتب',
  ExperienceCertificate: 'شهادة خبرة',
  BankLetter: 'خطاب بنكي',
  EmbassyLetter: 'خطاب سفارة',
  PersonalDataUpdate: 'تحديث بيانات شخصية',
  Other: 'أخرى',
};

export const preferredLanguageLabel: Record<PreferredLanguage, string> = {
  Arabic: 'العربية',
  English: 'الإنجليزية',
};

export const employeeServiceRequestStatusLabel: Record<EmployeeServiceRequestStatus, string> = {
  Submitted: 'مُقدَّم',
  InReview: 'قيد المعالجة',
  Completed: 'مكتمل',
  Rejected: 'مرفوض',
  Cancelled: 'مُلغى',
};

export function employeeServiceRequestStatusTone(
  s: EmployeeServiceRequestStatus,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Completed') return 'success';
  if (s === 'Rejected') return 'alert';
  if (s === 'InReview') return 'gold';
  if (s === 'Cancelled') return 'muted';
  return 'navy';
}

// ===== FIN-L1 — عرض التأثير على الرواتب =====
export const payrollImpactReviewStatusLabel: Record<PayrollImpactReviewStatus, string> = {
  Pending: 'بانتظار المراجعة المالية',
  Processed: 'تمّت المعالجة',
  Ignored: 'مُتجاهَل (بلا أثر)',
  NeedsReview: 'يحتاج مراجعة',
};

export function payrollImpactReviewStatusTone(
  s: PayrollImpactReviewStatus,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Processed') return 'success';
  if (s === 'NeedsReview') return 'alert';
  if (s === 'Ignored') return 'muted';
  return 'gold';
}

export const payrollImpactTypeLabel: Record<PayrollImpactType, string> = {
  UnpaidLeave: 'إجازة بدون راتب (محتملة)',
  PermissionAfterHoursCompensation: 'استئذان بتعهّد تعويض بعد الدوام',
  PermissionAdminOrPayrollReview: 'استئذان بمعالجة إدارية/مالية',
};

// ===== ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) =====
export const governanceCategoryLabel: Record<GovernanceCategory, string> = {
  Observation: 'ملاحظة',
  Risk: 'خطر',
  Decision: 'قرار',
  Recommendation: 'توصية',
  FollowUp: 'متابعة',
  Compliance: 'التزام',
  Performance: 'أداء',
  OperationalIssue: 'مشكلة تشغيلية',
};

export const governanceSeverityLabel: Record<GovernanceSeverity, string> = {
  Low: 'منخفضة',
  Medium: 'متوسطة',
  High: 'عالية',
  Critical: 'حرجة',
};

export function governanceSeverityTone(
  s: GovernanceSeverity,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Critical') return 'alert';
  if (s === 'High') return 'gold';
  if (s === 'Low') return 'muted';
  return 'navy';
}

export const governanceItemStatusLabel: Record<GovernanceItemStatus, string> = {
  Open: 'مفتوح',
  InReview: 'قيد المراجعة',
  Waiting: 'بانتظار',
  Resolved: 'مُعالَج',
  Closed: 'مُغلق',
  Cancelled: 'مُلغى',
};

export function governanceItemStatusTone(
  s: GovernanceItemStatus,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Resolved' || s === 'Closed') return 'success';
  if (s === 'Waiting') return 'gold';
  if (s === 'Cancelled') return 'muted';
  if (s === 'InReview') return 'navy';
  return 'alert';
}

export const governanceItemUpdateTypeLabel: Record<GovernanceItemUpdateType, string> = {
  Created: 'إنشاء',
  Comment: 'تعليق',
  StatusChanged: 'تغيير حالة',
  Reassigned: 'إعادة إسناد',
  FollowUp: 'متابعة',
  Edited: 'تعديل',
};

// نطاق التطبيق لبند الحوكمة (GOV-APPLICATION-SCOPE-R1): «يُطبَّق على من؟»
export const governanceApplicationScopeLabel: Record<GovernanceApplicationScope, string> = {
  Company: 'كل الشركة',
  Department: 'إدارة محددة',
  Team: 'فريق محدد',
  User: 'موظّف محدد',
  RelatedReport: 'تقرير مرتبط',
};

// ===== التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) =====
export const escalationTypeLabel: Record<EscalationType, string> = {
  Performance: 'أداء',
  Delay: 'تأخير',
  Quality: 'جودة',
  Compliance: 'التزام',
  Communication: 'تواصل',
  Workflow: 'سير عمل',
  ClientImpact: 'أثر على العميل',
  PolicyViolation: 'مخالفة سياسة',
  Other: 'أخرى',
};

export const escalationSeverityLabel: Record<EscalationSeverity, string> = {
  Low: 'منخفضة',
  Medium: 'متوسطة',
  High: 'عالية',
  Critical: 'حرجة',
};

export function escalationSeverityTone(
  s: EscalationSeverity,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Critical') return 'alert';
  if (s === 'High') return 'gold';
  if (s === 'Low') return 'muted';
  return 'navy';
}

export const governanceEscalationStatusLabel: Record<GovernanceEscalationStatus, string> = {
  Open: 'مفتوح',
  UnderReview: 'قيد المراجعة',
  Assigned: 'مُسنَد',
  WaitingForResponse: 'بانتظار الرد',
  Resolved: 'مُعالَج',
  Closed: 'مُغلق',
  Reopened: 'أُعيد فتحه',
  Cancelled: 'مُلغى',
};

export function governanceEscalationStatusTone(
  s: GovernanceEscalationStatus,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Resolved' || s === 'Closed') return 'success';
  if (s === 'WaitingForResponse') return 'gold';
  if (s === 'Cancelled') return 'muted';
  if (s === 'UnderReview' || s === 'Assigned' || s === 'Reopened') return 'navy';
  return 'alert';
}

export const escalationTargetTypeLabel: Record<EscalationTargetType, string> = {
  User: 'موظّف',
  Department: 'إدارة',
  Team: 'فريق',
  Report: 'تقرير',
  Workflow: 'سير عمل',
  GovernanceItem: 'بند حوكمة',
  Operational: 'تشغيلي',
  Other: 'أخرى',
};

export const escalationUpdateTypeLabel: Record<EscalationUpdateType, string> = {
  Created: 'إنشاء',
  Comment: 'تعليق',
  StatusChanged: 'تغيير حالة',
  Assigned: 'إسناد',
  Reopened: 'إعادة فتح',
  Edited: 'تعديل',
  Closed: 'إغلاق',
};

// ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) =====

export const actionItemStatusLabel: Record<ActionItemStatus, string> = {
  Open: 'مفتوح',
  InProgress: 'قيد التنفيذ',
  Blocked: 'مُعطَّل',
  Completed: 'مكتمل',
  Cancelled: 'مُلغى',
};

export function actionItemStatusTone(
  s: ActionItemStatus,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (s === 'Completed') return 'success';
  if (s === 'Blocked') return 'alert';
  if (s === 'Cancelled') return 'muted';
  if (s === 'InProgress') return 'gold';
  return 'navy';
}

export const actionItemPriorityLabel: Record<ActionItemPriority, string> = {
  Low: 'منخفضة',
  Medium: 'متوسطة',
  High: 'عالية',
  Critical: 'حرجة',
};

export function actionItemPriorityTone(
  p: ActionItemPriority,
): 'navy' | 'success' | 'alert' | 'gold' | 'muted' {
  if (p === 'Critical') return 'alert';
  if (p === 'High') return 'gold';
  if (p === 'Low') return 'muted';
  return 'navy';
}

export const actionItemSourceTypeLabel: Record<ActionItemSourceType, string> = {
  Manual: 'يدوي',
  Escalation: 'تصعيد',
  GovernanceItem: 'بند حوكمة',
};

export const actionItemUpdateTypeLabel: Record<ActionItemUpdateType, string> = {
  Created: 'إنشاء',
  Comment: 'تعليق',
  StatusChanged: 'تغيير حالة',
  DueDateChanged: 'تغيير تاريخ الاستحقاق',
  AssigneeChanged: 'تغيير المُسنَد إليه',
  CompletionSubmitted: 'تقديم الإكمال',
  Reopened: 'إعادة فتح',
  Cancelled: 'إلغاء',
};

// ===== سجل التدقيق: ترجمة الأحداث التقنية إلى لغة أعمال مفهومة =====

// تسمية الكيان بالعربية (بدل ReportSubmission / KpiEvaluation …).
export const auditEntityLabel: Record<string, string> = {
  ReportSubmission: 'تقرير',
  KpiEvaluation: 'تقييم KPI',
  ReportTemplate: 'قالب تقرير',
  KpiTemplate: 'قالب KPI',
  Risk: 'مخاطرة',
  Escalation: 'تصعيد',
  Decision: 'قرار',
  TrainingNeed: 'احتياج تدريبي',
  ImprovementPlan: 'خطة تحسين',
  User: 'مستخدم',
  Team: 'فريق',
  Department: 'إدارة',
};

export function auditEntityName(entityType: string): string {
  return auditEntityLabel[entityType] ?? entityType;
}

// معنى الفعل (verb بعد النقطة في action مثل submission.approved) + أيقونة + لون.
type AuditVerbMeta = { label: string; icon: string; tone: 'navy' | 'success' | 'alert' | 'gold' | 'orange' | 'muted' };

const auditVerbMeta: Record<string, AuditVerbMeta> = {
  submitted: { label: 'إرسال', icon: '📤', tone: 'navy' },
  approved: { label: 'اعتماد', icon: '✅', tone: 'success' },
  returned: { label: 'إرجاع للتعديل', icon: '↩️', tone: 'gold' },
  escalated: { label: 'تصعيد', icon: '⤴️', tone: 'orange' },
  created: { label: 'إنشاء', icon: '➕', tone: 'navy' },
  updated: { label: 'تعديل', icon: '✏️', tone: 'navy' },
  deleted: { label: 'حذف', icon: '🗑️', tone: 'alert' },
  closed: { label: 'إغلاق', icon: '🔒', tone: 'muted' },
  resolved: { label: 'معالجة', icon: '✔️', tone: 'success' },
};

// الجملة الفعلية الماضية لكل فعل (لصياغة جملة أعمال مفهومة).
const auditPastPhrase: Record<string, string> = {
  submitted: 'تم إرسال',
  approved: 'تم اعتماد',
  returned: 'تمت إعادة',
  escalated: 'تم تصعيد',
  created: 'تم إنشاء',
  updated: 'تم تعديل',
  deleted: 'تم حذف',
  closed: 'تم إغلاق',
  resolved: 'تمت معالجة',
};

function auditVerb(action: string): string {
  const parts = action.split('.');
  return (parts[1] ?? parts[0] ?? '').toLowerCase();
}

export function auditActionMeta(action: string): AuditVerbMeta {
  return auditVerbMeta[auditVerb(action)] ?? { label: action, icon: '•', tone: 'muted' };
}

// جملة أعمال كاملة: «تم اعتماد تقرير بواسطة أحمد عبدالرؤوف».
export function auditSentence(log: { action: string; entityType: string; actorName: string | null }): string {
  const verb = auditVerb(log.action);
  const past = auditPastPhrase[verb];
  const entity = auditEntityName(log.entityType);
  const by = log.actorName ? ` بواسطة ${log.actorName}` : '';
  if (past) return `${past} ${entity}${by}`;
  // احتياط لأي حدث غير معروف: نعرض الفعل + الكيان بصيغة واضحة.
  const meta = auditActionMeta(log.action);
  return `${meta.label} — ${entity}${by}`;
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric' });
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function formatPercent(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return `${value.toLocaleString('ar-EG')}٪`;
}

// عرض مفتاح الفترة بصيغة واضحة: «2026-W25» ⇒ «الأسبوع 25 — 2026».
// أي قيمة لا تطابق صيغة الأسبوع المعتمدة تُعرض كما هي (بيانات قديمة/غير قياسية).
const WEEK_PERIOD = /^(\d{4})-W(\d{2})$/;
export function formatPeriod(periodKey: string | null | undefined): string {
  if (!periodKey) return '—';
  const m = WEEK_PERIOD.exec(periodKey.trim());
  if (!m) return periodKey;
  const year = m[1];
  const week = Number(m[2]);
  return `الأسبوع ${week} — ${year}`;
}

// ===== Phase 5: تجميع الفترات (شهر/ربع/سنة) =====
// مفاتيح الفترة: الشهر «2026-06»، الربع «2026-Q2»، السنة «2026». تُطابق ما يقبله الخادم.
export const granularityLabel: Record<string, string> = {
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
  Yearly: 'سنوي',
  Custom: 'مدى مخصّص',
};

export const monthNamesAr = [
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
];

// بناء مفتاح فترة شهرية «YYYY-MM» (الشهر 1..12).
export function monthKey(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`;
}
// بناء مفتاح فترة ربعية «YYYY-Qn» (الربع 1..4).
export function quarterKey(year: number, quarter: number): string {
  return `${year}-Q${quarter}`;
}

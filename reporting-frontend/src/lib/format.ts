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
  ClientStatus,
  ProjectStatus,
  ServiceType,
  LeaveRequestType,
  LeaveRequestStatus,
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
  Closed: 'مُغلق',
};

export const projectStatusLabel: Record<ProjectStatus, string> = {
  Active: 'نشِط',
  Paused: 'متوقّف مؤقتًا',
  Completed: 'مكتمل',
  AtRisk: 'معرَّض للخطر',
  Closed: 'مُغلق',
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

// لون شارة حالة العميل/المشروع (يتوافق مع نغمات الهوية).
export function clientStatusTone(s: ClientStatus): 'success' | 'gold' | 'alert' | 'muted' {
  return s === 'Active' ? 'success' : s === 'Paused' ? 'gold' : s === 'AtRisk' ? 'alert' : 'muted';
}
export function projectStatusTone(s: ProjectStatus): 'success' | 'gold' | 'alert' | 'muted' | 'navy' {
  return s === 'Active' ? 'success' : s === 'Paused' ? 'gold' : s === 'AtRisk' ? 'alert' : s === 'Completed' ? 'navy' : 'muted';
}

// ===== V1.0.1: الإجازات والاستئذانات =====
export const leaveTypeLabel: Record<LeaveRequestType, string> = {
  Leave: 'إجازة',
  Permission: 'استئذان',
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

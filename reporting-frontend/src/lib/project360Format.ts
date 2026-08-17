// ======================================================================
// Project 360 — التسميات العربيّة ونغمات العرض (CPW-R3 · R2-W12)
//
// ملفّ مستقلّ عن `format.ts` عمدًا: `format.ts` يحمل تعديلات سابقة غير منشورة،
// وإلحاق كتلة جديدة به كان سيخلط تغييرات هذه الحزمة بتغييرات لا علاقة لها بها.
// ما هو موجود هناك فعلًا (الأولويّة، المخاطر، القرارات، الملاحظات) **يُستورَد** منه
// ولا يُنسَخ هنا.
//
// **ترجمة فقط**: لا قاعدة عمل ولا اشتقاق. أسباب الصحّة تُترجَم من رموزها الرسميّة،
// والرمز غير المعروف يُعرَض كما هو بدل إخفائه — لأنّ إخفاءه يخفي معلومة أرسلها الخادم.
// ======================================================================

import { formatPercent } from './format';
import type {
  ExecutionProposalStatus,
  ProjectDeliverableStatus,
  ProjectHealthStatus,
  ProjectProgressMode,
  ProjectKpiCategory,
  ProjectKpiDirection,
  ProjectKpiFrequency,
  ProjectKpiSourceType,
  ProjectKpiTrend,
  ProjectKpiUnit,
  ProjectObjectiveStatus,
} from '../types/project360';

export const projectObjectiveStatusLabel: Record<ProjectObjectiveStatus, string> = {
  NotStarted: 'لم يبدأ',
  InProgress: 'قيد التنفيذ',
  AtRisk: 'معرّض للخطر',
  Completed: 'مكتمل',
  Cancelled: 'ملغى',
};

export function projectObjectiveStatusTone(
  s: ProjectObjectiveStatus,
): 'muted' | 'navy' | 'gold' | 'success' | 'alert' {
  if (s === 'Completed') return 'success';
  if (s === 'AtRisk') return 'alert';
  if (s === 'InProgress') return 'navy';
  if (s === 'Cancelled') return 'muted';
  return 'gold';
}

export const projectKpiCategoryLabel: Record<ProjectKpiCategory, string> = {
  Marketing: 'تسويق',
  Seo: 'تحسين محرّكات البحث',
  PaidAds: 'إعلانات مدفوعة',
  SocialMedia: 'تواصل اجتماعي',
  Sales: 'مبيعات',
  Brand: 'علامة تجاريّة',
  Content: 'محتوى',
  CustomerService: 'خدمة عملاء',
  Operations: 'تشغيل',
  Custom: 'مخصّص',
};

export const projectKpiUnitLabel: Record<ProjectKpiUnit, string> = {
  Percentage: 'نسبة مئويّة',
  Number: 'عدد',
  Currency: 'مبلغ',
  Duration: 'مدّة',
  Score: 'درجة',
  Custom: 'وحدة مخصّصة',
};

export const projectKpiFrequencyLabel: Record<ProjectKpiFrequency, string> = {
  Weekly: 'أسبوعيّ',
  Monthly: 'شهريّ',
  Quarterly: 'ربع سنويّ',
};

export const projectKpiDirectionLabel: Record<ProjectKpiDirection, string> = {
  HigherIsBetter: 'الأعلى أفضل',
  LowerIsBetter: 'الأدنى أفضل',
};

export const projectKpiTrendLabel: Record<ProjectKpiTrend, string> = {
  Unknown: 'غير محدّد',
  Up: 'صاعد',
  Flat: 'مستقرّ',
  Down: 'هابط',
};

/**
 * اتّجاه السهم **لا يعني** «جيّد/سيّئ»: مؤشّر `LowerIsBetter` هبوطه تحسّن.
 * لذلك تُعرَض النغمة من نسبة التحقّق لا من الاتّجاه.
 */
export const projectKpiTrendGlyph: Record<ProjectKpiTrend, string> = {
  Unknown: '—',
  Up: '▲',
  Flat: '▬',
  Down: '▼',
};

export const projectKpiSourceTypeLabel: Record<ProjectKpiSourceType, string> = {
  Manual: 'يدويّ',
  TaskDerived: 'مشتقّ من المهامّ',
  Integration: 'تكامل خارجيّ',
};

export const projectDeliverableStatusLabel: Record<ProjectDeliverableStatus, string> = {
  NotStarted: 'لم يبدأ',
  InProgress: 'قيد التنفيذ',
  Delivered: 'مُسلَّم',
  Delayed: 'متأخّر',
  Cancelled: 'ملغى',
};

export function projectDeliverableStatusTone(
  s: ProjectDeliverableStatus,
): 'muted' | 'navy' | 'success' | 'alert' {
  if (s === 'Delivered') return 'success';
  if (s === 'Delayed') return 'alert';
  if (s === 'InProgress') return 'navy';
  return 'muted';
}

/**
 * الحالات **الأربع الظاهرة** + «لم تُقيَّم» (§7). كانت هنا ثلاث تسميات فقط
 * (`Green/Yellow/Red`) بينما يرسل الخادم خمس قيم، فمشروعٌ `Blocked` كان يصل بحالة بلا
 * تسمية فتظهر شارته فارغة. الفرق بين «متأخّر» و«محجوب» ليس تدرّجًا في اللون: الأوّل
 * يحتاج تسريعًا والثاني يحتاج قرارًا يرفع الحاجب، وطمسهما في «متعثّر» يخفي ما يجب فعله.
 */
export const projectHealthStatusLabel: Record<ProjectHealthStatus, string> = {
  Green: 'سليم',
  AtRisk: 'معرّض للخطر',
  Delayed: 'متأخّر',
  Blocked: 'محجوب',
  NotEvaluated: 'غير مقيَّمة',
};

export function projectHealthStatusTone(
  s: ProjectHealthStatus,
): 'success' | 'gold' | 'alert' | 'muted' {
  if (s === 'Green') return 'success';
  if (s === 'AtRisk') return 'gold';
  if (s === 'NotEvaluated') return 'muted';
  return 'alert';
}

/** طريقة احتساب التقدّم (§6-1) — تُعرَض بجوار الرقم لا في سجلّ مخفيّ. */
export const projectProgressModeLabel: Record<ProjectProgressMode, string> = {
  NotCalculated: 'لم تُحتسَب بعد',
  Weighted: 'متوسّط موزون بأوزان المخرَجات',
  EqualWeightFallback: 'متوسّط بأوزان متساوية (تراجُع)',
  NoDeliverables: 'لا مخرَجات نشطة',
};

export const executionProposalStatusLabel: Record<ExecutionProposalStatus, string> = {
  Pending: 'بانتظار المراجعة',
  Accepted: 'مقبول',
  Rejected: 'مرفوض',
};

export function executionProposalStatusTone(
  s: ExecutionProposalStatus,
): 'gold' | 'success' | 'alert' {
  if (s === 'Accepted') return 'success';
  if (s === 'Rejected') return 'alert';
  return 'gold';
}

/**
 * رموز أسباب الصحّة كما يصدرها `ProjectHealthReasonCodes` في الخادم.
 * أيّ رمز جديد يُضاف هناك يظهر هنا نصًّا خامًا حتّى تُضاف ترجمته — وهو سلوك مقصود.
 */
const healthReasonLabels: Record<string, string> = {
  'health.kpi.excluded': 'لا يوجد مؤشّر قابل للاحتساب، فاستُبعد مكوّن المؤشّرات وأُعيد توزيع وزنه.',
  'health.kpi.below_target': 'نتيجة المؤشّرات الموزونة دون العتبة الخضراء.',
  'health.kpi.weights_all_zero': 'كل أوزان المؤشّرات صفر، فعوملت بأوزان متساوية.',
  'health.progress.below_target': 'نسبة تقدّم المشروع المعلَنة دون العتبة الخضراء.',
  'health.schedule.excluded': 'تواريخ المشروع ناقصة، فاستُبعد مكوّن الجدول الزمنيّ وأُعيد توزيع وزنه.',
  'health.schedule.behind_plan': 'التقدّم الفعليّ متأخّر عن التقدّم المتوقَّع زمنيًّا.',
  'health.no_component': 'لا يوجد أيّ مكوّن متاح للاحتساب، فالصحّة غير محتسَبة (وليست صفرًا).',
  'health.all_healthy': 'كل المكوّنات المتاحة فوق العتبة الخضراء.',

  // إشارات تشغيليّة (§7): تسبق الرقم في تحديد الحالة الظاهرة، لأنّ مخاطرة حرجة مفتوحة
  // تحجب المشروع مهما بلغت نتيجته الحسابيّة.
  'health.blocked.open_blocker': 'توجد مخاطرة حرجة مفتوحة تحجب تقدّم المشروع.',
  'health.delayed.overdue_deliverable': 'يوجد مخرَج تعاقديّ تجاوز تاريخ استحقاقه ولم يُسلَّم.',
  'health.at_risk.open_high_risk': 'توجد مخاطرة عالية مفتوحة على المشروع.',
  'health.progress.equal_weight_fallback':
    'أوزان المخرَجات غير مضبوطة، فاحتُسِب التقدّم بأوزان متساوية — الرقم تقديريّ حتّى تُضبَط الأوزان.',
  'health.progress.no_deliverables': 'لا يوجد مخرَج تعاقديّ نشط يُشتقّ منه تقدّم المشروع.',
};

export function healthReasonLabel(code: string): string {
  return healthReasonLabels[code] ?? code;
}

/**
 * عرض نسبة قد تكون «مستبعَدة». `null` **ليست صفرًا** — والخلط بينهما يقلب قراءة
 * اللوحة رأسًا على عقب، فيُعرَض نصّ صريح بدل رقم مُختلَق.
 *
 * التنسيق نفسه يفوَّض إلى `formatPercent` القائم كي لا يكون في التطبيق شكلان للنسبة،
 * وهذه الدالّة تضيف التقريب فقط لأنّ نِسَب الصحّة تصل بكسور طويلة.
 */
export function percentOrDash(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined) return '—';
  return formatPercent(Number(Number(value).toFixed(digits)));
}

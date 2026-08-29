// P1-KPI-004/008 — العقد الموحّد لتحليلات KPI (v2).
//
// قاعدة مُلزِمة لهذا الملفّ ولكلّ مستهلكيه: **لا يُشتقّ أيّ رقم KPI هنا**. لا متوسّطات، ولا
// ترتيب، ولا عتبات، ولا حدود فترات. كلّ ذلك يأتي محسوبًا من الخادم عبر `/api/kpi/*`، لأنّ
// الخادم هو المصدر الوحيد للحقيقة والنطاق يُفرَض فيه لا هنا. الإخفاء في الواجهة ليس حماية.
import { useQuery } from '@tanstack/react-query';
import { api } from './api';

export type KpiCadence = 'WeeklyPulse' | 'Quarterly';
export type KpiDataQuality = 'NoData' | 'Complete' | 'Partial' | 'InsufficientCoverage';
export type KpiTrendValue = 'Unknown' | 'Up' | 'Flat' | 'Down';

/** DEC-01/18 — الحالات الستّ الصريحة لرحلة KPI. لا حالة سابعة ضمنيّة ولا فراغ بلا اسم. */
export type KpiJourneyState =
  | 'CadenceNotConfigured'
  | 'Exempt'
  | 'NotStarted'
  | 'InProgress'
  | 'CompleteEligible'
  | 'InsufficientCoverage';

/** DEC-01/5 — مصدر التواتر الفعليّ للموظّف، مُعلَن دائمًا (لا اختيار صامت). */
export type KpiCadenceSource =
  | 'employeeAssignment'
  | 'teamAssignment'
  | 'jobRole'
  | 'departmentAssignment'
  | 'generalTemplate'
  | 'notConfigured'
  | 'explicitRequest';

export type KpiPeriodResolved = {
  type: string;
  key: string;
  start: string;
  end: string;
  timezone: string;
  isOpen: boolean;
  label: string;
};

/** رقم KPI واحد ببياناته الوصفيّة. `value === null` تعني «لا بيانات» ولا تعني صفرًا أبدًا. */
export type KpiMeasure = {
  value: number | null;
  eligibleEvaluationCount: number;
  expectedEvaluationCount: number;
  adjustedExpectedCount: number;
  coverage: number | null;
  missingCount: number;
  excludedByStatusCount: number;
  dataQuality: KpiDataQuality;
  previousValue: number | null;
  delta: number | null;
  trend: KpiTrendValue;
  /** DEC-01/12 — `Completed ÷ AdjustedExpected × 100` كما حسبها الخادم. لا تُشتقّ هنا. */
  coveragePercent: number | null;
  /** DEC-01/14 — درجة موجودة لكنّ تغطيتها دون العتبة المعتمَدة ⇒ لا تُعتمد نتيجة نهائيّة. */
  isProvisional: boolean;
  /** DEC-01/18 — الحالة الصريحة المعروضة للمستخدم. */
  journeyState: KpiJourneyState;
};

export type KpiEmployeeScore = {
  userId: string;
  fullName: string;
  teamId: string | null;
  teamName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  measure: KpiMeasure;
  eligibleForRanking: boolean;
  isBelowTarget: boolean | null;
  appliedBelowTargetThreshold: number;
  thresholdSource: string;
  /** DEC-01/5 — تواتر الموظّف الفعليّ؛ `null` يعني «التواتر غير مُهيّأ» لا «أسبوعيّ افتراضًا». */
  effectiveCadence: KpiCadence | null;
  cadenceSource: KpiCadenceSource;
};

export type KpiGroupScore = {
  groupType: string;
  groupId: string | null;
  groupName: string | null;
  measure: KpiMeasure;
  scoredMemberCount: number;
  totalMemberCount: number;
  /** DEC-01/16 — عدد من دخلوا المتوسّط الرسميّ فعلًا (تغطية ≥ العتبة). */
  qualifiedMemberCount: number;
  /** DEC-01/17 — غير المؤهّلين لا يختفون: أسماؤهم وحالتهم تُعرَض منفصلة عن المتوسّط. */
  excludedForInsufficientCoverage: KpiEmployeeScore[] | null;
};

export type KpiPerformance = {
  periodResolved: KpiPeriodResolved;
  previousPeriodResolved: KpiPeriodResolved;
  /** DEC-01/2 — `null` يعني «تواتر كلّ موظّف من قالبه» لا «افتراضيّ أسبوعيّ». */
  cadence: KpiCadence | null;
  scopeType: string;
  company: KpiGroupScore;
  departments: KpiGroupScore[];
  teams: KpiGroupScore[];
  employees: KpiEmployeeScore[];
  calculatedAtUtc: string;
};

export type KpiRankings = {
  periodResolved: KpiPeriodResolved;
  cadence: KpiCadence | null;
  scopeType: string;
  topPerformers: KpiEmployeeScore[];
  needsSupport: KpiEmployeeScore[];
  excludedForInsufficientCoverage: number;
  minimumCoverage: number;
  calculatedAtUtc: string;
  /** DEC-01/17 — المستبعَدون بأسمائهم لا بعددهم فقط. */
  excludedEmployees: KpiEmployeeScore[] | null;
  /** DEC-01/5 — من لا تواتر فعّالًا لهم: حالة مسمّاة تُعالَج إداريًّا لا تُخفى. */
  cadenceNotConfiguredEmployees: KpiEmployeeScore[] | null;
};

export type KpiDrilldownRow = {
  evaluationId: string;
  subjectUserId: string;
  subjectName: string;
  templateTitle: string;
  cadence: KpiCadence;
  periodType: string;
  periodKey: string;
  periodStart: string;
  periodEnd: string;
  status: string;
  totalScore: number | null;
  submittedAtUtc: string | null;
};

/** DEC-01/18 — فترة مصدر واحدة داخل النافذة الربعيّة: مكتملة أو مفقودة أو مُعفاة بسبب مسمّى. */
export type KpiSourcePeriod = {
  periodKey: string;
  start: string;
  end: string;
  label: string;
  isCompleted: boolean;
  isExempt: boolean;
  exemptReason: string | null;
  score: number | null;
};

export type KpiDrilldown = {
  periodResolved: KpiPeriodResolved;
  cadence: KpiCadence | null;
  subjectUserId: string | null;
  recomputedValue: number | null;
  rowCount: number;
  rows: KpiDrilldownRow[];
  calculatedAtUtc: string;
  /** DEC-01/18 — أرقام التفصيل: Expected/AdjustedExpected/Completed/Missing/Coverage. */
  measure: KpiMeasure | null;
  sourcePeriods: KpiSourcePeriod[] | null;
  effectiveCadence: KpiCadence | null;
  cadenceSource: KpiCadenceSource;
};

/**
 * مُرشِّح واحد يقود كلّ شيء: البطاقات والرسوم والقوائم والجداول والترتيب والتفصيل.
 *
 * DEC-01/2 — `cadence` **اختياريّ**: `null` (الافتراضيّ) يعني «يحسم الخادم تواتر كلّ موظّف من
 * قالبه الفعّال»، فلا يُسأل المستخدم عن نوع التقييم عند فتح الشاشة. تحديده صراحةً = مسار واحد
 * صريح (DEC-01/3، مثلًا النبض الأسبوعيّ وحده). في الحالتين لا سقوط صامت إلى تواتر مُفترَض.
 */
export type KpiFilter = {
  periodType: string;
  cadence: KpiCadence | null;
  periodKey?: string | null;
  from?: string | null;
  to?: string | null;
  departmentId?: string | null;
  teamId?: string | null;
  subjectUserId?: string | null;
};

export const DEFAULT_KPI_FILTER: KpiFilter = {
  // DEC-01/1 — الربع الميلاديّ الجاري بتوقيت Asia/Riyadh، يحسمه الخادم لا المتصفّح.
  // DEC-01/4 — نافذة العرض ربعيّة حتّى لو كانت التقييمات المكوِّنة أسبوعيّة.
  periodType: 'CurrentQuarter',
  // DEC-01/2 — لا اختيار «نوع تقييم» عند الفتح.
  cadence: null,
};

function params(f: KpiFilter): Record<string, string> {
  const out: Record<string, string> = { periodType: f.periodType };
  if (f.cadence) out.cadence = f.cadence;
  if (f.periodKey) out.periodKey = f.periodKey;
  if (f.from) out.from = f.from;
  if (f.to) out.to = f.to;
  if (f.departmentId) out.departmentId = f.departmentId;
  if (f.teamId) out.teamId = f.teamId;
  if (f.subjectUserId) out.subjectUserId = f.subjectUserId;
  return out;
}

// مفتاح الذاكرة المؤقّتة يضمّ الفترة والكادنس وكلّ مُضيِّقات النطاق. تجاهل أيّها يعني تسريب
// نتائج نطاق إلى نطاق آخر داخل نفس الجلسة — وهو خلل أمنيّ لا خلل عرض.
function key(kind: string, f: KpiFilter) {
  return ['kpi', kind, f.periodType, f.cadence ?? 'auto', f.periodKey ?? '', f.from ?? '', f.to ?? '',
    f.departmentId ?? '', f.teamId ?? '', f.subjectUserId ?? ''] as const;
}

export function useKpiPerformance(filter: KpiFilter, enabled = true) {
  return useQuery({
    queryKey: key('performance', filter),
    queryFn: async () => (await api.get<KpiPerformance>('/kpi/performance', { params: params(filter) })).data,
    enabled,
  });
}

export function useKpiRankings(filter: KpiFilter, take = 6, enabled = true) {
  return useQuery({
    queryKey: [...key('rankings', filter), take],
    queryFn: async () =>
      (await api.get<KpiRankings>('/kpi/rankings', { params: { ...params(filter), take } })).data,
    enabled,
  });
}

export function useKpiDrilldown(filter: KpiFilter, enabled = true) {
  return useQuery({
    queryKey: key('drilldown', filter),
    queryFn: async () => (await api.get<KpiDrilldown>('/kpi/drilldown', { params: params(filter) })).data,
    enabled,
  });
}

/**
 * B-1 — حدود الفترة تُحلّ **على الخادم** بتوقيت `Asia/Riyadh`. الواجهة تستهلكها ولا تشتقّها
 * بـUTC ولا بتوقيت المتصفّح، وإلّا اختلف «الأسبوع» بين مستخدم وآخر.
 */
export function useResolvedPeriod(filter: KpiFilter) {
  return useQuery({
    queryKey: ['kpi', 'period', filter.periodType, filter.periodKey ?? '', filter.from ?? '', filter.to ?? ''],
    queryFn: async () =>
      (
        await api.get<{ current: KpiPeriodResolved; previous: KpiPeriodResolved; weekKeys: string[] }>(
          '/kpi/periods/resolve',
          {
            params: {
              type: filter.periodType,
              ...(filter.periodKey ? { periodKey: filter.periodKey } : {}),
              ...(filter.from ? { from: filter.from } : {}),
              ...(filter.to ? { to: filter.to } : {}),
            },
          },
        )
      ).data,
  });
}

// ===== عرض فقط (لا حساب) =====

/** شارة جودة البيانات — تجعل ضعف التغطية مرئيًّا بدل إخفائه خلف رقم يبدو سليمًا. */
export const dataQualityLabel: Record<KpiDataQuality, string> = {
  NoData: 'لا بيانات',
  Complete: 'تغطية كاملة',
  Partial: 'تغطية جزئيّة',
  InsufficientCoverage: 'تغطية غير كافية',
};

export const dataQualityTone: Record<KpiDataQuality, 'success' | 'gold' | 'alert' | 'muted'> = {
  NoData: 'muted',
  Complete: 'success',
  Partial: 'gold',
  InsufficientCoverage: 'alert',
};

/**
 * نبرة الرقم مقيسة على **العتبة القادمة من الخادم** (B-6) لا على ثوابت متناثرة في الواجهة.
 * `null` تبقى `muted`: لا تقييم ≠ أداء ضعيف.
 */
export function kpiTone(
  value: number | null,
  threshold: number | null,
): 'success' | 'gold' | 'alert' | 'muted' {
  if (value === null || threshold === null) return 'muted';
  if (value >= threshold) return 'success';
  if (value >= threshold * 0.75) return 'gold';
  return 'alert';
}

/**
 * B-6 — العتبة المطبَّقة كما أعادها الخادم مع الدرجات. **لا ثابت احتياطيّ في الواجهة**:
 * غياب العتبة يعني «لا حكم» (`null`) لا «افترض 60».
 */
export function appliedThreshold(perf?: { employees: KpiEmployeeScore[] }): number | null {
  return perf?.employees?.[0]?.appliedBelowTargetThreshold ?? null;
}

/**
 * DEC-01/18 — الحالات الستّ بأسمائها الظاهرة للمستخدم. لا حالة تُطوى في «—» ولا في فراغ:
 * انعدام التواتر والإعفاء سببان مختلفان تمامًا لانعدام الرقم، ويجب أن يفترقا في الشاشة.
 */
export const journeyStateLabel: Record<KpiJourneyState, string> = {
  CadenceNotConfigured: 'التواتر غير مُهيّأ',
  Exempt: 'مُعفى',
  NotStarted: 'لم يبدأ',
  InProgress: 'قيد الاستكمال',
  CompleteEligible: 'مكتمل ومؤهّل',
  InsufficientCoverage: 'تغطية غير كافية',
};

export const journeyStateTone: Record<KpiJourneyState, 'success' | 'gold' | 'alert' | 'muted'> = {
  CadenceNotConfigured: 'alert',
  Exempt: 'muted',
  NotStarted: 'muted',
  InProgress: 'gold',
  CompleteEligible: 'success',
  InsufficientCoverage: 'alert',
};

/** DEC-01/5 — مصدر التواتر يُعرَض نصًّا مفهومًا: المستخدم يعرف **لماذا** هذا تواتره. */
export const cadenceSourceLabel: Record<KpiCadenceSource, string> = {
  employeeAssignment: 'إسناد خاصّ بالموظّف',
  teamAssignment: 'إسناد الفريق',
  jobRole: 'المسمّى الوظيفيّ',
  departmentAssignment: 'إسناد الإدارة',
  generalTemplate: 'قالب عامّ',
  notConfigured: 'غير مُهيّأ',
  explicitRequest: 'اختيار صريح في المُرشِّح',
};

export const cadenceLabel: Record<KpiCadence, string> = {
  WeeklyPulse: 'نبض أسبوعيّ',
  Quarterly: 'تقييم ربعيّ رسميّ',
};

/** DEC-01/8 — أسباب إسقاط الالتزام من المقام، مسمّاة لا مطويّة. */
export const exemptReasonLabel: Record<string, string> = {
  approvedLeave: 'إجازة معتمَدة',
  administrativeExemption: 'إعفاء إداريّ مسجَّل',
  beforeHireDate: 'قبل تاريخ الالتحاق',
  afterExitDate: 'بعد انتهاء الخدمة',
};

export const trendLabel: Record<KpiTrendValue, string> = {
  Unknown: '—',
  Up: 'صاعد',
  Flat: 'مستقرّ',
  Down: 'هابط',
};

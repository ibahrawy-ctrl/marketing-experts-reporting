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
};

export type KpiGroupScore = {
  groupType: string;
  groupId: string | null;
  groupName: string | null;
  measure: KpiMeasure;
  scoredMemberCount: number;
  totalMemberCount: number;
};

export type KpiPerformance = {
  periodResolved: KpiPeriodResolved;
  previousPeriodResolved: KpiPeriodResolved;
  cadence: KpiCadence;
  scopeType: string;
  company: KpiGroupScore;
  departments: KpiGroupScore[];
  teams: KpiGroupScore[];
  employees: KpiEmployeeScore[];
  calculatedAtUtc: string;
};

export type KpiRankings = {
  periodResolved: KpiPeriodResolved;
  cadence: KpiCadence;
  scopeType: string;
  topPerformers: KpiEmployeeScore[];
  needsSupport: KpiEmployeeScore[];
  excludedForInsufficientCoverage: number;
  minimumCoverage: number;
  calculatedAtUtc: string;
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

export type KpiDrilldown = {
  periodResolved: KpiPeriodResolved;
  cadence: KpiCadence;
  subjectUserId: string | null;
  recomputedValue: number | null;
  rowCount: number;
  rows: KpiDrilldownRow[];
  calculatedAtUtc: string;
};

/**
 * مُرشِّح واحد يقود كلّ شيء: البطاقات والرسوم والقوائم والجداول والترتيب والتفصيل.
 * `cadence` **إلزاميّ** (B-3) — لا قيمة ضمنيّة ولا خلط بين النبض الأسبوعيّ والربعيّ الرسميّ.
 */
export type KpiFilter = {
  periodType: string;
  cadence: KpiCadence;
  periodKey?: string | null;
  from?: string | null;
  to?: string | null;
  departmentId?: string | null;
  teamId?: string | null;
  subjectUserId?: string | null;
};

export const DEFAULT_KPI_FILTER: KpiFilter = {
  // الافتراضيّ = آخر فترة مكتملة (§5.4): لا اتّجاه رسميّ من فترة مفتوحة.
  periodType: 'LastCompletedWeek',
  cadence: 'WeeklyPulse',
};

function params(f: KpiFilter): Record<string, string> {
  const out: Record<string, string> = { periodType: f.periodType, cadence: f.cadence };
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
  return ['kpi', kind, f.periodType, f.cadence, f.periodKey ?? '', f.from ?? '', f.to ?? '',
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

export const trendLabel: Record<KpiTrendValue, string> = {
  Unknown: '—',
  Up: 'صاعد',
  Flat: 'مستقرّ',
  Down: 'هابط',
};

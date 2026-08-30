// ======================================================================
// OBS-R5-01 — تجهيزات مشتركة لمساري KPI في اختبارات الواجهة.
//
// النصوص هنا **منسوخة حرفيًّا** من `KpiEvaluationService.BuildTrackAsync` (الخادم)، لأنّ
// الاختبارات تقيس ما تعرضه الواجهة لمستخدم حقيقيّ ⟹ أيّ انحراف بين النصّين يجب أن يظهر
// عند مقارنة الملفّين لا أن يُخفى بصياغة تقريبيّة داخل الاختبار.
// ======================================================================

import type {
  KpiCadence,
  KpiCadenceSource,
  KpiEvaluationSetupTemplateDto,
  KpiEvaluationTrackDto,
} from '../types/api';

export const CURRENT_QUARTER_KEY = '2026-Q3';
export const CURRENT_WEEK_KEY = '2026-W35';

export const WEEKLY_NOT_CONFIGURED_REASON =
  'مسار نبض الأسبوع غير مُهيّأ لهذا الموظّف: لا قالب أسبوعيّ فعّال مُسنَد له. هذا لا يمسّ المسار الربعيّ الرسميّ.';

export const QUARTERLY_NOT_CONFIGURED_REASON =
  'المسار الربعيّ الرسميّ غير مُهيّأ لهذا الموظّف: لا قالب ربعيّ فعّال مُسنَد له. هذا لا يمسّ مسار نبض الأسبوع.';

/** مسار مُهيّأ: مصدر حسمه خاصّ به، وقوالبه خاصّة به — لا تُشتقّ من المسار الآخر. */
export function weeklyTrack(
  cadenceSource: KpiCadenceSource,
  templates: KpiEvaluationSetupTemplateDto[],
): KpiEvaluationTrackDto {
  return {
    cadence: 'WeeklyPulse',
    cadenceSource,
    periodType: 'Weekly',
    currentPeriodKey: CURRENT_WEEK_KEY,
    templates,
    isConfigured: true,
    blockingReason: null,
  };
}

export function quarterlyTrack(
  cadenceSource: KpiCadenceSource,
  templates: KpiEvaluationSetupTemplateDto[],
): KpiEvaluationTrackDto {
  return {
    cadence: 'Quarterly',
    cadenceSource,
    periodType: 'Quarterly',
    currentPeriodKey: CURRENT_QUARTER_KEY,
    templates,
    isConfigured: true,
    blockingReason: null,
  };
}

/** مسار غير مُهيّأ: يبقى **ظاهرًا** بفترته ونوعه وسببه المسمّى، ولا يُخفي المسار المقابل. */
export function blockedTrack(cadence: KpiCadence): KpiEvaluationTrackDto {
  const isQuarterly = cadence === 'Quarterly';
  return {
    cadence,
    cadenceSource: 'notConfigured',
    periodType: isQuarterly ? 'Quarterly' : 'Weekly',
    currentPeriodKey: isQuarterly ? CURRENT_QUARTER_KEY : CURRENT_WEEK_KEY,
    templates: [],
    isConfigured: false,
    blockingReason: isQuarterly ? QUARTERLY_NOT_CONFIGURED_REASON : WEEKLY_NOT_CONFIGURED_REASON,
  };
}

// P1-KPI-008 + R5/DEC-01 — اختبارات شاشة نظرة KPI الموحّدة.
//
// ما تثبته هذه الاختبارات تحديدًا:
// (أ) الشاشة **تعرض** أرقام الخادم ولا تشتقّها: العينة تحاكي موظّفًا له تقييمان 85 و45،
//     والخادم يعيد متوسّطه 65 — فإن ظهر 85 يومًا ما فذلك عودة عيب «الطيّ إلى أعلى تقييم».
// (ب) «لا تقييم» ≠ صفر، و«مفقود» ≠ صفر: الغياب يظهر نصًّا صريحًا، والصفر الحقيقيّ يظهر رقمًا.
// (ج) مُرشِّح واحد يقود كلّ الطلبات (الفترة + الكادنس + النطاق) وينتقل إلى مفاتيح الذاكرة المؤقّتة.
// (د) حدود الفترة لا تُحسب في الواجهة: تُرسَل الأنواع/المفاتيح فقط ويُعرض ما حلّه الخادم.
// (هـ) DEC-01: الربع الجاري افتراضيًّا بلا سؤال عن نوع التقييم · الحالات الستّ · التغطية والمتوقَّع
//      المعدَّل · الدرجة المؤقّتة · المستبعَدون بأسمائهم · تفصيل يصل إلى الفترات المصدر.
import { render, screen, fireEvent, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { KpiOverview } from './KpiOverview';
import type { KpiEmployeeScore, KpiMeasure } from '../lib/useKpi';

const pct = (n: number) => `${n.toLocaleString('ar-EG')}٪`;

function measure(over: Partial<KpiMeasure> = {}): KpiMeasure {
  return {
    value: 65,
    eligibleEvaluationCount: 2,
    expectedEvaluationCount: 2,
    adjustedExpectedCount: 2,
    coverage: 1,
    missingCount: 0,
    excludedByStatusCount: 0,
    dataQuality: 'Complete',
    previousValue: 60,
    delta: 5,
    trend: 'Up',
    coveragePercent: 100,
    isProvisional: false,
    journeyState: 'CompleteEligible',
    ...over,
  };
}

function employee(id: string, name: string, m: KpiMeasure, over: Partial<KpiEmployeeScore> = {}): KpiEmployeeScore {
  return {
    userId: id,
    fullName: name,
    teamId: 'team-1',
    teamName: 'فريق المحتوى',
    departmentId: 'dep-1',
    departmentName: 'إدارة التسويق',
    measure: m,
    eligibleForRanking: true,
    isBelowTarget: m.value === null ? null : m.value < 60,
    appliedBelowTargetThreshold: 60,
    thresholdSource: 'kpiTemplateVersion',
    effectiveCadence: 'WeeklyPulse',
    cadenceSource: 'jobRole',
    ...over,
  };
}

// سارة: تقييمان 85 و45 ⇒ الخادم يعيد 65. المتوسّط لا يساوي أعلى تقييم.
const SARA = employee('u-1', 'سارة أحمد', measure({ excludedByStatusCount: 1 }));
// خالد: لا تقييم مكتمل — «لا تقييم» لا صفر، و«لم يبدأ» لا «تغطية غير كافية».
const KHALED = employee('u-2', 'خالد سالم', measure({
  value: null, eligibleEvaluationCount: 0, adjustedExpectedCount: 1, coverage: 0,
  missingCount: 1, dataQuality: 'NoData', previousValue: null, delta: null, trend: 'Unknown',
  coveragePercent: 0, journeyState: 'NotStarted',
}));
// ريم: صفر حقيقيّ — رقم صالح لا غياب.
const REEM = employee('u-3', 'ريم ناصر', measure({ value: 0, previousValue: 10, delta: -10, trend: 'Down' }));
// فهد — DEC-01/15 المثال الحاكم حرفيًّا: تقييم واحد ممتاز من تسعة متوقَّعة.
// 100٪ درجة · 11.11٪ تغطية · 8 مفقود · تغطية غير كافية · لا نتيجة ربعيّة نهائيّة.
const FAHD = employee('u-4', 'فهد العتيبي', measure({
  value: 100, eligibleEvaluationCount: 1, expectedEvaluationCount: 9, adjustedExpectedCount: 9,
  coverage: 0.1111, missingCount: 8, dataQuality: 'InsufficientCoverage',
  coveragePercent: 11.11, isProvisional: true, journeyState: 'InsufficientCoverage',
  previousValue: null, delta: null, trend: 'Unknown',
}), { eligibleForRanking: false });
// نورة — DEC-01/5: لا قالب فعّال ⇒ «التواتر غير مُهيّأ» بلا اختيار ضمنيّ ولا صفر.
const NOURA = employee('u-5', 'نورة الحربي', measure({
  value: null, eligibleEvaluationCount: 0, expectedEvaluationCount: 0, adjustedExpectedCount: 0,
  coverage: null, missingCount: 0, dataQuality: 'NoData', previousValue: null, delta: null,
  trend: 'Unknown', coveragePercent: null, journeyState: 'CadenceNotConfigured',
}), { eligibleForRanking: false, effectiveCadence: null, cadenceSource: 'notConfigured' });
// ماجد — DEC-01/8: أربع فترات أُسقِطت بإجازة معتمدة ⇒ المتوقَّع 13 والمعدَّل 9، والرقمان معروضان.
const MAJED = employee('u-6', 'ماجد القحطاني', measure({
  value: 88, eligibleEvaluationCount: 9, expectedEvaluationCount: 13, adjustedExpectedCount: 9,
  coverage: 1, missingCount: 0, coveragePercent: 100, journeyState: 'CompleteEligible',
}));

// DEC-01/1 — الفترة الافتراضيّة ربع ميلاديّ جارٍ بتوقيت الرياض، يحلّها الخادم.
const PERIOD = {
  type: 'Quarter', key: '2026-Q3', start: '2026-07-01', end: '2026-09-30',
  timezone: 'Asia/Riyadh', isOpen: true, label: 'الربع 3 — 2026',
};

const PERFORMANCE = {
  periodResolved: PERIOD,
  previousPeriodResolved: { ...PERIOD, key: '2026-Q2', label: 'الربع 2 — 2026', isOpen: false },
  // DEC-01/2 — لا كادنس مفروض: الخادم حسم تواتر كلّ موظّف من قالبه.
  cadence: null,
  scopeType: 'Company',
  company: {
    groupType: 'Company', groupId: null, groupName: 'الشركة',
    measure: measure({
      eligibleEvaluationCount: 3, expectedEvaluationCount: 4, adjustedExpectedCount: 4,
      coverage: 0.75, missingCount: 1, dataQuality: 'Partial', coveragePercent: 75,
      journeyState: 'InProgress',
    }),
    scoredMemberCount: 4, totalMemberCount: 6,
    // DEC-01/16 — المتوسّط الرسميّ من المؤهّلين وحدهم (فهد ونورة خارجه).
    qualifiedMemberCount: 3,
    // DEC-01/17 — المستبعَد لا يختفي: يظهر باسمه وحالة نقصه.
    excludedForInsufficientCoverage: [FAHD],
  },
  departments: [{
    groupType: 'Department', groupId: 'dep-1', groupName: 'إدارة التسويق',
    measure: measure(), scoredMemberCount: 4, totalMemberCount: 6,
    qualifiedMemberCount: 3, excludedForInsufficientCoverage: [FAHD],
  }],
  teams: [{
    groupType: 'Team', groupId: 'team-1', groupName: 'فريق المحتوى',
    measure: measure(), scoredMemberCount: 4, totalMemberCount: 6,
    qualifiedMemberCount: 3, excludedForInsufficientCoverage: [FAHD],
  }],
  employees: [SARA, KHALED, REEM, FAHD, NOURA, MAJED],
  calculatedAtUtc: '2026-08-30T10:00:00Z',
};

const RANKINGS = {
  periodResolved: PERIOD,
  cadence: null,
  scopeType: 'Company',
  topPerformers: [SARA],
  needsSupport: [REEM],
  excludedForInsufficientCoverage: 1,
  // DEC-01/13 — الحدّ الأدنى المعتمَد 80% لا 75%.
  minimumCoverage: 0.8,
  calculatedAtUtc: '2026-08-30T10:00:00Z',
  excludedEmployees: [FAHD],
  cadenceNotConfiguredEmployees: [NOURA],
};

// تفصيل سارة يعيد إنتاج 65 من صفّيه (85 و45) — فيتحقّق المستخدم من الرقم بنفسه،
// ويصل إلى الأرقام الخمسة والفترات المصدر (DEC-01/18).
const DRILLDOWN = {
  periodResolved: PERIOD,
  cadence: null,
  subjectUserId: 'u-1',
  recomputedValue: 65,
  rowCount: 2,
  rows: [
    { evaluationId: 'ev-1', subjectUserId: 'u-1', subjectName: 'سارة أحمد', templateTitle: 'نبض المحتوى', cadence: 'WeeklyPulse', periodType: 'Weekly', periodKey: '2026-W33', periodStart: '2026-08-15', periodEnd: '2026-08-21', status: 'Approved', totalScore: 85, submittedAtUtc: null },
    { evaluationId: 'ev-2', subjectUserId: 'u-1', subjectName: 'سارة أحمد', templateTitle: 'نبض المحتوى', cadence: 'WeeklyPulse', periodType: 'Weekly', periodKey: '2026-W34', periodStart: '2026-08-22', periodEnd: '2026-08-28', status: 'Approved', totalScore: 45, submittedAtUtc: null },
  ],
  calculatedAtUtc: '2026-08-30T10:00:00Z',
  measure: measure({
    value: 65, eligibleEvaluationCount: 2, expectedEvaluationCount: 4, adjustedExpectedCount: 3,
    coverage: 0.6667, missingCount: 1, coveragePercent: 66.67, isProvisional: true,
    journeyState: 'InProgress',
  }),
  sourcePeriods: [
    { periodKey: '2026-W33', start: '2026-08-15', end: '2026-08-21', label: 'الأسبوع 33 — 2026', isCompleted: true, isExempt: false, exemptReason: null, score: 85 },
    { periodKey: '2026-W34', start: '2026-08-22', end: '2026-08-28', label: 'الأسبوع 34 — 2026', isCompleted: true, isExempt: false, exemptReason: null, score: 45 },
    { periodKey: '2026-W35', start: '2026-08-29', end: '2026-09-04', label: 'الأسبوع 35 — 2026', isCompleted: false, isExempt: false, exemptReason: null, score: null },
    { periodKey: '2026-W36', start: '2026-09-05', end: '2026-09-11', label: 'الأسبوع 36 — 2026', isCompleted: false, isExempt: true, exemptReason: 'approvedLeave', score: null },
  ],
  effectiveCadence: 'WeeklyPulse',
  cadenceSource: 'jobRole',
};

type Call = { url: string; params: Record<string, unknown> };
let calls: Call[] = [];

function mockOk() {
  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: Record<string, unknown> }) => {
    calls.push({ url, params: config?.params ?? {} });
    if (url.startsWith('/kpi/rankings')) return Promise.resolve({ data: RANKINGS } as never);
    if (url.startsWith('/kpi/drilldown')) return Promise.resolve({ data: DRILLDOWN } as never);
    return Promise.resolve({ data: PERFORMANCE } as never);
  });
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <KpiOverview />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const paramsFor = (path: string) => calls.filter((c) => c.url.startsWith(path)).map((c) => c.params);

beforeEach(() => {
  calls = [];
  vi.restoreAllMocks();
});

describe('KpiOverview — حالات التحميل والخطأ', () => {
  it('يعرض حالة تحميل قبل وصول البيانات', () => {
    vi.spyOn(api, 'get').mockImplementation(() => new Promise(() => {}) as never);
    renderPage();
    expect(screen.getByText('يتم تحميل ملخص المؤشّرات…')).toBeInTheDocument();
  });

  it('يعرض رسالة خطأ مفهومة مع إعادة المحاولة عند فشل الجلب', async () => {
    vi.spyOn(api, 'get').mockImplementation(() => Promise.reject(new Error('boom')) as never);
    renderPage();
    expect(await screen.findByText('حدث خطأ أثناء جلب ملخص المؤشّرات. أعد المحاولة.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
  });
});

describe('KpiOverview — الأرقام كما يحسبها الخادم', () => {
  beforeEach(mockOk);

  it('يعرض متوسّط الشركة والفريق من الخادم بلا اشتقاق في الواجهة', async () => {
    renderPage();
    expect(await screen.findByText('متوسط مؤشر الشركة')).toBeInTheDocument();
    // متوسّط الفريق = 65 (لا 85 = أعلى تقييم، ولا متوسّط خام على التقييمات).
    expect(screen.getAllByText(pct(65)).length).toBeGreaterThan(0);
    expect(screen.queryByText(pct(85))).not.toBeInTheDocument();
  });

  it('يميّز «لا تقييم» عن الصفر الحقيقيّ', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    // نبحث داخل لوحة أعضاء الفريق فقط: الأسماء تتكرّر عمدًا في قوائم الترتيب.
    const panel = (await screen.findByText(/أعضاء فريق/)).closest('div.rounded-xl') as HTMLElement;
    const cardOf = (name: string) =>
      within(panel).getByText(name).closest('div.rounded-lg') as HTMLElement;
    // خالد بلا تقييم مكتمل ⇒ نصّ صريح لا رقم، ولا صفر مصطنع.
    const khaled = cardOf('خالد سالم');
    expect(within(khaled).getByText('لا تقييم')).toBeInTheDocument();
    expect(within(khaled).queryByText(pct(0))).not.toBeInTheDocument();
    // ريم درجتها صفر فعليّ ⇒ تظهر رقمًا لا غيابًا.
    const reem = cardOf('ريم ناصر');
    expect(within(reem).getByText(pct(0))).toBeInTheDocument();
    expect(within(reem).queryByText('لا تقييم')).not.toBeInTheDocument();
  });

  it('لا يكرّر موظّفًا بين «الأعلى أداءً» و«الأكثر حاجة للدعم»', async () => {
    renderPage();
    await screen.findByText('الأعلى أداءً');
    expect(screen.getByText('الأكثر حاجة للدعم')).toBeInTheDocument();
    // كلّ اسم يظهر مرّة واحدة فقط: القائمتان تأتيان من الخادم بلا تقاطع وبلا فرز محلّيّ.
    expect(screen.getAllByText('سارة أحمد')).toHaveLength(1);
    expect(screen.getAllByText('ريم ناصر')).toHaveLength(1);
  });
});

describe('KpiOverview — عقد DEC-01', () => {
  beforeEach(mockOk);

  it('البند 1+2: يفتح على الربع الجاري ولا يفرض «نوع تقييم» عند الفتح', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');

    const first = paramsFor('/kpi/performance')[0];
    expect(first).toMatchObject({ periodType: 'CurrentQuarter' });
    // لا كادنس مُرسَل ⇒ الخادم يحسم تواتر كلّ موظّف من قالبه الفعّال.
    expect(first.cadence).toBeUndefined();
    expect(paramsFor('/kpi/rankings')[0].cadence).toBeUndefined();
    // المُرشِّح يعرض «تلقائي» لا اختيارًا مفروضًا على المستخدم.
    expect((screen.getByLabelText('الكادنس') as HTMLSelectElement).value).toBe('');
  });

  it('البند 3: تحديد نوع التقييم صراحةً يفصل المسارين ويقود كلّ الطلبات', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    calls = [];
    fireEvent.change(screen.getByLabelText('الكادنس'), { target: { value: 'Quarterly' } });
    expect(await screen.findByText('متوسط مؤشر الشركة')).toBeInTheDocument();
    expect(paramsFor('/kpi/performance').at(-1)).toMatchObject({ cadence: 'Quarterly' });
    expect(paramsFor('/kpi/rankings').at(-1)).toMatchObject({ cadence: 'Quarterly' });
  });

  it('البند 1: التنقّل إلى ربع تاريخيّ متاح بلا أن يفقد المستخدم الربع الجاري كافتراضيّ', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    calls = [];
    fireEvent.change(screen.getByLabelText('نوع الفترة'), { target: { value: 'Quarter' } });
    fireEvent.change(screen.getByLabelText('مفتاح الفترة'), { target: { value: '2025-Q4' } });
    expect(await screen.findByText('متوسط مؤشر الشركة')).toBeInTheDocument();
    const last = paramsFor('/kpi/performance').at(-1)!;
    expect(last).toMatchObject({ periodType: 'Quarter', periodKey: '2025-Q4' });
    // لا حدود مشتقّة في الواجهة: النوع والمفتاح فقط.
    expect(last.from).toBeUndefined();
    expect(last.to).toBeUndefined();
  });

  it('البند 5: من لا تواتر فعّالًا له يظهر «التواتر غير مُهيّأ» لا صفرًا ولا اختيارًا ضمنيًّا', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    const panel = screen.getByText(/^التواتر غير مُهيّأ \(1\)$/).closest('div.rounded-xl') as HTMLElement;
    expect(within(panel).getByText('نورة الحربي')).toBeInTheDocument();
    expect(screen.queryByText('نورة الحربي — 0٪')).not.toBeInTheDocument();
  });

  it('البند 5: تواتر الموظّف ومصدره معروضان صراحةً', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    expect((await screen.findAllByText(/نبض أسبوعيّ · المسمّى الوظيفيّ/)).length).toBeGreaterThan(0);
  });

  it('البند 8: يعرض المتوقَّع الخامّ والمعدَّل معًا لا رقمًا واحدًا', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    const panel = (await screen.findByText(/أعضاء فريق/)).closest('div.rounded-xl') as HTMLElement;
    const majed = within(panel).getByText('ماجد القحطاني').closest('div.rounded-lg') as HTMLElement;
    expect(within(majed).getByText('9/9 (١٠٠٪)')).toBeInTheDocument();
    expect(within(majed).getByText(/متوقَّع خامّ 13 \(أُسقِط 4\)/)).toBeInTheDocument();
  });

  it('البند 10: الفترة المفقودة تُعرَض «مفقود» لا صفرًا', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    const panel = (await screen.findByText(/أعضاء فريق/)).closest('div.rounded-xl') as HTMLElement;
    const khaled = within(panel).getByText('خالد سالم').closest('div.rounded-lg') as HTMLElement;
    expect(within(khaled).getByText(/1 مفقود/)).toBeInTheDocument();
  });

  it('البند 12+14+15: المثال الحاكم — 100٪ درجة مؤقّتة بتغطية 11.11٪ و8 مفقود وتغطية غير كافية', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    // لوحة المستبعَدين على مستوى الشركة تحمل الحالة كاملة باسم الموظّف.
    const panel = screen
      .getByText(/^مستبعَدون من المتوسّط الرسميّ لضعف التغطية \(1\)$/)
      .closest('div.rounded-xl') as HTMLElement;
    const row = within(panel).getByText('فهد العتيبي').closest('li') as HTMLElement;
    expect(within(row).getByText('تغطية غير كافية')).toBeInTheDocument();
    expect(within(row).getByText(`1/9 (${pct(11.11)})`)).toBeInTheDocument();
    expect(within(row).getByText(/8 مفقود/)).toBeInTheDocument();
  });

  it('البند 14: الدرجة دون العتبة تُوسَم «مؤقّتة» صراحةً', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    const panel = screen.getByText(/^خارج الترتيب لضعف التغطية \(1\)$/).closest('div.rounded-xl') as HTMLElement;
    const row = within(panel).getByText('فهد العتيبي').closest('li') as HTMLElement;
    expect(within(row).getByText(pct(100))).toBeInTheDocument();
    expect(within(row).getByText('مؤقّتة')).toBeInTheDocument();
  });

  it('البند 13: عتبة الاعتماد المعروضة هي 80٪ كما أعادها الخادم لا ثابتًا في الواجهة', async () => {
    renderPage();
    expect(await screen.findByText(/تغطية لا تقلّ عن 80٪ · مستبعَدون لضعف التغطية: 1/)).toBeInTheDocument();
  });

  it('البند 16+17: المتوسّط الرسميّ يُعلِن عدد المؤهّلين، والمستبعَدون يظهرون بأسمائهم', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    // 3 مؤهّلون من 6 أعضاء — لا يُخفى أنّ نصف الفريق خارج الرقم الرسميّ.
    expect(screen.getByText('مؤهّلون للمتوسّط الرسميّ')).toBeInTheDocument();
    expect(screen.getAllByText('3/6').length).toBeGreaterThan(0);
    expect(screen.getByText(/^مستبعَدون من المتوسّط الرسميّ لضعف التغطية \(1\)$/)).toBeInTheDocument();
  });
});

describe('KpiOverview — المُرشِّح الموحّد', () => {
  beforeEach(mockOk);

  it('يعرض حدود الفترة كما حلّها الخادم بتوقيت الرياض', async () => {
    renderPage();
    expect(
      await screen.findByText(/الربع 3 — 2026 · 2026-07-01 ← 2026-09-30 · Asia\/Riyadh/),
    ).toBeInTheDocument();
  });
});

describe('KpiOverview — تفصيل الرقم', () => {
  beforeEach(mockOk);

  it('يعيد التفصيل إنتاج الرقم نفسه من صفوفه ضمن نفس الفترة والنطاق', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    fireEvent.click((await screen.findAllByText('تفصيل الرقم'))[0]);
    expect(await screen.findByText(/2 تقييم مكتمل معتمَد · المتوسّط المُعاد حسابه:/)).toBeInTheDocument();
    expect(screen.getAllByText(pct(85)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(pct(45)).length).toBeGreaterThan(0);
    const drill = paramsFor('/kpi/drilldown').at(-1)!;
    expect(drill).toMatchObject({ subjectUserId: 'u-1', periodType: 'CurrentQuarter' });
    expect(drill.cadence).toBeUndefined();
  });

  it('البند 18: التفصيل يصل إلى المتوقَّع والمعدَّل والمكتمل والمفقود والتغطية', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    fireEvent.click((await screen.findAllByText('تفصيل الرقم'))[0]);
    const numbers = (await screen.findByText('المتوقَّع المعدَّل')).closest('div.grid') as HTMLElement;
    const cellOf = (label: string) =>
      (within(numbers).getByText(label).closest('div') as HTMLElement).textContent ?? '';
    expect(cellOf('المتوقَّع')).toContain('4');
    expect(cellOf('المتوقَّع المعدَّل')).toContain('3');
    expect(cellOf('المكتمل')).toContain('2');
    expect(cellOf('المفقود')).toContain('1');
    expect(cellOf('التغطية')).toContain(pct(66.67));
  });

  it('البند 18: التفصيل يعرض الفترات المصدر مميِّزًا المكتملة والمفقودة والمُعفاة', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    fireEvent.click((await screen.findAllByText('تفصيل الرقم'))[0]);
    expect(await screen.findByText('الأسبوع 33 — 2026')).toBeInTheDocument();
    // W35 مفقودة ⇒ «مفقودة» لا صفر؛ W36 مُعفاة بسبب مسمّى لا مطويّ.
    const missing = screen.getByText('الأسبوع 35 — 2026').closest('li') as HTMLElement;
    expect(within(missing).getByText('مفقودة')).toBeInTheDocument();
    expect(within(missing).queryByText(pct(0))).not.toBeInTheDocument();
    const exempt = screen.getByText('الأسبوع 36 — 2026').closest('li') as HTMLElement;
    expect(within(exempt).getByText('إجازة معتمَدة')).toBeInTheDocument();
  });
});

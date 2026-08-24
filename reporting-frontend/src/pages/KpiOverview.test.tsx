// P1-KPI-008 — اختبارات شاشة نظرة KPI الموحّدة.
//
// ما تثبته هذه الاختبارات تحديدًا:
// (أ) الشاشة **تعرض** أرقام الخادم ولا تشتقّها: العينة تحاكي موظّفًا له تقييمان 85 و45،
//     والخادم يعيد متوسّطه 65 — فإن ظهر 85 يومًا ما فذلك عودة عيب «الطيّ إلى أعلى تقييم».
// (ب) «لا تقييم» ≠ صفر: الغياب يظهر نصًّا صريحًا، والصفر الحقيقيّ يظهر رقمًا.
// (ج) مُرشِّح واحد يقود كلّ الطلبات (الفترة + الكادنس + النطاق) وينتقل إلى مفاتيح الذاكرة المؤقّتة.
// (د) حدود الفترة لا تُحسب في الواجهة: تُرسَل الأنواع/المفاتيح فقط ويُعرض ما حلّه الخادم.
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
    ...over,
  };
}

// سارة: تقييمان 85 و45 ⇒ الخادم يعيد 65. المتوسّط لا يساوي أعلى تقييم.
const SARA = employee('u-1', 'سارة أحمد', measure({ excludedByStatusCount: 1 }));
// خالد: لا تقييم معتمَد — «لا تقييم» لا صفر.
const KHALED = employee('u-2', 'خالد سالم', measure({
  value: null, eligibleEvaluationCount: 0, adjustedExpectedCount: 1, coverage: 0,
  missingCount: 1, dataQuality: 'NoData', previousValue: null, delta: null, trend: 'Unknown',
}));
// ريم: صفر حقيقيّ — رقم صالح لا غياب.
const REEM = employee('u-3', 'ريم ناصر', measure({ value: 0, previousValue: 10, delta: -10, trend: 'Down' }));
// فهد: تغطية غير كافية ⇒ يُعرَض فرديًّا وخارج الترتيب (B-5).
const FAHD = employee('u-4', 'فهد العتيبي', measure({
  value: 70, eligibleEvaluationCount: 1, expectedEvaluationCount: 4, adjustedExpectedCount: 4,
  coverage: 0.25, missingCount: 3, dataQuality: 'InsufficientCoverage',
}), { eligibleForRanking: false });

const PERIOD = {
  type: 'LastCompletedWeek', key: '2026-W33', start: '2026-08-15', end: '2026-08-21',
  timezone: 'Asia/Riyadh', isOpen: false, label: 'الأسبوع 33',
};

const PERFORMANCE = {
  periodResolved: PERIOD,
  previousPeriodResolved: { ...PERIOD, key: '2026-W32', label: 'الأسبوع 32' },
  cadence: 'WeeklyPulse',
  scopeType: 'Company',
  company: {
    groupType: 'Company', groupId: null, groupName: 'الشركة',
    measure: measure({ eligibleEvaluationCount: 3, adjustedExpectedCount: 4, coverage: 0.75, missingCount: 1, dataQuality: 'Partial' }),
    scoredMemberCount: 3, totalMemberCount: 4,
  },
  departments: [{
    groupType: 'Department', groupId: 'dep-1', groupName: 'إدارة التسويق',
    measure: measure(), scoredMemberCount: 3, totalMemberCount: 4,
  }],
  teams: [{
    groupType: 'Team', groupId: 'team-1', groupName: 'فريق المحتوى',
    measure: measure(), scoredMemberCount: 3, totalMemberCount: 4,
  }],
  employees: [SARA, KHALED, REEM, FAHD],
  calculatedAtUtc: '2026-08-24T10:00:00Z',
};

const RANKINGS = {
  periodResolved: PERIOD,
  cadence: 'WeeklyPulse',
  scopeType: 'Company',
  topPerformers: [SARA],
  needsSupport: [REEM],
  excludedForInsufficientCoverage: 1,
  minimumCoverage: 0.75,
  calculatedAtUtc: '2026-08-24T10:00:00Z',
};

// تفصيل سارة يعيد إنتاج 65 من صفّيه (85 و45) — فيتحقّق المستخدم من الرقم بنفسه.
const DRILLDOWN = {
  periodResolved: PERIOD,
  cadence: 'WeeklyPulse',
  subjectUserId: 'u-1',
  recomputedValue: 65,
  rowCount: 2,
  rows: [
    { evaluationId: 'ev-1', subjectUserId: 'u-1', subjectName: 'سارة أحمد', templateTitle: 'نبض المحتوى', cadence: 'WeeklyPulse', periodType: 'Weekly', periodKey: '2026-W33', periodStart: '2026-08-15', periodEnd: '2026-08-21', status: 'Approved', totalScore: 85, submittedAtUtc: null },
    { evaluationId: 'ev-2', subjectUserId: 'u-1', subjectName: 'سارة أحمد', templateTitle: 'نبض المحتوى', cadence: 'WeeklyPulse', periodType: 'Weekly', periodKey: '2026-W33', periodStart: '2026-08-15', periodEnd: '2026-08-21', status: 'Approved', totalScore: 45, submittedAtUtc: null },
  ],
  calculatedAtUtc: '2026-08-24T10:00:00Z',
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
    expect(screen.getAllByText('3/4').length).toBeGreaterThan(0);
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
    // خالد بلا تقييم معتمَد ⇒ نصّ صريح لا رقم، ولا صفر مصطنع.
    const khaled = cardOf('خالد سالم');
    expect(within(khaled).getByText('لا تقييم')).toBeInTheDocument();
    expect(within(khaled).queryByText(pct(0))).not.toBeInTheDocument();
    // ريم درجتها صفر فعليّ ⇒ تظهر رقمًا لا غيابًا.
    const reem = cardOf('ريم ناصر');
    expect(within(reem).getByText(pct(0))).toBeInTheDocument();
    expect(within(reem).queryByText('لا تقييم')).not.toBeInTheDocument();
  });

  it('يُظهر جودة البيانات والتغطية ويستبعد ضعيف التغطية من الترتيب لا من العرض', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    expect(await screen.findByText('فهد العتيبي')).toBeInTheDocument();
    expect(screen.getAllByText('تغطية غير كافية').length).toBeGreaterThan(0);
    expect(screen.getByText('خارج الترتيب — تغطية غير كافية')).toBeInTheDocument();
    // شرط التغطية وعدد المستبعَدين يأتيان من الخادم لا من ثابت في الشاشة.
    expect(screen.getByText(/تغطية لا تقلّ عن 75٪ · مستبعَدون لضعف التغطية: 1/)).toBeInTheDocument();
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

describe('KpiOverview — المُرشِّح الموحّد', () => {
  beforeEach(mockOk);

  it('يرسل الكادنس صراحةً في كلّ طلب ولا يترك قيمة ضمنيّة', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    expect(paramsFor('/kpi/performance')[0]).toMatchObject({ cadence: 'WeeklyPulse', periodType: 'LastCompletedWeek' });
    expect(paramsFor('/kpi/rankings')[0]).toMatchObject({ cadence: 'WeeklyPulse' });
  });

  it('تغيير الكادنس يقود البطاقات والترتيب معًا (فصل النبض عن الربعيّ)', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    calls = [];
    fireEvent.change(screen.getByLabelText('الكادنس'), { target: { value: 'Quarterly' } });
    expect(await screen.findByText('متوسط مؤشر الشركة')).toBeInTheDocument();
    expect(paramsFor('/kpi/performance').at(-1)).toMatchObject({ cadence: 'Quarterly' });
    expect(paramsFor('/kpi/rankings').at(-1)).toMatchObject({ cadence: 'Quarterly' });
  });

  it('يرسل نوع الفترة ومفتاحها ولا يحسب حدودًا في الواجهة', async () => {
    renderPage();
    await screen.findByText('متوسط مؤشر الشركة');
    calls = [];
    fireEvent.change(screen.getByLabelText('نوع الفترة'), { target: { value: 'Week' } });
    fireEvent.change(screen.getByLabelText('مفتاح الفترة'), { target: { value: '2026-W20' } });
    expect(await screen.findByText('متوسط مؤشر الشركة')).toBeInTheDocument();
    const last = paramsFor('/kpi/performance').at(-1)!;
    expect(last).toMatchObject({ periodType: 'Week', periodKey: '2026-W20' });
    expect(last.from).toBeUndefined();
    expect(last.to).toBeUndefined();
  });

  it('يعرض حدود الفترة كما حلّها الخادم بتوقيت الرياض', async () => {
    renderPage();
    expect(await screen.findByText(/الأسبوع 33 · 2026-08-15 ← 2026-08-21 · Asia\/Riyadh/)).toBeInTheDocument();
  });
});

describe('KpiOverview — تفصيل الرقم', () => {
  beforeEach(mockOk);

  it('يعيد التفصيل إنتاج الرقم نفسه من صفوفه ضمن نفس الفترة والكادنس', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('تفصيل الأعضاء'));
    fireEvent.click((await screen.findAllByText('تفصيل الرقم'))[0]);
    expect(await screen.findByText(/2 تقييم معتمَد · المتوسّط المُعاد حسابه:/)).toBeInTheDocument();
    // الصفّان 85 و45 ظاهران، والمتوسّط 65 — لا طيّ إلى الأعلى.
    expect(screen.getByText(pct(85))).toBeInTheDocument();
    expect(screen.getByText(pct(45))).toBeInTheDocument();
    const drill = paramsFor('/kpi/drilldown').at(-1)!;
    expect(drill).toMatchObject({ subjectUserId: 'u-1', cadence: 'WeeklyPulse', periodType: 'LastCompletedWeek' });
  });
});

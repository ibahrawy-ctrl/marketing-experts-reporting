// ======================================================================
// OBS-R5-01 — «مساران متزامنان لا متبادلان» في الواجهة (قرار مالك المنتج)
//
// **العيب المُصلَح:** كانت الواجهة تستهلك حقلًا مسطّحًا واحدًا (`effectiveCadence`) فتعرض
// مسارًا واحدًا وتُخفي الآخر كلّيًّا. الإصلاح جعل العقد قائمة `tracks`، والإثبات هنا يقيس
// ما يراه المُقيّم فعلًا وما ترسله الواجهة فعلًا — لا ما يقوله الكود عن نفسه.
//
// يغطّي هذا الملفّ بنود القبول (1) و(4) و(5) و(6) و(7) و(9-واجهة) من قرار مالك المنتج.
// البنود الخادميّة (2،3،8،9،10،11،12) في `ObsR5OneDualTrackContractTests`.
//
// `api` وحده متجسَّس، وبقيّة الوحدات حقيقيّة ⟹ `postCalls` دليل مقيس لا محاكاة.
// ======================================================================

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { ToastProvider } from '../components/ActionResultToast';
import type { KpiEvaluationSetupDto, MyCyclesDto, ReportingCycleDto } from '../types/api';
import {
  CURRENT_QUARTER_KEY,
  CURRENT_WEEK_KEY,
  QUARTERLY_NOT_CONFIGURED_REASON,
  WEEKLY_NOT_CONFIGURED_REASON,
  blockedTrack,
  quarterlyTrack,
  weeklyTrack,
} from '../test/kpiTrackFixtures';

const DUAL_SUBJECT = '44444444-4444-4444-4444-444444444444';
const QUARTERLY_ONLY_SUBJECT = '55555555-5555-5555-5555-555555555555';
const WEEKLY_ONLY_SUBJECT = '66666666-6666-6666-6666-666666666666';

const WEEKLY_TEMPLATE = { id: 'tpl-pulse', name: 'النبض الأسبوعي العام' };
const QUARTERLY_TEMPLATE = { id: 'tpl-quarter', name: 'مؤشرات مندوب المبيعات' };

// موظّف واحد له المساران معًا: النبض من الإعداد العامّ، والربعيّ من مسمّاه الوظيفيّ.
// هذه هي الحالة التي كانت تُخفي النبض كلّيًّا قبل الإصلاح (الأخصّ كان يبتلع المسارين).
const DUAL_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: DUAL_SUBJECT,
  subjectName: 'مندوب مبيعات B2B',
  tracks: [
    weeklyTrack('generalTemplate', [WEEKLY_TEMPLATE]),
    quarterlyTrack('jobRole', [QUARTERLY_TEMPLATE]),
  ],
  isConfigured: true,
  blockingReason: null,
};

const QUARTERLY_ONLY_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: QUARTERLY_ONLY_SUBJECT,
  subjectName: 'موظّف ربعيّ فقط',
  tracks: [blockedTrack('WeeklyPulse'), quarterlyTrack('departmentAssignment', [QUARTERLY_TEMPLATE])],
  isConfigured: true,
  blockingReason: null,
};

const WEEKLY_ONLY_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: WEEKLY_ONLY_SUBJECT,
  subjectName: 'موظّف نبض فقط',
  tracks: [weeklyTrack('employeeAssignment', [WEEKLY_TEMPLATE]), blockedTrack('Quarterly')],
  isConfigured: true,
  blockingReason: null,
};

const SETUPS: Record<string, KpiEvaluationSetupDto> = {
  [DUAL_SUBJECT]: DUAL_SETUP,
  [QUARTERLY_ONLY_SUBJECT]: QUARTERLY_ONLY_SETUP,
  [WEEKLY_ONLY_SUBJECT]: WEEKLY_ONLY_SETUP,
};

// دورة أسبوعيّة واحدة «حاليّة» — منتقي التقويم يختارها تلقائيًّا، فمفتاح الأسبوع المُرسَل
// يأتي من الخادم لا من حساب محلّيّ في الواجهة.
const CURRENT_CYCLE: ReportingCycleDto = {
  cycleKey: CURRENT_WEEK_KEY,
  cycleNumber: 35,
  cycleYear: 2026,
  cycleStart: '2026-08-29',
  cycleEnd: '2026-09-04',
  tuesdayReference: '2026-09-01',
  cycleLabel: 'الأسبوع 35 لعام 2026',
  shortLabel: 'W35',
  dataCoverageStart: '2026-08-29',
  dataCoverageEnd: '2026-09-03',
  role: 'Employee',
  roleLabel: 'موظّف',
  roleDueOffset: 0,
  roleDueDate: '2026-09-01',
  roleDueDateLabel: 'الثلاثاء 1 سبتمبر',
  offset: 0,
  isCurrent: true,
  isPast: false,
  isFuture: false,
  status: 'current',
  isOpen: true,
  isLocked: false,
  lockReason: null,
  isOverdue: false,
  requiresReason: false,
  today: '2026-09-01',
  context: 'Kpi',
  unified: null,
};

const MY_CYCLES: MyCyclesDto = {
  context: 'Kpi',
  templateId: null,
  role: 'Employee',
  roleLabel: 'موظّف',
  currentCycleKey: CURRENT_WEEK_KEY,
  today: '2026-09-01',
  cycles: [CURRENT_CYCLE],
};

vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({
    user: { userId: 'u-manager', roles: ['Manager'] },
    hasAnyRole: () => true,
  }),
}));

import KpiPage from './KpiPage';

let postCalls: { url: string; body: unknown }[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  postCalls = [];

  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: Record<string, string> }) => {
    if (url === '/kpi-evaluations/evaluatable-subjects')
      return Promise.resolve({
        data: {
          isAdminOverride: false,
          subjects: Object.values(SETUPS).map((s) => ({ id: s.subjectUserId, fullName: s.subjectName })),
        },
      } as never);
    if (url === '/kpi-evaluations/effective-setup')
      return Promise.resolve({ data: SETUPS[config?.params?.subjectUserId ?? ''] } as never);
    if (url === '/reporting-calendar/my-cycles') return Promise.resolve({ data: MY_CYCLES } as never);
    if (url === '/kpi-evaluations/lookup')
      return Promise.resolve({ data: { found: false, evaluation: null } } as never);
    if (url === '/kpi-evaluations') return Promise.resolve({ data: [] } as never);
    return Promise.resolve({ data: [] } as never);
  });

  vi.spyOn(api, 'post').mockImplementation((url: string, body?: unknown) => {
    postCalls.push({ url, body });
    return Promise.resolve({ data: { id: 'created-evaluation' } } as never);
  });
});

async function renderEvaluationForm() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <MemoryRouter initialEntries={[`/app/kpi?subject=${DUAL_SUBJECT}`]}>
          <Routes>
            <Route path="/app/kpi" element={<KpiPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
  return await screen.findByLabelText('الموظف المراد تقييمه');
}

const weeklyCard = () => screen.getByTestId('kpi-track-WeeklyPulse');
const quarterlyCard = () => screen.getByTestId('kpi-track-Quarterly');

describe('OBS-R5-01 — الواجهة تعرض المسارين معًا ولا تخلط بينهما', () => {
  // ===== قبول (1): موظّف له أسبوعيّ + ربعيّ يرى المسارين معًا =====
  it('يرى المُقيّم المسارين معًا لموظّف واحد، كلٌّ بمصدر حسمه المستقلّ', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, DUAL_SUBJECT);

    await waitFor(() => expect(screen.getByTestId('kpi-track-WeeklyPulse')).toBeInTheDocument());
    expect(within(weeklyCard()).getByText('نبض الأسبوع')).toBeInTheDocument();
    expect(within(quarterlyCard()).getByText('التقييم الربعيّ الرسميّ')).toBeInTheDocument();

    // سلّم الأولويّة طُبِّق داخل كلّ مسار على حدة ⟹ مصدران مختلفان معروضان معًا،
    // ولا يُخفي فوزُ «المسمّى الوظيفيّ» في الربعيّ مسارَ النبض المحسوم من «الإعداد العامّ».
    expect(within(weeklyCard()).getByText(/يحدّده النظام من الإعداد العامّ/)).toBeInTheDocument();
    expect(within(quarterlyCard()).getByText(/يحدّده النظام من مسمّاه الوظيفيّ/)).toBeInTheDocument();

    // الإجراءان مستحقّان كلاهما — لا أحدهما معطَّل بسبب الآخر.
    expect(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'إجراء التقييم الربعي الرسمي' })).toBeEnabled();
  });

  // ===== قبول (4): غياب قالب أسبوعيّ لا يكسر المسار الربعيّ =====
  it('غياب مسار النبض يُعلَن بسببه وحده ولا يمنع إنشاء التقييم الربعيّ', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, QUARTERLY_ONLY_SUBJECT);

    await waitFor(() => expect(screen.getByText(WEEKLY_NOT_CONFIGURED_REASON)).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' })).toBeDisabled();
    // والربعيّ سليم تمامًا: مسار نشط تلقائيًّا وقالب مُنتقى وزرّ إنشاء مفعَّل.
    expect(screen.getByRole('button', { name: 'إجراء التقييم الربعي الرسمي' })).toBeEnabled();
    expect(await screen.findByText('الفترة (الربع الجاري)')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إنشاء تقييم' })).toBeEnabled();
  });

  // ===== قبول (5): غياب قالب ربعيّ لا يكسر مسار النبض =====
  it('غياب المسار الربعيّ يُعلَن بسببه وحده ولا يمنع تسجيل نبض الأسبوع', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, WEEKLY_ONLY_SUBJECT);

    await waitFor(() => expect(screen.getByText(QUARTERLY_NOT_CONFIGURED_REASON)).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'إجراء التقييم الربعي الرسمي' })).toBeDisabled();
    // والنبض سليم: منتقي الدورة الأسبوعيّة ظاهر وزرّ الإنشاء مفعَّل.
    expect(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' })).toBeEnabled();
    expect(await screen.findByText('الفترة (أسبوع)')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'إنشاء تقييم' })).toBeEnabled());
  });

  // ===== قبول (6): إنشاء نبض أسبوعيّ يرسل مفتاح الأسبوع ونوعه =====
  it('يرسل إجراء نبض الأسبوع نوع فترة Weekly ومفتاح الأسبوع وقالب مسار النبض', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, DUAL_SUBJECT);

    await userEvent.click(await screen.findByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' }));
    expect(await screen.findByText('الفترة (أسبوع)')).toBeInTheDocument();

    const create = await screen.findByRole('button', { name: 'إنشاء تقييم' });
    await waitFor(() => expect(create).toBeEnabled());
    await userEvent.click(create);

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].body).toEqual({
      kpiTemplateId: WEEKLY_TEMPLATE.id,
      subjectUserId: DUAL_SUBJECT,
      periodType: 'Weekly',
      periodKey: CURRENT_WEEK_KEY,
    });
  });

  // ===== قبول (7): إنشاء تقييم ربعيّ يرسل مفتاح الربع ونوعه =====
  it('يرسل إجراء التقييم الربعيّ نوع فترة Quarterly ومفتاح الربع وقالب المسار الربعيّ', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, DUAL_SUBJECT);

    await userEvent.click(await screen.findByRole('button', { name: 'إجراء التقييم الربعي الرسمي' }));
    expect(await screen.findByText('الفترة (الربع الجاري)')).toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: 'إنشاء تقييم' }));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].body).toEqual({
      kpiTemplateId: QUARTERLY_TEMPLATE.id,
      subjectUserId: DUAL_SUBJECT,
      periodType: 'Quarterly',
      periodKey: CURRENT_QUARTER_KEY,
    });
  });

  // ===== قبول (9-واجهة): لا خلط بين المسارين في القوالب ولا في الفترة =====
  it('لا تخلط قائمة القوالب ولا الفترة بين المسارين عند التنقّل بينهما', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, DUAL_SUBJECT);

    await userEvent.click(await screen.findByRole('button', { name: 'إجراء التقييم الربعي الرسمي' }));
    const templateSelect = screen.getByLabelText('قالب التقييم') as HTMLSelectElement;
    expect(within(templateSelect).getByRole('option', { name: QUARTERLY_TEMPLATE.name })).toBeInTheDocument();
    expect(within(templateSelect).queryByRole('option', { name: WEEKLY_TEMPLATE.name })).not.toBeInTheDocument();
    expect(screen.queryByText('الفترة (أسبوع)')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' }));
    expect(within(templateSelect).getByRole('option', { name: WEEKLY_TEMPLATE.name })).toBeInTheDocument();
    expect(within(templateSelect).queryByRole('option', { name: QUARTERLY_TEMPLATE.name })).not.toBeInTheDocument();
    expect(screen.queryByText('الفترة (الربع الجاري)')).not.toBeInTheDocument();
  });
});

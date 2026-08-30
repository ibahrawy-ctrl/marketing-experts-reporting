// ======================================================================
// DEF-R5-001 — رحلة إنشاء التقييم في الواجهة (قرار مالك المنتج، R5)
//
// **لماذا اختبار واجهة إضافةً إلى اختبارات التكامل؟** الخادم مُثبَت بالفعل أنّه الحاسم
// النهائيّ (DefR5OneEvaluationSetupContractTests)، لكنّ العيب المُبلَّغ عنه كان في الواجهة:
// تثبيت `WeeklyPulse` نصًّا داخل نداء الإنشاء. إثبات إزالته لا يكون إلّا بقياس ما تُرسله
// الواجهة فعلًا وما تعرضه للمستخدم — لا بقراءة الكود.
//
// `api` وحده متجسَّس، وبقيّة الوحدات حقيقيّة (الموجّه، TanStack Query، مكوّنات الواجهة)
// ⟹ سجلّ النداءات (`postCalls`) دليل مقيس لا محاكاة.
// ======================================================================

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { ToastProvider } from '../components/ActionResultToast';
import type { KpiEvaluationSetupDto, KpiEvaluationTrackDto } from '../types/api';
import { blockedTrack, quarterlyTrack, weeklyTrack } from '../test/kpiTrackFixtures';

const QUARTERLY_SUBJECT = '11111111-1111-1111-1111-111111111111';
const WEEKLY_SUBJECT = '22222222-2222-2222-2222-222222222222';
const UNCONFIGURED_SUBJECT = '33333333-3333-3333-3333-333333333333';

const tracksOf = (...rows: KpiEvaluationTrackDto[]) => rows;

const QUARTERLY_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: QUARTERLY_SUBJECT,
  subjectName: 'سارة الربعيّة',
  tracks: tracksOf(
    blockedTrack('WeeklyPulse'),
    quarterlyTrack('jobRole', [{ id: 'tpl-quarterly', name: 'قالب المسمّى الربعيّ' }]),
  ),
  isConfigured: true,
  blockingReason: null,
};

const WEEKLY_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: WEEKLY_SUBJECT,
  subjectName: 'خالد الأسبوعيّ',
  tracks: tracksOf(
    weeklyTrack('teamAssignment', [{ id: 'tpl-weekly', name: 'قالب نبض الفريق' }]),
    blockedTrack('Quarterly'),
  ),
  isConfigured: true,
  blockingReason: null,
};

const NO_TRACK_REASON =
  'لا يوجد إعداد KPI فعّال لهذا الموظّف على أيّ مسار: لا قالب نبض أسبوعيّ ولا قالب ربعيّ رسميّ مُسنَد له.';

const UNCONFIGURED_SETUP: KpiEvaluationSetupDto = {
  subjectUserId: UNCONFIGURED_SUBJECT,
  subjectName: 'نورة غير المهيّأة',
  tracks: tracksOf(blockedTrack('WeeklyPulse'), blockedTrack('Quarterly')),
  isConfigured: false,
  blockingReason: NO_TRACK_REASON,
};

const SETUPS: Record<string, KpiEvaluationSetupDto> = {
  [QUARTERLY_SUBJECT]: QUARTERLY_SETUP,
  [WEEKLY_SUBJECT]: WEEKLY_SETUP,
  [UNCONFIGURED_SUBJECT]: UNCONFIGURED_SETUP,
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
          subjects: [
            { id: QUARTERLY_SUBJECT, fullName: QUARTERLY_SETUP.subjectName },
            { id: WEEKLY_SUBJECT, fullName: WEEKLY_SETUP.subjectName },
            { id: UNCONFIGURED_SUBJECT, fullName: UNCONFIGURED_SETUP.subjectName },
          ],
        },
      } as never);
    if (url === '/kpi-evaluations/effective-setup')
      return Promise.resolve({ data: SETUPS[config?.params?.subjectUserId ?? ''] } as never);
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

// ندخل من مسار «تقييمات موظّف بعينه» (الرابط القادم من صفحة الموظّف) لأنّه يعرض نموذج الإنشاء
// نفسه بلا لوحة «النظرة الشاملة» — فالمقيس هنا رحلة الإنشاء وحدها لا تجميعات الأداء.
async function renderEvaluationForm() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <MemoryRouter initialEntries={[`/app/kpi?subject=${QUARTERLY_SUBJECT}`]}>
          <Routes>
            <Route path="/app/kpi" element={<KpiPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
  return await screen.findByLabelText('الموظف المراد تقييمه');
}

describe('DEF-R5-001 — الواجهة تعرض المسار المحسوم خادميًّا ولا تختاره', () => {
  // ===== 1: لا منتقي تقنيّ للتواتر بأيّ حال =====
  it('لا تعرض الواجهة قائمة اختيار للتواتر — المساران يُعرضان كإجراءات مستحقّة', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, QUARTERLY_SUBJECT);
    await screen.findByText('التقييم الربعيّ الرسميّ');

    // الحقول القابلة للاختيار هي «الموظّف» و«القالب» فقط — لا حقل ثالث للتواتر.
    const selects = screen.getAllByRole('combobox');
    expect(selects).toHaveLength(2);
    // OBS-R5-01/4 — بديل المنتقي التقنيّ إجراءان مستحقّان بنصّ الأعمال لا بنصّ التواتر.
    expect(screen.getByRole('button', { name: 'إجراء التقييم الربعي الرسمي' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' })).toBeDisabled();
    // ومصدر الحسم معروض شرحًا لا خيارًا.
    expect(screen.getByText(/يحدّده النظام من مسمّاه الوظيفيّ — لا يُختار يدويًّا/)).toBeInTheDocument();
  });

  // ===== 2: المسار الربعيّ الرسميّ — الفترة والنوع من الخادم =====
  it('ترسل الواجهة نوع الفترة ومفتاحها كما أعلنهما الخادم في المسار الربعيّ', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, QUARTERLY_SUBJECT);
    // الربع الجاري محسوم خادميًّا ويُعرض بلا منتقي (DEC-01/1).
    expect(await screen.findByText('الفترة (الربع الجاري)')).toBeInTheDocument();
    // يظهر مرّتين: في بطاقة المسار الربعيّ وفي حقل الفترة المحسوم — كلاهما من إعلان الخادم.
    expect(screen.getAllByText('2026-Q3').length).toBeGreaterThan(0);

    await userEvent.click(await screen.findByRole('button', { name: 'إنشاء تقييم' }));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].url).toBe('/kpi-evaluations');
    expect(postCalls[0].body).toEqual({
      kpiTemplateId: 'tpl-quarterly',
      subjectUserId: QUARTERLY_SUBJECT,
      periodType: 'Quarterly',
      periodKey: '2026-Q3',
    });
  });

  // ===== 3: مسار نبض الأسبوع — من إعداد الموظّف نفسه لا من ثابت في الواجهة =====
  it('تنتقل الواجهة إلى مسار نبض الأسبوع حين يعلنه الخادم لموظّف آخر', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, WEEKLY_SUBJECT);

    await screen.findByText('نبض الأسبوع');
    expect(screen.getByText(/يحدّده النظام من إسناد فريقه — لا يُختار يدويًّا/)).toBeInTheDocument();
    // نوع الفترة تبع المسار: منتقي الدورة الأسبوعيّة يظهر، وبادج الربع لا يظهر.
    expect(screen.getByText('الفترة (أسبوع)')).toBeInTheDocument();
    expect(screen.queryByText('الفترة (الربع الجاري)')).not.toBeInTheDocument();
  });

  // ===== 4: غياب الإعداد حالة مسمّاة — ولا يُرسَل طلب غير صالح =====
  it('تعلن الواجهة سبب المنع ولا ترسل أيّ طلب إنشاء حين لا يوجد تواتر فعّال', async () => {
    const subjectSelect = await renderEvaluationForm();
    await userEvent.selectOptions(subjectSelect, UNCONFIGURED_SUBJECT);

    expect(await screen.findByText(UNCONFIGURED_SETUP.blockingReason!)).toBeInTheDocument();
    // المساران معًا معلَنان «غير مُهيّأ» — لا أحدهما صامت بسبب الآخر.
    expect(screen.getAllByText('غير مُهيّأ')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'تسجيل نبض الأسبوع الحالي' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'إجراء التقييم الربعي الرسمي' })).toBeDisabled();
    // الزرّ معطَّل، ومحاولة النقر لا تُنتج نداءً — لا طلب غير صالح يصل الخادم أصلًا.
    const create = screen.getByRole('button', { name: 'إنشاء تقييم' });
    expect(create).toBeDisabled();
    await userEvent.click(create);
    expect(postCalls).toHaveLength(0);
  });
});

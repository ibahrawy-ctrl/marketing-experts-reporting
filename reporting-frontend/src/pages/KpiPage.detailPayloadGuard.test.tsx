// R6.5.3 — حارس حدّ البيانات في تفاصيل تقييم KPI.
//
// العيب: الحارس الوحيد في `KpiDetail` كان `if (isError || !ev)` — وهو يفحص **الصدق** لا **الشكل**.
// أيّ استجابة 200 صادقة لكن مخالفة للعقد (مصفوفة، أو كائن ناقص `results`) تعبره سالمة ثمّ تنهار
// بعدها عند `ev.results.map(...)` برمية `TypeError: Cannot read properties of undefined (reading 'map')`
// تصعد إلى React بلا حدّ خطأ، فتُسقط الشجرة كلّها وتظهر في Vitest بوصفها Unhandled Error.
//
// العقد على الجانبين يوجب مصفوفة دائمًا:
//   الخادم: `IReadOnlyList<KpiResultDto> Results` — معامل موضعيّ غير قابل للإسقاط (KpiModels.cs).
//   الواجهة: `results: KpiResultDto[]` — حقل مطلوب غير اختياريّ (types/api.ts).
// ولذلك تُعامَل الحمولة المخالفة **خطأَ تحميل** لا «لا نتائج»، ولا تُحوَّل إلى مصفوفة فارغة —
// إذ إنّ تحويلها كذلك يقلب فشلًا حقيقيًّا إلى صمت. وفي المقابل `results: []` المشروعة تبقى
// «لا نتائج» وتُصيَّر جدولًا فارغًا، فلا يُقلَب أيٌّ من المعنيين إلى الآخر.
//
// القياس هنا بحدّ خطأ صريح لا بترصّد أحداث عامّة: الرمية تُلتقَط لحظة وقوعها في التصيير نفسه،
// فالاختبار حتميّ بلا انتظار ولا إعادة محاولة ولا كتم لأخطاء الطرفيّة.
import { Component, type ReactNode } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { ToastProvider } from '../components/ActionResultToast';

vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({ user: { userId: 'u-manager', roles: ['Manager'] }, hasAnyRole: () => true }),
}));

import KpiPage from './KpiPage';

const EVAL_ID = 'eval-payload-guard';

// حدّ خطأ يلتقط أيّ رمية تصيير بدل أن تصعد غير مُعالَجة، فيتحوّل العيب من «Unhandled Error»
// متقطّع إلى فشل اختبار حتميّ يذكر نصّ الرمية.
let captured: Error | null = null;
class CaptureBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state: { error: Error | null } = { error: null };
  static getDerivedStateFromError(error: Error) {
    captured = error;
    return { error };
  }
  render() {
    return this.state.error ? <div data-testid="render-crash">{this.state.error.message}</div> : this.props.children;
  }
}

const VALID_RESULT = {
  kpiMetricId: 'm-1',
  metricName: 'إنجاز المهامّ',
  weight: 100,
  targetValue: 10,
  rawValue: 8,
  score: 80,
  note: null,
};

function evaluationPayload(results: unknown) {
  return {
    id: EVAL_ID,
    kpiTemplateVersionId: 'tv-1',
    templateTitle: 'قالب تجريبيّ',
    cadence: 'WeeklyPulse',
    subjectUserId: 'u-emp',
    subjectName: 'موظّف',
    evaluatorId: null,
    evaluatorName: null,
    teamId: null,
    departmentId: null,
    periodType: 'Week',
    periodKey: '2026-W37',
    status: 'Draft',
    totalScore: null,
    trend: 'Flat',
    isBelowTarget: false,
    submittedAtUtc: null,
    canEdit: false,
    results,
    reviewerId: null,
    reviewerName: null,
    reviewedAtUtc: null,
    reviewNote: null,
    canReview: false,
    canFlag: false,
    canAdminDelete: false,
    canReopen: false,
  };
}

// يردّ حمولة التفاصيل المطلوبة، وأحداث المراجعة مصفوفة فارغة، وأيّ شيء آخر مصفوفة فارغة.
function mockDetail(detail: () => Promise<unknown>) {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === `/kpi-evaluations/${EVAL_ID}`) return detail() as never;
    return Promise.resolve({ data: [] } as never);
  });
}

function renderDetail() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <CaptureBoundary>
          <MemoryRouter initialEntries={[`/app/kpi?open=${EVAL_ID}`]}>
            <Routes>
              <Route path="/app/kpi" element={<KpiPage />} />
            </Routes>
          </MemoryRouter>
        </CaptureBoundary>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
  captured = null;
});

describe('KpiDetail — حدّ بيانات تفاصيل التقييم (R6.5.3)', () => {
  it('حمولة 200 مخالفة للعقد (مصفوفة بدل كائن) لا تُسقط الصفحة وتظهر حالة تعذّر التحميل', async () => {
    // هذه هي الحمولة نفسها التي كانت تُفجّر العيب: مصفوفة صادقة تعبر `!ev`.
    mockDetail(() => Promise.resolve({ data: [] }));
    renderDetail();

    await waitFor(() => expect(screen.getByText('تعذّر تحميل التقييم')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();

    // لم تقع رمية تصيير أصلًا — ولا `.map` على `undefined`.
    expect(screen.queryByTestId('render-crash')).toBeNull();
    expect(captured).toBeNull();
  });

  it('كائن ناقص الحقل results لا يُسقط الصفحة ولا يُعرَض بوصفه «لا نتائج»', async () => {
    const { results: _omitted, ...withoutResults } = evaluationPayload([]);
    mockDetail(() => Promise.resolve({ data: withoutResults }));
    renderDetail();

    await waitFor(() => expect(screen.getByText('تعذّر تحميل التقييم')).toBeInTheDocument());
    expect(captured).toBeNull();
    // حارس عدم القلب: نقصُ العقد ليس «تقييمًا بلا مؤشّرات»، فلا يُصيَّر عنوان التقييم.
    expect(screen.queryByText('قالب تجريبيّ')).toBeNull();
  });

  it('results بقيمة غير مصفوفة (null) تُعامَل خطأ تحميل لا قائمة فارغة', async () => {
    mockDetail(() => Promise.resolve({ data: evaluationPayload(null) }));
    renderDetail();

    await waitFor(() => expect(screen.getByText('تعذّر تحميل التقييم')).toBeInTheDocument());
    expect(captured).toBeNull();
  });

  it('مصفوفة نتائج صحيحة تُعرَض كما هي بلا تغيير في السلوك', async () => {
    mockDetail(() => Promise.resolve({ data: evaluationPayload([VALID_RESULT]) }));
    renderDetail();

    await waitFor(() => expect(screen.getByText('قالب تجريبيّ')).toBeInTheDocument());
    expect(screen.getByText('إنجاز المهامّ')).toBeInTheDocument();
    expect(screen.queryByText('تعذّر تحميل التقييم')).toBeNull();
    expect(captured).toBeNull();
  });

  it('results = [] المشروعة تبقى «لا نتائج»: التقييم يُعرَض بجدول بلا صفوف ولا تُعلَن حالة خطأ', async () => {
    mockDetail(() => Promise.resolve({ data: evaluationPayload([]) }));
    renderDetail();

    await waitFor(() => expect(screen.getByText('قالب تجريبيّ')).toBeInTheDocument());
    expect(screen.queryByText('تعذّر تحميل التقييم')).toBeNull();
    const table = screen.getByText('المؤشر').closest('table');
    expect(table).not.toBeNull();
    expect(table!.querySelectorAll('tbody tr')).toHaveLength(0);
    expect(captured).toBeNull();
  });

  it('فشل الطلب الحقيقيّ يظل ظاهرًا في واجهة الخطأ المخصّصة ولا يُبتلَع', async () => {
    mockDetail(() => Promise.reject(new Error('boom')));
    renderDetail();

    await waitFor(() => expect(screen.getByText('تعذّر تحميل التقييم')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
    expect(captured).toBeNull();
  });
});

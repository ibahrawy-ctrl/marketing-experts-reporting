// ======================================================================
// P2-HR-010 — اختبارات لوحة قائمة خدمة الموظّف والالتزام.
//
// **لماذا `api` متجسَّس لا هوك مموَّه؟** الادّعاءات هنا عن العقد نفسه: أيّ مسار يُنادى،
// وأيّ بند يُرسَم، وأيّ بند لا يُتاح تحريره. تمويه الهوك كان سيخفي بالضبط ما نقيسه.
//
// وأهمّها ادّعاء بنيويّ: **البند المحسوب لا عنصر تحرير له إطلاقًا** — فالتصحيح موضعه
// المصدر، والكتابة هنا كانت ستُنشئ نسخة تناقض مصدرها.
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { ChecklistItem, EmployeeChecklist } from '../types/checklist';
import { FEATURES } from '../lib/navConfig';
import { EmployeeChecklistPanel } from './EmployeeChecklistPanel';

// P123-R1 — الميزة مفتوحة في هذه الكتلة عمدًا: الادّعاءات هنا عن العقد لا عن بوّابة الميزة،
// وهذه الأخيرة تُقاس مستقلّةً في `surfaceState.test.ts` و`navSecurity.test.ts`.
vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({ features: new Set<string>(Object.values(FEATURES)) }),
}));

const SUBJECT = '33333333-3333-3333-3333-333333333333';

let getCalls: string[] = [];
let putCalls: { url: string; payload: unknown }[] = [];
let body: EmployeeChecklist;

function item(over: Partial<ChecklistItem> & { key: string; titleAr: string }): ChecklistItem {
  return {
    groupAr: 'الالتزام التشغيليّ',
    source: 'Computed',
    status: 'NotStarted',
    statusLabelAr: 'بند مفتوح',
    openCount: 1,
    ownerUserId: null,
    ownerFullName: null,
    dueDate: null,
    lastActionAtUtc: null,
    evidenceReference: null,
    sourceKind: null,
    sourceLink: null,
    requiresMyAction: false,
    ...over,
  };
}

function fixture(): EmployeeChecklist {
  return {
    subjectUserId: SUBJECT,
    isSelf: false,
    viewerRelation: 'DirectTeam',
    summary: {
      applicable: 3,
      completed: 1,
      open: 2,
      notApplicable: 1,
      requiresMyAction: 1,
      completionRatio: 0.3333,
    },
    items: [
      item({
        key: 'reports-obligations',
        titleAr: 'التقارير الدوريّة المطلوبة',
        openCount: 2,
        statusLabelAr: '2 بندًا مفتوحًا',
        requiresMyAction: true,
        sourceKind: 'ObligationsService',
        sourceLink: '/app/reports',
      }),
      item({
        key: 'kpi-obligations',
        titleAr: 'تقييمات الأداء المطلوبة',
        // «غير منطبق» ليس صفرًا: لا إسناد أصلًا ⇒ لا يُقرأ إنجازًا.
        status: 'NotApplicable',
        statusLabelAr: 'غير منطبق',
        openCount: 0,
      }),
      item({
        key: 'onboarding-orientation',
        titleAr: 'إتمام التهيئة التعريفيّة',
        groupAr: 'التهيئة',
        source: 'Manual',
        status: 'Completed',
        statusLabelAr: 'مكتمل',
        openCount: 0,
        ownerFullName: 'منى الحربي',
        dueDate: '2026-08-30',
        evidenceReference: 'محضر التهيئة رقم ١',
      }),
    ],
  };
}

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  putCalls = [];
  body = fixture();
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    getCalls.push(url);
    return Promise.resolve({ data: body } as never);
  });
  vi.spyOn(api, 'put').mockImplementation((url: string, payload?: unknown) => {
    putCalls.push({ url, payload });
    return Promise.resolve({ data: {} } as never);
  });
});

function renderPanel(subject = SUBJECT) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter>
      <QueryClientProvider client={qc}>
        <EmployeeChecklistPanel subject={subject} />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

const card = (title: string) => screen.getByText(title).closest('li') as HTMLElement;

describe('EmployeeChecklistPanel — العقد مع الخادم', () => {
  it('ينادي مسار القائمة بمعرّف الموظّف', async () => {
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');
    expect(getCalls).toEqual([`/employees/${SUBJECT}/checklist`]);
  });

  it('وضع الذات ينادي مسار me الخادميّ ولا يشتقّ معرّفًا في المتصفّح', async () => {
    renderPanel('me');
    await screen.findByText('التقارير الدوريّة المطلوبة');
    expect(getCalls).toEqual(['/employees/me/checklist']);
  });

  it('يرسم البنود الواصلة وحدها ولا يخترع بندًا من كتالوج محلّيّ', async () => {
    body = { ...fixture(), items: [fixture().items[0]] };
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');

    expect(screen.queryByText('تقييمات الأداء المطلوبة')).toBeNull();
    expect(screen.queryByText('إتمام التهيئة التعريفيّة')).toBeNull();
    // ولا حتّى مفاتيح البنود المحجوبة تظهر في الشجرة
    expect(document.body.innerHTML).not.toContain('employment-contract-signed');
  });

  it('يعرض حالة تحميل مستقلّة لا تُسقِط بقيّة الملفّ', () => {
    vi.spyOn(api, 'get').mockImplementation(() => new Promise(() => {}) as never);
    renderPanel();
    expect(screen.getByRole('status', { name: 'جارٍ تحميل قائمة الالتزام' })).toBeInTheDocument();
  });

  it('يعرض خطأً قابلًا لإعادة المحاولة حين يمنع الخادم القائمة', async () => {
    vi.spyOn(api, 'get').mockRejectedValue(new Error('404'));
    renderPanel();
    expect(await screen.findByText('تعذّر تحميل قائمة الالتزام')).toBeInTheDocument();
  });

  it('حالة فارغة تُصرّح بأنّ الغياب ليس خلوًّا من البنود', async () => {
    body = { ...fixture(), items: [] };
    renderPanel();
    expect(await screen.findByText('لا توجد بنود متاحة لك')).toBeInTheDocument();
  });
});

describe('EmployeeChecklistPanel — التمييز بين المحسوب واليدويّ', () => {
  it('البند المحسوب بلا أيّ عنصر تحرير — لا حقل ولا زرّ حفظ', async () => {
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');

    const computed = card('التقارير الدوريّة المطلوبة');
    expect(within(computed).getByText('محسوب من مصدره')).toBeInTheDocument();
    expect(within(computed).queryByRole('combobox')).toBeNull();
    expect(within(computed).queryByRole('button', { name: 'حفظ البند' })).toBeNull();
  });

  it('البند اليدويّ وحده يحمل محرّرًا، ويُرسِل مساره ومفتاحه بدقّة', async () => {
    renderPanel();
    await screen.findByText('إتمام التهيئة التعريفيّة');

    const manual = card('إتمام التهيئة التعريفيّة');
    expect(within(manual).getByText('يدويّ')).toBeInTheDocument();

    fireEvent.change(within(manual).getByLabelText('الحالة'), { target: { value: 'InProgress' } });
    fireEvent.click(within(manual).getByRole('button', { name: 'حفظ البند' }));

    await waitFor(() => expect(putCalls).toHaveLength(1));
    expect(putCalls[0].url).toBe(`/employees/${SUBJECT}/checklist/onboarding-orientation`);
    expect(putCalls[0].payload).toEqual({ status: 'InProgress' });
  });

  it('وضع الذات يحرّر بمعرّف الموضوع الذي حسمه الخادم لا بالسلسلة me', async () => {
    renderPanel('me');
    await screen.findByText('إتمام التهيئة التعريفيّة');

    const manual = card('إتمام التهيئة التعريفيّة');
    fireEvent.click(within(manual).getByRole('button', { name: 'حفظ البند' }));

    await waitFor(() => expect(putCalls).toHaveLength(1));
    expect(putCalls[0].url).toBe(`/employees/${SUBJECT}/checklist/onboarding-orientation`);
  });

  it('رفض الخادم للتحرير يُعرَض برسالته لا بإخفاء الزرّ', async () => {
    vi.spyOn(api, 'put').mockRejectedValue({
      response: { status: 403, data: { message: 'لا تملك صلاحيّة تحرير بنود قائمة الالتزام.' } },
    });
    renderPanel();
    await screen.findByText('إتمام التهيئة التعريفيّة');

    const manual = card('إتمام التهيئة التعريفيّة');
    fireEvent.click(within(manual).getByRole('button', { name: 'حفظ البند' }));

    expect(await within(manual).findByRole('alert')).toHaveTextContent(
      'لا تملك صلاحيّة تحرير بنود قائمة الالتزام.',
    );
  });

  it('تعارض التزامن يُعرَض بوصفه تعارضًا لا خطأ إدخال', async () => {
    vi.spyOn(api, 'put').mockRejectedValue({ response: { status: 409, data: {} } });
    renderPanel();
    await screen.findByText('إتمام التهيئة التعريفيّة');

    const manual = card('إتمام التهيئة التعريفيّة');
    fireEvent.click(within(manual).getByRole('button', { name: 'حفظ البند' }));

    expect(await within(manual).findByRole('alert')).toHaveTextContent('تغيّر البند منذ آخر قراءة');
  });
});

describe('EmployeeChecklistPanel — «غير منطبق» ليس «صفر»', () => {
  it('البند غير المنطبق لا يُعرَض بعدّاد صفر', async () => {
    renderPanel();
    await screen.findByText('تقييمات الأداء المطلوبة');

    const na = card('تقييمات الأداء المطلوبة');
    expect(within(na).getByText('غير منطبق')).toBeInTheDocument();
    expect(within(na).queryByText('بنود مفتوحة')).toBeNull();
  });

  it('الملخّص يفصل غير المنطبق عن المكتمل ويعرض النسبة كما حسبها الخادم', async () => {
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');

    const label = (name: string) => screen.getByText(name).closest('div') as HTMLElement;
    expect(within(label('بنود منطبقة')).getByText('3')).toBeInTheDocument();
    expect(within(label('غير منطبقة')).getByText('1')).toBeInTheDocument();
    expect(within(label('نسبة الالتزام')).getByText('33%')).toBeInTheDocument();
  });

  it('يُبرِز ما يلزم المستخدمَ الحاليَّ فعلُه كما حسمه الخادم', async () => {
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');

    expect(screen.getByText('عليك إجراء في 1 بندًا.')).toBeInTheDocument();
    expect(within(card('التقارير الدوريّة المطلوبة')).getByText('يلزمك إجراء')).toBeInTheDocument();
    expect(within(card('إتمام التهيئة التعريفيّة')).queryByText('يلزمك إجراء')).toBeNull();
  });
});

describe('EmployeeChecklistPanel — الرابط إلى المصدر', () => {
  it('يعرض رابط المصدر حين يرسله الخادم وحده', async () => {
    renderPanel();
    await screen.findByText('التقارير الدوريّة المطلوبة');

    const withLink = within(card('التقارير الدوريّة المطلوبة')).getByRole('link');
    expect(withLink).toHaveAttribute('href', '/app/reports');
    expect(within(card('إتمام التهيئة التعريفيّة')).queryByRole('link')).toBeNull();
  });
});

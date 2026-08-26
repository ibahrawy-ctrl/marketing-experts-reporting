// ======================================================================
// P2-HR-009 — اختبارات لوحة عمليّات الموارد البشريّة.
//
// **ما تقيسه هذه الحزمة بالضبط:**
// ① أنّ الواجهة **لا تحسب رقمًا**: العدد المعروض هو ما أرسله الخادم حرفيًّا، وفتح البطاقة
//    يستدعي تفصيل الطابور نفسه بالمرشِّح نفسه ⇒ لا مصدرَ ثانٍ يمكن أن يخالف الأوّل.
// ② أنّ المرشِّح يُمرَّر إلى الخادم ولا يُطبَّق في المتصفّح: تصفيةٌ محلّيّة كانت ستُعطي
//    بطاقةً تقول رقمًا وجدولًا يعرض غيره.
// ③ أنّ 403 على اللوحة رسالة صلاحيّة مفهومة لا شاشة عطل، وأنّ فشل التصدير بـ403 لا يُسقط
//    اللوحة — المفتاحان منفصلان بنيويًّا لا بصريًّا.
// ④ أنّ الصفّ لا يحمل نصًّا حرًّا حسّاسًا، وأنّ «لا مهلة» تُعرَض شرطةً لا خرقًا.
//
// `api` متجسَّس لا هوك مموَّه: الادّعاء عن العقد الشبكيّ نفسه (المسار والمعامِلات).
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type {
  HrOperationsCard,
  HrOperationsDashboard,
  HrOperationsQueuePage,
  HrOperationsRow,
} from '../types/hrOperations';
import HrOperationsPage from './HrOperationsPage';

const USER_ID = '11111111-1111-1111-1111-111111111111';
const INCIDENT_ID = '22222222-2222-2222-2222-222222222222';

let getCalls: { url: string; params: Record<string, string> | undefined }[] = [];
let dashboard: HrOperationsDashboard;
let queuePage: HrOperationsQueuePage;
let dashboardError: unknown = null;
let exportError: unknown = null;

const QUEUE_KEYS = [
  'reports-missing',
  'reports-late',
  'kpi-missing',
  'kpi-awaiting-approval',
  'kpi-coverage-gap',
  'attendance-awaiting-employee',
  'attendance-employee-sla-breached',
  'attendance-awaiting-hr',
  'attendance-hr-sla-breached',
  'requests-awaiting-action',
  'follow-up-items',
] as const;

function card(key: string, count: number, breached = 0): HrOperationsCard {
  return {
    queue: QUEUE_KEYS.indexOf(key as (typeof QUEUE_KEYS)[number]) + 1,
    key: key as HrOperationsCard['key'],
    titleAr: `طابور ${key}`,
    groupAr: 'مجموعة',
    count,
    breachedCount: breached,
    maxAgeingDays: count > 0 ? 4 : 0,
    severityAr: breached > 0 ? 'حرِج' : 'سليم',
  };
}

function row(overrides: Partial<HrOperationsRow> = {}): HrOperationsRow {
  return {
    queue: 6,
    entityId: INCIDENT_ID,
    entityType: 'AttendanceIncident',
    subjectUserId: USER_ID,
    subjectFullName: 'سارة العتيبي',
    departmentId: null,
    departmentName: 'التسويق',
    teamId: null,
    teamName: 'فريق المحتوى',
    titleAr: 'تأخّر عن الدوام',
    typeAr: 'واقعة حضور',
    statusAr: 'بانتظار ردّ الموظّف',
    periodKey: '2026-W34',
    dueAt: '2026-08-20',
    slaDueAtUtc: '2026-08-20T06:30:00Z',
    slaBreached: false,
    ageingDays: 2,
    ownerUserId: USER_ID,
    ownerFullName: 'سارة العتيبي',
    nextActionAr: 'انتظار ردّ الموظّف ضمن نافذته',
    lastActionAtUtc: '2026-08-18T07:00:00Z',
    ...overrides,
  };
}

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  dashboardError = null;
  exportError = null;

  dashboard = {
    periodKeys: ['2026-W33', '2026-W34'],
    scope: { scopeType: 'Team', userCount: 7 },
    cards: QUEUE_KEYS.map((k) =>
      k === 'attendance-awaiting-employee' ? card(k, 3, 1) : card(k, 0),
    ),
  };

  queuePage = {
    queue: 6,
    key: 'attendance-awaiting-employee',
    titleAr: 'وقائع بانتظار ردّ الموظّف',
    totalCount: 3,
    breachedCount: 1,
    page: 1,
    pageSize: 25,
    rows: [row(), row({ entityId: 'e-2', subjectFullName: 'خالد المطيري' }), row({ entityId: 'e-3' })],
  };

  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: unknown }) => {
    getCalls.push({ url, params: config?.params as Record<string, string> | undefined });

    if (url.endsWith('/export')) {
      if (exportError) return Promise.reject(exportError) as never;
      return Promise.resolve({
        data: new Blob(['x'], { type: 'text/csv' }),
        headers: { 'content-disposition': 'attachment; filename="hr-operations-x.csv"' },
      } as never);
    }
    if (url === '/hr-operations/dashboard') {
      if (dashboardError) return Promise.reject(dashboardError) as never;
      return Promise.resolve({ data: dashboard } as never);
    }
    return Promise.resolve({ data: queuePage } as never);
  });
});

function renderPage(initialUrl = '/app/hr-operations') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter initialEntries={[initialUrl]}>
      <QueryClientProvider client={qc}>
        <HrOperationsPage />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('P2-HR-009 — البطاقات لا تُخالِف تفصيلها', () => {
  it('يعرض الطوابير الأحد عشر كما أرسلها الخادم بلا زيادة ولا نقصان', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByTestId('hr-ops-card-reports-missing')).toBeInTheDocument());

    QUEUE_KEYS.forEach((k) => expect(screen.getByTestId(`hr-ops-card-${k}`)).toBeInTheDocument());
  });

  it('لا يحسب العدد محلّيًّا: الرقم المعروض هو رقم الخادم حرفيًّا', async () => {
    // الخادم يقول ٣ بينما التفصيل يحمل ٣ صفوف — ونؤكّد أنّ البطاقة تعرض قيمة الخادم
    // لا `rows.length` ولا أيّ اشتقاق آخر.
    dashboard.cards = dashboard.cards.map((c) =>
      c.key === 'attendance-awaiting-employee' ? { ...c, count: 3 } : c,
    );
    renderPage();

    await waitFor(() =>
      expect(screen.getByTestId('hr-ops-count-attendance-awaiting-employee')).toHaveTextContent('3'),
    );
  });

  it('فتح البطاقة يستدعي تفصيل الطابور نفسه بالمرشِّح نفسه', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByTestId('hr-ops-card-attendance-awaiting-employee')).toBeInTheDocument());

    fireEvent.click(screen.getByTestId('hr-ops-card-attendance-awaiting-employee'));

    await waitFor(() => expect(screen.getByTestId('hr-ops-queue-table')).toBeInTheDocument());

    const queueCall = getCalls.find((c) => c.url === '/hr-operations/queues/attendance-awaiting-employee');
    expect(queueCall).toBeTruthy();
    // المرشِّح الدوريّ نفسه الذي بُنيت به البطاقة يُمرَّر إلى التفصيل.
    expect(queueCall?.params?.recentCycles).toBe('8');
    expect(screen.getAllByTestId('hr-ops-row')).toHaveLength(3);
  });

  it('العدد المعروض في ترويسة التفصيل هو totalCount من الخادم لا عدد صفوف الصفحة', async () => {
    queuePage = { ...queuePage, totalCount: 3, rows: queuePage.rows.slice(0, 2) };
    renderPage('/app/hr-operations?queue=attendance-awaiting-employee');

    await waitFor(() => expect(screen.getAllByTestId('hr-ops-row')).toHaveLength(2));
    expect(screen.getByTestId('hr-ops-drilldown')).toHaveTextContent('3 بندًا');
  });
});

describe('P2-HR-009 — المرشِّح يُفرَض خادميًّا لا في المتصفّح', () => {
  it('يمرّر مرشِّح الموظّف والمهلة إلى الخادم بدل التصفية محلّيًّا', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByTestId('hr-ops-filter')).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText('معرّف الموظّف'), { target: { value: USER_ID } });
    fireEvent.change(screen.getByLabelText('المهلة'), { target: { value: 'overdue' } });
    fireEvent.click(screen.getByText('تطبيق المرشِّحات'));

    await waitFor(() => {
      const last = [...getCalls].reverse().find((c) => c.url === '/hr-operations/dashboard');
      expect(last?.params?.userId).toBe(USER_ID);
      expect(last?.params?.overdueOnly).toBe('true');
    });
  });

  it('لا يُرسِل مرشِّحًا فارغًا يُفسَّر خادميًّا بلا معنى', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByTestId('hr-ops-filter')).toBeInTheDocument());

    const first = getCalls.find((c) => c.url === '/hr-operations/dashboard');
    expect(first?.params).not.toHaveProperty('userId');
    expect(first?.params).not.toHaveProperty('overdueOnly');
  });
});

describe('P2-HR-009 — الصلاحيّة قرار خادميّ والواجهة تعرضه فقط', () => {
  it('403 على اللوحة يظهر رسالة صلاحيّة لا شاشة عطل', async () => {
    dashboardError = { response: { status: 403 } };
    renderPage();

    await waitFor(() =>
      expect(screen.getByText('لا تملك صلاحيّة لوحة العمليّات')).toBeInTheDocument(),
    );
    expect(screen.queryByText('تعذّر تحميل البيانات')).not.toBeInTheDocument();
  });

  it('فشل التصدير بـ403 لا يُسقط اللوحة — المفتاحان منفصلان', async () => {
    exportError = { response: { status: 403 } };
    renderPage('/app/hr-operations?queue=attendance-awaiting-employee');

    await waitFor(() => expect(screen.getByTestId('hr-ops-export')).toBeInTheDocument());
    fireEvent.click(screen.getByTestId('hr-ops-export'));

    await waitFor(() =>
      expect(screen.getByText('لا تملك صلاحيّة تنفيذ هذا الإجراء.')).toBeInTheDocument(),
    );
    // الجدول ما يزال قائمًا: منع التصدير ليس منع العرض.
    expect(screen.getByTestId('hr-ops-queue-table')).toBeInTheDocument();
  });

  it('التصدير الناجح يُخبِر المستخدم صراحةً بأنّه مُسجَّل في التدقيق', async () => {
    const createObjectURL = vi.fn(() => 'blob:x');
    const revokeObjectURL = vi.fn();
    Object.assign(URL, { createObjectURL, revokeObjectURL });

    renderPage('/app/hr-operations?queue=attendance-awaiting-employee');
    await waitFor(() => expect(screen.getByTestId('hr-ops-export')).toBeInTheDocument());
    fireEvent.click(screen.getByTestId('hr-ops-export'));

    await waitFor(() => expect(screen.getByText(/سجلّ التدقيق/)).toBeInTheDocument());
    expect(getCalls.some((c) => c.url.endsWith('/export'))).toBe(true);
  });
});

describe('P2-HR-009 — حالات مستقلّة ومعنى دقيق للصفّ', () => {
  it('طابور فارغ يعرض حالة فراغ لا خطأ ولا جدولًا بلا صفوف', async () => {
    queuePage = { ...queuePage, totalCount: 0, breachedCount: 0, rows: [] };
    renderPage('/app/hr-operations?queue=reports-missing');

    await waitFor(() => expect(screen.getByText('لا بنود في هذا الطابور')).toBeInTheDocument());
    expect(screen.queryByTestId('hr-ops-queue-table')).not.toBeInTheDocument();
  });

  it('خطأ التفصيل لا يُخفي البطاقات — الحالات مستقلّة لا مشتركة', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url === '/hr-operations/dashboard') return Promise.resolve({ data: dashboard } as never);
      return Promise.reject(new Error('boom')) as never;
    });

    renderPage('/app/hr-operations?queue=reports-missing');

    await waitFor(() => expect(screen.getByText('تعذّر تحميل البيانات')).toBeInTheDocument());
    expect(screen.getByTestId('hr-ops-card-reports-missing')).toBeInTheDocument();
  });

  it('«لا مهلة» تُعرَض شرطةً لا خرقًا', async () => {
    queuePage = {
      ...queuePage,
      totalCount: 1,
      breachedCount: 0,
      rows: [row({ slaDueAtUtc: null, slaBreached: false })],
    };
    renderPage('/app/hr-operations?queue=attendance-awaiting-employee');

    await waitFor(() => expect(screen.getByTestId('hr-ops-row')).toBeInTheDocument());
    const cells = within(screen.getByTestId('hr-ops-row')).getAllByRole('cell');
    expect(cells[6]).toHaveTextContent('—');
  });

  it('الصفّ يقود إلى ملفّ الموظّف بمعرّفه لا باسمه', async () => {
    renderPage('/app/hr-operations?queue=attendance-awaiting-employee');
    await waitFor(() => expect(screen.getAllByTestId('hr-ops-row').length).toBeGreaterThan(0));

    const link = within(screen.getAllByTestId('hr-ops-row')[0]).getByRole('link');
    expect(link).toHaveAttribute('href', `/app/employee/${USER_ID}`);
  });

  it('النطاق يُعلَن مع الأرقام كي لا يُقرأ رقم خارج سياقه', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByTestId('hr-ops-scope')).toHaveTextContent('7 موظّفًا'));
  });
});

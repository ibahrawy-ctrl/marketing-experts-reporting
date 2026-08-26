// ======================================================================
// P2-ATT-007 — اختبارات سطح الحضور والالتزام.
//
// **ما تقيسه هذه الحزمة بالضبط:** أنّ الواجهة لا تتّخذ قرار تخويل. الأزرار تُرسَم من
// `allowedActions` القادمة من الخادم حصرًا، فلو أرسل الخادم قائمة فارغة لم يظهر زرّ واحد
// مهما كان دور المستخدم؛ ولو أرسل فعلًا ظهر ولو لم «يبدُ» منطقيًّا للمتصفّح. هذا هو
// الفارق بين واجهة تعكس القرار وواجهة تخترعه.
//
// وتقيس كذلك أنّ الحقل الذي حجبه الخادم **غائب** من الـJSON لا `null`: غيابه هو العقد
// الأمنيّ، وعرض «—» مكانه كان سيكشف وجوده.
//
// `api` متجسَّس لا هوك مموَّه: الادّعاء عن العقد الشبكيّ نفسه (المسار، الترويسة، الحمولة).
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { AttendanceIncidentDetail, AttendanceListItem } from '../types/attendance';
import AttendancePage from './AttendancePage';

const INCIDENT_ID = '44444444-4444-4444-4444-444444444444';
const TYPE_ID = '55555555-5555-5555-5555-555555555555';

let getCalls: { url: string; params: unknown }[] = [];
let postCalls: { url: string; body: unknown; config: unknown }[] = [];
let detail: AttendanceIncidentDetail;
let items: AttendanceListItem[];

function listItem(): AttendanceListItem {
  return {
    id: INCIDENT_ID,
    subjectUserId: 'u-1',
    subjectName: 'سارة العتيبي',
    incidentTypeId: TYPE_ID,
    typeCode: 'LATE',
    typeNameAr: 'تأخّر عن الدوام',
    incidentDate: '2026-08-18',
    status: 'AwaitingEmployee',
    statusAr: 'بانتظار ردّ الموظّف',
    isOfficialIncident: false,
    durationMinutes: 45,
    ageingDays: 2,
    slaDueAtUtc: '2026-08-20T06:30:00Z',
    isOverdue: false,
    lastActionAtUtc: '2026-08-18T07:00:00Z',
    nextActorAr: 'الموظّف',
  };
}

function detailFixture(): AttendanceIncidentDetail {
  return {
    id: INCIDENT_ID,
    subjectUserId: 'u-1',
    subjectName: 'سارة العتيبي',
    incidentTypeId: TYPE_ID,
    typeCode: 'LATE',
    typeNameAr: 'تأخّر عن الدوام',
    incidentDate: '2026-08-18',
    startTime: '08:45:00',
    returnTime: '09:30:00',
    durationMinutes: 45,
    description: 'تأخّر عن بداية الدوام بلا إشعار مسبق.',
    detectionSource: 'Manual',
    reportedByUserId: 'u-2',
    reportedByName: 'خالد المطيري',
    status: 'AwaitingEmployee',
    statusAr: 'بانتظار ردّ الموظّف',
    isOfficialIncident: false,
    concurrencyStamp: 3,
    slaDueAtUtc: '2026-08-20T06:30:00Z',
    isOverdue: false,
    ageingDays: 2,
    nextActorAr: 'الموظّف',
    respondedAtUtc: null,
    hrDecision: null,
    reviewedByUserId: null,
    reviewedAtUtc: null,
    reconciledWithLeaveId: null,
    reconciledWithPermissionId: null,
    duplicateOfId: null,
    closedAtUtc: null,
    createdAtUtc: '2026-08-18T07:00:00Z',
    attachments: [],
    events: [
      {
        id: 'e-1',
        actorUserId: 'u-2',
        actorName: 'خالد المطيري',
        action: 'Submit',
        fromStatus: 'Draft',
        toStatus: 'AwaitingEmployee',
        comment: null,
        createdAtUtc: '2026-08-18T07:00:00Z',
      },
    ],
    allowedActions: [],
  };
}

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  postCalls = [];
  detail = detailFixture();
  items = [listItem()];

  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: unknown }) => {
    getCalls.push({ url, params: config?.params });
    if (url === '/attendance/types') {
      return Promise.resolve({
        data: [
          {
            id: TYPE_ID,
            code: 'LATE',
            nameAr: 'تأخّر عن الدوام',
            requiresTimes: true,
            requiresPolicyReference: false,
            allowsMultiplePerDay: false,
            order: 1,
          },
        ],
      } as never);
    }
    if (url === `/attendance/${INCIDENT_ID}`) {
      return Promise.resolve({ data: detail } as never);
    }
    return Promise.resolve({
      data: { items, totalCount: items.length, page: 1, pageSize: 25 },
    } as never);
  });

  vi.spyOn(api, 'post').mockImplementation((url: string, b?: unknown, config?: unknown) => {
    postCalls.push({ url, body: b, config });
    return Promise.resolve({ data: detail } as never);
  });
});

function renderPage(initialUrl = '/app/attendance') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter initialEntries={[initialUrl]}>
      <QueryClientProvider client={qc}>
        <AttendancePage />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

function detailPanel(): Promise<HTMLElement> {
  return screen.findByTestId('attendance-detail');
}

describe('AttendancePage — القائمة والنطاق', () => {
  it('يعرض الوقائع ضمن نطاق المستخدم كما ردّها الخادم', async () => {
    renderPage();
    const table = await screen.findByTestId('attendance-list');
    expect(within(table).getByText('سارة العتيبي')).toBeInTheDocument();
    expect(within(table).getByText('بانتظار ردّ الموظّف')).toBeInTheDocument();
  });

  it('التبويب الافتراضيّ يطلب «ما ينتظر إجرائي» من الخادم لا يرشّح محلّيًّا', async () => {
    renderPage();
    await screen.findByTestId('attendance-list');
    const listCall = getCalls.find((c) => c.url === '/attendance');
    expect(listCall?.params).toMatchObject({ needsMyAction: 'true' });
  });

  it('طابور مراجعة الموارد البشريّة يطلب الحالة AwaitingHr خادميًّا', async () => {
    renderPage();
    await screen.findByTestId('attendance-list');
    fireEvent.click(screen.getByRole('tab', { name: 'طابور المراجعة' }));
    await waitFor(() => {
      const calls = getCalls.filter((c) => c.url === '/attendance');
      expect(calls[calls.length - 1].params).toMatchObject({ status: 'AwaitingHr' });
    });
  });

  it('يعرض حالة فارغة مستقلّة حين لا وقائع', async () => {
    items = [];
    renderPage();
    expect(await screen.findByText('لا توجد وقائع')).toBeInTheDocument();
    expect(screen.queryByTestId('attendance-list')).toBeNull();
  });

  it('يعرض حالة خطأ مستقلّة بلا انهيار الصفحة كلّها', async () => {
    vi.spyOn(api, 'get').mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText('تعذّر تحميل البيانات')).toBeInTheDocument();
    // الترويسة تبقى قائمة: فشل القائمة ليس فشل السطح.
    expect(screen.getByRole('heading', { name: 'الحضور والالتزام' })).toBeInTheDocument();
  });

  it('الصفحة كلّها بالاتّجاه من اليمين إلى اليسار', async () => {
    const { container } = renderPage();
    await screen.findByTestId('attendance-list');
    expect(container.querySelector('[dir="rtl"]')).not.toBeNull();
  });
});

describe('AttendancePage — الأزرار من عقد الخادم لا من الدور', () => {
  it('قائمة أفعال فارغة ⇒ لا يظهر أيّ زرّ إجراء', async () => {
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    const actions = within(panel).getByTestId('attendance-actions');
    expect(within(actions).getByText('لا إجراء متاح لك على هذه الواقعة الآن.')).toBeInTheDocument();
    expect(within(actions).queryByRole('button')).toBeNull();
  });

  it('يرسم بالضبط الأفعال التي سمّاها الخادم — لا أكثر ولا أقلّ', async () => {
    detail.allowedActions = ['Acknowledge', 'Dispute'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const actions = within(await detailPanel()).getByTestId('attendance-actions');
    const labels = within(actions)
      .getAllByRole('button')
      .map((b) => b.textContent);
    expect(labels).toEqual(['إقرار بالواقعة', 'اعتراض']);
    // أفعال الموارد البشريّة لم تصل ⇒ لا وجود لها في الشجرة إطلاقًا.
    expect(within(actions).queryByText('تأكيد الواقعة')).toBeNull();
    expect(within(actions).queryByText('رفض البلاغ')).toBeNull();
  });

  it('يرسم أفعال المراجعة حين يرسلها الخادم', async () => {
    detail.allowedActions = ['HrConfirm', 'HrReject', 'HrReconcile'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const actions = within(await detailPanel()).getByTestId('attendance-actions');
    const labels = within(actions)
      .getAllByRole('button')
      .map((b) => b.textContent);
    expect(labels).toEqual(['تأكيد الواقعة', 'رفض البلاغ', 'مصالحة مع إجازة']);
  });
});

describe('AttendancePage — حقّ الموظّف في الردّ', () => {
  it('الإقرار يرسل ختم التزامن ويصيب مسار acknowledge', async () => {
    detail.allowedActions = ['Acknowledge'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    fireEvent.click(within(panel).getByRole('button', { name: 'إقرار بالواقعة' }));
    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].url).toBe(`/attendance/${INCIDENT_ID}/acknowledge`);
    expect(postCalls[0].body).toMatchObject({ concurrencyStamp: 3 });
  });

  it('الاعتراض يفتح حقل السبب أوّلًا ولا يُرسِل قبل تأكيده', async () => {
    detail.allowedActions = ['Dispute'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    fireEvent.click(within(panel).getByRole('button', { name: 'اعتراض' }));

    // فتح الحقل ليس إرسالًا: لا قرار موثَّق بلا رواية مكتوبة.
    expect(postCalls).toHaveLength(0);
    const textarea = await screen.findByLabelText('نصّ الإجراء');
    fireEvent.change(textarea, { target: { value: 'كنت في مهمّة خارجيّة معتمدة.' } });
    fireEvent.click(screen.getByRole('button', { name: /تأكيد اعتراض/ }));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].url).toBe(`/attendance/${INCIDENT_ID}/dispute`);
    expect(postCalls[0].body).toMatchObject({
      concurrencyStamp: 3,
      response: 'كنت في مهمّة خارجيّة معتمدة.',
    });
  });

  it('التراجع يُغلق حقل السبب بلا إرسال', async () => {
    detail.allowedActions = ['Dispute'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    fireEvent.click(within(await detailPanel()).getByRole('button', { name: 'اعتراض' }));
    fireEvent.click(await screen.findByRole('button', { name: 'تراجع' }));
    await waitFor(() => expect(screen.queryByLabelText('نصّ الإجراء')).toBeNull());
    expect(postCalls).toHaveLength(0);
  });
});

describe('AttendancePage — مراجعة الموارد البشريّة', () => {
  it('قرارات المراجعة كلّها تمرّ بنقطة hr-review الواحدة بالقرار المسمّى', async () => {
    detail.allowedActions = ['HrConfirm'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    fireEvent.click(within(await detailPanel()).getByRole('button', { name: 'تأكيد الواقعة' }));
    fireEvent.change(await screen.findByLabelText('نصّ الإجراء'), {
      target: { value: 'تحقّقتُ من السجلّ ولم يرد إذن مسبق.' },
    });
    fireEvent.click(screen.getByRole('button', { name: /تأكيد تأكيد الواقعة/ }));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].url).toBe(`/attendance/${INCIDENT_ID}/hr-review`);
    expect(postCalls[0].body).toMatchObject({
      decision: 'Confirm',
      concurrencyStamp: 3,
      note: 'تحقّقتُ من السجلّ ولم يرد إذن مسبق.',
    });
  });

  it('التأكيد لا يُرسِل أيّ حمولة ماليّة ولا يمسّ الرواتب', async () => {
    detail.allowedActions = ['HrConfirm'];
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    fireEvent.click(within(await detailPanel()).getByRole('button', { name: 'تأكيد الواقعة' }));
    fireEvent.change(await screen.findByLabelText('نصّ الإجراء'), { target: { value: 'مؤكَّد.' } });
    fireEvent.click(screen.getByRole('button', { name: /تأكيد تأكيد الواقعة/ }));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    const keys = Object.keys(postCalls[0].body as Record<string, unknown>);
    expect(keys.some((k) => /payroll|deduct|salary|amount|balance/i.test(k))).toBe(false);
    expect(postCalls.some((c) => /payroll|deduction|salary/i.test(c.url))).toBe(false);
  });
});

describe('AttendancePage — الحقل المحجوب غائب لا فارغ', () => {
  it('لا يعرض ملاحظة الموارد البشريّة حين لا تصل في الحمولة', async () => {
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    expect(within(panel).queryByText('ملاحظة الموارد البشريّة')).toBeNull();
    expect(within(panel).queryByText('ردّ الموظّف')).toBeNull();
  });

  it('يعرضها حين يرسلها الخادم لصاحب الإذن', async () => {
    detail.hrNote = 'مطابَقة مع سجلّ البوّابة.';
    detail.employeeResponse = 'أقرّ بالواقعة.';
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    expect(within(panel).getByText('مطابَقة مع سجلّ البوّابة.')).toBeInTheDocument();
    expect(within(panel).getByText('أقرّ بالواقعة.')).toBeInTheDocument();
  });
});

describe('AttendancePage — بلاغ لا إدانة', () => {
  it('يميّز البلاغ عن الواقعة المؤكَّدة من حسم الخادم', async () => {
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    expect(within(panel).getByText('بلاغ')).toBeInTheDocument();
    expect(within(panel).queryByText('واقعة مؤكَّدة')).toBeNull();
  });

  it('يعلن الواقعة مؤكَّدة حين يقول الخادم ذلك ولو كانت الحالة تصعيدًا', async () => {
    detail.status = 'Escalated';
    detail.statusAr = 'مُصعَّدة';
    detail.isOfficialIncident = true;
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const panel = await detailPanel();
    expect(within(panel).getByText('واقعة مؤكَّدة')).toBeInTheDocument();
  });

  it('الخطّ الزمنيّ يعرض كلّ انتقال بحالتيه وفاعله', async () => {
    renderPage(`/app/attendance?incident=${INCIDENT_ID}`);
    const timeline = within(await detailPanel()).getByTestId('attendance-timeline');
    expect(within(timeline).getByText(/Draft ← AwaitingEmployee/)).toBeInTheDocument();
    expect(within(timeline).getByText(/خالد المطيري/)).toBeInTheDocument();
  });
});

describe('AttendancePage — تسجيل بلاغ', () => {
  it('يرسل مفتاح تكافؤ فريدًا مع كلّ محاولة إنشاء', async () => {
    renderPage();
    await screen.findByTestId('attendance-list');
    fireEvent.click(screen.getByRole('button', { name: 'تسجيل بلاغ' }));

    fireEvent.change(await screen.findByLabelText('معرّف الموظّف'), { target: { value: 'u-1' } });
    fireEvent.change(screen.getByLabelText('نوع الواقعة'), { target: { value: TYPE_ID } });
    fireEvent.change(screen.getByLabelText('تاريخ الواقعة'), { target: { value: '2026-08-18' } });
    fireEvent.change(await screen.findByLabelText('وقت البداية'), { target: { value: '08:45' } });
    fireEvent.change(screen.getByLabelText('وقت العودة'), { target: { value: '09:30' } });
    fireEvent.change(screen.getByLabelText('الوصف'), { target: { value: 'تأخّر بلا إشعار.' } });
    fireEvent.submit(screen.getByTestId('attendance-report-form'));

    await waitFor(() => expect(postCalls).toHaveLength(1));
    expect(postCalls[0].url).toBe('/attendance');
    const headers = (postCalls[0].config as { headers?: Record<string, string> })?.headers;
    expect(headers?.['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/i);
  });

  it('حقول الوقت تظهر فقط للنوع الذي يشترطها', async () => {
    renderPage();
    await screen.findByTestId('attendance-list');
    fireEvent.click(screen.getByRole('button', { name: 'تسجيل بلاغ' }));
    await screen.findByLabelText('معرّف الموظّف');
    // قبل اختيار النوع لا وقت مطلوب.
    expect(screen.queryByLabelText('وقت البداية')).toBeNull();
    fireEvent.change(screen.getByLabelText('نوع الواقعة'), { target: { value: TYPE_ID } });
    expect(await screen.findByLabelText('وقت البداية')).toBeInTheDocument();
  });
});

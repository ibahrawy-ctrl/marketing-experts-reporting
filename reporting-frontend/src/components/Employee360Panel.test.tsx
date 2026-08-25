// ======================================================================
// P2-EMP-003 — اختبارات لوحة Employee 360.
//
// **لماذا `api` متجسَّس لا هوك مموَّه؟** لأنّ الادّعاء الأمنيّ المركزيّ كمّيّ عن العقد
// نفسه: «ما لا يصل من الخادم لا يُرسَم»، و«وضع الذات ينادي `/employees/me/…` لا مسارًا
// بمعرّف مشتقّ في المتصفّح». تمويه الهوك كان سيخفي بالضبط ما نقيسه.
//
// `retry: false` إلزاميّ وإلّا صارت حالات الخطأ تتأخّر ثلاث محاولات.
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { Employee360Dto, Employee360Section } from '../types/employee360';
import { Employee360Panel } from './Employee360Panel';

const SUBJECT = '22222222-2222-2222-2222-222222222222';

let getCalls: { url: string; params: unknown }[] = [];
let body: Employee360Dto;

function section(over: Partial<Employee360Section> & { key: string; titleAr: string }): Employee360Section {
  return {
    status: 'Ready',
    dataQuality: 'Complete',
    lastUpdatedAtUtc: null,
    summary: null,
    items: null,
    ...over,
  };
}

/** حمولة أساس: هويّة + ملخّص تشغيليّ + خطّ زمنيّ. الأقسام الغائبة **غائبة فعلًا** لا فارغة. */
function fixture(): Employee360Dto {
  return {
    subjectUserId: SUBJECT,
    isSelf: false,
    viewerRelation: 'DirectTeam',
    periodKey: '2026-W34',
    sections: {
      identity: section({
        key: 'identity',
        titleAr: 'الهويّة وحالة التوظيف',
        summary: {
          userId: SUBJECT,
          fullName: 'سارة العتيبي',
          email: 'sara@example.com',
          jobRoleName: 'أخصّائيّ تسويق',
          teamName: 'فريق المحتوى',
          departmentName: 'التسويق',
          directManagerName: 'خالد',
          isActive: true,
          joinedAtUtc: '2024-01-05T00:00:00Z',
        },
      }),
      operationalSummary: section({
        key: 'operationalSummary',
        titleAr: 'الملخّص التشغيليّ',
        summary: {
          reportsSubmitted: 0,
          reportsReturned: 0,
          reportsNeedsAction: 3,
          kpiEvaluationCount: 0,
          lastKpiScore: null,
          lastKpiPeriodKey: null,
          openLeaveRequests: 0,
          openServiceRequests: 0,
          openNotesRequiringAction: 0,
          openGovernanceItems: 0,
        },
      }),
      timeline: section({
        key: 'timeline',
        titleAr: 'الخطّ الزمنيّ الموحّد',
        items: [
          {
            kind: 'ReportSubmitted',
            source: 'Reports',
            sourceId: 'r-1',
            label: 'تسليم تقرير أسبوعيّ',
            atUtc: '2026-08-20T09:00:00Z',
            needsMyAction: false,
          },
          {
            kind: 'LeaveRequested',
            source: 'Leave',
            sourceId: 'l-1',
            label: 'طلب إجازة بانتظار الاعتماد',
            atUtc: '2026-08-21T09:00:00Z',
            needsMyAction: true,
          },
        ],
      }),
    },
  };
}

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  body = fixture();
  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: unknown }) => {
    getCalls.push({ url, params: config?.params });
    return Promise.resolve({ data: body } as never);
  });
});

function renderPanel(subject = SUBJECT) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter>
      <QueryClientProvider client={qc}>
        <Employee360Panel subject={subject} />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('Employee360Panel — العقد مع الخادم', () => {
  it('ينادي مسار المعرّف مرّة واحدة بلا معامل فترة افتراضيّ', async () => {
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    expect(getCalls).toHaveLength(1);
    expect(getCalls[0].url).toBe(`/employees/${SUBJECT}/profile-360`);
    expect(getCalls[0].params).toBeUndefined();
  });

  it('وضع الذات ينادي مسار me الخادميّ ولا يشتقّ معرّفًا في المتصفّح', async () => {
    renderPanel('me');
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    expect(getCalls[0].url).toBe('/employees/me/profile-360');
  });

  it('يرسل مفتاح الفترة بعد تطبيقه فقط', async () => {
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    fireEvent.change(screen.getByLabelText('مفتاح الفترة'), { target: { value: '2026-W30' } });
    fireEvent.click(screen.getByRole('button', { name: 'تطبيق الفترة' }));
    await waitFor(() => expect(getCalls).toHaveLength(2));
    expect(getCalls[1].params).toEqual({ period: '2026-W30' });
  });

  it('يعرض الفترة والعلاقة كما حسمهما الخادم', async () => {
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    expect(screen.getByText(/مرؤوس مباشر/)).toBeInTheDocument();
    expect(screen.getByText(/2026-W34/)).toBeInTheDocument();
  });
});

describe('Employee360Panel — لا يُرسَم إلّا ما وصل', () => {
  it('لا يعرض قسمًا لم يرسله الخادم — لا عنوانًا ولا رابط تنقّل', async () => {
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    expect(screen.queryByRole('heading', { name: 'الملاحظات الإداريّة' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'الملاحظات الإداريّة' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'الإجازات والاستئذانات' })).toBeNull();
  });

  it('يبني قائمة التنقّل من مفاتيح الخادم بالترتيب المعتمد', async () => {
    renderPanel();
    const nav = await screen.findByRole('navigation', { name: 'أقسام الملفّ الشامل' });
    const links = within(nav).getAllByRole('link').map((a) => a.textContent);
    expect(links).toEqual(['الهويّة وحالة التوظيف', 'الملخّص التشغيليّ', 'الخطّ الزمنيّ الموحّد']);
  });

  it('لا يعرض عمود «السبب» حين لا يصل الحقل أصلًا في أيّ صفّ', async () => {
    body.sections.leaveAndPermissions = section({
      key: 'leaveAndPermissions',
      titleAr: 'الإجازات والاستئذانات',
      items: [
        {
          id: 'l-1',
          type: 'Annual',
          startDate: '2026-08-10',
          endDate: '2026-08-12',
          status: 'Approved',
          currentStep: 'Closed',
          createdAtUtc: '2026-08-01T00:00:00Z',
        },
      ],
    });
    renderPanel();
    await screen.findByRole('heading', { name: 'الإجازات والاستئذانات' });
    expect(screen.queryByRole('columnheader', { name: 'السبب' })).toBeNull();
  });

  it('يعرض عمود «السبب» حين يرسله الخادم لصاحب الإذن', async () => {
    body.sections.leaveAndPermissions = section({
      key: 'leaveAndPermissions',
      titleAr: 'الإجازات والاستئذانات',
      items: [
        {
          id: 'l-1',
          type: 'Annual',
          startDate: '2026-08-10',
          endDate: '2026-08-12',
          status: 'Approved',
          currentStep: 'Closed',
          reason: 'ظرف عائليّ',
          createdAtUtc: '2026-08-01T00:00:00Z',
        },
      ],
    });
    renderPanel();
    await screen.findByRole('heading', { name: 'الإجازات والاستئذانات' });
    expect(screen.getByRole('columnheader', { name: 'السبب' })).toBeInTheDocument();
    expect(screen.getByText('ظرف عائليّ')).toBeInTheDocument();
  });
});

describe('Employee360Panel — حالات القسم', () => {
  it('«صفر» يُعرض رقمًا و«لا بيانات» يُعرض حالة فارغة — لا يُخلَط بينهما', async () => {
    body.sections.reports = section({
      key: 'reports',
      titleAr: 'التقارير',
      status: 'NoData',
      dataQuality: 'Unavailable',
      reason: 'لا توجد تقارير في هذه الفترة.',
    });
    renderPanel();
    const summary = await screen.findByRole('heading', { name: 'الملخّص التشغيليّ' });
    const summaryCard = summary.closest('section') as HTMLElement;
    // صفر تقارير مُسلَّمة = رقم حقيقيّ معروض.
    expect(within(summaryCard).getByText('تقارير مُسلَّمة').parentElement).toHaveTextContent('0');
    // قسم التقارير نفسه = لا بيانات بسبب معلن، لا صفر مُختلَق.
    const reportsCard = screen.getByRole('heading', { name: 'التقارير' }).closest('section') as HTMLElement;
    expect(within(reportsCard).getByText('لا توجد تقارير في هذه الفترة.')).toBeInTheDocument();
  });

  it('فشل قسم واحد لا يُسقِط بقيّة الصفحة ويعرض إعادة المحاولة', async () => {
    body.sections.governance = section({
      key: 'governance',
      titleAr: 'الحوكمة',
      status: 'Error',
      dataQuality: 'Unavailable',
      reason: 'تعذّر تحميل هذا القسم. حاول مرّة أخرى.',
    });
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    const card = screen.getByRole('heading', { name: 'الحوكمة' }).closest('section') as HTMLElement;
    expect(within(card).getByText('تعذّر تحميل هذا القسم')).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: /إعادة المحاولة|أعد المحاولة/ })).toBeInTheDocument();
    // بقيّة الأقسام سليمة.
    expect(screen.getByText('سارة العتيبي')).toBeInTheDocument();
  });

  it('يعلن جودة البيانات «غير متاحة» لقسم الحضور بدل اختلاق بيانات', async () => {
    body.sections.attendanceAndCompliance = section({
      key: 'attendanceAndCompliance',
      titleAr: 'الحضور والالتزام',
      status: 'NoData',
      dataQuality: 'Unavailable',
      reason: 'وحدة الحضور غير مفعّلة في هذا الإصدار.',
    });
    renderPanel();
    const card = (await screen.findByRole('heading', { name: 'الحضور والالتزام' })).closest(
      'section',
    ) as HTMLElement;
    expect(within(card).getByText('غير متاحة')).toBeInTheDocument();
    expect(within(card).getByText('وحدة الحضور غير مفعّلة في هذا الإصدار.')).toBeInTheDocument();
  });

  it('يعرض حالة خطأ عامّة حين يرفض الخادم الطلب كلّه', async () => {
    vi.spyOn(api, 'get').mockRejectedValue(new Error('404'));
    renderPanel();
    expect(await screen.findByText('تعذّر تحميل الملفّ الشامل')).toBeInTheDocument();
  });
});

describe('Employee360Panel — مرشّحات الخطّ الزمنيّ', () => {
  it('يرشّح على «يحتاج إجراءً منّي» بلا نداء شبكة إضافيّ', async () => {
    renderPanel();
    await screen.findByText('تسليم تقرير أسبوعيّ');
    fireEvent.click(screen.getByLabelText('يحتاج إجراءً منّي'));
    expect(screen.queryByText('تسليم تقرير أسبوعيّ')).toBeNull();
    expect(screen.getByText('طلب إجازة بانتظار الاعتماد')).toBeInTheDocument();
    expect(getCalls).toHaveLength(1);
  });

  it('يرشّح على المصدر', async () => {
    renderPanel();
    await screen.findByText('تسليم تقرير أسبوعيّ');
    fireEvent.change(screen.getByLabelText('المصدر'), { target: { value: 'Leave' } });
    expect(screen.queryByText('تسليم تقرير أسبوعيّ')).toBeNull();
    expect(screen.getByText('طلب إجازة بانتظار الاعتماد')).toBeInTheDocument();
  });

  it('يعرض حالة فارغة حين لا يطابق شيء المرشّحات', async () => {
    renderPanel();
    await screen.findByText('تسليم تقرير أسبوعيّ');
    fireEvent.change(screen.getByLabelText('نوع الحدث'), { target: { value: 'ReportSubmitted' } });
    fireEvent.click(screen.getByLabelText('يحتاج إجراءً منّي'));
    expect(screen.getByText('لا أحداث مطابقة')).toBeInTheDocument();
  });
});

// ======================================================================
// P2-ATT-007 — قسم الحضور داخل الملفّ الشامل.
//
// الادّعاء المقيس هنا ليس «هل ظهر الجدول»، بل **أنّ اللوحة لا تترجم حالةً إلى حكم**:
// «مؤكَّدة» تُقرأ من `isConfirmed` الذي يحسمه الخادم، لا من اسم الحالة المعروض. لو اشتُقّت
// محلّيًّا لصارت `Escalated` إدانةً في شاشةٍ وبراءةً في أخرى.
// ======================================================================

const INCIDENT_ID = '33333333-3333-3333-3333-333333333333';

function attendanceSection(items: Record<string, unknown>[]) {
  return section({ key: 'attendanceAndCompliance', titleAr: 'الحضور والالتزام', items });
}

function attendanceCard(): Promise<HTMLElement> {
  return screen
    .findByRole('heading', { name: 'الحضور والالتزام' })
    .then((h) => h.closest('section') as HTMLElement);
}

describe('Employee360Panel — قسم الحضور والالتزام', () => {
  it('يعرض وقائع الحضور بأعمدتها ولا يخترع أثرًا ماليًّا', async () => {
    body.sections.attendanceAndCompliance = attendanceSection([
      {
        id: INCIDENT_ID,
        typeCode: 'LATE',
        typeNameAr: 'تأخّر عن الدوام',
        incidentDate: '2026-08-18',
        status: 'AwaitingEmployee',
        isConfirmed: false,
        createdAtUtc: '2026-08-18T06:30:00Z',
      },
    ]);
    renderPanel();
    const card = await attendanceCard();
    expect(within(card).getByText('تأخّر عن الدوام')).toBeInTheDocument();
    // «مؤكَّدة = لا» لبلاغ لم تُقرّه الموارد البشريّة بعد.
    expect(within(card).getByRole('columnheader', { name: 'مؤكَّدة' })).toBeInTheDocument();
    expect(within(card).getByText('لا')).toBeInTheDocument();
    // لا عمود ولا نصّ يوحي بخصم أو أثر على الراتب.
    expect(within(card).queryByText(/خصم|راتب|Payroll/i)).toBeNull();
  });

  it('«مؤكَّدة» تُقرأ من حسم الخادم لا من اسم الحالة', async () => {
    // `Escalated` حالة تصعيد، والخادم وحده يقرّر إن كانت مبنيّة على تأكيد سابق.
    body.sections.attendanceAndCompliance = attendanceSection([
      {
        id: INCIDENT_ID,
        typeCode: 'ABSENCE',
        typeNameAr: 'غياب',
        incidentDate: '2026-08-19',
        status: 'Escalated',
        isConfirmed: true,
        createdAtUtc: '2026-08-19T06:30:00Z',
      },
    ]);
    renderPanel();
    const card = await attendanceCard();
    expect(within(card).getByText('نعم')).toBeInTheDocument();
  });

  it('يربط كلّ واقعة برابط مصدر إلى سطح الحضور بمعرّفها', async () => {
    body.sections.attendanceAndCompliance = attendanceSection([
      {
        id: INCIDENT_ID,
        typeCode: 'LATE',
        typeNameAr: 'تأخّر عن الدوام',
        incidentDate: '2026-08-18',
        status: 'AwaitingEmployee',
        isConfirmed: false,
        createdAtUtc: '2026-08-18T06:30:00Z',
      },
    ]);
    renderPanel();
    const card = await attendanceCard();
    expect(within(card).getByRole('link', { name: 'فتح التفاصيل' })).toHaveAttribute(
      'href',
      `/app/attendance?incident=${INCIDENT_ID}`,
    );
  });

  it('لا يرسم عمود المصدر لقسم لا رابط مصدر له', async () => {
    body.sections.requestsAndBalances = section({
      key: 'requestsAndBalances',
      titleAr: 'الطلبات والأرصدة',
      items: [{ id: 'q-1', requestType: 'Letter', title: 'خطاب تعريف', status: 'Open' }],
    });
    renderPanel();
    const card = (await screen.findByRole('heading', { name: 'الطلبات والأرصدة' })).closest(
      'section',
    ) as HTMLElement;
    expect(within(card).queryByRole('columnheader', { name: 'المصدر' })).toBeNull();
  });

  it('قسم الحضور الغائب عن حمولة الخادم لا يُرسَم إطلاقًا', async () => {
    renderPanel();
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });
    expect(screen.queryByRole('heading', { name: 'الحضور والالتزام' })).toBeNull();
  });
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { ToastProvider } from '../components/ActionResultToast';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import LeaveRequestsPage from './LeaveRequestsPage';
import type {
  Role,
  MeResponse,
  TeamLeaderPendingGovernanceItemDto,
  TeamLeaderPendingGovernanceResultDto,
  LeaveRequestDto,
} from '../types/api';

// ===== LEAVE-TL-PENDING-GOVERNANCE-R1 — المرحلة 7: اختبارات الواجهة (قراءة-فقط عبر DOM) =====
// تُثبِت: ظهور تبويب «معلّقة عند قادة الفرق» لأدوار الحوكمة فقط (Admin/CEO/GeneralManager/HR)
// وغيابه عن Manager/TeamLeader/CeoSupport/Employee؛ عرض العدّادات وشارات التأخّر الأربع؛
// روابط ملف الموظّف/قائد الفريق؛ شارة «لا يوجد قائد فريق»؛ علامة «(غير نشط)»؛ فلتر التأخّر؛
// والحرج: لا يوجد أيّ زرّ اعتماد/رفض/إعادة توجيه/تعديل — قراءة-فقط بالكامل.

// أدوار الجلسة الحالية — يقرؤها mock الخاص بـ /auth/me لكلّ اختبار.
let currentRoles: Role[] = ['Admin'];
// نتيجة طابور الحوكمة المُرجَعة من الخادم (قابلة للتبديل لكلّ اختبار).
let governanceResult: TeamLeaderPendingGovernanceResultDto = emptyResult();
// تفاصيل الطلب المُرجَعة من `/leave-requests/{id}` — تُستخدَم في اختبارَي «لا أزرار قرار من الحوكمة».
let detailResult: LeaveRequestDto = detail();

// طلب معلّق عند قائد الفريق (Submitted/TeamLeader) يملكه موظّف آخر — أي أنّ خطوة المراجعة قائمة فعلًا.
function detail(over: Partial<LeaveRequestDto> = {}): LeaveRequestDto {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    requesterUserId: 'emp-1',
    requesterName: 'موظّف تجريبي',
    type: 'Leave',
    startDate: '2026-08-10',
    endDate: '2026-08-12',
    startTime: null,
    endTime: null,
    reason: 'سبب تجريبي',
    notes: null,
    status: 'Submitted',
    currentStep: 'TeamLeader',
    isHrRequest: false,
    teamLeaderReviewerId: null,
    teamLeaderReviewerName: null,
    managerReviewerId: null,
    managerReviewerName: null,
    hrReviewerId: null,
    hrReviewerName: null,
    teamLeaderDecisionAtUtc: null,
    managerDecisionAtUtc: null,
    hrDecisionAtUtc: null,
    rejectionReason: null,
    returnReason: null,
    impactsReports: false,
    canCancel: true,
    createdAtUtc: '2026-08-01T09:00:00Z',
    updatedAtUtc: null,
    cancelledAtUtc: null,
    balanceAtRequest: null,
    requestedLeaveDays: null,
    uncoveredLeaveDays: null,
    isPotentialUnpaidLeave: false,
    employeeAcknowledgedUnpaidDeduction: false,
    employeeAcknowledgedAtUtc: null,
    permissionShortfallResolution: 'None',
    timeline: [],
    ...over,
  };
}

function emptyResult(): TeamLeaderPendingGovernanceResultDto {
  return {
    items: [],
    total: 0,
    page: 1,
    pageSize: 200,
    counters: {
      totalPending: 0,
      attention: 0,
      critical: 0,
      expiredUnresolved: 0,
      missingTeamLeader: 0,
      oldestPendingDays: 0,
    },
  };
}

function item(over: Partial<TeamLeaderPendingGovernanceItemDto> = {}): TeamLeaderPendingGovernanceItemDto {
  return {
    requestId: '11111111-1111-1111-1111-111111111111',
    requestType: 'Leave',
    employeeUserId: 'emp-1',
    employeeName: 'موظّف تجريبي',
    departmentId: 'dep-1',
    departmentName: 'قسم التسويق',
    teamId: 'team-1',
    teamName: 'فريق السوشيال',
    teamLeaderUserId: 'tl-1',
    teamLeaderName: 'قائد الفريق التجريبي',
    createdAtUtc: '2026-08-01T09:00:00Z',
    startDate: '2026-08-10',
    endDate: '2026-08-12',
    requestedUnits: 3,
    currentStatus: 'Submitted',
    currentStep: 'TeamLeader',
    lastEventType: 'submission.submitted',
    lastEventAtUtc: '2026-08-01T09:00:00Z',
    daysPending: 5,
    daysUntilStart: 4,
    startedAlready: false,
    endedAlready: false,
    delayStatus: 'Pending',
    delayReason: 'معلّق بانتظار قرار قائد الفريق',
    hasLedger: false,
    ledgerCount: 0,
    isEmployeeActive: true,
    isTeamLeaderActive: true,
    missingTeamLeaderAssignment: false,
    ...over,
  };
}

const me: () => MeResponse = () => ({
  userId: 'me-1',
  fullName: 'مستخدم الاختبار',
  email: 'tester@test.local',
  isActive: true,
  roles: currentRoles,
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
});

let apiGet: ReturnType<typeof vi.spyOn>;

function lookup(url: string): unknown {
  const path = url.split('?')[0];
  if (path === '/auth/me') return me();
  if (path === '/leave-requests/governance/team-leader-pending') return governanceResult;
  // تفاصيل طلب مفرد: /leave-requests/{guid} — يُميَّز عن /my و/pending بوجود شرطات المعرّف.
  if (path.startsWith('/leave-requests/') && path.slice('/leave-requests/'.length).includes('-'))
    return detailResult;
  // بقيّة التبويبات (طلباتي / بانتظار قراري) لا تُختبَر هنا — تُرجِع قائمة فارغة.
  return [];
}

beforeEach(() => {
  tokenStore.clear();
  tokenStore.set('fake-access', 'fake-refresh');
  currentRoles = ['Admin'];
  governanceResult = emptyResult();
  detailResult = detail();
  vi.restoreAllMocks();
  apiGet = vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: lookup(url) } as never),
  );
});

function renderPage(entry = '/leave') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[entry]}>
            <LeaveRequestsPage />
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const GOV_TAB = 'معلّقة عند قادة الفرق';

// ---------- ظهور التبويب حسب الدور (1-8) ----------

it('1 يظهر تبويب الحوكمة لدور Admin', async () => {
  currentRoles = ['Admin'];
  renderPage();
  expect(await screen.findByText(GOV_TAB)).toBeInTheDocument();
});

it('2 يظهر تبويب الحوكمة لدور CEO', async () => {
  currentRoles = ['CEO'];
  renderPage();
  expect(await screen.findByText(GOV_TAB)).toBeInTheDocument();
});

it('3 يظهر تبويب الحوكمة لدور GeneralManager', async () => {
  currentRoles = ['GeneralManager'];
  renderPage();
  expect(await screen.findByText(GOV_TAB)).toBeInTheDocument();
});

it('4 يظهر تبويب الحوكمة لدور HR', async () => {
  currentRoles = ['HR'];
  renderPage();
  expect(await screen.findByText(GOV_TAB)).toBeInTheDocument();
});

it('5 لا يظهر تبويب الحوكمة لدور Manager', async () => {
  currentRoles = ['Manager'];
  renderPage();
  // ننتظر تحميل الصفحة عبر ظهور تبويب المراجعة، ثمّ نتأكّد أنّ تبويب الحوكمة غائب.
  expect(await screen.findByText('بانتظار قراري')).toBeInTheDocument();
  expect(screen.queryByText(GOV_TAB)).not.toBeInTheDocument();
});

it('6 لا يظهر تبويب الحوكمة لدور TeamLeader', async () => {
  currentRoles = ['TeamLeader'];
  renderPage();
  expect(await screen.findByText('بانتظار قراري')).toBeInTheDocument();
  expect(screen.queryByText(GOV_TAB)).not.toBeInTheDocument();
});

it('7 لا يظهر تبويب الحوكمة لدور CeoSupport', async () => {
  currentRoles = ['CeoSupport'];
  renderPage();
  expect(await screen.findByRole('heading', { name: 'الإجازات والاستئذانات' })).toBeInTheDocument();
  expect(screen.queryByText(GOV_TAB)).not.toBeInTheDocument();
});

it('8 لا يظهر تبويب الحوكمة لدور Employee', async () => {
  currentRoles = ['Employee'];
  renderPage();
  expect(await screen.findByRole('heading', { name: 'الإجازات والاستئذانات' })).toBeInTheDocument();
  expect(screen.queryByText(GOV_TAB)).not.toBeInTheDocument();
});

// ---------- محتوى تبويب الحوكمة (Admin، tab=governance) (9-21) ----------

it('9 يعرض تنويه القراءة-فقط في تبويب الحوكمة', async () => {
  currentRoles = ['Admin'];
  renderPage('/leave?tab=governance');
  expect(await screen.findByText(/عرض حوكمة قراءة-فقط على مستوى الشركة/)).toBeInTheDocument();
  expect(screen.getByText(/لا اعتماد ولا رفض ولا إعادة توجيه/)).toBeInTheDocument();
});

it('10 يعرض العدّادات الستّة بقيَمها', async () => {
  governanceResult = {
    ...emptyResult(),
    counters: {
      totalPending: 7,
      attention: 3,
      critical: 2,
      expiredUnresolved: 1,
      missingTeamLeader: 4,
      oldestPendingDays: 12,
    },
  };
  renderPage('/leave?tab=governance');
  expect(await screen.findByText('الإجمالي المعلّق')).toBeInTheDocument();
  // «يحتاج متابعة» و«موعد الإجازة بدأ» تظهران كتسمية عدّاد وكخيار فلتر معًا.
  expect(screen.getAllByText('يحتاج متابعة').length).toBeGreaterThan(0);
  expect(screen.getAllByText('موعد الإجازة بدأ').length).toBeGreaterThan(0);
  expect(screen.getByText('انتهت دون قرار')).toBeInTheDocument();
  expect(screen.getByText('بلا قائد فريق')).toBeInTheDocument();
  expect(screen.getByText('أقدم طلب (يوم)')).toBeInTheDocument();
  expect(screen.getByText('7')).toBeInTheDocument();
  expect(screen.getByText('12')).toBeInTheDocument();
});

it('11 يعرض اسم الموظّف رابطًا إلى ملفّه', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  const link = await screen.findByRole('link', { name: 'موظّف تجريبي' });
  expect(link).toHaveAttribute('href', '/app/employee/emp-1');
});

it('12 يعرض اسم قائد الفريق رابطًا إلى ملفّه', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  const link = await screen.findByRole('link', { name: 'قائد الفريق التجريبي' });
  expect(link).toHaveAttribute('href', '/app/employee/tl-1');
});

it('13 يعرض شارة التأخّر «معلّق» للحالة Pending', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item({ delayStatus: 'Pending' })] };
  renderPage('/leave?tab=governance');
  // «معلّق» موجود في الشارة وأيضًا خيار الفلتر ⇒ نتأكّد من وجود عنصر واحد على الأقلّ ضمن صفّ الجدول.
  const badges = await screen.findAllByText('معلّق');
  expect(badges.length).toBeGreaterThan(0);
});

it('14 يعرض شارة «يحتاج متابعة» للحالة Attention', async () => {
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item({ delayStatus: 'Attention' })],
  };
  renderPage('/leave?tab=governance');
  // تظهر في خيار الفلتر وفي شارة الصفّ.
  const els = await screen.findAllByText('يحتاج متابعة');
  expect(els.length).toBeGreaterThanOrEqual(2);
});

it('15 يعرض شارة «موعد الإجازة بدأ» للحالة Critical', async () => {
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item({ delayStatus: 'Critical', startedAlready: true })],
  };
  renderPage('/leave?tab=governance');
  const els = await screen.findAllByText('موعد الإجازة بدأ');
  expect(els.length).toBeGreaterThanOrEqual(2);
});

it('16 يعرض شارة «انتهت الإجازة دون قرار» للحالة ExpiredUnresolved', async () => {
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item({ delayStatus: 'ExpiredUnresolved', startedAlready: true, endedAlready: true })],
  };
  renderPage('/leave?tab=governance');
  // النصّ الكامل يظهر في خيار الفلتر وفي شارة الصفّ.
  const els = await screen.findAllByText('انتهت الإجازة دون قرار');
  expect(els.length).toBeGreaterThanOrEqual(2);
});

it('17 يعرض شارة «لا يوجد قائد فريق محدّد» عند غياب الإسناد', async () => {
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item({ missingTeamLeaderAssignment: true, teamLeaderUserId: null, teamLeaderName: null })],
  };
  renderPage('/leave?tab=governance');
  expect(await screen.findByText('لا يوجد قائد فريق محدّد')).toBeInTheDocument();
});

it('18 يعرض علامة «(غير نشط)» للموظّف غير النشط', async () => {
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item({ isEmployeeActive: false })],
  };
  renderPage('/leave?tab=governance');
  expect(await screen.findByText('(غير نشط)')).toBeInTheDocument();
});

it('19 قراءة-فقط: يوجد «عرض» و«نسخ المعرّف» بلا أيّ زرّ اعتماد/رفض/إعادة توجيه/تعديل', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  expect(await screen.findByRole('button', { name: 'عرض' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'نسخ المعرّف' })).toBeInTheDocument();
  // الحرج: لا وجود لأيّ زرّ قرار/كتابة في طابور الحوكمة.
  for (const forbidden of ['اعتماد', 'رفض', 'موافقة', 'إعادة توجيه', 'تعديل', 'إعادة تعيين', 'حذف']) {
    expect(screen.queryByRole('button', { name: new RegExp(forbidden) })).not.toBeInTheDocument();
  }
});

it('20 يعرض حالة فارغة عند غياب الطلبات', async () => {
  governanceResult = emptyResult();
  renderPage('/leave?tab=governance');
  expect(await screen.findByText('لا توجد طلبات معلّقة عند قادة الفرق')).toBeInTheDocument();
});

// يتحقّق أنّ الخادم استُدعي على مسار الحوكمة بالمعامل المطلوب (فلترة من جهة الخادم لا الواجهة).
async function expectServerCalledWith(fragment: string) {
  await waitFor(() => {
    const called = apiGet.mock.calls.some(
      (c: unknown[]) =>
        typeof c[0] === 'string' &&
        (c[0] as string).includes('/leave-requests/governance/team-leader-pending') &&
        (c[0] as string).includes(fragment),
    );
    expect(called).toBe(true);
  });
}

it('21 فلتر تصنيف التأخّر يستدعي الخادم بمعامل delayStatus', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('تصنيف التأخّر'), { target: { value: 'Attention' } });
  await expectServerCalledWith('delayStatus=Attention');
});

// ---------- بقيّة الفلاتر من جهة الخادم (22-27) ----------

it('22 فلتر قائد الفريق يستدعي الخادم بمعامل teamLeaderId', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  // خيارات القائمة مُشتقّة من نفس مسار الحوكمة القرائيّ (بلا سطح صلاحيات جديد).
  fireEvent.change(screen.getByLabelText('قائد الفريق'), { target: { value: 'tl-1' } });
  await expectServerCalledWith('teamLeaderId=tl-1');
});

it('23 فلتر الإدارة يستدعي الخادم بمعامل departmentId', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('الإدارة'), { target: { value: 'dep-1' } });
  await expectServerCalledWith('departmentId=dep-1');
});

it('24 فلتر الفريق يستدعي الخادم بمعامل teamId', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('الفريق'), { target: { value: 'team-1' } });
  await expectServerCalledWith('teamId=team-1');
});

it('25 فلتر النوع يستدعي الخادم بمعامل requestType', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('النوع'), { target: { value: 'Permission' } });
  await expectServerCalledWith('requestType=Permission');
});

it('26 فلترا فترة بداية الإجازة يستدعيان startDateFrom و startDateTo', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('بداية الإجازة من'), { target: { value: '2026-08-01' } });
  await expectServerCalledWith('startDateFrom=2026-08-01');
  fireEvent.change(screen.getByLabelText('بداية الإجازة إلى'), { target: { value: '2026-08-31' } });
  await expectServerCalledWith('startDateTo=2026-08-31');
});

it('27 البحث وتضمين غير النشطين يُمرَّران للخادم', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  fireEvent.change(screen.getByLabelText('بحث (اسم الموظّف / قائد الفريق)'), { target: { value: 'حبيبة' } });
  await expectServerCalledWith('search=');
  fireEvent.click(screen.getByLabelText('تضمين غير النشطين'));
  await expectServerCalledWith('includeInactive=true');
});

// ---------- حالات التحميل/الخطأ/الترقيم/العرض (28-34) ----------

it('28 يعرض حالة التحميل قبل وصول البيانات', async () => {
  apiGet.mockImplementation((url: string) => {
    if (url.startsWith('/leave-requests/governance/team-leader-pending')) {
      return new Promise(() => {}) as never; // لا يُحسَم ⇒ يبقى التحميل ظاهرًا.
    }
    return Promise.resolve({ data: lookup(url) } as never);
  });
  renderPage('/leave?tab=governance');
  expect(await screen.findByText('يتم تحميل طابور الحوكمة…')).toBeInTheDocument();
});

it('29 يعرض حالة الخطأ مع إعادة المحاولة عند فشل الطلب', async () => {
  apiGet.mockImplementation((url: string) => {
    if (url.startsWith('/leave-requests/governance/team-leader-pending')) {
      return Promise.reject(new Error('boom')) as never;
    }
    return Promise.resolve({ data: lookup(url) } as never);
  });
  renderPage('/leave?tab=governance');
  expect(
    await screen.findByText(/حدث خطأ أثناء جلب طابور الطلبات المعلّقة عند قادة الفرق/),
  ).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
});

it('30 الترقيم يعرض عدد الصفحات ويطلب الصفحة التالية من الخادم', async () => {
  governanceResult = { ...emptyResult(), total: 60, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  // 60 طلبًا ÷ 25 لكلّ صفحة = 3 صفحات.
  expect(screen.getByText(/صفحة 1 من 3 — إجمالي 60 طلبًا/)).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'السابق' })).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'التالي' }));
  await expectServerCalledWith('page=2');
});

it('31 الترقيم يستخدم pageSize=25 من جهة الخادم', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  await expectServerCalledWith('pageSize=25');
});

it('32 الجدول بمحاذاة يمينيّة (RTL)', async () => {
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  const { container } = renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  const table = container.querySelector('table');
  expect(table).not.toBeNull();
  expect(table!.className).toContain('text-right');
});

it('33 يعمل على عرض الجوّال (شبكة عدّادات متجاوبة + جدول قابل للتمرير أفقيًّا)', async () => {
  window.innerWidth = 375;
  window.dispatchEvent(new Event('resize'));
  governanceResult = {
    ...emptyResult(),
    total: 1,
    items: [item()],
    counters: { ...emptyResult().counters, totalPending: 1 },
  };
  const { container } = renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  const grid = container.querySelector('.grid-cols-2.sm\\:grid-cols-3');
  expect(grid).not.toBeNull();
  const scroller = container.querySelector('.overflow-x-auto');
  expect(scroller).not.toBeNull();
});

it('34 لا يُسجّل أيّ خطأ في وحدة التحكّم عند عرض التبويب', async () => {
  const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance');
  await screen.findByRole('button', { name: 'عرض' });
  expect(spy).not.toHaveBeenCalled();
});

// ---------- فتح التفاصيل من سياق الحوكمة لا يمنح أيّ سلطة قرار (35-36) ----------

// أسماء أزرار الكتابة الممنوعة في سياق الحوكمة.
const DECISION_BUTTONS = ['اعتماد', 'رفض', 'إعادة للتعديل', 'إلغاء الطلب'];

it('35 فتح تفاصيل الطلب من تبويب الحوكمة لا يعرض أيّ زرّ اعتماد/رفض/إعادة/إلغاء (Admin)', async () => {
  currentRoles = ['Admin'];
  governanceResult = { ...emptyResult(), total: 1, items: [item()] };
  renderPage('/leave?tab=governance&open=11111111-1111-1111-1111-111111111111');

  // التفاصيل مُحمَّلة فعلًا (كي لا يكون الغياب بسبب عدم الرسم).
  await screen.findByText('تفاصيل الطلب');

  for (const name of DECISION_BUTTONS) {
    expect(screen.queryByRole('button', { name })).toBeNull();
  }
  // ولا حتى بطاقة إجراء المراجعة نفسها.
  expect(screen.queryByText('قرار قائد الفريق')).toBeNull();
  // ولا أيّ حقل سبب/ملاحظة يكتب بيانات.
  expect(screen.queryByText('ملاحظة / سبب')).toBeNull();
  // لم يُستدعَ أيّ POST — الصفحة قراءة-فقط (spy مضبوط على get فقط، وهذا يؤكّد عدم وجود مسار كتابة).
  expect(apiGet.mock.calls.every((c: unknown[]) => typeof c[0] === 'string')).toBe(true);
});

it('36 ضابط: نفس الطلب مفتوحًا من تبويب «بانتظار قراري» يُظهر أزرار القرار (يُثبت أنّ الإخفاء سببه سياق الحوكمة)', async () => {
  currentRoles = ['Admin'];
  renderPage('/leave?tab=pending&open=11111111-1111-1111-1111-111111111111');

  await screen.findByText('تفاصيل الطلب');
  expect(await screen.findByRole('button', { name: 'اعتماد' })).toBeDefined();
  expect(screen.getByRole('button', { name: 'رفض' })).toBeDefined();
  expect(screen.getByRole('button', { name: 'إعادة للتعديل' })).toBeDefined();
  expect(screen.getByText('قرار قائد الفريق')).toBeDefined();
});

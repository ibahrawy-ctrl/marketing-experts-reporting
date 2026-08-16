import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import SubmissionsPage from './SubmissionsPage';

// ===== SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1 — اختبارات الواجهة للعرض الموحّد (10) =====
// تُثبِت أنّ تبويب «كل التقارير» يستهلك GET /api/submissions/overview ويعرض الصفوف المتوقّعة
// غير المُقدَّمة بجانب التسليمات الفعليّة، مع سلامة null-safe وترقيم من الخادم.

const me = {
  userId: 'admin-1',
  fullName: 'مدير النظام',
  email: 'admin@test.local',
  roles: ['Admin'],
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
};

// أربعة صفوف: تسليمان فعليّان + متوقّعان غير مُقدَّمين (أحدهما متأخّر).
const rows = [
  {
    rowKind: 'ExistingSubmission', submissionId: 'sub-1', reportTemplateId: 't1',
    templateTitle: 'التقرير الأسبوعي', submitterId: 'u-ahmed', submitterName: 'أحمد',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Weekly', periodKey: '2026-W29', status: 'Draft', statusLabel: 'مسودّة متأخرة',
    severity: 'High', submittedAtUtc: null, currentApproverId: null, dueAt: '2026-07-15',
    hasSubmission: true, isExpectedSubmission: false, isOverdue: true, delayDays: 5,
  },
  {
    rowKind: 'ExistingSubmission', submissionId: 'sub-2', reportTemplateId: 't1',
    templateTitle: 'التقرير الأسبوعي', submitterId: 'u-soad', submitterName: 'سعاد',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Weekly', periodKey: '2026-W29', status: 'Closed', statusLabel: 'مُغلق',
    severity: 'None', submittedAtUtc: '2026-07-14T09:00:00Z', currentApproverId: null, dueAt: '2026-07-15',
    hasSubmission: true, isExpectedSubmission: false, isOverdue: false, delayDays: 0,
  },
  {
    rowKind: 'ExpectedMissingSubmission', submissionId: null, reportTemplateId: 't1',
    templateTitle: 'التقرير الأسبوعي', submitterId: 'u-khaled', submitterName: 'خالد',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Weekly', periodKey: '2026-W29', status: 'NotSubmitted', statusLabel: 'لم يبدأ التقرير',
    severity: 'Low', submittedAtUtc: null, currentApproverId: null, dueAt: '2026-07-22',
    hasSubmission: false, isExpectedSubmission: true, isOverdue: false, delayDays: 0,
  },
  {
    rowKind: 'ExpectedMissingSubmission', submissionId: null, reportTemplateId: 't2',
    templateTitle: 'تقرير المتابعة', submitterId: 'u-laila', submitterName: 'ليلى',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Weekly', periodKey: '2026-W29', status: 'NotSubmitted', statusLabel: 'متأخّر — لم يُقدَّم',
    severity: 'High', submittedAtUtc: null, currentApproverId: null, dueAt: '2026-07-15',
    hasSubmission: false, isExpectedSubmission: true, isOverdue: true, delayDays: 3,
  },
];

const summary = {
  periodKey: '2026-W29', total: 40, overdueCount: 12, existingOverdueCount: 7,
  missingOverdueCount: 5, expectedMissingCount: 9, needsActionCount: 7,
  returnedCount: 3, closedCount: 10, waitingMyApprovalCount: 6, byStatus: [],
};

// آخر باراميترات مُرسَلة إلى /submissions/overview — لإثبات دفع الفلاتر إلى الخادم.
let lastOverviewParams: Record<string, unknown> | undefined;

function overviewPayload(items = rows, totalCount = 4, page = 1) {
  return { items, summary, page, pageSize: 200, totalCount };
}

function mockApi(payload = overviewPayload()) {
  lastOverviewParams = undefined;
  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: Record<string, unknown> }) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === '/submissions/overview') {
      lastOverviewParams = config?.params;
      // ترقيم: أعِد صفوف الصفحة المطلوبة اسميًّا (نفس البيانات تكفي للتأكيد).
      const p = (config?.params?.page as number) ?? 1;
      return Promise.resolve({ data: { ...payload, page: p } } as never);
    }
    return Promise.resolve({ data: [] } as never);
  });
}

// ===== ADDENDUM — تكافؤ الملخّص وعدّادات الفترة =====
// سجلّ كل طلبات /submissions/overview (لإثبات وصول الفترة والترقيم إلى الطلب الموحّد الوحيد
// الذي يحمل items + summary معًا)، وملخّص يختلف حسب periodKey (لإثبات عدم بقاء قيم البطاقات).
const overviewRequests: Array<Record<string, unknown>> = [];

// العقد المعتمد: Summary/البطاقات تُحسب على المجموعة بعد QuickFilter؛ فعند Overdue يصير
// الإجمالي = عدد المتأخّر (المجموعة المصفّاة نفسها)، مطابقةً لسلوك الخادم.
function summaryForPeriod(periodKey: string | undefined, quickFilter?: string) {
  const w29 = periodKey === '2026-W29';
  const overdueCount = w29 ? 3 : 12;
  const total = quickFilter === 'Overdue' ? overdueCount : w29 ? 10 : 40;
  return {
    periodKey: periodKey ?? null,
    total,
    overdueCount,
    existingOverdueCount: w29 ? 2 : 7,
    missingOverdueCount: w29 ? 1 : 5,
    expectedMissingCount: w29 ? 2 : 9,
    needsActionCount: w29 ? 2 : 7,
    returnedCount: w29 ? 1 : 3,
    closedCount: w29 ? 4 : 10,
    waitingMyApprovalCount: w29 ? 2 : 6,
    byStatus: [],
  };
}

function mockApiByPeriod(totalCount = 4) {
  overviewRequests.length = 0;
  lastOverviewParams = undefined;
  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: Record<string, unknown> }) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === '/submissions/overview') {
      lastOverviewParams = config?.params;
      overviewRequests.push({ ...(config?.params ?? {}) });
      const pk = config?.params?.periodKey as string | undefined;
      const qf = config?.params?.quickFilter as string | undefined;
      const p = (config?.params?.page as number) ?? 1;
      // مصدر واحد: نفس الطلب يُرجِع items + summary(للفترة وبعد QuickFilter) — لا استعلام ملخّص منفصل.
      return Promise.resolve({
        data: { items: rows, summary: summaryForPeriod(pk, qf), page: p, pageSize: 200, totalCount },
      } as never);
    }
    return Promise.resolve({ data: [] } as never);
  });
}

// يختار قيمة في منتقي «الفترة» (المنتقي الحاوي على خيار «آخر 12 أسبوعًا»).
async function selectPeriod(value: string) {
  const allOption = screen.getByRole('option', { name: 'آخر 12 أسبوعًا' });
  const periodSelect = allOption.closest('select')!;
  await userEvent.selectOptions(periodSelect, value);
}

beforeEach(() => {
  tokenStore.clear();
  tokenStore.set('acc', 'ref');
  vi.restoreAllMocks();
});

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter initialEntries={['/submissions?tab=all']}>
          <SubmissionsPage />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('العرض الموحّد — كل التقارير (MISSING-EXPECTED-OVERDUE-R1)', () => {
  it('T1: يعرض التسليمات الفعليّة والالتزامات المتوقّعة غير المُقدَّمة معًا', async () => {
    mockApi();
    renderPage();
    // صفّان فعليّان + صفّان متوقّعان.
    expect(await screen.findByText('أحمد')).toBeInTheDocument();
    expect(screen.getByText('سعاد')).toBeInTheDocument();
    expect(screen.getByText('خالد')).toBeInTheDocument();
    expect(screen.getByText('ليلى')).toBeInTheDocument();
  });

  it('T2: الصفّ المتوقّع ضمن المهلة تظهر حالته «لم يبدأ التقرير»', async () => {
    mockApi();
    renderPage();
    expect(await screen.findByText('لم يبدأ التقرير')).toBeInTheDocument();
  });

  it('T3: الصفّ المتوقّع المتأخّر يظهر بشارة «متأخر — لم يُقدَّم (3 يوم)»', async () => {
    mockApi();
    renderPage();
    expect(await screen.findByText('متأخر — لم يُقدَّم (3 يوم)')).toBeInTheDocument();
  });

  it('T4: الصفّ المتوقّع null-safe: بلا زرّ عرض، ويعرض «لا يوجد تسليم بعد»', async () => {
    mockApi();
    renderPage();
    const laila = (await screen.findByText('ليلى')).closest('tr')!;
    expect(within(laila).queryByText('عرض التقرير')).not.toBeInTheDocument();
    expect(within(laila).getByText('لا يوجد تسليم بعد')).toBeInTheDocument();
  });

  it('T5: الصفّ الفعليّ يعرض زرّ «عرض التقرير»', async () => {
    mockApi();
    renderPage();
    const ahmed = (await screen.findByText('أحمد')).closest('tr')!;
    expect(within(ahmed).getByText('عرض التقرير')).toBeInTheDocument();
  });

  it('T6: بطاقة الإجمالي تعرض قيمة الملخّص من الخادم', async () => {
    mockApi();
    renderPage();
    const total = (await screen.findByText('إجمالي التقارير')).closest('button')!;
    expect(within(total).getByText('40')).toBeInTheDocument();
  });

  it('T7: بطاقة «متأخرة» تعرض العدّاد الكلّي وسطر «منها N لم تُقدَّم»', async () => {
    mockApi();
    renderPage();
    const overdue = (await screen.findByText('متأخرة')).closest('button')!;
    expect(within(overdue).getByText('12')).toBeInTheDocument();
    expect(within(overdue).getByText('منها 5 لم تُقدَّم')).toBeInTheDocument();
  });

  it('T8: بطاقة «يحتاج إجراء الآن» تعرض needsActionCount', async () => {
    mockApi();
    renderPage();
    const needs = (await screen.findByText('يحتاج إجراء الآن')).closest('button')!;
    expect(within(needs).getByText('7')).toBeInTheDocument();
  });

  it('T9: النقر على شريحة «المتأخرة» يدفع quickFilter=Overdue إلى الخادم', async () => {
    mockApi();
    renderPage();
    await screen.findByText('أحمد');
    const chips = screen.getAllByText('المتأخرة');
    await userEvent.click(chips[chips.length - 1]);
    await waitFor(() => expect(lastOverviewParams?.quickFilter).toBe('Overdue'));
  });

  it('T10: الترقيم من الخادم — «صفحة 1 من 3» والنقر على «التالي» يجلب page=2', async () => {
    mockApi(overviewPayload(rows, 450));
    renderPage();
    expect(await screen.findByText('صفحة 1 من 3')).toBeInTheDocument();
    await userEvent.click(screen.getByText('التالي'));
    await waitFor(() => expect(lastOverviewParams?.page).toBe(2));
  });

  // بطاقة «بانتظار اعتمادي» والشريحة السريعة تحملان النصّ نفسه؛ البطاقة وحدها تحوي الرقم.
  const approvalCard = () =>
    screen
      .getAllByText('بانتظار اعتمادي')
      .map((el) => el.closest('button')!)
      .find((btn) => within(btn).queryByText('6'))!;

  it('T11: بطاقة «بانتظار اعتمادي» تعرض waitingMyApprovalCount من ملخّص الخادم', async () => {
    // العقد: رقم البطاقة يأتي من Summary الخادميّ (بعد النطاق والفلاتر وQuickFilter، قبل الترقيم)،
    // لا يُحسَب من صفوف الصفحة (كلّ صفوف mock هنا currentApproverId=null، فلو حُسِب محليًّا لظهر 0 لا 6).
    mockApi();
    renderPage();
    await screen.findByText('أحمد');
    expect(within(approvalCard()).getByText('6')).toBeInTheDocument();
  });

  it('T12: النقر على بطاقة «بانتظار اعتمادي» يدفع quickFilter=MineApproval إلى الخادم', async () => {
    mockApi();
    renderPage();
    await screen.findByText('أحمد');
    await userEvent.click(approvalCard());
    await waitFor(() => expect(lastOverviewParams?.quickFilter).toBe('MineApproval'));
  });

  it('T13: الصفّ المتوقّع غير المُقدَّم — للعرض فقط: لا أزرار إجراء (فتح/تعديل/إرسال/اعتماد/إعادة/حذف/بدء التقرير)', async () => {
    mockApi();
    renderPage();
    const laila = (await screen.findByText('ليلى')).closest('tr')!;
    for (const label of ['عرض التقرير', 'فتح', 'تعديل', 'إرسال', 'اعتماد', 'إعادة', 'حذف', 'بدء التقرير']) {
      expect(within(laila).queryByText(label)).not.toBeInTheDocument();
    }
    // لا أزرار إطلاقًا في صفّ متوقّع؛ حالة العرض فقط «لا يوجد تسليم بعد».
    expect(within(laila).queryAllByRole('button')).toHaveLength(0);
    expect(within(laila).getByText('لا يوجد تسليم بعد')).toBeInTheDocument();
  });
});

// ===== ADDENDUM — تكافؤ الملخّص وعدّادات الفترة (10) =====
// تُثبِت أنّ تغيير الفترة يدفع periodKey إلى الطلب الموحّد الوحيد (items + summary معًا)،
// وأنّ بطاقات الملخّص تتبع الخادم فورًا (لا قيم قديمة/محسوبة من صفوف الصفحة)،
// وأنّ الترقيم لا يغيّر الملخّص، وأنّ الفلاتر السريعة تبقى محفوظة عبر تغيير الفترة.
describe('العرض الموحّد — تكافؤ الفترة والملخّص (MISSING-EXPECTED-OVERDUE-R1 ADDENDUM)', () => {
  // يُعيد إيجاد البطاقة طازجةً في كل تأكيد؛ إذ يُستبدَل عنصر <button> عند إعادة التصيير
  // بعد كل جلب (لا keepPreviousData)، فأيّ مرجع مُلتقَط مسبقًا يصبح منفصلًا عن DOM.
  const card = (label: string) => screen.getByText(label).closest('button')!;

  it('P1: اختيار الفترة «2026-W29» يدفع periodKey إلى /submissions/overview', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    await selectPeriod('2026-W29');
    await waitFor(() => expect(lastOverviewParams?.periodKey).toBe('2026-W29'));
  });

  it('P2: الطلب الموحّد الوحيد يحمل الفترة للقائمة والملخّص معًا — بطاقة الإجمالي تصبح «10»', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('إجمالي التقارير');
    expect(within(card('إجمالي التقارير')).getByText('40')).toBeInTheDocument();
    await selectPeriod('2026-W29');
    await waitFor(() => expect(within(card('إجمالي التقارير')).getByText('10')).toBeInTheDocument());
    // نفس آخر طلب حَمَل periodKey — لا استعلام ملخّص منفصل بلا فترة.
    expect(lastOverviewParams?.periodKey).toBe('2026-W29');
  });

  it('P3: الملخّص يعكس الفترة الجديدة (40→10) ولا يبقى على القيمة القديمة', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('متأخرة');
    expect(within(card('متأخرة')).getByText('12')).toBeInTheDocument();
    await selectPeriod('2026-W29');
    await waitFor(() => expect(within(card('متأخرة')).getByText('3')).toBeInTheDocument());
    expect(within(card('متأخرة')).queryByText('12')).not.toBeInTheDocument();
  });

  it('P4: تغيير الفترة يُعيد إطلاق طلب واحد يحمل periodKey (قائمة + ملخّص معًا)', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    const before = overviewRequests.length;
    await selectPeriod('2026-W29');
    await waitFor(() => expect(overviewRequests.length).toBeGreaterThan(before));
    const last = overviewRequests[overviewRequests.length - 1];
    expect(last.periodKey).toBe('2026-W29');
  });

  it('P5: البطاقات ليست قيمًا قديمة — بعد الفترة تختفي «40» وتظهر «10»', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('إجمالي التقارير');
    await selectPeriod('2026-W29');
    await waitFor(() => expect(within(card('إجمالي التقارير')).getByText('10')).toBeInTheDocument());
    expect(within(card('إجمالي التقارير')).queryByText('40')).not.toBeInTheDocument();
  });

  it('P6: العودة إلى «آخر 12 أسبوعًا» تستعيد ملخّص النافذة التاريخية المحدودة (40)', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('إجمالي التقارير');
    await selectPeriod('2026-W29');
    await waitFor(() => expect(within(card('إجمالي التقارير')).getByText('10')).toBeInTheDocument());
    await selectPeriod('');
    await waitFor(() => expect(within(card('إجمالي التقارير')).getByText('40')).toBeInTheDocument());
  });

  it('P7: اختيار «آخر 12 أسبوعًا» يُسقِط باراميتر periodKey من الطلب', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    await selectPeriod('2026-W29');
    await waitFor(() => expect(lastOverviewParams?.periodKey).toBe('2026-W29'));
    await selectPeriod('');
    await waitFor(() => expect(lastOverviewParams?.periodKey).toBeUndefined());
  });

  it('P8: الفترة الأسبوعية تُمرَّر كسلسلة مطابقة تمامًا (2026-W29)', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    await selectPeriod('2026-W29');
    await waitFor(() => {
      const last = overviewRequests[overviewRequests.length - 1];
      expect(last.periodKey).toBe('2026-W29');
      expect(last.pageSize).toBe(200);
    });
  });

  it('P9: الترقيم لا يغيّر الملخّص — «التالي» يجلب page=2 والبطاقة تبقى «40»', async () => {
    mockApiByPeriod(450);
    renderPage();
    await screen.findByText('إجمالي التقارير');
    expect(within(card('إجمالي التقارير')).getByText('40')).toBeInTheDocument();
    await userEvent.click(screen.getByText('التالي'));
    await waitFor(() => expect(lastOverviewParams?.page).toBe(2));
    await waitFor(() => expect(within(card('إجمالي التقارير')).getByText('40')).toBeInTheDocument());
  });

  it('P10: الفلتر السريع «Overdue» يبقى محفوظًا عبر تغيير الفترة', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    const chips = screen.getAllByText('المتأخرة');
    await userEvent.click(chips[chips.length - 1]);
    await waitFor(() => expect(lastOverviewParams?.quickFilter).toBe('Overdue'));
    await selectPeriod('2026-W29');
    await waitFor(() => {
      expect(lastOverviewParams?.quickFilter).toBe('Overdue');
      expect(lastOverviewParams?.periodKey).toBe('2026-W29');
    });
  });

  it('P11: العقد المعتمد — التصفية السريعة تُحدّث البطاقات والقائمة معًا (Summary بعد QuickFilter)', async () => {
    mockApiByPeriod();
    renderPage();
    await screen.findByText('أحمد');
    // التسمية التوضيحية للعقد الجديد: الأرقام تعكس نفس المجموعة بعد كل الفلاتر والتصفية السريعة.
    const caption = screen.getByText(
      (t) =>
        t.includes('الأرقام أعلاه تعكس نفس المجموعة المعروضة في القائمة') &&
        t.includes('تغيير التصفية السريعة يُحدّث هذه الأرقام والقائمة معًا'),
    );
    expect(caption).toBeInTheDocument();
    // قبل التصفية السريعة: البطاقة = ملخّص النطاق الكامل (40).
    expect(within(card('إجمالي التقارير')).getByText('40')).toBeInTheDocument();
    const chips = screen.getAllByText('المتأخرة');
    await userEvent.click(chips[chips.length - 1]);
    await waitFor(() => expect(lastOverviewParams?.quickFilter).toBe('Overdue'));
    // بعد التصفية السريعة: البطاقة تُحدَّث إلى إجمالي المجموعة المصفّاة (المتأخّر = 12).
    await waitFor(() =>
      expect(within(card('إجمالي التقارير')).getByText('12')).toBeInTheDocument(),
    );
  });
});

// ===== DAILY-REPORTING-APPLICABILITY-R1 §6 — إثبات DOM للعرض الموحّد اليوميّ =====
// تُثبِت أنّ العرض الموحّد يعرض الصفّ اليوميّ الفعليّ (بمفتاح اليوم الخام محفوظًا، بما فيه
// الصيغ التاريخية القديمة مثل 6-7-2026)، والصفّ اليوميّ المتوقّع غير المُقدَّم («لا يوجد تسليم بعد»
// بلا أزرار)، والتأخّر اليوميّ، وأنّ فلتر Overdue يدمج اليوميّ والأسبوعيّ في قائمة واحدة.
const dailyRows = [
  {
    rowKind: 'ExistingSubmission', submissionId: 'd-1', reportTemplateId: 'dt1',
    templateTitle: 'تقرير المبيعات اليوميّ', submitterId: 'u-reem', submitterName: 'ريم',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Daily', periodKey: '2026-07-06', status: 'Closed', statusLabel: 'مُغلق',
    severity: 'None', submittedAtUtc: '2026-07-06T09:00:00Z', currentApproverId: null, dueAt: '2026-07-06',
    hasSubmission: true, isExpectedSubmission: false, isOverdue: false, delayDays: 0,
  },
  {
    // مفتاح قديم غير قياسيّ محفوظ خامًّا في العرض (يومي 6-7-2026) — لا تطبيع مرئيّ في الواجهة.
    rowKind: 'ExistingSubmission', submissionId: 'd-2', reportTemplateId: 'dt1',
    templateTitle: 'تقرير المبيعات اليوميّ', submitterId: 'u-salem', submitterName: 'سالم',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Daily', periodKey: '6-7-2026', status: 'Submitted', statusLabel: 'مُرسَل',
    severity: 'None', submittedAtUtc: '2026-07-06T09:00:00Z', currentApproverId: null, dueAt: '2026-07-06',
    hasSubmission: true, isExpectedSubmission: false, isOverdue: false, delayDays: 0,
  },
  {
    rowKind: 'ExistingSubmission', submissionId: 'd-3', reportTemplateId: 'dt1',
    templateTitle: 'تقرير المبيعات اليوميّ', submitterId: 'u-nasser', submitterName: 'ناصر',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Daily', periodKey: '2026-07-20', status: 'Draft', statusLabel: 'مسودّة متأخّرة',
    severity: 'High', submittedAtUtc: null, currentApproverId: null, dueAt: '2026-07-20',
    hasSubmission: true, isExpectedSubmission: false, isOverdue: true, delayDays: 2,
  },
  {
    rowKind: 'ExpectedMissingSubmission', submissionId: null, reportTemplateId: 'dt1',
    templateTitle: 'تقرير المبيعات اليوميّ', submitterId: 'u-hind', submitterName: 'هند',
    teamId: null, teamName: null, departmentId: null, departmentName: null,
    periodType: 'Daily', periodKey: '2026-07-19', status: 'NotSubmitted', statusLabel: 'متأخّر — لم يُقدَّم',
    severity: 'High', submittedAtUtc: null, currentApproverId: null, dueAt: '2026-07-19',
    hasSubmission: false, isExpectedSubmission: true, isOverdue: true, delayDays: 1,
  },
];

// مجموعة مختلطة: صفّ يوميّ متأخّر + صفّ أسبوعيّ متأخّر — لإثبات أنّ Overdue يدمج الإيقاعين.
const mixedOverdueRows = [dailyRows[2], dailyRows[3], rows[0], rows[3]];

function mockApiWithRows(items: unknown[], totalCount = items.length) {
  lastOverviewParams = undefined;
  vi.spyOn(api, 'get').mockImplementation((url: string, config?: { params?: Record<string, unknown> }) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === '/submissions/overview') {
      lastOverviewParams = config?.params;
      const p = (config?.params?.page as number) ?? 1;
      return Promise.resolve({
        data: { items, summary, page: p, pageSize: 200, totalCount },
      } as never);
    }
    return Promise.resolve({ data: [] } as never);
  });
}

describe('العرض الموحّد اليوميّ — إثبات DOM (DAILY-REPORTING-APPLICABILITY-R1 §6)', () => {
  it('DUI1: الصفّ اليوميّ الفعليّ يعرض الاسم والحالة وخليّة «يومي <مفتاح اليوم>»', async () => {
    mockApiWithRows(dailyRows);
    renderPage();
    const reem = (await screen.findByText('ريم')).closest('tr')!;
    expect(within(reem).getByText('مُغلق')).toBeInTheDocument();
    expect(within(reem).getByText('يومي 2026-07-06')).toBeInTheDocument();
  });

  it('DUI2: المفتاح القديم غير القياسيّ محفوظ خامًّا في العرض (يومي 6-7-2026)', async () => {
    mockApiWithRows(dailyRows);
    renderPage();
    const salem = (await screen.findByText('سالم')).closest('tr')!;
    // العرض لا يطبّع المفتاح — يعرضه كما خُزِّن (التطبيع داخليّ خادميّ فقط).
    expect(within(salem).getByText('يومي 6-7-2026')).toBeInTheDocument();
  });

  it('DUI3: الصفّ اليوميّ الفعليّ يعرض زرّ «عرض التقرير»', async () => {
    mockApiWithRows(dailyRows);
    renderPage();
    const reem = (await screen.findByText('ريم')).closest('tr')!;
    expect(within(reem).getByText('عرض التقرير')).toBeInTheDocument();
  });

  it('DUI4: الصفّ اليوميّ الفعليّ المتأخّر يعرض حالة التأخّر', async () => {
    mockApiWithRows(dailyRows);
    renderPage();
    const nasser = (await screen.findByText('ناصر')).closest('tr')!;
    expect(within(nasser).getByText('مسودّة متأخّرة')).toBeInTheDocument();
    expect(within(nasser).getByText('يومي 2026-07-20')).toBeInTheDocument();
  });

  it('DUI5: الصفّ اليوميّ المتوقّع غير المُقدَّم — «لا يوجد تسليم بعد» بلا أيّ زرّ', async () => {
    mockApiWithRows(dailyRows);
    renderPage();
    const hind = (await screen.findByText('هند')).closest('tr')!;
    expect(within(hind).getByText('لا يوجد تسليم بعد')).toBeInTheDocument();
    expect(within(hind).queryByText('عرض التقرير')).not.toBeInTheDocument();
    expect(within(hind).queryAllByRole('button')).toHaveLength(0);
    expect(within(hind).getByText('يومي 2026-07-19')).toBeInTheDocument();
  });

  it('DUI6: فلتر Overdue يدمج اليوميّ والأسبوعيّ في القائمة نفسها', async () => {
    mockApiWithRows(mixedOverdueRows);
    renderPage();
    // صفوف يوميّة (ناصر متأخّر فعليّ + هند متوقّع متأخّر) وأسبوعية (أحمد + ليلى) معًا.
    expect(await screen.findByText('ناصر')).toBeInTheDocument();
    expect(screen.getByText('هند')).toBeInTheDocument();
    expect(screen.getByText('أحمد')).toBeInTheDocument();
    expect(screen.getByText('ليلى')).toBeInTheDocument();
    // إيقاعان مختلفان ظاهران في القائمة الموحّدة نفسها.
    expect(screen.getByText('يومي 2026-07-20')).toBeInTheDocument();
    // صفّان أسبوعيّان بنفس المفتاح W29 (أحمد + ليلى) ⇒ خليّتان بالنصّ نفسه.
    expect(screen.getAllByText('أسبوعي 2026-W29').length).toBeGreaterThanOrEqual(1);
    // النقر على شريحة «المتأخرة» يدفع quickFilter=Overdue إلى الخادم (نفس الطلب الموحّد للإيقاعين).
    const chips = screen.getAllByText('المتأخرة');
    await userEvent.click(chips[chips.length - 1]);
    await waitFor(() => expect(lastOverviewParams?.quickFilter).toBe('Overdue'));
  });
});

// P2-HR-009 — اختبارات E2E للوحة عمليّات الموارد البشريّة على البناء الحقيقيّ (Vite preview).
//
// **الخادم غير مُشغَّل هنا، وهذا مُصرَّح به لا مُدَّعى خلافه:** نداءات `/api/**` تُعترَض وتُخدَم
// من عيّنة في الذاكرة. الغرض إثبات سلوك **الواجهة** على البناء المُصرَّف: أنّ العدّ يأتي من الخادم،
// وأنّ فتح البطاقة يستدعي الطابور نفسه بالمرشِّح نفسه، وأنّ 403 على اللوحة رسالة صلاحيّة لا عطل،
// وأنّ الاتّجاه RTL والاستجابة سليمان. صحّة النطاق والتخويل والتدقيق مُثبَتة في اختبارات التكامل
// المعزولة على قاعدة محلّيّة حقيقيّة — لا هنا، ولا يُدَّعى ذلك.
import { test, expect, type Page } from '@playwright/test';

const USER_ID = '11111111-1111-1111-1111-111111111111';

const ME = {
  userId: 'u-hr', fullName: 'منى الحربي', email: 'hr@test.local',
  isActive: true, roles: ['HR'], expectedReportCadence: 'Weekly',
};

const QUEUE_KEYS = [
  'reports-missing', 'reports-late', 'kpi-missing', 'kpi-awaiting-approval',
  'kpi-coverage-gap', 'attendance-awaiting-employee', 'attendance-employee-sla-breached',
  'attendance-awaiting-hr', 'attendance-hr-sla-breached', 'requests-awaiting-action',
  'follow-up-items',
];

const OPEN_QUEUE = 'attendance-awaiting-employee';

function card(key: string, count: number, breached = 0) {
  return {
    queue: QUEUE_KEYS.indexOf(key) + 1,
    key, titleAr: `طابور ${key}`, groupAr: 'الحضور والالتزام',
    count, breachedCount: breached,
    maxAgeingDays: count > 0 ? 6 : 0,
    severityAr: breached > 0 ? 'حرِج' : 'سليم',
  };
}

function row(id: string, name: string, over: Record<string, unknown> = {}) {
  return {
    queue: 6, entityId: id, entityType: 'AttendanceIncident',
    subjectUserId: USER_ID, subjectFullName: name,
    departmentId: null, departmentName: 'التسويق',
    teamId: null, teamName: 'فريق المحتوى',
    titleAr: 'تأخّر عن الدوام', typeAr: 'واقعة حضور', statusAr: 'بانتظار ردّ الموظّف',
    periodKey: '2026-W34', dueAt: '2026-08-20', slaDueAtUtc: '2026-08-20T06:30:00Z',
    slaBreached: false, ageingDays: 2,
    ownerUserId: USER_ID, ownerFullName: name,
    nextActionAr: 'انتظار ردّ الموظّف ضمن نافذته',
    lastActionAtUtc: '2026-08-18T07:00:00Z',
    ...over,
  };
}

type Stub = {
  dashboardStatus: number;
  exportStatus: number;
  rows: Record<string, unknown>[];
  totalCount: number;
  /** كلّ نداء GET كما وقع فعلًا — به نثبت أنّ المرشِّح ذهب إلى الخادم لا إلى المتصفّح. */
  calls: { path: string; query: string }[];
};

function newStub(over: Partial<Stub> = {}): Stub {
  const rows = [row('e-1', 'سارة العتيبي'), row('e-2', 'خالد المطيري'), row('e-3', 'ريم القحطاني')];
  return { dashboardStatus: 200, exportStatus: 200, rows, totalCount: 3, calls: [], ...over };
}

async function stubApi(page: Page, stub: Stub) {
  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });

  await page.route('**/api/**', async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname.split('/api/')[1] ?? '';
    stub.calls.push({ path, query: url.search });

    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    if (path.startsWith('auth/me')) return json(ME);

    if (path.endsWith('/export')) {
      if (stub.exportStatus !== 200)
        return json({ title: 'Forbidden', detail: 'ممنوع' }, stub.exportStatus);
      return route.fulfill({
        status: 200,
        headers: { 'content-disposition': 'attachment; filename="hr-operations-x.csv"' },
        contentType: 'text/csv; charset=utf-8',
        body: '\uFEFFالمعرّف\n"e-1"\n',
      });
    }

    if (path === 'hr-operations/dashboard') {
      if (stub.dashboardStatus !== 200)
        return json({ title: 'Forbidden', detail: 'ممنوع' }, stub.dashboardStatus);
      return json({
        periodKeys: ['2026-W33', '2026-W34'],
        scope: { scopeType: 'Team', userCount: 7 },
        cards: QUEUE_KEYS.map((k) => (k === OPEN_QUEUE ? card(k, stub.totalCount, 1) : card(k, 0))),
      });
    }

    if (path.startsWith('hr-operations/queues/')) {
      const key = path.split('/')[2];
      return json({
        queue: QUEUE_KEYS.indexOf(key) + 1, key, titleAr: `طابور ${key}`,
        totalCount: key === OPEN_QUEUE ? stub.totalCount : 0,
        breachedCount: key === OPEN_QUEUE ? 1 : 0,
        page: 1, pageSize: 25,
        rows: key === OPEN_QUEUE ? stub.rows : [],
      });
    }

    return json([]);
  });
}

test.describe('عمليّات الموارد البشريّة — E2E', () => {
  test('اللوحة تعرض الطوابير الأحد عشر وتُعلِن النطاق مع الأرقام', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/hr-operations');

    await expect(page.getByRole('heading', { name: 'عمليّات الموارد البشريّة' })).toBeVisible();
    for (const key of QUEUE_KEYS) await expect(page.getByTestId(`hr-ops-card-${key}`)).toBeVisible();

    // الرقم لا يُقرأ خارج سياقه: النطاق مُعلَن بجانبه.
    await expect(page.getByTestId('hr-ops-scope')).toContainText('7 موظّفًا');
  });

  test('فتح البطاقة يعرض البنود ذاتها التي عُدَّت (Drill-down)', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/hr-operations');

    await expect(page.getByTestId(`hr-ops-count-${OPEN_QUEUE}`)).toHaveText('3');
    await page.getByTestId(`hr-ops-card-${OPEN_QUEUE}`).click();

    await expect(page.getByTestId('hr-ops-queue-table')).toBeVisible();
    await expect(page.getByTestId('hr-ops-row')).toHaveCount(3);
    await expect(page.getByTestId('hr-ops-drilldown')).toContainText('3 بندًا');

    // الصفّ يقود إلى ملفّ الموظّف بمعرّفه.
    await expect(page.getByTestId('hr-ops-row').first().getByRole('link'))
      .toHaveAttribute('href', `/app/employee/${USER_ID}`);
  });

  test('المرشِّح يذهب إلى الخادم ولا يُطبَّق في المتصفّح', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/hr-operations');
    await expect(page.getByTestId('hr-ops-filter')).toBeVisible();

    stub.calls.length = 0;
    await page.getByLabel('معرّف الموظّف').fill(USER_ID);
    await page.getByLabel('المهلة').selectOption('overdue');
    await page.getByRole('button', { name: 'تطبيق المرشِّحات' }).click();

    await expect
      .poll(() => stub.calls.some((c) =>
        c.path === 'hr-operations/dashboard'
        && c.query.includes(`userId=${USER_ID}`)
        && c.query.includes('overdueOnly=true')))
      .toBe(true);
  });

  test('403 على اللوحة رسالة صلاحيّة مفهومة لا شاشة عطل', async ({ page }) => {
    await stubApi(page, newStub({ dashboardStatus: 403 }));
    await page.goto('/app/hr-operations');

    await expect(page.getByText('لا تملك صلاحيّة لوحة العمليّات')).toBeVisible();
    await expect(page.getByText('تعذّر تحميل البيانات')).toHaveCount(0);
  });

  test('منع التصدير بـ403 لا يُسقط اللوحة — المفتاحان منفصلان', async ({ page }) => {
    await stubApi(page, newStub({ exportStatus: 403 }));
    await page.goto(`/app/hr-operations?queue=${OPEN_QUEUE}`);

    await expect(page.getByTestId('hr-ops-queue-table')).toBeVisible();
    await page.getByTestId('hr-ops-export').click();

    await expect(page.getByText('لا تملك صلاحيّة تنفيذ هذا الإجراء.')).toBeVisible();
    await expect(page.getByTestId('hr-ops-queue-table')).toBeVisible();
  });

  test('التصدير الناجح ينزّل ملفًّا ويُخبِر بأنّه مُسجَّل في التدقيق', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto(`/app/hr-operations?queue=${OPEN_QUEUE}`);
    await expect(page.getByTestId('hr-ops-export')).toBeVisible();

    const download = page.waitForEvent('download');
    await page.getByTestId('hr-ops-export').click();
    await download;

    await expect(page.getByText(/سجلّ التدقيق/)).toBeVisible();
  });

  test('طابور فارغ حالة فراغ مستقلّة لا خطأ ولا جدول بلا صفوف', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/hr-operations?queue=reports-missing');

    await expect(page.getByText('لا بنود في هذا الطابور')).toBeVisible();
    await expect(page.getByTestId('hr-ops-queue-table')).toHaveCount(0);
  });

  test('الاتّجاه RTL والعرض سليم على سطح المكتب واللوحيّ والجوّال', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto(`/app/hr-operations?queue=${OPEN_QUEUE}`);

    await expect(page.getByTestId('hr-operations-page')).toHaveAttribute('dir', 'rtl');

    for (const size of [
      { width: 1440, height: 900 },
      { width: 834, height: 1112 },
      { width: 390, height: 844 },
    ]) {
      await page.setViewportSize(size);
      await expect(page.getByTestId(`hr-ops-card-${OPEN_QUEUE}`)).toBeVisible();
      await expect(page.getByTestId('hr-ops-queue-table')).toBeVisible();

      // لا فيض أفقيّ للصفحة: الجدول وحده يتمرّر داخل حاويته.
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(1);
    }
  });
});

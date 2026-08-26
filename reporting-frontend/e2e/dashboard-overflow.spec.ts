// DEF-P123-005 — لا تمرير أفقيّ للصفحة على أيّ مقاس، بلوحات **غير فارغة**.
//
// لماذا ملفّ جديد بدل توسيع navigation.spec.ts: ذاك يردّ 403 على كلّ نداءات البيانات عمدًا
// (سطر 41-44) فتُصيَّر كلّ البطاقات فارغة. العيب مشروط بالمحتوى لا بالدور — قِيس على adm
// (فائض 130px) و leadA1 (فائض 47px) بينما exec و empFull سليمان — ولذلك مرّ من تحت Playwright
// كاملًا في UAT. هنا نُطعم اللوحة بمحتوى عربيّ بأطوال واقعيّة مأخوذة من TEST، فيُعاد إنتاج
// العيب قبل الإصلاح ويُثبَت زواله بعده.
import { test, expect, type Page } from '@playwright/test';

const SIZES = [
  { name: 'الجوّال 390', width: 390, height: 844 },
  { name: 'اللوحيّ 768', width: 768, height: 1024 },
  { name: 'سطح المكتب 1440', width: 1440, height: 900 },
];

const me = (roles: string[], fullName: string) => ({
  userId: 'u-actor',
  fullName,
  email: 'actor@test.local',
  isActive: true,
  roles,
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
  permissions: [],
  scopeType: roles.includes('Admin') ? 'governance' : 'team',
});

// أطوال واقعيّة: عناوين القوالب وأسماء المُرسِلين على TEST عربيّة طويلة، وهي ما يدفع
// عرض البطاقة فوق عرض الإطار عند 390px.
// مزيج مقصود: عربيّ طويل (يلتفّ عند المسافات) **و**رموز ASCII طويلة بلا مسافات مثل التي
// أنشأها حصان UAT فعلًا (`UAT-P123-…`, `…@uat123.test`). الأخيرة هي ما يرفع الحدّ الأدنى
// للمحتوى (min-content) فيدفع مسار الشبكة/الفليكس فوق عرض الإطار — وهي غائبة عن بيانات
// Playwright المُنمَّطة، ولذلك لم يلتقط العيب.
const TITLES = [
  'UAT-P123-TPL-WEEKLY-SALES-REPORT-V2',
  'التقرير الأسبوعي لفريق التسويق الرقمي — قسم الحملات المدفوعة',
  'UAT-P123-TPL-MONTHLY-PERFORMANCE-REVIEW',
];
const NAMES = ['leadA1@uat123.test', 'عبد الرحمن بن محمد الشهراني', 'employeeFull@uat123.test'];
const STATUSES = ['Draft', 'Returned', 'Submitted', 'ApprovedByDirectManager'];

const submissions = Array.from({ length: 8 }, (_, i) => ({
  id: `s-${i}`,
  templateTitle: TITLES[i % TITLES.length],
  submitterId: `u-${i}`,
  submitterName: NAMES[i % NAMES.length],
  teamId: 't-1',
  departmentId: 'd-1',
  periodType: 'Weekly',
  periodKey: '2026-W34',
  status: STATUSES[i % STATUSES.length],
  submittedAtUtc: '2026-08-20T09:00:00Z',
  currentApproverId: 'u-actor',
}));

const users = NAMES.map((n, i) => ({
  id: `u-${i}`,
  fullName: n,
  email: `u${i}@test.local`,
  isActive: true,
  roles: ['Employee'],
  teamId: 't-1',
  departmentId: 'd-1',
  managerId: 'u-actor',
  jobRoleCode: null,
  jobRoleTitle: 'أخصائي تسويق رقمي أول',
}));

// لوحة قائد الفريق تُغذّى من `/dashboard/members-performance` و`/dashboard/pending-reports` لا من
// `directory/*`، وهي مصدر أعرض النصوص فيها (أسماء بُرد + عناوين قوالب).
const members = NAMES.map((n, i) => ({
  userId: `u-${i}`,
  name: n,
  kpiAverage: [61.4, 78.2, null][i],
  kpiTrend: ['Down', 'Up', 'Flat'][i],
  reportsTotal: 4,
  reportsCompleted: [2, 4, 1][i],
  isBelowTarget: [true, false, null][i],
  appliedBelowTargetThreshold: 70,
}));

const pendingReports = submissions.slice(0, 5).map((s, i) => ({
  submissionId: s.id,
  submitterId: s.submitterId,
  submitterName: s.submitterName,
  templateTitle: s.templateTitle,
  status: s.status,
  periodKey: s.periodKey,
  statusLabel: ['مسودّة', 'مُعاد للتعديل', 'لم يُرسل بعد'][i % 3],
  severity: 'High',
  hasSubmission: true,
}));

const teams = [{ id: 't-1', nameAr: 'UAT-P123-TEAM-A1', nameEn: null, departmentId: 'd-1', teamLeaderId: 'u-actor', isActive: true }];
const departments = [{ id: 'd-1', nameAr: 'UAT-P123-DEPT-ALPHA', nameEn: null, code: 'DMK', managerId: 'u-actor', isActive: true }];

const kpi = {
  company: { measure: { value: 72.5 } },
  employees: [],
  teams: [],
  appliedThreshold: 70,
};

// موزّع اللوحات في HomePage.tsx:39-55 يحجب الصفحة كلّها حتّى يصل /dashboard/me، وهو ما يحدّد
// أيّ لوحة تُعرَض أصلًا. بلا هذا الردّ تبقى الصفحة على «يتم تحميل لوحتك…» فيُقاس هيكل فارغ.
const dashboard = (type: string, name: string) => ({
  dashboardType: type,
  period: { periodKey: '2026-W34', label: 'الأسبوع 34 لعام 2026' },
  user: { id: 'u-actor', name, role: type },
  scope: { type: 'team', ids: ['t-1'] },
  permissions: [],
  summaryCards: [
    { key: 'required', title: 'تقارير مطلوبة هذا الأسبوع', value: 8, status: 'neutral', drilldownKey: null },
    { key: 'late', title: 'تقارير متأخرة تحتاج إجراء', value: 3, status: 'red', drilldownKey: null },
  ],
  widgets: [{ key: 'kpiTrend', type: 'line', title: 'اتجاه المؤشّر', data: [{ value: 70 }, { value: 74 }] }],
  actions: [],
});

async function stubDashboard(page: Page, roles: string[], fullName: string, dashboardType: string) {
  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });
  await page.route('**/api/**', async (route) => {
    const path = (route.request().url().split('/api/')[1] ?? '').split('?')[0];
    const json = (body: unknown) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

    if (path === 'auth/me') return json(me(roles, fullName));
    // تطابق تامّ لا `startsWith`: المسار `dashboard/members-performance` يبدأ بـ`dashboard/me` أيضًا،
    // فكان يتلقّى كائن اللوحة بدل مصفوفة الأعضاء ⟹ انهيار `(members ?? []).filter` وبقاء الصفحة على
    // «يتم تحميل لوحتك…» — عيب في الستَب لا في المنتج.
    if (path === 'dashboard/me') return json(dashboard(dashboardType, fullName));
    if (path === 'dashboard/members-performance') return json(members);
    if (path === 'dashboard/pending-reports') return json(pendingReports);
    if (path === 'dashboard/recent-activity') return json([]);
    if (path === 'submissions/pending-approvals') return json(submissions);
    if (path === 'escalations') return json([]);
    if (path === 'directory/users') return json(users);
    if (path === 'directory/teams') return json(teams);
    if (path === 'directory/departments') return json(departments);
    if (path.startsWith('submissions')) return json(submissions);
    if (path.startsWith('kpi/performance')) return json(kpi);
    // الافتراضيّ 403 كما في navigation.spec.ts: مسار عرض تتعامل معه كلّ صفحة برسالة صلاحيّة
    // فيبقى الهيكل حيًّا. (جُرِّب `[]` فأسقط الصفحة كلّها بـ«Cannot read properties of undefined».)
    return route.fulfill({ status: 403, contentType: 'application/json', body: '{"message":"ممنوع"}' });
  });
}

/// يعيد قائمة أعرض العناصر المتجاوزة للإطار — تشخيص قابل للقراءة عند الفشل بدل «true/false».
async function overflowReport(page: Page) {
  return page.evaluate(() => {
    const vw = window.innerWidth;
    const offenders: { tag: string; cls: string; width: number; text: string }[] = [];
    document.querySelectorAll<HTMLElement>('body *').forEach((el) => {
      const r = el.getBoundingClientRect();
      if (r.width > vw + 1) {
        offenders.push({
          tag: el.tagName.toLowerCase(),
          cls: (el.className || '').toString().slice(0, 90),
          width: Math.round(r.width),
          text: (el.textContent || '').trim().slice(0, 40),
        });
      }
    });
    offenders.sort((a, b) => b.width - a.width);
    return {
      scrollWidth: document.documentElement.scrollWidth,
      innerWidth: vw,
      offenders: offenders.slice(0, 6),
    };
  });
}

for (const actor of [
  { label: 'Admin', roles: ['Admin'], name: 'مدير النظام', type: 'AdminGovernance' },
  { label: 'TeamLeader', roles: ['TeamLeader'], name: 'قائد فريق الحملات', type: 'TeamLeader' },
]) {
  test(`لا تمرير أفقيّ على المقاسات الثلاثة للوحة ${actor.label} ببيانات غير فارغة`, async ({ page }) => {
    await stubDashboard(page, actor.roles, actor.name, actor.type);
    for (const size of SIZES) {
      await page.setViewportSize({ width: size.width, height: size.height });
      await page.goto('/app');
      await expect(page.locator('main')).toBeVisible();
      // حارس: الصفحة يجب أن تكون **مأهولة** — قياس هيكل فارغ ينجح دائمًا ويخفي العيب.
      await expect(page.locator('main')).not.toContainText('يتم تحميل لوحتك');
      await page.waitForTimeout(300);

      const r = await overflowReport(page);
      expect(
        r.scrollWidth,
        `${size.name}: فائض ${r.scrollWidth - r.innerWidth}px — ${JSON.stringify(r.offenders)}`,
      ).toBeLessThanOrEqual(r.innerWidth + 1);
    }
  });
}

test('درج الجوّال يُفتح ويُغلق بلا فائض أفقيّ في الحالتين — Admin', async ({ page }) => {
  await stubDashboard(page, ['Admin'], 'مدير النظام', 'AdminGovernance');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/app');
  await expect(page.locator('main')).toBeVisible();
  await expect(page.locator('main')).not.toContainText('يتم تحميل لوحتك');

  const closed = await overflowReport(page);
  expect(closed.scrollWidth, `الدرج مغلق: ${JSON.stringify(closed.offenders)}`).toBeLessThanOrEqual(
    closed.innerWidth + 1,
  );

  await page.getByRole('button', { name: 'القائمة', exact: true }).click();
  const drawer = page.getByRole('dialog', { name: 'قائمة التنقّل' });
  await expect(drawer).toBeVisible();
  await expect(drawer).toHaveClass(/right-0/);

  const open = await overflowReport(page);
  expect(open.scrollWidth, `الدرج مفتوح: ${JSON.stringify(open.offenders)}`).toBeLessThanOrEqual(
    open.innerWidth + 1,
  );

  await page.keyboard.press('Escape');
  await expect(drawer).toBeHidden();
});

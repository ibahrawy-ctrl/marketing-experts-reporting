// DEF-P123-004 — لا نداء عميل غير مشروط بصلاحيّة صاحبه.
//
// `/reports/governance-summary` يمنع بـ`RoleAccess.CanViewGovernance` (ReportingService.cs:2068)
// = `Has(roles,"ViewGovernance")` ⟸ Admin/CeoSupport/Ceo/GeneralManager (RoleAccess.cs:57-58).
// دور Manager خارجها، فكانت لوحته تُطلِق النداء ثمّ تبتلع 403 ضجيجًا في وحدة التحكّم وسجلّ الخادم.
//
// البوّابة الآن `dash.permissions.includes('ViewGovernance')`، و`dash.permissions` يملؤه
// `RoleAccess.PermissionsFor` نفسه (DashboardService.cs:137) ⟹ الواجهة تابعة للخادم بلا تكرار.
// لذلك يحاكي هذا الاختبار مخرَج `PermissionsFor` حرفيًّا لكلّ دور بدل اختراع قوائم.
import { test, expect, type Page } from '@playwright/test';

// مخرَج RoleAccess.PermissionsFor حرفيًّا (RoleAccess.cs:47-72).
const PERMS = {
  Manager: ['ViewOwn', 'ApproveReports', 'ExportReports', 'ViewAnalytics'],
  GM: ['ViewOwn', 'ApproveReports', 'ExportReports', 'ViewAnalytics', 'ViewGovernance', 'ManageTemplates'],
};

const submissions = Array.from({ length: 4 }, (_, i) => ({
  id: `s-${i}`, templateTitle: 'التقرير الأسبوعي', submitterId: `u-${i}`, submitterName: 'موظّف تجريبيّ',
  teamId: 't-1', departmentId: 'd-1', periodType: 'Weekly', periodKey: '2026-W34',
  status: 'Submitted', submittedAtUtc: '2026-08-20T09:00:00Z', currentApproverId: 'u-actor',
}));
const members = [{
  userId: 'u-0', name: 'موظّف تجريبيّ', kpiAverage: 74.1, kpiTrend: 'Up',
  reportsTotal: 4, reportsCompleted: 4, isBelowTarget: false, appliedBelowTargetThreshold: 70,
}];
const pendingReports = [{
  submissionId: 's-0', submitterId: 'u-0', submitterName: 'موظّف تجريبيّ', templateTitle: 'التقرير الأسبوعي',
  status: 'Draft', periodKey: '2026-W34', statusLabel: 'مسودّة', severity: 'High', hasSubmission: true,
}];
const kpi = { company: { measure: { value: 72.5 } }, employees: [], teams: [], appliedThreshold: 70 };
const govSummary = { openRisks: 3, openEscalations: 2, risksBySeverity: [], openDecisions: 1 };

type Actor = { roles: string[]; type: string; perms: string[] };

/** يُرجِع عدّاد نداءات ملخّص الحوكمة وقائمة أخطاء وحدة التحكّم. */
async function boot(page: Page, actor: Actor) {
  const govCalls: string[] = [];
  const govDenials: string[] = [];
  const pageErrors: string[] = [];

  page.on('pageerror', (e) => pageErrors.push(String(e)));
  page.on('request', (r) => { if (r.url().includes('/reports/governance-summary')) govCalls.push(r.url()); });
  // نقيس الرفض من **الشبكة** لا من نصّ وحدة التحكّم: رسالة المتصفّح لمورد فاشل
  // («Failed to load resource… 403») لا تحمل العنوان، فلا تُميّز نقطةً عن أخرى.
  // وحارس «صفر خطأ في وحدة التحكّم» عالميًّا لا معنى له هنا لأنّ الستَب يردّ 403 افتراضيًّا
  // على كلّ نقطة غير معنيّة بهذا الاختبار، فيولّد ضجيجًا هو من صنع الستَب لا من صنع المنتج.
  page.on('response', (r) => {
    if (r.url().includes('/reports/governance-summary') && r.status() === 403) govDenials.push(r.url());
  });

  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });

  await page.route('**/api/**', async (route) => {
    const path = (route.request().url().split('/api/')[1] ?? '').split('?')[0];
    const json = (b: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(b) });

    if (path === 'auth/me') return json({
      userId: 'u-actor', fullName: 'فاعل الاختبار', email: 'actor@test.local', isActive: true,
      roles: actor.roles, expectedReportCadence: 'Weekly', jobRoleCode: null, permissions: [], scopeType: 'department',
    });
    if (path === 'dashboard/me') return json({
      dashboardType: actor.type,
      period: { periodKey: '2026-W34', label: 'الأسبوع 34 لعام 2026' },
      user: { id: 'u-actor', name: 'فاعل الاختبار', role: actor.type },
      scope: { type: 'department', ids: ['d-1'] },
      permissions: actor.perms,
      summaryCards: [{ key: 'kpiAverage', title: 'متوسّط المؤشّر', value: 74, status: 'green', drilldownKey: null }],
      widgets: [{ key: 'kpiTrend', type: 'line', title: 'اتجاه المؤشّر', data: [{ value: 70 }, { value: 74 }] }],
      actions: [],
    });
    if (path === 'dashboard/members-performance') return json(members);
    if (path === 'dashboard/pending-reports') return json(pendingReports);
    if (path === 'dashboard/recent-activity') return json([]);
    if (path === 'submissions/pending-approvals') return json(submissions);
    if (path === 'escalations') return json([]);

    // المحاكاة الأمينة للخادم: النداء **يُرفَض** لمن لا يملك المفتاح. إن أطلقته الواجهة ظهر
    // 403 في وحدة التحكّم — وهو بالضبط ما يجعل الاختبار يفشل قبل الإصلاح.
    if (path === 'reports/governance-summary') {
      return actor.perms.includes('ViewGovernance')
        ? json(govSummary)
        : route.fulfill({ status: 403, contentType: 'application/json', body: '{"message":"لا تملك صلاحية عرض ملخص الحوكمة."}' });
    }

    if (path.startsWith('kpi/performance')) return json(kpi);
    return route.fulfill({ status: 403, contentType: 'application/json', body: '{"message":"ممنوع"}' });
  });

  await page.goto('/app');
  await expect(page.locator('main')).not.toContainText('يتم تحميل لوحتك');
  // مهلة تتجاوز أيّ تأخّر تصيير/إعادة جلب، وإلّا صار «صفر نداءات» نتيجة سباق لا نتيجة بوّابة.
  await page.waitForTimeout(1500);

  return { govCalls, govDenials, pageErrors };
}

test('لوحة مدير القسم لا تنادي ملخّص الحوكمة إطلاقًا — صفر نداء وصفر خطأ في وحدة التحكّم', async ({ page }) => {
  const r = await boot(page, { roles: ['Manager'], type: 'Manager', perms: PERMS.Manager });

  expect(r.govCalls, `نداءات غير مشروطة: ${JSON.stringify(r.govCalls)}`).toHaveLength(0);
  expect(r.govDenials, `رفض 403 مُولَّد من الواجهة: ${JSON.stringify(r.govDenials)}`).toHaveLength(0);
  expect(r.pageErrors, `أخطاء تصيير: ${JSON.stringify(r.pageErrors)}`).toHaveLength(0);

  // تدهور بلطف: اللوحة تُصيَّر كاملة بلا لافتة خطأ ولا فقدان محتوى.
  await expect(page.locator('main')).toContainText('لوحة مدير القسم');
});

test('لوحة المدير العام تنادي ملخّص الحوكمة مرّة واحدة بالضبط — لا صفرًا ولا تكرارًا', async ({ page }) => {
  const r = await boot(page, { roles: ['GeneralManager'], type: 'GM', perms: PERMS.GM });

  expect(r.govCalls, `عدد النداءات: ${JSON.stringify(r.govCalls)}`).toHaveLength(1);
  expect(r.govDenials, `رفض 403 غير متوقَّع: ${JSON.stringify(r.govDenials)}`).toHaveLength(0);
  expect(r.pageErrors, `أخطاء تصيير: ${JSON.stringify(r.pageErrors)}`).toHaveLength(0);
});

// P1-KPI-008 — اختبارات E2E لشاشة «نظرة KPI» على البناء الحقيقيّ (Vite preview).
//
// الخادم غير مطلوب هنا: تُعترَض نداءات `/api/**` وتُخدَم عيّنة ثابتة، لأنّ الغرض إثبات سلوك
// **الواجهة** (انتقال المُرشِّح إلى كلّ الطلبات، غياب التكرار في الترتيب، التفصيل، RTL،
// والاستجابة للمقاسات) لا إثبات الحساب — الحساب مُثبَت في اختبارات التكامل المعزولة.
import { test, expect, type Page } from '@playwright/test';

const PERIOD = {
  type: 'LastCompletedWeek', key: '2026-W33', start: '2026-08-15', end: '2026-08-21',
  timezone: 'Asia/Riyadh', isOpen: false, label: 'الأسبوع 33',
};

const measure = (over: Record<string, unknown> = {}) => ({
  value: 65, eligibleEvaluationCount: 2, expectedEvaluationCount: 2, adjustedExpectedCount: 2,
  coverage: 1, missingCount: 0, excludedByStatusCount: 0, dataQuality: 'Complete',
  previousValue: 60, delta: 5, trend: 'Up', ...over,
});

const employee = (userId: string, fullName: string, m: Record<string, unknown>) => ({
  userId, fullName, teamId: 'team-1', teamName: 'فريق المحتوى',
  departmentId: 'dep-1', departmentName: 'إدارة التسويق',
  measure: m, eligibleForRanking: true, isBelowTarget: false,
  appliedBelowTargetThreshold: 60, thresholdSource: 'kpiTemplateVersion',
});

const SARA = employee('u-1', 'سارة أحمد', measure());
const KHALED = employee('u-2', 'خالد سالم', measure({
  value: null, eligibleEvaluationCount: 0, adjustedExpectedCount: 1, coverage: 0,
  missingCount: 1, dataQuality: 'NoData', previousValue: null, delta: null, trend: 'Unknown',
}));
const REEM = employee('u-3', 'ريم ناصر', measure({ value: 0, trend: 'Down', delta: -10 }));

const PERFORMANCE = {
  periodResolved: PERIOD,
  previousPeriodResolved: { ...PERIOD, key: '2026-W32', label: 'الأسبوع 32' },
  cadence: 'WeeklyPulse', scopeType: 'Company',
  company: { groupType: 'Company', groupId: null, groupName: 'الشركة', measure: measure(), scoredMemberCount: 2, totalMemberCount: 3 },
  departments: [{ groupType: 'Department', groupId: 'dep-1', groupName: 'إدارة التسويق', measure: measure(), scoredMemberCount: 2, totalMemberCount: 3 }],
  teams: [{ groupType: 'Team', groupId: 'team-1', groupName: 'فريق المحتوى', measure: measure(), scoredMemberCount: 2, totalMemberCount: 3 }],
  employees: [SARA, KHALED, REEM],
  calculatedAtUtc: '2026-08-24T10:00:00Z',
};

const RANKINGS = {
  periodResolved: PERIOD, cadence: 'WeeklyPulse', scopeType: 'Company',
  topPerformers: [SARA], needsSupport: [REEM],
  excludedForInsufficientCoverage: 0, minimumCoverage: 0.75,
  calculatedAtUtc: '2026-08-24T10:00:00Z',
};

const DRILLDOWN = {
  periodResolved: PERIOD, cadence: 'WeeklyPulse', subjectUserId: 'u-1',
  recomputedValue: 65, rowCount: 2,
  rows: [85, 45].map((score, i) => ({
    evaluationId: `ev-${i}`, subjectUserId: 'u-1', subjectName: 'سارة أحمد',
    templateTitle: 'نبض المحتوى', cadence: 'WeeklyPulse', periodType: 'Weekly',
    periodKey: '2026-W33', periodStart: '2026-08-15', periodEnd: '2026-08-21',
    status: 'Approved', totalScore: score, submittedAtUtc: null,
  })),
  calculatedAtUtc: '2026-08-24T10:00:00Z',
};

const ME = {
  userId: 'u-admin', fullName: 'مدير النظام', email: 'admin@test.local',
  isActive: true, roles: ['Admin'], expectedReportCadence: 'Weekly',
};

/** يجمع نداءات KPI ليتحقّق الاختبار من انتقال المُرشِّح إلى كلّ طلب. */
async function stubApi(page: Page, seen: string[]) {
  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });
  await page.route('**/api/**', async (route) => {
    const url = route.request().url();
    const path = url.split('/api/')[1] ?? '';
    if (path.startsWith('kpi/')) seen.push(path);
    const body =
      path.startsWith('auth/me') ? ME
      : path.startsWith('kpi/rankings') ? RANKINGS
      : path.startsWith('kpi/drilldown') ? DRILLDOWN
      : path.startsWith('kpi/performance') ? PERFORMANCE
      : [];
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
}

test.describe('نظرة KPI — E2E', () => {
  test('المُرشِّح الموحّد ينتقل إلى كلّ الطلبات، والترتيب بلا تكرار، والتفصيل يعيد إنتاج الرقم', async ({ page }) => {
    const seen: string[] = [];
    await stubApi(page, seen);
    await page.goto('/app/kpi');

    await expect(page.getByText('متوسط مؤشر الشركة')).toBeVisible();
    // الفترة معروضة كما حلّها الخادم بتوقيت الرياض — لا اشتقاق في المتصفّح (B-1).
    await expect(page.getByText(/Asia\/Riyadh/)).toBeVisible();
    expect(seen.some((p) => p.includes('cadence=WeeklyPulse'))).toBeTruthy();

    // تغيير الكادنس يقود البطاقات والترتيب معًا (B-3).
    seen.length = 0;
    await page.getByLabel('الكادنس').selectOption('Quarterly');
    await expect.poll(() => seen.filter((p) => p.includes('cadence=Quarterly')).length).toBeGreaterThanOrEqual(2);
    expect(seen.some((p) => p.startsWith('kpi/performance'))).toBeTruthy();
    expect(seen.some((p) => p.startsWith('kpi/rankings'))).toBeTruthy();

    // لا تكرار بين القائمتين: كلّ اسم مرّة واحدة قبل فتح تفصيل الفريق.
    await expect(page.getByText('سارة أحمد')).toHaveCount(1);
    await expect(page.getByText('ريم ناصر')).toHaveCount(1);

    // تفصيل الرقم: صفوف 85 و45 بمتوسّط 65 — لا طيّ إلى أعلى تقييم.
    await page.getByRole('button', { name: 'تفصيل الأعضاء' }).click();
    await page.getByRole('button', { name: 'تفصيل الرقم' }).first().click();
    await expect(page.getByText(/تقييم معتمَد · المتوسّط المُعاد حسابه/)).toBeVisible();
    expect(seen.some((p) => p.startsWith('kpi/drilldown') && p.includes('subjectUserId=u-1'))).toBeTruthy();
  });

  test('«لا تقييم» تظهر غيابًا صريحًا لا صفرًا', async ({ page }) => {
    await stubApi(page, []);
    await page.goto('/app/kpi');
    await page.getByRole('button', { name: 'تفصيل الأعضاء' }).click();
    const khaled = page.locator('div.rounded-lg').filter({ hasText: 'خالد سالم' }).first();
    await expect(khaled.getByText('لا تقييم')).toBeVisible();
  });

  // الرابط العميق: تحميل مباشر لـ`/app/kpi` بلا تنقّل سابق داخل التطبيق. المُرشِّح يظهر مضبوطًا
  // على ما حلّه الخادم (لا حالة محفوظة في العميل)، والطلب الأوّل يحمل كادنسًا صريحًا (B-3).
  test('الرابط العميق يفتح الشاشة مباشرةً بمُرشِّح مضبوط من الخادم', async ({ page }) => {
    const seen: string[] = [];
    await stubApi(page, seen);
    await page.goto('/app/kpi');

    await expect(page.getByLabel('الكادنس')).toHaveValue('WeeklyPulse');
    await expect(page.getByLabel('نوع الفترة')).toHaveValue('LastCompletedWeek');
    await expect(page.getByText('الأسبوع 33')).toBeVisible();
    expect(seen.every((p) => p.includes('cadence='))).toBeTruthy();
  });

  test('الاتجاه RTL ويعمل على سطح المكتب واللوحيّ والجوّال', async ({ page }) => {
    await stubApi(page, []);
    await page.goto('/app/kpi');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    for (const size of [{ width: 1440, height: 900 }, { width: 820, height: 1180 }, { width: 390, height: 844 }]) {
      await page.setViewportSize(size);
      await expect(page.getByLabel('الكادنس')).toBeVisible();
      await expect(page.getByText('متوسط مؤشر الشركة')).toBeVisible();
    }
  });
});

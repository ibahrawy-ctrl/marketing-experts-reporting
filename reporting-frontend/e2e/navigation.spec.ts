// P3-NAV-002/003/004 — الملاحة على البناء الحقيقيّ (Vite preview).
//
// اختبارات الوحدة تُثبِت **السجلّ** (الظهور، الترتيب، الوجهة). ما لا تُثبِته: أنّ الشيء يعمل
// بعد التحزيم على مقاسات حقيقيّة — أنّ الوحدات السبع تُرسَم فعلًا، وأنّ الرابط العميق يصل
// بمعاملاته سليمة، وأنّ الشريط ينطوي إلى درج على الجوّال ولا يبقى مقصوصًا. لا خادم مطلوب:
// نداءات `/api/**` مُعترَضة، فالمقصود سلوك الواجهة لا صحّة البيانات.
import { test, expect, type Page } from '@playwright/test';

const ME = {
  userId: 'u-admin',
  fullName: 'مدير النظام',
  email: 'admin@test.local',
  isActive: true,
  roles: ['Admin'],
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
  permissions: [],
  scopeType: 'governance',
};

const SIZES = [
  { name: 'سطح المكتب', width: 1440, height: 900 },
  { name: 'اللوحيّ', width: 820, height: 1180 },
  { name: 'الجوّال', width: 390, height: 844 },
];

/// الوحدات السبع الثابتة — نفس ترتيب السجلّ، ويجب أن تظهر لأيّ دور يراها بلا تبديل.
const MODULES = ['الرئيسية', 'الموظفون', 'التقارير', 'الأداء وKPI', 'الحوكمة', 'العملاء والمشروعات', 'الإعدادات'];

async function stubApi(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });
  await page.route('**/api/**', async (route) => {
    const path = route.request().url().split('/api/')[1] ?? '';
    if (path.startsWith('auth/me')) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(ME) });
      return;
    }
    // بيانات الصفحات خارج موضوع هذه المجموعة، والردّ الفارغ يكسر أشكالًا تتوقّعها الصفحات.
    // 403 مسار عرض مشروع تتعامل معه كلّ صفحة برسالة صلاحيّة، فيبقى الهيكل حيًّا ويُقاس وحده.
    await route.fulfill({ status: 403, contentType: 'application/json', body: '{"message":"ممنوع"}' });
  });
}

const sidebar = (page: Page) => page.getByRole('navigation', { name: 'الوحدات الرئيسية' }).first();

test.describe('الملاحة — E2E', () => {
  test('الوحدات السبع تُرسَم بترتيبها الثابت على سطح المكتب', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app');

    // ننتظر آخر وحدة قبل القراءة: `allInnerTexts` لا ينتظر، فقراءة مبكّرة تُنتج قائمة فارغة تكذب.
    await expect(sidebar(page)).toContainText(MODULES[MODULES.length - 1]);
    const labels = await sidebar(page).getByRole('link').allInnerTexts();
    const found = MODULES.filter((m) => labels.some((l) => l.includes(m)));
    expect(found).toEqual(MODULES);
  });

  test('الوحدة الحاوية والقسم النشط يُعلَّمان معًا فلا يفقد المستخدم موضعه', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app/sales-aggregation');

    await expect(sidebar(page).locator('[aria-current="page"]')).toHaveText(/التقارير/);
    // القسم النشط مُعلَّم مرّة واحدة: إمّا لسانًا ظاهرًا أو زرّ «المزيد ⋯» الذي يطويه —
    // فالطيّ لا يجوز أن يبتلع الموضع الحاليّ ويترك الشريط بلا أثر له.
    await expect(page.getByRole('tablist').locator('[aria-current="page"]')).toHaveCount(1);
  });

  test('فتات الخبز تصف الموضع: الوحدة › المجموعة › القسم', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app/sales-aggregation');

    const crumbs = page.getByRole('navigation', { name: 'مسار التنقّل' });
    await expect(crumbs).toContainText('التقارير');
    await expect(crumbs).toContainText('تجميع المبيعات');
  });

  test('الرابط المدمَج يُحوِّل إلى وجهته حافظًا الاستعلام والمِرساة', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app/escalations?status=Open#top');

    // التحويل تنقّل داخليّ لا إعادة تحميل، فالمِرساة والاستعلام يبقيان كما كتبهما المستخدم.
    await expect(page).toHaveURL(/\/app\/governance\/escalations\?status=Open#top$/);
  });

  test('شريط الأقسام يلتفّ ولا يُقَصّ، والفائض يذهب إلى «المزيد ⋯»', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app/employee/me');

    const tablist = page.getByRole('tablist');
    await expect(tablist).toBeVisible();
    // لا تمرير أفقيّ مخفيّ: عرض المحتوى لا يتجاوز عرض الإطار.
    const overflowing = await tablist.evaluate((el) => el.scrollWidth > el.clientWidth + 1);
    expect(overflowing).toBeFalsy();
  });

  test('على الجوّال: درج من اليمين يُفتح بزرّ القائمة ويُغلق بـEscape', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[2]);
    await page.goto('/app');

    await page.getByRole('button', { name: 'القائمة', exact: true }).click();
    const drawer = page.getByRole('dialog', { name: 'قائمة التنقّل' });
    await expect(drawer).toBeVisible();
    await expect(drawer).toHaveClass(/right-0/);

    await page.keyboard.press('Escape');
    await expect(drawer).toBeHidden();
  });

  test('إتاحة: معالم معنونة، وكلّ رابط باسم مقروء، وموضع واحد لا موضعان', async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize(SIZES[0]);
    await page.goto('/app/sales-aggregation');
    await expect(sidebar(page)).toContainText(MODULES[MODULES.length - 1]);

    // معالم الملاحة معنونة صراحةً: قارئ الشاشة يميّز «الوحدات» من «مسار التنقّل».
    await expect(page.getByRole('navigation', { name: 'الوحدات الرئيسية' })).toHaveCount(1);
    await expect(page.getByRole('navigation', { name: 'مسار التنقّل' })).toHaveCount(1);

    // لا رابط بلا اسم مقروء (أيقونة عارية تُنطَق فراغًا).
    const nameless = await sidebar(page)
      .getByRole('link')
      .evaluateAll((els) => els.filter((e) => !(e.textContent ?? '').trim() && !e.getAttribute('aria-label')).length);
    expect(nameless).toBe(0);

    // موضع واحد فقط في كلّ شريط: تعدّد `aria-current` يجعل القارئ يُعلن موضعين متناقضين.
    await expect(sidebar(page).locator('[aria-current="page"]')).toHaveCount(1);
    await expect(page.getByRole('tablist').locator('[aria-current="page"]')).toHaveCount(1);
  });

  test('الاتّجاه RTL والملاحة صالحة على المقاسات الثلاثة', async ({ page }) => {
    await stubApi(page);
    await page.goto('/app');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    for (const size of SIZES) {
      await page.setViewportSize(size);
      // على كلّ مقاس يبقى للمستخدم طريق إلى الوحدات: شريط جانبيّ أو زرّ درج — لا شاشة بلا ملاحة.
      const reachable =
        (await sidebar(page).isVisible()) || (await page.getByRole('button', { name: 'القائمة', exact: true }).isVisible());
      expect(reachable, `لا ملاحة على مقاس ${size.name}`).toBeTruthy();
      // ولا تمرير أفقيّ للصفحة كلّها (كسر تخطيط RTL يظهر هكذا أوّلًا).
      const horizontal = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
      expect(horizontal, `تمرير أفقيّ على مقاس ${size.name}`).toBeFalsy();
    }
  });
});

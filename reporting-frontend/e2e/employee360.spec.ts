// P2-EMP-003 / P2-HR-010 — اختبارات E2E للملفّ الشامل وقائمة الالتزام على البناء الحقيقيّ.
//
// **الخادم غير مُشغَّل هنا، وهذا مُصرَّح به لا مُدَّعى خلافه:** نداءات `/api/**` تُعترَض وتُخدَم من
// عيّنة في الذاكرة. الغرض إثبات سلوك **الواجهة** على البناء المُصرَّف: أنّ ما لم يصل من الخادم
// لا يُرسَم، وأنّ وضع الذات ينادي مسار `me` ولا يشتقّ معرّفًا في المتصفّح، وأنّ البند المحسوب
// بلا محرّر، وأنّ الوصول المباشر إلى ملفّ خارج النطاق شاشة منع لا بيانات.
// صحّة النطاق والتخويل وحسّاسيّة الحقول مُثبَتة في اختبارات التكامل المعزولة على قاعدة محلّيّة
// حقيقيّة — لا هنا، ولا يُدَّعى ذلك.
import { test, expect, type Page } from '@playwright/test';

const SUBJECT = '33333333-3333-3333-3333-333333333333';

// اسم المستخدم الحاليّ **مختلف عمدًا** عن اسم صاحب الملفّ: بذلك يصير غياب اسم الموضوع
// دليلًا على المنع فعلًا، لا مجرّد غياب شريط الحساب العلويّ.
const ME = {
  userId: '99999999-9999-9999-9999-999999999999', fullName: 'خالد المطيري',
  email: 'khaled@test.local', isActive: true, roles: ['Employee'],
  expectedReportCadence: 'Weekly',
};

/** الأقسام الأحد عشر بترتيب العرض المعتمد — ما لم يصل منها لا يُرسَم. */
const SECTION_KEYS = [
  'identity', 'operationalSummary', 'reports', 'kpi', 'leaveAndPermissions',
  'requestsAndBalances', 'attendanceAndCompliance', 'notes', 'governance',
  'developmentAndTraining', 'timeline',
] as const;

const SECTION_TITLE: Record<string, string> = {
  identity: 'الهويّة وحالة التوظيف',
  operationalSummary: 'الملخّص التشغيليّ',
  reports: 'التقارير',
  kpi: 'تقييمات الأداء',
  leaveAndPermissions: 'الإجازات والاستئذانات',
  requestsAndBalances: 'الطلبات والأرصدة',
  attendanceAndCompliance: 'الحضور والالتزام',
  notes: 'الملاحظات الإداريّة',
  governance: 'الحوكمة',
  developmentAndTraining: 'التطوير والتدريب',
  timeline: 'الخطّ الزمنيّ الموحّد',
};

function section(key: string) {
  return {
    key,
    titleAr: SECTION_TITLE[key],
    status: 'Ready',
    dataQuality: 'Complete',
    lastUpdatedAtUtc: '2026-08-24T07:00:00Z',
    summary: key === 'identity'
      ? { userId: SUBJECT, fullName: 'سارة العتيبي', email: 'sara@test.local', jobRoleName: 'أخصّائيّ محتوى', teamName: 'فريق المحتوى', departmentName: 'التسويق', directManagerName: 'منى الحربي', isActive: true, joinedAtUtc: '2025-01-05T00:00:00Z' }
      : null,
    items: key === 'timeline'
      ? [{ kind: 'Report', source: 'ReportSubmission', sourceId: 's-1', label: 'تسليم تقرير أسبوعيّ', atUtc: '2026-08-23T08:00:00Z', needsMyAction: false }]
      : [],
    reason: null,
  };
}

function profile(keys: readonly string[] = SECTION_KEYS) {
  return {
    subjectUserId: SUBJECT,
    isSelf: true,
    viewerRelation: 'Self',
    periodKey: null,
    sections: Object.fromEntries(keys.map((k) => [k, section(k)])),
  };
}

function checklist(over: Record<string, unknown> = {}) {
  return {
    subjectUserId: SUBJECT,
    isSelf: true,
    viewerRelation: 'Self',
    summary: { applicable: 3, completed: 1, open: 2, notApplicable: 1, requiresMyAction: 1, completionRatio: 0.3333 },
    items: [
      {
        key: 'reports-obligations', titleAr: 'التقارير الدوريّة المطلوبة', groupAr: 'الالتزام التشغيليّ',
        source: 'Computed', status: 'NotStarted', statusLabelAr: '2 بندًا مفتوحًا', openCount: 2,
        ownerUserId: SUBJECT, ownerFullName: 'سارة العتيبي', dueDate: null, lastActionAtUtc: null,
        evidenceReference: null, sourceKind: 'ObligationsService', sourceLink: '/app/reports',
        requiresMyAction: true,
      },
      {
        key: 'kpi-obligations', titleAr: 'تقييمات الأداء المطلوبة', groupAr: 'الالتزام التشغيليّ',
        source: 'Computed', status: 'NotApplicable', statusLabelAr: 'غير منطبق', openCount: 0,
        ownerUserId: null, ownerFullName: null, dueDate: null, lastActionAtUtc: null,
        evidenceReference: null, sourceKind: null, sourceLink: null, requiresMyAction: false,
      },
      {
        key: 'onboarding-orientation', titleAr: 'إتمام التهيئة التعريفيّة', groupAr: 'التهيئة',
        source: 'Manual', status: 'Completed', statusLabelAr: 'مكتمل', openCount: 0,
        ownerUserId: SUBJECT, ownerFullName: 'منى الحربي', dueDate: '2026-08-30',
        lastActionAtUtc: '2026-08-22T09:00:00Z', evidenceReference: 'محضر التهيئة رقم ١',
        sourceKind: null, sourceLink: null, requiresMyAction: false,
      },
    ],
    ...over,
  };
}

type Stub = {
  profileStatus: number;
  checklistStatus: number;
  updateStatus: number;
  sectionKeys: readonly string[];
  /** كلّ نداء كما وقع فعلًا — به نثبت أنّ وضع الذات لم يشتقّ معرّفًا في المتصفّح. */
  calls: { method: string; path: string; body: string | null }[];
};

function newStub(over: Partial<Stub> = {}): Stub {
  return {
    profileStatus: 200, checklistStatus: 200, updateStatus: 200,
    sectionKeys: SECTION_KEYS, calls: [], ...over,
  };
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
    stub.calls.push({ method: req.method(), path, body: req.postData() });

    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    if (path.startsWith('auth/me')) return json(ME);

    if (path.includes('/checklist/')) {
      if (stub.updateStatus !== 200)
        return json({ title: 'Forbidden', detail: 'لا تملك صلاحيّة تحرير بنود قائمة الالتزام.' }, stub.updateStatus);
      return json({});
    }

    if (path.endsWith('/checklist')) {
      if (stub.checklistStatus !== 200) return json({ title: 'NotFound', detail: 'غير متاح' }, stub.checklistStatus);
      return json(checklist());
    }

    if (path.endsWith('/profile-360')) {
      if (stub.profileStatus !== 200) return json({ title: 'NotFound', detail: 'غير متاح' }, stub.profileStatus);
      return json(profile(stub.sectionKeys));
    }

    if (path.startsWith('dashboard/employee-profile/'))
      return json({ title: 'NotFound', detail: 'غير متاح' }, 404);

    return json([]);
  });
}

test.describe('الملفّ الشامل 360 — E2E', () => {
  test('وضع الذات يرسم الأقسام الأحد عشر وينادي مسار me الخادميّ', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/employee/me');

    await expect(page.getByRole('heading', { name: 'ملفّي' })).toBeVisible();
    for (const key of SECTION_KEYS)
      await expect(page.getByRole('heading', { name: SECTION_TITLE[key] })).toBeVisible();

    // المعرّف لم يُشتقّ في المتصفّح: النداء ذهب بالسلسلة `me` حرفيًّا.
    expect(stub.calls.some((c) => c.path === 'employees/me/profile-360')).toBe(true);
    expect(stub.calls.some((c) => c.path === `employees/${SUBJECT}/profile-360`)).toBe(false);
  });

  test('القسم الذي لم يصل لا يُرسَم ولا يُترك مكانه فارغًا', async ({ page }) => {
    await stubApi(page, newStub({ sectionKeys: ['identity', 'timeline'] }));
    await page.goto('/app/employee/me');

    await expect(page.getByRole('heading', { name: SECTION_TITLE.identity })).toBeVisible();
    await expect(page.getByRole('heading', { name: SECTION_TITLE.timeline })).toBeVisible();
    await expect(page.getByRole('heading', { name: SECTION_TITLE.notes })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: SECTION_TITLE.kpi })).toHaveCount(0);
  });

  test('الوصول المباشر إلى ملفّ خارج النطاق شاشة منع لا بيانات موظّف', async ({ page }) => {
    await stubApi(page, newStub({ profileStatus: 404, checklistStatus: 404 }));
    await page.goto(`/app/employee/${SUBJECT}`);

    await expect(page.getByText('لا يمكن عرض هذا الملف')).toBeVisible();
    await expect(page.getByText('سارة العتيبي')).toHaveCount(0);
  });

  test('عطل القائمة لا يُسقِط الملفّ الشامل — دورتا تحميل مستقلّتان', async ({ page }) => {
    await stubApi(page, newStub({ checklistStatus: 404 }));
    await page.goto('/app/employee/me');

    await expect(page.getByRole('heading', { name: SECTION_TITLE.identity })).toBeVisible();
    await expect(page.getByText('تعذّر تحميل قائمة الالتزام')).toBeVisible();
  });
});

test.describe('قائمة خدمة الموظّف والالتزام — E2E', () => {
  test('القائمة تعرض بنودها وملخّصها كما حسمه الخادم', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/employee/me');

    const panel = page.locator('#emp360-checklist');
    await expect(panel.getByRole('heading', { name: 'قائمة خدمة الموظّف والالتزام' })).toBeVisible();
    await expect(panel.getByText('التقارير الدوريّة المطلوبة')).toBeVisible();
    await expect(panel.getByText('إتمام التهيئة التعريفيّة')).toBeVisible();

    // النسبة كما حسبها الخادم لا كما تُعيد الواجهة اشتقاقها.
    await expect(panel.getByText('33%')).toBeVisible();
    await expect(panel.getByText('عليك إجراء في 1 بندًا.')).toBeVisible();
  });

  test('البند المحسوب بلا محرّر، واليدويّ وحده يحمل حقلًا وزرّ حفظ', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/employee/me');

    const computed = page.locator('li', { hasText: 'التقارير الدوريّة المطلوبة' }).first();
    await expect(computed.getByText('محسوب من مصدره')).toBeVisible();
    await expect(computed.getByRole('button', { name: 'حفظ البند' })).toHaveCount(0);
    await expect(computed.getByLabel('الحالة')).toHaveCount(0);

    const manual = page.locator('li', { hasText: 'إتمام التهيئة التعريفيّة' }).first();
    await expect(manual.getByText('يدويّ')).toBeVisible();
    await expect(manual.getByRole('button', { name: 'حفظ البند' })).toBeVisible();
  });

  test('«غير منطبق» لا يُعرَض عدّادًا صفرًا', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/employee/me');

    const na = page.locator('li', { hasText: 'تقييمات الأداء المطلوبة' }).first();
    await expect(na.getByText('غير منطبق')).toBeVisible();
    await expect(na.getByText('بنود مفتوحة')).toHaveCount(0);
  });

  test('حفظ البند اليدويّ يذهب إلى مسار البند بمعرّف الموضوع الذي حسمه الخادم', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/employee/me');

    const manual = page.locator('li', { hasText: 'إتمام التهيئة التعريفيّة' }).first();
    await expect(manual.getByRole('button', { name: 'حفظ البند' })).toBeVisible();
    await manual.getByLabel('الحالة').selectOption('InProgress');
    await manual.getByRole('button', { name: 'حفظ البند' }).click();

    await expect
      .poll(() => stub.calls.some((c) =>
        c.method === 'PUT'
        && c.path === `employees/${SUBJECT}/checklist/onboarding-orientation`
        && (c.body ?? '').includes('InProgress')))
      .toBe(true);

    await expect(manual.getByText(/سجلّ التدقيق/)).toBeVisible();
  });

  test('منع التحرير بـ403 رسالة صلاحيّة لا إخفاء للزرّ', async ({ page }) => {
    await stubApi(page, newStub({ updateStatus: 403 }));
    await page.goto('/app/employee/me');

    const manual = page.locator('li', { hasText: 'إتمام التهيئة التعريفيّة' }).first();
    await expect(manual.getByRole('button', { name: 'حفظ البند' })).toBeVisible();
    await manual.getByRole('button', { name: 'حفظ البند' }).click();

    await expect(manual.getByRole('alert')).toContainText('لا تملك صلاحيّة تحرير بنود قائمة الالتزام.');
    await expect(manual.getByRole('button', { name: 'حفظ البند' })).toBeVisible();
  });

  test('الاتّجاه RTL والعرض سليم على سطح المكتب واللوحيّ والجوّال', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/employee/me');
    await expect(page.locator('#emp360-checklist')).toBeVisible();

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    for (const size of [
      { width: 1440, height: 900 },
      { width: 834, height: 1112 },
      { width: 390, height: 844 },
    ]) {
      await page.setViewportSize(size);
      await expect(page.locator('#emp360-checklist').getByText('إتمام التهيئة التعريفيّة')).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(1);
    }
  });
});

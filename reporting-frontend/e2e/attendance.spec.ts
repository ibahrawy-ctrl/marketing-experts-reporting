// P2-ATT-007 — اختبارات E2E لسطح الحضور والالتزام على البناء الحقيقيّ (Vite preview).
//
// **الخادم غير مُشغَّل هنا، وهذا مُصرَّح به لا مُدَّعى خلافه:** نداءات `/api/**` تُعترَض
// وتُخدَم من عيّنة في الذاكرة. الغرض إثبات سلوك **الواجهة** على البناء المُصرَّف: أنّ الأزرار
// تُرسَم من `allowedActions` الخادميّة حصرًا، وأنّ الحقل المحجوب غائب لا فارغ، وأنّ الاتّجاه
// والاستجابة سليمان. صحّة آلة الحالات والتخويل مُثبَتة في اختبارات التكامل المعزولة على
// قاعدة محلّيّة حقيقيّة — لا هنا، ولا يُدَّعى ذلك.
import { test, expect, type Page } from '@playwright/test';

const INCIDENT_ID = '44444444-4444-4444-4444-444444444444';
const TYPE_ID = '55555555-5555-5555-5555-555555555555';

const ME = {
  userId: 'u-hr', fullName: 'منى الحربي', email: 'hr@test.local',
  isActive: true, roles: ['HR'], expectedReportCadence: 'Weekly',
};

const TYPES = [{
  id: TYPE_ID, code: 'LATE', nameAr: 'تأخّر عن الدوام',
  requiresTimes: true, requiresPolicyReference: false, allowsMultiplePerDay: false, order: 1,
}];

const LIST_ITEM = {
  id: INCIDENT_ID, subjectUserId: 'u-1', subjectName: 'سارة العتيبي',
  incidentTypeId: TYPE_ID, typeCode: 'LATE', typeNameAr: 'تأخّر عن الدوام',
  incidentDate: '2026-08-18', status: 'AwaitingEmployee', statusAr: 'بانتظار ردّ الموظّف',
  isOfficialIncident: false, durationMinutes: 45, ageingDays: 2,
  slaDueAtUtc: '2026-08-20T06:30:00Z', isOverdue: false,
  lastActionAtUtc: '2026-08-18T07:00:00Z', nextActorAr: 'الموظّف',
};

/** الحالة الحيّة للعيّنة: الـPOST يغيّرها كما يغيّرها الخادم، فيُقاس أثر الإجراء لا مجرّد إرساله. */
type Stub = {
  detail: Record<string, unknown>;
  posts: { path: string; body: unknown; idempotencyKey: string | null }[];
};

function freshDetail(over: Record<string, unknown> = {}) {
  return {
    id: INCIDENT_ID, subjectUserId: 'u-1', subjectName: 'سارة العتيبي',
    incidentTypeId: TYPE_ID, typeCode: 'LATE', typeNameAr: 'تأخّر عن الدوام',
    incidentDate: '2026-08-18', startTime: '08:45:00', returnTime: '09:30:00',
    durationMinutes: 45, description: 'تأخّر عن بداية الدوام بلا إشعار مسبق.',
    detectionSource: 'Manual', reportedByUserId: 'u-2', reportedByName: 'خالد المطيري',
    status: 'AwaitingEmployee', statusAr: 'بانتظار ردّ الموظّف',
    isOfficialIncident: false, concurrencyStamp: 3,
    slaDueAtUtc: '2026-08-20T06:30:00Z', isOverdue: false, ageingDays: 2,
    nextActorAr: 'الموظّف', respondedAtUtc: null, hrDecision: null,
    reviewedByUserId: null, reviewedAtUtc: null,
    reconciledWithLeaveId: null, reconciledWithPermissionId: null,
    duplicateOfId: null, closedAtUtc: null, createdAtUtc: '2026-08-18T07:00:00Z',
    attachments: [], allowedActions: [],
    events: [{
      id: 'e-1', actorUserId: 'u-2', actorName: 'خالد المطيري', action: 'Submit',
      fromStatus: 'Draft', toStatus: 'AwaitingEmployee', comment: null,
      createdAtUtc: '2026-08-18T07:00:00Z',
    }],
    ...over,
  };
}

async function stubApi(page: Page, stub: Stub, opts: { listEmpty?: boolean } = {}) {
  await page.addInitScript(() => {
    localStorage.setItem('me_access', 'e2e-token');
    localStorage.setItem('me_refresh', 'e2e-refresh');
  });

  await page.route('**/api/**', async (route) => {
    const req = route.request();
    const path = req.url().split('/api/')[1]?.split('?')[0] ?? '';
    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    if (req.method() === 'POST' && (path === 'attendance' || path.startsWith('attendance/'))) {
      // `attendance` بلا لاحقة = إنشاء بلاغ؛ الباقي انتقالُ حالة.
      const action = path === 'attendance' ? 'create' : (path.split('/').pop() ?? '');
      stub.posts.push({
        path, body: req.postDataJSON?.() ?? null,
        idempotencyKey: req.headers()['idempotency-key'] ?? null,
      });
      if (action === 'create') return json(stub.detail);

      // الانتقال كما يفرضه الخادم — الواجهة لا تحسبه ولا تفترضه.
      const next =
        action === 'acknowledge' ? { status: 'AwaitingHr', statusAr: 'بانتظار المراجعة', nextActorAr: 'الموارد البشريّة', employeeResponse: 'أقرّ بالواقعة.' }
        : action === 'dispute' ? { status: 'Disputed', statusAr: 'معترَض عليها', nextActorAr: 'الموارد البشريّة', employeeResponse: 'كنت في مهمّة معتمدة.' }
        : action === 'hr-review' ? { status: 'Confirmed', statusAr: 'مؤكَّدة', isOfficialIncident: true, nextActorAr: null, hrDecision: 'Confirm' }
        : {};

      stub.detail = { ...stub.detail, ...next, allowedActions: [], concurrencyStamp: 4,
        events: [...(stub.detail.events as unknown[]), {
          id: `e-${(stub.detail.events as unknown[]).length + 1}`,
          actorUserId: ME.userId, actorName: ME.fullName, action,
          fromStatus: String(stub.detail.status), toStatus: String(next.status ?? stub.detail.status),
          comment: null, createdAtUtc: '2026-08-19T08:00:00Z',
        }] };
      return json(stub.detail);
    }

    if (path.startsWith('auth/me')) return json(ME);
    if (path === 'attendance/types') return json(TYPES);
    if (path === `attendance/${INCIDENT_ID}`) return json(stub.detail);
    if (path === 'attendance') {
      const rows = opts.listEmpty ? [] : [{ ...LIST_ITEM, status: stub.detail.status, statusAr: stub.detail.statusAr }];
      return json({ items: rows, totalCount: rows.length, page: 1, pageSize: 25 });
    }
    return json([]);
  });
}

function newStub(over: Record<string, unknown> = {}): Stub {
  return { detail: freshDetail(over), posts: [] };
}

test.describe('الحضور والالتزام — E2E', () => {
  test('القائمة تعرض الوقائع ضمن النطاق، والتفصيل يفتح بخطّه الزمنيّ', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/attendance');

    await expect(page.getByRole('heading', { name: 'الحضور والالتزام' })).toBeVisible();
    const list = page.getByTestId('attendance-list');
    await expect(list.getByText('سارة العتيبي')).toBeVisible();

    await list.getByText('سارة العتيبي').click();
    const detail = page.getByTestId('attendance-detail');
    await expect(detail).toBeVisible();
    await expect(detail.getByTestId('attendance-timeline').getByText(/Draft ← AwaitingEmployee/)).toBeVisible();
    // بلاغ لا إدانة: الشارة تقرأ حسم الخادم.
    await expect(detail.getByText('بلاغ', { exact: true })).toBeVisible();
    await expect(detail.getByText('واقعة مؤكَّدة')).toHaveCount(0);
  });

  test('لا يظهر أيّ زرّ إجراء حين لا يمنح الخادم فعلًا واحدًا', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto(`/app/attendance?incident=${INCIDENT_ID}`);

    const actions = page.getByTestId('attendance-actions');
    await expect(actions.getByText('لا إجراء متاح لك على هذه الواقعة الآن.')).toBeVisible();
    await expect(actions.getByRole('button')).toHaveCount(0);
    // إخفاء الزرّ ليس تخويلًا، لكن غيابه هنا انعكاس أمين لعقد الخادم.
    await expect(page.getByRole('button', { name: 'تأكيد الواقعة' })).toHaveCount(0);
  });

  test('ردّ الموظّف: الاعتراض يستلزم رواية مكتوبة ثمّ ينقل الواقعة إلى المراجعة', async ({ page }) => {
    const stub = newStub({ allowedActions: ['Acknowledge', 'Dispute'] });
    await stubApi(page, stub);
    await page.goto(`/app/attendance?incident=${INCIDENT_ID}`);

    await page.getByRole('button', { name: 'اعتراض' }).click();
    // فتح الحقل ليس إرسالًا.
    expect(stub.posts).toHaveLength(0);

    await page.getByLabel('نصّ الإجراء').fill('كنت في مهمّة معتمدة.');
    await page.getByRole('button', { name: /تأكيد اعتراض/ }).click();

    await expect.poll(() => stub.posts.length).toBe(1);
    expect(stub.posts[0].path).toBe(`attendance/${INCIDENT_ID}/dispute`);
    expect(stub.posts[0].body).toMatchObject({ concurrencyStamp: 3, response: 'كنت في مهمّة معتمدة.' });
    await expect(page.getByTestId('attendance-detail').getByText('معترَض عليها')).toBeVisible();
  });

  test('مراجعة الموارد البشريّة: التأكيد يمرّ بنقطة hr-review ولا يُنشئ أثرًا ماليًّا', async ({ page }) => {
    const stub = newStub({
      status: 'AwaitingHr', statusAr: 'بانتظار المراجعة',
      allowedActions: ['HrConfirm', 'HrReject'],
    });
    await stubApi(page, stub);
    await page.goto(`/app/attendance?incident=${INCIDENT_ID}`);

    await page.getByRole('button', { name: 'تأكيد الواقعة' }).click();
    await page.getByLabel('نصّ الإجراء').fill('لا إذن مسبق في السجلّ.');
    await page.getByRole('button', { name: /تأكيد تأكيد الواقعة/ }).click();

    await expect.poll(() => stub.posts.length).toBe(1);
    expect(stub.posts[0].path).toBe(`attendance/${INCIDENT_ID}/hr-review`);
    expect(stub.posts[0].body).toMatchObject({ decision: 'Confirm', concurrencyStamp: 3 });

    // لا نداء واحد إلى أيّ سطح ماليّ، ولا مفتاح ماليّ في الحمولة.
    expect(stub.posts.some((p) => /payroll|deduction|salary|balance/i.test(p.path))).toBeFalsy();
    expect(Object.keys(stub.posts[0].body as Record<string, unknown>)
      .some((k) => /payroll|deduct|salary|amount|balance/i.test(k))).toBeFalsy();

    await expect(page.getByTestId('attendance-detail').getByText('واقعة مؤكَّدة')).toBeVisible();
  });

  test('تسجيل بلاغ يحمل مفتاح تكافؤ فيمنع الازدواج عند إعادة المحاولة', async ({ page }) => {
    const stub = newStub();
    await stubApi(page, stub);
    await page.goto('/app/attendance');

    await page.getByRole('button', { name: 'تسجيل بلاغ' }).click();
    await page.getByLabel('معرّف الموظّف').fill('u-1');
    await page.getByLabel('نوع الواقعة').selectOption(TYPE_ID);
    await page.getByLabel('تاريخ الواقعة').fill('2026-08-18');
    await page.getByLabel('وقت البداية').fill('08:45');
    await page.getByLabel('وقت العودة').fill('09:30');
    await page.getByLabel('الوصف').fill('تأخّر بلا إشعار.');
    await page.getByRole('button', { name: 'إرسال البلاغ' }).click();

    await expect.poll(() => stub.posts.length).toBe(1);
    expect(stub.posts[0].idempotencyKey).toMatch(/^[0-9a-f-]{36}$/i);
  });

  test('الحقل الذي لم يرسله الخادم غائب من الشاشة لا معروضًا فارغًا', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto(`/app/attendance?incident=${INCIDENT_ID}`);
    const detail = page.getByTestId('attendance-detail');
    await expect(detail).toBeVisible();
    await expect(detail.getByText('ملاحظة الموارد البشريّة')).toHaveCount(0);
    // `exact` ضروريّ: تسمية الحالة «بانتظار ردّ الموظّف» تحوي النصّ نفسه جزئيًّا.
    await expect(detail.getByText('ردّ الموظّف', { exact: true })).toHaveCount(0);
  });

  test('حالة فارغة مستقلّة حين لا وقائع ضمن النطاق', async ({ page }) => {
    await stubApi(page, newStub(), { listEmpty: true });
    await page.goto('/app/attendance');
    await expect(page.getByRole('heading', { name: 'لا توجد وقائع', exact: true })).toBeVisible();
    await expect(page.getByTestId('attendance-list')).toHaveCount(0);
  });

  test('الاتّجاه من اليمين إلى اليسار على السطح كلّه', async ({ page }) => {
    await stubApi(page, newStub());
    await page.goto('/app/attendance');
    await expect(page.getByTestId('attendance-list')).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  for (const vp of [
    { name: 'مكتب', width: 1440, height: 900 },
    { name: 'لوح', width: 834, height: 1112 },
    { name: 'جوّال', width: 390, height: 844 },
  ]) {
    test(`الجدول والإجراءات قابلة للاستعمال على مقاس ${vp.name}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await stubApi(page, newStub({ allowedActions: ['Acknowledge'] }));
      await page.goto(`/app/attendance?incident=${INCIDENT_ID}`);

      await expect(page.getByTestId('attendance-detail')).toBeVisible();
      const button = page.getByRole('button', { name: 'إقرار بالواقعة' });
      await expect(button).toBeVisible();
      // الزرّ داخل عرض النافذة فعلًا لا مجرّد موجود في الشجرة.
      const box = await button.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.x).toBeGreaterThanOrEqual(0);
      expect(box!.x + box!.width).toBeLessThanOrEqual(vp.width + 1);
    });
  }
});

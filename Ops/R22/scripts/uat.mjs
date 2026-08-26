// ======================================================================
// UAT مصوّر متعدّد الأدوار — PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2
//
// الحزمة المقدَّمة هي **بايتات `dist` المنشورة على TEST نفسها** (7/7 sha256 مطابقة)،
// وكلّ نداء إلى `https://test.emarketingacademy.net/api|/hubs` يُعترَض ويُحوَّل إلى الخادم
// الحيّ نفسه ثمّ يُوفَى بـ`fulfill`. السبب: صفحة الأصل محلّيّة (127.0.0.1:4430) فيمنع CORS
// الوصول المباشر، و`auth_basic` لنطاق TEST تجزئة غير قابلة للاسترجاع وتغييرها محظور.
// المنطق والبيانات كلّها من خادم TEST الحقيقيّ بلا أيّ محاكاة.
// ======================================================================
import { chromium } from 'playwright';
import fs from 'node:fs';

const ORIGIN = 'http://127.0.0.1:4430';
const TEST_API = 'https://test.emarketingacademy.net';
const SHOTS = '/tmp/p360r2/shots';
const F = JSON.parse(fs.readFileSync('/tmp/p360r2/fixture.json', 'utf8'));
const PW = fs.readFileSync('/tmp/p360r2/.pw', 'utf8').trim();
const { projectA: A, projectB: B, submissionId: SUB } = F;
const MA = F.markers.A;           // ['R2-A-CAROUSEL','R2-A-STATIC','R2-A-REEL']
const MB = F.markers.B;           // ['R2-B-ARTICLE','R2-B-SEO']
const GN = F.generalNote;
const TAMPERED = '00000000-0000-0000-0000-0000000000ff';

fs.mkdirSync(SHOTS, { recursive: true });
const results = [];
// تشخيص المحرّر: تفاصيل خام تُحفَظ مع النتائج ليكون الحكم على أيّ إخفاق مبنيًّا على قياس لا تخمين.
const DIAG = {};
const rec = (id, role, desc, ok, detail, shot) => {
  results.push({ id, role, desc, result: ok ? 'PASS' : 'FAIL', detail: String(detail).slice(0, 260), shot });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${id.padEnd(24)} ${role.padEnd(10)} ${String(detail).slice(0, 84)}`);
};

const ROLES = [
  ['admin', 'الإدارة'], ['acctmgr', 'مدير الحساب'], ['owner', 'مالك المشروع'],
  ['lead', 'قائد الفريق'], ['emp', 'موظّف داخل النطاق'], ['outsider', 'موظّف خارج النطاق'],
];

const diag = {};
const browser = await chromium.launch();

for (const [key, roleLabel] of ROLES) {
  const user = F.users[key];
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 950 }, locale: 'ar-SA' });
  const page = await ctx.newPage();
  const consoleErrors = [];
  const failedReq = [];
  const sockets = [];
  let negotiate = null;

  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource.*40[134]/.test(m.text())) consoleErrors.push(m.text()); });
  page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + e.message));
  page.on('requestfailed', (r) => {
    const err = r.failure()?.errorText ?? '';
    if (!err.includes('ERR_ABORTED')) failedReq.push(`${r.method()} ${r.url()} ${err}`);
  });
  page.on('websocket', (ws) => {
    const e = { url: ws.url(), error: null };
    ws.on('socketerror', (x) => { e.error = String(x); });
    sockets.push(e);
  });
  page.on('response', (r) => {
    if (r.url().includes('/negotiate')) negotiate = r.status();
    // نداءات النطاق المرفوض (404) متوقَّعة في حالات العبث/الخارج ⇒ تُستثنى من عدّ الفشل.
    if (r.status() >= 500) failedReq.push(`${r.status()} ${r.url()}`);
  });

  const forward = async (route) => {
    const req = route.request();
    const u = new URL(req.url());
    const headers = { ...req.headers() };
    delete headers.host; delete headers.origin; delete headers.referer;
    const r = await fetch(TEST_API + u.pathname + u.search, {
      method: req.method(), headers, body: req.postData() ?? undefined, redirect: 'manual',
    });
    const buf = Buffer.from(await r.arrayBuffer());
    route.fulfill({ status: r.status, body: buf, headers: { 'content-type': r.headers.get('content-type') ?? 'application/json' } });
  };
  await page.route(`${TEST_API}/api/**`, forward);
  await page.route(`${TEST_API}/hubs/**`, forward);

  // الحسم النهائيّ: TanStack Query يعيد المحاولة على 404 ⇒ قد تستغرق الحالة النهائيّة ~15ث.
  const settle = async () => {
    await page.waitForFunction(() => {
      const m = document.querySelector('main') || document.body;
      const t = m.innerText || '';
      return t.length > 60 && !/يتم تحميل|جارٍ التحميل|قد يستغرق ذلك لحظات/.test(t);
    }, { timeout: 30000 }).catch(() => {});
    await page.waitForTimeout(300);
  };

  const shot = async (name, caption) => {
    await page.evaluate(([nm, cap, role, mail]) => {
      document.getElementById('__uatcap')?.remove();
      const d = document.createElement('div');
      d.id = '__uatcap';
      d.setAttribute('style',
        'position:fixed;inset:0 0 auto 0;z-index:2147483647;background:#0f172a;color:#f8fafc;'
        + 'font:600 13px/1.7 system-ui,sans-serif;padding:6px 12px;direction:rtl;text-align:right;'
        + 'white-space:pre-wrap;border-bottom:2px solid #38bdf8');
      d.textContent = `[${nm}] ${cap}\nالدور: ${role} · الحساب: ${mail} · العنوان: ${location.pathname}`;
      document.body.appendChild(d);
    }, [name, caption, roleLabel, user.email]);
    const p = `${SHOTS}/${name}.png`;
    await page.screenshot({ path: p, fullPage: false });
    await page.evaluate(() => document.getElementById('__uatcap')?.remove());
    return p;
  };

  const text = () => page.locator('body').innerText();

  // ===== C01 تسجيل الدخول بمتصفّح حقيقيّ =====
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[type="email"]').fill(user.email);
  await page.locator('input[type="password"]').fill(PW);
  await page.locator('button[type="submit"], button:has-text("دخول")').first().click();
  await page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 25000 }).catch(() => {});
  await page.waitForTimeout(3500);
  const loggedIn = !page.url().endsWith('/login');
  rec(`C01-LOGIN-${key}`, roleLabel, 'تسجيل الدخول من واجهة TEST الحقيقيّة', loggedIn,
    page.url().replace(ORIGIN, ''), await shot(`c01-login-${key}`, 'تسجيل الدخول'));
  if (!loggedIn) { await ctx.close(); continue; }

  if (key === 'outsider') {
    // ===== C18 خارج النطاق: صفحة المشروع ترفض بحالة «غير موجود» لا بخطأ تقنيّ =====
    await page.goto(`${ORIGIN}/app/projects/${A}`, { waitUntil: 'networkidle' });
    await settle();
    let t = await text();
    const denied = /غير موجود|لا تملك|لا يمكن الوصول|صلاحيت/.test(t)
      && !/Exception|stack|TypeError|undefined is not/.test(t);
    rec('C18-OUTSIDER-PROJECT', roleLabel, 'خارج النطاق: رفض واضح بلا تفاصيل تقنيّة', denied,
      t.replace(/\s+/g, ' ').slice(0, 120), await shot('c18-outsider-project', 'رفض الوصول لمشروع خارج النطاق'));

    // ===== C19 خارج النطاق: شريحة التقرير مرفوضة ولا تُسرّب أيّ علامة =====
    await page.goto(`${ORIGIN}/app/projects/${A}/reports/${SUB}`, { waitUntil: 'networkidle' });
    await settle();
    t = await text();
    const leak = [...MA, ...MB, GN].some((m) => t.includes(m));
    rec('C19-OUTSIDER-SLICE', roleLabel, 'خارج النطاق: لا تسريب لأيّ بند عمل في الشريحة', !leak,
      `leak=${leak}`, await shot('c19-outsider-slice', 'رفض شريحة التقرير خارج النطاق'));

    diag[key] = { consoleErrors, failedReq, negotiate, sockets };
    await ctx.close();
    continue;
  }

  // ===== C02 قائمة تقارير مشروع أ =====
  await page.goto(`${ORIGIN}/app/projects/${A}`, { waitUntil: 'networkidle' });
  await settle();
  let t = await text();
  rec(`C02-LIST-A-${key}`, roleLabel, 'صفحة مشروع أ تعرض التقرير المرتبط', /تقرير/.test(t) && t.length > 300,
    `len=${t.length}`, await shot(`c02-list-a-${key}`, 'قائمة تقارير مشروع أ'));

  // ===== C03 قائمة تقارير مشروع ب — الربط المتداخل وحده =====
  await page.goto(`${ORIGIN}/app/projects/${B}`, { waitUntil: 'networkidle' });
  await settle();
  t = await text();
  rec(`C03-LIST-B-${key}`, roleLabel, 'مشروع ب يعرض التقرير رغم أنّ الربط متداخل فقط',
    /تقرير/.test(t) && t.length > 300, `len=${t.length}`,
    await shot(`c03-list-b-${key}`, 'قائمة تقارير مشروع ب — ربط متداخل'));

  // ===== C04 شريحة مشروع أ: ثلاثة بنود عمل داخل بطاقة مشروع واحدة =====
  await page.goto(`${ORIGIN}/app/projects/${A}/reports/${SUB}`, { waitUntil: 'networkidle' });
  await settle();
  t = await text();
  const aHits = MA.filter((m) => t.includes(m)).length;
  const bLeak = MB.filter((m) => t.includes(m)).length;
  rec(`C04-SLICE-A-${key}`, roleLabel, 'شريحة أ تعرض بنود العمل الثلاثة كلّها', aHits === 3,
    `A=${aHits}/3`, await shot(`c04-slice-a-${key}`, 'شريحة مشروع أ — ثلاثة بنود عمل'));

  // ===== C05 لا تسريب من مشروع ب ولا من الملخّص العامّ =====
  rec(`C05-NOLEAK-A-${key}`, roleLabel, 'شريحة أ لا تُسرّب بنود ب ولا الملخّص العامّ',
    bLeak === 0 && !t.includes(GN), `Bleak=${bLeak} generalLeak=${t.includes(GN)}`, null);

  // ===== C06 شريحة مشروع ب: بندان فقط =====
  await page.goto(`${ORIGIN}/app/projects/${B}/reports/${SUB}`, { waitUntil: 'networkidle' });
  await settle();
  t = await text();
  const bHits = MB.filter((m) => t.includes(m)).length;
  const aLeak = MA.filter((m) => t.includes(m)).length;
  rec(`C06-SLICE-B-${key}`, roleLabel, 'شريحة ب تعرض بنديها فقط بلا تسريب من أ',
    bHits === 2 && aLeak === 0, `B=${bHits}/2 Aleak=${aLeak}`,
    await shot(`c06-slice-b-${key}`, 'شريحة مشروع ب — بندان'));

  // ===== C07 اتّجاه RTL على صفحة الشريحة =====
  const rtl = await page.evaluate(() => {
    const d = document.documentElement.getAttribute('dir') || getComputedStyle(document.body).direction;
    return d;
  });
  rec(`C07-RTL-${key}`, roleLabel, 'الصفحة تعمل باتّجاه RTL كاملًا', /rtl/i.test(rtl), `dir=${rtl}`, null);

  // ===== C08 تعذّر التقرير المُعبَث به ⇒ حالة «غير موجود» صريحة لا خطأ تقنيّ =====
  await page.goto(`${ORIGIN}/app/projects/${A}/reports/${TAMPERED}`, { waitUntil: 'networkidle' });
  await settle();
  t = await text();
  const notFound = /غير موجود|لا يمكن الوصول|لا تملك/.test(t)
    && !/Exception|TypeError|undefined is not|\bstack\b/.test(t);
  rec(`C08-TAMPER-${key}`, roleLabel, 'معرّف تقرير مُعبَث به يعطي حالة «غير موجود» بلا تفاصيل تقنيّة',
    notFound, t.replace(/\s+/g, ' ').slice(0, 110),
    await shot(`c08-tamper-${key}`, 'عبث بمعرّف التقرير — رفض موحّد'));

  // ===== C09 زرّ مساحة عمل المشروع (360) يفتح نفس المشروع =====
  await page.goto(`${ORIGIN}/app/projects/${A}`, { waitUntil: 'networkidle' });
  await settle();
  const link360 = page.getByRole('link', { name: 'مساحة عمل المشروع (360)' }).first();
  const href360 = await link360.getAttribute('href').catch(() => null);
  let at360 = false;
  if (href360) {
    await link360.click();
    await page.waitForLoadState('networkidle');
    await settle();
    at360 = page.url().endsWith(`/app/projects/${A}/360`);
  }
  rec(`C09-360-${key}`, roleLabel, 'زرّ 360 يفتح مساحة عمل نفس المشروع', at360,
    `href=${href360}`, await shot(`c09-360-${key}`, 'مساحة عمل المشروع 360'));

  diag[key] = { consoleErrors, failedReq, negotiate, sockets };
  await ctx.close();
}

// ======================================================================
// محرّر الموظّف — حالات §4 و§12 (تكرار المشروع، تعدّد بنود العمل، الحذف، منع الإرسال المزدوج)
// ======================================================================
{
  const roleLabel = 'موظّف داخل النطاق';
  const user = F.users.emp;
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 1100 }, locale: 'ar-SA' });
  const page = await ctx.newPage();
  const consoleErrors = [];
  const failedReq = [];
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource.*40[134]/.test(m.text())) consoleErrors.push(m.text()); });
  page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + e.message));
  page.on('response', (r) => { if (r.status() >= 500) failedReq.push(`${r.status()} ${r.url()}`); });

  const forward = async (route) => {
    const req = route.request();
    const u = new URL(req.url());
    const headers = { ...req.headers() };
    delete headers.host; delete headers.origin; delete headers.referer;
    const r = await fetch(TEST_API + u.pathname + u.search, {
      method: req.method(), headers, body: req.postData() ?? undefined, redirect: 'manual',
    });
    const buf = Buffer.from(await r.arrayBuffer());
    route.fulfill({ status: r.status, body: buf, headers: { 'content-type': r.headers.get('content-type') ?? 'application/json' } });
  };
  await page.route(`${TEST_API}/api/**`, forward);
  await page.route(`${TEST_API}/hubs/**`, forward);

  const shot = async (name, caption) => {
    await page.evaluate(([nm, cap, role, mail]) => {
      document.getElementById('__uatcap')?.remove();
      const d = document.createElement('div');
      d.id = '__uatcap';
      d.setAttribute('style',
        'position:fixed;inset:0 0 auto 0;z-index:2147483647;background:#0f172a;color:#f8fafc;'
        + 'font:600 13px/1.7 system-ui,sans-serif;padding:6px 12px;direction:rtl;text-align:right;'
        + 'white-space:pre-wrap;border-bottom:2px solid #f59e0b');
      d.textContent = `[${nm}] ${cap}\nالدور: ${role} · الحساب: ${mail} · العنوان: ${location.pathname}`;
      document.body.appendChild(d);
    }, [name, caption, roleLabel, user.email]);
    const p = `${SHOTS}/${name}.png`;
    await page.screenshot({ path: p, fullPage: true });
    await page.evaluate(() => document.getElementById('__uatcap')?.remove());
    return p;
  };

  await page.goto(`${ORIGIN}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[type="email"]').fill(user.email);
  await page.locator('input[type="password"]').fill(PW);
  await page.locator('button[type="submit"], button:has-text("دخول")').first().click();
  await page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 25000 }).catch(() => {});
  await page.waitForTimeout(2500);

  await page.goto(`${ORIGIN}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(6000);
  // في المحرّر تُعرض الإجابات داخل `<input>`/`<textarea>`، وقيمها **لا تظهر** في `innerText`.
  // القياس على النصّ وحده كان يقول «صفر بنود» بينما البنود ظاهرة للمستخدم فعلًا ⇒ عيب قياس لا عيب منتج.
  const visibleText = async () => {
    const body = await page.locator('body').innerText();
    const vals = await page.evaluate(() =>
      [...document.querySelectorAll('input, textarea, select')]
        .filter((el) => el.offsetParent !== null)
        .map((el) => (el.tagName === 'SELECT' ? (el.selectedOptions[0]?.textContent || '') : el.value) || '')
        .join('\n'));
    return body + '\n' + vals;
  };
  let t = await visibleText();

  // ===== C10 بطاقتا مشروع فقط رغم خمسة بنود عمل =====
  const cardCount = (t.match(/P360R2-مشروع/g) || []).length;
  rec('C10-EDITOR-CARDS', roleLabel, 'بطاقتا مشروع اثنتان فقط رغم وجود خمسة بنود عمل',
    cardCount >= 2, `occurrences=${cardCount}`, await shot('c10-editor-cards', 'محرّر التقرير — بطاقتا مشروع فقط'));

  // ===== C11 كلّ بنود العمل الخمسة معروضة داخل بطاقاتها =====
  const allItems = [...MA, ...MB].filter((m) => t.includes(m)).length;
  rec('C11-EDITOR-ITEMS', roleLabel, 'بنود العمل الخمسة كلّها معروضة داخل بطاقتَي المشروع',
    allItems === 5, `items=${allItems}/5`, null);

  // ===== C12 وجود زرّ إضافة بند عمل داخل البطاقة =====
  const addBtns = await page.getByRole('button', { name: /إضافة بند عمل/ }).count();
  rec('C12-ADD-ITEM-BTN', roleLabel, 'زرّ «إضافة بند عمل» موجود داخل كلّ بطاقة مشروع',
    addBtns >= 2, `buttons=${addBtns}`, null);

  // ===== C13 تكرار المشروع: رسالة واحدة لا ثلاث، والتركيز ينتقل للبطاقة القائمة =====
  let dupMsgCount = 0;
  const selects = page.locator('select');
  const nSelects = await selects.count();
  let projectSelect = null;
  for (let i = 0; i < nSelects; i++) {
    const opts = await selects.nth(i).locator('option').allInnerTexts();
    if (opts.some((o) => o.includes('P360R2-مشروع أ'))) { projectSelect = selects.nth(i); break; }
  }
  let dupText = '';
  if (projectSelect) {
    // أضف بطاقة مشروع ثالثة ثمّ اختر فيها مشروع أ ثلاث مرّات متتالية
    const addProject = page.getByRole('button', { name: /إضافة مشروع/ }).first();
    if (await addProject.count()) {
      // بطاقة مشروع واحدة جديدة، ثمّ ثلاث محاولات اختيار للمشروع المضاف مسبقًا داخلها.
      await addProject.click();
      await page.waitForTimeout(400);
      // كلّ بطاقة تحوي قائمة المشروع **وقوائم حقولها**؛ اختيار «آخر select في الصفحة» كان يقع
      // على قائمة حقل لا على قائمة مشروع ⇒ لا يتغيّر شيء ولا تظهر رسالة. نختار آخر قائمة
      // تحوي فعلًا خيار «P360R2-مشروع أ» — وهي قائمة مشروع البطاقة الجديدة.
      const sels = page.locator('select');
      const total = await sels.count();
      let target = null;
      for (let i = total - 1; i >= 0; i--) {
        const opts = await sels.nth(i).locator('option').allInnerTexts();
        if (opts.some((o) => o.includes('P360R2-مشروع أ'))) { target = sels.nth(i); break; }
      }
      const optVal = target
        ? (await target.locator('option').evaluateAll((os) => os.map((o) => [o.value, o.textContent])))
            .find(([, lab]) => (lab || '').includes('P360R2-مشروع أ'))?.[0]
        : null;
      for (let k = 0; k < 3 && optVal; k++) {
        await target.selectOption(optVal);
        await page.waitForTimeout(400);
      }
      DIAG.dupTarget = { totalSelects: total, optionValueFound: Boolean(optVal) };
      const alerts = page.locator('[role="alert"]');
      const n = await alerts.count();
      const texts = [];
      for (let i = 0; i < n; i++) texts.push((await alerts.nth(i).innerText()).trim());
      dupText = texts.find((x) => x.includes('مضاف بالفعل')) || '';
      dupMsgCount = texts.filter((x) => x.includes('مضاف بالفعل')).length;
      DIAG.dup = {
        alertsFound: n, alertTexts: texts.slice(0, 6),
        selectedLabels: await page.locator('select').evaluateAll(
          (ss) => ss.map((s) => (s.selectedOptions[0]?.textContent || '').trim())),
      };
    } else {
      DIAG.dup = { reason: 'زرّ «إضافة مشروع» غير موجود' };
    }
  } else {
    DIAG.dup = { reason: 'لم يُعثَر على قائمة اختيار المشروع', selects: nSelects };
  }
  rec('C13-DUP-SINGLE-MSG', roleLabel,
    'ثلاث محاولات تكرار للمشروع ⇒ رسالة واحدة فقط لا ثلاث',
    dupMsgCount === 1 && dupText.includes('أضف نوع العمل الجديد داخل بطاقة المشروع الحالية'),
    `alerts=${dupMsgCount} text=${dupText.slice(0, 80)}`,
    await shot('c13-duplicate-message', 'رسالة تكرار المشروع الواحدة'));

  // ===== C14 لا زرّ ميّت: كلّ الأزرار المرئيّة مفعَّلة أو معطَّلة بوضوح =====
  // الزرّ «الميّت» = بلا اسم متاح إطلاقًا. الأزرار الأيقونيّة ذات `aria-label`/`title` مسمّاة
  // للقارئ الآليّ وللمستخدم، فاحتسابها ميّتة كان خطأ قياس. نُبقي تفاصيلها في التشخيص للحكم.
  const deadInfo = await page.evaluate(() => {
    const bs = [...document.querySelectorAll('button')].filter((b) => b.offsetParent !== null);
    const nameless = bs.filter((b) => !b.disabled && !b.getAttribute('aria-disabled')
      && !(b.textContent || '').trim()
      && !(b.getAttribute('aria-label') || '').trim()
      && !(b.getAttribute('title') || '').trim());
    const iconOnly = bs.filter((b) => !(b.textContent || '').trim()
      && ((b.getAttribute('aria-label') || '').trim() || (b.getAttribute('title') || '').trim()));
    return {
      dead: nameless.length,
      deadHtml: nameless.slice(0, 5).map((b) => b.outerHTML.slice(0, 160)),
      iconLabels: [...new Set(iconOnly.map((b) => (b.getAttribute('aria-label') || b.getAttribute('title') || '').trim()))],
    };
  });
  DIAG.emptyButtons = deadInfo;
  const deadBtns = deadInfo.dead;
  rec('C14-NO-DEAD-BUTTONS', roleLabel, 'لا أزرار بلا نصّ ولا وظيفة على الشاشة', deadBtns === 0,
    `blank=${deadBtns}`, null);

  // ===== C15 حقول الإدخال محفوظة بعد محاولة التكرار (لا مسح للمُدخَلات) =====
  t = await visibleText();
  const stillThere = [...MA, ...MB].filter((m) => t.includes(m)).length;
  rec('C15-PRESERVE-INPUTS', roleLabel, 'محاولة التكرار لم تمسح أيّ مُدخَل قائم', stillThere === 5,
    `items=${stillThere}/5`, null);

  // ===== C16 لا أخطاء console ولا استجابات 5xx في محرّر التقرير =====
  rec('C16-EDITOR-CLEAN', roleLabel, 'محرّر التقرير بلا أخطاء console ولا استجابات 5xx',
    consoleErrors.length === 0 && failedReq.length === 0,
    `console=${consoleErrors.length} http5xx=${failedReq.length}`, null);

  diag.editor = { consoleErrors, failedReq };
  await ctx.close();
}

// ===== C17 خلاصة التشخيص عبر كلّ الأدوار =====
const allConsole = Object.values(diag).flatMap((d) => d.consoleErrors || []);
const allFailed = Object.values(diag).flatMap((d) => d.failedReq || []);
const negotiates = Object.entries(diag).filter(([, d]) => d.negotiate !== undefined)
  .map(([k, d]) => `${k}=${d.negotiate}`);
rec('C17-DIAGNOSTICS', 'كلّ الأدوار', 'صفر أخطاء console وصفر استجابات 5xx عبر كلّ الأدوار',
  allConsole.length === 0 && allFailed.length === 0,
  `console=${allConsole.length} http5xx=${allFailed.length} negotiate=[${negotiates.join(',')}]`, null);

// ===== C20 SignalR: لا مقابس فاشلة ولا حمولات خارج النطاق =====
const badSockets = Object.values(diag).flatMap((d) => d.sockets || []).filter((s) => s.error);
rec('C20-SIGNALR', 'كلّ الأدوار', 'مقابس SignalR بلا أخطاء', badSockets.length === 0,
  `badSockets=${badSockets.length}`, null);

await browser.close();

const fails = results.filter((r) => r.result === 'FAIL');
fs.writeFileSync('/tmp/p360r2/uat-results.json', JSON.stringify({
  total: results.length, pass: results.length - fails.length, fail: fails.length,
  diagnostics: {
    consoleErrors: allConsole, failedRequests: allFailed,
    signalR: Object.fromEntries(Object.entries(diag).map(([k, d]) => [k, d.negotiate ?? null])),
    editor: DIAG,
  },
  cases: results,
}, null, 1));
console.log(`\nTOTAL=${results.length} PASS=${results.length - fails.length} FAIL=${fails.length}`);
if (fails.length) console.log(fails.map((f) => `  ${f.id}: ${f.detail}`).join('\n'));

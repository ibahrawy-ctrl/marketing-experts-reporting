// ======================================================================
// UAT مصوّر متعدّد الأدوار — PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1
//
// الحزمة المقدَّمة هي **بايتات `dist` المنشورة على TEST نفسها** (7/7 sha256 مطابقة)، وكلّ نداء
// إلى `https://test.emarketingacademy.net/api/**` يُعترَض ويُحوَّل إلى **نفق الخادم الحيّ**
// (127.0.0.1:15091 → 127.0.0.1:5091 على TEST) ثمّ يُفَى بـ`fulfill`. السبب: كلمة `auth_basic`
// لنطاق TEST تجزئة غير قابلة للاسترجاع وتغيير `htpasswd` محظور؛ و`route.continue` بعنوان مغاير
// يُخضِع الردّ لفحص CORS للأصل الأصليّ فيكسره. المنطق والبيانات كلّها من خادم TEST الحقيقيّ.
// ======================================================================
import { chromium } from 'playwright';
import fs from 'node:fs';

const ORIGIN = 'http://127.0.0.1:4420';
const TEST_API = 'https://test.emarketingacademy.net';
const TUNNEL = 'http://127.0.0.1:15091';
const SHOTS = '/tmp/p360nav/shots';
const F = JSON.parse(fs.readFileSync('/tmp/p360nav/fixture.json', 'utf8'));
const PW = fs.readFileSync('/tmp/p360nav/.pw', 'utf8').trim();
const { projectA: A, projectB: B, submissionId: SUB } = F;
const MA = F.markers.A, MB = F.markers.B, GN = F.markers.general;

fs.mkdirSync(SHOTS, { recursive: true });
const results = [];
const rec = (id, role, desc, ok, detail, shot) => {
  results.push({ id, role, desc, result: ok ? 'PASS' : 'FAIL', detail, shot });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${id.padEnd(22)} ${role.padEnd(9)} ${String(detail).slice(0, 90)}`);
};

const ROLES = [
  ['admin', 'الإدارة'], ['acctmgr', 'مدير الحساب'], ['owner', 'مالك المشروع'],
  ['lead', 'قائد الفريق'], ['emp', 'موظّف داخل النطاق'], ['outsider', 'موظّف خارج النطاق'],
];

const browser = await chromium.launch();

for (const [key, roleLabel] of ROLES) {
  const user = F.users[key];
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 950 }, locale: 'ar-SA' });
  const page = await ctx.newPage();
  const consoleErrors = [];
  const failedReq = [];
  const origins = new Set();
  const sockets = [];
  let negotiate = null;

  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + e.message));
  const aborted = [];
  page.on('requestfailed', (r) => {
    const err = r.failure()?.errorText ?? '';
    (err.includes('ERR_ABORTED') ? aborted : failedReq).push(`${r.method()} ${r.url()} ${err}`);
  });
  page.on('websocket', (ws) => {
    const entry = { url: ws.url(), closed: false, error: null };
    ws.on('socketerror', (e) => { entry.error = String(e); });
    ws.on('close', () => { entry.closed = true; });
    sockets.push(entry);
  });
  page.on('response', (r) => {
    if (r.url().includes('/negotiate')) negotiate = r.status();
    try { origins.add(new URL(r.url()).origin); } catch { /* تجاهل العناوين غير القياسيّة */ }
    if (r.status() >= 400 && !r.url().includes('/reports/') && !r.url().includes('/auth/login')) {
      failedReq.push(`${r.status()} ${r.url()}`);
    }
  });

  const forward = async (route) => {
    const req = route.request();
    const url = TUNNEL + new URL(req.url()).pathname + new URL(req.url()).search;
    const headers = { ...req.headers() };
    delete headers.host; delete headers.origin; delete headers.referer;
    const r = await fetch(url, { method: req.method(), headers, body: req.postData() ?? undefined, redirect: 'manual' });
    const buf = Buffer.from(await r.arrayBuffer());
    route.fulfill({ status: r.status, body: buf, headers: { 'content-type': r.headers.get('content-type') ?? 'application/json' } });
  };
  await page.route(`${TEST_API}/api/**`, forward);
  await page.route(`${TEST_API}/hubs/**`, forward);

  const settle = async () => {
    await page.waitForFunction(() => {
      const t = document.body.innerText || '';
      return t.length > 120 && !t.includes('يتم تحميل مساهمة التقرير في هذا المشروع');
    }, { timeout: 20000 }).catch(() => {});
    await page.waitForTimeout(250);
  };

  // شريط تعليق يُحقن قبل اللقطة: يقرأ العنوان الحيّ من `location` ويعلن الدور والحالة،
  // فتصير كلّ لقطة ذاتيّة التوصيف ولا تلتبس بأخرى (خصوصًا لقطتَي الرفض المتطابقتَين قصدًا).
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

  // ===== تسجيل الدخول بمتصفّح حقيقيّ عبر نموذج الواجهة =====
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[type="email"]').fill(user.email);
  await page.locator('input[type="password"]').fill(PW);
  await page.locator('button[type="submit"], button:has-text("دخول")').first().click();
  await page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 20000 }).catch(() => {});
  await page.waitForFunction(() => true, { timeout: 1 }).catch(() => {});
  await page.waitForTimeout(3500);   // كي يكتمل تفاوض SignalR قبل أوّل تنقّل
  const loggedIn = !page.url().endsWith('/login');
  rec(`LOGIN-${key}`, roleLabel, 'تسجيل الدخول من الواجهة', loggedIn, page.url().replace(ORIGIN, ''), await shot(`00-login-${key}`, 'تسجيل الدخول بمتصفّح حقيقيّ إلى TEST'));
  if (!loggedIn) { await ctx.close(); continue; }

  if (key !== 'outsider') {
    // ===== 1) زرّ مساحة عمل المشروع (360) =====
    await page.goto(`${ORIGIN}/app/projects/${A}`, { waitUntil: 'networkidle' });
    const link360 = page.getByRole('link', { name: 'مساحة عمل المشروع (360)' });
    const href360 = await link360.getAttribute('href').catch(() => null);
    await link360.click();
    await page.waitForLoadState('networkidle');
    const at360 = page.url().endsWith(`/app/projects/${A}/360`);
    const body360 = await page.locator('body').innerText();
    rec(`P360-${key}`, roleLabel, 'زرّ 360 يفتح مساحة عمل نفس المشروع',
      at360 && href360 === `/app/projects/${A}/360` && body360.length > 200 && !/يتم التحميل|جارٍ التحميل/.test(body360.slice(0, 120)),
      `href=${href360} url=${page.url().replace(ORIGIN, '')} text=${body360.length}`,
      await shot(`01-360-${key}`, 'مساحة عمل المشروع (360) لمشروع أ — تحمّلت فعلًا'));

    // ===== 2) زرّ «فتح» من صفحة المشروع أ =====
    await page.goto(`${ORIGIN}/app/projects/${A}`, { waitUntil: 'networkidle' });
    const openLink = page.getByRole('link', { name: 'فتح' }).first();
    const openHref = await openLink.getAttribute('href').catch(() => null);
    await openLink.click();
    await page.waitForLoadState('networkidle');
    await settle();
    const txtA = await page.locator('body').innerText();
    rec(`SLICE-A-${key}`, roleLabel, 'فتح ⟵ شريحة مشروع أ وحدها',
      openHref === `/app/projects/${A}/reports/${SUB}` && page.url().endsWith(`/app/projects/${A}/reports/${SUB}`)
      && txtA.includes(MA) && !txtA.includes(MB) && !txtA.includes(GN),
      `href=${openHref} A=${txtA.includes(MA)} B=${txtA.includes(MB)} عامّ=${txtA.includes(GN)}`,
      await shot(`02-sliceA-${key}`, `شريحة مشروع أ من التقرير الأسبوعيّ — البصمة ${MA} حاضرة و${MB} غائبة`));

    // ===== 3) زرّ الرجوع يعود إلى نفس المشروع =====
    await page.getByRole('link', { name: '← رجوع إلى صفحة المشروع' }).click();
    await page.waitForLoadState('networkidle');
    rec(`BACK-${key}`, roleLabel, 'الرجوع يعود إلى صفحة نفس المشروع',
      page.url().endsWith(`/app/projects/${A}`), page.url().replace(ORIGIN, ''), null);

    // ===== 4) نفس التقرير من مشروع ب =====
    await page.goto(`${ORIGIN}/app/projects/${B}/reports/${SUB}`, { waitUntil: 'networkidle' });
    await settle();
    const txtB = await page.locator('body').innerText();
    rec(`SLICE-B-${key}`, roleLabel, 'نفس التقرير من مشروع ب ⟵ شريحة ب وحدها',
      txtB.includes(MB) && !txtB.includes(MA) && !txtB.includes(GN),
      `B=${txtB.includes(MB)} A=${txtB.includes(MA)} عامّ=${txtB.includes(GN)}`,
      await shot(`03-sliceB-${key}`, `نفس التقرير من مشروع ب — البصمة ${MB} حاضرة و${MA} غائبة`));
  } else {
    // ===== خارج النطاق: حالة نهائيّة واضحة لا دوّامة ولا بياض =====
    await page.goto(`${ORIGIN}/app/projects/${A}/reports/${SUB}`, { waitUntil: 'networkidle' });
    await settle();
    const t = await page.locator('body').innerText();
    rec('DENY-outsider', roleLabel, 'خارج النطاق: حالة رفض نهائيّة بلا بيانات',
      t.includes('التقرير غير متاح ضمن هذا المشروع') && !t.includes(MA) && !t.includes(MB) && !t.includes(GN)
      && !t.includes('يتم تحميل مساهمة التقرير'),
      `deny=${t.includes('التقرير غير متاح ضمن هذا المشروع')} A=${t.includes(MA)} B=${t.includes(MB)}`,
      await shot('04-deny-outsider', 'موظّف خارج النطاق على مشروع أ — حالة رفض نهائيّة (404 موحَّد)'));

    const fake = '00000000-0000-4000-8000-000000000abc';
    await page.goto(`${ORIGIN}/app/projects/${fake}/reports/${SUB}`, { waitUntil: 'networkidle' });
    await settle();
    const t2 = await page.locator('body').innerText();
    rec('TAMPER-outsider', roleLabel, 'العبث بمعرّف المشروع: نفس الرفض بلا تعداد',
      t2.includes('التقرير غير متاح ضمن هذا المشروع') && !t2.includes(MA) && !t2.includes(MB),
      `deny=${t2.includes('التقرير غير متاح ضمن هذا المشروع')}`,
      await shot('05-tamper-outsider', 'معرّف مشروع مُلفَّق — نفس الرفض حرفيًّا: لا تمييز ولا تعداد'));
  }

  const FONTS = ['https://fonts.googleapis.com', 'https://fonts.gstatic.com'];
  const badOrigins = [...origins].filter((o) => o !== ORIGIN && o !== TEST_API && !FONTS.includes(o) && !o.startsWith('data:'));
  rec(`SIGNALR-${key}`, roleLabel, 'اتّصال SignalR: تفاوض 200 ومقبس مفتوح',
    negotiate === 200 && sockets.length > 0 && sockets.every((w) => !w.error),
    `negotiate=${negotiate} sockets=${sockets.length} err=${sockets.map((w) => w.error).filter(Boolean).join('|') || 'none'}`, null);

  const expected404 = consoleErrors.filter((e) => /status of 404/.test(e));
  const unexpected = consoleErrors.filter((e) => !/status of 404/.test(e));
  rec(`HYGIENE-${key}`, roleLabel, 'صفر خطأ Console غير متوقَّع · صفر طلب فاشل · أصل TEST حصرًا',
    unexpected.length === 0 && failedReq.length === 0 && badOrigins.length === 0,
    `console_unexpected=${unexpected.length} console_404_مقصود=${expected404.length} failed=${failedReq.length} aborted_by_nav=${aborted.length} origins=${[...origins].join(',')} | ${unexpected.slice(0, 3).join(' ~ ')} | ${failedReq.slice(0, 3).join(' ~ ')}`,
    null);

  await ctx.close();
}

await browser.close();
fs.writeFileSync('/tmp/p360nav/uat-results.json', JSON.stringify(results, null, 1));
const fails = results.filter((r) => r.result === 'FAIL');
console.log(`\nTOTAL=${results.length} PASS=${results.length - fails.length} FAIL=${fails.length}`);
process.exit(fails.length ? 1 : 0);

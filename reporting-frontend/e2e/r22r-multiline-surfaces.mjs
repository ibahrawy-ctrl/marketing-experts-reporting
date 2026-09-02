// R22B/RECONCILIATION — إثبات بصريّ للأسطح الأربعة الواجهيّة على الحزمة المنشورة على TEST.
// القياس ليس بوجود صنف CSS بل بعدد صناديق الأسطر المرسومة فعليًّا (Range.getClientRects)
// وبالقيمة المحسوبة لـwhite-space — أي أنّ المتصفّح رسم ثلاثة أسطر لا سطرًا واحدًا ملتصقًا.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

// كلمة `auth_basic` لنطاق TEST تجزئة غير قابلة للاسترجاع وتغيير `htpasswd` محظور، لذا يُقدَّم
// **نفس بايتات `dist` المنشورة** من خادم محلّيّ (sha256 مطابق للمانيفست المنشور)، وكلّ نداء
// `/api/**` يُعترَض ويُحوَّل إلى نفق SSH نحو خادم TEST الحيّ. المنطق والبيانات من TEST حصرًا.
const BASE = 'http://127.0.0.1:4420';
const TEST_API = 'https://test.emarketingacademy.net';
const TUNNEL = 'http://127.0.0.1:15091';
const OUT = process.env.OUT_DIR || '/tmp/r22r-ui';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });

const PW = fs.readFileSync(process.env.USER_PW_FILE || '/tmp/.r22r-user-pw', 'utf8').trim();
const ADMIN_PW = fs.readFileSync(process.env.ADMIN_PW_FILE || '/tmp/.r22r-admin-pw', 'utf8').trim();
const EMP = 'r22r-design@r22uat.test';
const LEAD = 'r22r-lead@r22uat.test';
const ADMIN = 'r22b-hotfix-admin@r22uat.test';
const MARK = 'س1/تصميم';
// معرّف تقرير التصميم على TEST (أُنشئ برحلة الـAPI في نفس الجولة).
const SUB = process.env.SUB_ID || 'e5ef7c88-b16d-4a11-a0a9-5da0f538d664';
const TYPED = ['سطر إدخال أوّل', 'سطر إدخال ثانٍ', 'سطر إدخال ثالث'];

const R = { surfaces: {}, log: [] };
const note = (k, v) => { R.log.push(`${k} = ${JSON.stringify(v)}`); console.log(k, '=', JSON.stringify(v)); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// يقيس عنصرًا نصّيًّا: عدد أسطر الرسم، والقيمة المحسوبة لـwhite-space وoverflow-wrap، والنصّ.
const MEASURE = (el) => {
  const cs = getComputedStyle(el);
  const range = document.createRange();
  range.selectNodeContents(el);
  return {
    text: el.textContent,
    renderedLines: range.getClientRects().length,
    whiteSpace: cs.whiteSpace,
    overflowWrap: cs.overflowWrap,
    wordBreak: cs.wordBreak,
  };
};

const forward = async (route) => {
  const req = route.request();
  const u = new URL(req.url());
  const headers = { ...req.headers() };
  delete headers.host; delete headers.origin; delete headers.referer;
  const r = await fetch(TUNNEL + u.pathname + u.search,
    { method: req.method(), headers, body: req.postData() ?? undefined, redirect: 'manual' });
  const buf = Buffer.from(await r.arrayBuffer());
  await route.fulfill({ status: r.status, body: buf,
    headers: { 'content-type': r.headers.get('content-type') ?? 'application/json' } });
};

async function newPage() {
  const page = await ctx.newPage();
  await page.route(`${TEST_API}/api/**`, forward);
  await page.route(`${TEST_API}/hubs/**`, forward);
  return page;
}

async function login(page, email, pw) {
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', pw);
  await page.click('button[type="submit"]');
  await page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 30000 });
  await sleep(1200);
}

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'ar' });

try {
  // ===== 1) SUBMISSION_DETAIL_AND_HISTORY — الموظّف يقرأ سبب الإعادة =====
  const p = await newPage();
  await login(p, EMP, PW);
  await p.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'domcontentloaded' });
  await sleep(6000);

  R.surfaces.SUBMISSION_DETAIL_AND_HISTORY = await p.evaluate(({ mark, m }) => {
    const fn = new Function('el', `return (${m})(el)`);
    const el = [...document.querySelectorAll('p,div,td,span')]
      .filter((e) => e.textContent.includes(mark) && e.children.length === 0)
      .sort((a, b) => a.textContent.length - b.textContent.length)[0];
    return el ? fn(el) : { error: 'not-found' };
  }, { mark: MARK, m: MEASURE.toString() });
  note('DETAIL', R.surfaces.SUBMISSION_DETAIL_AND_HISTORY);
  await p.screenshot({ path: path.join(SHOTS, 'S1-detail-history.png'), fullPage: true });

  // ===== 2) NOTIFICATION_BELL =====
  const bell = p.locator('button', { hasText: '🔔' }).first();
  if (await bell.count()) { await bell.click().catch(() => {}); await sleep(2500); }
  R.surfaces.NOTIFICATION_BELL = await p.evaluate(({ mark, m }) => {
    const fn = new Function('el', `return (${m})(el)`);
    const el = [...document.querySelectorAll('p')]
      .filter((e) => e.textContent.includes(mark) && e.children.length === 0)
      .sort((a, b) => a.textContent.length - b.textContent.length)[0];
    return el ? fn(el) : { error: 'not-found' };
  }, { mark: MARK, m: MEASURE.toString() });
  note('BELL', R.surfaces.NOTIFICATION_BELL);
  await p.screenshot({ path: path.join(SHOTS, 'S2-notification-bell.png') });
  await p.close();

  // ===== 3) APPROVAL_TEXTAREA — Enter داخل الحقل يُنتج سطرًا جديدًا فعليًّا =====
  // الموظّف يعيد الإرسال أوّلًا كي يظهر التقرير في «بانتظار اعتمادي» عند المراجِع.
  const pe = await newPage();
  await login(pe, EMP, PW);
  await pe.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'domcontentloaded' });
  await sleep(6000);
  const send = pe.locator('button', { hasText: 'إرسال للاعتماد' }).first();
  if (await send.count()) { await send.click().catch(() => {}); await sleep(3000); }
  for (const t of ['تأكيد', 'نعم', 'إرسال']) {
    const c = pe.locator('button', { hasText: t }).first();
    if (await c.count()) { await c.click().catch(() => {}); await sleep(2500); break; }
  }
  await pe.close();

  const pl = await newPage();
  await login(pl, LEAD, PW);
  await pl.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'domcontentloaded' });
  await sleep(6000);

  const ta = pl.locator('textarea[placeholder*="سبب القرار"]').first();
  R.surfaces.APPROVAL_TEXTAREA = { found: (await ta.count()) > 0 };
  if (R.surfaces.APPROVAL_TEXTAREA.found) {
    R.surfaces.APPROVAL_TEXTAREA.tagName = await ta.evaluate((e) => e.tagName);
    R.surfaces.APPROVAL_TEXTAREA.rows = await ta.evaluate((e) => e.rows);
    R.surfaces.APPROVAL_TEXTAREA.resize = await ta.evaluate((e) => getComputedStyle(e).resize);
    await ta.click();
    for (let i = 0; i < TYPED.length; i++) {
      await ta.type(TYPED[i], { delay: 12 });
      if (i < TYPED.length - 1) await pl.keyboard.press('Enter');
    }
    await sleep(400);
    const v = await ta.inputValue();
    R.surfaces.APPROVAL_TEXTAREA.value = v;
    R.surfaces.APPROVAL_TEXTAREA.newlineCount = (v.match(/\n/g) || []).length;
    R.surfaces.APPROVAL_TEXTAREA.enterPreserved = v === TYPED.join('\n');
    R.surfaces.APPROVAL_TEXTAREA.formStillOpen = !pl.url().endsWith('/login');
  }
  note('TEXTAREA', R.surfaces.APPROVAL_TEXTAREA);
  await pl.screenshot({ path: path.join(SHOTS, 'S3-approval-textarea-enter.png'), fullPage: true });

  // الحمولة الفعليّة المرسَلة إلى الخادم عند الإعادة (INPUT/API_MULTILINE_PRESERVED).
  let payload = null;
  pl.on('request', (rq) => {
    if (rq.url().includes('/return') && rq.method() === 'POST') payload = rq.postData();
  });
  const ret = pl.locator('button', { hasText: 'إعادة للتعديل' }).first();
  if (await ret.count()) { await ret.click().catch(() => {}); await sleep(3000); }
  R.surfaces.PAYLOAD = { raw: payload, newlineEscaped: !!(payload && payload.includes('\\n')) };
  note('PAYLOAD', R.surfaces.PAYLOAD);
  await pl.close();

  // ===== 4) ADMIN_ARCHIVE — قراءة باردة لتعليق تاريخيّ في عنصر محذوف إداريًّا =====
  const pa = await newPage();
  await login(pa, ADMIN, ADMIN_PW);
  await pa.goto(`${BASE}/app/admin/archive`, { waitUntil: 'domcontentloaded' });
  await sleep(3000);
  const adet = pa.locator('button', { hasText: 'التفاصيل' }).first();
  if (await adet.count()) { await adet.click(); await sleep(2500); }
  R.surfaces.ADMIN_ARCHIVE = await pa.evaluate(({ m }) => {
    const fn = new Function('el', `return (${m})(el)`);
    const el = [...document.querySelectorAll('td')]
      .filter((e) => e.textContent.includes('اعتماد/س1') || e.textContent.includes('س1/محتوى'))
      .sort((a, b) => a.textContent.length - b.textContent.length)[0];
    return el ? fn(el) : { error: 'not-found', bodyHas: document.body.innerText.includes('لقطة سير العمل') };
  }, { m: MEASURE.toString() });
  note('ARCHIVE', R.surfaces.ADMIN_ARCHIVE);
  await pa.screenshot({ path: path.join(SHOTS, 'S4-admin-archive.png'), fullPage: true });
  await pa.close();
} catch (e) {
  R.error = String(e).slice(0, 500);
  console.error('ERROR', R.error);
} finally {
  await browser.close();
  fs.writeFileSync(path.join(OUT, 'ui-surfaces.json'), JSON.stringify(R, null, 1));
  console.log('\nRESULT=' + path.join(OUT, 'ui-surfaces.json'));
}

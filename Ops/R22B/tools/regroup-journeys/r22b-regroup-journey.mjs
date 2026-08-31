import { chromium } from 'playwright';
import fs from 'node:fs';

const BASE = 'http://localhost:4432';
const PWD = process.env.R22B_PWD;
const EMAIL = 'r22a.e2e.writer@r22uat.test';
const SUB = 'd07d6c4e-f7eb-45d4-a681-1810fdb60cfc';
const SHOTS = '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/Ops/R22B/screenshots-regroup';
fs.mkdirSync(SHOTS, { recursive: true });

const errors = [];
const bad = [];
const out = {};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'ar' });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
page.on('response', (r) => { if (r.status() >= 500) bad.push(`${r.status()} ${r.url()}`); });

// الحزمة المنشورة تنادي https://test.emarketingacademy.net/api مباشرةً (كما بُنِيت فعلًا، بلا تعديل أيّ ملفّ منها).
// نوجّه هذه النداءات إلى المُقدِّم المحلّيّ الذي يمرّرها عبر نفق SSH إلى خلفيّة TEST نفسها.
await ctx.route(/^https:\/\/test\.emarketingacademy\.net\//, async (route) => {
  const req = route.request();
  const u = new URL(req.url());
  const headers = { ...req.headers() };
  delete headers.host;
  const r = await ctx.request.fetch(`${BASE}${u.pathname}${u.search}`, {
    method: req.method(),
    headers,
    data: req.postData() ?? undefined,
    maxRedirects: 0,
  });
  await route.fulfill({ response: r });
});

await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
await page.fill('input[type="email"]', EMAIL);
await page.fill('input[type="password"]', PWD);
await page.click('button[type="submit"]');
await page.waitForURL(/\/app/, { timeout: 30000 });

// فتح المسودّة المجمَّعة
await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle' });
await page.waitForTimeout(3500);

async function measure() {
  return await page.evaluate(() => {
    const t = (el) => (el.textContent || '').trim();
    const heads = [...document.querySelectorAll('*')]
      .filter((e) => e.children.length === 0 && /^بند عمل\s+\d+$/.test(t(e))).map(t);
    const addBtns = [...document.querySelectorAll('button')].filter((b) => t(b).includes('إضافة بند عمل')).length;
    // كل بطاقة مشروع تحوي منتقي مشروع واحدًا
    const projectSelects = [...document.querySelectorAll('select')].filter((s) =>
      [...s.options].some((o) => /مشروع|Project/i.test(o.textContent || ''))).length;
    return { heads, headCount: heads.length, addBtns, projectSelects };
  });
}

out.opened = await measure();
await page.screenshot({ path: `${SHOTS}/G01-regrouped-draft-4-cards.png`, fullPage: true });

// إعادة تحميل
await page.reload({ waitUntil: 'networkidle' });
await page.waitForTimeout(3500);
out.afterReload = await measure();
await page.screenshot({ path: `${SHOTS}/G02-after-reload-GOVERNING.png`, fullPage: true });

// الإرسال
const submitBtn = page.locator('button', { hasText: /^إرسال|إرسال التقرير|إرسال للاعتماد/ }).first();
out.submitButtonVisible = await submitBtn.count() > 0;
if (out.submitButtonVisible) {
  await submitBtn.click();
  await page.waitForTimeout(1500);
  const confirm = page.locator('button', { hasText: /^تأكيد|نعم|إرسال$/ }).last();
  if (await confirm.count() > 0 && await confirm.isVisible()) { await confirm.click(); }
  await page.waitForTimeout(4000);
  await page.screenshot({ path: `${SHOTS}/G03-after-submit.png`, fullPage: true });
  out.pageTextHasSubmitted = await page.evaluate(() =>
    /مُرسَل|مرسل|قيد المراجعة|Submitted|PendingReview/i.test(document.body.innerText));
  out.pageTextHasDuplicateError = await page.evaluate(() =>
    /لا يمكن تكرار نفس المشروع/.test(document.body.innerText));
}

out.consoleErrors = errors;
out.serverErrors5xx = bad;
fs.writeFileSync('/tmp/r22b-regroup-journey.json', JSON.stringify(out, null, 2));
console.log(JSON.stringify(out, null, 2));
await browser.close();

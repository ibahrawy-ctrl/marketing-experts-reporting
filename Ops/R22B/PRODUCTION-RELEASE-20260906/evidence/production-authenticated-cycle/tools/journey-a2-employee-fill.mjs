#!/usr/bin/env node
// R22B-PROD §3-ب — استئناف على المسوّدة القائمة 96613974 (لا إنشاء جديد إطلاقًا).
// يعبّئ الحدّ الأدنى اللازم + نصًّا متعدّد الأسطر على مستويين (رأس التقرير وبند العمل)، ثمّ يحفظ ويعيد التحميل ويرسل.
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const SUB = '96613974-ed0d-41bc-8cae-78c4511a4004';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/journey-shots`;
fs.mkdirSync(SHOT, { recursive: true });

const TAG = 'R22B-PROD-CANARY-20260906';
const ML = `${TAG}\nالسطر الثاني: اختبار حفظ الأسطر المتعدّدة.\nالسطر الثالث: تقرير تحقّق آليّ — يُهمَل تشغيليًّا.`;
const PROJECT = '481934f9-90e9-43fe-8ed2-0e631e95ea50'; // SEO — شركة خبراء التسويق (كيان داخليّ لا عميل خارجيّ)

const log = [];
const say = (k, v) => { const s = `${k} = ${v}`; log.push(s); console.log(s); };
const errors = { console: [], network: [], http5xx: [], http4xx: [] };

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/employee', { headless: true, viewport: { width: 1440, height: 1400 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.console.push(m.text().slice(0, 180)); });
page.on('requestfailed', (r) => errors.network.push(`${r.url().replace(BASE, '')} ${r.failure()?.errorText}`));
page.on('response', (r) => {
  const s = r.status();
  if (s >= 500) errors.http5xx.push(`${s} ${r.url().replace(BASE, '')}`);
  else if (s >= 400 && !/\/notifications|favicon/.test(r.url())) errors.http4xx.push(`${s} ${r.url().replace(BASE, '')}`);
});
const shot = (n) => page.screenshot({ path: `${SHOT}/${n}.png`, fullPage: true });

await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);
say('DRAFT_OPENED', page.url().includes(SUB) ? 'PASS' : 'FAIL');

// ── حارس عدم التكرار: إن وُجدت بطاقة مشروع مسبقًا فلا نضيف أخرى.
const cards = await page.locator('button', { hasText: 'حذف المشروع' }).count();
say('EXISTING_PROJECT_CARDS', cards);
if (cards === 0) {
  await page.locator('button', { hasText: '+ إضافة مشروع' }).first().click();
  await page.waitForTimeout(2500);
}
const cards2 = await page.locator('button', { hasText: 'حذف المشروع' }).count();
if (cards2 !== 1) { say('ABORT', `بطاقات المشروع = ${cards2} ≠ 1`); await ctx.close(); process.exit(3); }

const tas = page.locator('textarea');
const ins = page.locator('input');
const sels = page.locator('select');
const nT = await tas.count(), nI = await ins.count(), nS = await sels.count();
say('FIELD_SHAPE', `textarea=${nT} input=${nI} select=${nS}`);
if (nT !== 5 || nI !== 9 || nS !== 2) { say('ABORT', 'شكل الحقول لا يطابق الخريطة المستكشَفة'); await ctx.close(); process.exit(4); }

// الخريطة المستكشَفة: textarea[3]=ملاحظات للإدارة · textarea[4]=ملاحظات بند العمل
// select[0]=المشروع · select[1]=حالة المقال · input[3]=عنوان المقال · input[4]=الكلمة المفتاحية
// input[6]=تاريخ التسليم(date) · input[2]=آخر KPI  (الترقيم داخل كلّ نوع على حدة)
await tas.nth(3).fill(ML);
await tas.nth(4).fill(ML);
await sels.nth(0).selectOption(PROJECT);
await page.waitForTimeout(800);
await ins.nth(3).fill(`${TAG} — عنوان المقال`);
await ins.nth(4).fill(`${TAG}-keyword`);
await sels.nth(1).selectOption('Draft');
const dateIdx = await ins.evaluateAll((els) => els.findIndex((e) => e.type === 'date'));
await ins.nth(dateIdx).fill('2026-09-10');
say('FIELDS_FILLED', `textareas=2(multiline) project=1 title=1 keyword=1 status=Draft date[${dateIdx}]=2026-09-10`);
await shot('A04-filled');

// ── حفظ المسوّدة
await page.locator('button', { hasText: /^حفظ$/ }).first().click();
await page.waitForTimeout(5000);
const saved = await page.evaluate(() => ({
  indicator: !!document.querySelector('[data-testid="draft-saved-indicator"]'),
  toast: /تم حفظ البيانات بنجاح/.test(document.body.innerText || ''),
}));
say('SAVE_DRAFT', saved.indicator || saved.toast ? `PASS (indicator=${saved.indicator} toast=${saved.toast})` : 'FAIL');
await shot('A05-saved');

// ── إعادة التحميل والتحقّق من الثبات
await page.reload({ waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);
const persisted = await page.evaluate((ml) => {
  const t = [...document.querySelectorAll('textarea')];
  const withTag = t.filter((x) => x.value.includes('R22B-PROD-CANARY-20260906'));
  return {
    textareas: t.length,
    withTag: withTag.length,
    exact: t.filter((x) => x.value === ml).length,
    newlines: withTag.map((x) => (x.value.match(/\n/g) || []).length),
    whiteSpace: withTag[0] ? getComputedStyle(withTag[0]).whiteSpace : null,
    cards: [...document.querySelectorAll('button')].filter((b) => b.textContent.includes('حذف المشروع')).length,
  };
}, ML);
say('DRAFT_RELOAD', persisted.withTag === 2 ? `PASS ${JSON.stringify(persisted)}` : `FAIL ${JSON.stringify(persisted)}`);
say('MULTILINE_AT_INPUT', persisted.newlines.every((n) => n === 2) ? 'PASS (سطران جديدان في كلّ حقل)' : `FAIL ${JSON.stringify(persisted.newlines)}`);
await shot('A06-after-reload');

// ── الإرسال للاعتماد
const btn = page.locator('button', { hasText: 'إرسال للاعتماد' }).first();
const dis = await btn.isDisabled();
say('SUBMIT_BUTTON_ENABLED', dis ? 'NO' : 'YES');
if (!dis) {
  await btn.click();
  await page.waitForTimeout(6000);
  const after = await page.evaluate(() => ({
    toast: /تم إرسال التقرير للاعتماد/.test(document.body.innerText || ''),
    status: /قيد المراجعة|مُرسَل|بانتظار/.test(document.body.innerText || ''),
    body: (document.body.innerText || '').slice(0, 500).replace(/\n+/g, ' | '),
  }));
  say('SUBMIT_REPORT', after.toast || after.status ? `PASS (toast=${after.toast} status=${after.status})` : `CHECK — ${after.body}`);
  await shot('A07-submitted');
} else {
  const miss = await page.evaluate(() => (document.body.innerText.match(/يتعذّر الإرسال[^\n]*/) || [''])[0]);
  say('SUBMIT_REPORT', `BLOCKED — ${miss}`);
}

say('CONSOLE_ERRORS', errors.console.length);
say('NETWORK_ERRORS', errors.network.length);
say('HTTP_5XX', errors.http5xx.length);
say('HTTP_4XX', `${errors.http4xx.length} ${JSON.stringify(errors.http4xx.slice(0, 3))}`);
fs.writeFileSync(`${OUT}/journey-a-employee.txt`, log.join('\n') + '\n');
fs.writeFileSync(`${OUT}/journey-a-errors.json`, JSON.stringify(errors, null, 1));
await ctx.close();

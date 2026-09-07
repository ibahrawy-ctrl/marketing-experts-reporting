#!/usr/bin/env node
// R22B-PROD §5 — إعادة فتح الموظّف: قراءة تعليق الإرجاع متعدّد الأسطر ← تعديل ← حفظ ← إعادة إرسال.
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const SUB = '96613974-ed0d-41bc-8cae-78c4511a4004';
const TAG = 'R22B-PROD-CANARY-20260906';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/journey-shots`;

const log = [];
const say = (k, v) => { const s = `${k} = ${v}`; log.push(s); console.log(s); };
const errors = { console: [], network: [], http5xx: [], http4xx: [] };

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/employee', { headless: true, viewport: { width: 1440, height: 1400 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.console.push(m.text().slice(0, 180)); });
page.on('requestfailed', (r) => errors.network.push(`${r.url().replace(BASE, '')} ${r.failure()?.errorText}`));
page.on('response', (r) => { const s = r.status(); if (s >= 500) errors.http5xx.push(`${s} ${r.url().replace(BASE, '')}`); else if (s >= 400 && !/\/notifications|favicon/.test(r.url())) errors.http4xx.push(`${s} ${r.url().replace(BASE, '')}`); });
const shot = (n) => page.screenshot({ path: `${SHOT}/${n}.png`, fullPage: true });

await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4500);

// ── 1) تعليق الإرجاع كما يراه الموظّف
const cmt = await page.evaluate(() => {
  const needle = 'السطر الثاني: الرجاء تعديل حالة المقال.';
  const out = [];
  for (const el of document.querySelectorAll('div,p,span,pre,td,li')) {
    if (el.children.length > 0) continue;
    const t = el.textContent || '';
    if (!t.includes(needle)) continue;
    const cs = getComputedStyle(el);
    const lh = parseFloat(cs.lineHeight) || 0;
    const h = Math.round(el.getBoundingClientRect().height);
    out.push({ tag: el.tagName, newlines: (t.match(/\n/g) || []).length, whiteSpace: cs.whiteSpace, h, lineHeight: lh, visualLines: lh ? Math.round(h / lh) : null, clipped: el.scrollHeight - el.clientHeight > 2 });
  }
  const b = document.body.innerText || '';
  return { nodes: out, l1: b.includes('إعادة للتعديل (تحقّق آليّ).'), l2: b.includes(needle), l3: b.includes('السطر الثالث: هذا تعليق تحقّق — يُهمَل تشغيليًّا.'),
    status: /مُعاد للتعديل/.test(b) };
});
const ok = cmt.nodes.some((n) => n.newlines === 2 && n.whiteSpace === 'pre-wrap' && !n.clipped && n.visualLines >= 3);
say('RETURN_COMMENT_MULTILINE_AT_EMPLOYEE', ok && cmt.l1 && cmt.l2 && cmt.l3 ? `PASS ${JSON.stringify(cmt.nodes[0])}` : `FAIL ${JSON.stringify(cmt)}`);
say('STATUS_SHOWS_RETURNED', cmt.status ? 'PASS' : 'FAIL');
await shot('C01-returned-comment');

// ── 2) التعديل المطلوب: حالة المقال Draft ← Published
const sels = page.locator('select');
const nS = await sels.count();
const statusIdx = await sels.evaluateAll((els) => els.findIndex((s) => [...s.options].some((o) => o.value === 'Published')));
if (statusIdx < 0) { say('ABORT', `لم يُعثر على منتقي حالة المقال (selects=${nS})`); await ctx.close(); process.exit(5); }
await sels.nth(statusIdx).selectOption('Published');
await page.waitForTimeout(600);
say('EDIT_APPLIED', `حالة المقال: Draft ← Published (select[${statusIdx}])`);

await page.locator('button', { hasText: /^حفظ$/ }).first().click();
await page.waitForTimeout(5000);
const saved = await page.evaluate(() => ({ indicator: !!document.querySelector('[data-testid="draft-saved-indicator"]'), toast: /تم حفظ البيانات بنجاح/.test(document.body.innerText || '') }));
say('SAVE_AFTER_RETURN', saved.indicator || saved.toast ? `PASS (indicator=${saved.indicator} toast=${saved.toast})` : 'FAIL');
await shot('C02-edited-saved');

// ── 3) إعادة الإرسال
await page.reload({ waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);
const kept = await page.evaluate(() => {
  const t = [...document.querySelectorAll('textarea')].filter((x) => x.value.includes('R22B-PROD-CANARY-20260906'));
  const s = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.value === 'Published'));
  return { taggedTextareas: t.length, newlines: t.map((x) => (x.value.match(/\n/g) || []).length), articleStatus: s?.value ?? null };
});
say('EDIT_PERSISTED', kept.articleStatus === 'Published' && kept.taggedTextareas === 2 && kept.newlines.every((n) => n === 2) ? `PASS ${JSON.stringify(kept)}` : `FAIL ${JSON.stringify(kept)}`);

const btn = page.locator('button', { hasText: 'إرسال للاعتماد' }).first();
say('RESUBMIT_BUTTON_ENABLED', (await btn.isDisabled()) ? 'NO' : 'YES');
await btn.click();
await page.waitForTimeout(6000);
const after = await page.evaluate(() => ({ status: /مُرسَل|بانتظار الاعتماد|تم إرسال تقريرك/.test(document.body.innerText || ''), body: (document.body.innerText || '').slice(0, 400).replace(/\n+/g, ' | ') }));
say('RESUBMIT_REPORT', after.status ? 'PASS' : `CHECK — ${after.body}`);
await shot('C03-resubmitted');

say('CONSOLE_ERRORS', errors.console.length);
say('NETWORK_ERRORS', errors.network.length);
say('HTTP_5XX', errors.http5xx.length);
say('HTTP_4XX', `${errors.http4xx.length} ${JSON.stringify(errors.http4xx.slice(0, 3))}`);
fs.writeFileSync(`${OUT}/journey-c-employee-resubmit.txt`, log.join('\n') + '\n');
await ctx.close();

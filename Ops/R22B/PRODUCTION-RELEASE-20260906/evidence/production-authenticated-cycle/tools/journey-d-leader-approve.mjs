#!/usr/bin/env node
// R22B-PROD §6 — اعتماد قائدة الفريق + قياس الأسطر المتعدّدة بعد الاعتماد.
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

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/leader', { headless: true, viewport: { width: 1440, height: 1400 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.console.push(m.text().slice(0, 180)); });
page.on('requestfailed', (r) => errors.network.push(`${r.url().replace(BASE, '')} ${r.failure()?.errorText}`));
page.on('response', (r) => { const s = r.status(); if (s >= 500) errors.http5xx.push(`${s} ${r.url().replace(BASE, '')}`); else if (s >= 400 && !/\/notifications|favicon/.test(r.url())) errors.http4xx.push(`${s} ${r.url().replace(BASE, '')}`); });
const shot = (n) => page.screenshot({ path: `${SHOT}/${n}.png`, fullPage: true });

const measure = async (label) => {
  const m = await page.evaluate((tag) => {
    const out = [];
    for (const el of document.querySelectorAll('div,p,span,pre,td,li')) {
      if (el.children.length > 0) continue;
      const t = el.textContent || '';
      if (!t.includes(tag)) continue;
      const cs = getComputedStyle(el);
      const lh = parseFloat(cs.lineHeight) || 0;
      const h = Math.round(el.getBoundingClientRect().height);
      out.push({ tag: el.tagName, newlines: (t.match(/\n/g) || []).length, whiteSpace: cs.whiteSpace, h, lineHeight: lh, visualLines: lh ? Math.round(h / lh) : null, clipped: el.scrollHeight - el.clientHeight > 2 });
    }
    const b = document.body.innerText || '';
    return { nodes: out, l2: b.includes('السطر الثاني: اختبار حفظ الأسطر المتعدّدة.'), l3: b.includes('السطر الثالث: تقرير تحقّق آليّ — يُهمَل تشغيليًّا.'),
      returnL2: b.includes('السطر الثاني: الرجاء تعديل حالة المقال.') };
  }, TAG);
  const good = m.nodes.filter((n) => n.newlines === 2 && n.whiteSpace === 'pre-wrap' && !n.clipped && n.visualLines >= 3);
  say(label, good.length >= 1 && m.l2 && m.l3 ? `PASS (سليمة=${good.length}/${m.nodes.length} · ${JSON.stringify(good[0])} · تعليق الإرجاع محفوظ=${m.returnL2})` : `FAIL ${JSON.stringify(m)}`);
  return m;
};

await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4500);
const pre = await page.evaluate(() => ({ resubmitted: /مُرسَل|بانتظار/.test(document.body.innerText || ''), articleStatus: (document.body.innerText.match(/حالة المقال\s*\n\s*(\w+)/) || [])[1] ?? null }));
say('LEADER_SEES_RESUBMITTED', pre.resubmitted ? `PASS (حالة المقال=${pre.articleStatus})` : 'FAIL');

// ── الاعتماد (بلا سبب — النظام لا يشترطه للاعتماد)
const btn = page.locator('button', { hasText: /^اعتماد$/ }).first();
say('APPROVE_BUTTON_ENABLED', (await btn.isDisabled()) ? 'FAIL' : 'PASS');
await btn.click();
await page.waitForTimeout(7000);
const after = await page.evaluate(() => ({ body: (document.body.innerText || '').slice(0, 500).replace(/\n+/g, ' | '),
  approved: /معتمد من المدير المباشر|مغلق|تم الاعتماد/.test(document.body.innerText || '') }));
say('APPROVE_ACTION', after.approved ? 'PASS' : `CHECK — ${after.body}`);
await shot('D01-approved');

await page.reload({ waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);
await measure('MULTILINE_AFTER_APPROVAL');
await shot('D02-after-approval-multiline');

// ── اختفاؤه من طابور «بانتظار اعتمادي»
await page.goto(`${BASE}/app/submissions`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4500);
const q = await page.evaluate(() => {
  const rows = [...document.querySelectorAll('tr')].filter((r) => /جهاد صلاح/.test(r.innerText) && /2026-W37/.test(r.innerText));
  return { rows: rows.length, text: rows[0] ? rows[0].innerText.replace(/\s+/g, ' ').slice(0, 200) : null };
});
say('QUEUE_AFTER_APPROVAL', `صفوف W37 لجهاد = ${q.rows} · ${q.text}`);
await shot('D03-queue-after-approval');

say('CONSOLE_ERRORS', errors.console.length);
say('NETWORK_ERRORS', errors.network.length);
say('HTTP_5XX', errors.http5xx.length);
say('HTTP_4XX', `${errors.http4xx.length} ${JSON.stringify(errors.http4xx.slice(0, 3))}`);
fs.writeFileSync(`${OUT}/journey-d-leader-approve.txt`, log.join('\n') + '\n');
await ctx.close();

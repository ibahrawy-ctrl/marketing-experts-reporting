#!/usr/bin/env node
// R22B-PROD §4 — رحلة قائدة الفريق: الطابور ← قياس الأسطر المتعدّدة عند المراجعة ← تعليق بضغطات Enter حقيقيّة ← إعادة للتعديل.
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const SUB = '96613974-ed0d-41bc-8cae-78c4511a4004';
const TAG = 'R22B-PROD-CANARY-20260906';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/journey-shots`;
fs.mkdirSync(SHOT, { recursive: true });

const log = [];
const say = (k, v) => { const s = `${k} = ${v}`; log.push(s); console.log(s); };
const errors = { console: [], network: [], http5xx: [], http4xx: [] };

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/leader', { headless: true, viewport: { width: 1440, height: 1400 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.console.push(m.text().slice(0, 180)); });
page.on('requestfailed', (r) => errors.network.push(`${r.url().replace(BASE, '')} ${r.failure()?.errorText}`));
page.on('response', (r) => { const s = r.status(); if (s >= 500) errors.http5xx.push(`${s} ${r.url().replace(BASE, '')}`); else if (s >= 400 && !/\/notifications|favicon/.test(r.url())) errors.http4xx.push(`${s} ${r.url().replace(BASE, '')}`); });
const shot = (n) => page.screenshot({ path: `${SHOT}/${n}.png`, fullPage: true });

// ── 1) الطابور
await page.goto(`${BASE}/app/submissions`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4500);
const q = await page.evaluate((tag) => {
  const rows = [...document.querySelectorAll('tr')].filter((r) => /جهاد صلاح/.test(r.innerText) && /2026-W37/.test(r.innerText));
  const txt = document.body.innerText || '';
  return { canaryRows: rows.length, rowText: rows[0] ? rows[0].innerText.replace(/\s+/g, ' ').slice(0, 200) : null,
    pendingCounter: (txt.match(/(\d+)\s*\n?\s*بانتظار اعتمادي/) || [])[1] ?? null };
}, TAG);
say('LEADER_QUEUE_CONTAINS_CANARY', q.canaryRows === 1 ? `PASS (صفّ واحد فقط) — ${q.rowText}` : `FAIL (${q.canaryRows})`);
await shot('B01-leader-queue');

// ── 2) سطح المراجعة + قياس الأسطر المتعدّدة
await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);
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
      out.push({ tag: el.tagName, newlines: (t.match(/\n/g) || []).length, whiteSpace: cs.whiteSpace,
        h, lineHeight: lh, visualLines: lh ? Math.round(h / lh) : null, clipped: el.scrollHeight - el.clientHeight > 2 });
    }
    const body = document.body.innerText || '';
    return { nodes: out, bodyOccurrences: (body.match(new RegExp(tag, 'g')) || []).length,
      bodyHasLine2: body.includes('السطر الثاني: اختبار حفظ الأسطر المتعدّدة.'),
      bodyHasLine3: body.includes('السطر الثالث: تقرير تحقّق آليّ — يُهمَل تشغيليًّا.') };
  }, TAG);
  const good = m.nodes.filter((n) => n.newlines === 2 && n.whiteSpace === 'pre-wrap' && !n.clipped && n.visualLines >= 3);
  say(label, good.length >= 1 && m.bodyHasLine2 && m.bodyHasLine3
    ? `PASS (عُقَد سليمة=${good.length}/${m.nodes.length} · ${JSON.stringify(good[0])})`
    : `FAIL ${JSON.stringify(m)}`);
  return m;
};
await measure('MULTILINE_AT_REVIEW');
await shot('B02-review-multiline');

// ── 3) تعليق بضغطات Enter حقيقيّة
const reason = page.locator('textarea[placeholder="اكتب سبب القرار…"]').first();
await reason.click();
await reason.type(`${TAG} — إعادة للتعديل (تحقّق آليّ).`);
await page.keyboard.press('Enter');
await reason.type('السطر الثاني: الرجاء تعديل حالة المقال.');
await page.keyboard.press('Enter');
await reason.type('السطر الثالث: هذا تعليق تحقّق — يُهمَل تشغيليًّا.');
await page.waitForTimeout(800);
const typed = await reason.evaluate((e) => ({ newlines: (e.value.match(/\n/g) || []).length, len: e.value.length }));
say('LEADER_COMMENT_TYPED_WITH_ENTER', typed.newlines === 2 ? `PASS (${JSON.stringify(typed)})` : `FAIL ${JSON.stringify(typed)}`);

const retBtn = page.locator('button', { hasText: 'إعادة للتعديل' }).first();
say('RETURN_BUTTON_ENABLED_AFTER_REASON', (await retBtn.isDisabled()) ? 'FAIL' : 'PASS');
await shot('B03-reason-typed');

// ── 4) الإعادة للتعديل
await retBtn.click();
await page.waitForTimeout(6000);
const after = await page.evaluate(() => ({ body: (document.body.innerText || '').slice(0, 600).replace(/\n+/g, ' | '),
  returned: /مُعاد للتعديل|أُعيد للتعديل|إعادة للتعديل بنجاح/.test(document.body.innerText || '') }));
say('RETURN_ACTION', after.returned ? 'PASS' : `CHECK — ${after.body}`);
await shot('B04-returned');

say('CONSOLE_ERRORS', errors.console.length);
say('NETWORK_ERRORS', errors.network.length);
say('HTTP_5XX', errors.http5xx.length);
say('HTTP_4XX', `${errors.http4xx.length} ${JSON.stringify(errors.http4xx.slice(0, 3))}`);
fs.writeFileSync(`${OUT}/journey-b-leader-return.txt`, log.join('\n') + '\n');
await ctx.close();

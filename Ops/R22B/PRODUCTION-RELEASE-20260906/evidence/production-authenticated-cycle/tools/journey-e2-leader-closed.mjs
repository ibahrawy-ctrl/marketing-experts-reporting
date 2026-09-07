#!/usr/bin/env node
// R22B-PROD §7-ب — رؤية القائدة للتقرير المغلق عبر ضبط المرشّحات صراحةً (قراءة فقط).
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';
const BASE = 'https://reports.emarketingacademy.net';
const TAG = 'R22B-PROD-CANARY-20260906';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/journey-shots`;
const log = [];
const say = (k, v) => { const s = `${k} = ${v}`; log.push(s); console.log(s); };

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/leader', { headless: true, viewport: { width: 1440, height: 1400 } });
await ctx.route('**/*', (r) => (['POST', 'PUT', 'PATCH', 'DELETE'].includes(r.request().method()) && !/\/auth\/refresh|\/hubs\//.test(r.request().url()) ? r.abort() : r.continue()));
const page = await ctx.newPage();
await page.goto(`${BASE}/app/submissions`, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(4000);

// اضبط الفترة على 2026-W37 حيثما وُجد منتقٍ يحويها
const sels = page.locator('select');
for (let i = 0; i < await sels.count(); i++) {
  const has = await sels.nth(i).locator('option[value="2026-W37"]').count();
  if (has) { await sels.nth(i).selectOption('2026-W37'); await page.waitForTimeout(2500); say('PERIOD_FILTER_SET', `select[${i}] = 2026-W37`); break; }
}
// اضغط شريحة «المغلقة»
const chip = page.locator('button', { hasText: /^المغلقة$/ }).first();
if (await chip.count()) { await chip.click(); await page.waitForTimeout(3000); say('QUICK_FILTER', 'المغلقة'); }

const r = await page.evaluate(() => {
  const rows = [...document.querySelectorAll('tr')].filter((x) => /جهاد صلاح/.test(x.innerText) && /2026-W37/.test(x.innerText));
  const txt = document.body.innerText || '';
  return { rows: rows.length, first: rows[0]?.innerText.replace(/\s+/g, ' ').slice(0, 220) ?? null,
    counters: (txt.match(/\d+\s*\n\s*(إجمالي التقارير|بانتظار اعتمادي|معادة للتعديل|مغلقة)/g) || []).map((s) => s.replace(/\s+/g, ' ')) };
});
say('LEADER_SEES_CLOSED_IN_SCOPE_LIST', r.rows === 1 ? `PASS — ${r.first}` : `FAIL (${r.rows}) · ${JSON.stringify(r.counters)}`);
await page.screenshot({ path: `${SHOT}/E03-leader-closed-list.png`, fullPage: true });

if (r.rows === 1) {
  await page.locator('tr', { hasText: '2026-W37' }).first().locator('button,a').last().click();
  await page.waitForTimeout(4500);
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
      retL2: b.includes('السطر الثاني: الرجاء تعديل حالة المقال.'), closed: /مغلق/.test(b) };
  }, TAG);
  const good = m.nodes.filter((n) => n.newlines === 2 && n.whiteSpace === 'pre-wrap' && !n.clipped && n.visualLines >= 3);
  say('MULTILINE_AFTER_APPROVAL_LEADER', good.length >= 1 && m.l2 && m.l3 ? `PASS (سليمة=${good.length}/${m.nodes.length} · ${JSON.stringify(good[0])} · تعليق الإرجاع=${m.retL2} · مغلق=${m.closed})` : `FAIL ${JSON.stringify(m)}`);
  await page.screenshot({ path: `${SHOT}/E04-leader-closed-detail.png`, fullPage: true });
}
fs.writeFileSync(`${OUT}/journey-e2-leader-closed.txt`, log.join('\n') + '\n');
await ctx.close();

#!/usr/bin/env node
// R22B-PROD §7 — تحقّق ما بعد الاعتماد (قراءة فقط، كلّ كتابة مُلغاة): الأسطر المتعدّدة + الرؤية في التقارير والأرشيف للدورين.
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

const measureIn = async (page, label) => {
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
      retL2: b.includes('السطر الثاني: الرجاء تعديل حالة المقال.'), retL3: b.includes('السطر الثالث: هذا تعليق تحقّق — يُهمَل تشغيليًّا.'),
      closed: /مغلق/.test(b), articlePublished: /Published/.test(b) };
  }, TAG);
  const good = m.nodes.filter((n) => n.newlines === 2 && n.whiteSpace === 'pre-wrap' && !n.clipped && n.visualLines >= 3);
  say(label, good.length >= 1 && m.l2 && m.l3
    ? `PASS (سليمة=${good.length}/${m.nodes.length} · ${JSON.stringify(good[0])} · تعليق الإرجاع L2=${m.retL2} L3=${m.retL3} · مغلق=${m.closed} · Published=${m.articlePublished})`
    : `FAIL ${JSON.stringify(m)}`);
  return m;
};

const open = async (profile) => {
  const ctx = await chromium.launchPersistentContext(`/private/tmp/prod-auth/${profile}`, { headless: true, viewport: { width: 1440, height: 1400 } });
  await ctx.route('**/*', (r) => (['POST', 'PUT', 'PATCH', 'DELETE'].includes(r.request().method()) && !/\/auth\/refresh|\/hubs\//.test(r.request().url()) ? r.abort() : r.continue()));
  const page = await ctx.newPage();
  page.on('console', (m) => { if (m.type() === 'error') errors.console.push(`${profile}: ${m.text().slice(0, 160)}`); });
  page.on('requestfailed', (r) => errors.network.push(`${profile}: ${r.url().replace(BASE, '')} ${r.failure()?.errorText}`));
  page.on('response', (r) => { const s = r.status(); if (s >= 500) errors.http5xx.push(`${profile}: ${s} ${r.url().replace(BASE, '')}`); else if (s >= 400 && !/\/notifications|favicon/.test(r.url())) errors.http4xx.push(`${profile}: ${s} ${r.url().replace(BASE, '')}`); });
  return { ctx, page };
};

// ── (1) الموظّف: تقاريري ← فتح التقرير المغلق
{
  const { ctx, page } = await open('employee');
  await page.goto(`${BASE}/app/my-reports`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(4500);
  const list = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('tr')].filter((r) => /2026-W37/.test(r.innerText));
    return { rows: rows.length, text: rows[0] ? rows[0].innerText.replace(/\s+/g, ' ').slice(0, 220) : null };
  });
  say('EMPLOYEE_SEES_CLOSED_IN_MY_REPORTS', list.rows === 1 ? `PASS — ${list.text}` : `FAIL (${list.rows})`);
  await page.screenshot({ path: `${SHOT}/E01-employee-my-reports.png`, fullPage: true });

  await page.goto(`${BASE}/app/submissions?open=${SUB}`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(4500);
  await measureIn(page, 'MULTILINE_AFTER_APPROVAL_EMPLOYEE');
  await page.screenshot({ path: `${SHOT}/E02-employee-closed-detail.png`, fullPage: true });
  await ctx.close();
}

// ── (2) قائدة الفريق: كل التقارير + التصفية «مغلقة»
{
  const { ctx, page } = await open('leader');
  await page.goto(`${BASE}/app/submissions`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(4500);
  const closedTab = page.locator('button', { hasText: /^مغلقة$/ }).first();
  if (await closedTab.count()) { await closedTab.click(); await page.waitForTimeout(3500); }
  const list = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('tr')].filter((r) => /جهاد صلاح/.test(r.innerText) && /2026-W37/.test(r.innerText));
    return { rows: rows.length, text: rows[0] ? rows[0].innerText.replace(/\s+/g, ' ').slice(0, 220) : null };
  });
  say('LEADER_SEES_CLOSED', list.rows === 1 ? `PASS — ${list.text}` : `CHECK (${list.rows})`);
  await page.screenshot({ path: `${SHOT}/E03-leader-closed-list.png`, fullPage: true });

  if (list.rows === 1) {
    await page.locator('tr', { hasText: '2026-W37' }).first().locator('button, a').last().click().catch(() => {});
    await page.waitForTimeout(4500);
    await measureIn(page, 'MULTILINE_AFTER_APPROVAL_LEADER');
    await page.screenshot({ path: `${SHOT}/E04-leader-closed-detail.png`, fullPage: true });
  }
  await ctx.close();
}

say('CONSOLE_ERRORS', `${errors.console.length} ${JSON.stringify(errors.console.slice(0, 2))}`);
say('NETWORK_ERRORS', errors.network.length);
say('HTTP_5XX', errors.http5xx.length);
say('HTTP_4XX', `${errors.http4xx.length} ${JSON.stringify(errors.http4xx.slice(0, 3))}`);
fs.writeFileSync(`${OUT}/journey-e-post-approval.txt`, log.join('\n') + '\n');

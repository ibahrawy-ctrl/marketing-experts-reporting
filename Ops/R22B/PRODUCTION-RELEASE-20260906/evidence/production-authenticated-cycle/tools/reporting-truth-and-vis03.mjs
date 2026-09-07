#!/usr/bin/env node
// R22B-PROD §9 — حقيقة التقارير: تبويب «التقارير المرتبطة» في Project 360 + انسياح موحّد الصيغة مع البوّابة السابقة.
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';
const BASE = 'https://reports.emarketingacademy.net';
const PROJECT = '481934f9-90e9-43fe-8ed2-0e631e95ea50';
const SUB = '96613974-ed0d-41bc-8cae-78c4511a4004';
const TAG = 'R22B-PROD-CANARY-20260906';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/vis-shots`;
const log = [];
const say = (k, v) => { const s = `${k} = ${v}`; log.push(s); console.log(s); };

const SURFACES = [
  ['my-reports', '/app/my-reports'],
  ['scope-list', '/app/submissions'],
  ['canary-detail', `/app/submissions?open=${SUB}`],
  ['project360', `/app/projects/${PROJECT}/360`],
  ['calendar', '/app/report-calendar'],
];

const docOverflow = (page) => page.evaluate(() => {
  const de = document.documentElement;
  const scrollers = [...document.querySelectorAll('*')].filter((e) => /auto|scroll/.test(getComputedStyle(e).overflowX));
  let strayOutside = 0;
  for (const el of document.querySelectorAll('*')) {
    const r = el.getBoundingClientRect();
    if (r.width === 0) continue;
    if (r.right > de.clientWidth + 2 || r.left < -2) { if (!scrollers.some((s) => s !== el && s.contains(el))) strayOutside++; }
  }
  return { dir: de.getAttribute('dir') || getComputedStyle(document.body).direction,
    docOverflow: Math.max(0, de.scrollWidth - de.clientWidth), strayOutsideScrollers: strayOutside,
    scrollContainers: scrollers.length,
    spinners: document.querySelectorAll('[class*="animate-spin"],[role="progressbar"],[aria-busy="true"]').length };
});

const grid = {};
for (const profile of ['employee', 'leader']) {
  const ctx = await chromium.launchPersistentContext(`/private/tmp/prod-auth/${profile}`, { headless: true });
  await ctx.route('**/*', (r) => (['POST', 'PUT', 'PATCH', 'DELETE'].includes(r.request().method()) && !/\/auth\/refresh|\/hubs\//.test(r.request().url()) ? r.abort() : r.continue()));

  for (const [w, h] of [[1440, 1000], [390, 844]]) {
    const page = await ctx.newPage();
    await page.setViewportSize({ width: w, height: h });
    for (const [name, path] of SURFACES) {
      await page.goto(BASE + path, { waitUntil: 'networkidle', timeout: 60000 });
      await page.waitForTimeout(3000);
      const m = await docOverflow(page);
      grid[`${profile}/${w}/${name}`] = m;
      say(`VIS03 ${profile}/${w}/${name}`, `dir=${m.dir} docOvf=${m.docOverflow} strayOutsideScrollers=${m.strayOutsideScrollers} scrollers=${m.scrollContainers} spin=${m.spinners}`);
    }
    await page.close();
  }

  // تبويب «التقارير المرتبطة»
  const p = await ctx.newPage();
  await p.setViewportSize({ width: 1440, height: 1400 });
  await p.goto(`${BASE}/app/projects/${PROJECT}/360`, { waitUntil: 'networkidle', timeout: 60000 });
  await p.waitForTimeout(4000);
  await p.locator('button', { hasText: /^التقارير المرتبطة$/ }).first().click();
  await p.waitForTimeout(5000);
  const linked = await p.evaluate((tag) => {
    const b = document.body.innerText || '';
    return { hasTag: b.includes(tag), hasW37: b.includes('2026-W37'), hasEmployee: b.includes('جهاد صلاح'),
      occurrences: (b.match(new RegExp(tag, 'g')) || []).length, sample: b.slice(400, 1600).replace(/\n+/g, ' | ') };
  }, TAG);
  say(`REPORTING_TRUTH_P360_LINKED ${profile}`, linked.hasTag || (linked.hasW37 && linked.hasEmployee)
    ? `PASS (وسم=${linked.hasTag}×${linked.occurrences} · W37=${linked.hasW37} · الموظّف=${linked.hasEmployee})`
    : `FAIL (W37=${linked.hasW37} موظّف=${linked.hasEmployee}) · ${linked.sample.slice(0, 500)}`);
  await p.screenshot({ path: `${SHOT}/${profile}-p360-linked.png`, fullPage: true });
  await p.close();
  await ctx.close();
}

const bad = Object.entries(grid).filter(([, m]) => m.dir !== 'rtl' || m.docOverflow > 0 || m.strayOutsideScrollers > 0 || m.spinners > 0);
say('VIS03_VIS04_SUMMARY', bad.length === 0 ? `PASS ${Object.keys(grid).length}/${Object.keys(grid).length}` : `FAIL ${JSON.stringify(bad)}`);
fs.writeFileSync(`${OUT}/reporting-truth-and-vis03.txt`, log.join('\n') + '\n');
fs.writeFileSync(`${OUT}/reporting-truth-and-vis03.json`, JSON.stringify(grid, null, 1));

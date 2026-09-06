#!/usr/bin/env node
// R22B-REL §10 — بوّابة بصريّة مجهولة على الإنتاج الحقيقيّ (بلا نفق، بلا كتابة، بلا اعتماد).
// تقيس: تحميل SPA، اتّجاه RTL، الانسياح الأفقيّ، أخطاء الكونسول، أخطاء الشبكة، و5xx.
import { chromium, webkit } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const OUT = '/private/tmp/prod-verify';
const SIZES = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile390', width: 390, height: 844 },
];
const results = [];

for (const [engName, eng] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await eng.launch();
  for (const size of SIZES) {
    const ctx = await browser.newContext({ viewport: { width: size.width, height: size.height } });
    const page = await ctx.newPage();
    const consoleErrors = [];
    const netErrors = [];
    const statuses = [];
    page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 200)); });
    page.on('requestfailed', (r) => netErrors.push(`${r.url()} ${r.failure()?.errorText}`));
    page.on('response', (r) => statuses.push({ url: r.url(), status: r.status() }));

    await page.goto(BASE + '/', { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(1500);

    const dir = await page.evaluate(() => document.documentElement.getAttribute('dir') || getComputedStyle(document.body).direction);
    const lang = await page.evaluate(() => document.documentElement.getAttribute('lang'));
    const overflow = await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth));
    const rootMounted = await page.evaluate(() => (document.getElementById('root')?.children.length ?? 0) > 0);
    const hasArabic = await page.evaluate(() => /[\u0600-\u06FF]/.test(document.body.innerText));
    const bodyText = (await page.evaluate(() => document.body.innerText)).slice(0, 300).replace(/\s+/g, ' ');
    const fivexx = statuses.filter((s) => s.status >= 500);
    const notFound = statuses.filter((s) => s.status === 404);

    await page.screenshot({ path: `${OUT}/PROD-${engName}-${size.name}-login.png`, fullPage: true });

    results.push({
      engine: engName, size: size.name, dir, lang, rootMounted, hasArabic,
      horizontalOverflow: overflow, consoleErrors: consoleErrors.length, consoleErrorSample: consoleErrors.slice(0, 3),
      networkErrors: netErrors.length, networkErrorSample: netErrors.slice(0, 3),
      http5xx: fivexx.length, http404: notFound.length, http404Sample: notFound.slice(0, 3).map((s) => s.url),
      bodySample: bodyText,
    });
    await ctx.close();
  }
  await browser.close();
}

fs.writeFileSync(`${OUT}/prod-visual-anon.json`, JSON.stringify(results, null, 1));
let fail = 0;
for (const r of results) {
  const ok = r.rootMounted && r.dir === 'rtl' && r.horizontalOverflow === 0 && r.consoleErrors === 0 && r.networkErrors === 0 && r.http5xx === 0 && r.hasArabic;
  if (!ok) fail++;
  console.log(`[${ok ? 'PASS' : 'FAIL'}] ${r.engine}/${r.size} dir=${r.dir} mounted=${r.rootMounted} ar=${r.hasArabic} overflow=${r.horizontalOverflow} console=${r.consoleErrors} net=${r.networkErrors} 5xx=${r.http5xx} 404=${r.http404}`);
}
console.log(fail === 0 ? 'PROD_VISUAL_ANON_GATE=PASS' : `PROD_VISUAL_ANON_GATE=FAIL (${fail})`);
process.exit(fail === 0 ? 0 : 1);

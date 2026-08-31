import { chromium } from 'playwright';
import fs from 'node:fs';

const BASE = 'http://localhost:4432';
const PWD = process.env.R22B_PWD;
const EMAIL = 'r22a.e2e.writer@r22uat.test';
const SUB = 'd07d6c4e-f7eb-45d4-a681-1810fdb60cfc';
const SHOTS = '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/Ops/R22B/screenshots-regroup';
const PROJECTS = [
  ['P1', '44388186-3e2a-4825-8b92-a8307abdc941', 3],
  ['P2', '1d3ae941-05a2-43c9-a37d-8e32fbac5796', 1],
  ['P3', '9e731196-f87f-4bbf-82cd-260d06d90b56', 2],
  ['P4', '51710a1a-386d-4cce-a8ac-0665aaee9d9b', 5],
];

const errors = [], bad = [], out = {};
const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 1100 }, locale: 'ar' });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
page.on('response', (r) => { if (r.status() >= 500) bad.push(`${r.status()} ${r.url()}`); });

await ctx.route(/^https:\/\/test\.emarketingacademy\.net\//, async (route) => {
  const req = route.request();
  const u = new URL(req.url());
  const headers = { ...req.headers() }; delete headers.host;
  const r = await ctx.request.fetch(`${BASE}${u.pathname}${u.search}`,
    { method: req.method(), headers, data: req.postData() ?? undefined, maxRedirects: 0 });
  await route.fulfill({ response: r });
});

await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
await page.fill('input[type="email"]', EMAIL);
await page.fill('input[type="password"]', PWD);
await page.click('button[type="submit"]');
await page.waitForURL(/\/app/, { timeout: 30000 });

out.slices = {};
for (const [code, pid, expected] of PROJECTS) {
  await page.goto(`${BASE}/app/projects/${pid}/reports/${SUB}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  const n = await page.evaluate(() => [...document.querySelectorAll('*')]
    .filter((e) => e.children.length === 0 && /^بند عمل\s+\d+$/.test((e.textContent || '').trim())).length);
  out.slices[code] = { projectId: pid, expected, rendered: n, ok: n === expected };
  await page.screenshot({ path: `${SHOTS}/G04-p360-slice-${code}-${expected}-items.png`, fullPage: true });
}

out.consoleErrors = errors;
out.serverErrors5xx = bad;
fs.writeFileSync('/tmp/r22b-regroup-p360.json', JSON.stringify(out, null, 2));
console.log(JSON.stringify(out, null, 2));
await browser.close();

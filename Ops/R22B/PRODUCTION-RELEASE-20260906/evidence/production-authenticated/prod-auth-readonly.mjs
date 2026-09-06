#!/usr/bin/env node
// R22B-PROD §2/§5/§6/§7 — تحقّق مصادَق عليه على الإنتاج الحقيقيّ، قراءة فقط تمامًا.
// ممنوع في هذا السكربت: أيّ POST/PUT/PATCH/DELETE، وأيّ طباعة لتوكن أو كوكي أو كلمة مرور.
// يستعمل ملفّ جلسة المتصفّح الذي أنشأه المالك بنفسه في /private/tmp (خارج Git).
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const PROFILE = process.argv[2] ?? 'ceo';
const OUT = '/private/tmp/prod-verify';
const SHOT = `${OUT}/screenshots-auth`;
fs.mkdirSync(SHOT, { recursive: true });

const MULTILINE_SUBMISSION = 'de3e9c56-6a01-401f-97d6-a90dafb87708'; // تسليم إنتاج قائم (51 سطرًا) — لا يُعدَّل
const PAGES = [
  { id: 'P01-home', path: '/app' },
  { id: 'P02-submissions', path: '/app/submissions' },
  { id: 'P03-my-reports', path: '/app/my-reports' },
  { id: 'P04-submission-multiline', path: `/app/submissions?open=${MULTILINE_SUBMISSION}` },
  { id: 'P05-admin-archive', path: '/app/admin/archive' },
  { id: 'P06-projects', path: '/app/projects' },
  { id: 'P07-report-templates', path: '/app/report-templates' },
  { id: 'P08-report-calendar', path: '/app/report-calendar' },
  { id: 'P09-notfound', path: '/app/projects/00000000-0000-0000-0000-000000000000/360' },
];
const SIZES = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile390', width: 390, height: 844 },
];

const ctx = await chromium.launchPersistentContext(`/private/tmp/prod-auth/${PROFILE}`, {
  headless: true,
  viewport: SIZES[0],
});

// حارس صارم: أيّ طلب غير آمن يُلغى ويُسجَّل كخرق.
const unsafeAttempts = [];
await ctx.route('**/*', (route) => {
  const m = route.request().method();
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(m)) {
    const u = route.request().url();
    if (!/\/auth\/refresh|\/hubs\//.test(u)) { unsafeAttempts.push(`${m} ${u}`); return route.abort(); }
  }
  return route.continue();
});

const results = [];
for (const size of SIZES) {
  const page = await ctx.newPage();
  await page.setViewportSize(size);
  for (const p of PAGES) {
    const consoleErrors = [];
    const netErrors = [];
    const statuses = [];
    const onConsole = (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 180)); };
    const onFail = (r) => netErrors.push(`${r.url().replace(BASE, '')} ${r.failure()?.errorText}`);
    const onResp = (r) => statuses.push(r.status());
    page.on('console', onConsole); page.on('requestfailed', onFail); page.on('response', onResp);

    await page.goto(BASE + p.path, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => {});
    await page.waitForTimeout(2500);

    const m = await page.evaluate(() => {
      const de = document.documentElement;
      const spinner = document.querySelectorAll('[role="progressbar"], .animate-spin').length;
      const bodyText = document.body.innerText || '';
      return {
        dir: de.getAttribute('dir') || getComputedStyle(document.body).direction,
        overflow: Math.max(0, de.scrollWidth - de.clientWidth),
        mounted: (document.getElementById('root')?.children.length ?? 0) > 0,
        spinnersLeft: spinner,
        textLen: bodyText.length,
        hasArabic: /[\u0600-\u06FF]/.test(bodyText),
        loginVisible: /تسجيل الدخول|كلمة المرور/.test(bodyText) && bodyText.length < 900,
      };
    });

    await page.screenshot({ path: `${SHOT}/${p.id}-${size.name}.png`, fullPage: false });

    results.push({
      page: p.id, size: size.name, ...m,
      consoleErrors: consoleErrors.length, consoleSample: consoleErrors.slice(0, 3),
      networkErrors: netErrors.length, networkSample: netErrors.slice(0, 3),
      http5xx: statuses.filter((s) => s >= 500).length,
      http403: statuses.filter((s) => s === 403).length,
      http404: statuses.filter((s) => s === 404).length,
    });
    page.off('console', onConsole); page.off('requestfailed', onFail); page.off('response', onResp);
  }
  await page.close();
}

// قياس تصيير الأسطر المتعدّدة على تعليق إنتاج حقيقيّ قائم (بلا اقتباس المحتوى).
const mp = await ctx.newPage();
await mp.setViewportSize(SIZES[0]);
await mp.goto(`${BASE}/app/submissions?open=${MULTILINE_SUBMISSION}`, { waitUntil: 'networkidle', timeout: 60000 });
await mp.waitForTimeout(3000);
const multiline = await mp.evaluate(() => {
  const cands = [];
  for (const el of document.querySelectorAll('div,p,span,pre,td')) {
    if (el.children.length > 0) continue;
    const t = el.textContent || '';
    if (t.length > 400 && t.includes('\n')) {
      const cs = getComputedStyle(el);
      const lineH = parseFloat(cs.lineHeight) || 0;
      cands.push({
        len: t.length,
        newlines: (t.match(/\n/g) || []).length,
        whiteSpace: cs.whiteSpace,
        wordBreak: cs.overflowWrap || cs.wordBreak,
        renderedHeight: Math.round(el.getBoundingClientRect().height),
        lineHeight: lineH,
        approxVisualLines: lineH ? Math.round(el.getBoundingClientRect().height / lineH) : null,
        clipped: el.scrollHeight - el.clientHeight > 2,
      });
    }
  }
  return cands.sort((a, b) => b.len - a.len).slice(0, 3);
});
await mp.close();

fs.writeFileSync(`${OUT}/prod-auth-readonly.json`, JSON.stringify({ profile: PROFILE, unsafeAttempts, multiline, results }, null, 1));

let fail = 0;
for (const r of results) {
  const ok = r.mounted && r.dir === 'rtl' && r.overflow === 0 && r.consoleErrors === 0 && r.networkErrors === 0 && r.http5xx === 0 && !r.loginVisible && r.spinnersLeft === 0;
  if (!ok) fail++;
  console.log(`[${ok ? 'PASS' : 'FAIL'}] ${r.page}/${r.size} dir=${r.dir} ovf=${r.overflow} con=${r.consoleErrors} net=${r.networkErrors} 5xx=${r.http5xx} 403=${r.http403} 404=${r.http404} spin=${r.spinnersLeft} login=${r.loginVisible}`);
}
console.log('MULTILINE_RENDER=' + JSON.stringify(multiline));
console.log('UNSAFE_REQUESTS_BLOCKED=' + unsafeAttempts.length);
console.log(fail === 0 ? 'PROD_AUTH_READONLY_GATE=PASS' : `PROD_AUTH_READONLY_GATE=FAIL (${fail})`);
await ctx.close();
process.exit(fail === 0 ? 0 : 1);

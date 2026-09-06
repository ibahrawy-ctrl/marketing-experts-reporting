#!/usr/bin/env node
// R22B-PROD §7/§10 — إعادة التقاط لقطات الإنتاج مع إخفاء PII قبل حفظها كدليل.
// الإخفاء: تمويه بصريّ لكتلة الهويّة في الترويسة ولكلّ عنصر ورقيّ نصّه > 60 حرفًا
// (النصّ الحرّ وأسماء الأشخاص والمحتوى)، مع إبقاء التسميات والحالات والتنقّل لإثبات التخطيط.
// قراءة فقط: كلّ طلب غير آمن مُلغى.
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const OUT = '/private/tmp/prod-verify/screenshots-auth-redacted';
fs.mkdirSync(OUT, { recursive: true });

const MULTILINE_SUBMISSION = 'de3e9c56-6a01-401f-97d6-a90dafb87708';
const PAGES = [
  ['P01-home', '/app'],
  ['P02-submissions', '/app/submissions'],
  ['P03-my-reports', '/app/my-reports'],
  ['P04-submission-multiline', `/app/submissions?open=${MULTILINE_SUBMISSION}`],
  ['P05-admin-archive', '/app/admin/archive'],
  ['P06-projects', '/app/projects'],
  ['P07-report-templates', '/app/report-templates'],
  ['P08-report-calendar', '/app/report-calendar'],
  ['P09-notfound', '/app/projects/00000000-0000-0000-0000-000000000000/360'],
];
const SIZES = [['desktop', 1440, 900], ['mobile390', 390, 844]];

const REDACT = `(() => {
  const st = document.createElement('style');
  st.textContent = '.__pii{filter:blur(6px) !important;}';
  document.head.appendChild(st);
  let n = 0;
  for (const el of document.querySelectorAll('header *, [class*="avatar"], [class*="Avatar"]')) {
    const t = (el.textContent || '').trim();
    if (el.children.length === 0 && t.length > 1) { el.classList.add('__pii'); n++; }
  }
  for (const el of document.querySelectorAll('div,p,span,td,th,li,pre,h1,h2,h3,a')) {
    if (el.children.length > 0) continue;
    if (el.closest('nav,aside,button')) continue;
    const t = (el.textContent || '').trim();
    if (t.length > 12 || t.includes('@')) { el.classList.add('__pii'); n++; }
  }
  return n;
})()`;

const ctx = await chromium.launchPersistentContext('/private/tmp/prod-auth/ceo', { headless: true, viewport: { width: 1440, height: 900 } });
await ctx.route('**/*', (r) => (['POST', 'PUT', 'PATCH', 'DELETE'].includes(r.request().method()) && !/\/auth\/refresh|\/hubs\//.test(r.request().url()) ? r.abort() : r.continue()));

const log = [];
for (const [sname, w, h] of SIZES) {
  const page = await ctx.newPage();
  await page.setViewportSize({ width: w, height: h });
  for (const [id, path] of PAGES) {
    await page.goto(BASE + path, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => {});
    await page.waitForTimeout(2500);
    const redacted = await page.evaluate(REDACT);
    await page.screenshot({ path: `${OUT}/${id}-${sname}.png`, fullPage: true });
    log.push(`${id}-${sname} redactedElements=${redacted}`);
    console.log(log.at(-1));
  }
  await page.close();
}
fs.writeFileSync(`${OUT}/redaction-log.txt`, log.join('\n') + '\n');
await ctx.close();

/** استكشاف أفعال صفوف المخرَجات التعاقديّة (تعديل/حذف/تحديث تقدّم). */
import path from 'node:path';
import fs from 'node:fs';
import { readSecrets, launch, login, goto, instrument, newSink, clickText, EVIDENCE_ROOT } from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
instrument(page, newSink());
await login(page, 'emp2@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(2000);

const blocks = await page.$$eval('*', (els) => els
  .filter((e) => /P360-R21-UAT-DELIVERABLE/.test(e.textContent || '')
    && ![...e.children].some((c) => /P360-R21-UAT-DELIVERABLE/.test(c.textContent || '')))
  .slice(0, 6)
  .map((e) => {
    let box = e;
    for (let i = 0; i < 5 && box.parentElement; i += 1) {
      if (box.querySelectorAll('button').length) break;
      box = box.parentElement;
    }
    return {
      tag: box.tagName,
      text: (box.textContent || '').replace(/\s+/g, ' ').slice(0, 160),
      buttons: [...box.querySelectorAll('button')].map((b) => (b.textContent || '').trim()).slice(0, 10),
    };
  }));
console.log(JSON.stringify(blocks, null, 1).slice(0, 3000));
await browser.close();

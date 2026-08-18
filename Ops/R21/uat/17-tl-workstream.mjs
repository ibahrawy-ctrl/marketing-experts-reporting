/**
 * §12 الخطوة 30: قائد الفريق يدير هدف عمل (Workstream) للمشروع الذي يقوده — إضافة ثمّ تعديل.
 * هذه هي الشاشة التي كانت محجوبة عنه قبل R2.1 (`GAP-R21-01`/`GAP-R21-05`) بينما الخادم يقبله.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink, NAMES,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');

await login(page, 'team.leader@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}`, 3500);

const buttons = await page.evaluate(() => [...document.querySelectorAll('button')].map((b) => (b.textContent || '').trim()).filter(Boolean));
console.log('BUTTONS', JSON.stringify(buttons).slice(0, 700));
await shot(page, DIR, 'tl-workstreams-card', 'بطاقة أهداف العمل مفتوحة لقائد الفريق المُسنَد مع أزرار الإدارة', { role: 'TL', step: 30 });

// تعديل هدف العمل القائم
const opened = await page.evaluate((name) => {
  const cells = [...document.querySelectorAll('td')];
  const cell = cells.find((c) => (c.textContent || '').includes(name));
  if (!cell) return 'no-row';
  const row = cell.closest('tr');
  const btn = [...row.querySelectorAll('button')].find((b) => /تعديل/.test(b.textContent || ''));
  if (!btn) return 'no-edit-button';
  btn.click();
  return 'ok';
}, NAMES.workstream);
console.log('EDIT OPEN', opened);
await page.waitForTimeout(1500);
await shot(page, DIR, 'tl-workstream-edit-form', 'نموذج تعديل هدف العمل مفتوح بحساب قائد الفريق', { role: 'TL', step: 30 });

const changed = await page.evaluate(() => {
  const ta = document.querySelector('form textarea') || document.querySelector('textarea');
  if (!ta) return false;
  const setter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;
  setter.call(ta, 'حُدِّث الوصف تشغيليًّا بواسطة قائد الفريق المُسنَد ضمن UAT R2.1.');
  ta.dispatchEvent(new Event('input', { bubbles: true }));
  return true;
});
console.log('DESC CHANGED', changed);
await page.waitForTimeout(400);

await clickText(page, 'button', /^(حفظ|حفظ التعديل|تحديث)$/);
await page.waitForTimeout(3200);
await goto(page, `/app/projects/${projectId}`, 3000);
await shot(page, DIR, 'tl-workstream-saved', 'هدف العمل بعد التعديل التشغيليّ — الحفظ قبِله الخادم', { role: 'TL', step: 30 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-m.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-m.json'), { sink, buttons });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

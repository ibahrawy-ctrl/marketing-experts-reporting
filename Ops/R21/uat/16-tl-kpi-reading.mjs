/** §12 الخطوة 29: قائد الفريق يسجّل قراءة مؤشّر يدويّ ويُثبَت حفظها منسوبةً إليه. */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink,
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
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^المؤشّرات والقراءات$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-kpi-table', 'جدول المؤشّرات بعين قائد الفريق قبل تسجيل القراءة', { role: 'TL', step: 29 });

await clickText(page, 'button', /^القراءات$/);
await page.waitForTimeout(1500);

const filled = await page.evaluate(() => {
  const inputs = [...document.querySelectorAll('input')];
  const d = inputs.find((i) => i.type === 'date');
  const v = inputs.find((i) => i.type === 'number');
  if (!d || !v) return false;
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
  setter.call(d, '2026-08-17');
  d.dispatchEvent(new Event('input', { bubbles: true }));
  setter.call(v, '72');
  v.dispatchEvent(new Event('input', { bubbles: true }));
  return true;
});
console.log('FILLED', filled);
await page.waitForTimeout(600);
await shot(page, DIR, 'tl-kpi-reading-form', 'نموذج تسجيل قراءة المؤشّر معبّأ بحساب قائد الفريق', { role: 'TL', step: 29 });

await clickText(page, 'button', /^تسجيل قراءة$/);
await page.waitForTimeout(3200);
await shot(page, DIR, 'tl-kpi-reading-saved', 'القراءة محفوظة ومنسوبة إلى قائد الفريق مع تاريخها', { role: 'TL', step: 29 });

await goto(page, `/app/projects/${projectId}/360`, 2500);
await clickText(page, 'button', /^المؤشّرات والقراءات$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-kpi-current-value', 'القيمة الحاليّة ونسبة التحقّق للمؤشّر بعد القراءة', { role: 'TL', step: 29 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-l.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-l.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

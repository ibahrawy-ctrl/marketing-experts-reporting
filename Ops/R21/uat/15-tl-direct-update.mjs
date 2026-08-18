/**
 * §12 الخطوة 28: قائد الفريق يحدّث مخرَجًا **تشغيليًّا مباشرةً** (بلا ادّعاء)، ويُثبَت أنّ
 * التحديث المباشر يترك أثرًا كاملًا: الفاعل والسبب والقيمة السابقة، موسومًا «تحديث مباشر»
 * كي لا يُقرأ فعلُ طرفٍ واحد كأنّه مرّ بدورة مراجعة من طرفين.
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
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-deliverable-b-before', 'المخرَج B قبل التحديث المباشر: ٠٪', { role: 'TL', step: 28 });

const filled = await page.evaluate((name) => {
  const anchor = document.querySelector(`select[aria-label="حالة المخرَج ${name}"]`);
  if (!anchor) return 'no-anchor';
  const row = anchor.closest('div.flex');
  const inputs = [...row.querySelectorAll('input')];
  const num = inputs.find((i) => i.type === 'number');
  const txt = inputs.find((i) => i.type !== 'number');
  if (!num || !txt) return 'no-inputs';
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
  setter.call(num, '25');
  num.dispatchEvent(new Event('input', { bubbles: true }));
  setter.call(txt, 'تحديث تشغيليّ مباشر من قائد الفريق: اعتُمدت الدفعة الأولى من المخرَج B.');
  txt.dispatchEvent(new Event('input', { bubbles: true }));
  return 'ok';
}, NAMES.deliverableB);
console.log('FILL', filled);
await page.waitForTimeout(600);
await shot(page, DIR, 'tl-direct-update-form', 'قائد الفريق يكتب النسبة الجديدة وسبب التحديث المباشر للمخرَج B', { role: 'TL', step: 28 });

const clicked = await page.evaluate((name) => {
  const anchor = document.querySelector(`select[aria-label="حالة المخرَج ${name}"]`);
  const row = anchor.closest('div.flex');
  const btn = [...row.querySelectorAll('button')].find((b) => (b.textContent || '').trim() === 'تحديث');
  if (!btn || btn.disabled) return false;
  btn.click();
  return true;
}, NAMES.deliverableB);
console.log('COMMIT', clicked);
await page.waitForTimeout(3500);

await goto(page, `/app/projects/${projectId}/360`, 3000);
await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-deliverable-b-after', 'المخرَج B بعد التحديث المباشر: ٢٥٪', { role: 'TL', step: 28 });

await clickText(page, 'button', /^تحديثات التنفيذ$/);
await page.waitForTimeout(1500);
await clickText(page, 'button', /^الكلّ$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-direct-update-audit', 'سلسلة الأثر: التحديث المباشر موسوم ومعه الفاعل والسبب والقيمة السابقة', { role: 'TL', step: 33 });

await clickText(page, 'button', /^النظرة العامّة$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-progress-after-direct', 'تقدّم المشروع بعد التحديث المباشر: ٠٫٦×٥٠ + ٠٫٤×٢٥ = ٤٠٪', { role: 'TL', step: 28 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-k.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-k.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

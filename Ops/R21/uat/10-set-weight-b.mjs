/** ضبط وزن المخرَج B على ٤٠٪ عبر «تحديث الوزن» في الواجهة ⟹ مجموع الأوزان ١٠٠٪ ووضع Weighted. */
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
await login(page, 'emp2@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(2000);

const sel = `input[aria-label="وزن المخرَج ${NAMES.deliverableB}"]`;
await page.fill(sel, '40');
await shot(page, DIR, 'deliverable-b-weight', 'ضبط وزن المخرَج B على ٤٠٪ ليكتمل مجموع الأوزان ١٠٠٪', { role: 'OWNER', step: 8 });
// زرّ «تحديث الوزن» المجاور لهذا الحقل بعينه لا أوّل زرّ في الصفحة
const ok = await page.evaluate((s2) => {
  const input = document.querySelector(s2);
  if (!input) return false;
  const box = input.closest('div')?.parentElement;
  const btn = [...(box?.querySelectorAll('button') || [])].find((b) => /تحديث الوزن/.test(b.textContent || ''));
  if (!btn || btn.disabled) return false;
  btn.click();
  return true;
}, sel);
console.log('WEIGHT COMMITTED', ok);
await page.waitForTimeout(3000);
await goto(page, `/app/projects/${projectId}/360`, 3000);
await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(1800);
await shot(page, DIR, 'deliverables-weighted-100', 'المخرَجان بوزنَي ٦٠٪ و٤٠٪ — مجموع صالح ١٠٠٪ ⟹ نموذج Weighted', { role: 'OWNER', step: 8 });
writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-f.json'), shots);
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 3), h: sink.httpErrors.slice(0, 6) }).slice(0, 600));
await browser.close();

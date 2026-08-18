/**
 * §12 الخطوات 6–8: مؤشّر مرتبط بالهدف، مسار عمل، ومخرَجان تعاقديّان موزونان (60/40)
 * — كلّها بحساب مالك المشروع المُسنَد.
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
const notes = [];

await login(page, 'emp2@uat.local', s.UAT_PW);
const tab = async (n) => { await clickText(page, 'button', new RegExp(`^${n}$`)); await page.waitForTimeout(1600); };
const vis = async (sel, ty) => {
  const out = [];
  for (const el of await page.$$(sel)) {
    if (!(await el.isVisible())) continue;
    if (ty && (await el.evaluate((e) => e.type)) !== ty) continue;
    out.push(el);
  }
  return out;
};

// ── 6) مؤشّر مرتبط بالهدف ────────────────────────────────────────────────
await goto(page, `/app/projects/${projectId}/360`, 3200);
await tab('المؤشّرات والقراءات');
try {
  await clickText(page, 'button', /مؤشّر جديد تحت هذا الهدف/);
  await page.waitForTimeout(1300);
  const t = await vis('input', 'text');
  await t[0].fill(NAMES.kpi);
  const n = await vis('input', 'number');
  await n[0].fill('1000');       // القيمة المستهدَفة
  if (n[1]) await n[1].fill('200'); // خطّ الأساس
  if (n[2]) await n[2].fill('100'); // الوزن
  await shot(page, DIR, 'kpi-form-owner', 'إنشاء مؤشّر أداء مرتبط بالهدف بحساب مالك المشروع', { role: 'OWNER', step: 6 });
  await clickText(page, 'button', /^حفظ$/);
  await page.waitForTimeout(2600);
  await shot(page, DIR, 'kpi-saved', 'المؤشّر محفوظ تحت الهدف داخل تبويب المؤشّرات', { role: 'OWNER', step: 6 });
} catch (e) { notes.push(`KPI: ${String(e).slice(0, 200)}`); }

// ── 8) مخرَجان تعاقديّان موزونان ─────────────────────────────────────────
const addDeliverable = async (name, weight, qty) => {
  await goto(page, `/app/projects/${projectId}/360`, 3000);
  await tab('المخرَجات التعاقديّة');
  await clickText(page, 'button', /مخرَج تعاقديّ جديد/);
  await page.waitForTimeout(1300);
  const sels = await vis('select');
  await sels[0].selectOption('posts_package');
  const t = await vis('input', 'text');
  await t[0].fill(name);
  const n = await vis('input', 'number');
  await n[0].fill(String(qty));   // الكمّيّة المخطَّطة
  const d = await vis('input', 'date');
  if (d[0]) await d[0].fill('2026-12-31');
  const n2 = await vis('input', 'number');
  await n2[n2.length - 1].fill(String(weight)); // الوزن (%)
  await shot(page, DIR, `deliverable-form-${name.slice(-1)}`, `إنشاء المخرَج ${name} بوزن ${weight}%`, { role: 'OWNER', step: 8 });
  await clickText(page, 'button', /^حفظ$/);
  await page.waitForTimeout(2600);
};
try { await addDeliverable(NAMES.deliverableA, 60, 20); } catch (e) { notes.push(`DELIV-A: ${String(e).slice(0, 200)}`); }
try { await addDeliverable(NAMES.deliverableB, 40, 10); } catch (e) { notes.push(`DELIV-B: ${String(e).slice(0, 200)}`); }
await goto(page, `/app/projects/${projectId}/360`, 3000);
await tab('المخرَجات التعاقديّة');
await shot(page, DIR, 'deliverables-weighted', 'المخرَجان التعاقديّان بوزنَي ٦٠٪ و٤٠٪ ومجموع ١٠٠٪', { role: 'OWNER', step: 8 });

// ── 7) مسار عمل (أهداف العمل داخل صفحة المشروع) ──────────────────────────
await goto(page, `/app/projects/${projectId}`, 3000);
try {
  await clickText(page, 'button', /إضافة هدف عمل/);
  await page.waitForTimeout(1300);
  const f = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e, i) => ({
      i, t: e.tagName, ty: e.type, lb: (e.closest('label')?.textContent || '').trim().slice(0, 35),
    })));
  console.log('WORKSTREAM FORM', JSON.stringify(f).slice(0, 1600));
  const t = await vis('input', 'text');
  if (t[0]) await t[0].fill(NAMES.workstream);
  await shot(page, DIR, 'workstream-form-owner', 'إنشاء مسار عمل (هدف عمل) بحساب مالك المشروع', { role: 'OWNER', step: 7 });
  const btns = await page.$$eval('button', (els) =>
    [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
  console.log('WORKSTREAM BUTTONS', JSON.stringify(btns.slice(-8)));
  await clickText(page, 'button', /^(حفظ|إضافة)$/);
  await page.waitForTimeout(2600);
  await shot(page, DIR, 'workstream-saved-owner', 'مسار العمل ظاهر في صفحة المشروع', { role: 'OWNER', step: 7 });
} catch (e) { notes.push(`WORKSTREAM: ${String(e).slice(0, 200)}`); }

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-d.json'), shots);
console.log('NOTES', JSON.stringify(notes));
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 3), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

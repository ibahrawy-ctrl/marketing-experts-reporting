/**
 * §12 الخطوات 4–8: الاستراتيجيّة والهدف والمؤشّر ومسار العمل والمخرَجات الموزونة —
 * كلّها بحساب **مالك المشروع** (`emp2@uat.local`، دوره Employee) لإثبات أنّ القدرة البنيويّة
 * تأتي من إسناد `ProjectOwnerId` لا من الدور.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink, PERSONAS, NAMES,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const OWNER = { email: 'emp2@uat.local', label: 'مالك المشروع (Employee مُسنَد)' };
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');
const notes = [];

await login(page, OWNER.email, s.UAT_PW);

const tab = async (name) => { await clickText(page, 'button', new RegExp(`^${name}$`)); await page.waitForTimeout(1600); };
const visible = async (sel, ty) => {
  const out = [];
  for (const el of await page.$$(sel)) {
    if (!(await el.isVisible())) continue;
    if (ty && (await el.evaluate((e) => e.type)) !== ty) continue;
    out.push(el);
  }
  return out;
};

await goto(page, `/app/projects/${projectId}/360`, 3500);
await shot(page, DIR, 'p360-owner-overview', 'Project 360 — النظرة العامّة بعين مالك المشروع المُسنَد', { role: 'OWNER', step: 3 });

// ── 4) الاستراتيجيّة ──────────────────────────────────────────────────────
await tab('الاستراتيجيّة');
try {
  await clickText(page, 'button', /تسجيل الاستراتيجيّة/);
  await page.waitForTimeout(1200);
  const tas = await visible('textarea');
  const vals = [
    'أن يصبح العميل الموسوم مرجعًا في قطاعه خلال سنة (بيانات UAT).',
    'استراتيجيّة UAT مختصرة لإثبات مسار R2.1.', 'رواد الأعمال في السعوديّة',
    'مسوّق رقميّ ٣٠ عامًا', 'الجودة والسرعة', 'خدمة أسرع بسعر عادل',
    'منافس أ، منافس ب', 'ودّيّة ومهنيّة', 'الجودة أوّلًا', 'نموّ عضويّ',
    'سرعة الاستجابة', 'تعليميّ وترفيهيّ', 'ثلاث مرّات أسبوعيًّا', 'واثق وهادئ', 'إنستغرام، لينكدإن',
  ];
  for (let i = 0; i < tas.length && i < vals.length; i += 1) await tas[i].fill(vals[i]);
  await shot(page, DIR, 'strategy-form-owner', 'تسجيل الاستراتيجيّة بحساب مالك المشروع', { role: 'OWNER', step: 4 });
  await clickText(page, 'button', /^حفظ$/);
  await page.waitForTimeout(2500);
  await shot(page, DIR, 'strategy-saved', 'الاستراتيجيّة محفوظة ومعروضة داخل Project 360', { role: 'OWNER', step: 4 });
} catch (e) { notes.push(`STRATEGY: ${String(e).slice(0, 160)}`); }

// ── 5) هدف ───────────────────────────────────────────────────────────────
await goto(page, `/app/projects/${projectId}/360`, 3000);
await tab('الأهداف');
try {
  await clickText(page, 'button', /هدف جديد/);
  await page.waitForTimeout(1200);
  const texts = await visible('input', 'text');
  await texts[0].fill(`${NAMES.project}-OBJECTIVE`);
  if (texts[1]) await texts[1].fill('هدف UAT لإثبات ربط المؤشّر بالهدف.');
  const nums = await visible('input', 'number');
  if (nums[0]) await nums[0].fill('100');
  const dates = await visible('input', 'date');
  if (dates[0]) await dates[0].fill('2026-08-01');
  if (dates[1]) await dates[1].fill('2026-12-31');
  await shot(page, DIR, 'objective-form-owner', 'إضافة هدف للمشروع بحساب مالك المشروع', { role: 'OWNER', step: 5 });
  await clickText(page, 'button', /^حفظ$/);
  await page.waitForTimeout(2500);
  await shot(page, DIR, 'objective-saved', 'الهدف محفوظ داخل تبويب الأهداف', { role: 'OWNER', step: 5 });
} catch (e) { notes.push(`OBJECTIVE: ${String(e).slice(0, 160)}`); }

// ── 6) مؤشّر مرتبط بالهدف — استكشاف ثمّ تعبئة ─────────────────────────────
await goto(page, `/app/projects/${projectId}/360`, 3000);
await tab('المؤشّرات والقراءات');
const kpiBtns = await page.$$eval('button', (els) =>
  [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
console.log('KPI TAB BUTTONS', JSON.stringify(kpiBtns.slice(-8)));
try {
  await clickText(page, 'button', /مؤشّر جديد|مؤشّر أداء جديد|إضافة مؤشّر/);
  await page.waitForTimeout(1300);
  const f = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e, i) => ({
      i, t: e.tagName, ty: e.type, lb: (e.closest('label')?.textContent || '').trim().slice(0, 40),
      o: e.tagName === 'SELECT' ? [...e.options].map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 8) : undefined,
    })));
  console.log('KPI FORM', JSON.stringify(f).slice(0, 2500));
} catch (e) { notes.push(`KPI-FORM: ${String(e).slice(0, 160)}`); }

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-c.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-c.json'), { notes, sink });
console.log('NOTES', JSON.stringify(notes));
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 3), h: sink.httpErrors.slice(0, 6), fb: sink.forbiddenHosts }).slice(0, 800));
await browser.close();

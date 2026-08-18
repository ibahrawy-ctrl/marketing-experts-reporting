/** تصحيح البنية: حذف التكرار الناتج عن إعادة التشغيل، ضبط الأوزان ٦٠/٤٠، وإنشاء مسار العمل بنوعه. */
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

// ── أ) أفعال صفوف المخرَجات ──────────────────────────────────────────────
await goto(page, `/app/projects/${projectId}/360`, 3200);
await tab('المخرَجات التعاقديّة');
const rowInfo = await page.$$eval('tr', (rows) => rows
  .filter((r) => /P360-R21-UAT-DELIVERABLE/.test(r.textContent || ''))
  .map((r) => ({
    text: (r.textContent || '').replace(/\s+/g, ' ').slice(0, 120),
    buttons: [...r.querySelectorAll('button')].map((b) => (b.textContent || '').trim()),
  })));
console.log('DELIVERABLE ROWS', JSON.stringify(rowInfo, null, 1).slice(0, 2200));

// ── ب) مسار العمل مع اختيار النوع ────────────────────────────────────────
await goto(page, `/app/projects/${projectId}`, 3000);
try {
  await clickText(page, 'button', /إضافة هدف عمل/);
  await page.waitForTimeout(1400);
  const sels = await vis('select');
  const typeOpts = await sels[0].$$eval('option', (o) => o.map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 12));
  console.log('WS TYPE OPTS', JSON.stringify(typeOpts));
  const teamOpts = await sels[1].$$eval('option', (o) => o.map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 12));
  console.log('WS TEAM OPTS', JSON.stringify(teamOpts));
  const firstType = typeOpts.map((x) => x.split('|')[0]).find((v) => v);
  await sels[0].selectOption(firstType);
  const firstTeam = teamOpts.map((x) => x.split('|')[0]).find((v) => v);
  if (firstTeam) await sels[1].selectOption(firstTeam);
  const t = await vis('input', 'text');
  await t[0].fill(NAMES.workstream);
  await shot(page, DIR, 'workstream-form-owner', 'إنشاء مسار عمل (هدف عمل) بحساب مالك المشروع المُسنَد بدور Employee', { role: 'OWNER', step: 7 });
  await clickText(page, 'button', /^حفظ$/);
  await page.waitForTimeout(2800);
  await shot(page, DIR, 'workstream-saved-owner', 'مسار العمل محفوظ وظاهر في صفحة المشروع', { role: 'OWNER', step: 7 });
} catch (e) { notes.push(`WS: ${String(e).slice(0, 220)}`); }

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-e.json'), shots);
console.log('NOTES', JSON.stringify(notes));
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 3), h: sink.httpErrors.slice(0, 8) }).slice(0, 700));
await browser.close();

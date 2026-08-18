/**
 * §12 الخطوات 1–3: إنشاء عميل UAT موسوم، فتح Client 360، وإنشاء مشروع تحته — بواجهة حقيقيّة.
 * يُخرِج معرّفات العميل والمشروع إلى Ops/R21/uat/evidence/ids.json.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink, PERSONAS, BASE, NAMES,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const AM_ID = 'f18df329-4e41-489c-9887-aeca572658e7'; // account.manager@uat.local
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');

await login(page, PERSONAS.ADMIN.email, s.UAT_PW);

// ── 1) إنشاء العميل الموسوم ───────────────────────────────────────────────
await goto(page, '/app/clients', 2500);
let clientId = await page.$$eval('a[href^="/app/clients/"]', (els, name) => {
  const hit = els.find((e) => (e.closest('tr')?.textContent || '').includes(name));
  return hit ? hit.getAttribute('href').split('/').pop() : null;
}, NAMES.client);

if (!clientId) {
  await clickText(page, 'button', /إضافة عميل/);
  await page.waitForTimeout(1200);
  const all = await page.$$('input');
  const inputs = [];
  for (const el of all) {
    const ty = await el.evaluate((e) => e.type);
    if ((ty === 'text' || ty === '') && (await el.isVisible())) inputs.push(el);
  }
  if (!inputs.length) throw new Error('client form inputs not found');
  await inputs[0].fill(NAMES.client);
  const selects = await page.$$('select');
  for (const sel of selects) {
    const vals = await sel.$$eval('option', (o) => o.map((x) => x.value));
    if (vals.includes(AM_ID)) { await sel.selectOption(AM_ID); break; }
  }
  // جهة الاتصال + الملاحظات (بيانات وهميّة موسومة، بلا أيّ بيانات شخصيّة حقيقيّة)
  if (inputs[1]) await inputs[1].fill(`${NAMES.client}-CONTACT`);
  if (inputs[2]) await inputs[2].fill('uat@example.invalid');
  if (inputs[3]) await inputs[3].fill('بيانات UAT مؤقّتة — تُحذف بعد التوثيق');
  await shot(page, DIR, 'client-create-form', 'نموذج إنشاء العميل الموسوم P360-R21-UAT-CLIENT', { role: 'ADMIN', step: 1 });
  await clickText(page, 'button', /^(حفظ|إضافة|إنشاء)/);
  await page.waitForTimeout(2500);
  await goto(page, '/app/clients', 2500);
  clientId = await page.$$eval('a[href^="/app/clients/"]', (els, name) => {
    const hit = els.find((e) => (e.closest('tr')?.textContent || '').includes(name));
    return hit ? hit.getAttribute('href').split('/').pop() : null;
  }, NAMES.client);
}
if (!clientId) throw new Error('client not created');
console.log('CLIENT_ID', clientId);

// ── 2) Client 360 ─────────────────────────────────────────────────────────
await goto(page, `/app/clients/${clientId}`, 3000);
await shot(page, DIR, 'client360-empty', 'Client 360 للعميل الموسوم قبل إضافة المشروع', { role: 'ADMIN', step: 2 });
const cBtns = await page.$$eval('button', (els) =>
  [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
console.log('CLIENT360 BUTTONS', JSON.stringify(cBtns).slice(0, 900));

// ── 3) إنشاء المشروع تحت العميل (تبويب «المشاريع» داخل Client 360) ────────
await clickText(page, 'button', /^المشاريع$/);
await page.waitForTimeout(1800);
const pTabBtns = await page.$$eval('button', (els) =>
  [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
console.log('CLIENT360/PROJECTS BUTTONS', JSON.stringify(pTabBtns).slice(0, 900));

let projectId = await page.$$eval('a[href^="/app/projects/"]', (els, name) => {
  const hit = els.find((e) => (e.closest('tr')?.textContent || '').includes(name));
  return hit ? hit.getAttribute('href').split('/')[3] : null;
}, NAMES.project);

if (!projectId) {
  await clickText(page, 'button', /مشروع/);
  await page.waitForTimeout(1500);
  const fields = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e) => ({
      t: e.tagName, ty: e.type, lb: (e.closest('label')?.textContent || '').trim().slice(0, 45),
      ph: e.placeholder || undefined,
      o: e.tagName === 'SELECT' ? [...e.options].map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 16) : undefined,
    })));
  console.log('PROJECT-FORM', JSON.stringify(fields).slice(0, 4000));
  const dlgBtns = await page.$$eval('button', (els) =>
    [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
  console.log('PROJECT-FORM BUTTONS', JSON.stringify(dlgBtns).slice(0, 900));
}

fs.mkdirSync(EVIDENCE_ROOT, { recursive: true });
writeJson(path.join(EVIDENCE_ROOT, 'ids.json'), { clientId, projectId });
writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-a.json'), shots);
console.log('SINK', JSON.stringify({ c: sink.console, f: sink.failedRequests, fb: sink.forbiddenHosts, h: sink.httpErrors.slice(0, 6) }).slice(0, 900));
await browser.close();

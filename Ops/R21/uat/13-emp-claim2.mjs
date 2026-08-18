/** §12 الخطوة 21: عضو الفريق يرفع ادّعاءً ثانيًا بعد الرفض المسبَّب. */
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

await login(page, 'emp1@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^تحديثات التنفيذ$/);
await page.waitForTimeout(1800);
await clickText(page, 'button', /^تسجيل تنفيذ$/);
await page.waitForTimeout(1200);

await page.evaluate((name) => {
  const sel = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.textContent.includes(name)));
  const opt = [...sel.options].find((o) => o.textContent.includes(name));
  sel.value = opt.value;
  sel.dispatchEvent(new Event('change', { bubbles: true }));
}, NAMES.deliverableA);
await page.fill('input[type=number]', '50');
await page.fill('textarea', 'ادّعاء ثانٍ بعد معالجة سبب الرفض: أُرفِقت مخرجات المرحلة الأولى ونصف الثانية — ٥٠٪.');
await page.waitForTimeout(400);
await shot(page, DIR, 'emp-claim2-form', 'الادّعاء الثاني بعد معالجة سبب الرفض (٥٠٪ على المخرَج A)', { role: 'EMP', step: 21 });
await clickText(page, 'button', /^إرسال للمراجعة$/);
await page.waitForTimeout(3200);
await shot(page, DIR, 'emp-claim2-pending', 'الادّعاء الثاني معلَّق بانتظار مراجعة قائد الفريق', { role: 'EMP', step: 21 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-i.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-i.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

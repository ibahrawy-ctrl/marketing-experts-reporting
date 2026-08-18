/**
 * §12 الخطوات 12–14: عضو الفريق يفتح المشروع المسموح ويرفع ادّعاء تنفيذ،
 * ويُثبَت أنّه `Pending` ولا يمسّ تقدّم المخرَج ولا تقدّم المشروع.
 *
 * عضو الفريق هنا `emp1@uat.local` — رؤيته للمشروع من **عضويّة الفريق المالك** لا من دور،
 * وهذا هو الفرق الذي يجب أن تُثبته الصورة: يرى، ولا يملك حسم ادّعائه.
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

await login(page, 'emp1@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}/360`, 3200);
await shot(page, DIR, 'emp-p360-overview', 'عضو الفريق يفتح Project 360 للمشروع المسموح — يرى ولا يحسم', { role: 'EMP', step: 13 });

await clickText(page, 'button', /^تحديثات التنفيذ$/);
await page.waitForTimeout(1800);
await shot(page, DIR, 'emp-bridge-empty', 'تبويب تحديثات التنفيذ قبل رفع أيّ ادّعاء', { role: 'EMP', step: 13 });

await clickText(page, 'button', /^تسجيل تنفيذ$/);
await page.waitForTimeout(1200);

// اختيار المخرَج A من القائمة بالنصّ لا بالفهرس
const selected = await page.evaluate((name) => {
  const sel = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.textContent.includes(name)));
  if (!sel) return null;
  const opt = [...sel.options].find((o) => o.textContent.includes(name));
  sel.value = opt.value;
  sel.dispatchEvent(new Event('change', { bubbles: true }));
  return opt.textContent.trim();
}, NAMES.deliverableA);
console.log('SELECTED DELIVERABLE', selected);

await page.fill('input[type=number]', '35');
await page.fill('textarea', 'ادّعاء أوّل: أُنجزت المرحلة الأولى من المخرَج A — بانتظار مراجعة قائد الفريق.');
await page.waitForTimeout(400);
await shot(page, DIR, 'emp-claim-form', 'نموذج تسجيل التنفيذ معبّأ بحساب عضو الفريق (٣٥٪ على المخرَج A)', { role: 'EMP', step: 14 });

await clickText(page, 'button', /^إرسال للمراجعة$/);
await page.waitForTimeout(3200);
await shot(page, DIR, 'emp-claim-pending', 'الادّعاء «بانتظار المراجعة» — بلا زرّ قبول أو رفض لعضو الفريق', { role: 'EMP', step: 15 });

await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(1800);
await shot(page, DIR, 'emp-deliverables-unchanged', 'المخرَجات لم تتغيّر: الادّعاء المعلَّق لا ينقل نسبة', { role: 'EMP', step: 15 });

await clickText(page, 'button', /^النظرة العامّة$/);
await page.waitForTimeout(1800);
await shot(page, DIR, 'emp-overview-unchanged', 'تقدّم المشروع وصحّته دون تغيير ما دام الادّعاء معلَّقًا', { role: 'EMP', step: 15 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-g.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-g.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

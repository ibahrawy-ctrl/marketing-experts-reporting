/**
 * §12 الخطوات 31 و33 و34 — انتشار الأثر إلى عين مدير العميل (Account Manager).
 *
 * المقصود ليس أن يفتح الصفحة، بل أن **يرى الأثر نفسه** الذي أحدثه غيره: تقدّم المخرَج بعد
 * القبول، تقدّم المشروع، صحّته وأسبابها، وسلسلة التحديثات بالفاعل والوقت والقيمة السابقة —
 * ثمّ الرقم نفسه داخل ملفّ العميل (Client 360). لذلك تُنتزع القيم نصًّا وتُقارن، ولا يُكتفى
 * بأنّ الصفحة فُتِحت بنجاح.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink, NAMES,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { projectId, clientId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');
const observed = {};

const openTab = async (label) => {
  const ok = await page.evaluate((l) => {
    const b = [...document.querySelectorAll('button[role=tab]')].find((x) => x.textContent.trim() === l);
    if (!b) return false; b.click(); return true;
  }, label);
  await page.waitForTimeout(2600);
  return ok;
};
// تبويب «تحديثات التنفيذ» يفتح افتراضيًّا على «بانتظار المراجعة»، والسجلّ المحسوم يقع تحت «الكلّ».
// من دون هذه النقرة يبدو السجلّ فارغًا فيُقرَأ غيابُ الفلتر على أنّه غياب أثر.
const showAllProposals = async () => page.evaluate(() => {
  const b = [...document.querySelectorAll('[role=tabpanel] button')].find((x) => x.textContent.trim() === 'الكلّ');
  if (!b) return false; b.click(); return true;
});
const panelText = () => page.$eval('[role=tabpanel]', (e) => e.innerText.replace(/\s+/g, ' ').trim());


await login(page, 'account.manager@uat.local', s.UAT_PW);

// --- Project 360: النظرة العامّة (تقدّم + صحّة + أسباب + وقت احتساب) ---
await goto(page, `/app/projects/${projectId}/360`, 4000);
observed.overviewText = (await page.evaluate(() => document.body.innerText)).replace(/\s+/g, ' ').slice(0, 1800);
await shot(page, DIR, 'am-p360-overview',
  'مدير العميل يرى تقدّم المشروع وصحّته وأسبابها ووقت آخر احتساب كما أنتجها الخادم',
  { role: 'AM', step: 31 });

// --- المخرَجات التعاقديّة: قيمة المخرَج بعد القبول ---
observed.deliverablesTabOpened = await openTab('المخرَجات التعاقديّة');
observed.deliverablesText = await panelText();
await shot(page, DIR, 'am-deliverables-after-accept',
  'تطوّر المخرَجات بعين مدير العميل — القيم هي نتيجة قبول قائد الفريق لا إدخالًا مباشرًا منه',
  { role: 'AM', step: 31 });

// --- تحديثات التنفيذ: من غيّر ماذا ومتى ومن اعتمده ---
observed.bridgeTabOpened = await openTab('تحديثات التنفيذ');
observed.allFilterClicked = await showAllProposals();
await page.waitForTimeout(2600);
observed.bridgeText = await panelText();
await shot(page, DIR, 'am-execution-history',
  'سجلّ تحديثات التنفيذ: الرافع والمراجِع والقرار والقيمة السابقة والجديدة ووقت كلٍّ منهما',
  { role: 'AM', step: 34 });

// --- Client 360: ملخّص مشاريع العميل يحمل التقدّم وطريقته والصحّة (GAP-R21-02) ---
await goto(page, `/app/clients/${clientId}`, 3500);
await clickText(page, 'button', /^المشاريع$/).catch(() => {});
await page.waitForTimeout(2600);
observed.clientProjectsText = await panelText().catch(async () =>
  (await page.evaluate(() => document.body.innerText)).replace(/\s+/g, ' ').slice(0, 2000));
await shot(page, DIR, 'am-client360-projects-summary',
  'ملخّص مشاريع العميل داخل Client 360 يحمل التقدّم وطريقة احتسابه وحالة الصحّة',
  { role: 'AM', step: 33 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-o.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-o.json'), { sink, observed, names: NAMES });
console.log('OVERVIEW', observed.overviewText.slice(0, 900));
console.log('---DELIV', observed.deliverablesText.slice(0, 1200));
console.log('---BRIDGE', observed.bridgeText.slice(0, 2200));
console.log('---CLIENT', observed.clientProjectsText.slice(0, 900));
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

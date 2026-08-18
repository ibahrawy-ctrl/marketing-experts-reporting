/**
 * §12 الخطوة 32 — انتشار الأثر إلى العين الإداريّة (CEO/GM)، مع انتزاع صفوف الجداول نصًّا.
 *
 * الرؤية الإداريّة الشاملة ليست دليلًا بذاتها؛ الدليل أن تحمل الشاشة **نفس الأرقام والفاعلين**
 * الذين أنتجهم مسار الموظّف ← قائد الفريق. لذلك تُنتزع صفوف «تحديثات التنفيذ» و«المخرَجات»
 * صفًّا صفًّا لا كنصّ صفحة مقطوع، وإلّا لأثبتنا أنّ الصفحة فُتِحت فقط.
 */
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

await login(page, 'ceo@uat.local', s.UAT_PW);

await goto(page, `/app/projects/${projectId}/360`, 4000);
observed.header = (await page.evaluate(() => document.body.innerText)).replace(/\s+/g, ' ').slice(0, 1400);
await shot(page, DIR, 'mgmt-p360-overview',
  'العين الإداريّة (الرئيس التنفيذيّ) ترى تقدّم المشروع وصحّته وأسبابها بلا اختلاف عن عين قائد الفريق',
  { role: 'CEO', step: 32 });

observed.deliverablesTabOpened = await openTab('المخرَجات التعاقديّة');
observed.deliverablesText = await panelText();
await shot(page, DIR, 'mgmt-deliverables',
  'المخرَجات التعاقديّة بأوزانها ونسب إنجازها كما يراها المستوى الإداريّ',
  { role: 'CEO', step: 32 });

observed.bridgeTabOpened = await openTab('تحديثات التنفيذ');
observed.allFilterClicked = await showAllProposals();
await page.waitForTimeout(2600);
observed.bridgeText = await panelText();
await shot(page, DIR, 'mgmt-execution-history',
  'سلسلة تحديثات التنفيذ كاملة: الرافع والقيمة السابقة والجديدة والقرار والمراجِع ووقته',
  { role: 'CEO', step: 34 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-p.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-p.json'), { sink, observed });
console.log('HEADER', observed.header.slice(0, 800));
console.log('DELIVERABLES', observed.deliverablesText.slice(0, 1200));
console.log('BRIDGE', observed.bridgeText.slice(0, 2600));
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

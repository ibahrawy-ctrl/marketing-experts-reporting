/**
 * §12 الخطوات 3 و9 و10 و11: إنشاء المشروع تحت العميل الموسوم مع إسناد
 * مالك المشروع وقائد الفريق ومدير العميل والفريق المالك، ثمّ استكشاف تبويبات Project 360.
 *
 * الإسناد مقصود ليعطي أقوى برهان ممكن:
 *   الفريق المالك = «فريق UAT أ» (يضمّ عضو الفريق emp1) ويقوده lead@ — الذي **ليس** قائد المشروع
 *   قائد المشروع  = team.leader@ الذي **لا يقود** الفريق المالك ⟹ القدرة من الإسناد لا من قيادة الفريق
 *   مالك المشروع  = emp2@ بدور Employee ⟹ القدرة البنيويّة من الإسناد لا من الدور
 */
import path from 'node:path';
import {
  readSecrets, launch, login, goto, instrument, newSink, PERSONAS, NAMES,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const IDS = {
  ownerTeam: '626b14aa-eaa0-4fcc-9aaf-757d06157c6e', // فريق UAT أ
  accountManager: 'f18df329-4e41-489c-9887-aeca572658e7', // account.manager@uat.local
  projectOwner: '231565b2-31ff-457b-87fa-4706fe2e77e2', // emp2@uat.local (Employee)
  teamLeader: 'de210ca6-f008-4983-8786-77c3e59ce2b6', // team.leader@uat.local
};
const CLIENT_ID = JSON.parse(await import('node:fs').then((m) => m.promises.readFile(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'))).clientId;

const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');

await login(page, PERSONAS.ADMIN.email, s.UAT_PW);
await goto(page, `/app/clients/${CLIENT_ID}`, 2800);
await clickText(page, 'button', /^المشاريع$/);
await page.waitForTimeout(1600);

const findProject = () => page.$$eval('a[href^="/app/projects/"]', (els, name) => {
  const hit = els.find((e) => (e.closest('tr')?.textContent || '').includes(name));
  return hit ? hit.getAttribute('href').split('/')[3] : null;
}, NAMES.project);

let projectId = await findProject();
if (!projectId) {
  await clickText(page, 'button', /مشروع جديد/);
  await page.waitForTimeout(1500);
  const texts = [];
  for (const el of await page.$$('input')) {
    const ty = await el.evaluate((e) => e.type);
    if (ty === 'text' && (await el.isVisible())) texts.push(el);
  }
  await texts[0].fill(NAMES.project);
  if (texts[1]) await texts[1].fill('مشروع UAT مؤقّت للتذكرة R2.1 — يُحذف بعد التوثيق');
  const sels = await page.$$('select');
  const pick = async (id) => {
    for (const sel of sels) {
      const vals = await sel.$$eval('option', (o) => o.map((x) => x.value));
      if (vals.includes(id) && (await sel.inputValue()) !== id) { await sel.selectOption(id); return true; }
    }
    return false;
  };
  await pick(IDS.ownerTeam);
  await pick(IDS.accountManager);
  await pick(IDS.projectOwner);
  await pick(IDS.teamLeader);
  const dates = [];
  for (const el of await page.$$('input')) {
    if ((await el.evaluate((e) => e.type)) === 'date' && (await el.isVisible())) dates.push(el);
  }
  if (dates[0]) await dates[0].fill('2026-08-01');
  if (dates[1]) await dates[1].fill('2026-12-31');
  await shot(page, DIR, 'project-create-form', 'نموذج إنشاء المشروع مع إسناد مالك المشروع وقائد الفريق ومدير العميل', { role: 'ADMIN', step: 3 });
  await clickText(page, 'button', /حفظ المشروع/);
  await page.waitForTimeout(3000);
  await goto(page, `/app/clients/${CLIENT_ID}`, 2500);
  await clickText(page, 'button', /^المشاريع$/);
  await page.waitForTimeout(1600);
  projectId = await findProject();
}
if (!projectId) throw new Error('project not created');
console.log('PROJECT_ID', projectId);

// استكشاف تبويبات Project 360
await goto(page, `/app/projects/${projectId}/360`, 3500);
const tabs = await page.$$eval('button', (els) =>
  els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean));
console.log('P360 TABS/BUTTONS', JSON.stringify([...new Set(tabs)]).slice(0, 1200));

for (const t of ['الاستراتيجيّة', 'الأهداف', 'المؤشّرات والقراءات', 'المخرَجات التعاقديّة', 'تحديثات التنفيذ']) {
  try {
    await clickText(page, 'button', new RegExp(`^${t}$`));
    await page.waitForTimeout(1600);
    const b = await page.$$eval('button', (els) =>
      [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
    console.log(`\n[TAB ${t}] BUTTONS`, JSON.stringify(b).slice(0, 900));
  } catch (e) { console.log(`[TAB ${t}] ERROR`, String(e).slice(0, 120)); }
}

writeJson(path.join(EVIDENCE_ROOT, 'ids.json'), { clientId: CLIENT_ID, projectId });
writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-b.json'), shots);
console.log('\nSINK', JSON.stringify({ c: sink.console.slice(0, 3), f: sink.failedRequests.slice(0, 3), fb: sink.forbiddenHosts.slice(0, 3), h: sink.httpErrors.slice(0, 6) }).slice(0, 900));
await browser.close();

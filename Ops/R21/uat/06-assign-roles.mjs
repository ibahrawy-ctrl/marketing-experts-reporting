/**
 * §12 الخطوتان 9 و10: إسناد **مالك المشروع** و**قائد الفريق** من نموذج تعديل المشروع.
 * الاختيار بالفهرس داخل النموذج لا بالبحث عن القيمة، لأنّ قوائم الأشخاص الثلاث متطابقة
 * الخيارات فيلتقط البحثُ أوّلَ قائمة ويكتب فوق إسناد آخر.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink, PERSONAS,
  shot, shots, writeJson, clickText, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const IDS = {
  accountManager: 'f18df329-4e41-489c-9887-aeca572658e7',
  projectOwner: '231565b2-31ff-457b-87fa-4706fe2e77e2',
  teamLeader: 'de210ca6-f008-4983-8786-77c3e59ce2b6',
};
const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');

await login(page, PERSONAS.ADMIN.email, s.UAT_PW);
await goto(page, `/app/projects/${projectId}`, 3000);
await clickText(page, 'button', /تعديل المشروع/);
await page.waitForTimeout(1600);

const sels = [];
for (const el of await page.$$('select')) if (await el.isVisible()) sels.push(el);
const labelled = [];
for (const el of sels) {
  labelled.push({
    label: (await el.evaluate((e) => (e.closest('label')?.textContent || '').trim().slice(0, 20))),
    el,
  });
}
console.log('EDIT SELECT LABELS', JSON.stringify(labelled.map((x) => x.label)));

const setByLabel = async (re, value) => {
  const hit = labelled.find((x) => re.test(x.label));
  if (!hit) throw new Error(`select not found for ${re}`);
  await hit.el.selectOption(value);
};
await setByLabel(/مدير العميل/, IDS.accountManager);
await setByLabel(/مالك المشروع/, IDS.projectOwner);
await setByLabel(/قائد الفريق/, IDS.teamLeader);
await shot(page, DIR, 'project-assign-owner-and-leader',
  'إسناد مالك المشروع (موظّف) وقائد الفريق ومدير العميل من نموذج تعديل المشروع', { role: 'ADMIN', step: '9-10' });
await clickText(page, 'button', /^(حفظ|حفظ المشروع)$/);
await page.waitForTimeout(3000);
await goto(page, `/app/projects/${projectId}`, 3000);
await shot(page, DIR, 'project-after-assignment', 'صفحة المشروع بعد الإسناد: المالك وقائد الفريق ومدير العميل', { role: 'ADMIN', step: '9-10' });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-assign.json'), shots);
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 3), h: sink.httpErrors.slice(0, 6), fb: sink.forbiddenHosts }).slice(0, 800));
await browser.close();

/**
 * §12 الخطوتان 35 و36 (شقّ قائد الفريق) — `GAP-R21-06`.
 *
 * الشاشة والخادم يُقاسان في اللقطة نفسها: قائد الفريق المُسنَد يملك التشغيل ولا يملك البنية.
 * قبل الإصلاح كان زرّا «تعديل المشروع» و«أرشفة المشروع» يظهران له لأنّ الواجهة تشتقّهما من
 * `canManageClients` (تضمّ TeamLeader) بينما الخادم يحصرهما في `Policies.ProjectStructuralManage`
 * (تستثنيه) — زرٌّ يَعِد بما يردّه الخادم 403. الإثبات هنا من الطرفين معًا لا من طرف واحد.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink,
  shot, shots, writeJson, apiProbe, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');

await login(page, 'team.leader@uat.local', s.UAT_PW);
await goto(page, `/app/projects/${projectId}`, 3500);

const buttons = await page.evaluate(() =>
  [...document.querySelectorAll('button')].map((b) => (b.textContent || '').trim()).filter(Boolean));
const uiGuard = {
  editProjectVisible: buttons.some((b) => /^تعديل المشروع$/.test(b)),
  archiveProjectVisible: buttons.some((b) => /^أرشفة المشروع$/.test(b)),
  // ولا يُصادَر عنه التشغيل في الوقت نفسه — الإخفاء يجب أن يكون دقيقًا لا شاملًا.
  workstreamManageVisible: buttons.some((b) => /إضافة هدف عمل/.test(b)),
};
console.log('UI GUARD', JSON.stringify(uiGuard), JSON.stringify(buttons).slice(0, 500));
await shot(page, DIR, 'tl-no-structural-buttons',
  'صفحة المشروع بعين قائد الفريق: إدارة أهداف العمل متاحة، وزرّا تعديل المشروع وأرشفته غائبان',
  { role: 'TL', step: 36 });

// الحكم الفعليّ على الخادم: إخفاء الزرّ ليس حماية.
const probes = [];
probes.push({ id: 'TL-ARCHIVE-PROJECT', ...(await apiProbe(page, 'POST', `/projects/${projectId}/archive`, {})) });
probes.push({ id: 'TL-DELETE-PROJECT', ...(await apiProbe(page, 'DELETE', `/projects/${projectId}`)) });
console.log('PROBES', JSON.stringify(probes));

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-n.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-n.json'), { sink, buttons, uiGuard, probes });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

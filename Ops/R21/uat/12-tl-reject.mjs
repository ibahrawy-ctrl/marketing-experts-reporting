/**
 * §12 الخطوات 16–20: قائد الفريق المُسنَد يرى تفاصيل المشروع والادّعاء، ويُمنَع من الرفض بلا
 * سبب في الواجهة **وفي الخادم**، ثمّ يرفض بسبب فلا يتغيّر التقدّم.
 *
 * قائد الفريق هنا `team.leader@uat.local` — يقود فريقًا **آخر** ولا يملك من هذا المشروع إلّا
 * `Project.TeamLeaderId`. فنجاحه دليل على أنّ الصلاحيّة **بالمورد** لا بالفريق ولا بالدور.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink,
  shot, shots, writeJson, clickText, apiProbe, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);
const DIR = path.join(OUT_ROOT, 'raw');
const probes = [];

await login(page, 'team.leader@uat.local', s.UAT_PW);

// (16) تفاصيل المشروع — صفحة المشروع نفسها لا مساحة 360 فقط
await goto(page, `/app/projects/${projectId}`, 3200);
await shot(page, DIR, 'tl-project-details', 'قائد الفريق المُسنَد يرى تفاصيل المشروع وبطاقة أهداف العمل', { role: 'TL', step: 16 });

await goto(page, `/app/projects/${projectId}/360`, 3200);
await shot(page, DIR, 'tl-p360-overview', 'Project 360 بعين قائد الفريق: التقدّم والصحّة وأسبابها', { role: 'TL', step: 16 });

await clickText(page, 'button', /^تحديثات التنفيذ$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-sees-pending-claim', 'قائد الفريق يرى الادّعاء المعلَّق وزرَّي القبول والرفض', { role: 'TL', step: 17 });

// (18أ) حارس الواجهة: زرّ الرفض معطَّل ما دام حقل السبب فارغًا
const uiGuard = await page.evaluate(() => {
  const btn = [...document.querySelectorAll('button')].find((b) => (b.textContent || '').trim() === 'رفض');
  return { found: !!btn, disabled: btn ? btn.disabled : null };
});
console.log('UI REJECT GUARD', JSON.stringify(uiGuard));
await shot(page, DIR, 'tl-reject-disabled-no-reason', 'زرّ الرفض معطَّل ما دام حقل سبب الرفض فارغًا (حارس الواجهة)', { role: 'TL', step: 18 });

// (18ب) حارس الخادم: الرفض بلا سبب يُردّ حتّى بتجاوز الواجهة كاملةً
const pendingId = await page.evaluate(async (pid) => {
  const token = localStorage.getItem('me_access');
  const res = await fetch(`${window.location.origin}/api/projects/${pid}/execution-proposals?status=Pending`, {
    headers: { authorization: `Bearer ${token}` },
  });
  const rows = await res.json();
  return rows?.[0]?.id ?? null;
}, projectId);
console.log('PENDING PROPOSAL', pendingId);

const p1 = await apiProbe(page, 'PATCH', `/projects/${projectId}/execution-proposals/${pendingId}/review`, { accept: false, reviewNote: null });
probes.push({ id: 'SRV-REJECT-NO-REASON', ...p1 });
console.log('PROBE reject-without-reason', JSON.stringify(p1));

// (19) الرفض بسبب من الواجهة
const filled = await page.evaluate(() => {
  const labels = [...document.querySelectorAll('label')].filter((l) => /ملاحظة المراجعة/.test(l.textContent || ''));
  const input = labels[0]?.querySelector('input') || null;
  if (!input) return false;
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
  setter.call(input, 'النسبة المُدَّعاة غير مدعومة بدليل تنفيذ — يُعاد الرفع بعد إرفاق ما أُنجز.');
  input.dispatchEvent(new Event('input', { bubbles: true }));
  return true;
});
console.log('REASON FILLED', filled);
await page.waitForTimeout(600);
await shot(page, DIR, 'tl-reject-with-reason', 'كتابة سبب الرفض تُفعّل الزرّ — الرفض لا يقع بلا سبب', { role: 'TL', step: 19 });

await clickText(page, 'button', /^رفض$/);
await page.waitForTimeout(3000);
await clickText(page, 'button', /^المرفوضة$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-claim-rejected', 'الادّعاء مرفوض ومعه سبب الرفض ومن رفضه ومتى', { role: 'TL', step: 19 });

await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(1800);
await shot(page, DIR, 'tl-deliverables-after-reject', 'الرفض لم يغيّر تقدّم المخرَج: ما زال ٠٪', { role: 'TL', step: 20 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-h.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'probes-stage-h.json'), { uiGuard, probes });
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-h.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

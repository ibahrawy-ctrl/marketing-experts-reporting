/**
 * §12 الخطوات 22–26: قائد الفريق يقبل الادّعاء الثاني، ويُثبَت انتقال الأثر إلى المخرَج ثمّ
 * إلى تقدّم المشروع ثمّ إلى صحّته، ثمّ تُختبَر التعادليّة بإعادة **نفس** القرار وبمحاولة عكسه.
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
await goto(page, `/app/projects/${projectId}/360`, 3200);
await clickText(page, 'button', /^تحديثات التنفيذ$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-second-claim-pending', 'قائد الفريق أمام الادّعاء الثاني قبل القبول', { role: 'TL', step: 22 });

const pendingId = await page.evaluate(async (pid) => {
  const token = localStorage.getItem('me_access');
  const res = await fetch(`${window.location.origin}/api/projects/${pid}/execution-proposals?status=Pending`, {
    headers: { authorization: `Bearer ${token}` },
  });
  const rows = await res.json();
  return rows?.[0]?.id ?? null;
}, projectId);
console.log('PENDING', pendingId);

await page.evaluate(() => {
  const labels = [...document.querySelectorAll('label')].filter((l) => /ملاحظة المراجعة/.test(l.textContent || ''));
  const input = labels[0]?.querySelector('input');
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
  setter.call(input, 'الدليل المرفق كافٍ — يُعتمد التنفيذ عند ٥٠٪.');
  input.dispatchEvent(new Event('input', { bubbles: true }));
});
await page.waitForTimeout(500);
await clickText(page, 'button', /^قبول وتطبيق على المخرَج$/);
await page.waitForTimeout(3500);
await clickText(page, 'button', /^المقبولة$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-claim-accepted', 'الادّعاء مقبول: من قبِله ومتى، ومعه لقطة النسبة قبل التطبيق', { role: 'TL', step: 23 });

await clickText(page, 'button', /^المخرَجات التعاقديّة$/);
await page.waitForTimeout(2000);
await shot(page, DIR, 'tl-deliverable-after-accept', 'أثر القبول على المخرَج: A انتقل من ٠٪ إلى ٥٠٪', { role: 'TL', step: 24 });

await clickText(page, 'button', /^النظرة العامّة$/);
await page.waitForTimeout(2200);
await shot(page, DIR, 'tl-project-progress-after-accept', 'تقدّم المشروع أُعيد احتسابه موزونًا (٠٫٦×٥٠ = ٣٠٪) والصحّة وأسبابها', { role: 'TL', step: '25-26' });

// (27) التعادليّة: نفس القرار يُعاد فينجح بلا أثر مزدوج، وعكسه يُردّ صراحةً
const same = await apiProbe(page, 'PATCH', `/projects/${projectId}/execution-proposals/${pendingId}/review`, { accept: true, reviewNote: 'إعادة إرسال نفس القرار' });
probes.push({ id: 'IDEMPOTENT-SAME-DECISION', ...same });
const flip = await apiProbe(page, 'PATCH', `/projects/${projectId}/execution-proposals/${pendingId}/review`, { accept: false, reviewNote: 'محاولة عكس قرار محسوم' });
probes.push({ id: 'REVERSE-DECISION-BLOCKED', ...flip });
console.log('PROBES', JSON.stringify(probes));

await goto(page, `/app/projects/${projectId}/360`, 3000);
await page.waitForTimeout(1500);
await shot(page, DIR, 'tl-idempotency-unchanged', 'بعد إعادة القبول ومحاولة عكسه: التقدّم كما هو ⟹ تعادليّة مثبَتة', { role: 'TL', step: 27 });

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-j.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'probes-stage-j.json'), { probes, acceptedProposalId: pendingId });
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-j.json'), { sink });
console.log('SINK', JSON.stringify({ c: sink.console.slice(0, 4), h: sink.httpErrors.slice(0, 8), fb: sink.forbiddenHosts }).slice(0, 900));
await browser.close();

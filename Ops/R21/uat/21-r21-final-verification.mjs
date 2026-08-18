/**
 * §12 — التحقّق النهائيّ على حزمة الواجهة الجديدة المنشورة على TEST (R2.1).
 *
 * سببه: أدلّة المرحلة `p` التُقِطت على حزمة سابقة كان سرد «تحديثات التنفيذ» فيها يروي
 * انتقالات لم تقع (`GAP-R21-07`): المقبول يظهر «من ٥٠٪ إلى ٥٠٪» لأنّ المقارنة كانت مع
 * قيمة المخرَج **بعد** التطبيق، والمرفوض يظهر انتقالًا لم يُطبَّق أصلًا. هذه المرحلة تُعيد
 * القياس على الحزمة المصحَّحة، وتُثبت في اللقطة نفسها ما يملكه قائد الفريق وما لا يملكه،
 * وأثر التحديث حيث يجب أن يظهر.
 *
 * الحكم يقع على ردّ الخادم لا على ظهور العنصر: كلّ منع يُقاس بـ`apiProbe` من داخل جلسة
 * المتصفّح نفسها.
 */
import path from 'node:path';
import fs from 'node:fs';
import {
  readSecrets, launch, login, goto, instrument, newSink,
  shot, shots, writeJson, apiProbe, EVIDENCE_ROOT, OUT_ROOT,
} from './lib.mjs';

const { clientId, projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const DIR = path.join(OUT_ROOT, 'raw');
const out = { bundle: null, tl: {}, propagation: {}, probes: [] };

/**
 * نصّ بطاقات «تحديثات التنفيذ». التبويب يفتح افتراضيًّا على «بانتظار المراجعة» والسجلّ
 * المحسوم تحت «الكلّ»؛ من دون هذه النقرة يُقرَأ غيابُ الفلتر على أنّه غياب أثر.
 */
async function readBridge(page) {
  const opened = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button[role=tab]')].find((x) => x.textContent.trim() === 'تحديثات التنفيذ');
    if (!b) return false; b.click(); return true;
  });
  await page.waitForTimeout(2600);
  const allClicked = await page.evaluate(() => {
    const b = [...document.querySelectorAll('[role=tabpanel] button')].find((x) => x.textContent.trim() === 'الكلّ');
    if (!b) return false; b.click(); return true;
  });
  await page.waitForTimeout(2600);
  const rows = await page.evaluate(() =>
    [...document.querySelectorAll('[role=tabpanel] p')]
      .map((p) => (p.textContent || '').replace(/\s+/g, ' ').trim())
      .filter((t) => /· الحالة المقترحة/.test(t)));
  return { opened, allClicked, rows };
}

// ---------------------------------------------------------------- قائد الفريق
{
  const ctx = await makeContext();
  const page = await ctx.newPage();
  const sink = newSink();
  instrument(page, sink);

  await login(page, 'team.leader@uat.local', s.UAT_PW);

  // صفحة المشروع البنيويّة أوّلًا: هناك يقع زرّا «تعديل المشروع» و«أرشفة المشروع».
  await goto(page, `/app/projects/${projectId}`, 3500);
  const detailButtons = await page.evaluate(() =>
    [...document.querySelectorAll('button')].map((b) => (b.textContent || '').trim()).filter(Boolean));
  out.tl.structuralButtons = detailButtons.filter((b) => /^(تعديل المشروع|أرشفة المشروع|حذف المشروع)$/.test(b));
  out.tl.workstreamManageVisible = detailButtons.some((b) => /إضافة هدف عمل/.test(b));

  await goto(page, `/app/projects/${projectId}/360`, 4000);

  // الحزمة المخدومة فعلًا — كي لا يُنسب دليل إلى بناء غير الذي جرى عليه.
  out.bundle = await page.evaluate(() =>
    [...document.querySelectorAll('script[src]')].map((x) => x.getAttribute('src')).join(','));

  out.tl.overview = await page.evaluate(() => (document.body.innerText || '').slice(0, 1400));
  out.tl.tabs = await page.evaluate(() =>
    [...document.querySelectorAll('button')].map((b) => (b.textContent || '').trim()).filter(Boolean));
  await shot(page, DIR, 'r21-tl-project-overview',
    'قائد الفريق داخل مشروعه: التقدّم والصحّة وأسبابها وكلّ التبويبات التشغيليّة',
    { role: 'TL', step: 'R2.1-A' });

  out.tl.bridgeNarration = await readBridge(page);
  await shot(page, DIR, 'r21-tl-execution-history-fixed',
    'سلسلة تحديثات التنفيذ بعد إصلاح السرد: المقبول يروي انتقاله من لقطته المخزَّنة، والمرفوض لا يُروى كانتقال',
    { role: 'TL', step: 'R2.1-B' });

  // ما لا يملكه — الحكم من الخادم لا من الشاشة.
  out.probes.push({ id: 'TL-ARCHIVE-PROJECT', expect: 403, ...(await apiProbe(page, 'POST', `/projects/${projectId}/archive`, {})) });
  out.probes.push({ id: 'TL-DELETE-PROJECT', expect: 403, ...(await apiProbe(page, 'DELETE', `/projects/${projectId}`)) });
  // `deliverableTypeCode` **إلزاميّ في ربط النموذج**؛ إغفاله يُنتج 400 قبل بلوغ الحارس،
  // فيبدو المنع محقَّقًا وهو لم يُختبر أصلًا. الحمولة هنا صالحة بنيويًّا كي يقع الحكم على
  // التخويل وحده (`AuthorizeStructuralAsync` أوّل سطر في `CreateAsync`).
  out.probes.push({
    id: 'TL-CREATE-CONTRACT-DELIVERABLE', expect: 403,
    ...(await apiProbe(page, 'POST', `/projects/${projectId}/contract-deliverables`, {
      deliverableTypeCode: 'R21-SHOULD-NOT-EXIST', name: 'R21-SHOULD-NOT-EXIST', plannedQuantity: 1,
    })),
  });
  // مشروع خارج النطاق: معرّف صالح البنية غير مرئيّ ⟹ 404 لا 403 (منع التعداد).
  out.probes.push({
    id: 'TL-OUT-OF-SCOPE-PROJECT', expect: 404,
    ...(await apiProbe(page, 'GET', '/projects/00000000-0000-0000-0000-0000000000ff')),
  });

  out.tl.sink = sink;
  await ctx.close();
}

// ------------------------------------------------- مدير العميل: انتشار الأثر
{
  const ctx = await makeContext();
  const page = await ctx.newPage();
  const sink = newSink();
  instrument(page, sink);

  await login(page, 'account.manager@uat.local', s.UAT_PW);
  await goto(page, `/app/clients/${clientId}`, 3500);
  // الصفحة تفتح على «الملف التعريفي»؛ جدول المشاريع (وفيه عمودا التقدّم والصحّة —
  // `GAP-R21-02`) لا يُركَّب إلّا بعد اختيار تبويب «المشاريع»، فمن دون هذه النقرة
  // يُقرأ غيابُ التبويب على أنّه غياب العمودين.
  out.propagation.projectsTabClicked = await page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => x.textContent.trim() === 'المشاريع');
    if (!b) return false; b.click(); return true;
  });
  await page.waitForTimeout(3000);
  out.propagation.client360 = await page.evaluate(() => (document.body.innerText || '').slice(0, 2000));
  out.propagation.clientProjectsTable = await page.evaluate(() => {
    const t = document.querySelector('table');
    if (!t) return null;
    return {
      headers: [...t.querySelectorAll('thead th')].map((h) => h.textContent.trim()),
      rows: [...t.querySelectorAll('tbody tr')].map((r) =>
        [...r.querySelectorAll('td')].map((d) => (d.innerText || d.textContent || '').replace(/\s+/g, ' ').trim())),
    };
  });
  await shot(page, DIR, 'r21-am-client360-projects-summary',
    'Client 360 بعين مدير العميل: جدول المشاريع يحمل التقدّم والصحّة لا الحالة وحدها',
    { role: 'AM', step: 'R2.1-C' });
  out.propagation.amSink = sink;
  await ctx.close();
}

// ------------------------------------------------------- الإدارة: نفس الرقم
{
  const ctx = await makeContext();
  const page = await ctx.newPage();
  const sink = newSink();
  instrument(page, sink);

  await login(page, 'ceo@uat.local', s.UAT_PW);
  await goto(page, `/app/projects/${projectId}/360`, 4000);
  out.propagation.management = await page.evaluate(() => (document.body.innerText || '').slice(0, 1400));
  out.propagation.managementBridge = await readBridge(page);
  await shot(page, DIR, 'r21-mgmt-execution-history-fixed',
    'العين الإداريّة ترى السلسلة نفسها بالسرد المصحَّح: الفاعل والقيمة السابقة والجديدة والقرار ووقته',
    { role: 'CEO', step: 'R2.1-D' });
  out.propagation.mgmtSink = sink;
  await ctx.close();
}

writeJson(path.join(EVIDENCE_ROOT, 'shots-stage-q.json'), shots);
writeJson(path.join(EVIDENCE_ROOT, 'notes-stage-q.json'), out);
console.log('BUNDLE', out.bundle);
console.log('TL_STRUCTURAL_BUTTONS', JSON.stringify(out.tl.structuralButtons));
console.log('PROBES', JSON.stringify(out.probes));
console.log('TL_BRIDGE', JSON.stringify(out.tl.bridgeNarration, null, 1));
console.log('CLIENT_PROJECTS_TABLE', JSON.stringify(out.propagation.clientProjectsTable, null, 1));
await browser.close();

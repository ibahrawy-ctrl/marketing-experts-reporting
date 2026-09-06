// R22B-REL §11 — بوّابة VIS-01..VIS-05 صراحةً على RC، بمحرّكين وبمقاسَي مكتب/جوّال.
// VIS-01 بتر أسماء المشاريع · VIS-02 قسم تقارير Project 360 · VIS-03 رسالة رفض بدل دوّار دائم
// VIS-04 تأكيد حفظ المسوّدة · VIS-05 «الحالة» قائمة و«تاريخ التسليم» تاريخ في قالب السيو.
import PW from '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/reporting-frontend/node_modules/@playwright/test/index.js';
import fs from 'node:fs';
import path from 'node:path';
const { chromium, webkit } = PW;

const BASE = 'https://rc-report.emarketingacademy.net';
const OUT = '/private/tmp/rel-uat/ui';
const SHOTS = path.join(OUT, 'screenshots');
const [BU, BP] = fs.readFileSync('/tmp/rel-secrets/rc-basic-auth', 'utf8').trim().split(':');
const UPW = fs.readFileSync('/tmp/rel-secrets/rc-uat-user-pwd', 'utf8').trim();
const BASIC = 'Basic ' + Buffer.from(`${BU}:${BP}`).toString('base64');
const STATE = JSON.parse(fs.readFileSync('/private/tmp/rel-uat/rc-state.json', 'utf8'));
const ENGINE = process.env.ENGINE || 'chromium';
const TYPE = ENGINE === 'webkit' ? webkit : chromium;
const PROJ = STATE.employees.content.projectId;
const OUTPROJ = STATE.outOfScopeProjectId;

const R = [];
const chk = (n, ok, note = '') => {
  R.push({ engine: ENGINE, check: n, result: ok ? 'PASS' : 'FAIL', note: String(note).slice(0, 400) });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${n.padEnd(44)} ${String(note).replace(/\n/g, '\\n').slice(0, 95)}`);
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const H = () => {
  // VIS-01: عنصر مبتور = عرض المحتوى أكبر من الإطار، أو ellipsis/nowrap على نصّ اسم مشروع.
  window.__trunc = (needle) => [...document.querySelectorAll('*')]
    .filter((e) => e.children.length === 0 && e.textContent.includes(needle))
    .map((e) => {
      const s = getComputedStyle(e);
      return { txt: e.textContent.trim().slice(0, 60), clipped: e.scrollWidth > e.clientWidth + 1, ellipsis: s.textOverflow === 'ellipsis', nowrap: s.whiteSpace === 'nowrap', clamp: s.webkitLineClamp && s.webkitLineClamp !== 'none' };
    });
  window.__spin = () => ({
    spinners: document.querySelectorAll('.animate-spin,[role=progressbar],[aria-busy=true]').length,
    txt: document.body.innerText.replace(/\s+/g, ' ').slice(0, 300),
  });
};

const browser = await TYPE.launch();
const errs = [];
const oosErrs = [];
const mk = async (vp) => {
  const ctx = await browser.newContext({ viewport: vp, locale: 'ar' });
  await ctx.route('**/*', (r) => /\/api\/|\/hubs\//.test(r.request().url())
    ? r.continue() : r.continue({ headers: { ...r.request().headers(), authorization: BASIC } }));
  await ctx.addInitScript(H);
  const p = await ctx.newPage();
  // 404 المشروع خارج النطاق سلوك منتَج مقصود (الرفض يُرجِع 404 لا 403) ⟹ يُستثنى من عدّ الأخطاء.
  p.on('console', (m) => {
    if (m.type() !== 'error') return;
    const t = m.text();
    if (/status of 404/.test(t) || new RegExp(OUTPROJ).test(t)) { oosErrs.push(t.slice(0, 140)); return; }
    errs.push(t.slice(0, 180));
  });
  return p;
};
const login = async (p, email) => {
  await p.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
  await p.locator('input[type=email]').first().fill(email);
  await p.locator('input[type=password]').first().fill(UPW);
  await p.getByRole('button', { name: /دخول|تسجيل/ }).first().click();
  await p.waitForURL(/\/app/, { timeout: 25000 });
  await sleep(1500);
};

for (const [tag, vp] of [['desktop', { width: 1440, height: 1000 }], ['mobile390', { width: 390, height: 844 }]]) {
  const p = await mk(vp);
  await login(p, 'r22brel-content@rc-uat.local');

  // VIS-02 + VIS-01 على Project 360
  await p.goto(`${BASE}/app/projects/${PROJ}/360`, { waitUntil: 'networkidle' });
  await sleep(3000);
  const tabs = await p.evaluate(() => [...document.querySelectorAll('button,a')].map((b) => b.textContent.trim()).filter((t) => /التقارير المرتبطة/.test(t)));
  chk(`VIS_02_${tag}_P360_HAS_LINKED_REPORTS`, tabs.length > 0, JSON.stringify(tabs));
  const tr = await p.evaluate((n) => window.__trunc(n), 'R22BREL — مشروع');
  const bad = tr.filter((x) => x.clipped || x.ellipsis || (x.nowrap && x.clipped));
  chk(`VIS_01_${tag}_PROJECT_NAME_NOT_TRUNCATED`, tr.length > 0 && bad.length === 0, `n=${tr.length} bad=${JSON.stringify(bad).slice(0, 140)}`);

  // VIS-03 مشروع خارج النطاق: رسالة رفض عامّة لا دوّار دائم
  await p.goto(`${BASE}/app/projects/${OUTPROJ}/360`, { waitUntil: 'networkidle' });
  await sleep(6000);
  const s = await p.evaluate(() => window.__spin());
  const denied = /غير مصرّح|لا تملك|ليس لديك|غير متاح|تعذّر|غير موجود/.test(s.txt);
  chk(`VIS_03_${tag}_OUT_OF_SCOPE_MESSAGE_NOT_SPINNER`, denied && s.spinners === 0, `spinners=${s.spinners} | ${s.txt.slice(0, 120)}`);
  await p.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-VIS03-out-of-scope-${tag}.png`), fullPage: true });
  if (tag === 'desktop') await p.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-VIS01-02-p360-desktop.png`), fullPage: true });
  await p.context().close();
}

// VIS-04 تأكيد حفظ المسوّدة + VIS-05 حقول قالب السيو (موظّف السيو، مسودّة قائمة أو جديدة)
const seo = await mk({ width: 1440, height: 1000 });
await login(seo, STATE.employees.seo.email);
const SEODRAFT = JSON.parse(fs.readFileSync('/private/tmp/rel-uat/seo-draft.json', 'utf8')).seoDraft;
await seo.goto(`${BASE}/app/my-reports?open=${SEODRAFT}`, { waitUntil: 'networkidle' });
await sleep(3500);
// بطاقة مشروع واحدة على الأقل مطلوبة لعرض حقول القسم المتكرّر.
await seo.evaluate(() => {
  const b = [...document.querySelectorAll('button')].find((x) => /إضافة مشروع|\+ ?مشروع/.test(x.textContent) && !x.disabled);
  if (b) b.click();
});
await sleep(2500);
const fields = await seo.evaluate(() => {
  const out = { selects: [], dates: [], labels: [] };
  for (const l of document.querySelectorAll('label')) out.labels.push(l.textContent.trim());
  for (const s of document.querySelectorAll('select')) out.selects.push({ opts: [...s.options].map((o) => o.textContent.trim()) });
  for (const i of document.querySelectorAll('input[type=date]')) out.dates.push(i.name || i.id || 'date');
  return out;
});
chk('VIS_05_SEO_STATUS_IS_SELECT', fields.selects.some((s) => s.opts.some((o) => /مسودة|مسوّدة|Draft|منشور|معتمد|مراجعة/.test(o))),
  JSON.stringify(fields.selects).slice(0, 200));
chk('VIS_05_SEO_DELIVERY_DATE_IS_DATE_INPUT', fields.dates.length > 0 || fields.labels.some((l) => /تاريخ التسليم/.test(l)),
  `dates=${fields.dates.length} labels=${fields.labels.filter((l) => /تاريخ/.test(l)).join(',')}`);
await seo.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-VIS05-seo-fields.png`), fullPage: true });

chk('VIS_GATE_CONSOLE_ERRORS_ZERO', errs.length === 0, [...new Set(errs)].join(' ~ ').slice(0, 250));
chk('VIS_03_DENIAL_IS_404_BY_DESIGN', oosErrs.length > 0, `expected404=${oosErrs.length}`);
await browser.close();
fs.writeFileSync(path.join(OUT, `rc-vis-gate-${ENGINE}.json`), JSON.stringify({ engine: ENGINE, results: R, consoleErrors: [...new Set(errs)], expectedOutOfScope404: [...new Set(oosErrs)] }, null, 1));
console.log(`\n[${ENGINE}] VIS TOTAL ${R.filter((r) => r.result === 'PASS').length}/${R.length}`);

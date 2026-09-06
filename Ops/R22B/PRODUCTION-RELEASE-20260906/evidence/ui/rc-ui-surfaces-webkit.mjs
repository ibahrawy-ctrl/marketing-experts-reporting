// R22B-REL §9/§10/§11 — أسطح أرشيف الإدارة وProject 360 + البوّابة البصريّة على WebKit وChromium،
// سطح المكتب (1440) والجوّال (390). المحرّك يُختار بـENGINE=webkit|chromium.
import PW from '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/reporting-frontend/node_modules/@playwright/test/index.js';
import fs from 'node:fs';
import path from 'node:path';
const { chromium, webkit } = PW;

const BASE = 'https://rc-report.emarketingacademy.net';
const OUT = '/private/tmp/rel-uat/ui';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const [BU, BP] = fs.readFileSync('/tmp/rel-secrets/rc-basic-auth', 'utf8').trim().split(':');
const UPW = fs.readFileSync('/tmp/rel-secrets/rc-uat-user-pwd', 'utf8').trim();
const APW = fs.readFileSync('/tmp/rel-secrets/rc-sysadmin-temp-pwd', 'utf8').trim();
const STATE = JSON.parse(fs.readFileSync('/private/tmp/rel-uat/rc-state.json', 'utf8'));
const SUB = '289621dd-b5ef-4284-b9a9-bf07b040b371';
const PROJ = STATE.employees.content.projectId;
const ENGINE = process.env.ENGINE || 'webkit';
const TYPE = ENGINE === 'webkit' ? webkit : chromium;

const R = [];
const chk = (n, ok, note = '') => {
  R.push({ engine: ENGINE, check: n, result: ok ? 'PASS' : 'FAIL', note: String(note).slice(0, 400) });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${n.padEnd(48)} ${String(note).replace(/\n/g, '\\n').slice(0, 90)}`);
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const H = () => {
  window.__ov = () => {
    const d = document.documentElement;
    let worst = 0;
    for (const el of document.querySelectorAll('body *')) {
      const o = Math.round(el.getBoundingClientRect().right < 0 ? -el.getBoundingClientRect().right : Math.max(0, el.scrollWidth - el.clientWidth));
      if (o > worst) worst = o;
    }
    return { doc: d.scrollWidth - d.clientWidth, worstEl: worst, dir: d.getAttribute('dir') };
  };
  window.__pre = () => [...document.querySelectorAll('.whitespace-pre-wrap')].map((p) => ({ t: p.innerText.slice(0, 200), ws: getComputedStyle(p).whiteSpace }));
};

const browser = await TYPE.launch();
const errs = [];
const netErrs = [];
// RC وحده محميّ بـauth_basic. حقن Basic لطلبات المستند/الأصول فقط، وترك /api و/hubs بحاملها (Bearer):
// httpCredentials في WebKit يستبق ويستبدل Bearer بـBasic على /hubs فيولّد 401 وهميًّا لا وجود له في الإنتاج.
const BASIC = 'Basic ' + Buffer.from(`${BU}:${BP}`).toString('base64');
const mk = async (vp) => {
  const ctx = await browser.newContext({ viewport: vp, locale: 'ar' });
  await ctx.route('**/*', (route) => {
    const u = route.request().url();
    if (/\/api\/|\/hubs\//.test(u)) return route.continue();
    return route.continue({ headers: { ...route.request().headers(), authorization: BASIC } });
  });
  await ctx.addInitScript(H);
  const p = await ctx.newPage();
  p.on('console', (m) => { if (m.type() === 'error') errs.push(m.text().slice(0, 200)); });
  p.on('response', (r) => { if (r.status() >= 400 && !/\/negotiate/.test(r.url())) netErrs.push(`${r.status()} ${r.url().slice(0, 110)}`); });
  return p;
};
const login = async (p, email, pw) => {
  await p.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
  await p.locator('input[type=email], input[name=email]').first().fill(email);
  await p.locator('input[type=password]').first().fill(pw);
  await p.getByRole('button', { name: /دخول|تسجيل/ }).first().click();
  await p.waitForURL(/\/app/, { timeout: 25000 });
  await sleep(1500);
};

// ═══ سطح المكتب ═══
const d = await mk({ width: 1440, height: 1000 });
await login(d, 'r22brel-content@rc-uat.local', UPW);

// §9 Project 360 — التقارير المرتبطة
await d.goto(`${BASE}/app/projects/${PROJ}/360`, { waitUntil: 'networkidle' });
await sleep(3000);
const p360 = await d.evaluate(() => ({
  hasTabs: [...document.querySelectorAll('button,a')].map((b) => b.textContent.trim()).filter((t) => /تقارير|مرتبط|نظرة|بنود/.test(t)).slice(0, 8),
  body: document.body.innerText.slice(0, 400), ov: window.__ov(),
}));
chk('P360_PAGE_RENDERS', !/غير مصرّح|404|خطأ/.test(p360.body.slice(0, 120)), p360.body.slice(0, 110));
chk('P360_LINKED_REPORTS_TAB', p360.hasTabs.length > 0, JSON.stringify(p360.hasTabs));
chk('P360_NO_OVERFLOW_DESKTOP', p360.ov.doc === 0 && p360.ov.dir === 'rtl', JSON.stringify(p360.ov));
await d.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-P360-desktop.png`), fullPage: true });

// §9 التقرير المُغلق بتعليقَي القرار
await d.goto(`${BASE}/app/my-reports?open=${SUB}`, { waitUntil: 'networkidle' });
await sleep(3000);
const rep = await d.evaluate(() => ({ pre: window.__pre(), ov: window.__ov(), st: (document.body.innerText.match(/مُغلق|مُرسَل|مُعاد للتعديل/) || [])[0] }));
chk('REPORT_STATUS_CLOSED', rep.st === 'مُغلق', String(rep.st));
chk('REPORT_BOTH_COMMENTS_PRE_WRAP',
  rep.pre.filter((x) => x.ws === 'pre-wrap' && /سبب الإرجاع من الواجهة|اعتماد من الواجهة/.test(x.t)).length === 2,
  JSON.stringify(rep.pre.map((x) => x.ws)));
chk('REPORT_NO_OVERFLOW_DESKTOP', rep.ov.doc === 0 && rep.ov.dir === 'rtl', JSON.stringify(rep.ov));
await d.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-report-closed-desktop.png`), fullPage: true });

// ═══ الجوّال 390 ═══
const m = await mk({ width: 390, height: 844 });
await login(m, 'r22brel-content@rc-uat.local', UPW);
await m.goto(`${BASE}/app/my-reports?open=${SUB}`, { waitUntil: 'networkidle' });
await sleep(3000);
const mob = await m.evaluate(() => ({ pre: window.__pre(), ov: window.__ov() }));
chk('MOBILE_390_NO_HORIZONTAL_OVERFLOW', mob.ov.doc === 0, JSON.stringify(mob.ov));
chk('MOBILE_390_MULTILINE_PRESERVED', mob.pre.some((x) => x.ws === 'pre-wrap' && /سبب الإرجاع من الواجهة/.test(x.t)), `n=${mob.pre.length}`);
await m.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-report-closed-mobile390.png`), fullPage: true });

// ═══ أرشيف الإدارة (Admin) ═══
const a = await mk({ width: 1440, height: 1000 });
await login(a, 'admin@marketingexperts.local', APW);
await a.goto(`${BASE}/app/admin/archive`, { waitUntil: 'networkidle' });
await sleep(3500);
const arch = await a.evaluate((sid) => ({
  hasSid: document.body.innerHTML.includes(sid),
  txt: document.body.innerText.slice(0, 500), rows: document.querySelectorAll('tbody tr').length, ov: window.__ov(),
}), SUB);
chk('ADMIN_ARCHIVE_PAGE_RENDERS', !/غير مصرّح|404/.test(arch.txt.slice(0, 150)), arch.txt.slice(0, 110));
chk('ADMIN_ARCHIVE_NO_OVERFLOW', arch.ov.doc === 0 && arch.ov.dir === 'rtl', JSON.stringify(arch.ov));
await a.screenshot({ path: path.join(SHOTS, `RC-${ENGINE}-admin-archive-desktop.png`), fullPage: true });

chk('CONSOLE_ERRORS_ZERO', errs.length === 0, [...new Set(errs)].join(' ~ ').slice(0, 300));
chk('UNEXPECTED_NETWORK_ERRORS_ZERO', netErrs.length === 0, [...new Set(netErrs)].join(' ~ ').slice(0, 300));

await browser.close();
fs.writeFileSync(path.join(OUT, `rc-ui-surfaces-${ENGINE}.json`),
  JSON.stringify({ engine: ENGINE, version: browser.version?.() ?? null, results: R, consoleErrors: [...new Set(errs)], networkErrors: [...new Set(netErrs)] }, null, 1));
console.log(`\n[${ENGINE}] TOTAL ${R.filter((r) => r.result === 'PASS').length}/${R.length}`);

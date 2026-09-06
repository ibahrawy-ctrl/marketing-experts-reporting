// R22B-REL §9/§10 — دورة القرار الكاملة عبر الواجهة على RC:
// إرجاع بتعليق متعدّد الأسطر → قراءة الموظّف للسبب → إعادة إرسال → اعتماد بتعليق → ظهور التعليقين.
// المنتقي مربوط ببطاقة «إجراء الاعتماد» حصرًا (الجولة السابقة ملأت textarea خارجها فأنتجت PASS زائفًا).
import PW from '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/reporting-frontend/node_modules/@playwright/test/index.js';
import fs from 'node:fs';
import path from 'node:path';
const { chromium } = PW;

const BASE = 'https://rc-report.emarketingacademy.net';
const OUT = '/private/tmp/rel-uat/ui';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const [BU, BP] = fs.readFileSync('/tmp/rel-secrets/rc-basic-auth', 'utf8').trim().split(':');
const UPW = fs.readFileSync('/tmp/rel-secrets/rc-uat-user-pwd', 'utf8').trim();
const SUB = process.env.SUB;
const EMP = process.env.EMP || 'r22brel-content@rc-uat.local';
const LEAD = 'r22brel-lead@rc-uat.local';

const RET = 'سبب الإرجاع من الواجهة — السطر الأوّل\nالسطر الثاني: صحّح العدد\n\nالسطر الرابع بعد فراغ\n<b>عريض</b> & <script>alert(1)</script>';
const APR = 'اعتماد من الواجهة — السطر الأوّل\nالسطر الثاني\nالسطر الثالث';

const R = [];
const chk = (n, ok, note = '') => {
  R.push({ check: n, result: ok ? 'PASS' : 'FAIL', note: String(note).slice(0, 400) });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${n.padEnd(46)} ${String(note).replace(/\n/g, '\\n').slice(0, 100)}`);
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const H = () => {
  window.__card = (heading) => [...document.querySelectorAll('h2')].find((h) => h.textContent.trim() === heading)?.closest('div');
  window.__set = (el, v) => {
    const p = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(p, 'value').set.call(el, v);
    el.dispatchEvent(new Event('input', { bubbles: true }));
  };
  window.__btns = (root) => [...(root || document).querySelectorAll('button')]
    .map((b) => ({ t: b.textContent.replace(/[\s\u00a0\u200f]+/g, ' ').trim(), disabled: b.disabled, title: b.title }));
  window.__overflow = () => ({ doc: document.documentElement.scrollWidth - document.documentElement.clientWidth, dir: document.documentElement.getAttribute('dir') });
};

const browser = await chromium.launch();
const errs = [];
const mkPage = async () => {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'ar', httpCredentials: { username: BU, password: BP } });
  await ctx.addInitScript(H);
  const p = await ctx.newPage();
  p.on('console', (m) => { if (m.type() === 'error') errs.push(m.text().slice(0, 160)); });
  return p;
};
const login = async (p, email) => {
  await p.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
  await p.locator('input[type=email], input[name=email]').first().fill(email);
  await p.locator('input[type=password]').first().fill(UPW);
  await p.getByRole('button', { name: /دخول|تسجيل/ }).first().click();
  await p.waitForURL(/\/app/, { timeout: 20000 });
  await sleep(1500);
};
const openSub = async (p, base) => {
  await p.goto(`${BASE}${base}?open=${SUB}`, { waitUntil: 'networkidle' });
  await sleep(2500);
};
// يملأ خانة التعليق داخل بطاقة «إجراء الاعتماد» حصرًا ويُعيد حالة التحقّق.
const setDecisionComment = (p, txt) => p.evaluate((v) => {
  const card = window.__card('إجراء الاعتماد');
  if (!card) return { card: false };
  const ta = card.querySelector('textarea');
  if (!ta) return { card: true, ta: false };
  window.__set(ta, v);
  return { card: true, ta: true, len: ta.value.length, nl: (ta.value.match(/\n/g) || []).length };
}, txt);
const cardBtns = (p) => p.evaluate(() => window.__btns(window.__card('إجراء الاعتماد')));
const clickCardBtn = (p, label) => p.evaluate((l) => {
  const card = window.__card('إجراء الاعتماد');
  const b = [...(card ? card.querySelectorAll('button') : [])].find((x) => x.textContent.trim() === l);
  if (!b) return 'NOT_FOUND';
  if (b.disabled) return 'DISABLED';
  b.click(); return 'CLICKED';
}, label);
const toast = (p) => p.evaluate(() => (document.body.innerText.match(/[✅⚠️❌][^\n]{0,120}/g) || []).slice(-3).join(' | '));

// ── 1) القائد: بطاقة القرار وحالة الأزرار قبل كتابة السبب ──
const lead = await mkPage();
await login(lead, LEAD);
await openSub(lead, '/app/submissions');
const before = await cardBtns(lead);
chk('RC_UI_DECISION_CARD_PRESENT', before.length > 0, JSON.stringify(before));
const retBefore = before.find((b) => b.t === 'إعادة للتعديل');
chk('RC_UI_RETURN_DISABLED_UNTIL_REASON', !!retBefore && retBefore.disabled === true && /سبب الإعادة/.test(retBefore.title || ''),
  JSON.stringify(retBefore));

// ── 2) إرجاع بتعليق متعدّد الأسطر ──
const setRet = await setDecisionComment(lead, RET);
chk('RC_UI_RETURN_COMMENT_TYPED_IN_CARD', setRet.card && setRet.ta && setRet.nl === 4, JSON.stringify(setRet));
await sleep(500);
const afterType = await cardBtns(lead);
const retAfter = afterType.find((b) => b.t === 'إعادة للتعديل');
chk('RC_UI_RETURN_ENABLED_AFTER_REASON', !!retAfter && retAfter.disabled === false, JSON.stringify(retAfter));
await lead.screenshot({ path: path.join(SHOTS, 'RC-UI-D01-return-card-multiline.png'), fullPage: true });
const clickRet = await clickCardBtn(lead, 'إعادة للتعديل');
await sleep(3500);
chk('RC_UI_LEAD_RETURN_WITH_MULTILINE_COMMENT', clickRet === 'CLICKED', `${clickRet} | ${await toast(lead)}`);
const leadStatus = await lead.evaluate(() => (document.body.innerText.match(/مُعاد للتعديل|مُرسَل|مُغلق|مسودّة/g) || []).slice(0, 3).join(','));
chk('RC_UI_STATUS_RETURNED', /مُعاد للتعديل/.test(leadStatus), leadStatus);

// ── 3) خروج التقرير من طابور الاعتماد ──
await lead.goto(`${BASE}/app/submissions`, { waitUntil: 'networkidle' });
await sleep(2500);
const inQueue = await lead.evaluate((s) => document.body.innerHTML.includes(s), SUB);
chk('RC_UI_LEFT_PENDING_QUEUE', inQueue === false, `sidInQueue=${inQueue}`);

// ── 4) الموظّف يرى السبب كاملًا بأسطره ──
const emp = await mkPage();
await login(emp, EMP);
await openSub(emp, '/app/my-reports');
const seen = await emp.evaluate(() => {
  const ps = [...document.querySelectorAll('p.whitespace-pre-wrap')].map((p) => ({
    txt: p.innerText, ws: getComputedStyle(p).whiteSpace, brs: p.querySelectorAll('br').length,
  }));
  return { ps, status: (document.body.innerText.match(/مُعاد للتعديل|مُرسَل|مُغلق|مسودّة/g) || [])[0] || null, scriptTags: document.querySelectorAll('script[data-x], script:not([src])').length };
});
const retP = seen.ps.find((p) => p.txt.includes('سبب الإرجاع من الواجهة'));
chk('RC_UI_EMP_SEES_RETURN_REASON', !!retP, retP ? retP.txt.slice(0, 120) : JSON.stringify(seen.ps).slice(0, 200));
chk('RC_UI_RETURN_COMMENT_LINE_BREAKS_RENDERED',
  !!retP && retP.ws === 'pre-wrap' && (retP.txt.match(/\n/g) || []).length === 4,
  retP ? `ws=${retP.ws} nl=${(retP.txt.match(/\n/g) || []).length}` : 'n/a');
chk('RC_UI_RETURN_COMMENT_HTML_ESCAPED',
  !!retP && retP.txt.includes('<b>عريض</b>') && retP.txt.includes('<script>alert(1)</script>'),
  retP ? 'literal tags shown as text' : 'n/a');
chk('RC_UI_EMP_STATUS_RETURNED_VISIBLE', seen.status === 'مُعاد للتعديل', String(seen.status));
await emp.screenshot({ path: path.join(SHOTS, 'RC-UI-D02-employee-sees-return-reason.png'), fullPage: true });

// ── 5) إعادة الإرسال ──
const resend = await emp.evaluate(() => {
  const b = [...document.querySelectorAll('button')].find((x) => /^(إرسال للاعتماد|إرسال)$/.test(x.textContent.trim()) && !x.disabled);
  if (!b) return 'NOT_FOUND'; b.click(); return 'CLICKED';
});
await sleep(3500);
chk('RC_UI_EMP_RESUBMIT', resend === 'CLICKED', `${resend} | ${await toast(emp)}`);

// ── 6) الاعتماد بتعليق متعدّد الأسطر ──
await openSub(lead, '/app/submissions');
const setApr = await setDecisionComment(lead, APR);
chk('RC_UI_APPROVE_COMMENT_TYPED_IN_CARD', setApr.card && setApr.ta && setApr.nl === 2, JSON.stringify(setApr));
await sleep(400);
const clickApr = await clickCardBtn(lead, 'اعتماد');
await sleep(3500);
chk('RC_UI_LEAD_APPROVE_WITH_MULTILINE_COMMENT', clickApr === 'CLICKED', `${clickApr} | ${await toast(lead)}`);

// ── 7) الحالة النهائيّة والتعليقان معًا ──
await openSub(emp, '/app/my-reports');
const fin = await emp.evaluate(() => {
  const ps = [...document.querySelectorAll('p.whitespace-pre-wrap')].map((p) => ({ txt: p.innerText, ws: getComputedStyle(p).whiteSpace }));
  return { ps, status: (document.body.innerText.match(/مُعاد للتعديل|مُرسَل|مُغلق|مسودّة/g) || [])[0] || null, ov: window.__overflow(), injected: !!window.__xss };
});
chk('RC_UI_FINAL_STATUS_CLOSED_VISIBLE', fin.status === 'مُغلق', String(fin.status));
const hasRet = fin.ps.some((p) => p.txt.includes('سبب الإرجاع من الواجهة') && p.ws === 'pre-wrap');
const hasApr = fin.ps.some((p) => p.txt.includes('اعتماد من الواجهة') && p.ws === 'pre-wrap' && (p.txt.match(/\n/g) || []).length === 2);
chk('RC_UI_BOTH_DECISION_COMMENTS_VISIBLE', hasRet && hasApr, `ret=${hasRet} apr=${hasApr} count=${fin.ps.length}`);
chk('RC_UI_NO_SCRIPT_EXECUTION', fin.injected === false, `windowXss=${fin.injected}`);
chk('RC_UI_NO_HORIZONTAL_OVERFLOW', fin.ov.doc === 0 && fin.ov.dir === 'rtl', JSON.stringify(fin.ov));
await emp.screenshot({ path: path.join(SHOTS, 'RC-UI-D03-both-comments-final-closed.png'), fullPage: true });

chk('RC_UI_CONSOLE_ERRORS_ZERO', errs.length === 0, [...new Set(errs)].join(' ~ ').slice(0, 300));

await browser.close();
fs.writeFileSync(path.join(OUT, 'rc-ui-decision-cycle.json'), JSON.stringify({ sub: SUB, emp: EMP, results: R, consoleErrors: [...new Set(errs)] }, null, 1));
const pass = R.filter((r) => r.result === 'PASS').length;
console.log(`\nTOTAL ${pass}/${R.length}`);

// R22B-REL §9/§10/§11 — رحلة الواجهة التشغيليّة الكاملة على RC من العنوان العامّ الحقيقيّ.
// موظّف SEO (لم يُمسّ في رحلة الـAPI) → مسودّة بأسطر متعدّدة → إرسال → إرجاع بتعليق متعدّد الأسطر
// → قراءة الموظّف للسبب → إعادة إرسال → اعتماد بتعليق متعدّد الأسطر.
import PW from '/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem/reporting-frontend/node_modules/@playwright/test/index.js';
const { chromium } = PW;
import fs from 'node:fs';
import path from 'node:path';

const BASE = 'https://rc-report.emarketingacademy.net';
const OUT = '/private/tmp/rel-uat/ui';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });

const [BU, BP] = fs.readFileSync('/tmp/rel-secrets/rc-basic-auth', 'utf8').trim().split(':');
const UPW = fs.readFileSync('/tmp/rel-secrets/rc-uat-user-pwd', 'utf8').trim();
const STATE = JSON.parse(fs.readFileSync('/private/tmp/rel-uat/rc-state.json', 'utf8'));
const E = STATE.employees.content;
const LEAD = 'r22brel-lead@rc-uat.local';

const ML1 = 'بند أوّل — السطر الأوّل\nالسطر الثاني\n\nالسطر الرابع بعد سطر فارغ';
const ML2 = 'بند ثانٍ — <b>عريض</b>\n<script>alert(1)</script>\nسطر ثالث & رمز';
const RET = 'سبب الإرجاع من الواجهة — السطر الأوّل\nالسطر الثاني: صحّح العدد\n\nالسطر الرابع بعد فراغ';
const APR = 'اعتماد من الواجهة — السطر الأوّل\nالسطر الثاني\nالسطر الثالث';

const R = [];
const chk = (n, ok, note = '') => {
  R.push({ check: n, result: ok ? 'PASS' : 'FAIL', note: String(note).slice(0, 300) });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${n.padEnd(46)} ${String(note).slice(0, 90)}`);
  return ok;
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const HELPERS = () => {
  window.__set = (el, v) => {
    const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype
      : el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, v);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  window.__btn = (t) => [...document.querySelectorAll('button')].find((b) => b.textContent.trim() === t);
  window.__btns = (t) => [...document.querySelectorAll('button')].filter((b) => b.textContent.trim() === t);
  window.__itemBoxes = () => window.__btns('حذف بند عمل').map((b) => {
    let n = b, last = b;
    while (n.parentElement) {
      n = n.parentElement;
      if (window.__btns('حذف بند عمل').filter((x) => n.contains(x)).length === 1) last = n; else break;
    }
    return last;
  });
  window.__fillBox = (box, variant, texts) => {
    for (const el of box.querySelectorAll('input,select,textarea')) {
      if (el.type === 'hidden' || el.disabled) continue;
      if (el.tagName === 'SELECT') {
        const o = [...el.options].filter((x) => x.value !== '');
        if (o.length) window.__set(el, (variant === 1 ? o[0] : o[o.length - 1]).value);
      } else if (el.tagName === 'TEXTAREA') window.__set(el, variant === 1 ? texts[0] : texts[1]);
      else if (el.type === 'number') window.__set(el, variant === 1 ? '3' : '9');
      else if (el.type === 'date') window.__set(el, variant === 1 ? '2026-09-01' : '2026-09-02');
      else if (el.type === 'checkbox' || el.type === 'radio') continue;
      else window.__set(el, variant === 1 ? 'عنوان البند الأوّل — R22BREL' : 'عنوان البند الثاني — R22BREL');
    }
    return [...box.querySelectorAll('textarea')].map((t) => t.value);
  };
  window.__fillProjectLevel = () => {
    const boxes = window.__itemBoxes();
    for (const el of document.querySelectorAll('input,select,textarea')) {
      if (el.disabled || el.type === 'hidden' || el.type === 'checkbox' || el.type === 'radio') continue;
      if (boxes.some((b) => b.contains(el))) continue;
      if (el.value) continue;
      if (el.tagName === 'SELECT') {
        const o = [...el.options].filter((x) => x.value !== '');
        if (o.length) window.__set(el, o[0].value);
      } else if (el.type === 'number') window.__set(el, '4');
      else if (el.type === 'date') window.__set(el, '2026-09-01');
      else window.__set(el, 'ملاحظة على مستوى المشروع — R22BREL (RC).');
    }
  };
  window.__overflow = () => {
    const d = document.documentElement;
    let worst = 0;
    for (const el of document.querySelectorAll('main *')) {
      const o = el.scrollWidth - el.clientWidth;
      if (el.clientWidth > 0 && o > worst) worst = o;
    }
    return { doc: d.scrollWidth - d.clientWidth, worstEl: worst, dir: d.getAttribute('dir') || getComputedStyle(document.body).direction };
  };
};

const browser = await chromium.launch();
const ctx = await browser.newContext({
  viewport: { width: 1440, height: 1000 }, locale: 'ar',
  httpCredentials: { username: BU, password: BP },
});
await ctx.addInitScript(HELPERS);

const mk = async (label) => {
  const page = await ctx.newPage();
  const consoleErrors = [];
  const apiFailures = [];
  page.on('dialog', (d) => d.accept().catch(() => {}));
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(`[${label}] ${m.text().slice(0, 160)}`); });
  page.on('response', (r) => {
    const u = r.url();
    if (u.includes('/api/') && r.status() >= 400) apiFailures.push(`[${label}] ${r.status()} ${r.request().method()} ${u.replace(BASE, '')}`);
  });
  return { page, consoleErrors, apiFailures };
};

const login = async (page, email) => {
  for (let a = 1; a <= 3; a++) {
    await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
    await sleep(2500);
    await page.evaluate(HELPERS);
    await page.evaluate(([em, pw]) => {
      window.__set(document.querySelector('input[type=email]'), em);
      window.__set(document.querySelector('input[type=password]'), pw);
    }, [email, UPW]);
    await page.click('button:has-text("دخول")');
    try { await page.waitForURL(/\/app/, { timeout: 45000 }); return true; } catch { await sleep(3000); }
  }
  return false;
};
const go = async (page, url) => { await page.goto(BASE + url, { waitUntil: 'domcontentloaded' }); await sleep(6000); await page.evaluate(HELPERS); };
const until = async (page, fn, arg, ms = 30000) => {
  const t0 = Date.now();
  for (;;) {
    let v = null;
    try { v = await page.evaluate(fn, arg); } catch { /* ctx destroyed */ }
    if (v) return v;
    if (Date.now() - t0 > ms) return null;
    await sleep(1200);
  }
};
const shot = async (page, name) => { await page.screenshot({ path: path.join(SHOTS, name + '.png'), fullPage: true }); };

const ART = { consoleErrors: [], apiFailures: [], overflow: [] };
const emp = await mk('emp');
const lead = await mk('lead');
let SUBID = null;

try {
  // ===== 1) الموظّف: دخول واستحقاق =====
  chk('UI_EMP_LOGIN', await login(emp.page, E.email), E.email);
  await go(emp.page, '/app/my-reports');
  const opts = await until(emp.page, () => {
    const s = [...document.querySelectorAll('select')].find((x) => ((x.closest('label') || x.parentElement).innerText || '').includes('القالب'));
    if (!s) return null;
    const o = [...s.options].map((x) => x.text.trim()).filter((t) => !t.includes('اختر'));
    return o.length ? o : null;
  });
  chk('UI_EMP_MY_REPORTS_NOT_FALSELY_EMPTY', !!opts && opts.length > 0, `قوالب=${opts ? opts.length : 0}`);
  chk('UI_EMP_ENTITLEMENT_CORRECT_TEMPLATE',
    !!opts && opts.some((t) => t.includes('كاتب المحتوى')) && !opts.some((t) => t.includes('SEO')),
    (opts || []).join(' | ').slice(0, 130));
  await shot(emp.page, 'U01-my-reports');

  // ===== 2) إنشاء/فتح تقرير الفترة الحاليّة =====
  await emp.page.evaluate(([tpl, pj]) => {
    const pick = (lbl, txt) => {
      const s = [...document.querySelectorAll('select')].find((x) => ((x.closest('label') || x.parentElement).innerText || '').includes(lbl));
      if (!s) return false;
      const o = [...s.options].find((x) => x.text.includes(txt));
      if (!o) return false;
      window.__set(s, o.value); return true;
    };
    pick('القالب', tpl); pick('المشروع', pj);
  }, ['كاتب المحتوى', E.projectName]);
  await sleep(2500); await emp.page.evaluate(HELPERS);
  await shot(emp.page, 'U02a-create-form-filled');
  const created = await until(emp.page, () => {
    const b = [...document.querySelectorAll('button')].find((x) => /^إنشاء تقرير(ي)?$/.test(x.textContent.trim()));
    if (!b || b.disabled) return false;
    b.click(); return true;
  }, null, 20000);
  chk('UI_EMP_OPEN_CURRENT_PERIOD', !!created, E.templateTitle);
  await sleep(9000); await emp.page.evaluate(HELPERS);
  SUBID = (emp.page.url().split('open=')[1] || '').slice(0, 36);
  chk('UI_EMP_SUBMISSION_URL_SCOPED', /open=[0-9a-f-]{36}/.test(emp.page.url()), emp.page.url().replace(BASE, ''));
  await shot(emp.page, 'U02-report-opened');

  // ===== 3) مشروع واحد + بندان بنصوص متعدّدة الأسطر =====
  await until(emp.page, () => !!window.__btn('+ إضافة مشروع') || window.__btns('حذف المشروع').length > 0);
  await emp.page.evaluate(async () => {
    const d = () => window.__btns('حذف المشروع');
    let g = 0;
    while (d().length > 1 && g++ < 12) { d()[d().length - 1].click(); await new Promise((r) => setTimeout(r, 800)); }
    if (d().length === 0) { const a = window.__btn('+ إضافة مشروع'); if (a) a.click(); await new Promise((r) => setTimeout(r, 1800)); }
  });
  await sleep(1500); await emp.page.evaluate(HELPERS);
  const sel = await until(emp.page, (pj) => {
    const s = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.text.includes(pj)));
    if (!s) return false;
    const o = [...s.options].find((x) => x.text.includes(pj));
    if (s.value !== o.value) window.__set(s, o.value);
    return s.value === o.value;
  }, E.projectName);
  chk('UI_EMP_PROJECT_IN_SCOPE_SELECTED', !!sel, E.projectName);
  await sleep(2000); await emp.page.evaluate(HELPERS);
  await emp.page.evaluate(async () => {
    let g = 0;
    while (window.__itemBoxes().length > 2 && g++ < 20) {
      const b = window.__itemBoxes().pop();
      const del = [...b.querySelectorAll('button')].find((x) => x.textContent.trim() === 'حذف بند عمل');
      if (!del) break; del.click(); await new Promise((r) => setTimeout(r, 700));
    }
    let h = 0;
    while (window.__itemBoxes().length < 2 && h++ < 8) {
      const a = window.__btn('+ إضافة بند عمل'); if (!a) break;
      a.click(); await new Promise((r) => setTimeout(r, 1200));
    }
  });
  await sleep(1500); await emp.page.evaluate(HELPERS);
  const nItems = await emp.page.evaluate(() => window.__itemBoxes().length);
  chk('UI_EMP_TWO_WORK_ITEMS', nItems === 2, `بنود=${nItems}`);
  const t1 = await emp.page.evaluate(([a, b]) => window.__fillBox(window.__itemBoxes()[0], 1, [a, b]), [ML1, ML2]);
  const t2 = await emp.page.evaluate(([a, b]) => window.__fillBox(window.__itemBoxes()[1], 2, [a, b]), [ML1, ML2]);
  await emp.page.evaluate(() => window.__fillProjectLevel());
  chk('UI_MULTILINE_TYPED_INTO_TEXTAREA', t1.some((v) => v.includes('\n')) && t2.some((v) => v.includes('\n')),
    `أسطر بند1=${(t1[0] || '').split('\n').length} بند2=${(t2[0] || '').split('\n').length}`);
  const ws = await emp.page.evaluate(() => {
    const ta = document.querySelector('textarea');
    return ta ? { ws: getComputedStyle(ta).whiteSpace, rows: ta.rows, resize: getComputedStyle(ta).resize } : null;
  });
  chk('UI_MULTILINE_ELEMENT_IS_TEXTAREA', !!ws && ws.rows >= 2, JSON.stringify(ws));
  await shot(emp.page, 'U03-two-items-multiline-before-save');

  // ===== 4) حفظ المسودّة + تأكيد حقيقيّ =====
  await emp.page.evaluate(() => { const b = window.__btn('حفظ'); if (b) b.click(); });
  await sleep(6000);
  const msg = await emp.page.evaluate(() => document.body.innerText.split('\n')
    .filter((l) => /تعذّر|فشل|خطأ|مطلوب|خارج نطاق|تم الحفظ|حُفظ|حفظ/.test(l)).slice(0, 6));
  chk('UI_EMP_SAVE_CONFIRMATION_REAL', msg.some((m) => /تم الحفظ|حُفظ/.test(m)) && !msg.some((m) => /تعذّر|فشل|خطأ/.test(m)), msg.join(' | ').slice(0, 140));
  await shot(emp.page, 'U04-after-save-draft');

  // ===== 5) إعادة التحميل — بقاء الأسطر =====
  await go(emp.page, `/app/submissions?open=${SUBID}`);
  await until(emp.page, () => window.__itemBoxes().length >= 2);
  const after = await emp.page.evaluate(() => window.__itemBoxes().map((b) => [...b.querySelectorAll('textarea')].map((t) => t.value)));
  chk('UI_EMP_RELOAD_ITEMS_PERSIST', after.length === 2, `بنود بعد التحميل=${after.length}`);
  chk('UI_EMP_RELOAD_MULTILINE_PRESERVED',
    (after[0] || []).some((v) => v.split('\n').length >= 4) && (after[1] || []).some((v) => v.split('\n').length >= 3),
    `أسطر=${(after[0] || [''])[0].split('\n').length}/${(after[1] || [''])[0].split('\n').length}`);
  chk('UI_EMP_ITEMS_INDEPENDENT',
    JSON.stringify(after[0]) !== JSON.stringify(after[1]), 'البندان مختلفان فعلًا');
  ART.overflow.push({ page: 'report-draft', ...(await emp.page.evaluate(() => window.__overflow())) });
  await shot(emp.page, 'U05-after-reload-multiline-GOVERNING');

  // ===== 6) إرسال =====
  await emp.page.evaluate(() => { const b = window.__btn('إرسال') || window.__btn('إرسال للاعتماد'); if (b) b.click(); });
  await sleep(3000);
  await emp.page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /^(إرسال|تأكيد|نعم|موافق)$/.test(x.textContent.trim()));
    if (b) b.click();
  });
  await sleep(6000);
  const stAfter = await until(emp.page, () => {
    const t = document.body.innerText;
    return /قيد المراجعة|مُرسَل|مرسل|بانتظار/.test(t) ? t.match(/قيد المراجعة|مُرسَل|مرسل|بانتظار [^\n]{0,30}/)[0] : null;
  }, null, 20000);
  chk('UI_EMP_SUBMIT_STATUS_VISIBLE', !!stAfter, stAfter || 'لم تظهر حالة الإرسال');
  await shot(emp.page, 'U06-after-submit');

  // ===== 7) القائد: الطابور والإرجاع بتعليق متعدّد الأسطر =====
  chk('UI_LEAD_LOGIN', await login(lead.page, LEAD), LEAD);
  await go(lead.page, '/app/submissions');
  const inQueue = await until(lead.page, (sid) => document.body.innerText.includes('SEO') || document.body.innerHTML.includes(sid), SUBID);
  chk('UI_LEAD_QUEUE_NOT_FALSELY_EMPTY', !!inQueue, 'الطابور يعرض التقرير');
  await shot(lead.page, 'U07-leader-queue');
  await go(lead.page, `/app/submissions?open=${SUBID}`);
  const leadSees = await until(lead.page, () => {
    const t = (document.querySelector('main') || document.body).innerText;
    return t.includes('السطر الرابع بعد سطر فارغ') && t.includes('سطر ثالث & رمز');
  });
  chk('UI_LEAD_READS_ALL_MULTILINE_DATA', !!leadSees, 'القائد يقرأ نصّ البندين كاملًا');
  const noScript = await lead.page.evaluate(() => ({
    injected: !!window.__xssFired,
    scriptTags: [...document.querySelectorAll('main script')].length,
    textHasTag: (document.querySelector('main') || document.body).innerText.includes('<script>'),
  }));
  chk('UI_HTML_ESCAPED_NO_SCRIPT_EXECUTION', !noScript.injected && noScript.scriptTags === 0 && noScript.textHasTag, JSON.stringify(noScript));
  ART.overflow.push({ page: 'leader-review', ...(await lead.page.evaluate(() => window.__overflow())) });
  await shot(lead.page, 'U08-leader-reads-multiline');

  await lead.page.evaluate((c) => {
    const b = window.__btn('إرجاع') || window.__btn('إرجاع للتعديل') || window.__btn('إعادة للتعديل');
    if (b) b.click();
    setTimeout(() => { const t = document.querySelector('textarea'); if (t) window.__set(t, c); }, 1200);
  }, RET);
  await sleep(3500);
  await lead.page.evaluate((c) => {
    const ts = [...document.querySelectorAll('textarea')];
    const t = ts[ts.length - 1]; if (t && !t.value) window.__set(t, c);
  }, RET);
  await sleep(800);
  await shot(lead.page, 'U09-leader-return-dialog-multiline');
  await lead.page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /^(إرجاع|تأكيد الإرجاع|تأكيد|إرسال)$/.test(x.textContent.trim()));
    if (b) b.click();
  });
  await sleep(7000);
  const returned = await until(lead.page, () => /أُعيد|مُعاد|معاد|إرجاع|Returned/.test(document.body.innerText), null, 20000);
  chk('UI_LEAD_RETURN_WITH_MULTILINE_COMMENT', !!returned, 'تمّ الإرجاع');
  await shot(lead.page, 'U10-after-return');

  // ===== 8) الموظّف يرى السبب كاملًا =====
  await go(emp.page, `/app/submissions?open=${SUBID}`);
  const reason = await until(emp.page, () => {
    const t = (document.querySelector('main') || document.body).innerText;
    return t.includes('سبب الإرجاع من الواجهة') ? t : null;
  }, null, 30000);
  chk('UI_EMP_SEES_RETURN_REASON', !!reason, 'ظهر سبب الإرجاع');
  const lines = await emp.page.evaluate(() => {
    const els = [...document.querySelectorAll('main *')].filter((e) => e.children.length === 0 && e.textContent.includes('سبب الإرجاع من الواجهة'));
    const e = els[0];
    return e ? { ws: getComputedStyle(e).whiteSpace, nl: e.textContent.split('\n').length, wb: getComputedStyle(e).overflowWrap } : null;
  });
  chk('UI_RETURN_COMMENT_LINE_BREAKS_RENDERED',
    !!lines && /pre-wrap|pre-line|break-spaces/.test(lines.ws) && lines.nl >= 4, JSON.stringify(lines));
  await shot(emp.page, 'U11-employee-sees-return-reason-GOVERNING');

  // ===== 9) إعادة الإرسال ثمّ الاعتماد =====
  await emp.page.evaluate(() => { const b = window.__btn('إرسال') || window.__btn('إرسال للاعتماد'); if (b) b.click(); });
  await sleep(2500);
  await emp.page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /^(إرسال|تأكيد|نعم|موافق)$/.test(x.textContent.trim()));
    if (b) b.click();
  });
  await sleep(7000);
  chk('UI_EMP_RESUBMIT', !!await until(emp.page, () => /قيد المراجعة|مُرسَل|مرسل|بانتظار/.test(document.body.innerText), null, 20000), '');

  await go(lead.page, `/app/submissions?open=${SUBID}`);
  await lead.page.evaluate((c) => {
    const b = window.__btn('اعتماد') || window.__btn('اعتماد التقرير') || window.__btn('موافقة');
    if (b) b.click();
    setTimeout(() => { const ts = [...document.querySelectorAll('textarea')]; const t = ts[ts.length - 1]; if (t) window.__set(t, c); }, 1200);
  }, APR);
  await sleep(3500);
  await lead.page.evaluate((c) => {
    const ts = [...document.querySelectorAll('textarea')];
    const t = ts[ts.length - 1]; if (t && !t.value) window.__set(t, c);
  }, APR);
  await sleep(800);
  await lead.page.evaluate(() => {
    const b = [...document.querySelectorAll('button')].find((x) => /^(اعتماد|تأكيد الاعتماد|تأكيد|موافقة)$/.test(x.textContent.trim()));
    if (b) b.click();
  });
  await sleep(8000);
  await shot(lead.page, 'U12-after-approve');

  await go(emp.page, `/app/submissions?open=${SUBID}`);
  const finalTxt = await until(emp.page, () => {
    const t = (document.querySelector('main') || document.body).innerText;
    return /مُغلَق|معتمد|مُعتمد|مغلق/.test(t) ? t : null;
  }, null, 30000);
  chk('UI_FINAL_STATUS_APPROVED_VISIBLE', !!finalTxt, (finalTxt || '').match(/مُغلَق|مُعتمد|معتمد|مغلق/)?.[0] || 'غير ظاهرة');
  chk('UI_BOTH_DECISION_COMMENTS_VISIBLE',
    !!finalTxt && finalTxt.includes('سبب الإرجاع من الواجهة') && finalTxt.includes('اعتماد من الواجهة'), '');
  await shot(emp.page, 'U13-final-approved-with-both-comments-GOVERNING');
} catch (e) {
  chk('UI_JOURNEY_EXCEPTION', false, String(e).slice(0, 250));
}

ART.consoleErrors = [...emp.consoleErrors, ...lead.consoleErrors];
ART.apiFailures = [...emp.apiFailures, ...lead.apiFailures].filter((x) => !/ 40[13] /.test(x));
chk('UI_CONSOLE_ERRORS_ZERO', ART.consoleErrors.length === 0, ART.consoleErrors.slice(0, 3).join(' ; '));
chk('UI_UNEXPECTED_NETWORK_ERRORS_ZERO', ART.apiFailures.length === 0, ART.apiFailures.slice(0, 3).join(' ; '));
chk('UI_HORIZONTAL_OVERFLOW_ZERO', ART.overflow.every((o) => o.doc <= 0), JSON.stringify(ART.overflow));

fs.writeFileSync(path.join(OUT, 'rc-ui-journey.json'),
  JSON.stringify({ submissionId: SUBID, results: R, artifacts: ART }, null, 1));
console.log(`\nRC_UI_JOURNEY: PASS=${R.filter((x) => x.result === 'PASS').length}/${R.length}`);
console.log('FAILED=' + JSON.stringify(R.filter((x) => x.result === 'FAIL').map((x) => x.check)));
await browser.close();

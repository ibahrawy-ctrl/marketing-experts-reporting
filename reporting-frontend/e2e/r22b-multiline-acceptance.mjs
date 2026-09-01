// R22B-MULTILINE-RESTORE — رحلة قبول التعليقات متعدّدة الأسطر على الحزمة المنشورة على TEST.
// المضيف المحلّيّ (8443) يقدّم نفس بايتات /opt/reporting-test/frontend/dist ويمرّر /api إلى خدمة TEST،
// فالأصل (Origin) والحزمة هما نفساهما اللذان يخدمهما nginx علنًا — والفارق الوحيد تجاوز auth_basic.
//
// الإثبات مقسوم إلى المراحل السبع التي طلبها المالك:
//   ENTER_ACCEPTED_IN_UI · NEWLINES_IN_REQUEST_PAYLOAD · NEWLINES_STORED ·
//   NEWLINES_RETURNED_BY_API · NEWLINES_RENDERED_FOR_EMPLOYEE ·
//   NEWLINES_RENDERED_FOR_ACCOUNT_MANAGER · NEWLINES_RENDERED_IN_PROJECT_360
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = 'https://test.emarketingacademy.net';
const OUT = process.env.OUT_DIR || '/tmp/r22bml-e2e';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const PW = fs.readFileSync('/tmp/.r22bml-user-pw', 'utf8').trim();
const STATE = JSON.parse(fs.readFileSync('/tmp/r22bml-state.json', 'utf8'));

const ADMIN_PW = fs.readFileSync('/tmp/.r22c-admin-pw', 'utf8').trim();
const LEAD = 'r22c-lead@r22uat.test';
const AM = 'r22c-am@r22uat.test';
const ADMIN = 'r22b-hotfix-admin@r22uat.test'; // قارئ طرف ثالث مستقلّ (ليس كاتب التعليق ولا صاحب التقرير).
const SLUGS = ['content', 'design', 'video', 'moderation', 'seo'];
const ONLY = (process.env.ONLY || '').split(',').map((s) => s.trim()).filter(Boolean);

// ثلاثة أسطر يفصلها Enter حقيقيّ من لوحة المفاتيح، بخليط عربيّ/لاتينيّ/أرقام/رموز.
const LINES = (slug) => [
  `السطر الأول — R22BML-${slug} — سبب القرار`,
  'السطر الثاني — Q3/2026 · 45.7% ✓ (مقبول)',
  'السطر الثالث — يُرجى التنقيح؟ نعم!',
];
const ITEM_TEXT = (slug) => `بند عمل R22BML-${slug} — نصّ البند للتحقّق من رحلة التعليقات.`;

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
  window.__main = () => (document.querySelector('main') || document.body).innerText;
  window.__itemBoxes = () => window.__btns('حذف بند عمل').map((b) => {
    let n = b, last = b;
    while (n.parentElement) {
      n = n.parentElement;
      if (window.__btns('حذف بند عمل').filter((x) => n.contains(x)).length === 1) last = n; else break;
    }
    return last;
  });
  // حقل «ملاحظة / سبب» أيًّا كان نوعه — الفحص نفسه يكشف إن كان input أم textarea.
  window.__reasonEl = () => [...document.querySelectorAll('input,textarea')]
    .find((i) => (i.placeholder || '').includes('اكتب سبب القرار')) || null;
  // بصمة بنيويّة للحقل: النوع والسمات ومعالجات لوحة المفاتيح المعلنة.
  window.__reasonProbe = () => {
    const el = window.__reasonEl();
    if (!el) return null;
    const cs = getComputedStyle(el);
    return {
      tagName: el.tagName,
      type: el.getAttribute('type'),
      rows: el.getAttribute('rows'),
      className: el.className,
      resize: cs.resize,
      whiteSpace: cs.whiteSpace,
      hasInlineKeyHandler: !!(el.getAttribute('onkeydown') || el.getAttribute('onkeypress') || el.getAttribute('onkeyup')),
      isTextArea: el instanceof HTMLTextAreaElement,
    };
  };
  // العنصر الذي يعرض تعليقًا مخزَّنًا يحوي علامتنا: نقيس نمطه المحسوب وارتفاعه الفعليّ.
  window.__renderedComment = (marker) => {
    const els = [...document.querySelectorAll('p,div,span,td')]
      .filter((e) => (e.textContent || '').includes(marker));
    // الأعمق = العنصر الحامل للنصّ نفسه لا حاوياته.
    const el = els.filter((e) => !els.some((o) => o !== e && e.contains(o))).pop();
    if (!el) return null;
    const cs = getComputedStyle(el);
    const line = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.4;
    return {
      tag: el.tagName,
      className: el.className,
      whiteSpace: cs.whiteSpace,
      overflowWrap: cs.overflowWrap,
      wordBreak: cs.wordBreak,
      offsetHeight: el.offsetHeight,
      lineHeight: line,
      renderedLines: Math.round(el.offsetHeight / line),
      innerTextNewlines: (el.innerText.match(/\n/g) || []).length,
      textContentNewlines: (el.textContent.match(/\n/g) || []).length,
      innerText: el.innerText.slice(0, 400),
    };
  };
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const RESULTS = path.join(OUT, 'multiline-acceptance.json');
const results = fs.existsSync(RESULTS) ? JSON.parse(fs.readFileSync(RESULTS, 'utf8')) : {};

const browser = await chromium.launch({
  ignoreHTTPSErrors: true,
  args: ['--host-resolver-rules=MAP test.emarketingacademy.net 127.0.0.1:8443', '--ignore-certificate-errors'],
});

async function session(email) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 1200 }, locale: 'ar', ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  const consoleErrors = [];
  const apiFailures = [];
  const posted = [];
  const apiBodies = [];
  page.on('dialog', (d) => d.accept().catch(() => {}));
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 200)); });
  page.on('request', (r) => {
    if (r.method() === 'POST' && /\/api\/submissions\/[^/]+\/(approve|return)/.test(r.url())) {
      posted.push({ url: r.url().replace(BASE, ''), body: r.postData() || '' });
    }
  });
  page.on('response', async (r) => {
    const u = r.url();
    if (u.includes('/api/') && r.status() >= 400) apiFailures.push(`${r.status()} ${r.request().method()} ${u.replace(BASE, '')}`);
    if (/\/api\/submissions\/[0-9a-f-]{36}(\?|$)/.test(u) && r.request().method() === 'GET') {
      try { apiBodies.push({ url: u.replace(BASE, ''), body: (await r.text()).slice(0, 20000) }); } catch { /* ignore */ }
    }
  });
  await page.addInitScript(HELPERS);
  let ok = false;
  for (let a = 1; a <= 3 && !ok; a++) {
    await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
    await sleep(2500);
    await page.evaluate(HELPERS).catch(() => {});
    await page.evaluate(([em, pw]) => {
      window.__set(document.querySelector('input[type=email]'), em);
      window.__set(document.querySelector('input[type=password]'), pw);
    }, [email, email === ADMIN ? ADMIN_PW : PW]);
    await page.click('button:has-text("دخول")');
    try { await page.waitForURL(/\/app/, { timeout: 45000 }); ok = true; } catch { await sleep(3000); }
  }
  if (!ok) throw new Error('LOGIN_FAILED_' + email);
  return { ctx, page, consoleErrors, apiFailures, posted, apiBodies };
}

const go = async (page, url, wait = 6000) => {
  await page.goto(BASE + url, { waitUntil: 'domcontentloaded' });
  await sleep(wait);
  await page.evaluate(HELPERS).catch(() => {});
};
const shot = async (page, name) => {
  await page.screenshot({ path: path.join(SHOTS, name + '.png'), fullPage: true });
  return name + '.png';
};

for (const slug of SLUGS) {
  if (ONLY.length && !ONLY.includes(slug)) continue;
  const E = STATE.employees[slug];
  const MARKER = `R22BML-${slug}`;
  const TEXT = LINES(slug).join('\n');
  const COMMENT_NEEDLE = `${MARKER} — سبب القرار`; // فريد للتعليق وحده (لا يظهر في نصّ بند العمل).
  const R = { slug, email: E.email, templateId: E.templateId, templateTitle: E.templateTitle, projectId: E.projectId, marker: MARKER, expectedText: TEXT, shots: [] };

  // ============ (1–4) الموظّف: تقرير جديد للفترة الحاليّة ثمّ إرسال للاعتماد ============
  const emp = await session(E.email);
  try {
    await go(emp.page, '/app/submissions');
    // تقارير الفترة الحاليّة للقوالب الخمسة كلّها «مُغلقة» من تذكرة سابقة، فتُنشأ نسخة جديدة
    // من نفس القالب عبر السطح الرسميّ «إنشاء تقرير» — لا تعديل لأيّ بيانات قائمة.
    const opened = await emp.page.evaluate((tpl) => {
      const s = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => /اختر قالبًا/.test(o.text)));
      if (!s) return 'NO_TEMPLATE_SELECT';
      const o = [...s.options].find((x) => x.text.trim().startsWith(tpl));
      if (!o) return 'TEMPLATE_OPTION_MISSING';
      window.__set(s, o.value);
      return 'OK';
    }, E.templateTitle);
    if (opened !== 'OK') throw new Error(opened);
    await sleep(1500);
    await emp.page.evaluate(() => { const b = window.__btn('إنشاء تقرير'); if (b) b.click(); });
    await sleep(6000);
    await emp.page.evaluate(HELPERS).catch(() => {});
    R.submissionId = (emp.page.url().split('open=')[1] || '').slice(0, 36);
    R.submissionStatusText = await emp.page.evaluate(() => window.__main().split('\n').filter(Boolean).slice(0, 8));

    // مشروع واحد + بند عمل واحد
    await emp.page.evaluate(async () => {
      const d = () => window.__btns('حذف المشروع');
      let g = 0;
      while (d().length > 1 && g++ < 12) { d()[d().length - 1].click(); await new Promise((r) => setTimeout(r, 700)); }
      if (d().length === 0) { const a = window.__btn('+ إضافة مشروع'); if (a) a.click(); await new Promise((r) => setTimeout(r, 1800)); }
    });
    await sleep(1500);
    await emp.page.evaluate(HELPERS).catch(() => {});
    R.projectSelected = await emp.page.evaluate((pj) => {
      const s = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.text.includes(pj)));
      if (!s) return false;
      const o = [...s.options].find((x) => x.text.includes(pj));
      if (s.value !== o.value) window.__set(s, o.value);
      return s.value === o.value;
    }, E.projectName);
    await sleep(2000);
    await emp.page.evaluate(HELPERS).catch(() => {});
    await emp.page.evaluate(async () => {
      if (window.__itemBoxes().length === 0) {
        const a = window.__btn('+ إضافة بند عمل');
        if (a) { a.click(); await new Promise((r) => setTimeout(r, 1800)); }
      }
    });
    await sleep(1500);
    await emp.page.evaluate(HELPERS).catch(() => {});
    R.workItemsFilled = await emp.page.evaluate((txt) => {
      const boxes = window.__itemBoxes();
      for (const el of document.querySelectorAll('input,select,textarea')) {
        if (el.disabled || el.type === 'hidden' || el.type === 'checkbox' || el.type === 'radio') continue;
        if (el.value) continue;
        if (el.tagName === 'SELECT') {
          const opts = [...el.options].filter((o) => o.value !== '');
          if (opts.length) window.__set(el, opts[0].value);
        } else if (el.type === 'number') window.__set(el, '5');
        else if (el.type === 'date') window.__set(el, '2026-09-01');
        else window.__set(el, txt);
      }
      return boxes.length;
    }, ITEM_TEXT(slug));
    await sleep(1200);
    await emp.page.evaluate(() => { const b = window.__btn('حفظ'); if (b) b.click(); });
    await sleep(4500);
    await emp.page.evaluate(() => { const b = window.__btn('إرسال للاعتماد'); if (b) b.click(); });
    await sleep(2200);
    await emp.page.evaluate(() => {
      const ok = [...document.querySelectorAll('button')].find((b) => /^(تأكيد|نعم|إرسال)$/.test(b.textContent.trim()) && b.offsetParent);
      if (ok) ok.click();
    });
    await sleep(5000);
    R.shots.push(await shot(emp.page, `M01-${slug}-submitted`));
    R.submitApiFailures = emp.apiFailures.slice(0, 6);
  } catch (err) {
    R.employeeSubmitError = String(err).slice(0, 250);
  }
  await emp.ctx.close();

  // ============ (5–7) المراجِع: كتابة تعليق متعدّد الأسطر بـEnter حقيقيّ ============
  const lead = await session(LEAD);
  try {
    await go(lead.page, `/app/submissions?open=${R.submissionId}`);

    // (5) بصمة الحقل البنيويّة — يجب أن يكون textarea لا <input type="text">
    R.probe_reasonField = await lead.page.evaluate(() => window.__reasonProbe());
    R.MULTILINE_INPUT_ELEMENT = !!(R.probe_reasonField && R.probe_reasonField.isTextArea && R.probe_reasonField.rows === '4');

    // (6) الكتابة بـEnter حقيقيّ من لوحة المفاتيح (لا برمجيًّا)
    await lead.page.focus('textarea[placeholder*="اكتب سبب القرار"]').catch(async () => {
      await lead.page.evaluate(() => window.__reasonEl()?.focus());
    });
    const lines = LINES(slug);
    for (let i = 0; i < lines.length; i++) {
      await lead.page.keyboard.type(lines[i], { delay: 8 });
      if (i < lines.length - 1) await lead.page.keyboard.press('Enter');
    }
    await sleep(800);
    R.valueAfterKeyboard = await lead.page.evaluate(() => window.__reasonEl()?.value ?? null);
    R.ENTER_ACCEPTED_IN_UI = R.valueAfterKeyboard === TEXT;
    R.valueNewlineCount = (R.valueAfterKeyboard?.match(/\n/g) || []).length;
    R.shots.push(await shot(lead.page, `M02-${slug}-reviewer-typed-multiline`));

    // (7) الإرسال والتقاط الحمولة الفعليّة
    await lead.page.evaluate(() => { const b = window.__btn('إعادة للتعديل'); if (b) b.click(); });
    await sleep(6000);
    const post = lead.posted.find((p) => p.url.includes('/return'));
    R.requestPayload = post ? post.body.slice(0, 1200) : null;
    R.NEWLINES_IN_REQUEST_PAYLOAD = !!post && post.body.includes('\\n') && JSON.parse(post.body).comment === TEXT;

    // العرض بعد إعادة التحميل عند المراجِع
    await go(lead.page, `/app/submissions?open=${R.submissionId}`);
    R.render_reviewer = await lead.page.evaluate((m) => window.__renderedComment(m), MARKER);
    R.NEWLINES_RENDERED_FOR_REVIEWER = !!(R.render_reviewer
      && R.render_reviewer.whiteSpace === 'pre-wrap'
      && R.render_reviewer.innerTextNewlines >= 2
      && R.render_reviewer.renderedLines >= 3);
    R.shots.push(await shot(lead.page, `M03-${slug}-reviewer-rendered-multiline`));

    // الـAPI نفسها: هل يعود النصّ بـ\n؟
    const body = lead.apiBodies.filter((b) => b.body.includes(MARKER)).pop();
    R.NEWLINES_RETURNED_BY_API = !!body && body.body.includes('\\n');
    R.apiSnippet = body ? body.body.slice(Math.max(0, body.body.indexOf(MARKER) - 60), body.body.indexOf(MARKER) + 240) : null;
    R.leadApiFailures = lead.apiFailures.slice(0, 8);
    R.leadConsoleErrors = lead.consoleErrors.slice(0, 8);
  } catch (err) {
    R.reviewerError = String(err).slice(0, 250);
  }
  await lead.ctx.close();

  // ============ (8–9) الموظّف في سياق بارد: العرض + جرس الإشعارات ============
  const emp2 = await session(E.email);
  try {
    await go(emp2.page, `/app/submissions?open=${R.submissionId}`);
    R.render_employee = await emp2.page.evaluate((m) => window.__renderedComment(m), MARKER);
    R.NEWLINES_RENDERED_FOR_EMPLOYEE = !!(R.render_employee
      && R.render_employee.whiteSpace === 'pre-wrap'
      && R.render_employee.innerTextNewlines >= 2
      && R.render_employee.renderedLines >= 3);
    R.shots.push(await shot(emp2.page, `M04-${slug}-employee-sees-multiline`));

    // جرس الإشعارات
    await emp2.page.evaluate(() => {
      const b = [...document.querySelectorAll('button')].find((x) => /notification|bell|إشعار/i.test(x.className + ' ' + (x.getAttribute('aria-label') || '') + ' ' + x.title));
      if (b) b.click();
      else {
        const svgBtn = [...document.querySelectorAll('header button, nav button')].find((x) => x.querySelector('svg'));
        if (svgBtn) svgBtn.click();
      }
    });
    await sleep(2500);
    R.render_notification = await emp2.page.evaluate((m) => window.__renderedComment(m), MARKER);
    R.notificationPanelText = await emp2.page.evaluate(() => {
      const p = [...document.querySelectorAll('div')].filter((d) => /الإشعارات|إشعارات/.test(d.innerText || '') && d.innerText.length < 3000).pop();
      return p ? p.innerText.slice(0, 700) : null;
    });
    R.MULTILINE_NOTIFICATION = !!(R.render_notification && R.render_notification.whiteSpace === 'pre-wrap' && R.render_notification.innerTextNewlines >= 2);
    R.shots.push(await shot(emp2.page, `M05-${slug}-notification-bell`));
    R.employeeApiFailures = emp2.apiFailures.slice(0, 8);
  } catch (err) {
    R.employeeViewError = String(err).slice(0, 250);
  }
  await emp2.ctx.close();

  // ============ (10) مدير الحساب: التقرير في نطاق المشروع + Project 360 ============
  const am = await session(AM);
  try {
    // مدير الحساب (`AccountPortfolioReader`) لا يملك سطحًا يعرض تعليقات الاعتماد إطلاقًا:
    // `GET /api/submissions/{id}` يردّ 403 بحكم التخويل بالمورد، وسطحه المشروع هو شريحة المشروع
    // التي تعرض محتوى التقرير لا التعليق. نُثبت الأمرين نصًّا ولا نزعم نجاحًا غير موجود.
    await go(am.page, `/app/projects/${E.projectId}/reports/${R.submissionId}`);
    R.projectSliceText = await am.page.evaluate(() => window.__main().replace(/\n{3,}/g, '\n\n').slice(0, 400));
    R.shots.push(await shot(am.page, `M06-${slug}-account-manager-project-slice`));
    R.amSubmissionDetailStatus = await am.page.evaluate(
      (id) => fetch(`/api/submissions/${id}`, { headers: { Authorization: `Bearer ${localStorage.getItem('me_access')}` } })
        .then((x) => x.status).catch(() => -1), R.submissionId);
    R.render_am = await am.page.evaluate((m) => window.__renderedComment(m), COMMENT_NEEDLE);
    R.ACCOUNT_MANAGER_COMMENT_SURFACE_EXPOSED = R.render_am !== null;

    // Project 360 — تبويب «القرارات والحوكمة»: سطح العرض متعدّد الأسطر المُشخَّص
    // (ProjectGovernanceTab.tsx:93)، للقراءة فقط ولم يُمسّ؛ المطلوب إثبات أنّه ما زال يحفظ الأسطر.
    await go(am.page, `/app/projects/${E.projectId}/360`);
    await am.page.evaluate(() => { const b = window.__btn('القرارات والحوكمة'); if (b) b.click(); });
    await sleep(3000);
    R.p360Text = await am.page.evaluate(() => window.__main().replace(/\n{3,}/g, '\n\n').slice(0, 600));
    R.render_p360 = await am.page.evaluate((m) => window.__renderedComment(m), `${MARKER}-P360`);
    R.p360PreWrapNodes = await am.page.evaluate(() => [...document.querySelectorAll('p,div,span')]
      .filter((e) => getComputedStyle(e).whiteSpace === 'pre-wrap').length);
    R.NEWLINES_RENDERED_IN_PROJECT_360 = !!(R.render_p360
      && R.render_p360.whiteSpace === 'pre-wrap'
      && R.render_p360.innerTextNewlines >= 2
      && R.render_p360.renderedLines >= 3);
    R.shots.push(await shot(am.page, `M07-${slug}-project-360-governance`));
    R.amApiFailures = am.apiFailures.slice(0, 8);
  } catch (err) {
    R.amError = String(err).slice(0, 250);
  }
  await am.ctx.close();

  // ============ (11) سياق بارد جديد وطرف ثالث مستقلّ على تعليق مخزَّن مسبقًا — بلا أيّ تعديل للبيانات ============
  const cold = await session(ADMIN);
  try {
    await go(cold.page, `/app/submissions?open=${R.submissionId}`, 8000);
    R.render_cold = await cold.page.evaluate((m) => window.__renderedComment(m), COMMENT_NEEDLE);
    R.HISTORICAL_COMMENT_RENDERING = !!(R.render_cold
      && R.render_cold.whiteSpace === 'pre-wrap'
      && R.render_cold.innerTextNewlines >= 2
      && R.render_cold.renderedLines >= 3);
    R.MULTILINE_THIRD_PARTY_READER = R.HISTORICAL_COMMENT_RENDERING;
    R.shots.push(await shot(cold.page, `M08-${slug}-cold-reload-stored-comment`));
  } catch (err) {
    R.coldError = String(err).slice(0, 250);
  }
  await cold.ctx.close();

  results[slug] = R;
  fs.writeFileSync(RESULTS, JSON.stringify(results, null, 2));
  console.error('>>>', slug,
    'input=' + R.MULTILINE_INPUT_ELEMENT,
    'enter=' + R.ENTER_ACCEPTED_IN_UI,
    'payload=' + R.NEWLINES_IN_REQUEST_PAYLOAD,
    'api=' + R.NEWLINES_RETURNED_BY_API,
    'emp=' + R.NEWLINES_RENDERED_FOR_EMPLOYEE,
    'amSurface=' + R.ACCOUNT_MANAGER_COMMENT_SURFACE_EXPOSED + '/http' + R.amSubmissionDetailStatus,
    'p360=' + R.NEWLINES_RENDERED_IN_PROJECT_360,
    'notif=' + R.MULTILINE_NOTIFICATION,
    'third=' + R.MULTILINE_THIRD_PARTY_READER,
    'cold=' + R.HISTORICAL_COMMENT_RENDERING,
    R.employeeSubmitError || R.reviewerError || R.employeeViewError || R.amError || R.coldError || '');
}

fs.writeFileSync(RESULTS, JSON.stringify(results, null, 2));
await browser.close();
console.log('WROTE', RESULTS);

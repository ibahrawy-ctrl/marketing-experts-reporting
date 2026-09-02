// R22B CLOSURE — UAT تشغيليّ وبصريّ على حزمة TEST المنشورة بعينها.
// الأصل هو `https://test.emarketingacademy.net` حقيقةً في نظر المتصفّح (host-resolver-rules)،
// والبايتات المقدَّمة هي بايتات /opt/reporting-test/frontend/dist بعد rsync، والـ/api يمرّ
// عبر نفق SSH إلى 127.0.0.1:5091. بوّابةٌ تخاطب localhost لا تُثبت شيئًا عن حزمة المتصفّح.
//
// PART=emp|review|p360|visual  ·  ONLY=content,seo…
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = 'https://test.emarketingacademy.net';
const OUT = process.env.OUT_DIR || '/tmp/r22b-uat/out';
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const PW = fs.readFileSync('/tmp/r22b-uat/.user-pw', 'utf8').trim();
const STATE = JSON.parse(fs.readFileSync('/tmp/r22b-uat/closure-state.json', 'utf8'));
const PART = process.env.PART || 'emp';
const ONLY = (process.env.ONLY || '').split(',').map((s) => s.trim()).filter(Boolean);
const RESULTS = path.join(OUT, `${PART}.json`);
const out = fs.existsSync(RESULTS) ? JSON.parse(fs.readFileSync(RESULTS, 'utf8')) : {};
const save = () => fs.writeFileSync(RESULTS, JSON.stringify(out, null, 2));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// نصّ متعدّد الأسطر: التطبيع بالمسافات يبتلع \n فيمرّ ادّعاء «الأسطر محفوظة» زورًا.
const T1 = 'البند الأوّل — حملة «إطلاق المنتج».\nسطر ثانٍ يثبت تعدّد الأسطر.\nسطر ثالث.';
const T2 = 'البند الثاني — حملة «عروض نهاية الأسبوع».\nمهمّة مختلفة كلّيًّا.\nسطر ثالث مختلف.';
const T2E = 'البند الثاني بعد تعديل مستقلّ.\nالسطر الثاني تغيّر وحده.\nالسطر الثالث كما هو.';

const HELPERS = () => {
  window.__set = (el, v) => {
    const p = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype
      : el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(p, 'value').set.call(el, v);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  window.__btn = (t) => [...document.querySelectorAll('button')].find((b) => b.textContent.trim() === t);
  window.__btns = (t) => [...document.querySelectorAll('button')].filter((b) => b.textContent.trim() === t);
  window.__boxes = () => window.__btns('حذف بند عمل').map((b) => {
    let n = b, last = b;
    while (n.parentElement) {
      n = n.parentElement;
      if (window.__btns('حذف بند عمل').filter((x) => n.contains(x)).length === 1) last = n; else break;
    }
    return last;
  });
  window.__snap = (box) => [...box.querySelectorAll('input,select,textarea')]
    .filter((e) => e.type !== 'hidden')
    .map((e) => String(e.value ?? ''));
  window.__fill = (box, variant, txt) => {
    for (const el of box.querySelectorAll('input,select,textarea')) {
      if (el.type === 'hidden' || el.disabled) continue;
      if (el.tagName === 'SELECT') {
        const o = [...el.options].filter((x) => x.value !== '');
        if (o.length) window.__set(el, (variant === 1 ? o[0] : o[o.length - 1]).value);
      } else if (el.type === 'number') window.__set(el, variant === 1 ? '11' : '77');
      else if (el.type === 'date') window.__set(el, variant === 1 ? '2026-09-01' : '2026-09-02');
      else if (el.type === 'checkbox' || el.type === 'radio') continue;
      else window.__set(el, txt);
    }
    return window.__snap(box);
  };
  window.__fillOutside = () => {
    const boxes = window.__boxes();
    for (const el of document.querySelectorAll('input,select,textarea')) {
      if (el.disabled || el.type === 'hidden' || el.type === 'checkbox' || el.type === 'radio') continue;
      if (boxes.some((b) => b.contains(el))) continue;
      if (el.value) continue;
      if (el.tagName === 'SELECT') {
        const o = [...el.options].filter((x) => x.value !== '');
        if (o.length) window.__set(el, o[0].value);
      } else if (el.type === 'number') window.__set(el, '5');
      else if (el.type === 'date') window.__set(el, '2026-09-01');
      else window.__set(el, 'قيمة UAT للإغلاق R22B');
    }
  };
};

const browser = await chromium.launch({
  ignoreHTTPSErrors: true,
  args: ['--host-resolver-rules=MAP test.emarketingacademy.net 127.0.0.1:8443', '--ignore-certificate-errors'],
});

async function open(viewport = { width: 1440, height: 1080 }) {
  const ctx = await browser.newContext({ viewport, locale: 'ar', ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  const consoleErrors = [], apiFailures = [];
  page.on('dialog', (d) => d.accept().catch(() => {}));
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 180)); });
  page.on('response', (r) => {
    const u = r.url();
    if (u.includes('/api/') && r.status() >= 400) apiFailures.push(`${r.status()} ${r.request().method()} ${u.replace(BASE, '')}`);
  });
  await page.addInitScript(HELPERS);
  const ev = async (fn, arg) => {
    let last;
    for (let i = 0; i < 4; i++) {
      try { return await page.evaluate(fn, arg); }
      catch (e) {
        last = e;
        if (!/Execution context was destroyed|Target closed|Cannot find context/.test(String(e))) throw e;
        await sleep(1500); try { await page.evaluate(HELPERS); } catch { /* ignore */ }
      }
    }
    throw last;
  };
  const until = async (fn, arg, ms = 25000) => {
    const t0 = Date.now();
    for (;;) {
      const v = await ev(fn, arg);
      if (v) return v;
      if (Date.now() - t0 > ms) return null;
      await sleep(800);
    }
  };
  const shot = async (name) => {
    await page.screenshot({ path: path.join(SHOTS, `${name}.png`), fullPage: true });
    return `${name}.png`;
  };
  const login = async (email, pw = PW) => {
    for (let a = 1; a <= 3; a++) {
      await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
      await sleep(1800); await ev(HELPERS);
      await ev(([em, p]) => {
        window.__set(document.querySelector('input[type=email]'), em);
        window.__set(document.querySelector('input[type=password]'), p);
      }, [email, pw]);
      await page.click('button:has-text("دخول")');
      try { await page.waitForURL(/\/app/, { timeout: 40000 }); return true; } catch { await sleep(2500); }
    }
    throw new Error('LOGIN_FAILED:' + email);
  };
  const go = async (url, ms = 3500) => { await page.goto(url, { waitUntil: 'domcontentloaded' }); await sleep(ms); await ev(HELPERS); };
  const reload = async (ms = 4000) => { await page.reload({ waitUntil: 'domcontentloaded' }); await sleep(ms); await ev(HELPERS); };
  return { ctx, page, ev, until, shot, login, go, reload, consoleErrors, apiFailures };
}

// ===================== PART=emp — الرحلة التشغيليّة لكلّ تخصّص =====================
if (PART === 'emp') {
  for (const slug of ['content', 'design', 'video', 'moderation', 'seo']) {
    if (ONLY.length && !ONLY.includes(slug)) continue;
    const E = STATE.employees[slug];
    const s = await open();
    const R = { slug, email: E.email, templateTitle: E.templateTitle, projectName: E.projectName, shots: [] };
    const shot = async (n) => { R.shots.push(await s.shot(`${PART}-${slug}-${n}`)); };
    try {
      await s.login(E.email);

      // (1) تقاريري — الاستحقاق
      await s.go(`${BASE}/app/submissions`);
      await s.until(() => document.querySelectorAll('table tbody tr').length > 0);
      R.templateOptions = await s.ev(() => {
        const x = [...document.querySelectorAll('select')].find((y) => ((y.closest('label') || y.parentElement).innerText || '').includes('القالب'));
        return x ? [...x.options].map((o) => o.text.trim()).filter((t) => !t.includes('اختر')) : null;
      });
      R.unexpectedTemplates = (R.templateOptions || []).filter((t) => !t.startsWith(E.templateTitle));
      await shot('01-my-reports');

      // (2) إنشاء تقرير الدورة الحاليّة من نموذج الإنشاء ثمّ فتحه
      R.existingRow = await s.ev((t) => {
        const r = [...document.querySelectorAll('table tbody tr')]
          .filter((x) => x.cells[0].innerText.includes(t) && !x.innerText.includes('مغلق')).pop();
        if (!r) return false;
        [...r.querySelectorAll('button')].find((b) => b.textContent.trim() === 'عرض')?.click();
        return true;
      }, E.templateTitle);
      if (R.existingRow) { await sleep(4500); await s.ev(HELPERS); }
      R.createForm = R.existingRow ? { ok: true, chosen: 'REUSED_EXISTING_ROW' } : await s.ev((t) => {
        const sel = [...document.querySelectorAll('select')]
          .find((x) => ((x.closest('label') || x.parentElement).innerText || '').includes('القالب'));
        if (!sel) return { ok: false, why: 'NO_TEMPLATE_SELECT' };
        const opt = [...sel.options].find((o) => o.text.includes(t));
        if (!opt) return { ok: false, why: 'TEMPLATE_NOT_OFFERED', offered: [...sel.options].map((o) => o.text.trim()) };
        window.__set(sel, opt.value);
        return { ok: true, chosen: opt.text.trim() };
      }, E.templateTitle);
      if (!R.createForm.ok) throw new Error('CREATE_FORM:' + R.createForm.why);
      if (!R.existingRow) {
        await sleep(1500); await s.ev(HELPERS);
        await s.ev(() => { const b = window.__btn('إنشاء تقرير'); if (b && !b.disabled) b.click(); });
        await sleep(5000); await s.ev(HELPERS);
      }
      if (!s.page.url().includes('open=')) {
        // نموذج الإنشاء قد يكتفي بإضافة صفّ؛ نفتح صفّ الدورة الحاليّة من الجدول.
        await s.ev((t) => {
          const rows = [...document.querySelectorAll('table tbody tr')]
            .filter((x) => x.cells[0].innerText.includes(t) && !x.innerText.includes('مغلق'));
          const r = rows[rows.length - 1];
          if (r) [...r.querySelectorAll('button')].find((b) => b.textContent.trim() === 'عرض')?.click();
        }, E.templateTitle);
        await sleep(4500); await s.ev(HELPERS);
      }
      R.submissionId = (s.page.url().split('open=')[1] || '').slice(0, 36);
      if (!R.submissionId) throw new Error('NO_SUBMISSION_OPENED');
      R.periodHeader = await s.ev(() => (document.querySelector('main') || document.body).innerText
        .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 6));

      // (3) بطاقة مشروع واحدة + VIS-01: تسمية المشروع بعزل اتّجاهيّ
      await s.until(() => !!window.__btn('+ إضافة مشروع') || window.__btns('حذف المشروع').length > 0);
      await s.ev(async () => {
        const d = () => window.__btns('حذف المشروع');
        let g = 0;
        while (d().length > 1 && g++ < 12) { d()[d().length - 1].click(); await new Promise((r) => setTimeout(r, 600)); }
        if (d().length === 0) { const a = window.__btn('+ إضافة مشروع'); if (a) a.click(); await new Promise((r) => setTimeout(r, 1500)); }
      });
      await sleep(1200); await s.ev(HELPERS);
      R.vis01 = await s.ev((pname) => {
        const ps = [...document.querySelectorAll('select')].find((x) => [...x.options].some((o) => o.text.includes('R22C')));
        if (!ps) return { found: false };
        const own = [...ps.options].find((o) => o.text.includes(pname));
        if (own) window.__set(ps, own.value);
        const t = (own || [...ps.options].find((o) => o.text.includes('R22C'))).text;
        return { found: true, sample: t, fsi: t.includes('\u2068'), pdi: t.includes('\u2069'), selectedOwnProject: !!own };
      }, E.projectName);
      await sleep(1200); await s.ev(HELPERS);
      await shot('02-report-opened');

      // (4) بندان بنصّ متعدّد الأسطر
      R.itemsAdded = await s.ev(async () => {
        const add = window.__btn('+ إضافة بند عمل');
        while (window.__boxes().length < 2 && add) { add.click(); await new Promise((r) => setTimeout(r, 800)); }
        return window.__boxes().length;
      });
      await sleep(800); await s.ev(HELPERS);
      R.before = await s.ev(([a, b]) => {
        const x = window.__boxes();
        const r = [window.__fill(x[0], 1, a), window.__fill(x[1], 2, b)];
        window.__fillOutside();
        return r;
      }, [T1, T2]);
      await shot('03-two-items-before-save');

      // (5) حفظ + VIS-04: مؤشّر ثابت
      R.vis04Before = await s.ev(() => !!document.querySelector('[data-testid="draft-saved-indicator"]'));
      await s.ev(() => { const b = window.__btn('حفظ كمسودة') || window.__btn('حفظ'); if (b) b.click(); });
      await sleep(4500); await s.ev(HELPERS);
      R.vis04After = await s.ev(() => {
        const el = document.querySelector('[data-testid="draft-saved-indicator"]');
        return el ? el.innerText.trim().slice(0, 120) : null;
      });
      await shot('04-saved-indicator');
      await sleep(7000); // بعد ذوبان أيّ Toast عابر — المؤشّر يجب أن يبقى
      R.vis04Persist = await s.ev(() => {
        const el = document.querySelector('[data-testid="draft-saved-indicator"]');
        return el ? el.innerText.trim().slice(0, 120) : null;
      });
      await shot('05-indicator-persists-after-toast');

      // (6) إعادة تحميل — البندان مستقلّان والأسطر محفوظة
      await s.reload(5000);
      await s.until(() => window.__boxes().length >= 2);
      R.afterReload = await s.ev(() => window.__boxes().map((b) => window.__snap(b)));
      R.linesKept = JSON.stringify(R.afterReload).includes('\\n');
      R.itemsDistinct = JSON.stringify(R.afterReload[0]) !== JSON.stringify(R.afterReload[1]);
      await shot('06-after-reload-GOVERNING');

      // (7) تعديل البند الثاني وحده
      await s.ev((t) => { window.__fill(window.__boxes()[1], 2, t); }, T2E);
      await s.ev(() => { const b = window.__btn('حفظ كمسودة') || window.__btn('حفظ'); if (b) b.click(); });
      await sleep(4500);
      await s.reload(5000);
      await s.until(() => window.__boxes().length >= 2);
      R.afterEdit = await s.ev(() => window.__boxes().map((b) => window.__snap(b)));
      R.item1Untouched = JSON.stringify(R.afterEdit[0]) === JSON.stringify(R.afterReload[0]);
      R.item2Changed = JSON.stringify(R.afterEdit[1]) !== JSON.stringify(R.afterReload[1]);
      await shot('07-independent-edit-persisted');

      // (8) إرسال للاعتماد
      R.submitClicked = await s.ev(() => {
        const b = window.__btn('إرسال للاعتماد');
        if (!b || b.disabled) return false;
        b.click(); return true;
      });
      await sleep(6000); await s.ev(HELPERS);
      // نافذة تأكيد محتملة
      await s.ev(() => { const b = window.__btn('تأكيد') || window.__btn('إرسال'); if (b) b.click(); });
      await sleep(5000); await s.ev(HELPERS);
      R.statusAfterSubmit = await s.ev(() => (document.querySelector('main') || document.body).innerText
        .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 12));
      R.isSubmitted = JSON.stringify(R.statusAfterSubmit).includes('مُرسَل')
        || JSON.stringify(R.statusAfterSubmit).includes('قيد المراجعة')
        || !JSON.stringify(R.statusAfterSubmit).includes('مسودة');
      await shot('08-after-submit');

      R.status = 'DONE';
    } catch (e) {
      R.status = 'ERROR'; R.error = String(e).slice(0, 300);
      try { R.shots.push(await s.shot(`${PART}-${slug}-99-error`)); } catch { /* ignore */ }
    }
    R.consoleErrors = s.consoleErrors.slice(0, 12);
    R.apiFailures = s.apiFailures.slice(0, 20);
    out[slug] = R; save();
    console.error('>>>', slug, R.status, R.error || '', 'sub=' + (R.submissionId || '-'));
    await s.ctx.close();
  }
}

// ===================== PART=seo — VIS-05: صفّ محكوم لكلّ مقال =====================
// قالب مقالات SEO لا يحوي «بنود عمل»: بطاقة المشروع نفسها هي صفّ المقال. لذلك مسار مستقلّ.
if (PART === 'seo') {
  const E = STATE.employees.seo;
  const s = await open();
  const R = { email: E.email, templateTitle: E.templateTitle, shots: [] };
  const shot = async (n) => { R.shots.push(await s.shot(`seo-${n}`)); };
  try {
    await s.login(E.email);
    await s.go(`${BASE}/app/submissions`);
    await s.until(() => document.querySelectorAll('table tbody tr').length > 0);
    const reused = await s.ev((t) => {
      const r = [...document.querySelectorAll('table tbody tr')]
        .filter((x) => x.cells[0].innerText.includes(t) && !x.innerText.includes('مغلق')).pop();
      if (!r) return false;
      [...r.querySelectorAll('button')].find((b) => b.textContent.trim() === 'عرض')?.click();
      return true;
    }, E.templateTitle);
    if (!reused) throw new Error('NO_SEO_DRAFT_ROW');
    await sleep(4500); await s.ev(HELPERS);
    R.submissionId = (s.page.url().split('open=')[1] || '').slice(0, 36);

    // بطاقتا مشروع = مقالان، بمشروعين مختلفين للعميل نفسه
    await s.until(() => !!window.__btn('+ إضافة مشروع') || window.__btns('حذف المشروع').length > 0);
    R.cards = await s.ev(async () => {
      const d = () => window.__btns('حذف المشروع');
      const a = window.__btn('+ إضافة مشروع');
      while (d().length < 2 && a) { a.click(); await new Promise((r) => setTimeout(r, 1400)); }
      return d().length;
    });
    await sleep(1500); await s.ev(HELPERS);

    // البنية المحكومة: نوع كلّ حقل كما يراه المتصفّح فعلًا
    R.vis05 = await s.ev(() => {
      const lab = (el) => {
        let n = el;
        for (let i = 0; i < 5 && n; i++, n = n.parentElement) {
          const t = (n.innerText || '').split('\n')[0].trim();
          if (t && t.length < 40) return t;
        }
        return '';
      };
      const fields = [...document.querySelectorAll('input,select,textarea')]
        .filter((e) => e.type !== 'hidden')
        .map((e) => ({ tag: e.tagName, type: e.type, label: lab(e),
          options: e.tagName === 'SELECT' ? [...e.options].map((o) => o.text.trim()).filter((t) => t && !t.includes('اختر')) : null }));
      const status = fields.find((f) => f.label.includes('حالة المقال'));
      const date = fields.find((f) => f.label.includes('تاريخ التسليم'));
      return {
        statusIsSelect: !!status && status.tag === 'SELECT',
        statusOptions: status ? status.options : null,
        dateIsDateInput: !!date && date.type === 'date',
        freeTextStatusInputs: fields.filter((f) => f.label.includes('حالة') && f.tag !== 'SELECT').length,
        labels: fields.map((f) => f.label).filter(Boolean).slice(0, 30),
      };
    });
    await shot('01-governed-fields');

    // تعبئة الصفّين بقيم مميّزة ثمّ الحفظ
    R.filled = await s.ev(() => {
      const cards = window.__btns('حذف المشروع').map((b) => {
        let n = b, last = b;
        while (n.parentElement) {
          n = n.parentElement;
          if (window.__btns('حذف المشروع').filter((x) => n.contains(x)).length === 1) last = n; else break;
        }
        return last;
      });
      const snaps = [];
      cards.forEach((c, i) => {
        for (const el of c.querySelectorAll('input,select,textarea')) {
          if (el.type === 'hidden' || el.disabled) continue;
          if (el.tagName === 'SELECT') {
            const o = [...el.options].filter((x) => x.value !== '');
            if (!o.length) continue;
            if (el.value) continue; // لا نُبدّل اختيار المشروع
            window.__set(el, (i === 0 ? o[0] : o[o.length - 1]).value);
          } else if (el.type === 'date') window.__set(el, i === 0 ? '2026-09-01' : '2026-09-03');
          else if (el.type === 'number') window.__set(el, i === 0 ? '850' : '1200');
          else if (el.type === 'checkbox' || el.type === 'radio') continue;
          else window.__set(el, i === 0
            ? 'مقال أوّل — دليل الكلمات المفتاحيّة.\nسطر ثانٍ.\nسطر ثالث.'
            : 'مقال ثانٍ — تحسين سرعة الصفحة.\nسطر ثانٍ مختلف.\nسطر ثالث مختلف.');
        }
        snaps.push([...c.querySelectorAll('input,select,textarea')].filter((e) => e.type !== 'hidden').map((e) => String(e.value ?? '')));
      });
      window.__fillOutside();
      return snaps;
    });
    await shot('02-two-articles-before-save');

    await s.ev(() => { const b = window.__btn('حفظ كمسودة') || window.__btn('حفظ'); if (b) b.click(); });
    await sleep(5000); await s.ev(HELPERS);
    R.vis04After = await s.ev(() => {
      const el = document.querySelector('[data-testid="draft-saved-indicator"]');
      return el ? el.innerText.trim().slice(0, 120) : null;
    });
    await s.reload(5000);
    await s.until(() => window.__btns('حذف المشروع').length >= 2);
    R.afterReload = await s.ev(() => window.__btns('حذف المشروع').map((b) => {
      let n = b, last = b;
      while (n.parentElement) {
        n = n.parentElement;
        if (window.__btns('حذف المشروع').filter((x) => n.contains(x)).length === 1) last = n; else break;
      }
      return [...last.querySelectorAll('input,select,textarea')].filter((e) => e.type !== 'hidden').map((e) => String(e.value ?? ''));
    }));
    R.linesKept = JSON.stringify(R.afterReload).includes('\\n');
    R.rowsDistinct = JSON.stringify(R.afterReload[0]) !== JSON.stringify(R.afterReload[1]);
    R.statusValuesFromCatalog = await s.ev(() => {
      const sels = [...document.querySelectorAll('select')];
      const st = sels.filter((x) => [...x.options].some((o) => ['Draft', 'Revision', 'Approved', 'Published'].includes(o.text.trim())));
      return st.map((x) => x.options[x.selectedIndex]?.text.trim());
    });
    await shot('03-after-reload-GOVERNING');

    R.submitClicked = await s.ev(() => { const b = window.__btn('إرسال للاعتماد'); if (!b || b.disabled) return false; b.click(); return true; });
    await sleep(6000); await s.ev(HELPERS);
    await s.ev(() => { const b = window.__btn('تأكيد') || window.__btn('إرسال'); if (b) b.click(); });
    await sleep(5000); await s.ev(HELPERS);
    R.statusAfterSubmit = await s.ev(() => (document.querySelector('main') || document.body).innerText
      .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 12));
    R.isSubmitted = !JSON.stringify(R.statusAfterSubmit).includes('مسودة');
    await shot('04-after-submit');
    R.status = 'DONE';
  } catch (e) {
    R.status = 'ERROR'; R.error = String(e).slice(0, 300);
    try { R.shots.push(await s.shot('seo-99-error')); } catch { /* ignore */ }
  }
  R.consoleErrors = s.consoleErrors.slice(0, 12);
  R.apiFailures = s.apiFailures.slice(0, 20);
  out.seo = R; save();
  console.error('>>> seo', R.status, R.error || '', 'sub=' + (R.submissionId || '-'));
  await s.ctx.close();
}

// ===================== PART=review — المراجِع يعتمد =====================
if (PART === 'review') {
  const s = await open();
  const R = { shots: [] };
  try {
    // لا يوجد مسار `/app/reviews` في التطبيق إطلاقًا؛ طابور المراجِع هو تبويب
    // «بانتظار اعتمادي» داخل `/app/submissions`، والاعتماد يقع في صفحة التقرير نفسها
    // تحت ترويسة «إجراء الاعتماد». المسار المخترَع كان يعطي طابورًا فارغًا زورًا.
    await s.login('r22c-lead@r22uat.test');
    const queue = async () => {
      await s.go(`${BASE}/app/submissions`, 5000);
      await s.ev(() => { const b = window.__btn('بانتظار اعتمادي'); if (b) b.click(); });
      await sleep(3500); await s.ev(HELPERS);
      // الطابور قد يكون في حالة تحميل؛ صفر صفوفٍ لحظةَ القياس ليس دليلَ فراغٍ.
      // ننتظر ظهور صفٍّ أو نصّ الفراغ الصريح قبل أيّ استنتاج.
      await s.until(() => document.querySelectorAll('table tbody tr').length > 0
        || document.body.innerText.includes('لا توجد تقارير بانتظار اعتمادك'), null, 25000);
    };
    await queue();
    R.pending = await s.ev(() => [...document.querySelectorAll('table tbody tr')]
      .map((r) => r.innerText.trim().replace(/\s+/g, ' ').slice(0, 110)));
    R.shots.push(await s.shot('review-01-pending'));

    R.approved = [];
    R.multilineOnReviewSurface = [];
    for (let i = 0; i < 8; i++) {
      const opened = await s.ev(() => {
        const r = [...document.querySelectorAll('table tbody tr')][0];
        if (!r) return null;
        const label = r.innerText.trim().replace(/\s+/g, ' ').slice(0, 100);
        const b = [...r.querySelectorAll('button')].find((x) => x.textContent.trim() === 'عرض');
        if (!b) return null;
        b.click(); return label;
      });
      if (!opened) break;
      await sleep(5000); await s.ev(HELPERS);

      // VIS/MULTILINE على سطح المراجِع: الأسطر تصل المعتمِد كما كتبها الموظّف.
      const ml = await s.ev(() => {
        const t = document.body.innerText;
        return {
          hasSecondLine: t.includes('سطر ثانٍ يثبت تعدّد الأسطر') || t.includes('السطر الثاني تغيّر وحده'),
          newlinePreserved: /البند الأوّل[^\n]*\n[^\n]*سطر ثانٍ/.test(t)
            || /البند الثاني[^\n]*\n[^\n]*(مهمّة مختلفة|السطر الثاني)/.test(t),
          approverCard: t.includes('إجراء الاعتماد'),
        };
      });
      ml.row = opened;
      R.multilineOnReviewSurface.push(ml);
      if (i === 0) R.shots.push(await s.shot('review-02-reviewer-sees-multiline'));

      const clicked = await s.ev(() => {
        const b = window.__btn('اعتماد');
        if (!b || b.disabled) return false;
        b.click(); return true;
      });
      if (!clicked) { R.stoppedAt = opened; break; }
      await sleep(4000); await s.ev(HELPERS);
      R.approved.push(opened);
      await queue();
    }
    R.pendingAfter = await s.ev(() => [...document.querySelectorAll('table tbody tr')]
      .map((r) => r.innerText.trim().replace(/\s+/g, ' ').slice(0, 110)));
    R.shots.push(await s.shot('review-03-after-approvals'));
    R.status = 'DONE';
  } catch (e) { R.status = 'ERROR'; R.error = String(e).slice(0, 300); try { R.shots.push(await s.shot('review-99-error')); } catch { /* ignore */ } }
  R.consoleErrors = s.consoleErrors.slice(0, 12); R.apiFailures = s.apiFailures.slice(0, 20);
  out.review = R; save(); console.error('>>> review', R.status, R.error || '');
  await s.ctx.close();
}

// ===================== PART=p360 — VIS-02 · VIS-02ب · VIS-03 =====================
if (PART === 'p360') {
  const s = await open();
  const R = { shots: [] };
  const calls = [];
  s.page.on('request', (r) => { if (r.url().includes('/api/')) calls.push(r.url().replace(BASE, '')); });
  try {
    await s.login('r22c-am@r22uat.test');
    const pid = STATE.employees.content.projectId;

    // VIS-02ب: صفحة تفاصيل المشروع — «تقارير المشروع المرتبطة» بأعمدة القرار
    calls.length = 0;
    await s.go(`${BASE}/app/projects/${pid}`, 6000);
    R.vis02b = await s.ev(() => {
      const tb = [...document.querySelectorAll('table')].find((t) => t.tHead && t.tBodies[0]?.rows.length);
      if (!tb) return { table: false, pageText: (document.querySelector('main') || document.body).innerText.slice(0, 300) };
      return {
        table: true,
        headers: [...tb.tHead.rows[0].cells].map((c) => c.innerText.trim()),
        rows: [...tb.tBodies[0].rows].map((r) => [...r.cells].map((c) => c.innerText.trim().replace(/\s+/g, ' ').slice(0, 46))),
      };
    });
    R.shots.push(await s.shot('p360-01-project-header'));

    // VIS-02: صفحة 360 — الكسل: لا نداء لشريحة المشروع قبل فتح التبويب، ونداء واحد عنده
    calls.length = 0;
    await s.go(`${BASE}/app/projects/${pid}/360`, 6000);
    const rx = /\/projects\/[^/]+\/(reports|submissions)/;
    R.vis02_callsBeforeTab = calls.filter((u) => rx.test(u)).length;
    R.vis02_tabs = await s.ev(() => [...document.querySelectorAll('button,[role=tab]')]
      .map((x) => x.textContent.trim()).filter((t) => t && t.length < 30).slice(0, 20));
    R.shots.push(await s.shot('p360-02a-360-before-tab'));
    R.vis02_tabOpened = await s.ev(() => {
      const t = [...document.querySelectorAll('button,[role=tab]')]
        .find((x) => /التقارير المرتبطة/.test(x.textContent.trim()));
      if (!t) return false; t.click(); return true;
    });
    await sleep(5000); await s.ev(HELPERS);
    R.vis02_callsAfterTab = calls.filter((u) => rx.test(u)).length;
    R.vis02_slice = await s.ev(() => (document.querySelector('main') || document.body).innerText
      .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 40));
    R.shots.push(await s.shot('p360-02b-linked-reports-slice'));

    // VIS-03: مشروع خارج النطاق ⟹ سطح خطأ لا دوّار أبديّ
    const t0 = Date.now();
    await s.go(`${BASE}/app/projects/${STATE.outOfScopeProjectId}`, 1500);
    const settled = await s.until(() => {
      const t = (document.querySelector('main') || document.body).innerText;
      const spinning = /يتم تحميل البيانات|قد يستغرق ذلك لحظات/.test(t);
      const err = /غير مصرّح|لا تملك|غير موجود|تعذّر|خطأ|404|403/.test(t);
      return (!spinning && t.trim().length > 0) || err ? { spinning, err, text: t.split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 10) } : null;
    }, null, 20000);
    R.vis03 = { elapsedMs: Date.now() - t0, settled };
    await sleep(1500);
    R.vis03.finalText = await s.ev(() => (document.querySelector('main') || document.body).innerText
      .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 12));
    R.vis03.stillSpinning = JSON.stringify(R.vis03.finalText).includes('يتم تحميل البيانات');
    R.shots.push(await s.shot('p360-03-out-of-scope-ERROR-SURFACE'));
    R.status = 'DONE';
  } catch (e) { R.status = 'ERROR'; R.error = String(e).slice(0, 300); try { R.shots.push(await s.shot('p360-99-error')); } catch { /* ignore */ } }
  R.consoleErrors = s.consoleErrors.slice(0, 12); R.apiFailures = s.apiFailures.slice(0, 25);
  out.p360 = R; save(); console.error('>>> p360', R.status, R.error || '');
  await s.ctx.close();
}

// ===================== PART=visual — مصفوفة الأسطح البصريّة (EVID-01) =====================
// سطح الخطأ يُلتقَط فعلًا لا يُدَّعى؛ والتحميل يُلتقَط بخنق الشبكة لا بالتمنّي.
if (PART === 'visual') {
  const VIEWPORTS = [['desktop', { width: 1440, height: 1080 }], ['mobile', { width: 390, height: 844 }]];
  for (const [vp, size] of VIEWPORTS) {
    const s = await open(size);
    const R = { viewport: vp, shots: [], overflow: {} };
    try {
      await s.login(STATE.employees.content.email);
      const pid = STATE.employees.content.projectId;
      const pages = [
        ['home', `${BASE}/app`], ['submissions', `${BASE}/app/submissions`],
        ['project', `${BASE}/app/projects/${pid}`], ['project360', `${BASE}/app/projects/${pid}/360`],
      ];
      for (const [name, url] of pages) {
        await s.go(url, 5000);
        R.shots.push(await s.shot(`visual-${vp}-${name}`));
        // تجاوز أفقيّ = كسر تخطيط فعليّ لا انطباع
        R.overflow[name] = await s.ev(() => ({
          scrollW: document.documentElement.scrollWidth, clientW: document.documentElement.clientWidth,
          dir: document.documentElement.dir || getComputedStyle(document.body).direction,
        }));
      }
      // سطح الخطأ (EVID-01)
      await s.go(`${BASE}/app/projects/${STATE.outOfScopeProjectId}`, 4000);
      R.errorSurface = await s.ev(() => (document.querySelector('main') || document.body).innerText
        .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 8));
      R.shots.push(await s.shot(`visual-${vp}-error-surface`));
      // سطح الفراغ
      await s.go(`${BASE}/app/projects/00000000-0000-0000-0000-000000000000`, 4000);
      R.emptySurface = await s.ev(() => (document.querySelector('main') || document.body).innerText
        .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 8));
      R.shots.push(await s.shot(`visual-${vp}-empty-surface`));
      // سطح التحميل — تأخير نداءات /api وحدها.
      // خنق الشبكة العامّ (`Network.emulateNetworkConditions`) يجوّع تحميل الخطوط أيضًا،
      // فتسقط `page.screenshot` بـ«waiting for fonts to load» ولا يُلتقط السطح أصلًا.
      // التأخير مقصور على نداءات المشروع وحدها: تأخير `/api/**` كلّه يحبس تمهيد الغلاف
      // (auth/me) فيُلتقط سبينر الإقلاع لا سطح تحميل الصفحة المقصود.
      await s.page.route('**/api/projects/**', async (route) => { await sleep(9000); await route.continue(); });
      await s.page.goto(`${BASE}/app/projects/${pid}`, { waitUntil: 'domcontentloaded' }).catch(() => {});
      await sleep(3000);
      R.loadingSurface = await s.page.evaluate(() => (document.querySelector('main') || document.body).innerText
        .split('\n').map((x) => x.trim()).filter(Boolean).slice(0, 8)).catch(() => null);
      R.loadingHasSpinner = await s.page.evaluate(() => !!document.querySelector('[class*=animate-spin],[role=status],[aria-busy=true]')).catch(() => null);
      R.shots.push(await s.shot(`visual-${vp}-loading-surface`));
      await s.page.unroute('**/api/projects/**');
      R.status = 'DONE';
    } catch (e) { R.status = 'ERROR'; R.error = String(e).slice(0, 300); }
    R.consoleErrors = s.consoleErrors.slice(0, 12); R.apiFailures = s.apiFailures.slice(0, 25);
    out[vp] = R; save(); console.error('>>> visual', vp, R.status, R.error || '');
    await s.ctx.close();
  }
}

await browser.close();
console.log('WROTE', RESULTS);

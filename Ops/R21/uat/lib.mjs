/**
 * PROJECT360-R2-UAT-EVIDENCE-AND-GUIDE-CLOSURE-R2.1 — أدوات مشتركة لتنفيذ UAT بمتصفّح حقيقيّ.
 *
 * الهدف: تشغيل الرحلة على النطاق العامّ لبيئة TEST نفسها (`https://test.emarketingacademy.net`)
 * لا على نسخة محلّيّة، لأنّ §12 تشترط متصفّحًا حقيقيًّا على البيئة المنشورة.
 *
 * الأسرار: تُقرأ من ملفّ خارج المستودع بصلاحيّة 0600 ولا تُطبع ولا تُكتب في أيّ مخرَج.
 */
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '../../../reporting-frontend/node_modules/playwright-core/index.mjs';

export const BASE = process.env.R21_BASE || 'https://test.emarketingacademy.net';
export const SECRETS_FILE = process.env.R21_SECRETS || '/tmp/r21-secrets/r21.env';
export const OUT_ROOT = process.env.R21_OUT
  || path.resolve(process.cwd(), 'Docs/Guides/Marketing-Experts-Client360-Project360-Guide-Assets-R2.1');
export const EVIDENCE_ROOT = process.env.R21_EVIDENCE || path.resolve(process.cwd(), 'Ops/R21/uat/evidence');

export const TAG = 'P360-R21-UAT';
export const NAMES = {
  client: `${TAG}-CLIENT`,
  project: `${TAG}-PROJECT`,
  workstream: `${TAG}-WORKSTREAM`,
  deliverableA: `${TAG}-DELIVERABLE-A`,
  deliverableB: `${TAG}-DELIVERABLE-B`,
  kpi: `${TAG}-KPI`,
};

export const PERSONAS = {
  ADMIN: { email: 'ceo@uat.local', label: 'الرئيس التنفيذيّ (إدارة عليا)' },
  GM: { email: 'gm@uat.local', label: 'المدير العامّ' },
  OWNER: { email: 'lead@uat.local', label: 'مالك المشروع (مُسنَد بالتعيين لا بالدور)' },
  TL: { email: 'team.leader@uat.local', label: 'قائد الفريق المُسنَد' },
  EMP: { email: 'emp1@uat.local', label: 'عضو الفريق' },
  OUT_EMP: { email: 'employee@uat.local', label: 'موظّف خارج الفريق' },
  AM: { email: 'account.manager@uat.local', label: 'مدير العميل' },
  VIEWER: { email: 'viewer@uat.local', label: 'مُطّلع' },
};

export function readSecrets() {
  const raw = fs.readFileSync(SECRETS_FILE, 'utf8');
  const out = {};
  for (const line of raw.split('\n')) {
    const t = line.trim();
    if (!t || t.startsWith('#') || t.startsWith('---')) continue;
    const i = t.indexOf('=');
    if (i > 0) out[t.slice(0, i)] = t.slice(i + 1);
  }
  return out;
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

export async function launch(secrets) {
  const browser = await chromium.launch();
  const makeContext = async () => {
    const ctx = await browser.newContext({
      viewport: { width: 1500, height: 950 },
      locale: 'ar-SA',
      httpCredentials: { username: secrets.TEST_BASIC_USER, password: secrets.TEST_BASIC_PW },
      ignoreHTTPSErrors: false,
    });
    return ctx;
  };
  return { browser, makeContext };
}

/** يلتقط كلّ ما يثبت غياب الأخطاء: console + الطلبات الفاشلة + الطلبات المحظورة المضيف. */
export function instrument(page, sink) {
  page.on('console', (m) => {
    if (m.type() === 'error' || m.type() === 'warning') sink.console.push({ type: m.type(), text: m.text().slice(0, 400) });
  });
  page.on('pageerror', (e) => sink.console.push({ type: 'pageerror', text: String(e).slice(0, 400) }));
  page.on('requestfailed', (r) => sink.failedRequests.push({ url: r.url().slice(0, 300), err: r.failure()?.errorText }));
  page.on('response', (r) => {
    const u = r.url();
    if (/localhost|127\.0\.0\.1|rc-report\.|reports\.emarketingacademy\.net/.test(u)) sink.forbiddenHosts.push(u.slice(0, 300));
    if (r.status() >= 400) sink.httpErrors.push({ url: u.slice(0, 300), status: r.status() });
  });
}

export function newSink() {
  return { console: [], failedRequests: [], forbiddenHosts: [], httpErrors: [] };
}

export async function login(page, email, pw) {
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(900);
  await page.fill('input[type=email]', email);
  await page.fill('input[type=password]', pw);
  await Promise.all([
    page.waitForURL(/\/app/, { timeout: 25000 }).catch(() => {}),
    page.click('button[type=submit]'),
  ]);
  await page.waitForTimeout(2000);
  return page.url();
}

export async function goto(page, url, waitMs = 2000) {
  await page.goto(url.startsWith('http') ? url : `${BASE}${url}`, { waitUntil: 'domcontentloaded' }).catch(() => {});
  await page.waitForTimeout(waitMs);
}

let shotSeq = 0;
export const shots = [];

export async function shot(page, dir, name, caption, meta = {}) {
  fs.mkdirSync(dir, { recursive: true });
  shotSeq += 1;
  const file = `${String(shotSeq).padStart(2, '0')}-${name}.png`;
  await page.waitForTimeout(700);
  await page.screenshot({ path: path.join(dir, file), fullPage: true });
  shots.push({ n: shotSeq, file, caption, ...meta });
  return file;
}

export function writeJson(file, data) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
}

/** ينقر زرًّا/رابطًا بنصّه المرئيّ (أوّل تطابق ظاهر). */
export async function clickText(page, selector, re, { timeout = 8000 } = {}) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const els = await page.$$(selector);
    for (const el of els) {
      const txt = ((await el.textContent()) || '').trim();
      if (re.test(txt) && (await el.isVisible())) { await el.click(); return txt; }
    }
    await page.waitForTimeout(300);
  }
  throw new Error(`clickText: no visible ${selector} matching ${re}`);
}

export async function textOf(page, selector) {
  return page.$$eval(selector, (els) => els.map((e) => (e.textContent || '').trim()).filter(Boolean));
}

/**
 * فحص الخادم **من داخل جلسة المتصفّح نفسها** (نفس الرمز ونفس الأصل): إخفاء الزرّ ليس حماية،
 * فالحكم يقع على ردّ الـAPI لا على ظهور العنصر. يعيد الحالة ورمز الخطأ بلا طباعة أيّ سرّ.
 */
export async function apiProbe(page, method, urlPath, body = null) {
  return page.evaluate(async ([m, u, b]) => {
    const token = localStorage.getItem('me_access');
    const res = await fetch(`${window.location.origin}/api${u}`, {
      method: m,
      headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
      body: b === null ? undefined : JSON.stringify(b),
    });
    let payload = null;
    try { payload = await res.json(); } catch { payload = null; }
    // شكل الخطأ الموحّد `ProblemDetails`: `type` يحمل رمز الخطأ و`detail` الرسالة.
    return { status: res.status, code: payload?.type ?? null, detail: (payload?.detail ?? '').slice(0, 160) };
  }, [method, urlPath, body]);
}

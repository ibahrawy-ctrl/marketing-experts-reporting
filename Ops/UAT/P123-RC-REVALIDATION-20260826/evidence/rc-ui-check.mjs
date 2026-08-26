// Phase 9d — فحص واجهة RC على المرشّح 897c9b18.
//
// الحزمة المنشورة على RC تحمل `VITE_API_BASE_URL=https://rc-report.emarketingacademy.net/api`
// مخبوزًا فيها، ونجينكس أمامها يفرض auth_basic. فبدل تجاوز الحارس أو تسريب كلمة مروره،
// يُعترَض **كلّ** طلب إلى ذلك الأصل داخل المتصفّح ويُلبّى محلّيًّا:
//   • ‎/api‎ و‎/hubs‎ → خلفيّة RC الحيّة عبر نفق SSH على 127.0.0.1:5092
//   • ما عداه       → ملفّات dist المنسوخة حرفيًّا من /opt/reporting-rc/frontend/dist
// النتيجة: نفس الأصل تمامًا (لا CORS)، ونفس الحزمة، ونفس قاعدة البيانات — بلا لمس الخادم.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const ORIGIN = 'https://rc-report.emarketingacademy.net';
const API = 'http://127.0.0.1:15092';
const DIST = '/tmp/rc-ui/dist';
const OUT = process.argv[2];
const PW = 'RcP123#Synthetic!2026';

const ROLES = [
  { key: 'admin', email: 'rc-admin@p123.rc.test' },
  { key: 'hr', email: 'rc-hr@p123.rc.test' },
  { key: 'manager', email: 'rc-mgr@p123.rc.test' },
  { key: 'employee', email: 'rc-emp@p123.rc.test' },
];
const VIEWPORTS = [390, 768, 1440];
const ROUTES = ['/app', '/app/attendance', '/app/projects'];

// ضجيج الوسيط لا المنتج: WebSocket لا يمرّ عبر اعتراض HTTP.
const PROXY_NOISE = /websocket|signalr|\/hubs\//i;

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml',
  '.png': 'image/png', '.json': 'application/json', '.ico': 'image/x-icon',
};

async function installRouting(ctx) {
  await ctx.route(`${ORIGIN}/**`, async route => {
    const url = new URL(route.request().url());
    if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
      const req = route.request();
      try {
        const r = await fetch(API + url.pathname + url.search, {
          method: req.method(),
          headers: Object.fromEntries(Object.entries(req.headers())
            .filter(([k]) => ['content-type', 'authorization', 'accept'].includes(k))),
          body: ['GET', 'HEAD'].includes(req.method()) ? undefined : req.postData() ?? undefined,
        });
        return route.fulfill({
          status: r.status,
          contentType: r.headers.get('content-type') ?? 'application/json',
          body: Buffer.from(await r.arrayBuffer()),
        });
      } catch (e) {
        return route.fulfill({ status: 502, body: String(e) });
      }
    }
    let rel = url.pathname.replace(/^\//, '');
    let file = path.join(DIST, rel);
    if (!rel || !fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(DIST, 'index.html');
    return route.fulfill({
      status: 200,
      contentType: MIME[path.extname(file)] ?? 'application/octet-stream',
      body: fs.readFileSync(file),
    });
  });
}

const log = [];
const consoleErrors = [];

async function shoot(page, name, viewport, meta) {
  await page.setViewportSize({ width: viewport, height: 900 });
  await page.waitForTimeout(700);
  const m = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    dir: document.documentElement.getAttribute('dir'),
    nav: Array.from(document.querySelectorAll('nav a, aside a'))
      .map(a => a.textContent.trim()).filter(Boolean).slice(0, 24),
    head: document.body.innerText.replace(/\s+/g, ' ').slice(0, 110),
  }));
  const file = `${name}-${viewport}.png`;
  await page.screenshot({ path: `${OUT}/${file}` });
  log.push({ ...meta, viewport, file, dir: m.dir, horizontalOverflow: m.overflow, navItems: m.nav, bodyHead: m.head });
  console.log(`[shot] ${file} dir=${m.dir} overflow=${m.overflow} nav=${m.nav.length}`);
}

const browser = await chromium.launch({ ignoreHTTPSErrors: true });

{
  const ctx = await browser.newContext({ locale: 'ar-SA', ignoreHTTPSErrors: true });
  await installRouting(ctx);
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push({ role: 'anonymous', text: m.text() }); });
  page.on('pageerror', e => consoleErrors.push({ role: 'anonymous', text: 'pageerror: ' + e.message }));
  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  for (const v of VIEWPORTS) await shoot(page, 'login', v, { role: 'anonymous', route: '/login' });
  await ctx.close();
}

for (const role of ROLES) {
  const ctx = await browser.newContext({ locale: 'ar-SA', ignoreHTTPSErrors: true });
  await installRouting(ctx);
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push({ role: role.key, text: m.text() }); });
  page.on('pageerror', e => consoleErrors.push({ role: role.key, text: 'pageerror: ' + e.message }));

  await page.goto(`${ORIGIN}/login`, { waitUntil: 'networkidle' });
  await page.fill('input[type="email"], input[name="email"]', role.email);
  await page.fill('input[type="password"], input[name="password"]', PW);
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/app/, { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(1500);
  const landed = page.url();
  console.log(`[login] ${role.key} -> ${landed}`);

  for (const route of ROUTES) {
    await page.goto(`${ORIGIN}${route}`, { waitUntil: 'networkidle' }).catch(() => {});
    await page.waitForTimeout(700);
    for (const v of VIEWPORTS) {
      await shoot(page, `${role.key}${route.replace(/\//g, '-')}`, v, { role: role.key, route, landed });
    }
  }
  await ctx.close();
}

await browser.close();

const realErrors = consoleErrors.filter(e => !PROXY_NOISE.test(e.text));
fs.writeFileSync(`${OUT}/ui-log.json`, JSON.stringify({
  origin: ORIGIN, bundle: 'copied verbatim from /opt/reporting-rc/frontend/dist',
  shots: log,
  consoleErrorsTotal: consoleErrors.length,
  consoleErrorsProxyNoise: consoleErrors.length - realErrors.length,
  consoleErrorsReal: realErrors,
  overflowViolations: log.filter(s => s.horizontalOverflow).map(s => s.file),
  nonRtl: log.filter(s => s.dir !== 'rtl').map(s => `${s.file}:${s.dir}`),
}, null, 1));

console.log(`\nSHOTS=${log.length}`);
console.log(`LOGINS_OK=${[...new Set(log.filter(s => s.landed && /\/app/.test(s.landed)).map(s => s.role))].join(',')}`);
console.log(`CONSOLE_ERRORS_TOTAL=${consoleErrors.length} PROXY_NOISE=${consoleErrors.length - realErrors.length} REAL=${realErrors.length}`);
console.log(`OVERFLOW=${log.filter(s => s.horizontalOverflow).length}`);
console.log(`NON_RTL=${log.filter(s => s.dir !== 'rtl').length}`);
realErrors.slice(0, 8).forEach(e => console.log(`  REAL_ERR [${e.role}] ${e.text.slice(0, 170)}`));

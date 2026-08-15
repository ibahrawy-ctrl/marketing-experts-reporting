#!/usr/bin/env node
/**
 * UAT-ORGANIZATION-AND-ROLE-WORKFLOW-VALIDATION-R1 — §10 التقاط اللقطات المصوّرة لكلّ شخصية.
 *
 * لماذا محلّيًّا لا عبر النطاق العام:
 *   نطاق TEST (`test.emarketingacademy.net`) محميّ بـ nginx BasicAuth على الجذر، وكلمة الـBasicAuth
 *   غير متاحة (تجزئة فقط في `/etc/nginx/.htpasswd-rc-test`). لذلك يُقدَّم **نفس** بناء الواجهة الحقيقيّ
 *   المأخوذ من `/opt/reporting-test/frontend/dist` عبر خادم محلّيّ مؤقّت، و`/api` تُمرَّر عبر نفق SSH
 *   إلى `127.0.0.1:5091` (خدمة TEST نفسها، قاعدة `reporting_test_uat`). لا تعديل على الخادم ولا نشر.
 *
 * الأسرار: كلمات المرور تُقرأ من **stdin** (يُمرَّر محتوى `/root/uat-prep-runtime/uat-role-accounts.env`
 * عبر SSH مباشرةً) ولا تُكتب على القرص محلّيًّا ولا تُطبع إطلاقًا.
 *
 * الاستعمال:
 *   ssh <host> 'cat /root/uat-prep-runtime/uat-role-accounts.env' | node 11-uat-role-screenshots.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '../../reporting-frontend/node_modules/playwright-core/index.mjs';

const BASE = process.env.UAT_SHOT_BASE || 'http://127.0.0.1:8099';
const OUT = process.env.UAT_SHOT_OUT || path.resolve(process.cwd(), 'Docs/UAT/RoleWorkflowValidation');
const ALPHA = process.env.UAT_CLIENT_ALPHA || 'cc877dc2-dc6d-4fb1-97e0-fb84a1714571';
const BETA = process.env.UAT_CLIENT_BETA || '91ab4f03-e11d-40c4-9309-bb2b9c1b4e74';
const LOGIN_GAP_MS = Number(process.env.UAT_LOGIN_GAP_MS || 3500);

const PERSONAS = [
  { key: 'CEO', code: 'CEO', email: 'ceo@uat.local', label: 'الرئيس التنفيذي UAT' },
  { key: 'GM', code: 'GENERAL_MANAGER', email: 'gm@uat.local', label: 'المدير العام UAT' },
  { key: 'OPS_MGR', code: 'MANAGER', email: 'ops.manager@uat.local', label: 'مدير التشغيل UAT' },
  { key: 'AM', code: 'ACCOUNT_MGR', email: 'account.manager@uat.local', label: 'مدير العميل UAT' },
  { key: 'TL', code: 'TEAM_LEADER', email: 'team.leader@uat.local', label: 'قائد الفريق UAT' },
  { key: 'EMP', code: 'EMPLOYEE', email: 'employee@uat.local', label: 'موظف UAT' },
  { key: 'HR', code: 'HR_SPECIALIST', email: 'hr.manager@uat.local', label: 'أخصائي الموارد البشرية UAT' },
  { key: 'FIN_MGR', code: 'FINANCE_MANAGER', email: 'finance.manager@uat.local', label: 'المدير المالي UAT' },
  { key: 'FIN_EMP', code: 'FINANCE_EMPLOYEE', email: 'finance.employee@uat.local', label: 'موظف مالي UAT' },
  { key: 'SALES', code: 'SALES_EMPLOYEE', email: 'sales.employee@uat.local', label: 'موظف مبيعات UAT' },
  { key: 'VIEWER', code: 'VIEWER', email: 'viewer@uat.local', label: 'مُطّلع UAT' },
];

function readSecrets() {
  const raw = fs.readFileSync(0, 'utf8');
  const out = {};
  for (const line of raw.split('\n')) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const i = t.indexOf('=');
    if (i > 0) out[t.slice(0, i)] = t.slice(i + 1);
  }
  return out;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function shoot(page, dir, name) {
  await page.waitForTimeout(1200);
  await page.screenshot({ path: path.join(dir, name), fullPage: true });
}

async function goto(page, url) {
  await page.goto(`${BASE}${url}`, { waitUntil: 'domcontentloaded' }).catch(() => {});
  await page.waitForTimeout(1800);
}

const summary = { generatedAtUtc: new Date().toISOString(), base: BASE, personas: {} };

const secrets = readSecrets();
const browser = await chromium.launch();

for (const p of PERSONAS) {
  const dir = path.join(OUT, p.code);
  fs.mkdirSync(dir, { recursive: true });
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'ar-SA' });
  const page = await ctx.newPage();
  const rec = { email: p.email, label: p.label, shots: [], notes: [] };

  // 01 — صفحة تسجيل الدخول
  await goto(page, '/login');
  await shoot(page, dir, '01-login.png');
  rec.shots.push('01-login.png');

  // مباعدة: /api/auth/login محكوم بـ 30 طلبًا/60 ثانية لكلّ IP
  await sleep(LOGIN_GAP_MS);
  const pw = secrets[`UAT_PW_${p.key}`];
  if (!pw) { rec.notes.push('no-password'); summary.personas[p.key] = rec; await ctx.close(); continue; }

  await page.fill('input[type=email]', p.email);
  await page.fill('input[type=password]', pw);
  await Promise.all([
    page.waitForURL(/\/app/, { timeout: 20000 }).catch(() => {}),
    page.click('button[type=submit]'),
  ]);
  await page.waitForTimeout(2500);
  rec.landedUrl = page.url().replace(BASE, '');

  // 02 — لوحة التحكم
  await goto(page, '/app/');
  await shoot(page, dir, '02-dashboard.png');
  rec.shots.push('02-dashboard.png');

  // 03 — قائمة التنقّل (عنصر الشريط الجانبيّ إن وُجد، وإلا الصفحة كاملة)
  const navEl = await page.$('aside') || await page.$('nav');
  if (navEl) await navEl.screenshot({ path: path.join(dir, '03-navigation.png') });
  else await shoot(page, dir, '03-navigation.png');
  rec.shots.push('03-navigation.png');
  rec.navItems = await page.$$eval('aside a, aside button, nav a, nav button', (els) =>
    [...new Set(els.map((e) => (e.textContent || '').trim()).filter(Boolean))]);

  // 04 — محفظة العملاء (Account Portfolio)
  await goto(page, '/app/account-portfolio');
  await shoot(page, dir, '04-portfolio.png');
  rec.shots.push('04-portfolio.png');

  // 05 — العميل الظاهر (ألفا)
  await goto(page, p.key === 'AM' ? `/app/account-portfolio/clients/${ALPHA}` : `/app/clients/${ALPHA}`);
  await shoot(page, dir, '05-client-a.png');
  rec.shots.push('05-client-a.png');

  // 06 — العميل غير المُسنَد (بيتا) — يُتوقَّع منع لغير المصرَّح لهم
  await goto(page, p.key === 'AM' ? `/app/account-portfolio/clients/${BETA}` : `/app/clients/${BETA}`);
  await shoot(page, dir, '06-client-b-denied.png');
  rec.shots.push('06-client-b-denied.png');

  // 07/08 — قوائم العملاء والمشاريع (وجود/غياب زرّ الإنشاء)
  await goto(page, '/app/clients');
  await shoot(page, dir, '07-clients-list.png');
  rec.shots.push('07-clients-list.png');
  rec.createClientButton = await page.$$eval('button', (els) =>
    els.some((e) => /إضافة عميل|عميل جديد|إنشاء عميل/.test(e.textContent || '')));

  await goto(page, '/app/projects');
  await shoot(page, dir, '08-projects-list.png');
  rec.shots.push('08-projects-list.png');
  rec.createProjectButton = await page.$$eval('button', (els) =>
    els.some((e) => /إضافة مشروع|مشروع جديد|إنشاء مشروع/.test(e.textContent || '')));

  summary.personas[p.key] = rec;
  console.log(`${p.key.padEnd(9)} shots=${rec.shots.length} landed=${rec.landedUrl || 'N/A'} nav=${(rec.navItems || []).length}`);
  await ctx.close();
}

await browser.close();
fs.writeFileSync(path.join(OUT, 'screenshot-run-summary.json'), JSON.stringify(summary, null, 2));
console.log('summary written');

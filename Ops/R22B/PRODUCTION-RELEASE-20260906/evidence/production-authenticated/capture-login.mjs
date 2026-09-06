#!/usr/bin/env node
// R22B-PROD §الدخول — التقاط جلسة إنتاج بحساب قائم.
// المالك يكتب بيانات الدخول بنفسه داخل المتصفّح. هذا السكربت:
//  - لا يقرأ ولا يطبع ولا يخزّن كلمة مرور ولا توكن ولا كوكي.
//  - يطبع فقط: الاسم/البريد/الأدوار من /auth/me لتأكيد الدور الصحيح.
//  - يحفظ ملفّ الجلسة في /private/tmp (خارج Git تمامًا).
// الاستعمال: node capture-login.mjs <profileName> [timeoutMinutes]
import { chromium } from '/private/tmp/p123-e2e/node_modules/playwright/index.mjs';
import fs from 'node:fs';

const BASE = 'https://reports.emarketingacademy.net';
const profile = process.argv[2];
const timeoutMin = Number(process.argv[3] ?? 15);
if (!profile) { console.error('USAGE: capture-login.mjs <profileName>'); process.exit(2); }
const dir = `/private/tmp/prod-auth/${profile}`;
fs.mkdirSync(dir, { recursive: true });

const ctx = await chromium.launchPersistentContext(dir, {
  headless: false,
  viewport: { width: 1440, height: 900 },
  args: ['--window-size=1460,980'],
});
const page = ctx.pages()[0] ?? (await ctx.newPage());
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });

console.log(`WAITING_FOR_OWNER_LOGIN profile=${profile} (حتّى ${timeoutMin} دقيقة)`);
const deadline = Date.now() + timeoutMin * 60_000;
let me = null;
while (Date.now() < deadline) {
  await page.waitForTimeout(2000);
  const hasToken = await page.evaluate(() => !!localStorage.getItem('me_access')).catch(() => false);
  if (!hasToken) continue;
  me = await page.evaluate(async () => {
    const t = localStorage.getItem('me_access');
    const r = await fetch('/api/auth/me', { headers: { Authorization: 'Bearer ' + t } });
    if (!r.ok) return { httpStatus: r.status };
    const d = await r.json();
    // لا يُعاد أيّ توكن — الحقول الهويّاتيّة فقط.
    return { httpStatus: 200, id: d.id ?? d.userId, fullName: d.fullName, email: d.email, roles: d.roles, scopeType: d.scopeType };
  }).catch(() => null);
  if (me?.httpStatus === 200) break;
}
if (!me || me.httpStatus !== 200) {
  console.log('LOGIN_CAPTURE=TIMEOUT');
  await ctx.close();
  process.exit(1);
}
fs.writeFileSync(`/private/tmp/prod-auth/${profile}.identity.json`, JSON.stringify(me, null, 1));
console.log(`LOGIN_CAPTURE=OK profile=${profile} name=${me.fullName} email=${me.email} roles=${JSON.stringify(me.roles)} scope=${me.scopeType}`);
await ctx.close();
process.exit(0);

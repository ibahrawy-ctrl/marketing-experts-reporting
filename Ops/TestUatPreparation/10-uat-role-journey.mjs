#!/usr/bin/env node
/**
 * 10-uat-role-journey.mjs
 * UAT-ORGANIZATION-AND-ROLE-WORKFLOW-VALIDATION-R1 — §8/§9 رحلة استخدام فعليّة لكلّ حساب.
 *
 * قراءة-فقط عمليًّا:
 *  - كلّ فحوص القراءة GET.
 *  - فحوص «الكتابة» تُختبر تصريحًا فقط عبر مُميِّز 403-مقابل-400: يُرسَل جسم غير صالح ({})،
 *    فمن يُمنع بالسياسة/الحارس يحصل على 403، ومن يُسمَح له يصل إلى التحقّق فيحصل على 400 ⇒ لا تُنشأ بيانات.
 *  - PUT على كيان قائم يُرسَل بالقيم نفسها (لا تغيير فعليّ) لإثبات صلاحية التعديل.
 *
 * لا يطبع أيّ كلمة مرور أو توكن.
 */
import { readFileSync, writeFileSync } from 'node:fs';

const API_BASE = process.env.UAT_API_BASE || 'http://127.0.0.1:5091/api';
const TEST_ENV_FILE = '/etc/khubara-reporting-test.env';
const ACCOUNTS_ENV_FILE = '/root/uat-prep-runtime/uat-role-accounts.env';
const OUT_JSON = process.env.UAT_JOURNEY_OUT || '/root/uat-prep-runtime/role-journey-results.json';

if (!/:5091(\/|$)/.test(API_BASE)) { console.error(`NO-GO: حارس البيئة — منفذ TEST 5091 فقط. القيمة: ${API_BASE}`); process.exit(2); }

function parseEnvFile(path) {
  const out = {};
  const raw = readFileSync(path, 'utf8');
  for (const line of raw.split('\n')) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const i = t.indexOf('=');
    if (i < 0) continue;
    let v = t.slice(i + 1).trim();
    if (v.length >= 2 && ((v[0] === '"' && v.at(-1) === '"') || (v[0] === "'" && v.at(-1) === "'"))) v = v.slice(1, -1);
    out[t.slice(0, i).trim()] = v;
  }
  return out;
}
const testEnv = parseEnvFile(TEST_ENV_FILE);
const secrets = parseEnvFile(ACCOUNTS_ENV_FILE);
if (!/reporting_test_uat/.test(testEnv['ConnectionStrings__Default'] || '')) { console.error('NO-GO: ملفّ البيئة لا يشير إلى reporting_test_uat'); process.exit(2); }

async function api(method, path, { body, token } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(API_BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  if (text) { try { json = JSON.parse(text); } catch { /* خام */ } }
  return { status: res.status, ok: res.ok, body: json, type: json?.type || null };
}
async function login(email, password) {
  const r = await api('POST', '/auth/login', { body: { email, password } });
  return r.ok && r.body?.accessToken ? r.body.accessToken : null;
}

const PERSONAS = [
  { key: 'CEO',     email: 'ceo@uat.local',              label: 'الرئيس التنفيذي' },
  { key: 'GM',      email: 'gm@uat.local',               label: 'المدير العام' },
  { key: 'OPS_MGR', email: 'ops.manager@uat.local',      label: 'مدير التشغيل' },
  { key: 'AM',      email: 'account.manager@uat.local',  label: 'مدير العميل' },
  { key: 'TL',      email: 'team.leader@uat.local',      label: 'قائد فريق' },
  { key: 'EMP',     email: 'employee@uat.local',         label: 'موظف تنفيذ' },
  { key: 'HR',      email: 'hr.manager@uat.local',       label: 'مسؤول الموارد البشرية' },
  { key: 'FIN_MGR', email: 'finance.manager@uat.local',  label: 'المدير المالي' },
  { key: 'FIN_EMP', email: 'finance.employee@uat.local', label: 'موظف مالي' },
  { key: 'SALES',   email: 'sales.employee@uat.local',   label: 'موظف مبيعات' },
  { key: 'VIEWER',  email: 'viewer@uat.local',           label: 'مستخدم مطّلع' },
];

// ===== اكتشاف المعرّفات عبر الأدمن (قراءة فقط) =====
const adminToken = await login(testEnv['Seed__AdminEmail'], testEnv['Seed__AdminPassword']);
if (!adminToken) { console.error('NO-GO: تعذّر دخول الأدمن'); process.exit(2); }

const clientsR = await api('GET', '/clients?includeClosed=true', { token: adminToken });
const projectsR = await api('GET', '/projects?includeClosed=true', { token: adminToken });
const byName = (arr, n) => (arr || []).find((x) => x.name === n);
const ALPHA = byName(clientsR.body, 'عميل UAT ألفا');
const BETA = byName(clientsR.body, 'عميل UAT بيتا');
const DIRECT = byName(clientsR.body, 'عميل UAT الإدارة المباشرة');
const PROJ_A = byName(projectsR.body, 'مشروع UAT سوشيال نشط');
const PROJ_B = byName(projectsR.body, 'مشروع UAT سيو نشط');
if (!ALPHA || !BETA || !DIRECT || !PROJ_A || !PROJ_B) { console.error('NO-GO: تعذّر حلّ معرّفات العملاء/المشاريع'); process.exit(2); }

const alphaContacts = (await api('GET', `/clients/${ALPHA.id}/contacts?includeInactive=true`, { token: adminToken })).body || [];
const alphaChannels = (await api('GET', `/clients/${ALPHA.id}/digital-channels?includeInactive=true`, { token: adminToken })).body || [];
const alphaBrand = (await api('GET', `/clients/${ALPHA.id}/brand`, { token: adminToken })).body || null;
const betaContacts = (await api('GET', `/clients/${BETA.id}/contacts?includeInactive=true`, { token: adminToken })).body || [];
const CONTACT_A = alphaContacts[0];
const CHANNEL_A = alphaChannels[0];
const CONTACT_B = betaContacts[0];

/**
 * صدى أمين لكيان قائم: ينسخ **كلّ** حقول DTO المُرجَعة من GET عدا المعرّفات والطوابع الزمنيّة.
 * ضروريّ لأن PUT على هذه المسارات دلالته **استبدال كامل** لا دمج جزئيّ؛ أيّ حقل يُحذف من الجسم
 * يُمسَح من القاعدة. بناء الصدى يدويًّا بأسماء حقول مخمَّنة يُتلِف البيانات.
 */
function echo(dto) {
  const skip = new Set(['id', 'clientId', 'createdAtUtc', 'updatedAtUtc']);
  const out = {};
  for (const [k, v] of Object.entries(dto)) if (!skip.has(k)) out[k] = v;
  return out;
}

// ===== مصفوفة الفحوص =====
function probes(t) {
  const noop = {}; // جسم غير صالح — مُميِّز 403/400 بلا إنشاء بيانات
  const contactEcho = CONTACT_A && echo(CONTACT_A);
  const channelEcho = CHANNEL_A && echo(CHANNEL_A);
  const brandEcho = alphaBrand && echo(alphaBrand);

  return [
    // — تنقّل/لوحات —
    ['nav.dashboard_me',        'GET',  '/dashboard/me'],
    ['nav.exec_overview',       'GET',  '/dashboard/overview'],
    ['nav.notifications',       'GET',  '/notifications'],
    // — العملاء والمشاريع (النطاق العام) —
    ['clients.list',            'GET',  '/clients'],
    ['clients.alpha_read',      'GET',  `/clients/${ALPHA.id}`],
    ['clients.beta_read',       'GET',  `/clients/${BETA.id}`],
    ['clients.direct_read',     'GET',  `/clients/${DIRECT.id}`],
    ['projects.list',           'GET',  '/projects'],
    ['projects.a_read',         'GET',  `/projects/${PROJ_A.id}`],
    ['projects.b_read',         'GET',  `/projects/${PROJ_B.id}`],
    // — محفظة مدير العميل (§8) —
    ['portfolio.projects',      'GET',  '/account-portfolio/projects'],
    ['portfolio.clients',       'GET',  '/account-portfolio/clients'],
    ['portfolio.client_alpha',  'GET',  `/account-portfolio/clients/${ALPHA.id}`],
    ['portfolio.client_beta',   'GET',  `/account-portfolio/clients/${BETA.id}`],
    ['portfolio.project_a',     'GET',  `/account-portfolio/projects/${PROJ_A.id}`],
    ['portfolio.project_b',     'GET',  `/account-portfolio/projects/${PROJ_B.id}`],
    // — أبناء ملفّ العميل (قراءة) —
    ['alpha.contacts_read',     'GET',  `/clients/${ALPHA.id}/contacts`],
    ['alpha.channels_read',     'GET',  `/clients/${ALPHA.id}/digital-channels`],
    ['alpha.brand_read',        'GET',  `/clients/${ALPHA.id}/brand`],
    ['beta.contacts_read',      'GET',  `/clients/${BETA.id}/contacts`],
    // — أبناء ملفّ العميل (كتابة فعليّة بقيم مطابقة — لا تغيير) —
    ...(contactEcho && CONTACT_A ? [['alpha.contact_edit', 'PUT', `/clients/${ALPHA.id}/contacts/${CONTACT_A.id}`, contactEcho]] : []),
    ...(channelEcho && CHANNEL_A ? [['alpha.channel_edit', 'PUT', `/clients/${ALPHA.id}/digital-channels/${CHANNEL_A.id}`, channelEcho]] : []),
    ...(brandEcho ? [['alpha.brand_edit', 'PUT', `/clients/${ALPHA.id}/brand`, brandEcho]] : []),
    // — IDOR: كتابة على عميل غير مُسنَد —
    // الجسم = صدى جهة اتصال **بيتا نفسها** لا ألفا: من يُمنَع يحصل على 403، ومن يُسمَح له يكتب القيم ذاتها بلا تغيير.
    ...(CONTACT_B ? [['beta.contact_edit_idor', 'PUT', `/clients/${BETA.id}/contacts/${CONTACT_B.id}`, echo(CONTACT_B)]] : []),
    // — تصريح الكتابة الجوهريّة (مُميِّز 403/400 بلا إنشاء) —
    ['clients.core_edit',       'PUT',  `/clients/${ALPHA.id}`, noop],
    ['clients.create',          'POST', '/clients', noop],
    ['projects.create',         'POST', '/projects', noop],
    ['alpha.contact_create',    'POST', `/clients/${ALPHA.id}/contacts`, noop],
    ['beta.contact_create_idor','POST', `/clients/${BETA.id}/contacts`, noop],
    // — الدليل والحوكمة —
    ['directory.users',         'GET',  '/directory/users'],
    ['directory.user_create',   'POST', '/directory/users', noop],
    ['directory.job_roles',     'GET',  '/directory/job-roles'],
    // — التقارير والاعتماد —
    ['reports.templates',       'GET',  '/report-templates'],
    ['subs.mine',               'GET',  '/submissions/mine'],
    ['subs.pending_approvals',  'GET',  '/submissions/pending-approvals'],
    // — KPI —
    ['kpi.evaluatable',         'GET',  '/kpi-evaluations/evaluatable-subjects'],
    // — الإجازات/الأرصدة/المالية —
    ['leave.my',                'GET',  '/leave-requests/my'],
    ['leave.pending',           'GET',  '/leave-requests/pending'],
    ['balances.my',             'GET',  '/me/balances'],
    ['balances.employees',      'GET',  '/balances/employees'],
    ['payroll.impacts',         'GET',  '/payroll/leave-impacts'],
    // — الحوكمة الإدارية —
    ['positions.list',          'GET',  '/positions'],
    ['audit.list',              'GET',  '/audit'],
  ];
}

const results = { generatedAtUtc: new Date().toISOString(), apiBase: API_BASE, entities: {
  clientAlpha: ALPHA.id, clientBeta: BETA.id, clientDirect: DIRECT.id, projectA: PROJ_A.id, projectB: PROJ_B.id,
  contactAlpha: CONTACT_A?.id || null, channelAlpha: CHANNEL_A?.id || null, contactBeta: CONTACT_B?.id || null,
}, personas: {} };

// مباعدة الدخول: /api/auth/login محكوم بـ RateLimiting:AuthPermitLimit=30 لكلّ WindowSeconds=60 لكلّ IP.
// بلا مباعدة، تشغيلات متتالية تستنفد الحصّة فيرجع 429 ويُقرَأ خطأً كأنّه فشل كلمة مرور.
const LOGIN_GAP_MS = Number(process.env.UAT_LOGIN_GAP_MS || 3000);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

for (const p of PERSONAS) {
  await sleep(LOGIN_GAP_MS);
  const pw = secrets[`UAT_PW_${p.key}`];
  const loginRes = pw ? await api('POST', '/auth/login', { body: { email: p.email, password: pw } }) : { status: 0, body: null };
  const token = loginRes.body?.accessToken || null;
  const entry = { email: p.email, label: p.label, loginStatus: loginRes.status, probes: {} };
  if (!token) { results.personas[p.key] = entry; console.log(`${p.key.padEnd(8)} login=FAIL(${loginRes.status})`); continue; }

  const me = await api('GET', '/auth/me', { token });
  entry.roles = me.body?.roles || me.body?.user?.roles || null;

  for (const [name, method, path, body] of probes(token)) {
    const r = await api(method, path, { token, body });
    const rec = { status: r.status, type: r.type };
    if (r.ok && Array.isArray(r.body)) rec.count = r.body.length;
    if (r.ok && r.body && Array.isArray(r.body.items)) rec.count = r.body.items.length;
    if (name === 'clients.list' && Array.isArray(r.body)) rec.names = r.body.map((x) => x.name);
    if (name === 'projects.list' && Array.isArray(r.body)) rec.names = r.body.map((x) => x.name);
    if (name === 'portfolio.projects' && Array.isArray(r.body)) rec.names = r.body.map((x) => x.name || x.projectName);
    if (name === 'portfolio.clients' && Array.isArray(r.body)) rec.names = r.body.map((x) => x.name || x.clientName);
    entry.probes[name] = rec;
  }
  results.personas[p.key] = entry;
  const codes = Object.entries(entry.probes).map(([k, v]) => `${k}=${v.status}`).join(' ');
  console.log(`\n### ${p.key} (${p.email}) roles=${JSON.stringify(entry.roles)}\n${codes}`);
}

writeFileSync(OUT_JSON, JSON.stringify(results, null, 2));
console.log(`\nOUT=${OUT_JSON}`);
console.log('RESULT=DONE');

#!/usr/bin/env node
/**
 * 09-seed-uat-role-workflow.mjs
 * UAT-ORGANIZATION-AND-ROLE-WORKFLOW-VALIDATION-R1 — Fixture إعداد الهيكل التنظيمي وحسابات الأدوار
 * وبيانات العملاء/المشاريع على بيئة TEST/UAT حصرًا.
 *
 * خصائص إلزامية:
 *  - Idempotent: إعادة التشغيل لا تُنتج تكرارًا (المطابقة بمفاتيح ثابتة: البريد/الرمز/الاسم القانوني).
 *  - PLAN افتراضيًّا: لا كتابة إلا بـ --apply مع OPS_ALLOW_WRITE=1.
 *  - حارس بيئة: يرفض العمل على أيّ منفذ غير منفذ TEST (5091).
 *  - لا يطبع أيّ كلمة مرور إطلاقًا (لا في السجلّ ولا في الأخطاء ولا في قائمة العمليات).
 *  - Cleanup غير مضمَّن ولا يُنفَّذ تلقائيًّا (بأمر التذكرة §12).
 *
 * الاستخدام (على الخادم فقط):
 *    node 09-seed-uat-role-workflow.mjs                 # PLAN
 *    OPS_ALLOW_WRITE=1 node 09-seed-uat-role-workflow.mjs --apply
 */

import { readFileSync } from 'node:fs';

// ===================== إعدادات وحُرّاس =====================
const API_BASE = process.env.UAT_API_BASE || 'http://127.0.0.1:5091/api';
const TEST_ENV_FILE = process.env.UAT_TEST_ENV_FILE || '/etc/khubara-reporting-test.env';
const ACCOUNTS_ENV_FILE = process.env.UAT_ACCOUNTS_ENV_FILE || '/root/uat-prep-runtime/uat-role-accounts.env';
const APPLY = process.argv.includes('--apply');
const ALLOW_WRITE = process.env.OPS_ALLOW_WRITE === '1';

if (APPLY && !ALLOW_WRITE) {
  console.error('NO-GO: --apply يتطلّب OPS_ALLOW_WRITE=1');
  process.exit(2);
}
if (!/:5091(\/|$)/.test(API_BASE)) {
  console.error(`NO-GO: حارس البيئة — API_BASE يجب أن يكون منفذ TEST 5091. القيمة: ${API_BASE}`);
  process.exit(2);
}

const MODE = APPLY ? 'APPLY' : 'PLAN';
const counters = { Created: 0, AlreadyExists: 0, Updated: 0, Skipped: 0, Failed: 0 };
const failures = [];
const log = (...a) => console.log(...a);
const bump = (k, msg) => { counters[k]++; log(`  [${k}] ${msg}`); };
const fail = (msg, detail) => { counters.Failed++; failures.push(`${msg} :: ${detail}`); log(`  [Failed] ${msg} :: ${detail}`); };

// ===================== قراءة الملفّات السرّية =====================
function parseEnvFile(path) {
  const out = {};
  let raw;
  try { raw = readFileSync(path, 'utf8'); }
  catch (e) { console.error(`NO-GO: تعذّر قراءة ${path} (${e.code})`); process.exit(2); }
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
const ADMIN_EMAIL = testEnv['Seed__AdminEmail'];
const ADMIN_PASSWORD = testEnv['Seed__AdminPassword'];
if (!ADMIN_EMAIL || !ADMIN_PASSWORD) { console.error('NO-GO: Seed__AdminEmail/Seed__AdminPassword غير موجودين في ملفّ بيئة TEST'); process.exit(2); }
// حارس عزل إضافي: ملفّ البيئة يجب أن يشير إلى قاعدة UAT
if (!/reporting_test_uat/.test(testEnv['ConnectionStrings__Default'] || '')) {
  console.error('NO-GO: ملفّ البيئة لا يشير إلى قاعدة reporting_test_uat');
  process.exit(2);
}

// ===================== طبقة HTTP =====================
async function api(method, path, { body, token } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(API_BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  if (text) { try { json = JSON.parse(text); } catch { /* نصّ خام */ } }
  return { status: res.status, ok: res.ok, body: json, raw: text.slice(0, 400) };
}

const problem = (r) => `HTTP ${r.status} ${(r.body && (r.body.type || r.body.detail)) || r.raw}`;

async function login(email, password) {
  const r = await api('POST', '/auth/login', { body: { email, password } });
  return r.ok && r.body?.accessToken ? r.body.accessToken : null;
}

/** كتابة محكومة: في PLAN لا تُنفَّذ إطلاقًا. */
async function write(intent, fn) {
  if (!APPLY) { log(`  [PLAN] ${intent}`); return { planned: true }; }
  return fn();
}

// ===================== مواصفة البيانات (§4/§5/§7) =====================
const DEPARTMENTS = [
  { key: 'EXEC',  nameAr: 'الإدارة التنفيذية UAT',      nameEn: 'UAT Executive Department',       code: 'UAT_EXEC',  aliases: [] },
  { key: 'OPS',   nameAr: 'إدارة التشغيل UAT',           nameEn: 'UAT Operations Department',      code: 'UAT_OPS',   aliases: [] },
  { key: 'MKT',   nameAr: 'إدارة التسويق UAT',           nameEn: 'UAT Marketing Department',       code: 'UAT_MKT',   aliases: ['إدارة UAT التسويق'] },
  { key: 'HR',    nameAr: 'إدارة الموارد البشرية UAT',   nameEn: 'UAT Human Resources Department', code: 'UAT_HR',    aliases: [] },
  { key: 'FIN',   nameAr: 'الإدارة المالية UAT',         nameEn: 'UAT Finance Department',         code: 'UAT_FIN',   aliases: [] },
  { key: 'SALES', nameAr: 'إدارة المبيعات UAT',          nameEn: 'UAT Sales Department',           code: 'UAT_SALES', aliases: ['إدارة UAT المبيعات'] },
];

const TEAMS = [
  { key: 'ACCOUNTS', nameAr: 'فريق إدارة العملاء UAT',  nameEn: 'UAT Client Management Team', dept: 'OPS' },
  { key: 'CONTENT',  nameAr: 'فريق المحتوى UAT',        nameEn: 'UAT Content Team',           dept: 'MKT' },
  { key: 'DESIGN',   nameAr: 'فريق التصميم UAT',        nameEn: 'UAT Design Team',            dept: 'MKT' },
  { key: 'SOCIAL',   nameAr: 'فريق السوشيال ميديا UAT', nameEn: 'UAT Social Media Team',      dept: 'MKT' },
  { key: 'SALES',    nameAr: 'فريق المبيعات UAT',       nameEn: 'UAT Sales Team',             dept: 'SALES' },
  { key: 'HR',       nameAr: 'فريق الموارد البشرية UAT', nameEn: 'UAT HR Team',                dept: 'HR' },
  { key: 'FINANCE',  nameAr: 'فريق المالية UAT',        nameEn: 'UAT Finance Team',           dept: 'FIN' },
];

// ملاحظة §4: الرموز وظيفيّة حرفيّة (خصوصًا ACCOUNT_MGR لأن الواجهة تفحصه حرفيًّا)،
// والأسماء العربية تحمل لاحقة UAT وفق §3.
const JOB_ROLES = [
  { key: 'CEO',              nameAr: 'الرئيس التنفيذي UAT',      nameEn: 'Chief Executive Officer', code: 'CEO',              dept: 'EXEC' },
  { key: 'GENERAL_MANAGER',  nameAr: 'المدير العام UAT',          nameEn: 'General Manager',         code: 'GENERAL_MANAGER',  dept: 'EXEC' },
  { key: 'MANAGER',          nameAr: 'مدير إدارة UAT',            nameEn: 'Department Manager',      code: 'MANAGER',          dept: 'OPS' },
  { key: 'ACCOUNT_MGR',      nameAr: 'مدير العميل UAT',           nameEn: 'Account Manager',         code: 'ACCOUNT_MGR',      dept: 'OPS' },
  { key: 'TEAM_LEADER',      nameAr: 'قائد فريق UAT',             nameEn: 'Team Leader',             code: 'TEAM_LEADER',      dept: 'OPS' },
  { key: 'EMPLOYEE',         nameAr: 'موظف UAT',                  nameEn: 'Employee',                code: 'EMPLOYEE',         dept: 'OPS' },
  { key: 'HR_SPECIALIST',    nameAr: 'أخصائي موارد بشرية UAT',    nameEn: 'HR Specialist',           code: 'HR_SPECIALIST',    dept: 'HR' },
  { key: 'FINANCE_MANAGER',  nameAr: 'المدير المالي UAT',         nameEn: 'Finance Manager',         code: 'FINANCE_MANAGER',  dept: 'FIN' },
  { key: 'FINANCE_EMPLOYEE', nameAr: 'موظف مالي UAT',             nameEn: 'Finance Employee',        code: 'FINANCE_EMPLOYEE', dept: 'FIN' },
  { key: 'SALES_EMPLOYEE',   nameAr: 'موظف مبيعات UAT',           nameEn: 'Sales Employee',          code: 'SALES_EMPLOYEE',   dept: 'SALES' },
  { key: 'VIEWER',           nameAr: 'مطّلع UAT',                 nameEn: 'Viewer',                  code: 'VIEWER',           dept: 'EXEC' },
];

// الترتيب مُلزِم كي يُحلّ ManagerId من حساب أُنشئ قبله.
const USERS = [
  { key: 'CEO',      email: 'ceo@uat.local',              fullName: 'الرئيس التنفيذي UAT',        roles: ['CEO'],                            jobRole: 'CEO',              manager: null,     dept: null,    team: null },
  { key: 'GM',       email: 'gm@uat.local',               fullName: 'المدير العام UAT',            roles: ['GeneralManager'],                 jobRole: 'GENERAL_MANAGER',  manager: 'CEO',    dept: null,    team: null },
  { key: 'OPS_MGR',  email: 'ops.manager@uat.local',      fullName: 'مدير التشغيل UAT',            roles: ['Manager'],                        jobRole: 'MANAGER',          manager: 'GM',     dept: null,    team: null },
  { key: 'AM',       email: 'account.manager@uat.local',  fullName: 'مدير العميل UAT',             roles: ['Employee', 'AccountPortfolioReader'], jobRole: 'ACCOUNT_MGR',  manager: 'OPS_MGR', dept: null,   team: null },
  { key: 'TL',       email: 'team.leader@uat.local',      fullName: 'قائد فريق UAT',               roles: ['TeamLeader'],                     jobRole: 'TEAM_LEADER',      manager: 'OPS_MGR', dept: 'OPS',  team: 'ACCOUNTS' },
  { key: 'EMP',      email: 'employee@uat.local',         fullName: 'موظف تنفيذ UAT',              roles: ['Employee'],                       jobRole: 'EMPLOYEE',         manager: 'TL',     dept: 'OPS',   team: 'ACCOUNTS' },
  { key: 'HR',       email: 'hr.manager@uat.local',       fullName: 'مسؤول الموارد البشرية UAT',   roles: ['HR'],                             jobRole: 'HR_SPECIALIST',    manager: 'GM',     dept: 'HR',    team: null },
  { key: 'FIN_MGR',  email: 'finance.manager@uat.local',  fullName: 'المدير المالي UAT',           roles: ['FinanceManager'],                 jobRole: 'FINANCE_MANAGER',  manager: 'GM',     dept: 'FIN',   team: null },
  { key: 'FIN_EMP',  email: 'finance.employee@uat.local', fullName: 'موظف مالي UAT',               roles: ['Employee'],                       jobRole: 'FINANCE_EMPLOYEE', manager: 'FIN_MGR', dept: 'FIN',  team: null },
  { key: 'SALES',    email: 'sales.employee@uat.local',   fullName: 'موظف مبيعات UAT',             roles: ['Employee'],                       jobRole: 'SALES_EMPLOYEE',   manager: 'OPS_MGR', dept: 'SALES', team: null },
  { key: 'VIEWER',   email: 'viewer@uat.local',           fullName: 'مستخدم مطّلع UAT',            roles: ['Viewer'],                         jobRole: 'VIEWER',           manager: null,     dept: null,    team: null },
];

const CLIENTS = [
  {
    key: 'ALPHA', name: 'عميل UAT ألفا', accountManager: 'AM',
    core: {
      status: 'Active', tradeNameEn: 'UAT Alpha Client', legalName: 'شركة UAT ألفا للتجارة',
      clientTypeCode: 'Company', sectorCode: 'Ecommerce', country: 'SA', city: 'الرياض',
      website: 'https://alpha.uat.local', sourceCode: 'Referral', relationshipStartDate: '2026-01-05',
      mainContactName: 'جهة اتصال UAT ألفا', mainContactInfo: 'alpha.contact@uat.local',
      notes: 'عميل UAT ألفا — بيانات تجريبية لاختبار نطاق مدير العميل.',
    },
    contacts: [
      { name: 'جهة اتصال أساسية UAT ألفا', jobTitle: 'مدير التسويق', department: 'التسويق', email: 'alpha.primary@uat.local', phone: '+966500000001', whatsApp: '+966500000001', preferredContactMethodCode: 'Email', isPrimary: true, isFinancialContact: false, notes: 'جهة الاتصال الأساسية — بيانات UAT.', sortOrder: 1 },
      { name: 'جهة اتصال مالية UAT ألفا', jobTitle: 'محاسب', department: 'المالية', email: 'alpha.finance@uat.local', phone: '+966500000002', whatsApp: null, preferredContactMethodCode: 'Phone', isPrimary: false, isFinancialContact: true, notes: 'جهة الاتصال المالية — بيانات UAT.', sortOrder: 2 },
    ],
    channels: [
      { platformCode: 'Instagram',    displayName: 'UAT Alpha Instagram',   usernameOrHandle: 'uat_alpha', profileUrl: 'https://instagram.com/uat_alpha', accessStatusCode: 'FullAccess',    notes: 'قناة UAT — معرّف مرجعي فقط.', sortOrder: 1 },
      { platformCode: 'MetaBusiness', displayName: 'UAT Alpha Business Mgr', usernameOrHandle: null,       profileUrl: null,                              accessStatusCode: 'PartialAccess', notes: 'حساب أعمال UAT — بلا أسرار.', sortOrder: 2 },
    ],
    brand: {
      businessOverview: 'متجر إلكتروني تجريبي UAT لبيع المنتجات المنزلية.',
      productsAndServices: 'منتجات منزلية · أدوات مطبخ · ديكور.',
      targetAudience: 'الفئة 25–45، السعودية، اهتمام بالتسوق الإلكتروني.',
      targetLocations: 'الرياض · جدة · الدمام',
      uniqueSellingProposition: 'توصيل خلال 24 ساعة داخل المدن الرئيسية.',
      strengths: 'سرعة التوصيل · تنوّع المنتجات',
      competitors: 'منافس UAT أ · منافس UAT ب',
      toneOfVoice: 'ودّي ومباشر',
      preferredMessages: 'الجودة والسرعة',
      prohibitedMessages: 'المقارنات السعرية المباشرة',
      brandGuidelinesUrl: 'https://alpha.uat.local/brand',
      notes: 'ملفّ براند UAT تجريبي.',
    },
    projects: [
      { key: 'PROJ_A', name: 'مشروع UAT سوشيال نشط', serviceType: 'Social', status: 'Active', ownerTeam: 'ACCOUNTS', accountManager: 'AM', startDate: '2026-01-10', notes: 'المشروع أ — مُسنَد لمدير العميل تحت الاختبار.' },
    ],
  },
  {
    key: 'BETA', name: 'عميل UAT بيتا', accountManager: null,
    core: {
      status: 'Active', tradeNameEn: 'UAT Beta Client', legalName: 'شركة UAT بيتا للخدمات',
      clientTypeCode: 'Company', sectorCode: 'Services', country: 'SA', city: 'جدة',
      website: 'https://beta.uat.local', sourceCode: 'Website', relationshipStartDate: '2026-02-01',
      mainContactName: 'جهة اتصال UAT بيتا', mainContactInfo: 'beta.contact@uat.local',
      notes: 'عميل UAT بيتا — غير مُسنَد لمدير العميل تحت الاختبار (لاختبار IDOR).',
    },
    contacts: [
      { name: 'جهة اتصال أساسية UAT بيتا', jobTitle: 'مدير عام', department: 'الإدارة', email: 'beta.primary@uat.local', phone: '+966500000011', whatsApp: null, preferredContactMethodCode: 'Email', isPrimary: true, isFinancialContact: false, notes: 'جهة الاتصال الأساسية — بيانات UAT.', sortOrder: 1 },
      { name: 'جهة اتصال مالية UAT بيتا', jobTitle: 'مدير مالي', department: 'المالية', email: 'beta.finance@uat.local', phone: '+966500000012', whatsApp: null, preferredContactMethodCode: 'Email', isPrimary: false, isFinancialContact: true, notes: 'جهة الاتصال المالية — بيانات UAT.', sortOrder: 2 },
    ],
    channels: [
      { platformCode: 'Facebook', displayName: 'UAT Beta Facebook', usernameOrHandle: 'uat_beta', profileUrl: 'https://facebook.com/uat_beta', accessStatusCode: 'Requested', notes: 'قناة UAT — معرّف مرجعي فقط.', sortOrder: 1 },
    ],
    brand: {
      businessOverview: 'شركة خدمات تجريبية UAT.',
      productsAndServices: 'استشارات · صيانة · دعم فنّي.',
      targetAudience: 'الشركات الصغيرة والمتوسطة.',
      targetLocations: 'جدة · مكة',
      uniqueSellingProposition: 'استجابة خلال ساعتين.',
      strengths: 'فريق ميداني واسع',
      competitors: 'منافس UAT ج',
      toneOfVoice: 'رسمي',
      preferredMessages: 'الموثوقية',
      prohibitedMessages: 'الوعود غير القابلة للقياس',
      brandGuidelinesUrl: 'https://beta.uat.local/brand',
      notes: 'ملفّ براند UAT تجريبي.',
    },
    projects: [
      { key: 'PROJ_B', name: 'مشروع UAT سيو نشط', serviceType: 'Seo', status: 'Active', ownerTeam: 'CONTENT', accountManager: null, startDate: '2026-02-05', notes: 'المشروع ب — غير مُسنَد لمدير العميل تحت الاختبار.' },
    ],
  },
  {
    key: 'DIRECT', name: 'عميل UAT الإدارة المباشرة', accountManager: 'AM',
    core: {
      status: 'Active', tradeNameEn: 'UAT Direct Managed Client', legalName: 'مؤسسة UAT الإدارة المباشرة',
      clientTypeCode: 'Agency', sectorCode: 'Technology', country: 'SA', city: 'الخبر',
      website: 'https://direct.uat.local', sourceCode: 'Partner', relationshipStartDate: '2026-03-01',
      mainContactName: 'جهة اتصال UAT الإدارة المباشرة', mainContactInfo: 'direct.contact@uat.local',
      notes: 'عميل UAT أُنشئ من حساب إداريّ ثمّ أُسنِد لمدير العميل بعد الإنشاء.',
    },
    contacts: [
      { name: 'جهة اتصال أساسية UAT مباشرة', jobTitle: 'مالك', department: 'الإدارة', email: 'direct.primary@uat.local', phone: '+966500000021', whatsApp: '+966500000021', preferredContactMethodCode: 'WhatsApp', isPrimary: true, isFinancialContact: false, notes: 'جهة الاتصال الأساسية — بيانات UAT.', sortOrder: 1 },
      { name: 'جهة اتصال مالية UAT مباشرة', jobTitle: 'محاسب', department: 'المالية', email: 'direct.finance@uat.local', phone: '+966500000022', whatsApp: null, preferredContactMethodCode: 'Email', isPrimary: false, isFinancialContact: true, notes: 'جهة الاتصال المالية — بيانات UAT.', sortOrder: 2 },
    ],
    channels: [
      { platformCode: 'GoogleAds', displayName: 'UAT Direct Google Ads', usernameOrHandle: null, profileUrl: null, accessStatusCode: 'NoAccess', notes: 'قناة UAT — معرّف مرجعي فقط.', sortOrder: 1 },
    ],
    brand: {
      businessOverview: 'وكالة تقنية تجريبية UAT.',
      productsAndServices: 'تطوير · استضافة · دعم.',
      targetAudience: 'الشركات الناشئة.',
      targetLocations: 'الخبر · الدمام',
      uniqueSellingProposition: 'حلول جاهزة خلال أسبوع.',
      strengths: 'خبرة تقنية',
      competitors: 'منافس UAT د',
      toneOfVoice: 'تقني مبسّط',
      preferredMessages: 'السرعة في التسليم',
      prohibitedMessages: 'المصطلحات المعقّدة',
      brandGuidelinesUrl: 'https://direct.uat.local/brand',
      notes: 'ملفّ براند UAT تجريبي.',
    },
    projects: [
      { key: 'PROJ_C', name: 'مشروع UAT إدارة مباشرة', serviceType: 'Website', status: 'Active', ownerTeam: 'ACCOUNTS', accountManager: 'AM', startDate: '2026-03-05', notes: 'المشروع ج — تحت العميل المُنشأ إداريًّا.' },
    ],
  },
];

// ===================== الحالة المُحلّة =====================
const ids = { dept: {}, team: {}, jobRole: {}, user: {}, client: {}, project: {} };
const passwordStatus = {};   // key -> 'Created' | 'Validated' | 'Reset' | 'Missing' | 'Planned'

// ===================== المراحل =====================
async function stageDepartments(token) {
  log('\n--- §4/1 الإدارات (Departments) ---');
  const cur = (await api('GET', '/directory/departments', { token })).body || [];
  for (const d of DEPARTMENTS) {
    let m = cur.find(x => x.nameAr === d.nameAr)
         || cur.find(x => x.code && x.code === d.code)
         || cur.find(x => d.aliases.includes(x.nameAr));
    if (!m) {
      const r = await write(`إنشاء إدارة «${d.nameAr}»`, () => api('POST', '/directory/departments', { token, body: { nameAr: d.nameAr, nameEn: d.nameEn, code: d.code, managerId: null } }));
      if (r.planned) { counters.Created++; continue; }
      if (!r.ok) { fail(`إنشاء إدارة «${d.nameAr}»`, problem(r)); continue; }
      ids.dept[d.key] = r.body.id; bump('Created', `إدارة «${d.nameAr}»`); continue;
    }
    ids.dept[d.key] = m.id;
    const needs = m.nameAr !== d.nameAr || m.nameEn !== d.nameEn || m.code !== d.code || m.isActive !== true;
    if (!needs) { bump('AlreadyExists', `إدارة «${d.nameAr}»`); continue; }
    const r = await write(`تحديث إدارة «${m.nameAr}» ⟶ «${d.nameAr}»`, () => api('PUT', `/directory/departments/${m.id}`, { token, body: { nameAr: d.nameAr, nameEn: d.nameEn, code: d.code, managerId: m.managerId ?? null, isActive: true } }));
    if (r.planned) { counters.Updated++; continue; }
    if (!r.ok) fail(`تحديث إدارة «${d.nameAr}»`, problem(r)); else bump('Updated', `إدارة «${m.nameAr}» ⟶ «${d.nameAr}»`);
  }
}

async function stageTeams(token) {
  log('\n--- §4/2 الفرق (Teams) ---');
  const cur = (await api('GET', '/directory/teams', { token })).body || [];
  for (const t of TEAMS) {
    const deptId = ids.dept[t.dept];
    let m = cur.find(x => x.nameAr === t.nameAr);
    if (!m) {
      if (!deptId) { bump('Skipped', `فريق «${t.nameAr}» (الإدارة غير مُحلّة بعد — وضع PLAN)`); continue; }
      const r = await write(`إنشاء فريق «${t.nameAr}»`, () => api('POST', '/directory/teams', { token, body: { nameAr: t.nameAr, nameEn: t.nameEn, departmentId: deptId, teamLeaderId: null } }));
      if (r.planned) { counters.Created++; continue; }
      if (!r.ok) { fail(`إنشاء فريق «${t.nameAr}»`, problem(r)); continue; }
      ids.team[t.key] = r.body.id; bump('Created', `فريق «${t.nameAr}»`); continue;
    }
    ids.team[t.key] = m.id;
    const needs = m.nameEn !== t.nameEn || (deptId && m.departmentId !== deptId) || m.isActive !== true;
    if (!needs) { bump('AlreadyExists', `فريق «${t.nameAr}»`); continue; }
    const r = await write(`تحديث فريق «${t.nameAr}»`, () => api('PUT', `/directory/teams/${m.id}`, { token, body: { nameAr: t.nameAr, nameEn: t.nameEn, departmentId: deptId || m.departmentId, teamLeaderId: m.teamLeaderId ?? null, isActive: true, syncMemberDepartments: false } }));
    if (r.planned) { counters.Updated++; continue; }
    if (!r.ok) fail(`تحديث فريق «${t.nameAr}»`, problem(r)); else bump('Updated', `فريق «${t.nameAr}»`);
  }
}

async function stageJobRoles(token) {
  log('\n--- §4/3 المسمّيات الوظيفية (Job Roles) ---');
  const cur = (await api('GET', '/directory/job-roles/manage', { token })).body || [];
  for (const j of JOB_ROLES) {
    const deptId = ids.dept[j.dept] ?? null;
    let m = cur.find(x => x.code === j.code) || cur.find(x => x.nameAr === j.nameAr);
    if (!m) {
      const r = await write(`إنشاء مسمّى «${j.nameAr}» (${j.code})`, () => api('POST', '/directory/job-roles', { token, body: { nameAr: j.nameAr, nameEn: j.nameEn, code: j.code, departmentId: deptId } }));
      if (r.planned) { counters.Created++; continue; }
      if (!r.ok) { fail(`إنشاء مسمّى «${j.nameAr}»`, problem(r)); continue; }
      ids.jobRole[j.key] = r.body.id; bump('Created', `مسمّى «${j.nameAr}» (${j.code})`); continue;
    }
    ids.jobRole[j.key] = m.id;
    const needs = m.nameAr !== j.nameAr || m.nameEn !== j.nameEn || m.code !== j.code || (deptId && m.departmentId !== deptId);
    if (!needs) { bump('AlreadyExists', `مسمّى «${j.nameAr}» (${j.code})`); continue; }
    const r = await write(`تحديث مسمّى «${j.nameAr}»`, () => api('PUT', `/directory/job-roles/${m.id}`, { token, body: { nameAr: j.nameAr, nameEn: j.nameEn, code: j.code, departmentId: deptId ?? m.departmentId } }));
    if (r.planned) { counters.Updated++; continue; }
    if (!r.ok) fail(`تحديث مسمّى «${j.nameAr}»`, problem(r)); else bump('Updated', `مسمّى «${j.nameAr}»`);
  }
}

const sameRoles = (a, b) => {
  const x = [...(a || [])].map(s => s.toLowerCase()).sort();
  const y = [...(b || [])].map(s => s.toLowerCase()).sort();
  return x.length === y.length && x.every((v, i) => v === y[i]);
};

async function stageUsers(token) {
  log('\n--- §5/§6 حسابات الأدوار (11 حسابًا) ---');
  let cur = (await api('GET', '/directory/users?includeInactive=true', { token })).body || [];
  for (const u of USERS) {
    const pw = secrets[`UAT_PW_${u.key}`];
    if (!pw) { fail(`حساب ${u.email}`, `كلمة المرور UAT_PW_${u.key} غير موجودة في ملفّ الأسرار`); passwordStatus[u.key] = 'Missing'; continue; }
    const deptId = u.dept ? (ids.dept[u.dept] ?? null) : null;
    const teamId = u.team ? (ids.team[u.team] ?? null) : null;
    const mgrId  = u.manager ? (ids.user[u.manager] ?? null) : null;
    let m = cur.find(x => (x.email || '').toLowerCase() === u.email);

    if (!m) {
      const r = await write(`إنشاء حساب ${u.email}`, () => api('POST', '/directory/users', { token, body: { email: u.email, fullName: u.fullName, password: pw, roles: u.roles, departmentId: deptId, teamId, managerId: mgrId } }));
      if (r.planned) { counters.Created++; passwordStatus[u.key] = 'Planned'; continue; }
      if (!r.ok) { fail(`إنشاء حساب ${u.email}`, problem(r)); continue; }
      m = r.body; ids.user[u.key] = m.id; passwordStatus[u.key] = 'Created';
      bump('Created', `حساب ${u.email}`);
    } else {
      ids.user[u.key] = m.id;
      // (أ) البيانات الأساسية والانتماء التنظيمي والأدوار
      const needsBasic = m.fullName !== u.fullName || m.isActive !== true
        || (m.departmentId ?? null) !== deptId || (m.teamId ?? null) !== teamId || (m.managerId ?? null) !== mgrId;
      if (needsBasic) {
        const r = await write(`تحديث بيانات ${u.email}`, () => api('PUT', `/directory/users/${m.id}`, { token, body: { fullName: u.fullName, email: u.email, isActive: true, departmentId: deptId, teamId, managerId: mgrId } }));
        if (r.planned) counters.Updated++;
        else if (!r.ok) fail(`تحديث بيانات ${u.email}`, problem(r));
        else bump('Updated', `بيانات ${u.email}`);
      } else bump('AlreadyExists', `بيانات ${u.email}`);

      if (!sameRoles(m.roles, u.roles)) {
        const r = await write(`ضبط أدوار ${u.email} ⟶ [${u.roles.join(', ')}]`, () => api('PUT', `/directory/users/${m.id}/roles`, { token, body: { roles: u.roles } }));
        if (r.planned) counters.Updated++;
        else if (!r.ok) fail(`ضبط أدوار ${u.email}`, problem(r));
        else bump('Updated', `أدوار ${u.email} ⟶ [${u.roles.join(', ')}]`);
      } else bump('AlreadyExists', `أدوار ${u.email}`);

      // (ب) كلمة المرور: تُتحقَّق بمحاولة دخول فعليّة، ولا تُعاد إلا عند الفشل
      const t = await login(u.email, pw);
      if (t) { passwordStatus[u.key] = 'Validated'; bump('AlreadyExists', `كلمة مرور ${u.email} (تحقّق دخول ناجح)`); }
      else {
        const r = await write(`إعادة تعيين كلمة مرور ${u.email}`, () => api('POST', `/directory/users/${m.id}/reset-password`, { token, body: { newPassword: pw } }));
        if (r.planned) { counters.Updated++; passwordStatus[u.key] = 'Planned'; }
        else if (!r.ok) { fail(`إعادة تعيين كلمة مرور ${u.email}`, problem(r)); passwordStatus[u.key] = 'Failed'; }
        else { passwordStatus[u.key] = 'Reset'; bump('Updated', `كلمة مرور ${u.email}`); }
      }
    }

    // (ج) المسمّى الوظيفي
    const jrId = ids.jobRole[u.jobRole] ?? null;
    if (!jrId) { bump('Skipped', `مسمّى ${u.email} (المسمّى غير مُحلّ — وضع PLAN)`); continue; }
    const id = ids.user[u.key];
    if (!id) { bump('Skipped', `مسمّى ${u.email} (الحساب غير مُحلّ — وضع PLAN)`); continue; }
    const fresh = (await api('GET', '/directory/users?includeInactive=true', { token })).body || [];
    const now = fresh.find(x => x.id === id);
    if (now && now.jobRoleId === jrId) { bump('AlreadyExists', `مسمّى ${u.email}`); continue; }
    const r = await write(`ربط مسمّى ${u.email} ⟶ ${u.jobRole}`, () => api('PATCH', `/directory/users/${id}/job-role`, { token, body: { jobRoleId: jrId, notes: 'UAT-ROLE-WORKFLOW-VALIDATION-R1' } }));
    if (r.planned) counters.Updated++;
    else if (!r.ok) fail(`ربط مسمّى ${u.email}`, problem(r));
    else bump('Updated', `مسمّى ${u.email} ⟶ ${u.jobRole}`);
  }

  // قائد فريق «فريق إدارة العملاء UAT» = حساب قائد الفريق (لازم لاختبار §9)
  const tid = ids.team.ACCOUNTS, lid = ids.user.TL;
  if (tid && lid) {
    const teams = (await api('GET', '/directory/teams', { token })).body || [];
    const t = teams.find(x => x.id === tid);
    if (t && t.teamLeaderId === lid) bump('AlreadyExists', 'قائد فريق «فريق إدارة العملاء UAT»');
    else if (t) {
      const r = await write('ضبط قائد «فريق إدارة العملاء UAT»', () => api('PUT', `/directory/teams/${tid}`, { token, body: { nameAr: t.nameAr, nameEn: t.nameEn, departmentId: t.departmentId, teamLeaderId: lid, isActive: true, syncMemberDepartments: false } }));
      if (r.planned) counters.Updated++;
      else if (!r.ok) fail('ضبط قائد فريق إدارة العملاء', problem(r));
      else bump('Updated', 'قائد «فريق إدارة العملاء UAT»');
    }
  } else bump('Skipped', 'قائد «فريق إدارة العملاء UAT» (غير مُحلّ — وضع PLAN)');
}

const norm = (v) => (v === undefined || v === '' ? null : v);

async function stageClients(token) {
  log('\n--- §7 العملاء والمشاريع والأبناء ---');
  const cur = (await api('GET', '/clients?includeClosed=true', { token })).body || [];
  for (const c of CLIENTS) {
    const amId = c.accountManager ? (ids.user[c.accountManager] ?? null) : null;
    let m = cur.find(x => x.name === c.name);
    const desired = {
      name: c.name, status: c.core.status, accountManagerId: amId,
      mainContactName: c.core.mainContactName, mainContactInfo: c.core.mainContactInfo, notes: c.core.notes,
      tradeNameEn: c.core.tradeNameEn, legalName: c.core.legalName, clientTypeCode: c.core.clientTypeCode,
      sectorCode: c.core.sectorCode, country: c.core.country, city: c.core.city, website: c.core.website,
      sourceCode: c.core.sourceCode, relationshipStartDate: c.core.relationshipStartDate,
    };
    if (!m) {
      const r = await write(`إنشاء عميل «${c.name}»`, () => api('POST', '/clients', { token, body: desired }));
      if (r.planned) { counters.Created++; continue; }
      if (!r.ok) { fail(`إنشاء عميل «${c.name}»`, problem(r)); continue; }
      m = r.body; ids.client[c.key] = m.id; bump('Created', `عميل «${c.name}»`);
    } else {
      ids.client[c.key] = m.id;
      const diff = Object.keys(desired).some(k => norm(m[k]) !== norm(desired[k]));
      if (!diff) bump('AlreadyExists', `عميل «${c.name}»`);
      else {
        const r = await write(`تحديث عميل «${c.name}»`, () => api('PUT', `/clients/${m.id}`, { token, body: desired }));
        if (r.planned) counters.Updated++;
        else if (!r.ok) fail(`تحديث عميل «${c.name}»`, problem(r));
        else bump('Updated', `عميل «${c.name}»`);
      }
    }

    const cid = ids.client[c.key];
    if (!cid) { bump('Skipped', `أبناء «${c.name}» (العميل غير مُحلّ — وضع PLAN)`); continue; }

    // جهات الاتصال
    const contacts = (await api('GET', `/clients/${cid}/contacts?includeInactive=true`, { token })).body || [];
    for (const ct of c.contacts) {
      const ex = contacts.find(x => x.name === ct.name);
      if (!ex) {
        const r = await write(`إنشاء جهة اتصال «${ct.name}»`, () => api('POST', `/clients/${cid}/contacts`, { token, body: ct }));
        if (r.planned) counters.Created++;
        else if (!r.ok) fail(`جهة اتصال «${ct.name}»`, problem(r));
        else bump('Created', `جهة اتصال «${ct.name}»`);
      } else {
        const diff = Object.keys(ct).some(k => norm(ex[k]) !== norm(ct[k]));
        if (!diff && ex.isActive) bump('AlreadyExists', `جهة اتصال «${ct.name}»`);
        else {
          const r = await write(`تحديث جهة اتصال «${ct.name}»`, () => api('PUT', `/clients/${cid}/contacts/${ex.id}`, { token, body: ct }));
          if (r.planned) counters.Updated++;
          else if (!r.ok) fail(`تحديث جهة اتصال «${ct.name}»`, problem(r));
          else bump('Updated', `جهة اتصال «${ct.name}»`);
        }
      }
    }

    // القنوات الرقمية
    const channels = (await api('GET', `/clients/${cid}/digital-channels?includeInactive=true`, { token })).body || [];
    for (const ch of c.channels) {
      const ex = channels.find(x => x.platformCode === ch.platformCode);
      if (!ex) {
        const r = await write(`إنشاء قناة «${ch.platformCode}»`, () => api('POST', `/clients/${cid}/digital-channels`, { token, body: ch }));
        if (r.planned) counters.Created++;
        else if (!r.ok) fail(`قناة «${ch.platformCode}»`, problem(r));
        else bump('Created', `قناة «${ch.platformCode}» لـ«${c.name}»`);
      } else {
        const diff = Object.keys(ch).some(k => norm(ex[k]) !== norm(ch[k]));
        if (!diff && ex.isActive) bump('AlreadyExists', `قناة «${ch.platformCode}» لـ«${c.name}»`);
        else {
          const r = await write(`تحديث قناة «${ch.platformCode}»`, () => api('PUT', `/clients/${cid}/digital-channels/${ex.id}`, { token, body: ch }));
          if (r.planned) counters.Updated++;
          else if (!r.ok) fail(`تحديث قناة «${ch.platformCode}»`, problem(r));
          else bump('Updated', `قناة «${ch.platformCode}» لـ«${c.name}»`);
        }
      }
    }

    // ملفّ البراند
    const brandRes = await api('GET', `/clients/${cid}/brand`, { token });
    const brand = brandRes.ok ? brandRes.body : null;
    const brandDiff = !brand || Object.keys(c.brand).some(k => norm(brand[k]) !== norm(c.brand[k]));
    if (!brandDiff) bump('AlreadyExists', `ملفّ براند «${c.name}»`);
    else {
      const r = await write(`حفظ ملفّ براند «${c.name}»`, () => api('PUT', `/clients/${cid}/brand`, { token, body: c.brand }));
      if (r.planned) { brand ? counters.Updated++ : counters.Created++; }
      else if (!r.ok) fail(`ملفّ براند «${c.name}»`, problem(r));
      else bump(brand ? 'Updated' : 'Created', `ملفّ براند «${c.name}»`);
    }

    // المشاريع
    const projects = (await api('GET', `/projects?clientId=${cid}&includeClosed=true`, { token })).body || [];
    for (const p of c.projects) {
      const pAm = p.accountManager ? (ids.user[p.accountManager] ?? null) : null;
      const pTeam = p.ownerTeam ? (ids.team[p.ownerTeam] ?? null) : null;
      const ex = projects.find(x => x.name === p.name);
      const desiredP = { name: p.name, serviceType: p.serviceType, status: p.status, startDate: p.startDate, endDate: null, ownerTeamId: pTeam, accountManagerId: pAm, notes: p.notes };
      if (!ex) {
        const r = await write(`إنشاء مشروع «${p.name}»`, () => api('POST', '/projects', { token, body: { clientId: cid, ...desiredP } }));
        if (r.planned) { counters.Created++; continue; }
        if (!r.ok) { fail(`مشروع «${p.name}»`, problem(r)); continue; }
        ids.project[p.key] = r.body.id; bump('Created', `مشروع «${p.name}»`);
      } else {
        ids.project[p.key] = ex.id;
        const diff = Object.keys(desiredP).some(k => norm(ex[k]) !== norm(desiredP[k]));
        if (!diff) bump('AlreadyExists', `مشروع «${p.name}»`);
        else {
          const r = await write(`تحديث مشروع «${p.name}»`, () => api('PUT', `/projects/${ex.id}`, { token, body: desiredP }));
          if (r.planned) counters.Updated++;
          else if (!r.ok) fail(`تحديث مشروع «${p.name}»`, problem(r));
          else bump('Updated', `مشروع «${p.name}»`);
        }
      }
    }
  }
}

// ===================== التشغيل =====================
(async () => {
  log(`===== UAT-ROLE-WORKFLOW-VALIDATION-R1 FIXTURE — MODE=${MODE} =====`);
  log(`API_BASE=${API_BASE}`);

  const health = await fetch(API_BASE.replace(/\/api$/, '') + '/health').then(r => r.status).catch(() => 0);
  log(`health=${health}`);
  if (health !== 200) { console.error('NO-GO: health != 200'); process.exit(2); }

  const token = await login(ADMIN_EMAIL, ADMIN_PASSWORD);
  if (!token) { console.error('NO-GO: فشل دخول حساب الإدارة على TEST'); process.exit(2); }
  log('admin login: OK (التوكن غير مطبوع)');

  await stageDepartments(token);
  await stageTeams(token);
  await stageJobRoles(token);
  await stageUsers(token);
  await stageClients(token);

  log('\n===== §12 العدّادات =====');
  for (const [k, v] of Object.entries(counters)) log(`${k}=${v}`);
  log('\n===== حالة كلمات المرور (القيم مُقنَّعة دائمًا) =====');
  for (const u of USERS) log(`${u.email} :: ${passwordStatus[u.key] || 'Unknown'} :: ********`);
  if (failures.length) { log('\n===== الإخفاقات ====='); failures.forEach(f => log(`- ${f}`)); }
  log(`\nRESULT=${counters.Failed === 0 ? 'PASS' : 'FAIL'} MODE=${MODE}`);
  process.exit(counters.Failed === 0 ? 0 : 1);
})();

// سجلّ الملاحة المركزيّ (P3-NAV-001/002) — **مصدر الحقيقة الوحيد** لقائمة التنقّل.
//
// مبادئ حاكمة لا تُخالَف:
//  1) لا شرط ظهور واحد خارج هذا الملفّ: المكوّنات تستهلك `accessibleModules`/`visibleItems` ولا تبني
//     شروطًا خاصّة بها. أيّ شرط متفرّق يعني مصدرَي حقيقة يتباعدان مع الوقت.
//  2) **الظهور ليس تخويلًا.** الخادم هو المُنفِّذ الوحيد؛ هذا السجلّ يمنع عرض سطح لا يملكه المستخدم
//     (كذب في اتّجاه) ويمنع إخفاء سطح يملكه (كذب في الاتّجاه المقابل). حذف عنصر من هنا لا يحمي شيئًا،
//     وإظهاره لا يمنح شيئًا.
//  3) **المصدر المفضَّل للقرار = قدرات الخادم** (`permissions` من `/auth/me`) ثمّ النطاق (`scopeType`)،
//     والأدوار آخِرًا وفقط حين لا تُعبَّر القدرة بمفتاح صريح. مصفوفات الأدوار أدناه منقولة **حرفيًّا**
//     من بوّابات المسارات القائمة في `App.tsx` — لا توسيع ولا تضييق.
//  4) **الاحتياطيّ الآمن عند غياب القدرة = الإخفاء**، لا الإظهار.
//  5) `resolveTarget` حتميّ وخالٍ من الآثار الجانبيّة.
//  6) وحدة بلا عنصر ظاهر لا تظهر إطلاقًا؛ و«الموظفون» تظهر دائمًا لأنّ «ملفي» متاح لكلّ مستخدم نشط.
import type { Role } from '../types/api';
import type { IconName } from '../components/icons';

// ===== مصفوفات الأدوار (منقولة حرفيًّا من بوّابات App.tsx — بلا تغيير في النطاق) =====
const EXEC_VIEW: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
// أضيق عمدًا من حارس المسار (EXEC_ROLES): القائمة لا تعرض تجميع المبيعات لقادة الفرق.
// تضييق **العرض** لا يمنع أحدًا — المسار يبقى عاملًا بالوصول المباشر بحارسه الأصليّ.
const SALES_AGGREGATION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager'];
const EXECUTION_REPORTS: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'AccountPortfolioReader'];
const TEAM_EXECUTION_DASHBOARD: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader'];
const USERS_PAGE: Role[] = ['Admin', 'CEO', 'CeoSupport'];
const GOVERNANCE: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
const TEMPLATE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];
const JOB_ROLE_MANAGEMENT: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
const BALANCE_MANAGEMENT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'HR'];
const COMPLETION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer', 'HR'];
const HR_EMPLOYEE: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
const PAYROLL: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
const KPI_FINANCE_EXPORT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
const GOVERNANCE_WORKSPACE: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR'];
const GOVERNANCE_ESCALATION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
const GOVERNANCE_ACTION_ITEMS: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
const AUDIT: Role[] = ['Admin', 'CEO', 'GeneralManager'];
const ARCHIVE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];
const CLIENT_360: Role[] = [...EXEC_VIEW, 'AccountPortfolioReader'];

// ===== مفاتيح القدرات الخادميّة (تطابق Reporting.Application.Security.AppPermissions حرفيًّا) =====
export const PERMISSIONS = {
  hrOperationsView: 'HrOperations.View',
  hrOperationsExport: 'HrOperations.Export',
  attendanceReview: 'Attendance.Review',
  attendanceExport: 'Attendance.Export',
  employeeChecklistManage: 'EmployeeChecklist.Manage',
} as const;

// ===== الأنواع =====

/// نوع نطاق الرؤية كما يحسبه الخادم (IScopeResolver) — `own` هو «وضع الذات».
export type ScopeType = 'own' | 'team' | 'department' | 'company' | 'governance';

/// الوحدات السبع الثابتة — الترتيب والأيقونات لا يتغيّران بتغيّر الدور إطلاقًا.
export type ModuleId =
  | 'home'
  | 'people'
  | 'reports'
  | 'performance'
  | 'governance'
  | 'portfolio'
  | 'settings';

export type GroupId =
  | 'employee-services'
  | 'aggregations'
  | 'report-management'
  | 'org-structure'
  | 'catalogs'
  | 'notifications';

/// شرط ظهور خاصّ لا يُعبَّر عنه بدور ولا بمفتاح (مسمّى وظيفيّ بعينه).
export type JobRoleGate = 'salesRep' | 'salesTeamLeader' | 'accountManager';

export interface NavItem {
  /// معرّف فريد عبر السجلّ كلّه (يُختبَر آليًّا ضدّ التكرار).
  id: string;
  label: string;
  /// المسار المرجعيّ (canonical) — الوحيد الذي يظهر في القائمة والـbreadcrumbs.
  target: string;
  /// وجهة سياقيّة حتميّة بلا آثار جانبيّة (وضع الذات مثلًا). تُستعمل بدل `target` عند وجودها.
  resolveTarget?: (ctx: NavCtx) => string;
  order: number;
  group?: GroupId;
  /// أدوار مسموح بها — تُستعمل فقط حين لا يوجد مفتاح صلاحيّة صريح يعبّر عن القدرة.
  roles?: Role[];
  /// يكفي مفتاح واحد من هذه المفاتيح.
  permissionsAny?: string[];
  /// تلزم كلّ هذه المفاتيح.
  permissionsAll?: string[];
  /// يظهر فقط لهذه النطاقات (غياب النطاق ⇒ إخفاء، احتياطيّ آمن).
  scope?: ScopeType[];
  /// شرط مسمّى وظيفيّ حصريّ.
  jobRoleGate?: JobRoleGate;
  /// مسارات قديمة/مدمَجة تُحَلّ إلى هذا العنصر (تُسجَّل كتحويلات في App.tsx).
  aliases?: string[];
  /// مسارات ديناميّة تُبرِز هذا العنصر عند الوصول المباشر (تفاصيل فريق/عميل/مشروع…).
  matchPaths?: string[];
  /// مفتاح عدّاد اختياريّ — لا يُعرَض إلّا إذا كان العنصر نفسه ظاهرًا للمستخدم.
  badgeKey?: string;
  /// مطابقة تامّة للمسار بدل مطابقة البادئة (للرئيسية فقط).
  exact?: boolean;
  keywords?: string;
}

export interface NavGroup {
  id: GroupId;
  label: string;
}

export interface NavModule {
  id: ModuleId;
  label: string;
  icon: IconName;
  order: number;
  /// وحدة تظهر دائمًا لكلّ مستخدم نشط بصرف النظر عن عناصرها (الرئيسية والموظفون).
  alwaysVisible?: boolean;
  groups?: NavGroup[];
  items: NavItem[];
}

export interface NavCtx {
  /// هل الجلسة مصادَق عليها فعلًا (لا عنصر يظهر بدونها).
  authenticated: boolean;
  hasAnyRole: (...roles: Role[]) => boolean;
  /// مفاتيح `perm` القادمة من الخادم لهذا المستخدم — المصدر المفضَّل للقرار.
  permissions: ReadonlySet<string>;
  /// نوع النطاق القادم من الخادم (null إن لم يُعرَف ⇒ عناصر النطاق تُخفى).
  scopeType: ScopeType | null;
  isSalesRep: boolean;
  isSalesB2cTeamLeader: boolean;
  jobRoleCode: string | null;
}

// ===== الوحدات السبع =====
// الترتيب ثابت (order 1..7) والأيقونات ثابتة — لا يتغيّر منهما شيء بتغيّر الدور.
export const MODULES: NavModule[] = [
  {
    id: 'home',
    label: 'الرئيسية',
    icon: 'home',
    order: 1,
    alwaysVisible: true,
    items: [{ id: 'home.overview', label: 'الرئيسية', target: '/app', order: 1, exact: true }],
  },

  {
    // «الموظفون» تظهر دائمًا: «ملفي» متاح لكلّ مستخدم نشط، فلا حالة تجعلها فارغة.
    id: 'people',
    label: 'الموظفون',
    icon: 'teams',
    order: 2,
    alwaysVisible: true,
    groups: [{ id: 'employee-services', label: 'خدمات الموظفين' }],
    items: [
      { id: 'people.me', label: 'ملفي', target: '/app/employee/me', order: 1, matchPaths: ['/app/employee'], keywords: 'ملفّي الشخصي موظف 360' },
      // «دليل الموظفين» = سطح بيانات الموظفين القائم (`/app/hr-employees`) بأدواره كما هي.
      // لا يوجد دليل مستقلّ محكوم بالنطاق في النظام حتّى الآن — مسجَّل كقصور معروف، وممنوع
      // اختراع سطح جديد يوهم بقدرة لا يفرضها الخادم.
      { id: 'people.directory', label: 'دليل الموظفين', target: '/app/hr-employees', order: 2, roles: HR_EMPLOYEE, aliases: ['/app/directory'], keywords: 'دليل بيانات الموظفين hr' },
      { id: 'people.teams', label: 'فرق العمل', target: '/app/teams', order: 3, roles: EXEC_VIEW, matchPaths: ['/app/teams'] },
      { id: 'people.attendance', label: 'الحضور والالتزام', target: '/app/attendance', order: 4, keywords: 'غياب تأخير انصراف واقعة بلاغ' },

      { id: 'people.leaves', label: 'الإجازات والاستئذانات', target: '/app/leave-requests', order: 10, group: 'employee-services', aliases: ['/app/leaves', '/app/permissions'], keywords: 'إجازة استئذان' },
      { id: 'people.requests', label: 'الطلبات', target: '/app/hr-requests', order: 11, group: 'employee-services', aliases: ['/app/employee-requests'], keywords: 'hr خطاب طلب' },
      { id: 'people.balances', label: 'الأرصدة', target: '/app/balances', order: 12, group: 'employee-services', keywords: 'رصيد أرصدتي' },
      { id: 'people.balance-management', label: 'إدارة الأرصدة', target: '/app/balance-management', order: 13, group: 'employee-services', roles: BALANCE_MANAGEMENT },
      // سطح قائم بالفعل؛ لا تُنشَأ وحدة ماليّة جديدة (§5.2).
      { id: 'people.payroll-impacts', label: 'الطلبات المؤثّرة على الراتب', target: '/app/payroll/leave-impacts', order: 14, group: 'employee-services', roles: PAYROLL, keywords: 'رواتب مالية' },

      { id: 'people.development', label: 'التطوير والمتابعة', target: '/app/development', order: 20, keywords: 'تطوير خطة متابعة' },
      // HR Operations: **بالمفتاح لا بالدور**. الخادم يشترط `HrOperations.View` صراحةً ولا يمنحه أيّ
      // دور ضمنًا (ولا Admin). بوّابة أدوار هنا كانت ستكذب في الاتّجاهين معًا.
      { id: 'people.hr-operations', label: 'عمليّات الموارد البشريّة', target: '/app/hr-operations', order: 21, permissionsAny: [PERMISSIONS.hrOperationsView], badgeKey: 'hrOpsQueue', keywords: 'طوابير إجراءات متأخّر مهلة sla' },
    ],
  },

  {
    id: 'reports',
    label: 'التقارير',
    icon: 'reports',
    order: 3,
    groups: [
      { id: 'aggregations', label: 'التجميعات والمقارنات' },
      { id: 'report-management', label: 'إدارة التقارير' },
    ],
    items: [
      { id: 'reports.mine', label: 'تقاريري', target: '/app/my-reports', order: 1, keywords: 'تقريري إنشاء مسودة إرسال تسليم' },
      { id: 'reports.scope', label: 'تقارير النطاق', target: '/app/submissions', order: 2, roles: EXEC_VIEW, keywords: 'تقارير الفريق كل التقارير' },
      { id: 'reports.calendar', label: 'التقويم والاستحقاقات', target: '/app/report-calendar', order: 3, aliases: ['/app/calendar'], keywords: 'مواعيد جدول تقويم' },
      { id: 'reports.compliance', label: 'متابعة الالتزام', target: '/app/compliance', order: 4, roles: COMPLETION, aliases: ['/app/kpi-compliance'] },
      { id: 'reports.executive', label: 'التقارير التنفيذية', target: '/app/reports', order: 5, roles: EXEC_VIEW, aliases: ['/app/executive'], keywords: 'الإدارة executive' },

      { id: 'reports.analytics', label: 'التحليلات', target: '/app/analytics', order: 10, group: 'aggregations', roles: EXEC_VIEW, keywords: 'مقارنات تحليل' },
      { id: 'reports.sales-aggregation', label: 'تجميع المبيعات', target: '/app/sales-aggregation', order: 11, group: 'aggregations', roles: SALES_AGGREGATION, aliases: ['/app/sales-aggregate'] },
      { id: 'reports.sales-team', label: 'لوحة مبيعات الفريق', target: '/app/sales/team-dashboard', order: 12, group: 'aggregations', jobRoleGate: 'salesTeamLeader' },
      { id: 'reports.sales-mine', label: 'لوحة مبيعاتي', target: '/app/sales/my-dashboard', order: 13, group: 'aggregations', jobRoleGate: 'salesRep' },
      { id: 'reports.execution', label: 'تقارير التنفيذ', target: '/app/execution-reports', order: 14, group: 'aggregations', roles: EXECUTION_REPORTS },
      { id: 'reports.execution-team', label: 'لوحة تنفيذ الفريق', target: '/app/execution/team-dashboard', order: 15, group: 'aggregations', roles: TEAM_EXECUTION_DASHBOARD },

      // إدارة التقارير تنتقل كاملة من «الإعدادات» إلى «التقارير» ولا تبقى مكرّرة هناك (§5.3).
      { id: 'reports.templates', label: 'القوالب والإصدارات', target: '/app/report-templates', order: 20, group: 'report-management', roles: TEMPLATE_GOVERNANCE, keywords: 'قوالب إصدارات نسخ' },
      { id: 'reports.approval-flows', label: 'مسارات الاعتماد', target: '/app/workflows', order: 21, group: 'report-management', roles: EXEC_VIEW, aliases: ['/app/approval-flows'], keywords: 'اعتماد موافقة' },
      { id: 'reports.view-grants', label: 'منح الرؤية', target: '/app/report-view-grants', order: 22, group: 'report-management', roles: ADMIN },
    ],
  },

  {
    id: 'performance',
    label: 'الأداء وKPI',
    icon: 'kpi',
    order: 4,
    items: [
      // رابط واحد للوحة الأداء بدل رابطَين متنافسَين («مؤشراتي» و«مؤشرات الأداء»).
      // الهدف سياقيّ وحتميّ: وضع الذات ⇒ `/app/my-kpi` (السطح الشخصيّ القائم بحقوقه كما هي)،
      // وغيره ⇒ `/app/performance` (اللوحة الموحّدة). كلا المسارين يظلّان عاملَين بالوصول المباشر.
      {
        id: 'performance.dashboard',
        label: 'لوحة الأداء',
        target: '/app/performance',
        resolveTarget: (ctx) => (ctx.scopeType === 'own' ? '/app/my-kpi' : '/app/performance'),
        order: 1,
        matchPaths: ['/app/performance', '/app/my-kpi', '/app/kpi', '/app/employees'],
        aliases: ['/app/kpi-evaluations', '/app/kpi-aggregate', '/app/kpi-quarterly'],
        keywords: 'kpi أداء مؤشرات تقييم تجميع ربع سنوي',
      },
      { id: 'performance.templates', label: 'قوالب KPI', target: '/app/kpi-templates', order: 2, roles: TEMPLATE_GOVERNANCE },
      { id: 'performance.finance-export', label: 'التصدير المالي', target: '/app/kpi-finance-export', order: 3, roles: KPI_FINANCE_EXPORT, keywords: 'رواتب csv تصدير' },
    ],
  },

  {
    id: 'governance',
    label: 'الحوكمة',
    icon: 'governance',
    order: 5,
    items: [
      { id: 'governance.overview', label: 'نظرة عامة', target: '/app/governance', order: 1, roles: GOVERNANCE, aliases: ['/app/risks', '/app/decisions'], keywords: 'مخاطر قرارات' },
      { id: 'governance.workspace', label: 'ورشة الحوكمة', target: '/app/governance-workspace', order: 2, roles: GOVERNANCE_WORKSPACE },
      { id: 'governance.escalations', label: 'التصعيدات', target: '/app/governance/escalations', order: 3, roles: GOVERNANCE_ESCALATION, aliases: ['/app/escalations'] },
      { id: 'governance.actions', label: 'الإجراءات', target: '/app/governance/action-items', order: 4, roles: GOVERNANCE_ACTION_ITEMS, aliases: ['/app/actions'], keywords: 'إجراءات مرتبطة بي' },
      { id: 'governance.audit', label: 'سجل التدقيق', target: '/app/audit', order: 5, roles: AUDIT },
      { id: 'governance.archive', label: 'الأرشيف الإداري', target: '/app/admin/archive', order: 6, roles: ARCHIVE_GOVERNANCE, keywords: 'استرجاع محذوف أرشفة' },
    ],
  },

  {
    // يخرج هذا الدومين من «الموظفون» ويصبح وحدة مستقلّة (§5.6).
    id: 'portfolio',
    label: 'العملاء والمشروعات',
    icon: 'clients',
    order: 6,
    items: [
      { id: 'portfolio.clients', label: 'العملاء', target: '/app/clients', order: 1, roles: CLIENT_360, matchPaths: ['/app/clients'] },
      { id: 'portfolio.projects', label: 'المشروعات', target: '/app/projects', order: 2, roles: EXEC_VIEW, matchPaths: ['/app/projects'] },
      { id: 'portfolio.account', label: 'مشاريع عملائي', target: '/app/account-portfolio', order: 3, jobRoleGate: 'accountManager', matchPaths: ['/app/account-portfolio'], keywords: 'محفظة عملائي مدير العميل' },
    ],
  },

  {
    id: 'settings',
    label: 'الإعدادات',
    icon: 'settings',
    order: 7,
    groups: [
      { id: 'org-structure', label: 'الهيكل التنظيمي' },
      { id: 'catalogs', label: 'الخدمات وتصنيفات التنفيذ' },
      { id: 'notifications', label: 'الإشعارات والبريد' },
    ],
    items: [
      { id: 'settings.users', label: 'المستخدمون والأدوار والصلاحيات', target: '/app/users', order: 1, roles: USERS_PAGE, keywords: 'مستخدمين أدوار صلاحيات' },

      { id: 'settings.departments', label: 'الإدارات', target: '/app/departments', order: 10, group: 'org-structure', roles: ADMIN },
      { id: 'settings.job-roles', label: 'المسميات', target: '/app/job-roles', order: 11, group: 'org-structure', roles: JOB_ROLE_MANAGEMENT, matchPaths: ['/app/job-roles'], keywords: 'مسمّى وظيفي إدارة المسمّيات' },
      { id: 'settings.positions', label: 'المناصب', target: '/app/positions', order: 12, group: 'org-structure', roles: ADMIN, keywords: 'مناصب مرنة' },

      { id: 'settings.services', label: 'الخدمات', target: '/app/services', order: 20, group: 'catalogs', roles: TEMPLATE_GOVERNANCE },
      { id: 'settings.courses', label: 'الدورات', target: '/app/courses', order: 21, group: 'catalogs', roles: TEMPLATE_GOVERNANCE },
      { id: 'settings.execution-taxonomy', label: 'تصنيفات التنفيذ', target: '/app/execution-taxonomy', order: 22, group: 'catalogs', roles: TEMPLATE_GOVERNANCE },

      { id: 'settings.email-control', label: 'الإشعارات والبريد', target: '/app/email-control', order: 30, group: 'notifications', roles: ADMIN, aliases: ['/app/notifications'] },
      { id: 'settings.email-log', label: 'سجل البريد', target: '/app/email-notifications', order: 31, group: 'notifications', roles: GOVERNANCE, aliases: ['/app/email-log'] },

      { id: 'settings.general', label: 'الإعدادات العامة', target: '/app/settings', order: 40, roles: ADMIN },
    ],
  },
];

// ===== محلّل الرؤية المركزيّ (نقيّ وقابل للاختبار) =====

/// هل يظهر العنصر للمستخدم الحالي؟
/// الترتيب: مصادقة ⇒ مسمّى وظيفيّ ⇒ مفاتيح الصلاحيّة ⇒ النطاق ⇒ الأدوار.
/// أيّ شرط مُعلَن ولا تتوفّر معلوماته ⇒ **إخفاء** (احتياطيّ آمن).
export function isItemVisible(item: NavItem, ctx: NavCtx): boolean {
  if (!ctx.authenticated) return false;

  if (item.jobRoleGate) {
    if (item.jobRoleGate === 'salesRep') return ctx.isSalesRep;
    if (item.jobRoleGate === 'salesTeamLeader') return ctx.isSalesB2cTeamLeader;
    return ctx.jobRoleCode === 'ACCOUNT_MGR';
  }

  if (item.permissionsAll && !item.permissionsAll.every((p) => ctx.permissions.has(p))) return false;
  if (item.permissionsAny && !item.permissionsAny.some((p) => ctx.permissions.has(p))) return false;
  if (item.scope && (ctx.scopeType === null || !item.scope.includes(ctx.scopeType))) return false;
  if (item.roles && !ctx.hasAnyRole(...item.roles)) return false;

  return true;
}

/// عناصر الوحدة الظاهرة، مرتّبة بـ`order` ثمّ بالمجموعة.
export function visibleItems(m: NavModule, ctx: NavCtx): NavItem[] {
  return m.items.filter((i) => isItemVisible(i, ctx)).sort((a, b) => a.order - b.order);
}

/// الوحدات الظاهرة بترتيبها الثابت. وحدة بلا عنصر ظاهر لا تظهر إلّا إن كانت `alwaysVisible`.
export function accessibleModules(ctx: NavCtx): NavModule[] {
  if (!ctx.authenticated) return [];
  return MODULES.filter((m) => m.alwaysVisible || m.items.some((i) => isItemVisible(i, ctx)))
    .sort((a, b) => a.order - b.order);
}

/// وجهة العنصر للمستخدم الحالي (حتميّة، بلا آثار جانبيّة).
export function itemTarget(item: NavItem, ctx: NavCtx): string {
  return item.resolveTarget ? item.resolveTarget(ctx) : item.target;
}

/// وجهة الوحدة = أوّل عنصر ظاهر فيها (وفي «الموظفون» هو «ملفي» دائمًا ⇒ وضع الذات يحلّ مباشرةً).
export function moduleTarget(m: NavModule, ctx: NavCtx): string {
  const first = visibleItems(m, ctx)[0];
  return first ? itemTarget(first, ctx) : '/app';
}

// ===== سجلّ الـAliases =====

export interface AliasEntry {
  /// المسار البديل (القديم/المدمَج).
  from: string;
  /// المسار المرجعيّ الذي يُحَلّ إليه — يحمل الحارس الفعليّ.
  to: string;
  /// معرّف عنصر القائمة المرجعيّ (للـbreadcrumbs وحالة النشاط).
  itemId: string;
}

/// كلّ الـaliases مشتقّة من السجلّ نفسه ⇒ لا قائمة موازية تتباعد عنه.
export const ROUTE_ALIASES: AliasEntry[] = MODULES.flatMap((m) =>
  m.items.flatMap((i) => (i.aliases ?? []).map((from) => ({ from, to: i.target, itemId: i.id }))),
);

const ALIAS_MAP: ReadonlyMap<string, AliasEntry> = new Map(ROUTE_ALIASES.map((a) => [a.from, a]));

/// يحوّل مسار alias إلى مساره المرجعيّ (ويعيده كما هو إن لم يكن alias).
export function canonicalPath(pathname: string): string {
  return ALIAS_MAP.get(pathname)?.to ?? pathname;
}

// ===== المطابقة وحالة النشاط =====

function pathMatches(base: string, pathname: string, exact?: boolean): boolean {
  if (exact) return pathname === base;
  return pathname === base || pathname.startsWith(base + '/');
}

/// هل يمثّل العنصر المسار المعطى (بما فيه مساراته الديناميّة وaliasاته)؟
export function itemMatches(item: NavItem, pathname: string): boolean {
  if (pathMatches(item.target, pathname, item.exact)) return true;
  if (item.matchPaths?.some((p) => pathMatches(p, pathname))) return true;
  return (item.aliases ?? []).some((a) => pathMatches(a, pathname));
}

export interface ActiveNav {
  module: NavModule;
  item: NavItem;
  group: NavGroup | null;
}

/// الوحدة/العنصر الحاوي للمسار الحالي — أطول مطابقة، بلا فلترة أدوار كي يُبرَز التنقّل
/// حتّى عند الوصول المباشر بالرابط. الـalias يُبرِز عنصره المرجعيّ لا نفسه.
export function resolveActive(pathname: string): ActiveNav | null {
  const path = canonicalPath(pathname);
  let best: ActiveNav | null = null;
  let bestLen = -1;
  for (const m of MODULES) {
    for (const item of m.items) {
      if (!itemMatches(item, path)) continue;
      const len = Math.max(
        item.target.length,
        ...(item.matchPaths ?? []).filter((p) => pathMatches(p, path)).map((p) => p.length),
      );
      if (len > bestLen) {
        bestLen = len;
        best = { module: m, item, group: m.groups?.find((g) => g.id === item.group) ?? null };
      }
    }
  }
  return best;
}

/// هل يقع المسار داخل عنصر **ظاهر** لهذا المستخدم؟ يُطابق الوجهة وaliasاتها ومساراتها
/// الديناميّة، لا الوجهة المحسوبة وحدها — كي لا يختفي اختصار لمسار يملكه المستخدم فعلًا
/// لمجرّد أنّ عنصر القائمة يحلّ إلى وجهة أخرى في نطاقه (مثل `/app/my-kpi` مقابل `/app/performance`).
export function isPathVisible(pathname: string, ctx: NavCtx): boolean {
  const path = canonicalPath(pathname);
  return MODULES.some((m) => m.items.some((i) => itemMatches(i, path) && isItemVisible(i, ctx)));
}

// ===== العدّادات (Badges) =====

/// عدّاد العنصر — `null` ما لم يكن العنصر **ظاهرًا للمستخدم نفسه** وله قيمة صريحة.
/// هذا يمنع بنيويًّا عرض رقم قادم من سطح لا يملكه المستخدم.
export function resolveBadge(
  item: NavItem,
  ctx: NavCtx,
  badges: Readonly<Record<string, number | undefined>>,
): number | null {
  if (!item.badgeKey) return null;
  if (!isItemVisible(item, ctx)) return null;
  const value = badges[item.badgeKey];
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

// ===== البحث في الترويسة =====

export interface SearchableItem {
  to: string;
  label: string;
  moduleLabel: string;
  keywords: string;
}

export function searchableItems(ctx: NavCtx): SearchableItem[] {
  const out: SearchableItem[] = [];
  for (const m of accessibleModules(ctx)) {
    for (const item of visibleItems(m, ctx)) {
      out.push({ to: itemTarget(item, ctx), label: item.label, moduleLabel: m.label, keywords: item.keywords ?? '' });
    }
  }
  return out;
}

// ===== فتات الخبز (Breadcrumbs) =====

export interface Crumb {
  label: string;
  /// `null` للعنصر الحاليّ (غير قابل للنقر).
  to: string | null;
}

/// فتات خبز سياقيّة مشتقّة من السجلّ وحده: الوحدة › المجموعة › العنصر › [مقطع ديناميّ].
/// **لا تكشف اسم مورد**: المقطع الديناميّ يُعرَض بتسمية عامّة (`dynamicLabel` من صفحة مصرَّح لها فقط).
export function buildBreadcrumbs(pathname: string, ctx: NavCtx, dynamicLabel?: string): Crumb[] {
  const active = resolveActive(pathname);
  if (!active) return [];
  const crumbs: Crumb[] = [{ label: active.module.label, to: moduleTarget(active.module, ctx) }];
  if (active.group) crumbs.push({ label: active.group.label, to: null });

  const canonical = canonicalPath(pathname);
  // «ورقة» = المسار هو الوجهة نفسها أو أحد مساراتها المعلَنة؛ وما زاد عن ذلك مقطع ديناميّ
  // (معرّف مورد) يستحقّ فتاتة خاصّة. مطابقة البادئة هنا تبتلع المقطع الديناميّ وتُخفيه.
  const isLeaf = canonical === active.item.target || (active.item.matchPaths ?? []).includes(canonical);
  crumbs.push({ label: active.item.label, to: isLeaf ? null : itemTarget(active.item, ctx) });

  if (!isLeaf) crumbs.push({ label: dynamicLabel && dynamicLabel.trim() ? dynamicLabel : 'التفاصيل', to: null });
  return crumbs;
}

// نموذج بيانات التنقّل (UI-NAV-RESTRUCTURE-R2) — 7 وحدات رئيسية في الشريط الجانبي + تبويبات داخل كل وحدة.
// تنظيم بصريّ فقط: لا يُنشئ/يحذف مسارًا، ولا يوسّع/يضيّق أيّ صلاحية. أدوار كل تبويب مطابقة تمامًا
// لأدوار العنصر القديم في القائمة الجانبية (نفس مصفوفات DashboardShell)، والحماية الفعلية مفروضة
// خادميًّا عبر ProtectedRoute + سياسات الخادم. الغرض: تقليل ازدحام القائمة بلا فقد أيّ ميزة.
import type { Role } from '../types/api';
import type { IconName } from '../components/icons';

// ===== مصفوفات الأدوار (منقولة حرفيًّا من DashboardShell القديم — بلا تغيير في النطاق) =====
const EXEC_VIEW: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
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
// RESTORE-ARCHIVE-GOVERNANCE-R1 (مستعادة من نَسَب الإنتاج) — الأرشيف الإداريّ: Admin/CEO/GM فقط.
const ARCHIVE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];

export type ModuleId =
  | 'dashboard'
  | 'reports'
  | 'performance'
  | 'portfolio'
  | 'people'
  | 'governance'
  | 'leave'
  | 'settings';

export interface NavTab {
  to: string;
  label: string;
  roles?: Role[]; // إن غابت ظهر التبويب لكل مصادَق عليه
  // ظهور خاصّ بمندوب المبيعات (isSalesRep) — بصرف النظر عن roles؛ لا يظهر للأدمن.
  salesRepOnly?: boolean;
  // ظهور خاصّ بقائد فريق مبيعات B2C (isSalesB2cTeamLeader) — بصرف النظر عن roles؛ لا يظهر لبقية قادة الفرق.
  salesTeamLeaderOnly?: boolean;
  // ظهور خاصّ بمدير الحساب حصرًا (jobRoleCode === 'ACCOUNT_MGR') — بصرف النظر عن roles.
  accountManagerOnly?: boolean;
  // مطابقة تامّة للمسار (للرئيسية /app فقط) بدل مطابقة البادئة.
  exact?: boolean;
  // كلمات مفتاحية إضافية للبحث في الترويسة (بجانب label).
  keywords?: string;
}

export interface NavModule {
  id: ModuleId;
  label: string;
  icon: IconName;
  tabs: NavTab[];
}

export interface NavCtx {
  hasAnyRole: (...roles: Role[]) => boolean;
  isSalesRep: boolean;
  isSalesB2cTeamLeader: boolean;
  // رمز المسمّى الوظيفي للمستخدم الحالي (لتحديد ظهور عناصر خاصّة بمسمّى بعينه مثل مدير الحساب).
  jobRoleCode: string | null;
}

// ===== الوحدات السبع =====
export const MODULES: NavModule[] = [
  {
    id: 'dashboard',
    label: 'الرئيسية',
    icon: 'home',
    tabs: [{ to: '/app', label: 'الرئيسية', exact: true }],
  },
  {
    id: 'reports',
    label: 'التقارير',
    icon: 'reports',
    tabs: [
      // ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1: مساران متوازيان — «تقاريري» الشخصيّ (لكل
      // مصادَق عليه: إنشاء/مسودة/إرسال/متابعة) و«تقارير الفريق» الإداريّ (لمن له عرض الفريق). لا يخفي
      // أحدهما الآخر ولا يستبدله. الحماية الفعلية مفروضة خادميًّا.
      { to: '/app/my-reports', label: 'تقاريري', keywords: 'تقريري إنشاء مسودة إرسال تسليم' },
      { to: '/app/submissions', label: 'تقارير الفريق', roles: EXEC_VIEW, keywords: 'تقرير تسليم كل التقارير الفريق' },
      { to: '/app/report-calendar', label: 'تقويم التقارير', keywords: 'مواعيد جدول' },
      { to: '/app/compliance', label: 'متابعة الالتزام بالتقارير', roles: COMPLETION },
      { to: '/app/workflows', label: 'مسارات الاعتماد', roles: EXEC_VIEW, keywords: 'اعتماد موافقة' },
      { to: '/app/analytics', label: 'المقارنات والتحليلات', roles: EXEC_VIEW },
      { to: '/app/reports', label: 'التقارير التنفيذية', roles: EXEC_VIEW, keywords: 'الإدارة executive' },
      { to: '/app/sales-aggregation', label: 'تجميع المبيعات', roles: SALES_AGGREGATION },
      { to: '/app/sales/team-dashboard', label: 'لوحة مبيعات الفريق', salesTeamLeaderOnly: true },
      { to: '/app/sales/my-dashboard', label: 'لوحة مبيعاتي', salesRepOnly: true },
      { to: '/app/execution-reports', label: 'تقارير التنفيذ', roles: EXECUTION_REPORTS },
      { to: '/app/execution/team-dashboard', label: 'لوحة تنفيذ الفريق', roles: TEAM_EXECUTION_DASHBOARD },
    ],
  },
  {
    id: 'performance',
    label: 'الأداء وKPI',
    icon: 'kpi',
    tabs: [
      { to: '/app/my-kpi', label: 'مؤشرات أدائي', keywords: 'kpi أداء' },
      { to: '/app/kpi', label: 'مؤشرات الأداء KPI', keywords: 'تقييم' },
      { to: '/app/kpi-templates', label: 'قوالب KPI', roles: TEMPLATE_GOVERNANCE },
      { to: '/app/kpi-finance-export', label: 'تصدير KPI للمالية', roles: KPI_FINANCE_EXPORT, keywords: 'رواتب csv' },
    ],
  },
  {
    // وحدة مستقلّة لمدير الحساب — عنصر تنقّل رئيسيّ مباشر «المشاريع والعملاء»، خارج وحدة
    // «الفرق والموظفون». تبويب واحد ⇒ لا يظهر شريط تبويبات. الظهور مقصور على مدير الحساب
    // (jobRoleCode === 'ACCOUNT_MGR') فقط، لا على دور AccountPortfolioReader. نفس المسار
    // (لا توسيع/تضييق صلاحية؛ الحماية الفعلية مفروضة خادميًّا عبر ProtectedRoute + السياسات).
    id: 'portfolio',
    label: 'المشاريع والعملاء',
    icon: 'projects',
    tabs: [
      { to: '/app/account-portfolio', label: 'مشاريع عملائي', accountManagerOnly: true, keywords: 'محفظة عملائي مدير العميل عملاء مشاريع' },
    ],
  },
  {
    id: 'people',
    label: 'الفرق والموظفون',
    icon: 'teams',
    tabs: [
      { to: '/app/teams', label: 'فرق العمل', roles: EXEC_VIEW },
      { to: '/app/clients', label: 'العملاء', roles: EXEC_VIEW },
      { to: '/app/projects', label: 'المشاريع', roles: EXEC_VIEW },
      { to: '/app/users', label: 'إدارة فريق العمل', roles: USERS_PAGE, keywords: 'مستخدمين' },
      { to: '/app/hr-employees', label: 'إدارة بيانات الموظفين', roles: HR_EMPLOYEE, keywords: 'hr' },
      { to: '/app/job-roles', label: 'مسمّى الموظف الوظيفي', roles: JOB_ROLE_MANAGEMENT },
    ],
  },
  {
    id: 'governance',
    label: 'الحوكمة والمتابعة',
    icon: 'governance',
    tabs: [
      { to: '/app/governance', label: 'الحوكمة والمتابعة', roles: GOVERNANCE, keywords: 'مخاطر قرارات' },
      { to: '/app/governance-workspace', label: 'ورشة الحوكمة', roles: GOVERNANCE_WORKSPACE },
      { to: '/app/governance/escalations', label: 'التصعيدات', roles: GOVERNANCE_ESCALATION },
      { to: '/app/governance/action-items', label: 'إجراءات الحوكمة', roles: GOVERNANCE_ACTION_ITEMS },
      { to: '/app/audit', label: 'سجل التدقيق', roles: AUDIT },
      { to: '/app/admin/archive', label: 'الأرشيف الإداري', roles: ARCHIVE_GOVERNANCE, keywords: 'استرجاع محذوف أرشفة' },
      { to: '/app/email-notifications', label: 'سجل إشعارات البريد', roles: GOVERNANCE },
    ],
  },
  {
    id: 'leave',
    label: 'الإجازات والطلبات',
    icon: 'calendar',
    tabs: [
      { to: '/app/leave-requests', label: 'الإجازات والاستئذانات', keywords: 'إجازة استئذان' },
      // P2-ATT-007 — بلا `roles`: الموظّف يحتاج السطح ليرى بلاغًا يخصّه ويردّ عليه، وقصره على
      // الإدارة يحرمه حقّ الردّ الذي تقوم عليه آلة الحالات كلّها. النطاق مفروض خادميًّا (404 خارجه)،
      // وعلم `Phase2:AttendanceEnabled` المطفأ يجعل السطح كلّه 404 قبل التفعيل.
      { to: '/app/attendance', label: 'الحضور والالتزام', keywords: 'غياب تأخير انصراف واقعة بلاغ' },
      // P2-HR-009 — بلا `roles`: الوصول محكوم بمفتاح `HrOperations.View` وحده لا بالدور،
      // ولا يمكن للواجهة أن تعرف حامله من قائمة أدوار ثابتة.
      { to: '/app/hr-operations', label: 'عمليّات الموارد البشريّة', keywords: 'طوابير إجراءات متأخّر مهلة sla' },
      { to: '/app/balances', label: 'أرصدتي', keywords: 'رصيد' },
      { to: '/app/hr-requests', label: 'طلبات الموارد البشرية', keywords: 'hr خطاب' },
      { to: '/app/balance-management', label: 'إدارة الأرصدة', roles: BALANCE_MANAGEMENT },
      { to: '/app/payroll/leave-impacts', label: 'طلبات مؤثّرة على الراتب', roles: PAYROLL, keywords: 'رواتب مالية' },
    ],
  },
  {
    id: 'settings',
    label: 'الإعدادات',
    icon: 'settings',
    tabs: [
      { to: '/app/report-templates', label: 'قوالب التقارير', roles: TEMPLATE_GOVERNANCE },
      { to: '/app/job-roles/manage', label: 'إدارة المسمّيات الوظيفية', roles: JOB_ROLE_MANAGEMENT },
      { to: '/app/courses', label: 'إدارة الدورات', roles: TEMPLATE_GOVERNANCE },
      { to: '/app/services', label: 'إدارة الخدمات', roles: TEMPLATE_GOVERNANCE },
      { to: '/app/execution-taxonomy', label: 'تصنيفات التنفيذ', roles: TEMPLATE_GOVERNANCE },
      { to: '/app/departments', label: 'الإدارات', roles: ADMIN },
      { to: '/app/positions', label: 'المناصب المرنة', roles: ADMIN },
      { to: '/app/report-view-grants', label: 'منح رؤية التقارير', roles: ADMIN },
      { to: '/app/email-control', label: 'مركز التحكم بالبريد', roles: ADMIN },
      { to: '/app/settings', label: 'الإعدادات العامة', roles: ADMIN },
    ],
  },
];

// ===== مساعدات الرؤية والمطابقة =====

// هل يظهر التبويب للمستخدم الحالي (نفس منطق القائمة القديم: salesRepOnly ⇒ للمندوب، وإلا roles).
export function tabVisible(t: NavTab, ctx: NavCtx): boolean {
  if (t.salesRepOnly) return ctx.isSalesRep;
  if (t.salesTeamLeaderOnly) return ctx.isSalesB2cTeamLeader;
  if (t.accountManagerOnly) return ctx.jobRoleCode === 'ACCOUNT_MGR';
  return !t.roles || ctx.hasAnyRole(...t.roles);
}

// تبويبات الوحدة الظاهرة للمستخدم.
export function visibleTabs(m: NavModule, ctx: NavCtx): NavTab[] {
  return m.tabs.filter((t) => tabVisible(t, ctx));
}

// الوحدات التي بها تبويب ظاهر واحد على الأقل (بترتيب الإعلان).
export function accessibleModules(ctx: NavCtx): NavModule[] {
  return MODULES.filter((m) => m.tabs.some((t) => tabVisible(t, ctx)));
}

// هل يطابق التبويب مسارًا معيّنًا (مطابقة تامّة للرئيسية، وإلا بادئة).
export function tabMatches(t: NavTab, pathname: string): boolean {
  if (t.exact) return pathname === t.to;
  return pathname === t.to || pathname.startsWith(t.to + '/');
}

export interface ActiveNav {
  module: NavModule;
  tab: NavTab;
}

// الوحدة/التبويب الحاوي للمسار الحالي — أطول مطابقة عبر كل التبويبات (بلا فلترة أدوار)
// كي يُبرَز التنقّل حتى عند الوصول المباشر بالرابط. يرجع null لصفحات التفاصيل غير المُبوَّبة.
export function resolveActive(pathname: string, _ctx?: NavCtx): ActiveNav | null {
  let best: { module: NavModule; tab: NavTab } | null = null;
  for (const m of MODULES) {
    for (const t of m.tabs) {
      if (tabMatches(t, pathname) && (!best || t.to.length > best.tab.to.length)) {
        best = { module: m, tab: t };
      }
    }
  }
  return best;
}

// تبويبات قابلة للبحث في الترويسة (كل ما يراه المستخدم فعلًا).
export interface SearchableTab {
  to: string;
  label: string;
  moduleLabel: string;
  keywords: string;
}

export function searchableTabs(ctx: NavCtx): SearchableTab[] {
  const out: SearchableTab[] = [];
  for (const m of MODULES) {
    for (const t of m.tabs) {
      if (!tabVisible(t, ctx)) continue;
      out.push({ to: t.to, label: t.label, moduleLabel: m.label, keywords: t.keywords ?? '' });
    }
  }
  return out;
}

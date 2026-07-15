// نموذج بيانات التنقّل المدمج (NAVIGATION-REGRESSION-HOTFIX-R1) — وحدات رئيسية محدودة في الشريط
// الجانبي + شريط تبويبات أفقي للصفحات الفرعية داخل الوحدة النشطة. تنظيم بصريّ فقط: لا يُنشئ/يحذف
// مسارًا، ولا يوسّع/يضيّق أيّ صلاحية. أدوار كل تبويب مطابقة تمامًا لجدول المسارات في App.tsx،
// والحماية الفعلية مفروضة خادميًّا عبر ProtectedRoute + سياسات الخادم. الغرض: استعادة التنقّل
// المدمج المعتمد بدل قائمة الأكورديون الطويلة. لا مسارات استراتيجية (ورشة الحوكمة/المناصب/التنفيذ).
import type { Role } from '../types/api';
import type { IconName } from '../components/icons';

// ===== مصفوفات الأدوار (منقولة حرفيًّا من جدول App.tsx — بلا تغيير في النطاق) =====
const EXEC_VIEW: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
const SALES_AGGREGATION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager'];
const TEAM_SALES_DASHBOARD: Role[] = ['TeamLeader'];
const ACCOUNT_PORTFOLIO: Role[] = ['AccountPortfolioReader', 'Admin'];
const USERS_PAGE: Role[] = ['Admin', 'CEO', 'CeoSupport'];
const GOVERNANCE: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
const TEMPLATE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];
const JOB_ROLE_MANAGEMENT: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
const BALANCE_MANAGEMENT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'HR'];
const COMPLETION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer', 'HR'];
const HR_EMPLOYEE: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
const PAYROLL: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
const KPI_FINANCE_EXPORT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
const GOVERNANCE_ESCALATION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
const GOVERNANCE_ACTION_ITEMS: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
const AUDIT: Role[] = ['Admin', 'CEO', 'GeneralManager'];

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
  // مطابقة تامّة للمسار (للرئيسية /app فقط) بدل مطابقة البادئة.
  exact?: boolean;
  // كلمات مفتاحية إضافية للبحث (اختيارية، بجانب label).
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
}

// ===== الوحدات =====
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
      { to: '/app/submissions', label: 'التقارير المقدَّمة', keywords: 'تقرير تسليم' },
      { to: '/app/report-calendar', label: 'تقويم التقارير', keywords: 'مواعيد جدول' },
      { to: '/app/compliance', label: 'متابعة الالتزام بالتقارير', roles: COMPLETION },
      { to: '/app/workflows', label: 'مسارات الاعتماد', roles: EXEC_VIEW, keywords: 'اعتماد موافقة' },
      { to: '/app/analytics', label: 'المقارنات والتحليلات', roles: EXEC_VIEW },
      { to: '/app/reports', label: 'التقارير التنفيذية', roles: EXEC_VIEW, keywords: 'الإدارة executive' },
      { to: '/app/sales-aggregation', label: 'تجميع المبيعات', roles: SALES_AGGREGATION },
      { to: '/app/sales/team-dashboard', label: 'لوحة مبيعات الفريق', roles: TEAM_SALES_DASHBOARD },
      { to: '/app/sales/my-dashboard', label: 'لوحة مبيعاتي', salesRepOnly: true },
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
    // وحدة مستقلّة لمحفظة مدير الحساب — عنصر تنقّل رئيسيّ مباشر. تبويب واحد ⇒ لا يظهر شريط تبويبات.
    // نفس المسار والأدوار المعتمدة (AccountPortfolioReader/Admin)؛ الحماية الفعلية مفروضة خادميًّا.
    id: 'portfolio',
    label: 'المشاريع والعملاء',
    icon: 'projects',
    tabs: [
      { to: '/app/account-portfolio', label: 'مشاريع عملائي', roles: ACCOUNT_PORTFOLIO, keywords: 'محفظة مدير الحساب عملاء مشاريع' },
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
      { to: '/app/governance/escalations', label: 'التصعيدات', roles: GOVERNANCE_ESCALATION },
      { to: '/app/governance/action-items', label: 'إجراءات الحوكمة', roles: GOVERNANCE_ACTION_ITEMS },
      { to: '/app/audit', label: 'سجل التدقيق', roles: AUDIT },
      { to: '/app/email-notifications', label: 'سجل إشعارات البريد', roles: GOVERNANCE },
    ],
  },
  {
    id: 'leave',
    label: 'الإجازات والطلبات',
    icon: 'calendar',
    tabs: [
      { to: '/app/leave-requests', label: 'الإجازات والاستئذانات', keywords: 'إجازة استئذان' },
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
      { to: '/app/departments', label: 'الإدارات', roles: ADMIN },
      { to: '/app/report-view-grants', label: 'منح رؤية التقارير', roles: ADMIN },
      { to: '/app/email-control', label: 'مركز التحكم بالبريد', roles: ADMIN },
      { to: '/app/settings', label: 'الإعدادات العامة', roles: ADMIN },
    ],
  },
];

// ===== مساعدات الرؤية والمطابقة =====

// هل يظهر التبويب للمستخدم الحالي (salesRepOnly ⇒ للمندوب، وإلا roles).
export function tabVisible(t: NavTab, ctx: NavCtx): boolean {
  if (t.salesRepOnly) return ctx.isSalesRep;
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
export function resolveActive(pathname: string): ActiveNav | null {
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

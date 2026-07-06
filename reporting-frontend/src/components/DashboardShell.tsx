import { useMemo, useState, type ReactNode } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { roleLabel } from '../lib/format';
import { useNotificationRealtime } from '../lib/useNotifications';
import { NotificationsBell } from './NotificationsBell';
import { NavIcon, type IconName } from './icons';
import type { Role } from '../types/api';

const EXEC_VIEW: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
// إدارة فريق العمل (GOV-R1): Admin + CEO إدارة كاملة؛ CeoSupport عرض + إعادة تعيين كلمات المرور فقط.
const USERS_PAGE: Role[] = ['Admin', 'CEO', 'CeoSupport'];
// الحوكمة (المخاطر/القرارات) مقصورة على من يملك ViewGovernance بالخادم — تطابق RoleAccess.CanViewGovernance.
const GOVERNANCE: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
// حوكمة القوالب وKPI — المستوى الإداري الأعلى (Admin/CEO/GM)، تطابق سياسة TemplateGovernance بالخادم.
const TEMPLATE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];
// إدارة المسمّيات الوظيفية للموظفين (سطح مخصّص) — Admin/CeoSupport/HR/GM/CEO، تطابق سياسة UserJobRoleManagement بالخادم.
const JOB_ROLE_MANAGEMENT: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
// إدارة الأرصدة (خدمات الموظف V1.1) — تطابق سياسة BalanceManagement بالخادم.
const BALANCE_MANAGEMENT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'HR'];
// متابعة الالتزام بالتقارير (per-person) — تطابق سياسة ReportCompletionView بالخادم.
const COMPLETION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer', 'HR'];
// إدارة بيانات الموظفين (حزمة HR A) — اتحاد سياستَي UserBasicManagement و UserOrgAssignment بالخادم.
const HR_EMPLOYEE: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
// المالية والرواتب (FIN-L1) — تطابق سياسة PayrollImpactRead بالخادم (التعديل Admin/HR فقط داخل الصفحة).
const PAYROLL: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
// تصدير KPI للمالية (KPI-FIN1) — تطابق سياسة KpiFinanceExport بالخادم (قراءة/تصدير فقط).
const KPI_FINANCE_EXPORT: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
// محفظة مدير الحساب (مشاريعي) — عرض فقط، تطابق سياسة AccountPortfolioRead بالخادم.
const ACCOUNT_PORTFOLIO: Role[] = ['AccountPortfolioReader', 'Admin'];
// ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) — تطابق سياسة GovernanceWorkspaceAccess؛ الرؤية مقيّدة داخليًّا حسب الدور.
const GOVERNANCE_WORKSPACE: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR'];
// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — تطابق GovernanceEscalationUsers بالخادم (Viewer مستثنى)؛ الرؤية مقيّدة داخليًّا بالنطاق.
const GOVERNANCE_ESCALATION: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — تطابق GovernanceActionItemUsers بالخادم (Viewer مستثنى)؛ الرؤية مقيّدة داخليًّا بالنطاق.
const GOVERNANCE_ACTION_ITEMS: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];

// مجموعات القائمة الجانبية (UI-SIDEBAR-NAV-COLLAPSE-R1) — تنظيم بصري فقط؛ الأدوار لكل عنصر لم تتغيّر.
type NavGroup =
  | 'operations'
  | 'clients'
  | 'performance'
  | 'governance'
  | 'self-service'
  | 'finance'
  | 'build';

export interface NavItem {
  to: string;
  label: string;
  icon: IconName;
  roles?: Role[]; // إن غابت ظهر للجميع
  group: NavGroup;
}

const NAV: NavItem[] = [
  // أ) التشغيل والمتابعة
  { to: '/app', label: 'الرئيسية', icon: 'home', group: 'operations' },
  { to: '/app/submissions', label: 'التقارير المقدمة', icon: 'reports', group: 'operations' },
  { to: '/app/compliance', label: 'متابعة الالتزام بالتقارير', icon: 'reports', roles: COMPLETION, group: 'operations' },
  { to: '/app/workflows', label: 'مسارات الاعتماد', icon: 'workflow', roles: EXEC_VIEW, group: 'operations' },
  { to: '/app/report-calendar', label: 'تقويم التقارير', icon: 'calendar', group: 'operations' },
  { to: '/app/analytics', label: 'المقارنات والتحليلات', icon: 'analytics', roles: EXEC_VIEW, group: 'operations' },
  { to: '/app/sales-aggregation', label: 'تجميع المبيعات', icon: 'analytics', roles: EXEC_VIEW, group: 'operations' },
  // ب) الفرق والعملاء
  { to: '/app/teams', label: 'فرق العمل', icon: 'teams', roles: EXEC_VIEW, group: 'clients' },
  { to: '/app/clients', label: 'العملاء', icon: 'clients', roles: EXEC_VIEW, group: 'clients' },
  { to: '/app/projects', label: 'المشاريع', icon: 'projects', roles: EXEC_VIEW, group: 'clients' },
  { to: '/app/account-portfolio', label: 'مشاريعي', icon: 'projects', roles: ACCOUNT_PORTFOLIO, group: 'clients' },
  // ج) الأداء وKPI
  // مؤشرات أداء الموظّف الحالي (KPI-MY-KPI-NAV-VISIBILITY-R1) — صفحة شخصية للمستخدم الحالي فقط، تظهر للجميع؛ النطاق مفروض خادمًا (يرى نفسه فقط). أولًا داخل القسم لأنها الصفحة الشخصية الأساسية.
  { to: '/app/my-kpi', label: 'مؤشرات أدائي', icon: 'kpi', group: 'performance' },
  { to: '/app/kpi', label: 'مؤشرات الأداء KPI', icon: 'kpi', group: 'performance' },
  { to: '/app/kpi-templates', label: 'قوالب KPI', icon: 'kpiTemplate', roles: TEMPLATE_GOVERNANCE, group: 'performance' },
  // د) الحوكمة
  { to: '/app/governance', label: 'الحوكمة والمتابعة', icon: 'governance', roles: GOVERNANCE, group: 'governance' },
  { to: '/app/governance-workspace', label: 'ورشة الحوكمة', icon: 'governance', roles: GOVERNANCE_WORKSPACE, group: 'governance' },
  { to: '/app/governance/escalations', label: 'التصعيدات', icon: 'governance', roles: GOVERNANCE_ESCALATION, group: 'governance' },
  { to: '/app/governance/action-items', label: 'إجراءات الحوكمة', icon: 'governance', roles: GOVERNANCE_ACTION_ITEMS, group: 'governance' },
  { to: '/app/audit', label: 'سجل التدقيق', icon: 'audit', roles: ['Admin', 'CEO', 'GeneralManager'], group: 'governance' },
  { to: '/app/email-notifications', label: 'سجل إشعارات البريد', icon: 'reports', roles: GOVERNANCE, group: 'governance' },
  // هـ) خدمات الموظف
  { to: '/app/leave-requests', label: 'الإجازات والاستئذانات', icon: 'calendar', group: 'self-service' },
  { to: '/app/balances', label: 'أرصدتي', icon: 'kpi', group: 'self-service' },
  { to: '/app/hr-requests', label: 'طلبات الموارد البشرية', icon: 'reports', group: 'self-service' },
  // و) المالية والرواتب
  { to: '/app/payroll/leave-impacts', label: 'طلبات مؤثّرة على الراتب', icon: 'reports', roles: PAYROLL, group: 'finance' },
  { to: '/app/balance-management', label: 'إدارة الأرصدة', icon: 'kpi', roles: BALANCE_MANAGEMENT, group: 'finance' },
  { to: '/app/kpi-finance-export', label: 'تصدير KPI للمالية', icon: 'kpi', roles: KPI_FINANCE_EXPORT, group: 'finance' },
  // ز) البناء والإعداد
  { to: '/app/report-templates', label: 'قوالب التقارير', icon: 'template', roles: TEMPLATE_GOVERNANCE, group: 'build' },
  { to: '/app/hr-employees', label: 'إدارة بيانات الموظفين', icon: 'users', roles: HR_EMPLOYEE, group: 'build' },
  { to: '/app/users', label: 'إدارة فريق العمل', icon: 'users', roles: USERS_PAGE, group: 'build' },
  { to: '/app/job-roles', label: 'مسمّى الموظف الوظيفي', icon: 'users', roles: JOB_ROLE_MANAGEMENT, group: 'build' },
  { to: '/app/job-roles/manage', label: 'إدارة المسمّيات الوظيفية', icon: 'template', roles: JOB_ROLE_MANAGEMENT, group: 'build' },
  { to: '/app/positions', label: 'المناصب المرنة', icon: 'users', roles: ADMIN, group: 'build' },
  { to: '/app/report-view-grants', label: 'منح رؤية التقارير', icon: 'reports', roles: ADMIN, group: 'build' },
  { to: '/app/email-control', label: 'مركز التحكم بالبريد', icon: 'reports', roles: ADMIN, group: 'build' },
  { to: '/app/departments', label: 'الإدارات', icon: 'departments', roles: ADMIN, group: 'build' },
  { to: '/app/courses', label: 'إدارة الدورات', icon: 'template', roles: TEMPLATE_GOVERNANCE, group: 'build' },
  { to: '/app/settings', label: 'الإعدادات', icon: 'settings', roles: ADMIN, group: 'build' },
];

const GROUP_ORDER: NavGroup[] = ['operations', 'clients', 'performance', 'governance', 'self-service', 'finance', 'build'];

const GROUP_LABEL: Record<NavGroup, string> = {
  operations: 'التشغيل والمتابعة',
  clients: 'الفرق والعملاء',
  performance: 'الأداء وKPI',
  governance: 'الحوكمة',
  'self-service': 'خدمات الموظف',
  finance: 'المالية والرواتب',
  build: 'البناء والإعداد',
};

// المجموعات المفتوحة افتراضيًا (الأكثر استخدامًا يوميًا)؛ البقية مطوية لتفادي طول القائمة.
const DEFAULT_OPEN: Record<NavGroup, boolean> = {
  operations: true,
  clients: false,
  performance: false,
  governance: false,
  'self-service': true,
  finance: false,
  build: false,
};

// مفتاح حفظ حالة الطي/الفتح لكل متصفّح.
const NAV_STATE_KEY = 'me_nav_groups_v1';

function readSavedGroups(): Record<NavGroup, boolean> {
  const base = { ...DEFAULT_OPEN };
  try {
    const raw = localStorage.getItem(NAV_STATE_KEY);
    if (raw) {
      const saved = JSON.parse(raw) as Partial<Record<NavGroup, boolean>>;
      for (const g of GROUP_ORDER) if (typeof saved[g] === 'boolean') base[g] = saved[g] as boolean;
    }
  } catch {
    /* تجاهل أي حالة محفوظة تالفة/قديمة */
  }
  return base;
}

export function DashboardShell({ children }: { children: ReactNode }) {
  const { user, logout, hasAnyRole } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [openGroups, setOpenGroups] = useState<Record<NavGroup, boolean>>(readSavedGroups);
  useNotificationRealtime();

  const items = NAV.filter((n) => !n.roles || hasAnyRole(...n.roles));
  // المجموعات الظاهرة فعلًا (كل منها بها عنصر مسموح واحد على الأقل)، بترتيب ثابت.
  const visibleGroups = GROUP_ORDER.filter((g) => items.some((n) => n.group === g));

  // المجموعة الحاوية للمسار الحالي (أطول مطابقة) — تُفتح تلقائيًا.
  const activeGroup = useMemo<NavGroup | null>(() => {
    let best: { to: string; group: NavGroup } | null = null;
    for (const n of items) {
      const match =
        n.to === '/app'
          ? location.pathname === '/app'
          : location.pathname === n.to || location.pathname.startsWith(n.to + '/');
      if (match && (!best || n.to.length > best.to.length)) best = { to: n.to, group: n.group };
    }
    return best?.group ?? null;
  }, [items, location.pathname]);

  function persistGroups(next: Record<NavGroup, boolean>) {
    setOpenGroups(next);
    try {
      localStorage.setItem(NAV_STATE_KEY, JSON.stringify(next));
    } catch {
      /* تخزين غير متاح — نتجاهل بصمت */
    }
  }

  function toggleGroup(g: NavGroup) {
    persistGroups({ ...openGroups, [g]: !openGroups[g] });
  }

  function setAllGroups(value: boolean) {
    const next = { ...openGroups };
    for (const g of visibleGroups) next[g] = value;
    persistGroups(next);
  }

  async function handleLogout() {
    await logout();
    navigate('/login');
  }

  return (
    <div className="min-h-screen bg-offwhite text-ink">
      <header className="sticky top-0 z-20 bg-navy text-white shadow-sm">
        <div className="flex items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <button
              aria-label="القائمة"
              className="rounded-lg p-2 hover:bg-navy-600 lg:hidden"
              onClick={() => setOpen((v) => !v)}
            >
              ☰
            </button>
            <div className="rounded-lg bg-white px-3 py-1.5">
              <img src="/logo-arabic.png" alt="خبراء التسويق" className="h-7" />
            </div>
            <span className="hidden text-sm font-semibold text-navy-100 md:block">
              نظام تقارير الأداء والتشغيل
            </span>
          </div>
          <div className="flex items-center gap-3">
            <NotificationsBell />
            <div className="hidden text-left sm:block">
              <p className="text-sm font-semibold">{user?.fullName}</p>
              <p className="text-xs text-navy-100">
                {user?.roles.map((r) => roleLabel[r]).join(' · ')}
              </p>
            </div>
            <button
              onClick={handleLogout}
              className="rounded-lg bg-navy-600 px-3 py-1.5 text-sm hover:bg-navy-800"
            >
              خروج
            </button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex max-w-7xl gap-6 px-4 py-6">
        <aside className={`${open ? 'block' : 'hidden'} w-full shrink-0 lg:block lg:w-64`}>
          <nav className="space-y-1.5 rounded-2xl border border-line bg-white p-3">
            <div className="flex items-center justify-between px-1 pb-0.5">
              <span className="text-[11px] font-bold text-ink-3">القائمة</span>
              <div className="flex gap-1">
                <button
                  type="button"
                  onClick={() => setAllGroups(true)}
                  className="rounded px-2 py-0.5 text-[11px] font-medium text-navy-600 hover:bg-navy-50"
                >
                  فتح الكل
                </button>
                <button
                  type="button"
                  onClick={() => setAllGroups(false)}
                  className="rounded px-2 py-0.5 text-[11px] font-medium text-navy-600 hover:bg-navy-50"
                >
                  طي الكل
                </button>
              </div>
            </div>
            {visibleGroups.map((g) => {
              const gItems = items.filter((n) => n.group === g);
              // المجموعة الحاوية للمسار الحالي تُفتح تلقائيًا حتى لو طواها المستخدم.
              const expanded = openGroups[g] || g === activeGroup;
              return (
                <div key={g}>
                  <button
                    type="button"
                    onClick={() => toggleGroup(g)}
                    aria-expanded={expanded}
                    className="flex w-full items-center justify-between rounded-lg px-3 py-1.5 text-[11px] font-bold uppercase tracking-wide text-ink-3 transition hover:bg-navy-50"
                  >
                    <span>{GROUP_LABEL[g]}</span>
                    <svg
                      className={`h-3.5 w-3.5 shrink-0 transition-transform ${expanded ? '' : '-rotate-90'}`}
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth={2}
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      aria-hidden="true"
                    >
                      <path d="m6 9 6 6 6-6" />
                    </svg>
                  </button>
                  {expanded && (
                    <div className="mt-0.5 space-y-0.5">
                      {gItems.map((n) => (
                        <NavLink
                          key={n.to}
                          to={n.to}
                          end={n.to === '/app'}
                          onClick={() => setOpen(false)}
                          className={({ isActive }) =>
                            `flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition ${
                              isActive ? 'bg-navy text-white shadow-sm' : 'text-ink hover:bg-navy-50'
                            }`
                          }
                        >
                          {({ isActive }) => (
                            <>
                              <NavIcon
                                name={n.icon}
                                className={`h-[18px] w-[18px] shrink-0 ${isActive ? 'text-white' : 'text-navy-600'}`}
                              />
                              <span className="truncate">{n.label}</span>
                            </>
                          )}
                        </NavLink>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}

            <div className="mt-2 rounded-xl bg-gradient-to-l from-orange to-orange-600 p-3 text-white">
              <p className="text-sm font-bold">بحاجة لمساعدة؟</p>
              <p className="mt-0.5 text-xs text-white/90">دليل الاستخدام المصوّر متاح في مركز المساعدة.</p>
            </div>
          </nav>
        </aside>

        <main className="min-w-0 flex-1">{children}</main>
      </div>
    </div>
  );
}

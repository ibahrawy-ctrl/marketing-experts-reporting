import { useState, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { roleLabel } from '../lib/format';
import { useNotificationRealtime } from '../lib/useNotifications';
import { NotificationsBell } from './NotificationsBell';
import { NavIcon, type IconName } from './icons';
import type { Role } from '../types/api';

const EXEC_VIEW: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
// الحوكمة (المخاطر/القرارات) مقصورة على من يملك ViewGovernance بالخادم — تطابق RoleAccess.CanViewGovernance.
const GOVERNANCE: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
// حوكمة القوالب وKPI — المستوى الإداري الأعلى (Admin/CEO/GM)، تطابق سياسة TemplateGovernance بالخادم.
const TEMPLATE_GOVERNANCE: Role[] = ['Admin', 'CEO', 'GeneralManager'];

export interface NavItem {
  to: string;
  label: string;
  icon: IconName;
  roles?: Role[]; // إن غابت ظهر للجميع
  group: 'main' | 'build' | 'admin';
}

const NAV: NavItem[] = [
  { to: '/app', label: 'الرئيسية', icon: 'home', group: 'main' },
  { to: '/app/teams', label: 'فرق العمل', icon: 'teams', roles: EXEC_VIEW, group: 'main' },
  { to: '/app/clients', label: 'العملاء', icon: 'clients', roles: EXEC_VIEW, group: 'main' },
  { to: '/app/projects', label: 'المشاريع', icon: 'projects', roles: EXEC_VIEW, group: 'main' },
  { to: '/app/submissions', label: 'التقارير المقدمة', icon: 'reports', group: 'main' },
  { to: '/app/kpi', label: 'مؤشرات الأداء KPI', icon: 'kpi', group: 'main' },
  { to: '/app/report-calendar', label: 'تقويم التقارير', icon: 'calendar', group: 'main' },
  { to: '/app/leave-requests', label: 'الإجازات والاستئذانات', icon: 'calendar', group: 'main' },
  { to: '/app/workflows', label: 'مسارات الاعتماد', icon: 'workflow', roles: EXEC_VIEW, group: 'main' },
  { to: '/app/governance', label: 'الحوكمة والمتابعة', icon: 'governance', roles: GOVERNANCE, group: 'main' },
  { to: '/app/analytics', label: 'المقارنات والتحليلات', icon: 'analytics', roles: EXEC_VIEW, group: 'main' },
  { to: '/app/report-templates', label: 'قوالب التقارير', icon: 'template', roles: TEMPLATE_GOVERNANCE, group: 'build' },
  { to: '/app/kpi-templates', label: 'قوالب KPI', icon: 'kpiTemplate', roles: TEMPLATE_GOVERNANCE, group: 'build' },
  { to: '/app/users', label: 'إدارة فريق العمل', icon: 'users', roles: ADMIN, group: 'admin' },
  { to: '/app/departments', label: 'الإدارات', icon: 'departments', roles: ADMIN, group: 'admin' },
  { to: '/app/settings', label: 'الإعدادات', icon: 'settings', roles: ADMIN, group: 'admin' },
  { to: '/app/audit', label: 'سجل التدقيق', icon: 'audit', roles: ['Admin', 'CEO', 'GeneralManager'], group: 'admin' },
];

const GROUP_LABEL: Record<NavItem['group'], string> = {
  main: 'التشغيل والمتابعة',
  build: 'البناء والإعداد',
  admin: 'الإدارة والنظام',
};

export function DashboardShell({ children }: { children: ReactNode }) {
  const { user, logout, hasAnyRole } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  useNotificationRealtime();

  const items = NAV.filter((n) => !n.roles || hasAnyRole(...n.roles));
  const groups: NavItem['group'][] = ['main', 'build', 'admin'];

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
          <nav className="space-y-4 rounded-2xl border border-line bg-white p-3">
            {groups.map((g) => {
              const gItems = items.filter((n) => n.group === g);
              if (gItems.length === 0) return null;
              return (
                <div key={g}>
                  <p className="px-3 pb-1.5 pt-1 text-[11px] font-bold uppercase tracking-wide text-ink-3">
                    {GROUP_LABEL[g]}
                  </p>
                  <div className="space-y-1">
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

// الهيكل العام (UI-NAV-RESTRUCTURE-R2) — شريط جانبي بـ7 وحدات + شريط تبويبات داخل الوحدة النشطة.
// تنظيم بصريّ فقط: يقرأ نموذج التنقّل من navConfig (لا مسار جديد/محذوف، لا توسيع/تضييق صلاحية).
// الحماية الفعلية مفروضة عبر ProtectedRoute + سياسات الخادم.
import { useMemo, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { useNotificationRealtime } from '../lib/useNotifications';
import { NotificationsBell } from './NotificationsBell';
import { NavIcon } from './icons';
import { HeaderQuickTools, ProfileMenu } from './HeaderActions';
import {
  accessibleModules,
  resolveActive,
  visibleTabs,
  type NavCtx,
  type NavModule,
} from '../lib/navConfig';

// مفتاح طيّ الشريط الجانبي إلى شريط أيقونات على سطح المكتب (مفتاح v2 لتفادي تعارض الحالة القديمة).
const COLLAPSE_KEY = 'me_nav_collapsed_v2';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === '1';
  } catch {
    return false;
  }
}

export function DashboardShell({ children }: { children: ReactNode }) {
  const { user, hasAnyRole, isSalesRep, isSalesB2cTeamLeader } = useAuth();
  const jobRoleCode = user?.jobRoleCode ?? null;
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState<boolean>(readCollapsed);
  useNotificationRealtime();

  const ctx = useMemo<NavCtx>(() => ({ hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode }), [hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode]);
  const modules = useMemo(() => accessibleModules(ctx), [ctx]);
  const active = useMemo(() => resolveActive(location.pathname), [location.pathname]);

  // الوحدة النشطة (للتظليل) والتبويبات الظاهرة داخلها (لشريط التبويبات).
  const activeModuleId = active?.module.id ?? null;
  const activeModule = active?.module ?? null;
  const activeTabs = useMemo(
    () => (activeModule ? visibleTabs(activeModule, ctx) : []),
    [activeModule, ctx],
  );

  function persistCollapsed(v: boolean) {
    setCollapsed(v);
    try {
      localStorage.setItem(COLLAPSE_KEY, v ? '1' : '0');
    } catch {
      /* تخزين غير متاح — نتجاهل بصمت */
    }
  }

  // أول تبويب ظاهر لوحدةٍ ما = وجهة النقر على أيقونة الوحدة في الشريط الجانبي.
  function moduleTarget(m: NavModule): string {
    return visibleTabs(m, ctx)[0]?.to ?? '/app';
  }

  return (
    <div className="min-h-screen bg-offwhite text-ink">
      <header className="sticky top-0 z-20 bg-navy text-white shadow-sm">
        <div className="flex items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <button
              aria-label="القائمة"
              className="rounded-lg p-2 hover:bg-navy-600 lg:hidden"
              onClick={() => setMobileOpen((v) => !v)}
            >
              ☰
            </button>
            <button
              type="button"
              aria-label={collapsed ? 'توسيع القائمة' : 'طيّ القائمة'}
              title={collapsed ? 'توسيع القائمة' : 'طيّ القائمة'}
              className="hidden rounded-lg p-2 hover:bg-navy-600 lg:block"
              onClick={() => persistCollapsed(!collapsed)}
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
          <div className="flex items-center gap-2">
            <HeaderQuickTools />
            <NotificationsBell />
            <ProfileMenu />
          </div>
        </div>
      </header>

      <div className="mx-auto flex max-w-7xl gap-6 px-4 py-6">
        <aside
          className={`${mobileOpen ? 'block' : 'hidden'} w-full shrink-0 lg:block ${
            collapsed ? 'lg:w-20' : 'lg:w-64'
          }`}
        >
          <nav className="space-y-1 rounded-2xl border border-line bg-white p-3">
            {modules.map((m) => {
              const isActive = m.id === activeModuleId;
              return (
                <NavLink
                  key={m.id}
                  to={moduleTarget(m)}
                  onClick={() => setMobileOpen(false)}
                  title={m.label}
                  className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition ${
                    isActive ? 'bg-navy text-white shadow-sm' : 'text-ink hover:bg-navy-50'
                  } ${collapsed ? 'lg:justify-center' : ''}`}
                >
                  <NavIcon
                    name={m.icon}
                    className={`h-5 w-5 shrink-0 ${isActive ? 'text-white' : 'text-navy-600'}`}
                  />
                  <span className={`truncate ${collapsed ? 'lg:hidden' : ''}`}>{m.label}</span>
                </NavLink>
              );
            })}
          </nav>
        </aside>

        <main className="min-w-0 flex-1">
          {activeTabs.length > 1 && (
            <div className="mb-4 overflow-x-auto rounded-2xl border border-line bg-white p-1.5">
              <div className="flex items-center gap-1">
                {activeTabs.map((t) => {
                  const tabActive = active?.tab.to === t.to;
                  return (
                    <NavLink
                      key={t.to}
                      to={t.to}
                      className={`whitespace-nowrap rounded-lg px-3.5 py-2 text-sm font-medium transition ${
                        tabActive ? 'bg-navy text-white shadow-sm' : 'text-ink-2 hover:bg-navy-50'
                      }`}
                    >
                      {t.label}
                    </NavLink>
                  );
                })}
              </div>
            </div>
          )}
          {children}
        </main>
      </div>
    </div>
  );
}

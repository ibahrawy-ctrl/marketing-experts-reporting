// الهيكل العام (P3-NAV-002/004) — شريط جانبيّ بالوحدات السبع الثابتة + شريط أقسام بفائض
// «المزيد ⋯» + فتات خبز سياقيّة.
//
// **يستهلك سجلّ الملاحة ولا يقرّر شيئًا بنفسه**: لا شرط ظهور واحد مكتوب هنا. الحماية الفعليّة
// مفروضة عبر `ProtectedRoute` وسياسات الخادم؛ ما هنا عرضٌ فقط.
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useNavCtx } from '../lib/useNavCtx';
import { useNotificationRealtime } from '../lib/useNotifications';
import { NotificationsBell } from './NotificationsBell';
import { NavIcon } from './icons';
import { HeaderQuickTools, ProfileMenu } from './HeaderActions';
import { NavOverflow, type OverflowTab } from './NavOverflow';
import { Breadcrumbs } from './Breadcrumbs';
import {
  accessibleModules,
  itemTarget,
  moduleTarget,
  resolveActive,
  resolveBadge,
  visibleItems,
} from '../lib/navConfig';

// مفتاح طيّ الشريط الجانبي إلى شريط أيقونات على سطح المكتب.
// تفضيل عرض بحت — لا يُخزَّن فيه أيّ دور أو صلاحيّة أو بيان مستخدم.
const COLLAPSE_KEY = 'me_nav_collapsed_v2';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === '1';
  } catch {
    return false;
  }
}

export function DashboardShell({ children }: { children: ReactNode }) {
  const ctx = useNavCtx();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState<boolean>(readCollapsed);
  const drawerRef = useRef<HTMLElement>(null);
  const menuButtonRef = useRef<HTMLButtonElement>(null);
  useNotificationRealtime();

  const modules = useMemo(() => accessibleModules(ctx), [ctx]);
  const active = useMemo(() => resolveActive(location.pathname), [location.pathname]);
  const activeModule = active?.module ?? null;

  // أقسام الوحدة النشطة، مرتّبة كما في السجلّ ومحمولة إلى شريط الفائض.
  // لا عدّادات مُنتَجة هنا: لا سطح يُصدِر أرقامًا للقائمة بعد، و`resolveBadge` يحجب أيّ رقم
  // لعنصر غير ظاهر للمستخدم نفسه — فلا يمكن بنيويًّا تسريب عدّ خارج نطاقه.
  const badges = useMemo<Record<string, number | undefined>>(() => ({}), []);
  const tabs = useMemo<OverflowTab[]>(() => {
    if (!activeModule) return [];
    return visibleItems(activeModule, ctx).map((item) => ({
      id: item.id,
      to: itemTarget(item, ctx),
      label: item.label,
      groupLabel: activeModule.groups?.find((g) => g.id === item.group)?.label ?? null,
      badge: resolveBadge(item, ctx, badges),
    }));
  }, [activeModule, ctx, badges]);

  // درج الهاتف: Escape يُغلق ويعيد التركيز إلى زرّ الفتح، والتركيز محبوس داخل الدرج.
  useEffect(() => {
    if (!mobileOpen) return;
    const node = drawerRef.current;
    node?.querySelector<HTMLElement>('a,button')?.focus();

    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        setMobileOpen(false);
        menuButtonRef.current?.focus();
        return;
      }
      if (e.key !== 'Tab' || !node) return;
      const focusables = node.querySelectorAll<HTMLElement>('a[href],button:not([disabled])');
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [mobileOpen]);

  function persistCollapsed(v: boolean) {
    setCollapsed(v);
    try {
      localStorage.setItem(COLLAPSE_KEY, v ? '1' : '0');
    } catch {
      /* تخزين غير متاح — نتجاهل بصمت */
    }
  }

  const sideNav = (
    <nav className="space-y-1 rounded-2xl border border-line bg-white p-3" aria-label="الوحدات الرئيسية">
      {modules.map((m) => {
        const isActive = m.id === activeModule?.id;
        return (
          <NavLink
            key={m.id}
            to={moduleTarget(m, ctx)}
            title={m.label}
            aria-current={isActive ? 'page' : undefined}
            className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange ${
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
  );

  return (
    <div className="min-h-screen bg-offwhite text-ink">
      <header className="sticky top-0 z-20 bg-navy text-white shadow-sm">
        <div className="flex items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <button
              ref={menuButtonRef}
              aria-label="القائمة"
              aria-expanded={mobileOpen}
              aria-controls="nav-drawer"
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

      {/* درج الهاتف — يفتح من اليمين (اتّجاه RTL) فوق المحتوى، بطبقة تعتيم تُغلقه بالنقر خارجه. */}
      {mobileOpen && (
        <div className="fixed inset-0 z-30 lg:hidden">
          <div
            className="absolute inset-0 bg-black/40"
            aria-hidden="true"
            onClick={() => setMobileOpen(false)}
          />
          <aside
            id="nav-drawer"
            ref={drawerRef}
            role="dialog"
            aria-modal="true"
            aria-label="قائمة التنقّل"
            // النقر على أيّ رابط داخل الدرج يُغلقه كي لا يبقى فوق المحتوى بعد التنقّل.
            onClick={() => setMobileOpen(false)}
            className="absolute inset-y-0 right-0 w-72 max-w-[85vw] overflow-y-auto bg-offwhite p-3 shadow-xl"
          >
            {sideNav}
          </aside>
        </div>
      )}

      <div className="mx-auto flex max-w-7xl gap-6 px-4 py-6">
        <aside className={`hidden shrink-0 lg:block ${collapsed ? 'lg:w-20' : 'lg:w-64'}`}>
          {sideNav}
        </aside>

        <main className="min-w-0 flex-1">
          <Breadcrumbs ctx={ctx} />
          <NavOverflow tabs={tabs} activeId={active?.item.id ?? null} />
          {children}
        </main>
      </div>
    </div>
  );
}

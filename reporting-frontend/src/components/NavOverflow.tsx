// شريط التبويبات مع فائض «المزيد ⋯» (P3-NAV-004).
//
// حدّ ثابت: **7 عناصر مرئيّة كحدّ أقصى** في الصفّ الأفقيّ، والباقي داخل قائمة «المزيد ⋯».
// العنصر النشط إن وقع في الفائض يُبرَز الزرّ نفسه كنشط كي لا يفقد المستخدم موضعه.
// القائمة قابلة للتنقّل بالكيبورد كاملًا (Escape للإغلاق، النقر خارجها يُغلقها).
import { useEffect, useRef, useState } from 'react';
import { NavLink } from 'react-router-dom';

/// الحدّ الأقصى للعناصر المرئيّة قبل الطيّ في «المزيد ⋯».
export const MAX_VISIBLE_TABS = 7;

export interface OverflowTab {
  id: string;
  to: string;
  label: string;
  /// تسمية المجموعة إن كان العنصر ينتمي إليها (تُعرَض كعنوان داخل «المزيد»).
  groupLabel?: string | null;
  badge?: number | null;
}

function TabBadge({ value }: { value: number | null | undefined }) {
  // الصفر يُخفى باتّساق: العدّاد إشعارٌ بعمل مطلوب، ولا عمل عند الصفر.
  if (typeof value !== 'number' || value <= 0) return null;
  return (
    <span className="mr-1.5 inline-flex min-w-5 justify-center rounded-full bg-orange px-1.5 py-0.5 text-[11px] font-bold text-white">
      {value}
    </span>
  );
}

export function NavOverflow({ tabs, activeId }: { tabs: OverflowTab[]; activeId: string | null }) {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  const visible = tabs.slice(0, MAX_VISIBLE_TABS);
  const overflow = tabs.slice(MAX_VISIBLE_TABS);
  const activeInOverflow = overflow.some((t) => t.id === activeId);

  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false);
    }
    function onClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onClick);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onClick);
    };
  }, [open]);

  if (tabs.length <= 1) return null;

  return (
    <div className="mb-4 rounded-2xl border border-line bg-white p-1.5">
      {/* `flex-wrap` لا `overflow-x-auto`: لا قصّ أفقيّ ولا تمرير غير مقصود على أيّ عرض. */}
      <div className="flex flex-wrap items-center gap-1" role="tablist" aria-label="أقسام الوحدة">
        {visible.map((t) => (
          <NavLink
            key={t.id}
            to={t.to}
            role="tab"
            aria-current={t.id === activeId ? 'page' : undefined}
            aria-selected={t.id === activeId}
            className={`whitespace-nowrap rounded-lg px-3.5 py-2 text-sm font-medium transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange ${
              t.id === activeId ? 'bg-navy text-white shadow-sm' : 'text-ink-2 hover:bg-navy-50'
            }`}
          >
            {t.label}
            <TabBadge value={t.badge} />
          </NavLink>
        ))}

        {overflow.length > 0 && (
          <div className="relative" ref={wrapRef}>
            <button
              type="button"
              aria-haspopup="menu"
              aria-expanded={open}
              aria-current={activeInOverflow ? 'page' : undefined}
              onClick={() => setOpen((v) => !v)}
              className={`whitespace-nowrap rounded-lg px-3.5 py-2 text-sm font-medium transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange ${
                activeInOverflow ? 'bg-navy text-white shadow-sm' : 'text-ink-2 hover:bg-navy-50'
              }`}
            >
              المزيد ⋯
            </button>
            {open && (
              <div
                role="menu"
                aria-label="المزيد من الأقسام"
                className="absolute left-0 z-30 mt-1 min-w-56 rounded-xl border border-line bg-white p-1.5 shadow-lg"
              >
                {overflow.map((t, idx) => {
                  const showGroup = !!t.groupLabel && t.groupLabel !== overflow[idx - 1]?.groupLabel;
                  return (
                    <div key={t.id}>
                      {showGroup && (
                        <div className="px-3 pb-1 pt-2 text-[11px] font-semibold text-ink-3">{t.groupLabel}</div>
                      )}
                      <NavLink
                        to={t.to}
                        role="menuitem"
                        onClick={() => setOpen(false)}
                        aria-current={t.id === activeId ? 'page' : undefined}
                        className={`block rounded-lg px-3 py-2 text-sm transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange ${
                          t.id === activeId ? 'bg-navy text-white' : 'text-ink-2 hover:bg-navy-50'
                        }`}
                      >
                        {t.label}
                        <TabBadge value={t.badge} />
                      </NavLink>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

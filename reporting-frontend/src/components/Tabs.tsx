// Tabs — تبويبات داخل الصفحة الواحدة (UX-PRIMITIVES Phase 0).
// تنظيم بصريّ فقط: لا يحذف بيانات ولا يغيّر صلاحية ولا يمسّ backend.
// يدعم النمطين: غير مُتحكَّم (defaultTabId) أو مُتحكَّم (value + onChange).
// a11y: أدوار tablist/tab/tabpanel، roving tabindex، تنقّل بالأسهم/Home/End مع مراعاة RTL،
// تفعيل تلقائيّ عند التنقّل (نمط APG المعتاد). العنونة عبر aria-controls/aria-labelledby.
import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';

export type TabItem = {
  id: string;
  label: ReactNode;
  content: ReactNode;
  disabled?: boolean;
};

export function Tabs({
  items,
  defaultTabId,
  value,
  onChange,
  dir = 'rtl',
  className = '',
  ariaLabel,
}: {
  items: TabItem[];
  defaultTabId?: string;
  value?: string;
  onChange?: (id: string) => void;
  dir?: 'rtl' | 'ltr';
  className?: string;
  ariaLabel?: string;
}) {
  const baseId = useId();
  const firstEnabled = items.find((t) => !t.disabled)?.id ?? items[0]?.id ?? '';
  const [internal, setInternal] = useState<string>(defaultTabId ?? firstEnabled);
  const active = value ?? internal;
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  function select(id: string) {
    if (value === undefined) setInternal(id);
    onChange?.(id);
  }

  // فهارس التبويبات المُفعّلة فقط (نتخطّى المعطّلة أثناء التنقّل).
  const enabledIds = items.filter((t) => !t.disabled).map((t) => t.id);

  function focusAndSelect(id: string) {
    select(id);
    // نؤجّل نقل التركيز لِما بعد إعادة التصيير كي يكون tabIndex محدّثًا.
    requestAnimationFrame(() => tabRefs.current[id]?.focus());
  }

  function onKeyDown(e: KeyboardEvent<HTMLButtonElement>, id: string) {
    const idx = enabledIds.indexOf(id);
    if (idx < 0) return;
    const last = enabledIds.length - 1;
    // في RTL يُعكس اتجاه السهمين الأفقيَّين.
    const forwardKey = dir === 'rtl' ? 'ArrowLeft' : 'ArrowRight';
    const backwardKey = dir === 'rtl' ? 'ArrowRight' : 'ArrowLeft';
    let next: string | null = null;
    if (e.key === forwardKey) next = enabledIds[idx === last ? 0 : idx + 1];
    else if (e.key === backwardKey) next = enabledIds[idx === 0 ? last : idx - 1];
    else if (e.key === 'Home') next = enabledIds[0];
    else if (e.key === 'End') next = enabledIds[last];
    if (next) {
      e.preventDefault();
      focusAndSelect(next);
    }
  }

  const activeItem = items.find((t) => t.id === active) ?? items[0];

  return (
    <div className={className}>
      <div
        role="tablist"
        aria-label={ariaLabel}
        aria-orientation="horizontal"
        dir={dir}
        className="flex items-center gap-1 overflow-x-auto rounded-2xl border border-line bg-white p-1.5"
      >
        {items.map((t) => {
          const isActive = t.id === active;
          return (
            <button
              key={t.id}
              ref={(el) => {
                tabRefs.current[t.id] = el;
              }}
              type="button"
              role="tab"
              id={`${baseId}-tab-${t.id}`}
              aria-selected={isActive}
              aria-controls={`${baseId}-panel-${t.id}`}
              aria-disabled={t.disabled || undefined}
              tabIndex={isActive ? 0 : -1}
              disabled={t.disabled}
              onClick={() => !t.disabled && select(t.id)}
              onKeyDown={(e) => onKeyDown(e, t.id)}
              className={`whitespace-nowrap rounded-lg px-3.5 py-2 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-40 ${
                isActive ? 'bg-navy text-white shadow-sm' : 'text-ink-2 hover:bg-navy-50'
              }`}
            >
              {t.label}
            </button>
          );
        })}
      </div>

      {activeItem && (
        <div
          role="tabpanel"
          id={`${baseId}-panel-${activeItem.id}`}
          aria-labelledby={`${baseId}-tab-${activeItem.id}`}
          tabIndex={0}
          className="mt-4 focus:outline-none"
        >
          {activeItem.content}
        </div>
      )}
    </div>
  );
}

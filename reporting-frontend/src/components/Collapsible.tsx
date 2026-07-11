// Collapsible — قسم قابل للطيّ (disclosure) لإخفاء الكتل الثانوية افتراضيًّا (UX-PRIMITIVES Phase 0).
// تنظيم بصريّ فقط: المحتوى يبقى في DOM عند الطيّ (hidden) فلا يُفقد ولا يتغيّر أيّ منطق.
// يدعم غير مُتحكَّم (defaultOpen) أو مُتحكَّم (open + onOpenChange).
// a11y: زر أصليّ (Enter/Space يعملان تلقائيًّا) بـ aria-expanded/aria-controls،
// والمنطقة region بـ aria-labelledby. الأيقونة تدور عند الفتح.
import { useId, useState, type ReactNode } from 'react';

export function Collapsible({
  title,
  children,
  defaultOpen = false,
  open,
  onOpenChange,
  badge,
  className = '',
}: {
  title: ReactNode;
  children: ReactNode;
  defaultOpen?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  badge?: ReactNode;
  className?: string;
}) {
  const baseId = useId();
  const [internal, setInternal] = useState<boolean>(defaultOpen);
  const isOpen = open ?? internal;

  function toggle() {
    const next = !isOpen;
    if (open === undefined) setInternal(next);
    onOpenChange?.(next);
  }

  return (
    <div className={`overflow-hidden rounded-xl border border-line bg-white ${className}`}>
      <h3 className="m-0">
        <button
          type="button"
          id={`${baseId}-trigger`}
          aria-expanded={isOpen}
          aria-controls={`${baseId}-region`}
          onClick={toggle}
          className="flex w-full items-center justify-between gap-3 px-5 py-4 text-right text-sm font-semibold text-navy transition hover:bg-navy-50"
        >
          <span className="flex items-center gap-2">
            {/* أيقونة V تدور 180° عند الفتح — RTL محايد. */}
            <svg
              className={`h-4 w-4 shrink-0 text-navy-600 transition-transform ${isOpen ? 'rotate-180' : ''}`}
              viewBox="0 0 20 20"
              fill="none"
              aria-hidden="true"
            >
              <path d="M5 7.5 10 12.5 15 7.5" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            <span>{title}</span>
          </span>
          {badge != null && <span className="shrink-0">{badge}</span>}
        </button>
      </h3>
      <div
        id={`${baseId}-region`}
        role="region"
        aria-labelledby={`${baseId}-trigger`}
        hidden={!isOpen}
        className="border-t border-line px-5 py-4"
      >
        {children}
      </div>
    </div>
  );
}

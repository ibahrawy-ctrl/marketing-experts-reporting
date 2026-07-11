// StickyBar — شريط فلاتر/إجراءات لاصق للصفحات كثيفة البيانات (UX-PRIMITIVES Phase 0).
// تنظيم بصريّ فقط: غلاف مُنسَّق sticky، لا يحذف/يغيّر أيّ محتوى أو منطق أو صلاحية.
// RTL محايد (يعتمد fl-flow الطبيعيّ). يقبل position=top|bottom.
import type { ReactNode } from 'react';

export function StickyBar({
  children,
  position = 'top',
  ariaLabel,
  className = '',
}: {
  children: ReactNode;
  position?: 'top' | 'bottom';
  ariaLabel?: string;
  className?: string;
}) {
  const posClass = position === 'bottom' ? 'bottom-0' : 'top-0';
  return (
    <div
      role="region"
      aria-label={ariaLabel}
      className={`sticky ${posClass} z-10 -mx-4 border-y border-line bg-white/95 px-4 py-3 backdrop-blur supports-[backdrop-filter]:bg-white/80 ${className}`}
    >
      {children}
    </div>
  );
}

// ShowMore / PreviewList — عرض أول N عنصرًا ثم «عرض الكل» للقوائم الطويلة (UX-PRIMITIVES Phase 0).
// تنظيم بصريّ فقط: لا يحذف بيانات — كل العناصر متاحة بعد التوسيع، ولا يمسّ backend/صلاحيات.
// a11y: زر بـ aria-expanded يوضّح عدد المخفيّ؛ مناسب لسياق div/بطاقات.
// للجداول (حيث <div> غير صالح داخل <tbody>) استخدم hook «useShowMore» لتقطيع الصفوف
// و«ShowMoreButton» في تذييل/تسمية الجدول.
import { useMemo, useState, type ReactNode } from 'react';

// hook عام: يُرجِع الشريحة الظاهرة + حالة التوسيع + مبدّل + عدد المخفيّ.
export function useShowMore<T>(items: T[], previewCount = 10) {
  const [expanded, setExpanded] = useState(false);
  const visible = useMemo(
    () => (expanded ? items : items.slice(0, previewCount)),
    [expanded, items, previewCount],
  );
  const hiddenCount = Math.max(0, items.length - previewCount);
  return {
    visible,
    expanded,
    toggle: () => setExpanded((v) => !v),
    setExpanded,
    total: items.length,
    hiddenCount,
  };
}

// زر التبديل المعروض (يصلح للجداول والقوائم).
export function ShowMoreButton({
  expanded,
  onToggle,
  hiddenCount,
  controlsId,
  moreLabel,
  lessLabel = 'عرض أقل',
  className = '',
}: {
  expanded: boolean;
  onToggle: () => void;
  hiddenCount: number;
  controlsId?: string;
  moreLabel?: string;
  lessLabel?: string;
  className?: string;
}) {
  if (hiddenCount <= 0) return null;
  const more = moreLabel ?? `عرض الكل (${hiddenCount} إضافية)`;
  return (
    <button
      type="button"
      aria-expanded={expanded}
      aria-controls={controlsId}
      onClick={onToggle}
      className={`rounded-lg px-3.5 py-2 text-sm font-semibold text-navy transition hover:bg-navy-50 ${className}`}
    >
      {expanded ? lessLabel : more}
    </button>
  );
}

// غلاف جاهز لقوائم div/بطاقات: يصيّر الشريحة الظاهرة ثم زر التبديل.
export function PreviewList<T>({
  items,
  previewCount = 10,
  renderItem,
  moreLabel,
  lessLabel,
  className = '',
  listClassName = '',
}: {
  items: T[];
  previewCount?: number;
  renderItem: (item: T, index: number) => ReactNode;
  moreLabel?: string;
  lessLabel?: string;
  className?: string;
  listClassName?: string;
}) {
  const { visible, expanded, toggle, hiddenCount } = useShowMore(items, previewCount);
  return (
    <div className={className}>
      <div className={listClassName}>{visible.map((item, i) => renderItem(item, i))}</div>
      <ShowMoreButton
        expanded={expanded}
        onToggle={toggle}
        hiddenCount={hiddenCount}
        moreLabel={moreLabel}
        lessLabel={lessLabel}
        className="mt-2"
      />
    </div>
  );
}

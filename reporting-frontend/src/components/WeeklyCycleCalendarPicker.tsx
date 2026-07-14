import { useEffect, useMemo, useState } from 'react';
import { Alert, Badge, Button, Spinner } from './ui';
import { formatDate } from '../lib/format';
import { useReportingCalendar } from '../lib/useReportingCalendar';
import type { ReportingCalendarContext, ReportingCycleDto } from '../types/api';

// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.5. منتقي دورة أسبوعية مُدرِك للدور، قابل لإعادة الاستخدام
// (التقارير/KPI/تقويم التقارير). تقويم شهريّ RTL، الأسبوع يُعرض كمدى كامل السبت→الجمعة، والنقر على أيّ
// يوم يختار الأسبوع كاملًا. لا إدخال نصّي، ولا إعادة حساب لأيّ مفتاح دورة في الواجهة — كل الدورات وتواريخ
// الاستحقاق محسوبة خادميًّا بحسب الدور الأساسيّ للمستخدم عبر GET /api/reporting-calendar/my-cycles.

// أسماء الأيّام مرتّبة من السبت إلى الجمعة (بداية الأسبوع الإداريّ المعتمد).
const WEEKDAYS_AR = ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'];
const MONTHS_AR = [
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
];

// تحويل تاريخ UTC إلى مفتاح يوم YYYY-MM-DD (يطابق DateOnly الخادميّ، بلا انزياح منطقة زمنية).
function ymd(d: Date): string {
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-${String(d.getUTCDate()).padStart(2, '0')}`;
}
function parseYmd(s: string): Date {
  const [y, m, day] = s.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, day));
}
function addDaysUtc(d: Date, n: number): Date {
  return new Date(d.getTime() + n * 86_400_000);
}
// السبت في هذا الأسبوع أو قبله (بداية الأسبوع الإداريّ).
function saturdayOnOrBefore(d: Date): Date {
  const diff = (d.getUTCDay() - 6 + 7) % 7; // Saturday = 6
  return addDaysUtc(d, -diff);
}

export interface WeeklyCycleCalendarPickerProps {
  context?: ReportingCalendarContext;
  templateId?: string | null;
  value: string | null; // مفتاح الدورة المختارة
  onChange: (cycleKey: string, cycle: ReportingCycleDto) => void;
  // تحديث تلقائيّ للدورة الحالية عند أوّل تحميل حين لا توجد قيمة مختارة.
  autoSelectCurrent?: boolean;
  onRetry?: () => void;
}

export function WeeklyCycleCalendarPicker({
  context = 'Report',
  templateId = null,
  value,
  onChange,
  autoSelectCurrent = true,
}: WeeklyCycleCalendarPickerProps) {
  const query = useReportingCalendar({ context, templateId });
  const cycles = query.data?.cycles ?? [];

  // الدورة المختارة والدورة الحالية من نتائج الخادم (بلا إعادة حساب محليّ).
  const selectedCycle = useMemo(
    () => cycles.find((c) => c.cycleKey === value) ?? null,
    [cycles, value],
  );
  const currentCycle = useMemo(() => cycles.find((c) => c.isCurrent) ?? null, [cycles]);

  // شهر العرض (السنة/الشهر) — يتبع الدورة المختارة أو الحالية.
  const [view, setView] = useState<{ year: number; month: number } | null>(null);

  // اختيار الدورة الحالية تلقائيًّا عند أوّل تحميل إن لم تكن هناك قيمة.
  useEffect(() => {
    if (autoSelectCurrent && !value && currentCycle) {
      onChange(currentCycle.cycleKey, currentCycle);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentCycle, autoSelectCurrent]);

  // ضبط شهر العرض ليطابق الدورة المختارة/الحالية عند تغيّرها.
  useEffect(() => {
    const anchor = selectedCycle ?? currentCycle;
    if (anchor && view === null) {
      const d = parseYmd(anchor.cycleStart);
      setView({ year: d.getUTCFullYear(), month: d.getUTCMonth() });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedCycle, currentCycle]);

  if (query.isLoading) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-line bg-white p-4 text-sm text-ink-2">
        <Spinner /> يتم تحميل دورات التقارير…
      </div>
    );
  }
  if (query.isError || !query.data) {
    return (
      <Alert tone="alert">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span>تعذّر تحميل تقويم الدورات. لا يمكن إنشاء تقرير قبل تحميل دورة صالحة.</span>
          <Button variant="ghost" onClick={() => query.refetch()}>إعادة المحاولة</Button>
        </div>
      </Alert>
    );
  }

  const activeView = view ?? (() => {
    const anchor = selectedCycle ?? currentCycle;
    const d = anchor ? parseYmd(anchor.cycleStart) : new Date();
    return { year: d.getUTCFullYear(), month: d.getUTCMonth() };
  })();

  // شبكة الشهر: تبدأ من سبت الأسبوع المحتوي لأوّل الشهر، 6 صفوف × 7 أعمدة.
  const firstOfMonth = new Date(Date.UTC(activeView.year, activeView.month, 1));
  const gridStart = saturdayOnOrBefore(firstOfMonth);
  const weeks: Date[][] = [];
  for (let w = 0; w < 6; w++) {
    const row: Date[] = [];
    for (let d = 0; d < 7; d++) row.push(addDaysUtc(gridStart, w * 7 + d));
    weeks.push(row);
  }

  // إيجاد الدورة التي تحتوي يومًا معيّنًا (مقارنة نصّية على YYYY-MM-DD صالحة).
  function cycleForDay(dayKey: string): ReportingCycleDto | null {
    return cycles.find((c) => c.cycleStart <= dayKey && dayKey <= c.cycleEnd) ?? null;
  }

  function selectCycle(c: ReportingCycleDto) {
    if (!c.isOpen) return; // الدورات المقفلة (المستقبلية) غير قابلة للاختيار.
    onChange(c.cycleKey, c);
    const d = parseYmd(c.cycleStart);
    setView({ year: d.getUTCFullYear(), month: d.getUTCMonth() });
  }

  // تنقّل الدورة السابقة/التالية (ضمن نتائج الخادم فقط) + العودة للحالية.
  const sorted = [...cycles].sort((a, b) => a.offset - b.offset);
  const selIndex = selectedCycle ? sorted.findIndex((c) => c.cycleKey === selectedCycle.cycleKey) : -1;
  const prevCycle = selIndex > 0 ? sorted[selIndex - 1] : null;
  const nextCycle = selIndex >= 0 && selIndex < sorted.length - 1 ? sorted[selIndex + 1] : null;

  function shiftMonth(delta: number) {
    const m = activeView.month + delta;
    setView({ year: activeView.year + Math.floor(m / 12), month: ((m % 12) + 12) % 12 });
  }

  return (
    <div className="rounded-lg border border-line bg-white p-3" dir="rtl">
      {/* ترويسة: الدور + تنقّل الأشهر */}
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="text-sm">
          <span className="font-semibold text-navy">
            {MONTHS_AR[activeView.month]} {activeView.year}
          </span>
          <span className="mr-2 text-xs text-ink-3">دورك: {query.data.roleLabel}</span>
        </div>
        <div className="flex items-center gap-1">
          <Button variant="ghost" className="px-2 py-1" aria-label="الشهر السابق" onClick={() => shiftMonth(-1)}>‹</Button>
          <Button variant="ghost" className="px-2 py-1" aria-label="الشهر التالي" onClick={() => shiftMonth(1)}>›</Button>
        </div>
      </div>

      {/* شبكة التقويم */}
      <div role="grid" aria-label="تقويم دورات التقارير الأسبوعية" className="select-none">
        <div role="row" className="grid grid-cols-7 border-b border-line pb-1 text-center text-xs text-ink-3">
          {WEEKDAYS_AR.map((d) => (
            <div role="columnheader" key={d}>{d}</div>
          ))}
        </div>
        {weeks.map((row, wi) => {
          const rowCycle = cycleForDay(ymd(row[0]));
          const isSelectedRow = rowCycle != null && selectedCycle != null && rowCycle.cycleKey === selectedCycle.cycleKey;
          return (
            <div
              role="row"
              key={wi}
              className={`grid grid-cols-7 rounded-md ${isSelectedRow ? 'bg-orange/10 ring-1 ring-orange' : ''}`}
            >
              {row.map((day) => {
                const dayKey = ymd(day);
                const cycle = cycleForDay(dayKey);
                const inMonth = day.getUTCMonth() === activeView.month;
                const selectable = cycle != null && cycle.isOpen;
                const isSelected = cycle != null && selectedCycle != null && cycle.cycleKey === selectedCycle.cycleKey;
                const isToday = dayKey === query.data.today;
                return (
                  <button
                    type="button"
                    role="gridcell"
                    key={dayKey}
                    aria-selected={isSelected}
                    aria-disabled={!selectable}
                    disabled={!selectable}
                    title={cycle ? (cycle.isOpen ? cycle.cycleLabel : cycle.lockReason ?? 'مقفلة') : undefined}
                    onClick={() => cycle && selectCycle(cycle)}
                    className={[
                      'h-9 text-sm transition',
                      inMonth ? 'text-ink' : 'text-ink-3/50',
                      selectable ? 'cursor-pointer hover:bg-navy-50' : 'cursor-not-allowed opacity-40',
                      isSelected ? 'font-bold text-navy' : '',
                      isToday ? 'underline decoration-orange decoration-2 underline-offset-2' : '',
                    ].join(' ')}
                  >
                    {day.getUTCDate()}
                  </button>
                );
              })}
            </div>
          );
        })}
      </div>

      {/* شريط تنقّل الدورات */}
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={!prevCycle || !prevCycle.isOpen}
            onClick={() => prevCycle && selectCycle(prevCycle)}
          >
            الدورة السابقة
          </Button>
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={!nextCycle || !nextCycle.isOpen}
            onClick={() => nextCycle && selectCycle(nextCycle)}
          >
            الدورة التالية
          </Button>
        </div>
        {currentCycle && (
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={selectedCycle?.isCurrent ?? false}
            onClick={() => selectCycle(currentCycle)}
          >
            العودة للدورة الحالية
          </Button>
        )}
      </div>

      {/* تفاصيل الدورة المختارة */}
      {selectedCycle ? (
        <div className="mt-3 rounded-lg bg-navy-50 p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-semibold text-navy">
              الأسبوع {selectedCycle.cycleNumber} — {selectedCycle.cycleYear}
            </span>
            <CycleStatusBadge cycle={selectedCycle} />
          </div>
          <p className="mt-1 text-ink-2">
            {formatDate(selectedCycle.cycleStart)} ← {formatDate(selectedCycle.cycleEnd)}
          </p>
          <p className="mt-1 text-ink-2">
            آخر موعد لدورك ({selectedCycle.roleLabel}): <span className="font-medium text-ink">{selectedCycle.roleDueDateLabel}</span>
            {selectedCycle.isOverdue && <span className="mr-2 text-alert">— تجاوز الموعد</span>}
          </p>
          {selectedCycle.isLocked && selectedCycle.lockReason && (
            <p className="mt-1 text-xs text-alert">{selectedCycle.lockReason}</p>
          )}
          {selectedCycle.requiresReason && (
            <p className="mt-1 text-xs text-gold">دورة قديمة — قد يتطلّب التسليم المتأخّر ذكر سبب.</p>
          )}
        </div>
      ) : (
        <Alert tone="navy">اختر أسبوعًا من التقويم لتحديد الدورة.</Alert>
      )}
    </div>
  );
}

function CycleStatusBadge({ cycle }: { cycle: ReportingCycleDto }) {
  if (cycle.isCurrent) return <Badge tone="success">الدورة الحالية</Badge>;
  if (cycle.isFuture) return <Badge tone="muted">مقفلة</Badge>;
  return <Badge tone="gold">دورة ماضية</Badge>;
}

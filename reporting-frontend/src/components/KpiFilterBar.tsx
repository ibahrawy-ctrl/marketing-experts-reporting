// P1-KPI-008 — مُرشِّح KPI الموحّد: مصدر واحد للفترة والكادنس والنطاق تقوده كلّ الشاشة.
// لا يحسب هذا المكوّن حدود فترة ولا يشتقّها من توقيت المتصفّح (B-1)؛ يرسل النوع/المفتاح
// إلى الخادم ويعرض ما يعيده محلولًا بتوقيت الرياض.
import type { KpiCadence, KpiFilter, KpiPeriodResolved } from '../lib/useKpi';

// DEC-01/1 — الربع الجاري أوّلًا لأنّه الافتراضيّ؛ وبقيّة الأنواع للتنقّل التاريخيّ (ربع سابق مثلًا).
const PERIOD_TYPES: { value: string; label: string }[] = [
  { value: 'CurrentQuarter', label: 'الربع الجاري' },
  { value: 'Quarter', label: 'ربع محدَّد' },
  { value: 'LastCompletedWeek', label: 'آخر أسبوع مكتمل' },
  { value: 'Week', label: 'أسبوع محدَّد' },
  { value: 'Month', label: 'شهر' },
  { value: 'Year', label: 'سنة' },
  { value: 'Custom', label: 'مدى مخصّص' },
];

// DEC-01/2 — «تلقائي» هو الافتراضيّ: الخادم يحسم تواتر كلّ موظّف من قالبه الفعّال.
// DEC-01/3 — التحديد الصريح يفصل مسار النبض الأسبوعيّ عن مسار التقييم الربعيّ الرسميّ.
const CADENCES: { value: KpiCadence | ''; label: string }[] = [
  { value: '', label: 'تلقائي — حسب تواتر كلّ موظّف' },
  { value: 'WeeklyPulse', label: 'نبض أسبوعيّ' },
  { value: 'Quarterly', label: 'تقييم ربعيّ رسميّ' },
];

const selectClass =
  'rounded-lg border border-line bg-white px-3 py-2 text-sm text-navy focus:border-orange-500 focus:outline-none';

export function KpiFilterBar({
  filter,
  onChange,
  resolved,
}: {
  filter: KpiFilter;
  onChange: (next: KpiFilter) => void;
  resolved?: KpiPeriodResolved;
}) {
  const needsKey =
    filter.periodType !== 'CurrentQuarter' &&
    filter.periodType !== 'LastCompletedWeek' &&
    filter.periodType !== 'Custom';
  const needsRange = filter.periodType === 'Custom';

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-xl border border-line bg-white p-4">
      <label className="flex flex-col gap-1 text-xs text-ink-2">
        الفترة
        <select
          aria-label="نوع الفترة"
          className={selectClass}
          value={filter.periodType}
          onChange={(e) => onChange({ ...filter, periodType: e.target.value, periodKey: null, from: null, to: null })}
        >
          {PERIOD_TYPES.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label}
            </option>
          ))}
        </select>
      </label>

      {needsKey && (
        <label className="flex flex-col gap-1 text-xs text-ink-2">
          مفتاح الفترة
          <input
            aria-label="مفتاح الفترة"
            className={selectClass}
            placeholder="2026-W25 / 2026-06 / 2026-Q2 / 2026"
            value={filter.periodKey ?? ''}
            onChange={(e) => onChange({ ...filter, periodKey: e.target.value || null })}
          />
        </label>
      )}

      {needsRange && (
        <>
          <label className="flex flex-col gap-1 text-xs text-ink-2">
            من
            <input
              aria-label="من تاريخ"
              type="date"
              className={selectClass}
              value={filter.from ?? ''}
              onChange={(e) => onChange({ ...filter, from: e.target.value || null })}
            />
          </label>
          <label className="flex flex-col gap-1 text-xs text-ink-2">
            إلى
            <input
              aria-label="إلى تاريخ"
              type="date"
              className={selectClass}
              value={filter.to ?? ''}
              onChange={(e) => onChange({ ...filter, to: e.target.value || null })}
            />
          </label>
        </>
      )}

      {/* DEC-01/2+3: لا إلزام باختيار نوع التقييم؛ «تلقائي» يترك الحسم للخادم لكلّ موظّف،
          والتحديد الصريح يفصل المسارين بلا خلط. لا سقوط صامت في أيّ من الحالتين. */}
      <label className="flex flex-col gap-1 text-xs text-ink-2">
        نوع التقييم
        <select
          aria-label="الكادنس"
          className={selectClass}
          value={filter.cadence ?? ''}
          onChange={(e) => onChange({ ...filter, cadence: (e.target.value || null) as KpiCadence | null })}
        >
          {CADENCES.map((c) => (
            <option key={c.value || 'auto'} value={c.value}>
              {c.label}
            </option>
          ))}
        </select>
      </label>

      {resolved && (
        <p className="pb-2 text-xs text-ink-3">
          {resolved.label} · {resolved.start} ← {resolved.end} · {resolved.timezone}
          {resolved.isOpen && <span className="mr-1 font-semibold text-gold-600"> (فترة مفتوحة — لا اتّجاه رسميّ)</span>}
        </p>
      )}
    </div>
  );
}

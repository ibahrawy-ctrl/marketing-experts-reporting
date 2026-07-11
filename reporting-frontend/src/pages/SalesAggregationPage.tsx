import { Fragment, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Alert, Badge, Card, EmptyState, Field, Input, Select, StatCard } from '../components/ui';
import { Tabs } from '../components/Tabs';
import { Collapsible } from '../components/Collapsible';
import { ShowMoreButton, useShowMore } from '../components/ShowMore';
import { LoadingState, QueryError } from '../components/states';
import { useB2bBySource, useB2cCourseGrouped, useB2cNewOld } from '../lib/useSalesAggregation';
import { formatPeriod, formatPercent } from '../lib/format';
import {
  dateKey,
  monthKeyFor,
  operationalWeekKey,
  parseDateKey,
  previousPeriodKey,
  quarterOf,
  riyadhToday,
} from '../lib/dashboardPeriod';
import type {
  B2bAggregationReport,
  B2bServiceAggregateRow,
  B2bSourceBucket,
  B2bSourceReport,
  B2cCourseEmployeeRow,
  B2cCourseGroupedReport,
  B2cCourseGroupRow,
  B2cNewOldBucket,
  B2cNewOldCourseRow,
  B2cNewOldReport,
  PeriodType,
} from '../types/api';

type SalesTab = 'b2c' | 'b2b';
// عرض B2C: «حسب الدورة» (افتراضي المدير — الدورة تظهر مرّة واحدة + Drill-down) أو «حسب الموظّف» (العرض التفصيلي القديم).
type B2cView = 'course' | 'employee';
// تقسيم بيانات B2C: الكل (New+Old) / بيانات جديدة New Leads / بيانات CRM قديمة Old.
type B2cBreakdown = 'all' | 'new' | 'old';
// تقسيم بيانات B2B حسب المصدر: الكل (New+Data) / عملاء جدد New Leads / سحب البيانات Data Scraping.
type B2bSource = 'all' | 'new' | 'data';

// أنواع الفترات المدعومة في التجميع: يومي (أساس تخزين تقارير المبيعات) + تجميع أوسع.
// عند اختيار أسبوعي/شهري/ربع سنوي يجمع الخادمُ التقاريرَ اليومية الواقعة داخل نطاق الفترة.
const PERIOD_TYPES: PeriodType[] = ['Daily', 'Weekly', 'Monthly', 'Quarterly'];
const PERIOD_TYPE_LABEL: Record<string, string> = {
  Daily: 'يومي',
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
};

// عرض رقم عشري مختصر (يزيل الأصفار الزائدة): 4000 ⇒ «4٬000»، 0.6 ⇒ «0٫6».
function num(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return Number(value).toLocaleString('ar-EG', { maximumFractionDigits: 2 });
}

export default function SalesAggregationPage() {
  const today = useMemo(() => riyadhToday(), []);
  const [tab, setTab] = useState<SalesTab>('b2c');
  const [b2cView, setB2cView] = useState<B2cView>('course');
  const [b2cBreakdown, setB2cBreakdown] = useState<B2cBreakdown>('all');
  const [b2bSource, setB2bSource] = useState<B2bSource>('all');
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');

  // قيم منتقيات الفترة الخام (لا يرى المستخدم مفتاح الفترة النهائي — يُولَّد داخليًّا).
  const [dailyDate, setDailyDate] = useState<string>(() => dateKey(today)); // YYYY-MM-DD
  const [weeklyDate, setWeeklyDate] = useState<string>(() => dateKey(today)); // أي يوم داخل الأسبوع
  const [monthValue, setMonthValue] = useState<string>(() => monthKeyFor(today)); // YYYY-MM
  const [quarterYear, setQuarterYear] = useState<number>(() => today.getUTCFullYear());
  const [quarterNum, setQuarterNum] = useState<number>(() => quarterOf(today));

  // توليد مفتاح الفترة (PeriodKey) داخليًّا حسب النوع — دون أن يكتبه المستخدم يدويًّا.
  const periodKey = useMemo(() => {
    switch (periodType) {
      case 'Daily':
        return dailyDate || undefined;
      case 'Weekly': {
        const d = parseDateKey(weeklyDate);
        return d ? operationalWeekKey(d) : undefined;
      }
      case 'Monthly':
        return monthValue || undefined;
      case 'Quarterly':
        return `${quarterYear}-Q${quarterNum}`;
      default:
        return undefined;
    }
  }, [periodType, dailyDate, weeklyDate, monthValue, quarterYear, quarterNum]);

  const filter = useMemo(
    () => ({ periodType, periodKey }),
    [periodType, periodKey],
  );

  // فلتر الفترة السابقة (للمقارنة في لوحة القيادة) — يعيد استخدام نفس نقطة النهاية بمفتاح مختلف.
  const prevFilter = useMemo(
    () => ({ periodType, periodKey: previousPeriodKey(periodType, periodKey) }),
    [periodType, periodKey],
  );

  const b2cGrouped = useB2cCourseGrouped(filter);
  const b2cGroupedPrev = useB2cCourseGrouped(prevFilter);
  const b2cNewOld = useB2cNewOld(filter);
  const b2b = useB2bBySource(filter);
  const b2bPrev = useB2bBySource(prevFilter);
  // محتوى تبويب B2C: مفتاح العرض الفرعي (حسب الدورة/الموظّف) دائم الظهور، ثم حالات التحميل/الخطأ ثم العرض.
  // عرضا B2C (حسب الدورة/الموظّف) يشتقّان من تجميع الدورة نفسه، لذا يعتمدان على حالة b2cGrouped.
  const b2cPanel = (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs font-semibold text-ink-2">طريقة العرض:</span>
        <button
          type="button"
          onClick={() => setB2cView('course')}
          className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
            b2cView === 'course' ? 'bg-navy text-white' : 'bg-offwhite text-ink-2 hover:bg-line'
          }`}
        >
          تجميع حسب الدورة
        </button>
        <button
          type="button"
          onClick={() => setB2cView('employee')}
          className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
            b2cView === 'employee' ? 'bg-navy text-white' : 'bg-offwhite text-ink-2 hover:bg-line'
          }`}
        >
          تفصيل حسب الموظّف
        </button>
      </div>

      {b2cGrouped.isLoading ? (
        <LoadingState label="يتم تحميل التجميع…" />
      ) : b2cGrouped.isError ? (
        <QueryError
          onRetry={() => b2cGrouped.refetch()}
          description="حدث خطأ أثناء جلب بيانات التجميع. أعد المحاولة."
        />
      ) : b2cView === 'course' ? (
        <B2cCourseGroupedView
          data={b2cGrouped.data}
          prev={b2cGroupedPrev.data}
          newOld={b2cNewOld.data}
          breakdown={b2cBreakdown}
          onBreakdownChange={setB2cBreakdown}
        />
      ) : (
        <B2cEmployeeView
          data={b2cGrouped.data}
          breakdown={b2cBreakdown}
          onBreakdownChange={setB2cBreakdown}
        />
      )}
    </div>
  );

  // محتوى تبويب B2B: حالات التحميل/الخطأ ثم عرض التجميع حسب الخدمة/المصدر.
  const b2bPanel = b2b.isLoading ? (
    <LoadingState label="يتم تحميل التجميع…" />
  ) : b2b.isError ? (
    <QueryError
      onRetry={() => b2b.refetch()}
      description="حدث خطأ أثناء جلب بيانات التجميع. أعد المحاولة."
    />
  ) : (
    <B2bView data={b2b.data} prev={b2bPrev.data} source={b2bSource} onSourceChange={setB2bSource} />
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">تجميع المبيعات</h1>
        <p className="mt-1 text-sm text-ink-2">
          عرض تجميعي لمبيعات فريقك (B2C حسب الدورة، B2B حسب الخدمة) من التقارير المُهيكَلة.
          كلّ صفّ يجمع مدخلات موظّف واحد لفترة واحدة، مع المؤشرات المحسوبة تلقائيًّا.
        </p>
      </div>

      <Alert tone="navy">
        هذا العرض قراءة فقط ويلتزم بنطاق صلاحيتك: لا يظهر فيه إلا موظّفو فريقك/إدارتك.
        الأرقام مأخوذة مباشرة من مدخلات الموظّفين في تقاريرهم المعتمَدة — لا حساب أو صرف لأي مستحقات.
      </Alert>

      {/* الفلاتر المشتركة بين تبويبَي B2C و B2B (نوع الفترة + منتقي الفترة حسب النوع). */}
      <Card>
        <div className="grid gap-3 md:grid-cols-3">
          <Field label="نوع الفترة">
            <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
              {PERIOD_TYPES.map((p) => (
                <option key={p} value={p}>{PERIOD_TYPE_LABEL[p] ?? p}</option>
              ))}
            </Select>
          </Field>

          {periodType === 'Daily' && (
            <Field label="اليوم">
              <Input type="date" value={dailyDate} onChange={(e) => setDailyDate(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Weekly' && (
            <Field label="الأسبوع" help={periodKey ? `الأسبوع المولّد: ${periodKey}` : 'اختر أي يوم داخل الأسبوع'}>
              <Input type="date" value={weeklyDate} onChange={(e) => setWeeklyDate(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Monthly' && (
            <Field label="الشهر">
              <Input type="month" value={monthValue} onChange={(e) => setMonthValue(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Quarterly' && (
            <>
              <Field label="السنة">
                <Input
                  type="number"
                  value={String(quarterYear)}
                  onChange={(e) => setQuarterYear(Number(e.target.value) || quarterYear)}
                  min={2000}
                  max={3000}
                  dir="ltr"
                />
              </Field>
              <Field label="الربع">
                <Select value={String(quarterNum)} onChange={(e) => setQuarterNum(Number(e.target.value))}>
                  <option value="1">الربع 1</option>
                  <option value="2">الربع 2</option>
                  <option value="3">الربع 3</option>
                  <option value="4">الربع 4</option>
                </Select>
              </Field>
            </>
          )}
        </div>
      </Card>

      {/* تبويبا B2C / B2B عبر مكوّن Tabs الموحّد (UX-PRIMITIVES). الافتراضي = B2C حسب الدورة. */}
      <Tabs
        ariaLabel="نوع تجميع المبيعات"
        value={tab}
        onChange={(id) => setTab(id as SalesTab)}
        items={[
          { id: 'b2c', label: 'مبيعات B2C حسب الدورة', content: b2cPanel },
          { id: 'b2b', label: 'مبيعات B2B حسب الخدمة', content: b2bPanel },
        ]}
      />
    </div>
  );
}

function SummaryCards({
  rowCount,
  periodKey,
  cards,
}: {
  rowCount: number;
  periodKey: string | null | undefined;
  cards: { label: string; value: string; tone?: 'navy' | 'alert' }[];
}) {
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
        <Badge tone="navy">{formatPeriod(periodKey)}</Badge>
        <span>عدد الصفوف: {rowCount.toLocaleString('ar-EG')}</span>
      </div>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {cards.map((c) => (
          <StatCard key={c.label} label={c.label} value={c.value} tone={c.tone} />
        ))}
      </div>
    </div>
  );
}

// ===== أدوات لوحة القيادة التنفيذية B2C (Frontend-only، رسوم SVG/CSS خفيفة بلا مكتبات) =====

// نسبة مئوية آمنة (تتفادى القسمة على صفر).
function ratio(numer: number, denom: number): number {
  return denom > 0 ? (numer / denom) * 100 : 0;
}

// تنسيق نسبة مئوية مختصرة للعرض داخل الرسوم.
function pctText(value: number): string {
  return `${value.toLocaleString('ar-EG', { maximumFractionDigits: 1 })}٪`;
}

// لوحة ألوان ثابتة للدورات في الرسوم (تدور عند تجاوز العدد).
const CHART_COLORS = [
  '#F57C00', '#1A2B4A', '#2E7D32', '#00838F', '#6A1B9A',
  '#C62828', '#455A64', '#AD1457', '#0277BD', '#EF6C00',
];
function colorAt(index: number): string {
  return CHART_COLORS[index % CHART_COLORS.length];
}

// إجماليات الدورات (تبقى صحيحة لأن إجمالي كل دورة = مجموع مساهمات موظّفيها).
function sumCourses(courses: B2cCourseGroupRow[]) {
  return courses.reduce(
    (a, c) => ({
      workHours: a.workHours + c.workHours,
      leads: a.leads + c.leads,
      contacted: a.contacted + c.contacted,
      qualified: a.qualified + c.qualified,
      sales: a.sales + c.sales,
      revenue: a.revenue + c.revenue,
    }),
    { workHours: 0, leads: 0, contacted: 0, qualified: 0, sales: 0, revenue: 0 },
  );
}

// اشتقاق تجميع الدورات لمصدر واحد (New أو Old) من نفس بيانات by-course،
// بحيث تتطابق الإجماليات تمامًا مع «تفصيل حسب الموظّف» (المصدر نفسه = دلو كل موظّف).
// كل موظّف يأخذ دلوه المختار، وتُعاد حوسبة إجماليات الدورة ونِسبها.
// الموظّفون/الدورات الفارغون تمامًا للمصدر المختار يُستبعَدون.
function coursesForSource(courses: B2cCourseGroupRow[], source: 'new' | 'old'): B2cCourseGroupRow[] {
  const out: B2cCourseGroupRow[] = [];
  for (const c of courses) {
    const employees: B2cCourseEmployeeRow[] = [];
    for (const e of c.employees) {
      const b = source === 'new' ? e.new : e.old;
      if (bucketEmpty(b)) continue;
      employees.push({
        employeeId: e.employeeId,
        employeeName: e.employeeName,
        teamId: e.teamId,
        departmentId: e.departmentId,
        workHours: b.workHours,
        leads: b.leads,
        contacted: b.contacted,
        qualified: b.qualified,
        followUps: b.followUps,
        sales: b.sales,
        revenue: b.revenue,
        lost: b.lost,
        conversionRate: b.conversionRate,
        new: e.new,
        old: e.old,
      });
    }
    if (employees.length === 0) continue;
    const t = employees.reduce(
      (a, e) => ({
        workHours: a.workHours + e.workHours,
        leads: a.leads + e.leads,
        contacted: a.contacted + e.contacted,
        qualified: a.qualified + e.qualified,
        followUps: a.followUps + e.followUps,
        sales: a.sales + e.sales,
        revenue: a.revenue + e.revenue,
        lost: a.lost + e.lost,
      }),
      { workHours: 0, leads: 0, contacted: 0, qualified: 0, followUps: 0, sales: 0, revenue: 0, lost: 0 },
    );
    out.push({
      course: c.course,
      workHours: t.workHours,
      leads: t.leads,
      contacted: t.contacted,
      qualified: t.qualified,
      followUps: t.followUps,
      sales: t.sales,
      revenue: t.revenue,
      lost: t.lost,
      conversionRate: ratio(t.sales, t.leads),
      qualificationRate: ratio(t.qualified, t.contacted),
      contactRate: ratio(t.contacted, t.leads),
      revenuePerHour: t.workHours > 0 ? t.revenue / t.workHours : 0,
      salesPerHour: t.workHours > 0 ? t.sales / t.workHours : 0,
      lostRate: ratio(t.lost, t.leads),
      employeeCount: employees.length,
      employees,
    });
  }
  return out.sort((a, b) => b.revenue - a.revenue || a.course.localeCompare(b.course));
}

// شارة التغيّر مقابل الفترة السابقة (↑ أخضر / ↓ أحمر / — رمادي).
function DeltaBadge({
  current,
  previous,
  kind = 'number',
}: {
  current: number;
  previous: number | undefined;
  kind?: 'number' | 'percentPoints';
}) {
  if (previous === undefined) {
    return <span className="text-xs text-ink-2">لا توجد فترة سابقة</span>;
  }
  const diff = current - previous;
  const rel = previous !== 0 ? (diff / Math.abs(previous)) * 100 : current > 0 ? 100 : 0;
  const up = diff > 0.0001;
  const down = diff < -0.0001;
  const tone = up ? 'text-green-700' : down ? 'text-red-700' : 'text-ink-2';
  const arrow = up ? '▲' : down ? '▼' : '—';
  const detail =
    kind === 'percentPoints'
      ? `${diff >= 0 ? '+' : ''}${diff.toLocaleString('ar-EG', { maximumFractionDigits: 1 })} نقطة`
      : `${diff >= 0 ? '+' : ''}${rel.toLocaleString('ar-EG', { maximumFractionDigits: 1 })}٪`;
  return (
    <span className={`text-xs font-semibold ${tone}`}>
      {arrow} {detail}
    </span>
  );
}

// رسم دائري (Pie) SVG لتوزيع قيمة عبر الدورات.
function PieChart({ slices }: { slices: { label: string; value: number }[] }) {
  const total = slices.reduce((a, s) => a + s.value, 0);
  const size = 180;
  const r = 80;
  const cx = size / 2;
  const cy = size / 2;
  if (total <= 0) {
    return <p className="py-8 text-center text-sm text-ink-2">لا توجد قيم للعرض.</p>;
  }
  let acc = 0;
  const arcs = slices
    .filter((s) => s.value > 0)
    .map((s, i) => {
      const start = (acc / total) * 2 * Math.PI;
      acc += s.value;
      const end = (acc / total) * 2 * Math.PI;
      const large = end - start > Math.PI ? 1 : 0;
      const x1 = cx + r * Math.sin(start);
      const y1 = cy - r * Math.cos(start);
      const x2 = cx + r * Math.sin(end);
      const y2 = cy - r * Math.cos(end);
      const d = `M ${cx} ${cy} L ${x1} ${y1} A ${r} ${r} 0 ${large} 1 ${x2} ${y2} Z`;
      return { d, color: colorAt(i), label: s.label, value: s.value, share: ratio(s.value, total) };
    });
  return (
    <div className="flex flex-wrap items-center gap-4">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="shrink-0">
        {arcs.map((a) => (
          <path key={a.label} d={a.d} fill={a.color} stroke="#fff" strokeWidth={1} />
        ))}
      </svg>
      <ul className="min-w-[160px] flex-1 space-y-1 text-xs">
        {arcs.map((a) => (
          <li key={a.label} className="flex items-center gap-2">
            <span className="inline-block h-3 w-3 shrink-0 rounded-sm" style={{ backgroundColor: a.color }} />
            <span className="flex-1 truncate text-ink" title={a.label}>{a.label}</span>
            <span className="text-ink-2">{pctText(a.share)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

// مخطط أعمدة أفقيّة CSS (مناسب لـ RTL) — قيمة لكل دورة.
function BarChart({
  rows,
  format,
}: {
  rows: { label: string; value: number }[];
  format: (v: number) => string;
}) {
  const max = rows.reduce((m, r) => Math.max(m, r.value), 0);
  if (rows.length === 0 || max <= 0) {
    return <p className="py-8 text-center text-sm text-ink-2">لا توجد قيم للعرض.</p>;
  }
  return (
    <ul className="space-y-2">
      {rows.map((r, i) => (
        <li key={r.label} className="text-xs">
          <div className="mb-1 flex items-center justify-between gap-2">
            <span className="truncate text-ink" title={r.label}>{r.label}</span>
            <span className="shrink-0 font-semibold text-ink-2">{format(r.value)}</span>
          </div>
          <div className="h-2.5 w-full overflow-hidden rounded-full bg-offwhite">
            <div
              className="h-full rounded-full"
              style={{ width: `${ratio(r.value, max)}%`, backgroundColor: colorAt(i) }}
            />
          </div>
        </li>
      ))}
    </ul>
  );
}

// قمع التحويل: Lead → Contacted → Qualified → Sales (أشرطة متناقصة).
function Funnel({ leads, contacted, qualified, sales }: { leads: number; contacted: number; qualified: number; sales: number }) {
  const stages = [
    { label: 'Leads', value: leads, color: '#1A2B4A' },
    { label: 'Contacted', value: contacted, color: '#0277BD' },
    { label: 'Qualified', value: qualified, color: '#00838F' },
    { label: 'Sales', value: sales, color: '#2E7D32' },
  ];
  const max = Math.max(leads, 1);
  return (
    <ul className="space-y-2">
      {stages.map((s, i) => {
        const prev = i === 0 ? s.value : stages[i - 1].value;
        const stepRate = i === 0 ? 100 : ratio(s.value, prev);
        return (
          <li key={s.label}>
            <div className="mb-1 flex items-center justify-between text-xs">
              <span className="font-semibold text-ink">{s.label}</span>
              <span className="text-ink-2">
                {num(s.value)}
                {i > 0 && <span className="mr-2 text-ink-2">({pctText(stepRate)})</span>}
              </span>
            </div>
            <div className="h-6 w-full overflow-hidden rounded bg-offwhite">
              <div
                className="flex h-full items-center justify-start rounded pr-2 text-[10px] font-semibold text-white"
                style={{ width: `${Math.max(ratio(s.value, max), 3)}%`, backgroundColor: s.color }}
              />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

// خلية حرارة نسبة تحويل: كثافة اللون تتناسب مع القيمة (0 باهت → مرتفع مشبع).
function HeatCell({ value }: { value: number }) {
  const clamped = Math.max(0, Math.min(100, value));
  // كثافة الخلفية بين 0.08 و0.9 حسب النسبة.
  const alpha = 0.08 + (clamped / 100) * 0.82;
  const strong = clamped >= 45;
  return (
    <td className="px-3 py-2 text-center" style={{ backgroundColor: `rgba(46, 125, 50, ${alpha})` }}>
      <span className={strong ? 'font-semibold text-white' : 'text-ink'}>{pctText(value)}</span>
    </td>
  );
}

// قائمة أعلى/أسوأ الدورات ببطاقة صغيرة.
function RankList({
  rows,
  format,
}: {
  rows: { label: string; value: number }[];
  format: (v: number) => string;
}) {
  if (rows.length === 0) {
    return <p className="py-4 text-center text-xs text-ink-2">لا توجد بيانات.</p>;
  }
  return (
    <ol className="space-y-1.5 text-sm">
      {rows.map((r, i) => (
        <li key={r.label} className="flex items-center gap-2">
          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-navy text-[10px] font-bold text-white">
            {(i + 1).toLocaleString('ar-EG')}
          </span>
          <span className="flex-1 truncate text-ink" title={r.label}>{r.label}</span>
          <span className="shrink-0 font-semibold text-ink-2">{format(r.value)}</span>
        </li>
      ))}
    </ol>
  );
}

// عنوان قسم داخل اللوحة.
function Panel({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <Card>
      <h3 className="text-sm font-bold text-navy">{title}</h3>
      {hint && <p className="mt-0.5 text-xs text-ink-2">{hint}</p>}
      <div className="mt-3">{children}</div>
    </Card>
  );
}

// اللوحة التنفيذية الكاملة (تظهر أعلى الجدول).
function B2cExecutiveDashboard({
  courses,
  prev,
  periodKey,
}: {
  courses: B2cCourseGroupRow[];
  prev?: B2cCourseGroupedReport;
  periodKey: string | null | undefined;
}) {
  const t = sumCourses(courses);
  const avgConversion = ratio(t.sales, t.leads);
  const prevCourses = prev?.courses ?? [];
  const hasPrev = prevCourses.length > 0;
  const pt = hasPrev ? sumCourses(prevCourses) : undefined;
  const prevConversion = pt ? ratio(pt.sales, pt.leads) : undefined;

  // توزيع المبيعات حسب الدورة (Pie).
  const salesByCourse = courses.map((c) => ({ label: c.course, value: c.sales }));
  // الإيراد لكل دورة + عدد المبيعات لكل دورة (أعمدة).
  const revenueByCourse = [...courses]
    .sort((a, b) => b.revenue - a.revenue)
    .map((c) => ({ label: c.course, value: c.revenue }));
  const salesCountByCourse = [...courses]
    .sort((a, b) => b.sales - a.sales)
    .map((c) => ({ label: c.course, value: c.sales }));

  // أعلى 5 دورات (حسب الإيراد/المبيعات/التحويل) وأسوأها.
  const top5Revenue = [...courses].sort((a, b) => b.revenue - a.revenue).slice(0, 5)
    .map((c) => ({ label: c.course, value: c.revenue }));
  const top5Sales = [...courses].sort((a, b) => b.sales - a.sales).slice(0, 5)
    .map((c) => ({ label: c.course, value: c.sales }));
  const top5Conversion = [...courses].sort((a, b) => b.conversionRate - a.conversionRate).slice(0, 5)
    .map((c) => ({ label: c.course, value: c.conversionRate }));
  const worstCourses = [...courses]
    .sort((a, b) => a.conversionRate - b.conversionRate || a.revenue - b.revenue)
    .slice(0, 5)
    .map((c) => ({ label: c.course, value: c.conversionRate }));

  // أعلى الموظّفين ضمن الفترة (تجميع مساهمات الموظّف عبر كل الدورات).
  const empMap = new Map<string, { name: string; sales: number; revenue: number }>();
  for (const c of courses) {
    for (const e of c.employees) {
      const cur = empMap.get(e.employeeId) ?? { name: e.employeeName, sales: 0, revenue: 0 };
      cur.sales += e.sales;
      cur.revenue += e.revenue;
      empMap.set(e.employeeId, cur);
    }
  }
  const topEmployees = [...empMap.values()]
    .sort((a, b) => b.revenue - a.revenue || b.sales - a.sales)
    .slice(0, 5)
    .map((e) => ({ label: e.name, value: e.revenue }));

  return (
    <div className="space-y-4">
      {/* بطاقات المؤشّرات الرئيسية */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
        <StatCard label="إجمالي Leads" value={num(t.leads)} />
        <StatCard label="إجمالي Contacted" value={num(t.contacted)} />
        <StatCard label="إجمالي Qualified" value={num(t.qualified)} />
        <StatCard label="إجمالي Sales" value={num(t.sales)} tone="navy" />
        <StatCard label="إجمالي Revenue" value={num(t.revenue)} tone="navy" />
        <StatCard label="متوسّط نسبة التحويل" value={pctText(avgConversion)} tone="navy" />
      </div>

      {/* الرسوم البيانية وتحليل التحويل (قابل للطيّ — مطويّ افتراضيًّا لتقصير العرض) */}
      <Collapsible title="الرسوم البيانية وتحليل التحويل">
        <div className="space-y-4">
          {/* مقارنة الفترة السابقة */}
          <Card>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-sm font-bold text-navy">مقارنة مع الفترة السابقة</h3>
              <Badge tone="navy">
                {hasPrev ? `مقابل ${formatPeriod(prev?.periodKey)}` : 'لا توجد بيانات فترة سابقة'}
              </Badge>
            </div>
            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div className="rounded-lg border border-line p-3">
                <div className="text-xs text-ink-2">Revenue</div>
                <div className="mt-0.5 text-lg font-bold text-ink">{num(t.revenue)}</div>
                <DeltaBadge current={t.revenue} previous={pt?.revenue} />
              </div>
              <div className="rounded-lg border border-line p-3">
                <div className="text-xs text-ink-2">Sales</div>
                <div className="mt-0.5 text-lg font-bold text-ink">{num(t.sales)}</div>
                <DeltaBadge current={t.sales} previous={pt?.sales} />
              </div>
              <div className="rounded-lg border border-line p-3">
                <div className="text-xs text-ink-2">نسبة التحويل</div>
                <div className="mt-0.5 text-lg font-bold text-ink">{pctText(avgConversion)}</div>
                <DeltaBadge current={avgConversion} previous={prevConversion} kind="percentPoints" />
              </div>
            </div>
          </Card>

          {/* القمع + التوزيع الدائري */}
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Panel title="قمع التحويل" hint={`للفترة ${formatPeriod(periodKey)} — Lead ← Contacted ← Qualified ← Sales`}>
              <Funnel leads={t.leads} contacted={t.contacted} qualified={t.qualified} sales={t.sales} />
            </Panel>
            <Panel title="توزيع المبيعات حسب الدورة">
              <PieChart slices={salesByCourse} />
            </Panel>
          </div>

          {/* أعمدة الإيراد + المبيعات لكل دورة */}
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Panel title="الإيراد لكل دورة">
              <BarChart rows={revenueByCourse} format={num} />
            </Panel>
            <Panel title="عدد المبيعات لكل دورة">
              <BarChart rows={salesCountByCourse} format={num} />
            </Panel>
          </div>
        </div>
      </Collapsible>

      {/* أعلى الدورات والموظّفون (قابل للطيّ) */}
      <Collapsible title="أعلى الدورات والموظّفون">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
          <Panel title="أعلى 5 دورات — الإيراد">
            <RankList rows={top5Revenue} format={num} />
          </Panel>
          <Panel title="أعلى 5 دورات — المبيعات">
            <RankList rows={top5Sales} format={num} />
          </Panel>
          <Panel title="أعلى 5 دورات — نسبة التحويل">
            <RankList rows={top5Conversion} format={pctText} />
          </Panel>
          <Panel title="أضعف الدورات — نسبة التحويل">
            <RankList rows={worstCourses} format={pctText} />
          </Panel>
          <Panel title="أعلى الموظّفين — الإيراد" hint="ضمن الفترة المختارة">
            <RankList rows={topEmployees} format={num} />
          </Panel>
        </div>
      </Collapsible>

      {/* جدول حرارة التحويل لكل دورة (قابل للطيّ) */}
      <Collapsible title="خريطة حرارة التحويل حسب الدورة" badge="كلّما اخضرّت الخلية زادت النسبة">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2 font-semibold">Course</th>
                <th className="px-3 py-2 text-center font-semibold">Lead ← Contact</th>
                <th className="px-3 py-2 text-center font-semibold">Contact ← Qualified</th>
                <th className="px-3 py-2 text-center font-semibold">Qualified ← Sale</th>
              </tr>
            </thead>
            <tbody>
              {courses.map((c) => (
                <tr key={c.course} className="border-b border-line last:border-0">
                  <td className="px-3 py-2 font-medium text-navy">{c.course}</td>
                  <HeatCell value={ratio(c.contacted, c.leads)} />
                  <HeatCell value={ratio(c.qualified, c.contacted)} />
                  <HeatCell value={ratio(c.sales, c.qualified)} />
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Collapsible>
    </div>
  );
}

// أزرار تبديل تقسيم البيانات (الكل / بيانات جديدة New / بيانات CRM قديمة Old).
function BreakdownTabs({ value, onChange }: { value: B2cBreakdown; onChange: (v: B2cBreakdown) => void }) {
  const items: { key: B2cBreakdown; label: string }[] = [
    { key: 'all', label: 'الكل (جديد + قديم)' },
    { key: 'new', label: 'بيانات جديدة New Leads' },
    { key: 'old', label: 'بيانات CRM قديمة Old' },
  ];
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-xs font-semibold text-ink-2">تقسيم البيانات:</span>
      {items.map((it) => (
        <button
          key={it.key}
          type="button"
          onClick={() => onChange(it.key)}
          className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
            value === it.key ? 'bg-navy text-white' : 'bg-offwhite text-ink-2 hover:bg-line'
          }`}
        >
          {it.label}
        </button>
      ))}
    </div>
  );
}

// بطاقة مقارنة New مقابل Old لمؤشّر واحد (Revenue/Sales/WorkHours/Conversion).
function CompareCell({ label, newValue, oldValue, format }: {
  label: string;
  newValue: number;
  oldValue: number;
  format: (v: number) => string;
}) {
  return (
    <div className="rounded-lg border border-line p-3">
      <div className="text-xs text-ink-2">{label}</div>
      <div className="mt-2 grid grid-cols-2 gap-2">
        <div>
          <div className="text-[10px] font-semibold text-orange-700">جديد New</div>
          <div className="text-base font-bold text-ink">{format(newValue)}</div>
        </div>
        <div>
          <div className="text-[10px] font-semibold text-navy">قديم Old</div>
          <div className="text-base font-bold text-ink">{format(oldValue)}</div>
        </div>
      </div>
    </div>
  );
}

// قسم مقارنة New/Old الإجمالية: Revenue، Sales، WorkHours، نسبة التحويل/الاسترجاع.
function NewOldComparison({ report }: { report: B2cNewOldReport }) {
  const n = report.newTotals;
  const o = report.oldTotals;
  return (
    <div>
      <p className="text-xs text-ink-2">
        «نسبة التحويل» للبيانات الجديدة = المبيعات ÷ New Leads، وللبيانات القديمة = معدّل الاسترجاع (المبيعات ÷ Old Leads Worked).
      </p>
      <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <CompareCell label="Revenue" newValue={n.revenue} oldValue={o.revenue} format={num} />
        <CompareCell label="Sales" newValue={n.sales} oldValue={o.sales} format={num} />
        <CompareCell label="WorkHours" newValue={n.workHours} oldValue={o.workHours} format={num} />
        <CompareCell
          label="نسبة التحويل / الاسترجاع"
          newValue={n.conversionRate}
          oldValue={o.conversionRate}
          format={pctText}
        />
      </div>
    </div>
  );
}

// جدول تجميع الدورات لمصدر واحد (New/Old) مع Drill-down لتفصيل الموظّفين — كل الأرقام للمصدر المختار.
function SourceCourseTable({ courses, leadsLabel }: { courses: B2cCourseGroupRow[]; leadsLabel: string }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const toggle = (course: string) => setExpanded((prev) => ({ ...prev, [course]: !prev[course] }));
  const showMore = useShowMore(courses, 8);

  return (
    <table className="w-full min-w-[1100px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">Course</th>
          <th className="px-3 py-2.5 font-semibold">الموظّفون</th>
          <th className="px-3 py-2.5 font-semibold">WorkHours</th>
          <th className="px-3 py-2.5 font-semibold">{leadsLabel}</th>
          <th className="px-3 py-2.5 font-semibold">Contacted</th>
          <th className="px-3 py-2.5 font-semibold">Qualified</th>
          <th className="px-3 py-2.5 font-semibold">Sales</th>
          <th className="px-3 py-2.5 font-semibold">Revenue</th>
          <th className="px-3 py-2.5 font-semibold">نسبة التحويل</th>
          <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
        </tr>
      </thead>
      <tbody>
        {showMore.visible.map((c) => {
          const isOpen = !!expanded[c.course];
          return (
            <Fragment key={c.course}>
              <tr
                className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
                onClick={() => toggle(c.course)}
              >
                <td className="px-3 py-2.5 font-medium text-navy">
                  <span className="ml-1 inline-block w-3 text-ink-2">{isOpen ? '▾' : '▸'}</span>
                  {c.course}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{c.employeeCount.toLocaleString('ar-EG')}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.workHours)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.leads)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.contacted)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.qualified)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.sales)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.revenue)}</td>
                <td className="px-3 py-2.5 text-ink-2">{formatPercent(c.conversionRate)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.revenuePerHour)}</td>
              </tr>
              {isOpen && (
                <tr className="border-b border-line bg-offwhite/40 last:border-0">
                  <td colSpan={10} className="px-3 py-3">
                    {c.employees.length === 0 ? (
                      <p className="py-2 text-center text-xs text-ink-2">لا يوجد موظّفون لهذه الدورة.</p>
                    ) : (
                      <table className="w-full text-right text-sm">
                        <thead className="border-b border-line text-[11px] text-ink-2">
                          <tr>
                            <th className="px-3 py-1 font-semibold">الموظف</th>
                            <th className="px-3 py-1 font-semibold">WorkHours</th>
                            <th className="px-3 py-1 font-semibold">{leadsLabel}</th>
                            <th className="px-3 py-1 font-semibold">Contacted</th>
                            <th className="px-3 py-1 font-semibold">Qualified</th>
                            <th className="px-3 py-1 font-semibold">Sales</th>
                            <th className="px-3 py-1 font-semibold">Revenue</th>
                            <th className="px-3 py-1 font-semibold">نسبة التحويل</th>
                          </tr>
                        </thead>
                        <tbody>
                          {c.employees.map((e, i) => (
                            <tr
                              key={`${c.course}-${e.employeeId}-${i}`}
                              className="border-b border-line/60 last:border-0"
                            >
                              <td className="px-3 py-1.5 font-medium text-navy">↳ {e.employeeName}</td>
                              <td className="px-3 py-1.5 text-ink-2">{num(e.workHours)}</td>
                              <td className="px-3 py-1.5 text-ink-2">{num(e.leads)}</td>
                              <td className="px-3 py-1.5 text-ink-2">{num(e.contacted)}</td>
                              <td className="px-3 py-1.5 text-ink-2">{num(e.qualified)}</td>
                              <td className="px-3 py-1.5 font-medium text-ink">{num(e.sales)}</td>
                              <td className="px-3 py-1.5 font-medium text-ink">{num(e.revenue)}</td>
                              <td className="px-3 py-1.5 text-ink-2">{formatPercent(e.conversionRate)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </td>
                </tr>
              )}
            </Fragment>
          );
        })}
      </tbody>
      {showMore.hiddenCount > 0 && (
        <tfoot>
          <tr>
            <td colSpan={10} className="px-3 py-2 text-center">
              <ShowMoreButton
                expanded={showMore.expanded}
                onToggle={showMore.toggle}
                hiddenCount={showMore.hiddenCount}
              />
            </td>
          </tr>
        </tfoot>
      )}
    </table>
  );
}

// جدول «الكل»: لكل دورة الإجمالي (جديد + قديم) ثم عمودا New و Old جنبًا إلى جنب.
function AllBreakdownCourseTable({ courses }: { courses: B2cNewOldCourseRow[] }) {
  const rows = [...courses]
    .map((c) => ({
      course: c.course,
      n: c.new,
      o: c.old,
      totalSales: c.new.sales + c.old.sales,
      totalRevenue: c.new.revenue + c.old.revenue,
    }))
    .sort((a, b) => b.totalRevenue - a.totalRevenue || a.course.localeCompare(b.course));
  const showMore = useShowMore(rows, 8);
  if (rows.length === 0) {
    return <p className="py-6 text-center text-sm text-ink-2">لا توجد بيانات للفترة المختارة.</p>;
  }
  return (
    <table className="w-full min-w-[1000px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold" rowSpan={2}>Course</th>
          <th className="px-3 py-2 text-center font-semibold" colSpan={2}>الإجمالي</th>
          <th className="border-r border-line px-3 py-2 text-center font-semibold text-orange-700" colSpan={3}>جديد New</th>
          <th className="border-r border-line px-3 py-2 text-center font-semibold text-navy" colSpan={3}>قديم Old</th>
        </tr>
        <tr>
          <th className="px-3 py-2 font-semibold">Sales</th>
          <th className="px-3 py-2 font-semibold">Revenue</th>
          <th className="border-r border-line px-3 py-2 font-semibold">Leads</th>
          <th className="px-3 py-2 font-semibold">Sales</th>
          <th className="px-3 py-2 font-semibold">Revenue</th>
          <th className="border-r border-line px-3 py-2 font-semibold">Worked</th>
          <th className="px-3 py-2 font-semibold">Sales</th>
          <th className="px-3 py-2 font-semibold">Revenue</th>
        </tr>
      </thead>
      <tbody>
        {showMore.visible.map((r, i) => (
          <tr key={`${r.course}-${i}`} className="border-b border-line last:border-0 hover:bg-offwhite">
            <td className="px-3 py-2.5 font-medium text-navy">{r.course}</td>
            <td className="px-3 py-2.5 font-medium text-ink">{num(r.totalSales)}</td>
            <td className="px-3 py-2.5 font-medium text-ink">{num(r.totalRevenue)}</td>
            <td className="border-r border-line px-3 py-2.5 text-ink-2">{num(r.n.leads)}</td>
            <td className="px-3 py-2.5 text-ink-2">{num(r.n.sales)}</td>
            <td className="px-3 py-2.5 text-ink-2">{num(r.n.revenue)}</td>
            <td className="border-r border-line px-3 py-2.5 text-ink-2">{num(r.o.leads)}</td>
            <td className="px-3 py-2.5 text-ink-2">{num(r.o.sales)}</td>
            <td className="px-3 py-2.5 text-ink-2">{num(r.o.revenue)}</td>
          </tr>
        ))}
      </tbody>
      {showMore.hiddenCount > 0 && (
        <tfoot>
          <tr>
            <td colSpan={9} className="px-3 py-2 text-center">
              <ShowMoreButton
                expanded={showMore.expanded}
                onToggle={showMore.toggle}
                hiddenCount={showMore.hiddenCount}
              />
            </td>
          </tr>
        </tfoot>
      )}
    </table>
  );
}

// العرض الافتراضي للمدير: لوحة قيادة تنفيذية + تجميع حسب الدورة (الدورة مرّة واحدة) + Drill-down لتفصيل الموظّفين
// + تقسيم البيانات الجديدة/القديمة (Phase 7).
function B2cCourseGroupedView({
  data,
  prev,
  newOld,
  breakdown,
  onBreakdownChange,
}: {
  data?: B2cCourseGroupedReport;
  prev?: B2cCourseGroupedReport;
  newOld?: B2cNewOldReport;
  breakdown: B2cBreakdown;
  onBreakdownChange: (v: B2cBreakdown) => void;
}) {
  const courses = data?.courses ?? [];
  const newOldCourses = newOld?.courses ?? [];
  const hasNewOld = newOldCourses.length > 0;

  // تجميع الدورات لكل مصدر (New/Old) مشتقّ من نفس بيانات by-course — يضمن تطابق الإجماليات مع «تفصيل الموظّف».
  const newCourses = useMemo(() => coursesForSource(courses, 'new'), [courses]);
  const oldCourses = useMemo(() => coursesForSource(courses, 'old'), [courses]);
  // فترة سابقة مُرشَّحة لنفس المصدر لتُغذّي مقارنات لوحة القيادة (Charts + KPI).
  const newPrev = useMemo<B2cCourseGroupedReport | undefined>(
    () => (prev ? { ...prev, courses: coursesForSource(prev.courses, 'new') } : undefined),
    [prev],
  );
  const oldPrev = useMemo<B2cCourseGroupedReport | undefined>(
    () => (prev ? { ...prev, courses: coursesForSource(prev.courses, 'old') } : undefined),
    [prev],
  );

  // فلتر المصدر يُعرَض دائمًا (كما في «تفصيل الموظّف») حتى مع غياب البيانات — الحالة الفارغة تظهر أسفله لا بدلًا منه.
  const isEmpty = courses.length === 0 && !hasNewOld;

  return (
    <div className="space-y-4">
      <Card>
        <BreakdownTabs value={breakdown} onChange={onBreakdownChange} />
      </Card>

      {isEmpty && (
        <Card className="p-5">
          <EmptyState
            title="لا توجد بيانات تجميع B2C"
            description="لا توجد تقارير B2C معتمَدة مطابقة لهذه الفترة ضمن نطاقك."
          />
        </Card>
      )}

      {!isEmpty && breakdown === 'all' && (
        <>
          {courses.length > 0 && (
            <>
              <B2cExecutiveDashboard courses={courses} prev={prev} periodKey={data?.periodKey} />
              <Collapsible title="تفصيل الدورات">
                <p className="mb-3 text-xs text-ink-2">اضغط على أي دورة لعرض مساهمات موظّفيها.</p>
                <div className="overflow-x-auto">
                  <B2cCourseGroupedTable courses={courses} />
                </div>
              </Collapsible>
            </>
          )}
          {newOld && hasNewOld && (
            <>
              <Collapsible title="مقارنة البيانات الجديدة New مقابل بيانات CRM القديمة Old">
                <NewOldComparison report={newOld} />
              </Collapsible>
              <Collapsible title="تفصيل الدورات — جديد مقابل قديم">
                <p className="mb-3 text-xs text-ink-2">
                  لكل دورة الإجمالي (جديد + قديم) ثم البيانات الجديدة New والبيانات القديمة Old جنبًا إلى جنب.
                </p>
                <div className="overflow-x-auto">
                  <AllBreakdownCourseTable courses={newOldCourses} />
                </div>
              </Collapsible>
            </>
          )}
        </>
      )}

      {!isEmpty && breakdown === 'new' && (
        <>
          {newCourses.length > 0 ? (
            <>
              <B2cExecutiveDashboard courses={newCourses} prev={newPrev} periodKey={data?.periodKey} />
              {newOld && hasNewOld && (
                <Collapsible title="مقارنة البيانات الجديدة New مقابل بيانات CRM القديمة Old">
                  <NewOldComparison report={newOld} />
                </Collapsible>
              )}
              <Collapsible title="أداء البيانات الجديدة New Leads حسب الدورة" defaultOpen>
                <p className="mb-3 text-xs text-ink-2">
                  اضغط على أي دورة لعرض مساهمات موظّفيها. نسبة التحويل = المبيعات ÷ New Leads.
                </p>
                <div className="overflow-x-auto">
                  <SourceCourseTable courses={newCourses} leadsLabel="New Leads" />
                </div>
              </Collapsible>
            </>
          ) : (
            <Card className="p-5">
              <EmptyState
                title="لا توجد بيانات New Leads"
                description="لا توجد بيانات جديدة New معتمَدة مطابقة لهذه الفترة ضمن نطاقك."
              />
            </Card>
          )}
        </>
      )}

      {!isEmpty && breakdown === 'old' && (
        <>
          {oldCourses.length > 0 ? (
            <>
              <B2cExecutiveDashboard courses={oldCourses} prev={oldPrev} periodKey={data?.periodKey} />
              {newOld && hasNewOld && (
                <Collapsible title="مقارنة البيانات الجديدة New مقابل بيانات CRM القديمة Old">
                  <NewOldComparison report={newOld} />
                </Collapsible>
              )}
              <Collapsible title="أداء بيانات CRM القديمة Old حسب الدورة" defaultOpen>
                <p className="mb-3 text-xs text-ink-2">
                  اضغط على أي دورة لعرض مساهمات موظّفيها. نسبة التحويل هنا = معدّل الاسترجاع (المبيعات ÷ Old Leads Worked).
                </p>
                <div className="overflow-x-auto">
                  <SourceCourseTable courses={oldCourses} leadsLabel="Old Leads Worked" />
                </div>
              </Collapsible>
            </>
          ) : (
            <Card className="p-5">
              <EmptyState
                title="لا توجد بيانات Old CRM"
                description="لا توجد بيانات CRM قديمة Old معتمَدة مطابقة لهذه الفترة ضمن نطاقك."
              />
            </Card>
          )}
        </>
      )}
    </div>
  );
}

function B2cCourseGroupedTable({ courses }: { courses: B2cCourseGroupRow[] }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const toggle = (course: string) => setExpanded((prev) => ({ ...prev, [course]: !prev[course] }));
  const showMore = useShowMore(courses, 8);

  return (
    <table className="w-full min-w-[1100px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">Course</th>
          <th className="px-3 py-2.5 font-semibold">الموظّفون</th>
          <th className="px-3 py-2.5 font-semibold">WorkHours</th>
          <th className="px-3 py-2.5 font-semibold">Leads</th>
          <th className="px-3 py-2.5 font-semibold">Contacted</th>
          <th className="px-3 py-2.5 font-semibold">Qualified</th>
          <th className="px-3 py-2.5 font-semibold">Sales</th>
          <th className="px-3 py-2.5 font-semibold">Revenue</th>
          <th className="px-3 py-2.5 font-semibold">نسبة التحويل</th>
          <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
        </tr>
      </thead>
      <tbody>
        {showMore.visible.map((c) => {
          const isOpen = !!expanded[c.course];
          return (
            <Fragment key={c.course}>
              <tr
                className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
                onClick={() => toggle(c.course)}
              >
                <td className="px-3 py-2.5 font-medium text-navy">
                  <span className="ml-1 inline-block w-3 text-ink-2">{isOpen ? '▾' : '▸'}</span>
                  {c.course}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{c.employeeCount.toLocaleString('ar-EG')}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.workHours)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.leads)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.contacted)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.qualified)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.sales)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.revenue)}</td>
                <td className="px-3 py-2.5 text-ink-2">{formatPercent(c.conversionRate)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.revenuePerHour)}</td>
              </tr>
              {isOpen && (
                <tr className="border-b border-line bg-offwhite/40 last:border-0">
                  <td colSpan={10} className="px-3 py-3">
                    {c.employees.length === 0 ? (
                      <p className="py-2 text-center text-xs text-ink-2">لا يوجد موظّفون لهذه الدورة.</p>
                    ) : (
                      <div className="space-y-3">
                        {c.employees.map((e, i) => (
                          <EmployeeBreakdownBlock key={`${c.course}-${e.employeeId}-${i}`} emp={e} />
                        ))}
                      </div>
                    )}
                  </td>
                </tr>
              )}
            </Fragment>
          );
        })}
      </tbody>
      {showMore.hiddenCount > 0 && (
        <tfoot>
          <tr>
            <td colSpan={10} className="px-3 py-2 text-center">
              <ShowMoreButton
                expanded={showMore.expanded}
                onToggle={showMore.toggle}
                hiddenCount={showMore.hiddenCount}
              />
            </td>
          </tr>
        </tfoot>
      )}
    </table>
  );
}

// هل الدلو (New أو Old) فارغ تمامًا؟ يُعرَض حينها «—» دون إخفاء القسم (متطلّب Phase 7.1).
function bucketEmpty(b: B2cNewOldBucket): boolean {
  return (
    b.revenue === 0 &&
    b.sales === 0 &&
    b.contacted === 0 &&
    b.qualified === 0 &&
    b.leads === 0 &&
    b.workHours === 0
  );
}

// صفّ قسم واحد داخل تفصيل الموظّف (Total / New Leads / Old CRM Data). القسم الفارغ يعرض «—» ولا يُخفى.
function BreakdownRow({
  label,
  tone,
  revenue,
  sales,
  contacted,
  qualifiedLabel,
  qualified,
  leadsLabel,
  leads,
  workHours,
  empty,
}: {
  label: string;
  tone: string;
  revenue: number;
  sales: number;
  contacted: number;
  qualifiedLabel: string;
  qualified: number;
  leadsLabel: string;
  leads: number;
  workHours: number;
  empty: boolean;
}) {
  const cell = (value: number) => (empty ? '—' : num(value));
  return (
    <tr className="border-b border-line/60 last:border-0">
      <td className={`px-3 py-1.5 text-xs font-semibold ${tone}`}>{label}</td>
      <td className="px-3 py-1.5 text-ink">{cell(revenue)}</td>
      <td className="px-3 py-1.5 text-ink">{cell(sales)}</td>
      <td className="px-3 py-1.5 text-ink-2">{cell(contacted)}</td>
      <td className="px-3 py-1.5 text-ink-2">
        <span className="ml-1 text-[10px] text-ink-2">{qualifiedLabel}:</span>
        {cell(qualified)}
      </td>
      <td className="px-3 py-1.5 text-ink-2">
        <span className="ml-1 text-[10px] text-ink-2">{leadsLabel}:</span>
        {cell(leads)}
      </td>
      <td className="px-3 py-1.5 text-ink-2">{cell(workHours)}</td>
    </tr>
  );
}

// كتلة تفصيل موظّف واحد داخل دورة: الاسم ثم ثلاثة أقسام (Total / New Leads / Old CRM Data).
function EmployeeBreakdownBlock({ emp }: { emp: B2cCourseEmployeeRow }) {
  return (
    <div className="rounded-lg border border-line bg-white p-2">
      <div className="mb-1.5 px-1 text-sm font-semibold text-navy">↳ {emp.employeeName}</div>
      <table className="w-full text-right text-sm">
        <thead className="border-b border-line text-[11px] text-ink-2">
          <tr>
            <th className="px-3 py-1 font-semibold">القسم</th>
            <th className="px-3 py-1 font-semibold">Revenue</th>
            <th className="px-3 py-1 font-semibold">Sales</th>
            <th className="px-3 py-1 font-semibold">Contacted</th>
            <th className="px-3 py-1 font-semibold">Qualified</th>
            <th className="px-3 py-1 font-semibold">Leads</th>
            <th className="px-3 py-1 font-semibold">WorkHours</th>
          </tr>
        </thead>
        <tbody>
          <BreakdownRow
            label="الإجمالي Total"
            tone="text-navy"
            revenue={emp.revenue}
            sales={emp.sales}
            contacted={emp.contacted}
            qualifiedLabel="Qualified"
            qualified={emp.qualified}
            leadsLabel="Leads"
            leads={emp.leads}
            workHours={emp.workHours}
            empty={false}
          />
          <BreakdownRow
            label="بيانات جديدة New"
            tone="text-orange-700"
            revenue={emp.new.revenue}
            sales={emp.new.sales}
            contacted={emp.new.contacted}
            qualifiedLabel="Qualified"
            qualified={emp.new.qualified}
            leadsLabel="New Leads"
            leads={emp.new.leads}
            workHours={emp.new.workHours}
            empty={bucketEmpty(emp.new)}
          />
          <BreakdownRow
            label="بيانات CRM قديمة Old"
            tone="text-navy"
            revenue={emp.old.revenue}
            sales={emp.old.sales}
            contacted={emp.old.contacted}
            qualifiedLabel="Requalified"
            qualified={emp.old.qualified}
            leadsLabel="Old Leads Worked"
            leads={emp.old.leads}
            workHours={emp.old.workHours}
            empty={bucketEmpty(emp.old)}
          />
        </tbody>
      </table>
    </div>
  );
}

// صفّ عرض واحد في «تفصيل حسب الموظّف» بعد تطبيق التقسيم المختار (الكل/New/Old).
type EmpDisplayRow = {
  key: string;
  employeeName: string;
  course: string;
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  sales: number;
  revenue: number;
  conversionRate: number;
  revenuePerHour: number;
};

// العرض التفصيلي: صفّ لكل (موظّف، دورة) مشتقّ من تجميع الدورة نفسه، مع فلتر التقسيم New/Old (Phase 7.1).
// عند اختيار التقسيم تتغيّر البطاقات والجدول معًا (المصدر = دلو Total/New/Old لكل موظّف).
function B2cEmployeeView({
  data,
  breakdown,
  onBreakdownChange,
}: {
  data?: B2cCourseGroupedReport;
  breakdown: B2cBreakdown;
  onBreakdownChange: (v: B2cBreakdown) => void;
}) {
  const leadsLabel = breakdown === 'new' ? 'New Leads' : breakdown === 'old' ? 'Old Leads Worked' : 'Leads';
  const qualifiedLabel = breakdown === 'old' ? 'Requalified' : 'Qualified';

  const rows = useMemo<EmpDisplayRow[]>(() => {
    const out: EmpDisplayRow[] = [];
    for (const c of data?.courses ?? []) {
      for (const e of c.employees) {
        let v: Omit<EmpDisplayRow, 'key' | 'employeeName' | 'course'>;
        if (breakdown === 'new' || breakdown === 'old') {
          const b = breakdown === 'new' ? e.new : e.old;
          v = {
            workHours: b.workHours,
            leads: b.leads,
            contacted: b.contacted,
            qualified: b.qualified,
            sales: b.sales,
            revenue: b.revenue,
            conversionRate: b.conversionRate,
            revenuePerHour: b.revenuePerHour,
          };
        } else {
          v = {
            workHours: e.workHours,
            leads: e.leads,
            contacted: e.contacted,
            qualified: e.qualified,
            sales: e.sales,
            revenue: e.revenue,
            conversionRate: e.conversionRate,
            revenuePerHour: e.workHours > 0 ? e.revenue / e.workHours : 0,
          };
        }
        // استبعاد الصفوف الفارغة تمامًا للتقسيم المختار (مثلًا موظّف بلا بيانات New عند تحديد New).
        if (
          v.workHours === 0 &&
          v.leads === 0 &&
          v.contacted === 0 &&
          v.qualified === 0 &&
          v.sales === 0 &&
          v.revenue === 0
        ) {
          continue;
        }
        out.push({ key: `${e.employeeId}-${c.course}`, employeeName: e.employeeName, course: c.course, ...v });
      }
    }
    return out.sort((a, b) => b.revenue - a.revenue || a.employeeName.localeCompare(b.employeeName));
  }, [data, breakdown]);

  const totals = rows.reduce(
    (a, r) => ({
      leads: a.leads + r.leads,
      contacted: a.contacted + r.contacted,
      qualified: a.qualified + r.qualified,
      sales: a.sales + r.sales,
      revenue: a.revenue + r.revenue,
    }),
    { leads: 0, contacted: 0, qualified: 0, sales: 0, revenue: 0 },
  );

  return (
    <div className="space-y-4">
      <Card>
        <BreakdownTabs value={breakdown} onChange={onBreakdownChange} />
      </Card>
      <SummaryCards
        rowCount={rows.length}
        periodKey={data?.periodKey}
        cards={[
          { label: `إجمالي ${leadsLabel}`, value: num(totals.leads) },
          { label: 'إجمالي Contacted', value: num(totals.contacted) },
          { label: 'إجمالي Sales', value: num(totals.sales), tone: 'navy' },
          { label: 'إجمالي Revenue', value: num(totals.revenue), tone: 'navy' },
        ]}
      />
      <Card className="overflow-x-auto p-0">
        {rows.length === 0 ? (
          <div className="p-5">
            <EmptyState
              title="لا توجد بيانات تجميع B2C"
              description="لا توجد تقارير B2C معتمَدة مطابقة لهذه الفترة ضمن نطاقك لهذا التقسيم."
            />
          </div>
        ) : (
          <table className="w-full min-w-[1100px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">الموظف</th>
                <th className="px-3 py-2.5 font-semibold">Course</th>
                <th className="px-3 py-2.5 font-semibold">WorkHours</th>
                <th className="px-3 py-2.5 font-semibold">{leadsLabel}</th>
                <th className="px-3 py-2.5 font-semibold">Contacted</th>
                <th className="px-3 py-2.5 font-semibold">{qualifiedLabel}</th>
                <th className="px-3 py-2.5 font-semibold">Sales</th>
                <th className="px-3 py-2.5 font-semibold">Revenue</th>
                <th className="px-3 py-2.5 font-semibold">نسبة التحويل</th>
                <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.key} className="border-b border-line last:border-0 hover:bg-offwhite">
                  <td className="px-3 py-2.5 font-medium text-navy">{r.employeeName}</td>
                  <td className="px-3 py-2.5 text-ink-2">{r.course}</td>
                  <td className="px-3 py-2.5 text-ink-2">{num(r.workHours)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{num(r.leads)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{num(r.contacted)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{num(r.qualified)}</td>
                  <td className="px-3 py-2.5 font-medium text-ink">{num(r.sales)}</td>
                  <td className="px-3 py-2.5 font-medium text-ink">{num(r.revenue)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{formatPercent(r.conversionRate)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{num(r.revenuePerHour)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

// ===== لوحة قيادة B2B (بنموذج B2C) — تجميع الصفوف حسب الخدمة على الواجهة =====

// تفصيل موظّف داخل خدمة (Drill-down).
type B2bEmp = {
  employeeId: string;
  employeeName: string;
  workHours: number;
  leads: number;
  meetings: number;
  proposals: number;
  won: number;
  lost: number;
  revenue: number;
};

// إجماليات خدمة عبر كل الموظّفين + قائمة تفصيل الموظّفين.
type B2bServiceGroup = {
  service: string;
  workHours: number;
  leads: number;
  meetings: number;
  proposals: number;
  negotiation: number;
  won: number;
  lost: number;
  revenue: number;
  employees: B2bEmp[];
};

// إجماليات B2B من الصفوف الخام (أسبوع/موظّف/خدمة).
function sumB2b(rows: B2bServiceAggregateRow[]) {
  return rows.reduce(
    (a, r) => ({
      workHours: a.workHours + r.workHours,
      leads: a.leads + r.leads,
      meetings: a.meetings + r.meetings,
      proposals: a.proposals + r.proposals,
      won: a.won + r.won,
      lost: a.lost + r.lost,
      revenue: a.revenue + r.revenue,
    }),
    { workHours: 0, leads: 0, meetings: 0, proposals: 0, won: 0, lost: 0, revenue: 0 },
  );
}

// تجميع الصفوف حسب الخدمة (الخدمة تظهر مرّة واحدة) + تفصيل الموظّفين داخلها؛ مرتّبة تنازليًّا بالإيراد.
function groupB2bByService(rows: B2bServiceAggregateRow[]): B2bServiceGroup[] {
  const map = new Map<string, B2bServiceGroup>();
  for (const r of rows) {
    const key = r.service?.trim() || '—';
    let g = map.get(key);
    if (!g) {
      g = { service: key, workHours: 0, leads: 0, meetings: 0, proposals: 0, negotiation: 0, won: 0, lost: 0, revenue: 0, employees: [] };
      map.set(key, g);
    }
    g.workHours += r.workHours;
    g.leads += r.leads;
    g.meetings += r.meetings;
    g.proposals += r.proposals;
    g.negotiation += r.negotiation;
    g.won += r.won;
    g.lost += r.lost;
    g.revenue += r.revenue;
    let e = g.employees.find((x) => x.employeeId === r.employeeId);
    if (!e) {
      e = { employeeId: r.employeeId, employeeName: r.employeeName, workHours: 0, leads: 0, meetings: 0, proposals: 0, won: 0, lost: 0, revenue: 0 };
      g.employees.push(e);
    }
    e.workHours += r.workHours;
    e.leads += r.leads;
    e.meetings += r.meetings;
    e.proposals += r.proposals;
    e.won += r.won;
    e.lost += r.lost;
    e.revenue += r.revenue;
  }
  return [...map.values()].sort((a, b) => b.revenue - a.revenue);
}

// قمع B2B: Leads → Meetings → Proposals → Won (أشرطة متناقصة بمعدّل كل خطوة).
function B2bFunnel({ leads, meetings, proposals, won }: { leads: number; meetings: number; proposals: number; won: number }) {
  const stages = [
    { label: 'Leads', value: leads, color: '#1A2B4A' },
    { label: 'Meetings', value: meetings, color: '#0277BD' },
    { label: 'Proposals', value: proposals, color: '#00838F' },
    { label: 'Won', value: won, color: '#2E7D32' },
  ];
  const max = Math.max(leads, 1);
  return (
    <ul className="space-y-2">
      {stages.map((s, i) => {
        const prev = i === 0 ? s.value : stages[i - 1].value;
        const stepRate = i === 0 ? 100 : ratio(s.value, prev);
        return (
          <li key={s.label}>
            <div className="mb-1 flex items-center justify-between text-xs">
              <span className="font-semibold text-ink">{s.label}</span>
              <span className="text-ink-2">
                {num(s.value)}
                {i > 0 && <span className="mr-2 text-ink-2">({pctText(stepRate)})</span>}
              </span>
            </div>
            <div className="h-6 w-full overflow-hidden rounded bg-offwhite">
              <div
                className="flex h-full items-center justify-start rounded pr-2 text-[10px] font-semibold text-white"
                style={{ width: `${Math.max(ratio(s.value, max), 3)}%`, backgroundColor: s.color }}
              />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

// لوحة قيادة B2B الكاملة (تُعرض أعلى جدول التفصيل).
function B2bDashboard({ rows, prev, periodKey }: { rows: B2bServiceAggregateRow[]; prev?: B2bAggregationReport; periodKey: string | null | undefined }) {
  const t = sumB2b(rows);
  const services = groupB2bByService(rows);
  const winRate = ratio(t.won, t.leads);

  const prevRows = prev?.rows ?? [];
  const hasPrev = prevRows.length > 0;
  const pt = hasPrev ? sumB2b(prevRows) : undefined;
  const prevWinRate = pt ? ratio(pt.won, pt.leads) : undefined;

  // توزيع الإيراد حسب الخدمة (Pie) + أعمدة الإيراد/الفوز لكل خدمة.
  const revenueByService = services.map((s) => ({ label: s.service, value: s.revenue }));
  const wonByService = [...services].sort((a, b) => b.won - a.won).map((s) => ({ label: s.service, value: s.won }));

  // أعلى 5 خدمات بالإيراد + أضعف الخدمات بنسبة الفوز (Won/Leads).
  const top5Revenue = [...services].sort((a, b) => b.revenue - a.revenue).slice(0, 5).map((s) => ({ label: s.service, value: s.revenue }));
  const worstServices = [...services]
    .sort((a, b) => ratio(a.won, a.leads) - ratio(b.won, b.leads) || a.revenue - b.revenue)
    .slice(0, 5)
    .map((s) => ({ label: s.service, value: ratio(s.won, s.leads) }));

  // أعلى الموظّفين بالإيراد ضمن الفترة (تجميع عبر كل الخدمات).
  const empMap = new Map<string, { name: string; revenue: number; won: number }>();
  for (const s of services) {
    for (const e of s.employees) {
      const cur = empMap.get(e.employeeId) ?? { name: e.employeeName, revenue: 0, won: 0 };
      cur.revenue += e.revenue;
      cur.won += e.won;
      empMap.set(e.employeeId, cur);
    }
  }
  const topEmployees = [...empMap.values()].sort((a, b) => b.revenue - a.revenue || b.won - a.won).slice(0, 5).map((e) => ({ label: e.name, value: e.revenue }));

  return (
    <div className="space-y-4">
      {/* بطاقات المؤشّرات الرئيسية */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
        <StatCard label="إجمالي Leads" value={num(t.leads)} />
        <StatCard label="إجمالي Meetings" value={num(t.meetings)} />
        <StatCard label="إجمالي Proposals" value={num(t.proposals)} />
        <StatCard label="إجمالي Won" value={num(t.won)} tone="navy" />
        <StatCard label="إجمالي Revenue" value={num(t.revenue)} tone="navy" />
        <StatCard label="نسبة الفوز (Won/Leads)" value={pctText(winRate)} tone="navy" />
      </div>

      {/* مقارنة الفترة السابقة */}
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-bold text-navy">مقارنة مع الفترة السابقة</h3>
          <Badge tone="navy">{hasPrev ? `مقابل ${formatPeriod(prev?.periodKey)}` : 'لا توجد بيانات فترة سابقة'}</Badge>
        </div>
        <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-4">
          <div className="rounded-lg border border-line p-3">
            <div className="text-xs text-ink-2">Revenue</div>
            <div className="mt-0.5 text-lg font-bold text-ink">{num(t.revenue)}</div>
            <DeltaBadge current={t.revenue} previous={pt?.revenue} />
          </div>
          <div className="rounded-lg border border-line p-3">
            <div className="text-xs text-ink-2">Won</div>
            <div className="mt-0.5 text-lg font-bold text-ink">{num(t.won)}</div>
            <DeltaBadge current={t.won} previous={pt?.won} />
          </div>
          <div className="rounded-lg border border-line p-3">
            <div className="text-xs text-ink-2">Meetings</div>
            <div className="mt-0.5 text-lg font-bold text-ink">{num(t.meetings)}</div>
            <DeltaBadge current={t.meetings} previous={pt?.meetings} />
          </div>
          <div className="rounded-lg border border-line p-3">
            <div className="text-xs text-ink-2">نسبة الفوز</div>
            <div className="mt-0.5 text-lg font-bold text-ink">{pctText(winRate)}</div>
            <DeltaBadge current={winRate} previous={prevWinRate} kind="percentPoints" />
          </div>
        </div>
      </Card>

      {/* القمع + توزيع الإيراد */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="قمع التحويل" hint={`للفترة ${formatPeriod(periodKey)} — Leads ← Meetings ← Proposals ← Won`}>
          <B2bFunnel leads={t.leads} meetings={t.meetings} proposals={t.proposals} won={t.won} />
        </Panel>
        <Panel title="توزيع الإيراد حسب الخدمة">
          <PieChart slices={revenueByService} />
        </Panel>
      </div>

      {/* أعمدة الإيراد + الفوز لكل خدمة */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="الإيراد لكل خدمة">
          <BarChart rows={revenueByService} format={num} />
        </Panel>
        <Panel title="عدد الصفقات المكسوبة لكل خدمة">
          <BarChart rows={wonByService} format={num} />
        </Panel>
      </div>

      {/* أعلى الخدمات + أضعفها + أعلى الموظّفين */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Panel title="أعلى 5 خدمات — الإيراد">
          <RankList rows={top5Revenue} format={num} />
        </Panel>
        <Panel title="أضعف الخدمات — نسبة الفوز" hint="Won ÷ Leads">
          <RankList rows={worstServices} format={pctText} />
        </Panel>
        <Panel title="أعلى الموظّفين — الإيراد" hint="ضمن الفترة المختارة">
          <RankList rows={topEmployees} format={num} />
        </Panel>
      </div>

      {/* خريطة حرارة معدّلات التحويل حسب الخدمة */}
      <Panel title="خريطة حرارة التحويل حسب الخدمة" hint="كلّما اخضرّت الخلية زاد المعدّل">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2 font-semibold">Service</th>
                <th className="px-3 py-2 text-center font-semibold">Lead ← Meeting</th>
                <th className="px-3 py-2 text-center font-semibold">Meeting ← Proposal</th>
                <th className="px-3 py-2 text-center font-semibold">Proposal ← Won</th>
              </tr>
            </thead>
            <tbody>
              {services.map((s) => (
                <tr key={s.service} className="border-b border-line last:border-0">
                  <td className="px-3 py-2 font-medium text-navy">{s.service}</td>
                  <HeatCell value={ratio(s.meetings, s.leads)} />
                  <HeatCell value={ratio(s.proposals, s.meetings)} />
                  <HeatCell value={ratio(s.won, s.proposals)} />
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>

      {/* جدول التفصيل حسب الخدمة (Drill-down للموظّفين) */}
      <Card className="overflow-x-auto p-0">
        <B2bServiceGroupedTable services={services} />
      </Card>
    </div>
  );
}

// أزرار تبديل مصدر بيانات B2B (الكل / New Leads / Data Scraping) — RC-3 Task 2A.
function B2bSourceTabs({ value, onChange }: { value: B2bSource; onChange: (v: B2bSource) => void }) {
  const items: { key: B2bSource; label: string }[] = [
    { key: 'all', label: 'الكل (New + Data)' },
    { key: 'new', label: 'عملاء جدد New Leads' },
    { key: 'data', label: 'سحب البيانات Data Scraping' },
  ];
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-xs font-semibold text-ink-2">مصدر البيانات:</span>
      {items.map((it) => (
        <button
          key={it.key}
          type="button"
          onClick={() => onChange(it.key)}
          className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
            value === it.key ? 'bg-navy text-white' : 'bg-offwhite text-ink-2 hover:bg-line'
          }`}
        >
          {it.label}
        </button>
      ))}
    </div>
  );
}

// يختار دلو المصدر المطلوب (الكل/جديد/سحب بيانات) من صفّ موظّف داخل خدمة.
function pickB2bBucket(
  row: { total: B2bSourceBucket; newLeads: B2bSourceBucket; dataScraping: B2bSourceBucket },
  source: B2bSource,
): B2bSourceBucket {
  return source === 'new' ? row.newLeads : source === 'data' ? row.dataScraping : row.total;
}

// يحوّل تقرير B2B «حسب المصدر» إلى صفوف (خدمة، موظّف) للدلو المختار — كي تُعاد استخدام لوحة B2bDashboard كما هي.
// Data Scraping: Leads يمثّل Scraped Leads (رأس القمع)؛ Lost/LostRate غير مُتتبَّعين في نموذج المصدر ⇒ صفر.
function b2bSourceToRows(report: B2bSourceReport | undefined, source: B2bSource): B2bServiceAggregateRow[] {
  if (!report) return [];
  const rows: B2bServiceAggregateRow[] = [];
  for (const s of report.services) {
    for (const e of s.employees) {
      const b = pickB2bBucket(e, source);
      rows.push({
        periodKey: report.periodKey ?? '',
        employeeId: e.employeeId,
        employeeName: e.employeeName,
        service: s.service,
        teamId: e.teamId,
        departmentId: e.departmentId,
        workHours: b.workHours,
        leads: b.leads,
        meetings: b.meetings,
        proposals: b.proposals,
        negotiation: b.negotiation,
        won: b.won,
        lost: 0,
        revenue: b.revenue,
        meetingRate: b.meetingRate,
        proposalRate: b.proposalRate,
        winRate: b.winRate,
        revenuePerHour: b.revenuePerHour,
        wonPerHour: b.wonPerHour,
        lostRate: 0,
      });
    }
  }
  return rows;
}

function B2bView({ data, prev, source, onSourceChange }: {
  data?: B2bSourceReport;
  prev?: B2bSourceReport;
  source: B2bSource;
  onSourceChange: (v: B2bSource) => void;
}) {
  const rows = b2bSourceToRows(data, source);
  const prevRows = b2bSourceToRows(prev, source);
  const prevReport: B2bAggregationReport | undefined = prev
    ? {
        periodKey: prev.periodKey,
        rowCount: prevRows.length,
        submissionsConsidered: prev.submissionsConsidered,
        submissionsIgnored: prev.submissionsIgnored,
        rowsIgnored: prev.rowsIgnored,
        viewLevel: prev.viewLevel,
        rows: prevRows,
      }
    : undefined;

  return (
    <div className="space-y-4">
      <B2bSourceTabs value={source} onChange={onSourceChange} />
      <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
        <Badge tone="navy">{formatPeriod(data?.periodKey)}</Badge>
        <span>عدد الخدمات: {(data?.serviceCount ?? 0).toLocaleString('ar-EG')}</span>
      </div>
      {rows.length === 0 ? (
        <Card>
          <EmptyState
            title="لا توجد بيانات تجميع B2B"
            description="لا توجد تقارير B2B معتمَدة مطابقة لهذه الفترة ومصدر البيانات ضمن نطاقك."
          />
        </Card>
      ) : (
        <B2bDashboard rows={rows} prev={prevReport} periodKey={data?.periodKey} />
      )}
    </div>
  );
}

// جدول تجميع B2B حسب الخدمة (الخدمة مرّة واحدة) + تفصيل الموظّفين عند التوسيع.
function B2bServiceGroupedTable({ services }: { services: B2bServiceGroup[] }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const toggle = (service: string) => setExpanded((prev) => ({ ...prev, [service]: !prev[service] }));

  return (
    <table className="w-full min-w-[1100px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">Service</th>
          <th className="px-3 py-2.5 font-semibold">الموظّفون</th>
          <th className="px-3 py-2.5 font-semibold">WorkHours</th>
          <th className="px-3 py-2.5 font-semibold">Leads</th>
          <th className="px-3 py-2.5 font-semibold">Meetings</th>
          <th className="px-3 py-2.5 font-semibold">Proposals</th>
          <th className="px-3 py-2.5 font-semibold">Won</th>
          <th className="px-3 py-2.5 font-semibold">Revenue</th>
          <th className="px-3 py-2.5 font-semibold">نسبة الفوز</th>
          <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
        </tr>
      </thead>
      <tbody>
        {services.map((s) => {
          const isOpen = !!expanded[s.service];
          return (
            <Fragment key={s.service}>
              <tr className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite" onClick={() => toggle(s.service)}>
                <td className="px-3 py-2.5 font-medium text-navy">
                  <span className="ml-1 inline-block w-3 text-ink-2">{isOpen ? '▾' : '▸'}</span>
                  {s.service}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{s.employees.length.toLocaleString('ar-EG')}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(s.workHours)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(s.leads)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(s.meetings)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(s.proposals)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(s.won)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(s.revenue)}</td>
                <td className="px-3 py-2.5 text-ink-2">{pctText(ratio(s.won, s.leads))}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(s.workHours > 0 ? s.revenue / s.workHours : 0)}</td>
              </tr>
              {isOpen &&
                s.employees.map((e, i) => (
                  <tr key={`${s.service}-${e.employeeId}-${i}`} className="border-b border-line bg-offwhite/40 last:border-0">
                    <td className="px-3 py-2 pr-8 text-ink-2">↳ {e.employeeName}</td>
                    <td className="px-3 py-2" />
                    <td className="px-3 py-2 text-ink-2">{num(e.workHours)}</td>
                    <td className="px-3 py-2 text-ink-2">{num(e.leads)}</td>
                    <td className="px-3 py-2 text-ink-2">{num(e.meetings)}</td>
                    <td className="px-3 py-2 text-ink-2">{num(e.proposals)}</td>
                    <td className="px-3 py-2 text-ink">{num(e.won)}</td>
                    <td className="px-3 py-2 text-ink">{num(e.revenue)}</td>
                    <td className="px-3 py-2 text-ink-2">{pctText(ratio(e.won, e.leads))}</td>
                    <td className="px-3 py-2" />
                  </tr>
                ))}
            </Fragment>
          );
        })}
      </tbody>
    </table>
  );
}

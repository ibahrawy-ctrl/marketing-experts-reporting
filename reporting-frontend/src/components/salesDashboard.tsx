import { Fragment, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Badge, Card, StatCard } from './ui';
import { formatPeriod, formatPercent } from '../lib/format';
import type {
  B2bAggregationReport,
  B2bServiceAggregateRow,
  B2cCourseEmployeeRow,
  B2cCourseGroupedReport,
  B2cCourseGroupRow,
  B2cNewOldBucket,
  B2cNewOldReport,
} from '../types/api';

// ===== وحدة عرض لوحات المبيعات المشتركة (RC-3 Task 1.1) =====
// مكوّنات عرض B2C/B2B مشتركة بين لوحة قائد الفريق ولوحة المندوب الشخصية.
// كلّها عرض فقط — لا تجلب بيانات ولا تفرض نطاقًا (النطاق مفروض خادميًّا في نقاط التجميع).

// عرض رقم عشري مختصر (يزيل الأصفار الزائدة): 4000 ⇒ «4٬000»، 0.6 ⇒ «0٫6».
export function num(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return Number(value).toLocaleString('ar-EG', { maximumFractionDigits: 2 });
}

// نسبة مئوية آمنة (تتفادى القسمة على صفر).
export function ratio(numer: number, denom: number): number {
  return denom > 0 ? (numer / denom) * 100 : 0;
}

// تنسيق نسبة مئوية مختصرة للعرض داخل الرسوم.
export function pctText(value: number): string {
  return `${value.toLocaleString('ar-EG', { maximumFractionDigits: 1 })}٪`;
}

// لوحة ألوان ثابتة للرسوم (تدور عند تجاوز العدد).
const CHART_COLORS = [
  '#F57C00', '#1A2B4A', '#2E7D32', '#00838F', '#6A1B9A',
  '#C62828', '#455A64', '#AD1457', '#0277BD', '#EF6C00',
];
export function colorAt(index: number): string {
  return CHART_COLORS[index % CHART_COLORS.length];
}

// ===== الرسوم (SVG/CSS خفيفة بلا مكتبات) =====

// شارة التغيّر مقابل الفترة السابقة (▲ أخضر / ▼ أحمر / — رمادي).
export function DeltaBadge({
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
  return <span className={`text-xs font-semibold ${tone}`}>{arrow} {detail}</span>;
}

// قمع تحويل B2C: Lead → Contacted → Qualified → Sales (أشرطة متناقصة).
export function Funnel({ leads, contacted, qualified, sales }: { leads: number; contacted: number; qualified: number; sales: number }) {
  const stages = [
    { label: 'Leads', value: leads, color: '#1A2B4A' },
    { label: 'Contacted', value: contacted, color: '#0277BD' },
    { label: 'Qualified', value: qualified, color: '#00838F' },
    { label: 'Sales', value: sales, color: '#2E7D32' },
  ];
  return <FunnelBars stages={stages} />;
}

// قمع تحويل B2B: Leads → Meetings → Proposals → Won.
export function B2bFunnel({ leads, meetings, proposals, won }: { leads: number; meetings: number; proposals: number; won: number }) {
  const stages = [
    { label: 'Leads', value: leads, color: '#1A2B4A' },
    { label: 'Meetings', value: meetings, color: '#0277BD' },
    { label: 'Proposals', value: proposals, color: '#00838F' },
    { label: 'Won', value: won, color: '#2E7D32' },
  ];
  return <FunnelBars stages={stages} />;
}

function FunnelBars({ stages }: { stages: { label: string; value: number; color: string }[] }) {
  const max = Math.max(stages[0]?.value ?? 0, 1);
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

// مخطط أعمدة أفقيّة CSS (مناسب لـ RTL) — قيمة لكل بند.
export function BarChart({ rows, format }: { rows: { label: string; value: number }[]; format: (v: number) => string }) {
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
            <div className="h-full rounded-full" style={{ width: `${ratio(r.value, max)}%`, backgroundColor: colorAt(i) }} />
          </div>
        </li>
      ))}
    </ul>
  );
}

// مخطط دائري (Pie) بسيط بـ SVG — توزيع قيمة على بنود.
export function PieChart({ slices }: { slices: { label: string; value: number }[] }) {
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

// خلية خريطة حرارة (كثافة الأخضر حسب النسبة).
function HeatCell({ value }: { value: number }) {
  const clamped = Math.max(0, Math.min(100, value));
  const alpha = 0.08 + (clamped / 100) * 0.82;
  const strong = clamped >= 45;
  return (
    <td className="px-3 py-2 text-center" style={{ backgroundColor: `rgba(46, 125, 50, ${alpha})` }}>
      <span className={strong ? 'font-semibold text-white' : 'text-ink'}>{pctText(value)}</span>
    </td>
  );
}

// قائمة مرتّبة (Top N) ببطاقة صغيرة.
export function RankList({ rows, format }: { rows: { label: string; value: number }[]; format: (v: number) => string }) {
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

export function Panel({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <Card>
      <h3 className="text-sm font-bold text-navy">{title}</h3>
      {hint && <p className="mt-0.5 text-xs text-ink-2">{hint}</p>}
      <div className="mt-3">{children}</div>
    </Card>
  );
}

// ===================== B2C =====================

// إجماليات الدورات (صحيحة لأن إجمالي كل دورة = مجموع مساهمات موظّفيها).
export function sumCourses(courses: B2cCourseGroupRow[]) {
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

// بطاقات المؤشّرات الرئيسية B2C (تُستعمل للفريق وللمندوب).
export function B2cKpiCards({ courses }: { courses: B2cCourseGroupRow[] }) {
  const t = sumCourses(courses);
  const conversion = ratio(t.sales, t.leads);
  const revenuePerHour = t.workHours > 0 ? t.revenue / t.workHours : 0;
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:grid-cols-8">
      <StatCard label="إجمالي Leads" value={num(t.leads)} />
      <StatCard label="إجمالي Contacted" value={num(t.contacted)} />
      <StatCard label="إجمالي Qualified" value={num(t.qualified)} />
      <StatCard label="إجمالي Sales" value={num(t.sales)} tone="navy" />
      <StatCard label="إجمالي Revenue" value={num(t.revenue)} tone="navy" />
      <StatCard label="إجمالي WorkHours" value={num(t.workHours)} />
      <StatCard label="نسبة التحويل" value={pctText(conversion)} tone="navy" />
      <StatCard label="الإيراد/ساعة" value={num(revenuePerHour)} tone="navy" />
    </div>
  );
}

// مقارنة B2C مع الفترة السابقة (Revenue / Sales / نسبة التحويل).
export function B2cPrevComparison({ courses, prev }: { courses: B2cCourseGroupRow[]; prev?: B2cCourseGroupedReport }) {
  const t = sumCourses(courses);
  const conversion = ratio(t.sales, t.leads);
  const prevCourses = prev?.courses ?? [];
  const hasPrev = prevCourses.length > 0;
  const pt = hasPrev ? sumCourses(prevCourses) : undefined;
  const prevConversion = pt ? ratio(pt.sales, pt.leads) : undefined;
  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-bold text-navy">مقارنة مع الفترة السابقة</h3>
        <Badge tone="navy">{hasPrev ? `مقابل ${formatPeriod(prev?.periodKey)}` : 'لا توجد بيانات فترة سابقة'}</Badge>
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
          <div className="mt-0.5 text-lg font-bold text-ink">{pctText(conversion)}</div>
          <DeltaBadge current={conversion} previous={prevConversion} kind="percentPoints" />
        </div>
      </div>
    </Card>
  );
}

function NewOldMetric({ label, newValue, oldValue, format }: {
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

// قسم البيانات الجديدة New مقابل بيانات CRM القديمة Old (Breakdown All/New/Old).
export function NewOldSection({ report }: { report: B2cNewOldReport }) {
  const n = report.newTotals;
  const o = report.oldTotals;
  return (
    <Card>
      <h3 className="text-sm font-bold text-navy">البيانات الجديدة New مقابل بيانات CRM القديمة Old</h3>
      <p className="mt-0.5 text-xs text-ink-2">
        «نسبة تحويل New» = المبيعات ÷ New Leads. «معدّل استرجاع Old» = المبيعات ÷ Old Leads Worked.
      </p>
      <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <NewOldMetric label="Revenue" newValue={n.revenue} oldValue={o.revenue} format={num} />
        <NewOldMetric label="Sales" newValue={n.sales} oldValue={o.sales} format={num} />
        <NewOldMetric label="WorkHours" newValue={n.workHours} oldValue={o.workHours} format={num} />
        <NewOldMetric
          label="نسبة التحويل New / معدّل الاسترجاع Old"
          newValue={n.conversionRate}
          oldValue={o.conversionRate}
          format={pctText}
        />
      </div>
    </Card>
  );
}

// تجميع مساهمات كل موظّف عبر كل الدورات (على الواجهة، بلا تكرار حساب خادمي).
type EmpAgg = {
  employeeId: string;
  employeeName: string;
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  sales: number;
  revenue: number;
};

export function aggregateEmployees(courses: B2cCourseGroupRow[]): EmpAgg[] {
  const map = new Map<string, EmpAgg>();
  for (const c of courses) {
    for (const e of c.employees) {
      const cur = map.get(e.employeeId) ?? {
        employeeId: e.employeeId,
        employeeName: e.employeeName,
        workHours: 0,
        leads: 0,
        contacted: 0,
        qualified: 0,
        sales: 0,
        revenue: 0,
      };
      cur.workHours += e.workHours;
      cur.leads += e.leads;
      cur.contacted += e.contacted;
      cur.qualified += e.qualified;
      cur.sales += e.sales;
      cur.revenue += e.revenue;
      map.set(e.employeeId, cur);
    }
  }
  return [...map.values()];
}

// شبكة رسوم B2C: القمع، الإيراد لكل دورة، المبيعات لكل موظّف، أعلى الدورات، أعلى الموظّفين.
// showEmployees=false يخفي رسوم المستوى الشخصي (مناسبة للوحة المندوب الفردي).
export function B2cChartsGrid({ courses, periodKey, newOld, showEmployees = true }: {
  courses: B2cCourseGroupRow[];
  periodKey: string | null | undefined;
  newOld?: B2cNewOldReport;
  showEmployees?: boolean;
}) {
  const t = sumCourses(courses);
  const revenueByCourse = [...courses].sort((a, b) => b.revenue - a.revenue).map((c) => ({ label: c.course, value: c.revenue }));
  const salesByCourse = [...courses].sort((a, b) => b.sales - a.sales).map((c) => ({ label: c.course, value: c.sales }));
  const emps = aggregateEmployees(courses);
  const salesByEmployee = [...emps].sort((a, b) => b.sales - a.sales).map((e) => ({ label: e.employeeName, value: e.sales }));
  const topCourses = [...courses].sort((a, b) => b.revenue - a.revenue).slice(0, 5).map((c) => ({ label: c.course, value: c.revenue }));
  const topEmployees = [...emps].sort((a, b) => b.revenue - a.revenue || b.sales - a.sales).slice(0, 5).map((e) => ({ label: e.employeeName, value: e.revenue }));

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="قمع التحويل" hint={`للفترة ${formatPeriod(periodKey)} — Lead ← Contacted ← Qualified ← Sales`}>
          <Funnel leads={t.leads} contacted={t.contacted} qualified={t.qualified} sales={t.sales} />
        </Panel>
        {newOld && (
          <Panel title="مقارنة الإيراد: جديد New مقابل قديم Old">
            <BarChart
              rows={[
                { label: 'Revenue — New', value: newOld.newTotals.revenue },
                { label: 'Revenue — Old', value: newOld.oldTotals.revenue },
              ]}
              format={num}
            />
          </Panel>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="الإيراد لكل دورة">
          <BarChart rows={revenueByCourse} format={num} />
        </Panel>
        <Panel title="المبيعات لكل دورة">
          <BarChart rows={salesByCourse} format={num} />
        </Panel>
      </div>

      {showEmployees ? (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Panel title="أعلى 5 دورات — الإيراد">
            <RankList rows={topCourses} format={num} />
          </Panel>
          <Panel title="أعلى 5 موظّفين — الإيراد">
            <RankList rows={topEmployees} format={num} />
          </Panel>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Panel title="أعلى 5 دورات — الإيراد">
            <RankList rows={topCourses} format={num} />
          </Panel>
          <Panel title="المبيعات لكل موظّف">
            <BarChart rows={salesByEmployee} format={num} />
          </Panel>
        </div>
      )}
    </div>
  );
}

// هل الدلو (New أو Old) فارغ تمامًا؟ يُعرَض حينها «—» دون إخفاء القسم (منطق Phase 7.1).
function bucketEmpty(b: B2cNewOldBucket): boolean {
  return b.revenue === 0 && b.sales === 0 && b.contacted === 0 && b.qualified === 0 && b.leads === 0 && b.workHours === 0;
}

function BreakdownRow({
  label, tone, revenue, sales, contacted, qualifiedLabel, qualified, leadsLabel, leads, workHours, empty,
}: {
  label: string; tone: string; revenue: number; sales: number; contacted: number;
  qualifiedLabel: string; qualified: number; leadsLabel: string; leads: number; workHours: number; empty: boolean;
}) {
  const cell = (value: number) => (empty ? '—' : num(value));
  return (
    <tr className="border-b border-line/60 last:border-0">
      <td className={`px-3 py-1.5 text-xs font-semibold ${tone}`}>{label}</td>
      <td className="px-3 py-1.5 text-ink">{cell(revenue)}</td>
      <td className="px-3 py-1.5 text-ink">{cell(sales)}</td>
      <td className="px-3 py-1.5 text-ink-2">{cell(contacted)}</td>
      <td className="px-3 py-1.5 text-ink-2">
        <span className="ml-1 text-[10px] text-ink-2">{qualifiedLabel}:</span>{cell(qualified)}
      </td>
      <td className="px-3 py-1.5 text-ink-2">
        <span className="ml-1 text-[10px] text-ink-2">{leadsLabel}:</span>{cell(leads)}
      </td>
      <td className="px-3 py-1.5 text-ink-2">{cell(workHours)}</td>
    </tr>
  );
}

// كتلة تفصيل موظّف واحد داخل دورة: الاسم ثم ثلاثة أقسام (Total / New Leads / Old CRM Data).
export function EmployeeBreakdownBlock({ emp, showName = true }: { emp: B2cCourseEmployeeRow; showName?: boolean }) {
  return (
    <div className="rounded-lg border border-line bg-white p-2">
      {showName && <div className="mb-1.5 px-1 text-sm font-semibold text-navy">↳ {emp.employeeName}</div>}
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
            label="الإجمالي Total" tone="text-navy" revenue={emp.revenue} sales={emp.sales} contacted={emp.contacted}
            qualifiedLabel="Qualified" qualified={emp.qualified} leadsLabel="Leads" leads={emp.leads} workHours={emp.workHours} empty={false}
          />
          <BreakdownRow
            label="بيانات جديدة New" tone="text-orange-700" revenue={emp.new.revenue} sales={emp.new.sales} contacted={emp.new.contacted}
            qualifiedLabel="Qualified" qualified={emp.new.qualified} leadsLabel="New Leads" leads={emp.new.leads} workHours={emp.new.workHours} empty={bucketEmpty(emp.new)}
          />
          <BreakdownRow
            label="بيانات CRM قديمة Old" tone="text-navy" revenue={emp.old.revenue} sales={emp.old.sales} contacted={emp.old.contacted}
            qualifiedLabel="Requalified" qualified={emp.old.qualified} leadsLabel="Old Leads Worked" leads={emp.old.leads} workHours={emp.old.workHours} empty={bucketEmpty(emp.old)}
          />
        </tbody>
      </table>
    </div>
  );
}

// جدول أداء الدورات (مرتّب من الأفضل) + Drill-down لموظّفي الدورة.
export function CoursesPerformanceTable({ courses }: { courses: B2cCourseGroupRow[] }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const toggle = (course: string) => setExpanded((prev) => ({ ...prev, [course]: !prev[course] }));
  const sorted = [...courses].sort((a, b) => b.revenue - a.revenue || a.course.localeCompare(b.course));

  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">Course</th>
          <th className="px-3 py-2.5 font-semibold">Leads</th>
          <th className="px-3 py-2.5 font-semibold">Sales</th>
          <th className="px-3 py-2.5 font-semibold">Revenue</th>
          <th className="px-3 py-2.5 font-semibold">نسبة التحويل</th>
          <th className="px-3 py-2.5 font-semibold">WorkHours</th>
          <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
        </tr>
      </thead>
      <tbody>
        {sorted.map((c) => {
          const isOpen = !!expanded[c.course];
          return (
            <Fragment key={c.course}>
              <tr className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite" onClick={() => toggle(c.course)}>
                <td className="px-3 py-2.5 font-medium text-navy">
                  <span className="ml-1 inline-block w-3 text-ink-2">{isOpen ? '▾' : '▸'}</span>
                  {c.course}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.leads)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.sales)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(c.revenue)}</td>
                <td className="px-3 py-2.5 text-ink-2">{formatPercent(c.conversionRate)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.workHours)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(c.revenuePerHour)}</td>
              </tr>
              {isOpen && (
                <tr className="border-b border-line bg-offwhite/40 last:border-0">
                  <td colSpan={7} className="px-3 py-3">
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
    </table>
  );
}

// جدول أداء الموظّفين (تجميع عبر الدورات) + Drill-down لدورات كل موظّف.
export function EmployeesPerformanceTable({ courses }: { courses: B2cCourseGroupRow[] }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const toggle = (id: string) => setExpanded((prev) => ({ ...prev, [id]: !prev[id] }));

  const coursesByEmp = useMemo(() => {
    const map = new Map<string, { course: string; row: B2cCourseEmployeeRow }[]>();
    for (const c of courses) {
      for (const e of c.employees) {
        const arr = map.get(e.employeeId) ?? [];
        arr.push({ course: c.course, row: e });
        map.set(e.employeeId, arr);
      }
    }
    return map;
  }, [courses]);

  const emps = useMemo(
    () => aggregateEmployees(courses).sort((a, b) => b.revenue - a.revenue || a.employeeName.localeCompare(b.employeeName)),
    [courses],
  );

  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">الموظف</th>
          <th className="px-3 py-2.5 font-semibold">Leads</th>
          <th className="px-3 py-2.5 font-semibold">Sales</th>
          <th className="px-3 py-2.5 font-semibold">Revenue</th>
          <th className="px-3 py-2.5 font-semibold">نسبة التحويل</th>
          <th className="px-3 py-2.5 font-semibold">WorkHours</th>
          <th className="px-3 py-2.5 font-semibold">الإيراد/ساعة</th>
        </tr>
      </thead>
      <tbody>
        {emps.map((e) => {
          const isOpen = !!expanded[e.employeeId];
          const conversion = ratio(e.sales, e.leads);
          const revenuePerHour = e.workHours > 0 ? e.revenue / e.workHours : 0;
          const empCourses = coursesByEmp.get(e.employeeId) ?? [];
          return (
            <Fragment key={e.employeeId}>
              <tr className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite" onClick={() => toggle(e.employeeId)}>
                <td className="px-3 py-2.5 font-medium text-navy">
                  <span className="ml-1 inline-block w-3 text-ink-2">{isOpen ? '▾' : '▸'}</span>
                  {e.employeeName}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{num(e.leads)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(e.sales)}</td>
                <td className="px-3 py-2.5 font-medium text-ink">{num(e.revenue)}</td>
                <td className="px-3 py-2.5 text-ink-2">{pctText(conversion)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(e.workHours)}</td>
                <td className="px-3 py-2.5 text-ink-2">{num(revenuePerHour)}</td>
              </tr>
              {isOpen && (
                <tr className="border-b border-line bg-offwhite/40 last:border-0">
                  <td colSpan={7} className="px-3 py-3">
                    {empCourses.length === 0 ? (
                      <p className="py-2 text-center text-xs text-ink-2">لا توجد دورات لهذا الموظّف.</p>
                    ) : (
                      <div className="space-y-3">
                        {empCourses
                          .sort((a, b) => b.row.revenue - a.row.revenue)
                          .map((ec, i) => (
                            <div key={`${e.employeeId}-${ec.course}-${i}`}>
                              <div className="mb-1 px-1 text-xs font-semibold text-ink-2">الدورة: {ec.course}</div>
                              <EmployeeBreakdownBlock emp={ec.row} showName={false} />
                            </div>
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
    </table>
  );
}

// ===================== B2B =====================

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
export function sumB2b(rows: B2bServiceAggregateRow[]) {
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

// تجميع الصفوف حسب الخدمة + تفصيل الموظّفين داخلها؛ مرتّبة تنازليًّا بالإيراد.
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

// لوحة قيادة B2B الكاملة (قمع، توزيع الإيراد، أعلى الخدمات، جدول تفصيل).
// showEmployees=false يخفي أعمدة/رسوم المستوى الشخصي (مناسبة للوحة المندوب الفردي).
export function B2bDashboard({ rows, prev, periodKey, showEmployees = true }: {
  rows: B2bServiceAggregateRow[];
  prev?: B2bAggregationReport;
  periodKey: string | null | undefined;
  showEmployees?: boolean;
}) {
  const t = sumB2b(rows);
  const services = groupB2bByService(rows);
  const winRate = ratio(t.won, t.leads);
  const revenuePerHour = t.workHours > 0 ? t.revenue / t.workHours : 0;

  const prevRows = prev?.rows ?? [];
  const hasPrev = prevRows.length > 0;
  const pt = hasPrev ? sumB2b(prevRows) : undefined;
  const prevWinRate = pt ? ratio(pt.won, pt.leads) : undefined;

  const revenueByService = services.map((s) => ({ label: s.service, value: s.revenue }));
  const wonByService = [...services].sort((a, b) => b.won - a.won).map((s) => ({ label: s.service, value: s.won }));

  const top5Revenue = [...services].sort((a, b) => b.revenue - a.revenue).slice(0, 5).map((s) => ({ label: s.service, value: s.revenue }));
  const worstServices = [...services]
    .sort((a, b) => ratio(a.won, a.leads) - ratio(b.won, b.leads) || a.revenue - b.revenue)
    .slice(0, 5)
    .map((s) => ({ label: s.service, value: ratio(s.won, s.leads) }));

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
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-7">
        <StatCard label="إجمالي Leads" value={num(t.leads)} />
        <StatCard label="إجمالي Meetings" value={num(t.meetings)} />
        <StatCard label="إجمالي Proposals" value={num(t.proposals)} />
        <StatCard label="إجمالي Won" value={num(t.won)} tone="navy" />
        <StatCard label="إجمالي Revenue" value={num(t.revenue)} tone="navy" />
        <StatCard label="إجمالي WorkHours" value={num(t.workHours)} />
        <StatCard label="نسبة الفوز (Won/Leads)" value={pctText(winRate)} tone="navy" />
      </div>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4">
        <StatCard label="الإيراد/ساعة" value={num(revenuePerHour)} tone="navy" />
      </div>

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

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="قمع التحويل" hint={`للفترة ${formatPeriod(periodKey)} — Leads ← Meetings ← Proposals ← Won`}>
          <B2bFunnel leads={t.leads} meetings={t.meetings} proposals={t.proposals} won={t.won} />
        </Panel>
        <Panel title="توزيع الإيراد حسب الخدمة">
          <PieChart slices={revenueByService} />
        </Panel>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="الإيراد لكل خدمة">
          <BarChart rows={revenueByService} format={num} />
        </Panel>
        <Panel title="عدد الصفقات المكسوبة لكل خدمة">
          <BarChart rows={wonByService} format={num} />
        </Panel>
      </div>

      <div className={`grid grid-cols-1 gap-4 ${showEmployees ? 'lg:grid-cols-3' : 'lg:grid-cols-2'}`}>
        <Panel title="أعلى 5 خدمات — الإيراد">
          <RankList rows={top5Revenue} format={num} />
        </Panel>
        <Panel title="أضعف الخدمات — نسبة الفوز" hint="Won ÷ Leads">
          <RankList rows={worstServices} format={pctText} />
        </Panel>
        {showEmployees && (
          <Panel title="أعلى الموظّفين — الإيراد" hint="ضمن الفترة المختارة">
            <RankList rows={topEmployees} format={num} />
          </Panel>
        )}
      </div>

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

      {showEmployees && (
        <Card className="overflow-x-auto p-0">
          <B2bServiceGroupedTable services={services} />
        </Card>
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

// KPI-INDIVIDUAL-DASHBOARD-R1 — لوحة مؤشرات أداء موظّف بعينه (Frontend-only).
// نمطان: «مؤشرات أدائي» (الموظّف الحالي) و«مؤشرات أداء الموظف» (موظّف ضمن نطاق المستخدم).
// النطاق مفروض خادمًا بالكامل: الرأس والتقييمات والتجميع كلها عبر نقاط مقيّدة بـ ScopeResolver
// (employee-profile / kpi-evaluations/subject / kpi-evaluations/aggregate). من يفتح موظّفًا خارج نطاقه
// يحصل 403/404 ⇒ نعرض حالة فارغة واضحة بدل أرقام مضللة. لا يضيف أي endpoint ولا يكتب أي شيء.
import { useMemo, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { api, apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import {
  formatPercent,
  formatPeriod,
  kpiEvaluationStatusLabel,
  kpiTrendDisplay,
  monthKey,
  quarterKey,
} from '../lib/format';
import { riyadhToday } from '../lib/dashboardPeriod';
import { kpiTone } from '../lib/useKpi';
import type {
  EmployeeProfileDto,
  KpiAggregateDto,
  KpiEvaluationListItemDto,
  KpiGranularity,
} from '../types/api';
import { Badge, Card, EmptyState, Field, Select, Spinner, StatCard } from '../components/ui';
import { Donut, LineTrend } from '../components/Charts';

// ===== مرشّحات الفترة المطلوبة =====
type PeriodFilter =
  | 'this_month'
  | 'this_quarter'
  | 'last_4_weeks'
  | 'last_8_weeks'
  | 'last_12_weeks'
  | 'this_year'
  | 'custom';

const FILTER_OPTIONS: { value: PeriodFilter; label: string }[] = [
  { value: 'this_month', label: 'الشهر الحالي' },
  { value: 'this_quarter', label: 'الربع الحالي' },
  { value: 'last_4_weeks', label: 'آخر 4 أسابيع' },
  { value: 'last_8_weeks', label: 'آخر 8 أسابيع' },
  { value: 'last_12_weeks', label: 'آخر 12 أسبوع' },
  { value: 'this_year', label: 'السنة الحالية' },
  { value: 'custom', label: 'فترة مخصصة' },
];

// طلب التجميع المُشتق من المرشّح: شهري/ربعي/سنوي عبر periodKey، وآخر N أسابيع/المخصّص عبر from/to.
interface AggSpec {
  granularity: KpiGranularity;
  periodKey?: string;
  from?: string;
  to?: string;
}

// ===== أدوات التاريخ (UTC، دورة التقارير السبت→الجمعة مطابقة ReportingCalendarPolicy) =====
const DAY_MS = 86_400_000;
function addDays(d: Date, n: number): Date {
  return new Date(d.getTime() + n * DAY_MS);
}
function cycleSaturday(date: Date): Date {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const diff = (d.getUTCDay() - 6 + 7) % 7; // Saturday = 6
  return addDays(d, -diff);
}
function isoDate(d: Date): string {
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
function parseIso(s: string): Date {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s.trim());
  if (!m) return riyadhToday();
  return new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])));
}

// مدى آخر N دورات شاملًا الدورة الحالية: [سبت البداية, جمعة النهاية].
function lastNWeeksRange(n: number, anchor: Date): { from: string; to: string } {
  const curSat = cycleSaturday(anchor);
  const startSat = addDays(curSat, -(n - 1) * 7);
  const endFri = addDays(curSat, 6);
  return { from: isoDate(startSat), to: isoDate(endFri) };
}

// طلب الفترة الحالية حسب المرشّح.
function currentSpec(filter: PeriodFilter, from: string, to: string): AggSpec | null {
  const today = riyadhToday();
  const y = today.getUTCFullYear();
  switch (filter) {
    case 'this_month':
      return { granularity: 'Monthly', periodKey: monthKey(y, today.getUTCMonth() + 1) };
    case 'this_quarter':
      return { granularity: 'Quarterly', periodKey: quarterKey(y, Math.floor(today.getUTCMonth() / 3) + 1) };
    case 'this_year':
      return { granularity: 'Yearly', periodKey: String(y) };
    case 'last_4_weeks':
      return { granularity: 'Custom', ...lastNWeeksRange(4, today) };
    case 'last_8_weeks':
      return { granularity: 'Custom', ...lastNWeeksRange(8, today) };
    case 'last_12_weeks':
      return { granularity: 'Custom', ...lastNWeeksRange(12, today) };
    case 'custom':
      return from && to ? { granularity: 'Custom', from, to } : null;
  }
}

// طلب الفترة السابقة (للمقارنة) — نفس طول الفترة الحالية مزاحًا للخلف.
function previousSpec(filter: PeriodFilter, from: string, to: string): AggSpec | null {
  const today = riyadhToday();
  const y = today.getUTCFullYear();
  const m0 = today.getUTCMonth(); // 0..11
  switch (filter) {
    case 'this_month': {
      const pm = m0 === 0 ? 12 : m0; // الشهر السابق (1..12)
      const py = m0 === 0 ? y - 1 : y;
      return { granularity: 'Monthly', periodKey: monthKey(py, pm) };
    }
    case 'this_quarter': {
      const q = Math.floor(m0 / 3) + 1;
      const pq = q === 1 ? 4 : q - 1;
      const py = q === 1 ? y - 1 : y;
      return { granularity: 'Quarterly', periodKey: quarterKey(py, pq) };
    }
    case 'this_year':
      return { granularity: 'Yearly', periodKey: String(y - 1) };
    case 'last_4_weeks':
      return shiftWeeks(4, today);
    case 'last_8_weeks':
      return shiftWeeks(8, today);
    case 'last_12_weeks':
      return shiftWeeks(12, today);
    case 'custom': {
      if (!from || !to) return null;
      const f = parseIso(from);
      const t = parseIso(to);
      const len = Math.round((t.getTime() - f.getTime()) / DAY_MS) + 1;
      return { granularity: 'Custom', from: isoDate(addDays(f, -len)), to: isoDate(addDays(f, -1)) };
    }
  }
}
// الـ N دورات التي تسبق نافذة «آخر N دورات» الحالية مباشرة.
function shiftWeeks(n: number, anchor: Date): AggSpec {
  const curSat = cycleSaturday(anchor);
  const curFromSat = addDays(curSat, -(n - 1) * 7);
  const prevToFri = addDays(curFromSat, -1);
  const prevFromSat = addDays(curFromSat, -n * 7);
  return { granularity: 'Custom', from: isoDate(prevFromSat), to: isoDate(prevToFri) };
}

// معاملات الاستعلام لطلب /kpi-evaluations/aggregate.
function aggParams(spec: AggSpec, userId: string): Record<string, string> {
  const p: Record<string, string> = { granularity: spec.granularity, subjectUserId: userId };
  if (spec.periodKey) p.periodKey = spec.periodKey;
  if (spec.from) p.from = spec.from;
  if (spec.to) p.to = spec.to;
  return p;
}

// B-6 — لا نطاقات 85/60 مكتوبة هنا: التصنيف يُقاس على العتبة القادمة من الخادم عبر `kpiTone`،
// وغيابها يعني «لا حكم» (`muted`) لا افتراض رقم.

/** نبرة بطاقة رقميّة: إنذار فقط عند حكم صريح من الخادم بأنّ الرقم دون المستهدف. */
function alertTone(value: number | null, threshold: number | null): 'alert' | 'navy' {
  return kpiTone(value, threshold) === 'alert' ? 'alert' : 'navy';
}

// ===== المكوّن المشترك =====
function IndividualKpiDashboard({ userId, mode }: { userId: string; mode: 'self' | 'managed' }) {
  const [filter, setFilter] = useState<PeriodFilter>('this_month');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');

  const curSpec = useMemo(() => currentSpec(filter, customFrom, customTo), [filter, customFrom, customTo]);
  const prevSpec = useMemo(() => previousSpec(filter, customFrom, customTo), [filter, customFrom, customTo]);

  // (1) الرأس — مقيّد خادميًّا (403/404 ⇒ حالة فارغة).
  const headerQ = useQuery({
    queryKey: ['individual-kpi-header', userId],
    queryFn: async () => (await api.get<EmployeeProfileDto>(`/dashboard/employee-profile/${userId}`)).data,
    retry: false,
  });

  // (2) كل تقييمات الموظّف — مقيّد خادميًّا (self-or-scope).
  const evalsQ = useQuery({
    queryKey: ['individual-kpi-evals', userId],
    queryFn: async () =>
      (await api.get<KpiEvaluationListItemDto[]>(`/kpi-evaluations/subject/${userId}`)).data,
    retry: false,
  });

  // (3) التجميع للفترة الحالية + السابقة — مقيّد خادميًّا.
  const curAggQ = useQuery({
    queryKey: ['individual-kpi-agg', userId, curSpec],
    queryFn: async () =>
      (await api.get<KpiAggregateDto>('/kpi-evaluations/aggregate', { params: aggParams(curSpec!, userId) })).data,
    enabled: !!curSpec,
    retry: false,
  });
  const prevAggQ = useQuery({
    queryKey: ['individual-kpi-agg-prev', userId, prevSpec],
    queryFn: async () =>
      (await api.get<KpiAggregateDto>('/kpi-evaluations/aggregate', { params: aggParams(prevSpec!, userId) })).data,
    enabled: !!prevSpec,
    retry: false,
  });

  const title = mode === 'self' ? 'مؤشرات أدائي' : 'مؤشرات أداء الموظف';

  if (headerQ.isLoading) return <Spinner />;

  if (headerQ.isError) {
    const status = axios.isAxiosError(headerQ.error) ? headerQ.error.response?.status : undefined;
    const denied = status === 403 || status === 404;
    return (
      <div className="mx-auto max-w-3xl py-10">
        <EmptyState
          title={denied ? 'لا يمكن عرض هذه المؤشرات' : 'تعذّر تحميل المؤشرات'}
          description={
            denied
              ? 'هذا الموظّف خارج نطاق صلاحيتك، أو الملف غير موجود. لا يمكنك رؤية مؤشرات موظّف لا يخصّ نطاقك.'
              : apiErrorMessage(headerQ.error)
          }
        />
      </div>
    );
  }

  if (!headerQ.data) return null;
  const header = headerQ.data.header;
  const curAgg = curAggQ.data;
  const prevAgg = prevAggQ.data;

  // الأسابيع المحتسَبة داخل الفترة (مرتّبة تصاعديًّا) — مصدر الاتجاه والتوزيع والجدول.
  const weeks = [...(curAgg?.weeks ?? [])].sort((a, b) => a.periodKey.localeCompare(b.periodKey));
  const weekKeys = new Set(weeks.map((w) => w.periodKey));

  // تقييمات الموظّف ضمن الفترة (أسبوعية محتسَبة فقط — نتجاهل المسودّات غير النهائية).
  const inRange = (evalsQ.data ?? [])
    .filter((e) => e.periodType === 'Weekly' && e.totalScore != null && weekKeys.has(e.periodKey))
    .sort((a, b) => b.periodKey.localeCompare(a.periodKey));

  const scores = inRange.map((e) => e.totalScore as number);
  const average = curAgg?.average ?? null;
  const evalCount = curAgg?.evaluationsCount ?? 0;
  const lastScore = inRange[0]?.totalScore ?? null;
  const highest = scores.length ? Math.max(...scores) : null;
  const lowest = scores.length ? Math.min(...scores) : null;
  const diff =
    average != null && prevAgg?.average != null ? Math.round((average - prevAgg.average) * 10) / 10 : null;
  const trendLabel =
    diff == null ? 'لا توجد بيانات كافية' : diff > 0 ? 'تحسّن' : diff < 0 ? 'تراجع' : 'ثابت';
  const trendTone: 'success' | 'alert' | 'navy' = diff == null ? 'navy' : diff > 0 ? 'success' : diff < 0 ? 'alert' : 'navy';

  // B-6: العتبة المطبَّقة كما أعادها الخادم مع التجميع — لا ثابت في هذه الشاشة.
  const threshold = curAgg?.appliedBelowTargetThreshold ?? null;

  // توزيع التقييمات حسب النطاق (لهذا الموظّف فقط)، مقيسًا على عتبة الخادم.
  const dist = { excellent: 0, mid: 0, below: 0, unrated: 0 };
  for (const s of scores) {
    const tone = kpiTone(s, threshold);
    if (tone === 'success') dist.excellent++;
    else if (tone === 'gold') dist.mid++;
    else if (tone === 'alert') dist.below++;
    else dist.unrated++;
  }

  // نقاط منحنى الاتجاه + جدول الأداء (الأسابيع داخل الفترة).
  const trendPoints = weeks.map((w) => ({ label: `أ${w.periodKey.slice(-2)}`, value: w.score }));
  const hasData = average != null || inRange.length > 0;

  return (
    <div className="space-y-6">
      {/* ===== الرأس ===== */}
      <Card>
        <div className="mb-1 flex flex-wrap items-center gap-2">
          <h1 className="text-2xl font-bold text-navy">{title}</h1>
          {!header.isActive && <Badge tone="alert">غير مفعّل</Badge>}
        </div>
        <p className="text-sm text-ink-2">تحليل أداء الموظف عبر الفترات والتقييمات السابقة.</p>
        <div className="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-5">
          <div>
            <p className="text-ink-2">الموظّف</p>
            <p className="font-medium text-ink">{header.fullName}</p>
          </div>
          <div>
            <p className="text-ink-2">المسمّى الوظيفي</p>
            <p className="font-medium text-ink">{header.jobRoleName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">الفريق</p>
            <p className="font-medium text-ink">{header.teamName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">الإدارة</p>
            <p className="font-medium text-ink">{header.departmentName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">المدير المباشر</p>
            <p className="font-medium text-ink">{header.directManagerName ?? '—'}</p>
          </div>
        </div>
      </Card>

      {/* ===== المرشّح الزمني ===== */}
      <Card>
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-44">
            <Field label="الفترة">
              <Select value={filter} onChange={(e) => setFilter(e.target.value as PeriodFilter)}>
                {FILTER_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </Select>
            </Field>
          </div>
          {filter === 'custom' && (
            <>
              <div className="w-40">
                <Field label="من">
                  <input
                    type="date"
                    value={customFrom}
                    onChange={(e) => setCustomFrom(e.target.value)}
                    className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                  />
                </Field>
              </div>
              <div className="w-40">
                <Field label="إلى">
                  <input
                    type="date"
                    value={customTo}
                    onChange={(e) => setCustomTo(e.target.value)}
                    className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                  />
                </Field>
              </div>
            </>
          )}
          {curAgg && (
            <p className="pb-2 text-sm text-ink-2">{curAgg.periodLabel}</p>
          )}
        </div>
      </Card>

      {!hasData ? (
        <EmptyState
          title="لا توجد تقييمات في هذه الفترة"
          description="جرّب توسيع الفترة الزمنية أو اختيار فترة أخرى."
        />
      ) : (
        <>
          {/* ===== بطاقات الملخّص ===== */}
          <section>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard
                label="متوسط KPI في الفترة"
                value={average == null ? '—' : formatPercent(average)}
                tone={alertTone(average, threshold)}
              />
              <StatCard label="عدد التقييمات" value={evalCount} />
              <StatCard label="آخر تقييم" value={lastScore ?? '—'} tone={alertTone(lastScore, threshold)} />
              <StatCard label="أعلى تقييم" value={highest ?? '—'} />
            </div>
            <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <StatCard label="أقل تقييم" value={lowest ?? '—'} tone={alertTone(lowest, threshold)} />
              <StatCard
                label="الفرق عن الفترة السابقة"
                value={diff == null ? '—' : `${diff > 0 ? '+' : ''}${diff}`}
                tone={trendTone}
              />
              <Card>
                <p className="text-sm text-ink-2">اتجاه الأداء</p>
                <p className={`mt-2 text-lg font-semibold ${trendTone === 'alert' ? 'text-alert' : trendTone === 'success' ? 'text-success' : 'text-navy'}`}>
                  {trendLabel}
                </p>
              </Card>
            </div>
          </section>

          {/* ===== منحنى الأداء + التوزيع ===== */}
          <section className="grid gap-4 lg:grid-cols-2">
            <Card>
              <h2 className="mb-3 font-semibold text-navy">منحنى الأداء عبر الأسابيع</h2>
              <LineTrend points={trendPoints} />
            </Card>
            <Card>
              <h2 className="mb-3 font-semibold text-navy">توزيع التقييمات</h2>
              <Donut
                slices={[
                  { label: threshold === null ? 'بلغ المستهدف' : `بلغ المستهدف (≥${threshold})`, value: dist.excellent },
                  {
                    label: threshold === null ? 'قريب من المستهدف' : `قريب من المستهدف (≥${Math.round(threshold * 0.75)})`,
                    value: dist.mid,
                  },
                  {
                    label: threshold === null ? 'دون المستهدف' : `دون المستهدف (<${Math.round(threshold * 0.75)})`,
                    value: dist.below,
                  },
                  { label: 'بلا عتبة معتمَدة', value: dist.unrated },
                ]}
              />
            </Card>
          </section>

          {/* ===== جدول الأداء الأسبوعي ===== */}
          <section>
            <h2 className="mb-3 font-semibold text-navy">أداء الأسابيع داخل الفترة</h2>
            <Card>
              {weeks.length === 0 ? (
                <p className="py-4 text-center text-sm text-ink-2">لا توجد أسابيع محتسَبة داخل هذه الفترة.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[480px] text-right text-sm">
                    <thead className="border-b border-line text-xs text-ink-2">
                      <tr>
                        <th className="px-2 py-2 font-semibold">الأسبوع</th>
                        <th className="px-2 py-2 font-semibold">النتيجة</th>
                        <th className="px-2 py-2 font-semibold">الفرق عن السابق</th>
                        <th className="px-2 py-2 font-semibold">الاتجاه</th>
                        <th className="px-2 py-2 font-semibold">عدد التقييمات</th>
                      </tr>
                    </thead>
                    <tbody>
                      {weeks.map((w, i) => {
                        const prev = i > 0 ? weeks[i - 1].score : null;
                        const wDiff = prev == null ? null : Math.round((w.score - prev) * 10) / 10;
                        const arrow = wDiff == null ? '—' : wDiff > 0 ? 'صاعد ▲' : wDiff < 0 ? 'هابط ▼' : 'ثابت ▬';
                        return (
                          <tr key={w.periodKey} className="border-b border-line last:border-0">
                            <td className="px-2 py-2 font-medium text-navy">{formatPeriod(w.periodKey)}</td>
                            <td className="px-2 py-2 font-semibold">{formatPercent(w.score)}</td>
                            <td className="px-2 py-2 text-ink-2">{wDiff == null ? '—' : `${wDiff > 0 ? '+' : ''}${wDiff}`}</td>
                            <td className="px-2 py-2 text-ink-2">{arrow}</td>
                            <td className="px-2 py-2 text-ink-2">{w.evaluationsCount}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
          </section>

          {/* ===== جدول تقييمات الموظّف ===== */}
          <section>
            <h2 className="mb-3 font-semibold text-navy">{mode === 'self' ? 'تقييماتي' : 'تقييمات الموظف'}</h2>
            <Card>
              {inRange.length === 0 ? (
                <p className="py-4 text-center text-sm text-ink-2">لا توجد تقييمات في هذه الفترة.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-right text-sm">
                    <thead className="text-ink-2">
                      <tr className="border-b border-line">
                        <th className="py-2">الفترة</th>
                        <th className="py-2">القالب</th>
                        <th className="py-2">الدرجة</th>
                        <th className="py-2">الحالة</th>
                        <th className="py-2">الاتجاه</th>
                        <th className="py-2"></th>
                      </tr>
                    </thead>
                    <tbody>
                      {inRange.map((k) => (
                        <tr key={k.id} className="border-b border-line/60">
                          <td className="py-2 text-ink-2">{formatPeriod(k.periodKey)}</td>
                          <td className="py-2 font-medium text-ink">{k.templateTitle}</td>
                          <td className="py-2 font-semibold text-navy">{k.totalScore ?? '—'}</td>
                          <td className="py-2">
                            <Badge tone="navy">{kpiEvaluationStatusLabel[k.status]}</Badge>
                          </td>
                          <td className="py-2 text-ink-2">{kpiTrendDisplay(k.trend, k.totalScore != null)}</td>
                          <td className="py-2">
                            <Link className="text-orange-600 hover:underline" to={`/app/kpi?open=${k.id}`}>
                              عرض التفاصيل
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
          </section>
        </>
      )}
    </div>
  );
}

// مسار «مؤشرات أدائي» — الموظّف الحالي حصرًا (يقرأ هويته من الجلسة).
export function MyKpiPage() {
  const { user } = useAuth();
  if (!user) return <Spinner />;
  return <IndividualKpiDashboard userId={user.userId} mode="self" />;
}

// مسار «مؤشرات أداء الموظف» — موظّف بعينه (النطاق مفروض خادمًا).
export function EmployeeKpiPage() {
  const { userId = '' } = useParams();
  return <IndividualKpiDashboard userId={userId} mode="managed" />;
}

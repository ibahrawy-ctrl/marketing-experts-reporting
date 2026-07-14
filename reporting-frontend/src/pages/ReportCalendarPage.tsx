// تقويم التقارير وتجميع KPI الدوري (Phase 5) — لوحة تشغيلية وظيفية (لا إعادة تصميم بصري).
// تجمع إشارات المتابعة: التقارير الناقصة (§5)، تأخّر الاعتماد (§6)، التزام المبيعات اليومي (§9)،
// ومتوسط KPI الدوري (§8) مع مرشّحات الفترة (أسبوع/شهر/ربع/سنة/مدى مخصّص — §10).
// كل المصادر محصورة بنطاق المستخدم خادميًّا (ScopeResolver) — لا تصفية من الواجهة فقط.
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { Alert, Badge, Card, Field, Input, Select, StatCard } from '../components/ui';
import { WeeklyCycleCalendarPicker } from '../components/WeeklyCycleCalendarPicker';
import { LoadingState, QueryError } from '../components/states';
import {
  formatDate,
  formatPercent,
  granularityLabel,
  monthKey,
  monthNamesAr,
  quarterKey,
} from '../lib/format';
import type {
  ApprovalDelaysReport,
  KpiAggregateDto,
  KpiGranularity,
  MissingReportsReport,
  SalesDailyComplianceReport,
} from '../types/api';

// السنوات المتاحة في المرشّحات — السنة الحالية وما حولها.
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = [CURRENT_YEAR + 1, CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2];

export default function ReportCalendarPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">تقويم التقارير والتجميع الدوري</h1>
        <p className="mt-1 text-sm text-ink-2">
          دورة التقارير من السبت إلى الجمعة، وتاريخ الاستحقاق يختلف بحسب دورك. تابع هنا التقارير الناقصة،
          وتأخّر الاعتماد، والتزام المبيعات اليومي، ومتوسط مؤشرات الأداء عبر الفترات — كلّها ضمن نطاقك فقط.
        </p>
      </div>
      <Alert tone="navy">
        كل الأرقام محصورة بنطاق صلاحيتك (نفسك/فريقك/إدارتك) ويُفرض ذلك على الخادم. تأخّر الاعتماد يظهر
        للمستوى الأعلى من المعتمِد المتأخّر فقط.
      </Alert>

      <WeeklyCalendarSection />
      <KpiAggregateSection />
    </div>
  );
}

// ===== القسم الأسبوعي: تقارير ناقصة + تأخّر اعتماد + التزام مبيعات يومي =====
function WeeklyCalendarSection() {
  // الأسبوع المختار = مفتاح الدورة المحسوب خادميًّا عبر المنتقي (لا إدخال نصّي، لا حساب محليّ).
  const [weekKey, setWeekKey] = useState<string | null>(null);

  const missing = useQuery({
    queryKey: ['report-calendar', 'missing', weekKey ?? 'current'],
    queryFn: async () =>
      (await api.get<MissingReportsReport>('/report-calendar/missing-reports', {
        params: weekKey ? { weekKey } : undefined,
      })).data,
  });
  const delays = useQuery({
    queryKey: ['report-calendar', 'approval-delays'],
    queryFn: async () => (await api.get<ApprovalDelaysReport>('/report-calendar/approval-delays')).data,
  });
  const sales = useQuery({
    queryKey: ['report-calendar', 'sales-daily', weekKey ?? 'current'],
    queryFn: async () =>
      (await api.get<SalesDailyComplianceReport>('/report-calendar/sales-daily-compliance', {
        params: weekKey ? { weekKey } : undefined,
      })).data,
  });

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-navy">المتابعة الأسبوعية</h2>
          {missing.data && (
            <p className="mt-0.5 text-sm text-ink-2">
              {missing.data.periodLabel} · {formatDate(missing.data.weekStart)} ←{' '}
              {formatDate(missing.data.weekEnd)}
            </p>
          )}
        </div>
        <div className="w-full max-w-sm">
          <Field label="أسبوع محدّد">
            <WeeklyCycleCalendarPicker
              context="Report"
              value={weekKey}
              onChange={(key) => setWeekKey(key)}
            />
          </Field>
        </div>
      </div>

      {/* (1) التقارير الناقصة */}
      <MissingReportsBlock query={missing} />

      {/* (2) تأخّر الاعتماد */}
      <ApprovalDelaysBlock query={delays} />

      {/* (3) التزام المبيعات اليومي */}
      <SalesComplianceBlock query={sales} />
    </Card>
  );
}

function MissingReportsBlock({ query }: { query: ReturnType<typeof useQuery<MissingReportsReport>> }) {
  if (query.isLoading) return <LoadingState label="يتم تحميل التقارير الناقصة…" />;
  if (query.isError) return <QueryError onRetry={() => query.refetch()} description="تعذّر جلب التقارير الناقصة." />;
  const r = query.data;
  if (!r) return null;

  return (
    <section className="mb-8">
      <h3 className="mb-3 font-semibold text-navy">التقارير الأسبوعية الناقصة</h3>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="متوقَّع منهم تقرير" value={r.expectedCount} />
        <StatCard label="سلّموا" value={r.submittedCount} />
        <StatCard label="متأخّرون" value={r.lateCount} tone={r.lateCount > 0 ? 'alert' : 'navy'} />
        <StatCard label="لم يُسلّموا" value={r.missingCount} tone={r.missingCount > 0 ? 'alert' : 'navy'} />
      </div>
      {r.leaveCount > 0 && (
        <p className="mt-2 text-xs text-ink-2">
          مستثنون بإجازة معتمدة تغطّي الأسبوع: {r.leaveCount} — لا يُحتسبون ناقصين.
        </p>
      )}

      {r.teamShortfalls.length > 0 && (
        <div className="mt-4">
          <p className="mb-2 text-sm font-semibold text-ink-2">فرق بها قصور</p>
          <div className="flex flex-wrap gap-2">
            {r.teamShortfalls.map((t) => (
              <Badge key={t.teamId ?? t.teamName} tone="alert">
                {t.teamName}: ناقص {t.missing}{t.late > 0 ? ` · متأخّر ${t.late}` : ''} من {t.expected}
              </Badge>
            ))}
          </div>
        </div>
      )}

      <div className="mt-4">
        {!r.canViewRows ? (
          <Alert tone="navy">ملخّص فقط — تفاصيل الأفراد غير معروضة على هذا المستوى.</Alert>
        ) : r.rows.length === 0 ? (
          <p className="py-4 text-center text-sm text-ink-2">لا يوجد متوقَّع منهم تقرير ضمن نطاقك لهذا الأسبوع.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">الموظف</th>
                  <th className="px-2 py-2 font-semibold">الدور</th>
                  <th className="px-2 py-2 font-semibold">الفريق</th>
                  <th className="px-2 py-2 font-semibold">آخر موعد</th>
                  <th className="px-2 py-2 font-semibold">الحالة</th>
                </tr>
              </thead>
              <tbody>
                {r.rows.map((row) => (
                  <tr key={row.userId} className="border-b border-line last:border-0">
                    <td className="px-2 py-2 font-medium">
                      <Link to={`/app/employee/${row.userId}`} className="text-navy hover:text-orange-600 hover:underline">
                        {row.fullName}
                      </Link>
                    </td>
                    <td className="px-2 py-2 text-ink-2">{row.roleLabel}</td>
                    <td className="px-2 py-2 text-ink-2">{row.teamName ?? '—'}</td>
                    <td className="px-2 py-2 text-ink-2">{formatDate(row.dueDate)}</td>
                    <td className="px-2 py-2">
                      <Badge
                        tone={
                          row.status === 'submitted' ? 'success'
                          : row.status === 'leave' ? 'navy'
                          : row.status === 'late' ? 'gold'
                          : 'alert'
                        }
                      >
                        {row.status === 'submitted' ? 'سلّم'
                          : row.status === 'leave' ? 'إجازة معتمدة'
                          : row.status === 'late' ? 'متأخّر'
                          : 'لم يُسلّم'}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}

function ApprovalDelaysBlock({ query }: { query: ReturnType<typeof useQuery<ApprovalDelaysReport>> }) {
  if (query.isLoading) return <LoadingState label="يتم تحميل تأخّر الاعتماد…" />;
  if (query.isError) return <QueryError onRetry={() => query.refetch()} description="تعذّر جلب تأخّر الاعتماد." />;
  const r = query.data;
  if (!r) return null;

  return (
    <section className="mb-8">
      <div className="mb-3 flex items-center gap-2">
        <h3 className="font-semibold text-navy">تقارير تأخّر اعتمادها</h3>
        <Badge tone={r.delayCount > 0 ? 'alert' : 'success'}>{r.delayCount}</Badge>
      </div>
      {r.rows.length === 0 ? (
        <p className="py-4 text-center text-sm text-ink-2">
          لا توجد تقارير تجاوزت مهلة الاعتماد ضمن نطاقك. (تظهر هنا للمستوى الأعلى من المعتمِد المتأخّر فقط.)
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">التقرير</th>
                <th className="px-2 py-2 font-semibold">صاحب التقرير</th>
                <th className="px-2 py-2 font-semibold">المعتمِد المتأخّر</th>
                <th className="px-2 py-2 font-semibold">آخر موعد</th>
                <th className="px-2 py-2 font-semibold">تأخّر (أيام)</th>
                <th className="px-2 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {r.rows.map((row) => (
                <tr key={row.submissionId} className="border-b border-line last:border-0">
                  <td className="px-2 py-2 font-medium text-ink">{row.templateTitle}</td>
                  <td className="px-2 py-2 text-ink-2">{row.submitterName}</td>
                  <td className="px-2 py-2 text-ink-2">
                    {row.approverName} <span className="text-ink-3">({row.approverRoleLabel})</span>
                  </td>
                  <td className="px-2 py-2 text-ink-2">{formatDate(row.dueDate)}</td>
                  <td className="px-2 py-2">
                    <Badge tone="alert">{row.daysOverdue}</Badge>
                  </td>
                  <td className="px-2 py-2">
                    <Link to={`/app/submissions?open=${row.submissionId}`} className="font-semibold text-orange-600 hover:underline">
                      عرض
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function SalesComplianceBlock({ query }: { query: ReturnType<typeof useQuery<SalesDailyComplianceReport>> }) {
  if (query.isLoading) return <LoadingState label="يتم تحميل التزام المبيعات…" />;
  if (query.isError) return <QueryError onRetry={() => query.refetch()} description="تعذّر جلب التزام المبيعات." />;
  const r = query.data;
  if (!r) return null;

  return (
    <section>
      <h3 className="mb-3 font-semibold text-navy">التزام مندوبي المبيعات بالتقارير اليومية</h3>
      {r.reportersCount === 0 ? (
        <p className="py-4 text-center text-sm text-ink-2">لا يوجد مندوبو مبيعات (تقارير يومية) ضمن نطاقك.</p>
      ) : (
        <>
          <div className="grid grid-cols-3 gap-3">
            <StatCard label="عدد المندوبين" value={r.reportersCount} />
            <StatCard label="مكتمل الأسبوع" value={r.completeCount} tone="success" />
            <StatCard
              label="ناقص (يحتاج مراجعة)"
              value={r.incompleteCount}
              tone={r.incompleteCount > 0 ? 'alert' : 'navy'}
            />
          </div>
          {r.canViewRows && r.rows.length > 0 && (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full min-w-[560px] text-right text-sm">
                <thead className="border-b border-line text-xs text-ink-2">
                  <tr>
                    <th className="px-2 py-2 font-semibold">المندوب</th>
                    <th className="px-2 py-2 font-semibold">الفريق</th>
                    <th className="px-2 py-2 font-semibold">أيام مطلوبة</th>
                    <th className="px-2 py-2 font-semibold">أيام مُسلَّمة</th>
                    <th className="px-2 py-2 font-semibold">ناقصة</th>
                    <th className="px-2 py-2 font-semibold">أيام إجازة</th>
                    <th className="px-2 py-2 font-semibold">الحالة</th>
                  </tr>
                </thead>
                <tbody>
                  {r.rows.map((row) => (
                    <tr key={row.userId} className="border-b border-line last:border-0">
                      <td className="px-2 py-2 font-medium">
                        <Link to={`/app/employee/${row.userId}`} className="text-navy hover:text-orange-600 hover:underline">
                          {row.fullName}
                        </Link>
                      </td>
                      <td className="px-2 py-2 text-ink-2">{row.teamName ?? '—'}</td>
                      <td className="px-2 py-2 text-ink-2">{row.expectedDays}</td>
                      <td className="px-2 py-2 text-ink-2">{row.submittedDays}</td>
                      <td className="px-2 py-2 text-ink-2">{row.missingDays}</td>
                      <td className="px-2 py-2 text-ink-2">{row.leaveDays > 0 ? row.leaveDays : '—'}</td>
                      <td className="px-2 py-2">
                        <Badge tone={row.isComplete ? 'success' : 'alert'}>
                          {row.isComplete ? 'مكتمل' : 'يحتاج مراجعة'}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}

// ===== قسم تجميع KPI الدوري (§8/§10) =====
function KpiAggregateSection() {
  const [granularity, setGranularity] = useState<KpiGranularity>('Monthly');
  const [year, setYear] = useState(CURRENT_YEAR);
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [quarter, setQuarter] = useState(Math.floor(new Date().getMonth() / 3) + 1);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  // مفتاح الفترة المُشتق حسب الدقّة المختارة.
  const periodKey = useMemo(() => {
    if (granularity === 'Monthly') return monthKey(year, month);
    if (granularity === 'Quarterly') return quarterKey(year, quarter);
    if (granularity === 'Yearly') return String(year);
    return undefined;
  }, [granularity, year, month, quarter]);

  const customValid = granularity !== 'Custom' || (from !== '' && to !== '' && from <= to);

  const params =
    granularity === 'Custom'
      ? { granularity, from, to }
      : { granularity, periodKey };

  const agg = useQuery({
    queryKey: ['kpi-aggregate', granularity, periodKey ?? `${from}_${to}`],
    queryFn: async () => (await api.get<KpiAggregateDto>('/kpi-evaluations/aggregate', { params })).data,
    enabled: customValid,
  });

  return (
    <Card>
      <div className="mb-4">
        <h2 className="text-lg font-bold text-navy">متوسط مؤشرات الأداء عبر الفترات</h2>
        <p className="mt-0.5 text-sm text-ink-2">
          الأسبوع هو وحدة الأساس؛ المتوسط الشهري/الربع سنوي/السنوي يُحتسب من متوسط نتائج الأسابيع داخل المدى
          — ضمن نطاقك.
        </p>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-40">
          <Field label="الدقّة">
            <Select value={granularity} onChange={(e) => setGranularity(e.target.value as KpiGranularity)}>
              <option value="Monthly">{granularityLabel.Monthly}</option>
              <option value="Quarterly">{granularityLabel.Quarterly}</option>
              <option value="Yearly">{granularityLabel.Yearly}</option>
              <option value="Custom">{granularityLabel.Custom}</option>
            </Select>
          </Field>
        </div>

        {granularity !== 'Custom' && (
          <div className="w-32">
            <Field label="السنة">
              <Select value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {YEARS.map((y) => (
                  <option key={y} value={y}>{y}</option>
                ))}
              </Select>
            </Field>
          </div>
        )}

        {granularity === 'Monthly' && (
          <div className="w-36">
            <Field label="الشهر">
              <Select value={month} onChange={(e) => setMonth(Number(e.target.value))}>
                {monthNamesAr.map((name, i) => (
                  <option key={i} value={i + 1}>{name}</option>
                ))}
              </Select>
            </Field>
          </div>
        )}

        {granularity === 'Quarterly' && (
          <div className="w-32">
            <Field label="الربع">
              <Select value={quarter} onChange={(e) => setQuarter(Number(e.target.value))}>
                {[1, 2, 3, 4].map((q) => (
                  <option key={q} value={q}>الربع {q}</option>
                ))}
              </Select>
            </Field>
          </div>
        )}

        {granularity === 'Custom' && (
          <>
            <div className="w-40">
              <Field label="من">
                <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </Field>
            </div>
            <div className="w-40">
              <Field label="إلى">
                <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </Field>
            </div>
          </>
        )}
      </div>

      {granularity === 'Custom' && from !== '' && to !== '' && from > to && (
        <p className="mt-2 text-xs text-alert">تاريخ «من» يجب أن يسبق «إلى».</p>
      )}

      <div className="mt-4">
        {!customValid ? (
          <Alert tone="navy">اختر تاريخي البداية والنهاية لعرض المتوسط المخصّص.</Alert>
        ) : agg.isLoading ? (
          <LoadingState label="يتم احتساب المتوسط…" />
        ) : agg.isError ? (
          <QueryError onRetry={() => agg.refetch()} description="تعذّر احتساب متوسط المؤشرات." />
        ) : agg.data ? (
          <KpiAggregateResult data={agg.data} />
        ) : null}
      </div>
    </Card>
  );
}

function KpiAggregateResult({ data }: { data: KpiAggregateDto }) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-ink-2">
        {granularityLabel[data.granularity] ?? data.granularity} · {data.periodLabel}
      </p>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        <StatCard
          label="متوسط المؤشر"
          value={data.average == null ? '—' : formatPercent(data.average)}
          tone={data.average != null && data.average < 60 ? 'alert' : 'navy'}
        />
        <StatCard label="عدد الأسابيع المحتسَبة" value={data.weeksCount} />
        <StatCard label="عدد التقييمات" value={data.evaluationsCount} />
      </div>

      {!data.canViewRows ? (
        <Alert tone="navy">ملخّص فقط على هذا المستوى — لاستعراض الأسابيع التفصيلية اختر موظّفًا من ملفّه.</Alert>
      ) : data.weeks.length === 0 ? (
        <p className="py-4 text-center text-sm text-ink-2">لا توجد تقييمات أسبوعية داخل هذا المدى بعد.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[480px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الأسبوع</th>
                <th className="px-2 py-2 font-semibold">من</th>
                <th className="px-2 py-2 font-semibold">إلى</th>
                <th className="px-2 py-2 font-semibold">النتيجة</th>
                <th className="px-2 py-2 font-semibold">عدد التقييمات</th>
              </tr>
            </thead>
            <tbody>
              {data.weeks.map((w) => (
                <tr key={w.periodKey} className="border-b border-line last:border-0">
                  <td className="px-2 py-2 font-medium text-navy">{w.periodKey}</td>
                  <td className="px-2 py-2 text-ink-2">{formatDate(w.weekStart)}</td>
                  <td className="px-2 py-2 text-ink-2">{formatDate(w.weekEnd)}</td>
                  <td className="px-2 py-2 font-semibold">{formatPercent(w.score)}</td>
                  <td className="px-2 py-2 text-ink-2">{w.evaluationsCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

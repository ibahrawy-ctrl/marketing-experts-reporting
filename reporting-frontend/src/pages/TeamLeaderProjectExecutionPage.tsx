import { Fragment, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Alert, Badge, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  useExecutionByClient,
  useExecutionByEmployee,
  useExecutionByPod,
  useExecutionByProject,
  type ProjectExecutionFilter,
} from '../lib/useProjectExecution';
import { formatPeriod, formatPercent } from '../lib/format';
import {
  dateKey,
  monthKeyFor,
  operationalWeekKey,
  parseDateKey,
  quarterOf,
  riyadhToday,
} from '../lib/dashboardPeriod';
import type {
  PeriodComparison,
  PeriodType,
  ProjectExecMetrics,
  ProjectFirstByEmployeeRow,
  ProjectFirstByProjectRow,
} from '../types/api';

// RC-4 Task 4C — لوحة قائد الفريق لمتابعة تنفيذ المشاريع داخل الـPod، مبنية على محرّك تجميع 4B (قراءة فقط).
// كل الأرقام تأتي جاهزة من الخادم (تجميع + مقارنة دورية + تشخيصات). النطاق مفروض خادميًّا عبر IScopeResolver
// (قائد الفريق يرى فريقه فقط، المدير/GM/CEO أوسع). لا توسعة صلاحيات من الواجهة.

const PERIOD_TYPES: PeriodType[] = ['Weekly', 'Monthly', 'Quarterly'];
const PERIOD_TYPE_LABEL: Record<string, string> = {
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
};

function num(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return Number(value).toLocaleString('ar-EG', { maximumFractionDigits: 2 });
}

const EMPTY_METRICS: ProjectExecMetrics = {
  planned: 0, completed: 0, approved: 0, revisions: 0, published: 0, delayed: 0,
  messagesIn: 0, responses: 0, issueComments: 0, escalations: 0,
  completionRate: 0, approvalRate: 0, publishRate: 0, responseRate: 0,
};

// جمع المقاييس عبر صفوف؛ المعدّلات تُشتَقّ من المجاميع (قسمة آمنة) لا تُجمَع.
function sumMetrics(rows: { metrics: ProjectExecMetrics }[]): ProjectExecMetrics {
  const t = { ...EMPTY_METRICS };
  for (const r of rows) {
    t.planned += r.metrics.planned;
    t.completed += r.metrics.completed;
    t.approved += r.metrics.approved;
    t.revisions += r.metrics.revisions;
    t.published += r.metrics.published;
    t.delayed += r.metrics.delayed;
    t.messagesIn += r.metrics.messagesIn;
    t.responses += r.metrics.responses;
    t.issueComments += r.metrics.issueComments;
    t.escalations += r.metrics.escalations;
  }
  const pct = (a: number, b: number) => (b > 0 ? Math.round((a / b) * 1000) / 10 : 0);
  t.completionRate = pct(t.completed, t.planned);
  t.approvalRate = pct(t.approved, t.completed);
  t.publishRate = pct(t.published, t.approved);
  t.responseRate = pct(t.responses, t.messagesIn);
  return t;
}

// دمج مقارنات دورية متعددة (Pod/موظّف) في مقارنة واحدة على مقياس المخرجات (Completed+Responses).
function combineComparisons(list: (PeriodComparison | null | undefined)[]): PeriodComparison {
  let current = 0;
  let previous = 0;
  let hasPrevious = false;
  for (const c of list) {
    if (!c) continue;
    current += c.current;
    if (c.hasPrevious) {
      previous += c.previous;
      hasPrevious = true;
    }
  }
  if (!hasPrevious) return { current, previous: 0, change: 0, changePercent: null, trend: 'none', hasPrevious: false };
  const change = current - previous;
  const changePercent = previous !== 0 ? Math.round((change / Math.abs(previous)) * 1000) / 10 : null;
  const trend = change > 0.0001 ? 'up' : change < -0.0001 ? 'down' : 'stable';
  return { current, previous, change, changePercent, trend, hasPrevious: true };
}

// شارة الاتجاه من مقارنة الخادم. لا مقارنة (hasPrevious=false) ⇒ نصّ صريح بدل صفر مضلِّل.
function TrendBadge({ comparison }: { comparison: PeriodComparison | null | undefined }) {
  if (!comparison || !comparison.hasPrevious) {
    return <span className="text-xs text-ink-2">لا توجد بيانات سابقة</span>;
  }
  const { trend, change, changePercent } = comparison;
  const tone = trend === 'up' ? 'text-green-700' : trend === 'down' ? 'text-red-700' : 'text-ink-2';
  const arrow = trend === 'up' ? '▲' : trend === 'down' ? '▼' : '—';
  const detail =
    changePercent === null
      ? `${change >= 0 ? '+' : ''}${change.toLocaleString('ar-EG', { maximumFractionDigits: 1 })}`
      : `${changePercent >= 0 ? '+' : ''}${changePercent.toLocaleString('ar-EG', { maximumFractionDigits: 1 })}٪`;
  return (
    <span className={`text-xs font-semibold ${tone}`} title="مقابل الفترة السابقة">
      {arrow} {detail}
    </span>
  );
}

// كشف الاختناقات بقواعد صريحة (لا ذكاء اصطناعي): إنجاز منخفض/مراجعات مرتفعة/تصعيدات/اعتماد أقل بكثير/بلا تقدّم.
type Health = { level: 'good' | 'watch' | 'stuck'; reasons: string[] };
function assessHealth(m: ProjectExecMetrics, comparison: PeriodComparison | null | undefined): Health {
  const reasons: string[] = [];
  const hasOutput = m.completed > 0 || m.responses > 0;
  if (m.completed > 0 && m.completionRate < 50) reasons.push('نسبة إنجاز منخفضة');
  if (m.completed > 0 && m.revisions > m.completed) reasons.push('مراجعات مرتفعة');
  if (m.completed > 0 && m.approvalRate < 50) reasons.push('اعتماد أقل بكثير من المنجَز');
  const escalations = m.escalations + m.issueComments;
  if (escalations > 0) reasons.push('مشاكل/تصعيدات مفتوحة');
  if (comparison?.hasPrevious && comparison.change <= 0 && hasOutput) reasons.push('لا تقدّم مقابل الأسبوع السابق');
  const critical = m.escalations > 0 || reasons.length >= 3;
  if (critical) return { level: 'stuck', reasons };
  if (reasons.length >= 1) return { level: 'watch', reasons };
  return { level: 'good', reasons };
}

const HEALTH_LABEL: Record<Health['level'], string> = { good: 'جيد', watch: 'يحتاج متابعة', stuck: 'متعثر' };
const HEALTH_TONE: Record<Health['level'], 'success' | 'gold' | 'alert'> = {
  good: 'success', watch: 'gold', stuck: 'alert',
};

function HealthBadge({ health }: { health: Health }) {
  return (
    <span title={health.reasons.join(' · ') || 'لا مؤشّرات تعثّر'}>
      <Badge tone={HEALTH_TONE[health.level]}>{HEALTH_LABEL[health.level]}</Badge>
    </span>
  );
}

// شريط تقدّم صغير لمعدّل مئوي (0–100) — لتمثيل مراحل خطّ الإنتاج/المودريشن داخل المشروع.
function MiniBar({ label, value }: { label: string; value: number }) {
  const clamped = Math.max(0, Math.min(100, value));
  return (
    <div className="min-w-[7rem]">
      <div className="flex items-center justify-between text-[11px] text-ink-2">
        <span>{label}</span>
        <span>{formatPercent(value)}</span>
      </div>
      <div className="mt-0.5 h-1.5 w-full rounded-full bg-line">
        <div className="h-1.5 rounded-full bg-navy" style={{ width: `${clamped}%` }} />
      </div>
    </div>
  );
}

// بطاقة مؤشّر KPI رئيسية.
function KpiCard({ label, value, tone = 'navy', children }: {
  label: string; value: string; tone?: 'navy' | 'alert' | 'gold'; children?: ReactNode;
}) {
  const valueTone = tone === 'alert' ? 'text-alert' : tone === 'gold' ? 'text-gold' : 'text-navy';
  return (
    <div className="rounded-xl border border-line bg-white p-4">
      <p className="text-sm text-ink-2">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${valueTone}`}>{value}</p>
      {children ? <div className="mt-1">{children}</div> : null}
    </div>
  );
}

// إجمالي المخرجات = المنجَز + الردود (مقياس المخرجات الرئيسي المطابق للخادم).
function totalOutput(m: ProjectExecMetrics): number {
  return m.completed + m.responses;
}

// مزيج المساهمة: إنتاج (منجَز) مقابل مودريشن (ردود) كنسبة.
function contributionMix(m: ProjectExecMetrics): string {
  const prod = m.completed;
  const mod = m.responses;
  const total = prod + mod;
  if (total <= 0) return '—';
  const p = Math.round((prod / total) * 100);
  return `إنتاج ${p}٪ · مودريشن ${100 - p}٪`;
}

export default function TeamLeaderProjectExecutionPage() {
  const today = useMemo(() => riyadhToday(), []);
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');
  const [weeklyDate, setWeeklyDate] = useState<string>(() => dateKey(today));
  const [monthValue, setMonthValue] = useState<string>(() => monthKeyFor(today));
  const [quarterYear, setQuarterYear] = useState<number>(() => today.getUTCFullYear());
  const [quarterNum, setQuarterNum] = useState<number>(() => quarterOf(today));
  const [diagOpen, setDiagOpen] = useState(false);
  // المشروع المفتوح للتفصيل (Drill-down) — الاسم للعرض، والمعرّف لتضييق الفلتر خادميًّا.
  const [drillProject, setDrillProject] = useState<{ id: string; name: string; client: string } | null>(null);

  const periodKey = useMemo(() => {
    switch (periodType) {
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
  }, [periodType, weeklyDate, monthValue, quarterYear, quarterNum]);

  const filter = useMemo<ProjectExecutionFilter>(
    () => ({ periodType, periodKey }),
    [periodType, periodKey],
  );

  const projectQ = useExecutionByProject(filter);
  const podQ = useExecutionByPod(filter);
  const clientQ = useExecutionByClient(filter);
  const employeeQ = useExecutionByEmployee(filter);
  // تفصيل المشروع — تجميع الموظّفين المساهمين فيه (نطاق خادميّ).
  const drillFilter = useMemo<ProjectExecutionFilter>(
    () => ({ periodType, periodKey, projectId: drillProject?.id }),
    [periodType, periodKey, drillProject],
  );
  const drillQ = useExecutionByEmployee(drillFilter, !!drillProject);

  const isLoading = projectQ.isLoading || podQ.isLoading || clientQ.isLoading || employeeQ.isLoading;
  const isError = projectQ.isError || podQ.isError || clientQ.isError || employeeQ.isError;
  function retryAll() {
    projectQ.refetch();
    podQ.refetch();
    clientQ.refetch();
    employeeQ.refetch();
  }

  const projects = projectQ.data?.rows ?? [];
  const pods = podQ.data?.rows ?? [];
  const clients = clientQ.data?.rows ?? [];
  const employeeRows = employeeQ.data?.rows ?? [];

  // مؤشّرات KPI للفريق.
  const totals = useMemo(() => sumMetrics(projects), [projects]);
  const activeProjects = useMemo(() => {
    const active = clients.reduce((s, c) => s + (c.activeProjectCount ?? 0), 0);
    return active > 0 || clients.length > 0 ? active : projects.length;
  }, [clients, projects]);
  const participatingEmployees = useMemo(() => {
    const fromPods = pods.reduce((s, p) => s + (p.employeeCount ?? 0), 0);
    if (fromPods > 0) return fromPods;
    return new Set(employeeRows.map((e) => e.employeeId)).size;
  }, [pods, employeeRows]);
  const teamComparison = useMemo(() => combineComparisons(pods.map((p) => p.comparison)), [pods]);
  const totalIssues = totals.issueComments + totals.escalations;

  // قسم أداء الموظّفين — تجميع صفوف (موظّف، مشروع) لكل موظّف.
  const employeeAgg = useMemo(() => {
    const map = new Map<string, {
      employeeId: string; employeeName: string; teamName: string;
      projectIds: Set<string>; metrics: ProjectExecMetrics; comparisons: (PeriodComparison | null)[];
    }>();
    for (const r of employeeRows) {
      let e = map.get(r.employeeId);
      if (!e) {
        e = { employeeId: r.employeeId, employeeName: r.employeeName, teamName: r.teamName,
          projectIds: new Set(), metrics: { ...EMPTY_METRICS }, comparisons: [] };
        map.set(r.employeeId, e);
      }
      e.projectIds.add(r.projectId);
      e.metrics = sumMetrics([{ metrics: e.metrics }, r]);
      e.comparisons.push(r.comparison);
    }
    return Array.from(map.values())
      .map((e) => ({ ...e, comparison: combineComparisons(e.comparisons) }))
      .sort((a, b) => totalOutput(b.metrics) - totalOutput(a.metrics));
  }, [employeeRows]);

  const periodLabel = periodType === 'Weekly' ? formatPeriod(periodKey) : periodKey ?? '—';

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">لوحة تنفيذ مشاريع الفريق</h1>
          <p className="mt-1 text-sm text-ink-2">
            متابعة تنفيذ المشاريع داخل فريقك (Pod) — المخرجات والاعتماد والمراجعات والاختناقات. النطاق يُحدَّد تلقائيًّا حسب صلاحيتك.
          </p>
        </div>
        <div className="text-sm text-ink-2">الفترة: <span className="font-semibold text-navy">{periodLabel}</span></div>
      </div>

      {/* منتقي الفترة */}
      <Card>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="نوع الفترة">
            <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
              {PERIOD_TYPES.map((p) => (
                <option key={p} value={p}>{PERIOD_TYPE_LABEL[p]}</option>
              ))}
            </Select>
          </Field>
          {periodType === 'Weekly' && (
            <Field label="الأسبوع" help="اختر أي يوم داخل الأسبوع التشغيلي (الخميس → الأربعاء).">
              <Input type="date" value={weeklyDate} onChange={(e) => setWeeklyDate(e.target.value)} />
            </Field>
          )}
          {periodType === 'Monthly' && (
            <Field label="الشهر">
              <Input type="month" value={monthValue} onChange={(e) => setMonthValue(e.target.value)} />
            </Field>
          )}
          {periodType === 'Quarterly' && (
            <>
              <Field label="السنة">
                <Input type="number" value={quarterYear} onChange={(e) => setQuarterYear(Number(e.target.value))} />
              </Field>
              <Field label="الربع">
                <Select value={quarterNum} onChange={(e) => setQuarterNum(Number(e.target.value))}>
                  {[1, 2, 3, 4].map((q) => (
                    <option key={q} value={q}>{`الربع ${q}`}</option>
                  ))}
                </Select>
              </Field>
            </>
          )}
        </div>
      </Card>

      {isLoading && <LoadingState label="يتم تحميل بيانات تنفيذ الفريق…" />}
      {isError && !isLoading && <QueryError onRetry={retryAll} />}

      {!isLoading && !isError && (
        <>
          {/* بطاقات KPI للفريق */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <KpiCard label="المشاريع النشطة" value={num(activeProjects)} />
            <KpiCard label="إجمالي المنجَز" value={num(totals.completed)} />
            <KpiCard label="إجمالي المعتمَد" value={num(totals.approved)} />
            <KpiCard label="إجمالي المراجعات" value={num(totals.revisions)} tone={totals.revisions > 0 ? 'gold' : 'navy'} />
            <KpiCard label="المشاكل/التصعيدات" value={num(totalIssues)} tone={totalIssues > 0 ? 'alert' : 'navy'} />
            <KpiCard label="الموظّفون المشاركون" value={num(participatingEmployees)} />
            <KpiCard label="اتجاه الأداء" value={num(teamComparison.current)}>
              <TrendBadge comparison={teamComparison} />
            </KpiCard>
            <KpiCard label="نسبة الإنجاز الكلّية" value={formatPercent(totals.completionRate)} />
          </div>

          {/* المقارنة الأسبوعية للفريق */}
          <Card>
            <h2 className="text-lg font-semibold text-navy">المقارنة مع الفترة السابقة</h2>
            <div className="mt-3 grid gap-4 sm:grid-cols-3">
              <div>
                <p className="text-sm text-ink-2">إجمالي المخرجات (الحالية)</p>
                <p className="mt-1 text-2xl font-bold text-navy">{num(teamComparison.current)}</p>
              </div>
              <div>
                <p className="text-sm text-ink-2">إجمالي المخرجات (السابقة)</p>
                <p className="mt-1 text-2xl font-bold text-ink-2">
                  {teamComparison.hasPrevious ? num(teamComparison.previous) : '—'}
                </p>
              </div>
              <div>
                <p className="text-sm text-ink-2">التغيّر</p>
                <p className="mt-1"><TrendBadge comparison={teamComparison} /></p>
              </div>
            </div>
          </Card>

          {/* جدول تقدّم المشاريع */}
          <Card>
            <h2 className="text-lg font-semibold text-navy">تقدّم المشاريع</h2>
            {projects.length === 0 ? (
              <div className="mt-3">
                <EmptyState
                  title="لا توجد مشاريع بها نشاط خلال هذه الفترة"
                  description="لم تُرصَد مدخلات تنفيذ ضمن نطاق فريقك لهذه الفترة. جرّب فترة أخرى أو تأكّد من تسليم تقارير التنفيذ."
                />
              </div>
            ) : (
              <div className="mt-3 overflow-x-auto">
                <table className="w-full min-w-[62rem] text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs text-ink-2">
                      <th className="px-3 py-2 text-right">العميل</th>
                      <th className="px-3 py-2 text-right">المشروع</th>
                      <th className="px-3 py-2 text-center">إجمالي المخرجات</th>
                      <th className="px-3 py-2 text-center">المعتمَد</th>
                      <th className="px-3 py-2 text-center">المراجعات</th>
                      <th className="px-3 py-2 text-center">مشاكل/تصعيدات</th>
                      <th className="px-3 py-2 text-center">المساهمون</th>
                      <th className="px-3 py-2 text-center">الاتجاه</th>
                      <th className="px-3 py-2 text-center">الحالة</th>
                      <th className="px-3 py-2 text-right">مراحل التنفيذ</th>
                    </tr>
                  </thead>
                  <tbody>
                    {projects.map((r: ProjectFirstByProjectRow) => {
                      const health = assessHealth(r.metrics, r.comparison);
                      const issues = r.metrics.issueComments + r.metrics.escalations;
                      const isOpen = drillProject?.id === r.projectId;
                      return (
                        <Fragment key={r.projectId}>
                          <tr
                            className={`cursor-pointer border-b border-line/60 hover:bg-navy-50 ${isOpen ? 'bg-navy-50' : ''}`}
                            onClick={() =>
                              setDrillProject(isOpen ? null : { id: r.projectId, name: r.projectName, client: r.clientName })
                            }
                          >
                            <td className="px-3 py-2">{r.clientName || '—'}</td>
                            <td className="px-3 py-2 font-medium text-navy">{r.projectName}</td>
                            <td className="px-3 py-2 text-center">{num(totalOutput(r.metrics))}</td>
                            <td className="px-3 py-2 text-center">{num(r.metrics.approved)}</td>
                            <td className="px-3 py-2 text-center">{num(r.metrics.revisions)}</td>
                            <td className={`px-3 py-2 text-center ${issues > 0 ? 'font-semibold text-alert' : ''}`}>{num(issues)}</td>
                            <td className="px-3 py-2 text-center">{num(r.contributors)}</td>
                            <td className="px-3 py-2 text-center"><TrendBadge comparison={r.comparison} /></td>
                            <td className="px-3 py-2 text-center"><HealthBadge health={health} /></td>
                            <td className="px-3 py-2">
                              <div className="flex flex-wrap gap-3">
                                <MiniBar label="الإنجاز" value={r.metrics.completionRate} />
                                <MiniBar label="الاعتماد" value={r.metrics.approvalRate} />
                                <MiniBar label="النشر" value={r.metrics.publishRate} />
                                {r.metrics.messagesIn > 0 && <MiniBar label="الردود" value={r.metrics.responseRate} />}
                              </div>
                            </td>
                          </tr>
                          {isOpen && (
                            <tr className="border-b border-line bg-offwhite">
                              <td colSpan={10} className="px-3 py-4">
                                <ProjectDrilldown
                                  projectName={r.projectName}
                                  clientName={r.clientName}
                                  isLoading={drillQ.isLoading}
                                  isError={drillQ.isError}
                                  onRetry={() => drillQ.refetch()}
                                  rows={drillQ.data?.rows ?? []}
                                />
                              </td>
                            </tr>
                          )}
                        </Fragment>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          {/* قسم أداء الموظّفين */}
          <Card>
            <h2 className="text-lg font-semibold text-navy">أداء الموظّفين</h2>
            {employeeAgg.length === 0 ? (
              <div className="mt-3">
                <EmptyState title="لا توجد مساهمات موظّفين خلال هذه الفترة" />
              </div>
            ) : (
              <div className="mt-3 overflow-x-auto">
                <table className="w-full min-w-[52rem] text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs text-ink-2">
                      <th className="px-3 py-2 text-right">الموظّف</th>
                      <th className="px-3 py-2 text-center">المشاريع</th>
                      <th className="px-3 py-2 text-center">المنجَز</th>
                      <th className="px-3 py-2 text-center">المعتمَد</th>
                      <th className="px-3 py-2 text-center">المراجعات</th>
                      <th className="px-3 py-2 text-center">مشاكل/تصعيدات</th>
                      <th className="px-3 py-2 text-center">الاتجاه</th>
                      <th className="px-3 py-2 text-right">مزيج المساهمة</th>
                    </tr>
                  </thead>
                  <tbody>
                    {employeeAgg.map((e) => {
                      const issues = e.metrics.issueComments + e.metrics.escalations;
                      return (
                        <tr key={e.employeeId} className="border-b border-line/60">
                          <td className="px-3 py-2 font-medium text-navy">{e.employeeName || '—'}</td>
                          <td className="px-3 py-2 text-center">{num(e.projectIds.size)}</td>
                          <td className="px-3 py-2 text-center">{num(e.metrics.completed)}</td>
                          <td className="px-3 py-2 text-center">{num(e.metrics.approved)}</td>
                          <td className="px-3 py-2 text-center">{num(e.metrics.revisions)}</td>
                          <td className={`px-3 py-2 text-center ${issues > 0 ? 'font-semibold text-alert' : ''}`}>{num(issues)}</td>
                          <td className="px-3 py-2 text-center"><TrendBadge comparison={e.comparison} /></td>
                          <td className="px-3 py-2 text-xs text-ink-2">{contributionMix(e.metrics)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          {/* لوحة التشخيصات (قابلة للطي) */}
          <Card>
            <button
              type="button"
              onClick={() => setDiagOpen((v) => !v)}
              className="flex w-full items-center justify-between text-right"
              aria-expanded={diagOpen}
            >
              <span className="text-sm font-semibold text-navy">تشخيصات التجميع (للمراجعة)</span>
              <span className="text-xs text-ink-2">{diagOpen ? 'إخفاء' : 'عرض'}</span>
            </button>
            {diagOpen && (
              <div className="mt-3 space-y-3">
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 text-sm">
                  <DiagItem label="تسليمات مفحوصة" value={projectQ.data?.submissionsConsidered ?? 0} />
                  <DiagItem label="تسليمات مُتجاهَلة" value={projectQ.data?.submissionsIgnored ?? 0} />
                  <DiagItem label="صفوف مفحوصة" value={projectQ.data?.rowsConsidered ?? 0} />
                  <DiagItem label="صفوف مُتجاهَلة" value={projectQ.data?.rowsIgnored ?? 0} />
                </div>
                {projectQ.data?.ignoredReasons && Object.keys(projectQ.data.ignoredReasons).length > 0 ? (
                  <div>
                    <p className="text-xs font-semibold text-ink-2">أسباب التجاهل:</p>
                    <ul className="mt-1 space-y-0.5 text-xs text-ink-2">
                      {Object.entries(projectQ.data.ignoredReasons).map(([k, v]) => (
                        <li key={k}>{k}: <span className="font-semibold">{num(v)}</span></li>
                      ))}
                    </ul>
                  </div>
                ) : (
                  <p className="text-xs text-ink-2">لا توجد صفوف مُتجاهَلة ضمن النطاق.</p>
                )}
              </div>
            )}
          </Card>
        </>
      )}
    </div>
  );
}

function DiagItem({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-line bg-offwhite px-3 py-2">
      <p className="text-xs text-ink-2">{label}</p>
      <p className="mt-0.5 text-lg font-bold text-navy">{value.toLocaleString('ar-EG')}</p>
    </div>
  );
}

// تفصيل المشروع — الموظّفون المساهمون فيه ومقاييس كلٍّ منهم + المقارنة الدورية.
function ProjectDrilldown({
  projectName, clientName, isLoading, isError, onRetry, rows,
}: {
  projectName: string; clientName: string; isLoading: boolean; isError: boolean;
  onRetry: () => void; rows: ProjectFirstByEmployeeRow[];
}) {
  if (isLoading) return <LoadingState label="يتم تحميل مساهمي المشروع…" />;
  if (isError) return <QueryError onRetry={onRetry} />;
  return (
    <div className="space-y-3">
      <p className="text-sm font-semibold text-navy">
        مساهمو مشروع «{projectName}»{clientName ? ` — ${clientName}` : ''}
      </p>
      {rows.length === 0 ? (
        <Alert tone="navy">لا توجد مساهمات موظّفين مرصودة لهذا المشروع في هذه الفترة.</Alert>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[44rem] text-sm">
            <thead>
              <tr className="border-b border-line text-xs text-ink-2">
                <th className="px-3 py-2 text-right">الموظّف</th>
                <th className="px-3 py-2 text-right">نوع المساهمة</th>
                <th className="px-3 py-2 text-center">المنجَز</th>
                <th className="px-3 py-2 text-center">المعتمَد</th>
                <th className="px-3 py-2 text-center">المراجعات</th>
                <th className="px-3 py-2 text-center">مشاكل/تصعيدات</th>
                <th className="px-3 py-2 text-center">الاتجاه</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => {
                const issues = r.metrics.issueComments + r.metrics.escalations;
                const kind = r.metrics.messagesIn > 0 && r.metrics.completed === 0 ? 'مودريشن' : 'إنتاج';
                return (
                  <tr key={r.employeeId} className="border-b border-line/60">
                    <td className="px-3 py-2 font-medium text-navy">{r.employeeName || '—'}</td>
                    <td className="px-3 py-2 text-xs text-ink-2">{kind}</td>
                    <td className="px-3 py-2 text-center">{num(r.metrics.completed)}</td>
                    <td className="px-3 py-2 text-center">{num(r.metrics.approved)}</td>
                    <td className="px-3 py-2 text-center">{num(r.metrics.revisions)}</td>
                    <td className={`px-3 py-2 text-center ${issues > 0 ? 'font-semibold text-alert' : ''}`}>{num(issues)}</td>
                    <td className="px-3 py-2 text-center"><TrendBadge comparison={r.comparison} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

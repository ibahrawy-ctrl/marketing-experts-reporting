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
  previousPeriodKey,
  quarterOf,
  riyadhToday,
} from '../lib/dashboardPeriod';
import type {
  PeriodType,
  ProjectExecMetrics,
  ProjectFirstByClientRow,
  ProjectFirstByEmployeeRow,
  ProjectFirstByPodRow,
  ProjectFirstByProjectRow,
} from '../types/api';

// عرض تقارير التنفيذ Project-First (RC-4 Task 4، Path A) — قراءة فقط. كل الأرقام داخل المشاريع،
// والنطاق مفروض خادميًّا: قائد الفريق يرى Pod فريقه، والمدير/GM/CEO يرون أعلى-لأسفل مع Drill-down
// (Pod → عميل → مشروع → موظّف)، ومدير الحساب يرى مشاريع عملائه (Phase F).
type ExecView = 'pod' | 'client' | 'project' | 'employee';

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

// جمع المقاييس عبر الصفوف. المعدّلات لا تُجمَع بل تُشتَقّ من المجاميع (قسمة آمنة).
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

// شارة الفرق مقابل الفترة السابقة. previous غير معرّف ⇒ لا مقارنة ذات معنى (تجنّبًا لصفر مضلِّل).
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
    return <span className="text-xs text-ink-2">لا توجد بيانات كافية للمقارنة</span>;
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

function MetricsRow({ m }: { m: ProjectExecMetrics }) {
  return (
    <>
      <td className="px-3 py-2 text-center">{num(m.planned)}</td>
      <td className="px-3 py-2 text-center">{num(m.completed)}</td>
      <td className="px-3 py-2 text-center">{num(m.approved)}</td>
      <td className="px-3 py-2 text-center">{num(m.revisions)}</td>
      <td className="px-3 py-2 text-center">{num(m.published)}</td>
      <td className="px-3 py-2 text-center">{num(m.delayed)}</td>
      <td className="px-3 py-2 text-center">{formatPercent(m.completionRate)}</td>
      <td className="px-3 py-2 text-center">{formatPercent(m.approvalRate)}</td>
      <td className="px-3 py-2 text-center">{formatPercent(m.publishRate)}</td>
    </>
  );
}

const METRIC_HEADERS = ['مخطّط', 'مكتمل', 'معتمَد', 'مراجعات', 'منشور', 'متأخّر', 'الإنجاز', 'الاعتماد', 'النشر'];

// بطاقة مؤشّر مع شارة الفرق (StatCard الأساسي لا يقبل children).
function MetricCard({ label, value, children }: { label: string; value: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-line bg-white p-4">
      <p className="text-sm text-ink-2">{label}</p>
      <p className="mt-1 text-2xl font-bold text-navy">{value}</p>
      <div className="mt-1">{children}</div>
    </div>
  );
}

export default function TeamLeaderExecutionPage() {
  const today = useMemo(() => riyadhToday(), []);
  const [view, setView] = useState<ExecView>('project');
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');
  const [weeklyDate, setWeeklyDate] = useState<string>(() => dateKey(today));
  const [monthValue, setMonthValue] = useState<string>(() => monthKeyFor(today));
  const [quarterYear, setQuarterYear] = useState<number>(() => today.getUTCFullYear());
  const [quarterNum, setQuarterNum] = useState<number>(() => quarterOf(today));
  // حالة التنقّل أعلى-لأسفل (Drill-down). كل مستوى يضيّق الفلتر خادميًّا؛ الاسم للعرض في مسار التنقّل فقط.
  const [drillTeam, setDrillTeam] = useState<{ id: string; name: string } | null>(null);
  const [drillClient, setDrillClient] = useState<{ id: string; name: string } | null>(null);
  const [drillProject, setDrillProject] = useState<{ id: string; name: string } | null>(null);

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
    () => ({
      periodType,
      periodKey,
      teamId: drillTeam?.id,
      clientId: drillClient?.id,
      projectId: drillProject?.id,
    }),
    [periodType, periodKey, drillTeam, drillClient, drillProject],
  );
  const prevFilter = useMemo<ProjectExecutionFilter>(
    () => ({
      periodType,
      periodKey: previousPeriodKey(periodType, periodKey),
      teamId: drillTeam?.id,
      clientId: drillClient?.id,
      projectId: drillProject?.id,
    }),
    [periodType, periodKey, drillTeam, drillClient, drillProject],
  );

  const byPod = useExecutionByPod(filter, view === 'pod');
  const byClient = useExecutionByClient(filter, view === 'client');
  const byProject = useExecutionByProject(filter);
  const byProjectPrev = useExecutionByProject(prevFilter);
  const byEmployee = useExecutionByEmployee(filter, view === 'employee');

  const active =
    view === 'pod' ? byPod : view === 'client' ? byClient : view === 'employee' ? byEmployee : byProject;

  const currentTotals = useMemo(() => sumMetrics(byProject.data?.rows ?? []), [byProject.data]);
  // «بيانات كافية للمقارنة» = الفترة السابقة فيها صفوف فعلية. وإلا لا نعرض صفرًا مضلِّلًا.
  const hasPrev = (byProjectPrev.data?.rows?.length ?? 0) > 0;
  const prevTotals = hasPrev ? sumMetrics(byProjectPrev.data!.rows) : undefined;

  // النزول من Pod: نضيّق على الفريق ثم ننتقل لعرض المشاريع.
  const drillIntoPod = (row: ProjectFirstByPodRow) => {
    if (!row.teamId) return;
    setDrillTeam({ id: row.teamId, name: row.teamName || 'فريق بلا اسم' });
    setDrillClient(null);
    setDrillProject(null);
    setView('project');
  };
  // النزول من عميل: نضيّق على العميل ثم ننتقل لعرض المشاريع.
  const drillIntoClient = (row: ProjectFirstByClientRow) => {
    if (!row.clientId) return;
    setDrillClient({ id: row.clientId, name: row.clientName || 'عميل بلا اسم' });
    setDrillProject(null);
    setView('project');
  };
  // النزول من مشروع: نضيّق على المشروع ثم ننتقل لعرض الموظّفين.
  const drillIntoProject = (row: ProjectFirstByProjectRow) => {
    setDrillProject({ id: row.projectId, name: row.projectName || 'مشروع بلا اسم' });
    setView('employee');
  };

  const hasDrill = drillTeam || drillClient || drillProject;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">تقارير التنفيذ (حسب المشروع)</h1>
        <p className="mt-1 text-sm text-ink-2">
          عرض تجميعي للتنفيذ (محتوى/تصميم/فيديو/مديرشن) — كل الأرقام مأخوذة من داخل مشاريع تقارير الموظّفين
          المعتمَدة، مجمَّعة حسب Pod أو العميل أو المشروع أو الموظّف، مع تنقّل أعلى-لأسفل ومقارنة الفترة الحالية بالسابقة.
        </p>
      </div>

      <Alert tone="navy">
        هذا العرض قراءة فقط ويلتزم بنطاق صلاحيتك: قائد الفريق يرى Pod فريقه، والمدير/GM/CEO يرون نطاقهم كاملًا،
        ومدير الحساب يرى مشاريع عملائه فقط. لا حساب أو صرف لأي مستحقات.
      </Alert>

      <Card className="space-y-4">
        <div className="flex flex-wrap items-end gap-4">
          <Field label="نوع الفترة">
            <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
              {PERIOD_TYPES.map((p) => (
                <option key={p} value={p}>{PERIOD_TYPE_LABEL[p]}</option>
              ))}
            </Select>
          </Field>
          {periodType === 'Weekly' && (
            <Field label="أسبوع (اختر أيّ يوم داخله)">
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
                    <option key={q} value={q}>الربع {q}</option>
                  ))}
                </Select>
              </Field>
            </>
          )}
          <Field label="طريقة العرض">
            <Select value={view} onChange={(e) => setView(e.target.value as ExecView)}>
              <option value="pod">حسب Pod (الفريق)</option>
              <option value="client">حسب العميل</option>
              <option value="project">حسب المشروع</option>
              <option value="employee">حسب الموظّف (تفصيل)</option>
            </Select>
          </Field>
          <Badge tone="navy">الفترة: {formatPeriod(periodKey)}</Badge>
        </div>

        {/* مسار التنقّل أعلى-لأسفل — كل عنصر قابل للإزالة لإرجاع الفلتر. */}
        {hasDrill && (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="text-ink-2">التنقّل:</span>
            {drillTeam && (
              <button
                type="button"
                onClick={() => setDrillTeam(null)}
                className="rounded-full border border-line bg-cloud/50 px-3 py-1 text-navy hover:bg-cloud"
              >
                Pod: {drillTeam.name} ✕
              </button>
            )}
            {drillClient && (
              <button
                type="button"
                onClick={() => setDrillClient(null)}
                className="rounded-full border border-line bg-cloud/50 px-3 py-1 text-navy hover:bg-cloud"
              >
                عميل: {drillClient.name} ✕
              </button>
            )}
            {drillProject && (
              <button
                type="button"
                onClick={() => setDrillProject(null)}
                className="rounded-full border border-line bg-cloud/50 px-3 py-1 text-navy hover:bg-cloud"
              >
                مشروع: {drillProject.name} ✕
              </button>
            )}
            <button
              type="button"
              onClick={() => {
                setDrillTeam(null);
                setDrillClient(null);
                setDrillProject(null);
              }}
              className="text-red-700 hover:underline"
            >
              مسح الكل
            </button>
          </div>
        )}
      </Card>

      {/* لوحة مقارنة الفترة الحالية بالسابقة */}
      <Card className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-bold text-navy">مقارنة مع الفترة السابقة</h2>
          <Badge tone={hasPrev ? 'navy' : 'muted'}>
            {hasPrev ? `مقابل ${formatPeriod(byProjectPrev.data?.periodKey)}` : 'لا توجد بيانات كافية للمقارنة'}
          </Badge>
        </div>
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
          <MetricCard label="مكتمل" value={num(currentTotals.completed)}>
            <DeltaBadge current={currentTotals.completed} previous={prevTotals?.completed} />
          </MetricCard>
          <MetricCard label="معتمَد" value={num(currentTotals.approved)}>
            <DeltaBadge current={currentTotals.approved} previous={prevTotals?.approved} />
          </MetricCard>
          <MetricCard label="منشور" value={num(currentTotals.published)}>
            <DeltaBadge current={currentTotals.published} previous={prevTotals?.published} />
          </MetricCard>
          <MetricCard label="نسبة الإنجاز" value={formatPercent(currentTotals.completionRate)}>
            <DeltaBadge current={currentTotals.completionRate} previous={prevTotals?.completionRate} kind="percentPoints" />
          </MetricCard>
          <MetricCard label="نسبة الاعتماد" value={formatPercent(currentTotals.approvalRate)}>
            <DeltaBadge current={currentTotals.approvalRate} previous={prevTotals?.approvalRate} kind="percentPoints" />
          </MetricCard>
          <MetricCard label="نسبة النشر" value={formatPercent(currentTotals.publishRate)}>
            <DeltaBadge current={currentTotals.publishRate} previous={prevTotals?.publishRate} kind="percentPoints" />
          </MetricCard>
        </div>
      </Card>

      {active.isLoading ? (
        <LoadingState />
      ) : active.isError ? (
        <QueryError onRetry={() => active.refetch()} />
      ) : view === 'pod' ? (
        <PodTable rows={byPod.data?.rows ?? []} onDrill={drillIntoPod} />
      ) : view === 'client' ? (
        <ClientTable rows={byClient.data?.rows ?? []} onDrill={drillIntoClient} />
      ) : view === 'project' ? (
        <ProjectTable rows={byProject.data?.rows ?? []} onDrill={drillIntoProject} />
      ) : (
        <EmployeeTable rows={byEmployee.data?.rows ?? []} />
      )}
    </div>
  );
}

// جدول Pod (الفريق) — صفوف قابلة للنقر للنزول إلى مشاريع الفريق.
function PodTable({
  rows,
  onDrill,
}: {
  rows: ProjectFirstByPodRow[];
  onDrill: (row: ProjectFirstByPodRow) => void;
}) {
  if (rows.length === 0) {
    return <EmptyState title="لا توجد بيانات تنفيذ" description="لا توجد فرق بتقارير معتمَدة ضمن هذه الفترة والنطاق." />;
  }
  return (
    <Card className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-line text-ink-2">
            <th className="px-3 py-2 text-right">Pod (الفريق)</th>
            {METRIC_HEADERS.map((h) => (
              <th key={h} className="px-3 py-2 text-center">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.teamId ?? 'no-team'}
              className={`border-b border-line/60 ${r.teamId ? 'cursor-pointer hover:bg-cloud/40' : ''}`}
              onClick={() => r.teamId && onDrill(r)}
            >
              <td className="px-3 py-2 font-semibold text-navy">
                {r.teamName || '—'}
                {r.teamId && <span className="mr-1 text-xs text-ink-2">↩ تفصيل</span>}
              </td>
              <MetricsRow m={r.metrics} />
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

// جدول العميل — صفوف قابلة للنقر للنزول إلى مشاريع العميل.
function ClientTable({
  rows,
  onDrill,
}: {
  rows: ProjectFirstByClientRow[];
  onDrill: (row: ProjectFirstByClientRow) => void;
}) {
  if (rows.length === 0) {
    return <EmptyState title="لا توجد بيانات تنفيذ" description="لا يوجد عملاء بتقارير معتمَدة ضمن هذه الفترة والنطاق." />;
  }
  return (
    <Card className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-line text-ink-2">
            <th className="px-3 py-2 text-right">العميل</th>
            {METRIC_HEADERS.map((h) => (
              <th key={h} className="px-3 py-2 text-center">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.clientId ?? 'no-client'}
              className={`border-b border-line/60 ${r.clientId ? 'cursor-pointer hover:bg-cloud/40' : ''}`}
              onClick={() => r.clientId && onDrill(r)}
            >
              <td className="px-3 py-2 font-semibold text-navy">
                {r.clientName || '—'}
                {r.clientId && <span className="mr-1 text-xs text-ink-2">↩ تفصيل</span>}
              </td>
              <MetricsRow m={r.metrics} />
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

// جدول المشروع — صفوف قابلة للنقر للنزول إلى موظّفي المشروع.
function ProjectTable({
  rows,
  onDrill,
}: {
  rows: ProjectFirstByProjectRow[];
  onDrill: (row: ProjectFirstByProjectRow) => void;
}) {
  if (rows.length === 0) {
    return <EmptyState title="لا توجد بيانات تنفيذ" description="لا توجد مشاريع بتقارير معتمَدة ضمن هذه الفترة والنطاق." />;
  }
  return (
    <Card className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-line text-ink-2">
            <th className="px-3 py-2 text-right">المشروع</th>
            <th className="px-3 py-2 text-right">العميل</th>
            {METRIC_HEADERS.map((h) => (
              <th key={h} className="px-3 py-2 text-center">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.projectId}
              className="cursor-pointer border-b border-line/60 hover:bg-cloud/40"
              onClick={() => onDrill(r)}
            >
              <td className="px-3 py-2 font-semibold text-navy">
                {r.projectName}
                <span className="mr-1 text-xs text-ink-2">↩ الموظّفون</span>
              </td>
              <td className="px-3 py-2 text-ink-2">{r.clientName || '—'}</td>
              <MetricsRow m={r.metrics} />
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

function EmployeeTable({ rows }: { rows: ProjectFirstByEmployeeRow[] }) {
  // تجميع بصريّ حسب الموظّف ثم صفوف مشاريعه.
  const groups = useMemo(() => {
    const map = new Map<string, { name: string; rows: ProjectFirstByEmployeeRow[] }>();
    for (const r of rows) {
      const g = map.get(r.employeeId) ?? { name: r.employeeName, rows: [] };
      g.rows.push(r);
      map.set(r.employeeId, g);
    }
    return Array.from(map.values());
  }, [rows]);

  if (rows.length === 0) {
    return <EmptyState title="لا توجد بيانات تنفيذ" description="لا يوجد موظّفون بتقارير معتمَدة ضمن هذه الفترة والنطاق." />;
  }
  return (
    <Card className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-line text-ink-2">
            <th className="px-3 py-2 text-right">الموظّف / المشروع</th>
            <th className="px-3 py-2 text-right">العميل</th>
            {METRIC_HEADERS.map((h) => (
              <th key={h} className="px-3 py-2 text-center">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {groups.map((g) => (
            <Fragment key={g.name}>
              <tr className="bg-cloud/40">
                <td colSpan={2 + METRIC_HEADERS.length} className="px-3 py-2 font-bold text-navy">{g.name}</td>
              </tr>
              {g.rows.map((r) => (
                <tr key={`${r.employeeId}-${r.projectId}`} className="border-b border-line/60">
                  <td className="px-3 py-2 pr-6 text-navy">{r.projectName}</td>
                  <td className="px-3 py-2 text-ink-2">{r.clientName || '—'}</td>
                  <MetricsRow m={r.metrics} />
                </tr>
              ))}
            </Fragment>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

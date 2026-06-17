// المقارنات والتحليلات — قارن تقريرًا بتقرير، أسبوعًا بأسبوع، فريقًا بفريق، موظفًا بموظف، أو إدارة بإدارة.
import { useState } from 'react';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import {
  useAllSubmissions,
  useKpiSummary,
  useEscalations,
  useImprovementPlans,
  buildTeamAggregates,
  avg,
  type TeamAggregate,
} from '../lib/useOrg';
import { Card, Select, Badge, Button } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { formatPercent } from '../lib/format';
import { useAuth } from '../lib/auth';
import type { SubmissionListItem } from '../types/api';

type CompareKind = 'report' | 'week' | 'team' | 'employee' | 'department';

// اقتراح مقارنة جاهز — يملأ النوع والعنصرين بنقرة واحدة.
type Suggestion = { label: string; hint: string; kind: CompareKind; aId: string; bId: string };

const KIND_LABEL: Record<CompareKind, string> = {
  report: 'تقرير مقابل تقرير',
  week: 'أسبوع مقابل أسبوع',
  team: 'فريق مقابل فريق',
  employee: 'موظف مقابل موظف',
  department: 'إدارة مقابل إدارة',
};

// إحصاءات حالة مجموعة تسليمات (مكتملة/معادة/بانتظار/نسبة الإكمال).
function subStats(subs: SubmissionListItem[]) {
  const total = subs.length;
  const done = subs.filter((s) => s.status === 'Closed' || s.status === 'Visible').length;
  const returned = subs.filter((s) => s.status === 'Returned').length;
  const pending = subs.filter(
    (s) => s.status === 'Submitted' || s.status === 'ApprovedByDirectManager' || s.status === 'ApprovedByNextLevel',
  ).length;
  const completion = total === 0 ? null : Math.round((done / total) * 100);
  return { total, done, returned, pending, completion };
}

interface Metric {
  label: string;
  a: number | null;
  b: number | null;
  // اتجاه «الأفضل»: higher = الأعلى أفضل، lower = الأقل أفضل.
  better: 'higher' | 'lower';
  fmt?: (v: number) => string;
}

function diffBadge(m: Metric): { text: string; tone: 'success' | 'alert' | 'muted' } {
  if (m.a === null || m.b === null) return { text: '—', tone: 'muted' };
  const d = Math.round((m.a - m.b) * 10) / 10;
  if (d === 0) return { text: 'متساوٍ', tone: 'muted' };
  const aWins = m.better === 'higher' ? d > 0 : d < 0;
  const sign = d > 0 ? '+' : '';
  return { text: `${sign}${d}`, tone: aWins ? 'success' : 'alert' };
}

export default function ComparisonsPage() {
  const { hasAnyRole } = useAuth();
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();
  const submissions = useAllSubmissions();
  const kpi = useKpiSummary();
  const escalations = useEscalations('Open');
  const plans = useImprovementPlans();

  const [kind, setKind] = useState<CompareKind>('team');
  const [aId, setAId] = useState('');
  const [bId, setBId] = useState('');

  if (users.isLoading || teams.isLoading || kpi.isLoading || submissions.isLoading)
    return <LoadingState label="يتم تحميل بيانات المقارنات…" />;
  if (users.isError || teams.isError || kpi.isError || submissions.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          kpi.refetch();
          submissions.refetch();
        }}
        description="حدث خطأ أثناء جلب بيانات المقارنات. أعد المحاولة."
      />
    );

  const userList = users.data ?? [];
  const kpiRows = kpi.data?.rows ?? [];
  const kpiByUser = new Map(kpiRows.map((r) => [r.subjectUserId, r]));

  const teamAgg = buildTeamAggregates({
    teams: teams.data ?? [],
    users: userList,
    departments: departments.data ?? [],
    submissions: submissions.data ?? [],
    kpiRows,
    escalations: escalations.data ?? [],
    plans: plans.data ?? [],
  });
  const aggById = new Map(teamAgg.map((t) => [t.team.id, t]));

  const subList = submissions.data ?? [];

  // خيارات القائمتين حسب النوع.
  const options =
    kind === 'team'
      ? teamAgg.map((t) => ({ id: t.team.id, name: t.team.nameAr }))
      : kind === 'employee'
        ? userList.map((u) => ({ id: u.id, name: u.fullName }))
        : kind === 'department'
          ? (departments.data ?? []).map((d) => ({ id: d.id, name: d.nameAr }))
          : kind === 'report'
            ? [...new Set(subList.map((s) => s.templateTitle))].sort().map((t) => ({ id: t, name: t }))
            : [...new Set(subList.filter((s) => s.periodType === 'Weekly').map((s) => s.periodKey))]
                .sort()
                .map((k) => ({ id: k, name: k }));

  const onKindChange = (k: CompareKind) => {
    setKind(k);
    setAId('');
    setBId('');
  };

  const metrics = aId && bId && aId !== bId ? buildMetrics(kind, aId, bId, { aggById, kpiByUser, userList, departments: departments.data ?? [], teams: teams.data ?? [], kpiRows, submissions: subList }) : null;
  const nameOf = (id: string) => options.find((o) => o.id === id)?.name ?? '—';

  // مقترحات مقارنة جاهزة حسب الدور والبيانات المتاحة ضمن النطاق.
  const isManagement = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'Manager', 'CeoSupport');
  const suggestions: Suggestion[] = [];
  const weekKeys = [...new Set(subList.filter((s) => s.periodType === 'Weekly').map((s) => s.periodKey))].sort();
  if (weekKeys.length >= 2) {
    const last = weekKeys[weekKeys.length - 1];
    const prev = weekKeys[weekKeys.length - 2];
    suggestions.push({ label: 'الأسبوع الحالي مقابل السابق', hint: `${last} مقابل ${prev}`, kind: 'week', aId: last, bId: prev });
  }
  const teamsWithKpi = teamAgg.filter((t) => t.avgKpi !== null);
  if (teamsWithKpi.length >= 2) {
    const sorted = [...teamsWithKpi].sort((a, b) => (b.avgKpi ?? 0) - (a.avgKpi ?? 0));
    const best = sorted[0];
    const worst = sorted[sorted.length - 1];
    if (best.team.id !== worst.team.id)
      suggestions.push({ label: 'أعلى فريق مقابل أضعف فريق', hint: `${best.team.nameAr} مقابل ${worst.team.nameAr}`, kind: 'team', aId: best.team.id, bId: worst.team.id });
  }
  const b2c = teamAgg.find((t) => t.team.nameAr.includes('B2C'));
  const b2b = teamAgg.find((t) => t.team.nameAr.includes('B2B'));
  if (b2c && b2b) suggestions.push({ label: 'B2C مقابل B2B', hint: `${b2c.team.nameAr} مقابل ${b2b.team.nameAr}`, kind: 'team', aId: b2c.team.id, bId: b2b.team.id });
  if (isManagement && (departments.data?.length ?? 0) >= 2) {
    const d = departments.data!;
    suggestions.push({ label: 'مقارنة أول إدارتين', hint: `${d[0].nameAr} مقابل ${d[1].nameAr}`, kind: 'department', aId: d[0].id, bId: d[1].id });
  }

  const applySuggestion = (s: Suggestion) => {
    setKind(s.kind);
    setAId(s.aId);
    setBId(s.bId);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">المقارنات والتحليلات</h1>
        <p className="mt-1 text-sm text-ink-2">اختر نوع المقارنة ثم العنصرين لعرض الفروق والتوصية.</p>
      </div>

      <Card>
        <div className="flex flex-wrap items-end gap-3">
          <label className="block">
            <span className="mb-1 block text-xs text-ink-2">نوع المقارنة</span>
            <Select value={kind} onChange={(e) => onKindChange(e.target.value as CompareKind)} className="w-56">
              {(Object.keys(KIND_LABEL) as CompareKind[]).map((k) => (
                <option key={k} value={k}>{KIND_LABEL[k]}</option>
              ))}
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs text-ink-2">العنصر الأول</span>
            <Select value={aId} onChange={(e) => setAId(e.target.value)} className="w-56">
              <option value="">اختر…</option>
              {options.map((o) => (
                <option key={o.id} value={o.id} disabled={o.id === bId}>{o.name}</option>
              ))}
            </Select>
          </label>
          <span className="pb-2 font-bold text-ink-3">مقابل</span>
          <label className="block">
            <span className="mb-1 block text-xs text-ink-2">العنصر الثاني</span>
            <Select value={bId} onChange={(e) => setBId(e.target.value)} className="w-56">
              <option value="">اختر…</option>
              {options.map((o) => (
                <option key={o.id} value={o.id} disabled={o.id === aId}>{o.name}</option>
              ))}
            </Select>
          </label>
        </div>

        {suggestions.length > 0 && (
          <div className="mt-4 border-t border-line pt-3">
            <p className="mb-2 text-xs font-semibold text-ink-2">مقارنات مقترحة لك:</p>
            <div className="flex flex-wrap gap-2">
              {suggestions.map((s) => (
                <button
                  key={`${s.kind}-${s.aId}-${s.bId}`}
                  onClick={() => applySuggestion(s)}
                  title={s.hint}
                  className="rounded-full border border-line bg-offwhite px-3 py-1.5 text-xs font-medium text-navy transition hover:border-orange hover:text-orange-600"
                >
                  {s.label}
                </button>
              ))}
            </div>
          </div>
        )}
      </Card>

      {!metrics ? (
        <Card>
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">
              {options.length === 0 ? 'لا توجد عناصر متاحة للمقارنة ضمن نطاقك بعد.' : 'اختر عنصرين مختلفين لبدء المقارنة.'}
            </p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {options.length === 0
                ? 'تظهر الفِرق والإدارات والأفراد للمقارنة بمجرّد توفّر بياناتهم (تقارير ومؤشّرات أداء).'
                : 'حدّد نوع المقارنة ثم العنصر الأول والثاني من القوائم أعلاه، أو ابدأ بأحد المقترحات الجاهزة بالأسفل.'}
            </p>

            {suggestions.length > 0 ? (
              <div className="mx-auto mt-5 max-w-lg">
                <p className="mb-2 text-xs font-semibold text-ink-2">جرّب مقارنة سريعة:</p>
                <div className="flex flex-wrap justify-center gap-2">
                  {suggestions.map((s) => (
                    <Button key={`empty-${s.kind}-${s.aId}-${s.bId}`} variant="ghost" onClick={() => applySuggestion(s)}>
                      {s.label}
                    </Button>
                  ))}
                </div>
              </div>
            ) : (
              <div className="mx-auto mt-5 max-w-lg rounded-xl border border-line bg-offwhite p-4 text-right">
                <p className="mb-1 text-xs font-semibold text-navy">أمثلة على المقارنات المفيدة:</p>
                <ul className="space-y-1 text-xs text-ink-2">
                  <li>• فريق B2C مقابل فريق B2B — لمعرفة الفريق الأعلى التزامًا وأداءً.</li>
                  <li>• الأسبوع الحالي مقابل الأسبوع السابق — لرصد اتجاه الالتزام والإكمال.</li>
                  <li>• فريق مقابل فريق — لتحديد أين تتركّز التأخيرات والتصعيدات.</li>
                  <li>• إدارة مقابل إدارة — لمقارنة متوسط مؤشرات الأداء بين الإدارات.</li>
                </ul>
              </div>
            )}
          </div>
        </Card>
      ) : (
        <>
          <Card>
            <SectionTitle title={`${nameOf(aId)} مقابل ${nameOf(bId)}`} hint="القيمة الخضراء = العنصر الأفضل في المؤشر" />
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-right text-sm">
                <thead className="border-b border-line text-xs text-ink-2">
                  <tr>
                    <th className="px-2 py-2 font-semibold">المؤشر</th>
                    <th className="px-2 py-2 font-semibold">{nameOf(aId)}</th>
                    <th className="px-2 py-2 font-semibold">{nameOf(bId)}</th>
                    <th className="px-2 py-2 font-semibold">الفرق</th>
                  </tr>
                </thead>
                <tbody>
                  {metrics.map((m) => {
                    const db = diffBadge(m);
                    const fmt = (v: number | null) => (v === null ? '—' : (m.fmt ? m.fmt(v) : String(v)));
                    const aWins = m.a !== null && m.b !== null && m.a !== m.b && (m.better === 'higher' ? m.a > m.b : m.a < m.b);
                    const bWins = m.a !== null && m.b !== null && m.a !== m.b && !aWins;
                    return (
                      <tr key={m.label} className="border-b border-line last:border-0">
                        <td className="px-2 py-2 text-ink-2">{m.label}</td>
                        <td className={`px-2 py-2 font-semibold ${aWins ? 'text-success' : 'text-navy'}`}>{fmt(m.a)}</td>
                        <td className={`px-2 py-2 font-semibold ${bWins ? 'text-success' : 'text-navy'}`}>{fmt(m.b)}</td>
                        <td className="px-2 py-2"><Badge tone={db.tone === 'muted' ? 'muted' : db.tone}>{db.text}</Badge></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Card>

          <Card>
            <SectionTitle title="الخلاصة والتوصية" />
            <Recommendation metrics={metrics} nameA={nameOf(aId)} nameB={nameOf(bId)} />
          </Card>
        </>
      )}
    </div>
  );
}

function Recommendation({ metrics, nameA, nameB }: { metrics: Metric[]; nameA: string; nameB: string }) {
  const aWinLabels: string[] = [];
  const bWinLabels: string[] = [];
  for (const m of metrics) {
    if (m.a === null || m.b === null || m.a === m.b) continue;
    const aBetter = m.better === 'higher' ? m.a > m.b : m.a < m.b;
    if (aBetter) aWinLabels.push(m.label);
    else bWinLabels.push(m.label);
  }
  const aWins = aWinLabels.length;
  const bWins = bWinLabels.length;
  const leader = aWins === bWins ? null : aWins > bWins ? nameA : nameB;
  const lagging = leader === null ? null : leader === nameA ? nameB : nameA;

  return (
    <div className="space-y-3 text-sm">
      <p className="text-ink">
        تفوّق <span className="font-bold text-navy">{nameA}</span> في {aWins} مؤشر، و
        <span className="font-bold text-navy"> {nameB}</span> في {bWins} مؤشر.
      </p>

      <div className="grid gap-3 sm:grid-cols-2">
        <StrengthsList title={`نقاط قوة ${nameA}`} labels={aWinLabels} />
        <StrengthsList title={`نقاط قوة ${nameB}`} labels={bWinLabels} />
      </div>

      {leader ? (
        <div className="rounded-xl border border-line bg-offwhite p-4">
          <p className="font-semibold text-navy">التوصية</p>
          <p className="mt-1 text-ink-2">
            الأداء العام لصالح <span className="font-bold text-success">{leader}</span>. يُنصح بمراجعة
            ممارسات <span className="font-bold">{leader}</span> ونقلها إلى <span className="font-bold">{lagging}</span>،
            مع وضع خطة متابعة للمؤشرات التي تأخّر فيها <span className="font-bold">{lagging}</span>.
          </p>
        </div>
      ) : (
        <div className="rounded-xl border border-line bg-offwhite p-4">
          <p className="text-ink-2">الأداء متقارب بين العنصرين — لا يوجد متفوّق واضح. راجع كل مؤشر على حدة.</p>
        </div>
      )}
    </div>
  );
}

function StrengthsList({ title, labels }: { title: string; labels: string[] }) {
  return (
    <div className="rounded-xl border border-line bg-white p-3">
      <p className="mb-1.5 text-xs font-semibold text-navy">{title}</p>
      {labels.length === 0 ? (
        <p className="text-xs text-ink-3">لا تفوّق واضح في أي مؤشر.</p>
      ) : (
        <ul className="space-y-1 text-xs text-ink-2">
          {labels.map((l) => (
            <li key={l} className="flex items-center gap-1.5">
              <span className="text-success">▲</span>
              <span>{l}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function buildMetrics(
  kind: CompareKind,
  aId: string,
  bId: string,
  ctx: {
    aggById: Map<string, TeamAggregate>;
    kpiByUser: Map<string, { totalScore: number | null }>;
    userList: { id: string; teamId: string | null; departmentId: string | null }[];
    departments: { id: string }[];
    teams: { id: string; departmentId: string }[];
    kpiRows: { subjectUserId: string; totalScore: number | null }[];
    submissions: SubmissionListItem[];
  },
): Metric[] {
  if (kind === 'report') {
    const a = subStats(ctx.submissions.filter((s) => s.templateTitle === aId));
    const b = subStats(ctx.submissions.filter((s) => s.templateTitle === bId));
    return [
      { label: 'إجمالي التسليمات', a: a.total, b: b.total, better: 'higher' },
      { label: 'مكتملة / معتمدة', a: a.done, b: b.done, better: 'higher' },
      { label: 'نسبة الإكمال', a: a.completion, b: b.completion, better: 'higher', fmt: (v) => `${v}٪` },
      { label: 'تقارير معادة', a: a.returned, b: b.returned, better: 'lower' },
      { label: 'بانتظار الاعتماد', a: a.pending, b: b.pending, better: 'lower' },
    ];
  }
  if (kind === 'week') {
    const a = subStats(ctx.submissions.filter((s) => s.periodType === 'Weekly' && s.periodKey === aId));
    const b = subStats(ctx.submissions.filter((s) => s.periodType === 'Weekly' && s.periodKey === bId));
    return [
      { label: 'إجمالي التسليمات', a: a.total, b: b.total, better: 'higher' },
      { label: 'مكتملة / معتمدة', a: a.done, b: b.done, better: 'higher' },
      { label: 'نسبة الإكمال', a: a.completion, b: b.completion, better: 'higher', fmt: (v) => `${v}٪` },
      { label: 'تقارير معادة', a: a.returned, b: b.returned, better: 'lower' },
      { label: 'بانتظار الاعتماد', a: a.pending, b: b.pending, better: 'lower' },
    ];
  }
  if (kind === 'team') {
    const a = ctx.aggById.get(aId);
    const b = ctx.aggById.get(bId);
    if (!a || !b) return [];
    return [
      { label: 'متوسط KPI', a: a.avgKpi, b: b.avgKpi, better: 'higher', fmt: formatPercent },
      { label: 'الالتزام بالتسليم', a: a.compliance, b: b.compliance, better: 'higher', fmt: (v) => `${v}٪` },
      { label: 'تقارير متأخرة', a: a.late, b: b.late, better: 'lower' },
      { label: 'تقارير معادة', a: a.returned, b: b.returned, better: 'lower' },
      { label: 'تصعيدات مفتوحة', a: a.escalations, b: b.escalations, better: 'lower' },
      { label: 'خطط تطوير مفتوحة', a: a.openPlans, b: b.openPlans, better: 'lower' },
      { label: 'عدد الأعضاء', a: a.memberCount, b: b.memberCount, better: 'higher' },
    ];
  }
  if (kind === 'employee') {
    const ka = ctx.kpiByUser.get(aId)?.totalScore ?? null;
    const kb = ctx.kpiByUser.get(bId)?.totalScore ?? null;
    return [{ label: 'مؤشر الأداء KPI', a: ka, b: kb, better: 'higher', fmt: formatPercent }];
  }
  // department: aggregate KPI across members.
  const deptKpi = (deptId: string) => {
    const memberIds = ctx.userList.filter((u) => u.departmentId === deptId).map((u) => u.id);
    const vals = ctx.kpiRows
      .filter((r) => memberIds.includes(r.subjectUserId) && r.totalScore !== null)
      .map((r) => r.totalScore as number);
    return { kpi: avg(vals), members: memberIds.length, evaluated: vals.length };
  };
  const deptTeams = (deptId: string) => ctx.teams.filter((t) => t.departmentId === deptId).length;
  const a = deptKpi(aId);
  const b = deptKpi(bId);
  return [
    { label: 'متوسط KPI', a: a.kpi, b: b.kpi, better: 'higher', fmt: formatPercent },
    { label: 'عدد الموظفين', a: a.members, b: b.members, better: 'higher' },
    { label: 'عدد المُقيَّمين', a: a.evaluated, b: b.evaluated, better: 'higher' },
    { label: 'عدد الفرق', a: deptTeams(aId), b: deptTeams(bId), better: 'higher' },
  ];
}

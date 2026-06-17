// نظرة KPI متعددة المستويات — الشركة ← الإدارة ← الفريق ← الموظف، مع المقارنات.
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import {
  useAllSubmissions,
  useKpiSummary,
  useEscalations,
  useImprovementPlans,
  buildTeamAggregates,
  avg,
} from '../lib/useOrg';
import { Card, Badge, StatCard, Button } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle, ProgressBar } from '../components/dashboard';
import { Donut } from '../components/Charts';
import { kpiTrendDisplay, formatPercent } from '../lib/format';

function kpiTone(score: number | null): 'success' | 'gold' | 'alert' | 'muted' {
  if (score === null) return 'muted';
  if (score >= 70) return 'success';
  if (score >= 50) return 'gold';
  return 'alert';
}

export function KpiOverview() {
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();
  const submissions = useAllSubmissions();
  const kpi = useKpiSummary();
  const escalations = useEscalations('Open');
  const plans = useImprovementPlans();
  const [expandedTeam, setExpandedTeam] = useState<string | null>(null);

  if (users.isLoading || teams.isLoading || kpi.isLoading)
    return <LoadingState label="يتم تحميل ملخص المؤشّرات…" />;
  if (users.isError || teams.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          kpi.refetch();
        }}
        description="حدث خطأ أثناء جلب ملخص المؤشّرات. أعد المحاولة."
      />
    );

  const userList = users.data ?? [];
  const kpiRows = kpi.data?.rows ?? [];
  const kpiByUser = new Map(kpiRows.map((r) => [r.subjectUserId, r]));
  const companyAvg = kpi.data?.averageScore ?? null;
  const belowTarget = kpi.data?.belowTarget ?? 0;

  // مستوى الإدارة.
  const deptRows = (departments.data ?? []).map((d) => {
    const memberIds = userList.filter((u) => u.departmentId === d.id).map((u) => u.id);
    const vals = memberIds
      .map((id) => kpiByUser.get(id)?.totalScore)
      .filter((v): v is number => v !== null && v !== undefined);
    return { dept: d, avgKpi: avg(vals), evaluated: vals.length, members: memberIds.length };
  });

  // مستوى الفريق.
  const teamAgg = buildTeamAggregates({
    teams: teams.data ?? [],
    users: userList,
    departments: departments.data ?? [],
    submissions: submissions.data ?? [],
    kpiRows,
    escalations: escalations.data ?? [],
    plans: plans.data ?? [],
  });

  const expanded = teamAgg.find((t) => t.team.id === expandedTeam);
  const expandedMembers = expanded
    ? userList
        .filter((u) => expanded.memberIds.includes(u.id))
        .map((u) => ({ user: u, row: kpiByUser.get(u.id) }))
        .sort((a, b) => (b.row?.totalScore ?? -1) - (a.row?.totalScore ?? -1))
    : [];

  return (
    <div className="space-y-6">
      {/* مستوى الشركة */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="متوسط مؤشر الشركة" value={companyAvg === null ? '—' : formatPercent(companyAvg)} />
        <StatCard label="عدد التقييمات" value={kpi.data?.evaluated ?? 0} />
        <StatCard label="دون المستهدف" value={belowTarget} tone={belowTarget > 0 ? 'alert' : 'navy'} />
        <StatCard label="الفرق" value={teamAgg.length} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* مستوى الإدارة */}
        <Card>
          <SectionTitle title="مؤشر الأداء حسب الإدارة" />
          <div className="space-y-3">
            {deptRows.map((d) => (
              <div key={d.dept.id}>
                <div className="mb-1 flex items-center justify-between text-sm">
                  <span className="font-medium text-navy">{d.dept.nameAr}</span>
                  <span className="text-ink-2">
                    {d.avgKpi === null ? '—' : formatPercent(d.avgKpi)} · {d.evaluated}/{d.members}
                  </span>
                </div>
                <ProgressBar
                  value={d.avgKpi ?? 0}
                  tone={d.avgKpi !== null && d.avgKpi >= 70 ? 'success' : 'orange'}
                />
              </div>
            ))}
            {deptRows.length === 0 && (
              <p className="py-6 text-center text-sm text-ink-2">لا توجد إدارات ضمن نطاقك. تظهر متوسطات المؤشّرات لكل إدارة بمجرّد تقييم أعضائها.</p>
            )}
          </div>
        </Card>

        {/* توزيع حالات الأداء */}
        <Card>
          <SectionTitle title="توزيع مستوى الأداء" hint="عدد الموظفين حسب فئة المؤشر" />
          <Donut
            slices={[
              { label: 'ممتاز (≥70)', value: kpiRows.filter((r) => (r.totalScore ?? 0) >= 70).length },
              {
                label: 'متوسط (50-69)',
                value: kpiRows.filter((r) => (r.totalScore ?? 0) >= 50 && (r.totalScore ?? 0) < 70).length,
              },
              { label: 'دون المستهدف (<50)', value: kpiRows.filter((r) => (r.totalScore ?? 100) < 50).length },
            ]}
          />
        </Card>
      </div>

      {/* مستوى الفريق */}
      <Card>
        <SectionTitle title="مؤشر الأداء حسب الفريق" hint="انقر فريقًا لعرض تفصيل الأعضاء" />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الفريق</th>
                <th className="px-2 py-2 font-semibold">القائد</th>
                <th className="px-2 py-2 font-semibold">الأعضاء</th>
                <th className="px-2 py-2 font-semibold">متوسط KPI</th>
                <th className="px-2 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {teamAgg.map((t) => (
                <tr key={t.team.id} className="border-b border-line last:border-0">
                  <td className="px-2 py-2 font-medium text-navy">{t.team.nameAr}</td>
                  <td className="px-2 py-2 text-ink-2">{t.leaderName}</td>
                  <td className="px-2 py-2">{t.memberCount}</td>
                  <td className="px-2 py-2">
                    <Badge tone={kpiTone(t.avgKpi)}>{t.avgKpi === null ? '—' : formatPercent(t.avgKpi)}</Badge>
                  </td>
                  <td className="px-2 py-2">
                    <button
                      onClick={() => setExpandedTeam(expandedTeam === t.team.id ? null : t.team.id)}
                      className="text-sm font-semibold text-orange-600 hover:underline"
                    >
                      {expandedTeam === t.team.id ? 'إخفاء' : 'تفصيل الأعضاء'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {expanded && (
          <div className="mt-4 rounded-xl border border-line bg-offwhite p-4">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="font-bold text-navy">أعضاء فريق {expanded.team.nameAr}</h3>
              <div className="flex gap-2">
                <Link to="/app/analytics">
                  <Button variant="ghost">قارن بالفترة السابقة</Button>
                </Link>
                <Link to={`/app/teams/${expanded.team.id}`}>
                  <Button variant="ghost">صفحة الفريق</Button>
                </Link>
              </div>
            </div>
            <div className="grid gap-2 sm:grid-cols-2">
              {expandedMembers.map(({ user, row }) => {
                const score = row?.totalScore ?? null;
                const belowTarget = score != null && score < 60;
                // سبب غياب التقييم / سبب الاتجاه — بدل ترك شرطة غامضة.
                const reason =
                  score == null
                    ? 'لا تقييم لهذه الفترة — لم يُسجَّل بعد من المُقيِّم.'
                    : belowTarget
                      ? 'دون المستهدف (أقل من 60٪) — يُنصح بخطة تحسين.'
                      : null;
                return (
                  <div key={user.id} className="rounded-lg border border-line bg-white px-3 py-2.5 text-sm">
                    <div className="flex items-center justify-between gap-2">
                      <Link to={`/app/employee/${user.id}`} className="font-medium text-navy hover:text-orange-600 hover:underline">{user.fullName}</Link>
                      <span className="flex items-center gap-2">
                        {row && <span className="text-xs text-ink-2">{kpiTrendDisplay(row.trend, score != null)}</span>}
                        <Badge tone={kpiTone(score)}>
                          {score == null ? <span title="لا يوجد تقييم لهذه الفترة">لا تقييم</span> : formatPercent(score)}
                        </Badge>
                      </span>
                    </div>
                    <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                      {reason && <span className={belowTarget ? 'text-alert' : 'text-ink-3'}>{reason}</span>}
                      <Link to={`/app/employee/${user.id}#kpi`} className="font-semibold text-orange-600 hover:underline">عرض التقييمات</Link>
                      {belowTarget && (
                        <Link to="/app/development" className="font-semibold text-orange-600 hover:underline">افتح خطة تحسين</Link>
                      )}
                    </div>
                  </div>
                );
              })}
              {expandedMembers.length === 0 && <p className="text-sm text-ink-2">لا أعضاء.</p>}
            </div>
          </div>
        )}
      </Card>

      {/* مستوى الموظف — أقوى/أضعف */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle title="الأعلى أداءً" />
          <RankList rows={[...kpiRows].filter((r) => r.totalScore != null).sort((a, b) => (b.totalScore ?? 0) - (a.totalScore ?? 0)).slice(0, 6)} />
        </Card>
        <Card>
          <SectionTitle title="الأكثر حاجة للدعم" />
          <RankList rows={[...kpiRows].filter((r) => r.totalScore != null).sort((a, b) => (a.totalScore ?? 0) - (b.totalScore ?? 0)).slice(0, 6)} />
        </Card>
      </div>
    </div>
  );
}

function RankList({ rows }: { rows: { subjectUserId: string; subjectName: string; totalScore: number | null; trend: import('../types/api').KpiTrend }[] }) {
  if (rows.length === 0) return <p className="py-6 text-center text-sm text-ink-2">لا توجد تقييمات في هذه الفترة بعد. يظهر الترتيب بمجرّد اعتماد تقييمات الأداء.</p>;
  return (
    <ul className="space-y-2 text-sm">
      {rows.map((r, i) => (
        <li key={`${r.subjectUserId}-${i}`} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
          <Link to={`/app/employee/${r.subjectUserId}`} className="font-medium text-navy hover:text-orange-600 hover:underline">{r.subjectName}</Link>
          <span className="flex items-center gap-2">
            <span className="text-xs text-ink-2">{kpiTrendDisplay(r.trend, r.totalScore != null)}</span>
            <Badge tone={kpiTone(r.totalScore)}>{r.totalScore == null ? <span title="لا يوجد تقييم لهذه الفترة">لا تقييم</span> : formatPercent(r.totalScore)}</Badge>
          </span>
        </li>
      ))}
    </ul>
  );
}

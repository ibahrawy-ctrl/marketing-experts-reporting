// صفحة فرق العمل — بطاقة/صف لكل فريق مع كل مؤشرات المتابعة، وكل فريق يفتح صفحة تفاصيل.
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { DEFAULT_KPI_FILTER, appliedThreshold, useKpiPerformance } from '../lib/useKpi';
import {
  useAllSubmissions,
  useEscalations,
  useImprovementPlans,
  buildTeamAggregates,
  sortTeamAggregates,
  healthLabel,
  healthTone,
  type TeamAggregate,
} from '../lib/useOrg';
import { Card, Badge, Select, Button, StatCard } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { ProgressBar } from '../components/dashboard';
import { formatPercent } from '../lib/format';
import { useAuth } from '../lib/auth';
import { CreateTeamForm } from './UsersPage';

export default function TeamsPage() {
  const { hasAnyRole } = useAuth();
  const canCreateTeam = hasAnyRole('Admin'); // إنشاء الفريق محصور بالأدمن خادميًّا (Policies.AdminOnly).
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();
  const submissions = useAllSubmissions();
  // P1-KPI-008: مصدر KPI الوحيد = الخادم (Approved فقط · آخر فترة مكتملة · نبض أسبوعيّ).
  const kpi = useKpiPerformance(DEFAULT_KPI_FILTER);
  const escalations = useEscalations('Open');
  const plans = useImprovementPlans();
  const [view, setView] = useState<'cards' | 'table'>('cards');
  const [deptFilter, setDeptFilter] = useState('');
  const [healthFilter, setHealthFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);

  if (users.isLoading || teams.isLoading || submissions.isLoading || kpi.isLoading)
    return <LoadingState label="يتم تحميل الفرق…" />;
  if (users.isError || teams.isError || submissions.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          submissions.refetch();
          kpi.refetch();
        }}
        description="حدث خطأ أثناء جلب بيانات الفرق. أعد المحاولة."
      />
    );

  let aggregates = buildTeamAggregates({
    teams: teams.data ?? [],
    users: users.data ?? [],
    departments: departments.data ?? [],
    submissions: submissions.data ?? [],
    kpiEmployees: kpi.data?.employees ?? [],
    kpiTeams: kpi.data?.teams ?? [],
    kpiThreshold: appliedThreshold(kpi.data),
    escalations: escalations.data ?? [],
    plans: plans.data ?? [],
  });

  if (deptFilter) aggregates = aggregates.filter((t) => t.team.departmentId === deptFilter);
  if (healthFilter) aggregates = aggregates.filter((t) => t.health === healthFilter);
  // ترتيب تلقائي: خطر ← يحتاج متابعة ← جيد.
  aggregates = sortTeamAggregates(aggregates);

  const totalMembers = aggregates.reduce((a, t) => a + t.memberCount, 0);
  const totalLate = aggregates.reduce((a, t) => a + t.late, 0);
  const atRisk = aggregates.filter((t) => t.health === 'risk').length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">فرق العمل</h1>
          <p className="mt-1 text-sm text-ink-2">حالة كل فريق: الالتزام، المؤشرات، التأخير، والتصعيدات.</p>
        </div>
        <div className="flex gap-2">
          {canCreateTeam && (
            <Button variant="primary" onClick={() => setShowCreate((v) => !v)}>
              {showCreate ? 'إغلاق' : '+ إنشاء فريق جديد'}
            </Button>
          )}
          <Button variant={view === 'cards' ? 'primary' : 'ghost'} onClick={() => setView('cards')}>
            بطاقات
          </Button>
          <Button variant={view === 'table' ? 'primary' : 'ghost'} onClick={() => setView('table')}>
            جدول
          </Button>
        </div>
      </div>

      {canCreateTeam && showCreate && (
        <Card>
          <CreateTeamForm
            departments={departments.data ?? []}
            users={users.data ?? []}
            onDone={() => setShowCreate(false)}
          />
        </Card>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="عدد الفرق" value={aggregates.length} />
        <StatCard label="إجمالي الأعضاء" value={totalMembers} />
        <StatCard label="تقارير متأخرة" value={totalLate} tone={totalLate > 0 ? 'alert' : 'navy'} />
        <StatCard label="فرق في خطر" value={atRisk} tone={atRisk > 0 ? 'alert' : 'navy'} />
      </div>

      <div className="flex flex-wrap gap-3">
        <Select value={deptFilter} onChange={(e) => setDeptFilter(e.target.value)} className="max-w-xs">
          <option value="">كل الإدارات</option>
          {(departments.data ?? []).map((d) => (
            <option key={d.id} value={d.id}>
              {d.nameAr}
            </option>
          ))}
        </Select>
        <Select value={healthFilter} onChange={(e) => setHealthFilter(e.target.value)} className="max-w-xs">
          <option value="">كل الحالات</option>
          <option value="good">جيد</option>
          <option value="watch">يحتاج متابعة</option>
          <option value="risk">خطر</option>
        </Select>
      </div>

      {aggregates.length === 0 ? (
        <Card>
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد فرق مطابقة.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              لا يوجد فريق ضمن نطاقك يطابق البحث أو الفلتر الحالي. جرّب تعديل البحث أو اختيار «الكل».
              {canCreateTeam ? ' لإنشاء فريق جديد استخدم زر «إنشاء فريق جديد» أعلى الصفحة.' : ' تُنشأ الفرق وتُدار من قِبل المسؤول.'}
            </p>
          </div>
        </Card>
      ) : view === 'cards' ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {aggregates.map((t) => (
            <TeamCard key={t.team.id} t={t} />
          ))}
        </div>
      ) : (
        <TeamsTable rows={aggregates} />
      )}
    </div>
  );
}

function TeamCard({ t }: { t: TeamAggregate }) {
  return (
    <Card className="flex flex-col">
      <div className="flex items-start justify-between gap-2">
        <div>
          <h3 className="text-lg font-bold text-navy">{t.team.nameAr}</h3>
          <p className="text-xs text-ink-2">
            {t.departmentName} · القائد: {t.leaderName}
          </p>
        </div>
        <Badge tone={healthTone[t.health]}>{healthLabel[t.health]}</Badge>
      </div>

      {t.reasons.length > 0 && (
        <div className={`mt-3 rounded-lg border-r-4 px-3 py-2 text-xs ${t.health === 'risk' ? 'border-r-alert bg-alert/5' : 'border-r-gold bg-gold/5'}`}>
          <p className="font-semibold text-ink">سبب الحالة:</p>
          <p className="mt-0.5 text-ink-2">{t.reasons.join(' · ')}</p>
        </div>
      )}

      <div className="mt-3">
        <div className="mb-1 flex justify-between text-xs text-ink-2">
          <span>الالتزام بالتسليم</span>
          <span className="font-semibold">{t.compliance}٪</span>
        </div>
        <ProgressBar value={t.compliance} tone={t.compliance >= 80 ? 'success' : t.compliance >= 50 ? 'orange' : 'navy'} />
      </div>

      <dl className="mt-4 grid grid-cols-3 gap-y-3 text-center">
        <Stat label="الأعضاء" value={t.memberCount} />
        <Stat label="مطلوب" value={t.requiredThisWeek} />
        <Stat label="مُسلّم" value={t.submitted} tone="success" />
        <Stat label="متأخر" value={t.late} tone={t.late > 0 ? 'alert' : undefined} />
        <Stat label="معاد" value={t.returned} tone={t.returned > 0 ? 'gold' : undefined} />
        <Stat label="KPI" value={t.avgKpi === null ? '—' : `${t.avgKpi}`} tone="orange" />
        <Stat label="دون المستهدف" value={t.membersBelowTarget} tone={t.membersBelowTarget > 0 ? 'alert' : undefined} />
        <Stat label="تصعيدات" value={t.escalations} tone={t.escalations > 0 ? 'alert' : undefined} />
        <Stat label="خطط تطوير" value={t.openPlans} />
        <Stat label="بانتظار اعتماد" value={t.pendingApproval} />
      </dl>

      <div className="mt-4 border-t border-line pt-3">
        <Link to={`/app/teams/${t.team.id}`}>
          <Button variant="primary" className="w-full">
            تفاصيل الفريق
          </Button>
        </Link>
      </div>
    </Card>
  );
}

function Stat({ label, value, tone }: { label: string; value: React.ReactNode; tone?: 'success' | 'alert' | 'gold' | 'orange' }) {
  const color =
    tone === 'success'
      ? 'text-success'
      : tone === 'alert'
        ? 'text-alert'
        : tone === 'gold'
          ? 'text-gold'
          : tone === 'orange'
            ? 'text-orange-600'
            : 'text-navy';
  return (
    <div>
      <dd className={`text-xl font-bold ${color}`}>{value}</dd>
      <dt className="text-[11px] text-ink-2">{label}</dt>
    </div>
  );
}

function TeamsTable({ rows }: { rows: TeamAggregate[] }) {
  return (
    <Card className="overflow-x-auto p-0">
      <table className="w-full min-w-[900px] text-right text-sm">
        <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
          <tr>
            <th className="px-3 py-2.5 font-semibold">الفريق</th>
            <th className="px-3 py-2.5 font-semibold">القائد</th>
            <th className="px-3 py-2.5 font-semibold">الأعضاء</th>
            <th className="px-3 py-2.5 font-semibold">مطلوب</th>
            <th className="px-3 py-2.5 font-semibold">مُسلّم</th>
            <th className="px-3 py-2.5 font-semibold">متأخر</th>
            <th className="px-3 py-2.5 font-semibold">الالتزام</th>
            <th className="px-3 py-2.5 font-semibold">KPI</th>
            <th className="px-3 py-2.5 font-semibold">دون المستهدف</th>
            <th className="px-3 py-2.5 font-semibold">تصعيدات</th>
            <th className="px-3 py-2.5 font-semibold">خطط</th>
            <th className="px-3 py-2.5 font-semibold">الحالة / السبب</th>
            <th className="px-3 py-2.5 font-semibold"></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((t) => (
            <tr key={t.team.id} className="border-b border-line last:border-0 hover:bg-offwhite">
              <td className="px-3 py-2.5 font-semibold text-navy">{t.team.nameAr}</td>
              <td className="px-3 py-2.5 text-ink-2">{t.leaderName}</td>
              <td className="px-3 py-2.5">{t.memberCount}</td>
              <td className="px-3 py-2.5">{t.requiredThisWeek}</td>
              <td className="px-3 py-2.5 text-success">{t.submitted}</td>
              <td className={`px-3 py-2.5 ${t.late > 0 ? 'font-semibold text-alert' : ''}`}>{t.late}</td>
              <td className="px-3 py-2.5">{t.compliance}٪</td>
              <td className="px-3 py-2.5 text-orange-600">{t.avgKpi === null ? '—' : formatPercent(t.avgKpi)}</td>
              <td className={`px-3 py-2.5 ${t.membersBelowTarget > 0 ? 'font-semibold text-alert' : ''}`}>{t.membersBelowTarget}</td>
              <td className={`px-3 py-2.5 ${t.escalations > 0 ? 'font-semibold text-alert' : ''}`}>{t.escalations}</td>
              <td className="px-3 py-2.5">{t.openPlans}</td>
              <td className="px-3 py-2.5">
                <Badge tone={healthTone[t.health]}>{healthLabel[t.health]}</Badge>
                {t.reasons.length > 0 && <p className="mt-1 max-w-[220px] text-[11px] text-ink-3">{t.reasons.join(' · ')}</p>}
              </td>
              <td className="px-3 py-2.5">
                <Link to={`/app/teams/${t.team.id}`} className="text-sm font-semibold text-orange-600 hover:underline">
                  تفاصيل
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

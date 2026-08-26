// لوحة المدير/الحوكمة — بوّابة موجزة تقود إلى الصفحات الداخلية (ليست بديلاً عنها).
import { Link } from 'react-router-dom';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { DEFAULT_KPI_FILTER, appliedThreshold, useKpiPerformance } from '../lib/useKpi';
import {
  useAllSubmissions,
  useEscalations,
  useDecisions,
  useRisks,
  useImprovementPlans,
  buildTeamAggregates,
  activeWeeklyKey,
  latestWeeklyKey,
  healthLabel,
  healthTone,
} from '../lib/useOrg';
import { MetricTile, SectionTitle, ActionItem, DashboardHero, HeroChip } from '../components/dashboard';
import { Card, Badge, Button } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { NavIcon, type IconName } from '../components/icons';
import { submissionStatusLabel, formatPercent } from '../lib/format';
import { ReportInsightsSection } from './RoleHomeDashboards';

const QUICK: { to: string; label: string; icon: IconName }[] = [
  { to: '/app/users', label: 'إضافة مستخدم', icon: 'users' },
  { to: '/app/teams', label: 'عرض الفرق', icon: 'teams' },
  { to: '/app/report-templates', label: 'قالب تقرير جديد', icon: 'template' },
  { to: '/app/kpi-templates', label: 'قالب KPI جديد', icon: 'kpiTemplate' },
  { to: '/app/submissions', label: 'التقارير المقدمة', icon: 'reports' },
  { to: '/app/governance', label: 'الحوكمة والمتابعة', icon: 'governance' },
  { to: '/app/workflows', label: 'مسارات الاعتماد', icon: 'workflow' },
  { to: '/app/analytics', label: 'المقارنات', icon: 'analytics' },
];

export function AdminHome() {
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();
  const submissions = useAllSubmissions();
  // P1-KPI-008: كلّ أرقام KPI هنا من الخادم — لا طيّ صفوف ولا متوسّط في الواجهة.
  const kpi = useKpiPerformance(DEFAULT_KPI_FILTER);
  const escalations = useEscalations('Open');
  const decisions = useDecisions('Proposed');
  const risks = useRisks('Open');
  const plans = useImprovementPlans();

  const loading =
    users.isLoading || teams.isLoading || departments.isLoading || submissions.isLoading || kpi.isLoading;
  if (loading) return <LoadingState label="يتم تحميل لوحة الإدارة…" />;
  if (users.isError || teams.isError || departments.isError || submissions.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          departments.refetch();
          submissions.refetch();
          kpi.refetch();
        }}
        title="تعذّر تحميل لوحة الإدارة"
        description="حدث خطأ أثناء جلب بيانات اللوحة. تحقّق من اتصالك ثم أعد المحاولة."
      />
    );

  const userList = users.data ?? [];
  const teamList = teams.data ?? [];
  const subs = submissions.data ?? [];
  const kpiEmployees = kpi.data?.employees ?? [];

  const aggregates = buildTeamAggregates({
    teams: teamList,
    users: userList,
    departments: departments.data ?? [],
    submissions: subs,
    kpiEmployees,
    kpiTeams: kpi.data?.teams ?? [],
    kpiThreshold: appliedThreshold(kpi.data),
    escalations: escalations.data ?? [],
    plans: plans.data ?? [],
  });

  // الأسبوع المعروض = أحدث أسبوع فيه بيانات فعلية (نفس منطق buildTeamAggregates).
  const displayedWeek = activeWeeklyKey(subs);
  const newestWeek = latestWeeklyKey(subs);
  const usingFallbackWeek = displayedWeek != null && newestWeek != null && displayedWeek !== newestWeek;

  const requiredThisWeek = aggregates.reduce((a, t) => a + t.requiredThisWeek, 0);
  const submittedThisWeek = aggregates.reduce((a, t) => a + t.submitted, 0);
  const lateThisWeek = aggregates.reduce((a, t) => a + t.late, 0);
  // B-2: متوسّط الشركة = متوسّط متوسّطات الموظّفين كما حسبه الخادم، لا متوسّط تقييمات خام.
  const avgKpi = kpi.data?.company.measure.value ?? null;
  const pendingApprovals = subs.filter(
    (s) => s.status === 'Submitted' || s.status === 'ApprovedByDirectManager' || s.status === 'ApprovedByNextLevel',
  );
  const lateReports = subs.filter((s) => s.status === 'Draft' || s.status === 'Returned');
  const belowTargetKpi = kpiEmployees.filter((e) => e.isBelowTarget === true);
  const openDecisions = decisions.data ?? [];

  return (
    <div className="space-y-6">
      {/* ترويسة موحّدة */}
      <DashboardHero
        title="لوحة الإدارة والحوكمة"
        subtitle="نظرة شاملة على المستخدمين والفرق والتقارير والمؤشرات — وبوّابة سريعة لكل أقسام النظام."
        icon="governance"
        accent="orange"
        badges={<HeroChip>مدير النظام والحوكمة</HeroChip>}
        cta={
          <Link to="/app/governance">
            <span className="inline-block rounded-lg bg-orange px-4 py-2 text-sm font-bold text-white hover:bg-orange-600">
              فتح مركز الحوكمة
            </span>
          </Link>
        }
      />

      {/* توضيح الأسبوع المعروض في مؤشرات «الأسبوع» */}
      {displayedWeek && (
        <div className={`rounded-xl border-r-4 px-4 py-2.5 text-sm ${usingFallbackWeek ? 'border-r-gold bg-gold/5 text-ink' : 'border-r-navy bg-navy-50 text-ink-2'}`}>
          {usingFallbackWeek ? (
            <>
              لا توجد بيانات مكتملة للأسبوع الأحدث ({newestWeek})، يتم عرض أحدث أسبوع متاح:{' '}
              <span className="font-bold text-navy">{displayedWeek}</span>. تشير مؤشّرات «الأسبوع» أدناه إلى هذا الأسبوع.
            </>
          ) : (
            <>
              تشير مؤشّرات «الأسبوع» أدناه إلى الأسبوع: <span className="font-bold text-navy">{displayedWeek}</span>.
            </>
          )}
        </div>
      )}

      {/* بطاقات موجزة */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <MetricTile label="المستخدمون" value={userList.length} tone="navy" to="/app/users" icon="users" />
        <MetricTile label="الفرق" value={teamList.length} tone="navy" to="/app/teams" icon="teams" />
        <MetricTile label="تقارير مطلوبة (الأسبوع)" value={requiredThisWeek} tone="navy" to="/app/submissions" icon="reports" />
        <MetricTile label="تقارير مُسلّمة" value={submittedThisWeek} tone="success" to="/app/submissions" icon="reports" />
        <MetricTile label="تقارير متأخرة" value={lateThisWeek} tone="alert" to="/app/submissions" icon="workflow" />
        <MetricTile
          label="متوسط مؤشرات الأداء"
          value={avgKpi === null ? '—' : formatPercent(avgKpi)}
          tone="orange"
          to="/app/kpi"
          icon="kpi"
        />
        <MetricTile label="مخاطر مفتوحة" value={(risks.data ?? []).length} tone="alert" to="/app/governance" icon="governance" />
        <MetricTile label="قرارات قيد البتّ" value={openDecisions.length} tone="gold" to="/app/governance" icon="governance" />
      </div>

      {/* إجراءات سريعة */}
      <Card>
        <SectionTitle title="إجراءات سريعة" hint="ابدأ مهمة بنقرة واحدة" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {QUICK.map((q) => (
            <Link
              key={q.to}
              to={q.to}
              className="flex flex-col items-center gap-2 rounded-xl border border-line bg-offwhite p-4 text-center transition hover:border-navy hover:bg-white"
            >
              <span className="grid h-10 w-10 place-items-center rounded-full bg-navy-50 text-navy">
                <NavIcon name={q.icon} className="h-5 w-5" />
              </span>
              <span className="text-sm font-medium text-navy">{q.label}</span>
            </Link>
          ))}
        </div>
      </Card>

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      {/* المطلوب الآن */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle
            title="تقارير تحتاج إجراء"
            hint={`${lateReports.length} مسودة/معادة`}
            action={
              <Link to="/app/submissions">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {lateReports.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد تقارير متأخرة.</p>
          ) : (
            <ul>
              {lateReports.slice(0, 6).map((s) => (
                <ActionItem
                  key={s.id}
                  title={s.templateTitle}
                  context={`${s.submitterName} · ${s.periodKey}`}
                  badge={<Badge tone={s.status === 'Returned' ? 'alert' : 'gold'}>{submissionStatusLabel[s.status]}</Badge>}
                  action={
                    <Link to={`/app/submissions?open=${s.id}`}>
                      <Button variant="ghost">فتح</Button>
                    </Link>
                  }
                />
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <SectionTitle
            title="بانتظار الاعتماد"
            hint={`${pendingApprovals.length} تقرير`}
            action={
              <Link to="/app/submissions?tab=pending">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {pendingApprovals.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا شيء بانتظار الاعتماد.</p>
          ) : (
            <ul>
              {pendingApprovals.slice(0, 6).map((s) => (
                <ActionItem
                  key={s.id}
                  title={s.templateTitle}
                  context={`${s.submitterName} · ${s.periodKey}`}
                  badge={<Badge tone="navy">{submissionStatusLabel[s.status]}</Badge>}
                  action={
                    <Link to={`/app/submissions?open=${s.id}`}>
                      <Button variant="ghost">فتح</Button>
                    </Link>
                  }
                />
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <SectionTitle
            title="مؤشرات أداء دون المستهدف"
            hint={`${belowTargetKpi.length} موظف`}
            action={
              <Link to="/app/kpi">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {belowTargetKpi.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">جميع المؤشرات ضمن المستهدف.</p>
          ) : (
            <ul>
              {belowTargetKpi.slice(0, 6).map((r) => (
                <ActionItem
                  key={r.userId}
                  title={r.fullName}
                  context={`النتيجة: ${formatPercent(r.measure.value)} · ${kpi.data?.periodResolved.label ?? ''}`}
                  badge={<Badge tone="alert">دون المستهدف</Badge>}
                />
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <SectionTitle
            title="قرارات قيد البتّ"
            hint={`${openDecisions.length} قرار`}
            action={
              <Link to="/app/governance?tab=decisions">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {openDecisions.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا قرارات معلّقة.</p>
          ) : (
            <ul>
              {openDecisions.slice(0, 6).map((d) => (
                <ActionItem key={d.id} title={d.title} context={d.madeByName ?? '—'} badge={<Badge tone="gold">مقترح</Badge>} />
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* الفرق */}
      <Card>
        <SectionTitle
          title="الفرق"
          hint="نظرة سريعة على حالة كل فريق"
          action={
            <Link to="/app/teams">
              <Button variant="ghost">كل الفرق</Button>
            </Link>
          }
        />
        {aggregates.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-2">لا توجد فرق بعد. تُنشأ الفرق وتُربط بقادتها وأعضائها من صفحة «المستخدمون».</p>
        ) : (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {aggregates.map((t) => (
              <Link
                key={t.team.id}
                to={`/app/teams/${t.team.id}`}
                className="rounded-xl border border-line bg-offwhite p-4 transition hover:border-navy hover:bg-white"
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="font-bold text-navy">{t.team.nameAr}</p>
                  <Badge tone={healthTone[t.health]}>{healthLabel[t.health]}</Badge>
                </div>
                <p className="mt-1 text-xs text-ink-2">
                  القائد: {t.leaderName} · {t.memberCount} عضو
                </p>
                <div className="mt-3 flex items-center justify-between text-xs text-ink-2">
                  <span>الالتزام: {t.compliance}٪</span>
                  <span>KPI: {t.avgKpi === null ? '—' : formatPercent(t.avgKpi)}</span>
                  {t.late > 0 && <span className="font-semibold text-alert">{t.late} متأخر</span>}
                </div>
              </Link>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

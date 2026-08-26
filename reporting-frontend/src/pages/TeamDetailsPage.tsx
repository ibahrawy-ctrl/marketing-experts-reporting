// تفاصيل الفريق — أعضاء + KPI لكل عضو + تقارير + إنتاجية + احتياجات تدريب + تصعيدات + مقارنة أسبوعية.
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import {
  useDirectoryUsers,
  useTeams,
  useDepartments,
  useUpdateTeam,
  useAddTeamMember,
  useRemoveTeamMember,
  useTeamMoveImpact,
  useTeamMemberships,
  useAddAdditionalTeamMember,
  useRemoveAdditionalTeamMember,
} from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { DEFAULT_KPI_FILTER, appliedThreshold, useKpiPerformance } from '../lib/useKpi';
import {
  useAllSubmissions,
  useEscalations,
  useDecisions,
  useTrainingNeeds,
  useImprovementPlans,
  healthLabel,
  healthTone,
  teamHealth,
  activeWeeklyKey,
} from '../lib/useOrg';
import { Card, Badge, Button, StatCard, Alert, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle, ProgressBar } from '../components/dashboard';
import { LineTrend } from '../components/Charts';
import { ManagementNotesPanel } from '../components/ManagementNotesPanel';
import {
  submissionStatusLabel,
  kpiTrendDisplay,
  formatPercent,
  trainingNeedStatusLabel,
  decisionStatusLabel,
} from '../lib/format';
import type { SubmissionStatus, SubmissionListItem, TeamDto, DirectoryUserDto, DepartmentDto } from '../types/api';

const SUBMITTED_STATES: SubmissionStatus[] = [
  'Submitted',
  'ApprovedByDirectManager',
  'ApprovedByNextLevel',
  'Escalated',
  'Closed',
  'Visible',
];
const statusTone: Record<SubmissionStatus, 'gold' | 'navy' | 'alert' | 'orange' | 'success'> = {
  Draft: 'gold',
  Submitted: 'navy',
  Returned: 'alert',
  ApprovedByDirectManager: 'navy',
  ApprovedByNextLevel: 'navy',
  Escalated: 'orange',
  Closed: 'success',
  Visible: 'success',
};

export default function TeamDetailsPage() {
  const { teamId = '' } = useParams();
  const { canManageTeams, hasAnyRole } = useAuth();
  const isAdmin = hasAnyRole('Admin');
  const qc = useQueryClient();
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();
  const submissions = useAllSubmissions();
  // P1-KPI-008: درجات الأعضاء ومتوسّط الفريق من الخادم مباشرة (نطاق الفريق مفروض خادميًّا).
  const kpi = useKpiPerformance({ ...DEFAULT_KPI_FILTER, teamId });
  const escalations = useEscalations();
  const decisions = useDecisions();
  const trainingNeeds = useTrainingNeeds();
  const plans = useImprovementPlans();
  const [msg, setMsg] = useState<{ tone: 'success' | 'alert'; text: string } | null>(null);

  const escalate = useMutation({
    mutationFn: async (vars: { targetUserId: string; reason: string }) =>
      (await api.post('/escalations', vars)).data,
    onSuccess: () => {
      setMsg({ tone: 'success', text: 'تم رفع تصعيد إلى قائد الفريق بنجاح.' });
      qc.invalidateQueries({ queryKey: ['escalations'] });
    },
    onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
  });

  if (users.isLoading || teams.isLoading || submissions.isLoading || kpi.isLoading)
    return <LoadingState label="يتم تحميل تفاصيل الفريق…" />;
  if (users.isError || teams.isError || submissions.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          submissions.refetch();
          kpi.refetch();
        }}
        description="حدث خطأ أثناء جلب تفاصيل الفريق. أعد المحاولة."
      />
    );

  const team = (teams.data ?? []).find((t) => t.id === teamId);
  if (!team) {
    return (
      <Card>
        <p className="py-8 text-center text-sm text-ink-2">لم يُعثر على الفريق.</p>
        <div className="text-center">
          <Link to="/app/teams">
            <Button variant="ghost">رجوع للفرق</Button>
          </Link>
        </div>
      </Card>
    );
  }

  const allUsers = users.data ?? [];
  const members = allUsers.filter((u) => u.teamId === team.id);
  const memberIds = new Set(members.map((m) => m.id));
  const leaderName = allUsers.find((u) => u.id === team.teamLeaderId)?.fullName ?? '—';
  const deptList = departments.data ?? [];
  const deptName = deptList.find((d) => d.id === team.departmentId)?.nameAr ?? '—';
  // أعضاء تختلف إدارتهم عن إدارة الفريق — إشارة لوجوب المزامنة (يُصحَّح بحفظ نقل الفريق مع المزامنة).
  const mismatchedMembers = members.filter((m) => m.departmentId !== team.departmentId);
  // فهرسة لا طيّ: صفّ واحد لكلّ موظّف بمتوسّطه كما حسبه الخادم.
  const kpiByUser = new Map((kpi.data?.employees ?? []).map((e) => [e.userId, e]));

  const allSubs = submissions.data ?? [];
  const teamSubs = allSubs.filter((s) => memberIds.has(s.submitterId));
  const weekKey = activeWeeklyKey(allSubs);
  const weekSubs = weekKey
    ? teamSubs.filter((s) => s.periodType === 'Weekly' && s.periodKey === weekKey)
    : [];
  const submittedMembers = new Set(weekSubs.filter((s) => SUBMITTED_STATES.includes(s.status)).map((s) => s.submitterId));
  const submitted = submittedMembers.size;
  const required = members.length;
  const late = Math.max(0, required - submitted);
  const returned = teamSubs.filter((s) => s.status === 'Returned').length;
  const compliance = required === 0 ? 100 : Math.round((submitted / required) * 100);

  // B-2: متوسّط الفريق = متوسّط متوسّطات أعضائه، محسوبًا على الخادم لا هنا.
  const avgKpi = (kpi.data?.teams ?? []).find((t) => t.groupId === team.id)?.measure.value ?? null;
  const teamEscalations = (escalations.data ?? []).filter((e) => memberIds.has(e.targetUserId));
  const openEscalations = teamEscalations.filter((e) => e.status === 'Open').length;
  const health = teamHealth(compliance, avgKpi, openEscalations, appliedThreshold(kpi.data));

  const teamTraining = (trainingNeeds.data ?? []).filter((t) => memberIds.has(t.subjectUserId));
  const teamPlans = (plans.data ?? []).filter((p) => memberIds.has(p.subjectUserId));
  const teamDecisions = (decisions.data ?? []).filter(
    (d) => d.relatedSubmissionId && teamSubs.some((s) => s.id === d.relatedSubmissionId),
  );

  // مقارنة أسبوع بأسبوع: التزام التسليم على آخر أسابيع.
  const weeklyKeys = [...new Set(teamSubs.filter((s) => s.periodType === 'Weekly').map((s) => s.periodKey))]
    .sort()
    .slice(-6);
  const productivityPoints = weeklyKeys.map((k) => {
    const subsForWeek = teamSubs.filter(
      (s) => s.periodType === 'Weekly' && s.periodKey === k && SUBMITTED_STATES.includes(s.status),
    );
    const submittedCount = new Set(subsForWeek.map((s) => s.submitterId)).size;
    return { label: k.replace(/^\d{4}-/, ''), value: required === 0 ? 0 : Math.round((submittedCount / required) * 100) };
  });

  function doEscalate() {
    if (!team!.teamLeaderId) {
      setMsg({ tone: 'alert', text: 'لا يوجد قائد محدّد لهذا الفريق للتصعيد إليه.' });
      return;
    }
    escalate.mutate({
      targetUserId: team!.teamLeaderId,
      reason: `متابعة أداء فريق ${team!.nameAr}: التزام ${compliance}٪، ${late} تقرير متأخر.`,
    });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <Link to="/app/teams" className="text-sm text-orange-600 hover:underline">
            ← الفرق
          </Link>
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-bold text-navy">
            {team.nameAr}
            <Badge tone={healthTone[health]}>{healthLabel[health]}</Badge>
          </h1>
          <p className="mt-1 text-sm text-ink-2">
            {deptName} · القائد: {leaderName} · {members.length} عضو
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link to={`/app/submissions?team=${team.id}`}>
            <Button variant="ghost">كل التقارير</Button>
          </Link>
          <Link to={`/app/kpi?team=${team.id}`}>
            <Button variant="ghost">كل المؤشرات</Button>
          </Link>
          <Button variant="danger" onClick={doEscalate} disabled={escalate.isPending}>
            تصعيد لقائد الفريق
          </Button>
        </div>
      </div>

      {msg && <Alert tone={msg.tone}>{msg.text}</Alert>}

      {canManageTeams && mismatchedMembers.length > 0 && (
        <Alert tone="alert">
          {mismatchedMembers.length} من أعضاء هذا الفريق إدارتهم تختلف عن إدارة الفريق ({deptName}). يحدث هذا عادةً بعد نقل الفريق دون مزامنة الأعضاء. افتح «إدارة الفريق» بالأسفل واحفظ مع تفعيل «مزامنة إدارة الأعضاء» لتصحيح الانتماء.
        </Alert>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
        <StatCard label="الالتزام بالتسليم" value={`${compliance}٪`} tone={compliance < 50 ? 'alert' : 'navy'} />
        <StatCard label="متوسط KPI" value={avgKpi === null ? '—' : formatPercent(avgKpi)} />
        <StatCard label="تقارير متأخرة" value={late} tone={late > 0 ? 'alert' : 'navy'} />
        <StatCard label="تقارير مُرجعة" value={returned} tone={returned > 0 ? 'alert' : 'navy'} />
        <StatCard label="تصعيدات مفتوحة" value={openEscalations} tone={openEscalations > 0 ? 'alert' : 'navy'} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* الأعضاء + KPI */}
        <Card>
          <SectionTitle title="الأعضاء ومؤشّر الأداء" hint={`${members.length} عضو`} />
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">العضو</th>
                  <th className="px-2 py-2 font-semibold">KPI</th>
                  <th className="px-2 py-2 font-semibold">الاتجاه</th>
                  <th className="px-2 py-2 font-semibold">تسليم الأسبوع</th>
                </tr>
              </thead>
              <tbody>
                {members.map((m) => {
                  const row = kpiByUser.get(m.id);
                  const didSubmit = submittedMembers.has(m.id);
                  return (
                    <tr key={m.id} className="border-b border-line last:border-0">
                      <td className="px-2 py-2 font-medium">
                        <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${m.id}`}>
                          {m.fullName}
                        </Link>
                      </td>
                      <td className="px-2 py-2">
                        {row?.measure.value == null ? (
                          <span className="text-ink-2" title="لا يوجد تقييم KPI لهذه الفترة">لا يوجد تقييم</span>
                        ) : (
                          <span className={row.isBelowTarget === true ? 'font-semibold text-alert' : 'text-navy'}>
                            {formatPercent(row.measure.value)}
                          </span>
                        )}
                      </td>
                      <td className="px-2 py-2 text-ink-2">
                        {row ? kpiTrendDisplay(row.measure.trend, row.measure.value != null) : 'لا يوجد تقييم'}
                      </td>
                      <td className="px-2 py-2">
                        {didSubmit ? <Badge tone="success">مُسلّم</Badge> : <Badge tone="gold">لم يُسلّم</Badge>}
                      </td>
                    </tr>
                  );
                })}
                {members.length === 0 && (
                  <tr>
                    <td colSpan={4} className="py-6 text-center text-ink-2">
                      لا أعضاء.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>

        {/* الإنتاجية الأسبوعية */}
        <Card>
          <SectionTitle title="الإنتاجية الأسبوعية" hint="نسبة الالتزام بالتسليم عبر الأسابيع" />
          {productivityPoints.length >= 2 ? (
            <LineTrend points={productivityPoints} />
          ) : (
            <p className="py-10 text-center text-sm text-ink-2">لا توجد بيانات كافية للمقارنة الأسبوعية بعد.</p>
          )}
          <div className="mt-3">
            <div className="mb-1 flex justify-between text-xs text-ink-2">
              <span>التزام الأسبوع الحالي</span>
              <span className="font-semibold">{compliance}٪</span>
            </div>
            <ProgressBar value={compliance} tone={compliance >= 80 ? 'success' : 'orange'} />
          </div>
        </Card>
      </div>

      {/* تقارير الفريق */}
      <Card>
        <SectionTitle
          title="تقارير الفريق"
          hint={`${teamSubs.length} تقرير`}
          action={
            <Link to={`/app/submissions?team=${team.id}`}>
              <Button variant="ghost">الكل</Button>
            </Link>
          }
        />
        <TeamReportsTable rows={teamSubs.slice(0, 10)} />
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* احتياجات التدريب + الخطط */}
        <Card>
          <SectionTitle title="احتياجات التدريب وخطط التطوير" hint={`${teamTraining.length + teamPlans.length} بند`} />
          {teamTraining.length + teamPlans.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد بنود تطوير مفتوحة لهذا الفريق. تظهر هنا الاحتياجات التدريبية وخطط التحسين بعد إنشائها من صفحة «التطوير».</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {teamTraining.map((t) => (
                <li key={t.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <span className="truncate text-navy">{t.title}</span>
                  <Badge tone="navy">{trainingNeedStatusLabel[t.status]}</Badge>
                </li>
              ))}
              {teamPlans.map((p) => (
                <li key={p.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <span className="truncate text-navy">{p.title}</span>
                  <Badge tone="gold">خطة تطوير</Badge>
                </li>
              ))}
            </ul>
          )}
        </Card>

        {/* التصعيدات والقرارات */}
        <Card>
          <SectionTitle title="التصعيدات والقرارات" hint={`${teamEscalations.length + teamDecisions.length} بند`} />
          {teamEscalations.length + teamDecisions.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد تصعيدات أو قرارات مرتبطة بهذا الفريق. تظهر هنا عند رفعها أو تسجيلها من صفحة «الحوكمة».</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {teamEscalations.map((e) => (
                <li key={e.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <span className="truncate text-navy">{e.reason}</span>
                  <Badge tone={e.status === 'Open' ? 'alert' : 'success'}>{e.targetName ?? '—'}</Badge>
                </li>
              ))}
              {teamDecisions.map((d) => (
                <li key={d.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <span className="truncate text-navy">{d.title}</span>
                  <Badge tone="gold">{decisionStatusLabel[d.status]}</Badge>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* إدارة الفريق للمستوى الإداري الأعلى فقط (Admin/CEO/GM؛ + HR لاحقًا) — السياسة مفروضة خادميًّا (TeamManagement ⇒ 403 لغير المخوّلين). */}
      {canManageTeams && <TeamManagementCard team={team} members={members} allUsers={allUsers} departments={deptList} />}

      {/* عضويات الفريق الإضافية (MVP) — Admin فقط. منفصلة تمامًا عن العضوية الأساسية (User.TeamId). */}
      {isAdmin && <AdditionalMembershipsCard team={team} allUsers={allUsers} />}

      {/* الملاحظات الإدارية المرتبطة بهذا الفريق (طبقة سياقية). */}
      <ManagementNotesPanel
        entityType="Team"
        entityId={team.id}
        title="الملاحظات الإدارية على الفريق"
      />
    </div>
  );
}

// البند 2: لوحة إدارة الفريق — تعديل الاسم/القائد + إضافة/إزالة عضو. النطاق مفروض خادميًّا
// (الأدوار الإدارية فقط؛ خارج النطاق ⇒ 403 تظهر كرسالة خطأ). قائمة المستخدمين مُقيّدة بالنطاق أصلًا.
function TeamManagementCard({
  team,
  members,
  allUsers,
  departments,
}: {
  team: TeamDto;
  members: DirectoryUserDto[];
  allUsers: DirectoryUserDto[];
  departments: DepartmentDto[];
}) {
  const update = useUpdateTeam();
  const addMember = useAddTeamMember();
  const removeMember = useRemoveTeamMember();
  const [nameAr, setNameAr] = useState(team.nameAr);
  const [leaderId, setLeaderId] = useState(team.teamLeaderId ?? '');
  const [departmentId, setDepartmentId] = useState(team.departmentId);
  const [syncMembers, setSyncMembers] = useState(true);
  const [addId, setAddId] = useState('');
  const [msg, setMsg] = useState<{ tone: 'success' | 'alert'; text: string } | null>(null);

  const isDepartmentChange = departmentId !== team.departmentId;
  // ملخّص أثر النقل يُجلب من الخادم فقط عند اختيار إدارة مختلفة.
  const impact = useTeamMoveImpact(isDepartmentChange ? team.id : null, isDepartmentChange ? departmentId : null);

  // مرشّحون للإضافة: مستخدمون داخل النطاق وليسوا أعضاء بالفريق بالفعل.
  const memberIds = new Set(members.map((m) => m.id));
  const candidates = allUsers.filter((u) => u.isActive && !memberIds.has(u.id));

  function saveMeta() {
    update.mutate(
      {
        teamId: team.id,
        req: {
          nameAr: nameAr.trim(),
          nameEn: team.nameEn,
          departmentId,
          teamLeaderId: leaderId || null,
          isActive: team.isActive,
          syncMemberDepartments: syncMembers,
        },
      },
      {
        onSuccess: () =>
          setMsg({
            tone: 'success',
            text: isDepartmentChange
              ? 'تم حفظ بيانات الفريق ونقله للإدارة الجديدة' + (syncMembers ? ' مع مزامنة إدارة الأعضاء.' : ' دون مزامنة إدارة الأعضاء.')
              : 'تم حفظ بيانات الفريق.',
          }),
        onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
      },
    );
  }

  function doAdd() {
    if (!addId) return;
    addMember.mutate(
      { teamId: team.id, userId: addId },
      {
        onSuccess: () => {
          setMsg({ tone: 'success', text: 'تمت إضافة العضو للفريق.' });
          setAddId('');
        },
        onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
      },
    );
  }

  function doRemove(userId: string) {
    removeMember.mutate(
      { teamId: team.id, userId },
      {
        onSuccess: () => setMsg({ tone: 'success', text: 'تمت إزالة العضو من الفريق.' }),
        onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
      },
    );
  }

  return (
    <Card>
      <SectionTitle title="إدارة الفريق" hint="تعديل البيانات وإدارة الأعضاء ضمن نطاق صلاحيتك" />
      {msg && (
        <div className="mb-3">
          <Alert tone={msg.tone}>{msg.text}</Alert>
        </div>
      )}

      <div className="grid gap-3 md:grid-cols-2">
        <Field label="اسم الفريق">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field label="قائد الفريق">
          <Select value={leaderId} onChange={(e) => setLeaderId(e.target.value)}>
            <option value="">بدون قائد</option>
            {[...members, ...candidates].map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الإدارة">
          <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.nameAr}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {isDepartmentChange && (
        <div className="mt-3 rounded-lg border border-gold/40 bg-gold/5 p-4">
          <p className="text-sm font-semibold text-navy">ملخّص أثر نقل الفريق</p>
          {impact.isLoading ? (
            <p className="mt-2 text-xs text-ink-2">جارٍ حساب الأثر…</p>
          ) : impact.isError ? (
            <p className="mt-2 text-xs text-alert">تعذّر حساب أثر النقل. تحقّق من الإدارة المختارة.</p>
          ) : impact.data ? (
            <>
              <p className="mt-1 text-xs text-ink-2">
                نقل «{impact.data.teamName}» من «{impact.data.currentDepartmentName ?? '—'}» إلى «{impact.data.targetDepartmentName ?? '—'}».
              </p>
              <dl className="mt-3 grid grid-cols-2 gap-y-2 text-center sm:grid-cols-4">
                <div><dd className="text-lg font-bold text-navy">{impact.data.memberCount}</dd><dt className="text-[11px] text-ink-2">الأعضاء</dt></div>
                <div><dd className="text-lg font-bold text-navy">{impact.data.projectsCount}</dd><dt className="text-[11px] text-ink-2">المشاريع</dt></div>
                <div><dd className="text-lg font-bold text-navy">{impact.data.activeProjectsCount}</dd><dt className="text-[11px] text-ink-2">مشاريع نشطة</dt></div>
                <div><dd className="text-lg font-bold text-navy">{impact.data.submissionsCount}</dd><dt className="text-[11px] text-ink-2">تقارير</dt></div>
              </dl>
              <label className="mt-3 flex items-center gap-2 text-sm text-navy">
                <input type="checkbox" checked={syncMembers} onChange={(e) => setSyncMembers(e.target.checked)} />
                مزامنة إدارة الأعضاء الحاليين إلى الإدارة الجديدة
              </label>
              <p className="mt-1 text-xs text-ink-2">
                {syncMembers
                  ? `عند الحفظ ستُحدَّث إدارة ${impact.data.memberCount} عضوًا (من أعضاء الفريق الحاليين فقط) إلى «${impact.data.targetDepartmentName ?? '—'}». لا يتغيّر المسمّى الوظيفي ولا المدير ولا الفريق.`
                  : 'بدون المزامنة سيبقى أعضاء الفريق على إدارتهم القديمة، ما قد يُحدِث عدم تطابق يؤثّر على التجميع الإداري والتقارير المجمّعة مستقبلًا.'}
              </p>
              {impact.data.warnings.length > 0 && (
                <ul className="mt-2 list-disc space-y-0.5 pr-5 text-xs text-orange-700">
                  {impact.data.warnings.map((w, i) => (
                    <li key={i}>{w}</li>
                  ))}
                </ul>
              )}
            </>
          ) : null}
        </div>
      )}

      <div className="mt-3">
        <Button onClick={saveMeta} disabled={update.isPending || !nameAr.trim()}>
          حفظ بيانات الفريق
        </Button>
      </div>

      <div className="mt-5 border-t border-line pt-4">
        <p className="mb-2 text-sm font-semibold text-navy">الأعضاء</p>
        <ul className="space-y-2 text-sm">
          {members.map((m) => (
            <li key={m.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
              <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${m.id}`}>
                {m.fullName}
              </Link>
              <Button variant="danger" onClick={() => doRemove(m.id)} disabled={removeMember.isPending}>
                إزالة
              </Button>
            </li>
          ))}
          {members.length === 0 && <li className="py-2 text-ink-2">لا أعضاء في هذا الفريق بعد.</li>}
        </ul>

        <div className="mt-3 flex flex-wrap items-end gap-2">
          <div className="min-w-[220px] flex-1">
            <Field label="إضافة عضو">
              <Select value={addId} onChange={(e) => setAddId(e.target.value)}>
                <option value="">اختر مستخدمًا…</option>
                {candidates.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.fullName}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Button onClick={doAdd} disabled={addMember.isPending || !addId}>
            إضافة للفريق
          </Button>
        </div>
        <p className="mt-2 text-xs text-ink-3">
          تظهر هنا فقط الأسماء ضمن نطاق صلاحيتك. محاولة إضافة مستخدم خارج النطاق تُرفض من الخادم.
        </p>
      </div>
    </Card>
  );
}

// MULTI-TEAM-MEMBERSHIP-MVP-R1 — عضويات الفريق الإضافية (Admin فقط).
// عضو إضافي = عضوية ثانوية في فريق آخر دون تغيير الفريق/الإدارة/المدير/المسمّى الأساسي للموظف.
// لا تدخل في النطاق (Scope) ولا KPI ولا التقارير ولا المشاريع — عرض/ربط تنظيمي فقط في هذه المرحلة.
function AdditionalMembershipsCard({
  team,
  allUsers,
}: {
  team: TeamDto;
  allUsers: DirectoryUserDto[];
}) {
  const memberships = useTeamMemberships(team.id);
  const addAdditional = useAddAdditionalTeamMember();
  const removeAdditional = useRemoveAdditionalTeamMember();
  const [addId, setAddId] = useState('');
  const [msg, setMsg] = useState<{ tone: 'success' | 'alert'; text: string } | null>(null);

  const data = memberships.data;
  const primary = data?.primaryMembers ?? [];
  const additional = data?.additionalMembers ?? [];
  const usedIds = new Set([...primary.map((m) => m.userId), ...additional.map((m) => m.userId)]);
  // مرشّحون للإضافة: مستخدمون نشطون ليسوا أعضاء أساسيين أو إضافيين بالفعل.
  const candidates = allUsers.filter((u) => u.isActive && !usedIds.has(u.id));

  function doAdd() {
    if (!addId) return;
    addAdditional.mutate(
      { teamId: team.id, req: { userId: addId } },
      {
        onSuccess: () => {
          setMsg({ tone: 'success', text: 'تمت إضافة العضو الإضافي للفريق دون تغيير فريقه الأساسي.' });
          setAddId('');
        },
        onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
      },
    );
  }

  function doRemove(userId: string) {
    removeAdditional.mutate(
      { teamId: team.id, userId },
      {
        onSuccess: () => setMsg({ tone: 'success', text: 'تمت إزالة العضوية الإضافية.' }),
        onError: (e) => setMsg({ tone: 'alert', text: apiErrorMessage(e) }),
      },
    );
  }

  return (
    <Card>
      <SectionTitle
        title="عضويات الفريق الإضافية"
        hint="عضوية ثانوية لا تغيّر الفريق أو الإدارة الأساسية للموظف (مرحلة أولى — عرض فقط)"
      />
      {msg && (
        <div className="mb-3">
          <Alert tone={msg.tone}>{msg.text}</Alert>
        </div>
      )}

      {memberships.isLoading ? (
        <p className="py-6 text-center text-sm text-ink-2">جارٍ تحميل العضويات…</p>
      ) : memberships.isError ? (
        <p className="py-6 text-center text-sm text-alert">تعذّر تحميل عضويات الفريق.</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {/* الأعضاء الأساسيون (من User.TeamId) — قراءة فقط هنا. */}
          <div>
            <p className="mb-2 text-sm font-semibold text-navy">الأعضاء الأساسيون ({primary.length})</p>
            <ul className="space-y-2 text-sm">
              {primary.map((m) => (
                <li key={m.userId} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${m.userId}`}>
                    {m.fullName}
                  </Link>
                  <Badge tone="navy">أساسي</Badge>
                </li>
              ))}
              {primary.length === 0 && <li className="py-2 text-ink-2">لا أعضاء أساسيون.</li>}
            </ul>
          </div>

          {/* الأعضاء الإضافيون (من UserTeamMemberships) — إضافة/إزالة. */}
          <div>
            <p className="mb-2 text-sm font-semibold text-navy">الأعضاء الإضافيون ({additional.length})</p>
            <ul className="space-y-2 text-sm">
              {additional.map((m) => (
                <li key={m.userId} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                  <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${m.userId}`}>
                    {m.fullName}
                  </Link>
                  <Button variant="danger" onClick={() => doRemove(m.userId)} disabled={removeAdditional.isPending}>
                    إزالة
                  </Button>
                </li>
              ))}
              {additional.length === 0 && <li className="py-2 text-ink-2">لا أعضاء إضافيون بعد.</li>}
            </ul>
          </div>
        </div>
      )}

      <div className="mt-4 flex flex-wrap items-end gap-2 border-t border-line pt-4">
        <div className="min-w-[220px] flex-1">
          <Field label="إضافة عضو إضافي">
            <Select value={addId} onChange={(e) => setAddId(e.target.value)}>
              <option value="">اختر مستخدمًا…</option>
              {candidates.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Button onClick={doAdd} disabled={addAdditional.isPending || !addId}>
          إضافة عضو إضافي
        </Button>
      </div>
      <p className="mt-2 text-xs text-ink-3">
        العضوية الإضافية لا تغيّر الفريق الأساسي ولا الإدارة ولا المدير ولا المسمّى الوظيفي للموظف، ولا تؤثّر على نطاق الرؤية أو مؤشرات الأداء أو التقارير أو المشاريع في هذه المرحلة.
      </p>
    </Card>
  );
}

function TeamReportsTable({ rows }: { rows: SubmissionListItem[] }) {
  if (rows.length === 0) return <p className="py-6 text-center text-sm text-ink-2">لا توجد تقارير لهذا الفريق بعد. تظهر هنا بمجرّد أن يبدأ أعضاؤه في تسليم تقاريرهم.</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[640px] text-right text-sm">
        <thead className="border-b border-line text-xs text-ink-2">
          <tr>
            <th className="px-2 py-2 font-semibold">التقرير</th>
            <th className="px-2 py-2 font-semibold">صاحبه</th>
            <th className="px-2 py-2 font-semibold">الفترة</th>
            <th className="px-2 py-2 font-semibold">الحالة</th>
            <th className="px-2 py-2 font-semibold"></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((s) => (
            <tr key={s.id} className="border-b border-line last:border-0 hover:bg-offwhite">
              <td className="px-2 py-2 font-medium text-navy">{s.templateTitle}</td>
              <td className="px-2 py-2 text-ink-2">{s.submitterName}</td>
              <td className="px-2 py-2 text-ink-2">{s.periodKey}</td>
              <td className="px-2 py-2">
                <Badge tone={statusTone[s.status]}>{submissionStatusLabel[s.status]}</Badge>
              </td>
              <td className="px-2 py-2">
                <Link to={`/app/submissions?open=${s.id}`} className="text-sm font-semibold text-orange-600 hover:underline">
                  فتح
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// طبقة تجميع بيانات الفرق (التسليمات والتصعيدات والخطط) من نقاط النهاية القائمة.
//
// P1-KPI-008 — **أرقام KPI لا تُشتقّ هنا إطلاقًا**. كانت هذه الطبقة تستهلك `/reports/kpi-summary`
// الذي يعيد صفًّا لكلّ تقييم، ثمّ تطويها بـ`new Map(subjectUserId → row)` فيبقى آخر تقييم فقط
// (= «متوسّط الموظّف» يساوي فعليًّا تقييمًا واحدًا)، ثمّ تأخذ متوسّطًا خامًا للأعضاء في الواجهة.
// صار المصدر الوحيد الآن `/api/kpi/performance`: صفّ واحد لكلّ موظّف بمتوسّطه، ومتوسّط كلّ فريق
// محسوبًا على الخادم بالتوسيط ذي المرحلتين (B-2)، والعتبة قادمة من الخادم (B-6).
import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import { kpiTone, type KpiEmployeeScore, type KpiGroupScore } from './useKpi';
import type {
  SubmissionListItem,
  EscalationDto,
  DecisionDto,
  RiskDto,
  ImprovementPlanDto,
  TrainingNeedDto,
  DirectoryUserDto,
  TeamDto,
  DepartmentDto,
  SubmissionStatus,
} from '../types/api';

export function useAllSubmissions() {
  return useQuery({
    queryKey: ['all-submissions'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions')).data,
  });
}

export function useEscalations(status?: string) {
  return useQuery({
    queryKey: ['escalations', status ?? 'all'],
    queryFn: async () =>
      (await api.get<EscalationDto[]>('/escalations', { params: status ? { status } : {} })).data,
  });
}

export function useDecisions(status?: string) {
  return useQuery({
    queryKey: ['decisions', status ?? 'all'],
    queryFn: async () =>
      (await api.get<DecisionDto[]>('/decisions', { params: status ? { status } : {} })).data,
  });
}

export function useRisks(status?: string) {
  return useQuery({
    queryKey: ['risks', status ?? 'all'],
    queryFn: async () =>
      (await api.get<RiskDto[]>('/risks', { params: status ? { status } : {} })).data,
  });
}

export function useImprovementPlans(status?: string) {
  return useQuery({
    queryKey: ['improvement-plans', status ?? 'all'],
    queryFn: async () =>
      (await api.get<ImprovementPlanDto[]>('/improvement-plans', { params: status ? { status } : {} }))
        .data,
  });
}

export function useTrainingNeeds(status?: string) {
  return useQuery({
    queryKey: ['training-needs', status ?? 'all'],
    queryFn: async () =>
      (await api.get<TrainingNeedDto[]>('/training-needs', { params: status ? { status } : {} })).data,
  });
}

// ===== التجميع =====
export type TeamHealth = 'good' | 'watch' | 'risk';

export interface TeamAggregate {
  team: TeamDto;
  departmentName: string;
  leaderName: string;
  memberIds: string[];
  memberCount: number;
  requiredThisWeek: number;
  submitted: number;
  late: number;
  returned: number;
  pendingApproval: number;
  avgKpi: number | null;
  compliance: number; // 0..100
  escalations: number;
  openPlans: number;
  membersBelowTarget: number; // أعضاء حكم الخادم بأنّهم دون المستهدف (بعتبة نسخة القالب)
  noEvaluation: number; // أعضاء بلا تقييم لهذه الفترة
  reasons: string[]; // أسباب الحالة (لماذا «خطر»/«يحتاج متابعة»)
  health: TeamHealth;
}

const HEALTH_RANK: Record<TeamHealth, number> = { risk: 0, watch: 1, good: 2 };

// ترتيب تلقائي: خطر ← يحتاج متابعة ← جيد، ثم الأقل التزامًا أولًا داخل كل فئة.
export function sortTeamAggregates(items: TeamAggregate[]): TeamAggregate[] {
  return [...items].sort(
    (a, b) => HEALTH_RANK[a.health] - HEALTH_RANK[b.health] || a.compliance - b.compliance,
  );
}

const SUBMITTED_STATES: SubmissionStatus[] = [
  'Submitted',
  'ApprovedByDirectManager',
  'ApprovedByNextLevel',
  'Escalated',
  'Closed',
  'Visible',
];

// مفتاح الأسبوع الأحدث الموجود فعلياً في التسليمات الأسبوعية.
export function latestWeeklyKey(subs: SubmissionListItem[]): string | null {
  const keys = subs.filter((s) => s.periodType === 'Weekly').map((s) => s.periodKey);
  if (keys.length === 0) return null;
  return keys.sort().at(-1) ?? null;
}

// أحدث أسبوع فيه بيانات فعلية — لتجنّب عرض أرقام مضلِّلة (مثل «1 مُسلّم / 19 متأخر»)
// حين يكون الأسبوع الأحدث شبه فارغ. نختار أحدث أسبوع تبلغ تسليماته نصف أكثر الأسابيع
// نشاطًا على الأقل، فيتجاوز الأسبوع شبه الفارغ ويعرض الأسبوع المكتمل. (عرض فقط — لا يمسّ
// منطق التقارير أو الاعتماد.)
export function activeWeeklyKey(subs: SubmissionListItem[]): string | null {
  const weekly = subs.filter((s) => s.periodType === 'Weekly');
  if (weekly.length === 0) return null;
  const counts = new Map<string, number>();
  for (const s of weekly) counts.set(s.periodKey, (counts.get(s.periodKey) ?? 0) + 1);
  const max = Math.max(...counts.values());
  const threshold = Math.max(1, max * 0.5);
  const meaningful = [...counts.keys()].filter((k) => (counts.get(k) ?? 0) >= threshold).sort();
  return meaningful.at(-1) ?? latestWeeklyKey(subs);
}

/**
 * حالة الفريق. نسبة الالتزام حساب تسليمات لا حساب KPI، فتبقى هنا؛ أمّا حكم KPI فيُقاس على
 * **العتبة القادمة من الخادم** (B-6) عبر `kpiTone` لا على ثوابت 50/70 كانت مكتوبة هنا.
 * `avgKpi === null` تعني «لا تقييم» فلا تُغلَّظ بها الحالة (لا تقييم ≠ أداء ضعيف).
 */
export function teamHealth(
  compliance: number,
  avgKpi: number | null,
  escalations: number,
  kpiThreshold: number | null,
): TeamHealth {
  const tone = kpiTone(avgKpi, kpiThreshold);
  if (compliance < 50 || tone === 'alert' || escalations >= 3) return 'risk';
  if (compliance < 80 || tone === 'gold' || escalations > 0) return 'watch';
  return 'good';
}

export const healthLabel: Record<TeamHealth, string> = {
  good: 'جيد',
  watch: 'يحتاج متابعة',
  risk: 'خطر',
};

export const healthTone: Record<TeamHealth, 'success' | 'gold' | 'alert'> = {
  good: 'success',
  watch: 'gold',
  risk: 'alert',
};

export function buildTeamAggregates(args: {
  teams: TeamDto[];
  users: DirectoryUserDto[];
  departments: DepartmentDto[];
  submissions: SubmissionListItem[];
  /** صفّ واحد لكلّ **موظّف** بمتوسّطه كما حسبه الخادم — لا صفّ لكلّ تقييم. */
  kpiEmployees: KpiEmployeeScore[];
  /** متوسّط كلّ فريق كما حسبه الخادم (توسيط ذو مرحلتين) — لا يُعاد حسابه هنا. */
  kpiTeams: KpiGroupScore[];
  /** العتبة المطبَّقة القادمة من الخادم (B-6). */
  kpiThreshold: number | null;
  escalations: EscalationDto[];
  plans: ImprovementPlanDto[];
}): TeamAggregate[] {
  const { teams, users, departments, submissions, kpiEmployees, kpiTeams, kpiThreshold, escalations, plans } = args;
  // نعتمد «أحدث أسبوع فيه بيانات فعلية» بدل الأحدث مطلقًا حتى لا تُحتسب تأخيرات وهمية من أسبوع شبه فارغ.
  const weekKey = activeWeeklyKey(submissions);
  // فهرسة لا طيّ: الخادم يضمن صفًّا واحدًا لكلّ موظّف، فلا تُفقَد أيّ تقييمات هنا.
  const kpiByUser = new Map(kpiEmployees.map((e) => [e.userId, e]));
  const kpiByTeam = new Map(kpiTeams.map((g) => [g.groupId, g.measure]));
  const deptName = (id: string) => departments.find((d) => d.id === id)?.nameAr ?? '—';
  const userName = (id: string | null) => users.find((u) => u.id === id)?.fullName ?? '—';

  return teams
    .filter((t) => t.isActive)
    .map((team) => {
      const members = users.filter((u) => u.teamId === team.id);
      const memberIds = members.map((m) => m.id);
      const memberSet = new Set(memberIds);

      const teamSubs = submissions.filter((s) => memberSet.has(s.submitterId));
      const weekSubs = weekKey
        ? teamSubs.filter((s) => s.periodType === 'Weekly' && s.periodKey === weekKey)
        : teamSubs.filter((s) => s.periodType === 'Weekly');

      const submittedMembers = new Set(
        weekSubs.filter((s) => SUBMITTED_STATES.includes(s.status)).map((s) => s.submitterId),
      );
      const requiredThisWeek = memberIds.length;
      const submitted = submittedMembers.size;
      const late = Math.max(0, requiredThisWeek - submitted);
      const returned = teamSubs.filter((s) => s.status === 'Returned').length;
      const pendingApproval = teamSubs.filter(
        (s) => s.status === 'Submitted' || s.status === 'ApprovedByDirectManager' || s.status === 'ApprovedByNextLevel',
      ).length;

      // B-2: متوسّط الفريق يأتي جاهزًا من الخادم. `null` = لا تقييم مؤهَّل، لا صفر.
      const avgKpi = kpiByTeam.get(team.id)?.value ?? null;
      const scoredMembers = memberIds.filter(
        (id) => (kpiByUser.get(id)?.measure.value ?? null) !== null,
      ).length;
      const compliance = requiredThisWeek === 0 ? 100 : Math.round((submitted / requiredThisWeek) * 100);

      const teamEscalations = escalations.filter(
        (e) => e.status === 'Open' && memberSet.has(e.targetUserId),
      ).length;
      const openPlans = plans.filter(
        (p) => (p.status === 'Open' || p.status === 'InProgress') && memberSet.has(p.subjectUserId),
      ).length;

      // B-6: «دون المستهدف» قرار الخادم بعتبة نسخة القالب، لا مقارنة بثابت في الواجهة.
      const membersBelowTarget = memberIds.filter((id) => kpiByUser.get(id)?.isBelowTarget === true).length;
      const noEvaluation = requiredThisWeek === 0 ? 0 : requiredThisWeek - scoredMembers;

      const health = teamHealth(compliance, avgKpi, teamEscalations, kpiThreshold);

      // أسباب الحالة — تُبنى فقط للفرق غير «الجيدة» لتوضيح «لماذا».
      const reasons: string[] = [];
      if (health !== 'good') {
        if (late > 0) reasons.push(`${late} تقرير متأخر هذا الأسبوع`);
        if (returned > 0) reasons.push(`${returned} تقرير مُعاد للتعديل`);
        if (kpiThreshold !== null && avgKpi !== null && avgKpi < kpiThreshold)
          reasons.push(`متوسط KPI ${avgKpi}٪ دون المستهدف`);
        if (membersBelowTarget > 0) reasons.push(`${membersBelowTarget} عضو دون المستهدف`);
        if (noEvaluation > 0) reasons.push(`${noEvaluation} عضو بلا تقييم`);
        if (teamEscalations > 0) reasons.push(`${teamEscalations} تصعيد مفتوح`);
        if (openPlans > 0) reasons.push(`${openPlans} خطة تحسين قائمة`);
      }

      return {
        team,
        departmentName: deptName(team.departmentId),
        leaderName: userName(team.teamLeaderId),
        memberIds,
        memberCount: memberIds.length,
        requiredThisWeek,
        submitted,
        late,
        returned,
        pendingApproval,
        avgKpi,
        compliance,
        escalations: teamEscalations,
        openPlans,
        membersBelowTarget,
        noEvaluation,
        reasons,
        health,
      };
    });
}

// طبقة تجميع بيانات الفرق والمؤشرات من نقاط النهاية القائمة (حساب على جهة العميل).
import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  SubmissionListItem,
  KpiSummaryReport,
  KpiSummaryRow,
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

export function useKpiSummary() {
  return useQuery({
    queryKey: ['kpi-summary-all'],
    queryFn: async () => (await api.get<KpiSummaryReport>('/reports/kpi-summary')).data,
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
  membersBelowTarget: number; // أعضاء مؤشّرهم دون المستهدف (<60)
  noEvaluation: number; // أعضاء بلا تقييم لهذه الفترة
  reasons: string[]; // أسباب الحالة (لماذا «خطر»/«يحتاج متابعة»)
  health: TeamHealth;
}

// عتبة «دون المستهدف» = نفس منطق الباكند (IsBelowTarget = TotalScore < 60).
const KPI_BELOW_TARGET = 60;

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

export function avg(nums: number[]): number | null {
  if (nums.length === 0) return null;
  return Math.round((nums.reduce((a, b) => a + b, 0) / nums.length) * 10) / 10;
}

export function teamHealth(compliance: number, avgKpi: number | null, escalations: number): TeamHealth {
  if (compliance < 50 || (avgKpi !== null && avgKpi < 50) || escalations >= 3) return 'risk';
  if (compliance < 80 || (avgKpi !== null && avgKpi < 70) || escalations > 0) return 'watch';
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
  kpiRows: KpiSummaryRow[];
  escalations: EscalationDto[];
  plans: ImprovementPlanDto[];
}): TeamAggregate[] {
  const { teams, users, departments, submissions, kpiRows, escalations, plans } = args;
  // نعتمد «أحدث أسبوع فيه بيانات فعلية» بدل الأحدث مطلقًا حتى لا تُحتسب تأخيرات وهمية من أسبوع شبه فارغ.
  const weekKey = activeWeeklyKey(submissions);
  const kpiByUser = new Map(kpiRows.map((r) => [r.subjectUserId, r]));
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

      const kpiVals = memberIds
        .map((id) => kpiByUser.get(id)?.totalScore)
        .filter((v): v is number => v !== null && v !== undefined);
      const avgKpi = avg(kpiVals);
      const compliance = requiredThisWeek === 0 ? 100 : Math.round((submitted / requiredThisWeek) * 100);

      const teamEscalations = escalations.filter(
        (e) => e.status === 'Open' && memberSet.has(e.targetUserId),
      ).length;
      const openPlans = plans.filter(
        (p) => (p.status === 'Open' || p.status === 'InProgress') && memberSet.has(p.subjectUserId),
      ).length;

      const membersBelowTarget = memberIds.filter((id) => {
        const s = kpiByUser.get(id)?.totalScore;
        return s !== null && s !== undefined && s < KPI_BELOW_TARGET;
      }).length;
      const noEvaluation = requiredThisWeek === 0 ? 0 : requiredThisWeek - kpiVals.length;

      const health = teamHealth(compliance, avgKpi, teamEscalations);

      // أسباب الحالة — تُبنى فقط للفرق غير «الجيدة» لتوضيح «لماذا».
      const reasons: string[] = [];
      if (health !== 'good') {
        if (late > 0) reasons.push(`${late} تقرير متأخر هذا الأسبوع`);
        if (returned > 0) reasons.push(`${returned} تقرير مُعاد للتعديل`);
        if (avgKpi !== null && avgKpi < 70) reasons.push(`متوسط KPI ${avgKpi}٪ دون المستهدف`);
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

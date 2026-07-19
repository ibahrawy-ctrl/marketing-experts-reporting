// لوحات قيادة مختلفة لكل دور — Role يحدّد الشكل، وReporting Line (الخادم) يحدّد البيانات.
// كل الاستعلامات محصورة بنطاق المستخدم خادمًا (لا تسريب بيانات خارج النطاق).
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../lib/api';
import { Card, Badge, Button } from '../components/ui';
import { LoadingState } from '../components/states';
import { LineTrend, Donut } from '../components/Charts';
import { ReportInsightsSection } from './RoleHomeDashboards';
import {
  SectionTitle,
  MetricTile,
  ActionBanner,
  ActionItem,
  ProgressBar,
  ApprovalPath,
  AlertRow,
  MiniEmpty,
  NeedsActionPanel,
  type PathStep,
  type NeedsActionEntry,
} from '../components/dashboard';
import {
  submissionStatusLabel,
  kpiTrendDisplay,
  riskSeverityLabel,
  improvementPlanStatusLabel,
  trainingNeedStatusLabel,
} from '../lib/format';
import { useReportingCalendar } from '../lib/useReportingCalendar';
import { selectBannerCycleUnified, unifiedEmployeeBanner, unifiedUrgency } from '../lib/unifiedBanner';
import type {
  DashboardDto,
  SummaryCardDto,
  DashboardCardStatus,
  MemberPerformanceDto,
  PendingReportDto,
  ActivityItemDto,
  SubmissionListItem,
  SubmissionCompletenessReport,
  KpiSummaryReport,
  GovernanceSummaryReport,
  EscalationDto,
  DecisionDto,
  RiskDto,
  ImprovementPlanDto,
  TrainingNeedDto,
  SubmissionStatus,
  B2cRollupReport,
  MediaBuyerRollupReport,
  SeoRollupReport,
  ContentWriterRollupReport,
  DesignerRollupReport,
  VideoRollupReport,
  ModerationRollupReport,
  SocialOpsRollupReport,
} from '../types/api';

const tone: Record<DashboardCardStatus, 'navy' | 'success' | 'alert' | 'gold'> = {
  neutral: 'navy',
  green: 'success',
  amber: 'gold',
  red: 'alert',
};

// ===== استعلامات مشتركة (مع تفعيل حسب الحاجة) =====
const useMine = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-mine'], enabled, queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/mine')).data });
const usePendingApprovals = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-pending-approvals'], enabled, queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/pending-approvals')).data });
const useMembers = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-members'], enabled, queryFn: async () => (await api.get<MemberPerformanceDto[]>('/dashboard/members-performance')).data });
const usePendingReports = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-pending-reports'], enabled, queryFn: async () => (await api.get<PendingReportDto[]>('/dashboard/pending-reports')).data });
const useActivity = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-activity'], enabled, queryFn: async () => (await api.get<ActivityItemDto[]>('/dashboard/recent-activity')).data });
const useCompleteness = (key: string, enabled: boolean) =>
  useQuery({ queryKey: ['dash-completeness', key], enabled, queryFn: async () => (await api.get<SubmissionCompletenessReport>('/reports/submission-completeness', { params: { periodKey: key } })).data });
const useKpiSummary = (key: string, enabled: boolean) =>
  useQuery({ queryKey: ['dash-kpi-summary', key], enabled, queryFn: async () => (await api.get<KpiSummaryReport>('/reports/kpi-summary', { params: { periodKey: key } })).data });
const useGovSummary = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-gov-summary'], enabled, queryFn: async () => (await api.get<GovernanceSummaryReport>('/reports/governance-summary')).data });
const useEscalations = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-escalations'], enabled, queryFn: async () => (await api.get<EscalationDto[]>('/escalations')).data });
const useDecisions = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-decisions'], enabled, queryFn: async () => (await api.get<DecisionDto[]>('/decisions')).data });
const useRisks = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-risks'], enabled, queryFn: async () => (await api.get<RiskDto[]>('/risks')).data });
const usePlans = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-plans'], enabled, queryFn: async () => (await api.get<ImprovementPlanDto[]>('/improvement-plans')).data });
const useTraining = (enabled: boolean) =>
  useQuery({ queryKey: ['dash-training'], enabled, queryFn: async () => (await api.get<TrainingNeedDto[]>('/training-needs')).data });
// تجميع مبيعات B2C ضمن نطاق المستخدم — النطاق وحده يحدّد ما يظهر (الموظف أرقامه، القائد فريقه… إلخ).
const useB2cRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-b2c-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<B2cRollupReport>('/reports/b2c-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

const useMediaBuyerRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-mb-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<MediaBuyerRollupReport>('/reports/media-buyer-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// تجميع أداء SEO ضمن نطاق المستخدم — يدمج تقريرَي الفريق والمقالات (Business-1C).
const useSeoRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-seo-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<SeoRollupReport>('/reports/seo-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// تجميع أداء كاتب المحتوى ضمن نطاق المستخدم — من تقرير كاتب المحتوى الأسبوعي (Business-1D-1).
const useContentWriterRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-cw-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<ContentWriterRollupReport>('/reports/content-writer-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// تجميع أداء فريق التصميم ضمن نطاق المستخدم — من تقرير فريق التصميم (Business-1D-2).
const useDesignerRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-designer-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<DesignerRollupReport>('/reports/designer-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// تجميع أداء فريق الفيديو ضمن نطاق المستخدم — من تقرير فريق الفيديو (Business-1D-3).
const useVideoRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-video-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<VideoRollupReport>('/reports/video-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// تجميع أداء المودريشن ضمن نطاق المستخدم — من تقرير المديرشن الأسبوعي (Business-1D-4).
const useModerationRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-moderation-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<ModerationRollupReport>('/reports/moderation-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// ملخّص تشغيل السوشيال ميديا الموحّد — يجمع المحتوى/التصميم/الفيديو/المودريشن (Business-1D-5).
const useSocialOpsRollup = (periodKey: string, enabled: boolean) =>
  useQuery({
    queryKey: ['dash-social-ops-rollup', periodKey],
    enabled,
    queryFn: async () =>
      (await api.get<SocialOpsRollupReport>('/reports/social-ops-rollup', { params: { periodType: 'Weekly', periodKey } })).data,
  });

// شارة الحالة على بطاقات الأرقام.
function card(cards: SummaryCardDto[], key: string) {
  return cards.find((c) => c.key === key);
}

// مسار اعتماد تقريبي من حالة التقرير (لعرض توضيحي على الصفحة الرئيسية).
const CHAIN = ['أنت', 'قائد الفريق', 'المدير', 'المدير العام', 'الرئيس التنفيذي'];
function pathFromStatus(status: SubmissionStatus | null): PathStep[] {
  const idx: Record<string, number> = {
    Draft: 0, Returned: 0, Submitted: 1, ApprovedByDirectManager: 2, ApprovedByNextLevel: 3, Escalated: 3, Closed: 5, Visible: 5,
  };
  const reached = status ? idx[status] ?? 0 : 0;
  const returned = status === 'Returned';
  return CHAIN.map((label, i) => ({
    label,
    state: returned && i === 0 ? 'returned' : i < reached ? 'done' : i === reached ? 'current' : 'todo',
  }));
}

// «دون المستهدف» = حالة KPI رقمية أقل من العتبة 60 (نفس عتبة الباكند IsBelowTarget).
const belowTarget = (m: MemberPerformanceDto) => m.kpiAverage != null && m.kpiAverage < 60;
// «يحتاج دعمًا» = إجراء إداري/تدريبي (مؤشّر منخفض أو اتجاه هابط) — مصطلح إجراء لا حالة رقمية.
const supportNeeded = (m: MemberPerformanceDto) =>
  m.kpiAverage != null && (m.kpiAverage < 70 || m.kpiTrend === 'Down');

// جدول أداء الأعضاء — مُعاد استخدامه (تيم ليدر/مدير).
function MembersCard({ members, title = 'أداء أعضاء الفريق' }: { members?: MemberPerformanceDto[]; title?: string }) {
  return (
    <Card>
      <SectionTitle title={title} hint="متوسط KPI والاتجاه ونسبة إنجاز التقارير" />
      {!members || members.length === 0 ? (
        <MiniEmpty text="لا يوجد أعضاء ضمن نطاقك." hint="يظهر هنا أعضاء فريقك بمجرّد ربطهم بك في «المستخدمون». تواصل مع المسؤول لإضافة أعضاء إلى نطاقك." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2 font-medium">العضو</th>
                <th className="pb-2 font-medium">متوسط KPI</th>
                <th className="pb-2 font-medium">الاتجاه</th>
                <th className="pb-2 font-medium">التقارير</th>
                <th className="pb-2 font-medium">الحالة</th>
              </tr>
            </thead>
            <tbody>
              {members.map((m) => {
                const t = { Up: 'success', Down: 'alert', Flat: 'muted', Unknown: 'muted' }[m.kpiTrend] as 'success' | 'alert' | 'muted';
                const arrow = { Up: '▲', Down: '▼', Flat: '▬', Unknown: '•' }[m.kpiTrend] ?? '•';
                return (
                  <tr key={m.userId} className="border-t border-line">
                    <td className="py-2">
                      <Link to={`/app/employee/${m.userId}`} className="text-navy hover:text-orange-600 hover:underline">{m.name}</Link>
                    </td>
                    <td className="py-2 font-semibold text-navy">{m.kpiAverage ?? <span className="text-ink-2" title="لا يوجد تقييم KPI لهذه الفترة">لا يوجد تقييم</span>}</td>
                    <td className="py-2"><Badge tone={t}>{arrow} {kpiTrendDisplay(m.kpiTrend, m.kpiAverage != null)}</Badge></td>
                    <td className="py-2 text-ink-2">{m.reportsCompleted}/{m.reportsTotal}</td>
                    <td className="py-2">{belowTarget(m) ? <Badge tone="alert">دون المستهدف</Badge> : <Badge tone="success">ضمن المستهدف</Badge>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

// قائمة بانتظار الاعتماد (للمعتمِدين) — تنقل إلى صفحة التقارير/تبويب الاعتماد.
function PendingApprovalsCard({ items }: { items?: SubmissionListItem[] }) {
  return (
    <Card>
      <SectionTitle
        title="بانتظار اعتمادي"
        hint="تقارير مرسلة إليك لاتخاذ قرار"
        action={<Link to="/app/submissions?tab=pending"><Button variant="ghost">فتح الكل</Button></Link>}
      />
      {!items || items.length === 0 ? (
        <MiniEmpty text="لا توجد تقارير بانتظار اعتمادك. أحسنت!" hint="تظهر هنا التقارير عندما يُرسلها أعضاء فريقك لاعتمادك. لا حاجة لأي إجراء الآن." />
      ) : (
        <ul>
          {items.slice(0, 6).map((s) => (
            <ActionItem
              key={s.id}
              title={s.templateTitle}
              context={`${s.submitterName} · ${s.periodKey}`}
              badge={<Badge tone="navy">{submissionStatusLabel[s.status]}</Badge>}
              action={<Link to={`/app/submissions?open=${s.id}`}><Button>مراجعة</Button></Link>}
            />
          ))}
        </ul>
      )}
    </Card>
  );
}

// تقارير تحتاج إجراء (مسودة/معادة/مصعّدة) ضمن النطاق.
function PendingReportsCard({ items, title = 'تقارير متأخرة / تحتاج إجراء' }: { items?: PendingReportDto[]; title?: string }) {
  return (
    <Card>
      <SectionTitle title={title} hint="لم تُرسل بعد أو أُعيدت للتعديل" />
      {!items || items.length === 0 ? (
        <MiniEmpty text="لا توجد تقارير متأخرة." hint="تظهر هنا التقارير غير المُسلّمة أو المُعادة عند تجاوز موعدها. الوضع منضبط حاليًا." />
      ) : (
        <ul>
          {items.slice(0, 8).map((p) => (
            <ActionItem
              key={p.submissionId}
              title={p.templateTitle}
              context={`${p.submitterName} · ${p.periodKey}`}
              badge={<Badge tone="gold">{submissionStatusLabel[p.status]}</Badge>}
            />
          ))}
        </ul>
      )}
    </Card>
  );
}

function GovernanceTiles({ g }: { g?: GovernanceSummaryReport }) {
  if (!g) return null;
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <MetricTile label="مخاطر مفتوحة" value={g.openRisks} tone={g.openRisks > 0 ? 'alert' : 'success'} to="/app/governance" icon="governance" />
      <MetricTile label="تصعيدات مفتوحة" value={g.openEscalations} tone={g.openEscalations > 0 ? 'gold' : 'success'} to="/app/governance" icon="workflow" />
      <MetricTile label="قرارات مفتوحة" value={g.openDecisions} tone="navy" to="/app/governance" icon="governance" />
      <MetricTile label="خطط تدريب مفتوحة" value={g.openTrainingNeeds} tone="navy" to="/app/development" icon="kpi" />
    </div>
  );
}

// ===== لوحة تجميع مبيعات B2C (Business-1A) =====
// النطاق خادمي بالكامل: الموظف يرى أرقامه فقط، القائد فريقه، المدير نطاق المبيعات،
// المدير العام ملخّص الشركة، والرئيس التنفيذي ملخّصًا تنفيذيًا دون تفاصيل المندوبين.
const pct = (v: number) => `${v}٪`;

// شريط أسباب عدم الإغلاق المتكرّرة — مدخل تشغيلي لقائد الفريق/المدير.
function LostReasons({ reasons }: { reasons: string[] }) {
  if (!reasons || reasons.length === 0) return null;
  return (
    <div className="mt-4">
      <p className="mb-2 text-xs font-medium text-ink-2">أكثر أسباب عدم الإغلاق تكرارًا</p>
      <div className="flex flex-wrap gap-2">
        {reasons.map((r, i) => (
          <Badge key={i} tone="gold">{r}</Badge>
        ))}
      </div>
    </div>
  );
}

// جدول أرقام المندوبين (لقائد الفريق/مدير المبيعات) — يُبرز الأفضل والأضعف ومن يحتاج متابعة.
function B2cRepsTable({ r }: { r: B2cRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">المندوب</th>
            <th className="pb-2 font-medium">ليدز</th>
            <th className="pb-2 font-medium">متابعات</th>
            <th className="pb-2 font-medium">تسجيلات</th>
            <th className="pb-2 font-medium">التحويل</th>
            <th className="pb-2 font-medium">تحقيق التارجت</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأفضل أداءً"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{row.leads}</td>
              <td className="py-2 text-ink-2">{row.followUps}</td>
              <td className="py-2 font-semibold text-navy">{row.registrations}</td>
              <td className="py-2 text-ink-2">{pct(row.conversionRate)}</td>
              <td className="py-2 text-ink-2">{pct(row.targetAchievement)}</td>
              <td className="py-2">
                {row.needsFollowUp ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function B2cRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useB2cRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات B2C ضمن نطاقه (تجنّب لوحة فارغة لغير المعنيين).
  if (!r || r.reporters === 0) return null;

  // الموظف: أرقامي هذا الأسبوع (نطاق own → صفّ واحد، فالإجماليات = أرقامي).
  if (variant === 'employee') {
    const me = r.rows[0];
    const onTarget = r.overallTargetAchievement >= 100;
    return (
      <Card>
        <SectionTitle title="أرقامي — مبيعات B2C هذا الأسبوع" hint="من تقريرك الفردي المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="الليدز" value={r.totalLeads} tone="navy" />
          <MetricTile label="المتابعات" value={r.totalFollowUps} tone="navy" />
          <MetricTile label="التسجيلات" value={r.totalRegistrations} tone="success" />
          <MetricTile label="معدل التحويل" value={pct(r.overallConversionRate)} tone={r.overallConversionRate >= 25 ? 'success' : 'gold'} hint="المستهدف 25٪" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">حالتي مقابل التارجت:</span>
          <Badge tone={onTarget ? 'success' : 'gold'}>
            تحقيق التارجت {pct(r.overallTargetAchievement)} {onTarget ? '— ضمن المستهدف' : '— دون المستهدف'}
          </Badge>
          {me?.needsFollowUp && <Badge tone="alert">تحتاج رفع معدل التحويل</Badge>}
        </div>
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل مندوبين.
  if (variant === 'ceo') {
    const gap = Math.round((r.overallTargetAchievement - 100) * 10) / 10;
    const keyRisk = r.worst && r.worst.needsFollowUp ? 'انخفاض معدل التحويل لدى بعض المندوبين' : null;
    return (
      <Card>
        <SectionTitle title="مبيعات B2C — ملخّص تنفيذي" hint="القرار والاتجاه فقط — التفاصيل عبر الإدارة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="معدل التحويل B2C" value={pct(r.overallConversionRate)} tone={r.overallConversionRate >= 25 ? 'success' : 'gold'} />
          <MetricTile label="إجمالي التسجيلات" value={r.totalRegistrations} tone="success" />
          <MetricTile label="الفجوة عن التارجت" value={gap >= 0 ? `+${gap}٪` : `${gap}٪`} tone={gap >= 0 ? 'success' : 'alert'} />
          <MetricTile label="عدد المندوبين" value={r.reporters} tone="navy" />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">خطر رئيسي: {keyRisk}</AlertRow></ul>
        )}
      </Card>
    );
  }

  // المدير العام: ملخّص الشركة — إجماليات + الاتجاه العام + الأفضل/الأضعف.
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="مبيعات B2C — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="إجمالي الليدز" value={r.totalLeads} tone="navy" />
          <MetricTile label="إجمالي التسجيلات" value={r.totalRegistrations} tone="success" />
          <MetricTile label="معدل التحويل العام" value={pct(r.overallConversionRate)} tone={r.overallConversionRate >= 25 ? 'success' : 'gold'} />
          <MetricTile label="تحقيق التارجت" value={pct(r.overallTargetAchievement)} tone={r.overallTargetAchievement >= 100 ? 'success' : 'gold'} />
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأفضل أداءً: </span><span className="font-semibold text-navy">{r.best.name}</span> · {r.best.registrations} تسجيل · {pct(r.best.conversionRate)}</div>}
          {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاج متابعة: </span><span className="font-semibold text-navy">{r.worst.name}</span> · {pct(r.worst.conversionRate)} تحويل</div>}
        </div>
        <LostReasons reasons={r.commonLostReasons} />
      </Card>
    );
  }

  // قائد الفريق / مدير المبيعات: أرقام كل مندوب + إجمالي + الأفضل/الأضعف + من يحتاج متابعة + أسباب متكرّرة.
  const title = variant === 'manager' ? 'مبيعات B2C — مقارنة الفريق' : 'مبيعات B2C — أرقام الفريق';
  const needFollow = r.rows.filter((x) => x.needsFollowUp).length;
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل مندوب ضمن نطاقك — قابلة للتجميع من التقارير الفردية" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد المندوبين" value={r.reporters} tone="navy" />
        <MetricTile label="إجمالي الليدز" value={r.totalLeads} tone="navy" />
        <MetricTile label="إجمالي التسجيلات" value={r.totalRegistrations} tone="success" />
        <MetricTile label="معدل التحويل العام" value={pct(r.overallConversionRate)} tone={r.overallConversionRate >= 25 ? 'success' : 'gold'} hint="المستهدف 25٪" />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأفضل 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">{r.best.registrations} تسجيل · {pct(r.best.conversionRate)} تحويل</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأضعف </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">{pct(r.worst.conversionRate)} تحويل</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollow}</span><div className="text-xs text-ink-2">معدل تحويلهم دون المتوسط</div></div>
      </div>
      <div className="mt-4"><B2cRepsTable r={r} /></div>
      <LostReasons reasons={r.commonLostReasons} />
    </Card>
  );
}

// ===== تجميع أداء الإعلانات Media Buyer (Business-1B) =====
const money = (v: number) => `${v.toLocaleString('en-US')} ج.م`;

// قائمة سياقية (أسباب المشاكل / القرارات المطلوبة).
function ContextChips({ title, items, tone }: { title: string; items: string[]; tone: 'gold' | 'navy' | 'alert' }) {
  if (!items || items.length === 0) return null;
  return (
    <div className="mt-4">
      <p className="mb-2 text-xs font-medium text-ink-2">{title}</p>
      <div className="flex flex-wrap gap-2">
        {items.map((it, i) => (
          <Badge key={i} tone={tone}>{it}</Badge>
        ))}
      </div>
    </div>
  );
}

// جدول أرقام المشترين (لمدير الأداء) — يُبرز الأكفأ والأضعف ومن يحتاج تدخّل.
function MediaBuyersTable({ r }: { r: MediaBuyerRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">مشتري الإعلانات</th>
            <th className="pb-2 font-medium">الإنفاق</th>
            <th className="pb-2 font-medium">الليدز</th>
            <th className="pb-2 font-medium">CPL</th>
            <th className="pb-2 font-medium">CTR</th>
            <th className="pb-2 font-medium">التحويل</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأكفأ إنفاقًا"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{money(row.spend)}</td>
              <td className="py-2 font-semibold text-navy">{row.leads}</td>
              <td className="py-2 text-ink-2">{money(row.cpl)}</td>
              <td className="py-2 text-ink-2">{pct(row.ctr)}</td>
              <td className="py-2 text-ink-2">{pct(row.conversionRate)}</td>
              <td className="py-2">
                {row.needsIntervention ? <Badge tone="alert">يحتاج تدخّل</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function MediaBuyerRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useMediaBuyerRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات إعلانات ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // المشتري الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد، فالإجماليات = أرقامي).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء الإعلانات هذا الأسبوع" hint="من تقريرك الأسبوعي المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="إجمالي الإنفاق" value={money(r.totalSpend)} tone="navy" />
          <MetricTile label="الليدز" value={r.totalLeads} tone="success" />
          <MetricTile label="CPL (الإنفاق/الليدز)" value={money(r.overallCpl)} tone={r.overallCpl > 0 && r.overallCpl <= 50 ? 'success' : 'gold'} hint="المستهدف ≤ 50 ج.م" />
          <MetricTile label="معدل النقر CTR" value={pct(r.averageCtr)} tone={r.averageCtr >= 3 ? 'success' : 'gold'} hint="المستهدف 3٪" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">معدل التحويل:</span>
          <Badge tone={r.averageConversionRate >= 25 ? 'success' : 'gold'}>{pct(r.averageConversionRate)}</Badge>
          {me?.needsIntervention && <Badge tone="alert">CPL مرتفع — يحتاج تحسين الكفاءة</Badge>}
        </div>
        <ContextChips title="القرارات المطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل حملات/مشترين.
  if (variant === 'ceo') {
    const keyRisk = r.commonIssueCauses[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء الإعلانات — ملخّص تنفيذي" hint="القرار والاتجاه فقط — التفاصيل عبر إدارة الأداء" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="إجمالي الإنفاق" value={money(r.totalSpend)} tone="navy" />
          <MetricTile label="إجمالي الليدز" value={r.totalLeads} tone="success" />
          <MetricTile label="CPL العام" value={money(r.overallCpl)} tone={r.overallCpl > 0 && r.overallCpl <= 50 ? 'success' : 'gold'} />
          <MetricTile label="معدل التحويل" value={pct(r.averageConversionRate)} tone={r.averageConversionRate >= 25 ? 'success' : 'gold'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">خطر رئيسي: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + الاتجاه + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء الإعلانات — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="إجمالي الإنفاق" value={money(r.totalSpend)} tone="navy" />
          <MetricTile label="إجمالي الليدز" value={r.totalLeads} tone="success" />
          <MetricTile label="CPL العام" value={money(r.overallCpl)} tone={r.overallCpl > 0 && r.overallCpl <= 50 ? 'success' : 'gold'} />
          <MetricTile label="معدل النقر CTR" value={pct(r.averageCtr)} tone={r.averageCtr >= 3 ? 'success' : 'gold'} />
        </div>
        <ContextChips title="أبرز أسباب المشاكل" items={r.commonIssueCauses} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // مدير الأداء: أرقام كل مشترٍ + إجماليات + الأكفأ/الأضعف + من يحتاج تدخّل.
  const needIntervention = r.rows.filter((x) => x.needsIntervention).length;
  return (
    <Card>
      <SectionTitle title="أداء الإعلانات — مقارنة المشترين" hint="أرقام كل مشترٍ ضمن نطاقك — قابلة للتجميع من التقارير الأسبوعية" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد المشترين" value={r.reporters} tone="navy" />
        <MetricTile label="إجمالي الإنفاق" value={money(r.totalSpend)} tone="navy" />
        <MetricTile label="إجمالي الليدز" value={r.totalLeads} tone="success" />
        <MetricTile label="CPL العام" value={money(r.overallCpl)} tone={r.overallCpl > 0 && r.overallCpl <= 50 ? 'success' : 'gold'} hint="الإنفاق/الليدز" />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأكفأ 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">CPL {money(r.best.cpl)} · {r.best.leads} ليد</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأضعف </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">CPL {money(r.worst.cpl)}</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون تدخّل </span><span className="font-semibold text-navy">{needIntervention}</span><div className="text-xs text-ink-2">CPL أعلى من المتوسط</div></div>
      </div>
      <div className="mt-4"><MediaBuyersTable r={r} /></div>
      <ContextChips title="أبرز أسباب المشاكل" items={r.commonIssueCauses} tone="gold" />
      <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// ===== تجميع أداء SEO (Business-1C) =====
// شارة صافي حركة الكلمات: موجب = نمو (success)، صفر = ثابت (gold)، سالب = تراجع (alert).
const netTone = (v: number): 'success' | 'gold' | 'alert' => (v > 0 ? 'success' : v < 0 ? 'alert' : 'gold');
const netLabel = (v: number) => `${v > 0 ? '+' : ''}${v.toLocaleString('en-US')}`;

// جدول أرقام أعضاء SEO (لقائد الفريق/مدير التخطيط) — يُبرز الأفضل والأحوج للمتابعة.
function SeoMembersTable({ r }: { r: SeoRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">العضو</th>
            <th className="pb-2 font-medium">صافي الكلمات</th>
            <th className="pb-2 font-medium">المهام</th>
            <th className="pb-2 font-medium">مشاكل تقنية</th>
            <th className="pb-2 font-medium">مقالات منشورة</th>
            <th className="pb-2 font-medium">تسليم المحتوى</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأعلى تحسّنًا"> 🏆</span>}
              </td>
              <td className="py-2 font-semibold"><Badge tone={netTone(row.netKeywords)}>{netLabel(row.netKeywords)}</Badge></td>
              <td className="py-2 text-ink-2">{row.tasksDone}</td>
              <td className="py-2 text-ink-2">{row.technicalIssues}</td>
              <td className="py-2 text-ink-2">{row.articlesPublished} / {row.articlesPlanned}</td>
              <td className="py-2 text-ink-2">{pct(row.contentDeliveryRate)}</td>
              <td className="py-2">
                {row.needsFollowup ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function SeoRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useSeoRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات SEO ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // الأخصائي الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء SEO هذا الأسبوع" hint="من تقريرَي الفريق والمقالات المُسلّمَين لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="صافي تحسّن الكلمات" value={netLabel(r.netKeywordMovement)} tone={netTone(r.netKeywordMovement)} hint="تحسّنت − تراجعت" />
          <MetricTile label="المهام المنفّذة" value={r.totalTasksDone} tone="navy" />
          <MetricTile label="المشاكل التقنية" value={r.totalTechnicalIssues} tone={r.totalTechnicalIssues > 0 ? 'gold' : 'success'} />
          <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint="منشورة/مخطّط" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">الصفحات المفهرسة:</span>
          <Badge tone="navy">{r.totalIndexedPages}</Badge>
          <span className="text-sm text-ink-2">Organic Traffic (يدوي):</span>
          <Badge tone="muted">{r.totalOrganicTraffic.toLocaleString('en-US')}</Badge>
          {me?.needsFollowup && <Badge tone="alert">تراجع الكلمات — يحتاج متابعة</Badge>}
        </div>
        <ContextChips title="توصيات الأسبوع القادم" items={r.recommendations} tone="navy" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="gold" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل أعضاء.
  if (variant === 'ceo') {
    const keyRisk = r.decisionsNeeded[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء SEO — ملخّص تنفيذي" hint="الاتجاه والقرار فقط — التفاصيل عبر إدارة التخطيط" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="اتجاه SEO" value={r.netKeywordMovement >= 0 ? 'صاعد' : 'هابط'} tone={netTone(r.netKeywordMovement)} />
          <MetricTile label="صافي حركة الكلمات" value={netLabel(r.netKeywordMovement)} tone={netTone(r.netKeywordMovement)} />
          <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} />
          <MetricTile label="المشاكل التقنية" value={r.totalTechnicalIssues} tone={r.totalTechnicalIssues > 0 ? 'gold' : 'success'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">قرار مطلوب: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + الاتجاه + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء SEO — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="صافي حركة الكلمات" value={netLabel(r.netKeywordMovement)} tone={netTone(r.netKeywordMovement)} />
          <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalArticlesPublished}/${r.totalArticlesPlanned} مقال`} />
          <MetricTile label="المهام المنفّذة" value={r.totalTasksDone} tone="navy" />
          <MetricTile label="المشاكل التقنية" value={r.totalTechnicalIssues} tone={r.totalTechnicalIssues > 0 ? 'gold' : 'success'} />
        </div>
        <ContextChips title="توصيات" items={r.recommendations} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // قائد فريق SEO / مدير التخطيط والجودة: أرقام كل عضو + إجماليات + الأفضل/الأحوج + من يحتاج متابعة.
  const needFollowup = r.rows.filter((x) => x.needsFollowup).length;
  const title = variant === 'leader' ? 'أداء فريق SEO — مقارنة الأعضاء' : 'أداء SEO — ملخّص الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل عضو ضمن نطاقك — مجمّعة من تقارير الفريق والمقالات" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد الأعضاء" value={r.reporters} tone="navy" />
        <MetricTile label="صافي حركة الكلمات" value={netLabel(r.netKeywordMovement)} tone={netTone(r.netKeywordMovement)} hint="تحسّنت − تراجعت" />
        <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalArticlesPublished}/${r.totalArticlesPlanned} مقال`} />
        <MetricTile label="المشاكل التقنية" value={r.totalTechnicalIssues} tone={r.totalTechnicalIssues > 0 ? 'gold' : 'success'} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأعلى تحسّنًا 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">صافي {netLabel(r.best.netKeywords)} كلمة</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأحوج للمتابعة </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">صافي {netLabel(r.worst.netKeywords)} كلمة</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollowup}</span><div className="text-xs text-ink-2">صافي كلمات سالب</div></div>
      </div>
      <div className="mt-4"><SeoMembersTable r={r} /></div>
      <ContextChips title="توصيات الأسبوع القادم" items={r.recommendations} tone="gold" />
      <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// ===== تجميع أداء كاتب المحتوى (Business-1D-1) =====
// شارة نسبة الاعتماد من أول مرة: ≥80 ممتاز (success)، 70-79 مقبول (gold)، <70 يحتاج متابعة (alert).
const approvalTone = (v: number): 'success' | 'gold' | 'alert' => (v >= 80 ? 'success' : v >= 70 ? 'gold' : 'alert');

// جدول أرقام كتّاب المحتوى (لقائد السوشيال/مدير التخطيط) — يُبرز الأفضل والأحوج للمتابعة.
function ContentWritersTable({ r }: { r: ContentWriterRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">الكاتب</th>
            <th className="pb-2 font-medium">مطلوبة</th>
            <th className="pb-2 font-medium">مسلَّمة</th>
            <th className="pb-2 font-medium">اعتماد أول مرة</th>
            <th className="pb-2 font-medium">معادة للتعديل</th>
            <th className="pb-2 font-medium">متأخرة</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأعلى اعتمادًا"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{row.requiredPieces}</td>
              <td className="py-2 text-ink-2">{row.deliveredPieces}</td>
              <td className="py-2 font-semibold"><Badge tone={approvalTone(row.firstApprovalRate)}>{pct(row.firstApprovalRate)}</Badge></td>
              <td className="py-2 text-ink-2">{row.revisedPieces} <span className="text-xs">({pct(row.revisionRate)})</span></td>
              <td className="py-2 text-ink-2">{row.latePieces}</td>
              <td className="py-2">
                {row.needsFollowup ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function ContentWriterRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useContentWriterRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات كتابة محتوى ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // الكاتب الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء كتابة المحتوى هذا الأسبوع" hint="من تقرير كاتب المحتوى الأسبوعي المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="المسلَّمة / المطلوبة" value={`${r.totalDelivered}/${r.totalRequired}`} tone="navy" hint="تسليم المحتوى" />
          <MetricTile label="نسبة الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} hint="معتمدة/مسلَّمة" />
          <MetricTile label="المعادة للتعديل" value={`${r.totalRevised} (${pct(r.revisionRate)})`} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="الالتزام بالخطة" value={pct(r.avgPlanAdherence)} tone={r.avgPlanAdherence >= 80 ? 'success' : 'gold'} hint="نسبة تحقيق المخرجات" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">القطع المتأخرة:</span>
          <Badge tone={r.totalLate > 0 ? 'alert' : 'success'}>{r.totalLate}</Badge>
          {me?.needsFollowup && <Badge tone="alert">يحتاج تحسينًا — اعتماد منخفض أو تأخير</Badge>}
        </div>
        <ContextChips title="ما يحتاج تحسينًا (أسباب التأخير)" items={r.delayReasons} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل كتّاب.
  if (variant === 'ceo') {
    const keyRisk = r.decisionsNeeded[0] ?? r.delayReasons[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء المحتوى — ملخّص تنفيذي" hint="التسليم والاعتماد والقرار فقط — التفاصيل عبر إدارة التخطيط" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequired}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="مخاطر التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'alert' : 'success'} />
          <MetricTile label="قطع متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">أبرز خطر: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + الاعتماد + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء المحتوى — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequired}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="قطع متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        <ContextChips title="أبرز أسباب التأخير" items={r.delayReasons} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // قائد السوشيال / مدير التخطيط والجودة: أرقام كل كاتب + إجماليات + الأفضل/الأحوج + من يحتاج متابعة.
  const needFollowup = r.rows.filter((x) => x.needsFollowup).length;
  const title = variant === 'leader' ? 'أداء كتّاب المحتوى — مقارنة الأعضاء' : 'أداء المحتوى — ملخّص الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل كاتب ضمن نطاقك — مجمّعة من تقارير كتابة المحتوى" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد الكتّاب" value={r.reporters} tone="navy" />
        <MetricTile label="تسليم المحتوى" value={pct(r.contentDeliveryRate)} tone={r.contentDeliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequired}`} />
        <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
        <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} hint={`${r.totalRevised} قطعة`} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأعلى اعتمادًا 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.best.firstApprovalRate)}</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأحوج للمتابعة </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.worst.firstApprovalRate)}</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollowup}</span><div className="text-xs text-ink-2">اعتماد منخفض أو تأخير</div></div>
      </div>
      <div className="mt-4"><ContentWritersTable r={r} /></div>
      <ContextChips title="أبرز أسباب التأخير / التعديلات" items={r.delayReasons} tone="gold" />
      <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// ===== تجميع أداء فريق التصميم (Business-1D-2) =====
// جدول أرقام المصمّمين (لقائد السوشيال/مدير التخطيط) — يُبرز الأفضل والأحوج للمتابعة.
function DesignersTable({ r }: { r: DesignerRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">المصمّم</th>
            <th className="pb-2 font-medium">مطلوبة</th>
            <th className="pb-2 font-medium">مسلَّمة</th>
            <th className="pb-2 font-medium">اعتماد أول مرة</th>
            <th className="pb-2 font-medium">معادة للتعديل</th>
            <th className="pb-2 font-medium">الالتزام بالمواعيد</th>
            <th className="pb-2 font-medium">متأخرة</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأعلى اعتمادًا"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{row.requestedDesigns}</td>
              <td className="py-2 text-ink-2">{row.deliveredDesigns}</td>
              <td className="py-2 font-semibold"><Badge tone={approvalTone(row.firstApprovalRate)}>{pct(row.firstApprovalRate)}</Badge></td>
              <td className="py-2 text-ink-2">{row.revisedDesigns} <span className="text-xs">({pct(row.revisionRate)})</span></td>
              <td className="py-2 text-ink-2">{pct(row.onTimeRate)}</td>
              <td className="py-2 text-ink-2">{row.lateDesigns}</td>
              <td className="py-2">
                {row.needsFollowup ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function DesignerRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useDesignerRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات تصميم ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // المصمّم الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء التصميم هذا الأسبوع" hint="من تقرير فريق التصميم المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="المسلَّمة / المطلوبة" value={`${r.totalDelivered}/${r.totalRequested}`} tone="navy" hint="تسليم التصميم" />
          <MetricTile label="نسبة الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} hint="معتمدة/مسلَّمة" />
          <MetricTile label="المعادة للتعديل" value={`${r.totalRevised} (${pct(r.revisionRate)})`} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="الالتزام بالمواعيد" value={pct(r.onTimeRate)} tone={r.onTimeRate >= 80 ? 'success' : 'gold'} hint="غير متأخرة/مسلَّمة" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">التصاميم المتأخرة:</span>
          <Badge tone={r.totalLate > 0 ? 'alert' : 'success'}>{r.totalLate}</Badge>
          <span className="text-sm text-ink-2">بانتظار المراجعة:</span>
          <Badge tone={r.totalPendingReview > 0 ? 'gold' : 'success'}>{r.totalPendingReview}</Badge>
          {me?.needsFollowup && <Badge tone="alert">يحتاج تحسينًا — اعتماد منخفض أو تأخير</Badge>}
        </div>
        <ContextChips title="ما يحتاج تحسينًا (أسباب التأخير)" items={r.delayReasons} tone="gold" />
        <ContextChips title="مشاكل الهوية / نقص البريف / قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل مصمّمين.
  if (variant === 'ceo') {
    const keyRisk = r.decisionsNeeded[0] ?? r.delayReasons[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء التصميم — ملخّص تنفيذي" hint="التسليم والاعتماد والمخاطر فقط — التفاصيل عبر إدارة التخطيط" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم التصميم" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="مخاطر التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'alert' : 'success'} />
          <MetricTile label="تصاميم متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">أبرز خطر: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + الاعتماد + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء التصميم — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم التصميم" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="تصاميم متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        <ContextChips title="أبرز أسباب التأخير" items={r.delayReasons} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // قائد السوشيال / مدير التخطيط والجودة: أرقام كل مصمّم + إجماليات + الأفضل/الأحوج + من يحتاج متابعة.
  const needFollowup = r.rows.filter((x) => x.needsFollowup).length;
  const title = variant === 'leader' ? 'أداء المصمّمين — مقارنة الأعضاء' : 'أداء التصميم — ملخّص الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل مصمّم ضمن نطاقك — مجمّعة من تقارير فريق التصميم" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد المصمّمين" value={r.reporters} tone="navy" />
        <MetricTile label="تسليم التصميم" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
        <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
        <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} hint={`${r.totalRevised} تصميم`} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأعلى اعتمادًا 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.best.firstApprovalRate)}</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأحوج للمتابعة </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.worst.firstApprovalRate)}</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollowup}</span><div className="text-xs text-ink-2">اعتماد منخفض أو تأخير</div></div>
      </div>
      <div className="mt-4"><DesignersTable r={r} /></div>
      <ContextChips title="أبرز أسباب التأخير / التعديلات" items={r.delayReasons} tone="gold" />
      <ContextChips title="مشاكل الهوية / نقص البريف / قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// ===== تجميع أداء فريق الفيديو (Business-1D-3) =====
// جدول أرقام أعضاء الفيديو (لقائد السوشيال/مدير التخطيط) — يُبرز الأفضل والأحوج للمتابعة.
function VideosTable({ r }: { r: VideoRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">عضو الفيديو</th>
            <th className="pb-2 font-medium">مطلوبة</th>
            <th className="pb-2 font-medium">مسلَّمة</th>
            <th className="pb-2 font-medium">اعتماد أول مرة</th>
            <th className="pb-2 font-medium">معادة للتعديل</th>
            <th className="pb-2 font-medium">الالتزام بالمواعيد</th>
            <th className="pb-2 font-medium">متأخرة</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأعلى اعتمادًا"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{row.requestedVideos}</td>
              <td className="py-2 text-ink-2">{row.deliveredVideos}</td>
              <td className="py-2 font-semibold"><Badge tone={approvalTone(row.firstApprovalRate)}>{pct(row.firstApprovalRate)}</Badge></td>
              <td className="py-2 text-ink-2">{row.revisedVideos} <span className="text-xs">({pct(row.revisionRate)})</span></td>
              <td className="py-2 text-ink-2">{pct(row.onTimeRate)}</td>
              <td className="py-2 text-ink-2">{row.lateVideos}</td>
              <td className="py-2">
                {row.needsFollowup ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function VideoRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useVideoRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات فيديو ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // عضو الفيديو الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء الفيديو هذا الأسبوع" hint="من تقرير فريق الفيديو المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="المسلَّمة / المطلوبة" value={`${r.totalDelivered}/${r.totalRequested}`} tone="navy" hint="تسليم الفيديو" />
          <MetricTile label="نسبة الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} hint="معتمدة/مسلَّمة" />
          <MetricTile label="المعادة للتعديل" value={`${r.totalRevised} (${pct(r.revisionRate)})`} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="الالتزام بالمواعيد" value={pct(r.onTimeRate)} tone={r.onTimeRate >= 80 ? 'success' : 'gold'} hint="غير متأخرة/مسلَّمة" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">الفيديوهات المتأخرة:</span>
          <Badge tone={r.totalLate > 0 ? 'alert' : 'success'}>{r.totalLate}</Badge>
          <span className="text-sm text-ink-2">بانتظار المراجعة:</span>
          <Badge tone={r.totalPendingReview > 0 ? 'gold' : 'success'}>{r.totalPendingReview}</Badge>
          {me?.needsFollowup && <Badge tone="alert">يحتاج تحسينًا — اعتماد منخفض أو تأخير</Badge>}
        </div>
        <ContextChips title="ما يحتاج تحسينًا (أسباب التأخير)" items={r.delayReasons} tone="gold" />
        <ContextChips title="نقص المواد / مشاكل التصوير أو المونتاج / قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل أعضاء.
  if (variant === 'ceo') {
    const keyRisk = r.decisionsNeeded[0] ?? r.delayReasons[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء الفيديو — ملخّص تنفيذي" hint="التسليم والاعتماد والمخاطر فقط — التفاصيل عبر إدارة التخطيط" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم الفيديو" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="مخاطر التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'alert' : 'success'} />
          <MetricTile label="فيديوهات متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">أبرز خطر: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + الاعتماد + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء الفيديو — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="تسليم الفيديو" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
          <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
          <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} />
          <MetricTile label="فيديوهات متأخرة" value={r.totalLate} tone={r.totalLate > 0 ? 'gold' : 'success'} />
        </div>
        <ContextChips title="أبرز أسباب التأخير" items={r.delayReasons} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // قائد السوشيال / مدير التخطيط والجودة: أرقام كل عضو فيديو + إجماليات + الأفضل/الأحوج + من يحتاج متابعة.
  const needFollowup = r.rows.filter((x) => x.needsFollowup).length;
  const title = variant === 'leader' ? 'أداء فريق الفيديو — مقارنة الأعضاء' : 'أداء الفيديو — ملخّص الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل عضو فيديو ضمن نطاقك — مجمّعة من تقارير فريق الفيديو" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد أعضاء الفيديو" value={r.reporters} tone="navy" />
        <MetricTile label="تسليم الفيديو" value={pct(r.deliveryRate)} tone={r.deliveryRate >= 80 ? 'success' : 'gold'} hint={`${r.totalDelivered}/${r.totalRequested}`} />
        <MetricTile label="الاعتماد من أول مرة" value={pct(r.firstApprovalRate)} tone={approvalTone(r.firstApprovalRate)} />
        <MetricTile label="نسبة التعديلات" value={pct(r.revisionRate)} tone={r.revisionRate > 25 ? 'gold' : 'success'} hint={`${r.totalRevised} فيديو`} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأعلى اعتمادًا 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.best.firstApprovalRate)}</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأحوج للمتابعة </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">اعتماد {pct(r.worst.firstApprovalRate)}</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollowup}</span><div className="text-xs text-ink-2">اعتماد منخفض أو تأخير</div></div>
      </div>
      <div className="mt-4"><VideosTable r={r} /></div>
      <ContextChips title="أبرز أسباب التأخير / التعديلات" items={r.delayReasons} tone="gold" />
      <ContextChips title="نقص المواد / مشاكل التصوير أو المونتاج / قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// نسبة الرد: ≥90 ممتاز، ≥75 مقبول، else يحتاج تحسينًا.
const respTone = (v: number): 'success' | 'gold' | 'alert' => (v >= 90 ? 'success' : v >= 75 ? 'gold' : 'alert');

function ModeratorsTable({ r }: { r: ModerationRollupReport }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-right text-ink-2">
            <th className="pb-2 font-medium">المودريتر</th>
            <th className="pb-2 font-medium">واردة</th>
            <th className="pb-2 font-medium">مُجاب عليها</th>
            <th className="pb-2 font-medium">نسبة الرد</th>
            <th className="pb-2 font-medium">متوسط الرد (د)</th>
            <th className="pb-2 font-medium">مصعّدة</th>
            <th className="pb-2 font-medium">شكاوى</th>
            <th className="pb-2 font-medium">فرص محوَّلة</th>
            <th className="pb-2 font-medium">الحالة</th>
          </tr>
        </thead>
        <tbody>
          {r.rows.map((row) => (
            <tr key={row.submitterId} className="border-t border-line">
              <td className="py-2 text-navy">
                {row.name}
                {r.best && row.submitterId === r.best.submitterId && <span className="mr-1" title="الأعلى نسبة رد"> 🏆</span>}
              </td>
              <td className="py-2 text-ink-2">{row.incomingMessages}</td>
              <td className="py-2 text-ink-2">{row.answeredMessages}</td>
              <td className="py-2 font-semibold"><Badge tone={respTone(row.responseRate)}>{pct(row.responseRate)}</Badge></td>
              <td className="py-2 text-ink-2">{row.avgResponseMinutes}</td>
              <td className="py-2 text-ink-2">{row.escalations}</td>
              <td className="py-2 text-ink-2">{row.complaints}</td>
              <td className="py-2 text-ink-2">{row.convertedOpportunities}</td>
              <td className="py-2">
                {row.needsFollowup ? <Badge tone="alert">يحتاج متابعة</Badge> : <Badge tone="success">جيد</Badge>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function ModerationRollupPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'employee' | 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useModerationRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم يكن للمستخدم أي بيانات مودريشن ضمن نطاقه.
  if (!r || r.reporters === 0) return null;

  // المودريتر الفردي: أرقامي هذا الأسبوع (نطاق own → صفّ واحد).
  if (variant === 'employee') {
    const me = r.rows[0];
    return (
      <Card>
        <SectionTitle title="أرقامي — أداء المودريشن هذا الأسبوع" hint="من تقرير المديرشن الأسبوعي المُسلّم لهذه الفترة" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="المُجاب عليها / الواردة" value={`${r.totalAnswered}/${r.totalIncoming}`} tone="navy" hint="حجم المتابعة" />
          <MetricTile label="نسبة الرد" value={pct(r.responseRate)} tone={respTone(r.responseRate)} hint="مُجاب/واردة" />
          <MetricTile label="متوسط سرعة الرد" value={`${r.avgResponseMinutes} د`} tone={r.avgResponseMinutes <= 15 ? 'success' : 'gold'} hint="الأقل أفضل" />
          <MetricTile label="فرص محوَّلة" value={r.totalConverted} tone="success" hint="دعم المبيعات" />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <span className="text-sm text-ink-2">رسائل غير معالجة:</span>
          <Badge tone={r.totalUnhandled > 0 ? 'gold' : 'success'}>{r.totalUnhandled}</Badge>
          <span className="text-sm text-ink-2">مصعّدة:</span>
          <Badge tone={r.totalEscalations > 0 ? 'gold' : 'success'}>{r.totalEscalations}</Badge>
          <span className="text-sm text-ink-2">شكاوى:</span>
          <Badge tone={r.totalComplaints > 0 ? 'alert' : 'success'}>{r.totalComplaints}</Badge>
          {me?.needsFollowup && <Badge tone="alert">يحتاج تحسينًا — نسبة رد منخفضة أو شكاوى</Badge>}
        </div>
        <ContextChips title="الأسئلة المتكررة (FAQ)" items={r.recurringIssues} tone="navy" />
        <ContextChips title="توصيات / أسباب التصعيد" items={r.decisionsNeeded} tone="gold" />
      </Card>
    );
  }

  // الرئيس التنفيذي: ملخّص تنفيذي فقط — لا تفاصيل أعضاء.
  if (variant === 'ceo') {
    const keyRisk = r.decisionsNeeded[0] ?? r.recurringIssues[0] ?? null;
    return (
      <Card>
        <SectionTitle title="أداء المودريشن — ملخّص تنفيذي" hint="نسبة الرد والسرعة والمخاطر فقط — التفاصيل عبر إدارة التخطيط" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="نسبة الرد" value={pct(r.responseRate)} tone={respTone(r.responseRate)} hint={`${r.totalAnswered}/${r.totalIncoming}`} />
          <MetricTile label="متوسط سرعة الرد" value={`${r.avgResponseMinutes} د`} tone={r.avgResponseMinutes <= 15 ? 'success' : 'gold'} />
          <MetricTile label="مخاطر الشكاوى" value={r.totalComplaints} tone={r.totalComplaints > 0 ? 'alert' : 'success'} />
          <MetricTile label="مخاطر التصعيد" value={r.totalEscalations} tone={r.totalEscalations > 0 ? 'gold' : 'success'} />
        </div>
        {keyRisk && (
          <ul className="mt-4"><AlertRow tone="gold">أبرز خطر: {keyRisk}</AlertRow></ul>
        )}
        <ContextChips title="قرارات بانتظار اعتمادك" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // المدير العام: ملخّص الأداء — إجماليات + نسبة الرد + المخاطر (بلا صفوف تفصيلية).
  if (variant === 'gm') {
    return (
      <Card>
        <SectionTitle title="أداء المودريشن — ملخّص المدير العام" hint="الصورة المجمّعة على مستوى نطاقك" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="نسبة الرد" value={pct(r.responseRate)} tone={respTone(r.responseRate)} hint={`${r.totalAnswered}/${r.totalIncoming}`} />
          <MetricTile label="متوسط سرعة الرد" value={`${r.avgResponseMinutes} د`} tone={r.avgResponseMinutes <= 15 ? 'success' : 'gold'} />
          <MetricTile label="الشكاوى" value={r.totalComplaints} tone={r.totalComplaints > 0 ? 'alert' : 'success'} />
          <MetricTile label="الحالات المصعّدة" value={r.totalEscalations} tone={r.totalEscalations > 0 ? 'gold' : 'success'} />
        </div>
        <ContextChips title="الأسئلة المتكررة" items={r.recurringIssues} tone="gold" />
        <ContextChips title="قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
      </Card>
    );
  }

  // قائد السوشيال / مدير التخطيط والجودة: أرقام كل مودريتر + إجماليات + الأفضل/الأحوج + من يحتاج متابعة.
  const needFollowup = r.rows.filter((x) => x.needsFollowup).length;
  const title = variant === 'leader' ? 'أداء فريق المودريشن — مقارنة الأعضاء' : 'أداء المودريشن — ملخّص الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="أرقام كل مودريتر ضمن نطاقك — مجمّعة من تقارير المديرشن الأسبوعي" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="عدد المودريترز" value={r.reporters} tone="navy" />
        <MetricTile label="نسبة الرد" value={pct(r.responseRate)} tone={respTone(r.responseRate)} hint={`${r.totalAnswered}/${r.totalIncoming}`} />
        <MetricTile label="متوسط سرعة الرد" value={`${r.avgResponseMinutes} د`} tone={r.avgResponseMinutes <= 15 ? 'success' : 'gold'} />
        <MetricTile label="الشكاوى / المصعّدة" value={`${r.totalComplaints} / ${r.totalEscalations}`} tone={r.totalComplaints > 0 ? 'alert' : 'success'} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        {r.best && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأعلى نسبة رد 🏆 </span><span className="font-semibold text-navy">{r.best.name}</span><div className="text-xs text-ink-2">رد {pct(r.best.responseRate)}</div></div>}
        {r.worst && <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">الأحوج للمتابعة </span><span className="font-semibold text-navy">{r.worst.name}</span><div className="text-xs text-ink-2">رد {pct(r.worst.responseRate)}</div></div>}
        <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">يحتاجون متابعة </span><span className="font-semibold text-navy">{needFollowup}</span><div className="text-xs text-ink-2">نسبة رد منخفضة أو شكاوى</div></div>
      </div>
      <div className="mt-4"><ModeratorsTable r={r} /></div>
      <ContextChips title="الأسئلة المتكررة (FAQ)" items={r.recurringIssues} tone="gold" />
      <ContextChips title="توصيات / أسباب التصعيد / قرارات مطلوبة" items={r.decisionsNeeded} tone="navy" />
    </Card>
  );
}

// ============================================================
// Business-1D-5 — ملخّص تشغيل السوشيال ميديا الموحّد
// يجمع المحتوى/التصميم/الفيديو/المودريشن في صورة تشغيلية واحدة فوق اللوحات التفصيلية.
// ============================================================
const healthTone = (v: number): 'success' | 'gold' | 'alert' => (v >= 85 ? 'success' : v >= 70 ? 'gold' : 'alert');

export function SocialOpsSummaryPanel({
  periodKey,
  variant,
}: {
  periodKey: string;
  variant: 'leader' | 'manager' | 'gm' | 'ceo';
}) {
  const { data: r, isLoading } = useSocialOpsRollup(periodKey, true);
  if (isLoading) return null;
  // لا تظهر اللوحة إن لم تكن هناك أي بيانات سوشيال ضمن نطاق المستخدم.
  if (!r || r.totalReporters === 0) return null;

  const risks = (
    <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">أكثر مسار يحتاج متابعة </span><span className="font-semibold text-navy">{r.mostNeedsFollowupTrack}</span></div>
      <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">أكثر مسار تأخّرًا </span><span className="font-semibold text-navy">{r.mostDelayedTrack}</span></div>
      <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">أكثر مسار إعادةً </span><span className="font-semibold text-navy">{r.mostRevisedTrack}</span></div>
      <div className="rounded-xl border border-line bg-offwhite p-3 text-sm"><span className="text-ink-2">أكثر مسار شكاوى/تصعيد </span><span className="font-semibold text-navy">{r.mostComplaintsTrack}</span></div>
    </div>
  );

  // المدير العام / الرئيس التنفيذي: ملخّص تنفيذي فقط — صحة التشغيل + أبرز خطر + قرار مطلوب (بلا تفاصيل أعضاء/جداول).
  if (variant === 'gm' || variant === 'ceo') {
    const title = variant === 'ceo' ? 'ملخّص تشغيل السوشيال ميديا — تنفيذي' : 'ملخّص تشغيل السوشيال ميديا — المدير العام';
    return (
      <Card>
        <SectionTitle title={title} hint="صورة موحّدة لمسارات المحتوى والتصميم والفيديو والمودريشن — بلا تفاصيل أعضاء" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="صحة التشغيل" value={`${r.healthScore}٪`} tone={healthTone(r.healthScore)} hint={r.healthLabel} />
          <MetricTile label="عدد المُبلِّغين" value={r.totalReporters} tone="navy" hint="عبر المسارات الأربعة" />
          <MetricTile label="نسبة رد المودريشن" value={pct(r.moderation.responseRate)} tone={respTone(r.moderation.responseRate)} hint={`${r.moderation.answered}/${r.moderation.incoming}`} />
          <MetricTile label="مخاطر الشكاوى/التصعيد" value={`${r.moderation.complaints} / ${r.moderation.escalations}`} tone={r.moderation.complaints > 0 ? 'alert' : 'success'} hint="شكاوى / مصعّدة" />
        </div>
        <ul className="mt-4 space-y-2">
          <AlertRow tone={healthTone(r.healthScore) === 'success' ? 'success' : 'gold'}>أبرز خطر: {r.topRisk}</AlertRow>
          <AlertRow tone="navy">{r.recommendation}</AlertRow>
          {r.decisionNeeded && <AlertRow tone="alert">قرار مطلوب: {r.decisionNeeded}</AlertRow>}
        </ul>
      </Card>
    );
  }

  // قائد السوشيال / مدير التخطيط والجودة: ملخّص المسارات الأربعة + مؤشرات الخطر + التوصية (فوق اللوحات التفصيلية).
  const title = variant === 'leader' ? 'ملخّص تشغيل السوشيال ميديا — فريقي' : 'ملخّص تشغيل السوشيال ميديا — الإدارة';
  return (
    <Card>
      <SectionTitle title={title} hint="صورة موحّدة لمسارات المحتوى والتصميم والفيديو والمودريشن ضمن نطاقك" />
      <div className="mb-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="صحة التشغيل العامة" value={`${r.healthScore}٪`} tone={healthTone(r.healthScore)} hint={r.healthLabel} />
        <MetricTile label="عدد المُبلِّغين" value={r.totalReporters} tone="navy" hint="عبر المسارات الأربعة" />
        <MetricTile label="نسبة رد المودريشن" value={pct(r.moderation.responseRate)} tone={respTone(r.moderation.responseRate)} hint={`${r.moderation.answered}/${r.moderation.incoming}`} />
        <MetricTile label="الشكاوى / المصعّدة" value={`${r.moderation.complaints} / ${r.moderation.escalations}`} tone={r.moderation.complaints > 0 ? 'alert' : 'success'} />
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <TrackSummaryCard title="المحتوى" reporters={r.content.reporters} a={`مطلوبة ${r.content.required}`} b={`مسلَّمة ${r.content.delivered}`} c={`اعتماد أول مرة ${pct(r.content.firstApprovalRate)}`} followup={r.content.needsFollowup} approval={r.content.firstApprovalRate} />
        <TrackSummaryCard title="التصميم" reporters={r.design.reporters} a={`مطلوبة ${r.design.requested}`} b={`مسلَّمة ${r.design.delivered}`} c={`اعتماد أول مرة ${pct(r.design.firstApprovalRate)} · متأخرة ${r.design.late}`} followup={r.design.needsFollowup} approval={r.design.firstApprovalRate} late={r.design.late} />
        <TrackSummaryCard title="الفيديو" reporters={r.video.reporters} a={`مطلوبة ${r.video.requested}`} b={`مسلَّمة ${r.video.delivered}`} c={`اعتماد أول مرة ${pct(r.video.firstApprovalRate)} · متأخرة ${r.video.late}`} followup={r.video.needsFollowup} approval={r.video.firstApprovalRate} late={r.video.late} />
        <TrackSummaryCard title="المودريشن" reporters={r.moderation.reporters} a={`واردة ${r.moderation.incoming}`} b={`مُجاب ${r.moderation.answered}`} c={`نسبة الرد ${pct(r.moderation.responseRate)} · سرعة ${r.moderation.avgResponseMinutes}د`} response={r.moderation.responseRate} />
      </div>
      {risks}
      <ul className="mt-4 space-y-2">
        <AlertRow tone={healthTone(r.healthScore) === 'success' ? 'success' : 'gold'}>أبرز خطر: {r.topRisk}</AlertRow>
        <AlertRow tone="navy">{r.recommendation}</AlertRow>
        {r.decisionNeeded && <AlertRow tone="alert">قرار مطلوب: {r.decisionNeeded}</AlertRow>}
      </ul>
    </Card>
  );
}

// بطاقة ملخّص مسار واحد ضمن ملخّص السوشيال الموحّد.
function TrackSummaryCard({
  title, reporters, a, b, c, followup, approval, response, late,
}: {
  title: string;
  reporters: number;
  a: string;
  b: string;
  c: string;
  followup?: number;
  approval?: number;
  response?: number;
  late?: number;
}) {
  const tone: 'success' | 'gold' | 'alert' =
    response !== undefined ? respTone(response)
    : approval !== undefined ? (approval >= 80 ? 'success' : approval >= 70 ? 'gold' : 'alert')
    : 'navy' as never;
  return (
    <div className="rounded-2xl border border-line bg-white p-4">
      <div className="flex items-center justify-between">
        <span className="font-semibold text-navy">{title}</span>
        <Badge tone={tone}>{reporters} مُبلِّغ</Badge>
      </div>
      <div className="mt-2 space-y-1 text-sm text-ink-2">
        <div>{a}</div>
        <div>{b}</div>
        <div>{c}</div>
      </div>
      <div className="mt-2 flex flex-wrap gap-2">
        {followup !== undefined && followup > 0 && <Badge tone="alert">{followup} يحتاج متابعة</Badge>}
        {late !== undefined && late > 0 && <Badge tone="gold">{late} متأخرة</Badge>}
      </div>
    </div>
  );
}

// ============================================================
// 1) الموظف
// ============================================================
export function EmployeeDashboard({ dash, kpiDelta }: { dash: DashboardDto; kpiDelta: { value: number; up: boolean } | null }) {
  const { data: mine } = useMine(true);
  const { data: plans } = usePlans(true);
  const { data: training } = useTraining(true);
  // REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — مصدر الحالة الوحيد للّافتة = الحقل الموحّد الخادميّ.
  // نقرأ دورات الموظّف (past محدود) لنلتقط دورةً ماضيةً «متأخّرة غير مُسلَّمة» قد لا يكون لها صفّ تسليم أصلًا،
  // ثمّ نختار الدورة التي عيّنها الخادم «مطلوب إجراؤها الآن» (isCurrentPriority) بدل الاعتماد على mine[0].
  const { data: cycles } = useReportingCalendar({ context: 'Report', past: 8, future: 1 });
  const cur = mine?.[0] ?? null;
  const status = cur?.status ?? null;

  const bannerUnified = selectBannerCycleUnified(cycles?.cycles);
  // نستعمل المسار الموحّد فقط حين يكون الموظّف مُسنَدًا وحالته ليست «غير مطلوب» — وإلّا نرجع للمسار القديم
  // (يحمي مُقدّمي التقارير اليوميّة الذين لا يستهلكون my-cycles، فلا انحدار في سلوكهم).
  const useUnified =
    !!bannerUnified &&
    bannerUnified.unifiedStatus !== 'NotAssigned' &&
    bannerUnified.unifiedStatus !== 'NotRequired';

  const legacyBanner = (() => {
    if (!cur || status === 'Draft')
      return { title: 'تقريرك مطلوب الآن', desc: cur ? 'لديك مسودة غير مكتملة — أكملها وأرسلها للاعتماد.' : `تقرير ${dash.period.label} بانتظار البدء.`, cta: 'ابدأ / أكمل التقرير', t: 'orange' as const, to: '/app/submissions' };
    if (status === 'Returned')
      return { title: 'تقريرك أُعيد للتعديل', desc: 'راجع ملاحظات مديرك وعدّل التقرير ثم أعد إرساله.', cta: 'عدّل الآن', t: 'orange' as const, to: '/app/submissions' };
    if (status === 'Closed' || status === 'Visible')
      return { title: 'تم اعتماد تقريرك بالكامل', desc: 'اكتملت سلسلة الاعتماد حتى الإدارة العليا.', cta: 'عرض التقرير', t: 'success' as const, to: '/app/submissions' };
    return { title: 'تم إرسال تقريرك', desc: 'تقريرك قيد المراجعة في سلسلة الاعتماد.', cta: 'متابعة الحالة', t: 'navy' as const, to: '/app/submissions' };
  })();

  const banner = useUnified
    ? (() => { const b = unifiedEmployeeBanner(bannerUnified!); return { title: b.title, desc: b.description, cta: b.cta, t: b.tone, to: b.to }; })()
    : legacyBanner;

  const actions: NeedsActionEntry[] = [];
  if (useUnified && bannerUnified!.isCurrentPriority) {
    const b = unifiedEmployeeBanner(bannerUnified!);
    actions.push({ id: `cycle-${bannerUnified!.periodKey}`, title: b.title, context: bannerUnified!.statusDescription || bannerUnified!.cycleLabel, urgency: unifiedUrgency(bannerUnified!.severity), to: b.to, cta: b.cta });
  } else if (!useUnified && !cur)
    actions.push({ id: 'start', title: `ابدأ تقرير ${dash.period.label}`, context: 'لم تبدأ تقرير هذه الفترة بعد', urgency: 'high', to: '/app/submissions', cta: 'ابدأ' });
  else if (!useUnified && status === 'Draft')
    actions.push({ id: 'draft', title: 'أكمل مسودة تقريرك وأرسلها', context: 'لديك مسودة غير مكتملة', urgency: 'high', to: `/app/submissions?open=${cur!.id}`, cta: 'أكمل' });
  else if (!useUnified && status === 'Returned')
    actions.push({ id: 'returned', title: 'تقريرك أُعيد للتعديل', context: 'راجع ملاحظة المعتمِد وعدّل ثم أعد الإرسال', urgency: 'high', to: `/app/submissions?open=${cur!.id}`, cta: 'عدّل' });
  (plans ?? [])
    .filter((p) => p.status === 'Open' || p.status === 'InProgress')
    .slice(0, 3)
    .forEach((p) => actions.push({ id: `plan-${p.id}`, title: `خطة تحسين: ${p.title}`, context: improvementPlanStatusLabel[p.status], urgency: 'medium', to: '/app/development', cta: 'متابعة' }));
  (training ?? [])
    .filter((t) => t.status === 'Open' || t.status === 'Planned' || t.status === 'InProgress')
    .slice(0, 3)
    .forEach((t) => actions.push({ id: `need-${t.id}`, title: `احتياج تدريبي: ${t.title}`, context: trainingNeedStatusLabel[t.status], urgency: 'low', to: '/app/development', cta: 'عرض' }));

  return (
    <div className="space-y-6">
      <ActionBanner title={banner.title} description={banner.desc} tone={banner.t}
        cta={<Link to={banner.to}><Button variant="inverted">{banner.cta}</Button></Link>} />

      <NeedsActionPanel
        items={actions}
        emptyText="تقريرك مُرسَل ولا توجد خطط أو احتياجات مفتوحة — لا إجراء مطلوب الآن."
        emptyHint="ستظهر هنا أي مهمة تخصّك: تقرير متأخر، تقرير مُعاد، أو خطة تحسين/تدريب مفتوحة."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="حالة تقريري" value={status ? submissionStatusLabel[status] : 'لم يبدأ'} tone={status === 'Returned' ? 'alert' : status === 'Closed' ? 'success' : 'navy'} />
        <MetricTile label="متوسط KPI" value={card(dash.summaryCards, 'kpiAverage')?.value ?? '—'} tone={tone[card(dash.summaryCards, 'kpiAverage')?.status ?? 'neutral']} delta={kpiDelta} hint="مقارنة بالفترة السابقة" />
        <MetricTile label="تحتاج إجراء" value={card(dash.summaryCards, 'needsAction')?.value ?? 0} tone={tone[card(dash.summaryCards, 'needsAction')?.status ?? 'neutral']} />
        <MetricTile label="تقارير مكتملة" value={card(dash.summaryCards, 'completedReports')?.value ?? 0} tone="success" />
      </div>

      <B2cRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <MediaBuyerRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <SeoRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <ContentWriterRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <DesignerRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <VideoRollupPanel periodKey={dash.period.periodKey} variant="employee" />
      <ModerationRollupPanel periodKey={dash.period.periodKey} variant="employee" />

      <Card>
        <SectionTitle title="مسار اعتماد تقريرك" hint="إلى أين وصل تقريرك في سلسلة الاعتماد" />
        <ApprovalPath steps={pathFromStatus(status)} />
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle title="تقدّم KPI" hint="آخر الفترات" />
          <LineTrend points={(((dash.widgets.find((w) => w.key === 'kpiTrend')?.data as { periodKey: string; value: number }[]) ?? []).map((d) => ({ label: d.periodKey, value: d.value })))} />
        </Card>
        <Card>
          <SectionTitle title="خطط التحسين واحتياجات التدريب الخاصة بي" action={<Link to="/app/development"><Button variant="ghost">التفاصيل</Button></Link>} />
          {(!plans || plans.length === 0) && (!training || training.length === 0) ? (
            <MiniEmpty text="لا توجد خطط أو احتياجات حالية." hint="تظهر هنا خطط التحسين والاحتياجات التدريبية بعد إنشائها من صفحة «التطوير»." />
          ) : (
            <ul>
              {plans?.slice(0, 4).map((p) => <ActionItem key={p.id} title={p.title} context="خطة تحسين" badge={<Badge tone="navy">{improvementPlanStatusLabel[p.status]}</Badge>} />)}
              {training?.slice(0, 3).map((t) => <ActionItem key={t.id} title={t.title} context="احتياج تدريب" badge={<Badge tone="gold">{trainingNeedStatusLabel[t.status]}</Badge>} />)}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}

// ============================================================
// 2) قائد الفريق
// ============================================================
export function TeamLeaderDashboard({ dash }: { dash: DashboardDto }) {
  const { data: approvals } = usePendingApprovals(true);
  const { data: members } = useMembers(true);
  const { data: pending } = usePendingReports(true);
  const { data: escal } = useEscalations(true);
  const needApproval = approvals?.length ?? 0;

  const actions: NeedsActionEntry[] = [];
  (approvals ?? []).slice(0, 5).forEach((s) =>
    actions.push({ id: `appr-${s.id}`, title: `اعتمِد: ${s.templateTitle}`, context: `${s.submitterName} · ${s.periodKey}`, urgency: 'high', to: `/app/submissions?open=${s.id}`, cta: 'مراجعة' }));
  (escal ?? []).filter((e) => e.status === 'Open').slice(0, 3).forEach((e) =>
    actions.push({ id: `esc-${e.id}`, title: `تصعيد مفتوح: ${e.reason}`, context: e.targetName ?? '—', urgency: 'high', to: '/app/governance', cta: 'معالجة' }));
  (pending ?? []).slice(0, 4).forEach((p) =>
    actions.push({ id: `late-${p.submissionId}`, title: `تقرير متأخر/مُعاد: ${p.templateTitle}`, context: `${p.submitterName} · ${submissionStatusLabel[p.status]}`, urgency: 'medium', to: '/app/submissions?tab=all', cta: 'متابعة' }));
  (members ?? []).filter(supportNeeded).slice(0, 3).forEach((m) =>
    actions.push({ id: `sup-${m.userId}`, title: `عضو يحتاج دعمًا: ${m.name}`, context: m.kpiAverage != null ? `متوسط KPI ${m.kpiAverage}${m.kpiTrend === 'Down' ? ' · اتجاه هابط' : ''}` : 'اتجاه هابط', urgency: 'medium', to: '/app/development', cta: 'خطة' }));

  return (
    <div className="space-y-6">
      <ActionBanner
        title={needApproval > 0 ? `${needApproval} تقارير بانتظار اعتمادك` : 'لا تقارير معلّقة لاعتمادك'}
        description={needApproval > 0 ? 'راجع تقارير فريقك واتخذ القرار المناسب.' : 'فريقك على المسار الصحيح هذا الأسبوع.'}
        tone={needApproval > 0 ? 'orange' : 'success'}
        cta={<Link to="/app/submissions?tab=pending"><Button variant="inverted">مراجعة الاعتمادات</Button></Link>}
      />

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      <NeedsActionPanel
        items={actions}
        emptyText="لا اعتمادات معلّقة ولا تصعيدات ولا أعضاء يحتاجون دعمًا — فريقك منضبط."
        emptyHint="ستظهر هنا التقارير بانتظار اعتمادك، التصعيدات المفتوحة، التقارير المتأخرة، والأعضاء دون المستهدف."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="بانتظار اعتمادي" value={needApproval} tone={needApproval > 0 ? 'gold' : 'success'} icon="workflow" />
        <MetricTile label="متوسط KPI للفريق" value={card(dash.summaryCards, 'kpiAverage')?.value ?? '—'} tone={tone[card(dash.summaryCards, 'kpiAverage')?.status ?? 'neutral']} icon="kpi" />
        <MetricTile label="تقارير متأخرة" value={pending?.length ?? 0} tone={(pending?.length ?? 0) > 0 ? 'alert' : 'success'} icon="reports" />
        <MetricTile label="أعضاء دون المستهدف" value={members?.filter(belowTarget).length ?? 0} tone={(members?.filter(belowTarget).length ?? 0) > 0 ? 'alert' : 'success'} hint="قد يحتاجون إلى دعم أو خطة تحسين" icon="teams" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <PendingApprovalsCard items={approvals} />
        <PendingReportsCard items={pending} />
      </div>

      <B2cRollupPanel periodKey={dash.period.periodKey} variant="leader" />
      <SeoRollupPanel periodKey={dash.period.periodKey} variant="leader" />
      <SocialOpsSummaryPanel periodKey={dash.period.periodKey} variant="leader" />
      <ContentWriterRollupPanel periodKey={dash.period.periodKey} variant="leader" />
      <DesignerRollupPanel periodKey={dash.period.periodKey} variant="leader" />
      <VideoRollupPanel periodKey={dash.period.periodKey} variant="leader" />
      <ModerationRollupPanel periodKey={dash.period.periodKey} variant="leader" />

      <MembersCard members={members} />

      <Card>
        <SectionTitle title="تصعيدات الفريق" action={<Link to="/app/governance"><Button variant="ghost">الحوكمة</Button></Link>} />
        {!escal || escal.length === 0 ? <MiniEmpty text="لا توجد تصعيدات مفتوحة." hint="تظهر هنا التصعيدات حين يرفعها أحد أعضاء الفريق إلى الإدارة. لا شيء يتطلّب تدخّلك الآن." /> : (
          <ul>{escal.slice(0, 5).map((e) => <AlertRow key={e.id} tone="gold">{e.reason} — {e.targetName ?? '—'}</AlertRow>)}</ul>
        )}
      </Card>
    </div>
  );
}

// ============================================================
// 3) المدير
// ============================================================
export function ManagerDashboard({ dash }: { dash: DashboardDto }) {
  const { data: approvals } = usePendingApprovals(true);
  const { data: members } = useMembers(true);
  const { data: pending } = usePendingReports(true);
  const { data: gov } = useGovSummary(true);
  const { data: kpi } = useKpiSummary(dash.period.periodKey, true);

  const actions: NeedsActionEntry[] = [];
  (approvals ?? []).slice(0, 4).forEach((s) =>
    actions.push({ id: `appr-${s.id}`, title: `اعتمِد: ${s.templateTitle}`, context: `${s.submitterName} · ${s.periodKey}`, urgency: 'high', to: `/app/submissions?open=${s.id}`, cta: 'مراجعة' }));
  if ((gov?.openRisks ?? 0) > 0)
    actions.push({ id: 'risks', title: `${gov!.openRisks} مخاطر مفتوحة في القسم`, context: 'تحتاج خطة تخفيف أو متابعة', urgency: 'high', to: '/app/governance', cta: 'الحوكمة' });
  if ((kpi?.belowTarget ?? 0) > 0)
    actions.push({ id: 'below', title: `${kpi!.belowTarget} موظف دون المستهدف KPI`, context: 'راجع التقييمات وافتح خطط تحسين', urgency: 'medium', to: '/app/kpi', cta: 'المؤشرات' });
  (pending ?? []).slice(0, 4).forEach((p) =>
    actions.push({ id: `late-${p.submissionId}`, title: `تقرير متأخر/مُعاد: ${p.templateTitle}`, context: `${p.submitterName} · ${submissionStatusLabel[p.status]}`, urgency: 'medium', to: '/app/submissions?tab=all', cta: 'متابعة' }));
  (members ?? []).filter(supportNeeded).slice(0, 2).forEach((m) =>
    actions.push({ id: `sup-${m.userId}`, title: `عضو يحتاج دعمًا: ${m.name}`, context: m.kpiAverage != null ? `متوسط KPI ${m.kpiAverage}` : 'اتجاه هابط', urgency: 'low', to: '/app/development', cta: 'خطة' }));

  return (
    <div className="space-y-6">
      <ActionBanner
        title="إدارة القسم — متابعة القادة والفرق"
        description="أنت مسؤول عن إدارة قادة الفرق وجودة مراجعتهم، لا عن قراءة كل تقرير فردي."
        tone="navy"
        cta={<Link to="/app/reports"><Button variant="inverted">تقارير القسم</Button></Link>}
      />

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      <NeedsActionPanel
        items={actions}
        emptyText="لا اعتمادات معلّقة ولا مخاطر ولا تقارير متأخرة في قسمك."
        emptyHint="ستظهر هنا الاعتمادات المعلّقة، المخاطر المفتوحة، الموظفون دون المستهدف، والتقارير المتأخرة."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="متوسط KPI للقسم" value={card(dash.summaryCards, 'kpiAverage')?.value ?? '—'} tone={tone[card(dash.summaryCards, 'kpiAverage')?.status ?? 'neutral']} icon="kpi" />
        <MetricTile label="اعتمادات معلّقة" value={approvals?.length ?? 0} tone={(approvals?.length ?? 0) > 0 ? 'gold' : 'success'} icon="workflow" />
        <MetricTile label="تقارير متأخرة" value={pending?.length ?? 0} tone={(pending?.length ?? 0) > 0 ? 'alert' : 'success'} icon="reports" />
        <MetricTile label="موظفون دون المستهدف" value={kpi?.belowTarget ?? 0} tone={(kpi?.belowTarget ?? 0) > 0 ? 'alert' : 'success'} hint="قد يحتاجون إلى دعم أو خطة تحسين" icon="teams" />
      </div>

      <GovernanceTiles g={gov} />

      <div className="grid gap-4 lg:grid-cols-2">
        <PendingApprovalsCard items={approvals} />
        <PendingReportsCard items={pending} />
      </div>

      <B2cRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <MediaBuyerRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <SeoRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <SocialOpsSummaryPanel periodKey={dash.period.periodKey} variant="manager" />
      <ContentWriterRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <DesignerRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <VideoRollupPanel periodKey={dash.period.periodKey} variant="manager" />
      <ModerationRollupPanel periodKey={dash.period.periodKey} variant="manager" />

      <MembersCard members={members} title="أداء الفرق والأعضاء" />
    </div>
  );
}

// ============================================================
// 4) المدير العام
// ============================================================
export function GMDashboard({ dash }: { dash: DashboardDto }) {
  const { data: comp } = useCompleteness(dash.period.periodKey, true);
  const { data: kpi } = useKpiSummary(dash.period.periodKey, true);
  const { data: gov } = useGovSummary(true);
  const { data: approvals } = usePendingApprovals(true);
  const lateDeptList = comp?.byDepartment.filter((d) => d.completionRate < 100) ?? [];
  const lateDepts = lateDeptList.length;

  const actions: NeedsActionEntry[] = [];
  if ((gov?.openRisks ?? 0) > 0)
    actions.push({ id: 'risks', title: `${gov!.openRisks} مخاطر مفتوحة على مستوى الشركة`, context: 'تحتاج قرارًا أو خطة تخفيف', urgency: 'high', to: '/app/governance', cta: 'الحوكمة' });
  (approvals ?? []).slice(0, 4).forEach((s) =>
    actions.push({ id: `appr-${s.id}`, title: `اعتمِد: ${s.templateTitle}`, context: `${s.submitterName} · ${s.periodKey}`, urgency: 'high', to: `/app/submissions?open=${s.id}`, cta: 'مراجعة' }));
  lateDeptList.slice(0, 5).forEach((d) =>
    actions.push({ id: `dept-${d.departmentId ?? d.departmentName}`, title: `قسم متأخر: ${d.departmentName}`, context: `اكتمال ${d.completionRate}٪ (${d.closed}/${d.total})`, urgency: 'medium', to: '/app/reports', cta: 'تفاصيل' }));
  if ((kpi?.belowTarget ?? 0) > 0)
    actions.push({ id: 'below', title: `${kpi!.belowTarget} موظف دون المستهدف KPI`, context: 'متابعة على مستوى الأقسام', urgency: 'medium', to: '/app/kpi', cta: 'المؤشرات' });

  return (
    <div className="space-y-6">
      <ActionBanner
        title={`حالة الشركة التشغيلية — ${dash.period.label}`}
        description={`اكتمال التقارير ${comp?.completionRate ?? 0}٪ · نقطة التجميع الإدارية قبل الرئيس التنفيذي.`}
        tone="navy"
        cta={<Link to="/app/reports"><Button variant="inverted">إرسال ملخص للرئيس التنفيذي</Button></Link>}
      />

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      <NeedsActionPanel
        items={actions}
        emptyText="كل الأقسام مكتملة ولا مخاطر أو اعتمادات معلّقة — الشركة منضبطة."
        emptyHint="ستظهر هنا الأقسام المتأخرة، المخاطر المفتوحة، الاعتمادات المعلّقة، والموظفون دون المستهدف."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="اكتمال تقارير الشركة" value={`${comp?.completionRate ?? 0}٪`} tone={(comp?.completionRate ?? 0) >= 90 ? 'success' : 'gold'} icon="reports" />
        <MetricTile label="أقسام متأخرة" value={lateDepts} tone={lateDepts > 0 ? 'alert' : 'success'} icon="departments" />
        <MetricTile label="متوسط KPI" value={kpi?.averageScore ?? '—'} tone={(kpi?.averageScore ?? 0) >= 85 ? 'success' : (kpi?.averageScore ?? 0) >= 70 ? 'gold' : 'alert'} icon="kpi" />
        <MetricTile label="اعتمادات معلّقة" value={approvals?.length ?? 0} tone={(approvals?.length ?? 0) > 0 ? 'gold' : 'success'} icon="workflow" />
      </div>

      <GovernanceTiles g={gov} />

      <Card>
        <SectionTitle title="حالة الأقسام" hint="اكتمال تقارير كل قسم هذا الأسبوع" action={<Link to="/app/reports"><Button variant="ghost">تفاصيل</Button></Link>} />
        {!comp || comp.byDepartment.length === 0 ? <MiniEmpty text="لا توجد بيانات أقسام." hint="تظهر هنا نِسَب اكتمال التقارير لكل إدارة بمجرّد تسليم فِرقها لتقاريرها." /> : (
          <div className="space-y-3">
            {comp.byDepartment.map((d) => (
              <div key={d.departmentId ?? d.departmentName} className="flex items-center gap-3">
                <span className="w-40 shrink-0 truncate text-sm text-navy">{d.departmentName}</span>
                <div className="flex-1"><ProgressBar value={d.completionRate} tone={d.completionRate >= 90 ? 'success' : 'orange'} /></div>
                <span className="w-24 shrink-0 text-left text-xs text-ink-2">{d.closed}/{d.total} · {d.completionRate}٪</span>
              </div>
            ))}
          </div>
        )}
      </Card>

      <B2cRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <MediaBuyerRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <SeoRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <SocialOpsSummaryPanel periodKey={dash.period.periodKey} variant="gm" />
      <ContentWriterRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <DesignerRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <VideoRollupPanel periodKey={dash.period.periodKey} variant="gm" />
      <ModerationRollupPanel periodKey={dash.period.periodKey} variant="gm" />

      <PendingApprovalsCard items={approvals} />
    </div>
  );
}

// ============================================================
// 5) الرئيس التنفيذي
// ============================================================
export function CeoDashboard({ dash, kpiDelta }: { dash: DashboardDto; kpiDelta: { value: number; up: boolean } | null }) {
  const { data: comp } = useCompleteness(dash.period.periodKey, true);
  const { data: kpi } = useKpiSummary(dash.period.periodKey, true);
  const { data: gov } = useGovSummary(true);
  const { data: risks } = useRisks(true);
  const { data: decisions } = useDecisions(true);
  const highRisks = (risks ?? []).filter((r) => (r.severity === 'High' || r.severity === 'Critical') && r.status !== 'Closed');
  const openDecisions = (decisions ?? []).filter((d) => d.status === 'Proposed');

  const actions: NeedsActionEntry[] = [];
  openDecisions.forEach((d) =>
    actions.push({ id: `dec-${d.id}`, title: `قرار يحتاج قرارك: ${d.title}`, context: 'قرار مقترح بانتظار البتّ', urgency: 'high', to: '/app/governance', cta: 'افتح' }),
  );
  highRisks.forEach((r) =>
    actions.push({ id: `risk-${r.id}`, title: `خطر ${riskSeverityLabel[r.severity]}: ${r.title}`, context: 'خطر عالي الخطورة مفتوح', urgency: 'high', to: '/app/governance', cta: 'راجع' }),
  );
  if ((comp?.completionRate ?? 100) < 90)
    actions.push({ id: 'comp-low', title: `اكتمال الأسبوع ${comp?.completionRate ?? 0}٪`, context: 'تقارير ناقصة على مستوى الشركة', urgency: 'medium', to: '/app/reports', cta: 'تفاصيل' });
  if ((kpi?.belowTarget ?? 0) > 0)
    actions.push({ id: 'kpi-below', title: `${kpi?.belowTarget} موظف دون المستهدف`, context: 'مؤشرات أداء أقل من الحد المطلوب', urgency: 'medium', to: '/app/kpi', cta: 'افتح KPI' });

  return (
    <div className="space-y-6">
      <ActionBanner
        title={`الصورة العامة للشركة — ${dash.period.label}`}
        description="القرار والاتجاه والخطر فقط. التفاصيل تظهر عند الضغط (Drill-down)."
        tone="navy"
      />

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      <NeedsActionPanel
        items={actions}
        emptyText="لا قرارات معلّقة ولا مخاطر عالية ولا نقص في الاكتمال — الشركة منضبطة."
        emptyHint="عند ظهور قرار مقترح أو خطر عالي أو انخفاض في الاكتمال، ستجد هنا أهم ما يستدعي قرارك."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="متوسط KPI للشركة" value={kpi?.averageScore ?? '—'} tone={(kpi?.averageScore ?? 0) >= 85 ? 'success' : (kpi?.averageScore ?? 0) >= 70 ? 'gold' : 'alert'} delta={kpiDelta} icon="kpi" />
        <MetricTile label="اكتمال الأسبوع" value={`${comp?.completionRate ?? 0}٪`} tone={(comp?.completionRate ?? 0) >= 90 ? 'success' : 'gold'} icon="reports" />
        <MetricTile label="مخاطر عالية/حرجة" value={highRisks.length} tone={highRisks.length > 0 ? 'alert' : 'success'} to="/app/governance" icon="governance" />
        <MetricTile label="قرارات تحتاج قراري" value={openDecisions.length} tone={openDecisions.length > 0 ? 'gold' : 'success'} to="/app/governance" icon="governance" />
      </div>

      <B2cRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <MediaBuyerRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <SeoRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <SocialOpsSummaryPanel periodKey={dash.period.periodKey} variant="ceo" />
      <ContentWriterRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <DesignerRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <VideoRollupPanel periodKey={dash.period.periodKey} variant="ceo" />
      <ModerationRollupPanel periodKey={dash.period.periodKey} variant="ceo" />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle title="المخاطر عالية الخطورة" action={<Link to="/app/governance"><Button variant="ghost">الكل</Button></Link>} />
          {highRisks.length === 0 ? <MiniEmpty text="لا مخاطر عالية مفتوحة." hint="تظهر هنا المخاطر عالية الخطورة عند تسجيلها من صفحة «الحوكمة». الوضع آمن حاليًا." /> : (
            <ul>{highRisks.slice(0, 5).map((r) => <AlertRow key={r.id} tone="alert">{r.title} — {riskSeverityLabel[r.severity]}</AlertRow>)}</ul>
          )}
        </Card>
        <Card>
          <SectionTitle title="قرارات تحتاج قراري" action={<Link to="/app/governance"><Button variant="ghost">الكل</Button></Link>} />
          {openDecisions.length === 0 ? <MiniEmpty text="لا قرارات معلّقة." hint="تظهر هنا القرارات المقترحة بانتظار البتّ. أنشئها من صفحة «الحوكمة» عند الحاجة." /> : (
            <ul>{openDecisions.slice(0, 5).map((d) => <AlertRow key={d.id} tone="gold">{d.title}</AlertRow>)}</ul>
          )}
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle title="اتجاه أداء الشركة" />
          <LineTrend points={(((dash.widgets.find((w) => w.key === 'kpiTrend')?.data as { periodKey: string; value: number }[]) ?? []).map((d) => ({ label: d.periodKey, value: d.value })))} />
        </Card>
        <Card>
          <SectionTitle title="ملخّص الحوكمة" />
          {gov ? (
            <Donut slices={[
              { label: 'مخاطر', value: gov.openRisks },
              { label: 'تصعيدات', value: gov.openEscalations },
              { label: 'قرارات', value: gov.openDecisions },
              { label: 'تدريب', value: gov.openTrainingNeeds },
            ].filter((s) => s.value > 0)} />
          ) : <MiniEmpty text="لا بيانات." />}
        </Card>
      </div>
    </div>
  );
}

// ============================================================
// 6) دعم الرئيس التنفيذي (فاطمة)
// ============================================================
export function CeoSupportDashboard({ dash }: { dash: DashboardDto }) {
  const { data: comp } = useCompleteness(dash.period.periodKey, true);
  const { data: pending } = usePendingReports(true);
  const { data: activity } = useActivity(true);
  const { data: kpi } = useKpiSummary(dash.period.periodKey, true);
  const rate = comp?.completionRate ?? 0;
  const awaiting = (activity ?? []).filter((a) => a.status === 'Submitted' || a.status === 'ApprovedByDirectManager' || a.status === 'ApprovedByNextLevel');

  const weekState = rate >= 95 ? { t: 'success' as const, l: 'صورة الأسبوع مكتملة' } : rate >= 70 ? { t: 'orange' as const, l: 'صورة الأسبوع ناقصة' } : { t: 'navy' as const, l: 'صورة الأسبوع غير جاهزة' };

  const actions: NeedsActionEntry[] = [];
  (pending ?? []).slice(0, 12).forEach((p) =>
    actions.push({ id: `pend-${p.submissionId}`, title: `متابعة: ${p.templateTitle}`, context: `${p.submitterName} · ${p.periodKey} · ${submissionStatusLabel[p.status]}`, urgency: 'medium', to: `/app/submissions?open=${p.submissionId}`, cta: 'تابعي' }),
  );
  if (rate < 95)
    actions.push({ id: 'week-incomplete', title: `صورة الأسبوع غير مكتملة (${rate}٪)`, context: `${comp?.pending ?? 0} تقرير قيد الانتظار من ${comp?.total ?? 0}`, urgency: 'low', to: '/app/reports', cta: 'تجهيز الملخّص' });

  return (
    <div className="space-y-6">
      <ActionBanner
        title={weekState.l}
        description={`اكتمال التقارير ${rate}٪ — تتبّعي الاكتمال دون اعتماد فنّي (إلا بصلاحية خاصة من الرئيس التنفيذي).`}
        tone={weekState.t}
        cta={<Link to="/app/reports"><Button variant="inverted">تجهيز ملخّص الرئيس التنفيذي</Button></Link>}
      />

      {/* الفلتر الزمني + التزام التقارير + اختناقات سير الاعتماد — RPT-ROLE-HOME-REPORT-CARDS-R1 */}
      <ReportInsightsSection />

      <NeedsActionPanel
        items={actions}
        emptyText="كل التقارير مُرسَلة وصورة الأسبوع مكتملة — لا متابعة مطلوبة الآن."
        emptyHint="عند تأخّر أي تقرير أو نقص اكتمال الأسبوع، ستظهر هنا قائمة المتابعة مرتّبة."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="اكتمال الأسبوع" value={`${rate}٪`} tone={rate >= 95 ? 'success' : 'gold'} icon="reports" />
        <MetricTile label="تقارير مكتملة" value={comp?.closed ?? 0} tone="success" icon="reports" />
        <MetricTile label="متأخرة / لم تُرسل" value={pending?.length ?? 0} tone={(pending?.length ?? 0) > 0 ? 'alert' : 'success'} icon="workflow" />
        <MetricTile label="اعتمادات معلّقة" value={awaiting.length} tone={awaiting.length > 0 ? 'gold' : 'success'} icon="workflow" />
      </div>

      <Card>
        <SectionTitle title="نسبة اكتمال الأسبوع" />
        <ProgressBar value={rate} tone={rate >= 95 ? 'success' : 'orange'} />
        <p className="mt-2 text-xs text-ink-2">{comp?.closed ?? 0} مكتمل من {comp?.total ?? 0} · {comp?.pending ?? 0} قيد الانتظار · متوسط KPI {kpi?.averageScore ?? '—'}</p>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <PendingReportsCard items={pending} title="من لم يُرسل / متأخر" />
        <Card>
          <SectionTitle title="اعتمادات معلّقة" hint="تقارير تنتظر قرار المعتمِدين" />
          {awaiting.length === 0 ? <MiniEmpty text="لا اعتمادات معلّقة." hint="تظهر هنا التقارير التي تنتظر اعتمادك في سلسلة الموافقات. لا شيء معلّق الآن." /> : (
            <ul>{awaiting.slice(0, 8).map((a) => <ActionItem key={a.submissionId} title={a.templateTitle} context={`${a.submitterName} · ${a.periodKey}`} badge={<Badge tone="navy">{submissionStatusLabel[a.status]}</Badge>} />)}</ul>
          )}
        </Card>
      </div>
    </div>
  );
}

// ============================================================
// 7) مدير النظام / الحوكمة
// ============================================================
export function AdminDashboard({ dash }: { dash: DashboardDto }) {
  const { data: comp } = useCompleteness(dash.period.periodKey, true);
  const { data: pending } = usePendingReports(true);
  const { data: gov } = useGovSummary(true);
  const { data: users } = useQuery({ queryKey: ['dash-users'], queryFn: async () => (await api.get<{ id: string }[]>('/directory/users')).data });
  const { data: templates } = useQuery({ queryKey: ['dash-templates'], queryFn: async () => (await api.get<{ id: string }[]>('/report-templates')).data });

  const tiles = [
    { to: '/app/submissions', title: 'قوالب وتقارير', desc: 'إدارة القوالب والتقارير الدورية.' },
    { to: '/app/kpi', title: 'قوالب مؤشرات الأداء', desc: 'تعريف ومتابعة مؤشرات KPI.' },
    { to: '/app/governance', title: 'الحوكمة', desc: 'المخاطر والتصعيدات والقرارات.' },
    { to: '/app/reports', title: 'تقارير الحوكمة', desc: 'الاكتمال والأداء وجودة البيانات.' },
    { to: '/app/audit', title: 'سجل التدقيق', desc: 'تتبّع الإجراءات الحساسة.' },
    { to: '/app/development', title: 'التطوير', desc: 'احتياجات التدريب وخطط التحسين.' },
  ];

  const actions: NeedsActionEntry[] = [];
  if ((gov?.openRisks ?? 0) > 0)
    actions.push({ id: 'adm-risks', title: `${gov?.openRisks} خطر مفتوح`, context: 'مخاطر بحاجة إلى مراجعة الحوكمة', urgency: 'high', to: '/app/governance', cta: 'راجع' });
  if ((gov?.openEscalations ?? 0) > 0)
    actions.push({ id: 'adm-esc', title: `${gov?.openEscalations} تصعيد مفتوح`, context: 'تصعيدات بانتظار المعالجة', urgency: 'medium', to: '/app/governance', cta: 'افتح' });
  if ((pending?.length ?? 0) > 0)
    actions.push({ id: 'adm-pending', title: `${pending?.length} تقرير ناقص/متأخر`, context: 'تقارير لم تُرسل أو معادة على مستوى النظام', urgency: 'medium', to: '/app/reports', cta: 'تفاصيل' });

  return (
    <div className="space-y-6">
      <ActionBanner title="لوحة الحوكمة وإدارة النظام" description="مراقبة صحة النظام واكتمال البيانات وإدارة المستخدمين والقوالب." tone="navy" />

      <NeedsActionPanel
        items={actions}
        emptyText="لا مخاطر ولا تصعيدات ولا تقارير ناقصة — النظام منضبط."
        emptyHint="عند ظهور خطر مفتوح أو تصعيد أو نقص في التقارير، ستجد هنا أهم ما يستدعي إجراءً إداريًا."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label="المستخدمون" value={users?.length ?? '—'} tone="navy" />
        <MetricTile label="قوالب التقارير" value={templates?.length ?? '—'} tone="navy" />
        <MetricTile label="اكتمال الأسبوع" value={`${comp?.completionRate ?? 0}٪`} tone={(comp?.completionRate ?? 0) >= 90 ? 'success' : 'gold'} />
        <MetricTile label="تقارير ناقصة/متأخرة" value={pending?.length ?? 0} tone={(pending?.length ?? 0) > 0 ? 'alert' : 'success'} />
      </div>

      <GovernanceTiles g={gov} />

      <Card>
        <SectionTitle title="إدارة النظام" hint="اختر القسم الذي تريد إدارته" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {tiles.map((t) => (
            <Link key={t.to} to={t.to}>
              <Card className="h-full transition hover:border-navy hover:shadow-sm">
                <h3 className="font-semibold text-navy">{t.title}</h3>
                <p className="mt-1 text-sm text-ink-2">{t.desc}</p>
              </Card>
            </Link>
          ))}
        </div>
      </Card>

      <PendingReportsCard items={pending} />
    </div>
  );
}

export function DashboardFallback() {
  return <LoadingState label="يتم تحميل لوحتك…" />;
}

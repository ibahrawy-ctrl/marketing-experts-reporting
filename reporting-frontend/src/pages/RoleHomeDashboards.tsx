// لوحات رئيسية مخصّصة للأدوار التي كانت تسقط على لوحة الموظّف الافتراضية:
// HR / FinanceManager+Accountant / AccountPortfolioReader.
// UI-ROLE-BASED-HOME-R1 — Frontend-only: تعتمد حصرًا على APIs/routes منشورة،
// والنطاق مفروض خادمًا (الواجهة ليست مصدر أمان). كل استعلام قد يُرجِع 403
// مُفعَّل فقط عبر تركيب المكوّن للدور المناسب (enabled-by-mount) مع حارس hasAnyRole إضافي.
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useAuth } from '../lib/auth';
import { Card, Badge, Button, Input } from '../components/ui';
import {
  PRESET_OPTIONS,
  periodLabel,
  weekKeysForPeriod,
  type DashboardPeriod,
} from '../lib/dashboardPeriod';
import { LoadingState } from '../components/states';
import {
  SectionTitle,
  MetricTile,
  ActionItem,
  MiniEmpty,
  ProgressRing,
  StatPill,
  StageChip,
  toneForPercent,
} from '../components/dashboard';
import { usePayrollImpacts } from '../lib/usePayrollImpacts';
import { useMyPortfolioProjects, useMyPortfolioClients } from '../lib/useAccountPortfolio';
import {
  employeeServiceRequestStatusLabel,
  employeeServiceRequestTypeLabel,
  payrollImpactTypeLabel,
  projectStatusLabel,
  serviceTypeLabel,
} from '../lib/format';
import type {
  ComplianceSummaryReport,
  EmployeeServiceRequestListItemDto,
  Role,
  WorkflowBottlenecksSummaryReport,
} from '../types/api';

// ===== إجراءات سريعة موحّدة =====
function QuickActions({ items }: { items: { to: string; label: string; hint?: string }[] }) {
  if (items.length === 0) return null;
  return (
    <Card>
      <SectionTitle title="إجراءات سريعة" hint="انتقل مباشرةً إلى أكثر شاشاتك استخدامًا" />
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {items.map((it) => (
          <Link
            key={it.to}
            to={it.to}
            className="rounded-xl border border-line bg-offwhite p-4 transition hover:border-orange hover:shadow-sm"
          >
            <p className="font-semibold text-navy">{it.label}</p>
            {it.hint && <p className="mt-1 text-xs text-ink-2">{it.hint}</p>}
          </Link>
        ))}
      </div>
    </Card>
  );
}

// أدوار رؤية الالتزام (تطابق Roles.CompletionMonitors / Policy=ReportCompletionView خادمًا).
// الواجهة ليست مصدر أمان — الخادم يفرض الصلاحية والنطاق؛ هذا الحارس يمنع نداء 403 لغير المخوّلين.
const COMPLETION_MONITOR_ROLES: Role[] = [
  'Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'HR', 'Viewer',
];

// بطاقة «الالتزام: المستحق والمتأخر» — مُفعَّلة (RPT-DUE-LATE-COMPLIANCE-R1) لمن يملك رؤية الالتزام.
// تستهلك GET /reports/compliance-summary للأسبوع الحالي (أرقام التزام فقط) وتربط إلى /app/compliance.
function ComplianceHomeCard() {
  const { hasAnyRole } = useAuth();
  const canView = hasAnyRole(...COMPLETION_MONITOR_ROLES);

  const summary = useQuery({
    queryKey: ['home-compliance-summary'],
    queryFn: async () =>
      (await api.get<ComplianceSummaryReport>('/reports/compliance-summary')).data,
    enabled: canView,
  });

  if (!canView) {
    return (
      <Card className="border-dashed">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-ink-2">الالتزام: المستحق والمتأخر</h2>
          <Badge tone="muted">غير متاح لدورك</Badge>
        </div>
        <p className="mt-2 text-sm text-ink-3">
          نظرة موحّدة على التقارير المستحقّة والمتأخّرة — متاحة لأدوار متابعة الالتزام.
        </p>
      </Card>
    );
  }

  const s = summary.data;
  return (
    <Card>
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-navy">الالتزام: المستحق والمتأخر</h2>
        {s && <Badge tone="navy">{s.periodLabel}</Badge>}
      </div>
      {summary.isLoading ? (
        <LoadingState label="يتم تحميل ملخّص الالتزام…" />
      ) : !s ? (
        <p className="mt-2 text-sm text-ink-3">تعذّر جلب ملخّص الالتزام.</p>
      ) : (
        <>
          <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <MetricTile label="نسبة الالتزام" value={`${s.compliancePercent}%`} />
            <MetricTile label="في الموعد" value={s.onTime} />
            <MetricTile label="متأخر" value={s.late} />
            <MetricTile label="لم يسلّم وانقضى" value={s.missingOverdue} />
          </div>
          <div className="mt-4">
            <Link to="/app/compliance">
              <Button variant="ghost">عرض تفاصيل الالتزام</Button>
            </Link>
          </div>
        </>
      )}
    </Card>
  );
}

// بطاقة «التزام التقارير» الموسّعة — RPT-COMPLIANCE-ADMIN-HOME-CARD-R1 (Frontend-only).
// تستهلك نفس GET /reports/compliance-summary (RPT-DUE-LATE-COMPLIANCE-R1) وتعرض ست قيم
// (نسبة الالتزام/في الموعد/المتوقّعة/المُسلّمة/المتأخرة/لم تُسلّم وتجاوزت) + رابط إلى /app/compliance.
// النطاق والصلاحية مفروضان خادمًا؛ حارس الدور هنا يمنع نداء 403 لغير المخوّلين ويعرض «غير متاح لدورك».
// تجميع حقيقي لعدّة أسابيع تشغيلية (مجموع المتوقّع/المُسلّم/… ثم إعادة احتساب النِّسب).
// لأسبوع واحد يُعاد صفّ الخادم كما هو (دون إعادة احتساب) للحفاظ على المطابقة التامّة.
function aggregateCompliance(rows: ComplianceSummaryReport[]): ComplianceSummaryReport | null {
  if (rows.length === 0) return null;
  if (rows.length === 1) return rows[0];
  const sum = rows.reduce(
    (a, r) => ({
      expected: a.expected + r.expected,
      submitted: a.submitted + r.submitted,
      missing: a.missing + r.missing,
      late: a.late + r.late,
      lateSubmitted: a.lateSubmitted + r.lateSubmitted,
      missingOverdue: a.missingOverdue + r.missingOverdue,
      onTime: a.onTime + r.onTime,
    }),
    { expected: 0, submitted: 0, missing: 0, late: 0, lateSubmitted: 0, missingOverdue: 0, onTime: 0 },
  );
  const pct = (n: number) => (sum.expected > 0 ? Math.round((n / sum.expected) * 100) : 0);
  return {
    periodKey: `${rows[0].periodKey}…${rows[rows.length - 1].periodKey}`,
    periodLabel: '',
    ...sum,
    compliancePercent: pct(sum.submitted),
    onTimePercent: pct(sum.onTime),
  };
}

export function ReportComplianceHomeCard({ period }: { period?: DashboardPeriod }) {
  const { hasAnyRole } = useAuth();
  const canView = hasAnyRole(...COMPLETION_MONITOR_ROLES);
  const p = period ?? { preset: 'current_week' as const };
  const weekKeys = useMemo(() => weekKeysForPeriod(p), [p.preset, p.from, p.to]);
  const label = periodLabel(p);

  const summary = useQuery({
    queryKey: ['home-report-compliance-summary', weekKeys],
    queryFn: async () => {
      const rows = await Promise.all(
        weekKeys.map((wk) =>
          api
            .get<ComplianceSummaryReport>('/reports/compliance-summary', { params: { weekKey: wk } })
            .then((r) => r.data),
        ),
      );
      return aggregateCompliance(rows);
    },
    enabled: canView,
  });

  if (!canView) {
    return (
      <Card className="border-dashed">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-ink-2">التزام التقارير</h2>
          <Badge tone="muted">غير متاح لدورك</Badge>
        </div>
        <p className="mt-2 text-sm text-ink-3">
          نظرة موحّدة على نسبة الالتزام (Compliance) والتقارير المتأخرة وغير المُسلّمة — متاحة لأدوار متابعة الالتزام.
        </p>
      </Card>
    );
  }

  const s = summary.data;
  return (
    <Card className="flex flex-col">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-navy">التزام التقارير</h2>
        <Badge tone="navy">{label}</Badge>
      </div>
      {weekKeys.length > 1 && (
        <p className="mt-2 rounded-lg bg-offwhite px-3 py-2 text-xs text-ink-3">
          تجميع تشغيليّ لـ{weekKeys.length} أسابيع تشغيلية ضمن الفترة (الخميس→الأربعاء) — وليس تجميعًا يوميًّا دقيقًا من/إلى.
        </p>
      )}
      {summary.isLoading ? (
        <LoadingState label="يتم تحميل ملخّص الالتزام…" />
      ) : !s ? (
        <p className="mt-2 text-sm text-ink-3">تعذّر جلب ملخّص الالتزام. حدّث الصفحة لإعادة المحاولة.</p>
      ) : (
        <>
          <div className="mt-4 flex flex-1 flex-col gap-4 sm:flex-row sm:items-center">
            <div className="flex flex-col items-center gap-1 sm:w-40 sm:shrink-0">
              <ProgressRing value={s.compliancePercent} caption="الالتزام" />
              <p className="text-xs text-ink-3">
                في الموعد <span className={`font-bold ${toneForPercent(s.onTimePercent) === 'alert' ? 'text-alert' : 'text-navy'}`}>{s.onTimePercent}%</span>
              </p>
            </div>
            <div className="grid flex-1 grid-cols-2 gap-2">
              <StatPill label="المتوقّعة" value={s.expected} tone="navy" />
              <StatPill label="المُسلّمة" value={s.submitted} tone="success" />
              <StatPill label="المتأخرة" value={s.late} tone={s.late > 0 ? 'alert' : 'success'} />
              <StatPill label="لم تُسلّم وتجاوزت" value={s.missingOverdue} tone={s.missingOverdue > 0 ? 'alert' : 'success'} />
            </div>
          </div>
          <div className="mt-4">
            <Link to="/app/compliance">
              <Button variant="ghost">عرض التفاصيل</Button>
            </Link>
          </div>
        </>
      )}
    </Card>
  );
}

// صياغة العمر بالساعات إلى نص مقروء (ساعات/أيام).
function bottleneckAgeText(hours: number): string {
  if (hours < 24) return `${Math.round(hours)} ساعة`;
  const days = Math.floor(hours / 24);
  return `${days} يوم`;
}

// بطاقة «اختناقات سير الاعتماد» — RPT-WORKFLOW-BOTTLENECKS-R1. مُتاحة لأيّ مستخدم مصادَق؛
// النطاق مفروض خادمًا عبر ScopeResolver (الموظف يرى تقاريره العالقة فقط، القائد فريقه… إلخ).
// تستهلك GET /reports/workflow-bottlenecks/summary وتربط إلى /app/workflows.
export function WorkflowBottlenecksHomeCard({ period }: { period?: DashboardPeriod }) {
  const summary = useQuery({
    queryKey: ['home-workflow-bottlenecks-summary'],
    queryFn: async () =>
      (await api.get<WorkflowBottlenecksSummaryReport>('/reports/workflow-bottlenecks/summary')).data,
  });

  // الـendpoint يعرض «الحالة الحالية» فقط ولا يدعم فترة زمنية؛ ننبّه عند اختيار فترة غير الأسبوع الحالي.
  const showCurrentOnlyNote = (period?.preset ?? 'current_week') !== 'current_week';

  const s = summary.data;
  return (
    <Card className="flex flex-col">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-navy">اختناقات سير الاعتماد</h2>
        {s && (
          <Badge tone={s.overduePending > 0 ? 'alert' : 'navy'}>
            {s.totalPending} عالق
          </Badge>
        )}
      </div>
      {showCurrentOnlyNote && (
        <p className="mt-2 rounded-lg bg-offwhite px-3 py-2 text-xs text-ink-3">
          هذا المؤشر يعرض الحالة الحالية فقط (لا يدعم الفترة الزمنية بعد).
        </p>
      )}
      {summary.isLoading ? (
        <LoadingState label="يتم تحليل الاختناقات…" />
      ) : !s ? (
        <p className="mt-2 text-sm text-ink-3">تعذّر جلب ملخّص الاختناقات.</p>
      ) : s.totalPending === 0 ? (
        <p className="mt-3 text-sm text-ink-2">لا توجد تقارير عالقة في مسارات الاعتماد ضمن نطاقك.</p>
      ) : (
        <>
          <div className="mt-4 flex flex-1 flex-col gap-4 sm:flex-row sm:items-center">
            <div className="flex flex-col items-center justify-center rounded-2xl border border-line bg-offwhite px-4 py-3 sm:w-36 sm:shrink-0">
              <span className="text-4xl font-extrabold text-navy">{s.totalPending}</span>
              <span className="text-xs font-medium text-ink-2">تقارير عالقة</span>
              <span className={`mt-1 text-xs font-bold ${s.overduePending > 0 ? 'text-alert' : 'text-success'}`}>
                {s.overduePending} متأخرة عن SLA
              </span>
            </div>
            <div className="flex flex-1 flex-col gap-3">
              <div className="grid grid-cols-2 gap-2">
                <StatPill label="الأقدم انتظارًا" value={bottleneckAgeText(s.oldestPendingAgeHours)} tone="gold" />
                <StatPill
                  label="الأكثر اختناقًا"
                  value={s.stageWithMostPendingLabel ?? '—'}
                  tone="orange"
                />
              </div>
              <div>
                <p className="mb-1.5 text-xs text-ink-3">المراحل العالقة</p>
                <div className="flex flex-wrap gap-2">
                  <StageChip label="قائد الفريق" tone="muted" active={s.stageWithMostPending === 'team_leader'} />
                  <StageChip label="المدير" tone="muted" active={s.stageWithMostPending === 'manager'} />
                  <StageChip label="الإدارة العليا" tone="muted" active={s.stageWithMostPending === 'senior_management'} />
                </div>
              </div>
            </div>
          </div>
          <div className="mt-4">
            <Link to="/app/workflows">
              <Button variant="ghost">عرض التفاصيل</Button>
            </Link>
          </div>
        </>
      )}
    </Card>
  );
}

// ===== الفلتر الزمني للصفحة الرئيسية (RPT-ROLE-HOME-REPORT-CARDS-R1) =====
// اختيارات سريعة + مدى مخصّص (من/إلى). يطبَّق تلقائيًّا عند الاختيار. يُمرَّر إلى كروت التقارير.
export function DashboardDateRangeFilter({
  value,
  onChange,
}: {
  value: DashboardPeriod;
  onChange: (p: DashboardPeriod) => void;
}) {
  return (
    <div className="rounded-2xl border border-line bg-white px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-1.5">
          {PRESET_OPTIONS.map((opt) => {
            const active = value.preset === opt.value;
            return (
              <button
                key={opt.value}
                type="button"
                onClick={() => onChange({ preset: opt.value })}
                className={`rounded-full border px-3 py-1 text-xs font-medium transition ${
                  active
                    ? 'border-navy bg-navy text-white'
                    : 'border-line bg-offwhite text-ink-2 hover:border-navy hover:text-navy'
                }`}
              >
                {opt.label}
              </button>
            );
          })}
        </div>
        <div className="flex items-center gap-2">
          <span className="hidden text-xs text-ink-3 sm:inline">الفترة المختارة:</span>
          <Badge tone="navy">{periodLabel(value)}</Badge>
        </div>
      </div>
      {value.preset === 'custom' && (
        <div className="mt-3 flex flex-wrap items-end gap-3 border-t border-line pt-3">
          <label className="flex flex-col gap-1 text-xs text-ink-3">
            من تاريخ
            <Input
              type="date"
              value={value.from ?? ''}
              onChange={(e) => onChange({ preset: 'custom', from: e.target.value, to: value.to })}
              className="w-40"
            />
          </label>
          <label className="flex flex-col gap-1 text-xs text-ink-3">
            إلى تاريخ
            <Input
              type="date"
              value={value.to ?? ''}
              onChange={(e) => onChange({ preset: 'custom', from: value.from, to: e.target.value })}
              className="w-40"
            />
          </label>
        </div>
      )}
    </div>
  );
}

// قسم موحّد: الفلتر الزمني فوق كارتَي «التزام التقارير» و«اختناقات سير الاعتماد».
// يُسقَط في لوحات الأدوار المخوّلة (Admin/CEO/GM/Manager/TeamLeader/CeoSupport).
export function ReportInsightsSection() {
  const [period, setPeriod] = useState<DashboardPeriod>({ preset: 'current_week' });
  return (
    <section className="space-y-4">
      <div className="flex items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-navy">مؤشّرات التقارير</h2>
          <p className="text-xs text-ink-2">الالتزام واختناقات سير الاعتماد ضمن الفترة المحدّدة</p>
        </div>
      </div>
      <DashboardDateRangeFilter value={period} onChange={setPeriod} />
      <div className="grid items-stretch gap-4 lg:grid-cols-2">
        <ReportComplianceHomeCard period={period} />
        <WorkflowBottlenecksHomeCard period={period} />
      </div>
    </section>
  );
}

// ===== لوحة الالتزام + الاختناقات (موحّدة للوحات الأدوار) =====
function FuturePlaceholders() {
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <ComplianceHomeCard />
      <WorkflowBottlenecksHomeCard />
    </div>
  );
}

// ===== لوحة الموارد البشرية =====
const HR_REQUEST_OPEN = new Set(['Submitted', 'InReview']);

export function HrHomeDashboard() {
  const { hasAnyRole } = useAuth();
  // قائمة طلبات HR للمعالجة — سياسة HrRequestManagement (HR ضمنها). مُفعَّلة للدور المناسب فقط.
  const canManageHr = hasAnyRole('HR', 'Admin', 'CEO', 'GeneralManager', 'CeoSupport');
  const { data: requests, isLoading } = useQuery({
    queryKey: ['hr-home', 'requests'],
    enabled: canManageHr,
    queryFn: async () =>
      (await api.get<EmployeeServiceRequestListItemDto[]>('/employee-service-requests')).data,
  });

  const open = (requests ?? []).filter((r) => HR_REQUEST_OPEN.has(r.status));
  const submitted = (requests ?? []).filter((r) => r.status === 'Submitted').length;
  const inReview = (requests ?? []).filter((r) => r.status === 'InReview').length;

  const quick = [
    { to: '/app/hr-employees', label: 'إدارة بيانات الموظفين', hint: 'الدليل والتعديل' },
    { to: '/app/hr-requests', label: 'طلبات الموارد البشرية', hint: 'المعالجة والمتابعة' },
    { to: '/app/leave-requests', label: 'الإجازات والاستئذانات', hint: 'الطلبات الجارية' },
    ...(hasAnyRole('HR', 'Admin', 'CEO', 'GeneralManager', 'CeoSupport')
      ? [{ to: '/app/balance-management', label: 'إدارة الأرصدة', hint: 'أرصدة الموظفين' }]
      : []),
    ...(hasAnyRole('HR', 'Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer')
      ? [{ to: '/app/compliance', label: 'متابعة الالتزام', hint: 'التزام التسليم' }]
      : []),
    { to: '/app/job-roles', label: 'المسمّيات الوظيفية', hint: 'إسناد المسمّى' },
  ];

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile
          label="طلبات HR مفتوحة"
          value={isLoading ? '…' : open.length}
          tone={open.length > 0 ? 'gold' : 'success'}
          to="/app/hr-requests"
          hint="بانتظار المعالجة"
          icon="workflow"
        />
        <MetricTile label="جديدة (لم تُراجَع)" value={isLoading ? '…' : submitted} tone="navy" to="/app/hr-requests" icon="reports" />
        <MetricTile label="قيد المراجعة" value={isLoading ? '…' : inReview} tone="navy" to="/app/hr-requests" icon="reports" />
        <MetricTile label="إدارة الموظفين" value="فتح" tone="orange" to="/app/hr-employees" hint="الدليل والبيانات" icon="users" />
      </div>

      <QuickActions items={quick} />

      <Card>
        <SectionTitle
          title="أحدث طلبات الموارد البشرية"
          hint="بانتظار المعالجة — لا يشمل أي اعتماد فنّي للتقارير"
          action={
            <Link to="/app/hr-requests">
              <Button variant="ghost">عرض الكل</Button>
            </Link>
          }
        />
        {isLoading ? (
          <LoadingState label="يتم تحميل الطلبات…" />
        ) : open.length === 0 ? (
          <MiniEmpty text="لا توجد طلبات مفتوحة" hint="ستظهر هنا طلبات الموظفين الجديدة وقيد المراجعة." />
        ) : (
          <ul>
            {open.slice(0, 8).map((r) => (
              <ActionItem
                key={r.id}
                title={r.title}
                context={`${r.requesterName} · ${employeeServiceRequestTypeLabel[r.requestType]}`}
                badge={<Badge tone={r.status === 'Submitted' ? 'gold' : 'navy'}>{employeeServiceRequestStatusLabel[r.status]}</Badge>}
                action={
                  <Link to="/app/hr-requests">
                    <Button>معالجة</Button>
                  </Link>
                }
              />
            ))}
          </ul>
        )}
      </Card>

      <FuturePlaceholders />
    </div>
  );
}

// ===== لوحة المالية (FinanceManager / Accountant) =====
export function FinanceHomeDashboard() {
  // قائمة التأثير على الراتب — سياسة PayrollImpactRead (أدوار المالية ضمنها).
  // مُفعَّلة بالتركيب للدور المناسب فقط (اللوحة لا تُركَّب إلا لدور مالي في HomePage)؛
  // القراءة فقط — لا صرف ولا اعتماد مالي.
  const { data, isLoading } = usePayrollImpacts({});
  const summary = data?.summary;
  const items = data?.items ?? [];

  const quick = [
    { to: '/app/payroll/leave-impacts', label: 'طلبات مؤثّرة على الراتب', hint: 'مراجعة التأثير' },
    { to: '/app/kpi-finance-export', label: 'تصدير KPI للمالية', hint: 'الربع المختار — CSV' },
  ];

  return (
    <div className="space-y-6">
      <Card className="border-r-4 border-r-navy">
        <p className="text-sm text-ink-2">
          هذه اللوحة للمراجعة والقراءة فقط — لا تقوم بحساب أو صرف أو اعتماد أي مستحقات مالية.
        </p>
      </Card>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile
          label="طلبات مؤثّرة على الراتب"
          value={isLoading ? '…' : summary?.totalImpacted ?? 0}
          tone="navy"
          to="/app/payroll/leave-impacts"
          icon="reports"
        />
        <MetricTile
          label="بحاجة لمراجعة المالية"
          value={isLoading ? '…' : summary?.needsFinanceReviewCount ?? 0}
          tone={(summary?.needsFinanceReviewCount ?? 0) > 0 ? 'gold' : 'success'}
          to="/app/payroll/leave-impacts"
          hint="بانتظار مراجعتكم"
          icon="workflow"
        />
        <MetricTile
          label="تعويض وقت بعد الدوام"
          value={isLoading ? '…' : summary?.afterHoursCompensationRequests ?? 0}
          tone="navy"
          to="/app/payroll/leave-impacts"
          icon="calendar"
        />
        <MetricTile
          label="أيام إجازة غير مغطّاة"
          value={isLoading ? '…' : summary?.totalUncoveredLeaveDays ?? 0}
          tone="navy"
          to="/app/payroll/leave-impacts"
          icon="kpi"
        />
      </div>

      <QuickActions items={quick} />

      <Card>
        <SectionTitle
          title="أحدث الطلبات المؤثّرة على الراتب"
          hint="قراءة فقط — للمراجعة المالية دون أي صرف"
          action={
            <Link to="/app/payroll/leave-impacts">
              <Button variant="ghost">عرض الكل</Button>
            </Link>
          }
        />
        {isLoading ? (
          <LoadingState label="يتم تحميل الطلبات…" />
        ) : items.length === 0 ? (
          <MiniEmpty text="لا توجد طلبات مؤثّرة حاليًّا" hint="تظهر هنا الإجازات/الأذونات ذات الأثر على الراتب بعد اعتمادها." />
        ) : (
          <ul>
            {items.slice(0, 8).map((it) => (
              <ActionItem
                key={it.leaveRequestId}
                title={it.requesterName}
                context={payrollImpactTypeLabel[it.impactType]}
                action={
                  <Link to="/app/payroll/leave-impacts">
                    <Button>مراجعة</Button>
                  </Link>
                }
              />
            ))}
          </ul>
        )}
      </Card>

      <FuturePlaceholders />
    </div>
  );
}

// ===== لوحة محفظة الحسابات (AccountPortfolioReader) =====
export function AccountPortfolioHomeDashboard() {
  const { data: projects, isLoading: pLoading } = useMyPortfolioProjects();
  const { data: clients, isLoading: cLoading } = useMyPortfolioClients();

  const projectList = projects ?? [];
  const clientList = clients ?? [];
  const activeProjects = projectList.filter((p) => p.status === 'Active').length;

  const quick = [
    { to: '/app/account-portfolio', label: 'محفظة الحسابات', hint: 'مشاريعي وعملائي' },
  ];

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <MetricTile label="مشاريع المحفظة" value={pLoading ? '…' : projectList.length} tone="navy" to="/app/account-portfolio" icon="projects" />
        <MetricTile label="مشاريع نشطة" value={pLoading ? '…' : activeProjects} tone="success" to="/app/account-portfolio" icon="projects" />
        <MetricTile label="العملاء" value={cLoading ? '…' : clientList.length} tone="navy" to="/app/account-portfolio" icon="clients" />
      </div>

      <QuickActions items={quick} />

      <div className="grid gap-5 lg:grid-cols-2">
        <Card>
          <SectionTitle
            title="مشاريعي"
            hint="ضمن محفظتك — عرض فقط"
            action={
              <Link to="/app/account-portfolio">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {pLoading ? (
            <LoadingState label="يتم تحميل المشاريع…" />
          ) : projectList.length === 0 ? (
            <MiniEmpty text="لا توجد مشاريع في محفظتك" hint="تظهر هنا المشاريع المُسنَدة إليك." />
          ) : (
            <ul>
              {projectList.slice(0, 8).map((p) => (
                <ActionItem
                  key={p.id}
                  title={p.name}
                  context={`${p.clientName ?? '—'} · ${serviceTypeLabel[p.serviceType]}`}
                  badge={<Badge tone={p.status === 'Active' ? 'success' : 'muted'}>{projectStatusLabel[p.status]}</Badge>}
                  action={
                    <Link to={`/app/account-portfolio/projects/${p.id}`}>
                      <Button>فتح</Button>
                    </Link>
                  }
                />
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <SectionTitle
            title="عملائي"
            hint="ضمن محفظتك — عرض فقط"
            action={
              <Link to="/app/account-portfolio">
                <Button variant="ghost">عرض الكل</Button>
              </Link>
            }
          />
          {cLoading ? (
            <LoadingState label="يتم تحميل العملاء…" />
          ) : clientList.length === 0 ? (
            <MiniEmpty text="لا يوجد عملاء في محفظتك" hint="يظهر هنا العملاء المرتبطون بمشاريعك." />
          ) : (
            <ul>
              {clientList.slice(0, 8).map((c) => (
                <ActionItem
                  key={c.id}
                  title={c.name}
                  context={`${c.activeProjectCount} مشروع نشط من ${c.projectCount}`}
                  action={
                    <Link to={`/app/account-portfolio/clients/${c.id}`}>
                      <Button>فتح</Button>
                    </Link>
                  }
                />
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}

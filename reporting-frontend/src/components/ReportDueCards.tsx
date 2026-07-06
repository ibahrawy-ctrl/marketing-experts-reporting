// RPT-DUE1 — بطاقات مواعيد التقارير (بسيطة، بلا إعادة تصميم).
// الموظّف: «حالة تقرير هذا الأسبوع». القادة/المدراء/الإدارة العليا: «متابعة مواعيد التقارير».
// كلّها قراءة فقط؛ النطاق يُفرَض خادميًّا (ScopeResolver). لا بريد ولا إشعارات.
import { useAuth } from '../lib/auth';
import { formatDate } from '../lib/format';
import { Card, Badge, Spinner } from './ui';
import { SectionTitle } from './dashboard';
import { useReportDueMyStatus, useReportDueOverview } from '../lib/useReportDue';
import type { DelayType, ReportDueOverdueRow } from '../types/api';

const delayTone: Record<DelayType, 'success' | 'alert' | 'gold' | 'orange'> = {
  NoDelay: 'success',
  EmployeeReportNotSubmitted: 'alert',
  TeamLeaderReviewOverdue: 'gold',
  ManagerReviewOverdue: 'orange',
  ExecutiveReviewPending: 'orange',
};

// بطاقة الموظّف: حالة تقرير الأسبوع الحالي لنفسه فقط.
export function MyReportDueCard() {
  const { data, isLoading, isError } = useReportDueMyStatus();
  if (isLoading) return <Card><Spinner /></Card>;
  if (isError || !data) return null;
  if (!data.expected) return null;

  const tone = data.isOverdue ? 'alert' : data.submitted ? 'success' : 'gold';
  return (
    <div>
      <SectionTitle title="حالة تقرير هذا الأسبوع" hint={data.weekLabel} />
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <p className="text-base font-semibold text-navy">{data.statusLabel}</p>
            <p className="text-xs text-ink-2">
              موعد التسليم: {formatDate(data.employeeDueDate)} · الأسبوع{' '}
              {formatDate(data.weekStart)} — {formatDate(data.weekEnd)}
            </p>
          </div>
          <Badge tone={tone}>
            {data.isOverdue ? 'متأخّر' : data.submitted ? 'مُسلَّم' : 'ضمن المهلة'}
          </Badge>
        </div>
      </Card>
    </div>
  );
}

function OverdueRow({ row }: { row: ReportDueOverdueRow }) {
  const place = [row.departmentName, row.teamName].filter(Boolean).join(' · ');
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line py-2 last:border-0">
      <div className="space-y-0.5">
        <p className="text-sm font-medium text-ink">
          {row.userName}
          <span className="mr-2 text-xs text-ink-2">({row.roleLabel})</span>
        </p>
        <p className="text-xs text-ink-2">
          {row.expectedAction}
          {place && <span> — {place}</span>}
        </p>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-xs text-ink-2">استحقاق {formatDate(row.dueDate)}</span>
        <Badge tone={delayTone[row.delayType]}>متأخّر {row.overdueDays} يوم</Badge>
      </div>
    </div>
  );
}

// بطاقة القادة/المدراء/الإدارة العليا: متابعة مواعيد التقارير ضمن النطاق.
export function TeamReportDueCard() {
  const { data, isLoading, isError } = useReportDueOverview();
  if (isLoading) return <Card><Spinner /></Card>;
  if (isError || !data) return null;

  return (
    <div>
      <SectionTitle title="متابعة مواعيد التقارير" hint={data.weekLabel} />
      <Card>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Stat label="مطلوبة" value={data.requiredReportsCount} />
          <Stat label="مُسلَّمة" value={data.submittedReportsCount} />
          <Stat label="متأخّرة" value={data.overdueReportsCount} tone="alert" />
          <Stat label="مراجعات متأخّرة" value={data.overdueReviewsCount} tone="gold" />
        </div>
        {data.items.length > 0 ? (
          <div className="mt-4">
            {data.items.map((row) => (
              <OverdueRow key={`${row.userId}-${row.delayType}`} row={row} />
            ))}
          </div>
        ) : (
          <p className="mt-4 text-sm text-success">لا يوجد تأخّر ضمن نطاقك لهذا الأسبوع.</p>
        )}
      </Card>
    </div>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: 'alert' | 'gold' }) {
  const color = tone === 'alert' ? 'text-alert' : tone === 'gold' ? 'text-gold' : 'text-navy';
  return (
    <div className="rounded-lg bg-offwhite p-3 text-center">
      <p className={`text-2xl font-bold ${color}`}>{value}</p>
      <p className="text-xs text-ink-2">{label}</p>
    </div>
  );
}

// المنطقة الموحّدة المعروضة في لوحة المعلومات: موظّف ⇒ بطاقته؛ القادة فما فوق ⇒ بطاقة المتابعة.
export function ReportDueSection() {
  const { hasAnyRole } = useAuth();
  const isReviewer = hasAnyRole('TeamLeader', 'Manager', 'GeneralManager', 'CEO', 'CeoSupport', 'Admin');
  return (
    <div className="space-y-6">
      <MyReportDueCard />
      {isReviewer && <TeamReportDueCard />}
    </div>
  );
}

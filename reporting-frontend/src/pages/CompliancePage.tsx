// متابعة الالتزام بالتقارير (per-person) — شاشة متابعة التزام فقط.
// تستهلك GET /api/reports/submission-compliance: من سلّم، من تأخّر، حالة كل موظف متوقَّع.
// ممنوع عرض: محتوى التقرير/الإجابات/ملاحظات المدير/أزرار اعتماد أو إرجاع/تفاصيل KPI.
// النطاق والصلاحية مفروضان خادمًا (Policy=ReportCompletionView). لا تصفية أمنية من الواجهة.
import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { Alert, Badge, Card, Field, Input, Select, StatCard } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { useDepartments, useTeams } from '../lib/useDirectory';
import { formatDateTime, formatPercent } from '../lib/format';
import type {
  ComplianceBreakdownReport,
  ComplianceSummaryReport,
  ComplianceTrendReport,
  LateByTemplateReport,
  SubmissionComplianceReport,
} from '../types/api';

const WEEK_KEY = /^\d{4}-W\d{2}$/;
const isWeekKey = (k: string) => WEEK_KEY.test(k.trim());

type StatusFilter = 'all' | 'submitted' | 'notSubmitted' | 'late';

export default function CompliancePage() {
  const [weekInput, setWeekInput] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [search, setSearch] = useState('');

  const weekKey = isWeekKey(weekInput) ? weekInput.trim() : undefined;
  const weekValid = weekInput.trim() === '' || isWeekKey(weekInput);

  const departments = useDepartments();
  const teams = useTeams();

  const params = useMemo(() => {
    const p: Record<string, string> = {};
    if (weekKey) p.weekKey = weekKey;
    if (departmentId) p.departmentId = departmentId;
    if (teamId) p.teamId = teamId;
    return p;
  }, [weekKey, departmentId, teamId]);

  const scopeKey = [weekKey ?? 'current', departmentId || 'all', teamId || 'all'] as const;

  const compliance = useQuery({
    queryKey: ['submission-compliance', ...scopeKey],
    queryFn: async () =>
      (await api.get<SubmissionComplianceReport>('/reports/submission-compliance', { params })).data,
    enabled: weekValid,
  });

  const summary = useQuery({
    queryKey: ['compliance-summary', ...scopeKey],
    queryFn: async () =>
      (await api.get<ComplianceSummaryReport>('/reports/compliance-summary', { params })).data,
    enabled: weekValid,
  });

  const trend = useQuery({
    queryKey: ['compliance-trend', departmentId || 'all', teamId || 'all'],
    queryFn: async () => {
      const p: Record<string, string> = { weeks: '8' };
      if (departmentId) p.departmentId = departmentId;
      if (teamId) p.teamId = teamId;
      return (await api.get<ComplianceTrendReport>('/reports/compliance-trend', { params: p })).data;
    },
  });

  const lateByTemplate = useQuery({
    queryKey: ['late-by-template', ...scopeKey],
    queryFn: async () =>
      (await api.get<LateByTemplateReport>('/reports/late-by-template', { params })).data,
    enabled: weekValid,
  });

  const breakdownGroupBy = teamId ? 'team' : departmentId ? 'team' : 'department';
  const breakdown = useQuery({
    queryKey: ['compliance-breakdown', breakdownGroupBy, ...scopeKey],
    queryFn: async () => {
      const p: Record<string, string> = { ...params, groupBy: breakdownGroupBy };
      return (await api.get<ComplianceBreakdownReport>('/reports/compliance-breakdown', { params: p })).data;
    },
    enabled: weekValid,
  });

  const teamsForDept = useMemo(() => {
    const all = teams.data ?? [];
    return departmentId ? all.filter((t) => t.departmentId === departmentId) : all;
  }, [teams.data, departmentId]);

  const filteredRows = useMemo(() => {
    const rows = compliance.data?.rows ?? [];
    const term = search.trim();
    return rows.filter((r) => {
      if (statusFilter === 'submitted' && !r.submitted) return false;
      if (statusFilter === 'notSubmitted' && r.submitted) return false;
      if (statusFilter === 'late' && !r.late) return false;
      if (term && !r.fullName.includes(term)) return false;
      return true;
    });
  }, [compliance.data, statusFilter, search]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">متابعة الالتزام بالتقارير</h1>
        <p className="mt-1 text-sm text-ink-2">
          متابعة التزام التسليم لكل موظف متوقَّع خلال الأسبوع: من سلّم، من تأخّر، ومن لم يُسلّم بعد.
        </p>
      </div>

      <Alert tone="navy">
        هذه شاشة <span className="font-bold">متابعة التزام فقط</span> — لا تعرض محتوى التقارير ولا إجاباتها
        ولا ملاحظات المديرين ولا أي تقييمات أداء. كل الأرقام محصورة بنطاق صلاحيتك ويُفرض ذلك على الخادم.
      </Alert>

      <Card>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Field label="الأسبوع (اختياري)">
            <Input value={weekInput} onChange={(e) => setWeekInput(e.target.value)} placeholder="2026-W25" />
          </Field>
          <Field label="الإدارة">
            <Select
              value={departmentId}
              onChange={(e) => {
                setDepartmentId(e.target.value);
                setTeamId('');
              }}
            >
              <option value="">كل الإدارات</option>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.nameAr}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="الفريق">
            <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
              <option value="">كل الفرق</option>
              {teamsForDept.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nameAr}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="الحالة">
            <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}>
              <option value="all">الكل</option>
              <option value="submitted">سلّم</option>
              <option value="notSubmitted">لم يسلّم</option>
              <option value="late">متأخر</option>
            </Select>
          </Field>
          <Field label="بحث باسم الموظف">
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="اسم الموظف…" />
          </Field>
        </div>
        {!weekValid && <p className="mt-2 text-xs text-alert">استخدم صيغة YYYY-Www مثل 2026-W25.</p>}
        {weekValid && weekInput.trim() === '' && (
          <p className="mt-2 text-xs text-ink-3">اتركه فارغًا للأسبوع الحالي.</p>
        )}
      </Card>

      {summary.data && <SummaryCards summary={summary.data} />}

      {trend.data && trend.data.points.length > 0 && <TrendSection trend={trend.data} />}

      <div className="grid gap-4 lg:grid-cols-2">
        {lateByTemplate.data && <LateByTemplateSection report={lateByTemplate.data} />}
        {breakdown.data && <BreakdownSection report={breakdown.data} />}
      </div>

      {compliance.isLoading ? (
        <LoadingState label="يتم تحميل قائمة الالتزام…" />
      ) : compliance.isError ? (
        <QueryError onRetry={() => compliance.refetch()} description="تعذّر جلب قائمة الالتزام بالتقارير." />
      ) : (
        <ComplianceTable report={compliance.data!} rows={filteredRows} />
      )}
    </div>
  );
}

// بطاقات ملخّص الالتزام: متوقَّع / سلّم / في الموعد / متأخر / لم يسلّم وانقضى + النسب.
function SummaryCards({ summary }: { summary: ComplianceSummaryReport }) {
  return (
    <div className="space-y-3">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="نسبة الالتزام" value={formatPercent(summary.compliancePercent)} tone="navy" />
        <StatCard label="نسبة الالتزام في الموعد" value={formatPercent(summary.onTimePercent)} tone="success" />
        <StatCard label="متوقَّع" value={summary.expected} />
        <StatCard label="سلّم" value={summary.submitted} tone="success" />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="في الموعد" value={summary.onTime} tone="success" />
        <StatCard label="متأخر (إجمالي)" value={summary.late} tone={summary.late > 0 ? 'gold' : 'navy'} />
        <StatCard label="سلّم متأخرًا" value={summary.lateSubmitted} tone={summary.lateSubmitted > 0 ? 'gold' : 'navy'} />
        <StatCard label="لم يسلّم وانقضى موعده" value={summary.missingOverdue} tone={summary.missingOverdue > 0 ? 'alert' : 'navy'} />
      </div>
    </div>
  );
}

// اتجاه الالتزام عبر آخر 8 أسابيع (الأقدم → الأحدث) — أشرطة نسبة مبسّطة بلا مكتبة رسم.
function TrendSection({ trend }: { trend: ComplianceTrendReport }) {
  return (
    <Card>
      <h2 className="mb-3 text-lg font-bold text-navy">اتجاه الالتزام — آخر {trend.weeks} أسابيع</h2>
      <div className="space-y-2">
        {trend.points.map((p) => (
          <div key={p.periodKey} className="flex items-center gap-3">
            <span className="w-28 shrink-0 text-xs text-ink-2">{p.periodLabel}</span>
            <div className="relative h-5 flex-1 overflow-hidden rounded bg-navy-50">
              <div
                className="absolute inset-y-0 right-0 rounded bg-success/70"
                style={{ width: `${Math.min(100, Math.max(0, p.compliancePercent))}%` }}
              />
            </div>
            <span className="w-32 shrink-0 text-left text-xs text-ink-2">
              {formatPercent(p.compliancePercent)} · في الموعد {formatPercent(p.onTimePercent)}
            </span>
          </div>
        ))}
      </div>
    </Card>
  );
}

// القوالب/المسمّيات الأكثر تأخّرًا ضمن الأسبوع (تنازليًّا حسب نسبة التأخّر).
function LateByTemplateSection({ report }: { report: LateByTemplateReport }) {
  return (
    <Card className="overflow-x-auto p-0">
      <div className="border-b border-line px-4 py-3">
        <h2 className="text-base font-bold text-navy">الأكثر تأخّرًا حسب القالب/المسمّى</h2>
      </div>
      <table className="w-full min-w-[420px] text-right text-sm">
        <thead className="border-b border-line bg-navy-50 text-xs text-ink-2">
          <tr>
            <th className="px-4 py-2 font-semibold">القالب / المسمّى</th>
            <th className="px-4 py-2 font-semibold">متوقَّع</th>
            <th className="px-4 py-2 font-semibold">متأخر</th>
            <th className="px-4 py-2 font-semibold">نسبة التأخّر</th>
          </tr>
        </thead>
        <tbody>
          {report.rows.length === 0 ? (
            <tr>
              <td colSpan={4} className="px-4 py-8 text-center text-ink-3">لا تأخّر في هذا الأسبوع.</td>
            </tr>
          ) : (
            report.rows.map((r) => (
              <tr key={r.jobRoleId} className="border-b border-line last:border-0">
                <td className="px-4 py-2 text-ink">{r.templateTitle || r.jobRoleName}</td>
                <td className="px-4 py-2 text-ink-2">{r.expected}</td>
                <td className="px-4 py-2 text-ink-2">{r.late}</td>
                <td className="px-4 py-2">
                  <Badge tone={r.latePercent > 0 ? 'gold' : 'success'}>{formatPercent(r.latePercent)}</Badge>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </Card>
  );
}

// تجميع الالتزام حسب الفريق/الإدارة (تصاعديًّا حسب نسبة الالتزام = الأضعف أولًا).
function BreakdownSection({ report }: { report: ComplianceBreakdownReport }) {
  const groupLabel = report.groupBy === 'team' ? 'الفريق' : 'الإدارة';
  return (
    <Card className="overflow-x-auto p-0">
      <div className="border-b border-line px-4 py-3">
        <h2 className="text-base font-bold text-navy">الالتزام حسب {groupLabel}</h2>
      </div>
      <table className="w-full min-w-[420px] text-right text-sm">
        <thead className="border-b border-line bg-navy-50 text-xs text-ink-2">
          <tr>
            <th className="px-4 py-2 font-semibold">{groupLabel}</th>
            <th className="px-4 py-2 font-semibold">متوقَّع</th>
            <th className="px-4 py-2 font-semibold">سلّم</th>
            <th className="px-4 py-2 font-semibold">نسبة الالتزام</th>
          </tr>
        </thead>
        <tbody>
          {report.rows.length === 0 ? (
            <tr>
              <td colSpan={4} className="px-4 py-8 text-center text-ink-3">لا بيانات لهذا التجميع.</td>
            </tr>
          ) : (
            report.rows.map((r) => (
              <tr key={r.groupId ?? r.groupName} className="border-b border-line last:border-0">
                <td className="px-4 py-2 text-ink">{r.groupName}</td>
                <td className="px-4 py-2 text-ink-2">{r.expected}</td>
                <td className="px-4 py-2 text-ink-2">{r.submitted}</td>
                <td className="px-4 py-2">
                  <Badge tone={r.compliancePercent >= 80 ? 'success' : r.compliancePercent >= 50 ? 'gold' : 'alert'}>
                    {formatPercent(r.compliancePercent)}
                  </Badge>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </Card>
  );
}

function ComplianceTable({
  report,
  rows,
}: {
  report: SubmissionComplianceReport;
  rows: SubmissionComplianceReport['rows'];
}) {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
        <StatCard label="الفترة" value={report.periodLabel} />
        <StatCard label="المتوقَّع" value={report.expected} />
        <StatCard label="سلّم" value={report.submitted} tone="success" />
        <StatCard label="لم يسلّم" value={report.notSubmitted} tone={report.notSubmitted > 0 ? 'alert' : 'navy'} />
        <StatCard label="نسبة الالتزام" value={formatPercent(report.completionRate)} />
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[760px] text-right text-sm">
          <thead className="border-b border-line bg-navy-50 text-xs text-ink-2">
            <tr>
              <th className="px-4 py-3 font-semibold">الموظف</th>
              <th className="px-4 py-3 font-semibold">الإدارة</th>
              <th className="px-4 py-3 font-semibold">الفريق</th>
              <th className="px-4 py-3 font-semibold">المسمى الوظيفي</th>
              <th className="px-4 py-3 font-semibold">الحالة</th>
              <th className="px-4 py-3 font-semibold">التسليم</th>
              <th className="px-4 py-3 font-semibold">تاريخ التسليم</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-ink-3">
                  لا توجد صفوف مطابقة للمرشّحات الحالية.
                </td>
              </tr>
            ) : (
              rows.map((r) => (
                <tr key={r.userId} className="border-b border-line last:border-0">
                  <td className="px-4 py-3 font-medium text-ink">{r.fullName}</td>
                  <td className="px-4 py-3 text-ink-2">{r.departmentName ?? '—'}</td>
                  <td className="px-4 py-3 text-ink-2">{r.teamName ?? '—'}</td>
                  <td className="px-4 py-3 text-ink-2">{r.jobRoleName ?? '—'}</td>
                  <td className="px-4 py-3">
                    <Badge tone={r.submitted ? 'navy' : 'muted'}>{r.statusLabel}</Badge>
                  </td>
                  <td className="px-4 py-3">
                    {!r.submitted ? (
                      <Badge tone="alert">لم يسلّم</Badge>
                    ) : r.late ? (
                      <Badge tone="gold">سلّم — متأخر</Badge>
                    ) : (
                      <Badge tone="success">سلّم — في الموعد</Badge>
                    )}
                  </td>
                  <td className="px-4 py-3 text-ink-2">
                    {r.submittedAtUtc ? formatDateTime(r.submittedAtUtc) : '—'}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </Card>
    </div>
  );
}

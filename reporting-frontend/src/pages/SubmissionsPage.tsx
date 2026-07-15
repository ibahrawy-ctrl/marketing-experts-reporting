import { useMemo, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage, approvalErrorMessage } from '../lib/api';
import { useToast, POST_SUCCESS_NAV_DELAY_MS } from '../components/ActionResultToast';
import { useAuth } from '../lib/auth';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { useProjects } from '../lib/useClients';
import { useActiveCourses } from '../lib/useCourses';
import { useActiveServices } from '../lib/useServices';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { ApprovalPath, ProgressBar, type PathStep } from '../components/dashboard';
import { ManagementNotesPanel } from '../components/ManagementNotesPanel';
import {
  submissionStatusLabel,
  periodTypeLabel,
  approvalStatusLabel,
  formatDate,
} from '../lib/format';
import { WeeklyCycleCalendarPicker } from '../components/WeeklyCycleCalendarPicker';
import { DailyCalendarPicker } from '../components/DailyCalendarPicker';
import { normalizeDigits, sanitizeNumericInput, isNumericGridColumn } from '../lib/numericNormalizer';
import type {
  SubmissionListItem,
  SubmissionDto,
  SubmissionFieldValueDto,
  ReportTemplateListItem,
  FieldValueInput,
  FieldConfig,
  PeriodType,
  SubmissionStatus,
  ApprovalStepDto,
  ProjectDto,
  ProjectRepeatableConfig,
  ProjectRepeatableEntry,
  RepeatableSubField,
  ReportingDayDto,
} from '../types/api';

type Tab = 'all' | 'mine' | 'pending';
const MANAGEMENT_ROLES = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'] as const;
const LATE_STATES: SubmissionStatus[] = ['Draft', 'Returned'];
// «تحتاج إجراء» = التقارير المتوقّفة التي تتطلّب تحرّكًا من صاحبها/الفريق.
// تعريف موحّد مع بطاقة لوحة التحكم (DashboardService): مسودّة + معادة + مصعّدة.
// «بانتظار اعتمادي» (currentApproverId) بُعد منفصل ولا يُدمج هنا.
const NEEDS_ACTION_STATES: SubmissionStatus[] = ['Draft', 'Returned', 'Escalated'];

const statusTone: Partial<Record<SubmissionStatus, 'navy' | 'success' | 'orange' | 'alert' | 'gold'>> = {
  Draft: 'gold',
  Submitted: 'navy',
  Returned: 'alert',
  Escalated: 'orange',
  Closed: 'success',
  Visible: 'success',
};

export default function SubmissionsPage() {
  // الحالة محفوظة في رابط الصفحة (?tab=&open=) لدعم الروابط العميقة من اللوحات.
  const [params, setParams] = useSearchParams();
  const { hasAnyRole, canApprove } = useAuth();
  const isManagement = hasAnyRole(...MANAGEMENT_ROLES);
  const teamParam = params.get('team');

  const requested = params.get('tab');
  const tab: Tab =
    requested === 'pending' && canApprove
      ? 'pending'
      : requested === 'mine'
        ? 'mine'
        : isManagement
          ? 'all'
          : 'mine';
  const openId = params.get('open');

  const setTab = (t: Tab) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('tab', t); n.delete('open'); return n; });
  const open = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <SubmissionDetail id={openId} onBack={back} />;

  // «بانتظار اعتمادي» يظهر فقط لمن يملك صلاحية الاعتماد (Admin/CEO/GM/Manager/TeamLeader).
  // الموظف/المُطّلع/مساند الإدارة لا يعتمدون تقارير الآخرين فلا يُعرض لهم التبويب.
  const tabs: [Tab, string][] = [
    ...(isManagement ? ([['all', 'كل التقارير']] as [Tab, string][]) : []),
    ['mine', 'تقاريري'],
    ...(canApprove ? ([['pending', 'بانتظار اعتمادي']] as [Tab, string][]) : []),
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-navy">التقارير المقدمة</h1>
      <div className="flex gap-2 border-b border-line">
        {tabs.map(([k, label]) => (
          <button
            key={k}
            onClick={() => setTab(k)}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-semibold ${
              tab === k ? 'border-orange text-navy' : 'border-transparent text-ink-2'
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      {tab === 'all' && isManagement && <AllReportsTab onOpen={open} initialTeam={teamParam} />}
      {tab === 'mine' && <MineTab onOpen={open} />}
      {tab === 'pending' && <PendingTab onOpen={open} />}
    </div>
  );
}

// ===== تبويب «كل التقارير» — جدول متقدّم بفلاتر متعددة (للإدارة) =====
type QuickFilter = '' | 'late' | 'mine-approval' | 'returned' | 'closed' | 'needs-action';

function AllReportsTab({ onOpen, initialTeam }: { onOpen: (id: string) => void; initialTeam: string | null }) {
  const { user, hasAnyRole } = useAuth();
  // العرض الافتراضي حسب الدور: المعتمِدون المباشرون يبدؤون على «بانتظار اعتمادي»،
  // والإدارة العليا على «الكل».
  const defaultQuick: QuickFilter = hasAnyRole('TeamLeader', 'Manager') ? 'mine-approval' : '';
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['submissions-all'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions')).data,
  });
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();

  const [team, setTeam] = useState(initialTeam ?? '');
  const [dept, setDept] = useState('');
  const [employee, setEmployee] = useState('');
  const [template, setTemplate] = useState('');
  const [period, setPeriod] = useState('');
  const [status, setStatus] = useState('');
  const [quick, setQuick] = useState<QuickFilter>(defaultQuick);

  const userName = (id: string | null) => (users.data ?? []).find((u) => u.id === id)?.fullName ?? '—';

  const filtered = useMemo(() => {
    let rows = items ?? [];
    if (team) rows = rows.filter((s) => s.teamId === team);
    if (dept) rows = rows.filter((s) => s.departmentId === dept);
    if (employee) rows = rows.filter((s) => s.submitterId === employee);
    if (template) rows = rows.filter((s) => s.templateTitle === template);
    if (period) rows = rows.filter((s) => s.periodKey === period);
    if (status) rows = rows.filter((s) => s.status === status);
    if (quick === 'late') rows = rows.filter((s) => LATE_STATES.includes(s.status));
    if (quick === 'mine-approval') rows = rows.filter((s) => s.currentApproverId === user?.userId);
    if (quick === 'returned') rows = rows.filter((s) => s.status === 'Returned');
    if (quick === 'closed') rows = rows.filter((s) => s.status === 'Closed' || s.status === 'Visible');
    if (quick === 'needs-action') rows = rows.filter((s) => NEEDS_ACTION_STATES.includes(s.status));
    return rows;
  }, [items, team, dept, employee, template, period, status, quick, user?.userId]);

  if (isLoading) return <LoadingState label="يتم تحميل التقارير…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب قائمة التقارير. أعد المحاولة." />;

  const all = items ?? [];
  const templateNames = [...new Set(all.map((s) => s.templateTitle))].sort();
  const periods = [...new Set(all.map((s) => s.periodKey))].sort().reverse();

  // بطاقات ملخّص قابلة للنقر — كل بطاقة تضبط الفلتر السريع المقابل.
  const myApproval = all.filter((s) => s.currentApproverId === user?.userId).length;
  const lateCount = all.filter((s) => LATE_STATES.includes(s.status)).length;
  const returnedCount = all.filter((s) => s.status === 'Returned').length;
  const closedCount = all.filter((s) => s.status === 'Closed' || s.status === 'Visible').length;
  const needsActionCount = all.filter((s) => NEEDS_ACTION_STATES.includes(s.status)).length;
  const SUMMARY: { key: QuickFilter; label: string; value: number; tone: 'navy' | 'orange' | 'alert' | 'gold' | 'success' }[] = [
    { key: '', label: 'إجمالي التقارير', value: all.length, tone: 'navy' },
    { key: 'needs-action', label: 'يحتاج إجراء الآن', value: needsActionCount, tone: needsActionCount > 0 ? 'orange' : 'success' },
    { key: 'mine-approval', label: 'بانتظار اعتمادي', value: myApproval, tone: myApproval > 0 ? 'gold' : 'success' },
    { key: 'late', label: 'متأخرة', value: lateCount, tone: lateCount > 0 ? 'alert' : 'success' },
    { key: 'returned', label: 'معادة للتعديل', value: returnedCount, tone: returnedCount > 0 ? 'gold' : 'success' },
    { key: 'closed', label: 'مغلقة', value: closedCount, tone: 'success' },
  ];

  const QUICKS: [QuickFilter, string][] = [
    ['', 'الكل'],
    ['needs-action', 'يحتاج إجراء'],
    ['late', 'المتأخرة'],
    ['mine-approval', 'بانتظار اعتمادي'],
    ['returned', 'المعادة'],
    ['closed', 'المغلقة'],
  ];

  const toneText: Record<'navy' | 'orange' | 'alert' | 'gold' | 'success', string> = {
    navy: 'text-navy', orange: 'text-orange-600', alert: 'text-alert', gold: 'text-gold', success: 'text-success',
  };

  return (
    <div className="space-y-4">
      {/* بطاقات ملخّص قابلة للنقر — لمحة سريعة + فلترة بضغطة. */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
        {SUMMARY.map((c) => (
          <button
            key={c.label}
            onClick={() => setQuick(c.key)}
            className={`rounded-2xl border bg-white p-4 text-right transition hover:shadow-sm ${
              quick === c.key ? 'border-orange ring-1 ring-orange' : 'border-line'
            }`}
          >
            <p className={`text-2xl font-extrabold ${toneText[c.tone]}`}>{c.value}</p>
            <p className="mt-0.5 text-xs text-ink-2">{c.label}</p>
          </button>
        ))}
      </div>

      <Card>
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
          <Select value={team} onChange={(e) => setTeam(e.target.value)}>
            <option value="">كل الفرق</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>{t.nameAr}</option>
            ))}
          </Select>
          <Select value={dept} onChange={(e) => setDept(e.target.value)}>
            <option value="">كل الإدارات</option>
            {(departments.data ?? []).map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
          <Select value={employee} onChange={(e) => setEmployee(e.target.value)}>
            <option value="">كل الموظفين</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
          <Select value={template} onChange={(e) => setTemplate(e.target.value)}>
            <option value="">كل أنواع التقارير</option>
            {templateNames.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </Select>
          <Select value={period} onChange={(e) => setPeriod(e.target.value)}>
            <option value="">كل الفترات</option>
            {periods.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </Select>
          <Select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">كل الحالات</option>
            {(Object.keys(submissionStatusLabel) as SubmissionStatus[]).map((s) => (
              <option key={s} value={s}>{submissionStatusLabel[s]}</option>
            ))}
          </Select>
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          {QUICKS.map(([k, label]) => (
            <button
              key={label}
              onClick={() => setQuick(k)}
              className={`rounded-full border px-3 py-1 text-xs font-semibold ${
                quick === k ? 'border-navy bg-navy text-white' : 'border-line text-ink-2 hover:bg-offwhite'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </Card>

      <Card className="overflow-x-auto p-0">
        {filtered.length === 0 ? (
          <div className="py-12 text-center">
            <p className="text-sm font-medium text-ink-2">
              {all.length === 0 ? 'لا توجد تقارير بعد.' : 'لا توجد تقارير مطابقة للفلاتر.'}
            </p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {all.length === 0
                ? 'تظهر التقارير هنا بمجرّد أن يبدأ الموظفون في تسليمها. تُنشأ التقارير من قوالب منشورة في تبويب «تقاريري».'
                : 'لا يوجد تقرير يطابق الفلاتر المحدّدة حاليًا. جرّب توسيع نطاق الفلاتر أو إعادة ضبطها.'}
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[980px] text-right text-sm">
            <thead className="sticky top-0 z-10 border-b border-line bg-offwhite text-xs text-ink-2 shadow-sm">
              <tr>
                <th className="px-3 py-2.5 font-semibold">التقرير</th>
                <th className="px-3 py-2.5 font-semibold">صاحب التقرير</th>
                <th className="px-3 py-2.5 font-semibold">الفترة</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">تاريخ الإرسال</th>
                <th className="px-3 py-2.5 font-semibold">المسؤول الحالي</th>
                <th className="px-3 py-2.5 font-semibold">متأخر؟</th>
                <th className="px-3 py-2.5 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => {
                const late = LATE_STATES.includes(s.status);
                return (
                  <tr
                    key={s.id}
                    onClick={() => onOpen(s.id)}
                    className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
                  >
                    <td className="px-3 py-2.5 font-semibold text-navy hover:text-orange hover:underline">{s.templateTitle}</td>
                    <td className="px-3 py-2.5 text-ink-2">{s.submitterName}</td>
                    <td className="px-3 py-2.5 text-ink-2">{periodTypeLabel[s.periodType]} {s.periodKey}</td>
                    <td className="px-3 py-2.5">
                      <Badge tone={statusTone[s.status] ?? 'muted'}>{submissionStatusLabel[s.status]}</Badge>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{formatDate(s.submittedAtUtc)}</td>
                    <td className="px-3 py-2.5 text-ink-2">{s.currentApproverId ? userName(s.currentApproverId) : '—'}</td>
                    <td className="px-3 py-2.5">
                      {late ? <Badge tone="alert">متأخر</Badge> : <span className="text-ink-3">—</span>}
                    </td>
                    <td className="px-3 py-2.5">
                      <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(s.id); }}>عرض التقرير</Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </Card>
      <p className="text-xs text-ink-3">إجمالي المعروض: {filtered.length} من {all.length} تقرير.</p>
    </div>
  );
}

function SubmissionTable({
  items,
  onOpen,
  onDeleteDraft,
  deletingId,
  showSubmitter,
  emptyText = 'لا توجد تقارير.',
  emptyHint,
}: {
  items: SubmissionListItem[];
  onOpen: (id: string) => void;
  onDeleteDraft?: (id: string) => void;
  deletingId?: string | null;
  showSubmitter?: boolean;
  emptyText?: string;
  emptyHint?: string;
}) {
  if (!items.length)
    return (
      <div className="py-10 text-center">
        <p className="text-sm font-medium text-ink-2">{emptyText}</p>
        {emptyHint ? <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">{emptyHint}</p> : null}
      </div>
    );
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-right text-ink-2">
          <th className="pb-2">القالب</th>
          {showSubmitter && <th className="pb-2">المُرسِل</th>}
          <th className="pb-2">الفترة</th>
          <th className="pb-2">الحالة</th>
          <th className="pb-2"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((s) => (
          <tr key={s.id} className="border-t border-line">
            <td className="py-2">{s.templateTitle}</td>
            {showSubmitter && <td className="py-2">{s.submitterName}</td>}
            <td className="py-2">{periodTypeLabel[s.periodType]} {s.periodKey}</td>
            <td className="py-2"><Badge tone={statusTone[s.status] ?? 'muted'}>{submissionStatusLabel[s.status]}</Badge></td>
            <td className="py-2 text-left">
              <div className="flex items-center justify-end gap-1">
                <Button variant="ghost" onClick={() => onOpen(s.id)}>عرض</Button>
                {onDeleteDraft && s.status === 'Draft' && (
                  <Button
                    variant="danger"
                    disabled={deletingId === s.id}
                    onClick={() => onDeleteDraft(s.id)}
                  >
                    حذف المسودة
                  </Button>
                )}
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function MineTab({ onOpen }: { onOpen: (id: string) => void }) {
  const qc = useQueryClient();
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['submissions-mine'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/mine')).data,
  });
  // assignedOnly: قائمة الإنشاء تعرض فقط القوالب المربوطة بدور المستخدم — لا قوالب عامة ولا أدوار أخرى.
  const { data: templates } = useQuery({
    queryKey: ['report-templates', 'published', 'assigned'],
    queryFn: async () =>
      (await api.get<ReportTemplateListItem[]>('/report-templates', { params: { status: 'Published', isActive: true, assignedOnly: true } })).data,
  });

  const { user } = useAuth();
  // الدورية مفروضة خادميًّا حسب الدور: مندوبو المبيعات «يومي»، وبقية الأدوار «أسبوعي». تُعرض كقيمة ثابتة لا اختيار.
  const periodType: PeriodType = user?.expectedReportCadence ?? 'Weekly';
  const isDaily = periodType === 'Daily';

  // مفتاح الفترة الافتراضي فارغ في الوضعين: يملؤه منتقي التقويم الخادميّ (يوميّ my-days أو أسبوعيّ my-cycles)
  // باليوم الحاليّ/الدورة الحالية المحسوبة خادميًّا. لا حساب محليّ لأيّ مفتاح، ولا إدخال نصّيّ/تاريخ حرّ.
  const defaultPeriodKey = () => '';

  const [reportTemplateId, setReportTemplateId] = useState('');
  const [periodKey, setPeriodKey] = useState(defaultPeriodKey);
  // اليوميّ: هل اليوم المختار مفتوح للإنشاء؟ (يُقفَل للعطلة/المستقبل عبر الخادم). الأسبوعيّ يعتمد وجود المفتاح.
  const [dayOpenForDraft, setDayOpenForDraft] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  // البند 9 — منع ازدواج التقارير الأسبوعية الإلزامية: نوضّح أيّ قالب «أساسي مطلوب» وأيّها «تكميلي اختياري».
  const hasPrimary = (templates ?? []).some((t) => t.classification === 'Primary');
  const hasSupplementary = (templates ?? []).some((t) => t.classification === 'Supplementary');

  const create = useMutation({
    mutationFn: () => api.post<SubmissionDto>('/submissions', { reportTemplateId, periodType, periodKey }),
    onSuccess: (res) => {
      setPeriodKey(defaultPeriodKey());
      setDayOpenForDraft(false); // يُعاد ضبطه تلقائيًّا عند إعادة اختيار اليوم الحاليّ من المنتقي
      void qc.invalidateQueries({ queryKey: ['submissions-mine'] });
      onOpen(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const deleteDraft = useMutation({
    mutationFn: (submissionId: string) => api.delete(`/submissions/${submissionId}`),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['submissions-mine'] }); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل تقاريري…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب تقاريرك. أعد المحاولة." />;

  return (
    <div className="space-y-4">
      <Card>
        {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
        <div className="mb-3">
          <div className="text-sm font-semibold text-navy">إنشاء تقريري</div>
          <div className="text-xs text-navy/60">
            تُنشئ هنا تقريرك الخاص. تظهر القوالب المناسبة لدورك أنت فقط — لا قوالب المرؤوسين ولا الأدوار الأخرى.
          </div>
        </div>
        {templates && templates.length === 0 && (
          <div className="mb-3">
            <Alert tone="navy">
              لا يوجد قالب تقرير مخصص لهذا الدور بعد. يرجى مراجعة الإدارة/الحوكمة.
            </Alert>
          </div>
        )}
        <div className="mb-3">
          <Alert tone="navy">
            {isDaily
              ? 'دورية تقاريرك «يومية» (مندوب مبيعات) في المرحلة الحالية، وهي مفروضة تلقائيًّا. التقويم الكامل وآلية الإغلاق سيُدعمان لاحقًا.'
              : 'دورية تقاريرك «أسبوعية» في المرحلة الحالية، وهي مفروضة تلقائيًّا. التقويم الكامل وآلية الإغلاق سيُدعمان لاحقًا.'}
          </Alert>
        </div>
        {hasPrimary && hasSupplementary && (
          <div className="mb-3">
            <Alert tone="gold">
              لديك أكثر من قالب لنفس الأسبوع: القالب «المطلوب» هو تقريرك الأساسي الإلزامي، أمّا القالب «الاختياري» فهو متابعة/استبيان تكميلي لا يُعدّ إلزاميًّا. (دمج القالبين في تقرير موحّد قيد الدراسة ضمن حوكمة القوالب لاحقًا.)
            </Alert>
          </div>
        )}
        <div className="space-y-3">
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-72">
              <Field label="القالب">
                <Select value={reportTemplateId} onChange={(e) => setReportTemplateId(e.target.value)}>
                  <option value="">اختر قالبًا…</option>
                  {templates?.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.title}{t.classification === 'Supplementary' ? ' — اختياري (استبيان/متابعة)' : ' — مطلوب'}
                    </option>
                  ))}
                </Select>
              </Field>
              <div className="mt-1 text-xs text-navy/60">تظهر هنا القوالب المناسبة لدور صاحب التقرير فقط.</div>
            </div>
            <div className="w-40">
              <Field label="الدورية">
                <div className="flex h-10 items-center">
                  <Badge tone="navy">{periodTypeLabel[periodType]}</Badge>
                </div>
              </Field>
            </div>
          </div>

          {/* يوميّ: منتقي اليوم التقريريّ المُدرِك للدور والحالة (يحسب اليوم الحاليّ ومفتاحه خادميًّا). */}
          {isDaily && (
            <div className="max-w-md">
              <Field label="الفترة (يوم)">
                <DailyCalendarPicker
                  templateId={reportTemplateId || null}
                  value={periodKey || null}
                  onChange={(key: string, day: ReportingDayDto) => {
                    setErr(null);
                    setPeriodKey(key);
                    setDayOpenForDraft(day.isOpenForDraft);
                  }}
                />
              </Field>
              <div className="mt-3">
                <Button disabled={!reportTemplateId || !periodKey || !dayOpenForDraft || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
                  إنشاء تقرير
                </Button>
              </div>
            </div>
          )}

          {/* أسبوعيّ: منتقي دورة التقارير المُدرِك للدور (يحسب الدورة الحالية وتاريخ الاستحقاق خادميًّا). */}
          {!isDaily && (
            <div className="max-w-md">
              <Field label="الفترة (أسبوع)">
                <WeeklyCycleCalendarPicker
                  context="Report"
                  value={periodKey || null}
                  onChange={(key) => { setErr(null); setPeriodKey(key); }}
                />
              </Field>
              <div className="mt-3">
                <Button disabled={!reportTemplateId || !periodKey || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
                  إنشاء تقرير
                </Button>
              </div>
            </div>
          )}
        </div>
      </Card>
      <Card>
        <SubmissionTable
          items={items ?? []}
          onOpen={onOpen}
          onDeleteDraft={(sid) => {
            setErr(null);
            if (window.confirm('هل تريد حذف هذه المسودة؟ لا يمكن التراجع عن هذا الإجراء.')) deleteDraft.mutate(sid);
          }}
          deletingId={deleteDraft.isPending ? deleteDraft.variables ?? null : null}
          emptyText="لم تُنشئ أي تقرير بعد."
          emptyHint="اختر قالبًا والفترة من الأعلى ثم اضغط «إنشاء تقرير» لبدء أول تقرير لك. ستظهر تقاريرك هنا."
        />
      </Card>
    </div>
  );
}

function PendingTab({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['submissions-pending'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/pending-approvals')).data,
  });
  if (isLoading) return <LoadingState label="يتم تحميل التقارير بانتظار اعتمادك…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب التقارير بانتظار اعتمادك. أعد المحاولة." />;
  return (
    <Card>
      <SubmissionTable
        items={items ?? []}
        onOpen={onOpen}
        showSubmitter
        emptyText="لا توجد تقارير بانتظار اعتمادك."
        emptyHint="تظهر هنا التقارير عندما يُرسلها أعضاء فريقك للاعتماد. لا حاجة لأي إجراء الآن."
      />
    </Card>
  );
}

function fieldInputKind(
  t: SubmissionFieldValueDto['fieldType'],
): 'section' | 'select' | 'multiselect' | 'grid' | 'longtext' | 'number' | 'date' | 'bool' | 'text' {
  if (t === 'SectionHeader') return 'section';
  if (t === 'SingleSelect') return 'select';
  if (t === 'MultiSelect') return 'multiselect';
  if (t === 'TableGrid') return 'grid';
  if (['LongText', 'RichText'].includes(t)) return 'longtext';
  if (['Number', 'Decimal', 'Currency', 'Percentage', 'Rating', 'Scale'].includes(t)) return 'number';
  if (['Date', 'DateTime', 'Time'].includes(t)) return 'date';
  if (t === 'Boolean') return 'bool';
  return 'text';
}

function parseConfig(json: string | null): FieldConfig {
  if (!json) return {};
  try {
    return JSON.parse(json) as FieldConfig;
  } catch {
    return {};
  }
}

// شبكة الجدول: مصفوفة صفوف، كل صف مصفوفة خلايا نصية. تُخزَّن في valueJson.
export function parseGrid(json: string | null | undefined): string[][] {
  if (!json) return [];
  try {
    const v = JSON.parse(json);
    return Array.isArray(v) ? (v as string[][]) : [];
  } catch {
    return [];
  }
}

// ===== قسم المشاريع المتكرر — تحليل الإعداد والقيمة =====
export function parseRepeatableConfig(json: string | null): ProjectRepeatableConfig {
  const fallback: ProjectRepeatableConfig = { projectRequired: true, minProjects: 1, maxProjects: 10, fields: [] };
  if (!json) return fallback;
  try {
    const p = JSON.parse(json) as Partial<ProjectRepeatableConfig>;
    return {
      projectRequired: p.projectRequired ?? true,
      minProjects: Number.isFinite(p.minProjects) ? Number(p.minProjects) : 1,
      maxProjects: Number.isFinite(p.maxProjects) ? Number(p.maxProjects) : 10,
      fields: Array.isArray(p.fields) ? p.fields : [],
    };
  } catch {
    return fallback;
  }
}

export function parseRepeatableEntries(json: string | null | undefined): ProjectRepeatableEntry[] {
  if (!json) return [];
  try {
    const v = JSON.parse(json);
    if (!Array.isArray(v)) return [];
    return (v as ProjectRepeatableEntry[]).map((e) => ({
      projectId: e?.projectId ?? null,
      answers: e?.answers && typeof e.answers === 'object' ? e.answers : {},
    }));
  } catch {
    return [];
  }
}

// نوع إدخال الحقل الفرعي داخل القسم المتكرر.
export function subFieldInputKind(t: RepeatableSubField['type']): 'number' | 'longtext' | 'date' | 'bool' | 'select' | 'text' {
  if (['Currency', 'Number', 'Decimal', 'Percentage'].includes(t)) return 'number';
  if (t === 'LongText') return 'longtext';
  if (t === 'Date') return 'date';
  if (t === 'Boolean') return 'bool';
  if (t === 'Select') return 'select';
  return 'text';
}

function SubmissionDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { user, hasAnyRole } = useAuth();
  const canAdminDelete = hasAnyRole('Admin', 'CEO', 'GeneralManager');
  const { data: sub, isLoading, isError, refetch } = useQuery({
    queryKey: ['submission', id],
    queryFn: async () => (await api.get<SubmissionDto>(`/submissions/${id}`)).data,
  });
  // قابلة للاختيار فقط (نشطة + عميل غير مؤرشف + ضمن النطاق) لقوائم اختيار قسم المشاريع الجديدة.
  const { data: selectableProjects } = useProjects({ selectableOnly: true });
  // كل المشاريع ضمن النطاق (تشمل المؤرشفة) — لحلّ أسماء المشاريع في تفاصيل التقارير القديمة فقط.
  const { data: allProjects } = useProjects({ includeClosed: true });
  // كتالوج الدورات النشطة — يغذّي منتقي «الدورة» في شبكة قالب مبيعات B2C.
  const { data: activeCourses } = useActiveCourses();
  const courseNames = useMemo(() => (activeCourses ?? []).map((c) => c.nameAr), [activeCourses]);
  // كتالوج خدمات B2B النشطة — يغذّي منتقي «الخدمة» في شبكة قالب مبيعات B2B حسب الخدمة.
  const { data: activeServices } = useActiveServices();
  const serviceNames = useMemo(() => (activeServices ?? []).map((s) => s.nameAr), [activeServices]);
  const toast = useToast();
  // حالة خطأ سطريّة مقصورة على «الحذف الإداريّ» (ADMIN-GOVERNANCE-R1، خارج نطاق Approval UX R1) — تطابق سلوك الأصل.
  const [err, setErr] = useState<string | null>(null);
  const [draft, setDraft] = useState<Record<string, FieldValueInput>>({});
  const [comment, setComment] = useState('');

  // يُعيد Promise حتى ننتظر تحديث الكاش قبل Toast/الرجوع (لا رجوع قبل تحديث البيانات).
  const invalidateAll = () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ['submission', id] }),
      qc.invalidateQueries({ queryKey: ['submissions-mine'] }),
      qc.invalidateQueries({ queryKey: ['submissions-pending'] }),
    ]);

  // APPROVAL ACTION UX R1: أخطاء الطلبات (mutations) عبر Toast فقط؛ التحقّق السطريّ يبقى Alert.
  // الحفظ/الإرسال يبقيان في الصفحة؛ الـToast يُطلَق في مُعالِج الزر بعد اكتمال mutateAsync (أي بعد إبطال الكاش).
  const save = useMutation({
    mutationFn: (fields: SubmissionFieldValueDto[]) =>
      api.put(`/submissions/${id}/values`, {
        values: fields.map((f) => draft[f.templateFieldId] ?? toInput(f)),
      }),
    onSuccess: () => invalidateAll(),
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  const submit = useMutation({
    mutationFn: () => api.post(`/submissions/${id}/submit`),
    onSuccess: () => invalidateAll(),
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  const deleteDraft = useMutation({
    mutationFn: () => api.delete(`/submissions/${id}`),
    // قرار نهائيّ: تحديث الكاش ⇒ Toast ⇒ رجوع تلقائيّ بعد ~700ms.
    onSuccess: async () => { await invalidateAll(); toast.success('✅ تم حذف المسودة'); setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS); },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // قرار نهائيّ (اعتماد/إعادة/تصعيد): تحديث القوائم أولًا ⟵ Toast نجاح ⟵ رجوع تلقائيّ للقائمة بعد ~700ms.
  const action = useMutation({
    mutationFn: (kind: 'approve' | 'return' | 'escalate') =>
      api.post(`/submissions/${id}/${kind}`, { comment: comment || null }),
    onSuccess: async (_data, kind) => {
      const msg =
        kind === 'approve' ? '✅ تم اعتماد التقرير بنجاح'
        : kind === 'return' ? '✅ تم إرجاع التقرير للتعديل'
        : '✅ تم تصعيد التقرير';
      setComment('');
      await invalidateAll();
      toast.success(msg);
      setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
    },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // حذف إداريّ ناعم للتقرير (ADMIN-GOVERNANCE-R1): Admin/CEO/GM فقط، سبب إلزاميّ + تدقيق.
  const adminDelete = useMutation({
    mutationFn: (reason: string) => api.post(`/submissions/${id}/admin-delete`, { reason }),
    onSuccess: () => { invalidateAll(); onBack(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل التقرير…" />;
  if (isError || !sub)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل التقرير" description="حدث خطأ أثناء جلب تفاصيل التقرير. أعد المحاولة." />;

  const pendingApprovalStatuses: SubmissionStatus[] = [
    'Submitted',
    'ApprovedByDirectManager',
    'ApprovedByNextLevel',
    'Escalated',
  ];
  const isApprover =
    sub.currentApproverId != null &&
    sub.currentApproverId === user?.userId &&
    pendingApprovalStatuses.includes(sub.status);

  // نسبة اكتمال الحقول المطلوبة (لمنع الإرسال قبل الاكتمال + إبراز الناقص).
  const requiredFields = sub.fieldValues.filter((f) => f.isRequired && f.fieldType !== 'SectionHeader');
  const isFilled = (f: SubmissionFieldValueDto) => fieldHasValue(draft[f.templateFieldId] ?? toInput(f));
  const filledCount = requiredFields.filter(isFilled).length;
  const missingCount = requiredFields.length - filledCount;
  const completion = requiredFields.length ? Math.round((filledCount / requiredFields.length) * 100) : 100;

  // اسم المعتمِد الحالي — لرسالة «تم إرسال تقريرك إلى …».
  const currentApproverName =
    sub.approvalSteps.find((a) => a.approverId === sub.currentApproverId)?.approverName ?? null;
  const inFlight = pendingApprovalStatuses.includes(sub.status);

  // تصنيف الحقول للعرض Project-first: جسم المشاريع (PRS) + Scorecard رقمي ثانوي + نظرة عامة.
  // القوالب v4 خارج المشاريع = ملخص الأسبوع + التحديات فقط (+ مؤشرات KPI رقمية في Scorecard منفصل).
  const NUMERIC_TYPES = ['Number', 'Currency', 'Percentage'];
  const prsFields = sub.fieldValues.filter((f) => f.fieldType === 'ProjectRepeatableSection');
  const hasPRS = prsFields.length > 0;
  const scorecardFields = sub.fieldValues.filter((f) => NUMERIC_TYPES.includes(f.fieldType));
  const overviewFields = sub.fieldValues.filter(
    (f) =>
      f.fieldType !== 'ProjectRepeatableSection' &&
      f.fieldType !== 'SectionHeader' &&
      !NUMERIC_TYPES.includes(f.fieldType),
  );

  // عرض قسم المشاريع المتكرر (جسم التقرير) — تحرير أو قراءة.
  const renderPRS = (f: SubmissionFieldValueDto) => {
    const rcfg = parseRepeatableConfig(f.configJson);
    const cur = draft[f.templateFieldId] ?? toInput(f);
    const entries = parseRepeatableEntries(cur.valueJson);
    const update = (patch: Partial<FieldValueInput>) =>
      setDraft((prev) => ({ ...prev, [f.templateFieldId]: { ...cur, ...patch } }));
    if (!sub.canEdit) {
      return <ProjectRepeatableDisplay key={f.templateFieldId} config={rcfg} entries={entries} projects={allProjects ?? []} />;
    }
    return (
      <div key={f.templateFieldId}>
        {f.helpText && <p className="mb-2 text-xs text-ink-2">{f.helpText}</p>}
        <ProjectRepeatableEditor
          config={rcfg}
          entries={entries}
          projects={selectableProjects ?? []}
          allProjects={allProjects ?? []}
          onChange={(next) => update({ valueJson: JSON.stringify(next) })}
        />
      </div>
    );
  };

  // عرض حقل مفرد (نظرة عامة / Scorecard / التدفّق القديم) — تحرير أو قراءة.
  const renderField = (f: SubmissionFieldValueDto) => {
    const kind = fieldInputKind(f.fieldType);
    const cfg = parseConfig(f.configJson);
    const cur = draft[f.templateFieldId] ?? toInput(f);
    const update = (patch: Partial<FieldValueInput>) =>
      setDraft((prev) => ({ ...prev, [f.templateFieldId]: { ...cur, ...patch } }));

    // عنوان قسم — يُعرض كعنوان وليس كحقل إدخال (يُستخدم في التدفّق القديم فقط).
    if (kind === 'section') {
      return (
        <h3 key={f.templateFieldId} className="mt-4 border-b border-line pb-1 text-base font-bold text-navy">
          {f.label}
        </h3>
      );
    }

    const missing = f.isRequired && sub.canEdit && !fieldHasValue(cur);
    const label = `${f.label}${f.isRequired ? ' *' : ''}${missing ? ' — مطلوب' : ''}`;

    if (!sub.canEdit) {
      return (
        <Field key={f.templateFieldId} label={label} help={f.helpText ?? undefined}>
          {kind === 'grid' ? (
            <GridDisplay columns={cfg.columns ?? []} rows={parseGrid(f.valueJson)} />
          ) : (
            <p className="rounded-lg border border-line bg-offwhite px-3 py-2 text-sm whitespace-pre-wrap">
              {displayValue(f)}
            </p>
          )}
        </Field>
      );
    }

    return (
      <Field key={f.templateFieldId} label={label} help={f.helpText ?? undefined}>
        {kind === 'bool' ? (
          <Select
            value={cur.valueBool == null ? '' : cur.valueBool ? 'true' : 'false'}
            onChange={(e) => update({ valueBool: e.target.value === '' ? null : e.target.value === 'true' })}
          >
            <option value="">—</option>
            <option value="true">نعم</option>
            <option value="false">لا</option>
          </Select>
        ) : kind === 'select' ? (
          <Select value={cur.valueText ?? ''} onChange={(e) => update({ valueText: e.target.value || null })}>
            <option value="">—</option>
            {(cfg.options ?? []).map((opt) => (
              <option key={opt} value={opt}>{opt}</option>
            ))}
          </Select>
        ) : kind === 'multiselect' ? (
          <MultiSelectInput
            options={cfg.options ?? []}
            value={cur.valueText ?? ''}
            onChange={(v) => update({ valueText: v || null })}
          />
        ) : kind === 'grid' ? (
          <GridEditor
            columns={cfg.columns ?? []}
            rows={parseGrid(cur.valueJson)}
            onChange={(rows) => update({ valueJson: JSON.stringify(rows) })}
            // عمود «الدورة» (فهرس 0، B2C) أو «الخدمة» (فهرس 0، B2B) يصبح منتقيًا من الكتالوج المطابق.
            columnOptions={
              cfg.columns?.[0] === 'الدورة' && courseNames.length
                ? { 0: courseNames }
                : cfg.columns?.[0] === 'الخدمة' && serviceNames.length
                  ? { 0: serviceNames }
                  : undefined
            }
          />
        ) : kind === 'longtext' ? (
          <textarea
            className="w-full rounded-lg border border-line px-3 py-2 text-sm focus:border-navy focus:outline-none"
            rows={3}
            value={cur.valueText ?? ''}
            onChange={(e) => update({ valueText: e.target.value })}
          />
        ) : kind === 'number' ? (
          <Input
            type="number"
            value={cur.valueNumber ?? ''}
            onChange={(e) => update({ valueNumber: e.target.value === '' ? null : Number(e.target.value) })}
          />
        ) : kind === 'date' ? (
          <Input
            type="date"
            value={cur.valueDate ? cur.valueDate.slice(0, 10) : ''}
            onChange={(e) => update({ valueDate: e.target.value || null })}
          />
        ) : (
          <Input value={cur.valueText ?? ''} onChange={(e) => update({ valueText: e.target.value })} />
        )}
      </Field>
    );
  };

  // شريط إجراءات الحفظ/الإرسال/الحذف — يظهر مرة واحدة أسفل الحقول.
  const actionBar = sub.canEdit ? (
    <div className="flex flex-wrap items-center gap-2">
      <Button
        loading={save.isPending}
        disabled={save.isPending || submit.isPending}
        onClick={async () => {
          if (save.isPending || submit.isPending) return;
          try {
            await save.mutateAsync(sub.fieldValues);
            toast.success('✅ تم حفظ البيانات بنجاح');
          } catch { /* الخطأ يظهر عبر Toast من onError */ }
        }}
      >
        حفظ
      </Button>
      <Button
        variant="ghost"
        loading={submit.isPending || save.isPending}
        disabled={save.isPending || submit.isPending || missingCount > 0}
        title={missingCount > 0 ? 'أكمل الحقول المطلوبة أولًا' : undefined}
        onClick={async () => {
          if (save.isPending || submit.isPending) return;
          try {
            await save.mutateAsync(sub.fieldValues);
            await submit.mutateAsync();
            toast.success('✅ تم إرسال التقرير للاعتماد');
          } catch { /* الخطأ يظهر عبر Toast من onError */ }
        }}
      >
        إرسال للاعتماد
      </Button>
      {missingCount > 0 && (
        <span className="text-xs text-alert">يتعذّر الإرسال — أكمل {missingCount} حقلًا مطلوبًا.</span>
      )}
      {sub.status === 'Draft' && (
        <Button
          variant="danger"
          loading={deleteDraft.isPending}
          disabled={deleteDraft.isPending}
          onClick={() => {
            if (deleteDraft.isPending) return;
            if (window.confirm('هل تريد حذف هذه المسودة؟ لا يمكن التراجع عن هذا الإجراء.')) deleteDraft.mutate();
          }}
        >
          حذف المسودة
        </Button>
      )}
    </div>
  ) : null;

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{sub.templateTitle}</h1>
        <Badge tone={statusTone[sub.status] ?? 'muted'}>{submissionStatusLabel[sub.status]}</Badge>
      </div>
      <p className="text-ink-2">
        <Link to={`/app/employee/${sub.submitterId}`} className="text-navy hover:text-orange-600 hover:underline">
          {sub.submitterName}
        </Link>
        {' · '}
        {periodTypeLabel[sub.periodType]} {sub.periodKey}
        {sub.submittedAtUtc ? ` · أُرسل ${formatDate(sub.submittedAtUtc)}` : ''}
      </p>
      {err && <Alert tone="alert">{err}</Alert>}

      {/* رسالة تأكيد بعد الإرسال — إلى أين ذهب التقرير. */}
      {inFlight && (
        <Alert tone="success">
          تم إرسال تقريرك{currentApproverName ? ` إلى ${currentApproverName}` : ''} لاعتماده. تابع حالته في المسار أدناه.
        </Alert>
      )}
      {sub.status === 'Returned' && (
        <Alert tone="alert">أُعيد تقريرك للتعديل — راجع ملاحظة المعتمِد في المسار أدناه، ثم عدّل وأعد الإرسال.</Alert>
      )}

      {/* مسار الاعتماد البصري — يظهر دائمًا. */}
      <Card>
        <h2 className="mb-3 font-semibold text-navy">مسار الاعتماد</h2>
        <ApprovalPath steps={buildPath(sub)} />
      </Card>

      {/* شريط اكتمال الحقول المطلوبة (أثناء التحرير فقط). */}
      {sub.canEdit && requiredFields.length > 0 && (
        <Card>
          <div className="mb-2 flex items-center justify-between text-sm">
            <span className="font-semibold text-navy">اكتمال التقرير</span>
            <span className={missingCount > 0 ? 'text-alert' : 'text-success'}>
              {completion}٪{missingCount > 0 ? ` · متبقٍ ${missingCount} حقل مطلوب` : ' · مكتمل'}
            </span>
          </div>
          <ProgressBar value={completion} tone={missingCount > 0 ? 'orange' : 'success'} />
        </Card>
      )}

      {!hasPRS ? (
        // التوافق الخلفي: القوالب/التقارير بلا قسم مشاريع تُعرض بالتدفّق المسطّح القديم.
        <Card>
          <h2 className="mb-3 font-semibold text-navy">الحقول</h2>
          <div className="space-y-3">
            {sub.fieldValues.map((f) =>
              f.fieldType === 'ProjectRepeatableSection' ? renderPRS(f) : renderField(f),
            )}
          </div>
          {actionBar && <div className="mt-4">{actionBar}</div>}
        </Card>
      ) : (
        // العرض Project-first: (1) نظرة عامة (2) Scorecard رقمي مطويّ ثانوي (3) جسم المشاريع.
        <>
          {overviewFields.length > 0 && (
            <Card>
              <h2 className="mb-3 font-semibold text-navy">نظرة عامة</h2>
              <div className="space-y-3">{overviewFields.map((f) => renderField(f))}</div>
            </Card>
          )}

          {scorecardFields.length > 0 && (
            <Card>
              <details>
                <summary className="cursor-pointer select-none font-semibold text-navy">
                  🔢 مؤشرات الأداء (KPI){' '}
                  <span className="text-xs font-normal text-ink-2">— اضغط للعرض/الطي</span>
                </summary>
                <p className="mt-2 mb-3 text-xs text-ink-2">
                  مؤشرات رقمية للتجميع والاحتساب — ليست جزءًا من جسم التقرير.
                </p>
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {scorecardFields.map((f) => renderField(f))}
                </div>
              </details>
            </Card>
          )}

          <Card>
            <h2 className="mb-1 font-semibold text-navy">📁 تفاصيل المشاريع / العملاء</h2>
            <p className="mb-3 text-xs text-ink-2">
              جسم التقرير — كل مشروع/عميل في بطاقة مستقلّة تحوي كل التفاصيل والجداول.
            </p>
            <div className="space-y-4">{prsFields.map((f) => renderPRS(f))}</div>
            {actionBar && <div className="mt-4 border-t border-line pt-4">{actionBar}</div>}
          </Card>
        </>
      )}

      {/* ملاحظات المعتمِدين — تُعرض فقط عند وجود تعليق على أحد المستويات. */}
      {sub.approvalSteps.some((a) => a.comment) && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">ملاحظات الاعتماد</h2>
          <ul className="space-y-2 text-sm">
            {sub.approvalSteps.filter((a) => a.comment).map((a) => (
              <li key={a.level} className="border-b border-line pb-2 last:border-0">
                <p className="font-medium text-navy">{a.approverName ?? '—'} · {approvalStatusLabel[a.status]}</p>
                <p className="text-ink-2">{a.comment}</p>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {isApprover && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">إجراء الاعتماد</h2>
          <div className="mb-3">
            <Field label="ملاحظة / سبب" help="مطلوب عند الإعادة للتعديل أو التصعيد">
              <Input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اكتب سبب القرار…" />
            </Field>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              loading={action.isPending}
              disabled={action.isPending}
              onClick={() => { if (action.isPending) return; action.mutate('approve'); }}
            >
              اعتماد
            </Button>
            <Button
              variant="ghost"
              loading={action.isPending}
              disabled={action.isPending || !comment.trim()}
              title={!comment.trim() ? 'اكتب سبب الإعادة أولًا' : undefined}
              onClick={() => { if (action.isPending) return; action.mutate('return'); }}
            >
              إعادة للتعديل
            </Button>
            <Button
              variant="danger"
              loading={action.isPending}
              disabled={action.isPending || !comment.trim()}
              title={!comment.trim() ? 'اكتب سبب التصعيد أولًا' : undefined}
              onClick={() => { if (action.isPending) return; action.mutate('escalate'); }}
            >
              تصعيد
            </Button>
          </div>
          {!comment.trim() && (
            <p className="mt-2 text-xs text-ink-2">الاعتماد لا يتطلّب سببًا، لكن الإعادة والتصعيد يتطلّبان كتابة السبب.</p>
          )}
        </Card>
      )}

      {/* الحذف الإداريّ الناعم (ADMIN-GOVERNANCE-R1): Admin/CEO/GM فقط، سبب إلزاميّ. */}
      {canAdminDelete && (
        <Card>
          <h2 className="mb-2 font-semibold text-navy">حذف إداريّ</h2>
          <p className="mb-3 text-sm text-ink-2">
            حذف ناعم للتقرير مع حفظ السبب في سجلّ التدقيق. يُلغي خطوات الاعتماد المعلّقة ويُخفي التقرير من التجميعات.
          </p>
          <Button
            variant="danger"
            disabled={adminDelete.isPending}
            onClick={() => {
              setErr(null);
              const reason = window.prompt('سبب الحذف الإداريّ (إلزاميّ):')?.trim();
              if (!reason) return;
              adminDelete.mutate(reason);
            }}
          >
            حذف إداريّ للتقرير
          </Button>
        </Card>
      )}

      {/* الملاحظات الإدارية المرتبطة بهذا التقرير (طبقة سياقية). */}
      <ManagementNotesPanel
        entityType="ReportSubmission"
        entityId={id}
        title="الملاحظات الإدارية على هذا التقرير"
      />
    </div>
  );
}

// هل للحقل قيمة فعلية؟ (لحساب الاكتمال وإبراز الحقول الناقصة).
function fieldHasValue(v: FieldValueInput): boolean {
  if (v.valueText != null && v.valueText.trim() !== '') return true;
  if (v.valueNumber != null) return true;
  if (v.valueDate != null && v.valueDate !== '') return true;
  if (v.valueBool != null) return true;
  if (v.valueJson != null && v.valueJson !== '' && v.valueJson !== '[]') return true;
  return false;
}

// بناء مسار الاعتماد البصري من حالة التقرير وخطوات الاعتماد.
function buildPath(sub: SubmissionDto): PathStep[] {
  const stepState = (a: ApprovalStepDto): PathStep['state'] => {
    if (a.status === 'Approved') return 'done';
    if (a.status === 'Returned') return 'returned';
    if (a.status === 'Escalated') return 'current';
    return a.approverId === sub.currentApproverId ? 'current' : 'todo';
  };
  const submitter: PathStep = {
    label: 'المُرسِل',
    state: sub.status === 'Draft' ? 'current' : sub.status === 'Returned' ? 'returned' : 'done',
  };
  const steps: PathStep[] = sub.approvalSteps.map((a) => ({
    label: a.approverName ?? `المستوى ${a.level}`,
    state: stepState(a),
  }));
  // B2C-UAT-FIXPACK — الجزء 3: عند إغلاق التقرير نُضيف عقدة ختامية صريحة «تم الاعتماد».
  // المسار يُبنى ديناميكيًّا من approvalSteps فقط، فبعد إيقاف الصعود عند قائد الفريق (الخادم لا يُنشئ
  // خطوة للمدير) يظهر المسار مبسّطًا: المُرسِل ← قائد الفريق ← تم الاعتماد، بلا مدير/مدير عام/رئيس تنفيذي.
  const path = [submitter, ...steps];
  if (sub.status === 'Closed') path.push({ label: 'تم الاعتماد', state: 'done' });
  return path;
}

function toInput(f: SubmissionFieldValueDto): FieldValueInput {
  return {
    templateFieldId: f.templateFieldId,
    valueText: f.valueText,
    valueNumber: f.valueNumber,
    valueDate: f.valueDate,
    valueBool: f.valueBool,
    valueJson: f.valueJson,
  };
}

function displayValue(f: SubmissionFieldValueDto): string {
  if (f.valueBool != null) return f.valueBool ? 'نعم' : 'لا';
  if (f.valueNumber != null) return String(f.valueNumber);
  if (f.valueDate) return formatDate(f.valueDate);
  if (f.valueText) return f.valueText;
  return '—';
}

// ===== إدخال متعدد الاختيار: يُخزَّن نصًا مفصولًا بفواصل =====
function MultiSelectInput({
  options,
  value,
  onChange,
}: {
  options: string[];
  value: string;
  onChange: (v: string) => void;
}) {
  const selected = value ? value.split('،').map((s) => s.trim()).filter(Boolean) : [];
  const toggle = (opt: string) => {
    const next = selected.includes(opt) ? selected.filter((s) => s !== opt) : [...selected, opt];
    onChange(next.join('، '));
  };
  return (
    <div className="flex flex-wrap gap-2">
      {options.map((opt) => (
        <button
          key={opt}
          type="button"
          onClick={() => toggle(opt)}
          className={`rounded-full border px-3 py-1 text-sm ${
            selected.includes(opt) ? 'border-navy bg-navy text-white' : 'border-line text-ink-2'
          }`}
        >
          {opt}
        </button>
      ))}
    </div>
  );
}

// ===== محرّر شبكة الجدول =====
export function GridEditor({
  columns,
  rows,
  onChange,
  columnOptions,
}: {
  columns: string[];
  rows: string[][];
  onChange: (rows: string[][]) => void;
  // خيارات منسدلة اختيارية لكل عمود (المفتاح=فهرس العمود). لأعمدة الكتالوج مثل «الدورة».
  columnOptions?: Record<number, string[]>;
}) {
  const cols = columns.length ? columns : ['القيمة'];
  const setCell = (r: number, c: number, v: string) => {
    const next = rows.map((row) => [...row]);
    next[r][c] = v;
    onChange(next);
  };
  const addRow = () => onChange([...rows, cols.map(() => '')]);
  const removeRow = (r: number) => onChange(rows.filter((_, i) => i !== r));
  return (
    <div className="overflow-x-auto rounded-lg border border-line">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-offwhite text-right text-ink-2">
            {cols.map((c) => (
              <th key={c} className="px-2 py-1.5 font-medium">{c}</th>
            ))}
            <th className="w-10 px-2 py-1.5"></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, r) => (
            <tr key={r} className="border-t border-line">
              {cols.map((_, c) => {
                const opts = columnOptions?.[c];
                if (opts) {
                  const cell = row[c] ?? '';
                  // نحفظ أيّ قيمة قديمة خارج الكتالوج كخيار إضافي كي لا يُمحى تعديل التقارير القديمة.
                  const legacy = cell && !opts.includes(cell) ? [cell] : [];
                  return (
                    <td key={c} className="px-1 py-1">
                      <Select value={cell} onChange={(e) => setCell(r, c, e.target.value)}>
                        <option value="">—</option>
                        {legacy.map((v) => (
                          <option key={v} value={v}>{v} (قيمة قديمة)</option>
                        ))}
                        {opts.map((v) => (
                          <option key={v} value={v}>{v}</option>
                        ))}
                      </Select>
                    </td>
                  );
                }
                // عمود رقمي ⇒ تنقية صارمة (خانات + فاصلة عشرية + سالب) مع تحويل الخانات العربية أثناء الكتابة؛
                // عمود نصّي حرّ ⇒ تطبيع الخانات فقط (لا يمسّ الحروف) كي لا تُخزَّن خانة عربية إطلاقًا.
                const isNumeric = isNumericGridColumn(cols[c]);
                return (
                  <td key={c} className="px-1 py-1">
                    <input
                      className="w-full rounded border border-transparent px-2 py-1 focus:border-navy focus:outline-none"
                      inputMode={isNumeric ? 'decimal' : undefined}
                      value={row[c] ?? ''}
                      onChange={(e) =>
                        setCell(r, c, isNumeric ? sanitizeNumericInput(e.target.value) : normalizeDigits(e.target.value))
                      }
                    />
                  </td>
                );
              })}
              <td className="px-1 py-1 text-center">
                <button type="button" onClick={() => removeRow(r)} className="text-alert hover:underline">حذف</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" onClick={addRow} className="w-full border-t border-line py-1.5 text-sm font-medium text-navy hover:bg-offwhite">
        + إضافة صف
      </button>
    </div>
  );
}

// ===== قسم المشاريع المتكرر: محرّر التعبئة =====
// «نشط» في هذا السياق = مشروع غير مغلق وغير مكتمل (مرتبط بعمل جارٍ). النطاق مفروض خادميًّا أصلًا.
export function ProjectRepeatableEditor({
  config, entries, projects, allProjects, onChange,
}: {
  config: ProjectRepeatableConfig;
  entries: ProjectRepeatableEntry[];
  projects: ProjectDto[];      // المشاريع القابلة للاختيار فقط (مفلترة خادميًّا).
  allProjects: ProjectDto[];   // كل المشاريع ضمن النطاق — لحلّ القيمة المختارة سابقًا فقط.
  onChange: (entries: ProjectRepeatableEntry[]) => void;
}) {
  const selectable = projects;
  const atMax = config.maxProjects > 0 && entries.length >= config.maxProjects;

  const addEntry = () => onChange([...entries, { projectId: null, answers: {} }]);
  const removeEntry = (i: number) => onChange(entries.filter((_, idx) => idx !== i));
  const setProject = (i: number, projectId: string | null) =>
    onChange(entries.map((e, idx) => (idx === i ? { ...e, projectId } : e)));
  const setAnswer = (i: number, key: string, value: string) =>
    onChange(entries.map((e, idx) => (idx === i ? { ...e, answers: { ...e.answers, [key]: value } } : e)));

  // قائمة الخيارات: المشاريع القابلة للاختيار + المشروع المختار حاليًا إن لم يكن ضمنها (كي لا تنكسر القيم القديمة).
  const optionsFor = (selected: string | null): ProjectDto[] => {
    if (selected && !selectable.some((p) => p.id === selected)) {
      const extra = allProjects.find((p) => p.id === selected);
      return extra ? [extra, ...selectable] : selectable;
    }
    return selectable;
  };

  return (
    <div className="space-y-3 rounded-lg border border-dashed border-navy/30 bg-navy/[0.02] p-3">
      <p className="text-xs text-ink-2">
        أضِف مشروعًا واحدًا أو أكثر (حد {config.minProjects}–{config.maxProjects > 0 ? config.maxProjects : '∞'}). تظهر مشاريعك ضمن نطاقك فقط.
      </p>
      {entries.length === 0 && <p className="text-xs text-ink-3">لا توجد مشاريع مضافة بعد.</p>}

      {entries.map((entry, i) => (
        <div key={i} className="rounded-lg border border-line bg-white p-3">
          <div className="mb-2 flex items-end justify-between gap-2">
            <div className="w-72">
              <Field label={`المشروع${config.projectRequired ? ' *' : ''}`}>
                <Select value={entry.projectId ?? ''} onChange={(e) => setProject(i, e.target.value || null)}>
                  <option value="">اختر مشروعًا…</option>
                  {optionsFor(entry.projectId).map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}{p.clientName ? ` — ${p.clientName}` : ''}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            <Button variant="danger" onClick={() => removeEntry(i)}>حذف المشروع</Button>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            {config.fields.map((sf) => {
              const val = entry.answers[sf.key] ?? '';
              const label = `${sf.label}${sf.required ? ' *' : ''}`;
              // جدول صفوف داخل المشروع: يمتدّ على عرض كامل، صفوفه محفوظة كنصّ JSON في answers[key].
              if (sf.type === 'Grid') {
                return (
                  <div key={sf.key} className="md:col-span-2">
                    <p className="mb-1 text-sm font-medium text-ink">{label}</p>
                    <GridEditor
                      columns={sf.columns ?? []}
                      rows={parseGrid(val)}
                      onChange={(rows) => setAnswer(i, sf.key, JSON.stringify(rows))}
                    />
                  </div>
                );
              }
              const k = subFieldInputKind(sf.type);
              return (
                <Field key={sf.key} label={label}>
                  {k === 'bool' ? (
                    <Select value={val} onChange={(e) => setAnswer(i, sf.key, e.target.value)}>
                      <option value="">—</option>
                      <option value="true">نعم</option>
                      <option value="false">لا</option>
                    </Select>
                  ) : k === 'select' ? (
                    <Select value={val} onChange={(e) => setAnswer(i, sf.key, e.target.value)}>
                      <option value="">اختر…</option>
                      {(sf.options ?? []).map((o) => (
                        <option key={o} value={o}>{o}</option>
                      ))}
                    </Select>
                  ) : k === 'longtext' ? (
                    <textarea
                      className="w-full rounded-lg border border-line px-3 py-2 text-sm focus:border-navy focus:outline-none"
                      rows={2}
                      value={val}
                      onChange={(e) => setAnswer(i, sf.key, e.target.value)}
                    />
                  ) : k === 'number' ? (
                    <Input type="number" value={val} onChange={(e) => setAnswer(i, sf.key, e.target.value)} />
                  ) : k === 'date' ? (
                    <Input type="date" value={val ? val.slice(0, 10) : ''} onChange={(e) => setAnswer(i, sf.key, e.target.value)} />
                  ) : (
                    <Input value={val} onChange={(e) => setAnswer(i, sf.key, e.target.value)} />
                  )}
                </Field>
              );
            })}
          </div>
        </div>
      ))}

      <Button variant="ghost" onClick={addEntry} disabled={atMax}
        title={atMax ? `الحد الأقصى ${config.maxProjects} مشروعًا` : undefined}>
        + إضافة مشروع
      </Button>
    </div>
  );
}

// ===== قسم المشاريع المتكرر: عرض للقراءة فقط مجمّع حسب المشروع =====
export function ProjectRepeatableDisplay({
  config, entries, projects,
}: {
  config: ProjectRepeatableConfig;
  entries: ProjectRepeatableEntry[];
  projects: ProjectDto[];
}) {
  if (entries.length === 0)
    return <p className="rounded-lg border border-line bg-offwhite px-3 py-2 text-sm">—</p>;

  const projectName = (pid: string | null) => {
    if (!pid) return 'بدون مشروع محدّد';
    const p = projects.find((x) => x.id === pid);
    return p ? `${p.name}${p.clientName ? ` — ${p.clientName}` : ''}` : 'مشروع غير معروف';
  };
  const showAnswer = (sf: RepeatableSubField, raw: string | undefined): string => {
    if (raw == null || raw === '') return '—';
    if (sf.type === 'Boolean') return raw === 'true' ? 'نعم' : 'لا';
    return raw;
  };

  return (
    <div className="space-y-3">
      {entries.map((entry, i) => (
        <div key={i} className="rounded-lg border border-line bg-white p-3">
          <p className="mb-2 font-semibold text-navy">{projectName(entry.projectId)}</p>
          <dl className="grid gap-x-6 gap-y-1.5 text-sm md:grid-cols-2">
            {config.fields.filter((sf) => sf.type !== 'Grid').map((sf) => (
              <div key={sf.key} className="flex justify-between gap-3 border-b border-line/60 pb-1">
                <dt className="text-ink-2">{sf.label}</dt>
                <dd className="font-medium text-ink whitespace-pre-wrap">{showAnswer(sf, entry.answers[sf.key])}</dd>
              </div>
            ))}
          </dl>
          {config.fields.filter((sf) => sf.type === 'Grid').map((sf) => (
            <div key={sf.key} className="mt-3">
              <p className="mb-1 text-sm font-medium text-ink-2">{sf.label}</p>
              <GridDisplay columns={sf.columns ?? []} rows={parseGrid(entry.answers[sf.key])} />
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

export function GridDisplay({ columns, rows }: { columns: string[]; rows: string[][] }) {
  const cols = columns.length ? columns : ['القيمة'];
  if (!rows.length) return <p className="rounded-lg border border-line bg-offwhite px-3 py-2 text-sm">—</p>;
  return (
    <div className="overflow-x-auto rounded-lg border border-line">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-offwhite text-right text-ink-2">
            {cols.map((c) => (
              <th key={c} className="px-2 py-1.5 font-medium">{c}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, r) => (
            <tr key={r} className="border-t border-line">
              {cols.map((_, c) => (
                <td key={c} className="px-2 py-1.5">{row[c] ?? ''}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

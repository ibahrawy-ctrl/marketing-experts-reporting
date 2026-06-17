import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api, apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { UserPicker } from '../components/UserPicker';
import { MetricTile, ProgressBar, SectionTitle } from '../components/dashboard';
import { useAllSubmissions, useKpiSummary } from '../lib/useOrg';
import { useDirectoryUsers } from '../lib/useDirectory';
import {
  riskSeverityLabel,
  riskStatusLabel,
  escalationStatusLabel,
  decisionStatusLabel,
  formatDate,
} from '../lib/format';
import type {
  RiskDto,
  EscalationDto,
  DecisionDto,
  RiskSeverity,
  RiskStatus,
  EscalationStatus,
  DecisionStatus,
  GovernanceSummaryReport,
  SubmissionStatus,
} from '../types/api';

type Tab = 'overview' | 'risks' | 'escalations' | 'decisions';

const PENDING_STATES: SubmissionStatus[] = ['Submitted', 'ApprovedByDirectManager', 'ApprovedByNextLevel', 'Escalated'];
const ATTENTION_STATES: SubmissionStatus[] = ['Draft', 'Returned'];

function GovernanceOverview() {
  const { data: summary } = useQuery({
    queryKey: ['governance-summary'],
    queryFn: async () => (await api.get<GovernanceSummaryReport>('/reports/governance-summary')).data,
  });
  const submissions = useAllSubmissions();
  const kpi = useKpiSummary();

  if (submissions.isLoading || kpi.isLoading) return <LoadingState label="يتم تحميل نظرة الحوكمة العامة…" />;
  if (submissions.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          submissions.refetch();
          kpi.refetch();
        }}
        description="حدث خطأ أثناء جلب بيانات الحوكمة. أعد المحاولة."
      />
    );

  const subs = submissions.data ?? [];
  const closed = subs.filter((s) => s.status === 'Closed' || s.status === 'Visible').length;
  const completion = subs.length === 0 ? 100 : Math.round((closed / subs.length) * 100);
  const pending = subs.filter((s) => PENDING_STATES.includes(s.status)).length;
  const attention = subs.filter((s) => ATTENTION_STATES.includes(s.status)).length;
  const belowTarget = kpi.data?.belowTarget ?? 0;
  const notEvaluated = kpi.data?.rows ? Math.max(0, kpi.data.rows.length - kpi.data.evaluated) : 0;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <MetricTile label="نسبة اكتمال التقارير" value={`${completion}٪`} tone={completion >= 80 ? 'success' : completion >= 50 ? 'gold' : 'alert'} hint={`${closed}/${subs.length} مغلق`} />
        <MetricTile label="تقارير تحتاج إجراء" value={attention} tone={attention > 0 ? 'alert' : 'navy'} to="/app/submissions" hint="مسودة/معادة" />
        <MetricTile label="بانتظار الاعتماد" value={pending} tone={pending > 0 ? 'gold' : 'navy'} to="/app/submissions" />
        <MetricTile label="مؤشرات دون المستهدف" value={belowTarget} tone={belowTarget > 0 ? 'alert' : 'navy'} to="/app/kpi" />
        <MetricTile label="تقييمات ناقصة" value={notEvaluated} tone={notEvaluated > 0 ? 'gold' : 'navy'} to="/app/kpi" />
        <MetricTile label="مخاطر مفتوحة" value={summary?.openRisks ?? 0} tone={(summary?.openRisks ?? 0) > 0 ? 'alert' : 'navy'} />
        <MetricTile label="تصعيدات مفتوحة" value={summary?.openEscalations ?? 0} tone={(summary?.openEscalations ?? 0) > 0 ? 'alert' : 'navy'} />
        <MetricTile label="قرارات مفتوحة" value={summary?.openDecisions ?? 0} tone={(summary?.openDecisions ?? 0) > 0 ? 'gold' : 'navy'} />
      </div>

      <Card>
        <SectionTitle title="جودة البيانات والاكتمال" hint="نسبة التقارير المغلقة من إجمالي التقارير في النظام" />
        <ProgressBar value={completion} tone={completion >= 80 ? 'success' : 'orange'} />
        <div className="mt-3 flex flex-wrap gap-2">
          <Link to="/app/submissions"><Button variant="ghost">فتح التقارير</Button></Link>
          <Link to="/app/kpi"><Button variant="ghost">مراجعة المؤشرات</Button></Link>
          <Link to="/app/reports"><Button variant="ghost">تصدير التقارير</Button></Link>
        </div>
      </Card>
    </div>
  );
}

export default function GovernancePage() {
  const [tab, setTab] = useState<Tab>('overview');
  const { hasAnyRole } = useAuth();
  const isManagement = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader');

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-navy">الحوكمة والمتابعة</h1>
      <div className="flex gap-2 border-b border-line">
        {([
          ['overview', 'نظرة عامة'],
          ['risks', 'المخاطر'],
          ['escalations', 'التصعيدات'],
          ['decisions', 'القرارات'],
        ] as [Tab, string][]).map(([k, label]) => (
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
      {tab === 'overview' && <GovernanceOverview />}
      {tab === 'risks' && <RisksTab isManagement={isManagement} />}
      {tab === 'escalations' && <EscalationsTab />}
      {tab === 'decisions' && <DecisionsTab isManagement={isManagement} />}
    </div>
  );
}

const severityTone: Record<RiskSeverity, 'success' | 'gold' | 'orange' | 'alert'> = {
  Low: 'success',
  Medium: 'gold',
  High: 'orange',
  Critical: 'alert',
};

function RisksTab({ isManagement }: { isManagement: boolean }) {
  const qc = useQueryClient();
  const { data: risks, isLoading, isError, refetch } = useQuery({
    queryKey: ['risks'],
    queryFn: async () => (await api.get<RiskDto[]>('/risks')).data,
  });
  const [title, setTitle] = useState('');
  const [severity, setSeverity] = useState<RiskSeverity>('Medium');
  const [nextAction, setNextAction] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post('/risks', {
        title,
        description: null,
        severity,
        ownerId: null,
        departmentId: null,
        mitigationPlan: null,
        nextAction: nextAction.trim() || null,
      }),
    onSuccess: () => {
      setTitle('');
      setNextAction('');
      void qc.invalidateQueries({ queryKey: ['risks'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const close = useMutation({
    mutationFn: (r: RiskDto) =>
      api.put(`/risks/${r.id}`, { title: r.title, description: r.description, severity: r.severity, status: 'Closed' as RiskStatus, mitigationPlan: r.mitigationPlan, nextAction: r.nextAction }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['risks'] }),
  });

  if (isLoading) return <LoadingState label="يتم تحميل المخاطر…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب سجل المخاطر. أعد المحاولة." />;

  return (
    <div className="space-y-4">
      {isManagement && (
        <Card>
          {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-48">
              <Field label="عنوان الخطر">
                <Input value={title} onChange={(e) => setTitle(e.target.value)} />
              </Field>
            </div>
            <div className="w-40">
              <Field label="الخطورة">
                <Select value={severity} onChange={(e) => setSeverity(e.target.value as RiskSeverity)}>
                  {(Object.keys(riskSeverityLabel) as RiskSeverity[]).map((s) => (
                    <option key={s} value={s}>{riskSeverityLabel[s]}</option>
                  ))}
                </Select>
              </Field>
            </div>
            <div className="flex-1 min-w-48">
              <Field label="الإجراء التالي" help="الخطوة المقترحة لمعالجة الخطر">
                <Input value={nextAction} onChange={(e) => setNextAction(e.target.value)} placeholder="مثال: تصعيد للمدير العام" />
              </Field>
            </div>
            <Button disabled={!title || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
              إضافة خطر
            </Button>
          </div>
        </Card>
      )}
      <Card>
        {!risks?.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد مخاطر مسجّلة.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {isManagement
                ? 'سجّل أي مخاطرة تشغيلية أو استراتيجية من النموذج أعلاه لتتبّعها ومتابعة حالتها.'
                : 'تظهر هنا المخاطر بعد تسجيلها من قِبل الإدارة. لا مخاطر مفتوحة ضمن نطاقك حاليًا.'}
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">العنوان</th>
                <th className="pb-2">الخطورة</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2">المالك</th>
                <th className="pb-2">الإجراء التالي</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {risks.map((r) => (
                <tr key={r.id} className="border-t border-line">
                  <td className="py-2">{r.title}</td>
                  <td className="py-2"><Badge tone={severityTone[r.severity]}>{riskSeverityLabel[r.severity]}</Badge></td>
                  <td className="py-2">{riskStatusLabel[r.status]}</td>
                  <td className="py-2">{r.ownerName ?? '—'}</td>
                  <td className="py-2 text-ink-2">{r.nextAction ?? '—'}</td>
                  <td className="py-2 text-left">
                    {isManagement && r.status !== 'Closed' && (
                      <Button variant="ghost" onClick={() => close.mutate(r)}>إغلاق</Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

// تصعيد «نازل»: مُطلِقه أعلى إداريًا من المُستهدَف (وفق سلسلة المديرين). يطابق منطق الخادم.
function isDownwardEscalation(
  raisedById: string,
  targetUserId: string,
  users: { id: string; managerId: string | null }[],
): boolean {
  if (raisedById === targetUserId) return false;
  const managerOf = new Map(users.map((u) => [u.id, u.managerId]));
  let current: string | null = targetUserId;
  let guard = 0;
  while (current && guard++ < 64) {
    const m: string | null = managerOf.get(current) ?? null;
    if (m === raisedById) return true;
    current = m;
  }
  return false;
}

type ResolveArgs = { id: string; status: EscalationStatus; resolution: string | null };

function EscalationRow({
  e,
  meId,
  users,
  onResolve,
  pending,
}: {
  e: EscalationDto;
  meId: string | undefined;
  users: { id: string; managerId: string | null }[];
  onResolve: (args: ResolveArgs) => void;
  pending: boolean;
}) {
  const [mode, setMode] = useState<null | 'Resolved' | 'Dismissed'>(null);
  const [text, setText] = useState('');

  const isTarget = meId === e.targetUserId;
  const isRaiser = meId === e.raisedById;
  const downward = isDownwardEscalation(e.raisedById, e.targetUserId, users);
  const isOpen = e.status === 'Open' || e.status === 'Acknowledged';

  // ما المطلوب الآن (Item 6) — توجيه واضح لكل طرف بحسب اتجاه التصعيد ودوره فيه.
  const guidance = isTarget && isOpen
    ? downward
      ? 'تصعيد وارد من مستوى إداري أعلى: استلمه وعالجه ثم أغلقه، أو اطلب توضيحًا، أو ارفعه لأعلى. لا يمكنك رفض سلطة التصعيد.'
      : 'تصعيد مرفوع إليك للبتّ فيه: يمكنك معالجته وإغلاقه (بتعليق) أو رفضه (بذكر سبب).'
    : isRaiser && isOpen
      ? 'بانتظار معالجة المستهدف لتصعيدك.'
      : null;

  const submit = (status: 'Resolved' | 'Dismissed') => {
    if (!text.trim()) return;
    onResolve({ id: e.id, status, resolution: text.trim() });
    setMode(null);
    setText('');
  };

  return (
    <tr className="border-t border-line align-top">
      <td className="py-2">{e.reason}</td>
      <td className="py-2">{e.raisedByName ?? '—'}</td>
      <td className="py-2">{e.targetName ?? '—'}</td>
      <td className="py-2">
        {escalationStatusLabel[e.status]}
        {guidance && <p className="mt-1 max-w-xs text-xs text-ink-3">{guidance}</p>}
      </td>
      <td className="py-2 text-ink-2">{e.nextAction ?? '—'}</td>
      <td className="py-2 text-left">
        {isTarget && isOpen && mode === null && (
          <div className="flex flex-wrap justify-end gap-2">
            {e.status === 'Open' && (
              <Button variant="ghost" disabled={pending} onClick={() => onResolve({ id: e.id, status: 'Acknowledged', resolution: null })}>
                استلام
              </Button>
            )}
            <Button variant="ghost" disabled={pending} onClick={() => { setMode('Resolved'); setText(''); }}>معالجة وإغلاق</Button>
            {/* الرفض متاح فقط لتصعيد «صاعد» (المستهدف هو السلطة الأعلى). يُخفى في التصعيد النازل. */}
            {!downward && (
              <Button variant="ghost" disabled={pending} onClick={() => { setMode('Dismissed'); setText(''); }}>رفض</Button>
            )}
          </div>
        )}
        {isTarget && isOpen && mode !== null && (
          <div className="w-64 space-y-2 text-right">
            <Field label={mode === 'Dismissed' ? 'سبب الرفض (إلزامي)' : 'تعليق الإغلاق (إلزامي)'}>
              <Input value={text} onChange={(ev) => setText(ev.target.value)} />
            </Field>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => { setMode(null); setText(''); }}>إلغاء</Button>
              <Button disabled={!text.trim() || pending} onClick={() => submit(mode)}>
                {mode === 'Dismissed' ? 'تأكيد الرفض' : 'تأكيد الإغلاق'}
              </Button>
            </div>
          </div>
        )}
      </td>
    </tr>
  );
}

function EscalationsTab() {
  const qc = useQueryClient();
  const { user } = useAuth();
  const usersQuery = useDirectoryUsers();
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['escalations'],
    queryFn: async () => (await api.get<EscalationDto[]>('/escalations')).data,
  });
  const [targetUserId, setTargetUserId] = useState('');
  const [reason, setReason] = useState('');
  const [nextAction, setNextAction] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post('/escalations', {
        targetUserId,
        reason,
        reportSubmissionId: null,
        riskId: null,
        nextAction: nextAction.trim() || null,
      }),
    onSuccess: () => {
      setReason('');
      setTargetUserId('');
      setNextAction('');
      void qc.invalidateQueries({ queryKey: ['escalations'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const resolve = useMutation({
    mutationFn: ({ id, status, resolution }: ResolveArgs) =>
      api.post(`/escalations/${id}/resolve`, { status, resolution, nextAction: null }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['escalations'] }),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل التصعيدات…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب التصعيدات. أعد المحاولة." />;

  const users = usersQuery.data ?? [];

  return (
    <div className="space-y-4">
      <Card>
        {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-64">
            <Field label="إلى (المستهدف)"><UserPicker value={targetUserId} onChange={setTargetUserId} /></Field>
          </div>
          <div className="flex-1 min-w-48">
            <Field label="السبب"><Input value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
          </div>
          <div className="flex-1 min-w-48">
            <Field label="الإجراء التالي" help="الخطوة المطلوبة من المستهدف">
              <Input value={nextAction} onChange={(e) => setNextAction(e.target.value)} placeholder="مثال: اتخاذ قرار خلال 48 ساعة" />
            </Field>
          </div>
          <Button disabled={!targetUserId || !reason || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
            رفع تصعيد
          </Button>
        </div>
      </Card>
      <Card>
        {!items?.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد تصعيدات.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              يمكن لأي موظف رفع تصعيد للإدارة عند وجود عائق أو مشكلة تتطلّب تدخّلًا أعلى. أنشئ تصعيدًا من النموذج أعلاه عند الحاجة.
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">السبب</th>
                <th className="pb-2">من</th>
                <th className="pb-2">إلى</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2">الإجراء التالي</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((e) => (
                <EscalationRow
                  key={e.id}
                  e={e}
                  meId={user?.userId}
                  users={users}
                  onResolve={(args) => { setErr(null); resolve.mutate(args); }}
                  pending={resolve.isPending}
                />
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

function DecisionsTab({ isManagement }: { isManagement: boolean }) {
  const qc = useQueryClient();
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['decisions'],
    queryFn: async () => (await api.get<DecisionDto[]>('/decisions')).data,
  });
  const [title, setTitle] = useState('');
  const [nextAction, setNextAction] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post('/decisions', {
        title,
        description: null,
        relatedSubmissionId: null,
        relatedRiskId: null,
        relatedEscalationId: null,
        nextAction: nextAction.trim() || null,
      }),
    onSuccess: () => { setTitle(''); setNextAction(''); void qc.invalidateQueries({ queryKey: ['decisions'] }); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const setStatus = useMutation({
    mutationFn: ({ d, status }: { d: DecisionDto; status: DecisionStatus }) =>
      api.put(`/decisions/${d.id}`, { title: d.title, description: d.description, status, nextAction: d.nextAction }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['decisions'] }),
  });

  if (isLoading) return <LoadingState label="يتم تحميل القرارات…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب سجل القرارات. أعد المحاولة." />;

  return (
    <div className="space-y-4">
      {isManagement && (
        <Card>
          {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-48">
              <Field label="عنوان القرار"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
            </div>
            <div className="flex-1 min-w-48">
              <Field label="الإجراء التالي" help="خطوة التنفيذ/المتابعة المترتبة على القرار">
                <Input value={nextAction} onChange={(e) => setNextAction(e.target.value)} placeholder="مثال: متابعة التنفيذ خلال الأسبوع القادم" />
              </Field>
            </div>
            <Button disabled={!title || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
              إضافة قرار
            </Button>
          </div>
        </Card>
      )}
      <Card>
        {!items?.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد قرارات مسجّلة.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {isManagement
                ? 'سجّل القرارات الإدارية المهمّة هنا لتوثيقها ومتابعة حالتها (مقترح/معتمد/مرفوض) من النموذج أعلاه.'
                : 'تظهر هنا القرارات الإدارية بعد تسجيلها من قِبل الإدارة.'}
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">العنوان</th>
                <th className="pb-2">المتخذ</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2">الإجراء التالي</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((d) => (
                <tr key={d.id} className="border-t border-line">
                  <td className="py-2">{d.title}</td>
                  <td className="py-2">{d.madeByName ?? '—'}</td>
                  <td className="py-2">{decisionStatusLabel[d.status]} · {formatDate(d.createdAtUtc)}</td>
                  <td className="py-2 text-ink-2">{d.nextAction ?? '—'}</td>
                  <td className="py-2 text-left">
                    {isManagement && d.status === 'Proposed' && (
                      <div className="flex gap-2">
                        <Button variant="ghost" onClick={() => setStatus.mutate({ d, status: 'Approved' })}>اعتماد</Button>
                        <Button variant="ghost" onClick={() => setStatus.mutate({ d, status: 'Implemented' })}>تنفيذ</Button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useKpiSummary } from '../lib/useOrg';
import { Alert, Badge, Button, Card, Field, Input } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { UserPicker } from '../components/UserPicker';
import {
  trainingNeedStatusLabel,
  improvementPlanStatusLabel,
  kpiTrendLabel,
  formatDate,
  formatPercent,
} from '../lib/format';
import type {
  TrainingNeedDto,
  ImprovementPlanDto,
  TrainingNeedStatus,
  ImprovementPlanStatus,
} from '../types/api';

type Tab = 'training' | 'plans' | 'suggested' | 'completed';

const DONE_TRAINING: TrainingNeedStatus[] = ['Completed', 'Cancelled'];
const DONE_PLAN: ImprovementPlanStatus[] = ['Completed', 'Cancelled'];

export default function DevelopmentPage() {
  const [tab, setTab] = useState<Tab>('training');
  const { hasAnyRole } = useAuth();
  const isManagement = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader');

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-navy">التطوير</h1>
      <div className="flex flex-wrap gap-2 border-b border-line">
        {([
          ['training', 'الاحتياجات التدريبية'],
          ['plans', 'خطط التحسين'],
          ['suggested', 'مقترحات تلقائية'],
          ['completed', 'مكتملة'],
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
      {tab === 'training' && <TrainingTab isManagement={isManagement} />}
      {tab === 'plans' && <PlansTab isManagement={isManagement} />}
      {tab === 'suggested' && <SuggestedTab isManagement={isManagement} />}
      {tab === 'completed' && <CompletedTab />}
    </div>
  );
}

function TrainingTab({ isManagement }: { isManagement: boolean }) {
  const qc = useQueryClient();
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['training-needs'],
    queryFn: async () => (await api.get<TrainingNeedDto[]>('/training-needs')).data,
  });
  const [subjectUserId, setSubjectUserId] = useState('');
  const [title, setTitle] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post('/training-needs', { subjectUserId, title, description: null, source: null, relatedKpiEvaluationId: null }),
    onSuccess: () => {
      setTitle('');
      setSubjectUserId('');
      void qc.invalidateQueries({ queryKey: ['training-needs'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const setStatus = useMutation({
    mutationFn: ({ n, status }: { n: TrainingNeedDto; status: TrainingNeedStatus }) =>
      api.put(`/training-needs/${n.id}`, { title: n.title, description: n.description, status }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['training-needs'] }),
  });

  if (isLoading) return <LoadingState label="يتم تحميل الاحتياجات التدريبية…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الاحتياجات التدريبية. أعد المحاولة." />;

  // التبويب يعرض النشِط فقط — المكتمل/الملغى في تبويب «مكتملة».
  const active = (items ?? []).filter((n) => !DONE_TRAINING.includes(n.status));

  return (
    <div className="space-y-4">
      {isManagement && (
        <Card>
          {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-64">
              <Field label="الموظف"><UserPicker value={subjectUserId} onChange={setSubjectUserId} /></Field>
            </div>
            <div className="flex-1 min-w-48">
              <Field label="عنوان الاحتياج"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
            </div>
            <Button disabled={!subjectUserId || !title || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
              إضافة احتياج
            </Button>
          </div>
        </Card>
      )}
      <Card>
        {!active.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد احتياجات تدريبية مسجّلة.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {isManagement
                ? 'تُسجَّل الاحتياجات التدريبية عادةً عند ظهور فجوة في مهارات أحد أعضاء الفريق أو بعد تقييم أداء دون المستهدف. أضِف احتياجًا من النموذج أعلاه.'
                : 'تظهر هنا احتياجاتك التدريبية بعد رصدها من قِبل مديرك. لا احتياجات مسجّلة حاليًا.'}
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">العنوان</th>
                <th className="pb-2">الموظف</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {active.map((n) => (
                <tr key={n.id} className="border-t border-line">
                  <td className="py-2">{n.title}</td>
                  <td className="py-2">{n.subjectName ?? '—'}</td>
                  <td className="py-2"><Badge tone="navy">{trainingNeedStatusLabel[n.status]}</Badge></td>
                  <td className="py-2 text-left">
                    {isManagement && n.status !== 'Completed' && n.status !== 'Cancelled' && (
                      <div className="flex gap-2">
                        {n.status === 'Open' && (
                          <Button variant="ghost" onClick={() => setStatus.mutate({ n, status: 'Planned' })}>تخطيط</Button>
                        )}
                        <Button variant="ghost" onClick={() => setStatus.mutate({ n, status: 'Completed' })}>إكمال</Button>
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

function PlansTab({ isManagement }: { isManagement: boolean }) {
  const qc = useQueryClient();
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['improvement-plans'],
    queryFn: async () => (await api.get<ImprovementPlanDto[]>('/improvement-plans')).data,
  });
  const [subjectUserId, setSubjectUserId] = useState('');
  const [title, setTitle] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post('/improvement-plans', { subjectUserId, title, description: null, dueDateUtc: null, relatedTrainingNeedId: null }),
    onSuccess: () => {
      setTitle('');
      setSubjectUserId('');
      void qc.invalidateQueries({ queryKey: ['improvement-plans'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const setStatus = useMutation({
    mutationFn: ({ p, status }: { p: ImprovementPlanDto; status: ImprovementPlanStatus }) =>
      api.put(`/improvement-plans/${p.id}`, { title: p.title, description: p.description, status, dueDateUtc: p.dueDateUtc }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['improvement-plans'] }),
  });

  if (isLoading) return <LoadingState label="يتم تحميل خطط التحسين…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب خطط التحسين. أعد المحاولة." />;

  // التبويب يعرض النشِط فقط — المكتمل/الملغى في تبويب «مكتملة».
  const active = (items ?? []).filter((p) => !DONE_PLAN.includes(p.status));

  return (
    <div className="space-y-4">
      {isManagement && (
        <Card>
          {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-64">
              <Field label="الموظف"><UserPicker value={subjectUserId} onChange={setSubjectUserId} /></Field>
            </div>
            <div className="flex-1 min-w-48">
              <Field label="عنوان الخطة"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
            </div>
            <Button disabled={!subjectUserId || !title || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
              إضافة خطة
            </Button>
          </div>
        </Card>
      )}
      <Card>
        {!active.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد خطط تحسين.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              {isManagement
                ? 'أنشئ خطة تحسين لأي عضو يحتاج إلى رفع أدائه، وحدّد لها هدفًا ومدّة ومتابعة. أضِف خطة من النموذج أعلاه.'
                : 'تظهر هنا خطط التحسين الخاصة بك بعد إنشائها من قِبل مديرك.'}
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">العنوان</th>
                <th className="pb-2">الموظف</th>
                <th className="pb-2">الاستحقاق</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {active.map((p) => (
                <tr key={p.id} className="border-t border-line">
                  <td className="py-2">{p.title}</td>
                  <td className="py-2">{p.subjectName ?? '—'}</td>
                  <td className="py-2">{formatDate(p.dueDateUtc)}</td>
                  <td className="py-2"><Badge tone="navy">{improvementPlanStatusLabel[p.status]}</Badge></td>
                  <td className="py-2 text-left">
                    {isManagement && p.status !== 'Completed' && p.status !== 'Cancelled' && (
                      <div className="flex gap-2">
                        {p.status === 'Open' && (
                          <Button variant="ghost" onClick={() => setStatus.mutate({ p, status: 'InProgress' })}>بدء</Button>
                        )}
                        <Button variant="ghost" onClick={() => setStatus.mutate({ p, status: 'Completed' })}>إكمال</Button>
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

// ===== مقترحات تلقائية — مشتقّة من مؤشّرات الأداء، لا تُنشأ إلا بتأكيد المستخدم =====
type Suggestion = {
  subjectUserId: string;
  subjectName: string;
  score: number;
  kind: 'training' | 'plan';
  title: string;
  reason: string;
};

function SuggestedTab({ isManagement }: { isManagement: boolean }) {
  const qc = useQueryClient();
  const kpi = useKpiSummary();
  const needs = useQuery({
    queryKey: ['training-needs'],
    queryFn: async () => (await api.get<TrainingNeedDto[]>('/training-needs')).data,
  });
  const plans = useQuery({
    queryKey: ['improvement-plans'],
    queryFn: async () => (await api.get<ImprovementPlanDto[]>('/improvement-plans')).data,
  });
  const [err, setErr] = useState<string | null>(null);
  const [dismissed, setDismissed] = useState<Set<string>>(new Set());

  const createNeed = useMutation({
    mutationFn: (s: Suggestion) =>
      api.post('/training-needs', { subjectUserId: s.subjectUserId, title: s.title, description: s.reason, source: 'KpiAuto', relatedKpiEvaluationId: null }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['training-needs'] }),
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const createPlan = useMutation({
    mutationFn: (s: Suggestion) =>
      api.post('/improvement-plans', { subjectUserId: s.subjectUserId, title: s.title, description: s.reason, dueDateUtc: null, relatedTrainingNeedId: null }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['improvement-plans'] }),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (kpi.isLoading || needs.isLoading || plans.isLoading) return <LoadingState label="يتم حساب المقترحات…" />;
  if (kpi.isError) return <QueryError onRetry={() => kpi.refetch()} description="حدث خطأ أثناء جلب مؤشّرات الأداء. أعد المحاولة." />;

  // أصحاب احتياج/خطة نشطة بالفعل — لا نقترح تكرارًا.
  const haveNeed = new Set((needs.data ?? []).filter((n) => !DONE_TRAINING.includes(n.status)).map((n) => n.subjectUserId));
  const havePlan = new Set((plans.data ?? []).filter((p) => !DONE_PLAN.includes(p.status)).map((p) => p.subjectUserId));

  const rows = kpi.data?.rows ?? [];
  const suggestions: Suggestion[] = [];
  rows.forEach((r) => {
    if (r.totalScore == null) return;
    const score = r.totalScore;
    // منطق الاقتراح: دون المستهدف (<60) ← خطة تحسين؛ متوسط ضعيف (<70) أو اتجاه هابط ← احتياج تدريبي.
    if (score < 60 && !havePlan.has(r.subjectUserId)) {
      suggestions.push({
        subjectUserId: r.subjectUserId,
        subjectName: r.subjectName,
        score,
        kind: 'plan',
        title: `خطة تحسين أداء — ${r.subjectName}`,
        reason: `المؤشّر ${formatPercent(score)} دون المستهدف (أقل من 60٪).`,
      });
    }
    if ((score < 70 || r.trend === 'Down') && !haveNeed.has(r.subjectUserId)) {
      suggestions.push({
        subjectUserId: r.subjectUserId,
        subjectName: r.subjectName,
        score,
        kind: 'training',
        title: `احتياج تدريبي — ${r.subjectName}`,
        reason: r.trend === 'Down'
          ? `المؤشّر ${formatPercent(score)} والاتجاه ${kpiTrendLabel[r.trend]} مقارنةً بالفترة السابقة.`
          : `المؤشّر ${formatPercent(score)} أقل من 70٪.`,
      });
    }
  });

  const visible = suggestions.filter((s) => !dismissed.has(`${s.kind}-${s.subjectUserId}`));

  return (
    <div className="space-y-4">
      {err && <Alert tone="alert">{err}</Alert>}
      <Card className="bg-offwhite">
        <p className="text-sm font-semibold text-navy">كيف تُحسب المقترحات؟</p>
        <p className="mt-1 text-xs text-ink-2">
          تُشتقّ هذه البنود تلقائيًا من مؤشّرات الأداء: مؤشّر <b>دون المستهدف (أقل من 60٪)</b> يقترح <b>خطة تحسين</b>، ومؤشّر
          <b> أقل من 70٪ أو باتجاه هابط</b> يقترح <b>احتياجًا تدريبيًا</b>. لا يُنشأ أي بند تلقائيًا — يُنشأ فقط عند تأكيدك بالضغط على «إنشاء».
        </p>
      </Card>

      {visible.length === 0 ? (
        <Card>
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-success">لا توجد مقترحات حاليًا.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              لا يوجد موظف ضمن نطاقك دون المستهدف أو باتجاه هابط دون احتياج/خطة قائمة. ستظهر المقترحات تلقائيًا عند رصد فجوة أداء.
            </p>
          </div>
        </Card>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {visible.map((s) => {
            const busy = s.kind === 'plan' ? createPlan.isPending : createNeed.isPending;
            return (
              <Card key={`${s.kind}-${s.subjectUserId}`} className="border-r-4 border-r-orange">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-navy">{s.title}</p>
                    <p className="mt-0.5 text-xs text-ink-2">{s.reason}</p>
                  </div>
                  <Badge tone={s.kind === 'plan' ? 'alert' : 'gold'}>{s.kind === 'plan' ? 'خطة تحسين' : 'احتياج تدريبي'}</Badge>
                </div>
                {isManagement ? (
                  <div className="mt-3 flex gap-2">
                    <Button
                      disabled={busy}
                      onClick={() => {
                        setErr(null);
                        if (s.kind === 'plan') createPlan.mutate(s);
                        else createNeed.mutate(s);
                      }}
                    >
                      إنشاء {s.kind === 'plan' ? 'الخطة' : 'الاحتياج'}
                    </Button>
                    <Button variant="ghost" onClick={() => setDismissed((p) => new Set(p).add(`${s.kind}-${s.subjectUserId}`))}>
                      تجاهل
                    </Button>
                  </div>
                ) : (
                  <p className="mt-3 text-xs text-ink-3">يُنشئ مديرك البند المناسب عند الحاجة.</p>
                )}
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ===== مكتملة — أرشيف الاحتياجات والخطط المنجَزة/الملغاة =====
function CompletedTab() {
  const needs = useQuery({
    queryKey: ['training-needs'],
    queryFn: async () => (await api.get<TrainingNeedDto[]>('/training-needs')).data,
  });
  const plans = useQuery({
    queryKey: ['improvement-plans'],
    queryFn: async () => (await api.get<ImprovementPlanDto[]>('/improvement-plans')).data,
  });

  if (needs.isLoading || plans.isLoading) return <LoadingState label="يتم تحميل المكتمل…" />;

  const doneNeeds = (needs.data ?? []).filter((n) => DONE_TRAINING.includes(n.status));
  const donePlans = (plans.data ?? []).filter((p) => DONE_PLAN.includes(p.status));

  if (doneNeeds.length === 0 && donePlans.length === 0)
    return (
      <Card>
        <div className="py-10 text-center">
          <p className="text-sm font-medium text-ink-2">لا توجد بنود مكتملة بعد.</p>
          <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">تظهر هنا الاحتياجات التدريبية وخطط التحسين بعد إكمالها أو إلغائها.</p>
        </div>
      </Card>
    );

  return (
    <div className="space-y-4">
      <Card>
        <h2 className="mb-3 font-semibold text-navy">احتياجات تدريبية مكتملة ({doneNeeds.length})</h2>
        {doneNeeds.length === 0 ? (
          <p className="text-sm text-ink-2">لا شيء.</p>
        ) : (
          <ul className="space-y-2 text-sm">
            {doneNeeds.map((n) => (
              <li key={n.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                <span><span className="font-medium text-navy">{n.title}</span> · {n.subjectName ?? '—'}</span>
                <Badge tone={n.status === 'Completed' ? 'success' : 'muted'}>{trainingNeedStatusLabel[n.status]}</Badge>
              </li>
            ))}
          </ul>
        )}
      </Card>
      <Card>
        <h2 className="mb-3 font-semibold text-navy">خطط تحسين مكتملة ({donePlans.length})</h2>
        {donePlans.length === 0 ? (
          <p className="text-sm text-ink-2">لا شيء.</p>
        ) : (
          <ul className="space-y-2 text-sm">
            {donePlans.map((p) => (
              <li key={p.id} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                <span><span className="font-medium text-navy">{p.title}</span> · {p.subjectName ?? '—'}</span>
                <Badge tone={p.status === 'Completed' ? 'success' : 'muted'}>{improvementPlanStatusLabel[p.status]}</Badge>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  useGovernanceEscalations,
  useGovernanceEscalation,
  useCreateGovernanceEscalation,
  useUpdateGovernanceEscalation,
  useChangeGovernanceEscalationStatus,
  useAssignGovernanceEscalation,
  useAddGovernanceEscalationComment,
  useReopenGovernanceEscalation,
  useCloseGovernanceEscalation,
  useEscalationTargetDirectory,
  type GovernanceEscalationsFilter,
} from '../lib/useGovernanceEscalations';
import { useDirectoryUsers } from '../lib/useDirectory';
import { useAllSubmissions } from '../lib/useOrg';
import { useAuth } from '../lib/auth';
import { apiErrorMessage } from '../lib/api';
import {
  escalationTypeLabel,
  escalationSeverityLabel,
  escalationSeverityTone,
  governanceEscalationStatusLabel,
  governanceEscalationStatusTone,
  escalationTargetTypeLabel,
  escalationUpdateTypeLabel,
  formatDateTime,
} from '../lib/format';
import type {
  EscalationType,
  EscalationSeverity,
  GovernanceEscalationStatus,
  EscalationTargetType,
  GovernanceEscalationListItemDto,
  GovernanceEscalationDetailDto,
  CreateGovernanceEscalationRequest,
} from '../types/api';

const TYPES: EscalationType[] = [
  'Performance',
  'Delay',
  'Quality',
  'Compliance',
  'Communication',
  'Workflow',
  'ClientImpact',
  'PolicyViolation',
  'Other',
];
const SEVERITIES: EscalationSeverity[] = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES: GovernanceEscalationStatus[] = [
  'Open',
  'UnderReview',
  'Assigned',
  'WaitingForResponse',
  'Resolved',
  'Closed',
  'Reopened',
  'Cancelled',
];
const TARGET_TYPES: EscalationTargetType[] = [
  'User',
  'Department',
  'Team',
  'Report',
  'Workflow',
  'GovernanceItem',
  'Operational',
  'Other',
];

// الحالات «المفتوحة» (غير منتهية) لأغراض البطاقات.
const OPEN_STATUSES: GovernanceEscalationStatus[] = [
  'Open',
  'UnderReview',
  'Assigned',
  'WaitingForResponse',
  'Reopened',
];
const isOpen = (s: GovernanceEscalationStatus) => OPEN_STATUSES.includes(s);

export default function GovernanceEscalationsPage() {
  const [params, setParams] = useSearchParams();
  const openId = params.get('open');

  const [status, setStatus] = useState<GovernanceEscalationStatus | ''>('');
  const [type, setType] = useState<EscalationType | ''>('');
  const [severity, setSeverity] = useState<EscalationSeverity | ''>('');
  const [targetType, setTargetType] = useState<EscalationTargetType | ''>('');
  const [openOnly, setOpenOnly] = useState(false);
  const [mine, setMine] = useState(false);
  const [assignedToMe, setAssignedToMe] = useState(false);
  const [showCreate, setShowCreate] = useState(false);

  const filter: GovernanceEscalationsFilter = {
    status: status || undefined,
    type: type || undefined,
    severity: severity || undefined,
    targetType: targetType || undefined,
    openOnly: openOnly || undefined,
    mine: mine || undefined,
    assignedToMe: assignedToMe || undefined,
  };

  const { data, isLoading, isError, refetch } = useGovernanceEscalations(filter);

  const openDetail = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <GovernanceEscalationDetail id={openId} onBack={back} />;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">التصعيدات</h1>
          <p className="mt-1 max-w-2xl text-sm text-ink-2">
            مساحة لتسجيل ومتابعة التصعيد الفردي (أداء/تأخير/جودة/التزام/تواصل…) الموجَّه إلى موظّف أو
            إدارة أو فريق، مع خطّ زمني للتحديثات والإسناد وتغييرات الحالة. الرؤية والإجراءات مقيّدة حسب
            دورك ونطاقك.
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>+ تصعيد جديد</Button>
      </div>

      <SummaryCards
        onOpenMine={() => { setMine(true); setAssignedToMe(false); setOpenOnly(false); }}
        onOpenAssigned={() => { setAssignedToMe(true); setMine(false); setOpenOnly(true); }}
        onOpenOpen={() => { setOpenOnly(true); setMine(false); setAssignedToMe(false); }}
      />

      <Card>
        <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-4">
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as GovernanceEscalationStatus | '')}>
              <option value="">كل الحالات</option>
              {STATUSES.map((s) => <option key={s} value={s}>{governanceEscalationStatusLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="النوع">
            <Select value={type} onChange={(e) => setType(e.target.value as EscalationType | '')}>
              <option value="">كل الأنواع</option>
              {TYPES.map((t) => <option key={t} value={t}>{escalationTypeLabel[t]}</option>)}
            </Select>
          </Field>
          <Field label="الخطورة">
            <Select value={severity} onChange={(e) => setSeverity(e.target.value as EscalationSeverity | '')}>
              <option value="">كل المستويات</option>
              {SEVERITIES.map((s) => <option key={s} value={s}>{escalationSeverityLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="نوع الهدف">
            <Select value={targetType} onChange={(e) => setTargetType(e.target.value as EscalationTargetType | '')}>
              <option value="">كل الأهداف</option>
              {TARGET_TYPES.map((t) => <option key={t} value={t}>{escalationTargetTypeLabel[t]}</option>)}
            </Select>
          </Field>
          <Field label="المفتوحة فقط">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />
              إخفاء المُعالَجة/المُغلقة/المُلغاة
            </label>
          </Field>
          <Field label="تصعيداتي">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={mine} onChange={(e) => setMine(e.target.checked)} />
              ما رفعتُه فقط
            </label>
          </Field>
          <Field label="بانتظار قراري">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={assignedToMe} onChange={(e) => setAssignedToMe(e.target.checked)} />
              المُسنَد إليّ فقط
            </label>
          </Field>
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل التصعيدات…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب التصعيدات. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد تصعيدات مطابقة"
                description="عدّل الفلاتر أعلاه أو أنشئ تصعيدًا جديدًا."
              />
            </div>
          ) : (
            <EscalationsTable items={data!} onOpen={openDetail} />
          )}
        </Card>
      )}

      {showCreate && <CreateEscalationModal onClose={() => setShowCreate(false)} onCreated={openDetail} />}
    </div>
  );
}

// ===== بطاقات لوحة موجزة (مختلفة حسب الدور) =====
function SummaryCards({
  onOpenMine,
  onOpenAssigned,
  onOpenOpen,
}: {
  onOpenMine: () => void;
  onOpenAssigned: () => void;
  onOpenOpen: () => void;
}) {
  const { user, hasAnyRole } = useAuth();
  const wide = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'CeoSupport');
  const uid = user?.userId;
  const { data } = useGovernanceEscalations({});
  const all = data ?? [];

  const openCount = all.filter((e) => isOpen(e.status)).length;
  const criticalOpen = all.filter((e) => isOpen(e.status) && e.severity === 'Critical').length;
  const waiting = all.filter((e) => e.status === 'WaitingForResponse').length;
  const assignedToMe = all.filter((e) => isOpen(e.status) && e.assignedToUserId === uid).length;
  const mine = all.filter((e) => e.raisedByUserId === uid).length;
  const directedAtMe = all.filter((e) => e.targetUserId === uid).length;

  const cards = wide
    ? [
        { label: 'تصعيدات مفتوحة', value: openCount, tone: 'navy' as const, onClick: onOpenOpen },
        { label: 'حرجة مفتوحة', value: criticalOpen, tone: 'alert' as const, onClick: onOpenOpen },
        { label: 'بانتظار الرد', value: waiting, tone: 'gold' as const, onClick: onOpenOpen },
        { label: 'مُسنَدة إليّ', value: assignedToMe, tone: 'orange' as const, onClick: onOpenAssigned },
      ]
    : [
        { label: 'تصعيداتي', value: mine, tone: 'navy' as const, onClick: onOpenMine },
        { label: 'موجَّهة إليّ', value: directedAtMe, tone: 'gold' as const, onClick: onOpenOpen },
        { label: 'مُسنَدة إليّ', value: assignedToMe, tone: 'orange' as const, onClick: onOpenAssigned },
      ];

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((c) => (
        <button
          key={c.label}
          onClick={c.onClick}
          className="rounded-2xl border border-line bg-white p-4 text-right transition hover:border-orange"
        >
          <p className="text-sm text-ink-2">{c.label}</p>
          <div className="mt-2 flex items-center justify-between">
            <span className="text-2xl font-bold text-navy">{c.value}</span>
            <Badge tone={c.tone}>{c.label}</Badge>
          </div>
        </button>
      ))}
    </div>
  );
}

function EscalationsTable({
  items,
  onOpen,
}: {
  items: GovernanceEscalationListItemDto[];
  onOpen: (id: string) => void;
}) {
  const target = (g: GovernanceEscalationListItemDto) =>
    g.targetUserName ?? g.targetDepartmentName ?? g.targetTeamName ?? escalationTargetTypeLabel[g.targetType];
  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">العنوان</th>
          <th className="px-3 py-2.5 font-semibold">النوع</th>
          <th className="px-3 py-2.5 font-semibold">الخطورة</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
          <th className="px-3 py-2.5 font-semibold">الهدف</th>
          <th className="px-3 py-2.5 font-semibold">رفعه</th>
          <th className="px-3 py-2.5 font-semibold">المُسنَد إليه</th>
          <th className="px-3 py-2.5 font-semibold"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((g) => (
          <tr
            key={g.id}
            onClick={() => onOpen(g.id)}
            className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
          >
            <td className="px-3 py-2.5 font-medium text-navy">{g.title}</td>
            <td className="px-3 py-2.5 text-ink-2">{escalationTypeLabel[g.escalationType]}</td>
            <td className="px-3 py-2.5">
              <Badge tone={escalationSeverityTone(g.severity)}>{escalationSeverityLabel[g.severity]}</Badge>
            </td>
            <td className="px-3 py-2.5">
              <Badge tone={governanceEscalationStatusTone(g.status)}>{governanceEscalationStatusLabel[g.status]}</Badge>
            </td>
            <td className="px-3 py-2.5 text-ink-2">{target(g)}</td>
            <td className="px-3 py-2.5 text-ink-2">{g.raisedByName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">{g.assignedToName ?? '—'}</td>
            <td className="px-3 py-2.5">
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(g.id); }}>عرض</Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ===== نموذج إنشاء تصعيد =====
function CreateEscalationModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const create = useCreateGovernanceEscalation();
  const [err, setErr] = useState<string | null>(null);
  const [form, setForm] = useState<CreateGovernanceEscalationRequest>({
    title: '',
    description: '',
    escalationType: 'Performance',
    severity: 'Medium',
    targetType: 'User',
  });

  // دليل أهداف التصعيد الآمن (على مستوى الشركة، يستثني الحسابات الحسّاسة) للسماح بالرفع المتقاطع خارج النطاق.
  const directory = useEscalationTargetDirectory();
  const submissions = useAllSubmissions();

  const set = <K extends keyof CreateGovernanceEscalationRequest>(k: K, v: CreateGovernanceEscalationRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  // عند تغيير نوع الهدف نُصفّر معرّفات الهدف غير المناسبة.
  const changeTargetType = (t: EscalationTargetType) =>
    setForm((f) => ({
      ...f,
      targetType: t,
      targetUserId: t === 'User' ? f.targetUserId : undefined,
      targetDepartmentId: t === 'Department' ? f.targetDepartmentId : undefined,
      targetTeamId: t === 'Team' ? f.targetTeamId : undefined,
    }));

  const submit = () => {
    setErr(null);
    if (!form.title.trim()) { setErr('العنوان مطلوب.'); return; }
    if (form.targetType === 'User' && !form.targetUserId) { setErr('اختر الموظّف الهدف.'); return; }
    if (form.targetType === 'Department' && !form.targetDepartmentId) { setErr('اختر الإدارة الهدف.'); return; }
    if (form.targetType === 'Team' && !form.targetTeamId) { setErr('اختر الفريق الهدف.'); return; }
    create.mutate(
      {
        ...form,
        title: form.title.trim(),
        description: form.description?.trim() || undefined,
        targetUserId: form.targetUserId || undefined,
        targetDepartmentId: form.targetDepartmentId || undefined,
        targetTeamId: form.targetTeamId || undefined,
        relatedSubmissionId: form.relatedSubmissionId || undefined,
        relatedGovernanceItemId: form.relatedGovernanceItemId || undefined,
      },
      {
        onSuccess: (d) => { onClose(); onCreated(d.item.id); },
        onError: (e) => setErr(apiErrorMessage(e)),
      },
    );
  };

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-black/40 p-4">
      <div className="my-8 w-full max-w-2xl rounded-2xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold text-navy">تصعيد جديد</h2>
          <button onClick={onClose} className="text-sm text-ink-2 hover:text-navy">إغلاق ✕</button>
        </div>

        {err && <Alert tone="alert">{err}</Alert>}

        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <div className="md:col-span-2">
            <Field label="العنوان">
              <Input value={form.title} onChange={(e) => set('title', e.target.value)} placeholder="عنوان مختصر للتصعيد…" />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field label="الوصف (اختياري)">
              <Input value={form.description ?? ''} onChange={(e) => set('description', e.target.value)} placeholder="تفاصيل التصعيد / السياق…" />
            </Field>
          </div>
          <Field label="النوع">
            <Select value={form.escalationType} onChange={(e) => set('escalationType', e.target.value as EscalationType)}>
              {TYPES.map((t) => <option key={t} value={t}>{escalationTypeLabel[t]}</option>)}
            </Select>
          </Field>
          <Field label="الخطورة">
            <Select value={form.severity} onChange={(e) => set('severity', e.target.value as EscalationSeverity)}>
              {SEVERITIES.map((s) => <option key={s} value={s}>{escalationSeverityLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="نوع الهدف">
            <Select value={form.targetType} onChange={(e) => changeTargetType(e.target.value as EscalationTargetType)}>
              {TARGET_TYPES.map((t) => <option key={t} value={t}>{escalationTargetTypeLabel[t]}</option>)}
            </Select>
          </Field>

          {form.targetType === 'User' && (
            <Field label="الموظّف الهدف">
              {directory.isLoading ? (
                <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
              ) : directory.isError ? (
                <>
                  <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                  <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
                </>
              ) : (directory.data?.users ?? []).length === 0 ? (
                <>
                  <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                  <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
                </>
              ) : (
                <Select value={form.targetUserId ?? ''} onChange={(e) => set('targetUserId', e.target.value || undefined)}>
                  <option value="">اختر…</option>
                  {(directory.data?.users ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </Select>
              )}
            </Field>
          )}
          {form.targetType === 'Department' && (
            <Field label="الإدارة الهدف">
              {directory.isLoading ? (
                <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
              ) : directory.isError ? (
                <>
                  <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                  <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
                </>
              ) : (directory.data?.departments ?? []).length === 0 ? (
                <>
                  <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                  <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
                </>
              ) : (
                <Select value={form.targetDepartmentId ?? ''} onChange={(e) => set('targetDepartmentId', e.target.value || undefined)}>
                  <option value="">اختر…</option>
                  {(directory.data?.departments ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </Select>
              )}
            </Field>
          )}
          {form.targetType === 'Team' && (
            <Field label="الفريق الهدف">
              {directory.isLoading ? (
                <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
              ) : directory.isError ? (
                <>
                  <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                  <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
                </>
              ) : (directory.data?.teams ?? []).length === 0 ? (
                <>
                  <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                  <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
                </>
              ) : (
                <Select value={form.targetTeamId ?? ''} onChange={(e) => set('targetTeamId', e.target.value || undefined)}>
                  <option value="">اختر…</option>
                  {(directory.data?.teams ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </Select>
              )}
            </Field>
          )}

          <div className="md:col-span-2">
            <Field label="تقرير مرتبط (اختياري)">
              <Select value={form.relatedSubmissionId ?? ''} onChange={(e) => set('relatedSubmissionId', e.target.value || undefined)}>
                <option value="">بدون</option>
                {(submissions.data ?? []).map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.templateTitle} — {s.submitterName} ({s.periodKey})
                  </option>
                ))}
              </Select>
            </Field>
          </div>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>إلغاء</Button>
          <Button disabled={create.isPending} onClick={submit}>إنشاء التصعيد</Button>
        </div>
      </div>
    </div>
  );
}

// ===== التفاصيل + الخطّ الزمني =====
function GovernanceEscalationDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const { data, isLoading, isError, refetch } = useGovernanceEscalation(id);

  if (isLoading) return <LoadingState label="يتم تحميل تفاصيل التصعيد…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل التصعيد" description="حدث خطأ أثناء جلب التفاصيل. أعد المحاولة." />;

  return <DetailBody key={id} data={data} onBack={onBack} />;
}

function DetailBody({ data, onBack }: { data: GovernanceEscalationDetailDto; onBack: () => void }) {
  const it = data.item;
  const navigate = useNavigate();
  const changeStatus = useChangeGovernanceEscalationStatus();
  const assign = useAssignGovernanceEscalation();
  const addComment = useAddGovernanceEscalationComment();
  const reopen = useReopenGovernanceEscalation();
  const close = useCloseGovernanceEscalation();
  const update = useUpdateGovernanceEscalation();

  const [editing, setEditing] = useState(false);
  const [newStatus, setNewStatus] = useState<GovernanceEscalationStatus>(it.status);
  const [statusNote, setStatusNote] = useState('');
  const [statusResolution, setStatusResolution] = useState('');
  const [assignTo, setAssignTo] = useState(it.assignedToUserId ?? '');
  const [assignNote, setAssignNote] = useState('');
  const [closeResolution, setCloseResolution] = useState('');
  const [closeNote, setCloseNote] = useState('');
  const [reopenNote, setReopenNote] = useState('');
  const [comment, setComment] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const users = useDirectoryUsers();

  const isResolvedStatus = newStatus === 'Resolved' || newStatus === 'Closed';

  const target = it.targetUserName ?? it.targetDepartmentName ?? it.targetTeamName ?? escalationTargetTypeLabel[it.targetType];

  const submitStatus = () => {
    setErr(null);
    if (newStatus === it.status) { setErr('اختر حالة مختلفة عن الحالية.'); return; }
    changeStatus.mutate(
      { id: it.id, req: { status: newStatus, note: statusNote.trim() || undefined, resolution: isResolvedStatus ? statusResolution.trim() || undefined : undefined } },
      { onSuccess: () => { setStatusNote(''); setStatusResolution(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitAssign = () => {
    setErr(null);
    if (!assignTo) { setErr('اختر الموظّف المُسنَد إليه.'); return; }
    assign.mutate(
      { id: it.id, req: { assignedToUserId: assignTo, note: assignNote.trim() || undefined } },
      { onSuccess: () => { setAssignNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitClose = () => {
    setErr(null);
    close.mutate(
      { id: it.id, req: { resolution: closeResolution.trim() || undefined, note: closeNote.trim() || undefined } },
      { onSuccess: () => { setCloseResolution(''); setCloseNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitReopen = () => {
    setErr(null);
    reopen.mutate(
      { id: it.id, req: { note: reopenNote.trim() || undefined } },
      { onSuccess: () => { setReopenNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitComment = () => {
    setErr(null);
    if (!comment.trim()) { setErr('نص التعليق مطلوب.'); return; }
    addComment.mutate(
      { id: it.id, req: { body: comment.trim() } },
      { onSuccess: () => { setComment(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{it.title}</h1>
        <Badge tone={escalationSeverityTone(it.severity)}>{escalationSeverityLabel[it.severity]}</Badge>
        <Badge tone={governanceEscalationStatusTone(it.status)}>{governanceEscalationStatusLabel[it.status]}</Badge>
        <Badge tone="navy">{escalationTypeLabel[it.escalationType]}</Badge>
        {data.canEdit && !editing && (
          <Button variant="ghost" onClick={() => setEditing(true)}>تعديل البيانات</Button>
        )}
        <Button
          variant="ghost"
          onClick={() => navigate(`/app/governance/action-items?sourceType=Escalation&sourceId=${it.id}`)}
        >
          + إنشاء إجراء متابعة
        </Button>
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      {editing ? (
        <EditEscalationForm data={data} update={update} onDone={() => setEditing(false)} onError={setErr} />
      ) : (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">التفاصيل</h2>
          <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <Detail label="النوع" value={escalationTypeLabel[it.escalationType]} />
            <Detail label="الخطورة" value={escalationSeverityLabel[it.severity]} />
            <Detail label="الحالة" value={governanceEscalationStatusLabel[it.status]} />
            <Detail label="نوع الهدف" value={escalationTargetTypeLabel[it.targetType]} />
            <Detail label="الهدف" value={target} />
            <Detail label="رفعه" value={it.raisedByName ?? '—'} />
            <Detail label="المُسنَد إليه" value={it.assignedToName ?? '—'} />
            <Detail label="تاريخ الإنشاء" value={formatDateTime(it.createdAtUtc)} />
          </div>
          {data.description && (
            <div className="mt-3">
              <Alert tone="navy">{data.description}</Alert>
            </div>
          )}
          {data.resolution && (
            <div className="mt-3">
              <p className="mb-1 text-xs text-ink-2">ملخّص المعالجة</p>
              <Alert tone="success">{data.resolution}</Alert>
            </div>
          )}
          {data.closedAtUtc && (
            <p className="mt-3 text-xs text-ink-2">
              أُغلق بواسطة {data.closedByName ?? '—'} · {formatDateTime(data.closedAtUtc)}
            </p>
          )}
        </Card>
      )}

      {/* الإسناد */}
      {data.canAssign && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">الإسناد</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="إسناد إلى">
              <Select value={assignTo} onChange={(e) => setAssignTo(e.target.value)}>
                <option value="">اختر…</option>
                {(users.data ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
              </Select>
            </Field>
            <Field label="ملاحظة (اختياري)">
              <Input value={assignNote} onChange={(e) => setAssignNote(e.target.value)} placeholder="سياق الإسناد…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button disabled={assign.isPending} onClick={submitAssign}>تأكيد الإسناد</Button>
          </div>
        </Card>
      )}

      {/* تغيير الحالة */}
      {data.canChangeStatus && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">تغيير الحالة</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="الحالة الجديدة">
              <Select value={newStatus} onChange={(e) => setNewStatus(e.target.value as GovernanceEscalationStatus)}>
                {STATUSES.map((s) => <option key={s} value={s}>{governanceEscalationStatusLabel[s]}</option>)}
              </Select>
            </Field>
            <Field label="ملاحظة (اختياري)">
              <Input value={statusNote} onChange={(e) => setStatusNote(e.target.value)} placeholder="سبب/سياق تغيير الحالة…" />
            </Field>
            {isResolvedStatus && (
              <div className="md:col-span-2">
                <Field label="ملخّص المعالجة (اختياري)">
                  <Input value={statusResolution} onChange={(e) => setStatusResolution(e.target.value)} placeholder="كيف عولج التصعيد…" />
                </Field>
              </div>
            )}
          </div>
          <div className="mt-4">
            <Button disabled={changeStatus.isPending} onClick={submitStatus}>تحديث الحالة</Button>
          </div>
        </Card>
      )}

      {/* الإغلاق */}
      {data.canClose && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">إغلاق التصعيد</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <div className="md:col-span-2">
              <Field label="ملخّص المعالجة (اختياري)">
                <Input value={closeResolution} onChange={(e) => setCloseResolution(e.target.value)} placeholder="كيف عولج التصعيد قبل الإغلاق…" />
              </Field>
            </div>
            <Field label="ملاحظة (اختياري)">
              <Input value={closeNote} onChange={(e) => setCloseNote(e.target.value)} placeholder="ملاحظة الإغلاق…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button disabled={close.isPending} onClick={submitClose}>إغلاق التصعيد</Button>
          </div>
        </Card>
      )}

      {/* إعادة الفتح */}
      {data.canReopen && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">إعادة فتح التصعيد</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="سبب إعادة الفتح (اختياري)">
              <Input value={reopenNote} onChange={(e) => setReopenNote(e.target.value)} placeholder="سبب إعادة الفتح…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button disabled={reopen.isPending} onClick={submitReopen}>إعادة الفتح</Button>
          </div>
        </Card>
      )}

      {/* الخطّ الزمني */}
      <Card>
        <h2 className="mb-3 font-semibold text-navy">الخطّ الزمني للتحديثات</h2>
        {data.timeline.length === 0 ? (
          <p className="text-sm text-ink-2">لا توجد تحديثات بعد.</p>
        ) : (
          <ol className="space-y-3">
            {data.timeline.map((u) => (
              <li key={u.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-center gap-2 text-xs text-ink-2">
                  <Badge tone="navy">{escalationUpdateTypeLabel[u.updateType]}</Badge>
                  <span className="font-medium text-ink">{u.authorName ?? '—'}</span>
                  <span>· {formatDateTime(u.createdAtUtc)}</span>
                  {u.oldStatus && u.newStatus && (
                    <span>
                      · {governanceEscalationStatusLabel[u.oldStatus]} ← {governanceEscalationStatusLabel[u.newStatus]}
                    </span>
                  )}
                </div>
                {u.body && <p className="mt-2 whitespace-pre-wrap text-sm text-ink">{u.body}</p>}
              </li>
            ))}
          </ol>
        )}

        {/* إضافة تعليق */}
        {data.canComment && (
          <div className="mt-4 border-t border-line pt-4">
            <Field label="إضافة تعليق">
              <Input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اكتب تعليقًا أو ملاحظة متابعة…" />
            </Field>
            <div className="mt-3">
              <Button disabled={addComment.isPending} onClick={submitComment}>إضافة</Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}

function EditEscalationForm({
  data,
  update,
  onDone,
  onError,
}: {
  data: GovernanceEscalationDetailDto;
  update: ReturnType<typeof useUpdateGovernanceEscalation>;
  onDone: () => void;
  onError: (m: string | null) => void;
}) {
  const it = data.item;
  // دليل أهداف التصعيد الآمن (يستثني الحسابات الحسّاسة) لتعديل الهدف خارج النطاق دون كشف الحسابات الحسّاسة.
  const directory = useEscalationTargetDirectory();
  const submissions = useAllSubmissions();

  const [form, setForm] = useState<CreateGovernanceEscalationRequest>({
    title: it.title,
    description: data.description ?? '',
    escalationType: it.escalationType,
    severity: it.severity,
    targetType: it.targetType,
    targetUserId: it.targetUserId ?? undefined,
    targetDepartmentId: it.targetDepartmentId ?? undefined,
    targetTeamId: it.targetTeamId ?? undefined,
    relatedSubmissionId: it.relatedSubmissionId ?? undefined,
    relatedGovernanceItemId: it.relatedGovernanceItemId ?? undefined,
  });

  const set = <K extends keyof CreateGovernanceEscalationRequest>(k: K, v: CreateGovernanceEscalationRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const changeTargetType = (t: EscalationTargetType) =>
    setForm((f) => ({
      ...f,
      targetType: t,
      targetUserId: t === 'User' ? f.targetUserId : undefined,
      targetDepartmentId: t === 'Department' ? f.targetDepartmentId : undefined,
      targetTeamId: t === 'Team' ? f.targetTeamId : undefined,
    }));

  const save = () => {
    onError(null);
    if (!form.title.trim()) { onError('العنوان مطلوب.'); return; }
    if (form.targetType === 'User' && !form.targetUserId) { onError('اختر الموظّف الهدف.'); return; }
    if (form.targetType === 'Department' && !form.targetDepartmentId) { onError('اختر الإدارة الهدف.'); return; }
    if (form.targetType === 'Team' && !form.targetTeamId) { onError('اختر الفريق الهدف.'); return; }
    update.mutate(
      {
        id: it.id,
        req: {
          ...form,
          title: form.title.trim(),
          description: form.description?.trim() || undefined,
          targetUserId: form.targetUserId || undefined,
          targetDepartmentId: form.targetDepartmentId || undefined,
          targetTeamId: form.targetTeamId || undefined,
          relatedSubmissionId: form.relatedSubmissionId || undefined,
          relatedGovernanceItemId: form.relatedGovernanceItemId || undefined,
        },
      },
      { onSuccess: onDone, onError: (e) => onError(apiErrorMessage(e)) },
    );
  };

  return (
    <Card>
      <h2 className="mb-3 font-semibold text-navy">تعديل البيانات</h2>
      <div className="grid gap-3 md:grid-cols-2">
        <div className="md:col-span-2">
          <Field label="العنوان">
            <Input value={form.title} onChange={(e) => set('title', e.target.value)} />
          </Field>
        </div>
        <div className="md:col-span-2">
          <Field label="الوصف (اختياري)">
            <Input value={form.description ?? ''} onChange={(e) => set('description', e.target.value)} />
          </Field>
        </div>
        <Field label="النوع">
          <Select value={form.escalationType} onChange={(e) => set('escalationType', e.target.value as EscalationType)}>
            {TYPES.map((t) => <option key={t} value={t}>{escalationTypeLabel[t]}</option>)}
          </Select>
        </Field>
        <Field label="الخطورة">
          <Select value={form.severity} onChange={(e) => set('severity', e.target.value as EscalationSeverity)}>
            {SEVERITIES.map((s) => <option key={s} value={s}>{escalationSeverityLabel[s]}</option>)}
          </Select>
        </Field>
        <Field label="نوع الهدف">
          <Select value={form.targetType} onChange={(e) => changeTargetType(e.target.value as EscalationTargetType)}>
            {TARGET_TYPES.map((t) => <option key={t} value={t}>{escalationTargetTypeLabel[t]}</option>)}
          </Select>
        </Field>
        {form.targetType === 'User' && (
          <Field label="الموظّف الهدف">
            {directory.isLoading ? (
              <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
            ) : directory.isError ? (
              <>
                <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
              </>
            ) : (directory.data?.users ?? []).length === 0 ? (
              <>
                <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
              </>
            ) : (
              <Select value={form.targetUserId ?? ''} onChange={(e) => set('targetUserId', e.target.value || undefined)}>
                <option value="">اختر…</option>
                {(directory.data?.users ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
              </Select>
            )}
          </Field>
        )}
        {form.targetType === 'Department' && (
          <Field label="الإدارة الهدف">
            {directory.isLoading ? (
              <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
            ) : directory.isError ? (
              <>
                <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
              </>
            ) : (directory.data?.departments ?? []).length === 0 ? (
              <>
                <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
              </>
            ) : (
              <Select value={form.targetDepartmentId ?? ''} onChange={(e) => set('targetDepartmentId', e.target.value || undefined)}>
                <option value="">اختر…</option>
                {(directory.data?.departments ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </Select>
            )}
          </Field>
        )}
        {form.targetType === 'Team' && (
          <Field label="الفريق الهدف">
            {directory.isLoading ? (
              <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
            ) : directory.isError ? (
              <>
                <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
              </>
            ) : (directory.data?.teams ?? []).length === 0 ? (
              <>
                <Select disabled value=""><option value="">لا توجد خيارات</option></Select>
                <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
              </>
            ) : (
              <Select value={form.targetTeamId ?? ''} onChange={(e) => set('targetTeamId', e.target.value || undefined)}>
                <option value="">اختر…</option>
                {(directory.data?.teams ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </Select>
            )}
          </Field>
        )}
        <div className="md:col-span-2">
          <Field label="تقرير مرتبط (اختياري)">
            <Select value={form.relatedSubmissionId ?? ''} onChange={(e) => set('relatedSubmissionId', e.target.value || undefined)}>
              <option value="">بدون</option>
              {(submissions.data ?? []).map((s) => (
                <option key={s.id} value={s.id}>{s.templateTitle} — {s.submitterName} ({s.periodKey})</option>
              ))}
            </Select>
          </Field>
        </div>
      </div>
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="ghost" onClick={onDone}>إلغاء</Button>
        <Button disabled={update.isPending} onClick={save}>حفظ التعديلات</Button>
      </div>
    </Card>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-ink-2">{label}</p>
      <p className="whitespace-pre-wrap font-medium text-ink">{value}</p>
    </div>
  );
}

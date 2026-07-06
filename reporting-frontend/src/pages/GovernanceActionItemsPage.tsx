import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  useGovernanceActionItems,
  useGovernanceActionItem,
  useActionItemAssigneeDirectory,
  useCreateGovernanceActionItem,
  useChangeGovernanceActionItemStatus,
  useAssignGovernanceActionItem,
  useChangeGovernanceActionItemDueDate,
  useAddGovernanceActionItemComment,
  useCancelGovernanceActionItem,
  type GovernanceActionItemsFilter,
} from '../lib/useGovernanceActionItems';
import { useAuth } from '../lib/auth';
import { apiErrorMessage } from '../lib/api';
import {
  actionItemStatusLabel,
  actionItemStatusTone,
  actionItemPriorityLabel,
  actionItemPriorityTone,
  actionItemSourceTypeLabel,
  actionItemUpdateTypeLabel,
  formatDate,
  formatDateTime,
} from '../lib/format';
import type {
  ActionItemStatus,
  ActionItemPriority,
  ActionItemSourceType,
  GovernanceActionItemListItemDto,
  GovernanceActionItemDetailDto,
  CreateGovernanceActionItemRequest,
} from '../types/api';

const PRIORITIES: ActionItemPriority[] = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES: ActionItemStatus[] = ['Open', 'InProgress', 'Blocked', 'Completed', 'Cancelled'];
// حالات يُمكن ضبطها عبر نقطة تغيير الحالة (الإلغاء له نقطة مستقلّة).
const STATUS_CHOICES: ActionItemStatus[] = ['Open', 'InProgress', 'Blocked', 'Completed'];
const SOURCE_TYPES: ActionItemSourceType[] = ['Manual', 'Escalation', 'GovernanceItem'];

const OPEN_STATUSES: ActionItemStatus[] = ['Open', 'InProgress', 'Blocked'];
const isOpen = (s: ActionItemStatus) => OPEN_STATUSES.includes(s);

export default function GovernanceActionItemsPage() {
  const [params, setParams] = useSearchParams();
  const openId = params.get('open');

  const [status, setStatus] = useState<ActionItemStatus | ''>('');
  const [priority, setPriority] = useState<ActionItemPriority | ''>('');
  const [sourceType, setSourceType] = useState<ActionItemSourceType | ''>('');
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [mineOnly, setMineOnly] = useState(false);
  const [assignedToMe, setAssignedToMe] = useState(false);

  const presetSourceType = params.get('sourceType') as ActionItemSourceType | null;
  const presetSourceId = params.get('sourceId');
  // فتح نموذج الإنشاء تلقائيًّا عند القدوم من ربط مصدر (مثل تصعيد).
  const [showCreate, setShowCreate] = useState(!!presetSourceId);

  const clearPreset = () =>
    setParams((p) => {
      const n = new URLSearchParams(p);
      n.delete('sourceType');
      n.delete('sourceId');
      return n;
    });

  const filter: GovernanceActionItemsFilter = {
    status: status || undefined,
    priority: priority || undefined,
    sourceType: sourceType || undefined,
    overdueOnly: overdueOnly || undefined,
    mineOnly: mineOnly || undefined,
    assignedToMe: assignedToMe || undefined,
  };

  const { data, isLoading, isError, refetch } = useGovernanceActionItems(filter);

  const openDetail = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <GovernanceActionItemDetail id={openId} onBack={back} />;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">إجراءات الحوكمة والمتابعة</h1>
          <p className="mt-1 max-w-2xl text-sm text-ink-2">
            تحويل أيّ ملاحظة أو تصعيد أو بند حوكمة إلى إجراء متابَع له مُسنَد إليه وتاريخ استحقاق
            وأولوية وحالة وخطّ زمني للمتابعة. الرؤية والإجراءات مقيّدة حسب دورك ونطاقك.
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>+ إجراء جديد</Button>
      </div>

      <SummaryCards
        onOpenMine={() => { setMineOnly(true); setAssignedToMe(false); setOverdueOnly(false); }}
        onOpenAssigned={() => { setAssignedToMe(true); setMineOnly(false); setOverdueOnly(false); }}
        onOpenOverdue={() => { setOverdueOnly(true); setMineOnly(false); setAssignedToMe(false); }}
      />

      <Card>
        <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-4">
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as ActionItemStatus | '')}>
              <option value="">كل الحالات</option>
              {STATUSES.map((s) => <option key={s} value={s}>{actionItemStatusLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="الأولوية">
            <Select value={priority} onChange={(e) => setPriority(e.target.value as ActionItemPriority | '')}>
              <option value="">كل المستويات</option>
              {PRIORITIES.map((p) => <option key={p} value={p}>{actionItemPriorityLabel[p]}</option>)}
            </Select>
          </Field>
          <Field label="المصدر">
            <Select value={sourceType} onChange={(e) => setSourceType(e.target.value as ActionItemSourceType | '')}>
              <option value="">كل المصادر</option>
              {SOURCE_TYPES.map((t) => <option key={t} value={t}>{actionItemSourceTypeLabel[t]}</option>)}
            </Select>
          </Field>
          <Field label="المتأخّرة فقط">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={overdueOnly} onChange={(e) => setOverdueOnly(e.target.checked)} />
              تجاوزت تاريخ الاستحقاق
            </label>
          </Field>
          <Field label="إجراءاتي">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={mineOnly} onChange={(e) => setMineOnly(e.target.checked)} />
              ما أنشأتُه فقط
            </label>
          </Field>
          <Field label="المُسنَدة إليّ">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={assignedToMe} onChange={(e) => setAssignedToMe(e.target.checked)} />
              المُسنَد إليّ فقط
            </label>
          </Field>
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل الإجراءات…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الإجراءات. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد إجراءات مطابقة"
                description="عدّل الفلاتر أعلاه أو أنشئ إجراءً جديدًا."
              />
            </div>
          ) : (
            <ActionItemsTable items={data!} onOpen={openDetail} />
          )}
        </Card>
      )}

      {showCreate && (
        <CreateActionItemModal
          presetSourceType={presetSourceType ?? undefined}
          presetSourceId={presetSourceId ?? undefined}
          onClose={() => { setShowCreate(false); clearPreset(); }}
          onCreated={openDetail}
        />
      )}
    </div>
  );
}

// ===== بطاقات لوحة موجزة (مختلفة حسب الدور) =====
function SummaryCards({
  onOpenMine,
  onOpenAssigned,
  onOpenOverdue,
}: {
  onOpenMine: () => void;
  onOpenAssigned: () => void;
  onOpenOverdue: () => void;
}) {
  const { user, hasAnyRole } = useAuth();
  const wide = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'CeoSupport');
  const uid = user?.userId;
  const { data } = useGovernanceActionItems({});
  const all = data ?? [];

  const openCount = all.filter((a) => isOpen(a.status)).length;
  const overdue = all.filter((a) => a.isOverdue).length;
  const assignedToMe = all.filter((a) => isOpen(a.status) && a.assignedToUserId === uid).length;
  const mine = all.filter((a) => a.createdByUserId === uid).length;

  const cards = wide
    ? [
        { label: 'إجراءات مفتوحة', value: openCount, tone: 'navy' as const, onClick: onOpenOverdue },
        { label: 'متأخّرة', value: overdue, tone: 'alert' as const, onClick: onOpenOverdue },
        { label: 'مُسنَدة إليّ', value: assignedToMe, tone: 'orange' as const, onClick: onOpenAssigned },
        { label: 'أنشأتُها', value: mine, tone: 'gold' as const, onClick: onOpenMine },
      ]
    : [
        { label: 'إجراءاتي', value: mine, tone: 'navy' as const, onClick: onOpenMine },
        { label: 'مُسنَدة إليّ', value: assignedToMe, tone: 'orange' as const, onClick: onOpenAssigned },
        { label: 'متأخّرة', value: overdue, tone: 'alert' as const, onClick: onOpenOverdue },
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

function ActionItemsTable({
  items,
  onOpen,
}: {
  items: GovernanceActionItemListItemDto[];
  onOpen: (id: string) => void;
}) {
  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">العنوان</th>
          <th className="px-3 py-2.5 font-semibold">المصدر</th>
          <th className="px-3 py-2.5 font-semibold">الأولوية</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
          <th className="px-3 py-2.5 font-semibold">الاستحقاق</th>
          <th className="px-3 py-2.5 font-semibold">المُسنَد إليه</th>
          <th className="px-3 py-2.5 font-semibold">أنشأه</th>
          <th className="px-3 py-2.5 font-semibold"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((a) => (
          <tr
            key={a.id}
            onClick={() => onOpen(a.id)}
            className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
          >
            <td className="px-3 py-2.5 font-medium text-navy">{a.title}</td>
            <td className="px-3 py-2.5 text-ink-2">{actionItemSourceTypeLabel[a.sourceType]}</td>
            <td className="px-3 py-2.5">
              <Badge tone={actionItemPriorityTone(a.priority)}>{actionItemPriorityLabel[a.priority]}</Badge>
            </td>
            <td className="px-3 py-2.5">
              <Badge tone={actionItemStatusTone(a.status)}>{actionItemStatusLabel[a.status]}</Badge>
            </td>
            <td className="px-3 py-2.5 text-ink-2">
              {a.dueDate ? (
                <span className={a.isOverdue ? 'font-semibold text-alert' : ''}>
                  {formatDate(a.dueDate)}{a.isOverdue ? ' (متأخّر)' : ''}
                </span>
              ) : '—'}
            </td>
            <td className="px-3 py-2.5 text-ink-2">{a.assignedToName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">{a.createdByName ?? '—'}</td>
            <td className="px-3 py-2.5">
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(a.id); }}>عرض</Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ===== نموذج إنشاء إجراء =====
function CreateActionItemModal({
  presetSourceType,
  presetSourceId,
  onClose,
  onCreated,
}: {
  presetSourceType?: ActionItemSourceType;
  presetSourceId?: string;
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const create = useCreateGovernanceActionItem();
  const [err, setErr] = useState<string | null>(null);
  const [form, setForm] = useState<CreateGovernanceActionItemRequest>({
    title: '',
    description: '',
    priority: 'Medium',
    sourceType: presetSourceType ?? 'Manual',
    sourceId: presetSourceId,
    dueDate: undefined,
  });

  // دليل المُسنَد إليهم الآمن (على مستوى الشركة، يستثني الحسابات الحسّاسة).
  const directory = useActionItemAssigneeDirectory();

  const set = <K extends keyof CreateGovernanceActionItemRequest>(k: K, v: CreateGovernanceActionItemRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const submit = () => {
    setErr(null);
    if (!form.title.trim()) { setErr('العنوان مطلوب.'); return; }
    create.mutate(
      {
        ...form,
        title: form.title.trim(),
        description: form.description?.trim() || undefined,
        assignedToUserId: form.assignedToUserId || undefined,
        dueDate: form.dueDate || undefined,
        sourceId: form.sourceId || undefined,
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
          <h2 className="text-lg font-bold text-navy">إجراء جديد</h2>
          <button onClick={onClose} className="text-sm text-ink-2 hover:text-navy">إغلاق ✕</button>
        </div>

        {err && <Alert tone="alert">{err}</Alert>}
        {presetSourceType === 'Escalation' && presetSourceId && (
          <div className="mt-2">
            <Alert tone="navy">سيُربَط هذا الإجراء بالتصعيد المصدر. لن يُكشَف مصدر التصعيد للمُسنَد إليه.</Alert>
          </div>
        )}

        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <div className="md:col-span-2">
            <Field label="العنوان">
              <Input value={form.title} onChange={(e) => set('title', e.target.value)} placeholder="عنوان مختصر للإجراء…" />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field label="الوصف (اختياري)">
              <Input value={form.description ?? ''} onChange={(e) => set('description', e.target.value)} placeholder="تفاصيل الإجراء / السياق…" />
            </Field>
          </div>
          <Field label="الأولوية">
            <Select value={form.priority} onChange={(e) => set('priority', e.target.value as ActionItemPriority)}>
              {PRIORITIES.map((p) => <option key={p} value={p}>{actionItemPriorityLabel[p]}</option>)}
            </Select>
          </Field>
          <Field label="تاريخ الاستحقاق (اختياري)">
            <Input type="date" value={form.dueDate ?? ''} onChange={(e) => set('dueDate', e.target.value || undefined)} />
          </Field>
          <div className="md:col-span-2">
            <Field label="المُسنَد إليه (اختياري)">
              {directory.isLoading ? (
                <Select disabled value=""><option value="">جارٍ تحميل الخيارات…</option></Select>
              ) : directory.isError ? (
                <>
                  <Select disabled value=""><option value="">تعذّر تحميل الخيارات</option></Select>
                  <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
                </>
              ) : (directory.data?.users ?? []).length === 0 ? (
                <>
                  <Select disabled value=""><option value="">بدون</option></Select>
                  <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
                </>
              ) : (
                <Select value={form.assignedToUserId ?? ''} onChange={(e) => set('assignedToUserId', e.target.value || undefined)}>
                  <option value="">بدون</option>
                  {(directory.data?.users ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </Select>
              )}
            </Field>
          </div>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>إلغاء</Button>
          <Button disabled={create.isPending} onClick={submit}>إنشاء الإجراء</Button>
        </div>
      </div>
    </div>
  );
}

// ===== التفاصيل + الخطّ الزمني =====
function GovernanceActionItemDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const { data, isLoading, isError, refetch } = useGovernanceActionItem(id);

  if (isLoading) return <LoadingState label="يتم تحميل تفاصيل الإجراء…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل الإجراء" description="حدث خطأ أثناء جلب التفاصيل. أعد المحاولة." />;

  return <DetailBody key={id} data={data} onBack={onBack} />;
}

function DetailBody({ data, onBack }: { data: GovernanceActionItemDetailDto; onBack: () => void }) {
  const it = data.item;
  const changeStatus = useChangeGovernanceActionItemStatus();
  const assign = useAssignGovernanceActionItem();
  const changeDueDate = useChangeGovernanceActionItemDueDate();
  const addComment = useAddGovernanceActionItemComment();
  const cancel = useCancelGovernanceActionItem();

  const [newStatus, setNewStatus] = useState<ActionItemStatus>(
    it.status === 'Completed' || it.status === 'Cancelled' ? 'InProgress' : it.status,
  );
  const [statusNote, setStatusNote] = useState('');
  const [completionNote, setCompletionNote] = useState('');
  const [assignTo, setAssignTo] = useState(it.assignedToUserId ?? '');
  const [assignNote, setAssignNote] = useState('');
  const [dueDate, setDueDate] = useState(it.dueDate ?? '');
  const [dueNote, setDueNote] = useState('');
  const [cancelNote, setCancelNote] = useState('');
  const [comment, setComment] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const directory = useActionItemAssigneeDirectory();

  const submitStatus = () => {
    setErr(null);
    if (newStatus === it.status) { setErr('اختر حالة مختلفة عن الحالية.'); return; }
    changeStatus.mutate(
      {
        id: it.id,
        req: {
          status: newStatus,
          note: statusNote.trim() || undefined,
          completionNote: newStatus === 'Completed' ? completionNote.trim() || undefined : undefined,
        },
      },
      { onSuccess: () => { setStatusNote(''); setCompletionNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitReopen = () => {
    setErr(null);
    changeStatus.mutate(
      { id: it.id, req: { status: 'Open', note: statusNote.trim() || undefined } },
      { onSuccess: () => { setStatusNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
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

  const submitDueDate = () => {
    setErr(null);
    changeDueDate.mutate(
      { id: it.id, req: { dueDate: dueDate || undefined, note: dueNote.trim() || undefined } },
      { onSuccess: () => { setDueNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitCancel = () => {
    setErr(null);
    cancel.mutate(
      { id: it.id, req: { note: cancelNote.trim() || undefined } },
      { onSuccess: () => { setCancelNote(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
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
        <Badge tone={actionItemPriorityTone(it.priority)}>{actionItemPriorityLabel[it.priority]}</Badge>
        <Badge tone={actionItemStatusTone(it.status)}>{actionItemStatusLabel[it.status]}</Badge>
        {it.isOverdue && <Badge tone="alert">متأخّر</Badge>}
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      <Card>
        <h2 className="mb-3 font-semibold text-navy">التفاصيل</h2>
        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Detail label="الأولوية" value={actionItemPriorityLabel[it.priority]} />
          <Detail label="الحالة" value={actionItemStatusLabel[it.status]} />
          <Detail label="المصدر" value={actionItemSourceTypeLabel[it.sourceType]} />
          <Detail
            label="بند المصدر"
            value={data.sourceVisibleToViewer ? (it.sourceTitle ?? '—') : 'غير ظاهر'}
          />
          <Detail label="تاريخ الاستحقاق" value={it.dueDate ? formatDate(it.dueDate) : '—'} />
          <Detail label="المُسنَد إليه" value={it.assignedToName ?? '—'} />
          <Detail label="أسنده" value={data.assignedByName ?? '—'} />
          <Detail label="أنشأه" value={it.createdByName ?? '—'} />
          <Detail label="تاريخ الإنشاء" value={formatDateTime(it.createdAtUtc)} />
        </div>
        {data.description && (
          <div className="mt-3">
            <Alert tone="navy">{data.description}</Alert>
          </div>
        )}
        {data.completionNote && (
          <div className="mt-3">
            <p className="mb-1 text-xs text-ink-2">ملاحظة الإكمال</p>
            <Alert tone="success">{data.completionNote}</Alert>
          </div>
        )}
        {data.completedAtUtc && (
          <p className="mt-3 text-xs text-ink-2">
            اكتمل بواسطة {data.completedByName ?? '—'} · {formatDateTime(data.completedAtUtc)}
          </p>
        )}
      </Card>

      {/* الإسناد */}
      {data.canAssign && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">الإسناد</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="إسناد إلى">
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
                <Select value={assignTo} onChange={(e) => setAssignTo(e.target.value)}>
                  <option value="">اختر…</option>
                  {(directory.data?.users ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </Select>
              )}
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
              <Select value={newStatus} onChange={(e) => setNewStatus(e.target.value as ActionItemStatus)}>
                {STATUS_CHOICES.map((s) => <option key={s} value={s}>{actionItemStatusLabel[s]}</option>)}
              </Select>
            </Field>
            <Field label="ملاحظة (اختياري)">
              <Input value={statusNote} onChange={(e) => setStatusNote(e.target.value)} placeholder="سبب/سياق تغيير الحالة…" />
            </Field>
            {newStatus === 'Completed' && (
              <div className="md:col-span-2">
                <Field label="ملاحظة الإكمال (اختياري)">
                  <Input value={completionNote} onChange={(e) => setCompletionNote(e.target.value)} placeholder="كيف نُفِّذ الإجراء…" />
                </Field>
              </div>
            )}
          </div>
          <div className="mt-4">
            <Button disabled={changeStatus.isPending} onClick={submitStatus}>تحديث الحالة</Button>
          </div>
        </Card>
      )}

      {/* تاريخ الاستحقاق */}
      {data.canChangeDueDate && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">تاريخ الاستحقاق</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="التاريخ (اتركه فارغًا لإزالته)">
              <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </Field>
            <Field label="ملاحظة (اختياري)">
              <Input value={dueNote} onChange={(e) => setDueNote(e.target.value)} placeholder="سبب تغيير الاستحقاق…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button disabled={changeDueDate.isPending} onClick={submitDueDate}>تحديث الاستحقاق</Button>
          </div>
        </Card>
      )}

      {/* إعادة الفتح */}
      {data.canReopen && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">إعادة فتح الإجراء</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="سبب إعادة الفتح (اختياري)">
              <Input value={statusNote} onChange={(e) => setStatusNote(e.target.value)} placeholder="سبب إعادة الفتح…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button disabled={changeStatus.isPending} onClick={submitReopen}>إعادة الفتح</Button>
          </div>
        </Card>
      )}

      {/* الإلغاء */}
      {data.canCancel && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">إلغاء الإجراء</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="سبب الإلغاء (اختياري)">
              <Input value={cancelNote} onChange={(e) => setCancelNote(e.target.value)} placeholder="سبب الإلغاء…" />
            </Field>
          </div>
          <div className="mt-4">
            <Button variant="ghost" disabled={cancel.isPending} onClick={submitCancel}>إلغاء الإجراء</Button>
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
                  <Badge tone="navy">{actionItemUpdateTypeLabel[u.updateType]}</Badge>
                  <span className="font-medium text-ink">{u.authorName ?? '—'}</span>
                  <span>· {formatDateTime(u.createdAtUtc)}</span>
                  {u.oldStatus && u.newStatus && (
                    <span>
                      · {actionItemStatusLabel[u.oldStatus]} ← {actionItemStatusLabel[u.newStatus]}
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

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-ink-2">{label}</p>
      <p className="whitespace-pre-wrap font-medium text-ink">{value}</p>
    </div>
  );
}

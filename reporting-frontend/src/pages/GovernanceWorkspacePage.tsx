import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  useGovernanceItems,
  useGovernanceItem,
  useCreateGovernanceItem,
  useUpdateGovernanceItem,
  useChangeGovernanceItemStatus,
  useAddGovernanceItemComment,
  useGovernanceWorkspaceDirectory,
  type GovernanceItemsFilter,
} from '../lib/useGovernanceWorkspace';
import { useAllSubmissions } from '../lib/useOrg';
import { useAuth } from '../lib/auth';
import { apiErrorMessage } from '../lib/api';
import {
  governanceCategoryLabel,
  governanceSeverityLabel,
  governanceSeverityTone,
  governanceItemStatusLabel,
  governanceItemStatusTone,
  governanceItemUpdateTypeLabel,
  governanceApplicationScopeLabel,
  formatDate,
  formatDateTime,
} from '../lib/format';
import type {
  GovernanceCategory,
  GovernanceSeverity,
  GovernanceItemStatus,
  GovernanceApplicationScope,
  GovernanceItemListItemDto,
  GovernanceItemDetailDto,
  CreateGovernanceItemRequest,
} from '../types/api';

const CATEGORIES: GovernanceCategory[] = [
  'Observation',
  'Risk',
  'Decision',
  'Recommendation',
  'FollowUp',
  'Compliance',
  'Performance',
  'OperationalIssue',
];
const SEVERITIES: GovernanceSeverity[] = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES: GovernanceItemStatus[] = ['Open', 'InReview', 'Waiting', 'Resolved', 'Closed', 'Cancelled'];

// نطاق التطبيق (GOV-APPLICATION-SCOPE-R1): «كل الشركة» متاح فقط لأصحاب الرؤية الواسعة.
const SCOPES_WIDE: GovernanceApplicationScope[] = ['Company', 'Department', 'Team', 'User', 'RelatedReport'];
const SCOPES_SCOPED: GovernanceApplicationScope[] = ['Department', 'Team', 'User', 'RelatedReport'];

type WorkspaceDirectory = ReturnType<typeof useGovernanceWorkspaceDirectory>;

// قائمة اختيار تعتمد على دليل الحوكمة الموحّد (GOV-DIRECTORY-SCOPE-FIX-R1) مع حالات UX واضحة:
// تحميل / تعذّر التحميل / لا توجد خيارات ضمن النطاق / قائمة عادية. لا تُظهر «بدون» وحدها بلا تفسير.
function DirectorySelect<T extends { id: string }>({
  label,
  dir,
  options,
  getName,
  value,
  onChange,
  emptyOptionLabel,
}: {
  label: string;
  dir: WorkspaceDirectory;
  options: (d: NonNullable<WorkspaceDirectory['data']>) => T[];
  getName: (o: T) => string;
  value: string;
  onChange: (v: string) => void;
  emptyOptionLabel: string;
}) {
  const items = dir.data ? options(dir.data) : [];
  return (
    <Field label={label}>
      {dir.isLoading ? (
        <Select disabled value="">
          <option value="">جارٍ تحميل الخيارات…</option>
        </Select>
      ) : dir.isError ? (
        <>
          <Select disabled value="">
            <option value="">تعذّر تحميل الخيارات</option>
          </Select>
          <p className="mt-1 text-xs text-alert">تعذّر تحميل الخيارات. حدّث الصفحة وأعد المحاولة.</p>
        </>
      ) : items.length === 0 ? (
        <>
          <Select disabled value="">
            <option value="">{emptyOptionLabel}</option>
          </Select>
          <p className="mt-1 text-xs text-ink-2">لا توجد خيارات متاحة ضمن نطاق صلاحيتك.</p>
        </>
      ) : (
        <Select value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">{emptyOptionLabel}</option>
          {items.map((o) => (
            <option key={o.id} value={o.id}>{getName(o)}</option>
          ))}
        </Select>
      )}
    </Field>
  );
}

// حقول «نطاق التطبيق» الديناميكية (GOV-APPLICATION-SCOPE-R1): منتقي النطاق + الحقل السياقي المناسب
// لكل قيمة، مع تفريغ المراجع غير المطابقة عند تغيير النطاق. «كل الشركة» يظهر فقط لأصحاب الرؤية الواسعة.
function ScopeFields({
  wide,
  form,
  set,
  directory,
  submissions,
}: {
  wide: boolean;
  form: CreateGovernanceItemRequest;
  set: <K extends keyof CreateGovernanceItemRequest>(k: K, v: CreateGovernanceItemRequest[K]) => void;
  directory: WorkspaceDirectory;
  submissions: ReturnType<typeof useAllSubmissions>;
}) {
  const scopes = wide ? SCOPES_WIDE : SCOPES_SCOPED;
  const scope = form.applicationScope;

  const changeScope = (s: GovernanceApplicationScope) => {
    set('applicationScope', s);
    // تفريغ المراجع غير المطابقة للنطاق الجديد.
    if (s !== 'Department') set('departmentId', undefined);
    if (s !== 'Team') set('teamId', undefined);
    if (s !== 'User') set('relatedUserId', undefined);
    if (s !== 'RelatedReport') set('relatedSubmissionId', undefined);
  };

  return (
    <>
      <div className="md:col-span-2">
        <Field label="نطاق التطبيق (يُطبَّق على من؟)">
          <Select value={scope} onChange={(e) => changeScope(e.target.value as GovernanceApplicationScope)}>
            {scopes.map((s) => (
              <option key={s} value={s}>{governanceApplicationScopeLabel[s]}</option>
            ))}
          </Select>
        </Field>
      </div>

      {scope === 'Company' && (
        <div className="md:col-span-2">
          <Alert tone="navy">
            هذا البند يُطبَّق على كامل الشركة ولا يتطلّب تحديد إدارة أو فريق أو موظّف أو تقرير.
          </Alert>
        </div>
      )}

      {scope === 'Department' && (
        <div className="md:col-span-2">
          <DirectorySelect
            label="الإدارة المعنيّة"
            dir={directory}
            options={(d) => d.departments}
            getName={(d) => d.name}
            value={form.departmentId ?? ''}
            onChange={(v) => set('departmentId', v || undefined)}
            emptyOptionLabel="اختر الإدارة…"
          />
        </div>
      )}

      {scope === 'Team' && (
        <div className="md:col-span-2">
          <DirectorySelect
            label="الفريق المعنيّ"
            dir={directory}
            options={(d) => d.teams}
            getName={(t) => t.name}
            value={form.teamId ?? ''}
            onChange={(v) => set('teamId', v || undefined)}
            emptyOptionLabel="اختر الفريق…"
          />
        </div>
      )}

      {scope === 'User' && (
        <div className="md:col-span-2">
          <DirectorySelect
            label="الموظّف المعنيّ"
            dir={directory}
            options={(d) => d.users}
            getName={(u) => u.fullName}
            value={form.relatedUserId ?? ''}
            onChange={(v) => set('relatedUserId', v || undefined)}
            emptyOptionLabel="اختر الموظّف…"
          />
        </div>
      )}

      {scope === 'RelatedReport' && (
        <div className="md:col-span-2">
          <Field label="التقرير المرتبط">
            <Select value={form.relatedSubmissionId ?? ''} onChange={(e) => set('relatedSubmissionId', e.target.value || undefined)}>
              <option value="">اختر التقرير…</option>
              {(submissions.data ?? []).map((s) => (
                <option key={s.id} value={s.id}>
                  {s.templateTitle} — {s.submitterName} ({s.periodKey})
                </option>
              ))}
            </Select>
          </Field>
        </div>
      )}
    </>
  );
}

// تحقّق من اكتمال المرجع السياقي حسب نطاق التطبيق قبل الإرسال (يقابل التحقّق الخادميّ).
function scopeRefError(form: CreateGovernanceItemRequest): string | null {
  switch (form.applicationScope) {
    case 'Department':
      return form.departmentId ? null : 'يجب اختيار الإدارة عند نطاق «إدارة محددة».';
    case 'Team':
      return form.teamId ? null : 'يجب اختيار الفريق عند نطاق «فريق محدد».';
    case 'User':
      return form.relatedUserId ? null : 'يجب اختيار الموظّف عند نطاق «موظّف محدد».';
    case 'RelatedReport':
      return form.relatedSubmissionId ? null : 'يجب اختيار التقرير عند نطاق «تقرير مرتبط».';
    default:
      return null;
  }
}

export default function GovernanceWorkspacePage() {
  const [params, setParams] = useSearchParams();
  const openId = params.get('open');

  const [status, setStatus] = useState<GovernanceItemStatus | ''>('');
  const [category, setCategory] = useState<GovernanceCategory | ''>('');
  const [severity, setSeverity] = useState<GovernanceSeverity | ''>('');
  const [assignedToUserId, setAssignedToUserId] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [openOnly, setOpenOnly] = useState(false);
  const [showCreate, setShowCreate] = useState(false);

  const directory = useGovernanceWorkspaceDirectory();

  const filter: GovernanceItemsFilter = {
    status: status || undefined,
    category: category || undefined,
    severity: severity || undefined,
    assignedToUserId: assignedToUserId || undefined,
    departmentId: departmentId || undefined,
    teamId: teamId || undefined,
    openOnly: openOnly || undefined,
  };

  const { data, isLoading, isError, refetch } = useGovernanceItems(filter);

  const openDetail = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <GovernanceItemDetail id={openId} onBack={back} />;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">ورشة الحوكمة</h1>
          <p className="mt-1 max-w-2xl text-sm text-ink-2">
            مساحة عامة لتسجيل ومتابعة ملاحظات الحوكمة (من التقارير والأداء والمتابعة الإدارية) بشكل
            منظَّم وقابل للتتبّع، مع خطّ زمني للتحديثات والتعليقات وتغييرات الحالة. الرؤية مقيّدة حسب
            دورك ونطاقك.
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>+ بند حوكمة جديد</Button>
      </div>

      <Card>
        <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-4">
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as GovernanceItemStatus | '')}>
              <option value="">كل الحالات</option>
              {STATUSES.map((s) => <option key={s} value={s}>{governanceItemStatusLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="التصنيف">
            <Select value={category} onChange={(e) => setCategory(e.target.value as GovernanceCategory | '')}>
              <option value="">كل التصنيفات</option>
              {CATEGORIES.map((c) => <option key={c} value={c}>{governanceCategoryLabel[c]}</option>)}
            </Select>
          </Field>
          <Field label="الخطورة">
            <Select value={severity} onChange={(e) => setSeverity(e.target.value as GovernanceSeverity | '')}>
              <option value="">كل المستويات</option>
              {SEVERITIES.map((s) => <option key={s} value={s}>{governanceSeverityLabel[s]}</option>)}
            </Select>
          </Field>
          <DirectorySelect
            label="المُسنَد إليه"
            dir={directory}
            options={(d) => d.users}
            getName={(u) => u.fullName}
            value={assignedToUserId}
            onChange={setAssignedToUserId}
            emptyOptionLabel="الكل"
          />
          <DirectorySelect
            label="الإدارة"
            dir={directory}
            options={(d) => d.departments}
            getName={(d) => d.name}
            value={departmentId}
            onChange={setDepartmentId}
            emptyOptionLabel="كل الإدارات"
          />
          <DirectorySelect
            label="الفريق"
            dir={directory}
            options={(d) => d.teams}
            getName={(t) => t.name}
            value={teamId}
            onChange={setTeamId}
            emptyOptionLabel="كل الفِرق"
          />
          <Field label="المفتوحة فقط">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />
              إخفاء المُعالَجة/المُغلقة/المُلغاة
            </label>
          </Field>
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل بنود الحوكمة…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب بنود الحوكمة. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد بنود حوكمة مطابقة"
                description="عدّل الفلاتر أعلاه أو أنشئ بندًا جديدًا لتسجيل ملاحظة حوكمة."
              />
            </div>
          ) : (
            <ItemsTable items={data!} onOpen={openDetail} />
          )}
        </Card>
      )}

      {showCreate && <CreateItemModal onClose={() => setShowCreate(false)} onCreated={openDetail} />}
    </div>
  );
}

function ItemsTable({ items, onOpen }: { items: GovernanceItemListItemDto[]; onOpen: (id: string) => void }) {
  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">العنوان</th>
          <th className="px-3 py-2.5 font-semibold">التصنيف</th>
          <th className="px-3 py-2.5 font-semibold">الخطورة</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
          <th className="px-3 py-2.5 font-semibold">نطاق التطبيق</th>
          <th className="px-3 py-2.5 font-semibold">المُسنَد إليه</th>
          <th className="px-3 py-2.5 font-semibold">الإدارة/الفريق</th>
          <th className="px-3 py-2.5 font-semibold">الاستحقاق</th>
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
            <td className="px-3 py-2.5 text-ink-2">{governanceCategoryLabel[g.category]}</td>
            <td className="px-3 py-2.5">
              <Badge tone={governanceSeverityTone(g.severity)}>{governanceSeverityLabel[g.severity]}</Badge>
            </td>
            <td className="px-3 py-2.5">
              <Badge tone={governanceItemStatusTone(g.status)}>{governanceItemStatusLabel[g.status]}</Badge>
            </td>
            <td className="px-3 py-2.5 text-ink-2">{governanceApplicationScopeLabel[g.applicationScope]}</td>
            <td className="px-3 py-2.5 text-ink-2">{g.assignedToName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">
              {[g.departmentName, g.teamName].filter(Boolean).join(' · ') || '—'}
            </td>
            <td className="px-3 py-2.5 text-ink-2">{g.dueDate ? formatDate(g.dueDate) : '—'}</td>
            <td className="px-3 py-2.5">
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(g.id); }}>عرض</Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ===== نموذج الإنشاء =====
function CreateItemModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const { hasAnyRole } = useAuth();
  const wide = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'CeoSupport');
  const create = useCreateGovernanceItem();
  const [err, setErr] = useState<string | null>(null);
  const [form, setForm] = useState<CreateGovernanceItemRequest>({
    title: '',
    description: '',
    category: 'Observation',
    severity: 'Medium',
    applicationScope: wide ? 'Company' : 'User',
  });

  const directory = useGovernanceWorkspaceDirectory();
  const submissions = useAllSubmissions();

  const set = <K extends keyof CreateGovernanceItemRequest>(k: K, v: CreateGovernanceItemRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const submit = () => {
    setErr(null);
    if (!form.title.trim()) { setErr('العنوان مطلوب.'); return; }
    const scopeErr = scopeRefError(form);
    if (scopeErr) { setErr(scopeErr); return; }
    create.mutate(
      {
        ...form,
        title: form.title.trim(),
        description: form.description?.trim() || undefined,
        assignedToUserId: form.assignedToUserId || undefined,
        departmentId: form.departmentId || undefined,
        teamId: form.teamId || undefined,
        relatedSubmissionId: form.relatedSubmissionId || undefined,
        relatedUserId: form.relatedUserId || undefined,
        dueDate: form.dueDate || undefined,
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
          <h2 className="text-lg font-bold text-navy">بند حوكمة جديد</h2>
          <button onClick={onClose} className="text-sm text-ink-2 hover:text-navy">إغلاق ✕</button>
        </div>

        {err && <Alert tone="alert">{err}</Alert>}

        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <div className="md:col-span-2">
            <Field label="العنوان">
              <Input value={form.title} onChange={(e) => set('title', e.target.value)} placeholder="عنوان مختصر للملاحظة…" />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field label="الوصف (اختياري)">
              <Input value={form.description ?? ''} onChange={(e) => set('description', e.target.value)} placeholder="تفاصيل الملاحظة / السياق…" />
            </Field>
          </div>
          <Field label="التصنيف">
            <Select value={form.category} onChange={(e) => set('category', e.target.value as GovernanceCategory)}>
              {CATEGORIES.map((c) => <option key={c} value={c}>{governanceCategoryLabel[c]}</option>)}
            </Select>
          </Field>
          <Field label="الخطورة">
            <Select value={form.severity} onChange={(e) => set('severity', e.target.value as GovernanceSeverity)}>
              {SEVERITIES.map((s) => <option key={s} value={s}>{governanceSeverityLabel[s]}</option>)}
            </Select>
          </Field>
          <ScopeFields wide={wide} form={form} set={set} directory={directory} submissions={submissions} />
          <DirectorySelect
            label="المُسنَد إليه (اختياري)"
            dir={directory}
            options={(d) => d.users}
            getName={(u) => u.fullName}
            value={form.assignedToUserId ?? ''}
            onChange={(v) => set('assignedToUserId', v || undefined)}
            emptyOptionLabel="بدون"
          />
          <Field label="تاريخ الاستحقاق (اختياري)">
            <Input type="date" value={form.dueDate ?? ''} onChange={(e) => set('dueDate', e.target.value || undefined)} />
          </Field>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>إلغاء</Button>
          <Button disabled={create.isPending} onClick={submit}>إنشاء البند</Button>
        </div>
      </div>
    </div>
  );
}

// ===== التفاصيل + الخطّ الزمني =====
function GovernanceItemDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const { data, isLoading, isError, refetch } = useGovernanceItem(id);

  if (isLoading) return <LoadingState label="يتم تحميل تفاصيل البند…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل البند" description="حدث خطأ أثناء جلب التفاصيل. أعد المحاولة." />;

  return <DetailBody key={id} data={data} onBack={onBack} />;
}

function DetailBody({ data, onBack }: { data: GovernanceItemDetailDto; onBack: () => void }) {
  const it = data.item;
  const changeStatus = useChangeGovernanceItemStatus();
  const addComment = useAddGovernanceItemComment();
  const update = useUpdateGovernanceItem();

  const [editing, setEditing] = useState(false);
  const [newStatus, setNewStatus] = useState<GovernanceItemStatus>(it.status);
  const [statusNote, setStatusNote] = useState('');
  const [resolution, setResolution] = useState('');
  const [comment, setComment] = useState('');
  const [isFollowUp, setIsFollowUp] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const isClosing = newStatus === 'Resolved' || newStatus === 'Closed';

  const submitStatus = () => {
    setErr(null);
    if (newStatus === it.status) { setErr('اختر حالة مختلفة عن الحالية.'); return; }
    changeStatus.mutate(
      { id: it.id, req: { status: newStatus, note: statusNote.trim() || undefined, resolutionSummary: isClosing ? resolution.trim() || undefined : undefined } },
      { onSuccess: () => { setStatusNote(''); setResolution(''); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  const submitComment = () => {
    setErr(null);
    if (!comment.trim()) { setErr('نص التعليق مطلوب.'); return; }
    addComment.mutate(
      { id: it.id, req: { body: comment.trim(), isFollowUp } },
      { onSuccess: () => { setComment(''); setIsFollowUp(false); }, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{it.title}</h1>
        <Badge tone={governanceSeverityTone(it.severity)}>{governanceSeverityLabel[it.severity]}</Badge>
        <Badge tone={governanceItemStatusTone(it.status)}>{governanceItemStatusLabel[it.status]}</Badge>
        <Badge tone="navy">{governanceCategoryLabel[it.category]}</Badge>
        {data.canEdit && !editing && (
          <Button variant="ghost" onClick={() => setEditing(true)}>تعديل البيانات</Button>
        )}
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      {editing ? (
        <EditItemForm data={data} update={update} onDone={() => setEditing(false)} onError={setErr} />
      ) : (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">التفاصيل</h2>
          <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <Detail label="التصنيف" value={governanceCategoryLabel[it.category]} />
            <Detail label="الخطورة" value={governanceSeverityLabel[it.severity]} />
            <Detail label="الحالة" value={governanceItemStatusLabel[it.status]} />
            <Detail label="نطاق التطبيق" value={governanceApplicationScopeLabel[it.applicationScope]} />
            <Detail label="المُسنَد إليه" value={it.assignedToName ?? '—'} />
            <Detail label="الموظّف المرتبط" value={it.relatedUserName ?? '—'} />
            <Detail label="الإدارة" value={it.departmentName ?? '—'} />
            <Detail label="الفريق" value={it.teamName ?? '—'} />
            <Detail label="تاريخ الاستحقاق" value={it.dueDate ? formatDate(it.dueDate) : '—'} />
            <Detail label="أنشأه" value={it.createdByName ?? '—'} />
            <Detail label="تاريخ الإنشاء" value={formatDateTime(it.createdAtUtc)} />
          </div>
          {data.description && (
            <div className="mt-3">
              <Alert tone="navy">{data.description}</Alert>
            </div>
          )}
          {data.resolutionSummary && (
            <div className="mt-3">
              <p className="mb-1 text-xs text-ink-2">ملخّص المعالجة</p>
              <Alert tone="success">{data.resolutionSummary}</Alert>
            </div>
          )}
          {data.closedAtUtc && (
            <p className="mt-3 text-xs text-ink-2">
              أُغلق بواسطة {data.closedByName ?? '—'} · {formatDateTime(data.closedAtUtc)}
            </p>
          )}
        </Card>
      )}

      {/* تغيير الحالة */}
      {data.canChangeStatus && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">تغيير الحالة</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="الحالة الجديدة">
              <Select value={newStatus} onChange={(e) => setNewStatus(e.target.value as GovernanceItemStatus)}>
                {STATUSES.map((s) => <option key={s} value={s}>{governanceItemStatusLabel[s]}</option>)}
              </Select>
            </Field>
            <Field label="ملاحظة (اختياري)">
              <Input value={statusNote} onChange={(e) => setStatusNote(e.target.value)} placeholder="سبب/سياق تغيير الحالة…" />
            </Field>
            {isClosing && (
              <div className="md:col-span-2">
                <Field label="ملخّص المعالجة (اختياري)">
                  <Input value={resolution} onChange={(e) => setResolution(e.target.value)} placeholder="كيف عولج البند…" />
                </Field>
              </div>
            )}
          </div>
          <div className="mt-4">
            <Button disabled={changeStatus.isPending} onClick={submitStatus}>تحديث الحالة</Button>
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
                  <Badge tone="navy">{governanceItemUpdateTypeLabel[u.updateType]}</Badge>
                  <span className="font-medium text-ink">{u.authorName ?? '—'}</span>
                  <span>· {formatDateTime(u.createdAtUtc)}</span>
                  {u.oldStatus && u.newStatus && (
                    <span>
                      · {governanceItemStatusLabel[u.oldStatus]} ← {governanceItemStatusLabel[u.newStatus]}
                    </span>
                  )}
                </div>
                {u.body && <p className="mt-2 whitespace-pre-wrap text-sm text-ink">{u.body}</p>}
              </li>
            ))}
          </ol>
        )}

        {/* إضافة تعليق/متابعة */}
        <div className="mt-4 border-t border-line pt-4">
          <Field label="إضافة تعليق / متابعة">
            <Input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اكتب تعليقًا أو ملاحظة متابعة…" />
          </Field>
          <label className="mt-2 flex items-center gap-2 text-sm text-ink-2">
            <input type="checkbox" checked={isFollowUp} onChange={(e) => setIsFollowUp(e.target.checked)} />
            تسجيله كملاحظة متابعة
          </label>
          <div className="mt-3">
            <Button disabled={addComment.isPending} onClick={submitComment}>إضافة</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}

function EditItemForm({
  data,
  update,
  onDone,
  onError,
}: {
  data: GovernanceItemDetailDto;
  update: ReturnType<typeof useUpdateGovernanceItem>;
  onDone: () => void;
  onError: (m: string | null) => void;
}) {
  const it = data.item;
  const { hasAnyRole } = useAuth();
  const wide = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'CeoSupport');
  const directory = useGovernanceWorkspaceDirectory();
  const submissions = useAllSubmissions();

  const [form, setForm] = useState<CreateGovernanceItemRequest>({
    title: it.title,
    description: data.description ?? '',
    category: it.category,
    severity: it.severity,
    applicationScope: it.applicationScope,
    assignedToUserId: it.assignedToUserId ?? undefined,
    departmentId: it.departmentId ?? undefined,
    teamId: it.teamId ?? undefined,
    relatedSubmissionId: it.relatedSubmissionId ?? undefined,
    relatedUserId: it.relatedUserId ?? undefined,
    dueDate: it.dueDate ?? undefined,
  });

  const set = <K extends keyof CreateGovernanceItemRequest>(k: K, v: CreateGovernanceItemRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const save = () => {
    onError(null);
    if (!form.title.trim()) { onError('العنوان مطلوب.'); return; }
    const scopeErr = scopeRefError(form);
    if (scopeErr) { onError(scopeErr); return; }
    update.mutate(
      {
        id: it.id,
        req: {
          ...form,
          title: form.title.trim(),
          description: form.description?.trim() || undefined,
          assignedToUserId: form.assignedToUserId || undefined,
          departmentId: form.departmentId || undefined,
          teamId: form.teamId || undefined,
          relatedSubmissionId: form.relatedSubmissionId || undefined,
          relatedUserId: form.relatedUserId || undefined,
          dueDate: form.dueDate || undefined,
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
        <Field label="التصنيف">
          <Select value={form.category} onChange={(e) => set('category', e.target.value as GovernanceCategory)}>
            {CATEGORIES.map((c) => <option key={c} value={c}>{governanceCategoryLabel[c]}</option>)}
          </Select>
        </Field>
        <Field label="الخطورة">
          <Select value={form.severity} onChange={(e) => set('severity', e.target.value as GovernanceSeverity)}>
            {SEVERITIES.map((s) => <option key={s} value={s}>{governanceSeverityLabel[s]}</option>)}
          </Select>
        </Field>
        <ScopeFields wide={wide} form={form} set={set} directory={directory} submissions={submissions} />
        <DirectorySelect
          label="المُسنَد إليه (اختياري)"
          dir={directory}
          options={(d) => d.users}
          getName={(u) => u.fullName}
          value={form.assignedToUserId ?? ''}
          onChange={(v) => set('assignedToUserId', v || undefined)}
          emptyOptionLabel="بدون"
        />
        <Field label="تاريخ الاستحقاق (اختياري)">
          <Input type="date" value={form.dueDate ?? ''} onChange={(e) => set('dueDate', e.target.value || undefined)} />
        </Field>
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

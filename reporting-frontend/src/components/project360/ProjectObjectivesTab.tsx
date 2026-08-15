// ======================================================================
// Project 360 — تبويب الأهداف (CPW-R3 · R2-W12 · D-07)
//
// **فصل القدرتين**: الإنشاء/التعديل/الحذف كتابة بنيويّة (`canManage`)، أمّا تغيير
// الحالة فكتابة تشغيليّة (`canOperate`) متاحة لقائد الفريق ومدير الحساب المسؤولَين.
// إخفاء الأزرار تنظيم بصريّ فقط — الرفض النهائيّ عند الخادم في الحالتين.
//
// **الوزن المطبَّع والتقدّم مشتقّان في الخادم**: يُعرَضان كما وصلا ولا يُعاد حسابهما.
// ======================================================================

import { useState } from 'react';
import { Badge, Button, Card, EmptyState, Field, Input, Select } from '../ui';
import { LoadingState } from '../states';
import { ActionButton, Project360QueryError, SectionHeading, type Project360Access } from './shared';
import { apiErrorMessage } from '../../lib/api';
import { deliverablePriorityLabel, formatDate } from '../../lib/format';
import {
  percentOrDash,
  projectObjectiveStatusLabel,
  projectObjectiveStatusTone,
} from '../../lib/project360Format';
import {
  useCreateProjectObjective,
  useDeleteProjectObjective,
  useProjectObjectives,
  useUpdateProjectObjective,
  useUpdateProjectObjectiveStatus,
} from '../../lib/useProject360';
import type { ProjectObjectiveDto, ProjectObjectiveStatus } from '../../types/project360';
import type { DeliverablePriority as Priority } from '../../types/api';

const STATUSES: ProjectObjectiveStatus[] = [
  'NotStarted',
  'InProgress',
  'AtRisk',
  'Completed',
  'Cancelled',
];
const PRIORITIES: Priority[] = ['Low', 'Medium', 'High', 'Urgent'];

type FormState = {
  name: string;
  description: string;
  priority: Priority;
  weight: string;
  startDate: string;
  dueDate: string;
  notes: string;
};

const EMPTY_FORM: FormState = {
  name: '',
  description: '',
  priority: 'Medium',
  weight: '0',
  startDate: '',
  dueDate: '',
  notes: '',
};

export function ProjectObjectivesTab({
  projectId,
  access,
}: {
  projectId: string;
  access: Project360Access;
}) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const query = useProjectObjectives(projectId, includeInactive);
  const create = useCreateProjectObjective(projectId);
  const update = useUpdateProjectObjective(projectId);
  const setStatus = useUpdateProjectObjectiveStatus(projectId);
  const remove = useDeleteProjectObjective(projectId);

  const [form, setForm] = useState<FormState | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  if (query.isLoading) return <LoadingState />;
  if (query.isError) return <Project360QueryError error={query.error} onRetry={() => query.refetch()} />;

  const items = query.data ?? [];
  const mutationError = create.error ?? update.error ?? setStatus.error ?? remove.error;

  function openCreate() {
    setEditingId(null);
    setForm({ ...EMPTY_FORM });
  }

  function openEdit(o: ProjectObjectiveDto) {
    setEditingId(o.id);
    setForm({
      name: o.name,
      description: o.description ?? '',
      priority: o.priority,
      weight: String(o.weight),
      startDate: o.startDate ?? '',
      dueDate: o.dueDate ?? '',
      notes: o.notes ?? '',
    });
  }

  function submit() {
    if (!form) return;
    const payload = {
      name: form.name.trim(),
      description: form.description || null,
      priority: form.priority,
      weight: Number(form.weight) || 0,
      startDate: form.startDate || null,
      dueDate: form.dueDate || null,
      notes: form.notes || null,
    };
    const done = { onSuccess: () => setForm(null) };
    if (editingId) update.mutate({ objectiveId: editingId, request: payload }, done);
    else create.mutate(payload, done);
  }

  return (
    <div className="space-y-4">
      <SectionHeading
        title="أهداف المشروع"
        action={
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2 text-sm text-ink-2">
              <input
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => setIncludeInactive(e.target.checked)}
              />
              إظهار غير النشطة
            </label>
            {access.canManage ? <Button onClick={openCreate}>هدف جديد</Button> : null}
          </div>
        }
      />

      {mutationError ? <p className="text-sm text-alert">{apiErrorMessage(mutationError)}</p> : null}

      {form ? (
        <Card>
          <h4 className="mb-3 text-sm font-semibold text-navy">
            {editingId ? 'تعديل الهدف' : 'هدف جديد'}
          </h4>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="الاسم">
              <Input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Field>
            <Field label="الأولويّة">
              <Select
                value={form.priority}
                onChange={(e) => setForm({ ...form, priority: e.target.value as Priority })}
              >
                {PRIORITIES.map((p) => (
                  <option key={p} value={p}>
                    {deliverablePriorityLabel[p]}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="الوزن" help="الوزن الخام؛ يطبّعه الخادم على مستوى المشروع.">
              <Input
                type="number"
                step="0.01"
                value={form.weight}
                onChange={(e) => setForm({ ...form, weight: e.target.value })}
              />
            </Field>
            <Field label="تاريخ البدء">
              <Input
                type="date"
                value={form.startDate}
                onChange={(e) => setForm({ ...form, startDate: e.target.value })}
              />
            </Field>
            <Field label="تاريخ الاستحقاق">
              <Input
                type="date"
                value={form.dueDate}
                onChange={(e) => setForm({ ...form, dueDate: e.target.value })}
              />
            </Field>
            <Field label="الوصف">
              <Input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </Field>
          </div>
          <div className="mt-4 flex gap-2">
            <ActionButton onClick={submit} busy={create.isPending || update.isPending}>
              حفظ
            </ActionButton>
            <Button variant="ghost" onClick={() => setForm(null)}>
              إلغاء
            </Button>
          </div>
        </Card>
      ) : null}

      {items.length === 0 ? (
        <EmptyState
          title="لا توجد أهداف"
          description="ابدأ بتسجيل أهداف المشروع لتتمكّن من ربط مؤشّرات الأداء بها."
        />
      ) : (
        <div className="space-y-3">
          {items.map((o) => (
            <Card key={o.id} className={o.isActive ? '' : 'opacity-60'}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h4 className="text-sm font-semibold text-navy">{o.name}</h4>
                    <Badge tone={projectObjectiveStatusTone(o.status)}>
                      {projectObjectiveStatusLabel[o.status]}
                    </Badge>
                    <Badge tone="muted">{deliverablePriorityLabel[o.priority]}</Badge>
                    {!o.isActive ? <Badge tone="muted">غير نشط</Badge> : null}
                  </div>
                  {o.description ? (
                    <p className="mt-1 text-sm text-ink-2">{o.description}</p>
                  ) : null}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {access.canOperate ? (
                    <Select
                      className="w-40"
                      aria-label={`حالة الهدف ${o.name}`}
                      value={o.status}
                      onChange={(e) =>
                        setStatus.mutate({
                          objectiveId: o.id,
                          request: { status: e.target.value as ProjectObjectiveStatus },
                        })
                      }
                    >
                      {STATUSES.map((s) => (
                        <option key={s} value={s}>
                          {projectObjectiveStatusLabel[s]}
                        </option>
                      ))}
                    </Select>
                  ) : null}
                  {access.canManage ? (
                    <>
                      <Button variant="ghost" onClick={() => openEdit(o)}>
                        تعديل
                      </Button>
                      <Button variant="danger" onClick={() => remove.mutate(o.id)}>
                        حذف
                      </Button>
                    </>
                  ) : null}
                </div>
              </div>

              <div className="mt-3 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-5">
                <Meta label="الوزن" value={String(o.weight)} />
                {/* الخادم يعيد الوزن المطبَّع في المدى 0..1؛ الضرب هنا تحويل وحدة عرض لا احتساب. */}
                <Meta label="الوزن المطبَّع" value={percentOrDash(o.normalizedWeight * 100)} />
                <Meta label="التقدّم" value={percentOrDash(o.progressPercent)} />
                <Meta label="المؤشّرات" value={`${o.computedKpiCount} / ${o.kpiCount}`} />
                <Meta label="الاستحقاق" value={formatDate(o.dueDate)} />
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-ink-2">{label}</p>
      <p className="font-medium text-ink">{value}</p>
    </div>
  );
}

// ======================================================================
// Project 360 — تبويب المخرَجات التعاقديّة (CPW-R3 · R2-W12 · §11)
//
// **التسمية فارقة لا تجميليّة**: هذه التزامات تعاقديّة تجاه العميل، وليست
// «مخرَجات مسار العمل» (Workstream Deliverables) القائمة في صفحة المشروع الأصليّة.
// خلط الاسمين في الواجهة يخلط سجلَّين مختلفين في ذهن المستخدم، فالعنوان صريح.
//
// **النوع من الكتالوج**: قائمة الأنواع تأتي من `GET /contract-deliverables/types`
// ولا يُثبَّت رمز واحد في الكود. وبعد الإنشاء لا يُعدَّل النوع (لقطة ثابتة).
// ======================================================================

import { useState } from 'react';
import { Badge, Button, Card, EmptyState, Field, Input, Select } from '../ui';
import { LoadingState } from '../states';
import { ActionButton, Project360QueryError, SectionHeading, type Project360Access } from './shared';
import { apiErrorMessage } from '../../lib/api';
import { deliverablePriorityLabel, formatDate } from '../../lib/format';
import {
  percentOrDash,
  projectDeliverableStatusLabel,
  projectDeliverableStatusTone,
} from '../../lib/project360Format';
import {
  useContractDeliverableTypes,
  useCreateContractDeliverable,
  useProjectContractDeliverables,
  useUpdateContractDeliverable,
  useUpdateContractDeliverableProgress,
} from '../../lib/useProject360';
import type {
  ProjectContractDeliverableDto,
  ProjectDeliverableStatus,
} from '../../types/project360';

const STATUSES: ProjectDeliverableStatus[] = [
  'NotStarted',
  'InProgress',
  'Delivered',
  'Delayed',
  'Cancelled',
];

export function ProjectContractDeliverablesTab({
  projectId,
  access,
}: {
  projectId: string;
  access: Project360Access;
}) {
  const list = useProjectContractDeliverables(projectId);
  const types = useContractDeliverableTypes(projectId);
  const create = useCreateContractDeliverable(projectId);
  const progress = useUpdateContractDeliverableProgress(projectId);
  const update = useUpdateContractDeliverable(projectId);

  const [showForm, setShowForm] = useState(false);
  const [typeCode, setTypeCode] = useState('');
  const [name, setName] = useState('');
  const [planned, setPlanned] = useState('1');
  const [dueDate, setDueDate] = useState('');
  const [weight, setWeight] = useState('0');

  if (list.isLoading) return <LoadingState />;
  if (list.isError) return <Project360QueryError error={list.error} onRetry={() => list.refetch()} />;

  const items = list.data ?? [];
  const typeOptions = types.data ?? [];
  const mutationError = create.error ?? progress.error ?? update.error;

  return (
    <div className="space-y-4">
      <SectionHeading
        title="المخرَجات التعاقديّة"
        action={
          access.canManage ? (
            <Button onClick={() => setShowForm((v) => !v)}>مخرَج تعاقديّ جديد</Button>
          ) : undefined
        }
      />
      <p className="text-xs text-ink-2">
        التزامات تعاقديّة تجاه العميل — مستقلّة عن مخرَجات مسارات العمل داخل صفحة المشروع.
      </p>

      {mutationError ? <p className="text-sm text-alert">{apiErrorMessage(mutationError)}</p> : null}

      {showForm ? (
        <Card>
          <div className="grid gap-4 md:grid-cols-4">
            <Field label="نوع المخرَج" help="من كتالوج المخرَجات التعاقديّة — لا يُعدَّل بعد الإنشاء.">
              <Select value={typeCode} onChange={(e) => setTypeCode(e.target.value)}>
                <option value="">اختر النوع…</option>
                {typeOptions.map((t) => (
                  <option key={t.code} value={t.code}>
                    {t.nameAr}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="الاسم" help="اتركه فارغًا لاستعمال اسم النوع.">
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </Field>
            <Field label="الكمّيّة المخطَّطة">
              <Input
                type="number"
                min={1}
                value={planned}
                onChange={(e) => setPlanned(e.target.value)}
              />
            </Field>
            <Field label="تاريخ الاستحقاق">
              <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </Field>
            <Field
              label="الوزن (%)"
              help="حصّة المخرَج من تقدّم المشروع. اتركه صفرًا فيتقاسم المخرَجات الوزن بالتساوي."
            >
              <Input
                type="number"
                min={0}
                max={100}
                value={weight}
                onChange={(e) => setWeight(e.target.value)}
              />
            </Field>
          </div>
          <div className="mt-4 flex gap-2">
            <ActionButton
              busy={create.isPending}
              disabled={!typeCode}
              onClick={() =>
                create.mutate(
                  {
                    deliverableTypeCode: typeCode,
                    name: name.trim() || null,
                    plannedQuantity: Number(planned) || 1,
                    dueDate: dueDate || null,
                    weightPercentage: Number(weight) || 0,
                  },
                  {
                    onSuccess: () => {
                      setShowForm(false);
                      setTypeCode('');
                      setName('');
                      setPlanned('1');
                      setDueDate('');
                      setWeight('0');
                    },
                  },
                )
              }
            >
              حفظ
            </ActionButton>
            <Button variant="ghost" onClick={() => setShowForm(false)}>
              إلغاء
            </Button>
          </div>
        </Card>
      ) : null}

      {items.length === 0 ? (
        <EmptyState
          title="لا توجد مخرَجات تعاقديّة"
          description="سجّل التزامات العقد ليظهر التزام المشروع بها في لوحة النظرة العامّة."
        />
      ) : (
        <div className="space-y-3">
          {items.map((d) => (
            <Card key={d.id}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h4 className="text-sm font-semibold text-navy">{d.name}</h4>
                    <Badge tone={projectDeliverableStatusTone(d.status)}>
                      {projectDeliverableStatusLabel[d.status]}
                    </Badge>
                    <Badge tone="muted">{d.deliverableTypeNameAr ?? d.deliverableTypeCode}</Badge>
                    <Badge tone="muted">{deliverablePriorityLabel[d.priority]}</Badge>
                  </div>
                  <p className="mt-1 text-xs text-ink-2">
                    التقدّم {percentOrDash(d.progressPercent)} · المنجَز {d.completedQuantity} من{' '}
                    {d.plannedQuantity} · الاستحقاق {formatDate(d.dueDate)} · الوزن{' '}
                    {d.weightPercentage > 0 ? `${d.weightPercentage}٪` : 'غير موزون'}
                  </p>
                </div>

                {access.canOperate || access.canManage ? (
                  <div className="flex flex-wrap items-end gap-2">
                    {access.canOperate ? (
                      <>
                    <Select
                      className="w-40"
                      aria-label={`حالة المخرَج ${d.name}`}
                      value={d.status}
                      onChange={(e) =>
                        progress.mutate({
                          deliverableId: d.id,
                          request: {
                            status: e.target.value as ProjectDeliverableStatus,
                            progressPercent: d.progressPercent,
                            completedQuantity: d.completedQuantity,
                          },
                        })
                      }
                    >
                      {STATUSES.map((s) => (
                        <option key={s} value={s}>
                          {projectDeliverableStatusLabel[s]}
                        </option>
                      ))}
                    </Select>
                    <ProgressEditor
                      value={d.progressPercent}
                      disabled={progress.isPending}
                      onCommit={(pct) =>
                        progress.mutate({
                          deliverableId: d.id,
                          request: {
                            status: d.status,
                            progressPercent: pct,
                            completedQuantity: d.completedQuantity,
                          },
                        })
                      }
                    />
                      </>
                    ) : null}
                    {access.canManage ? (
                      <WeightEditor
                        deliverable={d}
                        disabled={update.isPending}
                        onCommit={(pct) =>
                          update.mutate({
                            deliverableId: d.id,
                            // تعديل بنيويّ كامل: `PUT` يستبدل السجلّ، فإرسال الوزن وحده يمسح
                            // بقيّة الحقول إلى قيمها الافتراضيّة. تُعاد القيم الحاليّة كما هي.
                            request: {
                              name: d.name,
                              description: d.description,
                              objectiveId: d.objectiveId,
                              workstreamId: d.workstreamId,
                              plannedQuantity: d.plannedQuantity,
                              startDate: d.startDate,
                              dueDate: d.dueDate,
                              priority: d.priority,
                              ownerUserId: d.ownerUserId,
                              notes: d.notes,
                              sortOrder: d.sortOrder,
                              weightPercentage: pct,
                            },
                          })
                        }
                      />
                    ) : null}
                  </div>
                ) : null}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * تعديل وزن المخرَج بعد إنشائه — بلا هذا الحقل يبقى كلّ مخرَج قديم بوزن صفر إلى الأبد،
 * فلا يبلغ المشروع `Weighted` مهما ضُبِط في المخرَجات الجديدة (§6-1).
 */
function WeightEditor({
  deliverable,
  disabled,
  onCommit,
}: {
  deliverable: ProjectContractDeliverableDto;
  disabled?: boolean;
  onCommit: (pct: number) => void;
}) {
  const [draft, setDraft] = useState(String(deliverable.weightPercentage));
  return (
    <div className="flex items-end gap-2">
      <Field label="الوزن (%)">
        <Input
          className="w-24"
          type="number"
          min={0}
          max={100}
          aria-label={`وزن المخرَج ${deliverable.name}`}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
        />
      </Field>
      <Button
        variant="ghost"
        disabled={disabled || draft === String(deliverable.weightPercentage)}
        onClick={() => onCommit(Number(draft) || 0)}
      >
        تحديث الوزن
      </Button>
    </div>
  );
}

/** حقل تقدّم منفصل: الإرسال بالتأكيد لا بكلّ ضغطة مفتاح، وإلّا انهال على الخادم طلب لكلّ رقم. */
function ProgressEditor({
  value,
  disabled,
  onCommit,
}: {
  value: number;
  disabled?: boolean;
  onCommit: (pct: number) => void;
}) {
  const [draft, setDraft] = useState(String(value));
  return (
    <div className="flex items-end gap-2">
      <Field label="نسبة التقدّم">
        <Input
          className="w-24"
          type="number"
          min={0}
          max={100}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
        />
      </Field>
      <Button
        variant="ghost"
        disabled={disabled || draft === String(value)}
        onClick={() => onCommit(Number(draft) || 0)}
      >
        تحديث
      </Button>
    </div>
  );
}

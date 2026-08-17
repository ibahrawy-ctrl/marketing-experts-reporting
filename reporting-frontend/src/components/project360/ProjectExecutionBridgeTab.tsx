// ======================================================================
// Project 360 — تبويب جسر التنفيذ الهجين (P360-WF-R2 §10 · OPTION B)
//
// **من يرى ماذا**: النموذج يظهر لكلّ من يرى المشروع — المنفِّذ هو من يعرف ما أنجزه،
// وإخفاء النموذج عمّن لا يملك الكتابة كان سيُبقي تنفيذه خارج المشروع تمامًا. أمّا
// زرّا القبول والرفض فيظهران بـ`canReview` القادمة من الخادم لكلّ مقترح على حدة.
//
// **الرقم الحاليّ معروض دائمًا بجانب المُدَّعى**: المراجِع يقارن بالمعروض لا بالذاكرة،
// وقبولُ نسبةٍ أدنى ممّا بلغه المخرَج فعلًا قرارٌ لا يجوز أن يقع بالسهو.
//
// **لا تعديل مباشر للمخرَج من هنا**: هذا التبويب مسار الادّعاء والحسم فقط. الكتابة
// المباشرة تبقى في تبويب المخرَجات التعاقديّة لمن يملكها.
// ======================================================================

import { useState } from 'react';
import { Badge, Button, Card, EmptyState, Field, Input, Select } from '../ui';
import { LoadingState } from '../states';
import { ActionButton, Project360QueryError, SectionHeading, type Project360Access } from './shared';
import { apiErrorMessage } from '../../lib/api';
import { formatDateTime } from '../../lib/format';
import {
  executionProposalStatusLabel,
  executionProposalStatusTone,
  percentOrDash,
  projectDeliverableStatusLabel,
} from '../../lib/project360Format';
import {
  useCreateProjectExecutionProposal,
  useProjectContractDeliverables,
  useProjectExecutionProposals,
  useReviewProjectExecutionProposal,
} from '../../lib/useProject360';
import type {
  ExecutionProposalStatus,
  ProjectDeliverableStatus,
  ProjectExecutionProposalDto,
} from '../../types/project360';

/** الحالات القابلة للاقتراح — `Cancelled` مستبعَدة: إلغاء التزام تعاقديّ قرار إداريّ لا ادّعاء تنفيذ. */
const PROPOSABLE_STATUSES: ProjectDeliverableStatus[] = ['InProgress', 'Delivered', 'Delayed'];

const FILTERS: { value: ExecutionProposalStatus | 'all'; label: string }[] = [
  { value: 'Pending', label: 'بانتظار المراجعة' },
  { value: 'Accepted', label: 'المقبولة' },
  { value: 'Rejected', label: 'المرفوضة' },
  { value: 'all', label: 'الكلّ' },
];

export function ProjectExecutionBridgeTab({
  projectId,
  access,
}: {
  projectId: string;
  access: Project360Access;
}) {
  const [filter, setFilter] = useState<ExecutionProposalStatus | 'all'>('Pending');
  const list = useProjectExecutionProposals(projectId, filter === 'all' ? undefined : filter);
  const deliverables = useProjectContractDeliverables(projectId);
  const create = useCreateProjectExecutionProposal(projectId);
  const review = useReviewProjectExecutionProposal(projectId);

  const [showForm, setShowForm] = useState(false);
  const [deliverableId, setDeliverableId] = useState('');
  const [percent, setPercent] = useState('');
  const [status, setStatus] = useState<ProjectDeliverableStatus>('InProgress');
  const [note, setNote] = useState('');

  if (list.isLoading) return <LoadingState />;
  if (list.isError) return <Project360QueryError error={list.error} onRetry={() => list.refetch()} />;

  const items = list.data ?? [];
  const deliverableOptions = (deliverables.data ?? []).filter((d) => d.isActive);
  const mutationError = create.error ?? review.error;

  return (
    <div className="space-y-4">
      <SectionHeading
        title="تحديثات التنفيذ"
        action={
          <Button onClick={() => setShowForm((v) => !v)} disabled={deliverableOptions.length === 0}>
            تسجيل تنفيذ
          </Button>
        }
      />
      <p className="text-xs text-ink-2">
        سجّل ما نُفِّذ على مخرَج تعاقديّ ليراجعه المسؤول عن المشروع. النسبة لا تنتقل إلى المخرَج
        ولا إلى تقدّم المشروع إلّا بعد القبول.
      </p>

      {mutationError ? <p className="text-sm text-alert">{apiErrorMessage(mutationError)}</p> : null}

      {deliverableOptions.length === 0 ? (
        <p className="text-xs text-ink-2">
          لا يوجد مخرَج تعاقديّ نشط لتسجيل تنفيذ عليه — يُنشئه مدير المشروع في تبويب المخرَجات.
        </p>
      ) : null}

      {showForm && deliverableOptions.length > 0 ? (
        <Card>
          <div className="grid gap-4 md:grid-cols-3">
            <Field label="المخرَج التعاقديّ">
              <Select value={deliverableId} onChange={(e) => setDeliverableId(e.target.value)}>
                <option value="">اختر المخرَج…</option>
                {deliverableOptions.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name} — الحاليّ {percentOrDash(d.progressPercent)}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="النسبة بعد التنفيذ">
              <Input
                type="number"
                min={0}
                max={100}
                value={percent}
                onChange={(e) => setPercent(e.target.value)}
              />
            </Field>
            <Field label="الحالة المقترحة">
              <Select
                value={status}
                onChange={(e) => setStatus(e.target.value as ProjectDeliverableStatus)}
              >
                {PROPOSABLE_STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {projectDeliverableStatusLabel[s]}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="mt-4">
            <Field label="ما الذي نُفِّذ؟" help="وصف ما أُنجِز — هذا هو دليل المراجِع.">
              {/* `textarea` خام بنفس أسلوب تبويب الاستراتيجيّة — لا وجود لـ`Textarea` في `ui`،
                  وإضافته هناك كانت ستوسّع سطح مكوّن عامّ من أجل حقل واحد. */}
              <textarea
                className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </Field>
          </div>
          <div className="mt-4 flex gap-2">
            <ActionButton
              busy={create.isPending}
              disabled={!deliverableId || percent === ''}
              onClick={() =>
                create.mutate(
                  {
                    deliverableId,
                    proposedProgressPercent: Number(percent) || 0,
                    proposedStatus: status,
                    executionNote: note.trim() || null,
                  },
                  {
                    onSuccess: () => {
                      setShowForm(false);
                      setDeliverableId('');
                      setPercent('');
                      setStatus('InProgress');
                      setNote('');
                      setFilter('Pending');
                    },
                  },
                )
              }
            >
              إرسال للمراجعة
            </ActionButton>
            <Button variant="ghost" onClick={() => setShowForm(false)}>
              إلغاء
            </Button>
          </div>
        </Card>
      ) : null}

      <div className="flex flex-wrap gap-2" role="group" aria-label="ترشيح تحديثات التنفيذ">
        {FILTERS.map((f) => (
          <Button
            key={f.value}
            variant={filter === f.value ? 'primary' : 'ghost'}
            onClick={() => setFilter(f.value)}
          >
            {f.label}
          </Button>
        ))}
      </div>

      {items.length === 0 ? (
        <EmptyState
          title="لا توجد تحديثات تنفيذ"
          description="حين يسجّل أعضاء الفريق ما نفّذوه على المخرَجات التعاقديّة، تظهر هنا للمراجعة."
        />
      ) : (
        <div className="space-y-3">
          {items.map((p) => (
            <ProposalCard
              key={p.id}
              proposal={p}
              busy={review.isPending}
              onReview={(accept, reviewNote) =>
                review.mutate({ proposalId: p.id, request: { accept, reviewNote } })
              }
            />
          ))}
        </div>
      )}

      {!access.canOperate ? (
        <p className="text-xs text-ink-2">
          مراجعة التحديثات تتطلّب مسؤوليّة عن هذا المشروع — يمكنك تسجيل تنفيذك ومتابعة قراره.
        </p>
      ) : null}
    </div>
  );
}

function ProposalCard({
  proposal: p,
  busy,
  onReview,
}: {
  proposal: ProjectExecutionProposalDto;
  busy: boolean;
  onReview: (accept: boolean, reviewNote: string | null) => void;
}) {
  const [reviewNote, setReviewNote] = useState('');

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h4 className="text-sm font-semibold text-navy">{p.deliverableName}</h4>
            <Badge tone={executionProposalStatusTone(p.status)}>
              {executionProposalStatusLabel[p.status]}
            </Badge>
          </div>
          <p className="mt-1 text-xs text-ink-2">
            {/* الحاليّ ⟵ المُدَّعى: المقارنة معروضة كي لا تقع بالسهو. */}
            من {percentOrDash(p.deliverableCurrentProgressPercent)} إلى{' '}
            <span className="font-semibold text-ink">{percentOrDash(p.proposedProgressPercent)}</span>{' '}
            · الحالة المقترحة {projectDeliverableStatusLabel[p.proposedStatus]}
          </p>
          <p className="mt-1 text-xs text-ink-2">
            رفعه {p.proposedByFullName ?? '—'} · {formatDateTime(p.createdAtUtc)}
            {p.submissionId ? ' · مستشهدًا بتقرير' : ''}
          </p>
          {p.executionNote ? (
            <p className="mt-2 whitespace-pre-line text-sm text-ink">{p.executionNote}</p>
          ) : null}
        </div>
      </div>

      {p.status === 'Pending' && p.canReview ? (
        <div className="mt-3 border-t border-line pt-3">
          <Field label="ملاحظة المراجعة" help="مطلوبة عند الرفض — الرفض بلا سبب لا يترك ما يُصحَّح.">
            <Input value={reviewNote} onChange={(e) => setReviewNote(e.target.value)} />
          </Field>
          <div className="mt-3 flex flex-wrap gap-2">
            <ActionButton busy={busy} onClick={() => onReview(true, reviewNote.trim() || null)}>
              قبول وتطبيق على المخرَج
            </ActionButton>
            <ActionButton
              variant="ghost"
              busy={busy}
              disabled={!reviewNote.trim()}
              onClick={() => onReview(false, reviewNote.trim())}
            >
              رفض
            </ActionButton>
          </div>
        </div>
      ) : null}

      {p.status !== 'Pending' ? (
        <div className="mt-3 border-t border-line pt-3 text-xs text-ink-2">
          {executionProposalStatusLabel[p.status]} بواسطة {p.reviewedByFullName ?? '—'} ·{' '}
          {formatDateTime(p.reviewedAtUtc)}
          {p.status === 'Accepted' && p.previousProgressPercent !== null ? (
            // اللقطة معروضة لأنّ الأثر بلا «ما كان قبله» غير قابل للمراجعة ولا للعكس.
            <> · كانت النسبة قبل التطبيق {percentOrDash(p.previousProgressPercent)}</>
          ) : null}
          {p.reviewNote ? <p className="mt-1 text-ink">{p.reviewNote}</p> : null}
        </div>
      ) : null}
    </Card>
  );
}

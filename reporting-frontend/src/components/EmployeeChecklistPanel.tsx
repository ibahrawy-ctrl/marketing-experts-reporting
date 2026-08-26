// P2-HR-010 — قائمة خدمة الموظّف والالتزام داخل الملفّ الشامل.
//
// مبدآن يحكمان هذا الملفّ:
// (1) **لا اشتقاق في المتصفّح**: كلّ بند وحالته وعدّاده ومَن يلزمه الإجراء يصل محسومًا من
//     الخادم. البند المحجوب لا يصل أصلًا، فلا يوجد هنا شرط صلاحيّة ولا إخفاء بصريّ.
// (2) **المحسوب لا يُحرَّر هنا**: التصحيح موضعه المصدر (التقرير/التقييم/الواقعة)، ولذلك
//     لا يُرسَم أيّ عنصر تحرير على بند محسوب — والخادم يرفضه أيضًا بـ400 لو حدث.
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Badge, Card, EmptyState, StatCard } from './ui';
import { CardsSkeleton, QueryError } from './states';
import { formatDate, formatDateTime } from '../lib/format';
import { useEmployeeChecklist, useUpdateChecklistItem } from '../lib/useEmployeeChecklist';
import {
  CHECKLIST_MANUAL_STATUSES,
  CHECKLIST_STATUS_LABEL,
  type ChecklistItem,
  type ChecklistItemStatus,
} from '../types/checklist';

type Tone = 'navy' | 'success' | 'alert' | 'gold' | 'muted';

const STATUS_TONE: Record<ChecklistItemStatus, Tone> = {
  NotStarted: 'alert',
  InProgress: 'gold',
  Completed: 'success',
  NotApplicable: 'muted',
};

const SOURCE_LABEL: Record<string, string> = {
  Computed: 'محسوب من مصدره',
  Manual: 'يدويّ',
};

/** يستخرج رسالة الخادم كما هي؛ ولا يُخمَّن سبب المنع في المتصفّح. */
function serverMessage(error: unknown): string {
  const res = (error as { response?: { status?: number; data?: { message?: string } } })?.response;
  if (res?.data?.message) return res.data.message;
  if (res?.status === 403) return 'لا تملك صلاحيّة تحرير بنود قائمة الالتزام.';
  if (res?.status === 404) return 'البند غير متاح لك.';
  if (res?.status === 409) return 'تغيّر البند منذ آخر قراءة. أعد التحميل ثمّ حاول.';
  return 'تعذّر حفظ البند. أعد المحاولة.';
}

function ManualEditor({
  item,
  subject,
  subjectUserId,
}: {
  item: ChecklistItem;
  subject: string;
  subjectUserId: string;
}) {
  const [status, setStatus] = useState<ChecklistItemStatus>(item.status);
  const update = useUpdateChecklistItem(subject, subjectUserId);
  const fieldId = `checklist-status-${item.key}`;

  return (
    <div className="mt-3 flex flex-wrap items-end gap-2 border-t border-line/60 pt-3">
      <label htmlFor={fieldId} className="flex flex-col gap-1 text-xs text-ink-2">
        الحالة
        <select
          id={fieldId}
          className="rounded-lg border border-line bg-white px-3 py-2 text-sm text-navy focus:border-orange-500 focus:outline-none"
          value={status}
          onChange={(e) => setStatus(e.target.value as ChecklistItemStatus)}
        >
          {CHECKLIST_MANUAL_STATUSES.map((s) => (
            <option key={s} value={s}>
              {CHECKLIST_STATUS_LABEL[s]}
            </option>
          ))}
        </select>
      </label>
      <button
        type="button"
        className="rounded-lg bg-navy px-4 py-2 text-sm font-semibold text-white hover:bg-navy-700 disabled:opacity-60"
        disabled={update.isPending}
        onClick={() => update.mutate({ itemKey: item.key, payload: { status } })}
      >
        {update.isPending ? 'جارٍ الحفظ…' : 'حفظ البند'}
      </button>
      {update.isError && (
        <p role="alert" className="w-full text-xs text-red-700">
          {serverMessage(update.error)}
        </p>
      )}
      {update.isSuccess && !update.isPending && (
        <p role="status" className="w-full text-xs text-emerald-700">
          حُفِظ البند وسُجِّل في سجلّ التدقيق.
        </p>
      )}
    </div>
  );
}

function ItemCard({
  item,
  subject,
  subjectUserId,
}: {
  item: ChecklistItem;
  subject: string;
  subjectUserId: string;
}) {
  return (
    <li className="rounded-lg border border-line p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-medium text-ink">{item.titleAr}</span>
        <span className="flex flex-wrap items-center gap-2">
          {item.requiresMyAction && <Badge tone="alert">يلزمك إجراء</Badge>}
          <Badge tone={STATUS_TONE[item.status] ?? 'muted'}>{item.statusLabelAr}</Badge>
          <Badge tone="muted">{SOURCE_LABEL[item.source] ?? item.source}</Badge>
        </span>
      </div>

      <dl className="mt-2 grid gap-2 text-xs text-ink-2 sm:grid-cols-2 lg:grid-cols-4">
        {/* «غير منطبق» لا يُعرَض عدّادًا: صفرٌ هنا كان سيُقرأ إنجازًا. */}
        {item.status !== 'NotApplicable' && (
          <div>
            <dt>بنود مفتوحة</dt>
            <dd className="font-medium text-ink">{item.openCount}</dd>
          </div>
        )}
        <div>
          <dt>المسؤول</dt>
          <dd className="font-medium text-ink">{item.ownerFullName ?? '—'}</dd>
        </div>
        <div>
          <dt>تاريخ الاستحقاق</dt>
          <dd className="font-medium text-ink">{item.dueDate ? formatDate(item.dueDate) : '—'}</dd>
        </div>
        <div>
          <dt>آخر إجراء</dt>
          <dd className="font-medium text-ink">
            {item.lastActionAtUtc ? formatDateTime(item.lastActionAtUtc) : '—'}
          </dd>
        </div>
        {item.evidenceReference && (
          <div className="sm:col-span-2 lg:col-span-4">
            <dt>الإثبات</dt>
            <dd className="font-medium text-ink">{item.evidenceReference}</dd>
          </div>
        )}
      </dl>

      {item.sourceLink && (
        <Link className="mt-2 inline-block text-sm text-navy underline" to={item.sourceLink}>
          فتح المصدر{item.sourceKind ? ` (${item.sourceKind})` : ''}
        </Link>
      )}

      {item.source === 'Manual' && (
        <ManualEditor item={item} subject={subject} subjectUserId={subjectUserId} />
      )}
    </li>
  );
}

/** `subject` = معرّف الموظّف أو السلسلة `me`؛ لا يُشتقّ المعرّف في المتصفّح في وضع الذات. */
export function EmployeeChecklistPanel({ subject }: { subject: string }) {
  const { data, isLoading, isError, refetch } = useEmployeeChecklist(subject);

  if (isLoading) {
    return (
      <div role="status" aria-label="جارٍ تحميل قائمة الالتزام">
        <CardsSkeleton count={3} />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <QueryError
        onRetry={() => refetch()}
        title="تعذّر تحميل قائمة الالتزام"
        description="قد تكون القائمة خارج نطاق صلاحيتك، أو حدث خطأ مؤقّت. أعد المحاولة."
      />
    );
  }

  // المجموعات تُشتقّ من البنود الواصلة وحدها — لا قائمة مجموعات ثابتة تكشف ما لم يصل.
  const groups = data.items.reduce<Record<string, ChecklistItem[]>>((acc, item) => {
    (acc[item.groupAr] ??= []).push(item);
    return acc;
  }, {});

  const ratio = Math.round(data.summary.completionRatio * 100);

  return (
    <section id="emp360-checklist" aria-labelledby="emp360-checklist-title" tabIndex={-1} className="scroll-mt-24">
      <Card>
        <h3 id="emp360-checklist-title" className="mb-3 font-semibold text-navy">
          قائمة خدمة الموظّف والالتزام
        </h3>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <StatCard label="بنود منطبقة" value={data.summary.applicable} />
          <StatCard label="مكتملة" value={data.summary.completed} />
          <StatCard label="مفتوحة" value={data.summary.open} />
          <StatCard label="غير منطبقة" value={data.summary.notApplicable} />
          <StatCard label="نسبة الالتزام" value={`${ratio}%`} />
        </div>

        {data.summary.requiresMyAction > 0 && (
          <p className="mt-3 text-sm text-red-700">
            عليك إجراء في {data.summary.requiresMyAction} بندًا.
          </p>
        )}

        {data.items.length === 0 ? (
          <div className="mt-4">
            <EmptyState
              title="لا توجد بنود متاحة لك"
              description="لا يعني هذا خلوّ الملفّ من البنود؛ البنود خارج صلاحيّتك لا تُرسَل أصلًا."
            />
          </div>
        ) : (
          Object.entries(groups).map(([group, items]) => (
            <div key={group} className="mt-4">
              <h4 className="mb-2 text-sm font-semibold text-ink-2">{group}</h4>
              <ul className="space-y-2">
                {items.map((item) => (
                  <ItemCard
                    key={item.key}
                    item={item}
                    subject={subject}
                    subjectUserId={data.subjectUserId}
                  />
                ))}
              </ul>
            </div>
          ))
        )}
      </Card>
    </section>
  );
}

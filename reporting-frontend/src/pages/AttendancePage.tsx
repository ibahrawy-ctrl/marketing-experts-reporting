// P2-ATT-007 — سطح وقائع الحضور والالتزام (المُبلِّغ · الموظّف · الموارد البشريّة).
//
// مبدأ حاكم واحد يفسّر كلّ ما في هذا الملفّ: **الأزرار تُرسَم من `allowedActions` القادمة من
// الخادم**، ولا يوجد هنا أيّ شرط صلاحيّة محسوب في المتصفّح. إخفاء الزرّ ليس تخويلًا؛ الخادم
// يعيد التحقّق عند كلّ كتابة، ويردّ 404 لما هو خارج النطاق فلا يُسرَّب وجود واقعة ولا موظّف.
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { formatDate, formatDateTime } from '../lib/format';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { QueryError, TableSkeleton } from '../components/states';
import {
  useAttendanceIncident,
  useAttendanceIncidents,
  useAttendanceTypes,
  useCreateAttendanceIncident,
  useHrReviewAttendance,
  useRunAttendanceAction,
  useUploadAttendanceAttachment,
  type HrDecision,
} from '../lib/useAttendance';
import {
  ATTENDANCE_ACTIONS_REQUIRING_REASON,
  ATTENDANCE_ACTION_LABEL,
  type AttendanceAction,
  type AttendanceIncidentDetail,
  type AttendanceListFilter,
  type AttendanceListItem,
} from '../types/attendance';

type Tone = 'navy' | 'success' | 'alert' | 'gold' | 'muted';

/** لون الحالة — تمييز بصريّ فقط، والحسم الدلاليّ في `isOfficialIncident` القادم من الخادم. */
function statusTone(status: string, official: boolean): Tone {
  if (official) return 'alert';
  if (status === 'AwaitingEmployee' || status === 'AwaitingHr' || status === 'Corrected') return 'gold';
  if (status === 'Rejected' || status === 'Reconciled' || status === 'Withdrawn') return 'success';
  return 'muted';
}

/** الأفعال التي يملكها سطح المراجعة عبر نقطة `hr-review` الواحدة لا عبر مسار مستقلّ. */
const HR_ACTIONS: Partial<Record<AttendanceAction, HrDecision>> = {
  HrConfirm: 'Confirm',
  HrReject: 'Reject',
  HrCorrect: 'Correct',
  HrReconcile: 'Reconcile',
  Void: 'Void',
};

function errorMessage(err: unknown): string {
  const res = (err as { response?: { status?: number; data?: { detail?: string; title?: string } } })
    ?.response;
  if (res?.status === 404) return 'لا توجد واقعة مطابقة.';
  return res?.data?.detail ?? res?.data?.title ?? 'تعذّر تنفيذ الإجراء. حاول مرّة أخرى.';
}

// ═══════════════════════════════ نموذج تسجيل بلاغ ═══════════════════════════════

function ReportForm({ onDone }: { onDone: (id: string) => void }) {
  const types = useAttendanceTypes();
  const create = useCreateAttendanceIncident();
  const [form, setForm] = useState({
    subjectUserId: '',
    incidentTypeId: '',
    incidentDate: new Date().toISOString().slice(0, 10),
    startTime: '',
    returnTime: '',
    description: '',
  });
  const [error, setError] = useState<string | null>(null);

  const selected = types.data?.find((t) => t.id === form.incidentTypeId);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const created = await create.mutateAsync({
        subjectUserId: form.subjectUserId.trim(),
        incidentTypeId: form.incidentTypeId,
        incidentDate: form.incidentDate,
        startTime: form.startTime ? `${form.startTime}:00` : null,
        returnTime: form.returnTime ? `${form.returnTime}:00` : null,
        description: form.description.trim(),
        submitImmediately: true,
      });
      onDone(created.id);
    } catch (err) {
      setError(errorMessage(err));
    }
  }

  return (
    <Card className="p-4">
      <h3 className="mb-3 text-base font-semibold text-navy">تسجيل بلاغ حضور</h3>
      <p className="mb-4 text-sm text-ink-2">
        البلاغ ليس واقعة مؤكَّدة: يُشعَر به الموظّف ليردّ عليه، ثمّ تقرّر الموارد البشريّة. لا أثر
        ماليّ لأيّ من ذلك.
      </p>

      <form onSubmit={submit} className="grid gap-3 md:grid-cols-2" data-testid="attendance-report-form">
        <Field label="معرّف الموظّف">
          <Input
            required
            value={form.subjectUserId}
            aria-label="معرّف الموظّف"
            onChange={(e) => setForm({ ...form, subjectUserId: e.target.value })}
          />
        </Field>

        <Field label="نوع الواقعة">
          <Select
            required
            value={form.incidentTypeId}
            aria-label="نوع الواقعة"
            onChange={(e) => setForm({ ...form, incidentTypeId: e.target.value })}
          >
            <option value="">— اختر —</option>
            {(types.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="تاريخ الواقعة">
          <Input
            required
            type="date"
            value={form.incidentDate}
            aria-label="تاريخ الواقعة"
            onChange={(e) => setForm({ ...form, incidentDate: e.target.value })}
          />
        </Field>

        {selected?.requiresTimes && (
          <>
            <Field label="وقت البداية">
              <Input
                required
                type="time"
                value={form.startTime}
                aria-label="وقت البداية"
                onChange={(e) => setForm({ ...form, startTime: e.target.value })}
              />
            </Field>
            <Field label="وقت العودة">
              <Input
                required
                type="time"
                value={form.returnTime}
                aria-label="وقت العودة"
                onChange={(e) => setForm({ ...form, returnTime: e.target.value })}
              />
            </Field>
          </>
        )}

        <div className="md:col-span-2">
          <Field label="الوصف">
            <textarea
              required
              rows={3}
              aria-label="الوصف"
              className="w-full rounded-lg border border-line px-3 py-2 text-sm"
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
            />
          </Field>
        </div>

        {error && (
          <div className="md:col-span-2">
            <Alert tone="alert">{error}</Alert>
          </div>
        )}

        <div className="md:col-span-2">
          <Button type="submit" loading={create.isPending}>
            إرسال البلاغ
          </Button>
        </div>
      </form>
    </Card>
  );
}

// ═══════════════════════════════ الخطّ الزمنيّ ═══════════════════════════════

function Timeline({ detail }: { detail: AttendanceIncidentDetail }) {
  if (detail.events.length === 0) {
    return <p className="text-sm text-ink-2">لا توجد انتقالات مسجّلة بعد.</p>;
  }
  return (
    <ol className="space-y-2" data-testid="attendance-timeline">
      {detail.events.map((e) => (
        <li key={e.id} className="rounded-lg border border-line p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-medium text-navy">{e.action}</span>
            <span className="text-xs text-ink-3">{formatDateTime(e.createdAtUtc)}</span>
          </div>
          <p className="mt-1 text-ink-2">
            {e.fromStatus} ← {e.toStatus} · بواسطة {e.actorName}
          </p>
          {e.comment && <p className="mt-1 text-ink">{e.comment}</p>}
        </li>
      ))}
    </ol>
  );
}

// ═══════════════════════════════ لوحة التفاصيل والإجراءات ═══════════════════════════════

function DetailPanel({ id, onClose }: { id: string; onClose: () => void }) {
  const detail = useAttendanceIncident(id);
  const runAction = useRunAttendanceAction();
  const hrReview = useHrReviewAttendance();
  const upload = useUploadAttendanceAttachment();

  const [pending, setPending] = useState<AttendanceAction | null>(null);
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);

  if (detail.isLoading) return <TableSkeleton rows={5} cols={2} />;
  if (detail.isError) return <QueryError onRetry={() => detail.refetch()} />;

  const d = detail.data;
  if (!d) return <EmptyState title="لا توجد واقعة مطابقة." />;

  async function run(action: AttendanceAction) {
    if (!d) return;
    setError(null);
    try {
      const hrDecision = HR_ACTIONS[action];
      if (hrDecision) {
        await hrReview.mutateAsync({
          id: d.id,
          decision: hrDecision,
          note: text.trim() || undefined,
          concurrencyStamp: d.concurrencyStamp,
        });
      } else {
        await runAction.mutateAsync({
          id: d.id,
          action,
          concurrencyStamp: d.concurrencyStamp,
          text: text.trim(),
        });
      }
      setPending(null);
      setText('');
    } catch (err) {
      setError(errorMessage(err));
    }
  }

  function onAction(action: AttendanceAction) {
    setError(null);
    // الأفعال ذات الرواية تفتح حقل النصّ أوّلًا؛ لا قرار موثَّق بلا سبب مكتوب.
    if (ATTENDANCE_ACTIONS_REQUIRING_REASON.includes(action) || HR_ACTIONS[action]) {
      setPending(action);
      return;
    }
    void run(action);
  }

  return (
    // الغلاف موجود لأنّ `Card` لا يمرّر السمات الإضافيّة إلى الـDOM؛ توسيعها كان سيمسّ كلّ الصفحات.
    <section data-testid="attendance-detail" aria-label="تفاصيل الواقعة">
    <Card className="p-4">
      <div className="mb-3 flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="text-base font-semibold text-navy">{d.typeNameAr}</h3>
          <p className="text-sm text-ink-2">
            {d.subjectName} · {formatDate(d.incidentDate)}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge tone={statusTone(d.status, d.isOfficialIncident)}>{d.statusAr}</Badge>
          <Badge tone={d.isOfficialIncident ? 'alert' : 'muted'}>
            {d.isOfficialIncident ? 'واقعة مؤكَّدة' : 'بلاغ'}
          </Badge>
          <Button variant="ghost" onClick={onClose}>
            إغلاق
          </Button>
        </div>
      </div>

      <dl className="mb-4 grid gap-2 text-sm md:grid-cols-2">
        <div>
          <dt className="text-ink-3">مُقدِّم البلاغ</dt>
          <dd className="text-ink">{d.reportedByName}</dd>
        </div>
        <div>
          <dt className="text-ink-3">المدّة</dt>
          <dd className="text-ink">{d.durationMinutes != null ? `${d.durationMinutes} دقيقة` : '—'}</dd>
        </div>
        <div>
          <dt className="text-ink-3">المهلة</dt>
          <dd className={d.isOverdue ? 'text-alert' : 'text-ink'}>
            {d.slaDueAtUtc ? formatDateTime(d.slaDueAtUtc) : '—'}
            {d.isOverdue ? ' · متأخّرة' : ''}
          </dd>
        </div>
        <div>
          <dt className="text-ink-3">الإجراء التالي على</dt>
          <dd className="text-ink">{d.nextActorAr ?? '—'}</dd>
        </div>
        <div className="md:col-span-2">
          <dt className="text-ink-3">الوصف</dt>
          <dd className="text-ink">{d.description}</dd>
        </div>
        {/* ردّ الموظّف وملاحظة الموارد البشريّة لا يصلان إلّا لمن صرّح له الخادم — غيابهما حماية. */}
        {d.employeeResponse !== undefined && (
          <div className="md:col-span-2">
            <dt className="text-ink-3">ردّ الموظّف</dt>
            <dd className="text-ink">{d.employeeResponse}</dd>
          </div>
        )}
        {d.hrNote !== undefined && (
          <div className="md:col-span-2">
            <dt className="text-ink-3">ملاحظة الموارد البشريّة</dt>
            <dd className="text-ink">{d.hrNote}</dd>
          </div>
        )}
      </dl>

      {d.reconciliationSuggestions && d.reconciliationSuggestions.length > 0 && (
        <div className="mb-4">
          <h4 className="mb-2 text-sm font-semibold text-navy">اقتراحات مصالحة</h4>
          <Alert tone="navy">
            يوجد {d.reconciliationSuggestions.length} إجازة/استئذان معتمد يغطّي هذا التاريخ. الاقتراح
            لا يُغلِق الواقعة؛ القرار للموارد البشريّة.
          </Alert>
        </div>
      )}

      <div className="mb-4">
        <h4 className="mb-2 text-sm font-semibold text-navy">المرفقات</h4>
        {d.attachments.length === 0 ? (
          <p className="text-sm text-ink-2">لا مرفقات.</p>
        ) : (
          <ul className="space-y-1 text-sm">
            {d.attachments.map((a) => (
              <li key={a.id}>
                <a
                  className="text-navy underline"
                  href={`${import.meta.env.VITE_API_BASE_URL ?? '/api'}/attendance/${d.id}/attachments/${a.id}`}
                >
                  {a.fileName}
                </a>
              </li>
            ))}
          </ul>
        )}
        <label className="mt-2 block text-sm">
          <span className="mb-1 block text-ink-2">إرفاق دليل</span>
          <input
            type="file"
            aria-label="إرفاق دليل"
            className="text-sm"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void upload.mutateAsync({ id: d.id, file }).catch((err) => setError(errorMessage(err)));
            }}
          />
        </label>
      </div>

      <div className="mb-4">
        <h4 className="mb-2 text-sm font-semibold text-navy">الخطّ الزمنيّ</h4>
        <Timeline detail={d} />
      </div>

      {error && (
        <div className="mb-3">
          <Alert tone="alert">{error}</Alert>
        </div>
      )}

      {pending && (
        <div className="mb-3 space-y-2 rounded-lg border border-line p-3">
          <Field label={HR_ACTIONS[pending] ? 'ملاحظة القرار' : 'السبب'}>
            <textarea
              rows={2}
              aria-label="نصّ الإجراء"
              className="w-full rounded-lg border border-line px-3 py-2 text-sm"
              value={text}
              onChange={(e) => setText(e.target.value)}
            />
          </Field>
          <div className="flex gap-2">
            <Button
              loading={runAction.isPending || hrReview.isPending}
              onClick={() => void run(pending)}
            >
              تأكيد {ATTENDANCE_ACTION_LABEL[pending]}
            </Button>
            <Button variant="ghost" onClick={() => { setPending(null); setText(''); }}>
              تراجع
            </Button>
          </div>
        </div>
      )}

      {/* الأزرار كلّها من عقد القدرات الخادميّ — قائمة فارغة تعني: لا إجراء لك الآن. */}
      <div className="flex flex-wrap gap-2" data-testid="attendance-actions">
        {d.allowedActions.length === 0 && (
          <p className="text-sm text-ink-2">لا إجراء متاح لك على هذه الواقعة الآن.</p>
        )}
        {d.allowedActions.map((a) => (
          <Button
            key={a}
            variant={a === 'HrConfirm' || a === 'Void' ? 'danger' : 'ghost'}
            onClick={() => onAction(a)}
          >
            {ATTENDANCE_ACTION_LABEL[a] ?? a}
          </Button>
        ))}
      </div>
    </Card>
    </section>
  );
}

// ═══════════════════════════════ الصفحة ═══════════════════════════════

type TabId = 'mine' | 'queue' | 'all';

const TABS: { id: TabId; label: string }[] = [
  { id: 'mine', label: 'ما ينتظر إجرائي' },
  { id: 'queue', label: 'طابور المراجعة' },
  { id: 'all', label: 'كلّ الوقائع' },
];

export default function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const [tab, setTab] = useState<TabId>('mine');
  const [showForm, setShowForm] = useState(false);
  const [filters, setFilters] = useState<AttendanceListFilter>({ page: 1, pageSize: 25 });

  const selectedId = params.get('incident');
  const types = useAttendanceTypes();

  const effective: AttendanceListFilter = {
    ...filters,
    needsMyAction: tab === 'mine' || undefined,
    status: tab === 'queue' ? 'AwaitingHr' : filters.status,
  };

  const list = useAttendanceIncidents(effective);

  function select(id: string | null) {
    const next = new URLSearchParams(params);
    if (id) next.set('incident', id);
    else next.delete('incident');
    setParams(next, { replace: true });
  }

  return (
    <div className="space-y-4" dir="rtl">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">الحضور والالتزام</h1>
          <p className="text-sm text-ink-2">
            بلاغ ليس إدانة: الموظّف يردّ، والموارد البشريّة تقرّر، ولا أثر ماليّ في أيّ مسار.
          </p>
        </div>
        <Button onClick={() => setShowForm((v) => !v)}>
          {showForm ? 'إخفاء نموذج البلاغ' : 'تسجيل بلاغ'}
        </Button>
      </header>

      {showForm && (
        <ReportForm
          onDone={(id) => {
            setShowForm(false);
            select(id);
          }}
        />
      )}

      <nav className="flex flex-wrap gap-2" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => setTab(t.id)}
            className={`rounded-lg px-3 py-1.5 text-sm ${
              tab === t.id ? 'bg-navy text-white' : 'bg-navy-50 text-navy hover:bg-navy-100'
            }`}
          >
            {t.label}
          </button>
        ))}
      </nav>

      <Card className="p-3">
        <div className="grid gap-3 md:grid-cols-4">
          <Field label="النوع">
            <Select
              value={filters.incidentTypeId ?? ''}
              aria-label="تصفية بالنوع"
              onChange={(e) =>
                setFilters({ ...filters, incidentTypeId: e.target.value || undefined, page: 1 })
              }
            >
              <option value="">الكلّ</option>
              {(types.data ?? []).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nameAr}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="من تاريخ">
            <Input
              type="date"
              aria-label="من تاريخ"
              value={filters.fromDate ?? ''}
              onChange={(e) => setFilters({ ...filters, fromDate: e.target.value || undefined, page: 1 })}
            />
          </Field>
          <Field label="إلى تاريخ">
            <Input
              type="date"
              aria-label="إلى تاريخ"
              value={filters.toDate ?? ''}
              onChange={(e) => setFilters({ ...filters, toDate: e.target.value || undefined, page: 1 })}
            />
          </Field>
          <Field label="المتأخّرة فقط">
            <Select
              value={filters.overdueOnly ? 'yes' : 'no'}
              aria-label="المتأخّرة فقط"
              onChange={(e) => setFilters({ ...filters, overdueOnly: e.target.value === 'yes', page: 1 })}
            >
              <option value="no">الكلّ</option>
              <option value="yes">المتأخّرة عن مهلتها</option>
            </Select>
          </Field>
        </div>
      </Card>

      {list.isLoading && <TableSkeleton rows={6} cols={6} />}
      {list.isError && <QueryError onRetry={() => list.refetch()} />}

      {list.data && list.data.items.length === 0 && (
        <EmptyState
          title="لا توجد وقائع"
          description="لا توجد وقائع مطابقة لهذه المرشِّحات ضمن نطاق رؤيتك."
        />
      )}

      {list.data && list.data.items.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-right text-sm" data-testid="attendance-list">
            <thead className="bg-navy-50 text-navy">
              <tr>
                <th className="p-3 font-medium">الموظّف</th>
                <th className="p-3 font-medium">النوع</th>
                <th className="p-3 font-medium">التاريخ</th>
                <th className="p-3 font-medium">الحالة</th>
                <th className="p-3 font-medium">العمر</th>
                <th className="p-3 font-medium">الإجراء التالي</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((row: AttendanceListItem) => (
                <tr
                  key={row.id}
                  className="cursor-pointer border-t border-line hover:bg-offwhite"
                  onClick={() => select(row.id)}
                >
                  <td className="p-3">{row.subjectName}</td>
                  <td className="p-3">{row.typeNameAr}</td>
                  <td className="p-3">{formatDate(row.incidentDate)}</td>
                  <td className="p-3">
                    <Badge tone={statusTone(row.status, row.isOfficialIncident)}>{row.statusAr}</Badge>
                  </td>
                  <td className={`p-3 ${row.isOverdue ? 'text-alert' : ''}`}>
                    {row.ageingDays} يوم عمل
                  </td>
                  <td className="p-3">{row.nextActorAr ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="border-t border-line p-3 text-xs text-ink-2">
            إجماليّ الوقائع ضمن نطاقك ومرشِّحاتك: {list.data.totalCount}
          </div>
        </Card>
      )}

      {selectedId && <DetailPanel id={selectedId} onClose={() => select(null)} />}
    </div>
  );
}

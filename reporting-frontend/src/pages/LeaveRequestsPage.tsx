import { useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  leaveTypeLabel,
  leaveStatusLabelFor,
  leaveStatusTone,
  formatDate,
  formatDateTime,
} from '../lib/format';
import type {
  LeaveRequestDto,
  LeaveRequestListItemDto,
  LeaveRequestType,
  CreateLeaveRequestRequest,
} from '../types/api';

// أدوار المراجعة (تطابق Policies.LeaveReview بالخادم) — يظهر لها تبويب «بانتظار قراري».
// تشمل الموارد البشرية HR لأنها المعتمِد النهائي لطلبات الموظّفين العاديّين (الفرض الدقيق في الخادم).
const REVIEW_ROLES = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'HR'] as const;

type Tab = 'mine' | 'pending';

export default function LeaveRequestsPage() {
  // الحالة محفوظة في الرابط (?tab=&open=) لدعم الروابط العميقة.
  const [params, setParams] = useSearchParams();
  const { hasAnyRole } = useAuth();
  const canReview = hasAnyRole(...REVIEW_ROLES);

  const requested = params.get('tab');
  const tab: Tab = requested === 'pending' && canReview ? 'pending' : 'mine';
  const openId = params.get('open');

  const setTab = (t: Tab) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('tab', t); n.delete('open'); return n; });
  const open = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <LeaveDetail id={openId} onBack={back} />;

  const tabs: [Tab, string][] = [
    ['mine', 'طلباتي'],
    ...(canReview ? ([['pending', 'بانتظار قراري']] as [Tab, string][]) : []),
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">الإجازات والاستئذانات</h1>
        <p className="mt-1 text-sm text-ink-2">
          قدّم طلب إجازة أو استئذان وتابع اعتماده عبر قائد الفريق ثم المدير ثم الموارد البشرية. لا يُصبح
          الطلب رسميًّا ويؤثّر في تقاريرك إلا بعد الاعتماد النهائي.
        </p>
      </div>
      <div className="flex gap-2 border-b border-line">
        {tabs.map(([k, label]) => (
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
      {tab === 'mine' && <MineTab onOpen={open} />}
      {tab === 'pending' && <PendingTab onOpen={open} />}
    </div>
  );
}

// ===== تبويب «طلباتي» — إنشاء + قائمة طلبات الموظّف =====
function MineTab({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['leave-requests-mine'],
    queryFn: async () => (await api.get<LeaveRequestListItemDto[]>('/leave-requests/my')).data,
  });

  if (isLoading) return <LoadingState label="يتم تحميل طلباتك…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب طلباتك. أعد المحاولة." />;

  return (
    <div className="space-y-4">
      <CreateLeaveForm onCreated={onOpen} />
      <Card className="overflow-x-auto p-0">
        {(items ?? []).length === 0 ? (
          <div className="p-5">
            <EmptyState
              title="لا توجد طلبات بعد"
              description="أنشئ طلب إجازة أو استئذان من النموذج أعلاه. ستظهر طلباتك هنا مع حالتها في مسار الاعتماد."
            />
          </div>
        ) : (
          <LeaveTable items={items ?? []} onOpen={onOpen} />
        )}
      </Card>
    </div>
  );
}

function CreateLeaveForm({ onCreated }: { onCreated: (id: string) => void }) {
  const qc = useQueryClient();
  const [type, setType] = useState<LeaveRequestType>('Leave');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const reset = () => {
    setStartDate(''); setEndDate(''); setStartTime(''); setEndTime(''); setReason(''); setNotes('');
  };

  const create = useMutation({
    mutationFn: () => {
      // TimeOnly بالخادم يتوقّع «HH:mm:ss» — حقل الوقت يُرجع «HH:mm» فنُكمله.
      const t = (v: string) => (v ? (v.length === 5 ? `${v}:00` : v) : null);
      const body: CreateLeaveRequestRequest =
        type === 'Leave'
          ? { type, startDate, endDate: endDate || startDate, reason, notes: notes || null }
          : { type, startDate, startTime: t(startTime), endTime: t(endTime), reason, notes: notes || null };
      return api.post<LeaveRequestDto>('/leave-requests', body);
    },
    onSuccess: (res) => {
      reset();
      void qc.invalidateQueries({ queryKey: ['leave-requests-mine'] });
      onCreated(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const isLeave = type === 'Leave';
  const canSubmit = isLeave
    ? !!startDate && !!endDate && !!reason.trim()
    : !!startDate && !!startTime && !!endTime && !!reason.trim();

  return (
    <Card>
      <div className="mb-3">
        <div className="text-sm font-semibold text-navy">طلب جديد</div>
        <div className="text-xs text-navy/60">
          الإجازة تغطّي يومًا كاملًا أو أكثر؛ الاستئذان لجزء من يوم واحد (من وقت إلى وقت).
        </div>
      </div>
      {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        <Field label="النوع">
          <Select value={type} onChange={(e) => { setType(e.target.value as LeaveRequestType); setErr(null); }}>
            <option value="Leave">{leaveTypeLabel.Leave}</option>
            <option value="Permission">{leaveTypeLabel.Permission}</option>
          </Select>
        </Field>
        <Field label={isLeave ? 'تاريخ البداية' : 'التاريخ'}>
          <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </Field>
        {isLeave ? (
          <Field label="تاريخ النهاية">
            <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </Field>
        ) : (
          <>
            <Field label="من الساعة">
              <Input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
            </Field>
            <Field label="إلى الساعة">
              <Input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} />
            </Field>
          </>
        )}
      </div>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        <Field label="السبب">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="اذكر سبب الطلب…" />
        </Field>
        <Field label="ملاحظات (اختياري)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="أي تفاصيل إضافية…" />
        </Field>
      </div>
      <div className="mt-4">
        <Button disabled={!canSubmit || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
          إرسال الطلب
        </Button>
      </div>
    </Card>
  );
}

// ===== تبويب «بانتظار قراري» — قائمة الطلبات التي تنتظر مراجعة المستخدم =====
function PendingTab({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['leave-requests-pending'],
    queryFn: async () => (await api.get<LeaveRequestListItemDto[]>('/leave-requests/pending')).data,
  });
  if (isLoading) return <LoadingState label="يتم تحميل الطلبات بانتظار قرارك…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الطلبات بانتظار قرارك. أعد المحاولة." />;
  return (
    <Card className="overflow-x-auto p-0">
      {(items ?? []).length === 0 ? (
        <div className="p-5">
          <EmptyState
            title="لا توجد طلبات بانتظار قرارك"
            description="تظهر هنا طلبات الإجازة والاستئذان عندما تصل إلى خطوة مراجعتك ضمن نطاقك. لا حاجة لأي إجراء الآن."
          />
        </div>
      ) : (
        <LeaveTable items={items ?? []} onOpen={onOpen} showRequester />
      )}
    </Card>
  );
}

function LeaveTable({
  items,
  onOpen,
  showRequester,
}: {
  items: LeaveRequestListItemDto[];
  onOpen: (id: string) => void;
  showRequester?: boolean;
}) {
  return (
    <table className="w-full min-w-[720px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          {showRequester && <th className="px-3 py-2.5 font-semibold">مقدّم الطلب</th>}
          <th className="px-3 py-2.5 font-semibold">النوع</th>
          <th className="px-3 py-2.5 font-semibold">المدّة</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
          <th className="px-3 py-2.5 font-semibold">يؤثّر في التقارير؟</th>
          <th className="px-3 py-2.5 font-semibold">تاريخ التقديم</th>
          <th className="px-3 py-2.5 font-semibold"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((r) => (
          <tr
            key={r.id}
            onClick={() => onOpen(r.id)}
            className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
          >
            {showRequester && <td className="px-3 py-2.5 font-medium text-navy">{r.requesterName}</td>}
            <td className="px-3 py-2.5 text-ink-2">{leaveTypeLabel[r.type]}</td>
            <td className="px-3 py-2.5 text-ink-2">{durationLabel(r)}</td>
            <td className="px-3 py-2.5"><Badge tone={leaveStatusTone(r.status)}>{leaveStatusLabelFor(r.status, r.isHrRequest)}</Badge></td>
            <td className="px-3 py-2.5">
              {r.impactsReports ? <Badge tone="success">نعم</Badge> : <span className="text-ink-3">—</span>}
            </td>
            <td className="px-3 py-2.5 text-ink-2">{formatDate(r.createdAtUtc)}</td>
            <td className="px-3 py-2.5">
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(r.id); }}>عرض</Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// وصف مختصر للمدّة: الإجازة تعرض المدى بالأيام، الاستئذان يعرض الوقت من/إلى.
function durationLabel(r: { type: LeaveRequestType; startDate: string; endDate: string; startTime: string | null; endTime: string | null }): string {
  if (r.type === 'Permission') {
    return `${formatDate(r.startDate)} · ${formatTime(r.startTime)} — ${formatTime(r.endTime)}`;
  }
  return r.startDate === r.endDate ? formatDate(r.startDate) : `${formatDate(r.startDate)} — ${formatDate(r.endDate)}`;
}

function formatTime(t: string | null): string {
  if (!t) return '—';
  return t.slice(0, 5); // «HH:mm»
}

// ===== تفاصيل الطلب + الإجراءات حسب الخطوة الحالية =====
function LeaveDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { user, hasAnyRole } = useAuth();
  const canReview = hasAnyRole(...REVIEW_ROLES);
  const [comment, setComment] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const { data: r, isLoading, isError, refetch } = useQuery({
    queryKey: ['leave-request', id],
    queryFn: async () => (await api.get<LeaveRequestDto>(`/leave-requests/${id}`)).data,
  });

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ['leave-request', id] });
    void qc.invalidateQueries({ queryKey: ['leave-requests-mine'] });
    void qc.invalidateQueries({ queryKey: ['leave-requests-pending'] });
  };

  const cancel = useMutation({
    mutationFn: () => api.post(`/leave-requests/${id}/cancel`),
    onSuccess: () => invalidate(),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // الإجراءات: path نسبي تحت /leave-requests/{id}/… ؛ الرفض/الإعادة يتطلّبان سببًا.
  const decide = useMutation({
    mutationFn: (vars: { path: string; needsReason: boolean }) =>
      api.post(`/leave-requests/${id}/${vars.path}`, vars.needsReason ? { reason: comment } : { comment: comment || null }),
    onSuccess: () => { setComment(''); invalidate(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل الطلب…" />;
  if (isError || !r)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل الطلب" description="حدث خطأ أثناء جلب تفاصيل الطلب. أعد المحاولة." />;

  const isOwner = r.requesterUserId === user?.userId;
  // خطوة المراجعة الحالية ⇒ نقطة الاعتماد + الرفض المناسبة. المستخدم لا يراجع طلبه ولا يكرّر خطوة سبق أن راجعها (يفرضه الخادم أيضًا).
  // طلب الموارد البشرية يسلك مسارًا خاصًّا: المدير العام يراجع (نقطة المدير) ثم الإدارة العليا تعتمد نهائيًّا (نقطة الموارد البشرية).
  const reviewStep = r.isHrRequest
    ? (r.status === 'TeamLeaderApproved' ? { label: 'مراجعة المدير العام', approve: 'manager/approve', reject: 'manager/reject' }
      : r.status === 'ManagerApproved' ? { label: 'الاعتماد النهائي من الإدارة العليا', approve: 'hr/approve', reject: 'hr/reject' }
      : null)
    : (r.status === 'Submitted' ? { label: 'قرار قائد الفريق', approve: 'team-leader/approve', reject: 'team-leader/reject' }
      : r.status === 'TeamLeaderApproved' ? { label: 'قرار المدير', approve: 'manager/approve', reject: 'manager/reject' }
      : r.status === 'ManagerApproved' ? { label: 'القرار النهائي (الموارد البشرية)', approve: 'hr/approve', reject: 'hr/reject' }
      : null);
  const showReview = canReview && !isOwner && reviewStep != null;
  // المالك لا يعتمد طلبه الشخصي — رسالة توضيحية عندما يكون الطلب في خطوة مراجعة قائمة.
  const ownerCannotApprove = isOwner && reviewStep != null;
  // الإعادة للتعديل متاحة للمراجع في أي خطوة مراجعة قائمة.
  const canReturn = canReview && !isOwner && (r.status === 'Submitted' || r.status === 'TeamLeaderApproved' || r.status === 'ManagerApproved');

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{leaveTypeLabel[r.type]}</h1>
        <Badge tone={leaveStatusTone(r.status)}>{leaveStatusLabelFor(r.status, r.isHrRequest)}</Badge>
        {r.isHrRequest && <Badge tone="navy">طلب موارد بشرية</Badge>}
        {r.impactsReports && <Badge tone="success">معتمد — يؤثّر في التقارير</Badge>}
      </div>
      <p className="text-ink-2">
        <Link to={`/app/employee/${r.requesterUserId}`} className="text-navy hover:text-orange-600 hover:underline">
          {r.requesterName}
        </Link>
        {' · '}
        قُدّم {formatDate(r.createdAtUtc)}
      </p>

      {err && <Alert tone="alert">{err}</Alert>}

      <Card>
        <h2 className="mb-3 font-semibold text-navy">تفاصيل الطلب</h2>
        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Detail label="النوع" value={leaveTypeLabel[r.type]} />
          {r.type === 'Permission' ? (
            <>
              <Detail label="التاريخ" value={formatDate(r.startDate)} />
              <Detail label="الوقت" value={`${formatTime(r.startTime)} — ${formatTime(r.endTime)}`} />
            </>
          ) : (
            <>
              <Detail label="تاريخ البداية" value={formatDate(r.startDate)} />
              <Detail label="تاريخ النهاية" value={formatDate(r.endDate)} />
            </>
          )}
          <Detail label="السبب" value={r.reason} />
          {r.notes && <Detail label="ملاحظات" value={r.notes} />}
        </div>
        {(r.rejectionReason || r.returnReason) && (
          <div className="mt-3 space-y-2">
            {r.rejectionReason && <Alert tone="alert">سبب الرفض: {r.rejectionReason}</Alert>}
            {r.returnReason && <Alert tone="gold">سبب الإعادة للتعديل: {r.returnReason}</Alert>}
          </div>
        )}
      </Card>

      {/* المعتمِدون المسجَّلون — مسار طلب الموارد البشرية يختلف (مراجعة المدير العام ثم اعتماد الإدارة العليا). */}
      <Card>
        <h2 className="mb-1 font-semibold text-navy">{r.isHrRequest ? 'مسار اعتماد طلب الموارد البشرية' : 'المعتمِدون'}</h2>
        <p className="mb-3 text-xs text-ink-2">
          {r.isHrRequest
            ? 'لا يراجع مقدّم الطلب طلبه. المسار: مراجعة المدير العام ثم الاعتماد النهائي من الإدارة العليا.'
            : 'المسار: قائد الفريق ثم المدير ثم الاعتماد النهائي من الموارد البشرية.'}
        </p>
        {r.isHrRequest ? (
          <div className="grid gap-3 text-sm sm:grid-cols-2">
            <Detail label="المدير العام (مراجعة)" value={approverLine(r.managerReviewerName, r.managerDecisionAtUtc)} />
            <Detail label="الإدارة العليا (اعتماد نهائي)" value={approverLine(r.hrReviewerName, r.hrDecisionAtUtc)} />
          </div>
        ) : (
          <div className="grid gap-3 text-sm sm:grid-cols-3">
            <Detail label="قائد الفريق" value={approverLine(r.teamLeaderReviewerName, r.teamLeaderDecisionAtUtc)} />
            <Detail label="المدير" value={approverLine(r.managerReviewerName, r.managerDecisionAtUtc)} />
            <Detail label="الموارد البشرية" value={approverLine(r.hrReviewerName, r.hrDecisionAtUtc)} />
          </div>
        )}
      </Card>

      {/* المالك لا يعتمد طلبه الشخصي. */}
      {ownerCannotApprove && (
        <Alert tone="gold">لا يمكنك اعتماد طلبك الشخصي. سيتولّى المراجعة المعتمِدون وفق المسار أعلاه.</Alert>
      )}

      {/* إلغاء الطلب — للمالك قبل الاعتماد النهائي. */}
      {isOwner && r.canCancel && (
        <Card>
          <h2 className="mb-2 font-semibold text-navy">إلغاء الطلب</h2>
          <p className="mb-3 text-sm text-ink-2">يمكنك إلغاء طلبك ما دام لم يُعتمَد نهائيًّا.</p>
          <Button variant="danger" disabled={cancel.isPending} onClick={() => { setErr(null); cancel.mutate(); }}>
            إلغاء الطلب
          </Button>
        </Card>
      )}

      {/* إجراء المراجعة/الاعتماد حسب الخطوة. */}
      {(showReview || canReturn) && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">{reviewStep?.label ?? 'إجراء المراجعة'}</h2>
          <div className="mb-3">
            <Field label="ملاحظة / سبب" help="إلزامي عند الرفض أو الإعادة للتعديل">
              <Input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اكتب سبب القرار…" />
            </Field>
          </div>
          <div className="flex flex-wrap gap-2">
            {showReview && reviewStep && (
              <>
                <Button
                  disabled={decide.isPending}
                  onClick={() => { setErr(null); decide.mutate({ path: reviewStep.approve, needsReason: false }); }}
                >
                  اعتماد
                </Button>
                <Button
                  variant="danger"
                  disabled={decide.isPending || !comment.trim()}
                  title={!comment.trim() ? 'اكتب سبب الرفض أولًا' : undefined}
                  onClick={() => { setErr(null); decide.mutate({ path: reviewStep.reject, needsReason: true }); }}
                >
                  رفض
                </Button>
              </>
            )}
            {canReturn && (
              <Button
                variant="ghost"
                disabled={decide.isPending || !comment.trim()}
                title={!comment.trim() ? 'اكتب سبب الإعادة أولًا' : undefined}
                onClick={() => { setErr(null); decide.mutate({ path: 'return', needsReason: true }); }}
              >
                إعادة للتعديل
              </Button>
            )}
          </div>
          <p className="mt-2 text-xs text-ink-2">الاعتماد لا يتطلّب سببًا، لكن الرفض والإعادة يتطلّبان كتابة السبب.</p>
        </Card>
      )}

      {/* الخطّ الزمني — كل قرار: من، متى، الخطوة، التعليق. */}
      <Card>
        <h2 className="mb-3 font-semibold text-navy">الخطّ الزمني</h2>
        {r.timeline.length === 0 ? (
          <p className="text-sm text-ink-2">لا توجد أحداث بعد.</p>
        ) : (
          <ul className="space-y-3">
            {r.timeline.map((e) => (
              <li key={e.id} className="flex items-start gap-3">
                <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-orange" />
                <div>
                  <p className="text-sm text-ink">
                    <span className="font-medium text-navy">{e.actorName ?? '—'}</span>
                    {' · '}
                    {leaveStatusLabelFor(e.fromStatus, r.isHrRequest)} ← {leaveStatusLabelFor(e.toStatus, r.isHrRequest)}
                  </p>
                  {e.comment && <p className="text-sm text-ink-2">{e.comment}</p>}
                  <p className="text-xs text-ink-2">{formatDateTime(e.atUtc)}</p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-ink-2">{label}</p>
      <p className="font-medium text-ink whitespace-pre-wrap">{value}</p>
    </div>
  );
}

function approverLine(name: string | null, at: string | null): string {
  if (!name) return 'بانتظار';
  return at ? `${name} · ${formatDate(at)}` : name;
}

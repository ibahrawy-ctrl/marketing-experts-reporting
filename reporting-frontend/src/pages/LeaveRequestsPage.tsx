import { useMemo, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage, approvalErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useToast, POST_SUCCESS_NAV_DELAY_MS } from '../components/ActionResultToast';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  leaveTypeLabel,
  leaveStatusLabelFor,
  leaveStatusTone,
  leaveGovernanceDelayLabel,
  leaveGovernanceDelayTone,
  permissionShortfallResolutionLabel,
  formatDate,
  formatDateTime,
} from '../lib/format';
import type {
  LeaveRequestDto,
  LeaveRequestListItemDto,
  LeaveRequestType,
  CreateLeaveRequestRequest,
  MyBalancesDto,
  PermissionShortfallResolution,
  TeamLeaderPendingGovernanceResultDto,
  TeamLeaderPendingGovernanceItemDto,
  LeaveGovernanceDelayStatus,
} from '../types/api';

// أدوار المراجعة (تطابق Policies.LeaveReview بالخادم) — يظهر لها تبويب «بانتظار قراري».
// تشمل الموارد البشرية HR لأنها المعتمِد النهائي لطلبات الموظّفين العاديّين (الفرض الدقيق في الخادم).
const REVIEW_ROLES = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'HR'] as const;

// أدوار طابور الحوكمة (تطابق Policies.LeaveGovernanceRead / Roles.LeaveGovernanceReaders بالخادم).
// قراءة-فقط على مستوى الشركة — لا تمنح أيّ سلطة قرار. لا تشمل Manager/TeamLeader/Employee/Viewer.
// CeoSupport مُستبعَد اتّساقًا مع سياسة وحدة الإجازات القائمة (لا يملك رؤية تفاصيل أيّ طلب إجازة).
const GOVERNANCE_ROLES = ['Admin', 'CEO', 'GeneralManager', 'HR'] as const;

type Tab = 'mine' | 'pending' | 'governance';

export default function LeaveRequestsPage() {
  // الحالة محفوظة في الرابط (?tab=&open=) لدعم الروابط العميقة.
  const [params, setParams] = useSearchParams();
  const { hasAnyRole } = useAuth();
  const canReview = hasAnyRole(...REVIEW_ROLES);
  const canGovern = hasAnyRole(...GOVERNANCE_ROLES);

  const requested = params.get('tab');
  const tab: Tab =
    requested === 'pending' && canReview
      ? 'pending'
      : requested === 'governance' && canGovern
      ? 'governance'
      : 'mine';
  const openId = params.get('open');

  const setTab = (t: Tab) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('tab', t); n.delete('open'); return n; });
  const open = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  // فتح التفاصيل من تبويب الحوكمة = عرض قراءة-فقط: تُخفى كلّ أزرار القرار (اعتماد/رفض/إعادة/إلغاء)
  // كي لا يمنح سطح الحوكمة أيّ سلطة قرار ولو للأدوار التي تملكها من طابور «بانتظار قراري».
  if (openId) return <LeaveDetail id={openId} onBack={back} readOnly={tab === 'governance'} />;

  const tabs: [Tab, string][] = [
    ['mine', 'طلباتي'],
    ...(canReview ? ([['pending', 'بانتظار قراري']] as [Tab, string][]) : []),
    ...(canGovern ? ([['governance', 'معلّقة عند قادة الفرق']] as [Tab, string][]) : []),
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
      {tab === 'governance' && <GovernanceTab onOpen={open} />}
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

// عدد أيام الإجازة شاملةً الطرفين (يطابق حساب الخادم) من تاريخين بصيغة YYYY-MM-DD.
function inclusiveDays(start: string, end: string): number {
  if (!start || !end) return 0;
  const ms = Date.parse(`${end}T00:00:00Z`) - Date.parse(`${start}T00:00:00Z`);
  if (Number.isNaN(ms) || ms < 0) return 0;
  return Math.round(ms / 86400000) + 1;
}

// مدّة الاستئذان بالدقائق من حقلَي الوقت «HH:mm». تُرجع 0 إن نقص أحدهما أو كان الترتيب خاطئًا.
function permissionMinutes(start: string, end: string): number {
  if (!start || !end) return 0;
  const [sh, sm] = start.split(':').map(Number);
  const [eh, em] = end.split(':').map(Number);
  if ([sh, sm, eh, em].some(Number.isNaN)) return 0;
  const diff = (eh * 60 + em) - (sh * 60 + sm);
  return diff > 0 ? diff : 0;
}

function CreateLeaveForm({ onCreated }: { onCreated: (id: string) => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [type, setType] = useState<LeaveRequestType>('Leave');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);
  // نافذة إقرار الإجازة بدون راتب عند نقص الرصيد السنوي.
  const [confirmUnpaid, setConfirmUnpaid] = useState(false);
  // نافذة اختيار قرار الاستئذان عند تجاوز الرصيد الشهري + القرار المختار.
  const [confirmPermission, setConfirmPermission] = useState(false);
  const [permResolution, setPermResolution] = useState<PermissionShortfallResolution>('None');

  // رصيد الإجازات/الأذونات لفحص الكفاية قبل الإرسال (الفحص النهائي خادميّ — هذا للتجربة فقط).
  const { data: balances } = useQuery({
    queryKey: ['my-balances'],
    queryFn: async () => (await api.get<MyBalancesDto>('/me/balances')).data,
  });

  const reset = () => {
    setStartDate(''); setEndDate(''); setStartTime(''); setEndTime(''); setReason(''); setNotes('');
  };

  const create = useMutation({
    mutationFn: (vars: { acknowledged?: boolean; resolution?: PermissionShortfallResolution }) => {
      // TimeOnly بالخادم يتوقّع «HH:mm:ss» — حقل الوقت يُرجع «HH:mm» فنُكمله.
      const t = (v: string) => (v ? (v.length === 5 ? `${v}:00` : v) : null);
      const body: CreateLeaveRequestRequest =
        type === 'Leave'
          ? { type, startDate, endDate: endDate || startDate, reason, notes: notes || null, acknowledgedUnpaidDeduction: vars.acknowledged ?? false }
          : { type, startDate, startTime: t(startTime), endTime: t(endTime), reason, notes: notes || null, permissionShortfallResolution: vars.resolution ?? 'None' };
      return api.post<LeaveRequestDto>('/leave-requests', body);
    },
    onSuccess: async (res) => {
      reset();
      setConfirmUnpaid(false);
      setConfirmPermission(false);
      setPermResolution('None');
      // التسلسل: إبطال الكاش وانتظار تحديثه أولًا ⇒ Toast نجاح ⇒ بعد ~700ms انتقال لتفاصيل الطلب الجديد.
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['leave-requests-mine'] }),
        qc.invalidateQueries({ queryKey: ['my-balances'] }),
      ]);
      toast.success('✅ تم تقديم الطلب');
      setTimeout(() => onCreated(res.data.id), POST_SUCCESS_NAV_DELAY_MS);
    },
    onError: (e) => {
      // فشل استدعاء الـAPI ⇒ Toast (رسائل التحقّق المحلّية تبقى Alert سطريًّا).
      setConfirmUnpaid(false);
      setConfirmPermission(false);
      toast.error(apiErrorMessage(e));
    },
  });

  const isLeave = type === 'Leave';
  // حدّ مدّة الاستئذان: لا يتجاوز ساعتين (120 دقيقة). الأطول يلزمه طلب إجازة/غياب (يُفرَض خادميًّا أيضًا).
  const permTooLong = !isLeave && permissionMinutes(startTime, endTime) > 120;
  const canSubmit = (isLeave
    ? !!startDate && !!endDate && !!reason.trim()
    : !!startDate && !!startTime && !!endTime && !!reason.trim())
    && !permTooLong;

  // الإجازة ⇒ رصيد الإجازات السنوي (بالأيام). المطلوب (أيام شاملة) مقابل المتبقّي السنوي.
  const requestedDays = isLeave ? inclusiveDays(startDate, endDate || startDate) : 0;
  const annualRemaining = balances?.annualLeave.remaining ?? 0;
  const uncovered = isLeave ? Math.max(0, requestedDays - annualRemaining) : 0;
  const insufficient = isLeave && requestedDays > 0 && uncovered > 0;

  // الاستئذان ⇒ رصيد الأذونات الشهري (بالعدد). يوجد قيد شهري فقط إن أعاد الخادم رصيدًا شهريًّا متبقّيًا.
  // permissionRemainingThisMonth ≤ 0 ⇒ الإذن التالي يتجاوز الرصيد الشهري المتاح ⇒ يلزم اختيار قرار.
  const hasMonthlyLimit = !isLeave && balances?.permissionRemainingThisMonth != null;
  const monthlyRemaining = balances?.permissionRemainingThisMonth ?? 0;
  const monthlyUsed = balances?.permissionUsedThisMonth ?? 0;
  const monthlyLimit = balances?.permissionMonthlyLimit ?? 0;
  const permissionInsufficient = hasMonthlyLimit && monthlyRemaining <= 0;

  // عند الإرسال: نقص الرصيد السنوي للإجازة ⇒ نافذة الإقرار؛ تجاوز الرصيد الشهري للاستئذان ⇒ نافذة اختيار القرار.
  const handleSubmit = () => {
    setErr(null);
    if (permTooLong) {
      setErr('مدة الاستئذان لا يمكن أن تتجاوز ساعتين. إذا كانت المدة أطول، يرجى تقديم طلب إجازة/غياب وفق السياسة.');
      return;
    }
    if (insufficient) { setConfirmUnpaid(true); return; }
    if (permissionInsufficient) { setPermResolution('None'); setConfirmPermission(true); return; }
    create.mutate(isLeave ? { acknowledged: false } : { resolution: 'None' });
  };

  return (
    <Card>
      <div className="mb-3">
        <div className="text-sm font-semibold text-navy">طلب جديد</div>
        <div className="text-xs text-navy/60">
          {isLeave
            ? 'الإجازة تغطّي يومًا كاملًا أو أكثر وتخصم من رصيد الإجازات السنوي.'
            : 'الاستئذان لجزء من يوم واحد (من وقت إلى وقت) ويخصم من رصيد الاستئذانات الشهري.'}
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
      {permTooLong && (
        <div className="mt-3">
          <Alert tone="alert">
            مدة الاستئذان لا يمكن أن تتجاوز ساعتين. إذا كانت المدة أطول، يرجى تقديم طلب إجازة/غياب وفق السياسة.
          </Alert>
        </div>
      )}
      {insufficient && (
        <div className="mt-3">
          <Alert tone="alert">
            رصيد الإجازات السنوي المتاح لديك غير كافٍ لهذا الطلب (المطلوب {requestedDays} يوم، المتبقّي {annualRemaining} يوم،
            غير المغطّى {uncovered} يوم). عند الإرسال سيُطلب إقرارك باحتمال احتساب الأيام غير المغطّاة إجازةً بدون راتب.
          </Alert>
        </div>
      )}
      {permissionInsufficient && (
        <div className="mt-3">
          <Alert tone="alert">
            رصيد الاستئذانات الشهري المتاح لديك غير كافٍ لهذا الطلب (الحدّ الشهري {monthlyLimit}، المستخدَم هذا الشهر {monthlyUsed}،
            المتبقّي {Math.max(0, monthlyRemaining)}). عند الإرسال سيُطلب منك اختيار أحد قرارين قبل المتابعة.
          </Alert>
        </div>
      )}
      <div className="mt-4">
        <Button loading={create.isPending} disabled={create.isPending || !canSubmit} onClick={() => { if (create.isPending) return; handleSubmit(); }}>
          إرسال الطلب
        </Button>
      </div>

      {confirmUnpaid && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true">
          <Card className="w-full max-w-lg">
            <h2 className="mb-3 text-lg font-bold text-navy">تنبيه: رصيد الإجازات السنوي غير كافٍ</h2>
            <Alert tone="alert">
              أقرّ بأن رصيد الإجازات المتاح لدي غير كافٍ لهذا الطلب، وأعلم أنه في حال موافقة الإدارة قد تُحتسب
              الأيام غير المغطاة كإجازة بدون راتب وقد يتم خصمها من راتب نهاية الشهر.
            </Alert>
            <div className="mt-3 grid gap-2 text-sm text-ink-2 sm:grid-cols-3">
              <div>المطلوب: <span className="font-semibold text-navy">{requestedDays} يوم</span></div>
              <div>الرصيد السنوي المتاح: <span className="font-semibold text-navy">{annualRemaining} يوم</span></div>
              <div>غير المغطّى: <span className="font-semibold text-orange-600">{uncovered} يوم</span></div>
            </div>
            <div className="mt-4 flex flex-wrap justify-end gap-2">
              <Button variant="ghost" disabled={create.isPending} onClick={() => setConfirmUnpaid(false)}>
                إلغاء
              </Button>
              <Button variant="danger" loading={create.isPending} disabled={create.isPending} onClick={() => { if (create.isPending) return; create.mutate({ acknowledged: true }); }}>
                الاستمرار مع الإقرار
              </Button>
            </div>
          </Card>
        </div>
      )}

      {confirmPermission && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true">
          <Card className="w-full max-w-lg">
            <h2 className="mb-3 text-lg font-bold text-navy">تنبيه: رصيد الاستئذانات الشهري غير كافٍ</h2>
            <Alert tone="alert">
              هذا الطلب يتجاوز رصيد الاستئذانات الشهري المتاح لديك. اختر أحد الخيارين للمتابعة. لا يوجد أي خصم آلي
              من الراتب — القرار يُسجَّل للمراجعة الإدارية فقط.
            </Alert>
            <div className="mt-3 grid gap-2 text-sm text-ink-2 sm:grid-cols-3">
              <div>الحدّ الشهري: <span className="font-semibold text-navy">{monthlyLimit}</span></div>
              <div>المستخدَم هذا الشهر: <span className="font-semibold text-navy">{monthlyUsed}</span></div>
              <div>المتبقّي: <span className="font-semibold text-orange-600">{Math.max(0, monthlyRemaining)}</span></div>
            </div>
            <div className="mt-4 space-y-2">
              <label className="flex cursor-pointer items-start gap-2 rounded-md border border-line p-3 text-sm hover:bg-offwhite">
                <input
                  type="radio"
                  name="perm-resolution"
                  className="mt-1"
                  checked={permResolution === 'CompensateAfterHours'}
                  onChange={() => setPermResolution('CompensateAfterHours')}
                />
                <span className="text-ink">نعم، أتعهد بتعويض وقت الاستئذان بعد مواعيد العمل</span>
              </label>
              <label className="flex cursor-pointer items-start gap-2 rounded-md border border-line p-3 text-sm hover:bg-offwhite">
                <input
                  type="radio"
                  name="perm-resolution"
                  className="mt-1"
                  checked={permResolution === 'AdminOrPayrollReview'}
                  onChange={() => setPermResolution('AdminOrPayrollReview')}
                />
                <span className="text-ink">لا، أرغب في تقديم الطلب مع علمي بأنه قد تتم معالجته إداريًا أو ماليًا حسب قرار الإدارة، وأوافق على إقرار الخصم المالي عند اعتماده</span>
              </label>
            </div>
            <div className="mt-4 flex flex-wrap justify-end gap-2">
              <Button variant="ghost" disabled={create.isPending} onClick={() => { setConfirmPermission(false); setPermResolution('None'); }}>
                إلغاء
              </Button>
              <Button
                loading={create.isPending}
                disabled={create.isPending || permResolution === 'None'}
                title={permResolution === 'None' ? 'اختر أحد الخيارين أولًا' : undefined}
                onClick={() => { if (create.isPending) return; create.mutate({ resolution: permResolution }); }}
              >
                {permResolution === 'AdminOrPayrollReview'
                  ? 'إرسال الطلب مع إقرار الخصم المالي'
                  : permResolution === 'CompensateAfterHours'
                    ? 'إرسال الطلب مع التعهد بالتعويض'
                    : 'الاستمرار'}
              </Button>
            </div>
          </Card>
        </div>
      )}
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

// ===== تبويب «معلّقة عند قادة الفرق» (LEAVE-TL-PENDING-GOVERNANCE-R1) — قراءة-فقط =====
// نافذة حوكمة على مستوى الشركة تُظهِر الطلبات العالقة عند خطوة قائد الفريق (Status=Submitted, CurrentStep=TeamLeader).
// مستقلّ تمامًا عن «بانتظار قراري». لا يمنح أيّ سلطة قرار: لا اعتماد/رفض/إعادة توجيه/تعديل. الإجراءات المتاحة
// حصرًا: عرض التفاصيل، فتح ملف الموظّف، الانتقال للطلب، نسخ المعرّف، الفلاتر والترتيب.
const GOVERNANCE_PAGE_SIZE = 25;

function GovernanceTab({ onOpen }: { onOpen: (id: string) => void }) {
  const [page, setPage] = useState(1);
  const [teamLeaderId, setTeamLeaderId] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [requestType, setRequestType] = useState<'' | LeaveRequestType>('');
  const [delayStatus, setDelayStatus] = useState<'' | LeaveGovernanceDelayStatus>('');
  const [startDateFrom, setStartDateFrom] = useState('');
  const [startDateTo, setStartDateTo] = useState('');
  const [search, setSearch] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);

  // أيّ تغيير في الفلاتر يُعيد الترقيم للصفحة الأولى (يمنع صفحة فارغة بعد التضييق).
  function withReset<T>(setter: (v: T) => void) {
    return (v: T) => {
      setter(v);
      setPage(1);
    };
  }

  const qs = new URLSearchParams();
  qs.set('page', String(page));
  qs.set('pageSize', String(GOVERNANCE_PAGE_SIZE));
  if (teamLeaderId) qs.set('teamLeaderId', teamLeaderId);
  if (departmentId) qs.set('departmentId', departmentId);
  if (teamId) qs.set('teamId', teamId);
  if (requestType) qs.set('requestType', requestType);
  if (delayStatus) qs.set('delayStatus', delayStatus);
  if (startDateFrom) qs.set('startDateFrom', startDateFrom);
  if (startDateTo) qs.set('startDateTo', startDateTo);
  if (search.trim()) qs.set('search', search.trim());
  if (includeInactive) qs.set('includeInactive', 'true');

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: [
      'leave-governance-tl-pending',
      page,
      teamLeaderId,
      departmentId,
      teamId,
      requestType,
      delayStatus,
      startDateFrom,
      startDateTo,
      search.trim(),
      includeInactive,
    ],
    queryFn: async () =>
      (await api.get<TeamLeaderPendingGovernanceResultDto>(
        `/leave-requests/governance/team-leader-pending?${qs.toString()}`,
      )).data,
    // إبقاء النتيجة السابقة أثناء تغيير الفلاتر/الصفحة: تبقى الفلاتر ظاهرة بلا وميض شاشة تحميل.
    placeholderData: (prev) => prev,
  });

  // مصدر خيارات الفلاتر (قائد الفريق/الإدارة/الفريق) = نفس مسار الحوكمة القرائيّ بلا فلاتر،
  // كي لا نفتح سطح صلاحيات جديدًا على الدليل التنظيميّ.
  const { data: optionsData } = useQuery({
    queryKey: ['leave-governance-tl-pending-options'],
    queryFn: async () =>
      (await api.get<TeamLeaderPendingGovernanceResultDto>(
        '/leave-requests/governance/team-leader-pending?page=1&pageSize=500&includeInactive=true',
      )).data,
  });

  const options = useMemo(() => {
    const src = optionsData?.items ?? [];
    const leaders = new Map<string, string>();
    const departments = new Map<string, string>();
    const teams = new Map<string, string>();
    for (const i of src) {
      if (i.teamLeaderUserId) leaders.set(i.teamLeaderUserId, i.teamLeaderName ?? i.teamLeaderUserId);
      if (i.departmentId) departments.set(i.departmentId, i.departmentName ?? i.departmentId);
      if (i.teamId) teams.set(i.teamId, i.teamName ?? i.teamId);
    }
    const sort = (m: Map<string, string>) =>
      [...m.entries()].sort((a, b) => a[1].localeCompare(b[1], 'ar'));
    return { leaders: sort(leaders), departments: sort(departments), teams: sort(teams) };
  }, [optionsData]);

  if (isLoading) return <LoadingState label="يتم تحميل طابور الحوكمة…" />;
  if (isError)
    return (
      <QueryError
        onRetry={() => refetch()}
        description="حدث خطأ أثناء جلب طابور الطلبات المعلّقة عند قادة الفرق. أعد المحاولة."
      />
    );

  const c = data?.counters;
  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / GOVERNANCE_PAGE_SIZE));

  return (
    <div className="space-y-4">
      <Alert tone="navy">
        عرض حوكمة قراءة-فقط على مستوى الشركة للطلبات العالقة عند خطوة قائد الفريق. لا يمنح هذا العرض أيّ سلطة
        قرار — لا اعتماد ولا رفض ولا إعادة توجيه. للمتابعة والرصد فقط.
      </Alert>

      {c && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <CounterCard label="الإجمالي المعلّق" value={c.totalPending} tone="navy" />
          <CounterCard label="يحتاج متابعة" value={c.attention} tone="gold" />
          <CounterCard label="موعد الإجازة بدأ" value={c.critical} tone="gold" />
          <CounterCard label="انتهت دون قرار" value={c.expiredUnresolved} tone="alert" />
          <CounterCard label="بلا قائد فريق" value={c.missingTeamLeader} tone="alert" />
          <CounterCard label="أقدم طلب (يوم)" value={c.oldestPendingDays} tone="muted" />
        </div>
      )}

      <Card className="p-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="قائد الفريق">
            <Select value={teamLeaderId} onChange={(e) => withReset(setTeamLeaderId)(e.target.value)}>
              <option value="">الكل</option>
              {options.leaders.map(([id, name]) => (
                <option key={id} value={id}>{name}</option>
              ))}
            </Select>
          </Field>
          <Field label="الإدارة">
            <Select value={departmentId} onChange={(e) => withReset(setDepartmentId)(e.target.value)}>
              <option value="">الكل</option>
              {options.departments.map(([id, name]) => (
                <option key={id} value={id}>{name}</option>
              ))}
            </Select>
          </Field>
          <Field label="الفريق">
            <Select value={teamId} onChange={(e) => withReset(setTeamId)(e.target.value)}>
              <option value="">الكل</option>
              {options.teams.map(([id, name]) => (
                <option key={id} value={id}>{name}</option>
              ))}
            </Select>
          </Field>
          <Field label="النوع">
            <Select
              value={requestType}
              onChange={(e) => withReset(setRequestType)(e.target.value as '' | LeaveRequestType)}
            >
              <option value="">الكل</option>
              <option value="Leave">إجازة</option>
              <option value="Permission">استئذان</option>
            </Select>
          </Field>
          <Field label="تصنيف التأخّر">
            <Select
              value={delayStatus}
              onChange={(e) => withReset(setDelayStatus)(e.target.value as '' | LeaveGovernanceDelayStatus)}
            >
              <option value="">الكل</option>
              <option value="Pending">معلّق</option>
              <option value="Attention">يحتاج متابعة</option>
              <option value="Critical">موعد الإجازة بدأ</option>
              <option value="ExpiredUnresolved">انتهت الإجازة دون قرار</option>
            </Select>
          </Field>
          <Field label="بداية الإجازة من">
            <Input type="date" value={startDateFrom} onChange={(e) => withReset(setStartDateFrom)(e.target.value)} />
          </Field>
          <Field label="بداية الإجازة إلى">
            <Input type="date" value={startDateTo} onChange={(e) => withReset(setStartDateTo)(e.target.value)} />
          </Field>
          <Field label="بحث (اسم الموظّف / قائد الفريق)">
            <Input value={search} onChange={(e) => withReset(setSearch)(e.target.value)} placeholder="اكتب للبحث…" />
          </Field>
          <Field label="خيارات">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => withReset(setIncludeInactive)(e.target.checked)}
              />
              تضمين غير النشطين
            </label>
          </Field>
        </div>
      </Card>

      <Card className="overflow-x-auto p-0">
        {items.length === 0 ? (
          <div className="p-5">
            <EmptyState
              title="لا توجد طلبات معلّقة عند قادة الفرق"
              description="لا توجد حاليًّا طلبات إجازة أو استئذان عالقة عند خطوة قائد الفريق مطابقة للفلاتر."
            />
          </div>
        ) : (
          <GovernanceTable items={items} onOpen={onOpen} />
        )}
      </Card>

      {total > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="text-xs text-ink-2">
            صفحة {page} من {pageCount} — إجمالي {total} طلبًا
          </div>
          <div className="flex items-center gap-2">
            <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              السابق
            </Button>
            <Button
              variant="ghost"
              disabled={page >= pageCount}
              onClick={() => setPage((p) => Math.min(pageCount, p + 1))}
            >
              التالي
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function CounterCard({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone: 'navy' | 'gold' | 'alert' | 'muted';
}) {
  const toneCls: Record<typeof tone, string> = {
    navy: 'text-navy',
    gold: 'text-amber-600',
    alert: 'text-red-600',
    muted: 'text-ink-2',
  };
  return (
    <Card className="p-3 text-center">
      <div className={`text-2xl font-bold ${toneCls[tone]}`}>{value}</div>
      <div className="mt-1 text-xs text-ink-2">{label}</div>
    </Card>
  );
}

function GovernanceTable({
  items,
  onOpen,
}: {
  items: TeamLeaderPendingGovernanceItemDto[];
  onOpen: (id: string) => void;
}) {
  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">الموظّف</th>
          <th className="px-3 py-2.5 font-semibold">قائد الفريق</th>
          <th className="px-3 py-2.5 font-semibold">النوع</th>
          <th className="px-3 py-2.5 font-semibold">المدّة</th>
          <th className="px-3 py-2.5 font-semibold">التصنيف</th>
          <th className="px-3 py-2.5 font-semibold">أيام الانتظار</th>
          <th className="px-3 py-2.5 font-semibold">تاريخ التقديم</th>
          <th className="px-3 py-2.5 font-semibold"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((i) => (
          <tr key={i.requestId} className="border-b border-line last:border-0 hover:bg-offwhite">
            <td className="px-3 py-2.5">
              <Link
                to={`/app/employee/${i.employeeUserId}`}
                className="font-medium text-navy hover:text-orange-600 hover:underline"
              >
                {i.employeeName}
              </Link>
              {!i.isEmployeeActive && <span className="mr-2 text-xs text-red-600">(غير نشط)</span>}
              {i.departmentName && <div className="text-xs text-ink-3">{i.departmentName}</div>}
            </td>
            <td className="px-3 py-2.5 text-ink-2">
              {i.missingTeamLeaderAssignment ? (
                <Badge tone="alert">لا يوجد قائد فريق محدّد</Badge>
              ) : (
                <>
                  {i.teamLeaderUserId ? (
                    <Link
                      to={`/app/employee/${i.teamLeaderUserId}`}
                      className="text-navy hover:text-orange-600 hover:underline"
                    >
                      {i.teamLeaderName ?? '—'}
                    </Link>
                  ) : (
                    (i.teamLeaderName ?? '—')
                  )}
                  {!i.isTeamLeaderActive && <span className="mr-2 text-xs text-red-600">(غير نشط)</span>}
                </>
              )}
            </td>
            <td className="px-3 py-2.5 text-ink-2">{leaveTypeLabel[i.requestType]}</td>
            <td className="px-3 py-2.5 text-ink-2">
              {i.startDate === i.endDate ? formatDate(i.startDate) : `${formatDate(i.startDate)} — ${formatDate(i.endDate)}`}
            </td>
            <td className="px-3 py-2.5">
              <Badge tone={leaveGovernanceDelayTone(i.delayStatus)}>{leaveGovernanceDelayLabel[i.delayStatus]}</Badge>
            </td>
            <td className="px-3 py-2.5 text-ink-2">{i.daysPending}</td>
            <td className="px-3 py-2.5 text-ink-2">{formatDate(i.createdAtUtc)}</td>
            <td className="px-3 py-2.5">
              <div className="flex items-center justify-end gap-1">
                <Button variant="ghost" onClick={() => onOpen(i.requestId)}>عرض</Button>
                <Button
                  variant="ghost"
                  onClick={() => { void navigator.clipboard?.writeText(i.requestId); }}
                  title="نسخ معرّف الطلب"
                >
                  نسخ المعرّف
                </Button>
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
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
function LeaveDetail({
  id,
  onBack,
  readOnly = false,
}: {
  id: string;
  onBack: () => void;
  readOnly?: boolean;
}) {
  const qc = useQueryClient();
  const toast = useToast();
  const { user, hasAnyRole } = useAuth();
  // في سياق الحوكمة (readOnly) لا سلطة مراجعة إطلاقًا مهما كان دور المستخدم.
  const canReview = !readOnly && hasAnyRole(...REVIEW_ROLES);
  const [comment, setComment] = useState('');

  const { data: r, isLoading, isError, refetch } = useQuery({
    queryKey: ['leave-request', id],
    queryFn: async () => (await api.get<LeaveRequestDto>(`/leave-requests/${id}`)).data,
  });

  // يُعيد Promise حتى ننتظر اكتمال تحديث الكاش قبل الرجوع للقائمة (لا رجوع قبل تحديث البيانات).
  const invalidate = () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ['leave-request', id] }),
      qc.invalidateQueries({ queryKey: ['leave-requests-mine'] }),
      qc.invalidateQueries({ queryKey: ['leave-requests-pending'] }),
    ]);

  const cancel = useMutation({
    mutationFn: () => api.post(`/leave-requests/${id}/cancel`),
    // قرار نهائيّ: إبطال الكاش وانتظاره ⇒ Toast ⇒ بعد ~700ms رجوع تلقائيّ للقائمة.
    onSuccess: async () => { await invalidate(); toast.success('✅ تم إلغاء الطلب'); setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS); },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // الإجراءات: path نسبي تحت /leave-requests/{id}/… ؛ الرفض/الإعادة يتطلّبان سببًا.
  const decide = useMutation({
    mutationFn: (vars: { path: string; needsReason: boolean }) =>
      api.post(`/leave-requests/${id}/${vars.path}`, vars.needsReason ? { reason: comment } : { comment: comment || null }),
    onSuccess: async (_data, vars) => {
      const msg = vars.path === 'return'
        ? '✅ تم إرجاع الطلب للتعديل'
        : vars.path.endsWith('reject')
          ? '✅ تم رفض الطلب'
          : '✅ تم اعتماد الطلب';
      setComment('');
      // قرار نهائيّ: تحديث الكاش أولًا ⇒ Toast ⇒ رجوع تلقائيّ بعد ~700ms.
      await invalidate();
      toast.success(msg);
      setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
    },
    onError: (e) => toast.error(approvalErrorMessage(e)),
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

      {/* تنبيه تجاوز الرصيد — يظهر للمراجع/الإدارة. الإجازة ⇒ أيام سنوية غير مغطّاة قد تُحتسب بدون راتب.
          الاستئذان ⇒ تجاوز الرصيد الشهري مع قرار الموظّف (تعويض الوقت / معالجة إدارية-مالية). لا خصم آلي في الحالتين. */}
      {r.isPotentialUnpaidLeave && (
        <Alert tone="alert">
          {r.type === 'Leave' ? (
            <div className="space-y-2">
              <p className="font-semibold">
                هذا الطلب يتجاوز رصيد الإجازات السنوي المتاح للموظف. أقرّ الموظف بأنه في حال الموافقة قد تُحتسب
                الأيام غير المغطاة كإجازة بدون راتب وقد تُخصم من الراتب.
              </p>
              <div className="grid gap-3 text-sm sm:grid-cols-3">
                <Detail label="الرصيد السنوي وقت الطلب" value={r.balanceAtRequest != null ? `${r.balanceAtRequest} يوم` : '—'} />
                <Detail label="الأيام المطلوبة" value={r.requestedLeaveDays != null ? `${r.requestedLeaveDays} يوم` : '—'} />
                <Detail label="الأيام غير المغطّاة" value={r.uncoveredLeaveDays != null ? `${r.uncoveredLeaveDays} يوم` : '—'} />
              </div>
              {r.employeeAcknowledgedAtUtc && (
                <p className="text-xs">أقرّ الموظّف بذلك في {formatDate(r.employeeAcknowledgedAtUtc)}.</p>
              )}
            </div>
          ) : (
            <div className="space-y-2">
              <p className="font-semibold">
                {r.permissionShortfallResolution === 'CompensateAfterHours'
                  ? 'هذا الطلب يتجاوز رصيد الاستئذانات الشهري. الموظف أقرّ بتعويض وقت الاستئذان بعد مواعيد العمل.'
                  : 'هذا الطلب يتجاوز رصيد الاستئذانات الشهري، وقد يحتاج معالجة إدارية أو مالية حسب قرار الإدارة.'}
              </p>
              <div className="grid gap-3 text-sm sm:grid-cols-3">
                <Detail label="الرصيد الشهري وقت الطلب" value={r.balanceAtRequest != null ? `${r.balanceAtRequest}` : '—'} />
                <Detail label="الأذونات المطلوبة" value={r.requestedLeaveDays != null ? `${r.requestedLeaveDays}` : '—'} />
                <Detail label="غير المغطّى بالرصيد الشهري" value={r.uncoveredLeaveDays != null ? `${r.uncoveredLeaveDays}` : '—'} />
              </div>
              <Detail label="قرار الموظّف" value={permissionShortfallResolutionLabel[r.permissionShortfallResolution]} />
              {r.employeeAcknowledgedAtUtc && (
                <p className="text-xs">سجّل الموظّف قراره في {formatDate(r.employeeAcknowledgedAtUtc)}.</p>
              )}
            </div>
          )}
        </Alert>
      )}

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

      {/* إلغاء الطلب — للمالك قبل الاعتماد النهائي. مُستبعَد كليًّا في سياق الحوكمة (قراءة-فقط). */}
      {!readOnly && isOwner && r.canCancel && (
        <Card>
          <h2 className="mb-2 font-semibold text-navy">إلغاء الطلب</h2>
          <p className="mb-3 text-sm text-ink-2">يمكنك إلغاء طلبك ما دام لم يُعتمَد نهائيًّا.</p>
          <Button variant="danger" loading={cancel.isPending} disabled={cancel.isPending} onClick={() => { if (cancel.isPending) return; cancel.mutate(); }}>
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
                  loading={decide.isPending}
                  disabled={decide.isPending}
                  onClick={() => { if (decide.isPending) return; decide.mutate({ path: reviewStep.approve, needsReason: false }); }}
                >
                  اعتماد
                </Button>
                <Button
                  variant="danger"
                  loading={decide.isPending}
                  disabled={decide.isPending || !comment.trim()}
                  title={!comment.trim() ? 'اكتب سبب الرفض أولًا' : undefined}
                  onClick={() => { if (decide.isPending) return; decide.mutate({ path: reviewStep.reject, needsReason: true }); }}
                >
                  رفض
                </Button>
              </>
            )}
            {canReturn && (
              <Button
                variant="ghost"
                loading={decide.isPending}
                disabled={decide.isPending || !comment.trim()}
                title={!comment.trim() ? 'اكتب سبب الإعادة أولًا' : undefined}
                onClick={() => { if (decide.isPending) return; decide.mutate({ path: 'return', needsReason: true }); }}
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

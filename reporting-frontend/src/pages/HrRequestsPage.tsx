import { useRef, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage, downloadFile } from '../lib/api';
import { useAuth } from '../lib/auth';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  employeeServiceRequestTypeLabel,
  employeeServiceRequestStatusLabel,
  employeeServiceRequestStatusTone,
  preferredLanguageLabel,
  formatDate,
  formatDateTime,
} from '../lib/format';
import type {
  EmployeeServiceRequestDto,
  EmployeeServiceRequestListItemDto,
  EmployeeServiceRequestType,
  EmployeeServiceRequestStatus,
  PreferredLanguage,
  CreateEmployeeServiceRequest,
} from '../types/api';

// أدوار معالجة طلبات الموارد البشرية (تطابق Policies.HrRequestManagement بالخادم).
// يظهر لها تبويب «طلبات المعالجة». الفرض النهائي للصلاحية في الخادم.
const HR_ROLES = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'HR'] as const;

const REQUEST_TYPES: EmployeeServiceRequestType[] = [
  'HrLetter',
  'SalaryCertificate',
  'ExperienceCertificate',
  'BankLetter',
  'EmbassyLetter',
  'PersonalDataUpdate',
  'Other',
];

const STATUSES: EmployeeServiceRequestStatus[] = [
  'Submitted',
  'InReview',
  'Completed',
  'Rejected',
  'Cancelled',
];

// أنواع الطلبات التي يُتوقّع أن تُرفَق بخطاب نهائي (PDF). تُظهر تلميحًا للموارد البشرية فقط.
const LETTER_TYPES: EmployeeServiceRequestType[] = [
  'BankLetter',
  'SalaryCertificate',
  'ExperienceCertificate',
  'EmbassyLetter',
  'HrLetter',
];

const MAX_FINAL_DOCUMENT_BYTES = 10 * 1024 * 1024;

type Tab = 'mine' | 'manage';

export default function HrRequestsPage() {
  // الحالة محفوظة في الرابط (?tab=&open=) لدعم الروابط العميقة.
  const [params, setParams] = useSearchParams();
  const { hasAnyRole } = useAuth();
  const canManage = hasAnyRole(...HR_ROLES);

  const requested = params.get('tab');
  const tab: Tab = requested === 'manage' && canManage ? 'manage' : 'mine';
  const openId = params.get('open');

  const setTab = (t: Tab) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('tab', t); n.delete('open'); return n; });
  const open = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <RequestDetail id={openId} onBack={back} />;

  const tabs: [Tab, string][] = [
    ['mine', 'طلباتي'],
    ...(canManage ? ([['manage', 'طلبات المعالجة']] as [Tab, string][]) : []),
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">طلبات الموارد البشرية</h1>
        <p className="mt-1 text-sm text-ink-2">
          اطلب خطابًا أو شهادة (راتب، خبرة، خطاب بنكي/سفارة) أو تحديث بياناتك. تتولّى الموارد البشرية
          المعالجة وترفق المستند النهائي عند الإكمال. هذه الطلبات لا تؤثّر في رصيد إجازاتك.
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
      {tab === 'manage' && <ManageTab onOpen={open} />}
    </div>
  );
}

// ===== تبويب «طلباتي» — إنشاء + قائمة طلبات الموظّف =====
function MineTab({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['hr-requests-mine'],
    queryFn: async () => (await api.get<EmployeeServiceRequestListItemDto[]>('/employee-service-requests/my')).data,
  });

  if (isLoading) return <LoadingState label="يتم تحميل طلباتك…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب طلباتك. أعد المحاولة." />;

  return (
    <div className="space-y-4">
      <CreateRequestForm onCreated={onOpen} />
      <Card className="overflow-x-auto p-0">
        {(items ?? []).length === 0 ? (
          <div className="p-5">
            <EmptyState
              title="لا توجد طلبات بعد"
              description="أنشئ طلبًا من النموذج أعلاه. ستظهر طلباتك هنا مع حالتها لدى الموارد البشرية."
            />
          </div>
        ) : (
          <RequestTable items={items ?? []} onOpen={onOpen} />
        )}
      </Card>
    </div>
  );
}

function CreateRequestForm({ onCreated }: { onCreated: (id: string) => void }) {
  const qc = useQueryClient();
  const [requestType, setRequestType] = useState<EmployeeServiceRequestType>('HrLetter');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [preferredLanguage, setPreferredLanguage] = useState<PreferredLanguage>('Arabic');
  const [destinationEntity, setDestinationEntity] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const reset = () => { setTitle(''); setDescription(''); setDestinationEntity(''); };

  const create = useMutation({
    mutationFn: () => {
      const body: CreateEmployeeServiceRequest = {
        requestType,
        title: title.trim(),
        description: description.trim() || null,
        preferredLanguage,
        destinationEntity: destinationEntity.trim() || null,
      };
      return api.post<EmployeeServiceRequestDto>('/employee-service-requests', body);
    },
    onSuccess: (res) => {
      reset();
      void qc.invalidateQueries({ queryKey: ['hr-requests-mine'] });
      onCreated(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const canSubmit = !!title.trim();

  return (
    <Card>
      <div className="mb-3">
        <div className="text-sm font-semibold text-navy">طلب جديد</div>
        <div className="text-xs text-navy/60">
          اختر نوع الطلب واكتب عنوانًا واضحًا. أضف الجهة المستفيدة إن كان المستند موجّهًا لجهة محددة.
        </div>
      </div>
      {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        <Field label="نوع الطلب">
          <Select
            value={requestType}
            onChange={(e) => { setRequestType(e.target.value as EmployeeServiceRequestType); setErr(null); }}
          >
            {REQUEST_TYPES.map((t) => (
              <option key={t} value={t}>{employeeServiceRequestTypeLabel[t]}</option>
            ))}
          </Select>
        </Field>
        <Field label="لغة المستند">
          <Select
            value={preferredLanguage}
            onChange={(e) => setPreferredLanguage(e.target.value as PreferredLanguage)}
          >
            <option value="Arabic">{preferredLanguageLabel.Arabic}</option>
            <option value="English">{preferredLanguageLabel.English}</option>
          </Select>
        </Field>
        <Field label="الجهة المستفيدة (اختياري)">
          <Input
            value={destinationEntity}
            onChange={(e) => setDestinationEntity(e.target.value)}
            placeholder="مثال: بنك الراجحي…"
          />
        </Field>
      </div>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        <Field label="العنوان">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="عنوان مختصر للطلب…" />
        </Field>
        <Field label="تفاصيل (اختياري)">
          <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="أي تفاصيل إضافية…" />
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

// ===== تبويب «طلبات المعالجة» — قائمة كل الطلبات للموارد البشرية مع فلاتر =====
function ManageTab({ onOpen }: { onOpen: (id: string) => void }) {
  const [type, setType] = useState<EmployeeServiceRequestType | ''>('');
  const [status, setStatus] = useState<EmployeeServiceRequestStatus | ''>('');
  const [q, setQ] = useState('');

  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['hr-requests-manage', type, status, q],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (type) params.type = type;
      if (status) params.status = status;
      if (q.trim()) params.q = q.trim();
      return (await api.get<EmployeeServiceRequestListItemDto[]>('/employee-service-requests', { params })).data;
    },
  });

  return (
    <div className="space-y-4">
      <Card>
        <div className="grid gap-3 md:grid-cols-3">
          <Field label="بحث">
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="بالعنوان أو اسم الموظّف…" />
          </Field>
          <Field label="النوع">
            <Select value={type} onChange={(e) => setType(e.target.value as EmployeeServiceRequestType | '')}>
              <option value="">كل الأنواع</option>
              {REQUEST_TYPES.map((t) => (
                <option key={t} value={t}>{employeeServiceRequestTypeLabel[t]}</option>
              ))}
            </Select>
          </Field>
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as EmployeeServiceRequestStatus | '')}>
              <option value="">كل الحالات</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>{employeeServiceRequestStatusLabel[s]}</option>
              ))}
            </Select>
          </Field>
        </div>
      </Card>
      {isLoading ? (
        <LoadingState label="يتم تحميل الطلبات…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الطلبات. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(items ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد طلبات مطابقة"
                description="عدّل الفلاتر أعلاه. تظهر هنا كل طلبات الموظّفين بانتظار المعالجة أو المكتملة."
              />
            </div>
          ) : (
            <RequestTable items={items ?? []} onOpen={onOpen} showRequester />
          )}
        </Card>
      )}
    </div>
  );
}

function RequestTable({
  items,
  onOpen,
  showRequester,
}: {
  items: EmployeeServiceRequestListItemDto[];
  onOpen: (id: string) => void;
  showRequester?: boolean;
}) {
  return (
    <table className="w-full min-w-[720px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          {showRequester && <th className="px-3 py-2.5 font-semibold">مقدّم الطلب</th>}
          <th className="px-3 py-2.5 font-semibold">العنوان</th>
          <th className="px-3 py-2.5 font-semibold">النوع</th>
          <th className="px-3 py-2.5 font-semibold">اللغة</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
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
            <td className="px-3 py-2.5 font-medium text-navy">{r.title}</td>
            <td className="px-3 py-2.5 text-ink-2">{employeeServiceRequestTypeLabel[r.requestType]}</td>
            <td className="px-3 py-2.5 text-ink-2">{preferredLanguageLabel[r.preferredLanguage]}</td>
            <td className="px-3 py-2.5">
              <Badge tone={employeeServiceRequestStatusTone(r.status)}>{employeeServiceRequestStatusLabel[r.status]}</Badge>
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

// ===== تفاصيل الطلب + الإجراءات (المالك: إلغاء؛ الموارد البشرية: معالجة) =====
function RequestDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { user, hasAnyRole } = useAuth();
  const canManage = hasAnyRole(...HR_ROLES);
  const [comment, setComment] = useState('');
  const [hrComment, setHrComment] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const [uploadErr, setUploadErr] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data: r, isLoading, isError, refetch } = useQuery({
    queryKey: ['hr-request', id],
    queryFn: async () => (await api.get<EmployeeServiceRequestDto>(`/employee-service-requests/${id}`)).data,
  });

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ['hr-request', id] });
    void qc.invalidateQueries({ queryKey: ['hr-requests-mine'] });
    void qc.invalidateQueries({ queryKey: ['hr-requests-manage'] });
  };

  const cancel = useMutation({
    mutationFn: () => api.post(`/employee-service-requests/${id}/cancel`),
    onSuccess: () => invalidate(),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const startReview = useMutation({
    mutationFn: () => api.post(`/employee-service-requests/${id}/start-review`),
    onSuccess: () => invalidate(),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const addComment = useMutation({
    mutationFn: () => api.post(`/employee-service-requests/${id}/comment`, { comment: comment.trim() }),
    onSuccess: () => { setComment(''); invalidate(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const complete = useMutation({
    mutationFn: () =>
      api.post(`/employee-service-requests/${id}/complete`, {
        hrComment: hrComment.trim() || null,
      }),
    onSuccess: () => { setHrComment(''); invalidate(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // رفع الخطاب النهائي (PDF فقط، ≤ 10MB) — يُخزَّن خادميًّا بلا كشف للمسار الداخلي.
  const upload = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append('file', file);
      return api.post(`/employee-service-requests/${id}/final-document`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
    },
    onSuccess: () => { setUploadErr(null); if (fileInputRef.current) fileInputRef.current.value = ''; invalidate(); },
    onError: (e) => setUploadErr(apiErrorMessage(e)),
  });

  const onPickFile = (file: File | undefined) => {
    setUploadErr(null);
    if (!file) return;
    const isPdf = file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf');
    if (!isPdf) { setUploadErr('يجب أن يكون الملف بصيغة PDF فقط.'); return; }
    if (file.size <= 0) { setUploadErr('الملف فارغ. اختر ملف PDF صالحًا.'); return; }
    if (file.size > MAX_FINAL_DOCUMENT_BYTES) { setUploadErr('حجم الملف يتجاوز الحدّ الأقصى (10 ميجابايت).'); return; }
    upload.mutate(file);
  };

  const downloadFinal = async () => {
    setErr(null);
    try {
      await downloadFile(`/employee-service-requests/${id}/final-document`, r?.finalDocumentFileName || 'final-document.pdf');
    } catch (e) {
      setErr(apiErrorMessage(e));
    }
  };

  const reject = useMutation({
    mutationFn: () => api.post(`/employee-service-requests/${id}/reject`, { reason: rejectReason.trim() }),
    onSuccess: () => { setRejectReason(''); invalidate(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل الطلب…" />;
  if (isError || !r)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل الطلب" description="حدث خطأ أثناء جلب تفاصيل الطلب. أعد المحاولة." />;

  const isOwner = r.requesterUserId === user?.userId;
  // المعالجة متاحة للموارد البشرية على الطلبات غير المنتهية (لا مكتمل/مرفوض/ملغى).
  const isOpen = r.status === 'Submitted' || r.status === 'InReview';
  const showManage = canManage && isOpen;

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{r.title}</h1>
        <Badge tone={employeeServiceRequestStatusTone(r.status)}>{employeeServiceRequestStatusLabel[r.status]}</Badge>
        <Badge tone="navy">{employeeServiceRequestTypeLabel[r.requestType]}</Badge>
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
          <Detail label="النوع" value={employeeServiceRequestTypeLabel[r.requestType]} />
          <Detail label="لغة المستند" value={preferredLanguageLabel[r.preferredLanguage]} />
          {r.destinationEntity && <Detail label="الجهة المستفيدة" value={r.destinationEntity} />}
          {r.description && <Detail label="التفاصيل" value={r.description} />}
          {r.assignedToHrName && <Detail label="المسؤول من الموارد البشرية" value={r.assignedToHrName} />}
          {r.completedAtUtc && <Detail label="تاريخ الإكمال" value={formatDate(r.completedAtUtc)} />}
        </div>
        {(r.hrComment || r.rejectionReason) && (
          <div className="mt-3 space-y-2">
            {r.hrComment && <Alert tone="navy">تعليق الموارد البشرية: {r.hrComment}</Alert>}
            {r.rejectionReason && <Alert tone="alert">سبب الرفض: {r.rejectionReason}</Alert>}
          </div>
        )}
        {/* الخطاب النهائي — تحميل آمن (المالك أو الموارد البشرية)، بلا كشف للمسار الداخلي. */}
        <div className="mt-3 border-t border-line pt-3">
          {r.hasFinalDocument ? (
            <div className="flex flex-wrap items-center gap-3">
              <Alert tone="success">
                الخطاب النهائي جاهز{r.finalDocumentFileName ? `: ${r.finalDocumentFileName}` : ''}
                {r.finalDocumentUploadedAt ? ` · رُفع ${formatDateTime(r.finalDocumentUploadedAt)}` : ''}
              </Alert>
              <Button variant="ghost" onClick={() => void downloadFinal()}>تحميل الخطاب</Button>
            </div>
          ) : r.status === 'Completed' ? (
            <p className="text-sm text-ink-2">لم يتم إرفاق ملف نهائي بعد.</p>
          ) : null}
        </div>
      </Card>

      {/* إلغاء الطلب — للمالك قبل الإكمال/الرفض. */}
      {isOwner && r.canCancel && (
        <Card>
          <h2 className="mb-2 font-semibold text-navy">إلغاء الطلب</h2>
          <p className="mb-3 text-sm text-ink-2">يمكنك إلغاء طلبك ما دام لم يكتمل أو يُرفض.</p>
          <Button variant="danger" disabled={cancel.isPending} onClick={() => { setErr(null); cancel.mutate(); }}>
            إلغاء الطلب
          </Button>
        </Card>
      )}

      {/* معالجة الموارد البشرية. */}
      {showManage && (
        <Card>
          <h2 className="mb-3 font-semibold text-navy">معالجة الطلب</h2>
          {r.status === 'Submitted' && (
            <div className="mb-4">
              <p className="mb-2 text-sm text-ink-2">ابدأ المعالجة لتسجيل أنّك تتولّى هذا الطلب.</p>
              <Button disabled={startReview.isPending} onClick={() => { setErr(null); startReview.mutate(); }}>
                بدء المعالجة
              </Button>
            </div>
          )}

          <div className="mb-4 border-t border-line pt-4">
            <Field label="إضافة تعليق (لا يغيّر الحالة)">
              <Input value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اكتب تعليقًا للموظّف…" />
            </Field>
            <div className="mt-2">
              <Button
                variant="ghost"
                disabled={addComment.isPending || !comment.trim()}
                onClick={() => { setErr(null); addComment.mutate(); }}
              >
                إضافة تعليق
              </Button>
            </div>
          </div>

          {/* رفع الخطاب النهائي (PDF) قبل الإكمال. */}
          <div className="mb-4 border-t border-line pt-4">
            <h3 className="mb-2 text-sm font-semibold text-navy">الخطاب النهائي (PDF)</h3>
            {LETTER_TYPES.includes(r.requestType) && (
              <p className="mb-2 text-sm text-ink-2">يفضّل رفع الملف النهائي قبل إكمال الطلب.</p>
            )}
            {uploadErr && <div className="mb-2"><Alert tone="alert">{uploadErr}</Alert></div>}
            {r.hasFinalDocument && (
              <div className="mb-2 flex flex-wrap items-center gap-3">
                <Alert tone="success">
                  مرفوع{r.finalDocumentFileName ? `: ${r.finalDocumentFileName}` : ''}
                </Alert>
                <Button variant="ghost" onClick={() => void downloadFinal()}>تحميل</Button>
              </div>
            )}
            <input
              ref={fileInputRef}
              type="file"
              accept="application/pdf,.pdf"
              onChange={(e) => onPickFile(e.target.files?.[0])}
              disabled={upload.isPending}
              className="block w-full text-sm text-ink-2 file:ml-3 file:rounded-md file:border-0 file:bg-navy file:px-4 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-navy/90"
            />
            <p className="mt-1 text-xs text-ink-2">
              {upload.isPending ? 'جارٍ الرفع…' : 'PDF فقط، بحدّ أقصى 10 ميجابايت. يمكن استبدال الملف قبل الإكمال.'}
            </p>
          </div>

          <div className="mb-4 border-t border-line pt-4">
            <h3 className="mb-2 text-sm font-semibold text-navy">إكمال الطلب</h3>
            <Field label="تعليق الإكمال (اختياري)">
              <Input value={hrComment} onChange={(e) => setHrComment(e.target.value)} placeholder="ملاحظة نهائية…" />
            </Field>
            <div className="mt-2">
              <Button disabled={complete.isPending} onClick={() => { setErr(null); complete.mutate(); }}>
                إكمال الطلب
              </Button>
            </div>
          </div>

          <div className="border-t border-line pt-4">
            <h3 className="mb-2 text-sm font-semibold text-navy">رفض الطلب</h3>
            <Field label="سبب الرفض" help="إلزامي">
              <Input value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder="اكتب سبب الرفض…" />
            </Field>
            <div className="mt-2">
              <Button
                variant="danger"
                disabled={reject.isPending || !rejectReason.trim()}
                title={!rejectReason.trim() ? 'اكتب سبب الرفض أولًا' : undefined}
                onClick={() => { setErr(null); reject.mutate(); }}
              >
                رفض الطلب
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* الخطّ الزمني. */}
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
                    {employeeServiceRequestStatusLabel[e.fromStatus]} ← {employeeServiceRequestStatusLabel[e.toStatus]}
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

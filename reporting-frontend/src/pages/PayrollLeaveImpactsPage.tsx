import { useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  usePayrollImpacts,
  usePayrollImpact,
  useReviewPayrollImpact,
  type PayrollImpactFilter,
} from '../lib/usePayrollImpacts';
import { useHrDirectoryUsers, useHrDirectoryDepartments, useHrDirectoryTeams } from '../lib/useDirectory';
import { apiErrorMessage } from '../lib/api';
import {
  leaveTypeLabel,
  leaveRequestStatusLabel,
  permissionShortfallResolutionLabel,
  payrollImpactReviewStatusLabel,
  payrollImpactReviewStatusTone,
  payrollImpactTypeLabel,
  formatDate,
  formatDateTime,
  monthKey,
  monthNamesAr,
} from '../lib/format';
import type {
  PayrollImpactListItemDto,
  PayrollImpactType,
  PayrollImpactReviewStatus,
  LeaveRequestType,
  LeaveRequestStatus,
} from '../types/api';

const IMPACT_TYPES: PayrollImpactType[] = [
  'UnpaidLeave',
  'PermissionAfterHoursCompensation',
  'PermissionAdminOrPayrollReview',
];
const REVIEW_STATUSES: PayrollImpactReviewStatus[] = ['Pending', 'Processed', 'Ignored', 'NeedsReview'];
const APPROVAL_STATUSES: LeaveRequestStatus[] = [
  'Submitted',
  'TeamLeaderApproved',
  'ManagerApproved',
  'HrApproved',
  'TeamLeaderRejected',
  'ManagerRejected',
  'HrRejected',
  'ReturnedForEdit',
  'Cancelled',
];

const now = new Date();
const YEARS = [now.getFullYear() - 1, now.getFullYear(), now.getFullYear() + 1];

export default function PayrollLeaveImpactsPage() {
  const [params, setParams] = useSearchParams();
  const openId = params.get('open');

  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [allMonths, setAllMonths] = useState(false);
  const [employeeUserId, setEmployeeUserId] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [type, setType] = useState<LeaveRequestType | ''>('');
  const [approvalStatus, setApprovalStatus] = useState<LeaveRequestStatus | ''>('');
  const [allApprovalStatuses, setAllApprovalStatuses] = useState(false);
  const [impactType, setImpactType] = useState<PayrollImpactType | ''>('');
  const [reviewStatus, setReviewStatus] = useState<PayrollImpactReviewStatus | ''>('');

  const users = useHrDirectoryUsers();
  const departments = useHrDirectoryDepartments();
  const teams = useHrDirectoryTeams();

  const filter: PayrollImpactFilter = {
    month: allMonths ? undefined : monthKey(year, month),
    employeeUserId: employeeUserId || undefined,
    departmentId: departmentId || undefined,
    teamId: teamId || undefined,
    type: type || undefined,
    approvalStatus: approvalStatus || undefined,
    allApprovalStatuses: allApprovalStatuses || undefined,
    impactType: impactType || undefined,
    reviewStatus: reviewStatus || undefined,
  };

  const { data, isLoading, isError, refetch } = usePayrollImpacts(filter);

  const openDetail = (id: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('open', id); return n; });
  const back = () =>
    setParams((p) => { const n = new URLSearchParams(p); n.delete('open'); return n; });

  if (openId) return <ImpactDetail leaveRequestId={openId} onBack={back} />;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">طلبات مؤثّرة على الراتب</h1>
        <p className="mt-1 text-sm text-ink-2">
          عرض إعلامي على مستوى الشركة لطلبات الإجازة/الاستئذان المؤثّرة على الراتب (إجازة بدون رصيد كافٍ،
          استئذان بتعهّد تعويض أو بمعالجة إدارية/مالية). هذه شاشة مراجعة مالية فقط — لا تعدّل الطلب الأصلي
          ولا حالته ولا تُجري أي خصم آليّ.
        </p>
      </div>

      {/* بطاقات تلخيصية */}
      {data && (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
          <SummaryCard label="إجمالي الطلبات المؤثّرة" value={data.summary.totalImpacted} tone="navy" />
          <SummaryCard label="أيام إجازة غير مغطّاة" value={data.summary.totalUncoveredLeaveDays} tone="alert" />
          <SummaryCard label="استئذانات مؤثّرة" value={data.summary.totalImpactedPermissions} tone="gold" />
          <SummaryCard label="تعهّدات تعويض بعد الدوام" value={data.summary.afterHoursCompensationRequests} tone="navy" />
          <SummaryCard label="بحاجة مراجعة مالية" value={data.summary.needsFinanceReviewCount} tone="orange" />
        </div>
      )}

      {/* فلاتر */}
      <Card>
        <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-4">
          <Field label="السنة">
            <Select value={year} disabled={allMonths} onChange={(e) => setYear(Number(e.target.value))}>
              {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
            </Select>
          </Field>
          <Field label="الشهر">
            <Select value={month} disabled={allMonths} onChange={(e) => setMonth(Number(e.target.value))}>
              {monthNamesAr.map((nm, i) => <option key={i} value={i + 1}>{nm}</option>)}
            </Select>
          </Field>
          <Field label="نطاق الفترة">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input type="checkbox" checked={allMonths} onChange={(e) => setAllMonths(e.target.checked)} />
              كل الفترات (تجاهُل الشهر/السنة)
            </label>
          </Field>
          <Field label="الموظّف">
            <Select value={employeeUserId} onChange={(e) => setEmployeeUserId(e.target.value)}>
              <option value="">كل الموظّفين</option>
              {(users.data ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
            </Select>
          </Field>
          <Field label="الإدارة">
            <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">كل الإدارات</option>
              {(departments.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.nameAr}</option>)}
            </Select>
          </Field>
          <Field label="الفريق">
            <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
              <option value="">كل الفِرق</option>
              {(teams.data ?? []).map((t) => <option key={t.id} value={t.id}>{t.nameAr}</option>)}
            </Select>
          </Field>
          <Field label="نوع الطلب">
            <Select value={type} onChange={(e) => setType(e.target.value as LeaveRequestType | '')}>
              <option value="">إجازة + استئذان</option>
              <option value="Leave">{leaveTypeLabel.Leave}</option>
              <option value="Permission">{leaveTypeLabel.Permission}</option>
            </Select>
          </Field>
          <Field label="نوع التأثير">
            <Select value={impactType} onChange={(e) => setImpactType(e.target.value as PayrollImpactType | '')}>
              <option value="">كل الأنواع</option>
              {IMPACT_TYPES.map((it) => <option key={it} value={it}>{payrollImpactTypeLabel[it]}</option>)}
            </Select>
          </Field>
          <Field label="حالة المراجعة المالية">
            <Select value={reviewStatus} onChange={(e) => setReviewStatus(e.target.value as PayrollImpactReviewStatus | '')}>
              <option value="">كل الحالات</option>
              {REVIEW_STATUSES.map((rs) => <option key={rs} value={rs}>{payrollImpactReviewStatusLabel[rs]}</option>)}
            </Select>
          </Field>
          <Field label="حالة اعتماد الطلب">
            <Select
              value={approvalStatus}
              disabled={allApprovalStatuses}
              onChange={(e) => setApprovalStatus(e.target.value as LeaveRequestStatus | '')}
            >
              <option value="">معتمَد نهائيًّا (افتراضي)</option>
              {APPROVAL_STATUSES.map((s) => <option key={s} value={s}>{leaveRequestStatusLabel[s]}</option>)}
            </Select>
          </Field>
          <Field label="كل حالات الاعتماد">
            <label className="flex items-center gap-2 py-2 text-sm text-ink-2">
              <input
                type="checkbox"
                checked={allApprovalStatuses}
                onChange={(e) => setAllApprovalStatuses(e.target.checked)}
              />
              عرض كل الحالات (بما فيها المرفوضة/الملغاة)
            </label>
          </Field>
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل الطلبات المؤثّرة…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الطلبات المؤثّرة. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data?.items ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد طلبات مؤثّرة مطابقة"
                description="عدّل الفلاتر أعلاه. تظهر هنا الطلبات المعتمَدة نهائيًّا المؤثّرة على الراتب افتراضيًّا."
              />
            </div>
          ) : (
            <ImpactTable items={data!.items} onOpen={openDetail} />
          )}
        </Card>
      )}
    </div>
  );
}

function SummaryCard({ label, value, tone }: { label: string; value: number; tone: 'navy' | 'alert' | 'gold' | 'orange' }) {
  const color =
    tone === 'alert' ? 'text-red-600' : tone === 'gold' ? 'text-amber-600' : tone === 'orange' ? 'text-orange' : 'text-navy';
  return (
    <Card className="p-4">
      <p className="text-xs text-ink-2">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${color}`}>{value.toLocaleString('ar-EG')}</p>
    </Card>
  );
}

function ImpactTable({ items, onOpen }: { items: PayrollImpactListItemDto[]; onOpen: (id: string) => void }) {
  return (
    <table className="w-full min-w-[900px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">الموظّف</th>
          <th className="px-3 py-2.5 font-semibold">الإدارة/الفريق</th>
          <th className="px-3 py-2.5 font-semibold">النوع</th>
          <th className="px-3 py-2.5 font-semibold">نوع التأثير</th>
          <th className="px-3 py-2.5 font-semibold">الفترة</th>
          <th className="px-3 py-2.5 font-semibold">غير مغطّى</th>
          <th className="px-3 py-2.5 font-semibold">حالة الاعتماد</th>
          <th className="px-3 py-2.5 font-semibold">المراجعة المالية</th>
          <th className="px-3 py-2.5 font-semibold"></th>
        </tr>
      </thead>
      <tbody>
        {items.map((r) => (
          <tr
            key={r.leaveRequestId}
            onClick={() => onOpen(r.leaveRequestId)}
            className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
          >
            <td className="px-3 py-2.5 font-medium text-navy">{r.requesterName}</td>
            <td className="px-3 py-2.5 text-ink-2">
              {[r.departmentName, r.teamName].filter(Boolean).join(' · ') || '—'}
            </td>
            <td className="px-3 py-2.5 text-ink-2">{leaveTypeLabel[r.type]}</td>
            <td className="px-3 py-2.5 text-ink-2">{payrollImpactTypeLabel[r.impactType]}</td>
            <td className="px-3 py-2.5 text-ink-2">
              {formatDate(r.startDate)}{r.endDate !== r.startDate ? ` ← ${formatDate(r.endDate)}` : ''}
            </td>
            <td className="px-3 py-2.5 text-ink-2">
              {r.type === 'Leave' ? (r.uncoveredLeaveDays ?? 0).toLocaleString('ar-EG') + ' يوم' : '—'}
            </td>
            <td className="px-3 py-2.5">
              <Badge tone="navy">{leaveRequestStatusLabel[r.approvalStatus]}</Badge>
            </td>
            <td className="px-3 py-2.5">
              <Badge tone={payrollImpactReviewStatusTone(r.reviewStatus)}>
                {payrollImpactReviewStatusLabel[r.reviewStatus]}
              </Badge>
            </td>
            <td className="px-3 py-2.5">
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(r.leaveRequestId); }}>عرض</Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ===== لوحة التفاصيل + المراجعة المالية =====
function ImpactDetail({ leaveRequestId, onBack }: { leaveRequestId: string; onBack: () => void }) {
  const { data, isLoading, isError, refetch } = usePayrollImpact(leaveRequestId);
  const review = useReviewPayrollImpact();
  const [status, setStatus] = useState<PayrollImpactReviewStatus>('Pending');
  const [financeNote, setFinanceNote] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const [initialised, setInitialised] = useState(false);

  if (isLoading) return <LoadingState label="يتم تحميل التفاصيل…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل الطلب" description="حدث خطأ أثناء جلب التفاصيل. أعد المحاولة." />;

  const it = data.item;
  if (!initialised) {
    setInitialised(true);
    setStatus(it.reviewStatus);
    setFinanceNote(it.financeNote ?? '');
  }

  const save = () => {
    setErr(null);
    review.mutate(
      { leaveRequestId, req: { status, financeNote: financeNote.trim() || null } },
      { onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{it.requesterName}</h1>
        <Badge tone="navy">{leaveTypeLabel[it.type]}</Badge>
        <Badge tone={payrollImpactReviewStatusTone(it.reviewStatus)}>
          {payrollImpactReviewStatusLabel[it.reviewStatus]}
        </Badge>
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      <Card>
        <h2 className="mb-3 font-semibold text-navy">تفاصيل الطلب المؤثّر</h2>
        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Detail label="نوع التأثير" value={payrollImpactTypeLabel[it.impactType]} />
          <Detail label="حالة اعتماد الطلب" value={leaveRequestStatusLabel[it.approvalStatus]} />
          <Detail label="الإدارة" value={it.departmentName ?? '—'} />
          <Detail label="الفريق" value={it.teamName ?? '—'} />
          <Detail
            label="الفترة"
            value={`${formatDate(it.startDate)}${it.endDate !== it.startDate ? ` ← ${formatDate(it.endDate)}` : ''}`}
          />
          {it.startTime && <Detail label="من وقت" value={it.startTime} />}
          {it.endTime && <Detail label="إلى وقت" value={it.endTime} />}
          {it.type === 'Leave' && (
            <>
              <Detail label="الرصيد وقت الطلب" value={(it.balanceAtRequest ?? 0).toLocaleString('ar-EG')} />
              <Detail label="الأيام المطلوبة" value={(it.requestedLeaveDays ?? 0).toLocaleString('ar-EG')} />
              <Detail label="الأيام غير المغطّاة" value={(it.uncoveredLeaveDays ?? 0).toLocaleString('ar-EG')} />
            </>
          )}
          {it.type === 'Permission' && (
            <Detail label="قرار الموظّف" value={permissionShortfallResolutionLabel[it.permissionShortfallResolution]} />
          )}
          {it.employeeAcknowledgedAtUtc && (
            <Detail label="وقت إقرار الموظّف" value={formatDateTime(it.employeeAcknowledgedAtUtc)} />
          )}
        </div>
        <div className="mt-3 space-y-2">
          <Alert tone="navy">سبب الطلب: {data.reason}</Alert>
          {data.notes && <Alert tone="navy">ملاحظات الموظّف: {data.notes}</Alert>}
        </div>
      </Card>

      <Card>
        <h2 className="mb-1 font-semibold text-navy">المراجعة المالية</h2>
        <p className="mb-3 text-xs text-ink-2">
          حالة وملاحظة فقط. لا تغيّر هذه المراجعة الطلب الأصلي ولا حالته ولا تُجري أي خصم على الراتب.
        </p>
        {it.reviewedByName && (
          <p className="mb-3 text-xs text-ink-2">
            آخر مراجعة: {it.reviewedByName} · {formatDateTime(it.reviewedAtUtc)}
          </p>
        )}
        {data.canManage ? (
          <>
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="حالة المراجعة">
                <Select value={status} onChange={(e) => setStatus(e.target.value as PayrollImpactReviewStatus)}>
                  {REVIEW_STATUSES.map((rs) => (
                    <option key={rs} value={rs}>{payrollImpactReviewStatusLabel[rs]}</option>
                  ))}
                </Select>
              </Field>
              <Field label="ملاحظة مالية (اختياري)">
                <Input value={financeNote} onChange={(e) => setFinanceNote(e.target.value)} placeholder="ملاحظة المراجعة المالية…" />
              </Field>
            </div>
            <div className="mt-4">
              <Button disabled={review.isPending} onClick={save}>حفظ المراجعة</Button>
            </div>
          </>
        ) : (
          <div className="space-y-2 text-sm">
            <Detail label="الحالة" value={payrollImpactReviewStatusLabel[it.reviewStatus]} />
            {it.financeNote && <Detail label="الملاحظة المالية" value={it.financeNote} />}
            <Alert tone="navy">لديك صلاحية الاطّلاع فقط. تحديث المراجعة المالية متاح للموارد البشرية ومدير النظام.</Alert>
          </div>
        )}
      </Card>

      <p className="text-xs text-ink-2">
        لعرض الملف الكامل للموظّف:{' '}
        <Link to={`/app/employee/${it.requesterUserId}`} className="text-navy hover:text-orange-600 hover:underline">
          {it.requesterName}
        </Link>
      </p>
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

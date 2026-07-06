import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useDepartments, useTeams } from '../lib/useDirectory';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  balanceTypeLabel,
  balanceDirectionLabel,
  balanceSourceLabel,
  formatDateTime,
} from '../lib/format';
import type {
  EmployeeBalanceRowDto,
  EmployeeLedgerDto,
  BalanceType,
  BalanceDirection,
  OpeningBalanceRequest,
  BalanceAdjustmentRequest,
} from '../types/api';

const CURRENT_YEAR = new Date().getFullYear();

// إدارة أرصدة الموظّفين (خدمات الموظف، V1.1) — BalanceManagement (Admin/CEO/GM/CeoSupport/HR).
// عرض الأرصدة، رصيد افتتاحي، تعديل يدوي بسبب إلزامي، وسجلّ الحركات. لا حذف لحركة (التصحيح بإضافة معاكسة).
export default function BalanceManagementPage() {
  const [selected, setSelected] = useState<string | null>(null);
  if (selected) return <EmployeeLedger userId={selected} onBack={() => setSelected(null)} />;
  return <EmployeesList onOpen={setSelected} />;
}

function EmployeesList({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [year, setYear] = useState(CURRENT_YEAR);
  const departments = useDepartments();
  const teams = useTeams();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['balance-employees', q, departmentId, teamId, year],
    queryFn: async () =>
      (
        await api.get<EmployeeBalanceRowDto[]>('/balances/employees', {
          params: {
            q: q || undefined,
            departmentId: departmentId || undefined,
            teamId: teamId || undefined,
            year,
          },
        })
      ).data,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">أرصدة الموظّفين</h1>
        <p className="mt-1 text-sm text-ink-2">
          اطّلع على أرصدة الإجازات والأذونات لكل موظّف وأدِرها: رصيد افتتاحي، تعديل يدوي مع سبب إلزامي،
          وسجلّ حركات كامل. الأرصدة محسوبة من حركات لا يمكن حذفها.
        </p>
      </div>

      <Card>
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
          <Field label="بحث بالاسم/البريد">
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="اكتب للبحث…" />
          </Field>
          <Field label="الإدارة">
            <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">الكل</option>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>{d.nameAr}</option>
              ))}
            </Select>
          </Field>
          <Field label="الفريق">
            <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
              <option value="">الكل</option>
              {(teams.data ?? []).map((t) => (
                <option key={t.id} value={t.id}>{t.nameAr}</option>
              ))}
            </Select>
          </Field>
          <Field label="السنة">
            <Input type="number" value={year} onChange={(e) => setYear(Number(e.target.value) || CURRENT_YEAR)} />
          </Field>
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل الأرصدة…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب الأرصدة. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState title="لا يوجد موظّفون مطابقون" description="عدّل عوامل التصفية أعلاه." />
            </div>
          ) : (
            <table className="w-full min-w-[760px] text-right text-sm">
              <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
                <tr>
                  <th className="px-3 py-2.5 font-semibold">الموظّف</th>
                  <th className="px-3 py-2.5 font-semibold">المسمّى</th>
                  <th className="px-3 py-2.5 font-semibold">الإدارة</th>
                  <th className="px-3 py-2.5 font-semibold">رصيد الإجازات</th>
                  <th className="px-3 py-2.5 font-semibold">رصيد الأذونات</th>
                  <th className="px-3 py-2.5 font-semibold"></th>
                </tr>
              </thead>
              <tbody>
                {(data ?? []).map((r) => (
                  <tr
                    key={r.employeeId}
                    onClick={() => onOpen(r.employeeId)}
                    className="cursor-pointer border-b border-line last:border-0 hover:bg-offwhite"
                  >
                    <td className="px-3 py-2.5 font-medium text-navy">{r.employeeName}</td>
                    <td className="px-3 py-2.5 text-ink-2">{r.jobTitle ?? '—'}</td>
                    <td className="px-3 py-2.5 text-ink-2">{r.departmentName ?? '—'}</td>
                    <td className="px-3 py-2.5">
                      <Badge tone={r.annualLeaveNegative ? 'alert' : 'navy'}>{r.annualLeaveRemaining} يوم</Badge>
                    </td>
                    <td className="px-3 py-2.5">
                      <Badge tone={r.permissionNegative ? 'alert' : 'navy'}>{r.permissionRemaining}</Badge>
                    </td>
                    <td className="px-3 py-2.5">
                      <Button variant="ghost" onClick={(e) => { e.stopPropagation(); onOpen(r.employeeId); }}>
                        عرض السجلّ
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}
    </div>
  );
}

function EmployeeLedger({ userId, onBack }: { userId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const [year, setYear] = useState(CURRENT_YEAR);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['balance-ledger', userId, year],
    queryFn: async () =>
      (await api.get<EmployeeLedgerDto>(`/balances/employees/${userId}/ledger`, { params: { year } })).data,
  });

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ['balance-ledger', userId] });
    void qc.invalidateQueries({ queryKey: ['balance-employees'] });
  };

  if (isLoading) return <LoadingState label="يتم تحميل السجلّ…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب السجلّ. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع للقائمة</button>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-navy">{data.employeeName}</h1>
        <div className="w-32">
          <Field label="السنة">
            <Input type="number" value={year} onChange={(e) => setYear(Number(e.target.value) || CURRENT_YEAR)} />
          </Field>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <div className="flex items-center justify-between">
            <h2 className="font-semibold text-navy">رصيد الإجازات (أيام)</h2>
            {data.annualLeave.isNegative && <Badge tone="alert">سالب</Badge>}
          </div>
          <p className={`mt-2 text-2xl font-bold ${data.annualLeave.isNegative ? 'text-red-600' : 'text-navy'}`}>
            {data.annualLeave.remaining}
          </p>
          <p className="mt-1 text-xs text-ink-2">مُضاف {data.annualLeave.credited} · مخصوم {data.annualLeave.debited}</p>
        </Card>
        <Card>
          <div className="flex items-center justify-between">
            <h2 className="font-semibold text-navy">رصيد الأذونات</h2>
            {data.permission.isNegative && <Badge tone="alert">سالب</Badge>}
          </div>
          <p className={`mt-2 text-2xl font-bold ${data.permission.isNegative ? 'text-red-600' : 'text-navy'}`}>
            {data.permission.remaining}
          </p>
          <p className="mt-1 text-xs text-ink-2">مُضاف {data.permission.credited} · مخصوم {data.permission.debited}</p>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <OpeningForm userId={userId} year={year} onDone={invalidate} />
        <AdjustForm userId={userId} year={year} onDone={invalidate} />
      </div>

      <Card className="overflow-x-auto p-0">
        <div className="border-b border-line p-4">
          <h2 className="font-semibold text-navy">سجلّ الحركات ({data.entries.length})</h2>
        </div>
        {data.entries.length === 0 ? (
          <div className="p-5">
            <EmptyState title="لا توجد حركات بعد" description="ابدأ برصيد افتتاحي، أو ستظهر هنا حركات الاعتماد الآلية." />
          </div>
        ) : (
          <table className="w-full min-w-[760px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">النوع</th>
                <th className="px-3 py-2.5 font-semibold">الاتجاه</th>
                <th className="px-3 py-2.5 font-semibold">المقدار</th>
                <th className="px-3 py-2.5 font-semibold">المصدر</th>
                <th className="px-3 py-2.5 font-semibold">ملاحظة</th>
                <th className="px-3 py-2.5 font-semibold">المنفّذ</th>
                <th className="px-3 py-2.5 font-semibold">التاريخ</th>
              </tr>
            </thead>
            <tbody>
              {data.entries.map((e) => (
                <tr key={e.id} className="border-b border-line last:border-0">
                  <td className="px-3 py-2.5 text-ink-2">{balanceTypeLabel[e.balanceType]}</td>
                  <td className="px-3 py-2.5">
                    <Badge tone={e.direction === 'Credit' ? 'success' : 'alert'}>{balanceDirectionLabel[e.direction]}</Badge>
                  </td>
                  <td className="px-3 py-2.5 font-medium text-navy">{e.amount}</td>
                  <td className="px-3 py-2.5 text-ink-2">{balanceSourceLabel[e.source]}</td>
                  <td className="px-3 py-2.5 text-ink-2">{e.notes ?? '—'}</td>
                  <td className="px-3 py-2.5 text-ink-2">{e.createdByName ?? '—'}</td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDateTime(e.createdAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

function OpeningForm({ userId, year, onDone }: { userId: string; year: number; onDone: () => void }) {
  const [balanceType, setBalanceType] = useState<BalanceType>('AnnualLeave');
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState(false);

  const mut = useMutation({
    mutationFn: () => {
      const body: OpeningBalanceRequest = { balanceType, amount: Number(amount), year, notes: notes || null };
      return api.post(`/balances/employees/${userId}/opening`, body);
    },
    onSuccess: () => { setAmount(''); setNotes(''); setOk(true); onDone(); },
    onError: (e) => { setOk(false); setErr(apiErrorMessage(e)); },
  });

  return (
    <Card>
      <h2 className="mb-1 font-semibold text-navy">رصيد افتتاحي</h2>
      <p className="mb-3 text-xs text-ink-2">يُضيف رصيدًا لسنة {year}. للتصحيح استخدم التعديل اليدوي بدلًا من تكرار الافتتاحي.</p>
      {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
      {ok && <div className="mb-3"><Alert tone="success">تم تسجيل الرصيد الافتتاحي.</Alert></div>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="نوع الرصيد">
          <Select value={balanceType} onChange={(e) => { setBalanceType(e.target.value as BalanceType); setOk(false); }}>
            <option value="AnnualLeave">{balanceTypeLabel.AnnualLeave}</option>
            <option value="Permission">{balanceTypeLabel.Permission}</option>
          </Select>
        </Field>
        <Field label="المقدار">
          <Input type="number" value={amount} onChange={(e) => { setAmount(e.target.value); setOk(false); }} placeholder="مثال: 21" />
        </Field>
      </div>
      <div className="mt-3">
        <Field label="ملاحظة (اختياري)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="تفاصيل…" />
        </Field>
      </div>
      <div className="mt-4">
        <Button
          disabled={mut.isPending || !amount || Number(amount) <= 0}
          onClick={() => { setErr(null); mut.mutate(); }}
        >
          تسجيل الرصيد الافتتاحي
        </Button>
      </div>
    </Card>
  );
}

function AdjustForm({ userId, year, onDone }: { userId: string; year: number; onDone: () => void }) {
  const [balanceType, setBalanceType] = useState<BalanceType>('AnnualLeave');
  const [direction, setDirection] = useState<BalanceDirection>('Credit');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState(false);

  const mut = useMutation({
    mutationFn: () => {
      const body: BalanceAdjustmentRequest = { balanceType, direction, amount: Number(amount), year, reason };
      return api.post(`/balances/employees/${userId}/adjust`, body);
    },
    onSuccess: () => { setAmount(''); setReason(''); setOk(true); onDone(); },
    onError: (e) => { setOk(false); setErr(apiErrorMessage(e)); },
  });

  return (
    <Card>
      <h2 className="mb-1 font-semibold text-navy">تعديل يدوي</h2>
      <p className="mb-3 text-xs text-ink-2">يُضيف حركة إضافة/خصم بسبب إلزامي يُسجَّل في التدقيق. لا يُحذف ولا يُعدّل أي حركة قائمة.</p>
      {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
      {ok && <div className="mb-3"><Alert tone="success">تم تسجيل التعديل.</Alert></div>}
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="نوع الرصيد">
          <Select value={balanceType} onChange={(e) => { setBalanceType(e.target.value as BalanceType); setOk(false); }}>
            <option value="AnnualLeave">{balanceTypeLabel.AnnualLeave}</option>
            <option value="Permission">{balanceTypeLabel.Permission}</option>
          </Select>
        </Field>
        <Field label="الاتجاه">
          <Select value={direction} onChange={(e) => { setDirection(e.target.value as BalanceDirection); setOk(false); }}>
            <option value="Credit">{balanceDirectionLabel.Credit}</option>
            <option value="Debit">{balanceDirectionLabel.Debit}</option>
          </Select>
        </Field>
        <Field label="المقدار">
          <Input type="number" value={amount} onChange={(e) => { setAmount(e.target.value); setOk(false); }} placeholder="مثال: 2" />
        </Field>
      </div>
      <div className="mt-3">
        <Field label="سبب التعديل (إلزامي)">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="اذكر سبب التعديل…" />
        </Field>
      </div>
      <div className="mt-4">
        <Button
          disabled={mut.isPending || !amount || Number(amount) <= 0 || !reason.trim()}
          title={!reason.trim() ? 'اكتب سبب التعديل أولًا' : undefined}
          onClick={() => { setErr(null); mut.mutate(); }}
        >
          تسجيل التعديل
        </Button>
      </div>
    </Card>
  );
}

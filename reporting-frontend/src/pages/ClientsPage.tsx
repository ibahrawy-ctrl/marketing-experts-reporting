// صفحة العملاء — قائمة العملاء + لوحة صحّة الحساب (Account Health) + إنشاء عميل جديد.
// النطاق ومستوى الرؤية مفروضان من الخادم؛ أزرار الإدارة تظهر فقط لمن يملك canEditClientCore.
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  useClients,
  useClientHealth,
  useCreateClient,
  useArchiveClient,
  useReactivateClient,
  useDeleteClient,
} from '../lib/useClients';
import { useDirectoryUsers } from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { Card, Badge, Select, Button, StatCard, Field, Input, Alert, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { clientStatusLabel, clientStatusTone, formatDate } from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type { ClientDto, ClientStatus, CreateClientRequest } from '../types/api';

// عرض القائمة: النشط (غير المؤرشفين) / المؤرشف (Closed فقط) / الكل.
type ClientView = 'active' | 'archived' | 'all';

export default function ClientsPage() {
  // canEditClientCore يطابق سياسة ClientCoreManagement بالخادم (تستثني قائد الفريق) — تحكم إنشاء/أرشفة/تفعيل/حذف العميل.
  const { canEditClientCore } = useAuth();
  const [view, setView] = useState<ClientView>('active');
  const [statusFilter, setStatusFilter] = useState<ClientStatus | ''>('');
  const [showCreate, setShowCreate] = useState(false);

  const clients = useClients({
    status: view === 'archived' ? 'Closed' : statusFilter || undefined,
    includeClosed: view !== 'active',
  });
  const health = useClientHealth();

  if (clients.isLoading) return <LoadingState label="يتم تحميل العملاء…" />;
  if (clients.isError)
    return (
      <QueryError
        onRetry={() => clients.refetch()}
        description="حدث خطأ أثناء جلب بيانات العملاء. أعد المحاولة."
      />
    );

  const rows = clients.data ?? [];
  const total = rows.length;
  const active = rows.filter((c) => c.status === 'Active').length;
  const atRisk = rows.filter((c) => c.status === 'AtRisk').length;
  const totalProjects = rows.reduce((a, c) => a + c.projectCount, 0);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">العملاء</h1>
          <p className="mt-1 text-sm text-ink-2">
            إدارة العملاء ومشاريعهم وربط التقارير بهم. تظهر هنا العملاء ضمن نطاق صلاحيتك فقط.
          </p>
        </div>
        {canEditClientCore && (
          <Button variant={showCreate ? 'ghost' : 'primary'} onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? 'إغلاق' : '+ إضافة عميل'}
          </Button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي العملاء" value={total} />
        <StatCard label="عملاء نشِطون" value={active} />
        <StatCard label="عملاء معرّضون للخطر" value={atRisk} tone={atRisk > 0 ? 'alert' : 'navy'} />
        <StatCard label="إجمالي المشاريع" value={totalProjects} />
      </div>

      {canEditClientCore && showCreate && <CreateClientForm onDone={() => setShowCreate(false)} />}

      <ClientHealthPanel
        report={health.data}
        isLoading={health.isLoading}
        isError={health.isError}
        onRetry={() => health.refetch()}
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={view}
          onChange={(e) => setView(e.target.value as ClientView)}
          className="max-w-xs"
        >
          <option value="active">النشط</option>
          <option value="archived">المؤرشف</option>
          <option value="all">الكل</option>
        </Select>
        {view !== 'archived' && (
          <Select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as ClientStatus | '')}
            className="max-w-xs"
          >
            <option value="">كل الحالات</option>
            {(['Active', 'Paused', 'AtRisk'] as ClientStatus[]).map((s) => (
              <option key={s} value={s}>
                {clientStatusLabel[s]}
              </option>
            ))}
          </Select>
        )}
        <span className="text-xs text-ink-3">«مؤرشف» يقابل الحالة Closed داخليًا.</span>
      </div>

      {rows.length === 0 ? (
        <EmptyState
          title="لا يوجد عملاء"
          description="لا يوجد عميل ضمن نطاقك يطابق الفلتر الحالي. جرّب تغيير الحالة أو أضِف عميلًا جديدًا."
        />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[760px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">العميل</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">مدير العميل</th>
                <th className="px-3 py-2.5 font-semibold">المشاريع</th>
                <th className="px-3 py-2.5 font-semibold">نشِطة</th>
                <th className="px-3 py-2.5 font-semibold">في خطر</th>
                <th className="px-3 py-2.5 font-semibold">جهة الاتصال</th>
                <th className="px-3 py-2.5 font-semibold"></th>
                {canEditClientCore && <th className="px-3 py-2.5 font-semibold">إجراءات</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((c) => (
                <ClientRow key={c.id} client={c} canManage={canEditClientCore} />
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}

function ClientRow({ client: c, canManage }: { client: ClientDto; canManage: boolean }) {
  const archive = useArchiveClient();
  const reactivate = useReactivateClient();
  const del = useDeleteClient();
  const [err, setErr] = useState<string | null>(null);
  const isArchived = c.status === 'Closed';
  const busy = archive.isPending || reactivate.isPending || del.isPending;

  async function run(action: () => Promise<unknown>, fallback: string) {
    setErr(null);
    try {
      await action();
    } catch (e) {
      setErr(apiErrorMessage(e, fallback));
    }
  }

  return (
    <>
      <tr className="border-b border-line last:border-0 hover:bg-offwhite">
        <td className="px-3 py-2.5 font-semibold text-navy">{c.name}</td>
        <td className="px-3 py-2.5">
          <Badge tone={clientStatusTone(c.status)}>{clientStatusLabel[c.status]}</Badge>
        </td>
        <td className="px-3 py-2.5 text-ink-2">{c.accountManagerName ?? '—'}</td>
        <td className="px-3 py-2.5">{c.projectCount}</td>
        <td className="px-3 py-2.5 text-success">{c.activeProjectCount}</td>
        <td className={`px-3 py-2.5 ${c.atRiskProjectCount > 0 ? 'font-semibold text-alert' : ''}`}>
          {c.atRiskProjectCount}
        </td>
        <td className="px-3 py-2.5 text-ink-2">{c.mainContactName ?? '—'}</td>
        <td className="px-3 py-2.5">
          <Link to={`/app/clients/${c.id}`} className="text-sm font-semibold text-orange-600 hover:underline">
            تفاصيل
          </Link>
        </td>
        {canManage && (
          <td className="px-3 py-2.5">
            <div className="flex flex-wrap gap-1.5">
              {!isArchived ? (
                <Button
                  variant="ghost"
                  className="!px-2 !py-1 text-xs"
                  disabled={busy}
                  onClick={() => {
                    if (window.confirm(`أرشفة العميل «${c.name}»؟ يمكن إعادة تفعيله لاحقًا.`))
                      run(() => archive.mutateAsync(c.id), 'تعذّرت الأرشفة.');
                  }}
                >
                  أرشفة
                </Button>
              ) : (
                <>
                  <Button
                    variant="ghost"
                    className="!px-2 !py-1 text-xs"
                    disabled={busy}
                    onClick={() => run(() => reactivate.mutateAsync(c.id), 'تعذّرت إعادة التفعيل.')}
                  >
                    إعادة تفعيل
                  </Button>
                  <Button
                    variant="ghost"
                    className="!px-2 !py-1 text-xs !text-alert"
                    disabled={busy || !c.canHardDelete}
                    title={!c.canHardDelete ? c.deleteBlockReason ?? undefined : undefined}
                    onClick={() => {
                      if (window.confirm(`حذف العميل «${c.name}» نهائيًا؟ لا يمكن التراجع.`))
                        run(() => del.mutateAsync(c.id), 'تعذّر الحذف النهائي.');
                    }}
                  >
                    حذف نهائي
                  </Button>
                </>
              )}
            </div>
          </td>
        )}
      </tr>
      {isArchived && !c.canHardDelete && c.deleteBlockReason && canManage && (
        <tr>
          <td colSpan={9} className="px-3 pb-2 pt-0">
            <p className="text-xs text-ink-2">{c.deleteBlockReason}</p>
          </td>
        </tr>
      )}
      {err && (
        <tr>
          <td colSpan={9} className="px-3 pb-2 pt-0">
            <Alert tone="alert">{err}</Alert>
          </td>
        </tr>
      )}
    </>
  );
}

function ClientHealthPanel({
  report,
  isLoading,
  isError,
  onRetry,
}: {
  report: import('../types/api').ClientHealthReport | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading) return null;
  if (isError)
    return <QueryError onRetry={onRetry} title="تعذّر تحميل صحّة الحسابات" description="أعد المحاولة." />;
  if (!report) return null;

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-navy">صحّة الحسابات</h2>
        <span className="text-xs text-ink-2">{report.periodLabel}</span>
      </div>
      <div className="mt-4 grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي العملاء" value={report.totalClients} />
        <StatCard
          label="عملاء في خطر"
          value={report.atRiskClients}
          tone={report.atRiskClients > 0 ? 'alert' : 'navy'}
        />
        <StatCard
          label="قرارات مطلوبة"
          value={report.decisionNeededCount}
          tone={report.decisionNeededCount > 0 ? 'alert' : 'navy'}
        />
        <StatCard label="فرص تجديد" value={report.renewalOpportunities} />
      </div>

      {!report.canViewRows ? (
        <Alert tone="navy">
          <span className="text-sm">
            تُعرض لك أرقام إجمالية على مستوى الشركة (ملخّص تنفيذي) دون تفاصيل صفوف العملاء.
          </span>
        </Alert>
      ) : report.rows.length === 0 ? (
        <p className="mt-4 text-sm text-ink-2">لا توجد بيانات صحّة عملاء ضمن نطاقك.</p>
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[820px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">العميل</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">مدير العميل</th>
                <th className="px-3 py-2.5 font-semibold">مشاريع</th>
                <th className="px-3 py-2.5 font-semibold">في خطر</th>
                <th className="px-3 py-2.5 font-semibold">مخاطر مفتوحة</th>
                <th className="px-3 py-2.5 font-semibold">ملاحظات</th>
                <th className="px-3 py-2.5 font-semibold">آخر تقرير</th>
                <th className="px-3 py-2.5 font-semibold">خطر التسرّب</th>
                <th className="px-3 py-2.5 font-semibold">قرار</th>
              </tr>
            </thead>
            <tbody>
              {report.rows.map((r) => (
                <tr key={r.clientId} className="border-b border-line last:border-0 hover:bg-offwhite">
                  <td className="px-3 py-2.5 font-semibold text-navy">
                    <Link to={`/app/clients/${r.clientId}`} className="hover:underline">
                      {r.clientName}
                    </Link>
                  </td>
                  <td className="px-3 py-2.5">
                    <Badge tone={clientStatusTone(r.status)}>{clientStatusLabel[r.status]}</Badge>
                  </td>
                  <td className="px-3 py-2.5 text-ink-2">{r.accountManagerName ?? '—'}</td>
                  <td className="px-3 py-2.5">{r.projectCount}</td>
                  <td className={`px-3 py-2.5 ${r.atRiskProjectCount > 0 ? 'font-semibold text-alert' : ''}`}>
                    {r.atRiskProjectCount}
                  </td>
                  <td className={`px-3 py-2.5 ${r.openRiskCount > 0 ? 'font-semibold text-alert' : ''}`}>
                    {r.openRiskCount}
                  </td>
                  <td className="px-3 py-2.5">{r.openNoteCount}</td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDate(r.lastReportAtUtc)}</td>
                  <td className="px-3 py-2.5">
                    <Badge
                      tone={r.churnRisk === 'عالٍ' ? 'alert' : r.churnRisk === 'متوسط' ? 'gold' : 'success'}
                    >
                      {r.churnRisk}
                    </Badge>
                  </td>
                  <td className="px-3 py-2.5">
                    {r.decisionNeeded ? <Badge tone="alert">مطلوب</Badge> : <span className="text-ink-3">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function CreateClientForm({ onDone }: { onDone: () => void }) {
  const navigate = useNavigate();
  const create = useCreateClient();
  const users = useDirectoryUsers();
  const [name, setName] = useState('');
  const [accountManagerId, setAccountManagerId] = useState('');
  const [mainContactName, setMainContactName] = useState('');
  const [mainContactInfo, setMainContactInfo] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم العميل مطلوب.');
      return;
    }
    const req: CreateClientRequest = {
      name: name.trim(),
      accountManagerId: accountManagerId || null,
      mainContactName: mainContactName.trim() || null,
      mainContactInfo: mainContactInfo.trim() || null,
      notes: notes.trim() || null,
    };
    try {
      const created = await create.mutateAsync(req);
      onDone();
      // فتح صفحة تفاصيل العميل الجديد مباشرةً لاستكمال بيانات Client 360.
      navigate(`/app/clients/${created.id}`);
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر إنشاء العميل.'));
    }
  }

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-bold text-navy">إضافة عميل جديد</h2>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="اسم العميل">
          <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="مثال: شركة النخبة" />
        </Field>
        <Field label="مدير العميل">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="اسم جهة الاتصال">
          <Input value={mainContactName} onChange={(e) => setMainContactName(e.target.value)} />
        </Field>
        <Field label="بيانات الاتصال">
          <Input value={mainContactInfo} onChange={(e) => setMainContactInfo(e.target.value)} placeholder="هاتف / بريد" />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={create.isPending || !name.trim()}>
          {create.isPending ? 'جارٍ الحفظ…' : 'حفظ العميل'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </Card>
  );
}

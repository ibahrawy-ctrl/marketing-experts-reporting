// صفحة تفاصيل العميل — بيانات العميل + مشاريعه + تقاريره المرتبطة، مع تعديل/أرشفة وإنشاء مشروع.
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  useClient,
  useClientReports,
  useUpdateClient,
  useArchiveClient,
  useProjects,
  useCreateProject,
} from '../lib/useClients';
import { useDirectoryUsers, useTeams } from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { Card, Badge, Button, StatCard, Field, Input, Select, Alert, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  clientStatusLabel,
  clientStatusTone,
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  submissionStatusLabel,
  periodTypeLabel,
  formatDate,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type {
  ClientStatus,
  ProjectStatus,
  ServiceType,
  UpdateClientRequest,
  CreateProjectRequest,
} from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

export default function ClientDetailPage() {
  const { clientId } = useParams<{ clientId: string }>();
  const { canManageClients } = useAuth();
  const client = useClient(clientId);
  const projects = useProjects({ clientId: clientId, includeClosed: true });
  const reports = useClientReports(clientId);
  const [editing, setEditing] = useState(false);
  const [creatingProject, setCreatingProject] = useState(false);

  if (client.isLoading) return <LoadingState label="يتم تحميل بيانات العميل…" />;
  if (client.isError || !client.data)
    return (
      <QueryError
        onRetry={() => client.refetch()}
        title="تعذّر عرض العميل"
        description="قد يكون العميل خارج نطاق صلاحيتك أو غير موجود."
      />
    );

  const c = client.data;
  const projectRows = projects.data ?? [];
  const reportRows = reports.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="mb-1 text-xs text-ink-2">
            <Link to="/app/clients" className="hover:underline">
              العملاء
            </Link>{' '}
            / {c.name}
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-navy">{c.name}</h1>
            <Badge tone={clientStatusTone(c.status)}>{clientStatusLabel[c.status]}</Badge>
          </div>
        </div>
        {canManageClients && !editing && (
          <div className="flex gap-2">
            <Button variant="ghost" onClick={() => setEditing(true)}>
              تعديل بيانات العميل
            </Button>
            {c.status !== 'Closed' && <ArchiveClientButton id={c.id} />}
          </div>
        )}
      </div>

      {canManageClients && editing ? (
        <EditClientForm client={c} onDone={() => setEditing(false)} />
      ) : (
        <Card>
          <dl className="grid grid-cols-2 gap-4 text-sm lg:grid-cols-4">
            <Info label="مدير الحساب" value={c.accountManagerName ?? '—'} />
            <Info label="جهة الاتصال" value={c.mainContactName ?? '—'} />
            <Info label="بيانات الاتصال" value={c.mainContactInfo ?? '—'} />
            <Info label="تاريخ الإضافة" value={formatDate(c.createdAtUtc)} />
          </dl>
          {c.notes && (
            <div className="mt-4 rounded-lg bg-offwhite p-3 text-sm text-ink-2">
              <span className="font-semibold text-ink">ملاحظات: </span>
              {c.notes}
            </div>
          )}
        </Card>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي المشاريع" value={c.projectCount} />
        <StatCard label="مشاريع نشِطة" value={c.activeProjectCount} />
        <StatCard
          label="مشاريع في خطر"
          value={c.atRiskProjectCount}
          tone={c.atRiskProjectCount > 0 ? 'alert' : 'navy'}
        />
        <StatCard label="تقارير مرتبطة" value={reportRows.length} />
      </div>

      {/* المشاريع */}
      <Card className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-bold text-navy">مشاريع العميل</h2>
          {canManageClients && (
            <Button
              variant={creatingProject ? 'ghost' : 'primary'}
              onClick={() => setCreatingProject((v) => !v)}
            >
              {creatingProject ? 'إغلاق' : '+ مشروع جديد'}
            </Button>
          )}
        </div>

        {canManageClients && creatingProject && (
          <CreateProjectForm clientId={c.id} onDone={() => setCreatingProject(false)} />
        )}

        {projectRows.length === 0 ? (
          <EmptyState title="لا توجد مشاريع" description="لم تُضَف مشاريع لهذا العميل بعد." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] text-right text-sm">
              <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
                <tr>
                  <th className="px-3 py-2.5 font-semibold">المشروع</th>
                  <th className="px-3 py-2.5 font-semibold">الخدمة</th>
                  <th className="px-3 py-2.5 font-semibold">الحالة</th>
                  <th className="px-3 py-2.5 font-semibold">الفريق</th>
                  <th className="px-3 py-2.5 font-semibold">البداية</th>
                  <th className="px-3 py-2.5 font-semibold">النهاية</th>
                  <th className="px-3 py-2.5 font-semibold"></th>
                </tr>
              </thead>
              <tbody>
                {projectRows.map((p) => (
                  <tr key={p.id} className="border-b border-line last:border-0 hover:bg-offwhite">
                    <td className="px-3 py-2.5 font-semibold text-navy">{p.name}</td>
                    <td className="px-3 py-2.5 text-ink-2">{serviceTypeLabel[p.serviceType]}</td>
                    <td className="px-3 py-2.5">
                      <Badge tone={projectStatusTone(p.status)}>{projectStatusLabel[p.status]}</Badge>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{p.ownerTeamName ?? '—'}</td>
                    <td className="px-3 py-2.5 text-ink-2">{formatDate(p.startDate)}</td>
                    <td className="px-3 py-2.5 text-ink-2">{formatDate(p.endDate)}</td>
                    <td className="px-3 py-2.5">
                      <Link
                        to={`/app/projects/${p.id}`}
                        className="text-sm font-semibold text-orange-600 hover:underline"
                      >
                        تفاصيل
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* التقارير المرتبطة */}
      <LinkedReportsCard rows={reportRows} title="تقارير العميل المرتبطة" />
    </div>
  );
}

function Info({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-ink-2">{label}</dt>
      <dd className="mt-0.5 font-medium text-ink">{value}</dd>
    </div>
  );
}

export function LinkedReportsCard({
  rows,
  title,
}: {
  rows: import('../types/api').LinkedReportRow[];
  title: string;
}) {
  return (
    <Card>
      <h2 className="text-lg font-bold text-navy">{title}</h2>
      {rows.length === 0 ? (
        <p className="mt-3 text-sm text-ink-2">لا توجد تقارير مرتبطة بعد.</p>
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[640px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">مُقدِّم التقرير</th>
                <th className="px-3 py-2.5 font-semibold">الدورية</th>
                <th className="px-3 py-2.5 font-semibold">الفترة</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">تاريخ الإرسال</th>
                <th className="px-3 py-2.5 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.submissionId} className="border-b border-line last:border-0 hover:bg-offwhite">
                  <td className="px-3 py-2.5 font-medium text-ink">{r.submitterName ?? '—'}</td>
                  <td className="px-3 py-2.5 text-ink-2">{periodTypeLabel[r.periodType]}</td>
                  <td className="px-3 py-2.5 text-ink-2">{r.periodKey}</td>
                  <td className="px-3 py-2.5">
                    <Badge tone={r.status === 'Closed' ? 'success' : r.status === 'Returned' ? 'gold' : 'navy'}>
                      {submissionStatusLabel[r.status]}
                    </Badge>
                  </td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDate(r.submittedAtUtc)}</td>
                  <td className="px-3 py-2.5">
                    <Link
                      to={`/app/submissions?open=${r.submissionId}`}
                      className="text-sm font-semibold text-orange-600 hover:underline"
                    >
                      فتح
                    </Link>
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

function ArchiveClientButton({ id }: { id: string }) {
  const archive = useArchiveClient();
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  if (!confirm)
    return (
      <Button variant="danger" onClick={() => setConfirm(true)}>
        أرشفة العميل
      </Button>
    );

  return (
    <div className="flex flex-col items-end gap-1">
      {err && <span className="text-xs text-alert">{err}</span>}
      <div className="flex gap-2">
        <Button
          variant="danger"
          onClick={async () => {
            setErr(null);
            try {
              await archive.mutateAsync(id);
              setConfirm(false);
            } catch (e) {
              setErr(apiErrorMessage(e, 'تعذّرت الأرشفة.'));
            }
          }}
          disabled={archive.isPending}
        >
          تأكيد الأرشفة
        </Button>
        <Button variant="ghost" onClick={() => setConfirm(false)}>
          تراجع
        </Button>
      </div>
    </div>
  );
}

function EditClientForm({
  client,
  onDone,
}: {
  client: import('../types/api').ClientDto;
  onDone: () => void;
}) {
  const update = useUpdateClient();
  const users = useDirectoryUsers();
  const [name, setName] = useState(client.name);
  const [status, setStatus] = useState<ClientStatus>(client.status);
  const [accountManagerId, setAccountManagerId] = useState(client.accountManagerId ?? '');
  const [mainContactName, setMainContactName] = useState(client.mainContactName ?? '');
  const [mainContactInfo, setMainContactInfo] = useState(client.mainContactInfo ?? '');
  const [notes, setNotes] = useState(client.notes ?? '');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم العميل مطلوب.');
      return;
    }
    const req: UpdateClientRequest = {
      name: name.trim(),
      status,
      accountManagerId: accountManagerId || null,
      mainContactName: mainContactName.trim() || null,
      mainContactInfo: mainContactInfo.trim() || null,
      notes: notes.trim() || null,
    };
    try {
      await update.mutateAsync({ id: client.id, req });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ التعديلات.'));
    }
  }

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-bold text-navy">تعديل بيانات العميل</h2>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="اسم العميل">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="الحالة">
          <Select value={status} onChange={(e) => setStatus(e.target.value as ClientStatus)}>
            {(['Active', 'Paused', 'AtRisk', 'Closed'] as ClientStatus[]).map((s) => (
              <option key={s} value={s}>
                {clientStatusLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مدير الحساب">
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
          <Input value={mainContactInfo} onChange={(e) => setMainContactInfo(e.target.value)} />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={update.isPending || !name.trim()}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </Card>
  );
}

function CreateProjectForm({ clientId, onDone }: { clientId: string; onDone: () => void }) {
  const create = useCreateProject();
  const teams = useTeams();
  const users = useDirectoryUsers();
  const [name, setName] = useState('');
  const [serviceType, setServiceType] = useState<ServiceType>('Social');
  const [status, setStatus] = useState<ProjectStatus>('Active');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [ownerTeamId, setOwnerTeamId] = useState('');
  const [accountManagerId, setAccountManagerId] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم المشروع مطلوب.');
      return;
    }
    const req: CreateProjectRequest = {
      clientId,
      name: name.trim(),
      serviceType,
      status,
      startDate: startDate || null,
      endDate: endDate || null,
      ownerTeamId: ownerTeamId || null,
      accountManagerId: accountManagerId || null,
      notes: notes.trim() || null,
    };
    try {
      await create.mutateAsync(req);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر إنشاء المشروع.'));
    }
  }

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <h3 className="font-bold text-navy">مشروع جديد</h3>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="اسم المشروع">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="نوع الخدمة">
          <Select value={serviceType} onChange={(e) => setServiceType(e.target.value as ServiceType)}>
            {SERVICE_TYPES.map((s) => (
              <option key={s} value={s}>
                {serviceTypeLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الحالة">
          <Select value={status} onChange={(e) => setStatus(e.target.value as ProjectStatus)}>
            {(['Active', 'Paused', 'Completed', 'AtRisk', 'Closed'] as ProjectStatus[]).map((s) => (
              <option key={s} value={s}>
                {projectStatusLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الفريق المسؤول">
          <Select value={ownerTeamId} onChange={(e) => setOwnerTeamId(e.target.value)}>
            <option value="">— بدون —</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مدير الحساب">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="تاريخ البداية">
          <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </Field>
        <Field label="تاريخ النهاية">
          <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={create.isPending || !name.trim()}>
          {create.isPending ? 'جارٍ الحفظ…' : 'حفظ المشروع'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

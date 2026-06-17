// صفحة تفاصيل المشروع — ملخّص المشروع (تقارير/مخاطر/ملاحظات) + بياناته + تقاريره المرتبطة، مع تعديل/أرشفة.
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  useProject,
  useProjectSummary,
  useProjectReports,
  useUpdateProject,
  useArchiveProject,
} from '../lib/useClients';
import { useDirectoryUsers, useTeams } from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { Card, Badge, Button, StatCard, Field, Input, Select, Alert } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { LinkedReportsCard } from './ClientDetailPage';
import {
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  formatDate,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type { ProjectStatus, ServiceType, UpdateProjectRequest } from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

export default function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { canManageClients } = useAuth();
  const project = useProject(projectId);
  const summary = useProjectSummary(projectId);
  const reports = useProjectReports(projectId);
  const [editing, setEditing] = useState(false);

  if (project.isLoading) return <LoadingState label="يتم تحميل بيانات المشروع…" />;
  if (project.isError || !project.data)
    return (
      <QueryError
        onRetry={() => project.refetch()}
        title="تعذّر عرض المشروع"
        description="قد يكون المشروع خارج نطاق صلاحيتك أو غير موجود."
      />
    );

  const p = project.data;
  const s = summary.data;
  const reportRows = reports.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="mb-1 text-xs text-ink-2">
            <Link to="/app/projects" className="hover:underline">
              المشاريع
            </Link>{' '}
            /{' '}
            {p.clientName && (
              <>
                <Link to={`/app/clients/${p.clientId}`} className="hover:underline">
                  {p.clientName}
                </Link>{' '}
                /{' '}
              </>
            )}
            {p.name}
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-navy">{p.name}</h1>
            <Badge tone={projectStatusTone(p.status)}>{projectStatusLabel[p.status]}</Badge>
          </div>
        </div>
        {canManageClients && !editing && (
          <div className="flex gap-2">
            <Button variant="ghost" onClick={() => setEditing(true)}>
              تعديل المشروع
            </Button>
            {p.status !== 'Closed' && <ArchiveProjectButton id={p.id} />}
          </div>
        )}
      </div>

      {canManageClients && editing ? (
        <EditProjectForm project={p} onDone={() => setEditing(false)} />
      ) : (
        <Card>
          <dl className="grid grid-cols-2 gap-4 text-sm lg:grid-cols-4">
            <Info label="العميل" value={p.clientName ?? '—'} />
            <Info label="نوع الخدمة" value={serviceTypeLabel[p.serviceType]} />
            <Info label="الفريق المسؤول" value={p.ownerTeamName ?? '—'} />
            <Info label="مدير الحساب" value={p.accountManagerName ?? '—'} />
            <Info label="تاريخ البداية" value={formatDate(p.startDate)} />
            <Info label="تاريخ النهاية" value={formatDate(p.endDate)} />
            <Info label="تاريخ الإضافة" value={formatDate(p.createdAtUtc)} />
          </dl>
          {p.notes && (
            <div className="mt-4 rounded-lg bg-offwhite p-3 text-sm text-ink-2">
              <span className="font-semibold text-ink">ملاحظات: </span>
              {p.notes}
            </div>
          )}
        </Card>
      )}

      {s && (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
          <StatCard label="إجمالي التقارير" value={s.totalReports} />
          <StatCard label="مُغلقة" value={s.closedReports} />
          <StatCard label="قيد المعالجة" value={s.pendingReports} tone={s.pendingReports > 0 ? 'alert' : 'navy'} />
          <StatCard label="مخاطر مفتوحة" value={s.openRiskCount} tone={s.openRiskCount > 0 ? 'alert' : 'navy'} />
          <StatCard label="ملاحظات مفتوحة" value={s.openNoteCount} />
        </div>
      )}
      {s?.lastReportAtUtc && (
        <p className="text-sm text-ink-2">آخر تقرير مرتبط: {formatDate(s.lastReportAtUtc)}</p>
      )}

      <LinkedReportsCard rows={reportRows} title="تقارير المشروع المرتبطة" />
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

function ArchiveProjectButton({ id }: { id: string }) {
  const archive = useArchiveProject();
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  if (!confirm)
    return (
      <Button variant="danger" onClick={() => setConfirm(true)}>
        أرشفة المشروع
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

function EditProjectForm({
  project,
  onDone,
}: {
  project: import('../types/api').ProjectDto;
  onDone: () => void;
}) {
  const update = useUpdateProject();
  const teams = useTeams();
  const users = useDirectoryUsers();
  const [name, setName] = useState(project.name);
  const [serviceType, setServiceType] = useState<ServiceType>(project.serviceType);
  const [status, setStatus] = useState<ProjectStatus>(project.status);
  const [startDate, setStartDate] = useState(project.startDate ?? '');
  const [endDate, setEndDate] = useState(project.endDate ?? '');
  const [ownerTeamId, setOwnerTeamId] = useState(project.ownerTeamId ?? '');
  const [accountManagerId, setAccountManagerId] = useState(project.accountManagerId ?? '');
  const [notes, setNotes] = useState(project.notes ?? '');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم المشروع مطلوب.');
      return;
    }
    const req: UpdateProjectRequest = {
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
      await update.mutateAsync({ id: project.id, req });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ التعديلات.'));
    }
  }

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-bold text-navy">تعديل المشروع</h2>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="اسم المشروع">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="نوع الخدمة">
          <Select value={serviceType} onChange={(e) => setServiceType(e.target.value as ServiceType)}>
            {SERVICE_TYPES.map((t) => (
              <option key={t} value={t}>
                {serviceTypeLabel[t]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الحالة">
          <Select value={status} onChange={(e) => setStatus(e.target.value as ProjectStatus)}>
            {(['Active', 'Paused', 'Completed', 'AtRisk', 'Closed'] as ProjectStatus[]).map((st) => (
              <option key={st} value={st}>
                {projectStatusLabel[st]}
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

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
import { useTaxonomyOptionDetails } from '../lib/useExecutionTaxonomy';
import {
  useProjectWorkstreams,
  useCreateProjectWorkstream,
  useUpdateProjectWorkstream,
  useActivateProjectWorkstream,
  useDeactivateProjectWorkstream,
} from '../lib/useProjectWorkstreams';
import {
  useWorkstreamDeliverables,
  useCreateWorkstreamDeliverable,
  useUpdateWorkstreamDeliverable,
  useActivateWorkstreamDeliverable,
  useDeactivateWorkstreamDeliverable,
} from '../lib/useWorkstreamDeliverables';
import { useAuth } from '../lib/auth';
import { Card, Badge, Button, StatCard, Field, Input, Select, Alert } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { LinkedReportsCard } from './ClientDetailPage';
import {
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  workstreamStatusLabel,
  workstreamStatusTone,
  deliverablePriorityLabel,
  deliverablePriorityTone,
  formatDate,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type {
  ProjectStatus,
  ServiceType,
  UpdateProjectRequest,
  ProjectWorkstreamDto,
  WorkstreamStatus,
  WorkstreamDeliverableDto,
  DeliverablePriority,
} from '../types/api';

const WORKSTREAM_STATUSES: WorkstreamStatus[] = ['Active', 'Paused', 'Completed', 'Cancelled'];
const DELIVERABLE_PRIORITIES: DeliverablePriority[] = ['Low', 'Medium', 'High', 'Urgent'];

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

  // **قدرة محسوبة خادمًا لا مشتقّة من الدور (R2.1 §7-16)**: كانت هذه السطور تعيد كتابة شرط
  // `CanManagePlanAsync` في المتصفّح فتُسقِط قائد الفريق ومالك المشروع، فيرى الاثنان شاشة
  // أهداف العمل بلا أيّ زرّ بينما الخادم يقبل كتابتهما. `canOperate` يأتي من الخادم بنفس
  // الحرّاس التي تحكم الطلب ⟹ لا يمكن أن تفترق الشاشة عن الردّ.
  const canManagePlan = p.canOperate;

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
            {/* مدخل مساحة عمل Project 360 (CPW-R3 · R2-W12) — صفحة مستقلّة لا تبويب هنا،
                كي تبقى هذه الصفحة وسجلّاتها القائمة كما هي بلا مساس. */}
            <Link
              to={`/app/projects/${p.id}/360`}
              className="text-sm font-medium text-orange-600 hover:underline"
            >
              مساحة عمل المشروع (360)
            </Link>
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
            <Info label="مدير العميل" value={p.accountManagerName ?? '—'} />
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

      <WorkstreamsCard projectId={p.id} canManage={canManagePlan} canManagePlan={canManagePlan} />

      <LinkedReportsCard rows={reportRows} title="تقارير المشروع المرتبطة" />
    </div>
  );
}

// ===== أهداف العمل داخل المشروع (P1 workstreams + P2 خطّة الإنتاج) =====
function WorkstreamsCard({
  projectId,
  canManage,
  canManagePlan,
}: {
  projectId: string;
  canManage: boolean;
  canManagePlan: boolean;
}) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const [adding, setAdding] = useState(false);
  const workstreams = useProjectWorkstreams(projectId, includeInactive);
  const rows = workstreams.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-bold text-navy">أهداف العمل داخل المشروع</h2>
          <p className="text-xs text-ink-2">
            حدد الفرق المشاركة، وما هو المطلوب من كل هدف عمل داخل المشروع.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-1 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            إظهار المعطّلة
          </label>
          {canManage && !adding && (
            <Button variant="primary" onClick={() => setAdding(true)}>
              إضافة هدف عمل
            </Button>
          )}
        </div>
      </div>

      {canManage && adding && (
        <WorkstreamForm projectId={projectId} onDone={() => setAdding(false)} />
      )}

      {workstreams.isLoading ? (
        <LoadingState label="يتم تحميل أهداف العمل…" />
      ) : workstreams.isError ? (
        <QueryError
          onRetry={() => workstreams.refetch()}
          title="تعذّر عرض أهداف العمل"
          description="قد يكون المشروع خارج نطاق صلاحيتك."
        />
      ) : rows.length === 0 ? (
        <p className="rounded-lg bg-offwhite p-4 text-sm text-ink-2">
          لا توجد أهداف عمل لهذا المشروع بعد.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-line text-right text-xs text-ink-2">
                <th className="p-2 font-medium">هدف العمل</th>
                <th className="p-2 font-medium">النوع</th>
                <th className="p-2 font-medium">الفريق المسؤول</th>
                <th className="p-2 font-medium">مدير التنفيذ</th>
                <th className="p-2 font-medium">الحالة</th>
                <th className="p-2 font-medium">الترتيب</th>
                {canManage && <th className="p-2 font-medium">إجراءات</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((w) => (
                <WorkstreamRow
                  key={w.id}
                  projectId={projectId}
                  workstream={w}
                  canManage={canManage}
                  canManagePlan={canManagePlan}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function WorkstreamRow({
  projectId,
  workstream,
  canManage,
  canManagePlan,
}: {
  projectId: string;
  workstream: ProjectWorkstreamDto;
  canManage: boolean;
  canManagePlan: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const activate = useActivateProjectWorkstream(projectId);
  const deactivate = useDeactivateProjectWorkstream(projectId);
  const w = workstream;
  const colSpan = canManage ? 7 : 6;

  if (editing) {
    return (
      <tr className="border-b border-line align-top">
        <td colSpan={colSpan} className="p-2">
          <WorkstreamForm projectId={projectId} existing={w} onDone={() => setEditing(false)} />
        </td>
      </tr>
    );
  }

  return (
    <>
      <tr className={`border-b border-line ${w.isActive ? '' : 'opacity-60'}`}>
        <td className="p-2 font-medium text-ink">
          <button
            type="button"
            className="ml-1 text-xs text-navy hover:underline"
            onClick={() => setExpanded((v) => !v)}
          >
            {expanded ? '▾' : '▸'}
          </button>
          {w.name}
          {!w.isActive && <span className="mr-1 text-xs text-ink-2"> (معطّل)</span>}
          {w.notes && <div className="text-xs font-normal text-ink-2">{w.notes}</div>}
        </td>
        <td className="p-2 text-ink-2">{w.workstreamTypeNameAr ?? '—'}</td>
        <td className="p-2 text-ink-2">{w.responsibleTeamName ?? '—'}</td>
        <td className="p-2 text-ink-2">{w.responsibleManagerName ?? '—'}</td>
        <td className="p-2">
          <Badge tone={workstreamStatusTone(w.status)}>{workstreamStatusLabel[w.status]}</Badge>
        </td>
        <td className="p-2 text-ink-2">{w.sortOrder}</td>
        {canManage && (
          <td className="p-2">
            <div className="flex gap-1">
              <Button variant="ghost" onClick={() => setEditing(true)}>
                تعديل
              </Button>
              {w.isActive ? (
                <Button
                  variant="ghost"
                  onClick={() => deactivate.mutate(w.id)}
                  disabled={deactivate.isPending}
                >
                  تعطيل
                </Button>
              ) : (
                <Button
                  variant="ghost"
                  onClick={() => activate.mutate(w.id)}
                  disabled={activate.isPending}
                >
                  تفعيل
                </Button>
              )}
            </div>
          </td>
        )}
      </tr>
      {expanded && (
        <tr className="border-b border-line bg-offwhite/60">
          <td colSpan={colSpan} className="p-3">
            <DeliverablesPanel
              projectId={projectId}
              workstreamId={w.id}
              workstreamActive={w.isActive}
              canManage={canManagePlan}
            />
          </td>
        </tr>
      )}
    </>
  );
}

// ===== P2: المخرجات المطلوبة داخل هدف العمل =====
// كل صفّ = مجموعة مخرَج مخطَّطة (Planned Deliverable Group) ذات معرّف ثابت، لا عنصر منفَّذ.
// تخطيط فقط: لا تُعرَض أعمدة «المنفّذ/المتبقّي/نسبة الإنجاز» — يظهر التقدم الفعلي بعد تفعيل تقارير التنفيذ v5.
function DeliverablesPanel({
  projectId,
  workstreamId,
  workstreamActive,
  canManage,
}: {
  projectId: string;
  workstreamId: string;
  workstreamActive: boolean;
  canManage: boolean;
}) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const [adding, setAdding] = useState(false);
  const deliverables = useWorkstreamDeliverables(projectId, workstreamId, includeInactive);
  const rows = deliverables.data ?? [];

  // ملخّص تخطيطيّ فقط (لا بيانات تنفيذ). يُحتسَب من المخرَجات النشطة.
  const activeRows = rows.filter((d) => d.isActive);
  const distinctTypeCount = new Set(activeRows.map((d) => d.deliverableTypeCode)).size;
  const totalPlannedQty = activeRows.reduce((sum, d) => sum + (d.plannedQuantity || 0), 0);
  const totalEstimatedHours = activeRows.reduce((sum, d) => sum + (d.estimatedHours ?? 0), 0);
  const nearestDueDate = activeRows
    .map((d) => d.dueDate)
    .filter((x): x is string => !!x)
    .sort()[0];

  return (
    <div className="space-y-3 rounded-lg border border-line bg-white p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-bold text-navy">المخرجات المطلوبة</h3>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-1 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            إظهار المعطّلة
          </label>
          {canManage && !adding && (
            <Button
              variant="primary"
              onClick={() => setAdding(true)}
              disabled={!workstreamActive}
              title={!workstreamActive ? 'هدف العمل معطّل — فعّله لإضافة مخرَجات.' : undefined}
            >
              إضافة مخرَج
            </Button>
          )}
        </div>
      </div>

      {rows.length > 0 && (
        <>
          <div className="grid grid-cols-2 gap-2 text-center lg:grid-cols-5">
            <SummaryTile label="عدد أنواع المخرجات" value={distinctTypeCount} />
            <SummaryTile label="إجمالي الكمية المخطَّطة" value={totalPlannedQty} />
            <SummaryTile label="إجمالي الساعات المقدَّرة" value={totalEstimatedHours} />
            <SummaryTile label="عدد المخرجات النشطة" value={activeRows.length} />
            <SummaryTile label="أقرب موعد تسليم" value={nearestDueDate ? formatDate(nearestDueDate) : '—'} />
          </div>
          <p className="rounded-lg bg-offwhite p-2 text-xs text-ink-2">
            سيظهر التقدم الفعلي والمنفذ والمتبقي بعد تفعيل تقارير التنفيذ.
          </p>
        </>
      )}

      {canManage && adding && workstreamActive && (
        <DeliverableForm
          projectId={projectId}
          workstreamId={workstreamId}
          onDone={() => setAdding(false)}
        />
      )}

      {deliverables.isLoading ? (
        <LoadingState label="يتم تحميل المخرَجات…" />
      ) : deliverables.isError ? (
        <QueryError
          onRetry={() => deliverables.refetch()}
          title="تعذّر عرض المخرَجات"
          description="قد يكون هدف العمل خارج نطاق صلاحيتك."
        />
      ) : rows.length === 0 ? (
        <p className="rounded-lg bg-offwhite p-3 text-sm text-ink-2">
          لا توجد مخرَجات في خطّة إنتاج هذا الهدف بعد.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-line text-right text-xs text-ink-2">
                <th className="p-2 font-medium">اسم المخرج</th>
                <th className="p-2 font-medium">نوع المخرج</th>
                <th className="p-2 font-medium">الاستخدام</th>
                <th className="p-2 font-medium">الكمية المخططة</th>
                <th className="p-2 font-medium">الساعات المقدرة</th>
                <th className="p-2 font-medium">تاريخ البداية</th>
                <th className="p-2 font-medium">تاريخ الاستحقاق</th>
                <th className="p-2 font-medium">الأولوية</th>
                <th className="p-2 font-medium">المسؤول</th>
                <th className="p-2 font-medium">الحالة</th>
                {canManage && <th className="p-2 font-medium">الإجراءات</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((d) => (
                <DeliverableRow
                  key={d.id}
                  projectId={projectId}
                  workstreamId={workstreamId}
                  deliverable={d}
                  canManage={canManage}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function DeliverableRow({
  projectId,
  workstreamId,
  deliverable,
  canManage,
}: {
  projectId: string;
  workstreamId: string;
  deliverable: WorkstreamDeliverableDto;
  canManage: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const activate = useActivateWorkstreamDeliverable(projectId, workstreamId);
  const deactivate = useDeactivateWorkstreamDeliverable(projectId, workstreamId);
  const d = deliverable;
  const colSpan = canManage ? 11 : 10;

  if (editing) {
    return (
      <tr className="border-b border-line align-top">
        <td colSpan={colSpan} className="p-2">
          <DeliverableForm
            projectId={projectId}
            workstreamId={workstreamId}
            existing={d}
            onDone={() => setEditing(false)}
          />
        </td>
      </tr>
    );
  }

  return (
    <tr className={`border-b border-line ${d.isActive ? '' : 'opacity-60'}`}>
      <td className="p-2 font-medium text-ink">
        {d.name}
        {d.notes && <div className="text-xs font-normal text-ink-2">{d.notes}</div>}
      </td>
      <td className="p-2 text-ink-2">{d.deliverableTypeNameAr ?? '—'}</td>
      <td className="p-2 text-ink-2">{d.usageContextNameAr ?? '—'}</td>
      <td className="p-2 text-ink-2">{d.plannedQuantity}</td>
      <td className="p-2 text-ink-2">{d.estimatedHours ?? '—'}</td>
      <td className="p-2 text-ink-2">{formatDate(d.startDate)}</td>
      <td className="p-2 text-ink-2">{formatDate(d.dueDate)}</td>
      <td className="p-2">
        <Badge tone={deliverablePriorityTone(d.priority)}>{deliverablePriorityLabel[d.priority]}</Badge>
      </td>
      <td className="p-2 text-ink-2">{d.responsibleUserName ?? '—'}</td>
      <td className="p-2">
        <Badge tone={d.isActive ? 'success' : 'muted'}>{d.isActive ? 'نشط' : 'معطّل'}</Badge>
      </td>
      {canManage && (
        <td className="p-2">
          <div className="flex gap-1">
            <Button variant="ghost" onClick={() => setEditing(true)}>
              تعديل
            </Button>
            {d.isActive ? (
              <Button
                variant="ghost"
                onClick={() => deactivate.mutate(d.id)}
                disabled={deactivate.isPending}
              >
                تعطيل
              </Button>
            ) : (
              <Button
                variant="ghost"
                onClick={() => activate.mutate(d.id)}
                disabled={activate.isPending}
              >
                تفعيل
              </Button>
            )}
          </div>
        </td>
      )}
    </tr>
  );
}

function DeliverableForm({
  projectId,
  workstreamId,
  existing,
  onDone,
}: {
  projectId: string;
  workstreamId: string;
  existing?: WorkstreamDeliverableDto;
  onDone: () => void;
}) {
  const create = useCreateWorkstreamDeliverable(projectId, workstreamId);
  const update = useUpdateWorkstreamDeliverable(projectId, workstreamId);
  const types = useTaxonomyOptionDetails('deliverable');
  const usages = useTaxonomyOptionDetails('usage_context');
  const users = useDirectoryUsers();

  const [deliverableTypeCode, setDeliverableTypeCode] = useState(existing?.deliverableTypeCode ?? '');
  const [usageContextCode, setUsageContextCode] = useState(existing?.usageContextCode ?? '');
  const [name, setName] = useState(existing?.name ?? '');
  const [plannedQuantity, setPlannedQuantity] = useState(existing?.plannedQuantity ?? 1);
  const [estimatedHours, setEstimatedHours] = useState(
    existing?.estimatedHours != null ? String(existing.estimatedHours) : '',
  );
  const [startDate, setStartDate] = useState(existing?.startDate ?? '');
  const [dueDate, setDueDate] = useState(existing?.dueDate ?? '');
  const [priority, setPriority] = useState<DeliverablePriority>(existing?.priority ?? 'Medium');
  const [responsibleUserId, setResponsibleUserId] = useState(existing?.responsibleUserId ?? '');
  const [sortOrder, setSortOrder] = useState(existing?.sortOrder ?? 0);
  const [notes, setNotes] = useState(existing?.notes ?? '');
  const [err, setErr] = useState<string | null>(null);

  const isEdit = !!existing;

  async function submit() {
    setErr(null);
    if (!isEdit && !deliverableTypeCode) {
      setErr('نوع المخرَج مطلوب.');
      return;
    }
    if (plannedQuantity <= 0) {
      setErr('الكمية المخطَّطة يجب أن تكون أكبر من صفر.');
      return;
    }
    const hours = estimatedHours.trim() ? Number(estimatedHours) : null;
    if (hours != null && (Number.isNaN(hours) || hours < 0)) {
      setErr('الساعات المقدَّرة لا يمكن أن تكون سالبة.');
      return;
    }
    if (startDate && dueDate && dueDate < startDate) {
      setErr('تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ البداية.');
      return;
    }
    try {
      if (isEdit) {
        await update.mutateAsync({
          id: existing!.id,
          req: {
            usageContextCode: usageContextCode || null,
            name: name.trim() || null,
            plannedQuantity,
            estimatedHours: hours,
            startDate: startDate || null,
            dueDate: dueDate || null,
            priority,
            responsibleUserId: responsibleUserId || null,
            notes: notes.trim() || null,
            sortOrder,
          },
        });
      } else {
        await create.mutateAsync({
          deliverableTypeCode,
          usageContextCode: usageContextCode || null,
          name: name.trim() || null,
          plannedQuantity,
          estimatedHours: hours,
          startDate: startDate || null,
          dueDate: dueDate || null,
          priority,
          responsibleUserId: responsibleUserId || null,
          notes: notes.trim() || null,
          sortOrder,
        });
      }
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ المخرَج.'));
    }
  }

  const pending = create.isPending || update.isPending;

  return (
    <div className="space-y-3 rounded-lg border border-line bg-offwhite p-4">
      <h4 className="text-sm font-bold text-navy">{isEdit ? 'تعديل مخرج' : 'إضافة مخرج'}</h4>
      {err && <Alert tone="alert">{err}</Alert>}

      <fieldset className="space-y-3">
        <legend className="text-xs font-bold text-ink-2">التعريف</legend>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="نوع المخرج (مطلوب)">
            <Select
              value={deliverableTypeCode}
              onChange={(e) => setDeliverableTypeCode(e.target.value)}
              disabled={isEdit}
            >
              <option value="">— اختر النوع —</option>
              {(types.data ?? []).map((t) => (
                <option key={t.code} value={t.code}>
                  {t.nameAr}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="الاستخدام (اختياري)">
            <Select value={usageContextCode} onChange={(e) => setUsageContextCode(e.target.value)}>
              <option value="">— بدون —</option>
              {(usages.data ?? []).map((u) => (
                <option key={u.code} value={u.code}>
                  {u.nameAr}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="اسم المخرج (اختياري — يتبع اسم النوع افتراضيًّا)">
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </Field>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-bold text-ink-2">التخطيط</legend>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="الكمية المخططة (مطلوب)">
            <Input
              type="number"
              value={plannedQuantity}
              onChange={(e) => setPlannedQuantity(Number(e.target.value) || 0)}
            />
          </Field>
          <Field label="الساعات المقدرة (اختياري)">
            <Input
              type="number"
              step="0.5"
              value={estimatedHours}
              onChange={(e) => setEstimatedHours(e.target.value)}
            />
          </Field>
          <Field label="تاريخ البداية (اختياري)">
            <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </Field>
          <Field label="تاريخ الاستحقاق (اختياري)">
            <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </Field>
          <Field label="الأولوية">
            <Select value={priority} onChange={(e) => setPriority(e.target.value as DeliverablePriority)}>
              {DELIVERABLE_PRIORITIES.map((pr) => (
                <option key={pr} value={pr}>
                  {deliverablePriorityLabel[pr]}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-bold text-ink-2">المسؤولية</legend>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="المسؤول (اختياري)">
            <Select value={responsibleUserId} onChange={(e) => setResponsibleUserId(e.target.value)}>
              <option value="">— بدون —</option>
              {(users.data ?? []).map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-bold text-ink-2">إضافي</legend>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="الترتيب">
            <Input
              type="number"
              value={sortOrder}
              onChange={(e) => setSortOrder(Number(e.target.value) || 0)}
            />
          </Field>
          <Field label="الملاحظات">
            <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Field>
        </div>
      </fieldset>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={pending}>
          {pending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

function WorkstreamForm({
  projectId,
  existing,
  onDone,
}: {
  projectId: string;
  existing?: ProjectWorkstreamDto;
  onDone: () => void;
}) {
  const create = useCreateProjectWorkstream(projectId);
  const update = useUpdateProjectWorkstream(projectId);
  const types = useTaxonomyOptionDetails('workstream_type');
  const teams = useTeams();
  const users = useDirectoryUsers();

  const [workstreamTypeCode, setWorkstreamTypeCode] = useState(existing?.workstreamTypeCode ?? '');
  const [name, setName] = useState(existing?.name ?? '');
  const [responsibleTeamId, setResponsibleTeamId] = useState(existing?.responsibleTeamId ?? '');
  const [responsibleManagerId, setResponsibleManagerId] = useState(existing?.responsibleManagerId ?? '');
  const [status, setStatus] = useState<WorkstreamStatus>(existing?.status ?? 'Active');
  const [sortOrder, setSortOrder] = useState(existing?.sortOrder ?? 0);
  const [notes, setNotes] = useState(existing?.notes ?? '');
  const [err, setErr] = useState<string | null>(null);

  const isEdit = !!existing;

  async function submit() {
    setErr(null);
    if (!isEdit && !workstreamTypeCode) {
      setErr('نوع هدف العمل مطلوب.');
      return;
    }
    if (!responsibleTeamId) {
      setErr('الفريق المسؤول مطلوب.');
      return;
    }
    try {
      if (isEdit) {
        await update.mutateAsync({
          id: existing!.id,
          req: {
            responsibleTeamId,
            status,
            name: name.trim() || null,
            responsibleManagerId: responsibleManagerId || null,
            sortOrder,
            notes: notes.trim() || null,
          },
        });
      } else {
        await create.mutateAsync({
          workstreamTypeCode,
          responsibleTeamId,
          name: name.trim() || null,
          responsibleManagerId: responsibleManagerId || null,
          sortOrder,
          notes: notes.trim() || null,
        });
      }
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ هدف العمل.'));
    }
  }

  const pending = create.isPending || update.isPending;

  return (
    <div className="space-y-3 rounded-lg border border-line bg-offwhite p-4">
      <h3 className="text-sm font-bold text-navy">{isEdit ? 'تعديل هدف العمل' : 'إضافة هدف عمل'}</h3>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="نوع هدف العمل">
          <Select
            value={workstreamTypeCode}
            onChange={(e) => setWorkstreamTypeCode(e.target.value)}
            disabled={isEdit}
          >
            <option value="">— اختر النوع —</option>
            {(types.data ?? []).map((t) => (
              <option key={t.code} value={t.code}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الاسم (اختياري — يتبع اسم النوع افتراضيًّا)">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="الفريق المسؤول">
          <Select value={responsibleTeamId} onChange={(e) => setResponsibleTeamId(e.target.value)}>
            <option value="">— اختر الفريق —</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مدير التنفيذ (اختياري)">
          <Select value={responsibleManagerId} onChange={(e) => setResponsibleManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        {isEdit && (
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as WorkstreamStatus)}>
              {WORKSTREAM_STATUSES.map((st) => (
                <option key={st} value={st}>
                  {workstreamStatusLabel[st]}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Field label="الترتيب">
          <Input
            type="number"
            value={sortOrder}
            onChange={(e) => setSortOrder(Number(e.target.value) || 0)}
          />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={pending}>
          {pending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
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

// بطاقة ملخّص تخطيطيّ داخل هدف العمل — أرقام مخطَّطة فقط (لا تنفيذ/منفّذ/متبقّي/نسبة إنجاز).
function SummaryTile({
  label,
  value,
  hint,
}: {
  label: string;
  value: React.ReactNode;
  hint?: string;
}) {
  return (
    <div className="rounded-lg border border-line bg-offwhite p-2" title={hint}>
      <div className="text-[11px] text-ink-2">{label}</div>
      <div className="mt-0.5 text-sm font-bold text-navy">{value}</div>
      {hint && <div className="mt-0.5 text-[10px] text-ink-2">{hint}</div>}
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
  const [projectOwnerId, setProjectOwnerId] = useState(project.projectOwnerId ?? '');
  const [teamLeaderId, setTeamLeaderId] = useState(project.teamLeaderId ?? '');
  const [notes, setNotes] = useState(project.notes ?? '');
  const [err, setErr] = useState<string | null>(null);

  // `/directory/users` مُنطَّق بالصلاحيّة: مديرٌ قد لا يرى فيه مَن أُسنِد إليه المشروع أصلًا
  // (مثال مقيس على TEST: 4 مستخدمين للمدير مقابل 17 للمدير العامّ). وبلا خيارٍ مقابلٍ للقيمة
  // القائمة يعرض المنتقي «— بدون —» على إسنادٍ **موجود فعلًا**، فيكذب النموذج على من يقرؤه.
  // الاسم معروف سلفًا من `ProjectDto` نفسه، فإضافته خيارًا لا تكشف شيئًا لم يُكشف.
  const withAssigned = (id: string, fullName: string | null) => {
    const list = users.data ?? [];
    return id && !list.some((u) => u.id === id) ? [{ id, fullName: fullName ?? id }, ...list] : list;
  };

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
      projectOwnerId: projectOwnerId || null,
      teamLeaderId: teamLeaderId || null,
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
        <Field label="مدير العميل">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {withAssigned(accountManagerId, project.accountManagerName).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مالك المشروع">
          <Select value={projectOwnerId} onChange={(e) => setProjectOwnerId(e.target.value)}>
            <option value="">— بدون —</option>
            {withAssigned(projectOwnerId, project.projectOwnerName).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="قائد الفريق">
          <Select value={teamLeaderId} onChange={(e) => setTeamLeaderId(e.target.value)}>
            <option value="">— بدون —</option>
            {withAssigned(teamLeaderId, project.teamLeaderName).map((u) => (
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

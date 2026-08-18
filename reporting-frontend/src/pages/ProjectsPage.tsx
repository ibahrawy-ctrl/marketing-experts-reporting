// صفحة المشاريع — قائمة شاملة بكل المشاريع ضمن النطاق مع فلاتر (الحالة/الخدمة/العميل).
import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useProjects,
  useArchiveProject,
  useReactivateProject,
  useDeleteProject,
} from '../lib/useClients';
import { useAuth } from '../lib/auth';
import { Card, Badge, Select, Button, StatCard, Alert, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  formatDate,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type { ProjectDto, ProjectStatus, ServiceType } from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

// عرض القائمة: النشط (غير المؤرشفة) / المؤرشف (Closed فقط) / الكل.
type ProjectView = 'active' | 'archived' | 'all';

export default function ProjectsPage() {
  // أعمدة الإجراءات هنا كلّها بنيويّة (أرشفة/إعادة تفعيل/حذف) ⟹ تُقاس بسياسة الخادم
  // `Policies.ProjectStructuralManage` لا بـ`canManageClients` التي تضمّ TeamLeader (R2.1 · GAP-R21-06).
  const { canManageProjectStructure } = useAuth();
  const [view, setView] = useState<ProjectView>('active');
  const [statusFilter, setStatusFilter] = useState<ProjectStatus | ''>('');
  const [serviceFilter, setServiceFilter] = useState<ServiceType | ''>('');

  const projects = useProjects({
    status: view === 'archived' ? 'Closed' : statusFilter || undefined,
    serviceType: serviceFilter || undefined,
    includeClosed: view !== 'active',
  });

  if (projects.isLoading) return <LoadingState label="يتم تحميل المشاريع…" />;
  if (projects.isError)
    return (
      <QueryError
        onRetry={() => projects.refetch()}
        description="حدث خطأ أثناء جلب بيانات المشاريع. أعد المحاولة."
      />
    );

  const rows = projects.data ?? [];
  const active = rows.filter((p) => p.status === 'Active').length;
  const atRisk = rows.filter((p) => p.status === 'AtRisk').length;
  const completed = rows.filter((p) => p.status === 'Completed').length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">المشاريع</h1>
        <p className="mt-1 text-sm text-ink-2">
          كل المشاريع ضمن نطاق صلاحيتك. تُدار المشاريع من صفحة تفاصيل كل عميل.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي المشاريع" value={rows.length} />
        <StatCard label="نشِطة" value={active} />
        <StatCard label="مكتملة" value={completed} />
        <StatCard label="في خطر" value={atRisk} tone={atRisk > 0 ? 'alert' : 'navy'} />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Select value={view} onChange={(e) => setView(e.target.value as ProjectView)} className="max-w-xs">
          <option value="active">النشط</option>
          <option value="archived">المؤرشف</option>
          <option value="all">الكل</option>
        </Select>
        {view !== 'archived' && (
          <Select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as ProjectStatus | '')}
            className="max-w-xs"
          >
            <option value="">كل الحالات</option>
            {(['Active', 'Paused', 'Completed', 'AtRisk'] as ProjectStatus[]).map((s) => (
              <option key={s} value={s}>
                {projectStatusLabel[s]}
              </option>
            ))}
          </Select>
        )}
        <Select
          value={serviceFilter}
          onChange={(e) => setServiceFilter(e.target.value as ServiceType | '')}
          className="max-w-xs"
        >
          <option value="">كل الخدمات</option>
          {SERVICE_TYPES.map((s) => (
            <option key={s} value={s}>
              {serviceTypeLabel[s]}
            </option>
          ))}
        </Select>
        <span className="text-xs text-ink-3">«مؤرشف» يقابل الحالة Closed داخليًا.</span>
      </div>

      {rows.length === 0 ? (
        <EmptyState
          title="لا توجد مشاريع"
          description="لا يوجد مشروع ضمن نطاقك يطابق الفلتر الحالي."
        />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[760px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">المشروع</th>
                <th className="px-3 py-2.5 font-semibold">العميل</th>
                <th className="px-3 py-2.5 font-semibold">الخدمة</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">الفريق</th>
                <th className="px-3 py-2.5 font-semibold">البداية</th>
                <th className="px-3 py-2.5 font-semibold">النهاية</th>
                <th className="px-3 py-2.5 font-semibold"></th>
                {canManageProjectStructure && <th className="px-3 py-2.5 font-semibold">إجراءات</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <ProjectRow key={p.id} project={p} canManage={canManageProjectStructure} />
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}

function ProjectRow({ project: p, canManage }: { project: ProjectDto; canManage: boolean }) {
  const archive = useArchiveProject();
  const reactivate = useReactivateProject();
  const del = useDeleteProject();
  const [err, setErr] = useState<string | null>(null);
  const isArchived = p.status === 'Closed';
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
        <td className="px-3 py-2.5 font-semibold text-navy">{p.name}</td>
        <td className="px-3 py-2.5 text-ink-2">
          {p.clientName ? (
            <Link to={`/app/clients/${p.clientId}`} className="hover:underline">
              {p.clientName}
            </Link>
          ) : (
            '—'
          )}
        </td>
        <td className="px-3 py-2.5 text-ink-2">{serviceTypeLabel[p.serviceType]}</td>
        <td className="px-3 py-2.5">
          <Badge tone={projectStatusTone(p.status)}>{projectStatusLabel[p.status]}</Badge>
        </td>
        <td className="px-3 py-2.5 text-ink-2">{p.ownerTeamName ?? '—'}</td>
        <td className="px-3 py-2.5 text-ink-2">{formatDate(p.startDate)}</td>
        <td className="px-3 py-2.5 text-ink-2">{formatDate(p.endDate)}</td>
        <td className="px-3 py-2.5">
          <Link to={`/app/projects/${p.id}`} className="text-sm font-semibold text-orange-600 hover:underline">
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
                    if (window.confirm(`أرشفة المشروع «${p.name}»؟ يمكن إعادة تفعيله لاحقًا.`))
                      run(() => archive.mutateAsync(p.id), 'تعذّرت الأرشفة.');
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
                    onClick={() => run(() => reactivate.mutateAsync(p.id), 'تعذّرت إعادة التفعيل.')}
                  >
                    إعادة تفعيل
                  </Button>
                  <Button
                    variant="ghost"
                    className="!px-2 !py-1 text-xs !text-alert"
                    disabled={busy || !p.canHardDelete}
                    title={!p.canHardDelete ? p.deleteBlockReason ?? undefined : undefined}
                    onClick={() => {
                      if (window.confirm(`حذف المشروع «${p.name}» نهائيًا؟ لا يمكن التراجع.`))
                        run(() => del.mutateAsync(p.id), 'تعذّر الحذف النهائي.');
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
      {isArchived && !p.canHardDelete && p.deleteBlockReason && canManage && (
        <tr>
          <td colSpan={9} className="px-3 pb-2 pt-0">
            <p className="text-xs text-ink-2">{p.deleteBlockReason}</p>
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

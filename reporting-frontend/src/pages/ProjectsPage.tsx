// صفحة المشاريع — قائمة شاملة بكل المشاريع ضمن النطاق مع فلاتر (الحالة/الخدمة/العميل).
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useProjects } from '../lib/useClients';
import { Card, Badge, Select, StatCard, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  formatDate,
} from '../lib/format';
import type { ProjectStatus, ServiceType } from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

export default function ProjectsPage() {
  const [statusFilter, setStatusFilter] = useState<ProjectStatus | ''>('');
  const [serviceFilter, setServiceFilter] = useState<ServiceType | ''>('');
  const [includeClosed, setIncludeClosed] = useState(false);

  const projects = useProjects({
    status: statusFilter || undefined,
    serviceType: serviceFilter || undefined,
    includeClosed,
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
        <Select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as ProjectStatus | '')}
          className="max-w-xs"
        >
          <option value="">كل الحالات</option>
          {(['Active', 'Paused', 'Completed', 'AtRisk', 'Closed'] as ProjectStatus[]).map((s) => (
            <option key={s} value={s}>
              {projectStatusLabel[s]}
            </option>
          ))}
        </Select>
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
        <label className="flex items-center gap-2 text-sm text-ink-2">
          <input type="checkbox" checked={includeClosed} onChange={(e) => setIncludeClosed(e.target.checked)} />
          إظهار المغلقة
        </label>
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
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.id} className="border-b border-line last:border-0 hover:bg-offwhite">
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
        </Card>
      )}
    </div>
  );
}

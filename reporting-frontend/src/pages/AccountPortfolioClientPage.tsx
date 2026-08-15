import { Link, useParams } from 'react-router-dom';
import { Badge, Card, StatCard, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { serviceTypeLabel, projectStatusLabel, clientStatusLabel, formatDateTime } from '../lib/format';
import { useMyPortfolioClient } from '../lib/useAccountPortfolio';

// تفاصيل عميل في المحفظة — عرض فقط. العميل + مشاريع المستخدم المرئية التابعة له فقط (لا إدارة).
export default function AccountPortfolioClientPage() {
  const { id } = useParams<{ id: string }>();
  const clientQ = useMyPortfolioClient(id);

  if (clientQ.isLoading) return <LoadingState label="جارٍ تحميل العميل…" />;
  if (clientQ.isError || !clientQ.data)
    return <QueryError onRetry={() => clientQ.refetch()} description="تعذّر تحميل العميل أو أنه خارج نطاقك." />;

  const { client, projects } = clientQ.data;

  return (
    <div className="space-y-6">
      <div>
        <Link to="/app/account-portfolio" className="text-sm text-ink-3 hover:text-orange-600">
          ← العودة للمحفظة
        </Link>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-bold text-navy">{client.name}</h1>
          {/* جسر المحفظة ← ملفّ العميل الشامل (CPW-R2). Client 360 يبقى مصدر الحقيقة؛ لا تُكرَّر بياناته هنا. */}
          <Link
            to={`/app/clients/${client.id}`}
            className="rounded-lg bg-navy px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600"
          >
            فتح ملف العميل
          </Link>
        </div>
        <div className="mt-2">
          <Badge tone={client.status === 'Active' ? 'success' : 'muted'}>{clientStatusLabel[client.status]}</Badge>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <StatCard label="مشاريع المحفظة" value={client.projectCount} />
        <StatCard label="المشاريع النشطة" value={client.activeProjectCount} tone="success" />
      </div>

      <Card>
        <h2 className="mb-3 text-lg font-bold text-navy">مشاريع العميل</h2>
        {projects.length === 0 ? (
          <EmptyState title="لا توجد مشاريع" description="لا توجد مشاريع مرئية لك تابعة لهذا العميل." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead>
                <tr className="border-b border-line text-ink-3">
                  <th className="px-3 py-2 font-semibold">المشروع</th>
                  <th className="px-3 py-2 font-semibold">الخدمة</th>
                  <th className="px-3 py-2 font-semibold">الحالة</th>
                  <th className="px-3 py-2 font-semibold">المخرجات</th>
                  <th className="px-3 py-2 font-semibold">آخر مخرَج</th>
                </tr>
              </thead>
              <tbody>
                {projects.map((p) => (
                  <tr key={p.id} className="border-b border-line/60 hover:bg-navy-50">
                    <td className="px-3 py-2 font-medium">
                      <Link to={`/app/account-portfolio/projects/${p.id}`} className="text-navy hover:text-orange-600">
                        {p.name}
                      </Link>
                    </td>
                    <td className="px-3 py-2 text-ink-2">{serviceTypeLabel[p.serviceType]}</td>
                    <td className="px-3 py-2">
                      <Badge tone={p.status === 'Active' ? 'success' : 'muted'}>
                        {projectStatusLabel[p.status]}
                      </Badge>
                    </td>
                    <td className="px-3 py-2 text-ink-2">{p.outputCount}</td>
                    <td className="px-3 py-2 text-ink-2">{formatDateTime(p.lastOutputAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

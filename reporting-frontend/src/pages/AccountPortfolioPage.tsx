import { Link } from 'react-router-dom';
import { Badge, Card, StatCard, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { serviceTypeLabel, projectStatusLabel, formatDateTime } from '../lib/format';
import { useMyPortfolioProjects, useMyPortfolioClients } from '../lib/useAccountPortfolio';

// محفظة مدير الحساب (مشاريعي) — عرض فقط. لا إنشاء/تعديل/حذف/اعتماد، لا مسودّات، لا KPI.
// النطاق مفروض خادمًا على مشاريع المستخدم نفسه (Project.AccountManagerId == المستخدم).
export default function AccountPortfolioPage() {
  const projectsQ = useMyPortfolioProjects();
  const clientsQ = useMyPortfolioClients();

  if (projectsQ.isLoading || clientsQ.isLoading) return <LoadingState label="جارٍ تحميل المحفظة…" />;
  if (projectsQ.isError || !projectsQ.data || clientsQ.isError || !clientsQ.data)
    return (
      <QueryError
        onRetry={() => {
          projectsQ.refetch();
          clientsQ.refetch();
        }}
        description="تعذّر تحميل محفظة المشاريع."
      />
    );

  const projects = projectsQ.data;
  const clients = clientsQ.data;
  const activeCount = projects.filter((p) => p.status === 'Active').length;
  const needsFollowUp = projects.filter((p) => p.outputCount === 0).length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">مشاريعي</h1>
        <p className="mt-1 text-sm text-ink-2">
          عرض مشاريع وعملاء محفظتك ومخرجاتها المعتمَدة — عرض فقط، دون تعديل أو اعتماد.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي المشاريع" value={projects.length} />
        <StatCard label="العملاء" value={clients.length} />
        <StatCard label="المشاريع النشطة" value={activeCount} tone="success" />
        <StatCard label="بحاجة لمتابعة" value={needsFollowUp} tone={needsFollowUp > 0 ? 'alert' : 'navy'} />
      </div>

      <Card>
        <h2 className="mb-3 text-lg font-bold text-navy">مشاريع المحفظة</h2>
        {projects.length === 0 ? (
          <EmptyState
            title="لا توجد مشاريع"
            description="لا توجد مشاريع مسنَدة إليك حاليًا في المحفظة."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead>
                <tr className="border-b border-line text-ink-3">
                  <th className="px-3 py-2 font-semibold">المشروع</th>
                  <th className="px-3 py-2 font-semibold">العميل</th>
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
                    <td className="px-3 py-2 text-ink-2">
                      {p.clientName ? (
                        <Link to={`/app/account-portfolio/clients/${p.clientId}`} className="hover:text-orange-600">
                          {p.clientName}
                        </Link>
                      ) : (
                        '—'
                      )}
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

      <Card>
        <h2 className="mb-3 text-lg font-bold text-navy">عملاء المحفظة</h2>
        {clients.length === 0 ? (
          <EmptyState title="لا يوجد عملاء" description="لا يوجد عملاء مشتقّون من مشاريعك حاليًا." />
        ) : (
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
            {clients.map((c) => (
              <Link
                key={c.id}
                to={`/app/account-portfolio/clients/${c.id}`}
                className="rounded-xl border border-line bg-white p-4 transition hover:border-orange-300 hover:bg-navy-50"
              >
                <p className="font-bold text-navy">{c.name}</p>
                <p className="mt-1 text-xs text-ink-3">
                  {c.projectCount} مشروع · {c.activeProjectCount} نشط
                </p>
              </Link>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

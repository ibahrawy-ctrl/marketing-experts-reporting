import { Link, useParams } from 'react-router-dom';
import { Badge, Card, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  serviceTypeLabel,
  projectStatusLabel,
  submissionStatusLabel,
  periodTypeLabel,
  formatDate,
  formatDateTime,
} from '../lib/format';
import { useMyPortfolioProject, useMyPortfolioProjectOutputs } from '../lib/useAccountPortfolio';

// تفاصيل مشروع في المحفظة — عرض فقط. لا تعديل/حذف/أرشفة/اعتماد/إعادة.
// المخرجات معتمَدة فقط (تُستثنى المسودّة/المُعادة)، وحقول ملخّص آمنة فقط (لا قيم حقول التقرير الخام).
export default function AccountPortfolioProjectPage() {
  const { id } = useParams<{ id: string }>();
  const projectQ = useMyPortfolioProject(id);
  const outputsQ = useMyPortfolioProjectOutputs(id);

  if (projectQ.isLoading) return <LoadingState label="جارٍ تحميل المشروع…" />;
  if (projectQ.isError || !projectQ.data)
    return <QueryError onRetry={() => projectQ.refetch()} description="تعذّر تحميل المشروع أو أنه خارج نطاقك." />;

  const p = projectQ.data;

  return (
    <div className="space-y-6">
      <div>
        <Link to="/app/account-portfolio" className="text-sm text-ink-3 hover:text-orange-600">
          ← العودة للمحفظة
        </Link>
        <h1 className="mt-2 text-2xl font-bold text-navy">{p.name}</h1>
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <Badge tone={p.status === 'Active' ? 'success' : 'muted'}>{projectStatusLabel[p.status]}</Badge>
          <Badge tone="navy">{serviceTypeLabel[p.serviceType]}</Badge>
          {p.clientName && (
            <Link to={`/app/account-portfolio/clients/${p.clientId}`} className="text-sm text-navy hover:text-orange-600">
              {p.clientName}
            </Link>
          )}
        </div>
      </div>

      <Card>
        <div className="grid grid-cols-2 gap-4 text-sm md:grid-cols-4">
          <div>
            <p className="text-ink-3">تاريخ البدء</p>
            <p className="mt-1 font-semibold text-navy">{formatDate(p.startDate)}</p>
          </div>
          <div>
            <p className="text-ink-3">تاريخ الانتهاء</p>
            <p className="mt-1 font-semibold text-navy">{formatDate(p.endDate)}</p>
          </div>
          <div>
            <p className="text-ink-3">عدد المخرجات المعتمَدة</p>
            <p className="mt-1 font-semibold text-navy">{p.outputCount}</p>
          </div>
          <div>
            <p className="text-ink-3">آخر مخرَج</p>
            <p className="mt-1 font-semibold text-navy">{formatDateTime(p.lastOutputAtUtc)}</p>
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="mb-3 text-lg font-bold text-navy">المخرجات المعتمَدة</h2>
        {outputsQ.isLoading ? (
          <LoadingState label="جارٍ تحميل المخرجات…" />
        ) : outputsQ.isError || !outputsQ.data ? (
          <QueryError onRetry={() => outputsQ.refetch()} description="تعذّر تحميل المخرجات." />
        ) : outputsQ.data.length === 0 ? (
          <EmptyState
            title="لا توجد مخرجات معتمَدة"
            description="لا توجد تقارير معتمَدة مرتبطة بهذا المشروع حتى الآن."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead>
                <tr className="border-b border-line text-ink-3">
                  <th className="px-3 py-2 font-semibold">مقدِّم التقرير</th>
                  <th className="px-3 py-2 font-semibold">نوع الفترة</th>
                  <th className="px-3 py-2 font-semibold">الفترة</th>
                  <th className="px-3 py-2 font-semibold">الحالة</th>
                  <th className="px-3 py-2 font-semibold">تاريخ التسليم</th>
                </tr>
              </thead>
              <tbody>
                {outputsQ.data.map((o) => (
                  <tr key={o.submissionId} className="border-b border-line/60">
                    <td className="px-3 py-2 font-medium">{o.submitterName ?? '—'}</td>
                    <td className="px-3 py-2 text-ink-2">{periodTypeLabel[o.periodType]}</td>
                    <td className="px-3 py-2 text-ink-2">{o.periodKey}</td>
                    <td className="px-3 py-2">
                      <Badge tone="navy">{submissionStatusLabel[o.status]}</Badge>
                    </td>
                    <td className="px-3 py-2 text-ink-2">{formatDateTime(o.submittedAtUtc)}</td>
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

// الصفحة الرئيسية = موزّع لوحات حسب الدور.
// القاعدة: Role يحدّد شكل اللوحة، وReporting Line (الخادم) يحدّد البيانات.
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useAuth } from '../lib/auth';
import { roleLabel } from '../lib/format';
import { LoadingState, QueryError } from '../components/states';
import type { DashboardDto } from '../types/api';
import {
  EmployeeDashboard,
  TeamLeaderDashboard,
  ManagerDashboard,
  GMDashboard,
  CeoDashboard,
  CeoSupportDashboard,
} from './RoleDashboards';
import { AdminHome } from './AdminHome';

// دلتا KPI = الفرق بين آخر نقطتين في اتجاه KPI (للبطاقات ذات شارة المقارنة).
function kpiDeltaFrom(dash: DashboardDto): { value: number; up: boolean } | null {
  const data = (dash.widgets.find((w) => w.key === 'kpiTrend')?.data as { value: number }[]) ?? [];
  if (data.length < 2) return null;
  const last = data[data.length - 1].value;
  const prev = data[data.length - 2].value;
  return { value: Math.round(Math.abs(last - prev) * 10) / 10, up: last >= prev };
}

export default function HomePage() {
  const { user } = useAuth();

  const { data: dash, isLoading, isError, refetch } = useQuery({
    queryKey: ['dashboard-me'],
    queryFn: async () => (await api.get<DashboardDto>('/dashboard/me')).data,
  });

  if (isLoading) return <LoadingState label="يتم تحميل لوحتك…" />;
  if (isError || !dash)
    return (
      <QueryError
        onRetry={() => refetch()}
        title="تعذّر تحميل لوحة المعلومات"
        description="حدث خطأ أثناء جلب بيانات لوحتك. تحقّق من اتصالك بالشبكة ثم أعد المحاولة."
      />
    );

  // لوحة المدير/الحوكمة لها بوّابتها المستقلة الموجزة.
  if (dash.dashboardType === 'AdminGovernance') return <AdminHome />;

  const roles = user?.roles.map((r) => roleLabel[r]).join(' · ');
  const kpiDelta = kpiDeltaFrom(dash);

  const body = (() => {
    switch (dash.dashboardType) {
      case 'Employee':
        return <EmployeeDashboard dash={dash} kpiDelta={kpiDelta} />;
      case 'TeamLeader':
        return <TeamLeaderDashboard dash={dash} />;
      case 'Manager':
        return <ManagerDashboard dash={dash} />;
      case 'GM':
        return <GMDashboard dash={dash} />;
      case 'CEO':
        return <CeoDashboard dash={dash} kpiDelta={kpiDelta} />;
      case 'Governance':
        return <CeoSupportDashboard dash={dash} />;
      default:
        return <EmployeeDashboard dash={dash} kpiDelta={kpiDelta} />;
    }
  })();

  return (
    <div className="space-y-6">
      <div>
        <p className="font-semibold text-orange">{roles}</p>
        <h1 className="text-2xl font-bold text-navy">أهلاً، {user?.fullName}</h1>
        <p className="mt-1 text-ink-2">نظام تقارير الأداء والتشغيل الداخلي — {dash.period.label}.</p>
      </div>
      {body}
    </div>
  );
}

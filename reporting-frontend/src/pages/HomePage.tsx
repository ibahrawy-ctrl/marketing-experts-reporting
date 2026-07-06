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
import {
  HrHomeDashboard,
  FinanceHomeDashboard,
  AccountPortfolioHomeDashboard,
} from './RoleHomeDashboards';
import { DashboardHero, HeroChip } from '../components/dashboard';
import { ReportDueSection } from '../components/ReportDueCards';
import type { IconName } from '../components/icons';

// دلتا KPI = الفرق بين آخر نقطتين في اتجاه KPI (للبطاقات ذات شارة المقارنة).
function kpiDeltaFrom(dash: DashboardDto): { value: number; up: boolean } | null {
  const data = (dash.widgets.find((w) => w.key === 'kpiTrend')?.data as { value: number }[]) ?? [];
  if (data.length < 2) return null;
  const last = data[data.length - 1].value;
  const prev = data[data.length - 2].value;
  return { value: Math.round(Math.abs(last - prev) * 10) / 10, up: last >= prev };
}

export default function HomePage() {
  const { user, hasAnyRole } = useAuth();

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

  // الأدوار التي يصنّفها الخادم Employee (لغياب dashboardType مخصّص لها: HR/المالية/محفظة
  // الحسابات) لكن لها سطح عمل مستقل ⇒ نوجّهها للوحة الملائمة بالأولوية، وإلا لوحة الموظّف
  // الافتراضية. الخادم يُرجِع dashboardType='Employee' لهذه الأدوار، لذا الفحص يكون داخل
  // حالة Employee تحديدًا (لا في default). مصدر البيانات والصلاحية يبقى الخادم؛ تفرّع عرضٍ فقط.
  const resolveEmployeeHome = () => {
    if (hasAnyRole('HR')) return <HrHomeDashboard />;
    if (hasAnyRole('FinanceManager', 'Accountant')) return <FinanceHomeDashboard />;
    if (hasAnyRole('AccountPortfolioReader')) return <AccountPortfolioHomeDashboard />;
    return <EmployeeDashboard dash={dash} kpiDelta={kpiDelta} />;
  };

  // عنوان/أيقونة/وصف الترويسة (Hero) حسب نوع اللوحة، ولأدوار Employee حسب الدور الفعليّ.
  const heroMeta = (): { title: string; icon: IconName; subtitle: string } => {
    switch (dash.dashboardType) {
      case 'TeamLeader':
        return { title: 'لوحة قائد الفريق', icon: 'teams', subtitle: 'متابعة التزام فريقك واعتماد تقاريرهم' };
      case 'Manager':
        return { title: 'لوحة مدير القسم', icon: 'departments', subtitle: 'أداء القسم وسير الاعتماد والاختناقات' };
      case 'GM':
        return { title: 'لوحة المدير العام', icon: 'analytics', subtitle: 'نظرة شاملة على الأقسام والالتزام والحوكمة' };
      case 'CEO':
        return { title: 'لوحة الرئيس التنفيذي', icon: 'analytics', subtitle: 'المؤشّرات التنفيذية والمخاطر والقرارات' };
      case 'Governance':
        return { title: 'لوحة الحوكمة والمتابعة', icon: 'governance', subtitle: 'متابعة الالتزام والحوكمة دون اعتماد فنّي' };
      default:
        if (hasAnyRole('HR')) return { title: 'لوحة الموارد البشرية', icon: 'users', subtitle: 'طلبات الموظفين والأرصدة وبيانات الفريق' };
        if (hasAnyRole('FinanceManager', 'Accountant'))
          return { title: 'لوحة المالية', icon: 'reports', subtitle: 'تأثير الإجازات على الرواتب وتصدير KPI' };
        if (hasAnyRole('AccountPortfolioReader'))
          return { title: 'لوحة محفظة العملاء', icon: 'clients', subtitle: 'عملاؤك ومشاريعك ضمن نطاقك' };
        return { title: 'لوحة الموظف', icon: 'home', subtitle: 'تقاريرك ومؤشّراتك ومهامك الشخصية' };
    }
  };
  const hero = heroMeta();

  const body = (() => {
    switch (dash.dashboardType) {
      case 'Employee':
        return resolveEmployeeHome();
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
        // أمان احتياطيّ: أيّ dashboardType غير متوقّع يتبع نفس منطق Employee fallback.
        return resolveEmployeeHome();
    }
  })();

  return (
    <div className="space-y-6">
      <DashboardHero
        title={hero.title}
        subtitle={`أهلاً، ${user?.fullName} — ${hero.subtitle}`}
        icon={hero.icon}
        badges={
          <>
            {roles && <HeroChip>{roles}</HeroChip>}
            <HeroChip>الفترة: {dash.period.label}</HeroChip>
          </>
        }
      />
      <ReportDueSection />
      {body}
    </div>
  );
}

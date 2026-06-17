import { Routes, Route, Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { DashboardShell } from './components/DashboardShell';
import { ProtectedRoute } from './components/ProtectedRoute';
import LoginPage from './pages/LoginPage';
import HomePage from './pages/HomePage';
import ExecutiveReportsPage from './pages/ExecutiveReportsPage';
import SubmissionsPage from './pages/SubmissionsPage';
import KpiPage from './pages/KpiPage';
import ReportCalendarPage from './pages/ReportCalendarPage';
import GovernancePage from './pages/GovernancePage';
import DevelopmentPage from './pages/DevelopmentPage';
import AuditPage from './pages/AuditPage';
import TeamsPage from './pages/TeamsPage';
import TeamDetailsPage from './pages/TeamDetailsPage';
import ComparisonsPage from './pages/ComparisonsPage';
import ReportTemplatesPage from './pages/ReportTemplatesPage';
import KpiTemplatesPage from './pages/KpiTemplatesPage';
import ApprovalWorkflowsPage from './pages/ApprovalWorkflowsPage';
import UsersPage from './pages/UsersPage';
import EmployeeProfilePage from './pages/EmployeeProfilePage';
import LeaveRequestsPage from './pages/LeaveRequestsPage';
import { DepartmentsPage } from './pages/DepartmentsPage';
import SettingsPage from './pages/SettingsPage';
import ClientsPage from './pages/ClientsPage';
import ClientDetailPage from './pages/ClientDetailPage';
import ProjectsPage from './pages/ProjectsPage';
import ProjectDetailPage from './pages/ProjectDetailPage';
import type { Role } from './types/api';

const EXEC_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
// الحوكمة مقصورة على من يملك ViewGovernance بالخادم (تطابق RoleAccess.CanViewGovernance).
const GOVERNANCE_ROLES: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
// حوكمة القوالب وKPI — المستوى الإداري الأعلى (Admin/CEO/GM)، تطابق سياسة TemplateGovernance بالخادم.
const TEMPLATE_GOVERNANCE_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager'];

function Landing() {
  return (
    <div className="min-h-screen bg-offwhite text-ink">
      <header className="bg-navy text-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-5">
          <div className="rounded-xl bg-white px-4 py-2">
            <img src="/logo-arabic.png" alt="خبراء التسويق" className="h-9" />
          </div>
          <span className="en text-xs tracking-widest text-navy-100">
            PERFORMANCE&nbsp;&amp;&nbsp;OPERATIONS&nbsp;REPORTING
          </span>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6 py-16">
        <p className="mb-3 font-semibold text-orange">نظام داخلي</p>
        <h1 className="text-4xl font-bold leading-tight text-navy">
          نظام تقارير الأداء والتشغيل الداخلي
        </h1>
        <p className="mt-4 max-w-2xl text-lg text-ink-2">
          من التقرير الفردي إلى لوحة الـ CEO — تقارير، اعتماد, مؤشرات أداء، وحوكمة في منظومة
          واحدة.
        </p>
        <div className="mt-10">
          <Link
            to="/login"
            className="inline-block rounded-lg bg-orange px-6 py-3 font-semibold text-white hover:bg-orange-600"
          >
            تسجيل الدخول
          </Link>
        </div>
      </main>

      <footer className="mx-auto max-w-5xl px-6 py-8 text-sm text-ink-3">
        خبراء التسويق · تسويق أوضح … نمو أقوى.
      </footer>
    </div>
  );
}

function Protected({ children, roles }: { children: ReactNode; roles?: Role[] }) {
  return (
    <ProtectedRoute roles={roles}>
      <DashboardShell>{children}</DashboardShell>
    </ProtectedRoute>
  );
}

const APP_ROUTES: { path: string; element: ReactNode; roles?: Role[] }[] = [
  { path: '/app', element: <HomePage /> },
  { path: '/app/teams', element: <TeamsPage />, roles: EXEC_ROLES },
  { path: '/app/teams/:teamId', element: <TeamDetailsPage />, roles: EXEC_ROLES },
  // العملاء والمشاريع: متاح لمستوى الإدارة — النطاق ومستوى الرؤية مفروضان خادمًا.
  { path: '/app/clients', element: <ClientsPage />, roles: EXEC_ROLES },
  { path: '/app/clients/:clientId', element: <ClientDetailPage />, roles: EXEC_ROLES },
  { path: '/app/projects', element: <ProjectsPage />, roles: EXEC_ROLES },
  { path: '/app/projects/:projectId', element: <ProjectDetailPage />, roles: EXEC_ROLES },
  // ملف أداء الموظف: متاح لكل مصادَق عليه — النطاق مفروض خادمًا (الموظف يرى نفسه فقط).
  { path: '/app/employee/:userId', element: <EmployeeProfilePage /> },
  { path: '/app/submissions', element: <SubmissionsPage /> },
  // الإجازات والاستئذانات (V1.0.1): متاح لكل مصادَق عليه — النطاق والدور مفروضان خادمًا.
  { path: '/app/leave-requests', element: <LeaveRequestsPage /> },
  { path: '/app/report-templates', element: <ReportTemplatesPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  { path: '/app/kpi', element: <KpiPage /> },
  // تقويم التقارير والتجميع الدوري: متاح لكل مصادَق عليه — النطاق مفروض خادمًا.
  { path: '/app/report-calendar', element: <ReportCalendarPage /> },
  { path: '/app/kpi-templates', element: <KpiTemplatesPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  { path: '/app/workflows', element: <ApprovalWorkflowsPage />, roles: EXEC_ROLES },
  { path: '/app/governance', element: <GovernancePage />, roles: GOVERNANCE_ROLES },
  { path: '/app/analytics', element: <ComparisonsPage />, roles: EXEC_ROLES },
  { path: '/app/users', element: <UsersPage />, roles: ADMIN },
  { path: '/app/departments', element: <DepartmentsPage />, roles: ADMIN },
  { path: '/app/settings', element: <SettingsPage />, roles: ADMIN },
  { path: '/app/audit', element: <AuditPage />, roles: ['Admin', 'CEO', 'GeneralManager'] },
  { path: '/app/development', element: <DevelopmentPage /> },
  { path: '/app/reports', element: <ExecutiveReportsPage />, roles: EXEC_ROLES },
];

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Landing />} />
      <Route path="/login" element={<LoginPage />} />
      {APP_ROUTES.map((r) => (
        <Route key={r.path} path={r.path} element={<Protected roles={r.roles}>{r.element}</Protected>} />
      ))}
    </Routes>
  );
}

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
import SalesAggregationPage from './pages/SalesAggregationPage';
import TeamLeaderSalesDashboardPage from './pages/TeamLeaderSalesDashboardPage';
import TeamLeaderExecutionPage from './pages/TeamLeaderExecutionPage';
import TeamLeaderProjectExecutionPage from './pages/TeamLeaderProjectExecutionPage';
import SalesRepDashboardPage from './pages/SalesRepDashboardPage';
import ReportTemplatesPage from './pages/ReportTemplatesPage';
import KpiTemplatesPage from './pages/KpiTemplatesPage';
import ApprovalWorkflowsPage from './pages/ApprovalWorkflowsPage';
import UsersPage from './pages/UsersPage';
import CompliancePage from './pages/CompliancePage';
import HrEmployeesPage from './pages/HrEmployeesPage';
import JobRolesAssignmentPage from './pages/JobRolesAssignmentPage';
import JobRoleManagementPage from './pages/JobRoleManagementPage';
import PositionsPage from './pages/PositionsPage';
import ReportViewGrantsPage from './pages/ReportViewGrantsPage';
import EmailNotificationsPage from './pages/EmailNotificationsPage';
import EmailControlCenterPage from './pages/EmailControlCenterPage';
import EmployeeProfilePage from './pages/EmployeeProfilePage';
import { MyKpiPage, EmployeeKpiPage } from './pages/IndividualKpiPage';
import LeaveRequestsPage from './pages/LeaveRequestsPage';
import MyBalancesPage from './pages/MyBalancesPage';
import BalanceManagementPage from './pages/BalanceManagementPage';
import HrRequestsPage from './pages/HrRequestsPage';
import PayrollLeaveImpactsPage from './pages/PayrollLeaveImpactsPage';
import KpiFinanceExportPage from './pages/KpiFinanceExportPage';
import { DepartmentsPage } from './pages/DepartmentsPage';
import SettingsPage from './pages/SettingsPage';
import CourseManagementPage from './pages/CourseManagementPage';
import ServiceManagementPage from './pages/ServiceManagementPage';
import ExecutionTaxonomyManagementPage from './pages/ExecutionTaxonomyManagementPage';
import ClientsPage from './pages/ClientsPage';
import ClientDetailPage from './pages/ClientDetailPage';
import ProjectsPage from './pages/ProjectsPage';
import ProjectDetailPage from './pages/ProjectDetailPage';
import Project360Page from './pages/Project360Page';
import AccountPortfolioPage from './pages/AccountPortfolioPage';
import AccountPortfolioProjectPage from './pages/AccountPortfolioProjectPage';
import AccountPortfolioClientPage from './pages/AccountPortfolioClientPage';
import GovernanceWorkspacePage from './pages/GovernanceWorkspacePage';
import GovernanceEscalationsPage from './pages/GovernanceEscalationsPage';
import GovernanceActionItemsPage from './pages/GovernanceActionItemsPage';
import AdminArchivePage from './pages/AdminArchivePage';
import type { Role } from './types/api';

const EXEC_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer'];
const ADMIN: Role[] = ['Admin'];
// لوحة مبيعات الفريق (RC3-Task1) — لقائد الفريق والأدوار الأعلى فقط؛ النطاق مفروض خادميًّا عبر IScopeResolver.
const TEAM_SALES_DASHBOARD_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader'];
// تقارير التنفيذ Project-First (RC4-Task4) — للأدوار الإدارية وقائد الفريق ومدير الحساب (AccountPortfolioReader).
// الرؤية مفروضة خادميًّا: نطاق IScopeResolver ∪ حافظة IClientProjectAccess (مدير الحساب يرى مشاريع عملائه).
const EXECUTION_REPORTS_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'AccountPortfolioReader'];
// لوحة تنفيذ مشاريع الفريق (RC4-Task4C) — لقائد الفريق والأدوار الأعلى فقط (التركيز على قائد الفريق)؛
// لا تظهر للموظّف العادي ولا لمدير الحساب. النطاق مفروض خادميًّا عبر IScopeResolver (قائد الفريق يرى فريقه فقط).
const TEAM_EXECUTION_DASHBOARD_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader'];
// صفحة إدارة فريق العمل (GOV-R1): Admin + CEO يديران المستخدمين بالكامل؛ CeoSupport عرض + إعادة تعيين كلمات المرور فقط.
// إنشاء/تعديل/أدوار/حذف المستخدمين = Admin + CEO (مفروضة بسياسة UserManagement بالخادم وفي الواجهة).
const USERS_PAGE_ROLES: Role[] = ['Admin', 'CEO', 'CeoSupport'];
// الحوكمة مقصورة على من يملك ViewGovernance بالخادم (تطابق RoleAccess.CanViewGovernance).
const GOVERNANCE_ROLES: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
// حوكمة القوالب وKPI — المستوى الإداري الأعلى (Admin/CEO/GM)، تطابق سياسة TemplateGovernance بالخادم.
const TEMPLATE_GOVERNANCE_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager'];
// إدارة المسمّيات الوظيفية للموظفين (سطح مخصّص للقراءة + تعديل JobRole فقط) — تطابق سياسة UserJobRoleManagement بالخادم.
const JOB_ROLE_MANAGEMENT_ROLES: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
// إدارة الأرصدة (خدمات الموظف V1.1) — تطابق سياسة BalanceManagement بالخادم.
const BALANCE_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'HR'];
// متابعة الالتزام بالتقارير (per-person) — تطابق سياسة ReportCompletionView بالخادم.
const COMPLETION_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'Viewer', 'HR'];
// إدارة بيانات الموظفين (حزمة HR A) — اتحاد سياستَي UserBasicManagement و UserOrgAssignment بالخادم.
const HR_EMPLOYEE_ROLES: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];
// عرض التأثير على الرواتب (FIN-L1) — تطابق سياسة PayrollImpactRead بالخادم (قراءة؛ التعديل Admin/HR فقط داخل الصفحة).
const PAYROLL_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
// تصدير KPI للمالية (KPI-FIN1) — تطابق سياسة KpiFinanceExport بالخادم (قراءة/تصدير فقط على مستوى الشركة).
const KPI_FINANCE_EXPORT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'HR', 'CeoSupport', 'FinanceManager', 'Accountant'];
// محفظة مدير الحساب (ACCOUNT-MANAGER-PORTFOLIO) — عرض فقط، تطابق سياسة AccountPortfolioRead بالخادم.
const ACCOUNT_PORTFOLIO_ROLES: Role[] = ['AccountPortfolioReader', 'Admin'];
// مساحة عمل Project 360 (CPW-R3 · R2-W12) — الخادم يكتفي بـ[Authorize] ويحسم الرؤية بالنطاق،
// فتُبقى بوّابة الواجهة واسعة بقدر من يملك مشاريع مرئيّة فعلًا بدل تضييق يُخفي مساحة مسموحة.
// `Employee` مضاف عمدًا (P360-WF-R2 §10): جسر التنفيذ الهجين يجعل **المنفِّذ** هو من يرفع
// ادّعاء التنفيذ، والخادم يمنحه الرؤية فعلًا حين ينتمي لفريق المشروع. بلا هذا الدور كانت
// البوّابة المحلّيّة تحجب الشاشة كلّها عمّن أذِن له الخادم صراحةً — اشتقاق تخويل موازٍ نفته §12.
// من لا مشروع مرئيًّا له يرى الرسالة الموحّدة نفسها من الخادم، فلا تُكشف بذلك أيّ بيانات.
const PROJECT_360_ROLES: Role[] = [...EXEC_ROLES, 'AccountPortfolioReader', 'Employee'];
// ملفّ العميل الشامل Client 360 (CPW-R2) — نفس أدوار التنفيذ زائد مدير العميل (AccountPortfolioReader).
// لا يُوسَّع EXEC_ROLES نفسه كي لا تُفتَح بقيّة الشاشات التنفيذيّة. الرؤية والتحرير مفروضان خادميًّا:
// القراءة عبر Client.AccountManagerId، والتحرير الأساسيّ يبقى محصورًا بسياسة ClientCoreManagers.
const CLIENT_360_ROLES: Role[] = [...EXEC_ROLES, 'AccountPortfolioReader'];
// ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) — تطابق سياسة GovernanceWorkspaceAccess بالخادم؛ الرؤية مقيّدة داخليًّا حسب الدور.
const GOVERNANCE_WORKSPACE_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR'];
// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — تطابق سياسة GovernanceEscalationAccess بالخادم؛ الفرز (واسع/نطاق/HR/موظف) مفروض داخليًّا.
const GOVERNANCE_ESCALATION_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — تطابق سياسة GovernanceActionItemAccess بالخادم؛ الفرز (واسع/نطاق/HR/موظف) مفروض داخليًّا.
const GOVERNANCE_ACTION_ITEM_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'CeoSupport', 'Manager', 'TeamLeader', 'HR', 'Employee'];
// الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1) — قراءة/استرجاع العناصر المحذوفة إداريًّا؛ تطابق سياسة ArchiveGovernanceAccess بالخادم.
const ARCHIVE_GOVERNANCE_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager'];

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
  { path: '/app/clients', element: <ClientsPage />, roles: CLIENT_360_ROLES },
  { path: '/app/clients/:clientId', element: <ClientDetailPage />, roles: CLIENT_360_ROLES },
  { path: '/app/projects', element: <ProjectsPage />, roles: EXEC_ROLES },
  // تفاصيل المشروع (R2.1 · GAP-R21-05): البوّابة هنا **أوسع من EXEC_ROLES عمدًا** لأنّ شاشة
  // مسارات العمل («أهداف العمل») لا وجود لها إلّا في هذه الصفحة، والخادم يمنح إدارتها لمالك
  // المشروع وقائد الفريق ومدير الحساب المُسنَدين **بالمورد لا بالدور** (`CanManagePlanAsync`).
  // بـEXEC_ROLES وحدها كان مالك مشروع بدور Employee يُحجَب عن الصفحة كلّها بينما الخادم يقبل
  // كتابته — نفس انفصال الشاشة عن الخادم الذي أُغلِق في `Project360Page`. لا يُكشَف شيء إضافيّ:
  // الرؤية تُحسَم خادمًا لكلّ مشروع (`404 project.not_found` خارج النطاق)، وأزرار التعديل
  // والأرشفة تبقى خلف `canManageClients` وخلف `Policies.ProjectStructuralManage` خادميًّا.
  { path: '/app/projects/:projectId', element: <ProjectDetailPage />, roles: PROJECT_360_ROLES },
  // مساحة عمل Project 360 (CPW-R3 · R2-W12): بوّابة الواجهة أوسع من EXEC_ROLES عمدًا لأنّ
  // مدير الحساب المسؤول من مستخدميها؛ ومع ذلك الرؤية الفعليّة تُحسَم خادمًا لكلّ مشروع على حدة.
  { path: '/app/projects/:projectId/360', element: <Project360Page />, roles: PROJECT_360_ROLES },
  // محفظة مدير الحساب (مشاريعي) — عرض فقط، النطاق مفروض خادمًا (Project.AccountManagerId == المستخدم).
  { path: '/app/account-portfolio', element: <AccountPortfolioPage />, roles: ACCOUNT_PORTFOLIO_ROLES },
  { path: '/app/account-portfolio/projects/:id', element: <AccountPortfolioProjectPage />, roles: ACCOUNT_PORTFOLIO_ROLES },
  { path: '/app/account-portfolio/clients/:id', element: <AccountPortfolioClientPage />, roles: ACCOUNT_PORTFOLIO_ROLES },
  // ملف أداء الموظف: متاح لكل مصادَق عليه — النطاق مفروض خادمًا (الموظف يرى نفسه فقط).
  { path: '/app/employee/:userId', element: <EmployeeProfilePage /> },
  // مؤشرات أداء فردية (KPI-INDIVIDUAL-DASHBOARD-R1): «مؤشرات أدائي» للموظّف الحالي،
  // و«مؤشرات أداء الموظف» لموظّف بعينه — النطاق مفروض خادمًا (self-or-scope ⇒ 403/404 خارج النطاق).
  { path: '/app/my-kpi', element: <MyKpiPage /> },
  { path: '/app/employees/:userId/kpi', element: <EmployeeKpiPage /> },
  { path: '/app/submissions', element: <SubmissionsPage /> },
  // ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1: سطح «تقاريري» الشخصيّ الموازي — متاح لكل مصادَق
  // عليه (لا بوابة أدوار)، يُثبّت العرض على تقارير المستخدم نفسه (إنشاء/مسودة/إرسال/متابعة). لا يستبدل
  // سطح الفريق /app/submissions ولا يخفيه عن القادة/الإدارة.
  { path: '/app/my-reports', element: <SubmissionsPage personalOnly /> },
  // الإجازات والاستئذانات (V1.0.1): متاح لكل مصادَق عليه — النطاق والدور مفروضان خادمًا.
  { path: '/app/leave-requests', element: <LeaveRequestsPage /> },
  // خدمات الموظف (V1.1): الأرصدة وطلبات الموارد البشرية — متاح لكل مصادَق عليه (النطاق مفروض خادمًا).
  { path: '/app/balances', element: <MyBalancesPage /> },
  { path: '/app/hr-requests', element: <HrRequestsPage /> },
  { path: '/app/balance-management', element: <BalanceManagementPage />, roles: BALANCE_MANAGEMENT_ROLES },
  // المالية والرواتب (FIN-L1): عرض الطلبات المؤثّرة على الراتب — قراءة للأدوار المالية، التعديل Admin/HR فقط.
  { path: '/app/payroll/leave-impacts', element: <PayrollLeaveImpactsPage />, roles: PAYROLL_ROLES },
  // تصدير KPI للمالية (KPI-FIN1): معاينة/تصدير CSV قراءة فقط — تطابق سياسة KpiFinanceExport بالخادم.
  { path: '/app/kpi-finance-export', element: <KpiFinanceExportPage />, roles: KPI_FINANCE_EXPORT_ROLES },
  { path: '/app/report-templates', element: <ReportTemplatesPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  { path: '/app/kpi', element: <KpiPage /> },
  // تقويم التقارير والتجميع الدوري: متاح لكل مصادَق عليه — النطاق مفروض خادمًا.
  { path: '/app/report-calendar', element: <ReportCalendarPage /> },
  { path: '/app/kpi-templates', element: <KpiTemplatesPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  { path: '/app/workflows', element: <ApprovalWorkflowsPage />, roles: EXEC_ROLES },
  { path: '/app/governance', element: <GovernancePage />, roles: GOVERNANCE_ROLES },
  { path: '/app/governance-workspace', element: <GovernanceWorkspacePage />, roles: GOVERNANCE_WORKSPACE_ROLES },
  // التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — الرؤية والإجراءات مقيّدة داخليًّا حسب الدور والنطاق.
  { path: '/app/governance/escalations', element: <GovernanceEscalationsPage />, roles: GOVERNANCE_ESCALATION_ROLES },
  { path: '/app/governance/action-items', element: <GovernanceActionItemsPage />, roles: GOVERNANCE_ACTION_ITEM_ROLES },
  { path: '/app/analytics', element: <ComparisonsPage />, roles: EXEC_ROLES },
  // تجميع المبيعات (B2C-UAT-FIXPACK الجزء 4) — عرض تجميعي للمدير؛ النطاق مفروض خادميًّا عبر IScopeResolver.
  { path: '/app/sales-aggregation', element: <SalesAggregationPage />, roles: EXEC_ROLES },
  // لوحة مبيعات الفريق (RC3-Task1) — لقائد الفريق؛ يرى فريقه فقط (النطاق مفروض خادميًّا عبر IScopeResolver).
  { path: '/app/sales/team-dashboard', element: <TeamLeaderSalesDashboardPage />, roles: TEAM_SALES_DASHBOARD_ROLES },
  // تقارير التنفيذ Project-First (RC4-Task4) — تجميع تنفيذ الفرق حسب المشروع/الموظّف مع مقارنة أسبوعية؛ النطاق مفروض خادميًّا عبر IScopeResolver.
  { path: '/app/execution-reports', element: <TeamLeaderExecutionPage />, roles: EXECUTION_REPORTS_ROLES },
  // لوحة تنفيذ مشاريع الفريق (RC4-Task4C) — متابعة تنفيذ المشاريع داخل الـPod مبنية على محرّك تجميع 4B؛ النطاق مفروض خادميًّا.
  { path: '/app/execution/team-dashboard', element: <TeamLeaderProjectExecutionPage />, roles: TEAM_EXECUTION_DASHBOARD_ROLES },
  // لوحة مبيعاتي الشخصية (RC3-Task1.1) — للمندوب (SALES_B2C/SALES_B2B) والأدمن؛ الحماية داخل الصفحة عبر jobRoleCode.
  // بلا roles على المسار (المندوب دوره Employee) — النطاق مفروض خادميًّا (المندوب يرى نفسه فقط عبر تقاطع IScopeResolver).
  { path: '/app/sales/my-dashboard', element: <SalesRepDashboardPage /> },
  // متابعة الالتزام بالتقارير (per-person) — شاشة متابعة فقط، الصلاحية مفروضة خادمًا.
  { path: '/app/compliance', element: <CompliancePage />, roles: COMPLETION_ROLES },
  // إدارة بيانات الموظفين (حزمة HR A) — تعديل الاسم + التنظيم الوظيفي فقط.
  { path: '/app/hr-employees', element: <HrEmployeesPage />, roles: HR_EMPLOYEE_ROLES },
  { path: '/app/users', element: <UsersPage />, roles: USERS_PAGE_ROLES },
  { path: '/app/job-roles', element: <JobRolesAssignmentPage />, roles: JOB_ROLE_MANAGEMENT_ROLES },
  { path: '/app/job-roles/manage', element: <JobRoleManagementPage />, roles: JOB_ROLE_MANAGEMENT_ROLES },
  // المناصب المرنة (Phase 1A — رؤية فقط) — Admin فقط (تطابق سياسة PositionManagement بالخادم).
  { path: '/app/positions', element: <PositionsPage />, roles: ADMIN },
  // منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1) — Admin فقط (تطابق سياسة AdminOnly بالخادم).
  { path: '/app/report-view-grants', element: <ReportViewGrantsPage />, roles: ADMIN },
  // سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-UI-R1) — قراءة فقط، Admin/CEO/GM/CeoSupport (تطابق سياسة EmailNotificationLog بالخادم).
  { path: '/app/email-notifications', element: <EmailNotificationsPage />, roles: GOVERNANCE_ROLES },
  // مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — قوالب/قواعد/تذكير يدويّ DryRun — Admin فقط (سياسة EmailControlManage بالخادم).
  { path: '/app/email-control', element: <EmailControlCenterPage />, roles: ADMIN },
  { path: '/app/departments', element: <DepartmentsPage />, roles: ADMIN },
  // إدارة كتالوج الدورات (B2C) — تطابق سياسة TemplateGovernance بالخادم (Admin/CEO/GM).
  { path: '/app/courses', element: <CourseManagementPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  // إدارة كتالوج خدمات B2B — تطابق سياسة TemplateGovernance بالخادم (Admin/CEO/GM).
  { path: '/app/services', element: <ServiceManagementPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  // إدارة كتالوج تصنيفات التنفيذ (RC-4 Task 4D2) — تطابق سياسة TemplateGovernance بالخادم (Admin/CEO/GM).
  { path: '/app/execution-taxonomy', element: <ExecutionTaxonomyManagementPage />, roles: TEMPLATE_GOVERNANCE_ROLES },
  { path: '/app/settings', element: <SettingsPage />, roles: ADMIN },
  { path: '/app/audit', element: <AuditPage />, roles: ['Admin', 'CEO', 'GeneralManager'] },
  // الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1) — قراءة/استرجاع المحذوف إداريًّا؛ تطابق سياسة ArchiveGovernanceAccess بالخادم.
  { path: '/app/admin/archive', element: <AdminArchivePage />, roles: ARCHIVE_GOVERNANCE_ROLES },
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

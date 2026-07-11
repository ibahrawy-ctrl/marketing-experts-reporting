import { render, screen, fireEvent, within } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type {
  PeriodComparison,
  ProjectExecMetrics,
  ProjectFirstByClientRow,
  ProjectFirstByEmployeeRow,
  ProjectFirstByPodRow,
  ProjectFirstByProjectRow,
  ProjectFirstExecutionReport,
} from '../types/api';

// ===== RC4-Task4C — لوحة تنفيذ مشاريع الفريق =====
// عزل الصفحة عن الشبكة: هوكات محرّك التجميع 4B مُموّهة (لا استدعاء API).
// النطاق (فريق قائد الفريق) مفروض خادميًّا عبر IScopeResolver — يُختبَر في اختبارات الخادم لا هنا.

type QueryLike<T> = { data: T | undefined; isLoading: boolean; isError: boolean; refetch: () => void };

// حالة قابلة للتعديل لكل هوك في كل اختبار.
const state: {
  project: QueryLike<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>;
  employee: QueryLike<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>;
  pod: QueryLike<ProjectFirstExecutionReport<ProjectFirstByPodRow>>;
  client: QueryLike<ProjectFirstExecutionReport<ProjectFirstByClientRow>>;
} = {
  project: empty(),
  employee: empty(),
  pod: empty(),
  client: empty(),
};

function empty<T>(): QueryLike<T> {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn() };
}

function metrics(p: Partial<ProjectExecMetrics> = {}): ProjectExecMetrics {
  return {
    planned: 0, completed: 0, approved: 0, revisions: 0, published: 0, delayed: 0,
    messagesIn: 0, responses: 0, issueComments: 0, escalations: 0,
    completionRate: 0, approvalRate: 0, publishRate: 0, responseRate: 0,
    ...p,
  };
}

function comparison(p: Partial<PeriodComparison> = {}): PeriodComparison {
  return { current: 0, previous: 0, change: 0, changePercent: null, trend: 'none', hasPrevious: false, ...p };
}

function report<T>(rows: T[], extra: Partial<ProjectFirstExecutionReport<T>> = {}): ProjectFirstExecutionReport<T> {
  return {
    periodKey: '2026-W25', previousPeriodKey: '2026-W24', rowCount: rows.length,
    submissionsConsidered: 0, submissionsIgnored: 0, entriesIgnored: 0,
    rowsConsidered: 0, rowsIgnored: 0, ignoredReasons: {}, viewLevel: 'team', rows, ...extra,
  };
}

vi.mock('../lib/useProjectExecution', () => ({
  useExecutionByProject: () => state.project,
  useExecutionByEmployee: () => state.employee,
  useExecutionByPod: () => state.pod,
  useExecutionByClient: () => state.client,
}));

import TeamLeaderProjectExecutionPage from './TeamLeaderProjectExecutionPage';

beforeEach(() => {
  state.project = empty();
  state.employee = empty();
  state.pod = empty();
  state.client = empty();
});

it('يعرض العنوان ومنتقي الفترة (الافتراضي أسبوعي)', () => {
  const { container } = render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getByText('لوحة تنفيذ مشاريع الفريق')).toBeInTheDocument();
  expect(screen.getByText('نوع الفترة')).toBeInTheDocument();
  expect(screen.getByText('الأسبوع')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();
});

it('تبديل نوع الفترة يُظهر المنتقي المطابق (شهري/ربع سنوي)', () => {
  const { container } = render(<TeamLeaderProjectExecutionPage />);
  const typeSelect = screen.getByRole('combobox');
  fireEvent.change(typeSelect, { target: { value: 'Monthly' } });
  expect(screen.getByText('الشهر')).toBeInTheDocument();
  expect(container.querySelector('input[type="month"]')).not.toBeNull();
  fireEvent.change(typeSelect, { target: { value: 'Quarterly' } });
  expect(screen.getByText('السنة')).toBeInTheDocument();
  expect(screen.getByText('الربع')).toBeInTheDocument();
});

it('يعرض بطاقات KPI محسوبة من صفوف التجميع', () => {
  state.project = { ...empty(), data: report<ProjectFirstByProjectRow>([
    { projectId: 'p1', projectName: 'مشروع أ', clientId: 'c1', clientName: 'عميل أ', contributors: 3,
      metrics: metrics({ planned: 10, completed: 8, approved: 6, revisions: 2, completionRate: 80 }),
      comparison: comparison({ current: 8, previous: 5, change: 3, changePercent: 60, trend: 'up', hasPrevious: true }) },
  ]) };
  state.client = { ...empty(), data: report<ProjectFirstByClientRow>([
    { clientId: 'c1', clientName: 'عميل أ', projectCount: 1, activeProjectCount: 1, metrics: metrics(), comparison: null },
  ]) };
  state.pod = { ...empty(), data: report<ProjectFirstByPodRow>([
    { teamId: 't1', teamName: 'فريق أ', projectCount: 1, employeeCount: 3,
      metrics: metrics({ completed: 8, responses: 0 }),
      comparison: comparison({ current: 8, previous: 5, change: 3, changePercent: 60, trend: 'up', hasPrevious: true }) },
  ]) };
  render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getByText('المشاريع النشطة')).toBeInTheDocument();
  expect(screen.getByText('إجمالي المنجَز')).toBeInTheDocument();
  expect(screen.getByText('إجمالي المعتمَد')).toBeInTheDocument();
  expect(screen.getByText('الموظّفون المشاركون')).toBeInTheDocument();
  expect(screen.getByText('اتجاه الأداء')).toBeInTheDocument();
});

it('يعرض جدول تقدّم المشاريع مع شارة الحالة والاتجاه', () => {
  state.project = { ...empty(), data: report<ProjectFirstByProjectRow>([
    { projectId: 'p1', projectName: 'مشروع الإطلاق', clientId: 'c1', clientName: 'عميل أ', contributors: 2,
      metrics: metrics({ planned: 10, completed: 9, approved: 8, revisions: 1, completionRate: 90, approvalRate: 88 }),
      comparison: comparison({ current: 9, previous: 6, change: 3, changePercent: 50, trend: 'up', hasPrevious: true }) },
  ]) };
  render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getByText('تقدّم المشاريع')).toBeInTheDocument();
  expect(screen.getByText('مشروع الإطلاق')).toBeInTheDocument();
  expect(screen.getByText('جيد')).toBeInTheDocument();
});

it('النقر على صفّ المشروع يفتح التفصيل (Drill-down) لمساهمي المشروع', () => {
  state.project = { ...empty(), data: report<ProjectFirstByProjectRow>([
    { projectId: 'p1', projectName: 'مشروع الإطلاق', clientId: 'c1', clientName: 'عميل أ', contributors: 1,
      metrics: metrics({ completed: 5, approved: 4 }), comparison: null },
  ]) };
  // صفوف الموظّفين تُستخدَم للتفصيل (نفس هوك الموظّفين).
  state.employee = { ...empty(), data: report<ProjectFirstByEmployeeRow>([
    { employeeId: 'e1', employeeName: 'سارة', teamId: 't1', teamName: 'فريق أ', projectId: 'p1',
      projectName: 'مشروع الإطلاق', clientId: 'c1', clientName: 'عميل أ',
      metrics: metrics({ completed: 5, approved: 4 }), comparison: null },
  ]) };
  render(<TeamLeaderProjectExecutionPage />);
  fireEvent.click(screen.getByText('مشروع الإطلاق'));
  expect(screen.getByText(/مساهمو مشروع/)).toBeInTheDocument();
  expect(screen.getByText('نوع المساهمة')).toBeInTheDocument();
});

it('يعرض قسم أداء الموظّفين من صفوف (موظّف، مشروع)', () => {
  state.employee = { ...empty(), data: report<ProjectFirstByEmployeeRow>([
    { employeeId: 'e1', employeeName: 'سارة', teamId: 't1', teamName: 'فريق أ', projectId: 'p1',
      projectName: 'مشروع أ', clientId: 'c1', clientName: 'عميل أ',
      metrics: metrics({ completed: 5, approved: 4, revisions: 1 }), comparison: null },
  ]) };
  render(<TeamLeaderProjectExecutionPage />);
  const section = screen.getByText('أداء الموظّفين').closest('div')!;
  expect(within(section).getByText('سارة')).toBeInTheDocument();
});

it('يعرض حالة فارغة واضحة (لا أصفار مضلِّلة) عند غياب البيانات', () => {
  render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getByText('لا توجد مشاريع بها نشاط خلال هذه الفترة')).toBeInTheDocument();
  expect(screen.getByText('لا توجد مساهمات موظّفين خلال هذه الفترة')).toBeInTheDocument();
});

it('يعرض حالة التحميل', () => {
  state.project = { ...empty(), isLoading: true };
  render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getByText('يتم تحميل بيانات تنفيذ الفريق…')).toBeInTheDocument();
});

it('يعرض حالة الخطأ مع زرّ إعادة المحاولة', () => {
  const refetch = vi.fn();
  state.project = { ...empty(), isError: true, refetch };
  render(<TeamLeaderProjectExecutionPage />);
  const retry = screen.getByText('إعادة المحاولة');
  expect(retry).toBeInTheDocument();
  fireEvent.click(retry);
  expect(refetch).toHaveBeenCalled();
});

it('لوحة التشخيصات قابلة للطي وتعرض أرقام التجميع وأسباب التجاهل', () => {
  state.project = { ...empty(), data: report<ProjectFirstByProjectRow>([
    { projectId: 'p1', projectName: 'مشروع أ', clientId: 'c1', clientName: 'عميل أ', contributors: 1,
      metrics: metrics({ completed: 5 }), comparison: null },
  ], {
    submissionsConsidered: 12, submissionsIgnored: 2, rowsConsidered: 20, rowsIgnored: 3,
    ignoredReasons: { empty_project_entry: 3 },
  }) };
  render(<TeamLeaderProjectExecutionPage />);
  const toggle = screen.getByText('تشخيصات التجميع (للمراجعة)');
  expect(screen.queryByText('تسليمات مفحوصة')).toBeNull();
  fireEvent.click(toggle);
  expect(screen.getByText('تسليمات مفحوصة')).toBeInTheDocument();
  expect(screen.getByText('صفوف مُتجاهَلة')).toBeInTheDocument();
  expect(screen.getByText(/empty_project_entry/)).toBeInTheDocument();
});

it('يعرض «لا توجد بيانات سابقة» حين لا توجد مقارنة سابقة', () => {
  state.pod = { ...empty(), data: report<ProjectFirstByPodRow>([
    { teamId: 't1', teamName: 'فريق أ', projectCount: 1, employeeCount: 1,
      metrics: metrics({ completed: 3 }), comparison: comparison({ current: 3 }) },
  ]) };
  render(<TeamLeaderProjectExecutionPage />);
  expect(screen.getAllByText('لا توجد بيانات سابقة').length).toBeGreaterThan(0);
});

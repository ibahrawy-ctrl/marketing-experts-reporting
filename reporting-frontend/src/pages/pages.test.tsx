import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactElement } from 'react';
import { AuthProvider } from '../lib/auth';
import { ToastProvider } from '../components/ActionResultToast';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import AuditPage from './AuditPage';
import KpiPage from './KpiPage';
import SubmissionsPage from './SubmissionsPage';
import DevelopmentPage from './DevelopmentPage';
import GovernancePage from './GovernancePage';
import HomePage from './HomePage';
import LeaveRequestsPage from './LeaveRequestsPage';

// خريطة استجابات api.get حسب المسار — تُغطّي البيانات التي تعرضها الصفحات.
const getData: Record<string, unknown> = {
  '/audit-logs': [
    {
      id: 'a1',
      createdAtUtc: '2026-06-09T10:00:00Z',
      actorName: 'مدير تجريبي',
      action: 'submission.created',
      entityType: 'ReportSubmission',
      entityId: 's1',
      ipAddress: '127.0.0.1',
    },
  ],
  '/kpi-evaluations': [
    {
      id: 'k1',
      templateTitle: 'النبض الأسبوعي العام',
      subjectName: 'موظف تجريبي',
      periodKey: '2026-W23',
      totalScore: 80,
      trend: 'Up',
      status: 'Draft',
    },
  ],
  '/kpi-templates': [],
  '/submissions/mine': [
    {
      id: 'sub1',
      templateTitle: 'تقرير المدير العام',
      periodKey: '2026-06',
      status: 'Draft',
    },
  ],
  '/submissions/pending-approvals': [],
  '/report-templates': [],
  '/leave-requests/my': [
    {
      id: 'lr1',
      requesterUserId: 'u1',
      requesterName: 'موظف تجريبي',
      type: 'Leave',
      startDate: '2026-06-18',
      endDate: '2026-06-20',
      startTime: null,
      endTime: null,
      reason: 'سفر عائلي',
      status: 'Submitted',
      currentStep: 'TeamLeader',
      isHrRequest: false,
      impactsReports: false,
      createdAtUtc: '2026-06-17T09:00:00Z',
    },
  ],
  '/training-needs': [
    {
      id: 't1',
      title: 'تدريب على العروض',
      subjectName: 'موظف تجريبي',
      status: 'Open',
    },
  ],
  '/improvement-plans': [],
  '/risks': [],
  '/escalations': [],
  '/reports/b2c-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalLeads: 0,
    totalCalls: 0,
    totalFollowUps: 0,
    totalRegistrations: 0,
    totalClosedDeals: 0,
    totalTarget: 0,
    overallConversionRate: 0,
    overallTargetAchievement: 0,
    best: null,
    worst: null,
    rows: [],
    commonLostReasons: [],
  },
  '/reports/media-buyer-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalSpend: 0,
    totalLeads: 0,
    overallCpl: 0,
    averageCtr: 0,
    averageConversionRate: 0,
    best: null,
    worst: null,
    rows: [],
    commonIssueCauses: [],
    decisionsNeeded: [],
  },
  '/reports/seo-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalImprovedKeywords: 0,
    totalDeclinedKeywords: 0,
    netKeywordMovement: 0,
    totalTasksDone: 0,
    totalTechnicalIssues: 0,
    totalIndexedPages: 0,
    totalOrganicTraffic: 0,
    totalArticlesPlanned: 0,
    totalArticlesPublished: 0,
    totalArticlesLate: 0,
    contentDeliveryRate: 0,
    best: null,
    worst: null,
    rows: [],
    decisionsNeeded: [],
    recommendations: [],
  },
  '/reports/content-writer-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalRequired: 0,
    totalDelivered: 0,
    totalApprovedFirstTime: 0,
    totalLate: 0,
    totalRevised: 0,
    contentDeliveryRate: 0,
    firstApprovalRate: 0,
    revisionRate: 0,
    avgPlanAdherence: 0,
    best: null,
    worst: null,
    rows: [],
    delayReasons: [],
    decisionsNeeded: [],
  },
  '/reports/designer-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalRequested: 0,
    totalDelivered: 0,
    totalApprovedFirstTime: 0,
    totalLate: 0,
    totalPendingReview: 0,
    totalRevised: 0,
    deliveryRate: 0,
    firstApprovalRate: 0,
    revisionRate: 0,
    onTimeRate: 0,
    avgPlanAdherence: 0,
    best: null,
    worst: null,
    rows: [],
    delayReasons: [],
    decisionsNeeded: [],
  },
  '/reports/video-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalRequested: 0,
    totalDelivered: 0,
    totalApprovedFirstTime: 0,
    totalLate: 0,
    totalPendingReview: 0,
    totalRevised: 0,
    deliveryRate: 0,
    firstApprovalRate: 0,
    revisionRate: 0,
    onTimeRate: 0,
    avgPlanAdherence: 0,
    best: null,
    worst: null,
    rows: [],
    delayReasons: [],
    decisionsNeeded: [],
  },
  '/reports/moderation-rollup': {
    periodKey: '2026-W23',
    reporters: 0,
    totalIncoming: 0,
    totalAnswered: 0,
    totalUnhandled: 0,
    totalProblematic: 0,
    totalEscalations: 0,
    totalComplaints: 0,
    totalConverted: 0,
    responseRate: 0,
    avgResponseMinutes: 0,
    best: null,
    worst: null,
    rows: [],
    recurringIssues: [],
    decisionsNeeded: [],
  },
  '/reports/social-ops-rollup': {
    periodKey: '2026-W23',
    totalReporters: 0,
    content: { reporters: 0, required: 0, delivered: 0, firstApprovalRate: 0, needsFollowup: 0 },
    design: { reporters: 0, requested: 0, delivered: 0, firstApprovalRate: 0, late: 0, needsFollowup: 0 },
    video: { reporters: 0, requested: 0, delivered: 0, firstApprovalRate: 0, late: 0, needsFollowup: 0 },
    moderation: { reporters: 0, incoming: 0, answered: 0, responseRate: 0, avgResponseMinutes: 0, complaints: 0, escalations: 0 },
    healthScore: 0,
    healthLabel: 'لا توجد بيانات',
    topRisk: 'لا يوجد',
    mostNeedsFollowupTrack: 'لا يوجد',
    mostDelayedTrack: 'لا يوجد',
    mostRevisedTrack: 'لا يوجد',
    mostComplaintsTrack: 'لا يوجد',
    recommendation: 'لا توجد بيانات تشغيلية لهذه الفترة',
    decisionNeeded: null,
  },
  '/dashboard/me': {
    dashboardType: 'Employee',
    period: { periodKey: '2026-W23', label: 'الأسبوع الحالي' },
    user: { id: 'u1', name: 'موظف تجريبي', role: 'Employee' },
    scope: { type: 'own', ids: ['u1'] },
    permissions: ['ViewOwn'],
    summaryCards: [
      { key: 'totalReports', title: 'التقارير المطلوبة', value: 3, status: 'neutral', drilldownKey: 'report-status' },
    ],
    widgets: [{ key: 'kpiTrend', type: 'lineChart', title: 'تقدم KPI', data: [] }],
    actions: [],
  },
};

function lookup(url: string) {
  const path = url.split('?')[0];
  return getData[path] ?? [];
}

beforeEach(() => {
  tokenStore.clear();
  vi.restoreAllMocks();
  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: lookup(url) } as never),
  );
});

function renderPage(el: ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter>{el}</MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('module pages render', () => {
  it('AuditPage shows heading and a log row', async () => {
    renderPage(<AuditPage />);
    expect(await screen.findByText('سجل التدقيق')).toBeInTheDocument();
    // الاسم صار ضمن جملة أعمال مفهومة بدل خلية مجرّدة (تحسين Phase UX-1).
    expect(await screen.findByText('تم إنشاء تقرير بواسطة مدير تجريبي')).toBeInTheDocument();
  });

  it('KpiPage shows heading and an evaluation row', async () => {
    renderPage(<KpiPage />);
    expect(await screen.findByText('مؤشرات الأداء')).toBeInTheDocument();
    expect(await screen.findByText('النبض الأسبوعي العام')).toBeInTheDocument();
  });

  it('SubmissionsPage shows heading and a submission row', async () => {
    renderPage(<SubmissionsPage />);
    expect(await screen.findByText('التقارير المقدمة')).toBeInTheDocument();
    expect(await screen.findByText('تقرير المدير العام')).toBeInTheDocument();
  });

  it('DevelopmentPage shows heading and a training need', async () => {
    renderPage(<DevelopmentPage />);
    expect(await screen.findByText('التطوير')).toBeInTheDocument();
    expect(await screen.findByText('تدريب على العروض')).toBeInTheDocument();
  });

  it('GovernancePage shows heading', async () => {
    renderPage(<GovernancePage />);
    expect(await screen.findByText('الحوكمة والمتابعة')).toBeInTheDocument();
  });

  it('LeaveRequestsPage shows heading and a leave request row', async () => {
    renderPage(<LeaveRequestsPage />);
    expect(await screen.findByRole('heading', { name: 'الإجازات والاستئذانات' })).toBeInTheDocument();
    // حالة الطلب «بانتظار قائد الفريق» تظهر فقط في صف الجدول (لا تتعارض مع نص آخر).
    expect(await screen.findByText('بانتظار قائد الفريق')).toBeInTheDocument();
  });

  it('HomePage renders the employee dashboard for dashboardType=Employee', async () => {
    renderPage(<HomePage />);
    // شعار الترحيب + لافتة الإجراء الموجَّهة للموظف (مسودّة غير مكتملة).
    expect(await screen.findByText('تقريرك مطلوب الآن')).toBeInTheDocument();
    expect(await screen.findByText('مسار اعتماد تقريرك')).toBeInTheDocument();
  });
});

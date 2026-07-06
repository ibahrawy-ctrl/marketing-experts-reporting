import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import ExecutiveReportsPage from './ExecutiveReportsPage';

// بيانات الاكتمال والمؤشّرات المسموحة لكل دور يصل للصفحة (Manager/TeamLeader/Viewer/الإدارة).
const completeness = {
  periodKey: '2026-W26',
  total: 4,
  closed: 3,
  pending: 1,
  completionRate: 0.75,
  byStatus: [{ status: 'Closed', count: 3 }],
  byDepartment: [{ departmentId: 'd1', departmentName: 'المبيعات', total: 4, closed: 3, completionRate: 0.75 }],
};
const kpi = {
  periodKey: '2026-W26',
  evaluated: 2,
  averageScore: 78.5,
  belowTarget: 1,
  rows: [],
};
const governance = {
  openRisks: 2,
  risksBySeverity: [{ severity: 'High', count: 1 }],
  openEscalations: 1,
  openTrainingNeeds: 0,
  openImprovementPlans: 0,
  openDecisions: 3,
};

// خطأ 403 يحاكي ردّ الخادم لمن لا يملك صلاحية ViewGovernance (مدير/قائد فريق/Viewer).
function forbidden() {
  return Promise.reject({ response: { status: 403, data: { type: 'auth.forbidden' } } });
}

beforeEach(() => {
  tokenStore.clear();
  vi.restoreAllMocks();
});

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter>
          <ExecutiveReportsPage />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('ExecutiveReportsPage — تقييد الحوكمة لا يكسر الصفحة', () => {
  it('Manager/TeamLeader: حوكمة 403 لا تكسر الصفحة ويُخفى قسم الحوكمة', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.startsWith('/reports/governance-summary')) return forbidden() as never;
      if (url.startsWith('/reports/submission-completeness')) return Promise.resolve({ data: completeness } as never);
      if (url.startsWith('/reports/kpi-summary')) return Promise.resolve({ data: kpi } as never);
      return Promise.resolve({ data: [] } as never);
    });

    renderPage();

    // الصفحة تُحمَّل بأقسامها المسموحة، دون رسالة الفشل القاتل.
    expect(await screen.findByText('التقارير التنفيذية')).toBeInTheDocument();
    expect(await screen.findByText('اكتمال التقارير')).toBeInTheDocument();
    expect(await screen.findByText('ملخص مؤشرات الأداء')).toBeInTheDocument();
    expect(screen.queryByText('تعذّر تحميل التقارير التنفيذية')).not.toBeInTheDocument();
    // قسم الحوكمة لا يظهر لمن لا يملك صلاحيته.
    expect(screen.queryByText('ملخص الحوكمة')).not.toBeInTheDocument();
  });

  it('Admin/CEO/GM: تتوفّر بيانات الحوكمة فيظهر قسم الحوكمة', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.startsWith('/reports/governance-summary')) return Promise.resolve({ data: governance } as never);
      if (url.startsWith('/reports/submission-completeness')) return Promise.resolve({ data: completeness } as never);
      if (url.startsWith('/reports/kpi-summary')) return Promise.resolve({ data: kpi } as never);
      return Promise.resolve({ data: [] } as never);
    });

    renderPage();

    expect(await screen.findByText('التقارير التنفيذية')).toBeInTheDocument();
    expect(await screen.findByText('ملخص الحوكمة')).toBeInTheDocument();
  });

  it('فشل بيانات جوهرية (اكتمال) يُظهر رسالة الخطأ القاتل', async () => {
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url.startsWith('/reports/submission-completeness')) return forbidden() as never;
      if (url.startsWith('/reports/governance-summary')) return Promise.resolve({ data: governance } as never);
      if (url.startsWith('/reports/kpi-summary')) return Promise.resolve({ data: kpi } as never);
      return Promise.resolve({ data: [] } as never);
    });

    renderPage();

    await waitFor(() =>
      expect(screen.getByText('تعذّر تحميل التقارير التنفيذية')).toBeInTheDocument(),
    );
  });
});

// اختبار منطق توزيع لوحة الرئيسية حسب الدور (UI-ROLE-BASED-HOME-R1).
// يثبت أنّ الخادم حين يُرجِع dashboardType='Employee' لأدوار HR/المالية/محفظة الحسابات،
// تتفرّع HomePage داخل حالة Employee إلى اللوحة الملائمة بدل لوحة الموظّف الافتراضية،
// وأنّ الموظّف العادي (نفس dashboardType) يبقى يرى لوحة الموظّف (ضبط مرجعي).
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import HomePage from './HomePage';

// علامات نصّية فريدة لكل لوحة (تكفي لإثبات أيّ لوحة رُكِّبت).
const HR_MARKER = 'أحدث طلبات الموارد البشرية';
const FINANCE_MARKER = 'هذه اللوحة للمراجعة والقراءة فقط — لا تقوم بحساب أو صرف أو اعتماد أي مستحقات مالية.';
const EMPLOYEE_MARKER = 'تقريرك مطلوب الآن';

// لوحة موظّف يُصنّفها الخادم Employee دائمًا لهذه الأدوار (هو جوهر المشكلة).
const EMPLOYEE_DASHBOARD = {
  dashboardType: 'Employee',
  period: { label: 'الأسبوع الحالي', key: '2026-W26' },
  summaryCards: [],
  widgets: [],
};

let currentData: Record<string, unknown> = {};

function lookup(url: string) {
  const path = url.split('?')[0];
  return currentData[path] ?? [];
}

function setup(roles: string[]) {
  currentData = {
    '/auth/me': {
      userId: 'u-1',
      fullName: 'مستخدم اختبار',
      email: 'test@local',
      isActive: true,
      roles,
      expectedReportCadence: 'Weekly',
    },
    '/dashboard/me': EMPLOYEE_DASHBOARD,
    '/employee-service-requests': [],
  };
}

beforeEach(() => {
  tokenStore.clear();
  tokenStore.set('access', 'refresh'); // مُصادَق ⇒ AuthProvider يقرأ /auth/me
  vi.restoreAllMocks();
  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: lookup(url) } as never),
  );
});

function renderHome() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter initialEntries={['/app']}>
          <HomePage />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('UI-ROLE-BASED-HOME-R1 — توزيع لوحة الرئيسية حسب الدور', () => {
  it('dashboardType=Employee + دور HR ⇒ يعرض لوحة HR ولا يعرض لوحة الموظّف', async () => {
    setup(['HR']);
    renderHome();
    expect(await screen.findByText(HR_MARKER)).toBeInTheDocument();
    expect(screen.queryByText(EMPLOYEE_MARKER)).not.toBeInTheDocument();
  });

  it('dashboardType=Employee + دور FinanceManager ⇒ يعرض لوحة المالية ولا يعرض لوحة الموظّف', async () => {
    setup(['FinanceManager']);
    renderHome();
    expect(await screen.findByText(FINANCE_MARKER)).toBeInTheDocument();
    expect(screen.queryByText(EMPLOYEE_MARKER)).not.toBeInTheDocument();
  });

  it('dashboardType=Employee + دور Accountant ⇒ يعرض لوحة المالية', async () => {
    setup(['Accountant']);
    renderHome();
    expect(await screen.findByText(FINANCE_MARKER)).toBeInTheDocument();
  });

  it('dashboardType=Employee + موظّف عادي ⇒ يبقى يعرض لوحة الموظّف (ضبط مرجعي)', async () => {
    setup(['Employee']);
    renderHome();
    expect(await screen.findByText(EMPLOYEE_MARKER)).toBeInTheDocument();
    expect(screen.queryByText(HR_MARKER)).not.toBeInTheDocument();
    expect(screen.queryByText(FINANCE_MARKER)).not.toBeInTheDocument();
  });
});

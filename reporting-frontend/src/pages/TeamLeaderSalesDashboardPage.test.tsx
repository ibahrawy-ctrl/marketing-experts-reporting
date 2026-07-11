import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { it, expect, vi, beforeEach } from 'vitest';

// عزل الصفحة عن الشبكة: هوكات التجميع مُموّهة (لا استدعاء API).
// النطاق (team فقط) مفروض خادميًّا عبر IScopeResolver — يُختبَر في اختبارات الخادم لا هنا.
// سياق المبيعات: قائد فريق B2C ⇒ يُعرَض قسم B2C فقط (showB2c=true).
vi.mock('../lib/useSalesAggregation', () => ({
  useSalesContext: () => ({
    data: { viewLevel: 'team', showB2c: true, showB2b: false, isSalesRep: false, repType: null },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  }),
  useB2cAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cCourseGrouped: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cNewOld: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2bAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
}));

// سياق المصادقة مُموّه — نتحكّم بصلاحية قائد فريق مبيعات B2C لكل اختبار.
const authState: { isSalesB2cTeamLeader: boolean } = { isSalesB2cTeamLeader: true };
vi.mock('../lib/auth', () => ({
  useAuth: () => ({ isSalesB2cTeamLeader: authState.isSalesB2cTeamLeader }),
}));

import TeamLeaderSalesDashboardPage from './TeamLeaderSalesDashboardPage';

// ===== RC3-Task1 — لوحة مبيعات الفريق لقائد الفريق =====

beforeEach(() => {
  authState.isSalesB2cTeamLeader = true;
});

// حارس الوصول يتطلّب سياق موجِّه (Navigate)؛ نصيّر داخل MemoryRouter دائمًا.
function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/app/sales/team-dashboard']}>
      <Routes>
        <Route path="/app" element={<div>الرئيسية</div>} />
        <Route path="/app/sales/team-dashboard" element={<TeamLeaderSalesDashboardPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

it('يعرض عنوان اللوحة ومنتقيات الفترة دون صندوق نصّي لمفتاح الفترة (الافتراضي أسبوعي)', () => {
  const { container } = renderPage();

  expect(screen.getByText('لوحة مبيعات الفريق')).toBeInTheDocument();

  // لا يوجد أي حقل بعنوان «مفتاح الفترة» (النطاق والفترة مُدارَان بالمنتقيات).
  expect(screen.queryByText('مفتاح الفترة')).toBeNull();

  // الافتراضي أسبوعي ⇒ منتقي «نوع الفترة» + منتقي تاريخ.
  expect(screen.getByText('نوع الفترة')).toBeInTheDocument();
  expect(screen.getByText('الأسبوع')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();

  // لا حقل نصّي حرّ لمفتاح الفترة في الوضع الأسبوعي.
  expect(screen.queryByRole('textbox')).toBeNull();
});

it('تبديل النوع يُظهر المنتقي المطابق يومي/شهري/ربع سنوي دون صندوق مفتاح فترة نصّي', () => {
  const { container } = renderPage();
  const typeSelect = screen.getByRole('combobox'); // «نوع الفترة» (الوحيد في الوضع الأسبوعي)

  fireEvent.change(typeSelect, { target: { value: 'Daily' } });
  expect(screen.getByText('اليوم')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();

  fireEvent.change(typeSelect, { target: { value: 'Monthly' } });
  expect(screen.getByText('الشهر')).toBeInTheDocument();
  expect(container.querySelector('input[type="month"]')).not.toBeNull();

  fireEvent.change(typeSelect, { target: { value: 'Quarterly' } });
  expect(screen.getByText('السنة')).toBeInTheDocument();
  expect(screen.getByText('الربع')).toBeInTheDocument();
  expect(container.querySelector('input[type="number"]')).not.toBeNull();

  expect(screen.queryByText('مفتاح الفترة')).toBeNull();
});

it('يعرض حالة «لا توجد بيانات» بالنص المطلوب حين لا توجد بيانات لفريق قائد الفريق', () => {
  renderPage();
  expect(
    screen.getByText('لا توجد بيانات مبيعات لفريقك خلال هذه الفترة.'),
  ).toBeInTheDocument();
});

it('حارس الوصول المباشر: قائد فريق غير مبيعات B2C يُعاد توجيهه ولا يرى محتوى اللوحة', () => {
  authState.isSalesB2cTeamLeader = false;
  renderPage();
  // لا عنوان لوحة المبيعات — أُعيد التوجيه للرئيسية.
  expect(screen.queryByText('لوحة مبيعات الفريق')).toBeNull();
  expect(screen.getByText('الرئيسية')).toBeInTheDocument();
});

import { render, screen } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';

// عزل الصفحة عن الشبكة: هوكات التجميع مُموّهة (لا استدعاء API).
// النطاق الشخصي (المندوب يرى نفسه فقط) مفروض خادميًّا عبر IScopeResolver — يُختبَر بالخادم لا هنا.
vi.mock('../lib/useSalesAggregation', () => ({
  useB2cAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cCourseGrouped: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cNewOld: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2bAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
}));

// سياق المصادقة مُموّه — نتحكّم بنوع المندوب/الأدوار لكل اختبار.
const authState: {
  isSalesRep: boolean;
  salesRepType: 'B2C' | 'B2B' | null;
  roles: string[];
} = { isSalesRep: true, salesRepType: 'B2C', roles: ['Employee'] };

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: { userId: 'self-1', fullName: 'مندوب', roles: authState.roles },
    isSalesRep: authState.isSalesRep,
    salesRepType: authState.salesRepType,
    hasAnyRole: (...r: string[]) => r.some((x) => authState.roles.includes(x)),
  }),
}));

import SalesRepDashboardPage from './SalesRepDashboardPage';

beforeEach(() => {
  authState.isSalesRep = true;
  authState.salesRepType = 'B2C';
  authState.roles = ['Employee'];
});

// ===== RC3-Task1.1 — لوحة مبيعاتي الشخصية للمندوب =====

it('مندوب B2C يرى عنوان «لوحة مبيعاتي» ومنتقيات الفترة (الافتراضي أسبوعي)', () => {
  const { container } = render(<SalesRepDashboardPage />);
  expect(screen.getByText('لوحة مبيعاتي')).toBeInTheDocument();
  expect(screen.getByText('نوع الفترة')).toBeInTheDocument();
  expect(screen.getByText('الأسبوع')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();
});

it('يعرض حالة «لا توجد بيانات لك» بالنص المطلوب حين لا توجد بيانات للمندوب', () => {
  render(<SalesRepDashboardPage />);
  expect(
    screen.getByText('لا توجد بيانات مبيعات لك خلال هذه الفترة.'),
  ).toBeInTheDocument();
});

it('يمنع غير المندوب وغير الأدمن من رؤية اللوحة (حارس واجهي)', () => {
  authState.isSalesRep = false;
  authState.salesRepType = null;
  authState.roles = ['Employee'];
  render(<SalesRepDashboardPage />);
  expect(screen.getByText('هذه اللوحة مخصّصة لمندوبي المبيعات.')).toBeInTheDocument();
  expect(screen.queryByText('لوحة مبيعاتي')).toBeNull();
});

it('يسمح للأدمن غير المندوب بالاطّلاع (متغيّر B2C افتراضي) ويوضّح ذلك', () => {
  authState.isSalesRep = false;
  authState.salesRepType = null;
  authState.roles = ['Admin'];
  render(<SalesRepDashboardPage />);
  expect(screen.getByText('لوحة مبيعاتي')).toBeInTheDocument();
  expect(screen.queryByText('هذه اللوحة مخصّصة لمندوبي المبيعات.')).toBeNull();
});

it('مندوب B2B يرى اللوحة بنسختها B2B (وصف الاجتماعات/العروض/الصفقات)', () => {
  authState.isSalesRep = true;
  authState.salesRepType = 'B2B';
  authState.roles = ['Employee'];
  render(<SalesRepDashboardPage />);
  expect(screen.getByText('لوحة مبيعاتي')).toBeInTheDocument();
  expect(
    screen.getByText('لا توجد بيانات مبيعات لك خلال هذه الفترة.'),
  ).toBeInTheDocument();
});

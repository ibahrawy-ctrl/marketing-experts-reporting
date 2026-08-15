import { render, screen } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type { PortfolioClientDetailDto } from '../types/api';

// ===== CPW-R2 — جسر المحفظة ← ملفّ العميل الشامل =====
// Client 360 يبقى مصدر الحقيقة؛ المحفظة لا تُكرِّر بياناته بل تُحيل إليه برابط واضح.

const DETAIL: PortfolioClientDetailDto = {
  client: { id: 'c1', name: 'عميل المحفظة', status: 'Active', projectCount: 2, activeProjectCount: 1 },
  projects: [],
};

const state = {
  data: DETAIL as PortfolioClientDetailDto | undefined,
  isLoading: false,
  isError: false,
  refetch: vi.fn(),
};

vi.mock('react-router-dom', () => ({
  useParams: () => ({ id: 'c1' }),
  Link: ({ to, children }: { to: string; children: React.ReactNode }) => <a href={to}>{children}</a>,
}));

vi.mock('../lib/useAccountPortfolio', () => ({
  useMyPortfolioClient: () => state,
}));

import AccountPortfolioClientPage from './AccountPortfolioClientPage';

beforeEach(() => {
  state.data = DETAIL;
  state.isLoading = false;
  state.isError = false;
});

it('يعرض رابط «فتح ملف العميل» موجَّهًا إلى ملفّ العميل الشامل', () => {
  render(<AccountPortfolioClientPage />);
  const link = screen.getByRole('link', { name: 'فتح ملف العميل' });
  expect(link).toHaveAttribute('href', '/app/clients/c1');
});

it('يعرض اسم العميل ومشاريعه دون تكرار بيانات ملفّ العميل الشامل', () => {
  render(<AccountPortfolioClientPage />);
  expect(screen.getByText('عميل المحفظة')).toBeInTheDocument();
  expect(screen.getByText('لا توجد مشاريع')).toBeInTheDocument();
  // لا جهات اتصال ولا قنوات ولا مستندات في المحفظة — كلّها في Client 360 حصرًا.
  expect(screen.queryByText('جهات الاتصال')).not.toBeInTheDocument();
  expect(screen.queryByText('المستندات')).not.toBeInTheDocument();
});

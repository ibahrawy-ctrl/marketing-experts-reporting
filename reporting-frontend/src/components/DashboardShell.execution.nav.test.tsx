import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi, beforeEach } from 'vitest';
import type { Role } from '../types/api';

// ===== RC4-Task4C / UI-NAV-RESTRUCTURE-R2 — ظهور تبويب «لوحة تنفيذ الفريق» ضمن وحدة «التقارير» =====
// يظهر لـ TeamLeader/Manager/GeneralManager/CEO/Admin؛ لا للموظّف العادي ولا لمدير الحساب (AccountPortfolioReader).
// بعد إعادة الهيكلة صار تبويبًا في وحدة «التقارير»، فنصيّر على مسار من مساراتها (/app/submissions).

const authState: { roles: Role[]; isSalesRep: boolean; jobRoleCode: string | null } = {
  roles: ['Employee'],
  isSalesRep: false,
  jobRoleCode: null,
};

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: { userId: 'u1', fullName: 'مستخدم', email: 'u@test.local', roles: authState.roles, jobRoleCode: authState.jobRoleCode },
    logout: vi.fn(),
    changePassword: vi.fn(),
    changeEmail: vi.fn(),
    canApprove: false,
    hasAnyRole: (...r: Role[]) => r.some((x) => authState.roles.includes(x)),
    isSalesRep: authState.isSalesRep,
    isSalesB2cTeamLeader: authState.roles.includes('TeamLeader') && authState.jobRoleCode === 'SALES_B2C_TL',
    permissions: new Set<string>(),
    scopeType: null,
  }),
}));
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';

const LINK = 'لوحة تنفيذ الفريق';
const SALES_TEAM = 'لوحة مبيعات الفريق';

// نفتح «المزيد ⋯» بعد التصيير كي يشمل الفحص أقسام الوحدة كاملة لا السبعة الأولى فقط.
function renderShell() {
  const result = render(
    <MemoryRouter initialEntries={['/app/submissions']}>
      <DashboardShell>
        <div>محتوى</div>
      </DashboardShell>
    </MemoryRouter>,
  );
  const more = screen.queryByRole('button', { name: 'المزيد ⋯' });
  if (more) fireEvent.click(more);
  return result;
}

beforeEach(() => {
  localStorage.clear();
  authState.roles = ['Employee'];
  authState.isSalesRep = false;
  authState.jobRoleCode = null;
});

it.each<[Role]>([['TeamLeader'], ['Manager'], ['GeneralManager'], ['CEO'], ['Admin']])(
  'الدور %s يرى «لوحة تنفيذ الفريق»',
  (role) => {
    authState.roles = [role];
    renderShell();
    expect(screen.getByText(LINK)).toBeInTheDocument();
  },
);

it('الموظف العادي لا يرى «لوحة تنفيذ الفريق»', () => {
  authState.roles = ['Employee'];
  renderShell();
  expect(screen.queryByText(LINK)).toBeNull();
});

it('مدير الحساب (AccountPortfolioReader) لا يرى «لوحة تنفيذ الفريق»', () => {
  authState.roles = ['AccountPortfolioReader'];
  renderShell();
  expect(screen.queryByText(LINK)).toBeNull();
});

it('لا يكسر تنقّل المبيعات: قائد فريق مبيعات B2C يرى لوحتَي المبيعات والتنفيذ معًا', () => {
  authState.roles = ['TeamLeader'];
  authState.jobRoleCode = 'SALES_B2C_TL';
  renderShell();
  expect(screen.getByText(SALES_TEAM)).toBeInTheDocument();
  expect(screen.getByText(LINK)).toBeInTheDocument();
});

it('قائد فريق تنفيذ (غير مبيعات) يرى «لوحة تنفيذ الفريق» دون «لوحة مبيعات الفريق»', () => {
  authState.roles = ['TeamLeader'];
  authState.jobRoleCode = null;
  renderShell();
  expect(screen.getByText(LINK)).toBeInTheDocument();
  expect(screen.queryByText(SALES_TEAM)).toBeNull();
});

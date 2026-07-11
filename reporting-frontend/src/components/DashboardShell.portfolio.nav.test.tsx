import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi, beforeEach } from 'vitest';
import type { Role } from '../types/api';

// ===== Account-Manager Portfolio Navigation — عنصر تنقّل رئيسيّ مباشر «المشاريع والعملاء» =====
// وحدة مستقلّة (id=portfolio) لعنصر «المشاريع والعملاء» خارج وحدة «الفرق والموظفون».
// شرط الظهور مقصور على مدير الحساب حصرًا (jobRoleCode === 'ACCOUNT_MGR')، لا على دور
// AccountPortfolioReader. نفس المسار (/app/account-portfolio) — تنظيم بصريّ فقط، الحماية خادميّة.

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
  }),
}));
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';

const PORTFOLIO_MODULE = 'المشاريع والعملاء';
const PEOPLE_MODULE = 'الفرق والموظفون';
const OLD_LABEL = 'مشاريعي';

function renderShell(route = '/app/account-portfolio') {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <DashboardShell>
        <div>محتوى</div>
      </DashboardShell>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  localStorage.clear();
  authState.roles = ['Employee'];
  authState.isSalesRep = false;
  authState.jobRoleCode = null;
});

it('مدير الحساب (jobRoleCode === ACCOUNT_MGR) يرى عنصر «المشاريع والعملاء» الرئيسيّ المباشر', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = 'ACCOUNT_MGR';
  renderShell();
  expect(screen.getByText(PORTFOLIO_MODULE)).toBeInTheDocument();
});

it('مدير الحساب لا يرى صفحة المحفظة داخل وحدة «الفرق والموظفون»', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = 'ACCOUNT_MGR';
  renderShell();
  // «المشاريع والعملاء» عنصر رئيسيّ مستقلّ؛ وحدة «الفرق والموظفون» لا تظهر لمدير الحساب أصلًا.
  expect(screen.queryByText(PEOPLE_MODULE)).toBeNull();
});

it('التسمية القديمة «مشاريعي» لم تعد موجودة في التنقّل', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = 'ACCOUNT_MGR';
  renderShell();
  expect(screen.queryByText(OLD_LABEL)).toBeNull();
});

it('حامل دور AccountPortfolioReader بلا مسمّى ACCOUNT_MGR لا يرى العنصر (الظهور بالمسمّى لا بالدور)', () => {
  authState.roles = ['AccountPortfolioReader'];
  authState.jobRoleCode = null;
  renderShell('/app/submissions');
  expect(screen.queryByText(PORTFOLIO_MODULE)).toBeNull();
});

it('الأدمن (بلا مسمّى ACCOUNT_MGR) لا يرى عنصر «المشاريع والعملاء»', () => {
  authState.roles = ['Admin'];
  authState.jobRoleCode = null;
  renderShell('/app/submissions');
  expect(screen.queryByText(PORTFOLIO_MODULE)).toBeNull();
});

it('الموظف العادي (بلا مسمّى ACCOUNT_MGR) لا يرى عنصر «المشاريع والعملاء»', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = null;
  renderShell('/app/submissions');
  expect(screen.queryByText(PORTFOLIO_MODULE)).toBeNull();
});

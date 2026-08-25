import { render, screen, within } from '@testing-library/react';
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
    permissions: new Set<string>(),
    scopeType: null,
  }),
}));
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';
import { MODULES } from '../lib/navConfig';

// P3-NAV-002: صارت الوحدة «العملاء والمشروعات»، وصفحة المحفظة قسمٌ داخلها اسمه «مشاريع عملائي».
// الوحدة نفسها تظهر لمن يرى العملاء أو المشروعات؛ **القسم** وحده مقصور على مسمّى مدير الحساب،
// فالفحص يقع على القسم لا على عنوان الوحدة.
const PORTFOLIO_MODULE = 'العملاء والمشروعات';
const PORTFOLIO_ITEM = 'مشاريع عملائي';
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

it('مدير الحساب (jobRoleCode === ACCOUNT_MGR) يرى وحدة «العملاء والمشروعات» في الشريط الجانبيّ', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = 'ACCOUNT_MGR';
  renderShell();
  // نحصر البحث في الشريط الجانبيّ: فتات الخبز تعرض اسم الوحدة أيضًا.
  const side = within(screen.getByRole('navigation', { name: 'الوحدات الرئيسية' }));
  expect(side.getByText(PORTFOLIO_MODULE)).toBeInTheDocument();
});

it('صفحة المحفظة تنتمي لوحدة «العملاء والمشروعات» لا لوحدة «الموظفون»', () => {
  const owner = MODULES.find((m) => m.items.some((i) => i.target === '/app/account-portfolio'));
  expect(owner?.id).toBe('portfolio');
  expect(MODULES.find((m) => m.id === 'people')?.items.map((i) => i.target)).not.toContain(
    '/app/account-portfolio',
  );
});

it('التسمية القديمة «مشاريعي» لم تعد موجودة في التنقّل', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = 'ACCOUNT_MGR';
  renderShell();
  expect(screen.queryByText(OLD_LABEL)).toBeNull();
});

it('حامل دور AccountPortfolioReader بلا مسمّى ACCOUNT_MGR لا يرى القسم (الظهور بالمسمّى لا بالدور)', () => {
  authState.roles = ['AccountPortfolioReader'];
  authState.jobRoleCode = null;
  // نصيّر على «العملاء» لا على مسار المحفظة نفسه: فتات الخبز تصف الموضع الحاليّ دائمًا
  // (حتّى عند الوصول المباشر بالرابط)، والمقصود هنا فحص **قائمة** الأقسام لا الموضع.
  renderShell('/app/clients');
  expect(screen.queryByText(PORTFOLIO_ITEM)).toBeNull();
});

it('الأدمن (بلا مسمّى ACCOUNT_MGR) لا يرى قسم «مشاريع عملائي»', () => {
  authState.roles = ['Admin'];
  authState.jobRoleCode = null;
  // نصيّر على «العملاء» لا على مسار المحفظة نفسه: فتات الخبز تصف الموضع الحاليّ دائمًا
  // (حتّى عند الوصول المباشر بالرابط)، والمقصود هنا فحص **قائمة** الأقسام لا الموضع.
  renderShell('/app/clients');
  expect(screen.queryByText(PORTFOLIO_ITEM)).toBeNull();
});

it('الموظف العادي (بلا مسمّى ACCOUNT_MGR) لا يرى قسم «مشاريع عملائي» ولا وحدته', () => {
  authState.roles = ['Employee'];
  authState.jobRoleCode = null;
  renderShell('/app/submissions');
  expect(screen.queryByText(PORTFOLIO_ITEM)).toBeNull();
  expect(screen.queryByText(PORTFOLIO_MODULE)).toBeNull();
});

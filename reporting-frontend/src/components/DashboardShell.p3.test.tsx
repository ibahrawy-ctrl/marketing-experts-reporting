import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Role } from '../types/api';

// ===== P3-NAV-002/004 — الهيكل العام: حالة النشاط الأب/الابن، فتات الخبز، درج الهاتف =====
// الظهور هنا عرضٌ فقط؛ الحراسة على الخادم وفي ProtectedRoute. ما يُختبَر: ألّا يفقد المستخدم
// موضعه (حتّى بالوصول المباشر أو عبر مسار مدمَج)، وأن يبقى الدرج قابلًا للتشغيل بالكيبورد.

const authState: { roles: Role[]; permissions: string[]; scopeType: string | null; jobRoleCode: string | null } = {
  roles: ['Admin'],
  permissions: [],
  scopeType: null,
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
    isSalesRep: false,
    isSalesB2cTeamLeader: false,
    permissions: new Set(authState.permissions),
    scopeType: authState.scopeType,
  }),
}));
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';

function renderShell(route: string) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <DashboardShell>
        <div>محتوى</div>
      </DashboardShell>
    </MemoryRouter>,
  );
}

const sidebar = () => within(screen.getAllByRole('navigation', { name: 'الوحدات الرئيسية' })[0]);
const crumbs = () => within(screen.getByRole('navigation', { name: 'مسار التنقّل' }));

beforeEach(() => {
  localStorage.clear();
  authState.roles = ['Admin'];
  authState.permissions = [];
  authState.scopeType = null;
  authState.jobRoleCode = null;
});

describe('حالة النشاط الأب/الابن', () => {
  it('الوحدة الحاوية تُعلَّم نشطة وإن كان المسار قسمًا داخلها لا وجهتها', () => {
    renderShell('/app/sales-aggregation');
    const active = sidebar()
      .getAllByRole('link')
      .filter((a) => a.getAttribute('aria-current') === 'page');
    expect(active).toHaveLength(1);
    expect(active[0]).toHaveTextContent('التقارير');
  });

  it('القسم النشط وحده مُعلَّم في شريط الأقسام', () => {
    renderShell('/app/submissions');
    const tabs = within(screen.getByRole('tablist'))
      .getAllByRole('tab')
      .filter((t) => t.getAttribute('aria-current') === 'page');
    expect(tabs).toHaveLength(1);
    expect(tabs[0]).toHaveTextContent('تقارير النطاق');
  });

  it('المسار المدمَج (alias) يُبرِز عنصره المرجعيّ لا شيئًا آخر', () => {
    renderShell('/app/escalations');
    expect(crumbs().getByText('الحوكمة')).toBeInTheDocument();
    expect(crumbs().getByText('التصعيدات')).toBeInTheDocument();
  });

  it('المسار الديناميّ يُبرِز قسمه ويعرض مقطعًا عامًّا بلا اسم مورد', () => {
    renderShell('/app/teams/abc-123');
    expect(crumbs().getByText('فرق العمل')).toBeInTheDocument();
    expect(crumbs().getByText('التفاصيل')).toBeInTheDocument();
    expect(crumbs().queryByText('abc-123')).toBeNull();
  });

  it('المسار غير المعروف لا يعرض فتات خبز مضلِّلة', () => {
    renderShell('/app/does-not-exist');
    expect(screen.queryByRole('navigation', { name: 'مسار التنقّل' })).toBeNull();
  });
});

describe('النطاق يغيّر الوجهة لا الصلاحيّة', () => {
  it('وضع الذات يقود «لوحة الأداء» إلى السطح الشخصيّ', () => {
    authState.roles = ['Employee'];
    authState.scopeType = 'own';
    renderShell('/app/my-kpi');
    expect(sidebar().getByText('الأداء وKPI').closest('a')).toHaveAttribute('href', '/app/my-kpi');
  });

  it('النطاق الإداريّ يقودها إلى اللوحة الموحّدة', () => {
    authState.scopeType = 'company';
    renderShell('/app/performance');
    expect(sidebar().getByText('الأداء وKPI').closest('a')).toHaveAttribute('href', '/app/performance');
  });
});

describe('القدرة الخادميّة تحكم الظهور', () => {
  it('بلا قدرة عمليّات الموارد البشريّة لا يظهر قسمها ولو للأدمن', () => {
    renderShell('/app/employee/me');
    expect(within(screen.getByRole('tablist')).queryByText('عمليّات الموارد البشريّة')).toBeNull();
  });

  it('مع القدرة يظهر القسم', () => {
    authState.permissions = ['HrOperations.View'];
    renderShell('/app/employee/me');
    const bar = within(screen.getByRole('tablist'));
    const more = bar.queryByRole('button', { name: 'المزيد ⋯' });
    if (more) fireEvent.click(more);
    expect(bar.getByText('عمليّات الموارد البشريّة')).toBeInTheDocument();
  });
});

describe('درج الهاتف (RTL وكيبورد)', () => {
  it('يفتح من اليمين كنافذة حواريّة معنونة', () => {
    renderShell('/app');
    fireEvent.click(screen.getByRole('button', { name: 'القائمة' }));
    const drawer = screen.getByRole('dialog', { name: 'قائمة التنقّل' });
    expect(drawer.className).toContain('right-0');
    expect(drawer).toHaveAttribute('aria-modal', 'true');
  });

  it('Escape يُغلقه ويعيد التركيز إلى زرّ الفتح', () => {
    renderShell('/app');
    const button = screen.getByRole('button', { name: 'القائمة' });
    fireEvent.click(button);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(button);
  });

  it('النقر على طبقة التعتيم يُغلقه', () => {
    const { container } = renderShell('/app');
    fireEvent.click(screen.getByRole('button', { name: 'القائمة' }));
    fireEvent.click(container.querySelector('[aria-hidden="true"]')!);
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('النقر على وحدة داخل الدرج يُغلقه فلا يبقى فوق المحتوى', () => {
    renderShell('/app');
    fireEvent.click(screen.getByRole('button', { name: 'القائمة' }));
    const drawer = screen.getByRole('dialog', { name: 'قائمة التنقّل' });
    fireEvent.click(within(drawer).getByText('التقارير'));
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('زرّ القائمة يعلن حالته للقارئ الشاشيّ', () => {
    renderShell('/app');
    const button = screen.getByRole('button', { name: 'القائمة' });
    expect(button).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });
});

describe('طيّ الشريط الجانبيّ', () => {
  it('التفضيل يُحفَظ محلّيًّا ولا يحمل أيّ دور أو صلاحيّة', () => {
    renderShell('/app');
    fireEvent.click(screen.getByRole('button', { name: 'طيّ القائمة' }));
    expect(localStorage.getItem('me_nav_collapsed_v2')).toBe('1');
    expect(screen.getByRole('button', { name: 'توسيع القائمة' })).toBeInTheDocument();
  });
});

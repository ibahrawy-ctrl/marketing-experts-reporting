import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';

// بوّابة المسارات تُقاس بالتوجيه الفعليّ لا بقائمة الأدوار: `ProtectedRoute` يحوّل غير المخوَّل
// إلى `/app`، فالشاشة البديلة هي الدليل الوحيد على الحجب.
const roles = vi.hoisted(() => ({ current: ['Employee'] as string[] }));

vi.mock('./lib/auth', () => ({
  useAuth: () => ({
    user: { userId: 'u1', fullName: 'موظف تنفيذ', roles: roles.current },
    loading: false,
    hasAnyRole: (...wanted: string[]) => wanted.some((r) => roles.current.includes(r)),
    canManageClients: false,
  }),
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('./components/DashboardShell', () => ({
  DashboardShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('./pages/Project360Page', () => ({
  default: () => <div data-testid="project-360" />,
}));

vi.mock('./pages/HomePage', () => ({
  default: () => <div data-testid="home" />,
}));

vi.mock('./pages/ProjectDetailPage', () => ({
  default: () => <div data-testid="project-detail" />,
}));

import App from './App';

describe('App landing', () => {
  it('renders the system title and login link', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByText('نظام تقارير الأداء والتشغيل الداخلي'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'تسجيل الدخول' })).toBeInTheDocument();
  });
});

describe('بوّابة مساحة عمل Project 360 (P360-WF-R2 §10 · §12)', () => {
  // المنفِّذ هو من يرفع ادّعاء التنفيذ في جسر التنفيذ الهجين. كانت البوّابة المحلّيّة تُخرجه
  // إلى `/app` بينما الخادم يمنحه المشروع فعلًا (200 على المسارات الأربعة) — اشتقاق تخويل
  // موازٍ في الواجهة، وهو بالضبط ما تنفيه §12.
  it('يفتح الشاشة للموظّف المنفِّذ ولا يحوّله إلى اللوحة', () => {
    roles.current = ['Employee'];
    render(
      <MemoryRouter initialEntries={['/app/projects/p1/360']}>
        <App />
      </MemoryRouter>,
    );
    expect(screen.getByTestId('project-360')).toBeInTheDocument();
  });

  it('يحجب من لا دور تنفيذيًّا له، فيبقى الحجب حجبًا لا انفتاحًا شاملًا', () => {
    roles.current = ['Accountant'];
    render(
      <MemoryRouter initialEntries={['/app/projects/p1/360']}>
        <App />
      </MemoryRouter>,
    );
    expect(screen.queryByTestId('project-360')).not.toBeInTheDocument();
    expect(screen.getByTestId('home')).toBeInTheDocument();
  });
});

describe('بوّابة صفحة تفاصيل المشروع (R2.1 · GAP-R21-05)', () => {
  // شاشة مسارات العمل («أهداف العمل») لا وجود لها إلّا في هذه الصفحة، والخادم يمنح إدارتها
  // بالمورد لا بالدور: مالك مشروع بدور Employee يقبله `CanManagePlanAsync` بينما كانت بوّابة
  // المسار تُخرجه إلى `/app` قبل أن يرى الشاشة أصلًا. الاختبار يقيس التوجيه الفعليّ لأنّ
  // قائمة الأدوار وحدها لا تُثبت أنّ المستخدم بلغ الشاشة.
  it('يفتح صفحة المشروع للموظّف المُسنَد مالكًا فلا تنفصل الشاشة عن الخادم', () => {
    roles.current = ['Employee'];
    render(
      <MemoryRouter initialEntries={['/app/projects/p1']}>
        <App />
      </MemoryRouter>,
    );
    expect(screen.getByTestId('project-detail')).toBeInTheDocument();
  });

  it('يبقى الحجب قائمًا لمن لا دور تنفيذيًّا ولا تنفيذ له', () => {
    roles.current = ['Accountant'];
    render(
      <MemoryRouter initialEntries={['/app/projects/p1']}>
        <App />
      </MemoryRouter>,
    );
    expect(screen.queryByTestId('project-detail')).not.toBeInTheDocument();
    expect(screen.getByTestId('home')).toBeInTheDocument();
  });
});

import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi, beforeEach } from 'vitest';
import type { Role } from '../types/api';

// ===== RC3-Task1.1A — ظهور روابط المبيعات الثلاثة في القائمة الجانبية =====
// تجميع المبيعات: Manager/GM/CEO/Admin فقط.
// لوحة مبيعات الفريق: TeamLeader فقط.
// لوحة مبيعاتي: مندوب المبيعات (isSalesRep) فقط — لا الأدمن.

// سياق المصادقة مُموّه — نتحكّم بالأدوار وحالة المندوب لكل اختبار.
const authState: { roles: Role[]; isSalesRep: boolean } = {
  roles: ['Employee'],
  isSalesRep: false,
};

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: { userId: 'u1', fullName: 'مستخدم', roles: authState.roles, jobRoleCode: null },
    logout: vi.fn(),
    hasAnyRole: (...r: Role[]) => r.some((x) => authState.roles.includes(x)),
    isSalesRep: authState.isSalesRep,
  }),
}));

// عزل عن الشبكة/الوقت الحقيقي.
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';

const AGG = 'تجميع المبيعات';
const TEAM = 'لوحة مبيعات الفريق';
const MINE = 'لوحة مبيعاتي';

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/app']}>
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
});

it('قائد الفريق يرى «لوحة مبيعات الفريق» فقط دون «تجميع المبيعات» أو «لوحة مبيعاتي»', () => {
  authState.roles = ['TeamLeader'];
  renderShell();
  expect(screen.getByText(TEAM)).toBeInTheDocument();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('المدير يرى «تجميع المبيعات» فقط دون «لوحة مبيعات الفريق» أو «لوحة مبيعاتي»', () => {
  authState.roles = ['Manager'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('الأدمن يرى «تجميع المبيعات» فقط (لا لوحة الفريق ولا لوحة مبيعاتي)', () => {
  authState.roles = ['Admin'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('المدير العام (GM) يرى «تجميع المبيعات» فقط', () => {
  authState.roles = ['GeneralManager'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('الرئيس التنفيذي (CEO) يرى «تجميع المبيعات» فقط', () => {
  authState.roles = ['CEO'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('مندوب المبيعات (isSalesRep) يرى «لوحة مبيعاتي» فقط دون التجميع أو لوحة الفريق', () => {
  authState.roles = ['Employee'];
  authState.isSalesRep = true;
  renderShell();
  expect(screen.getByText(MINE)).toBeInTheDocument();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(TEAM)).toBeNull();
});

it('الموظف العادي (غير مندوب) لا يرى أيًّا من روابط المبيعات الثلاثة', () => {
  authState.roles = ['Employee'];
  authState.isSalesRep = false;
  renderShell();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('عناصر قائمة أخرى في مجموعة التشغيل تبقى ظاهرة (لا كسر للترتيب/العناصر)', () => {
  authState.roles = ['Admin'];
  renderShell();
  expect(screen.getByText('الرئيسية')).toBeInTheDocument();
  expect(screen.getByText('التقارير المقدمة')).toBeInTheDocument();
  expect(screen.getByText('تقويم التقارير')).toBeInTheDocument();
});

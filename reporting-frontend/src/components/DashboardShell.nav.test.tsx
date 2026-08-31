import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi, beforeEach } from 'vitest';
import { FEATURES } from '../lib/navConfig';
import type { Role } from '../types/api';

// ===== UI-NAV-RESTRUCTURE-R2 — ظهور تبويبات المبيعات الثلاثة داخل وحدة «التقارير» =====
// بعد إعادة الهيكلة صارت روابط المبيعات تبويبات ضمن وحدة «التقارير»، فتظهر في شريط التبويبات
// حين تكون الوحدة نشطة (نصيّرها على مسار من مسارات التقارير مثل /app/submissions).
// تجميع المبيعات: Manager/GM/CEO/Admin فقط. لوحة مبيعات الفريق: قائد فريق مبيعات B2C (SALES_B2C_TL) فقط.
// لوحة مبيعاتي: مندوب المبيعات (isSalesRep) فقط — لا الأدمن.

// سياق المصادقة مُموّه — نتحكّم بالأدوار وحالة المندوب وقائد مبيعات B2C لكل اختبار.
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
    // P3-NAV-001: قدرات الخادم كما تصل للواجهة — لا قدرة ولا نطاق في هذه الحالات.
    permissions: new Set<string>(),
    scopeType: null,
    // P123-R1: كلّ الميزات مفتوحة عمدًا — الادّعاءات هنا عن الأدوار والمسمّيات والقدرات،
    // وبوّابة الميزة بُعد مستقلّ يُقاس في `navConfig.test.ts` فلا تُلوَّث به هذه القياسات.
    features: new Set<string>(Object.values(FEATURES)),
  }),
}));

// عزل عن الشبكة/الوقت الحقيقي.
vi.mock('../lib/useNotifications', () => ({ useNotificationRealtime: () => undefined }));
vi.mock('./NotificationsBell', () => ({ NotificationsBell: () => null }));

import { DashboardShell } from './DashboardShell';

const AGG = 'تجميع المبيعات';
const TEAM = 'لوحة مبيعات الفريق';
const MINE = 'لوحة مبيعاتي';

// نصيّر على مسار من وحدة «التقارير» كي يظهر شريط تبويبات الوحدة (فيه تبويبات المبيعات).
// شريط الأقسام يطوي ما بعد سبعة عناصر داخل «المزيد ⋯» (P3-NAV-004)، فنفتحه بعد التصيير
// كي يشمل الفحص كلّ أقسام الوحدة لا المرئيّ منها فقط — إثباتًا/نفيًا على القائمة كاملة.
function renderShell(route = '/app/submissions') {
  const result = render(
    <MemoryRouter initialEntries={[route]}>
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

it('قائد فريق مبيعات B2C (SALES_B2C_TL) يرى تبويب «لوحة مبيعات الفريق» فقط دون «تجميع المبيعات» أو «لوحة مبيعاتي»', () => {
  authState.roles = ['TeamLeader'];
  authState.jobRoleCode = 'SALES_B2C_TL';
  renderShell();
  expect(screen.getByText(TEAM)).toBeInTheDocument();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('قائد فريق غير مبيعات (بلا SALES_B2C_TL) لا يرى تبويب «لوحة مبيعات الفريق»', () => {
  authState.roles = ['TeamLeader'];
  authState.jobRoleCode = null;
  renderShell();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('قائد فريق التنفيذ (مسمّى آخر) لا يرى تبويب «لوحة مبيعات الفريق»', () => {
  authState.roles = ['TeamLeader'];
  authState.jobRoleCode = 'EXECUTION_TL';
  renderShell();
  expect(screen.queryByText(TEAM)).toBeNull();
});

it('المدير يرى تبويب «تجميع المبيعات» فقط دون «لوحة مبيعات الفريق» أو «لوحة مبيعاتي»', () => {
  authState.roles = ['Manager'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('الأدمن يرى تبويب «تجميع المبيعات» فقط (لا لوحة الفريق ولا لوحة مبيعاتي)', () => {
  authState.roles = ['Admin'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('المدير العام (GM) يرى تبويب «تجميع المبيعات» فقط', () => {
  authState.roles = ['GeneralManager'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('الرئيس التنفيذي (CEO) يرى تبويب «تجميع المبيعات» فقط', () => {
  authState.roles = ['CEO'];
  renderShell();
  expect(screen.getByText(AGG)).toBeInTheDocument();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('مندوب المبيعات (isSalesRep) يرى تبويب «لوحة مبيعاتي» فقط دون التجميع أو لوحة الفريق', () => {
  authState.roles = ['Employee'];
  authState.isSalesRep = true;
  renderShell();
  expect(screen.getByText(MINE)).toBeInTheDocument();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(TEAM)).toBeNull();
});

it('الموظف العادي (غير مندوب) لا يرى أيًّا من تبويبات المبيعات الثلاثة', () => {
  authState.roles = ['Employee'];
  authState.isSalesRep = false;
  renderShell();
  expect(screen.queryByText(AGG)).toBeNull();
  expect(screen.queryByText(TEAM)).toBeNull();
  expect(screen.queryByText(MINE)).toBeNull();
});

it('وحدات الشريط الجانبي وتبويبات وحدة التقارير تبقى ظاهرة (لا كسر للتنقّل المدمج)', () => {
  authState.roles = ['Admin'];
  renderShell();
  // وحدات رئيسية في الشريط الجانبي.
  expect(screen.getByText('الرئيسية')).toBeInTheDocument();
  // تبويبات وحدة التقارير النشطة (اللصيقة بالمسمّى الحرفي في navConfig).
  // ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1: مساران متوازيان — «تقاريري» الشخصيّ (لكل مصادَق
  // عليه) و«تقارير الفريق» الإداريّ (EXEC_VIEW). الأدمن يرى كليهما دون أن يستبدل أحدهما الآخر.
  // نحصر البحث في شريط الأقسام: فتات الخبز تعرض اسم القسم النشط أيضًا، فالبحث العامّ يلتبس.
  const tabs = within(screen.getByRole('tablist'));
  expect(tabs.getByText('تقاريري')).toBeInTheDocument();
  expect(tabs.getByText('تقارير النطاق')).toBeInTheDocument();
  expect(tabs.getByText('التقويم والاستحقاقات')).toBeInTheDocument();
});

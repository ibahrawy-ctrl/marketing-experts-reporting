// ======================================================================
// DEF-R5-002 — نافذة خدمة الموظّف على سطح إدارة الموظّفين القائم (قرار مالك المنتج، R5)
//
// شرط مالك المنتج الصريح: **لا شاشة مستقلّة**. إثبات ذلك لا يكون بقراءة الكود بل بقياس
// أنّ الحقلين يُقرآن ويُكتبان من داخل صفّ الموظّف نفسه في `HrEmployeesPage`، وأنّ الفراغ
// يُعرَض حالةً مسمّاة («غير مسجَّل» / «على رأس العمل») لا فراغًا يُفسَّر خروجًا.
//
// `api` وحده متجسَّس، وبقيّة الوحدة حقيقيّة ⟹ جسم الطلب المُرسَل دليل مقيس.
// ======================================================================

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { HrDirectoryUserDto } from '../types/api';

const ONGOING: HrDirectoryUserDto = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  fullName: 'منى على رأس العمل',
  email: 'mona@test.local',
  isActive: true,
  departmentId: null,
  teamId: null,
  managerId: null,
  jobRoleId: null,
  isSensitive: false,
  canEdit: true,
  hireDate: '2026-01-04',
  exitDate: null,
};

const UNRECORDED: HrDirectoryUserDto = {
  ...ONGOING,
  id: 'aaaaaaaa-0000-0000-0000-000000000002',
  fullName: 'فهد بلا نافذة مسجَّلة',
  email: 'fahad@test.local',
  hireDate: null,
  exitDate: null,
};

let roles: string[] = ['HR'];

vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({
    user: { userId: 'u-hr', roles },
    hasAnyRole: (...r: string[]) => r.some((x) => roles.includes(x)),
  }),
}));

import HrEmployeesPage from './HrEmployeesPage';

let patchCalls: { url: string; body: unknown }[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  patchCalls = [];
  roles = ['HR'];

  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/directory/hr/users') return Promise.resolve({ data: [ONGOING, UNRECORDED] } as never);
    return Promise.resolve({ data: [] } as never);
  });
  vi.spyOn(api, 'patch').mockImplementation((url: string, body?: unknown) => {
    patchCalls.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
});

async function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <HrEmployeesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return await screen.findByText(ONGOING.fullName);
}

function rowOf(name: string) {
  return screen.getByText(name).closest('tr') as HTMLElement;
}

describe('DEF-R5-002 — نافذة الخدمة داخل شاشة الموظّفين نفسها', () => {
  // ===== 1: القراءة من الصفّ نفسه، والفراغ حالة مسمّاة =====
  it('تعرض نافذة الخدمة في صفّ الموظّف، و«غير مسجَّل» بدل فراغ يُفسَّر خروجًا', async () => {
    await renderPage();

    const ongoing = within(rowOf(ONGOING.fullName));
    expect(ongoing.getByText('التحاق: 2026-01-04')).toBeInTheDocument();
    // لم تنتهِ خدمته ⇒ يُعرَض «على رأس العمل» لا فراغًا ولا تاريخًا مُفترَضًا.
    expect(ongoing.getByText('انتهاء الخدمة: على رأس العمل')).toBeInTheDocument();

    const unrecorded = within(rowOf(UNRECORDED.fullName));
    expect(unrecorded.getByText('التحاق: غير مسجَّل')).toBeInTheDocument();
  });

  // ===== 2: التحرير في مكانه — لا شاشة مستقلّة ولا انتقال =====
  it('يُحرَّر الحقلان داخل الصفّ نفسه ويُرسلان معًا حالةً نهائيّة إلى عقد إدارة الموظّف القائم', async () => {
    await renderPage();
    await userEvent.click(within(rowOf(ONGOING.fullName)).getByRole('button', { name: 'تعديل نافذة الخدمة' }));

    // المحرّر انفتح في الصفحة ذاتها — لا تنقّل ولا مسار جديد.
    expect(await screen.findByText(`نافذة الخدمة — ${ONGOING.fullName}`)).toBeInTheDocument();
    // وتحذير عدم إعادة كتابة التاريخ معروض قبل أيّ تعديل.
    expect(screen.getByText(/لا يُعدّل أيّ تقييم سابق/)).toBeInTheDocument();

    const exit = screen.getByLabelText('تاريخ انتهاء الخدمة');
    await userEvent.type(exit, '2026-06-30');
    await userEvent.click(screen.getByRole('button', { name: 'حفظ نافذة الخدمة' }));

    await waitFor(() => expect(patchCalls).toHaveLength(1));
    expect(patchCalls[0].url).toBe(`/directory/users/${ONGOING.id}/employment-window`);
    // الحقلان معًا: تاريخ الالتحاق القائم لم يُفقَد بإرسال جزئيّ.
    expect(patchCalls[0].body).toEqual({ hireDate: '2026-01-04', exitDate: '2026-06-30' });
  });

  // ===== 3: لا تحرير غير مصرَّح به على السطح نفسه =====
  it('لا يظهر زرّ تعديل نافذة الخدمة لمن لا يملك صلاحيّة إدارة بيانات الموظّف', async () => {
    roles = ['GeneralManager']; // يقرأ الدليل ولا يملك UserBasicManagement
    await renderPage();
    expect(screen.queryByRole('button', { name: 'تعديل نافذة الخدمة' })).not.toBeInTheDocument();
  });
});

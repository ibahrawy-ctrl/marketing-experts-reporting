// ======================================================================
// P123-R2 — الدليل يجب أن يكون **طريقًا** لا لوحة عرض.
//
// الادّعاء المركزيّ ليس «تظهر أسماء»، بل: من الاسم إلى الملفّ **بنقرة واحدة وبلا معرّف مكتوب**.
// لذا يُقاس هنا وجود رابط حقيقيّ إلى `/app/employee/{id}` لا مجرّد نصّ، ويُقاس أنّ الصفحة
// لا تعرض حقل معرّف إطلاقًا — فذلك كان بديل المستخدم الوحيد قبل هذا السطح.
//
// ويُفصَل الفراغان عمدًا: «لا أحد في نطاقك» حقيقة عن الصلاحيّة، و«لا مطابق لبحثك» حقيقة عن نصّ
// كتبه المستخدم. خلطهما كان سيُخبر مديرًا له فريق أنّ نطاقه فارغ لمجرّد خطأ إملائيّ.
// ======================================================================

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { api } from '../lib/api';
import EmployeeDirectoryPage from './EmployeeDirectoryPage';

const SARA = '11111111-1111-1111-1111-111111111111';
const KHALID = '22222222-2222-2222-2222-222222222222';

const users = [
  { id: SARA, fullName: 'سارة العتيبي', email: 'sara@x.local', isActive: true, roles: ['Employee'], departmentId: 'd1', teamId: 't1', managerId: null, jobRoleId: 'j1' },
  { id: KHALID, fullName: 'خالد الشمري', email: 'khalid@x.local', isActive: true, roles: ['Employee'], departmentId: 'd1', teamId: 't1', managerId: null, jobRoleId: null },
];

function httpError(status: number): AxiosError {
  const err = new AxiosError('denied');
  err.response = { status, statusText: '', data: {}, headers: {}, config: { headers: new AxiosHeaders() } };
  return err;
}

/** يردّ قوائم المرجع دائمًا، ويترك ردّ `/directory/users` لكلّ اختبار. */
function mockApi(usersResponse: () => Promise<unknown>) {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/directory/users') return usersResponse() as never;
    if (url === '/directory/departments') return Promise.resolve({ data: [{ id: 'd1', nameAr: 'التسويق' }] }) as never;
    if (url === '/directory/teams') return Promise.resolve({ data: [{ id: 't1', nameAr: 'فريق المحتوى' }] }) as never;
    if (url.startsWith('/directory/job-roles')) return Promise.resolve({ data: [{ id: 'j1', nameAr: 'أخصّائي محتوى' }] }) as never;
    return Promise.resolve({ data: [] }) as never;
  });
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <EmployeeDirectoryPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('P123-R2 — من الاسم إلى الملفّ بنقرة واحدة', () => {
  it('كلّ صفّ يحمل رابطًا حقيقيًّا إلى ملفّ صاحبه', async () => {
    mockApi(() => Promise.resolve({ data: users }));
    renderPage();

    const nameLink = await screen.findByRole('link', { name: 'سارة العتيبي' });
    expect(nameLink).toHaveAttribute('href', `/app/employee/${SARA}`);

    const openLinks = screen.getAllByRole('link', { name: 'فتح الملفّ' });
    expect(openLinks.map((a) => a.getAttribute('href'))).toEqual([
      `/app/employee/${SARA}`,
      `/app/employee/${KHALID}`,
    ]);
  });

  it('لا يُطالَب المستخدم بمعرّف: لا حقل معرّف ولا معرّف معروض نصًّا', async () => {
    mockApi(() => Promise.resolve({ data: users }));
    renderPage();

    await screen.findByRole('link', { name: 'سارة العتيبي' });
    expect(screen.queryByText(SARA)).toBeNull();
    // الحقل الوحيد في الصفحة هو البحث بالاسم/البريد.
    const inputs = screen.getAllByRole('textbox');
    expect(inputs).toHaveLength(1);
    expect(inputs[0]).toHaveAccessibleName('البحث في دليل الموظّفين');
  });

  it('البحث يُضيّق القائمة ويُبقي الرابط عاملًا', async () => {
    mockApi(() => Promise.resolve({ data: users }));
    renderPage();
    await screen.findByRole('link', { name: 'سارة العتيبي' });

    await userEvent.type(screen.getByRole('textbox'), 'خالد');

    await waitFor(() => expect(screen.queryByRole('link', { name: 'سارة العتيبي' })).toBeNull());
    expect(screen.getByRole('link', { name: 'خالد الشمري' })).toHaveAttribute(
      'href',
      `/app/employee/${KHALID}`,
    );
  });
});

describe('P123-R2 — الفراغان لا يُخلطان', () => {
  it('نطاق فارغ فعلًا: رسالة عن الصلاحيّة لا عن البحث', async () => {
    mockApi(() => Promise.resolve({ data: [] }));
    renderPage();

    expect(await screen.findByText('لا يوجد موظّفون ضمن نطاقك')).toBeInTheDocument();
    expect(screen.queryByText('لا يوجد موظّف مطابق لبحثك.')).toBeNull();
    // ولا يُعرض حقل بحث على قائمة لا يمكن تضييقها أصلًا.
    expect(screen.queryByRole('textbox')).toBeNull();
  });

  it('بحث بلا مطابق: رسالة عن البحث لا عن الصلاحيّة', async () => {
    mockApi(() => Promise.resolve({ data: users }));
    renderPage();
    await screen.findByRole('link', { name: 'سارة العتيبي' });

    await userEvent.type(screen.getByRole('textbox'), 'لا أحد بهذا الاسم');

    expect(await screen.findByText('لا يوجد موظّف مطابق لبحثك.')).toBeInTheDocument();
    expect(screen.queryByText('لا يوجد موظّفون ضمن نطاقك')).toBeNull();
  });
});

describe('P123-R2 — المنع الدائم يُفصَل عن العطل المؤقّت', () => {
  it('403 يعطي منعًا بلا إعادة محاولة', async () => {
    mockApi(() => Promise.reject(httpError(403)));
    renderPage();

    expect(await screen.findByText('لا يمكن عرض دليل الموظّفين')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).toBeNull();
  });

  it('500 وحده يبقى عطلًا قابلًا لإعادة المحاولة', async () => {
    mockApi(() => Promise.reject(httpError(500)));
    renderPage();

    expect(await screen.findByText('تعذّر تحميل دليل الموظّفين')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
  });
});

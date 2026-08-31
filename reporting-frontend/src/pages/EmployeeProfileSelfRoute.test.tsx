// ======================================================================
// اختبار انحدار لعيب وضع الذات (اكتُشِف في CS9 عبر E2E على البناء الحقيقيّ):
//
// المقطع الثابت `/app/employee/me` يفوز على `/app/employee/:userId` في مطابقة المسارات،
// فلا يوجد باراميتر في تلك المطابقة إطلاقًا ⇒ `useParams().userId` فارغ لا `'me'`.
// وباشتقاق وضع الذات من الباراميتر وحده كانت الصفحة تسقط في العرض الإداريّ، فتنادي
// `/dashboard/employee-profile/` بمعرّف فارغ وتعرض «لا يمكن عرض هذا الملف» لصاحب الملفّ نفسه.
//
// الادّعاء هنا مزدوج عمدًا: **يُرسَم سطح الذات**، و**لا يُنادى المسار الإداريّ أصلًا** —
// فالثاني هو ما يمنع عودة العيب بصمت لو أُعيد الاشتقاق من الباراميتر.
// ======================================================================

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { FEATURES } from '../lib/navConfig';
import EmployeeProfilePage from './EmployeeProfilePage';

// P123-R1 — الميزة مفتوحة عمدًا: الادّعاء هنا عن مطابقة المسار وحلّ معرّف الذات خادميًّا،
// وبوّابة الميزة بُعد مستقلّ يُقاس في مكانه.
vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({ features: new Set<string>(Object.values(FEATURES)) }),
}));

const SUBJECT = '33333333-3333-3333-3333-333333333333';

let getCalls: string[] = [];

const profile360 = {
  subjectUserId: SUBJECT,
  isSelf: true,
  viewerRelation: 'Self',
  periodKey: null,
  sections: {
    identity: {
      key: 'identity',
      titleAr: 'الهويّة وحالة التوظيف',
      status: 'Ready',
      dataQuality: 'Complete',
      lastUpdatedAtUtc: null,
      summary: {
        userId: SUBJECT, fullName: 'سارة العتيبي', email: null, jobRoleName: null,
        teamName: null, departmentName: null, directManagerName: null,
        isActive: true, joinedAtUtc: '2025-01-05T00:00:00Z',
      },
      items: [],
      reason: null,
    },
  },
};

const emptyChecklist = {
  subjectUserId: SUBJECT,
  isSelf: true,
  viewerRelation: 'Self',
  summary: { applicable: 0, completed: 0, open: 0, notApplicable: 0, requiresMyAction: 0, completionRatio: 0 },
  items: [],
};

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    getCalls.push(url);
    if (url.endsWith('/checklist')) return Promise.resolve({ data: emptyChecklist } as never);
    // المسار الإداريّ يُرفَض هنا عمدًا: الادّعاء أنّه **لا يُنادى** في وضع الذات، لا أنّه ينجح.
    if (url.startsWith('/dashboard/employee-profile')) return Promise.reject(new Error('404'));
    return Promise.resolve({ data: profile360 } as never);
  });
});

/** يحاكي تعريف المسارَين كما هما في `App.tsx` — بما فيه أسبقيّة المقطع الثابت. */
function renderAt(path: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter initialEntries={[path]}>
      <QueryClientProvider client={qc}>
        <Routes>
          <Route path="/app/employee/me" element={<EmployeeProfilePage selfRoute />} />
          <Route path="/app/employee/:userId" element={<EmployeeProfilePage />} />
        </Routes>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('/app/employee/me — وضع الذات', () => {
  it('يرسم سطح الذات لا شاشة المنع', async () => {
    renderAt('/app/employee/me');
    expect(await screen.findByRole('heading', { name: 'ملفّي' })).toBeInTheDocument();
    expect(screen.queryByText('لا يمكن عرض هذا الملف')).toBeNull();
  });

  it('لا ينادي المسار الإداريّ إطلاقًا، وينادي مسار me الخادميّ', async () => {
    renderAt('/app/employee/me');
    await screen.findByRole('heading', { name: 'الهويّة وحالة التوظيف' });

    expect(getCalls.some((u) => u.startsWith('/dashboard/employee-profile'))).toBe(false);
    expect(getCalls).toContain('/employees/me/profile-360');
    // ولا يُشتقّ معرّف في المتصفّح: لا نداء بمعرّف صريح.
    expect(getCalls.some((u) => u.includes(SUBJECT))).toBe(false);
  });

  it('المسار على المعرّف يبقى كما هو ولا يستبدله وضع الذات', async () => {
    renderAt(`/app/employee/${SUBJECT}`);
    expect(await screen.findByText('تعذّر تحميل الملف')).toBeInTheDocument();
    expect(getCalls).toContain(`/dashboard/employee-profile/${SUBJECT}`);
    expect(screen.queryByRole('heading', { name: 'ملفّي' })).toBeNull();
  });
});

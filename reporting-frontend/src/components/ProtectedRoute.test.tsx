import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Role } from '../types/api';

// ===== P3-SEC-005 — الوصول المباشر بالرابط: الحارس لا القائمة =====
// إخفاء عنصر من القائمة ليس منعًا؛ من يكتب المسار في شريط العنوان يتجاوز القائمة كلّها.
// هنا يُقاس ما يحدث عند الدخول المباشر: بلا جلسة ⇒ صفحة الدخول، وبدور غير مسموح ⇒ إعادة
// إلى الرئيسيّة لا عرضٌ للمحتوى. والحراسة النهائيّة تبقى خادميّة على كلّ حال — هذه طبقة عرض.

const authState: { user: unknown; loading: boolean; roles: Role[] } = {
  user: { userId: 'u1' },
  loading: false,
  roles: ['Employee'],
};

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: authState.user,
    loading: authState.loading,
    hasAnyRole: (...r: Role[]) => r.some((x) => authState.roles.includes(x)),
  }),
}));

import { ProtectedRoute } from './ProtectedRoute';

/// نصيّر على مسار عميق كأنّ المستخدم لصقه في شريط العنوان.
function renderAt(deepLink: string, roles?: Role[]) {
  return render(
    <MemoryRouter initialEntries={[deepLink]}>
      <Routes>
        <Route path="/login" element={<div>صفحة الدخول</div>} />
        <Route path="/app" element={<div>الرئيسيّة</div>} />
        <Route
          path={deepLink}
          element={
            <ProtectedRoute roles={roles}>
              <div>محتوى محميّ</div>
            </ProtectedRoute>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  authState.user = { userId: 'u1' };
  authState.loading = false;
  authState.roles = ['Employee'];
});

describe('الوصول المباشر بالرابط', () => {
  it('بلا جلسة: المسار العميق يقود إلى صفحة الدخول لا إلى المحتوى', () => {
    authState.user = null;
    renderAt('/app/hr-operations');
    expect(screen.getByText('صفحة الدخول')).toBeInTheDocument();
    expect(screen.queryByText('محتوى محميّ')).toBeNull();
  });

  it('بدور غير مسموح: إعادة إلى الرئيسيّة بلا تسريب المحتوى', () => {
    authState.roles = ['Employee'];
    renderAt('/app/governance', ['Admin', 'CEO']);
    expect(screen.getByText('الرئيسيّة')).toBeInTheDocument();
    expect(screen.queryByText('محتوى محميّ')).toBeNull();
  });

  it('بالدور المسموح: المحتوى يُعرَض', () => {
    authState.roles = ['Admin'];
    renderAt('/app/governance', ['Admin', 'CEO']);
    expect(screen.getByText('محتوى محميّ')).toBeInTheDocument();
  });

  it('أثناء تحميل الجلسة لا يُعرَض المحتوى ولا يُحوَّل قبل معرفة الحقيقة', () => {
    // التحويل المبكّر كان سيطرد صاحب الجلسة الصحيحة إلى صفحة الدخول عند كلّ إعادة تحميل.
    authState.loading = true;
    authState.user = null;
    renderAt('/app/hr-operations');
    expect(screen.queryByText('محتوى محميّ')).toBeNull();
    expect(screen.queryByText('صفحة الدخول')).toBeNull();
  });
});

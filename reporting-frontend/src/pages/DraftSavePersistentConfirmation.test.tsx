// VIS-04 — تأكيد حفظ المسودّة أثرٌ باقٍ لا ومضة عابرة.
//
// **السبب الجذريّ**: التأكيد الوحيد كان `toast.success` — يظهر ثوانيَ ثمّ يختفي. الموظّف
// الذي يحفظ ثمّ ينتقل ليتحقّق من شيء آخر يعود فلا يجد على الشاشة ما يقول إنّ الحفظ وقع
// أصلًا، فيحفظ مرّة أخرى «للاطمئنان» أو يظنّ أنّ الزرّ لم يعمل. لقطات 1 سبتمبر لم تلتقط
// أيّ سطح يثبت الحفظ لأنّ الـToast كان قد اختفى وقت اللقطة.
//
// **الإصلاح المُختبَر هنا**: مؤشّر ثابت بجوار زرّ الحفظ يحمل وقت آخر حفظ ناجح، ولا يظهر
// إلّا بعد نجاح فعليّ. الادّعاء السالب (لا يظهر عند الفشل) هو نصف المواصفة الأهمّ:
// مؤشّر يظهر بلا حفظ أسوأ من لا مؤشّر إطلاقًا.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { ToastProvider } from '../components/ActionResultToast';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import { SubmissionDetail } from './SubmissionsPage';

const SUB_ID = '33333333-3333-3333-3333-333333333333';
const EMPLOYEE_ID = 'emp-1';

const me = {
  userId: EMPLOYEE_ID,
  fullName: 'موظّف المحتوى',
  email: 'employee@test.local',
  roles: ['Employee'],
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
};

// مسودّة قابلة للتحرير: `canEdit: true` هو شرط ظهور شريط الإجراءات أصلًا.
function draftSubmission() {
  return {
    id: SUB_ID,
    reportTemplateVersionId: 'ver-1',
    templateTitle: 'تقرير أسبوعيّ',
    submitterId: EMPLOYEE_ID,
    submitterName: 'موظّف المحتوى',
    teamId: null,
    departmentId: null,
    periodType: 'Weekly',
    periodKey: '2026-W36',
    status: 'Draft',
    submittedAtUtc: null,
    closedAtUtc: null,
    currentApproverId: null,
    canEdit: true,
    fieldValues: [],
    approvalSteps: [],
    clientId: null,
    clientName: null,
    projectId: null,
    projectName: null,
  };
}

function renderDetail() {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === `/submissions/${SUB_ID}`)
      return Promise.resolve({ data: draftSubmission() } as never);
    return Promise.resolve({ data: [] } as never);
  });
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter>
            <SubmissionDetail id={SUB_ID} onBack={() => {}} />
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  tokenStore.clear();
  tokenStore.set('acc', 'ref');
  vi.restoreAllMocks();
});

describe('VIS-04 — تأكيد حفظ المسودّة الباقي', () => {
  it('لا يعرض المؤشّر قبل أيّ حفظ', async () => {
    vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never);
    renderDetail();
    await screen.findByRole('button', { name: 'حفظ' });
    expect(screen.queryByTestId('draft-saved-indicator')).not.toBeInTheDocument();
  });

  it('يعرض المؤشّر بعد حفظ ناجح ويبقى ظاهرًا', async () => {
    const user = userEvent.setup();
    const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'حفظ' }));

    await waitFor(() => expect(put).toHaveBeenCalledWith(`/submissions/${SUB_ID}/values`, { values: [] }));
    const indicator = await screen.findByTestId('draft-saved-indicator');
    expect(indicator.textContent).toContain('حُفظت المسودّة');

    // «باقٍ» ادّعاء زمنيّ: بعد مرور ما يكفي لاختفاء أيّ Toast، المؤشّر ما زال في الشجرة.
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.getByTestId('draft-saved-indicator')).toBeInTheDocument();
  });

  // النصف الحاسم من المواصفة: الفشل لا يترك أثرًا يوحي بالنجاح.
  it('لا يعرض المؤشّر إذا فشل الحفظ', async () => {
    const user = userEvent.setup();
    vi.spyOn(api, 'put').mockRejectedValue(
      Object.assign(new Error('HTTP 500'), {
        isAxiosError: true,
        response: { status: 500, data: { title: 'خطأ خادم' } },
      }),
    );
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'حفظ' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'حفظ' })).not.toBeDisabled());
    expect(screen.queryByTestId('draft-saved-indicator')).not.toBeInTheDocument();
  });
});

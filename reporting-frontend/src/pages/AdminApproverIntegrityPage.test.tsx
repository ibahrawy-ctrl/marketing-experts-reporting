// RPT-APPROVER-INTEGRITY-01 — اختبارات سطح الإنقاذ الإداريّ للاعتمادات العالقة.
// تغطّي حالات السطح الأربع (تحميل · فارغ · خطأ · نجاح) وتثبت إلزاميّة الخطوتين:
// «تطبيق الإصلاح» يبقى معطَّلًا حتّى تُنتَج خطّة (dryRun) فيها عنصر واحد قابل لإعادة التوجيه على الأقلّ،
// وأنّ التخطيط لا يستدعي إلّا `dryRun: true` والتطبيق `dryRun: false`.
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import AdminApproverIntegrityPage from './AdminApproverIntegrityPage';
import type { ApproverIntegrityReportDto, ApproverRepairReportDto } from '../types/api';

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AdminApproverIntegrityPage />
    </QueryClientProvider>,
  );
}

const emptyReport: ApproverIntegrityReportDto = {
  items: [],
  totalCount: 0,
  nullApproverCount: 0,
  inactiveApproverCount: 0,
  orphanApproverCount: 0,
};

const oneIssueReport: ApproverIntegrityReportDto = {
  items: [
    {
      submissionId: 'sub-1',
      kind: 'InactiveApprover',
      submitterId: 'emp-1',
      submitterName: 'موظّف تجريبيّ',
      submitterIsActive: true,
      teamId: 'team-1',
      teamName: 'فريق التجربة',
      periodType: 'Weekly',
      periodKey: '2026-W36',
      status: 'Submitted',
      currentApproverId: 'old-1',
      currentApproverName: 'المعتمِد القديم',
      submittedAtUtc: '2026-09-01T08:00:00Z',
      stalledDays: 7,
      suggestedApproverId: 'new-1',
      suggestedApproverName: 'معتمِد بديل نشط',
    },
  ],
  totalCount: 1,
  nullApproverCount: 0,
  inactiveApproverCount: 1,
  orphanApproverCount: 0,
};

const plan: ApproverRepairReportDto = {
  dryRun: true,
  items: [{ submissionId: 'sub-1', fromApproverId: 'old-1', toApproverId: 'new-1', rerouted: true }],
  reroutedCount: 1,
  failedCount: 0,
};

const appliedPlan: ApproverRepairReportDto = { ...plan, dryRun: false };

const emptyPlan: ApproverRepairReportDto = { dryRun: true, items: [], reroutedCount: 0, failedCount: 0 };

// خطأ بشكل axios حقيقيّ: الخادم يضع الرسالة العربيّة في ProblemDetails.detail،
// و`apiErrorMessage` تعرضها كما هي بدل النصّ الاحتياطيّ.
function axiosError(detail: string, status = 500) {
  return { isAxiosError: true, message: 'Request failed', response: { status, data: { detail } } };
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('AdminApproverIntegrityPage — حالات السطح', () => {
  it('1) حالة التحميل: تظهر ريثما لم يصل الردّ', async () => {
    vi.spyOn(api, 'get').mockReturnValue(new Promise(() => {}) as never);
    const { container } = renderPage();
    expect(container.querySelector('.animate-spin')).not.toBeNull();
  });

  it('2) حالة الفراغ: لا اعتمادات عالقة ⇒ رسالة صريحة ولا جدول', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: emptyReport } as never);
    renderPage();
    expect(await screen.findByText('لا اعتمادات عالقة')).toBeTruthy();
    expect(screen.queryByText('الإصلاح الإداريّ')).toBeNull();
  });

  it('3) حالة الخطأ: فشل التحميل ⇒ تنبيه برسالة الخادم بلا انهيار', async () => {
    vi.spyOn(api, 'get').mockRejectedValue(axiosError('غير مصرَّح لك بهذا السطح.', 403) as never);
    renderPage();
    expect(await screen.findByText('غير مصرَّح لك بهذا السطح.')).toBeTruthy();
    expect(screen.queryByText('لا اعتمادات عالقة')).toBeNull();
  });

  it('4) حالة النجاح: تعرض العدّادات وصفّ الحالة العالقة', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: oneIssueReport } as never);
    renderPage();
    expect(await screen.findByText('معتمِد بديل نشط')).toBeTruthy();
    expect(screen.getByText('إجمالي الحالات العالقة')).toBeTruthy();
    expect(screen.getByText('2026-W36')).toBeTruthy();
    expect(screen.getByText('المعتمِد القديم')).toBeTruthy();
    // «معتمِد معطَّل» يظهر مرّتين: بطاقة العدّاد وشارة نوع الخلل.
    expect(screen.getAllByText('معتمِد معطَّل')).toHaveLength(2);
  });
});

describe('AdminApproverIntegrityPage — إلزاميّة الخطوتين (تخطيط ثمّ تطبيق)', () => {
  it('5) «تطبيق الإصلاح» معطَّل قبل التخطيط', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: oneIssueReport } as never);
    renderPage();
    const apply = await screen.findByRole('button', { name: 'تطبيق الإصلاح' });
    expect((apply as HTMLButtonElement).disabled).toBe(true);
  });

  it('6) خطّة بلا عناصر قابلة لإعادة التوجيه ⇒ يبقى التطبيق معطَّلًا', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: oneIssueReport } as never);
    vi.spyOn(api, 'post').mockResolvedValue({ data: emptyPlan } as never);
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'تخطيط بلا كتابة' }));
    await waitFor(() => expect(screen.getByText(/خطّة مقترَحة بلا كتابة/)).toBeTruthy());
    expect((screen.getByRole('button', { name: 'تطبيق الإصلاح' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('7) تخطيط ثمّ تطبيق: dryRun=true ثمّ dryRun=false، ورسالة نجاح', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: oneIssueReport } as never);
    const post = vi
      .spyOn(api, 'post')
      .mockResolvedValueOnce({ data: plan } as never)
      .mockResolvedValueOnce({ data: appliedPlan } as never);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'تخطيط بلا كتابة' }));
    await waitFor(() => expect(screen.getByText(/خطّة مقترَحة بلا كتابة/)).toBeTruthy());
    expect(post).toHaveBeenCalledWith('/submissions/approver-integrity/repair', { dryRun: true });

    const apply = screen.getByRole('button', { name: 'تطبيق الإصلاح' }) as HTMLButtonElement;
    expect(apply.disabled).toBe(false);
    await userEvent.click(apply);

    await waitFor(() => expect(screen.getByText(/تمّ الإصلاح/)).toBeTruthy());
    expect(post).toHaveBeenNthCalledWith(2, '/submissions/approver-integrity/repair', { dryRun: false });
    expect(screen.queryByText(/خطّة مقترَحة بلا كتابة/)).toBeNull();
  });

  it('8) فشل التطبيق ⇒ رسالة خطأ بلا ادّعاء نجاح', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: oneIssueReport } as never);
    vi.spyOn(api, 'post')
      .mockResolvedValueOnce({ data: plan } as never)
      .mockRejectedValueOnce(axiosError('تعذّر إيجاد معتمِد بديل نشط.', 409) as never);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'تخطيط بلا كتابة' }));
    await waitFor(() => expect(screen.getByText(/خطّة مقترَحة بلا كتابة/)).toBeTruthy());
    await userEvent.click(screen.getByRole('button', { name: 'تطبيق الإصلاح' }));

    await waitFor(() => expect(screen.getByText('تعذّر إيجاد معتمِد بديل نشط.')).toBeTruthy());
    expect(screen.queryByText(/تمّ الإصلاح/)).toBeNull();
  });
});

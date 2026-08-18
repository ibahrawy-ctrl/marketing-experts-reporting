import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';

// ===== P360-WF-R2 §4 — الإسناد القائم خارج نطاق دليل المحرِّر =====
// `/directory/users` مُنطَّق بالصلاحيّة: على TEST يرى المديرُ 4 مستخدمين ويرى المدير العامّ 17.
// فإن كان مالك المشروع أو قائد الفريق أو مدير العميل خارج ذلك النطاق، لم يكن للقيمة القائمة
// خيارٌ مقابلٌ في المنتقي، فيعرض «— بدون —» على إسنادٍ **موجود فعلًا** — نموذج يكذب على قارئه
// في الشاشة نفسها التي تُحدَّد بها المسؤوليّة. القياس هنا على **النصّ المعروض** لا على الحمولة.

const updateMutateAsync = vi.fn().mockResolvedValue({});

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'p1' }),
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    canManageClients: true,
    canManageProjectStructure: true,
    user: { userId: 'mgr', roles: ['Manager'] },
    hasAnyRole: () => true,
  }),
}));

vi.mock('../lib/useClients', () => ({
  useProject: () => ({
    data: {
      id: 'p1', clientId: 'c1', clientName: 'عميل تجريبي', name: 'مشروع الموقع',
      serviceType: 'Website', status: 'Active', ownerTeamId: null, ownerTeamName: null,
      accountManagerId: 'u-out-am', accountManagerName: 'مدير حساب خارج نطاق المدير',
      startDate: null, endDate: null, createdAtUtc: '2026-07-01T00:00:00Z', notes: null,
      projectOwnerId: 'u-out-owner', projectOwnerName: 'مالك مشروع خارج نطاق المدير',
      teamLeaderId: 'u-in-leader', teamLeaderName: 'قائد الفريق المرشَّح',
    },
    isLoading: false, isError: false, refetch: vi.fn(),
  }),
  useProjectSummary: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectReports: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateProject: () => ({ mutateAsync: updateMutateAsync, isPending: false }),
  useArchiveProject: () => ({ mutate: vi.fn(), isPending: false }),
}));

// دليل ضيّق عمدًا: لا يحوي مالك المشروع ولا مدير العميل المُسنَدَين.
vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({ data: [{ id: 'u-in-leader', fullName: 'قائد الفريق المرشَّح' }] }),
  useTeams: () => ({ data: [{ id: 't1', nameAr: 'فريق التطوير' }] }),
}));

vi.mock('../lib/useExecutionTaxonomy', () => ({ useTaxonomyOptionDetails: () => ({ data: [] }) }));

vi.mock('../lib/useProjectWorkstreams', () => ({
  useProjectWorkstreams: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
  useCreateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useActivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
  useDeactivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('./ClientDetailPage', () => ({ LinkedReportsCard: () => <div data-testid="linked-reports" /> }));

import ProjectDetailPage from './ProjectDetailPage';

function openForm() {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تعديل المشروع'));
}

it('يعرض المُسنَد القائم ولو كان خارج دليل المحرِّر، فلا يُقرأ الإسناد على أنّه «بدون»', () => {
  openForm();
  const am = screen.getByLabelText('مدير العميل') as HTMLSelectElement;
  const owner = screen.getByLabelText('مالك المشروع') as HTMLSelectElement;
  const leader = screen.getByLabelText('قائد الفريق') as HTMLSelectElement;

  expect(am.value).toBe('u-out-am');
  expect(am.options[am.selectedIndex].textContent).toBe('مدير حساب خارج نطاق المدير');
  expect(owner.value).toBe('u-out-owner');
  expect(owner.options[owner.selectedIndex].textContent).toBe('مالك مشروع خارج نطاق المدير');
  // ومَن هو داخل الدليل لا يُكرَّر خيارًا.
  expect([...leader.options].filter((o) => o.value === 'u-in-leader')).toHaveLength(1);
});

it('يحفظ الإسناد القائم كما هو حين لا يمسّه المحرِّر', async () => {
  openForm();
  fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
  await waitFor(() => expect(updateMutateAsync).toHaveBeenCalledTimes(1));
  expect(updateMutateAsync.mock.calls[0][0].req).toMatchObject({
    accountManagerId: 'u-out-am',
    projectOwnerId: 'u-out-owner',
    teamLeaderId: 'u-in-leader',
  });
});

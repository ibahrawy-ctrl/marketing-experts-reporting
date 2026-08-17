import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';

// ===== P360-WF-R2 §4 — إسناد أدوار المشروع من الواجهة =====
// الخادم يقبل `ProjectOwnerId`/`TeamLeaderId` منذ GAP-01، لكنّ الحقلين كانا غائبَين عن
// نوع الطلب في الواجهة، فكانت الشاشة **تعرضهما ولا تُسنِدهما أبدًا**: نجاح الـAPI لا يعني
// نجاح الواجهة. هذا الملفّ يقيس الحمولة الفعليّة الخارجة من نموذج التعديل لا وجود الحقل.

const updateMutateAsync = vi.fn().mockResolvedValue({});

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'p1' }),
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    canManageClients: true,
    user: { userId: 'admin', roles: ['Admin'] },
    hasAnyRole: () => true,
  }),
}));

vi.mock('../lib/useClients', () => ({
  useProject: () => ({
    data: {
      id: 'p1', clientId: 'c1', clientName: 'عميل تجريبي', name: 'مشروع الموقع',
      serviceType: 'Website', status: 'Active', ownerTeamId: null, ownerTeamName: null,
      accountManagerId: null, accountManagerName: null, startDate: null, endDate: null,
      createdAtUtc: '2026-07-01T00:00:00Z', notes: null,
      projectOwnerId: null, projectOwnerName: null, teamLeaderId: null, teamLeaderName: null,
    },
    isLoading: false, isError: false, refetch: vi.fn(),
  }),
  useProjectSummary: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectReports: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateProject: () => ({ mutateAsync: updateMutateAsync, isPending: false }),
  useArchiveProject: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({
    data: [
      { id: 'u-owner', fullName: 'مالك المشروع المرشَّح' },
      { id: 'u-leader', fullName: 'قائد الفريق المرشَّح' },
    ],
  }),
  useTeams: () => ({ data: [{ id: 't1', nameAr: 'فريق التطوير' }] }),
}));

vi.mock('../lib/useExecutionTaxonomy', () => ({
  useTaxonomyOptionDetails: () => ({ data: [] }),
}));

vi.mock('../lib/useProjectWorkstreams', () => ({
  useProjectWorkstreams: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
  useCreateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useActivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
  useDeactivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('./ClientDetailPage', () => ({
  LinkedReportsCard: () => <div data-testid="linked-reports" />,
}));

import ProjectDetailPage from './ProjectDetailPage';

beforeEach(() => {
  updateMutateAsync.mockClear();
});

it('يعرض منتقيَي مالك المشروع وقائد الفريق في نموذج التعديل', () => {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تعديل المشروع'));
  expect(screen.getByText('مالك المشروع')).toBeInTheDocument();
  expect(screen.getByText('قائد الفريق')).toBeInTheDocument();
});

it('يُرسل مالك المشروع وقائد الفريق ضمن حمولة التعديل', async () => {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تعديل المشروع'));

  fireEvent.change(screen.getByLabelText('مالك المشروع'), { target: { value: 'u-owner' } });
  fireEvent.change(screen.getByLabelText('قائد الفريق'), { target: { value: 'u-leader' } });
  fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));

  await waitFor(() => expect(updateMutateAsync).toHaveBeenCalledTimes(1));
  expect(updateMutateAsync.mock.calls[0][0].req).toMatchObject({
    projectOwnerId: 'u-owner',
    teamLeaderId: 'u-leader',
  });
});

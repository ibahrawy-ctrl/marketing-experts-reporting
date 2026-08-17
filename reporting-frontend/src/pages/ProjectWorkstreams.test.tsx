import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type { ProjectWorkstreamDto } from '../types/api';

// ===== P1 — تيّارات العمل داخل المشروع (Project Workstreams) =====
// تُعزل صفحة تفاصيل المشروع عن الشبكة بتمويه كل هوكاتها. تُثبِت: عرض قسم تيّارات العمل،
// القائمة، نموذج الإضافة، نموذج التعديل، التفعيل/التعطيل، منتقي النوع من الكتالوج،
// منتقي الفريق، وحالات فارغ/تحميل/خطأ.

type QueryLike<T> = { data: T | undefined; isLoading: boolean; isError: boolean; refetch: () => void };

const wsState: QueryLike<ProjectWorkstreamDto[]> = { data: [], isLoading: false, isError: false, refetch: vi.fn() };

const createMutate = vi.fn().mockResolvedValue({});
const updateMutate = vi.fn().mockResolvedValue({});
const activateMutate = vi.fn();
const deactivateMutate = vi.fn();

let canManage = true;
// حين يكون غير null يُغلِّب قدرة الخادم على علَم الدور — به وحده يُختبَر أنّ الشاشة تتبع الخادم.
let serverCanOperate: boolean | null = null;

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'p1' }),
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    canManageClients: canManage,
    user: { userId: 'admin', roles: ['Admin'] },
    hasAnyRole: () => true,
  }),
}));

vi.mock('../lib/useClients', () => ({
  useProject: () => ({
    data: {
      id: 'p1', clientId: 'c1', clientName: 'عميل تجريبي', name: 'مشروع الموقع',
      serviceType: 'Website', status: 'Active', ownerTeamName: null, accountManagerName: null,
      startDate: null, endDate: null, createdAtUtc: '2026-07-01T00:00:00Z', notes: null,
      // **القدرة تأتي من الخادم لا من الدور (R2.1)**: الشاشة تقرأ `canOperate` من ProjectDto،
      // ولو بقي هذا المُثبَّت يحاكي الدور وحده لاختبرنا سلوكًا لم يعد موجودًا.
      canOperate: serverCanOperate ?? canManage, canManageStructure: canManage,
    },
    isLoading: false, isError: false, refetch: vi.fn(),
  }),
  useProjectSummary: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectReports: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useArchiveProject: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({ data: [{ id: 'u1', fullName: 'مدير أول' }] }),
  useTeams: () => ({ data: [{ id: 't1', nameAr: 'فريق التطوير' }, { id: 't2', nameAr: 'فريق التصميم' }] }),
}));

vi.mock('../lib/useExecutionTaxonomy', () => ({
  useTaxonomyOptionDetails: () => ({
    data: [
      { code: 'web_development', nameAr: 'تطوير الويب' },
      { code: 'ui_design', nameAr: 'تصميم واجهات المستخدم' },
    ],
  }),
}));

vi.mock('../lib/useProjectWorkstreams', () => ({
  useProjectWorkstreams: () => wsState,
  useCreateProjectWorkstream: () => ({ mutateAsync: createMutate, isPending: false }),
  useUpdateProjectWorkstream: () => ({ mutateAsync: updateMutate, isPending: false }),
  useActivateProjectWorkstream: () => ({ mutate: activateMutate, isPending: false }),
  useDeactivateProjectWorkstream: () => ({ mutate: deactivateMutate, isPending: false }),
}));

vi.mock('./ClientDetailPage', () => ({
  LinkedReportsCard: () => <div data-testid="linked-reports" />,
}));

import ProjectDetailPage from './ProjectDetailPage';

function ws(over: Partial<ProjectWorkstreamDto> = {}): ProjectWorkstreamDto {
  return {
    id: 'w1', projectId: 'p1', workstreamTypeCode: 'web_development', workstreamTypeNameAr: 'تطوير الويب',
    name: 'تطوير الموقع', responsibleTeamId: 't1', responsibleTeamName: 'فريق التطوير',
    responsibleManagerId: null, responsibleManagerName: null, status: 'Active', sortOrder: 0,
    notes: null, isActive: true, createdAtUtc: '2026-07-01T00:00:00Z', updatedAtUtc: null,
    ...over,
  };
}

beforeEach(() => {
  wsState.data = [];
  wsState.isLoading = false;
  wsState.isError = false;
  canManage = true;
  serverCanOperate = null;
  createMutate.mockClear();
  updateMutate.mockClear();
  activateMutate.mockClear();
  deactivateMutate.mockClear();
});

// ---- 1: تعرض قسم تيّارات العمل + زر الإضافة (للإدارة) ----
it('تعرض قسم تيّارات العمل وزر الإضافة للإدارة', () => {
  render(<ProjectDetailPage />);
  expect(screen.getByText('أهداف العمل داخل المشروع')).toBeInTheDocument();
  expect(screen.getByText('إضافة هدف عمل')).toBeInTheDocument();
});

// ---- 2: القائمة تعرض صفوف التيّارات (الاسم/النوع/الفريق) ----
it('تعرض صفوف تيّارات العمل مع الاسم والنوع والفريق', () => {
  wsState.data = [ws({ name: 'تطوير الموقع', workstreamTypeNameAr: 'تطوير الويب', responsibleTeamName: 'فريق التطوير' })];
  render(<ProjectDetailPage />);
  expect(screen.getByText('تطوير الموقع')).toBeInTheDocument();
  expect(screen.getByText('تطوير الويب')).toBeInTheDocument();
  expect(screen.getByText('فريق التطوير')).toBeInTheDocument();
});

// ---- 3: نموذج الإضافة يظهر عند النقر على «إضافة هدف عمل» ----
it('نموذج الإضافة يظهر عند الضغط على إضافة هدف عمل', () => {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('إضافة هدف عمل'));
  expect(screen.getByText('نوع هدف العمل')).toBeInTheDocument();
  // منتقيا النوع والفريق يظهران داخل النموذج.
  expect(screen.getByRole('option', { name: '— اختر النوع —' })).toBeInTheDocument();
  expect(screen.getByRole('option', { name: '— اختر الفريق —' })).toBeInTheDocument();
});

// ---- 4: نموذج التعديل يظهر عند النقر على «تعديل» بحقل النوع مُعطّل ----
it('نموذج التعديل يظهر عند الضغط على تعديل والنوع مُعطّل', () => {
  wsState.data = [ws()];
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تعديل'));
  expect(screen.getByText('تعديل هدف العمل')).toBeInTheDocument();
  // منتقي الحالة يظهر فقط في وضع التعديل (خيار «متوقّف مؤقتًا» حصريّ للنموذج).
  expect(screen.getByRole('option', { name: 'متوقّف مؤقتًا' })).toBeInTheDocument();
  // حقل النوع مُعطّل في وضع التعديل.
  const typeOption = screen.getByRole('option', { name: 'تطوير الويب' });
  expect((typeOption.parentElement as HTMLSelectElement).disabled).toBe(true);
});

// ---- 5: زرّا التعطيل/التفعيل يستدعيان الهوك بمعرّف التيّار ----
it('تعطيل تيّار نشط يستدعي deactivate بمعرّفه', () => {
  wsState.data = [ws({ id: 'w-active', isActive: true })];
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تعطيل'));
  expect(deactivateMutate).toHaveBeenCalledWith('w-active');
});

it('تفعيل تيّار معطّل يستدعي activate بمعرّفه', () => {
  wsState.data = [ws({ id: 'w-inactive', isActive: false, status: 'Paused' })];
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('تفعيل'));
  expect(activateMutate).toHaveBeenCalledWith('w-inactive');
});

// ---- 6: منتقي النوع يستعمل خيارات الكتالوج (workstream_type) ----
it('منتقي النوع في نموذج الإضافة يعرض خيارات الكتالوج', () => {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('إضافة هدف عمل'));
  expect(screen.getByRole('option', { name: 'تطوير الويب' })).toBeInTheDocument();
  expect(screen.getByRole('option', { name: 'تصميم واجهات المستخدم' })).toBeInTheDocument();
});

// ---- 7: منتقي الفريق يظهر بخيارات الفرق ----
it('منتقي الفريق يظهر بخيارات الفرق', () => {
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('إضافة هدف عمل'));
  expect(screen.getByRole('option', { name: 'فريق التطوير' })).toBeInTheDocument();
  expect(screen.getByRole('option', { name: 'فريق التصميم' })).toBeInTheDocument();
});

// ---- 8: حالات فارغ/تحميل/خطأ ----
it('تعرض حالة فارغة حين لا توجد تيّارات', () => {
  wsState.data = [];
  render(<ProjectDetailPage />);
  expect(screen.getByText('لا توجد أهداف عمل لهذا المشروع بعد.')).toBeInTheDocument();
});

it('تعرض حالة التحميل حين isLoading', () => {
  wsState.data = undefined;
  wsState.isLoading = true;
  render(<ProjectDetailPage />);
  expect(screen.getByText('يتم تحميل أهداف العمل…')).toBeInTheDocument();
});

it('تعرض حالة الخطأ حين isError', () => {
  wsState.data = undefined;
  wsState.isError = true;
  render(<ProjectDetailPage />);
  expect(screen.getByText('تعذّر عرض أهداف العمل')).toBeInTheDocument();
});

// ---- 9 (R2.1 · GAP-R21-01): القدرة تُقرأ من الخادم لا من دور المتصفّح ----
// قائد الفريق ليس من أدوار إدارة العملاء، ومع ذلك الخادم يمنحه `canOperate` على مشروعه
// بعينه. قبل الإصلاح كانت الشاشة تشتقّ الصلاحيّة من الدور فتُخفي كلّ أزرار أهداف العمل
// عنه بينما الخادم يقبل كتابته — شاشة تكذّب ردَّ الخادم.
it('قائد الفريق يرى إدارة أهداف العمل حين يمنحه الخادم canOperate رغم أنّ دوره لا يدير العملاء', () => {
  canManage = false;
  serverCanOperate = true;
  wsState.data = [ws({ name: 'تطوير الموقع' })];
  render(<ProjectDetailPage />);
  expect(screen.getByText('إضافة هدف عمل')).toBeInTheDocument();
  expect(screen.getByText('تعديل')).toBeInTheDocument();
});

// ---- 10 (R2.1 · GAP-R21-01): ومنع الطرف المقابل — منع الخادم يُخفي الأزرار ----
it('يُخفي إدارة أهداف العمل حين يمنع الخادم canOperate ولو كان الدور يدير العملاء', () => {
  canManage = true;
  serverCanOperate = false;
  wsState.data = [ws({ name: 'تطوير الموقع' })];
  render(<ProjectDetailPage />);
  expect(screen.queryByText('إضافة هدف عمل')).not.toBeInTheDocument();
  expect(screen.queryByText('تعديل')).not.toBeInTheDocument();
});

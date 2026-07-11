import { render, screen, fireEvent, within } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type { ExecutionTaxonomyDto } from '../types/api';

// ===== RC-4 Task 4D2 — شاشة إدارة كتالوج تصنيفات التنفيذ (الأدمن/CEO/GM) =====
// تُعزل عن الشبكة بتمويه هوكات ../lib/useExecutionTaxonomy. تُثبِت: العنوان/التنبيه/نموذج
// الإنشاء، منتقي المجال (13 مجالًا)، عرض القيم، الإنشاء بالطلب الصحيح، التعديل بحقول Domain/Code
// للقراءة فقط، التعطيل/التفعيل، وحالات فارغ/تحميل/خطأ.

const listState: { data: ExecutionTaxonomyDto[] | undefined; isLoading: boolean; isError: boolean } = {
  data: [],
  isLoading: false,
  isError: false,
};

const createMutate = vi.fn().mockResolvedValue({});
const updateMutate = vi.fn().mockResolvedValue({});
const activateMutate = vi.fn().mockResolvedValue({});
const deactivateMutate = vi.fn().mockResolvedValue({});

vi.mock('../lib/useExecutionTaxonomy', () => ({
  useExecutionTaxonomyAdmin: () => ({
    data: listState.data,
    isLoading: listState.isLoading,
    isError: listState.isError,
    refetch: vi.fn(),
  }),
  useCreateExecutionTaxonomy: () => ({ mutateAsync: createMutate, isPending: false }),
  useUpdateExecutionTaxonomy: () => ({ mutateAsync: updateMutate, isPending: false }),
  useActivateExecutionTaxonomy: () => ({ mutateAsync: activateMutate, isPending: false }),
  useDeactivateExecutionTaxonomy: () => ({ mutateAsync: deactivateMutate, isPending: false }),
}));

import ExecutionTaxonomyManagementPage from './ExecutionTaxonomyManagementPage';

function tax(over: Partial<ExecutionTaxonomyDto> = {}): ExecutionTaxonomyDto {
  return {
    id: 'id-' + (over.code ?? 'x'),
    domain: 'content_type',
    code: 'carousel',
    nameAr: 'كاروسيل',
    nameEn: 'Carousel',
    isActive: true,
    sortOrder: 10,
    createdAtUtc: '2026-07-01T00:00:00Z',
    updatedAtUtc: null,
    ...over,
  };
}

beforeEach(() => {
  listState.data = [];
  listState.isLoading = false;
  listState.isError = false;
  createMutate.mockClear();
  updateMutate.mockClear();
  activateMutate.mockClear();
  deactivateMutate.mockClear();
  vi.spyOn(window, 'confirm').mockReturnValue(true);
});

// ---- 1: تعرض العنوان + تنبيه اللقطة + نموذج الإنشاء ----
it('تعرض العنوان وتنبيه اللقطة ونموذج الإضافة', () => {
  render(<ExecutionTaxonomyManagementPage />);
  expect(screen.getByText('إدارة تصنيفات التنفيذ')).toBeInTheDocument();
  expect(screen.getByText(/تعديل الكتالوج لا يغيّر قوالب التنفيذ/)).toBeInTheDocument();
  expect(screen.getByText('إضافة قيمة جديدة')).toBeInTheDocument();
  expect(screen.getByText('إضافة القيمة')).toBeInTheDocument();
});

// ---- 2: منتقي المجال يعرض 19 مجالًا (13 قوالب التنفيذ + 6 منصّة التنفيذ العامة P0) ----
it('منتقي المجال في نموذج الإنشاء يعرض 19 مجالًا', () => {
  render(<ExecutionTaxonomyManagementPage />);
  const domainSelect = screen.getByLabelText('المجال *') as HTMLSelectElement;
  expect(within(domainSelect).getAllByRole('option')).toHaveLength(19);
  expect(within(domainSelect).getByText('نوع المحتوى')).toBeInTheDocument();
  expect(within(domainSelect).getByText('زمن الاستجابة')).toBeInTheDocument();
  // مجالات P0 الجديدة
  expect(within(domainSelect).getByText('نوع تيار العمل')).toBeInTheDocument();
  expect(within(domainSelect).getByText('المنصّة / القناة')).toBeInTheDocument();
});

// ---- 3: يعرض قيم المجال المختار (الرمز/الاسم/الحالة) ----
it('يعرض صفوف القيم مع الرمز والاسم وشارة الحالة', () => {
  listState.data = [tax({ code: 'carousel', nameAr: 'كاروسيل', isActive: true })];
  render(<ExecutionTaxonomyManagementPage />);
  expect(screen.getByText('carousel')).toBeInTheDocument();
  expect(screen.getByText('كاروسيل')).toBeInTheDocument();
  expect(screen.getByText('نشطة')).toBeInTheDocument();
});

// ---- 4: الإنشاء يستدعي mutateAsync بالطلب الصحيح ----
it('الإضافة تستدعي mutateAsync بالمجال والرمز والاسم والترتيب الصحيح', async () => {
  render(<ExecutionTaxonomyManagementPage />);
  fireEvent.change(screen.getByLabelText('المجال *'), { target: { value: 'design_type' } });
  fireEvent.change(screen.getByPlaceholderText('carousel'), { target: { value: 'banner' } });
  fireEvent.change(screen.getByPlaceholderText('كاروسيل'), { target: { value: 'بانر' } });
  fireEvent.change(screen.getByPlaceholderText('Carousel'), { target: { value: 'Banner' } });
  fireEvent.click(screen.getByText('إضافة القيمة'));
  await vi.waitFor(() => expect(createMutate).toHaveBeenCalledTimes(1));
  expect(createMutate).toHaveBeenCalledWith({
    domain: 'design_type',
    code: 'banner',
    nameAr: 'بانر',
    nameEn: 'Banner',
    sortOrder: 10,
  });
});

// ---- 5: التعديل يفتح نافذة بحقول Domain/Code للقراءة فقط والاسم مُعبّأ ----
it('التعديل يفتح النافذة بحقول المجال والرمز غير قابلة للتعديل والاسم مُعبّأ مسبقًا', () => {
  listState.data = [tax({ code: 'carousel', nameAr: 'كاروسيل' })];
  render(<ExecutionTaxonomyManagementPage />);
  fireEvent.click(screen.getByText('تعديل'));
  expect(screen.getByText('تعديل القيمة')).toBeInTheDocument();
  expect(screen.getByText(/غير قابلين/)).toBeInTheDocument();
  expect(screen.getByDisplayValue('كاروسيل')).toBeInTheDocument();
});

// ---- 6: تعطيل قيمة نشطة يستدعي هوك التعطيل ----
it('تعطيل قيمة نشطة يستدعي deactivate بعد التأكيد', async () => {
  listState.data = [tax({ id: 'id-a', code: 'carousel', isActive: true })];
  render(<ExecutionTaxonomyManagementPage />);
  fireEvent.click(screen.getByText('تعطيل'));
  await vi.waitFor(() => expect(deactivateMutate).toHaveBeenCalledWith('id-a'));
});

// ---- 7: تفعيل قيمة معطّلة يستدعي هوك التفعيل ----
it('تفعيل قيمة معطّلة يستدعي activate', async () => {
  listState.data = [tax({ id: 'id-b', code: 'story', nameAr: 'ستوري', isActive: false })];
  render(<ExecutionTaxonomyManagementPage />);
  fireEvent.click(screen.getByText('تفعيل'));
  await vi.waitFor(() => expect(activateMutate).toHaveBeenCalledWith('id-b'));
});

// ---- 8: حالة «لا توجد قيم» حين لا قيم في المجال ----
it('تعرض حالة فارغة حين لا توجد قيم في المجال المختار', () => {
  listState.data = [];
  render(<ExecutionTaxonomyManagementPage />);
  expect(screen.getByText('لا توجد قيم')).toBeInTheDocument();
});

// ---- 9: حالة التحميل ----
it('تعرض حالة التحميل حين isLoading', () => {
  listState.data = undefined;
  listState.isLoading = true;
  const { container } = render(<ExecutionTaxonomyManagementPage />);
  // LoadingState يعرض عنصرًا مميّزًا؛ نتأكّد أن الجدول غير ظاهر بعد.
  expect(container.querySelector('table')).toBeNull();
});

// ---- 10: حالة الخطأ ----
it('تعرض حالة الخطأ حين isError', () => {
  listState.data = undefined;
  listState.isError = true;
  const { container } = render(<ExecutionTaxonomyManagementPage />);
  expect(container.querySelector('table')).toBeNull();
});

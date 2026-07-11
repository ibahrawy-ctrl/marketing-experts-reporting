import { render, screen, fireEvent, within } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type { ProjectWorkstreamDto, WorkstreamDeliverableDto } from '../types/api';

// ===== P2.5 — المخرجات المطلوبة داخل هدف العمل (Planning UX Freeze) =====
// تُعزل صفحة تفاصيل المشروع عن الشبكة بتمويه كل هوكاتها. يُوسَّع صفّ هدف العمل (النقر على ▸)
// كي تُركَّب لوحة المخرجات، ثم تُثبَت: العنوان «المخرجات المطلوبة»، زر الإضافة (وتعطيله عند تعطّل الهدف)،
// بطاقات الملخّص التخطيطيّة (عدد الأنواع/الكمية المخططة/الساعات المقدرة/المخرجات النشطة/أقرب موعد)،
// رسالة إرجاء التقدم الفعلي، رؤوس الجدول (بعمود الحالة، بلا منفّذ/متبقّي)، صفّ المخرج بشارة الحالة،
// نموذج الإضافة/التعديل المقسَّم مع التحقّق، تعطيل حقل النوع في التعديل، التفعيل/التعطيل،
// حالات فارغ/تحميل/خطأ، وإخفاء أدوات الإدارة عن غير المخوَّل (canManagePlan=false).

type QueryLike<T> = { data: T | undefined; isLoading: boolean; isError: boolean; refetch: () => void };

const wsState: QueryLike<ProjectWorkstreamDto[]> = { data: [], isLoading: false, isError: false, refetch: vi.fn() };
const delivState: QueryLike<WorkstreamDeliverableDto[]> = { data: [], isLoading: false, isError: false, refetch: vi.fn() };

const createMutate = vi.fn().mockResolvedValue({});
const updateMutate = vi.fn().mockResolvedValue({});
const activateMutate = vi.fn();
const deactivateMutate = vi.fn();

// true ⇒ hasAnyRole يعيد true لأدوار الإدارة ⇒ canManagePlan=true.
let planManager = true;

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'p1' }),
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    canManageClients: true,
    user: { userId: 'admin', roles: ['Admin'] },
    hasAnyRole: () => planManager,
  }),
}));

vi.mock('../lib/useClients', () => ({
  useProject: () => ({
    data: {
      id: 'p1', clientId: 'c1', clientName: 'عميل تجريبي', name: 'مشروع الموقع',
      serviceType: 'Website', status: 'Active', ownerTeamName: null, accountManagerName: null,
      accountManagerId: null, startDate: null, endDate: null, createdAtUtc: '2026-07-01T00:00:00Z', notes: null,
    },
    isLoading: false, isError: false, refetch: vi.fn(),
  }),
  useProjectSummary: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectReports: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useArchiveProject: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({ data: [{ id: 'u1', fullName: 'مسؤول أول' }] }),
  useTeams: () => ({ data: [{ id: 't1', nameAr: 'فريق التطوير' }] }),
}));

vi.mock('../lib/useExecutionTaxonomy', () => ({
  useTaxonomyOptionDetails: (kind: string) => {
    if (kind === 'deliverable')
      return { data: [{ code: 'design_post', nameAr: 'تصميم منشور' }, { code: 'video_reel', nameAr: 'فيديو ريل' }] };
    if (kind === 'usage_context') return { data: [{ code: 'instagram', nameAr: 'إنستغرام' }] };
    if (kind === 'workstream_type') return { data: [{ code: 'web_development', nameAr: 'تطوير الويب' }] };
    return { data: [] };
  },
}));

vi.mock('../lib/useProjectWorkstreams', () => ({
  useProjectWorkstreams: () => wsState,
  useCreateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateProjectWorkstream: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useActivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
  useDeactivateProjectWorkstream: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('../lib/useWorkstreamDeliverables', () => ({
  useWorkstreamDeliverables: () => delivState,
  useCreateWorkstreamDeliverable: () => ({ mutateAsync: createMutate, isPending: false }),
  useUpdateWorkstreamDeliverable: () => ({ mutateAsync: updateMutate, isPending: false }),
  useActivateWorkstreamDeliverable: () => ({ mutate: activateMutate, isPending: false }),
  useDeactivateWorkstreamDeliverable: () => ({ mutate: deactivateMutate, isPending: false }),
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

function deliv(over: Partial<WorkstreamDeliverableDto> = {}): WorkstreamDeliverableDto {
  return {
    id: 'd1', workstreamId: 'w1', deliverableTypeCode: 'design_post', deliverableTypeNameAr: 'تصميم منشور',
    usageContextCode: 'instagram', usageContextNameAr: 'إنستغرام', name: 'منشور إطلاق',
    plannedQuantity: 4, estimatedHours: 6, startDate: '2026-07-01', dueDate: '2026-07-10',
    priority: 'Medium', responsibleUserId: 'u1', responsibleUserName: 'مسؤول أول', notes: null,
    sortOrder: 0, isActive: true, createdAtUtc: '2026-07-01T00:00:00Z', updatedAtUtc: null,
    ...over,
  };
}

// يوسّع صفّ هدف العمل الوحيد كي تُركَّب لوحة المخرجات.
function renderExpanded() {
  wsState.data = [ws()];
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('▸'));
}

// جدول المخرجات هو الجدول المتداخل (الأخير) داخل صفّ الهدف الموسَّع؛
// نعزله عن جدول أهداف العمل الخارجي لتفادي التطابق المزدوج (النوع/تعديل/تعطيل).
function delivTable(): HTMLElement {
  const tables = screen.getAllByRole('table');
  return tables[tables.length - 1];
}

beforeEach(() => {
  wsState.data = [ws()];
  wsState.isLoading = false;
  wsState.isError = false;
  delivState.data = [];
  delivState.isLoading = false;
  delivState.isError = false;
  planManager = true;
  createMutate.mockClear();
  updateMutate.mockClear();
  activateMutate.mockClear();
  deactivateMutate.mockClear();
});

// ---- 1: عنوان «المخرجات المطلوبة» يظهر عند توسيع الهدف ----
it('تعرض عنوان المخرجات المطلوبة عند توسيع الهدف', () => {
  renderExpanded();
  expect(screen.getByText('المخرجات المطلوبة')).toBeInTheDocument();
});

// ---- 2: زر «إضافة مخرَج» يظهر للمخوَّل ----
it('تعرض زر إضافة مخرَج للمخوَّل (canManagePlan)', () => {
  renderExpanded();
  expect(screen.getByText('إضافة مخرَج')).toBeInTheDocument();
});

// ---- 3: زر الإضافة مُعطّل حين يكون هدف العمل معطّلًا ----
it('زر إضافة مخرَج مُعطّل حين يكون هدف العمل معطّلًا', () => {
  wsState.data = [ws({ isActive: false })];
  render(<ProjectDetailPage />);
  fireEvent.click(screen.getByText('▸'));
  const btn = screen.getByText('إضافة مخرَج');
  expect((btn as HTMLButtonElement).disabled).toBe(true);
  expect(btn.getAttribute('title')).toBe('هدف العمل معطّل — فعّله لإضافة مخرَجات.');
});

// ---- 4: بطاقات الملخّص التخطيطيّة (بلا المنفّذ/المتبقّي) + رسالة الإرجاء ----
it('تعرض بطاقات الملخّص التخطيطيّة ورسالة إرجاء التقدم الفعلي بلا عنصر منفّذ/متبقّي', () => {
  delivState.data = [deliv()];
  renderExpanded();
  expect(screen.getByText('عدد أنواع المخرجات')).toBeInTheDocument();
  expect(screen.getByText('إجمالي الكمية المخطَّطة')).toBeInTheDocument();
  expect(screen.getByText('إجمالي الساعات المقدَّرة')).toBeInTheDocument();
  expect(screen.getByText('عدد المخرجات النشطة')).toBeInTheDocument();
  expect(screen.getByText('أقرب موعد تسليم')).toBeInTheDocument();
  // رسالة الإرجاء الموحّدة تظهر.
  expect(
    screen.getByText('سيظهر التقدم الفعلي والمنفذ والمتبقي بعد تفعيل تقارير التنفيذ.'),
  ).toBeInTheDocument();
  // لا عنصر «المنفّذ / المتبقّي» كبطاقة ولا نصّ v5.
  expect(screen.queryByText('المنفّذ / المتبقّي')).not.toBeInTheDocument();
  expect(screen.queryByText('سيظهر بعد تفعيل تقارير التنفيذ v5')).not.toBeInTheDocument();
});

// ---- 5: حساب إجمالي الكمية المخطَّطة والساعات وعدد الأنواع من المخرجات النشطة ----
it('تحتسب الملخّص الإجماليّ للكمية والساعات وعدد الأنواع', () => {
  delivState.data = [
    deliv({ id: 'a', deliverableTypeCode: 'design_post', plannedQuantity: 4, estimatedHours: 6, dueDate: '2026-07-10' }),
    deliv({ id: 'b', deliverableTypeCode: 'video_reel', plannedQuantity: 3, estimatedHours: 5, dueDate: '2026-07-05' }),
  ];
  renderExpanded();
  // القيمة داخل بطاقة الملخّص = شقيقة عنصر التسمية داخل نفس البطاقة.
  const tileValue = (label: string) =>
    screen.getByText(label).parentElement!.querySelector('div:nth-child(2)')!.textContent;
  expect(tileValue('إجمالي الكمية المخطَّطة')).toBe('7'); // 4+3
  expect(tileValue('إجمالي الساعات المقدَّرة')).toBe('11'); // 6+5
  expect(tileValue('عدد أنواع المخرجات')).toBe('2'); // نوعان مختلفان
});

// ---- 6: رؤوس الجدول تشمل الحالة وتخلو من المنفّذ/المتبقّي ----
it('تعرض رؤوس الجدول بعمود الحالة وبلا المنفّذ/المتبقّي', () => {
  delivState.data = [deliv()];
  renderExpanded();
  const table = delivTable();
  const headers = [
    'اسم المخرج', 'نوع المخرج', 'الاستخدام', 'الكمية المخططة', 'الساعات المقدرة',
    'تاريخ البداية', 'تاريخ الاستحقاق', 'الأولوية', 'المسؤول', 'الحالة',
  ];
  for (const h of headers) expect(within(table).getByRole('columnheader', { name: h })).toBeInTheDocument();
  expect(within(table).queryByRole('columnheader', { name: 'المنفّذ' })).not.toBeInTheDocument();
  expect(within(table).queryByRole('columnheader', { name: 'المتبقّي' })).not.toBeInTheDocument();
});

// ---- 7: صفّ المخرج يعرض شارة الحالة (نشط) بلا خلايا تنفيذ نائبة ----
it('يعرض صفّ المخرج شارة الحالة نشط بلا خلايا المنفّذ/المتبقّي النائبة', () => {
  delivState.data = [deliv({ name: 'منشور إطلاق', isActive: true })];
  renderExpanded();
  const table = delivTable();
  expect(within(table).getByText('منشور إطلاق')).toBeInTheDocument();
  expect(within(table).getByText('نشط')).toBeInTheDocument();
  expect(within(table).queryByText('سيظهر بعد التنفيذ')).not.toBeInTheDocument();
  expect(within(table).queryByText('سيُحسَب بعد التنفيذ')).not.toBeInTheDocument();
});

// ---- 8: المخرج المعطّل يعرض شارة الحالة معطّل ----
it('يعرض المخرج المعطّل شارة الحالة معطّل', () => {
  delivState.data = [deliv({ isActive: false })];
  renderExpanded();
  expect(within(delivTable()).getByText('معطّل')).toBeInTheDocument();
});

// ---- 9: نموذج الإضافة المقسَّم يظهر مع أقسامه ومنتقي النوع ----
it('نموذج الإضافة يظهر بأقسامه (التعريف/التخطيط/المسؤولية/إضافي) ومنتقي النوع', () => {
  renderExpanded();
  fireEvent.click(screen.getByText('إضافة مخرَج'));
  expect(screen.getByText('التعريف')).toBeInTheDocument();
  expect(screen.getByText('التخطيط')).toBeInTheDocument();
  expect(screen.getByText('المسؤولية')).toBeInTheDocument();
  expect(screen.getByText('إضافي')).toBeInTheDocument();
  expect(screen.getByRole('option', { name: '— اختر النوع —' })).toBeInTheDocument();
  expect(screen.getByRole('option', { name: 'تصميم منشور' })).toBeInTheDocument();
});

// ---- 10: تحقّق الكمية ≤ 0 ----
it('يرفض النموذج الكمية صفرًا برسالة تحقّق عربية', () => {
  renderExpanded();
  fireEvent.click(screen.getByText('إضافة مخرَج'));
  fireEvent.change(screen.getByLabelText('نوع المخرج (مطلوب)'), { target: { value: 'design_post' } });
  fireEvent.change(screen.getByLabelText('الكمية المخططة (مطلوب)'), { target: { value: '0' } });
  fireEvent.click(screen.getByText('حفظ'));
  expect(screen.getByText('الكمية المخطَّطة يجب أن تكون أكبر من صفر.')).toBeInTheDocument();
  expect(createMutate).not.toHaveBeenCalled();
});

// ---- 11: تحقّق الساعات السالبة ----
it('يرفض النموذج الساعات السالبة برسالة تحقّق عربية', () => {
  renderExpanded();
  fireEvent.click(screen.getByText('إضافة مخرَج'));
  fireEvent.change(screen.getByLabelText('نوع المخرج (مطلوب)'), { target: { value: 'design_post' } });
  fireEvent.change(screen.getByLabelText('الساعات المقدرة (اختياري)'), { target: { value: '-1' } });
  fireEvent.click(screen.getByText('حفظ'));
  expect(screen.getByText('الساعات المقدَّرة لا يمكن أن تكون سالبة.')).toBeInTheDocument();
  expect(createMutate).not.toHaveBeenCalled();
});

// ---- 12: تحقّق نطاق التاريخ (الاستحقاق قبل البداية) ----
it('يرفض النموذج تاريخ استحقاق سابقًا لتاريخ البداية', () => {
  renderExpanded();
  fireEvent.click(screen.getByText('إضافة مخرَج'));
  fireEvent.change(screen.getByLabelText('نوع المخرج (مطلوب)'), { target: { value: 'design_post' } });
  fireEvent.change(screen.getByLabelText('تاريخ البداية (اختياري)'), { target: { value: '2026-07-10' } });
  fireEvent.change(screen.getByLabelText('تاريخ الاستحقاق (اختياري)'), { target: { value: '2026-07-01' } });
  fireEvent.click(screen.getByText('حفظ'));
  expect(screen.getByText('تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ البداية.')).toBeInTheDocument();
  expect(createMutate).not.toHaveBeenCalled();
});

// ---- 13: نموذج التعديل يُعطّل حقل النوع ----
it('نموذج التعديل يظهر بحقل النوع مُعطّلًا', () => {
  delivState.data = [deliv()];
  renderExpanded();
  fireEvent.click(within(delivTable()).getByText('تعديل'));
  expect(screen.getByText('تعديل مخرج')).toBeInTheDocument();
  const typeOption = screen.getByRole('option', { name: 'تصميم منشور' });
  expect((typeOption.parentElement as HTMLSelectElement).disabled).toBe(true);
});

// ---- 14: التعطيل/التفعيل يستدعيان الهوك بمعرّف المخرَج ----
it('تعطيل مخرَج نشط يستدعي deactivate بمعرّفه', () => {
  delivState.data = [deliv({ id: 'd-active', isActive: true })];
  renderExpanded();
  fireEvent.click(within(delivTable()).getByText('تعطيل'));
  expect(deactivateMutate).toHaveBeenCalledWith('d-active');
});

it('تفعيل مخرَج معطّل يستدعي activate بمعرّفه', () => {
  delivState.data = [deliv({ id: 'd-inactive', isActive: false })];
  renderExpanded();
  fireEvent.click(within(delivTable()).getByText('تفعيل'));
  expect(activateMutate).toHaveBeenCalledWith('d-inactive');
});

// ---- 15: حالات فارغ/تحميل/خطأ ----
it('تعرض حالة فارغة حين لا توجد مخرَجات', () => {
  delivState.data = [];
  renderExpanded();
  expect(screen.getByText('لا توجد مخرَجات في خطّة إنتاج هذا الهدف بعد.')).toBeInTheDocument();
});

it('تعرض حالة التحميل حين isLoading', () => {
  delivState.data = undefined;
  delivState.isLoading = true;
  renderExpanded();
  expect(screen.getByText('يتم تحميل المخرَجات…')).toBeInTheDocument();
});

it('تعرض حالة الخطأ حين isError', () => {
  delivState.data = undefined;
  delivState.isError = true;
  renderExpanded();
  expect(screen.getByText('تعذّر عرض المخرَجات')).toBeInTheDocument();
});

// ---- 16: منتقي المسؤول يعرض المستخدمين ----
it('منتقي المسؤول في النموذج يعرض المستخدمين', () => {
  renderExpanded();
  fireEvent.click(screen.getByText('إضافة مخرَج'));
  expect(screen.getByRole('option', { name: 'مسؤول أول' })).toBeInTheDocument();
});

// ---- 17: غير المخوَّل (canManagePlan=false) لا يرى الإضافة ولا أزرار الإجراءات ----
it('غير المخوَّل لا يرى زر الإضافة ولا أزرار الإجراءات', () => {
  planManager = false;
  delivState.data = [deliv()];
  renderExpanded();
  expect(screen.queryByText('إضافة مخرَج')).not.toBeInTheDocument();
  const table = delivTable();
  expect(within(table).queryByText('تعديل')).not.toBeInTheDocument();
  expect(within(table).queryByText('تعطيل')).not.toBeInTheDocument();
  expect(within(table).queryByRole('columnheader', { name: 'الإجراءات' })).not.toBeInTheDocument();
});

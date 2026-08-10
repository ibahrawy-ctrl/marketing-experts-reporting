import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';
import type {
  ClientDocumentDetailDto,
  ClientDocumentDto,
  ClientExternalLinkDto,
  ClientStorageUsageDto,
} from '../types/api';

// ===== CPW-R1B2 — اختبارات سلوك مباشرة لتبويبَي «المستندات» و«الروابط المهمّة» =====
// تُعزل الصفحة عن الشبكة بتمويه كلّ هوكاتها. تُثبَت: عرض التبويبين، الحالات (تحميل/فارغ/خطأ)،
// ظهور الرفع/الأرشفة/التنزيل حسب الصلاحية، سجلّ النسخ، النصّ الإرشاديّ الخاصّ بالأسرار،
// وتحقّق الملفّ غير الصالح وتحقّق الرابط الخارجيّ.
// ملاحظة موثَّقة: لا فرع `isError` على مستوى استعلامَي القائمة في الواجهة الحاليّة — سطح الخطأ
// الوحيد هو تنبيه `err` المحلّي الناتج عن فشل التنزيل/الأرشفة/الحذف، وهو ما يُختبَر هنا.

type QueryLike<T> = { data: T | undefined; isLoading: boolean; isError: boolean; refetch: () => void };

const DOC: ClientDocumentDto = {
  id: 'd1',
  clientId: 'c1',
  title: 'عقد الخدمات',
  description: null,
  categoryCode: 'contract',
  tags: null,
  confidentialityCode: null,
  lifecycleStatus: 'Current',
  approvalStatusCode: 'approved',
  versionCount: 2,
  uploadedByUserId: 'u-admin',
  uploadedByName: 'مدير النظام',
  isArchived: false,
  archivedAtUtc: null,
  archiveReason: null,
  currentVersionId: 'v2',
  currentVersionNo: 2,
  currentFileName: 'contract.pdf',
  currentContentType: 'application/pdf',
  currentSizeBytes: 2048,
  currentScanStatus: 'NotScanned',
  currentForceAttachment: false,
  createdAtUtc: '2026-08-01T00:00:00Z',
  updatedAtUtc: null,
  visibilityType: 'ManagementAndFinance',
  allowedRoles: null,
  allowedUserIds: null,
  canManageVisibility: false,
};

const USAGE: ClientStorageUsageDto = {
  clientId: 'c1',
  usedBytes: 2048,
  quotaBytes: 1024 * 1024,
  remainingBytes: 1024 * 1024 - 2048,
  documentCount: 1,
  versionCount: 2,
  maxUploadSizeBytes: 1024,
  allowedExtensions: ['pdf', 'docx'],
  scanEngine: 'None',
  scannerConfigured: false,
};

const DETAIL: ClientDocumentDetailDto = {
  document: DOC,
  versions: [
    {
      id: 'v2', clientDocumentId: 'd1', versionNo: 2, originalFileName: 'contract-v2.pdf',
      contentType: 'application/pdf', sizeBytes: 2048, sha256: 'a'.repeat(64), scanStatus: 'NotScanned',
      scanEngine: 'None', scannedAtUtc: null, scanDetail: null, uploadedByUserId: 'u-admin',
      uploadedByName: 'مدير النظام', changeNote: null, isCurrent: true, createdAtUtc: '2026-08-02T00:00:00Z',
    },
    {
      id: 'v1', clientDocumentId: 'd1', versionNo: 1, originalFileName: 'contract-v1.pdf',
      contentType: 'application/pdf', sizeBytes: 1024, sha256: 'b'.repeat(64), scanStatus: 'NotScanned',
      scanEngine: 'None', scannedAtUtc: null, scanDetail: null, uploadedByUserId: 'u-admin',
      uploadedByName: 'مدير النظام', changeNote: null, isCurrent: false, createdAtUtc: '2026-08-01T00:00:00Z',
    },
  ],
};

const LINK: ClientExternalLinkDto = {
  id: 'l1', clientId: 'c1', title: 'مجلّد الأصول', url: 'https://example.com/assets',
  categoryCode: 'drive', description: null, isActive: true, sortOrder: 0,
  createdByUserId: 'u-admin', createdByName: 'مدير النظام', createdAtUtc: '2026-08-01T00:00:00Z',
  updatedAtUtc: null,
};

const docsState: QueryLike<ClientDocumentDto[]> = { data: [], isLoading: false, isError: false, refetch: vi.fn() };
const usageState: QueryLike<ClientStorageUsageDto> = { data: USAGE, isLoading: false, isError: false, refetch: vi.fn() };
const detailState: QueryLike<ClientDocumentDetailDto> = { data: DETAIL, isLoading: false, isError: false, refetch: vi.fn() };
const linksState: QueryLike<ClientExternalLinkDto[]> = { data: [], isLoading: false, isError: false, refetch: vi.fn() };

const downloadDoc = vi.fn().mockResolvedValue(undefined);
const createLink = vi.fn().mockResolvedValue({});
const uploadDoc = vi.fn().mockResolvedValue({});

// صلاحية الكتابة: مدير أساسيّ مخوَّل، أو مدير الحساب للعميل (accountManagerId = 'am1').
let canEditClientCore = true;
let currentUserId = 'u-admin';

vi.mock('react-router-dom', () => ({
  useParams: () => ({ clientId: 'c1' }),
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('../lib/auth', () => ({
  useAuth: () => ({ canEditClientCore, user: { userId: currentUserId, roles: ['Admin'] } }),
}));

vi.mock('../lib/useClients', () => ({
  useClient: () => ({
    data: {
      id: 'c1', name: 'عميل تجريبي', status: 'Active', accountManagerId: 'am1', accountManagerName: 'مدير الحساب',
      mainContactName: null, mainContactInfo: null, notes: null, projectCount: 0, activeProjectCount: 0,
      atRiskProjectCount: 0, createdAtUtc: '2026-07-01T00:00:00Z', updatedAtUtc: null, tradeNameEn: null,
      legalName: null, clientTypeCode: null, sectorCode: null, country: null, city: null, website: null,
      sourceCode: null, relationshipStartDate: null, canHardDelete: false, deleteBlockReason: null,
    },
    isLoading: false, isError: false, refetch: vi.fn(),
  }),
  useClientReports: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateClient: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useArchiveClient: () => ({ mutate: vi.fn(), isPending: false }),
  useProjects: () => ({ data: [], isLoading: false, isError: false }),
  useCreateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useClientContacts: () => ({ data: [], isLoading: false, isError: false }),
  useCreateClientContact: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateClientContact: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSetClientContactActive: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useClientDigitalChannels: () => ({ data: [], isLoading: false, isError: false }),
  useCreateClientDigitalChannel: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateClientDigitalChannel: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSetClientDigitalChannelActive: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useClientBrand: () => ({ data: undefined, isLoading: false, isError: false }),
  useUpsertClientBrand: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({ data: [{ id: 'am1', fullName: 'مدير الحساب' }] }),
  useTeams: () => ({ data: [] }),
}));

vi.mock('../lib/useClientDocuments', () => ({
  useClientDocuments: () => docsState,
  useClientDocument: () => detailState,
  useClientStorageUsage: () => usageState,
  useUploadClientDocument: () => ({ mutateAsync: uploadDoc, isPending: false }),
  useAddClientDocumentVersion: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  useUpdateClientDocument: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  useSetClientDocumentArchived: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  useDeleteClientDocument: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  useClientExternalLinks: () => linksState,
  useCreateClientExternalLink: () => ({ mutateAsync: createLink, isPending: false }),
  useUpdateClientExternalLink: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  useSetClientExternalLinkActive: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
  downloadClientDocument: (...args: unknown[]) => downloadDoc(...args),
  downloadClientDocumentVersion: (...args: unknown[]) => downloadDoc(...args),
}));

import ClientDetailPage from './ClientDetailPage';

beforeEach(() => {
  canEditClientCore = true;
  currentUserId = 'u-admin';
  docsState.data = [DOC];
  docsState.isLoading = false;
  usageState.data = USAGE;
  detailState.data = DETAIL;
  detailState.isLoading = false;
  linksState.data = [LINK];
  linksState.isLoading = false;
  downloadDoc.mockReset().mockResolvedValue(undefined);
  createLink.mockReset().mockResolvedValue({});
  uploadDoc.mockReset().mockResolvedValue({});
});

function openDocuments() {
  const view = render(<ClientDetailPage />);
  fireEvent.click(screen.getByRole('button', { name: 'المستندات' }));
  return view;
}

function openLinks() {
  const view = render(<ClientDetailPage />);
  fireEvent.click(screen.getByRole('button', { name: 'الروابط المهمّة' }));
  return view;
}

it('يعرض تبويب المستندات مع بطاقات الاستهلاك وصفّ المستند', () => {
  openDocuments();
  expect(screen.getByText('مستندات العميل')).toBeInTheDocument();
  expect(screen.getByText('المتبقّي من الحصّة')).toBeInTheDocument();
  expect(screen.getByText('عقد الخدمات')).toBeInTheDocument();
});

it('يعرض حالة فارغة حين لا توجد مستندات', () => {
  docsState.data = [];
  openDocuments();
  expect(screen.getByText('لا توجد مستندات')).toBeInTheDocument();
});

it('يعرض حالة التحميل أثناء جلب المستندات', () => {
  docsState.isLoading = true;
  docsState.data = undefined;
  openDocuments();
  expect(screen.getByText('يتم تحميل المستندات…')).toBeInTheDocument();
});

it('يُظهِر تنبيه صدق الفحص حين لا يوجد محرّك فحص مُفعَّل', () => {
  openDocuments();
  expect(screen.getByText(/لا يوجد محرّك فحص فيروسات مُفعَّل/)).toBeInTheDocument();
  // «غير مفحوص» يظهر في التنبيه وفي شارة حالة الفحص معًا.
  expect(screen.getAllByText(/غير مفحوص/).length).toBeGreaterThan(0);
});

it('يُظهِر إجراءات الكتابة للمخوَّل ويُخفيها عن غير المخوَّل', () => {
  const first = openDocuments();
  expect(screen.getByRole('button', { name: '+ رفع مستند' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'أرشفة' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'حذف' })).toBeInTheDocument();

  first.unmount();
  canEditClientCore = false;
  currentUserId = 'u-other';
  openDocuments();
  expect(screen.queryByRole('button', { name: '+ رفع مستند' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'أرشفة' })).not.toBeInTheDocument();
});

it('يمنح مدير الحساب صلاحية الرفع دون صلاحية إدارة العملاء', () => {
  canEditClientCore = false;
  currentUserId = 'am1';
  openDocuments();
  expect(screen.getByRole('button', { name: '+ رفع مستند' })).toBeInTheDocument();
});

it('يُظهِر زرّ التنزيل عند وجود نسخة سارية ويُخفيه عند غيابها', () => {
  const first = openDocuments();
  expect(screen.getAllByRole('button', { name: 'تنزيل' }).length).toBeGreaterThan(0);

  first.unmount();
  docsState.data = [{ ...DOC, currentVersionId: null, currentVersionNo: null }];
  openDocuments();
  expect(screen.queryByRole('button', { name: 'تنزيل' })).not.toBeInTheDocument();
});

it('يعرض سجلّ النسخ مع تمييز النسخة السارية', () => {
  openDocuments();
  fireEvent.click(screen.getByRole('button', { name: 'النسخ (2)' }));
  // «v2» يظهر أيضًا في بطاقة «النسخة الحالية» ⇒ getAllByText.
  expect(screen.getAllByText('v2').length).toBeGreaterThan(0);
  expect(screen.getByText('v1')).toBeInTheDocument();
  expect(screen.getByText('سارية')).toBeInTheDocument();
  expect(screen.getByText('contract-v1.pdf')).toBeInTheDocument();
});

it('يعرض رسالة خطأ عند فشل التنزيل', async () => {
  downloadDoc.mockRejectedValue(new Error('تعذّر الوصول إلى الخادم'));
  openDocuments();
  fireEvent.click(screen.getAllByRole('button', { name: 'تنزيل' })[0]);
  await waitFor(() => expect(screen.getByText('تعذّر الوصول إلى الخادم')).toBeInTheDocument());
});

it('يعرض النصّ الإرشاديّ للامتدادات ويرفض الرفع بلا ملفّ', async () => {
  openDocuments();
  fireEvent.click(screen.getByRole('button', { name: '+ رفع مستند' }));
  expect(screen.getByText(/الامتدادات المسموحة:/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() => expect(screen.getByText('اختر ملفًّا للرفع.')).toBeInTheDocument());
});

it('يرفض الملفّ المتجاوِز للحدّ الأقصى قبل أيّ نداء شبكة', async () => {
  const { container } = openDocuments();
  fireEvent.click(screen.getByRole('button', { name: '+ رفع مستند' }));
  const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
  const big = new File(['x'.repeat(5000)], 'big.pdf', { type: 'application/pdf' });
  fireEvent.change(fileInput, { target: { files: [big] } });
  fireEvent.change(screen.getByLabelText('عنوان المستند'), { target: { value: 'ملفّ ضخم' } });
  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() => expect(screen.getByText(/حجم الملفّ يتجاوز الحدّ المسموح/)).toBeInTheDocument());
});

it('يعرض تبويب الروابط مع التنبيه الخاصّ بعدم إدراج الأسرار', () => {
  openLinks();
  expect(screen.getByText('الروابط المهمّة', { selector: 'h2' })).toBeInTheDocument();
  expect(
    screen.getByText('الرابط مرجع عنوان فقط — لا تُدرِج داخله أيّ كلمة مرور أو رمز وصول أو مفتاح واجهة برمجيّة.'),
  ).toBeInTheDocument();
  expect(screen.getByText('مجلّد الأصول')).toBeInTheDocument();
});

it('يعرض حالة فارغة حين لا توجد روابط', () => {
  linksState.data = [];
  openLinks();
  expect(screen.getByText('لا توجد روابط')).toBeInTheDocument();
});

it('يتحقّق من حقول الرابط الخارجيّ قبل الحفظ', async () => {
  openLinks();
  fireEvent.click(screen.getByRole('button', { name: '+ رابط' }));
  fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
  await waitFor(() => expect(screen.getByText('عنوان الرابط مطلوب.')).toBeInTheDocument());

  fireEvent.change(screen.getByLabelText('العنوان'), { target: { value: 'لوحة التحليلات' } });
  fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
  await waitFor(() => expect(screen.getByText('العنوان الإلكتروني مطلوب.')).toBeInTheDocument());
  expect(createLink).not.toHaveBeenCalled();
});

// ===== CPW-R2 — سياسة رؤية المستند + رؤية مدير العميل =====
// الخادم هو الفاصل الوحيد في التطبيق؛ ما يُختبَر هنا سلوك الاختيار والعرض في الواجهة فقط.

function openUpload() {
  const view = openDocuments();
  fireEvent.click(screen.getByRole('button', { name: '+ رفع مستند' }));
  return view;
}

/// «التصنيف» يظهر أيضًا في شريط ترشيح المستندات ⇒ تُحصَر الاستعلامات داخل نموذج الرفع وحده.
function uploadForm() {
  return within(screen.getByText('رفع مستند جديد').closest('div') as HTMLElement);
}

/// يملأ الحدّ الأدنى المقبول للرفع (ملفّ صالح الحجم + عنوان) كي يصل النموذج إلى استدعاء الرفع.
function fillValidUpload(container: HTMLElement) {
  const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
  const small = new File(['x'.repeat(16)], 'doc.pdf', { type: 'application/pdf' });
  fireEvent.change(fileInput, { target: { files: [small] } });
  fireEvent.change(screen.getByLabelText('عنوان المستند'), { target: { value: 'مستند اختبار' } });
}

it('يعرض محرّر سياسة الرؤية بخياراته الثمانية في نموذج الرفع', () => {
  openUpload();
  expect(screen.getByText('من يمكنه رؤية هذا المستند؟')).toBeInTheDocument();
  const select = screen.getByLabelText('سياسة الرؤية') as HTMLSelectElement;
  expect(select.options.length).toBe(8);
  [
    'كل من لديه صلاحية على العميل',
    'الإدارة فقط',
    'الإدارة والمالية',
    'المالية فقط',
    'الموارد البشرية والإدارة',
    'فريق المشروع',
    'أدوار مخصصة',
    'أشخاص محددون',
  ].forEach((label) => {
    expect([...select.options].some((o) => o.textContent === label)).toBe(true);
  });
});

it('يطبّق سياسة الرؤية الافتراضيّة للتصنيف تلقائيًّا عند تغيير التصنيف', () => {
  const { container } = openUpload();
  const select = screen.getByLabelText('سياسة الرؤية') as HTMLSelectElement;
  // «عقد» أوّل تصنيف ⇒ الافتراضيّ «الإدارة والمالية».
  expect(select.value).toBe('ManagementAndFinance');

  fireEvent.change(uploadForm().getByLabelText('التصنيف'), { target: { value: 'TechnicalProposal' } });
  expect(select.value).toBe('ProjectTeam');

  fireEvent.change(uploadForm().getByLabelText('التصنيف'), { target: { value: 'NDA' } });
  expect(select.value).toBe('ManagementOnly');

  fireEvent.change(uploadForm().getByLabelText('التصنيف'), { target: { value: 'Report' } });
  expect(select.value).toBe('ClientScoped');
  expect(container).toBeTruthy();
});

it('لا يدوس على اختيار المستخدم اليدويّ لسياسة الرؤية عند تغيير التصنيف بعده', async () => {
  const { container } = openUpload();
  const select = screen.getByLabelText('سياسة الرؤية') as HTMLSelectElement;

  fireEvent.change(select, { target: { value: 'FinanceOnly' } });
  expect(select.value).toBe('FinanceOnly');

  // التصنيف الجديد افتراضيّه «فريق المشروع» — لكنّ الاختيار اليدويّ يبقى.
  fireEvent.change(uploadForm().getByLabelText('التصنيف'), { target: { value: 'TechnicalProposal' } });
  expect(select.value).toBe('FinanceOnly');

  fillValidUpload(container);
  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() => expect(uploadDoc).toHaveBeenCalled());
  expect(uploadDoc.mock.calls[0][0]).toMatchObject({
    categoryCode: 'TechnicalProposal',
    visibilityType: 'FinanceOnly',
  });
});

it('يفرض اختيار دور واحد على الأقلّ لسياسة «أدوار مخصصة» ثمّ يُرسِل الأدوار المختارة', async () => {
  const { container } = openUpload();
  fireEvent.change(screen.getByLabelText('سياسة الرؤية'), { target: { value: 'CustomRoles' } });
  expect(screen.getByText('الأدوار المصرّح لها')).toBeInTheDocument();
  expect(screen.getByText('اختر دورًا واحدًا على الأقلّ.')).toBeInTheDocument();

  fillValidUpload(container);
  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() =>
    expect(screen.getByText('سياسة «أدوار مخصصة» تتطلّب اختيار دور واحد على الأقلّ.')).toBeInTheDocument(),
  );
  expect(uploadDoc).not.toHaveBeenCalled();

  fireEvent.click(screen.getByLabelText('المدير المالي'));
  expect(screen.queryByText('اختر دورًا واحدًا على الأقلّ.')).not.toBeInTheDocument();
  // المستخدم الحاليّ Admin ⇒ السياسة لا تشمله ⇒ يظهر تنبيه فقد الرؤية.
  expect(screen.getByText(/السياسة المختارة لا تشمل حسابك/)).toBeInTheDocument();

  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() => expect(uploadDoc).toHaveBeenCalled());
  expect(uploadDoc.mock.calls[0][0]).toMatchObject({
    visibilityType: 'CustomRoles',
    allowedRoles: ['FinanceManager'],
  });
});

it('يفرض اختيار شخص واحد على الأقلّ لسياسة «أشخاص محددون» ثمّ يُرسِل المعرّفات المختارة', async () => {
  const { container } = openUpload();
  fireEvent.change(screen.getByLabelText('سياسة الرؤية'), { target: { value: 'CustomUsers' } });
  expect(screen.getByText('الأشخاص المصرّح لهم')).toBeInTheDocument();
  expect(screen.getByText('اختر شخصًا واحدًا على الأقلّ.')).toBeInTheDocument();

  fillValidUpload(container);
  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() =>
    expect(screen.getByText('سياسة «أشخاص محددون» تتطلّب اختيار شخص واحد على الأقلّ.')).toBeInTheDocument(),
  );
  expect(uploadDoc).not.toHaveBeenCalled();

  fireEvent.click(screen.getByLabelText('مدير الحساب'));
  expect(screen.queryByText('اختر شخصًا واحدًا على الأقلّ.')).not.toBeInTheDocument();

  fireEvent.click(screen.getByRole('button', { name: 'رفع المستند' }));
  await waitFor(() => expect(uploadDoc).toHaveBeenCalled());
  expect(uploadDoc.mock.calls[0][0]).toMatchObject({
    visibilityType: 'CustomUsers',
    allowedUserIds: ['am1'],
  });
});

it('يعرض شارة سياسة الرؤية على صفّ المستند', () => {
  openDocuments();
  expect(screen.getByText('الإدارة والمالية')).toBeInTheDocument();
});

it('لا يعرض المستند المحجوب لأنّ الخادم يُرشِّح القائمة قبل وصولها', () => {
  // القائمة تأتي مُرشَّحة من الخادم ⇒ المستند المحجوب غير موجود أصلًا في الحمولة.
  docsState.data = [DOC];
  openDocuments();
  expect(screen.getByText('عقد الخدمات')).toBeInTheDocument();
  expect(screen.queryByText('عرض مالي سرّي')).not.toBeInTheDocument();
  expect(screen.queryByText('المالية فقط')).not.toBeInTheDocument();
});

it('لا يكشف قوائم الأدوار/الأشخاص المصرّح لهم إلّا لمن يملك إدارة الرؤية', () => {
  const withPolicy = {
    ...DOC,
    visibilityType: 'CustomRoles' as const,
    allowedRoles: ['FinanceManager'],
    allowedUserIds: ['am1'],
  };

  // بلا صلاحيّة إدارة الرؤية: القائمتان لا تُعرَضان إطلاقًا.
  docsState.data = [{ ...withPolicy, canManageVisibility: false }];
  const first = openDocuments();
  expect(screen.queryByText('الأدوار المصرّح لها')).not.toBeInTheDocument();
  expect(screen.queryByText('الأشخاص المصرّح لهم')).not.toBeInTheDocument();
  first.unmount();

  // مع الصلاحيّة: تُعرَضان.
  docsState.data = [{ ...withPolicy, canManageVisibility: true }];
  const second = openDocuments();
  expect(screen.getByText('الأدوار المصرّح لها')).toBeInTheDocument();
  expect(screen.getByText('الأشخاص المصرّح لهم')).toBeInTheDocument();
  second.unmount();
});

it('يعرض ملفّ العميل لمدير العميل دون إظهار أدوات تعديل البيانات الأساسيّة', () => {
  canEditClientCore = false;
  currentUserId = 'am1';
  render(<ClientDetailPage />);
  expect(screen.getAllByText('عميل تجريبي').length).toBeGreaterThan(0);
  expect(screen.getAllByText('مدير العميل').length).toBeGreaterThan(0);
  expect(screen.queryByRole('button', { name: 'تعديل بيانات العميل' })).not.toBeInTheDocument();
});

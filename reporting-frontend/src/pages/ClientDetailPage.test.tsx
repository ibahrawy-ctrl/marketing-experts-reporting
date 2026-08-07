import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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
  useUploadClientDocument: () => ({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false }),
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

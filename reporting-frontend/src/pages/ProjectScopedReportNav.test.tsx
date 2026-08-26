// ======================================================================
// التنقّل من صفحة المشروع (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1)
//
// **لماذا `href` الفعليّ لا استدعاء `navigate` مموَّه؟** العطل المُبلَّغ عنه كان وجهةً
// خاطئة لا نقرةً ضائعة: زرّ «فتح» كان يقود إلى صفحة التقارير العامّة فيعرض عمل مشروعات
// أخرى يحملها التقرير الأسبوعيّ نفسه. تمويه الموجّه كان سيخفي بالضبط ما نقيسه، فيُركَّب
// `MemoryRouter` حقيقيّ ويُقرأ العنوان من الـDOM.
//
// `useProject`/`useProjectReports` وحدهما مموَّهان جزئيًّا (`importOriginal`) كي تبقى بقيّة
// الوحدة حقيقيّة؛ وما عداهما هوكات حقيقيّة فوق `api` متجسَّس ⟹ سجلّ النداءات قابل للقياس.
// ======================================================================

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { LinkedReportRow } from '../types/api';

const PROJECT_ID = '1f23cea4-682e-4dc4-a72c-ac7be39d2356';
const SUBMISSION_ID = '1caffdb6-0a94-41db-831e-765bc025bfda';

const ROW: LinkedReportRow = {
  submissionId: SUBMISSION_ID,
  submitterId: 'u-ahmed',
  submitterName: 'أحمد عبدالفتاح',
  periodType: 'Weekly',
  periodKey: '2026-W35',
  status: 'Draft',
  submittedAtUtc: null,
  clientId: 'c1',
  projectId: PROJECT_ID,
};

const PROJECT = {
  id: PROJECT_ID,
  clientId: 'c1',
  clientName: 'عيادات محمد الرافعي',
  name: 'حملات إعلانية',
  serviceType: 'MediaBuying',
  status: 'Active',
  ownerTeamId: null,
  ownerTeamName: null,
  accountManagerId: null,
  accountManagerName: null,
  startDate: null,
  endDate: null,
  notes: null,
  createdAtUtc: '2026-07-01T00:00:00Z',
  updatedAtUtc: null,
  canHardDelete: false,
  deleteBlockReason: null,
  projectOwnerId: null,
  projectOwnerName: null,
  teamLeaderId: null,
  teamLeaderName: null,
  progressPercent: null,
  progressMode: 'NoDeliverables',
  progressCalculatedAtUtc: null,
  progressSourceDeliverableCount: 0,
  healthStatus: null,
  healthPercent: null,
  healthComputedAtUtc: null,
  canManageStructure: true,
  canOperate: true,
};

vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({
    canManageProjectStructure: true,
    canManageClients: true,
    canEditClientCore: true,
    user: { userId: 'u-admin', roles: ['Admin'] },
    hasAnyRole: () => true,
  }),
}));

vi.mock('../lib/useClients', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/useClients')>()),
  useProject: () => ({ data: PROJECT, isLoading: false, isError: false, refetch: vi.fn() }),
  useProjectSummary: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectReports: () => ({ data: [ROW], isLoading: false, isError: false }),
}));

import ProjectDetailPage from './ProjectDetailPage';
import { LinkedReportsCard } from './ClientDetailPage';

let getCalls: string[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    getCalls.push(url);
    return Promise.resolve({ data: [] } as never);
  });
});

function renderProjectPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/app/projects/${PROJECT_ID}`]}>
        <Routes>
          <Route path="/app/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('صفحة المشروع — وجهات التنقّل', () => {
  // ===== واجهة 1: زرّ 360 يحمل نفس معرّف المشروع =====
  it('يقود زرّ «مساحة عمل المشروع (360)» إلى مساحة عمل نفس المشروع', () => {
    renderProjectPage();
    const link = screen.getByRole('link', { name: 'مساحة عمل المشروع (360)' });
    expect(link).toHaveAttribute('href', `/app/projects/${PROJECT_ID}/360`);
  });

  // ===== واجهة 2: «فتح» يبقى تحت نطاق المشروع =====
  it('يقود زرّ «فتح» إلى مساهمة التقرير في هذا المشروع لا إلى التقرير الكامل', () => {
    renderProjectPage();
    expect(screen.getByRole('link', { name: 'فتح' })).toHaveAttribute(
      'href',
      `/app/projects/${PROJECT_ID}/reports/${SUBMISSION_ID}`,
    );
  });

  // ===== واجهة 3: لا تحويل صامت إلى قائمة التقارير العامّة =====
  it('لا يوجد في صفحة المشروع أيّ رابط إلى قائمة التقارير العامّة', () => {
    const { container } = renderProjectPage();
    const hrefs = [...container.querySelectorAll('a')].map((a) => a.getAttribute('href') ?? '');
    expect(hrefs.filter((h) => h.startsWith('/app/submissions'))).toHaveLength(0);
    // ولا يخرج أيّ رابط عن نطاق هذا المشروع/عميله (باستثناء فتات المسار المعروفة).
    expect(hrefs).not.toContain('/app/reports');
  });

  // ===== حارس السياق: خارج المشروع تبقى الوجهة القديمة كما هي بلا توسيع =====
  it('يبقى «فتح» في سياق العميل على وجهته العامّة السابقة بلا تغيير', () => {
    render(
      <MemoryRouter>
        <LinkedReportsCard rows={[ROW]} title="تقارير العميل المرتبطة" />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'فتح' })).toHaveAttribute(
      'href',
      `/app/submissions?open=${SUBMISSION_ID}`,
    );
  });

  // ===== لا نداء شبكة غير متوقَّع من صفحة المشروع =====
  it('لا تُطلِق صفحة المشروع أيّ نداء إلى التسليم الكامل', () => {
    renderProjectPage();
    expect(getCalls.filter((u) => u.startsWith('/submissions/'))).toHaveLength(0);
  });
});

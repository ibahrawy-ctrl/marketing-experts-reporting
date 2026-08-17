// ======================================================================
// جسر التنفيذ الهجين — اختبارات الواجهة (P360-WF-R2 §10 · OPTION B)
//
// **ما يُقاس هنا هو القرار لا الشكل**: النموذج مفتوح لمن يرى المشروع، وأزرار الحسم
// تتبع `canReview` **لكلّ مقترح على حدة** كما أرسلها الخادم. أيّ ادّعاء يُبنى على أدوار
// محلّيّة كان سيعيد بناء التخويل في الواجهة — وهو بالضبط ما أزالته §12.
// ======================================================================

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../../lib/api';
import type {
  ProjectContractDeliverableDto,
  ProjectExecutionProposalDto,
} from '../../types/project360';
import { ProjectExecutionBridgeTab } from './ProjectExecutionBridgeTab';
import type { Project360Access } from './shared';

const P = 'project-1';
const D = 'deliverable-1';
const PROPOSALS_URL = `/projects/${P}/execution-proposals`;
const DELIVERABLES_URL = `/projects/${P}/contract-deliverables`;

const OPERATOR: Project360Access = { canManage: true, canOperate: true, canWriteGovernance: true };
const READER: Project360Access = { canManage: false, canOperate: false, canWriteGovernance: false };

type Call = { url: string; body?: unknown };

let getBodies: Record<string, unknown>;
let posts: Call[] = [];
let patches: Call[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  posts = [];
  patches = [];
  getBodies = { [PROPOSALS_URL]: [], [DELIVERABLES_URL]: [deliverable()] };

  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: url in getBodies ? getBodies[url] : [] } as never),
  );
  vi.spyOn(api, 'post').mockImplementation((url: string, body?: unknown) => {
    posts.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
  vi.spyOn(api, 'patch').mockImplementation((url: string, body?: unknown) => {
    patches.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
});

function deliverable(
  over: Partial<ProjectContractDeliverableDto> = {},
): ProjectContractDeliverableDto {
  return {
    id: D,
    projectId: P,
    objectiveId: null,
    workstreamId: null,
    deliverableTypeCode: 'POST',
    deliverableTypeNameAr: 'منشور',
    name: 'حزمة منشورات أغسطس',
    description: null,
    plannedQuantity: 10,
    completedQuantity: 4,
    status: 'InProgress',
    progressPercent: 40,
    weightPercentage: 0,
    startDate: null,
    dueDate: null,
    deliveredAtUtc: null,
    priority: 'Medium',
    ownerUserId: null,
    ownerFullName: null,
    notes: null,
    sortOrder: 1,
    isActive: true,
    createdAtUtc: '2026-08-01T09:00:00Z',
    updatedAtUtc: null,
    ...over,
  };
}

function proposal(over: Partial<ProjectExecutionProposalDto> = {}): ProjectExecutionProposalDto {
  return {
    id: 'proposal-1',
    projectId: P,
    deliverableId: D,
    deliverableName: 'حزمة منشورات أغسطس',
    deliverableCurrentProgressPercent: 40,
    deliverableCurrentStatus: 'InProgress',
    submissionId: null,
    proposedById: 'user-emp',
    proposedByFullName: 'ماجد الحربي',
    proposedProgressPercent: 70,
    proposedStatus: 'InProgress',
    executionNote: 'نُشِرت سبعة منشورات من عشرة',
    status: 'Pending',
    reviewedById: null,
    reviewedByFullName: null,
    reviewedAtUtc: null,
    reviewNote: null,
    previousProgressPercent: null,
    createdAtUtc: '2026-08-15T09:00:00Z',
    canReview: true,
    ...over,
  };
}

function renderTab(access: Project360Access = OPERATOR) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <ProjectExecutionBridgeTab projectId={P} access={access} />
    </QueryClientProvider>,
  );
}

describe('جسر التنفيذ — رفع الادّعاء', () => {
  it('يفتح نموذج التسجيل لمن يرى المشروع ولو بلا صلاحيّة تشغيليّة', async () => {
    renderTab(READER);
    const open = await screen.findByRole('button', { name: 'تسجيل تنفيذ' });
    expect(open).not.toBeDisabled();
  });

  it('يعطّل التسجيل حين لا يوجد مخرَج تعاقديّ نشط', async () => {
    getBodies[DELIVERABLES_URL] = [deliverable({ isActive: false })];
    renderTab();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'تسجيل تنفيذ' })).toBeDisabled(),
    );
    expect(
      screen.getByText(/لا يوجد مخرَج تعاقديّ نشط لتسجيل تنفيذ عليه/),
    ).toBeInTheDocument();
  });

  it('يعرض النسبة الحاليّة للمخرَج داخل القائمة كي لا يُقترَح رقم أدنى بالسهو', async () => {
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'تسجيل تنفيذ' }));
    expect(await screen.findByText(/حزمة منشورات أغسطس — الحاليّ/)).toBeInTheDocument();
  });

  it('يرسل الادّعاء إلى مسار المقترحات لا إلى مسار المخرَج مباشرةً', async () => {
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'تسجيل تنفيذ' }));

    fireEvent.change(await screen.findByRole('combobox', { name: /المخرَج التعاقديّ/ }), {
      target: { value: D },
    });
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '70' } });
    fireEvent.click(screen.getByRole('button', { name: 'إرسال للمراجعة' }));

    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0].url).toBe(PROPOSALS_URL);
    expect(posts[0].body).toMatchObject({
      deliverableId: D,
      proposedProgressPercent: 70,
      proposedStatus: 'InProgress',
    });
    // لا كتابة مباشرة على المخرَج من هذا المسار.
    expect(patches).toHaveLength(0);
  });

  it('يمنع الإرسال قبل اختيار المخرَج وتحديد النسبة', async () => {
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'تسجيل تنفيذ' }));
    expect(await screen.findByRole('button', { name: 'إرسال للمراجعة' })).toBeDisabled();
  });
});

describe('جسر التنفيذ — الحسم', () => {
  it('يعرض المقارنة «من الحاليّ إلى المُدَّعى» لا الرقم المُدَّعى وحده', async () => {
    getBodies[PROPOSALS_URL] = [proposal()];
    renderTab();
    expect(await screen.findByText(/من .* إلى/)).toBeInTheDocument();
  });

  it('يُظهر أزرار الحسم بـ`canReview` القادمة من الخادم', async () => {
    getBodies[PROPOSALS_URL] = [proposal({ canReview: true })];
    renderTab();
    expect(await screen.findByRole('button', { name: 'قبول وتطبيق على المخرَج' })).toBeInTheDocument();
  });

  it('يُخفي أزرار الحسم حين ينفي الخادم `canReview` ولو كان المستخدم مشغّلًا', async () => {
    getBodies[PROPOSALS_URL] = [proposal({ canReview: false })];
    renderTab(OPERATOR);
    await screen.findByText('حزمة منشورات أغسطس');
    expect(
      screen.queryByRole('button', { name: 'قبول وتطبيق على المخرَج' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'رفض' })).not.toBeInTheDocument();
  });

  it('يمنع الرفض بلا سبب ويسمح به بعد كتابة الملاحظة', async () => {
    getBodies[PROPOSALS_URL] = [proposal()];
    renderTab();
    const reject = await screen.findByRole('button', { name: 'رفض' });
    expect(reject).toBeDisabled();

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'الدليل ناقص' } });
    expect(screen.getByRole('button', { name: 'رفض' })).not.toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'رفض' }));
    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0].url).toBe(`${PROPOSALS_URL}/proposal-1/review`);
    expect(patches[0].body).toEqual({ accept: false, reviewNote: 'الدليل ناقص' });
  });

  it('يرسل القبول بلا اشتراط ملاحظة', async () => {
    getBodies[PROPOSALS_URL] = [proposal()];
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'قبول وتطبيق على المخرَج' }));
    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0].body).toEqual({ accept: true, reviewNote: null });
  });

  it('يعرض لقطة «ما كانت النسبة قبل التطبيق» على المقترح المقبول', async () => {
    getBodies[PROPOSALS_URL] = [
      proposal({
        status: 'Accepted',
        canReview: false,
        reviewedByFullName: 'ريم القحطاني',
        reviewedAtUtc: '2026-08-16T09:00:00Z',
        previousProgressPercent: 40,
      }),
    ];
    renderTab();
    expect(await screen.findByText(/كانت النسبة قبل التطبيق/)).toBeInTheDocument();
    expect(screen.getByText(/ريم القحطاني/)).toBeInTheDocument();
  });

  // ---- R2.1 · GAP-R21-03: تمييز التحديث المباشر عن قبول الادّعاء ----
  // التحديث التشغيليّ المباشر صار يُقيَّد في السلسلة نفسها كي تُعرف قيمته السابقة وفاعله،
  // ولولا التمييز لقُرِئ فعلُ طرفٍ واحد كأنّه مرّ بدورة مراجعة من طرفين.
  it('يميّز التحديث المباشر حين يكون الرافع هو المراجِع نفسه', async () => {
    getBodies[PROPOSALS_URL] = [
      proposal({
        status: 'Accepted',
        canReview: false,
        proposedById: 'user-tl',
        proposedByFullName: 'قائد الفريق',
        reviewedById: 'user-tl',
        reviewedByFullName: 'قائد الفريق',
        reviewedAtUtc: '2026-08-17T09:00:00Z',
        previousProgressPercent: 30,
      }),
    ];
    renderTab();
    expect(await screen.findByText('تحديث مباشر')).toBeInTheDocument();
    expect(screen.getByText(/حدّثه قائد الفريق/)).toBeInTheDocument();
    expect(screen.getByText(/طُبِّق مباشرةً/)).toBeInTheDocument();
    expect(screen.getByText(/كانت النسبة قبل التطبيق/)).toBeInTheDocument();
  });

  it('لا يصف قبول ادّعاء غيرِه بأنّه تحديث مباشر', async () => {
    getBodies[PROPOSALS_URL] = [
      proposal({
        status: 'Accepted',
        canReview: false,
        proposedById: 'user-emp',
        proposedByFullName: 'منفّذ',
        reviewedById: 'user-tl',
        reviewedByFullName: 'قائد الفريق',
        reviewedAtUtc: '2026-08-17T09:00:00Z',
        previousProgressPercent: 30,
      }),
    ];
    renderTab();
    expect(await screen.findByText(/رفعه منفّذ/)).toBeInTheDocument();
    expect(screen.queryByText('تحديث مباشر')).not.toBeInTheDocument();
  });

  it('يرشّح بالحالة عبر معامل الاستعلام لا في العميل', async () => {
    const spy = vi.spyOn(api, 'get');
    renderTab();
    await screen.findByRole('button', { name: 'تسجيل تنفيذ' });
    fireEvent.click(screen.getByRole('button', { name: 'المرفوضة' }));
    await waitFor(() =>
      expect(
        spy.mock.calls.some(
          ([url, cfg]) =>
            url === PROPOSALS_URL &&
            (cfg as { params?: { status?: string } } | undefined)?.params?.status === 'Rejected',
        ),
      ).toBe(true),
    );
  });
});

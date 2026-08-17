// ======================================================================
// Project 360 Workspace — اختبارات التعاقد مع الشبكة (CPW-R3 · R2-W12 · §12)
//
// **لماذا `api` مُتجسَّس لا هوكات مموَّهة؟** لأنّ الادّعاءات المطلوبة هنا كمّيّة عن
// **النداءات نفسها**: «نداء واحد للنظرة العامّة»، و«لا نداء لتبويب لم يُفتَح». تمويه
// الهوكات كان سيخفي بالضبط ما نريد قياسه. لذا يُركَّب `QueryClientProvider` حقيقيّ
// فوق `api` متجسَّس، ويُقاس سجلّ الـURLs الفعليّ.
//
// `retry: false` إلزاميّ: الافتراض ثلاث محاولات ⟹ عدّاد النداءات يصير بلا معنى،
// وحالات الخطأ تتأخّر إلى ما بعد مهلة الانتظار.
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { formatPercent } from '../lib/format';
import type { ProjectOverviewDto } from '../types/project360';
import Project360Page from './Project360Page';

const PROJECT_ID = '11111111-1111-1111-1111-111111111111';
const OVERVIEW_URL = `/projects/${PROJECT_ID}/overview`;
const OBJECTIVES_URL = `/projects/${PROJECT_ID}/objectives`;
const KPIS_URL = `/projects/${PROJECT_ID}/kpis`;
const STRATEGY_URL = `/projects/${PROJECT_ID}/strategy`;
const SCHEMA_URL = `/projects/${PROJECT_ID}/strategy/schema`;
const DELIVERABLES_URL = `/projects/${PROJECT_ID}/contract-deliverables`;
const RISKS_URL = `/projects/${PROJECT_ID}/risks`;
const RECOMPUTE_URL = `/projects/${PROJECT_ID}/health/recompute`;

// ---------------------------------------------------------------------
// **لا تمويه لـ`useAuth` بعد الآن**: الصفحة لم تعد تشتقّ الصلاحيّة من الأدوار
// والمعرّفات محلّيًّا (P360-WF-R2 §12) — مصدرها الوحيد `overview.access` القادم من
// الخادم. إبقاء تمويه المصادقة كان سيسمح للاختبار بأن ينجح على قواعد لا وجود لها
// في الشيفرة، وهو بالضبط الانحراف الذي أُزيل.
// ---------------------------------------------------------------------

// ---------------------------------------------------------------------
// حمولة النظرة العامّة.
//
// **الأرقام متنافرة عمدًا**: `score = 42.5` مع `status = 'Green'` ومكوّنات كلّها 90.
// أيّ إعادة احتساب في العميل كانت ستنتج ٩٠ وحالة مختلفة ⟹ التنافر هو الدليل.
// ---------------------------------------------------------------------
function overviewFixture(): ProjectOverviewDto {
  return {
    project: {
      id: PROJECT_ID,
      name: 'مشروع الهويّة الرقميّة',
      clientId: 'client-1',
      clientName: 'عميل الاختبار',
      serviceType: 'Social',
      status: 'Active',
      startDate: '2026-01-01',
      endDate: '2026-12-31',
      progressPercent: 40,
      summary: 'موجز المشروع للاختبار',
      projectOwnerId: 'user-owner',
      teamLeaderId: 'user-tl',
      accountManagerId: 'user-am',
      ownerTeamId: 'team-1',
      // أسماء أشخاص لا تطابق تسميات الحقول: لو سُمّي المسنَد باسم حقله لصار الادّعاء
      // يمرّ على التسمية نفسها ولا يثبت أنّ القيمة عُرِضت أصلًا.
      projectOwnerName: 'سارة العتيبي',
      teamLeaderName: 'ماجد الحربي',
      accountManagerName: 'ريم القحطاني',
      ownerTeamName: 'فريق المحتوى',
      progressMode: 'Weighted',
      progressCalculatedAtUtc: '2026-08-15T09:00:00Z',
      progressSourceDeliverableCount: 2,
    },
    health: {
      score: 42.5,
      status: 'Green',
      kpiScore: 90,
      progressPercent: 90,
      scheduleScore: 90,
      reasonCodes: ['health.kpi.below_target', 'health.schedule.behind_plan'],
      lastEvaluatedAtUtc: '2026-08-15T09:00:00Z',
      persistedAtUtc: '2026-08-15T09:00:00Z',
    },
    objectives: {
      total: 3,
      notStarted: 1,
      inProgress: 1,
      atRisk: 1,
      completed: 0,
      averageAchievementPercent: 55,
      items: [],
    },
    kpis: {
      total: 4,
      onTrack: 2,
      atRisk: 1,
      offTrack: 1,
      withoutReadings: 0,
      averageAchievementPercent: 61,
      lastReadingDate: '2026-08-10',
    },
    deliverables: { total: 2, completed: 1, pending: 1, delayed: 0 },
    governance: {
      risksTotal: 1,
      risksOpen: 1,
      decisionsTotal: 0,
      decisionsPending: 0,
      notesTotal: 0,
      notesOpen: 0,
    },
    strategy: { exists: true, filledCoreFields: 6, totalCoreFields: 11, attributesCount: 2 },
    access: { canManageStructure: true, canOperate: true, canWriteGovernance: true },
  };
}

// ---------------------------------------------------------------------
// تجسّس الشبكة.
// ---------------------------------------------------------------------
let getCalls: string[] = [];
let postCalls: string[] = [];
let getBodies: Record<string, unknown> = {};
let getFailures: Record<string, unknown> = {};

function axiosLikeError(status: number, type: string, detail: string) {
  return Object.assign(new Error(detail), {
    isAxiosError: true,
    response: { status, data: { type, detail, title: detail } },
  });
}

function countOf(list: string[], url: string) {
  return list.filter((u) => u === url).length;
}

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  postCalls = [];
  getFailures = {};
  getBodies = {
    [OVERVIEW_URL]: overviewFixture(),
    [OBJECTIVES_URL]: [],
    [KPIS_URL]: [],
    [STRATEGY_URL]: null,
    [SCHEMA_URL]: { coreFields: [], dynamicFields: [], sections: [] },
    [DELIVERABLES_URL]: [],
    [`${DELIVERABLES_URL}/types`]: [],
    [RISKS_URL]: [],
    [`/projects/${PROJECT_ID}/decisions`]: [],
    [`/projects/${PROJECT_ID}/notes`]: [],
    '/directory/users': [],
  };

  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    getCalls.push(url);
    if (url in getFailures) return Promise.reject(getFailures[url]);
    return Promise.resolve({ data: url in getBodies ? getBodies[url] : [] } as never);
  });
  vi.spyOn(api, 'post').mockImplementation((url: string) => {
    postCalls.push(url);
    return Promise.resolve({ data: overviewFixture().health } as never);
  });
});

function renderWorkspace() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/app/projects/${PROJECT_ID}/360`]}>
        <Routes>
          <Route path="/app/projects/:projectId/360" element={<Project360Page />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function renderLoaded() {
  const utils = renderWorkspace();
  await screen.findByRole('heading', { name: 'مشروع الهويّة الرقميّة' });
  return utils;
}

describe('Project360Page — التحميل الأوّل والصحّة', () => {
  it('يُظهر حالة التحميل قبل وصول النظرة العامّة', () => {
    renderWorkspace();
    expect(screen.getByText('يتم تحميل مساحة عمل المشروع…')).toBeInTheDocument();
  });

  it('يستهلك نداءً واحدًا فقط للنظرة العامّة في التحميل الأوّل', async () => {
    await renderLoaded();
    expect(countOf(getCalls, OVERVIEW_URL)).toBe(1);
  });

  it('لا يطلب أيّ مورد تبويب قبل فتح تبويبه (تحميل كسول)', async () => {
    await renderLoaded();
    const eager = [OBJECTIVES_URL, KPIS_URL, STRATEGY_URL, SCHEMA_URL, DELIVERABLES_URL, RISKS_URL];
    for (const url of eager) expect(getCalls).not.toContain(url);
  });

  it('يعرض الصحّة كما وردت من الخادم بلا أيّ احتساب في العميل', async () => {
    await renderLoaded();
    // القيمة المعروضة هي 42.5 القادمة من الـAPI، لا 90 التي كانت لتنتج لو أعاد العميل الاحتساب.
    expect(screen.getByTestId('health-score')).toHaveTextContent(formatPercent(42.5));
    expect(screen.getByTestId('health-score')).not.toHaveTextContent(formatPercent(90));
    // والحالة «سليم» رغم أنّ 42.5 عتبتها حمراء ⟹ لا عتبات مطبَّقة في الواجهة.
    expect(screen.getByText('سليم')).toBeInTheDocument();
  });

  it('يعرض أسباب الصحّة المشتقّة من الاستجابة كما هي', async () => {
    await renderLoaded();
    const reasons = screen.getByTestId('health-reasons');
    expect(within(reasons).getByText(/نتيجة المؤشّرات الموزونة دون العتبة الخضراء/)).toBeInTheDocument();
    expect(within(reasons).getByText(/التقدّم الفعليّ متأخّر عن التقدّم المتوقَّع/)).toBeInTheDocument();
    expect(within(reasons).queryAllByRole('listitem')).toHaveLength(2);
  });

  it('يعرض المكوّن المستبعَد بوسم «مستبعَد» لا بصفر', async () => {
    getBodies[OVERVIEW_URL] = {
      ...overviewFixture(),
      health: { ...overviewFixture().health, kpiScore: null, scheduleScore: null },
    };
    await renderLoaded();
    expect(screen.getAllByText('— مستبعَد')).toHaveLength(2);
  });
});

describe('Project360Page — التبويبات', () => {
  it('يعرض تبويبات R2 الثمانية فقط', async () => {
    await renderLoaded();
    const names = screen.getAllByRole('tab').map((t) => t.textContent);
    expect(names).toEqual([
      'النظرة العامّة',
      'الموجز والسياق',
      'الاستراتيجيّة',
      'الأهداف',
      'المؤشّرات والقراءات',
      'المخرَجات التعاقديّة',
      'تحديثات التنفيذ',
      'القرارات والحوكمة',
    ]);
  });

  it('لا يعرض تبويبات المستندات أو المهامّ أو CRM أو المالية', async () => {
    await renderLoaded();
    for (const forbidden of [/مستندات/, /مهامّ|مهام/, /CRM/i, /مالي/]) {
      expect(screen.queryByRole('tab', { name: forbidden })).not.toBeInTheDocument();
    }
  });

  it('يطلب مورد الأهداف عند فتح تبويبه فقط، ومرّة واحدة', async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole('tab', { name: 'الأهداف' }));
    await waitFor(() => expect(countOf(getCalls, OBJECTIVES_URL)).toBe(1));
    expect(getCalls).not.toContain(KPIS_URL);
  });

  it('يطلب الاستراتيجيّة ومخطَّطها عند فتح تبويبها فقط', async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole('tab', { name: 'الاستراتيجيّة' }));
    await waitFor(() => expect(getCalls).toContain(SCHEMA_URL));
    expect(getCalls).toContain(STRATEGY_URL);
    expect(getCalls).not.toContain(DELIVERABLES_URL);
  });

  it('لا يعيد طلب النظرة العامّة عند التنقّل بين التبويبات', async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole('tab', { name: 'الأهداف' }));
    await waitFor(() => expect(countOf(getCalls, OBJECTIVES_URL)).toBe(1));
    fireEvent.click(screen.getByRole('tab', { name: 'النظرة العامّة' }));
    expect(countOf(getCalls, OVERVIEW_URL)).toBe(1);
  });
});

// ---------------------------------------------------------------------
// خريطة القدرات مصدرًا وحيدًا (§12).
//
// كلّ ادّعاء هنا يغيّر **حمولة الخادم** لا أدوار المستخدم: هذا هو الفرق الذي تحرسه
// التذكرة. لو عادت الصفحة إلى الاشتقاق المحلّيّ لسقطت هذه الاختبارات، لأنّ الأدوار
// لم تعد تُموَّه أصلًا.
// ---------------------------------------------------------------------
function overviewWithAccess(access: Partial<ProjectOverviewDto['access']>): ProjectOverviewDto {
  const base = overviewFixture();
  return { ...base, access: { ...base.access, ...access } };
}

describe('Project360Page — خريطة القدرات وإعادة الاحتساب', () => {
  it('يعرض زرّ إعادة الاحتساب حين تسمح خريطة القدرات ويستدعي المسار المخصَّص', async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole('button', { name: 'إعادة احتساب الصحّة' }));
    await waitFor(() => expect(countOf(postCalls, RECOMPUTE_URL)).toBe(1));
  });

  it('يُبطل النظرة العامّة بعد نجاح إعادة الاحتساب', async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole('button', { name: 'إعادة احتساب الصحّة' }));
    await waitFor(() => expect(countOf(getCalls, OVERVIEW_URL)).toBe(2));
  });

  it('يُخفي زرّ إعادة الاحتساب حين ينفي الخادم `canOperate`', async () => {
    getBodies[OVERVIEW_URL] = overviewWithAccess({ canOperate: false, canManageStructure: false });
    await renderLoaded();
    expect(screen.queryByRole('button', { name: 'إعادة احتساب الصحّة' })).not.toBeInTheDocument();
  });

  it('يُظهر الزرّ بـ`canOperate` وحدها ولو نفى الخادم الكتابة البنيويّة', async () => {
    getBodies[OVERVIEW_URL] = overviewWithAccess({ canOperate: true, canManageStructure: false });
    await renderLoaded();
    expect(screen.getByRole('button', { name: 'إعادة احتساب الصحّة' })).toBeInTheDocument();
  });

  it('يُخفي أزرار إنشاء الأهداف حين ينفي الخادم `canManageStructure`', async () => {
    getBodies[OVERVIEW_URL] = overviewWithAccess({ canManageStructure: false });
    await renderLoaded();
    fireEvent.click(screen.getByRole('tab', { name: 'الأهداف' }));
    await waitFor(() => expect(countOf(getCalls, OBJECTIVES_URL)).toBe(1));
    expect(screen.queryByRole('button', { name: 'هدف جديد' })).not.toBeInTheDocument();
  });
});

describe('Project360Page — ربط رموز الأخطاء', () => {
  it('يترجم 404 إلى رسالة «غير موجود أو خارج النطاق» بلا كشف الوجود', async () => {
    getFailures[OVERVIEW_URL] = axiosLikeError(404, 'project.not_found', 'المشروع غير موجود.');
    renderWorkspace();
    expect(await screen.findByText('المشروع غير موجود')).toBeInTheDocument();
    expect(
      screen.getByText('المشروع غير موجود أو أنّه خارج نطاق صلاحيّتك.'),
    ).toBeInTheDocument();
  });

  it('يترجم 403 داخل تبويب إلى رسالة نقص صلاحيّة لا إلى «غير موجود»', async () => {
    getFailures[OBJECTIVES_URL] = axiosLikeError(403, 'auth.forbidden', 'ممنوع.');
    await renderLoaded();
    fireEvent.click(screen.getByRole('tab', { name: 'الأهداف' }));
    expect(await screen.findByText('لا تملك صلاحيّة هذا الإجراء')).toBeInTheDocument();
    expect(screen.queryByText('المشروع غير موجود')).not.toBeInTheDocument();
  });
});

describe('Project360Page — العرض', () => {
  it('يصيّر مساحة العمل باتّجاه RTL', async () => {
    const { container } = await renderLoaded();
    expect(container.querySelector('[dir="rtl"]')).not.toBeNull();
  });

  it('يعرض ملخّصات النظرة العامّة من نفس الحمولة بلا نداء إضافيّ', async () => {
    await renderLoaded();
    expect(screen.getByText('عميل الاختبار')).toBeInTheDocument();
    expect(getCalls).toEqual([OVERVIEW_URL]);
  });

  it('يعرض المسنَدين بأسمائهم لا بمعرّفاتهم', async () => {
    await renderLoaded();
    for (const name of ['سارة العتيبي', 'ماجد الحربي', 'ريم القحطاني', 'فريق المحتوى']) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
    expect(screen.queryByText('user-owner')).not.toBeInTheDocument();
  });

  it('يعرض نسبة التقدّم مقرونة بطريقة احتسابها وعدد مخرَجات مصدرها (§6-1)', async () => {
    await renderLoaded();
    expect(screen.getByText('طريقة الاحتساب')).toBeInTheDocument();
    expect(screen.getByText('متوسّط موزون بأوزان المخرَجات')).toBeInTheDocument();
    expect(screen.getByText('مخرَجات المصدر')).toBeInTheDocument();
  });

  it('يُنذر صراحةً حين يسقط الاحتساب إلى الأوزان المتساوية', async () => {
    getBodies[OVERVIEW_URL] = {
      ...overviewFixture(),
      project: { ...overviewFixture().project, progressMode: 'EqualWeightFallback' },
    };
    await renderLoaded();
    expect(screen.getByText(/الرقم تقديريّ حتّى/)).toBeInTheDocument();
  });

  it('لا يُنذر حين يكون الاحتساب موزونًا', async () => {
    await renderLoaded();
    expect(screen.queryByText(/الرقم تقديريّ حتّى/)).not.toBeInTheDocument();
  });
});

// ======================================================================
// Project 360 — اختبارات تبويبات التفاصيل (CPW-R3 · R2-W12 · §12)
//
// كلّ تبويب يُصيَّر منفردًا فوق `api` متجسَّس، فتُقاس **الحمولة والمسار الفعليّين**
// لا مجرّد استدعاء هوك مموَّه. الادّعاء الجوهريّ في هذا الملفّ: العقود المُقرَّرة
// (الإنشاء تحت هدف حصرًا، الكتالوج مصدر الحقول، القراءة اليدويّة للمصدر اليدويّ)
// تُطبَّق في الواجهة كما في الخادم.
// ======================================================================

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactElement } from 'react';
import { api } from '../../lib/api';
import type {
  ProjectContractDeliverableDto,
  ProjectKpiDto,
  ProjectObjectiveDto,
  ProjectStrategyDto,
  ProjectStrategySchemaDto,
} from '../../types/project360';
import { ProjectContractDeliverablesTab } from './ProjectContractDeliverablesTab';
import { ProjectKpisTab } from './ProjectKpisTab';
import { ProjectObjectivesTab } from './ProjectObjectivesTab';
import { ProjectStrategyTab } from './ProjectStrategyTab';
import type { Project360Access } from './shared';

const P = 'project-1';
const OBJ = 'objective-1';

const MANAGER: Project360Access = { canManage: true, canOperate: true };
const OPERATOR: Project360Access = { canManage: false, canOperate: true };
const READER: Project360Access = { canManage: false, canOperate: false };

// ---------------------------------------------------------------------
// تجسّس الشبكة — يسجّل المسار والحمولة لكلّ فعل.
// ---------------------------------------------------------------------
type Call = { url: string; body?: unknown };

let getBodies: Record<string, unknown>;
let posts: Call[] = [];
let puts: Call[] = [];
let patches: Call[] = [];
let deletes: Call[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  posts = [];
  puts = [];
  patches = [];
  deletes = [];
  getBodies = {};

  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: url in getBodies ? getBodies[url] : [] } as never),
  );
  vi.spyOn(api, 'post').mockImplementation((url: string, body?: unknown) => {
    posts.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
  vi.spyOn(api, 'put').mockImplementation((url: string, body?: unknown) => {
    puts.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
  vi.spyOn(api, 'patch').mockImplementation((url: string, body?: unknown) => {
    patches.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
  vi.spyOn(api, 'delete').mockImplementation((url: string) => {
    deletes.push({ url });
    return Promise.resolve({ data: {} } as never);
  });
});

function renderTab(el: ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{el}</QueryClientProvider>);
}

// ---------------------------------------------------------------------
// عيّنات.
// ---------------------------------------------------------------------
function objective(over: Partial<ProjectObjectiveDto> = {}): ProjectObjectiveDto {
  return {
    id: OBJ,
    projectId: P,
    workstreamId: null,
    name: 'رفع الوعي بالعلامة',
    description: 'وصف الهدف',
    priority: 'High',
    weight: 2,
    normalizedWeight: 0.4,
    status: 'InProgress',
    startDate: '2026-01-01',
    dueDate: '2026-06-30',
    ownerUserId: null,
    ownerFullName: null,
    notes: null,
    sortOrder: 1,
    isActive: true,
    progressPercent: 60,
    kpiCount: 2,
    computedKpiCount: 1,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
    ...over,
  };
}

function kpi(over: Partial<ProjectKpiDto> = {}): ProjectKpiDto {
  return {
    id: 'kpi-1',
    projectId: P,
    objectiveId: OBJ,
    name: 'الوصول الشهريّ',
    description: null,
    category: 'Marketing',
    unit: 'Number',
    customUnitLabel: null,
    direction: 'HigherIsBetter',
    frequency: 'Monthly',
    baselineValue: null,
    targetValue: 1000,
    currentValue: 700,
    lastReadingDate: '2026-08-01',
    weight: 1,
    sourceType: 'Manual',
    externalSourceKey: null,
    externalMetricCode: null,
    lastSyncedAtUtc: null,
    notes: null,
    sortOrder: 1,
    isActive: true,
    achievementPercent: 70,
    variance: -300,
    trend: 'Up',
    ownerUserId: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
    ...over,
  };
}

function deliverable(over: Partial<ProjectContractDeliverableDto> = {}): ProjectContractDeliverableDto {
  return {
    id: 'del-1',
    projectId: P,
    objectiveId: null,
    workstreamId: null,
    deliverableTypeCode: 'monthly_report',
    deliverableTypeNameAr: 'تقرير شهريّ',
    name: 'تقرير أغسطس',
    description: null,
    plannedQuantity: 1,
    completedQuantity: 0,
    status: 'InProgress',
    progressPercent: 30,
    startDate: null,
    dueDate: '2026-08-31',
    deliveredAtUtc: null,
    priority: 'Medium',
    ownerUserId: null,
    ownerFullName: null,
    notes: null,
    sortOrder: 1,
    isActive: true,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
    ...over,
  };
}

// رموز الكتالوج هنا مُختلَقة عمدًا: لو كانت الواجهة تعرف أسماء أقسام مثبَّتة لَما ظهرت هذه.
const schema: ProjectStrategySchemaDto = {
  coreFields: [
    { fieldCode: 'vision', nameAr: 'الرؤية', isCore: true, sectionCode: null, sectionNameAr: null, sortOrder: 1 },
    {
      fieldCode: 'strategy_summary',
      nameAr: 'ملخّص الاستراتيجيّة',
      isCore: true,
      sectionCode: null,
      sectionNameAr: null,
      sortOrder: 2,
    },
  ],
  dynamicFields: [
    {
      fieldCode: 'catalog_field_alpha',
      nameAr: 'حقل الكتالوج ألفا',
      isCore: false,
      sectionCode: 'sec_alpha',
      sectionNameAr: 'قسم ألفا',
      sortOrder: 1,
    },
    {
      fieldCode: 'catalog_field_orphan',
      nameAr: 'حقل بلا قسم',
      isCore: false,
      sectionCode: null,
      sectionNameAr: null,
      sortOrder: 2,
    },
  ],
  sections: [{ code: 'sec_alpha', nameAr: 'قسم ألفا', sortOrder: 1 }],
};

const strategy: ProjectStrategyDto = {
  id: 'strategy-1',
  projectId: P,
  vision: 'رؤية مسجّلة',
  strategySummary: null,
  targetAudience: null,
  customerPersona: null,
  positioning: null,
  valueProposition: null,
  competitors: null,
  toneOfVoice: null,
  messaging: null,
  marketingApproach: null,
  successFactors: null,
  attributes: [
    {
      fieldCode: 'catalog_field_alpha',
      fieldNameAr: 'حقل الكتالوج ألفا',
      sectionCode: 'sec_alpha',
      sectionNameAr: 'قسم ألفا',
      valueText: 'قيمة ألفا',
      sortOrder: 1,
    },
  ],
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: null,
};

// ---------------------------------------------------------------------

describe('تبويب الاستراتيجيّة — مبنيّ من الكتالوج', () => {
  beforeEach(() => {
    getBodies[`/projects/${P}/strategy`] = strategy;
    getBodies[`/projects/${P}/strategy/schema`] = schema;
  });

  it('يبني الأقسام والحقول من المخطَّط وحده', async () => {
    renderTab(<ProjectStrategyTab projectId={P} access={MANAGER} />);
    expect(await screen.findByText('قسم ألفا')).toBeInTheDocument();
    expect(screen.getByText('حقل الكتالوج ألفا')).toBeInTheDocument();
    expect(screen.getByText('الرؤية')).toBeInTheDocument();
    expect(screen.getByText('قيمة ألفا')).toBeInTheDocument();
  });

  it('يجمع الحقل بلا قسم تحت «أخرى» بدل إسقاطه', async () => {
    renderTab(<ProjectStrategyTab projectId={P} access={MANAGER} />);
    expect(await screen.findByText('أخرى')).toBeInTheDocument();
    expect(screen.getByText('حقل بلا قسم')).toBeInTheDocument();
  });

  it('يحوّل رمز حقل النواة إلى مفتاح camelCase عند الحفظ', async () => {
    renderTab(<ProjectStrategyTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'تعديل' }));
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0].url).toBe(`/projects/${P}/strategy`);
    const body = puts[0].body as Record<string, unknown>;
    expect(body).toHaveProperty('strategySummary');
    expect(body).not.toHaveProperty('strategy_summary');
    expect(body.vision).toBe('رؤية مسجّلة');
  });

  it('يرسل السمات الديناميكيّة غير الفارغة فقط', async () => {
    renderTab(<ProjectStrategyTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'تعديل' }));
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(puts).toHaveLength(1));
    const attrs = (puts[0].body as { attributes: { fieldCode: string }[] }).attributes;
    expect(attrs.map((a) => a.fieldCode)).toEqual(['catalog_field_alpha']);
  });

  it('يُخفي زرّ التحرير عن غير الإداريّين', async () => {
    renderTab(<ProjectStrategyTab projectId={P} access={OPERATOR} />);
    expect(await screen.findByText('قسم ألفا')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
  });
});

describe('تبويب الأهداف — دورة الحياة الكاملة', () => {
  it('يعرض حالة الفراغ حين لا أهداف', async () => {
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    expect(await screen.findByText('لا توجد أهداف')).toBeInTheDocument();
  });

  it('ينشئ هدفًا على مسار المشروع بالحمولة المُدخَلة', async () => {
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'هدف جديد' }));
    fireEvent.change(screen.getByLabelText('الاسم'), { target: { value: 'هدف جديد للاختبار' } });
    fireEvent.change(screen.getByLabelText(/الوزن/), { target: { value: '3' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0].url).toBe(`/projects/${P}/objectives`);
    expect(posts[0].body).toMatchObject({ name: 'هدف جديد للاختبار', weight: 3 });
  });

  it('يعدّل الهدف على مسار المعرّف', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'تعديل' }));
    fireEvent.change(screen.getByLabelText('الاسم'), { target: { value: 'اسم معدَّل' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0].url).toBe(`/projects/${P}/objectives/${OBJ}`);
    expect(puts[0].body).toMatchObject({ name: 'اسم معدَّل' });
  });

  it('يغيّر الحالة على مسار الحالة المخصَّص', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    const select = await screen.findByLabelText('حالة الهدف رفع الوعي بالعلامة');
    fireEvent.change(select, { target: { value: 'Completed' } });
    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0].url).toBe(`/projects/${P}/objectives/${OBJ}/status`);
    expect(patches[0].body).toEqual({ status: 'Completed' });
  });

  it('يحذف الهدف على مسار المعرّف', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'حذف' }));
    await waitFor(() => expect(deletes).toHaveLength(1));
    expect(deletes[0].url).toBe(`/projects/${P}/objectives/${OBJ}`);
  });

  it('يعرض الوزن المطبَّع كنسبة عرض من قيمة الخادم 0..1', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective({ normalizedWeight: 0.4 })];
    renderTab(<ProjectObjectivesTab projectId={P} access={MANAGER} />);
    const label = await screen.findByText('الوزن المطبَّع');
    expect(label.parentElement?.textContent).toContain('٤٠');
  });

  it('يُظهر منتقي الحالة للمسؤول التشغيليّ ويُخفي أزرار الإدارة', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    renderTab(<ProjectObjectivesTab projectId={P} access={OPERATOR} />);
    expect(await screen.findByLabelText('حالة الهدف رفع الوعي بالعلامة')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'هدف جديد' })).not.toBeInTheDocument();
  });

  it('يُخفي كلّ أدوات التعديل عن القارئ', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    renderTab(<ProjectObjectivesTab projectId={P} access={READER} />);
    expect(await screen.findByText('رفع الوعي بالعلامة')).toBeInTheDocument();
    expect(screen.queryByLabelText('حالة الهدف رفع الوعي بالعلامة')).not.toBeInTheDocument();
  });
});

describe('تبويب المؤشّرات — الإنشاء تحت هدف حصرًا', () => {
  it('يمنع الإنشاء بلا هدف ويشرح السبب', async () => {
    renderTab(<ProjectKpisTab projectId={P} access={MANAGER} />);
    expect(await screen.findByText('لا توجد أهداف بعد')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /مؤشّر جديد/ })).not.toBeInTheDocument();
  });

  it('ينشئ المؤشّر على مسار الهدف لا على مسار المشروع', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    getBodies[`/projects/${P}/kpis`] = [];
    renderTab(<ProjectKpisTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'مؤشّر جديد تحت هذا الهدف' }));
    fireEvent.change(screen.getByLabelText('الاسم'), { target: { value: 'مؤشّر الاختبار' } });
    fireEvent.change(screen.getByLabelText('القيمة المستهدَفة'), { target: { value: '500' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0].url).toBe(`/projects/${P}/objectives/${OBJ}/kpis`);
    expect(posts[0].url).not.toBe(`/projects/${P}/kpis`);
    expect(posts[0].body).toMatchObject({ name: 'مؤشّر الاختبار', targetValue: 500 });
  });

  it('يتيح تسجيل قراءة يدويّة لمؤشّر مصدره Manual', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    getBodies[`/projects/${P}/kpis`] = [kpi()];
    renderTab(<ProjectKpisTab projectId={P} access={OPERATOR} />);
    fireEvent.click(await screen.findByRole('button', { name: 'القراءات' }));
    fireEvent.change(await screen.findByLabelText('تاريخ القراءة'), {
      target: { value: '2026-08-15' },
    });
    fireEvent.change(screen.getByLabelText('القيمة'), { target: { value: '820' } });
    fireEvent.click(screen.getByRole('button', { name: 'تسجيل قراءة' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0].url).toBe(`/projects/${P}/objectives/${OBJ}/kpis/kpi-1/readings`);
    expect(posts[0].body).toEqual({ readingDate: '2026-08-15', value: 820 });
  });

  it('يمنع القراءة اليدويّة لمؤشّر مشتقّ من المهامّ', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    getBodies[`/projects/${P}/kpis`] = [kpi({ sourceType: 'TaskDerived' })];
    renderTab(<ProjectKpisTab projectId={P} access={OPERATOR} />);
    fireEvent.click(await screen.findByRole('button', { name: 'القراءات' }));
    // لوحة القراءات مفتوحة فعلًا (نصّ الفراغ ظاهر) ومع ذلك لا نموذج تسجيل.
    expect(await screen.findByText('لا توجد قراءات مسجّلة.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل قراءة' })).not.toBeInTheDocument();
  });

  it('يمنع القراءة اليدويّة عن القارئ حتّى لو كان المصدر يدويًّا', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    getBodies[`/projects/${P}/kpis`] = [kpi()];
    renderTab(<ProjectKpisTab projectId={P} access={READER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'القراءات' }));
    expect(screen.queryByRole('button', { name: 'تسجيل قراءة' })).not.toBeInTheDocument();
  });

  it('يعرض نسبة التحقّق كما وردت من الخادم بلا إعادة احتساب', async () => {
    getBodies[`/projects/${P}/objectives`] = [objective()];
    // 700 من 1000 كانت لتعطي ٧٠٪ لو احتسب العميل؛ الخادم أرسل ٢٥٪ فهي المعروضة.
    getBodies[`/projects/${P}/kpis`] = [kpi({ achievementPercent: 25 })];
    renderTab(<ProjectKpisTab projectId={P} access={MANAGER} />);
    const row = (await screen.findByText('الوصول الشهريّ')).closest('tr')!;
    expect(within(row).getByText('٢٥٪')).toBeInTheDocument();
    expect(within(row).queryByText('٧٠٪')).not.toBeInTheDocument();
  });
});

describe('تبويب المخرَجات التعاقديّة', () => {
  it('يفصل التسمية عن مخرَجات مسارات العمل', async () => {
    renderTab(<ProjectContractDeliverablesTab projectId={P} access={MANAGER} />);
    expect(
      await screen.findByText(
        'التزامات تعاقديّة تجاه العميل — مستقلّة عن مخرَجات مسارات العمل داخل صفحة المشروع.',
      ),
    ).toBeInTheDocument();
  });

  it('يبني منتقي النوع من الكتالوج لا من رموز مثبَّتة', async () => {
    getBodies[`/projects/${P}/contract-deliverables/types`] = [
      { code: 'catalog_type_x', nameAr: 'نوع الكتالوج س', sortOrder: 1 },
    ];
    renderTab(<ProjectContractDeliverablesTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'مخرَج تعاقديّ جديد' }));
    expect(await screen.findByRole('option', { name: 'نوع الكتالوج س' })).toBeInTheDocument();
  });

  it('ينشئ المخرَج برمز النوع المختار', async () => {
    getBodies[`/projects/${P}/contract-deliverables/types`] = [
      { code: 'catalog_type_x', nameAr: 'نوع الكتالوج س', sortOrder: 1 },
    ];
    renderTab(<ProjectContractDeliverablesTab projectId={P} access={MANAGER} />);
    fireEvent.click(await screen.findByRole('button', { name: 'مخرَج تعاقديّ جديد' }));
    fireEvent.change(await screen.findByLabelText(/نوع المخرَج/), {
      target: { value: 'catalog_type_x' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0].url).toBe(`/projects/${P}/contract-deliverables`);
    expect(posts[0].body).toMatchObject({ deliverableTypeCode: 'catalog_type_x' });
  });

  it('يحدّث الحالة على مسار التقدّم المخصَّص', async () => {
    getBodies[`/projects/${P}/contract-deliverables`] = [deliverable()];
    renderTab(<ProjectContractDeliverablesTab projectId={P} access={OPERATOR} />);
    fireEvent.change(await screen.findByLabelText('حالة المخرَج تقرير أغسطس'), {
      target: { value: 'Delivered' },
    });
    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0].url).toBe(`/projects/${P}/contract-deliverables/del-1/progress`);
    expect(patches[0].body).toMatchObject({ status: 'Delivered', progressPercent: 30 });
  });

  it('يُخفي أدوات التحديث عن القارئ', async () => {
    getBodies[`/projects/${P}/contract-deliverables`] = [deliverable()];
    renderTab(<ProjectContractDeliverablesTab projectId={P} access={READER} />);
    expect(await screen.findByText('تقرير أغسطس')).toBeInTheDocument();
    expect(screen.queryByLabelText('حالة المخرَج تقرير أغسطس')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'مخرَج تعاقديّ جديد' })).not.toBeInTheDocument();
  });
});

// ======================================================================
// PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2 — اختبارات الواجهة
//
// تُثبِت أنّ الموظّف يسجّل عدّة بنود عمل داخل بطاقة المشروع الواحدة بلا تكرار المشروع،
// وأنّ محاولة تكرار المشروع تُنتج **رسالة واحدة** موجَّهة إلى البطاقة القائمة لا Toasts متتابعة،
// وأنّ بنود العمل لا تُفقَد في رحلة تفكيك/عرض القيم (وهو مسار فقدان بيانات المسودّة).
// ======================================================================

import { useState } from 'react';
import { render as rtlRender, screen, fireEvent, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  parseRepeatableConfig,
  parseRepeatableEntries,
  ProjectRepeatableEditor,
  ProjectRepeatableDisplay,
} from './SubmissionsPage';
import type { ProjectDto, ProjectRepeatableConfig, ProjectRepeatableEntry } from '../types/api';

function render(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return rtlRender(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const PROJECT_A = 'p-a';
const PROJECT_B = 'p-b';

function project(id: string, name: string): ProjectDto {
  return {
    id, clientId: 'c1', clientName: 'عميل', name, serviceType: 'Social', status: 'Active',
    startDate: null, endDate: null, ownerTeamId: null, ownerTeamName: null,
    accountManagerId: null, accountManagerName: null, notes: null,
    createdAtUtc: '2026-08-01T00:00:00Z', updatedAtUtc: null,
    canHardDelete: true, deleteBlockReason: null,
  } as ProjectDto;
}

const PROJECTS = [project(PROJECT_A, 'مشروع أ'), project(PROJECT_B, 'مشروع ب')];

// قالب v2: حقل على مستوى المشروع + مجموعة بنود عمل بتسميات من القالب وحده.
const CONFIG_V2_JSON = JSON.stringify({
  schemaVersion: 2,
  projectRequired: true,
  minProjects: 1,
  maxProjects: 0,
  fields: [{ key: 'work_status', label: 'حالة العمل', type: 'Text', required: true }],
  workItems: {
    key: 'work_items',
    label: 'بنود العمل',
    itemLabel: 'بند عمل',
    addLabel: '+ إضافة بند عمل',
    minItems: 1,
    maxItems: 0,
    uniqueBy: [],
    fields: [{ key: 'work_type', label: 'نوع العمل', type: 'Text', required: true }],
  },
});

// قالب v1 حرفيًّا — لا يعرف بنود العمل إطلاقًا.
const CONFIG_V1_JSON = JSON.stringify({
  projectRequired: true, minProjects: 1, maxProjects: 5,
  fields: [{ key: 'work_type', label: 'نوع العمل', type: 'Text', required: true }],
});

// غلاف بحالة حقيقيّة: المحرّر مضبوط بالكامل من الأب، فاختباره بلا حالة يقيس لا شيء.
function Harness({ config, initial }: { config: ProjectRepeatableConfig; initial: ProjectRepeatableEntry[] }) {
  const [entries, setEntries] = useState<ProjectRepeatableEntry[]>(initial);
  return (
    <div dir="rtl">
      <ProjectRepeatableEditor
        config={config}
        entries={entries}
        projects={PROJECTS}
        allProjects={PROJECTS}
        onChange={setEntries}
      />
      <pre data-testid="state">{JSON.stringify(entries)}</pre>
    </div>
  );
}

let consoleErrors: unknown[][] = [];

beforeEach(() => {
  consoleErrors = [];
  vi.spyOn(console, 'error').mockImplementation((...args) => { consoleErrors.push(args); });
});

afterEach(() => {
  // أيّ خطأ Console يُبطِل شرط «BROWSER_CONSOLE_ERRORS = 0» المطلوب في التذكرة.
  expect(consoleErrors).toEqual([]);
  vi.restoreAllMocks();
});

function state(): ProjectRepeatableEntry[] {
  return JSON.parse(screen.getByTestId('state').textContent || '[]');
}

describe('R2 — تفكيك القالب والقيم', () => {
  // ---- 1: تعريف بنود العمل يصل من القالب بتسمياته لا بتسمية مثبَّتة في الكود ----
  it('1 parseRepeatableConfig يحافظ على مجموعة بنود العمل وتسمياتها', () => {
    const cfg = parseRepeatableConfig(CONFIG_V2_JSON);
    expect(cfg.schemaVersion).toBe(2);
    expect(cfg.workItems?.label).toBe('بنود العمل');
    expect(cfg.workItems?.addLabel).toBe('+ إضافة بند عمل');
    expect(cfg.workItems?.fields).toHaveLength(1);
    expect(cfg.workItems?.fields[0].label).toBe('نوع العمل');
  });

  // ---- 2: قالب v1 يبقى بلا مجموعة ⇒ سلوك حرفيّ كما كان ----
  it('2 قالب v1 لا يكتسب مجموعة بنود عمل', () => {
    expect(parseRepeatableConfig(CONFIG_V1_JSON).workItems).toBeUndefined();
  });

  // ---- 3: مجموعة بلا حقول لا تُفعَّل (بطاقة خاوية لا يملؤها أحد) ----
  it('3 مجموعة بنود عمل بلا حقول تُهمَل', () => {
    const json = JSON.stringify({ projectRequired: true, minProjects: 1, maxProjects: 0, fields: [], workItems: { fields: [] } });
    expect(parseRepeatableConfig(json).workItems).toBeUndefined();
  });

  // ---- 4: بنود العمل تنجو من دورة تفكيك القيم (مسار فقدان بيانات المسودّة) ----
  it('4 parseRepeatableEntries يحافظ على بنود العمل', () => {
    const json = JSON.stringify([
      { projectId: PROJECT_A, answers: { work_status: 'مكتمل' }, workItems: [{ answers: { work_type: 'كاروسيل' } }, { answers: { work_type: 'ريل' } }] },
    ]);
    const entries = parseRepeatableEntries(json);
    expect(entries[0].workItems).toHaveLength(2);
    expect(entries[0].workItems?.[1].answers.work_type).toBe('ريل');
  });

  // ---- 5: بيانات v1 لا تكتسب مفتاحًا جديدًا ----
  it('5 عنصر v1 يبقى بلا مفتاح workItems', () => {
    const entries = parseRepeatableEntries(JSON.stringify([{ projectId: PROJECT_A, answers: { work_type: 'مقال' } }]));
    expect(entries[0].workItems).toBeUndefined();
    expect(JSON.stringify(entries[0])).not.toContain('workItems');
  });

  // ---- 5أ: انحدار — قيمة JSON غير نصّيّة يقبلها الخادم كانت تُسقِط المحرّر عند `trim()` ----
  it('5أ القيم الرقميّة والمنطقيّة والغائبة تُقرأ نصًّا بلا انهيار', () => {
    const json = JSON.stringify([
      {
        projectId: PROJECT_A,
        answers: { count: 3, done: true, note: null },
        workItems: [{ answers: { count: 5, ratio: 2.5 } }],
      },
    ]);
    const entries = parseRepeatableEntries(json);
    expect(entries[0].answers.count).toBe('3');
    expect(entries[0].answers.done).toBe('true');
    expect(entries[0].answers.note).toBe('');
    expect(entries[0].workItems?.[0].answers.count).toBe('5');
    expect(entries[0].workItems?.[0].answers.ratio).toBe('2.5');
    // الشرط الحقيقيّ: كلّ قيمة قابلة لاستدعاء `trim()` كما يفترض المحرّر.
    for (const v of Object.values(entries[0].workItems![0].answers)) {
      expect(typeof v).toBe('string');
    }
  });
});

describe('R2 — محرّر بنود العمل', () => {
  // ---- 6: إضافة مشروع ثمّ عدّة بنود عمل داخل البطاقة نفسها بلا تكرار المشروع ----
  it('6 عدّة بنود عمل داخل بطاقة مشروع واحدة', () => {
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={[]} />);

    fireEvent.click(screen.getByRole('button', { name: '+ إضافة مشروع' }));
    expect(state()).toHaveLength(1);
    // القالب يعلن minItems=1 ⇒ البطاقة الجديدة تولد ببند عمل واحد جاهز للتعبئة.
    expect(state()[0].workItems).toHaveLength(1);

    fireEvent.click(screen.getByRole('button', { name: '+ إضافة بند عمل' }));
    fireEvent.click(screen.getByRole('button', { name: '+ إضافة بند عمل' }));

    expect(state()).toHaveLength(1);            // المشروع لم يتكرّر
    expect(state()[0].workItems).toHaveLength(3);
    expect(screen.getAllByText(/^بند عمل \d$/)).toHaveLength(3);
  });

  // ---- 7: حذف بند عمل لا يحذف المشروع ----
  it('7 حذف بند عمل يُبقي المشروع وبقيّة البنود', () => {
    const initial: ProjectRepeatableEntry[] = [{
      projectId: PROJECT_A, answers: {},
      workItems: [{ answers: { work_type: 'كاروسيل' } }, { answers: { work_type: 'ريل' } }],
    }];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);

    fireEvent.click(screen.getAllByRole('button', { name: 'حذف بند عمل' })[0]);

    expect(state()).toHaveLength(1);
    expect(state()[0].projectId).toBe(PROJECT_A);
    expect(state()[0].workItems).toHaveLength(1);
    expect(state()[0].workItems?.[0].answers.work_type).toBe('ريل');
  });

  // ---- 8: تحذير قبل حذف مشروع يحمل بنود عمل، والإلغاء لا يفقد شيئًا ----
  it('8 إلغاء التحذير يُبقي المشروع وبنوده', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const initial: ProjectRepeatableEntry[] = [{ projectId: PROJECT_A, answers: {}, workItems: [{ answers: { work_type: 'ريل' } }] }];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);

    fireEvent.click(screen.getByRole('button', { name: 'حذف المشروع' }));

    expect(window.confirm).toHaveBeenCalledTimes(1);
    expect(state()).toHaveLength(1);
    expect(state()[0].workItems).toHaveLength(1);
  });

  // ---- 9: تكرار المشروع ⇒ رسالة واحدة فقط + عدم إفساد البطاقة الثانية ----
  it('9 اختيار مشروع مضاف مسبقًا يُنتج رسالة واحدة ولا يُغيّر الحالة', () => {
    const initial: ProjectRepeatableEntry[] = [
      { projectId: PROJECT_A, answers: {}, workItems: [{ answers: { work_type: 'كاروسيل' } }] },
      { projectId: null, answers: {}, workItems: [{ answers: {} }] },
    ];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);

    const selects = screen.getAllByRole('combobox');
    fireEvent.change(selects[1], { target: { value: PROJECT_A } });

    const alerts = screen.getAllByRole('alert');
    expect(alerts).toHaveLength(1);
    expect(alerts[0].textContent).toContain('هذا المشروع مضاف بالفعل داخل التقرير');
    // البطاقة الثانية لم تلتقط المشروع المكرّر، والأولى لم تُمَسّ.
    expect(state()[1].projectId).toBeNull();
    expect(state()[0].workItems).toHaveLength(1);
  });

  // ---- 10: تكرار المحاولة لا يُراكم الرسائل ----
  it('10 ثلاث محاولات تكرار ⇒ رسالة واحدة لا ثلاث', () => {
    const initial: ProjectRepeatableEntry[] = [
      { projectId: PROJECT_A, answers: {}, workItems: [{ answers: {} }] },
      { projectId: null, answers: {}, workItems: [{ answers: {} }] },
    ];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);
    const selects = screen.getAllByRole('combobox');

    fireEvent.change(selects[1], { target: { value: PROJECT_A } });
    fireEvent.change(selects[1], { target: { value: PROJECT_A } });
    fireEvent.change(selects[1], { target: { value: PROJECT_A } });

    expect(screen.getAllByRole('alert')).toHaveLength(1);
  });

  // ---- 11: اختيار مشروع مختلف يُزيل الرسالة ويثبت القيمة ----
  it('11 اختيار مشروع غير مكرّر يُزيل الرسالة', () => {
    const initial: ProjectRepeatableEntry[] = [
      { projectId: PROJECT_A, answers: {}, workItems: [{ answers: {} }] },
      { projectId: null, answers: {}, workItems: [{ answers: {} }] },
    ];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);
    const selects = screen.getAllByRole('combobox');

    fireEvent.change(selects[1], { target: { value: PROJECT_A } });
    expect(screen.queryAllByRole('alert')).toHaveLength(1);

    fireEvent.change(selects[1], { target: { value: PROJECT_B } });

    expect(screen.queryAllByRole('alert')).toHaveLength(0);
    expect(state()[1].projectId).toBe(PROJECT_B);
  });

  // ---- 12: تغيير المشروع لا يمسح بنود العمل المُدخَلة ----
  it('12 تبديل المشروع يحافظ على بنود العمل', () => {
    const initial: ProjectRepeatableEntry[] = [{ projectId: PROJECT_A, answers: {}, workItems: [{ answers: { work_type: 'كاروسيل' } }] }];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);

    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: PROJECT_B } });

    expect(state()[0].projectId).toBe(PROJECT_B);
    expect(state()[0].workItems?.[0].answers.work_type).toBe('كاروسيل');
  });

  // ---- 13: قالب v1 لا يعرض أيّ أثر لبنود العمل ----
  it('13 محرّر قالب v1 بلا مجموعة بنود عمل', () => {
    render(<Harness config={parseRepeatableConfig(CONFIG_V1_JSON)} initial={[]} />);
    fireEvent.click(screen.getByRole('button', { name: '+ إضافة مشروع' }));

    expect(screen.queryByRole('button', { name: '+ إضافة بند عمل' })).toBeNull();
    expect(state()[0].workItems).toBeUndefined();
  });

  // ---- 13أ: انحدار — فتح مسودّة مخزَّنة بقيمة رقميّة على حقل رقميّ مقيَّد لا يُسقِط المحرّر ----
  it('13أ المحرّر يفتح مسودّة بقيم رقميّة مخزَّنة بلا انهيار', () => {
    const cfgJson = JSON.stringify({
      schemaVersion: 2,
      projectRequired: true, minProjects: 1, maxProjects: 0,
      fields: [{ key: 'work_status', label: 'حالة العمل', type: 'Text', required: true }],
      workItems: {
        key: 'work_items', label: 'بنود العمل', itemLabel: 'بند عمل', addLabel: '+ إضافة بند عمل',
        minItems: 1, maxItems: 0, uniqueBy: [],
        fields: [{ key: 'count', label: 'العدد', type: 'Number', required: true, min: 1, max: 100, integerOnly: true }],
      },
    });
    // القيم كما يخزّنها الخادم فعلًا: `count` رقم JSON لا نصّ.
    const stored = JSON.stringify([{ projectId: PROJECT_A, answers: {}, workItems: [{ answers: { count: 3 } }] }]);

    render(<Harness config={parseRepeatableConfig(cfgJson)} initial={parseRepeatableEntries(stored)} />);

    expect(screen.getByDisplayValue('3')).toBeTruthy();
    expect(screen.queryAllByRole('alert')).toHaveLength(0);
  });

  // ---- 14: قيم بند العمل تُكتب في البند الصحيح لا في مستوى المشروع ----
  it('14 الكتابة في حقل بند العمل لا تلوّث إجابات المشروع', () => {
    const initial: ProjectRepeatableEntry[] = [{ projectId: PROJECT_A, answers: {}, workItems: [{ answers: {} }, { answers: {} }] }];
    render(<Harness config={parseRepeatableConfig(CONFIG_V2_JSON)} initial={initial} />);

    // ثلاثة حقول نصّيّة: حالة العمل (مستوى المشروع) ثمّ نوع العمل لكلّ بند.
    const inputs = screen.getAllByRole('textbox');
    fireEvent.change(inputs[2], { target: { value: 'ريل' } });

    expect(state()[0].answers.work_type).toBeUndefined();
    expect(state()[0].workItems?.[0].answers.work_type).toBeUndefined();
    expect(state()[0].workItems?.[1].answers.work_type).toBe('ريل');
  });
});

describe('R2 — عرض القراءة', () => {
  // ---- 15: بنود العمل تُعرض تحت بطاقة مشروعها بتسميات القالب ----
  it('15 العرض يُظهر كلّ بنود العمل داخل بطاقة المشروع', () => {
    const entries: ProjectRepeatableEntry[] = [{
      projectId: PROJECT_A, answers: { work_status: 'مكتمل' },
      workItems: [{ answers: { work_type: 'كاروسيل' } }, { answers: { work_type: 'ريل' } }],
    }];
    render(
      <ProjectRepeatableDisplay
        config={parseRepeatableConfig(CONFIG_V2_JSON)}
        entries={entries}
        projects={[{ id: PROJECT_A, name: 'مشروع أ', clientId: 'c1', clientName: 'عميل' }]}
      />,
    );

    expect(screen.getByText('بنود العمل')).toBeTruthy();
    expect(screen.getAllByText(/^بند عمل \d$/)).toHaveLength(2);
    expect(screen.getByText('كاروسيل')).toBeTruthy();
    expect(screen.getByText('ريل')).toBeTruthy();
  });

  // ---- 16: تقرير v1 يُعرض كما كان بلا أيّ قسم بنود عمل ----
  it('16 تقرير قديم يُعرض بلا مجموعة بنود عمل', () => {
    const entries: ProjectRepeatableEntry[] = [{ projectId: PROJECT_A, answers: { work_type: 'مقال' } }];
    render(
      <ProjectRepeatableDisplay
        config={parseRepeatableConfig(CONFIG_V1_JSON)}
        entries={entries}
        projects={[{ id: PROJECT_A, name: 'مشروع أ', clientId: 'c1', clientName: 'عميل' }]}
      />,
    );

    expect(screen.queryByText('بنود العمل')).toBeNull();
    expect(screen.getByText('مقال')).toBeTruthy();
  });

  // ---- 17: العرض لا يذكر إلّا المشروع الممرَّر إليه (عقد الشريحة) ----
  it('17 العرض لا يخترع بطاقات لمشاريع أخرى', () => {
    const entries: ProjectRepeatableEntry[] = [{
      projectId: PROJECT_A, answers: { work_status: 'مكتمل' }, workItems: [{ answers: { work_type: 'كاروسيل' } }],
    }];
    const { container } = render(
      <ProjectRepeatableDisplay
        config={parseRepeatableConfig(CONFIG_V2_JSON)}
        entries={entries}
        projects={[{ id: PROJECT_A, name: 'مشروع أ', clientId: 'c1', clientName: 'عميل' }]}
      />,
    );

    expect(within(container).queryByText(/مشروع ب/)).toBeNull();
    expect(container.textContent).not.toContain(PROJECT_B);
  });
});

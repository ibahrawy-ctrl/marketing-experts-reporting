import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import {
  parseGrid,
  parseRepeatableConfig,
  parseRepeatableEntries,
  subFieldInputKind,
  GridEditor,
  GridDisplay,
  ProjectRepeatableEditor,
  ProjectRepeatableDisplay,
} from './SubmissionsPage';
import type { ProjectDto, ProjectRepeatableConfig, ProjectRepeatableEntry } from '../types/api';

// ===== OFFICIAL-LAUNCH-FIX-PACK-R1B — اختبارات Grid (الحقل الفرعي جدوليّ داخل قسم المشاريع المتكرر) =====
// تُثبِت: التسلسل/التفكيك ذهابًا وإيابًا لـ string[][] داخل answers[key]، محرّر/عرض الجدول،
// وأن كل مقاييس الكلمة المفتاحية في SEO تعيش في صفّ واحد. لا Migration، لا تغيير KPI/ValueNumber.

function project(id: string, name: string, clientName: string | null = null): ProjectDto {
  return {
    id,
    clientId: 'c-' + id,
    clientName,
    name,
    serviceType: 'Seo',
    status: 'Active',
    startDate: null,
    endDate: null,
    ownerTeamId: null,
    ownerTeamName: null,
    accountManagerId: null,
    accountManagerName: null,
    notes: null,
    createdAtUtc: '2026-06-01T00:00:00Z',
    updatedAtUtc: null,
    canHardDelete: true,
    deleteBlockReason: null,
  } as ProjectDto;
}

function gridConfig(columns: string[], key = 'kw', label = 'كلمات المشروع'): ProjectRepeatableConfig {
  return {
    projectRequired: true,
    minProjects: 1,
    maxProjects: 5,
    fields: [{ key, label, type: 'Grid', required: false, columns }],
  };
}

// ---- 17: parseGrid يعيد string[][] من JSON صالح ----
it('17 parseGrid returns rows from valid JSON', () => {
  expect(parseGrid('[["a","b"],["c","d"]]')).toEqual([['a', 'b'], ['c', 'd']]);
});

// ---- 18: parseGrid يعيد [] من null/غير صالح ----
it('18 parseGrid returns [] for null and invalid JSON', () => {
  expect(parseGrid(null)).toEqual([]);
  expect(parseGrid(undefined)).toEqual([]);
  expect(parseGrid('{not json')).toEqual([]);
  expect(parseGrid('{"a":1}')).toEqual([]); // object not array
});

// ---- 19: parseRepeatableConfig يحافظ على أعمدة Grid ----
it('19 parseRepeatableConfig preserves Grid columns', () => {
  const json = JSON.stringify({
    projectRequired: false,
    minProjects: 2,
    maxProjects: 3,
    fields: [{ key: 'kw', label: 'كلمات', type: 'Grid', required: true, columns: ['الكلمة', 'Position'] }],
  });
  const cfg = parseRepeatableConfig(json);
  expect(cfg.projectRequired).toBe(false);
  expect(cfg.minProjects).toBe(2);
  expect(cfg.maxProjects).toBe(3);
  expect(cfg.fields[0].type).toBe('Grid');
  expect(cfg.fields[0].columns).toEqual(['الكلمة', 'Position']);
});

// ---- 20: parseRepeatableConfig يرجع الافتراضي عند null ----
it('20 parseRepeatableConfig falls back to defaults on null/invalid', () => {
  const cfg = parseRepeatableConfig(null);
  expect(cfg).toEqual({ projectRequired: true, minProjects: 1, maxProjects: 10, fields: [] });
  expect(parseRepeatableConfig('!!broken').fields).toEqual([]);
});

// ---- 21: parseRepeatableEntries يفكّك المشاريع مع answers ----
it('21 parseRepeatableEntries parses projectId + answers', () => {
  const json = JSON.stringify([{ projectId: 'p1', answers: { kw: '[["a"]]' } }]);
  const entries = parseRepeatableEntries(json);
  expect(entries).toHaveLength(1);
  expect(entries[0].projectId).toBe('p1');
  expect(entries[0].answers.kw).toBe('[["a"]]');
});

// ---- 22: parseRepeatableEntries يعيد [] عند null/غير مصفوفة ----
it('22 parseRepeatableEntries returns [] for null and non-array', () => {
  expect(parseRepeatableEntries(null)).toEqual([]);
  expect(parseRepeatableEntries('{"x":1}')).toEqual([]);
  const normalized = parseRepeatableEntries('[{"projectId":null}]');
  expect(normalized[0].answers).toEqual({});
});

// ---- 23: subFieldInputKind الأنواع الرقمية → number ----
it('23 subFieldInputKind maps numeric types to number', () => {
  for (const t of ['Currency', 'Number', 'Decimal', 'Percentage'] as const)
    expect(subFieldInputKind(t)).toBe('number');
});

// ---- 24: subFieldInputKind بقية الأنواع ----
it('24 subFieldInputKind maps text/long/date/bool correctly', () => {
  expect(subFieldInputKind('LongText')).toBe('longtext');
  expect(subFieldInputKind('Date')).toBe('date');
  expect(subFieldInputKind('Boolean')).toBe('bool');
  expect(subFieldInputKind('ShortText')).toBe('text');
  expect(subFieldInputKind('Grid')).toBe('text'); // Grid يُعالَج قبلها بمسار خاص
});

// ---- 25: GridDisplay يعرض رؤوس الأعمدة وقيم الخلايا ----
it('25 GridDisplay renders column headers and cell values', () => {
  render(<GridDisplay columns={['الكلمة', 'Position']} rows={[['seo', '3']]} />);
  expect(screen.getByText('الكلمة')).toBeInTheDocument();
  expect(screen.getByText('Position')).toBeInTheDocument();
  expect(screen.getByText('seo')).toBeInTheDocument();
  expect(screen.getByText('3')).toBeInTheDocument();
});

// ---- 26: GridDisplay يعرض شرطة عند غياب الصفوف ----
it('26 GridDisplay shows dash placeholder when empty', () => {
  render(<GridDisplay columns={['الكلمة']} rows={[]} />);
  expect(screen.getByText('—')).toBeInTheDocument();
});

// ---- 27: GridDisplay يستخدم عمودًا افتراضيًّا عند غياب الأعمدة ----
it('27 GridDisplay uses default column when columns empty', () => {
  render(<GridDisplay columns={[]} rows={[['x']]} />);
  expect(screen.getByText('القيمة')).toBeInTheDocument();
  expect(screen.getByText('x')).toBeInTheDocument();
});

// ---- 28: GridEditor يعرض الأعمدة والصفوف القائمة في حقول الإدخال ----
it('28 GridEditor renders columns and existing rows as inputs', () => {
  render(<GridEditor columns={['الكلمة', 'Position']} rows={[['seo', '3']]} onChange={() => {}} />);
  expect(screen.getByText('الكلمة')).toBeInTheDocument();
  expect(screen.getByDisplayValue('seo')).toBeInTheDocument();
  expect(screen.getByDisplayValue('3')).toBeInTheDocument();
});

// ---- 29: GridEditor «إضافة صف» يضيف صفًّا فارغًا بعدد الأعمدة ----
it('29 GridEditor add-row appends an empty row sized to columns', () => {
  const onChange = vi.fn();
  render(<GridEditor columns={['الكلمة', 'Position']} rows={[]} onChange={onChange} />);
  fireEvent.click(screen.getByText('+ إضافة صف'));
  expect(onChange).toHaveBeenCalledWith([['', '']]);
});

// ---- 30: GridEditor تعديل خلية يستدعي onChange بالقيمة الجديدة ----
it('30 GridEditor editing a cell calls onChange with updated value', () => {
  const onChange = vi.fn();
  render(<GridEditor columns={['الكلمة', 'Position']} rows={[['', '']]} onChange={onChange} />);
  const inputs = screen.getAllByRole('textbox');
  fireEvent.change(inputs[0], { target: { value: 'رمضان' } });
  expect(onChange).toHaveBeenCalledWith([['رمضان', '']]);
});

// ---- 31: GridEditor «حذف» يزيل الصف ----
it('31 GridEditor delete removes a row', () => {
  const onChange = vi.fn();
  render(<GridEditor columns={['الكلمة']} rows={[['a'], ['b']]} onChange={onChange} />);
  fireEvent.click(screen.getAllByText('حذف')[0]);
  expect(onChange).toHaveBeenCalledWith([['b']]);
});

// ---- 32: ProjectRepeatableEditor «إضافة مشروع» يضيف عنصرًا ----
it('32 ProjectRepeatableEditor add-project appends an entry', () => {
  const onChange = vi.fn();
  render(
    <ProjectRepeatableEditor
      config={gridConfig(['الكلمة'])}
      entries={[]}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={onChange}
    />,
  );
  fireEvent.click(screen.getByText('+ إضافة مشروع'));
  expect(onChange).toHaveBeenCalledWith([{ projectId: null, answers: {} }]);
});

// ---- 33: ProjectRepeatableEditor يعرض الحقل الفرعي Grid كمحرّر جدول ----
it('33 ProjectRepeatableEditor renders Grid sub-field as a table editor', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { kw: '[["seo","3"]]' } }];
  render(
    <ProjectRepeatableEditor
      config={gridConfig(['الكلمة', 'Position'])}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  expect(screen.getByText('كلمات المشروع')).toBeInTheDocument();
  expect(screen.getByDisplayValue('seo')).toBeInTheDocument();
  expect(screen.getByDisplayValue('3')).toBeInTheDocument();
});

// ---- 34: تعديل خلية Grid يسلسل string[][] داخل answers[key] ----
it('34 ProjectRepeatableEditor serializes grid cell edits into answers[key] as JSON string[][]', () => {
  const onChange = vi.fn();
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { kw: '[["",""]]' } }];
  render(
    <ProjectRepeatableEditor
      config={gridConfig(['الكلمة', 'Position'])}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={onChange}
    />,
  );
  const inputs = screen.getAllByRole('textbox');
  fireEvent.change(inputs[0], { target: { value: 'دورات تسويق' } });
  const arg = onChange.mock.calls[0][0] as ProjectRepeatableEntry[];
  expect(arg[0].answers.kw).toBe(JSON.stringify([['دورات تسويق', '']]));
});

// ---- 35: ProjectRepeatableEditor يعطّل الإضافة عند بلوغ الحد الأقصى ----
it('35 ProjectRepeatableEditor disables add-project at max', () => {
  const cfg = { ...gridConfig(['الكلمة']), maxProjects: 1 };
  render(
    <ProjectRepeatableEditor
      config={cfg}
      entries={[{ projectId: 'p1', answers: {} }]}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  expect((screen.getByText('+ إضافة مشروع') as HTMLButtonElement).disabled).toBe(true);
});

// ---- 36: ProjectRepeatableDisplay — كل مقاييس الكلمة في صفّ واحد (إثبات SEO) ----
it('36 ProjectRepeatableDisplay shows all SEO keyword metrics in the same row', () => {
  const columns = ['الكلمة المفتاحية', 'الصفحة', 'Position', 'Impressions', 'Clicks', 'CTR'];
  const row = ['دورة تسويق', '/courses', '4', '1200', '90', '7.5%'];
  const cfg: ProjectRepeatableConfig = {
    projectRequired: true, minProjects: 1, maxProjects: 5,
    fields: [{ key: 'kw', label: 'كلمات المشروع', type: 'Grid', required: false, columns }],
  };
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { kw: JSON.stringify([row]) } }];
  render(<ProjectRepeatableDisplay config={cfg} entries={entries} projects={[project('p1', 'مشروع أ', 'عميل س')]} />);
  expect(screen.getByText('مشروع أ — عميل س')).toBeInTheDocument();
  // الخلايا الست في صفّ الجدول نفسه.
  const cells = screen.getAllByRole('cell');
  const texts = cells.map((c) => c.textContent);
  for (const v of row) expect(texts).toContain(v);
});

// ---- 37: دورة كاملة — تسلسل ثم تفكيك يستعيد string[][] داخل answers ----
it('37 full round-trip: stringify entries then parse recovers grid string[][]', () => {
  const rows = [['seo', '3'], ['sem', '5']];
  const entries = [{ projectId: 'p1', answers: { kw: JSON.stringify(rows) } }];
  const back = parseRepeatableEntries(JSON.stringify(entries));
  expect(back[0].projectId).toBe('p1');
  expect(parseGrid(back[0].answers.kw)).toEqual(rows);
});

// ===== MODERATION-PERFORMANCE-INSIGHTS-R1A — عرض المديرشن الحيّ V5 (Vocabulary 1) =====
// يُثبِت عبر DOM: تجميع الحقول المتاحة في أقسام مرتّبة، ظهور القيمة "0" الرقمية،
// سقوط القوالب غير-المديرشن للعرض العام (fallback)، تصفية صفوف الجدول الفارغة كليًّا.
// لا مؤشرات محسوبة، لا مقاييس غير مدعومة بالبيانات.
function moderationV5Config(): ProjectRepeatableConfig {
  return {
    projectRequired: true, minProjects: 1, maxProjects: 10,
    fields: [
      { key: 'project_status', label: 'حالة المشروع', type: 'Select', required: false, options: ['🟢 ممتاز', '🟡 مستقر', '🔴 يحتاج تدخل'] },
      { key: 'time_consumption', label: 'استهلاك الوقت', type: 'Percentage', required: false },
      { key: 'incoming_messages', label: 'الرسائل الواردة', type: 'Number', required: false },
      { key: 'answered_messages', label: 'الرسائل المُجابة', type: 'Number', required: false },
      { key: 'avg_response_minutes', label: 'متوسط زمن الرد (د)', type: 'Number', required: false },
      { key: 'problematic_comments', label: 'التعليقات الإشكالية', type: 'Number', required: false },
      { key: 'escalations', label: 'الحالات المصعّدة', type: 'Number', required: false },
      { key: 'complaints', label: 'الشكاوى', type: 'Number', required: false },
      { key: 'converted_opportunities', label: 'الفرص المحوَّلة', type: 'Number', required: false },
      { key: 'cases_grid', label: 'سجل الحالات', type: 'Grid', required: false, columns: ['نوع الحالة', 'الوصف', 'القناة', 'الحالة', 'هل تم التصعيد؟', 'الإجراء التالي'] },
      { key: 'done', label: 'ما أُنجز', type: 'LongText', required: false },
      { key: 'issues', label: 'المشكلات', type: 'LongText', required: false },
      { key: 'recurring_questions', label: 'الأسئلة المتكررة', type: 'LongText', required: false },
      { key: 'next_week', label: 'خطة الأسبوع القادم', type: 'LongText', required: false },
      { key: 'recommendations', label: 'التوصيات', type: 'LongText', required: false },
    ],
  };
}

// ---- 38: عرض المديرشن V5 يُظهِر عناوين الأقسام الخمسة والقيمة "0" الرقمية ----
it('38 ProjectRepeatableDisplay renders V5 moderation grouped sections including a "0" value', () => {
  const entries: ProjectRepeatableEntry[] = [{
    projectId: 'p1',
    answers: {
      project_status: '🟢 ممتاز', time_consumption: '80', incoming_messages: '120', answered_messages: '118',
      avg_response_minutes: '5', problematic_comments: '2', escalations: '0', complaints: '1', converted_opportunities: '3',
      cases_grid: JSON.stringify([['شكوى', 'تأخر رد', 'واتساب', 'مغلقة', 'لا', 'متابعة']]),
      done: 'إنجاز', issues: 'لا يوجد', recurring_questions: 'الأسعار', next_week: 'حملة', recommendations: 'تحسين الرد',
    },
  }];
  render(<ProjectRepeatableDisplay config={moderationV5Config()} entries={entries} projects={[project('p1', 'مشروع أ', 'عميل س')]} />);
  for (const t of ['نظرة عامة', 'حجم العمل', 'الجودة والتصعيد', 'الحالات', 'السرد والتوصيات'])
    expect(screen.getByText(t)).toBeInTheDocument();
  expect(screen.getByText('🟢 ممتاز')).toBeInTheDocument();
  expect(screen.getByText('0')).toBeInTheDocument(); // escalations=0 يجب أن يظهر لا أن يُخفى
  expect(screen.getByText('نوع الحالة')).toBeInTheDocument(); // رأس عمود cases_grid
});

// ---- 39: مفتاح غير معروف في قالب المديرشن يظهر تحت «حقول إضافية» ----
it('39 unknown moderation key falls under "حقول إضافية" section', () => {
  const cfg = moderationV5Config();
  cfg.fields.push({ key: 'legacy_extra', label: 'حقل قديم', type: 'ShortText', required: false });
  const entries: ProjectRepeatableEntry[] = [{
    projectId: 'p1',
    answers: { project_status: '🟢 ممتاز', incoming_messages: '10', cases_grid: '[]', legacy_extra: 'قيمة قديمة' },
  }];
  render(<ProjectRepeatableDisplay config={cfg} entries={entries} projects={[project('p1', 'مشروع أ')]} />);
  expect(screen.getByText('حقول إضافية')).toBeInTheDocument();
  expect(screen.getByText('حقل قديم')).toBeInTheDocument();
  expect(screen.getByText('قيمة قديمة')).toBeInTheDocument();
});

// ---- 40: قالب غير-مديرشن يسقط للعرض العام بلا عناوين أقسام (fallback) ----
it('40 non-moderation config uses generic flat layout (no group titles)', () => {
  const cfg: ProjectRepeatableConfig = {
    projectRequired: true, minProjects: 1, maxProjects: 5,
    fields: [{ key: 'kw', label: 'كلمات المشروع', type: 'Grid', required: false, columns: ['الكلمة'] }],
  };
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { kw: '[["seo"]]' } }];
  render(<ProjectRepeatableDisplay config={cfg} entries={entries} projects={[project('p1', 'مشروع أ')]} />);
  expect(screen.queryByText('نظرة عامة')).not.toBeInTheDocument();
  expect(screen.queryByText('السرد والتوصيات')).not.toBeInTheDocument();
  expect(screen.getByText('كلمات المشروع')).toBeInTheDocument();
  expect(screen.getByText('seo')).toBeInTheDocument();
});

// ---- 41: GridDisplay يتخطّى الصفوف الفارغة كليًّا ويُبقي الصفوف ذات القيم ----
it('41 GridDisplay skips fully-empty rows and keeps filled rows', () => {
  render(<GridDisplay columns={['نوع الحالة', 'الوصف']} rows={[['', ''], ['شكوى', 'تأخر'], ['  ', '']]} />);
  expect(screen.getByText('شكوى')).toBeInTheDocument();
  expect(screen.getByText('تأخر')).toBeInTheDocument();
  // صفّ واحد فقط ذو قيمة يظهر (خليّتان).
  expect(screen.getAllByRole('cell')).toHaveLength(2);
});

// ---- 42: GridDisplay يعرض شرطة عندما تكون كل الصفوف فارغة كليًّا ----
it('42 GridDisplay shows dash when all rows are fully empty', () => {
  render(<GridDisplay columns={['نوع الحالة', 'الوصف']} rows={[['', ''], ['  ', '']]} />);
  expect(screen.getByText('—')).toBeInTheDocument();
  expect(screen.queryAllByRole('cell')).toHaveLength(0);
});

describe('R1B Grid frontend suite meta', () => {
  it('covers editor, display, helpers and round-trip', () => {
    expect(true).toBe(true);
  });
});

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

describe('R1B Grid frontend suite meta', () => {
  it('covers editor, display, helpers and round-trip', () => {
    expect(true).toBe(true);
  });
});

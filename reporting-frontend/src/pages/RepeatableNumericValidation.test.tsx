import { render, screen } from '@testing-library/react';
import { it, expect } from 'vitest';
import {
  parseRepeatableConfig,
  validateRepeatableNumber,
  ProjectRepeatableEditor,
} from './SubmissionsPage';
import type { ProjectDto, ProjectRepeatableConfig, ProjectRepeatableEntry, RepeatableSubField } from '../types/api';

// ===== PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — اختبارات التحقّق الرقميّ للحقول الفرعيّة =====
// تُثبِت: قراءة القيود (min/max/integerOnly/step) من الإعداد، التوافق الخلفيّ للقوالب بلا قيود،
// إسقاط سمات min/max/step إلى <input type=number>، والتحقّق العميليّ المطابق للخادم.
// لا Migration، لا فرض min=0 عالميّ، لا كسر لأيّ قالب قائم.

function project(id: string, name: string): ProjectDto {
  return {
    id, clientId: 'c-' + id, clientName: null, name, serviceType: 'Seo', status: 'Active',
    startDate: null, endDate: null, ownerTeamId: null, ownerTeamName: null,
    accountManagerId: null, accountManagerName: null, notes: null,
    createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: null, canHardDelete: true, deleteBlockReason: null,
  } as ProjectDto;
}

function numericField(overrides: Partial<RepeatableSubField> = {}): RepeatableSubField {
  return { key: 'pieces', label: 'عدد القطع', type: 'Number', required: false, ...overrides } as RepeatableSubField;
}

function configWith(field: RepeatableSubField): ProjectRepeatableConfig {
  return { projectRequired: true, minProjects: 1, maxProjects: 5, fields: [field] };
}

// ---- N1: المُحلِّل يقرأ القيود الرقميّة الأربعة ----
it('N1 parseRepeatableConfig reads numeric constraints', () => {
  const json = JSON.stringify({
    projectRequired: true, minProjects: 1, maxProjects: 5,
    fields: [{ key: 'pieces', label: 'قطع', type: 'Number', required: true, min: 0, max: 100, integerOnly: true, step: 1 }],
  });
  const cfg = parseRepeatableConfig(json);
  const f = cfg.fields[0];
  expect(f.min).toBe(0);
  expect(f.max).toBe(100);
  expect(f.integerOnly).toBe(true);
  expect(f.step).toBe(1);
});

// ---- N2: قالب قديم بلا قيود يُحلَّل بلا قيود (توافق خلفيّ) ----
it('N2 legacy field without constraints parses with no constraints', () => {
  const json = JSON.stringify({
    projectRequired: true, minProjects: 1, maxProjects: 5,
    fields: [{ key: 'v', label: 'قيمة', type: 'Number', required: false }],
  });
  const f = parseRepeatableConfig(json).fields[0];
  expect(f.min).toBeUndefined();
  expect(f.max).toBeUndefined();
  expect(f.integerOnly).toBe(false);
  expect(f.step).toBeUndefined();
});

// ---- N3: step غير صالح (≤0) يُهمَل عند التحليل ----
it('N3 invalid step (<=0) is dropped by the parser', () => {
  const json = JSON.stringify({
    projectRequired: true, minProjects: 1, maxProjects: 5,
    fields: [{ key: 'v', label: 'قيمة', type: 'Number', required: false, step: 0 }],
  });
  expect(parseRepeatableConfig(json).fields[0].step).toBeUndefined();
});

// ---- N4: المحرّر يُسقِط سمات min/max على <input type=number> ----
it('N4 editor forwards min/max to the number input', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { pieces: '5' } }];
  render(
    <ProjectRepeatableEditor
      config={configWith(numericField({ min: 0, max: 100 }))}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  const input = screen.getByRole('spinbutton') as HTMLInputElement;
  expect(input.min).toBe('0');
  expect(input.max).toBe('100');
});

// ---- N5: integerOnly ⇒ step يفترض 1 على <input> ----
it('N5 integerOnly defaults input step to 1', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { pieces: '5' } }];
  render(
    <ProjectRepeatableEditor
      config={configWith(numericField({ integerOnly: true }))}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  const input = screen.getByRole('spinbutton') as HTMLInputElement;
  expect(input.step).toBe('1');
});

// ---- N6: step عشريّ صريح يُسقَط على <input> ----
it('N6 explicit decimal step is forwarded to the input', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { pieces: '0.2' } }];
  render(
    <ProjectRepeatableEditor
      config={configWith(numericField({ type: 'Decimal', step: 0.1 }))}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  const input = screen.getByRole('spinbutton') as HTMLInputElement;
  expect(input.step).toBe('0.1');
});

// ---- N7: التحقّق العميليّ — عشريّ مع integerOnly يُرفَض ----
it('N7 integer validation rejects a decimal value', () => {
  expect(validateRepeatableNumber(numericField({ integerOnly: true }), '12.5')).toBe('يجب إدخال عدد صحيح.');
});

// ---- N8: التحقّق العميليّ — قيمة سالبة تحت الحدّ الأدنى تُرفَض ----
it('N8 negative validation rejects value below min', () => {
  expect(validateRepeatableNumber(numericField({ min: 0 }), '-1')).toBe('القيمة أقل من الحدّ الأدنى (0).');
});

// ---- N9: التحقّق العميليّ — قيمة أكبر من الحدّ الأقصى تُرفَض ----
it('N9 rejects value above max', () => {
  expect(validateRepeatableNumber(numericField({ max: 100 }), '101')).toBe('القيمة أكبر من الحدّ الأقصى (100).');
});

// ---- N10: قيمة صالحة (صفر مع min=0) لا تُنتِج خطأ ----
it('N10 valid value (zero with min=0) clears the error', () => {
  expect(validateRepeatableNumber(numericField({ min: 0, integerOnly: true }), '0')).toBeNull();
});

// ---- N11: قيمة موجبة صالحة ضمن المدى لا تُنتِج خطأ ----
it('N11 valid positive value within range clears the error', () => {
  expect(validateRepeatableNumber(numericField({ min: 0, max: 100, integerOnly: true }), '50')).toBeNull();
});

// ---- N12: قيمة فارغة لا تُنتِج خطأ (الاختيارية تُترَك للحقل المطلوب) ----
it('N12 empty value does not produce a numeric error', () => {
  expect(validateRepeatableNumber(numericField({ min: 0 }), '')).toBeNull();
  expect(validateRepeatableNumber(numericField({ min: 0 }), '   ')).toBeNull();
});

// ---- N13: حقل بلا قيود لا يُنتِج خطأ لأيّ قيمة (توافق خلفيّ للسالب التاريخيّ) ----
it('N13 field without constraints never errors (historical negative passes)', () => {
  expect(validateRepeatableNumber(numericField(), '-1')).toBeNull();
  expect(validateRepeatableNumber(numericField(), '12.5')).toBeNull();
});

// ---- N14: خطوة غير مطابقة تُرفَض ----
it('N14 off-step value is rejected', () => {
  expect(validateRepeatableNumber(numericField({ type: 'Decimal', step: 0.1 }), '0.15')).toBe('القيمة لا تطابق خطوة الإدخال (0.1).');
});

// ---- N15: المحرّر يعرض رسالة الخطأ العميليّة لقيمة تحت الحدّ (يحاكي كود الخادم below_min) ----
it('N15 editor renders the client-side below-min message for -1 (mirrors server code)', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { pieces: '-1' } }];
  render(
    <ProjectRepeatableEditor
      config={configWith(numericField({ min: 0, integerOnly: true }))}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  expect(screen.getByText('القيمة أقل من الحدّ الأدنى (0).')).toBeInTheDocument();
});

// ---- N16: المحرّر لا يعرض خطأً لحقل قديم بلا قيود يحمل قيمة سالبة تاريخيّة ----
it('N16 editor shows no error for a legacy unconstrained field holding a historical -1', () => {
  const entries: ProjectRepeatableEntry[] = [{ projectId: 'p1', answers: { pieces: '-1' } }];
  render(
    <ProjectRepeatableEditor
      config={configWith(numericField())}
      entries={entries}
      projects={[project('p1', 'مشروع أ')]}
      allProjects={[project('p1', 'مشروع أ')]}
      onChange={() => {}}
    />,
  );
  expect(screen.queryByText(/الحدّ الأدنى/)).not.toBeInTheDocument();
  expect((screen.getByRole('spinbutton') as HTMLInputElement).value).toBe('-1');
});

// ---- N17: نوع غير رقميّ لا يخضع للتحقّق الرقميّ ----
it('N17 non-numeric type is not numerically validated', () => {
  const textField = { key: 't', label: 'نص', type: 'ShortText', required: false, min: 0 } as RepeatableSubField;
  expect(validateRepeatableNumber(textField, '-5')).toBeNull();
});

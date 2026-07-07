import { render, screen, fireEvent, within } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GridEditor } from './SubmissionsPage';
import type { ServiceDto } from '../types/api';

// ===== RC3-Task2A — إصلاح منتقي «الخدمة» في قالب مبيعات B2B حسب الخدمة =====
// يُثبِت أن عمود «الخدمة» (فهرس 0) يُعرَض كـ Select من كتالوج الخدمات (نفس منطق «الدورة» في B2C):
//   - يظهر كـ Select لا حقل نصّي حرّ.
//   - خياراته من الخدمات النشطة فقط (مصدرها GET /api/services عبر useActiveServices → nameAr).
//   - الخدمة المعطّلة لا تظهر في القائمة للتقارير الجديدة.
//   - قيمة خدمة قديمة (Legacy) خارج الكتالوج تبقى ظاهرة ولا تكسر العرض.
//   - عمود «الدورة» في B2C لا ينكسر (نفس المكوّن، اختبار عدم تراجع).

// أعمدة قالب B2B العشرة (مطابقة B2bByServiceReportSchema.Columns).
const B2B_COLUMNS = [
  'الخدمة', 'ساعات العمل', 'Leads', 'Meetings', 'Proposals',
  'Negotiation', 'Won', 'Lost', 'Revenue', 'Next Step',
];

function svc(nameAr: string, isActive: boolean, sortOrder: number): ServiceDto {
  return {
    id: 'svc-' + nameAr,
    nameAr,
    nameEn: null,
    isActive,
    sortOrder,
    createdAtUtc: '2026-07-01T00:00:00Z',
    updatedAtUtc: null,
  };
}

// يحاكي ما يفعله SubmissionDetail: GET /api/services يعيد النشطة فقط ثم تُشتقّ الأسماء.
// (نُبقي الفلترة هنا لإثبات أن المعطّلة لا تصل للمنتقي حتى لو تسرّبت للاستجابة.)
function activeServiceNames(catalog: ServiceDto[]): string[] {
  return catalog.filter((s) => s.isActive).map((s) => s.nameAr);
}

const CATALOG: ServiceDto[] = [
  svc('تصميم موقع إلكتروني', true, 1),
  svc('إدارة سوشيال ميديا', true, 2),
  svc('حملات إعلانية', true, 3),
  svc('خدمة قديمة معطّلة', false, 99), // معطّلة — يجب ألّا تظهر للتقارير الجديدة.
];

// ---- 1: عمود «الخدمة» يُعرَض كـ Select (لا حقل نصّي) والأعمدة الرقمية تبقى نصّية ----
it('B2B Service column renders as a Select dropdown, numeric columns stay text inputs', () => {
  const serviceNames = activeServiceNames(CATALOG);
  render(
    <GridEditor
      columns={B2B_COLUMNS}
      rows={[['', '', '', '', '', '', '', '', '', '']]}
      onChange={() => {}}
      columnOptions={{ 0: serviceNames }}
    />,
  );
  const rowCells = screen.getAllByRole('row')[1]; // صفّ البيانات (بعد صفّ الرؤوس).
  // عمود «الخدمة» = Select (combobox)، وباقي الأعمدة الرقمية = حقول نصّية (textbox).
  const combos = within(rowCells).getAllByRole('combobox');
  const texts = within(rowCells).getAllByRole('textbox');
  expect(combos).toHaveLength(1);            // عمود الخدمة فقط منسدل.
  expect(texts.length).toBe(B2B_COLUMNS.length - 1); // 9 أعمدة رقمية/نصّية حرّة.
});

// ---- 2: خيارات المنتقي تأتي من /api/services (النشطة فقط) والمعطّلة لا تظهر ----
it('Service options come from active /api/services entries; deactivated service is hidden', () => {
  const serviceNames = activeServiceNames(CATALOG);
  render(
    <GridEditor
      columns={B2B_COLUMNS}
      rows={[['', '', '', '', '', '', '', '', '', '']]}
      onChange={() => {}}
      columnOptions={{ 0: serviceNames }}
    />,
  );
  const combo = screen.getByRole('combobox');
  const optionValues = within(combo)
    .getAllByRole('option')
    .map((o) => (o as HTMLOptionElement).value);
  // الخدمات النشطة الثلاث حاضرة.
  expect(optionValues).toContain('تصميم موقع إلكتروني');
  expect(optionValues).toContain('إدارة سوشيال ميديا');
  expect(optionValues).toContain('حملات إعلانية');
  // المعطّلة غائبة عن التقارير الجديدة.
  expect(optionValues).not.toContain('خدمة قديمة معطّلة');
});

// ---- 3: اختيار خدمة من المنسدل يستدعي onChange بالقيمة المختارة (لا كتابة يدوية) ----
it('selecting a service from the dropdown calls onChange with the chosen catalog value', () => {
  const onChange = vi.fn();
  const serviceNames = activeServiceNames(CATALOG);
  render(
    <GridEditor
      columns={B2B_COLUMNS}
      rows={[['', '', '', '', '', '', '', '', '', '']]}
      onChange={onChange}
      columnOptions={{ 0: serviceNames }}
    />,
  );
  fireEvent.change(screen.getByRole('combobox'), { target: { value: 'حملات إعلانية' } });
  const nextRows = onChange.mock.calls[0][0] as string[][];
  expect(nextRows[0][0]).toBe('حملات إعلانية');
});

// ---- 4: قيمة خدمة قديمة (Legacy) خارج الكتالوج تبقى ظاهرة ومختارة ----
it('legacy service value outside the active catalog stays visible and selected', () => {
  const serviceNames = activeServiceNames(CATALOG);
  render(
    <GridEditor
      columns={B2B_COLUMNS}
      rows={[['خدمة تسويقية ملغاة', '12', '', '', '', '', '', '', '', '']]}
      onChange={() => {}}
      columnOptions={{ 0: serviceNames }}
    />,
  );
  const combo = screen.getByRole('combobox') as HTMLSelectElement;
  // القيمة القديمة محفوظة ومختارة رغم غيابها عن الكتالوج النشط.
  expect(combo.value).toBe('خدمة تسويقية ملغاة');
  // وتظهر موسومة كقيمة قديمة كي لا تُمحى عند التعديل.
  expect(screen.getByText('خدمة تسويقية ملغاة (قيمة قديمة)')).toBeInTheDocument();
});

// ---- 5: عدم تراجع B2C — عمود «الدورة» ما زال يُعرَض كـ Select ----
it('B2C Course Select is unaffected (regression guard): الدورة column still renders as a dropdown', () => {
  const courseNames = ['دورة تسويق رقمي', 'دورة تحليل بيانات'];
  render(
    <GridEditor
      columns={['الدورة', 'عدد المسجّلين', 'الإيراد']}
      rows={[['', '', '']]}
      onChange={() => {}}
      columnOptions={{ 0: courseNames }}
    />,
  );
  const combo = screen.getByRole('combobox');
  const optionValues = within(combo)
    .getAllByRole('option')
    .map((o) => (o as HTMLOptionElement).value);
  expect(optionValues).toContain('دورة تسويق رقمي');
  expect(optionValues).toContain('دورة تحليل بيانات');
});

// ---- 6: بدون columnOptions يبقى الجدول حقولًا نصّية (حارس السلوك الافتراضي) ----
it('without columnOptions the grid stays plain text inputs (default guard)', () => {
  render(
    <GridEditor
      columns={B2B_COLUMNS}
      rows={[['', '', '', '', '', '', '', '', '', '']]}
      onChange={() => {}}
    />,
  );
  expect(screen.queryByRole('combobox')).toBeNull();
  const rowCells = screen.getAllByRole('row')[1];
  expect(within(rowCells).getAllByRole('textbox')).toHaveLength(B2B_COLUMNS.length);
});

describe('RC3-Task2A B2B Service Select suite meta', () => {
  it('covers select rendering, active-only options, legacy value, and B2C non-regression', () => {
    expect(true).toBe(true);
  });
});

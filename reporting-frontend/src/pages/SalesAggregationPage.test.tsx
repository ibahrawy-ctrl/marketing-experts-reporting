import { render, screen, fireEvent, within } from '@testing-library/react';
import { it, expect, vi, beforeEach } from 'vitest';

// حالة قابلة للحقن لبيانات تجميع الدورة (vi.hoisted كي تُستخدَم داخل vi.mock المرفوع).
const state = vi.hoisted(() => ({ grouped: undefined as unknown }));

// عزل الصفحة عن الشبكة: هوكات التجميع مُموّهة (لا استدعاء API).
// useB2cCourseGrouped يُرجِع البيانات المحقونة (يُستدعى للفترة الحالية والسابقة معًا).
vi.mock('../lib/useSalesAggregation', () => ({
  useB2cAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cCourseGrouped: () => ({ data: state.grouped, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cNewOld: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2bAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2bBySource: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
}));

import SalesAggregationPage from './SalesAggregationPage';

// تنسيق رقم مطابق لدالّة num في الصفحة (ar-EG) لمطابقة نصّ الخلايا بثبات.
const fmt = (v: number) => Number(v).toLocaleString('ar-EG', { maximumFractionDigits: 2 });

// دلو New/Old بكل الحقول المطلوبة (القيم الافتراضية 0 ثم تُستبدَل).
function bucket(over: Record<string, number>): Record<string, number> {
  return {
    workHours: 0, leads: 0, contacted: 0, qualified: 0, followUps: 0,
    sales: 0, revenue: 0, lost: 0, conversionRate: 0, qualificationRate: 0,
    contactRate: 0, revenuePerHour: 0, salesPerHour: 0, lostRate: 0, ...over,
  };
}

// تقرير تجميع دورة واحدة بموظّف واحد له دلوان مختلفان (New ≠ Old) لإثبات تغيّر الإجماليات حسب المصدر.
function buildReport() {
  const empNew = bucket({ workHours: 10, leads: 20, contacted: 15, qualified: 8, sales: 5, revenue: 5000, conversionRate: 25 });
  const empOld = bucket({ workHours: 6, leads: 40, contacted: 20, qualified: 4, sales: 2, revenue: 1200, conversionRate: 5 });
  const employee = {
    employeeId: 'e1', employeeName: 'أحمد سالم', teamId: 't1', departmentId: 'd1',
    workHours: 16, leads: 60, contacted: 35, qualified: 12, followUps: 0,
    sales: 7, revenue: 6200, lost: 0, conversionRate: 11.67, new: empNew, old: empOld,
  };
  const course = {
    course: 'دورة النخبة', workHours: 16, leads: 60, contacted: 35, qualified: 12, followUps: 0,
    sales: 7, revenue: 6200, lost: 0, conversionRate: 11.67, qualificationRate: 34.29,
    contactRate: 58.33, revenuePerHour: 387.5, salesPerHour: 0.44, lostRate: 0,
    employeeCount: 1, employees: [employee],
  };
  return {
    periodKey: '2026-W25', courseCount: 1, submissionsConsidered: 1, submissionsIgnored: 0,
    rowsIgnored: 0, viewLevel: 'course', courses: [course],
  };
}

beforeEach(() => {
  state.grouped = undefined;
});

// ===== SALES-AGG-FIX — الواجهة لا تعرض صندوق «مفتاح الفترة» النصّي، بل منتقيات حسب النوع =====

it('لا يعرض صندوق مفتاح الفترة النصّي؛ يعرض منتقي تاريخ في الوضع الأسبوعي الافتراضي', () => {
  const { container } = render(<SalesAggregationPage />);

  expect(screen.queryByText('مفتاح الفترة')).toBeNull();
  expect(screen.getByText('نوع الفترة')).toBeInTheDocument();
  expect(screen.getByText('الأسبوع')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();
  expect(screen.queryByRole('textbox')).toBeNull();
});

it('تبديل النوع يُظهر المنتقي المطابق (يومي/شهري/ربع سنوي) دون أي صندوق نصّي لمفتاح الفترة', () => {
  const { container } = render(<SalesAggregationPage />);
  const typeSelect = screen.getByRole('combobox');

  fireEvent.change(typeSelect, { target: { value: 'Daily' } });
  expect(screen.getByText('اليوم')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();

  fireEvent.change(typeSelect, { target: { value: 'Monthly' } });
  expect(screen.getByText('الشهر')).toBeInTheDocument();
  expect(container.querySelector('input[type="month"]')).not.toBeNull();

  fireEvent.change(typeSelect, { target: { value: 'Quarterly' } });
  expect(screen.getByText('السنة')).toBeInTheDocument();
  expect(screen.getByText('الربع')).toBeInTheDocument();
  expect(container.querySelector('input[type="number"]')).not.toBeNull();

  expect(screen.queryByText('مفتاح الفترة')).toBeNull();
});

// ===== B2C-COURSE-SOURCE-FILTER — فلتر المصدر يظهر في «تجميع حسب الدورة» ويربط KPI/Charts/الجدول/التفصيل =====

it('عرض تجميع الدورة يعرض فلتر المصدر (الكل/New/Old)', () => {
  state.grouped = buildReport();
  render(<SalesAggregationPage />);

  // الوضع الافتراضي = B2C + تجميع حسب الدورة ⇒ فلتر التقسيم ظاهر.
  expect(screen.getByText('تقسيم البيانات:')).toBeInTheDocument();
  expect(screen.getByText('الكل (جديد + قديم)')).toBeInTheDocument();
  expect(screen.getByText('بيانات جديدة New Leads')).toBeInTheDocument();
  expect(screen.getByText('بيانات CRM قديمة Old')).toBeInTheDocument();
});

it('LIVE-FIX — عرض تجميع الدورة يعرض فلتر المصدر حتى مع غياب بيانات الفترة (الحالة الفارغة أسفل الفلتر لا بدلًا منه)', () => {
  // يحاكي حالة الإنتاج المؤكَّدة: by-course يُرجِع 0 دورة للفترة ⇒ يجب أن يبقى الفلتر ظاهرًا (كما في تفصيل الموظّف)
  // وتظهر الحالة الفارغة أسفله، لا أن يُختصَر العرض إلى الحالة الفارغة قبل الفلتر.
  state.grouped = { periodKey: '2026-W27', courseCount: 0, submissionsConsidered: 0, submissionsIgnored: 0, rowsIgnored: 0, viewLevel: 'course', courses: [] };
  render(<SalesAggregationPage />);

  expect(screen.getByText('تقسيم البيانات:')).toBeInTheDocument();
  expect(screen.getByText('بيانات جديدة New Leads')).toBeInTheDocument();
  expect(screen.getByText('بيانات CRM قديمة Old')).toBeInTheDocument();
  // الحالة الفارغة حاضرة أسفل الفلتر.
  expect(screen.getByText('لا توجد بيانات تجميع B2C')).toBeInTheDocument();
});

it('اختيار New في تجميع الدورة يُحدّث KPI/Charts + جدول الدورات + التفصيل بأرقام المصدر الجديد', () => {
  state.grouped = buildReport();
  render(<SalesAggregationPage />);

  fireEvent.click(screen.getByText('بيانات جديدة New Leads'));

  // عنوان قسم New + لوحة القيادة (KPI/Charts) حاضرة.
  expect(screen.getByText('أداء البيانات الجديدة New Leads حسب الدورة')).toBeInTheDocument();
  expect(screen.getByText('متوسّط نسبة التحويل')).toBeInTheDocument();

  // أرقام المصدر الجديد ظاهرة، وقيمة Old الحصرية (1200) غائبة تمامًا ⇒ التصفية فعّالة.
  expect(screen.getAllByText(fmt(5000)).length).toBeGreaterThan(0);
  expect(screen.queryByText(fmt(1200))).toBeNull();

  // Drill-down: توسيع الدورة داخل جدول المصدر يُظهر مساهمة الموظّف بأرقام New.
  const newTable = screen.getByRole('columnheader', { name: 'New Leads' }).closest('table')!;
  fireEvent.click(within(newTable).getByText('دورة النخبة'));
  expect(within(newTable).getByText('↳ أحمد سالم')).toBeInTheDocument();
});

it('اختيار Old في تجميع الدورة يُحدّث الأرقام لبيانات CRM القديمة (1200) دون أرقام New (5000)', () => {
  state.grouped = buildReport();
  render(<SalesAggregationPage />);

  fireEvent.click(screen.getByText('بيانات CRM قديمة Old'));

  expect(screen.getByText('أداء بيانات CRM القديمة Old حسب الدورة')).toBeInTheDocument();
  expect(screen.getByText('متوسّط نسبة التحويل')).toBeInTheDocument();
  expect(screen.getAllByText(fmt(1200)).length).toBeGreaterThan(0);
  expect(screen.queryByText(fmt(5000))).toBeNull();
});

it('عرض «تفصيل حسب الموظّف» لا ينكسر ويظلّ فلتر المصدر يعمل فيه', () => {
  state.grouped = buildReport();
  render(<SalesAggregationPage />);

  fireEvent.click(screen.getByText('تفصيل حسب الموظّف'));

  // جدول الموظّفين + فلتر التقسيم حاضران.
  expect(screen.getByText('تقسيم البيانات:')).toBeInTheDocument();
  const table = screen.getByRole('table');
  expect(within(table).getByText('أحمد سالم')).toBeInTheDocument();
});

it('التبديل إلى B2B لا ينكسر (يعرض حالة فارغة عند غياب البيانات)', () => {
  state.grouped = buildReport();
  render(<SalesAggregationPage />);

  fireEvent.click(screen.getByText('مبيعات B2B حسب الخدمة'));

  expect(screen.getByText('لا توجد بيانات تجميع B2B')).toBeInTheDocument();
  // لم يعد قسم B2C ظاهرًا.
  expect(screen.queryByText('أداء البيانات الجديدة New Leads حسب الدورة')).toBeNull();
});

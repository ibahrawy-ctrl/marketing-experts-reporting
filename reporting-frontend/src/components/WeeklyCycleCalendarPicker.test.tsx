import { useState } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WeeklyCycleCalendarPicker } from './WeeklyCycleCalendarPicker';
import type { MyCyclesDto, ReportingCycleDto } from '../types/api';

// اختبار عدم التراجع للوضع الأسبوعيّ: نتأكّد أن منتقي الدورة الأسبوعيّة لم يتأثّر بإضافة الوضع اليوميّ،
// ولا يزال يقرأ الدورات من الخادم بلا إدخال نصّيّ. نُحاكي هوك الدورات لعزل المكوّن عن الشبكة.
const mockUseReportingCalendar = vi.fn();
vi.mock('../lib/useReportingCalendar', () => ({
  useReportingCalendar: (opts: unknown) => mockUseReportingCalendar(opts),
}));

function cycle(overrides: Partial<ReportingCycleDto> = {}): ReportingCycleDto {
  return {
    cycleKey: '2026-W29',
    cycleNumber: 29,
    cycleYear: 2026,
    cycleStart: '2026-07-11',
    cycleEnd: '2026-07-17',
    cycleLabel: 'الأسبوع 29 — 2026',
    offset: 0,
    isCurrent: true,
    isOpen: true,
    isFuture: false,
    isPast: false,
    isLocked: false,
    isOverdue: false,
    requiresReason: false,
    lockReason: null,
    roleLabel: 'موظّف',
    roleDueDateLabel: 'الأربعاء 15 يوليو 2026',
    ...overrides,
  } as ReportingCycleDto;
}

function cyclesData(): MyCyclesDto {
  return {
    context: 'Report',
    templateId: null,
    role: 'Employee',
    roleLabel: 'موظّف',
    today: '2026-07-14',
    cycles: [
      cycle({ cycleKey: '2026-W28', cycleNumber: 28, cycleStart: '2026-07-04', cycleEnd: '2026-07-10', offset: -1, isCurrent: false, isPast: true }),
      cycle(),
    ],
  } as MyCyclesDto;
}

function mockSuccess() {
  mockUseReportingCalendar.mockReturnValue({
    data: cyclesData(),
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  });
}

function Harness() {
  const [value, setValue] = useState<string | null>(null);
  return (
    <WeeklyCycleCalendarPicker value={value} onChange={(k) => setValue(k)} />
  );
}

beforeEach(() => {
  mockUseReportingCalendar.mockReset();
});

describe('WeeklyCycleCalendarPicker (no regression)', () => {
  it('لا يعرض أيّ حقل إدخال تاريخ نصّيّ', () => {
    mockSuccess();
    const { container } = render(<Harness />);
    expect(container.querySelector('input[type="date"]')).toBeNull();
    expect(container.querySelector('input')).toBeNull();
  });

  it('يعرض شبكة التقويم الأسبوعيّة ويحدّد الدورة الحالية تلقائيًّا', () => {
    mockSuccess();
    render(<Harness />);
    expect(screen.getByLabelText('تقويم دورات التقارير الأسبوعية')).toBeInTheDocument();
    expect(screen.getAllByText(/الأسبوع 29/).length).toBeGreaterThan(0);
  });

  it('عند فشل الواجهة يعرض رسالة + إعادة محاولة (لا ارتداد محليّ)', () => {
    mockUseReportingCalendar.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: vi.fn(),
    });
    render(<Harness />);
    expect(screen.getByText('إعادة المحاولة')).toBeInTheDocument();
  });
});

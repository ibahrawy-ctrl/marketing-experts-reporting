import { useState } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { DailyCalendarPicker } from './DailyCalendarPicker';
import type { MyDaysDto, ReportingDayDto, ReportingDayStatus } from '../types/api';

// نُحاكي هوك الأيام لعزل المكوّن عن الشبكة (لا axios). المكوّن يقرأ ما يُعيده الخادم فقط.
const mockUseReportingDays = vi.fn();
vi.mock('../lib/useReportingCalendar', () => ({
  useReportingDays: (opts: unknown) => mockUseReportingDays(opts),
}));

function ymd(y: number, m: number, d: number) {
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}
function addKey(key: string, delta: number): string {
  const [y, m, d] = key.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d + delta));
  return ymd(dt.getUTCFullYear(), dt.getUTCMonth() + 1, dt.getUTCDate());
}

// بناء صفّ يوم اختباريّ متوافق مع ReportingDayDto.
function day(
  key: string,
  status: ReportingDayStatus,
  overrides: Partial<ReportingDayDto> = {},
): ReportingDayDto {
  const isToday = key === TODAY;
  const isFuture = key > TODAY;
  const isPast = key < TODAY;
  const isHoliday = status === 'Holiday';
  const isSubmitted = status === 'Submitted';
  const hasDraft = status === 'Draft' || overrides.hasDraft === true;
  const isSelectable = !isHoliday && !isFuture;
  return {
    dayKey: key,
    date: key,
    dayNameAr: DAY_NAMES[key] ?? 'يوم',
    fullDateLabel: `${DAY_NAMES[key] ?? 'يوم'} ${key}`,
    isToday,
    isPast,
    isFuture,
    isHoliday,
    isSelectable,
    isOpenForDraft: isSelectable,
    isDueToday: isToday && !isHoliday,
    isOverdue: status === 'Overdue',
    isSubmitted,
    hasDraft,
    status,
    statusLabel: STATUS_LABELS[status],
    lockReason: isHoliday
      ? 'لا تقارير يومية في العطلة الأسبوعية (الجمعة).'
      : isFuture
        ? 'لا يمكن إنشاء تقرير ليوم لم يبدأ بعد.'
        : null,
    previousDayKey: addKey(key, -1),
    nextDayKey: addKey(key, 1),
    ...overrides,
  };
}

const TODAY = ymd(2026, 7, 14); // الثلاثاء
const DAY_NAMES: Record<string, string> = {
  [ymd(2026, 7, 10)]: 'الجمعة',
  [ymd(2026, 7, 11)]: 'السبت',
  [ymd(2026, 7, 12)]: 'الأحد',
  [ymd(2026, 7, 13)]: 'الاثنين',
  [ymd(2026, 7, 14)]: 'الثلاثاء',
  [ymd(2026, 7, 15)]: 'الأربعاء',
};
const STATUS_LABELS: Record<ReportingDayStatus, string> = {
  Available: 'متاح للتسليم',
  Draft: 'مسودّة غير مُرسَلة',
  Submitted: 'مُرسَل',
  Overdue: 'متأخّر — لم يُرسَل',
  Holiday: 'عطلة أسبوعية',
  FutureLocked: 'يوم لم يبدأ بعد',
  Returned: 'مُعاد للتعديل',
  Reopened: 'أُعيد فتحه',
};

const WINDOW: ReportingDayDto[] = [
  day(ymd(2026, 7, 10), 'Holiday'), // الجمعة — العطلة الأسبوعية الوحيدة
  day(ymd(2026, 7, 11), 'Overdue'), // السبت — يوم عمل يوميّ كامل (ليس عطلة)
  day(ymd(2026, 7, 12), 'Overdue'),
  day(ymd(2026, 7, 13), 'Submitted'),
  day(ymd(2026, 7, 14), 'Available'),
  day(ymd(2026, 7, 15), 'FutureLocked'),
];

function daysData(): MyDaysDto {
  return {
    templateId: null,
    role: 'Employee',
    roleLabel: 'موظّف',
    currentDayKey: TODAY,
    today: TODAY,
    days: WINDOW,
  };
}

function mockSuccess() {
  mockUseReportingDays.mockReturnValue({
    data: daysData(),
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  });
}

// غلاف يحفظ القيمة المختارة كي يعكس التنقّل تغيّر التسمية (كما في SubmissionsPage).
function Harness({ onChange }: { onChange?: (k: string, d: ReportingDayDto) => void }) {
  const [value, setValue] = useState<string | null>(null);
  return (
    <DailyCalendarPicker
      value={value}
      onChange={(k, d) => {
        setValue(k);
        onChange?.(k, d);
      }}
    />
  );
}

beforeEach(() => {
  mockUseReportingDays.mockReset();
});

describe('DailyCalendarPicker', () => {
  it('لا يعرض إطلاقًا حقل إدخال تاريخ نصّيّ (input[type=date])', () => {
    mockSuccess();
    const { container } = render(<Harness />);
    expect(container.querySelector('input[type="date"]')).toBeNull();
    expect(container.querySelector('input')).toBeNull();
  });

  it('يحدّد اليوم الحاليّ تلقائيًّا عند أوّل تحميل ويعرض اسمه وتاريخه', () => {
    mockSuccess();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    expect(onChange).toHaveBeenCalledWith(TODAY, expect.objectContaining({ dayKey: TODAY, isToday: true }));
    expect(screen.getAllByText(`الثلاثاء ${TODAY}`).length).toBeGreaterThan(0);
    expect(screen.getAllByText('متاح للتسليم').length).toBeGreaterThan(0);
  });

  it('زرّا اليوم السابق/التالي ينقلان لليوم المجاور (بمفاتيح الخادم)', () => {
    mockSuccess();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    onChange.mockClear();

    fireEvent.click(screen.getByLabelText('اليوم السابق'));
    expect(onChange).toHaveBeenCalledWith(ymd(2026, 7, 13), expect.objectContaining({ status: 'Submitted' }));
    expect(screen.getAllByText('مُرسَل').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByLabelText('اليوم التالي'));
    expect(onChange).toHaveBeenLastCalledWith(TODAY, expect.objectContaining({ isToday: true }));
  });

  it('زرّ «العودة إلى اليوم» يعيد التحديد لليوم الحاليّ', () => {
    mockSuccess();
    render(<Harness />);
    // انتقل للماضي أولًا
    fireEvent.click(screen.getByLabelText('اليوم السابق'));
    expect(screen.getAllByText('مُرسَل').length).toBeGreaterThan(0);
    // ثم العودة لليوم
    fireEvent.click(screen.getByText('العودة إلى اليوم'));
    expect(screen.getAllByText('متاح للتسليم').length).toBeGreaterThan(0);
  });

  it('يعرض الشارة الصحيحة لكل حالة (متأخّر/عطلة/مستقبل مقفل)', () => {
    mockSuccess();
    render(<Harness />);
    // اليوم التالي = مستقبل مقفل
    fireEvent.click(screen.getByLabelText('اليوم التالي'));
    expect(screen.getAllByText('يوم لم يبدأ بعد').length).toBeGreaterThan(0);
    expect(screen.getByText('لا يمكن إنشاء تقرير ليوم لم يبدأ بعد.')).toBeInTheDocument();
  });

  it('تصحيح السبت: السبت يوم عمل قابل للاختيار وليس عطلة، والجمعة وحدها العطلة، والتنقّل الجمعة→السبت يعمل', () => {
    mockSuccess();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    onChange.mockClear();

    // انتقل من اليوم (الثلاثاء 07-14) رجوعًا حتى الجمعة 07-10.
    fireEvent.click(screen.getByLabelText('اليوم السابق')); // 07-13 الاثنين
    fireEvent.click(screen.getByLabelText('اليوم السابق')); // 07-12 الأحد
    fireEvent.click(screen.getByLabelText('اليوم السابق')); // 07-11 السبت
    fireEvent.click(screen.getByLabelText('اليوم السابق')); // 07-10 الجمعة

    // الجمعة عطلة: شارة العطلة + غير قابلة للاختيار.
    expect(onChange).toHaveBeenLastCalledWith(
      ymd(2026, 7, 10),
      expect.objectContaining({ status: 'Holiday', isHoliday: true, isSelectable: false }),
    );
    expect(screen.getAllByText('عطلة أسبوعية').length).toBeGreaterThan(0);

    // التنقّل الجمعة→السبت: السبت يوم عمل قابل للاختيار وليس عطلة، بلا نصّ «عطلة أسبوعية».
    fireEvent.click(screen.getByLabelText('اليوم التالي')); // 07-11 السبت
    expect(onChange).toHaveBeenLastCalledWith(
      ymd(2026, 7, 11),
      expect.objectContaining({ isHoliday: false, isSelectable: true }),
    );
    expect(screen.queryByText('عطلة أسبوعية')).toBeNull();
    expect(screen.getByText(`السبت ${ymd(2026, 7, 11)}`)).toBeInTheDocument();
  });

  it('عند فشل الواجهة لا ارتداد محليّ: رسالة + إعادة محاولة، ولا يُستدعى onChange (يبقى الإنشاء معطّلًا)', () => {
    mockUseReportingDays.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: vi.fn(),
    });
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    expect(screen.getByText('إعادة المحاولة')).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
    // لا يوجد أيّ تحديد يوم → لا تسمية يوم كاملة
    expect(screen.queryByLabelText('اليوم السابق')).toBeNull();
  });
});

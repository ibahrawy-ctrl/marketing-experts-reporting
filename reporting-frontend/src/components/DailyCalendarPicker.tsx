import { useEffect, useMemo, useState } from 'react';
import { Alert, Badge, Button, Spinner } from './ui';
import { useReportingDays } from '../lib/useReportingCalendar';
import type { ReportingDayDto } from '../types/api';

// ROLE-AWARE-REPORTING-CALENDAR — الوضع اليوميّ (Daily). منتقي يوم تقريريّ مُدرِك للدور والحالة،
// بديل دائم لـ<input type="date">. لا إدخال نصّيّ ولا حساب تاريخ محليّ — كل الأيام ومفاتيحها وحالاتها
// محسوبة خادميًّا عبر GET /api/reporting-calendar/my-days. عند فشل الواجهة لا ارتداد محليّ: رسالة + إعادة محاولة.

export interface DailyCalendarPickerProps {
  templateId?: string | null;
  value: string | null; // مفتاح اليوم المختار YYYY-MM-DD
  onChange: (dayKey: string, day: ReportingDayDto) => void;
  // تحديد اليوم الحاليّ تلقائيًّا عند أوّل تحميل حين لا توجد قيمة مختارة.
  autoSelectCurrent?: boolean;
}

function badgeTone(status: string): 'success' | 'orange' | 'alert' | 'gold' | 'muted' | 'navy' {
  switch (status) {
    case 'Available':
      return 'success';
    case 'Submitted':
      return 'navy';
    case 'Draft':
      return 'orange';
    case 'Overdue':
      return 'alert';
    case 'Returned':
      return 'gold';
    case 'Holiday':
    case 'FutureLocked':
    default:
      return 'muted';
  }
}

export function DailyCalendarPicker({
  templateId = null,
  value,
  onChange,
  autoSelectCurrent = true,
}: DailyCalendarPickerProps) {
  // نقطة الارتكاز للاستعلام تتبع اليوم المختار كي تُعيد النافذة توسيطها عند التنقّل بعيدًا.
  const [anchor, setAnchor] = useState<string | null>(value);
  const query = useReportingDays({ templateId, anchorDate: anchor ?? value });
  const days = query.data?.days ?? [];

  const selectedDay = useMemo(
    () => days.find((d) => d.dayKey === value) ?? null,
    [days, value],
  );
  const currentDay = useMemo(() => days.find((d) => d.isToday) ?? null, [days]);

  // اختيار اليوم الحاليّ تلقائيًّا عند أوّل تحميل إن لم تكن هناك قيمة.
  useEffect(() => {
    if (autoSelectCurrent && !value && currentDay) {
      onChange(currentDay.dayKey, currentDay);
      setAnchor(currentDay.dayKey);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentDay, autoSelectCurrent]);

  if (query.isLoading) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-line bg-white p-4 text-sm text-ink-2" dir="rtl">
        <Spinner /> يتم تحميل أيام التقارير…
      </div>
    );
  }
  if (query.isError || !query.data) {
    return (
      <Alert tone="alert">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span>تعذّر تحميل تقويم الأيام. لا يمكن إنشاء تقرير قبل تحميل يوم صالح.</span>
          <Button variant="ghost" onClick={() => query.refetch()}>إعادة المحاولة</Button>
        </div>
      </Alert>
    );
  }

  // التنقّل عبر مفاتيح الأيام المجاورة من الخادم (لا حساب تاريخ محليّ). النافذة تُعاد توسيطها على اليوم الجديد.
  function goTo(dayKey: string) {
    setAnchor(dayKey);
    const found = days.find((d) => d.dayKey === dayKey);
    if (found) onChange(dayKey, found);
    else onChange(dayKey, selectedDay ?? currentDay!); // ستُحدَّث التفاصيل بعد إعادة الجلب
  }

  const prevKey = selectedDay?.previousDayKey ?? null;
  const nextKey = selectedDay?.nextDayKey ?? null;
  const atToday = selectedDay?.isToday ?? false;

  return (
    <div className="rounded-lg border border-line bg-white p-3" dir="rtl">
      {/* ترويسة: الدور */}
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="font-semibold text-navy">تقويم التقارير اليومية</span>
        <span className="text-xs text-ink-3">دورك: {query.data.roleLabel}</span>
      </div>

      {/* شريط التنقّل: اليوم السابق ← التسمية الكاملة ← اليوم التالي */}
      <div className="flex items-center justify-between gap-2">
        <Button
          variant="ghost"
          className="px-3 py-2 text-sm"
          aria-label="اليوم السابق"
          disabled={!prevKey}
          onClick={() => prevKey && goTo(prevKey)}
        >
          ◀ اليوم السابق
        </Button>

        <div className="min-w-0 flex-1 text-center" role="status" aria-live="polite">
          <div className="truncate text-base font-bold text-ink">
            {selectedDay ? selectedDay.fullDateLabel : '—'}
          </div>
          {selectedDay && (
            <div className="mt-1 flex items-center justify-center gap-2">
              <Badge tone={badgeTone(selectedDay.status)}>{selectedDay.statusLabel}</Badge>
              {selectedDay.isToday && <span className="text-xs text-orange-600">اليوم</span>}
            </div>
          )}
        </div>

        <Button
          variant="ghost"
          className="px-3 py-2 text-sm"
          aria-label="اليوم التالي"
          disabled={!nextKey}
          onClick={() => nextKey && goTo(nextKey)}
        >
          اليوم التالي ▶
        </Button>
      </div>

      {/* العودة إلى اليوم */}
      <div className="mt-3 flex items-center justify-center border-t border-line pt-3">
        <Button
          variant="ghost"
          className="px-3 py-1 text-xs"
          disabled={atToday || !currentDay}
          onClick={() => currentDay && goTo(currentDay.dayKey)}
        >
          العودة إلى اليوم
        </Button>
      </div>

      {/* تفاصيل اليوم المختار + سبب القفل إن وُجد */}
      {selectedDay ? (
        <div className="mt-3 rounded-lg bg-navy-50 p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-semibold text-navy">{selectedDay.dayNameAr} — {selectedDay.dayKey}</span>
            <Badge tone={badgeTone(selectedDay.status)}>{selectedDay.statusLabel}</Badge>
          </div>
          {selectedDay.lockReason && (
            <p className="mt-1 text-xs text-alert">{selectedDay.lockReason}</p>
          )}
          {selectedDay.isOverdue && !selectedDay.lockReason && (
            <p className="mt-1 text-xs text-alert">تجاوز هذا اليوم موعده دون إرسال.</p>
          )}
        </div>
      ) : (
        <Alert tone="navy">لا يوجد يوم محدَّد.</Alert>
      )}
    </div>
  );
}

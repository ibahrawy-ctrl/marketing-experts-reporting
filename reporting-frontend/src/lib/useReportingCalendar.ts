import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type { MyCyclesDto, MyDaysDto, ReportingCalendarContext } from '../types/api';

// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.5. مصدر الدورات الوحيد للواجهة.
// يجلب دورات المستخدم الحاليّ (ماضٍ محدود + الحالية + مستقبل محدود) محسوبةً خادميًّا بحسب دوره الأساسيّ.
// الواجهة لا ترسل الدور ولا تعيد حساب أيّ مفتاح دورة — تقرأ ما يُعيده الخادم فقط.
export interface UseReportingCalendarOptions {
  context?: ReportingCalendarContext;
  templateId?: string | null;
  past?: number;
  future?: number;
  enabled?: boolean;
}

export function useReportingCalendar(options: UseReportingCalendarOptions = {}) {
  const { context = 'Report', templateId = null, past, future, enabled = true } = options;

  return useQuery({
    queryKey: ['reporting-calendar', 'my-cycles', context, templateId ?? null, past ?? null, future ?? null],
    queryFn: async () => {
      const params: Record<string, string> = { context };
      if (templateId) params.templateId = templateId;
      if (past != null) params.past = String(past);
      if (future != null) params.future = String(future);
      return (await api.get<MyCyclesDto>('/reporting-calendar/my-cycles', { params })).data;
    },
    enabled,
  });
}

// ROLE-AWARE-REPORTING-CALENDAR — الوضع اليوميّ (Daily). مصدر الأيام الوحيد للواجهة.
// يجلب نافذة أيام المستخدم (ماضٍ قريب + اليوم + مستقبل مسموح) محسوبةً خادميًّا مع حالة كل يوم.
// الواجهة لا تحسب أيّ مفتاح يوم محليًّا ولا تسمح بإدخال تاريخ يدويّ — تقرأ ما يُعيده الخادم فقط.
export interface UseReportingDaysOptions {
  templateId?: string | null;
  anchorDate?: string | null;
  previousCount?: number;
  nextCount?: number;
  enabled?: boolean;
}

export function useReportingDays(options: UseReportingDaysOptions = {}) {
  const { templateId = null, anchorDate = null, previousCount, nextCount, enabled = true } = options;

  return useQuery({
    queryKey: [
      'reporting-calendar',
      'my-days',
      templateId ?? null,
      anchorDate ?? null,
      previousCount ?? null,
      nextCount ?? null,
    ],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (templateId) params.templateId = templateId;
      if (anchorDate) params.anchorDate = anchorDate;
      if (previousCount != null) params.previousCount = String(previousCount);
      if (nextCount != null) params.nextCount = String(nextCount);
      return (await api.get<MyDaysDto>('/reporting-calendar/my-days', { params })).data;
    },
    enabled,
  });
}

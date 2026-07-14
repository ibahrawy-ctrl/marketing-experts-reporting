import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type { MyCyclesDto, ReportingCalendarContext } from '../types/api';

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

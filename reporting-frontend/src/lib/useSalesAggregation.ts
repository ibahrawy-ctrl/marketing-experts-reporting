import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type { AggregationFilter, B2bAggregationReport, B2bSourceReport, B2cAggregationReport, B2cCourseGroupedReport, B2cNewOldReport, SalesContextDto } from '../types/api';

// مُرشِّحات محرّك التجميع (ERDS Phase 4). كلّها اختيارية؛ النطاق مفروض خادميًّا عبر IScopeResolver.
function toParams(filter: AggregationFilter): Record<string, string> {
  const params: Record<string, string> = {};
  if (filter.periodType) params.periodType = filter.periodType;
  if (filter.periodKey) params.periodKey = filter.periodKey;
  if (filter.employeeId) params.employeeId = filter.employeeId;
  if (filter.teamId) params.teamId = filter.teamId;
  if (filter.departmentId) params.departmentId = filter.departmentId;
  if (filter.item) params.course = filter.item; // B2C = course
  return params;
}

// تجميع مبيعات B2C حسب الدورة (قراءة فقط — لا يغيّر أيّ تسليم/قالب/مسار اعتماد).
export function useB2cAggregation(filter: AggregationFilter) {
  return useQuery({
    queryKey: ['aggregation-b2c', filter],
    queryFn: async () =>
      (await api.get<B2cAggregationReport>('/reporting/aggregation/b2c', { params: toParams(filter) })).data,
  });
}

// تجميع مبيعات B2C مجموعةً حسب الدورة (العرض الافتراضي للمدير) + تفصيل الموظّفين (Drill-down).
// enabled: يمنع الجلب حين لا يُعرَض قسم B2C (مثل قائد فريق B2B) — تفاديًا لتحميل بيانات لا حاجة لها.
export function useB2cCourseGrouped(filter: AggregationFilter, enabled = true) {
  return useQuery({
    queryKey: ['aggregation-b2c-by-course', filter],
    enabled,
    queryFn: async () =>
      (await api.get<B2cCourseGroupedReport>('/reporting/aggregation/b2c/by-course', { params: toParams(filter) })).data,
  });
}

// تجميع مبيعات B2C مفصولًا إلى بيانات جديدة New / بيانات CRM قديمة Old (Phase 7، قراءة فقط).
export function useB2cNewOld(filter: AggregationFilter, enabled = true) {
  return useQuery({
    queryKey: ['aggregation-b2c-new-old', filter],
    enabled,
    queryFn: async () =>
      (await api.get<B2cNewOldReport>('/reporting/aggregation/b2c/new-old', { params: toParams(filter) })).data,
  });
}

// سياق المبيعات الموثوق (RC-3 Task 1.1) — يُحسَب خادميًّا من المسمّى الوظيفي ومسمّيات أعضاء النطاق.
// يحدّد أيّ أقسام تُعرَض (B2C/B2B) وهل المستخدم مندوب فردي. لا يقرأ أيّ تسليم.
export function useSalesContext() {
  return useQuery({
    queryKey: ['sales-context'],
    queryFn: async () =>
      (await api.get<SalesContextDto>('/reporting/aggregation/sales-context')).data,
  });
}

// تجميع مبيعات B2B حسب الخدمة (قراءة فقط). الفلتر item يُرسَل كـ service.
// enabled: يمنع الجلب حين لا يُعرَض قسم B2B (مثل قائد فريق B2C) — تفاديًا لتحميل بيانات لا حاجة لها.
export function useB2bAggregation(filter: AggregationFilter, enabled = true) {
  const params = { ...toParams(filter) };
  if (filter.item) {
    delete params.course;
    params.service = filter.item;
  }
  return useQuery({
    queryKey: ['aggregation-b2b', filter],
    enabled,
    queryFn: async () =>
      (await api.get<B2bAggregationReport>('/reporting/aggregation/b2b', { params })).data,
  });
}

// تجميع مبيعات B2B مفصولًا حسب المصدر New Leads / Data Scraping / Legacy (RC-3 Task 2A، قراءة فقط).
// الفلتر item يُرسَل كـ service. enabled يمنع الجلب حين لا يُعرَض قسم B2B.
export function useB2bBySource(filter: AggregationFilter, enabled = true) {
  const params = { ...toParams(filter) };
  if (filter.item) {
    delete params.course;
    params.service = filter.item;
  }
  return useQuery({
    queryKey: ['aggregation-b2b-by-source', filter],
    enabled,
    queryFn: async () =>
      (await api.get<B2bSourceReport>('/reporting/aggregation/b2b/by-source', { params })).data,
  });
}

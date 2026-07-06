// RPT-DUE1 — هوكات قراءة مواعيد التقارير والتأخّر (محسوبة عند الطلب، بلا بريد/إشعارات).
// my-status: self-only لأيّ موظّف. overview/overdue: ضمن نطاق المستخدم (ScopeResolver خادميًّا).
import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  ReportDueMyStatus,
  ReportDueOverview,
  ReportDueOverdueReport,
} from '../types/api';

export interface ReportDueScopeFilter {
  weekKey?: string;
  departmentId?: string;
  teamId?: string;
}

export function useReportDueMyStatus(weekKey?: string, enabled = true) {
  return useQuery({
    queryKey: ['report-due-my-status', weekKey ?? null],
    enabled,
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (weekKey) params.weekKey = weekKey;
      return (await api.get<ReportDueMyStatus>('/reports/due/my-status', { params })).data;
    },
  });
}

export function useReportDueOverview(filter: ReportDueScopeFilter = {}, enabled = true) {
  return useQuery({
    queryKey: ['report-due-overview', filter],
    enabled,
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.weekKey) params.weekKey = filter.weekKey;
      if (filter.departmentId) params.departmentId = filter.departmentId;
      if (filter.teamId) params.teamId = filter.teamId;
      return (await api.get<ReportDueOverview>('/reports/due/overview', { params })).data;
    },
  });
}

export function useReportDueOverdue(filter: ReportDueScopeFilter = {}, enabled = true) {
  return useQuery({
    queryKey: ['report-due-overdue', filter],
    enabled,
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.weekKey) params.weekKey = filter.weekKey;
      if (filter.departmentId) params.departmentId = filter.departmentId;
      if (filter.teamId) params.teamId = filter.teamId;
      return (await api.get<ReportDueOverdueReport>('/reports/due/overdue', { params })).data;
    },
  });
}

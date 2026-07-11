import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  PeriodType,
  ProjectFirstByClientRow,
  ProjectFirstByEmployeeRow,
  ProjectFirstByPodRow,
  ProjectFirstByProjectRow,
  ProjectFirstExecutionReport,
} from '../types/api';

// مُرشِّح محرّك التجميع Project-First (RC-4 Task 4، Path A). كلّه اختياري؛ النطاق مفروض خادميًّا عبر IScopeResolver.
// ClientId/ProjectId تصفية على المعرّفات الحقيقية (لا الاسم النصّي). Pod = فريق المُسلِّم.
export interface ProjectExecutionFilter {
  periodType?: PeriodType;
  periodKey?: string;
  teamId?: string;
  employeeId?: string;
  clientId?: string;
  projectId?: string;
}

function toParams(filter: ProjectExecutionFilter): Record<string, string> {
  const params: Record<string, string> = {};
  if (filter.periodType) params.periodType = filter.periodType;
  if (filter.periodKey) params.periodKey = filter.periodKey;
  if (filter.teamId) params.teamId = filter.teamId;
  if (filter.employeeId) params.employeeId = filter.employeeId;
  if (filter.clientId) params.clientId = filter.clientId;
  if (filter.projectId) params.projectId = filter.projectId;
  return params;
}

// تجميع لكل مشروع ضمن نطاق المستخدم.
export function useExecutionByProject(filter: ProjectExecutionFilter, enabled = true) {
  return useQuery({
    queryKey: ['project-execution-projects', filter],
    enabled,
    queryFn: async () =>
      (await api.get<ProjectFirstExecutionReport<ProjectFirstByProjectRow>>(
        '/reporting/project-execution/projects',
        { params: toParams(filter) },
      )).data,
  });
}

// تجميع لكل (موظّف، مشروع) — للتفصيل داخل عرض قائد الفريق/المدير.
export function useExecutionByEmployee(filter: ProjectExecutionFilter, enabled = true) {
  return useQuery({
    queryKey: ['project-execution-employees', filter],
    enabled,
    queryFn: async () =>
      (await api.get<ProjectFirstExecutionReport<ProjectFirstByEmployeeRow>>(
        '/reporting/project-execution/employees',
        { params: toParams(filter) },
      )).data,
  });
}

// تجميع لكل Pod (فريق المُسلِّم) ضمن نطاق المستخدم.
export function useExecutionByPod(filter: ProjectExecutionFilter, enabled = true) {
  return useQuery({
    queryKey: ['project-execution-pods', filter],
    enabled,
    queryFn: async () =>
      (await api.get<ProjectFirstExecutionReport<ProjectFirstByPodRow>>(
        '/reporting/project-execution/pods',
        { params: toParams(filter) },
      )).data,
  });
}

// تجميع لكل عميل ضمن نطاق المستخدم.
export function useExecutionByClient(filter: ProjectExecutionFilter, enabled = true) {
  return useQuery({
    queryKey: ['project-execution-clients', filter],
    enabled,
    queryFn: async () =>
      (await api.get<ProjectFirstExecutionReport<ProjectFirstByClientRow>>(
        '/reporting/project-execution/clients',
        { params: toParams(filter) },
      )).data,
  });
}

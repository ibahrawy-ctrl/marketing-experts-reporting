import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  CreateProjectWorkstreamRequest,
  ProjectWorkstreamDto,
  UpdateProjectWorkstreamRequest,
} from '../types/api';

// تيّارات العمل داخل المشروع (P1). القراءة مُنَطَّقة خادميًّا بنطاق رؤية المشروع؛ الكتابة محكومة بسياسة الإدارة.
// لا حذف نهائيّ — تفعيل/تعطيل فقط.

function key(projectId: string | undefined, includeInactive: boolean) {
  return ['project-workstreams', projectId, includeInactive] as const;
}

function invalidate(qc: QueryClient, projectId: string) {
  qc.invalidateQueries({ queryKey: ['project-workstreams', projectId] });
}

export function useProjectWorkstreams(projectId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: key(projectId, includeInactive),
    enabled: !!projectId,
    queryFn: async () =>
      (await api.get<ProjectWorkstreamDto[]>(`/projects/${projectId}/workstreams`, {
        params: { includeInactive },
      })).data,
  });
}

export function useCreateProjectWorkstream(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateProjectWorkstreamRequest) =>
      (await api.post<ProjectWorkstreamDto>(`/projects/${projectId}/workstreams`, req)).data,
    onSuccess: () => invalidate(qc, projectId),
  });
}

export function useUpdateProjectWorkstream(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateProjectWorkstreamRequest }) =>
      (await api.put<ProjectWorkstreamDto>(`/projects/${projectId}/workstreams/${id}`, req)).data,
    onSuccess: () => invalidate(qc, projectId),
  });
}

export function useActivateProjectWorkstream(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ProjectWorkstreamDto>(`/projects/${projectId}/workstreams/${id}/activate`)).data,
    onSuccess: () => invalidate(qc, projectId),
  });
}

export function useDeactivateProjectWorkstream(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ProjectWorkstreamDto>(`/projects/${projectId}/workstreams/${id}/deactivate`)).data,
    onSuccess: () => invalidate(qc, projectId),
  });
}

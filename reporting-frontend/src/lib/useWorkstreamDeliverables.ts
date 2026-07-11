import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  CreateWorkstreamDeliverableRequest,
  UpdateWorkstreamDeliverableRequest,
  WorkstreamDeliverableDto,
} from '../types/api';

// مخرَجات خطّة الإنتاج داخل تيار العمل (P2). القراءة مُنَطَّقة خادميًّا بنطاق رؤية المشروع؛
// الكتابة محكومة بسياسة الإدارة. لا حذف نهائيّ — تفعيل/تعطيل فقط. تخطيط فقط بلا تنفيذ.

function base(projectId: string, workstreamId: string) {
  return `/projects/${projectId}/workstreams/${workstreamId}/deliverables`;
}

function key(projectId: string, workstreamId: string, includeInactive: boolean) {
  return ['workstream-deliverables', projectId, workstreamId, includeInactive] as const;
}

function invalidate(qc: QueryClient, projectId: string, workstreamId: string) {
  qc.invalidateQueries({ queryKey: ['workstream-deliverables', projectId, workstreamId] });
}

export function useWorkstreamDeliverables(
  projectId: string,
  workstreamId: string,
  includeInactive = false,
  enabled = true,
) {
  return useQuery({
    queryKey: key(projectId, workstreamId, includeInactive),
    enabled: enabled && !!projectId && !!workstreamId,
    queryFn: async () =>
      (await api.get<WorkstreamDeliverableDto[]>(base(projectId, workstreamId), {
        params: { includeInactive },
      })).data,
  });
}

export function useCreateWorkstreamDeliverable(projectId: string, workstreamId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateWorkstreamDeliverableRequest) =>
      (await api.post<WorkstreamDeliverableDto>(base(projectId, workstreamId), req)).data,
    onSuccess: () => invalidate(qc, projectId, workstreamId),
  });
}

export function useUpdateWorkstreamDeliverable(projectId: string, workstreamId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateWorkstreamDeliverableRequest }) =>
      (await api.put<WorkstreamDeliverableDto>(`${base(projectId, workstreamId)}/${id}`, req)).data,
    onSuccess: () => invalidate(qc, projectId, workstreamId),
  });
}

export function useActivateWorkstreamDeliverable(projectId: string, workstreamId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<WorkstreamDeliverableDto>(`${base(projectId, workstreamId)}/${id}/activate`)).data,
    onSuccess: () => invalidate(qc, projectId, workstreamId),
  });
}

export function useDeactivateWorkstreamDeliverable(projectId: string, workstreamId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<WorkstreamDeliverableDto>(`${base(projectId, workstreamId)}/${id}/deactivate`)).data,
    onSuccess: () => invalidate(qc, projectId, workstreamId),
  });
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  PositionDto,
  PositionPermissionOptionDto,
  UserPositionDto,
  CreatePositionRequest,
  AddPositionScopeRequest,
} from '../types/api';

// إدارة المناصب المرنة (Phase 1A — رؤية فقط) — Admin فقط.
export function usePositions() {
  return useQuery({
    queryKey: ['positions'],
    queryFn: async () => (await api.get<PositionDto[]>('/positions')).data,
  });
}

export function usePositionPermissionOptions() {
  return useQuery({
    queryKey: ['position-permission-options'],
    queryFn: async () =>
      (await api.get<PositionPermissionOptionDto[]>('/positions/permission-options')).data,
  });
}

// المناصب المُسنَدة لمستخدم معيّن (لصفحة المستخدمين).
export function useUserPositions(userId: string | null) {
  return useQuery({
    queryKey: ['user-positions', userId],
    enabled: !!userId,
    queryFn: async () => (await api.get<UserPositionDto[]>(`/users/${userId}/positions`)).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['positions'] });
  qc.invalidateQueries({ queryKey: ['user-positions'] });
}

export function useCreatePosition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreatePositionRequest) =>
      (await api.post<PositionDto>('/positions', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useUpdatePosition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: CreatePositionRequest }) =>
      (await api.put<PositionDto>(`/positions/${id}`, req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useSetPositionActive() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) =>
      (await api.post<PositionDto>(`/positions/${id}/${isActive ? 'enable' : 'disable'}`, {})).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useAddPositionPermission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, permissionKey }: { id: string; permissionKey: string }) =>
      (await api.post<PositionDto>(`/positions/${id}/permissions`, { permissionKey })).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useRemovePositionPermission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, permissionKey }: { id: string; permissionKey: string }) =>
      (await api.delete<PositionDto>(`/positions/${id}/permissions/${permissionKey}`)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useAddPositionScope() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AddPositionScopeRequest }) =>
      (await api.post<PositionDto>(`/positions/${id}/scopes`, req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useRemovePositionScope() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, scopeId }: { id: string; scopeId: string }) =>
      (await api.delete<PositionDto>(`/positions/${id}/scopes/${scopeId}`)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useAssignPosition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, userId }: { id: string; userId: string }) =>
      (await api.post(`/positions/${id}/assign`, { userId })).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useRevokePosition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, userId }: { id: string; userId: string }) =>
      (await api.post(`/positions/${id}/revoke`, { userId })).data,
    onSuccess: () => invalidate(qc),
  });
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  GovernanceItemListItemDto,
  GovernanceItemDetailDto,
  GovernanceItemStatus,
  GovernanceCategory,
  GovernanceSeverity,
  CreateGovernanceItemRequest,
  UpdateGovernanceItemRequest,
  ChangeGovernanceItemStatusRequest,
  AddGovernanceItemCommentRequest,
  GovernanceDirectoryDto,
} from '../types/api';

// فلاتر قائمة بنود ورشة الحوكمة (GOV-GOVERNANCE-UX1).
export interface GovernanceItemsFilter {
  status?: GovernanceItemStatus;
  category?: GovernanceCategory;
  severity?: GovernanceSeverity;
  assignedToUserId?: string;
  departmentId?: string;
  teamId?: string;
  openOnly?: boolean;
}

export function useGovernanceItems(filter: GovernanceItemsFilter) {
  return useQuery({
    queryKey: ['governance-items', filter],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.status) params.status = filter.status;
      if (filter.category) params.category = filter.category;
      if (filter.severity) params.severity = filter.severity;
      if (filter.assignedToUserId) params.assignedToUserId = filter.assignedToUserId;
      if (filter.departmentId) params.departmentId = filter.departmentId;
      if (filter.teamId) params.teamId = filter.teamId;
      if (filter.openOnly) params.openOnly = 'true';
      return (await api.get<GovernanceItemListItemDto[]>('/governance/items', { params })).data;
    },
  });
}

// دليل ورشة الحوكمة الموحّد (GOV-DIRECTORY-SCOPE-FIX-R1): قوائم المستخدمين/الإدارات/الفِرق ضمن نطاق الملكية
// — المصدر الوحيد لاختيار المُسنَد إليه/المتعلَّق في الورشة (بدل HR Directory العام).
export function useGovernanceWorkspaceDirectory(enabled = true) {
  return useQuery({
    queryKey: ['governance-workspace-directory'],
    enabled,
    staleTime: 5 * 60 * 1000,
    queryFn: async () =>
      (await api.get<GovernanceDirectoryDto>('/governance/items/directory')).data,
  });
}

export function useGovernanceItem(id: string | null) {
  return useQuery({
    queryKey: ['governance-item', id],
    enabled: !!id,
    queryFn: async () => (await api.get<GovernanceItemDetailDto>(`/governance/items/${id}`)).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>, id?: string) {
  qc.invalidateQueries({ queryKey: ['governance-items'] });
  if (id) qc.invalidateQueries({ queryKey: ['governance-item', id] });
}

export function useCreateGovernanceItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateGovernanceItemRequest) =>
      (await api.post<GovernanceItemDetailDto>('/governance/items', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useUpdateGovernanceItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateGovernanceItemRequest }) =>
      (await api.put<GovernanceItemDetailDto>(`/governance/items/${id}`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useChangeGovernanceItemStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ChangeGovernanceItemStatusRequest }) =>
      (await api.post<GovernanceItemDetailDto>(`/governance/items/${id}/status`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useAddGovernanceItemComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AddGovernanceItemCommentRequest }) =>
      (await api.post<GovernanceItemDetailDto>(`/governance/items/${id}/comments`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

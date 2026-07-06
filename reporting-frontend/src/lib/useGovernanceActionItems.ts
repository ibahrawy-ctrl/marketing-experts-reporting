import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  GovernanceActionItemListItemDto,
  GovernanceActionItemDetailDto,
  ActionItemStatus,
  ActionItemPriority,
  ActionItemSourceType,
  CreateGovernanceActionItemRequest,
  ChangeGovernanceActionItemStatusRequest,
  AssignGovernanceActionItemRequest,
  ChangeGovernanceActionItemDueDateRequest,
  AddGovernanceActionItemCommentRequest,
  CancelGovernanceActionItemRequest,
  ActionItemAssigneeDirectoryDto,
} from '../types/api';

// فلاتر قائمة إجراءات الحوكمة (GOV-ACTION-ITEMS-R1).
export interface GovernanceActionItemsFilter {
  status?: ActionItemStatus;
  assignedToUserId?: string;
  sourceType?: ActionItemSourceType;
  sourceId?: string;
  priority?: ActionItemPriority;
  dueFrom?: string;
  dueTo?: string;
  overdueOnly?: boolean;
  mineOnly?: boolean;
  assignedToMe?: boolean;
}

export function useGovernanceActionItems(filter: GovernanceActionItemsFilter) {
  return useQuery({
    queryKey: ['governance-action-items', filter],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.status) params.status = filter.status;
      if (filter.assignedToUserId) params.assignedToUserId = filter.assignedToUserId;
      if (filter.sourceType) params.sourceType = filter.sourceType;
      if (filter.sourceId) params.sourceId = filter.sourceId;
      if (filter.priority) params.priority = filter.priority;
      if (filter.dueFrom) params.dueFrom = filter.dueFrom;
      if (filter.dueTo) params.dueTo = filter.dueTo;
      if (filter.overdueOnly) params.overdueOnly = 'true';
      if (filter.mineOnly) params.mineOnly = 'true';
      if (filter.assignedToMe) params.assignedToMe = 'true';
      return (await api.get<GovernanceActionItemListItemDto[]>('/governance/action-items', { params })).data;
    },
  });
}

// دليل المُسنَد إليهم الآمن (على مستوى الشركة، بلا حسابات حسّاسة) لاختيار المُسنَد إليه.
export function useActionItemAssigneeDirectory(enabled = true) {
  return useQuery({
    queryKey: ['action-item-assignee-directory'],
    enabled,
    staleTime: 5 * 60 * 1000,
    queryFn: async () =>
      (await api.get<ActionItemAssigneeDirectoryDto>('/governance/action-items/assignee-directory')).data,
  });
}

export function useGovernanceActionItem(id: string | null) {
  return useQuery({
    queryKey: ['governance-action-item', id],
    enabled: !!id,
    queryFn: async () => (await api.get<GovernanceActionItemDetailDto>(`/governance/action-items/${id}`)).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>, id?: string) {
  qc.invalidateQueries({ queryKey: ['governance-action-items'] });
  if (id) qc.invalidateQueries({ queryKey: ['governance-action-item', id] });
}

export function useCreateGovernanceActionItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateGovernanceActionItemRequest) =>
      (await api.post<GovernanceActionItemDetailDto>('/governance/action-items', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useChangeGovernanceActionItemStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ChangeGovernanceActionItemStatusRequest }) =>
      (await api.post<GovernanceActionItemDetailDto>(`/governance/action-items/${id}/status`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useAssignGovernanceActionItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AssignGovernanceActionItemRequest }) =>
      (await api.post<GovernanceActionItemDetailDto>(`/governance/action-items/${id}/assign`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useChangeGovernanceActionItemDueDate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ChangeGovernanceActionItemDueDateRequest }) =>
      (await api.post<GovernanceActionItemDetailDto>(`/governance/action-items/${id}/due-date`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useAddGovernanceActionItemComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AddGovernanceActionItemCommentRequest }) =>
      (await api.post<GovernanceActionItemDetailDto>(`/governance/action-items/${id}/updates`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useCancelGovernanceActionItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: CancelGovernanceActionItemRequest }) =>
      (await api.post<GovernanceActionItemDetailDto>(`/governance/action-items/${id}/cancel`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

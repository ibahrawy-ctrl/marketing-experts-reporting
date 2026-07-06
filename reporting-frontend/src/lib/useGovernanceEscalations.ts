import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  GovernanceEscalationListItemDto,
  GovernanceEscalationDetailDto,
  GovernanceEscalationStatus,
  EscalationType,
  EscalationSeverity,
  EscalationTargetType,
  CreateGovernanceEscalationRequest,
  UpdateGovernanceEscalationRequest,
  ChangeGovernanceEscalationStatusRequest,
  AssignGovernanceEscalationRequest,
  AddGovernanceEscalationCommentRequest,
  ReopenGovernanceEscalationRequest,
  CloseGovernanceEscalationRequest,
  EscalationTargetDirectoryDto,
} from '../types/api';

// فلاتر قائمة التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1).
export interface GovernanceEscalationsFilter {
  status?: GovernanceEscalationStatus;
  type?: EscalationType;
  severity?: EscalationSeverity;
  targetType?: EscalationTargetType;
  assignedToUserId?: string;
  raisedByUserId?: string;
  targetUserId?: string;
  targetDepartmentId?: string;
  targetTeamId?: string;
  openOnly?: boolean;
  mine?: boolean;
  assignedToMe?: boolean;
}

export function useGovernanceEscalations(filter: GovernanceEscalationsFilter) {
  return useQuery({
    queryKey: ['governance-escalations', filter],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.status) params.status = filter.status;
      if (filter.type) params.type = filter.type;
      if (filter.severity) params.severity = filter.severity;
      if (filter.targetType) params.targetType = filter.targetType;
      if (filter.assignedToUserId) params.assignedToUserId = filter.assignedToUserId;
      if (filter.raisedByUserId) params.raisedByUserId = filter.raisedByUserId;
      if (filter.targetUserId) params.targetUserId = filter.targetUserId;
      if (filter.targetDepartmentId) params.targetDepartmentId = filter.targetDepartmentId;
      if (filter.targetTeamId) params.targetTeamId = filter.targetTeamId;
      if (filter.openOnly) params.openOnly = 'true';
      if (filter.mine) params.mine = 'true';
      if (filter.assignedToMe) params.assignedToMe = 'true';
      return (await api.get<GovernanceEscalationListItemDto[]>('/governance/escalations', { params })).data;
    },
  });
}

// دليل أهداف التصعيد الآمن (على مستوى الشركة، بلا حسابات حسّاسة) لاختيار الهدف عند الرفع المتقاطع.
export function useEscalationTargetDirectory(enabled = true) {
  return useQuery({
    queryKey: ['escalation-target-directory'],
    enabled,
    staleTime: 5 * 60 * 1000,
    queryFn: async () =>
      (await api.get<EscalationTargetDirectoryDto>('/governance/escalations/target-directory')).data,
  });
}

export function useGovernanceEscalation(id: string | null) {
  return useQuery({
    queryKey: ['governance-escalation', id],
    enabled: !!id,
    queryFn: async () => (await api.get<GovernanceEscalationDetailDto>(`/governance/escalations/${id}`)).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>, id?: string) {
  qc.invalidateQueries({ queryKey: ['governance-escalations'] });
  if (id) qc.invalidateQueries({ queryKey: ['governance-escalation', id] });
}

export function useCreateGovernanceEscalation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateGovernanceEscalationRequest) =>
      (await api.post<GovernanceEscalationDetailDto>('/governance/escalations', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useUpdateGovernanceEscalation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateGovernanceEscalationRequest }) =>
      (await api.put<GovernanceEscalationDetailDto>(`/governance/escalations/${id}`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useChangeGovernanceEscalationStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ChangeGovernanceEscalationStatusRequest }) =>
      (await api.post<GovernanceEscalationDetailDto>(`/governance/escalations/${id}/status`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useAssignGovernanceEscalation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AssignGovernanceEscalationRequest }) =>
      (await api.post<GovernanceEscalationDetailDto>(`/governance/escalations/${id}/assign`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useAddGovernanceEscalationComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: AddGovernanceEscalationCommentRequest }) =>
      (await api.post<GovernanceEscalationDetailDto>(`/governance/escalations/${id}/comments`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useReopenGovernanceEscalation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ReopenGovernanceEscalationRequest }) =>
      (await api.post<GovernanceEscalationDetailDto>(`/governance/escalations/${id}/reopen`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

export function useCloseGovernanceEscalation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: CloseGovernanceEscalationRequest }) =>
      (await api.post<GovernanceEscalationDetailDto>(`/governance/escalations/${id}/close`, req)).data,
    onSuccess: (_d, vars) => invalidate(qc, vars.id),
  });
}

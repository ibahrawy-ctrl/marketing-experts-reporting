import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  PayrollImpactListDto,
  PayrollImpactDetailDto,
  PayrollImpactReviewRequest,
  LeaveRequestType,
  LeaveRequestStatus,
  PayrollImpactType,
  PayrollImpactReviewStatus,
} from '../types/api';

// فلاتر قائمة التأثير على الراتب (FIN-L1). الافتراضي: المعتمَد نهائيًّا (HrApproved) فقط.
export interface PayrollImpactFilter {
  month?: string; // yyyy-MM
  employeeUserId?: string;
  departmentId?: string;
  teamId?: string;
  type?: LeaveRequestType;
  approvalStatus?: LeaveRequestStatus;
  allApprovalStatuses?: boolean;
  impactType?: PayrollImpactType;
  reviewStatus?: PayrollImpactReviewStatus;
}

export function usePayrollImpacts(filter: PayrollImpactFilter) {
  return useQuery({
    queryKey: ['payroll-impacts', filter],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filter.month) params.month = filter.month;
      if (filter.employeeUserId) params.employeeUserId = filter.employeeUserId;
      if (filter.departmentId) params.departmentId = filter.departmentId;
      if (filter.teamId) params.teamId = filter.teamId;
      if (filter.type) params.type = filter.type;
      if (filter.approvalStatus) params.approvalStatus = filter.approvalStatus;
      if (filter.allApprovalStatuses) params.allApprovalStatuses = 'true';
      if (filter.impactType) params.impactType = filter.impactType;
      if (filter.reviewStatus) params.reviewStatus = filter.reviewStatus;
      return (await api.get<PayrollImpactListDto>('/payroll/leave-impacts', { params })).data;
    },
  });
}

export function usePayrollImpact(leaveRequestId: string | null) {
  return useQuery({
    queryKey: ['payroll-impact', leaveRequestId],
    enabled: !!leaveRequestId,
    queryFn: async () =>
      (await api.get<PayrollImpactDetailDto>(`/payroll/leave-impacts/${leaveRequestId}`)).data,
  });
}

export function useReviewPayrollImpact() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ leaveRequestId, req }: { leaveRequestId: string; req: PayrollImpactReviewRequest }) =>
      (await api.patch<PayrollImpactDetailDto>(`/payroll/leave-impacts/${leaveRequestId}/review`, req)).data,
    onSuccess: (_data, vars) => {
      qc.invalidateQueries({ queryKey: ['payroll-impacts'] });
      qc.invalidateQueries({ queryKey: ['payroll-impact', vars.leaveRequestId] });
    },
  });
}

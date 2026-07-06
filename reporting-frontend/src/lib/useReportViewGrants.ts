import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { ReportViewGrantDto, CreateReportViewGrantRequest } from '../types/api';

// منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1) — إدارة المنح للأدمن فقط، عرض فقط، معزول.
export function useReportViewGrants(includeRevoked = false) {
  return useQuery({
    queryKey: ['report-view-grants', includeRevoked],
    queryFn: async () =>
      (await api.get<ReportViewGrantDto[]>('/report-view-grants', { params: { includeRevoked } })).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['report-view-grants'] });
}

export function useCreateReportViewGrant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateReportViewGrantRequest) =>
      (await api.post<ReportViewGrantDto>('/report-view-grants', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useRevokeReportViewGrant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.delete(`/report-view-grants/${id}`)).data,
    onSuccess: () => invalidate(qc),
  });
}

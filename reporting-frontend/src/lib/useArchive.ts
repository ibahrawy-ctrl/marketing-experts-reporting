// الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1) — هوكات قراءة العناصر المحذوفة إداريًّا
// (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid. تطابق سياسة ArchiveGovernanceAccess بالخادم
// (Admin/CEO/GM فقط)؛ الحماية الفعلية مفروضة خادميًّا.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  ArchiveDetailsDto,
  ArchiveItemType,
  ArchiveListFilter,
  ArchivePagedResult,
  RestoreRequest,
} from '../types/api';

const ARCHIVE_KEY = 'admin-archive';

export function useArchiveList(filter: ArchiveListFilter) {
  return useQuery({
    queryKey: [ARCHIVE_KEY, 'list', filter],
    queryFn: async () => {
      const params: Record<string, string | number> = {};
      if (filter.itemType) params.itemType = filter.itemType;
      if (filter.periodKey) params.periodKey = filter.periodKey;
      if (filter.employeeId) params.employeeId = filter.employeeId;
      params.page = filter.page ?? 1;
      params.pageSize = filter.pageSize ?? 20;
      const res = await api.get<ArchivePagedResult>('/admin/archive', { params });
      return res.data;
    },
  });
}

export function useArchiveDetails(itemType: ArchiveItemType | null, id: string | null) {
  return useQuery({
    queryKey: [ARCHIVE_KEY, 'details', itemType, id],
    enabled: !!itemType && !!id,
    queryFn: async () => {
      const seg = itemType === 'Report' ? 'report' : 'kpi';
      const res = await api.get<ArchiveDetailsDto>(`/admin/archive/${seg}/${id}`);
      return res.data;
    },
  });
}

export function useRestoreArchiveItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { itemType: ArchiveItemType; id: string; request: RestoreRequest }) => {
      const seg = input.itemType === 'Report' ? 'report' : 'kpi';
      const res = await api.post<ArchiveDetailsDto>(`/admin/archive/${seg}/${input.id}/restore`, input.request);
      return res.data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [ARCHIVE_KEY] });
    },
  });
}

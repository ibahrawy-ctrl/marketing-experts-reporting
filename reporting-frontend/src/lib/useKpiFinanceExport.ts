import { useQuery } from '@tanstack/react-query';
import { api, downloadFile } from './api';
import type { KpiFinanceExportDto, KpiFinanceExportFilter } from '../types/api';

// مُرشِّحات تصدير KPI للمالية (KPI-FIN1). السنة والربع إلزاميان؛ الباقي اختياري.
function toParams(filter: KpiFinanceExportFilter): Record<string, string> {
  const params: Record<string, string> = {
    year: String(filter.year),
    quarter: String(filter.quarter),
  };
  if (filter.departmentId) params.departmentId = filter.departmentId;
  if (filter.teamId) params.teamId = filter.teamId;
  if (filter.status) params.status = filter.status;
  return params;
}

// معاينة JSON (قراءة فقط — لا تُسجِّل حدث تدقيق ولا تغيّر أيّ تقييم).
export function useKpiFinanceExport(filter: KpiFinanceExportFilter) {
  return useQuery({
    queryKey: ['kpi-finance-export', filter],
    queryFn: async () =>
      (await api.get<KpiFinanceExportDto>('/kpi-evaluations/finance-export', { params: toParams(filter) })).data,
  });
}

// تنزيل CSV (UTF-8 + BOM). هذا المسار وحده يُسجِّل حدث kpi.finance_exported بالخادم.
export async function downloadKpiFinanceCsv(filter: KpiFinanceExportFilter): Promise<void> {
  const params = new URLSearchParams(toParams(filter)).toString();
  await downloadFile(
    `/kpi-evaluations/finance-export/csv?${params}`,
    `kpi-finance-export-${filter.year}-Q${filter.quarter}.csv`,
  );
}

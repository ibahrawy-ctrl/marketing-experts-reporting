// RPT-APPROVER-INTEGRITY-01 — هوكات سطح الإنقاذ الإداريّ: قراءة الاعتمادات العالقة (تسليمات
// مفتوحة لا تظهر في «بانتظار اعتمادي» لأيّ مستخدم) وتشغيل الإصلاح الرسميّ عليها.
// تطابق سياسة AdminReportDelete بالخادم (Admin/CEO/GM)؛ الحماية الفعليّة مفروضة خادميًّا.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { ApproverIntegrityReportDto, ApproverRepairReportDto } from '../types/api';

const APPROVER_INTEGRITY_KEY = 'approver-integrity';

export function useApproverIntegrity() {
  return useQuery({
    queryKey: [APPROVER_INTEGRITY_KEY, 'list'],
    queryFn: async () => {
      const res = await api.get<ApproverIntegrityReportDto>('/submissions/approver-integrity');
      return res.data;
    },
  });
}

export function useRepairApproverIntegrity() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dryRun: boolean) => {
      const res = await api.post<ApproverRepairReportDto>('/submissions/approver-integrity/repair', { dryRun });
      return res.data;
    },
    onSuccess: (data) => {
      // التخطيط (dryRun) لا يكتب شيئًا ⇒ لا يُبطل الكاش؛ التطبيق وحده يغيّر الحالة.
      if (!data.dryRun) qc.invalidateQueries({ queryKey: [APPROVER_INTEGRITY_KEY] });
    },
  });
}

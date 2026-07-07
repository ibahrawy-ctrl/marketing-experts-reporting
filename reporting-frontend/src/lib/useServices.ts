import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { ServiceDto } from '../types/api';

// كتالوج خدمات B2B النشطة — يغذّي منتقي «الخدمة» في قالب مبيعات B2B حسب الخدمة.
// قراءة فقط، متاح لأي مستخدم مصادَق (الموظّف يحتاجه عند تعبئة تقريره).
export function useActiveServices() {
  return useQuery({
    queryKey: ['services', 'active'],
    queryFn: async () => (await api.get<ServiceDto[]>('/services')).data,
  });
}

// ===== هوكات إدارة الخدمات (الأدمن/CEO/GM عبر سياسة TemplateGovernance) — تعيد استخدام نقاط النهاية القائمة =====

export interface ServiceWriteRequest {
  nameAr: string;
  nameEn: string | null;
  sortOrder: number;
}

function invalidateServices(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: ['services', 'active'] });
  qc.invalidateQueries({ queryKey: ['services', 'admin'] });
}

// قائمة الإدارة (كل الخدمات بما فيها المعطّلة).
export function useAdminServices() {
  return useQuery({
    queryKey: ['services', 'admin'],
    queryFn: async () => (await api.get<ServiceDto[]>('/admin/services')).data,
  });
}

export function useCreateService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: ServiceWriteRequest) =>
      (await api.post<ServiceDto>('/admin/services', req)).data,
    onSuccess: () => invalidateServices(qc),
  });
}

export function useUpdateService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: ServiceWriteRequest }) =>
      (await api.put<ServiceDto>(`/admin/services/${id}`, req)).data,
    onSuccess: () => invalidateServices(qc),
  });
}

export function useActivateService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ServiceDto>(`/admin/services/${id}/activate`)).data,
    onSuccess: () => invalidateServices(qc),
  });
}

// الحذف الناعم = تعطيل (التقارير القديمة تخزّن اسم الخدمة نصًّا فلا يتأثّر التاريخ).
export function useDeactivateService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ServiceDto>(`/admin/services/${id}/deactivate`)).data,
    onSuccess: () => invalidateServices(qc),
  });
}

// نتيجة الحذف الآمن: نهائيّ إن لم تُستخدَم الخدمة، وإلّا أرشفة (تعطيل) دون حذف.
export interface ServiceDeleteResult {
  hardDeleted: boolean;
  service: ServiceDto | null;
  message: string;
}

// حذف آمن: يحذف الخدمة نهائيًّا إن لم تُستخدَم في أي تقرير، وإلّا يؤرشفها (يعطّلها) — التقارير القديمة تبقى كما هي.
export function useDeleteService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.delete<ServiceDeleteResult>(`/admin/services/${id}`)).data,
    onSuccess: () => invalidateServices(qc),
  });
}

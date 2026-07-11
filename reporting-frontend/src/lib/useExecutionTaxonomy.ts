import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { ExecutionTaxonomyDto, ExecutionTaxonomyOptionDto } from '../types/api';

// إدارة كتالوج تصنيفات التنفيذ (الأدمن/CEO/GM عبر سياسة TemplateGovernance) — RC-4 Task 4D2.
// قراءة/إنشاء/تعديل/تفعيل/تعطيل فقط — لا حذف نهائيّ.

const KEY = ['execution-taxonomy', 'admin'] as const;

function invalidate(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: KEY });
}

// RC-4 Task 4D3 — خيارات نشطة لمجال واحد (NameAr مرتّبة) لملء التقارير.
// متاح لأيّ مستخدم مصادَق (endpoint قراءة منفصل عن الإدارة). enabled عند وجود domain فقط.
export function useTaxonomyOptions(domain: string | undefined) {
  return useQuery({
    queryKey: ['execution-taxonomy', 'options', domain] as const,
    enabled: !!domain,
    staleTime: 5 * 60 * 1000,
    queryFn: async () =>
      (await api.get<string[]>('/execution-taxonomy/options', { params: { domain } })).data,
  });
}

// P1 — خيارات مُفصَّلة (Code + NameAr) لمجال واحد للقوائم التي ترسل الرمز (نوع تيار العمل).
export function useTaxonomyOptionDetails(domain: string | undefined) {
  return useQuery({
    queryKey: ['execution-taxonomy', 'options-detailed', domain] as const,
    enabled: !!domain,
    staleTime: 5 * 60 * 1000,
    queryFn: async () =>
      (await api.get<ExecutionTaxonomyOptionDto[]>('/execution-taxonomy/options/detailed', { params: { domain } })).data,
  });
}

// القائمة الإدارية: كل القيم بما فيها المعطّلة (الفلترة بالمجال تتمّ في الواجهة).
export function useExecutionTaxonomyAdmin() {
  return useQuery({
    queryKey: KEY,
    queryFn: async () =>
      (await api.get<ExecutionTaxonomyDto[]>('/execution-taxonomy', { params: { includeInactive: true } })).data,
  });
}

export interface CreateExecutionTaxonomyRequest {
  domain: string;
  code: string;
  nameAr: string;
  nameEn: string | null;
  sortOrder: number;
}

export interface UpdateExecutionTaxonomyRequest {
  nameAr: string;
  nameEn: string | null;
  sortOrder: number;
}

export function useCreateExecutionTaxonomy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateExecutionTaxonomyRequest) =>
      (await api.post<ExecutionTaxonomyDto>('/execution-taxonomy', req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useUpdateExecutionTaxonomy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateExecutionTaxonomyRequest }) =>
      (await api.put<ExecutionTaxonomyDto>(`/execution-taxonomy/${id}`, req)).data,
    onSuccess: () => invalidate(qc),
  });
}

export function useActivateExecutionTaxonomy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ExecutionTaxonomyDto>(`/execution-taxonomy/${id}/activate`)).data,
    onSuccess: () => invalidate(qc),
  });
}

// الحذف الناعم = تعطيل (لا حذف نهائيّ؛ القوالب/التقارير القديمة تخزّن النصّ لقطةً فلا تتأثّر).
export function useDeactivateExecutionTaxonomy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<ExecutionTaxonomyDto>(`/execution-taxonomy/${id}/deactivate`)).data,
    onSuccess: () => invalidate(qc),
  });
}

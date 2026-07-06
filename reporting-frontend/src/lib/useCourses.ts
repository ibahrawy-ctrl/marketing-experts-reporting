import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { CourseDto } from '../types/api';

// كتالوج الدورات النشطة — يغذّي منتقي «الدورة» في قالب مبيعات B2C.
// قراءة فقط، متاح لأي مستخدم مصادَق (الموظّف يحتاجه عند تعبئة تقريره).
export function useActiveCourses() {
  return useQuery({
    queryKey: ['courses', 'active'],
    queryFn: async () => (await api.get<CourseDto[]>('/courses')).data,
  });
}

// ===== هوكات إدارة الدورات (الأدمن/CEO/GM عبر سياسة TemplateGovernance) — تعيد استخدام نقاط النهاية القائمة =====

export interface CourseWriteRequest {
  nameAr: string;
  nameEn: string | null;
  sortOrder: number;
}

function invalidateCourses(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: ['courses', 'active'] });
  qc.invalidateQueries({ queryKey: ['courses', 'admin'] });
}

// قائمة الإدارة (كل الدورات بما فيها المعطّلة عند includeInactive=true).
export function useAdminCourses(includeInactive: boolean) {
  return useQuery({
    queryKey: ['courses', 'admin', includeInactive],
    queryFn: async () =>
      (
        await api.get<CourseDto[]>('/admin/courses', {
          params: includeInactive ? { includeInactive: true } : {},
        })
      ).data,
  });
}

export function useCreateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CourseWriteRequest) =>
      (await api.post<CourseDto>('/admin/courses', req)).data,
    onSuccess: () => invalidateCourses(qc),
  });
}

export function useUpdateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: CourseWriteRequest }) =>
      (await api.put<CourseDto>(`/admin/courses/${id}`, req)).data,
    onSuccess: () => invalidateCourses(qc),
  });
}

export function useActivateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<CourseDto>(`/admin/courses/${id}/activate`)).data,
    onSuccess: () => invalidateCourses(qc),
  });
}

// الحذف الناعم = تعطيل (لا يوجد عمود IsDeleted ولا هجرة؛ التاريخ محفوظ لأن التقارير القديمة تخزّن اسم الدورة نصًّا).
export function useDeactivateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.patch<CourseDto>(`/admin/courses/${id}/deactivate`)).data,
    onSuccess: () => invalidateCourses(qc),
  });
}

// نتيجة الحذف الآمن: نهائيّ إن لم تُستخدَم الدورة، وإلّا أرشفة (تعطيل) دون حذف.
export interface CourseDeleteResult {
  hardDeleted: boolean;
  course: CourseDto | null;
  message: string;
}

// حذف آمن: يحذف الدورة نهائيًّا إن لم تُستخدَم في أي تقرير، وإلّا يؤرشفها (يعطّلها) — التقارير القديمة تبقى كما هي.
export function useDeleteCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) =>
      (await api.delete<CourseDeleteResult>(`/admin/courses/${id}`)).data,
    onSuccess: () => invalidateCourses(qc),
  });
}

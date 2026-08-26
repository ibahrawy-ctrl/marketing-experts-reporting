import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  AttendanceAction,
  AttendanceIncidentDetail,
  AttendanceListFilter,
  AttendancePaged,
  AttendanceType,
} from '../types/attendance';

// P2-ATT-007 — وصول الواجهة إلى سطح وقائع الحضور.
// النطاق والصلاحيّة مفروضان خادميًّا بالكامل: خارج النطاق يعود 404، والعلم المطفأ يُخفي السطح كلّه.

const LIST_KEY = 'attendance-incidents';
const DETAIL_KEY = 'attendance-incident';

/** يُسقِط المفاتيح الفارغة كي لا تُرسَل مرشِّحات بلا معنى تُفسَّر خادميًّا. */
function toParams(filter: AttendanceListFilter): Record<string, string> {
  const params: Record<string, string> = {};
  Object.entries(filter).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    if (value === false) return;
    params[key] = String(value);
  });
  return params;
}

export function useAttendanceTypes(enabled = true) {
  return useQuery({
    queryKey: ['attendance-types'],
    enabled,
    staleTime: 5 * 60 * 1000,
    queryFn: async () => (await api.get<AttendanceType[]>('/attendance/types')).data,
  });
}

export function useAttendanceIncidents(filter: AttendanceListFilter) {
  return useQuery({
    queryKey: [LIST_KEY, filter],
    queryFn: async () =>
      (await api.get<AttendancePaged>('/attendance', { params: toParams(filter) })).data,
  });
}

export function useAttendanceIncident(id: string | null) {
  return useQuery({
    queryKey: [DETAIL_KEY, id],
    enabled: !!id,
    queryFn: async () =>
      (await api.get<AttendanceIncidentDetail>(`/attendance/${id}`)).data,
  });
}

function invalidate(qc: ReturnType<typeof useQueryClient>, id?: string) {
  qc.invalidateQueries({ queryKey: [LIST_KEY] });
  if (id) qc.invalidateQueries({ queryKey: [DETAIL_KEY, id] });
}

export interface CreateIncidentInput {
  subjectUserId: string;
  incidentTypeId: string;
  incidentDate: string;
  startTime?: string | null;
  returnTime?: string | null;
  description: string;
  policyRefId?: string | null;
  submitImmediately: boolean;
}

export function useCreateAttendanceIncident() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: CreateIncidentInput) => {
      // مفتاح تكافؤ لكلّ محاولة إرسال: إعادة الإرسال الشبكيّة لا تُنشئ بلاغًا ثانيًا.
      const idempotencyKey = crypto.randomUUID();
      const { data } = await api.post<AttendanceIncidentDetail>('/attendance', input, {
        headers: { 'Idempotency-Key': idempotencyKey },
      });
      return data;
    },
    onSuccess: (d) => invalidate(qc, d.id),
  });
}

/** خريطة الفعل إلى مساره — الفعل يصل من `allowedActions` فلا يُخترَع مسار لفعل لم يسمح به الخادم. */
const ACTION_PATH: Partial<Record<AttendanceAction, string>> = {
  Submit: 'submit',
  Withdraw: 'withdraw',
  Acknowledge: 'acknowledge',
  Dispute: 'dispute',
  Escalate: 'escalate',
  Close: 'close',
};

export interface RunActionInput {
  id: string;
  action: AttendanceAction;
  concurrencyStamp: number;
  /** سبب/ردّ مكتوب — إلزاميّ للأفعال التي تستلزم رواية. */
  text?: string;
}

export function useRunAttendanceAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action, concurrencyStamp, text }: RunActionInput) => {
      const path = ACTION_PATH[action];
      if (!path) throw new Error(`فعل غير مدعوم في هذا السطح: ${action}`);

      // الأفعال تختلف في اسم حقل النصّ: الردّ للموظّف، والسبب للسحب/التصعيد/الإغلاق.
      const body: Record<string, unknown> = { concurrencyStamp };
      if (action === 'Acknowledge' || action === 'Dispute') body.response = text ?? null;
      else if (action !== 'Submit') body.reason = text ?? '';

      const { data } = await api.post<AttendanceIncidentDetail>(`/attendance/${id}/${path}`, body);
      return data;
    },
    onSuccess: (d) => invalidate(qc, d.id),
  });
}

export type HrDecision = 'Confirm' | 'Reject' | 'Correct' | 'Reconcile' | 'Void';

export interface HrReviewInput {
  id: string;
  decision: HrDecision;
  note?: string;
  concurrencyStamp: number;
  reconcileWithLeaveRequestId?: string;
  correctedIncidentDate?: string;
  correctedStartTime?: string;
  correctedReturnTime?: string;
  correctedIncidentTypeId?: string;
  correctedDescription?: string;
}

export function useHrReviewAttendance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...body }: HrReviewInput) =>
      (await api.post<AttendanceIncidentDetail>(`/attendance/${id}/hr-review`, body)).data,
    onSuccess: (d) => invalidate(qc, d.id),
  });
}

export function useUploadAttendanceAttachment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, file }: { id: string; file: File }) => {
      const form = new FormData();
      form.append('file', file);
      const { data } = await api.post<AttendanceIncidentDetail>(
        `/attendance/${id}/attachments`,
        form,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      );
      return data;
    },
    onSuccess: (d) => invalidate(qc, d.id),
  });
}

/** اقتراحات المصالحة — تصل فقط لمن يملك مراجعة، وتغيب من الـJSON لغيره. */
export function useReconciliationSuggestions(id: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ['attendance-reconciliation', id],
    enabled: !!id && enabled,
    queryFn: async () =>
      (await api.get(`/attendance/${id}/reconciliation-suggestions`)).data,
  });
}

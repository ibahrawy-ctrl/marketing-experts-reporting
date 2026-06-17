import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import {
  formatDateTime,
  managementNoteEntityLabel,
  managementNoteStatusLabel,
  managementNoteTypeLabel,
} from '../lib/format';
import type {
  CreateManagementNoteRequest,
  ManagementNoteDto,
  ManagementNoteEntityType,
  ManagementNoteType,
} from '../types/api';
import { Alert, Badge, Button, Card, Field, Select } from './ui';

// لون شارة نوع الملاحظة — التنبيه أحمر، التوجيه برتقالي، المتابعة ذهبي، التوثيق رمادي.
const noteTypeTone: Record<ManagementNoteType, 'navy' | 'orange' | 'success' | 'alert' | 'gold' | 'muted'> = {
  Documentation: 'muted',
  Guidance: 'orange',
  Warning: 'alert',
  FollowUp: 'gold',
};

const NOTE_TYPES: ManagementNoteType[] = ['Documentation', 'Guidance', 'Warning', 'FollowUp'];

/**
 * لوحة الملاحظات الإدارية المرتبطة بالسياق.
 * تُعرض داخل تفاصيل التقرير/الموظف/تقييم KPI/الفريق… وتجلب ملاحظات الكيان المرتبط فقط.
 * من يملك صلاحية إدارية (canApprove أو canViewGovernance) يستطيع الإضافة والمعالجة.
 */
export function ManagementNotesPanel({
  entityType,
  entityId,
  title = 'الملاحظات الإدارية',
}: {
  entityType: ManagementNoteEntityType;
  entityId: string;
  title?: string;
}) {
  const qc = useQueryClient();
  const { canApprove, canViewGovernance } = useAuth();
  const canManage = canApprove || canViewGovernance;

  const [noteType, setNoteType] = useState<ManagementNoteType>('Documentation');
  const [body, setBody] = useState('');
  const [requiresAction, setRequiresAction] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const queryKey = ['management-notes', entityType, entityId];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: async () =>
      (
        await api.get<ManagementNoteDto[]>('/management-notes', {
          params: { entityType, entityId },
        })
      ).data,
  });

  const create = useMutation({
    mutationFn: () =>
      api.post<ManagementNoteDto>('/management-notes', {
        entityType,
        entityId,
        noteType,
        body: body.trim(),
        requiresAction,
      } satisfies CreateManagementNoteRequest),
    onSuccess: () => {
      setBody('');
      setRequiresAction(false);
      setNoteType('Documentation');
      void qc.invalidateQueries({ queryKey });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const resolve = useMutation({
    mutationFn: (id: string) => api.post<ManagementNoteDto>(`/management-notes/${id}/resolve`),
    onSuccess: () => void qc.invalidateQueries({ queryKey }),
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // الوصول لهذه اللوحة محصور أصلًا بمن يرى الكيان؛ لكن غير الإداريين قد يحصلون 403 من الخادم.
  // لا نُظهر اللوحة لغير الإداريين لتجنّب أخطاء صلاحية مزعجة في سياقات لا تخصّهم.
  if (!canManage) return null;

  const notes = data ?? [];

  return (
    <Card>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-semibold text-navy">{title}</h2>
        <span className="text-xs text-ink-2">المرجع: {managementNoteEntityLabel[entityType]}</span>
      </div>

      {err && (
        <div className="mb-3">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}

      {/* نموذج إضافة ملاحظة — للإداريين فقط. */}
      <div className="mb-4 space-y-3 rounded-lg border border-line bg-offwhite p-3">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="نوع الملاحظة">
            <Select value={noteType} onChange={(e) => setNoteType(e.target.value as ManagementNoteType)}>
              {NOTE_TYPES.map((t) => (
                <option key={t} value={t}>
                  {managementNoteTypeLabel[t]}
                </option>
              ))}
            </Select>
          </Field>
          <label className="flex items-end gap-2 pb-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={requiresAction}
              onChange={(e) => setRequiresAction(e.target.checked)}
              className="h-4 w-4 rounded border-line"
            />
            تتطلّب إجراءً (وليست مجرد توثيق)
          </label>
        </div>
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder="اكتب ملاحظتك الإدارية على هذا السياق…"
          rows={3}
          className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
        />
        <Button
          disabled={create.isPending || !body.trim()}
          onClick={() => {
            setErr(null);
            create.mutate();
          }}
        >
          إضافة ملاحظة
        </Button>
      </div>

      {/* قائمة الملاحظات. */}
      {isLoading ? (
        <p className="text-sm text-ink-2">جارٍ تحميل الملاحظات…</p>
      ) : isError ? (
        <p className="text-sm text-alert">تعذّر تحميل الملاحظات.</p>
      ) : notes.length === 0 ? (
        <p className="text-sm text-ink-2">لا توجد ملاحظات إدارية على هذا السياق بعد.</p>
      ) : (
        <ul className="space-y-3">
          {notes.map((n) => (
            <li key={n.id} className="rounded-lg border border-line p-3">
              <div className="mb-1 flex flex-wrap items-center gap-2">
                <Badge tone={noteTypeTone[n.noteType]}>{managementNoteTypeLabel[n.noteType]}</Badge>
                {n.requiresAction && <Badge tone="orange">يتطلّب إجراءً</Badge>}
                <Badge tone={n.status === 'Resolved' ? 'success' : 'navy'}>
                  {managementNoteStatusLabel[n.status]}
                </Badge>
              </div>
              <p className="whitespace-pre-wrap text-sm text-ink">{n.body}</p>
              <p className="mt-2 text-xs text-ink-2">
                {n.authorName ?? '—'} · {formatDateTime(n.createdAtUtc)}
                {n.status === 'Resolved' && n.resolvedByName
                  ? ` · عُولجت بواسطة ${n.resolvedByName}${n.resolvedAtUtc ? ` (${formatDateTime(n.resolvedAtUtc)})` : ''}`
                  : ''}
              </p>
              {n.status === 'Open' && (
                <div className="mt-2">
                  <Button
                    variant="ghost"
                    disabled={resolve.isPending}
                    onClick={() => {
                      setErr(null);
                      resolve.mutate(n.id);
                    }}
                  >
                    تمّت المعالجة
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

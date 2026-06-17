import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { Card, Input, Field, Select, Badge, EmptyState } from '../components/ui';
import { TableSkeleton, QueryError } from '../components/states';
import {
  formatDateTime,
  auditSentence,
  auditActionMeta,
  auditEntityName,
} from '../lib/format';
import type { AuditLogDto } from '../types/api';

// خيارات الأحداث المعروفة (verb) لعرضها كفلتر مفهوم بدل كتابة نص تقني.
const EVENT_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'كل الأحداث' },
  { value: 'submitted', label: 'إرسال' },
  { value: 'approved', label: 'اعتماد' },
  { value: 'returned', label: 'إرجاع للتعديل' },
  { value: 'escalated', label: 'تصعيد' },
  { value: 'created', label: 'إنشاء' },
  { value: 'updated', label: 'تعديل' },
  { value: 'deleted', label: 'حذف' },
];

const ENTITY_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'كل الكيانات' },
  { value: 'ReportSubmission', label: 'تقرير' },
  { value: 'KpiEvaluation', label: 'تقييم KPI' },
  { value: 'ReportTemplate', label: 'قالب تقرير' },
  { value: 'KpiTemplate', label: 'قالب KPI' },
  { value: 'Risk', label: 'مخاطرة' },
  { value: 'Escalation', label: 'تصعيد' },
  { value: 'Decision', label: 'قرار' },
];

export default function AuditPage() {
  const [entityType, setEntityType] = useState('');
  const [event, setEvent] = useState('');
  const [actor, setActor] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [expanded, setExpanded] = useState<string | null>(null);

  // الفلترة على مستوى الخادم تبقى بنوع الكيان فقط (كما هي الـ API)؛
  // باقي الفلاتر (الحدث/المنفّذ/الفترة) تُطبَّق على العميل دون تغيير الصلاحيات.
  const { data: logs, isLoading, isError, refetch } = useQuery({
    queryKey: ['audit-logs', entityType],
    queryFn: async () =>
      (
        await api.get<AuditLogDto[]>('/audit-logs', {
          params: { entityType: entityType || undefined },
        })
      ).data,
  });

  const filtered = useMemo(() => {
    let rows = logs ?? [];
    if (event) rows = rows.filter((l) => l.action.toLowerCase().includes(event));
    if (actor.trim()) rows = rows.filter((l) => (l.actorName ?? '').includes(actor.trim()));
    if (from) rows = rows.filter((l) => new Date(l.createdAtUtc) >= new Date(from));
    if (to) rows = rows.filter((l) => new Date(l.createdAtUtc) <= new Date(`${to}T23:59:59`));
    return rows;
  }, [logs, event, actor, from, to]);

  const copy = (text: string) => {
    if (navigator.clipboard) navigator.clipboard.writeText(text).catch(() => {});
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">سجل التدقيق</h1>
        <p className="mt-1 text-sm text-ink-2">كل حركة في النظام موثّقة: من فعل ماذا ومتى. التفاصيل التقنية متاحة عند الحاجة.</p>
      </div>

      <Card>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Field label="نوع الحدث">
            <Select value={event} onChange={(e) => setEvent(e.target.value)}>
              {EVENT_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </Select>
          </Field>
          <Field label="الكيان">
            <Select value={entityType} onChange={(e) => setEntityType(e.target.value)}>
              {ENTITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </Select>
          </Field>
          <Field label="المنفّذ">
            <Input value={actor} onChange={(e) => setActor(e.target.value)} placeholder="اسم الموظف" />
          </Field>
          <Field label="من تاريخ">
            <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field label="إلى تاريخ">
            <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </Field>
        </div>
      </Card>

      <Card>
        {isLoading ? (
          <TableSkeleton rows={8} cols={4} />
        ) : isError ? (
          <QueryError onRetry={() => refetch()} />
        ) : !filtered.length ? (
          <EmptyState
            title="لا توجد أحداث مطابقة"
            description="لم نعثر على حركات ضمن الفلاتر المحددة. جرّب توسيع الفترة أو إزالة فلتر الحدث/المنفّذ. تظهر هنا كل عمليات الإرسال والاعتماد والإرجاع والتعديل في النظام."
          />
        ) : (
          <ul className="divide-y divide-line">
            {filtered.map((l) => {
              const meta = auditActionMeta(l.action);
              const open = expanded === l.id;
              return (
                <li key={l.id} className="py-3">
                  <div className="flex items-start gap-3">
                    <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-navy-50 text-base" aria-hidden>
                      {meta.icon}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="font-medium text-navy">{auditSentence(l)}</p>
                      <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-ink-2">
                        <span>{formatDateTime(l.createdAtUtc)}</span>
                        <span aria-hidden>·</span>
                        <Badge tone={meta.tone === 'orange' ? 'orange' : meta.tone}>{meta.label}</Badge>
                        <span className="rounded bg-line px-1.5 py-0.5">{auditEntityName(l.entityType)}</span>
                        <button
                          type="button"
                          className="text-navy underline-offset-2 hover:underline"
                          onClick={() => setExpanded(open ? null : l.id)}
                        >
                          {open ? 'إخفاء التفاصيل التقنية' : 'تفاصيل تقنية'}
                        </button>
                      </div>
                      {open && (
                        <div className="mt-2 space-y-1 rounded-lg border border-line bg-offwhite p-3 text-xs text-ink-2">
                          <DetailRow label="الكود التقني" value={l.action} />
                          <DetailRow label="نوع الكيان (تقني)" value={l.entityType} />
                          {l.entityId && (
                            <div className="flex items-center gap-2">
                              <span className="font-medium text-ink">المعرّف:</span>
                              <code className="rounded bg-white px-1.5 py-0.5 font-mono text-[11px]">{l.entityId}</code>
                              <button type="button" className="text-navy hover:underline" onClick={() => copy(l.entityId!)}>نسخ</button>
                            </div>
                          )}
                          <DetailRow label="عنوان IP" value={l.ipAddress ?? '—'} />
                          {l.dataJson && (
                            <div>
                              <span className="font-medium text-ink">بيانات إضافية:</span>
                              <pre className="mt-1 overflow-x-auto rounded bg-white p-2 font-mono text-[11px] text-ink">{l.dataJson}</pre>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center gap-2">
      <span className="font-medium text-ink">{label}:</span>
      <span className="font-mono text-[11px]">{value}</span>
    </div>
  );
}

// سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-UI-R1) — عرض إداريّ قراءة-فقط.
// النظام يعمل في وضع DryRun (لا إرسال فعليّ). هذه الشاشة مراجعة/رقابة فقط:
// لا إرسال، لا إعادة إرسال، لا تعديل حالة، لا حذف. للأدوار Admin/CEO/GM/CeoSupport فقط.
import { useMemo, useState } from 'react';
import {
  useEmailNotificationLog,
  useEmailNotificationDetail,
} from '../lib/useEmailNotifications';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { formatDateTime } from '../lib/format';
import type { EmailNotificationLogFilter } from '../types/api';

// تسميات عربية لأنواع أحداث البريد (14 حوكمة/HR + 9 تذكيرات تقارير).
const eventTypeLabel: Record<string, string> = {
  'governance-item-created': 'إسناد بند حوكمة',
  'governance-item-updated': 'تحديث بند حوكمة',
  'governance-action-item-assigned': 'إسناد إجراء حوكمة',
  'governance-action-item-reassigned': 'إعادة إسناد إجراء حوكمة',
  'governance-action-item-completed': 'إغلاق إجراء حوكمة',
  'governance-escalation-created': 'إنشاء تصعيد',
  'governance-escalation-assigned': 'إسناد تصعيد',
  'governance-escalation-closed': 'إغلاق تصعيد',
  'leave-request-created': 'إنشاء طلب إجازة',
  'leave-request-needs-hr-action': 'طلب إجازة يحتاج HR',
  'leave-request-approved': 'الموافقة على إجازة',
  'leave-request-rejected': 'رفض إجازة',
  'hr-request-created': 'إنشاء طلب موارد بشرية',
  'hr-request-completed': 'إغلاق طلب موارد بشرية',
  'report-weekly-due': 'تذكير التقرير الأسبوعي',
  'report-daily-due': 'تذكير التقرير اليومي',
  'report-overdue': 'تأخر تقرير',
  'report-team-overdue-summary': 'ملخص تأخر الفريق',
  'report-department-overdue-summary': 'ملخص تأخر الإدارة',
  'report-executive-overdue-summary': 'ملخص تنفيذي للتقارير',
  'report-review-overdue-teamleader': 'تأخر مراجعة قائد الفريق',
  'report-review-overdue-manager': 'تأخر مراجعة المدير',
  'report-review-pending-executive': 'مراجعات تنفيذية معلقة',
};

const EVENT_TYPES = Object.keys(eventTypeLabel);

function eventLabel(evt: string): string {
  return eventTypeLabel[evt] ?? evt;
}

// حالات الإشعار مع أسلوب الشارة (بلا ألوان قاسية).
const statusLabel: Record<string, string> = {
  Pending: 'قيد الانتظار',
  DryRun: 'تجريبي (DryRun)',
  Sent: 'أُرسِل',
  Failed: 'فشل',
  Skipped: 'متخطّى',
  Cancelled: 'مُلغًى',
};

type StatusTone = 'navy' | 'orange' | 'success' | 'alert' | 'gold' | 'muted';
const statusTone: Record<string, StatusTone> = {
  Pending: 'muted',
  DryRun: 'navy',
  Sent: 'success',
  Failed: 'alert',
  Skipped: 'gold',
  Cancelled: 'muted',
};

const modeLabel: Record<string, string> = {
  Disabled: 'معطّل',
  DryRun: 'تجريبي',
  Enabled: 'مُفعّل',
};

const STATUS_FILTERS = ['DryRun', 'Skipped', 'Failed', 'Sent', 'Pending', 'Cancelled'];

function shortKey(key: string): string {
  return key.length > 12 ? `${key.slice(0, 12)}…` : key;
}

export default function EmailNotificationsPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [eventType, setEventType] = useState('');
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);

  const pageSize = 25;
  const filter: EmailNotificationLogFilter = useMemo(
    () => ({
      page,
      pageSize,
      status: status || undefined,
      eventType: eventType || undefined,
      search: search || undefined,
      dateFrom: dateFrom || undefined,
      dateTo: dateTo || undefined,
    }),
    [page, status, eventType, search, dateFrom, dateTo],
  );

  const log = useEmailNotificationLog(filter);
  const detail = useEmailNotificationDetail(detailId);

  function applyFilters() {
    setPage(1);
    setSearch(searchInput.trim());
  }

  function resetFilters() {
    setStatus('');
    setEventType('');
    setSearchInput('');
    setSearch('');
    setDateFrom('');
    setDateTo('');
    setPage(1);
  }

  const data = log.data;
  const summary = data?.summary;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div className="space-y-6">
      <SectionTitle
        title="سجلّ إشعارات البريد"
        hint="عرض رقابيّ لإشعارات البريد المسجّلة في النظام — قراءة فقط، دون إرسال أو تعديل أو حذف."
      />

      <Alert tone="navy">
        هذه الشاشة تعرض إشعارات البريد المسجّلة في وضع DryRun. لا يتم إرسال أي بريد فعلي حاليًا.
      </Alert>

      {/* بطاقات الملخّص */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
        <SummaryCard label="الإجمالي" value={summary?.total} />
        <SummaryCard label="تجريبي (DryRun)" value={summary?.dryRun} />
        <SummaryCard label="متخطّى" value={summary?.skipped} />
        <SummaryCard label="فشل" value={summary?.failed} />
        <SummaryCard label="أُرسِل" value={summary?.sent} />
        <SummaryCard
          label="آخر إشعار"
          text={summary?.lastCreatedAtUtc ? formatDateTime(summary.lastCreatedAtUtc) : '—'}
        />
      </div>

      {/* الفلاتر */}
      <Card>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <Field label="الحالة">
            <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
              <option value="">— كل الحالات —</option>
              {STATUS_FILTERS.map((s) => (
                <option key={s} value={s}>{statusLabel[s]}</option>
              ))}
            </Select>
          </Field>
          <Field label="نوع الحدث">
            <Select value={eventType} onChange={(e) => { setEventType(e.target.value); setPage(1); }}>
              <option value="">— كل الأحداث —</option>
              {EVENT_TYPES.map((t) => (
                <option key={t} value={t}>{eventTypeLabel[t]}</option>
              ))}
            </Select>
          </Field>
          <Field label="بحث نصّي (الموضوع/المستلم/مفتاح الترابط)">
            <Input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') applyFilters(); }}
              placeholder="ابحث…"
            />
          </Field>
          <Field label="من تاريخ">
            <Input type="date" value={dateFrom} onChange={(e) => { setDateFrom(e.target.value); setPage(1); }} />
          </Field>
          <Field label="إلى تاريخ">
            <Input type="date" value={dateTo} onChange={(e) => { setDateTo(e.target.value); setPage(1); }} />
          </Field>
          <div className="flex items-end gap-2">
            <Button onClick={applyFilters}>تطبيق البحث</Button>
            <Button variant="ghost" onClick={resetFilters}>مسح</Button>
            <Button variant="ghost" onClick={() => log.refetch()}>تحديث</Button>
          </div>
        </div>
      </Card>

      {/* الجدول */}
      {log.isLoading ? (
        <LoadingState label="يتم تحميل سجلّ الإشعارات…" />
      ) : log.isError ? (
        <QueryError onRetry={() => log.refetch()} description="حدث خطأ أثناء جلب سجلّ الإشعارات." />
      ) : !data || data.items.length === 0 ? (
        <Card>
          <p className="py-8 text-center text-sm text-slate-500">لا توجد إشعارات بريد مسجلة حتى الآن.</p>
        </Card>
      ) : (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead>
                <tr className="border-b text-slate-500">
                  <th className="p-2">التاريخ</th>
                  <th className="p-2">نوع الحدث</th>
                  <th className="p-2">المستلم</th>
                  <th className="p-2">البريد</th>
                  <th className="p-2">الموضوع</th>
                  <th className="p-2">الحالة</th>
                  <th className="p-2">مفتاح الترابط</th>
                  <th className="p-2"></th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((n) => (
                  <tr key={n.id} className="border-b align-top">
                    <td className="whitespace-nowrap p-2 text-slate-500">{formatDateTime(n.createdAtUtc)}</td>
                    <td className="p-2">{eventLabel(n.eventType)}</td>
                    <td className="p-2">{n.recipientName ?? '—'}</td>
                    <td className="p-2 text-slate-500">{n.recipientEmail ?? '—'}</td>
                    <td className="max-w-xs truncate p-2">{n.subject}</td>
                    <td className="p-2">
                      <Badge tone={statusTone[n.status] ?? 'muted'}>{statusLabel[n.status] ?? n.status}</Badge>
                    </td>
                    <td className="p-2 font-mono text-xs text-slate-400" title={n.correlationKey}>{shortKey(n.correlationKey)}</td>
                    <td className="p-2">
                      <Button variant="ghost" onClick={() => setDetailId(n.id)}>عرض التفاصيل</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* ترقيم الصفحات */}
          <div className="mt-4 flex items-center justify-between text-sm text-slate-500">
            <span>إجمالي {data.totalCount} إشعار</span>
            <div className="flex items-center gap-2">
              <Button variant="ghost" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>
                السابق
              </Button>
              <span>صفحة {page} من {totalPages}</span>
              <Button variant="ghost" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}>
                التالي
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* تفاصيل الإشعار (Drawer) */}
      {detailId && (
        <div className="fixed inset-0 z-50 flex justify-start bg-black/40" onClick={() => setDetailId(null)}>
          <div
            className="h-full w-full max-w-lg overflow-y-auto bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-lg font-semibold text-navy">تفاصيل الإشعار</h3>
              <Button variant="ghost" onClick={() => setDetailId(null)}>إغلاق</Button>
            </div>
            {detail.isLoading ? (
              <LoadingState label="يتم تحميل التفاصيل…" />
            ) : detail.isError || !detail.data ? (
              <QueryError onRetry={() => detail.refetch()} description="تعذّر جلب تفاصيل الإشعار." />
            ) : (
              <div className="space-y-4 text-sm">
                <DetailRow label="الموضوع" value={detail.data.subject} />
                <DetailRow label="نوع الحدث" value={eventLabel(detail.data.eventType)} />
                <div className="flex items-center gap-3">
                  <Badge tone={statusTone[detail.data.status] ?? 'muted'}>
                    {statusLabel[detail.data.status] ?? detail.data.status}
                  </Badge>
                  <span className="text-slate-500">الوضع: {modeLabel[detail.data.mode] ?? detail.data.mode}</span>
                </div>
                <DetailRow label="المستلم" value={detail.data.recipientName ?? '—'} />
                <DetailRow label="بريد المستلم" value={detail.data.recipientEmail ?? '—'} />
                <DetailRow label="تاريخ الإنشاء" value={formatDateTime(detail.data.createdAtUtc)} />
                <DetailRow label="مفتاح الترابط" value={detail.data.correlationKey} mono />
                <DetailRow
                  label="الكيان المصدر"
                  value={`${detail.data.sourceEntityType} — ${detail.data.sourceEntityId}`}
                  mono
                />
                {detail.data.errorMessage && (
                  <div>
                    <div className="mb-1 font-semibold text-alert">سبب الفشل</div>
                    <div className="rounded-md bg-red-50 p-2 text-alert">{detail.data.errorMessage}</div>
                  </div>
                )}
                <div>
                  <div className="mb-1 font-semibold text-navy">نصّ الرسالة</div>
                  <div
                    className="rounded-md border border-line bg-slate-50 p-3 text-sm leading-relaxed"
                    dangerouslySetInnerHTML={{ __html: detail.data.bodyHtml }}
                  />
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, text }: { label: string; value?: number; text?: string }) {
  return (
    <Card className="text-center">
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-1 text-lg font-semibold text-navy">
        {text ?? (value ?? '—')}
      </div>
    </Card>
  );
}

function DetailRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <div className="mb-0.5 text-xs text-slate-500">{label}</div>
      <div className={mono ? 'break-all font-mono text-xs' : 'text-ink'}>{value}</div>
    </div>
  );
}

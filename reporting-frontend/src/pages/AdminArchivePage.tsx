// الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 13) — شاشة قراءة العناصر المحذوفة إداريًّا
// ناعمًا (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid. Admin/CEO/GM فقط (تطابق سياسة الخادم).
// لا حذف نهائيّ ولا جدولة ولا إشعارات؛ الاسترجاع يعكس الحذف الإداريّ فقط بسبب إلزاميّ (10–500 محرفًا).
import { useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Select,
  Spinner,
} from '../components/ui';
import { apiErrorMessage } from '../lib/api';
import { formatDateTime } from '../lib/format';
import { useArchiveDetails, useArchiveList, useRestoreArchiveItem } from '../lib/useArchive';
import type { ArchiveItemDto, ArchiveItemType, ArchiveRetentionStatus } from '../types/api';

const itemTypeLabel: Record<ArchiveItemType, string> = {
  Report: 'تقرير',
  KpiEvaluation: 'تقييم KPI',
};

const retentionLabel: Record<ArchiveRetentionStatus, { text: string; tone: 'success' | 'gold' | 'alert' }> = {
  Fresh: { text: 'حديث', tone: 'success' },
  ReviewDue: { text: 'يستحقّ المراجعة', tone: 'gold' },
  LongTerm: { text: 'محفوظ طويل الأمد', tone: 'alert' },
};

export default function AdminArchivePage() {
  const [itemType, setItemType] = useState<'' | ArchiveItemType>('');
  const [periodKey, setPeriodKey] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<{ type: ArchiveItemType; id: string } | null>(null);

  const filter = {
    itemType: itemType || undefined,
    periodKey: periodKey.trim() || undefined,
    employeeId: employeeId.trim() || undefined,
    page,
    pageSize: 20,
  };
  const list = useArchiveList(filter);
  const totalPages = list.data ? Math.max(1, Math.ceil(list.data.totalCount / list.data.pageSize)) : 1;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold text-navy">الأرشيف الإداريّ</h1>
        <p className="mt-1 text-sm text-ink-2">
          العناصر المحذوفة إداريًّا (تقارير وتقييمات KPI). يمكن استرجاعها وفق دلالات الاسترجاع المعتمَدة —
          دون حذف نهائيّ ولا إشعارات.
        </p>
      </div>

      <Card>
        <div className="grid gap-4 md:grid-cols-4">
          <Field label="النوع">
            <Select
              value={itemType}
              onChange={(e) => {
                setItemType(e.target.value as '' | ArchiveItemType);
                setPage(1);
              }}
            >
              <option value="">الكل</option>
              <option value="Report">تقارير</option>
              <option value="KpiEvaluation">تقييمات KPI</option>
            </Select>
          </Field>
          <Field label="مفتاح الفترة" help="مثل 2026-W27 أو 2026-Q2">
            <Input
              value={periodKey}
              onChange={(e) => {
                setPeriodKey(e.target.value);
                setPage(1);
              }}
              placeholder="اختياريّ"
            />
          </Field>
          <Field label="مُعرّف الموظف">
            <Input
              value={employeeId}
              onChange={(e) => {
                setEmployeeId(e.target.value);
                setPage(1);
              }}
              placeholder="اختياريّ (GUID)"
            />
          </Field>
        </div>
      </Card>

      {list.isLoading ? (
        <Spinner />
      ) : list.isError ? (
        <Alert tone="alert">{apiErrorMessage(list.error, 'تعذّر تحميل الأرشيف.')}</Alert>
      ) : !list.data || list.data.items.length === 0 ? (
        <EmptyState title="لا عناصر مؤرشفة" description="لا توجد عناصر محذوفة إداريًّا مطابقة للمرشّحات." />
      ) : (
        <>
          <Card className="overflow-x-auto p-0">
            <table className="w-full text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-4 py-3 font-medium">النوع</th>
                  <th className="px-4 py-3 font-medium">الموظف</th>
                  <th className="px-4 py-3 font-medium">القالب</th>
                  <th className="px-4 py-3 font-medium">الفترة</th>
                  <th className="px-4 py-3 font-medium">تاريخ الحذف</th>
                  <th className="px-4 py-3 font-medium">حُذف بواسطة</th>
                  <th className="px-4 py-3 font-medium">الاحتفاظ</th>
                  <th className="px-4 py-3 font-medium">قابلية الاسترجاع</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {list.data.items.map((item) => (
                  <ArchiveRow
                    key={item.archiveItemId}
                    item={item}
                    onOpen={() => setSelected({ type: item.itemType, id: item.archiveItemId })}
                  />
                ))}
              </tbody>
            </table>
          </Card>

          <div className="flex items-center justify-between text-sm text-ink-2">
            <span>
              إجمالي {list.data.totalCount} عنصر · صفحة {list.data.page} من {totalPages}
            </span>
            <div className="flex gap-2">
              <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                السابق
              </Button>
              <Button variant="ghost" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                التالي
              </Button>
            </div>
          </div>
        </>
      )}

      {selected && (
        <ArchiveDetailsModal
          itemType={selected.type}
          id={selected.id}
          onClose={() => setSelected(null)}
        />
      )}
    </div>
  );
}

function ArchiveRow({ item, onOpen }: { item: ArchiveItemDto; onOpen: () => void }) {
  const ret = retentionLabel[item.retentionStatus];
  return (
    <tr className="border-b border-line last:border-0 hover:bg-navy-50/40">
      <td className="px-4 py-3">
        <Badge tone={item.itemType === 'Report' ? 'navy' : 'orange'}>{itemTypeLabel[item.itemType]}</Badge>
      </td>
      <td className="px-4 py-3 font-medium text-ink">{item.employeeName}</td>
      <td className="px-4 py-3 text-ink-2">{item.templateName}</td>
      <td className="px-4 py-3">{item.periodKey}</td>
      <td className="px-4 py-3 text-ink-2">{formatDateTime(item.deletedAtUtc)}</td>
      <td className="px-4 py-3 text-ink-2">{item.deletedByName ?? '—'}</td>
      <td className="px-4 py-3">
        <Badge tone={ret.tone}>
          {ret.text} · {item.daysSinceDeletion} يومًا
        </Badge>
      </td>
      <td className="px-4 py-3">
        {item.canRestore ? (
          <Badge tone="success">قابل للاسترجاع</Badge>
        ) : (
          <Badge tone="muted">محجوب</Badge>
        )}
      </td>
      <td className="px-4 py-3">
        <Button variant="ghost" onClick={onOpen}>
          التفاصيل
        </Button>
      </td>
    </tr>
  );
}

function ArchiveDetailsModal({
  itemType,
  id,
  onClose,
}: {
  itemType: ArchiveItemType;
  id: string;
  onClose: () => void;
}) {
  const details = useArchiveDetails(itemType, id);
  const restore = useRestoreArchiveItem();
  const [reason, setReason] = useState('');
  const [restoreError, setRestoreError] = useState<string | null>(null);
  const [restoreOk, setRestoreOk] = useState(false);

  const d = details.data;
  const reasonTrimmed = reason.trim();
  const reasonValid = reasonTrimmed.length >= 10 && reasonTrimmed.length <= 500;

  async function handleRestore() {
    if (!reasonValid) return;
    setRestoreError(null);
    try {
      await restore.mutateAsync({ itemType, id, request: { reason: reasonTrimmed } });
      setRestoreOk(true);
    } catch (e) {
      setRestoreError(apiErrorMessage(e, 'تعذّر الاسترجاع.'));
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-black/40 p-4">
      <div className="my-8 w-full max-w-3xl rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-5 py-4">
          <h2 className="text-lg font-bold text-navy">
            تفاصيل {itemTypeLabel[itemType]} مؤرشف
          </h2>
          <button className="rounded-lg p-1 text-ink-2 hover:bg-navy-50" onClick={onClose} aria-label="إغلاق">
            ✕
          </button>
        </div>

        <div className="max-h-[70vh] space-y-4 overflow-y-auto px-5 py-4">
          {details.isLoading ? (
            <Spinner />
          ) : details.isError || !d ? (
            <Alert tone="alert">{apiErrorMessage(details.error, 'تعذّر تحميل التفاصيل.')}</Alert>
          ) : (
            <>
              <div className="grid gap-3 text-sm md:grid-cols-2">
                <Info label="الموظف" value={d.employeeName} />
                <Info label="القالب" value={d.templateName} />
                <Info label="الفترة" value={d.periodKey} />
                <Info label="الحالة" value={d.status} />
                <Info label="تاريخ الحذف" value={formatDateTime(d.deletedAtUtc)} />
                <Info label="حُذف بواسطة" value={d.deletedByName ?? '—'} />
                <Info label="سبب الحذف" value={d.deletionReason ?? '—'} />
                <Info
                  label="الاحتفاظ"
                  value={`${retentionLabel[d.retentionStatus].text} · ${d.daysSinceDeletion} يومًا`}
                />
                {itemType === 'Report' ? (
                  <Info label="عدد الحقول" value={String(d.fieldValuesCount)} />
                ) : (
                  <>
                    <Info label="عدد المؤشرات" value={String(d.kpiResultsCount)} />
                    <Info label="أحداث المراجعة" value={String(d.reviewEventsCount)} />
                  </>
                )}
              </div>

              {d.workflowSteps.length > 0 && (
                <div>
                  <h3 className="mb-2 text-sm font-semibold text-navy">لقطة سير العمل</h3>
                  <div className="overflow-x-auto rounded-lg border border-line">
                    <table className="w-full text-right text-xs">
                      <thead className="border-b border-line text-ink-2">
                        <tr>
                          <th className="px-3 py-2 font-medium">المستوى</th>
                          <th className="px-3 py-2 font-medium">المعتمِد</th>
                          <th className="px-3 py-2 font-medium">الحالة</th>
                          <th className="px-3 py-2 font-medium">التعليق</th>
                          <th className="px-3 py-2 font-medium">تاريخ القرار</th>
                        </tr>
                      </thead>
                      <tbody>
                        {d.workflowSteps.map((s, i) => (
                          <tr key={i} className="border-b border-line last:border-0">
                            <td className="px-3 py-2">{s.level}</td>
                            <td className="px-3 py-2">{s.approverName ?? s.approverId}</td>
                            <td className="px-3 py-2">{s.status}</td>
                            <td className="px-3 py-2 text-ink-2">{s.comment ?? '—'}</td>
                            <td className="px-3 py-2 text-ink-2">{formatDateTime(s.decidedAtUtc)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              <div className="rounded-lg border border-line bg-navy-50/40 p-3 text-sm">
                <h3 className="mb-1 font-semibold text-navy">استراتيجية الاسترجاع</h3>
                <p className="text-ink-2">
                  {d.restoreStrategy === 'HistoricalApproverRestored'
                    ? `استرجاع المعتمِد التاريخيّ (${d.historicalApproverName ?? d.historicalApproverId})`
                    : d.restoreStrategy === 'NoActiveApprover'
                      ? 'استرجاع دون معتمِد نشط — يحتاج قرارًا إداريًّا لاحقًا لإعادة التوجيه.'
                      : d.itemType === 'KpiEvaluation'
                        ? 'استرجاع مباشر للتقييم.'
                        : 'غير محدّد.'}
                </p>
                {d.restoreWarning && (
                  <p className="mt-2 rounded-md bg-amber-50 p-2 text-gold">{d.restoreWarning}</p>
                )}
                {!d.canRestore && d.restoreBlockedReason && (
                  <p className="mt-2 rounded-md bg-red-50 p-2 text-alert">{d.restoreBlockedReason}</p>
                )}
              </div>

              {restoreOk ? (
                <Alert tone="success">تمّ استرجاع العنصر بنجاح.</Alert>
              ) : d.canRestore ? (
                <div className="space-y-2 rounded-lg border border-line p-3">
                  <Field label="سبب الاسترجاع (إلزاميّ)" help="بين 10 و500 محرفًا">
                    <textarea
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                      rows={3}
                      className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                      placeholder="اذكر سبب الاسترجاع للأثر التدقيقيّ…"
                    />
                  </Field>
                  {restoreError && <Alert tone="alert">{restoreError}</Alert>}
                  <div className="flex justify-end">
                    <Button onClick={handleRestore} loading={restore.isPending} disabled={!reasonValid}>
                      استرجاع العنصر
                    </Button>
                  </div>
                </div>
              ) : (
                <Alert tone="gold">هذا العنصر غير قابل للاسترجاع حاليًّا (انظر السبب أعلاه).</Alert>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="block text-xs text-ink-2">{label}</span>
      <span className="block font-medium text-ink">{value}</span>
    </div>
  );
}

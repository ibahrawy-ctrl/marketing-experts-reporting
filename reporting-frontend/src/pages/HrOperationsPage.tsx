// P2-HR-009 — لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.
//
// مبدأ حاكم: **لا رقم يُحسَب هنا**. كلّ عدّ وكلّ صفّ يأتيان من الخادم داخل نطاق المُشاهِد،
// وعدد البطاقة هو عين عدد تفصيلها تحت المرشِّح نفسه ⇒ لا يمكن للبطاقة أن تخالف ما يُفتَح منها.
// الصلاحيّة خادميّة بالكامل: 403 عند غياب المفتاح، و404 عند مغادرة النطاق. إخفاء زرّ ليس تخويلًا.
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { formatDate, formatDateTime } from '../lib/format';
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { CardsSkeleton, QueryError, TableSkeleton } from '../components/states';
import {
  useExportHrOperationsQueue,
  useHrOperationsDashboard,
  useHrOperationsQueue,
} from '../lib/useHrOperations';
import type {
  HrOperationsCard,
  HrOperationsFilter,
  HrOperationsQueueKey,
  HrOperationsRow,
} from '../types/hrOperations';

const PAGE_SIZE = 25;

function errorMessage(err: unknown): string {
  const res = (err as { response?: { status?: number; data?: { detail?: string; title?: string } } })
    ?.response;
  if (res?.status === 403) return 'لا تملك صلاحيّة تنفيذ هذا الإجراء.';
  if (res?.status === 404) return 'لا توجد بيانات مطابقة داخل نطاقك.';
  return res?.data?.detail ?? res?.data?.title ?? 'تعذّر تنفيذ الإجراء. حاول مرّة أخرى.';
}

/** الحرج لون، لا رقم إضافيّ: الشدّة تأتي محسوبةً من الخادم مع البطاقة. */
function severityTone(severityAr: string): 'alert' | 'gold' | 'success' | 'muted' {
  if (severityAr === 'حرِج') return 'alert';
  if (severityAr === 'مرتفع' || severityAr === 'متوسّط') return 'gold';
  if (severityAr === 'سليم') return 'success';
  return 'muted';
}

// ═══════════════════════════════ البطاقات ═══════════════════════════════

function QueueCard({
  card,
  active,
  onOpen,
}: {
  card: HrOperationsCard;
  active: boolean;
  onOpen: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onOpen}
      data-testid={`hr-ops-card-${card.key}`}
      aria-pressed={active}
      className={`rounded-xl border p-4 text-right transition ${
        active ? 'border-navy bg-navy-50' : 'border-line bg-white hover:border-navy-100'
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-semibold text-navy">{card.titleAr}</p>
        <Badge tone={severityTone(card.severityAr)}>{card.severityAr}</Badge>
      </div>
      <p className="mt-3 text-3xl font-bold text-navy" data-testid={`hr-ops-count-${card.key}`}>
        {card.count}
      </p>
      <p className="mt-1 text-xs text-ink-2">
        {card.groupAr}
        {card.breachedCount > 0 ? ` · ${card.breachedCount} خارج المهلة` : ''}
        {card.maxAgeingDays > 0 ? ` · أقدم بند ${card.maxAgeingDays} يومًا` : ''}
      </p>
    </button>
  );
}

// ═══════════════════════════════ جدول التفصيل ═══════════════════════════════

function QueueRows({ rows }: { rows: HrOperationsRow[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[52rem] text-right text-sm" data-testid="hr-ops-queue-table">
        <thead className="border-b border-line text-xs text-ink-2">
          <tr>
            <th className="px-3 py-2 font-medium">الموظّف</th>
            <th className="px-3 py-2 font-medium">البند</th>
            <th className="px-3 py-2 font-medium">النوع</th>
            <th className="px-3 py-2 font-medium">الحالة</th>
            <th className="px-3 py-2 font-medium">الفترة</th>
            <th className="px-3 py-2 font-medium">الاستحقاق</th>
            <th className="px-3 py-2 font-medium">المهلة</th>
            <th className="px-3 py-2 font-medium">التقادم</th>
            <th className="px-3 py-2 font-medium">المسؤول</th>
            <th className="px-3 py-2 font-medium">الإجراء التالي</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={`${row.entityType}-${row.entityId}-${row.periodKey ?? ''}`}
              className="border-b border-line-2 last:border-0"
              data-testid="hr-ops-row"
            >
              <td className="px-3 py-2">
                <a
                  className="font-medium text-navy underline-offset-2 hover:underline"
                  href={`/app/employee/${row.subjectUserId}`}
                >
                  {row.subjectFullName}
                </a>
                <p className="text-xs text-ink-2">
                  {[row.departmentName, row.teamName].filter(Boolean).join(' · ') || '—'}
                </p>
              </td>
              <td className="px-3 py-2 text-ink-2">{row.titleAr}</td>
              <td className="px-3 py-2 text-ink-2">{row.typeAr}</td>
              <td className="px-3 py-2">
                <Badge tone={row.slaBreached ? 'alert' : 'muted'}>{row.statusAr}</Badge>
              </td>
              <td className="px-3 py-2 text-ink-2">{row.periodKey ?? '—'}</td>
              <td className="px-3 py-2 text-ink-2">{row.dueAt ? formatDate(row.dueAt) : '—'}</td>
              <td className="px-3 py-2 text-ink-2">
                {/* «لا مهلة» ليست «مهلة مخروقة» — تُعرَض شرطة لا تحذيرًا. */}
                {row.slaDueAtUtc ? (
                  <span className={row.slaBreached ? 'font-semibold text-alert' : ''}>
                    {formatDateTime(row.slaDueAtUtc)}
                  </span>
                ) : (
                  '—'
                )}
              </td>
              <td className="px-3 py-2 text-ink-2">{row.ageingDays} يومًا</td>
              <td className="px-3 py-2 text-ink-2">{row.ownerFullName ?? '—'}</td>
              <td className="px-3 py-2 text-ink-2">{row.nextActionAr}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ═══════════════════════════════ الصفحة ═══════════════════════════════

export default function HrOperationsPage() {
  const [params, setParams] = useSearchParams();
  const selected = (params.get('queue') as HrOperationsQueueKey | null) ?? null;

  const [filter, setFilter] = useState<HrOperationsFilter>({ recentCycles: 8 });
  const [draft, setDraft] = useState({ userId: '', type: '', status: '', overdueOnly: false });
  const [page, setPage] = useState(1);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const dashboard = useHrOperationsDashboard(filter);
  const queue = useHrOperationsQueue(selected, filter, page, PAGE_SIZE);
  const exportQueue = useExportHrOperationsQueue();

  function applyFilter(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    setFilter({
      recentCycles: 8,
      userId: draft.userId.trim() || undefined,
      type: draft.type.trim() || undefined,
      status: draft.status.trim() || undefined,
      overdueOnly: draft.overdueOnly || undefined,
    });
  }

  function openQueue(key: HrOperationsQueueKey) {
    setPage(1);
    setParams(selected === key ? {} : { queue: key });
  }

  async function runExport() {
    if (!selected) return;
    setNotice(null);
    setError(null);
    try {
      const fileName = await exportQueue.mutateAsync({ key: selected, filter });
      setNotice(`تمّ تنزيل «${fileName}». التصدير مُسجَّل في سجلّ التدقيق.`);
    } catch (err) {
      setError(errorMessage(err));
    }
  }

  // 403 صريح على اللوحة ⇒ رسالة صلاحيّة لا شاشة خطأ عامّة.
  const forbidden =
    (dashboard.error as { response?: { status?: number } } | undefined)?.response?.status === 403;

  const totalPages = queue.data ? Math.max(1, Math.ceil(queue.data.totalCount / queue.data.pageSize)) : 1;

  return (
    <div dir="rtl" className="space-y-6" data-testid="hr-operations-page">
      <header>
        <h1 className="text-xl font-bold text-navy">عمليّات الموارد البشريّة</h1>
        <p className="mt-1 text-sm text-ink-2">
          طوابير الإجراءات داخل نطاقك وحده. كلّ عدد هنا هو عدد صفوف طابوره تحت المرشِّح نفسه، وفتح
          البطاقة يعرض البنود ذاتها التي عُدَّت.
        </p>
      </header>

      {notice && <Alert tone="success">{notice}</Alert>}
      {error && <Alert tone="alert">{error}</Alert>}

      <Card className="p-4">
        <form onSubmit={applyFilter} className="grid gap-3 md:grid-cols-4" data-testid="hr-ops-filter">
          <Field label="معرّف الموظّف">
            <Input
              value={draft.userId}
              aria-label="معرّف الموظّف"
              onChange={(e) => setDraft({ ...draft, userId: e.target.value })}
            />
          </Field>
          <Field label="النوع">
            <Input
              value={draft.type}
              aria-label="النوع"
              onChange={(e) => setDraft({ ...draft, type: e.target.value })}
            />
          </Field>
          <Field label="الحالة">
            <Input
              value={draft.status}
              aria-label="الحالة"
              onChange={(e) => setDraft({ ...draft, status: e.target.value })}
            />
          </Field>
          <Field label="المهلة">
            <Select
              value={draft.overdueOnly ? 'overdue' : 'all'}
              aria-label="المهلة"
              onChange={(e) => setDraft({ ...draft, overdueOnly: e.target.value === 'overdue' })}
            >
              <option value="all">الكلّ</option>
              <option value="overdue">خارج المهلة فقط</option>
            </Select>
          </Field>
          <div className="md:col-span-4">
            <Button type="submit">تطبيق المرشِّحات</Button>
          </div>
        </form>
      </Card>

      {/* ── البطاقات ── */}
      {dashboard.isLoading ? (
        <CardsSkeleton count={11} />
      ) : forbidden ? (
        <EmptyState
          title="لا تملك صلاحيّة لوحة العمليّات"
          description="هذه اللوحة تحتاج تخويلًا صريحًا مستقلًّا عن الدور. راجع مسؤول النظام."
        />
      ) : dashboard.isError ? (
        <QueryError onRetry={() => dashboard.refetch()} />
      ) : dashboard.data ? (
        <>
          <p className="text-xs text-ink-2" data-testid="hr-ops-scope">
            النطاق: {dashboard.data.scope.scopeType} · {dashboard.data.scope.userCount} موظّفًا ·
            الفترات: {dashboard.data.periodKeys.join('، ') || '—'}
          </p>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {dashboard.data.cards.map((card) => (
              <QueueCard
                key={card.key}
                card={card}
                active={selected === card.key}
                onOpen={() => openQueue(card.key)}
              />
            ))}
          </div>
        </>
      ) : null}

      {/* ── التفصيل ── */}
      {selected && (
        <section data-testid="hr-ops-drilldown">
        <Card className="p-4">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-base font-semibold text-navy">
              {queue.data?.titleAr ?? 'تفصيل الطابور'}
              {queue.data ? (
                <span className="mr-2 text-sm font-normal text-ink-2">
                  ({queue.data.totalCount} بندًا · {queue.data.breachedCount} خارج المهلة)
                </span>
              ) : null}
            </h2>
            <div className="flex gap-2">
              {/* التصديرُ مفتاحٌ مستقلّ عن العرض: قد يفشل بـ403 ولو نجحت اللوحة، وذلك متوقَّع لا عطل. */}
              <Button
                variant="ghost"
                loading={exportQueue.isPending}
                onClick={runExport}
                data-testid="hr-ops-export"
              >
                تصدير الطابور
              </Button>
              <Button variant="ghost" onClick={() => setParams({})}>
                إغلاق
              </Button>
            </div>
          </div>

          {queue.isLoading ? (
            <TableSkeleton rows={6} cols={10} />
          ) : queue.isError ? (
            <QueryError onRetry={() => queue.refetch()} />
          ) : !queue.data || queue.data.rows.length === 0 ? (
            <EmptyState
              title="لا بنود في هذا الطابور"
              description="لا يوجد ما يستدعي إجراءً هنا ضمن نطاقك والمرشِّحات الحاليّة."
            />
          ) : (
            <>
              <QueueRows rows={queue.data.rows} />
              {totalPages > 1 && (
                <div className="mt-3 flex items-center justify-between text-sm text-ink-2">
                  <Button
                    variant="ghost"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    السابق
                  </Button>
                  <span>
                    صفحة {queue.data.page} من {totalPages}
                  </span>
                  <Button
                    variant="ghost"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    التالي
                  </Button>
                </div>
              )}
            </>
          )}
        </Card>
        </section>
      )}
    </div>
  );
}

// إدارة كتالوج خدمات B2B (الأدمن/CEO/GM عبر سياسة TemplateGovernance) — RC-3 Task 2 الجزء 2.
// إنشاء/تعديل/تفعيل/تعطيل/حذف ناعم(=تعطيل)/إعادة ترتيب/بحث/Pagination.
// إضافية بالكامل: تعيد استخدام نقاط النهاية القائمة (لا هجرة، لا endpoint جديد) — نظير صفحة إدارة الدورات.
import { useMemo, useState } from 'react';
import { Alert, Badge, Button, Card, EmptyState, Field, Input } from '../components/ui';
import { SectionTitle } from '../components/dashboard';
import { LoadingState, QueryError } from '../components/states';
import {
  useActivateService,
  useAdminServices,
  useCreateService,
  useDeactivateService,
  useDeleteService,
  useUpdateService,
  type ServiceWriteRequest,
} from '../lib/useServices';
import { apiErrorMessage } from '../lib/api';
import type { ServiceDto } from '../types/api';

const PAGE_SIZE = 10;

function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('ar-EG', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

export default function ServiceManagementPage() {
  const services = useAdminServices(); // القائمة الإدارية تُرجِع الجميع بما فيها المعطّلة.
  const create = useCreateService();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<ServiceDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  // نموذج الإنشاء.
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');

  const all = services.data ?? [];

  // ترتيب ثابت حسب SortOrder ثم الاسم — يطابق ترتيب القائمة العامّة.
  const sorted = useMemo(
    () => [...all].sort((a, b) => a.sortOrder - b.sortOrder || a.nameAr.localeCompare(b.nameAr, 'ar')),
    [all],
  );

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return sorted;
    return sorted.filter(
      (s) => s.nameAr.toLowerCase().includes(q) || (s.nameEn ?? '').toLowerCase().includes(q),
    );
  }, [sorted, search]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageRows = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  async function handleCreate() {
    setError(null);
    const trimmed = nameAr.trim();
    if (!trimmed) {
      setError('اسم الخدمة بالعربية مطلوب.');
      return;
    }
    // ترتيب افتراضي = بعد آخر خدمة موجودة.
    const nextOrder = sorted.length > 0 ? Math.max(...sorted.map((s) => s.sortOrder)) + 1 : 1;
    const req: ServiceWriteRequest = {
      nameAr: trimmed,
      nameEn: nameEn.trim() || null,
      sortOrder: nextOrder,
    };
    try {
      await create.mutateAsync(req);
      setNameAr('');
      setNameEn('');
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر إنشاء الخدمة.'));
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <SectionTitle
          title="إدارة الخدمات"
          hint="كتالوج خدمات B2B الذي يغذّي منتقي «الخدمة» في تقرير مبيعات B2B حسب الخدمة. التعطيل يخفي الخدمة من التقارير الجديدة ويحفظ التقارير القديمة."
        />
      </Card>

      <Card>
        <h3 className="mb-3 text-base font-semibold text-navy">إضافة خدمة جديدة</h3>
        {error && (
          <div className="mb-3">
            <Alert tone="alert">{error}</Alert>
          </div>
        )}
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="الاسم بالعربية *">
            <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} placeholder="مثال: تصميم موقع إلكتروني" />
          </Field>
          <Field label="الاسم بالإنجليزية (اختياري)">
            <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} placeholder="Website Design" />
          </Field>
        </div>
        <div className="mt-3">
          <Button onClick={handleCreate} disabled={create.isPending}>
            {create.isPending ? 'جارٍ الإضافة…' : 'إضافة الخدمة'}
          </Button>
        </div>
      </Card>

      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-base font-semibold text-navy">قائمة الخدمات</h3>
          <div className="w-full sm:w-72">
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="بحث بالاسم…"
            />
          </div>
        </div>

        {services.isLoading ? (
          <LoadingState />
        ) : services.isError ? (
          <QueryError onRetry={() => services.refetch()} />
        ) : all.length === 0 ? (
          <EmptyState
            title="لا توجد خدمات"
            description="لا توجد خدمات، يرجى إضافتها من لوحة الإدارة."
          />
        ) : filtered.length === 0 ? (
          <EmptyState title="لا نتائج" description="لا توجد خدمات مطابقة لبحثك." />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-right text-sm">
                <thead>
                  <tr className="border-b border-line text-ink-2">
                    <th className="px-3 py-2 font-medium">الاسم بالعربية</th>
                    <th className="px-3 py-2 font-medium">الاسم بالإنجليزية</th>
                    <th className="px-3 py-2 font-medium">الحالة</th>
                    <th className="px-3 py-2 font-medium">الترتيب</th>
                    <th className="px-3 py-2 font-medium">تاريخ الإنشاء</th>
                    <th className="px-3 py-2 font-medium">إجراءات</th>
                  </tr>
                </thead>
                <tbody>
                  {pageRows.map((s) => (
                    <ServiceRow
                      key={s.id}
                      service={s}
                      sorted={sorted}
                      onEdit={() => setEditing(s)}
                    />
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="mt-4 flex items-center justify-center gap-3">
                <Button
                  variant="ghost"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={safePage <= 1}
                >
                  السابق
                </Button>
                <span className="text-sm text-ink-2">
                  صفحة {safePage} من {totalPages}
                </span>
                <Button
                  variant="ghost"
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={safePage >= totalPages}
                >
                  التالي
                </Button>
              </div>
            )}
          </>
        )}
      </Card>

      {editing && <EditServiceModal service={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}

function ServiceRow({
  service,
  sorted,
  onEdit,
}: {
  service: ServiceDto;
  sorted: ServiceDto[];
  onEdit: () => void;
}) {
  const activate = useActivateService();
  const deactivate = useDeactivateService();
  const update = useUpdateService();
  const remove = useDeleteService();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const index = sorted.findIndex((s) => s.id === service.id);
  const prev = index > 0 ? sorted[index - 1] : null;
  const next = index < sorted.length - 1 ? sorted[index + 1] : null;
  const busy = activate.isPending || deactivate.isPending || update.isPending || remove.isPending;

  // إعادة الترتيب = تبديل SortOrder مع الجار عبر PUT (فحص التفرّد بالخادم يستثني الذات ⇒ التبديل آمن).
  async function swapWith(other: ServiceDto) {
    setError(null);
    try {
      await update.mutateAsync({
        id: service.id,
        req: { nameAr: service.nameAr, nameEn: service.nameEn, sortOrder: other.sortOrder },
      });
      await update.mutateAsync({
        id: other.id,
        req: { nameAr: other.nameAr, nameEn: other.nameEn, sortOrder: service.sortOrder },
      });
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر إعادة الترتيب.'));
    }
  }

  async function toggleActive() {
    setError(null);
    try {
      if (service.isActive) {
        // الحذف الناعم = تعطيل (لا حذف نهائي؛ التاريخ محفوظ لأن التقارير تخزّن اسم الخدمة نصًّا).
        if (!window.confirm(`تعطيل الخدمة «${service.nameAr}»؟ ستختفي من التقارير الجديدة وتبقى في التقارير القديمة.`)) return;
        await deactivate.mutateAsync(service.id);
      } else {
        await activate.mutateAsync(service.id);
      }
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر تغيير الحالة.'));
    }
  }

  // حذف آمن: نهائيّ إن لم تُستخدَم، وإلّا أرشفة (تعطيل). الرسالة النهائية تأتي من الخادم.
  async function handleDelete() {
    setError(null);
    setNotice(null);
    if (
      !window.confirm(
        `حذف الخدمة «${service.nameAr}»؟\n\nإن لم تكن مستخدَمة في أي تقرير ستُحذف نهائيًّا، وإن كانت مستخدَمة ستُؤرشَف (تُعطَّل) دون حذف. التقارير القديمة تبقى كما هي في الحالتين.`,
      )
    )
      return;
    try {
      const res = await remove.mutateAsync(service.id);
      setNotice(res.message);
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر حذف الخدمة.'));
    }
  }

  return (
    <>
      <tr className="border-b border-line/60">
        <td className="px-3 py-2 font-medium text-ink">{service.nameAr}</td>
        <td className="px-3 py-2 text-ink-2">{service.nameEn || '—'}</td>
        <td className="px-3 py-2">
          {service.isActive ? <Badge tone="success">نشطة</Badge> : <Badge tone="muted">معطّلة</Badge>}
        </td>
        <td className="px-3 py-2">
          <div className="flex items-center gap-1.5">
            <span className="text-ink-2">{service.sortOrder}</span>
            <button
              type="button"
              onClick={() => prev && swapWith(prev)}
              disabled={!prev || busy}
              title="تحريك لأعلى"
              className="rounded px-1.5 py-0.5 text-navy-600 hover:bg-navy-50 disabled:opacity-30"
            >
              ▲
            </button>
            <button
              type="button"
              onClick={() => next && swapWith(next)}
              disabled={!next || busy}
              title="تحريك لأسفل"
              className="rounded px-1.5 py-0.5 text-navy-600 hover:bg-navy-50 disabled:opacity-30"
            >
              ▼
            </button>
          </div>
        </td>
        <td className="px-3 py-2 text-ink-2">{formatDate(service.createdAtUtc)}</td>
        <td className="px-3 py-2">
          <div className="flex flex-wrap gap-1.5">
            <Button variant="ghost" onClick={onEdit} disabled={busy}>
              تعديل
            </Button>
            {service.isActive ? (
              <Button variant="danger" onClick={toggleActive} disabled={busy}>
                تعطيل
              </Button>
            ) : (
              <Button variant="ghost" onClick={toggleActive} disabled={busy}>
                تفعيل
              </Button>
            )}
            <Button variant="danger" onClick={handleDelete} disabled={busy}>
              حذف
            </Button>
          </div>
        </td>
      </tr>
      {error && (
        <tr>
          <td colSpan={6} className="px-3 pb-2">
            <Alert tone="alert">{error}</Alert>
          </td>
        </tr>
      )}
      {notice && (
        <tr>
          <td colSpan={6} className="px-3 pb-2">
            <Alert tone="navy">{notice}</Alert>
          </td>
        </tr>
      )}
    </>
  );
}

function EditServiceModal({ service, onClose }: { service: ServiceDto; onClose: () => void }) {
  const update = useUpdateService();
  const [nameAr, setNameAr] = useState(service.nameAr);
  const [nameEn, setNameEn] = useState(service.nameEn ?? '');
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    setError(null);
    const trimmed = nameAr.trim();
    if (!trimmed) {
      setError('اسم الخدمة بالعربية مطلوب.');
      return;
    }
    try {
      await update.mutateAsync({
        id: service.id,
        req: { nameAr: trimmed, nameEn: nameEn.trim() || null, sortOrder: service.sortOrder },
      });
      onClose();
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر حفظ التعديل.'));
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-xl border border-line bg-white p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="mb-3 text-base font-semibold text-navy">تعديل الخدمة</h3>
        {error && (
          <div className="mb-3">
            <Alert tone="alert">{error}</Alert>
          </div>
        )}
        <div className="space-y-3">
          <Field label="الاسم بالعربية *">
            <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
          </Field>
          <Field label="الاسم بالإنجليزية (اختياري)">
            <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
          </Field>
        </div>
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose} disabled={update.isPending}>
            إلغاء
          </Button>
          <Button onClick={handleSave} disabled={update.isPending}>
            {update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
          </Button>
        </div>
      </div>
    </div>
  );
}

// إدارة كتالوج تصنيفات التنفيذ (الأدمن/CEO/GM عبر سياسة TemplateGovernance) — RC-4 Task 4D2.
// عرض/إنشاء/تعديل الاسم والترتيب/تفعيل/تعطيل. لا حذف نهائيّ. Domain و Code غير قابلين للتعديل.
// تنبيه: تعديل الكتالوج لا يغيّر قوالب التنفيذ v3 القائمة (القيم لقطة داخل القالب) — تظهر عند إنشاء إصدار قالب جديد فقط.
import { useMemo, useState } from 'react';
import { Alert, Badge, Button, Card, EmptyState, Field, Input } from '../components/ui';
import { SectionTitle } from '../components/dashboard';
import { LoadingState, QueryError } from '../components/states';
import {
  useActivateExecutionTaxonomy,
  useCreateExecutionTaxonomy,
  useDeactivateExecutionTaxonomy,
  useExecutionTaxonomyAdmin,
  useUpdateExecutionTaxonomy,
  type CreateExecutionTaxonomyRequest,
} from '../lib/useExecutionTaxonomy';
import { apiErrorMessage } from '../lib/api';
import type { ExecutionTaxonomyDto } from '../types/api';

// المجالات الثابتة بترتيب العرض + تسمياتها العربية.
// الثلاثة عشر الأولى = قوالب التنفيذ v3/v4. الستة الأخيرة = P0 منصّة التنفيذ العامة.
const DOMAINS: { code: string; label: string }[] = [
  { code: 'content_type', label: 'نوع المحتوى' },
  { code: 'content_goal', label: 'هدف المحتوى' },
  { code: 'work_status', label: 'حالة العمل' },
  { code: 'design_type', label: 'نوع التصميم' },
  { code: 'design_status', label: 'حالة التصميم' },
  { code: 'design_tool', label: 'أداة التصميم' },
  { code: 'video_type', label: 'نوع الفيديو' },
  { code: 'edit_type', label: 'نوع المونتاج' },
  { code: 'video_duration', label: 'مدة الفيديو' },
  { code: 'video_status', label: 'حالة الفيديو' },
  { code: 'activity_type', label: 'نوع النشاط' },
  { code: 'interaction_result', label: 'نتيجة التفاعل' },
  { code: 'response_time', label: 'زمن الاستجابة' },
  // P0 — منصّة التنفيذ العامة
  { code: 'workstream_type', label: 'نوع تيار العمل' },
  { code: 'deliverable', label: 'المُخرَج' },
  { code: 'usage_context', label: 'سياق الاستخدام' },
  { code: 'workflow_step', label: 'خطوة سير العمل' },
  { code: 'delay_reason', label: 'سبب التأخير' },
  { code: 'platform_channel', label: 'المنصّة / القناة' },
];

function domainLabel(code: string): string {
  return DOMAINS.find((d) => d.code === code)?.label ?? code;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('ar-EG', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

type StatusFilter = 'all' | 'active' | 'inactive';

export default function ExecutionTaxonomyManagementPage() {
  const values = useExecutionTaxonomyAdmin();
  const create = useCreateExecutionTaxonomy();
  const [domainFilter, setDomainFilter] = useState<string>('content_type');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<ExecutionTaxonomyDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  // نموذج الإنشاء.
  const [newDomain, setNewDomain] = useState<string>('content_type');
  const [newCode, setNewCode] = useState('');
  const [newNameAr, setNewNameAr] = useState('');
  const [newNameEn, setNewNameEn] = useState('');

  const all = values.data ?? [];

  // القيم داخل المجال المختار مرتّبة حسب SortOrder ثم الاسم (لإعادة الترتيب والجيران).
  const domainSorted = useMemo(
    () =>
      all
        .filter((v) => v.domain === domainFilter)
        .sort((a, b) => a.sortOrder - b.sortOrder || a.nameAr.localeCompare(b.nameAr, 'ar')),
    [all, domainFilter],
  );

  const filtered = useMemo(() => {
    let rows = domainSorted;
    if (statusFilter === 'active') rows = rows.filter((v) => v.isActive);
    else if (statusFilter === 'inactive') rows = rows.filter((v) => !v.isActive);
    const q = search.trim().toLowerCase();
    if (q) {
      rows = rows.filter(
        (v) =>
          v.code.toLowerCase().includes(q) ||
          v.nameAr.toLowerCase().includes(q) ||
          (v.nameEn ?? '').toLowerCase().includes(q),
      );
    }
    return rows;
  }, [domainSorted, statusFilter, search]);

  async function handleCreate() {
    setError(null);
    const code = newCode.trim();
    const nameAr = newNameAr.trim();
    if (!code) {
      setError('الرمز (Code) مطلوب.');
      return;
    }
    if (!nameAr) {
      setError('الاسم بالعربية مطلوب.');
      return;
    }
    // الترتيب الافتراضي = بعد آخر قيمة في نفس المجال.
    const inDomain = all.filter((v) => v.domain === newDomain);
    const nextOrder = inDomain.length > 0 ? Math.max(...inDomain.map((v) => v.sortOrder)) + 10 : 10;
    const req: CreateExecutionTaxonomyRequest = {
      domain: newDomain,
      code,
      nameAr,
      nameEn: newNameEn.trim() || null,
      sortOrder: nextOrder,
    };
    try {
      await create.mutateAsync(req);
      setNewCode('');
      setNewNameAr('');
      setNewNameEn('');
      setDomainFilter(newDomain); // اعرض المجال الذي أُضيفت إليه القيمة.
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر إنشاء قيمة التصنيف.'));
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <SectionTitle
          title="إدارة تصنيفات التنفيذ"
          hint="كتالوج القيم الذي يغذّي قوائم قوالب التنفيذ (كاتب المحتوى / التصميم / الفيديو / المديرشن)."
        />
        <div className="mt-2">
          <Alert tone="navy">
            تعديل الكتالوج لا يغيّر قوالب التنفيذ أو التقارير القائمة؛ القيم الجديدة/المعطّلة تظهر فقط عند إنشاء إصدار
            قالب جديد. التعطيل يخفي القيمة من التقارير الجديدة ويبقي القديمة كما هي دون حذف.
          </Alert>
        </div>
      </Card>

      <Card>
        <h3 className="mb-3 text-base font-semibold text-navy">إضافة قيمة جديدة</h3>
        {error && (
          <div className="mb-3">
            <Alert tone="alert">{error}</Alert>
          </div>
        )}
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="المجال *">
            <select
              value={newDomain}
              onChange={(e) => setNewDomain(e.target.value)}
              className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm"
            >
              {DOMAINS.map((d) => (
                <option key={d.code} value={d.code}>
                  {d.label}
                </option>
              ))}
            </select>
          </Field>
          <Field label="الرمز (Code) *">
            <Input value={newCode} onChange={(e) => setNewCode(e.target.value)} placeholder="carousel" />
          </Field>
          <Field label="الاسم بالعربية *">
            <Input value={newNameAr} onChange={(e) => setNewNameAr(e.target.value)} placeholder="كاروسيل" />
          </Field>
          <Field label="الاسم بالإنجليزية (اختياري)">
            <Input value={newNameEn} onChange={(e) => setNewNameEn(e.target.value)} placeholder="Carousel" />
          </Field>
        </div>
        <div className="mt-3">
          <Button onClick={handleCreate} disabled={create.isPending}>
            {create.isPending ? 'جارٍ الإضافة…' : 'إضافة القيمة'}
          </Button>
        </div>
      </Card>

      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-base font-semibold text-navy">قائمة القيم</h3>
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={domainFilter}
              onChange={(e) => setDomainFilter(e.target.value)}
              aria-label="المجال"
              className="rounded-lg border border-line bg-white px-3 py-2 text-sm"
            >
              {DOMAINS.map((d) => (
                <option key={d.code} value={d.code}>
                  {d.label}
                </option>
              ))}
            </select>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
              aria-label="الحالة"
              className="rounded-lg border border-line bg-white px-3 py-2 text-sm"
            >
              <option value="all">كل الحالات</option>
              <option value="active">النشطة</option>
              <option value="inactive">المعطّلة</option>
            </select>
            <div className="w-full sm:w-56">
              <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالرمز أو الاسم…" />
            </div>
          </div>
        </div>

        {values.isLoading ? (
          <LoadingState />
        ) : values.isError ? (
          <QueryError onRetry={() => values.refetch()} />
        ) : domainSorted.length === 0 ? (
          <EmptyState
            title="لا توجد قيم"
            description={`لا توجد قيم في مجال «${domainLabel(domainFilter)}». أضِف قيمة جديدة من الأعلى.`}
          />
        ) : filtered.length === 0 ? (
          <EmptyState title="لا نتائج" description="لا توجد قيم مطابقة لبحثك أو الفلتر المحدّد." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-right text-sm">
              <thead>
                <tr className="border-b border-line text-ink-2">
                  <th className="px-3 py-2 font-medium">الرمز</th>
                  <th className="px-3 py-2 font-medium">الاسم بالعربية</th>
                  <th className="px-3 py-2 font-medium">الاسم بالإنجليزية</th>
                  <th className="px-3 py-2 font-medium">الحالة</th>
                  <th className="px-3 py-2 font-medium">الترتيب</th>
                  <th className="px-3 py-2 font-medium">تاريخ الإنشاء</th>
                  <th className="px-3 py-2 font-medium">إجراءات</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((v) => (
                  <TaxonomyRow key={v.id} value={v} domainSorted={domainSorted} onEdit={() => setEditing(v)} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {editing && <EditTaxonomyModal value={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}

function TaxonomyRow({
  value,
  domainSorted,
  onEdit,
}: {
  value: ExecutionTaxonomyDto;
  domainSorted: ExecutionTaxonomyDto[];
  onEdit: () => void;
}) {
  const activate = useActivateExecutionTaxonomy();
  const deactivate = useDeactivateExecutionTaxonomy();
  const update = useUpdateExecutionTaxonomy();
  const [error, setError] = useState<string | null>(null);

  const index = domainSorted.findIndex((v) => v.id === value.id);
  const prev = index > 0 ? domainSorted[index - 1] : null;
  const next = index < domainSorted.length - 1 ? domainSorted[index + 1] : null;
  const busy = activate.isPending || deactivate.isPending || update.isPending;

  // إعادة الترتيب = تبديل SortOrder مع الجار داخل نفس المجال عبر PUT.
  async function swapWith(other: ExecutionTaxonomyDto) {
    setError(null);
    try {
      await update.mutateAsync({
        id: value.id,
        req: { nameAr: value.nameAr, nameEn: value.nameEn, sortOrder: other.sortOrder },
      });
      await update.mutateAsync({
        id: other.id,
        req: { nameAr: other.nameAr, nameEn: other.nameEn, sortOrder: value.sortOrder },
      });
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر إعادة الترتيب.'));
    }
  }

  async function toggleActive() {
    setError(null);
    try {
      if (value.isActive) {
        if (
          !window.confirm(
            `تعطيل القيمة «${value.nameAr}»؟ ستختفي من التقارير الجديدة عند إنشاء إصدار قالب جديد، وتبقى في التقارير القديمة.`,
          )
        )
          return;
        await deactivate.mutateAsync(value.id);
      } else {
        await activate.mutateAsync(value.id);
      }
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر تغيير الحالة.'));
    }
  }

  return (
    <>
      <tr className="border-b border-line/60">
        <td className="px-3 py-2 font-mono text-xs text-ink-2">{value.code}</td>
        <td className="px-3 py-2 font-medium text-ink">{value.nameAr}</td>
        <td className="px-3 py-2 text-ink-2">{value.nameEn || '—'}</td>
        <td className="px-3 py-2">
          {value.isActive ? <Badge tone="success">نشطة</Badge> : <Badge tone="muted">معطّلة</Badge>}
        </td>
        <td className="px-3 py-2">
          <div className="flex items-center gap-1.5">
            <span className="text-ink-2">{value.sortOrder}</span>
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
        <td className="px-3 py-2 text-ink-2">{formatDate(value.createdAtUtc)}</td>
        <td className="px-3 py-2">
          <div className="flex flex-wrap gap-1.5">
            <Button variant="ghost" onClick={onEdit} disabled={busy}>
              تعديل
            </Button>
            {value.isActive ? (
              <Button variant="danger" onClick={toggleActive} disabled={busy}>
                تعطيل
              </Button>
            ) : (
              <Button variant="ghost" onClick={toggleActive} disabled={busy}>
                تفعيل
              </Button>
            )}
          </div>
        </td>
      </tr>
      {error && (
        <tr>
          <td colSpan={7} className="px-3 pb-2">
            <Alert tone="alert">{error}</Alert>
          </td>
        </tr>
      )}
    </>
  );
}

function EditTaxonomyModal({ value, onClose }: { value: ExecutionTaxonomyDto; onClose: () => void }) {
  const update = useUpdateExecutionTaxonomy();
  const [nameAr, setNameAr] = useState(value.nameAr);
  const [nameEn, setNameEn] = useState(value.nameEn ?? '');
  const [sortOrder, setSortOrder] = useState(String(value.sortOrder));
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    setError(null);
    const trimmed = nameAr.trim();
    if (!trimmed) {
      setError('الاسم بالعربية مطلوب.');
      return;
    }
    const order = Number.parseInt(sortOrder, 10);
    if (Number.isNaN(order)) {
      setError('الترتيب يجب أن يكون رقمًا.');
      return;
    }
    try {
      await update.mutateAsync({
        id: value.id,
        req: { nameAr: trimmed, nameEn: nameEn.trim() || null, sortOrder: order },
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
        <h3 className="mb-1 text-base font-semibold text-navy">تعديل القيمة</h3>
        <p className="mb-3 text-xs text-ink-2">
          المجال: {domainLabel(value.domain)} — الرمز: <span className="font-mono">{value.code}</span> (غير قابلين
          للتعديل)
        </p>
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
          <Field label="الترتيب">
            <Input value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} inputMode="numeric" />
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

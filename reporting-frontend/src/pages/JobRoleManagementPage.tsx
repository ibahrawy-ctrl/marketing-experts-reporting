// إدارة المسمّيات الوظيفية (JobRole CRUD) — إنشاء/تعديل/أرشفة/إعادة تفعيل مع عدّاد الموظفين والقوالب.
// لا حذف نهائي في هذه المرحلة (أرشفة فقط). متاح لـ Admin/CeoSupport/HR/GM/CEO (سياسة UserJobRoleManagement خادمًا).
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useJobRolesManage,
  useDepartments,
  useCreateJobRole,
  useUpdateJobRole,
  useArchiveJobRole,
  useReactivateJobRole,
} from '../lib/useDirectory';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { apiErrorMessage } from '../lib/api';
import type { JobRoleDetailDto } from '../types/api';

type StatusFilter = 'active' | 'archived' | 'all';

const EMPTY_FORM = { nameAr: '', nameEn: '', code: '', departmentId: '' };

export default function JobRoleManagementPage() {
  const roles = useJobRolesManage();
  const departments = useDepartments();
  const createRole = useCreateJobRole();
  const updateRole = useUpdateJobRole();
  const archiveRole = useArchiveJobRole();
  const reactivateRole = useReactivateJobRole();

  const [statusFilter, setStatusFilter] = useState<StatusFilter>('active');
  const [q, setQ] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ ...EMPTY_FORM });
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);

  const deptName = (id: string | null) =>
    (departments.data ?? []).find((d) => d.id === id)?.nameAr ?? '— غير محدّدة —';

  const list = useMemo(() => {
    let items = roles.data ?? [];
    if (statusFilter === 'active') items = items.filter((r) => r.isActive);
    else if (statusFilter === 'archived') items = items.filter((r) => !r.isActive);
    if (q.trim()) {
      const needle = q.trim().toLowerCase();
      items = items.filter(
        (r) =>
          r.nameAr.includes(q.trim()) ||
          (r.nameEn ?? '').toLowerCase().includes(needle) ||
          (r.code ?? '').toLowerCase().includes(needle),
      );
    }
    return [...items].sort((a, b) => a.nameAr.localeCompare(b.nameAr, 'ar'));
  }, [roles.data, statusFilter, q]);

  if (roles.isLoading || departments.isLoading) return <LoadingState label="يتم تحميل المسمّيات الوظيفية…" />;
  if (roles.isError) return <QueryError onRetry={() => roles.refetch()} description="حدث خطأ أثناء جلب المسمّيات الوظيفية." />;

  function resetForm() {
    setEditingId(null);
    setForm({ ...EMPTY_FORM });
  }

  function startEdit(r: JobRoleDetailDto) {
    setEditingId(r.id);
    setForm({
      nameAr: r.nameAr,
      nameEn: r.nameEn ?? '',
      code: r.code ?? '',
      departmentId: r.departmentId ?? '',
    });
    setErrorMsg(null);
    setOkMsg(null);
  }

  async function submit() {
    setErrorMsg(null);
    setOkMsg(null);
    const req = {
      nameAr: form.nameAr.trim(),
      nameEn: form.nameEn.trim() || null,
      code: form.code.trim() || null,
      departmentId: form.departmentId || null,
    };
    try {
      if (editingId) {
        await updateRole.mutateAsync({ jobRoleId: editingId, req });
        setOkMsg(`تم تعديل المسمّى الوظيفي «${req.nameAr}».`);
      } else {
        await createRole.mutateAsync(req);
        setOkMsg(`تم إنشاء المسمّى الوظيفي «${req.nameAr}».`);
      }
      resetForm();
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function archive(r: JobRoleDetailDto) {
    if (!window.confirm(`أرشفة المسمّى «${r.nameAr}»؟ لن يظهر في قوائم الاختيار الجديدة، ولن يتأثّر الموظفون أو القوالب الحالية.`)) return;
    setErrorMsg(null);
    setOkMsg(null);
    try {
      await archiveRole.mutateAsync(r.id);
      setOkMsg(`تمت أرشفة «${r.nameAr}».`);
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function reactivate(r: JobRoleDetailDto) {
    setErrorMsg(null);
    setOkMsg(null);
    try {
      await reactivateRole.mutateAsync(r.id);
      setOkMsg(`تمت إعادة تفعيل «${r.nameAr}».`);
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  const saving = createRole.isPending || updateRole.isPending;
  const busy = saving || archiveRole.isPending || reactivateRole.isPending;

  return (
    <div className="space-y-6">
      <SectionTitle
        title="إدارة المسمّيات الوظيفية"
        hint="أنشئ المسمّيات وعدّلها وأرشفها. المسمّى يربط قوالب التقارير بالموظفين — لا يُحذف نهائيًّا، بل يُؤرشف فقط."
      />

      {errorMsg && <Alert tone="alert">{errorMsg}</Alert>}
      {okMsg && <Alert tone="success">{okMsg}</Alert>}

      {/* نموذج إنشاء/تعديل */}
      <Card>
        <SectionTitle title={editingId ? 'تعديل مسمّى وظيفي' : 'إنشاء مسمّى وظيفي جديد'} />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="الاسم بالعربية" help="إلزامي — لا يُسمح بتكرار اسم عربي قائم.">
            <Input value={form.nameAr} onChange={(e) => setForm({ ...form, nameAr: e.target.value })} placeholder="مثال: مصمم موشن جرافيك" />
          </Field>
          <Field label="الاسم بالإنجليزية (اختياري)">
            <Input value={form.nameEn} onChange={(e) => setForm({ ...form, nameEn: e.target.value })} placeholder="Motion Graphic Designer" />
          </Field>
          <Field label="الرمز (اختياري)">
            <Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="MGD" />
          </Field>
          <Field label="الإدارة الافتراضية (اختياري)">
            <Select value={form.departmentId} onChange={(e) => setForm({ ...form, departmentId: e.target.value })}>
              <option value="">— غير محدّدة —</option>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>{d.nameAr}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="mt-3 flex items-center gap-3">
          <Button onClick={submit} disabled={!form.nameAr.trim() || saving}>
            {saving ? 'جارٍ الحفظ…' : editingId ? 'حفظ التعديل' : 'إنشاء المسمّى'}
          </Button>
          {editingId && (
            <Button variant="ghost" onClick={resetForm} disabled={saving}>إلغاء التعديل</Button>
          )}
        </div>
      </Card>

      {/* قائمة المسمّيات */}
      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <SectionTitle title={`المسمّيات (${list.length})`} />
          <div className="flex flex-wrap items-center gap-2">
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="ابحث بالاسم أو الرمز…" className="max-w-[220px]" />
            <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)} className="max-w-[160px]">
              <option value="active">النشطة</option>
              <option value="archived">المؤرشفة</option>
              <option value="all">الكل</option>
            </Select>
          </div>
        </div>

        {list.length === 0 ? (
          <p className="py-10 text-center text-sm text-ink-2">لا توجد مسمّيات مطابقة.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[820px] text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">المسمّى</th>
                  <th className="px-2 py-2 font-semibold">الإدارة الافتراضية</th>
                  <th className="px-2 py-2 font-semibold">الموظفون</th>
                  <th className="px-2 py-2 font-semibold">القوالب</th>
                  <th className="px-2 py-2 font-semibold">الحالة</th>
                  <th className="px-2 py-2 font-semibold">إجراءات</th>
                </tr>
              </thead>
              <tbody>
                {list.map((r) => (
                  <tr key={r.id} className="border-b border-line last:border-0">
                    <td className="px-2 py-2">
                      <div className="font-medium text-navy">{r.nameAr}</div>
                      {(r.nameEn || r.code) && (
                        <div className="text-xs text-ink-3">{[r.nameEn, r.code].filter(Boolean).join(' · ')}</div>
                      )}
                    </td>
                    <td className="px-2 py-2 text-ink-2">{r.departmentName ?? deptName(r.departmentId)}</td>
                    <td className="px-2 py-2"><Badge tone={r.employeeCount > 0 ? 'navy' : 'muted'}>{r.employeeCount}</Badge></td>
                    <td className="px-2 py-2">
                      {r.templateCount > 0 ? (
                        <Link to={`/app/report-templates?jobRoleId=${r.id}`} className="font-semibold text-navy underline">
                          {r.templateCount} — عرض القوالب
                        </Link>
                      ) : (
                        <Badge tone="muted">0</Badge>
                      )}
                    </td>
                    <td className="px-2 py-2">
                      <Badge tone={r.isActive ? 'success' : 'muted'}>{r.isActive ? 'نشط' : 'مؤرشف'}</Badge>
                    </td>
                    <td className="px-2 py-2">
                      <div className="flex flex-wrap gap-1.5">
                        <Button variant="ghost" onClick={() => startEdit(r)} disabled={busy}>تعديل</Button>
                        {r.isActive ? (
                          <Button variant="ghost" onClick={() => archive(r)} disabled={busy}>أرشفة</Button>
                        ) : (
                          <Button variant="ghost" onClick={() => reactivate(r)} disabled={busy}>إعادة تفعيل</Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="mt-3 text-xs text-ink-3">
          «الموظفون» = عدد الموظفين المسنَد لهم هذا المسمّى. «القوالب» = عدد قوالب التقارير المرتبطة به (مباشرةً أو عبر إسناد).
          الأرشفة لا تمسّ الموظفين أو التقارير الحالية — تمنع فقط ظهور المسمّى في الاختيارات الجديدة.
        </p>
      </Card>
    </div>
  );
}

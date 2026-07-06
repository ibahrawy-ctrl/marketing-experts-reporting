// إدارة المسمّيات الوظيفية للموظفين — سطح مخصّص للقراءة + تعديل المسمّى الوظيفي (JobRole) فقط.
// لا يسمح بإنشاء/حذف/تعطيل مستخدم، ولا بتغيير الأدوار أو البريد أو كلمة المرور أو أي بيانات أخرى.
// متاح لـ Admin/CeoSupport/HR/GM/CEO (مفروض خادمًا عبر سياسة UserJobRoleManagement وفي الواجهة بالتوجيه).
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useDirectoryUsers,
  useTeams,
  useDepartments,
  useJobRoles,
  useUpdateUserJobRole,
} from '../lib/useDirectory';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { apiErrorMessage } from '../lib/api';
import type { DirectoryUserDto } from '../types/api';

export default function JobRolesAssignmentPage() {
  const users = useDirectoryUsers(false); // الموظفون النشطون فقط — لا تعطيل/تفعيل من هذا السطح.
  const teams = useTeams();
  const departments = useDepartments();
  const jobRoles = useJobRoles();
  const updateJobRole = useUpdateUserJobRole();

  const [q, setQ] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [newJobRoleId, setNewJobRoleId] = useState<string>('');
  const [notes, setNotes] = useState('');
  const [savedMsg, setSavedMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const allUsers = useMemo(() => users.data ?? [], [users.data]);
  const selected = allUsers.find((u) => u.id === selectedId) ?? null;

  if (users.isLoading || teams.isLoading || jobRoles.isLoading)
    return <LoadingState label="يتم تحميل بيانات الموظفين والمسمّيات…" />;
  if (users.isError || teams.isError || jobRoles.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
          jobRoles.refetch();
        }}
        description="حدث خطأ أثناء جلب البيانات. أعد المحاولة."
      />
    );

  const deptName = (id: string | null) =>
    (departments.data ?? []).find((d) => d.id === id)?.nameAr ?? '—';
  const teamName = (id: string | null) => (teams.data ?? []).find((t) => t.id === id)?.nameAr ?? '—';
  const jobRoleName = (id: string | null) =>
    (jobRoles.data ?? []).find((j) => j.id === id)?.nameAr ?? '— غير محدّد —';

  let list = allUsers;
  if (q.trim())
    list = list.filter(
      (u) => u.fullName.includes(q.trim()) || u.email.toLowerCase().includes(q.trim().toLowerCase()),
    );

  function pick(u: DirectoryUserDto) {
    setSelectedId(u.id);
    setNewJobRoleId(u.jobRoleId ?? '');
    setNotes('');
    setSavedMsg(null);
    setErrorMsg(null);
  }

  async function save() {
    if (!selected) return;
    setSavedMsg(null);
    setErrorMsg(null);
    try {
      await updateJobRole.mutateAsync({
        userId: selected.id,
        req: { jobRoleId: newJobRoleId || null, notes: notes.trim() || null },
      });
      setSavedMsg(
        `تم تحديث المسمّى الوظيفي للموظف «${selected.fullName}» إلى «${
          newJobRoleId ? jobRoleName(newJobRoleId) : '— بدون مسمّى —'
        }». ستظهر قوالب التقارير الجديدة وفق المسمّى الجديد عند إنشاء تقارير لاحقة. التقارير المُسلَّمة سابقًا لا تتغيّر.`,
      );
      setNotes('');
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  const currentJobRoleId = selected?.jobRoleId ?? '';
  const unchanged = !!selected && (newJobRoleId || '') === (currentJobRoleId || '');

  return (
    <div className="space-y-6">
      <SectionTitle
        title="إدارة المسمّيات الوظيفية للموظفين"
        hint="سطح مخصّص لتعديل المسمّى الوظيفي للموظف فقط — لا يمسّ الاسم أو البريد أو الأدوار أو كلمة المرور أو حالة التفعيل."
      />

      <Alert tone="gold">
        تنبيه: تغيير المسمّى الوظيفي قد يغيّر قوالب التقارير المعروضة للموظف عند إنشاء تقارير جديدة
        (قوالب التتبّع حسب المسمّى). الاستثناء/التضمين الخاص بالموظف يبقى الأعلى أولويةً، والتقارير
        المُسلَّمة سابقًا لا تتغيّر ولا تُحذف.
      </Alert>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,360px)_1fr]">
        {/* قائمة الموظفين + البحث */}
        <Card>
          <Field label="ابحث عن موظف" help="بالاسم أو البريد الإلكتروني">
            <Input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="اكتب اسم الموظف أو بريده…"
            />
          </Field>
          <div className="mt-3 max-h-[28rem] divide-y divide-line overflow-y-auto rounded-lg border border-line">
            {list.length === 0 && (
              <p className="p-4 text-sm text-ink-2">لا يوجد موظفون مطابقون.</p>
            )}
            {list.map((u) => (
              <button
                key={u.id}
                onClick={() => pick(u)}
                className={`block w-full px-4 py-3 text-right transition hover:bg-navy-50 ${
                  u.id === selectedId ? 'bg-navy-50' : 'bg-white'
                }`}
              >
                <p className="font-semibold text-navy">{u.fullName}</p>
                <p className="en text-xs text-ink-2">{u.email}</p>
                <p className="mt-1 text-xs text-ink-2">
                  المسمّى الحالي: {jobRoleName(u.jobRoleId)}
                </p>
              </button>
            ))}
          </div>
        </Card>

        {/* تفاصيل الموظف المختار + تعديل المسمّى */}
        <Card>
          {!selected ? (
            <p className="py-16 text-center text-ink-2">اختر موظفًا من القائمة لعرض مسمّاه وتعديله.</p>
          ) : (
            <div className="space-y-5">
              <div>
                <h3 className="text-lg font-bold text-navy">{selected.fullName}</h3>
                <p className="en text-sm text-ink-2">{selected.email}</p>
                <div className="mt-2 flex flex-wrap gap-2 text-xs">
                  <Badge tone="navy">الإدارة: {deptName(selected.departmentId)}</Badge>
                  <Badge tone="gold">الفريق: {teamName(selected.teamId)}</Badge>
                </div>
              </div>

              <div className="rounded-lg border border-line bg-offwhite p-3">
                <p className="text-sm text-ink-2">المسمّى الوظيفي الحالي</p>
                <p className="mt-1 font-semibold text-navy">{jobRoleName(selected.jobRoleId)}</p>
              </div>

              <Field
                label="المسمّى الوظيفي الجديد"
                help="اختر «— بدون مسمّى —» لإزالة المسمّى الوظيفي عن الموظف."
              >
                <Select value={newJobRoleId} onChange={(e) => setNewJobRoleId(e.target.value)}>
                  <option value="">— بدون مسمّى —</option>
                  {(jobRoles.data ?? [])
                    .filter((j) => j.isActive || j.id === selected.jobRoleId)
                    .map((j) => (
                      <option key={j.id} value={j.id}>
                        {j.nameAr}
                        {j.code ? ` (${j.code})` : ''}
                      </option>
                    ))}
                </Select>
              </Field>

              <Field label="ملاحظة (اختياري)" help="تُسجَّل في سجل التدقيق مع التغيير.">
                <Input
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="سبب التغيير أو ملاحظة مرجعية…"
                />
              </Field>

              {errorMsg && <Alert tone="alert">{errorMsg}</Alert>}
              {savedMsg && (
                <Alert tone="success">
                  {savedMsg}{' '}
                  <Link to="/app/report-templates" className="font-semibold underline">
                    عرض قوالب التقارير والموظفين المرتبطين
                  </Link>
                </Alert>
              )}

              <div className="flex items-center gap-3">
                <Button onClick={save} disabled={unchanged || updateJobRole.isPending}>
                  {updateJobRole.isPending ? 'يتم الحفظ…' : 'حفظ المسمّى الوظيفي'}
                </Button>
                {unchanged && (
                  <span className="text-sm text-ink-2">المسمّى المختار مطابق للحالي — لا تغيير.</span>
                )}
              </div>
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

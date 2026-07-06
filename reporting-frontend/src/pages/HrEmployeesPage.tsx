// إدارة بيانات الموظفين (الموارد البشرية) — حزمة HR A.
// سطحان محدودان فقط: (1) تعديل الاسم الكامل، (2) تعديل التنظيم الوظيفي (الإدارة/الفريق/المدير).
// لا يعرض: إنشاء/حذف/تعطيل مستخدم، إدارة الأدوار/الصلاحيات، إعادة تعيين كلمة المرور، تعديل البريد.
// كل القيود الأمنية مفروضة خادمًا (PATCH /basic سياسة UserBasicManagement، PATCH /org-assignment سياسة UserOrgAssignment).
import { Fragment, useMemo, useState } from 'react';
import {
  useHrDirectoryUsers,
  useHrDirectoryDepartments,
  useHrDirectoryTeams,
  useHrDirectoryManagers,
  useJobRoles,
  useUpdateUserBasic,
  useUpdateUserOrgAssignment,
} from '../lib/useDirectory';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import type { DepartmentDto, HrDirectoryUserDto, Role, TeamDto } from '../types/api';

// أدوار تعديل البيانات الأساسية — تطابق سياسة UserBasicManagement بالخادم.
const BASIC_EDIT_ROLES: Role[] = ['Admin', 'CeoSupport', 'HR'];
// أدوار تعديل التنظيم الوظيفي — تطابق سياسة UserOrgAssignment بالخادم.
const ORG_EDIT_ROLES: Role[] = ['Admin', 'CeoSupport', 'HR', 'GeneralManager', 'CEO'];

type Editing = { userId: string; mode: 'basic' | 'org' } | null;

export default function HrEmployeesPage() {
  const { hasAnyRole } = useAuth();
  const isAdmin = hasAnyRole('Admin');
  const canEditBasic = hasAnyRole(...BASIC_EDIT_ROLES);
  const canEditOrg = hasAnyRole(...ORG_EDIT_ROLES);

  // قوائم «دليل الموارد البشرية» المخصّصة (قراءة على مستوى الشركة، منفصلة عن الدليل العام، محكومة بسياسة HrDirectoryRead).
  const users = useHrDirectoryUsers();
  const departments = useHrDirectoryDepartments();
  const teams = useHrDirectoryTeams();
  const managersQuery = useHrDirectoryManagers();
  const jobRoles = useJobRoles();

  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Editing>(null);

  const deptName = useMemo(() => {
    const m = new Map<string, string>();
    (departments.data ?? []).forEach((d) => m.set(d.id, d.nameAr));
    return m;
  }, [departments.data]);
  const teamName = useMemo(() => {
    const m = new Map<string, string>();
    (teams.data ?? []).forEach((t) => m.set(t.id, t.nameAr));
    return m;
  }, [teams.data]);
  const userName = useMemo(() => {
    const m = new Map<string, string>();
    (users.data ?? []).forEach((u) => m.set(u.id, u.fullName));
    return m;
  }, [users.data]);
  const roleName = useMemo(() => {
    const m = new Map<string, string>();
    (jobRoles.data ?? []).forEach((j) => m.set(j.id, j.nameAr));
    return m;
  }, [jobRoles.data]);

  const rows = useMemo(() => {
    const list = users.data ?? [];
    const term = search.trim();
    return term ? list.filter((u) => u.fullName.includes(term) || u.email.includes(term)) : list;
  }, [users.data, search]);

  if (users.isLoading) return <LoadingState label="يتم تحميل قائمة الموظفين…" />;
  if (users.isError) return <QueryError onRetry={() => users.refetch()} description="تعذّر جلب قائمة الموظفين." />;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">إدارة بيانات الموظفين</h1>
        <p className="mt-1 text-sm text-ink-2">
          شاشة لتعديل الاسم الكامل والتنظيم الوظيفي فقط. الأدوار والصلاحيات وكلمات المرور وتفعيل/تعطيل الحسابات
          تُدار من صفحة إدارة المستخدمين الرئيسية.
        </p>
      </div>

      {isAdmin ? (
        <Alert tone="navy">
          أنت تعمل بصلاحية مدير النظام. يمكنك إدارة بيانات الموظفين التشغيلية من هذه الشاشة، وتتوفر إدارة
          المستخدمين الكاملة مثل الأدوار وكلمات المرور والتفعيل والتعطيل من{' '}
          <span className="font-bold">شاشة إدارة المستخدمين</span> الرئيسية.
        </Alert>
      ) : (
        <Alert tone="gold">
          هذا السطح لا يمنحك صلاحيات خارج حزمة الموارد البشرية: لا يمكن من هنا إنشاء/حذف/تعطيل حساب، ولا إدارة
          الأدوار أو الصلاحيات، ولا إعادة تعيين كلمات المرور، ولا تعديل البريد. أي محاولة غير مصرّح بها يرفضها
          الخادم.
        </Alert>
      )}

      <Card>
        <div className="w-full sm:w-72">
          <Field label="بحث">
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="الاسم أو البريد…" />
          </Field>
        </div>
      </Card>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[820px] text-right text-sm">
          <thead className="border-b border-line bg-navy-50 text-xs text-ink-2">
            <tr>
              <th className="px-4 py-3 font-semibold">الموظف</th>
              <th className="px-4 py-3 font-semibold">الإدارة</th>
              <th className="px-4 py-3 font-semibold">الفريق</th>
              <th className="px-4 py-3 font-semibold">المدير المباشر</th>
              <th className="px-4 py-3 font-semibold">المسمى الوظيفي</th>
              <th className="px-4 py-3 font-semibold">إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-ink-3">
                  لا يوجد موظفون مطابقون للبحث.
                </td>
              </tr>
            ) : (
              rows.map((u) => (
                <Fragment key={u.id}>
                  <tr className="border-b border-line last:border-0 align-top">
                    <td className="px-4 py-3">
                      <div className="font-medium text-ink">{u.fullName}</div>
                      <div className="text-xs text-ink-3">{u.email}</div>
                      {!u.isActive && (
                        <Badge tone="muted">
                          <span className="text-[10px]">موقوف</span>
                        </Badge>
                      )}
                    </td>
                    <td className="px-4 py-3 text-ink-2">
                      {u.departmentId ? deptName.get(u.departmentId) ?? '—' : '—'}
                    </td>
                    <td className="px-4 py-3 text-ink-2">{u.teamId ? teamName.get(u.teamId) ?? '—' : '—'}</td>
                    <td className="px-4 py-3 text-ink-2">{u.managerId ? userName.get(u.managerId) ?? '—' : '—'}</td>
                    <td className="px-4 py-3 text-ink-2">{u.jobRoleId ? roleName.get(u.jobRoleId) ?? '—' : '—'}</td>
                    <td className="px-4 py-3">
                      {u.canEdit ? (
                        <div className="flex flex-col gap-1.5">
                          <div className="flex flex-wrap gap-2">
                            {canEditBasic && (
                              <Button
                                variant="ghost"
                                onClick={() =>
                                  setEditing((e) =>
                                    e?.userId === u.id && e.mode === 'basic' ? null : { userId: u.id, mode: 'basic' },
                                  )
                                }
                              >
                                تعديل الاسم
                              </Button>
                            )}
                            {canEditOrg && (
                              <Button
                                variant="ghost"
                                onClick={() =>
                                  setEditing((e) =>
                                    e?.userId === u.id && e.mode === 'org' ? null : { userId: u.id, mode: 'org' },
                                  )
                                }
                              >
                                تعديل التنظيم الوظيفي
                              </Button>
                            )}
                          </div>
                          {/* الأدمن يعدّل الاسم/التنظيم من هنا؛ الأدوار/كلمة المرور/التفعيل والتعطيل تتم من شاشة إدارة المستخدمين. */}
                          {u.isSensitive && isAdmin && (
                            <span className="text-[10px] text-ink-3">
                              الأدوار وكلمات المرور والتفعيل/التعطيل تتم من شاشة إدارة المستخدمين الرئيسية.
                            </span>
                          )}
                        </div>
                      ) : (
                        <Badge tone="muted">
                          <span className="text-[10px]">حساب محمي — غير قابل للتعديل</span>
                        </Badge>
                      )}
                    </td>
                  </tr>
                  {editing?.userId === u.id && editing.mode === 'basic' && (
                    <tr className="bg-offwhite">
                      <td colSpan={6} className="px-4 py-4">
                        <BasicEditor user={u} onClose={() => setEditing(null)} />
                      </td>
                    </tr>
                  )}
                  {editing?.userId === u.id && editing.mode === 'org' && (
                    <tr className="bg-offwhite">
                      <td colSpan={6} className="px-4 py-4">
                        <OrgEditor
                          user={u}
                          departments={departments.data ?? []}
                          teams={teams.data ?? []}
                          managers={(managersQuery.data ?? []).filter((m) => m.id !== u.id)}
                          onClose={() => setEditing(null)}
                        />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))
            )}
          </tbody>
        </table>
      </Card>
    </div>
  );
}

// ===== تعديل الاسم الكامل فقط =====
function BasicEditor({ user, onClose }: { user: HrDirectoryUserDto; onClose: () => void }) {
  const [fullName, setFullName] = useState(user.fullName);
  const mutation = useUpdateUserBasic();

  async function save() {
    try {
      await mutation.mutateAsync({ userId: user.id, req: { fullName: fullName.trim() } });
      onClose();
    } catch {
      /* الرسالة تُعرض أسفله */
    }
  }

  const dirty = fullName.trim() !== user.fullName && fullName.trim() !== '';

  return (
    <div className="max-w-md space-y-3">
      <h3 className="text-sm font-bold text-navy">تعديل الاسم الكامل</h3>
      <Field label="الاسم الكامل">
        <Input value={fullName} onChange={(e) => setFullName(e.target.value)} />
      </Field>
      {mutation.isError && <Alert tone="alert">{apiErrorMessage(mutation.error)}</Alert>}
      <div className="flex gap-2">
        <Button onClick={save} disabled={!dirty || mutation.isPending}>
          {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onClose}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

// ===== تعديل التنظيم الوظيفي (الإدارة/الفريق/المدير المباشر) =====
function OrgEditor({
  user,
  departments,
  teams,
  managers,
  onClose,
}: {
  user: HrDirectoryUserDto;
  departments: DepartmentDto[];
  teams: TeamDto[];
  managers: HrDirectoryUserDto[];
  onClose: () => void;
}) {
  const [departmentId, setDepartmentId] = useState(user.departmentId ?? '');
  const [teamId, setTeamId] = useState(user.teamId ?? '');
  const [managerId, setManagerId] = useState(user.managerId ?? '');
  const mutation = useUpdateUserOrgAssignment();

  const teamsForDept = useMemo(
    () => (departmentId ? teams.filter((t) => t.departmentId === departmentId) : teams),
    [teams, departmentId],
  );

  async function save() {
    try {
      await mutation.mutateAsync({
        userId: user.id,
        req: {
          departmentId: departmentId || null,
          teamId: teamId || null,
          managerId: managerId || null,
        },
      });
      onClose();
    } catch {
      /* الرسالة تُعرض أسفله */
    }
  }

  const changed =
    (departmentId || null) !== (user.departmentId ?? null) ||
    (teamId || null) !== (user.teamId ?? null) ||
    (managerId || null) !== (user.managerId ?? null);

  return (
    <div className="max-w-2xl space-y-3">
      <h3 className="text-sm font-bold text-navy">تعديل التنظيم الوظيفي — {user.fullName}</h3>
      <Alert tone="gold">
        تنبيه: تغيير الإدارة أو الفريق أو المدير المباشر قد يؤثّر على <span className="font-bold">نطاق الرؤية</span>{' '}
        لهذا الموظف ومن يتابعه، وعلى مسار اعتماد تقاريره المستقبلية. التقارير المُسلَّمة سابقًا لا تتغيّر.
      </Alert>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="الإدارة">
          <Select
            value={departmentId}
            onChange={(e) => {
              setDepartmentId(e.target.value);
              setTeamId('');
            }}
          >
            <option value="">بدون إدارة</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الفريق">
          <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
            <option value="">بدون فريق</option>
            {teamsForDept.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="المدير المباشر">
          <Select value={managerId} onChange={(e) => setManagerId(e.target.value)}>
            <option value="">بدون مدير</option>
            {managers.map((m) => (
              <option key={m.id} value={m.id}>
                {m.fullName}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      {mutation.isError && <Alert tone="alert">{apiErrorMessage(mutation.error)}</Alert>}
      <div className="flex gap-2">
        <Button onClick={save} disabled={!changed || mutation.isPending}>
          {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ التغيير'}
        </Button>
        <Button variant="ghost" onClick={onClose}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

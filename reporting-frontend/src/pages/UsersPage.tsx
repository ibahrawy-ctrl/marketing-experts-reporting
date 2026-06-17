// المستخدمون والأدوار — دليل المستخدمين مع أدوارهم وصلاحياتهم ونطاق رؤيتهم، مع إضافة/تعديل/حذف وإدارة الفرق للأدمن.
import { Fragment, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useDirectoryUsers,
  useTeams,
  useDepartments,
  useRoleMatrix,
  useUpdateUserRoles,
  useCreateUser,
  useUpdateUser,
  useDeleteUser,
  useAddTeamMember,
  useRemoveTeamMember,
  useCreateTeam,
  useUpdateTeam,
  useDeleteTeam,
} from '../lib/useDirectory';
import { Card, Badge, Select, Input, StatCard, Button, Alert, Field } from '../components/ui';
import { LoadingState, QueryError, TableSkeleton } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { ManagementNotesPanel } from '../components/ManagementNotesPanel';
import { roleLabel } from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import { useAuth } from '../lib/auth';
import type { Role, RoleAccessDto, DirectoryUserDto, DepartmentDto, TeamDto } from '../types/api';

const roleTone: Partial<Record<Role, 'navy' | 'orange' | 'success' | 'gold' | 'muted'>> = {
  Admin: 'orange',
  CEO: 'navy',
  GeneralManager: 'navy',
  Manager: 'success',
  TeamLeader: 'gold',
  Employee: 'muted',
  CeoSupport: 'navy',
  HR: 'success',
  Viewer: 'muted',
};

const scopeTone: Record<string, 'orange' | 'navy' | 'success' | 'gold' | 'muted'> = {
  governance: 'orange',
  company: 'navy',
  department: 'success',
  team: 'gold',
  own: 'muted',
};

export default function UsersPage() {
  const { user, hasAnyRole, canApprove, canViewGovernance } = useAuth();
  const isAdmin = hasAnyRole('Admin');
  // من يملك صلاحية إدارية (اعتماد أو حوكمة) يستطيع كتابة ملاحظات إدارية على ملف الموظف.
  const canManageNotes = canApprove || canViewGovernance;
  const showActions = isAdmin || canManageNotes;
  const users = useDirectoryUsers(true);
  const teams = useTeams();
  const departments = useDepartments();
  const matrix = useRoleMatrix();
  const [q, setQ] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [deptFilter, setDeptFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editMode, setEditMode] = useState<'roles' | 'data' | 'notes'>('roles');
  const [showAdd, setShowAdd] = useState(false);

  if (users.isLoading || teams.isLoading) return <LoadingState label="يتم تحميل المستخدمين…" />;
  if (users.isError || teams.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
        }}
        description="حدث خطأ أثناء جلب بيانات المستخدمين. أعد المحاولة."
      />
    );

  const teamName = (id: string | null) => (teams.data ?? []).find((t) => t.id === id)?.nameAr ?? '—';
  const deptName = (id: string | null) => (departments.data ?? []).find((d) => d.id === id)?.nameAr ?? '—';
  const matrixByRole = new Map((matrix.data ?? []).map((m) => [m.role, m]));
  const allUsers = users.data ?? [];

  let list = allUsers;
  if (q.trim()) list = list.filter((u) => u.fullName.includes(q.trim()) || u.email.includes(q.trim()));
  if (roleFilter) list = list.filter((u) => u.roles.includes(roleFilter as Role));
  if (deptFilter) list = list.filter((u) => u.departmentId === deptFilter);
  if (activeFilter) list = list.filter((u) => (activeFilter === 'active' ? u.isActive : !u.isActive));

  const total = allUsers.length;
  const activeCount = allUsers.filter((u) => u.isActive).length;
  const roleCount = (r: Role) => allUsers.filter((u) => u.roles.includes(r)).length;

  const openEditor = (id: string, mode: 'roles' | 'data' | 'notes') => {
    if (editingId === id && editMode === mode) {
      setEditingId(null);
    } else {
      setEditingId(id);
      setEditMode(mode);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">المستخدمون والأدوار</h1>
        <p className="mt-1 text-sm text-ink-2">
          دليل المستخدمين وأدوارهم وصلاحياتهم ونطاق رؤيتهم — {isAdmin ? 'يمكنك إضافة وتعديل وحذف المستخدمين وتوزيع الصلاحيات.' : 'عرض فقط.'}
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي المستخدمين" value={total} />
        <StatCard label="نشِط" value={activeCount} />
        <StatCard label="مدراء" value={roleCount('Manager') + roleCount('GeneralManager')} />
        <StatCard label="قادة فرق" value={roleCount('TeamLeader')} />
      </div>

      {/* مرجع الصلاحيات ونطاق الرؤية لكل دور */}
      <Card>
        <SectionTitle
          title="الصلاحيات ونطاق الرؤية لكل دور"
          hint="مَن يرى ماذا وما الذي يستطيع فعله — مرجع موحّد"
        />
        {matrix.isLoading ? (
          <TableSkeleton rows={5} cols={4} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">الدور</th>
                  <th className="px-2 py-2 font-semibold">نطاق الرؤية</th>
                  <th className="px-2 py-2 font-semibold">الصلاحيات</th>
                  <th className="px-2 py-2 font-semibold">عدد المستخدمين</th>
                </tr>
              </thead>
              <tbody>
                {(matrix.data ?? []).map((m) => (
                  <tr key={m.role} className="border-b border-line last:border-0 align-top">
                    <td className="px-2 py-3">
                      <Badge tone={roleTone[m.role] ?? 'muted'}>{m.roleLabelAr}</Badge>
                    </td>
                    <td className="px-2 py-3">
                      <Badge tone={scopeTone[m.scopeType] ?? 'muted'}>{m.scopeDescriptionAr}</Badge>
                    </td>
                    <td className="px-2 py-3">
                      <div className="flex flex-wrap gap-1">
                        {m.permissionLabelsAr.map((p) => (
                          <span key={p} className="rounded-md bg-navy-50 px-2 py-0.5 text-xs text-navy">{p}</span>
                        ))}
                      </div>
                    </td>
                    <td className="px-2 py-3 text-ink-2">{roleCount(m.role)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* إدارة الفرق — إنشاء/تعديل/حذف فرق + إضافة/حذف أعضاء (للأدمن) */}
      {isAdmin && <TeamManager users={allUsers} departments={departments.data ?? []} />}

      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <SectionTitle title={`المستخدمون (${list.length})`} />
          {isAdmin && (
            <Button onClick={() => setShowAdd((s) => !s)}>
              {showAdd ? 'إغلاق' : '+ إضافة مستخدم'}
            </Button>
          )}
        </div>

        {isAdmin && showAdd && (
          <div className="mb-4 rounded-lg border border-line bg-offwhite p-4">
            <AddUserForm
              teams={teams.data ?? []}
              departments={departments.data ?? []}
              users={allUsers}
              onDone={() => setShowAdd(false)}
            />
          </div>
        )}

        <div className="mb-3 flex flex-wrap gap-3">
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="بحث بالاسم أو البريد…" className="max-w-xs" />
          <Select value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)} className="max-w-[180px]">
            <option value="">كل الأدوار</option>
            {(Object.keys(roleLabel) as Role[]).map((r) => (
              <option key={r} value={r}>{roleLabel[r]}</option>
            ))}
          </Select>
          <Select value={deptFilter} onChange={(e) => setDeptFilter(e.target.value)} className="max-w-[180px]">
            <option value="">كل الإدارات</option>
            {(departments.data ?? []).map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
          <Select value={activeFilter} onChange={(e) => setActiveFilter(e.target.value)} className="max-w-[140px]">
            <option value="">الكل</option>
            <option value="active">نشِط</option>
            <option value="inactive">غير نشِط</option>
          </Select>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الاسم</th>
                <th className="px-2 py-2 font-semibold">البريد</th>
                <th className="px-2 py-2 font-semibold">الأدوار</th>
                <th className="px-2 py-2 font-semibold">نطاق الرؤية</th>
                <th className="px-2 py-2 font-semibold">الإدارة</th>
                <th className="px-2 py-2 font-semibold">الفريق</th>
                <th className="px-2 py-2 font-semibold">الحالة</th>
                {showActions && <th className="px-2 py-2 font-semibold">إجراءات</th>}
              </tr>
            </thead>
            <tbody>
              {list.map((u) => {
                const primary = primaryRole(u.roles);
                const scope = primary ? matrixByRole.get(primary) : undefined;
                return (
                  <Fragment key={u.id}>
                    <tr className="border-b border-line last:border-0">
                      <td className="px-2 py-2 font-medium">
                        <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${u.id}`}>
                          {u.fullName}
                        </Link>
                      </td>
                      <td className="px-2 py-2 text-ink-2">{u.email}</td>
                      <td className="px-2 py-2">
                        <div className="flex flex-wrap gap-1">
                          {u.roles.map((r) => (
                            <Badge key={r} tone={roleTone[r] ?? 'muted'}>{roleLabel[r]}</Badge>
                          ))}
                        </div>
                      </td>
                      <td className="px-2 py-2 text-xs text-ink-2">{scope?.scopeDescriptionAr ?? '—'}</td>
                      <td className="px-2 py-2 text-ink-2">{deptName(u.departmentId)}</td>
                      <td className="px-2 py-2 text-ink-2">{teamName(u.teamId)}</td>
                      <td className="px-2 py-2">
                        <Badge tone={u.isActive ? 'success' : 'muted'}>{u.isActive ? 'نشِط' : 'موقوف'}</Badge>
                      </td>
                      {showActions && (
                        <td className="px-2 py-2">
                          <div className="flex flex-wrap gap-2 text-sm font-semibold">
                            {isAdmin && (
                              <>
                                <button
                                  onClick={() => openEditor(u.id, 'data')}
                                  className="text-navy hover:underline"
                                >
                                  تعديل البيانات
                                </button>
                                <button
                                  onClick={() => openEditor(u.id, 'roles')}
                                  className="text-orange-600 hover:underline"
                                >
                                  الصلاحيات
                                </button>
                              </>
                            )}
                            {canManageNotes && (
                              <button
                                onClick={() => openEditor(u.id, 'notes')}
                                className="text-navy hover:underline"
                              >
                                ملاحظات
                              </button>
                            )}
                            {isAdmin && <DeleteUserButton target={u} isSelf={u.id === user?.userId} />}
                          </div>
                        </td>
                      )}
                    </tr>
                    {showActions && editingId === u.id && (
                      <tr className="border-b border-line">
                        <td colSpan={8} className="bg-offwhite px-3 py-4">
                          {editMode === 'notes' ? (
                            <ManagementNotesPanel
                              entityType="User"
                              entityId={u.id}
                              title={`الملاحظات الإدارية على ${u.fullName}`}
                            />
                          ) : editMode === 'roles' && isAdmin ? (
                            <RoleEditor
                              target={u}
                              matrix={matrix.data ?? []}
                              isSelf={u.id === user?.userId}
                              onDone={() => setEditingId(null)}
                            />
                          ) : isAdmin ? (
                            <UserDataEditor
                              target={u}
                              teams={teams.data ?? []}
                              departments={departments.data ?? []}
                              users={allUsers}
                              isSelf={u.id === user?.userId}
                              onDone={() => setEditingId(null)}
                            />
                          ) : null}
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
              {list.length === 0 && (
                <tr><td colSpan={showActions ? 8 : 7} className="py-10 text-center">
                  <p className="text-sm font-medium text-ink-2">لا يوجد مستخدمون مطابقون.</p>
                  <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">لا يطابق أحد البحث أو الفلتر الحالي. جرّب تعديل كلمة البحث أو إظهار غير النشطين{isAdmin ? '، أو أضِف مستخدمًا جديدًا من زر «إضافة مستخدم».' : '.'}</p>
                </td></tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}

// أعلى دور صلاحيةً (لعرض نطاق الرؤية الفعّال).
const ROLE_ORDER: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'HR', 'Employee', 'Viewer'];
function primaryRole(roles: Role[]): Role | null {
  return ROLE_ORDER.find((r) => roles.includes(r)) ?? null;
}

const ALL_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'CeoSupport', 'HR', 'Employee', 'Viewer'];

// ── إضافة مستخدم جديد ─────────────────────────────────────────────────
function AddUserForm({
  teams,
  departments,
  users,
  onDone,
}: {
  teams: TeamDto[];
  departments: DepartmentDto[];
  users: DirectoryUserDto[];
  onDone: () => void;
}) {
  const create = useCreateUser();
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [password, setPassword] = useState('');
  const [roles, setRoles] = useState<Role[]>(['Employee']);
  const [teamId, setTeamId] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [managerId, setManagerId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const toggle = (r: Role) =>
    setRoles((prev) => (prev.includes(r) ? prev.filter((x) => x !== r) : [...prev, r]));

  const submit = () => {
    setError(null);
    create.mutate(
      {
        email: email.trim(),
        fullName: fullName.trim(),
        password,
        roles,
        teamId: teamId || null,
        departmentId: departmentId || null,
        managerId: managerId || null,
      },
      {
        onSuccess: onDone,
        onError: (e) => setError(apiErrorMessage(e)),
      },
    );
  };

  return (
    <div className="space-y-4">
      <p className="text-sm font-semibold text-navy">إضافة مستخدم جديد</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="الاسم الكامل">
          <Input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="مثال: محمد أحمد" />
        </Field>
        <Field label="البريد الإلكتروني">
          <Input value={email} onChange={(e) => setEmail(e.target.value)} type="email" placeholder="name@marketingexperts.local" />
        </Field>
        <Field label="كلمة المرور" help="٨ أحرف على الأقل، تشمل حرفًا كبيرًا وصغيرًا ورقمًا">
          <Input value={password} onChange={(e) => setPassword(e.target.value)} type="password" />
        </Field>
        <Field label="المدير المباشر (اختياري)">
          <Select value={managerId} onChange={(e) => setManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {users.map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
        <Field label="الفريق (اختياري)">
          <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
            <option value="">— بدون —</option>
            {teams.map((t) => (
              <option key={t.id} value={t.id}>{t.nameAr}</option>
            ))}
          </Select>
        </Field>
        <Field label="الإدارة (اختياري)" help="تُستنتج تلقائيًا من الفريق إن تُرك فارغًا">
          <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">— تلقائي —</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
        </Field>
      </div>

      <div>
        <p className="mb-1 text-sm font-medium text-ink">الأدوار</p>
        <div className="flex flex-wrap gap-2">
          {ALL_ROLES.map((r) => {
            const on = roles.includes(r);
            return (
              <button
                key={r}
                type="button"
                onClick={() => toggle(r)}
                className={`rounded-lg border px-3 py-1.5 text-sm transition ${
                  on ? 'border-navy bg-navy text-white' : 'border-line bg-white text-ink hover:border-navy'
                }`}
              >
                {roleLabel[r]}
              </button>
            );
          })}
        </div>
      </div>

      {error && <Alert tone="alert">{error}</Alert>}

      <div className="flex gap-2">
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending ? 'جارٍ الإنشاء…' : 'إنشاء المستخدم'}
        </Button>
        <Button variant="ghost" onClick={onDone}>إلغاء</Button>
      </div>
    </div>
  );
}

// ── تعديل بيانات مستخدم ───────────────────────────────────────────────
function UserDataEditor({
  target,
  teams,
  departments,
  users,
  isSelf,
  onDone,
}: {
  target: DirectoryUserDto;
  teams: TeamDto[];
  departments: DepartmentDto[];
  users: DirectoryUserDto[];
  isSelf: boolean;
  onDone: () => void;
}) {
  const update = useUpdateUser();
  const [fullName, setFullName] = useState(target.fullName);
  const [isActive, setIsActive] = useState(target.isActive);
  const [teamId, setTeamId] = useState(target.teamId ?? '');
  const [departmentId, setDepartmentId] = useState(target.departmentId ?? '');
  const [managerId, setManagerId] = useState(target.managerId ?? '');
  const [error, setError] = useState<string | null>(null);

  const save = () => {
    setError(null);
    update.mutate(
      {
        userId: target.id,
        req: {
          fullName: fullName.trim(),
          isActive,
          teamId: teamId || null,
          departmentId: departmentId || null,
          managerId: managerId || null,
        },
      },
      {
        onSuccess: onDone,
        onError: (e) => setError(apiErrorMessage(e)),
      },
    );
  };

  return (
    <div className="space-y-4">
      <p className="text-sm font-semibold text-navy">تعديل بيانات: {target.fullName}</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="الاسم الكامل">
          <Input value={fullName} onChange={(e) => setFullName(e.target.value)} />
        </Field>
        <Field label="المدير المباشر">
          <Select value={managerId} onChange={(e) => setManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {users.filter((u) => u.id !== target.id).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
        <Field label="الفريق">
          <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
            <option value="">— بدون —</option>
            {teams.map((t) => (
              <option key={t.id} value={t.id}>{t.nameAr}</option>
            ))}
          </Select>
        </Field>
        <Field label="الإدارة" help="تُستنتج من الفريق إن تُرك فارغًا">
          <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">— تلقائي —</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
        </Field>
      </div>

      <label className="flex items-center gap-2 text-sm text-ink">
        <input
          type="checkbox"
          checked={isActive}
          onChange={(e) => setIsActive(e.target.checked)}
          className="h-4 w-4 rounded border-line"
        />
        حساب نشِط
      </label>

      {isSelf && !isActive && (
        <Alert tone="navy">لا يمكنك إيقاف حسابك الخاص.</Alert>
      )}
      {error && <Alert tone="alert">{error}</Alert>}

      <div className="flex gap-2">
        <Button onClick={save} disabled={update.isPending}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ البيانات'}
        </Button>
        <Button variant="ghost" onClick={onDone}>إلغاء</Button>
      </div>
    </div>
  );
}

// ── زر حذف مستخدم ─────────────────────────────────────────────────────
function DeleteUserButton({ target, isSelf }: { target: DirectoryUserDto; isSelf: boolean }) {
  const del = useDeleteUser();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (isSelf) return null;

  if (!confirming) {
    return (
      <button onClick={() => setConfirming(true)} className="text-alert hover:underline">
        حذف
      </button>
    );
  }

  return (
    <span className="inline-flex items-center gap-2">
      <button
        onClick={() =>
          del.mutate(target.id, {
            onSuccess: () => setConfirming(false),
            onError: (e) => setError(apiErrorMessage(e)),
          })
        }
        disabled={del.isPending}
        className="text-alert hover:underline"
      >
        {del.isPending ? 'جارٍ…' : 'تأكيد الحذف'}
      </button>
      <button onClick={() => { setConfirming(false); setError(null); }} className="text-ink-2 hover:underline">
        تراجع
      </button>
      {error && <span className="text-xs text-alert">{error}</span>}
    </span>
  );
}

// ── إدارة أعضاء الفرق ─────────────────────────────────────────────────
function TeamManager({ users, departments }: { users: DirectoryUserDto[]; departments: DepartmentDto[] }) {
  const teams = useTeams();
  const addMember = useAddTeamMember();
  const removeMember = useRemoveTeamMember();
  const deleteTeam = useDeleteTeam();
  const [teamId, setTeamId] = useState('');
  const [addUserId, setAddUserId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [editTeam, setEditTeam] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const team = (teams.data ?? []).find((t) => t.id === teamId) ?? null;
  const members = teamId ? users.filter((u) => u.teamId === teamId) : [];
  const candidates = teamId ? users.filter((u) => u.teamId !== teamId && u.isActive) : [];
  const deptName = (id: string) => departments.find((d) => d.id === id)?.nameAr ?? '—';
  const userName = (id: string | null) => users.find((u) => u.id === id)?.fullName ?? null;

  const resetTeamPanels = () => { setEditTeam(false); setConfirmDelete(false); setAddUserId(''); setError(null); };

  const add = () => {
    if (!teamId || !addUserId) return;
    setError(null);
    addMember.mutate(
      { teamId, userId: addUserId },
      { onSuccess: () => setAddUserId(''), onError: (e) => setError(apiErrorMessage(e)) },
    );
  };

  const remove = (userId: string) => {
    setError(null);
    removeMember.mutate({ teamId, userId }, { onError: (e) => setError(apiErrorMessage(e)) });
  };

  const doDelete = () => {
    setError(null);
    deleteTeam.mutate(teamId, {
      onSuccess: () => { setTeamId(''); resetTeamPanels(); },
      onError: (e) => setError(apiErrorMessage(e)),
    });
  };

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <SectionTitle title="إدارة الفرق" hint="إنشاء فريق أو تعديل بياناته أو حذفه، وإدارة أعضائه" />
        <Button variant="ghost" onClick={() => { setShowCreate((v) => !v); setError(null); }}>
          {showCreate ? 'إغلاق' : '+ إضافة فريق'}
        </Button>
      </div>

      {showCreate && (
        <div className="mb-4">
          <CreateTeamForm
            departments={departments}
            users={users}
            onDone={(newId) => { setShowCreate(false); setTeamId(newId); resetTeamPanels(); }}
          />
        </div>
      )}

      <div className="mb-4 max-w-sm">
        <Field label="اختر الفريق">
          <Select value={teamId} onChange={(e) => { setTeamId(e.target.value); resetTeamPanels(); }}>
            <option value="">— اختر فريقًا —</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>{t.nameAr}{t.isActive ? '' : ' (معطّل)'}</option>
            ))}
          </Select>
        </Field>
      </div>

      {team && (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-line bg-offwhite px-3 py-2.5">
            <div className="text-sm">
              <p className="font-semibold text-navy">{team.nameAr}{team.nameEn ? ` · ${team.nameEn}` : ''}</p>
              <p className="text-ink-2">
                الإدارة: {deptName(team.departmentId)}
                {' · '}القائد: {userName(team.teamLeaderId) ?? 'بدون'}
                {team.isActive ? '' : ' · معطّل'}
              </p>
            </div>
            <div className="flex gap-2">
              <Button variant="ghost" onClick={() => { setEditTeam((v) => !v); setConfirmDelete(false); setError(null); }}>
                {editTeam ? 'إغلاق التعديل' : 'تعديل بيانات الفريق'}
              </Button>
              <Button variant="danger" onClick={() => { setConfirmDelete((v) => !v); setEditTeam(false); setError(null); }}>
                حذف الفريق
              </Button>
            </div>
          </div>

          {confirmDelete && (
            <Alert tone="alert">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <span>سيُحذف الفريق نهائيًا وتُفرّغ عضوية أعضائه. متأكد؟</span>
                <div className="flex gap-2">
                  <Button variant="danger" onClick={doDelete} disabled={deleteTeam.isPending}>
                    {deleteTeam.isPending ? 'جارٍ…' : 'تأكيد الحذف'}
                  </Button>
                  <Button variant="ghost" onClick={() => setConfirmDelete(false)}>إلغاء</Button>
                </div>
              </div>
            </Alert>
          )}

          {editTeam && (
            <TeamEditor
              team={team}
              departments={departments}
              users={users}
              onDone={() => setEditTeam(false)}
              setErr={setError}
            />
          )}

          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-[240px] flex-1">
              <Field label="إضافة عضو">
                <Select value={addUserId} onChange={(e) => setAddUserId(e.target.value)}>
                  <option value="">— اختر مستخدمًا —</option>
                  {candidates.map((u) => (
                    <option key={u.id} value={u.id}>{u.fullName}</option>
                  ))}
                </Select>
              </Field>
            </div>
            <Button onClick={add} disabled={!addUserId || addMember.isPending}>
              {addMember.isPending ? 'جارٍ…' : 'إضافة للفريق'}
            </Button>
          </div>

          {error && <Alert tone="alert">{error}</Alert>}

          <div>
            <p className="mb-2 text-sm font-semibold text-navy">الأعضاء الحاليون ({members.length})</p>
            {members.length === 0 ? (
              <p className="text-sm text-ink-2">لا يوجد أعضاء في هذا الفريق بعد. أضِف عضوًا من قائمة الاختيار أدناه لربطه بالفريق.</p>
            ) : (
              <ul className="divide-y divide-line rounded-lg border border-line">
                {members.map((u) => (
                  <li key={u.id} className="flex items-center justify-between px-3 py-2 text-sm">
                    <span className="text-navy">{u.fullName}</span>
                    <button
                      onClick={() => remove(u.id)}
                      disabled={removeMember.isPending}
                      className="text-sm font-semibold text-alert hover:underline disabled:opacity-50"
                    >
                      إزالة
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </Card>
  );
}

// ── إنشاء فريق جديد ───────────────────────────────────────────────────
function CreateTeamForm({
  departments,
  users,
  onDone,
}: {
  departments: DepartmentDto[];
  users: DirectoryUserDto[];
  onDone: (newId: string) => void;
}) {
  const create = useCreateTeam();
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [teamLeaderId, setTeamLeaderId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    setError(null);
    if (!nameAr.trim()) { setError('اسم الفريق مطلوب.'); return; }
    if (!departmentId) { setError('يجب اختيار الإدارة.'); return; }
    create.mutate(
      { nameAr: nameAr.trim(), nameEn: nameEn.trim() || null, departmentId, teamLeaderId: teamLeaderId || null },
      { onSuccess: (t) => onDone(t.id), onError: (e) => setError(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <p className="mb-3 text-sm font-semibold text-navy">فريق جديد</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="اسم الفريق (عربي)">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} placeholder="مثال: فريق المبيعات" />
        </Field>
        <Field label="الاسم (إنجليزي) — اختياري">
          <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} placeholder="Sales Team" />
        </Field>
        <Field label="الإدارة">
          <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">— اختر الإدارة —</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
        </Field>
        <Field label="قائد الفريق — اختياري">
          <Select value={teamLeaderId} onChange={(e) => setTeamLeaderId(e.target.value)}>
            <option value="">— بدون قائد —</option>
            {users.filter((u) => u.isActive).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
      </div>
      {error && <div className="mt-3"><Alert tone="alert">{error}</Alert></div>}
      <div className="mt-3">
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending ? 'جارٍ…' : 'إنشاء الفريق'}
        </Button>
      </div>
    </div>
  );
}

// ── تعديل بيانات فريق ─────────────────────────────────────────────────
function TeamEditor({
  team,
  departments,
  users,
  onDone,
  setErr,
}: {
  team: TeamDto;
  departments: DepartmentDto[];
  users: DirectoryUserDto[];
  onDone: () => void;
  setErr: (s: string | null) => void;
}) {
  const update = useUpdateTeam();
  const [nameAr, setNameAr] = useState(team.nameAr);
  const [nameEn, setNameEn] = useState(team.nameEn ?? '');
  const [departmentId, setDepartmentId] = useState(team.departmentId);
  const [teamLeaderId, setTeamLeaderId] = useState(team.teamLeaderId ?? '');
  const [isActive, setIsActive] = useState(team.isActive);

  const save = () => {
    setErr(null);
    if (!nameAr.trim()) { setErr('اسم الفريق مطلوب.'); return; }
    update.mutate(
      { teamId: team.id, req: { nameAr: nameAr.trim(), nameEn: nameEn.trim() || null, departmentId, teamLeaderId: teamLeaderId || null, isActive } },
      { onSuccess: onDone, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <p className="mb-3 text-sm font-semibold text-navy">تعديل بيانات الفريق</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="اسم الفريق (عربي)">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field label="الاسم (إنجليزي) — اختياري">
          <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
        </Field>
        <Field label="الإدارة">
          <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
        </Field>
        <Field label="قائد الفريق — اختياري">
          <Select value={teamLeaderId} onChange={(e) => setTeamLeaderId(e.target.value)}>
            <option value="">— بدون قائد —</option>
            {users.filter((u) => u.isActive).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
      </div>
      <label className="mt-3 flex items-center gap-2 text-sm text-navy">
        <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        فريق نشط
      </label>
      <div className="mt-3">
        <Button onClick={save} disabled={update.isPending}>
          {update.isPending ? 'جارٍ…' : 'حفظ التعديلات'}
        </Button>
      </div>
    </div>
  );
}

// ── تعديل أدوار مستخدم ────────────────────────────────────────────────
function RoleEditor({
  target,
  matrix,
  isSelf,
  onDone,
}: {
  target: DirectoryUserDto;
  matrix: RoleAccessDto[];
  isSelf: boolean;
  onDone: () => void;
}) {
  const update = useUpdateUserRoles();
  const [selected, setSelected] = useState<Role[]>(target.roles);
  const [error, setError] = useState<string | null>(null);

  const toggle = (r: Role) =>
    setSelected((prev) => (prev.includes(r) ? prev.filter((x) => x !== r) : [...prev, r]));

  // معاينة الصلاحيات المجمّعة من الأدوار المختارة.
  const previewPerms = new Set<string>();
  for (const r of selected) {
    matrix.find((m) => m.role === r)?.permissionLabelsAr.forEach((p) => previewPerms.add(p));
  }
  const primary = primaryRole(selected);
  const previewScope = primary ? matrix.find((m) => m.role === primary)?.scopeDescriptionAr : null;

  const save = () => {
    setError(null);
    update.mutate(
      { userId: target.id, roles: selected },
      {
        onSuccess: onDone,
        onError: (e) => setError(apiErrorMessage(e)),
      },
    );
  };

  return (
    <div className="space-y-3">
      <p className="text-sm font-semibold text-navy">تعديل أدوار: {target.fullName}</p>
      <div className="flex flex-wrap gap-2">
        {ALL_ROLES.map((r) => {
          const on = selected.includes(r);
          return (
            <button
              key={r}
              type="button"
              onClick={() => toggle(r)}
              className={`rounded-lg border px-3 py-1.5 text-sm transition ${
                on ? 'border-navy bg-navy text-white' : 'border-line bg-white text-ink hover:border-navy'
              }`}
            >
              {roleLabel[r]}
            </button>
          );
        })}
      </div>

      <div className="rounded-lg border border-line bg-white p-3 text-sm">
        <p className="mb-1 text-ink-2">
          نطاق الرؤية الفعّال: <span className="font-semibold text-navy">{previewScope ?? '—'}</span>
        </p>
        <div className="flex flex-wrap gap-1">
          {[...previewPerms].map((p) => (
            <span key={p} className="rounded-md bg-navy-50 px-2 py-0.5 text-xs text-navy">{p}</span>
          ))}
          {previewPerms.size === 0 && <span className="text-xs text-ink-2">لا صلاحيات مختارة.</span>}
        </div>
      </div>

      {isSelf && (
        <Alert tone="navy">أنت تعدّل أدوار حسابك — لا يمكن إزالة دور «مدير النظام» عن نفسك.</Alert>
      )}
      {error && <Alert tone="alert">{error}</Alert>}

      <div className="flex gap-2">
        <Button onClick={save} disabled={update.isPending}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ الصلاحيات'}
        </Button>
        <Button variant="ghost" onClick={onDone}>إلغاء</Button>
      </div>
    </div>
  );
}

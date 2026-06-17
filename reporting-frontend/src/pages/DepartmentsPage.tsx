// إدارة الإدارات — إنشاء/تعديل/تعطيل إدارة، ربط مدير الإدارة، وعرض فرقها.
import { Fragment, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  useDepartments,
  useTeams,
  useDirectoryUsers,
  useCreateDepartment,
  useUpdateDepartment,
  useDeleteDepartment,
} from '../lib/useDirectory';
import { Card, Badge, Select, Input, StatCard, Button, Alert, Field } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { apiErrorMessage } from '../lib/api';
import type { DepartmentDto, DirectoryUserDto, TeamDto } from '../types/api';

export function DepartmentsPage() {
  const departments = useDepartments();
  const teams = useTeams();
  const users = useDirectoryUsers(true);
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const del = useDeleteDepartment();

  if (departments.isLoading || teams.isLoading || users.isLoading)
    return <LoadingState label="يتم تحميل الإدارات…" />;
  if (departments.isError || teams.isError || users.isError)
    return (
      <QueryError
        onRetry={() => {
          departments.refetch();
          teams.refetch();
          users.refetch();
        }}
        description="حدث خطأ أثناء جلب بيانات الإدارات. أعد المحاولة."
      />
    );

  const deptList = departments.data ?? [];
  const teamList = teams.data ?? [];
  const userList = users.data ?? [];
  const userById = new Map(userList.map((u) => [u.id, u]));
  const teamsByDept = (deptId: string) => teamList.filter((t) => t.departmentId === deptId);
  const membersOf = (deptId: string) => userList.filter((u) => u.departmentId === deptId).length;

  const activeCount = deptList.filter((d) => d.isActive).length;

  const doDelete = (id: string) => {
    setError(null);
    del.mutate(id, {
      onSuccess: () => setConfirmDelete(null),
      onError: (e) => setError(apiErrorMessage(e)),
    });
  };

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي الإدارات" value={deptList.length} />
        <StatCard label="إدارات نشطة" value={activeCount} />
        <StatCard label="الفرق" value={teamList.length} />
        <StatCard label="الموظفون المرتبطون" value={userList.filter((u) => u.departmentId).length} />
      </div>

      <Card>
        <div className="flex items-center justify-between">
          <SectionTitle title="إدارة الإدارات" hint="إنشاء إدارة أو تعديل بياناتها أو تعطيلها، وربط مديرها وعرض فرقها" />
          <Button variant={adding ? 'ghost' : 'primary'} onClick={() => { setAdding((v) => !v); setEditingId(null); }}>
            {adding ? 'إلغاء' : '+ إضافة إدارة'}
          </Button>
        </div>

        {error && <div className="mb-3"><Alert tone="alert">{error}</Alert></div>}

        {adding && (
          <div className="mb-4">
            <CreateDepartmentForm users={userList} onDone={() => setAdding(false)} />
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الإدارة</th>
                <th className="px-2 py-2 font-semibold">الرمز</th>
                <th className="px-2 py-2 font-semibold">المدير</th>
                <th className="px-2 py-2 font-semibold">الفرق</th>
                <th className="px-2 py-2 font-semibold">الموظفون</th>
                <th className="px-2 py-2 font-semibold">الحالة</th>
                <th className="px-2 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {deptList.map((d) => {
                const dteams = teamsByDept(d.id);
                const manager = d.managerId ? userById.get(d.managerId) : undefined;
                return (
                  <Fragment key={d.id}>
                    <tr className="border-b border-line last:border-0">
                      <td className="px-2 py-2 font-medium text-navy">
                        {d.nameAr}
                        {d.nameEn && <span className="block text-xs text-ink-3">{d.nameEn}</span>}
                      </td>
                      <td className="px-2 py-2 text-ink-2">{d.code ?? '—'}</td>
                      <td className="px-2 py-2 text-ink-2">{manager?.fullName ?? '—'}</td>
                      <td className="px-2 py-2">{dteams.length}</td>
                      <td className="px-2 py-2">{membersOf(d.id)}</td>
                      <td className="px-2 py-2">
                        <Badge tone={d.isActive ? 'success' : 'muted'}>{d.isActive ? 'نشطة' : 'معطّلة'}</Badge>
                      </td>
                      <td className="px-2 py-2">
                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => { setEditingId(editingId === d.id ? null : d.id); setConfirmDelete(null); }}
                            className="text-sm font-semibold text-orange-600 hover:underline"
                          >
                            {editingId === d.id ? 'إخفاء' : 'تعديل'}
                          </button>
                          <button
                            onClick={() => { setConfirmDelete(confirmDelete === d.id ? null : d.id); setEditingId(null); }}
                            className="text-sm font-semibold text-red-600 hover:underline"
                          >
                            حذف
                          </button>
                        </div>
                      </td>
                    </tr>
                    {(editingId === d.id || confirmDelete === d.id || dteams.length > 0) && (
                      <tr>
                        <td colSpan={7} className="px-2 pb-3">
                          {dteams.length > 0 && editingId !== d.id && confirmDelete !== d.id && (
                            <TeamChips teams={dteams} />
                          )}
                          {editingId === d.id && (
                            <DepartmentEditor
                              dept={d}
                              users={userList}
                              onDone={() => setEditingId(null)}
                              setErr={setError}
                            />
                          )}
                          {confirmDelete === d.id && (
                            <div className="rounded-lg border border-red-200 bg-red-50 p-4">
                              {dteams.length > 0 ? (
                                <p className="text-sm text-red-700">
                                  لا يمكن حذف هذه الإدارة لأنها تحتوي على {dteams.length} فريق. انقل الفرق أو احذفها أولًا، أو عطّل الإدارة بدلًا من الحذف.
                                </p>
                              ) : (
                                <p className="text-sm text-red-700">
                                  سيُحذف هذا السجل نهائيًا وسيُفصل ارتباط الموظفين بالإدارة. هل أنت متأكد؟
                                </p>
                              )}
                              <div className="mt-3 flex items-center gap-2">
                                {dteams.length === 0 && (
                                  <Button variant="danger" onClick={() => doDelete(d.id)} disabled={del.isPending}>
                                    {del.isPending ? 'جارٍ…' : 'تأكيد الحذف'}
                                  </Button>
                                )}
                                <Button variant="ghost" onClick={() => setConfirmDelete(null)}>إلغاء</Button>
                              </div>
                            </div>
                          )}
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
              {deptList.length === 0 && (
                <tr><td colSpan={7} className="py-10 text-center">
                  <p className="text-sm font-medium text-ink-2">لا توجد إدارات ضمن نطاقك.</p>
                  <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">تُنشأ الإدارات وتُربط بمديريها وفِرقها من قِبل المسؤول. تواصل مع المسؤول لإضافة إدارة.</p>
                </td></tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}

function TeamChips({ teams }: { teams: TeamDto[] }) {
  return (
    <div className="rounded-lg border border-line bg-offwhite p-3">
      <p className="mb-2 text-xs font-semibold text-ink-2">فرق هذه الإدارة</p>
      <div className="flex flex-wrap gap-2">
        {teams.map((t) => (
          <Link key={t.id} to={`/app/teams/${t.id}`}>
            <Badge tone="navy">{t.nameAr}</Badge>
          </Link>
        ))}
      </div>
    </div>
  );
}

function CreateDepartmentForm({ users, onDone }: { users: DirectoryUserDto[]; onDone: () => void }) {
  const create = useCreateDepartment();
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [code, setCode] = useState('');
  const [managerId, setManagerId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    setError(null);
    if (!nameAr.trim()) { setError('اسم الإدارة مطلوب.'); return; }
    create.mutate(
      { nameAr: nameAr.trim(), nameEn: nameEn.trim() || null, code: code.trim() || null, managerId: managerId || null },
      { onSuccess: onDone, onError: (e) => setError(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <p className="mb-3 text-sm font-semibold text-navy">إدارة جديدة</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="اسم الإدارة (عربي)">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} placeholder="مثال: إدارة المبيعات" />
        </Field>
        <Field label="الاسم (إنجليزي) — اختياري">
          <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} placeholder="Sales Department" />
        </Field>
        <Field label="الرمز — اختياري">
          <Input value={code} onChange={(e) => setCode(e.target.value)} placeholder="SALES" />
        </Field>
        <Field label="مدير الإدارة — اختياري">
          <Select value={managerId} onChange={(e) => setManagerId(e.target.value)}>
            <option value="">— بدون مدير —</option>
            {users.filter((u) => u.isActive).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
      </div>
      {error && <div className="mt-3"><Alert tone="alert">{error}</Alert></div>}
      <div className="mt-3">
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending ? 'جارٍ…' : 'إنشاء الإدارة'}
        </Button>
      </div>
    </div>
  );
}

function DepartmentEditor({
  dept,
  users,
  onDone,
  setErr,
}: {
  dept: DepartmentDto;
  users: DirectoryUserDto[];
  onDone: () => void;
  setErr: (s: string | null) => void;
}) {
  const update = useUpdateDepartment();
  const [nameAr, setNameAr] = useState(dept.nameAr);
  const [nameEn, setNameEn] = useState(dept.nameEn ?? '');
  const [code, setCode] = useState(dept.code ?? '');
  const [managerId, setManagerId] = useState(dept.managerId ?? '');
  const [isActive, setIsActive] = useState(dept.isActive);

  const save = () => {
    setErr(null);
    if (!nameAr.trim()) { setErr('اسم الإدارة مطلوب.'); return; }
    update.mutate(
      { departmentId: dept.id, req: { nameAr: nameAr.trim(), nameEn: nameEn.trim() || null, code: code.trim() || null, managerId: managerId || null, isActive } },
      { onSuccess: onDone, onError: (e) => setErr(apiErrorMessage(e)) },
    );
  };

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <p className="mb-3 text-sm font-semibold text-navy">تعديل بيانات الإدارة</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="اسم الإدارة (عربي)">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field label="الاسم (إنجليزي) — اختياري">
          <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
        </Field>
        <Field label="الرمز — اختياري">
          <Input value={code} onChange={(e) => setCode(e.target.value)} />
        </Field>
        <Field label="مدير الإدارة — اختياري">
          <Select value={managerId} onChange={(e) => setManagerId(e.target.value)}>
            <option value="">— بدون مدير —</option>
            {users.filter((u) => u.isActive).map((u) => (
              <option key={u.id} value={u.id}>{u.fullName}</option>
            ))}
          </Select>
        </Field>
      </div>
      <label className="mt-3 flex items-center gap-2 text-sm text-navy">
        <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        إدارة نشطة
      </label>
      <div className="mt-3">
        <Button onClick={save} disabled={update.isPending}>
          {update.isPending ? 'جارٍ…' : 'حفظ التعديلات'}
        </Button>
      </div>
    </div>
  );
}

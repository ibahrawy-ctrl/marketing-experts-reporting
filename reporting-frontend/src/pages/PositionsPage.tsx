// إدارة المناصب المرنة (Phase 1A — رؤية فقط) — Admin فقط (سياسة PositionManagement خادمًا).
// المنصب يوسّع نطاق الرؤية فقط (تقارير/لوحة) ولا يمنح أي قدرة اعتماد أو كتابة.
import { useMemo, useState } from 'react';
import {
  usePositions,
  usePositionPermissionOptions,
  useCreatePosition,
  useUpdatePosition,
  useSetPositionActive,
  useAddPositionPermission,
  useRemovePositionPermission,
  useAddPositionScope,
  useRemovePositionScope,
} from '../lib/usePositions';
import { useDepartments, useTeams, useDirectoryUsers } from '../lib/useDirectory';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { apiErrorMessage } from '../lib/api';
import type { PositionDto, PositionScopeKind, AddPositionScopeRequest } from '../types/api';

type StatusFilter = 'active' | 'archived' | 'all';

const EMPTY_FORM = { code: '', name: '', description: '' };

const SCOPE_KIND_LABEL: Record<PositionScopeKind, string> = {
  Department: 'إدارة',
  Team: 'فريق',
  SpecificUsers: 'مستخدم محدّد',
  AllCompany: 'كامل الشركة',
};

export default function PositionsPage() {
  const positions = usePositions();
  const permOptions = usePositionPermissionOptions();
  const departments = useDepartments();
  const teams = useTeams();
  const users = useDirectoryUsers();

  const createPos = useCreatePosition();
  const updatePos = useUpdatePosition();
  const setActive = useSetPositionActive();
  const addPerm = useAddPositionPermission();
  const removePerm = useRemovePositionPermission();
  const addScope = useAddPositionScope();
  const removeScope = useRemovePositionScope();

  const [statusFilter, setStatusFilter] = useState<StatusFilter>('active');
  const [q, setQ] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ ...EMPTY_FORM });
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);

  // مدخلات إضافة نطاق (حالة محلية لكل منصب موسَّع).
  const [scopeKind, setScopeKind] = useState<PositionScopeKind>('Department');
  const [scopeDept, setScopeDept] = useState('');
  const [scopeTeam, setScopeTeam] = useState('');
  const [scopeUser, setScopeUser] = useState('');

  const list = useMemo(() => {
    let items = positions.data ?? [];
    if (statusFilter === 'active') items = items.filter((p) => p.isActive);
    else if (statusFilter === 'archived') items = items.filter((p) => !p.isActive);
    if (q.trim()) {
      const needle = q.trim().toLowerCase();
      items = items.filter(
        (p) => p.name.includes(q.trim()) || p.code.toLowerCase().includes(needle),
      );
    }
    return [...items].sort((a, b) => a.name.localeCompare(b.name, 'ar'));
  }, [positions.data, statusFilter, q]);

  if (positions.isLoading || permOptions.isLoading || departments.isLoading || teams.isLoading || users.isLoading)
    return <LoadingState label="يتم تحميل المناصب…" />;
  if (positions.isError)
    return <QueryError onRetry={() => positions.refetch()} description="حدث خطأ أثناء جلب المناصب." />;

  function resetForm() {
    setEditingId(null);
    setForm({ ...EMPTY_FORM });
  }

  function startEdit(p: PositionDto) {
    setEditingId(p.id);
    setForm({ code: p.code, name: p.name, description: p.description ?? '' });
    setErrorMsg(null);
    setOkMsg(null);
  }

  async function submit() {
    setErrorMsg(null);
    setOkMsg(null);
    const req = {
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description.trim() || null,
    };
    try {
      if (editingId) {
        await updatePos.mutateAsync({ id: editingId, req });
        setOkMsg(`تم تعديل المنصب «${req.name}».`);
      } else {
        await createPos.mutateAsync(req);
        setOkMsg(`تم إنشاء المنصب «${req.name}».`);
      }
      resetForm();
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function toggleActive(p: PositionDto) {
    if (
      p.isActive &&
      !window.confirm(`تعطيل المنصب «${p.name}»؟ سيتوقّف توسيع الرؤية لكل من أُسند إليه هذا المنصب.`)
    )
      return;
    setErrorMsg(null);
    setOkMsg(null);
    try {
      await setActive.mutateAsync({ id: p.id, isActive: !p.isActive });
      setOkMsg(p.isActive ? `تم تعطيل «${p.name}».` : `تم تفعيل «${p.name}».`);
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function togglePerm(p: PositionDto, key: string, has: boolean) {
    setErrorMsg(null);
    setOkMsg(null);
    try {
      if (has) await removePerm.mutateAsync({ id: p.id, permissionKey: key });
      else await addPerm.mutateAsync({ id: p.id, permissionKey: key });
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  function resetScopeInputs() {
    setScopeKind('Department');
    setScopeDept('');
    setScopeTeam('');
    setScopeUser('');
  }

  async function submitScope(p: PositionDto) {
    setErrorMsg(null);
    setOkMsg(null);
    const req: AddPositionScopeRequest = {
      kind: scopeKind,
      departmentId: scopeKind === 'Department' ? scopeDept || null : null,
      teamId: scopeKind === 'Team' ? scopeTeam || null : null,
      targetUserId: scopeKind === 'SpecificUsers' ? scopeUser || null : null,
    };
    try {
      await addScope.mutateAsync({ id: p.id, req });
      resetScopeInputs();
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function deleteScope(p: PositionDto, scopeId: string) {
    setErrorMsg(null);
    setOkMsg(null);
    try {
      await removeScope.mutateAsync({ id: p.id, scopeId });
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  const saving = createPos.isPending || updatePos.isPending;
  const busy =
    saving ||
    setActive.isPending ||
    addPerm.isPending ||
    removePerm.isPending ||
    addScope.isPending ||
    removeScope.isPending;

  const options = permOptions.data ?? [];

  return (
    <div className="space-y-6">
      <SectionTitle
        title="المناصب المرنة"
        hint="المنصب يوسّع نطاق الرؤية فقط (تقارير/لوحة) لمن يُسند إليه — ولا يمنح أي قدرة اعتماد أو إرجاع أو تعديل. الإدارة مقصورة على مدير النظام."
      />

      {errorMsg && <Alert tone="alert">{errorMsg}</Alert>}
      {okMsg && <Alert tone="success">{okMsg}</Alert>}

      {/* نموذج إنشاء/تعديل */}
      <Card>
        <SectionTitle title={editingId ? 'تعديل منصب' : 'إنشاء منصب جديد'} />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="الرمز" help="إلزامي — مُعرّف فريد (مثال: QUALITY_VIEWER).">
            <Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="QUALITY_VIEWER" />
          </Field>
          <Field label="الاسم" help="إلزامي.">
            <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="مراجع جودة" />
          </Field>
          <Field label="الوصف (اختياري)">
            <Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="منصب رؤية لمتابعة الجودة" />
          </Field>
        </div>
        <div className="mt-3 flex items-center gap-3">
          <Button onClick={submit} disabled={!form.code.trim() || !form.name.trim() || saving}>
            {saving ? 'جارٍ الحفظ…' : editingId ? 'حفظ التعديل' : 'إنشاء المنصب'}
          </Button>
          {editingId && (
            <Button variant="ghost" onClick={resetForm} disabled={saving}>إلغاء التعديل</Button>
          )}
        </div>
      </Card>

      {/* قائمة المناصب */}
      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <SectionTitle title={`المناصب (${list.length})`} />
          <div className="flex flex-wrap items-center gap-2">
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="ابحث بالاسم أو الرمز…" className="max-w-[220px]" />
            <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)} className="max-w-[160px]">
              <option value="active">النشطة</option>
              <option value="archived">المعطّلة</option>
              <option value="all">الكل</option>
            </Select>
          </div>
        </div>

        {list.length === 0 ? (
          <p className="py-10 text-center text-sm text-ink-2">لا توجد مناصب مطابقة.</p>
        ) : (
          <div className="space-y-3">
            {list.map((p) => {
              const expanded = expandedId === p.id;
              return (
                <div key={p.id} className="rounded-xl border border-line">
                  <div className="flex flex-wrap items-center justify-between gap-3 p-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-navy">{p.name}</span>
                        <Badge tone={p.isActive ? 'success' : 'muted'}>{p.isActive ? 'نشط' : 'معطّل'}</Badge>
                      </div>
                      <div className="text-xs text-ink-3">
                        {p.code}
                        {p.description ? ` · ${p.description}` : ''}
                      </div>
                      <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-ink-2">
                        <Badge tone="navy">{p.assignedUsersCount} مُسنَد</Badge>
                        <Badge tone="muted">{p.permissions.length} صلاحية</Badge>
                        <Badge tone="muted">{p.scopes.length} نطاق</Badge>
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      <Button variant="ghost" onClick={() => setExpandedId(expanded ? null : p.id)} disabled={busy}>
                        {expanded ? 'إخفاء' : 'الصلاحيات والنطاقات'}
                      </Button>
                      <Button variant="ghost" onClick={() => startEdit(p)} disabled={busy}>تعديل</Button>
                      <Button variant="ghost" onClick={() => toggleActive(p)} disabled={busy}>
                        {p.isActive ? 'تعطيل' : 'تفعيل'}
                      </Button>
                    </div>
                  </div>

                  {expanded && (
                    <div className="space-y-4 border-t border-line p-3">
                      {/* الصلاحيات */}
                      <div>
                        <p className="mb-1.5 text-sm font-semibold text-ink">الصلاحيات (رؤية فقط)</p>
                        <div className="flex flex-wrap gap-2">
                          {options.map((opt) => {
                            const has = p.permissions.includes(opt.key);
                            return (
                              <button
                                key={opt.key}
                                onClick={() => togglePerm(p, opt.key, has)}
                                disabled={busy}
                                className={`rounded-full border px-3 py-1 text-xs font-medium transition ${
                                  has
                                    ? 'border-navy bg-navy text-white'
                                    : 'border-line bg-white text-ink hover:border-navy'
                                }`}
                              >
                                {has ? '✓ ' : '+ '}
                                {opt.labelAr}
                              </button>
                            );
                          })}
                        </div>
                      </div>

                      {/* النطاقات */}
                      <div>
                        <p className="mb-1.5 text-sm font-semibold text-ink">نطاقات الرؤية</p>
                        {p.scopes.length === 0 ? (
                          <p className="mb-2 text-xs text-ink-3">لا توجد نطاقات بعد.</p>
                        ) : (
                          <div className="mb-2 flex flex-wrap gap-2">
                            {p.scopes.map((s) => {
                              const detail =
                                s.kind === 'Department'
                                  ? s.departmentName
                                  : s.kind === 'Team'
                                    ? s.teamName
                                    : s.kind === 'SpecificUsers'
                                      ? s.targetUserName
                                      : null;
                              return (
                                <span
                                  key={s.id}
                                  className="inline-flex items-center gap-2 rounded-lg border border-line bg-offwhite px-2.5 py-1 text-xs"
                                >
                                  <span className="font-medium text-navy">{SCOPE_KIND_LABEL[s.kind]}</span>
                                  {detail && <span className="text-ink-2">{detail}</span>}
                                  <button
                                    onClick={() => deleteScope(p, s.id)}
                                    disabled={busy}
                                    className="text-alert hover:underline"
                                    title="إزالة النطاق"
                                  >
                                    ✕
                                  </button>
                                </span>
                              );
                            })}
                          </div>
                        )}

                        {/* إضافة نطاق */}
                        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                          <Select value={scopeKind} onChange={(e) => setScopeKind(e.target.value as PositionScopeKind)}>
                            <option value="Department">إدارة</option>
                            <option value="Team">فريق</option>
                            <option value="SpecificUsers">مستخدم محدّد</option>
                            <option value="AllCompany">كامل الشركة</option>
                          </Select>
                          {scopeKind === 'Department' && (
                            <Select value={scopeDept} onChange={(e) => setScopeDept(e.target.value)}>
                              <option value="">— اختر إدارة —</option>
                              {(departments.data ?? []).map((d) => (
                                <option key={d.id} value={d.id}>{d.nameAr}</option>
                              ))}
                            </Select>
                          )}
                          {scopeKind === 'Team' && (
                            <Select value={scopeTeam} onChange={(e) => setScopeTeam(e.target.value)}>
                              <option value="">— اختر فريقًا —</option>
                              {(teams.data ?? []).map((t) => (
                                <option key={t.id} value={t.id}>{t.nameAr}</option>
                              ))}
                            </Select>
                          )}
                          {scopeKind === 'SpecificUsers' && (
                            <Select value={scopeUser} onChange={(e) => setScopeUser(e.target.value)}>
                              <option value="">— اختر مستخدمًا —</option>
                              {(users.data ?? []).map((u) => (
                                <option key={u.id} value={u.id}>{u.fullName}</option>
                              ))}
                            </Select>
                          )}
                          <Button
                            onClick={() => submitScope(p)}
                            disabled={
                              busy ||
                              (scopeKind === 'Department' && !scopeDept) ||
                              (scopeKind === 'Team' && !scopeTeam) ||
                              (scopeKind === 'SpecificUsers' && !scopeUser)
                            }
                          >
                            إضافة نطاق
                          </Button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
        <p className="mt-3 text-xs text-ink-3">
          المنصب يوحّد نطاق الرؤية مع نطاق الدور (اتحاد لا استبدال) — يضيف رؤية ولا يلغي أي رؤية قائمة.
          الصلاحيات هنا للرؤية فقط ولا تمنح اعتمادًا أو تعديلًا. التعطيل أو إلغاء الإسناد يوقف الأثر فورًا.
        </p>
      </Card>
    </div>
  );
}

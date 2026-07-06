// منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1) — Admin فقط (سياسة AdminOnly خادمًا).
// المنح يتيح للمستفيد رؤية تقارير مستخدم/فريق (عرض فقط، حالات معتمدة فقط) دون انضمامه للفريق
// ودون أيّ قدرة اعتماد/تعديل. معزول تمامًا عن KPI/المشاريع/نطاق الأدوار.
import { useMemo, useState } from 'react';
import {
  useReportViewGrants,
  useCreateReportViewGrant,
  useRevokeReportViewGrant,
} from '../lib/useReportViewGrants';
import { useDirectoryUsers, useTeams } from '../lib/useDirectory';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { apiErrorMessage } from '../lib/api';
import { formatDateTime } from '../lib/format';
import type { ReportViewGrantScopeKind } from '../types/api';

export default function ReportViewGrantsPage() {
  const [includeRevoked, setIncludeRevoked] = useState(false);
  const grants = useReportViewGrants(includeRevoked);
  const users = useDirectoryUsers();
  const teams = useTeams();

  const createGrant = useCreateReportViewGrant();
  const revokeGrant = useRevokeReportViewGrant();

  const [granteeUserId, setGranteeUserId] = useState('');
  const [scopeKind, setScopeKind] = useState<ReportViewGrantScopeKind>('User');
  const [targetUserId, setTargetUserId] = useState('');
  const [targetTeamId, setTargetTeamId] = useState('');
  const [notes, setNotes] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);

  const activeUsers = useMemo(
    () => (users.data ?? []).filter((u) => u.isActive).sort((a, b) => a.fullName.localeCompare(b.fullName, 'ar')),
    [users.data],
  );
  const activeTeams = useMemo(
    () => (teams.data ?? []).filter((t) => t.isActive).sort((a, b) => a.nameAr.localeCompare(b.nameAr, 'ar')),
    [teams.data],
  );

  if (grants.isLoading || users.isLoading || teams.isLoading)
    return <LoadingState label="يتم تحميل منح الرؤية…" />;
  if (grants.isError)
    return <QueryError onRetry={() => grants.refetch()} description="حدث خطأ أثناء جلب منح الرؤية." />;

  function resetForm() {
    setGranteeUserId('');
    setScopeKind('User');
    setTargetUserId('');
    setTargetTeamId('');
    setNotes('');
  }

  async function submit() {
    setErrorMsg(null);
    setOkMsg(null);
    if (!granteeUserId) {
      setErrorMsg('اختر المستفيد.');
      return;
    }
    if (scopeKind === 'User' && !targetUserId) {
      setErrorMsg('اختر المستخدم المستهدَف.');
      return;
    }
    if (scopeKind === 'Team' && !targetTeamId) {
      setErrorMsg('اختر الفريق المستهدَف.');
      return;
    }
    try {
      await createGrant.mutateAsync({
        granteeUserId,
        scopeKind,
        targetUserId: scopeKind === 'User' ? targetUserId : null,
        targetTeamId: scopeKind === 'Team' ? targetTeamId : null,
        notes: notes.trim() || null,
      });
      setOkMsg('تم إنشاء المنح بنجاح.');
      resetForm();
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  async function revoke(id: string) {
    if (!window.confirm('هل تريد إلغاء هذا المنح؟ سيفقد المستفيد رؤية التقارير المتاحة عبره.')) return;
    setErrorMsg(null);
    setOkMsg(null);
    try {
      await revokeGrant.mutateAsync(id);
      setOkMsg('تم إلغاء المنح.');
    } catch (e) {
      setErrorMsg(apiErrorMessage(e));
    }
  }

  const list = grants.data ?? [];

  return (
    <div className="space-y-6">
      <SectionTitle
        title="منح رؤية التقارير"
        hint="إتاحة رؤية تقارير مستخدم أو فريق لمستفيد محدّد — عرض فقط، حالات معتمدة فقط، دون انضمامه للفريق ودون أيّ صلاحية اعتماد أو تعديل."
      />

      <Alert tone="navy">
        المنح يتيح القراءة فقط للتقارير المُرسَلة رسميًّا (لا المسودّات ولا المُعادة للتعديل). لا يمنح المستفيد أيّ
        قدرة على الاعتماد/الإرجاع/التعديل، ولا يظهر داخل الفريق، ولا يفتح له مؤشرات الأداء أو المشاريع.
      </Alert>

      <Card>
        <h3 className="mb-4 text-lg font-semibold text-navy">إنشاء منح جديد</h3>
        {errorMsg && <div className="mb-3"><Alert tone="alert">{errorMsg}</Alert></div>}
        {okMsg && <div className="mb-3"><Alert tone="success">{okMsg}</Alert></div>}
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="المستفيد (من يرى التقارير)">
            <Select value={granteeUserId} onChange={(e) => setGranteeUserId(e.target.value)}>
              <option value="">— اختر المستفيد —</option>
              {activeUsers.map((u) => (
                <option key={u.id} value={u.id}>{u.fullName}</option>
              ))}
            </Select>
          </Field>
          <Field label="نوع النطاق">
            <Select
              value={scopeKind}
              onChange={(e) => setScopeKind(e.target.value as ReportViewGrantScopeKind)}
            >
              <option value="User">مستخدم محدّد</option>
              <option value="Team">فريق كامل</option>
            </Select>
          </Field>
          {scopeKind === 'User' ? (
            <Field label="المستخدم المستهدَف (صاحب التقارير)">
              <Select value={targetUserId} onChange={(e) => setTargetUserId(e.target.value)}>
                <option value="">— اختر المستخدم —</option>
                {activeUsers
                  .filter((u) => u.id !== granteeUserId)
                  .map((u) => (
                    <option key={u.id} value={u.id}>{u.fullName}</option>
                  ))}
              </Select>
            </Field>
          ) : (
            <Field label="الفريق المستهدَف (تقارير أعضائه)">
              <Select value={targetTeamId} onChange={(e) => setTargetTeamId(e.target.value)}>
                <option value="">— اختر الفريق —</option>
                {activeTeams.map((t) => (
                  <option key={t.id} value={t.id}>{t.nameAr}</option>
                ))}
              </Select>
            </Field>
          )}
          <Field label="ملاحظات (اختياري)">
            <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="سبب المنح…" />
          </Field>
        </div>
        <div className="mt-4 flex gap-2">
          <Button onClick={submit} disabled={createGrant.isPending}>
            {createGrant.isPending ? 'جارٍ الحفظ…' : 'إنشاء المنح'}
          </Button>
        </div>
      </Card>

      <Card>
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-navy">المنوح القائمة</h3>
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input
              type="checkbox"
              checked={includeRevoked}
              onChange={(e) => setIncludeRevoked(e.target.checked)}
            />
            إظهار المُلغاة
          </label>
        </div>
        {list.length === 0 ? (
          <p className="text-sm text-slate-500">لا توجد منح.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-right text-sm">
              <thead>
                <tr className="border-b text-slate-500">
                  <th className="p-2">المستفيد</th>
                  <th className="p-2">النطاق</th>
                  <th className="p-2">الهدف</th>
                  <th className="p-2">الحالة</th>
                  <th className="p-2">أُنشئ</th>
                  <th className="p-2">ملاحظات</th>
                  <th className="p-2"></th>
                </tr>
              </thead>
              <tbody>
                {list.map((g) => (
                  <tr key={g.id} className="border-b">
                    <td className="p-2">{g.granteeName}</td>
                    <td className="p-2">{g.scopeKind === 'User' ? 'مستخدم' : 'فريق'}</td>
                    <td className="p-2">
                      {g.scopeKind === 'User' ? g.targetUserName : g.targetTeamName}
                    </td>
                    <td className="p-2">
                      {g.isActive ? (
                        <Badge tone="success">نشط</Badge>
                      ) : (
                        <Badge tone="muted">مُلغًى</Badge>
                      )}
                    </td>
                    <td className="p-2">{formatDateTime(g.createdAtUtc)}</td>
                    <td className="p-2 text-slate-500">{g.notes ?? '—'}</td>
                    <td className="p-2">
                      {g.isActive && (
                        <Button
                          variant="ghost"
                          onClick={() => revoke(g.id)}
                          disabled={revokeGrant.isPending}
                        >
                          إلغاء
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

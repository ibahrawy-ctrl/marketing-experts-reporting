// مسارات الاعتماد — ثلاث نوافذ: مسارات الاعتماد بالأسماء + قائمة الاعتماد الحيّة (بانتظار اعتمادي) + نقاط الاختناق (الأقدم انتظارًا).
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { api } from '../lib/api';
import { Card, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { submissionStatusLabel } from '../lib/format';
import type {
  SubmissionListItem,
  WorkflowBottlenecksSummaryReport,
  WorkflowBottlenecksByStageReport,
  WorkflowBottlenecksByApproverReport,
  WorkflowBottlenecksDetailsReport,
} from '../types/api';

interface ChainStep {
  label: string;
  name: string;
  tone: 'navy' | 'orange' | 'success';
}

function Chain({ steps }: { steps: ChainStep[] }) {
  const dotTone: Record<ChainStep['tone'], string> = {
    navy: 'bg-navy text-white',
    orange: 'bg-orange text-white',
    success: 'bg-success text-white',
  };
  return (
    <div className="flex flex-wrap items-center gap-2">
      {steps.map((s, i) => (
        <div key={i} className="flex items-center gap-2">
          <div className="rounded-xl border border-line bg-white px-3 py-2 text-center">
            <span className={`mb-1 inline-block rounded-full px-2 py-0.5 text-[10px] font-bold ${dotTone[s.tone]}`}>{s.label}</span>
            <p className="text-sm font-semibold text-navy">{s.name}</p>
          </div>
          {i < steps.length - 1 && <span className="text-lg font-bold text-ink-3">←</span>}
        </div>
      ))}
    </div>
  );
}

// عمر الانتظار بالأيام منذ الإرسال (للتقارير التي لم تُغلق بعد).
function daysWaiting(iso: string | null): number | null {
  if (!iso) return null;
  const ms = Date.now() - new Date(iso).getTime();
  if (Number.isNaN(ms)) return null;
  return Math.max(0, Math.floor(ms / 86_400_000));
}

// لون شارة عمر الانتظار — أحمر إن تجاوز 7 أيام، ذهبي 3–7، أخضر أقل.
function ageTone(days: number | null): 'success' | 'gold' | 'alert' | 'muted' {
  if (days == null) return 'muted';
  if (days >= 7) return 'alert';
  if (days >= 3) return 'gold';
  return 'success';
}

// لون SLA: أخضر إن العمر < نصف SLA، ذهبي ضمن SLA، أحمر تجاوز SLA.
function slaTone(ageHours: number, slaHours: number): 'success' | 'gold' | 'alert' {
  if (slaHours > 0 && ageHours > slaHours) return 'alert';
  if (slaHours > 0 && ageHours >= slaHours / 2) return 'gold';
  return 'success';
}

// صياغة العمر بالساعات إلى نص مقروء (ساعات/أيام).
function ageText(hours: number): string {
  if (hours < 24) return `${Math.round(hours)} ساعة`;
  const days = Math.floor(hours / 24);
  const rem = Math.round(hours - days * 24);
  return rem > 0 ? `${days} يوم و${rem} ساعة` : `${days} يوم`;
}

type WorkflowTab = 'paths' | 'queue' | 'bottleneck';

export default function ApprovalWorkflowsPage() {
  const [tab, setTab] = useState<WorkflowTab>('paths');
  const [bnStage, setBnStage] = useState<string>('');
  const [bnTeamId, setBnTeamId] = useState<string>('');
  const [bnDeptId, setBnDeptId] = useState<string>('');
  const [bnOverdueOnly, setBnOverdueOnly] = useState<boolean>(false);
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();

  // قائمة بانتظار اعتمادي — مفروضة النطاق خادمًا (currentApproverId == أنا).
  const queue = useQuery({
    queryKey: ['workflow-pending-approvals'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/pending-approvals')).data,
  });

  // اختناقات مسار الاعتماد — مفروضة النطاق خادمًا عبر ScopeResolver. تُجلب فقط عند فتح التبويب.
  const bnEnabled = tab === 'bottleneck';
  const bnSummary = useQuery({
    queryKey: ['workflow-bottlenecks-summary'],
    queryFn: async () => (await api.get<WorkflowBottlenecksSummaryReport>('/reports/workflow-bottlenecks/summary')).data,
    enabled: bnEnabled,
  });
  const bnByStage = useQuery({
    queryKey: ['workflow-bottlenecks-by-stage'],
    queryFn: async () => (await api.get<WorkflowBottlenecksByStageReport>('/reports/workflow-bottlenecks/by-stage')).data,
    enabled: bnEnabled,
  });
  const bnByApprover = useQuery({
    queryKey: ['workflow-bottlenecks-by-approver'],
    queryFn: async () => (await api.get<WorkflowBottlenecksByApproverReport>('/reports/workflow-bottlenecks/by-approver')).data,
    enabled: bnEnabled,
  });
  const bnDetails = useQuery({
    queryKey: ['workflow-bottlenecks-details', bnStage, bnTeamId, bnDeptId, bnOverdueOnly],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (bnStage) qs.set('stage', bnStage);
      if (bnTeamId) qs.set('teamId', bnTeamId);
      if (bnDeptId) qs.set('departmentId', bnDeptId);
      if (bnOverdueOnly) qs.set('overdueOnly', 'true');
      const q = qs.toString();
      return (await api.get<WorkflowBottlenecksDetailsReport>(`/reports/workflow-bottlenecks/details${q ? `?${q}` : ''}`)).data;
    },
    enabled: bnEnabled,
  });

  if (users.isLoading || teams.isLoading) return <LoadingState label="يتم تحميل مسارات الاعتماد…" />;
  if (users.isError || teams.isError)
    return (
      <QueryError
        onRetry={() => {
          users.refetch();
          teams.refetch();
        }}
        description="حدث خطأ أثناء جلب مسارات الاعتماد. أعد المحاولة."
      />
    );

  const userList = users.data ?? [];
  const teamList = (teams.data ?? []).filter((t) => t.isActive);
  const deptList = departments.data ?? [];

  const nameOf = (id: string | null | undefined) => userList.find((u) => u.id === id)?.fullName ?? '—';
  const gm = userList.find((u) => u.roles.includes('GeneralManager'));
  const ceo = userList.find((u) => u.roles.includes('CEO'));

  // صفوف القائمة الحيّة مرتّبة بالأقدم انتظارًا أولًا.
  const queueRows = (queue.data ?? [])
    .map((s) => ({ ...s, age: daysWaiting(s.submittedAtUtc) }))
    .sort((a, b) => (b.age ?? -1) - (a.age ?? -1));

  const tabs: { key: WorkflowTab; label: string }[] = [
    { key: 'paths', label: 'مسارات الاعتماد' },
    { key: 'queue', label: `قائمة الاعتماد الحيّة (${queueRows.length})` },
    { key: 'bottleneck', label: 'نقاط الاختناق' },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">مسارات الاعتماد</h1>
        <p className="mt-1 text-sm text-ink-2">
          لا يصل أي تقرير إلى الرئيس التنفيذي مباشرة — إلا عبر التصعيد. كل تقرير يمرّ بالسلسلة الكاملة.
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`rounded-lg px-4 py-2 text-sm font-semibold transition ${
              tab === t.key ? 'bg-navy text-white' : 'bg-white text-navy border border-line hover:bg-navy-50'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'paths' && (
      <>
      <Card>
        <SectionTitle title="القاعدة العامة للاعتماد" hint="التسلسل القياسي لأي تقرير في النظام" />
        <Chain
          steps={[
            { label: 'يُنشئ', name: 'الموظف', tone: 'navy' },
            { label: 'اعتماد 1', name: 'قائد الفريق', tone: 'orange' },
            { label: 'اعتماد 2', name: 'المدير', tone: 'orange' },
            { label: 'اعتماد 3', name: gm ? gm.fullName : 'المدير العام', tone: 'orange' },
            { label: 'إغلاق', name: ceo ? ceo.fullName : 'الرئيس التنفيذي', tone: 'success' },
          ]}
        />
        <div className="mt-4 rounded-xl border border-gold/30 bg-amber-50 p-3 text-sm text-ink">
          <span className="font-bold text-gold">استثناء التصعيد:</span> عند التصعيد يُرفع التقرير لمستوى الإدارة الأعلى مباشرة دون انتظار المستوى التالي في السلسلة.
        </div>
      </Card>

      <Card>
        <SectionTitle title={`مسارات الفرق (${teamList.length})`} hint="السلسلة الفعلية بالأسماء لكل فريق" />
        <div className="space-y-4">
          {teamList.map((t) => {
            const dept = deptList.find((d) => d.id === t.departmentId);
            const memberCount = userList.filter((u) => u.teamId === t.id).length;
            const steps: ChainStep[] = [
              { label: 'الفريق', name: `${t.nameAr} (${memberCount} عضو)`, tone: 'navy' },
              { label: 'قائد الفريق', name: nameOf(t.teamLeaderId), tone: 'orange' },
              { label: 'المدير', name: nameOf(dept?.managerId), tone: 'orange' },
              { label: 'المدير العام', name: gm ? gm.fullName : '—', tone: 'orange' },
              { label: 'الرئيس التنفيذي', name: ceo ? ceo.fullName : '—', tone: 'success' },
            ];
            return (
              <div key={t.id} className="rounded-xl border border-line p-4">
                <div className="mb-3 flex items-center gap-2">
                  <h3 className="font-bold text-navy">{t.nameAr}</h3>
                  {dept && <Badge tone="navy">{dept.nameAr}</Badge>}
                </div>
                <div className="overflow-x-auto">
                  <Chain steps={steps} />
                </div>
              </div>
            );
          })}
          {teamList.length === 0 && (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد فرق نشطة لعرض مساراتها. تُنشأ الفرق وتُفعّل من صفحة «المستخدمون»، ثم تظهر سلسلة الاعتماد الكاملة لكل فريق هنا.</p>
          )}
        </div>
      </Card>
      </>
      )}

      {tab === 'queue' && (
        <Card>
          <SectionTitle title="قائمة الاعتماد الحيّة" hint="التقارير التي تنتظر اعتمادك أنت — مرتّبة بالأقدم انتظارًا أولًا" />
          {queue.isLoading ? (
            <LoadingState label="يتم تحميل قائمة الاعتماد…" />
          ) : queueRows.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد تقارير بانتظار اعتمادك حاليًا. كل ما يخصّك مُعتمَد.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead className="text-ink-2">
                  <tr className="border-b border-line">
                    <th className="py-2">صاحب التقرير</th>
                    <th className="py-2">القالب</th>
                    <th className="py-2">الفترة</th>
                    <th className="py-2">الحالة</th>
                    <th className="py-2">منذ الإرسال</th>
                    <th className="py-2"></th>
                  </tr>
                </thead>
                <tbody>
                  {queueRows.map((s) => (
                    <tr key={s.id} className="border-b border-line/60">
                      <td className="py-2 font-medium text-ink">
                        <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${s.submitterId}`}>
                          {s.submitterName}
                        </Link>
                      </td>
                      <td className="py-2 text-ink-2">{s.templateTitle}</td>
                      <td className="py-2 text-ink-2">{s.periodKey}</td>
                      <td className="py-2">
                        <Badge tone={s.status === 'Escalated' ? 'alert' : 'navy'}>{submissionStatusLabel[s.status]}</Badge>
                      </td>
                      <td className="py-2">
                        <Badge tone={ageTone(s.age)}>{s.age == null ? '—' : `${s.age} يوم`}</Badge>
                      </td>
                      <td className="py-2">
                        <Link className="text-orange-600 hover:underline" to={`/app/submissions?open=${s.id}`}>
                          فتح للاعتماد
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {tab === 'bottleneck' && (
        <div className="space-y-6">
          <Card>
            <SectionTitle
              title="ملخّص اختناقات مسار الاعتماد"
              hint="التقارير العالقة في خطوات الاعتماد ضمن نطاق رؤيتك — موضع وعمر فقط، بلا أيّ محتوى للتقرير"
            />
            {bnSummary.isLoading ? (
              <LoadingState label="يتم تحليل الاختناقات…" />
            ) : bnSummary.isError ? (
              <QueryError onRetry={() => bnSummary.refetch()} description="تعذّر جلب ملخّص الاختناقات. أعد المحاولة." />
            ) : (
              (() => {
                const s = bnSummary.data;
                if (!s) return null;
                return (
                  <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                    <div className="rounded-xl border border-line bg-white p-3">
                      <p className="text-xs text-ink-2">تقارير عالقة</p>
                      <p className="mt-1 text-2xl font-bold text-navy">{s.totalPending}</p>
                    </div>
                    <div className="rounded-xl border border-line bg-white p-3">
                      <p className="text-xs text-ink-2">متأخرة عن SLA</p>
                      <p className={`mt-1 text-2xl font-bold ${s.overduePending > 0 ? 'text-red-600' : 'text-success'}`}>
                        {s.overduePending}
                      </p>
                    </div>
                    <div className="rounded-xl border border-line bg-white p-3">
                      <p className="text-xs text-ink-2">الأقدم انتظارًا</p>
                      <p className="mt-1 text-lg font-bold text-navy">
                        {s.totalPending > 0 ? ageText(s.oldestPendingAgeHours) : '—'}
                      </p>
                    </div>
                    <div className="rounded-xl border border-line bg-white p-3">
                      <p className="text-xs text-ink-2">المرحلة الأكثر اختناقًا</p>
                      <p className="mt-1 text-sm font-bold text-navy">
                        {s.stageWithMostPendingLabel ?? '—'}
                        {s.stageWithMostPending && (
                          <span className="mr-1 text-ink-2">({s.stageWithMostPendingCount})</span>
                        )}
                      </p>
                    </div>
                    <div className="col-span-2 rounded-xl border border-line bg-white p-3 md:col-span-2">
                      <p className="text-xs text-ink-2">متوسّط عمر الخطوة</p>
                      <p className="mt-1 text-lg font-bold text-navy">
                        {s.totalPending > 0 ? ageText(s.averageStageAgeHours) : '—'}
                      </p>
                    </div>
                    <div className="col-span-2 rounded-xl border border-line bg-white p-3 md:col-span-2">
                      <p className="text-xs text-ink-2">المعتمِد الأكثر تراكمًا</p>
                      <p className="mt-1 text-sm font-bold text-navy">
                        {s.reviewerWithMostPendingName ?? '—'}
                        {s.reviewerWithMostPending && (
                          <span className="mr-1 text-ink-2">({s.reviewerWithMostPendingCount})</span>
                        )}
                      </p>
                    </div>
                  </div>
                );
              })()
            )}
          </Card>

          <Card>
            <SectionTitle title="التوزيع حسب المرحلة" hint="قائد فريق (SLA 24س) / مدير (48س) / الإدارة العليا (72س)" />
            {bnByStage.isLoading ? (
              <LoadingState label="يتم تحميل التوزيع…" />
            ) : (bnByStage.data?.rows.length ?? 0) === 0 ? (
              <p className="py-6 text-center text-sm text-ink-2">لا توجد مراحل بها تقارير عالقة ضمن نطاقك.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-right text-sm">
                  <thead className="text-ink-2">
                    <tr className="border-b border-line">
                      <th className="py-2">المرحلة</th>
                      <th className="py-2">عالقة</th>
                      <th className="py-2">متأخرة</th>
                      <th className="py-2">متوسّط العمر</th>
                      <th className="py-2">الأقدم</th>
                      <th className="py-2">SLA</th>
                    </tr>
                  </thead>
                  <tbody>
                    {bnByStage.data!.rows.map((r) => (
                      <tr key={r.stageKey} className="border-b border-line/60">
                        <td className="py-2 font-medium text-ink">{r.stageLabel}</td>
                        <td className="py-2">
                          <Badge tone="navy">{r.pendingCount}</Badge>
                        </td>
                        <td className="py-2">
                          <Badge tone={r.overdueCount > 0 ? 'alert' : 'success'}>{r.overdueCount}</Badge>
                        </td>
                        <td className="py-2 text-ink-2">{ageText(r.averageAgeHours)}</td>
                        <td className="py-2">
                          <Badge tone={slaTone(r.oldestAgeHours, r.slaHours)}>{ageText(r.oldestAgeHours)}</Badge>
                        </td>
                        <td className="py-2 text-ink-2">{r.slaHours} ساعة</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          <Card>
            <SectionTitle title="التوزيع حسب المعتمِد" hint="من تتراكم لديهم التقارير العالقة ضمن نطاقك" />
            {bnByApprover.isLoading ? (
              <LoadingState label="يتم تحميل التوزيع…" />
            ) : (bnByApprover.data?.rows.length ?? 0) === 0 ? (
              <p className="py-6 text-center text-sm text-ink-2">لا يوجد معتمِدون لديهم تقارير عالقة ضمن نطاقك.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-right text-sm">
                  <thead className="text-ink-2">
                    <tr className="border-b border-line">
                      <th className="py-2">المعتمِد</th>
                      <th className="py-2">المرحلة</th>
                      <th className="py-2">عالقة</th>
                      <th className="py-2">متأخرة</th>
                      <th className="py-2">متوسّط العمر</th>
                      <th className="py-2">الأقدم</th>
                    </tr>
                  </thead>
                  <tbody>
                    {bnByApprover.data!.rows.map((r) => (
                      <tr key={r.approverId} className="border-b border-line/60">
                        <td className="py-2 font-medium text-ink">
                          <Link className="text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${r.approverId}`}>
                            {r.approverName}
                          </Link>
                          <span className="mr-1 text-xs text-ink-3">{r.approverRoleLabel}</span>
                        </td>
                        <td className="py-2 text-ink-2">{r.stageLabel}</td>
                        <td className="py-2">
                          <Badge tone="navy">{r.pendingCount}</Badge>
                        </td>
                        <td className="py-2">
                          <Badge tone={r.overdueCount > 0 ? 'alert' : 'success'}>{r.overdueCount}</Badge>
                        </td>
                        <td className="py-2 text-ink-2">{ageText(r.averageAgeHours)}</td>
                        <td className="py-2">
                          <Badge tone={ageTone(Math.floor(r.oldestAgeHours / 24))}>{ageText(r.oldestAgeHours)}</Badge>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          <Card>
            <SectionTitle title="تفاصيل التقارير العالقة" hint="موضع كل تقرير في المسار وعمره مقابل SLA — بلا أيّ محتوى للتقرير" />
            <div className="mb-4 flex flex-wrap items-end gap-3">
              <label className="text-sm">
                <span className="mb-1 block text-ink-2">المرحلة</span>
                <select
                  value={bnStage}
                  onChange={(e) => setBnStage(e.target.value)}
                  className="rounded-lg border border-line bg-white px-3 py-2 text-navy"
                >
                  <option value="">كل المراحل</option>
                  <option value="team_leader">خطوة قائد الفريق</option>
                  <option value="manager">خطوة المدير</option>
                  <option value="senior_management">الإدارة العليا</option>
                </select>
              </label>
              <label className="text-sm">
                <span className="mb-1 block text-ink-2">الإدارة</span>
                <select
                  value={bnDeptId}
                  onChange={(e) => setBnDeptId(e.target.value)}
                  className="rounded-lg border border-line bg-white px-3 py-2 text-navy"
                >
                  <option value="">كل الإدارات</option>
                  {deptList.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.nameAr}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-sm">
                <span className="mb-1 block text-ink-2">الفريق</span>
                <select
                  value={bnTeamId}
                  onChange={(e) => setBnTeamId(e.target.value)}
                  className="rounded-lg border border-line bg-white px-3 py-2 text-navy"
                >
                  <option value="">كل الفرق</option>
                  {teamList.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.nameAr}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex items-center gap-2 pb-2 text-sm text-navy">
                <input type="checkbox" checked={bnOverdueOnly} onChange={(e) => setBnOverdueOnly(e.target.checked)} />
                المتأخرة عن SLA فقط
              </label>
            </div>
            {bnDetails.isLoading ? (
              <LoadingState label="يتم تحميل التفاصيل…" />
            ) : bnDetails.isError ? (
              <QueryError onRetry={() => bnDetails.refetch()} description="تعذّر جلب تفاصيل الاختناقات. أعد المحاولة." />
            ) : (bnDetails.data?.rows.length ?? 0) === 0 ? (
              <p className="py-6 text-center text-sm text-ink-2">لا توجد تقارير عالقة مطابقة للفلاتر ضمن نطاقك.</p>
            ) : (
              <>
                <div className="mb-3 flex flex-wrap gap-2 text-sm">
                  <Badge tone="navy">الإجمالي: {bnDetails.data!.total}</Badge>
                  <Badge tone={bnDetails.data!.overdue > 0 ? 'alert' : 'success'}>المتأخرة: {bnDetails.data!.overdue}</Badge>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-right text-sm">
                    <thead className="text-ink-2">
                      <tr className="border-b border-line">
                        <th className="py-2">القالب</th>
                        <th className="py-2">صاحب التقرير</th>
                        <th className="py-2">الفريق/الإدارة</th>
                        <th className="py-2">المرحلة</th>
                        <th className="py-2">المعتمِد الحالي</th>
                        <th className="py-2">الحالة</th>
                        <th className="py-2">عمر الخطوة</th>
                        <th className="py-2"></th>
                      </tr>
                    </thead>
                    <tbody>
                      {bnDetails.data!.rows.map((r) => (
                        <tr key={r.submissionId} className="border-b border-line/60">
                          <td className="py-2 font-medium text-ink">{r.templateTitle}</td>
                          <td className="py-2 text-ink-2">{r.submitterName}</td>
                          <td className="py-2 text-ink-2">{r.teamName ?? r.departmentName ?? '—'}</td>
                          <td className="py-2 text-ink-2">{r.stageLabel}</td>
                          <td className="py-2 text-ink-2">{r.currentApproverName ?? '—'}</td>
                          <td className="py-2">
                            <Badge tone={r.status === 'Escalated' ? 'alert' : 'navy'}>{submissionStatusLabel[r.status]}</Badge>
                          </td>
                          <td className="py-2">
                            <Badge tone={slaTone(r.ageHours, r.slaHours)}>
                              {ageText(r.ageHours)}
                              {r.isOverdue ? ' · متأخر' : ''}
                            </Badge>
                          </td>
                          <td className="py-2">
                            <Link className="text-orange-600 hover:underline" to={`/app/submissions?open=${r.submissionId}`}>
                              فتح
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <p className="pt-3 text-xs text-ink-3">
                  تلوين SLA: أخضر = أقل من نصف المهلة، ذهبي = ضمن المهلة، أحمر = تجاوز المهلة (متأخر).
                </p>
              </>
            )}
          </Card>
        </div>
      )}
    </div>
  );
}

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
import type { SubmissionListItem } from '../types/api';

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

type WorkflowTab = 'paths' | 'queue' | 'bottleneck';

export default function ApprovalWorkflowsPage() {
  const [tab, setTab] = useState<WorkflowTab>('paths');
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();

  // قائمة بانتظار اعتمادي — مفروضة النطاق خادمًا (currentApproverId == أنا).
  const queue = useQuery({
    queryKey: ['workflow-pending-approvals'],
    queryFn: async () => (await api.get<SubmissionListItem[]>('/submissions/pending-approvals')).data,
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

  // نقاط الاختناق: تجميع القائمة الحيّة حسب صاحب التقرير مع أقدم عمر انتظار.
  const bottlenecks = Object.values(
    queueRows.reduce<Record<string, { submitterId: string; name: string; count: number; maxAge: number }>>(
      (acc, s) => {
        const key = s.submitterId;
        const age = s.age ?? 0;
        if (!acc[key]) acc[key] = { submitterId: s.submitterId, name: s.submitterName, count: 0, maxAge: 0 };
        acc[key].count += 1;
        acc[key].maxAge = Math.max(acc[key].maxAge, age);
        return acc;
      },
      {},
    ),
  ).sort((a, b) => b.maxAge - a.maxAge || b.count - a.count);

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
        <Card>
          <SectionTitle title="نقاط الاختناق" hint="من تتراكم تقاريرهم في انتظار اعتمادك — الأطول انتظارًا أولًا" />
          {queue.isLoading ? (
            <LoadingState label="يتم تحليل نقاط الاختناق…" />
          ) : bottlenecks.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-2">لا توجد نقاط اختناق — لا تقارير متراكمة بانتظار اعتمادك.</p>
          ) : (
            <div className="space-y-2">
              {bottlenecks.map((b) => (
                <div key={b.submitterId} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line p-3">
                  <Link className="font-semibold text-navy hover:text-orange-600 hover:underline" to={`/app/employee/${b.submitterId}`}>
                    {b.name}
                  </Link>
                  <div className="flex items-center gap-2">
                    <Badge tone="navy">{b.count} تقرير بانتظارك</Badge>
                    <Badge tone={ageTone(b.maxAge)}>أقدم: {b.maxAge} يوم</Badge>
                  </div>
                </div>
              ))}
              <p className="pt-2 text-xs text-ink-3">
                التلوين: أحمر = تجاوز 7 أيام بانتظار اعتمادك، ذهبي = 3–7 أيام، أخضر = أقل من 3 أيام.
              </p>
            </div>
          )}
        </Card>
      )}
    </div>
  );
}

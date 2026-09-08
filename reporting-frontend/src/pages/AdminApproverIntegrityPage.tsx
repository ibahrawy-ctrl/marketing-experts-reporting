// RPT-APPROVER-INTEGRITY-01 — سطح الإنقاذ الإداريّ للاعتمادات العالقة.
// يعرض التسليمات المفتوحة التي **لا تظهر في «بانتظار اعتمادي» لأيّ مستخدم** لأنّ معتمِدها الحاليّ
// فارغ أو معطَّل أو مرجع يتيم، ويشغّل الإصلاح الرسميّ عليها بنفس سلسلة APPROVAL-FALLBACK-R1.
// الإصلاح ذو خطوتين إلزاميًّا: تخطيط (dryRun) يعرض من→إلى بلا أيّ كتابة، ثمّ تطبيق بتأكيد صريح.
// Admin/CEO/GM فقط (تطابق سياسة AdminReportDelete بالخادم)؛ الحماية الفعليّة مفروضة خادميًّا.
import { useState } from 'react';
import { Alert, Badge, Button, Card, EmptyState, Spinner, StatCard } from '../components/ui';
import { apiErrorMessage } from '../lib/api';
import { formatDateTime } from '../lib/format';
import { useApproverIntegrity, useRepairApproverIntegrity } from '../lib/useApproverIntegrity';
import type {
  ApproverIntegrityIssueDto,
  ApproverIssueKind,
  ApproverRepairReportDto,
} from '../types/api';

const kindLabel: Record<ApproverIssueKind, { text: string; tone: 'alert' | 'gold' | 'muted' }> = {
  NullApprover: { text: 'بلا معتمِد', tone: 'gold' },
  InactiveApprover: { text: 'معتمِد معطَّل', tone: 'alert' },
  OrphanApprover: { text: 'مرجع يتيم', tone: 'alert' },
};

export default function AdminApproverIntegrityPage() {
  const list = useApproverIntegrity();
  const repair = useRepairApproverIntegrity();
  const [plan, setPlan] = useState<ApproverRepairReportDto | null>(null);
  const [applied, setApplied] = useState<ApproverRepairReportDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handlePlan() {
    setError(null);
    setApplied(null);
    try {
      setPlan(await repair.mutateAsync(true));
    } catch (e) {
      setPlan(null);
      setError(apiErrorMessage(e, 'تعذّر إعداد خطّة الإصلاح.'));
    }
  }

  async function handleApply() {
    setError(null);
    try {
      const result = await repair.mutateAsync(false);
      setApplied(result);
      setPlan(null);
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر تنفيذ الإصلاح.'));
    }
  }

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold text-navy">سلامة مسارات الاعتماد</h1>
        <p className="mt-1 text-sm text-ink-2">
          تسليمات مفتوحة عالقة لا تظهر في طابور «بانتظار اعتمادي» لأيّ مستخدم، لأنّ معتمِدها الحاليّ
          فارغ أو معطَّل أو مرجع لمستخدم محذوف. الإصلاح يعيد توجيهها إلى معتمِد نشط وفق سلسلة
          الاعتماد المعتمَدة، ولا يغيّر نصّ التقرير ولا حالته ولا فترته ولا صاحبه.
        </p>
      </div>

      {list.isLoading ? (
        <Spinner />
      ) : list.isError ? (
        <Alert tone="alert">{apiErrorMessage(list.error, 'تعذّر تحميل سطح الإنقاذ.')}</Alert>
      ) : !list.data ? (
        <Alert tone="alert">تعذّر تحميل سطح الإنقاذ.</Alert>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="إجمالي الحالات العالقة" value={String(list.data.totalCount)} tone="navy" />
            <StatCard label="بلا معتمِد" value={String(list.data.nullApproverCount)} tone="gold" />
            <StatCard label="معتمِد معطَّل" value={String(list.data.inactiveApproverCount)} tone="alert" />
            <StatCard label="مرجع يتيم" value={String(list.data.orphanApproverCount)} tone="alert" />
          </div>

          {error && <Alert tone="alert">{error}</Alert>}

          {applied && (
            <Alert tone="success">
              تمّ الإصلاح: أُعيد توجيه {applied.reroutedCount} تسليمًا
              {applied.failedCount > 0 ? ` · تعذّر إيجاد معتمِد بديل لـ${applied.failedCount}` : ''}.
            </Alert>
          )}

          {list.data.items.length === 0 ? (
            <EmptyState
              title="لا اعتمادات عالقة"
              description="كلّ التسليمات المفتوحة لها معتمِد نشط يراها في طابوره."
            />
          ) : (
            <>
              <Card className="overflow-x-auto p-0">
                <table className="w-full text-right text-sm">
                  <thead className="border-b border-line text-xs text-ink-2">
                    <tr>
                      <th className="px-4 py-3 font-medium">نوع الخلل</th>
                      <th className="px-4 py-3 font-medium">صاحب التقرير</th>
                      <th className="px-4 py-3 font-medium">الفريق</th>
                      <th className="px-4 py-3 font-medium">الفترة</th>
                      <th className="px-4 py-3 font-medium">الحالة</th>
                      <th className="px-4 py-3 font-medium">المعتمِد الحاليّ</th>
                      <th className="px-4 py-3 font-medium">تاريخ الإرسال</th>
                      <th className="px-4 py-3 font-medium">أيّام التعليق</th>
                      <th className="px-4 py-3 font-medium">المعتمِد المقترَح</th>
                    </tr>
                  </thead>
                  <tbody>
                    {list.data.items.map((item) => (
                      <IssueRow key={item.submissionId} item={item} />
                    ))}
                  </tbody>
                </table>
              </Card>

              <Card>
                <h2 className="text-sm font-semibold text-navy">الإصلاح الإداريّ</h2>
                <p className="mt-1 text-sm text-ink-2">
                  ابدأ بالتخطيط: يعرض لكلّ تسليم المعتمِد القديم والجديد بلا أيّ كتابة. ثمّ طبّق.
                  العمليّة Idempotent — تشغيلها مرّة أخرى لا ينتج ازدواجًا.
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button variant="ghost" onClick={handlePlan} loading={repair.isPending && !plan}>
                    تخطيط بلا كتابة
                  </Button>
                  <Button
                    variant="danger"
                    onClick={handleApply}
                    disabled={!plan || plan.reroutedCount === 0}
                    loading={repair.isPending && !!plan}
                  >
                    تطبيق الإصلاح
                  </Button>
                </div>

                {plan && (
                  <div className="mt-4">
                    <Alert tone="gold">
                      خطّة مقترَحة بلا كتابة: {plan.reroutedCount} قابل لإعادة التوجيه
                      {plan.failedCount > 0 ? ` · ${plan.failedCount} بلا معتمِد بديل صالح` : ''}.
                    </Alert>
                    <div className="mt-3 overflow-x-auto rounded-lg border border-line">
                      <table className="w-full text-right text-xs">
                        <thead className="border-b border-line text-ink-2">
                          <tr>
                            <th className="px-3 py-2 font-medium">التسليم</th>
                            <th className="px-3 py-2 font-medium">من</th>
                            <th className="px-3 py-2 font-medium">إلى</th>
                            <th className="px-3 py-2 font-medium">النتيجة</th>
                          </tr>
                        </thead>
                        <tbody>
                          {plan.items.map((it) => (
                            <tr key={it.submissionId} className="border-b border-line last:border-0">
                              <td className="px-3 py-2 font-mono">{it.submissionId}</td>
                              <td className="px-3 py-2 font-mono text-ink-2">{it.fromApproverId ?? '—'}</td>
                              <td className="px-3 py-2 font-mono text-ink-2">{it.toApproverId ?? '—'}</td>
                              <td className="px-3 py-2">
                                {it.rerouted ? (
                                  <Badge tone="success">سيُعاد توجيهه</Badge>
                                ) : (
                                  <Badge tone="alert">لا معتمِد بديل</Badge>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </Card>
            </>
          )}
        </>
      )}
    </div>
  );
}

function IssueRow({ item }: { item: ApproverIntegrityIssueDto }) {
  const kind = kindLabel[item.kind];
  return (
    <tr className="border-b border-line last:border-0 hover:bg-navy-50/40">
      <td className="px-4 py-3">
        <Badge tone={kind.tone}>{kind.text}</Badge>
      </td>
      <td className="px-4 py-3 font-medium text-ink">
        {item.submitterName}
        {!item.submitterIsActive && <span className="mr-2 text-xs text-ink-2">(معطَّل)</span>}
      </td>
      <td className="px-4 py-3 text-ink-2">{item.teamName ?? '—'}</td>
      <td className="px-4 py-3">{item.periodKey}</td>
      <td className="px-4 py-3 text-ink-2">{item.status}</td>
      <td className="px-4 py-3 text-ink-2">{item.currentApproverName ?? '—'}</td>
      <td className="px-4 py-3 text-ink-2">{formatDateTime(item.submittedAtUtc)}</td>
      <td className="px-4 py-3">{item.stalledDays}</td>
      <td className="px-4 py-3">
        {item.suggestedApproverName ? (
          <Badge tone="success">{item.suggestedApproverName}</Badge>
        ) : (
          <Badge tone="alert">لا معتمِد بديل</Badge>
        )}
      </td>
    </tr>
  );
}

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, downloadFile, apiErrorMessage } from '../lib/api';
import { Card, StatCard, Badge, Button, Alert } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  formatPercent,
  kpiTrendDisplay,
  riskSeverityLabel,
  submissionStatusLabel,
} from '../lib/format';
import type {
  GovernanceSummaryReport,
  KpiSummaryReport,
  SubmissionCompletenessReport,
} from '../types/api';

export default function ExecutiveReportsPage() {
  const completeness = useQuery({
    queryKey: ['report-completeness'],
    queryFn: async () =>
      (await api.get<SubmissionCompletenessReport>('/reports/submission-completeness')).data,
  });
  const governance = useQuery({
    queryKey: ['report-governance'],
    queryFn: async () =>
      (await api.get<GovernanceSummaryReport>('/reports/governance-summary')).data,
  });
  const kpi = useQuery({
    queryKey: ['report-kpi'],
    queryFn: async () => (await api.get<KpiSummaryReport>('/reports/kpi-summary')).data,
  });

  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function exportFile(path: string, filename: string, key: string) {
    setError(null);
    setBusy(key);
    try {
      await downloadFile(path, filename);
    } catch (e) {
      setError(apiErrorMessage(e, 'تعذّر التصدير'));
    } finally {
      setBusy(null);
    }
  }

  if (completeness.isLoading || governance.isLoading || kpi.isLoading)
    return <LoadingState label="يتم تحميل التقارير التنفيذية…" />;
  if (completeness.isError || governance.isError || kpi.isError)
    return (
      <QueryError
        onRetry={() => {
          completeness.refetch();
          governance.refetch();
          kpi.refetch();
        }}
        title="تعذّر تحميل التقارير التنفيذية"
        description="حدث خطأ أثناء جلب بيانات الاكتمال أو المؤشّرات أو الحوكمة. أعد المحاولة."
      />
    );

  const c = completeness.data;
  const g = governance.data;
  const k = kpi.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-navy">التقارير التنفيذية</h1>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="primary"
            disabled={busy !== null}
            onClick={() =>
              exportFile(
                '/reports/executive-summary/export-pdf',
                'الملخص-التنفيذي.pdf',
                'exec',
              )
            }
          >
            {busy === 'exec' ? 'جارٍ التصدير…' : 'تصدير الملخص التنفيذي PDF'}
          </Button>
          <Button
            variant="ghost"
            disabled={busy !== null}
            onClick={() =>
              exportFile(
                '/reports/submission-completeness/export-pdf',
                'اكتمال-التقارير.pdf',
                'completeness',
              )
            }
          >
            {busy === 'completeness' ? 'جارٍ التصدير…' : 'اكتمال التقارير PDF'}
          </Button>
          <Button
            variant="ghost"
            disabled={busy !== null}
            onClick={() =>
              exportFile('/reports/kpi-summary/export-pdf', 'مؤشرات-الأداء.pdf', 'kpi')
            }
          >
            {busy === 'kpi' ? 'جارٍ التصدير…' : 'مؤشرات الأداء PDF'}
          </Button>
          <Button
            variant="ghost"
            disabled={busy !== null}
            onClick={() =>
              exportFile('/reports/submissions/export', 'التسليمات.csv', 'csv')
            }
          >
            {busy === 'csv' ? 'جارٍ التصدير…' : 'تصدير CSV'}
          </Button>
        </div>
      </div>

      {error && <Alert tone="alert">{error}</Alert>}

      <Alert tone="navy">
        كل ملف مُصدَّر (PDF/CSV) يلتزم بنطاق صلاحيتك: لا يظهر فيه إلا الموظّفون والإدارات ضمن نطاقك،
        وللفترة المعروضة حاليًا في هذه الصفحة. الملخّص التنفيذي تجميعي بلا تفاصيل أعضاء؛ أما CSV التسليمات
        فيُدرج صفوف التسليمات ضمن نطاقك فقط.
      </Alert>

      {/* اكتمال التقارير */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-ink">اكتمال التقارير</h2>
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard label="الإجمالي" value={c?.total ?? 0} />
          <StatCard label="مُغلقة" value={c?.closed ?? 0} tone="navy" />
          <StatCard label="قيد الإنجاز" value={c?.pending ?? 0} />
          <StatCard label="نسبة الاكتمال" value={formatPercent(c?.completionRate)} />
        </div>
        {!!c?.byDepartment.length && (
          <Card>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-ink-2">
                  <th className="pb-2">الإدارة</th>
                  <th className="pb-2">الإجمالي</th>
                  <th className="pb-2">مُغلقة</th>
                  <th className="pb-2">النسبة</th>
                </tr>
              </thead>
              <tbody>
                {c.byDepartment.map((d) => (
                  <tr key={d.departmentId ?? 'none'} className="border-t border-line">
                    <td className="py-2">{d.departmentName}</td>
                    <td className="py-2">{d.total}</td>
                    <td className="py-2">{d.closed}</td>
                    <td className="py-2">{formatPercent(d.completionRate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
        {!!c?.byStatus.length && (
          <div className="flex flex-wrap gap-2">
            {c.byStatus.map((s) => (
              <Badge key={s.status} tone="muted">
                {submissionStatusLabel[s.status]}: {s.count}
              </Badge>
            ))}
          </div>
        )}
      </section>

      {/* مؤشرات الأداء */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-ink">ملخص مؤشرات الأداء</h2>
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
          <StatCard label="المُقيَّمون" value={k?.evaluated ?? 0} />
          <StatCard
            label="متوسط الدرجات"
            value={k?.averageScore != null ? k.averageScore.toFixed(1) : '—'}
          />
          <StatCard label="دون المستهدف" value={k?.belowTarget ?? 0} tone="alert" />
        </div>
        {!!k?.rows.length && (
          <Card>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-ink-2">
                  <th className="pb-2">الموظف</th>
                  <th className="pb-2">الدرجة</th>
                  <th className="pb-2">الاتجاه</th>
                  <th className="pb-2">الحالة</th>
                </tr>
              </thead>
              <tbody>
                {k.rows.map((r, i) => (
                  <tr key={`${r.subjectUserId}-${r.periodKey}-${i}`} className="border-t border-line">
                    <td className="py-2">{r.subjectName}</td>
                    <td className="py-2">{r.totalScore != null ? r.totalScore.toFixed(1) : <span className="text-ink-2" title="لا يوجد تقييم لهذه الفترة">لا تقييم</span>}</td>
                    <td className="py-2">{kpiTrendDisplay(r.trend, r.totalScore != null)}</td>
                    <td className="py-2">
                      {r.isBelowTarget ? (
                        <Badge tone="alert">دون المستهدف</Badge>
                      ) : (
                        <Badge tone="success">ضمن المستهدف</Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
      </section>

      {/* الحوكمة */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-ink">ملخص الحوكمة</h2>
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
          <StatCard label="مخاطر مفتوحة" value={g?.openRisks ?? 0} tone="alert" />
          <StatCard label="تصعيدات مفتوحة" value={g?.openEscalations ?? 0} />
          <StatCard label="قرارات مفتوحة" value={g?.openDecisions ?? 0} />
          <StatCard label="احتياجات تدريب" value={g?.openTrainingNeeds ?? 0} />
          <StatCard label="خطط تحسين" value={g?.openImprovementPlans ?? 0} />
        </div>
        {!!g?.risksBySeverity.length && (
          <div className="flex flex-wrap gap-2">
            {g.risksBySeverity.map((s) => (
              <Badge key={s.severity} tone="gold">
                {riskSeverityLabel[s.severity]}: {s.count}
              </Badge>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

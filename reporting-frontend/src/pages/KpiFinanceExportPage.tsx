import { useState } from 'react';
import { Alert, Badge, Button, Card, EmptyState, Field, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { useKpiFinanceExport, downloadKpiFinanceCsv } from '../lib/useKpiFinanceExport';
import { useHrDirectoryDepartments, useHrDirectoryTeams } from '../lib/useDirectory';
import { apiErrorMessage } from '../lib/api';
import { kpiEvaluationStatusLabel, formatDateTime } from '../lib/format';
import type { KpiEvaluationStatus, KpiFinanceExportFilter, KpiFinanceExportRowDto } from '../types/api';

// الحالات المسموح تصديرها (Approved افتراضيًّا، أو Closed). أيّ حالة أخرى يرفضها الخادم.
const EXPORT_STATUSES: KpiEvaluationStatus[] = ['Approved', 'Closed'];
const QUARTERS = [1, 2, 3, 4];

const now = new Date();
const YEARS = [now.getFullYear() - 2, now.getFullYear() - 1, now.getFullYear(), now.getFullYear() + 1];

export default function KpiFinanceExportPage() {
  const [year, setYear] = useState(now.getFullYear());
  const [quarter, setQuarter] = useState(Math.floor(now.getMonth() / 3) + 1);
  const [departmentId, setDepartmentId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [status, setStatus] = useState<KpiEvaluationStatus>('Approved');
  const [exporting, setExporting] = useState(false);
  const [exportErr, setExportErr] = useState<string | null>(null);

  const departments = useHrDirectoryDepartments();
  const teams = useHrDirectoryTeams();

  const filter: KpiFinanceExportFilter = {
    year,
    quarter,
    departmentId: departmentId || undefined,
    teamId: teamId || undefined,
    status,
  };

  const { data, isLoading, isError, refetch } = useKpiFinanceExport(filter);

  const exportCsv = async () => {
    setExportErr(null);
    setExporting(true);
    try {
      await downloadKpiFinanceCsv(filter);
    } catch (e) {
      setExportErr(apiErrorMessage(e));
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">تصدير KPI للمالية</h1>
        <p className="mt-1 text-sm text-ink-2">
          معاينة وتصدير <span className="font-bold">التقييمات الربعيّة الرسميّة</span> المعتمَدة للربع المختار،
          على مستوى الشركة. كلّ صفّ = تقييم ربعيّ رسميّ معتمَد واحد.
        </p>
      </div>

      <Alert tone="navy">
        هذا التصدير قراءة فقط ولا يقوم بحساب أو صرف أي مستحقات مالية. مصدره{' '}
        <span className="font-bold">المسار الربعيّ الرسميّ وحده</span>؛ نبض الأسبوع مؤشّر تشغيليّ غير رسميّ
        ولا يدخل هذا التصدير.
      </Alert>

      {/* فلاتر */}
      <Card>
        <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-5">
          <Field label="السنة">
            <Select value={year} onChange={(e) => setYear(Number(e.target.value))}>
              {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
            </Select>
          </Field>
          <Field label="الربع">
            <Select value={quarter} onChange={(e) => setQuarter(Number(e.target.value))}>
              {QUARTERS.map((q) => <option key={q} value={q}>الربع {q}</option>)}
            </Select>
          </Field>
          <Field label="الإدارة">
            <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">كل الإدارات</option>
              {(departments.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.nameAr}</option>)}
            </Select>
          </Field>
          <Field label="الفريق">
            <Select value={teamId} onChange={(e) => setTeamId(e.target.value)}>
              <option value="">كل الفِرق</option>
              {(teams.data ?? []).map((t) => <option key={t.id} value={t.id}>{t.nameAr}</option>)}
            </Select>
          </Field>
          <Field label="الحالة">
            <Select value={status} onChange={(e) => setStatus(e.target.value as KpiEvaluationStatus)}>
              {EXPORT_STATUSES.map((s) => <option key={s} value={s}>{kpiEvaluationStatusLabel[s]}</option>)}
            </Select>
          </Field>
        </div>
      </Card>

      {exportErr && <Alert tone="alert">{exportErr}</Alert>}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-ink-2">
          {data ? `${data.periodLabel} — ${data.rowCount.toLocaleString('ar-EG')} صفّ` : ''}
        </p>
        <Button disabled={exporting || !data} onClick={exportCsv}>
          {exporting ? 'جارٍ التصدير…' : 'تصدير CSV'}
        </Button>
      </div>

      {isLoading ? (
        <LoadingState label="يتم تحميل المعاينة…" />
      ) : isError ? (
        <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب معاينة التصدير. أعد المحاولة." />
      ) : (
        <Card className="overflow-x-auto p-0">
          {(data?.rows ?? []).length === 0 ? (
            <div className="p-5">
              <EmptyState
                title="لا توجد تقييمات مطابقة"
                description="لا يوجد تقييم ربعيّ رسميّ معتمَد لهذا الربع بالفلاتر المختارة. التصدير سيُنتج رؤوس الأعمدة فقط. وجود نبض أسبوعيّ معتمَد داخل الربع لا يُغني عن التقييم الربعيّ ولا يظهر هنا."
              />
            </div>
          ) : (
            <ExportTable rows={data!.rows} />
          )}
        </Card>
      )}
    </div>
  );
}

function ExportTable({ rows }: { rows: KpiFinanceExportRowDto[] }) {
  return (
    <table className="w-full min-w-[1000px] text-right text-sm">
      <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
        <tr>
          <th className="px-3 py-2.5 font-semibold">اسم الموظف</th>
          <th className="px-3 py-2.5 font-semibold">الإدارة</th>
          <th className="px-3 py-2.5 font-semibold">الفريق</th>
          <th className="px-3 py-2.5 font-semibold">المسمى الوظيفي</th>
          <th className="px-3 py-2.5 font-semibold">مفتاح الفترة</th>
          <th className="px-3 py-2.5 font-semibold">السنة</th>
          <th className="px-3 py-2.5 font-semibold">الربع</th>
          <th className="px-3 py-2.5 font-semibold">القالب المستخدم</th>
          <th className="px-3 py-2.5 font-semibold">الدرجة النهائية</th>
          <th className="px-3 py-2.5 font-semibold">الحالة</th>
          <th className="px-3 py-2.5 font-semibold">تاريخ آخر تحديث / اعتماد</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.evaluationId} className="border-b border-line last:border-0 hover:bg-offwhite">
            <td className="px-3 py-2.5 font-medium text-navy">{r.employeeName}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.departmentName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.teamName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.jobRoleName ?? '—'}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.periodKey}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.year}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.quarter}</td>
            <td className="px-3 py-2.5 text-ink-2">{r.templateTitle}</td>
            <td className="px-3 py-2.5 text-ink-2">
              {r.totalScore != null ? r.totalScore.toLocaleString('ar-EG') : '—'}
            </td>
            <td className="px-3 py-2.5">
              <Badge tone="navy">{kpiEvaluationStatusLabel[r.status]}</Badge>
            </td>
            <td className="px-3 py-2.5 text-ink-2">{formatDateTime(r.lastUpdatedAtUtc)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

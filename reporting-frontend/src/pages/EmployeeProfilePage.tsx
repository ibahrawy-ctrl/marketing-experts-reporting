import { useEffect, useMemo, useState } from 'react';
import { useParams, useLocation, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { api, apiErrorMessage } from '../lib/api';
import {
  formatDate,
  formatDateTime,
  formatPercent,
  granularityLabel,
  kpiEvaluationStatusLabel,
  kpiTrendDisplay,
  monthKey,
  monthNamesAr,
  quarterKey,
  submissionStatusLabel,
  leaveTypeLabel,
  leaveRequestStatusLabel,
  leaveStatusTone,
} from '../lib/format';
import type { EmployeeProfileDto, KpiAggregateDto, KpiGranularity } from '../types/api';
import { ManagementNotesPanel } from '../components/ManagementNotesPanel';
import { Employee360Panel } from '../components/Employee360Panel';
import { LoadingState, QueryError } from '../components/states';
import { Badge, Card, EmptyState, Field, Select, Spinner, StatCard } from '../components/ui';

// السنوات المتاحة في مرشّح التجميع الدوري — السنة الحالية وما حولها.
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = [CURRENT_YEAR + 1, CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2];

// لون شارة الحالة المختصرة في الرأس — جيد أخضر، متابعة ذهبي، متأخر أحمر، لا بيانات رمادي.
const statusTone: Record<string, 'success' | 'gold' | 'alert' | 'muted'> = {
  good: 'success',
  watch: 'gold',
  late: 'alert',
  insufficient: 'muted',
};

/**
 * ملف أداء الموظف الموحّد (Phase 3) — صفحة واحدة تجمع: الرأس + ملخّص الأداء + التقارير + تقييمات KPI
 * + الملاحظات الإدارية + بنود الحوكمة + الخط الزمني.
 * النطاق مفروض خادمًا: من يفتح ملفًا خارج نطاقه يحصل 403/404 → نعرض حالة فارغة واضحة بدل بيانات.
 */
export default function EmployeeProfilePage() {
  const { userId = '' } = useParams();
  const location = useLocation();

  // P2-EMP-003 — وضع الذات: المسار `/app/employee/me` يُحَلّ **خادميًّا** عبر سطح 360،
  // فلا نستدعي عرض الملفّ الإداريّ القديم (المبنيّ على معرّف صريح) ولا نشتقّ المعرّف محلّيًّا.
  const isSelfRoute = userId === 'me';

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['employee-profile', userId],
    queryFn: async () => (await api.get<EmployeeProfileDto>(`/dashboard/employee-profile/${userId}`)).data,
    retry: false,
    enabled: !isSelfRoute,
  });

  // عند الوصول عبر رابط «عرض التقييمات» (#kpi) ننتقل تلقائيًا لقسم تقييمات الأداء بعد تحميل البيانات.
  useEffect(() => {
    if (!data || !location.hash) return;
    const el = document.querySelector(location.hash);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [data, location.hash]);

  // وضع الذات يعرض سطح 360 وحده: الموظّف يرى ما يخصّه فقط، ولا تُطلَب أيّ نقطة إداريّة
  // تفترض صلاحيّة إشرافيّة. ما لا يُصرَّح به لا يصل من الخادم أصلًا فلا يُرسَم هنا.
  if (isSelfRoute) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold text-navy">ملفّي</h1>
        <Employee360Panel subject="me" />
      </div>
    );
  }

  if (isLoading) return <Spinner />;

  if (isError) {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined;
    const denied = status === 403 || status === 404;
    return (
      <div className="mx-auto max-w-3xl py-10">
        <EmptyState
          title={denied ? 'لا يمكن عرض هذا الملف' : 'تعذّر تحميل الملف'}
          description={
            denied
              ? 'هذا الموظّف خارج نطاق صلاحيتك، أو الملف غير موجود. لا يمكنك رؤية بيانات موظّف لا يخصّ نطاقك.'
              : apiErrorMessage(error)
          }
        />
      </div>
    );
  }

  if (!data) return null;

  const { header, summary, reports, kpiEvaluations, governanceItems, timeline, leaveRequests } = data;

  return (
    <div className="space-y-6">
      {/* ===== (1) الرأس ===== */}
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="mb-1 flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold text-navy">{header.fullName}</h1>
              <Badge tone={statusTone[header.statusKey] ?? 'muted'}>{header.statusLabel}</Badge>
              {!header.isActive && <Badge tone="alert">غير مفعّل</Badge>}
            </div>
            <p className="text-sm text-ink-2">{header.email ?? '—'}</p>
          </div>
        </div>

        <div className="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <p className="text-ink-2">المسمّى الوظيفي</p>
            <p className="font-medium text-ink">{header.jobRoleName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">الفريق</p>
            <p className="font-medium text-ink">{header.teamName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">الإدارة</p>
            <p className="font-medium text-ink">{header.departmentName ?? '—'}</p>
          </div>
          <div>
            <p className="text-ink-2">المدير المباشر</p>
            <p className="font-medium text-ink">{header.directManagerName ?? '—'}</p>
          </div>
        </div>
        <div className="mt-4">
          <Link
            to={`/app/employees/${userId}/kpi`}
            className="inline-block rounded-lg bg-orange px-4 py-2 text-sm font-semibold text-white hover:bg-orange-600"
          >
            مؤشرات أداء الموظف
          </Link>
        </div>
      </Card>

      {/* ===== (2) ملخّص الأداء ===== */}
      <section>
        <h2 className="mb-3 font-semibold text-navy">ملخّص الأداء</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard
            label={`آخر تقييم KPI${summary.lastKpiPeriod ? ` (${summary.lastKpiPeriod})` : ''}`}
            value={summary.lastKpiScore ?? '—'}
            tone={summary.lastKpiScore != null && summary.lastKpiScore < 60 ? 'alert' : 'navy'}
          />
          <StatCard label={`متوسط KPI (${summary.kpiCount} تقييم)`} value={summary.averageKpi ?? '—'} />
          <StatCard label="تقارير مُسلَّمة" value={summary.reportsSubmitted} />
          <StatCard
            label="تحتاج إجراء (مسودة/مُعاد/مُصعّد)"
            value={summary.reportsNeedsAction}
            tone={summary.reportsNeedsAction > 0 ? 'alert' : 'navy'}
          />
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-3">
          <StatCard label="تقارير مُعادة للتعديل" value={summary.reportsReturned} tone={summary.reportsReturned > 0 ? 'alert' : 'navy'} />
          <StatCard
            label="ملاحظات إدارية تتطلّب إجراءً"
            value={summary.openNotesRequiringAction}
            tone={summary.openNotesRequiringAction > 0 ? 'alert' : 'navy'}
          />
          <Card>
            <p className="text-sm text-ink-2">اتجاه آخر تقييم</p>
            <p className="mt-2 text-lg font-semibold text-navy">
              {kpiTrendDisplay(summary.lastKpiTrend, summary.lastKpiScore != null)}
            </p>
          </Card>
        </div>
      </section>

      {/* ===== (2-ب) التجميع الدوري لمؤشرات الأداء (Phase 5 §11) ===== */}
      <KpiPeriodSection userId={userId} />

      {/* ===== (3) التقارير ===== */}
      <section>
        <h2 className="mb-3 font-semibold text-navy">أحدث التقارير</h2>
        <Card>
          {reports.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد تقارير لهذا الموظّف بعد.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead className="text-ink-2">
                  <tr className="border-b border-line">
                    <th className="py-2">القالب</th>
                    <th className="py-2">الفترة</th>
                    <th className="py-2">الحالة</th>
                    <th className="py-2">تاريخ التسليم</th>
                    <th className="py-2">المعتمِد الحالي</th>
                    <th className="py-2"></th>
                  </tr>
                </thead>
                <tbody>
                  {reports.map((r) => (
                    <tr key={r.submissionId} className="border-b border-line/60">
                      <td className="py-2 font-medium text-ink">{r.templateTitle}</td>
                      <td className="py-2 text-ink-2">{r.periodKey}</td>
                      <td className="py-2">
                        <Badge tone={r.status === 'Returned' ? 'alert' : r.status === 'Closed' ? 'success' : 'navy'}>
                          {submissionStatusLabel[r.status]}
                        </Badge>
                      </td>
                      <td className="py-2 text-ink-2">{formatDate(r.submittedAtUtc)}</td>
                      <td className="py-2 text-ink-2">{r.currentApproverName ?? '—'}</td>
                      <td className="py-2">
                        <Link className="text-orange-600 hover:underline" to={`/app/submissions?open=${r.submissionId}`}>
                          عرض
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </section>

      {/* ===== (4) تقييمات KPI ===== */}
      <section id="kpi" className="scroll-mt-24">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold text-navy">أحدث تقييمات الأداء (KPI)</h2>
          <Link to={`/app/kpi?subject=${userId}`} className="text-sm font-semibold text-orange-600 hover:underline">
            عرض كل تقييمات الموظّف
          </Link>
        </div>
        <Card>
          {kpiEvaluations.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد تقييمات KPI لهذا الموظّف بعد.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead className="text-ink-2">
                  <tr className="border-b border-line">
                    <th className="py-2">القالب</th>
                    <th className="py-2">الفترة</th>
                    <th className="py-2">الدرجة</th>
                    <th className="py-2">الاتجاه</th>
                    <th className="py-2">الحالة</th>
                    <th className="py-2"></th>
                  </tr>
                </thead>
                <tbody>
                  {kpiEvaluations.map((k) => (
                    <tr key={k.evaluationId} className="border-b border-line/60">
                      <td className="py-2 font-medium text-ink">{k.templateTitle}</td>
                      <td className="py-2 text-ink-2">{k.periodKey}</td>
                      <td className="py-2 font-semibold text-navy">{k.totalScore ?? '—'}</td>
                      <td className="py-2 text-ink-2">{kpiTrendDisplay(k.trend, k.totalScore != null)}</td>
                      <td className="py-2">
                        <Badge tone="navy">{kpiEvaluationStatusLabel[k.status]}</Badge>
                      </td>
                      <td className="py-2">
                        <Link className="text-orange-600 hover:underline" to={`/app/kpi?open=${k.evaluationId}`}>
                          عرض
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </section>

      {/* ===== (5) الملاحظات الإدارية — يعاد استخدام لوحة Phase 2 بسياق الموظف ===== */}
      <ManagementNotesPanel entityType="User" entityId={userId} title="الملاحظات الإدارية على الموظّف" />

      {/* ===== (6) بنود الحوكمة المرتبطة ===== */}
      <section>
        <h2 className="mb-3 font-semibold text-navy">بنود الحوكمة المرتبطة</h2>
        <Card>
          {governanceItems.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد مخاطر أو تصعيدات مرتبطة بهذا الموظّف.</p>
          ) : (
            <ul className="space-y-2">
              {governanceItems.map((g) => (
                <li key={`${g.kind}-${g.id}`} className="flex flex-wrap items-center gap-2 border-b border-line/60 pb-2">
                  <Badge tone={g.kind === 'Risk' ? 'gold' : 'orange'}>{g.kind === 'Risk' ? 'مخاطرة' : 'تصعيد'}</Badge>
                  <span className="text-sm text-ink">{g.title}</span>
                  <span className="text-xs text-ink-2">· {g.status} · {formatDate(g.createdAtUtc)}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </section>

      {/* ===== (6-ب) الإجازات والاستئذانات (V1.0.1) — عرض فقط ===== */}
      <section>
        <h2 className="mb-3 font-semibold text-navy">الإجازات والاستئذانات</h2>
        <Card>
          {leaveRequests.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد طلبات إجازة أو استئذان لهذا الموظّف بعد.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead className="text-ink-2">
                  <tr className="border-b border-line">
                    <th className="py-2">النوع</th>
                    <th className="py-2">المدّة</th>
                    <th className="py-2">الحالة</th>
                    <th className="py-2">المعتمِد النهائي</th>
                    <th className="py-2">يؤثّر في التقارير؟</th>
                    <th className="py-2"></th>
                  </tr>
                </thead>
                <tbody>
                  {leaveRequests.map((l) => (
                    <tr key={l.id} className="border-b border-line/60">
                      <td className="py-2 font-medium text-ink">{leaveTypeLabel[l.type]}</td>
                      <td className="py-2 text-ink-2">
                        {l.type === 'Permission'
                          ? `${formatDate(l.startDate)} · ${(l.startTime ?? '').slice(0, 5)} — ${(l.endTime ?? '').slice(0, 5)}`
                          : l.startDate === l.endDate
                            ? formatDate(l.startDate)
                            : `${formatDate(l.startDate)} — ${formatDate(l.endDate)}`}
                      </td>
                      <td className="py-2">
                        <Badge tone={leaveStatusTone(l.status)}>{leaveRequestStatusLabel[l.status]}</Badge>
                      </td>
                      <td className="py-2 text-ink-2">{l.finalApproverName ?? '—'}</td>
                      <td className="py-2">
                        {l.impactsReports ? <Badge tone="success">نعم</Badge> : <span className="text-ink-3">—</span>}
                      </td>
                      <td className="py-2">
                        <Link className="text-orange-600 hover:underline" to={`/app/leave-requests?open=${l.id}`}>
                          عرض
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </section>

      {/* ===== (7) الخط الزمني ===== */}
      <section>
        <h2 className="mb-3 font-semibold text-navy">الخط الزمني</h2>
        <Card>
          {timeline.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد أحداث مسجّلة بعد.</p>
          ) : (
            <ul className="space-y-3">
              {timeline.map((t, i) => (
                <li key={i} className="flex items-start gap-3">
                  <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-orange" />
                  <div>
                    <p className="text-sm text-ink">{t.label}</p>
                    <p className="text-xs text-ink-2">{formatDateTime(t.atUtc)}</p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </section>

      {/* ===== (8) الملفّ الشامل 360 (P2-EMP-003) — إضافة لا استبدال ===== */}
      <Employee360Panel subject={userId} />
    </div>
  );
}

// ===== التجميع الدوري لمؤشرات الأداء لموظّف بعينه (Phase 5 §11) =====
// يستخدم نقطة /kpi-evaluations/aggregate مع subjectUserId — النطاق مفروض خادمًا.
function KpiPeriodSection({ userId }: { userId: string }) {
  const [granularity, setGranularity] = useState<KpiGranularity>('Monthly');
  const [year, setYear] = useState(CURRENT_YEAR);
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [quarter, setQuarter] = useState(Math.floor(new Date().getMonth() / 3) + 1);

  // مفتاح الفترة المُشتق حسب الدقّة المختارة (شهر/ربع/سنة).
  const periodKey = useMemo(() => {
    if (granularity === 'Monthly') return monthKey(year, month);
    if (granularity === 'Quarterly') return quarterKey(year, quarter);
    return String(year);
  }, [granularity, year, month, quarter]);

  const agg = useQuery({
    queryKey: ['employee-kpi-aggregate', userId, granularity, periodKey],
    queryFn: async () =>
      (
        await api.get<KpiAggregateDto>('/kpi-evaluations/aggregate', {
          params: { granularity, periodKey, subjectUserId: userId },
        })
      ).data,
    retry: false,
  });

  return (
    <section>
      <h2 className="mb-3 font-semibold text-navy">التجميع الدوري للأداء (KPI)</h2>
      <Card>
        <p className="mb-4 text-sm text-ink-2">
          الأسبوع هو وحدة الأساس؛ المتوسط الشهري/الربع سنوي/السنوي يُحتسب من متوسط نتائج الأسابيع داخل المدى.
        </p>

        <div className="flex flex-wrap items-end gap-3">
          <div className="w-40">
            <Field label="الدقّة">
              <Select value={granularity} onChange={(e) => setGranularity(e.target.value as KpiGranularity)}>
                <option value="Monthly">{granularityLabel.Monthly}</option>
                <option value="Quarterly">{granularityLabel.Quarterly}</option>
                <option value="Yearly">{granularityLabel.Yearly}</option>
              </Select>
            </Field>
          </div>
          <div className="w-32">
            <Field label="السنة">
              <Select value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {YEARS.map((y) => (
                  <option key={y} value={y}>{y}</option>
                ))}
              </Select>
            </Field>
          </div>
          {granularity === 'Monthly' && (
            <div className="w-36">
              <Field label="الشهر">
                <Select value={month} onChange={(e) => setMonth(Number(e.target.value))}>
                  {monthNamesAr.map((name, i) => (
                    <option key={i} value={i + 1}>{name}</option>
                  ))}
                </Select>
              </Field>
            </div>
          )}
          {granularity === 'Quarterly' && (
            <div className="w-32">
              <Field label="الربع">
                <Select value={quarter} onChange={(e) => setQuarter(Number(e.target.value))}>
                  {[1, 2, 3, 4].map((q) => (
                    <option key={q} value={q}>الربع {q}</option>
                  ))}
                </Select>
              </Field>
            </div>
          )}
        </div>

        <div className="mt-4">
          {agg.isLoading ? (
            <LoadingState label="يتم احتساب المتوسط…" />
          ) : agg.isError ? (
            <QueryError onRetry={() => agg.refetch()} description="تعذّر احتساب متوسط المؤشرات." />
          ) : agg.data ? (
            <EmployeeKpiAggregateResult data={agg.data} />
          ) : null}
        </div>
      </Card>
    </section>
  );
}

function EmployeeKpiAggregateResult({ data }: { data: KpiAggregateDto }) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-ink-2">
        {granularityLabel[data.granularity] ?? data.granularity} · {data.periodLabel}
      </p>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        <StatCard
          label="متوسط المؤشر"
          value={data.average == null ? '—' : formatPercent(data.average)}
          tone={data.average != null && data.average < 60 ? 'alert' : 'navy'}
        />
        <StatCard label="عدد الأسابيع المحتسَبة" value={data.weeksCount} />
        <StatCard label="عدد التقييمات" value={data.evaluationsCount} />
      </div>

      {data.weeks.length === 0 ? (
        <p className="py-4 text-center text-sm text-ink-2">لا توجد تقييمات أسبوعية داخل هذا المدى بعد.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[480px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الأسبوع</th>
                <th className="px-2 py-2 font-semibold">من</th>
                <th className="px-2 py-2 font-semibold">إلى</th>
                <th className="px-2 py-2 font-semibold">النتيجة</th>
                <th className="px-2 py-2 font-semibold">عدد التقييمات</th>
              </tr>
            </thead>
            <tbody>
              {data.weeks.map((w) => (
                <tr key={w.periodKey} className="border-b border-line last:border-0">
                  <td className="px-2 py-2 font-medium text-navy">{w.periodKey}</td>
                  <td className="px-2 py-2 text-ink-2">{formatDate(w.weekStart)}</td>
                  <td className="px-2 py-2 text-ink-2">{formatDate(w.weekEnd)}</td>
                  <td className="px-2 py-2 font-semibold">{formatPercent(w.score)}</td>
                  <td className="px-2 py-2 text-ink-2">{w.evaluationsCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

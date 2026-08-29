// نظرة KPI متعددة المستويات — الشركة ← الإدارة ← الفريق ← الموظف.
//
// P1-KPI-008: كلّ رقم هنا يأتي من `/api/kpi/*` محسوبًا على الخادم بتوسيط ذي مرحلتين (B-2)
// وعلى تقييمات Approved فقط داخل الفترة والكادنس المطلوبَين. **لا يُشتقّ أيّ رقم في هذا الملفّ**:
// لا `avg()`، ولا ترتيب محلّيّ، ولا `new Map(subjectUserId → row)` الذي كان يطوي تقييمات الموظّف
// إلى آخر صفّ فيُظهِر «أعلى تقييم» بدل المتوسّط، ولا `?? 0` يحوّل «لا تقييم» إلى صفر.
// مُرشِّح واحد يقود البطاقات والرسم والجدول والترتيب والتفصيل معًا.
import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  DEFAULT_KPI_FILTER,
  appliedThreshold,
  cadenceLabel,
  cadenceSourceLabel,
  exemptReasonLabel,
  journeyStateLabel,
  journeyStateTone,
  kpiTone,
  trendLabel,
  useKpiDrilldown,
  useKpiPerformance,
  useKpiRankings,
  type KpiEmployeeScore,
  type KpiFilter,
  type KpiGroupScore,
  type KpiMeasure,
} from '../lib/useKpi';
import { KpiFilterBar } from '../components/KpiFilterBar';
import { Card, Badge, StatCard, Button } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle, ProgressBar } from '../components/dashboard';
import { Donut } from '../components/Charts';
import { formatPercent } from '../lib/format';

/**
 * «لا تقييم» ≠ صفر: الرقم الغائب يُعرَض غيابًا صريحًا لا رقمًا مصطنعًا.
 * DEC-01/14 — الدرجة دون عتبة التغطية تُوسَم **«مؤقّتة»** صراحةً: تُعرَض للاطّلاع
 * ولا تُقرأ نتيجةً ربعيّة نهائيّة.
 */
function ScoreBadge({ measure, threshold }: { measure: KpiMeasure; threshold: number | null }) {
  return (
    <span className="flex items-center gap-1">
      <Badge tone={kpiTone(measure.value, threshold)}>
        {measure.value === null ? (
          <span title="لا يوجد تقييم معتمَد في هذه الفترة">لا تقييم</span>
        ) : (
          formatPercent(measure.value)
        )}
      </Badge>
      {measure.isProvisional && (
        <Badge tone="gold">
          <span title="تغطية دون الحدّ الأدنى المعتمَد — لا تُعتمد نتيجة نهائيّة ولا تدخل المتوسّط الرسميّ">
            مؤقّتة
          </span>
        </Badge>
      )}
    </span>
  );
}

/**
 * DEC-01/8+12+18 — التغطية معروضة بأرقامها الأربعة: الحالة الصريحة، ثمّ
 * `Completed/AdjustedExpected` والنسبة كما حسبها الخادم، ثمّ `Expected` الخامّ حين اختلف
 * عن المعدَّل (فيرى المستخدم **الرقمين معًا** ويعرف كم أُسقِط)، ثمّ المفقود.
 * لا حساب هنا: `coveragePercent` يأتي جاهزًا من الخادم.
 */
function CoverageBadge({ measure }: { measure: KpiMeasure }) {
  const adjusted = measure.expectedEvaluationCount - measure.adjustedExpectedCount;
  return (
    <span className="flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
      <Badge tone={journeyStateTone[measure.journeyState]}>{journeyStateLabel[measure.journeyState]}</Badge>
      <span title="التقييمات المكتملة المعتمَدة مقابل المتوقَّع بعد خصم الإعفاءات">
        {measure.eligibleEvaluationCount}/{measure.adjustedExpectedCount}
        {measure.coveragePercent !== null && ` (${formatPercent(measure.coveragePercent)})`}
      </span>
      {adjusted > 0 && (
        <span title="المتوقَّع الخامّ قبل خصم الإجازات والإعفاءات وحدود الالتحاق/الخروج">
          · متوقَّع خامّ {measure.expectedEvaluationCount} (أُسقِط {adjusted})
        </span>
      )}
      {measure.missingCount > 0 && <span title="التزامات بلا تقييم معتمَد — مفقودة لا أصفار">· {measure.missingCount} مفقود</span>}
    </span>
  );
}

/** DEC-01/5 — تواتر الموظّف ومصدره ظاهران، فلا يبقى «لماذا هذا تواتري؟» سؤالًا بلا جواب. */
function CadenceChip({ employee }: { employee: KpiEmployeeScore }) {
  if (employee.effectiveCadence === null)
    return (
      <span className="text-xs font-semibold text-alert" title="لا قالب فعّال يحدّد تواتر هذا الموظّف — يُعالَج بإسناد قالب لا بافتراض تواتر">
        التواتر غير مُهيّأ
      </span>
    );
  return (
    <span className="text-xs text-ink-3">
      {cadenceLabel[employee.effectiveCadence]} · {cadenceSourceLabel[employee.cadenceSource]}
    </span>
  );
}

function TrendChip({ measure }: { measure: KpiMeasure }) {
  if (measure.trend === 'Unknown') return <span className="text-xs text-ink-3" title="لا مقارنة متاحة (فترة مفتوحة أو بلا بيانات سابقة)">—</span>;
  const tone = measure.trend === 'Up' ? 'text-success' : measure.trend === 'Down' ? 'text-alert' : 'text-ink-2';
  return (
    <span className={`text-xs ${tone}`}>
      {trendLabel[measure.trend]}
      {measure.delta !== null && ` ${measure.delta > 0 ? '+' : ''}${measure.delta}`}
    </span>
  );
}

export function KpiOverview() {
  const [filter, setFilter] = useState<KpiFilter>(DEFAULT_KPI_FILTER);
  const [expandedTeam, setExpandedTeam] = useState<string | null>(null);
  const [drillUser, setDrillUser] = useState<string | null>(null);

  const perf = useKpiPerformance(filter);
  const rankings = useKpiRankings(filter);

  // المُرشِّح يبقى ظاهرًا في كلّ الحالات: تغييره يُطلِق جلبًا جديدًا، وإخفاؤه أثناء الجلب يجعل
  // المستخدم يفقد أداة التحكّم في منتصف التصفية.
  const shell = (children: ReactNode) => (
    <div className="space-y-6">
      <KpiFilterBar filter={filter} onChange={setFilter} resolved={perf.data?.periodResolved} />
      {children}
    </div>
  );

  if (perf.isLoading) return shell(<LoadingState label="يتم تحميل ملخص المؤشّرات…" />);
  if (perf.isError)
    return shell(
      <QueryError
        onRetry={() => {
          perf.refetch();
          rankings.refetch();
        }}
        description="حدث خطأ أثناء جلب ملخص المؤشّرات. أعد المحاولة."
      />,
    );

  const data = perf.data!;
  const company = data.company;
  // العتبة تأتي من الخادم (نسخة القالب أوّلًا ثمّ الإعداد المركزيّ) — لا ثابت في الواجهة (B-6).
  const threshold = appliedThreshold(data);
  const belowTarget = data.employees.filter((e) => e.isBelowTarget === true).length;
  const expanded = data.teams.find((t) => t.groupId === expandedTeam);
  const expandedMembers = expanded
    ? data.employees.filter((e) => e.teamId === expanded.groupId)
    : [];

  return shell(
    <>
      {/* مستوى الشركة — متوسّط متوسّطات الموظّفين (B-2)، لا متوسّط خام على التقييمات. */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          label="متوسط مؤشر الشركة"
          value={company.measure.value === null ? '—' : formatPercent(company.measure.value)}
        />
        {/* DEC-01/16 — المتوسّط الرسميّ يُبنى من المؤهّلين وحدهم، فيُعرَض عددهم لا عدد من له درجة فقط. */}
        <StatCard
          label="مؤهّلون للمتوسّط الرسميّ"
          value={`${company.qualifiedMemberCount}/${company.totalMemberCount}`}
        />
        <StatCard label="دون المستهدف" value={belowTarget} tone={belowTarget > 0 ? 'alert' : 'navy'} />
        <StatCard label="الفرق" value={data.teams.length} />
      </div>

      <div className="flex flex-wrap items-center gap-3 text-sm">
        <CoverageBadge measure={company.measure} />
        <TrendChip measure={company.measure} />
        <span className="text-xs text-ink-3" title="لهم درجة محسوبة، سواء دخلت المتوسّط الرسميّ أم بقيت مؤقّتة">
          · {company.scoredMemberCount} موظّفًا له درجة
        </span>
        {company.measure.excludedByStatusCount > 0 && (
          <span className="text-xs text-ink-3" title="تقييمات موجودة لكنّها غير معتمَدة فلا تدخل الرقم">
            · {company.measure.excludedByStatusCount} تقييم غير معتمَد مستبعَد
          </span>
        )}
      </div>

      {/* DEC-01/17 — غير المؤهّلين لا يختفون من النتائج: أسماؤهم وحالتهم منفصلة عن المتوسّط الرسميّ. */}
      <ExcludedPanel group={company} />
      {/* DEC-01/5 — من لا تواتر فعّالًا لهم: حالة إداريّة مسمّاة تُعالَج، لا صفر ولا إخفاء. */}
      <CadenceNotConfiguredPanel employees={data.employees} />

      <div className="grid gap-4 lg:grid-cols-2">
        {/* مستوى الإدارة — متوسّط متوسّطات موظّفيها مباشرةً، لا متوسّط الفرق. */}
        <Card>
          <SectionTitle title="مؤشر الأداء حسب الإدارة" />
          <div className="space-y-3">
            {data.departments.map((d: KpiGroupScore) => (
              <div key={d.groupId ?? d.groupName}>
                <div className="mb-1 flex items-center justify-between text-sm">
                  <span className="font-medium text-navy">{d.groupName ?? '—'}</span>
                  <span className="text-ink-2" title="المتوسّط الرسميّ · المؤهّلون من إجمالي الأعضاء">
                    {d.measure.value === null ? 'لا تقييم' : formatPercent(d.measure.value)} ·{' '}
                    {d.qualifiedMemberCount}/{d.totalMemberCount}
                  </span>
                </div>
                {/* الشريط يعرض صفرًا للفارغ عمدًا لأنّه رسم لا رقم؛ الرقم نفسه أعلاه يقول «لا تقييم». */}
                <ProgressBar
                  value={d.measure.value ?? 0}
                  tone={kpiTone(d.measure.value, threshold) === 'success' ? 'success' : 'orange'}
                />
                <CoverageBadge measure={d.measure} />
              </div>
            ))}
            {data.departments.length === 0 && (
              <p className="py-6 text-center text-sm text-ink-2">
                لا توجد إدارات ضمن نطاقك. تظهر متوسطات المؤشّرات لكل إدارة بمجرّد تقييم أعضائها.
              </p>
            )}
          </div>
        </Card>

        {/* توزيع مستوى الأداء — يُبنى من درجات الموظّفين (صفّ واحد لكلّ موظّف) لا من التقييمات. */}
        <Card>
          <SectionTitle title="توزيع مستوى الأداء" hint="عدد الموظفين حسب فئة المؤشر (بلا تكرار)" />
          <Donut
            slices={[
              {
                // الفئات تُشتقّ من نبرة العتبة القادمة من الخادم؛ غياب العتبة يجعل التصنيف محايدًا.
                label: threshold === null ? 'بلغ المستهدف' : `ممتاز (≥${threshold})`,
                value: data.employees.filter((e) => kpiTone(e.measure.value, threshold) === 'success').length,
              },
              {
                label: threshold === null ? 'قريب من المستهدف' : `متوسط (≥${Math.round(threshold * 0.75)})`,
                value: data.employees.filter((e) => kpiTone(e.measure.value, threshold) === 'gold').length,
              },
              {
                label: threshold === null ? 'دون المستهدف' : `دون المستهدف (<${Math.round(threshold * 0.75)})`,
                value: data.employees.filter((e) => kpiTone(e.measure.value, threshold) === 'alert').length,
              },
              {
                label: 'لا تقييم',
                value: data.employees.filter((e) => e.measure.value === null).length,
              },
            ]}
          />
        </Card>
      </div>

      {/* مستوى الفريق */}
      <Card>
        <SectionTitle title="مؤشر الأداء حسب الفريق" hint="انقر فريقًا لعرض تفصيل الأعضاء" />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-right text-sm">
            <thead className="border-b border-line text-xs text-ink-2">
              <tr>
                <th className="px-2 py-2 font-semibold">الفريق</th>
                <th className="px-2 py-2 font-semibold" title="من دخلوا المتوسّط الرسميّ من إجمالي الأعضاء">
                  مؤهّلون
                </th>
                <th className="px-2 py-2 font-semibold">متوسط KPI</th>
                <th className="px-2 py-2 font-semibold">التغطية</th>
                <th className="px-2 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {data.teams.map((t) => (
                <tr key={t.groupId ?? t.groupName} className="border-b border-line last:border-0">
                  <td className="px-2 py-2 font-medium text-navy">{t.groupName ?? '—'}</td>
                  <td className="px-2 py-2">
                    {t.qualifiedMemberCount}/{t.totalMemberCount}
                  </td>
                  <td className="px-2 py-2">
                    <ScoreBadge measure={t.measure} threshold={threshold} />
                  </td>
                  <td className="px-2 py-2">
                    <CoverageBadge measure={t.measure} />
                  </td>
                  <td className="px-2 py-2">
                    <button
                      onClick={() => setExpandedTeam(expandedTeam === t.groupId ? null : t.groupId)}
                      className="text-sm font-semibold text-orange-600 hover:underline"
                    >
                      {expandedTeam === t.groupId ? 'إخفاء' : 'تفصيل الأعضاء'}
                    </button>
                  </td>
                </tr>
              ))}
              {data.teams.length === 0 && (
                <tr>
                  <td colSpan={5} className="py-6 text-center text-sm text-ink-2">
                    لا توجد فرق ضمن نطاقك.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {expanded && (
          <div className="mt-4 rounded-xl border border-line bg-offwhite p-4">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="font-bold text-navy">أعضاء فريق {expanded.groupName}</h3>
              <Link to={`/app/teams/${expanded.groupId}`}>
                <Button variant="ghost">صفحة الفريق</Button>
              </Link>
            </div>
            <div className="grid gap-2 sm:grid-cols-2">
              {expandedMembers.map((e) => (
                <EmployeeCard
                  key={e.userId}
                  employee={e}
                  threshold={threshold}
                  onDrill={() => setDrillUser(drillUser === e.userId ? null : e.userId)}
                  drilling={drillUser === e.userId}
                  filter={filter}
                />
              ))}
              {expandedMembers.length === 0 && <p className="text-sm text-ink-2">لا أعضاء.</p>}
            </div>
          </div>
        )}
      </Card>

      {/* الترتيب — من الخادم مباشرةً بعد تطبيق شرط التغطية (B-5)، بلا فرز أو تصفية هنا. */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SectionTitle
            title="الأعلى أداءً"
            hint={
              rankings.data
                ? `تغطية لا تقلّ عن ${Math.round(rankings.data.minimumCoverage * 100)}٪ · مستبعَدون لضعف التغطية: ${rankings.data.excludedForInsufficientCoverage}`
                : undefined
            }
          />
          <RankList rows={rankings.data?.topPerformers ?? []} threshold={threshold} />
        </Card>
        <Card>
          <SectionTitle title="الأكثر حاجة للدعم" />
          <RankList rows={rankings.data?.needsSupport ?? []} threshold={threshold} />
        </Card>
      </div>

      {/* DEC-01/17 — المستبعَدون من الترتيب بأسمائهم، منفصلين عن القائمتين الرسميّتين. */}
      {rankings.data?.excludedEmployees && rankings.data.excludedEmployees.length > 0 && (
        <Card>
          <SectionTitle
            title={`خارج الترتيب لضعف التغطية (${rankings.data.excludedEmployees.length})`}
            hint={`الحدّ الأدنى المعتمَد ${formatPercent(rankings.data.minimumCoverage * 100)} من المتوقَّع المعدَّل`}
          />
          <ul className="space-y-2 text-sm">
            {rankings.data.excludedEmployees.map((e) => (
              <li key={e.userId} className="flex flex-wrap items-center justify-between gap-2 border-b border-line py-2 last:border-0">
                <Link to={`/app/employee/${e.userId}`} className="font-medium text-navy hover:text-orange-600 hover:underline">
                  {e.fullName}
                </Link>
                <span className="flex flex-wrap items-center gap-2">
                  <CoverageBadge measure={e.measure} />
                  <ScoreBadge measure={e.measure} threshold={threshold} />
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </>,
  );
}

/**
 * DEC-01/17 — «الموظفون غير المؤهّلين بسبب ضعف التغطية لا يختفون من النتائج. يجب عرض عددهم
 * وأسمائهم وحالة النقص بشكل منفصل عن المتوسط الرسمي.» هذه اللوحة هي تنفيذ ذلك البند حرفيًّا:
 * منفصلة عن الرقم الرسميّ، وبالأسماء لا بالعدد وحده.
 */
function ExcludedPanel({ group }: { group: KpiGroupScore }) {
  const rows = group.excludedForInsufficientCoverage ?? [];
  if (rows.length === 0) return null;
  return (
    <Card>
      <SectionTitle
        title={`مستبعَدون من المتوسّط الرسميّ لضعف التغطية (${rows.length})`}
        hint="درجاتهم مؤقّتة ولا تدخل المتوسّط ولا التصدير المالي النهائي — يظهرون هنا للمعالجة لا للإخفاء"
      />
      <ul className="space-y-2 text-sm">
        {rows.map((e) => (
          <li key={e.userId} className="flex flex-wrap items-center justify-between gap-2 border-b border-line py-2 last:border-0">
            <Link to={`/app/employee/${e.userId}`} className="font-medium text-navy hover:text-orange-600 hover:underline">
              {e.fullName}
            </Link>
            <CoverageBadge measure={e.measure} />
          </li>
        ))}
      </ul>
    </Card>
  );
}

/**
 * DEC-01/5 — «إن لم يوجد أي إعداد فعّال، تُعرض حالة `التواتر غير مُهيّأ` دون اختيار ضمني».
 * هؤلاء بلا مقام أصلًا، فلا تغطية لهم ولا درجة؛ الحلّ إسناد قالب لا افتراض تواتر.
 */
function CadenceNotConfiguredPanel({ employees }: { employees: KpiEmployeeScore[] }) {
  const rows = employees.filter((e) => e.effectiveCadence === null);
  if (rows.length === 0) return null;
  return (
    <Card>
      <SectionTitle
        title={`التواتر غير مُهيّأ (${rows.length})`}
        hint="لا قالب فعّال يحدّد تواترهم — لا يُحتسب لهم متوقَّع ولا تغطية حتّى يُسنَد قالب"
      />
      <ul className="flex flex-wrap gap-2 text-sm">
        {rows.map((e) => (
          <li key={e.userId}>
            <Link
              to={`/app/employee/${e.userId}`}
              className="rounded-lg border border-line px-2 py-1 font-medium text-navy hover:text-orange-600"
            >
              {e.fullName}
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function EmployeeCard({
  employee,
  threshold,
  onDrill,
  drilling,
  filter,
}: {
  employee: KpiEmployeeScore;
  threshold: number | null;
  onDrill: () => void;
  drilling: boolean;
  filter: KpiFilter;
}) {
  const drill = useKpiDrilldown({ ...filter, subjectUserId: employee.userId }, drilling);
  return (
    <div className="rounded-lg border border-line bg-white px-3 py-2.5 text-sm">
      <div className="flex items-center justify-between gap-2">
        <Link
          to={`/app/employee/${employee.userId}`}
          className="font-medium text-navy hover:text-orange-600 hover:underline"
        >
          {employee.fullName}
        </Link>
        <span className="flex items-center gap-2">
          <TrendChip measure={employee.measure} />
          <ScoreBadge measure={employee.measure} threshold={threshold} />
        </span>
      </div>
      <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
        <CoverageBadge measure={employee.measure} />
        <CadenceChip employee={employee} />
        {!employee.eligibleForRanking && employee.measure.value !== null && (
          <span className="text-alert" title="يظهر في العرض الفرديّ لكنّه خارج الترتيب والمقارنة الرسميّة">
            خارج الترتيب — تغطية غير كافية
          </span>
        )}
        <button onClick={onDrill} className="font-semibold text-orange-600 hover:underline">
          {drilling ? 'إخفاء التفصيل' : 'تفصيل الرقم'}
        </button>
      </div>

      {/* DEC-01/18 — التفصيل يجب أن يصل إلى: Expected · AdjustedExpected · Completed · Missing ·
          Coverage · الفترات المصدر. وهو أيضًا إعادة إنتاج للرقم من صفوفه ليتحقّق المستخدم يدويًّا. */}
      {drilling && drill.data && (
        <div className="mt-2 space-y-2 rounded-lg border border-line bg-offwhite p-2 text-xs">
          {drill.data.measure && <DrilldownNumbers measure={drill.data.measure} />}
          <p className="text-ink-2">
            {drill.data.rowCount} تقييم مكتمل معتمَد · المتوسّط المُعاد حسابه:{' '}
            {drill.data.recomputedValue === null ? 'لا تقييم' : formatPercent(drill.data.recomputedValue)}
          </p>

          {/* الفترات المصدر: كلّ فترة داخل النافذة إمّا مكتملة أو مُعفاة بسبب مسمّى أو مفقودة.
              المفقودة تظهر «مفقودة» لا صفرًا (DEC-01/10). */}
          {drill.data.sourcePeriods && drill.data.sourcePeriods.length > 0 && (
            <ul className="space-y-1">
              {drill.data.sourcePeriods.map((p) => (
                <li key={p.periodKey} className="flex items-center justify-between gap-2">
                  <span className="text-ink-3" title={`${p.start} ← ${p.end}`}>
                    {p.label}
                  </span>
                  {p.isExempt ? (
                    <Badge tone="muted">
                      {p.exemptReason ? (exemptReasonLabel[p.exemptReason] ?? p.exemptReason) : 'مُعفاة'}
                    </Badge>
                  ) : p.isCompleted ? (
                    <span className="font-medium text-navy">
                      {p.score === null ? '—' : formatPercent(p.score)}
                    </span>
                  ) : (
                    <Badge tone="alert">مفقودة</Badge>
                  )}
                </li>
              ))}
            </ul>
          )}

          {drill.data.rows.length > 0 && (
            <ul className="space-y-1 border-t border-line pt-1">
              {drill.data.rows.map((r) => (
                <li key={r.evaluationId} className="flex justify-between gap-2">
                  <span className="text-ink-3">
                    {r.periodKey} · {r.templateTitle}
                  </span>
                  <span className="font-medium text-navy">
                    {r.totalScore === null ? '—' : formatPercent(r.totalScore)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

/** DEC-01/18 — الأرقام الخمسة المطلوبة في التفصيل، كلّ رقم بمسمّاه لا مطويًّا في كسر واحد. */
function DrilldownNumbers({ measure }: { measure: KpiMeasure }) {
  const cells: { label: string; value: string; title: string }[] = [
    { label: 'المتوقَّع', value: String(measure.expectedEvaluationCount), title: 'الالتزامات المتوقَّعة حسب التواتر داخل النافذة' },
    { label: 'المتوقَّع المعدَّل', value: String(measure.adjustedExpectedCount), title: 'بعد خصم الإجازات المعتمدة والإعفاءات وحدود الالتحاق/الخروج' },
    { label: 'المكتمل', value: String(measure.eligibleEvaluationCount), title: 'التقييمات التي بلغت حالة الاعتماد' },
    { label: 'المفقود', value: String(measure.missingCount), title: 'التزامات بلا تقييم معتمَد — مفقودة لا أصفار' },
    {
      label: 'التغطية',
      value: measure.coveragePercent === null ? '—' : formatPercent(measure.coveragePercent),
      title: 'المكتمل ÷ المتوقَّع المعدَّل × 100',
    },
  ];
  return (
    <div className="grid grid-cols-3 gap-1 sm:grid-cols-5">
      {cells.map((c) => (
        <div key={c.label} className="rounded-md bg-white px-2 py-1 text-center" title={c.title}>
          <p className="text-ink-3">{c.label}</p>
          <p className="font-semibold text-navy">{c.value}</p>
        </div>
      ))}
    </div>
  );
}

function RankList({ rows, threshold }: { rows: KpiEmployeeScore[]; threshold: number | null }) {
  if (rows.length === 0)
    return (
      <p className="py-6 text-center text-sm text-ink-2">
        لا يوجد من يستوفي شرط التغطية في هذه الفترة بعد. يظهر الترتيب بمجرّد اعتماد تقييمات كافية.
      </p>
    );
  return (
    <ul className="space-y-2 text-sm">
      {rows.map((r) => (
        <li key={r.userId} className="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0">
          <Link to={`/app/employee/${r.userId}`} className="font-medium text-navy hover:text-orange-600 hover:underline">
            {r.fullName}
          </Link>
          <span className="flex items-center gap-2">
            <TrendChip measure={r.measure} />
            <ScoreBadge measure={r.measure} threshold={threshold} />
          </span>
        </li>
      ))}
    </ul>
  );
}

import { useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { Alert, Badge, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { SectionTitle } from '../components/dashboard';
import { LoadingState, QueryError } from '../components/states';
import {
  useB2bAggregation,
  useB2cCourseGrouped,
  useB2cNewOld,
  useSalesContext,
} from '../lib/useSalesAggregation';
import { formatPeriod } from '../lib/format';
import {
  dateKey,
  monthKeyFor,
  operationalWeekKey,
  parseDateKey,
  previousPeriodKey,
  quarterOf,
  riyadhToday,
} from '../lib/dashboardPeriod';
import {
  B2bDashboard,
  B2cChartsGrid,
  B2cKpiCards,
  B2cPrevComparison,
  CoursesPerformanceTable,
  EmployeesPerformanceTable,
  NewOldSection,
} from '../components/salesDashboard';
import type { PeriodType } from '../types/api';

// لوحة مبيعات الفريق لقائد الفريق (RC-3 Task 1.1): عرض قراءة فقط لأداء فريقه فقط.
// نوع الفريق (B2C/B2B) يُحسَم خادميًّا عبر sales-context (المصدر الموثوق) ثم يُعرَض القسم المناسب فقط:
//   قائد فريق B2C ⇒ B2C فقط، قائد فريق B2B ⇒ B2B فقط، مختلط/إداري أعلى ⇒ كلاهما.
// النطاق (موظّفو الفريق فقط) مفروض خادميًّا في نقاط التجميع (ScopeResolver) — لا تسريب بيانات لا واجهيًّا ولا خادميًّا.

// أنواع الفترات المدعومة: يومي (أساس تخزين تقارير المبيعات) + تجميع أوسع يُشتقّ خادميًّا.
const PERIOD_TYPES: PeriodType[] = ['Daily', 'Weekly', 'Monthly', 'Quarterly'];
const PERIOD_TYPE_LABEL: Record<string, string> = {
  Daily: 'يومي',
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
};

export default function TeamLeaderSalesDashboardPage() {
  const { isSalesB2cTeamLeader } = useAuth();
  const today = useMemo(() => riyadhToday(), []);
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');

  // قيم منتقيات الفترة الخام (لا يرى المستخدم مفتاح الفترة النهائي — يُولَّد داخليًّا، لا صندوق نصّي يدوي).
  const [dailyDate, setDailyDate] = useState<string>(() => dateKey(today));
  const [weeklyDate, setWeeklyDate] = useState<string>(() => dateKey(today));
  const [monthValue, setMonthValue] = useState<string>(() => monthKeyFor(today));
  const [quarterYear, setQuarterYear] = useState<number>(() => today.getUTCFullYear());
  const [quarterNum, setQuarterNum] = useState<number>(() => quarterOf(today));

  const periodKey = useMemo(() => {
    switch (periodType) {
      case 'Daily':
        return dailyDate || undefined;
      case 'Weekly': {
        const d = parseDateKey(weeklyDate);
        return d ? operationalWeekKey(d) : undefined;
      }
      case 'Monthly':
        return monthValue || undefined;
      case 'Quarterly':
        return `${quarterYear}-Q${quarterNum}`;
      default:
        return undefined;
    }
  }, [periodType, dailyDate, weeklyDate, monthValue, quarterYear, quarterNum]);

  const filter = useMemo(() => ({ periodType, periodKey }), [periodType, periodKey]);
  const prevFilter = useMemo(
    () => ({ periodType, periodKey: previousPeriodKey(periodType, periodKey) }),
    [periodType, periodKey],
  );

  // سياق المبيعات الموثوق: يحدّد أيّ قسم يُعرَض (B2C/B2B) خادميًّا حسب مسمّى القائد ونطاق فريقه.
  const context = useSalesContext();
  const showB2c = context.data?.showB2c ?? false;
  const showB2b = context.data?.showB2b ?? false;
  const both = showB2c && showB2b;

  // B2C: يُجلب فقط عند الحاجة (enabled=showB2c) كي لا يُحمّل قائد B2B بيانات B2C.
  const grouped = useB2cCourseGrouped(filter, showB2c);
  const groupedPrev = useB2cCourseGrouped(prevFilter, showB2c);
  const newOld = useB2cNewOld(filter, showB2c);

  // B2B: يُجلب فقط عند الحاجة (enabled=showB2b).
  const b2b = useB2bAggregation(filter, showB2b);
  const b2bPrev = useB2bAggregation(prevFilter, showB2b);

  const courses = grouped.data?.courses ?? [];
  const b2bRows = b2b.data?.rows ?? [];
  const hasB2c = courses.length > 0;
  const hasB2b = b2bRows.length > 0;

  // حالات التحميل/الخطأ الموحّدة عبر الأقسام المفعّلة فقط.
  const isLoading =
    context.isLoading ||
    (showB2c && grouped.isLoading) ||
    (showB2b && b2b.isLoading);
  const isError =
    context.isError ||
    (showB2c && grouped.isError) ||
    (showB2b && b2b.isError);
  // «لا بيانات» = لا يوجد ما يُعرَض في أيٍّ من الأقسام المفعّلة.
  const hasData = (showB2c && hasB2c) || (showB2b && hasB2b);

  const retry = () => {
    if (context.isError) void context.refetch();
    if (showB2c && grouped.isError) void grouped.refetch();
    if (showB2b && b2b.isError) void b2b.refetch();
  };

  // حارس الوصول المباشر: هذه اللوحة حصرية لقائد فريق مبيعات B2C (TeamLeader + SALES_B2C_TL).
  // أيّ قائد فريق آخر (تنفيذ/سوشيال) يفتح الرابط مباشرة يُعاد توجيهه للرئيسية (بلا كشف أي محتوى).
  if (!isSalesB2cTeamLeader) return <Navigate to="/app" replace />;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">لوحة مبيعات الفريق</h1>
        <p className="mt-1 text-sm text-ink-2">
          {both
            ? 'أداء فريقك في المبيعات (B2C وB2B): أفضل الدورات/الخدمات، التحويلات، الإيراد، وساعات العمل — لتحديد نقاط القوّة وفرص التحسين.'
            : showB2b
            ? 'أداء فريقك في مبيعات B2B: أفضل الخدمات، القمع (Leads → Meetings → Proposals → Won)، الإيراد، وساعات العمل.'
            : 'أداء فريقك في مبيعات B2C: أفضل الدورات، البيانات الجديدة مقابل CRM القديمة، التحويلات، الإيراد، وساعات العمل.'}
        </p>
      </div>

      <Alert tone="navy">
        هذا العرض قراءة فقط ويلتزم بنطاق صلاحيتك كقائد فريق: لا يظهر فيه إلا موظّفو فريقك.
        الأرقام مأخوذة مباشرة من مدخلات الموظّفين في تقاريرهم المعتمَدة — لا حساب أو صرف لأي مستحقات.
        {both && ' يظهر لك قسما B2C وB2B لأن نطاق فريقك يشمل النوعين.'}
      </Alert>

      {/* منتقي الفترة (بلا صندوق مفتاح فترة يدوي) */}
      <Card>
        <div className="grid gap-3 md:grid-cols-3">
          <Field label="نوع الفترة">
            <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
              {PERIOD_TYPES.map((p) => (
                <option key={p} value={p}>{PERIOD_TYPE_LABEL[p] ?? p}</option>
              ))}
            </Select>
          </Field>

          {periodType === 'Daily' && (
            <Field label="اليوم">
              <Input type="date" value={dailyDate} onChange={(e) => setDailyDate(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Weekly' && (
            <Field label="الأسبوع" help={periodKey ? `الأسبوع المولّد: ${periodKey}` : 'اختر أي يوم داخل الأسبوع'}>
              <Input type="date" value={weeklyDate} onChange={(e) => setWeeklyDate(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Monthly' && (
            <Field label="الشهر">
              <Input type="month" value={monthValue} onChange={(e) => setMonthValue(e.target.value)} dir="ltr" />
            </Field>
          )}

          {periodType === 'Quarterly' && (
            <>
              <Field label="السنة">
                <Input
                  type="number"
                  value={String(quarterYear)}
                  onChange={(e) => setQuarterYear(Number(e.target.value) || quarterYear)}
                  min={2000}
                  max={3000}
                  dir="ltr"
                />
              </Field>
              <Field label="الربع">
                <Select value={String(quarterNum)} onChange={(e) => setQuarterNum(Number(e.target.value))}>
                  <option value="1">الربع 1</option>
                  <option value="2">الربع 2</option>
                  <option value="3">الربع 3</option>
                  <option value="4">الربع 4</option>
                </Select>
              </Field>
            </>
          )}
        </div>
      </Card>

      {isLoading ? (
        <LoadingState label="يتم تحميل لوحة مبيعات الفريق…" />
      ) : isError ? (
        <QueryError
          onRetry={retry}
          description="حدث خطأ أثناء جلب بيانات مبيعات فريقك. أعد المحاولة."
        />
      ) : !hasData ? (
        <Card className="p-5">
          <EmptyState
            title="لا توجد بيانات مبيعات لفريقك خلال هذه الفترة."
            description="جرّب تغيير نوع الفترة أو اختيار فترة أخرى تحتوي على تقارير مبيعات معتمَدة لموظّفي فريقك."
          />
        </Card>
      ) : (
        <div className="space-y-8">
          {/* ===== قسم B2C ===== */}
          {showB2c && (
            <section className="space-y-4">
              {both && (
                <div className="flex items-center gap-2">
                  <span className="inline-block h-4 w-1 rounded bg-orange" />
                  <h2 className="text-lg font-bold text-navy">مبيعات B2C</h2>
                </div>
              )}
              {!hasB2c ? (
                <Card className="p-5">
                  <EmptyState
                    title="لا توجد بيانات مبيعات B2C لفريقك خلال هذه الفترة."
                    description="جرّب فترة أخرى تحتوي على تقارير B2C معتمَدة لموظّفي فريقك."
                  />
                </Card>
              ) : (
                <>
                  <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
                    <Badge tone="navy">{formatPeriod(grouped.data?.periodKey)}</Badge>
                    <span>عدد الدورات: {courses.length.toLocaleString('ar-EG')}</span>
                  </div>

                  <B2cKpiCards courses={courses} />
                  <B2cPrevComparison courses={courses} prev={groupedPrev.data} />
                  {newOld.data && <NewOldSection report={newOld.data} />}
                  <B2cChartsGrid courses={courses} periodKey={grouped.data?.periodKey} newOld={newOld.data} />

                  <Card>
                    <SectionTitle title="أداء الدورات" hint="مرتّبة من الأفضل — اضغط على أي دورة لعرض مساهمات موظّفي فريقك." />
                  </Card>
                  <Card className="overflow-x-auto p-0">
                    <CoursesPerformanceTable courses={courses} />
                  </Card>

                  <Card>
                    <SectionTitle title="أداء موظّفي الفريق" hint="مرتّبين حسب الإيراد — اضغط على أي موظّف لعرض تفصيله." />
                  </Card>
                  <Card className="overflow-x-auto p-0">
                    <EmployeesPerformanceTable courses={courses} />
                  </Card>
                </>
              )}
            </section>
          )}

          {/* ===== قسم B2B ===== */}
          {showB2b && (
            <section className="space-y-4">
              {both && (
                <div className="flex items-center gap-2">
                  <span className="inline-block h-4 w-1 rounded bg-navy" />
                  <h2 className="text-lg font-bold text-navy">مبيعات B2B</h2>
                </div>
              )}
              {!hasB2b ? (
                <Card className="p-5">
                  <EmptyState
                    title="لا توجد بيانات مبيعات B2B لفريقك خلال هذه الفترة."
                    description="جرّب فترة أخرى تحتوي على تقارير B2B معتمَدة لموظّفي فريقك."
                  />
                </Card>
              ) : (
                <>
                  <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
                    <Badge tone="navy">{formatPeriod(b2b.data?.periodKey)}</Badge>
                    <span>عدد الصفوف: {b2bRows.length.toLocaleString('ar-EG')}</span>
                  </div>
                  <B2bDashboard rows={b2bRows} prev={b2bPrev.data} periodKey={b2b.data?.periodKey} />
                </>
              )}
            </section>
          )}
        </div>
      )}
    </div>
  );
}

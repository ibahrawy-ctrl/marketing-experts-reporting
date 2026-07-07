import { useMemo, useState } from 'react';
import { Alert, Badge, Card, EmptyState, Field, Input, Select } from '../components/ui';
import { SectionTitle } from '../components/dashboard';
import { LoadingState, QueryError } from '../components/states';
import { useAuth } from '../lib/auth';
import {
  useB2bAggregation,
  useB2cCourseGrouped,
  useB2cNewOld,
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
  NewOldSection,
  pctText,
  ratio,
  sumB2b,
  sumCourses,
} from '../components/salesDashboard';
import type { B2cCourseGroupRow, PeriodType } from '../types/api';

// لوحة مبيعاتي الشخصية (RC-3 Task 1.1): كل مندوب يرى بياناته هو فقط.
// النطاق مفروض خادميًّا (نطاق المندوب = نفسه فقط) + نُرسل employeeId=الذات صراحةً كتأكيد إضافي.
// المتغيّر (B2C/B2B) يُشتقّ من مسمّى المندوب (salesRepType). الأدمن يُسمح له للاطّلاع (افتراضي B2C).
// لا ذكاء اصطناعي — الملاحظات النصّية («أقوى دورة»/«أضعف نقطة تحويل») مشتقّة حسابيًّا من البيانات فقط.

const PERIOD_TYPES: PeriodType[] = ['Daily', 'Weekly', 'Monthly', 'Quarterly'];
const PERIOD_TYPE_LABEL: Record<string, string> = {
  Daily: 'يومي',
  Weekly: 'أسبوعي',
  Monthly: 'شهري',
  Quarterly: 'ربع سنوي',
};

export default function SalesRepDashboardPage() {
  const { user, isSalesRep, salesRepType, hasAnyRole } = useAuth();
  const isAdmin = hasAnyRole('Admin');

  const today = useMemo(() => riyadhToday(), []);
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');

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

  // متغيّر اللوحة: مسمّى المندوب أولًا؛ الأدمن غير المندوب يُعرَض له متغيّر B2C للاطّلاع.
  const repType: 'B2C' | 'B2B' = salesRepType ?? 'B2C';

  // employeeId=الذات: تأكيد صريح أنّ اللوحة شخصية (مع فرض النطاق الخادمي أصلًا على المندوب).
  const selfId = user?.userId;
  const filter = useMemo(
    () => ({ periodType, periodKey, employeeId: selfId }),
    [periodType, periodKey, selfId],
  );
  const prevFilter = useMemo(
    () => ({ periodType, periodKey: previousPeriodKey(periodType, periodKey), employeeId: selfId }),
    [periodType, periodKey, selfId],
  );

  const wantB2c = repType === 'B2C';
  const wantB2b = repType === 'B2B';

  const grouped = useB2cCourseGrouped(filter, wantB2c);
  const groupedPrev = useB2cCourseGrouped(prevFilter, wantB2c);
  const newOld = useB2cNewOld(filter, wantB2c);
  const b2b = useB2bAggregation(filter, wantB2b);
  const b2bPrev = useB2bAggregation(prevFilter, wantB2b);

  // حارس الوصول الواجهي: غير المندوب وغير الأدمن لا يرى هذه اللوحة (النطاق الخادمي حارس ثانٍ).
  if (!isSalesRep && !isAdmin) {
    return (
      <Card className="p-5">
        <EmptyState
          title="هذه اللوحة مخصّصة لمندوبي المبيعات."
          description="لوحة «مبيعاتي» تعرض أداءك الشخصي كمندوب مبيعات. إن كنت تعتقد أن هذا خطأ، تواصل مع الإدارة."
        />
      </Card>
    );
  }

  const courses = grouped.data?.courses ?? [];
  const b2bRows = b2b.data?.rows ?? [];
  const hasData = wantB2c ? courses.length > 0 : b2bRows.length > 0;

  const isLoading = wantB2c ? grouped.isLoading : b2b.isLoading;
  const isError = wantB2c ? grouped.isError : b2b.isError;
  const retry = () => (wantB2c ? void grouped.refetch() : void b2b.refetch());

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">لوحة مبيعاتي</h1>
        <p className="mt-1 text-sm text-ink-2">
          {wantB2c
            ? 'أداؤك الشخصي في مبيعات B2C: Leads، التحويلات، الإيراد، ساعات العمل، وأفضل دوراتك — بياناتك أنت فقط.'
            : 'أداؤك الشخصي في مبيعات B2B: Leads، الاجتماعات، العروض، الصفقات المكسوبة، والإيراد — بياناتك أنت فقط.'}
        </p>
      </div>

      <Alert tone="navy">
        هذا العرض قراءة فقط ويعرض بياناتك الشخصية فقط — لا يظهر أداء زملائك.
        الأرقام مأخوذة مباشرة من مدخلاتك في تقاريرك المعتمَدة — لا حساب أو صرف لأي مستحقات.
        {!isSalesRep && isAdmin && ' (تعرض هذه اللوحة حاليًا بصفتك مدير النظام للاطّلاع.)'}
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
        <LoadingState label="يتم تحميل لوحة مبيعاتك…" />
      ) : isError ? (
        <QueryError onRetry={retry} description="حدث خطأ أثناء جلب بيانات مبيعاتك. أعد المحاولة." />
      ) : !hasData ? (
        <Card className="p-5">
          <EmptyState
            title="لا توجد بيانات مبيعات لك خلال هذه الفترة."
            description="جرّب تغيير نوع الفترة أو اختيار فترة أخرى تحتوي على تقارير مبيعات معتمَدة لك."
          />
        </Card>
      ) : wantB2c ? (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
            <Badge tone="navy">{formatPeriod(grouped.data?.periodKey)}</Badge>
            <span>عدد دوراتك: {courses.length.toLocaleString('ar-EG')}</span>
          </div>

          <B2cKpiCards courses={courses} />
          <B2cInsights courses={courses} />
          <B2cPrevComparison courses={courses} prev={groupedPrev.data} />
          {newOld.data && <NewOldSection report={newOld.data} />}
          <B2cChartsGrid courses={courses} periodKey={grouped.data?.periodKey} newOld={newOld.data} showEmployees={false} />

          <Card>
            <SectionTitle title="أداء دوراتك" hint="مرتّبة من الأفضل — اضغط على أي دورة لعرض تفصيلها (الكلّي / بيانات جديدة / CRM قديمة)." />
          </Card>
          <Card className="overflow-x-auto p-0">
            <CoursesPerformanceTable courses={courses} />
          </Card>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
            <Badge tone="navy">{formatPeriod(b2b.data?.periodKey)}</Badge>
            <span>عدد الصفوف: {b2bRows.length.toLocaleString('ar-EG')}</span>
          </div>
          <B2bInsights rows={b2bRows} />
          <B2bDashboard rows={b2bRows} prev={b2bPrev.data} periodKey={b2b.data?.periodKey} showEmployees={false} />
        </div>
      )}
    </div>
  );
}

// ===== ملاحظات نصّية مشتقّة حسابيًّا (لا ذكاء اصطناعي) =====

// أقوى دورة = الأعلى إيرادًا. أضعف نقطة تحويل = مرحلة القمع صاحبة أدنى معدّل انتقال.
function B2cInsights({ courses }: { courses: B2cCourseGroupRow[] }) {
  const t = sumCourses(courses);
  const best = [...courses].sort((a, b) => b.revenue - a.revenue)[0];

  // مراحل القمع الثلاث: Lead→Contacted، Contacted→Qualified، Qualified→Sales.
  const stages = [
    { label: 'التواصل مع Leads (Lead ← Contacted)', rate: ratio(t.contacted, t.leads), has: t.leads > 0 },
    { label: 'تأهيل المتواصَل معهم (Contacted ← Qualified)', rate: ratio(t.qualified, t.contacted), has: t.contacted > 0 },
    { label: 'إغلاق المؤهَّلين (Qualified ← Sales)', rate: ratio(t.sales, t.qualified), has: t.qualified > 0 },
  ].filter((s) => s.has);
  const weakest = stages.length > 0 ? [...stages].sort((a, b) => a.rate - b.rate)[0] : undefined;

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <Card className="border-r-4 border-r-green-600">
        <div className="text-xs text-ink-2">أقوى دورة لديك</div>
        {best && best.revenue > 0 ? (
          <>
            <div className="mt-1 text-lg font-bold text-navy">{best.course}</div>
            <div className="mt-0.5 text-sm text-ink-2">
              الإيراد: {best.revenue.toLocaleString('ar-EG', { maximumFractionDigits: 2 })} — المبيعات: {best.sales.toLocaleString('ar-EG')}
            </div>
          </>
        ) : (
          <div className="mt-1 text-sm text-ink-2">لا توجد دورة ذات إيراد بعد في هذه الفترة.</div>
        )}
      </Card>
      <Card className="border-r-4 border-r-orange">
        <div className="text-xs text-ink-2">أضعف نقطة تحويل لديك</div>
        {weakest ? (
          <>
            <div className="mt-1 text-lg font-bold text-navy">{weakest.label}</div>
            <div className="mt-0.5 text-sm text-ink-2">
              معدّل الانتقال: {pctText(weakest.rate)} — ركّز على تحسين هذه المرحلة لرفع مبيعاتك.
            </div>
          </>
        ) : (
          <div className="mt-1 text-sm text-ink-2">لا توجد بيانات كافية لتحديد نقطة التحويل الأضعف.</div>
        )}
      </Card>
    </div>
  );
}

// نظير B2B: أقوى خدمة (أعلى إيرادًا) + أضعف نقطة تحويل في قمع B2B.
function B2bInsights({ rows }: { rows: import('../types/api').B2bServiceAggregateRow[] }) {
  const t = sumB2b(rows);
  // تجميع الإيراد حسب الخدمة لتحديد الأقوى.
  const byService = new Map<string, number>();
  for (const r of rows) {
    const key = r.service?.trim() || '—';
    byService.set(key, (byService.get(key) ?? 0) + r.revenue);
  }
  const bestService = [...byService.entries()].sort((a, b) => b[1] - a[1])[0];

  const stages = [
    { label: 'حجز الاجتماعات (Leads ← Meetings)', rate: ratio(t.meetings, t.leads), has: t.leads > 0 },
    { label: 'تقديم العروض (Meetings ← Proposals)', rate: ratio(t.proposals, t.meetings), has: t.meetings > 0 },
    { label: 'إغلاق الصفقات (Proposals ← Won)', rate: ratio(t.won, t.proposals), has: t.proposals > 0 },
  ].filter((s) => s.has);
  const weakest = stages.length > 0 ? [...stages].sort((a, b) => a.rate - b.rate)[0] : undefined;

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <Card className="border-r-4 border-r-green-600">
        <div className="text-xs text-ink-2">أقوى خدمة لديك</div>
        {bestService && bestService[1] > 0 ? (
          <>
            <div className="mt-1 text-lg font-bold text-navy">{bestService[0]}</div>
            <div className="mt-0.5 text-sm text-ink-2">
              الإيراد: {bestService[1].toLocaleString('ar-EG', { maximumFractionDigits: 2 })}
            </div>
          </>
        ) : (
          <div className="mt-1 text-sm text-ink-2">لا توجد خدمة ذات إيراد بعد في هذه الفترة.</div>
        )}
      </Card>
      <Card className="border-r-4 border-r-orange">
        <div className="text-xs text-ink-2">أضعف نقطة تحويل لديك</div>
        {weakest ? (
          <>
            <div className="mt-1 text-lg font-bold text-navy">{weakest.label}</div>
            <div className="mt-0.5 text-sm text-ink-2">
              معدّل الانتقال: {pctText(weakest.rate)} — ركّز على تحسين هذه المرحلة لرفع صفقاتك.
            </div>
          </>
        ) : (
          <div className="mt-1 text-sm text-ink-2">لا توجد بيانات كافية لتحديد نقطة التحويل الأضعف.</div>
        )}
      </Card>
    </div>
  );
}

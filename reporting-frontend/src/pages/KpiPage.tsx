import { useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage, approvalErrorMessage } from '../lib/api';
import { useToast, POST_SUCCESS_NAV_DELAY_MS } from '../components/ActionResultToast';
import { useAuth } from '../lib/auth';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { KpiOverview } from './KpiOverview';
import { ManagementNotesPanel } from '../components/ManagementNotesPanel';
import {
  kpiEvaluationStatusLabel,
  kpiTrendDisplay,
  periodTypeLabel,
  formatPeriod,
} from '../lib/format';
import type {
  EvaluatableSubjectsDto,
  KpiCalcMethod,
  KpiEvaluationDto,
  KpiEvaluationListItemDto,
  KpiEvaluationLookupDto,
  KpiEvaluationReviewEventDto,
  KpiEvaluationSetupDto,
  KpiCadence,
  KpiCadenceSource,
  KpiResultDto,
} from '../types/api';
import { WeeklyCycleCalendarPicker } from '../components/WeeklyCycleCalendarPicker';

// توضيح طريقة الإدخال لكل نوع مؤشّر داخل شاشة التقييم — يحدّد للمستخدم الحقل المعتمد في الحساب.
const calcMethodHint: Record<KpiCalcMethod, string> = {
  Auto: 'تلقائي — أدخل القيمة الفعلية، وسيحسب النظام الدرجة من المستهدف.',
  Manual: 'يدوي — أدخل الدرجة مباشرة من 0 إلى 100.',
  Hybrid: 'مزيج — إن أدخلت الدرجة اليدوية فهي المعتمدة، وإلا تُحتسب من القيمة الفعلية.',
};

// DEC-01/5 (DEF-R5-001) — مصدر حسم التواتر كما يعلنه الخادم: يُعرض للمستخدم شرحًا لا خيارًا.
const cadenceSourceLabel: Record<KpiCadenceSource, string> = {
  employeeAssignment: 'إسناد خاصّ بهذا الموظّف',
  teamAssignment: 'إسناد فريقه',
  jobRole: 'مسمّاه الوظيفيّ',
  departmentAssignment: 'إسناد إدارته',
  generalTemplate: 'الإعداد العامّ',
  notConfigured: 'غير مُهيّأ',
  explicitRequest: 'طلب صريح',
};

// OBS-R5-01 — المساران متزامنان: لكلٍّ اسمه المعروض وإجراؤه المستحقّ. لا منتقي cadence تقنيّ
// في الواجهة: المستخدم يضغط الإجراء المستحقّ نفسه، والمسار الآخر يبقى ظاهرًا ومتاحًا دائمًا.
const cadenceTrackLabel: Record<KpiCadence, string> = {
  WeeklyPulse: 'نبض الأسبوع',
  Quarterly: 'التقييم الربعيّ الرسميّ',
};
const cadenceActionLabel: Record<KpiCadence, string> = {
  WeeklyPulse: 'تسجيل نبض الأسبوع الحالي',
  Quarterly: 'إجراء التقييم الربعي الرسمي',
};

// عناوين عربية لأحداث سجلّ المراجعة (ADMIN-GOVERNANCE-R1).
const reviewActionLabel: Record<string, string> = {
  Submitted: 'إرسال للمراجعة',
  Approved: 'اعتماد',
  RequestRevision: 'طلب تعديل',
  Reject: 'رفض نهائيّ',
  Comment: 'تعليق مراجعة',
  Flag: 'إشارة للمراجعة',
  RequestReopen: 'طلب إعادة فتح',
  Reopen: 'إعادة فتح',
  AdminDeleted: 'حذف إداريّ',
};

export default function KpiPage() {
  const { hasAnyRole } = useAuth();
  const isManagement = hasAnyRole('Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader');
  // الفتح المباشر لتقييم محدّد عبر ?open=<id> (مثل رابط «عرض» من صفحة الموظّف) — مصدر الحقيقة هو رابط العنوان.
  const [params, setParams] = useSearchParams();
  const openId = params.get('open');
  // ?subject=<userId> يحصر القائمة بتقييمات موظّف واحد (الدخول من صفحة الموظّف) — يُفرَض النطاق والصلاحية خادميًّا أيضًا.
  const subject = params.get('subject');
  const closeDetail = () => {
    const next = new URLSearchParams(params);
    next.delete('open');
    setParams(next, { replace: true });
  };
  const openDetail = (id: string) => {
    const next = new URLSearchParams(params);
    next.set('open', id);
    setParams(next);
  };
  const [tab, setTab] = useState<'overview' | 'evaluations'>(isManagement ? 'overview' : 'evaluations');

  if (openId) return <KpiDetail id={openId} onBack={closeDetail} />;

  // عرض مخصّص لموظّف واحد: لا يعرض تقييمات كل الشركة، بل تقييمات هذا الموظّف فقط ضمن نطاق المستخدم.
  if (isManagement && subject)
    return <KpiList isManagement={isManagement} onOpen={openDetail} subjectFilter={subject} />;

  if (!isManagement) return <KpiList isManagement={isManagement} onOpen={openDetail} />;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">مؤشرات الأداء KPI</h1>
          <p className="mt-1 text-sm text-ink-2">نظرة متعددة المستويات على الأداء، وإدارة تقييمات الموظفين.</p>
        </div>
        <div className="flex gap-2">
          <Button variant={tab === 'overview' ? 'primary' : 'ghost'} onClick={() => setTab('overview')}>
            نظرة شاملة
          </Button>
          <Button variant={tab === 'evaluations' ? 'primary' : 'ghost'} onClick={() => setTab('evaluations')}>
            التقييمات
          </Button>
        </div>
      </div>
      <Alert tone="navy">
        «نظرة شاملة» تعرض مؤشّر الأداء حسب الإدارة والفريق ضمن نطاقك فقط (تجميع للقراءة). «التقييمات» لإنشاء
        تقييم لمرؤوسيك المباشرين ومتابعته. كل المصادر محصورة بنطاقك خادميًّا — لا تظهر بيانات خارج صلاحيتك.
      </Alert>
      {tab === 'overview' ? <KpiOverview /> : <KpiList isManagement={isManagement} onOpen={openDetail} hideTitle />}
    </div>
  );
}

function KpiList({ isManagement, onOpen, hideTitle, subjectFilter }: { isManagement: boolean; onOpen: (id: string) => void; hideTitle?: boolean; subjectFilter?: string | null }) {
  const qc = useQueryClient();
  const [kpiTemplateId, setKpiTemplateId] = useState('');
  const [subjectUserId, setSubjectUserId] = useState('');
  // KPI-REVIEWER-OVERRIDE-R1: الفلترة تتبع اختيار النموذج فعليًّا — عند اختيار موظّف في النموذج
  // تُحصر القائمة بتقييماته وحده. subjectFilter (من ?subject=) يبقى له الأسبقية عند الدخول من صفحة الموظّف.
  const listSubjectId = subjectFilter || subjectUserId || null;
  // النطاق والصلاحية يُفرَضان خادميًّا في كل الأحوال.
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-evaluations', listSubjectId ?? 'all'],
    queryFn: async () =>
      (await api.get<KpiEvaluationListItemDto[]>(
        '/kpi-evaluations',
        listSubjectId ? { params: { subjectUserId: listSubjectId } } : undefined,
      )).data,
  });
  // كل العناصر تخصّ الموظّف نفسه عند الحصر، فنشتقّ اسمه من أوّل عنصر لعرضه في الشريط التوضيحي.
  const subjectName = items?.[0]?.subjectName;
  // فارغ في البداية — منتقي التقويم (أسبوعيّ) أو مفتاح الربع الجاري (ربعيّ) يملؤه من حسم الخادم.
  const [periodKey, setPeriodKey] = useState('');
  const [err, setErr] = useState<string | null>(null);

  // نطاق إنشاء التقييم أضيق من نطاق العرض: لا تظهر إلا أسماء المرؤوسين المباشرين
  // للمستخدم الحالي (أو كل الموظّفين إن كان أدمن «وضع إداري»). يُحسم خادميًّا أيضًا.
  const { data: evaluatable } = useQuery({
    queryKey: ['kpi-evaluatable-subjects'],
    queryFn: async () =>
      (await api.get<EvaluatableSubjectsDto>('/kpi-evaluations/evaluatable-subjects')).data,
    enabled: isManagement,
  });
  const subjects = evaluatable?.subjects ?? [];

  // DEF-R5-001 + OBS-R5-01 — «الإعداد الفعّال» يُحسم خادميًّا بالكامل ويُعاد **مسارَين معًا**:
  // نبض الأسبوع والتقييم الربعيّ الرسميّ. لكلّ مسار مصدر حسمه (سلّم DEC-01/5 مُطبَّقًا داخله وحده)
  // ونوع فترته ومفتاح فترته الجارية بتوقيت الرياض وقوالبه المؤهَّلة وحالته المسمّاة عند غياب التهيئة.
  // الواجهة لا تثبّت تواترًا ولا تعرض منتقيًا تقنيًّا له، ولا تُخفي مسارًا بسبب غياب الآخر.
  const { data: setup, isFetching: setupLoading } = useQuery({
    queryKey: ['kpi-evaluation-setup', subjectUserId],
    queryFn: async () =>
      (await api.get<KpiEvaluationSetupDto>('/kpi-evaluations/effective-setup', {
        params: { subjectUserId },
      })).data,
    enabled: isManagement && !!subjectUserId,
  });
  const tracks = setup?.tracks ?? [];
  const configuredTracks = tracks.filter((t) => t.isConfigured);
  // المسار الذي يعمل عليه المُقيّم الآن: يُختار بالضغط على الإجراء المستحقّ نفسه لا من قائمة تقنيّة.
  // وإن كان مسار واحد فقط مُهيّأً فهو النشط تلقائيًّا — بلا خطوة زائدة وبلا إخفاء للمسار الآخر.
  const [trackCadence, setTrackCadence] = useState<KpiCadence | ''>('');
  const activeTrack =
    configuredTracks.find((t) => t.cadence === trackCadence)
    ?? (configuredTracks.length === 1 ? configuredTracks[0] : null);

  const templates = activeTrack?.templates;
  const periodType = activeTrack?.periodType ?? null;
  const isQuarterly = activeTrack?.cadence === 'Quarterly';

  // عند تغيير الموظّف نُفرّغ المسار والقالب والفترة: الثلاثة مشتقّة من إعداد الموظّف المختار.
  const onSubjectChange = (id: string) => {
    setSubjectUserId(id);
    setTrackCadence('');
    setKpiTemplateId('');
    setPeriodKey('');
    setErr(null);
  };

  // تبديل المسار لا يحمل معه قالب المسار الآخر ولا فترته — منعًا لأيّ خلط بين المسارين.
  const onTrackChange = (cadence: KpiCadence) => {
    setTrackCadence(cadence);
    setKpiTemplateId('');
    setPeriodKey('');
    setErr(null);
  };

  // الخادم يُطبّق أولوية اختيار القالب: الأخصّ يطغى داخل مسار الموظّف وحده.
  // فإن أُرجِع قالب واحد فقط نختاره تلقائيًّا (قيمة مُشتقّة) لتبسيط الإنشاء وتأكيد أنه القالب المناسب.
  const effectiveTemplateId =
    (kpiTemplateId && templates?.some((t) => t.id === kpiTemplateId) ? kpiTemplateId : '')
    || (templates?.length === 1 ? templates[0].id : '');

  // DEC-01/1 — الفترة الربعيّة لا تُختار: الربع الجاري محسوم خادميًّا. الأسبوعيّة تبقى بمنتقي الدورة.
  const effectivePeriodKey = isQuarterly ? (activeTrack?.currentPeriodKey ?? '') : periodKey;
  const canCreate = !!activeTrack && !!effectiveTemplateId && !!effectivePeriodKey && !!periodType;

  // KPI-REVIEWER-OVERRIDE-R1: بحث قرائيّ صرف عن تقييم قائم لهذا (الموظّف + القالب + الفترة) قبل الإنشاء.
  // لا ينشئ سجلًّا ولا يعدّل شيئًا، ويمنع ازدواج التقييم ويُظهر التقييم التاريخيّ للاطّلاع.
  const { data: lookup, isFetching: lookupLoading } = useQuery({
    queryKey: ['kpi-evaluation-lookup', subjectUserId, effectiveTemplateId, effectivePeriodKey],
    queryFn: async () =>
      (await api.get<KpiEvaluationLookupDto>('/kpi-evaluations/lookup', {
        params: { subjectUserId, kpiTemplateId: effectiveTemplateId, periodKey: effectivePeriodKey },
      })).data,
    enabled: isManagement && !!subjectUserId && !!effectiveTemplateId && !!effectivePeriodKey,
  });
  const existingEvaluation = lookup?.found ? lookup.evaluation : null;

  const create = useMutation({
    // نوع الفترة يأتي من حسم الخادم لا من ثابت في الواجهة، والخادم يعيد التحقّق منه على أيّ حال.
    mutationFn: () => api.post<KpiEvaluationDto>('/kpi-evaluations', {
      kpiTemplateId: effectiveTemplateId, subjectUserId, periodType, periodKey: effectivePeriodKey,
    }),
    onSuccess: (res) => {
      setSubjectUserId('');
      setKpiTemplateId('');
      setPeriodKey('');
      void qc.invalidateQueries({ queryKey: ['kpi-evaluations'] });
      onOpen(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل تقييمات الأداء…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب تقييمات الأداء. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      {!hideTitle && <h1 className="text-2xl font-bold text-navy">مؤشرات الأداء</h1>}
      {subjectFilter && (
        <Alert tone="navy">
          تعرض هذه الصفحة تقييمات الأداء الخاصّة بـ
          <span className="font-semibold">{subjectName ? ` «${subjectName}» ` : ' هذا الموظّف '}</span>
          فقط، ضمن نطاق صلاحيتك.{' '}
          <Link to="/app/kpi" className="font-semibold text-orange-600 hover:underline">عرض كل التقييمات</Link>
        </Alert>
      )}
      {isManagement && (
        <Card>
          <div className="mb-3">
            <Alert tone="navy">
              قالب التقييم = مجموعة المؤشّرات وأوزانها لمسمّى وظيفي معيّن. اختر الموظّف أولًا لتظهر القوالب
              المناسبة لدوره فقط. طريقة احتساب كل مؤشّر: «يدوي» يُدخله المُقيّم، «تلقائي» يُحتسب من التقارير،
              و«مزيج» يجمع بينهما.
            </Alert>
          </div>
          {evaluatable?.isAdminOverride && (
            <div className="mb-3 flex">
              <Badge tone="gold">وضع إداري — يمكن اختيار أي موظّف</Badge>
            </div>
          )}
          {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
          {subjects.length === 0 ? (
            <Alert tone="navy">لا يوجد موظفون ضمن نطاق تقييمك المباشر.</Alert>
          ) : (
          <div className="space-y-3">
            <div className="flex flex-wrap items-end gap-3">
              <div className="w-56">
                <Field label="الموظف المراد تقييمه">
                  <Select value={subjectUserId} onChange={(e) => onSubjectChange(e.target.value)}>
                    <option value="">اختر الموظف…</option>
                    {subjects.map((s) => (
                      <option key={s.id} value={s.id}>{s.fullName}</option>
                    ))}
                  </Select>
                </Field>
                <p className="mt-1 text-xs text-ink-3">تظهر هنا فقط الأسماء التي يحق لك تقييمها حسب الهيكل الإداري.</p>
              </div>
            </div>

            {/* OBS-R5-01/4 — إجراءات مستحقّة لا منتقي cadence تقنيّ: يُعرض المساران معًا دائمًا،
                كلٌّ بمصدر حسمه وفترته الجارية، والمسار غير المُهيّأ يُعلن سببه ولا يُخفي الآخر. */}
            {subjectUserId && setup && (
              <div className="grid gap-3 sm:grid-cols-2">
                {setup.tracks.map((t) => (
                  <div
                    key={t.cadence}
                    data-testid={`kpi-track-${t.cadence}`}
                    className={`rounded-lg border p-3 ${
                      activeTrack?.cadence === t.cadence ? 'border-orange-400 bg-orange-50' : 'border-line bg-offwhite'
                    }`}
                  >
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone={t.cadence === 'Quarterly' ? 'gold' : 'navy'}>{cadenceTrackLabel[t.cadence]}</Badge>
                      {t.isConfigured ? (
                        <span className="text-xs text-ink-3">{formatPeriod(t.currentPeriodKey)}</span>
                      ) : (
                        <Badge tone="alert">غير مُهيّأ</Badge>
                      )}
                    </div>
                    <p className="mt-1 text-xs text-ink-3">
                      {t.isConfigured
                        ? `يحدّده النظام من ${cadenceSourceLabel[t.cadenceSource]} — لا يُختار يدويًّا.`
                        : t.blockingReason}
                    </p>
                    <div className="mt-2">
                      <Button
                        variant={activeTrack?.cadence === t.cadence ? 'primary' : 'ghost'}
                        disabled={!t.isConfigured}
                        onClick={() => onTrackChange(t.cadence)}
                      >
                        {cadenceActionLabel[t.cadence]}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div className="w-56">
              {/* القوالب تخصّ المسار النشط وحده — لا تُخلَط قوالب المسارين في قائمة واحدة. */}
              <Field label="قالب التقييم">
                <Select value={effectiveTemplateId} onChange={(e) => setKpiTemplateId(e.target.value)} disabled={!activeTrack}>
                  <option value="">
                    {!subjectUserId ? 'اختر الموظف أولًا' : !activeTrack ? 'اختر الإجراء المستحقّ أولًا' : 'اختر قالبًا…'}
                  </option>
                  {templates?.map((t) => (
                    <option key={t.id} value={t.id}>{t.name}</option>
                  ))}
                </Select>
              </Field>
              <p className="mt-1 text-xs text-ink-3">
                {activeTrack
                  ? `تظهر هنا قوالب «${cadenceTrackLabel[activeTrack.cadence]}» الفعّالة لهذا الموظّف وحدها.`
                  : 'تظهر هنا قوالب KPI الفعّالة لهذا الموظّف ضمن مساره فقط.'}
              </p>
            </div>

            <div className="max-w-md">
              {/* أسبوعيّ: منتقي الدورة المُدرِك للدور. ربعيّ: الربع الجاري محسوم خادميًّا (DEC-01/1). */}
              {periodType === 'Quarterly' ? (
                <Field label="الفترة (الربع الجاري)">
                  <div className="flex h-10 items-center rounded-lg border border-line bg-offwhite px-3">
                    <Badge tone="navy">{formatPeriod(effectivePeriodKey)}</Badge>
                  </div>
                </Field>
              ) : periodType === 'Weekly' ? (
                <Field label="الفترة (أسبوع)">
                  <WeeklyCycleCalendarPicker
                    context="Kpi"
                    value={periodKey || null}
                    onChange={(key) => { setErr(null); setPeriodKey(key); }}
                  />
                </Field>
              ) : null}
              {existingEvaluation && (
                <div className="mt-3">
                  <Alert tone="gold">
                    يوجد تقييم قائم لهذا الموظّف في هذه الدورة ({formatPeriod(existingEvaluation.periodKey)}):{' '}
                    النتيجة <span className="font-semibold">{existingEvaluation.totalScore ?? 'لم تُحتسب'}</span>{' '}
                    والحالة <span className="font-semibold">{kpiEvaluationStatusLabel[existingEvaluation.status]}</span>.
                    لن يُنشأ تقييم جديد — افتح التقييم القائم للاطّلاع أو الاستكمال.
                  </Alert>
                </div>
              )}
              <div className="mt-3">
                <Button
                  disabled={!canCreate || create.isPending || lookupLoading || setupLoading}
                  onClick={() => {
                    setErr(null);
                    if (existingEvaluation) { onOpen(existingEvaluation.id); return; }
                    create.mutate();
                  }}
                >
                  {existingEvaluation ? 'فتح التقييم القائم' : 'إنشاء تقييم'}
                </Button>
              </div>
            </div>
          </div>
          )}
          {/* حالة مسمّاة لا صمت (DEC-01/5): سبب المنع كما يعلنه الخادم، ولا يُرسَل طلب إنشاء أصلًا. */}
          {subjectUserId && setup && !setup.isConfigured && (
            <div className="mt-3">
              <Alert tone="gold">{setup.blockingReason}</Alert>
            </div>
          )}
        </Card>
      )}
      <Card>
        {!items?.length ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد تقييمات أداء بعد.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              تظهر هنا تقييمات مؤشّرات الأداء (KPI) الخاصة بك بمجرّد أن يُنشئها مديرك المباشر. تُنشأ التقييمات من قِبل الإدارة اعتمادًا على قوالب المؤشّرات المنشورة.
            </p>
          </div>
        ) : (
          // جدول سبعة أعمدة عرضه الأدنى يتجاوز 390px متى حوى اسم قالب أو موظّف طويلًا،
          // فيدفع الصفحة كلّها إلى تمرير أفقيّ. الحاوية القياسيّة في المستودع تحصر التمرير داخله.
          <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-right text-ink-2">
                <th className="pb-2">القالب</th>
                <th className="pb-2">الموظف</th>
                <th className="pb-2">الفترة</th>
                <th className="pb-2">النتيجة</th>
                <th className="pb-2">الاتجاه</th>
                <th className="pb-2">الحالة</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((e) => (
                <tr key={e.id} className="border-t border-line">
                  <td className="py-2">{e.templateTitle}</td>
                  <td className="py-2">
                    <Link className="text-orange-600 hover:underline" to={`/app/employees/${e.subjectUserId}/kpi`}>
                      {e.subjectName}
                    </Link>
                  </td>
                  <td className="py-2">{formatPeriod(e.periodKey)}</td>
                  <td className="py-2 font-semibold">{e.totalScore ?? <span className="font-normal text-ink-2" title="لم تُحتسب الدرجة بعد">لم تُحتسب</span>}</td>
                  <td className="py-2">{kpiTrendDisplay(e.trend, e.totalScore != null)}</td>
                  <td className="py-2"><Badge tone="navy">{kpiEvaluationStatusLabel[e.status]}</Badge></td>
                  <td className="py-2 text-left">
                    <Button variant="ghost" onClick={() => onOpen(e.id)}>عرض</Button>
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

function KpiDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const { data: ev, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-evaluation', id],
    queryFn: async () => (await api.get<KpiEvaluationDto>(`/kpi-evaluations/${id}`)).data,
  });
  const [draft, setDraft] = useState<Record<string, { rawValue: string; score: string; note: string }>>({});
  const [dirty, setDirty] = useState(false);

  // تحويل المسودة الحالية إلى حمولة الحفظ. الحقل الفارغ يُرسَل null (مسح متعمَّد) بدل تجاهله.
  const buildPayload = (results: KpiResultDto[]) => ({
    results: results.map((r) => {
      const d = draft[r.kpiMetricId];
      const num = (v: string | undefined, fallback: number | null) =>
        v === undefined ? fallback : v.trim() === '' ? null : Number(v);
      return {
        kpiMetricId: r.kpiMetricId,
        rawValue: num(d?.rawValue, r.rawValue),
        score: num(d?.score, r.score),
        note: d?.note ?? r.note,
      };
    }),
  });

  // APPROVAL ACTION UX R1: يُرجِع Promise حتى يُنتظَر تحديث القوائم/العدّادات قبل إظهار Toast والرجوع للقائمة.
  const invalidateAll = () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] }),
      qc.invalidateQueries({ queryKey: ['kpi-evaluations'] }),
      qc.invalidateQueries({ queryKey: ['kpi-review-events', id] }),
    ]);

  // APPROVAL ACTION UX R1: أخطاء الطلبات عبر Toast فقط. الحفظ/الإرسال صامتان في onSuccess (الـToast يظهر في المُعالِج).
  const save = useMutation({
    mutationFn: (results: KpiResultDto[]) => api.put(`/kpi-evaluations/${id}/results`, buildPayload(results)),
    onSuccess: () => { setDirty(false); void qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] }); },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  const submit = useMutation({
    mutationFn: () => api.post(`/kpi-evaluations/${id}/submit`),
    onSuccess: () => {
      setDirty(false);
      void qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] });
      void qc.invalidateQueries({ queryKey: ['kpi-evaluations'] });
    },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // إصلاح بَغ النتيجة صفر: عند الإرسال نحفظ المسودة أولًا إن وُجدت تغييرات غير محفوظة،
  // فلا يُحتسب التقييم على نتائج فارغة. إن فشل الحفظ لا نُرسِل (يبقى قابلًا للتحرير).
  const saveThenSubmit = async (results: KpiResultDto[]) => {
    if (save.isPending || submit.isPending) return;
    try {
      if (dirty) await save.mutateAsync(results);
      await submit.mutateAsync();
      toast.success('✅ تم إرسال التقييم');
    } catch {
      /* الخطأ يظهر عبر Toast من onError؛ لا نتابع الإرسال */
    }
  };

  // اعتماد نهائيّ للتقييم: Toast نجاح ⟵ تحديث القوائم ⟵ رجوع للقائمة.
  const approve = useMutation({
    mutationFn: () => api.post(`/kpi-evaluations/${id}/approve`),
    onSuccess: async () => {
      await invalidateAll();
      toast.success('✅ تم اعتماد تقييم KPI بنجاح');
      setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
    },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // القرارات النهائية (رفض/طلب تعديل/إعادة فتح) ترجع للقائمة؛ البقية (إشارة/طلب فتح/تعليق) تبقى في الصفحة.
  const reviewSuccessLabels: Record<string, string> = {
    'request-revision': '✅ تم إرسال طلب التعديل',
    reject: '✅ تم رفض التقييم',
    flag: '✅ تم وضع إشارة للمراجعة',
    'request-reopen': '✅ تم إرسال طلب إعادة الفتح',
    comment: '✅ تم حفظ تعليق المراجعة',
    reopen: '✅ تم إعادة فتح التقييم',
    'admin-delete': '✅ تم حذف التقييم',
  };
  const reviewTerminalActions = ['request-revision', 'reject', 'reopen', 'admin-delete'];

  // إجراءات المراجعة والحوكمة (ADMIN-GOVERNANCE-R1). كلها POST بجسم {reason} — بعضها يتطلّب سببًا إلزاميًّا.
  const reviewAction = useMutation({
    mutationFn: ({ action, reason }: { action: string; reason?: string }) =>
      api.post(`/kpi-evaluations/${id}/${action}`, { reason }),
    onSuccess: async (_data, vars) => {
      await invalidateAll();
      toast.success(reviewSuccessLabels[vars.action] ?? '✅ تم تنفيذ الإجراء');
      if (reviewTerminalActions.includes(vars.action)) setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
    },
    onError: (e) => toast.error(approvalErrorMessage(e)),
  });

  // تشغيل إجراء يتطلّب سببًا إلزاميًّا عبر نافذة إدخال بسيطة. يُلغى بلا فعل عند غياب السبب.
  const runWithReason = (action: string, promptLabel: string) => {
    if (reviewAction.isPending) return;
    const reason = window.prompt(promptLabel)?.trim();
    if (!reason) return;
    reviewAction.mutate({ action, reason });
  };

  const { data: reviewEvents } = useQuery({
    queryKey: ['kpi-review-events', id],
    queryFn: async () => (await api.get<KpiEvaluationReviewEventDto[]>(`/kpi-evaluations/${id}/review-events`)).data,
  });

  if (isLoading) return <LoadingState label="يتم تحميل التقييم…" />;
  if (isError || !ev)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل التقييم" description="حدث خطأ أثناء جلب تفاصيل التقييم. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع</button>
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-bold text-navy">{ev.templateTitle}</h1>
        <Badge tone="navy">{kpiEvaluationStatusLabel[ev.status]}</Badge>
      </div>
      <p className="text-ink-2">
        {ev.subjectName} · {periodTypeLabel[ev.periodType]} · {formatPeriod(ev.periodKey)} · النتيجة: {ev.totalScore ?? '—'}
      </p>
      {ev.canEdit && (
        <Alert tone="navy">
          <span className="font-semibold">كيف تُحتسب النتيجة:</span> درجة كل مؤشّر (من 0 إلى 100) × وزنه، ثم مجموع
          الدرجات الموزونة. مثال: درجة 80 ووزن 20 ⇒ الدرجة الموزونة = 16 من 20. يتم احتساب النتيجة النهائية عند
          إرسال التقييم، وليس أثناء كتابة المسودة.
        </Alert>
      )}
      <Card>
        <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-right text-ink-2">
              <th className="pb-2">المؤشر</th>
              <th className="pb-2">الوزن</th>
              <th className="pb-2">المستهدف</th>
              <th className="pb-2">القيمة</th>
              <th className="pb-2">الدرجة</th>
              <th className="pb-2">ملاحظة</th>
            </tr>
          </thead>
          <tbody>
            {ev.results.map((r) => {
              const d = draft[r.kpiMetricId] ?? {
                rawValue: r.rawValue?.toString() ?? '',
                score: r.score?.toString() ?? '',
                note: r.note ?? '',
              };
              const set = (patch: Partial<typeof d>) => {
                setDirty(true);
                setDraft((prev) => ({ ...prev, [r.kpiMetricId]: { ...d, ...patch } }));
              };
              // الحقل المعتمد حسب طريقة الحساب: التلقائي يأخذ القيمة الفعلية، اليدوي يأخذ الدرجة، والمزيج كليهما.
              const showActual = r.calcMethod === 'Auto' || r.calcMethod === 'Hybrid';
              const showScore = r.calcMethod === 'Manual' || r.calcMethod === 'Hybrid';
              return (
                <tr key={r.kpiMetricId} className="border-t border-line align-top">
                  <td className="py-2">
                    <div className="font-medium text-navy">{r.metricName}{r.unit ? ` (${r.unit})` : ''}</div>
                    <div className="mt-0.5 text-xs text-ink-3">{calcMethodHint[r.calcMethod]}</div>
                  </td>
                  <td className="py-2">{r.weight}</td>
                  <td className="py-2">{r.targetValue ?? '—'}</td>
                  <td className="py-2 w-28">
                    {!ev.canEdit ? (
                      r.rawValue ?? '—'
                    ) : showActual ? (
                      <Input value={d.rawValue} onChange={(e) => set({ rawValue: e.target.value })} placeholder="القيمة الفعلية" />
                    ) : (
                      <span className="text-xs text-ink-3">غير مستخدم لهذا المؤشّر</span>
                    )}
                  </td>
                  <td className="py-2 w-28">
                    {!ev.canEdit ? (
                      r.score ?? '—'
                    ) : showScore ? (
                      <Input value={d.score} onChange={(e) => set({ score: e.target.value })} placeholder="0–100" />
                    ) : (
                      <span className="text-xs text-ink-3">يُحتسب تلقائيًا عند الإرسال</span>
                    )}
                  </td>
                  <td className="py-2">
                    {ev.canEdit ? (
                      <Input value={d.note} onChange={(e) => set({ note: e.target.value })} />
                    ) : (
                      r.note ?? '—'
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        </div>
        {ev.canEdit && (
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <Button
              loading={save.isPending}
              disabled={save.isPending || submit.isPending}
              onClick={async () => {
                if (save.isPending || submit.isPending) return;
                try {
                  await save.mutateAsync(ev.results);
                  toast.success('✅ تم حفظ التقييم');
                } catch { /* الخطأ يظهر عبر Toast من onError */ }
              }}
            >
              حفظ النتائج
            </Button>
            <Button variant="ghost" loading={submit.isPending} disabled={save.isPending || submit.isPending} onClick={() => saveThenSubmit(ev.results)}>
              إرسال
            </Button>
            {save.isPending ? (
              <span className="text-xs text-ink-2">جارٍ الحفظ…</span>
            ) : dirty ? (
              <Badge tone="gold">توجد تغييرات غير محفوظة</Badge>
            ) : (
              <Badge tone="success">محفوظ</Badge>
            )}
            <span className="text-xs text-ink-3">عند الإرسال يُحفظ ما لم يُحفظ تلقائيًا أولًا.</span>
          </div>
        )}
        {/* مسار المراجعة والحوكمة (ADMIN-GOVERNANCE-R1): تُعرَض الأزرار حسب الأعلام المحسوبة خادميًّا. */}
        {(ev.canReview || ev.canFlag || ev.canReopen || ev.canAdminDelete) && (
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-line pt-4">
            {ev.canReview && (ev.status === 'UnderReview' || ev.status === 'Submitted') && (
              <>
                <Button loading={approve.isPending} disabled={approve.isPending || reviewAction.isPending}
                  onClick={() => { if (approve.isPending || reviewAction.isPending) return; approve.mutate(); }}>اعتماد</Button>
                <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                  onClick={() => runWithReason('request-revision', 'سبب طلب التعديل (إلزاميّ):')}>طلب تعديل</Button>
                <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                  onClick={() => runWithReason('reject', 'سبب الرفض النهائيّ (إلزاميّ):')}>رفض نهائيّ</Button>
              </>
            )}
            {ev.canFlag && (
              <>
                <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                  onClick={() => runWithReason('flag', 'سبب الإشارة للمراجعة (إلزاميّ):')}>إشارة للمراجعة</Button>
                <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                  onClick={() => runWithReason('request-reopen', 'سبب طلب إعادة الفتح (إلزاميّ):')}>طلب إعادة فتح</Button>
              </>
            )}
            <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
              onClick={() => runWithReason('comment', 'التعليق (إلزاميّ):')}>تعليق مراجعة</Button>
            {ev.canReopen && (
              <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                onClick={() => runWithReason('reopen', 'سبب إعادة الفتح للتعديل (إلزاميّ):')}>إعادة فتح</Button>
            )}
            {ev.canAdminDelete && (
              <Button variant="ghost" loading={reviewAction.isPending} disabled={approve.isPending || reviewAction.isPending}
                onClick={() => runWithReason('admin-delete', 'سبب الحذف الإداريّ (إلزاميّ):')}>حذف إداريّ</Button>
            )}
          </div>
        )}
      </Card>

      {/* معلومات المراجع الحاليّ ونتيجة المراجعة إن وُجدت. */}
      {(ev.reviewerName || ev.reviewNote) && (
        <Card>
          <h3 className="mb-2 text-sm font-bold text-navy">المراجعة</h3>
          {ev.reviewerName && (
            <p className="text-sm text-ink-2">
              المراجع: <span className="font-medium text-navy">{ev.reviewerName}</span>
              {ev.reviewedAtUtc ? ` · ${new Date(ev.reviewedAtUtc).toLocaleString('ar')}` : ''}
            </p>
          )}
          {ev.reviewNote && <p className="mt-1 text-sm text-ink-2">ملاحظة المراجعة: {ev.reviewNote}</p>}
        </Card>
      )}

      {/* الخطّ الزمنيّ لأحداث المراجعة (Timeline). */}
      {reviewEvents && reviewEvents.length > 0 && (
        <Card>
          <h3 className="mb-3 text-sm font-bold text-navy">سجلّ المراجعة</h3>
          <ul className="space-y-2">
            {reviewEvents.map((rev) => (
              <li key={rev.id} className="border-r-2 border-line pr-3 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone="navy">{reviewActionLabel[rev.action] ?? rev.action}</Badge>
                  <span className="text-ink-2">{rev.actorName ?? '—'}</span>
                  <span className="text-xs text-ink-3">{new Date(rev.createdAtUtc).toLocaleString('ar')}</span>
                </div>
                {rev.reason && <p className="mt-1 text-ink-2">{rev.reason}</p>}
              </li>
            ))}
          </ul>
        </Card>
      )}

      {/* الملاحظات الإدارية المرتبطة بهذا التقييم (طبقة سياقية). */}
      <ManagementNotesPanel
        entityType="KpiEvaluation"
        entityId={id}
        title="الملاحظات الإدارية على هذا التقييم"
      />
    </div>
  );
}

import { useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
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
  KpiTemplateDto,
  KpiResultDto,
  PeriodType,
} from '../types/api';
import { operationalWeekKey, riyadhToday } from '../lib/dashboardPeriod';

// صيغة الفترة الأسبوعية المعتمدة YYYY-Www (مثل 2026-W25) — تمنع إدخال قيَم حرّة غير مفهومة.
const isValidWeekKey = (key: string) => /^\d{4}-W\d{2}$/.test(key.trim());

// توضيح طريقة الإدخال لكل نوع مؤشّر داخل شاشة التقييم — يحدّد للمستخدم الحقل المعتمد في الحساب.
const calcMethodHint: Record<KpiCalcMethod, string> = {
  Auto: 'تلقائي — أدخل القيمة الفعلية، وسيحسب النظام الدرجة من المستهدف.',
  Manual: 'يدوي — أدخل الدرجة مباشرة من 0 إلى 100.',
  Hybrid: 'مزيج — إن أدخلت الدرجة اليدوية فهي المعتمدة، وإلا تُحتسب من القيمة الفعلية.',
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

  if (openId) return <KpiDetail id={openId} isManagement={isManagement} onBack={closeDetail} />;

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
  // عند تمرير subjectFilter نطلب تقييمات موظّف واحد فقط (subjectUserId). النطاق والصلاحية يُفرَضان خادميًّا.
  const { data: items, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-evaluations', subjectFilter ?? 'all'],
    queryFn: async () =>
      (await api.get<KpiEvaluationListItemDto[]>(
        '/kpi-evaluations',
        subjectFilter ? { params: { subjectUserId: subjectFilter } } : undefined,
      )).data,
  });
  // كل العناصر تخصّ الموظّف نفسه عند الحصر، فنشتقّ اسمه من أوّل عنصر لعرضه في الشريط التوضيحي.
  const subjectName = items?.[0]?.subjectName;
  const [kpiTemplateId, setKpiTemplateId] = useState('');
  const [subjectUserId, setSubjectUserId] = useState('');
  // حارس الدورية: تقييم KPI أسبوعي فقط في المرحلة الحالية — لا يُتاح اختيار دورية أخرى.
  const periodType: PeriodType = 'Weekly';
  // افتراضيًّا الأسبوع التشغيلي الحالي (الخميس→الأربعاء) المطابق لمنطق الخادم، فلا يُضطر المستخدم لنسخ الصيغة يدويًّا.
  const [periodKey, setPeriodKey] = useState(() => operationalWeekKey(riyadhToday()));
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

  // قوالب التقييم تُصفّى بحسب المسمّى الوظيفي للموظّف المُختار: لا تُجلب إلا بعد اختياره،
  // فيظهر للمدير فقط القوالب العامّة أو المربوطة بدور هذا الموظّف.
  const { data: templates } = useQuery({
    queryKey: ['kpi-templates', 'for-subject', subjectUserId],
    queryFn: async () =>
      // قوالب التقييم: المنشورة النشطة الأسبوعية فقط (لا مسوّدات/مؤرشفة/غير أسبوعية)،
      // والمطابقة لمسمّى الموظّف أو العامّة — حتى لا تظهر قوالب تجريبية أو غير مناسبة.
      (await api.get<KpiTemplateDto[]>('/kpi-templates', {
        params: { isActive: true, status: 'Published', cadence: 'WeeklyPulse', subjectUserId },
      })).data,
    enabled: isManagement && !!subjectUserId,
  });

  // عند تغيير الموظّف نُفرّغ القالب المُختار لأن قائمة القوالب تتغيّر بحسب دوره.
  const onSubjectChange = (id: string) => {
    setSubjectUserId(id);
    setKpiTemplateId('');
  };

  // الخادم يُطبّق أولوية اختيار القالب: قالب متخصص واحد لدور الموظّف، وإلا العام فقط.
  // فإن أُرجِع قالب واحد فقط نختاره تلقائيًّا (قيمة مُشتقّة) لتبسيط الإنشاء وتأكيد أنه القالب المناسب.
  const effectiveTemplateId = kpiTemplateId || (templates?.length === 1 ? templates[0].id : '');

  const create = useMutation({
    mutationFn: () => api.post<KpiEvaluationDto>('/kpi-evaluations', { kpiTemplateId: effectiveTemplateId, subjectUserId, periodType, periodKey }),
    onSuccess: (res) => {
      setSubjectUserId('');
      setKpiTemplateId('');
      setPeriodKey(operationalWeekKey(riyadhToday()));
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
            <div className="w-56">
              <Field label="قالب التقييم">
                <Select value={effectiveTemplateId} onChange={(e) => setKpiTemplateId(e.target.value)} disabled={!subjectUserId}>
                  <option value="">{subjectUserId ? 'اختر قالبًا…' : 'اختر الموظف أولًا'}</option>
                  {templates?.map((t) => (
                    <option key={t.id} value={t.id}>{t.title}</option>
                  ))}
                </Select>
              </Field>
              <p className="mt-1 text-xs text-ink-3">تظهر هنا قوالب KPI المناسبة لدور الموظف فقط.</p>
            </div>
            <div className="w-40">
              <Field label="الدورية">
                <div className="flex h-10 items-center rounded-lg border border-line bg-offwhite px-3">
                  <Badge tone="navy">{periodTypeLabel[periodType]}</Badge>
                </div>
              </Field>
              <p className="mt-1 text-xs text-ink-3">تقييم KPI الحالي أسبوعي. التجميع الشهري والربع سنوي والسنوي سيُدعم لاحقًا.</p>
            </div>
            <div className="w-40">
              <Field label="الفترة (أسبوع)"><Input value={periodKey} onChange={(e) => setPeriodKey(e.target.value)} placeholder="2026-W25" /></Field>
              {periodKey.trim() !== '' && !isValidWeekKey(periodKey) && (
                <p className="mt-1 text-xs text-alert">استخدم صيغة الأسبوع YYYY-Www مثل 2026-W25.</p>
              )}
            </div>
            <Button
              disabled={!effectiveTemplateId || !subjectUserId || !isValidWeekKey(periodKey) || create.isPending}
              onClick={() => { setErr(null); create.mutate(); }}
            >
              إنشاء تقييم
            </Button>
          </div>
          )}
          {subjectUserId && templates && templates.length === 0 && (
            <div className="mt-3">
              <Alert tone="navy">
                لا توجد قوالب مؤشّرات مرتبطة بالمسمّى الوظيفي لهذا الموظّف حاليًا. اربط قالبًا بمسمّاه من إعدادات
                قوالب المؤشّرات، أو استخدم قالبًا عامًّا مثل «النبض الأسبوعي العام».
              </Alert>
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
        )}
      </Card>
    </div>
  );
}

function KpiDetail({ id, isManagement, onBack }: { id: string; isManagement: boolean; onBack: () => void }) {
  const qc = useQueryClient();
  const { data: ev, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-evaluation', id],
    queryFn: async () => (await api.get<KpiEvaluationDto>(`/kpi-evaluations/${id}`)).data,
  });
  const [draft, setDraft] = useState<Record<string, { rawValue: string; score: string; note: string }>>({});
  const [dirty, setDirty] = useState(false);
  const [err, setErr] = useState<string | null>(null);

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

  const save = useMutation({
    mutationFn: (results: KpiResultDto[]) => api.put(`/kpi-evaluations/${id}/results`, buildPayload(results)),
    onSuccess: () => { setDirty(false); void qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] }); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const submit = useMutation({
    mutationFn: () => api.post(`/kpi-evaluations/${id}/submit`),
    onSuccess: () => {
      setDirty(false);
      void qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] });
      void qc.invalidateQueries({ queryKey: ['kpi-evaluations'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // إصلاح بَغ النتيجة صفر: عند الإرسال نحفظ المسودة أولًا إن وُجدت تغييرات غير محفوظة،
  // فلا يُحتسب التقييم على نتائج فارغة. إن فشل الحفظ لا نُرسِل (يبقى قابلًا للتحرير).
  const saveThenSubmit = async (results: KpiResultDto[]) => {
    setErr(null);
    try {
      if (dirty) await save.mutateAsync(results);
      await submit.mutateAsync();
    } catch {
      /* الخطأ مُعالَج في onError للطلب الفاشل؛ لا نتابع الإرسال */
    }
  };

  const approve = useMutation({
    mutationFn: () => api.post(`/kpi-evaluations/${id}/approve`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['kpi-evaluation', id] });
      void qc.invalidateQueries({ queryKey: ['kpi-evaluations'] });
    },
    onError: (e) => setErr(apiErrorMessage(e)),
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
      {err && <Alert tone="alert">{err}</Alert>}
      {ev.canEdit && (
        <Alert tone="navy">
          <span className="font-semibold">كيف تُحتسب النتيجة:</span> درجة كل مؤشّر (من 0 إلى 100) × وزنه، ثم مجموع
          الدرجات الموزونة. مثال: درجة 80 ووزن 20 ⇒ الدرجة الموزونة = 16 من 20. يتم احتساب النتيجة النهائية عند
          إرسال التقييم، وليس أثناء كتابة المسودة.
        </Alert>
      )}
      <Card>
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
        {ev.canEdit && (
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <Button disabled={save.isPending || submit.isPending} onClick={() => { setErr(null); save.mutate(ev.results); }}>
              حفظ النتائج
            </Button>
            <Button variant="ghost" disabled={save.isPending || submit.isPending} onClick={() => saveThenSubmit(ev.results)}>
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
        {isManagement && ev.status === 'Submitted' && (
          <div className="mt-4">
            <Button disabled={approve.isPending} onClick={() => { setErr(null); approve.mutate(); }}>اعتماد</Button>
          </div>
        )}
      </Card>

      {/* الملاحظات الإدارية المرتبطة بهذا التقييم (طبقة سياقية). */}
      <ManagementNotesPanel
        entityType="KpiEvaluation"
        entityId={id}
        title="الملاحظات الإدارية على هذا التقييم"
      />
    </div>
  );
}

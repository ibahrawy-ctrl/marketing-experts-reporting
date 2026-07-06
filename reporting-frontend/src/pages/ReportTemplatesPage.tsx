// قوالب التقارير — قائمة + إنشاء/تعديل + باني الحقول (إضافة/تعديل/حذف/ترتيب) + نشر إصدار.
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useJobRoles, useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { fieldTypeLabel, templateStatusLabel, periodTypeLabel, templateClassificationLabel, formatDate } from '../lib/format';
import type {
  ReportTemplateListItem,
  ReportTemplateDetailDto,
  TemplateVersionDto,
  TemplateFieldDto,
  TemplatePreviewDto,
  TemplateAssignmentsDto,
  TemplateAssignmentScope,
  TemplateAssignmentKind,
  CreateAssignmentRequest,
  FieldType,
  PeriodType,
  TemplateStatus,
  TemplateClassification,
  JobRoleDto,
  ProjectRepeatableConfig,
  RepeatableSubField,
  RepeatableSubFieldType,
} from '../types/api';

// أنواع الحقول الفرعية المتاحة داخل قسم المشاريع المتكرر (+ 'Grid' = جدول صفوف، 'Select' = قائمة منسدلة).
const REPEATABLE_SUBFIELD_TYPES: RepeatableSubFieldType[] = [
  'Currency', 'Number', 'Decimal', 'Percentage', 'ShortText', 'LongText', 'Date', 'Boolean', 'Select', 'Grid',
];

// تسمية نوع الحقل الفرعي — 'Grid'/'Select' ليسا FieldType فيُعالَجان منفصلًا.
function repeatableSubTypeLabel(t: RepeatableSubFieldType): string {
  if (t === 'Grid') return 'جدول (صفوف)';
  if (t === 'Select') return 'قائمة منسدلة';
  return fieldTypeLabel[t];
}

const EMPTY_REPEATABLE_CONFIG: ProjectRepeatableConfig = {
  projectRequired: true,
  minProjects: 1,
  maxProjects: 10,
  fields: [],
};

function parseRepeatableConfig(configJson: string | null): ProjectRepeatableConfig {
  if (!configJson) return { ...EMPTY_REPEATABLE_CONFIG };
  try {
    const parsed = JSON.parse(configJson) as Partial<ProjectRepeatableConfig>;
    return {
      projectRequired: parsed.projectRequired ?? true,
      minProjects: Number.isFinite(parsed.minProjects) ? Number(parsed.minProjects) : 1,
      maxProjects: Number.isFinite(parsed.maxProjects) ? Number(parsed.maxProjects) : 10,
      fields: Array.isArray(parsed.fields) ? parsed.fields : [],
    };
  } catch {
    return { ...EMPTY_REPEATABLE_CONFIG };
  }
}

// مفتاح آمن من التسمية (a-z0-9_)، مع لاحقة عند التكرار.
function slugifyKey(label: string, existing: string[]): string {
  const base = label.trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'field';
  let key = base;
  let i = 2;
  while (existing.includes(key)) { key = `${base}_${i}`; i += 1; }
  return key;
}

// تسميات أسباب استثناء المستخدم من تغطية القالب.
const exclusionReasonLabel: Record<string, string> = {
  excludedBecauseInactive: 'حساب موقوف',
  excludedBecauseRoleMismatch: 'مسمّى وظيفي مختلف',
  excludedBecauseMoreSpecificTemplateExists: 'مستثنى — يوجد قالب أخصّ لنفس الدورية',
  excludedBecauseTemplateNotAssignable: 'القالب غير منشور/غير نشط حاليًا',
  excludedManually: 'مستثنى يدويًّا (صريح)',
};

// تسميات سبب ربط المستخدم بالقالب.
const matchReasonLabel: Record<string, string> = {
  matchedByUser: 'إسناد صريح للموظّف',
  matchedByJobRole: 'مطابقة المسمّى الوظيفي',
  matchedByTeam: 'إسناد على مستوى الفريق',
  matchedByDepartment: 'إسناد على مستوى الإدارة',
  matchedByGeneral: 'قالب عام (افتراضي) — لا يملك قالبًا أخصّ',
};

// تسميات نطاق الإسناد/الاستثناء الصريح.
const scopeLabel: Record<TemplateAssignmentScope, string> = {
  Employee: 'موظّف',
  JobRole: 'مسمّى وظيفي',
  Team: 'فريق',
  Department: 'إدارة',
};

const statusTone: Record<TemplateStatus, 'success' | 'gold' | 'muted'> = {
  Published: 'success',
  Draft: 'gold',
  Archived: 'muted',
};

// «أساسي» = نبرة أساسية بارزة، «تكميلي» = نبرة هادئة لتمييزه بصريًا أنه اختياري.
const classificationTone: Record<TemplateClassification, 'navy' | 'muted'> = {
  Primary: 'navy',
  Supplementary: 'muted',
};

export default function ReportTemplatesPage() {
  const [openId, setOpenId] = useState<string | null>(null);
  if (openId) return <TemplateDetail id={openId} onBack={() => setOpenId(null)} />;
  return <TemplateList onOpen={setOpenId} />;
}

function TemplateList({ onOpen }: { onOpen: (id: string) => void }) {
  const qc = useQueryClient();
  const [statusFilter, setStatusFilter] = useState('');
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['report-templates', statusFilter],
    queryFn: async () =>
      (await api.get<ReportTemplateListItem[]>('/report-templates', { params: statusFilter ? { status: statusFilter } : {} })).data,
  });

  const jobRoles = useJobRoles();
  const [title, setTitle] = useState('');
  const [periodType, setPeriodType] = useState<PeriodType>('Weekly');
  const [jobRoleId, setJobRoleId] = useState('');
  const [classification, setClassification] = useState<TemplateClassification>('Primary');
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.post<ReportTemplateDetailDto>('/report-templates', { title, description: null, jobRoleId: jobRoleId || null, defaultPeriodType: periodType, classification }),
    onSuccess: (res) => {
      setTitle('');
      setJobRoleId('');
      setClassification('Primary');
      void qc.invalidateQueries({ queryKey: ['report-templates'] });
      onOpen(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // فلتر اختياري حسب المسمّى الوظيفي (يأتي من شاشة «إدارة المسمّيات الوظيفية» عبر ?jobRoleId=).
  const [searchParams, setSearchParams] = useSearchParams();
  const jobRoleFilter = searchParams.get('jobRoleId');

  if (isLoading) return <LoadingState label="يتم تحميل قوالب التقارير…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب قوالب التقارير. أعد المحاولة." />;
  const allItems = data ?? [];
  const items = jobRoleFilter ? allItems.filter((t) => t.jobRoleId === jobRoleFilter) : allItems;
  const jobRoleFilterName = jobRoleFilter ? (jobRoles.data ?? []).find((j) => j.id === jobRoleFilter)?.nameAr ?? null : null;
  const clearJobRoleFilter = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('jobRoleId');
    setSearchParams(next, { replace: true });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">قوالب التقارير</h1>
        <p className="mt-1 text-sm text-ink-2">أنشئ القوالب، حدّد الحقول، وانشر الإصدارات لتصبح متاحة للتسليم.</p>
      </div>

      <Card>
        <SectionTitle title="إنشاء قالب جديد" />
        {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-72"><Field label="اسم القالب"><Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="مثال: تقرير المبيعات الأسبوعي" /></Field></div>
          <div className="w-44">
            <Field label="الدورية الافتراضية">
              <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
                {(Object.keys(periodTypeLabel) as PeriodType[]).map((p) => (
                  <option key={p} value={p}>{periodTypeLabel[p]}</option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="w-56">
            <Field label="المسمى الوظيفي (اختياري)">
              <Select value={jobRoleId} onChange={(e) => setJobRoleId(e.target.value)}>
                <option value="">— غير محدد —</option>
                {(jobRoles.data ?? []).filter((j) => j.isActive).map((j) => (
                  <option key={j.id} value={j.id}>{j.nameAr}</option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="w-56">
            <Field label="تصنيف القالب">
              <Select value={classification} onChange={(e) => setClassification(e.target.value as TemplateClassification)}>
                {(Object.keys(templateClassificationLabel) as TemplateClassification[]).map((c) => (
                  <option key={c} value={c}>{templateClassificationLabel[c]}</option>
                ))}
              </Select>
            </Field>
          </div>
          <Button disabled={!title.trim() || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
            إنشاء ومتابعة الحقول
          </Button>
        </div>
        <p className="mt-2 text-xs text-ink-3">
          القالب يُربط بمسمى وظيفي ليصل للموظفين المناسبين. مسار الاعتماد قياسي تلقائيًا: الموظف ← قائد الفريق ← المدير ← المدير العام ← الرئيس التنفيذي.
        </p>
        <p className="mt-1 text-xs text-ink-3">
          <span className="font-semibold text-navy">تصنيف القالب:</span> «أساسي» = تقرير الدور المطلوب الوحيد لكل فترة (قسم النبض يكون مضمَّنًا داخله). «تكميلي» = استبيان/قالب اختياري لا يُحتسب تقريرًا أساسيًا ثانيًا. لا يُسمح بتقريرين أساسيين مطلوبين لنفس الفترة.
        </p>
      </Card>

      {jobRoleFilter && (
        <Alert tone="navy">
          عرض القوالب المرتبطة بالمسمّى الوظيفي: <strong>{jobRoleFilterName ?? jobRoleFilter}</strong>{' '}
          <button onClick={clearJobRoleFilter} className="mr-2 font-semibold text-orange underline">إلغاء الفلتر وعرض كل القوالب</button>
        </Alert>
      )}

      <Card>
        <div className="mb-3 flex items-center justify-between gap-3">
          <SectionTitle title={`القوالب (${items.length})`} />
          <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} className="max-w-[180px]">
            <option value="">كل الحالات</option>
            {(Object.keys(templateStatusLabel) as TemplateStatus[]).map((s) => (
              <option key={s} value={s}>{templateStatusLabel[s]}</option>
            ))}
          </Select>
        </div>
        {items.length === 0 ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد قوالب تقارير مطابقة.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              قوالب التقارير تحدّد الحقول التي يملؤها الموظفون. أنشئ قالبًا جديدًا من زر «قالب جديد»، أو عدّل الفلتر لعرض قوالب أخرى.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">القالب</th>
                  <th className="px-2 py-2 font-semibold">التصنيف</th>
                  <th className="px-2 py-2 font-semibold">الدورية</th>
                  <th className="px-2 py-2 font-semibold">الإصدار</th>
                  <th className="px-2 py-2 font-semibold">الحقول</th>
                  <th className="px-2 py-2 font-semibold">الحالة</th>
                  <th className="px-2 py-2 font-semibold"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => (
                  <tr key={t.id} className="border-b border-line last:border-0">
                    <td className="px-2 py-2 font-medium text-navy">{t.title}</td>
                    <td className="px-2 py-2"><Badge tone={classificationTone[t.classification]}>{templateClassificationLabel[t.classification]}</Badge></td>
                    <td className="px-2 py-2 text-ink-2">{periodTypeLabel[t.defaultPeriodType]}</td>
                    <td className="px-2 py-2">v{t.latestVersionNumber}</td>
                    <td className="px-2 py-2">{t.fieldCount}</td>
                    <td className="px-2 py-2"><Badge tone={statusTone[t.status]}>{templateStatusLabel[t.status]}</Badge></td>
                    <td className="px-2 py-2 text-left">
                      <Button variant="ghost" onClick={() => onOpen(t.id)}>إدارة الحقول</Button>
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

type DetailTab = 'details' | 'preview' | 'assignments' | 'versions';

const DETAIL_TABS: { key: DetailTab; label: string }[] = [
  { key: 'details', label: 'التفاصيل' },
  { key: 'preview', label: 'المعاينة' },
  { key: 'assignments', label: 'الموظفون المرتبطون' },
  { key: 'versions', label: 'النسخ' },
];

function TemplateDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const jobRoles = useJobRoles();
  const { data: tpl, isLoading, isError, refetch } = useQuery({
    queryKey: ['report-template', id],
    queryFn: async () => (await api.get<ReportTemplateDetailDto>(`/report-templates/${id}`)).data,
  });
  const [err, setErr] = useState<string | null>(null);
  const [editMeta, setEditMeta] = useState(false);
  const [tab, setTab] = useState<DetailTab>('details');
  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ['report-template', id] });
    void qc.invalidateQueries({ queryKey: ['report-templates'] });
  };

  const publish = useMutation({
    mutationFn: (versionId: string) => api.post(`/report-templates/versions/${versionId}/publish`),
    onSuccess: invalidate,
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const newVersion = useMutation({
    mutationFn: () => api.post(`/report-templates/${id}/versions`),
    onSuccess: invalidate,
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  // الأرشفة: تُخفي القالب من إنشاء التقارير الجديدة مع الحفاظ الكامل على التقارير القديمة.
  const archive = useMutation({
    mutationFn: () => api.post(`/report-templates/${id}/archive`),
    onSuccess: () => { invalidate(); onBack(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  // الحذف النهائي: مسموح فقط لقالب مسودة غير مستخدَم (لا تقارير مرتبطة).
  const hardDelete = useMutation({
    mutationFn: () => api.delete(`/report-templates/${id}`),
    onSuccess: () => { invalidate(); onBack(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل القالب…" />;
  if (isError || !tpl)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل القالب" description="حدث خطأ أثناء جلب تفاصيل القالب. أعد المحاولة." />;

  // الإصدار الأحدث = الأعلى رقمًا.
  const latest = [...tpl.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0];
  const jobRoleName = tpl.jobRoleId ? (jobRoles.data ?? []).find((j) => j.id === tpl.jobRoleId)?.nameAr : null;

  const onArchive = () => {
    if (!window.confirm('سيتم أرشفة القالب: يختفي من إنشاء التقارير الجديدة مع الحفاظ الكامل على التقارير القديمة المرتبطة به. هل تريد المتابعة؟')) return;
    setErr(null);
    archive.mutate();
  };
  const onHardDelete = () => {
    if (!window.confirm('حذف نهائي لهذا القالب (مسودة غير مستخدَمة). لا يمكن التراجع عن العملية. هل تريد المتابعة؟')) return;
    setErr(null);
    hardDelete.mutate();
  };

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع للقوالب</button>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-navy">{tpl.title}</h1>
          <Badge tone={statusTone[tpl.status]}>{templateStatusLabel[tpl.status]}</Badge>
          <Badge tone={classificationTone[tpl.classification]}>{templateClassificationLabel[tpl.classification]}</Badge>
          <Badge tone={tpl.isActive ? 'success' : 'muted'}>{tpl.isActive ? 'نشط' : 'غير نشط'}</Badge>
        </div>
        <div className="flex flex-wrap gap-2">
          {tpl.status !== 'Archived' && (
            <Button variant="ghost" onClick={() => { setErr(null); setEditMeta((v) => !v); setTab('details'); }}>
              {editMeta ? 'إغلاق التعديل' : 'تعديل بيانات القالب'}
            </Button>
          )}
          {latest && !latest.isPublished && (
            <Button onClick={() => { setErr(null); publish.mutate(latest.id); }} disabled={publish.isPending || latest.fields.length === 0}>
              نشر الإصدار v{latest.versionNumber}
            </Button>
          )}
          {latest && latest.isPublished && (
            <Button variant="ghost" onClick={() => { setErr(null); newVersion.mutate(); }} disabled={newVersion.isPending}>
              إصدار جديد (نسخ)
            </Button>
          )}
          {tpl.status !== 'Archived' && (
            <Button variant="danger" onClick={onArchive} disabled={archive.isPending}>
              أرشفة
            </Button>
          )}
          {tpl.canHardDelete && (
            <Button variant="danger" onClick={onHardDelete} disabled={hardDelete.isPending}>
              حذف نهائي
            </Button>
          )}
        </div>
      </div>
      <p className="text-sm text-ink-2">
        {periodTypeLabel[tpl.defaultPeriodType]} · الإصدارات: {tpl.versions.length}
        {' · '}المسمى الوظيفي: {jobRoleName ?? 'غير محدد'}
        {' · '}التصنيف: {templateClassificationLabel[tpl.classification]}
        {' · '}التقارير المرتبطة: {tpl.submissionCount}
      </p>

      {/* تنبيه قابلية الحذف مقابل الأرشفة */}
      {tpl.canHardDelete ? (
        <Alert tone="gold">
          هذا القالب مسودة ولم يُستخدم في أي تقرير، لذا يمكن حذفه نهائيًّا. القوالب المنشورة أو المستخدَمة تُؤرشَف فقط (لا تُحذف) حفاظًا على التقارير القديمة.
        </Alert>
      ) : tpl.status !== 'Archived' ? (
        <Alert tone="navy">
          هذا القالب منشور أو مستخدَم في تقارير سابقة؛ لذا يُؤرشَف فقط (لا يُحذف نهائيًّا). الأرشفة تُخفيه من إنشاء التقارير الجديدة دون المساس بالتقارير القديمة المرتبطة بإصداراته.
        </Alert>
      ) : null}

      {err && <Alert tone="alert">{err}</Alert>}

      {/* تبويبات التفاصيل/المعاينة/الموظفون المرتبطون/النسخ */}
      <div className="flex flex-wrap gap-1 border-b border-line">
        {DETAIL_TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`rounded-t-lg px-4 py-2 text-sm font-semibold transition ${
              tab === t.key ? 'border-b-2 border-orange text-navy' : 'text-ink-2 hover:text-navy'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'details' && (
        <div className="space-y-6">
          {tpl.classification === 'Primary' ? (
            <div className="rounded-xl border border-navy/15 bg-navy-50 p-3 text-sm text-ink">
              <span className="font-bold text-navy">تقرير أساسي مطلوب:</span> هذا هو تقرير الدور المطلوب الوحيد لكل فترة، ويضم قسم النبض الأسبوعي مضمَّنًا بداخله. لا يُسمح للموظف بتقريرين أساسيين مطلوبين لنفس الفترة.
            </div>
          ) : (
            <div className="rounded-xl border border-line bg-offwhite p-3 text-sm text-ink-2">
              <span className="font-bold text-ink">قالب تكميلي/اختياري:</span> استبيان نبض أو قالب اختياري لا يُحتسب تقريرًا أساسيًا ثانيًا ولا يُفرض على الموظف كتقرير مطلوب لنفس الفترة.
            </div>
          )}
          <div className="rounded-xl border border-navy/15 bg-navy-50 p-3 text-sm text-ink">
            <span className="font-bold text-navy">مسار الاعتماد:</span> الموظف ← قائد الفريق ← المدير ← المدير العام ← الرئيس التنفيذي.
            أي تقرير من هذا القالب يسير في السلسلة القياسية تلقائيًا (لا يصل للرئيس التنفيذي مباشرة إلا بالتصعيد).
          </div>
          {editMeta && <TemplateMetaEditor tpl={tpl} jobRoles={jobRoles.data ?? []} onSaved={() => { setEditMeta(false); invalidate(); }} setErr={setErr} />}
          {latest && <VersionEditor version={latest} onChanged={invalidate} setErr={setErr} />}
        </div>
      )}

      {tab === 'preview' && <TemplatePreviewPanel id={id} />}
      {tab === 'assignments' && <TemplateAssignmentsPanel id={id} />}
      {tab === 'versions' && <VersionsList versions={tpl.versions} onChanged={invalidate} setErr={setErr} />}
    </div>
  );
}

// معاينة القالب «كما يراه الموظّف» — قراءة فقط بلا أي إنشاء/كتابة (لا تُنشئ تسليمًا).
function TemplatePreviewPanel({ id }: { id: string }) {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['report-template-preview', id],
    queryFn: async () => (await api.get<TemplatePreviewDto>(`/report-templates/${id}/preview`)).data,
  });

  if (isLoading) return <LoadingState label="يتم تجهيز المعاينة…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّرت المعاينة" description="حدث خطأ أثناء تجهيز معاينة القالب. أعد المحاولة." />;

  const fields = [...data.fields].sort((a, b) => a.order - b.order);

  return (
    <Card>
      <Alert tone="navy">
        هذه معاينة لِما يراه الموظّف عند تعبئة هذا القالب. عرض فقط — لا يتم إنشاء تقرير أو حفظ أي بيانات.
        {!data.isPublished && ' (لا يوجد إصدار منشور بعد؛ تُعرض حقول آخر مسودة).'}
      </Alert>
      <div className="mt-4 rounded-2xl border border-line bg-offwhite p-5">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-xl font-bold text-navy">{data.title}</h2>
          <Badge tone="navy">{periodTypeLabel[data.defaultPeriodType]}</Badge>
          <Badge tone={classificationTone[data.classification]}>{templateClassificationLabel[data.classification]}</Badge>
          {data.versionNumber != null && <Badge tone="muted">إصدار v{data.versionNumber}</Badge>}
        </div>
        {data.description && <p className="mt-2 text-sm text-ink-2">{data.description}</p>}

        {fields.length === 0 ? (
          <p className="mt-6 text-sm text-ink-2">لا توجد حقول في هذا القالب بعد.</p>
        ) : (
          <div className="mt-5 space-y-4">
            {fields.map((f) => <PreviewField key={f.id} field={f} />)}
          </div>
        )}

        <div className="mt-6 flex gap-2 border-t border-line pt-4">
          <Button variant="ghost" disabled>حفظ كمسودة</Button>
          <Button disabled>إرسال التقرير</Button>
          <span className="self-center text-xs text-ink-3">(الأزرار للعرض فقط في المعاينة)</span>
        </div>
      </div>
    </Card>
  );
}

// عرض حقل واحد في المعاينة بشكل مطابق لِما يراه الموظّف (معطّل/غير تفاعلي).
function PreviewField({ field }: { field: TemplateFieldDto }) {
  if (field.fieldType === 'SectionHeader') {
    return (
      <div className="border-b border-navy/20 pb-1 pt-2">
        <h3 className="text-base font-bold text-navy">{field.label}</h3>
        {field.helpText && <p className="text-xs text-ink-2">{field.helpText}</p>}
      </div>
    );
  }

  if (field.fieldType === 'ProjectRepeatableSection') {
    const cfg = parseRepeatableConfig(field.configJson);
    return (
      <div className="rounded-lg border border-dashed border-gold/60 bg-gold/5 p-3">
        <div className="mb-1 flex items-center gap-2">
          <span className="text-sm font-medium text-ink">{field.label}</span>
          <Badge tone="gold">قسم مشاريع متكرر</Badge>
          {field.isRequired ? <Badge tone="alert">مطلوب</Badge> : <Badge tone="muted">اختياري</Badge>}
        </div>
        <p className="text-xs text-ink-2">
          يُعبّأ مرة لكل مشروع (حد {cfg.minProjects}–{cfg.maxProjects > 0 ? cfg.maxProjects : '∞'}). الحقول لكل مشروع:
        </p>
        <ul className="mt-1 list-inside list-disc text-xs text-ink-2">
          {cfg.fields.length === 0
            ? <li className="text-ink-3">لم تُعرَّف حقول فرعية بعد.</li>
            : cfg.fields.map((f) => (
              <li key={f.key}>{f.label || f.key} <span className="text-ink-3">({repeatableSubTypeLabel(f.type)}{f.required ? '، مطلوب' : ''})</span></li>
            ))}
        </ul>
      </div>
    );
  }

  const placeholder = field.helpText ?? `أدخل ${fieldTypeLabel[field.fieldType]}…`;

  return (
    <div>
      <div className="mb-1 flex items-center gap-2">
        <span className="text-sm font-medium text-ink">{field.label}</span>
        {field.isRequired ? <Badge tone="alert">مطلوب</Badge> : <Badge tone="muted">اختياري</Badge>}
        <span className="text-xs text-ink-3">{fieldTypeLabel[field.fieldType]}</span>
      </div>
      {field.fieldType === 'LongText' || field.fieldType === 'RichText' ? (
        <textarea
          disabled
          rows={3}
          placeholder={placeholder}
          className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm text-ink-3"
        />
      ) : field.fieldType === 'Boolean' ? (
        <label className="flex items-center gap-2 text-sm text-ink-3">
          <input type="checkbox" disabled /> {placeholder}
        </label>
      ) : (
        <Input disabled placeholder={placeholder} className="text-ink-3" />
      )}
    </div>
  );
}

// تغطية القالب: المرتبطون والمستثنون بأسبابهم — بنفس أولوية الاختيار بالخادم.
function TemplateAssignmentsPanel({ id }: { id: string }) {
  const qc = useQueryClient();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['report-template-assignments', id],
    queryFn: async () => (await api.get<TemplateAssignmentsDto>(`/report-templates/${id}/assignments`)).data,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['report-template-assignments', id] });

  if (isLoading) return <LoadingState label="يتم حساب الموظفين المرتبطين…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر حساب التغطية" description="حدث خطأ أثناء حساب الموظفين المرتبطين. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      <Card>
        <SectionTitle title="قاعدة الربط" hint="أولوية الخادم: استثناء/إسناد الموظّف ← المسمّى الوظيفي ← الفريق ← الإدارة ← القالب العام. أي استثناء يتفوّق على الإسناد في نفس المستوى أو أدنى." />
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs text-ink-2">نوع القالب</dt>
            <dd className="font-medium text-navy">{data.isRoleSpecific ? `متخصص — مسمّى: ${data.jobRoleName ?? 'غير محدد'}` : 'عام (لكل من لا يملك قالبًا أخصّ)'}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">الدورية</dt>
            <dd className="font-medium text-navy">{periodTypeLabel[data.defaultPeriodType]}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">التصنيف</dt>
            <dd className="font-medium text-navy">{templateClassificationLabel[data.classification]}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">قابلية الإسناد</dt>
            <dd>
              <Badge tone={data.isAssignable ? 'success' : 'gold'}>
                {data.isAssignable ? 'منشور ونشط — يُسنَد فعليًّا' : 'غير قابل للإسناد حاليًا (مسودة/مؤرشف/غير نشط)'}
              </Badge>
            </dd>
          </div>
        </dl>
        {!data.isAssignable && (
          <div className="mt-3"><Alert tone="gold">القالب غير منشور/غير نشط، لذا لا يستلمه أي موظّف حاليًا في إنشاء التقارير الجديدة. القائمة أدناه توضّح من سيستلمه عند نشره.</Alert></div>
        )}
      </Card>

      {data.conflicts.length > 0 && (
        <Card>
          <SectionTitle title={`تعارضات (${data.conflicts.length})`} hint="موظّف لديه أكثر من تقرير أساسي لنفس الدورية — يجب حلّه عبر الاستثناء أو تغيير التصنيف." />
          <ConflictTable conflicts={data.conflicts} />
        </Card>
      )}

      <AddAssignmentForm templateId={id} onDone={invalidate} />

      <Card>
        <SectionTitle title={`الإسنادات/الاستثناءات الصريحة (${data.assignments.length})`} hint="الصفوف المُضافة يدويًّا لهذا القالب. يمكن تعطيلها أو حذفها." />
        <ExplicitAssignmentsTable templateId={id} rows={data.assignments} onChanged={invalidate} />
      </Card>

      <Card>
        <SectionTitle title={`الموظفون المرتبطون (${data.matchedUsers.length})`} hint="من سيستلم هذا القالب فعليًّا، مع سبب الظهور لكل موظّف." />
        {!data.isRoleSpecific && (
          <div className="mb-3 space-y-2">
            <Alert tone="navy">
              هذا القالب <strong>عام</strong>، ويظهر فقط للموظفين الذين لا يوجد لهم قالب أخصّ (حسب الموظّف أو المسمّى الوظيفي أو
              الفريق أو الإدارة) لنفس الدورية. أي موظّف له قالب أخصّ يظهر في «الموظفون المستثنون» بسبب «يوجد قالب أخصّ».
            </Alert>
            {data.matchedUsers.length >= 8 && (
              <Alert tone="gold">
                تنبيه: هذا القالب العام سيظهر لـ {data.matchedUsers.length} موظّفًا — أي موظّف لا يملك قالبًا أخصّ. راجع القائمة
                واستخدم أزرار الاستثناء أدناه عند الحاجة.
              </Alert>
            )}
          </div>
        )}
        <MatchedUsersManager
          templateId={id}
          users={data.matchedUsers}
          isRoleSpecific={data.isRoleSpecific}
          onChanged={invalidate}
        />
      </Card>

      <Card>
        <SectionTitle title={`الموظفون المستثنون (${data.excludedUsers.length})`} hint="مع سبب الاستثناء لكل موظّف." />
        <AssignmentUserTable users={data.excludedUsers} mode="exclude" emptyText="لا يوجد موظفون مستثنون." />
      </Card>
    </div>
  );
}

// جدول الموظفين المرتبطين بأزرار استثناء سريعة (موظّف/مسمّى/فريق/إدارة) + اختيار متعدّد لاستثناء جماعي.
// لا يغيّر منطق المطابقة بالخادم — يضيف فقط صفوف استثناء صريحة عبر نفس واجهة الإسناد/الاستثناء.
function MatchedUsersManager({
  templateId, users, isRoleSpecific, onChanged,
}: {
  templateId: string; users: TemplateAssignmentsDto['matchedUsers']; isRoleSpecific: boolean; onChanged: () => void;
}) {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const postExclude = async (scopeType: TemplateAssignmentScope, scopeId: string, notes: string) => {
    const body: CreateAssignmentRequest = { scopeType, scopeId, kind: 'Exclude', notes };
    await api.post(`/report-templates/${templateId}/assignments`, body);
  };

  const afterChange = () => {
    setSelected(new Set());
    qc.invalidateQueries({ queryKey: ['report-template-assignments', templateId] });
    onChanged();
  };

  const runExclusion = async (fn: () => Promise<void>) => {
    setBusy(true);
    setErr(null);
    try {
      await fn();
      afterChange();
    } catch (e) {
      setErr(apiErrorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  const excludeOne = (scope: TemplateAssignmentScope, scopeId: string, label: string) =>
    runExclusion(() => postExclude(scope, scopeId, `استثناء سريع — ${label}`));

  const excludeSelected = () =>
    runExclusion(async () => {
      for (const userId of selected) await postExclude('Employee', userId, 'استثناء جماعي من تبويب الموظفين المرتبطين');
    });

  const toggle = (userId: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId); else next.add(userId);
      return next;
    });

  const allSelected = users.length > 0 && selected.size === users.length;
  const toggleAll = () => setSelected(allSelected ? new Set() : new Set(users.map((u) => u.userId)));

  if (users.length === 0) return <p className="py-6 text-center text-sm text-ink-2">لا يوجد موظفون مرتبطون بهذا القالب.</p>;

  return (
    <div className="space-y-3">
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="flex flex-wrap items-center gap-3 rounded-lg border border-line bg-offwhite px-3 py-2">
        <label className="flex items-center gap-2 text-sm text-ink-2">
          <input type="checkbox" checked={allSelected} onChange={toggleAll} disabled={busy} />
          تحديد الكل
        </label>
        <Button onClick={excludeSelected} disabled={selected.size === 0 || busy}>
          {busy ? 'جارٍ التنفيذ…' : `استثناء المحددين (${selected.size})`}
        </Button>
        <span className="text-xs text-ink-3">يُنشئ استثناءً صريحًا لكل موظّف محدّد على مستوى «موظّف».</span>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-right text-sm">
          <thead className="border-b border-line text-xs text-ink-2">
            <tr>
              <th className="px-2 py-2 font-semibold"><span className="sr-only">تحديد</span></th>
              <th className="px-2 py-2 font-semibold">الموظف</th>
              <th className="px-2 py-2 font-semibold">المسمى الوظيفي</th>
              <th className="px-2 py-2 font-semibold">سبب الظهور</th>
              <th className="px-2 py-2 font-semibold">استثناء سريع</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.userId} className="border-b border-line last:border-0 align-top">
                <td className="px-2 py-2">
                  <input type="checkbox" checked={selected.has(u.userId)} onChange={() => toggle(u.userId)} disabled={busy} />
                </td>
                <td className="px-2 py-2">
                  <div className="font-medium text-navy">{u.fullName}</div>
                  {u.email && <div className="text-xs text-ink-3">{u.email}</div>}
                </td>
                <td className="px-2 py-2 text-ink-2">{u.jobRoleName ?? 'غير محدد'}</td>
                <td className="px-2 py-2 text-ink-2">{u.matchReason ? (matchReasonLabel[u.matchReason] ?? u.matchReason) : '—'}</td>
                <td className="px-2 py-2">
                  <div className="flex flex-wrap gap-1.5">
                    <Button variant="ghost" onClick={() => excludeOne('Employee', u.userId, `الموظّف ${u.fullName}`)} disabled={busy}>
                      الموظّف
                    </Button>
                    {u.jobRoleId && (
                      <Button variant="ghost" onClick={() => excludeOne('JobRole', u.jobRoleId!, `المسمّى ${u.jobRoleName ?? ''}`)} disabled={busy}>
                        المسمّى
                      </Button>
                    )}
                    {u.teamId && (
                      <Button variant="ghost" onClick={() => excludeOne('Team', u.teamId!, `الفريق ${u.teamName ?? ''}`)} disabled={busy}>
                        الفريق
                      </Button>
                    )}
                    {u.departmentId && (
                      <Button variant="ghost" onClick={() => excludeOne('Department', u.departmentId!, `الإدارة ${u.departmentName ?? ''}`)} disabled={busy}>
                        الإدارة
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!isRoleSpecific && (
        <p className="text-xs text-ink-3">
          «الموظّف» يستثني هذا الشخص فقط. «المسمّى/الفريق/الإدارة» يستثني كل من ينتمي إليها من هذا القالب العام.
        </p>
      )}
    </div>
  );
}

// نموذج إضافة إسناد أو استثناء صريح (نطاق + كيان + نوع + ملاحظة).
function AddAssignmentForm({ templateId, onDone }: { templateId: string; onDone: () => void }) {
  const qc = useQueryClient();
  const [scopeType, setScopeType] = useState<TemplateAssignmentScope>('Employee');
  const [scopeId, setScopeId] = useState('');
  const [kind, setKind] = useState<TemplateAssignmentKind>('Include');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const users = useDirectoryUsers(true);
  const jobRoles = useJobRoles();
  const teams = useTeams();
  const departments = useDepartments();

  const options: { id: string; name: string }[] =
    scopeType === 'Employee' ? (users.data ?? []).map((u) => ({ id: u.id, name: u.fullName }))
    : scopeType === 'JobRole' ? (jobRoles.data ?? []).map((r) => ({ id: r.id, name: r.nameAr }))
    : scopeType === 'Team' ? (teams.data ?? []).map((t) => ({ id: t.id, name: t.nameAr }))
    : (departments.data ?? []).map((d) => ({ id: d.id, name: d.nameAr }));

  const add = useMutation({
    mutationFn: () => {
      const body: CreateAssignmentRequest = { scopeType, scopeId, kind, notes: notes.trim() || null };
      return api.post(`/report-templates/${templateId}/assignments`, body);
    },
    onSuccess: () => {
      setScopeId('');
      setNotes('');
      setErr(null);
      qc.invalidateQueries({ queryKey: ['report-template-assignments', templateId] });
      onDone();
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <Card>
      <SectionTitle title="إضافة إسناد / استثناء" hint="اختر النطاق ثم الكيان، وحدّد إن كان إسنادًا (يستلم القالب) أو استثناءً (لا يستلمه)." />
      {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Field label="النطاق">
          <Select value={scopeType} onChange={(e) => { setScopeType(e.target.value as TemplateAssignmentScope); setScopeId(''); }}>
            {(['Employee', 'JobRole', 'Team', 'Department'] as TemplateAssignmentScope[]).map((s) => (
              <option key={s} value={s}>{scopeLabel[s]}</option>
            ))}
          </Select>
        </Field>
        <Field label="الكيان">
          <Select value={scopeId} onChange={(e) => setScopeId(e.target.value)}>
            <option value="">— اختر —</option>
            {options.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
          </Select>
        </Field>
        <Field label="النوع">
          <Select value={kind} onChange={(e) => setKind(e.target.value as TemplateAssignmentKind)}>
            <option value="Include">إسناد (يستلم القالب)</option>
            <option value="Exclude">استثناء (لا يستلمه)</option>
          </Select>
        </Field>
        <Field label="ملاحظة (اختياري)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="سبب الإسناد/الاستثناء" />
        </Field>
      </div>
      <div className="mt-3">
        <Button onClick={() => add.mutate()} disabled={!scopeId || add.isPending}>
          {add.isPending ? 'جارٍ الحفظ…' : kind === 'Include' ? 'إسناد' : 'استثناء'}
        </Button>
      </div>
    </Card>
  );
}

// جدول الإسنادات/الاستثناءات الصريحة مع تعطيل/تفعيل وحذف.
function ExplicitAssignmentsTable({
  templateId, rows, onChanged,
}: {
  templateId: string; rows: TemplateAssignmentsDto['assignments']; onChanged: () => void;
}) {
  const toggle = useMutation({
    mutationFn: (r: TemplateAssignmentsDto['assignments'][number]) =>
      api.put(`/report-templates/${templateId}/assignments/${r.id}`, { isActive: !r.isActive, notes: r.notes }),
    onSuccess: onChanged,
  });
  const remove = useMutation({
    mutationFn: (rowId: string) => api.delete(`/report-templates/${templateId}/assignments/${rowId}`),
    onSuccess: onChanged,
  });

  if (rows.length === 0) return <p className="py-6 text-center text-sm text-ink-2">لا توجد إسنادات صريحة. استخدم النموذج أعلاه لإضافة واحد.</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[640px] text-right text-sm">
        <thead className="border-b border-line text-xs text-ink-2">
          <tr>
            <th className="px-2 py-2 font-semibold">النطاق</th>
            <th className="px-2 py-2 font-semibold">الكيان</th>
            <th className="px-2 py-2 font-semibold">النوع</th>
            <th className="px-2 py-2 font-semibold">ملاحظة</th>
            <th className="px-2 py-2 font-semibold">الحالة</th>
            <th className="px-2 py-2 font-semibold">إجراءات</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id} className="border-b border-line last:border-0">
              <td className="px-2 py-2 text-ink-2">{scopeLabel[r.scopeType]}</td>
              <td className="px-2 py-2 font-medium text-navy">{r.scopeName ?? r.scopeId}</td>
              <td className="px-2 py-2">
                <Badge tone={r.kind === 'Include' ? 'success' : 'alert'}>{r.kind === 'Include' ? 'إسناد' : 'استثناء'}</Badge>
              </td>
              <td className="px-2 py-2 text-ink-2">{r.notes ?? '—'}</td>
              <td className="px-2 py-2"><Badge tone={r.isActive ? 'success' : 'muted'}>{r.isActive ? 'مُفعّل' : 'معطّل'}</Badge></td>
              <td className="px-2 py-2">
                <div className="flex gap-2">
                  <Button variant="ghost" onClick={() => toggle.mutate(r)} disabled={toggle.isPending}>
                    {r.isActive ? 'تعطيل' : 'تفعيل'}
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => { if (window.confirm('حذف هذا الإسناد نهائيًّا؟')) remove.mutate(r.id); }}
                    disabled={remove.isPending}
                  >
                    حذف
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// جدول التعارضات: موظّف + هذا القالب + القالب الآخر + السبب + الحل المقترح.
function ConflictTable({ conflicts }: { conflicts: TemplateAssignmentsDto['conflicts'] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[680px] text-right text-sm">
        <thead className="border-b border-line text-xs text-ink-2">
          <tr>
            <th className="px-2 py-2 font-semibold">الموظف</th>
            <th className="px-2 py-2 font-semibold">القالب الحالي</th>
            <th className="px-2 py-2 font-semibold">القالب الآخر</th>
            <th className="px-2 py-2 font-semibold">الدورية</th>
            <th className="px-2 py-2 font-semibold">الحل المقترح</th>
          </tr>
        </thead>
        <tbody>
          {conflicts.map((c) => (
            <tr key={`${c.userId}-${c.otherTemplateId}`} className="border-b border-line last:border-0">
              <td className="px-2 py-2 font-medium text-navy">{c.fullName}</td>
              <td className="px-2 py-2 text-ink-2">{c.thisTemplateTitle}</td>
              <td className="px-2 py-2 text-ink-2">{c.otherTemplateTitle}</td>
              <td className="px-2 py-2 text-ink-2">{periodTypeLabel[c.periodType]}</td>
              <td className="px-2 py-2 text-ink-2">{c.suggestedResolution}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AssignmentUserTable({
  users, mode, emptyText,
}: {
  users: TemplateAssignmentsDto['matchedUsers']; mode: 'match' | 'exclude'; emptyText: string;
}) {
  if (users.length === 0) return <p className="py-6 text-center text-sm text-ink-2">{emptyText}</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[520px] text-right text-sm">
        <thead className="border-b border-line text-xs text-ink-2">
          <tr>
            <th className="px-2 py-2 font-semibold">الموظف</th>
            <th className="px-2 py-2 font-semibold">المسمى الوظيفي</th>
            <th className="px-2 py-2 font-semibold">الحالة</th>
            <th className="px-2 py-2 font-semibold">{mode === 'exclude' ? 'سبب الاستثناء' : 'سبب الربط'}</th>
          </tr>
        </thead>
        <tbody>
          {users.map((u) => (
            <tr key={u.userId} className="border-b border-line last:border-0">
              <td className="px-2 py-2">
                <div className="font-medium text-navy">{u.fullName}</div>
                {u.email && <div className="text-xs text-ink-3">{u.email}</div>}
              </td>
              <td className="px-2 py-2 text-ink-2">{u.jobRoleName ?? 'غير محدد'}</td>
              <td className="px-2 py-2"><Badge tone={u.isActive ? 'success' : 'muted'}>{u.isActive ? 'نشط' : 'موقوف'}</Badge></td>
              <td className="px-2 py-2 text-ink-2">
                {mode === 'exclude'
                  ? (u.exclusionReason ? (exclusionReasonLabel[u.exclusionReason] ?? u.exclusionReason) : '—')
                  : (u.matchReason ? (matchReasonLabel[u.matchReason] ?? u.matchReason) : '—')}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// عرض كل النسخ (إصدارات) القالب وحقولها + عدد التقارير المرتبطة وحذف النسخ غير المستخدَمة فقط.
function VersionsList({ versions, onChanged, setErr }: { versions: TemplateVersionDto[]; onChanged: () => void; setErr: (s: string | null) => void }) {
  const ordered = [...versions].sort((a, b) => b.versionNumber - a.versionNumber);
  const del = useMutation({
    mutationFn: (versionId: string) => api.delete(`/report-templates/versions/${versionId}`),
    onSuccess: () => { setErr(null); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  return (
    <div className="space-y-4">
      <p className="text-sm text-ink-2">
        لا يمكن حذف نسخة مستخدَمة في تقارير سابقة، ولا النسخة الوحيدة، ولا الأحدث، ولا النسخة المنشورة الحالية — حفاظًا على التقارير القديمة.
      </p>
      {ordered.map((v) => (
        <Card key={v.id}>
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <h3 className="text-base font-bold text-navy">الإصدار v{v.versionNumber}</h3>
            <Badge tone={v.isPublished ? 'success' : 'gold'}>{v.isPublished ? 'منشور' : 'مسودة'}</Badge>
            {v.isCurrentPublished && <Badge tone="muted">النسخة الحالية</Badge>}
            {v.publishedAtUtc && <span className="text-xs text-ink-2">نُشر {formatDate(v.publishedAtUtc)}</span>}
            <span className="text-xs text-ink-3">· {v.fields.length} حقل</span>
            <span className="text-xs text-ink-3">· مرتبط بـ {v.submissionCount} تقرير</span>
            <div className="ms-auto flex items-center gap-2">
              <Button
                variant="danger"
                disabled={!v.canDelete || del.isPending}
                title={v.canDelete ? undefined : v.deleteBlockReason ?? undefined}
                onClick={() => {
                  setErr(null);
                  if (window.confirm('هل تريد حذف هذه النسخة؟ لا يمكن التراجع عن هذا الإجراء.')) del.mutate(v.id);
                }}
              >
                حذف النسخة
              </Button>
            </div>
          </div>
          {!v.canDelete && v.deleteBlockReason && (
            <p className="mb-2 text-xs text-ink-3">{v.deleteBlockReason}</p>
          )}
          {v.fields.length === 0 ? (
            <p className="text-sm text-ink-2">لا توجد حقول في هذا الإصدار.</p>
          ) : (
            <ol className="space-y-1">
              {[...v.fields].sort((a, b) => a.order - b.order).map((f, i) => (
                <li key={f.id} className="flex items-center gap-2 rounded-lg border border-line bg-white px-3 py-1.5 text-sm">
                  <span className="text-xs text-ink-3">#{i + 1}</span>
                  <span className="font-medium text-navy">{f.label}</span>
                  <span className="text-xs text-ink-2">{fieldTypeLabel[f.fieldType]}</span>
                  {f.isRequired && <Badge tone="alert">مطلوب</Badge>}
                </li>
              ))}
            </ol>
          )}
        </Card>
      ))}
    </div>
  );
}

function TemplateMetaEditor({ tpl, jobRoles, onSaved, setErr }: { tpl: ReportTemplateDetailDto; jobRoles: JobRoleDto[]; onSaved: () => void; setErr: (s: string | null) => void }) {
  const [title, setTitle] = useState(tpl.title);
  const [description, setDescription] = useState(tpl.description ?? '');
  const [periodType, setPeriodType] = useState<PeriodType>(tpl.defaultPeriodType);
  const [jobRoleId, setJobRoleId] = useState(tpl.jobRoleId ?? '');
  const [classification, setClassification] = useState<TemplateClassification>(tpl.classification);

  const save = useMutation({
    mutationFn: () =>
      api.put(`/report-templates/${tpl.id}`, {
        title,
        description: description.trim() || null,
        jobRoleId: jobRoleId || null,
        defaultPeriodType: periodType,
        classification,
      }),
    onSuccess: onSaved,
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <Card>
      <SectionTitle title="تعديل بيانات القالب" hint="يُعدّل العنوان والدورية والمسمى الوظيفي والوصف دون التأثير على الحقول أو الإصدارات." />
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-72"><Field label="اسم القالب"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field></div>
        <div className="w-44">
          <Field label="الدورية الافتراضية">
            <Select value={periodType} onChange={(e) => setPeriodType(e.target.value as PeriodType)}>
              {(Object.keys(periodTypeLabel) as PeriodType[]).map((p) => (
                <option key={p} value={p}>{periodTypeLabel[p]}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="w-56">
          <Field label="المسمى الوظيفي">
            <Select value={jobRoleId} onChange={(e) => setJobRoleId(e.target.value)}>
              <option value="">— غير محدد —</option>
              {jobRoles.filter((j) => j.isActive).map((j) => (
                <option key={j.id} value={j.id}>{j.nameAr}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="w-56">
          <Field label="تصنيف القالب">
            <Select value={classification} onChange={(e) => setClassification(e.target.value as TemplateClassification)}>
              {(Object.keys(templateClassificationLabel) as TemplateClassification[]).map((c) => (
                <option key={c} value={c}>{templateClassificationLabel[c]}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="w-full"><Field label="الوصف (اختياري)"><Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="وصف موجز للقالب" /></Field></div>
        <Button onClick={() => { setErr(null); save.mutate(); }} disabled={!title.trim() || save.isPending}>حفظ التعديلات</Button>
      </div>
    </Card>
  );
}

const FIELD_TYPES = Object.keys(fieldTypeLabel) as FieldType[];

function VersionEditor({ version, onChanged, setErr }: { version: TemplateVersionDto; onChanged: () => void; setErr: (s: string | null) => void }) {
  const locked = version.isPublished;
  const fields = [...version.fields].sort((a, b) => a.order - b.order);

  return (
    <Card>
      <SectionTitle
        title={`حقول الإصدار v${version.versionNumber}`}
        hint={locked ? 'الإصدار منشور — أنشئ إصدارًا جديدًا للتعديل.' : 'أضف الحقول ورتّبها ثم انشر الإصدار.'}
        action={version.publishedAtUtc ? <span className="text-xs text-ink-2">نُشر {formatDate(version.publishedAtUtc)}</span> : undefined}
      />

      {fields.length === 0 ? (
        <div className="py-8 text-center">
          <p className="text-sm font-medium text-ink-2">لا توجد حقول بعد.</p>
          <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
            {locked
              ? 'هذا الإصدار منشور بلا حقول. أنشئ إصدارًا جديدًا لإضافة الحقول.'
              : 'أضف أول حقل من النموذج أعلاه (نص، رقم، تاريخ، عنوان قسم…). يجب إضافة حقل واحد على الأقل قبل نشر الإصدار.'}
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {fields.map((f, i) => (
            <FieldRow
              key={f.id}
              field={f}
              index={i}
              total={fields.length}
              locked={locked}
              orderedIds={fields.map((x) => x.id)}
              versionId={version.id}
              onChanged={onChanged}
              setErr={setErr}
            />
          ))}
        </div>
      )}

      {!locked && <AddFieldForm versionId={version.id} onChanged={onChanged} setErr={setErr} />}
    </Card>
  );
}

// محرّر إعداد قسم المشاريع المتكرر: الحد الأدنى/الأقصى، إلزامية المشروع، والحقول الفرعية لكل مشروع.
function RepeatableSectionConfigEditor({
  field, onChanged, setErr,
}: { field: TemplateFieldDto; onChanged: () => void; setErr: (s: string | null) => void }) {
  const [config, setConfig] = useState<ProjectRepeatableConfig>(() => parseRepeatableConfig(field.configJson));
  const [open, setOpen] = useState(false);

  const save = useMutation({
    mutationFn: () => api.put(`/report-templates/fields/${field.id}`, {
      label: field.label, key: field.key, fieldType: field.fieldType,
      isRequired: field.isRequired, helpText: field.helpText, configJson: JSON.stringify(config),
    }),
    onSuccess: () => { onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const addSubField = () => {
    const existing = config.fields.map((f) => f.key);
    const sf: RepeatableSubField = { key: slugifyKey('حقل', existing), label: '', type: 'ShortText', required: false };
    setConfig({ ...config, fields: [...config.fields, sf] });
  };
  const updateSubField = (i: number, patch: Partial<RepeatableSubField>) => {
    const fields = config.fields.map((f, idx) => (idx === i ? { ...f, ...patch } : f));
    setConfig({ ...config, fields });
  };
  const removeSubField = (i: number) => setConfig({ ...config, fields: config.fields.filter((_, idx) => idx !== i) });

  return (
    <div className="mt-1 rounded-lg border border-line bg-offwhite p-3">
      <button onClick={() => setOpen((v) => !v)} className="text-sm font-medium text-navy">
        {open ? '▾' : '▸'} إعداد قسم المشاريع المتكرر ({config.fields.length} حقل فرعي)
      </button>
      {open && (
        <div className="mt-3 space-y-3">
          <div className="flex flex-wrap items-end gap-3">
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="checkbox" checked={config.projectRequired}
                onChange={(e) => setConfig({ ...config, projectRequired: e.target.checked })} />
              اختيار المشروع إلزامي
            </label>
            <div className="w-28"><Field label="حد أدنى للمشاريع">
              <Input type="number" min={0} value={config.minProjects}
                onChange={(e) => setConfig({ ...config, minProjects: Math.max(0, Number(e.target.value) || 0) })} />
            </Field></div>
            <div className="w-28"><Field label="حد أقصى (0=بلا حد)">
              <Input type="number" min={0} value={config.maxProjects}
                onChange={(e) => setConfig({ ...config, maxProjects: Math.max(0, Number(e.target.value) || 0) })} />
            </Field></div>
          </div>

          <div className="space-y-2">
            <p className="text-xs font-bold text-ink-2">الحقول الفرعية لكل مشروع</p>
            {config.fields.length === 0 && <p className="text-xs text-ink-3">لا توجد حقول فرعية بعد.</p>}
            {config.fields.map((sf, i) => (
              <div key={i} className="flex flex-wrap items-end gap-2 rounded-lg border border-line bg-white p-2">
                <div className="w-48"><Field label="التسمية">
                  <Input value={sf.label} onChange={(e) => {
                    const existing = config.fields.filter((_, idx) => idx !== i).map((f) => f.key);
                    updateSubField(i, { label: e.target.value, key: slugifyKey(e.target.value, existing) });
                  }} />
                </Field></div>
                <div className="w-36"><Field label="النوع">
                  <Select value={sf.type} onChange={(e) => {
                    const type = e.target.value as RepeatableSubFieldType;
                    // عند التحويل إلى/من جدول أو قائمة، هيّئ/امسح أعمدته/خياراته.
                    if (type === 'Grid') {
                      updateSubField(i, { type, columns: sf.columns?.length ? sf.columns : ['العمود 1'], options: undefined });
                    } else if (type === 'Select') {
                      updateSubField(i, { type, options: sf.options?.length ? sf.options : ['خيار 1'], columns: undefined });
                    } else {
                      updateSubField(i, { type, columns: undefined, options: undefined });
                    }
                  }}>
                    {REPEATABLE_SUBFIELD_TYPES.map((t) => <option key={t} value={t}>{repeatableSubTypeLabel(t)}</option>)}
                  </Select>
                </Field></div>
                <label className="mb-2 flex items-center gap-1.5 text-sm text-ink">
                  <input type="checkbox" checked={sf.required} onChange={(e) => updateSubField(i, { required: e.target.checked })} /> مطلوب
                </label>
                <Button variant="danger" onClick={() => removeSubField(i)}>حذف</Button>
                {sf.type === 'Grid' && (
                  <div className="w-full">
                    <p className="mb-1 text-xs font-bold text-ink-2">أعمدة الجدول</p>
                    <div className="flex flex-wrap items-end gap-2">
                      {(sf.columns ?? []).map((col, ci) => (
                        <div key={ci} className="flex items-end gap-1">
                          <div className="w-40"><Field label={`العمود ${ci + 1}`}>
                            <Input value={col} onChange={(e) => {
                              const columns = (sf.columns ?? []).map((c, idx) => (idx === ci ? e.target.value : c));
                              updateSubField(i, { columns });
                            }} />
                          </Field></div>
                          <Button variant="danger" onClick={() => {
                            const columns = (sf.columns ?? []).filter((_, idx) => idx !== ci);
                            updateSubField(i, { columns });
                          }}>×</Button>
                        </div>
                      ))}
                      <Button variant="ghost" onClick={() => {
                        const columns = [...(sf.columns ?? []), `العمود ${(sf.columns?.length ?? 0) + 1}`];
                        updateSubField(i, { columns });
                      }}>+ عمود</Button>
                    </div>
                  </div>
                )}
                {sf.type === 'Select' && (
                  <div className="w-full">
                    <p className="mb-1 text-xs font-bold text-ink-2">خيارات القائمة</p>
                    <div className="flex flex-wrap items-end gap-2">
                      {(sf.options ?? []).map((opt, oi) => (
                        <div key={oi} className="flex items-end gap-1">
                          <div className="w-40"><Field label={`خيار ${oi + 1}`}>
                            <Input value={opt} onChange={(e) => {
                              const options = (sf.options ?? []).map((o, idx) => (idx === oi ? e.target.value : o));
                              updateSubField(i, { options });
                            }} />
                          </Field></div>
                          <Button variant="danger" onClick={() => {
                            const options = (sf.options ?? []).filter((_, idx) => idx !== oi);
                            updateSubField(i, { options });
                          }}>×</Button>
                        </div>
                      ))}
                      <Button variant="ghost" onClick={() => {
                        const options = [...(sf.options ?? []), `خيار ${(sf.options?.length ?? 0) + 1}`];
                        updateSubField(i, { options });
                      }}>+ خيار</Button>
                    </div>
                  </div>
                )}
              </div>
            ))}
            <Button variant="ghost" onClick={addSubField}>+ إضافة حقل فرعي</Button>
          </div>

          <div className="border-t border-line pt-2">
            <Button onClick={() => { setErr(null); save.mutate(); }}
              disabled={save.isPending || config.fields.some((f) => !f.label.trim())}>
              حفظ إعداد القسم
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function FieldRow({
  field, index, total, locked, orderedIds, versionId, onChanged, setErr,
}: {
  field: TemplateFieldDto; index: number; total: number; locked: boolean;
  orderedIds: string[]; versionId: string; onChanged: () => void; setErr: (s: string | null) => void;
}) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [label, setLabel] = useState(field.label);
  const [fieldType, setFieldType] = useState<FieldType>(field.fieldType);
  const [isRequired, setIsRequired] = useState(field.isRequired);

  const update = useMutation({
    mutationFn: () => api.put(`/report-templates/fields/${field.id}`, { label, key: field.key, fieldType, isRequired, helpText: field.helpText, configJson: field.configJson }),
    onSuccess: () => { setEditing(false); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const del = useMutation({
    mutationFn: () => api.delete(`/report-templates/fields/${field.id}`),
    onSuccess: onChanged,
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const reorder = useMutation({
    mutationFn: (ids: string[]) => api.post(`/report-templates/versions/${versionId}/reorder`, ids),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['report-template'] }); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  const move = (dir: -1 | 1) => {
    const ids = [...orderedIds];
    const j = index + dir;
    if (j < 0 || j >= ids.length) return;
    [ids[index], ids[j]] = [ids[j], ids[index]];
    reorder.mutate(ids);
  };

  if (editing) {
    return (
      <div className="flex flex-wrap items-end gap-2 rounded-lg border border-line bg-offwhite p-3">
        <div className="w-56"><Field label="التسمية"><Input value={label} onChange={(e) => setLabel(e.target.value)} /></Field></div>
        <div className="w-44">
          <Field label="النوع">
            <Select value={fieldType} onChange={(e) => setFieldType(e.target.value as FieldType)}>
              {FIELD_TYPES.map((t) => <option key={t} value={t}>{fieldTypeLabel[t]}</option>)}
            </Select>
          </Field>
        </div>
        <label className="mb-2 flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={isRequired} onChange={(e) => setIsRequired(e.target.checked)} /> مطلوب
        </label>
        <Button onClick={() => { setErr(null); update.mutate(); }} disabled={update.isPending}>حفظ</Button>
        <Button variant="ghost" onClick={() => setEditing(false)}>إلغاء</Button>
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between gap-2 rounded-lg border border-line bg-white px-3 py-2">
        <div className="flex items-center gap-3">
          <span className="text-xs text-ink-3">#{index + 1}</span>
          <div>
            <span className="font-medium text-navy">{field.label}</span>
            {field.fieldType === 'SectionHeader' && <Badge tone="navy">عنوان قسم</Badge>}
            {field.fieldType === 'ProjectRepeatableSection' && <Badge tone="gold">قسم مشاريع</Badge>}
          </div>
          <span className="text-xs text-ink-2">{fieldTypeLabel[field.fieldType]}</span>
          {field.isRequired && <Badge tone="alert">مطلوب</Badge>}
        </div>
        {!locked && (
          <div className="flex items-center gap-1">
            <button onClick={() => move(-1)} disabled={index === 0} className="px-1.5 text-ink-2 hover:text-navy disabled:opacity-30">↑</button>
            <button onClick={() => move(1)} disabled={index === total - 1} className="px-1.5 text-ink-2 hover:text-navy disabled:opacity-30">↓</button>
            <Button variant="ghost" onClick={() => setEditing(true)}>تعديل</Button>
            <Button variant="danger" onClick={() => { setErr(null); del.mutate(); }} disabled={del.isPending}>حذف</Button>
          </div>
        )}
      </div>
      {!locked && field.fieldType === 'ProjectRepeatableSection' && (
        <RepeatableSectionConfigEditor field={field} onChanged={onChanged} setErr={setErr} />
      )}
    </div>
  );
}

function AddFieldForm({ versionId, onChanged, setErr }: { versionId: string; onChanged: () => void; setErr: (s: string | null) => void }) {
  const [label, setLabel] = useState('');
  const [fieldType, setFieldType] = useState<FieldType>('ShortText');
  const [isRequired, setIsRequired] = useState(false);

  const add = useMutation({
    mutationFn: () => api.post(`/report-templates/versions/${versionId}/fields`, { label, key: null, fieldType, isRequired, helpText: null, configJson: null }),
    onSuccess: () => { setLabel(''); setIsRequired(false); setFieldType('ShortText'); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <div className="mt-4 flex flex-wrap items-end gap-2 border-t border-line pt-4">
      <div className="w-56"><Field label="حقل جديد"><Input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="اسم الحقل" /></Field></div>
      <div className="w-44">
        <Field label="النوع">
          <Select value={fieldType} onChange={(e) => setFieldType(e.target.value as FieldType)}>
            {FIELD_TYPES.map((t) => <option key={t} value={t}>{fieldTypeLabel[t]}</option>)}
          </Select>
        </Field>
      </div>
      <label className="mb-2 flex items-center gap-2 text-sm text-ink">
        <input type="checkbox" checked={isRequired} onChange={(e) => setIsRequired(e.target.checked)} /> مطلوب
      </label>
      <Button onClick={() => { setErr(null); add.mutate(); }} disabled={!label.trim() || add.isPending}>إضافة الحقل</Button>
    </div>
  );
}

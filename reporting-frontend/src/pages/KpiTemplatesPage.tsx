// قوالب KPI — قائمة + إنشاء + إدارة المؤشرات (اسم، وزن، طريقة الحساب، المستهدف) + نشر (يتطلب مجموع الأوزان = 100).
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { useJobRoles, useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { templateStatusLabel, kpiCadenceLabel, kpiCalcMethodLabel } from '../lib/format';
import type {
  KpiTemplateDto,
  KpiTemplateDetailDto,
  KpiTemplateVersionDto,
  KpiMetricDto,
  KpiCadence,
  KpiCalcMethod,
  TemplateStatus,
  KpiTemplateAssignmentsDto,
  CreateKpiAssignmentRequest,
  TemplateAssignmentScope,
  TemplateAssignmentKind,
} from '../types/api';

// تسميات أسباب استثناء المستخدم من تغطية قالب KPI.
const exclusionReasonLabel: Record<string, string> = {
  excludedBecauseInactive: 'حساب موقوف',
  excludedBecauseMoreSpecificTemplateExists: 'مستثنى — يوجد قالب أخصّ لنفس الدورية',
  excludedBecauseTemplateNotAssignable: 'القالب غير منشور/غير نشط حاليًا',
  excludedManually: 'مستثنى يدويًّا (صريح)',
};

// تسميات سبب ربط المستخدم بقالب KPI.
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

export default function KpiTemplatesPage() {
  const [openId, setOpenId] = useState<string | null>(null);
  if (openId) return <KpiTemplateDetail id={openId} onBack={() => setOpenId(null)} />;
  return <KpiTemplateList onOpen={setOpenId} />;
}

function KpiTemplateList({ onOpen }: { onOpen: (id: string) => void }) {
  const qc = useQueryClient();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-templates-all'],
    queryFn: async () => (await api.get<KpiTemplateDto[]>('/kpi-templates')).data,
  });

  const [title, setTitle] = useState('');
  // Phase 4: تقييم KPI أسبوعي فقط حاليًا — الافتراضي «النبض الأسبوعي».
  const [cadence, setCadence] = useState<KpiCadence>('WeeklyPulse');
  const [jobRoleId, setJobRoleId] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const jobRoles = useJobRoles(true);

  const create = useMutation({
    mutationFn: () => api.post<KpiTemplateDetailDto>('/kpi-templates', { title, description: null, jobRoleId: jobRoleId || null, cadence }),
    onSuccess: (res) => {
      setTitle('');
      setJobRoleId('');
      void qc.invalidateQueries({ queryKey: ['kpi-templates-all'] });
      onOpen(res.data.id);
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل قوالب المؤشّرات…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب قوالب المؤشّرات. أعد المحاولة." />;
  const items = data ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">قوالب KPI</h1>
        <p className="mt-1 text-sm text-ink-2">عرّف المؤشرات وأوزانها وطريقة الحساب، ثم انشر القالب لاستخدامه في التقييمات.</p>
      </div>

      <Card>
        <SectionTitle title="إنشاء قالب KPI" />
        <div className="mb-3">
          <Alert tone="navy">
            تقييم KPI الحالي أسبوعي فقط. التجميع الشهري والربع سنوي والسنوي سيتم في Phase 5. لا يمكن نشر/تفعيل قالب KPI بدورية غير أسبوعية.
          </Alert>
        </div>
        {err && <div className="mb-3"><Alert tone="alert">{err}</Alert></div>}
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-72"><Field label="اسم القالب"><Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="مثال: تقييم مندوب المبيعات" /></Field></div>
          <div className="w-56">
            <Field label="المسمى الوظيفي">
              <Select value={jobRoleId} onChange={(e) => setJobRoleId(e.target.value)}>
                <option value="">قالب عام (بدون مسمى)</option>
                {(jobRoles.data ?? []).map((r) => (
                  <option key={r.id} value={r.id}>{r.nameAr}</option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="w-44">
            <Field label="الدورية">
              <Select value={cadence} onChange={(e) => setCadence(e.target.value as KpiCadence)}>
                {(Object.keys(kpiCadenceLabel) as KpiCadence[]).map((c) => (
                  <option key={c} value={c}>{kpiCadenceLabel[c]}</option>
                ))}
              </Select>
            </Field>
          </div>
          <Button disabled={!title.trim() || create.isPending} onClick={() => { setErr(null); create.mutate(); }}>
            إنشاء وإضافة المؤشرات
          </Button>
        </div>
        <p className="mt-2 text-xs text-ink-3">
          إذا لم تختر مسمى وظيفيًا، سيعمل القالب كقالب عام للموظفين الذين لا يوجد لهم قالب KPI أخصّ بمسمّاهم.
        </p>
      </Card>

      <Card>
        <SectionTitle title={`القوالب (${items.length})`} />
        {items.length === 0 ? (
          <div className="py-10 text-center">
            <p className="text-sm font-medium text-ink-2">لا توجد قوالب مؤشّرات أداء.</p>
            <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
              قوالب المؤشّرات تحدّد المقاييس وأوزانها لتقييم الأداء. أنشئ قالبًا جديدًا من زر «قالب جديد» لبدء تقييم الفِرق.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px] text-right text-sm">
              <thead className="border-b border-line text-xs text-ink-2">
                <tr>
                  <th className="px-2 py-2 font-semibold">القالب</th>
                  <th className="px-2 py-2 font-semibold">الدورية</th>
                  <th className="px-2 py-2 font-semibold">المؤشرات</th>
                  <th className="px-2 py-2 font-semibold">الحالة</th>
                  <th className="px-2 py-2 font-semibold"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => (
                  <tr key={t.id} className="border-b border-line last:border-0">
                    <td className="px-2 py-2 font-medium text-navy">{t.title}</td>
                    <td className="px-2 py-2 text-ink-2">{kpiCadenceLabel[t.cadence]}</td>
                    <td className="px-2 py-2">{t.metricCount}</td>
                    <td className="px-2 py-2"><Badge tone={statusTone[t.status]}>{templateStatusLabel[t.status]}</Badge></td>
                    <td className="px-2 py-2 text-left">
                      <Button variant="ghost" onClick={() => onOpen(t.id)}>إدارة المؤشرات</Button>
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

function KpiTemplateDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { hasAnyRole } = useAuth();
  const isAdmin = hasAnyRole('Admin');
  const { data: tpl, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-template', id],
    queryFn: async () => (await api.get<KpiTemplateDetailDto>(`/kpi-templates/${id}`)).data,
  });
  const [err, setErr] = useState<string | null>(null);
  const [editMeta, setEditMeta] = useState(false);
  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ['kpi-template', id] });
    void qc.invalidateQueries({ queryKey: ['kpi-templates-all'] });
  };

  const publish = useMutation({
    mutationFn: (versionId: string) => api.post(`/kpi-templates/versions/${versionId}/publish`),
    onSuccess: invalidate,
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const newVersion = useMutation({
    mutationFn: () => api.post(`/kpi-templates/${id}/versions`),
    onSuccess: invalidate,
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const archive = useMutation({
    mutationFn: () => api.delete(`/kpi-templates/${id}`),
    onSuccess: () => { invalidate(); onBack(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const reactivate = useMutation({
    mutationFn: () => api.post(`/kpi-templates/${id}/reactivate`),
    onSuccess: invalidate,
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (isLoading) return <LoadingState label="يتم تحميل القالب…" />;
  if (isError || !tpl)
    return <QueryError onRetry={() => refetch()} title="تعذّر تحميل القالب" description="حدث خطأ أثناء جلب تفاصيل القالب. أعد المحاولة." />;
  const latest = [...tpl.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0];

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع للقوالب</button>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-navy">{tpl.title}</h1>
          <Badge tone={statusTone[tpl.status]}>{templateStatusLabel[tpl.status]}</Badge>
        </div>
        <div className="flex gap-2">
          {tpl.status !== 'Archived' && (
            <Button variant="ghost" onClick={() => { setErr(null); setEditMeta((v) => !v); }}>
              {editMeta ? 'إغلاق التعديل' : 'تعديل بيانات القالب'}
            </Button>
          )}
          {latest && !latest.isPublished && (
            <Button onClick={() => { setErr(null); publish.mutate(latest.id); }} disabled={publish.isPending}>
              نشر الإصدار v{latest.versionNumber}
            </Button>
          )}
          {latest && latest.isPublished && (
            <Button variant="ghost" onClick={() => { setErr(null); newVersion.mutate(); }} disabled={newVersion.isPending}>
              إصدار جديد (نسخ)
            </Button>
          )}
          {tpl.status !== 'Archived' && (
            <Button variant="danger" onClick={() => { setErr(null); archive.mutate(); }} disabled={archive.isPending}>أرشفة</Button>
          )}
          {tpl.status === 'Archived' && (
            <Button onClick={() => { setErr(null); reactivate.mutate(); }} disabled={reactivate.isPending}>إعادة تفعيل</Button>
          )}
        </div>
      </div>
      <p className="text-sm text-ink-2">{kpiCadenceLabel[tpl.cadence]} · الإصدارات: {tpl.versions.length}</p>
      {err && <Alert tone="alert">{err}</Alert>}

      {editMeta && <KpiTemplateMetaEditor tpl={tpl} onSaved={() => { setEditMeta(false); invalidate(); }} setErr={setErr} />}

      {latest && <VersionEditor version={latest} onChanged={invalidate} setErr={setErr} />}

      {isAdmin && <KpiAssignmentsPanel id={id} />}
    </div>
  );
}

function KpiTemplateMetaEditor({ tpl, onSaved, setErr }: { tpl: KpiTemplateDetailDto; onSaved: () => void; setErr: (s: string | null) => void }) {
  const [title, setTitle] = useState(tpl.title);
  const [description, setDescription] = useState(tpl.description ?? '');
  const [cadence, setCadence] = useState<KpiCadence>(tpl.cadence);
  const [jobRoleId, setJobRoleId] = useState(tpl.jobRoleId ?? '');
  const jobRoles = useJobRoles(true);

  const save = useMutation({
    mutationFn: () =>
      api.put(`/kpi-templates/${tpl.id}`, {
        title,
        description: description.trim() || null,
        jobRoleId: jobRoleId || null,
        cadence,
      }),
    onSuccess: onSaved,
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  // المسمّى الحالي قد يكون مؤرشفًا فلا يظهر ضمن النشطة — نضمن إدراجه حتى لا يُفقد عند الحفظ.
  const roles = jobRoles.data ?? [];
  const currentMissing = tpl.jobRoleId && !roles.some((r) => r.id === tpl.jobRoleId);

  return (
    <Card>
      <SectionTitle title="تعديل بيانات القالب" hint="يُعدّل العنوان والمسمى والدورية والوصف دون التأثير على المؤشرات أو الإصدارات." />
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-72"><Field label="اسم القالب"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field></div>
        <div className="w-56">
          <Field label="المسمى الوظيفي">
            <Select value={jobRoleId} onChange={(e) => setJobRoleId(e.target.value)}>
              <option value="">قالب عام (بدون مسمى)</option>
              {currentMissing && <option value={tpl.jobRoleId!}>المسمى الحالي (مؤرشف)</option>}
              {roles.map((r) => (
                <option key={r.id} value={r.id}>{r.nameAr}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="w-44">
          <Field label="الدورية">
            <Select value={cadence} onChange={(e) => setCadence(e.target.value as KpiCadence)}>
              {(Object.keys(kpiCadenceLabel) as KpiCadence[]).map((c) => (
                <option key={c} value={c}>{kpiCadenceLabel[c]}</option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="w-full"><Field label="الوصف (اختياري)"><Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="وصف موجز للقالب" /></Field></div>
        <Button onClick={() => { setErr(null); save.mutate(); }} disabled={!title.trim() || save.isPending}>حفظ التعديلات</Button>
      </div>
      <p className="mt-2 text-xs text-ink-3">
        قالب عام = يظهر للموظفين الذين لا يوجد لهم قالب KPI مرتبط بمسمّاهم الوظيفي مباشرة.
      </p>
    </Card>
  );
}

const CALC_METHODS = Object.keys(kpiCalcMethodLabel) as KpiCalcMethod[];

function VersionEditor({ version, onChanged, setErr }: { version: KpiTemplateVersionDto; onChanged: () => void; setErr: (s: string | null) => void }) {
  const locked = version.isPublished;
  const metrics = [...version.metrics].sort((a, b) => a.order - b.order);
  const total = metrics.reduce((s, m) => s + m.weight, 0);
  const weightOk = total === 100;

  return (
    <Card>
      <SectionTitle
        title={`مؤشرات الإصدار v${version.versionNumber}`}
        hint={locked ? 'الإصدار منشور — أنشئ إصدارًا جديدًا للتعديل.' : 'يجب أن يكون مجموع الأوزان = 100 قبل النشر.'}
        action={
          <Badge tone={weightOk ? 'success' : 'alert'}>مجموع الأوزان: {total}</Badge>
        }
      />

      {metrics.length === 0 ? (
        <div className="py-8 text-center">
          <p className="text-sm font-medium text-ink-2">لا توجد مؤشرات بعد.</p>
          <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">
            {locked
              ? 'هذا الإصدار منشور بلا مؤشّرات. أنشئ إصدارًا جديدًا لإضافة المؤشّرات.'
              : 'أضف المؤشّرات من النموذج أعلاه، واحرص أن يكون مجموع أوزانها = 100 قبل نشر الإصدار.'}
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {metrics.map((m) => (
            <MetricRow key={m.id} metric={m} locked={locked} onChanged={onChanged} setErr={setErr} />
          ))}
        </div>
      )}

      {!locked && <AddMetricForm versionId={version.id} onChanged={onChanged} setErr={setErr} />}
    </Card>
  );
}

function MetricRow({ metric, locked, onChanged, setErr }: { metric: KpiMetricDto; locked: boolean; onChanged: () => void; setErr: (s: string | null) => void }) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(metric.name);
  const [weight, setWeight] = useState(String(metric.weight));
  const [calcMethod, setCalcMethod] = useState<KpiCalcMethod>(metric.calcMethod);
  const [target, setTarget] = useState(metric.targetValue?.toString() ?? '');
  const [unit, setUnit] = useState(metric.unit ?? '');

  const update = useMutation({
    mutationFn: () => api.put(`/kpi-templates/metrics/${metric.id}`, {
      name, description: metric.description, weight: Number(weight),
      targetValue: target ? Number(target) : null, unit: unit || null,
      calcMethod, calcConfigJson: metric.calcConfigJson,
    }),
    onSuccess: () => { setEditing(false); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });
  const del = useMutation({
    mutationFn: () => api.delete(`/kpi-templates/metrics/${metric.id}`),
    onSuccess: onChanged,
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  if (editing) {
    return (
      <div className="flex flex-wrap items-end gap-2 rounded-lg border border-line bg-offwhite p-3">
        <div className="w-48"><Field label="المؤشر"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field></div>
        <div className="w-24"><Field label="الوزن"><Input type="number" value={weight} onChange={(e) => setWeight(e.target.value)} /></Field></div>
        <div className="w-40">
          <Field label="طريقة الحساب">
            <Select value={calcMethod} onChange={(e) => setCalcMethod(e.target.value as KpiCalcMethod)}>
              {CALC_METHODS.map((c) => <option key={c} value={c}>{kpiCalcMethodLabel[c]}</option>)}
            </Select>
          </Field>
        </div>
        <div className="w-24"><Field label="المستهدف"><Input type="number" value={target} onChange={(e) => setTarget(e.target.value)} /></Field></div>
        <div className="w-24"><Field label="الوحدة"><Input value={unit} onChange={(e) => setUnit(e.target.value)} /></Field></div>
        <Button onClick={() => { setErr(null); update.mutate(); }} disabled={update.isPending}>حفظ</Button>
        <Button variant="ghost" onClick={() => setEditing(false)}>إلغاء</Button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-line bg-white px-3 py-2">
      <div className="flex flex-wrap items-center gap-3">
        <span className="font-medium text-navy">{metric.name}</span>
        <Badge tone="navy">وزن {metric.weight}</Badge>
        <span className="text-xs text-ink-2">{kpiCalcMethodLabel[metric.calcMethod]}</span>
        {metric.targetValue != null && <span className="text-xs text-ink-2">مستهدف {metric.targetValue}{metric.unit ? ` ${metric.unit}` : ''}</span>}
      </div>
      {!locked && (
        <div className="flex items-center gap-1">
          <Button variant="ghost" onClick={() => setEditing(true)}>تعديل</Button>
          <Button variant="danger" onClick={() => { setErr(null); del.mutate(); }} disabled={del.isPending}>حذف</Button>
        </div>
      )}
    </div>
  );
}

function AddMetricForm({ versionId, onChanged, setErr }: { versionId: string; onChanged: () => void; setErr: (s: string | null) => void }) {
  const [name, setName] = useState('');
  const [weight, setWeight] = useState('');
  const [calcMethod, setCalcMethod] = useState<KpiCalcMethod>('Manual');
  const [target, setTarget] = useState('');
  const [unit, setUnit] = useState('');

  const add = useMutation({
    mutationFn: () => api.post(`/kpi-templates/versions/${versionId}/metrics`, {
      name, description: null, weight: Number(weight),
      targetValue: target ? Number(target) : null, unit: unit || null,
      calcMethod, calcConfigJson: null,
    }),
    onSuccess: () => { setName(''); setWeight(''); setTarget(''); setUnit(''); setCalcMethod('Manual'); onChanged(); },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <div className="mt-4 flex flex-wrap items-end gap-2 border-t border-line pt-4">
      <div className="w-48"><Field label="مؤشر جديد"><Input value={name} onChange={(e) => setName(e.target.value)} placeholder="اسم المؤشر" /></Field></div>
      <div className="w-24"><Field label="الوزن"><Input type="number" value={weight} onChange={(e) => setWeight(e.target.value)} /></Field></div>
      <div className="w-40">
        <Field label="طريقة الحساب">
          <Select value={calcMethod} onChange={(e) => setCalcMethod(e.target.value as KpiCalcMethod)}>
            {CALC_METHODS.map((c) => <option key={c} value={c}>{kpiCalcMethodLabel[c]}</option>)}
          </Select>
        </Field>
      </div>
      <div className="w-24"><Field label="المستهدف"><Input type="number" value={target} onChange={(e) => setTarget(e.target.value)} /></Field></div>
      <div className="w-24"><Field label="الوحدة"><Input value={unit} onChange={(e) => setUnit(e.target.value)} placeholder="٪" /></Field></div>
      <Button onClick={() => { setErr(null); add.mutate(); }} disabled={!name.trim() || !weight || add.isPending}>إضافة المؤشر</Button>
    </div>
  );
}

// ===== نطاق تطبيق قالب KPI (Phase T1) — إدارة الأدمن حصرًا =====
// يحاكي تبويب «تغطية قالب التقرير» لكن بلا تعارضات وبالدورية بدل الدورية/التصنيف.
function KpiAssignmentsPanel({ id }: { id: string }) {
  const qc = useQueryClient();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['kpi-template-assignments', id],
    queryFn: async () => (await api.get<KpiTemplateAssignmentsDto>(`/kpi-templates/${id}/assignments`)).data,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['kpi-template-assignments', id] });

  if (isLoading) return <LoadingState label="يتم حساب الموظفين المرتبطين…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} title="تعذّر حساب التغطية" description="حدث خطأ أثناء حساب الموظفين المرتبطين. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-navy">نطاق تطبيق القالب</h2>
        <p className="mt-1 text-sm text-ink-2">حدّد من ينطبق عليه هذا القالب في مسار التقييم (إسناد/استثناء على مستوى موظّف/مسمّى/فريق/إدارة). إدارة الأدمن حصرًا.</p>
      </div>

      <Card>
        <SectionTitle title="قاعدة الربط" hint="أولوية الخادم: استثناء/إسناد الموظّف ← المسمّى الوظيفي ← الفريق ← الإدارة ← القالب العام. أي استثناء يتفوّق على الإسناد في نفس المستوى أو أدنى — ضمن نفس الدورية." />
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs text-ink-2">نوع القالب</dt>
            <dd className="font-medium text-navy">{data.isRoleSpecific ? `متخصص — مسمّى: ${data.jobRoleName ?? 'غير محدد'}` : 'عام (لكل من لا يملك قالبًا أخصّ)'}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">الدورية</dt>
            <dd className="font-medium text-navy">{kpiCadenceLabel[data.cadence]}</dd>
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
          <div className="mt-3"><Alert tone="gold">القالب غير منشور/غير نشط، لذا لا يظهر لأي موظّف حاليًا في مسار التقييم. القائمة أدناه توضّح من سيظهر له عند نشره.</Alert></div>
        )}
      </Card>

      <AddKpiAssignmentForm templateId={id} onDone={invalidate} />

      <Card>
        <SectionTitle title={`الإسنادات/الاستثناءات الصريحة (${data.assignments.length})`} hint="الصفوف المُضافة يدويًّا لهذا القالب. يمكن تعطيلها أو حذفها." />
        <KpiExplicitAssignmentsTable templateId={id} rows={data.assignments} onChanged={invalidate} />
      </Card>

      <Card>
        <SectionTitle title={`الموظفون المرتبطون (${data.matchedUsers.length})`} hint="من ينطبق عليه هذا القالب فعليًّا في مسار التقييم، مع سبب الظهور لكل موظّف." />
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
        <KpiMatchedUsersManager
          templateId={id}
          users={data.matchedUsers}
          isRoleSpecific={data.isRoleSpecific}
          onChanged={invalidate}
        />
      </Card>

      <Card>
        <SectionTitle title={`الموظفون المستثنون (${data.excludedUsers.length})`} hint="مع سبب الاستثناء لكل موظّف." />
        <KpiAssignmentUserTable users={data.excludedUsers} emptyText="لا يوجد موظفون مستثنون." />
      </Card>
    </div>
  );
}

// نموذج إضافة إسناد أو استثناء صريح لقالب KPI (نطاق + كيان + نوع + ملاحظة).
function AddKpiAssignmentForm({ templateId, onDone }: { templateId: string; onDone: () => void }) {
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
      const body: CreateKpiAssignmentRequest = { scopeType, scopeId, kind, notes: notes.trim() || null };
      return api.post(`/kpi-templates/${templateId}/assignments`, body);
    },
    onSuccess: () => {
      setScopeId('');
      setNotes('');
      setErr(null);
      qc.invalidateQueries({ queryKey: ['kpi-template-assignments', templateId] });
      onDone();
    },
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <Card>
      <SectionTitle title="إضافة إسناد / استثناء" hint="اختر النطاق ثم الكيان، وحدّد إن كان إسنادًا (ينطبق عليه القالب) أو استثناءً (لا ينطبق)." />
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
            <option value="Include">إسناد (ينطبق عليه القالب)</option>
            <option value="Exclude">استثناء (لا ينطبق)</option>
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

// جدول الإسنادات/الاستثناءات الصريحة لقالب KPI مع تعطيل/تفعيل وحذف.
function KpiExplicitAssignmentsTable({
  templateId, rows, onChanged,
}: {
  templateId: string; rows: KpiTemplateAssignmentsDto['assignments']; onChanged: () => void;
}) {
  const toggle = useMutation({
    mutationFn: (r: KpiTemplateAssignmentsDto['assignments'][number]) =>
      api.put(`/kpi-templates/${templateId}/assignments/${r.id}`, { isActive: !r.isActive, notes: r.notes }),
    onSuccess: onChanged,
  });
  const remove = useMutation({
    mutationFn: (rowId: string) => api.delete(`/kpi-templates/${templateId}/assignments/${rowId}`),
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

// جدول الموظفين المرتبطين بأزرار استثناء سريعة + اختيار متعدّد. لا يغيّر منطق المطابقة بالخادم.
function KpiMatchedUsersManager({
  templateId, users, isRoleSpecific, onChanged,
}: {
  templateId: string; users: KpiTemplateAssignmentsDto['matchedUsers']; isRoleSpecific: boolean; onChanged: () => void;
}) {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const postExclude = async (scopeType: TemplateAssignmentScope, scopeId: string, notes: string) => {
    const body: CreateKpiAssignmentRequest = { scopeType, scopeId, kind: 'Exclude', notes };
    await api.post(`/kpi-templates/${templateId}/assignments`, body);
  };

  const afterChange = () => {
    setSelected(new Set());
    qc.invalidateQueries({ queryKey: ['kpi-template-assignments', templateId] });
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

// جدول الموظفين المستثنين (قراءة فقط) مع سبب الاستثناء.
function KpiAssignmentUserTable({
  users, emptyText,
}: {
  users: KpiTemplateAssignmentsDto['excludedUsers']; emptyText: string;
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
            <th className="px-2 py-2 font-semibold">سبب الاستثناء</th>
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
                {u.exclusionReason ? (exclusionReasonLabel[u.exclusionReason] ?? u.exclusionReason) : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

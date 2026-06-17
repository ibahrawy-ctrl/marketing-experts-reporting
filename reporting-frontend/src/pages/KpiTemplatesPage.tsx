// قوالب KPI — قائمة + إنشاء + إدارة المؤشرات (اسم، وزن، طريقة الحساب، المستهدف) + نشر (يتطلب مجموع الأوزان = 100).
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { templateStatusLabel, kpiCadenceLabel, kpiCalcMethodLabel } from '../lib/format';
import type {
  KpiTemplateDto,
  KpiTemplateDetailDto,
  KpiTemplateVersionDto,
  KpiMetricDto,
  KpiCadence,
  KpiCalcMethod,
  TemplateStatus,
} from '../types/api';

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
  const [err, setErr] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.post<KpiTemplateDetailDto>('/kpi-templates', { title, description: null, jobRoleId: null, cadence }),
    onSuccess: (res) => {
      setTitle('');
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
        </div>
      </div>
      <p className="text-sm text-ink-2">{kpiCadenceLabel[tpl.cadence]} · الإصدارات: {tpl.versions.length}</p>
      {err && <Alert tone="alert">{err}</Alert>}

      {editMeta && <KpiTemplateMetaEditor tpl={tpl} onSaved={() => { setEditMeta(false); invalidate(); }} setErr={setErr} />}

      {latest && <VersionEditor version={latest} onChanged={invalidate} setErr={setErr} />}
    </div>
  );
}

function KpiTemplateMetaEditor({ tpl, onSaved, setErr }: { tpl: KpiTemplateDetailDto; onSaved: () => void; setErr: (s: string | null) => void }) {
  const [title, setTitle] = useState(tpl.title);
  const [description, setDescription] = useState(tpl.description ?? '');
  const [cadence, setCadence] = useState<KpiCadence>(tpl.cadence);

  const save = useMutation({
    mutationFn: () =>
      api.put(`/kpi-templates/${tpl.id}`, {
        title,
        description: description.trim() || null,
        jobRoleId: tpl.jobRoleId,
        cadence,
      }),
    onSuccess: onSaved,
    onError: (e) => setErr(apiErrorMessage(e)),
  });

  return (
    <Card>
      <SectionTitle title="تعديل بيانات القالب" hint="يُعدّل العنوان والدورية والوصف دون التأثير على المؤشرات أو الإصدارات." />
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-72"><Field label="اسم القالب"><Input value={title} onChange={(e) => setTitle(e.target.value)} /></Field></div>
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

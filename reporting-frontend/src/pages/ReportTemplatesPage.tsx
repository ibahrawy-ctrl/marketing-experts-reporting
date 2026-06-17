// قوالب التقارير — قائمة + إنشاء/تعديل + باني الحقول (إضافة/تعديل/حذف/ترتيب) + نشر إصدار.
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { useJobRoles } from '../lib/useDirectory';
import { Alert, Badge, Button, Card, Field, Input, Select } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { fieldTypeLabel, templateStatusLabel, periodTypeLabel, templateClassificationLabel, formatDate } from '../lib/format';
import type {
  ReportTemplateListItem,
  ReportTemplateDetailDto,
  TemplateVersionDto,
  TemplateFieldDto,
  FieldType,
  PeriodType,
  TemplateStatus,
  TemplateClassification,
  JobRoleDto,
} from '../types/api';

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

  if (isLoading) return <LoadingState label="يتم تحميل قوالب التقارير…" />;
  if (isError) return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب قوالب التقارير. أعد المحاولة." />;
  const items = data ?? [];

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

function TemplateDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const jobRoles = useJobRoles();
  const { data: tpl, isLoading, isError, refetch } = useQuery({
    queryKey: ['report-template', id],
    queryFn: async () => (await api.get<ReportTemplateDetailDto>(`/report-templates/${id}`)).data,
  });
  const [err, setErr] = useState<string | null>(null);
  const [editMeta, setEditMeta] = useState(false);
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
  const archive = useMutation({
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

  return (
    <div className="space-y-6">
      <button onClick={onBack} className="text-sm font-semibold text-navy hover:text-orange">← رجوع للقوالب</button>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-navy">{tpl.title}</h1>
          <Badge tone={statusTone[tpl.status]}>{templateStatusLabel[tpl.status]}</Badge>
          <Badge tone={classificationTone[tpl.classification]}>{templateClassificationLabel[tpl.classification]}</Badge>
        </div>
        <div className="flex gap-2">
          {tpl.status !== 'Archived' && (
            <Button variant="ghost" onClick={() => { setErr(null); setEditMeta((v) => !v); }}>
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
            <Button variant="danger" onClick={() => { setErr(null); archive.mutate(); }} disabled={archive.isPending}>
              أرشفة
            </Button>
          )}
        </div>
      </div>
      <p className="text-sm text-ink-2">
        {periodTypeLabel[tpl.defaultPeriodType]} · الإصدارات: {tpl.versions.length}
        {' · '}المسمى الوظيفي: {jobRoleName ?? 'غير محدد'}
        {' · '}التصنيف: {templateClassificationLabel[tpl.classification]}
      </p>
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
      {err && <Alert tone="alert">{err}</Alert>}

      {editMeta && <TemplateMetaEditor tpl={tpl} jobRoles={jobRoles.data ?? []} onSaved={() => { setEditMeta(false); invalidate(); }} setErr={setErr} />}

      {latest && <VersionEditor version={latest} onChanged={invalidate} setErr={setErr} />}
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
    <div className="flex items-center justify-between gap-2 rounded-lg border border-line bg-white px-3 py-2">
      <div className="flex items-center gap-3">
        <span className="text-xs text-ink-3">#{index + 1}</span>
        <div>
          <span className="font-medium text-navy">{field.label}</span>
          {field.fieldType === 'SectionHeader' && <Badge tone="navy">عنوان قسم</Badge>}
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

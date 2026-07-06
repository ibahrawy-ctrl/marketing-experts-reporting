// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — قوالب/قواعد/تذكير يدويّ + سجلّ.
// النظام في وضع DryRun فقط: لا إرسال فعليّ، لا SMTP. الكتابة للأدمن حصرًا (سياسة EmailControlManage).
// تبويب «السجل» يعيد استخدام شاشة سجلّ إشعارات البريد القائمة (سياستها المنفصلة).
import { useMemo, useState } from 'react';
import {
  useEmailTemplates,
  useUpdateEmailTemplate,
  usePreviewEmailTemplate,
  useEmailRules,
  useUpdateEmailRule,
  usePreviewRecipients,
  useManualReminderDryRun,
} from '../lib/useEmailControl';
import {
  useDirectoryUsers,
  useDepartments,
  useTeams,
  useJobRoles,
} from '../lib/useDirectory';
import { Card, Input, Select, Button, Alert, Field, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { roleLabel } from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import EmailNotificationsPage from './EmailNotificationsPage';
import type {
  EmailTemplateDto,
  UpdateEmailTemplateRequest,
  EmailRuleDto,
  UpdateEmailRuleRequest,
  RecipientScopeType,
  RecipientPreviewRequest,
  RecipientPreviewDto,
  Role,
} from '../types/api';

type Tab = 'templates' | 'rules' | 'manual' | 'logs';

const TAB_LABEL: Record<Tab, string> = {
  templates: 'القوالب',
  rules: 'القواعد',
  manual: 'تذكير يدويّ',
  logs: 'السجل',
};

const categoryLabel: Record<string, string> = {
  Confirmation: 'تأكيد الحساب',
  Reports: 'التقارير',
  Governance: 'الحوكمة',
  HR: 'الموارد البشرية',
  Common: 'عام',
};

const modeLabel: Record<string, string> = {
  Disabled: 'معطّل',
  DryRun: 'تجريبي (DryRun)',
  Enabled: 'مُفعّل',
};

// R1: يُسمح فقط بـ DryRun/Disabled في الواجهة (Enabled محجوب — لا إرسال فعليّ).
const ALLOWED_MODES = ['DryRun', 'Disabled'];

const scopeTypeLabel: Record<RecipientScopeType, string> = {
  Users: 'مستخدمون محدّدون',
  Team: 'فريق',
  Department: 'إدارة',
  JobRole: 'مسمّى وظيفي',
  IdentityRole: 'دور في النظام',
};

// أدوار Identity المتاحة كنطاق (تطابق أسماء الأدوار خادميًّا).
const IDENTITY_ROLES: Role[] = [
  'Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader', 'Employee', 'CeoSupport', 'HR',
  'Viewer', 'FinanceManager', 'Accountant', 'AccountPortfolioReader',
];

export default function EmailControlCenterPage() {
  const [tab, setTab] = useState<Tab>('templates');

  return (
    <div className="space-y-6">
      <SectionTitle
        title="مركز التحكم بالبريد"
        hint="إدارة قوالب البريد وقواعده، ومعاينة المستقبِلين، وتذكير يدويّ — كلها في وضع DryRun (بلا إرسال فعليّ)."
      />

      <Alert tone="gold">
        النظام يعمل في وضع تجريبي (DryRun): كل الرسائل تُسجَّل ولا تُرسَل فعليًّا. لا يوجد إرسال بريد حقيقيّ في هذه المرحلة.
      </Alert>

      {/* التبويبات */}
      <div className="flex flex-wrap gap-2 border-b border-line">
        {(Object.keys(TAB_LABEL) as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`rounded-t-md px-4 py-2 text-sm font-medium transition ${
              tab === t
                ? 'border-b-2 border-navy text-navy'
                : 'text-slate-500 hover:text-navy'
            }`}
          >
            {TAB_LABEL[t]}
          </button>
        ))}
      </div>

      {tab === 'templates' && <TemplatesTab />}
      {tab === 'rules' && <RulesTab />}
      {tab === 'manual' && <ManualReminderTab identityRoles={IDENTITY_ROLES} />}
      {tab === 'logs' && <EmailNotificationsPage />}
    </div>
  );
}

// ===================== تبويب القوالب =====================
function TemplatesTab() {
  const templates = useEmailTemplates();
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  if (templates.isLoading) return <LoadingState label="يتم تحميل القوالب…" />;
  if (templates.isError) return <QueryError onRetry={() => templates.refetch()} description="تعذّر جلب قوالب البريد." />;

  const list = templates.data ?? [];
  const selected = list.find((t) => t.key === selectedKey) ?? null;

  // تجميع حسب الفئة
  const byCategory = list.reduce<Record<string, EmailTemplateDto[]>>((acc, t) => {
    (acc[t.category] ??= []).push(t);
    return acc;
  }, {});

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1.5fr]">
      <Card>
        <h3 className="mb-3 text-sm font-semibold text-navy">القوالب ({list.length})</h3>
        <div className="space-y-4">
          {Object.entries(byCategory).map(([cat, items]) => (
            <div key={cat}>
              <div className="mb-1 text-xs font-medium text-slate-400">{categoryLabel[cat] ?? cat}</div>
              <div className="space-y-1">
                {items.map((t) => (
                  <button
                    key={t.key}
                    onClick={() => setSelectedKey(t.key)}
                    className={`flex w-full items-center justify-between rounded-md px-3 py-2 text-right text-sm transition ${
                      selectedKey === t.key ? 'bg-navy/10 text-navy' : 'hover:bg-slate-50'
                    }`}
                  >
                    <span>{t.nameAr}</span>
                    {!t.isEnabled && <Badge tone="muted">معطّل</Badge>}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      </Card>

      {selected ? (
        <TemplateEditor key={selected.key} template={selected} />
      ) : (
        <Card>
          <p className="py-12 text-center text-sm text-slate-500">اختر قالبًا من القائمة لتحريره أو معاينته.</p>
        </Card>
      )}
    </div>
  );
}

function TemplateEditor({ template }: { template: EmailTemplateDto }) {
  const update = useUpdateEmailTemplate();
  const preview = usePreviewEmailTemplate();

  const [nameAr, setNameAr] = useState(template.nameAr);
  const [subject, setSubject] = useState(template.subjectTemplate);
  const [body, setBody] = useState(template.bodyTemplate);
  const [isEnabled, setIsEnabled] = useState(template.isEnabled);
  const [defaultMode, setDefaultMode] = useState(template.defaultMode);

  function save() {
    const req: UpdateEmailTemplateRequest = { nameAr, subjectTemplate: subject, bodyTemplate: body, isEnabled, defaultMode };
    update.mutate({ key: template.key, req });
  }

  function runPreview() {
    preview.mutate({ key: template.key, req: { subjectTemplate: subject, bodyTemplate: body } });
  }

  return (
    <Card>
      <div className="mb-4 flex items-center justify-between">
        <h3 className="text-sm font-semibold text-navy">{template.nameAr}</h3>
        <code className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-500">{template.key}</code>
      </div>

      <div className="space-y-4">
        <Field label="الاسم (بالعربية)">
          <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field label="عنوان الرسالة (Subject)">
          <Input value={subject} onChange={(e) => setSubject(e.target.value)} />
        </Field>
        <Field label="نصّ الرسالة (Body)">
          <textarea
            className="min-h-[140px] w-full rounded-md border border-line bg-white p-2 text-sm focus:border-navy focus:outline-none"
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />
        </Field>

        {template.availableVariables.length > 0 && (
          <div>
            <div className="mb-1 text-xs text-slate-500">المتغيّرات المتاحة (استخدمها بصيغة {'{{ اسم }}'})</div>
            <div className="flex flex-wrap gap-1">
              {template.availableVariables.map((v) => (
                <code key={v} className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs text-navy">{`{{${v}}}`}</code>
              ))}
            </div>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="الوضع الافتراضي">
            <Select value={defaultMode} onChange={(e) => setDefaultMode(e.target.value)}>
              {ALLOWED_MODES.map((m) => (
                <option key={m} value={m}>{modeLabel[m]}</option>
              ))}
            </Select>
          </Field>
          <div className="flex items-end">
            <label className="flex cursor-pointer items-center gap-2 text-sm">
              <input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} />
              <span>القالب مُفعّل</span>
            </label>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button onClick={save} disabled={update.isPending}>{update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}</Button>
          <Button variant="ghost" onClick={runPreview} disabled={preview.isPending}>معاينة</Button>
          {update.isSuccess && <span className="text-sm text-success">تم الحفظ.</span>}
          {update.isError && <span className="text-sm text-alert">{apiErrorMessage(update.error, 'تعذّر الحفظ.')}</span>}
        </div>

        {preview.isError && (
          <Alert tone="alert">{apiErrorMessage(preview.error, 'تعذّرت المعاينة.')}</Alert>
        )}
        {preview.data && (
          <div className="rounded-md border border-line bg-slate-50 p-3">
            <div className="mb-2 text-xs text-slate-500">معاينة</div>
            <div className="mb-2 font-semibold text-navy">{preview.data.subject}</div>
            <div
              className="rounded-md border border-line bg-white p-3 text-sm leading-relaxed"
              dangerouslySetInnerHTML={{ __html: preview.data.bodyHtml }}
            />
          </div>
        )}
      </div>
    </Card>
  );
}

// ===================== تبويب القواعد =====================
function RulesTab() {
  const rules = useEmailRules();

  if (rules.isLoading) return <LoadingState label="يتم تحميل القواعد…" />;
  if (rules.isError) return <QueryError onRetry={() => rules.refetch()} description="تعذّر جلب قواعد البريد." />;

  const list = rules.data ?? [];
  if (list.length === 0) {
    return <Card><p className="py-8 text-center text-sm text-slate-500">لا توجد قواعد بريد.</p></Card>;
  }

  return (
    <div className="space-y-4">
      {list.map((r) => (
        <RuleEditor key={r.id} rule={r} />
      ))}
    </div>
  );
}

const RULE_RECIPIENT_FIELDS: { key: keyof UpdateEmailRuleRequest; label: string }[] = [
  { key: 'sendToEmployee', label: 'الموظف' },
  { key: 'sendToManager', label: 'المدير' },
  { key: 'sendToTeamLeader', label: 'قائد الفريق' },
  { key: 'sendToHr', label: 'الموارد البشرية' },
  { key: 'sendToGovernance', label: 'الحوكمة' },
  { key: 'sendToAdmin', label: 'مدير النظام' },
];

function RuleEditor({ rule }: { rule: EmailRuleDto }) {
  const update = useUpdateEmailRule();
  const [form, setForm] = useState<UpdateEmailRuleRequest>({
    isEnabled: rule.isEnabled,
    sendToEmployee: rule.sendToEmployee,
    sendToManager: rule.sendToManager,
    sendToTeamLeader: rule.sendToTeamLeader,
    sendToHr: rule.sendToHr,
    sendToGovernance: rule.sendToGovernance,
    sendToAdmin: rule.sendToAdmin,
    cooldownMinutes: rule.cooldownMinutes,
    mode: rule.mode,
  });

  function setBool(key: keyof UpdateEmailRuleRequest, value: boolean) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  return (
    <Card>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <div className="font-semibold text-navy">{rule.eventType}</div>
          <code className="font-mono text-xs text-slate-400">القالب: {rule.templateKey}</code>
        </div>
        <label className="flex cursor-pointer items-center gap-2 text-sm">
          <input type="checkbox" checked={form.isEnabled} onChange={(e) => setBool('isEnabled', e.target.checked)} />
          <span>القاعدة مُفعّلة</span>
        </label>
      </div>

      <div className="mb-3">
        <div className="mb-1 text-xs text-slate-500">المستقبِلون</div>
        <div className="flex flex-wrap gap-3">
          {RULE_RECIPIENT_FIELDS.map((f) => (
            <label key={f.key} className="flex cursor-pointer items-center gap-1.5 text-sm">
              <input
                type="checkbox"
                checked={form[f.key] as boolean}
                onChange={(e) => setBool(f.key, e.target.checked)}
              />
              <span>{f.label}</span>
            </label>
          ))}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="فترة التهدئة (دقائق)">
          <Input
            type="number"
            min={0}
            value={form.cooldownMinutes ?? ''}
            onChange={(e) => setForm((f) => ({ ...f, cooldownMinutes: e.target.value === '' ? null : Number(e.target.value) }))}
          />
        </Field>
        <Field label="الوضع">
          <Select value={form.mode} onChange={(e) => setForm((f) => ({ ...f, mode: e.target.value }))}>
            {ALLOWED_MODES.map((m) => (
              <option key={m} value={m}>{modeLabel[m]}</option>
            ))}
          </Select>
        </Field>
      </div>

      <div className="mt-3 flex items-center gap-2">
        <Button onClick={() => update.mutate({ id: rule.id, req: form })} disabled={update.isPending}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        {update.isSuccess && <span className="text-sm text-success">تم الحفظ.</span>}
        {update.isError && <span className="text-sm text-alert">{apiErrorMessage(update.error, 'تعذّر الحفظ.')}</span>}
      </div>
    </Card>
  );
}

// ===================== منتقي النطاق (مشترك) =====================
interface ScopeValue {
  scopeType: RecipientScopeType;
  scopeId: string | null;
  roleName: string | null;
  userIds: string[];
}

function ScopePicker({
  value,
  onChange,
  identityRoles,
}: {
  value: ScopeValue;
  onChange: (v: ScopeValue) => void;
  identityRoles: Role[];
}) {
  const users = useDirectoryUsers(false);
  const departments = useDepartments();
  const teams = useTeams();
  const jobRoles = useJobRoles(true);

  function set<K extends keyof ScopeValue>(key: K, v: ScopeValue[K]) {
    onChange({ ...value, [key]: v });
  }

  function toggleUser(id: string) {
    const has = value.userIds.includes(id);
    set('userIds', has ? value.userIds.filter((u) => u !== id) : [...value.userIds, id]);
  }

  return (
    <div className="space-y-4">
      <Field label="نوع النطاق">
        <Select
          value={value.scopeType}
          onChange={(e) =>
            onChange({ scopeType: e.target.value as RecipientScopeType, scopeId: null, roleName: null, userIds: [] })
          }
        >
          {(Object.keys(scopeTypeLabel) as RecipientScopeType[]).map((s) => (
            <option key={s} value={s}>{scopeTypeLabel[s]}</option>
          ))}
        </Select>
      </Field>

      {value.scopeType === 'Team' && (
        <Field label="الفريق">
          <Select value={value.scopeId ?? ''} onChange={(e) => set('scopeId', e.target.value || null)}>
            <option value="">— اختر فريقًا —</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>{t.nameAr}</option>
            ))}
          </Select>
        </Field>
      )}

      {value.scopeType === 'Department' && (
        <Field label="الإدارة">
          <Select value={value.scopeId ?? ''} onChange={(e) => set('scopeId', e.target.value || null)}>
            <option value="">— اختر إدارة —</option>
            {(departments.data ?? []).map((d) => (
              <option key={d.id} value={d.id}>{d.nameAr}</option>
            ))}
          </Select>
        </Field>
      )}

      {value.scopeType === 'JobRole' && (
        <Field label="المسمّى الوظيفي">
          <Select value={value.scopeId ?? ''} onChange={(e) => set('scopeId', e.target.value || null)}>
            <option value="">— اختر مسمّى —</option>
            {(jobRoles.data ?? []).map((j) => (
              <option key={j.id} value={j.id}>{j.nameAr}</option>
            ))}
          </Select>
        </Field>
      )}

      {value.scopeType === 'IdentityRole' && (
        <Field label="الدور في النظام">
          <Select value={value.roleName ?? ''} onChange={(e) => set('roleName', e.target.value || null)}>
            <option value="">— اختر دورًا —</option>
            {identityRoles.map((r) => (
              <option key={r} value={r}>{roleLabel[r]}</option>
            ))}
          </Select>
        </Field>
      )}

      {value.scopeType === 'Users' && (
        <Field label={`المستخدمون المحدّدون (${value.userIds.length})`}>
          {users.isLoading ? (
            <LoadingState label="يتم تحميل المستخدمين…" />
          ) : (
            <div className="max-h-64 overflow-y-auto rounded-md border border-line p-2">
              {(users.data ?? []).map((u) => (
                <label key={u.id} className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-slate-50">
                  <input type="checkbox" checked={value.userIds.includes(u.id)} onChange={() => toggleUser(u.id)} />
                  <span>{u.fullName}</span>
                  <span className="text-xs text-slate-400">{u.email}</span>
                </label>
              ))}
            </div>
          )}
        </Field>
      )}
    </div>
  );
}

function buildScopeRequest(value: ScopeValue): RecipientPreviewRequest {
  return {
    scopeType: value.scopeType,
    scopeId: value.scopeId,
    roleName: value.roleName,
    userIds: value.scopeType === 'Users' ? value.userIds : null,
  };
}

function RecipientResultTable({ data }: { data: RecipientPreviewDto | { total?: number; recipients: RecipientPreviewDto['rows'] } }) {
  const rows = 'rows' in data ? data.rows : data.recipients;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-right text-sm">
        <thead>
          <tr className="border-b text-slate-500">
            <th className="p-2">المستخدم</th>
            <th className="p-2">البريد</th>
            <th className="p-2">الحالة</th>
            <th className="p-2">السبب</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.userId} className="border-b align-top">
              <td className="p-2">{r.fullName}</td>
              <td className="p-2 text-slate-500">{r.email ?? '—'}</td>
              <td className="p-2">
                <Badge tone={r.eligible ? 'success' : 'muted'}>{r.eligible ? 'مؤهَّل' : 'مستبعَد'}</Badge>
              </td>
              <td className="p-2 text-slate-500">{r.reason}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ===================== تبويب التذكير اليدويّ =====================
function ManualReminderTab({ identityRoles }: { identityRoles: Role[] }) {
  const [scope, setScope] = useState<ScopeValue>({ scopeType: 'Users', scopeId: null, roleName: null, userIds: [] });
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [link, setLink] = useState('');

  const previewRecipients = usePreviewRecipients();
  const dryRun = useManualReminderDryRun();

  const previewData = previewRecipients.data;
  const eligibleCount = previewData?.eligibleCount ?? 0;
  const canSubmit = useMemo(
    () => !!previewData && eligibleCount > 0 && subject.trim().length > 0 && body.trim().length > 0,
    [previewData, eligibleCount, subject, body],
  );

  function runPreview() {
    dryRun.reset();
    previewRecipients.mutate(buildScopeRequest(scope));
  }

  function runDryRun() {
    dryRun.mutate({
      ...buildScopeRequest(scope),
      subject: subject.trim(),
      body: body.trim(),
      link: link.trim() || null,
    });
  }

  // تغيير النطاق يُبطل المعاينة السابقة (تفاديًا لإرسال DryRun على نطاق مختلف).
  function updateScope(v: ScopeValue) {
    setScope(v);
    previewRecipients.reset();
    dryRun.reset();
  }

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <h3 className="mb-3 text-sm font-semibold text-navy">محتوى التذكير</h3>
        <div className="space-y-4">
          <Field label="العنوان (Subject)">
            <Input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="عنوان التذكير…" />
          </Field>
          <Field label="النص (Body)">
            <textarea
              className="min-h-[120px] w-full rounded-md border border-line bg-white p-2 text-sm focus:border-navy focus:outline-none"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="نصّ التذكير…"
            />
          </Field>
          <Field label="رابط (اختياري)">
            <Input value={link} onChange={(e) => setLink(e.target.value)} placeholder="/submissions?period=…" />
          </Field>
        </div>
      </Card>

      <Card>
        <h3 className="mb-3 text-sm font-semibold text-navy">المستقبِلون</h3>
        <ScopePicker value={scope} onChange={updateScope} identityRoles={identityRoles} />

        <div className="mt-4 flex flex-wrap items-center gap-2">
          <Button variant="ghost" onClick={runPreview} disabled={previewRecipients.isPending}>
            {previewRecipients.isPending ? 'جارٍ المعاينة…' : 'معاينة المستقبِلين'}
          </Button>
          <Button onClick={runDryRun} disabled={!canSubmit || dryRun.isPending}>
            {dryRun.isPending ? 'جارٍ الإنشاء…' : 'إنشاء تذكير DryRun'}
          </Button>
        </div>

        {previewRecipients.isError && (
          <Alert tone="alert">{apiErrorMessage(previewRecipients.error, 'تعذّرت معاينة المستقبِلين.')}</Alert>
        )}
        {!canSubmit && previewData && eligibleCount === 0 && (
          <Alert tone="gold">لا يوجد مستقبِلون مؤهَّلون — لا يمكن إنشاء التذكير.</Alert>
        )}
      </Card>

      {previewData && (
        <Card className="lg:col-span-2">
          <div className="mb-3 flex flex-wrap items-center gap-3 text-sm">
            <span className="font-semibold text-navy">معاينة المستقبِلين</span>
            <Badge tone="muted">إجمالي: {previewData.totalCandidates}</Badge>
            <Badge tone="success">مؤهَّل: {previewData.eligibleCount}</Badge>
            <Badge tone="gold">مستبعَد: {previewData.excludedCount}</Badge>
          </div>
          <RecipientResultTable data={previewData} />
        </Card>
      )}

      {dryRun.isError && (
        <Alert tone="alert">{apiErrorMessage(dryRun.error, 'تعذّر إنشاء التذكير.')}</Alert>
      )}
      {dryRun.data && (
        <Card className="lg:col-span-2">
          <div className="mb-3 flex flex-wrap items-center gap-3 text-sm">
            <span className="font-semibold text-success">تم إنشاء دفعة DryRun</span>
            <code className="font-mono text-xs text-slate-400">{dryRun.data.batchId}</code>
            <Badge tone="muted">إجمالي: {dryRun.data.total}</Badge>
            <Badge tone="success">أُنشئ: {dryRun.data.created}</Badge>
            <Badge tone="navy">مكرّر: {dryRun.data.duplicate}</Badge>
            <Badge tone="gold">متخطّى: {dryRun.data.skipped}</Badge>
          </div>
          <Alert tone="navy">
            هذه رسائل تجريبية (DryRun) سُجِّلت دون إرسال فعليّ. يمكنك مراجعتها في تبويب «السجل».
          </Alert>
          <div className="mt-3">
            <RecipientResultTable data={dryRun.data} />
          </div>
        </Card>
      )}
    </div>
  );
}

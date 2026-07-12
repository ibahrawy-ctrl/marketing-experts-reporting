// صفحة تفاصيل العميل (Client 360 — CPW-R1B) — تبويبات: الملف/جهات الاتصال/القنوات الرقمية/البراند/المشاريع/التقارير.
import { useState, type ReactNode, type TextareaHTMLAttributes } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  useClient,
  useClientReports,
  useUpdateClient,
  useArchiveClient,
  useProjects,
  useCreateProject,
  useClientContacts,
  useCreateClientContact,
  useUpdateClientContact,
  useSetClientContactActive,
  useClientDigitalChannels,
  useCreateClientDigitalChannel,
  useUpdateClientDigitalChannel,
  useSetClientDigitalChannelActive,
  useClientBrand,
  useUpsertClientBrand,
} from '../lib/useClients';
import { useDirectoryUsers, useTeams } from '../lib/useDirectory';
import { useAuth } from '../lib/auth';
import { Card, Badge, Button, StatCard, Field, Input, Select, Alert, EmptyState } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import {
  clientStatusLabel,
  clientStatusTone,
  projectStatusLabel,
  projectStatusTone,
  serviceTypeLabel,
  submissionStatusLabel,
  periodTypeLabel,
  formatDate,
  clientTypeLabel,
  sectorLabel,
  clientSourceLabel,
  contactMethodLabel,
  platformLabel,
  accessStatusLabel,
  codeLabel,
  CLIENT_TYPE_CODES,
  SECTOR_CODES,
  CLIENT_SOURCE_CODES,
  CONTACT_METHOD_CODES,
  PLATFORM_CODES,
  ACCESS_STATUS_CODES,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import type {
  ClientStatus,
  ProjectStatus,
  ServiceType,
  ClientDto,
  UpdateClientRequest,
  CreateProjectRequest,
  ClientContactDto,
  CreateClientContactRequest,
  ClientDigitalChannelDto,
  CreateClientDigitalChannelRequest,
  ClientBrandProfileDto,
  UpsertClientBrandProfileRequest,
  LinkedReportRow,
} from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

type TabKey = 'overview' | 'contacts' | 'channels' | 'brand' | 'projects' | 'reports';
const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'الملف' },
  { key: 'contacts', label: 'جهات الاتصال' },
  { key: 'channels', label: 'القنوات الرقمية' },
  { key: 'brand', label: 'البراند' },
  { key: 'projects', label: 'المشاريع' },
  { key: 'reports', label: 'التقارير' },
];

export default function ClientDetailPage() {
  const { clientId } = useParams<{ clientId: string }>();
  const { canEditClientCore, user } = useAuth();
  const client = useClient(clientId);
  const reports = useClientReports(clientId);
  const [tab, setTab] = useState<TabKey>('overview');

  if (client.isLoading) return <LoadingState label="يتم تحميل بيانات العميل…" />;
  if (client.isError || !client.data)
    return (
      <QueryError
        onRetry={() => client.refetch()}
        title="تعذّر عرض العميل"
        description="قد يكون العميل خارج نطاق صلاحيتك أو غير موجود."
      />
    );

  const c = client.data;
  const reportRows = reports.data ?? [];
  // صلاحية الكتابة على الكيانات الفرعية (جهات الاتصال/القنوات/البراند): مدير أساسيّ مخوَّل أو مدير الحساب للعميل.
  // الخادم يفرض القرار نهائيًّا (Resource-Based)؛ هذا الحساب لإظهار/إخفاء عناصر التحكم فقط.
  const canWriteChildren = canEditClientCore || (!!user && user.userId === c.accountManagerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="mb-1 text-xs text-ink-2">
            <Link to="/app/clients" className="hover:underline">
              العملاء
            </Link>{' '}
            / {c.name}
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-navy">{c.name}</h1>
            <Badge tone={clientStatusTone(c.status)}>{clientStatusLabel[c.status]}</Badge>
          </div>
          {c.tradeNameEn && <div className="mt-1 text-sm text-ink-2">{c.tradeNameEn}</div>}
        </div>
        {canEditClientCore && c.status !== 'Closed' && <ArchiveClientButton id={c.id} />}
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="إجمالي المشاريع" value={c.projectCount} />
        <StatCard label="مشاريع نشِطة" value={c.activeProjectCount} />
        <StatCard
          label="مشاريع في خطر"
          value={c.atRiskProjectCount}
          tone={c.atRiskProjectCount > 0 ? 'alert' : 'navy'}
        />
        <StatCard label="تقارير مرتبطة" value={reportRows.length} />
      </div>

      {/* شريط التبويبات */}
      <div className="flex flex-wrap gap-1 border-b border-line">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => setTab(t.key)}
            className={`rounded-t-lg px-4 py-2 text-sm font-semibold transition ${
              tab === t.key
                ? 'border-b-2 border-orange-500 text-navy'
                : 'text-ink-2 hover:text-navy'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'overview' && <OverviewTab client={c} canEdit={canEditClientCore} />}
      {tab === 'contacts' && <ContactsTab clientId={c.id} canWrite={canWriteChildren} />}
      {tab === 'channels' && <ChannelsTab clientId={c.id} canWrite={canWriteChildren} />}
      {tab === 'brand' && <BrandTab clientId={c.id} canWrite={canWriteChildren} />}
      {tab === 'projects' && <ProjectsTab client={c} canManage={canEditClientCore} />}
      {tab === 'reports' && <LinkedReportsCard rows={reportRows} title="تقارير العميل المرتبطة" />}
    </div>
  );
}

// ===== تبويب الملف (Overview) =====
function OverviewTab({ client: c, canEdit }: { client: ClientDto; canEdit: boolean }) {
  const [editing, setEditing] = useState(false);
  if (canEdit && editing) return <EditClientForm client={c} onDone={() => setEditing(false)} />;
  return (
    <Card className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-navy">الملف التعريفي</h2>
        {canEdit && (
          <Button variant="ghost" onClick={() => setEditing(true)}>
            تعديل بيانات العميل
          </Button>
        )}
      </div>
      <dl className="grid grid-cols-2 gap-4 text-sm lg:grid-cols-4">
        <Info label="الاسم التجاري (عربي)" value={c.name} />
        <Info label="الاسم التجاري (إنجليزي)" value={c.tradeNameEn ?? '—'} />
        <Info label="الاسم القانوني" value={c.legalName ?? '—'} />
        <Info label="نوع العميل" value={codeLabel(clientTypeLabel, c.clientTypeCode)} />
        <Info label="القطاع" value={codeLabel(sectorLabel, c.sectorCode)} />
        <Info label="مصدر العلاقة" value={codeLabel(clientSourceLabel, c.sourceCode)} />
        <Info label="الدولة" value={c.country ?? '—'} />
        <Info label="المدينة" value={c.city ?? '—'} />
        <Info
          label="الموقع الإلكتروني"
          value={
            c.website ? (
              <a href={c.website} target="_blank" rel="noreferrer" className="text-orange-600 hover:underline">
                {c.website}
              </a>
            ) : (
              '—'
            )
          }
        />
        <Info label="بداية العلاقة" value={formatDate(c.relationshipStartDate)} />
        <Info label="مدير الحساب" value={c.accountManagerName ?? '—'} />
        <Info label="تاريخ الإضافة" value={formatDate(c.createdAtUtc)} />
        <Info label="جهة الاتصال الرئيسية" value={c.mainContactName ?? '—'} />
        <Info label="بيانات الاتصال" value={c.mainContactInfo ?? '—'} />
      </dl>
      {c.notes && (
        <div className="rounded-lg bg-offwhite p-3 text-sm text-ink-2">
          <span className="font-semibold text-ink">ملاحظات: </span>
          {c.notes}
        </div>
      )}
    </Card>
  );
}

function Info({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-ink-2">{label}</dt>
      <dd className="mt-0.5 font-medium text-ink">{value}</dd>
    </div>
  );
}

function Textarea({ className = '', ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      className={`w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy ${className}`}
      rows={3}
      {...rest}
    />
  );
}

// ===== تبويب جهات الاتصال =====
function ContactsTab({ clientId, canWrite }: { clientId: string; canWrite: boolean }) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const contacts = useClientContacts(clientId, includeInactive);
  const [creating, setCreating] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const setActive = useSetClientContactActive(clientId);
  const [err, setErr] = useState<string | null>(null);

  const rows = contacts.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-navy">جهات الاتصال</h2>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-1.5 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            إظهار المعطَّلة
          </label>
          {canWrite && (
            <Button variant={creating ? 'ghost' : 'primary'} onClick={() => { setCreating((v) => !v); setEditId(null); }}>
              {creating ? 'إغلاق' : '+ جهة اتصال'}
            </Button>
          )}
        </div>
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      {canWrite && creating && (
        <ContactForm clientId={clientId} onDone={() => setCreating(false)} />
      )}

      {contacts.isLoading ? (
        <LoadingState label="يتم تحميل جهات الاتصال…" />
      ) : rows.length === 0 ? (
        <EmptyState title="لا توجد جهات اتصال" description="لم تُضَف جهات اتصال لهذا العميل بعد." />
      ) : (
        <div className="space-y-3">
          {rows.map((ct) =>
            canWrite && editId === ct.id ? (
              <ContactForm
                key={ct.id}
                clientId={clientId}
                contact={ct}
                onDone={() => setEditId(null)}
              />
            ) : (
              <div
                key={ct.id}
                className={`rounded-lg border p-3 text-sm ${ct.isActive ? 'border-line' : 'border-line bg-offwhite opacity-70'}`}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-navy">{ct.name}</span>
                    {ct.isPrimary && <Badge tone="success">رئيسية</Badge>}
                    {ct.isFinancialContact && <Badge tone="gold">مالية</Badge>}
                    {!ct.isActive && <Badge tone="muted">معطَّلة</Badge>}
                  </div>
                  {canWrite && (
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={() => { setEditId(ct.id); setCreating(false); }}>
                        تعديل
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={async () => {
                          setErr(null);
                          try {
                            await setActive.mutateAsync({ id: ct.id, active: !ct.isActive });
                          } catch (e) {
                            setErr(apiErrorMessage(e, 'تعذّر تغيير حالة جهة الاتصال.'));
                          }
                        }}
                      >
                        {ct.isActive ? 'تعطيل' : 'تفعيل'}
                      </Button>
                    </div>
                  )}
                </div>
                <dl className="mt-2 grid grid-cols-2 gap-2 text-xs text-ink-2 lg:grid-cols-4">
                  {ct.jobTitle && <Info label="المسمّى" value={ct.jobTitle} />}
                  {ct.department && <Info label="القسم" value={ct.department} />}
                  {ct.email && <Info label="البريد" value={ct.email} />}
                  {ct.phone && <Info label="الهاتف" value={ct.phone} />}
                  {ct.whatsApp && <Info label="واتساب" value={ct.whatsApp} />}
                  {ct.preferredContactMethodCode && (
                    <Info label="طريقة التواصل المفضّلة" value={codeLabel(contactMethodLabel, ct.preferredContactMethodCode)} />
                  )}
                </dl>
                {ct.notes && <p className="mt-2 text-xs text-ink-2">{ct.notes}</p>}
              </div>
            ),
          )}
        </div>
      )}
    </Card>
  );
}

function ContactForm({
  clientId,
  contact,
  onDone,
}: {
  clientId: string;
  contact?: ClientContactDto;
  onDone: () => void;
}) {
  const create = useCreateClientContact(clientId);
  const update = useUpdateClientContact(clientId);
  const [name, setName] = useState(contact?.name ?? '');
  const [jobTitle, setJobTitle] = useState(contact?.jobTitle ?? '');
  const [department, setDepartment] = useState(contact?.department ?? '');
  const [email, setEmail] = useState(contact?.email ?? '');
  const [phone, setPhone] = useState(contact?.phone ?? '');
  const [whatsApp, setWhatsApp] = useState(contact?.whatsApp ?? '');
  const [method, setMethod] = useState(contact?.preferredContactMethodCode ?? '');
  const [isPrimary, setIsPrimary] = useState(contact?.isPrimary ?? false);
  const [isFinancial, setIsFinancial] = useState(contact?.isFinancialContact ?? false);
  const [notes, setNotes] = useState(contact?.notes ?? '');
  const [err, setErr] = useState<string | null>(null);
  const pending = create.isPending || update.isPending;

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم جهة الاتصال مطلوب.');
      return;
    }
    const req: CreateClientContactRequest = {
      name: name.trim(),
      jobTitle: jobTitle.trim() || null,
      department: department.trim() || null,
      email: email.trim() || null,
      phone: phone.trim() || null,
      whatsApp: whatsApp.trim() || null,
      preferredContactMethodCode: method || null,
      isPrimary,
      isFinancialContact: isFinancial,
      notes: notes.trim() || null,
      sortOrder: contact?.sortOrder ?? 0,
    };
    try {
      if (contact) await update.mutateAsync({ id: contact.id, req });
      else await create.mutateAsync(req);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ جهة الاتصال.'));
    }
  }

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <h3 className="font-bold text-navy">{contact ? 'تعديل جهة اتصال' : 'جهة اتصال جديدة'}</h3>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="الاسم">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="المسمّى الوظيفي">
          <Input value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
        </Field>
        <Field label="القسم">
          <Input value={department} onChange={(e) => setDepartment(e.target.value)} />
        </Field>
        <Field label="البريد الإلكتروني">
          <Input value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label="الهاتف">
          <Input value={phone} onChange={(e) => setPhone(e.target.value)} />
        </Field>
        <Field label="واتساب">
          <Input value={whatsApp} onChange={(e) => setWhatsApp(e.target.value)} />
        </Field>
        <Field label="طريقة التواصل المفضّلة">
          <Select value={method} onChange={(e) => setMethod(e.target.value)}>
            <option value="">— غير محدَّد —</option>
            {CONTACT_METHOD_CODES.map((code) => (
              <option key={code} value={code}>
                {contactMethodLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="mt-3 flex flex-wrap gap-4">
        <label className="flex items-center gap-1.5 text-sm text-ink">
          <input type="checkbox" checked={isPrimary} onChange={(e) => setIsPrimary(e.target.checked)} />
          جهة الاتصال الرئيسية
        </label>
        <label className="flex items-center gap-1.5 text-sm text-ink">
          <input type="checkbox" checked={isFinancial} onChange={(e) => setIsFinancial(e.target.checked)} />
          جهة اتصال مالية
        </label>
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={pending || !name.trim()}>
          {pending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

// ===== تبويب القنوات الرقمية =====
function ChannelsTab({ clientId, canWrite }: { clientId: string; canWrite: boolean }) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const channels = useClientDigitalChannels(clientId, includeInactive);
  const [creating, setCreating] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const setActive = useSetClientDigitalChannelActive(clientId);
  const [err, setErr] = useState<string | null>(null);

  const rows = channels.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-bold text-navy">القنوات الرقمية</h2>
          <p className="text-xs text-ink-2">معرّفات مرجعية فقط — لا تُخزَّن كلمات مرور أو رموز وصول.</p>
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-1.5 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            إظهار المعطَّلة
          </label>
          {canWrite && (
            <Button variant={creating ? 'ghost' : 'primary'} onClick={() => { setCreating((v) => !v); setEditId(null); }}>
              {creating ? 'إغلاق' : '+ قناة'}
            </Button>
          )}
        </div>
      </div>

      {err && <Alert tone="alert">{err}</Alert>}

      {canWrite && creating && (
        <ChannelForm clientId={clientId} onDone={() => setCreating(false)} />
      )}

      {channels.isLoading ? (
        <LoadingState label="يتم تحميل القنوات…" />
      ) : rows.length === 0 ? (
        <EmptyState title="لا توجد قنوات رقمية" description="لم تُضَف قنوات رقمية لهذا العميل بعد." />
      ) : (
        <div className="space-y-3">
          {rows.map((ch) =>
            canWrite && editId === ch.id ? (
              <ChannelForm key={ch.id} clientId={clientId} channel={ch} onDone={() => setEditId(null)} />
            ) : (
              <div
                key={ch.id}
                className={`rounded-lg border p-3 text-sm ${ch.isActive ? 'border-line' : 'border-line bg-offwhite opacity-70'}`}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-navy">{codeLabel(platformLabel, ch.platformCode)}</span>
                    {ch.displayName && <span className="text-ink-2">— {ch.displayName}</span>}
                    {ch.accessStatusCode && (
                      <Badge tone={ch.accessStatusCode === 'FullAccess' ? 'success' : 'gold'}>
                        {codeLabel(accessStatusLabel, ch.accessStatusCode)}
                      </Badge>
                    )}
                    {!ch.isActive && <Badge tone="muted">معطَّلة</Badge>}
                  </div>
                  {canWrite && (
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={() => { setEditId(ch.id); setCreating(false); }}>
                        تعديل
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={async () => {
                          setErr(null);
                          try {
                            await setActive.mutateAsync({ id: ch.id, active: !ch.isActive });
                          } catch (e) {
                            setErr(apiErrorMessage(e, 'تعذّر تغيير حالة القناة.'));
                          }
                        }}
                      >
                        {ch.isActive ? 'تعطيل' : 'تفعيل'}
                      </Button>
                    </div>
                  )}
                </div>
                <dl className="mt-2 grid grid-cols-2 gap-2 text-xs text-ink-2 lg:grid-cols-4">
                  {ch.usernameOrHandle && <Info label="المعرّف" value={ch.usernameOrHandle} />}
                  {ch.profileUrl && (
                    <Info
                      label="الرابط"
                      value={
                        <a href={ch.profileUrl} target="_blank" rel="noreferrer" className="text-orange-600 hover:underline">
                          فتح
                        </a>
                      }
                    />
                  )}
                  {ch.businessManagerId && <Info label="Business Manager" value={ch.businessManagerId} />}
                  {ch.adAccountId && <Info label="Ad Account" value={ch.adAccountId} />}
                  {ch.pixelId && <Info label="Pixel" value={ch.pixelId} />}
                  {ch.ga4PropertyId && <Info label="GA4 Property" value={ch.ga4PropertyId} />}
                  {ch.gtmContainerId && <Info label="GTM Container" value={ch.gtmContainerId} />}
                </dl>
                {ch.notes && <p className="mt-2 text-xs text-ink-2">{ch.notes}</p>}
              </div>
            ),
          )}
        </div>
      )}
    </Card>
  );
}

function ChannelForm({
  clientId,
  channel,
  onDone,
}: {
  clientId: string;
  channel?: ClientDigitalChannelDto;
  onDone: () => void;
}) {
  const create = useCreateClientDigitalChannel(clientId);
  const update = useUpdateClientDigitalChannel(clientId);
  const [platformCode, setPlatformCode] = useState(channel?.platformCode ?? 'Facebook');
  const [displayName, setDisplayName] = useState(channel?.displayName ?? '');
  const [handle, setHandle] = useState(channel?.usernameOrHandle ?? '');
  const [profileUrl, setProfileUrl] = useState(channel?.profileUrl ?? '');
  const [accessStatus, setAccessStatus] = useState(channel?.accessStatusCode ?? '');
  const [businessManagerId, setBusinessManagerId] = useState(channel?.businessManagerId ?? '');
  const [adAccountId, setAdAccountId] = useState(channel?.adAccountId ?? '');
  const [pixelId, setPixelId] = useState(channel?.pixelId ?? '');
  const [ga4, setGa4] = useState(channel?.ga4PropertyId ?? '');
  const [gtm, setGtm] = useState(channel?.gtmContainerId ?? '');
  const [notes, setNotes] = useState(channel?.notes ?? '');
  const [err, setErr] = useState<string | null>(null);
  const pending = create.isPending || update.isPending;

  async function submit() {
    setErr(null);
    const req: CreateClientDigitalChannelRequest = {
      platformCode,
      displayName: displayName.trim() || null,
      usernameOrHandle: handle.trim() || null,
      profileUrl: profileUrl.trim() || null,
      accessStatusCode: accessStatus || null,
      businessManagerId: businessManagerId.trim() || null,
      adAccountId: adAccountId.trim() || null,
      pixelId: pixelId.trim() || null,
      ga4PropertyId: ga4.trim() || null,
      gtmContainerId: gtm.trim() || null,
      notes: notes.trim() || null,
      sortOrder: channel?.sortOrder ?? 0,
    };
    try {
      if (channel) await update.mutateAsync({ id: channel.id, req });
      else await create.mutateAsync(req);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ القناة.'));
    }
  }

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <h3 className="font-bold text-navy">{channel ? 'تعديل قناة' : 'قناة جديدة'}</h3>
      <p className="mt-1 text-xs text-ink-2">لا تُدخِل كلمات مرور أو رموز وصول — معرّفات مرجعية فقط.</p>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="المنصّة">
          <Select value={platformCode} onChange={(e) => setPlatformCode(e.target.value)}>
            {PLATFORM_CODES.map((code) => (
              <option key={code} value={code}>
                {platformLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الاسم الظاهر">
          <Input value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </Field>
        <Field label="المعرّف / اسم المستخدم">
          <Input value={handle} onChange={(e) => setHandle(e.target.value)} />
        </Field>
        <Field label="رابط الصفحة" help="http/https فقط">
          <Input value={profileUrl} onChange={(e) => setProfileUrl(e.target.value)} />
        </Field>
        <Field label="حالة الوصول">
          <Select value={accessStatus} onChange={(e) => setAccessStatus(e.target.value)}>
            <option value="">— غير محدَّدة —</option>
            {ACCESS_STATUS_CODES.map((code) => (
              <option key={code} value={code}>
                {accessStatusLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Business Manager ID">
          <Input value={businessManagerId} onChange={(e) => setBusinessManagerId(e.target.value)} />
        </Field>
        <Field label="Ad Account ID">
          <Input value={adAccountId} onChange={(e) => setAdAccountId(e.target.value)} />
        </Field>
        <Field label="Pixel ID">
          <Input value={pixelId} onChange={(e) => setPixelId(e.target.value)} />
        </Field>
        <Field label="GA4 Property ID">
          <Input value={ga4} onChange={(e) => setGa4(e.target.value)} />
        </Field>
        <Field label="GTM Container ID">
          <Input value={gtm} onChange={(e) => setGtm(e.target.value)} />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={pending}>
          {pending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

// ===== تبويب البراند =====
const BRAND_FIELDS: { key: keyof ClientBrandProfileDto; label: string }[] = [
  { key: 'businessOverview', label: 'نبذة عن النشاط' },
  { key: 'productsAndServices', label: 'المنتجات والخدمات' },
  { key: 'targetAudience', label: 'الجمهور المستهدف' },
  { key: 'targetLocations', label: 'المناطق المستهدفة' },
  { key: 'uniqueSellingProposition', label: 'الميزة التنافسية (USP)' },
  { key: 'strengths', label: 'نقاط القوة' },
  { key: 'competitors', label: 'المنافسون' },
  { key: 'toneOfVoice', label: 'نبرة الصوت' },
  { key: 'preferredMessages', label: 'الرسائل المفضّلة' },
  { key: 'prohibitedMessages', label: 'الرسائل الممنوعة' },
  { key: 'notes', label: 'ملاحظات' },
];

function BrandTab({ clientId, canWrite }: { clientId: string; canWrite: boolean }) {
  const brand = useClientBrand(clientId);
  const [editing, setEditing] = useState(false);

  if (brand.isLoading) return <LoadingState label="يتم تحميل ملفّ البراند…" />;
  const b = brand.data ?? null;

  if (canWrite && editing) {
    return <BrandForm clientId={clientId} brand={b} onDone={() => setEditing(false)} />;
  }

  return (
    <Card className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-navy">ملفّ البراند</h2>
        {canWrite && (
          <Button variant="ghost" onClick={() => setEditing(true)}>
            {b ? 'تعديل البراند' : '+ إنشاء ملفّ البراند'}
          </Button>
        )}
      </div>
      {!b ? (
        <EmptyState title="لا يوجد ملفّ براند" description="لم يُنشأ ملفّ براند لهذا العميل بعد." />
      ) : (
        <div className="space-y-3 text-sm">
          {BRAND_FIELDS.map(({ key, label }) => {
            const val = b[key] as string | null;
            if (!val) return null;
            return (
              <div key={key}>
                <div className="text-xs text-ink-2">{label}</div>
                <div className="mt-0.5 whitespace-pre-wrap text-ink">{val}</div>
              </div>
            );
          })}
          {b.brandGuidelinesUrl && (
            <div>
              <div className="text-xs text-ink-2">دليل الهوية</div>
              <a href={b.brandGuidelinesUrl} target="_blank" rel="noreferrer" className="text-orange-600 hover:underline">
                {b.brandGuidelinesUrl}
              </a>
            </div>
          )}
          <div className="text-xs text-ink-2">آخر تحديث: {formatDate(b.updatedAtUtc ?? b.createdAtUtc)}</div>
        </div>
      )}
    </Card>
  );
}

function BrandForm({
  clientId,
  brand,
  onDone,
}: {
  clientId: string;
  brand: ClientBrandProfileDto | null;
  onDone: () => void;
}) {
  const upsert = useUpsertClientBrand(clientId);
  const [form, setForm] = useState<UpsertClientBrandProfileRequest>({
    businessOverview: brand?.businessOverview ?? '',
    productsAndServices: brand?.productsAndServices ?? '',
    targetAudience: brand?.targetAudience ?? '',
    targetLocations: brand?.targetLocations ?? '',
    uniqueSellingProposition: brand?.uniqueSellingProposition ?? '',
    strengths: brand?.strengths ?? '',
    competitors: brand?.competitors ?? '',
    toneOfVoice: brand?.toneOfVoice ?? '',
    preferredMessages: brand?.preferredMessages ?? '',
    prohibitedMessages: brand?.prohibitedMessages ?? '',
    brandGuidelinesUrl: brand?.brandGuidelinesUrl ?? '',
    notes: brand?.notes ?? '',
  });
  const [err, setErr] = useState<string | null>(null);

  function set<K extends keyof UpsertClientBrandProfileRequest>(key: K, value: string) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function submit() {
    setErr(null);
    const trimmed: UpsertClientBrandProfileRequest = {};
    (Object.keys(form) as (keyof UpsertClientBrandProfileRequest)[]).forEach((k) => {
      const v = (form[k] ?? '').toString().trim();
      trimmed[k] = v || null;
    });
    try {
      await upsert.mutateAsync(trimmed);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ ملفّ البراند.'));
    }
  }

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-bold text-navy">{brand ? 'تعديل ملفّ البراند' : 'إنشاء ملفّ البراند'}</h2>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-4">
        {BRAND_FIELDS.map(({ key, label }) => (
          <Field key={key} label={label}>
            <Textarea
              value={(form[key as keyof UpsertClientBrandProfileRequest] ?? '') as string}
              onChange={(e) => set(key as keyof UpsertClientBrandProfileRequest, e.target.value)}
            />
          </Field>
        ))}
        <Field label="رابط دليل الهوية" help="http/https فقط">
          <Input
            value={form.brandGuidelinesUrl ?? ''}
            onChange={(e) => set('brandGuidelinesUrl', e.target.value)}
          />
        </Field>
      </div>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={upsert.isPending}>
          {upsert.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </Card>
  );
}

// ===== تبويب المشاريع =====
function ProjectsTab({ client: c, canManage }: { client: ClientDto; canManage: boolean }) {
  const projects = useProjects({ clientId: c.id, includeClosed: true });
  const [creatingProject, setCreatingProject] = useState(false);
  const projectRows = projects.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-navy">مشاريع العميل</h2>
        {canManage && (
          <Button
            variant={creatingProject ? 'ghost' : 'primary'}
            onClick={() => setCreatingProject((v) => !v)}
          >
            {creatingProject ? 'إغلاق' : '+ مشروع جديد'}
          </Button>
        )}
      </div>

      {canManage && creatingProject && (
        <CreateProjectForm clientId={c.id} onDone={() => setCreatingProject(false)} />
      )}

      {projects.isLoading ? (
        <LoadingState label="يتم تحميل المشاريع…" />
      ) : projectRows.length === 0 ? (
        <EmptyState title="لا توجد مشاريع" description="لم تُضَف مشاريع لهذا العميل بعد." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">المشروع</th>
                <th className="px-3 py-2.5 font-semibold">الخدمة</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">الفريق</th>
                <th className="px-3 py-2.5 font-semibold">البداية</th>
                <th className="px-3 py-2.5 font-semibold">النهاية</th>
                <th className="px-3 py-2.5 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {projectRows.map((p) => (
                <tr key={p.id} className="border-b border-line last:border-0 hover:bg-offwhite">
                  <td className="px-3 py-2.5 font-semibold text-navy">{p.name}</td>
                  <td className="px-3 py-2.5 text-ink-2">{serviceTypeLabel[p.serviceType]}</td>
                  <td className="px-3 py-2.5">
                    <Badge tone={projectStatusTone(p.status)}>{projectStatusLabel[p.status]}</Badge>
                  </td>
                  <td className="px-3 py-2.5 text-ink-2">{p.ownerTeamName ?? '—'}</td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDate(p.startDate)}</td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDate(p.endDate)}</td>
                  <td className="px-3 py-2.5">
                    <Link
                      to={`/app/projects/${p.id}`}
                      className="text-sm font-semibold text-orange-600 hover:underline"
                    >
                      تفاصيل
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

export function LinkedReportsCard({ rows, title }: { rows: LinkedReportRow[]; title: string }) {
  return (
    <Card>
      <h2 className="text-lg font-bold text-navy">{title}</h2>
      {rows.length === 0 ? (
        <p className="mt-3 text-sm text-ink-2">لا توجد تقارير مرتبطة بعد.</p>
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[640px] text-right text-sm">
            <thead className="border-b border-line bg-offwhite text-xs text-ink-2">
              <tr>
                <th className="px-3 py-2.5 font-semibold">مُقدِّم التقرير</th>
                <th className="px-3 py-2.5 font-semibold">الدورية</th>
                <th className="px-3 py-2.5 font-semibold">الفترة</th>
                <th className="px-3 py-2.5 font-semibold">الحالة</th>
                <th className="px-3 py-2.5 font-semibold">تاريخ الإرسال</th>
                <th className="px-3 py-2.5 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.submissionId} className="border-b border-line last:border-0 hover:bg-offwhite">
                  <td className="px-3 py-2.5 font-medium text-ink">{r.submitterName ?? '—'}</td>
                  <td className="px-3 py-2.5 text-ink-2">{periodTypeLabel[r.periodType]}</td>
                  <td className="px-3 py-2.5 text-ink-2">{r.periodKey}</td>
                  <td className="px-3 py-2.5">
                    <Badge tone={r.status === 'Closed' ? 'success' : r.status === 'Returned' ? 'gold' : 'navy'}>
                      {submissionStatusLabel[r.status]}
                    </Badge>
                  </td>
                  <td className="px-3 py-2.5 text-ink-2">{formatDate(r.submittedAtUtc)}</td>
                  <td className="px-3 py-2.5">
                    <Link
                      to={`/app/submissions?open=${r.submissionId}`}
                      className="text-sm font-semibold text-orange-600 hover:underline"
                    >
                      فتح
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function ArchiveClientButton({ id }: { id: string }) {
  const archive = useArchiveClient();
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  if (!confirm)
    return (
      <Button variant="danger" onClick={() => setConfirm(true)}>
        أرشفة العميل
      </Button>
    );

  return (
    <div className="flex flex-col items-end gap-1">
      {err && <span className="text-xs text-alert">{err}</span>}
      <div className="flex gap-2">
        <Button
          variant="danger"
          onClick={async () => {
            setErr(null);
            try {
              await archive.mutateAsync(id);
              setConfirm(false);
            } catch (e) {
              setErr(apiErrorMessage(e, 'تعذّرت الأرشفة.'));
            }
          }}
          disabled={archive.isPending}
        >
          تأكيد الأرشفة
        </Button>
        <Button variant="ghost" onClick={() => setConfirm(false)}>
          تراجع
        </Button>
      </div>
    </div>
  );
}

function EditClientForm({ client, onDone }: { client: ClientDto; onDone: () => void }) {
  const update = useUpdateClient();
  const users = useDirectoryUsers();
  const [name, setName] = useState(client.name);
  const [status, setStatus] = useState<ClientStatus>(client.status);
  const [accountManagerId, setAccountManagerId] = useState(client.accountManagerId ?? '');
  const [mainContactName, setMainContactName] = useState(client.mainContactName ?? '');
  const [mainContactInfo, setMainContactInfo] = useState(client.mainContactInfo ?? '');
  const [notes, setNotes] = useState(client.notes ?? '');
  const [tradeNameEn, setTradeNameEn] = useState(client.tradeNameEn ?? '');
  const [legalName, setLegalName] = useState(client.legalName ?? '');
  const [clientTypeCode, setClientTypeCode] = useState(client.clientTypeCode ?? '');
  const [sectorCode, setSectorCode] = useState(client.sectorCode ?? '');
  const [country, setCountry] = useState(client.country ?? '');
  const [city, setCity] = useState(client.city ?? '');
  const [website, setWebsite] = useState(client.website ?? '');
  const [sourceCode, setSourceCode] = useState(client.sourceCode ?? '');
  const [relationshipStartDate, setRelationshipStartDate] = useState(client.relationshipStartDate ?? '');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم العميل مطلوب.');
      return;
    }
    const req: UpdateClientRequest = {
      name: name.trim(),
      status,
      accountManagerId: accountManagerId || null,
      mainContactName: mainContactName.trim() || null,
      mainContactInfo: mainContactInfo.trim() || null,
      notes: notes.trim() || null,
      tradeNameEn: tradeNameEn.trim() || null,
      legalName: legalName.trim() || null,
      clientTypeCode: clientTypeCode || null,
      sectorCode: sectorCode || null,
      country: country.trim() || null,
      city: city.trim() || null,
      website: website.trim() || null,
      sourceCode: sourceCode || null,
      relationshipStartDate: relationshipStartDate || null,
    };
    try {
      await update.mutateAsync({ id: client.id, req });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ التعديلات.'));
    }
  }

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-bold text-navy">تعديل بيانات العميل</h2>
      {err && <Alert tone="alert">{err}</Alert>}
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="الاسم التجاري (عربي)">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="الاسم التجاري (إنجليزي)">
          <Input value={tradeNameEn} onChange={(e) => setTradeNameEn(e.target.value)} />
        </Field>
        <Field label="الاسم القانوني">
          <Input value={legalName} onChange={(e) => setLegalName(e.target.value)} />
        </Field>
        <Field label="الحالة">
          <Select value={status} onChange={(e) => setStatus(e.target.value as ClientStatus)}>
            {(['Active', 'Paused', 'AtRisk', 'Closed'] as ClientStatus[]).map((s) => (
              <option key={s} value={s}>
                {clientStatusLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="نوع العميل">
          <Select value={clientTypeCode} onChange={(e) => setClientTypeCode(e.target.value)}>
            <option value="">— غير محدَّد —</option>
            {CLIENT_TYPE_CODES.map((code) => (
              <option key={code} value={code}>
                {clientTypeLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="القطاع">
          <Select value={sectorCode} onChange={(e) => setSectorCode(e.target.value)}>
            <option value="">— غير محدَّد —</option>
            {SECTOR_CODES.map((code) => (
              <option key={code} value={code}>
                {sectorLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مصدر العلاقة">
          <Select value={sourceCode} onChange={(e) => setSourceCode(e.target.value)}>
            <option value="">— غير محدَّد —</option>
            {CLIENT_SOURCE_CODES.map((code) => (
              <option key={code} value={code}>
                {clientSourceLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مدير الحساب">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الدولة">
          <Input value={country} onChange={(e) => setCountry(e.target.value)} />
        </Field>
        <Field label="المدينة">
          <Input value={city} onChange={(e) => setCity(e.target.value)} />
        </Field>
        <Field label="الموقع الإلكتروني" help="http/https فقط">
          <Input value={website} onChange={(e) => setWebsite(e.target.value)} />
        </Field>
        <Field label="تاريخ بداية العلاقة">
          <Input
            type="date"
            value={relationshipStartDate}
            onChange={(e) => setRelationshipStartDate(e.target.value)}
          />
        </Field>
        <Field label="جهة الاتصال الرئيسية">
          <Input value={mainContactName} onChange={(e) => setMainContactName(e.target.value)} />
        </Field>
        <Field label="بيانات الاتصال">
          <Input value={mainContactInfo} onChange={(e) => setMainContactInfo(e.target.value)} />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="flex gap-2">
        <Button variant="primary" onClick={submit} disabled={update.isPending || !name.trim()}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </Card>
  );
}

function CreateProjectForm({ clientId, onDone }: { clientId: string; onDone: () => void }) {
  const create = useCreateProject();
  const teams = useTeams();
  const users = useDirectoryUsers();
  const [name, setName] = useState('');
  const [serviceType, setServiceType] = useState<ServiceType>('Social');
  const [status, setStatus] = useState<ProjectStatus>('Active');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [ownerTeamId, setOwnerTeamId] = useState('');
  const [accountManagerId, setAccountManagerId] = useState('');
  const [notes, setNotes] = useState('');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!name.trim()) {
      setErr('اسم المشروع مطلوب.');
      return;
    }
    const req: CreateProjectRequest = {
      clientId,
      name: name.trim(),
      serviceType,
      status,
      startDate: startDate || null,
      endDate: endDate || null,
      ownerTeamId: ownerTeamId || null,
      accountManagerId: accountManagerId || null,
      notes: notes.trim() || null,
    };
    try {
      await create.mutateAsync(req);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر إنشاء المشروع.'));
    }
  }

  return (
    <div className="rounded-lg border border-line bg-offwhite p-4">
      <h3 className="font-bold text-navy">مشروع جديد</h3>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="اسم المشروع">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="نوع الخدمة">
          <Select value={serviceType} onChange={(e) => setServiceType(e.target.value as ServiceType)}>
            {SERVICE_TYPES.map((s) => (
              <option key={s} value={s}>
                {serviceTypeLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الحالة">
          <Select value={status} onChange={(e) => setStatus(e.target.value as ProjectStatus)}>
            {(['Active', 'Paused', 'Completed', 'AtRisk', 'Closed'] as ProjectStatus[]).map((s) => (
              <option key={s} value={s}>
                {projectStatusLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الفريق المسؤول">
          <Select value={ownerTeamId} onChange={(e) => setOwnerTeamId(e.target.value)}>
            <option value="">— بدون —</option>
            {(teams.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.nameAr}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مدير الحساب">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="تاريخ البداية">
          <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </Field>
        <Field label="تاريخ النهاية">
          <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
        </Field>
        <Field label="ملاحظات">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={create.isPending || !name.trim()}>
          {create.isPending ? 'جارٍ الحفظ…' : 'حفظ المشروع'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

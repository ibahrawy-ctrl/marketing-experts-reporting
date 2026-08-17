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
import {
  useClientDocuments,
  useClientDocument,
  useClientStorageUsage,
  useUploadClientDocument,
  useAddClientDocumentVersion,
  useUpdateClientDocument,
  useSetClientDocumentArchived,
  useDeleteClientDocument,
  useClientExternalLinks,
  useCreateClientExternalLink,
  useUpdateClientExternalLink,
  useSetClientExternalLinkActive,
  downloadClientDocument,
  downloadClientDocumentVersion,
} from '../lib/useClientDocuments';
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
  documentCategoryLabel,
  DOCUMENT_CATEGORY_CODES,
  linkCategoryLabel,
  LINK_CATEGORY_CODES,
  confidentialityLabel,
  CONFIDENTIALITY_CODES,
  documentLifecycleLabel,
  documentLifecycleTone,
  documentScanStatusLabel,
  documentScanStatusTone,
  documentVisibilityLabel,
  DOCUMENT_VISIBILITY_TYPES,
  defaultVisibilityForCategory,
  roleLabel,
  formatBytes,
} from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import {
  percentOrDash,
  projectHealthStatusLabel,
  projectHealthStatusTone,
  projectProgressModeLabel,
} from '../lib/project360Format';
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
  ClientDocumentDto,
  ClientExternalLinkDto,
  CreateClientExternalLinkRequest,
  DocumentLifecycleStatus,
  DocumentVisibilityType,
  Role,
} from '../types/api';

const SERVICE_TYPES: ServiceType[] = ['Social', 'Seo', 'MediaBuying', 'Website', 'Video', 'Branding', 'Other'];

type TabKey =
  | 'overview'
  | 'contacts'
  | 'channels'
  | 'brand'
  | 'documents'
  | 'links'
  | 'projects'
  | 'reports';
const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'الملف' },
  { key: 'contacts', label: 'جهات الاتصال' },
  { key: 'channels', label: 'القنوات الرقمية' },
  { key: 'brand', label: 'البراند' },
  { key: 'documents', label: 'المستندات' },
  { key: 'links', label: 'الروابط المهمّة' },
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
      {tab === 'documents' && <DocumentsTab clientId={c.id} canWrite={canWriteChildren} />}
      {tab === 'links' && <LinksTab clientId={c.id} canWrite={canWriteChildren} />}
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
        <Info label="مدير العميل" value={c.accountManagerName ?? '—'} />
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
                <th className="px-3 py-2.5 font-semibold">التقدّم</th>
                <th className="px-3 py-2.5 font-semibold">الصحّة</th>
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
                  {/* التقدّم والصحّة يصلان من الخادم داخل ProjectDto ذاته: الرقم هنا هو **نفس**
                      رقم Project 360 لا احتسابًا ثانيًا في المتصفّح، ومعه طريقة احتسابه كي لا
                      تُقرأ نسبةٌ سقطت أوزانها كأنّها نسبة موزونة. */}
                  <td className="px-3 py-2.5">
                    <span className="font-semibold text-navy">{percentOrDash(p.progressPercent)}</span>
                    <span className="block text-[11px] text-ink-2">
                      {projectProgressModeLabel[p.progressMode]}
                    </span>
                  </td>
                  <td className="px-3 py-2.5">
                    <Badge tone={projectHealthStatusTone(p.healthStatus)}>
                      {projectHealthStatusLabel[p.healthStatus]}
                    </Badge>
                    <span className="block text-[11px] text-ink-2">{percentOrDash(p.healthPercent)}</span>
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
        <Field label="مدير العميل">
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
  const [projectOwnerId, setProjectOwnerId] = useState('');
  const [teamLeaderId, setTeamLeaderId] = useState('');
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
      projectOwnerId: projectOwnerId || null,
      teamLeaderId: teamLeaderId || null,
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
        <Field label="مدير العميل">
          <Select value={accountManagerId} onChange={(e) => setAccountManagerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="مالك المشروع">
          <Select value={projectOwnerId} onChange={(e) => setProjectOwnerId(e.target.value)}>
            <option value="">— بدون —</option>
            {(users.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="قائد الفريق">
          <Select value={teamLeaderId} onChange={(e) => setTeamLeaderId(e.target.value)}>
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

// ===== تبويب المستندات (CPW-R1B2) =====
// التنزيل يمرّ عبر نقطة نهاية مصادَقة فقط، ولا يظهر مسار التخزين إطلاقًا.
function DocumentsTab({ clientId, canWrite }: { clientId: string; canWrite: boolean }) {
  const [categoryCode, setCategoryCode] = useState('');
  const [confidentialityCode, setConfidentialityCode] = useState('');
  const [lifecycleStatus, setLifecycleStatus] = useState('');
  const [search, setSearch] = useState('');
  const [includeArchived, setIncludeArchived] = useState(false);
  const docs = useClientDocuments(clientId, {
    categoryCode: categoryCode || undefined,
    confidentialityCode: confidentialityCode || undefined,
    lifecycleStatus: (lifecycleStatus || undefined) as DocumentLifecycleStatus | undefined,
    search: search.trim() || undefined,
    includeArchived,
  });
  const usage = useClientStorageUsage(clientId);
  const [uploading, setUploading] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const rows = docs.data ?? [];
  const u = usage.data;

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-navy">مستندات العميل</h2>
        {canWrite && (
          <Button variant={uploading ? 'ghost' : 'primary'} onClick={() => setUploading((v) => !v)}>
            {uploading ? 'إغلاق' : '+ رفع مستند'}
          </Button>
        )}
      </div>

      {u && (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatCard label="المستندات" value={u.documentCount} />
          <StatCard label="النسخ" value={u.versionCount} />
          <StatCard label="المستهلَك" value={formatBytes(u.usedBytes)} />
          <StatCard
            label="المتبقّي من الحصّة"
            value={formatBytes(u.remainingBytes)}
            tone={u.remainingBytes <= 0 ? 'alert' : 'navy'}
          />
        </div>
      )}

      {u && !u.scannerConfigured && (
        <Alert tone="gold">
          لا يوجد محرّك فحص فيروسات مُفعَّل ({u.scanEngine}) — الملفّات تُقبَل بحالة «غير مفحوص». تعامَل مع
          المستندات الواردة بحذر.
        </Alert>
      )}

      {err && <Alert tone="alert">{err}</Alert>}

      {canWrite && uploading && (
        <UploadDocumentForm
          clientId={clientId}
          maxUploadSizeBytes={u?.maxUploadSizeBytes}
          allowedExtensions={u?.allowedExtensions ?? []}
          onDone={() => setUploading(false)}
        />
      )}

      <div className="grid gap-3 md:grid-cols-5">
        <Field label="التصنيف">
          <Select value={categoryCode} onChange={(e) => setCategoryCode(e.target.value)}>
            <option value="">— الكل —</option>
            {DOCUMENT_CATEGORY_CODES.map((code) => (
              <option key={code} value={code}>
                {documentCategoryLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="السرّيّة">
          <Select value={confidentialityCode} onChange={(e) => setConfidentialityCode(e.target.value)}>
            <option value="">— الكل —</option>
            {CONFIDENTIALITY_CODES.map((code) => (
              <option key={code} value={code}>
                {confidentialityLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الحالة">
          <Select value={lifecycleStatus} onChange={(e) => setLifecycleStatus(e.target.value)}>
            <option value="">— الكل —</option>
            {(['Draft', 'Current', 'Superseded', 'Archived'] as DocumentLifecycleStatus[]).map((s) => (
              <option key={s} value={s}>
                {documentLifecycleLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="بحث">
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="العنوان أو الوسوم…" />
        </Field>
        <label className="flex items-end gap-1.5 pb-2 text-xs text-ink-2">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(e) => setIncludeArchived(e.target.checked)}
          />
          إظهار المؤرشفة
        </label>
      </div>

      {docs.isLoading ? (
        <LoadingState label="يتم تحميل المستندات…" />
      ) : rows.length === 0 ? (
        <EmptyState title="لا توجد مستندات" description="لم تُرفَع مستندات مطابقة لهذا العميل بعد." />
      ) : (
        <div className="space-y-3">
          {rows.map((d) => (
            <DocumentRow
              key={d.id}
              clientId={clientId}
              doc={d}
              canWrite={canWrite}
              open={openId === d.id}
              onToggle={() => setOpenId((v) => (v === d.id ? null : d.id))}
              onError={setErr}
            />
          ))}
        </div>
      )}
    </Card>
  );
}

function DocumentRow({
  clientId,
  doc: d,
  canWrite,
  open,
  onToggle,
  onError,
}: {
  clientId: string;
  doc: ClientDocumentDto;
  canWrite: boolean;
  open: boolean;
  onToggle: () => void;
  onError: (msg: string | null) => void;
}) {
  const detail = useClientDocument(clientId, open ? d.id : undefined);
  const setArchived = useSetClientDocumentArchived(clientId);
  const remove = useDeleteClientDocument(clientId);
  const [editing, setEditing] = useState(false);
  const [addingVersion, setAddingVersion] = useState(false);

  async function toggleArchive() {
    onError(null);
    const reason = d.isArchived ? undefined : window.prompt('سبب الأرشفة (اختياري):') ?? undefined;
    try {
      await setArchived.mutateAsync({ documentId: d.id, archived: !d.isArchived, req: { reason: reason ?? null } });
    } catch (e) {
      onError(apiErrorMessage(e, 'تعذّر تغيير حالة الأرشفة.'));
    }
  }

  async function softDelete() {
    onError(null);
    const reason = window.prompt('سبب الحذف (إلزاميّ) — الحذف منطقيّ ولا يمسّ الملفّ المخزَّن:');
    if (!reason || !reason.trim()) return;
    try {
      await remove.mutateAsync({ documentId: d.id, req: { reason: reason.trim() } });
    } catch (e) {
      onError(apiErrorMessage(e, 'تعذّر حذف المستند.'));
    }
  }

  return (
    <div className={`rounded-lg border p-3 text-sm ${d.isArchived ? 'border-line bg-offwhite opacity-70' : 'border-line'}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold text-navy">{d.title}</span>
          <Badge tone={documentLifecycleTone(d.lifecycleStatus)}>{documentLifecycleLabel[d.lifecycleStatus]}</Badge>
          <Badge tone="navy">{codeLabel(documentCategoryLabel, d.categoryCode)}</Badge>
          {d.confidentialityCode && (
            <Badge tone="gold">{codeLabel(confidentialityLabel, d.confidentialityCode)}</Badge>
          )}
          {d.currentScanStatus && (
            <Badge tone={documentScanStatusTone(d.currentScanStatus)}>
              {documentScanStatusLabel[d.currentScanStatus]}
            </Badge>
          )}
          <Badge tone="muted">{documentVisibilityLabel[d.visibilityType]}</Badge>
          {d.isArchived && <Badge tone="muted">مؤرشف</Badge>}
        </div>
        <div className="flex flex-wrap gap-2">
          {d.currentVersionId && (
            <Button
              variant="ghost"
              onClick={async () => {
                onError(null);
                try {
                  await downloadClientDocument(clientId, d.id, d.currentFileName ?? d.title);
                } catch (e) {
                  onError(apiErrorMessage(e, 'تعذّر تنزيل المستند.'));
                }
              }}
            >
              تنزيل
            </Button>
          )}
          <Button variant="ghost" onClick={onToggle}>
            {open ? 'إخفاء النسخ' : `النسخ (${d.versionCount})`}
          </Button>
          {canWrite && (
            <>
              <Button variant="ghost" onClick={() => { setEditing((v) => !v); setAddingVersion(false); }}>
                تعديل
              </Button>
              <Button variant="ghost" onClick={() => { setAddingVersion((v) => !v); setEditing(false); }}>
                + نسخة
              </Button>
              <Button variant="ghost" disabled={setArchived.isPending} onClick={toggleArchive}>
                {d.isArchived ? 'إلغاء الأرشفة' : 'أرشفة'}
              </Button>
              <Button variant="danger" disabled={remove.isPending} onClick={softDelete}>
                حذف
              </Button>
            </>
          )}
        </div>
      </div>

      <dl className="mt-2 grid grid-cols-2 gap-2 text-xs text-ink-2 lg:grid-cols-4">
        <Info label="النسخة الحالية" value={d.currentVersionNo ? `v${d.currentVersionNo}` : '—'} />
        <Info label="الملفّ" value={d.currentFileName ?? '—'} />
        <Info label="الحجم" value={formatBytes(d.currentSizeBytes)} />
        <Info label="رفعه" value={d.uploadedByName ?? '—'} />
        <Info label="تاريخ الإضافة" value={formatDate(d.createdAtUtc)} />
        {d.tags && <Info label="الوسوم" value={d.tags} />}
        {d.isArchived && d.archiveReason && <Info label="سبب الأرشفة" value={d.archiveReason} />}
        {/* قائمتا التصريح لا تُعرَضان إلّا لمن يملك صلاحيّة إدارة رؤية المستندات (§12). */}
        {d.canManageVisibility && (d.allowedRoles?.length ?? 0) > 0 && (
          <Info
            label="الأدوار المصرّح لها"
            value={d.allowedRoles!.map((r) => roleLabel[r as Role] ?? r).join('، ')}
          />
        )}
        {d.canManageVisibility && (d.allowedUserIds?.length ?? 0) > 0 && (
          <AllowedUsersInfo userIds={d.allowedUserIds!} />
        )}
      </dl>
      {d.description && <p className="mt-2 text-xs text-ink-2">{d.description}</p>}

      {canWrite && editing && (
        <EditDocumentForm clientId={clientId} doc={d} onDone={() => setEditing(false)} />
      )}
      {canWrite && addingVersion && (
        <AddVersionForm clientId={clientId} documentId={d.id} onDone={() => setAddingVersion(false)} />
      )}

      {open && (
        <div className="mt-3 rounded-lg bg-offwhite p-3">
          {detail.isLoading ? (
            <LoadingState label="يتم تحميل سجلّ النسخ…" />
          ) : (
            <ul className="space-y-2 text-xs">
              {(detail.data?.versions ?? []).map((v) => (
                <li key={v.id} className="flex flex-wrap items-center justify-between gap-2">
                  <span className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-navy">v{v.versionNo}</span>
                    {v.isCurrent && <Badge tone="success">سارية</Badge>}
                    <Badge tone={documentScanStatusTone(v.scanStatus)}>{documentScanStatusLabel[v.scanStatus]}</Badge>
                    <span>{v.originalFileName}</span>
                    <span className="text-ink-2">{formatBytes(v.sizeBytes)}</span>
                    <span className="text-ink-2">{formatDate(v.createdAtUtc)}</span>
                    {v.changeNote && <span className="text-ink-2">— {v.changeNote}</span>}
                  </span>
                  <Button
                    variant="ghost"
                    onClick={async () => {
                      onError(null);
                      try {
                        await downloadClientDocumentVersion(clientId, d.id, v.id, v.originalFileName);
                      } catch (e) {
                        onError(apiErrorMessage(e, 'تعذّر تنزيل النسخة.'));
                      }
                    }}
                  >
                    تنزيل
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

// ===== محرّر سياسة رؤية المستند (CPW-R2) =====
// الخادم هو الفاصل الوحيد في التطبيق؛ ما هنا اختيارٌ وعرضٌ فقط.
// مجموعات الأدوار مرآةٌ لـ DocumentVisibilityPolicy في الخادم — تُستعمل حصرًا لتحذير «قد لا ترى المستند».
const VISIBILITY_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager'];
const VISIBILITY_FINANCE_ROLES: Role[] = ['FinanceManager', 'Accountant'];
const VISIBILITY_HR_MANAGEMENT_ROLES: Role[] = ['HR', 'Admin', 'CEO', 'GeneralManager'];
const ALL_ROLE_CODES = Object.keys(roleLabel) as Role[];

/// هل يبقى صاحبُ الأدوار المعطاة قادرًا على رؤية مستندٍ بهذه السياسة؟
/// `null` = غير محسوم في الواجهة (يعتمد على نطاق العميل/المشروع في الخادم) ⇒ لا تحذير.
function visibilitySelfAccess(
  type: DocumentVisibilityType,
  myRoles: Role[],
  myUserId: string | undefined,
  allowedRoles: string[],
  allowedUserIds: string[],
): boolean | null {
  const any = (set: Role[]) => myRoles.some((r) => set.includes(r));
  switch (type) {
    case 'ManagementOnly':
      return any(VISIBILITY_MANAGEMENT_ROLES);
    case 'ManagementAndFinance':
      return any(VISIBILITY_MANAGEMENT_ROLES) || any(VISIBILITY_FINANCE_ROLES);
    case 'FinanceOnly':
      return any(VISIBILITY_FINANCE_ROLES);
    case 'HRManagementOnly':
      return any(VISIBILITY_HR_MANAGEMENT_ROLES);
    case 'CustomRoles':
      return allowedRoles.length === 0 ? null : myRoles.some((r) => allowedRoles.includes(r));
    case 'CustomUsers':
      return allowedUserIds.length === 0 ? null : !!myUserId && allowedUserIds.includes(myUserId);
    default:
      // ClientScoped / ProjectTeam — يعتمدان على نطاق العميل أو المشروع، ولا يُحسمان هنا.
      return null;
  }
}

function DocumentVisibilityEditor({
  visibilityType,
  allowedRoles,
  allowedUserIds,
  onVisibilityTypeChange,
  onAllowedRolesChange,
  onAllowedUserIdsChange,
}: {
  visibilityType: DocumentVisibilityType;
  allowedRoles: string[];
  allowedUserIds: string[];
  onVisibilityTypeChange: (v: DocumentVisibilityType) => void;
  onAllowedRolesChange: (v: string[]) => void;
  onAllowedUserIdsChange: (v: string[]) => void;
}) {
  const { user } = useAuth();
  const users = useDirectoryUsers();
  const selfAccess = visibilitySelfAccess(
    visibilityType,
    user?.roles ?? [],
    user?.userId,
    allowedRoles,
    allowedUserIds,
  );

  function toggle(list: string[], value: string, apply: (v: string[]) => void) {
    apply(list.includes(value) ? list.filter((x) => x !== value) : [...list, value]);
  }

  return (
    <div className="md:col-span-2 rounded-lg border border-line bg-white p-3">
      <h4 className="font-semibold text-navy">من يمكنه رؤية هذا المستند؟</h4>
      <p className="mt-1 text-xs text-ink-2">
        الرؤية والتنزيل مرتبطان — من لا يستطيع رؤية المستند لا يستطيع تنزيله.
      </p>
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="سياسة الرؤية">
          <Select
            value={visibilityType}
            onChange={(e) => onVisibilityTypeChange(e.target.value as DocumentVisibilityType)}
          >
            {DOCUMENT_VISIBILITY_TYPES.map((t) => (
              <option key={t} value={t}>
                {documentVisibilityLabel[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {visibilityType === 'CustomRoles' && (
        <div className="mt-3">
          <p className="text-xs font-semibold text-navy">الأدوار المصرّح لها</p>
          <div className="mt-2 flex flex-wrap gap-x-4 gap-y-2">
            {ALL_ROLE_CODES.map((r) => (
              <label key={r} className="flex items-center gap-1.5 text-xs text-ink-2">
                <input
                  type="checkbox"
                  checked={allowedRoles.includes(r)}
                  onChange={() => toggle(allowedRoles, r, onAllowedRolesChange)}
                />
                {roleLabel[r]}
              </label>
            ))}
          </div>
          {allowedRoles.length === 0 && (
            <p className="mt-2 text-xs text-red-700">اختر دورًا واحدًا على الأقلّ.</p>
          )}
        </div>
      )}

      {visibilityType === 'CustomUsers' && (
        <div className="mt-3">
          <p className="text-xs font-semibold text-navy">الأشخاص المصرّح لهم</p>
          {users.isLoading ? (
            <LoadingState label="يتم تحميل قائمة المستخدمين…" />
          ) : (
            <div className="mt-2 max-h-48 overflow-y-auto rounded-lg border border-line p-2">
              {(users.data ?? []).map((u) => (
                <label key={u.id} className="flex items-center gap-1.5 py-0.5 text-xs text-ink-2">
                  <input
                    type="checkbox"
                    checked={allowedUserIds.includes(u.id)}
                    onChange={() => toggle(allowedUserIds, u.id, onAllowedUserIdsChange)}
                  />
                  {u.fullName}
                </label>
              ))}
            </div>
          )}
          {allowedUserIds.length === 0 && (
            <p className="mt-2 text-xs text-red-700">اختر شخصًا واحدًا على الأقلّ.</p>
          )}
        </div>
      )}

      {selfAccess === false && (
        <div className="mt-3">
          <Alert tone="gold">
            تنبيه: السياسة المختارة لا تشمل حسابك — قد لا تتمكّن من رؤية هذا المستند أو تنزيله بعد الحفظ.
          </Alert>
        </div>
      )}
    </div>
  );
}

/// يحوّل معرّفات المستخدمين المصرّح لهم إلى أسماء — لا تُعرَض المعرّفات الخام.
/// يُركَّب فقط لمن يملك صلاحيّة إدارة الرؤية، فلا يُجلَب الدليل لغيرهم.
function AllowedUsersInfo({ userIds }: { userIds: string[] }) {
  const users = useDirectoryUsers();
  const byId = new Map((users.data ?? []).map((u) => [u.id, u.fullName]));
  return (
    <Info
      label="الأشخاص المصرّح لهم"
      value={users.isLoading ? '…' : userIds.map((id) => byId.get(id) ?? id).join('، ')}
    />
  );
}

function UploadDocumentForm({
  clientId,
  maxUploadSizeBytes,
  allowedExtensions,
  onDone,
}: {
  clientId: string;
  maxUploadSizeBytes?: number;
  allowedExtensions: string[];
  onDone: () => void;
}) {
  const upload = useUploadClientDocument(clientId);
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const initialCategory = DOCUMENT_CATEGORY_CODES[0] ?? '';
  const [categoryCode, setCategoryCode] = useState(initialCategory);
  const [confidentialityCode, setConfidentialityCode] = useState('');
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [err, setErr] = useState<string | null>(null);
  // سياسة الرؤية (CPW-R2): تتبع افتراضيّ التصنيف حتّى يختار المستخدم سياسةً يدويًّا، فلا تُداس بعدها.
  const [visibilityType, setVisibilityType] = useState<DocumentVisibilityType>(
    defaultVisibilityForCategory(initialCategory),
  );
  const [visibilityTouched, setVisibilityTouched] = useState(false);
  const [allowedRoles, setAllowedRoles] = useState<string[]>([]);
  const [allowedUserIds, setAllowedUserIds] = useState<string[]>([]);

  function changeCategory(code: string) {
    setCategoryCode(code);
    if (!visibilityTouched) setVisibilityType(defaultVisibilityForCategory(code));
  }

  async function submit() {
    setErr(null);
    if (!file) {
      setErr('اختر ملفًّا للرفع.');
      return;
    }
    if (!title.trim()) {
      setErr('عنوان المستند مطلوب.');
      return;
    }
    if (maxUploadSizeBytes && file.size > maxUploadSizeBytes) {
      setErr(`حجم الملفّ يتجاوز الحدّ المسموح (${formatBytes(maxUploadSizeBytes)}).`);
      return;
    }
    if (visibilityType === 'CustomRoles' && allowedRoles.length === 0) {
      setErr('سياسة «أدوار مخصصة» تتطلّب اختيار دور واحد على الأقلّ.');
      return;
    }
    if (visibilityType === 'CustomUsers' && allowedUserIds.length === 0) {
      setErr('سياسة «أشخاص محددون» تتطلّب اختيار شخص واحد على الأقلّ.');
      return;
    }
    try {
      await upload.mutateAsync({
        file,
        title: title.trim(),
        categoryCode,
        confidentialityCode: confidentialityCode || undefined,
        description: description.trim() || undefined,
        tags: tags.trim() || undefined,
        changeNote: changeNote.trim() || undefined,
        visibilityType,
        allowedRoles: visibilityType === 'CustomRoles' ? allowedRoles : undefined,
        allowedUserIds: visibilityType === 'CustomUsers' ? allowedUserIds : undefined,
      });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر رفع المستند.'));
    }
  }

  return (
    <div className="rounded-lg border border-orange-200 bg-orange-50/40 p-3">
      <h3 className="font-semibold text-navy">رفع مستند جديد</h3>
      {allowedExtensions.length > 0 && (
        <p className="mt-1 text-xs text-ink-2">
          الامتدادات المسموحة: {allowedExtensions.join('، ')}
          {maxUploadSizeBytes ? ` — الحدّ الأقصى ${formatBytes(maxUploadSizeBytes)}` : ''}
        </p>
      )}
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="الملفّ">
          <input
            type="file"
            className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </Field>
        <Field label="عنوان المستند">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} />
        </Field>
        <Field label="التصنيف">
          <Select value={categoryCode} onChange={(e) => changeCategory(e.target.value)}>
            {DOCUMENT_CATEGORY_CODES.map((code) => (
              <option key={code} value={code}>
                {documentCategoryLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="درجة السرّيّة">
          <Select value={confidentialityCode} onChange={(e) => setConfidentialityCode(e.target.value)}>
            <option value="">— بدون —</option>
            {CONFIDENTIALITY_CODES.map((code) => (
              <option key={code} value={code}>
                {confidentialityLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الوسوم (مفصولة بفواصل)">
          <Input value={tags} onChange={(e) => setTags(e.target.value)} />
        </Field>
        <Field label="ملاحظة النسخة">
          <Input value={changeNote} onChange={(e) => setChangeNote(e.target.value)} />
        </Field>
        <div className="md:col-span-2">
          <Field label="الوصف">
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </div>
        <DocumentVisibilityEditor
          visibilityType={visibilityType}
          allowedRoles={allowedRoles}
          allowedUserIds={allowedUserIds}
          onVisibilityTypeChange={(v) => {
            setVisibilityTouched(true);
            setVisibilityType(v);
          }}
          onAllowedRolesChange={setAllowedRoles}
          onAllowedUserIdsChange={setAllowedUserIds}
        />
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={upload.isPending}>
          {upload.isPending ? 'جارٍ الرفع…' : 'رفع المستند'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

function AddVersionForm({
  clientId,
  documentId,
  onDone,
}: {
  clientId: string;
  documentId: string;
  onDone: () => void;
}) {
  const addVersion = useAddClientDocumentVersion(clientId);
  const [file, setFile] = useState<File | null>(null);
  const [changeNote, setChangeNote] = useState('');
  const [err, setErr] = useState<string | null>(null);

  async function submit() {
    setErr(null);
    if (!file) {
      setErr('اختر ملفًّا للرفع.');
      return;
    }
    try {
      await addVersion.mutateAsync({ documentId, file, changeNote: changeNote.trim() || undefined });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر إضافة النسخة.'));
    }
  }

  return (
    <div className="mt-3 rounded-lg border border-line bg-white p-3">
      <h3 className="font-semibold text-navy">إضافة نسخة أحدث</h3>
      <p className="mt-1 text-xs text-ink-2">النسخة السابقة تصبح «مُستبدَلة» ولا تُحذف.</p>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="الملفّ">
          <input
            type="file"
            className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </Field>
        <Field label="ملاحظة التغيير">
          <Input value={changeNote} onChange={(e) => setChangeNote(e.target.value)} />
        </Field>
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={addVersion.isPending}>
          {addVersion.isPending ? 'جارٍ الرفع…' : 'حفظ النسخة'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

function EditDocumentForm({
  clientId,
  doc: d,
  onDone,
}: {
  clientId: string;
  doc: ClientDocumentDto;
  onDone: () => void;
}) {
  const update = useUpdateClientDocument(clientId);
  const [title, setTitle] = useState(d.title);
  const [categoryCode, setCategoryCode] = useState(d.categoryCode);
  const [confidentialityCode, setConfidentialityCode] = useState(d.confidentialityCode ?? '');
  const [lifecycleStatus, setLifecycleStatus] = useState<DocumentLifecycleStatus>(d.lifecycleStatus);
  const [description, setDescription] = useState(d.description ?? '');
  const [tags, setTags] = useState(d.tags ?? '');
  const [err, setErr] = useState<string | null>(null);
  // في التعديل لا يُطبَّق افتراضيّ التصنيف إطلاقًا — تبقى السياسة القائمة حتّى يغيّرها المستخدم صراحةً.
  const [visibilityType, setVisibilityType] = useState<DocumentVisibilityType>(d.visibilityType);
  const [allowedRoles, setAllowedRoles] = useState<string[]>(d.allowedRoles ?? []);
  const [allowedUserIds, setAllowedUserIds] = useState<string[]>(d.allowedUserIds ?? []);

  async function submit() {
    setErr(null);
    if (!title.trim()) {
      setErr('عنوان المستند مطلوب.');
      return;
    }
    if (d.canManageVisibility && visibilityType === 'CustomRoles' && allowedRoles.length === 0) {
      setErr('سياسة «أدوار مخصصة» تتطلّب اختيار دور واحد على الأقلّ.');
      return;
    }
    if (d.canManageVisibility && visibilityType === 'CustomUsers' && allowedUserIds.length === 0) {
      setErr('سياسة «أشخاص محددون» تتطلّب اختيار شخص واحد على الأقلّ.');
      return;
    }
    try {
      await update.mutateAsync({
        documentId: d.id,
        req: {
          title: title.trim(),
          categoryCode,
          confidentialityCode: confidentialityCode || null,
          lifecycleStatus,
          description: description.trim() || null,
          tags: tags.trim() || null,
          // من لا يملك صلاحيّة إدارة الرؤية لا يُرسِل السياسة إطلاقًا ⇒ تبقى كما هي على الخادم.
          ...(d.canManageVisibility
            ? {
                visibilityType,
                allowedRoles: visibilityType === 'CustomRoles' ? allowedRoles : null,
                allowedUserIds: visibilityType === 'CustomUsers' ? allowedUserIds : null,
              }
            : {}),
        },
      });
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ التعديل.'));
    }
  }

  return (
    <div className="mt-3 rounded-lg border border-line bg-white p-3">
      <h3 className="font-semibold text-navy">تعديل بيانات المستند</h3>
      <p className="mt-1 text-xs text-ink-2">التعديل على البيانات الوصفيّة فقط — لا يمسّ أيّ ملفّ.</p>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="العنوان">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} />
        </Field>
        <Field label="التصنيف">
          <Select value={categoryCode} onChange={(e) => setCategoryCode(e.target.value)}>
            {DOCUMENT_CATEGORY_CODES.map((code) => (
              <option key={code} value={code}>
                {documentCategoryLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="درجة السرّيّة">
          <Select value={confidentialityCode} onChange={(e) => setConfidentialityCode(e.target.value)}>
            <option value="">— بدون —</option>
            {CONFIDENTIALITY_CODES.map((code) => (
              <option key={code} value={code}>
                {confidentialityLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الحالة">
          <Select
            value={lifecycleStatus}
            onChange={(e) => setLifecycleStatus(e.target.value as DocumentLifecycleStatus)}
          >
            {(['Draft', 'Current', 'Superseded', 'Archived'] as DocumentLifecycleStatus[]).map((s) => (
              <option key={s} value={s}>
                {documentLifecycleLabel[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الوسوم">
          <Input value={tags} onChange={(e) => setTags(e.target.value)} />
        </Field>
        <div className="md:col-span-2">
          <Field label="الوصف">
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </div>
        {d.canManageVisibility && (
          <DocumentVisibilityEditor
            visibilityType={visibilityType}
            allowedRoles={allowedRoles}
            allowedUserIds={allowedUserIds}
            onVisibilityTypeChange={setVisibilityType}
            onAllowedRolesChange={setAllowedRoles}
            onAllowedUserIdsChange={setAllowedUserIds}
          />
        )}
      </div>
      <div className="mt-3 flex gap-2">
        <Button variant="primary" onClick={submit} disabled={update.isPending}>
          {update.isPending ? 'جارٍ الحفظ…' : 'حفظ'}
        </Button>
        <Button variant="ghost" onClick={onDone}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

// ===== تبويب الروابط المهمّة (CPW-R1B2) =====
function LinksTab({ clientId, canWrite }: { clientId: string; canWrite: boolean }) {
  const [includeInactive, setIncludeInactive] = useState(false);
  const links = useClientExternalLinks(clientId, includeInactive);
  const [creating, setCreating] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const setActive = useSetClientExternalLinkActive(clientId);
  const [err, setErr] = useState<string | null>(null);

  const rows = links.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-navy">الروابط المهمّة</h2>
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
              {creating ? 'إغلاق' : '+ رابط'}
            </Button>
          )}
        </div>
      </div>

      <Alert tone="navy">
        الرابط مرجع عنوان فقط — لا تُدرِج داخله أيّ كلمة مرور أو رمز وصول أو مفتاح واجهة برمجيّة.
      </Alert>

      {err && <Alert tone="alert">{err}</Alert>}

      {canWrite && creating && <ExternalLinkForm clientId={clientId} onDone={() => setCreating(false)} />}

      {links.isLoading ? (
        <LoadingState label="يتم تحميل الروابط…" />
      ) : rows.length === 0 ? (
        <EmptyState title="لا توجد روابط" description="لم تُضَف روابط مهمّة لهذا العميل بعد." />
      ) : (
        <div className="space-y-3">
          {rows.map((l) =>
            canWrite && editId === l.id ? (
              <ExternalLinkForm key={l.id} clientId={clientId} link={l} onDone={() => setEditId(null)} />
            ) : (
              <div
                key={l.id}
                className={`rounded-lg border p-3 text-sm ${l.isActive ? 'border-line' : 'border-line bg-offwhite opacity-70'}`}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-navy">{l.title}</span>
                    <Badge tone="navy">{codeLabel(linkCategoryLabel, l.categoryCode)}</Badge>
                    {!l.isActive && <Badge tone="muted">معطَّل</Badge>}
                  </div>
                  {canWrite && (
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={() => { setEditId(l.id); setCreating(false); }}>
                        تعديل
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={async () => {
                          setErr(null);
                          try {
                            await setActive.mutateAsync({ id: l.id, active: !l.isActive });
                          } catch (e) {
                            setErr(apiErrorMessage(e, 'تعذّر تغيير حالة الرابط.'));
                          }
                        }}
                      >
                        {l.isActive ? 'تعطيل' : 'تفعيل'}
                      </Button>
                    </div>
                  )}
                </div>
                <a
                  href={l.url}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="mt-1 block break-all text-xs text-orange-600 hover:underline"
                >
                  {l.url}
                </a>
                {l.description && <p className="mt-2 text-xs text-ink-2">{l.description}</p>}
                <div className="mt-2 text-xs text-ink-2">
                  أضافه {l.createdByName ?? '—'} — {formatDate(l.createdAtUtc)}
                </div>
              </div>
            ),
          )}
        </div>
      )}
    </Card>
  );
}

function ExternalLinkForm({
  clientId,
  link,
  onDone,
}: {
  clientId: string;
  link?: ClientExternalLinkDto;
  onDone: () => void;
}) {
  const create = useCreateClientExternalLink(clientId);
  const update = useUpdateClientExternalLink(clientId);
  const [title, setTitle] = useState(link?.title ?? '');
  const [url, setUrl] = useState(link?.url ?? '');
  const [categoryCode, setCategoryCode] = useState(link?.categoryCode ?? LINK_CATEGORY_CODES[0] ?? '');
  const [description, setDescription] = useState(link?.description ?? '');
  const [sortOrder, setSortOrder] = useState(String(link?.sortOrder ?? 0));
  const [err, setErr] = useState<string | null>(null);
  const pending = create.isPending || update.isPending;

  async function submit() {
    setErr(null);
    if (!title.trim()) {
      setErr('عنوان الرابط مطلوب.');
      return;
    }
    if (!url.trim()) {
      setErr('العنوان الإلكتروني مطلوب.');
      return;
    }
    const req: CreateClientExternalLinkRequest = {
      title: title.trim(),
      url: url.trim(),
      categoryCode,
      description: description.trim() || null,
      sortOrder: Number(sortOrder) || 0,
    };
    try {
      if (link) await update.mutateAsync({ id: link.id, req });
      else await create.mutateAsync(req);
      onDone();
    } catch (e) {
      setErr(apiErrorMessage(e, 'تعذّر حفظ الرابط.'));
    }
  }

  return (
    <div className="rounded-lg border border-orange-200 bg-orange-50/40 p-3">
      <h3 className="font-semibold text-navy">{link ? 'تعديل الرابط' : 'إضافة رابط'}</h3>
      {err && (
        <div className="mt-2">
          <Alert tone="alert">{err}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 md:grid-cols-2">
        <Field label="العنوان">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} />
        </Field>
        <Field label="الرابط">
          <Input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://…" />
        </Field>
        <Field label="التصنيف">
          <Select value={categoryCode} onChange={(e) => setCategoryCode(e.target.value)}>
            {LINK_CATEGORY_CODES.map((code) => (
              <option key={code} value={code}>
                {linkCategoryLabel[code]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="ترتيب العرض">
          <Input type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        </Field>
        <div className="md:col-span-2">
          <Field label="الوصف">
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </div>
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

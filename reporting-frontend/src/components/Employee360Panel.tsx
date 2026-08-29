// P2-EMP-003 — لوحة Employee 360 داخل صفحة ملفّ الموظّف القائمة (لا صفحة منافسة).
// مبدأ أمنيّ حاكم: **الأقسام تُرسَم من مفاتيح الخادم حصرًا**. القسم غير المصرَّح به لا يصل
// أصلًا في `sections`، فلا يوجد في هذا الملفّ أيّ إخفاء بصريّ ولا شرط صلاحيّة محسوب في المتصفّح.
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { formatDate, formatDateTime } from '../lib/format';
import { Badge, Card, EmptyState, StatCard } from '../components/ui';
import {
  CardsSkeleton,
  FeatureDisabledState,
  ForbiddenState,
  QueryError,
  TableSkeleton,
} from '../components/states';
import { classifySurfaceState, useFeatureEnabled } from '../lib/surfaceState';
import { FEATURES } from '../lib/navConfig';
import { EmployeeChecklistPanel } from './EmployeeChecklistPanel';
import {
  EMPLOYEE_360_SECTION_ORDER,
  type Employee360Balance,
  type Employee360Dto,
  type Employee360Identity,
  type Employee360OperationalSummary,
  type Employee360Section,
  type Employee360SectionStatus,
  type Employee360TimelineEvent,
} from '../types/employee360';

type Tone = 'navy' | 'success' | 'alert' | 'gold' | 'muted';

const STATUS_LABEL: Record<Employee360SectionStatus, string> = {
  Ready: 'جاهز',
  NoData: 'لا بيانات',
  Partial: 'جزئيّ',
  Error: 'تعذّر التحميل',
};

const STATUS_TONE: Record<Employee360SectionStatus, Tone> = {
  Ready: 'success',
  NoData: 'muted',
  Partial: 'gold',
  Error: 'alert',
};

const QUALITY_LABEL: Record<string, string> = {
  Complete: 'بيانات مكتملة',
  Partial: 'بيانات جزئيّة',
  Unavailable: 'غير متاحة',
};

const RELATION_LABEL: Record<string, string> = {
  Self: 'ملفّي',
  DirectTeam: 'مرؤوس مباشر',
  Department: 'ضمن إدارتي',
  Company: 'على مستوى الشركة',
};

/** أعمدة العرض لكلّ قسم — ما ليس معرَّفًا هنا يُعرض بعرض عامّ لا بافتراض شكل. */
type Column = { label: string; field: string; kind?: 'date' | 'dateTime' | 'bool' | 'number' };

const SECTION_COLUMNS: Record<string, Column[]> = {
  reports: [
    { label: 'القالب', field: 'templateTitle' },
    { label: 'الفترة', field: 'periodKey' },
    { label: 'النوع', field: 'periodType' },
    { label: 'الحالة', field: 'status' },
    { label: 'تاريخ التسليم', field: 'submittedAtUtc', kind: 'dateTime' },
  ],
  kpi: [
    { label: 'القالب', field: 'templateTitle' },
    { label: 'الفترة', field: 'periodKey' },
    { label: 'الدرجة', field: 'totalScore', kind: 'number' },
    { label: 'الحالة', field: 'status' },
    { label: 'الاتجاه', field: 'trend' },
  ],
  leaveAndPermissions: [
    { label: 'النوع', field: 'type' },
    { label: 'من', field: 'startDate', kind: 'date' },
    { label: 'إلى', field: 'endDate', kind: 'date' },
    { label: 'الحالة', field: 'status' },
    { label: 'الخطوة الحاليّة', field: 'currentStep' },
    // «السبب» عمود اختياريّ: لا يصل من الخادم إلّا لمن يملك إذن HrOnly، ولا يُعرض إن لم يصل.
    { label: 'السبب', field: 'reason' },
  ],
  requestsAndBalances: [
    { label: 'نوع الطلب', field: 'requestType' },
    { label: 'العنوان', field: 'title' },
    { label: 'الحالة', field: 'status' },
    { label: 'أُنشئ في', field: 'createdAtUtc', kind: 'dateTime' },
  ],
  attendanceAndCompliance: [
    { label: 'النوع', field: 'typeNameAr' },
    { label: 'التاريخ', field: 'incidentDate', kind: 'date' },
    { label: 'الحالة', field: 'status' },
    { label: 'مؤكَّدة', field: 'isConfirmed', kind: 'bool' },
  ],
  notes: [
    { label: 'النوع', field: 'noteType' },
    { label: 'النصّ', field: 'body' },
    { label: 'الحالة', field: 'status' },
    { label: 'تتطلّب إجراءً', field: 'requiresAction', kind: 'bool' },
    { label: 'التاريخ', field: 'createdAtUtc', kind: 'dateTime' },
  ],
  governance: [
    { label: 'النوع', field: 'kind' },
    { label: 'العنوان', field: 'title' },
    { label: 'الحالة', field: 'status' },
    { label: 'التاريخ', field: 'createdAtUtc', kind: 'dateTime' },
  ],
  developmentAndTraining: [
    { label: 'النوع', field: 'kind' },
    { label: 'العنوان', field: 'title' },
    { label: 'الحالة', field: 'status' },
    { label: 'الاستحقاق', field: 'dueDateUtc', kind: 'date' },
  ],
};

/**
 * رابط المصدر لكلّ صفّ — «الرابط إلى المصدر» المطلوب في المواصفة. الوجهة سطحٌ مستقلّ
 * يعيد فرض التخويل بنفسه؛ فالرابط اختصار تنقّل لا منح صلاحيّة. ومن لا يملك الواقعة يجد 404
 * هناك كما يجدها لو كتب المسار بيده.
 */
type RowLink = (row: Record<string, unknown>) => string | null;

const SECTION_ROW_LINK: Partial<Record<string, RowLink>> = {
  attendanceAndCompliance: (row) =>
    typeof row.id === 'string' ? `/app/attendance?incident=${row.id}` : null,
};

function cellValue(row: Record<string, unknown>, col: Column): string {
  const raw = row[col.field];
  if (raw === null || raw === undefined) return '—';
  switch (col.kind) {
    case 'date':
      return formatDate(String(raw));
    case 'dateTime':
      return formatDateTime(String(raw));
    case 'bool':
      return raw ? 'نعم' : 'لا';
    default:
      return String(raw);
  }
}

/** عمود يُحذف كلّيًّا إذا لم يصل حقله في أيّ صفّ — كي لا نعرض «—» لحقل حجبه الخادم. */
function visibleColumns(cols: Column[], rows: Record<string, unknown>[]): Column[] {
  return cols.filter((c) => rows.some((r) => r[c.field] !== undefined));
}

function SectionTable({ sectionKey, items }: { sectionKey: string; items: Record<string, unknown>[] }) {
  const cols = visibleColumns(SECTION_COLUMNS[sectionKey] ?? [], items);
  const linkOf = SECTION_ROW_LINK[sectionKey];
  if (cols.length === 0) {
    return (
      <ul className="space-y-2 text-sm text-ink">
        {items.map((row, i) => (
          <li key={i} className="rounded-lg border border-line p-3">
            {String(row.title ?? row.label ?? row.name ?? '—')}
          </li>
        ))}
      </ul>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-right text-sm">
        <thead>
          <tr className="border-b border-line text-xs text-ink-2">
            {cols.map((c) => (
              <th key={c.field} scope="col" className="px-3 py-2 font-medium">
                {c.label}
              </th>
            ))}
            {linkOf && <th scope="col" className="px-3 py-2 font-medium">المصدر</th>}
          </tr>
        </thead>
        <tbody>
          {items.map((row, i) => {
            const href = linkOf?.(row) ?? null;
            return (
              <tr key={i} className="border-b border-line/60 last:border-0">
                {cols.map((c) => (
                  <td key={c.field} className="px-3 py-2 text-ink">
                    {cellValue(row, c)}
                  </td>
                ))}
                {linkOf && (
                  <td className="px-3 py-2">
                    {href ? (
                      <Link className="text-navy underline" to={href}>
                        فتح التفاصيل
                      </Link>
                    ) : (
                      '—'
                    )}
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function IdentityBody({ identity }: { identity: Employee360Identity }) {
  const fields: [string, string][] = [
    ['الاسم', identity.fullName],
    ['البريد', identity.email ?? '—'],
    ['المسمّى الوظيفيّ', identity.jobRoleName ?? '—'],
    ['الفريق', identity.teamName ?? '—'],
    ['الإدارة', identity.departmentName ?? '—'],
    ['المدير المباشر', identity.directManagerName ?? '—'],
    ['تاريخ الالتحاق', formatDate(identity.joinedAtUtc)],
    ['الحالة', identity.isActive ? 'مفعّل' : 'غير مفعّل'],
  ];
  return (
    <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
      {fields.map(([label, value]) => (
        <div key={label}>
          <dt className="text-ink-2">{label}</dt>
          <dd className="font-medium text-ink">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function OperationalSummaryBody({ summary }: { summary: Employee360OperationalSummary }) {
  // «صفر» قيمة حقيقيّة تُعرض رقمًا؛ أمّا «لا بيانات» فيُعالَج قبل الوصول إلى هنا بحالة NoData.
  const cards: { label: string; value: number | string; tone?: Tone }[] = [
    { label: 'تقارير مُسلَّمة', value: summary.reportsSubmitted },
    { label: 'تقارير مُعادة', value: summary.reportsReturned, tone: summary.reportsReturned > 0 ? 'alert' : 'navy' },
    {
      label: 'تحتاج إجراءً',
      value: summary.reportsNeedsAction,
      tone: summary.reportsNeedsAction > 0 ? 'alert' : 'navy',
    },
    { label: 'عدد تقييمات KPI', value: summary.kpiEvaluationCount },
    {
      label: `آخر درجة KPI${summary.lastKpiPeriodKey ? ` (${summary.lastKpiPeriodKey})` : ''}`,
      value: summary.lastKpiScore ?? 'لا تقييم',
    },
    { label: 'إجازات مفتوحة', value: summary.openLeaveRequests },
    { label: 'طلبات خدمة مفتوحة', value: summary.openServiceRequests },
    {
      label: 'ملاحظات تتطلّب إجراءً',
      value: summary.openNotesRequiringAction,
      tone: summary.openNotesRequiringAction > 0 ? 'alert' : 'navy',
    },
    { label: 'بنود حوكمة مفتوحة', value: summary.openGovernanceItems },
  ];
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {cards.map((c) => (
        <StatCard key={c.label} label={c.label} value={c.value} tone={c.tone ?? 'navy'} />
      ))}
    </div>
  );
}

function BalancesBody({ balances }: { balances: Employee360Balance[] }) {
  if (balances.length === 0) return null;
  return (
    <div className="mb-4">
      <h4 className="mb-2 text-sm font-semibold text-navy">الأرصدة</h4>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {balances.map((b) => (
          <StatCard key={`${b.balanceType}-${b.year}`} label={`${b.balanceType} — ${b.year}`} value={b.net} />
        ))}
      </div>
    </div>
  );
}

function TimelineBody({ events }: { events: Employee360TimelineEvent[] }) {
  const [kind, setKind] = useState('');
  const [source, setSource] = useState('');
  const [onlyMine, setOnlyMine] = useState(false);

  const kinds = useMemo(() => Array.from(new Set(events.map((e) => e.kind))).sort(), [events]);
  const sources = useMemo(() => Array.from(new Set(events.map((e) => e.source))).sort(), [events]);

  const filtered = events.filter(
    (e) => (!kind || e.kind === kind) && (!source || e.source === source) && (!onlyMine || e.needsMyAction),
  );

  const selectClass = 'rounded-lg border border-line bg-white px-3 py-2 text-sm text-navy focus:border-orange-500 focus:outline-none';

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs text-ink-2">
          نوع الحدث
          <select aria-label="نوع الحدث" className={selectClass} value={kind} onChange={(e) => setKind(e.target.value)}>
            <option value="">الكلّ</option>
            {kinds.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-ink-2">
          المصدر
          <select aria-label="المصدر" className={selectClass} value={source} onChange={(e) => setSource(e.target.value)}>
            <option value="">الكلّ</option>
            {sources.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 pb-2 text-sm text-ink">
          <input
            type="checkbox"
            aria-label="يحتاج إجراءً منّي"
            checked={onlyMine}
            onChange={(e) => setOnlyMine(e.target.checked)}
          />
          يحتاج إجراءً منّي
        </label>
      </div>

      {filtered.length === 0 ? (
        <EmptyState title="لا أحداث مطابقة" description="غيّر المرشّحات لعرض أحداث أخرى." />
      ) : (
        <ol className="space-y-2">
          {filtered.map((e) => (
            <li key={`${e.source}-${e.sourceId}-${e.atUtc}`} className="rounded-lg border border-line p-3 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="font-medium text-ink">{e.label}</span>
                <span className="flex items-center gap-2">
                  {e.needsMyAction && <Badge tone="alert">يحتاج إجراءً</Badge>}
                  <Badge tone="muted">{e.source}</Badge>
                </span>
              </div>
              <p className="mt-1 text-xs text-ink-2">{formatDateTime(e.atUtc)}</p>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function SectionBody({ section, onRetry }: { section: Employee360Section; onRetry: () => void }) {
  if (section.status === 'Error') {
    return (
      <QueryError
        onRetry={onRetry}
        title="تعذّر تحميل هذا القسم"
        description={section.reason ?? 'حدث خطأ أثناء بناء هذا القسم. بقيّة الأقسام لم تتأثّر.'}
      />
    );
  }

  if (section.status === 'NoData') {
    return <EmptyState title="لا توجد بيانات" description={section.reason ?? undefined} />;
  }

  const items = (section.items ?? []) as Record<string, unknown>[];

  if (section.key === 'identity') {
    return section.summary ? <IdentityBody identity={section.summary as Employee360Identity} /> : null;
  }

  if (section.key === 'operationalSummary') {
    return section.summary ? (
      <OperationalSummaryBody summary={section.summary as Employee360OperationalSummary} />
    ) : null;
  }

  if (section.key === 'timeline') {
    return <TimelineBody events={items as unknown as Employee360TimelineEvent[]} />;
  }

  const balances =
    section.key === 'requestsAndBalances'
      ? ((section.summary as { balances?: Employee360Balance[] } | null)?.balances ?? [])
      : [];

  return (
    <>
      {balances.length > 0 && <BalancesBody balances={balances} />}
      {items.length > 0 ? (
        <SectionTable sectionKey={section.key} items={items} />
      ) : (
        balances.length === 0 && <EmptyState title="لا توجد عناصر" description={section.reason ?? undefined} />
      )}
    </>
  );
}

function SectionCard({ section, onRetry }: { section: Employee360Section; onRetry: () => void }) {
  const headingId = `emp360-${section.key}`;
  return (
    <section id={headingId} aria-labelledby={`${headingId}-title`} tabIndex={-1} className="scroll-mt-24">
      <Card>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h3 id={`${headingId}-title`} className="font-semibold text-navy">
            {section.titleAr}
          </h3>
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={STATUS_TONE[section.status] ?? 'muted'}>{STATUS_LABEL[section.status] ?? section.status}</Badge>
            <Badge tone={section.dataQuality === 'Complete' ? 'success' : 'muted'}>
              {QUALITY_LABEL[section.dataQuality] ?? section.dataQuality}
            </Badge>
            {section.lastUpdatedAtUtc && (
              <span className="text-xs text-ink-2">آخر تحديث: {formatDateTime(section.lastUpdatedAtUtc)}</span>
            )}
          </div>
        </div>
        <SectionBody section={section} onRetry={onRetry} />
      </Card>
    </section>
  );
}

/**
 * `subject` هو معرّف الموظّف أو السلسلة `me`. في وضع الذات يُحَلّ المعرّف **خادميًّا**
 * عبر `/employees/me/profile-360` ولا يُشتقّ من المتصفّح.
 */
export function Employee360Panel({ subject }: { subject: string }) {
  const [periodKey, setPeriodKey] = useState('');
  const [draftPeriod, setDraftPeriod] = useState('');

  // P123-R1 — توفّر الميزة يُقرأ من عقد المستخدم **قبل** إرسال الطلب. لا مفرّ من ذلك:
  // بعد الإرسال يعود 404 واحد لحالتين مختلفتين جذريًّا (الميزة مغلقة / الموظّف خارج نطاقك)،
  // فلا يمكن تمييزهما من رمز الحالة، وكانت النتيجة رسالة «خطأ مؤقّت، أعد المحاولة» تُعرَض
  // على إغلاق **دائم** — وهو ما تمنعه DEC-05 نصًّا.
  const featureEnabled = useFeatureEnabled(FEATURES.employee360);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['employee-360', subject, periodKey],
    queryFn: async () =>
      (
        await api.get<Employee360Dto>(`/employees/${subject}/profile-360`, {
          params: periodKey ? { period: periodKey } : undefined,
        })
      ).data,
    retry: false,
    enabled: featureEnabled,
  });

  const state = classifySurfaceState({ featureEnabled, isLoading, error, isEmpty: !data });

  if (state === 'FeatureDisabled') {
    return <FeatureDisabledState description="الملفّ الشامل للموظّف غير مفعّل في النظام حاليًّا. ليس هذا خطأً، ولا يلزمك فعل شيء." />;
  }

  if (state === 'Loading') {
    return (
      <div className="space-y-4" role="status" aria-label="جارٍ تحميل ملفّ الموظّف الشامل">
        <CardsSkeleton count={4} />
        <TableSkeleton rows={5} cols={5} />
      </div>
    );
  }

  if (state === 'Forbidden') {
    return (
      <ForbiddenState
        title="لا يمكن عرض الملفّ الشامل لهذا الموظّف"
        description="هذا الموظّف خارج نطاق صلاحيّتك، أو الملفّ غير موجود. راجع مديرك المباشر إن كنت تحتاج الاطّلاع عليه."
      />
    );
  }

  if (state === 'Failed' || !data) {
    return (
      <QueryError
        onRetry={() => refetch()}
        title="تعذّر تحميل الملفّ الشامل"
        description="حدث خطأ مؤقّت أثناء جلب الملفّ. أعد المحاولة."
      />
    );
  }

  // الترتيب الثابت أوّلًا، ثمّ أيّ مفتاح جديد يضيفه الخادم لاحقًا — بلا افتراض وجود أيّ قسم.
  const keys = [
    ...EMPLOYEE_360_SECTION_ORDER.filter((k) => k in data.sections),
    ...Object.keys(data.sections).filter((k) => !EMPLOYEE_360_SECTION_ORDER.includes(k as never)),
  ];

  return (
    <div className="space-y-4">
      <Card>
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="font-semibold text-navy">الملفّ الشامل (360)</h2>
            <p className="mt-1 text-xs text-ink-2">
              {RELATION_LABEL[data.viewerRelation] ?? data.viewerRelation}
              {data.periodKey ? ` · الفترة: ${data.periodKey}` : ''}
            </p>
          </div>
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1 text-xs text-ink-2">
              مفتاح الأسبوع (اتركه فارغًا لآخر أسبوع مكتمل)
              <input
                aria-label="مفتاح الفترة"
                placeholder="2026-W34"
                className="rounded-lg border border-line bg-white px-3 py-2 text-sm text-navy focus:border-orange-500 focus:outline-none"
                value={draftPeriod}
                onChange={(e) => setDraftPeriod(e.target.value)}
              />
            </label>
            <button
              type="button"
              className="rounded-lg bg-navy px-4 py-2 text-sm font-semibold text-white hover:bg-navy-700"
              onClick={() => setPeriodKey(draftPeriod.trim())}
            >
              تطبيق الفترة
            </button>
          </div>
        </div>

        <nav aria-label="أقسام الملفّ الشامل" className="mt-4 flex flex-wrap gap-2">
          {keys.map((k) => (
            <a
              key={k}
              href={`#emp360-${k}`}
              className="rounded-full border border-line px-3 py-1 text-xs text-navy hover:border-orange-500 focus:border-orange-500 focus:outline-none"
            >
              {data.sections[k].titleAr}
            </a>
          ))}
        </nav>

        {/*
          مرساة القائمة خارج شريط الأقسام عمدًا: عقد ذلك الشريط «لا يُرسَم إلّا ما وصل من
          الخادم»، والقائمة لوحة محلّيّة لا قسمًا في `sections` — فإقحامها هناك يكذّب العقد.
        */}
        <a
          href="#emp360-checklist"
          className="mt-3 inline-block rounded-full border border-line px-3 py-1 text-xs text-navy hover:border-orange-500 focus:border-orange-500 focus:outline-none"
        >
          الانتقال إلى قائمة الالتزام
        </a>
      </Card>

      {keys.map((k) => (
        <SectionCard key={k} section={data.sections[k]} onRetry={() => refetch()} />
      ))}

      {/*
        قائمة الالتزام (P2-HR-010) لوحة مستقلّة داخل الملفّ الشامل لا قسمًا ثاني عشر:
        عقد `sections` الأحد عشر يبقى كما هو، ولها نداؤها ودورة تحميل/خطأ خاصّة بها
        كي لا يُسقِط عطلُها بقيّةَ الملفّ ولا العكس.
      */}
      <EmployeeChecklistPanel subject={subject} />
    </div>
  );
}

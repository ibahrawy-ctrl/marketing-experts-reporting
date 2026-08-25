// P2-HR-009 — عقود لوحة عمليّات الموارد البشريّة كما يُصدِرها الخادم حرفيًّا.
// لا تُشتقّ هنا أعداد ولا حالات: الواجهة تعرض ما حسبه الخادم داخل نطاق المُشاهِد فقط.

export type HrOperationsQueueKey =
  | 'reports-missing'
  | 'reports-late'
  | 'kpi-missing'
  | 'kpi-awaiting-approval'
  | 'kpi-coverage-gap'
  | 'attendance-awaiting-employee'
  | 'attendance-employee-sla-breached'
  | 'attendance-awaiting-hr'
  | 'attendance-hr-sla-breached'
  | 'requests-awaiting-action'
  | 'follow-up-items';

/** صفّ موحَّد لكلّ الطوابير — عنوانٌ ومسار، بلا نصّ حرّ حسّاس. */
export interface HrOperationsRow {
  queue: number;
  entityId: string;
  entityType: string;
  subjectUserId: string;
  subjectFullName: string;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  titleAr: string;
  typeAr: string;
  statusAr: string;
  periodKey: string | null;
  dueAt: string | null;
  slaDueAtUtc: string | null;
  slaBreached: boolean;
  ageingDays: number;
  ownerUserId: string | null;
  ownerFullName: string | null;
  nextActionAr: string;
  lastActionAtUtc: string | null;
}

/**
 * بطاقة طابور. `count` ليس رقمًا مستقلًّا بل عدد صفوف الطابور نفسه تحت المرشِّح نفسه
 * ⇒ فتح التفصيل لا يمكن أن يُظهر عددًا مخالفًا.
 */
export interface HrOperationsCard {
  queue: number;
  key: HrOperationsQueueKey;
  titleAr: string;
  groupAr: string;
  count: number;
  breachedCount: number;
  maxAgeingDays: number;
  severityAr: string;
}

export interface HrOperationsScope {
  scopeType: string;
  userCount: number;
}

export interface HrOperationsDashboard {
  periodKeys: string[];
  scope: HrOperationsScope;
  cards: HrOperationsCard[];
}

export interface HrOperationsQueuePage {
  queue: number;
  key: HrOperationsQueueKey;
  titleAr: string;
  totalCount: number;
  breachedCount: number;
  page: number;
  pageSize: number;
  rows: HrOperationsRow[];
}

/** مرشِّح يضيّق فقط — لا قيمة فيه توسّع نطاق المُشاهِد. */
export interface HrOperationsFilter {
  recentCycles?: number;
  fromCycleKey?: string;
  toCycleKey?: string;
  departmentId?: string;
  teamId?: string;
  userId?: string;
  type?: string;
  status?: string;
  overdueOnly?: boolean;
}

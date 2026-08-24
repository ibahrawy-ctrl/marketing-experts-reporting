// P2-EMP-002/003 — عقد الواجهة مع سطح Employee 360.
// القسم غير المصرَّح به **لا يصل أصلًا** في `sections`، فلا تُبنى الواجهة على إخفاء بصريّ.

export type Employee360SectionStatus = 'Ready' | 'NoData' | 'Partial' | 'Error';
export type Employee360DataQuality = 'Complete' | 'Partial' | 'Unavailable';

export interface Employee360Section {
  key: string;
  titleAr: string;
  status: Employee360SectionStatus;
  dataQuality: Employee360DataQuality;
  lastUpdatedAtUtc: string | null;
  summary: unknown | null;
  items: unknown[] | null;
  reason?: string | null;
}

export interface Employee360Dto {
  subjectUserId: string;
  isSelf: boolean;
  viewerRelation: string;
  periodKey: string | null;
  sections: Record<string, Employee360Section>;
}

export interface Employee360Identity {
  userId: string;
  fullName: string;
  email: string | null;
  jobRoleName: string | null;
  teamName: string | null;
  departmentName: string | null;
  directManagerName: string | null;
  isActive: boolean;
  joinedAtUtc: string;
}

export interface Employee360OperationalSummary {
  reportsSubmitted: number;
  reportsReturned: number;
  reportsNeedsAction: number;
  kpiEvaluationCount: number;
  lastKpiScore: number | null;
  lastKpiPeriodKey: string | null;
  openLeaveRequests: number;
  openServiceRequests: number;
  openNotesRequiringAction: number;
  openGovernanceItems: number;
}

export interface Employee360TimelineEvent {
  kind: string;
  source: string;
  sourceId: string;
  label: string;
  atUtc: string;
  needsMyAction: boolean;
}

export interface Employee360Balance {
  balanceType: string;
  year: number;
  credited: number;
  debited: number;
  net: number;
}

/** ترتيب العرض الثابت للأقسام الأحد عشر — ما لم يصل من الخادم لا يُعرض إطلاقًا. */
export const EMPLOYEE_360_SECTION_ORDER = [
  'identity',
  'operationalSummary',
  'reports',
  'kpi',
  'leaveAndPermissions',
  'requestsAndBalances',
  'attendanceAndCompliance',
  'notes',
  'governance',
  'developmentAndTraining',
  'timeline',
] as const;

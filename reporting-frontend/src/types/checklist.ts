// P2-HR-010 — عقد قائمة خدمة الموظّف والالتزام.
//
// مبدأ حاكم على هذا الملفّ كلّه: **البنود تصل من الخادم أو لا تكون**. لا كتالوج ثابت هنا
// ولا اشتقاق في المتصفّح، لأنّ ظهور مفتاح بند هو بذاته معلومة (وجود عقد، وجود إخلاء طرف)؛
// والبند المحجوب لا يصل أصلًا فلا يوجد ما يُخفى بصريًّا.

export type ChecklistItemStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'NotApplicable';

/** مصدر البند — محسوب من مصدره في كلّ نداء، أو يدويّ مخزَّن. لا حالة ثالثة. */
export type ChecklistItemSource = 'Computed' | 'Manual';

export interface ChecklistItem {
  key: string;
  titleAr: string;
  groupAr: string;
  source: ChecklistItemSource;
  status: ChecklistItemStatus;
  statusLabelAr: string;
  /** عدد البنود المفتوحة في المصدر. `0` مع `NotApplicable` لا يعني إنجازًا. */
  openCount: number;
  ownerUserId: string | null;
  ownerFullName: string | null;
  dueDate: string | null;
  lastActionAtUtc: string | null;
  evidenceReference: string | null;
  sourceKind: string | null;
  sourceLink: string | null;
  /** هل يقع الإجراء على المستخدم الحاليّ؟ يُحسَم خادميًّا لا في المتصفّح. */
  requiresMyAction: boolean;
}

export interface ChecklistSummary {
  applicable: number;
  completed: number;
  open: number;
  notApplicable: number;
  requiresMyAction: number;
  completionRatio: number;
}

export interface EmployeeChecklist {
  subjectUserId: string;
  isSelf: boolean;
  viewerRelation: string;
  summary: ChecklistSummary;
  items: ChecklistItem[];
}

export interface UpdateChecklistItemPayload {
  status: ChecklistItemStatus;
  dueDate?: string | null;
  ownerUserId?: string | null;
  evidenceReference?: string | null;
  note?: string | null;
  concurrencyStamp?: string | null;
}

export const CHECKLIST_STATUS_LABEL: Record<ChecklistItemStatus, string> = {
  NotStarted: 'لم يبدأ',
  InProgress: 'قيد التنفيذ',
  Completed: 'مكتمل',
  NotApplicable: 'غير منطبق',
};

/** الحالات القابلة للاختيار في التحرير اليدويّ — مطابِقة لما يقبله الخادم. */
export const CHECKLIST_MANUAL_STATUSES: ChecklistItemStatus[] = [
  'NotStarted',
  'InProgress',
  'Completed',
  'NotApplicable',
];

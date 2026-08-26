// P2-ATT-007 — عقد الواجهة مع سطح وقائع الحضور.
//
// مبدأ حاكم: **الأزرار تُبنى من `allowedActions` القادمة من الخادم**، لا من الدور المحلّيّ.
// إخفاء الزرّ ليس تخويلًا؛ الخادم يعيد التحقّق عند كلّ كتابة ويردّ 404 خارج النطاق.
// الحقل الذي حجبه الخادم **لا يصل أصلًا**، فكلّ حقل حسّاس هنا اختياريّ بحكم العقد لا بحكم الاحتياط.

export type AttendanceStatus =
  | 'Draft'
  | 'Reported'
  | 'AwaitingEmployee'
  | 'Acknowledged'
  | 'Disputed'
  | 'EmployeeResponseTimedOut'
  | 'AwaitingHr'
  | 'Corrected'
  | 'Confirmed'
  | 'Rejected'
  | 'Reconciled'
  | 'Withdrawn'
  | 'Escalated'
  | 'Closed'
  | 'Cancelled'
  | 'Voided';

/** أفعال الانتقال كما يسمّيها الخادم في `allowedActions` — لا تُشتقّ محلّيًّا. */
export type AttendanceAction =
  | 'Submit'
  | 'Cancel'
  | 'Withdraw'
  | 'Acknowledge'
  | 'Dispute'
  | 'HrConfirm'
  | 'HrReject'
  | 'HrCorrect'
  | 'HrReconcile'
  | 'ReturnToEmployee'
  | 'Escalate'
  | 'Close'
  | 'Void';

export interface AttendanceType {
  id: string;
  code: string;
  nameAr: string;
  requiresTimes: boolean;
  requiresPolicyReference: boolean;
  allowsMultiplePerDay: boolean;
  order: number;
}

export interface AttendanceListItem {
  id: string;
  subjectUserId: string;
  subjectName: string;
  incidentTypeId: string;
  typeCode: string;
  typeNameAr: string;
  incidentDate: string;
  status: AttendanceStatus;
  statusAr: string;
  /** الفارق بين بلاغ وواقعة مؤكَّدة — يحسمه الخادم ولا يُستنتج من اسم الحالة. */
  isOfficialIncident: boolean;
  durationMinutes: number | null;
  ageingDays: number;
  slaDueAtUtc: string | null;
  isOverdue: boolean;
  lastActionAtUtc: string | null;
  nextActorAr: string | null;
}

export interface AttendanceEvent {
  id: string;
  actorUserId: string;
  actorName: string;
  action: string;
  fromStatus: string;
  toStatus: string;
  comment: string | null;
  createdAtUtc: string;
}

export interface AttendanceAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  createdAtUtc: string;
}

export interface AttendanceReconciliationSuggestion {
  leaveRequestId: string;
  type: string;
  typeAr: string;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  evidenceLink: string;
}

export interface AttendanceIncidentDetail {
  id: string;
  subjectUserId: string;
  subjectName: string;
  incidentTypeId: string;
  typeCode: string;
  typeNameAr: string;
  incidentDate: string;
  startTime: string | null;
  returnTime: string | null;
  durationMinutes: number | null;
  description: string;
  detectionSource: string;
  reportedByUserId: string;
  reportedByName: string;
  status: AttendanceStatus;
  statusAr: string;
  isOfficialIncident: boolean;
  concurrencyStamp: number;
  slaDueAtUtc: string | null;
  isOverdue: boolean;
  ageingDays: number;
  nextActorAr: string | null;
  /** يصل فقط لمن صرّح له الخادم بمشاركته؛ غيابه هو الحماية لا قيمته الفارغة. */
  employeeResponse?: string;
  respondedAtUtc: string | null;
  hrDecision: string | null;
  /** ملاحظة داخليّة — تغيب من الـJSON بلا إذن `Sensitivity.HrOnly.Read`. */
  hrNote?: string;
  reviewedByUserId: string | null;
  reviewedAtUtc: string | null;
  reconciledWithLeaveId: string | null;
  reconciledWithPermissionId: string | null;
  duplicateOfId: string | null;
  closedAtUtc: string | null;
  createdAtUtc: string;
  attachments: AttendanceAttachment[];
  events: AttendanceEvent[];
  /** عقد القدرات: ما يملك هذا المُشاهِد تشغيله الآن. مصدر الأزرار الوحيد. */
  allowedActions: AttendanceAction[];
  reconciliationSuggestions?: AttendanceReconciliationSuggestion[];
}

export interface AttendancePaged {
  items: AttendanceListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AttendanceListFilter {
  subjectUserId?: string;
  teamId?: string;
  departmentId?: string;
  incidentTypeId?: string;
  status?: AttendanceStatus;
  fromDate?: string;
  toDate?: string;
  overdueOnly?: boolean;
  needsMyAction?: boolean;
  page?: number;
  pageSize?: number;
}

/** نصّ الزرّ لكلّ فعل — تسمية عرض فقط، والقرار للخادم. */
export const ATTENDANCE_ACTION_LABEL: Record<AttendanceAction, string> = {
  Submit: 'إرسال البلاغ',
  Cancel: 'إلغاء المسودّة',
  Withdraw: 'سحب البلاغ',
  Acknowledge: 'إقرار بالواقعة',
  Dispute: 'اعتراض',
  HrConfirm: 'تأكيد الواقعة',
  HrReject: 'رفض البلاغ',
  HrCorrect: 'تصحيح البيانات',
  HrReconcile: 'مصالحة مع إجازة',
  ReturnToEmployee: 'إعادة إلى الموظّف',
  Escalate: 'تصعيد',
  Close: 'إغلاق',
  Void: 'إبطال',
};

/** أفعال تستلزم سببًا مكتوبًا — الرفض بلا رواية ليس قرارًا موثَّقًا. */
export const ATTENDANCE_ACTIONS_REQUIRING_REASON: AttendanceAction[] = [
  'Withdraw',
  'Dispute',
  'Escalate',
  'Close',
];

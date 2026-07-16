// أنواع DTO المطابقة لطبقة الـAPI (التعدادات سلاسل نصية).

export type Role =
  | 'Admin'
  | 'CEO'
  | 'GeneralManager'
  | 'Manager'
  | 'TeamLeader'
  | 'Employee'
  | 'CeoSupport'
  | 'HR'
  | 'Viewer'
  | 'FinanceManager'
  | 'Accountant'
  | 'AccountPortfolioReader';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresUtc: string;
  userId: string;
  fullName: string;
  email: string;
  roles: Role[];
  // الدورية المتوقَّعة لتقارير المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم).
  expectedReportCadence: PeriodType;
  // رمز المسمّى الوظيفي (مثل SALES_B2C) — لتحديد لوحات المبيعات وعناصر التنقّل (null إن لم يُسنَد).
  jobRoleCode?: string | null;
}

export type PeriodType = 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'Yearly' | 'AdHoc';

export interface MeResponse {
  userId: string;
  fullName: string;
  email: string;
  isActive: boolean;
  roles: Role[];
  // الدورية المتوقَّعة لتقارير المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم).
  expectedReportCadence: PeriodType;
  // رمز المسمّى الوظيفي (مثل SALES_B2C) — لتحديد لوحات المبيعات وعناصر التنقّل (null إن لم يُسنَد).
  jobRoleCode?: string | null;
}

/// سياق المبيعات الموثوق (RC-3 Task 1.1) — يُحسَب خادميًّا لتحديد الأقسام المعروضة ونوع المندوب.
export interface SalesContextDto {
  viewLevel: string;
  showB2c: boolean;
  showB2b: boolean;
  isSalesRep: boolean;
  repType: string | null;
}

export type SubmissionStatus =
  | 'Draft'
  | 'Submitted'
  | 'Returned'
  | 'ApprovedByDirectManager'
  | 'ApprovedByNextLevel'
  | 'Escalated'
  | 'Closed'
  | 'Visible';

export type KpiTrend = 'Unknown' | 'Up' | 'Flat' | 'Down';
export type RiskSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type RiskStatus = 'Open' | 'Mitigating' | 'Monitoring' | 'Closed';
export type EscalationStatus = 'Open' | 'Acknowledged' | 'Resolved' | 'Dismissed';
export type DecisionStatus = 'Proposed' | 'Approved' | 'Rejected' | 'Implemented';
export type TrainingNeedStatus = 'Open' | 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
export type ImprovementPlanStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export interface NotificationDto {
  id: string;
  type: string;
  title: string;
  body: string | null;
  link: string | null;
  isRead: boolean;
  createdAtUtc: string;
}

// ===== Reports =====
export interface SubmissionStatusCount {
  status: SubmissionStatus;
  count: number;
}
export interface DepartmentCompletenessRow {
  departmentId: string | null;
  departmentName: string;
  total: number;
  closed: number;
  pending: number;
  completionRate: number;
}
export interface SubmissionCompletenessReport {
  periodKey: string | null;
  total: number;
  closed: number;
  pending: number;
  completionRate: number;
  byStatus: SubmissionStatusCount[];
  byDepartment: DepartmentCompletenessRow[];
}
export interface KpiSummaryRow {
  subjectUserId: string;
  subjectName: string;
  totalScore: number | null;
  trend: KpiTrend;
  isBelowTarget: boolean;
  periodKey: string;
}
export interface KpiSummaryReport {
  periodKey: string | null;
  evaluated: number;
  averageScore: number | null;
  belowTarget: number;
  rows: KpiSummaryRow[];
}
export interface SeverityCount {
  severity: RiskSeverity;
  count: number;
}
export interface GovernanceSummaryReport {
  openRisks: number;
  risksBySeverity: SeverityCount[];
  openEscalations: number;
  openTrainingNeeds: number;
  openImprovementPlans: number;
  openDecisions: number;
}

// ===== تجميع مبيعات B2C (Business-1A) =====
export interface B2cRollupRow {
  submitterId: string;
  name: string;
  leads: number;
  calls: number;
  followUps: number;
  registrations: number;
  closedDeals: number;
  targetRegistrations: number;
  conversionRate: number;
  targetAchievement: number;
  needsFollowUp: boolean;
}
export interface B2cRollupReport {
  periodKey: string | null;
  reporters: number;
  totalLeads: number;
  totalCalls: number;
  totalFollowUps: number;
  totalRegistrations: number;
  totalClosedDeals: number;
  totalTarget: number;
  overallConversionRate: number;
  overallTargetAchievement: number;
  best: B2cRollupRow | null;
  worst: B2cRollupRow | null;
  rows: B2cRollupRow[];
  commonLostReasons: string[];
  // مستوى الرؤية المسموح حسب الدور (self/team/department/summary) — يحدده الخادم.
  viewLevel?: string;
  // هل يُرجع الخادم صفوف المندوبين التفصيلية لهذا الدور؟
  canViewRows?: boolean;
}

// ===== تجميع أداء الإعلانات Media Buyer (Business-1B) =====
export interface MediaBuyerRollupRow {
  submitterId: string;
  name: string;
  spend: number;
  leads: number;
  cpl: number;
  ctr: number;
  conversionRate: number;
  needsIntervention: boolean;
}
export interface MediaBuyerRollupReport {
  periodKey: string | null;
  reporters: number;
  totalSpend: number;
  totalLeads: number;
  overallCpl: number;
  averageCtr: number;
  averageConversionRate: number;
  best: MediaBuyerRollupRow | null;
  worst: MediaBuyerRollupRow | null;
  rows: MediaBuyerRollupRow[];
  commonIssueCauses: string[];
  decisionsNeeded: string[];
  // مستوى الرؤية المسموح حسب الدور (self/team/department/summary) — يحدده الخادم.
  viewLevel?: string;
  // هل يُرجع الخادم صفوف المشترين التفصيلية لهذا الدور؟
  canViewRows?: boolean;
}

// ===== تجميع أداء SEO (Business-1C) =====
export interface SeoRollupRow {
  submitterId: string;
  name: string;
  improvedKeywords: number;
  declinedKeywords: number;
  netKeywords: number;
  tasksDone: number;
  technicalIssues: number;
  indexedPages: number;
  organicTraffic: number;
  articlesPlanned: number;
  articlesPublished: number;
  articlesLate: number;
  contentDeliveryRate: number;
  needsFollowup: boolean;
}
export interface SeoRollupReport {
  periodKey: string | null;
  reporters: number;
  totalImprovedKeywords: number;
  totalDeclinedKeywords: number;
  netKeywordMovement: number;
  totalTasksDone: number;
  totalTechnicalIssues: number;
  totalIndexedPages: number;
  totalOrganicTraffic: number;
  totalArticlesPlanned: number;
  totalArticlesPublished: number;
  totalArticlesLate: number;
  contentDeliveryRate: number;
  best: SeoRollupRow | null;
  worst: SeoRollupRow | null;
  rows: SeoRollupRow[];
  decisionsNeeded: string[];
  recommendations: string[];
  // مستوى الرؤية المسموح حسب الدور (self/team/department/summary) — يحدده الخادم.
  viewLevel?: string;
  // هل يُرجع الخادم صفوف الأعضاء التفصيلية لهذا الدور؟
  canViewRows?: boolean;
}

export interface ContentWriterRollupRow {
  submitterId: string;
  name: string;
  requiredPieces: number;
  deliveredPieces: number;
  approvedFirstTime: number;
  latePieces: number;
  revisedPieces: number;
  firstApprovalRate: number;
  revisionRate: number;
  planAdherence: number;
  needsFollowup: boolean;
}
export interface ContentWriterRollupReport {
  periodKey: string | null;
  reporters: number;
  totalRequired: number;
  totalDelivered: number;
  totalApprovedFirstTime: number;
  totalLate: number;
  totalRevised: number;
  contentDeliveryRate: number;
  firstApprovalRate: number;
  revisionRate: number;
  avgPlanAdherence: number;
  best: ContentWriterRollupRow | null;
  worst: ContentWriterRollupRow | null;
  rows: ContentWriterRollupRow[];
  delayReasons: string[];
  decisionsNeeded: string[];
  // مستوى الرؤية المسموح حسب الدور (self/team/department/summary) — يحدده الخادم.
  viewLevel?: string;
  // هل يُرجع الخادم صفوف الأعضاء التفصيلية لهذا الدور؟
  canViewRows?: boolean;
}

export interface DesignerRollupRow {
  submitterId: string;
  name: string;
  requestedDesigns: number;
  deliveredDesigns: number;
  approvedFirstTime: number;
  lateDesigns: number;
  pendingReview: number;
  revisedDesigns: number;
  firstApprovalRate: number;
  revisionRate: number;
  onTimeRate: number;
  planAdherence: number;
  needsFollowup: boolean;
}
export interface DesignerRollupReport {
  periodKey: string | null;
  reporters: number;
  totalRequested: number;
  totalDelivered: number;
  totalApprovedFirstTime: number;
  totalLate: number;
  totalPendingReview: number;
  totalRevised: number;
  deliveryRate: number;
  firstApprovalRate: number;
  revisionRate: number;
  onTimeRate: number;
  avgPlanAdherence: number;
  best: DesignerRollupRow | null;
  worst: DesignerRollupRow | null;
  rows: DesignerRollupRow[];
  delayReasons: string[];
  decisionsNeeded: string[];
  viewLevel?: string;
  canViewRows?: boolean;
}

export interface VideoRollupRow {
  submitterId: string;
  name: string;
  requestedVideos: number;
  deliveredVideos: number;
  approvedFirstTime: number;
  lateVideos: number;
  pendingReview: number;
  revisedVideos: number;
  firstApprovalRate: number;
  revisionRate: number;
  onTimeRate: number;
  planAdherence: number;
  needsFollowup: boolean;
}
export interface VideoRollupReport {
  periodKey: string | null;
  reporters: number;
  totalRequested: number;
  totalDelivered: number;
  totalApprovedFirstTime: number;
  totalLate: number;
  totalPendingReview: number;
  totalRevised: number;
  deliveryRate: number;
  firstApprovalRate: number;
  revisionRate: number;
  onTimeRate: number;
  avgPlanAdherence: number;
  best: VideoRollupRow | null;
  worst: VideoRollupRow | null;
  rows: VideoRollupRow[];
  delayReasons: string[];
  decisionsNeeded: string[];
  viewLevel?: string;
  canViewRows?: boolean;
}

export interface ModerationRollupRow {
  submitterId: string;
  name: string;
  incomingMessages: number;
  answeredMessages: number;
  unhandledMessages: number;
  avgResponseMinutes: number;
  problematicComments: number;
  escalations: number;
  complaints: number;
  convertedOpportunities: number;
  responseRate: number;
  needsFollowup: boolean;
}
export interface ModerationRollupReport {
  periodKey: string | null;
  reporters: number;
  totalIncoming: number;
  totalAnswered: number;
  totalUnhandled: number;
  totalProblematic: number;
  totalEscalations: number;
  totalComplaints: number;
  totalConverted: number;
  responseRate: number;
  avgResponseMinutes: number;
  best: ModerationRollupRow | null;
  worst: ModerationRollupRow | null;
  rows: ModerationRollupRow[];
  recurringIssues: string[];
  decisionsNeeded: string[];
  viewLevel?: string;
  canViewRows?: boolean;
}

// ===== Business-1D-5: ملخّص تشغيل السوشيال ميديا الموحّد =====
export interface SocialContentSummary {
  reporters: number;
  required: number;
  delivered: number;
  firstApprovalRate: number;
  needsFollowup: number;
}
export interface SocialDesignSummary {
  reporters: number;
  requested: number;
  delivered: number;
  firstApprovalRate: number;
  late: number;
  needsFollowup: number;
}
export interface SocialVideoSummary {
  reporters: number;
  requested: number;
  delivered: number;
  firstApprovalRate: number;
  late: number;
  needsFollowup: number;
}
export interface SocialModerationSummary {
  reporters: number;
  incoming: number;
  answered: number;
  responseRate: number;
  avgResponseMinutes: number;
  complaints: number;
  escalations: number;
}
export interface SocialOpsRollupReport {
  periodKey: string | null;
  totalReporters: number;
  content: SocialContentSummary;
  design: SocialDesignSummary;
  video: SocialVideoSummary;
  moderation: SocialModerationSummary;
  healthScore: number;
  healthLabel: string;
  topRisk: string;
  mostNeedsFollowupTrack: string;
  mostDelayedTrack: string;
  mostRevisedTrack: string;
  mostComplaintsTrack: string;
  recommendation: string;
  decisionNeeded: string | null;
  viewLevel?: string;
  canViewRows?: boolean;
}

// ===== Submissions =====
export interface SubmissionListItem {
  id: string;
  templateTitle: string;
  submitterId: string;
  submitterName: string;
  teamId: string | null;
  departmentId: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
  currentApproverId: string | null;
}

// ===== Governance =====
export interface RiskDto {
  id: string;
  title: string;
  description: string | null;
  severity: RiskSeverity;
  status: RiskStatus;
  ownerId: string;
  ownerName: string | null;
  departmentId: string | null;
  mitigationPlan: string | null;
  closedAtUtc: string | null;
  createdAtUtc: string;
  relatedSubmissionId: string | null;
  relatedKpiEvaluationId: string | null;
  subjectUserId: string | null;
  teamId: string | null;
  nextAction: string | null;
  clientId: string | null;
  projectId: string | null;
}
export interface EscalationDto {
  id: string;
  raisedById: string;
  raisedByName: string | null;
  targetUserId: string;
  targetName: string | null;
  reason: string;
  status: EscalationStatus;
  reportSubmissionId: string | null;
  riskId: string | null;
  resolvedAtUtc: string | null;
  resolution: string | null;
  createdAtUtc: string;
  kpiEvaluationId: string | null;
  nextAction: string | null;
}
export interface DecisionDto {
  id: string;
  title: string;
  description: string | null;
  madeById: string;
  madeByName: string | null;
  status: DecisionStatus;
  relatedSubmissionId: string | null;
  relatedRiskId: string | null;
  relatedEscalationId: string | null;
  decidedAtUtc: string | null;
  createdAtUtc: string;
  relatedKpiEvaluationId: string | null;
  nextAction: string | null;
}

// ===== Management Notes (طبقة الملاحظات الإدارية الموحّدة) =====
export type ManagementNoteEntityType =
  | 'ReportSubmission'
  | 'User'
  | 'KpiEvaluation'
  | 'Team'
  | 'Escalation'
  | 'Decision'
  | 'Risk';
export type ManagementNoteType = 'Documentation' | 'Guidance' | 'Warning' | 'FollowUp';
export type ManagementNoteStatus = 'Open' | 'Resolved';
export interface ManagementNoteDto {
  id: string;
  entityType: ManagementNoteEntityType;
  entityId: string;
  authorId: string;
  authorName: string | null;
  noteType: ManagementNoteType;
  body: string;
  requiresAction: boolean;
  status: ManagementNoteStatus;
  resolvedById: string | null;
  resolvedByName: string | null;
  resolvedAtUtc: string | null;
  createdAtUtc: string;
}
export interface CreateManagementNoteRequest {
  entityType: ManagementNoteEntityType;
  entityId: string;
  noteType: ManagementNoteType;
  body: string;
  requiresAction: boolean;
}

// ===== Development =====
export interface TrainingNeedDto {
  id: string;
  subjectUserId: string;
  subjectName: string | null;
  raisedById: string;
  raisedByName: string | null;
  title: string;
  description: string | null;
  source: string | null;
  status: TrainingNeedStatus;
  relatedKpiEvaluationId: string | null;
  createdAtUtc: string;
}
export interface ImprovementPlanDto {
  id: string;
  subjectUserId: string;
  subjectName: string | null;
  ownerId: string;
  ownerName: string | null;
  title: string;
  description: string | null;
  status: ImprovementPlanStatus;
  dueDateUtc: string | null;
  relatedTrainingNeedId: string | null;
  createdAtUtc: string;
}

// ===== Directory =====
export interface DirectoryUserDto {
  id: string;
  fullName: string;
  email: string;
  isActive: boolean;
  roles: Role[];
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
  jobRoleId: string | null;
}
// صف «دليل الموارد البشرية» المخصّص (قراءة فقط لحزمة HR A) — منفصل عن DirectoryUserDto العام.
// لا يحمل الأدوار/الصلاحيات؛ يحمل علمَين أمنيين: isSensitive (حساب إداري حسّاس) و canEdit (=ليس حسّاسًا).
export interface HrDirectoryUserDto {
  id: string;
  fullName: string;
  email: string;
  isActive: boolean;
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
  jobRoleId: string | null;
  isSensitive: boolean;
  canEdit: boolean;
}
export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  roles: Role[];
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
}
export interface UpdateUserRequest {
  fullName: string;
  email: string;
  isActive: boolean;
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
}
// تعديل المسمّى الوظيفي للموظف فقط — السطح المخصّص (لا يمسّ أي حقل آخر). jobRoleId=null لإزالة المسمّى.
export interface UpdateUserJobRoleRequest {
  jobRoleId: string | null;
  notes?: string | null;
}
// تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم فقط) — حزمة HR A. لا يمسّ البريد/الأدوار/الصلاحيات/كلمة المرور/التفعيل.
export interface UpdateUserBasicRequest {
  fullName: string;
  notes?: string | null;
}
// تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير المباشر) — حزمة HR A. القيود الأمنية مفروضة خادمًا.
export interface UpdateUserOrgAssignmentRequest {
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
  notes?: string | null;
}
export interface DepartmentDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  managerId: string | null;
  isActive: boolean;
}
export interface TeamDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  departmentId: string;
  teamLeaderId: string | null;
  isActive: boolean;
}
export interface JobRoleDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  departmentId: string | null;
  isActive: boolean;
}
// مسمّى وظيفي مع عدّادات الاستخدام واسم الإدارة — لشاشة إدارة المسمّيات الوظيفية.
export interface JobRoleDetailDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  departmentId: string | null;
  departmentName: string | null;
  isActive: boolean;
  employeeCount: number;
  templateCount: number;
}
export interface CreateJobRoleRequest {
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  departmentId: string | null;
}
export interface UpdateJobRoleRequest {
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  departmentId: string | null;
}
export type CapabilityStatus = 'Active' | 'NotGranted' | 'ProposedLater' | 'SensitiveDecision';
export interface RoleCapability {
  key: string;
  labelAr: string;
  status: CapabilityStatus;
}
export interface RoleCapabilityGroup {
  key: string;
  titleAr: string;
  items: RoleCapability[];
}
export interface RoleAccessDto {
  role: Role;
  roleLabelAr: string;
  scopeType: string;
  scopeDescriptionAr: string;
  permissions: string[];
  permissionLabelsAr: string[];
  capabilityGroups: RoleCapabilityGroup[];
}
export interface CreateTeamRequest {
  nameAr: string;
  nameEn: string | null;
  departmentId: string;
  teamLeaderId: string | null;
}
export interface UpdateTeamRequest {
  nameAr: string;
  nameEn: string | null;
  departmentId: string;
  teamLeaderId: string | null;
  isActive: boolean;
  syncMemberDepartments?: boolean;
}
// ملخّص فريق مع عدّاداته — لشاشة الفرق وتفاصيل الإدارة (قراءة فقط).
export interface TeamSummaryDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  departmentId: string;
  departmentName: string | null;
  teamLeaderId: string | null;
  teamLeaderName: string | null;
  isActive: boolean;
  memberCount: number;
  projectsCount: number;
  activeProjectsCount: number;
  memberDepartmentMismatchCount: number;
  primaryMemberCount: number;
  additionalMemberCount: number;
}
// عضويات الفريق المتعددة (MULTI-TEAM-MEMBERSHIP-MVP-R1) — عضو أساسي (User.TeamId) + أعضاء إضافيون (UserTeamMemberships).
export interface TeamMemberDto {
  userId: string;
  fullName: string;
  email: string;
  isActive: boolean;
  departmentId: string | null;
  jobRoleId: string | null;
  isPrimary: boolean;
  membershipRowId: string | null;
  membershipIsActive: boolean;
  startDateUtc: string | null;
  endDateUtc: string | null;
  notes: string | null;
}
export interface TeamMembershipsDto {
  teamId: string;
  teamNameAr: string;
  departmentId: string;
  primaryMembers: TeamMemberDto[];
  additionalMembers: TeamMemberDto[];
}
export interface UserTeamMembershipDto {
  membershipRowId: string;
  teamId: string;
  teamNameAr: string;
  departmentId: string;
  departmentName: string | null;
  isActive: boolean;
  membershipType: string;
  startDateUtc: string | null;
  endDateUtc: string | null;
  notes: string | null;
}
export interface UserTeamMembershipsDto {
  userId: string;
  fullName: string;
  primaryTeamId: string | null;
  primaryTeamNameAr: string | null;
  additionalMemberships: UserTeamMembershipDto[];
}
export interface AddAdditionalMemberRequest {
  userId: string;
  notes?: string | null;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
}
// ملخّص إدارة مع فرقها وعدّاداتها — لشاشة الإدارات وتفاصيلها (قراءة فقط).
export interface DepartmentSummaryDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  managerId: string | null;
  managerName: string | null;
  hasManager: boolean;
  isActive: boolean;
  teamCount: number;
  memberCount: number;
  projectsCount: number;
  teams: TeamSummaryDto[];
}
// ملخّص أثر نقل فريق إلى إدارة جديدة — يُعرَض قبل الحفظ (قراءة فقط).
export interface TeamMoveImpactDto {
  teamId: string;
  teamName: string;
  currentDepartmentId: string;
  currentDepartmentName: string | null;
  targetDepartmentId: string;
  targetDepartmentName: string | null;
  isDepartmentChange: boolean;
  teamLeaderId: string | null;
  teamLeaderName: string | null;
  memberCount: number;
  projectsCount: number;
  activeProjectsCount: number;
  submissionsCount: number;
  memberDepartmentMismatchCount: number;
  willSyncMembers: boolean;
  warnings: string[];
}
export interface CreateDepartmentRequest {
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  managerId: string | null;
}
export interface UpdateDepartmentRequest {
  nameAr: string;
  nameEn: string | null;
  code: string | null;
  managerId: string | null;
  isActive: boolean;
}

// ===== Field types & template / submission detail =====
export type FieldType =
  | 'ShortText'
  | 'LongText'
  | 'RichText'
  | 'Number'
  | 'Decimal'
  | 'Currency'
  | 'Percentage'
  | 'Date'
  | 'DateTime'
  | 'Time'
  | 'Boolean'
  | 'SingleSelect'
  | 'MultiSelect'
  | 'Rating'
  | 'Scale'
  | 'FileUpload'
  | 'Image'
  | 'Url'
  | 'Email'
  | 'Phone'
  | 'TableGrid'
  | 'SectionHeader'
  | 'ProjectRepeatableSection';

export type TemplateStatus = 'Draft' | 'Published' | 'Archived';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Returned' | 'Escalated' | 'CancelledByAdministrativeDeletion';
// ADMIN-GOVERNANCE-R1: حالات مراجعة KPI الجديدة (UnderReview/NeedsRevision/Rejected). Submitted يبقى للتوافق مع القديم.
export type KpiEvaluationStatus = 'Draft' | 'InProgress' | 'Submitted' | 'Approved' | 'Closed' | 'UnderReview' | 'NeedsRevision' | 'Rejected';
export type KpiCadence = 'WeeklyPulse' | 'Quarterly';
export type KpiCalcMethod = 'Manual' | 'Auto' | 'Hybrid';

// ===== Template detail (versions + fields) =====
export interface TemplateVersionDto {
  id: string;
  versionNumber: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  fields: TemplateFieldDto[];
  // عدد التقارير المرتبطة بهذه النسخة تحديدًا (لتحديد قابلية الحذف الآمن للنسخة).
  submissionCount: number;
  // هل هذه هي النسخة المنشورة الحالية المرتبطة بها التقارير الجديدة؟
  isCurrentPublished: boolean;
  // الحذف مسموح فقط لنسخة غير مستخدَمة وليست الوحيدة ولا الأحدث ولا المنشورة الحالية.
  canDelete: boolean;
  // سبب منع الحذف (يُعرض على الزر المعطّل) حين canDelete=false.
  deleteBlockReason: string | null;
}
// تصنيف القالب من حيث الإلزام: أساسي (إلزامي) أو تكميلي (اختياري — استبيان/متابعة).
export type TemplateClassification = 'Primary' | 'Supplementary';

export interface ReportTemplateDetailDto {
  id: string;
  title: string;
  description: string | null;
  jobRoleId: string | null;
  defaultPeriodType: PeriodType;
  status: TemplateStatus;
  ownerId: string;
  isActive: boolean;
  classification: TemplateClassification;
  versions: TemplateVersionDto[];
  // عدد التقارير المُسلَّمة المرتبطة بأي إصدار (لتحديد قابلية الحذف الآمن).
  submissionCount: number;
  // الحذف النهائي مسموح فقط لقالب مسودة غير مستخدَم؛ غير ذلك أرشفة فقط.
  canHardDelete: boolean;
}

// معاينة القالب «كما يراه الموظّف» — الإصدار الفعّال وحقوله، قراءة فقط بلا إنشاء تسليم.
export interface TemplatePreviewDto {
  templateId: string;
  title: string;
  description: string | null;
  defaultPeriodType: PeriodType;
  classification: TemplateClassification;
  status: TemplateStatus;
  isActive: boolean;
  versionNumber: number | null;
  isPublished: boolean;
  fields: TemplateFieldDto[];
}

// نطاق الإسناد/الاستثناء الصريح للقالب.
export type TemplateAssignmentScope = 'Employee' | 'JobRole' | 'Team' | 'Department';
// نوع الصف: إسناد (Include) أو استثناء (Exclude).
export type TemplateAssignmentKind = 'Include' | 'Exclude';

// مستخدم ضمن تغطية القالب (مرتبط أو مستثنى بسبب).
export interface TemplateAssignmentUserDto {
  userId: string;
  fullName: string;
  email: string | null;
  jobRoleId: string | null;
  jobRoleName: string | null;
  isActive: boolean;
  // أسباب الاستثناء للمستثنين.
  exclusionReason: string | null;
  // سبب الربط للمرتبطين: matchedByUser/matchedByJobRole/matchedByTeam/matchedByDepartment/matchedByGeneral.
  matchReason: string | null;
  // انتماء تنظيمي (قراءة فقط) لأزرار الاستثناء السريع على مستوى الفريق/الإدارة.
  teamId: string | null;
  teamName: string | null;
  departmentId: string | null;
  departmentName: string | null;
}

// صفّ إسناد/استثناء صريح (للعرض والإدارة).
export interface TemplateAssignmentRowDto {
  id: string;
  scopeType: TemplateAssignmentScope;
  scopeId: string;
  scopeName: string | null;
  kind: TemplateAssignmentKind;
  notes: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

// تعارض «أكثر من تقرير أساسي لنفس الدورية» لموظّف.
export interface TemplateAssignmentConflictDto {
  userId: string;
  fullName: string;
  thisTemplateId: string;
  thisTemplateTitle: string;
  otherTemplateId: string;
  otherTemplateTitle: string;
  periodType: PeriodType;
  reason: string;
  suggestedResolution: string;
}

// تغطية القالب: المرتبطون والمستثنون بأسبابهم (نفس أولوية الاختيار بالخادم).
export interface TemplateAssignmentsDto {
  templateId: string;
  title: string;
  jobRoleId: string | null;
  jobRoleName: string | null;
  defaultPeriodType: PeriodType;
  classification: TemplateClassification;
  status: TemplateStatus;
  isActive: boolean;
  isAssignable: boolean;
  isRoleSpecific: boolean;
  matchedUsers: TemplateAssignmentUserDto[];
  excludedUsers: TemplateAssignmentUserDto[];
  assignments: TemplateAssignmentRowDto[];
  conflicts: TemplateAssignmentConflictDto[];
}

// إنشاء إسناد/استثناء صريح.
export interface CreateAssignmentRequest {
  scopeType: TemplateAssignmentScope;
  scopeId: string;
  kind: TemplateAssignmentKind;
  notes?: string | null;
}

// تعطيل/تفعيل + تعديل ملاحظة إسناد قائم.
export interface UpdateAssignmentRequest {
  isActive: boolean;
  notes?: string | null;
}

// ===== KPI template detail (versions + metrics) =====
export interface KpiMetricDto {
  id: string;
  name: string;
  description: string | null;
  order: number;
  weight: number;
  targetValue: number | null;
  unit: string | null;
  calcMethod: KpiCalcMethod;
  calcConfigJson: string | null;
}
export interface KpiTemplateVersionDto {
  id: string;
  versionNumber: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  totalWeight: number;
  metrics: KpiMetricDto[];
}
export interface KpiTemplateDetailDto {
  id: string;
  title: string;
  description: string | null;
  jobRoleId: string | null;
  cadence: KpiCadence;
  status: TemplateStatus;
  ownerId: string;
  isActive: boolean;
  versions: KpiTemplateVersionDto[];
}

// ===== إسناد قوالب KPI (Phase T1) — رؤية/اختيار قالب فقط =====
// مستخدم ضمن تغطية قالب KPI (مرتبط أو مستثنى بسبب) — نفس بنية إسناد التقارير دون التعارضات.
export interface KpiTemplateAssignmentUserDto {
  userId: string;
  fullName: string;
  email: string | null;
  jobRoleId: string | null;
  jobRoleName: string | null;
  isActive: boolean;
  exclusionReason: string | null;
  matchReason: string | null;
  teamId: string | null;
  teamName: string | null;
  departmentId: string | null;
  departmentName: string | null;
}

export interface KpiTemplateAssignmentRowDto {
  id: string;
  scopeType: TemplateAssignmentScope;
  scopeId: string;
  scopeName: string | null;
  kind: TemplateAssignmentKind;
  notes: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

// تغطية قالب KPI: المرتبطون والمستثنون بأسبابهم + الإسنادات الصريحة (بلا تعارضات، يستخدم الدورية).
export interface KpiTemplateAssignmentsDto {
  templateId: string;
  title: string;
  jobRoleId: string | null;
  jobRoleName: string | null;
  cadence: KpiCadence;
  status: TemplateStatus;
  isActive: boolean;
  isAssignable: boolean;
  isRoleSpecific: boolean;
  matchedUsers: KpiTemplateAssignmentUserDto[];
  excludedUsers: KpiTemplateAssignmentUserDto[];
  assignments: KpiTemplateAssignmentRowDto[];
}

export interface CreateKpiAssignmentRequest {
  scopeType: TemplateAssignmentScope;
  scopeId: string;
  kind: TemplateAssignmentKind;
  notes?: string | null;
}

export interface UpdateKpiAssignmentRequest {
  isActive: boolean;
  notes?: string | null;
}

export interface ReportTemplateListItem {
  id: string;
  title: string;
  description: string | null;
  jobRoleId: string | null;
  defaultPeriodType: PeriodType;
  status: TemplateStatus;
  ownerId: string;
  isActive: boolean;
  latestVersionNumber: number;
  fieldCount: number;
  classification: TemplateClassification;
}

export interface TemplateFieldDto {
  id: string;
  label: string;
  key: string | null;
  fieldType: FieldType;
  order: number;
  isRequired: boolean;
  helpText: string | null;
  configJson: string | null;
}

export interface SubmissionFieldValueDto {
  templateFieldId: string;
  label: string;
  fieldType: FieldType;
  valueText: string | null;
  valueNumber: number | null;
  valueDate: string | null;
  valueBool: boolean | null;
  valueJson: string | null;
  isRequired: boolean;
  helpText: string | null;
  configJson: string | null;
}

// إعدادات الحقل المرنة المخزّنة كـ JSON في configJson.
export interface FieldConfig {
  options?: string[];
  columns?: string[];
}

// نوع الحقل الفرعي داخل قسم المشاريع المتكرر (مجموعة فرعية من FieldType + جدول صفوف).
// 'Grid' = جدول صفوف داخل المشروع؛ تُخزَّن صفوفه كنصّ JSON (string[][]) ضمن answers[key]
// فيبقى شكل القيمة Record<string,string> متوافقًا خلفيًّا مع التقارير القديمة (بلا Migration).
export type RepeatableSubFieldType =
  | 'Currency'
  | 'Number'
  | 'Decimal'
  | 'Percentage'
  | 'ShortText'
  | 'LongText'
  | 'Date'
  | 'Boolean'
  | 'Select'
  | 'Grid';

export interface RepeatableSubField {
  key: string;
  label: string;
  type: RepeatableSubFieldType;
  required: boolean;
  // أعمدة الجدول — تُستخدم فقط عندما يكون النوع 'Grid'.
  columns?: string[];
  // خيارات القائمة المنسدلة — تُستخدم فقط عندما يكون النوع 'Select'.
  options?: string[];
}

// إعداد قسم المشاريع المتكرر — يُخزَّن في configJson لحقل ProjectRepeatableSection.
export interface ProjectRepeatableConfig {
  projectRequired: boolean;
  minProjects: number;
  maxProjects: number;
  fields: RepeatableSubField[];
}

// عنصر مشروع واحد في قيمة القسم — يُخزَّن ضمن مصفوفة في valueJson.
export interface ProjectRepeatableEntry {
  projectId: string | null;
  answers: Record<string, string>;
}

export interface ApprovalStepDto {
  level: number;
  approverId: string;
  approverName: string | null;
  status: ApprovalStatus;
  comment: string | null;
  decidedAtUtc: string | null;
}

export interface SubmissionDto {
  id: string;
  reportTemplateVersionId: string;
  templateTitle: string;
  submitterId: string;
  submitterName: string;
  teamId: string | null;
  departmentId: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
  closedAtUtc: string | null;
  currentApproverId: string | null;
  canEdit: boolean;
  fieldValues: SubmissionFieldValueDto[];
  approvalSteps: ApprovalStepDto[];
  clientId: string | null;
  clientName: string | null;
  projectId: string | null;
  projectName: string | null;
}

// ADMIN-GOVERNANCE-R1: جسم طلب الحذف الإداريّ الناعم لتقرير مُسلَّم (السبب إلزاميّ).
export interface AdminDeleteRequest {
  reason?: string;
}

export interface FieldValueInput {
  templateFieldId: string;
  valueText?: string | null;
  valueNumber?: number | null;
  valueDate?: string | null;
  valueBool?: boolean | null;
  valueJson?: string | null;
}

// ===== KPI evaluations =====
export interface KpiResultDto {
  kpiMetricId: string;
  metricName: string;
  weight: number;
  targetValue: number | null;
  unit: string | null;
  calcMethod: KpiCalcMethod;
  rawValue: number | null;
  score: number | null;
  note: string | null;
}

export interface KpiEvaluationDto {
  id: string;
  kpiTemplateVersionId: string;
  templateTitle: string;
  cadence: KpiCadence;
  subjectUserId: string;
  subjectName: string;
  evaluatorId: string | null;
  evaluatorName: string | null;
  teamId: string | null;
  departmentId: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: KpiEvaluationStatus;
  totalScore: number | null;
  trend: KpiTrend;
  isBelowTarget: boolean;
  submittedAtUtc: string | null;
  canEdit: boolean;
  results: KpiResultDto[];
  // ADMIN-GOVERNANCE-R1: مسار المراجعة/الاعتماد
  reviewerId: string | null;
  reviewerName: string | null;
  reviewedAtUtc: string | null;
  reviewNote: string | null;
  canReview: boolean;
  canFlag: boolean;
  canAdminDelete: boolean;
  canReopen: boolean;
}

// ADMIN-GOVERNANCE-R1: جسم إجراء المراجعة (السبب إلزاميّ في الإجراءات التي تتطلّبه).
export interface KpiReviewActionRequest {
  reason?: string;
}

// ADMIN-GOVERNANCE-R1: حدث في سجلّ مراجعة تقييم KPI (Timeline).
export interface KpiEvaluationReviewEventDto {
  id: string;
  action: string;
  actorId: string;
  actorName: string | null;
  fromStatus: string | null;
  toStatus: string | null;
  reason: string | null;
  createdAtUtc: string;
}

export interface KpiTemplateDto {
  id: string;
  title: string;
  description: string | null;
  jobRoleId: string | null;
  cadence: KpiCadence;
  status: TemplateStatus;
  ownerId: string;
  isActive: boolean;
  latestVersionNumber: number;
  metricCount: number;
}

export interface KpiEvaluationListItemDto {
  id: string;
  templateTitle: string;
  subjectUserId: string;
  subjectName: string;
  evaluatorId: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: KpiEvaluationStatus;
  totalScore: number | null;
  trend: KpiTrend;
}

// ===== تصدير KPI للمالية (KPI-FIN1) — قراءة/تصدير فقط على مستوى الشركة =====
export interface KpiFinanceExportFilter {
  year: number;
  quarter: number;
  departmentId?: string;
  teamId?: string;
  status?: KpiEvaluationStatus;
}

export interface KpiFinanceExportRowDto {
  evaluationId: string;
  subjectUserId: string;
  employeeName: string;
  departmentName: string | null;
  teamName: string | null;
  jobRoleName: string | null;
  periodType: PeriodType;
  periodKey: string;
  year: number;
  quarter: number;
  templateTitle: string;
  totalScore: number | null;
  status: KpiEvaluationStatus;
  lastUpdatedAtUtc: string;
}

export interface KpiFinanceExportDto {
  year: number;
  quarter: number;
  periodLabel: string;
  rangeStart: string;
  rangeEnd: string;
  status: KpiEvaluationStatus;
  rowCount: number;
  rows: KpiFinanceExportRowDto[];
}

// الموظّفون الذين يحقّ للمستخدم الحالي إنشاء تقييم KPI لهم (مرؤوسوه المباشرون، أو الكل للأدمن).
export interface EvaluatableSubjectDto {
  id: string;
  fullName: string;
  email: string;
}
export interface EvaluatableSubjectsDto {
  isAdminOverride: boolean;
  subjects: EvaluatableSubjectDto[];
}

// ===== Dashboard (role-driven, server-decided) =====
export type DashboardCardStatus = 'neutral' | 'green' | 'amber' | 'red';

export interface DashboardPeriodDto {
  periodKey: string;
  label: string;
}
export interface DashboardUserDto {
  id: string;
  name: string;
  role: string;
}
export interface DashboardScopeDto {
  type: string;
  ids: string[];
}
export interface SummaryCardDto {
  key: string;
  title: string;
  value: number | null;
  status: DashboardCardStatus;
  drilldownKey: string | null;
}
export interface DashboardWidgetDto {
  key: string;
  type: string;
  title: string;
  data: unknown;
}
export interface DashboardActionDto {
  key: string;
  label: string;
  permission: string;
}
export interface DashboardDto {
  dashboardType: string;
  period: DashboardPeriodDto;
  user: DashboardUserDto;
  scope: DashboardScopeDto;
  permissions: string[];
  summaryCards: SummaryCardDto[];
  widgets: DashboardWidgetDto[];
  actions: DashboardActionDto[];
}

// ===== Dashboard drill-down =====
export interface KpiTrendPointDto {
  periodKey: string;
  score: number;
}
export interface KpiTrendDto {
  subjectId: string;
  subjectName: string;
  points: KpiTrendPointDto[];
}
export interface MemberPerformanceDto {
  userId: string;
  name: string;
  kpiAverage: number | null;
  kpiTrend: KpiTrend;
  reportsTotal: number;
  reportsCompleted: number;
}
export interface ActivityItemDto {
  submissionId: string;
  submitterName: string;
  templateTitle: string;
  status: SubmissionStatus;
  periodKey: string;
  submittedAtUtc: string | null;
}
export interface PendingReportDto {
  submissionId: string;
  submitterId: string;
  submitterName: string;
  templateTitle: string;
  status: SubmissionStatus;
  periodKey: string;
}

// ===== ملف أداء الموظف (Phase 3) =====
export interface EmployeeProfileHeaderDto {
  userId: string;
  fullName: string;
  email: string | null;
  jobRoleName: string | null;
  teamName: string | null;
  departmentName: string | null;
  directManagerName: string | null;
  isActive: boolean;
  statusKey: string;
  statusLabel: string;
}
export interface EmployeeProfileSummaryDto {
  lastKpiScore: number | null;
  lastKpiPeriod: string | null;
  lastKpiTrend: KpiTrend;
  averageKpi: number | null;
  kpiCount: number;
  reportsSubmitted: number;
  reportsReturned: number;
  reportsNeedsAction: number;
  openNotesRequiringAction: number;
}
export interface EmployeeProfileReportDto {
  submissionId: string;
  templateTitle: string;
  periodKey: string;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
  currentApproverName: string | null;
}
export interface EmployeeProfileKpiDto {
  evaluationId: string;
  templateTitle: string;
  periodKey: string;
  totalScore: number | null;
  status: KpiEvaluationStatus;
  trend: KpiTrend;
}
export interface EmployeeProfileGovernanceDto {
  kind: string;
  id: string;
  title: string;
  status: string;
  createdAtUtc: string;
}
export interface EmployeeProfileTimelineDto {
  kind: string;
  label: string;
  atUtc: string;
}
// إجازة/استئذان حديث للموظّف (V1.0.1) — عرض فقط.
export interface EmployeeProfileLeaveDto {
  id: string;
  type: LeaveRequestType;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  status: LeaveRequestStatus;
  finalApproverName: string | null;
  impactsReports: boolean;
  createdAtUtc: string;
}
export interface EmployeeProfileDto {
  header: EmployeeProfileHeaderDto;
  summary: EmployeeProfileSummaryDto;
  reports: EmployeeProfileReportDto[];
  kpiEvaluations: EmployeeProfileKpiDto[];
  governanceItems: EmployeeProfileGovernanceDto[];
  timeline: EmployeeProfileTimelineDto[];
  leaveRequests: EmployeeProfileLeaveDto[];
}

// ===== Phase 5: تقويم التقارير وتجميع KPI الدوري =====
// كل النواتج مقيَّدة خادميًّا بنطاق المستخدم (ScopeResolver) — لا تصفية من الواجهة فقط.

export type KpiGranularity = 'Monthly' | 'Quarterly' | 'Yearly' | 'Custom';

export interface KpiWeeklyPointDto {
  periodKey: string;
  weekStart: string;
  weekEnd: string;
  score: number;
  evaluationsCount: number;
}
export interface KpiAggregateDto {
  granularity: string;
  periodLabel: string;
  rangeStart: string;
  rangeEnd: string;
  average: number | null;
  weeksCount: number;
  evaluationsCount: number;
  scopeType: string;
  canViewRows: boolean;
  weeks: KpiWeeklyPointDto[];
}

export interface ExpectedReporterRow {
  userId: string;
  fullName: string;
  roleLabel: string;
  expectedCadence: PeriodType;
  teamId: string | null;
  teamName: string | null;
  status: 'submitted' | 'late' | 'missing' | 'leave';
  dueDate: string;
  submittedAtUtc: string | null;
}
export interface TeamShortfallRow {
  teamId: string | null;
  teamName: string;
  expected: number;
  missing: number;
  late: number;
}
export interface MissingReportsReport {
  periodKey: string;
  periodLabel: string;
  weekStart: string;
  weekEnd: string;
  expectedCount: number;
  submittedCount: number;
  lateCount: number;
  missingCount: number;
  scopeType: string;
  canViewRows: boolean;
  rows: ExpectedReporterRow[];
  teamShortfalls: TeamShortfallRow[];
  // عدد من استُثنوا بإجازة معتمدة تغطّي الأسبوع (V1.0.1).
  leaveCount: number;
}

export interface ApprovalDelayRow {
  submissionId: string;
  submitterId: string;
  submitterName: string;
  templateTitle: string;
  periodKey: string;
  status: SubmissionStatus;
  approverId: string;
  approverName: string;
  approverRoleLabel: string;
  dueDate: string;
  daysOverdue: number;
  submittedAtUtc: string | null;
}
export interface ApprovalDelaysReport {
  scopeType: string;
  delayCount: number;
  rows: ApprovalDelayRow[];
}

export interface SalesDailyComplianceRow {
  userId: string;
  fullName: string;
  teamId: string | null;
  teamName: string | null;
  expectedDays: number;
  submittedDays: number;
  missingDays: number;
  isComplete: boolean;
  needsReview: boolean;
  // أيام الإجازة المعتمدة ضمن النافذة المنقضية (V1.0.1) — مستثناة من المتوقَّع.
  leaveDays: number;
}
export interface SalesDailyComplianceReport {
  periodKey: string;
  periodLabel: string;
  weekStart: string;
  weekEnd: string;
  reportersCount: number;
  completeCount: number;
  incompleteCount: number;
  scopeType: string;
  canViewRows: boolean;
  rows: SalesDailyComplianceRow[];
}

// ===== متابعة التزام التسليم (per-person) — شاشة متابعة فقط بلا أي محتوى للتقرير =====
// تطابق Reporting.Application.Reports.SubmissionComplianceRow/Report خادمًا.
export interface SubmissionComplianceRow {
  userId: string;
  fullName: string;
  departmentName: string | null;
  teamName: string | null;
  jobRoleName: string | null;
  submitted: boolean;
  statusLabel: string;
  late: boolean;
  submittedAtUtc: string | null;
  periodKey: string;
}
export interface SubmissionComplianceReport {
  periodKey: string;
  periodLabel: string;
  expected: number;
  submitted: number;
  notSubmitted: number;
  late: number;
  completionRate: number;
  rows: SubmissionComplianceRow[];
  lateSubmitted: number;
  missingOverdue: number;
  onTime: number;
  onTimePercent: number;
}

// ===== RPT-DUE-LATE-COMPLIANCE-R1: ملخّص/اتجاه/تأخّر الالتزام =====
// تطابق Reporting.Application.Reports.Compliance* خادمًا. أرقام التزام فقط (لا محتوى تقارير).
export interface ComplianceSummaryReport {
  periodKey: string;
  periodLabel: string;
  expected: number;
  submitted: number;
  missing: number;
  late: number;
  lateSubmitted: number;
  missingOverdue: number;
  onTime: number;
  compliancePercent: number;
  onTimePercent: number;
}
export interface ComplianceTrendPoint {
  periodKey: string;
  periodLabel: string;
  expected: number;
  submitted: number;
  late: number;
  compliancePercent: number;
  onTimePercent: number;
}
export interface ComplianceTrendReport {
  weeks: number;
  points: ComplianceTrendPoint[];
}
export interface LateByTemplateRow {
  jobRoleId: string;
  templateTitle: string;
  jobRoleName: string;
  expected: number;
  late: number;
  missing: number;
  latePercent: number;
}
export interface LateByTemplateReport {
  periodKey: string;
  periodLabel: string;
  rows: LateByTemplateRow[];
}
export interface ComplianceBreakdownRow {
  groupId: string | null;
  groupName: string;
  expected: number;
  submitted: number;
  late: number;
  missing: number;
  compliancePercent: number;
  onTimePercent: number;
}
export interface ComplianceBreakdownReport {
  periodKey: string;
  periodLabel: string;
  groupBy: string;
  rows: ComplianceBreakdownRow[];
}

// ===== RPT-WORKFLOW-BOTTLENECKS-R1: اختناقات مسار الاعتماد =====
// تطابق Reporting.Application.Reports.WorkflowBottleneck* خادمًا. لا محتوى للتقارير — موضع/عمر فقط.
export interface WorkflowBottlenecksSummaryReport {
  totalPending: number;
  overduePending: number;
  oldestPendingAgeHours: number;
  averageStageAgeHours: number;
  stageWithMostPending: string | null;
  stageWithMostPendingLabel: string | null;
  stageWithMostPendingCount: number;
  reviewerWithMostPending: string | null;
  reviewerWithMostPendingName: string | null;
  reviewerWithMostPendingCount: number;
}
export interface WorkflowBottleneckStageRow {
  stageKey: string;
  stageLabel: string;
  pendingCount: number;
  overdueCount: number;
  averageAgeHours: number;
  oldestAgeHours: number;
  slaHours: number;
}
export interface WorkflowBottlenecksByStageReport {
  rows: WorkflowBottleneckStageRow[];
}
export interface WorkflowBottleneckApproverRow {
  approverId: string;
  approverName: string;
  approverRole: string;
  approverRoleLabel: string;
  stageKey: string;
  stageLabel: string;
  pendingCount: number;
  overdueCount: number;
  averageAgeHours: number;
  oldestAgeHours: number;
}
export interface WorkflowBottlenecksByApproverReport {
  rows: WorkflowBottleneckApproverRow[];
}
export interface WorkflowBottleneckDetailRow {
  submissionId: string;
  templateTitle: string;
  submitterName: string;
  teamName: string | null;
  departmentName: string | null;
  currentApproverId: string | null;
  currentApproverName: string | null;
  currentApproverRole: string | null;
  stageKey: string;
  stageLabel: string;
  status: SubmissionStatus;
  statusLabel: string;
  submittedAtUtc: string | null;
  stageEnteredAtUtc: string;
  ageHours: number;
  slaHours: number;
  isOverdue: boolean;
}
export interface WorkflowBottlenecksDetailsReport {
  total: number;
  overdue: number;
  rows: WorkflowBottleneckDetailRow[];
}

// ===== Phase 6: بُعد العميل/المشروع وإدارة الحسابات =====
export type ClientStatus = 'Active' | 'Paused' | 'AtRisk' | 'Closed';
export type ProjectStatus = 'Active' | 'Paused' | 'Completed' | 'AtRisk' | 'Closed';
export type ServiceType = 'Social' | 'Seo' | 'MediaBuying' | 'Website' | 'Video' | 'Branding' | 'Other';

export interface ClientDto {
  id: string;
  name: string;
  status: ClientStatus;
  accountManagerId: string | null;
  accountManagerName: string | null;
  mainContactName: string | null;
  mainContactInfo: string | null;
  notes: string | null;
  projectCount: number;
  activeProjectCount: number;
  atRiskProjectCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  canHardDelete: boolean;
  deleteBlockReason: string | null;
}
export interface CreateClientRequest {
  name: string;
  accountManagerId?: string | null;
  mainContactName?: string | null;
  mainContactInfo?: string | null;
  notes?: string | null;
  status?: ClientStatus;
}
export interface UpdateClientRequest {
  name: string;
  status: ClientStatus;
  accountManagerId?: string | null;
  mainContactName?: string | null;
  mainContactInfo?: string | null;
  notes?: string | null;
}

export interface ProjectDto {
  id: string;
  clientId: string;
  clientName: string | null;
  name: string;
  serviceType: ServiceType;
  status: ProjectStatus;
  startDate: string | null;
  endDate: string | null;
  ownerTeamId: string | null;
  ownerTeamName: string | null;
  accountManagerId: string | null;
  accountManagerName: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  canHardDelete: boolean;
  deleteBlockReason: string | null;
}
export interface CreateProjectRequest {
  clientId: string;
  name: string;
  serviceType: ServiceType;
  startDate?: string | null;
  endDate?: string | null;
  ownerTeamId?: string | null;
  accountManagerId?: string | null;
  notes?: string | null;
  status?: ProjectStatus;
}
export interface UpdateProjectRequest {
  name: string;
  serviceType: ServiceType;
  status: ProjectStatus;
  startDate?: string | null;
  endDate?: string | null;
  ownerTeamId?: string | null;
  accountManagerId?: string | null;
  notes?: string | null;
}

// تقرير مرتبط بعميل/مشروع.
export interface LinkedReportRow {
  submissionId: string;
  submitterId: string;
  submitterName: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
  clientId: string | null;
  projectId: string | null;
}

// ملخّص المشروع (drill-down).
export interface ProjectSummaryDto {
  project: ProjectDto;
  totalReports: number;
  closedReports: number;
  pendingReports: number;
  lastReportAtUtc: string | null;
  openRiskCount: number;
  openNoteCount: number;
}

// صحّة العميل (لوحة مدير الحساب) — يحدد الخادم مستوى الرؤية والصفوف.
export interface ClientHealthRow {
  clientId: string;
  clientName: string;
  status: ClientStatus;
  accountManagerId: string | null;
  accountManagerName: string | null;
  projectCount: number;
  atRiskProjectCount: number;
  openRiskCount: number;
  openNoteCount: number;
  lastReportAtUtc: string | null;
  churnRisk: string;
  decisionNeeded: boolean;
}
export interface ClientHealthReport {
  periodLabel: string;
  rows: ClientHealthRow[];
  totalClients: number;
  atRiskClients: number;
  decisionNeededCount: number;
  renewalOpportunities: number;
  viewLevel: string;
  canViewRows: boolean;
}

// ===== V1.0.1: الإجازات والاستئذانات =====
// التعدادات سلاسل نصية. DateOnly يُسلسل «YYYY-MM-DD»، وTimeOnly «HH:mm:ss».
export type LeaveRequestType = 'Leave' | 'Permission';
// قرار الموظّف عند تجاوز الاستئذان رصيدَ الأذونات الشهري (V1.1.1).
export type PermissionShortfallResolution = 'None' | 'CompensateAfterHours' | 'AdminOrPayrollReview';
export type LeaveRequestStatus =
  | 'Draft'
  | 'Submitted'
  | 'TeamLeaderApproved'
  | 'TeamLeaderRejected'
  | 'ManagerApproved'
  | 'ManagerRejected'
  | 'HrApproved'
  | 'HrRejected'
  | 'ReturnedForEdit'
  | 'Cancelled';
export type LeaveRequestStep = 'Employee' | 'TeamLeader' | 'Manager' | 'Hr' | 'Completed';

export interface LeaveRequestEventDto {
  id: string;
  actorUserId: string;
  actorName: string | null;
  action: string;
  step: LeaveRequestStep;
  fromStatus: LeaveRequestStatus;
  toStatus: LeaveRequestStatus;
  comment: string | null;
  atUtc: string;
}
export interface LeaveRequestDto {
  id: string;
  requesterUserId: string;
  requesterName: string;
  type: LeaveRequestType;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  reason: string;
  notes: string | null;
  status: LeaveRequestStatus;
  currentStep: LeaveRequestStep;
  isHrRequest: boolean;
  teamLeaderReviewerId: string | null;
  teamLeaderReviewerName: string | null;
  managerReviewerId: string | null;
  managerReviewerName: string | null;
  hrReviewerId: string | null;
  hrReviewerName: string | null;
  teamLeaderDecisionAtUtc: string | null;
  managerDecisionAtUtc: string | null;
  hrDecisionAtUtc: string | null;
  rejectionReason: string | null;
  returnReason: string | null;
  impactsReports: boolean;
  canCancel: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  cancelledAtUtc: string | null;
  // لقطة كفاية رصيد الإجازات (V1.1). null للطلبات ذات الرصيد الكافي أو للأذونات.
  balanceAtRequest: number | null;
  requestedLeaveDays: number | null;
  uncoveredLeaveDays: number | null;
  isPotentialUnpaidLeave: boolean;
  employeeAcknowledgedUnpaidDeduction: boolean;
  employeeAcknowledgedAtUtc: string | null;
  // قرار الموظّف عند تجاوز رصيد الأذونات الشهري (V1.1.1). None للإجازات/الأذونات ضمن الرصيد.
  permissionShortfallResolution: PermissionShortfallResolution;
  timeline: LeaveRequestEventDto[];
}
export interface LeaveRequestListItemDto {
  id: string;
  requesterUserId: string;
  requesterName: string;
  type: LeaveRequestType;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  reason: string;
  status: LeaveRequestStatus;
  currentStep: LeaveRequestStep;
  isHrRequest: boolean;
  impactsReports: boolean;
  isPotentialUnpaidLeave: boolean;
  createdAtUtc: string;
}
export interface CreateLeaveRequestRequest {
  type: LeaveRequestType;
  startDate: string;
  endDate?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  reason: string;
  notes?: string | null;
  // إقرار الموظّف بأن الأيام غير المغطّاة بالرصيد قد تُحتسب إجازةً بدون راتب (V1.1 — حارس الرصيد).
  acknowledgedUnpaidDeduction?: boolean;
  // قرار الموظّف عند تجاوز رصيد الأذونات الشهري (V1.1.1). إلزامي (غير None) فقط عند نقص الرصيد الشهري للأذونات.
  permissionShortfallResolution?: PermissionShortfallResolution;
}
export interface LeaveRevokeRequest {
  reason: string;
}

// ===== FIN-L1 — عرض التأثير على الرواتب =====
// عرض إعلامي بحت على مستوى الشركة لطلبات الإجازة/الاستئذان المؤثّرة على الراتب + مراجعة مالية (حالة/ملاحظة).
// لا يعدّل الطلب الأصلي ولا حالته ولا يُجري خصمًا آليًّا. الطلب المؤثّر بلا صفّ مراجعة = Pending ضمنيًّا.
export type PayrollImpactReviewStatus = 'Pending' | 'Processed' | 'Ignored' | 'NeedsReview';
export type PayrollImpactType =
  | 'UnpaidLeave'
  | 'PermissionAfterHoursCompensation'
  | 'PermissionAdminOrPayrollReview';

export interface PayrollImpactSummaryDto {
  totalImpacted: number;
  totalUncoveredLeaveDays: number;
  totalImpactedPermissions: number;
  afterHoursCompensationRequests: number;
  needsFinanceReviewCount: number;
}
export interface PayrollImpactListItemDto {
  leaveRequestId: string;
  requesterUserId: string;
  requesterName: string;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  type: LeaveRequestType;
  impactType: PayrollImpactType;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  approvalStatus: LeaveRequestStatus;
  balanceAtRequest: number | null;
  requestedLeaveDays: number | null;
  uncoveredLeaveDays: number | null;
  isPotentialUnpaidLeave: boolean;
  employeeAcknowledgedUnpaidDeduction: boolean;
  employeeAcknowledgedAtUtc: string | null;
  permissionShortfallResolution: PermissionShortfallResolution;
  reviewStatus: PayrollImpactReviewStatus;
  financeNote: string | null;
  reviewedByUserId: string | null;
  reviewedByName: string | null;
  reviewedAtUtc: string | null;
  createdAtUtc: string;
}
export interface PayrollImpactListDto {
  summary: PayrollImpactSummaryDto;
  items: PayrollImpactListItemDto[];
}
export interface PayrollImpactDetailDto {
  item: PayrollImpactListItemDto;
  reason: string;
  notes: string | null;
  canManage: boolean;
}
export interface PayrollImpactReviewRequest {
  status: PayrollImpactReviewStatus;
  financeNote?: string | null;
}

// ===== V1.1 — أرصدة الإجازات والأذونات (خدمات الموظف) =====
export type BalanceType = 'AnnualLeave' | 'Permission';
export type BalanceDirection = 'Credit' | 'Debit';
export type BalanceSource =
  | 'OpeningBalance'
  | 'ApprovedLeave'
  | 'ApprovedPermission'
  | 'ManualAdjustment'
  | 'CarryOver'
  | 'Reversal';
export type PermissionUnit = 'Count' | 'Hours';

export interface BalanceSummaryDto {
  balanceType: BalanceType;
  credited: number;
  debited: number;
  remaining: number;
  isNegative: boolean;
}
export interface MyBalancesDto {
  year: number;
  annualLeave: BalanceSummaryDto;
  permission: BalanceSummaryDto;
  pendingLeaveRequests: number;
  permissionUnit: PermissionUnit;
  permissionMonthlyLimit: number | null;
  permissionAnnualLimit: number | null;
  allowNegativeBalance: boolean;
  hasPolicy: boolean;
  permissionUsedThisMonth: number | null;
  permissionRemainingThisMonth: number | null;
}
export interface EmployeeBalanceRowDto {
  employeeId: string;
  employeeName: string;
  jobTitle: string | null;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  year: number;
  annualLeaveRemaining: number;
  permissionRemaining: number;
  annualLeaveNegative: boolean;
  permissionNegative: boolean;
}
export interface BalanceLedgerEntryDto {
  id: string;
  balanceType: BalanceType;
  amount: number;
  direction: BalanceDirection;
  source: BalanceSource;
  relatedRequestId: string | null;
  year: number;
  notes: string | null;
  createdBy: string;
  createdByName: string | null;
  createdAtUtc: string;
}
export interface EmployeeLedgerDto {
  employeeId: string;
  employeeName: string;
  year: number;
  annualLeave: BalanceSummaryDto;
  permission: BalanceSummaryDto;
  entries: BalanceLedgerEntryDto[];
}
export interface OpeningBalanceRequest {
  balanceType: BalanceType;
  amount: number;
  year: number;
  notes?: string | null;
}
export interface BalanceAdjustmentRequest {
  balanceType: BalanceType;
  direction: BalanceDirection;
  amount: number;
  year: number;
  reason: string;
}

// ===== V1.1 — طلبات الموارد البشرية العامة (خدمات الموظف) =====
export type EmployeeServiceRequestType =
  | 'HrLetter'
  | 'SalaryCertificate'
  | 'ExperienceCertificate'
  | 'BankLetter'
  | 'EmbassyLetter'
  | 'PersonalDataUpdate'
  | 'Other';
export type PreferredLanguage = 'Arabic' | 'English';
export type EmployeeServiceRequestStatus =
  | 'Submitted'
  | 'InReview'
  | 'Completed'
  | 'Rejected'
  | 'Cancelled';

export interface EmployeeServiceRequestEventDto {
  id: string;
  actorUserId: string;
  actorName: string | null;
  action: string;
  fromStatus: EmployeeServiceRequestStatus;
  toStatus: EmployeeServiceRequestStatus;
  comment: string | null;
  atUtc: string;
}
export interface EmployeeServiceRequestDto {
  id: string;
  requesterUserId: string;
  requesterName: string;
  requestType: EmployeeServiceRequestType;
  title: string;
  description: string | null;
  preferredLanguage: PreferredLanguage;
  destinationEntity: string | null;
  attachmentPath: string | null;
  status: EmployeeServiceRequestStatus;
  hrComment: string | null;
  // الملف النهائي (الخطاب) — لا يُكشف المسار الداخلي إطلاقًا، فقط الحالة/الاسم/وقت الرفع.
  hasFinalDocument: boolean;
  finalDocumentFileName: string | null;
  finalDocumentUploadedAt: string | null;
  assignedToHrUserId: string | null;
  assignedToHrName: string | null;
  rejectionReason: string | null;
  completedAtUtc: string | null;
  canCancel: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  timeline: EmployeeServiceRequestEventDto[];
}
export interface EmployeeServiceRequestListItemDto {
  id: string;
  requesterUserId: string;
  requesterName: string;
  requestType: EmployeeServiceRequestType;
  title: string;
  preferredLanguage: PreferredLanguage;
  status: EmployeeServiceRequestStatus;
  createdAtUtc: string;
}
export interface CreateEmployeeServiceRequest {
  requestType: EmployeeServiceRequestType;
  title: string;
  description?: string | null;
  preferredLanguage: PreferredLanguage;
  destinationEntity?: string | null;
  attachmentPath?: string | null;
}
export interface EmployeeServiceRequestCommentRequest {
  comment: string;
}
export interface EmployeeServiceRequestCompleteRequest {
  hrComment?: string | null;
}
export interface EmployeeServiceRequestRejectRequest {
  reason: string;
}

// ===== Audit =====
export interface AuditLogDto {
  id: string;
  actorId: string | null;
  actorName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  dataJson: string | null;
  ipAddress: string | null;
  createdAtUtc: string;
}

// المناصب المرنة (Phase 1A — رؤية فقط)
export type PositionScopeKind = 'Department' | 'Team' | 'SpecificUsers' | 'AllCompany';

export interface PositionScopeDto {
  id: string;
  kind: PositionScopeKind;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  targetUserId: string | null;
  targetUserName: string | null;
}

export interface PositionDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  permissions: string[];
  scopes: PositionScopeDto[];
  assignedUsersCount: number;
}

export interface PositionPermissionOptionDto {
  key: string;
  labelAr: string;
}

export interface UserPositionDto {
  id: string;
  positionId: string;
  positionCode: string;
  positionName: string;
  positionIsActive: boolean;
}

export interface CreatePositionRequest {
  code: string;
  name: string;
  description: string | null;
}

export interface AddPositionScopeRequest {
  kind: PositionScopeKind;
  departmentId: string | null;
  teamId: string | null;
  targetUserId: string | null;
}

// منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1) — Admin فقط، عرض فقط، معزول.
export type ReportViewGrantScopeKind = 'User' | 'Team';

export interface ReportViewGrantDto {
  id: string;
  granteeUserId: string;
  granteeName: string;
  scopeKind: ReportViewGrantScopeKind;
  targetUserId: string | null;
  targetUserName: string | null;
  targetTeamId: string | null;
  targetTeamName: string | null;
  isActive: boolean;
  createdAtUtc: string;
  createdByUserId: string | null;
  revokedAtUtc: string | null;
  expiresAtUtc: string | null;
  notes: string | null;
}

export interface CreateReportViewGrantRequest {
  granteeUserId: string;
  scopeKind: ReportViewGrantScopeKind;
  targetUserId?: string | null;
  targetTeamId?: string | null;
  expiresAtUtc?: string | null;
  notes?: string | null;
}

// ===== محفظة مدير الحساب (ACCOUNT-MANAGER-PORTFOLIO) — عرض فقط =====
export interface PortfolioProjectDto {
  id: string;
  name: string;
  clientId: string;
  clientName: string | null;
  serviceType: ServiceType;
  status: ProjectStatus;
  startDate: string | null;
  endDate: string | null;
  outputCount: number;
  lastOutputAtUtc: string | null;
}
export interface PortfolioClientDto {
  id: string;
  name: string;
  status: ClientStatus;
  projectCount: number;
  activeProjectCount: number;
}
export interface PortfolioClientDetailDto {
  client: PortfolioClientDto;
  projects: PortfolioProjectDto[];
}
export interface PortfolioOutputDto {
  submissionId: string;
  submitterId: string;
  submitterName: string | null;
  periodType: PeriodType;
  periodKey: string;
  status: SubmissionStatus;
  submittedAtUtc: string | null;
}

// ===== ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) =====
export type GovernanceCategory =
  | 'Observation'
  | 'Risk'
  | 'Decision'
  | 'Recommendation'
  | 'FollowUp'
  | 'Compliance'
  | 'Performance'
  | 'OperationalIssue';
export type GovernanceSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type GovernanceItemStatus =
  | 'Open'
  | 'InReview'
  | 'Waiting'
  | 'Resolved'
  | 'Closed'
  | 'Cancelled';
export type GovernanceItemUpdateType =
  | 'Created'
  | 'Comment'
  | 'StatusChanged'
  | 'Reassigned'
  | 'FollowUp'
  | 'Edited';
export type GovernanceApplicationScope =
  | 'Company'
  | 'Department'
  | 'Team'
  | 'User'
  | 'RelatedReport';

export interface GovernanceItemListItemDto {
  id: string;
  title: string;
  category: GovernanceCategory;
  severity: GovernanceSeverity;
  status: GovernanceItemStatus;
  applicationScope: GovernanceApplicationScope;
  createdById: string;
  createdByName: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  relatedSubmissionId: string | null;
  relatedUserId: string | null;
  relatedUserName: string | null;
  dueDate: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
export interface GovernanceItemUpdateDto {
  id: string;
  authorId: string;
  authorName: string | null;
  updateType: GovernanceItemUpdateType;
  body: string | null;
  oldStatus: GovernanceItemStatus | null;
  newStatus: GovernanceItemStatus | null;
  createdAtUtc: string;
}
export interface GovernanceItemDetailDto {
  item: GovernanceItemListItemDto;
  description: string | null;
  resolutionSummary: string | null;
  closedAtUtc: string | null;
  closedById: string | null;
  closedByName: string | null;
  canEdit: boolean;
  canChangeStatus: boolean;
  timeline: GovernanceItemUpdateDto[];
}
export interface CreateGovernanceItemRequest {
  title: string;
  description?: string | null;
  category: GovernanceCategory;
  severity: GovernanceSeverity;
  applicationScope: GovernanceApplicationScope;
  assignedToUserId?: string | null;
  departmentId?: string | null;
  teamId?: string | null;
  relatedSubmissionId?: string | null;
  relatedUserId?: string | null;
  dueDate?: string | null;
}
export interface UpdateGovernanceItemRequest extends CreateGovernanceItemRequest {}
export interface ChangeGovernanceItemStatusRequest {
  status: GovernanceItemStatus;
  note?: string | null;
  resolutionSummary?: string | null;
}
export interface AddGovernanceItemCommentRequest {
  body: string;
  isFollowUp?: boolean;
}

// ===== التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ عن بنود الحوكمة العامة =====
export type EscalationType =
  | 'Performance'
  | 'Delay'
  | 'Quality'
  | 'Compliance'
  | 'Communication'
  | 'Workflow'
  | 'ClientImpact'
  | 'PolicyViolation'
  | 'Other';
export type EscalationSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type GovernanceEscalationStatus =
  | 'Open'
  | 'UnderReview'
  | 'Assigned'
  | 'WaitingForResponse'
  | 'Resolved'
  | 'Closed'
  | 'Reopened'
  | 'Cancelled';
export type EscalationTargetType =
  | 'User'
  | 'Department'
  | 'Team'
  | 'Report'
  | 'Workflow'
  | 'GovernanceItem'
  | 'Operational'
  | 'Other';
export type EscalationUpdateType =
  | 'Created'
  | 'Comment'
  | 'StatusChanged'
  | 'Assigned'
  | 'Reopened'
  | 'Edited'
  | 'Closed';

export interface GovernanceEscalationListItemDto {
  id: string;
  title: string;
  escalationType: EscalationType;
  severity: EscalationSeverity;
  status: GovernanceEscalationStatus;
  raisedByUserId: string;
  raisedByName: string | null;
  targetType: EscalationTargetType;
  targetUserId: string | null;
  targetUserName: string | null;
  targetDepartmentId: string | null;
  targetDepartmentName: string | null;
  targetTeamId: string | null;
  targetTeamName: string | null;
  relatedSubmissionId: string | null;
  relatedGovernanceItemId: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
export interface GovernanceEscalationUpdateDto {
  id: string;
  authorId: string;
  authorName: string | null;
  updateType: EscalationUpdateType;
  body: string | null;
  oldStatus: GovernanceEscalationStatus | null;
  newStatus: GovernanceEscalationStatus | null;
  createdAtUtc: string;
}
export interface GovernanceEscalationDetailDto {
  item: GovernanceEscalationListItemDto;
  description: string | null;
  resolution: string | null;
  closedAtUtc: string | null;
  closedByUserId: string | null;
  closedByName: string | null;
  canEdit: boolean;
  canChangeStatus: boolean;
  canAssign: boolean;
  canClose: boolean;
  canReopen: boolean;
  canComment: boolean;
  timeline: GovernanceEscalationUpdateDto[];
}
export interface CreateGovernanceEscalationRequest {
  title: string;
  description?: string | null;
  escalationType: EscalationType;
  severity: EscalationSeverity;
  targetType: EscalationTargetType;
  targetUserId?: string | null;
  targetDepartmentId?: string | null;
  targetTeamId?: string | null;
  relatedSubmissionId?: string | null;
  relatedGovernanceItemId?: string | null;
}
export interface UpdateGovernanceEscalationRequest extends CreateGovernanceEscalationRequest {}
export interface ChangeGovernanceEscalationStatusRequest {
  status: GovernanceEscalationStatus;
  note?: string | null;
  resolution?: string | null;
}
export interface AssignGovernanceEscalationRequest {
  assignedToUserId: string;
  note?: string | null;
}
export interface AddGovernanceEscalationCommentRequest {
  body: string;
}
export interface ReopenGovernanceEscalationRequest {
  note?: string | null;
}
export interface CloseGovernanceEscalationRequest {
  resolution?: string | null;
  note?: string | null;
}

// دليل أهداف التصعيد الآمن (على مستوى الشركة، بلا حسابات حسّاسة).
export interface EscalationTargetUserDto {
  id: string;
  fullName: string;
  departmentId?: string | null;
  teamId?: string | null;
}
export interface EscalationTargetDepartmentDto {
  id: string;
  name: string;
}
export interface EscalationTargetTeamDto {
  id: string;
  name: string;
  departmentId?: string | null;
}
export interface EscalationTargetDirectoryDto {
  users: EscalationTargetUserDto[];
  departments: EscalationTargetDepartmentDto[];
  teams: EscalationTargetTeamDto[];
}

// دليل ورشة الحوكمة الموحّد (GOV-DIRECTORY-SCOPE-FIX-R1): قوائم اختيار ضمن نطاق الملكية للورشة.
export interface GovernanceDirectoryUserDto {
  id: string;
  fullName: string;
  departmentId?: string | null;
  teamId?: string | null;
}
export interface GovernanceDirectoryDepartmentDto {
  id: string;
  name: string;
}
export interface GovernanceDirectoryTeamDto {
  id: string;
  name: string;
  departmentId?: string | null;
}
export interface GovernanceDirectoryDto {
  users: GovernanceDirectoryUserDto[];
  departments: GovernanceDirectoryDepartmentDto[];
  teams: GovernanceDirectoryTeamDto[];
}

// ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ يحوّل أيّ ملاحظة/تصعيد إلى إجراء متابَع =====
export type ActionItemSourceType = 'Manual' | 'Escalation' | 'GovernanceItem';
export type ActionItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type ActionItemStatus = 'Open' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';
export type ActionItemUpdateType =
  | 'Created'
  | 'Comment'
  | 'StatusChanged'
  | 'DueDateChanged'
  | 'AssigneeChanged'
  | 'CompletionSubmitted'
  | 'Reopened'
  | 'Cancelled';

export interface GovernanceActionItemListItemDto {
  id: string;
  title: string;
  sourceType: ActionItemSourceType;
  sourceId: string | null;
  sourceTitle: string | null;
  priority: ActionItemPriority;
  status: ActionItemStatus;
  isOverdue: boolean;
  dueDate: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdByUserId: string;
  createdByName: string | null;
  isSensitive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
export interface GovernanceActionItemUpdateDto {
  id: string;
  authorId: string;
  authorName: string | null;
  updateType: ActionItemUpdateType;
  body: string | null;
  oldStatus: ActionItemStatus | null;
  newStatus: ActionItemStatus | null;
  createdAtUtc: string;
}
export interface GovernanceActionItemDetailDto {
  item: GovernanceActionItemListItemDto;
  description: string | null;
  completionNote: string | null;
  completedAtUtc: string | null;
  completedByUserId: string | null;
  completedByName: string | null;
  assignedByUserId: string | null;
  assignedByName: string | null;
  sourceVisibleToViewer: boolean;
  canChangeStatus: boolean;
  canAssign: boolean;
  canChangeDueDate: boolean;
  canCancel: boolean;
  canReopen: boolean;
  canComment: boolean;
  timeline: GovernanceActionItemUpdateDto[];
}
export interface CreateGovernanceActionItemRequest {
  title: string;
  description?: string | null;
  priority: ActionItemPriority;
  sourceType?: ActionItemSourceType;
  sourceId?: string | null;
  assignedToUserId?: string | null;
  dueDate?: string | null;
}
export interface ChangeGovernanceActionItemStatusRequest {
  status: ActionItemStatus;
  note?: string | null;
  completionNote?: string | null;
}
export interface AssignGovernanceActionItemRequest {
  assignedToUserId: string;
  note?: string | null;
}
export interface ChangeGovernanceActionItemDueDateRequest {
  dueDate?: string | null;
  note?: string | null;
}
export interface AddGovernanceActionItemCommentRequest {
  body: string;
}
export interface CancelGovernanceActionItemRequest {
  note?: string | null;
}
export interface ActionItemAssigneeDto {
  id: string;
  fullName: string;
  departmentId?: string | null;
  teamId?: string | null;
}
export interface ActionItemAssigneeDirectoryDto {
  users: ActionItemAssigneeDto[];
}

// ===== RPT-DUE1 — مواعيد التقارير والتأخّر (قراءة فقط، محسوب عند الطلب) =====
export type DelayType =
  | 'NoDelay'
  | 'EmployeeReportNotSubmitted'
  | 'TeamLeaderReviewOverdue'
  | 'ManagerReviewOverdue'
  | 'ExecutiveReviewPending';

export interface ReportDueMyStatus {
  weekKey: string;
  weekLabel: string;
  weekStart: string;
  weekEnd: string;
  employeeDueDate: string;
  expected: boolean;
  submitted: boolean;
  isOverdue: boolean;
  delayType: DelayType;
  statusLabel: string;
  submissionId: string | null;
}

export interface ReportDueOverdueRow {
  userId: string;
  userName: string;
  role: string;
  roleLabel: string;
  departmentId: string | null;
  departmentName: string | null;
  teamId: string | null;
  teamName: string | null;
  delayType: DelayType;
  expectedAction: string;
  dueDate: string;
  overdueDays: number;
  relatedSubmissionId: string | null;
}

export interface ReportDueOverview {
  weekKey: string;
  weekLabel: string;
  scopeType: string;
  requiredReportsCount: number;
  submittedReportsCount: number;
  missingReportsCount: number;
  overdueReportsCount: number;
  pendingReviewsCount: number;
  overdueReviewsCount: number;
  items: ReportDueOverdueRow[];
}

export interface ReportDueOverdueReport {
  weekKey: string;
  totalCount: number;
  overdueReportsCount: number;
  overdueReviewsCount: number;
  rows: ReportDueOverdueRow[];
}

// ===== EMAIL-NOTIFICATIONS-UI-R1 (سجلّ إشعارات البريد — قراءة فقط) =====
export interface EmailNotificationLogFilter {
  page?: number;
  pageSize?: number;
  status?: string;
  eventType?: string;
  recipientUserId?: string;
  search?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface EmailNotificationRowDto {
  id: string;
  createdAtUtc: string;
  eventType: string;
  status: string;
  mode: string;
  recipientUserId: string | null;
  recipientName: string | null;
  recipientEmail: string | null;
  subject: string;
  bodyPreview: string;
  correlationKey: string;
  sourceEntityType: string;
  sourceEntityId: string;
  errorMessage: string | null;
}

export interface EmailNotificationLogSummaryDto {
  total: number;
  dryRun: number;
  skipped: number;
  failed: number;
  sent: number;
  pending: number;
  cancelled: number;
  lastCreatedAtUtc: string | null;
}

export interface EmailNotificationLogPageDto {
  items: EmailNotificationRowDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: EmailNotificationLogSummaryDto;
}

export interface EmailNotificationLogDetailDto {
  id: string;
  createdAtUtc: string;
  eventType: string;
  status: string;
  mode: string;
  recipientUserId: string | null;
  recipientName: string | null;
  recipientEmail: string | null;
  subject: string;
  bodyHtml: string;
  bodyText: string | null;
  correlationKey: string;
  sourceEntityType: string;
  sourceEntityId: string;
  attemptCount: number;
  lastAttemptAt: string | null;
  sentAt: string | null;
  failedAt: string | null;
  errorMessage: string | null;
  createdByUserId: string | null;
}

// ===== EMAIL-CONTROL-CENTER-R1 (مركز التحكم بالبريد — قوالب/قواعد/تذكير يدويّ DryRun، Admin فقط) =====
export interface EmailTemplateDto {
  id: string;
  key: string;
  nameAr: string;
  category: string;
  subjectTemplate: string;
  bodyTemplate: string;
  availableVariables: string[];
  isEnabled: boolean;
  defaultMode: string;
  updatedAtUtc: string | null;
}

export interface UpdateEmailTemplateRequest {
  nameAr: string;
  subjectTemplate: string;
  bodyTemplate: string;
  isEnabled: boolean;
  defaultMode: string;
}

export interface EmailTemplatePreviewRequest {
  subjectTemplate?: string | null;
  bodyTemplate?: string | null;
  variables?: Record<string, string> | null;
}

export interface EmailTemplatePreviewDto {
  subject: string;
  bodyHtml: string;
  bodyText: string;
}

export interface EmailRuleDto {
  id: string;
  templateKey: string;
  eventType: string;
  isEnabled: boolean;
  sendToEmployee: boolean;
  sendToManager: boolean;
  sendToTeamLeader: boolean;
  sendToHr: boolean;
  sendToGovernance: boolean;
  sendToAdmin: boolean;
  cooldownMinutes: number | null;
  mode: string;
  updatedAtUtc: string | null;
}

export interface UpdateEmailRuleRequest {
  isEnabled: boolean;
  sendToEmployee: boolean;
  sendToManager: boolean;
  sendToTeamLeader: boolean;
  sendToHr: boolean;
  sendToGovernance: boolean;
  sendToAdmin: boolean;
  cooldownMinutes: number | null;
  mode: string;
}

export type RecipientScopeType = 'Users' | 'Team' | 'Department' | 'JobRole' | 'IdentityRole';

export interface RecipientPreviewRequest {
  scopeType: RecipientScopeType;
  scopeId?: string | null;
  roleName?: string | null;
  userIds?: string[] | null;
}

export interface RecipientPreviewRowDto {
  userId: string;
  fullName: string;
  email: string | null;
  eligible: boolean;
  reason: string;
}

export interface RecipientPreviewDto {
  totalCandidates: number;
  eligibleCount: number;
  excludedCount: number;
  rows: RecipientPreviewRowDto[];
}

export interface ManualReminderDryRunRequest {
  scopeType: RecipientScopeType;
  subject: string;
  body: string;
  link?: string | null;
  scopeId?: string | null;
  roleName?: string | null;
  userIds?: string[] | null;
}

export interface ManualReminderDryRunResultDto {
  batchId: string;
  total: number;
  created: number;
  skipped: number;
  duplicate: number;
  recipients: RecipientPreviewRowDto[];
}

// ===== محرّك التجميع الرقمي للمبيعات (ERDS Phase 4) — B2C-UAT-FIXPACK الجزء 4 =====
// عرض تجميعي قراءة فقط للمدير: النطاق مفروض خادميًّا عبر IScopeResolver (يرى فريقه فقط، لا يتعدّى صلاحيّته).
export interface AggregationFilter {
  periodType?: PeriodType;
  periodKey?: string;
  employeeId?: string;
  teamId?: string;
  departmentId?: string;
  item?: string;
}
export interface B2cCourseAggregateRow {
  periodKey: string;
  employeeId: string;
  employeeName: string;
  course: string;
  teamId: string | null;
  departmentId: string | null;
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  followUps: number;
  sales: number;
  revenue: number;
  lost: number;
  conversionRate: number;
  qualificationRate: number;
  contactRate: number;
  revenuePerHour: number;
  salesPerHour: number;
  lostRate: number;
}
export interface B2cAggregationReport {
  periodKey: string | null;
  rowCount: number;
  submissionsConsidered: number;
  submissionsIgnored: number;
  rowsIgnored: number;
  viewLevel: string;
  rows: B2cCourseAggregateRow[];
}
// تفصيل موظّف داخل مجموعة دورة (Drill-down). الحقول القياسية = الإجمالي Total (New + Old)؛
// دلوَا new/old يفصّلان مساهمة البيانات الجديدة/القديمة (Phase 7.1).
export interface B2cCourseEmployeeRow {
  employeeId: string;
  employeeName: string;
  teamId: string | null;
  departmentId: string | null;
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  followUps: number;
  sales: number;
  revenue: number;
  lost: number;
  conversionRate: number;
  new: B2cNewOldBucket;
  old: B2cNewOldBucket;
}
// كتالوج الدورات (المصدر الرسمي لأسماء دورات مبيعات B2C).
export interface CourseDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
// كتالوج خدمات B2B (المصدر الرسمي لأسماء خدمات مبيعات B2B حسب الخدمة).
export interface ServiceDto {
  id: string;
  nameAr: string;
  nameEn: string | null;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
// صفّ تجميع «حسب الدورة»: إجماليات الدورة عبر كل الموظّفين + تفصيل الموظّفين.
export interface B2cCourseGroupRow {
  course: string;
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  followUps: number;
  sales: number;
  revenue: number;
  lost: number;
  conversionRate: number;
  qualificationRate: number;
  contactRate: number;
  revenuePerHour: number;
  salesPerHour: number;
  lostRate: number;
  employeeCount: number;
  employees: B2cCourseEmployeeRow[];
}
export interface B2cCourseGroupedReport {
  periodKey: string | null;
  courseCount: number;
  submissionsConsidered: number;
  submissionsIgnored: number;
  rowsIgnored: number;
  viewLevel: string;
  courses: B2cCourseGroupRow[];
}
// Phase 7 — دلو مؤشرات B2C واحد (New أو Old). ConversionRate = المبيعات ÷ Leads (New)
// أو المبيعات ÷ Old Leads Worked (Old = معدّل الاسترجاع Recovery).
export interface B2cNewOldBucket {
  workHours: number;
  leads: number;
  contacted: number;
  qualified: number;
  followUps: number;
  sales: number;
  revenue: number;
  lost: number;
  conversionRate: number;
  qualificationRate: number;
  contactRate: number;
  revenuePerHour: number;
  salesPerHour: number;
  lostRate: number;
}
// صفّ تجميع لكل دورة مع دلوَي البيانات الجديدة والقديمة جنبًا إلى جنب.
export interface B2cNewOldCourseRow {
  course: string;
  new: B2cNewOldBucket;
  old: B2cNewOldBucket;
}
// نتيجة تجميع B2C بفصل البيانات الجديدة New / بيانات CRM القديمة Old.
export interface B2cNewOldReport {
  periodKey: string | null;
  courseCount: number;
  submissionsConsidered: number;
  submissionsIgnored: number;
  rowsIgnored: number;
  viewLevel: string;
  newTotals: B2cNewOldBucket;
  oldTotals: B2cNewOldBucket;
  courses: B2cNewOldCourseRow[];
}
export interface B2bServiceAggregateRow {
  periodKey: string;
  employeeId: string;
  employeeName: string;
  service: string;
  teamId: string | null;
  departmentId: string | null;
  workHours: number;
  leads: number;
  meetings: number;
  proposals: number;
  negotiation: number;
  won: number;
  lost: number;
  revenue: number;
  meetingRate: number;
  proposalRate: number;
  winRate: number;
  revenuePerHour: number;
  wonPerHour: number;
  lostRate: number;
}
export interface B2bAggregationReport {
  periodKey: string | null;
  rowCount: number;
  submissionsConsidered: number;
  submissionsIgnored: number;
  rowsIgnored: number;
  viewLevel: string;
  rows: B2bServiceAggregateRow[];
}
// RC-3 Task 2A — دلو مؤشرات B2B واحد (Total/New Leads/Data Scraping). validLeads = 0 لمصدر New Leads.
export interface B2bSourceBucket {
  workHours: number;
  leads: number;
  validLeads: number;
  contacted: number;
  meetings: number;
  proposals: number;
  negotiation: number;
  won: number;
  revenue: number;
  winRate: number;
  meetingRate: number;
  proposalRate: number;
  revenuePerHour: number;
  wonPerHour: number;
}
// تفصيل موظّف داخل خدمة (Drill-down) مع دلاء Total/New/Data.
export interface B2bSourceServiceEmployeeRow {
  employeeId: string;
  employeeName: string;
  teamId: string | null;
  departmentId: string | null;
  total: B2bSourceBucket;
  newLeads: B2bSourceBucket;
  dataScraping: B2bSourceBucket;
}
// صفّ تجميع لكل خدمة مع دلاء Total/New/Data + تفصيل الموظّفين.
export interface B2bSourceServiceRow {
  service: string;
  total: B2bSourceBucket;
  newLeads: B2bSourceBucket;
  dataScraping: B2bSourceBucket;
  employeeCount: number;
  employees: B2bSourceServiceEmployeeRow[];
}
// نتيجة تجميع B2B بفصل المصدر (New Leads / Data Scraping / Legacy).
export interface B2bSourceReport {
  periodKey: string | null;
  serviceCount: number;
  submissionsConsidered: number;
  submissionsIgnored: number;
  rowsIgnored: number;
  viewLevel: string;
  totals: B2bSourceBucket;
  newLeadsTotals: B2bSourceBucket;
  dataScrapingTotals: B2bSourceBucket;
  legacyTotals: B2bSourceBucket;
  services: B2bSourceServiceRow[];
}

// ===== ROLE-AWARE-REPORTING-CALENDAR — Phase 2.3/2.5 =====
// دورة تقارير مُدرِكة للدور: النافذة السبت→الجمعة موحّدة لكل المستويات، وتاريخ الاستحقاق يختلف بحسب
// الدور الأساسيّ الخادميّ فقط. كل الحقول محسوبة على الخادم — الواجهة لا تعيد حساب أيّ مفتاح دورة.
export type ReportingCalendarContext = 'Report' | 'Kpi';

export interface ReportingCycleDto {
  cycleKey: string;
  cycleNumber: number;
  cycleYear: number;
  cycleStart: string; // DateOnly (السبت)
  cycleEnd: string; // DateOnly (الجمعة)
  tuesdayReference: string;
  cycleLabel: string;
  shortLabel: string;
  dataCoverageStart: string;
  dataCoverageEnd: string;
  role: string;
  roleLabel: string;
  roleDueOffset: number;
  roleDueDate: string; // DateOnly — تاريخ استحقاق الدور
  roleDueDateLabel: string;
  offset: number; // 0=الحالية، سالب=ماضية، موجب=مستقبلية
  isCurrent: boolean;
  isPast: boolean;
  isFuture: boolean;
  status: string; // current | past | locked
  isOpen: boolean;
  isLocked: boolean;
  lockReason: string | null;
  isOverdue: boolean;
  requiresReason: boolean;
  today: string;
  context: ReportingCalendarContext;
}

export interface MyCyclesDto {
  context: ReportingCalendarContext;
  templateId: string | null;
  role: string;
  roleLabel: string;
  currentCycleKey: string;
  today: string;
  cycles: ReportingCycleDto[];
}

// ROLE-AWARE-REPORTING-CALENDAR — الوضع اليوميّ (Daily). صفّ يوم واحد + غلاف my-days.
// كل الحقول محسوبة خادميًّا (مفتاح اليوم YYYY-MM-DD بتوقيت الرياض، والحالة من قاعدة البيانات).
export type ReportingDayStatus =
  | 'Available'
  | 'Draft'
  | 'Submitted'
  | 'Overdue'
  | 'Holiday'
  | 'FutureLocked'
  | 'Returned'
  | 'Reopened';

export interface ReportingDayDto {
  dayKey: string; // YYYY-MM-DD
  date: string; // DateOnly
  dayNameAr: string; // «الثلاثاء»
  fullDateLabel: string; // «الثلاثاء 14 يوليو 2026»
  isToday: boolean;
  isPast: boolean;
  isFuture: boolean;
  isHoliday: boolean; // الجمعة وحدها (السبت يوم عمل)
  isSelectable: boolean;
  isOpenForDraft: boolean;
  isDueToday: boolean;
  isOverdue: boolean;
  isSubmitted: boolean;
  hasDraft: boolean;
  status: ReportingDayStatus;
  statusLabel: string;
  lockReason: string | null;
  previousDayKey: string;
  nextDayKey: string;
}

export interface MyDaysDto {
  templateId: string | null;
  role: string;
  roleLabel: string;
  currentDayKey: string;
  today: string;
  days: ReportingDayDto[];
}

// ===== الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1) =====
// عناصر محذوفة إداريًّا ناعمًا (تقارير + تقييمات KPI) قابلة للقراءة والاسترجاع وفق دلالات Hybrid.
export type ArchiveItemType = 'Report' | 'KpiEvaluation';
export type ArchiveRetentionStatus = 'Fresh' | 'ReviewDue' | 'LongTerm';
export type ArchiveRestoreStrategy = 'NotApplicable' | 'HistoricalApproverRestored' | 'NoActiveApprover';

export interface ArchiveWorkflowStepDto {
  level: number;
  approverId: string;
  approverName: string | null;
  status: string;
  comment: string | null;
  decidedAtUtc: string | null;
}

export interface ArchiveAuditEntryDto {
  id: string;
  action: string;
  actorId: string | null;
  actorName: string | null;
  createdAtUtc: string;
  dataJson: string | null;
}

export interface ArchiveItemDto {
  archiveItemId: string;
  itemType: ArchiveItemType;
  employeeId: string;
  employeeName: string;
  templateName: string;
  periodKey: string;
  status: string;
  deletedAtUtc: string;
  deletedByUserId: string | null;
  deletedByName: string | null;
  deletionReason: string | null;
  canRestore: boolean;
  restoreBlockedCode: string | null;
  restoreBlockedReason: string | null;
  daysSinceDeletion: number;
  retentionStatus: ArchiveRetentionStatus;
}

export interface ArchiveDetailsDto extends ArchiveItemDto {
  currentApproverId: string | null;
  currentApproverName: string | null;
  workflowSteps: ArchiveWorkflowStepDto[];
  fieldValuesCount: number;
  kpiResultsCount: number;
  reviewEventsCount: number;
  auditTrail: ArchiveAuditEntryDto[];
  historicalApproverId: string | null;
  historicalApproverName: string | null;
  historicalApproverIsActive: boolean | null;
  restoreStrategy: ArchiveRestoreStrategy;
  restoreWarning: string | null;
}

export interface ArchivePagedResult {
  items: ArchiveItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ArchiveListFilter {
  itemType?: ArchiveItemType;
  periodKey?: string;
  employeeId?: string;
  page?: number;
  pageSize?: number;
}

export interface RestoreRequest {
  reason: string;
}

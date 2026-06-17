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
  | 'Viewer';

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
  isActive: boolean;
  departmentId: string | null;
  teamId: string | null;
  managerId: string | null;
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
export interface RoleAccessDto {
  role: Role;
  roleLabelAr: string;
  scopeType: string;
  scopeDescriptionAr: string;
  permissions: string[];
  permissionLabelsAr: string[];
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
  | 'SectionHeader';

export type TemplateStatus = 'Draft' | 'Published' | 'Archived';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Returned' | 'Escalated';
export type KpiEvaluationStatus = 'Draft' | 'InProgress' | 'Submitted' | 'Approved' | 'Closed';
export type KpiCadence = 'WeeklyPulse' | 'Quarterly';
export type KpiCalcMethod = 'Manual' | 'Auto' | 'Hybrid';

// ===== Template detail (versions + fields) =====
export interface TemplateVersionDto {
  id: string;
  versionNumber: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  fields: TemplateFieldDto[];
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

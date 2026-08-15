// ======================================================================
// Project 360 — عقود الواجهة (CPW-R3 · R2-W12)
//
// **مرآة حرفيّة** لـ`Reporting.Application/Projects360/Project360Models.cs`:
// كلّ نوع هنا يقابل Record واحدًا هناك بنفس الحقول وبنفس الاختياريّة (`?` ⟵ `| null`).
// لا يُشتقّ في هذا الملفّ أيّ حقل ولا تُحسَب فيه أيّ قيمة — الاشتقاق كلّه في الخادم.
//
// **لا تكرار للأنواع المشتركة**: `ServiceType` و`ProjectStatus` و`DeliverablePriority`
// وأنواع الحوكمة تُستورَد من `./api` لأنّها موجودة فعلًا هناك وتخدم صفحات قائمة.
//
// الترميز: الخادم يسلسل الـEnums **نصًّا** (`JsonStringEnumConverter` في `Program.cs`)،
// فتقابلها هنا اتّحادات نصّيّة حرفيّة لا أرقام. و`DateOnly` تصل سلسلةً `YYYY-MM-DD`.
// ======================================================================

import type {
  DecisionStatus,
  DeliverablePriority,
  ManagementNoteStatus,
  ManagementNoteType,
  ProjectStatus,
  RiskSeverity,
  RiskStatus,
  ServiceType,
} from './api';

// ----------------------------------------------------------------------
// التعدادات الخاصّة بـProject 360 (تقابل `Reporting.Domain/Enums/Enums.cs`)
// ----------------------------------------------------------------------

export type ProjectObjectiveStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'AtRisk'
  | 'Completed'
  | 'Cancelled';

export type ProjectKpiCategory =
  | 'Marketing'
  | 'Seo'
  | 'PaidAds'
  | 'SocialMedia'
  | 'Sales'
  | 'Brand'
  | 'Content'
  | 'CustomerService'
  | 'Operations'
  | 'Custom';

export type ProjectKpiUnit = 'Percentage' | 'Number' | 'Currency' | 'Duration' | 'Score' | 'Custom';

export type ProjectKpiFrequency = 'Weekly' | 'Monthly' | 'Quarterly';

export type ProjectKpiDirection = 'HigherIsBetter' | 'LowerIsBetter';

export type ProjectKpiTrend = 'Unknown' | 'Up' | 'Flat' | 'Down';

/**
 * FINDING-W6-02 (قرار مالك مُثبَّت): ثلاث قيم **حصرًا**. ممنوع إضافة `Calculated`
 * وممنوع إعادة تسمية `Integration`. أيّ توسعة هنا تكسر عقدًا مجمَّدًا.
 */
export type ProjectKpiSourceType = 'Manual' | 'TaskDerived' | 'Integration';

export type ProjectDeliverableStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Delivered'
  | 'Delayed'
  | 'Cancelled';

export type ProjectHealthStatus = 'Green' | 'Yellow' | 'Red';

// ----------------------------------------------------------------------
// §3 — الاستراتيجيّة
// ----------------------------------------------------------------------

export interface ProjectStrategyAttributeDto {
  fieldCode: string;
  fieldNameAr: string | null;
  sectionCode: string | null;
  sectionNameAr: string | null;
  valueText: string | null;
  sortOrder: number;
}

export interface ProjectStrategyDto {
  id: string;
  projectId: string;
  vision: string | null;
  strategySummary: string | null;
  targetAudience: string | null;
  customerPersona: string | null;
  positioning: string | null;
  valueProposition: string | null;
  competitors: string | null;
  toneOfVoice: string | null;
  messaging: string | null;
  marketingApproach: string | null;
  successFactors: string | null;
  attributes: ProjectStrategyAttributeDto[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface UpsertProjectStrategyAttributeRequest {
  fieldCode: string;
  sectionCode?: string | null;
  valueText?: string | null;
  sortOrder?: number;
}

export interface UpsertProjectStrategyRequest {
  vision?: string | null;
  strategySummary?: string | null;
  targetAudience?: string | null;
  customerPersona?: string | null;
  positioning?: string | null;
  valueProposition?: string | null;
  competitors?: string | null;
  toneOfVoice?: string | null;
  messaging?: string | null;
  marketingApproach?: string | null;
  successFactors?: string | null;
  attributes?: UpsertProjectStrategyAttributeRequest[];
}

export interface ProjectStrategySchemaFieldDto {
  fieldCode: string;
  nameAr: string;
  isCore: boolean;
  sectionCode: string | null;
  sectionNameAr: string | null;
  sortOrder: number;
}

export interface ProjectStrategySectionDto {
  code: string;
  nameAr: string;
  sortOrder: number;
}

/**
 * مصدر بناء نموذج الاستراتيجيّة. **الأقسام والحقول تأتي منه حصرًا** — ممنوع تثبيت
 * «SEO» أو «Social» أو «Ads» في الواجهة، فالكتالوج قد يتغيّر بلا نشر واجهة.
 */
export interface ProjectStrategySchemaDto {
  coreFields: ProjectStrategySchemaFieldDto[];
  dynamicFields: ProjectStrategySchemaFieldDto[];
  sections: ProjectStrategySectionDto[];
}

// ----------------------------------------------------------------------
// §4 — الأهداف
// ----------------------------------------------------------------------

export interface ProjectObjectiveDto {
  id: string;
  projectId: string;
  workstreamId: string | null;
  name: string;
  description: string | null;
  priority: DeliverablePriority;
  weight: number;
  /** الوزن بعد التطبيع على مستوى المشروع — يحسبه الخادم (DEC-W4-01) ولا يُعاد حسابه هنا. */
  normalizedWeight: number;
  status: ProjectObjectiveStatus;
  startDate: string | null;
  dueDate: string | null;
  ownerUserId: string | null;
  ownerFullName: string | null;
  notes: string | null;
  sortOrder: number;
  isActive: boolean;
  /** مشتقّ من مؤشّرات الهدف (DN-02) — `null` تعني «لا مؤشّر محتسَبًا» لا «صفر». */
  progressPercent: number | null;
  kpiCount: number;
  computedKpiCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateProjectObjectiveRequest {
  name: string;
  description?: string | null;
  workstreamId?: string | null;
  priority?: DeliverablePriority;
  weight?: number;
  status?: ProjectObjectiveStatus;
  startDate?: string | null;
  dueDate?: string | null;
  ownerUserId?: string | null;
  notes?: string | null;
  sortOrder?: number;
}

export interface UpdateProjectObjectiveRequest {
  name: string;
  description?: string | null;
  workstreamId?: string | null;
  priority?: DeliverablePriority;
  weight?: number;
  startDate?: string | null;
  dueDate?: string | null;
  ownerUserId?: string | null;
  notes?: string | null;
  sortOrder?: number;
}

export interface UpdateProjectObjectiveStatusRequest {
  status: ProjectObjectiveStatus;
}

// ----------------------------------------------------------------------
// §5 + §6 — المؤشّرات والقراءات
// ----------------------------------------------------------------------

export interface ProjectKpiDto {
  id: string;
  projectId: string;
  /** المؤشّر **تحت هدف حصرًا** (D-02) — لذا هذا الحقل غير اختياريّ. */
  objectiveId: string;
  name: string;
  description: string | null;
  category: ProjectKpiCategory;
  unit: ProjectKpiUnit;
  customUnitLabel: string | null;
  direction: ProjectKpiDirection;
  frequency: ProjectKpiFrequency;
  baselineValue: number | null;
  targetValue: number;
  currentValue: number | null;
  lastReadingDate: string | null;
  weight: number;
  sourceType: ProjectKpiSourceType;
  externalSourceKey: string | null;
  externalMetricCode: string | null;
  lastSyncedAtUtc: string | null;
  notes: string | null;
  sortOrder: number;
  isActive: boolean;
  achievementPercent: number | null;
  variance: number | null;
  trend: ProjectKpiTrend;
  ownerUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateProjectKpiRequest {
  name: string;
  targetValue: number;
  description?: string | null;
  category?: ProjectKpiCategory;
  unit?: ProjectKpiUnit;
  customUnitLabel?: string | null;
  direction?: ProjectKpiDirection;
  frequency?: ProjectKpiFrequency;
  baselineValue?: number | null;
  weight?: number;
  notes?: string | null;
  sortOrder?: number;
}

export interface UpdateProjectKpiRequest {
  name: string;
  targetValue: number;
  description?: string | null;
  category?: ProjectKpiCategory;
  unit?: ProjectKpiUnit;
  customUnitLabel?: string | null;
  direction?: ProjectKpiDirection;
  frequency?: ProjectKpiFrequency;
  baselineValue?: number | null;
  weight?: number;
  notes?: string | null;
  sortOrder?: number;
}

export interface ProjectKpiReadingDto {
  id: string;
  projectKpiId: string;
  readingDate: string;
  value: number;
  targetSnapshot: number | null;
  achievementSnapshot: number | null;
  recordedByUserId: string;
  recordedByFullName: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface CreateProjectKpiReadingRequest {
  readingDate: string;
  value: number;
  notes?: string | null;
}

export interface UpdateProjectKpiReadingRequest {
  value: number;
  notes?: string | null;
}

// ----------------------------------------------------------------------
// §11 — المخرَج التعاقديّ
//
// **ليس** `ProjectDeliverable` الخاصّ بالـWorkstream: هذا التزام تعاقديّ تجاه العميل
// محكوم بكتالوج `contract_deliverable`، والتسمية في الواجهة يجب أن تُبقي الفارق ظاهرًا.
// ----------------------------------------------------------------------

export interface ProjectContractDeliverableDto {
  id: string;
  projectId: string;
  objectiveId: string | null;
  workstreamId: string | null;
  deliverableTypeCode: string;
  deliverableTypeNameAr: string | null;
  name: string;
  description: string | null;
  plannedQuantity: number;
  completedQuantity: number;
  status: ProjectDeliverableStatus;
  progressPercent: number;
  startDate: string | null;
  dueDate: string | null;
  deliveredAtUtc: string | null;
  priority: DeliverablePriority;
  ownerUserId: string | null;
  ownerFullName: string | null;
  notes: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateProjectContractDeliverableRequest {
  deliverableTypeCode: string;
  name?: string | null;
  description?: string | null;
  objectiveId?: string | null;
  workstreamId?: string | null;
  plannedQuantity?: number;
  startDate?: string | null;
  dueDate?: string | null;
  priority?: DeliverablePriority;
  ownerUserId?: string | null;
  notes?: string | null;
  sortOrder?: number;
}

/** `deliverableTypeCode` **غائب عمدًا**: لقطة ثابتة لا تُعدَّل بعد الإنشاء. */
export interface UpdateProjectContractDeliverableRequest {
  name: string;
  description?: string | null;
  objectiveId?: string | null;
  workstreamId?: string | null;
  plannedQuantity?: number;
  startDate?: string | null;
  dueDate?: string | null;
  priority?: DeliverablePriority;
  ownerUserId?: string | null;
  notes?: string | null;
  sortOrder?: number;
}

export interface UpdateProjectContractDeliverableProgressRequest {
  status: ProjectDeliverableStatus;
  progressPercent: number;
  completedQuantity?: number | null;
  deliveredAtUtc?: string | null;
}

// ----------------------------------------------------------------------
// §14 — لوحة النظرة العامّة والصحّة
// ----------------------------------------------------------------------

/**
 * **العقد الرسميّ الوحيد للصحّة**. الواجهة تعرض هذه القيم كما هي:
 * ممنوع اشتقاق `score` أو `status` أو ترجمة الأوزان في React (FINDING-W6-03).
 *
 * `null` في أيّ مكوّن تعني **«مستبعَد من الاحتساب»** لا «صفر» — والفرق جوهريّ
 * لأنّ الخادم يعيد تطبيع الأوزان على المكوّنات المتاحة وحدها.
 *
 * `lastEvaluatedAtUtc` لحظة احتساب هذه اللقطة، بينما `persistedAtUtc` هو العمود
 * المخزَّن؛ و`null` فيه تعني «لم تُحتسَب الصحّة المخزَّنة بعد».
 */
export interface ProjectHealthDto {
  score: number | null;
  status: ProjectHealthStatus | null;
  kpiScore: number | null;
  progressPercent: number | null;
  scheduleScore: number | null;
  /** أسباب **مشتقّة ولا تُخزَّن** — تُعرَض من الاستجابة ولا تُعاد صياغتها في الواجهة. */
  reasonCodes: string[];
  lastEvaluatedAtUtc: string | null;
  persistedAtUtc: string | null;
}

export interface ProjectOverviewCoreDto {
  id: string;
  name: string;
  clientId: string;
  clientName: string;
  serviceType: ServiceType;
  status: ProjectStatus;
  startDate: string | null;
  endDate: string | null;
  progressPercent: number;
  summary: string | null;
  projectOwnerId: string | null;
  teamLeaderId: string | null;
  accountManagerId: string | null;
  ownerTeamId: string | null;
}

export interface ProjectOverviewObjectivesDto {
  total: number;
  notStarted: number;
  inProgress: number;
  atRisk: number;
  completed: number;
  averageAchievementPercent: number | null;
  items: ProjectObjectiveDto[];
}

export interface ProjectOverviewKpisDto {
  total: number;
  onTrack: number;
  atRisk: number;
  offTrack: number;
  withoutReadings: number;
  averageAchievementPercent: number | null;
  lastReadingDate: string | null;
}

export interface ProjectOverviewDeliverablesDto {
  total: number;
  completed: number;
  pending: number;
  delayed: number;
  overallProgressPercent: number | null;
}

export interface ProjectOverviewGovernanceDto {
  risksTotal: number;
  risksOpen: number;
  decisionsTotal: number;
  decisionsPending: number;
  notesTotal: number;
  notesOpen: number;
}

export interface ProjectOverviewStrategyDto {
  exists: boolean;
  filledCoreFields: number;
  totalCoreFields: number;
  attributesCount: number;
}

/**
 * حصيلة اللوحة في **نداء واحد**. التحميل الأوّل لمساحة العمل يستهلك هذا النداء وحده،
 * والتبويبات التفصيليّة تُحمَّل كسلًا بعده (§10).
 */
export interface ProjectOverviewDto {
  project: ProjectOverviewCoreDto;
  health: ProjectHealthDto;
  objectives: ProjectOverviewObjectivesDto;
  kpis: ProjectOverviewKpisDto;
  deliverables: ProjectOverviewDeliverablesDto;
  governance: ProjectOverviewGovernanceDto;
  strategy: ProjectOverviewStrategyDto;
}

// ----------------------------------------------------------------------
// W5 — قراءات الحوكمة المرشَّحة بالمشروع (قراءة فقط، صفر كتابة من هذا المسار)
// ----------------------------------------------------------------------

export interface ProjectRiskListItemDto {
  id: string;
  title: string;
  description: string | null;
  severity: RiskSeverity;
  status: RiskStatus;
  ownerId: string;
  ownerFullName: string | null;
  mitigationPlan: string | null;
  nextAction: string | null;
  closedAtUtc: string | null;
  createdAtUtc: string;
}

export interface ProjectDecisionListItemDto {
  id: string;
  title: string;
  description: string | null;
  status: DecisionStatus;
  madeById: string;
  madeByFullName: string | null;
  nextAction: string | null;
  decidedAtUtc: string | null;
  createdAtUtc: string;
}

export interface ProjectNoteListItemDto {
  id: string;
  noteType: ManagementNoteType;
  body: string;
  requiresAction: boolean;
  status: ManagementNoteStatus;
  authorId: string;
  authorFullName: string | null;
  resolvedAtUtc: string | null;
  createdAtUtc: string;
}

/** خيار كتالوج (نوع مخرَج تعاقديّ مثلًا) — تُبنى منه القوائم بلا تثبيت رموز. */
export interface ProjectCatalogOptionDto {
  code: string;
  nameAr: string;
  sortOrder: number;
}

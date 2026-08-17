// ======================================================================
// Project 360 — طبقة الوصول للبيانات (CPW-R3 · R2-W12)
//
// **قناة واحدة**: كلّ نداء يمرّ بـ`api` المركزيّ (الترويسة والأخطاء والتوكن هناك).
// لا `fetch` داخل مكوّن، ولا تكرار لنداء واحد في مكوّنين.
//
// **مفاتيح مستقرّة**: كلّ مفتاح يبدأ بـ`['project360', projectId, <المورد>, ...]`،
// فيكفي إبطال البادئة لإبطال المورد كلّه بلا تعداد المتغيّرات.
//
// **الصحّة تتغيّر ضمنًا**: أيّ طفرة تمسّ مدخلات `ProjectHealthPolicy` (هدف/مؤشّر/قراءة)
// يعيد الخادم حساب الصحّة معها في نفس وحدة العمل (FINDING-W6-03) ⟹ تُبطَل
// `overview` مع المورد المعدَّل، وإلّا عُرضت صحّة بائتة رغم صحّتها في الخادم.
// ======================================================================

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  CreateProjectContractDeliverableRequest,
  CreateProjectExecutionProposalRequest,
  ExecutionProposalStatus,
  ProjectExecutionProposalDto,
  ReviewProjectExecutionProposalRequest,
  CreateProjectKpiReadingRequest,
  CreateProjectKpiRequest,
  CreateProjectObjectiveRequest,
  ProjectCatalogOptionDto,
  ProjectContractDeliverableDto,
  ProjectDecisionListItemDto,
  ProjectHealthDto,
  ProjectKpiDto,
  ProjectKpiReadingDto,
  ProjectNoteListItemDto,
  ProjectObjectiveDto,
  ProjectOverviewDto,
  ProjectRiskListItemDto,
  ProjectStrategyDto,
  ProjectStrategySchemaDto,
  UpdateProjectContractDeliverableProgressRequest,
  UpdateProjectContractDeliverableRequest,
  UpdateProjectKpiReadingRequest,
  UpdateProjectKpiRequest,
  UpdateProjectObjectiveRequest,
  UpdateProjectObjectiveStatusRequest,
  UpsertProjectStrategyRequest,
} from '../types/project360';

const ROOT = 'project360';

type Resource =
  | 'overview'
  | 'objectives'
  | 'kpis'
  | 'readings'
  | 'strategy'
  | 'strategy-schema'
  | 'contract-deliverables'
  | 'deliverable-types'
  | 'risks'
  | 'decisions'
  | 'notes'
  | 'execution-proposals';

function key(projectId: string | undefined, resource: Resource, ...rest: unknown[]) {
  return [ROOT, projectId, resource, ...rest] as const;
}

function invalidateResource(qc: QueryClient, projectId: string, resource: Resource) {
  qc.invalidateQueries({ queryKey: [ROOT, projectId, resource] });
}

/**
 * إبطال اللوحة بعد طفرة تمسّ مدخلات الصحّة. مستقلّ عن إبطال المورد نفسه كي يبقى
 * الاستدعاء صريحًا في كلّ طفرة تستحقّه بدل إبطال شامل يُبطل تبويبات لم تتغيّر.
 */
function invalidateOverview(qc: QueryClient, projectId: string) {
  qc.invalidateQueries({ queryKey: [ROOT, projectId, 'overview'] });
}

// ----------------------------------------------------------------------
// النظرة العامّة والصحّة
// ----------------------------------------------------------------------

/** نداء التحميل الأوّل الوحيد لمساحة العمل (§10) — لا يُستدعى مرّتين في نفس الشاشة. */
export function useProjectOverview(projectId: string | undefined) {
  return useQuery({
    queryKey: key(projectId, 'overview'),
    enabled: !!projectId,
    queryFn: async () =>
      (await api.get<ProjectOverviewDto>(`/projects/${projectId}/overview`)).data,
  });
}

/**
 * إعادة احتساب الصحّة. الاستجابة هي عقد الصحّة الرسميّ نفسه، ومع ذلك تُبطَل اللوحة
 * لأنّ الأعمدة المخزَّنة تغيّرت فعلًا ولا يجوز أن تبقى نسخة الذاكرة سابقة لها.
 */
export function useRecomputeProjectHealth(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () =>
      (await api.post<ProjectHealthDto>(`/projects/${projectId}/health/recompute`)).data,
    onSuccess: () => invalidateOverview(qc, projectId),
  });
}

// ----------------------------------------------------------------------
// الأهداف
// ----------------------------------------------------------------------

export function useProjectObjectives(projectId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: key(projectId, 'objectives', includeInactive),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectObjectiveDto[]>(`/projects/${projectId}/objectives`, {
          params: { includeInactive },
        })
      ).data,
  });
}

export function useCreateProjectObjective(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateProjectObjectiveRequest) =>
      (await api.post<ProjectObjectiveDto>(`/projects/${projectId}/objectives`, req)).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useUpdateProjectObjective(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { objectiveId: string; request: UpdateProjectObjectiveRequest }) =>
      (
        await api.put<ProjectObjectiveDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useUpdateProjectObjectiveStatus(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      objectiveId: string;
      request: UpdateProjectObjectiveStatusRequest;
    }) =>
      (
        await api.patch<ProjectObjectiveDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/status`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

/** التفعيل/التعطيل يغيّر مجموعة المؤشّرات الداخلة في الاحتساب ⟹ يُبطل الصحّة أيضًا. */
export function useSetProjectObjectiveActive(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { objectiveId: string; isActive: boolean }) =>
      (
        await api.patch<ProjectObjectiveDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/${
            vars.isActive ? 'activate' : 'deactivate'
          }`,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'objectives');
      invalidateResource(qc, projectId, 'kpis');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useDeleteProjectObjective(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (objectiveId: string) => {
      await api.delete(`/projects/${projectId}/objectives/${objectiveId}`);
    },
    onSuccess: () => {
      invalidateResource(qc, projectId, 'objectives');
      invalidateResource(qc, projectId, 'kpis');
      invalidateOverview(qc, projectId);
    },
  });
}

// ----------------------------------------------------------------------
// المؤشّرات
//
// **لا مسار إنشاء على مستوى المشروع** (D-02): الإنشاء يمرّ بالهدف حصرًا، ولذلك
// `useCreateProjectKpi` يطلب `objectiveId` ولا يوجد بديل بدونه.
// ----------------------------------------------------------------------

export function useProjectKpis(projectId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: key(projectId, 'kpis', 'all', includeInactive),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectKpiDto[]>(`/projects/${projectId}/kpis`, {
          params: { includeInactive },
        })
      ).data,
  });
}

export function useObjectiveKpis(
  projectId: string | undefined,
  objectiveId: string | undefined,
  includeInactive = false,
) {
  return useQuery({
    queryKey: key(projectId, 'kpis', 'objective', objectiveId, includeInactive),
    enabled: !!projectId && !!objectiveId,
    queryFn: async () =>
      (
        await api.get<ProjectKpiDto[]>(
          `/projects/${projectId}/objectives/${objectiveId}/kpis`,
          { params: { includeInactive } },
        )
      ).data,
  });
}

export function useCreateProjectKpi(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { objectiveId: string; request: CreateProjectKpiRequest }) =>
      (
        await api.post<ProjectKpiDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/kpis`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'kpis');
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useUpdateProjectKpi(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      objectiveId: string;
      kpiId: string;
      request: UpdateProjectKpiRequest;
    }) =>
      (
        await api.put<ProjectKpiDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/kpis/${vars.kpiId}`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'kpis');
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useSetProjectKpiActive(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { objectiveId: string; kpiId: string; isActive: boolean }) =>
      (
        await api.patch<ProjectKpiDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/kpis/${vars.kpiId}/${
            vars.isActive ? 'activate' : 'deactivate'
          }`,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'kpis');
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

// ----------------------------------------------------------------------
// القراءات
// ----------------------------------------------------------------------

export function useKpiReadings(
  projectId: string | undefined,
  objectiveId: string | undefined,
  kpiId: string | undefined,
) {
  return useQuery({
    queryKey: key(projectId, 'readings', objectiveId, kpiId),
    enabled: !!projectId && !!objectiveId && !!kpiId,
    queryFn: async () =>
      (
        await api.get<ProjectKpiReadingDto[]>(
          `/projects/${projectId}/objectives/${objectiveId}/kpis/${kpiId}/readings`,
        )
      ).data,
  });
}

export function useCreateKpiReading(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      objectiveId: string;
      kpiId: string;
      request: CreateProjectKpiReadingRequest;
    }) =>
      (
        await api.post<ProjectKpiReadingDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/kpis/${vars.kpiId}/readings`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'readings');
      invalidateResource(qc, projectId, 'kpis');
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

/** التصحيح **بالتحديث** لا بقراءة ثانية لنفس التاريخ (عقد الخادم). */
export function useUpdateKpiReading(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      objectiveId: string;
      kpiId: string;
      readingId: string;
      request: UpdateProjectKpiReadingRequest;
    }) =>
      (
        await api.put<ProjectKpiReadingDto>(
          `/projects/${projectId}/objectives/${vars.objectiveId}/kpis/${vars.kpiId}/readings/${vars.readingId}`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'readings');
      invalidateResource(qc, projectId, 'kpis');
      invalidateResource(qc, projectId, 'objectives');
      invalidateOverview(qc, projectId);
    },
  });
}

// ----------------------------------------------------------------------
// الاستراتيجيّة
// ----------------------------------------------------------------------

export function useProjectStrategy(projectId: string | undefined) {
  return useQuery({
    queryKey: key(projectId, 'strategy'),
    enabled: !!projectId,
    queryFn: async () =>
      (await api.get<ProjectStrategyDto | null>(`/projects/${projectId}/strategy`)).data,
  });
}

/** المخطَّط يبني النموذج — ولا يُخزَّن في حالة محلّيّة كي لا تتفرّع نسخة من الكتالوج. */
export function useProjectStrategySchema(projectId: string | undefined) {
  return useQuery({
    queryKey: key(projectId, 'strategy-schema'),
    enabled: !!projectId,
    queryFn: async () =>
      (await api.get<ProjectStrategySchemaDto>(`/projects/${projectId}/strategy/schema`)).data,
  });
}

export function useUpsertProjectStrategy(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: UpsertProjectStrategyRequest) =>
      (await api.put<ProjectStrategyDto>(`/projects/${projectId}/strategy`, req)).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'strategy');
      invalidateOverview(qc, projectId);
    },
  });
}

// ----------------------------------------------------------------------
// المخرَجات التعاقديّة
// ----------------------------------------------------------------------

export function useProjectContractDeliverables(
  projectId: string | undefined,
  includeInactive = false,
) {
  return useQuery({
    queryKey: key(projectId, 'contract-deliverables', includeInactive),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectContractDeliverableDto[]>(
          `/projects/${projectId}/contract-deliverables`,
          { params: { includeInactive } },
        )
      ).data,
  });
}

export function useContractDeliverableTypes(projectId: string | undefined) {
  return useQuery({
    queryKey: key(projectId, 'deliverable-types'),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectCatalogOptionDto[]>(
          `/projects/${projectId}/contract-deliverables/types`,
        )
      ).data,
  });
}

export function useCreateContractDeliverable(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateProjectContractDeliverableRequest) =>
      (
        await api.post<ProjectContractDeliverableDto>(
          `/projects/${projectId}/contract-deliverables`,
          req,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'contract-deliverables');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useUpdateContractDeliverable(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      deliverableId: string;
      request: UpdateProjectContractDeliverableRequest;
    }) =>
      (
        await api.put<ProjectContractDeliverableDto>(
          `/projects/${projectId}/contract-deliverables/${vars.deliverableId}`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'contract-deliverables');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useUpdateContractDeliverableProgress(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      deliverableId: string;
      request: UpdateProjectContractDeliverableProgressRequest;
    }) =>
      (
        await api.patch<ProjectContractDeliverableDto>(
          `/projects/${projectId}/contract-deliverables/${vars.deliverableId}/progress`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'contract-deliverables');
      invalidateOverview(qc, projectId);
    },
  });
}

export function useSetContractDeliverableActive(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { deliverableId: string; isActive: boolean }) =>
      (
        await api.patch<ProjectContractDeliverableDto>(
          `/projects/${projectId}/contract-deliverables/${vars.deliverableId}/${
            vars.isActive ? 'activate' : 'deactivate'
          }`,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'contract-deliverables');
      invalidateOverview(qc, projectId);
    },
  });
}

// ----------------------------------------------------------------------
// الحوكمة — قراءة فقط (لا طفرة من هذا المسار إطلاقًا)
// ----------------------------------------------------------------------

export function useProjectRisks(projectId: string | undefined, openOnly = false) {
  return useQuery({
    queryKey: key(projectId, 'risks', openOnly),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectRiskListItemDto[]>(`/projects/${projectId}/risks`, {
          params: { openOnly },
        })
      ).data,
  });
}

export function useProjectDecisions(projectId: string | undefined, pendingOnly = false) {
  return useQuery({
    queryKey: key(projectId, 'decisions', pendingOnly),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectDecisionListItemDto[]>(`/projects/${projectId}/decisions`, {
          params: { pendingOnly },
        })
      ).data,
  });
}

export function useProjectNotes(projectId: string | undefined, openOnly = false) {
  return useQuery({
    queryKey: key(projectId, 'notes', openOnly),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectNoteListItemDto[]>(`/projects/${projectId}/notes`, {
          params: { openOnly },
        })
      ).data,
  });
}

// ----------------------------------------------------------------------
// §10 — جسر التنفيذ الهجين
// ----------------------------------------------------------------------

export function useProjectExecutionProposals(
  projectId: string | undefined,
  status?: ExecutionProposalStatus,
) {
  return useQuery({
    queryKey: key(projectId, 'execution-proposals', status ?? 'all'),
    enabled: !!projectId,
    queryFn: async () =>
      (
        await api.get<ProjectExecutionProposalDto[]>(
          `/projects/${projectId}/execution-proposals`,
          { params: status ? { status } : undefined },
        )
      ).data,
  });
}

/**
 * رفع الادّعاء **لا يُبطِل اللوحة**: المقترح المعلَّق لا يمسّ رقمًا من أرقام المشروع،
 * وإبطالها هنا كان سيوحي للمستخدم بأنّ ادّعاءه أثّر قبل أن يُقَرّ.
 */
export function useCreateProjectExecutionProposal(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateProjectExecutionProposalRequest) =>
      (
        await api.post<ProjectExecutionProposalDto>(
          `/projects/${projectId}/execution-proposals`,
          req,
        )
      ).data,
    onSuccess: () => invalidateResource(qc, projectId, 'execution-proposals'),
  });
}

/**
 * الحسم يُبطِل ثلاثة: المقترحات، والمخرَجات (القبول يكتب على المخرَج)، واللوحة (ومنها
 * التقدّم والصحّة). إبطال المقترحات وحدها كان سيترك المخرَج معروضًا بنسبته القديمة
 * بعد أن غيّرها القبول فعلًا في الخادم.
 */
export function useReviewProjectExecutionProposal(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      proposalId: string;
      request: ReviewProjectExecutionProposalRequest;
    }) =>
      (
        await api.patch<ProjectExecutionProposalDto>(
          `/projects/${projectId}/execution-proposals/${vars.proposalId}/review`,
          vars.request,
        )
      ).data,
    onSuccess: () => {
      invalidateResource(qc, projectId, 'execution-proposals');
      invalidateResource(qc, projectId, 'contract-deliverables');
      invalidateOverview(qc, projectId);
    },
  });
}

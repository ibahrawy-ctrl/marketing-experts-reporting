import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  ClientDto,
  CreateClientRequest,
  UpdateClientRequest,
  ClientHealthReport,
  ProjectDto,
  CreateProjectRequest,
  UpdateProjectRequest,
  ProjectSummaryDto,
  LinkedReportRow,
  ClientStatus,
  ProjectStatus,
  ServiceType,
} from '../types/api';

// ===== فلاتر القوائم =====
export interface ClientsFilter {
  status?: ClientStatus;
  accountManagerId?: string;
  includeClosed?: boolean;
}
export interface ProjectsFilter {
  clientId?: string;
  status?: ProjectStatus;
  serviceType?: ServiceType;
  ownerTeamId?: string;
  accountManagerId?: string;
  includeClosed?: boolean;
}

// ===== العملاء =====
export function useClients(filter: ClientsFilter = {}) {
  return useQuery({
    queryKey: ['clients', filter],
    queryFn: async () => (await api.get<ClientDto[]>('/clients', { params: filter })).data,
  });
}

export function useClientHealth() {
  return useQuery({
    queryKey: ['client-health'],
    queryFn: async () => (await api.get<ClientHealthReport>('/clients/health')).data,
  });
}

export function useClient(id: string | undefined) {
  return useQuery({
    queryKey: ['client', id],
    queryFn: async () => (await api.get<ClientDto>(`/clients/${id}`)).data,
    enabled: !!id,
  });
}

export function useClientReports(id: string | undefined) {
  return useQuery({
    queryKey: ['client-reports', id],
    queryFn: async () => (await api.get<LinkedReportRow[]>(`/clients/${id}/reports`)).data,
    enabled: !!id,
  });
}

function invalidateClients(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['clients'] });
  qc.invalidateQueries({ queryKey: ['client'] });
  qc.invalidateQueries({ queryKey: ['client-health'] });
}

export function useCreateClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateClientRequest) =>
      (await api.post<ClientDto>('/clients', req)).data,
    onSuccess: () => invalidateClients(qc),
  });
}

export function useUpdateClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateClientRequest }) =>
      (await api.put<ClientDto>(`/clients/${id}`, req)).data,
    onSuccess: () => invalidateClients(qc),
  });
}

export function useArchiveClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post(`/clients/${id}/archive`)).data,
    onSuccess: () => invalidateClients(qc),
  });
}

// ===== المشاريع =====
export function useProjects(filter: ProjectsFilter = {}) {
  return useQuery({
    queryKey: ['projects', filter],
    queryFn: async () => (await api.get<ProjectDto[]>('/projects', { params: filter })).data,
  });
}

export function useProject(id: string | undefined) {
  return useQuery({
    queryKey: ['project', id],
    queryFn: async () => (await api.get<ProjectDto>(`/projects/${id}`)).data,
    enabled: !!id,
  });
}

export function useProjectReports(id: string | undefined) {
  return useQuery({
    queryKey: ['project-reports', id],
    queryFn: async () => (await api.get<LinkedReportRow[]>(`/projects/${id}/reports`)).data,
    enabled: !!id,
  });
}

export function useProjectSummary(id: string | undefined) {
  return useQuery({
    queryKey: ['project-summary', id],
    queryFn: async () => (await api.get<ProjectSummaryDto>(`/projects/${id}/summary`)).data,
    enabled: !!id,
  });
}

function invalidateProjects(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['projects'] });
  qc.invalidateQueries({ queryKey: ['project'] });
  qc.invalidateQueries({ queryKey: ['project-summary'] });
  qc.invalidateQueries({ queryKey: ['clients'] });
  qc.invalidateQueries({ queryKey: ['client'] });
  qc.invalidateQueries({ queryKey: ['client-health'] });
}

export function useCreateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateProjectRequest) =>
      (await api.post<ProjectDto>('/projects', req)).data,
    onSuccess: () => invalidateProjects(qc),
  });
}

export function useUpdateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateProjectRequest }) =>
      (await api.put<ProjectDto>(`/projects/${id}`, req)).data,
    onSuccess: () => invalidateProjects(qc),
  });
}

export function useArchiveProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post(`/projects/${id}/archive`)).data,
    onSuccess: () => invalidateProjects(qc),
  });
}

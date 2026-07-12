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
  ClientContactDto,
  CreateClientContactRequest,
  UpdateClientContactRequest,
  ClientDigitalChannelDto,
  CreateClientDigitalChannelRequest,
  UpdateClientDigitalChannelRequest,
  ClientBrandProfileDto,
  UpsertClientBrandProfileRequest,
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
  selectableOnly?: boolean;
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

export function useReactivateClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post<ClientDto>(`/clients/${id}/reactivate`)).data,
    onSuccess: () => invalidateClients(qc),
  });
}

export function useDeleteClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.delete(`/clients/${id}`)).data,
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

export function useReactivateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post<ProjectDto>(`/projects/${id}/reactivate`)).data,
    onSuccess: () => invalidateProjects(qc),
  });
}

export function useDeleteProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.delete(`/projects/${id}`)).data,
    onSuccess: () => invalidateProjects(qc),
  });
}

// ===== Client 360 — جهات الاتصال (CPW-R1B) =====
export function useClientContacts(clientId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: ['client-contacts', clientId, includeInactive],
    queryFn: async () =>
      (await api.get<ClientContactDto[]>(`/clients/${clientId}/contacts`, {
        params: { includeInactive },
      })).data,
    enabled: !!clientId,
  });
}

function invalidateContacts(qc: ReturnType<typeof useQueryClient>, clientId: string) {
  qc.invalidateQueries({ queryKey: ['client-contacts', clientId] });
}

export function useCreateClientContact(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateClientContactRequest) =>
      (await api.post<ClientContactDto>(`/clients/${clientId}/contacts`, req)).data,
    onSuccess: () => invalidateContacts(qc, clientId),
  });
}

export function useUpdateClientContact(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateClientContactRequest }) =>
      (await api.put<ClientContactDto>(`/clients/${clientId}/contacts/${id}`, req)).data,
    onSuccess: () => invalidateContacts(qc, clientId),
  });
}

export function useSetClientContactActive(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) =>
      (await api.patch<ClientContactDto>(
        `/clients/${clientId}/contacts/${id}/${active ? 'activate' : 'deactivate'}`,
      )).data,
    onSuccess: () => invalidateContacts(qc, clientId),
  });
}

// ===== Client 360 — القنوات الرقمية (CPW-R1B) =====
export function useClientDigitalChannels(clientId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: ['client-channels', clientId, includeInactive],
    queryFn: async () =>
      (await api.get<ClientDigitalChannelDto[]>(`/clients/${clientId}/digital-channels`, {
        params: { includeInactive },
      })).data,
    enabled: !!clientId,
  });
}

function invalidateChannels(qc: ReturnType<typeof useQueryClient>, clientId: string) {
  qc.invalidateQueries({ queryKey: ['client-channels', clientId] });
}

export function useCreateClientDigitalChannel(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateClientDigitalChannelRequest) =>
      (await api.post<ClientDigitalChannelDto>(`/clients/${clientId}/digital-channels`, req)).data,
    onSuccess: () => invalidateChannels(qc, clientId),
  });
}

export function useUpdateClientDigitalChannel(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateClientDigitalChannelRequest }) =>
      (await api.put<ClientDigitalChannelDto>(`/clients/${clientId}/digital-channels/${id}`, req))
        .data,
    onSuccess: () => invalidateChannels(qc, clientId),
  });
}

export function useSetClientDigitalChannelActive(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) =>
      (await api.patch<ClientDigitalChannelDto>(
        `/clients/${clientId}/digital-channels/${id}/${active ? 'activate' : 'deactivate'}`,
      )).data,
    onSuccess: () => invalidateChannels(qc, clientId),
  });
}

// ===== Client 360 — ملفّ البراند (CPW-R1B) — علاقة 1:0..1 =====
export function useClientBrand(clientId: string | undefined) {
  return useQuery({
    queryKey: ['client-brand', clientId],
    queryFn: async () =>
      (await api.get<ClientBrandProfileDto | null>(`/clients/${clientId}/brand`)).data,
    enabled: !!clientId,
  });
}

export function useUpsertClientBrand(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: UpsertClientBrandProfileRequest) =>
      (await api.put<ClientBrandProfileDto>(`/clients/${clientId}/brand`, req)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['client-brand', clientId] }),
  });
}

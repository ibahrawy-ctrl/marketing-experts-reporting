import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  PortfolioProjectDto,
  PortfolioClientDto,
  PortfolioClientDetailDto,
  PortfolioOutputDto,
} from '../types/api';

// محفظة مدير الحساب (مشاريعي/عملائي) — عرض فقط. النطاق مفروض خادمًا على مشاريع المستخدم نفسه.
export function useMyPortfolioProjects() {
  return useQuery({
    queryKey: ['account-portfolio', 'projects'],
    queryFn: async () => (await api.get<PortfolioProjectDto[]>('/account-portfolio/projects')).data,
  });
}

export function useMyPortfolioProject(id: string | undefined) {
  return useQuery({
    queryKey: ['account-portfolio', 'project', id],
    enabled: !!id,
    queryFn: async () => (await api.get<PortfolioProjectDto>(`/account-portfolio/projects/${id}`)).data,
  });
}

export function useMyPortfolioClients() {
  return useQuery({
    queryKey: ['account-portfolio', 'clients'],
    queryFn: async () => (await api.get<PortfolioClientDto[]>('/account-portfolio/clients')).data,
  });
}

export function useMyPortfolioClient(id: string | undefined) {
  return useQuery({
    queryKey: ['account-portfolio', 'client', id],
    enabled: !!id,
    queryFn: async () =>
      (await api.get<PortfolioClientDetailDto>(`/account-portfolio/clients/${id}`)).data,
  });
}

export function useMyPortfolioProjectOutputs(projectId: string | undefined) {
  return useQuery({
    queryKey: ['account-portfolio', 'outputs', projectId],
    enabled: !!projectId,
    queryFn: async () =>
      (await api.get<PortfolioOutputDto[]>(`/account-portfolio/projects/${projectId}/outputs`)).data,
  });
}

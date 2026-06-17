import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  DirectoryUserDto,
  DepartmentDto,
  TeamDto,
  JobRoleDto,
  RoleAccessDto,
  Role,
  CreateUserRequest,
  UpdateUserRequest,
  CreateTeamRequest,
  UpdateTeamRequest,
  CreateDepartmentRequest,
  UpdateDepartmentRequest,
} from '../types/api';

export function useDirectoryUsers(includeInactive = false) {
  return useQuery({
    queryKey: ['directory-users', includeInactive],
    queryFn: async () =>
      (await api.get<DirectoryUserDto[]>('/directory/users', { params: { includeInactive } })).data,
  });
}

export function useDepartments() {
  return useQuery({
    queryKey: ['directory-departments'],
    queryFn: async () => (await api.get<DepartmentDto[]>('/directory/departments')).data,
  });
}

export function useTeams() {
  return useQuery({
    queryKey: ['directory-teams'],
    queryFn: async () => (await api.get<TeamDto[]>('/directory/teams')).data,
  });
}

export function useJobRoles() {
  return useQuery({
    queryKey: ['directory-job-roles'],
    queryFn: async () => (await api.get<JobRoleDto[]>('/directory/job-roles')).data,
  });
}

export function useRoleMatrix() {
  return useQuery({
    queryKey: ['directory-role-matrix'],
    queryFn: async () => (await api.get<RoleAccessDto[]>('/directory/role-matrix')).data,
  });
}

export function useUpdateUserRoles() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, roles }: { userId: string; roles: Role[] }) =>
      (await api.put(`/directory/users/${userId}/roles`, { roles })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['directory-users'] });
    },
  });
}

function invalidateDirectory(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['directory-users'] });
  qc.invalidateQueries({ queryKey: ['directory-teams'] });
  qc.invalidateQueries({ queryKey: ['directory-departments'] });
}

export function useCreateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateUserRequest) =>
      (await api.post<DirectoryUserDto>('/directory/users', req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useUpdateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, req }: { userId: string; req: UpdateUserRequest }) =>
      (await api.put<DirectoryUserDto>(`/directory/users/${userId}`, req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useDeleteUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (userId: string) => (await api.delete(`/directory/users/${userId}`)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useAddTeamMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ teamId, userId }: { teamId: string; userId: string }) =>
      (await api.post(`/directory/teams/${teamId}/members`, { userId })).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useRemoveTeamMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ teamId, userId }: { teamId: string; userId: string }) =>
      (await api.delete(`/directory/teams/${teamId}/members/${userId}`)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useCreateTeam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateTeamRequest) =>
      (await api.post<TeamDto>('/directory/teams', req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useUpdateTeam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ teamId, req }: { teamId: string; req: UpdateTeamRequest }) =>
      (await api.put<TeamDto>(`/directory/teams/${teamId}`, req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useDeleteTeam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (teamId: string) => (await api.delete(`/directory/teams/${teamId}`)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useCreateDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateDepartmentRequest) =>
      (await api.post<DepartmentDto>('/directory/departments', req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useUpdateDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ departmentId, req }: { departmentId: string; req: UpdateDepartmentRequest }) =>
      (await api.put<DepartmentDto>(`/directory/departments/${departmentId}`, req)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

export function useDeleteDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (departmentId: string) =>
      (await api.delete(`/directory/departments/${departmentId}`)).data,
    onSuccess: () => invalidateDirectory(qc),
  });
}

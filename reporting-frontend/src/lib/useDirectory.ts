import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  DirectoryUserDto,
  HrDirectoryUserDto,
  DepartmentDto,
  TeamDto,
  JobRoleDto,
  JobRoleDetailDto,
  CreateJobRoleRequest,
  UpdateJobRoleRequest,
  RoleAccessDto,
  Role,
  CreateUserRequest,
  UpdateUserRequest,
  UpdateUserJobRoleRequest,
  UpdateUserBasicRequest,
  UpdateUserEmploymentWindowRequest,
  UpdateUserOrgAssignmentRequest,
  CreateTeamRequest,
  UpdateTeamRequest,
  CreateDepartmentRequest,
  UpdateDepartmentRequest,
  TeamSummaryDto,
  DepartmentSummaryDto,
  TeamMoveImpactDto,
  TeamMembershipsDto,
  TeamMemberDto,
  UserTeamMembershipsDto,
  AddAdditionalMemberRequest,
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

// ===== ملخّصات الهيكل التنظيمي (ORG-STRUCTURE-ADMIN-R1) — قراءة فقط مع عدّادات، تخضع لفلترة النطاق =====
export function useTeamSummaries() {
  return useQuery({
    queryKey: ['directory-teams-summary'],
    queryFn: async () => (await api.get<TeamSummaryDto[]>('/directory/teams/summary')).data,
  });
}

export function useDepartmentSummaries() {
  return useQuery({
    queryKey: ['directory-departments-summary'],
    queryFn: async () => (await api.get<DepartmentSummaryDto[]>('/directory/departments/summary')).data,
  });
}

// ملخّص أثر نقل فريق إلى إدارة مستهدفة — يُجلب عند الطلب فقط (enabled) قبل الحفظ.
export function useTeamMoveImpact(teamId: string | null, targetDepartmentId: string | null) {
  return useQuery({
    queryKey: ['directory-team-move-impact', teamId, targetDepartmentId],
    enabled: !!teamId && !!targetDepartmentId,
    queryFn: async () =>
      (await api.get<TeamMoveImpactDto>(`/directory/teams/${teamId}/move-impact`, {
        params: { targetDepartmentId },
      })).data,
  });
}

// ===== دليل الموارد البشرية المخصّص (قراءة فقط لحزمة HR A) — منفصل عن الدليل العام، محكوم بسياسة HrDirectoryRead =====
export function useHrDirectoryUsers(includeInactive = false) {
  return useQuery({
    queryKey: ['hr-directory-users', includeInactive],
    queryFn: async () =>
      (await api.get<HrDirectoryUserDto[]>('/directory/hr/users', { params: { includeInactive } })).data,
  });
}

export function useHrDirectoryDepartments() {
  return useQuery({
    queryKey: ['hr-directory-departments'],
    queryFn: async () => (await api.get<DepartmentDto[]>('/directory/hr/departments')).data,
  });
}

export function useHrDirectoryTeams() {
  return useQuery({
    queryKey: ['hr-directory-teams'],
    queryFn: async () => (await api.get<TeamDto[]>('/directory/hr/teams')).data,
  });
}

// المديرون المتاحون (النشطون فقط) لمنتقي المدير في النقل التنظيمي — استبعاد الذات/الدائرية مفروض خادمًا عند الحفظ.
export function useHrDirectoryManagers() {
  return useQuery({
    queryKey: ['hr-directory-managers'],
    queryFn: async () => (await api.get<HrDirectoryUserDto[]>('/directory/hr/managers')).data,
  });
}

export function useJobRoles(activeOnly = false) {
  return useQuery({
    queryKey: ['directory-job-roles', activeOnly],
    queryFn: async () =>
      (await api.get<JobRoleDto[]>('/directory/job-roles', { params: activeOnly ? { activeOnly: true } : {} })).data,
  });
}

// قائمة المسمّيات الوظيفية مع عدّاد الموظفين/القوالب واسم الإدارة — لشاشة الإدارة.
export function useJobRolesManage() {
  return useQuery({
    queryKey: ['directory-job-roles-manage'],
    queryFn: async () => (await api.get<JobRoleDetailDto[]>('/directory/job-roles/manage')).data,
  });
}

function invalidateJobRoles(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['directory-job-roles'] });
  qc.invalidateQueries({ queryKey: ['directory-job-roles-manage'] });
}

export function useCreateJobRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateJobRoleRequest) =>
      (await api.post<JobRoleDetailDto>('/directory/job-roles', req)).data,
    onSuccess: () => invalidateJobRoles(qc),
  });
}

export function useUpdateJobRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ jobRoleId, req }: { jobRoleId: string; req: UpdateJobRoleRequest }) =>
      (await api.put<JobRoleDetailDto>(`/directory/job-roles/${jobRoleId}`, req)).data,
    onSuccess: () => invalidateJobRoles(qc),
  });
}

export function useArchiveJobRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (jobRoleId: string) =>
      (await api.post<JobRoleDetailDto>(`/directory/job-roles/${jobRoleId}/archive`, {})).data,
    onSuccess: () => invalidateJobRoles(qc),
  });
}

export function useReactivateJobRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (jobRoleId: string) =>
      (await api.post<JobRoleDetailDto>(`/directory/job-roles/${jobRoleId}/reactivate`, {})).data,
    onSuccess: () => invalidateJobRoles(qc),
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
  qc.invalidateQueries({ queryKey: ['directory-teams-summary'] });
  qc.invalidateQueries({ queryKey: ['directory-departments-summary'] });
  qc.invalidateQueries({ queryKey: ['hr-directory-users'] });
  qc.invalidateQueries({ queryKey: ['hr-directory-managers'] });
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

// تعديل المسمّى الوظيفي للموظف فقط — السطح المخصّص (PATCH). لا يمسّ أي حقل آخر.
export function useUpdateUserJobRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, req }: { userId: string; req: UpdateUserJobRoleRequest }) =>
      (await api.patch<DirectoryUserDto>(`/directory/users/${userId}/job-role`, req)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['directory-users'] }),
  });
}

// تعديل البيانات الأساسية للموظف (الاسم فقط) — حزمة HR A (PATCH). لا يمسّ البريد/الأدوار/كلمة المرور/التفعيل.
export function useUpdateUserBasic() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, req }: { userId: string; req: UpdateUserBasicRequest }) =>
      (await api.patch<DirectoryUserDto>(`/directory/users/${userId}/basic`, req)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['directory-users'] });
      qc.invalidateQueries({ queryKey: ['hr-directory-users'] });
      qc.invalidateQueries({ queryKey: ['hr-directory-managers'] });
    },
  });
}

// DEF-R5-002 — نافذة خدمة الموظّف (الالتحاق/انتهاء الخدمة) على سطح إدارة الموظّف نفسه (PATCH).
// لا تُعيد كتابة أيّ تقييم تاريخيّ؛ أثرها حسابيّ عند كلّ فترة تُطلَب ⇒ نُبطِل مؤشّرات الأداء كذلك.
export function useUpdateUserEmploymentWindow() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, req }: { userId: string; req: UpdateUserEmploymentWindowRequest }) =>
      (await api.patch<DirectoryUserDto>(`/directory/users/${userId}/employment-window`, req)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['hr-directory-users'] });
      qc.invalidateQueries({ queryKey: ['directory-users'] });
      qc.invalidateQueries({ queryKey: ['kpi-performance'] });
    },
  });
}

// تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) — حزمة HR A (PATCH). القيود مفروضة خادمًا.
export function useUpdateUserOrgAssignment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, req }: { userId: string; req: UpdateUserOrgAssignmentRequest }) =>
      (await api.patch<DirectoryUserDto>(`/directory/users/${userId}/org-assignment`, req)).data,
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

export function useResetUserPassword() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, newPassword }: { userId: string; newPassword: string }) =>
      (await api.post(`/directory/users/${userId}/reset-password`, { newPassword })).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['directory-users'] }),
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

// ===== عضويات الفريق المتعددة (MULTI-TEAM-MEMBERSHIP-MVP-R1) — Admin فقط، منفصلة عن عضوية الفريق الأساسية =====
export function useTeamMemberships(teamId: string | null) {
  return useQuery({
    queryKey: ['team-memberships', teamId],
    enabled: !!teamId,
    queryFn: async () =>
      (await api.get<TeamMembershipsDto>(`/directory/teams/${teamId}/memberships`)).data,
  });
}

function invalidateMemberships(qc: ReturnType<typeof useQueryClient>, teamId: string) {
  qc.invalidateQueries({ queryKey: ['team-memberships', teamId] });
  qc.invalidateQueries({ queryKey: ['user-team-memberships'] });
  qc.invalidateQueries({ queryKey: ['directory-teams-summary'] });
  qc.invalidateQueries({ queryKey: ['directory-departments-summary'] });
}

export function useAddAdditionalTeamMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ teamId, req }: { teamId: string; req: AddAdditionalMemberRequest }) =>
      (await api.post<TeamMemberDto>(`/directory/teams/${teamId}/additional-members`, req)).data,
    onSuccess: (_d, vars) => invalidateMemberships(qc, vars.teamId),
  });
}

export function useRemoveAdditionalTeamMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ teamId, userId }: { teamId: string; userId: string }) =>
      (await api.delete(`/directory/teams/${teamId}/additional-members/${userId}`)).data,
    onSuccess: (_d, vars) => invalidateMemberships(qc, vars.teamId),
  });
}

export function useUserTeamMemberships(userId: string | null) {
  return useQuery({
    queryKey: ['user-team-memberships', userId],
    enabled: !!userId,
    queryFn: async () =>
      (await api.get<UserTeamMembershipsDto>(`/directory/users/${userId}/team-memberships`)).data,
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

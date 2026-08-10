import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, downloadFile } from './api';
import type {
  ArchiveClientDocumentRequest,
  ClientDocumentDetailDto,
  ClientDocumentDto,
  ClientDocumentsFilter,
  ClientExternalLinkDto,
  ClientStorageUsageDto,
  CreateClientExternalLinkRequest,
  DeleteClientDocumentRequest,
  DocumentVisibilityType,
  UpdateClientDocumentRequest,
  UpdateClientExternalLinkRequest,
} from '../types/api';

// ===== مستندات العميل (CPW-R1B2) =====
// ملاحظة أمنيّة: التنزيل يمرّ حصرًا عبر نقطة نهاية مصادَقة (C-02) — لا روابط موقَّعة ولا مسار تخزين في الواجهة.

function invalidateDocuments(qc: ReturnType<typeof useQueryClient>, clientId: string) {
  qc.invalidateQueries({ queryKey: ['client-documents', clientId] });
  qc.invalidateQueries({ queryKey: ['client-document', clientId] });
  qc.invalidateQueries({ queryKey: ['client-storage-usage', clientId] });
}

export function useClientDocuments(clientId: string | undefined, filter: ClientDocumentsFilter = {}) {
  return useQuery({
    queryKey: ['client-documents', clientId, filter],
    queryFn: async () =>
      (await api.get<ClientDocumentDto[]>(`/clients/${clientId}/documents`, { params: filter })).data,
    enabled: !!clientId,
  });
}

export function useClientDocument(clientId: string | undefined, documentId: string | undefined) {
  return useQuery({
    queryKey: ['client-document', clientId, documentId],
    queryFn: async () =>
      (await api.get<ClientDocumentDetailDto>(`/clients/${clientId}/documents/${documentId}`)).data,
    enabled: !!clientId && !!documentId,
  });
}

export function useClientStorageUsage(clientId: string | undefined) {
  return useQuery({
    queryKey: ['client-storage-usage', clientId],
    queryFn: async () =>
      (await api.get<ClientStorageUsageDto>(`/clients/${clientId}/documents/storage-usage`)).data,
    enabled: !!clientId,
  });
}

/// حمولة رفع مستند جديد — الملفّ إلزاميّ والبقيّة بيانات وصفيّة.
export interface UploadClientDocumentInput {
  file: File;
  title: string;
  categoryCode: string;
  description?: string;
  tags?: string;
  confidentialityCode?: string;
  changeNote?: string;
  // سياسة الرؤية (CPW-R2). عند الإغفال يطبّق الخادم افتراضيّ التصنيف.
  visibilityType?: DocumentVisibilityType;
  allowedRoles?: string[];
  allowedUserIds?: string[];
}

function buildUploadForm(input: UploadClientDocumentInput): FormData {
  const form = new FormData();
  form.append('File', input.file);
  form.append('Title', input.title);
  form.append('CategoryCode', input.categoryCode);
  if (input.description) form.append('Description', input.description);
  if (input.tags) form.append('Tags', input.tags);
  if (input.confidentialityCode) form.append('ConfidentialityCode', input.confidentialityCode);
  if (input.changeNote) form.append('ChangeNote', input.changeNote);
  if (input.visibilityType) form.append('VisibilityType', input.visibilityType);
  // ربط القوائم في multipart يتمّ بتكرار المفتاح نفسه (List<string>/List<Guid>).
  for (const role of input.allowedRoles ?? []) form.append('AllowedRoles', role);
  for (const uid of input.allowedUserIds ?? []) form.append('AllowedUserIds', uid);
  return form;
}

// النسخة الافتراضيّة من axios تضع application/json ⇒ يجب إسقاط الترويسة ليضبط المتصفّح حدّ multipart.
const MULTIPART = { headers: { 'Content-Type': 'multipart/form-data' } } as const;

export function useUploadClientDocument(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: UploadClientDocumentInput) =>
      (await api.post<ClientDocumentDto>(
        `/clients/${clientId}/documents`,
        buildUploadForm(input),
        MULTIPART,
      )).data,
    onSuccess: () => invalidateDocuments(qc, clientId),
  });
}

export function useAddClientDocumentVersion(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ documentId, file, changeNote }: { documentId: string; file: File; changeNote?: string }) => {
      const form = new FormData();
      form.append('File', file);
      if (changeNote) form.append('ChangeNote', changeNote);
      return (await api.post<ClientDocumentDto>(
        `/clients/${clientId}/documents/${documentId}/versions`,
        form,
        MULTIPART,
      )).data;
    },
    onSuccess: () => invalidateDocuments(qc, clientId),
  });
}

export function useUpdateClientDocument(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ documentId, req }: { documentId: string; req: UpdateClientDocumentRequest }) =>
      (await api.put<ClientDocumentDto>(`/clients/${clientId}/documents/${documentId}`, req)).data,
    onSuccess: () => invalidateDocuments(qc, clientId),
  });
}

export function useSetClientDocumentArchived(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      documentId,
      archived,
      req,
    }: {
      documentId: string;
      archived: boolean;
      req: ArchiveClientDocumentRequest;
    }) =>
      (await api.patch<ClientDocumentDto>(
        `/clients/${clientId}/documents/${documentId}/${archived ? 'archive' : 'unarchive'}`,
        req,
      )).data,
    onSuccess: () => invalidateDocuments(qc, clientId),
  });
}

/// حذف منطقيّ (Tombstone) بسبب إلزاميّ — لا حذف نهائيّ للصفّ ولا للملفّ.
export function useDeleteClientDocument(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ documentId, req }: { documentId: string; req: DeleteClientDocumentRequest }) =>
      (await api.post(`/clients/${clientId}/documents/${documentId}/delete`, req)).data,
    onSuccess: () => invalidateDocuments(qc, clientId),
  });
}

export function downloadClientDocument(clientId: string, documentId: string, fileName: string) {
  return downloadFile(`/clients/${clientId}/documents/${documentId}/download`, fileName);
}

export function downloadClientDocumentVersion(
  clientId: string,
  documentId: string,
  versionId: string,
  fileName: string,
) {
  return downloadFile(
    `/clients/${clientId}/documents/${documentId}/versions/${versionId}/download`,
    fileName,
  );
}

// ===== الروابط المهمّة (CPW-R1B2) =====

function invalidateLinks(qc: ReturnType<typeof useQueryClient>, clientId: string) {
  qc.invalidateQueries({ queryKey: ['client-links', clientId] });
}

export function useClientExternalLinks(clientId: string | undefined, includeInactive = false) {
  return useQuery({
    queryKey: ['client-links', clientId, includeInactive],
    queryFn: async () =>
      (await api.get<ClientExternalLinkDto[]>(`/clients/${clientId}/links`, {
        params: { includeInactive },
      })).data,
    enabled: !!clientId,
  });
}

export function useCreateClientExternalLink(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateClientExternalLinkRequest) =>
      (await api.post<ClientExternalLinkDto>(`/clients/${clientId}/links`, req)).data,
    onSuccess: () => invalidateLinks(qc, clientId),
  });
}

export function useUpdateClientExternalLink(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateClientExternalLinkRequest }) =>
      (await api.put<ClientExternalLinkDto>(`/clients/${clientId}/links/${id}`, req)).data,
    onSuccess: () => invalidateLinks(qc, clientId),
  });
}

export function useSetClientExternalLinkActive(clientId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) =>
      (await api.patch<ClientExternalLinkDto>(
        `/clients/${clientId}/links/${id}/${active ? 'activate' : 'deactivate'}`,
      )).data,
    onSuccess: () => invalidateLinks(qc, clientId),
  });
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type {
  EmailTemplateDto,
  UpdateEmailTemplateRequest,
  EmailTemplatePreviewRequest,
  EmailTemplatePreviewDto,
  EmailRuleDto,
  UpdateEmailRuleRequest,
  RecipientPreviewRequest,
  RecipientPreviewDto,
  ManualReminderDryRunRequest,
  ManualReminderDryRunResultDto,
} from '../types/api';

// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — قوالب/قواعد/معاينة مستقبِلين/تذكير يدويّ DryRun.
// كلها للأدمن حصرًا عبر سياسة EmailControlManage خادميًّا. DryRun فقط — لا إرسال فعليّ.

export function useEmailTemplates() {
  return useQuery({
    queryKey: ['email-control-templates'],
    queryFn: async () => (await api.get<EmailTemplateDto[]>('/email-control/templates')).data,
  });
}

export function useUpdateEmailTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ key, req }: { key: string; req: UpdateEmailTemplateRequest }) =>
      (await api.put<EmailTemplateDto>(`/email-control/templates/${key}`, req)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['email-control-templates'] }),
  });
}

export function usePreviewEmailTemplate() {
  return useMutation({
    mutationFn: async ({ key, req }: { key: string; req: EmailTemplatePreviewRequest }) =>
      (await api.post<EmailTemplatePreviewDto>(`/email-control/templates/${key}/preview`, req)).data,
  });
}

export function useEmailRules() {
  return useQuery({
    queryKey: ['email-control-rules'],
    queryFn: async () => (await api.get<EmailRuleDto[]>('/email-control/rules')).data,
  });
}

export function useUpdateEmailRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: string; req: UpdateEmailRuleRequest }) =>
      (await api.put<EmailRuleDto>(`/email-control/rules/${id}`, req)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['email-control-rules'] }),
  });
}

export function usePreviewRecipients() {
  return useMutation({
    mutationFn: async (req: RecipientPreviewRequest) =>
      (await api.post<RecipientPreviewDto>('/email-control/recipients/preview', req)).data,
  });
}

export function useManualReminderDryRun() {
  return useMutation({
    mutationFn: async (req: ManualReminderDryRunRequest) =>
      (await api.post<ManualReminderDryRunResultDto>('/email-control/manual-reminders/dry-run', req)).data,
  });
}

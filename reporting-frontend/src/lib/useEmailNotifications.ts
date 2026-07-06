import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  EmailNotificationLogFilter,
  EmailNotificationLogPageDto,
  EmailNotificationLogDetailDto,
} from '../types/api';

// سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-UI-R1) — قراءة فقط، بلا إرسال/تعديل/حذف.
// للأدوار العليا فقط (Admin/CEO/GM/CeoSupport) عبر سياسة EmailNotificationLog خادميًّا.
export function useEmailNotificationLog(filter: EmailNotificationLogFilter) {
  return useQuery({
    queryKey: ['email-notification-log', filter],
    queryFn: async () =>
      (
        await api.get<EmailNotificationLogPageDto>('/email-notifications/log', {
          params: filter,
        })
      ).data,
  });
}

export function useEmailNotificationDetail(id: string | null) {
  return useQuery({
    queryKey: ['email-notification-detail', id],
    enabled: !!id,
    queryFn: async () =>
      (await api.get<EmailNotificationLogDetailDto>(`/email-notifications/${id}`)).data,
  });
}

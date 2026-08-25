import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from './api';
import type {
  HrOperationsDashboard,
  HrOperationsFilter,
  HrOperationsQueueKey,
  HrOperationsQueuePage,
} from '../types/hrOperations';

// P2-HR-009 — وصول الواجهة إلى لوحة العمليّات.
// الصلاحيّة والنطاق مفروضان خادميًّا: 403 عند غياب المفتاح، و404 عند مغادرة النطاق،
// والتصدير مفتاح مستقلّ عن العرض ⇒ نجاح العرض لا يعني إتاحة التصدير.

const DASHBOARD_KEY = 'hr-operations-dashboard';
const QUEUE_KEY = 'hr-operations-queue';

/**
 * لا إعادة محاولة تلقائيّة على هذين الاستعلامين. 403 (لا مفتاح) و404 (خارج النطاق) قراران
 * نهائيّان من الخادم لا عطل عابر، وإعادة المحاولة تُبقي الشاشة في «جارٍ التحميل» فتُخفي سبب
 * المنع عن المستخدم وتُكرِّر نداءً مرفوضًا على سطح محكوم بالصلاحيّة. والإعادة متاحة صراحةً
 * بزرّ في `QueryError` ⇒ قرار المستخدم لا تكرار صامت.
 */
const NO_AUTO_RETRY = false;

/** يُسقِط المفاتيح الفارغة كي لا يُرسَل مرشِّح بلا معنى يُفسَّر خادميًّا. */
function toParams(filter: HrOperationsFilter): Record<string, string> {
  const params: Record<string, string> = {};
  Object.entries(filter).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    if (value === false) return;
    params[key] = String(value);
  });
  return params;
}

export function useHrOperationsDashboard(filter: HrOperationsFilter, enabled = true) {
  return useQuery({
    queryKey: [DASHBOARD_KEY, filter],
    enabled,
    retry: NO_AUTO_RETRY,
    queryFn: async () =>
      (await api.get<HrOperationsDashboard>('/hr-operations/dashboard', { params: toParams(filter) }))
        .data,
  });
}

export function useHrOperationsQueue(
  key: HrOperationsQueueKey | null,
  filter: HrOperationsFilter,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: [QUEUE_KEY, key, filter, page, pageSize],
    enabled: !!key,
    retry: NO_AUTO_RETRY,
    queryFn: async () =>
      (
        await api.get<HrOperationsQueuePage>(`/hr-operations/queues/${key}`, {
          params: { ...toParams(filter), page: String(page), pageSize: String(pageSize) },
        })
      ).data,
  });
}

/**
 * تصدير طابور. يُنزَّل كـBlob لأنّ الخادم يردّ ملفًّا لا JSON؛ وكلّ نداء منه
 * يُسجَّل خادميًّا في سجلّ التدقيق — لا تصدير صامت.
 */
export function useExportHrOperationsQueue() {
  return useMutation({
    mutationFn: async ({
      key,
      filter,
    }: {
      key: HrOperationsQueueKey;
      filter: HrOperationsFilter;
    }) => {
      const res = await api.get<Blob>(`/hr-operations/queues/${key}/export`, {
        params: toParams(filter),
        responseType: 'blob',
      });

      const disposition = String(res.headers?.['content-disposition'] ?? '');
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
      const fileName = match ? decodeURIComponent(match[1]) : `hr-operations-${key}.csv`;

      const url = URL.createObjectURL(res.data);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);

      return fileName;
    },
  });
}

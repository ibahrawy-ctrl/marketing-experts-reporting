import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { EmployeeChecklist, UpdateChecklistItemPayload } from '../types/checklist';

// P2-HR-010 — وصول الواجهة إلى قائمة الالتزام.
// القراءة محكومة بالنطاق والحسّاسيّة خادميًّا (404 خارج النطاق)، والتحرير مفتاح مستقلّ
// (`EmployeeChecklist.Manage` ⇒ 403 عند غيابه). نجاح القراءة لا يعني إتاحة التحرير.

const CHECKLIST_KEY = 'employee-checklist';

/**
 * لا إعادة محاولة تلقائيّة: 403 و404 قرارا خادم نهائيّان لا عطل عابر، وإعادة المحاولة
 * تُبقي الشاشة في «جارٍ التحميل» فتُخفي سبب المنع. الإعادة متاحة صراحةً بزرّ.
 */
const NO_AUTO_RETRY = false;

/** `subject` = معرّف الموظّف أو السلسلة `me` — وفي وضع الذات يُحَلّ المعرّف خادميًّا. */
export function useEmployeeChecklist(subject: string, enabled = true) {
  return useQuery({
    queryKey: [CHECKLIST_KEY, subject],
    enabled,
    retry: NO_AUTO_RETRY,
    queryFn: async () =>
      (await api.get<EmployeeChecklist>(`/employees/${subject}/checklist`)).data,
  });
}

/**
 * تحرير بند يدويّ واحد. المسار لا يقبل مفتاحًا محسوبًا (يردّ الخادم 400)،
 * وكلّ نجاح يُبطِل ذاكرة القائمة كي لا تُعرض نسخة بائتة بعد الكتابة.
 */
export function useUpdateChecklistItem(subject: string, subjectUserId?: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ itemKey, payload }: { itemKey: string; payload: UpdateChecklistItemPayload }) =>
      (
        await api.put(
          `/employees/${subjectUserId ?? subject}/checklist/${encodeURIComponent(itemKey)}`,
          payload,
        )
      ).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [CHECKLIST_KEY, subject] }),
  });
}

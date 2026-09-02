// VIS-03 — سياسة إعادة المحاولة الموحّدة للاستعلامات.
//
// **السبب الجذريّ الذي تحرسه هذه الاختبارات**: `main.tsx` كان ينشئ
// `new QueryClient()` بلا خيارات، وافتراض TanStack Query هو `retry: 3` بتراجع أُسّيّ.
// على خطأ نهائيّ كـ404 «مشروع غير موجود» أو 403 «خارج النطاق» يظلّ `isPending`
// صحيحًا طوال المحاولات الأربع (~٧ ثوانٍ)، وطالما `isLoading = isPending && isFetching`
// فالصفحة تعرض دوّارًا لا ينتهي بدل حالة الخطأ. المستخدم يرى «تعليقًا» لا رسالة.
//
// الادّعاء هنا سلوكيّ لا شكليّ: نمرّر أخطاء بأكواد حقيقيّة ونتحقّق من القرار.

import { describe, it, expect } from 'vitest';
import { shouldRetryQuery } from './api';

function axiosLikeError(status: number) {
  return Object.assign(new Error(`HTTP ${status}`), {
    isAxiosError: true,
    response: { status, data: { title: `HTTP ${status}` } },
  });
}

describe('VIS-03 — سياسة إعادة المحاولة', () => {
  // 4xx قرارات نهائيّة من الخادم: إعادتها لا تغيّر النتيجة وتؤجّل ظهور الخطأ فقط.
  it('لا يعيد المحاولة إطلاقًا على أخطاء العميل 4xx', () => {
    for (const status of [400, 401, 403, 404, 409, 422]) {
      expect(shouldRetryQuery(0, axiosLikeError(status))).toBe(false);
      expect(shouldRetryQuery(1, axiosLikeError(status))).toBe(false);
    }
  });

  // 5xx وأخطاء الشبكة عابرة بطبيعتها ⟹ محاولتان إضافيّتان ثمّ استسلام.
  it('يعيد المحاولة مرّتين على أخطاء الخادم 5xx ثمّ يتوقّف', () => {
    expect(shouldRetryQuery(0, axiosLikeError(500))).toBe(true);
    expect(shouldRetryQuery(1, axiosLikeError(503))).toBe(true);
    expect(shouldRetryQuery(2, axiosLikeError(500))).toBe(false);
  });

  // انقطاع الشبكة لا يحمل استجابة أصلًا: `status` غير معرَّف ⟹ يُعامَل معاملة العابر.
  it('يعيد المحاولة على انقطاع الشبكة الذي لا استجابة له', () => {
    const networkError = Object.assign(new Error('Network Error'), { isAxiosError: true });
    expect(shouldRetryQuery(0, networkError)).toBe(true);
    expect(shouldRetryQuery(2, networkError)).toBe(false);
  });

  // الحدّ الأقصى ثابت: ثلاث محاولات إجمالًا (الأصليّة + اثنتان) لا أربع.
  it('لا يتجاوز الحدّ الأقصى مهما ارتفع عدّاد الإخفاق', () => {
    expect(shouldRetryQuery(3, axiosLikeError(500))).toBe(false);
    expect(shouldRetryQuery(99, axiosLikeError(500))).toBe(false);
  });
});

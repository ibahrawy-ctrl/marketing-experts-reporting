import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './tokenStore';

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5090/api';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// إرفاق رمز الوصول لكل طلب.
api.interceptors.request.use((config) => {
  const token = tokenStore.access;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// تجديد رمز الوصول مرة واحدة عند 401، مع منع تكرار طلبات التجديد المتزامنة.
let refreshing: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refresh = tokenStore.refresh;
  if (!refresh) return null;
  try {
    const { data } = await axios.post(`${API_BASE_URL}/auth/refresh`, {
      refreshToken: refresh,
    });
    tokenStore.set(data.accessToken, data.refreshToken);
    return data.accessToken as string;
  } catch {
    tokenStore.clear();
    return null;
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as
      | (InternalAxiosRequestConfig & { _retried?: boolean })
      | undefined;
    const isAuthCall = original?.url?.includes('/auth/');

    if (error.response?.status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true;
      refreshing = refreshing ?? refreshAccessToken();
      const token = await refreshing;
      refreshing = null;
      if (token) {
        original.headers.Authorization = `Bearer ${token}`;
        return api(original);
      }
      // فشل التجديد: إعادة التوجيه لتسجيل الدخول.
      if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  },
);

// تنزيل ملف (CSV/PDF) عبر axios ليُرفق رمز الوصول، ثم حفظه في المتصفح.
export async function downloadFile(path: string, filename: string): Promise<void> {
  const res = await api.get(path, { responseType: 'blob' });
  const url = URL.createObjectURL(res.data as Blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

// استخراج رسالة خطأ مقروءة من استجابة الـAPI.
export function apiErrorMessage(error: unknown, fallback = 'حدث خطأ غير متوقع'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { detail?: string; title?: string } | undefined;
    return data?.detail ?? data?.title ?? error.message ?? fallback;
  }
  if (error instanceof Error) return error.message;
  return fallback;
}

// استخراج كود الخطأ (ProblemDetails.type) + رمز الحالة + الرسالة معًا.
// ملاحظة: الخادم يضع كود الخطأ في حقل «type» لا «code».
export function apiErrorCode(
  error: unknown,
  fallback = 'حدث خطأ غير متوقع',
): { code?: string; status?: number; message: string } {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { type?: string; detail?: string; title?: string } | undefined;
    return {
      code: data?.type,
      status: error.response?.status,
      message: data?.detail ?? data?.title ?? error.message ?? fallback,
    };
  }
  if (error instanceof Error) return { message: error.message };
  return { message: fallback };
}

// رسالة خطأ نوعية موحّدة لأزرار الاعتماد/القرار (APPROVAL ACTION UX R1):
// - كود auth.forbidden (فقدان دور المعتمِد الحاليّ داخل شاشة قرار) ⟶ اعتمده مستخدم آخر / لم تعد المعتمِد الحاليّ.
// - 409 / *.conflict / not_actionable / no_pending_step / already ⟶ الطلب لم يعد متاحًا (قد يكون نُفِّذ مسبقًا).
// - 403 عامّة (نقص صلاحية حقيقيّ) ⟶ رسالة الخادم أو «ليست لديك صلاحية…».
// - غير ذلك ⟶ رسالة detail/title القائمة.
export function approvalErrorMessage(error: unknown): string {
  const { code, status, message } = apiErrorCode(error);
  const c = code ?? '';
  if (c === 'auth.forbidden') {
    return '⚠️ تم اعتماد هذا الطلب بواسطة مستخدم آخر، أو لم تعد المعتمِد الحاليّ لهذا الطلب.';
  }
  if (
    status === 409 ||
    c.endsWith('.conflict') ||
    c.includes('not_actionable') ||
    c.includes('no_pending_step') ||
    c.includes('already')
  ) {
    return 'ℹ️ الطلب لم يعد متاحًا لهذا الإجراء (قد يكون نُفِّذ مسبقًا).';
  }
  if (status === 403) {
    return message || 'ليست لديك صلاحية لتنفيذ هذا الإجراء.';
  }
  return message;
}

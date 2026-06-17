// تخزين رموز الجلسة مع حماية ضد بيئات بلا localStorage (اختبارات/SSR).
const ACCESS = 'me_access';
const REFRESH = 'me_refresh';

function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSet(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    /* تجاهل في البيئات بلا تخزين */
  }
}

export const tokenStore = {
  get access() {
    return safeGet(ACCESS);
  },
  get refresh() {
    return safeGet(REFRESH);
  },
  set(access: string, refresh: string) {
    safeSet(ACCESS, access);
    safeSet(REFRESH, refresh);
  },
  clear() {
    safeSet(ACCESS, null);
    safeSet(REFRESH, null);
  },
};

import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';

// طبقة Toast موحّدة لـ APPROVAL ACTION UX R1: نجاح/خطأ/معلومة أعلى الشاشة، RTL، إغلاق تلقائيّ + يدويّ.
// مكوّن داخليّ خفيف بلا أيّ تبعية خارجية، بألوان هوية «خبراء التسويق» (نفس خرائط ألوان Alert في ui.tsx).

type ToastKind = 'success' | 'error' | 'info';

type ToastItem = { id: number; kind: ToastKind; message: string };

type ToastApi = {
  success: (message: string) => void;
  error: (message: string) => void;
  info: (message: string) => void;
};

const ToastContext = createContext<ToastApi | null>(null);

const AUTO_DISMISS_MS = 3500;

// APPROVAL ACTION UX R1: مهلة موحّدة بين ظهور Toast النجاح وبدء الرجوع التلقائيّ للقائمة.
// المستخدم يرى تأكيد النجاح بصريًّا أولًا، ثم ينتقل تلقائيًّا (بلا ضغط إضافيّ). الـToast يبقى أثناء الانتقال.
export const POST_SUCCESS_NAV_DELAY_MS = 700;

const kindStyles: Record<ToastKind, string> = {
  success: 'border-green-100 bg-green-50 text-success',
  error: 'border-red-100 bg-red-50 text-alert',
  info: 'border-navy-100 bg-navy-50 text-navy',
};

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const counter = useRef(0);
  const timers = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const timer = timers.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }, []);

  const push = useCallback(
    (kind: ToastKind, message: string) => {
      const id = ++counter.current;
      setToasts((prev) => [...prev, { id, kind, message }]);
      const timer = setTimeout(() => dismiss(id), AUTO_DISMISS_MS);
      timers.current.set(id, timer);
    },
    [dismiss],
  );

  const api = useMemo<ToastApi>(
    () => ({
      success: (m) => push('success', m),
      error: (m) => push('error', m),
      info: (m) => push('info', m),
    }),
    [push],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 top-4 z-[100] flex flex-col items-center gap-2 px-4"
        dir="rtl"
        aria-live="polite"
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`pointer-events-auto flex w-full max-w-md items-start gap-3 rounded-lg border p-3 text-sm font-semibold shadow-lg ${kindStyles[t.kind]}`}
            role="status"
          >
            <span className="flex-1">{t.message}</span>
            <button
              type="button"
              onClick={() => dismiss(t.id)}
              className="shrink-0 rounded px-1 text-base leading-none opacity-60 transition hover:opacity-100"
              aria-label="إغلاق"
            >
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within a ToastProvider');
  return ctx;
}

import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react';

type Tone = 'navy' | 'orange' | 'success' | 'alert' | 'gold' | 'muted';

const toneClasses: Record<Tone, string> = {
  navy: 'bg-navy-50 text-navy',
  orange: 'bg-orange-50 text-orange-600',
  success: 'bg-green-50 text-success',
  alert: 'bg-red-50 text-alert',
  gold: 'bg-amber-50 text-gold',
  muted: 'bg-line text-ink-2',
};

export function Badge({ children, tone = 'muted' }: { children: ReactNode; tone?: Tone }) {
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${toneClasses[tone]}`}>
      {children}
    </span>
  );
}

// `min-w-0` ليس تجميلًا: البطاقة تُستعمل عنصرًا في شبكة/فليكس، والقيمة الافتراضيّة `min-width:auto`
// تمنع المسار من النزول تحت `min-content` لمحتواها، فيفيض عرض الصفحة عند 390px متى حوى المحتوى
// رموزًا طويلة بلا مسافات (عناوين قوالب/بُرد). تصفيرها يسمح للمسار بالانكماش فيعمل `truncate` بالداخل.
export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`min-w-0 rounded-xl border border-line bg-white p-5 ${className}`}>{children}</div>
  );
}

export function StatCard({
  label,
  value,
  tone = 'navy',
}: {
  label: string;
  value: ReactNode;
  tone?: Tone;
}) {
  return (
    <div className="rounded-xl border border-line bg-white p-5">
      <p className="text-sm text-ink-2">{label}</p>
      <p className={`mt-2 text-3xl font-bold ${tone === 'alert' ? 'text-alert' : 'text-navy'}`}>
        {value}
      </p>
    </div>
  );
}

export function Button({
  children,
  variant = 'primary',
  className = '',
  loading = false,
  disabled,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'ghost' | 'danger' | 'inverted';
  loading?: boolean;
}) {
  // ملاحظة: «inverted» = زر أبيض على خلفية ملوّنة (Hero Banner). يُعرّف هنا كـ variant نظيف
  // بدل تمرير className="bg-white text-navy" الذي كان يتعارض مع text-white الأساسي فيختفي النص.
  const styles =
    variant === 'primary'
      ? 'bg-orange text-white hover:bg-orange-600'
      : variant === 'danger'
        ? 'bg-red-50 text-alert hover:bg-red-100'
        : variant === 'inverted'
          ? 'bg-white text-navy shadow-sm hover:bg-white/90'
          : 'bg-navy-50 text-navy hover:bg-navy-100';
  // APPROVAL ACTION UX R1: عند loading نعطّل الزر ونُظهِر Spinner صغير مضمّن (حماية من النقر المزدوج).
  return (
    <button
      className={`inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition disabled:opacity-50 ${styles} ${className}`}
      disabled={disabled || loading}
      {...rest}
    >
      {loading && (
        <span className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden="true" />
      )}
      {children}
    </button>
  );
}

export function Input({ className = '', ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy ${className}`}
      {...rest}
    />
  );
}

export function Select({ className = '', children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={`w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy ${className}`}
      {...rest}
    >
      {children}
    </select>
  );
}

export function Field({ label, help, children }: { label: string; help?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-ink">{label}</span>
      {help && <span className="mb-1 block text-xs text-ink-2">{help}</span>}
      {children}
    </label>
  );
}

export function Alert({ tone = 'navy', children }: { tone?: 'navy' | 'success' | 'alert' | 'gold'; children: ReactNode }) {
  const map = {
    navy: 'border-navy-100 bg-navy-50 text-navy',
    success: 'border-green-100 bg-green-50 text-success',
    alert: 'border-red-100 bg-red-50 text-alert',
    gold: 'border-amber-100 bg-amber-50 text-gold',
  };
  return <div className={`rounded-lg border p-3 text-sm ${map[tone]}`}>{children}</div>;
}

export function Spinner() {
  return (
    <div className="grid place-items-center py-16 text-ink-2">
      <div className="h-8 w-8 animate-spin rounded-full border-2 border-line border-t-navy" />
    </div>
  );
}

// شاشة تحميل النظام — شعار خبراء التسويق + مؤشّر تحميل بألوان الهوية.
export function LoadingScreen({ label = 'جارٍ التحميل…' }: { label?: string }) {
  return (
    <div className="grid min-h-screen place-items-center bg-offwhite">
      <div className="flex flex-col items-center gap-5">
        <img src="/logo-mark.png" alt="خبراء التسويق" className="h-14" />
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-line border-t-orange" />
        <p className="text-sm text-ink-2">{label}</p>
      </div>
    </div>
  );
}

// حالة فارغة موحّدة — شعار باهت + رسالة، ضمن هوية خبراء التسويق.
export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-line-2 bg-white px-6 py-12 text-center">
      <img src="/logo-mark.png" alt="" className="h-10 opacity-30" />
      <h3 className="text-base font-semibold text-navy">{title}</h3>
      {description ? <p className="max-w-md text-sm text-ink-2">{description}</p> : null}
      {action}
    </div>
  );
}

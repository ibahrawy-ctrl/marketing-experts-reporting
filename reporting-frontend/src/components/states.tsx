// حالات التحميل والخطأ الموحّدة — بهوية خبراء التسويق (كحلي/برتقالي).
// تستبدل الـ Spinner المجرّد بهيكل عظمي (Skeleton) + رسالة خطأ مفهومة مع إعادة المحاولة.
import { useEffect, useState } from 'react';
import { Button } from './ui';

// كتلة هيكلية وامضة.
export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-line ${className}`} />;
}

// هيكل جدول — صفوف × أعمدة، يُحاكي شكل البيانات قبل وصولها.
export function TableSkeleton({ rows = 6, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="space-y-3" role="status" aria-label="جارٍ تحميل البيانات">
      <div className="flex gap-3">
        {Array.from({ length: cols }).map((_, i) => (
          <Skeleton key={i} className="h-4 flex-1" />
        ))}
      </div>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex gap-3">
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} className="h-5 flex-1" />
          ))}
        </div>
      ))}
    </div>
  );
}

// هيكل بطاقات مؤشّرات.
export function CardsSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" role="status" aria-label="جارٍ تحميل البيانات">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="rounded-2xl border border-line bg-white p-5">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="mt-3 h-8 w-1/2" />
        </div>
      ))}
    </div>
  );
}

// مؤشّر تحميل مع سياق — إذا طال التحميل تظهر رسالة «يتم تحميل البيانات…».
export function LoadingState({ label = 'يتم تحميل البيانات…' }: { label?: string }) {
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setSlow(true), 4000);
    return () => clearTimeout(t);
  }, []);
  return (
    <div className="grid place-items-center gap-3 py-16 text-ink-2" role="status">
      <div className="h-8 w-8 animate-spin rounded-full border-2 border-line border-t-orange" />
      <p className="text-sm">{slow ? 'يتم تحميل البيانات… قد يستغرق ذلك لحظات.' : label}</p>
    </div>
  );
}

// رسالة خطأ مفهومة + زر إعادة المحاولة (بهوية الهوية لا توجد كلمات تقنية).
export function QueryError({
  onRetry,
  title = 'تعذّر تحميل البيانات',
  description = 'حدث خطأ أثناء جلب البيانات. تحقّق من اتصالك ثم أعد المحاولة.',
}: {
  onRetry?: () => void;
  title?: string;
  description?: string;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-red-100 bg-red-50 px-6 py-12 text-center">
      <h3 className="text-base font-semibold text-alert">{title}</h3>
      <p className="max-w-md text-sm text-ink-2">{description}</p>
      {onRetry && <Button onClick={onRetry}>إعادة المحاولة</Button>}
    </div>
  );
}

// ═══════════ P123-R1 — الحالتان الدائمتان ═══════════
//
// تختلفان عن `QueryError` في جوهر لا في شكل: **لا زرّ إعادة محاولة فيهما إطلاقًا**، ولا نبرة
// عُطل. كانت الشاشات تعرض «حدث خطأ مؤقّت، أعد المحاولة» على ميزة مغلقة بالإعداد وعلى سطح
// خارج نطاق المستخدم — فيقرأ المستخدم قرارًا متعمَّدًا خللًا، ويعيد المحاولة على ما لن يتغيّر.

// الميزة مغلقة في هذه البيئة — لا علاقة للأمر بهذا المستخدم ولا بصلاحيّاته.
export function FeatureDisabledState({
  title = 'هذه الخدمة غير مفعّلة حاليًّا',
  description = 'لم تُفعَّل هذه الخدمة في النظام بعد. ليس هذا خطأً، ولا يلزمك فعل شيء — راجع إدارة النظام إن كنت تحتاجها.',
}: {
  title?: string;
  description?: string;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-line-2 bg-offwhite px-6 py-12 text-center">
      <h3 className="text-base font-semibold text-navy">{title}</h3>
      <p className="max-w-md text-sm text-ink-2">{description}</p>
    </div>
  );
}

// السطح مفتوح لكنّه ليس لهذا المستخدم — أو خارج نطاقه (الخادم يوحّد الحالتين عمدًا: عدم إفشاء).
export function ForbiddenState({
  title = 'لا تملك صلاحية الاطّلاع على هذه البيانات',
  description = 'هذه البيانات خارج نطاق صلاحيّتك، أو غير موجودة. راجع مديرك المباشر إن كنت تحتاج الوصول إليها.',
}: {
  title?: string;
  description?: string;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-line-2 bg-offwhite px-6 py-12 text-center">
      <h3 className="text-base font-semibold text-navy">{title}</h3>
      <p className="max-w-md text-sm text-ink-2">{description}</p>
    </div>
  );
}

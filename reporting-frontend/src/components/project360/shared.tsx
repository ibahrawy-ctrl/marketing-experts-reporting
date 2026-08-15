// ======================================================================
// Project 360 — عناصر مشتركة بين التبويبات (CPW-R3 · R2-W12)
//
// **الخادم هو المرجع**: `Project360Access` تُخفي أزرارًا فقط. لا تُغني عن تخويل
// الخادم ولا تُعتبر ضمانًا — كلّ مسار محميّ عنده بالبوّابة نفسها بصرف النظر عمّا يُعرَض.
// ======================================================================

import type { ComponentProps, ReactNode } from 'react';
import axios from 'axios';
import { apiErrorMessage } from '../../lib/api';
import { Button } from '../ui';
import { QueryError } from '../states';

/** رمز حالة HTTP إن كان الخطأ من المحور، وإلّا `undefined`. */
function httpStatusOf(error: unknown): number | undefined {
  return axios.isAxiosError(error) ? error.response?.status : undefined;
}

/**
 * زرّ إجراء يعطّل نفسه أثناء الطفرة (حماية من النقر المزدوج).
 *
 * السلوك محليّ لحزمة Project 360 عمدًا: ربطه بخاصّيّة `loading` في `Button` العامّ كان
 * سيجعل هذه الحزمة تعتمد على سطح مكوّن تُعدّله تذكرة أخرى، فتفشل بمعزل عنها.
 */
export function ActionButton({
  busy = false,
  disabled,
  ...rest
}: ComponentProps<typeof Button> & { busy?: boolean }) {
  return <Button {...rest} disabled={disabled || busy} aria-busy={busy || undefined} />;
}

/**
 * القدرتان المعتمدتان في Project 360:
 * - `canManage`: الكتابة البنيويّة (إنشاء/تعديل/حذف الأهداف والمؤشّرات والمخرَجات) — أدوار الإدارة.
 * - `canOperate`: الكتابة التشغيليّة (الحالة/التقدّم/القراءات/إعادة احتساب الصحّة) — الإدارة أو
 *   قائد الفريق أو مدير الحساب **المسؤول عن هذا المشروع بعينه** (D-07).
 */
export type Project360Access = {
  canManage: boolean;
  canOperate: boolean;
};

/**
 * خطأ استعلام بصياغة تُفرِّق بين الحالات التي يهمّ المستخدم تمييزها.
 *
 * ملاحظة عقديّة: `404` هنا تعني «غير موجود **أو** خارج نطاقك» — الخادم لا يميّز بينهما
 * عمدًا (FINDING-W6-04)، فلا يجوز أن توحي الواجهة بأنّ المورد موجود لكنّه ممنوع.
 */
export function Project360QueryError({
  error,
  onRetry,
}: {
  error: unknown;
  onRetry?: () => void;
}) {
  const status = httpStatusOf(error);
  if (status === 404) {
    return (
      <QueryError
        title="المشروع غير موجود"
        description="المشروع غير موجود أو أنّه خارج نطاق صلاحيّتك."
      />
    );
  }
  if (status === 403) {
    return (
      <QueryError
        title="لا تملك صلاحيّة هذا الإجراء"
        description="يمكنك الاطّلاع على المشروع، لكن هذه العمليّة تتطلّب صلاحيّة أعلى."
      />
    );
  }
  return <QueryError onRetry={onRetry} description={apiErrorMessage(error)} />;
}

export function SectionHeading({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h3 className="text-base font-semibold text-navy">{title}</h3>
      {action}
    </div>
  );
}

/** سطر «تسمية ⟵ قيمة» — يُستعمل في بطاقات الهويّة والملخّصات. */
export function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-ink-2">{label}</p>
      <div className="mt-0.5 truncate text-sm font-medium text-ink">{children}</div>
    </div>
  );
}

// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — PHASE 5
// دوالّ نقيّة (بلا شبكة/بلا DOM) تشتقّ لافتة لوحة الموظّف من الحالة الموحّدة الخادميّة.
// الواجهة لا تحسب أيّ حالة: المصدر الوحيد هو الحقل الموحّد unified القادم من my-cycles،
// وتحديدًا العلم الخادميّ isCurrentPriority الذي يعيّن دورةً واحدةً «مطلوب إجراؤها الآن».
import type { ReportingCycleDto, UnifiedReportCycleStatusDto } from '../types/api';

// شدّة الحالة الموحّدة ⇢ درجة لون لافتة الإجراء (ActionBanner لا يعرف إلا orange/success/navy).
export function unifiedBannerTone(severity: string): 'orange' | 'success' | 'navy' {
  switch (severity) {
    case 'alert':
    case 'warn':
      return 'orange';
    case 'success':
      return 'success';
    default:
      return 'navy';
  }
}

// نصّ زرّ الإجراء بحسب الحالة الموحّدة (لا يُحسب من الموعد محليًّا).
export function unifiedBannerCta(status: UnifiedReportCycleStatusDto['unifiedStatus']): string {
  switch (status) {
    case 'DueNow':
    case 'OverdueNotSubmitted':
      return 'ابدأ التقرير';
    case 'Draft':
    case 'OverdueDraft':
      return 'أكمل التقرير';
    case 'ReturnedForChanges':
    case 'OverdueReturned':
      return 'عدّل الآن';
    case 'Closed':
      return 'عرض التقرير';
    default:
      return 'متابعة الحالة';
  }
}

// درجة إلحاح بند «يحتاج إجراء» بحسب شدّة الحالة الموحّدة.
export function unifiedUrgency(severity: string): 'high' | 'medium' | 'low' {
  switch (severity) {
    case 'alert':
      return 'high';
    case 'warn':
      return 'medium';
    default:
      return 'low';
  }
}

// رابط الإجراء داخل تطبيق الواجهة: الخادم يُعيد مسارًا بلا بادئة /app (مثل /submissions?period=…)،
// ومسارات الواجهة كلّها تحت /app، فنضيف البادئة مرّةً واحدةً بأمان (بلا تكرار).
export function unifiedActionTo(actionUrl: string): string {
  if (!actionUrl) return '/app/submissions';
  if (actionUrl.startsWith('/app')) return actionUrl;
  return `/app${actionUrl.startsWith('/') ? '' : '/'}${actionUrl}`;
}

// اختيار الدورة التي تقود اللافتة: أولًا الدورة الوحيدة التي عيّنها الخادم «مطلوب إجراؤها الآن»
// (isCurrentPriority) وفق مصفوفة الأولوية الخادميّة؛ وإلّا الدورة الحالية (لعرض حالة إعلاميّة كـ«قيد المراجعة»).
// تُرجِع null إذا لم تصل بيانات دورات موحّدة إطلاقًا ⇒ يرجع النداء إلى المسار القديم (توافق خلفيّ).
export function selectBannerCycleUnified(
  cycles: ReportingCycleDto[] | undefined | null,
): UnifiedReportCycleStatusDto | null {
  if (!cycles || cycles.length === 0) return null;
  const priority = cycles.find((c) => c.unified?.isCurrentPriority);
  if (priority?.unified) return priority.unified;
  const current = cycles.find((c) => c.isCurrent && c.unified);
  if (current?.unified) return current.unified;
  return null;
}

export interface UnifiedBanner {
  title: string;
  description: string;
  cta: string;
  tone: 'orange' | 'success' | 'navy';
  to: string;
}

// بناء اللافتة من الحالة الموحّدة الخادميّة: العنوان/الوصف من تسميات الخادم، اللون من الشدّة،
// نصّ الزرّ من نوع الحالة، والرابط من actionUrl الخادميّ. لا حساب محليّ للحالة.
export function unifiedEmployeeBanner(unified: UnifiedReportCycleStatusDto): UnifiedBanner {
  return {
    title: unified.statusLabel,
    description: unified.statusDescription || unified.cycleLabel,
    cta: unifiedBannerCta(unified.unifiedStatus),
    tone: unifiedBannerTone(unified.severity),
    to: unifiedActionTo(unified.actionUrl),
  };
}

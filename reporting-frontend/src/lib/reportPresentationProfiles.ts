// ===== AMR-OUTPUT-REDESIGN-R1 — طبقة عرض التقارير المدفوعة بميفولة العرض (Presentation Profiles) =====
// الهدف: ترقية مخرَج التقارير من «مصيّر عامّ» (صفوف dt/dd متساوية الوزن) إلى مخرَج قراريّ واعٍ
// بالأنواع، دون أيّ تغيير في البيانات/المفاتيح/المخطط/القالب (لا Migration، لا API، لا Workflow).
//
// المبدأ المعماريّ: لا صفحة hardcoded لتقرير بعينه. بل «ميفولة عرض» (Profile) تصف أدوار الحقول
// (ترويسة/حالة/شارات/مقاييس/سرد/قرارات/مخاطر/أولويّات/روابط)، ويقودها مصيّر عامّ قابل لإعادة
// الاستخدام. القوالب بلا Profile تظلّ تُعرض بالمصيّر العامّ القائم (fallback) دون أيّ تغيير.
//
// الكشف عن الـProfile يتمّ بتوقيع مفاتيح الحقول (متين مثل moderationGroups) + عنوان القالب اختياريًّا،
// لأنّ SubmissionDto لا يحمل TemplateKey. أيّ حقل غير مستهلَك من الـProfile يظهر في مجموعة ذيليّة
// «معلومات إضافية» (لا فقدان لأيّ حقل تاريخيّ).

import type { RepeatableSubField } from '../types/api';

export type PresentationTone = 'navy' | 'orange' | 'success' | 'alert' | 'gold' | 'muted';

// خريطة RAG لقيمة Select ⇒ لون شارة دلاليّ. القيم غير المذكورة تأخذ defaultTone.
// emptyValues تُعامَل كـ«لا شارة» (فراغ منطقيّ، مثل «لا يوجد»).
export interface SelectBadgeSpec {
  key: string;
  labelPrefix?: string; // بادئة اختياريّة تُعرض قبل القيمة، مثل «مخاطر:».
  emptyValues: string[];
  toneByValue: Record<string, PresentationTone>;
  defaultTone: PresentationTone;
}

// بطاقة مقياس رقميّ (Number/Currency/Percentage).
export interface MetricSpec {
  key: string;
  label: string;
  tone: PresentationTone;
}

// دِلاء حالة المشروع لاشتقاق ملخّص المحفظة (عدّ فعليّ من القيم المحفوظة — بلا أرقام مخترَعة).
export interface StatusBuckets {
  stable: string[]; // 🟢 على المسار/مكتمل
  followUp: string[]; // 🟡 متأخر/معلّق
  atRisk: string[]; // 🔴 متعثّر
}

// ميفولة عرض تقرير: تصف أدوار الحقول الفرعيّة داخل قسم المشاريع المتكرر.
export interface PresentationProfile {
  id: string;
  // كشف الـProfile عبر توقيع مفاتيح الحقول + عنوان القالب اختياريًّا.
  match: (fields: RepeatableSubField[], templateTitle: string) => boolean;

  // (A) ترويسة المشروع — شارات الحالة + سطر المرحلة.
  statusBadges: SelectBadgeSpec[];
  phaseKey?: string;

  // (C) مقاييس التسليم — بطاقات + شريط نسبة اعتماد اختياريّ.
  metrics: MetricSpec[];
  approvalProgress?: { sentKey: string; approvedKey: string; label: string };

  // (B) السرد التنفيذيّ للمشروع.
  summaryKeys: string[];

  // (E) العميل والتواصل.
  clientKeys: string[];

  // (F) التأخيرات والعوائق.
  blockerKeys: string[];

  // (H) القرارات المطلوبة — بطاقة بارزة مستقلّة (AMR-A3).
  decisionKey: string;

  // (I) أولويّة الأسبوع القادم / الفرص.
  priorityKeys: string[];

  // (J) الروابط والأدلّة والتبعيّات.
  linkKeys: string[];
  footerKeys: string[];

  // اشتقاق ملخّص المحفظة + فهرس المشاريع.
  statusKey: string;
  statusBuckets: StatusBuckets;
  riskKey: string;
  relationshipKey: string;
}

// ===== ميفولة عرض تقرير إدارة الحسابات (Account Manager) =====
// المفاتيح مطابقة تمامًا لحقول قسم المشاريع في قالب AM على الإنتاج (لا تُغيَّر — عرض فقط).
export const accountManagerProfile: PresentationProfile = {
  id: 'account-manager',
  // توقيع مميِّز لا يتصادم مع مفردات المودريشن: تسليمات + قرارات + علاقة العميل.
  match: (fields, templateTitle) => {
    const keys = new Set(fields.map((f) => f.key));
    const signature =
      keys.has('deliverables_sent') && keys.has('decisions_required') && keys.has('client_relationship');
    const byTitle = templateTitle.includes('إدارة الحسابات') || templateTitle.includes('مدير الحسابات');
    return signature || (byTitle && keys.has('decisions_required'));
  },

  statusBadges: [
    {
      key: 'project_status',
      emptyValues: [''],
      toneByValue: {
        'على المسار': 'success',
        مكتمل: 'success',
        متأخر: 'gold',
        معلّق: 'gold',
        متعثّر: 'alert',
      },
      defaultTone: 'gold',
    },
    {
      key: 'risk_severity',
      labelPrefix: 'مخاطر:',
      emptyValues: ['', 'لا يوجد'],
      toneByValue: {
        منخفض: 'success',
        متوسط: 'gold',
        مرتفع: 'alert',
        حرج: 'alert',
      },
      defaultTone: 'gold',
    },
    {
      key: 'client_relationship',
      labelPrefix: 'علاقة:',
      emptyValues: [''],
      toneByValue: {
        ممتازة: 'success',
        جيدة: 'success',
        محايدة: 'gold',
        متوترة: 'alert',
        حرجة: 'alert',
      },
      defaultTone: 'gold',
    },
  ],
  phaseKey: 'current_phase',

  metrics: [
    { key: 'deliverables_sent', label: 'أُرسل', tone: 'navy' },
    { key: 'deliverables_approved', label: 'اعتُمد', tone: 'success' },
    { key: 'deliverables_pending', label: 'منتظر', tone: 'gold' },
  ],
  approvalProgress: { sentKey: 'deliverables_sent', approvedKey: 'deliverables_approved', label: 'نسبة الاعتماد' },

  summaryKeys: ['achievements'],
  clientKeys: ['client_requests'],
  blockerKeys: ['open_issues', 'delays', 'scope_changes'],
  decisionKey: 'decisions_required',
  priorityKeys: ['next_steps', 'commercial_opportunities'],
  linkKeys: ['evidence_link'],
  footerKeys: ['internal_dependencies', 'notes'],

  statusKey: 'project_status',
  statusBuckets: {
    stable: ['على المسار', 'مكتمل'],
    followUp: ['متأخر', 'معلّق'],
    atRisk: ['متعثّر'],
  },
  riskKey: 'risk_severity',
  relationshipKey: 'client_relationship',
};

// سجلّ الـProfiles — يُضاف إليه ميفولات مستقبليّة (تخصّصات أخرى) دون مساس بالمصيّر العامّ.
const REGISTRY: PresentationProfile[] = [accountManagerProfile];

// يُعيد أوّل Profile مطابق أو null (⇐ المصيّر العامّ fallback).
export function resolvePresentationProfile(
  fields: RepeatableSubField[],
  templateTitle: string,
): PresentationProfile | null {
  for (const p of REGISTRY) {
    if (p.match(fields, templateTitle)) return p;
  }
  return null;
}

// كل المفاتيح التي يستهلكها الـProfile صراحةً — لتحديد الحقول التاريخيّة غير المعروفة (fallback group).
export function profileKnownKeys(profile: PresentationProfile): Set<string> {
  const keys = new Set<string>();
  profile.statusBadges.forEach((b) => keys.add(b.key));
  if (profile.phaseKey) keys.add(profile.phaseKey);
  profile.metrics.forEach((m) => keys.add(m.key));
  profile.summaryKeys.forEach((k) => keys.add(k));
  profile.clientKeys.forEach((k) => keys.add(k));
  profile.blockerKeys.forEach((k) => keys.add(k));
  keys.add(profile.decisionKey);
  profile.priorityKeys.forEach((k) => keys.add(k));
  profile.linkKeys.forEach((k) => keys.add(k));
  profile.footerKeys.forEach((k) => keys.add(k));
  return keys;
}

// أدوات مشتركة للعرض (نصّ غير فارغ / تحديد لون شارة).
export function hasText(raw: string | undefined | null): boolean {
  return raw != null && String(raw).trim() !== '';
}

// عبارات «لا معنى لها» (نفي/غياب) — تُخفى من العرض فقط، دون أيّ تعديل للبيانات المحفوظة.
// المطابقة على النصّ الكامل بعد التطبيع (trim + إزالة النقطة/المسافات الذيليّة)، لا على substring،
// كي لا نُخفي جملة مفيدة تحوي كلمة «لا» (مثل «لا يمكن إطلاق الحملة قبل اعتماد العميل»).
const NON_MEANINGFUL_PRESENTATION_VALUES = new Set<string>([
  '-',
  '—',
  'لا',
  'لا يوجد',
  'لا توجد',
  'ليس هناك',
  'غير متوفر',
  'n/a',
]);

// هل القيمة النصّيّة ذات معنى فعليّ للعرض؟
// - trim + معالجة null/undefined/فراغ.
// - تخفي عبارات النفي/الغياب مع تحمّل المسافات والنقطة في النهاية.
// - الرقم 0 قيمة دالّة (لا يُعتبر فارغًا).
// - لا تُخفي نصًّا حقيقيًّا يحوي كلمة «لا» ضمن جملة مفيدة (مطابقة كاملة لا substring).
// - عرض فقط: لا تُغيّر البيانات الأصلية إطلاقًا.
export function isMeaningfulPresentationValue(raw: string | number | undefined | null): boolean {
  if (raw == null) return false;
  if (typeof raw === 'number') return Number.isFinite(raw); // 0 دالّ
  const trimmed = String(raw).trim();
  if (trimmed === '') return false;
  const normalized = trimmed.replace(/[.\s]+$/u, '').trim().toLowerCase();
  if (normalized === '') return false;
  return !NON_MEANINGFUL_PRESENTATION_VALUES.has(normalized);
}

export function badgeToneFor(spec: SelectBadgeSpec, raw: string | undefined): PresentationTone | null {
  if (!hasText(raw)) return null;
  const v = String(raw).trim();
  if (spec.emptyValues.includes(v)) return null;
  return spec.toneByValue[v] ?? spec.defaultTone;
}

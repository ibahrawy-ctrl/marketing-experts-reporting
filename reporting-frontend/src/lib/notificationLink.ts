// تطبيع روابط الإشعارات الداخلية قبل التنقّل داخل الـ SPA.
// السبب: بعض الإشعارات القديمة/الخارجية تحمل روابط لا تطابق مسارات التطبيق (تبدأ بلا بادئة /app
// أو تشير إلى تفاصيل بمعرّف لا يوجد له Route)، فينتج عنها صفحة بيضاء عند navigate مباشرةً.
// هذه الدالة نقية (بلا تأثيرات جانبية) وتضمن إرجاع مسار آمن موجود دائمًا (fallback إلى /app) ⇒ لا صفحة بيضاء.

const FALLBACK = '/app';

// مسارات قائمة معروفة يبثّها الخادم أحيانًا بلا بادئة /app.
const BARE_LIST_ROUTES = new Set([
  '/submissions',
  '/hr-requests',
  '/leave-requests',
  '/compliance',
  '/my-kpi',
  '/development',
]);

/**
 * يحوّل رابط إشعار مخزَّن إلى مسار SPA آمن.
 * - /submissions/{id} و /app/submissions/{id} ⇒ /app/submissions?open={id}
 * - /kpi-evaluations/{id} ⇒ /app/my-kpi (لا يوجد Route بمعرّف)
 * - /training-needs/{id} و /improvement-plans/{id} ⇒ /app/development
 * - /escalations/{id} ⇒ /app/governance/escalations
 * - أي مسار يبدأ بـ /app يبقى كما هو
 * - مسارات القائمة المعروفة بلا بادئة (مع/بدون query) ⇒ تُسبق بـ /app
 * - أي شيء آخر غير معروف أو فارغ ⇒ /app (لا صفحة بيضاء)
 */
export function normalizeNotificationLink(link?: string | null): string {
  if (!link) return FALLBACK;
  const raw = link.trim();
  if (!raw) return FALLBACK;

  const sub = raw.match(/^\/(?:app\/)?submissions\/([^/?#]+)/);
  if (sub) return `/app/submissions?open=${sub[1]}`;

  if (/^\/(?:app\/)?kpi-evaluations\//.test(raw)) return '/app/my-kpi';
  if (/^\/(?:app\/)?training-needs\//.test(raw)) return '/app/development';
  if (/^\/(?:app\/)?improvement-plans\//.test(raw)) return '/app/development';
  if (/^\/(?:app\/)?escalations\//.test(raw)) return '/app/governance/escalations';

  // مسار تطبيق صحيح بالفعل.
  if (raw === '/app' || raw.startsWith('/app/') || raw.startsWith('/app?')) return raw;

  // مسار قائمة معروف بلا بادئة (قد يحمل query مثل ?period= أو ?tab=).
  const path = raw.split(/[?#]/)[0];
  if (BARE_LIST_ROUTES.has(path)) return `/app${raw}`;

  return FALLBACK;
}

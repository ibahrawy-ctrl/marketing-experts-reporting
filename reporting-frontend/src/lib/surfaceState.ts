// P123-R1 — الحالات الستّ لأيّ سطح بيانات، ومَن يقرّرها.
//
// المشكلة التي يغلقها هذا الملفّ: كانت الشاشات تعرف حالتين فقط («بيانات» أو «خطأ»)، فانطبقت
// رسالة **مؤقّتة** («حدث خطأ، أعد المحاولة») على حالات **دائمة** (ميزة مغلقة، أو خارج النطاق).
// المستخدم يعيد المحاولة بلا نهاية على شيء لن يتغيّر، ويقرأ إغلاقًا متعمَّدًا عطلًا في النظام.
// لذا تُفصَل الحالات فصلًا صريحًا، ويُحسَم الفرز في **دالّة واحدة** لا في كلّ صفحة على حدة.
//
// الحالات الستّ:
//   Loading         — الطلب جارٍ (مؤقّت)
//   Available       — بيانات صالحة للعرض
//   Empty           — الطلب نجح ولا بيانات (ليس خطأً)
//   Forbidden       — السطح مفتوح لكنّ هذا المستخدم ليس صاحبه / خارج نطاقه (دائم)
//   FeatureDisabled — السطح مغلق في هذه البيئة أصلًا (دائم، ولا علاقة له بالمستخدم)
//   Failed          — عطل حقيقيّ يستحقّ إعادة المحاولة (مؤقّت)
import { useAuth } from './auth';
import { apiErrorCode } from './api';

export type SurfaceState = 'Loading' | 'Available' | 'Empty' | 'Forbidden' | 'FeatureDisabled' | 'Failed';

/// هل يستطيع المستخدم فعل شيء حيال هذه الحالة؟ الحالات الدائمة **لا يُعرَض لها زرّ إعادة محاولة**:
/// عرضه وعدٌ كاذب يدفع المستخدم إلى تكرار طلب مرفوض بنيويًّا.
export function isPermanentState(state: SurfaceState): boolean {
  return state === 'Forbidden' || state === 'FeatureDisabled';
}

export function classifySurfaceState(input: {
  /// `undefined` تعني «هذا السطح غير مشروط بميزة» — لا «الميزة مغلقة».
  featureEnabled?: boolean;
  isLoading: boolean;
  error?: unknown;
  isEmpty?: boolean;
}): SurfaceState {
  // الميزة **أوّلًا وقبل التحميل**: إن كانت مغلقة فالطلب لا يُرسَل أصلًا، فلا معنى لانتظار
  // ردّ نعرف يقينًا أنّه 404. هذا هو الترتيب الذي يجعل التمييز ممكنًا: بعد إرسال الطلب
  // يصير 404 «الميزة مغلقة» و404 «خارج نطاقك» غير قابلَين للتفريق من رمز الحالة وحده.
  if (input.featureEnabled === false) return 'FeatureDisabled';
  if (input.isLoading) return 'Loading';

  if (input.error != null) {
    const { status } = apiErrorCode(input.error);
    // 403 = «لا تملك المفتاح»، و404 هنا = «خارج نطاقك أو غير موجود» (اصطلاح عدم الإفشاء
    // الخادميّ). كلاهما يعني للمستخدم الشيء نفسه: لا شيء لك هنا، وإعادة المحاولة عبث.
    if (status === 403 || status === 404) return 'Forbidden';
    return 'Failed';
  }

  if (input.isEmpty) return 'Empty';
  return 'Available';
}

/// هل هذه الميزة مفتوحة في هذه البيئة؟ المصدر هو عقد المستخدم (`/auth/me`) لا تخمين الواجهة.
/// **لا يمنح شيئًا**: الخادم يبقى المُنفِّذ الوحيد للتخويل؛ هذا يمنع عرض سطح مُغلَق فقط.
export function useFeatureEnabled(featureKey: string): boolean {
  return useAuth().features.has(featureKey);
}

import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { classifySurfaceState, isPermanentState, type SurfaceState } from './surfaceState';

// ===== P123-R1 — الحالات الستّ لا تُخلَط =====
//
// الخلط الذي تغلقه هذه الاختبارات ليس تجميليًّا: عرض «حدث خطأ مؤقّت، أعد المحاولة» على ميزة
// مغلقة بالإعداد أو على سطح خارج نطاق المستخدم يدفعه إلى تكرار طلب مرفوض بنيويًّا، ويجعله
// يقرأ قرارًا متعمَّدًا عطلًا في النظام. لذا يُفرَض هنا فرزٌ **حصريّ**: لكلّ مدخل حالة واحدة.

function httpError(status: number): AxiosError {
  const err = new AxiosError('failed');
  err.response = {
    status,
    statusText: '',
    data: {},
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return err;
}

describe('classifySurfaceState', () => {
  it('الميزة المغلقة تسبق كلّ شيء — حتّى التحميل والخطأ', () => {
    // الأسبقيّة مقصودة: بغياب الميزة لا يُرسَل طلب أصلًا، فلا معنى لانتظار ردّ نعرفه سلفًا.
    expect(classifySurfaceState({ featureEnabled: false, isLoading: true })).toBe('FeatureDisabled');
    expect(classifySurfaceState({ featureEnabled: false, isLoading: false, error: httpError(500) })).toBe(
      'FeatureDisabled',
    );
    expect(classifySurfaceState({ featureEnabled: false, isLoading: false, isEmpty: true })).toBe('FeatureDisabled');
  });

  it('غياب `featureEnabled` يعني «سطح غير مشروط بميزة» لا «ميزة مغلقة»', () => {
    // لو عوملت `undefined` معاملة `false` لاختفت كلّ الأسطح غير المشروطة دفعةً واحدة.
    expect(classifySurfaceState({ isLoading: false })).toBe('Available');
    expect(classifySurfaceState({ isLoading: true })).toBe('Loading');
  });

  it('403 و404 كلاهما «ممنوع» دائم لا عطل مؤقّت', () => {
    // الخادم يوحّد «خارج النطاق» و«غير موجود» في 404 عمدًا (عدم إفشاء)، والمعنى للمستخدم واحد.
    expect(classifySurfaceState({ isLoading: false, error: httpError(403) })).toBe('Forbidden');
    expect(classifySurfaceState({ isLoading: false, error: httpError(404) })).toBe('Forbidden');
  });

  it('الأعطال الحقيقيّة وحدها تُصنَّف Failed', () => {
    expect(classifySurfaceState({ isLoading: false, error: httpError(500) })).toBe('Failed');
    expect(classifySurfaceState({ isLoading: false, error: httpError(502) })).toBe('Failed');
    expect(classifySurfaceState({ isLoading: false, error: new Error('network') })).toBe('Failed');
  });

  it('النجاح بلا بيانات فراغ لا خطأ', () => {
    expect(classifySurfaceState({ isLoading: false, isEmpty: true })).toBe('Empty');
    expect(classifySurfaceState({ featureEnabled: true, isLoading: false, isEmpty: false })).toBe('Available');
  });

  it('التحميل يسبق الخطأ والفراغ (بيانات الطلب السابق لا تُصنَّف حالةً نهائيّة)', () => {
    expect(classifySurfaceState({ featureEnabled: true, isLoading: true, error: httpError(403) })).toBe('Loading');
    expect(classifySurfaceState({ featureEnabled: true, isLoading: true, isEmpty: true })).toBe('Loading');
  });

  it('الحالتان الدائمتان وحدهما بلا إعادة محاولة', () => {
    const permanent: SurfaceState[] = ['Forbidden', 'FeatureDisabled'];
    const transient: SurfaceState[] = ['Loading', 'Available', 'Empty', 'Failed'];
    for (const s of permanent) expect(isPermanentState(s)).toBe(true);
    for (const s of transient) expect(isPermanentState(s)).toBe(false);
  });
});

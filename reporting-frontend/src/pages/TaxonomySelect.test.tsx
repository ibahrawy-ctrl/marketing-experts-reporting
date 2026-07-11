import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import { TaxonomySelect } from './SubmissionsPage';
import type { RepeatableSubField } from '../types/api';

// RC-4 Task 4D3 — اختبارات مكوّن قائمة الخيارات الديناميكية داخل قسم المشاريع.
// المصدر الأساسيّ = كتالوج تصنيفات التنفيذ (catalogDomain)؛ options لقطة احتياطيّة (fallback).

function sfWith(over: Partial<RepeatableSubField> = {}): RepeatableSubField {
  return {
    key: 'content_type',
    label: 'نوع المحتوى',
    type: 'Select',
    catalogDomain: 'content_type',
    options: [],
    ...over,
  } as RepeatableSubField;
}

function renderSelect(sf: RepeatableSubField, value = '', onChange = () => {}) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <TaxonomySelect sf={sf} value={value} onChange={onChange} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('TaxonomySelect — خيارات Taxonomy الديناميكية', () => {
  // (1) catalogDomain يجلب الخيارات ديناميكيًّا ويعرضها
  it('يجلب الخيارات من الكتالوج عند وجود catalogDomain', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: ['مقال', 'فيديو'] } as never);
    renderSelect(sfWith());
    await waitFor(() => expect(screen.getByRole('option', { name: 'مقال' })).toBeInTheDocument());
    expect(screen.getByRole('option', { name: 'فيديو' })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/execution-taxonomy/options', { params: { domain: 'content_type' } });
  });

  // (2) القيم المعطّلة لا تظهر (الـ endpoint يُعيد النشطة فقط)
  it('لا يعرض إلا ما يُعيده الـ endpoint (النشطة فقط)', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: ['نشط'] } as never);
    renderSelect(sfWith());
    await waitFor(() => expect(screen.getByRole('option', { name: 'نشط' })).toBeInTheDocument());
    expect(screen.queryByRole('option', { name: 'معطّل' })).not.toBeInTheDocument();
  });

  // (3) عند فشل الجلب: عرض اللقطة الاحتياطيّة + رسالة تنبيه
  it('يستخدم اللقطة fallback ويُظهر تنبيهًا عند فشل الجلب', async () => {
    vi.spyOn(api, 'get').mockRejectedValue(new Error('network'));
    renderSelect(sfWith({ options: ['لقطة-أولى', 'لقطة-ثانية'] }));
    await waitFor(() =>
      expect(screen.getByText(/تعذّر تحديث الخيارات من الكتالوج/)).toBeInTheDocument(),
    );
    expect(screen.getByRole('option', { name: 'لقطة-أولى' })).toBeInTheDocument();
  });

  // (4) القالب القديم بلا catalogDomain يستخدم options الثابتة ولا يستدعي الـ API
  it('القائمة الثابتة القديمة (بلا catalogDomain) تعمل بلا استدعاء API', async () => {
    const spy = vi.spyOn(api, 'get').mockResolvedValue({ data: [] } as never);
    renderSelect(sfWith({ catalogDomain: undefined, options: ['ثابت-أ', 'ثابت-ب'] }));
    expect(screen.getByRole('option', { name: 'ثابت-أ' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'ثابت-ب' })).toBeInTheDocument();
    expect(spy).not.toHaveBeenCalled();
  });

  // (5) القيمة القديمة المحفوظة تبقى معروضة حتى لو لم تعُد ضمن النشطة
  it('يُبقي القيمة القديمة المحفوظة قابلة للعرض («قيمة قديمة»)', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: ['نشط-حاليّ'] } as never);
    renderSelect(sfWith(), 'قيمة-محفوظة-قديمة');
    await waitFor(() => expect(screen.getByRole('option', { name: 'نشط-حاليّ' })).toBeInTheDocument());
    expect(screen.getByRole('option', { name: /قيمة-محفوظة-قديمة \(قيمة قديمة\)/ })).toBeInTheDocument();
  });

  // (6) onChange يمرّر النصّ الخام كما هو (شكل ValueJson لا يتغيّر)
  it('onChange يمرّر NameAr خامًا دون تحويل', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: ['مقال', 'فيديو'] } as never);
    const onChange = vi.fn();
    renderSelect(sfWith(), '', onChange);
    await waitFor(() => expect(screen.getByRole('option', { name: 'فيديو' })).toBeInTheDocument());
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'فيديو' } });
    expect(onChange).toHaveBeenCalledWith('فيديو');
  });

  // (7) حالتا التحميل والخطأ القاطع واضحتان
  it('يُظهر حالة التحميل ثم خطأً قاطعًا عند تعذّر الجلب بلا لقطة', async () => {
    // تحميل: وعد لا يُحسَم — نصّ التحميل ظاهر والقائمة معطّلة.
    let reject: (e: unknown) => void = () => {};
    vi.spyOn(api, 'get').mockReturnValue(new Promise((_, r) => { reject = r; }) as never);
    renderSelect(sfWith({ options: [] }));
    expect(screen.getByRole('option', { name: 'جارٍ تحميل الخيارات…' })).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeDisabled();

    // خطأ قاطع (لا options احتياطيّة) ⇒ رسالة حمراء واضحة.
    reject(new Error('boom'));
    await waitFor(() =>
      expect(screen.getByText(/تعذّر تحميل خيارات هذا الحقل/)).toBeInTheDocument(),
    );
  });
});

import { describe, it, expect } from 'vitest';
import { normalizeDigits, sanitizeNumericInput, isNumericGridColumn } from './numericNormalizer';

// أداة تطبيع الأرقام في الواجهة (RC-3 Task 2B): تحويل الخانات العربية/الفارسية إلى لاتينية أثناء الكتابة.
describe('normalizeDigits', () => {
  it.each([
    ['١٠', '10'],
    ['٣٠٠٠٠', '30000'],
    ['٠', '0'],
    ['٩٩٩', '999'],
    ['۱۲۳', '123'], // فارسية
    ['١٢٣.٥٠', '123.50'],
    ['1234567890', '1234567890'], // لاتينية تبقى
  ])('يحوّل %s إلى %s', (input, expected) => {
    expect(normalizeDigits(input)).toBe(expected);
  });

  it('يُبقي الحروف والعلامات ويحوّل الأرقام فقط', () => {
    expect(normalizeDigits('ملاحظة ١٢ و ٣٠%')).toBe('ملاحظة 12 و 30%');
  });

  it('يتعامل مع النصّ الفارغ', () => {
    expect(normalizeDigits('')).toBe('');
  });
});

describe('sanitizeNumericInput', () => {
  it.each([
    ['١٢٣abc', '123'],
    ['أبجد', ''],
    ['12+3', '123'],
    ['10/2', '102'],
    ['5*4', '54'],
    ['30%', '30'],
    ['(٥)', '5'],
    ['-١٢', '-12'],
    ['١٢-٣', '123'],
    ['١٢.٣.٤', '12.34'],
    ['١٢٣.٥', '123.5'],
  ])('ينقّي %s إلى %s', (input, expected) => {
    expect(sanitizeNumericInput(input)).toBe(expected);
  });
});

describe('isNumericGridColumn', () => {
  it.each(['الخدمة', 'الدورة', 'العميل', 'المشروع', 'المنصة', 'الحالة', 'ملاحظات', 'سبب التأخير'])(
    'العمود النصّي %s ليس رقميًّا',
    (col) => {
      expect(isNumericGridColumn(col)).toBe(false);
    },
  );

  it.each(['New Leads', 'Won', 'Revenue', 'ساعات العمل', 'Spend', 'Leads'])(
    'العمود %s رقميّ',
    (col) => {
      expect(isNumericGridColumn(col)).toBe(true);
    },
  );

  it('العمود غير المعرّف (undefined) ليس رقميًّا', () => {
    expect(isNumericGridColumn(undefined)).toBe(false);
  });
});

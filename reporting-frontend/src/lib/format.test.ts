import { describe, it, expect } from 'vitest';
import {
  roleLabel,
  submissionStatusLabel,
  formatDate,
  formatPercent,
  bidiIsolate,
  projectOptionLabel,
} from './format';

describe('format helpers', () => {
  it('maps roles and statuses to Arabic', () => {
    expect(roleLabel.Admin).toBe('مدير النظام');
    expect(submissionStatusLabel.Closed).toBe('مُغلق');
  });

  it('handles null dates and percents', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatPercent(null)).toBe('—');
    expect(formatPercent(75)).toContain('٪');
  });
});

// VIS-01 — العزل الاتّجاهيّ لأسماء المشاريع والعملاء داخل `<option>`.
//
// `<option>` لا يقبل عناصر أبناء ⟹ `<bdi>` مستحيل، والحلّ الوحيد محارف العزل
// FSI (U+2068) … PDI (U+2069). بدونها يُعيد خوارزم Unicode ترتيب المقاطع اللاتينيّة
// داخل السلسلة العربيّة فيظهر الاسم مبتورًا أو مقلوبًا (وهو ما رُصِد فعلًا في
// لقطة E02 حيث ظهر «R22B UAT — مشروع الفيديو (مؤقّت) — 2B UAT»).
describe('VIS-01 — العزل الاتّجاهيّ لتسميات المشاريع', () => {
  it('يغلّف النصّ بمحرفَي FSI وPDI بالضبط', () => {
    expect(bidiIsolate('R22B UAT')).toBe('\u2068R22B UAT\u2069');
  });

  it('يعزل طرفَي التسمية كلًّا على حدة ويصل بينهما بشَرطة طويلة', () => {
    expect(projectOptionLabel('R22B UAT — مشروع الفيديو', 'عميل س')).toBe(
      '\u2068R22B UAT — مشروع الفيديو\u2069 — \u2068عميل س\u2069',
    );
  });

  it('لا يضيف فاصلًا معلّقًا حين لا يوجد اسم عميل', () => {
    expect(projectOptionLabel('مشروع أ', null)).toBe('\u2068مشروع أ\u2069');
    expect(projectOptionLabel('مشروع أ')).toBe('\u2068مشروع أ\u2069');
  });

  // محارف العزل غير مرئيّة ولا تُغيّر النصّ المنطوق: تجريدها يعيد الأصل حرفًا بحرف،
  // فلا يتأثّر بحث المستخدم ولا نسخ الاسم إلى مكان آخر.
  it('محارف العزل قابلة للتجريد فيعود النصّ الأصليّ كما هو', () => {
    const label = projectOptionLabel('R22B UAT — مشروع الفيديو (مؤقّت)', 'عميل التجربة');
    expect(label.replace(/[\u2068\u2069]/g, '')).toBe(
      'R22B UAT — مشروع الفيديو (مؤقّت) — عميل التجربة',
    );
  });
});

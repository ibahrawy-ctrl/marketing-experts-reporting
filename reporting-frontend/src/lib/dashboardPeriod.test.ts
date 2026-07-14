import { describe, it, expect } from 'vitest';
import { operationalWeekKey, weekKeysForPeriod, previousPeriodKey } from './dashboardPeriod';

// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.7 — اختبارات انحدار للواجهة.
// تُطابق مرساة السبت المعتمدة خادميًّا (ReportingCalendarPolicy): الدورة تبدأ السبت وتنتهي الجمعة،
// ورقم/سنة الدورة = ISO week لمرجع الثلاثاء (السبت+3). تتحقّق من: جدول الإدارة W27→W33،
// **لا ارتداد** لمرساة الخميس القديمة، ثبات المفتاح داخل الدورة، والمفتاح السابق للدورة الأسبوعية.

function utc(y: number, m: number, d: number): Date {
  return new Date(Date.UTC(y, m - 1, d));
}

describe('operationalWeekKey — مرساة السبت', () => {
  // (1) جدول الإدارة المعتمد: كل سبت داخل دورته يُنتج مفتاحها الصحيح W27→W33.
  it.each([
    // كل سبت بداية دورة → مفتاحها.
    [utc(2026, 6, 27), '2026-W27'],
    [utc(2026, 7, 4), '2026-W28'],
    [utc(2026, 7, 11), '2026-W29'],
    [utc(2026, 7, 18), '2026-W30'],
    [utc(2026, 7, 25), '2026-W31'],
    [utc(2026, 8, 1), '2026-W32'],
    [utc(2026, 8, 8), '2026-W33'],
  ])('السبت %s → %s', (date, expected) => {
    expect(operationalWeekKey(date)).toBe(expected);
  });

  // (2) أي يوم داخل الدورة (السبت→الجمعة) يُنتج نفس المفتاح — الثلاثاء 30 يونيو داخل W27.
  it('يوم وسط الدورة (ثلاثاء 2026-06-30) → 2026-W27', () => {
    expect(operationalWeekKey(utc(2026, 6, 30))).toBe('2026-W27');
  });

  // (3) لا ارتداد لمرساة الخميس: كل أيام دورة W27 (السبت 27 يونيو → الجمعة 3 يوليو) تحمل نفس المفتاح.
  it('كل أيام دورة W27 تحمل 2026-W27 (لا انقسام على الخميس)', () => {
    for (let d = 27; d <= 30; d++) expect(operationalWeekKey(utc(2026, 6, d))).toBe('2026-W27');
    for (let d = 1; d <= 3; d++) expect(operationalWeekKey(utc(2026, 7, d))).toBe('2026-W27');
    // اليوم التالي (السبت 4 يوليو) ينتقل إلى W28.
    expect(operationalWeekKey(utc(2026, 7, 4))).toBe('2026-W28');
  });
});

describe('weekKeysForPeriod — مفاتيح الفترة المخصّصة', () => {
  // فترة مخصّصة تغطّي دورتين متتاليتين تُرجِع مفتاحيهما بالترتيب بلا تكرار.
  it('من 2026-06-30 إلى 2026-07-06 → [2026-W27, 2026-W28]', () => {
    expect(weekKeysForPeriod({ preset: 'custom', from: '2026-06-30', to: '2026-07-06' })).toEqual([
      '2026-W27',
      '2026-W28',
    ]);
  });
});

describe('previousPeriodKey — الدورة الأسبوعية السابقة', () => {
  // الدورة السابقة لـ W28 هي W27 (سبت − 7).
  it("Weekly 2026-W28 → 2026-W27", () => {
    expect(previousPeriodKey('Weekly', '2026-W28')).toBe('2026-W27');
  });

  // حدود السنة: الدورة السابقة لأول دورة 2027 (W01، سبت 2 يناير 2027) هي 2026-W53.
  it('Weekly 2027-W01 → 2026-W53 (التفاف حدود السنة)', () => {
    expect(previousPeriodKey('Weekly', '2027-W01')).toBe('2026-W53');
  });
});

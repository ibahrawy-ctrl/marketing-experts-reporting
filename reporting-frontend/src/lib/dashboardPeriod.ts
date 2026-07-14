// RPT-ROLE-HOME-REPORT-CARDS-R1 — أدوات الفلتر الزمني للصفحة الرئيسية (Frontend-only).
// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.6: مواءمة مع مرساة السبت المعتمدة في ReportingCalendarPolicy الخادمية.
// دورة التقارير تبدأ السبت (على/قبل التاريخ) وتنتهي الجمعة (+6). رقم/سنة الدورة = ISO week لمرجع الثلاثاء (السبت+3).
// لا تستدعي أي endpoint جديد؛ تُستهلك فقط من compliance-summary القائم عبر weekKey.

import type { PeriodType } from '../types/api';

export type DashboardPreset =
  | 'current_week'
  | 'last_week'
  | 'last_2_weeks'
  | 'last_30_days'
  | 'last_90_days'
  | 'custom';

export interface DashboardPeriod {
  preset: DashboardPreset;
  from?: string; // 'yyyy-mm-dd' (custom فقط)
  to?: string; // 'yyyy-mm-dd' (custom فقط)
}

const DAY_MS = 86_400_000;
const WEEK_MS = 7 * DAY_MS;

// تاريخ تقويميّ (منتصف الليل UTC) — نتعامل مع كل التواريخ بأجزاء UTC لتفادي انزياح المنطقة الزمنية.
function utcDate(y: number, m: number, d: number): Date {
  return new Date(Date.UTC(y, m, d));
}

function addDays(d: Date, n: number): Date {
  return new Date(d.getTime() + n * DAY_MS);
}

// «اليوم» بتوقيت الرياض (UTC+3 ثابت بلا توقيت صيفي) كتاريخ تقويميّ UTC.
export function riyadhToday(): Date {
  const shifted = new Date(Date.now() + 3 * 3_600_000);
  return utcDate(shifted.getUTCFullYear(), shifted.getUTCMonth(), shifted.getUTCDate());
}

// السبت الذي تبدأ به دورة التقارير المحتوية للتاريخ (مطابق CycleStart الخادمي).
function saturdayOnOrBefore(date: Date): Date {
  const d = utcDate(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
  const diff = (d.getUTCDay() - 6 + 7) % 7; // DayOfWeek.Saturday = 6
  return addDays(d, -diff);
}

// ISO week لأي تاريخ عبر خميس أسبوع ISO المحتوي له (أسابيع ISO تبدأ الإثنين، الأسبوع 1 يحوي أول خميس).
function isoWeek(date: Date): { year: number; week: number } {
  const d = utcDate(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
  const dayNr = (d.getUTCDay() + 6) % 7; // الإثنين = 0 … الأحد = 6
  const thursday = addDays(d, 3 - dayNr); // خميس أسبوع ISO المحتوي للتاريخ
  const year = thursday.getUTCFullYear();
  let firstThursday = utcDate(year, 0, 4); // 4 يناير دائمًا في الأسبوع 1
  const ft = (firstThursday.getUTCDay() + 6) % 7;
  firstThursday = addDays(firstThursday, 3 - ft); // خميس الأسبوع 1
  const week = 1 + Math.round((thursday.getTime() - firstThursday.getTime()) / WEEK_MS);
  return { year, week };
}

// مفتاح دورة التقارير YYYY-Www لتاريخ معيّن (رقم/سنة الدورة = ISO week لمرجع الثلاثاء = السبت+3).
export function operationalWeekKey(date: Date): string {
  const tuesdayReference = addDays(saturdayOnOrBefore(date), 3);
  const { year, week } = isoWeek(tuesdayReference);
  return `${year}-W${String(week).padStart(2, '0')}`;
}

function parseDate(s?: string): Date | null {
  if (!s) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s.trim());
  if (!m) return null;
  return utcDate(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
}

// مدى التواريخ [from, to] الفعليّ للفترة (لحساب الأسابيع المتداخلة وتسمية الفترة).
export function periodRange(p: DashboardPeriod): { from: Date; to: Date } {
  const today = riyadhToday();
  switch (p.preset) {
    case 'current_week': {
      const s = saturdayOnOrBefore(today);
      return { from: s, to: addDays(s, 6) };
    }
    case 'last_week': {
      const s = saturdayOnOrBefore(addDays(today, -7));
      return { from: s, to: addDays(s, 6) };
    }
    case 'last_2_weeks': {
      const s = saturdayOnOrBefore(addDays(today, -7));
      return { from: s, to: addDays(saturdayOnOrBefore(today), 6) };
    }
    case 'last_30_days':
      return { from: addDays(today, -29), to: today };
    case 'last_90_days':
      return { from: addDays(today, -89), to: today };
    case 'custom': {
      const from = parseDate(p.from) ?? today;
      const to = parseDate(p.to) ?? today;
      return from <= to ? { from, to } : { from: to, to: from };
    }
  }
}

// مفاتيح دورات التقارير المتداخلة مع الفترة (الأقدم → الأحدث، بلا تكرار).
export function weekKeysForPeriod(p: DashboardPeriod): string[] {
  const { from, to } = periodRange(p);
  const keys: string[] = [];
  let cur = saturdayOnOrBefore(from);
  const end = saturdayOnOrBefore(to);
  while (cur.getTime() <= end.getTime()) {
    const k = operationalWeekKey(cur);
    if (!keys.includes(k)) keys.push(k);
    cur = addDays(cur, 7);
  }
  return keys.length > 0 ? keys : [operationalWeekKey(riyadhToday())];
}

function fmtDate(d: Date): string {
  const dd = String(d.getUTCDate()).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}/${d.getUTCFullYear()}`;
}

// تسمية الفترة المعروضة بجوار كل كارت.
export function periodLabel(p: DashboardPeriod): string {
  switch (p.preset) {
    case 'current_week':
      return 'الأسبوع الحالي';
    case 'last_week':
      return 'آخر أسبوع';
    case 'last_2_weeks':
      return 'آخر أسبوعين';
    case 'last_30_days':
      return 'آخر شهر';
    case 'last_90_days':
      return 'آخر 3 أشهر';
    case 'custom': {
      const { from, to } = periodRange(p);
      return `${fmtDate(from)} إلى ${fmtDate(to)}`;
    }
  }
}

// ===== مفاتيح الفترات لشاشة تجميع المبيعات (توليد داخلي — لا يراه المستخدم) =====

// مفتاح يومي YYYY-MM-DD لتاريخ تقويميّ.
export function dateKey(date: Date): string {
  const y = date.getUTCFullYear();
  const m = String(date.getUTCMonth() + 1).padStart(2, '0');
  const d = String(date.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

// مفتاح شهري YYYY-MM لتاريخ.
export function monthKeyFor(date: Date): string {
  const y = date.getUTCFullYear();
  const m = String(date.getUTCMonth() + 1).padStart(2, '0');
  return `${y}-${m}`;
}

// رقم الربع (1..4) لتاريخ.
export function quarterOf(date: Date): number {
  return Math.floor(date.getUTCMonth() / 3) + 1;
}

// تحليل مفتاح يومي YYYY-MM-DD إلى تاريخ تقويميّ UTC (أو null).
export function parseDateKey(s: string): Date | null {
  return parseDate(s);
}

// سبت بداية الدورة لمفتاح دورة معيّن (السنة + رقم الأسبوع) — عكس operationalWeekKey.
// نُعيّن ثلاثاء أسبوع ISO المطلوب (السبت+3) ثم نطرح 3 أيام للوصول للسبت.
function saturdayOfCycleKey(year: number, week: number): Date {
  let firstMonday = utcDate(year, 0, 4); // 4 يناير دائمًا في الأسبوع 1
  const fm = (firstMonday.getUTCDay() + 6) % 7; // الإثنين = 0
  firstMonday = addDays(firstMonday, -fm); // إثنين الأسبوع 1
  const monday = addDays(firstMonday, (week - 1) * 7); // إثنين الأسبوع المطلوب
  const tuesday = addDays(monday, 1); // مرجع الثلاثاء
  return addDays(tuesday, -3); // السبت الذي تبدأ به الدورة
}

// مفتاح الفترة السابقة لنفس النوع (إضافيّة — للمقارنة مع الفترة السابقة في لوحة القيادة).
// يومي ⇐ اليوم السابق؛ أسبوعي ⇐ الدورة السابقة (سبت − 7)؛ شهري ⇐ الشهر السابق؛ ربع سنوي ⇐ الربع السابق (يلتف على السنة).
export function previousPeriodKey(
  periodType: PeriodType,
  periodKey: string | null | undefined,
): string | undefined {
  if (!periodKey) return undefined;
  switch (periodType) {
    case 'Daily': {
      const d = parseDate(periodKey);
      return d ? dateKey(addDays(d, -1)) : undefined;
    }
    case 'Weekly': {
      const m = /^(\d{4})-W(\d{2})$/.exec(periodKey.trim());
      if (!m) return undefined;
      const saturday = saturdayOfCycleKey(Number(m[1]), Number(m[2]));
      return operationalWeekKey(addDays(saturday, -7));
    }
    case 'Monthly': {
      const m = /^(\d{4})-(\d{2})$/.exec(periodKey.trim());
      if (!m) return undefined;
      let y = Number(m[1]);
      let mo = Number(m[2]) - 1; // 0-based
      mo -= 1;
      if (mo < 0) {
        mo = 11;
        y -= 1;
      }
      return `${y}-${String(mo + 1).padStart(2, '0')}`;
    }
    case 'Quarterly': {
      const m = /^(\d{4})-Q([1-4])$/.exec(periodKey.trim());
      if (!m) return undefined;
      let y = Number(m[1]);
      let q = Number(m[2]) - 1;
      if (q < 1) {
        q = 4;
        y -= 1;
      }
      return `${y}-Q${q}`;
    }
    default:
      return undefined;
  }
}

export const PRESET_OPTIONS: { value: DashboardPreset; label: string }[] = [
  { value: 'current_week', label: 'الأسبوع الحالي' },
  { value: 'last_week', label: 'آخر أسبوع' },
  { value: 'last_2_weeks', label: 'آخر أسبوعين' },
  { value: 'last_30_days', label: 'آخر شهر' },
  { value: 'last_90_days', label: 'آخر 3 أشهر' },
];

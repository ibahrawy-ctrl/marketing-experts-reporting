// اختبارات تطبيع روابط الإشعارات (OFFICIAL-LAUNCH-FIX-PACK-R1A).
// تثبت أنّ الروابط القديمة/المكسورة تُحوَّل لمسارات SPA آمنة، وأنّ أي رابط غير معروف يؤول إلى /app
// (لا صفحة بيضاء)، وأنّ الروابط الصحيحة لا تتغيّر.
import { describe, it, expect } from 'vitest';
import { normalizeNotificationLink } from './notificationLink';

describe('normalizeNotificationLink', () => {
  it('1) يحوّل /submissions/{id} إلى /app/submissions?open={id}', () => {
    expect(normalizeNotificationLink('/submissions/abc-123')).toBe('/app/submissions?open=abc-123');
  });

  it('2) يحوّل /app/submissions/{id} إلى /app/submissions?open={id}', () => {
    expect(normalizeNotificationLink('/app/submissions/xyz-999')).toBe('/app/submissions?open=xyz-999');
  });

  it('3) لا يغيّر الروابط الصحيحة مثل /app/hr-requests', () => {
    expect(normalizeNotificationLink('/app/hr-requests')).toBe('/app/hr-requests');
    expect(normalizeNotificationLink('/app/leave-requests?tab=pending')).toBe('/app/leave-requests?tab=pending');
    expect(normalizeNotificationLink('/app')).toBe('/app');
  });

  it('4) الروابط غير المعروفة لا تكسر الصفحة (fallback إلى /app)', () => {
    expect(normalizeNotificationLink('/foo/bar/123')).toBe('/app');
    expect(normalizeNotificationLink('/unknown-route')).toBe('/app');
  });

  it('5) الروابط بمعرّف بلا Route تؤول لصفحة قائمة آمنة (لا صفحة بيضاء)', () => {
    expect(normalizeNotificationLink('/kpi-evaluations/1')).toBe('/app/my-kpi');
    expect(normalizeNotificationLink('/training-needs/1')).toBe('/app/development');
    expect(normalizeNotificationLink('/improvement-plans/1')).toBe('/app/development');
    expect(normalizeNotificationLink('/escalations/1')).toBe('/app/governance/escalations');
  });

  it('6) القيم الفارغة/الخالية لا تكسر (تؤول إلى /app)', () => {
    expect(normalizeNotificationLink(null)).toBe('/app');
    expect(normalizeNotificationLink(undefined)).toBe('/app');
    expect(normalizeNotificationLink('')).toBe('/app');
    expect(normalizeNotificationLink('   ')).toBe('/app');
  });

  it('يُسبق مسارات القائمة المعروفة بلا بادئة بـ /app مع الحفاظ على الـ query', () => {
    expect(normalizeNotificationLink('/submissions?period=2026-W26')).toBe('/app/submissions?period=2026-W26');
    expect(normalizeNotificationLink('/hr-requests')).toBe('/app/hr-requests');
    expect(normalizeNotificationLink('/leave-requests')).toBe('/app/leave-requests');
  });
});

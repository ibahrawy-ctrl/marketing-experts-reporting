import { describe, it, expect } from 'vitest';
import {
  unifiedBannerTone,
  unifiedBannerCta,
  unifiedUrgency,
  unifiedActionTo,
  selectBannerCycleUnified,
  unifiedEmployeeBanner,
} from './unifiedBanner';
import type { ReportingCycleDto, UnifiedReportCycleStatusDto } from '../types/api';

// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — PHASE 5: اختبارات نقيّة للّافتة الموحّدة.
// نتأكّد أن اللافتة تتبع الحالة الموحّدة الخادميّة (isCurrentPriority/severity/statusLabel/actionUrl)
// لا mine[0] المحليّ، وأن الدورة الماضية المتأخّرة غير المُسلَّمة تقود اللافتة رغم غياب أحدث تسليم.
function unified(overrides: Partial<UnifiedReportCycleStatusDto> = {}): UnifiedReportCycleStatusDto {
  return {
    templateId: null,
    templateVersionId: null,
    templateName: 'قالب',
    periodType: 'Weekly',
    periodKey: '2026-W29',
    cycleLabel: 'الأسبوع 29 — 2026',
    cycleStartDate: '2026-07-11',
    cycleEndDate: '2026-07-17',
    dueAt: '2026-07-15',
    assignmentId: null,
    isAssigned: true,
    submissionId: null,
    submissionStatus: null,
    submittedAt: null,
    approvedAt: null,
    closedAt: null,
    hasSubmission: false,
    isLate: false,
    delayDays: 0,
    unifiedStatus: 'DueNow',
    statusLabel: 'مستحقّ الآن',
    statusDescription: 'أنجز تقرير هذه الدورة قبل الموعد.',
    severity: 'info',
    availableActions: [],
    actionUrl: '/submissions?period=2026-W29',
    isCurrentPriority: true,
    ...overrides,
  };
}

function cycle(overrides: Partial<ReportingCycleDto> = {}): ReportingCycleDto {
  return { isCurrent: false, unified: null, ...overrides } as ReportingCycleDto;
}

describe('unifiedBannerTone', () => {
  it('alert و warn ⇒ برتقالي', () => {
    expect(unifiedBannerTone('alert')).toBe('orange');
    expect(unifiedBannerTone('warn')).toBe('orange');
  });
  it('success ⇒ أخضر، info/none ⇒ كحليّ', () => {
    expect(unifiedBannerTone('success')).toBe('success');
    expect(unifiedBannerTone('info')).toBe('navy');
    expect(unifiedBannerTone('none')).toBe('navy');
  });
});

describe('unifiedBannerCta', () => {
  it('نصّ الزرّ يتبع نوع الحالة الموحّدة', () => {
    expect(unifiedBannerCta('DueNow')).toBe('ابدأ التقرير');
    expect(unifiedBannerCta('OverdueNotSubmitted')).toBe('ابدأ التقرير');
    expect(unifiedBannerCta('Draft')).toBe('أكمل التقرير');
    expect(unifiedBannerCta('OverdueDraft')).toBe('أكمل التقرير');
    expect(unifiedBannerCta('ReturnedForChanges')).toBe('عدّل الآن');
    expect(unifiedBannerCta('OverdueReturned')).toBe('عدّل الآن');
    expect(unifiedBannerCta('Closed')).toBe('عرض التقرير');
    expect(unifiedBannerCta('PendingApproval')).toBe('متابعة الحالة');
  });
});

describe('unifiedUrgency', () => {
  it('alert⇒high، warn⇒medium، غيرها⇒low', () => {
    expect(unifiedUrgency('alert')).toBe('high');
    expect(unifiedUrgency('warn')).toBe('medium');
    expect(unifiedUrgency('success')).toBe('low');
    expect(unifiedUrgency('info')).toBe('low');
  });
});

describe('unifiedActionTo', () => {
  it('يضيف بادئة /app لمسار الخادم مرّةً واحدةً بلا تكرار', () => {
    expect(unifiedActionTo('/submissions?period=2026-W29')).toBe('/app/submissions?period=2026-W29');
    expect(unifiedActionTo('/app/submissions')).toBe('/app/submissions');
    expect(unifiedActionTo('')).toBe('/app/submissions');
  });
});

describe('selectBannerCycleUnified', () => {
  it('يختار الدورة التي عيّنها الخادم isCurrentPriority لا الدورة الحالية بالضرورة', () => {
    const past = cycle({ unified: unified({ periodKey: '2026-W28', unifiedStatus: 'OverdueNotSubmitted', severity: 'alert', isLate: true, isCurrentPriority: true }) });
    const now = cycle({ isCurrent: true, unified: unified({ periodKey: '2026-W29', unifiedStatus: 'DueNow', isCurrentPriority: false }) });
    const sel = selectBannerCycleUnified([past, now]);
    expect(sel?.periodKey).toBe('2026-W28');
    expect(sel?.unifiedStatus).toBe('OverdueNotSubmitted');
  });

  it('عند غياب أولويّة إجراء يعود للدورة الحالية (حالة إعلاميّة)', () => {
    const now = cycle({ isCurrent: true, unified: unified({ periodKey: '2026-W29', unifiedStatus: 'PendingApproval', severity: 'info', isCurrentPriority: false }) });
    expect(selectBannerCycleUnified([now])?.unifiedStatus).toBe('PendingApproval');
  });

  it('يرجع null عند غياب أيّ دورة موحّدة (⇒ المسار القديم)', () => {
    expect(selectBannerCycleUnified(undefined)).toBeNull();
    expect(selectBannerCycleUnified([])).toBeNull();
    expect(selectBannerCycleUnified([cycle({ unified: null })])).toBeNull();
  });
});

describe('unifiedEmployeeBanner', () => {
  it('يبني اللافتة من تسميات/شدّة/رابط الخادم للدورة المتأخّرة غير المُسلَّمة', () => {
    const b = unifiedEmployeeBanner(unified({ unifiedStatus: 'OverdueNotSubmitted', statusLabel: 'متأخّر غير مُسلَّم', statusDescription: 'انقضى الموعد ولم تُسلِّم.', severity: 'alert', actionUrl: '/submissions?period=2026-W28' }));
    expect(b.title).toBe('متأخّر غير مُسلَّم');
    expect(b.description).toBe('انقضى الموعد ولم تُسلِّم.');
    expect(b.tone).toBe('orange');
    expect(b.cta).toBe('ابدأ التقرير');
    expect(b.to).toBe('/app/submissions?period=2026-W28');
  });

  it('حالة مُسلَّم في الوقت ⇒ أخضر + متابعة الحالة', () => {
    const b = unifiedEmployeeBanner(unified({ unifiedStatus: 'SubmittedOnTime', statusLabel: 'سُلّم في الوقت', severity: 'success' }));
    expect(b.tone).toBe('success');
    expect(b.cta).toBe('متابعة الحالة');
  });
});

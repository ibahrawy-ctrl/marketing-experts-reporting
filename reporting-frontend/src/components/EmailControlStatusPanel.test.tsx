// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — المرحلة 12: اختبارات الواجهة (24 حالة).
//
// تُختبَر لوحة الحالة عبر مكوّن العرض الخالص EmailControlStatusView (بلا شبكة)،
// ثمّ تُختبَر عدم الارتداد على صفحة مركز التحكّم (التبويبات الأربعة، والقواعد، والتذكير اليدويّ).
// مبدأ حاكم: الوضع يأتي من الحقل mode حصرًا — لا يُشتقّ من العلم القديم legacyEmailEnabled.
import { render, screen, within, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EmailControlStatusView } from './EmailControlStatusPanel';
import type { EmailControlCenterStatusDto, EmailControlStatusWarningDto } from '../types/api';

// ===== مُولِّد حالة أساسيّة (كلّ اختبار يُبدِّل ما يخصّه فقط) =====

function makeStatus(over: Partial<EmailControlCenterStatusDto> = {}): EmailControlCenterStatusDto {
  return {
    mode: 'DryRun',
    isLiveSendingEnabled: false,
    schedulerEnabled: false,
    pollMinutes: 15,
    dailyDueHour: 16,
    weeklyDueHour: 9,
    overdueHour: 9,
    summaryHour: 9,
    reviewHour: 9,
    timeZoneId: 'Asia/Riyadh',
    timeZoneLabel: 'توقيت الرياض (UTC+3)',
    environmentName: 'ReleaseCandidate',
    smtpConfigured: false,
    smtpHost: null,
    smtpPort: null,
    usesTls: true,
    senderAddress: null,
    credentialConfigured: false,
    legacyEmailEnabled: false,
    legacyFlagIsAuthoritative: false,
    totalNotifications: 0,
    historicalDryRunCount: 0,
    enabledCount: 0,
    sentCount: 0,
    pendingCount: 0,
    processingCount: 0,
    failedCount: 0,
    outboxCount: 0,
    lastNotificationCreatedAtUtc: null,
    lastSentAtUtc: null,
    lastFailureAtUtc: null,
    lastScheduledNotificationCreatedAtUtc: null,
    checkedAtUtc: '2026-07-29T13:00:00Z',
    warnings: [],
    ...over,
  };
}

/** يُشبه حالة الإنتاج الحاليّة: إرسال فعليّ + مجدول مفعَّل + SMTP جاهز + علم قديم معطّل. */
function productionLike(over: Partial<EmailControlCenterStatusDto> = {}) {
  return makeStatus({
    mode: 'Enabled',
    isLiveSendingEnabled: true,
    schedulerEnabled: true,
    environmentName: 'Production',
    smtpConfigured: true,
    smtpHost: 'smtp.gmail.com',
    smtpPort: 587,
    senderAddress: 'info@example.test',
    credentialConfigured: true,
    legacyEmailEnabled: false,
    totalNotifications: 188,
    historicalDryRunCount: 139,
    enabledCount: 49,
    sentCount: 49,
    lastNotificationCreatedAtUtc: '2026-07-29T13:10:43Z',
    lastSentAtUtc: '2026-07-29T13:10:44Z',
    lastScheduledNotificationCreatedAtUtc: '2026-07-29T13:10:43Z',
    ...over,
  });
}

function renderView(
  status: EmailControlCenterStatusDto | undefined,
  extra: { isLoading?: boolean; isError?: boolean; isFetching?: boolean; onRefresh?: () => void } = {},
) {
  const onRefresh = extra.onRefresh ?? vi.fn();
  const utils = render(
    <div dir="rtl">
      <EmailControlStatusView
        status={status}
        isLoading={extra.isLoading ?? false}
        isError={extra.isError ?? false}
        isFetching={extra.isFetching ?? false}
        onRefresh={onRefresh}
      />
    </div>,
  );
  return { ...utils, onRefresh };
}

/** يُرجِع البطاقة التي تحوي عنوانًا مُعطى — لحصر التأكيدات داخل قسمها. */
function section(title: string): HTMLElement {
  const heading = screen.getByRole('heading', { name: title });
  return heading.parentElement as HTMLElement;
}

/** يقرأ قيمة صفّ «التسمية ⟵ القيمة» داخل قسم. */
function rowValue(sectionTitle: string, label: string): string {
  const scope = section(sectionTitle);
  const labelEl = within(scope).getByText(label);
  const row = labelEl.parentElement as HTMLElement;
  return (row.lastElementChild?.textContent ?? '').trim();
}

// ===== 1-3) الأوضاع الثلاثة =====

describe('EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — عرض الوضع', () => {
  it('1) Mode=Enabled ⇒ يعرض «الإرسال الفعلي مفعّل» وشارة LIVE / ENABLED', () => {
    renderView(productionLike());
    expect(screen.getByRole('heading', { name: 'الإرسال الفعلي مفعّل' })).toBeInTheDocument();
    expect(screen.getByText('LIVE / ENABLED')).toBeInTheDocument();
    expect(screen.queryByText('DRY RUN')).not.toBeInTheDocument();
    expect(screen.queryByText(/وضع المحاكاة مفعّل/)).not.toBeInTheDocument();
  });

  it('2) Mode=DryRun ⇒ يعرض «وضع المحاكاة مفعّل» و«يتم تسجيل الإشعارات دون إرسال بريد حقيقي»', () => {
    renderView(makeStatus({ mode: 'DryRun' }));
    expect(
      screen.getByRole('heading', { name: 'وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية' }),
    ).toBeInTheDocument();
    expect(screen.getByText('يتم تسجيل الإشعارات دون إرسال بريد حقيقي.')).toBeInTheDocument();
    expect(screen.getByText('DRY RUN')).toBeInTheDocument();
    expect(screen.queryByText('LIVE / ENABLED')).not.toBeInTheDocument();
  });

  it('3) Mode=Disabled ⇒ يعرض «نظام إشعارات البريد متوقف» وشارة DISABLED', () => {
    renderView(makeStatus({ mode: 'Disabled' }));
    expect(screen.getByRole('heading', { name: 'نظام إشعارات البريد متوقف' })).toBeInTheDocument();
    expect(screen.getByText('DISABLED')).toBeInTheDocument();
    expect(screen.queryByText('LIVE / ENABLED')).not.toBeInTheDocument();
  });

  it('4) سجلّات DryRun التاريخيّة تُعرَض في قسم منفصل ولا تُغيّر الوضع الحاليّ', () => {
    renderView(productionLike({ historicalDryRunCount: 139 }));

    // الوضع الحاليّ ما زال «الإرسال الفعلي مفعّل».
    expect(screen.getByRole('heading', { name: 'الإرسال الفعلي مفعّل' })).toBeInTheDocument();

    // والعدّاد التاريخيّ داخل قسم «السجلّ التاريخيّ» مع تنويه صريح.
    const history = section('السجلّ التاريخيّ');
    expect(within(history).getByText('سجلّ تاريخيّ بوضع المحاكاة')).toBeInTheDocument();
    expect(within(history).getByText('139')).toBeInTheDocument();
    expect(
      within(history).getByText('أرقام تراكميّة لصفوف سُجِّلت سابقًا — لا تعبّر عن الوضع الحاليّ للنظام.'),
    ).toBeInTheDocument();
  });

  it('5) Mode=Enabled مع العلم القديم معطّل ⇒ لا يُقال إنّ البريد متوقف، ويظهر توضيح التوافق', () => {
    renderView(productionLike({ legacyEmailEnabled: false }));

    expect(screen.getByRole('heading', { name: 'الإرسال الفعلي مفعّل' })).toBeInTheDocument();
    expect(screen.queryByText(/نظام إشعارات البريد متوقف/)).not.toBeInTheDocument();

    const legacy = section('إعدادات التوافق القديمة');
    expect(
      within(legacy).getByText('القناة الجديدة مفعلة، بينما العلم القديم معطل ويخص مسارات البريد القديمة فقط.'),
    ).toBeInTheDocument();
    expect(rowValue('إعدادات التوافق القديمة', 'Legacy Email Flag (Email:Enabled)')).toBe('معطّل');
    expect(rowValue('إعدادات التوافق القديمة', 'هل هو مصدر حقيقة لهذه القناة؟')).toBe('لا');
  });
});

// ===== 6-7) المجدول =====

describe('جدول التشغيل', () => {
  it('6) المجدول مفعَّل ⇒ يظهر «مُفعَّل» مع النوافذ وفترة النبض', () => {
    renderView(productionLike({ schedulerEnabled: true, pollMinutes: 15, dailyDueHour: 16 }));
    expect(rowValue('جدول التشغيل', 'المجدول')).toBe('مُفعَّل');
    expect(rowValue('جدول التشغيل', 'فترة النبض')).toBe('كل 15 دقيقة');
    expect(rowValue('جدول التشغيل', 'نافذة التقارير اليوميّة')).toBe('16:00');
    expect(rowValue('جدول التشغيل', 'المنطقة الزمنيّة')).toBe('توقيت الرياض (UTC+3) — Asia/Riyadh');
  });

  it('7) المجدول معطّل ⇒ يظهر «معطّل»، والنافذة الفارغة تُعرَض «بلا نافذة»', () => {
    renderView(makeStatus({ schedulerEnabled: false, dailyDueHour: null }));
    expect(rowValue('جدول التشغيل', 'المجدول')).toBe('معطّل');
    expect(rowValue('جدول التشغيل', 'نافذة التقارير اليوميّة')).toBe('بلا نافذة');
  });
});

// ===== 8-9) جاهزيّة SMTP =====

describe('جاهزيّة SMTP', () => {
  it('8) SMTP مُهيَّأ ⇒ يعرض المضيف والمنفذ والمُرسِل و«مضبوطة» لبيانات الاعتماد', () => {
    renderView(productionLike());
    const smtp = 'حالة المعالجة وجاهزيّة SMTP';
    expect(rowValue(smtp, 'جاهزيّة SMTP')).toBe('مُهيَّأ');
    expect(rowValue(smtp, 'مضيف SMTP')).toBe('smtp.gmail.com');
    expect(rowValue(smtp, 'المنفذ')).toBe('587');
    expect(rowValue(smtp, 'STARTTLS')).toBe('نعم');
    expect(rowValue(smtp, 'عنوان المُرسِل')).toBe('info@example.test');
    expect(rowValue(smtp, 'بيانات الاعتماد')).toBe('مضبوطة');
  });

  it('9) SMTP غير مُهيَّأ ⇒ «غير مُهيَّأ» والقيم الغائبة تُعرَض «—» بلا انهيار', () => {
    renderView(makeStatus({ smtpConfigured: false, smtpHost: null, smtpPort: null, senderAddress: null }));
    const smtp = 'حالة المعالجة وجاهزيّة SMTP';
    expect(rowValue(smtp, 'جاهزيّة SMTP')).toBe('غير مُهيَّأ');
    expect(rowValue(smtp, 'مضيف SMTP')).toBe('—');
    expect(rowValue(smtp, 'المنفذ')).toBe('—');
    expect(rowValue(smtp, 'عنوان المُرسِل')).toBe('—');
    expect(rowValue(smtp, 'بيانات الاعتماد')).toBe('غير مضبوطة');
  });
});

// ===== 10-11) العدّادات =====

describe('العدّادات', () => {
  it('10) العدّادات تُعرَض بقيمها الصحيحة في قسمَيها', () => {
    renderView(
      productionLike({
        totalNotifications: 188,
        historicalDryRunCount: 139,
        enabledCount: 49,
        sentCount: 49,
        pendingCount: 3,
        processingCount: 2,
        failedCount: 1,
        outboxCount: 4,
      }),
    );

    const proc = section('حالة المعالجة وجاهزيّة SMTP');
    for (const [label, value] of [
      ['قيد الانتظار', '3'],
      ['قيد المعالجة', '2'],
      ['فاشلة', '1'],
      ['صندوق الصادر القديم', '4'],
    ] as const) {
      const el = within(proc).getByText(label);
      expect((el.parentElement as HTMLElement).textContent).toContain(value);
    }

    const hist = section('السجلّ التاريخيّ');
    for (const [label, value] of [
      ['إجمالي الإشعارات', '188'],
      ['سجلّ تاريخيّ بوضع المحاكاة', '139'],
      ['سجلّ بوضع الإرسال الفعليّ', '49'],
      ['أُرسِلت فعليًّا', '49'],
    ] as const) {
      const el = within(hist).getByText(label);
      expect((el.parentElement as HTMLElement).textContent).toContain(value);
    }
  });

  it('11) القيم الصفريّة تُعرَض «0» صراحةً ولا تُخفى', () => {
    renderView(makeStatus());
    const proc = section('حالة المعالجة وجاهزيّة SMTP');
    for (const label of ['قيد الانتظار', 'قيد المعالجة', 'فاشلة', 'صندوق الصادر القديم']) {
      const el = within(proc).getByText(label);
      expect((el.parentElement as HTMLElement).textContent).toContain('0');
    }
    const hist = section('السجلّ التاريخيّ');
    const total = within(hist).getByText('إجمالي الإشعارات');
    expect((total.parentElement as HTMLElement).textContent).toContain('0');
  });
});

// ===== 12-13) الطوابع الزمنيّة =====

describe('آخر نشاط', () => {
  it('12) الطوابع الزمنيّة الموجودة تُعرَض مُنسَّقة لا كسلاسل ISO خام', () => {
    renderView(productionLike({ lastSentAtUtc: '2026-07-29T13:10:44Z' }));
    const value = rowValue('آخر نشاط', 'آخر إرسال ناجح');
    expect(value).not.toBe('—');
    expect(value).not.toContain('2026-07-29T13:10:44Z');
  });

  it('13) الطوابع الزمنيّة الفارغة تُعرَض «—» بلا انهيار', () => {
    renderView(
      makeStatus({
        lastNotificationCreatedAtUtc: null,
        lastSentAtUtc: null,
        lastFailureAtUtc: null,
        lastScheduledNotificationCreatedAtUtc: null,
      }),
    );
    expect(rowValue('آخر نشاط', 'آخر إشعار مُسجَّل')).toBe('—');
    expect(rowValue('آخر نشاط', 'آخر إرسال ناجح')).toBe('—');
    expect(rowValue('آخر نشاط', 'آخر إخفاق')).toBe('—');
    expect(rowValue('آخر نشاط', 'آخر إشعار من فئة مجدوَلة')).toBe('—');
  });

  it('14) «آخر إشعار من فئة مجدوَلة» مصحوب بتنويه أنّه ليس «آخر تشغيل للمجدول»', () => {
    renderView(productionLike());
    const activity = section('آخر نشاط');
    expect(within(activity).getByText(/ليس «آخر تشغيل للمجدول»/)).toBeInTheDocument();
    expect(screen.queryByText(/آخر تشغيل للمجدول:/)).not.toBeInTheDocument();
  });
});

// ===== 15-16) التنبيهات =====

describe('التنبيهات', () => {
  it('15) التنبيهات تُعرَض بدرجات خطورتها العربيّة', () => {
    const warnings: EmailControlStatusWarningDto[] = [
      { severity: 'Critical', code: 'live_without_smtp', message: 'الإرسال الفعليّ مفعَّل بينما SMTP غير مُهيَّأ.' },
      { severity: 'Warning', code: 'scheduler_disabled', message: 'المجدول معطّل.' },
      { severity: 'Info', code: 'legacy_flag_not_authoritative', message: 'العلم القديم ليس مصدر حقيقة.' },
    ];
    renderView(productionLike({ smtpConfigured: false, warnings }));

    const box = section('التنبيهات');
    expect(within(box).getByText('حرِج:')).toBeInTheDocument();
    expect(within(box).getByText('الإرسال الفعليّ مفعَّل بينما SMTP غير مُهيَّأ.')).toBeInTheDocument();
    expect(within(box).getByText('تحذير:')).toBeInTheDocument();
    expect(within(box).getByText('معلومة:')).toBeInTheDocument();
  });

  it('16) بلا تنبيهات ⇒ «لا توجد تنبيهات.»', () => {
    renderView(productionLike({ warnings: [] }));
    expect(within(section('التنبيهات')).getByText('لا توجد تنبيهات.')).toBeInTheDocument();
  });
});

// ===== 17-20) حالات التحميل والخطأ والتحديث =====

describe('التحميل والخطأ والتحديث', () => {
  it('17) أثناء التحميل يظهر مؤشّر القراءة ولا تظهر أيّ قيم حالة', () => {
    renderView(undefined, { isLoading: true });
    expect(screen.getByText('يتم قراءة الحالة التشغيليّة…')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'جدول التشغيل' })).not.toBeInTheDocument();
  });

  it('18) عند فشل الطلب تظهر رسالة واضحة وزرّ إعادة المحاولة', () => {
    const { onRefresh } = renderView(undefined, { isError: true });
    expect(screen.getByText('تعذّر قراءة الحالة التشغيليّة لقناة البريد.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('19) رفض الصلاحية (401/403) يُعامَل كخطأ قراءة ولا يُعرَض كوضع «متوقف»', () => {
    renderView(undefined, { isError: true });
    expect(screen.queryByRole('heading', { name: 'نظام إشعارات البريد متوقف' })).not.toBeInTheDocument();
    expect(screen.queryByText('DISABLED')).not.toBeInTheDocument();
    expect(screen.getByText('تعذّر قراءة الحالة التشغيليّة لقناة البريد.')).toBeInTheDocument();
  });

  it('20) زرّ «تحديث» يستدعي القراءة، ووقت آخر فحص معروض', () => {
    const { onRefresh } = renderView(productionLike());
    expect(screen.getByText(/آخر فحص:/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'تحديث' }));
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });
});

// ===== 21-22) الاستجابة والاتّجاه والأسرار =====

describe('العرض الآمن والمتجاوب', () => {
  it('21) التخطيط متجاوب (شبكة تنهار لعمود واحد) ولا يفرض اتّجاهًا لاتينيًّا داخل RTL', () => {
    const { container } = renderView(productionLike());
    // شبكة القسمين الوسطى: عمود واحد افتراضيًّا، عمودان على الشاشات الكبيرة ⇒ لا تجاوز على 375px.
    expect(container.querySelector('.grid.gap-4.lg\\:grid-cols-2')).not.toBeNull();
    // عدّادات المعالجة: عمودان على الجوّال، أربعة على الأكبر.
    expect(container.querySelector('.grid-cols-2.sm\\:grid-cols-4')).not.toBeNull();
    // لا عنصر يقلب الاتّجاه إلى LTR داخل الغلاف RTL.
    expect(container.querySelector('[dir="ltr"]')).toBeNull();
  });

  it('22) لا يظهر أيّ سرّ في DOM — جاهزيّة الاعتماد قيمة منطقيّة فقط', () => {
    const { container } = renderView(productionLike({ credentialConfigured: true }));
    const text = container.textContent ?? '';
    for (const forbidden of ['password', 'Password', 'كلمة المرور', 'secret', 'Secret', 'ConnectionString', 'apiKey']) {
      expect(text).not.toContain(forbidden);
    }
    expect(rowValue('حالة المعالجة وجاهزيّة SMTP', 'بيانات الاعتماد')).toBe('مضبوطة');
  });
});

// ===== 23-24) عدم الارتداد على صفحة مركز التحكّم =====

vi.mock('../lib/useEmailControl', () => {
  const idleMutation = () => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    data: undefined,
    reset: vi.fn(),
  });
  return {
    useEmailControlStatus: () => ({
      data: {
        mode: 'DryRun',
        isLiveSendingEnabled: false,
        schedulerEnabled: false,
        pollMinutes: 15,
        dailyDueHour: 16,
        weeklyDueHour: 9,
        overdueHour: 9,
        summaryHour: 9,
        reviewHour: 9,
        timeZoneId: 'Asia/Riyadh',
        timeZoneLabel: 'توقيت الرياض (UTC+3)',
        environmentName: 'ReleaseCandidate',
        smtpConfigured: false,
        smtpHost: null,
        smtpPort: null,
        usesTls: true,
        senderAddress: null,
        credentialConfigured: false,
        legacyEmailEnabled: false,
        legacyFlagIsAuthoritative: false,
        totalNotifications: 0,
        historicalDryRunCount: 0,
        enabledCount: 0,
        sentCount: 0,
        pendingCount: 0,
        processingCount: 0,
        failedCount: 0,
        outboxCount: 0,
        lastNotificationCreatedAtUtc: null,
        lastSentAtUtc: null,
        lastFailureAtUtc: null,
        lastScheduledNotificationCreatedAtUtc: null,
        checkedAtUtc: '2026-07-29T13:00:00Z',
        warnings: [],
      },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    }),
    useEmailTemplates: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
    useUpdateEmailTemplate: idleMutation,
    usePreviewEmailTemplate: idleMutation,
    useEmailRules: () => ({
      data: [
        {
          id: 'r1',
          eventType: 'report-weekly-due',
          templateKey: 'report-weekly-due',
          isEnabled: true,
          sendToEmployee: true,
          sendToManager: false,
          sendToTeamLeader: false,
          sendToHr: false,
          sendToGovernance: false,
          sendToAdmin: false,
          cooldownMinutes: 60,
          mode: 'DryRun',
        },
      ],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    }),
    useUpdateEmailRule: idleMutation,
    usePreviewRecipients: idleMutation,
    useManualReminderDryRun: idleMutation,
  };
});

vi.mock('../lib/useDirectory', () => ({
  useDirectoryUsers: () => ({ data: [], isLoading: false }),
  useDepartments: () => ({ data: [], isLoading: false }),
  useTeams: () => ({ data: [], isLoading: false }),
  useJobRoles: () => ({ data: [], isLoading: false }),
}));

vi.mock('../pages/EmailNotificationsPage', () => ({
  default: () => <div>سجلّ الرسائل (بديل اختباريّ)</div>,
}));

describe('عدم الارتداد على مركز التحكّم بالبريد', () => {
  beforeEach(() => vi.clearAllMocks());

  it('23) التبويبات الأربعة تعمل، ولوحة الحالة تظهر فوق الصفحة مباشرةً', async () => {
    const { default: EmailControlCenterPage } = await import('../pages/EmailControlCenterPage');
    render(
      <div dir="rtl">
        <EmailControlCenterPage />
      </div>,
    );

    // لوحة الحالة ظاهرة فورًا بلا الحاجة لتبويب.
    expect(
      screen.getByRole('heading', { name: 'وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية' }),
    ).toBeInTheDocument();

    // التبويبات الأربعة القديمة باقية وتُبدَّل.
    for (const label of ['القوالب', 'القواعد', 'تذكير يدويّ', 'السجل']) {
      expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
    }

    fireEvent.click(screen.getByRole('button', { name: 'القواعد' }));
    expect(screen.getByText('report-weekly-due')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'السجل' }));
    expect(screen.getByText('سجلّ الرسائل (بديل اختباريّ)')).toBeInTheDocument();
  });

  it('24) وضع تسليم القواعد لم يتغيّر (DryRun/معطّل فقط) — Enabled ليس خيارًا هنا', async () => {
    const { default: EmailControlCenterPage } = await import('../pages/EmailControlCenterPage');
    render(
      <div dir="rtl">
        <EmailControlCenterPage />
      </div>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'القواعد' }));
    const modeSelect = screen.getByLabelText('الوضع') as HTMLSelectElement;
    const options = Array.from(modeSelect.options).map((o) => o.value);
    expect(options).toEqual(['DryRun', 'Disabled']);
    expect(options).not.toContain('Enabled');
  });
});

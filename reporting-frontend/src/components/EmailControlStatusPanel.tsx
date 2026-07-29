// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — لوحة الحالة التشغيليّة الحيّة لقناة البريد.
//
// قراءة فقط بالكامل: تعرض ما يقرؤه التطبيق من الإعدادات الحيّة وعدّادات القاعدة، بلا أيّ كتابة.
// مصدر الحقيقة للوضع = EmailNotifications:Mode (يصل عبر الحقل mode)، ولا يُشتقّ من أيّ علم قديم.
// بلا أسرار: لا تعرض كلمة مرور ولا طولها ولا بصمتها — جاهزيّة الاعتماد قيمة منطقيّة واحدة فقط.
import { Alert, Badge, Button, Card } from './ui';
import { LoadingState, QueryError } from './states';
import { formatDateTime } from '../lib/format';
import { useEmailControlStatus } from '../lib/useEmailControl';
import type { EmailControlCenterStatusDto, EmailControlStatusWarningDto } from '../types/api';

// ===== وصف الوضع الحاليّ (المصدر الوحيد: الحقل mode) =====

type ModePresentation = {
  badge: string;
  badgeTone: 'success' | 'gold' | 'alert' | 'muted';
  title: string;
  description: string;
};

function describeMode(mode: string): ModePresentation {
  switch (mode) {
    case 'Enabled':
      return {
        badge: 'LIVE / ENABLED',
        badgeTone: 'success',
        title: 'الإرسال الفعلي مفعّل',
        description: 'يتم إرسال رسائل بريد حقيقيّة إلى المستقبِلين عند تحقّق شروط الإرسال.',
      };
    case 'DryRun':
      return {
        badge: 'DRY RUN',
        badgeTone: 'gold',
        title: 'وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية',
        description: 'يتم تسجيل الإشعارات دون إرسال بريد حقيقي.',
      };
    case 'Disabled':
      return {
        badge: 'DISABLED',
        badgeTone: 'muted',
        title: 'نظام إشعارات البريد متوقف',
        description: 'لا يتم تسجيل إشعارات جديدة ولا إرسال أيّ بريد.',
      };
    default:
      return {
        badge: 'INVALID',
        badgeTone: 'alert',
        title: 'تعذّر قراءة وضع البريد من الإعدادات',
        description: 'قيمة EmailNotifications:Mode غير صالحة — راجع التنبيهات أدناه.',
      };
  }
}

const severityTone: Record<string, 'alert' | 'gold' | 'navy'> = {
  Critical: 'alert',
  Warning: 'gold',
  Info: 'navy',
};

const severityLabel: Record<string, string> = {
  Critical: 'حرِج',
  Warning: 'تحذير',
  Info: 'معلومة',
};

// ===== عناصر عرض صغيرة =====

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b border-line py-2 last:border-b-0">
      <span className="text-sm text-ink-2">{label}</span>
      <span className="text-sm font-semibold text-navy">{value}</span>
    </div>
  );
}

function Counter({ label, value, tone = 'navy' }: { label: string; value: number; tone?: 'navy' | 'alert' }) {
  return (
    <div className="rounded-lg border border-line bg-white p-3 text-center">
      <p className="text-xs text-ink-2">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${tone === 'alert' ? 'text-alert' : 'text-navy'}`}>{value}</p>
    </div>
  );
}

function hourLabel(hour: number | null): string {
  return hour === null || hour === undefined ? 'بلا نافذة' : `${hour}:00`;
}

function yesNo(value: boolean): string {
  return value ? 'نعم' : 'لا';
}

// ===== العرض الخالص (بلا استدعاء شبكة — يسهّل الاختبار) =====

export function EmailControlStatusView({
  status,
  isLoading,
  isError,
  isFetching = false,
  onRefresh,
}: {
  status: EmailControlCenterStatusDto | undefined;
  isLoading: boolean;
  isError: boolean;
  isFetching?: boolean;
  onRefresh: () => void;
}) {
  if (isLoading) return <LoadingState label="يتم قراءة الحالة التشغيليّة…" />;
  if (isError || !status) {
    return <QueryError onRetry={onRefresh} description="تعذّر قراءة الحالة التشغيليّة لقناة البريد." />;
  }

  const m = describeMode(status.mode);
  const warnings = status.warnings ?? [];

  return (
    <div className="space-y-4">
      {/* ===== 1) الحالة التشغيليّة الحاليّة ===== */}
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={m.badgeTone}>{m.badge}</Badge>
              <h3 className="text-lg font-bold text-navy">{m.title}</h3>
            </div>
            <p className="text-sm text-ink-2">{m.description}</p>
            <p className="text-xs text-ink-2">
              الوضع الحاليّ: <span className="font-semibold text-navy">{status.mode}</span> · بيئة التشغيل:{' '}
              <span className="font-semibold text-navy">{status.environmentName}</span>
            </p>
          </div>
          <div className="flex flex-col items-end gap-2">
            <Button variant="ghost" onClick={onRefresh} loading={isFetching}>
              تحديث
            </Button>
            <p className="text-xs text-ink-2">آخر فحص: {formatDateTime(status.checkedAtUtc)}</p>
          </div>
        </div>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* ===== 2) جدول التشغيل ===== */}
        <Card>
          <h4 className="mb-2 text-base font-bold text-navy">جدول التشغيل</h4>
          <Row
            label="المجدول"
            value={
              status.schedulerEnabled ? (
                <Badge tone="success">مُفعَّل</Badge>
              ) : (
                <Badge tone="muted">معطّل</Badge>
              )
            }
          />
          <Row label="فترة النبض" value={`كل ${status.pollMinutes} دقيقة`} />
          <Row label="نافذة التقارير اليوميّة" value={hourLabel(status.dailyDueHour)} />
          <Row label="نافذة التقارير الأسبوعيّة" value={hourLabel(status.weeklyDueHour)} />
          <Row label="نافذة المتأخّرات" value={hourLabel(status.overdueHour)} />
          <Row label="نافذة التلخيصات" value={hourLabel(status.summaryHour)} />
          <Row label="نافذة المراجعات" value={hourLabel(status.reviewHour)} />
          <Row label="المنطقة الزمنيّة" value={`${status.timeZoneLabel} — ${status.timeZoneId}`} />
        </Card>

        {/* ===== 3) حالة المعالجة (جاهزيّة SMTP + طوابير المعالجة) ===== */}
        <Card>
          <h4 className="mb-2 text-base font-bold text-navy">حالة المعالجة وجاهزيّة SMTP</h4>
          <Row
            label="جاهزيّة SMTP"
            value={
              status.smtpConfigured ? (
                <Badge tone="success">مُهيَّأ</Badge>
              ) : (
                <Badge tone="alert">غير مُهيَّأ</Badge>
              )
            }
          />
          <Row label="مضيف SMTP" value={status.smtpHost ?? '—'} />
          <Row label="المنفذ" value={status.smtpPort ?? '—'} />
          <Row label="STARTTLS" value={yesNo(status.usesTls)} />
          <Row label="عنوان المُرسِل" value={status.senderAddress ?? '—'} />
          <Row
            label="بيانات الاعتماد"
            value={
              status.credentialConfigured ? (
                <Badge tone="success">مضبوطة</Badge>
              ) : (
                <Badge tone="alert">غير مضبوطة</Badge>
              )
            }
          />
          <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Counter label="قيد الانتظار" value={status.pendingCount} />
            <Counter label="قيد المعالجة" value={status.processingCount} />
            <Counter label="فاشلة" value={status.failedCount} tone={status.failedCount > 0 ? 'alert' : 'navy'} />
            <Counter label="صندوق الصادر القديم" value={status.outboxCount} />
          </div>
        </Card>

        {/* ===== 4) آخر نشاط ===== */}
        <Card>
          <h4 className="mb-2 text-base font-bold text-navy">آخر نشاط</h4>
          <Row label="آخر إشعار مُسجَّل" value={formatDateTime(status.lastNotificationCreatedAtUtc)} />
          <Row label="آخر إرسال ناجح" value={formatDateTime(status.lastSentAtUtc)} />
          <Row label="آخر إخفاق" value={formatDateTime(status.lastFailureAtUtc)} />
          <Row
            label="آخر إشعار من فئة مجدوَلة"
            value={formatDateTime(status.lastScheduledNotificationCreatedAtUtc)}
          />
          <p className="mt-2 text-xs text-ink-2">
            ملاحظة: «آخر إشعار من فئة مجدوَلة» ليس «آخر تشغيل للمجدول» — لا يوجد تتبّع تشغيل مُخزَّن في
            البنية الحاليّة، والمسار اليدويّ يُنتج صفوفًا لا تُميَّز عن صفوف المجدول.
          </p>
        </Card>

        {/* ===== 5) السجلّ التاريخيّ ===== */}
        <Card>
          <h4 className="mb-2 text-base font-bold text-navy">السجلّ التاريخيّ</h4>
          <p className="mb-3 text-xs text-ink-2">
            أرقام تراكميّة لصفوف سُجِّلت سابقًا — لا تعبّر عن الوضع الحاليّ للنظام.
          </p>
          <div className="grid grid-cols-3 gap-2">
            <Counter label="إجمالي الإشعارات" value={status.totalNotifications} />
            <Counter label="سجلّ تاريخيّ بوضع المحاكاة" value={status.historicalDryRunCount} />
            <Counter label="سجلّ بوضع الإرسال الفعليّ" value={status.enabledCount} />
          </div>
          <div className="mt-3">
            <Counter label="أُرسِلت فعليًّا" value={status.sentCount} />
          </div>
        </Card>
      </div>

      {/* ===== 6) إعدادات التوافق القديمة ===== */}
      <Card>
        <h4 className="mb-2 text-base font-bold text-navy">إعدادات التوافق القديمة</h4>
        <Row
          label="Legacy Email Flag (Email:Enabled)"
          value={status.legacyEmailEnabled ? <Badge tone="success">مُفعَّل</Badge> : <Badge tone="muted">معطّل</Badge>}
        />
        <Row label="هل هو مصدر حقيقة لهذه القناة؟" value={yesNo(status.legacyFlagIsAuthoritative)} />
        <p className="mt-2 text-sm text-ink-2">
          هذا العلم يخصّ مسارات البريد القديمة فقط (صندوق الصادر القديم وتذكير التسليم القديم)، ولا يحكم
          قناة الإشعارات الجديدة إطلاقًا.
        </p>
        {status.mode === 'Enabled' && !status.legacyEmailEnabled && (
          <div className="mt-3">
            <Alert tone="navy">
              القناة الجديدة مفعلة، بينما العلم القديم معطل ويخص مسارات البريد القديمة فقط.
            </Alert>
          </div>
        )}
      </Card>

      {/* ===== 7) التنبيهات ===== */}
      <Card>
        <h4 className="mb-2 text-base font-bold text-navy">التنبيهات</h4>
        {warnings.length === 0 ? (
          <p className="text-sm text-ink-2">لا توجد تنبيهات.</p>
        ) : (
          <ul className="space-y-2">
            {warnings.map((w: EmailControlStatusWarningDto) => (
              <li key={w.code}>
                <Alert tone={severityTone[w.severity] ?? 'navy'}>
                  <span className="font-semibold">{severityLabel[w.severity] ?? w.severity}:</span> {w.message}
                </Alert>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

// ===== الحاوية (تربط الـhook بالعرض) =====

export default function EmailControlStatusPanel() {
  const q = useEmailControlStatus();
  return (
    <EmailControlStatusView
      status={q.data}
      isLoading={q.isLoading}
      isError={q.isError}
      isFetching={q.isFetching}
      onRefresh={() => q.refetch()}
    />
  );
}

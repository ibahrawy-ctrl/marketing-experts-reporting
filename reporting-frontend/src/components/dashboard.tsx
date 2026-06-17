// مكوّنات لوحات القيادة القابلة لإعادة الاستخدام — بهوية خبراء التسويق (كحلي/برتقالي).
// الفلسفة: كل لوحة موجَّهة بالأفعال — «ماذا عليّ الآن؟» قبل الأرقام.
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Card, Badge, Button } from './ui';

type Tone = 'navy' | 'orange' | 'success' | 'alert' | 'gold' | 'muted';

const valueTone: Record<Tone, string> = {
  navy: 'text-navy',
  orange: 'text-orange-600',
  success: 'text-success',
  alert: 'text-alert',
  gold: 'text-gold',
  muted: 'text-ink-2',
};

const accentBar: Record<Tone, string> = {
  navy: 'bg-navy',
  orange: 'bg-orange',
  success: 'bg-success',
  alert: 'bg-alert',
  gold: 'bg-gold',
  muted: 'bg-line-2',
};

// عنوان قسم موحّد مع إجراء اختياري على اليسار.
export function SectionTitle({ title, hint, action }: { title: string; hint?: string; action?: ReactNode }) {
  return (
    <div className="mb-3 flex items-center justify-between gap-3">
      <div>
        <h2 className="text-lg font-bold text-navy">{title}</h2>
        {hint && <p className="text-xs text-ink-2">{hint}</p>}
      </div>
      {action}
    </div>
  );
}

// بطاقة مؤشّر كبيرة — قيمة + شارة دلتا اختيارية + قابلية الضغط (drill-down).
export function MetricTile({
  label,
  value,
  tone = 'navy',
  delta,
  hint,
  to,
}: {
  label: string;
  value: ReactNode;
  tone?: Tone;
  delta?: { value: number; up: boolean } | null;
  hint?: string;
  to?: string;
}) {
  const body = (
    <div className="relative h-full overflow-hidden rounded-2xl border border-line bg-white p-5 transition hover:shadow-sm">
      <span className={`absolute inset-y-0 right-0 w-1.5 ${accentBar[tone]}`} />
      <p className="text-sm text-ink-2">{label}</p>
      <div className="mt-2 flex items-end gap-2">
        <span className={`text-3xl font-extrabold ${valueTone[tone]}`}>{value}</span>
        {delta && (
          <span
            className={`mb-1 text-xs font-bold ${delta.up ? 'text-success' : 'text-alert'}`}
            title="مقارنة بالفترة السابقة"
          >
            {delta.up ? '▲' : '▼'} {Math.abs(delta.value)}
          </span>
        )}
      </div>
      {hint && <p className="mt-1 text-xs text-ink-2">{hint}</p>}
    </div>
  );
  return to ? (
    <Link to={to} className="block h-full">
      {body}
    </Link>
  ) : (
    body
  );
}

// لوحة «ماذا عليّ الآن؟» — أهم إجراء مطلوب من المستخدم، بارز بالبرتقالي.
export function ActionBanner({
  title,
  description,
  cta,
  tone = 'orange',
}: {
  title: string;
  description?: string;
  cta?: ReactNode;
  tone?: 'orange' | 'success' | 'navy';
}) {
  const styles = {
    orange: 'from-orange to-orange-600',
    success: 'from-success to-[#15875a]',
    navy: 'from-navy to-navy-600',
  }[tone];
  return (
    <div className={`flex flex-wrap items-center justify-between gap-4 rounded-2xl bg-gradient-to-l ${styles} p-5 text-white`}>
      <div>
        <h2 className="text-lg font-bold">{title}</h2>
        {description && <p className="mt-1 text-sm text-white/90">{description}</p>}
      </div>
      {cta}
    </div>
  );
}

// صفّ إجراء داخل قائمة «المطلوب الآن» — عنوان + سياق + زر.
export function ActionItem({
  title,
  context,
  badge,
  action,
}: {
  title: string;
  context?: string;
  badge?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <li className="flex items-center justify-between gap-3 border-b border-line py-2.5 last:border-0">
      <div className="min-w-0">
        <p className="truncate font-medium text-navy">{title}</p>
        {context && <p className="truncate text-xs text-ink-2">{context}</p>}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {badge}
        {action}
      </div>
    </li>
  );
}

// شريط نسبة الاكتمال.
export function ProgressBar({ value, tone = 'orange' }: { value: number; tone?: 'orange' | 'navy' | 'success' }) {
  const pct = Math.max(0, Math.min(100, Math.round(value)));
  const bar = { orange: 'bg-orange', navy: 'bg-navy', success: 'bg-success' }[tone];
  return (
    <div className="h-2.5 w-full overflow-hidden rounded-full bg-line">
      <div className={`h-full rounded-full ${bar} transition-all`} style={{ width: `${pct}%` }} />
    </div>
  );
}

// مسار الاعتماد البصري (RTL): الموظف ← قائد الفريق ← المدير ← المدير العام ← الرئيس التنفيذي.
export type PathStep = { label: string; state: 'done' | 'current' | 'todo' | 'returned' };

export function ApprovalPath({ steps }: { steps: PathStep[] }) {
  const dot: Record<PathStep['state'], string> = {
    done: 'bg-success text-white border-success',
    current: 'bg-orange text-white border-orange ring-4 ring-orange-100',
    todo: 'bg-white text-ink-3 border-line-2',
    returned: 'bg-alert text-white border-alert',
  };
  const mark: Record<PathStep['state'], string> = { done: '✓', current: '●', todo: '○', returned: '↺' };
  return (
    <div className="flex items-stretch gap-1 overflow-x-auto py-1">
      {steps.map((s, i) => (
        <div key={i} className="flex min-w-0 flex-1 flex-col items-center">
          <div className="flex w-full items-center">
            <span className={`h-0.5 flex-1 ${i === 0 ? 'bg-transparent' : steps[i].state === 'todo' ? 'bg-line' : 'bg-success'}`} />
            <span className={`grid h-8 w-8 shrink-0 place-items-center rounded-full border-2 text-sm font-bold ${dot[s.state]}`}>
              {mark[s.state]}
            </span>
            <span className={`h-0.5 flex-1 ${i === steps.length - 1 ? 'bg-transparent' : steps[i + 1] && steps[i + 1].state !== 'todo' ? 'bg-success' : 'bg-line'}`} />
          </div>
          <span className={`mt-1.5 truncate text-center text-xs ${s.state === 'current' ? 'font-bold text-orange-600' : s.state === 'todo' ? 'text-ink-3' : 'text-navy'}`}>
            {s.label}
          </span>
        </div>
      ))}
    </div>
  );
}

// صفّ تنبيه — نقطة لون + نص (مخاطر/تأخير/قرار مطلوب).
export function AlertRow({ tone, children }: { tone: 'alert' | 'gold' | 'navy' | 'success'; children: ReactNode }) {
  const dot = { alert: 'bg-alert', gold: 'bg-gold', navy: 'bg-navy', success: 'bg-success' }[tone];
  return (
    <li className="flex items-center gap-2.5 border-b border-line py-2 text-sm last:border-0">
      <span className={`h-2 w-2 shrink-0 rounded-full ${dot}`} />
      <span className="text-ink">{children}</span>
    </li>
  );
}

// حالة فارغة خفيفة داخل البطاقات — مع سطر تلميح اختياري يوضّح متى تظهر البيانات أو ما الخطوة التالية.
export function MiniEmpty({ text, hint }: { text: string; hint?: string }) {
  return (
    <div className="py-6 text-center">
      <p className="text-sm font-medium text-ink-2">{text}</p>
      {hint ? <p className="mx-auto mt-1 max-w-sm text-xs text-ink-3">{hint}</p> : null}
    </div>
  );
}

// ===== «يحتاج إجراء الآن» — قائمة موحّدة لأهم ما على المستخدم فعله، تتصدّر كل لوحة =====
// الفلسفة (UX-2): الإجابة الفورية عن «ماذا عليّ الآن؟» قبل أي رقم. كل بند يوضّح السبب
// والسياق ويقود مباشرةً إلى مكان الإجراء (Drill-down/رابط). عند عدم وجود إجراءات
// نعرض حالة إيجابية صريحة بدل تركها فارغة.
export type NeedsActionEntry = {
  id: string;
  title: string;
  context?: string;
  urgency?: 'high' | 'medium' | 'low';
  to?: string;
  cta?: string;
};

const URGENCY_RANK: Record<NonNullable<NeedsActionEntry['urgency']>, number> = { high: 0, medium: 1, low: 2 };
const URGENCY_TONE: Record<NonNullable<NeedsActionEntry['urgency']>, 'alert' | 'gold' | 'navy'> = {
  high: 'alert',
  medium: 'gold',
  low: 'navy',
};
const URGENCY_LABEL: Record<NonNullable<NeedsActionEntry['urgency']>, string> = {
  high: 'عاجل',
  medium: 'متابعة',
  low: 'للعلم',
};

export function NeedsActionPanel({
  items,
  emptyText = 'لا يوجد ما يتطلّب إجراءً منك الآن — الوضع منضبط.',
  emptyHint,
  limit = 8,
}: {
  items: NeedsActionEntry[];
  emptyText?: string;
  emptyHint?: string;
  limit?: number;
}) {
  const sorted = [...items].sort(
    (a, b) => URGENCY_RANK[a.urgency ?? 'low'] - URGENCY_RANK[b.urgency ?? 'low'],
  );
  const shown = sorted.slice(0, limit);
  const hiddenCount = sorted.length - shown.length;
  const highCount = items.filter((i) => (i.urgency ?? 'low') === 'high').length;

  return (
    <Card className={items.length === 0 ? '' : 'border-r-4 border-r-orange'}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <h2 className="text-lg font-bold text-navy">يحتاج إجراء الآن</h2>
          {items.length > 0 && (
            <Badge tone={highCount > 0 ? 'alert' : 'gold'}>{items.length} بند</Badge>
          )}
        </div>
        <p className="text-xs text-ink-2">أهم ما عليك فعله، مرتّبًا حسب الأولوية</p>
      </div>
      {items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-line bg-offwhite py-6 text-center">
          <p className="text-sm font-medium text-success">{emptyText}</p>
          {emptyHint ? <p className="mx-auto mt-1 max-w-md text-xs text-ink-3">{emptyHint}</p> : null}
        </div>
      ) : (
        <>
          <ul>
            {shown.map((it) => {
              const u = it.urgency ?? 'low';
              return (
                <ActionItem
                  key={it.id}
                  title={it.title}
                  context={it.context}
                  badge={<Badge tone={URGENCY_TONE[u]}>{URGENCY_LABEL[u]}</Badge>}
                  action={
                    it.to ? (
                      <Link to={it.to}>
                        <Button>{it.cta ?? 'فتح'}</Button>
                      </Link>
                    ) : undefined
                  }
                />
              );
            })}
          </ul>
          {hiddenCount > 0 && (
            <p className="mt-2 text-center text-xs text-ink-3">+ {hiddenCount} بند إضافي بأولوية أقل</p>
          )}
        </>
      )}
    </Card>
  );
}

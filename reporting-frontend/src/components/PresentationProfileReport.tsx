// ===== AMR-OUTPUT-REDESIGN-R1 — المصيّر العامّ المدفوع بميفولة العرض =====
// يقرأ PresentationProfile ويصيّر مخرَجًا قراريًّا: ملخّص محفظة تنفيذيّ + فهرس مشاريع + بطاقة لكل
// مشروع بأقسام A–J (ترويسة/مقاييس/سرد/عميل/عوائق/قرارات/أولويّات/روابط) + مجموعة ذيليّة للحقول
// التاريخيّة غير المعروفة. لا يغيّر البيانات إطلاقًا (عرض فقط، مشتقّات من القيم المحفوظة).
// القوالب بلا Profile لا تمرّ من هنا (المصيّر العامّ القائم يبقى fallback في SubmissionsPage).

import { useState } from 'react';
import { Badge, Card } from './ui';
import type {
  ProjectDto,
  ProjectRepeatableConfig,
  ProjectRepeatableEntry,
  RepeatableSubField,
} from '../types/api';
import {
  badgeToneFor,
  hasText,
  isMeaningfulPresentationValue,
  profileKnownKeys,
  type PresentationProfile,
  type PresentationTone,
} from '../lib/reportPresentationProfiles';

const cardBorderTone: Record<PresentationTone, string> = {
  navy: 'border-navy/20 bg-navy/[0.03]',
  orange: 'border-orange/30 bg-orange/[0.05]',
  gold: 'border-gold/30 bg-gold/[0.06]',
  success: 'border-success/30 bg-success/[0.06]',
  alert: 'border-alert/30 bg-alert/[0.06]',
  muted: 'border-line bg-offwhite',
};

// عدّ مشتقّ من القيم المحفوظة — لا أرقام مخترَعة.
function toNumber(raw: string | undefined): number | null {
  if (!hasText(raw)) return null;
  const n = Number(String(raw).replace(/[^\d.-]/g, ''));
  return Number.isFinite(n) ? n : null;
}

// عرض قيمة حقل فرعيّ (Boolean ⇒ نعم/لا؛ غيره ⇒ النصّ كما هو).
function displayValue(sf: RepeatableSubField | undefined, raw: string | undefined): string {
  if (!hasText(raw)) return '';
  if (sf?.type === 'Boolean') return String(raw).trim() === 'true' ? 'نعم' : 'لا';
  return String(raw).trim();
}

export function PresentationProfileReport({
  profile,
  config,
  entries,
  projects,
}: {
  profile: PresentationProfile;
  config: ProjectRepeatableConfig;
  entries: ProjectRepeatableEntry[];
  projects: ProjectDto[];
}) {
  if (entries.length === 0)
    return (
      <p className="rounded-lg border border-line bg-offwhite px-3 py-2 text-sm text-ink-2">
        لا توجد مشاريع في هذا التقرير.
      </p>
    );

  const byKey = new Map(config.fields.map((f) => [f.key, f]));
  const label = (key: string) => byKey.get(key)?.label ?? key;
  const known = profileKnownKeys(profile);

  const projectName = (pid: string | null) => {
    if (!pid) return 'بدون مشروع محدّد';
    const p = projects.find((x) => x.id === pid);
    return p ? `${p.name}${p.clientName ? ` — ${p.clientName}` : ''}` : 'مشروع غير معروف';
  };

  // ===== اشتقاق ملخّص المحفظة (عدّ فعليّ) =====
  const total = entries.length;
  let stable = 0;
  let followUp = 0;
  let atRisk = 0;
  let sumSent = 0;
  let sumApproved = 0;
  let sumPending = 0;
  let withClientRequests = 0;
  let withDecisions = 0;
  let withRisk = 0;
  for (const e of entries) {
    const st = String(e.answers[profile.statusKey] ?? '').trim();
    if (profile.statusBuckets.stable.includes(st)) stable += 1;
    else if (profile.statusBuckets.followUp.includes(st)) followUp += 1;
    else if (profile.statusBuckets.atRisk.includes(st)) atRisk += 1;
    sumSent += toNumber(e.answers[profile.approvalProgress?.sentKey ?? 'deliverables_sent']) ?? 0;
    sumApproved += toNumber(e.answers[profile.approvalProgress?.approvedKey ?? 'deliverables_approved']) ?? 0;
    sumPending += toNumber(e.answers['deliverables_pending']) ?? 0;
    if (profile.clientKeys.some((k) => isMeaningfulPresentationValue(e.answers[k]))) withClientRequests += 1;
    if (isMeaningfulPresentationValue(e.answers[profile.decisionKey])) withDecisions += 1;
    const riskTone = badgeToneFor(
      profile.statusBadges.find((b) => b.key === profile.riskKey) ?? {
        key: profile.riskKey,
        emptyValues: ['', 'لا يوجد'],
        toneByValue: {},
        defaultTone: 'gold',
      },
      e.answers[profile.riskKey],
    );
    if (riskTone != null) withRisk += 1;
  }

  const summaryTiles: { label: string; value: string; tone: PresentationTone }[] = [
    { label: 'إجمالي المشاريع', value: String(total), tone: 'navy' },
    { label: '🟢 على المسار / مكتمل', value: String(stable), tone: 'success' },
    { label: '🟡 يحتاج متابعة', value: String(followUp), tone: 'gold' },
    { label: '🔴 متعثّر', value: String(atRisk), tone: 'alert' },
    { label: 'تسليمات أُرسلت', value: String(sumSent), tone: 'navy' },
    { label: 'تسليمات اعتُمدت', value: String(sumApproved), tone: 'success' },
    { label: 'تسليمات منتظرة', value: String(sumPending), tone: 'gold' },
    { label: '⚠ مشاريع بها مخاطر', value: String(withRisk), tone: 'orange' },
    { label: '📋 مشاريع بطلبات عميل', value: String(withClientRequests), tone: 'navy' },
    { label: '📌 قرارات مطلوبة', value: String(withDecisions), tone: withDecisions > 0 ? 'alert' : 'muted' },
  ];

  return (
    <div className="space-y-4">
      {/* ملخّص المحفظة التنفيذيّ — مشتقّ بالكامل من القيم المحفوظة */}
      <section className="rounded-lg border border-navy/15 bg-navy/[0.02] p-3">
        <h3 className="mb-2 text-sm font-bold text-navy">ملخّص المحفظة التنفيذيّ</h3>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
          {summaryTiles.map((t) => (
            <div key={t.label} className={`rounded-lg border px-3 py-2 ${cardBorderTone[t.tone]}`}>
              <p className="text-[11px] leading-tight text-ink-2">{t.label}</p>
              <p className="mt-0.5 text-xl font-bold text-navy">{t.value}</p>
            </div>
          ))}
        </div>
      </section>

      {/* فهرس المشاريع — صفّ لكل مشروع، نقر ⇒ قفز لبطاقته */}
      <section className="overflow-x-auto rounded-lg border border-line">
        <table className="w-full text-right text-sm">
          <thead>
            <tr className="bg-offwhite text-ink-2">
              <th className="px-2 py-2 font-medium">المشروع</th>
              <th className="px-2 py-2 font-medium">الحالة</th>
              <th className="px-2 py-2 font-medium">المرحلة</th>
              <th className="px-2 py-2 font-medium">التسليمات</th>
              <th className="px-2 py-2 font-medium">المخاطر</th>
              <th className="px-2 py-2 font-medium">العلاقة</th>
              <th className="px-2 py-2 font-medium">القرار المطلوب</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, i) => {
              const statusSpec = profile.statusBadges.find((b) => b.key === profile.statusKey);
              const riskSpec = profile.statusBadges.find((b) => b.key === profile.riskKey);
              const relSpec = profile.statusBadges.find((b) => b.key === profile.relationshipKey);
              const statusTone = statusSpec ? badgeToneFor(statusSpec, entry.answers[statusSpec.key]) : null;
              const riskTone = riskSpec ? badgeToneFor(riskSpec, entry.answers[riskSpec.key]) : null;
              const relTone = relSpec ? badgeToneFor(relSpec, entry.answers[relSpec.key]) : null;
              const sent = toNumber(entry.answers['deliverables_sent']);
              const approved = toNumber(entry.answers['deliverables_approved']);
              const deliverText =
                sent != null || approved != null ? `${approved ?? 0}/${sent ?? 0}` : '—';
              const decides = isMeaningfulPresentationValue(entry.answers[profile.decisionKey]);
              return (
                <tr key={i} className="border-t border-line align-top">
                  <td className="px-2 py-2">
                    <a href={`#amr-project-${i}`} className="font-medium text-navy underline-offset-2 hover:underline">
                      {projectName(entry.projectId)}
                    </a>
                  </td>
                  <td className="px-2 py-2">
                    {statusTone ? <Badge tone={statusTone}>{entry.answers[statusSpec!.key]}</Badge> : '—'}
                  </td>
                  <td className="px-2 py-2 text-ink-2">{displayValue(byKey.get(profile.phaseKey ?? ''), entry.answers[profile.phaseKey ?? '']) || '—'}</td>
                  <td className="px-2 py-2 text-ink-2">{deliverText}</td>
                  <td className="px-2 py-2">
                    {riskTone ? <Badge tone={riskTone}>{entry.answers[riskSpec!.key]}</Badge> : '—'}
                  </td>
                  <td className="px-2 py-2">
                    {relTone ? <Badge tone={relTone}>{entry.answers[relSpec!.key]}</Badge> : '—'}
                  </td>
                  <td className="px-2 py-2">
                    {decides ? <Badge tone="alert">📌 مطلوب</Badge> : '—'}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>

      {/* بطاقة تفصيل لكل مشروع (A–J) */}
      {entries.map((entry, i) => (
        <ProjectCard
          key={i}
          anchor={`amr-project-${i}`}
          openByDefault={i === 0}
          title={projectName(entry.projectId)}
          profile={profile}
          answers={entry.answers}
          byKey={byKey}
          known={known}
          label={label}
        />
      ))}
    </div>
  );
}

// بطاقة مشروع واحد: قابلة للطيّ تفاعليًّا، لكنها تُفتح دائمًا عند الطباعة (print:block) ولا تُقصّ (break-inside-avoid).
function ProjectCard({
  anchor,
  openByDefault,
  title,
  profile,
  answers,
  byKey,
  known,
  label,
}: {
  anchor: string;
  openByDefault: boolean;
  title: string;
  profile: PresentationProfile;
  answers: Record<string, string>;
  byKey: Map<string, RepeatableSubField>;
  known: Set<string>;
  label: (key: string) => string;
}) {
  const [open, setOpen] = useState(openByDefault);

  const phaseText = displayValue(byKey.get(profile.phaseKey ?? ''), answers[profile.phaseKey ?? '']);
  const sent = toNumber(answers['deliverables_sent']);
  const approved = toNumber(answers['deliverables_approved']);
  const approvalPct =
    sent != null && sent > 0 && approved != null ? Math.min(100, Math.round((approved / sent) * 100)) : null;

  // المقاييس رقميّة: 0 قيمة دالّة تظهر، والعبارات غير الدالّة («—»/«لا») لا تُعرض كمقياس.
  const presentMetrics = profile.metrics.filter((m) => isMeaningfulPresentationValue(answers[m.key]));
  const decisionText = displayValue(byKey.get(profile.decisionKey), answers[profile.decisionKey]);
  const hasDecision = isMeaningfulPresentationValue(answers[profile.decisionKey]);

  // حقول تاريخيّة غير معروفة للـProfile — تُعرض في مجموعة ذيليّة (لا فقدان لأيّ حقل ذي معنى).
  const fallbackFields = Array.from(byKey.values()).filter(
    (f) => !known.has(f.key) && f.type !== 'Grid' && isMeaningfulPresentationValue(answers[f.key]),
  );

  return (
    <Card className="break-inside-avoid p-0">
      <div id={anchor} className="scroll-mt-4" />
      {/* (A) ترويسة المشروع */}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-start justify-between gap-2 border-b border-line bg-offwhite px-4 py-3 text-right"
      >
        <div className="min-w-0">
          <p className="truncate font-semibold text-navy">{title}</p>
          {hasText(phaseText) && <p className="mt-0.5 text-xs text-ink-2">المرحلة: {phaseText}</p>}
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {profile.statusBadges.map((spec) => {
              const tone = badgeToneFor(spec, answers[spec.key]);
              if (!tone) return null;
              return (
                <Badge key={spec.key} tone={tone}>
                  {spec.labelPrefix ? `${spec.labelPrefix} ` : ''}
                  {String(answers[spec.key]).trim()}
                </Badge>
              );
            })}
          </div>
        </div>
        <span className="shrink-0 text-xs text-ink-2">{open ? '▲ طيّ' : '▼ عرض'}</span>
      </button>

      <div className={`${open ? 'block' : 'hidden'} space-y-4 p-4 print:block`}>
        {/* (C) مقاييس التسليم */}
        {presentMetrics.length > 0 && (
          <section>
            <h4 className="mb-2 text-sm font-semibold text-navy">مقاييس التسليم</h4>
            <div className="grid grid-cols-3 gap-2">
              {presentMetrics.map((m) => (
                <div key={m.key} className={`rounded-lg border px-3 py-2 text-center ${cardBorderTone[m.tone]}`}>
                  <p className="text-[11px] text-ink-2">{m.label}</p>
                  <p className="mt-0.5 text-xl font-bold text-navy">{String(answers[m.key]).trim()}</p>
                </div>
              ))}
            </div>
            {approvalPct != null && (
              <div className="mt-2">
                <div className="mb-1 flex justify-between text-xs text-ink-2">
                  <span>{profile.approvalProgress?.label ?? 'نسبة الاعتماد'}</span>
                  <span>{approvalPct}٪</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-line">
                  <div className="h-full rounded-full bg-success" style={{ width: `${approvalPct}%` }} />
                </div>
              </div>
            )}
          </section>
        )}

        {/* (B) السرد التنفيذيّ */}
        <NarrativeGroup title="الإنجاز الأسبوعيّ" keys={profile.summaryKeys} answers={answers} label={label} />

        {/* (E) العميل والتواصل */}
        <NarrativeGroup title="ملاحظات وطلبات العميل" keys={profile.clientKeys} answers={answers} label={label} />

        {/* (F) التأخيرات والعوائق */}
        <NarrativeGroup title="القضايا والتأخيرات" keys={profile.blockerKeys} answers={answers} label={label} tone="gold" />

        {/* (H) القرارات المطلوبة — بطاقة بارزة (AMR-A3)؛ عبارات النفي («لا يوجد») لا تُنشئ قرارًا */}
        {hasDecision && (
          <section className="rounded-lg border-2 border-orange/50 bg-orange/[0.06] p-3">
            <h4 className="mb-1 flex items-center gap-1.5 text-sm font-bold text-orange-600">
              📌 قرارات مطلوبة من الإدارة
            </h4>
            <p className="whitespace-pre-wrap text-sm text-ink">{decisionText}</p>
          </section>
        )}

        {/* (I) أولويّة الأسبوع القادم / الفرص */}
        <NarrativeGroup title="الخطوات القادمة والفرص" keys={profile.priorityKeys} answers={answers} label={label} />

        {/* (J) الروابط والأدلّة والتبعيّات */}
        <LinksGroup
          linkKeys={profile.linkKeys}
          footerKeys={profile.footerKeys}
          answers={answers}
          label={label}
        />

        {/* المجموعة الذيليّة — حقول تاريخيّة غير معروفة (لا فقدان) */}
        {fallbackFields.length > 0 && (
          <section className="rounded-lg border border-line/70 bg-offwhite/40 p-3">
            <h4 className="mb-2 text-sm font-semibold text-ink-2">معلومات إضافية</h4>
            <dl className="grid gap-x-6 gap-y-1.5 text-sm md:grid-cols-2">
              {fallbackFields.map((f) => (
                <div key={f.key} className="flex justify-between gap-3 border-b border-line/60 pb-1">
                  <dt className="text-ink-2">{f.label}</dt>
                  <dd className="whitespace-pre-wrap font-medium text-ink">{displayValue(f, answers[f.key])}</dd>
                </div>
              ))}
            </dl>
          </section>
        )}
      </div>
    </Card>
  );
}

// مجموعة سرديّة: بطاقات نصّيّة (فقرات، لا صفوف dt/dd ضيّقة). تختفي إن خلت كل حقولها.
function NarrativeGroup({
  title,
  keys,
  answers,
  label,
  tone,
}: {
  title: string;
  keys: string[];
  answers: Record<string, string>;
  label: (key: string) => string;
  tone?: PresentationTone;
}) {
  const present = keys.filter((k) => isMeaningfulPresentationValue(answers[k]));
  if (present.length === 0) return null;
  const wrap = tone === 'gold' ? 'rounded-lg border border-gold/30 bg-gold/[0.05] p-3' : '';
  return (
    <section className={wrap}>
      <h4 className="mb-2 text-sm font-semibold text-navy">{title}</h4>
      <div className="space-y-2">
        {present.map((k) => (
          <div key={k}>
            {present.length > 1 && <p className="text-xs font-medium text-ink-2">{label(k)}</p>}
            <p className="whitespace-pre-wrap text-sm text-ink">{String(answers[k]).trim()}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

// مجموعة الروابط والتبعيّات: evidence_link رابط قابل للنقر + تبعيّات/ملاحظات نصّيّة. تختفي إن خلت.
function LinksGroup({
  linkKeys,
  footerKeys,
  answers,
  label,
}: {
  linkKeys: string[];
  footerKeys: string[];
  answers: Record<string, string>;
  label: (key: string) => string;
}) {
  const links = linkKeys.filter((k) => isMeaningfulPresentationValue(answers[k]));
  const footers = footerKeys.filter((k) => isMeaningfulPresentationValue(answers[k]));
  if (links.length === 0 && footers.length === 0) return null;
  const isUrl = (v: string) => /^https?:\/\//i.test(v.trim());
  return (
    <section>
      <h4 className="mb-2 text-sm font-semibold text-navy">الأدلّة والتبعيّات</h4>
      <div className="space-y-2 text-sm">
        {links.map((k) => {
          const v = String(answers[k]).trim();
          return (
            <p key={k}>
              <span className="text-xs text-ink-2">{label(k)}: </span>
              {isUrl(v) ? (
                <a href={v} target="_blank" rel="noreferrer" className="text-navy underline underline-offset-2">
                  {v}
                </a>
              ) : (
                <span className="text-ink">{v}</span>
              )}
            </p>
          );
        })}
        {footers.map((k) => (
          <div key={k}>
            <p className="text-xs font-medium text-ink-2">{label(k)}</p>
            <p className="whitespace-pre-wrap text-ink">{String(answers[k]).trim()}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

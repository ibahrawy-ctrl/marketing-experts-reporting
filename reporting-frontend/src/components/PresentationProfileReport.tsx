// ===== AMR-OUTPUT-REDESIGN-R1 — المصيّر العامّ المدفوع بميفولة العرض =====
// يقرأ PresentationProfile ويصيّر مخرَجًا قراريًّا: ملخّص محفظة تنفيذيّ + فهرس مشاريع + بطاقة لكل
// مشروع بأقسام A–J (ترويسة/مقاييس/سرد/عميل/عوائق/قرارات/أولويّات/روابط) + مجموعة ذيليّة للحقول
// التاريخيّة غير المعروفة. لا يغيّر البيانات إطلاقًا (عرض فقط، مشتقّات من القيم المحفوظة).
// القوالب بلا Profile لا تمرّ من هنا (المصيّر العامّ القائم يبقى fallback في SubmissionsPage).
//
// ===== AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1 =====
// المخرَج صار «العميل أوّلًا ثمّ المشروع»: تنقّل سريع حسب العميل + بطاقات التفاصيل مجمّعة تحت عميلها.
// التجميع نظاميّ بالكامل: ProjectId ⇒ ProjectDto.clientId ⇒ ProjectDto.clientName.
// ممنوع منعًا باتًّا استنتاج العميل من نصّ اسم المشروع أو من خرائط يدويّة.
// المرساة مشتقّة من ProjectId لا من الاسم (مشروعان بالاسم نفسه تحت عميلَين مختلفَين موجودان فعليًّا).
// كل الأرقام والحسابات كما هي حرفيًّا (لا تغيير في ملخّص المحفظة ولا في فهرس المشاريع).

import { useEffect, useMemo, useState } from 'react';
import { Badge, Card } from './ui';
import type {
  ProjectNameRef,
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

// مفتاح المجموعة الاحتياطيّة: مشروع بلا مشروع محدّد، أو غير موجود في دليل المشاريع، أو بلا عميل.
const UNASSIGNED_CLIENT_KEY = '__amr_unassigned_client__';
const UNASSIGNED_CLIENT_LABEL = 'عميل غير محدّد / بيانات تاريخية';
const QUICK_NAV_ID = 'amr-quick-nav';
const HIGHLIGHT_MS = 2200;

// معرّفات فرعيّة للبطاقة (عنوان/جسم) مشتقّة من المرساة لكن ببادئة مختلفة،
// كي يبقى `[id^="amr-project-"]` مقصورًا على مراسي المشاريع نفسها (مرساة واحدة لكلّ مشروع).
function subId(anchor: string, suffix: 'title' | 'body'): string {
  return `${anchor.replace(/^amr-project-/, 'amr-pcard-')}-${suffix}`;
}

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

// حقائق مشتقّة لكلّ مشروع — تُحسَب مرّة واحدة وتُستعمل للمجاميع العامّة وللعدّادات لكلّ عميل معًا،
// كي تبقى أرقام ملخّص المحفظة مطابقة تمامًا لما قبل إعادة التنظيم.
interface EntryFacts {
  bucket: 'stable' | 'followUp' | 'atRisk' | 'none';
  sent: number;
  approved: number;
  pending: number;
  hasClientRequests: boolean;
  hasDecision: boolean;
  hasRisk: boolean;
}

interface ProjectItem {
  index: number; // ترتيب الإدخال الأصليّ داخل التقرير (لا يُعاد ترتيبه)
  entry: ProjectRepeatableEntry;
  anchorId: string; // مشتقّ من ProjectId لا من الاسم
  shortTitle: string; // اسم المشروع فقط (العميل ظاهر في ترويسة المجموعة)
  fullTitle: string; // اسم المشروع — اسم العميل (كما في الفهرس القائم)
  facts: EntryFacts;
}

interface ClientGroup {
  key: string; // ClientId الفعليّ أو مفتاح المجموعة الاحتياطيّة
  name: string;
  items: ProjectItem[];
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
  projects: ProjectNameRef[];
}) {
  // حالة مرفوعة: فتح/طيّ بطاقات المشاريع، فتح مجموعات التنقّل، الإبراز المؤقّت، وطلب التمرير.
  const [openCards, setOpenCards] = useState<Record<string, boolean>>({});
  const [navOpen, setNavOpen] = useState<Record<string, boolean>>({});
  const [highlight, setHighlight] = useState<string | null>(null);
  const [scrollRequest, setScrollRequest] = useState<{ id: string; seq: number } | null>(null);

  const byKey = useMemo(() => new Map(config.fields.map((f) => [f.key, f])), [config.fields]);
  const known = useMemo(() => profileKnownKeys(profile), [profile]);
  const label = (key: string) => byKey.get(key)?.label ?? key;

  // ===== بناء العناصر + التجميع حسب العميل (ClientId فقط) =====
  const { items, groups } = useMemo(() => {
    const projectById = new Map(projects.map((p) => [p.id, p]));
    const anchorSeen = new Map<string, number>();
    const built: ProjectItem[] = entries.map((entry, index) => {
      const p = entry.projectId ? projectById.get(entry.projectId) : undefined;
      const baseAnchor = entry.projectId ? `amr-project-${entry.projectId}` : `amr-project-row-${index}`;
      // مشروع مكرّر داخل التقرير نفسه ⇒ لاحقة ترتيبيّة لضمان تفرّد المرساة (تبقى مشتقّة من ProjectId).
      const seen = anchorSeen.get(baseAnchor) ?? 0;
      anchorSeen.set(baseAnchor, seen + 1);
      const anchorId = seen === 0 ? baseAnchor : `${baseAnchor}-${index}`;

      const shortTitle = p ? p.name : entry.projectId ? 'مشروع غير معروف' : 'بدون مشروع محدّد';
      const fullTitle = p ? `${p.name}${p.clientName ? ` — ${p.clientName}` : ''}` : shortTitle;

      return { index, entry, anchorId, shortTitle, fullTitle, facts: buildFacts(profile, entry) };
    });

    // ترتيب العملاء = ترتيب الظهور الأوّل؛ ترتيب المشاريع داخل العميل = ترتيب الإدخال (بلا فرز أبجديّ).
    const map = new Map<string, ClientGroup>();
    built.forEach((item) => {
      const p = item.entry.projectId ? projectById.get(item.entry.projectId) : undefined;
      const key = p?.clientId ? p.clientId : UNASSIGNED_CLIENT_KEY;
      const name =
        key === UNASSIGNED_CLIENT_KEY ? UNASSIGNED_CLIENT_LABEL : p?.clientName?.trim() || 'عميل غير مسمّى';
      const existing = map.get(key);
      if (existing) existing.items.push(item);
      else map.set(key, { key, name, items: [item] });
    });

    return { items: built, groups: Array.from(map.values()) };
  }, [entries, projects, profile]);

  // مصدر تنقّل واحد مشترك — يستعمله زرّ التنقّل السريع وخليّة الفهرس معًا (لا مسار منطقيّ ثانٍ).
  const openProjectAndNavigate = (anchorId: string, clientKey: string) => {
    setNavOpen((s) => ({ ...s, [clientKey]: true }));
    setOpenCards((s) => ({ ...s, [anchorId]: true }));
    setScrollRequest((prev) => ({ id: anchorId, seq: (prev?.seq ?? 0) + 1 }));
  };

  const backToNav = () => {
    const el = typeof document !== 'undefined' ? document.getElementById(QUICK_NAV_ID) : null;
    if (el && typeof el.scrollIntoView === 'function') el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  // يُنفَّذ بعد اكتمال تحديث React للـDOM: تمرير سلس ⇒ نقل التركيز لعنوان المشروع ⇒ إبراز مؤقّت يُزال تلقائيًّا.
  useEffect(() => {
    if (!scrollRequest) return;
    const el = document.getElementById(scrollRequest.id);
    if (el && typeof el.scrollIntoView === 'function') el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    const title = document.getElementById(subId(scrollRequest.id, 'title'));
    if (title && typeof title.focus === 'function') title.focus({ preventScroll: true });
    setHighlight(scrollRequest.id);
    const t = setTimeout(() => setHighlight(null), HIGHLIGHT_MS);
    return () => clearTimeout(t);
  }, [scrollRequest]);

  if (entries.length === 0)
    return (
      <p className="rounded-lg border border-line bg-offwhite px-3 py-2 text-sm text-ink-2">
        لا توجد مشاريع في هذا التقرير.
      </p>
    );

  // ===== اشتقاق ملخّص المحفظة (عدّ فعليّ، بترتيب الإدخال الأصليّ — أرقام مطابقة تمامًا للسابق) =====
  const total = items.length;
  let stable = 0;
  let followUp = 0;
  let atRisk = 0;
  let sumSent = 0;
  let sumApproved = 0;
  let sumPending = 0;
  let withClientRequests = 0;
  let withDecisions = 0;
  let withRisk = 0;
  for (const it of items) {
    const f = it.facts;
    if (f.bucket === 'stable') stable += 1;
    else if (f.bucket === 'followUp') followUp += 1;
    else if (f.bucket === 'atRisk') atRisk += 1;
    sumSent += f.sent;
    sumApproved += f.approved;
    sumPending += f.pending;
    if (f.hasClientRequests) withClientRequests += 1;
    if (f.hasDecision) withDecisions += 1;
    if (f.hasRisk) withRisk += 1;
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
      {/* (1) التنقّل السريع حسب العميل — يُخفى عند الطباعة (بلا فائدة على الورق) */}
      <section
        id={QUICK_NAV_ID}
        className="scroll-mt-4 rounded-lg border border-navy/15 bg-white p-3 print:hidden"
      >
        <h3 className="mb-2 text-sm font-bold text-navy">الوصول السريع حسب العميل</h3>
        <div className="space-y-2">
          {groups.map((g) => {
            const counts = groupCounts(g);
            const expanded = navOpen[g.key] ?? false;
            return (
              <div key={g.key} className="rounded-lg border border-line bg-offwhite">
                <button
                  type="button"
                  aria-expanded={expanded}
                  onClick={() => setNavOpen((s) => ({ ...s, [g.key]: !expanded }))}
                  className="flex w-full flex-wrap items-center gap-x-2 gap-y-1 px-3 py-2 text-right"
                >
                  <span className="shrink-0 text-xs text-ink-2">{expanded ? '▲' : '▼'}</span>
                  <span className="font-semibold text-navy">{g.name}</span>
                  <span className="text-xs text-ink-2">
                    {g.items.length === 1 ? 'مشروع واحد' : `${g.items.length} مشروعات`}
                  </span>
                  {counts.followUp > 0 && <Badge tone="gold">🟡 متابعة ({counts.followUp})</Badge>}
                  {counts.atRisk > 0 && <Badge tone="alert">🔴 متعثّر ({counts.atRisk})</Badge>}
                  {counts.risk > 0 && <Badge tone="orange">⚠ مخاطر ({counts.risk})</Badge>}
                  {counts.decisions > 0 && <Badge tone="alert">⚑ قرارات ({counts.decisions})</Badge>}
                </button>
                {expanded && (
                  <div className="flex flex-wrap gap-2 border-t border-line px-3 py-2">
                    {g.items.map((it) => (
                      <button
                        key={it.anchorId}
                        type="button"
                        onClick={() => openProjectAndNavigate(it.anchorId, g.key)}
                        className="rounded-full border border-navy/30 bg-white px-3 py-1 text-sm text-navy hover:bg-navy/[0.06]"
                      >
                        {it.shortTitle}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </section>

      {/* (2) ملخّص المحفظة التنفيذيّ — مشتقّ بالكامل من القيم المحفوظة (بلا أيّ تغيير في الأرقام) */}
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

      {/* (3) فهرس المشاريع — صفّ لكل مشروع، نقر الاسم ⇒ نفس دالّة التنقّل المشتركة */}
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
            {groups.map((g) =>
              g.items.map((it) => {
                const entry = it.entry;
                const statusSpec = profile.statusBadges.find((b) => b.key === profile.statusKey);
                const riskSpec = profile.statusBadges.find((b) => b.key === profile.riskKey);
                const relSpec = profile.statusBadges.find((b) => b.key === profile.relationshipKey);
                const statusTone = statusSpec ? badgeToneFor(statusSpec, entry.answers[statusSpec.key]) : null;
                const riskTone = riskSpec ? badgeToneFor(riskSpec, entry.answers[riskSpec.key]) : null;
                const relTone = relSpec ? badgeToneFor(relSpec, entry.answers[relSpec.key]) : null;
                const sent = toNumber(entry.answers['deliverables_sent']);
                const approved = toNumber(entry.answers['deliverables_approved']);
                const deliverText = sent != null || approved != null ? `${approved ?? 0}/${sent ?? 0}` : '—';
                const decides = isMeaningfulPresentationValue(entry.answers[profile.decisionKey]);
                return (
                  <tr key={it.anchorId} className="border-t border-line align-top">
                    <td className="px-2 py-2">
                      <button
                        type="button"
                        onClick={() => openProjectAndNavigate(it.anchorId, g.key)}
                        className="text-right font-medium text-navy underline-offset-2 hover:underline"
                      >
                        {it.fullTitle}
                      </button>
                    </td>
                    <td className="px-2 py-2">
                      {statusTone ? <Badge tone={statusTone}>{entry.answers[statusSpec!.key]}</Badge> : '—'}
                    </td>
                    <td className="px-2 py-2 text-ink-2">
                      {displayValue(byKey.get(profile.phaseKey ?? ''), entry.answers[profile.phaseKey ?? '']) || '—'}
                    </td>
                    <td className="px-2 py-2 text-ink-2">{deliverText}</td>
                    <td className="px-2 py-2">
                      {riskTone ? <Badge tone={riskTone}>{entry.answers[riskSpec!.key]}</Badge> : '—'}
                    </td>
                    <td className="px-2 py-2">
                      {relTone ? <Badge tone={relTone}>{entry.answers[relSpec!.key]}</Badge> : '—'}
                    </td>
                    <td className="px-2 py-2">{decides ? <Badge tone="alert">📌 مطلوب</Badge> : '—'}</td>
                  </tr>
                );
              }),
            )}
          </tbody>
        </table>
      </section>

      {/* (4) بطاقات التفاصيل مجمّعة تحت عميلها (لا قائمة مسطّحة) */}
      {groups.map((g) => {
        const counts = groupCounts(g);
        return (
          <section key={g.key} className="space-y-3">
            <header className="break-after-avoid rounded-lg border border-navy/20 bg-navy/[0.04] px-3 py-2">
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                <h3 className="text-sm font-bold text-navy">العميل: {g.name}</h3>
                <span className="text-xs text-ink-2">
                  عدد المشروعات: {g.items.length}
                </span>
                {counts.followUp > 0 && <Badge tone="gold">🟡 متابعة ({counts.followUp})</Badge>}
                {counts.atRisk > 0 && <Badge tone="alert">🔴 متعثّر ({counts.atRisk})</Badge>}
                {counts.risk > 0 && <Badge tone="orange">⚠ مخاطر ({counts.risk})</Badge>}
                {counts.decisions > 0 && <Badge tone="alert">⚑ قرارات ({counts.decisions})</Badge>}
              </div>
            </header>
            {g.items.map((it, n) => (
              <ProjectCard
                key={it.anchorId}
                anchor={it.anchorId}
                order={n + 1}
                open={openCards[it.anchorId] ?? it.index === 0}
                highlighted={highlight === it.anchorId}
                onToggle={() =>
                  setOpenCards((s) => ({ ...s, [it.anchorId]: !(s[it.anchorId] ?? it.index === 0) }))
                }
                onBackToNav={backToNav}
                title={it.shortTitle}
                profile={profile}
                answers={it.entry.answers}
                byKey={byKey}
                known={known}
                label={label}
              />
            ))}
          </section>
        );
      })}
    </div>
  );
}

// حقائق مشتقّة لمشروع واحد — منطق العدّ نفسه حرفيًّا كما كان في حلقة ملخّص المحفظة.
function buildFacts(profile: PresentationProfile, e: ProjectRepeatableEntry): EntryFacts {
  const st = String(e.answers[profile.statusKey] ?? '').trim();
  const bucket: EntryFacts['bucket'] = profile.statusBuckets.stable.includes(st)
    ? 'stable'
    : profile.statusBuckets.followUp.includes(st)
      ? 'followUp'
      : profile.statusBuckets.atRisk.includes(st)
        ? 'atRisk'
        : 'none';
  const riskTone = badgeToneFor(
    profile.statusBadges.find((b) => b.key === profile.riskKey) ?? {
      key: profile.riskKey,
      emptyValues: ['', 'لا يوجد'],
      toneByValue: {},
      defaultTone: 'gold',
    },
    e.answers[profile.riskKey],
  );
  return {
    bucket,
    sent: toNumber(e.answers[profile.approvalProgress?.sentKey ?? 'deliverables_sent']) ?? 0,
    approved: toNumber(e.answers[profile.approvalProgress?.approvedKey ?? 'deliverables_approved']) ?? 0,
    pending: toNumber(e.answers['deliverables_pending']) ?? 0,
    hasClientRequests: profile.clientKeys.some((k) => isMeaningfulPresentationValue(e.answers[k])),
    hasDecision: isMeaningfulPresentationValue(e.answers[profile.decisionKey]),
    hasRisk: riskTone != null,
  };
}

// عدّادات مجموعة العميل — مشتقّة من الحقائق نفسها (بلا احتساب مزدوج وبلا قواعد جديدة).
function groupCounts(g: ClientGroup) {
  let followUp = 0;
  let atRisk = 0;
  let risk = 0;
  let decisions = 0;
  for (const it of g.items) {
    if (it.facts.bucket === 'followUp') followUp += 1;
    if (it.facts.bucket === 'atRisk') atRisk += 1;
    if (it.facts.hasRisk) risk += 1;
    if (it.facts.hasDecision) decisions += 1;
  }
  return { followUp, atRisk, risk, decisions };
}

// بطاقة مشروع واحد: قابلة للطيّ تفاعليًّا، لكنها تُفتح دائمًا عند الطباعة (print:block) ولا تُقصّ (break-inside-avoid).
function ProjectCard({
  anchor,
  order,
  open,
  highlighted,
  onToggle,
  onBackToNav,
  title,
  profile,
  answers,
  byKey,
  known,
  label,
}: {
  anchor: string;
  order: number;
  open: boolean;
  highlighted: boolean;
  onToggle: () => void;
  onBackToNav: () => void;
  title: string;
  profile: PresentationProfile;
  answers: Record<string, string>;
  byKey: Map<string, RepeatableSubField>;
  known: Set<string>;
  label: (key: string) => string;
}) {
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
    <Card
      className={`break-inside-avoid p-0 transition-shadow ${
        highlighted ? 'ring-2 ring-orange ring-offset-2' : ''
      }`}
    >
      <div id={anchor} className="scroll-mt-4" />
      {/* (A) ترويسة المشروع */}
      <div className="flex w-full items-start justify-between gap-2 border-b border-line bg-offwhite px-4 py-3">
        <div className="min-w-0">
          <h4
            id={subId(anchor, 'title')}
            tabIndex={-1}
            className="truncate font-semibold text-navy outline-none focus:underline"
          >
            {order}. {title}
          </h4>
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
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={open}
          aria-controls={subId(anchor, 'body')}
          className="shrink-0 text-xs text-ink-2 print:hidden"
        >
          {open ? '▲ طيّ' : '▼ عرض'}
        </button>
      </div>

      <div id={subId(anchor, 'body')} className={`${open ? 'block' : 'hidden'} space-y-4 p-4 print:block`}>
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

        {/* العودة إلى التنقّل السريع — بلا إعادة تحميل وبلا تغيير أيّ حالة للتقرير */}
        <div className="pt-1 print:hidden">
          <button
            type="button"
            onClick={onBackToNav}
            className="text-xs text-navy underline-offset-2 hover:underline"
          >
            ↑ العودة إلى قائمة العملاء والمشروعات
          </button>
        </div>
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

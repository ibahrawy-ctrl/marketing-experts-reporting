import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect } from 'vitest';
import { PresentationProfileReport } from './PresentationProfileReport';
import { ProjectRepeatableDisplay } from '../pages/SubmissionsPage';
import {
  resolvePresentationProfile,
  accountManagerProfile,
  isMeaningfulPresentationValue,
} from '../lib/reportPresentationProfiles';
import type { ProjectDto, ProjectRepeatableConfig, ProjectRepeatableEntry, RepeatableSubField } from '../types/api';

// ===== AMR-OUTPUT-REDESIGN-R1 — اختبارات المصيّر المدفوع بميفولة العرض (14 اختبارًا) =====
// تُثبِت: كشف Profile إدارة الحسابات، fallback للمصيّر العامّ، إخفاء الفراغات، شارات الحالة،
// بطاقات المقاييس، بطاقة القرارات (ظهور/غياب)، تتبّع المخاطر، حفظ الحقول التاريخيّة، تعدّد المشاريع،
// فتح الكل عند الطباعة، عدم تغيير البيانات، بنية RTL، وبقاء مصيّر المودريشن أخضر.

function project(id: string, name: string, clientName: string | null = null): ProjectDto {
  return {
    id, clientId: 'c-' + id, clientName, name, serviceType: 'Seo', status: 'Active',
    startDate: null, endDate: null, ownerTeamId: null, ownerTeamName: null,
    accountManagerId: null, accountManagerName: null, notes: null,
    createdAtUtc: '2026-07-01T00:00:00Z', updatedAtUtc: null, canHardDelete: true, deleteBlockReason: null,
  } as ProjectDto;
}

const AM_FIELDS: RepeatableSubField[] = [
  { key: 'project_status', label: 'حالة المشروع', type: 'Select', required: true },
  { key: 'risk_severity', label: 'شدّة المخاطر', type: 'Select', required: false },
  { key: 'client_relationship', label: 'علاقة العميل', type: 'Select', required: false },
  { key: 'current_phase', label: 'المرحلة الحالية', type: 'ShortText', required: false },
  { key: 'deliverables_sent', label: 'تسليمات مُرسلة', type: 'Number', required: false },
  { key: 'deliverables_approved', label: 'تسليمات معتمدة', type: 'Number', required: false },
  { key: 'deliverables_pending', label: 'تسليمات منتظرة', type: 'Number', required: false },
  { key: 'achievements', label: 'الإنجازات', type: 'LongText', required: true },
  { key: 'client_requests', label: 'طلبات العميل', type: 'LongText', required: false },
  { key: 'open_issues', label: 'قضايا مفتوحة', type: 'LongText', required: false },
  { key: 'delays', label: 'التأخيرات', type: 'LongText', required: false },
  { key: 'scope_changes', label: 'تغييرات النطاق', type: 'LongText', required: false },
  { key: 'decisions_required', label: 'قرارات مطلوبة', type: 'LongText', required: false },
  { key: 'internal_dependencies', label: 'تبعيّات داخلية', type: 'LongText', required: false },
  { key: 'next_steps', label: 'الخطوات القادمة', type: 'LongText', required: false },
  { key: 'commercial_opportunities', label: 'فرص تجارية', type: 'LongText', required: false },
  { key: 'evidence_link', label: 'رابط الأدلّة', type: 'ShortText', required: false },
  { key: 'notes', label: 'ملاحظات', type: 'LongText', required: false },
];

const amConfig = (fields: RepeatableSubField[] = AM_FIELDS): ProjectRepeatableConfig => ({
  projectRequired: true, minProjects: 1, maxProjects: 10, fields,
});

function entry(projectId: string, answers: Record<string, string>): ProjectRepeatableEntry {
  return { projectId, answers };
}

const fullAnswers = {
  project_status: 'متأخر',
  risk_severity: 'متوسط',
  client_relationship: 'جيدة',
  current_phase: 'التنفيذ',
  deliverables_sent: '6',
  deliverables_approved: '2',
  deliverables_pending: '4',
  achievements: 'أنجزنا صفحة الهبوط ونشرنا الحملة الأولى.',
  client_requests: 'العميل يطلب تعديل الهوية اللونيّة.',
  decisions_required: 'نحتاج قرار الإدارة بشأن تمديد الميزانية.',
  next_steps: 'إطلاق الحملة الثانية الأسبوع القادم.',
  evidence_link: 'https://example.com/proof',
};

const P = [project('p1', 'مشروع تجريبيّ أ', 'عميل تجريبيّ أ')];

// ---- 1: قالب إدارة الحسابات يختار Profile إدارة الحسابات ----
it('1 AM field signature selects the Account Manager profile', () => {
  const p = resolvePresentationProfile(AM_FIELDS, 'أيّ عنوان');
  expect(p).not.toBeNull();
  expect(p?.id).toBe(accountManagerProfile.id);
});

// ---- 2: قالب غير معروف يعود للمصيّر العامّ (fallback) ----
it('2 unknown template resolves to null and renders the generic renderer', () => {
  const genericFields: RepeatableSubField[] = [
    { key: 'activity_type', label: 'النشاط', type: 'Select', required: false },
    { key: 'count', label: 'العدد', type: 'Number', required: false },
  ];
  expect(resolvePresentationProfile(genericFields, 'قالب عام')).toBeNull();
  render(
    <ProjectRepeatableDisplay
      config={{ projectRequired: true, minProjects: 1, maxProjects: 5, fields: genericFields }}
      entries={[entry('p1', { activity_type: 'نشر', count: '5' })]}
      projects={P}
    />,
  );
  // لا ملخّص محفظة ⇒ لم يدخل مسار الـProfile.
  expect(screen.queryByText('ملخّص المحفظة التنفيذيّ')).toBeNull();
  expect(screen.getByText('النشاط')).toBeInTheDocument();
});

// ---- 3: الحقول الفارغة لا تُعرض (لا صفوف «—» سرديّة) ----
it('3 empty fields are hidden (no empty narrative sections)', () => {
  const answers = { project_status: 'على المسار', achievements: 'تمّ الإنجاز.' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  // achievements موجود ⇒ عنوانه يظهر؛ client_requests فارغ ⇒ عنوانه لا يظهر.
  expect(screen.getByText('الإنجاز الأسبوعيّ')).toBeInTheDocument();
  expect(screen.queryByText('ملاحظات وطلبات العميل')).toBeNull();
  expect(screen.queryByText('القضايا والتأخيرات')).toBeNull();
});

// ---- 4: حقول Select تُعرض كشارات ----
it('4 status Select fields render as colored badges', () => {
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', fullAnswers)]} projects={P} />);
  // قيمة الحالة تظهر داخل شارة (rounded-full) في ترويسة البطاقة و/أو الفهرس.
  const statusNodes = screen.getAllByText('متأخر');
  expect(statusNodes.some((n) => n.className.includes('rounded-full'))).toBe(true);
});

// ---- 5: الحقول الرقمية تُعرض كبطاقات مقاييس ----
it('5 numeric deliverables render as metric cards', () => {
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', fullAnswers)]} projects={P} />);
  expect(screen.getByText('أُرسل')).toBeInTheDocument();
  expect(screen.getByText('اعتُمد')).toBeInTheDocument();
  expect(screen.getByText('منتظر')).toBeInTheDocument();
  expect(screen.getByText('نسبة الاعتماد')).toBeInTheDocument();
});

// ---- 6: بطاقة القرارات تظهر عند وجود قرار (AMR-A3) ----
it('6 decisions card appears when decisions_required is populated', () => {
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', fullAnswers)]} projects={P} />);
  expect(screen.getByText('📌 قرارات مطلوبة من الإدارة')).toBeInTheDocument();
  expect(screen.getByText('نحتاج قرار الإدارة بشأن تمديد الميزانية.')).toBeInTheDocument();
});

// ---- 7: بطاقة القرارات تغيب عند الفراغ ----
it('7 decisions card is absent when decisions_required is empty', () => {
  const answers = { project_status: 'على المسار', achievements: 'تمّ.', decisions_required: '' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.queryByText('📌 قرارات مطلوبة من الإدارة')).toBeNull();
});

// ---- 8: شارة المخاطر تتبع حالة المخاطر ----
it('8 risk badge follows risk state (shown for real risk, hidden for "لا يوجد")', () => {
  const { unmount } = render(
    <PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', { project_status: 'متأخر', risk_severity: 'مرتفع', achievements: 'x' })]} projects={P} />,
  );
  expect(screen.getAllByText(/مخاطر: مرتفع/).length).toBeGreaterThanOrEqual(1);
  unmount();
  render(
    <PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', { project_status: 'متأخر', risk_severity: 'لا يوجد', achievements: 'x' })]} projects={P} />,
  );
  expect(screen.queryByText(/مخاطر:/)).toBeNull();
});

// ---- 9: الحقول التاريخيّة غير المعروفة تظهر في مجموعة «معلومات إضافية» (لا فقدان) ----
it('9 unknown historical fields appear in the "معلومات إضافية" fallback group', () => {
  const fields = [...AM_FIELDS, { key: 'legacy_kpi', label: 'مؤشّر قديم', type: 'ShortText', required: false } as RepeatableSubField];
  render(
    <PresentationProfileReport profile={accountManagerProfile} config={amConfig(fields)} entries={[entry('p1', { ...fullAnswers, legacy_kpi: 'قيمة تاريخيّة' })]} projects={P} />,
  );
  expect(screen.getByText('معلومات إضافية')).toBeInTheDocument();
  expect(screen.getByText('مؤشّر قديم')).toBeInTheDocument();
  expect(screen.getByText('قيمة تاريخيّة')).toBeInTheDocument();
});

// ---- 10: ستة مشاريع تُصيَّر مستقلّة ----
it('10 six projects render as independent cards with anchors', () => {
  const projects = Array.from({ length: 6 }, (_, i) => project(`p${i}`, `مشروع ${i}`, `عميل ${i}`));
  const entries = projects.map((p, i) => entry(p.id, { project_status: 'على المسار', achievements: `إنجاز ${i}` }));
  const { container } = render(
    <PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={entries} projects={projects} />,
  );
  const anchors = container.querySelectorAll('[id^="amr-project-"]');
  expect(anchors.length).toBe(6);
  expect(screen.getByText('إجمالي المشاريع')).toBeInTheDocument();
});

// ---- 11: وضع الطباعة يفتح كل المشاريع (المحتوى يبقى في DOM مع print:block عند الطيّ) ----
it('11 print mode keeps collapsed project content in the DOM (print:block)', () => {
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', fullAnswers)]} projects={P} />);
  const toggle = screen.getByRole('button', { name: /طيّ|عرض/ });
  fireEvent.click(toggle); // طيّ البطاقة
  // المحتوى ما زال في DOM (مخفيّ على الشاشة، لكن print:block يُظهره عند الطباعة).
  const decision = screen.getByText('نحتاج قرار الإدارة بشأن تمديد الميزانية.');
  expect(decision).toBeInTheDocument();
  const contentWrap = decision.closest('div.print\\:block');
  expect(contentWrap).not.toBeNull();
});

// ---- 12: لا تغيير لأيّ بيانات أثناء التصيير ----
it('12 rendering does not mutate the entries/answers data', () => {
  const answers = Object.freeze({ ...fullAnswers });
  const entries = Object.freeze([Object.freeze({ projectId: 'p1', answers })]) as unknown as ProjectRepeatableEntry[];
  const snapshot = JSON.stringify(entries);
  expect(() =>
    render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={entries} projects={P} />),
  ).not.toThrow();
  expect(JSON.stringify(entries)).toBe(snapshot);
});

// ---- 13: بنية RTL — فهرس المشاريع بأعمدته السبعة ----
it('13 overview table has the seven RTL columns in order', () => {
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', fullAnswers)]} projects={P} />);
  const headers = screen.getAllByRole('columnheader').map((h) => h.textContent);
  expect(headers).toEqual(['المشروع', 'الحالة', 'المرحلة', 'التسليمات', 'المخاطر', 'العلاقة', 'القرار المطلوب']);
});

// ---- 14: مصيّر المودريشن يبقى أخضر — القالب المودريشن لا يدخل مسار الـProfile ----
it('14 moderation template is unaffected (generic grouped renderer, no portfolio summary)', () => {
  // مفردات المودريشن الإنتاجيّة (isModerationVocab1) تشترط project_status + incoming_messages + cases_grid.
  const modFields: RepeatableSubField[] = [
    { key: 'project_status', label: 'project_status', type: 'ShortText', required: false },
    { key: 'incoming_messages', label: 'incoming_messages', type: 'Number', required: false },
    { key: 'answered_messages', label: 'answered_messages', type: 'Number', required: false },
    { key: 'avg_response_minutes', label: 'avg_response_minutes', type: 'Number', required: false },
    { key: 'cases_grid', label: 'cases_grid', type: 'Grid', required: false },
  ];
  expect(resolvePresentationProfile(modFields, 'تقرير المديرشن الأسبوعي')).toBeNull();
  render(
    <ProjectRepeatableDisplay
      config={{ projectRequired: true, minProjects: 1, maxProjects: 5, fields: modFields }}
      entries={[entry('p1', { project_status: 'ممتاز', incoming_messages: '120' })]}
      projects={P}
    />,
  );
  expect(screen.queryByText('ملخّص المحفظة التنفيذيّ')).toBeNull();
  // العنوان الإنتاجيّ لمجموعة المودريشن (MOD_VOCAB1_GROUPS) = «حجم العمل» — يثبت بقاء مصيّر المودريشن دون اختطاف AMR.
  expect(screen.getByText('حجم العمل')).toBeInTheDocument();
});

// ===== AMR-R1 — كشف القيمة ذات المعنى (Meaningful Value Detection) — 8 اختبارات إضافية =====
// يمنع الإيجابيّة الكاذبة: عبارات النفي/الغياب («لا يوجد»/«—»/فراغ) لا تُنشئ قرارًا/عائقًا/فرصة،
// مع الحفاظ على الرقم 0 والنصوص المفيدة التي تحوي كلمة «لا».

// عدّاد القرارات في تجانب الملخّص (يحوي العنوان + القيمة).
function decisionsCounterText(): string {
  return screen.getByText('📌 قرارات مطلوبة').closest('div')?.textContent ?? '';
}

// ---- 15: الدالّة الموحّدة isMeaningfulPresentationValue تُطبّق قاعدة النفي/الغياب بدقّة ----
it('15 isMeaningfulPresentationValue hides null/empty/negation and keeps 0 and real "لا" text', () => {
  // غير دالّة (تُخفى)
  for (const v of [null, undefined, '', '   ', '-', '—', 'لا', 'لا يوجد', 'لا يوجد ', 'لا يوجد.', 'لا توجد', 'ليس هناك', 'غير متوفر', 'N/A', 'n/a', 'N/A.']) {
    expect(isMeaningfulPresentationValue(v as unknown as string)).toBe(false);
  }
  // دالّة (تظهر) — الرقم 0 ونصوص مفيدة تحوي «لا»
  expect(isMeaningfulPresentationValue(0)).toBe(true);
  expect(isMeaningfulPresentationValue('0')).toBe(true);
  expect(isMeaningfulPresentationValue('لا يمكن إطلاق الحملة قبل اعتماد العميل')).toBe(true);
  expect(isMeaningfulPresentationValue('لا يوجد طلبات جديدة من العميل')).toBe(true);
  expect(isMeaningfulPresentationValue('تمّ الإنجاز')).toBe(true);
});

// ---- 16: decisions_required = "لا يوجد " ⇒ لا شارة قرار، لا بطاقة قرار، لا يدخل العدّاد ----
it('16 decisions_required "لا يوجد " yields no badge, no card, and zero in the counter', () => {
  const answers = { project_status: 'على المسار', achievements: 'x', decisions_required: 'لا يوجد ' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.queryByText('📌 قرارات مطلوبة من الإدارة')).toBeNull();
  expect(screen.queryByText('📌 مطلوب')).toBeNull();
  expect(decisionsCounterText()).toContain('0');
});

// ---- 17: decisions_required = "—" ⇒ لا يظهر ----
it('17 decisions_required "—" does not render a decision', () => {
  const answers = { project_status: 'على المسار', achievements: 'x', decisions_required: '—' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.queryByText('📌 قرارات مطلوبة من الإدارة')).toBeNull();
  expect(screen.queryByText('📌 مطلوب')).toBeNull();
  expect(decisionsCounterText()).toContain('0');
});

// ---- 18: decisions_required = مسافات فقط ⇒ لا يظهر ----
it('18 decisions_required whitespace-only does not render a decision', () => {
  const answers = { project_status: 'على المسار', achievements: 'x', decisions_required: '     ' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.queryByText('📌 قرارات مطلوبة من الإدارة')).toBeNull();
  expect(screen.queryByText('📌 مطلوب')).toBeNull();
  expect(decisionsCounterText()).toContain('0');
});

// ---- 19: قرار حقيقيّ ⇒ يظهر في البطاقة والجدول والعدّاد ----
it('19 a real decision renders in the card, the overview badge, and the counter', () => {
  const answers = { project_status: 'متأخر', achievements: 'x', decisions_required: 'نحتاج اعتماد الميزانية الإضافيّة.' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.getByText('📌 قرارات مطلوبة من الإدارة')).toBeInTheDocument();
  expect(screen.getByText('نحتاج اعتماد الميزانية الإضافيّة.')).toBeInTheDocument();
  expect(screen.getByText('📌 مطلوب')).toBeInTheDocument();
  expect(decisionsCounterText()).toContain('1');
});

// ---- 20: القيمة الرقميّة 0 في مقياس ⇒ تظهر ولا تُخفى ----
it('20 numeric metric value 0 is displayed (not treated as empty)', () => {
  const answers = { project_status: 'على المسار', achievements: 'x', deliverables_approved: '0' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  const metricCard = screen.getByText('اعتُمد').closest('div');
  expect(metricCard).not.toBeNull();
  expect(metricCard?.textContent).toContain('0');
});

// ---- 21: نصّ فعليّ يحوي كلمة «لا» ⇒ يظهر (لا يُعتبر فارغًا) ----
it('21 a real sentence containing "لا" is shown (not hidden as empty)', () => {
  const answers = { project_status: 'متأخر', achievements: 'x', decisions_required: 'لا يمكن إطلاق الحملة قبل اعتماد العميل' };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p1', answers)]} projects={P} />);
  expect(screen.getByText('📌 قرارات مطلوبة من الإدارة')).toBeInTheDocument();
  expect(screen.getByText('لا يمكن إطلاق الحملة قبل اعتماد العميل')).toBeInTheDocument();
  expect(decisionsCounterText()).toContain('1');
});

// ---- 22: مشروع بقيمة نفي صريحة (decisions_required = «لا يوجد ») لا يظهر كصاحب قرار مطلوب ----
it('22 project with explicit negation (decisions_required "لا يوجد ") is not a decision owner', () => {
  const p4 = project('p4', 'مشروع تجريبيّ د', 'عميل تجريبيّ د');
  const realAnswers = {
    achievements: 'بيتم النشر باستمرار علي كل المنصات ',
    current_phase: 'مستقرة ',
    risk_severity: 'منخفض',
    project_status: 'على المسار',
    client_requests: 'لا يوجد طلبات ', // جملة مفيدة تحوي «لا» ⇒ تبقى ظاهرة
    deliverables_sent: '3',
    decisions_required: 'لا يوجد ', // نفي صريح ⇒ لا قرار
    client_relationship: 'جيدة',
    deliverables_pending: '0',
    deliverables_approved: '0',
  };
  render(<PresentationProfileReport profile={accountManagerProfile} config={amConfig()} entries={[entry('p4', realAnswers)]} projects={[p4]} />);
  expect(screen.queryByText('📌 قرارات مطلوبة من الإدارة')).toBeNull();
  expect(screen.queryByText('📌 مطلوب')).toBeNull();
  expect(decisionsCounterText()).toContain('0');
  // الجملة المفيدة «لا يوجد طلبات» تبقى ظاهرة (إثبات عدم إخفاء نصّ يحوي «لا»).
  expect(screen.getByText('ملاحظات وطلبات العميل')).toBeInTheDocument();
});

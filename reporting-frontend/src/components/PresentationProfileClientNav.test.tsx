import { render, screen, fireEvent, act, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi, beforeEach, afterEach } from 'vitest';
import { PresentationProfileReport } from './PresentationProfileReport';
import { ProjectRepeatableDisplay, SubmissionDetail } from '../pages/SubmissionsPage';
import { ToastProvider } from './ActionResultToast';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import { accountManagerProfile, resolvePresentationProfile } from '../lib/reportPresentationProfiles';
import type {
  ProjectDto,
  ProjectRepeatableConfig,
  ProjectRepeatableEntry,
  RepeatableSubField,
  SubmissionDto,
  SubmissionFieldValueDto,
} from '../types/api';

// ===== AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1 — 26 اختبارًا =====
// تُثبِت: التجميع «العميل أوّلًا» مشتقّ من ClientId النظاميّ (لا من نصّ اسم المشروع)،
// التنقّل السريع حسب العميل، سلوك أزرار المشاريع (فتح/تمرير/إبراز/تركيز)، رابط العودة،
// وحدة مسار التنقّل بين الفهرس والتنقّل السريع، الحالات التاريخيّة/الاستثنائيّة،
// ترتيب الأقسام الجديد داخل صفحة التقرير، وثبات كل الأرقام والقوالب الأخرى.

// ---------- تجهيزات (بيانات مُعقّمة تحاكي بنية تقرير W30 الحقيقيّ، بلا أيّ اسم إنتاجيّ) ----------

function project(
  id: string,
  name: string,
  clientId: string | null,
  clientName: string | null,
  status: 'Active' | 'Closed' = 'Active',
): ProjectDto {
  return {
    id, clientId, clientName, name, serviceType: 'Seo', status,
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

function entry(projectId: string | null, answers: Record<string, string>): ProjectRepeatableEntry {
  return { projectId, answers } as ProjectRepeatableEntry;
}

// أربعة عملاء / ستّة مشاريع — بنفس بنية W30: عميلان بمشروعين، وعميلان بمشروع واحد،
// ومشروعان مختلفان يحملان **الاسم نفسه** تحت عميلَين مختلفَين (إثبات المرساة من ProjectId).
const PROJECTS: ProjectDto[] = [
  project('pr-a1', 'تعديلات المتجر', 'cl-a', 'عميل أوّل'),
  project('pr-b1', 'تحسين محركات البحث', 'cl-b', 'عميل ثانٍ'),
  project('pr-c1', 'تحسين محركات البحث', 'cl-c', 'عميل ثالث'),
  project('pr-d1', 'سوشيال ميديا', 'cl-d', 'عميل رابع'),
  project('pr-c2', 'الحملات الإعلانية', 'cl-c', 'عميل ثالث'),
  project('pr-a2', 'إدارة الحملات', 'cl-a', 'عميل أوّل'),
];

// ترتيب الإدخال داخل التقرير = a1, b1, c1, d1, c2, a2 (متشابك عمدًا — لإثبات أنّ التجميع لا يعتمد الترتيب).
const ENTRIES: ProjectRepeatableEntry[] = [
  entry('pr-a1', {
    project_status: 'على المسار', risk_severity: 'لا يوجد', client_relationship: 'جيدة',
    deliverables_sent: '4', deliverables_approved: '2', deliverables_pending: '2',
    achievements: 'إنجاز المشروع الأوّل.', decisions_required: 'لا يوجد',
  }),
  entry('pr-b1', {
    project_status: 'متأخر', risk_severity: 'متوسط', client_relationship: 'جيدة',
    deliverables_sent: '3', deliverables_approved: '1', deliverables_pending: '2',
    achievements: 'إنجاز المشروع الثاني.', decisions_required: 'نحتاج اعتماد الميزانية.',
  }),
  entry('pr-c1', {
    project_status: 'متعثّر', risk_severity: 'مرتفع', client_relationship: 'متوترة',
    deliverables_sent: '2', deliverables_approved: '0', deliverables_pending: '2',
    achievements: 'إنجاز المشروع الثالث.', client_requests: 'العميل يطلب تعديل الهوية.',
  }),
  entry('pr-d1', {
    project_status: 'مكتمل', risk_severity: 'لا يوجد', client_relationship: 'ممتازة',
    deliverables_sent: '1', deliverables_approved: '1', deliverables_pending: '0',
    achievements: 'إنجاز المشروع الرابع.',
  }),
  entry('pr-c2', {
    project_status: 'على المسار', risk_severity: 'لا يوجد', client_relationship: 'جيدة',
    deliverables_sent: '5', deliverables_approved: '5', deliverables_pending: '0',
    achievements: 'إنجاز المشروع الخامس.',
  }),
  entry('pr-a2', {
    project_status: 'معلّق', risk_severity: 'منخفض', client_relationship: 'جيدة',
    deliverables_sent: '0', deliverables_approved: '0', deliverables_pending: '0',
    achievements: 'إنجاز المشروع السادس.', decisions_required: 'نحتاج قرارًا بشأن النطاق.',
  }),
];

// سجلّ نداءات scrollIntoView (jsdom لا يوفّرها) — يُثبِت الوجهة الفعليّة للتنقّل.
let scrollTargets: string[] = [];

beforeEach(() => {
  scrollTargets = [];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (Element.prototype as any).scrollIntoView = function (this: Element) {
    scrollTargets.push(this.id);
  };
  tokenStore.clear();
  tokenStore.set('acc', 'ref');
  vi.restoreAllMocks();
});

afterEach(() => {
  vi.useRealTimers();
});

function renderReport(
  entries: ProjectRepeatableEntry[] = ENTRIES,
  projects: ProjectDto[] = PROJECTS,
) {
  return render(
    <PresentationProfileReport
      profile={accountManagerProfile}
      config={amConfig()}
      entries={entries}
      projects={projects}
    />,
  );
}

const nav = () => document.getElementById('amr-quick-nav') as HTMLElement;
const anchorEl = (projectId: string) => document.getElementById(`amr-project-${projectId}`);
const bodyEl = (projectId: string) => document.getElementById(`amr-pcard-${projectId}-body`);
const cardOf = (projectId: string) => anchorEl(projectId)?.parentElement as HTMLElement;
// ملاحظة: صنف الطباعة `print:block` يحوي كلمة block، لذا يُفحص الرمز المستقلّ حصرًا.
const isOpen = (projectId: string) =>
  (bodyEl(projectId)?.className ?? '').split(/\s+/).includes('block');

// زرّ عميل في التنقّل السريع (الأزرار الحاملة aria-expanded داخل قسم التنقّل).
function clientToggle(clientName: string): HTMLElement {
  const btn = Array.from(nav().querySelectorAll('button[aria-expanded]')).find((b) =>
    (b.textContent ?? '').includes(clientName),
  );
  if (!btn) throw new Error(`لا يوجد زرّ عميل باسم ${clientName}`);
  return btn as HTMLElement;
}

// قسم مجموعة العميل في بطاقات التفاصيل.
function clientSection(clientName: string): HTMLElement {
  return screen.getByText(`العميل: ${clientName}`).closest('section') as HTMLElement;
}

// ---- 1: العميل أوّلًا — التنقّل السريع حسب العميل يتصدّر مخرَج المشاريع ----
it('1 التنقّل السريع حسب العميل يظهر أوّلًا قبل ملخّص المحفظة وفهرس المشاريع', () => {
  const { container } = renderReport();
  expect(screen.getByText('الوصول السريع حسب العميل')).toBeInTheDocument();
  const text = container.textContent ?? '';
  expect(text.indexOf('الوصول السريع حسب العميل')).toBeLessThan(text.indexOf('ملخّص المحفظة التنفيذيّ'));
  expect(text.indexOf('ملخّص المحفظة التنفيذيّ')).toBeLessThan(text.indexOf('العميل: عميل أوّل'));
});

// ---- 2: التجميع بمعرّف العميل (ClientId) لا باسمه ----
it('2 التجميع يعتمد ClientId: عميلان مختلفان بالاسم نفسه لا يندمجان', () => {
  const projects = [
    project('px', 'مشروع س', 'cl-x', 'اسم مكرّر'),
    project('py', 'مشروع ص', 'cl-y', 'اسم مكرّر'),
  ];
  const entries = [
    entry('px', { project_status: 'على المسار', achievements: 'أ' }),
    entry('py', { project_status: 'على المسار', achievements: 'ب' }),
  ];
  renderReport(entries, projects);
  // مجموعتان مستقلّتان رغم تطابق الاسم.
  expect(screen.getAllByText('العميل: اسم مكرّر')).toHaveLength(2);
  expect(nav().querySelectorAll('button[aria-expanded]')).toHaveLength(2);
});

// ---- 3: عميل واحد بعدّة مشاريع = مجموعة واحدة ----
it('3 عميل واحد بمشروعين يُعرَض كمجموعة واحدة تحمل عدد المشروعات', () => {
  renderReport();
  expect(screen.getAllByText('العميل: عميل أوّل')).toHaveLength(1);
  expect(clientSection('عميل أوّل').textContent).toContain('عدد المشروعات: 2');
  expect(clientSection('عميل ثالث').textContent).toContain('عدد المشروعات: 2');
  expect(clientSection('عميل رابع').textContent).toContain('عدد المشروعات: 1');
});

// ---- 4: مشروعان بالاسم نفسه تحت عميلَين مختلفَين يبقيان منفصلَين ----
it('4 مشروعان بالاسم نفسه تحت عميلَين مختلفَين لهما مرساتان مختلفتان', () => {
  const { container } = renderReport();
  expect(anchorEl('pr-b1')).not.toBeNull();
  expect(anchorEl('pr-c1')).not.toBeNull();
  // ستّ مراسي لستّة مشاريع — مرساة واحدة لكلّ ProjectId.
  expect(container.querySelectorAll('[id^="amr-project-"]')).toHaveLength(6);
});

// ---- 5: ترتيب العملاء = ترتيب الظهور الأوّل (لا فرز أبجديّ) ----
it('5 ترتيب العملاء يتبع أوّل ظهور في التقرير لا الترتيب الأبجديّ', () => {
  renderReport();
  const navOrder = Array.from(nav().querySelectorAll('button[aria-expanded]')).map((b) =>
    (b.textContent ?? '').replace(/[▼▲]/g, '').trim(),
  );
  expect(navOrder[0]).toContain('عميل أوّل');
  expect(navOrder[1]).toContain('عميل ثانٍ');
  expect(navOrder[2]).toContain('عميل ثالث');
  expect(navOrder[3]).toContain('عميل رابع');
  const headers = screen.getAllByText(/^العميل: /).map((h) => h.textContent);
  expect(headers).toEqual([
    'العميل: عميل أوّل',
    'العميل: عميل ثانٍ',
    'العميل: عميل ثالث',
    'العميل: عميل رابع',
  ]);
});

// ---- 6: ترتيب المشاريع داخل العميل = ترتيب الإدخال الأصليّ ----
it('6 ترتيب المشاريع داخل مجموعة العميل يطابق ترتيب الإدخال', () => {
  renderReport();
  const titles = Array.from(
    clientSection('عميل أوّل').querySelectorAll('h4[id^="amr-pcard-"]'),
  ).map((h) => h.textContent);
  expect(titles).toEqual(['1. تعديلات المتجر', '2. إدارة الحملات']);
  const cTitles = Array.from(
    clientSection('عميل ثالث').querySelectorAll('h4[id^="amr-pcard-"]'),
  ).map((h) => h.textContent);
  expect(cTitles).toEqual(['1. تحسين محركات البحث', '2. الحملات الإعلانية']);
});

// ---- 7: فتح العميل يُظهر أزرار مشاريعه ----
it('7 الضغط على اسم العميل يفتح/يطوي أزرار مشاريعه', () => {
  renderReport();
  const toggle = clientToggle('عميل أوّل');
  expect(toggle.getAttribute('aria-expanded')).toBe('false');
  expect(within(nav()).queryByRole('button', { name: 'تعديلات المتجر' })).toBeNull();
  fireEvent.click(toggle);
  expect(clientToggle('عميل أوّل').getAttribute('aria-expanded')).toBe('true');
  expect(within(nav()).getByRole('button', { name: 'تعديلات المتجر' })).toBeInTheDocument();
  expect(within(nav()).getByRole('button', { name: 'إدارة الحملات' })).toBeInTheDocument();
  fireEvent.click(clientToggle('عميل أوّل'));
  expect(within(nav()).queryByRole('button', { name: 'تعديلات المتجر' })).toBeNull();
});

// ---- 8: الضغط على مشروع يفتح بطاقته إن كانت مطويّة ----
it('8 الضغط على زرّ المشروع يفتح بطاقته المطويّة', () => {
  renderReport();
  expect(isOpen('pr-a2')).toBe(false);
  fireEvent.click(clientToggle('عميل أوّل'));
  fireEvent.click(within(nav()).getByRole('button', { name: 'إدارة الحملات' }));
  expect(isOpen('pr-a2')).toBe(true);
});

// ---- 9: الضغط على مشروع ينقل إلى العنصر الصحيح (مرساة ProjectId) ----
it('9 التنقّل يستهدف مرساة المشروع المشتقّة من ProjectId', () => {
  renderReport();
  fireEvent.click(clientToggle('عميل ثالث'));
  fireEvent.click(within(nav()).getByRole('button', { name: 'الحملات الإعلانية' }));
  expect(scrollTargets[scrollTargets.length - 1]).toBe('amr-project-pr-c2');
  // التركيز انتقل إلى عنوان المشروع (وصوليًّا) بلا كسر موضع الصفحة.
  expect(document.activeElement?.id).toBe('amr-pcard-pr-c2-title');
});

// ---- 10: إبراز بصريّ مؤقّت يُزال تلقائيًّا ----
it('10 المشروع المقصود يُبرَز مؤقّتًا ثمّ يُزال الإبراز تلقائيًّا', () => {
  vi.useFakeTimers();
  renderReport();
  fireEvent.click(clientToggle('عميل رابع'));
  fireEvent.click(within(nav()).getByRole('button', { name: 'سوشيال ميديا' }));
  expect(cardOf('pr-d1').className).toContain('ring-2');
  act(() => {
    vi.advanceTimersByTime(3000);
  });
  expect(cardOf('pr-d1').className).not.toContain('ring-2');
});

// ---- 11: لا يُفتح مشروع آخر يحمل الاسم نفسه ----
it('11 فتح مشروع لا يفتح مشروعًا آخر بالاسم نفسه تحت عميل مختلف', () => {
  renderReport();
  expect(isOpen('pr-b1')).toBe(false);
  expect(isOpen('pr-c1')).toBe(false);
  fireEvent.click(clientToggle('عميل ثالث'));
  fireEvent.click(within(nav()).getByRole('button', { name: 'تحسين محركات البحث' }));
  expect(isOpen('pr-c1')).toBe(true);
  // المشروع المتطابق بالاسم تحت العميل الثاني يبقى كما هو.
  expect(isOpen('pr-b1')).toBe(false);
  // ولا يُغلق أيّ مشروع مفتوح مسبقًا (البطاقة الأولى تبقى مفتوحة).
  expect(isOpen('pr-a1')).toBe(true);
});

// ---- 12: رابط العودة يعيد إلى قسم التنقّل السريع ----
it('12 رابط «العودة إلى قائمة العملاء والمشروعات» ينقل إلى قسم التنقّل السريع', () => {
  renderReport();
  const backs = screen.getAllByRole('button', { name: '↑ العودة إلى قائمة العملاء والمشروعات' });
  expect(backs).toHaveLength(6);
  fireEvent.click(backs[0]);
  expect(scrollTargets[scrollTargets.length - 1]).toBe('amr-quick-nav');
});

// ---- 13: اسم المشروع في الفهرس يستعمل دالّة التنقّل نفسها ----
it('13 نقر اسم المشروع في فهرس المشاريع يستعمل مسار التنقّل نفسه', () => {
  renderReport();
  fireEvent.click(screen.getByRole('button', { name: 'إدارة الحملات — عميل أوّل' }));
  expect(isOpen('pr-a2')).toBe(true);
  expect(scrollTargets[scrollTargets.length - 1]).toBe('amr-project-pr-a2');
  // ومجموعة العميل في التنقّل السريع تُفتح تلقائيًّا (مسار واحد لا مساران).
  expect(clientToggle('عميل أوّل').getAttribute('aria-expanded')).toBe('true');
});

// ---- 14: مشروع بلا عميل قابل للحلّ ⇒ مجموعة احتياطيّة بلا فقدان ----
it('14 المشروع غير المرتبط بعميل يُوضع في «عميل غير محدّد / بيانات تاريخية» بلا فقدان', () => {
  const projects = [project('pk', 'مشروع معروف', 'cl-k', 'عميل معروف')];
  const entries = [
    entry('pk', { project_status: 'على المسار', achievements: 'أ' }),
    entry('pr-ghost', { project_status: 'على المسار', achievements: 'مشروع تاريخيّ' }), // ProjectId غير قابل للحلّ
    entry(null, { project_status: 'على المسار', achievements: 'بلا مشروع' }), // بلا ProjectId
  ];
  const { container } = renderReport(entries, projects);
  expect(screen.getByText('العميل: عميل غير محدّد / بيانات تاريخية')).toBeInTheDocument();
  expect(screen.getByText('إجمالي المشاريع').closest('div')?.textContent).toContain('3');
  expect(container.textContent).toContain('مشروع غير معروف');
  expect(container.textContent).toContain('بدون مشروع محدّد');
  expect(container.textContent).toContain('مشروع تاريخيّ');
});

// ---- 15: عميل مؤرشف (مشروع مُغلق) لا يفقد مشروعه ----
it('15 المشروع تحت عميل مؤرشف/مشروع مُغلق يبقى مجمّعًا تحت عميله', () => {
  const projects = [project('pz', 'مشروع مؤرشف', 'cl-z', 'عميل مؤرشف', 'Closed')];
  const entries = [entry('pz', { project_status: 'مكتمل', achievements: 'أُغلق المشروع.' })];
  renderReport(entries, projects);
  expect(screen.getByText('العميل: عميل مؤرشف')).toBeInTheDocument();
  expect(screen.getByText('1. مشروع مؤرشف')).toBeInTheDocument();
  expect(screen.queryByText('العميل: عميل غير محدّد / بيانات تاريخية')).toBeNull();
});

// ---- 19: كل الأرقام والحسابات كما هي (ملخّص المحفظة) ----
it('19 أرقام ملخّص المحفظة مطابقة للمصدر المحفوظ بلا أيّ تغيير في قواعد الاحتساب', () => {
  renderReport();
  const tile = (label: string) => screen.getByText(label).closest('div')?.textContent ?? '';
  expect(tile('إجمالي المشاريع')).toContain('6');
  expect(tile('🟢 على المسار / مكتمل')).toContain('3');
  expect(tile('🟡 يحتاج متابعة')).toContain('2');
  expect(tile('🔴 متعثّر')).toContain('1');
  expect(tile('تسليمات أُرسلت')).toContain('15');
  expect(tile('تسليمات اعتُمدت')).toContain('9');
  expect(tile('تسليمات منتظرة')).toContain('6');
  expect(tile('⚠ مشاريع بها مخاطر')).toContain('3');
  expect(tile('📋 مشاريع بطلبات عميل')).toContain('1');
  expect(tile('📌 قرارات مطلوبة')).toContain('2');
  // فهرس المشاريع يبقى بأعمدته السبعة ذاتها.
  expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
    'المشروع', 'الحالة', 'المرحلة', 'التسليمات', 'المخاطر', 'العلاقة', 'القرار المطلوب',
  ]);
});

// ---- 20: القيمة الرقميّة 0 تظل ظاهرة ----
it('20 القيمة الرقميّة 0 داخل مجموعة العميل تظل ظاهرة (لا تُعامَل كفراغ)', () => {
  renderReport();
  const sec = clientSection('عميل أوّل');
  const zeroMetric = within(sec).getAllByText('اعتُمد')[1]; // بطاقة المشروع الثاني (قيمها 0)
  expect(zeroMetric.closest('div')?.textContent).toContain('0');
});

// ---- 21: الطباعة تفتح كل المشاريع وتُخفي التنقّل السريع ----
it('21 الطباعة: التنقّل السريع مخفيّ، وكل بطاقات المشاريع مفتوحة ولا تُقصّ', () => {
  const { container } = renderReport();
  expect(nav().className).toContain('print:hidden');
  const bodies = Array.from(container.querySelectorAll('[id^="amr-pcard-"][id$="-body"]'));
  expect(bodies).toHaveLength(6);
  for (const b of bodies) expect(b.className).toContain('print:block');
  // ترويسة العميل لا تنفصل عن أوّل مشروع، والبطاقة لا تُقصّ بين صفحتين.
  expect(clientSection('عميل أوّل').querySelector('header')?.className).toContain('break-after-avoid');
  expect(cardOf('pr-a1').className).toContain('break-inside-avoid');
  // أزرار العودة وأزرار الطيّ مخفيّة على الورق.
  const back = screen.getAllByRole('button', { name: '↑ العودة إلى قائمة العملاء والمشروعات' })[0];
  expect(back.parentElement?.className).toContain('print:hidden');
});

// ---- 22: بنية الجوال سليمة (التفاف بلا قصّ + شبكة متجاوبة) ----
it('22 بنية الجوال: أزرار المشاريع تلتفّ، وشبكة الملخّص متجاوبة، والاتجاه RTL محفوظ', () => {
  renderReport();
  fireEvent.click(clientToggle('عميل ثالث'));
  const pillsWrap = within(nav()).getByRole('button', { name: 'الحملات الإعلانية' }).parentElement!;
  expect(pillsWrap.className).toContain('flex-wrap');
  expect(clientToggle('عميل ثالث').className).toContain('flex-wrap');
  expect(clientToggle('عميل ثالث').className).toContain('text-right');
  const grid = screen.getByText('إجمالي المشاريع').closest('div')?.parentElement;
  expect(grid?.className).toContain('grid-cols-2');
  expect(grid?.className).toContain('lg:grid-cols-5');
  // الفهرس يبقى قابلًا للتمرير أفقيًّا على الشاشات الضيّقة.
  expect(screen.getAllByRole('columnheader')[0].closest('section')?.className).toContain('overflow-x-auto');
});

// ---- 23: نسخة V2 القديمة من قالب مدير الحسابات تبقى على المصيّر العامّ ----
it('23 نسخة AM V2 (المخطط القديم) لا تدخل مسار الـProfile وتبقى على المصيّر العامّ', () => {
  const v2Fields: RepeatableSubField[] = [
    { key: 'status', label: 'الحالة', type: 'Select', required: false },
    { key: 'achievements', label: 'الإنجازات', type: 'LongText', required: false },
    { key: 'blockers', label: 'المعوّقات', type: 'LongText', required: false },
    { key: 'needsTeam', label: 'يحتاج الفريق', type: 'LongText', required: false },
    { key: 'needsClient', label: 'يحتاج العميل', type: 'LongText', required: false },
    { key: 'decisions', label: 'القرارات', type: 'LongText', required: false },
    { key: 'priority', label: 'الأولوية', type: 'LongText', required: false },
  ];
  expect(resolvePresentationProfile(v2Fields, '🤝 تقرير إدارة الحسابات العملاء')).toBeNull();
  render(
    <ProjectRepeatableDisplay
      config={{ projectRequired: true, minProjects: 1, maxProjects: 10, fields: v2Fields }}
      entries={[entry('pr-a1', { status: 'جيد', achievements: 'تمّ' })]}
      projects={PROJECTS}
      templateTitle="🤝 تقرير إدارة الحسابات العملاء"
    />,
  );
  expect(screen.queryByText('الوصول السريع حسب العميل')).toBeNull();
  expect(screen.queryByText('ملخّص المحفظة التنفيذيّ')).toBeNull();
  expect(screen.getByText('الحالة')).toBeInTheDocument();
});

// ---- 24: تصميم المودريشن لم يتغيّر ----
it('24 قالب المودريشن يبقى على مصيّره الخاصّ بلا تنقّل عميل ولا ملخّص محفظة', () => {
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
      entries={[entry('pr-a1', { project_status: 'ممتاز', incoming_messages: '120' })]}
      projects={PROJECTS}
      templateTitle="تقرير المديرشن الأسبوعي"
    />,
  );
  expect(screen.queryByText('الوصول السريع حسب العميل')).toBeNull();
  expect(screen.getByText('حجم العمل')).toBeInTheDocument();
});

// ---- 25: قالب آخر بلا Profile لم يتغيّر ----
it('25 قالب آخر بلا Profile يبقى على المصيّر العامّ دون تجميع بالعميل', () => {
  const genericFields: RepeatableSubField[] = [
    { key: 'activity_type', label: 'النشاط', type: 'Select', required: false },
    { key: 'count', label: 'العدد', type: 'Number', required: false },
  ];
  expect(resolvePresentationProfile(genericFields, 'قالب عام')).toBeNull();
  render(
    <ProjectRepeatableDisplay
      config={{ projectRequired: true, minProjects: 1, maxProjects: 5, fields: genericFields }}
      entries={[entry('pr-a1', { activity_type: 'نشر', count: '5' })]}
      projects={PROJECTS}
      templateTitle="قالب عام"
    />,
  );
  expect(screen.queryByText('الوصول السريع حسب العميل')).toBeNull();
  expect(screen.queryByText('ملخّص المحفظة التنفيذيّ')).toBeNull();
  expect(screen.getByText('النشاط')).toBeInTheDocument();
});

// ---- 26: لا تعديل لأيّ بيانات ----
it('26 التصيير والتنقّل لا يعدّلان أيّ بيانات (لا كتابة على المدخلات)', () => {
  const frozen = Object.freeze(
    ENTRIES.map((e) => Object.freeze({ projectId: e.projectId, answers: Object.freeze({ ...e.answers }) })),
  ) as unknown as ProjectRepeatableEntry[];
  const snapshot = JSON.stringify(frozen);
  renderReport(frozen);
  fireEvent.click(clientToggle('عميل أوّل'));
  fireEvent.click(within(nav()).getByRole('button', { name: 'إدارة الحملات' }));
  fireEvent.click(screen.getAllByRole('button', { name: '↑ العودة إلى قائمة العملاء والمشروعات' })[0]);
  expect(JSON.stringify(frozen)).toBe(snapshot);
  expect(JSON.stringify(PROJECTS)).toContain('pr-a1');
});

// ===== ترتيب أقسام الصفحة (16–18) — تصيير حقيقيّ لتفاصيل التقرير =====

const me = {
  userId: 'u-viewer', fullName: 'مراجع', email: 'viewer@test.local',
  roles: ['Manager'], expectedReportCadence: 'Weekly', jobRoleCode: null,
};

function fv(
  templateFieldId: string,
  label: string,
  fieldType: SubmissionFieldValueDto['fieldType'],
  extra: Partial<SubmissionFieldValueDto> = {},
): SubmissionFieldValueDto {
  return {
    templateFieldId, label, fieldType,
    valueText: null, valueNumber: null, valueDate: null, valueBool: null, valueJson: null,
    isRequired: false, helpText: null, configJson: null,
    ...extra,
  };
}

function submissionWith(prsFields: RepeatableSubField[], templateTitle: string): SubmissionDto {
  return {
    id: 'sub-amr-1',
    reportTemplateVersionId: 'tv-4',
    templateTitle,
    submitterId: 'u-am', submitterName: 'مديرة الحسابات',
    teamId: null, departmentId: null,
    periodType: 'Weekly', periodKey: '2026-W30',
    status: 'Closed', submittedAtUtc: '2026-07-23T09:00:00Z', closedAtUtc: null,
    currentApproverId: null, canEdit: false,
    fieldValues: [
      fv('f0', 'بيانات التقرير', 'SectionHeader'),
      fv('f1', 'الملخص الأسبوعي', 'LongText', { valueText: 'ملخّص الأسبوع التجريبيّ.' }),
      fv('f2', 'أبرز التحديات', 'LongText', { valueText: 'تحدٍّ تجريبيّ للأسبوع.' }),
      fv('f3', 'المشروعات', 'SectionHeader'),
      fv('f4', 'المشروعات', 'ProjectRepeatableSection', {
        valueJson: JSON.stringify(ENTRIES),
        configJson: JSON.stringify(amConfig(prsFields)),
      }),
    ],
    approvalSteps: [],
    clientId: null, clientName: null, projectId: null, projectName: null,
  } as SubmissionDto;
}

function mockDetailApi(sub: SubmissionDto) {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === '/submissions/sub-amr-1') return Promise.resolve({ data: sub } as never);
    if (url === '/projects') return Promise.resolve({ data: PROJECTS } as never);
    return Promise.resolve({ data: [] } as never);
  });
}

async function renderDetail(sub: SubmissionDto) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  mockDetailApi(sub);
  const utils = render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter>
            <SubmissionDetail id="sub-amr-1" onBack={() => {}} />
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
  await screen.findByText('📁 تفاصيل المشاريع / العملاء', undefined, { timeout: 3000 }).catch(() => null);
  return utils;
}

// ---- 16: النظرة العامة بعد تفاصيل المشروعات ----
it('16 «نظرة عامة» تظهر بعد قسم «تفاصيل العملاء والمشروعات»', async () => {
  const { container } = await renderDetail(submissionWith(AM_FIELDS, '🤝 تقرير إدارة الحسابات العملاء'));
  await screen.findByText('تفاصيل العملاء والمشروعات');
  const text = container.textContent ?? '';
  expect(text.indexOf('تفاصيل العملاء والمشروعات')).toBeGreaterThanOrEqual(0);
  expect(text.indexOf('نظرة عامة')).toBeGreaterThan(text.indexOf('تفاصيل العملاء والمشروعات'));
});

// ---- 17: الملخّص الأسبوعي بعد تفاصيل المشروعات ----
it('17 «الملخص الأسبوعي» يظهر بعد تفاصيل المشروعات ولا يُفقَد محتواه', async () => {
  const { container } = await renderDetail(submissionWith(AM_FIELDS, '🤝 تقرير إدارة الحسابات العملاء'));
  await screen.findByText('تفاصيل العملاء والمشروعات');
  const text = container.textContent ?? '';
  expect(text.indexOf('الملخص الأسبوعي')).toBeGreaterThan(text.indexOf('تفاصيل العملاء والمشروعات'));
  expect(screen.getByText('ملخّص الأسبوع التجريبيّ.')).toBeInTheDocument();
});

// ---- 18: أبرز التحديات بعد تفاصيل المشروعات ----
it('18 «أبرز التحديات» تظهر بعد تفاصيل المشروعات ولا يُفقَد محتواها', async () => {
  const { container } = await renderDetail(submissionWith(AM_FIELDS, '🤝 تقرير إدارة الحسابات العملاء'));
  await screen.findByText('تفاصيل العملاء والمشروعات');
  const text = container.textContent ?? '';
  expect(text.indexOf('أبرز التحديات')).toBeGreaterThan(text.indexOf('تفاصيل العملاء والمشروعات'));
  expect(screen.getByText('تحدٍّ تجريبيّ للأسبوع.')).toBeInTheDocument();
  // والتنقّل السريع حسب العميل حاضر داخل الصفحة الحقيقيّة.
  expect(screen.getByText('الوصول السريع حسب العميل')).toBeInTheDocument();
});

// ---- 25-ب: قالب بلا Profile يحتفظ بترتيبه القديم داخل الصفحة (نظرة عامة أوّلًا) ----
it('25ب قالب بلا Profile يحتفظ بالترتيب القائم: «نظرة عامة» قبل تفاصيل المشاريع', async () => {
  const genericFields: RepeatableSubField[] = [
    { key: 'activity_type', label: 'النشاط', type: 'Select', required: false },
    { key: 'count', label: 'العدد', type: 'Number', required: false },
  ];
  const { container } = await renderDetail(submissionWith(genericFields, 'تقرير عام أسبوعي'));
  await screen.findByText('📁 تفاصيل المشاريع / العملاء');
  const text = container.textContent ?? '';
  expect(text.indexOf('نظرة عامة')).toBeLessThan(text.indexOf('📁 تفاصيل المشاريع / العملاء'));
  expect(screen.queryByText('تفاصيل العملاء والمشروعات')).toBeNull();
});

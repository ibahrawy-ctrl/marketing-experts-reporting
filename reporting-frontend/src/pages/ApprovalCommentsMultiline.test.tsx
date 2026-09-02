import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { ToastProvider } from '../components/ActionResultToast';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import { SubmissionDetail } from './SubmissionsPage';
import { NotificationsBell } from '../components/NotificationsBell';
import { ProjectGovernanceTab } from '../components/project360/ProjectGovernanceTab';

// REPORT-APPROVAL-COMMENTS-MULTILINE-HOTFIX-R1 — مواصفة سلوك تعليق الاعتماد متعدّد الأسطر.
// كلّ الاختبارات واجهيّة بحتة (لا Backend، لا API contract، لا Workflow).

const SUB_ID = '11111111-1111-1111-1111-111111111111';
const APPROVER_ID = 'approver-1';

// النصّ المرجعيّ المعتمَد في التذكرة: أربعة أسطر بينها سطر فارغ واحد.
const MULTILINE = 'السطر الأول\nالسطر الثاني\n\nالسطر الرابع';
// تعليق تاريخيّ أحاديّ السطر (توافق خلفيّ).
const LEGACY_SINGLE_LINE = 'تعليق تاريخيّ أحاديّ السطر بلا أسطر جديدة';
// نصّ طويل (> 1000 حرف) بلا مسافات — أسوأ حالة للالتفاف الأفقيّ.
const LONG_TEXT = 'ط'.repeat(1200);
// R22B-MULTILINE-RESTORE: نصّ مختلط يغطّي عقد الوظيفة رقم 9 — عربيّة وإنجليزيّة وأرقام وترقيم
// عربيّ ولاتينيّ ورموز، موزَّعة على ثلاثة أسطر. أيّ تطبيع أو هروب سيكسر المطابقة الحرفيّة.
const MIXED_CHARSET = 'السطر الأول: Q3-2026 «مراجعة»\nLine 2 — 45.7% ✓ (مقبول)\nالسطر الثالث؟ نعم! #تسويق_رقميّ';

const me = {
  userId: APPROVER_ID,
  fullName: 'المعتمِد',
  email: 'approver@test.local',
  roles: ['Manager'],
  expectedReportCadence: 'Weekly',
  jobRoleCode: null,
};

function submission(overrides: Record<string, unknown> = {}) {
  return {
    id: SUB_ID,
    reportTemplateVersionId: 'ver-1',
    templateTitle: 'تقرير أسبوعيّ',
    submitterId: 'emp-1',
    submitterName: 'موظّف',
    teamId: null,
    departmentId: null,
    periodType: 'Weekly',
    periodKey: '2026-W20',
    status: 'Submitted',
    submittedAtUtc: '2026-05-10T08:00:00Z',
    closedAtUtc: null,
    currentApproverId: APPROVER_ID,
    canEdit: false,
    fieldValues: [],
    approvalSteps: [],
    clientId: null,
    clientName: null,
    projectId: null,
    projectName: null,
    ...overrides,
  };
}

function mockGet(sub: Record<string, unknown>) {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/auth/me') return Promise.resolve({ data: me } as never);
    if (url === `/submissions/${SUB_ID}`) return Promise.resolve({ data: sub } as never);
    return Promise.resolve({ data: [] } as never);
  });
}

function renderDetail(sub = submission()) {
  mockGet(sub);
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const utils = render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter>
            <SubmissionDetail id={SUB_ID} onBack={() => {}} />
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
  return { ...utils, post };
}

/** حقل «ملاحظة / سبب» داخل بطاقة إجراء الاعتماد. */
async function findCommentField() {
  return await screen.findByPlaceholderText('اكتب سبب القرار…');
}

/** يبحث عن عنصر <p> نصّه يطابق القيمة حرفيًّا (بلا تطبيع مسافات). */
function findParagraphByExactText(container: HTMLElement, text: string) {
  return Array.from(container.querySelectorAll('p')).find((p) => p.textContent === text);
}

beforeEach(() => {
  tokenStore.clear();
  tokenStore.set('acc', 'ref');
  vi.restoreAllMocks();
});

describe('REPORT-APPROVAL-COMMENTS-MULTILINE-HOTFIX-R1', () => {
  it('1. حقل الملاحظة صار <textarea> لا <input>', async () => {
    renderDetail();
    const field = await findCommentField();
    expect(field.tagName).toBe('TEXTAREA');
    expect(field.tagName).not.toBe('INPUT');
    // ارتفاع مبدئيّ مناسب + قابليّة التمدّد رأسيًّا.
    expect(field).toHaveAttribute('rows', '4');
    expect(field.className).toContain('resize-y');
  });

  it('2. القيمة أحاديّة السطر تعمل كما كانت (لا انحدار)', async () => {
    const user = userEvent.setup();
    renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    await user.type(field, 'سبب مختصر');
    expect(field.value).toBe('سبب مختصر');
    expect(field.value).not.toContain('\n');
  });

  it('3. القيمة التي تحتوي \\n تبقى محفوظة في الحالة (state)', async () => {
    renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    fireEvent.change(field, { target: { value: MULTILINE } });
    await waitFor(() => expect(field.value).toBe(MULTILINE));
    expect((field.value.match(/\n/g) ?? []).length).toBe(3);
    expect(field.value.split('\n')).toHaveLength(4);
  });

  it('4. Enter داخل الحقل يُنشئ سطرًا جديدًا ولا يُرسِل الطلب', async () => {
    const user = userEvent.setup();
    const { post } = renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    await user.type(field, 'الأول{Enter}الثاني');
    expect(field.value).toBe('الأول\nالثاني');
    // لا استدعاء API نتج عن Enter (لا submit غير مقصود).
    expect(post).not.toHaveBeenCalled();
  });

  it('5. «اعتماد» يرسل التعليق متعدّد الأسطر كما هو حرفيًّا', async () => {
    const user = userEvent.setup();
    const { post } = renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    fireEvent.change(field, { target: { value: MULTILINE } });
    await user.click(screen.getByRole('button', { name: 'اعتماد' }));
    await waitFor(() => expect(post).toHaveBeenCalled());
    expect(post).toHaveBeenCalledWith(`/submissions/${SUB_ID}/approve`, { comment: MULTILINE });
  });

  it('6. «إعادة للتعديل» يرسل التعليق متعدّد الأسطر كما هو حرفيًّا', async () => {
    const user = userEvent.setup();
    const { post } = renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    fireEvent.change(field, { target: { value: MULTILINE } });
    await user.click(screen.getByRole('button', { name: 'إعادة للتعديل' }));
    await waitFor(() => expect(post).toHaveBeenCalled());
    expect(post).toHaveBeenCalledWith(`/submissions/${SUB_ID}/return`, { comment: MULTILINE });
  });

  it('7. «تصعيد» يرسل التعليق متعدّد الأسطر كما هو حرفيًّا', async () => {
    const user = userEvent.setup();
    const { post } = renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    fireEvent.change(field, { target: { value: MULTILINE } });
    await user.click(screen.getByRole('button', { name: 'تصعيد' }));
    await waitFor(() => expect(post).toHaveBeenCalled());
    expect(post).toHaveBeenCalledWith(`/submissions/${SUB_ID}/escalate`, { comment: MULTILINE });
  });

  it('8. عرض approvalSteps.comment يحفظ الأسطر (whitespace-pre-wrap)', async () => {
    const { container } = renderDetail(
      submission({
        approvalSteps: [
          { level: 1, approverId: APPROVER_ID, approverName: 'المعتمِد', status: 'Approved', comment: MULTILINE, decidedAtUtc: '2026-05-11T08:00:00Z' },
        ],
      }),
    );
    await screen.findByText('ملاحظات الاعتماد');
    const el = findParagraphByExactText(container, MULTILINE);
    expect(el).toBeDefined();
    expect(el!.className).toContain('whitespace-pre-wrap');
    expect(el!.className).toContain('break-words');
    // النصّ المعروض مطابق حرفيًّا للمُخزَّن (لا قصّ ولا تطبيع).
    expect(el!.textContent).toBe(MULTILINE);
  });

  it('9. جرس الإشعارات يحفظ الأسطر في نصّ الإشعار (whitespace-pre-wrap)', async () => {
    const user = userEvent.setup();
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url === '/notifications/unread-count') return Promise.resolve({ data: { count: 1 } } as never);
      if (url === '/notifications')
        return Promise.resolve({
          data: [
            {
              id: 'n-1',
              type: 'submission.returned',
              title: 'أُعيد تقريرك للتعديل',
              body: MULTILINE,
              link: null,
              isRead: false,
              createdAtUtc: '2026-05-11T08:00:00Z',
            },
          ],
        } as never);
      return Promise.resolve({ data: [] } as never);
    });
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never);

    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(
      <QueryClientProvider client={qc}>
        <MemoryRouter>
          <NotificationsBell />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await user.click(await screen.findByRole('button', { name: 'الإشعارات' }));
    await screen.findByText('أُعيد تقريرك للتعديل');
    const el = findParagraphByExactText(container, MULTILINE);
    expect(el).toBeDefined();
    expect(el!.className).toContain('whitespace-pre-wrap');
    expect(el!.className).toContain('break-words');
    expect(el!.textContent).toBe(MULTILINE);
  });

  it('10. نصّ طويل جدًّا (>1000 حرف) يُعرَض بلا انهيار', async () => {
    const { container } = renderDetail(
      submission({
        approvalSteps: [
          { level: 1, approverId: APPROVER_ID, approverName: 'المعتمِد', status: 'Approved', comment: LONG_TEXT, decidedAtUtc: '2026-05-11T08:00:00Z' },
        ],
      }),
    );
    await screen.findByText('ملاحظات الاعتماد');
    const el = findParagraphByExactText(container, LONG_TEXT);
    expect(el).toBeDefined();
    expect(el!.textContent).toHaveLength(1200);
    // break-words يمنع تجاوز الحاوية أفقيًّا لكلمة بلا فواصل.
    expect(el!.className).toContain('break-words');
  });

  it('11. التوافق الخلفيّ: تعليق تاريخيّ أحاديّ السطر يُعرَض كما كان', async () => {
    const { container } = renderDetail(
      submission({
        approvalSteps: [
          { level: 1, approverId: APPROVER_ID, approverName: 'المعتمِد', status: 'Approved', comment: LEGACY_SINGLE_LINE, decidedAtUtc: '2026-05-11T08:00:00Z' },
        ],
      }),
    );
    await screen.findByText('ملاحظات الاعتماد');
    const el = findParagraphByExactText(container, LEGACY_SINGLE_LINE);
    expect(el).toBeDefined();
    expect(el!.textContent).toBe(LEGACY_SINGLE_LINE);
    expect(el!.textContent).not.toContain('\n');
  });

  it('12. تعليق فارغ يُرسَل null (عقد الـAPI لم يتغيّر)', async () => {
    const user = userEvent.setup();
    const { post } = renderDetail();
    await findCommentField();
    await user.click(screen.getByRole('button', { name: 'اعتماد' }));
    await waitFor(() => expect(post).toHaveBeenCalled());
    expect(post).toHaveBeenCalledWith(`/submissions/${SUB_ID}/approve`, { comment: null });
  });

  // ── R22B-MULTILINE-RESTORE — تغطية أضيفت بعد حادثة 1 سبتمبر ─────────────────
  // العيب عاد لأنّ الإصلاح واختباراته عاشا على فرع يتيم. هذان الاختباران يغلقان
  // آخر فجوتين في عقد الوظيفة: سلامة المحارف (البند 9)، وعرض Project 360 (البند 6).

  it('13. سلامة المحارف: عربيّة/إنجليزيّة/أرقام/ترقيم تمرّ بلا تشويه عبر الإدخال والإرسال والعرض', async () => {
    const user = userEvent.setup();
    const { post, unmount } = renderDetail();
    const field = (await findCommentField()) as HTMLTextAreaElement;
    fireEvent.change(field, { target: { value: MIXED_CHARSET } });
    await waitFor(() => expect(field.value).toBe(MIXED_CHARSET));
    // ثلاثة أسطر بالضبط، بلا \r ولا هروب.
    expect(field.value.split('\n')).toHaveLength(3);
    expect(field.value).not.toContain('\r');
    await user.click(screen.getByRole('button', { name: 'اعتماد' }));
    await waitFor(() => expect(post).toHaveBeenCalled());
    expect(post).toHaveBeenCalledWith(`/submissions/${SUB_ID}/approve`, { comment: MIXED_CHARSET });
    unmount();

    // ونفس النصّ يعود من الـAPI فيُعرَض حرفيًّا بلا دمج أسطر.
    const { container } = renderDetail(
      submission({
        approvalSteps: [
          { level: 1, approverId: APPROVER_ID, approverName: 'المعتمِد', status: 'Returned', comment: MIXED_CHARSET, decidedAtUtc: '2026-05-11T08:00:00Z' },
        ],
      }),
    );
    await screen.findByText('ملاحظات الاعتماد');
    const el = findParagraphByExactText(container, MIXED_CHARSET);
    expect(el).toBeDefined();
    expect(el!.textContent).toBe(MIXED_CHARSET);
    expect(el!.className).toContain('whitespace-pre-wrap');
  });

  it('14. Project 360 يعرض الملاحظة الإداريّة متعدّدة الأسطر بلا دمج', async () => {
    const PROJECT_ID = '22222222-2222-2222-2222-222222222222';
    vi.spyOn(api, 'get').mockImplementation((url: string) => {
      if (url === `/projects/${PROJECT_ID}/notes`)
        return Promise.resolve({
          data: [
            {
              id: 'note-1',
              noteType: 'Instruction',
              status: 'Open',
              requiresAction: false,
              body: MULTILINE,
              authorFullName: 'المعتمِد',
              createdAtUtc: '2026-05-11T08:00:00Z',
            },
          ],
        } as never);
      return Promise.resolve({ data: [] } as never);
    });

    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(
      <QueryClientProvider client={qc}>
        <MemoryRouter>
          <ProjectGovernanceTab projectId={PROJECT_ID} />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await screen.findByText('الملاحظات الإداريّة');
    const el = await waitFor(() => {
      const found = findParagraphByExactText(container, MULTILINE);
      expect(found).toBeDefined();
      return found!;
    });
    expect(el.className).toContain('whitespace-pre-wrap');
    expect(el.textContent).toBe(MULTILINE);
    expect((el.textContent!.match(/\n/g) ?? []).length).toBe(3);
  });
});

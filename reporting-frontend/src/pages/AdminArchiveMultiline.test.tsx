import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import AdminArchivePage from './AdminArchivePage';

// R22B/MULTILINE-ADMIN-ARCHIVE — السطح الخامس في عقد الأسطر المتعدّدة.
// جدول «لقطة سير العمل» داخل نافذة تفاصيل الأرشيف كان يعرض تعليق الاعتماد في <td>
// بلا معالجة، و<td> الافتراضيّ `white-space: normal` يطوي \n إلى مسافة واحدة ⟹ التعليق
// التاريخيّ متعدّد الأسطر يصل للمراجع الإداريّ سطرًا واحدًا ملتصقًا. الاختبار واجهيّ بحت.

const ITEM_ID = '22222222-2222-2222-2222-222222222222';
const MULTILINE = 'السطر الأول\nالسطر الثاني\n\nالسطر الرابع';
// نصّ طويل بلا مسافات — أسوأ حالة لتخطيط الجدول: يجب أن يلتفّ لا أن يمدّد الجدول.
const LONG_TEXT = 'ط'.repeat(1200);

function item(overrides: Record<string, unknown> = {}) {
  return {
    archiveItemId: ITEM_ID,
    itemType: 'Report',
    employeeId: 'emp-1',
    employeeName: 'موظّف الأرشيف',
    templateName: 'تقرير أسبوعيّ',
    periodKey: '2026-W20',
    status: 'Closed',
    deletedAtUtc: '2026-05-20T08:00:00Z',
    deletedByUserId: 'admin-1',
    deletedByName: 'المشرف',
    deletionReason: 'سبب الحذف',
    canRestore: true,
    restoreBlockedCode: null,
    restoreBlockedReason: null,
    daysSinceDeletion: 3,
    retentionStatus: 'Fresh',
    ...overrides,
  };
}

function details(comment: string | null) {
  return {
    ...item(),
    currentApproverId: null,
    currentApproverName: null,
    workflowSteps: [
      {
        level: 1,
        approverId: 'approver-1',
        approverName: 'المعتمِد',
        status: 'Returned',
        comment,
        decidedAtUtc: '2026-05-19T09:00:00Z',
      },
    ],
    fieldValuesCount: 0,
    kpiResultsCount: 0,
    reviewEventsCount: 0,
    auditTrail: [],
    historicalApproverId: null,
    historicalApproverName: null,
    historicalApproverIsActive: null,
    restoreStrategy: 'Direct',
    restoreWarning: null,
  };
}

function mockApi(comment: string | null) {
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url === '/admin/archive')
      return Promise.resolve({
        data: { items: [item()], totalCount: 1, page: 1, pageSize: 20 },
      } as never);
    if (url.startsWith('/admin/archive/'))
      return Promise.resolve({ data: details(comment) } as never);
    return Promise.resolve({ data: [] } as never);
  });
}

async function openDetails(comment: string | null) {
  mockApi(comment);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <AdminArchivePage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  const button = await screen.findByRole('button', { name: 'التفاصيل' });
  await userEvent.click(button);
  await waitFor(() => expect(screen.getByText('لقطة سير العمل')).toBeInTheDocument());
}

/** خليّة التعليق: الرابعة في صفّ جدول لقطة سير العمل. */
function commentCell(): HTMLElement {
  const header = screen.getByText('لقطة سير العمل');
  const table = header.parentElement!.querySelector('table')!;
  const row = table.querySelector('tbody tr')!;
  return row.querySelectorAll('td')[3] as HTMLElement;
}

describe('AdminArchivePage — أسطر تعليق الاعتماد في لقطة سير العمل', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('يحفظ الأسطر المتعدّدة نصًّا وعرضًا (whitespace-pre-wrap)', async () => {
    await openDetails(MULTILINE);
    const cell = commentCell();

    // 1) النصّ وصل حرفيًّا بأسطره — لا تطبيع ولا حذف للسطر الفارغ.
    expect(cell.textContent).toBe(MULTILINE);
    expect(cell.textContent?.split('\n')).toHaveLength(4);
    // 2) والعرض يحترمها: بلا pre-wrap يطوي المتصفّح \n إلى مسافة رغم صحّة النصّ.
    expect(cell.className).toContain('whitespace-pre-wrap');
  });

  it('لا يكسر تخطيط الجدول بنصّ طويل بلا مسافات (break-words)', async () => {
    await openDetails(LONG_TEXT);
    const cell = commentCell();

    expect(cell.textContent).toBe(LONG_TEXT);
    expect(cell.className).toContain('break-words');
    // بقيّة أعمدة الصفّ ما زالت موجودة ⟹ الصفّ لم ينهَر.
    const row = cell.parentElement!;
    expect(row.querySelectorAll('td')).toHaveLength(5);
    expect(row.querySelectorAll('td')[1].textContent).toBe('المعتمِد');
  });

  it('يبقي التوافق الخلفيّ: تعليق فارغ يُعرض شرطة لا نصًّا فارغًا', async () => {
    await openDetails(null);
    expect(commentCell().textContent).toBe('—');
  });

  it('قراءة باردة: المصدر استجابة الـAPI وحدها، والتعليق التاريخيّ أحاديّ السطر يُعرض كما كان', async () => {
    // «القراءة الباردة» هي العقد الحاكم لهذا السطح: لا إدخال في هذه الجلسة إطلاقًا —
    // القيمة مُخزَّنة سلفًا وتُقرأ من نقطة تفاصيل الأرشيف. لو انكسر العرض هنا لضاع قرار
    // لم يعد قابلًا للتحرير أصلًا (العنصر محذوف إداريًّا).
    const legacy = 'تعليق تاريخيّ أحاديّ السطر بلا أسطر جديدة';
    await openDetails(legacy);

    expect(api.get).toHaveBeenCalledWith(`/admin/archive/report/${ITEM_ID}`);
    const cell = commentCell();
    expect(cell.textContent).toBe(legacy);
    expect(cell.textContent).not.toContain('\n');
    // pre-wrap لا يُدخِل أسطرًا لم تكن موجودة — يحفظ الموجود فقط.
    expect(cell.className).toContain('whitespace-pre-wrap');
  });
});
